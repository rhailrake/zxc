#!/usr/bin/env bash
set -euo pipefail

APP_DIR="${ZXC_APP_DIR:-/opt/zxc-src}"
PUBLISH_DIR="${ZXC_PUBLISH_DIR:-/opt/zxc}"
SERVICE="${ZXC_SYSTEMD_SERVICE:-zxc.service}"
BRANCH="${ZXC_UPDATE_BRANCH:-master}"

cd "$APP_DIR"

git fetch origin "$BRANCH"
git checkout "$BRANCH"
git reset --hard "origin/$BRANCH"

dotnet restore zxc.slnx
dotnet publish src/Zxc.Bot/Zxc.Bot.csproj -c Release -o "$PUBLISH_DIR" --no-restore

install -m 0755 deploy/update.sh "$PUBLISH_DIR/update.sh.new"
install -m 0755 deploy/restart.sh "$PUBLISH_DIR/restart.sh.new"
mv "$PUBLISH_DIR/update.sh.new" "$PUBLISH_DIR/update.sh"
mv "$PUBLISH_DIR/restart.sh.new" "$PUBLISH_DIR/restart.sh"

sudo systemctl restart "$SERVICE"
