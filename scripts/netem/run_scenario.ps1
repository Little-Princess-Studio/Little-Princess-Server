#requires -Version 7
<#
.SYNOPSIS
Run one bench scenario: start optional proxy, run client demo with send.bench,
parse the result JSON line, return as a hashtable.

Assumes:
  - cluster already running (proc.ps1 start cluster), Gate KCP=11002, TCP=11001
  - LPS.Client.Demo binaries built

.PARAMETER Label       Friendly label printed and stored in result
.PARAMETER Transport   tcp | kcp
.PARAMETER Port        Gate port to connect to (11001 TCP, 11002 KCP, or proxy port)
.PARAMETER ProxyCmd    Optional - if set, "python script.py args..." launched in background
.PARAMETER Count       Number of echo RPCs (default 100)
.PARAMETER TimeoutMs   Per-RPC timeout in ms (default 5000)
#>
param(
    [Parameter(Mandatory)] [string] $Label,
    [Parameter(Mandatory)] [ValidateSet('tcp','kcp')] [string] $Transport,
    [Parameter(Mandatory)] [int] $Port,
    [string] $ProxyCmd = '',
    [int] $Count = 100,
    [int] $TimeoutMs = 5000
)

$ErrorActionPreference = 'Stop'

$proxyProc = $null
if ($ProxyCmd) {
    Write-Host "[scenario:$Label] launching proxy: $ProxyCmd"
    # Background python proc, stdout to a log so we can correlate.
    $proxyOut = Join-Path $env:TEMP "bench_proxy_${Label}.out"
    $proxyErr = Join-Path $env:TEMP "bench_proxy_${Label}.err"
    Remove-Item $proxyOut, $proxyErr -ErrorAction SilentlyContinue
    # Start-Process refuses identical paths for stdout+stderr.
    $proxyProc = Start-Process -FilePath 'python' -ArgumentList $ProxyCmd `
        -RedirectStandardOutput $proxyOut `
        -RedirectStandardError $proxyErr `
        -WindowStyle Hidden -PassThru
    Start-Sleep 2
    if ($proxyProc.HasExited) {
        $tail = (Get-Content $proxyOut, $proxyErr -ErrorAction SilentlyContinue | Out-String)
        throw "[scenario:$Label] proxy exited immediately. Log:`n$tail"
    }
}

try {
    $clientLog = Join-Path $env:TEMP "bench_client_$Label.log"
    $resultFile = Join-Path $env:TEMP "bench_result_$Label.json"
    Remove-Item $clientLog, $resultFile -ErrorAction SilentlyContinue

    $args = @(
        'run','--no-launch-profile','--no-build','--',
        '--transport', $Transport,
        '--port', $Port,
        '--bench', $Count, $TimeoutMs
    )
    Write-Host "[scenario:$Label] dotnet $($args -join ' ')"
    $env:LPS_BENCH_RESULT_FILE = $resultFile
    $clientProc = Start-Process -FilePath 'dotnet' -ArgumentList $args `
        -WorkingDirectory (Resolve-Path 'LPS.Client.Demo') `
        -RedirectStandardOutput $clientLog `
        -RedirectStandardError "$clientLog.err" `
        -WindowStyle Hidden -PassThru
    Remove-Item Env:\LPS_BENCH_RESULT_FILE -ErrorAction SilentlyContinue

    # Wait for the side file to appear (written just before Environment.Exit
    # inside send.bench). This sidesteps all Start-Process stdout buffering.
    $maxWaitSec = [Math]::Min(100, [int]([math]::Ceiling($Count * 0.5)) + 35)
    Write-Host "[scenario:$Label] waiting up to ${maxWaitSec}s for result file..."

    $deadline = (Get-Date).AddSeconds($maxWaitSec)
    while ((Get-Date) -lt $deadline) {
        if (Test-Path $resultFile) { break }
        if ($clientProc.HasExited) {
            Start-Sleep 1
            break
        }
        Start-Sleep -Milliseconds 250
    }

    if (-not $clientProc.HasExited) {
        Stop-Process -Id $clientProc.Id -Force -ErrorAction SilentlyContinue
    }

    if (-not (Test-Path $resultFile)) {
        $tail = (Get-Content $clientLog -ErrorAction SilentlyContinue | Select-Object -Last 20 | Out-String)
        throw "[scenario:$Label] result file never appeared. Tail:`n$tail"
    }

    $json = Get-Content $resultFile -Raw
    $data = $json | ConvertFrom-Json
    $data | Add-Member -NotePropertyName 'label' -NotePropertyValue $Label -Force
    $data | Add-Member -NotePropertyName 'transport' -NotePropertyValue $Transport -Force

    # When invoked via `pwsh -File`, returned PSCustomObjects get pretty-
    # printed into strings across the subprocess boundary. Emit a single
    # sentinel-prefixed JSON line on stdout so the outer harness can
    # reliably extract just the data with a regex match.
    $payload = $data | ConvertTo-Json -Compress
    Write-Output "##SCENARIO-RESULT## $payload"
    return $data
}
finally {
    if ($proxyProc -and -not $proxyProc.HasExited) {
        Stop-Process -Id $proxyProc.Id -Force -ErrorAction SilentlyContinue
    }
}
