<#
.SYNOPSIS
  TASK-4.1 spike: run the generator once per setting set on the same zones, one after another,
  and collect each run's summary in data/zonegen/bench.txt.
#>
param([int]$Limit = 2000)
$ErrorActionPreference = "Stop"
$here = $PSScriptRoot
$out = Join-Path $here "..\local-test\data\zonegen\bench.txt"
$runs = [ordered]@{
  "plain"     = @{ OREEXTRACT_PLAIN = 1 }
  "budget"    = @{ OREEXTRACT_BUDGET_MS = 30 }
  "uncap"     = @{ OREEXTRACT_BUDGET_MS = 30; OREEXTRACT_UNCAP = 1 }
  "ahead"     = @{ OREEXTRACT_BUDGET_MS = 30; OREEXTRACT_AHEAD = 16 }
  "nosave"    = @{ OREEXTRACT_PLAIN = 1; OREEXTRACT_NOSAVE = 1 }
  "nodungeon" = @{ OREEXTRACT_PLAIN = 1; OREEXTRACT_NODUNGEON = 1 }
  "vegcut"    = @{ OREEXTRACT_PLAIN = 1; OREEXTRACT_VEGCUT = 94 }
  "land"      = @{ OREEXTRACT_PLAIN = 1; OREEXTRACT_LAND = 1 }
  "all"       = @{ OREEXTRACT_BUDGET_MS = 30; OREEXTRACT_UNCAP = 1; OREEXTRACT_AHEAD = 16; OREEXTRACT_NOSAVE = 1; OREEXTRACT_NODUNGEON = 1; OREEXTRACT_VEGCUT = 94 }
  "all-land"  = @{ OREEXTRACT_BUDGET_MS = 30; OREEXTRACT_UNCAP = 1; OREEXTRACT_AHEAD = 16; OREEXTRACT_NOSAVE = 1; OREEXTRACT_NODUNGEON = 1; OREEXTRACT_VEGCUT = 94; OREEXTRACT_LAND = 1 }
}
"bench started $(Get-Date -Format s), $Limit zones per run" | Set-Content $out
foreach ($tag in $runs.Keys) {
  $s = $runs[$tag].Clone(); $s.OREEXTRACT_LIMIT = $Limit
  $sw = [Diagnostics.Stopwatch]::StartNew()
  $lines = & (Join-Path $here "run.ps1") $tag -Settings $s -Wait 2>&1 | ForEach-Object { "$_" }
  $sum = ($lines | Where-Object { $_ -match '^(zones|done|failed|seconds|zones_per_min|deposits|working set)' }) -join '; '
  $peak = ($lines | Where-Object { $_ -match 'working set' } | Select-Object -Last 1)
  "$tag | wall $([int]$sw.Elapsed.TotalSeconds) s | $sum" | Add-Content $out
}
"bench done $(Get-Date -Format s)" | Add-Content $out
Get-Content $out
