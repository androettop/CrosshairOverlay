#!/usr/bin/env bash
set -euo pipefail

rid="$1"
publish_dir="./publish/${rid}"
artifact_dir="./artifacts/${rid}"
tarball_path="${artifact_dir}/${APP_NAME}-${rid}.tar.gz"

mkdir -p "${artifact_dir}"
tar -czf "${tarball_path}" -C "${publish_dir}" .
