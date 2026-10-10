# 10. Troubleshooting

## Web se neotevře z jiného zařízení

- ověřit, že server běží na `0.0.0.0:8080`,
- ověřit IP PC,
- ověřit Windows Firewall TCP 8080,
- obě zařízení musí mít síťovou cestu mezi sebou.

## ESP hlásí 401 / API key rejected

`DEVICE_API_KEY` ve firmware neodpovídá `DeviceApiKey` v `appsettings.json`.

## ESP nemůže spustit dráhu — no active web session

Není aktivní série pro správné `STATION_ID`, nebo firmware ještě nenačetl konfiguraci. Ověřit `TIMER01`, síť a `/admin`.

## START rejected: no READY runner

Na dráze není aktuální `Scheduled` položka, dráha čeká na předchozí výsledek nebo je seznam vyčerpán.

## Běžec vyběhl, ale display zůstává PŘIPRAVEN

Nejpravděpodobněji nebyl zachycen fyzický START. Ověřit tlačítko, kabel, vstup M351, 10k pull-up a příslušný GPIO. Pokud ESP zároveň není online, display to musí označit OFFLINE a stav STARTu není průkazný.

## Dráha zůstala zamčená po STOP

- v ručním režimu zkontrolovat Pending a Confirm/Reject,
- ověřit spojení ESP-server,
- při předchozím výpadku může být výsledek v NVS a čekat na resend,
- zkontrolovat serial log `[QUEUE]`.

## GPIO34/35 jsou nestabilní

Tyto GPIO nemají interní pull-up. V projektu je externí 10k pull-up požadovaný na všech osmi vstupech, nejen 34/35.

## Nesprávné čtení baterie

Změřit reálné napětí článku multimetrem, zkontrolovat 100k/100k dělič a případně doladit `BATTERY_CAL_FACTOR`.

## Import startovní listiny nejde

Podporované: `.csv`, `.txt`, `.xlsx`. Pro text použít např. `Jméno;Dráha;Pořadí`. Dráha musí být 1–8, pořadí > 0. Duplicitní pořadí na stejné dráze není povoleno.

Pokud už v sérii existuje uložený čas, kompletní přepis startovní listiny server odmítne. Vytvořit novou sérii.

## UNDO je odmítnuto

Zkontrolovat, že ESP je online, dráha neběží, nečeká na rozhodnutí a po vraceném běžci nebyla na stejné dráze provedena další akce.

## `backup-now.ps1` vrací 401

Známá drobnost v0.9: skript neposílá admin cookie. Viz `03_SERVER_INSTALLATION.md`.
