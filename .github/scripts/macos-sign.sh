#!/usr/bin/env bash
set -euo pipefail

rid="$1"
app_dir="./artifacts/${rid}/${APP_NAME}.app"
dmg_path="./artifacts/${rid}/${APP_NAME}-${rid}.dmg"

codesign --force --deep --options runtime --timestamp --sign "${APPLE_SIGNING_IDENTITY}" "${app_dir}"
codesign --verify --deep --strict --verbose=2 "${app_dir}"

codesign --force --timestamp --sign "${APPLE_SIGNING_IDENTITY}" "${dmg_path}"
codesign --verify --verbose=2 "${dmg_path}"
