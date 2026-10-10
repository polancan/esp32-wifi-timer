# PROJECT CONTEXT — ESP32 8kanálová Wi‑Fi časomíra

## 1. Cíl projektu

Přenosná časomíra až pro **8 nezávislých drah**. Každá dráha má velké fyzické tlačítko na cca 5m kabelu. První stisk = START, druhý = STOP. Délka běhu může být na každé dráze jiná; například dráha 1 může odbavovat závodníka přibližně po minutě, zatímco jiné dráhy mají delší disciplínu. Dráhy se nesmí navzájem blokovat ani synchronizovat.

Systém je určen mimo jiné pro **dětské hasičské soutěže**. PC se serverem bude připojené k velké obrazovce pro diváky.

---

## 2. Architektura

```text
8× tlačítko
   │ cca 5 m kabel
   ▼
4× dvoukanálový PC817 modul M351
   │
   ▼
ESP32 DevKit V1
   │ Wi‑Fi / REST
   ▼
Windows PC
ASP.NET Core + SQLite
   ├── /admin   obsluha
   └── /display veřejná výsledková tabule
```

ESP32 neposílá data přímo do databáze. Komunikuje se serverem přes REST API.

Měření času se provádí **monotónním mikroskopickým časovačem ESP32 (`esp_timer_get_time`)**, nikdy RTC nebo NTP. RTC/NTP slouží jen pro kalendářní čas výsledku.

---

## 3. Aktuální software

### Server v0.5

Soubor: `01_CURRENT_SERVER/ESP32_Timer_Windows_SQLite_v0_5.zip`

- ASP.NET Core **.NET 10**
- SQLite: `Data\timing.db`
- výchozí port: **8080**
- XLSX export přes ClosedXML
- Windows Service skripty
- backup databáze
- veřejný dashboard pro TV

### Webové části

- `/` — rozcestí
- `/admin` — administrace
- `/display` — veřejná read‑only obrazovka pro diváky

Admin PIN: **2026**

Veřejná obrazovka je tematicky udělaná pro **dětskou hasičskou soutěž** a standardně ukazuje 8 drah v rozložení 4×2 na Full-HD TV.

Na každé dráze veřejná obrazovka ukazuje:
- poslední potvrzené výsledky,
- aktuálního závodníka,
- následující dva závodníky.

**Nepotvrzený čas se divákům nezobrazuje jako výsledek.**

Výchozí `appsettings.json`:
- `DeviceApiKey`: `CHANGE_ME_DEVICE_KEY`
- `StationId`: `TIMER01`
- Admin PIN: `2026`
- PublicDisplay title: `Dětská hasičská soutěž`

Device API key musí být stejný v serveru i FW. Pro první test lze použít `CHANGE_ME_DEVICE_KEY`, ideálně později změnit.

### Firmware v1.3.1

Soubor: `02_CURRENT_FIRMWARE/ESP32_Timer_Arduino_v1_3_1_NVS_ArduinoJson6_7.zip`

Důležité:
- **LittleFS byl odstraněn.**
- Offline uložení používá vestavěné **Preferences/NVS**.
- kompatibilní s ArduinoJson **6.x i 7.x**.
- externí knihovny: ArduinoJson + RTClib by Adafruit.
- WiFi, HTTPClient, ArduinoOTA, Wire, Preferences jsou součást ESP32 Arduino core.

Každá dráha má vlastní NVS slot `lane1` až `lane8`. Po STOP se daná dráha uzamkne, takže na jednu dráhu může být maximálně jeden čekající/neodeslaný výsledek. Retry používá stejné `DeviceEventId`, aby nevznikly duplicity.

---

## 4. Stavový automat / pořadí závodníků

Každá dráha má vlastní startovní listinu a vlastní stav.

Bezpečnostní pravidlo:

```text
READY
  ↓ START
RUNNING
  ↓ STOP
PENDING
  ├── CONFIRM → FINISHED → další závodník
  └── REJECT  → stejný závodník znovu READY
```

**START nikdy neposouvá pořadník.**

Po STOP je uzamčena pouze konkrétní dráha. Ostatní dráhy pokračují nezávisle.

Server podporuje také:
- SKIP,
- UNDO posledního potvrzeného výsledku,
- import startovní listiny CSV/XLSX,
- export výsledků.

UNDO výsledek fyzicky nemaže; je označen jako vrácený/invalidovaný a závodník se může vrátit na řadu.

### Startovní listina

Typický import:

```text
Jméno;Dráha;Pořadí
Jan Novák;1;1
Petr Svoboda;1;2
Eva Malá;1;3
Karel Dvořák;2;1
```

Při START si ESP zamkne konkrétní `EntryId` závodníka. Výsledek se tedy nemůže po změně pořadí omylem přiřadit jinému člověku.

---

## 5. Aktuální pinout ESP32 DevKit V1

| Funkce | GPIO |
|---|---:|
| Dráha 1 | GPIO32 |
| Dráha 2 | GPIO33 |
| Dráha 3 | GPIO25 |
| Dráha 4 | GPIO26 |
| Dráha 5 | GPIO27 |
| Dráha 6 | GPIO14 |
| Dráha 7 | GPIO34 |
| Dráha 8 | GPIO35 |
| RTC SDA | GPIO21 |
| RTC SCL | GPIO22 |
| Wi‑Fi LED | GPIO23 |
| DATA LED | GPIO18 |
| SERVICE/RESET | GPIO19 |
| baterie ADC | GPIO36 |
| rezerva | GPIO13 |

GPIO34 a GPIO35 nemají interní pull-up.

FW aktuálně používá `pinMode(LANE_PINS[i], INPUT)`, takže pro jisté definované úrovně je doporučeno použít **externí 10k pull-up na 3,3 V na všech 8 vstupech**. Minimálně GPIO34 a GPIO35 ho potřebují vždy.

Vstup je aktivní v LOW: optočlen při stisku stáhne GPIO k GND.

---

## 6. Optočleny M351 / PC817

Použity 4× Hadex **M351**, každý má 2 kanály PC817.

Mapování:

| Kanál | ESP32 |
|---|---:|
| M351 #1 / CH1 | GPIO32 |
| M351 #1 / CH2 | GPIO33 |
| M351 #2 / CH1 | GPIO25 |
| M351 #2 / CH2 | GPIO26 |
| M351 #3 / CH1 | GPIO27 |
| M351 #3 / CH2 | GPIO14 |
| M351 #4 / CH1 | GPIO34 |
| M351 #4 / CH2 | GPIO35 |

Princip jednoho kanálu:

```text
Tlačítková strana:
+5 V ── tlačítko ── INx M351
0 V  ────────────── G vstupní strany

ESP strana:
3,3 V ── 10k ──┬── GPIO
                │
             výstup PC817
                │
               GND
```

Nikdy nepřivádět 5 V na GPIO ESP32.

Pokud má M351 propojovací jumper mezi zeměmi, pro optické oddělení jej nechat rozpojený. V aktuální konstrukci ale 5V smyčka a ESP mohou používat společný zdroj, takže úplná galvanická izolace stejně není cílem; PC817 hlavně chrání GPIO před dlouhým vedením a rušením.

---

## 7. Napájení

Hardware:
- 1× 18650 článek
- M402I ochrana 1S (pokud článek není chráněný)
- L458 hlavní vypínač
- M407 / MT3608 step-up na 5,0 V

Princip:

```text
18650
  ↓
M402I
  ↓
L458 ON/OFF
  ↓
MT3608 → 5,0 V
  ├── ESP32 VIN/5V
  └── vstupní smyčky PC817
```

### Měření baterie

GPIO36 přes dělič:

```text
BAT+ ── 100k ──┬── GPIO36
                │
              100k
                │
               GND
```

Doporučeně 100 nF mezi GPIO36 a GND.

BAT+ se měří přímo na článku **před MT3608**.

---

## 8. Tlačítka / kabely

- 8× velké 60mm červené arcade tlačítko, montážní otvor cca 24 mm.
- aktuální STL otvor: **24,6 mm**.
- kabel k dráze cca **5 m**, 2×0,75 mm².
- původně bylo počítáno s PG7 průchodkami.
- na tlačítkové krabičce může PG7 zůstat napevno.

Nový požadavek: na **hlavní jednotce** má být rychlé rozpojování osmi kabelů, aby se box nemusel otevírat.

---

## 9. GX16 vs M12 — otevřený bod

Poslední téma před přenosem do nového chatu.

### M12
Výhody:
- průmyslový standard,
- velmi dobré venkovní/IP varianty,
- malé a robustní.

Nevýhody:
- dražší,
- některé panelové typy mají jiné zadní montážní závity / otvory než 12 mm.

### GX16
Pro tento projekt je **velmi rozumná varianta**, zejména pokud je hlavní box při soutěži chráněný před přímým deštěm.

Výhody:
- robustní kovový konektor,
- závitová aretace,
- levný,
- snadno se pájí,
- výrazný a dobře ovladatelný při rozbalování akce.

Nevýhody:
- běžné GX16 nejsou automaticky IP67,
- jsou větší než M12,
- přesný montážní otvor závisí na konkrétním typu.

Pro současný 2žilový kabel stačí **GX16-2**. GX16-4 má smysl jen pokud se přejde na vícevodičový kabel a bude se chtít využít např. LED v tlačítku nebo další signál.

### Doporučený další krok

1. Vybrat konkrétní panelový GX16 (nebo M12) kus dostupný v ČR.
2. Ověřit datasheet / skutečný průměr panelového otvoru, vnější rozměr matice a hloubku těla.
3. Zkontrolovat, zda se 8 konektorů vejde do zadní stěny 160 mm vedle sebe s rezervou pro matice a pájené vývody.
4. Teprve potom upravit `stopwatch_enclosures_v1_7.scad` a vygenerovat V1.8.

**Aktuální V1.7 hlavní box má vzadu 8× 12,5mm otvor pro PG7. Pokud se zvolí GX16, hlavní `main_base_v1_7.stl` netisknout finálně před úpravou.**

---

## 10. 3D modely — aktuální V1.7

Aktuální tiskový ZIP:
`03_CURRENT_3D/ESP32_Timer_V1_7_STL_PRINT_ONLY.zip`

Editovatelný zdroj:
`03_CURRENT_3D/ESP32_8ch_wifi_timer_3D_models_v1_7_SOURCE_AND_STL.zip`

Materiál pro finální tisk: **PETG**.

Doporučené parametry:
- 0,20 mm vrstva,
- 0,4 mm tryska,
- 3–4 perimetry,
- cca 12–15 % výplň,
- bez supportů, pokud slicer neukáže problém.

### Rozměry

Tlačítkový box:
- cca 90 × 100 × **55 mm**
- původní 45mm verze byla nízká a mikrospínač kolidoval se dnem.

Hlavní box:
- cca 160 × 110 × 50 mm.

Tray:
- cca 140 × 76 mm.
- záměrně nechává vzadu servisní koridor pro kabelové průchodky/konektory.

### Historie nalezených chyb a oprav

- V1.3: orientace vík byla zrcadlově špatně.
- V1.4: opraveno otočení/mirroring vík.
- V1.5: tlačítková základna zvýšena z 45 na 55 mm kvůli mikrospínači.
- V1.6:
  - opraveny výztuhy tlačítkové základny, které zasahovaly do lemu víka,
  - opraven tray: zadní řada montážních otvorů z 62 na 63 mm,
  - RESET otvor 12,5 mm.
- V1.7:
  - opravená zadní výztuha hlavní krabičky; ve V1.6 po vyříznutí PG7 otvorů znovu zakryla otvory cca 1,2mm membránou.
  - V1.7 perforuje výztuhu stejnými osmi otvory.

Editovatelný V1.7 zdroj v balíku byl ověřen tak, že z něj vygenerovaný `main_base` má shodné rozměry, objem, plochu i počet trojúhelníků jako tiskový V1.7 STL.

### Co už bylo vytištěno

Uživatel má z PLA:
- jednu starou nízkou tlačítkovou krabičku — pouze prototyp,
- hlavní základnu,
- starší tray.

Rozhodnutí: finální díly vytisknout **všechny znovu z PETG**. Staré PLA použít jen pro zkoušky osazení.

---

## 11. Rozložení hlavní krabičky

Tray je nosič nízkonapěťové elektroniky. Doporučené rozmístění:

```text
ZADNÍ STĚNA / 8 kabelových konektorů

[ PC817 #1 ] [ PC817 #3 ]
[ PC817 #2 ] [ PC817 #4 ]

         [ ESP32 ]

[18650] [MT3608] [DS3231]
[ochrana]        [battery divider]

PŘEDNÍ ČÁST
```

PC817 držet blízko zadních konektorů. Napájecí část držet pohromadě a pokud možno oddělit od signálového vedení.

Na víku hlavního boxu:
- hlavní vypínač L458,
- Wi‑Fi LED,
- DATA LED,
- ERR/status LED podle návrhu víka,
- servisní/reset tlačítko.

USB ESP32 není přístupné zvenku; při prvním flashování se otevře víko, další aktualizace lze přes OTA.

---

## 12. BOM / objednaný hardware

Uživatel už díly objednal.

Klíčové kusy:
- ESP32 DevKit V1 — už vlastněn,
- DS3231 — už vlastněn,
- 4× Hadex M351 (2ch PC817),
- M407 MT3608 step-up,
- M402I ochrana 1S,
- 16× PG7 O275 původně pro centrální i tlačítkové boxy,
- cca 45 m kabelu 2×0,75 mm² N125C,
- L458 hlavní vypínač,
- 8× 60mm arcade tlačítko,
- 18650 + držák.

CSV BOM jsou ve složce `04_HARDWARE_BOM`.

---

## 13. Doporučený test po přenosu

1. Nejdřív zkompilovat FW v1.3.1 v Arduino IDE.
2. Nastavit `WIFI_SSID`, `WIFI_PASSWORD`, `API_BASE_URL`, `DEVICE_API_KEY`.
3. Server spustit `dotnet run` a otevřít `/admin`.
4. Přihlásit PINem `2026`.
5. Vytvořit testovací session + startovní listinu.
6. Pro první elektrický test lze simulovat tlačítko přímo na vstupu optočlenu / GPIO podle zapojení.
7. Ověřit START → STOP → PENDING → CONFIRM/REJECT.
8. Ověřit dvě dráhy současně a rozdílné tempo drah.
9. Ověřit veřejný `/display` na druhém monitoru/TV.
10. Ověřit výpadek Wi‑Fi/serveru a resend NVS výsledku.

---

## 14. Co řešit jako další

**Priorita 1:** vybrat konkrétní rychlokonektor hlavní krabičky — pravděpodobně GX16-2 vs M12 — a udělat V1.8 hlavní základny.

Potom:
- fyzické rozmístění skutečných modulů na tray,
- definitivní kabeláž a konektory,
- otestovat FW na skutečném M351,
- první kompletní dráha,
- teprve pak sériový PETG tisk osmi tlačítkových boxů.
