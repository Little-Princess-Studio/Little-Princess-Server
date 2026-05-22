# Standalone CRA dev server with HMR. Talks to the WebManager backend
# running on http://localhost:7088 (started via scripts/dev_webmgr.ps1).
#
# Run order:
#   terminal 1: cluster (managed by Sisyphus, no script needed)
#   terminal 2: .\scripts\dev_webmgr.ps1    -> http://localhost:7088 (static + API)
#   terminal 3: .\scripts\dev_frontend.ps1  -> http://localhost:3000 (HMR)
#
# Use http://localhost:3000 while editing UI - it hot-reloads on .tsx save.
# Use http://localhost:7088 to verify the production-style bundle.
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath (Join-Path $root "LPS.Server.WebManager\ClientApp")

# Skip the prestart https/dotnet cert dance - we hit the backend over http.
$env:BROWSER = "none"
$env:HTTPS = "false"
$env:PORT = "3000"
$env:REACT_APP_API_BASE = "http://localhost:7088/api/web-manager"
# CRA's source-map-loader chokes on recharts v2.13's stale sourcemap references
# to es-toolkit/compat (recharts ships ES6 with maps that point at deps it no
# longer pulls in). Disabling sourcemaps avoids the noise; we keep TS errors via tsc.
$env:GENERATE_SOURCEMAP = "false"

if (-not (Test-Path node_modules)) {
    Write-Host ">> first run: installing npm deps (2-3 min)..." -ForegroundColor Yellow
    npm install --no-audit --no-fund
}

Write-Host ">> CRA dev server on http://localhost:3000 (API -> $env:REACT_APP_API_BASE)" -ForegroundColor Cyan
node scripts/start.js
