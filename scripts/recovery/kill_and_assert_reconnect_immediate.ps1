# Integration test: TcpClient reconnect on the IMMEDIATE host-manager path.
#
# The default Config/host0/ has use_mq_to_host=true everywhere so
# ImmediateHostManagerConnectionOfGate/Server are not exercised. This script
# boots an alternate cluster (Config/host0_immediate/) where gate0 + server0
# use TCP to HostManager, then crashes hostmanager and asserts that gate0 /
# server0 reconnect via OnReconnected -> Control{Restart}.
#
# Differences vs kill_and_assert_reconnect.ps1:
#   - Boots dotnet run -- bydefault --headless --config-dir Config/host0_immediate/
#     directly (bypassing scripts/proc.ps1, which is hardcoded to host0/).
#   - Asserts on "[host] restart-registering Gate gate0" and
#     "[host] restart-registering Server server0" - the new HostManager log
#     line added by C3 - which only fires for the Immediate path because
#     Control{Restart} is the message we send on reconnect.
[CmdletBinding()]
param(
    [int]$ReadyTimeoutSec = 45,
    [int]$ReconnectTimeoutSec = 35,
    [int]$SettleAfterReadySec = 10,
    [string]$SupervisorBase = 'http://localhost:7090'
)

$ErrorActionPreference = 'Stop'
$repoRoot   = (Resolve-Path "$PSScriptRoot/../..").Path
$demoDir    = Join-Path $repoRoot 'LPS.Server.Demo'
$logDir     = Join-Path $demoDir 'logs'
$clusterLog = Join-Path $logDir 'dev_cluster_immediate.log'
$clusterErr = Join-Path $logDir 'dev_cluster_immediate.err'
$pidFile    = Join-Path $logDir 'dev_cluster_immediate.pid'

$failures = New-Object System.Collections.ArrayList

function Write-Step($m) { Write-Host "[recovery-imm] $m" -ForegroundColor Cyan }
function Write-Pass($m) { Write-Host "[recovery-imm]   PASS: $m" -ForegroundColor Green }
function Write-Fail($m) { Write-Host "[recovery-imm]   FAIL: $m" -ForegroundColor Red; [void]$failures.Add($m) }

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

function Get-InstancePid([string]$name) {
    $s = Get-Status
    if (-not $s) { return $null }
    $inst = $s.instances | Where-Object name -eq $name
    if (-not $inst) { return $null }
    return [int]$inst.pid
}

function Wait-NewPid([string]$name, [int]$oldPid, [int]$timeoutSec) {
    $deadline = (Get-Date).AddSeconds($timeoutSec)
    while ((Get-Date) -lt $deadline) {
        $newPid = Get-InstancePid $name
        if ($newPid -and $newPid -ne $oldPid) {
            $s = Get-Status
            $inst = $s.instances | Where-Object name -eq $name
            if ($inst.alive -and -not $inst.hasExited) { return $newPid }
        }
        Start-Sleep -Milliseconds 500
    }
    return $null
}

function Wait-LogContains([string]$pattern, [int]$timeoutSec, [DateTime]$sinceUtc) {
    $deadline = (Get-Date).AddSeconds($timeoutSec)
    while ((Get-Date) -lt $deadline) {
        if (Test-Path $clusterLog) {
            $hits = Select-String -Path $clusterLog -Pattern $pattern -SimpleMatch -ErrorAction SilentlyContinue
            if ($hits) {
                foreach ($h in $hits) {
                    if ($h.Line -match '\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d+') {
                        try {
                            $ts = [DateTime]::Parse($Matches[0])
                            if ($ts.ToUniversalTime() -ge $sinceUtc) { return $true }
                        } catch { }
                    }
                }
            }
        }
        Start-Sleep -Milliseconds 500
    }
    return $false
}

function Stop-OurCluster {
    # Best-effort teardown: kill our supervisor process + everything it spawned.
    if (Test-Path $pidFile) {
        try {
            $pp = [int](Get-Content $pidFile)
            Get-CimInstance Win32_Process -Filter "ParentProcessId=$pp" -ErrorAction SilentlyContinue | ForEach-Object {
                Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue
            }
            Stop-Process -Id $pp -Force -ErrorAction SilentlyContinue
        } catch { }
        Remove-Item $pidFile -ErrorAction SilentlyContinue
    }
}

# ----- 0. Stop any existing cluster (host0 or our previous host0_immediate) -----

Write-Step "Stopping any leftover clusters..."
& pwsh (Join-Path $repoRoot 'scripts/proc.ps1') stop all *>$null
Stop-OurCluster

# ----- 1. Boot host0_immediate cluster -----

Write-Step "Booting Config/host0_immediate cluster via dotnet run --config-dir..."
New-Item -ItemType Directory -Path $logDir -Force | Out-Null

# Use cmd /c start /B so dotnet run runs detached and we get a stable PID file.
$cmdLine = "dotnet run --no-build -- bydefault --headless --config-dir Config/host0_immediate/ > `"$clusterLog`" 2> `"$clusterErr`""
$proc = Start-Process -FilePath 'cmd' -ArgumentList '/c', $cmdLine `
    -WorkingDirectory $demoDir -PassThru -WindowStyle Hidden
$proc.Id | Out-File $pidFile -Encoding ASCII
Write-Step "Launched supervisor wrapper pid=$($proc.Id)."

Write-Step "Waiting for all 9 instances to report alive (timeout ${ReadyTimeoutSec}s)..."
if (-not (Wait-AllAlive $ReadyTimeoutSec)) {
    Write-Host "[recovery-imm] Cluster failed to come up." -ForegroundColor Red
    Get-Status | ConvertTo-Json -Depth 4 | Out-String | Write-Host
    if (Test-Path $clusterLog) {
        Write-Host "--- tail of $clusterLog ---" -ForegroundColor Yellow
        Get-Content $clusterLog -Tail 40
    }
    Stop-OurCluster
    exit 2
}
Write-Pass "all 9 instances alive (immediate config)."

Write-Step "Settling for ${SettleAfterReadySec}s..."
Start-Sleep -Seconds $SettleAfterReadySec

# ----- 2. Kill hostmanager, assert gate0 (Immediate path) reconnects -----

Write-Step "Killing hostmanager to exercise Immediate*Connection reconnect..."
$hmOldPid = Get-InstancePid 'hostmanager'
if (-not $hmOldPid) {
    Write-Fail "could not find hostmanager pid"
} else {
    $cutoffUtc = (Get-Date).ToUniversalTime().AddSeconds(-2)
    Stop-Process -Id $hmOldPid -Force -ErrorAction SilentlyContinue
    Write-Step "Killed hostmanager pid=$hmOldPid; waiting for respawn..."

    $hmNewPid = Wait-NewPid 'hostmanager' $hmOldPid $ReconnectTimeoutSec
    if (-not $hmNewPid) {
        Write-Fail "hostmanager did not respawn within ${ReconnectTimeoutSec}s"
    } else {
        Write-Pass "hostmanager respawned: $hmOldPid -> $hmNewPid"

        # gate0 uses Immediate path (use_mq_to_host=false in host0_immediate
        # gate.conf.json). On reconnect it sends Control{Restart} ->
        # HostMgr logs "restart-registering Gate <mailbox-id>". MailBox.Id
        # is an internal base64 token, not the supervisor display name, so
        # we match on the role prefix only.
        Write-Step "Searching for HostMgr 'restart-registering Gate '..."
        if (Wait-LogContains 'restart-registering Gate ' $ReconnectTimeoutSec $cutoffUtc) {
            Write-Pass "HostMgr accepted Gate's Control.Restart (Immediate path verified)."
        } else {
            Write-Fail "no 'restart-registering Gate ' log line in ${ReconnectTimeoutSec}s."
        }

        # server0 also uses Immediate path; assert same.
        Write-Step "Searching for HostMgr 'restart-registering Server '..."
        if (Wait-LogContains 'restart-registering Server ' $ReconnectTimeoutSec $cutoffUtc) {
            Write-Pass "HostMgr accepted Server's Control.Restart (Immediate path verified)."
        } else {
            Write-Fail "no 'restart-registering Server ' log line in ${ReconnectTimeoutSec}s."
        }
    }
}

# ----- 3. Teardown -----

Write-Step "Stopping host0_immediate cluster..."
Stop-OurCluster

if ($failures.Count -eq 0) {
    Write-Host "[recovery-imm] ALL PASS" -ForegroundColor Green
    exit 0
} else {
    Write-Host "[recovery-imm] $($failures.Count) FAILURE(S):" -ForegroundColor Red
    foreach ($f in $failures) { Write-Host "  - $f" -ForegroundColor Red }
    exit 1
}
