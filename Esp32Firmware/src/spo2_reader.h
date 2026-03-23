#pragma once
#include <Arduino.h>
#include <Wire.h>
#include <MAX30105.h>
#include <spo2_algorithm.h>

// ─── SpO2 / HR Reader (MAX30102) ─────────────────────────────────────────────
// Reads SpO2 (%) and heart rate (bpm) from the MAX30102 via I2C.
// Updated every READ_INTERVAL_MS milliseconds (non-blocking poll).

static const int SPO2_SDA_PIN = 21;
static const int SPO2_SCL_PIN = 22;
static const int SPO2_INT_PIN = 23;
static const unsigned long SPO2_READ_INTERVAL_MS = 1500;

struct SpO2Reader {
    MAX30105 sensor;
    float    spO2      = 0.f;
    int      heartRate = 0;
    bool     valid     = false;

    unsigned long _lastRead = 0;

    // Buffers for the SparkFun algorithm
    static const int BUFFER_LEN = 100;
    uint32_t irBuffer[BUFFER_LEN];
    uint32_t redBuffer[BUFFER_LEN];
    int32_t  spo2Raw    = 0;
    int8_t   spo2Valid  = 0;
    int32_t  hrRaw      = 0;
    int8_t   hrValid    = 0;

    bool begin() {
        Wire.begin(SPO2_SDA_PIN, SPO2_SCL_PIN);
        pinMode(SPO2_INT_PIN, INPUT);

        if (!sensor.begin(Wire, I2C_SPEED_FAST)) {
            Serial.println("[SpO2] MAX30102 not found.");
            return false;
        }
        sensor.setup();
        sensor.setPulseAmplitudeRed(0x0A);
        sensor.setPulseAmplitudeGreen(0);
        return true;
    }

    // Call from loop(); updates spO2/heartRate at most once per SPO2_READ_INTERVAL_MS.
    void poll() {
        unsigned long now = millis();
        if (now - _lastRead < SPO2_READ_INTERVAL_MS) return;
        _lastRead = now;

        // Collect 100 samples for the algorithm
        for (int i = 0; i < BUFFER_LEN; i++) {
            while (!sensor.available()) sensor.check();
            redBuffer[i] = sensor.getRed();
            irBuffer[i]  = sensor.getIR();
            sensor.nextSample();
        }

        maxim_heart_rate_and_oxygen_saturation(
            irBuffer, BUFFER_LEN, redBuffer,
            &spo2Raw, &spo2Valid, &hrRaw, &hrValid);

        if (spo2Valid) {
            spO2      = (float)spo2Raw;
            heartRate = (int)hrRaw;
            valid     = true;
        }
    }
};
