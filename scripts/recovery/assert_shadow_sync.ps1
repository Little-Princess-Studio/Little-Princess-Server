# Integration test: server-side shadow entity property sync.
#
# Scenario:
#   1. Boot cluster (host0 default).
#   2. Launch LPS.Client.Demo with --run send.authority send.login send.debug_shadow <newName>
#      The client logs in -> becomes a Player on either server0 or server1.
#      DebugCreateShadowAndMutate runs on whichever server hosts the Player
#      ("ori-server"), spawns a shadow on the peer server ("shadow-server"),
#      and mutates Player.Name = <newName>.
#   3. The Gate's shadow fan-out (Gate.ClientMessages.cs:HandlePropertySync...)
#      forwards the PropertySyncCommandList to the shadow-server.
#   4. The shadow-server applies it via BaseEntity.ApplySyncCommandList.
#      Logs: "Seeded shadow ... from PropertyFullSync." +
#            "Created shadow ... (ori=..., class=Player)" in cluster log.
#
# Assertions:
#   * One server logs "[DebugShadow] ori=... creating shadow on serverN."
#   * Gate log "[Gate] Routed CreateShadowEntity"
#   * Peer server logs "Created shadow"
#   * Peer server logs "Seeded shadow ... from PropertyFullSync" (R2 Option B)
#   * Side-channel result file written by send.debug_shadow shows non-empty mutation result
[CmdletBinding()]
param(
    [int]$ReadyTimeoutSec = 30,
    [int]$SettleAfterReadySec = 12,
    [int]$ClientTimeoutSec = 30,
    [string]$NewName = "ShadowSyncTest-" + (Get-Random),
    [string]$SupervisorBase = 'http://localhost:7090'
)

$ErrorActionPreference = 'Stop'
$repoRoot   = (Resolve-Path "$PSScriptRoot/../..").Path
$demoDir    = Join-Path $repoRoot 'LPS.Server.Demo'
$clientDir  = Join-Path $repoRoot 'LPS.Client.Demo'
$logDir     = Join-Path $demoDir 'logs'
$clusterLog = Join-Path $logDir 'dev_cluster.log'

$failures = New-Object System.Collections.ArrayList

function Write-Step($m) { Write-Host "[shadow-test] $m" -ForegroundColor Cyan }
function Write-Pass($m) { Write-Host "[shadow-test]   PASS: $m" -ForegroundColor Green }
function Write-Fail($m) { Write-Host "[shadow-test]   FAIL: $m" -ForegroundColor Red; [void]$failures.Add($m) }

function Get-Status() {
    try { return Invoke-RestMethod -Uri "$SupervisorBase/supervisor/status" -TimeoutSec 5 }
    catch { return $null }
}

function Wait-AllAlive([int]$timeoutSec) {
    $deadline = (Get-Date).AddSeconds($timeoutSec)
    while ((Get-Date) -lt $deadline) {
        $s = Get-Status
        if ($s -and $s.instances) {
            $bad = $s.instances | Where-Object { -not $_.alive -or $_.hasExited }
            if ($bad.Count -eq 0 -and $s.instances.Count -ge 9) { return $true }
        }
        Start-Sleep -Milliseconds 500
    }
    return $false
}

# ----- 1. Boot cluster -----

Write-Step "Stopping any leftover cluster..."
& pwsh (Join-Path $repoRoot 'scripts/proc.ps1') stop all *>$null

Write-Step "Starting cluster..."
& pwsh (Join-Path $repoRoot 'scripts/proc.ps1') start cluster *>$null

if (-not (Wait-AllAlive $ReadyTimeoutSec)) {
    Write-Host "[shadow-test] Cluster failed to come up." -ForegroundColor Red
    Get-Status | ConvertTo-Json -Depth 4 | Out-String | Write-Host
    & pwsh (Join-Path $repoRoot 'scripts/proc.ps1') stop all *>$null
    exit 2
}
Write-Pass "all 9 instances alive."
Start-Sleep -Seconds $SettleAfterReadySec

# ----- 2. Drive demo via client -----

$cutoffUtc  = (Get-Date).ToUniversalTime().AddSeconds(-2)
$clientLog  = Join-Path $env:TEMP "shadow_test_client.log"
$resultFile = Join-Path $env:TEMP "shadow_test_result.txt"
Remove-Item $clientLog, "$clientLog.err", $resultFile -ErrorAction SilentlyContinue

Write-Step "Launching LPS.Client.Demo with debug_shadow flow..."
$env:LPS_DEBUG_SHADOW_RESULT_FILE = $resultFile
$cmdArgs = @(
    'run','--no-launch-profile','--no-build','--',
    '--run','send.authority','send.login',"`"send.debug_shadow $NewName`""
)
# When PowerShell hands `cmdArgs` to Start-Process it must keep the trailing
# multi-word arg intact (single string element). We pass each command name as
# its own element above; the "send.debug_shadow $NewName" pair stays as one
# element so CommandParser.Dispatch sees a single "command name + argument"
# line instead of splitting on spaces.
$clientProc = Start-Process -FilePath 'dotnet' -ArgumentList $cmdArgs `
    -WorkingDirectory $clientDir `
    -RedirectStandardOutput $clientLog `
    -RedirectStandardError  "$clientLog.err" `
    -WindowStyle Hidden -PassThru
Remove-Item Env:\LPS_DEBUG_SHADOW_RESULT_FILE -ErrorAction SilentlyContinue

# Wait for either the result file or the client to exit / time out.
$deadline = (Get-Date).AddSeconds($ClientTimeoutSec)
while ((Get-Date) -lt $deadline) {
    if (Test-Path $resultFile)            { break }
    if ($clientProc.HasExited)            { break }
    Start-Sleep -Milliseconds 250
}

if (-not $clientProc.HasExited) {
    Stop-Process -Id $clientProc.Id -Force -ErrorAction SilentlyContinue
}

if (Test-Path $resultFile) {
    $resultText = (Get-Content $resultFile -Raw).Trim()
    Write-Pass "client produced result: $resultText"
} else {
    Write-Fail "client did not produce result file within ${ClientTimeoutSec}s"
    Write-Host "--- tail of client log ---"
    Get-Content $clientLog -ErrorAction SilentlyContinue | Select-Object -Last 25 | Out-String | Write-Host
    Write-Host "--- tail of client err ---"
    Get-Content "$clientLog.err" -ErrorAction SilentlyContinue | Select-Object -Last 15 | Out-String | Write-Host
}

# ----- 3. Inspect cluster log for evidence -----

Write-Step "Searching cluster log for shadow lifecycle events..."

function Test-LogPattern([string]$label, [string]$pattern) {
    if (Test-Path $clusterLog) {
        $hits = Select-String -Path $clusterLog -Pattern $pattern -SimpleMatch -ErrorAction SilentlyContinue
        if ($hits) {
            foreach ($h in $hits) {
                if ($h.Line -match '\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d+') {
                    try {
                        $ts = [DateTime]::Parse($Matches[0])
                        if ($ts.ToUniversalTime() -ge $cutoffUtc) {
                            Write-Pass "$label : matched"
                            return
                        }
                    } catch { }
                }
            }
        }
    }
    Write-Fail "$label : no match for '$pattern' since cutoff"
}

Test-LogPattern 'ori-server initiated shadow create' '[DebugShadow] ori='
Test-LogPattern 'Gate routed CreateShadowEntity'    '[Gate] Routed CreateShadowEntity'
Test-LogPattern 'target server created shadow'      'Created shadow'
Test-LogPattern 'target server seeded from FullSync' 'Seeded shadow'
Test-LogPattern 'ori-server got Res, emitted FullSync' 'Shadow created on target'

# ----- 4. Teardown -----

Write-Step "Stopping cluster..."
& pwsh (Join-Path $repoRoot 'scripts/proc.ps1') stop all *>$null

if ($failures.Count -eq 0) {
    Write-Host "[shadow-test] ALL PASS" -ForegroundColor Green
    exit 0
} else {
    Write-Host "[shadow-test] $($failures.Count) FAILURE(S):" -ForegroundColor Red
    foreach ($f in $failures) { Write-Host "  - $f" -ForegroundColor Red }
    exit 1
}
