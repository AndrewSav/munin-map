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
  "plain"     = @{ ZONEGEN_PLAIN = 1 }
  "budget"    = @{ ZONEGEN_BUDGET_MS = 30 }
  "uncap"     = @{ ZONEGEN_BUDGET_MS = 30; ZONEGEN_UNCAP = 1 }
  "ahead"     = @{ ZONEGEN_BUDGET_MS = 30; ZONEGEN_AHEAD = 16 }
  "nosave"    = @{ ZONEGEN_PLAIN = 1; ZONEGEN_NOSAVE = 1 }
  "nodungeon" = @{ ZONEGEN_PLAIN = 1; ZONEGEN_NODUNGEON = 1 }
  "vegcut"    = @{ ZONEGEN_PLAIN = 1; ZONEGEN_VEGCUT = 94 }
  "land"      = @{ ZONEGEN_PLAIN = 1; ZONEGEN_LAND = 1 }
  "all"       = @{ ZONEGEN_BUDGET_MS = 30; ZONEGEN_UNCAP = 1; ZONEGEN_AHEAD = 16; ZONEGEN_NOSAVE = 1; ZONEGEN_NODUNGEON = 1; ZONEGEN_VEGCUT = 94 }
  "all-land"  = @{ ZONEGEN_BUDGET_MS = 30; ZONEGEN_UNCAP = 1; ZONEGEN_AHEAD = 16; ZONEGEN_NOSAVE = 1; ZONEGEN_NODUNGEON = 1; ZONEGEN_VEGCUT = 94; ZONEGEN_LAND = 1 }
}
"bench started $(Get-Date -Format s), $Limit zones per run" | Set-Content $out
foreach ($tag in $runs.Keys) {
  $s = $runs[$tag].Clone(); $s.ZONEGEN_LIMIT = $Limit
  $sw = [Diagnostics.Stopwatch]::StartNew()
  $lines = & (Join-Path $here "run.ps1") $tag -Settings $s -Wait 2>&1 | ForEach-Object { "$_" }
  $sum = ($lines | Where-Object { $_ -match '^(zones|done|failed|seconds|zones_per_min|deposits|working set)' }) -join '; '
  $peak = ($lines | Where-Object { $_ -match 'working set' } | Select-Object -Last 1)
  "$tag | wall $([int]$sw.Elapsed.TotalSeconds) s | $sum" | Add-Content $out
}
"bench done $(Get-Date -Format s)" | Add-Content $out
Get-Content $out
