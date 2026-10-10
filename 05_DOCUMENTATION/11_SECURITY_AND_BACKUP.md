# 11. Bezpečnost a zálohování

## Síťový model

Projekt je navržen primárně pro důvěryhodnou lokální síť/Wi‑Fi na soutěži. Výchozí server používá čisté HTTP, nikoliv HTTPS.

### Doporučení

- změnit výchozí `Admin:Pin`,
- změnit `DeviceApiKey`,
- změnit `OTA_PASSWORD`,
- testovací endpoint po vývoji vypnout,
- server a ESP provozovat v oddělené lokální síti bez veřejného přístupu,
- pokud má být server dostupný z nedůvěryhodné sítě, použít HTTPS/reverse proxy a odpovídající síťová pravidla.

## Admin autentizace

Admin PIN vytvoří 12hodinovou HttpOnly cookie. `Secure=false`, protože výchozí deployment je HTTP. To je další důvod držet systém v izolované LAN nebo přidat TLS před případným širším nasazením.

## Device autentizace

ESP posílá statický `X-Device-Key`. Klíč je uložen v prostém textu v firmware i `appsettings.json`; chraň konfiguraci a nepoužívej výchozí hodnotu.

## Databáze a backup

Aktivní databáze je `Data/timing.db`. Server používá SQLite WAL, proto při externím kopírování za běhu nepředpokládat, že prosté kopírování pouze `.db` je vždy konzistentní. Preferovat serverovou backup funkci nebo aplikaci zastavit.

Adresář záloh je výchozí `Backups`, retention 30 kusů.

Před významnou změnou serveru nebo DB:

1. exportovat výsledky do XLSX,
2. vytvořit DB backup,
3. zazálohovat celý adresář aplikace,
4. teprve potom nasazovat novou verzi.
