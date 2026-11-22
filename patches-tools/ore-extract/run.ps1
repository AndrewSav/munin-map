<#
.SYNOPSIS
  TASK-4.1 spike: run the OreExtract generator in its own container on a fresh copy of the base world.

.DESCRIPTION
  Uses the local test server's image and installed server (Docker volume munin-test_server), but
  its own /config (a copy of data/zonegen/base: worlds_local/<world> and bepinex/BepInEx.cfg) and
  its own plugins folder holding only OreExtract.dll, so it doesn't touch the test server. No ports
  are published. Results land in data/zonegen/runs/<tag>/config/ore-extract-out.

.EXAMPLE
  .\run.ps1 plain -Settings @{ OREEXTRACT_PLAIN = 1; OREEXTRACT_LIMIT = 2000 } -Wait
  .\run.ps1 low -Settings @{ OREEXTRACT_LAND = 1 } -LowPriority          # starts and returns
#>
param(
  [Parameter(Mandatory)][string]$Tag,
  [hashtable]$Settings = @{},
  [string]$Mode = "generate",
  [switch]$Wait,
  [switch]$LowPriority,
  [string]$Cpuset = "",   # e.g. 0-3: pin the container to these cores
  [string]$Base = ""   # a folder with worlds_local/<world> and bepinex/ (default: data/zonegen/base)
)
$ErrorActionPreference = "Stop"
$here = $PSScriptRoot
$lt = Join-Path $here "..\local-test"
. (Join-Path $lt "common.ps1")
Assert-Docker
$envFile = Get-LiveEnv
$world = $envFile.WORLD_NAME
$base = if ($Base) { $Base } else { Join-Path $lt "data\zonegen\base" }
if (-not (Test-Path (Join-Path $base "worlds_local\$world"))) { throw "no base world in $base (see TASK-4.1 notes)" }
$dll = Join-Path $here "..\..\ore-extract\plugin\bin\Release\OreExtract.dll"
if (-not (Test-Path $dll)) { throw "build OreExtract first: dotnet build -c Release in ore-extract\plugin" }

$run = Join-Path $lt "data\zonegen\runs\$Tag"
if (Test-Path $run) { Remove-Item $run -Recurse -Force }
New-Item -ItemType Directory -Force (Join-Path $run "config"), (Join-Path $run "plugins") | Out-Null
Copy-Item (Join-Path $base "*") (Join-Path $run "config") -Recurse
Copy-Item $dll (Join-Path $run "plugins")
New-Item -ItemType Directory -Force (Join-Path $run "config\bepinex\plugins") | Out-Null
Copy-Item $dll (Join-Path $run "config\bepinex\plugins")   # fast-start.sh copies from here into the mounted plugins folder
$cfg = (Resolve-Path (Join-Path $run "config")).Path
$plg = (Resolve-Path (Join-Path $run "plugins")).Path

$name = "zonegen-$Tag"
docker rm -f $name *> $null
$dargs = @("run", "-d", "--name", $name,
  "-e", "WORLD_NAME=$world", "-e", "SERVER_ARGS=$($envFile.SERVER_ARGS)", "-e", "SERVER_NAME=$name",
  "-e", "OREEXTRACT_MODE=$Mode", "-e", "OREEXTRACT_TAG=$Tag", "-e", "OREEXTRACT_OUT=/config/ore-extract-out")
foreach ($k in $Settings.Keys) { $dargs += @("-e", "$k=$($Settings[$k])") }
if ($LowPriority) { $dargs += @("--cpu-shares", "2") }
if ($Cpuset) { $dargs += @("--cpuset-cpus", $Cpuset) }
$dargs += @("-v", "munin-test_server:/opt/valheim", "-v", "${cfg}:/config", "-v", "${plg}:/opt/valheim/bepinex/BepInEx/plugins", "munin-test-fast")
docker @dargs | Out-Null
if ($LASTEXITCODE -ne 0) { throw "docker run failed" }
Write-Host "started $name"
if (-not $Wait) { return }
docker wait $name | Out-Null
docker logs $name 2>&1 | Select-String "OreExtract:" | ForEach-Object { $_.Line }
$sum = Join-Path $cfg "ore-extract-out\$Tag-summary.txt"
if (Test-Path $sum) { Get-Content $sum }
docker rm $name | Out-Null
