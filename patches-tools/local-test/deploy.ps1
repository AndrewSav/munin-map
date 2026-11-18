<#
.SYNOPSIS
  Build Munin Map from this checkout and (re)start the local test server with it.

.DESCRIPTION
  1. The first time, runs update.ps1 to install the server and BepInEx.
  2. Builds with the repo's build.ps1, against the test server's own game assemblies.
  3. Copies only MuninMap.dll into the plugins folder, as on live, so the web app is served
     from inside the DLL.
  4. Recreates the test server, which copies the plugin in as it starts. Then it waits until
     the game server is up and Munin answers on http://localhost:3000.
  Prints how long each step took, and warns if the test server's Valheim version differs
  from live's.

.EXAMPLE
  .\deploy.ps1            # build and restart
  .\deploy.ps1 -NoBuild   # restart with the DLL already deployed
#>
param([switch]$NoBuild)
$ErrorActionPreference = "Stop"
$here = $PSScriptRoot
. (Join-Path $here "common.ps1")
$repo = Resolve-Path (Join-Path $here "..\..")
$compose = Join-Path $here "compose.yml"
$data = Join-Path $here "data"
$managed = Join-Path $data "server\server\valheim_server_Data\Managed"
$total = [Diagnostics.Stopwatch]::StartNew()

Assert-Docker
$world = (Get-LiveEnv).WORLD_NAME
if (-not (Test-Path (Join-Path $data "config\worlds_local\$world"))) { throw "no world yet: run pull-save.ps1 first" }
docker volume inspect munin-test_server *> $null
if ($LASTEXITCODE -ne 0 -or -not (Test-Path (Join-Path $managed "assembly_valheim.dll"))) {
  Step "first run: install the server (update.ps1)" { & (Join-Path $here "update.ps1") }
}

if (-not $NoBuild) {
  Step "build" {
    # build.ps1 copies only some of the game assemblies the project references, so give it all of them
    New-Item -ItemType Directory -Force (Join-Path $repo "libs\valheim") | Out-Null
    Copy-Item (Join-Path $managed "*.dll") (Join-Path $repo "libs\valheim") -Force
    # quiet unless it fails
    $log = try { & (Join-Path $repo "build.ps1") -ValheimManaged $managed -SkipPackage *>&1 | Out-String } catch { $_ | Out-String }
    if ($log -notmatch 'Build succeeded') { Write-Host $log; throw "build failed" }
    if ($log -match '(\d+) Warning\(s\)' -and $Matches[1] -ne '0') { Write-Host $log }
  }
  Step "deploy" {
    $plugins = Join-Path $data "config\bepinex\plugins"
    New-Item -ItemType Directory -Force $plugins | Out-Null
    Copy-Item (Join-Path $repo "src\bin\Release\MuninMap.dll") $plugins -Force
    Write-Host ("   MuninMap.dll {0}" -f (Get-FileHash (Join-Path $plugins "MuninMap.dll") -Algorithm SHA256).Hash.Substring(0, 12))
  }
}

Step "restart" {
  docker compose -f $compose up -d --force-recreate valheim
  if ($LASTEXITCODE -ne 0) { throw "docker compose up failed" }
}
Step "wait for the game server" {
  Wait-Until "Game server connected" 600 { docker logs munin-test 2>&1 | Select-String -Quiet "Game server connected" }
}
Step "wait for Munin" {
  Wait-Until "Munin on :3000" 300 {
    try { (Invoke-WebRequest -Uri "http://127.0.0.1:3000/" -TimeoutSec 5 -UseBasicParsing).StatusCode -eq 200 } catch { $false }
  }
}
Write-Host ("Ready in {0:n0} s. Join 127.0.0.1:2456 (no password); map at http://localhost:3000" -f $total.Elapsed.TotalSeconds)

$local = Get-TestVersions ((docker logs munin-test 2>&1) -join "`n")
$local.Image = Get-BaseImage
Compare-WithLive $local
