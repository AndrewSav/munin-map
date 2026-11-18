# Fast-starting test server image: the live image with its startup replaced.
# The live image's startup (supervisord, chown/chmod passes, steamcmd, BepInEx check and merge)
# costs ~30 s on every start. This one runs the already-merged server + BepInEx install in
# /opt/valheim/bepinex directly. update.ps1 runs the live image's own startup to refresh it.
ARG BASE_IMAGE=ghcr.io/community-valheim-tools/valheim-server:1.4.1
FROM ${BASE_IMAGE}
# the user the server runs as: live's PUID/PGID, from .env via compose.yml
ARG UID=1000
ARG GID=1000

# what the live image's bootstrap does at every start, done once here: give the valheim user
# that ID and the home folder, where Steam and the save-dir symlink live
RUN sed -i -E "s/^valheim:x:[0-9]+:[0-9]+:/valheim:x:${UID}:${GID}:/" /etc/passwd \
 && groupmod -g "${GID}" -o valheim \
 && chown -R "${UID}:${GID}" /home/valheim \
 && mkdir -p /home/valheim/munin-data && chown "${UID}:${GID}" /home/valheim/munin-data

COPY fast-start.sh /usr/local/bin/fast-start
USER ${UID}:${GID}
ENV HOME=/home/valheim
CMD ["/usr/local/bin/fast-start"]
