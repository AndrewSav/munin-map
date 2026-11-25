<#
.SYNOPSIS
  Compare two Valheim versions' game assemblies for the game-update routine (README.md here).

.DESCRIPTION
  Given two folders of game DLLs (a valheim_server_Data\Managed, or a copy that
  patches-tools\local-test\update.ps1 kept in its data\managed\<version>), it:
  1. lists the DLLs added to or removed from the folder (a removed System DLL that MuninMap.dll
     still references is what broke 1.0.30: System.Memory);
  2. decompiles assembly_valheim and assembly_utils from both (ilspycmd), ignoring access
     modifiers so a publicized copy compares with a plain one;
  3. reports every entry of watch.txt as changed or same, then every other class that changed.

  Output folder (default: a temp folder) keeps both decompiles, so `git diff --no-index` on
  <out>\old\<Type>.cs <out>\new\<Type>.cs shows a change.

.EXAMPLE
  .\diff-game.ps1 -Old ..\local-test\data\managed\l-1.0.30 -New ..\local-test\data\server\server\valheim_server_Data\Managed
#>
param(
  [Parameter(Mandatory)][string]$Old,
  [Parameter(Mandatory)][string]$New,
  [string]$Out = (Join-Path ([IO.Path]::GetTempPath()) "valheim-game-diff"),
  [string]$Watch = (Join-Path $PSScriptRoot "watch.txt"),
  # the built plugin: its assembly references are checked against the new game folder and BepInEx
  [string]$Plugin = (Join-Path $PSScriptRoot "..\..\src\bin\Release\MuninMap.dll"),
  [string]$BepInExCore = (Join-Path $PSScriptRoot "..\..\libs\BepInEx\core")
)
$ErrorActionPreference = "Stop"
if (-not (Get-Command ilspycmd -ErrorAction SilentlyContinue)) { throw "ilspycmd not found: dotnet tool install -g ilspycmd" }

# --- 0. the plugin's references ----------------------------------------------------
# Every assembly MuninMap.dll references must come from the game's folder or BepInEx's core; anything
# else has to be merged in (src/ILRepack.targets). 1.0.30 dropped System.Memory this way.
Write-Host "== MuninMap.dll references the new game doesn't provide"
if (Test-Path $Plugin) {
  $fs = [IO.File]::OpenRead((Resolve-Path $Plugin))
  try {
    $md = [System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader([System.Reflection.PortableExecutable.PEReader]::new($fs))
    $have = @(Get-ChildItem $New -Filter *.dll | ForEach-Object BaseName) + @(Get-ChildItem $BepInExCore -Filter *.dll -ErrorAction SilentlyContinue | ForEach-Object BaseName)
    $missing = foreach ($h in $md.AssemblyReferences) {
      $r = $md.GetAssemblyReference($h); $name = $md.GetString($r.Name)
      if ($name -notin $have) { "$name $($r.Version)" }
    }
  } finally { $fs.Close() }
  if ($missing) { $missing | ForEach-Object { Write-Host "  MISSING: $_ (merge it in src/ILRepack.targets)" -ForegroundColor Red } }
  else { Write-Host "  none" }
} else { Write-Host "  (skipped: $Plugin not built)" }

# --- 1. DLL list ---------------------------------------------------------------
$oldDlls = Get-ChildItem $Old -Filter *.dll | ForEach-Object Name
$newDlls = Get-ChildItem $New -Filter *.dll | ForEach-Object Name
Write-Host "== DLLs in the folder"
$gone = $oldDlls | Where-Object { $_ -notin $newDlls }
$added = $newDlls | Where-Object { $_ -notin $oldDlls }
if ($oldDlls.Count -lt 10 -or $newDlls.Count -lt 10) {
  Write-Host "  (skipped: a folder holds only a few DLLs, not a whole Managed folder)"
} else {
  if ($gone) { $gone | ForEach-Object { Write-Host "  removed: $_" -ForegroundColor Yellow } }
  if ($added) { $added | ForEach-Object { Write-Host "  added:   $_" } }
  if (-not $gone -and -not $added) { Write-Host "  same list" }
}

# --- 2. decompile --------------------------------------------------------------
Remove-Item -Recurse -Force $Out -ErrorAction SilentlyContinue
foreach ($side in @(@{ Name = "old"; Dir = $Old }, @{ Name = "new"; Dir = $New })) {
  foreach ($asm in "assembly_valheim", "assembly_utils") {
    $dll = Join-Path $side.Dir "$asm.dll"
    if (-not (Test-Path $dll)) { throw "$dll not found" }
    $dest = Join-Path $Out "$($side.Name)\$asm"
    New-Item -ItemType Directory -Force $dest | Out-Null
    # -r: the DLLs next to it so types resolve, then the other version's for any it lacks (a kept copy
    # may hold only these two); unresolved types decompile differently and would show as changes
    $other = if ($side.Name -eq "old") { $New } else { $Old }
    ilspycmd -p -o $dest -r $side.Dir -r $other $dll | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "ilspycmd failed on $dll" }
  }
}

function Get-Normalized([string]$text) {
  # access modifiers differ between a publicized and a plain copy; nothing else should
  # (and ILSpy's //IL_ notes depend on which references it found)
  $text = $text -replace '\b(public|private|protected|internal)\s+', ''
  ($text -split "`r?`n" | ForEach-Object { $_.TrimEnd() } | Where-Object { $_ -ne '' -and $_ -notmatch '^\s*//IL_' }) -join "`n"
}

# Type -> its decompiled file (per side). ILSpy writes one file per top-level type, in a folder per
# namespace; nested types live in their parent's file.
function Get-TypeFiles([string]$side) {
  $map = @{}
  Get-ChildItem (Join-Path $Out $side) -Recurse -Filter *.cs | ForEach-Object {
    $rel = $_.FullName.Substring((Join-Path $Out "$side\").Length)
    $key = ($rel -replace '^[^\\]+\\', '') -replace '\.cs$', ''   # drop the assembly folder
    $map[$key] = $_.FullName
  }
  $map
}
$oldFiles = Get-TypeFiles "old"
$newFiles = Get-TypeFiles "new"

# every declaration of Member in a file (all overloads, or a nested class/struct), braces matched
function Get-Member([string]$file, [string]$member) {
  if (-not $file) { return $null }
  $lines = Get-Content $file
  $decl = "^\s*(\[[^\]]*\]\s*)*((public|private|protected|internal|static|virtual|override|abstract|sealed|async|unsafe|extern|new|readonly)\s+)*([\w<>\[\],\.\? ]+\s+)?$([regex]::Escape($member))\s*(<[^>]*>)?\s*(\(|$)|\b(class|struct|enum)\s+$([regex]::Escape($member))\b"
  $out = New-Object System.Collections.Generic.List[string]
  for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -notmatch $decl) { continue }
    $depth = 0; $opened = $false
    for ($j = $i; $j -lt $lines.Count; $j++) {
      $out.Add($lines[$j])
      $depth += ([regex]::Matches($lines[$j], '\{')).Count - ([regex]::Matches($lines[$j], '\}')).Count
      if ($lines[$j] -match '\{') { $opened = $true }
      if (($opened -and $depth -le 0) -or (-not $opened -and $lines[$j] -match ';\s*$')) { break }
    }
    $i = $j
  }
  if ($out.Count -eq 0) { return $null }
  $out -join "`n"
}

function Find-TypeFile($files, [string]$type) {
  if ($files.ContainsKey($type)) { return $files[$type] }
  $hit = $files.Keys | Where-Object { $_ -like "*\$type" } | Select-Object -First 1
  if ($hit) { $files[$hit] } else { $null }
}

# --- 3. report -----------------------------------------------------------------
Write-Host "== watch.txt"
$watchedTypes = @{}
$changedWatched = 0
foreach ($entry in Get-Content $Watch) {
  $entry = $entry.Trim()
  if (-not $entry -or $entry.StartsWith('#')) { continue }
  $dot = $entry.IndexOf('.')
  $type = if ($dot -lt 0) { $entry } else { $entry.Substring(0, $dot) }
  $member = if ($dot -lt 0) { $null } else { $entry.Substring($dot + 1) }
  $watchedTypes[$type] = $true
  $of = Find-TypeFile $oldFiles $type; $nf = Find-TypeFile $newFiles $type
  if ($member) { $a = Get-Member $of $member; $b = Get-Member $nf $member }
  else { $a = if ($of) { Get-Content $of -Raw } else { $null }; $b = if ($nf) { Get-Content $nf -Raw } else { $null } }
  if ($null -eq $a -and $null -eq $b) { $state = "NOT FOUND in either version (fix watch.txt)"; $color = "Red" }
  elseif ($null -eq $a) { $state = "new in the new version"; $color = "Yellow" }
  elseif ($null -eq $b) { $state = "GONE in the new version"; $color = "Red" }
  elseif ((Get-Normalized $a) -eq (Get-Normalized $b)) { $state = "same"; $color = "Gray" }
  else { $state = "CHANGED"; $color = "Yellow" }
  if ($state -ne "same") { $changedWatched++ }
  Write-Host ("  {0,-45} {1}" -f $entry, $state) -ForegroundColor $color
}

Write-Host "== other classes that changed"
$all = @($oldFiles.Keys) + @($newFiles.Keys) | Sort-Object -Unique
foreach ($k in $all) {
  $type = Split-Path $k -Leaf
  if ($watchedTypes.ContainsKey($type)) { continue }
  $a = if ($oldFiles[$k]) { Get-Normalized (Get-Content $oldFiles[$k] -Raw) } else { $null }
  $b = if ($newFiles[$k]) { Get-Normalized (Get-Content $newFiles[$k] -Raw) } else { $null }
  if ($null -eq $a) { Write-Host "  added:   $k" }
  elseif ($null -eq $b) { Write-Host "  removed: $k" }
  elseif ($a -ne $b) { Write-Host "  changed: $k" }
}
Write-Host ""
Write-Host "Decompiled into $Out (old\..., new\...). Watched entries not the same: $changedWatched"
