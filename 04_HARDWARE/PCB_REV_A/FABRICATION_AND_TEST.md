# Uvolnění do výroby a přejímka Rev A

## Výstupy, které musí konstruktér doplnit

1. Nativní projekt KiCadu (schéma, footprinty, PCB, 3D modely) se zamčeným kusem 30pin DevKitu, konkrétními typy obou boostů, svorkovnic, baterie a RTC.
2. Úplná ERC/DRC kontrola bez nevysvětlených chyb; výjimky zdůvodnit v projektu. Kontrola nezkratované izolační bariéry z netlistu i vizuálně ve vrstvách mědi.
3. Gerber všech měděných vrstev, nepájivé masky, potisku, obrysu, vrtání Excellon a fab drawing; pro strojní osazení také CPL/centroid a finální BOM s výrobními čísly dílů. Tyto soubory zatím **neexistují**.
4. 3D kontrola vůči originální skříni V1.7, montážní výkres, případně nový díl vaničky a ověření 8 průchodek PG7.
5. Revizní záznam hodnot součástek, zakreslení skutečného limitu regulace 12–24 V a změření prototypu. Výrobní pokyn bez tohoto kroku je předčasný.

## Minimální elektrické testy prototypu

| Test | Metoda | Kritérium |
|---|---|---|
| Izolace před zapnutím | Ohmmetrem GND_LOGIC ↔ F_GND, 3V3 ↔ F_FIELD+ | Bez vodivého spojení. Žádný destruktivní vysokonapěťový test na osazené desce bez testovacího předpisu. |
| Napájení 5 V | Laboratorní zdroj místo 18650, vyzkoušet celý rozsah povoleného napětí článku a start Wi‑Fi | ESP32 bez resetů, boost bez přetížení; ověřit i souběh všech osmi stisků. |
| Step-up naprázdno a s 8 kanály | Měřit TP_F24 proti TP_FGND; nastavit 12,0 / 18,0 / 24,0 V | Regulace v toleranci dané schématem, bez překročení 24,5 V při seřízení; prověřit start/odlehčení a poruchu trimru. |
| Vstupy 1–8 | Každou 5m dvojlinku spínat otevřeno/stisk při 12 a 24 V | Na odpovídajícím GPIO pouze 0–3,3 V; každý stisk jediná správná dráha, bez falešného startu na sousedech. |
| Odolnost kabelu | Zkrat mezi vodiči smyčky, rozpojený kabel, jedna žíla na F_GND, postupně po jedné větvi | Zdroj a jištění zůstanou bezpečné; žádné poškození nebo 24 V na logice. Zkrat F_FIELD+ na F_GND zkoušet jen se zdrojem s proudovým omezením. |
| Teplota | 8 kanálů stisknutých aspoň 30 min v uzavřené krabičce, nejnižší napětí článku a Wi‑Fi aktivní | Rezistory, měniče, baterie a skříň v mezích datasheetů; výsledek zapsat. |
| RTC / ADC / servis | Provoz se skutečným RTC a kalibrací baterie; připojit USB v předepsaném servisním režimu | Žádné 5V pull-upy na I²C a žádné zpětné napájení bateriového boostu z USB. |

## Poznámky k bezpečnosti a provozu

24 V v této koncepci vzniká **uvnitř** zařízení; konektory J1–J8 jsou jen pro beznapěťové tlačítko. Nepřipojovat vnější 24V napájení, jiná elektronická čidla ani kabel společný s cizím strojem bez nového vstupního návrhu. Izolace odděluje kabelové obvody od logiky, ale na desce samotné je nutné ověřit vzdálenosti, přepěťové chování a mechanickou ochranu. U 18650 používat chráněný článek a vhodné jištění; USB konektor ESP32 není automaticky nabíječ článku.
