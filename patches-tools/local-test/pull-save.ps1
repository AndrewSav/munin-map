<#
.SYNOPSIS
  Copy the newest consistency-checked world backup from the live server into the local test server.

.DESCRIPTION
  Read-only on the live server. It takes the newest of the image's daily AUTOBACKUP folders, which
  are known to hold a committed save, never the live world folder (a copy taken mid-save can
  be broken). It checks that the backup's _main.N.ok marker matches its _main.N.db2, stops
  the local test server if it is running, and swaps the copy in as the local world.

  It also copies the live Munin settings, with discord_webhook blanked so the test server
  never posts to the real Discord.

  Start the server again with deploy.ps1.
#>
param(
  [string]$SshHost = "",
  [string]$RemoteConfig = "",   # relative to the ssh user's home
  [string]$World = ""
)
$ErrorActionPreference = "Stop"
$here = $PSScriptRoot
$total = [Diagnostics.Stopwatch]::StartNew()

# Windows OpenSSH uses the ssh-agent's keys; the ssh that comes with Git Bash does not
$sshDir = @("$env:ProgramFiles\OpenSSH", "$env:SystemRoot\System32\OpenSSH") | Where-Object { Test-Path "$_\ssh.exe" } | Select-Object -First 1
if (-not $sshDir) { throw "Windows OpenSSH not found (looked in $env:ProgramFiles\OpenSSH and System32\OpenSSH)" }
$ssh = "$sshDir\ssh.exe"; $scp = "$sshDir\scp.exe"

# --- newest backup, and check it is a committed save
$listing = & $ssh -o BatchMode=yes $SshHost "cd ~/$RemoteConfig/backups && d=`$(ls -1d AUTOBACKUP-$World-* 2>/dev/null | sort | tail -1) && [ -n `"`$d`" ] && echo `$d && ls -1 `"`$d`""
if ($LASTEXITCODE -ne 0 -or -not $listing) { throw "no AUTOBACKUP-$World-* folder found under ~/$RemoteConfig/backups on $SshHost" }
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
& $scp -q -p -r -o BatchMode=yes "${SshHost}:$RemoteConfig/backups/$backup" $incoming
if ($LASTEXITCODE -ne 0) { throw "scp of $backup failed" }
$staged = Join-Path $incoming $backup
$got = @(Get-ChildItem $staged -File).Count
if ($got -ne $files.Count) { throw "copied $got files, expected $($files.Count)" }
& $scp -q -o BatchMode=yes "${SshHost}:$RemoteConfig/bepinex/com.cocopuff2u.muninmap.cfg" (Join-Path $incoming "muninmap.cfg")
if ($LASTEXITCODE -ne 0) { throw "scp of the Munin settings failed" }
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
$target = Join-Path $worlds $World
if (Test-Path $target) { Remove-Item $target -Recurse -Force }
Move-Item $staged $target

# --- Munin settings from live, never posting to Discord
$bepinex = Join-Path $data "config\bepinex"
New-Item -ItemType Directory -Force $bepinex | Out-Null
$cfg = (Get-Content (Join-Path $incoming "muninmap.cfg")) -replace '^(\s*discord_webhook\s*=).*$', '$1 ' `
  -replace '^(\s*data_path\s*=).*$', '$1 /home/valheim/munin-data'   # the Docker volume in compose.yml
Set-Content -Path (Join-Path $bepinex "com.cocopuff2u.muninmap.cfg") -Value $cfg
Remove-Item $incoming -Recurse -Force

Write-Host ("World $World is now $backup. Done in {0:n1} s. Start the server with deploy.ps1." -f $total.Elapsed.TotalSeconds)
