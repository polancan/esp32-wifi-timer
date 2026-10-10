/*
  ESP32 8-channel Wi-Fi Timer - Arduino IDE firmware v1.3.1

  Hardware:
    ESP32 DevKit / ESP32-WROOM-32
    8x optically isolated inputs (PC817 modules)
    DS3231 RTC
    Wi-Fi connection to TimerServer (ASP.NET Core + SQLite)

  Timing principle:
    - duration is measured locally with esp_timer_get_time() in microseconds
    - RTC/NTP is used only for calendar timestamps
    - network latency never enters the measured duration

  IMPORTANT:
    - GPIO34 and GPIO35 do NOT have internal pull-ups.
    - Use external 10k pull-ups to 3.3 V on ALL 8 lane inputs.
    - The firmware assumes an optocoupler output pulls the GPIO LOW when pressed.
      If your hardware behaves oppositely, set INPUT_ACTIVE_LOW to false.
*/

#include <Arduino.h>
#include <WiFi.h>
#include <HTTPClient.h>
#include <ArduinoOTA.h>
#include <Wire.h>
#include <RTClib.h>
#include <ArduinoJson.h>
#include <Preferences.h>
#include <esp_timer.h>
#include <sys/time.h>

// ArduinoJson 6.x / 7.x compatibility.
// v7 uses JsonDocument with automatic allocation, while v6 requires
// DynamicJsonDocument with an explicit capacity.
#if ARDUINOJSON_VERSION_MAJOR >= 7
  #define TIMER_JSON_DOC(name, capacity) JsonDocument name
#else
  #define TIMER_JSON_DOC(name, capacity) DynamicJsonDocument name(capacity)
#endif

// ============================================================================
// USER CONFIGURATION - EDIT THESE VALUES
// ============================================================================

const char* WIFI_SSID     = "YOUR_WIFI";
const char* WIFI_PASSWORD = "YOUR_PASSWORD";

// IP address of the Windows PC running TimerServer.
// Give the PC a DHCP reservation / fixed LAN address.
const char* API_BASE_URL  = "http://192.168.1.50:8080";

// Must be identical to DeviceApiKey in TimerServer appsettings.json.
const char* DEVICE_API_KEY = "CHANGE_ME_DEVICE_KEY";

// Must match the station used in the web application.
const char* STATION_ID = "TIMER01";

// Optional OTA password. Empty string = OTA without password.
const char* OTA_PASSWORD = "timer-ota";

const char* FIRMWARE_VERSION = "1.3.1";

// Recommended true: prevents creation of unassigned results when no web run
// session is active. Offline timing still works if the ESP already knows the
// active session and Wi-Fi drops afterwards.
const bool REQUIRE_ACTIVE_SESSION = true;

// PC817 output logic. With 10k pull-up to 3.3 V and optocoupler pulling to GND,
// a pressed button is LOW => true.
const bool INPUT_ACTIVE_LOW = true;

// ============================================================================
// FINAL GPIO MAP
// ============================================================================

const uint8_t LANE_PINS[8] = {
  32, // lane 1
  33, // lane 2
  25, // lane 3
  26, // lane 4
  27, // lane 5
  14, // lane 6
  34, // lane 7 - input only
  35  // lane 8 - input only
};

const uint8_t PIN_RTC_SDA   = 21;
const uint8_t PIN_RTC_SCL   = 22;
const uint8_t PIN_LED_WIFI  = 23;
const uint8_t PIN_LED_DATA  = 18;
const uint8_t PIN_SERVICE   = 19;  // momentary switch to GND

// Battery measurement: connect BAT+ -> 100k -> GPIO36 -> 100k -> GND.
// With a 1:2 divider a fully charged 4.2 V cell gives about 2.1 V at ADC.
// GPIO36 is ADC1 and therefore remains usable while Wi-Fi is active.
const uint8_t PIN_BATTERY   = 36;
const float BATTERY_DIVIDER_RATIO = 2.0f;
const float BATTERY_CAL_FACTOR = 1.000f; // fine-tune after multimeter comparison

// ============================================================================
// TUNING
// ============================================================================

const int64_t  DEBOUNCE_US             = 15000LL;          // 15 ms
const uint32_t CONFIG_REFRESH_MS        = 1500UL;           // active web run
const uint32_t QUEUE_FLUSH_MS           = 5000UL;           // retry queued results
const uint32_t WIFI_RECONNECT_MS        = 5000UL;
const uint32_t NTP_RESYNC_MS            = 6UL * 60UL * 60UL * 1000UL;
const uint32_t NTP_RETRY_MS             = 10UL * 60UL * 1000UL; // until first successful NTP
const uint32_t HTTP_TIMEOUT_MS          = 3500UL;
const uint32_t STATUS_PRINT_MS          = 15000UL;
const uint32_t DEVICE_STATUS_MS          = 5000UL;
const char*    NVS_QUEUE_NAMESPACE      = "timerqueue";

RTC_DS3231 rtc;

struct LaneState {
  bool rawLevel = HIGH;
  bool stableLevel = HIGH;

  // Debounce state. firstEdgeUs preserves the first physical transition,
  // while lastRawChangeUs is used only to confirm stable level.
  bool transitionActive = false;
  int64_t firstEdgeUs = 0;
  int64_t lastRawChangeUs = 0;

  bool running = false;
  int64_t startMonoUs = 0;
  int64_t startEpochUs = 0;
  String sessionId;
  String entryId;
  String runnerName;

  // After STOP the lane remains locked until the web operator confirms or
  // rejects the pending result. This prevents runner order from mixing.
  bool waitingDecision = false;
  bool resultDelivered = false;
  String waitingEventId;
};

struct LaneConfig {
  String entryId;
  String runnerName;
  String status;
  int sequence = 0;
  bool canStart = false;
};

LaneState lanes[8];
LaneConfig laneConfig[8];

String activeSessionId;
String activeSessionName;

bool rtcPresent = false;
bool timeValid = false;
bool otaInProgress = false;
Preferences queuePrefs;
bool nvsQueueReady = false;

// Wall-clock anchor. Calendar time for any monotonic timestamp is calculated as:
// epoch = anchorEpochUs + (mono - anchorMonoUs)
int64_t anchorMonoUs = 0;
int64_t anchorEpochUs = 0;

uint32_t lastConfigFetchMs = 0;
uint32_t lastQueueFlushMs = 0;
uint32_t lastWifiReconnectMs = 0;
uint32_t lastNtpSyncMs = 0;
uint32_t lastNtpAttemptMs = 0;
uint32_t lastStatusPrintMs = 0;
uint32_t lastDeviceStatusMs = 0;
bool deviceStatusDirty = true;

// ============================================================================
// UTILITY
// ============================================================================

bool isPressedLevel(bool electricalLevel) {
  return INPUT_ACTIVE_LOW ? (electricalLevel == LOW) : (electricalLevel == HIGH);
}

void flashDataLed(uint8_t count, uint16_t onMs = 60, uint16_t offMs = 80) {
  for (uint8_t i = 0; i < count; ++i) {
    digitalWrite(PIN_LED_DATA, HIGH);
    delay(onMs);
    digitalWrite(PIN_LED_DATA, LOW);
    if (i + 1 < count) delay(offMs);
  }
}

String isoUtcFromEpochUs(int64_t epochUs) {
  if (epochUs < 0) epochUs = 0;

  time_t sec = static_cast<time_t>(epochUs / 1000000LL);
  int ms = static_cast<int>((epochUs % 1000000LL) / 1000LL);

  struct tm tmUtc;
  gmtime_r(&sec, &tmUtc);

  char buf[40];
  snprintf(buf, sizeof(buf),
           "%04d-%02d-%02dT%02d:%02d:%02d.%03dZ",
           tmUtc.tm_year + 1900,
           tmUtc.tm_mon + 1,
           tmUtc.tm_mday,
           tmUtc.tm_hour,
           tmUtc.tm_min,
           tmUtc.tm_sec,
           ms);
  return String(buf);
}

int64_t epochUsForMono(int64_t monoUs) {
  return anchorEpochUs + (monoUs - anchorMonoUs);
}

String makeUuid() {
  uint8_t b[16];
  for (uint8_t &x : b) x = static_cast<uint8_t>(esp_random() & 0xFF);

  // RFC 4122 v4
  b[6] = (b[6] & 0x0F) | 0x40;
  b[8] = (b[8] & 0x3F) | 0x80;

  char out[37];
  snprintf(out, sizeof(out),
           "%02x%02x%02x%02x-%02x%02x-%02x%02x-%02x%02x-%02x%02x%02x%02x%02x%02x",
           b[0], b[1], b[2], b[3], b[4], b[5], b[6], b[7],
           b[8], b[9], b[10], b[11], b[12], b[13], b[14], b[15]);
  return String(out);
}

float readBatteryVoltage() {
  uint32_t sumMv = 0;
  const uint8_t samples = 16;
  for (uint8_t i = 0; i < samples; ++i) {
    sumMv += analogReadMilliVolts(PIN_BATTERY);
  }
  const float adcV = (sumMv / static_cast<float>(samples)) / 1000.0f;
  return adcV * BATTERY_DIVIDER_RATIO * BATTERY_CAL_FACTOR;
}

int batteryPercentFromVoltage(float v) {
  // Approximate Li-ion state-of-charge under a light load. The voltage is
  // intentionally also shown in the web UI because percentage is only an estimate.
  struct Point { float v; int p; };
  static const Point curve[] = {
    {3.30f, 0}, {3.50f, 8}, {3.60f, 18}, {3.70f, 35}, {3.80f, 52},
    {3.90f, 68}, {4.00f, 82}, {4.10f, 93}, {4.20f, 100}
  };
  if (v <= curve[0].v) return 0;
  if (v >= curve[8].v) return 100;
  for (uint8_t i = 1; i < 9; ++i) {
    if (v <= curve[i].v) {
      float t = (v - curve[i-1].v) / (curve[i].v - curve[i-1].v);
      return static_cast<int>(roundf(curve[i-1].p + t * (curve[i].p - curve[i-1].p)));
    }
  }
  return 0;
}

// ============================================================================
// CLOCK / RTC / NTP
// ============================================================================

bool setAnchorFromRtc() {
  if (!rtcPresent) return false;

  DateTime now = rtc.now();
  uint32_t unixSeconds = now.unixtime();

  // Reject obviously invalid RTC values.
  if (unixSeconds < 1700000000UL) {
    Serial.println("[TIME] RTC time is invalid / too old.");
    return false;
  }

  anchorMonoUs = esp_timer_get_time();
  anchorEpochUs = static_cast<int64_t>(unixSeconds) * 1000000LL;
  timeValid = true;

  Serial.printf("[TIME] RTC anchor: %s\n",
                isoUtcFromEpochUs(anchorEpochUs).c_str());
  return true;
}

bool syncNtp(bool updateRtc = true) {
  if (WiFi.status() != WL_CONNECTED) return false;

  lastNtpAttemptMs = millis();
  Serial.println("[NTP] Synchronizing...");
  configTime(0, 0, "pool.ntp.org", "time.cloudflare.com", "time.google.com");

  const uint32_t startMs = millis();
  time_t nowSec = 0;

  while (millis() - startMs < 8000UL) {
    time(&nowSec);
    if (nowSec > 1700000000) break;
    ArduinoOTA.handle();
    delay(100);
  }

  if (nowSec <= 1700000000) {
    Serial.println("[NTP] Sync failed; RTC/fallback remains in use.");
    return false;
  }

  struct timeval tv;
  gettimeofday(&tv, nullptr);

  anchorMonoUs = esp_timer_get_time();
  anchorEpochUs = static_cast<int64_t>(tv.tv_sec) * 1000000LL + tv.tv_usec;
  timeValid = true;
  lastNtpSyncMs = millis();

  if (updateRtc && rtcPresent) {
    rtc.adjust(DateTime(static_cast<uint32_t>(tv.tv_sec)));
  }

  Serial.printf("[NTP] OK: %s\n", isoUtcFromEpochUs(anchorEpochUs).c_str());
  return true;
}

// ============================================================================
// NETWORK / API
// ============================================================================

void setWifiLed() {
  if (WiFi.status() == WL_CONNECTED) {
    digitalWrite(PIN_LED_WIFI, HIGH);
  } else {
    // Blink slowly while disconnected.
    digitalWrite(PIN_LED_WIFI, ((millis() / 500UL) % 2UL) ? HIGH : LOW);
  }
}

void connectWifiInitial() {
  WiFi.mode(WIFI_STA);
  WiFi.setHostname(STATION_ID);
  WiFi.setAutoReconnect(true);
  WiFi.persistent(false);

  Serial.printf("[WIFI] Connecting to %s", WIFI_SSID);
  WiFi.begin(WIFI_SSID, WIFI_PASSWORD);

  uint32_t started = millis();
  while (WiFi.status() != WL_CONNECTED && millis() - started < 15000UL) {
    delay(250);
    Serial.print('.');
  }
  Serial.println();

  if (WiFi.status() == WL_CONNECTED) {
    Serial.printf("[WIFI] Connected, IP=%s RSSI=%d dBm\n",
                  WiFi.localIP().toString().c_str(), WiFi.RSSI());
  } else {
    Serial.println("[WIFI] Not connected. Offline queue remains available.");
  }
}

int postJson(const String &path, const String &json) {
  if (WiFi.status() != WL_CONNECTED || otaInProgress) return -1;

  HTTPClient http;
  http.setTimeout(HTTP_TIMEOUT_MS);
  http.begin(String(API_BASE_URL) + path);
  http.addHeader("Content-Type", "application/json");
  http.addHeader("X-Device-Key", DEVICE_API_KEY);

  int code = http.POST(json);
  if (code < 0) {
    Serial.printf("[HTTP] POST %s failed: %s\n",
                  path.c_str(), http.errorToString(code).c_str());
  }
  http.end();
  return code;
}

void sendDeviceStatus() {
  if (WiFi.status() != WL_CONNECTED || otaInProgress) return;

  uint8_t runningMask = 0;
  uint8_t waitingMask = 0;
  for (uint8_t i = 0; i < 8; ++i) {
    if (lanes[i].running) runningMask |= (1U << i);
    if (lanes[i].waitingDecision) waitingMask |= (1U << i);
  }

  const float batteryV = readBatteryVoltage();
  const int batteryPct = batteryPercentFromVoltage(batteryV);

  TIMER_JSON_DOC(doc, 1024);
  doc["stationId"] = STATION_ID;
  doc["firmwareVersion"] = FIRMWARE_VERSION;
  doc["batteryVoltage"] = roundf(batteryV * 1000.0f) / 1000.0f;
  doc["batteryPercent"] = batteryPct;
  doc["runningMask"] = runningMask;
  doc["waitingMask"] = waitingMask;

  String json;
  serializeJson(doc, json);
  int code = postJson("/api/device/status", json);
  if (code >= 200 && code < 300) {
    deviceStatusDirty = false;
  } else if (code > 0) {
    Serial.printf("[STATUS] Device status POST returned %d\n", code);
  }
}

void fetchActiveSession() {
  if (WiFi.status() != WL_CONNECTED || otaInProgress) return;

  HTTPClient http;
  http.setTimeout(HTTP_TIMEOUT_MS);
  String url = String(API_BASE_URL) + "/api/device/config/" + STATION_ID;
  http.begin(url);
  http.addHeader("X-Device-Key", DEVICE_API_KEY);

  int code = http.GET();
  if (code == 200) {
    String payload = http.getString();
    TIMER_JSON_DOC(doc, 4096);
    DeserializationError err = deserializeJson(doc, payload);

    if (!err) {
      String newId = doc["activeRunSessionId"].isNull()
                       ? String("")
                       : String(doc["activeRunSessionId"].as<const char*>());
      String newName = doc["name"].isNull()
                         ? String("")
                         : String(doc["name"].as<const char*>());

      if (newId != activeSessionId) {
        activeSessionId = newId;
        activeSessionName = newName;
        Serial.printf("[RUN] Active session: %s | %s\n",
                      activeSessionId.length() ? activeSessionId.c_str() : "none",
                      activeSessionName.length() ? activeSessionName.c_str() : "none");
      } else {
        activeSessionName = newName;
      }

      // Server returns the CURRENT person independently for every lane.
      // A lane with Pending status remains blocked until Confirm/Reject on web.
      JsonArray arr = doc["lanes"].as<JsonArray>();
      for (JsonObject item : arr) {
        int laneNo = item["lane"] | 0;
        if (laneNo < 1 || laneNo > 8) continue;
        uint8_t i = static_cast<uint8_t>(laneNo - 1);

        LaneConfig cfg;
        cfg.entryId = item["entryId"].isNull() ? String("") : String(item["entryId"].as<const char*>());
        cfg.runnerName = item["runnerName"].isNull() ? String("") : String(item["runnerName"].as<const char*>());
        cfg.status = item["status"].isNull() ? String("EMPTY") : String(item["status"].as<const char*>());
        cfg.sequence = item["sequence"] | 0;
        cfg.canStart = item["canStart"] | false;
        laneConfig[i] = cfg;

        LaneState &ls = lanes[i];
        // Do not unlock merely because an old cached server state said READY.
        // First the stopped result must have actually reached the server.
        if (ls.waitingDecision && ls.resultDelivered) {
          if (cfg.canStart || cfg.status == "EMPTY") {
            ls.waitingDecision = false;
            ls.resultDelivered = false;
            ls.waitingEventId = "";
            deviceStatusDirty = true;
            Serial.printf("[LANE %u] Web decision complete. Next=%s\n",
                          laneNo,
                          cfg.runnerName.length() ? cfg.runnerName.c_str() : "none");
          }
        }
      }
    } else {
      Serial.printf("[HTTP] Config JSON error: %s\n", err.c_str());
    }
  } else if (code == 401) {
    Serial.println("[HTTP] API key rejected (401). Check DEVICE_API_KEY.");
  } else if (code > 0) {
    Serial.printf("[HTTP] Config GET returned %d\n", code);
  }

  http.end();
}

// ============================================================================
// OFFLINE RESULT QUEUE - Preferences / NVS
// ============================================================================

// Each lane can have at most one unsent result because a lane is locked after
// STOP until that result reaches the server and the operator confirms/rejects it.
// Therefore NVS uses one durable slot per lane instead of a filesystem queue.

String queueKeyForLane(uint8_t laneNo) {
  char key[8];
  snprintf(key, sizeof(key), "lane%u", laneNo);
  return String(key);
}

bool queueResult(const String &json) {
  if (!nvsQueueReady) {
    Serial.println("[QUEUE] ERROR: NVS unavailable; result cannot be queued.");
    return false;
  }

  TIMER_JSON_DOC(doc, 1536);
  DeserializationError err = deserializeJson(doc, json);
  if (err) {
    Serial.printf("[QUEUE] ERROR: cannot parse result JSON: %s\n", err.c_str());
    return false;
  }

  int laneNo = doc["lane"] | 0;
  if (laneNo < 1 || laneNo > 8) {
    Serial.println("[QUEUE] ERROR: result contains invalid lane number.");
    return false;
  }

  String key = queueKeyForLane(static_cast<uint8_t>(laneNo));
  size_t written = queuePrefs.putString(key.c_str(), json);
  if (written == 0) {
    Serial.printf("[QUEUE] ERROR: NVS write failed for lane %d.\n", laneNo);
    return false;
  }

  Serial.printf("[QUEUE] Result stored in NVS for lane %d.\n", laneNo);
  return true;
}

uint32_t countQueuedResults() {
  if (!nvsQueueReady) return 0;

  uint32_t count = 0;
  for (uint8_t laneNo = 1; laneNo <= 8; ++laneNo) {
    String key = queueKeyForLane(laneNo);
    if (queuePrefs.isKey(key.c_str()) && queuePrefs.getString(key.c_str(), "").length() > 0) {
      ++count;
    }
  }
  return count;
}

void flushQueue() {
  if (!nvsQueueReady) return;
  if (WiFi.status() != WL_CONNECTED || otaInProgress) return;

  uint32_t sent = 0;
  uint32_t kept = 0;

  for (uint8_t laneNo = 1; laneNo <= 8; ++laneNo) {
    ArduinoOTA.handle();

    String key = queueKeyForLane(laneNo);
    if (!queuePrefs.isKey(key.c_str())) continue;

    String json = queuePrefs.getString(key.c_str(), "");
    json.trim();
    if (!json.length()) {
      queuePrefs.remove(key.c_str());
      continue;
    }

    int code = postJson("/api/results", json);

    // Server returns 2xx both for a newly stored result and an idempotent retry.
    if (code >= 200 && code < 300) {
      queuePrefs.remove(key.c_str());
      ++sent;

      TIMER_JSON_DOC(sentDoc, 1536);
      if (!deserializeJson(sentDoc, json)) {
        String sentEvent = sentDoc["deviceEventId"].isNull()
          ? String("")
          : String(sentDoc["deviceEventId"].as<const char*>());
        for (LaneState &ls : lanes) {
          if (ls.waitingDecision && sentEvent.length() && ls.waitingEventId == sentEvent) {
            ls.resultDelivered = true;
          }
        }
      }
    } else {
      ++kept;
    }

    delay(5);
  }

  if (sent || kept) {
    Serial.printf("[QUEUE] NVS flush: sent=%lu remaining=%lu\n",
                  static_cast<unsigned long>(sent),
                  static_cast<unsigned long>(kept));
  }
}

// ============================================================================
// TIMING
// ============================================================================

void sendTimingResult(uint8_t laneIndex, int64_t stopMonoUs) {
  LaneState &s = lanes[laneIndex];
  const int64_t durationUs = stopMonoUs - s.startMonoUs;
  const int64_t stopEpochUs = s.startEpochUs + durationUs;

  String eventId = makeUuid();
  s.waitingEventId = eventId;
  s.waitingDecision = true;
  s.resultDelivered = false;
  deviceStatusDirty = true;

  TIMER_JSON_DOC(doc, 1536);
  doc["deviceEventId"] = eventId;
  doc["stationId"] = STATION_ID;
  doc["lane"] = laneIndex + 1;
  if (s.sessionId.length()) doc["runSessionId"] = s.sessionId;
  else doc["runSessionId"] = nullptr;
  if (s.entryId.length()) doc["entryId"] = s.entryId;
  else doc["entryId"] = nullptr;
  doc["startedAtUtc"] = isoUtcFromEpochUs(s.startEpochUs);
  doc["stoppedAtUtc"] = isoUtcFromEpochUs(stopEpochUs);
  doc["durationUs"] = durationUs;

  String json;
  serializeJson(doc, json);

  digitalWrite(PIN_LED_DATA, HIGH);
  int code = postJson("/api/results", json);
  digitalWrite(PIN_LED_DATA, LOW);

  if (code >= 200 && code < 300) {
    s.resultDelivered = true;
    Serial.printf("[LANE %u] Result delivered. Waiting for web Confirm/Reject.\n", laneIndex + 1);
  } else {
    Serial.printf("[LANE %u] Server unavailable / rejected (%d); queueing and LOCKING lane.\n",
                  laneIndex + 1, code);
    queueResult(json);
  }
}

void handleLanePress(uint8_t laneIndex, int64_t edgeMonoUs) {
  if (otaInProgress) return;
  LaneState &s = lanes[laneIndex];
  LaneConfig &cfg = laneConfig[laneIndex];

  if (!s.running) {
    if (s.waitingDecision) {
      Serial.printf("[LANE %u] START rejected: previous result waits for web decision.\n", laneIndex + 1);
      flashDataLed(2, 80, 60);
      return;
    }
    if (!timeValid) {
      Serial.printf("[LANE %u] START rejected: no valid RTC/NTP time.\n", laneIndex + 1);
      flashDataLed(3, 40, 50);
      return;
    }
    if (REQUIRE_ACTIVE_SESSION && activeSessionId.length() == 0) {
      Serial.printf("[LANE %u] START rejected: no active web session.\n", laneIndex + 1);
      flashDataLed(2, 100, 80);
      return;
    }
    if (!cfg.canStart || cfg.entryId.length() == 0) {
      Serial.printf("[LANE %u] START rejected: no READY runner (status=%s).\n",
                    laneIndex + 1, cfg.status.c_str());
      flashDataLed(2, 100, 80);
      return;
    }

    s.running = true;
    deviceStatusDirty = true;
    s.startMonoUs = edgeMonoUs;
    s.startEpochUs = epochUsForMono(edgeMonoUs);
    s.sessionId = activeSessionId;       // snapshot at physical START
    s.entryId = cfg.entryId;             // snapshot exact person/order at START
    s.runnerName = cfg.runnerName;

    Serial.printf("[LANE %u] START %s | #%d %s | entry=%s\n",
                  laneIndex + 1,
                  isoUtcFromEpochUs(s.startEpochUs).c_str(),
                  cfg.sequence,
                  s.runnerName.c_str(),
                  s.entryId.c_str());
  } else {
    s.running = false;
    deviceStatusDirty = true;
    const int64_t durationUs = edgeMonoUs - s.startMonoUs;
    if (durationUs <= 0) {
      Serial.printf("[LANE %u] Invalid duration; discarded.\n", laneIndex + 1);
      s.sessionId = ""; s.entryId = ""; s.runnerName = "";
      return;
    }

    Serial.printf("[LANE %u] STOP %.3f s | %s\n",
                  laneIndex + 1,
                  static_cast<double>(durationUs) / 1000000.0,
                  s.runnerName.c_str());

    sendTimingResult(laneIndex, edgeMonoUs);
    // Keep entry identity until server receives and operator decides.
    s.sessionId = "";
    s.entryId = "";
    s.runnerName = "";
  }
}

void scanInputs() {
  const int64_t nowUs = esp_timer_get_time();

  for (uint8_t i = 0; i < 8; ++i) {
    LaneState &s = lanes[i];
    bool rawNow = digitalRead(LANE_PINS[i]);

    if (rawNow != s.rawLevel) {
      s.rawLevel = rawNow;
      s.lastRawChangeUs = nowUs;

      // Capture the FIRST departure from the current stable state.
      if (!s.transitionActive && rawNow != s.stableLevel) {
        s.transitionActive = true;
        s.firstEdgeUs = nowUs;
      }
      // If bouncing returns away from stable again, keep original firstEdgeUs.
    }

    if (!s.transitionActive) continue;

    if (s.rawLevel != s.stableLevel) {
      // New level has remained unchanged long enough -> accept transition.
      if (nowUs - s.lastRawChangeUs >= DEBOUNCE_US) {
        s.stableLevel = s.rawLevel;
        int64_t eventTimestampUs = s.firstEdgeUs;
        s.transitionActive = false;

        if (isPressedLevel(s.stableLevel)) {
          handleLanePress(i, eventTimestampUs);
        }
      }
    } else {
      // Signal returned to the old stable state. If it remains there for the
      // debounce period, treat the excursion as noise and forget it.
      if (nowUs - s.lastRawChangeUs >= DEBOUNCE_US) {
        s.transitionActive = false;
      }
    }
  }
}

void resetRunningLanes() {
  uint8_t count = 0;
  for (LaneState &s : lanes) {
    if (s.running) ++count;
    s.running = false;
    s.sessionId = "";
    s.entryId = "";
    s.runnerName = "";
  }
  deviceStatusDirty = true;
  Serial.printf("[SERVICE] Reset running lanes: %u\n", count);
  flashDataLed(1, 150, 0);
}

// ============================================================================
// OTA
// ============================================================================

void setupOta() {
  ArduinoOTA.setHostname(STATION_ID);
  if (strlen(OTA_PASSWORD) > 0) {
    ArduinoOTA.setPassword(OTA_PASSWORD);
  }

  ArduinoOTA.onStart([]() {
    otaInProgress = true;
    Serial.println("[OTA] Update started. Timing inputs disabled.");
    digitalWrite(PIN_LED_DATA, HIGH);
  });

  ArduinoOTA.onEnd([]() {
    Serial.println("\n[OTA] Update complete.");
    digitalWrite(PIN_LED_DATA, LOW);
  });

  ArduinoOTA.onProgress([](unsigned int progress, unsigned int total) {
    static uint8_t lastPercent = 255;
    uint8_t percent = static_cast<uint8_t>((progress * 100U) / total);
    if (percent != lastPercent && percent % 10 == 0) {
      Serial.printf("[OTA] %u%%\n", percent);
      lastPercent = percent;
    }
  });

  ArduinoOTA.onError([](ota_error_t error) {
    otaInProgress = false;
    digitalWrite(PIN_LED_DATA, LOW);
    Serial.printf("[OTA] Error %u\n", error);
  });

  ArduinoOTA.begin();
}

// ============================================================================
// STATUS
// ============================================================================

void printStatus() {
  uint8_t running = 0;
  uint8_t waiting = 0;
  for (const LaneState &s : lanes) { if (s.running) ++running; if (s.waitingDecision) ++waiting; }

  const float batteryV = readBatteryVoltage();
  Serial.printf("[STATUS] WiFi=%s IP=%s RSSI=%d time=%s session=%s running=%u waiting=%u battery=%.2fV(~%d%%) queue=%lu heap=%u\n",
                WiFi.status() == WL_CONNECTED ? "OK" : "DOWN",
                WiFi.status() == WL_CONNECTED ? WiFi.localIP().toString().c_str() : "-",
                WiFi.status() == WL_CONNECTED ? WiFi.RSSI() : 0,
                timeValid ? "OK" : "INVALID",
                activeSessionId.length() ? activeSessionName.c_str() : "none",
                running,
                waiting,
                batteryV,
                batteryPercentFromVoltage(batteryV),
                static_cast<unsigned long>(countQueuedResults()),
                ESP.getFreeHeap());
}

// ============================================================================
// SETUP / LOOP
// ============================================================================

void setup() {
  Serial.begin(115200);
  delay(300);
  Serial.println();
  Serial.println("====================================================");
  Serial.println(" ESP32 8-channel Wi-Fi Timer - Arduino FW v1.3");
  Serial.println("====================================================");

#ifdef ESP_ARDUINO_VERSION_STR
  Serial.printf("ESP32 Arduino core: %s\n", ESP_ARDUINO_VERSION_STR);
#endif

  pinMode(PIN_LED_WIFI, OUTPUT);
  pinMode(PIN_LED_DATA, OUTPUT);
  digitalWrite(PIN_LED_WIFI, LOW);
  digitalWrite(PIN_LED_DATA, LOW);

  pinMode(PIN_SERVICE, INPUT_PULLUP);

  pinMode(PIN_BATTERY, INPUT);
  analogReadResolution(12);
  analogSetPinAttenuation(PIN_BATTERY, ADC_11db);

  // External 10k pull-ups are required on all lane inputs.
  for (uint8_t i = 0; i < 8; ++i) {
    pinMode(LANE_PINS[i], INPUT);
    bool level = digitalRead(LANE_PINS[i]);
    lanes[i].rawLevel = level;
    lanes[i].stableLevel = level;
    lanes[i].lastRawChangeUs = esp_timer_get_time();
  }

  nvsQueueReady = queuePrefs.begin(NVS_QUEUE_NAMESPACE, false);
  if (!nvsQueueReady) {
    Serial.println("[NVS] ERROR: Preferences namespace could not be opened. Offline queue unavailable.");
  } else {
    Serial.printf("[NVS] Offline queue ready. Queued results: %lu\n",
                  static_cast<unsigned long>(countQueuedResults()));
  }

  Wire.begin(PIN_RTC_SDA, PIN_RTC_SCL);
  rtcPresent = rtc.begin();

  if (!rtcPresent) {
    Serial.println("[RTC] DS3231 not found.");
  } else {
    Serial.println("[RTC] DS3231 detected.");
    if (rtc.lostPower()) {
      Serial.println("[RTC] Warning: RTC reports lost power; waiting for NTP sync.");
    } else {
      setAnchorFromRtc();
    }
  }

  connectWifiInitial();
  setupOta();

  // Prefer NTP when the LAN has Internet access; NTP also updates the RTC.
  // If it is unavailable, a valid DS3231 remains sufficient.
  if (WiFi.status() == WL_CONNECTED) {
    syncNtp(true);
    // Flush old offline results before reading lane readiness.
    flushQueue();
    fetchActiveSession();
    sendDeviceStatus();
    lastDeviceStatusMs = millis();
  }

  printStatus();
  Serial.println("[READY] Firmware initialized.");
}

void loop() {
  ArduinoOTA.handle();
  setWifiLed();

  if (!otaInProgress) {
    scanInputs();
  }

  // SERVICE / RESET button: falling edge, button connected to GND.
  static bool lastServiceLevel = HIGH;
  static uint32_t servicePressedMs = 0;
  bool serviceLevel = digitalRead(PIN_SERVICE);

  if (lastServiceLevel == HIGH && serviceLevel == LOW) {
    servicePressedMs = millis();
  }
  if (lastServiceLevel == LOW && serviceLevel == HIGH) {
    uint32_t held = millis() - servicePressedMs;
    if (held >= 3000UL) {
      Serial.println("[SERVICE] Emergency cancel (3 s hold).");
      resetRunningLanes();
    } else if (held >= 30UL) {
      Serial.println("[SERVICE] Short press ignored. Hold 3 s for emergency cancel of RUNNING lanes.");
    }
  }
  lastServiceLevel = serviceLevel;

  // Wi-Fi recovery.
  if (WiFi.status() != WL_CONNECTED) {
    if (millis() - lastWifiReconnectMs >= WIFI_RECONNECT_MS) {
      lastWifiReconnectMs = millis();
      Serial.println("[WIFI] Reconnect attempt...");
      WiFi.reconnect();
    }
  } else {
    // Retry queued results FIRST. Only afterwards ask the server for lane state,
    // so an offline STOP cannot be accidentally unlocked by stale READY state.
    if (millis() - lastQueueFlushMs >= QUEUE_FLUSH_MS) {
      lastQueueFlushMs = millis();
      flushQueue();
    }

    if (millis() - lastConfigFetchMs >= CONFIG_REFRESH_MS) {
      lastConfigFetchMs = millis();
      fetchActiveSession();
    }

    if (deviceStatusDirty || millis() - lastDeviceStatusMs >= DEVICE_STATUS_MS) {
      lastDeviceStatusMs = millis();
      sendDeviceStatus();
    }

    // NTP: retry periodically until the first successful sync, then every 6 h.
    if (lastNtpSyncMs == 0) {
      if (millis() - lastNtpAttemptMs >= NTP_RETRY_MS) {
        syncNtp(true);
      }
    } else if (millis() - lastNtpSyncMs >= NTP_RESYNC_MS) {
      syncNtp(true);
    }
  }

  if (millis() - lastStatusPrintMs >= STATUS_PRINT_MS) {
    lastStatusPrintMs = millis();
    printStatus();
  }

  delay(1);
}
