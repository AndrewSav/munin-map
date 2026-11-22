---
id: TASK-4.3
title: Ship ore-extract with the fork's releases
status: Done
assignee:
  - '@claude'
created_date: '2026-11-22T22:22:18Z'
updated_date: '2026-11-22T22:56:34Z'
labels:
  - tooling
  - release
dependencies: []
parent_task_id: TASK-4
type: feature
ordinal: 32000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
ore-extract (TASK-4.2's ore-extract, renamed) generates a copy of a world headless and writes its ore deposits; it is fork tooling in patches-tools today. TASK-4's approach makes it part of the product: admins run it by hand for `predict_ore = file`, and the server extract (`predict_ore = server`) needs it on the server. So it has to come with each release, built from the same commit as Munin, for Windows and Linux, and live where product code lives rather than in patches-tools.

Ref: TASK-4 (approach in its notes), TASK-4.2 (patches-tools/zonegen, build.sh, README), .github/workflows/patches.yml (the fork's release workflow), decision-1 (release tags).
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 zonegen is renamed ore-extract throughout what ships: folder, binaries and bundles (ore-extract-win-x64.zip and ore-extract-linux-x64.tar.gz: fixed names like MuninMap.zip, so releases/latest/download always has the newest; the version is inside, `ore-extract version`), the project, the BepInEx plugin inside it (OreExtract, with its own ID), its environment variables (OREEXTRACT_*), its work folder and its README; TASK-4.1/4.2 and the history keep the old name
- [x] #2 Each fork release built by the release workflow carries the ore-extract bundles for Windows x64 and Linux x64 as release assets next to MuninMap.zip, listed in SHA256SUMS
- [x] #3 ore-extract is built from the same commit as Munin, and its version names the Munin release it belongs to
- [x] #4 ore-extract moves from patches-tools/zonegen to a new top-level ore-extract/ folder (plugin/, cli/, its README and build script), a folder upstream doesn't have, so a rebase can't conflict there; patches-tools keeps only what isn't shipped (decision-2)
- [x] #5 A release without the bundles fails the workflow instead of publishing half a release
- [x] #6 PATCHES.md has an entry: what admins get and how it differs from upstream
<!-- AC:END -->

## Definition of Done
<!-- DOD:BEGIN -->
- [ ] #1 Written to decision-2 (rebases easily): new behaviour in files we own, upstream edits small and marked, PATCHES.md entry current for a product change
<!-- DOD:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Move with git mv: patches-tools/zonegen/{plugin,cli,README.md,build.sh,.gitignore} to a new top-level ore-extract/; the test-only scripts (run.ps1, bench.ps1, compare.py, compare_locations.py) stay, in patches-tools/ore-extract/, pointed at the new plugin path.
2. Rename: projects (ore-extract/plugin/OreExtract.csproj, ore-extract/cli/ore-extract.csproj, AssemblyName ore-extract), namespaces (OreExtract, OreExtractCli), the BepInEx plugin (id patches.oreextract, name OreExtract, DLL OreExtract.dll, embedded under that name), log prefix "OreExtract:", environment variables ZONEGEN_* to OREEXTRACT_* (plugin, CLI, spike scripts), work folder %LOCALAPPDATA%\ore-extract and ~/.local/share/ore-extract, server names ore-extract-<k>, the parent check (/proc/<pid>/comm starting ore-extract), usage text and README.
3. Version: the CLI's InformationalVersion is the release stamp (X.Y.Z+patches.N) passed by the workflow, so `ore-extract version` names the Munin release; a local build stays 0.0.0-local.
4. build.sh: fixed bundle names ore-extract-win-x64.zip and ore-extract-linux-x64.tar.gz, version from an environment variable, zip via `zip` (Linux runner) or PowerShell (Windows).
5. Workflow (.github/workflows/patches.yml, ours): after Munin's build (which fills libs/valheim and libs/BepInEx), build ore-extract with the stamp; add both bundles to dist/, to SHA256SUMS, to the uploaded artifact and to `gh release create`; a check before publishing fails the run if any of the three assets is missing.
6. PATCHES.md entry for TASK-4.3 (admins get ore-extract with each release; upstream has no such tool). README: download from the release, the new name.
7. Verify: local build of both bundles (Windows and the Linux container), ore-extract version, a short run with the renamed plugin on Windows and Linux, the spike scripts' paths; then the workflow on a push to patches (build only) shows the bundles in the artifact; the release path is checked at the next release.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Done locally (2026-11-23): moved with git mv to a top-level ore-extract/ (plugin/OreExtract.cs and .csproj, cli/ with ore-extract.csproj, README, build.sh, .gitignore); the spike scripts and accuracy checks to patches-tools/ore-extract/ (run.ps1 now builds against ../../ore-extract/plugin; the local test data folder data/zonegen is kept, since it holds existing worlds and runs). Renamed throughout: namespaces OreExtract/OreExtractCli, plugin id patches.oreextract, OREEXTRACT_* variables, log prefix, work folder, server and world names (oreextract_probe/oreextract_seed: no hyphen in world names), the parent check (/proc/<pid>/comm starting ore-extract). The blanket rename turned the game's ZoneSystem.IsZoneGenerated into IsOreExtracterated; caught by the build and restored, and a check found no other word hit; a line diff against the old files shows only the renames plus the intended version and header changes.
Version: Version 0.0.0-local, no commit appended to the informational version, ToolVersion shows it whole (it used to cut at +, which would have dropped +patches.N); build.sh passes OREEXTRACT_VERSION as InformationalVersion; fixed bundle names ore-extract-win-x64.zip and ore-extract-linux-x64.tar.gz. The Exec that builds the plugin now uses forward slashes (MSBuild doesn't convert a command string on Linux).
Firewall: the port search opened a UDP socket on all interfaces per candidate port, which made Windows Firewall ask about ore-extract (the user saw the prompt); it now reads the system's list of UDP listeners (IPGlobalProperties.GetActiveUdpListeners) and opens nothing. The generator server itself still listens on its game port, so Windows may ask once about valheim_server.exe; no incoming connections are needed either way.
Checked: both bundles build on Windows and in the .NET 8 SDK Linux container (as the runner will), ore-extract version prints the stamp passed; Windows run (the same deposits and accuracy as before the rename, OreExtract.dll the only plugin loaded), two processes on separate ports with the new port search; Linux run and the kill test (the server quits when the renamed tool is gone).
Workflow (.github/workflows/patches.yml): a step builds ore-extract after Munin on every run (OREEXTRACT_VERSION = the release stamp), SHA256SUMS covers all three files, the release check also compares `ore-extract version` with the stamp, publishing stops before the release exists if any asset is missing, and the artifact carries the bundles. README: a Get it section (release assets, latest links, version) and the build notes. PATCHES.md: TASK-4.3 entry.
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
ore-extract (TASK-4.2's zonegen, renamed) now ships with the fork's releases. It moved to a top-level ore-extract/ folder (plugin/, cli/, README, build.sh), a folder upstream doesn't have; the test-only scripts and accuracy checks stayed in patches-tools/ore-extract/. Renamed throughout (namespaces, plugin id, OREEXTRACT_* variables, work folder, server and world names, the parent check). Bundles have fixed names, ore-extract-win-x64.zip and ore-extract-linux-x64.tar.gz, so releases/latest/download always has the newest, and `ore-extract version` prints the release stamp the workflow passes in. The release workflow builds ore-extract after Munin on every run, from the same commit and assemblies, adds both bundles to SHA256SUMS and the artifact, checks their version against the stamp on a release, and stops before publishing if any asset is missing. The port search no longer opens test sockets (it made Windows Firewall ask about the tool); it reads the system's list of UDP listeners.

Verified: both bundles build on Windows and in the Linux .NET 8 SDK container; runs on Windows (same deposits and accuracy as before) and Linux (including the kill test); two processes on separate ports. Release path checked with the release patches-3.4.0-6 (started by hand from the workflow): the version check passed for both MuninMap.dll and ore-extract (3.4.0+patches.6), the release carries MuninMap.zip, both ore-extract bundles and SHA256SUMS, and releases/latest/download/ore-extract-win-x64.zip downloads, matches its checksum and reports that version. The missing-asset stop runs only on a failure and was not exercised. README (Get it, build) and PATCHES.md updated.
<!-- SECTION:FINAL_SUMMARY:END -->
