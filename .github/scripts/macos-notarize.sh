#!/usr/bin/env bash
set -euo pipefail

rid="$1"
dmg_path="./artifacts/${rid}/${APP_NAME}-${rid}.dmg"
app_dir="./artifacts/${rid}/${APP_NAME}.app"

xcrun notarytool submit "${dmg_path}" \
  --apple-id "${APPLE_ID}" \
  --password "${APPLE_APP_PASSWORD}" \
  --team-id "${APPLE_TEAM_ID}" \
  --wait

xcrun stapler staple "${app_dir}"
xcrun stapler staple "${dmg_path}"
