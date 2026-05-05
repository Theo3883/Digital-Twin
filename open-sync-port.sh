#!/bin/bash
# Opens WebAPI sync port 5003 for inbound WiFi connections while this script runs.
# Live traffic on port 5003 is shown via tcpdump.
# Also patches API_BASE_URL in .env and Secrets.xcconfig to this laptop IP.
# When you press Ctrl+C or close the terminal, all changes are automatically reverted.

PORT=5003
ANCHOR="digitaltwin_sync_temp"
TCPDUMP_PID=""
HAS_CLEANED_UP="false"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ENV_FILE="$SCRIPT_DIR/.env"
SECRETS_XCCONFIG_1="$SCRIPT_DIR/SwiftUIApp/Secrets.xcconfig"
SECRETS_XCCONFIG_2="$SCRIPT_DIR/SwiftUIApp/DigitalTwinApp/Secrets.xcconfig"

ORIGINAL_ENV_API_BASE_URL=""
ORIGINAL_SECRETS_1_API_BASE_URL=""
ORIGINAL_SECRETS_2_API_BASE_URL=""

get_env_api_base_url() {
    local file="$1"
    grep -E '^API_BASE_URL=' "$file" | head -n1 | cut -d= -f2-
}

set_env_api_base_url() {
    local file="$1" value="$2"
    sed -i '' "s|^API_BASE_URL=.*|API_BASE_URL=$value|" "$file"
}

get_xcconfig_api_base_url() {
    local file="$1"
    grep -E '^API_BASE_URL[[:space:]]*=' "$file" | head -n1 | sed -E 's/^API_BASE_URL[[:space:]]*=[[:space:]]*//'
}

set_xcconfig_api_base_url() {
    local file="$1" value="$2"
    sed -i '' "s|^API_BASE_URL[[:space:]]*=.*|API_BASE_URL = $value|" "$file"
}

# ── Cleanup: close firewall rule and stop traffic capture ───────────────────

cleanup() {
    if [[ "$HAS_CLEANED_UP" == "true" ]]; then
        return
    fi
    HAS_CLEANED_UP="true"

    echo ""
    echo "Stopping traffic capture..."
    [[ -n "$TCPDUMP_PID" ]] && sudo kill "$TCPDUMP_PID" 2>/dev/null

    echo "Restoring API_BASE_URL values..."
    if [[ -f "$ENV_FILE" && -n "$ORIGINAL_ENV_API_BASE_URL" ]]; then
        set_env_api_base_url "$ENV_FILE" "$ORIGINAL_ENV_API_BASE_URL"
    fi
    if [[ -f "$SECRETS_XCCONFIG_1" && -n "$ORIGINAL_SECRETS_1_API_BASE_URL" ]]; then
        set_xcconfig_api_base_url "$SECRETS_XCCONFIG_1" "$ORIGINAL_SECRETS_1_API_BASE_URL"
    fi
    if [[ -f "$SECRETS_XCCONFIG_2" && -n "$ORIGINAL_SECRETS_2_API_BASE_URL" ]]; then
        set_xcconfig_api_base_url "$SECRETS_XCCONFIG_2" "$ORIGINAL_SECRETS_2_API_BASE_URL"
    fi

    echo "Removing inbound rule for port $PORT..."
    sudo pfctl -a "$ANCHOR" -F all 2>/dev/null
    echo "Port $PORT is CLOSED. API_BASE_URL values restored."
}

handle_signal() {
    cleanup
    exit 0
}

trap cleanup EXIT
trap handle_signal INT TERM HUP

# ── Get WiFi IP ──────────────────────────────────────────────────────────────
WIFI_IP=$(ipconfig getifaddr en0 2>/dev/null || ipconfig getifaddr en1 2>/dev/null)
if [[ -z "$WIFI_IP" ]]; then
    echo "ERROR: Could not find a WiFi IP address. Make sure you are connected to WiFi."
    exit 1
fi

NEW_API_BASE_URL="http://$WIFI_IP:$PORT"
NEW_XCCONFIG_API_BASE_URL="http:\$(SLASH)\$(SLASH)$WIFI_IP:$PORT"

if [[ -f "$ENV_FILE" ]]; then
    ORIGINAL_ENV_API_BASE_URL="$(get_env_api_base_url "$ENV_FILE")"
    if [[ -n "$ORIGINAL_ENV_API_BASE_URL" ]]; then
        set_env_api_base_url "$ENV_FILE" "$NEW_API_BASE_URL"
        echo "Updated $ENV_FILE -> API_BASE_URL=$NEW_API_BASE_URL (was: $ORIGINAL_ENV_API_BASE_URL)"
    fi
fi

if [[ -f "$SECRETS_XCCONFIG_1" ]]; then
    ORIGINAL_SECRETS_1_API_BASE_URL="$(get_xcconfig_api_base_url "$SECRETS_XCCONFIG_1")"
    if [[ -n "$ORIGINAL_SECRETS_1_API_BASE_URL" ]]; then
        set_xcconfig_api_base_url "$SECRETS_XCCONFIG_1" "$NEW_XCCONFIG_API_BASE_URL"
        echo "Updated $SECRETS_XCCONFIG_1 -> API_BASE_URL = $NEW_XCCONFIG_API_BASE_URL"
    fi
fi

if [[ -f "$SECRETS_XCCONFIG_2" ]]; then
    ORIGINAL_SECRETS_2_API_BASE_URL="$(get_xcconfig_api_base_url "$SECRETS_XCCONFIG_2")"
    if [[ -n "$ORIGINAL_SECRETS_2_API_BASE_URL" ]]; then
        set_xcconfig_api_base_url "$SECRETS_XCCONFIG_2" "$NEW_XCCONFIG_API_BASE_URL"
        echo "Updated $SECRETS_XCCONFIG_2 -> API_BASE_URL = $NEW_XCCONFIG_API_BASE_URL"
    fi
fi

# ── Ensure pf is enabled ─────────────────────────────────────────────────────
sudo pfctl -e 2>/dev/null || true   # safe: no-op if already enabled

# ── Add pass-in rule via a named anchor (leaves all other rules untouched) ───
echo "pass in proto tcp from any to any port $PORT" \
    | sudo pfctl -a "$ANCHOR" -f -

echo ""
echo "┌────────────────────────────────────────────────────────────────┐"
echo "│  Port $PORT is now OPEN for inbound connections                  │"
echo "│                                                                │"
echo "│  Laptop WiFi IP   : $WIFI_IP                                  │"
echo "│  Phone connects to: $WIFI_IP:$PORT                            │"
echo "│                                                                │"
echo "│  Ensure WebAPI is running before connecting from your phone.  │"
echo "│  Press Ctrl+C to close port and restore config files.         │"
echo "└────────────────────────────────────────────────────────────────┘"

# ── Detect WiFi interface ────────────────────────────────────────────────────
WIFI_IF=$(route -n get "$WIFI_IP" 2>/dev/null | awk '/interface:/{print $2}')
if [[ -z "$WIFI_IF" ]]; then
    WIFI_IF="en0"  # fallback
fi

# ── Start live traffic capture ───────────────────────────────────────────────
echo ""
echo "── Live traffic on port $PORT (interface: $WIFI_IF) ─────────────────────"
echo "   Format: TIME  SRC_IP.PORT > DST_IP.PORT  [TCP flags]  bytes"
echo "───────────────────────────────────────────────────────────────────────"
sudo tcpdump -i "$WIFI_IF" -n -l -q "tcp port $PORT" 2>/dev/null &
TCPDUMP_PID=$!

# ── Block until Ctrl+C ───────────────────────────────────────────────────────
while true; do
    sleep 1
done
