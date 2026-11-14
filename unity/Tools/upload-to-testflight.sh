#!/usr/bin/env bash
# Unity Build Automation's post-build script for iOS: sends the .ipa to App Store Connect, where
# TestFlight picks it up, so uploading needs no Mac on our side. SETUP.md 9.7 sets these on the
# build target:
#   ASC_KEY_ID, ASC_ISSUER_ID   the App Store Connect API key's IDs
#   ASC_KEY_BASE64              its .p8 file, as base64 on one line
#   ASC_APP_ID                  the app's Apple ID in App Store Connect (digits only)
set -uo pipefail

for name in ASC_KEY_ID ASC_ISSUER_ID ASC_KEY_BASE64 ASC_APP_ID; do
  if [[ -z "${!name:-}" ]]; then
    echo "$name is not set: skipping the upload to App Store Connect."
    exit 0
  fi
done
echo "::mask-value::$ASC_KEY_BASE64"

ipa="${UNITY_PLAYER_PATH:-}"
if [[ -d "$ipa" ]]; then
  ipa="$(find "$ipa" -name '*.ipa' -print -quit)"
fi
if [[ ! -f "$ipa" ]]; then
  echo "No .ipa found (UNITY_PLAYER_PATH='${UNITY_PLAYER_PATH:-}')."
  exit 1
fi

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
mkdir "$work/private_keys"
echo "$ASC_KEY_BASE64" | base64 --decode > "$work/private_keys/AuthKey_$ASC_KEY_ID.p8"
unzip -p "$ipa" 'Payload/*.app/Info.plist' > "$work/Info.plist"

info() {
  plutil -extract "$1" raw -o - "$work/Info.plist"
}

API_PRIVATE_KEYS_DIR="$work/private_keys" xcrun altool --upload-package "$ipa" \
  --type ios \
  --apple-id "$ASC_APP_ID" \
  --bundle-id "$(info CFBundleIdentifier)" \
  --bundle-version "$(info CFBundleVersion)" \
  --bundle-short-version-string "$(info CFBundleShortVersionString)" \
  --apiKey "$ASC_KEY_ID" \
  --apiIssuer "$ASC_ISSUER_ID" 2>&1 | tee "$work/upload.log"
status="${PIPESTATUS[0]}"

# altool in Xcode 26 can exit 0 after a failed upload, so its output has the last word.
if [[ "$status" -ne 0 ]] || grep -qiE 'error:|upload failed' "$work/upload.log"; then
  echo "The upload to App Store Connect failed."
  exit 1
fi
echo "Uploaded to App Store Connect. TestFlight shows the build once Apple has processed it."
