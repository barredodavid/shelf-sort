#!/bin/bash
# Unity Build Automation post-build script (build target > Script hooks > Post-build script: ci/upload-testflight.sh):
# sends the signed .ipa just built to TestFlight with an App Store Connect API key.
# Environment variables to set on the build target: ASC_KEY_ID, ASC_ISSUER_ID, ASC_KEY_P8 (the .p8 file's contents).
set -e
if [ -z "$ASC_KEY_ID" ] || [ -z "$ASC_ISSUER_ID" ] || [ -z "$ASC_KEY_P8" ]; then
  echo "TestFlight upload skipped: ASC_KEY_ID / ASC_ISSUER_ID / ASC_KEY_P8 not set"
  exit 0
fi

# the .ipa: the path Unity passes ($1, the build output), or the newest one under the workspace
IPA=""
if [ -n "$1" ]; then IPA=$(find "$1" -name "*.ipa" 2>/dev/null | head -1); fi
if [ -z "$IPA" ]; then IPA=$(find "${WORKSPACE:-.}" -name "*.ipa" -print0 2>/dev/null | xargs -0 ls -t 2>/dev/null | head -1); fi
if [ -z "$IPA" ]; then
  echo "TestFlight upload: no .ipa found"
  exit 1
fi
echo "Uploading $IPA to TestFlight"

# altool looks for the key as AuthKey_<id>.p8 in ~/.appstoreconnect/private_keys
mkdir -p ~/.appstoreconnect/private_keys
# a one-line settings field loses the .p8's line breaks (or keeps them as "\n"): keep only the base64 body between
# the BEGIN/END lines and rebuild the file, 64 characters a line
BODY=$(printf '%s' "$ASC_KEY_P8" | sed 's/\\n/ /g; s/-----BEGIN PRIVATE KEY-----//; s/-----END PRIVATE KEY-----//' | tr -d ' \r\n\t')
{
  echo "-----BEGIN PRIVATE KEY-----"
  printf '%s' "$BODY" | fold -w 64
  echo
  echo "-----END PRIVATE KEY-----"
} > ~/.appstoreconnect/private_keys/AuthKey_$ASC_KEY_ID.p8
xcrun altool --upload-app --type ios --file "$IPA" --apiKey "$ASC_KEY_ID" --apiIssuer "$ASC_ISSUER_ID"
