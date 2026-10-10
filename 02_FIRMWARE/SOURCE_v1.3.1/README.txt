ESP32 Timer firmware v1.3.1

ESP32 8-channel Wi-Fi Timer – Arduino IDE firmware v1.3
======================================================

Určeno pro TimerServer v0.5+.

NOVINKY v1.3
-------------
- LittleFS byl úplně odstraněn.
- Offline výsledky se ukládají přes vestavěné Preferences / NVS.
- Není potřeba instalovat knihovnu LittleFS ani vybírat partition s filesystemem.
- Každá dráha má vlastní NVS slot (lane1 až lane8).
- Po STOP zůstává daná dráha zamčená, dokud se výsledek nedostane na server
  a obsluha jej nepotvrdí nebo nezruší.
- Zachováno měření baterie, heartbeat, OTA, RTC/NTP a ochrana pořadí.

KNIHOVNY V ARDUINO IDE
----------------------
Externí:
- ArduinoJson 6.x or 7.x
- RTClib by Adafruit

Součást ESP32 Arduino core:
- WiFi
- HTTPClient
- ArduinoOTA
- Wire
- Preferences (NVS)

Doporučený board package:
- ESP32 by Espressif Systems / Arduino-ESP32 3.x

NVS OFFLINE FRONTa
------------------
Po neúspěšném POST výsledku uloží FW JSON do NVS podle dráhy:
  lane1 ... lane8

Protože se dráha po STOP zamkne, může mít každá dráha maximálně jeden
neodeslaný výsledek. Po obnovení spojení se výsledky znovu odešlou se stejným
DeviceEventId, takže server může retry zpracovat idempotentně.

BATERIE
-------
Zapoj dělič:

BAT+ -- 100k --+-- GPIO36
               |
             100k
               |
              GND

Volitelně 100 nF GPIO36 -> GND.
BAT+ je přímo kladný pól 18650 PŘED step-up MT3608.

Konfigurace v kódu:
BATTERY_DIVIDER_RATIO = 2.0
BATTERY_CAL_FACTOR    = 1.000

GPIO
----
Lane 1 = GPIO32
Lane 2 = GPIO33
Lane 3 = GPIO25
Lane 4 = GPIO26
Lane 5 = GPIO27
Lane 6 = GPIO14
Lane 7 = GPIO34
Lane 8 = GPIO35
RTC SDA = GPIO21
RTC SCL = GPIO22
Wi-Fi LED = GPIO23
DATA LED = GPIO18
SERVICE = GPIO19
BATTERY ADC = GPIO36

GPIO34 a GPIO35 nemají interní pull-up. Doporučeno 10k pull-up na 3.3 V na
všech osmi vstupech.

BEZPEČNOST POŘADÍ
-----------------
START nikdy neposune pořadník.
STOP vytvoří Pending výsledek a zamkne pouze danou dráhu.
Confirm posune jen danou dráhu.
Reject vrátí stejného člověka.
UNDO řeší serverová aplikace v bezpečném stavu dráhy.
