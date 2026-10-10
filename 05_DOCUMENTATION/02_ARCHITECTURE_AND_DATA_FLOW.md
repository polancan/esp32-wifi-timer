# 2. Architektura a tok dat

## Blokové schéma

```text
8× tlačítko
   │ cca 5 m, 2×0,75 mm²
   ▼
4× M351 / PC817
   │ 8× digitální vstup
   ▼
ESP32 DevKit V1
   │
   ├─ lokální měření času: esp_timer_get_time()
   ├─ RTC/NTP pouze pro datum/čas události
   ├─ NVS: max. 1 neodeslaný výsledek na každou dráhu
   │
   ▼ Wi‑Fi / HTTP REST
Windows PC
ASP.NET Core v0.9
   │
   ├─ SQLite Data/timing.db
   ├─ /admin
   ├─ /display
   └─ /top
```

## START

1. ESP32 má z `/api/device/config/{stationId}` načteného aktuálního závodníka na každé dráze včetně `EntryId`.
2. Při fyzickém stisku ESP ověří, že dráha není uzamčená a `canStart=true`.
3. Uloží snapshot `RunSessionId`, `EntryId`, jména a monotónního času START.
4. `RunningMask` se změní a heartbeat odešle serveru stav BĚŽÍ.
5. Pořadník se STARTem **neposouvá**.

## STOP

1. Druhý stisk na stejné dráze vytvoří STOP.
2. `DurationUs = stopMonoUs - startMonoUs`.
3. ESP vytvoří unikátní `DeviceEventId` a odešle `/api/results`.
4. Dráha se uzamkne (`waitingDecision=true`).
5. Pokud server není dostupný, JSON výsledku se uloží do NVS slotu `lane1` až `lane8` a zkouší se znovu odeslat.
6. Server zpracovává opakované `DeviceEventId` idempotentně, takže retry nevytvoří duplicitu.

## Ruční potvrzení

```text
Scheduled
   ↓ START
RUNNING (jen stav ESP, StartEntry zůstává aktuální)
   ↓ STOP
StartEntry=Pending + TimingResult=Pending
   ├─ Confirm → StartEntry=Completed + Result=Confirmed
   └─ Reject  → StartEntry=Scheduled + Result=Rejected
```

## Automatické potvrzení

Je-li `RunSessions.AutoConfirmResults=1`, server při přijetí STOP uloží rovnou:

```text
StartEntry=Completed
TimingResult=Confirmed
```

Tím se při dalším načtení konfigurace připraví další závodník. Dříve existující `Pending` výsledky se pouhým zapnutím režimu automaticky nepotvrzují.

## UNDO

UNDO je záměrně konzervativní. Je povoleno jen když:

- zařízení je online,
- daná dráha neběží,
- dráha nečeká na vyhodnocení,
- není na ní jiný `Pending` výsledek,
- po vraceném výsledku nebyla provedena další akce měnící pořadí.

Pak se poslední `Confirmed` výsledek změní na `Undone` a jeho `StartEntry` se vrátí do `Scheduled`.

## Veřejný stav STARTu

`/display` kombinuje data startovní listiny a `DeviceStatus`:

- **PŘIPRAVEN** — běžec je na řadě, ale `RunningMask` pro dráhu není aktivní.
- **BĚŽÍ** — příslušný bit `RunningMask` je 1.
- **V CÍLI / čeká** — dráha je po STOPu v čekajícím stavu.
- **OFFLINE** — `LastSeenUtc` je starší než `DeviceStatusFreshSeconds` (výchozí 15 s).

To slouží i jako kontrola chybného stisku STARTu.
