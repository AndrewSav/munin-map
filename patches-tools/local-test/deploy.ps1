<#
.SYNOPSIS
  Build Munin Map from this checkout and (re)start the local test server with it.

.DESCRIPTION
  1. On the first run, starts the server once so steamcmd installs it. The build compiles
     against that install's own game assemblies.
  2. Builds with the repo's build.ps1.
  3. Copies only MuninMap.dll into the plugins folder, as on live, so the web app is served
     from inside the DLL.
  4. Recreates the container, which copies the plugin in, and waits until the game server is
     up and Munin answers on http://localhost:3000.
  Prints how long each step took.

.EXAMPLE
  .\deploy.ps1            # build and restart
  .\deploy.ps1 -NoBuild   # restart with the DLL already deployed
#>
param([switch]$NoBuild)
$ErrorActionPreference = "Stop"
$here = $PSScriptRoot
$repo = Resolve-Path (Join-Path $here "..\..")
$compose = Join-Path $here "compose.yml"
$data = Join-Path $here "data"
$managed = Join-Path $data "server\server\valheim_server_Data\Managed"
$total = [Diagnostics.Stopwatch]::StartNew()
function Step($name, [scriptblock]$body) {
  $sw = [Diagnostics.Stopwatch]::StartNew()
  Write-Host "== $name"
  & $body
  Write-Host ("   {0}: {1:n0} s" -f $name, $sw.Elapsed.TotalSeconds)
}
function Wait-Until($what, [int]$seconds, [scriptblock]$test) {
  $deadline = (Get-Date).AddSeconds($seconds)
  while (-not (& $test)) {
    if ((Get-Date) -gt $deadline) { throw "timed out after $seconds s waiting for $what" }
    Start-Sleep -Seconds 2
  }
}

docker info --format '{{.ServerVersion}}' *> $null
if ($LASTEXITCODE -ne 0) { throw "Docker is not running: start Docker Desktop first" }
if (-not (Test-Path (Join-Path $data "config\worlds_local"))) { throw "no world yet: run pull-save.ps1 first" }

if (-not $NoBuild) {
  if (-not (Test-Path (Join-Path $managed "assembly_valheim.dll"))) {
    Step "first start: install the dedicated server" {
      docker compose -f $compose up -d
      if ($LASTEXITCODE -ne 0) { throw "docker compose up failed" }
      Wait-Until "the server install" 1800 { Test-Path (Join-Path $managed "assembly_valheim.dll") }
    }
  }
  Step "build" {
    # build.ps1 copies only some of the game assemblies the project references, so give it all of them
    New-Item -ItemType Directory -Force (Join-Path $repo "libs\valheim") | Out-Null
    Copy-Item (Join-Path $managed "*.dll") (Join-Path $repo "libs\valheim") -Force
    & (Join-Path $repo "build.ps1") -ValheimManaged $managed -SkipPackage
  }
  Step "deploy" {
    $plugins = Join-Path $data "config\bepinex\plugins"
    New-Item -ItemType Directory -Force $plugins | Out-Null
    Copy-Item (Join-Path $repo "src\bin\Release\MuninMap.dll") $plugins -Force
    Write-Host ("   MuninMap.dll {0}" -f (Get-FileHash (Join-Path $plugins "MuninMap.dll") -Algorithm SHA256).Hash.Substring(0, 12))
  }
}

$since = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
Step "restart" {
  docker compose -f $compose up -d --force-recreate
  if ($LASTEXITCODE -ne 0) { throw "docker compose up failed" }
}
Step "wait for the game server" {
  Wait-Until "Game server connected" 900 { docker logs --since $since munin-test 2>&1 | Select-String -Quiet "Game server connected" }
}
Step "wait for Munin" {
  Wait-Until "Munin on :3000" 600 {
    try { (Invoke-WebRequest -Uri "http://localhost:3000/" -TimeoutSec 3 -UseBasicParsing).StatusCode -eq 200 } catch { $false }
  }
}
Write-Host ("Ready in {0:n0} s. Join 127.0.0.1:2456 (no password); map at http://localhost:3000" -f $total.Elapsed.TotalSeconds)
