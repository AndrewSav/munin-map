<#
.SYNOPSIS
  Copy the newest consistency-checked world backup from the live server into the local test server.

.DESCRIPTION
  Read-only on the live server (settings.psd1 says where it is). It:
  1. Reads the live docker-compose.yml for the world name, world modifiers (SERVER_ARGS) and
     user ID (PUID/PGID), and writes them to .env for compose.yml. Git ignores .env.
  2. Takes the newest of the image's daily AUTOBACKUP folders, which are known to hold a
     committed save, never the live world folder (a copy taken mid-save can be broken), and
     checks that its _main.N.ok marker matches its _main.N.db2.
  3. Copies the live Munin settings, with discord_webhook, telegram_bot_token and telegram_chat_id
     blanked so the test server never posts to the real Discord or Telegram, hidden_players
     blanked so test characters show on the map, and
     data_path pointed at the test server's Docker volume.
  4. Copies Munin's own data for the world (the top-level files of its world folder: stats,
     activity, trails, events, bases, claims, build history, caches), so builders, players and
     trails are named as on live. Tiles, models and icons are left out: hundreds of MB, and the
     test server makes its own. -WorldOnly skips this and keeps the test server's Munin data.
  5. Stops the local test server if it is running, and swaps the copies in. Munin's map tiles at
     zoom 5-7 and its overlays are deleted, so the map is redrawn from this world.

  Start the server again with deploy.ps1.
#>
param([switch]$WorldOnly)
$ErrorActionPreference = "Stop"
$here = $PSScriptRoot
. (Join-Path $here "common.ps1")
$settings = Get-Settings
$dir = $settings.LiveDir
$scp = Join-Path (Get-SshDir) "scp.exe"
$total = [Diagnostics.Stopwatch]::StartNew()

# --- live settings the test server copies
$liveCompose = Invoke-Live "cat ~/$dir/docker-compose.yml"
if ($LASTEXITCODE -ne 0 -or -not $liveCompose) { throw "could not read ~/$dir/docker-compose.yml on $($settings.SshHost)" }
function Get-ComposeValue([string]$name, [string]$default) {
  $line = $liveCompose | Where-Object { $_ -match "^\s+${name}:\s*(.*)$" } | Select-Object -First 1
  if (-not $line) { return $default }
  $null = $line -match "^\s+${name}:\s*(.*)$"
  $v = $Matches[1]
  if ($v -match '^"([^"]*)"') { return $Matches[1] }          # quoted, maybe with a comment after
  ($v -replace '\s+#.*$', '').Trim()
}
$live = [ordered]@{
  WORLD_NAME  = Get-ComposeValue "WORLD_NAME" ""
  SERVER_ARGS = Get-ComposeValue "SERVER_ARGS" ""
  PUID        = Get-ComposeValue "PUID" "1000"
  PGID        = Get-ComposeValue "PGID" "1000"
}
if (-not $live.WORLD_NAME) { throw "no WORLD_NAME in the live docker-compose.yml" }
$world = $live.WORLD_NAME
Set-Content -Path (Join-Path $here ".env") -Value ($live.Keys | ForEach-Object { "$_=`"$($live[$_])`"" })

# --- newest backup, and check it is a committed save
$listing = Invoke-Live "cd ~/$dir/config/backups && d=`$(ls -1d AUTOBACKUP-$world-* 2>/dev/null | sort | tail -1) && [ -n `"`$d`" ] && echo `$d && ls -1 `"`$d`""
if ($LASTEXITCODE -ne 0 -or -not $listing) { throw "no AUTOBACKUP-$world-* folder found under ~/$dir/config/backups" }
$backup = $listing[0]; $files = @($listing | Select-Object -Skip 1)
$db = @($files | Where-Object { $_ -match '^_main\.(\d+)\.db2$' }) | ForEach-Object { [int]($_ -replace '^_main\.(\d+)\.db2$', '$1') }
$ok = @($files | Where-Object { $_ -match '^_main\.(\d+)\.ok$' }) | ForEach-Object { [int]($_ -replace '^_main\.(\d+)\.ok$', '$1') }
if (-not $db -or -not $ok -or (($db | Measure-Object -Maximum).Maximum -ne ($ok | Measure-Object -Maximum).Maximum)) {
  throw "$backup is not a committed save (db2: $($db -join ','), ok: $($ok -join ','))"
}
Write-Host "Backup: $backup ($($files.Count) files, save $(($ok | Measure-Object -Maximum).Maximum))"

# --- copy it down to a staging folder
$data = Join-Path $here "data"
$incoming = Join-Path $data "incoming"
if (Test-Path $incoming) { Remove-Item $incoming -Recurse -Force }
New-Item -ItemType Directory -Force $incoming | Out-Null
& $scp -q -p -r -o BatchMode=yes "$($settings.SshHost):$dir/config/backups/$backup" $incoming
if ($LASTEXITCODE -ne 0) { throw "scp of $backup failed" }
$staged = Join-Path $incoming $backup
$got = @(Get-ChildItem $staged -File).Count
if ($got -ne $files.Count) { throw "copied $got files, expected $($files.Count)" }
& $scp -q -o BatchMode=yes "$($settings.SshHost):$dir/config/bepinex/com.cocopuff2u.muninmap.cfg" (Join-Path $incoming "muninmap.cfg")
if ($LASTEXITCODE -ne 0) { throw "scp of the Munin settings failed" }

# --- Munin's data for the world: the top-level files of its folder
$muninIn = Join-Path $data "config\munin-incoming"   # under ./data/config, which the container sees as /config
if (Test-Path $muninIn) { Remove-Item $muninIn -Recurse -Force }
if (-not $WorldOnly) {
  $dataPath = (Get-Content (Join-Path $incoming "muninmap.cfg") | Where-Object { $_ -match '^\s*data_path\s*=' } | Select-Object -First 1) -replace '^\s*data_path\s*=\s*', ''
  # empty: BepInEx/config/MuninMap, which the live image keeps in ./config/bepinex
  if ($dataPath.Trim()) { throw "live data_path is '$($dataPath.Trim())': pull-save.ps1 only knows where the default one is on the live host. Use -WorldOnly, or teach it this one." }
  $liveMunin = "$dir/config/bepinex/MuninMap/$world"
  $names = @(Invoke-Live "cd ~/$liveMunin && find . -maxdepth 1 -type f -printf '%f
'")
  if ($LASTEXITCODE -ne 0 -or -not $names) { throw "no Munin data found in ~/$liveMunin on $($settings.SshHost)" }
  New-Item -ItemType Directory -Force $muninIn | Out-Null
  & $scp -q -p -o BatchMode=yes @($names | ForEach-Object { "$($settings.SshHost):$liveMunin/$_" }) $muninIn
  if ($LASTEXITCODE -ne 0) { throw "scp of the Munin data failed" }
  $got = @(Get-ChildItem $muninIn -File).Count
  if ($got -ne $names.Count) { throw "copied $got Munin files, expected $($names.Count)" }
  # Munin writes some of these in place, so a copy can catch one half written
  foreach ($f in Get-ChildItem $muninIn -File -Filter *.json) {
    try { $null = Get-Content $f.FullName -Raw | ConvertFrom-Json } catch { throw "$($f.Name) from live doesn't parse (caught mid-write?): run pull-save.ps1 again" }
  }
  Write-Host "Munin data: $got files from ~/$liveMunin"
}
Write-Host ("Copied in {0:n1} s" -f $total.Elapsed.TotalSeconds)

# --- stop the local server so it can't save over the swap
$compose = Join-Path $here "compose.yml"
if (docker ps -q --filter "name=^munin-test$" 2>$null) {
  Write-Host "Stopping the local test server"
  docker compose -f $compose stop
  if ($LASTEXITCODE -ne 0) { throw "could not stop munin-test" }
}

# --- swap the world in
$worlds = Join-Path $data "config\worlds_local"
New-Item -ItemType Directory -Force $worlds | Out-Null
$target = Join-Path $worlds $world
if (Test-Path $target) { Remove-Item $target -Recurse -Force }
Move-Item $staged $target

# --- Munin settings from live, never posting to Discord, and hiding nobody (nothing to hide on a
# local server, and test characters are often the ones live hides)
$bepinex = Join-Path $data "config\bepinex"
New-Item -ItemType Directory -Force $bepinex | Out-Null
$cfg = (Get-Content (Join-Path $incoming "muninmap.cfg")) -replace '^(\s*discord_webhook\s*=).*$', '$1 ' `
  -replace '^(\s*telegram_bot_token\s*=).*$', '$1 ' `
  -replace '^(\s*telegram_chat_id\s*=).*$', '$1 ' `
  -replace '^(\s*hidden_players\s*=).*$', '$1 ' `
  -replace '^(\s*data_path\s*=).*$', '$1 /home/valheim/munin-data'   # the Docker volume in compose.yml
Set-Content -Path (Join-Path $bepinex "com.cocopuff2u.muninmap.cfg") -Value $cfg
Remove-Item $incoming -Recurse -Force

# --- the munin-data volume, in a one-off container as root (so files can be given to the
# container's valheim user, who runs the server):
# - Munin's map tiles at zoom 5-7 and the vegetation and ground overlays go, every time: they show
#   the world they were drawn from (a generated snapshot, see use-snapshot.ps1), and Munin's first
#   sweep after a start only records a baseline, so it would never redraw them. Zoom 0-4 carry no
#   vegetation or changed ground, and the worlds share one seed, so they stay.
# - unless -WorldOnly, the test server's own top-level files go and live's come in; other
#   subfolders stay.
$w = "/home/valheim/munin-data/$world"
$sh = "set -e; mkdir -p '$w'; rm -rf '$w/tiles/map/5' '$w/tiles/map/6' '$w/tiles/map/7' '$w/tiles/veg' '$w/tiles/terra'"
if (-not $WorldOnly) {
  $sh += "; find '$w' -maxdepth 1 -type f -delete; cp /config/munin-incoming/* '$w/'; " +
         "chown valheim:valheim '$w'; find '$w' -maxdepth 1 -type f -exec chown valheim:valheim {} + -exec chmod 644 {} +"
}
docker compose -f $compose run --rm --no-deps -u root --entrypoint sh valheim -c $sh
if ($LASTEXITCODE -ne 0) { throw "could not update Munin's data in the munin-data volume" }
if (-not $WorldOnly) { Remove-Item $muninIn -Recurse -Force }

Write-Host ("World $world is now $backup. Done in {0:n1} s. Start the server with deploy.ps1." -f $total.Elapsed.TotalSeconds)
