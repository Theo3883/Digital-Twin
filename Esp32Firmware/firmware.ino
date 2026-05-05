/*
 * ESP32 BLE 12-Lead — Real PTB-XL Replay
 * ----------------------------------------
 * 10 verified recordings per disease mode from PROGMEM.
 * Picks a random recording each time a 10s loop completes.
 *
 * Serial commands: '0'-'6' to switch mode
 *   0=Normal  1=Tachy  2=Brady  3=STEMI  4=AFib  5=PVC  6=LongQT(=Normal)
 *
 * BLE packet: 5 timesteps × 12 leads × uint16 LE = 120 bytes
 */

#include <NimBLEDevice.h>
#include <math.h>
#include "esp32_real_ecg.h"

// ── BLE UUIDs ──────────────────────────────────────────────
#define ECG_SERVICE_UUID    "4fafc201-1fb5-459e-8fcc-c5c9c331914b"
#define ECG_CHAR_UUID       "beb5483e-36e1-4688-b7f5-ea07361b26a8"
#define VITALS_SERVICE_UUID "6e400001-b5a3-f393-e0a9-e50e24dcca9e"
#define HR_CHAR_UUID        "6e400002-b5a3-f393-e0a9-e50e24dcca9e"
#define SPO2_CHAR_UUID      "6e400003-b5a3-f393-e0a9-e50e24dcca9e"

// ── Config ─────────────────────────────────────────────────
#define ECG_SAMPLE_RATE  100
#define NUM_LEADS        12
#define SAMPLES_PER_PKT  5
#define PKT_SIZE         (SAMPLES_PER_PKT * NUM_LEADS * 2)
#define ECG_INTERVAL_US  (1000000 / ECG_SAMPLE_RATE)
#define ECG_ADC_BASELINE 2048
#define ECG_ADC_SCALE    800
#define VITALS_UPDATE_MS 1000
#define BASE_HR          72.0f
#define BASE_SPO2        97.5f

// ── Globals ────────────────────────────────────────────────
NimBLEServer*          pServer = nullptr;
NimBLECharacteristic*  pEcgChar = nullptr;
NimBLECharacteristic*  pHrChar = nullptr;
NimBLECharacteristic*  pSpO2Char = nullptr;

bool     deviceConnected = false, oldDeviceConnected = false;
uint32_t lastEcgMicros = 0, lastVitalsMs = 0;
float    currentHR = BASE_HR, currentSpO2 = BASE_SPO2;
float    hrTrend = 0.0f, spo2Trend = 0.0f;

volatile int diseaseMode = 0;
int playbackIndex = 0;
int currentSet = 0;  // which of the 10 recordings is active

// ── BLE Callbacks ──────────────────────────────────────────
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

// ── Read sample from PROGMEM ───────────────────────────────
int16_t readSample(int mode, int set, int t, int lead) {
  switch (mode) {
    case 0: return (int16_t)pgm_read_word(&realEcg_0[set][t][lead]);
    case 1: return (int16_t)pgm_read_word(&realEcg_1[set][t][lead]);
    case 2: return (int16_t)pgm_read_word(&realEcg_2[set][t][lead]);
    case 3: return (int16_t)pgm_read_word(&realEcg_3[set][t][lead]);
    case 4: return (int16_t)pgm_read_word(&realEcg_4[set][t][lead]);
    case 5: return (int16_t)pgm_read_word(&realEcg_5[set][t][lead]);
    default: return (int16_t)pgm_read_word(&realEcg_0[set][t][lead]);
  }
}

uint16_t generateSample(int lead) {
  int mode = diseaseMode > 5 ? 0 : diseaseMode;
  int maxSets = realEcgCounts[mode];
  int set = currentSet % maxSets;
  int t = playbackIndex % REAL_ECG_LEN;

  int16_t mv1000 = readSample(mode, set, t, lead);
  float mv = mv1000 / 1000.0f;
  int16_t adc = (int16_t)(ECG_ADC_BASELINE + mv * ECG_ADC_SCALE);
  return (uint16_t)constrain(adc, 0, 4095);
}

// ── Mode switching ─────────────────────────────────────────
void switchMode(int newMode) {
  diseaseMode = newMode;
  playbackIndex = 0;
  int mode = newMode > 5 ? 0 : newMode;
  int maxSets = realEcgCounts[mode];
  currentSet = random(0, maxSets);

  const char* names[] = {"Normal","Tachycardia","Bradycardia","STEMI","AFib","PVC","LongQT"};
  Serial.printf(">> Mode %d: %s (set %d/%d)\n", newMode, names[newMode], currentSet, maxSets);
}

void checkSerial() {
  if (Serial.available()) {
    char c = Serial.read();
    if (c >= '0' && c <= '6') {
      switchMode(c - '0');
    }
  }
}

// ── HR & SpO2 ──────────────────────────────────────────────
void updateVitals() {
  static float tp = 0;
  tp += 2.0f * PI * 0.005f;
  if (tp > 2.0f * PI) tp -= 2.0f * PI;
  hrTrend   = 4.0f * sinf(tp);
  spo2Trend = 0.5f * sinf(tp + 1.0f);
  float hrN = ((float)random(-100, 101) / 100.0f) * 1.5f;
  float spN = ((float)random(-100, 101) / 100.0f) * 0.3f;

  switch (diseaseMode) {
    case 1: currentHR = constrain(130.0f + hrN, 120.0f, 145.0f); break;
    case 2: currentHR = constrain(46.0f + hrN, 38.0f, 55.0f); break;
    case 4: currentHR = constrain(125.0f + hrTrend + (hrN * 6.0f), 95.0f, 165.0f); break;
    default: currentHR = constrain(BASE_HR + hrTrend + hrN, 45.0f, 150.0f); break;
  }
  currentSpO2 = constrain(BASE_SPO2 + spo2Trend + spN, 90.0f, 100.0f);
}

// ── Setup ──────────────────────────────────────────────────
void setup() {
  Serial.begin(115200);
  Serial.println("\n========================================");
  Serial.println("  ESP32 12-Lead ECG — PTB-XL Replay");
  Serial.println("========================================");
  Serial.println("Recordings per mode:");
  const char* names[] = {"Normal","Tachycardia","Bradycardia","STEMI","AFib","PVC"};
  for (int i = 0; i < 6; i++) {
    Serial.printf("  %d = %-13s : %d sets\n", i, names[i], realEcgCounts[i]);
  }
  Serial.println("  6 = LongQT        : uses Normal");
  Serial.println("Send '0'-'6' via Serial to switch mode");
  Serial.println("========================================\n");

  randomSeed(analogRead(0));
  switchMode(0);

  NimBLEDevice::init("DigitalTwin-ESP32");
  NimBLEDevice::setPower(ESP_PWR_LVL_P9);
  NimBLEDevice::setMTU(185);

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
  Serial.println("📡 Advertising...\n");

  lastEcgMicros = micros();
  lastVitalsMs  = millis();
}

// ── Main Loop ──────────────────────────────────────────────
void loop() {
  if (!deviceConnected && oldDeviceConnected) {
    delay(500);
    NimBLEDevice::getAdvertising()->start();
    Serial.println("📡 Re-advertising...");
  }
  oldDeviceConnected = deviceConnected;
  if (!deviceConnected) { delay(200); return; }

  checkSerial();

  // ── ECG: 5 timesteps × 12 leads per packet ──
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

      // Serial plotter: Lead II
      uint16_t leadII = (uint16_t)(pkt[(t * NUM_LEADS + 1) * 2])
                       | ((uint16_t)(pkt[(t * NUM_LEADS + 1) * 2 + 1]) << 8);
      Serial.println((int)leadII - ECG_ADC_BASELINE);

      playbackIndex++;

      // Pick new random recording every 10s loop
      if (playbackIndex % REAL_ECG_LEN == 0) {
        int mode = diseaseMode > 5 ? 0 : diseaseMode;
        int maxSets = realEcgCounts[mode];
        currentSet = random(0, maxSets);
      }
    }

    pEcgChar->setValue(pkt, PKT_SIZE);
    pEcgChar->notify();
  }

  // ── HR & SpO2: 1 Hz ──
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