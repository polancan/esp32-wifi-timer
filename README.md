# ESP32 Wi-Fi Timer — osmikanálová časomíra

Časomíra pro až **8 nezávislých drah** s ESP32, webovou administrací a výsledkovou tabulí. První stisk tlačítka spustí měření, druhý jej zastaví. Čas měří přímo ESP32; síťová latence naměřený čas neovlivňuje.

Repozitář obsahuje server, firmware, podklady k zapojení, 3D modely a českou dokumentaci z předávacího balíku ze 7. 10. 2026.

| Část | Verze / technologie |
| --- | --- |
| Server a web | v0.9 · ASP.NET Core / .NET 10 · SQLite schema 5 |
| Firmware | v1.3.1 · ESP32 Arduino · NVS · ArduinoJson 6/7 |
| Krabičky | V1.7 · OpenSCAD + STL · PG7 · PETG |

## Funkce

- Nezávislé dráhy, pořadníky závodníků a TOP 5 pro každou dráhu.
- Ruční potvrzení / odmítnutí výsledku nebo automatické potvrzování, bezpečné UNDO.
- Veřejná živá obrazovka `/display` a výsledky `/top`.
- Import a export startovních listin a výsledků, SQLite databáze a zálohy.
- Uchování jednoho neodeslaného výsledku na dráhu v NVS při výpadku spojení.
- OTA aktualizace ESP32 a servisní tlačítko.

## Rychlý start serveru

Na Windows s .NET 10 SDK:

```powershell
git clone https://github.com/polancan/esp32-wifi-timer.git
cd esp32-wifi-timer/01_SERVER/SOURCE_v0.9
dotnet restore
dotnet run
```

Otevřete `http://localhost:8080/admin` pro obsluhu, `/display` pro průběh soutěže nebo `/top` pro výsledky. Testovací startovní listina je v `06_TEST_DATA/startlist_lane1_10.csv`.

Výchozí konfigurace je pouze pro testování v důvěryhodné LAN. Před reálným použitím změňte PIN `2026`, klíč `CHANGE_ME_DEVICE_KEY`, Wi-Fi údaje a OTA heslo `timer-ota`; na serveru a ESP32 musí být shodný klíč. Po testování vypněte testovací endpointy. Server nevystavujte přímo do internetu. Podrobnosti: [bezpečnost a zálohy](05_DOCUMENTATION/11_SECURITY_AND_BACKUP.md).

Pro lokální serverové hodnoty lze použít proměnné prostředí `Admin__Pin`, `DeviceApiKey` a `Features__EnableTestEndpoints`, případně ignorovaný soubor `appsettings.Production.json` při prostředí Production. Skutečné přístupové údaje necommitujte.

## Firmware a hardware

V Arduino IDE otevřete [sketch ESP32](02_FIRMWARE/SOURCE_v1.3.1/ESP32_Timer_Arduino/ESP32_Timer_Arduino.ino), nainstalujte podporu ESP32, ArduinoJson a RTClib a nastavte Wi-Fi, adresu serveru a klíč podle [návodu k firmware](05_DOCUMENTATION/04_FIRMWARE_FLASHING.md).

Zapojení a pinout jsou v [hardwarové dokumentaci](05_DOCUMENTATION/07_HARDWARE_AND_WIRING.md). Všech osm vstupů vyžaduje externí 10k pull-up na 3,3 V. Referenční obrázky nenahrazují ověření skutečného zapojení. Aktuální krabička má otvory pro PG7; rychlokonektory zůstávají otevřeným návrhovým bodem.

Pro variantu s 30pin DevKit V1, osmi suchými kontakty a nastavitelnou izolovanou smyčkou 12–24 V jsou připravené [podklady PCB Rev A](04_HARDWARE/PCB_REV_A/README.md). Jde o zadání pro návrh a ověření desky; Gerbery, finální footprinty a změřený prototyp dosud nejsou hotové.

## Obsah repozitáře

| Složka | Obsah |
| --- | --- |
| `01_SERVER` | Zdroj serveru, web, instalační skripty a původní ZIP |
| `02_FIRMWARE` | Arduino firmware a poznámky k zapojení |
| `03_3D` | OpenSCAD a 19 STL dílů tiskové sady |
| `04_HARDWARE` | Seznamy součástek a referenční schémata |
| `05_DOCUMENTATION` | Instalace, provoz, API, testy a roadmapa |
| `06_TEST_DATA` | Ukázková data bez skutečných závodníků |
| `90_ARCHIVE_PREVIOUS_SERVER_VERSIONS` | Historické servery v0.5–v0.8 |

Začněte [průvodcem projektem](05_DOCUMENTATION/00_START_HERE.md). Další postup popisuje [roadmapa](05_DOCUMENTATION/13_OPEN_POINTS_AND_ROADMAP.md) a [testovací plán](05_DOCUMENTATION/09_TEST_PLAN.md).

## Ověření a známá omezení

Server byl 10. 10. 2026 úspěšně sestaven v konfiguraci Release pomocí .NET SDK 10.0.401. Kontrola zranitelností NuGet nebyla dostupná kvůli spojení se zdrojem balíčků (NU1900). Firmware ani skutečný hardware tímto nejsou ověřeny. Automatické sestavení serveru je definováno v GitHub Actions.

Skript `backup-now.ps1` ve v0.9 neprovádí přihlášení k chráněnému API a může vracet HTTP 401; zálohu zatím spouštějte z přihlášené administrace. Původní ZIPy jsou historické snímky, aktuální stav sledujte ve zdrojových složkách. Původní `FILE_INDEX.txt` a `SHA256SUMS.txt` popisují předávací balík, nikoli dodatečně doplněné soubory pro GitHub.

## Licence

Licence dosud nebyla určena. Veřejná dostupnost repozitáře sama o sobě neuděluje licenci k dalšímu použití. Viz [LICENSE_NOT_DEFINED.txt](LICENSE_NOT_DEFINED.txt). Výběr licence zůstává na vlastníkovi projektu.
