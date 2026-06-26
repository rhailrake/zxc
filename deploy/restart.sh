#!/usr/bin/env bash
set -euo pipefail

SERVICE="${ZXC_SYSTEMD_SERVICE:-zxc.service}"

sudo systemctl restart "$SERVICE"
