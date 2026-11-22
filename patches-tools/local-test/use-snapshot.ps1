<#
.SYNOPSIS
  Switch the local test server to a saved world snapshot, with Munin's map tiles to match.

.DESCRIPTION
  A snapshot is a folder in data/snapshots/:
    world/   the world's save files, as in data/config/worlds_local/<world>
    tiles/   (optional) Munin's tiles drawn for that world
  This script:
  1. Stops the local test server if it is running.
  2. Replaces the test world with the snapshot's world/.
  3. Puts the snapshot's tiles/ in place of Munin's tiles, owned by the server's user. Munin's first
     sweep after a start only records a baseline, so tiles drawn for another world would never be
     redrawn. A snapshot without tiles/ gets the same clearing as pull-save.ps1: zoom 5-7 and the
     vegetation and ground overlays go, and Munin redraws them (about 20 minutes for a fully
     generated world, though tiles someone is looking at come first).
  Munin's other data (stats, claims and so on) is left as it is. Go back to a copy of live with
  pull-save.ps1, which leaves the snapshots alone. Start the server with deploy.ps1 -NoBuild.

.EXAMPLE
  .\use-snapshot.ps1                                        # list the snapshots
  .\use-snapshot.ps1 MyWorld-generated-2026-11-22
#>
param([string]$Name)
$ErrorActionPreference = "Stop"
$here = $PSScriptRoot
. (Join-Path $here "common.ps1")
$snapshots = Join-Path $here "data\snapshots"

if (-not $Name) {
  Write-Host "Snapshots in $snapshots :"
  if (Test-Path $snapshots) { Get-ChildItem $snapshots -Directory | ForEach-Object { "  " + $_.Name + $(if (Test-Path (Join-Path $_.FullName "tiles")) { "  (with tiles)" } else { "" }) } }
  return
}
$snap = Join-Path $snapshots $Name
if (-not (Test-Path (Join-Path $snap "world"))) { throw "no snapshot world at $snap\world" }
Assert-Docker
$world = (Get-LiveEnv).WORLD_NAME
if (-not $world) { throw "no WORLD_NAME in .env: run pull-save.ps1 once first" }
$total = [Diagnostics.Stopwatch]::StartNew()

# --- stop the local server so it can't save over the swap
$compose = Join-Path $here "compose.yml"
if (docker ps -q --filter "name=^munin-test$" 2>$null) {
  Write-Host "Stopping the local test server"
  docker compose -f $compose stop
  if ($LASTEXITCODE -ne 0) { throw "could not stop munin-test" }
}

# --- the world
$target = Join-Path $here "data\config\worlds_local\$world"
if (Test-Path $target) { Remove-Item $target -Recurse -Force }
Copy-Item (Join-Path $snap "world") $target -Recurse
Write-Host "World $world is now the snapshot $Name"

# --- Munin's tiles, in a one-off container as root so they can be given to the server's user
$w = "/home/valheim/munin-data/$world"
if (Test-Path (Join-Path $snap "tiles")) {
  $sh = "set -e; mkdir -p '$w'; rm -rf '$w/tiles'; cp -r /snap/tiles '$w/tiles'; chown -R valheim:valheim '$w/tiles'"
  Write-Host "Copying the snapshot's tiles (a minute or two)"
} else {
  $sh = "set -e; mkdir -p '$w'; rm -rf '$w/tiles/map/5' '$w/tiles/map/6' '$w/tiles/map/7' '$w/tiles/veg' '$w/tiles/terra'"
  Write-Host "The snapshot has no tiles: Munin redraws zoom 5-7"
}
docker compose -f $compose run --rm --no-deps -u root -v "$((Resolve-Path $snap).Path):/snap:ro" --entrypoint sh valheim -c $sh
if ($LASTEXITCODE -ne 0) { throw "could not update Munin's tiles in the munin-data volume" }

Write-Host ("Done in {0:n1} s. Start the server with deploy.ps1 -NoBuild." -f $total.Elapsed.TotalSeconds)
