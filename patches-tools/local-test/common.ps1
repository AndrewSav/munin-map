# Helpers shared by the local test scripts. Dot-source it: . (Join-Path $PSScriptRoot "common.ps1")

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
    Start-Sleep -Milliseconds 500
  }
}

function Assert-Docker {
  docker info --format '{{.ServerVersion}}' *> $null
  if ($LASTEXITCODE -ne 0) { throw "Docker is not running: start Docker Desktop first" }
}

# settings.psd1: where the live server is (see settings.example.psd1)
function Get-Settings {
  $path = Join-Path $PSScriptRoot "settings.psd1"
  if (-not (Test-Path $path)) { throw "no settings.psd1: copy settings.example.psd1 to settings.psd1 next to it and fill it in" }
  $s = Import-PowerShellDataFile $path
  foreach ($k in "SshHost", "LiveDir") { if (-not $s[$k]) { throw "settings.psd1 has no $k" } }
  $s
}

# .env: the live settings pull-save.ps1 copied (world name, world modifiers, user ID)
function Get-LiveEnv {
  $path = Join-Path $PSScriptRoot ".env"
  if (-not (Test-Path $path)) { throw "no .env yet: run pull-save.ps1 first" }
  $e = @{}
  foreach ($line in Get-Content $path) {
    if ($line -match '^\s*([A-Z_]+)=(.*)$') { $e[$Matches[1]] = $Matches[2].Trim().Trim('"') }
  }
  $e
}

# Windows OpenSSH uses the ssh-agent's keys; the ssh that comes with Git Bash does not
function Get-SshDir {
  $dir = @("$env:ProgramFiles\OpenSSH", "$env:SystemRoot\System32\OpenSSH") | Where-Object { Test-Path "$_\ssh.exe" } | Select-Object -First 1
  if (-not $dir) { throw "Windows OpenSSH not found (looked in $env:ProgramFiles\OpenSSH and System32\OpenSSH)" }
  $dir
}

function Invoke-Live([string]$command) {
  $s = Get-Settings
  & (Join-Path (Get-SshDir) "ssh.exe") -o BatchMode=yes -o ConnectTimeout=5 $s.SshHost $command
}

# Valheim and BepInEx versions from BepInEx's log output
function Get-TestVersions([string]$log) {
  $v = [ordered]@{ Valheim = "?"; BepInEx = "?"; Image = "?" }
  if ($log -match 'Valheim Version: (\S+)') { $v.Valheim = $Matches[1] }
  if ($log -match 'BepInExPack Valheim version (\S+)') { $v.BepInEx = $Matches[1] }
  $v
}

# the base image named in compose.yml
function Get-BaseImage {
  $line = Select-String -Path (Join-Path $PSScriptRoot "compose.yml") -Pattern '^x-base-image: &base-image (\S+)' | Select-Object -First 1
  if ($line) { $line.Matches[0].Groups[1].Value } else { "?" }
}

# The live server's versions and image, read over ssh. $null (with a warning) when it can't be reached.
function Get-LiveVersions {
  $dir = (Get-Settings).LiveDir
  $out = Invoke-Live "grep -m2 -E 'Valheim Version|BepInExPack Valheim version' ~/$dir/server/bepinex/BepInEx/LogOutput.log; grep -m1 -E '^\s*image:' ~/$dir/docker-compose.yml" 2>$null
  if ($LASTEXITCODE -ne 0 -or -not $out) { Write-Warning "could not read the live server's versions"; return $null }
  $text = $out -join "`n"
  $v = Get-TestVersions $text
  if ($text -match 'image:\s*(\S+)') { $v.Image = $Matches[1] }
  $v
}

# Print the test server's versions next to live's, and warn about any difference.
function Compare-WithLive($local) {
  $live = Get-LiveVersions
  foreach ($k in $local.Keys) {
    $l = if ($live) { $live[$k] } else { "?" }
    Write-Host ("   {0,-8} test {1,-62} live {2}" -f $k, $local[$k], $l)
    if ($live -and $l -ne "?" -and $local[$k] -ne $l) {
      $fix = if ($k -eq "Image") { "change x-base-image in compose.yml to match, then run update.ps1" } else { "run update.ps1" }
      Write-Warning "$k differs from live: test $($local[$k]), live $l. $fix"
    }
  }
}
