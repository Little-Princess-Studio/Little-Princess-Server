# Background-process helper for cluster / webmgr.
# Used by Sisyphus to spawn and restart detached processes WITHOUT blocking
# the shell. Output goes to logs/<name>.log (+ .err); PID file in logs/<name>.pid.
#
# Usage (from any cwd):
#   pwsh scripts/proc.ps1 start cluster      # dotnet run -- bydefault --headless
#   pwsh scripts/proc.ps1 start webmgr       # WebManager on http://localhost:7088
#   pwsh scripts/proc.ps1 stop  cluster
#   pwsh scripts/proc.ps1 stop  webmgr
#   pwsh scripts/proc.ps1 restart cluster
#   pwsh scripts/proc.ps1 status
#
# Returns immediately. No readiness wait - check logs/<name>.log if needed.
param(
    [Parameter(Mandatory=$true)][ValidateSet('start','stop','restart','status')]
    [string]$action,
    [ValidateSet('cluster','webmgr','all','')]
    [string]$target = ''
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$logDir = Join-Path $root 'LPS.Server.Demo\logs'
New-Item -ItemType Directory -Path $logDir -Force | Out-Null

$specs = @{
    cluster = @{
        exe  = 'dotnet'
        args = @('run','--no-build','--','bydefault','--headless')
        cwd  = Join-Path $root 'LPS.Server.Demo'
        log  = Join-Path $logDir 'dev_cluster.log'
        pid_ = Join-Path $logDir 'dev_cluster.pid'
    }
    webmgr = @{
        exe  = 'dotnet'
        args = @('run','--no-build','--no-launch-profile','--urls','http://localhost:7088')
        cwd  = Join-Path $root 'LPS.Server.WebManager'
        log  = Join-Path $logDir 'dev_webmgr.log'
        pid_ = Join-Path $logDir 'dev_webmgr.pid'
    }
}

function StopOne([string]$name) {
    $spec = $specs[$name]
    if (Test-Path $spec.pid_) {
        $procId = [int](Get-Content $spec.pid_)
        try { Get-CimInstance Win32_Process -Filter "ParentProcessId=$procId" | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue } } catch {}
        Stop-Process -Id $procId -Force -ErrorAction SilentlyContinue
        Remove-Item $spec.pid_ -ErrorAction SilentlyContinue
        Write-Host "[$name] stopped (pid=$procId)"
    } else {
        # Fallback: find by command line
        $matches = Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" |
            Where-Object { $_.CommandLine -like "*$($spec.args -join ' ')*" -or ($name -eq 'cluster' -and $_.CommandLine -like '*bydefault*') -or ($name -eq 'webmgr' -and $_.CommandLine -like '*WebManager*') }
        if ($matches) {
            $matches | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
            Write-Host "[$name] stopped (by cmdline match)"
        } else {
            Write-Host "[$name] not running"
        }
    }
}

function StartOne([string]$name) {
    $spec = $specs[$name]
    "" | Set-Content -LiteralPath $spec.log -Encoding utf8
    $p = Start-Process -FilePath $spec.exe -ArgumentList $spec.args -WorkingDirectory $spec.cwd `
            -RedirectStandardOutput $spec.log -RedirectStandardError "$($spec.log).err" `
            -PassThru -WindowStyle Hidden
    $p.Id | Set-Content -LiteralPath $spec.pid_
    Write-Host "[$name] started pid=$($p.Id) log=$($spec.log)"
}

function StatusOne([string]$name) {
    $spec = $specs[$name]
    if (Test-Path $spec.pid_) {
        $procId = [int](Get-Content $spec.pid_)
        $proc = Get-Process -Id $procId -ErrorAction SilentlyContinue
        if ($proc) { Write-Host "[$name] alive pid=$procId" }
        else { Write-Host "[$name] DEAD (stale pidfile pid=$procId)" }
    } else { Write-Host "[$name] not started (no pidfile)" }
}

$targets = if ($target -eq '' -or $target -eq 'all') { @('cluster','webmgr') } else { @($target) }

switch ($action) {
    'stop'    { $targets | ForEach-Object { StopOne $_ } }
    'start'   { $targets | ForEach-Object { StartOne $_ } }
    'restart' { $targets | ForEach-Object { StopOne $_; Start-Sleep -Milliseconds 1500; StartOne $_ } }
    'status'  { $targets | ForEach-Object { StatusOne $_ } }
}
