# Integration test: TcpClient reconnect after a peer process crashes.
#
# Scenario (default config, host0/):
#   1. Boot the cluster via scripts/proc.ps1 start cluster.
#   2. Wait until /supervisor/status shows all 9 instances alive.
#   3. Pick a target Server PID, kill it with Stop-Process -Force.
#      (Do NOT use POST /supervisor/instance/.../stop - that path marks the
#       name as DeliberatelyStopping and the auto-restart branch will skip it.)
#   4. The supervisor's non-zero-exit auto-restart respawns the Server.
#   5. Assert: the Gate's TcpClient to that Server reconnects, evidenced by
#      "Reconnected to <ip>:<port>" in dev_cluster.log within $TimeoutSec.
#   6. Same drill for DbManager <-> HostManager (always TCP; HostMgr crash
#      exercises DbManager's OnReconnected -> Control{Restart} flow which
#      hits HostManager.Register.cs:223 RestartInstance).
#
# Exit code:
#   0 - all assertions pass
#   1 - any assertion failed (details printed)
#   2 - cluster failed to come up or supervisor HTTP unreachable
#
# Run from repo root:
#   pwsh scripts/recovery/kill_and_assert_reconnect.ps1
[CmdletBinding()]
param(
    [int]$ReadyTimeoutSec = 30,
    [int]$ReconnectTimeoutSec = 25,
    [int]$SettleAfterReadySec = 8,
    [string]$SupervisorBase = 'http://localhost:7090'
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path "$PSScriptRoot/../..").Path
$clusterLog = Join-Path $repoRoot 'LPS.Server.Demo/logs/dev_cluster.log'
$failures = New-Object System.Collections.ArrayList

function Write-Step($msg) {
    Write-Host "[recovery] $msg" -ForegroundColor Cyan
}

function Write-Pass($msg) {
    Write-Host "[recovery]   PASS: $msg" -ForegroundColor Green
}

function Write-Fail($msg) {
    Write-Host "[recovery]   FAIL: $msg" -ForegroundColor Red
    [void]$failures.Add($msg)
}

function Get-Status() {
    try {
        return Invoke-RestMethod -Uri "$SupervisorBase/supervisor/status" -TimeoutSec 5
    } catch {
        return $null
    }
}

function Wait-AllAlive([int]$timeoutSec) {
    $deadline = (Get-Date).AddSeconds($timeoutSec)
    while ((Get-Date) -lt $deadline) {
        $s = Get-Status
        if ($s -and $s.instances) {
            $bad = $s.instances | Where-Object { -not $_.alive -or $_.hasExited }
            if ($bad.Count -eq 0 -and $s.instances.Count -ge 9) {
                return $true
            }
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
            if ($inst.alive -and -not $inst.hasExited) {
                return $newPid
            }
        }
        Start-Sleep -Milliseconds 500
    }
    return $null
}

function Wait-LogContains([string]$pattern, [int]$timeoutSec, [DateTime]$sinceUtc) {
    $deadline = (Get-Date).AddSeconds($timeoutSec)
    while ((Get-Date) -lt $deadline) {
        if (Test-Path $clusterLog) {
            $matches_ = Select-String -Path $clusterLog -Pattern $pattern -SimpleMatch -ErrorAction SilentlyContinue
            if ($matches_) {
                # Check at least one match is after sinceUtc - the log files
                # retain prior runs so we must filter by recency. The
                # cluster log lines start with [name] then a timestamp like
                # "2026-05-22 23:54:20.6636" - parse leniently.
                foreach ($m in $matches_) {
                    $line = $m.Line
                    if ($line -match '\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d+') {
                        try {
                            $ts = [DateTime]::Parse($Matches[0])
                            if ($ts.ToUniversalTime() -ge $sinceUtc) {
                                return $true
                            }
                        } catch {
                            # Fall through to next match.
                        }
                    }
                }
            }
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

Write-Step "Waiting for all 9 instances to report alive (timeout ${ReadyTimeoutSec}s)..."
if (-not (Wait-AllAlive $ReadyTimeoutSec)) {
    Write-Host "[recovery] Cluster failed to come up." -ForegroundColor Red
    Get-Status | ConvertTo-Json -Depth 4 | Out-String | Write-Host
    & pwsh (Join-Path $repoRoot 'scripts/proc.ps1') stop all *>$null
    exit 2
}
Write-Pass "all 9 instances alive."

Write-Step "Settling for ${SettleAfterReadySec}s (registration / HostStatus=Running)..."
Start-Sleep -Seconds $SettleAfterReadySec

# ----- 2. Kill server0, assert reconnect -----

Write-Step "Killing server0..."
$srvOldPid = Get-InstancePid 'server0'
if (-not $srvOldPid) {
    Write-Fail "could not find server0 pid"
} else {
    $cutoffUtc = (Get-Date).ToUniversalTime().AddSeconds(-2)
    Stop-Process -Id $srvOldPid -Force -ErrorAction SilentlyContinue
    Write-Step "Killed server0 pid=$srvOldPid; waiting for respawn (timeout ${ReconnectTimeoutSec}s)..."

    $srvNewPid = Wait-NewPid 'server0' $srvOldPid $ReconnectTimeoutSec
    if (-not $srvNewPid) {
        Write-Fail "server0 did not respawn within ${ReconnectTimeoutSec}s"
    } else {
        Write-Pass "server0 respawned: $srvOldPid -> $srvNewPid"

        # Gate side should have logged "Reconnected to 127.0.0.1:12001"
        # (port from Config/host0/server.conf.json) somewhere in the cluster log.
        Write-Step "Searching dev_cluster.log for Gate->Server reconnect..."
        if (Wait-LogContains 'Reconnected to 127.0.0.1:12001' $ReconnectTimeoutSec $cutoffUtc) {
            Write-Pass "Gate logged reconnect to server0 (port 12001)."
        } else {
            Write-Fail "no 'Reconnected to 127.0.0.1:12001' log line found in ${ReconnectTimeoutSec}s after kill."
        }
    }
}

# ----- 3. Kill hostmanager, assert DbManager reconnect (always TCP) -----

Write-Step "Killing hostmanager..."
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

        # DbManager's OnReconnected sends Control.Restart -> HostMgr logs
        # "restart-registering Dbmanager dbmanager". This exercises the
        # previously-empty RemoteType.Dbmanager arm filled in C3.
        Write-Step "Searching dev_cluster.log for HostMgr restart-registering Dbmanager..."
        if (Wait-LogContains 'restart-registering Dbmanager dbmanager' $ReconnectTimeoutSec $cutoffUtc) {
            Write-Pass "HostMgr accepted DbManager's Control.Restart (restart-registering)."
        } else {
            Write-Fail "no 'restart-registering Dbmanager' log line found within ${ReconnectTimeoutSec}s."
        }
    }
}

# ----- 4. Teardown -----

Write-Step "Stopping cluster..."
& pwsh (Join-Path $repoRoot 'scripts/proc.ps1') stop all *>$null

if ($failures.Count -eq 0) {
    Write-Host "[recovery] ALL PASS" -ForegroundColor Green
    exit 0
} else {
    Write-Host "[recovery] $($failures.Count) FAILURE(S):" -ForegroundColor Red
    foreach ($f in $failures) { Write-Host "  - $f" -ForegroundColor Red }
    exit 1
}
