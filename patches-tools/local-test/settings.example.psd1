# Where the live server is. Copy this file to settings.psd1 (git ignores it) and fill it in.
@{
  # ssh host of the live server. Windows OpenSSH logs in with a key from the ssh-agent.
  SshHost = 'my-valheim-host'

  # The live server's compose folder, relative to the ssh user's home. It holds
  # docker-compose.yml (for ghcr.io/community-valheim-tools/valheim-server) and the
  # ./config folder mounted at /config: worlds_local/, backups/ and bepinex/.
  LiveDir = 'path/to/valheim'
}
