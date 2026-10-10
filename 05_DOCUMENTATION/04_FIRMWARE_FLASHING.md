# 4. Firmware ESP32 v1.3.1

## Zdroj

`02_FIRMWARE/SOURCE_v1.3.1/ESP32_Timer_Arduino/ESP32_Timer_Arduino.ino`

## Požadavky

Arduino IDE s podporou ESP32. Externí knihovny:

- **ArduinoJson** 6.x nebo 7.x.
- **RTClib by Adafruit**.

Součást ESP32 Arduino core:

- WiFi,
- HTTPClient,
- ArduinoOTA,
- Wire,
- Preferences/NVS.

LittleFS není potřeba.

## Povinná konfigurace před flashnutím

V horní části `.ino` upravit:

```cpp
const char* WIFI_SSID       = "...";
const char* WIFI_PASSWORD   = "...";
const char* API_BASE_URL    = "http://IP_PC:8080";
const char* DEVICE_API_KEY  = "...";
const char* STATION_ID      = "TIMER01";
const char* OTA_PASSWORD    = "...";
```

`DEVICE_API_KEY` musí přesně odpovídat `DeviceApiKey` na serveru. `STATION_ID` musí odpovídat stanici použité ve webu.

## Pinout

| Funkce | GPIO |
|---|---:|
| Dráha 1 | 32 |
| Dráha 2 | 33 |
| Dráha 3 | 25 |
| Dráha 4 | 26 |
| Dráha 5 | 27 |
| Dráha 6 | 14 |
| Dráha 7 | 34 |
| Dráha 8 | 35 |
| RTC SDA | 21 |
| RTC SCL | 22 |
| Wi‑Fi LED | 23 |
| DATA LED | 18 |
| SERVICE/RESET | 19 |
| Battery ADC | 36 |

Na **všech 8 vstupních GPIO** použít externí 10k pull-up na 3,3 V. GPIO34 a GPIO35 interní pull-up ani nemají.

## Aktivní logika vstupu

Výchozí `INPUT_ACTIVE_LOW=true`: při stisku optočlen stáhne GPIO na GND.

Debounce je 15 ms. Čas události se bere z prvního fyzického přechodu, ne až z konce debounce intervalu.

## Časování

Doba běhu je měřena pouze:

```cpp
esp_timer_get_time()
```

RTC/NTP slouží jen k přiřazení kalendářního UTC času START/STOP. Síťová odezva se do naměřené doby nepočítá.

## Komunikace

- config se načítá cca každých **1,5 s**,
- status zařízení se posílá cca každých **5 s** nebo při změně,
- NVS queue se zkouší odeslat cca každých **5 s**,
- HTTP timeout je 3,5 s.

## Offline chování

Po STOP se dráha zamkne. Pokud POST na server selže, výsledek se uloží do NVS pod klíčem `lane1` až `lane8`. Protože je dráha zamknutá, v jednom slotu nemůže vznikat nekonečná fronta; na každou dráhu je maximálně jeden čekající výsledek.

Po obnovení Wi‑Fi firmware nejdřív zkouší odeslat queue a teprve potom načítá nový stav dráhy.

## SERVICE tlačítko

GPIO19, tlačítko proti GND. Krátký stisk se ignoruje. Držení alespoň **3 s** provede nouzové zrušení právě běžících lokálních stavů (`running=false`). Nejde o náhradu standardního Confirm/Reject/UNDO a je určeno pro servisní situaci.

## OTA

ArduinoOTA je povoleno. Během OTA jsou vstupy časování deaktivované. Pro ostrý provoz změnit výchozí OTA heslo.

## Poznámka k banneru

Zdrojový kód má `FIRMWARE_VERSION="1.3.1"`, ale textový banner při startu stále vypisuje „FW v1.3“. Jde jen o kosmetickou nepřesnost logu.
