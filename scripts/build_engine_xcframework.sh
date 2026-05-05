#!/bin/zsh
set -euo pipefail

# ── Paths & names ──────────────────────────────────────────────────────────
ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
NAME="DigitalTwin.Mobile.NativeHost"
PROJ="$ROOT_DIR/Mobile/DigitalTwin.Mobile.NativeHost/DigitalTwin.Mobile.NativeHost.csproj"
FRAMEWORKS_DIR="$ROOT_DIR/build/frameworks"
XCFRAMEWORK_OUT="$ROOT_DIR/SwiftUIApp/DigitalTwinApp/Interop/DigitalTwin.Mobile.NativeHost.xcframework"

CONFIG="${1:-Debug}"

# ── 0. Generate Secrets.xcconfig from .env ────────────────────────────────
"$ROOT_DIR/scripts/generate_xcconfig.sh"

# ── 0b. Load .env into environment for engine build ───────────────────────
# This is used by MSBuild to generate `EngineBuildConfig.g.cs` at compile time.
if [[ -f "$ROOT_DIR/.env" ]]; then
  set -a
  source "$ROOT_DIR/.env"
  set +a
fi

# ── 0c. Sync OCR model assets into the Mobile OCR project ──────────────────
"$ROOT_DIR/scripts/sync_mobile_ocr_models.sh"

# ── 1. Publish NativeAOT for device (ios-arm64) ───────────────────────────
echo "==> Publishing NativeAOT for ios-arm64 ($CONFIG)..."
dotnet publish "$PROJ" \
  -c "$CONFIG" \
  -r ios-arm64 \
  --self-contained

DEV_BASE="$ROOT_DIR/Mobile/DigitalTwin.Mobile.NativeHost/bin/$CONFIG/net10.0/ios-arm64"
DEV_DYLIB="${DEV_BASE}/publish/${NAME}.dylib"
# Fallback: some SDK versions place native output under native/
[[ ! -f "$DEV_DYLIB" ]] && DEV_DYLIB="${DEV_BASE}/native/${NAME}.dylib"

if [[ ! -f "$DEV_DYLIB" ]]; then
  echo "ERROR: Device dylib not found"
  echo "Searched:"
  echo "  ${DEV_BASE}/publish/${NAME}.dylib"
  echo "  ${DEV_BASE}/native/${NAME}.dylib"
  echo "Contents of base dir:"
  find "$DEV_BASE" -type f -name "*.dylib" 2>/dev/null || echo "(none)"
  exit 1
fi
echo "  Found: $DEV_DYLIB"

# ── 2. Publish NativeAOT for simulator (iossimulator-arm64) ───────────────
echo "==> Publishing NativeAOT for iossimulator-arm64 ($CONFIG)..."
dotnet publish "$PROJ" \
  -c "$CONFIG" \
  -r iossimulator-arm64 \
  --self-contained

SIM_BASE="$ROOT_DIR/Mobile/DigitalTwin.Mobile.NativeHost/bin/$CONFIG/net10.0/iossimulator-arm64"
SIM_DYLIB="${SIM_BASE}/publish/${NAME}.dylib"
[[ ! -f "$SIM_DYLIB" ]] && SIM_DYLIB="${SIM_BASE}/native/${NAME}.dylib"

if [[ ! -f "$SIM_DYLIB" ]]; then
  echo "ERROR: Simulator dylib not found"
  echo "Searched:"
  echo "  ${SIM_BASE}/publish/${NAME}.dylib"
  echo "  ${SIM_BASE}/native/${NAME}.dylib"
  echo "Contents of base dir:"
  find "$SIM_BASE" -type f -name "*.dylib" 2>/dev/null || echo "(none)"
  exit 1
fi
echo "  Found: $SIM_DYLIB"

# ── 3. Assemble .framework bundles ────────────────────────────────────────
echo "==> Assembling framework bundles..."
rm -rf "$FRAMEWORKS_DIR"
mkdir -p \
  "$FRAMEWORKS_DIR/ios-arm64/$NAME.framework" \
  "$FRAMEWORKS_DIR/iossimulator-arm64/$NAME.framework"

cp "$DEV_DYLIB" "$FRAMEWORKS_DIR/ios-arm64/$NAME.framework/$NAME"
cp "$SIM_DYLIB" "$FRAMEWORKS_DIR/iossimulator-arm64/$NAME.framework/$NAME"

/usr/bin/install_name_tool -id "@rpath/$NAME.framework/$NAME" "$FRAMEWORKS_DIR/ios-arm64/$NAME.framework/$NAME"
/usr/bin/install_name_tool -id "@rpath/$NAME.framework/$NAME" "$FRAMEWORKS_DIR/iossimulator-arm64/$NAME.framework/$NAME"

create_info_plist() {
  local framework_dir="$1"
  /usr/bin/plutil -create xml1 "$framework_dir/Info.plist"
  /usr/bin/plutil -insert CFBundleName -string "$NAME" "$framework_dir/Info.plist"
  /usr/bin/plutil -insert CFBundleIdentifier -string "com.digitaltwin.$NAME" "$framework_dir/Info.plist"
  /usr/bin/plutil -insert CFBundleVersion -string "1" "$framework_dir/Info.plist"
  /usr/bin/plutil -insert CFBundleShortVersionString -string "1.0" "$framework_dir/Info.plist"
  /usr/bin/plutil -insert CFBundleExecutable -string "$NAME" "$framework_dir/Info.plist"
  /usr/bin/plutil -insert CFBundlePackageType -string "FMWK" "$framework_dir/Info.plist"
}

create_info_plist "$FRAMEWORKS_DIR/ios-arm64/$NAME.framework"
create_info_plist "$FRAMEWORKS_DIR/iossimulator-arm64/$NAME.framework"

# ── 4. Build XCFramework ─────────────────────────────────────────────────
echo "==> Creating XCFramework..."
rm -rf "$XCFRAMEWORK_OUT"
xcodebuild -create-xcframework \
  -framework "$FRAMEWORKS_DIR/ios-arm64/$NAME.framework" \
  -framework "$FRAMEWORKS_DIR/iossimulator-arm64/$NAME.framework" \
  -output "$XCFRAMEWORK_OUT"

echo "==> XCFramework updated at: $XCFRAMEWORK_OUT"

# ── 5. Build SwiftUI app (optional — skip with SKIP_XCODE_BUILD=1) ──────
if [[ "${SKIP_XCODE_BUILD:-0}" != "1" ]]; then
  echo "==> Building SwiftUI app (simulator, Debug)..."
  xcodebuild \
    -project "$ROOT_DIR/SwiftUIApp/DigitalTwinApp.xcodeproj" \
    -scheme DigitalTwinApp \
    -configuration Debug \
    -sdk iphonesimulator \
    -arch arm64 \
    build
fi

echo "==> Done"