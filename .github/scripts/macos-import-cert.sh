#!/usr/bin/env bash
set -euo pipefail

echo "${APPLE_CERT_BASE64}" | base64 --decode > apple_cert.p12
security create-keychain -p "" build.keychain
security default-keychain -s build.keychain
security unlock-keychain -p "" build.keychain
security set-keychain-settings -lut 21600 build.keychain
security import apple_cert.p12 -k build.keychain -P "${APPLE_CERT_PASSWORD}" -T /usr/bin/codesign
security set-key-partition-list -S apple-tool:,apple: -s -k "" build.keychain
