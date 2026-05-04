#!/usr/bin/env bash
set -euo pipefail

security delete-keychain build.keychain || true
rm -f apple_cert.p12
