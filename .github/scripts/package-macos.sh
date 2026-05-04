#!/usr/bin/env bash
set -euo pipefail

rid="$1"
publish_dir="./publish/${rid}"
artifact_dir="./artifacts/${rid}"
app_dir="${artifact_dir}/${APP_NAME}.app"
contents_dir="${app_dir}/Contents"
macos_dir="${contents_dir}/MacOS"
resources_dir="${contents_dir}/Resources"
dmg_stage_dir="${artifact_dir}/dmg"
dmg_path="${artifact_dir}/${APP_NAME}-${rid}.dmg"
icon_source="./assets/icon-source.png"
app_version="${APP_VERSION:-0.0.0}"

rm -rf "${artifact_dir}"
mkdir -p "${macos_dir}" "${resources_dir}" "${dmg_stage_dir}"

ditto "${publish_dir}" "${macos_dir}"
chmod +x "${macos_dir}/${APP_NAME}"

iconset_dir="${artifact_dir}/AppIcon.iconset"
rm -rf "${iconset_dir}"
mkdir -p "${iconset_dir}"

sips -z 16 16     "${icon_source}" --out "${iconset_dir}/icon_16x16.png" >/dev/null
sips -z 32 32     "${icon_source}" --out "${iconset_dir}/icon_16x16@2x.png" >/dev/null
sips -z 32 32     "${icon_source}" --out "${iconset_dir}/icon_32x32.png" >/dev/null
sips -z 64 64     "${icon_source}" --out "${iconset_dir}/icon_32x32@2x.png" >/dev/null
sips -z 128 128   "${icon_source}" --out "${iconset_dir}/icon_128x128.png" >/dev/null
sips -z 256 256   "${icon_source}" --out "${iconset_dir}/icon_128x128@2x.png" >/dev/null
sips -z 256 256   "${icon_source}" --out "${iconset_dir}/icon_256x256.png" >/dev/null
sips -z 512 512   "${icon_source}" --out "${iconset_dir}/icon_256x256@2x.png" >/dev/null
sips -z 512 512   "${icon_source}" --out "${iconset_dir}/icon_512x512.png" >/dev/null
sips -z 1024 1024 "${icon_source}" --out "${iconset_dir}/icon_512x512@2x.png" >/dev/null

iconutil -c icns "${iconset_dir}" -o "${resources_dir}/AppIcon.icns"

cat > "${contents_dir}/Info.plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleDevelopmentRegion</key>
  <string>en</string>
  <key>CFBundleDisplayName</key>
  <string>${APP_DISPLAY_NAME}</string>
  <key>CFBundleExecutable</key>
  <string>${APP_NAME}</string>
  <key>CFBundleIdentifier</key>
  <string>com.crosshairoverlay.app</string>
  <key>CFBundleInfoDictionaryVersion</key>
  <string>6.0</string>
  <key>CFBundleIconFile</key>
  <string>AppIcon</string>
  <key>CFBundleIconName</key>
  <string>AppIcon</string>
  <key>CFBundleName</key>
  <string>${APP_DISPLAY_NAME}</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>CFBundleShortVersionString</key>
  <string>${app_version}</string>
  <key>CFBundleVersion</key>
  <string>${app_version}</string>
  <key>LSMinimumSystemVersion</key>
  <string>12.0</string>
  <key>NSHighResolutionCapable</key>
  <true/>
</dict>
</plist>
EOF

codesign --force --deep --sign - "${app_dir}"
codesign --verify --deep --strict --verbose=2 "${app_dir}"

ditto "${app_dir}" "${dmg_stage_dir}/${APP_NAME}.app"
ln -s /Applications "${dmg_stage_dir}/Applications"

hdiutil create \
  -volname "${APP_NAME}" \
  -srcfolder "${dmg_stage_dir}" \
  -ov \
  -format UDZO \
  "${dmg_path}"

codesign --force --sign - "${dmg_path}"
