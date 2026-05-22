#requires -Version 7
<#
.SYNOPSIS
Run the full TCP vs KCP benchmark matrix. Assumes cluster is up.
Saves results JSON to scripts/netem/results.json.
#>
param(
    [int] $Count = 50,
    [int] $TimeoutMs = 5000
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
Push-Location $repoRoot

$results = @()

function Run-Scenario {
    param(
        [Parameter(Mandatory)] [string] $Label,
        [Parameter(Mandatory)] [ValidateSet('tcp','kcp')] [string] $Transport,
        [Parameter(Mandatory)] [int] $Port,
        [string] $ProxyCmd = '',
        [int] $Count = 50,
        [int] $TimeoutMs = 5000
    )
    # Call the scenario script as a sub-process. PSCustomObjects don't
    # survive the subprocess boundary (they get Format-List stringified),
    # so the inner script emits a sentinel line "##SCENARIO-RESULT## <json>".
    # We grep for that line and re-parse.
    $allOut = & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'run_scenario.ps1') `
        -Label $Label -Transport $Transport -Port $Port `
        -ProxyCmd $ProxyCmd -Count $Count -TimeoutMs $TimeoutMs 2>&1

    foreach ($line in $allOut) {
        $s = "$line"
        if ($s.StartsWith('##SCENARIO-RESULT##')) {
            $json = $s.Substring('##SCENARIO-RESULT##'.Length).Trim()
            return $json | ConvertFrom-Json
        }
    }
    return $null
}

function Add-Result($r) {
    if ($null -eq $r -or -not ($r.PSObject.Properties['p99'])) {
        Write-Host "[bench_all] scenario returned no data row" -ForegroundColor Red
        return
    }
    $script:results += $r
    $line = "{0,-30} count={1,3} ok={2,3} fail={3,3} mean={4,7:F1} p50={5,7:F1} p90={6,7:F1} p99={7,7:F1} max={8,7:F1}" `
        -f $r.label, $r.count, $r.ok, $r.fail, $r.mean, $r.p50, $r.p90, $r.p99, $r.max
    Write-Host $line -ForegroundColor Green
}

try {
    # === Baselines: localhost loopback, no impairment ==================
    Write-Host "`n=== BASELINE (loopback, no impairment) ===" -ForegroundColor Cyan
    Add-Result (Run-Scenario -Label "baseline-tcp" -Transport tcp -Port 11001 -Count $Count -TimeoutMs $TimeoutMs)
    Add-Result (Run-Scenario -Label "baseline-kcp" -Transport kcp -Port 11002 -Count $Count -TimeoutMs $TimeoutMs)

    # === Latency only: 50ms one-way (both directions, so RTT +100ms) ===
    Write-Host "`n=== LATENCY 50ms one-way (RTT +100ms) ===" -ForegroundColor Cyan
    Add-Result (Run-Scenario -Label "lat50-tcp" -Transport tcp -Port 21001 -Count $Count -TimeoutMs $TimeoutMs `
        -ProxyCmd "scripts/netem/tcp_proxy.py --listen 127.0.0.1:21001 --upstream 127.0.0.1:11001 --latency-ms 50 --jitter-ms 5")
    Add-Result (Run-Scenario -Label "lat50-kcp" -Transport kcp -Port 21002 -Count $Count -TimeoutMs $TimeoutMs `
        -ProxyCmd "scripts/netem/udp_proxy.py --listen 127.0.0.1:21002 --upstream 127.0.0.1:11002 --latency-ms 50 --jitter-ms 5")

    # === KCP-only: real packet loss (TCP cannot be tested fairly here) =
    Write-Host "`n=== KCP ONLY: drop scenarios (TCP loss requires clumsy) ===" -ForegroundColor Cyan
    Add-Result (Run-Scenario -Label "drop5-lat50-kcp" -Transport kcp -Port 21002 -Count $Count -TimeoutMs $TimeoutMs `
        -ProxyCmd "scripts/netem/udp_proxy.py --listen 127.0.0.1:21002 --upstream 127.0.0.1:11002 --drop 0.05 --latency-ms 50 --jitter-ms 5")
    Add-Result (Run-Scenario -Label "drop10-lat100-kcp" -Transport kcp -Port 21002 -Count $Count -TimeoutMs $TimeoutMs `
        -ProxyCmd "scripts/netem/udp_proxy.py --listen 127.0.0.1:21002 --upstream 127.0.0.1:11002 --drop 0.10 --latency-ms 100 --jitter-ms 10")
}
finally {
    Pop-Location
}

$outPath = Join-Path $PSScriptRoot 'results.json'
$results | ConvertTo-Json -Depth 5 | Set-Content $outPath
Write-Host "`nResults saved to $outPath" -ForegroundColor Yellow
Write-Host "`n=== SUMMARY ===" -ForegroundColor Cyan
$results | Format-Table label, ok, fail, mean, p50, p90, p99, max -AutoSize
