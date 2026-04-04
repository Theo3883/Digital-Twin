#!/bin/bash
# Opens port 5432 (PostgreSQL) for inbound WiFi connections while this script runs.
# Also patches POSTGRES_HOST in the MAUI env files so the app points to this machine.
# Live traffic on port 5432 is shown via tcpdump.
# When you press Ctrl+C or close the terminal, everything is automatically reverted.

PORT=5432
ANCHOR="digitaltwin_db_temp"
TCPDUMP_PID=""

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_ENV="$SCRIPT_DIR/.env"
BUILD_ENV="$SCRIPT_DIR/DigitalTwin.MAUI/build-secrets.env"

# ── Patch POSTGRES_HOST in one env file ─────────────────────────────────────
patch_host() {
    local file="$1" new_ip="$2"
    if [[ -f "$file" ]]; then
        sed -i '' "s|^POSTGRES_HOST=.*|POSTGRES_HOST=$new_ip|" "$file"
    fi
}

# ── Cleanup: revert env files and close firewall rule ───────────────────────
ORIGINAL_ROOT_HOST=""
ORIGINAL_BUILD_HOST=""

cleanup() {
    echo ""
    echo "Stopping traffic capture..."
    [[ -n "$TCPDUMP_PID" ]] && sudo kill "$TCPDUMP_PID" 2>/dev/null

    echo "Reverting POSTGRES_HOST in env files..."
    [[ -n "$ORIGINAL_ROOT_HOST"  ]] && patch_host "$ROOT_ENV"  "$ORIGINAL_ROOT_HOST"
    [[ -n "$ORIGINAL_BUILD_HOST" ]] && patch_host "$BUILD_ENV" "$ORIGINAL_BUILD_HOST"

    echo "Removing inbound rule for port $PORT..."
    sudo pfctl -a "$ANCHOR" -F all 2>/dev/null
    echo "Port $PORT is CLOSED. Env files restored."
}
trap cleanup EXIT INT TERM HUP

# ── Get WiFi IP ──────────────────────────────────────────────────────────────
WIFI_IP=$(ipconfig getifaddr en0 2>/dev/null || ipconfig getifaddr en1 2>/dev/null)
if [[ -z "$WIFI_IP" ]]; then
    echo "ERROR: Could not find a WiFi IP address. Make sure you are connected to WiFi."
    exit 1
fi

# ── Save originals and patch env files ──────────────────────────────────────
if [[ -f "$ROOT_ENV" ]]; then
    ORIGINAL_ROOT_HOST=$(grep '^POSTGRES_HOST=' "$ROOT_ENV" | cut -d= -f2)
    patch_host "$ROOT_ENV" "$WIFI_IP"
    echo "Updated $ROOT_ENV  → POSTGRES_HOST=$WIFI_IP  (was: $ORIGINAL_ROOT_HOST)"
fi

if [[ -f "$BUILD_ENV" ]]; then
    ORIGINAL_BUILD_HOST=$(grep '^POSTGRES_HOST=' "$BUILD_ENV" | cut -d= -f2)
    patch_host "$BUILD_ENV" "$WIFI_IP"
    echo "Updated $BUILD_ENV → POSTGRES_HOST=$WIFI_IP  (was: $ORIGINAL_BUILD_HOST)"
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
echo "│  Rebuild & redeploy the MAUI app to pick up the new host.     │"
echo "│  Press Ctrl+C to close the port and revert env files.         │"
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
