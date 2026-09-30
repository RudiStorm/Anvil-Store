#!/bin/sh
set -eu

# Railway volumes are mounted after the image is built and are owned by root.
# Prepare the mounted application data directory before dropping privileges.
mkdir -p /app/data/store-storage /app/data/keys
chown -R anvil:anvil /app/data

exec su -s /bin/sh anvil -c 'dotnet /app/Anvil.Store.dll --urls "http://0.0.0.0:${PORT:-8080}"'
