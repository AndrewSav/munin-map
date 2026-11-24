#!/usr/bin/env bash
# TASK-4.2 (fork tooling since TASK-4.7): build ore-extract for Windows and Linux and pack each with
# the README: dist/ore-extract-win-x64.zip, dist/ore-extract-linux-x64.tar.gz. The version is inside
# (`ore-extract version`): OREEXTRACT_VERSION if set, else the project's 0.0.0-local.
# Needs the .NET 8 SDK, the game's and BepInEx's assemblies in the repo's libs/ (Munin's build puts
# them there), and GNU tar plus zip or PowerShell (Git Bash, or any Linux with zip).
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
ver=()
[ -n "${OREEXTRACT_VERSION:-}" ] && ver=("-p:InformationalVersion=$OREEXTRACT_VERSION")
rm -rf "$here/dist"
for rid in win-x64 linux-x64; do
  dotnet publish "$here/cli/ore-extract.csproj" -c Release -r "$rid" -nologo -v q -o "$here/dist/$rid" "${ver[@]}"
  cp "$here/README.md" "$here/dist/$rid/"
done
(cd "$here/dist/win-x64" && \
  if command -v zip >/dev/null; then zip -q ../ore-extract-win-x64.zip ore-extract.exe README.md; \
  else powershell -NoProfile -Command "Compress-Archive -Force -Path ore-extract.exe,README.md -DestinationPath ../ore-extract-win-x64.zip"; fi)
tar -C "$here/dist/linux-x64" --mode=755 -czf "$here/dist/ore-extract-linux-x64.tar.gz" ore-extract README.md
ls -l "$here/dist"/ore-extract-*
