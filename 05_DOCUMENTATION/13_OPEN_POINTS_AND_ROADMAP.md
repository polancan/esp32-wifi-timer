# 13. Otevřené body a roadmapa

## Priorita A — dokončení hardware

1. Rozhodnout, zda centrální box zůstane na PG7, nebo se použije rychlokonektor.
2. Pokud rychlokonektor: vybrat **konkrétní** panelový díl a změřit otvor, matici a hloubku.
3. Teprve potom vytvořit V1.8 3D model.
4. Fyzicky rozmístit M351, ESP32, napájení a RTC na tray.
5. Otestovat jednu kompletní dráhu s 5m kabelem.
6. Až poté vyrobit/tisknout všech osm finálních tlačítkových boxů z PETG.

## Priorita B — software před ostrým nasazením

- End-to-end build serveru na Windows s .NET 10 SDK.
- Ověřit dlouhodobý běh na reálném ESP32.
- Vypnout test endpoint v ostré konfiguraci.
- Upravit `backup-now.ps1`, aby se uměl autentizovat.
- Připravit skutečné backup/restore cvičení SQLite.

## Volitelné budoucí funkce

Tyto věci byly diskutované nebo dávají smysl, ale nejsou součástí aktuálního datového modelu v0.9:

- bohatší údaje závodníka: tým/SDH, kategorie, ročník, startovní číslo,
- generátor testovací startovní listiny přímo v UI,
- více přednastavených barevných témat podle kategorie závodu; aktuálně jsou jen `clean` a `legacy`,
- oddělený režim výsledkovky po uzavření série,
- detailnější audit změn obsluhy,
- HTTPS/reverse proxy pro nasazení mimo izolovanou LAN.

Při rozšiřování zachovat klíčové invarianty: lokální měření času, `EntryId` snapshot při START, nezávislé dráhy a idempotentní resend přes `DeviceEventId`.
