# 12. Historie rozhodnutí

## Server

### v0.5

Základ ASP.NET Core + SQLite, startovní listina, nezávislé dráhy, admin/display, import/export, Windows Service.

### v0.6

Přidáno nastavení **automatického potvrzení výsledků** pro aktivní sérii. Výchozí zůstává ruční potvrzení.

### v0.7

Přidána samostatná TOP 5 obrazovka a veřejné API. TOP je počítáno pouze z potvrzených časů.

### v0.8

Provozní `/display` sloučen: aktuální běžec + další dva + TOP 5. Přidána kontrola `RunningMask` pro jasné rozlišení PŘIPRAVEN/BĚŽÍ a OFFLINE stavu.

### v0.9

Nový jednoduchý `clean` vzhled a přepínač na původní `legacy` vzhled z v0.6. TOP 5 je zobrazeno **samostatně pro každou dráhu**. UI theme je uložen v `RunSessions.UiTheme`.

## Firmware

Aktuální v1.3.1:

- NVS/Preferences místo LittleFS,
- ArduinoJson 6/7 kompatibilita,
- 8 nezávislých drah,
- `EntryId` snapshot při START,
- offline retry s `DeviceEventId`,
- status masky, baterie, OTA, DS3231/NTP.

## 3D

- V1.3: problém orientace vík.
- V1.4: oprava mirroringu.
- V1.5: tlačítkový box zvýšen na 55 mm.
- V1.6: oprava výztuh tlačítkového boxu, tray a reset otvoru.
- V1.7: oprava membrány přes zadní PG7 otvory centrálního boxu.

## Důležitá poslední mechanická korekce

Nebylo rozhodnuto o změně PG7 na GX16/M12. Platný model je nadále V1.7 s PG7. Jakýkoliv další model musí být označen novou verzí až po výběru skutečného konektoru.
