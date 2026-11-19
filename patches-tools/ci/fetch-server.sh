#!/usr/bin/env bash
# patches: TASK-6. The Valheim dedicated server's managed assemblies, which the build compiles
# against, into the folder given (default ./valheim-managed). Fetched with SteamCMD (an anonymous
# login is enough for app 896660), as upstream's .github/workflows/release.yml does; skipped when
# the folder (restored from the workflow's cache) holds assemblies less than a week old.
set -euo pipefail
out="${1:-valheim-managed}"

if [ -f "$out/assembly_valheim.dll" ] && [ -n "$(find "$out" -maxdepth 1 -name assembly_valheim.dll -mtime -7)" ]; then
  echo "using cached assemblies"; exit 0
fi
sudo dpkg --add-architecture i386
sudo apt-get update -q
sudo apt-get install -y -q lib32gcc-s1 curl
mkdir -p "$HOME/steamcmd" && cd "$HOME/steamcmd"
curl -sSL https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz | tar xz
# "Missing configuration" is SteamCMD not having the app manifest yet on a fresh install:
# refreshing the app info and retrying gets past it
ok=0
for attempt in 1 2 3 4; do
  if ./steamcmd.sh +@sSteamCmdForcePlatformType linux +force_install_dir "$HOME/valheim-server" \
       +login anonymous +app_info_update 1 +app_info_print 896660 +app_update 896660 validate +quit; then ok=1; break; fi
  echo "steamcmd attempt $attempt failed, retrying"; sleep 15
done
[ "$ok" = 1 ] || { echo "::error::steamcmd could not fetch app 896660"; exit 1; }
cd "$GITHUB_WORKSPACE"
rm -rf "$out" && mkdir -p "$out"
cp "$HOME/valheim-server/valheim_server_Data/Managed/"*.dll "$out/"
ls "$out" | head
