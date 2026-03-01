#pragma once
#include <Arduino.h>
#include <WebSocketsServer.h>
#include <ArduinoJson.h>

// ─── ECG WebSocket Server ─────────────────────────────────────────────────────
// Listens on WS_PORT; when the MAUI client connects, pushes ECG frames as JSON:
//   { "ecg": [int16...], "spo2": float, "hr": int, "ts": long }
//
// Multiple clients are supported but the typical use-case is one MAUI phone.

static const uint16_t WS_PORT = 8080;

struct EcgServer {
    WebSocketsServer ws{WS_PORT};
    bool clientConnected = false;

    void begin() {
        ws.begin();
        ws.onEvent([this](uint8_t num, WStype_t type, uint8_t* payload, size_t len) {
            onWsEvent(num, type, payload, len);
        });
        Serial.printf("[WS] Server started on port %d\n", WS_PORT);
    }

    void loop() {
        ws.loop();
    }

    // Send a complete ECG frame to all connected clients.
    void sendFrame(const int16_t* samples, int sampleCount, float spO2, int hr) {
        if (!clientConnected) return;

        JsonDocument doc;
        JsonArray ecg = doc["ecg"].to<JsonArray>();
        for (int i = 0; i < sampleCount; i++) ecg.add(samples[i]);
        doc["spo2"] = spO2;
        doc["hr"]   = hr;
        doc["ts"]   = (long long)millis();

        String json;
        serializeJson(doc, json);
        ws.broadcastTXT(json);
    }

private:
    void onWsEvent(uint8_t num, WStype_t type, uint8_t* payload, size_t len) {
        (void)payload; (void)len;
        switch (type) {
            case WStype_CONNECTED:
                clientConnected = true;
                Serial.printf("[WS] Client #%d connected\n", num);
                break;
            case WStype_DISCONNECTED:
                // Only mark disconnected when no other client remains
                if (ws.connectedClients() == 0) clientConnected = false;
                Serial.printf("[WS] Client #%d disconnected\n", num);
                break;
            default:
                break;
        }
    }
};
