<#
.SYNOPSIS
  Update the test server's Valheim and BepInEx, and check them against the live server.

.DESCRIPTION
  The fast test server never updates itself. This script:
  1. Rebuilds the fast image, pulling its base image again.
  2. Runs the live image once with its own startup (compose service "update"). steamcmd
     updates the server in data/server, and BepInExPack_Valheim is checked and merged in.
     Once the merged server has started, it is stopped.
  3. Copies the merged install into the fast server's Docker volume.
  4. Prints the test server's Valheim and BepInEx versions and base image next to the live
     server's, read over ssh.

  Run it when live updates, or whenever deploy.ps1 warns that the versions differ. deploy.ps1
  runs it on its own the first time.
#>
$ErrorActionPreference = "Stop"
$here = $PSScriptRoot
. (Join-Path $here "common.ps1")
$compose = Join-Path $here "compose.yml"
$total = [Diagnostics.Stopwatch]::StartNew()
Assert-Docker

Step "stop the test server" { docker compose -f $compose stop valheim 2>$null }

Step "build the fast image" {
  docker compose -f $compose build --pull valheim
  if ($LASTEXITCODE -ne 0) { throw "image build failed" }
}

$since = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
Step "update with the live image's own startup" {
  docker compose -f $compose --profile update up -d --force-recreate update
  if ($LASTEXITCODE -ne 0) { throw "docker compose up update failed" }
  Wait-Until "the updated server to load BepInEx" 1800 {
    docker logs --since $since munin-test-update 2>&1 | Select-String -Quiet "Chainloader startup complete"
  }
  docker compose -f $compose --profile update rm -f -s update | Out-Null
}

Step "copy the merged install into the server volume" {
  docker compose -f $compose create valheim 2>$null | Out-Null   # lets compose create (and label) the volumes
  # as root, so the copy can be handed to the valheim user (the image gives it live's user ID)
  docker run --rm --user 0 --entrypoint sh `
    -v "$(Join-Path $here 'data\server'):/src:ro" -v munin-test_server:/dst munin-test-fast `
    -c 'rm -rf /dst/bepinex && cp -a /src/bepinex /dst/ && chown -R valheim:valheim /dst'
  if ($LASTEXITCODE -ne 0) { throw "copying the merged install into the volume failed" }
}

$local = Get-TestVersions (Get-Content (Join-Path $here "data\server\bepinex\BepInEx\LogOutput.log") -Raw)
$local.Image = Get-BaseImage
Compare-WithLive $local
Write-Host ("Updated in {0:n0} s." -f $total.Elapsed.TotalSeconds)
