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

# Windows OpenSSH uses the ssh-agent's keys; the ssh that comes with Git Bash does not
function Get-SshDir {
  $dir = @("$env:ProgramFiles\OpenSSH", "$env:SystemRoot\System32\OpenSSH") | Where-Object { Test-Path "$_\ssh.exe" } | Select-Object -First 1
  if (-not $dir) { throw "Windows OpenSSH not found (looked in $env:ProgramFiles\OpenSSH and System32\OpenSSH)" }
  $dir
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
function Get-LiveVersions([string]$SshHost = "", [string]$RemoteDir = "") {
  $ssh = Join-Path (Get-SshDir) "ssh.exe"
  $out = & $ssh -o BatchMode=yes -o ConnectTimeout=5 $SshHost "grep -m2 -E 'Valheim Version|BepInExPack Valheim version' ~/$RemoteDir/server/bepinex/BepInEx/LogOutput.log; grep -m1 -E '^\s*image:' ~/$RemoteDir/docker-compose.yml" 2>$null
  if ($LASTEXITCODE -ne 0 -or -not $out) { Write-Warning "could not read the live server's versions from $SshHost"; return $null }
  $text = $out -join "`n"
  $v = Get-TestVersions $text
  if ($text -match 'image:\s*(\S+)') { $v.Image = $Matches[1] }
  $v
}

# Print the test server's versions next to live's, and warn about any difference.
function Compare-WithLive($local, [string]$SshHost = "", [string]$RemoteDir = "") {
  $live = Get-LiveVersions $SshHost $RemoteDir
  foreach ($k in $local.Keys) {
    $l = if ($live) { $live[$k] } else { "?" }
    Write-Host ("   {0,-8} test {1,-62} live {2}" -f $k, $local[$k], $l)
    if ($live -and $l -ne "?" -and $local[$k] -ne $l) {
      $fix = if ($k -eq "Image") { "change x-base-image in compose.yml to match, then run update.ps1" } else { "run update.ps1" }
      Write-Warning "$k differs from live: test $($local[$k]), live $l. $fix"
    }
  }
}
