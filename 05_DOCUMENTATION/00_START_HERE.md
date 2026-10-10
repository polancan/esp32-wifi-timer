# ESP32 8kanálová Wi‑Fi časomíra — předávací balík

**Stav projektu: 7. 10. 2026**

Tento balík obsahuje celý aktuální stav projektu tak, aby se v něm mohl zorientovat další vývojář nebo technik bez znalosti předchozí diskuse.

## Aktuální verze

| Část | Verze | Stav |
|---|---|---|
| Server / web | **v0.9** | aktuální zdroj |
| ESP32 firmware | **v1.3.1** | aktuální zdroj |
| 3D modely | **V1.7** | aktuální PG7 varianta |
| Databáze | SQLite, SchemaVersion **5** | vytváří/migruje server automaticky |

## Co systém dělá

Systém měří až **8 nezávislých drah**. Každá dráha má jedno fyzické tlačítko: první platný stisk je START, druhý STOP. ESP32 měří dobu lokálně pomocí `esp_timer_get_time()`, takže síťová latence nemá vliv na čas. Výsledky se posílají přes Wi‑Fi na ASP.NET Core server, kde se přiřazují ke konkrétním závodníkům a ukládají do SQLite.

Veřejná obrazovka `/display` slouží během soutěže: pro každou dráhu ukazuje aktuálního běžce, stav STARTu, další dva závodníky a **TOP 5 dané dráhy**. Samostatná stránka `/top` je vhodná po skončení soutěže pro vyhlášení.

## Důležité provozní principy

- Dráhy jsou **zcela nezávislé**; jedna dráha nesmí blokovat ostatní.
- START neposouvá pořadník.
- Po STOP se konkrétní dráha uzamkne, dokud server výsledek nevyhodnotí.
- V ručním režimu: `STOP → Pending → Confirm/Reject`.
- V automatickém režimu: nový výsledek po STOP rovnou přejde do `Confirmed` a připraví se další běžec.
- `UNDO` vrací poslední potvrzený výsledek dané dráhy pouze tehdy, když je to bezpečné pro pořadí.
- Při výpadku serveru/Wi‑Fi se nejvýše jeden neodeslaný výsledek na každou dráhu uchová v ESP32 NVS.
- Veřejný stav **BĚŽÍ** vychází z `RunningMask` přímo z ESP32. Pokud závodník fyzicky vyběhne a obrazovka stále ukazuje **PŘIPRAVEN**, START nebyl zachycen.

## Nejrychlejší orientace v balíku

1. `01_SERVER/` — ASP.NET Core server v0.9, webové UI a SQLite logika.
2. `02_FIRMWARE/` — Arduino firmware v1.3.1 pro ESP32 DevKit V1.
3. `03_3D/` — OpenSCAD zdroj a STL V1.7.
4. `04_HARDWARE/` — BOM a referenční schémata.
5. `05_DOCUMENTATION/` — technická dokumentace a postupy.
6. `06_TEST_DATA/` — testovací startovní listina.
7. `90_ARCHIVE_PREVIOUS_SERVER_VERSIONS/` — v0.5 až v0.8 pro historii/rollback.

## Co je aktuálně nerozhodnuté

**Rychlokonektory hlavní krabičky nejsou vybrané.** Aktuální 3D model V1.7 zůstává beze změny s **8× otvorem 12,5 mm pro PG7**. GX16/M12 byly pouze diskutované varianty. Dokud nebude vybrán konkrétní konektor podle skutečných rozměrů, hlavní box se nemá překreslovat na jiný typ konektoru.

## První spuštění pro nového vývojáře

Server: přečíst `05_DOCUMENTATION/03_SERVER_INSTALLATION.md` a spustit zdroj z `01_SERVER/SOURCE_v0.9`.

Firmware: přečíst `05_DOCUMENTATION/04_FIRMWARE_FLASHING.md`, upravit Wi‑Fi, IP serveru a API klíč a nahrát `02_FIRMWARE/SOURCE_v1.3.1/ESP32_Timer_Arduino/ESP32_Timer_Arduino.ino`.

Pro první test není potřeba hardware: server má testovací endpoint a v administraci lze vytvořit sérii a importovat `06_TEST_DATA/startlist_lane1_10.csv`.
