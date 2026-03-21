#include <Arduino.h>
#include <WiFi.h>

#include "secrets.h"

#include "ecg_sampler.h"
#include "spo2_reader.h"
#include "ecg_server.h"

// ─── WiFi credentials (override via build_flags or a local secrets.h) ────────
#ifndef WIFI_SSID
#define WIFI_SSID "YourSSID"
#endif
#ifndef WIFI_PASS
#define WIFI_PASS "YourPassword"
#endif

// ─── Globals ─────────────────────────────────────────────────────────────────
static EcgSampler  ecgSampler;
static SpO2Reader  spO2Reader;
static EcgServer   ecgServer;
static const bool SERIAL_PLOTTER_ENABLED = true;

// Hardware timer for 500 Hz ECG sampling
static hw_timer_t* sampleTimer = nullptr;
static volatile uint32_t pendingSampleTicks = 0;
static portMUX_TYPE sampleTickMux = portMUX_INITIALIZER_UNLOCKED;

void IRAM_ATTR onSampleTimer() {
    portENTER_CRITICAL_ISR(&sampleTickMux);
    pendingSampleTicks++;
    portEXIT_CRITICAL_ISR(&sampleTickMux);
}

static void writeSerialPlotterSample(int16_t sample) {
    if (!SERIAL_PLOTTER_ENABLED) return;

    if (Serial.availableForWrite() >= 8) {
        Serial.println(sample);
    }
}

// ─── Setup ───────────────────────────────────────────────────────────────────
void setup() {
    Serial.begin(115200);

    // Connect to WiFi
    WiFi.begin(WIFI_SSID, WIFI_PASS);
    Serial.print("[WiFi] Connecting");
    while (WiFi.status() != WL_CONNECTED) {
        delay(500);
        Serial.print('.');
    }
    Serial.printf("\n[WiFi] Connected — IP: %s\n", WiFi.localIP().toString().c_str());

    // Initialise peripherals
    ecgSampler.begin();

    if (!spO2Reader.begin()) {
        Serial.println("[Setup] SpO2 sensor unavailable — HR/SpO2 will be 0.");
    }

    // Start WebSocket server
    ecgServer.begin();

    // Hardware timer: tick every 2000 µs → 500 Hz
    sampleTimer = timerBegin(0, 80, true); // prescaler 80 → 1 µs tick
    timerAttachInterrupt(sampleTimer, &onSampleTimer, true);
    timerAlarmWrite(sampleTimer, 2000, true); // 2000 µs = 500 Hz
    timerAlarmEnable(sampleTimer);

    Serial.println("[Setup] Ready. Connect MAUI app to ws://" + WiFi.localIP().toString() + ":" + String(WS_PORT));
}

// ─── Loop ────────────────────────────────────────────────────────────────────
void loop() {
    ecgServer.loop();
    spO2Reader.poll();

    uint32_t ticksToProcess = 0;
    portENTER_CRITICAL(&sampleTickMux);
    ticksToProcess = pendingSampleTicks;
    pendingSampleTicks = 0;
    portEXIT_CRITICAL(&sampleTickMux);

    for (uint32_t i = 0; i < ticksToProcess; i++) {
        ecgSampler.sample();
        writeSerialPlotterSample(ecgSampler.lastSample);
    }

    if (ecgSampler.takeBuffer()) {
        ecgServer.sendFrame(
            ecgSampler.buffer,
            BUFFER_SIZE,
            spO2Reader.spO2,
            spO2Reader.heartRate);
    }
}
