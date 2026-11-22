#!/usr/bin/env bash
# TASK-4.2: build zonegen for Windows and Linux and pack each with the README:
#   dist/zonegen-<version>-win-x64.zip, dist/zonegen-<version>-linux-x64.tar.gz
# Needs the .NET 8 SDK (and GNU tar, as in Git Bash or any Linux).
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
ver=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$here/cli/zonegen.csproj")
rm -rf "$here/dist"
for rid in win-x64 linux-x64; do
  dotnet publish "$here/cli/zonegen.csproj" -c Release -r "$rid" -nologo -v q -o "$here/dist/$rid"
  cp "$here/README.md" "$here/dist/$rid/"
done
(cd "$here/dist/win-x64" && rm -f ../zonegen-$ver-win-x64.zip && \
  if command -v zip >/dev/null; then zip -q ../zonegen-$ver-win-x64.zip zonegen.exe README.md; \
  else powershell -NoProfile -Command "Compress-Archive -Force -Path zonegen.exe,README.md -DestinationPath ../zonegen-$ver-win-x64.zip"; fi)
tar -C "$here/dist/linux-x64" --mode=755 -czf "$here/dist/zonegen-$ver-linux-x64.tar.gz" zonegen README.md
ls -l "$here/dist"/zonegen-*
