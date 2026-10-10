# 1. Přehled projektu

## Účel

Přenosná 8kanálová časomíra určená primárně pro dětské hasičské soutěže, ale architektura není svázána s jedinou disciplínou. Každá dráha má vlastní pořadník a vlastní časování.

## Fyzické části

- ESP32 DevKit V1 / ESP32-WROOM-32.
- 4× dvoukanálový PC817 modul M351 = 8 galvanicky oddělených vstupních kanálů.
- DS3231 RTC.
- 8× velké tlačítko na cca 5 m kabelu 2×0,75 mm².
- 1× 18650, ochrana 1S, step-up na 5 V, hlavní vypínač.
- 3D tištěná centrální krabička a 8 tlačítkových krabiček.
- Windows PC s ASP.NET Core serverem a SQLite.
- Volitelně TV/monitor s veřejnou stránkou `/display`.

## Software

### Server v0.9

- Target: **.NET 10**.
- ASP.NET Core Minimal API.
- SQLite přes `Microsoft.Data.Sqlite`.
- XLSX import/export přes `ClosedXML`.
- výchozí port `8080`.
- webové stránky jsou statické soubory ve `wwwroot`.
- umí běžet jako Windows Service.

### Firmware v1.3.1

- Arduino IDE / ESP32 Arduino core.
- ArduinoJson 6.x i 7.x.
- RTClib.
- Preferences/NVS pro offline výsledek.
- ArduinoOTA.
- LittleFS se nepoužívá.

## Webové obrazovky

- `/` — jednoduchý rozcestník.
- `/admin` — administrace, chráněná PINem.
- `/display` — provozní obrazovka pro obsluhu/diváky během závodu.
- `/top` — výsledková obrazovka vhodná k vyhlášení.

### Vzhled

V0.9 má dvě varianty:

- `clean` — nový výchozí jednoduchý světlý styl.
- `legacy` — původní vzhled odpovídající v0.6.

Volba se ukládá k aktivní sérii (`RunSessions.UiTheme`) a používá se pro administraci i veřejné obrazovky.

## Hlavní datové entity

- **RunSession** — jedna aktivní série/běh pro stanici.
- **StartEntry** — konkrétní závodník na konkrétní dráze a pořadí.
- **TimingResult** — naměřený výsledek.
- **DeviceStatus** — heartbeat ESP32, baterie a masky `RunningMask`/`WaitingMask`.

## Stav projektu

Funkční logika serveru a firmware je navržena pro nezávislých 8 drah, bezpečné přiřazení výsledku přes `EntryId`, ruční i automatické potvrzení, UNDO, import/export, veřejný průběh a TOP 5 po jednotlivých drahách.

Mechanická část je stále V1.7 s PG7. Přechod na GX16/M12 není rozhodnutý.
