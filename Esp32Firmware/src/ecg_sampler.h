#pragma once
#include <Arduino.h>

// ─── ECG Sampler ─────────────────────────────────────────────────────────────
// Reads AD8232 ECG signal at 500 Hz via ADC on pin 34.
// Applies a 50 Hz IIR notch filter to remove mains interference.
// Fills a 1-second buffer (BUFFER_SIZE samples) ready for transmission.

static const int ECG_PIN        = 34;
static const int SAMPLE_RATE_HZ = 500;
static const int BUFFER_SIZE    = SAMPLE_RATE_HZ; // 500 samples = 1 second

// Notch filter state (50 Hz, fs = 500 Hz)
// Coefficients generated for a 2nd-order IIR notch (Q=35):
//   b = [1, -2*cos(2π*50/500), 1] normalised
static const float NOTCH_B0 =  0.9787f;
static const float NOTCH_B1 = -1.6180f; // -2*cos(2π/10)
static const float NOTCH_B2 =  0.9787f;
static const float NOTCH_A1 = -1.6180f;
static const float NOTCH_A2 =  0.9574f;

struct EcgSampler {
    int16_t buffer[BUFFER_SIZE];
    int     bufIndex    = 0;
    bool    bufferReady = false;

    // IIR filter delay lines
    float x1 = 0.f, x2 = 0.f;
    float y1 = 0.f, y2 = 0.f;

    void begin() {
        pinMode(ECG_PIN, INPUT);
    }

    // Single ECG sample + filter step.
    // Call from normal task context (not ISR).
    void sample() {
        float raw = (float)analogRead(ECG_PIN);
        float filtered = NOTCH_B0 * raw + NOTCH_B1 * x1 + NOTCH_B2 * x2
                       - NOTCH_A1 * y1 - NOTCH_A2 * y2;
        x2 = x1; x1 = raw;
        y2 = y1; y1 = filtered;

        buffer[bufIndex++] = (int16_t)constrain((int)filtered, -32768, 32767);

        if (bufIndex >= BUFFER_SIZE) {
            bufIndex    = 0;
            bufferReady = true;
        }
    }

    // Backward-compatible name; avoid calling from ISR.
    void sampleISR() {
        sample();
    }

    // Returns true and resets the flag when a full buffer is available.
    bool takeBuffer() {
        if (!bufferReady) return false;
        bufferReady = false;
        return true;
    }
};
