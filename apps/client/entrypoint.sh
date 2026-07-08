#!/bin/sh
set -e

# a hacky way of doing runtime environment variable injection for Vite apps

# Replace the build-time placeholder with the runtime environment variable
# in all JS files served by a web server of choice (caddy, nginx, etc.).
if [ -n "$VITE_DISCORD_CLIENT_ID" ]; then
  find /srv -name '*.js' -exec sed -i "s/__VITE_DISCORD_CLIENT_ID__/${VITE_DISCORD_CLIENT_ID}/g" {} +
  echo "Injected VITE_DISCORD_CLIENT_ID at runtime."
else
  echo "WARNING: VITE_DISCORD_CLIENT_ID is not set. The app will not work correctly."
fi

# Hand off to the default Caddy entrypoint
exec caddy run --config /etc/caddy/Caddyfile --adapter caddyfile
