# 9. Testovací plán

Cílem je po změně serveru, firmware nebo kabeláže projít alespoň následující testy.

## A. Server bez hardware

1. Spustit server.
2. Přihlásit se do `/admin`.
3. Vytvořit sérii `TEST` pro `TIMER01`.
4. Importovat `06_TEST_DATA/startlist_lane1_10.csv`.
5. Je-li `EnableTestEndpoints=true`, vložit testovací výsledky přes `/api/test/result`.
6. Ověřit ruční Confirm/Reject.
7. Ověřit TOP 5 na dráze 1.
8. Ověřit export XLSX.
9. Ověřit přepnutí `clean` ↔ `legacy`.

## B. Jedna reálná dráha

1. Ověřit klidovou HIGH úroveň GPIO.
2. Stisk tlačítka → BĚŽÍ.
3. Druhý stisk → STOP/Pending nebo Confirmed podle režimu.
4. Ověřit přesnost času proti referenčním stopkám alespoň orientačně.
5. Ověřit, že opakovaný bounce nevytvoří další událost.

## C. Všech 8 drah

1. Každou dráhu otestovat samostatně.
2. Spustit dvě až více drah současně.
3. Zastavovat je v různém pořadí.
4. Ověřit, že se nezamění závodníci ani `EntryId`.
5. Ověřit, že jedna dráha v Pending neblokuje ostatní.

## D. Kontrola chybného STARTu

1. Připravit závodníka.
2. Fyzicky simulovat situaci, kdy závodník vyběhne, ale tlačítko není stisknuto.
3. `/display` musí zůstat PŘIPRAVEN.
4. Poté provést správný START a ověřit přechod na BĚŽÍ.

## E. Reject a UNDO

- Reject musí vrátit stejného závodníka do `Scheduled`.
- UNDO musí vrátit poslední potvrzený výsledek pouze v bezpečné situaci.
- Po UNDO musí výsledek zmizet z TOP 5 při další aktualizaci.

## F. Automatické potvrzení

1. Zapnout auto-confirm.
2. Dokončit běh.
3. Ověřit, že nevznikne Pending a připraví se další běžec.
4. Ověřit, že UNDO stále funguje.

## G. Výpadek Wi‑Fi/serveru

1. Spustit běh s již načtenou aktivní sérií.
2. Odpojit Wi‑Fi/server před STOP.
3. STOP musí uložit výsledek do NVS a dráhu zamknout.
4. Obnovit síť.
5. Ověřit resend.
6. Ověřit, že nevznikla duplicita.

## H. Reboot a napájení

- Ověřit start s platným DS3231 bez internetu.
- Ověřit start po ztrátě RTC napájení a následný NTP sync.
- Zkontrolovat čtení baterie proti multimetru a případně upravit `BATTERY_CAL_FACTOR`.

## I. Dlouhodobý test

Před ostrou soutěží nechat systém běžet několik hodin s heartbeat, opakovanými starty a exportem. Sleduj serial log, stabilitu Wi‑Fi a volnou heap ESP32.
