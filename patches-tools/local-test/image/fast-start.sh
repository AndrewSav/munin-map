#!/bin/bash
# Starts the Valheim server straight away: no supervisord, no chown/chmod passes, no steamcmd,
# no BepInEx check. It runs the server and BepInEx install that the image's own startup has
# already merged into /opt/valheim/bepinex (update.ps1 refreshes that).
set -euo pipefail
cd /opt/valheim/bepinex

# the plugin, as the image's bootstrap would sync it from /config/bepinex/plugins
mkdir -p BepInEx/plugins
cp -f /config/bepinex/plugins/*.dll BepInEx/plugins/

export SteamAppId=892970
export DOORSTOP_ENABLED=1 DOORSTOP_TARGET_ASSEMBLY=./BepInEx/core/BepInEx.Preloader.dll
export LD_LIBRARY_PATH="$PWD/linux64/:$PWD/doorstop_libs/"
export LD_PRELOAD=libdoorstop_x64.so
public=0; [ "${SERVER_PUBLIC:-false}" = true ] && public=1
# shellcheck disable=SC2086
exec ./valheim_server.x86_64 -nographics -batchmode -name "$SERVER_NAME" -port "${SERVER_PORT:-2456}" \
  -world "$WORLD_NAME" -public "$public" ${SERVER_ARGS:-}
