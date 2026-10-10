# 6. API a databáze

## Autentizace

### Admin

`POST /api/auth/login` s JSON:

```json
{"pin":"2026"}
```

Po úspěchu server nastaví HttpOnly cookie `TimerAdmin` s platností 12 hodin. Všechny běžné admin API endpointy pak vyžadují tuto cookie.

### ESP32

Zařízení používá HTTP header:

```text
X-Device-Key: <DeviceApiKey>
```

Server ověřuje klíč na device endpointu.

### Veřejné endpointy

`/api/public/*` jsou bez admin PINu.

## Hlavní endpointy

| Metoda | Cesta | Účel |
|---|---|---|
| POST | `/api/auth/login` | admin login |
| POST | `/api/auth/logout` | admin logout |
| GET | `/api/public/config` | veřejná konfigurace |
| GET | `/api/public/display/{stationId}` | data provozní obrazovky |
| GET | `/api/public/top/{stationId}?take=5` | TOP časy po drahách |
| GET | `/api/device/config/{stationId}` | aktuální běžec pro všech 8 drah |
| POST | `/api/device/status` | heartbeat, baterie, masky |
| POST | `/api/results` | naměřený výsledek z ESP |
| GET | `/api/device/status/{stationId}` | stav stanice pro admin |
| GET | `/api/sessions` | seznam sérií |
| GET | `/api/sessions/active/{stationId}` | aktivní série |
| POST | `/api/sessions` | vytvoření série |
| POST | `/api/sessions/{id}/settings` | auto-confirm |
| POST | `/api/sessions/{id}/theme` | clean/legacy |
| POST | `/api/sessions/{id}/close` | uzavření série |
| GET | `/api/sessions/{id}/startlist` | startovní listina |
| POST | `/api/sessions/{id}/startlist/import-text` | text import |
| POST | `/api/sessions/{id}/startlist/import-file` | CSV/TXT/XLSX import |
| POST | `/api/startlist/{entryId}/skip` | přeskočení běžce |
| POST | `/api/results/{id}/confirm` | potvrzení výsledku |
| POST | `/api/results/{id}/reject` | zamítnutí výsledku |
| POST | `/api/sessions/{id}/lanes/{lane}/undo` | vrácení posledního potvrzeného času |
| GET | `/api/results` | filtrování výsledků |
| GET | `/api/export.xlsx` | export XLSX |
| POST | `/api/admin/backup` | vytvoření DB backupu |
| POST | `/api/test/result` | testovací čas; jen když EnableTestEndpoints=true |

## SQLite tabulky

### `RunSessions`

- `Id`
- `StationId`
- `Name`
- `IsActive`
- `CreatedAtUtc`
- `ClosedAtUtc`
- `AutoConfirmResults`
- `UiTheme`

### `StartEntries`

- `Id` — GUID.
- `RunSessionId`.
- `Lane` 1–8.
- `Sequence` > 0.
- `RunnerName`.
- `Status` — typicky `Scheduled`, `Pending`, `Completed`, `Skipped`.
- `ResultId`.
- timestamps.

Unikátní je kombinace `(RunSessionId, Lane, Sequence)`.

### `TimingResults`

- `Id` — integer PK.
- `DeviceEventId` — unikátní, slouží k idempotenci retry.
- `StationId`.
- `RunSessionId`.
- `EntryId`.
- `Lane`.
- `RunnerNameSnapshot`.
- `StartedAtUtc`, `StoppedAtUtc`.
- `DurationUs`.
- `ReceivedAtUtc`.
- `ValidationStatus` — `Pending`, `Confirmed`, `Rejected`, `Undone`.

### `DeviceStatus`

- `StationId` PK.
- `FirmwareVersion`.
- `BatteryVoltage`, `BatteryPercent`.
- `RunningMask`, `WaitingMask`.
- `LastSeenUtc`.

### `AppMeta`

Obsahuje `SchemaVersion`. V0.9 používá hodnotu **5**.

## Masky drah

Bit 0 = dráha 1, bit 1 = dráha 2, … bit 7 = dráha 8.

`RunningMask` = dráhy, na kterých lokálně běží čas.

`WaitingMask` = dráhy, které po STOP čekají na dokončení serverového rozhodnutí/odemčení.
