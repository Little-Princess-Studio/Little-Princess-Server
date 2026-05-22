# Starts the LPS cluster headlessly and keeps it running until you press Ctrl+C.
# On Ctrl+C it sends the graceful "shutdown\n" stdin signal so child processes
# tear down cleanly (see StartupManager). Pair with scripts/dev_webmgr.ps1 in a
# second terminal for a hot-reloading WebManager dev loop.
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $root

Write-Host ">> building LPS.sln (incremental)..." -ForegroundColor Cyan
dotnet build LPS.sln --nologo -v quiet | Out-Host
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$logDir = Join-Path $root "LPS.Server.Demo\logs"
New-Item -ItemType Directory -Path $logDir -Force | Out-Null
$logPath = Join-Path $logDir "dev_cluster.log"
"" | Set-Content -LiteralPath $logPath

Write-Host ">> launching cluster (logs -> $logPath)" -ForegroundColor Cyan
$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = "dotnet"
$psi.Arguments = "run --no-build -- bydefault --headless"
$psi.WorkingDirectory = Join-Path $root "LPS.Server.Demo"
$psi.RedirectStandardInput = $true
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$psi.UseShellExecute = $false
$proc = [System.Diagnostics.Process]::Start($psi)

$writer = [System.IO.StreamWriter]::new($logPath, $true, [System.Text.Encoding]::UTF8)
$writer.AutoFlush = $true
$pumpOut = Register-ObjectEvent -InputObject $proc -EventName OutputDataReceived -Action {
    if ($EventArgs.Data) { $Event.MessageData.WriteLine($EventArgs.Data); Write-Host $EventArgs.Data }
} -MessageData $writer
$pumpErr = Register-ObjectEvent -InputObject $proc -EventName ErrorDataReceived -Action {
    if ($EventArgs.Data) { $Event.MessageData.WriteLine("[ERR] " + $EventArgs.Data); Write-Host -ForegroundColor Red $EventArgs.Data }
} -MessageData $writer
$proc.BeginOutputReadLine()
$proc.BeginErrorReadLine()

$cleanup = {
    Write-Host "`n>> sending graceful shutdown..." -ForegroundColor Yellow
    try { $proc.StandardInput.WriteLine("shutdown"); $proc.StandardInput.Flush(); $proc.StandardInput.Close() } catch {}
    if (-not $proc.WaitForExit(20000)) {
        Write-Host ">> timeout, killing tree" -ForegroundColor Red
        try { $proc.Kill($true) } catch {}
    }
    Unregister-Event -SourceIdentifier $pumpOut.Name -ErrorAction SilentlyContinue
    Unregister-Event -SourceIdentifier $pumpErr.Name -ErrorAction SilentlyContinue
    $writer.Dispose()
    Write-Host ">> cluster stopped" -ForegroundColor Cyan
}

try {
    Write-Host ">> cluster running. Press Ctrl+C to stop." -ForegroundColor Green
    while (-not $proc.HasExited) { Start-Sleep -Milliseconds 500 }
} finally {
    & $cleanup
}
