# 3. Server — instalace, build a konfigurace

## Zdroj

`01_SERVER/SOURCE_v0.9`

Projekt je `TimerServer.csproj` a cílí na `net10.0`.

## Požadavky pro vývoj

- Windows 10/11 nebo Windows Server.
- .NET 10 SDK.
- Prohlížeč.
- Volitelně PowerShell s administrátorským oprávněním pro instalaci služby.

## Spuštění ze zdroje

V adresáři `01_SERVER/SOURCE_v0.9`:

```powershell
dotnet restore
dotnet run
```

Výchozí URL: `http://localhost:8080`.

Pro přístup z ESP musí být PC dosažitelné v LAN a TCP port 8080 povolen ve firewallu.

## Publish

Framework-dependent varianta:

```powershell
dotnet publish -c Release -r win-x64 --self-contained false -o publish
```

Vyžaduje .NET 10 runtime na cílovém PC.

Self-contained varianta:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -o publish
```

Je větší, ale cílový PC nepotřebuje předinstalovaný .NET runtime.

Skript `Deploy/install-service.ps1` očekává `TimerServer.exe` přímo v kořeni publish adresáře.

## Instalace jako Windows Service

Po publish spusť PowerShell jako správce v adresáři aplikace:

```powershell
.\Deploy\install-service.ps1
```

Skript:

- vytvoří službu `ESP32TimerServer`,
- nastaví automatický start,
- nastaví restart služby po pádu,
- vytvoří inbound firewall pravidlo pro TCP 8080 v profilu Private,
- spustí službu.

Odinstalace:

```powershell
.\Deploy\uninstall-service.ps1
```

## appsettings.json

Výchozí důležité položky:

| Klíč | Výchozí | Význam |
|---|---|---|
| `Urls` | `http://0.0.0.0:8080` | poslouchání serveru |
| `DeviceApiKey` | `CHANGE_ME_DEVICE_KEY` | tajný klíč mezi ESP a serverem |
| `StationDefaults:StationId` | `TIMER01` | výchozí ID stanice |
| `Storage:DatabasePath` | `Data\\timing.db` | SQLite DB |
| `Storage:BackupDirectory` | `Backups` | zálohy |
| `Storage:BackupRetentionCount` | `30` | počet záloh |
| `Features:EnableTestEndpoints` | `true` | testovací endpoint |
| `DeviceStatusFreshSeconds` | `15` | po kolika s je zařízení offline |
| `Admin:Pin` | `2026` | PIN do adminu |
| `PublicDisplay:RecentResults` | `3` | počet nedávných výsledků v datech displaye |

### Co změnit před ostrým provozem

- Změnit `DeviceApiKey` a stejnou hodnotu zapsat do firmware.
- Změnit administrátorský PIN.
- Po dokončení testů zvážit `EnableTestEndpoints=false`.
- PC dát stabilní IP adresu nebo DHCP reservation a tu použít v `API_BASE_URL` firmware.

## Databáze

Při prvním startu server automaticky vytvoří `Data/timing.db`. Při startu také doplňuje chybějící sloupce a nastaví `SchemaVersion=5`.

DB běží v režimu WAL (`PRAGMA journal_mode=WAL`).

## Záloha

Server má endpoint `/api/admin/backup`, který vytvoří kopii SQLite do adresáře `Backups` a respektuje retention.

**Známá drobnost v0.9:** přiložený `Deploy/backup-now.ps1` volá admin endpoint bez přihlášení/cookie, zatímco admin API je chráněné PINem. Při čistém v0.9 tedy může skončit HTTP 401. Pro ruční zálohu použít administraci/API po přihlášení, nebo skript v další revizi upravit tak, aby se nejdřív přihlásil.

## URL

- `/admin` — administrace.
- `/display` — průběh soutěže.
- `/top` — TOP výsledky / vyhlášení.
- `/api/status` — základní status serveru, ale je admin-authenticated.
