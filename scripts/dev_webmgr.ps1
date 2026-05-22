# Runs the WebManager under `dotnet watch` so any .cs / .cshtml / appsettings
# edit triggers a hot-reload or rebuild while the cluster keeps running.
# Frontend (ClientApp) HMR is handled by the React dev server proxied from
# Startup.cs SpaProxy — no extra work needed here.
#
# Pair with scripts/dev_cluster.ps1 in a separate terminal.
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath (Join-Path $root "LPS.Server.WebManager")

# Avoid the https launch profile (cert hassle) and pin the port the smoke
# script + frontend BaseApi default to.
$env:ASPNETCORE_URLS = "http://localhost:7088"
$env:ASPNETCORE_ENVIRONMENT = "Development"
# SpaProxy launches `npm start` (see csproj <SpaProxyLaunchCommand>) and
# reverse-proxies the React dev server at /. Without this the root route
# 404s and you see "WebRootPath was not found: ...\wwwroot".
$env:ASPNETCORE_HOSTINGSTARTUPASSEMBLIES = "Microsoft.AspNetCore.SpaProxy"
$env:DOTNET_WATCH_RESTART_ON_RUDE_EDIT = "true"

Write-Host ">> dotnet watch on LPS.Server.WebManager (http://localhost:7088)" -ForegroundColor Cyan
Write-Host ">> SPA proxy will spawn 'npm start' (first run can take ~1min)" -ForegroundColor Yellow
Write-Host ">> edit any .cs file - it will hot-reload / restart automatically." -ForegroundColor Green
dotnet watch run --no-launch-profile --urls http://localhost:7088
