/*
 * ESP32 BLE 12-Lead Mock Sensor
 * ---------------------------------------------------------
 * Generates realistic 12-lead ECG (I,II,III,aVR,aVL,aVF,V1-V6)
 * plus HR and SpO2 over BLE using NimBLE.
 *
 * ECG packet: 5 timesteps x 12 leads x uint16 = 120 bytes
 * Requires MTU > 123 (iOS auto-negotiates 185-512)
 *
 * Disease modes (send '0'-'6' via Serial):
 *   0=Normal  1=Tachy  2=Brady  3=STEMI
 *   4=AFib    5=PVC    6=LongQT
 */

#include <NimBLEDevice.h>
#include <math.h>

// ===========================================================
//  BLE UUIDs
// ===========================================================
#define ECG_SERVICE_UUID   "4fafc201-1fb5-459e-8fcc-c5c9c331914b"
#define ECG_CHAR_UUID      "beb5483e-36e1-4688-b7f5-ea07361b26a8"
#define VITALS_SERVICE_UUID "6e400001-b5a3-f393-e0a9-e50e24dcca9e"
#define HR_CHAR_UUID       "6e400002-b5a3-f393-e0a9-e50e24dcca9e"
#define SPO2_CHAR_UUID     "6e400003-b5a3-f393-e0a9-e50e24dcca9e"

// ===========================================================
//  Configuration
// ===========================================================
#define ECG_SAMPLE_RATE    100
#define NUM_LEADS          12
#define SAMPLES_PER_PKT    5
#define PKT_SIZE           (SAMPLES_PER_PKT * NUM_LEADS * 2) // 120 bytes
#define ECG_INTERVAL_US    (1000000 / ECG_SAMPLE_RATE)
#define ECG_ADC_BASELINE   2048
#define ECG_ADC_SCALE      800
#define MAX_WAVES          7
#define VITALS_UPDATE_MS   1000
#define BASE_HR            72.0f
#define BASE_SPO2          97.5f

// Lead indices
enum Lead { I=0,II,III,aVR,aVL,aVF,V1,V2,V3,V4,V5,V6 };

// ===========================================================
//  Globals
// ===========================================================
NimBLEServer*         pServer   = nullptr;
NimBLECharacteristic* pEcgChar  = nullptr;
NimBLECharacteristic* pHrChar   = nullptr;
NimBLECharacteristic* pSpO2Char = nullptr;

bool deviceConnected = false, oldDeviceConnected = false;
float ecgPhase = 0.0f, ecgBeatDuration = 60.0f / BASE_HR;
uint32_t lastEcgMicros = 0, lastVitalsMs = 0;
float currentHR = BASE_HR, currentSpO2 = BASE_SPO2;
float hrTrend = 0.0f, spo2Trend = 0.0f;
volatile int diseaseMode = 0;
int pvcCounter = 0;

// ===========================================================
//  BLE Callbacks
// ===========================================================
class ServerCB : public NimBLEServerCallbacks {
    void onConnect(NimBLEServer* s, NimBLEConnInfo& ci) override {
        deviceConnected = true;
        Serial.println(">> Connected");
    }
    void onDisconnect(NimBLEServer* s, NimBLEConnInfo& ci, int r) override {
        deviceConnected = false;
        Serial.println(">> Disconnected");
    }
};

// ===========================================================
//  12-Lead Wave Tables
// ===========================================================
//
// Timing: {center, width} — shared across all leads per mode
// Amplitudes: [12 leads][MAX_WAVES] — lead-specific
//
// Lead order: I, II, III, aVR, aVL, aVF, V1, V2, V3, V4, V5, V6
// Wave order: P, Q, R, S, T, [extra1], [extra2]
// Unused waves padded with 0.0 amplitude

// --- Normal Sinus Rhythm ---
static const float normTime[MAX_WAVES][2] = {
    {0.12f,0.035f}, {0.21f,0.007f}, {0.24f,0.010f},
    {0.27f,0.008f}, {0.42f,0.050f}, {0,0.001f}, {0,0.001f}
};
static const float normAmp[NUM_LEADS][MAX_WAVES] = {
//      P      Q      R      S      T     x1    x2
    { 0.08,-0.04, 0.70,-0.08, 0.20, 0, 0},  // I
    { 0.11,-0.06, 1.00,-0.15, 0.28, 0, 0},  // II
    { 0.03,-0.02, 0.35,-0.10, 0.10, 0, 0},  // III
    {-0.08, 0.02,-0.15,-0.65,-0.18, 0, 0},  // aVR
    { 0.04,-0.03, 0.35,-0.05, 0.10, 0, 0},  // aVL
    { 0.08,-0.04, 0.70,-0.12, 0.20, 0, 0},  // aVF
    { 0.06, 0.00, 0.12,-0.75,-0.12, 0, 0},  // V1 (rS)
    { 0.08, 0.00, 0.25,-0.55, 0.30, 0, 0},  // V2
    { 0.07,-0.02, 0.50,-0.35, 0.25, 0, 0},  // V3
    { 0.06,-0.04, 0.85,-0.18, 0.22, 0, 0},  // V4
    { 0.06,-0.04, 0.72,-0.08, 0.18, 0, 0},  // V5
    { 0.05,-0.03, 0.55,-0.04, 0.15, 0, 0},  // V6
};

// --- Anterior STEMI (ST elev in V1-V4, reciprocal in II/III/aVF) ---
static const float stemiTime[MAX_WAVES][2] = {
    {0.12f,0.035f}, {0.21f,0.007f}, {0.24f,0.010f},
    {0.27f,0.008f}, {0.32f,0.030f}, {0.42f,0.055f}, {0,0.001f}
};
static const float stemiAmp[NUM_LEADS][MAX_WAVES] = {
//      P      Q      R      S    ST-elev  T-hyper  x
    { 0.08,-0.04, 0.70,-0.06, 0.15, 0.25, 0},  // I
    { 0.11,-0.06, 1.00,-0.15,-0.10,-0.15, 0},  // II  reciprocal
    { 0.03,-0.02, 0.35,-0.10,-0.08,-0.10, 0},  // III reciprocal
    {-0.08, 0.02,-0.15,-0.65, 0.12, 0.18, 0},  // aVR ST elev
    { 0.04,-0.03, 0.35,-0.05, 0.10, 0.20, 0},  // aVL
    { 0.08,-0.04, 0.70,-0.12,-0.08,-0.12, 0},  // aVF reciprocal
    { 0.06, 0.00, 0.12,-0.75, 0.30, 0.40, 0},  // V1 ST elev
    { 0.08, 0.00, 0.25,-0.55, 0.40, 0.50, 0},  // V2 ST elev max
    { 0.07,-0.02, 0.50,-0.35, 0.35, 0.45, 0},  // V3 ST elev
    { 0.06,-0.04, 0.85,-0.18, 0.25, 0.35, 0},  // V4 ST elev
    { 0.06,-0.04, 0.72,-0.08, 0.08, 0.20, 0},  // V5 mild
    { 0.05,-0.03, 0.55,-0.04, 0.05, 0.16, 0},  // V6 mild
};

// --- PVC beat (wide QRS, no P, inverted T) ---
static const float pvcTime[MAX_WAVES][2] = {
    {0,0.001f}, {0.20f,0.020f}, {0.26f,0.025f},
    {0.33f,0.018f}, {0.50f,0.060f}, {0,0.001f}, {0,0.001f}
};
static const float pvcAmp[NUM_LEADS][MAX_WAVES] = {
//    (noP)  Q-wide  R-wide  S-deep  T-inv  x     x
    { 0,-0.10, 0.90,-0.30,-0.25, 0, 0},  // I
    { 0,-0.15, 1.20,-0.40,-0.35, 0, 0},  // II
    { 0,-0.08, 0.50,-0.20,-0.15, 0, 0},  // III
    { 0, 0.10,-0.80, 0.30, 0.25, 0, 0},  // aVR
    { 0,-0.08, 0.50,-0.15,-0.15, 0, 0},  // aVL
    { 0,-0.12, 0.90,-0.35,-0.28, 0, 0},  // aVF
    { 0, 0.10,-0.20,-1.00, 0.20, 0, 0},  // V1 deep neg
    { 0, 0.05,-0.10,-0.80, 0.25, 0, 0},  // V2
    { 0,-0.05, 0.40,-0.50,-0.20, 0, 0},  // V3
    { 0,-0.10, 1.00,-0.25,-0.30, 0, 0},  // V4
    { 0,-0.12, 0.90,-0.15,-0.25, 0, 0},  // V5
    { 0,-0.10, 0.70,-0.08,-0.20, 0, 0},  // V6
};

// --- Long QT (T wave pushed late + widened) ---
static const float lqtTime[MAX_WAVES][2] = {
    {0.12f,0.035f}, {0.21f,0.007f}, {0.24f,0.010f},
    {0.27f,0.008f}, {0.55f,0.070f}, {0,0.001f}, {0,0.001f}
};
// Long QT uses normAmp (same amplitudes, just timing differs)

// ===========================================================
//  Waveform Generator (12-lead aware)
// ===========================================================

float computeWave(float phase, const float timing[][2],
                  const float amp[], int numWaves) {
    float v = 0.0f;
    for (int i = 0; i < numWaves; i++) {
        if (amp[i] == 0.0f) continue;
        float d = phase - timing[i][0];
        if (d >  0.5f) d -= 1.0f;
        if (d < -0.5f) d += 1.0f;
        float s = timing[i][1];
        v += amp[i] * expf(-(d * d) / (2.0f * s * s));
    }
    return v;
}

uint16_t generateSample(int lead) {
    float ecg = 0.0f;

    switch (diseaseMode) {
        case 0: case 1: case 2:  // Normal / Tachy / Brady
            ecg = computeWave(ecgPhase, normTime, normAmp[lead], MAX_WAVES);
            break;
        case 3:  // STEMI
            ecg = computeWave(ecgPhase, stemiTime, stemiAmp[lead], MAX_WAVES);
            break;
        case 4:  // AF: skip P wave (index 0), add fibrillation
            ecg = computeWave(ecgPhase, normTime, normAmp[lead], MAX_WAVES);
            ecg -= normAmp[lead][0] * expf(
                -((ecgPhase - normTime[0][0]) * (ecgPhase - normTime[0][0]))
                / (2.0f * normTime[0][1] * normTime[0][1]));
            ecg += 0.03f * sinf(ecgPhase * 47.0f)
                 + 0.02f * sinf(ecgPhase * 73.0f);
            break;
        case 5:  // PVC every ~8 beats
            if (pvcCounter % 8 == 7)
                ecg = computeWave(ecgPhase, pvcTime, pvcAmp[lead], MAX_WAVES);
            else
                ecg = computeWave(ecgPhase, normTime, normAmp[lead], MAX_WAVES);
            break;
        case 6:  // Long QT
            ecg = computeWave(ecgPhase, lqtTime, normAmp[lead], MAX_WAVES);
            break;
        default:
            ecg = computeWave(ecgPhase, normTime, normAmp[lead], MAX_WAVES);
    }

    int16_t adc = (int16_t)(ECG_ADC_BASELINE + ecg * ECG_ADC_SCALE);
    return (uint16_t)constrain(adc, 0, 4095);
}

// Advance phase (call once per time step, after generating all 12 leads)
void advancePhase() {
    float delta = 1.0f / (ecgBeatDuration * ECG_SAMPLE_RATE);
    ecgPhase += delta;
    if (ecgPhase >= 1.0f) {
        ecgPhase -= 1.0f;
        bool wasPvc = (diseaseMode == 5 && pvcCounter % 8 == 7);
        pvcCounter++;
        switch (diseaseMode) {
            case 1: ecgBeatDuration = 60.0f / (float)random(120, 141); break;
            case 2: ecgBeatDuration = 60.0f / (float)random(40, 51); break;
            case 4: ecgBeatDuration = 60.0f / (float)random(100, 161); break;
            case 5:
                if (wasPvc) ecgBeatDuration = 60.0f / currentHR * 0.7f;
                else if (pvcCounter % 8 == 0) ecgBeatDuration = 60.0f / currentHR * 1.3f;
                else ecgBeatDuration = 60.0f / currentHR;
                break;
            default: ecgBeatDuration = 60.0f / currentHR; break;
        }
    }
}

void checkSerial() {
    if (Serial.available()) {
        char c = Serial.read();
        if (c >= '0' && c <= '6') { diseaseMode = c - '0'; pvcCounter = 0; }
    }
}

// ===========================================================
//  HR & SpO2
// ===========================================================
void updateVitals() {
    static float tp = 0;
    tp += 2.0f * PI * 0.005f;
    if (tp > 2.0f * PI) tp -= 2.0f * PI;
    hrTrend = 4.0f * sinf(tp);
    spo2Trend = 0.5f * sinf(tp + 1.0f);
    float hrN = ((float)random(-100, 101) / 100.0f) * 1.5f;
    float spN = ((float)random(-100, 101) / 100.0f) * 0.3f;
    currentHR = constrain(BASE_HR + hrTrend + hrN, 45.0f, 150.0f);
    currentSpO2 = constrain(BASE_SPO2 + spo2Trend + spN, 90.0f, 100.0f);
}

// ===========================================================
//  Setup
// ===========================================================
void setup() {
    Serial.begin(115200);
    Serial.println("-- ESP32 12-Lead Mock --");
    randomSeed(analogRead(0));

    NimBLEDevice::init("DigitalTwin-ESP32");
    NimBLEDevice::setPower(ESP_PWR_LVL_P9);
    NimBLEDevice::setMTU(185);  // request MTU for 120-byte ECG packets

    pServer = NimBLEDevice::createServer();
    pServer->setCallbacks(new ServerCB());

    NimBLEService* ecgSvc = pServer->createService(ECG_SERVICE_UUID);
    pEcgChar = ecgSvc->createCharacteristic(ECG_CHAR_UUID, NIMBLE_PROPERTY::NOTIFY);
    ecgSvc->start();

    NimBLEService* vitSvc = pServer->createService(VITALS_SERVICE_UUID);
    pHrChar   = vitSvc->createCharacteristic(HR_CHAR_UUID,   NIMBLE_PROPERTY::READ | NIMBLE_PROPERTY::NOTIFY);
    pSpO2Char = vitSvc->createCharacteristic(SPO2_CHAR_UUID, NIMBLE_PROPERTY::READ | NIMBLE_PROPERTY::NOTIFY);
    vitSvc->start();

    NimBLEAdvertising* adv = NimBLEDevice::getAdvertising();
    adv->addServiceUUID(ECG_SERVICE_UUID);
    NimBLEDevice::startAdvertising();

    Serial.println("Advertising... scan with LightBlue");
    Serial.println("Send '0'-'6' to change disease mode");
    lastEcgMicros = micros();
    lastVitalsMs = millis();
}

// ===========================================================
//  Main Loop
// ===========================================================
void loop() {
    if (!deviceConnected && oldDeviceConnected) {
        delay(500);
        NimBLEDevice::getAdvertising()->start();
        Serial.println("Re-advertising...");
    }
    oldDeviceConnected = deviceConnected;

    if (!deviceConnected) { delay(200); return; }
    checkSerial();

    // --- ECG: 5 time steps x 12 leads per packet ---
    uint32_t now = micros();
    if (now - lastEcgMicros >= ECG_INTERVAL_US * SAMPLES_PER_PKT) {
        lastEcgMicros = now;
        uint8_t pkt[PKT_SIZE];
        int idx = 0;

        for (int t = 0; t < SAMPLES_PER_PKT; t++) {
            for (int lead = 0; lead < NUM_LEADS; lead++) {
                uint16_t s = generateSample(lead);
                pkt[idx++] = s & 0xFF;
                pkt[idx++] = (s >> 8) & 0xFF;
            }
            // Print Lead II only for Serial Plotter
            // (12 values per line at 250 Hz would overflow serial)
            uint16_t leadII = (uint16_t)(pkt[(t * NUM_LEADS + 1) * 2])
                            | ((uint16_t)(pkt[(t * NUM_LEADS + 1) * 2 + 1]) << 8);
            Serial.println((int)leadII - ECG_ADC_BASELINE);

            advancePhase();  // advance once per time step
        }

        pEcgChar->setValue(pkt, PKT_SIZE);
        pEcgChar->notify();
    }

    // --- HR & SpO2: 1 Hz ---
    uint32_t ms = millis();
    if (ms - lastVitalsMs >= VITALS_UPDATE_MS) {
        lastVitalsMs = ms;
        updateVitals();

        uint16_t hv = (uint16_t)(currentHR * 10);
        uint8_t hp[2] = {(uint8_t)(hv & 0xFF), (uint8_t)(hv >> 8)};
        pHrChar->setValue(hp, 2);
        pHrChar->notify();

        uint16_t sv = (uint16_t)(currentSpO2 * 10);
        uint8_t sp[2] = {(uint8_t)(sv & 0xFF), (uint8_t)(sv >> 8)};
        pSpO2Char->setValue(sp, 2);
        pSpO2Char->notify();
    }
}
