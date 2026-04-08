#!/usr/bin/env bash
set -euo pipefail

# Rebuilds the NativeAOT .NET engine and updates the SwiftUI XCFramework.
#
# Usage:
#   ./scripts/build_engine_xcframework.sh
#
# Optional env vars:
#   CONFIG=Release|Debug               (default: Release)
#   DOTNET_ARGS="..."                  (extra args passed to dotnet publish)
#   SKIP_XCODE_BUILD=1                 (skip building the SwiftUI project)

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONFIG="${CONFIG:-Release}"
DOTNET_ARGS="${DOTNET_ARGS:-}"
SKIP_XCODE_BUILD="${SKIP_XCODE_BUILD:-0}"

MOBILE_HOST_PROJ="$ROOT_DIR/Mobile/DigitalTwin.Mobile.NativeHost/DigitalTwin.Mobile.NativeHost.csproj"

SWIFT_INTEROP_DIR="$ROOT_DIR/SwiftUIApp/DigitalTwinApp/Interop"
FRAMEWORKS_DIR="$SWIFT_INTEROP_DIR/_frameworks"
XCFRAMEWORK_OUT="$SWIFT_INTEROP_DIR/DigitalTwin.Mobile.Engine.xcframework"

NAME="DigitalTwin.Mobile.NativeHost"

echo "==> Publishing NativeAOT dylibs ($CONFIG)"
dotnet publish "$MOBILE_HOST_PROJ" -c "$CONFIG" -r iossimulator-arm64 -p:NativeLib=Shared $DOTNET_ARGS
dotnet publish "$MOBILE_HOST_PROJ" -c "$CONFIG" -r ios-arm64 -p:NativeLib=Shared $DOTNET_ARGS

SIM_PUB="$ROOT_DIR/Mobile/DigitalTwin.Mobile.NativeHost/bin/$CONFIG/net10.0/iossimulator-arm64/publish"
DEV_PUB="$ROOT_DIR/Mobile/DigitalTwin.Mobile.NativeHost/bin/$CONFIG/net10.0/ios-arm64/publish"

SIM_DYLIB="$SIM_PUB/$NAME.dylib"
DEV_DYLIB="$DEV_PUB/$NAME.dylib"

if [[ ! -f "$SIM_DYLIB" ]]; then
  echo "ERROR: Missing simulator dylib at: $SIM_DYLIB" >&2
  exit 1
fi
if [[ ! -f "$DEV_DYLIB" ]]; then
  echo "ERROR: Missing device dylib at: $DEV_DYLIB" >&2
  exit 1
fi

echo "==> Creating framework bundles + XCFramework"
rm -rf "$FRAMEWORKS_DIR" "$XCFRAMEWORK_OUT"
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

xcodebuild -create-xcframework \
  -framework "$FRAMEWORKS_DIR/ios-arm64/$NAME.framework" \
  -framework "$FRAMEWORKS_DIR/iossimulator-arm64/$NAME.framework" \
  -output "$XCFRAMEWORK_OUT"

echo "==> XCFramework updated at: $XCFRAMEWORK_OUT"

if [[ "$SKIP_XCODE_BUILD" != "1" ]]; then
  echo "==> Building SwiftUI app (simulator, Debug)"
  xcodebuild \
    -project "$ROOT_DIR/SwiftUIApp/DigitalTwinApp.xcodeproj" \
    -scheme DigitalTwinApp \
    -configuration Debug \
    -sdk iphonesimulator \
    -arch arm64 \
    build
fi

echo "==> Done"

