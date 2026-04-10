#!/bin/zsh
set -euo pipefail

# ── Generate Secrets.xcconfig from root .env ──────────────────────────────
# Run as an Xcode pre-build script or manually before building.
# Only the keys listed in ALLOWED_KEYS are forwarded (no DB passwords, etc.).

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
ENV_FILE="$ROOT_DIR/.env"
# The iOS target uses this xcconfig as its base configuration (see project.pbxproj).
XCCONFIG_OUT="$ROOT_DIR/SwiftUIApp/DigitalTwinApp/Secrets.xcconfig"

ALLOWED_KEYS=(
  GEMINI_API_KEY
  OPENWEATHERMAP_API_KEY
  GOOGLE_OAUTH_CLIENT_ID
  API_BASE_URL
)


if [[ ! -f "$ENV_FILE" ]]; then
  echo "⚠️  No .env file found at $ENV_FILE — generating empty Secrets.xcconfig"
  cat > "$XCCONFIG_OUT" <<'EOF'
// Auto-generated — do not edit. Run scripts/generate_xcconfig.sh to regenerate.
SLASH = /
GEMINI_API_KEY =
OPENWEATHERMAP_API_KEY =
GOOGLE_OAUTH_CLIENT_ID =
API_BASE_URL =
EOF
  exit 0
fi

echo "// Auto-generated from .env — do not edit. Run scripts/generate_xcconfig.sh to regenerate." > "$XCCONFIG_OUT"
echo "SLASH = /" >> "$XCCONFIG_OUT"

for key in "${ALLOWED_KEYS[@]}"; do
  value=$(grep "^${key}=" "$ENV_FILE" 2>/dev/null | head -1 | cut -d'=' -f2- || true)
  # xcconfig treats `//` as a comment start. Convert URLs like `http://...` to
  # `http:$(SLASH)$(SLASH)...` so build settings expand safely.
  value="$(printf '%s' "$value" | sed 's#://#:$(SLASH)$(SLASH)#g')"
  echo "${key} = ${value}" >> "$XCCONFIG_OUT"
done

echo "==> Secrets.xcconfig written to $XCCONFIG_OUT"
