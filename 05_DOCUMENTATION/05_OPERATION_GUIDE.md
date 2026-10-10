# 5. Provozní návod

## 1. Před soutěží

1. Zapnout Windows PC a server.
2. Ověřit `http://localhost:8080`.
3. Zapnout ESP32 jednotku.
4. V administraci `/admin` se přihlásit PINem.
5. Ověřit, že stanice `TIMER01` je online, firmware odpovídá a baterie má rozumné napětí.
6. Vytvořit novou sérii.
7. Zvolit ruční nebo automatické potvrzení.
8. Zvolit vzhled `Čistý` nebo `Původní v0.6`.
9. Importovat startovní listinu.
10. Na druhé obrazovce otevřít `/display`.

## 2. Startovní listina

Podporované formáty: CSV, TXT, XLSX.

Nejjednodušší struktura:

```text
Jméno;Dráha;Pořadí
Jan Novák;1;1
Petr Svoboda;1;2
Eva Malá;2;1
```

Server umí také `Dráha;Pořadí;Jméno` nebo `Dráha;Jméno` s automatickým pořadím.

Startovní listinu nelze kompletně přepsat po uložení prvního času v dané sérii; pro jinou listinu vytvořit novou sérii.

## 3. Význam stavů na veřejné obrazovce

- **PŘIPRAVEN** — tento závodník je na řadě, časomíra dosud nevidí START.
- **BĚŽÍ** — časomíra skutečně měří.
- **V CÍLI** — STOP byl zachycen a stav se vyhodnocuje.
- **OFFLINE** — ESP nehlásí čerstvý heartbeat; stav tlačítek nelze považovat za spolehlivý.

Praktická kontrola: když závodník fyzicky odstartuje a jeho karta nepřejde během krátké chvíle na **BĚŽÍ**, běh zastavit co nejdříve. Je pravděpodobné, že START nebyl zachycen; není nutné nechat závodníka doběhnout celý běh bez času.

## 4. Ruční režim

Po doběhu výsledek čeká na potvrzení:

- **Potvrdit** — závodník je dokončen a připraví se další.
- **Zamítnout** — výsledek je `Rejected` a stejný závodník se vrátí jako připravený.

Ruční režim je vhodný tam, kde obsluha chce každý čas zkontrolovat.

## 5. Automatické potvrzení

Po zapnutí se nové výsledky potvrzují rovnou. Vhodné pro rychlý provoz s ověřeným hardwarem. `UNDO` zůstává k dispozici.

Zapnutí automatiky nemění již existující čekající výsledky.

## 6. SKIP

Přeskočit lze pouze aktuálně připraveného závodníka. Stav se změní na `Skipped` a na řadu přijde další.

## 7. UNDO

Používat pro vrácení posledního potvrzeného výsledku na konkrétní dráze. Server operaci odmítne, pokud by mohl promíchat pořadí nebo je zařízení offline/busy.

## 8. TOP 5

Na `/display` je **TOP 5 zvlášť pro každou dráhu**, nikoliv celkový žebříček. Započítávají se jen `Confirmed` výsledky dokončených startovních položek.

`Pending`, `Rejected` a `Undone` se do TOP 5 nezapočítávají.

## 9. Po soutěži

- Otevřít `/top` pro výsledkovou obrazovku.
- Exportovat výsledky do XLSX.
- Vytvořit zálohu databáze.
- Uzavřít aktivní sérii.
