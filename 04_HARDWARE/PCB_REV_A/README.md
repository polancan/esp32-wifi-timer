# PCB Rev A – výrobní zadání, 8kanálové stopky

Stav: **podklady pro nakreslení a ověření PCB; zatím NEODESÍLAT do výroby**. Tato složka není projekt KiCadu ani sada Gerberů. Určuje elektrické vazby, proudy, rozmístění a přejímací kritéria, aby konstruktér mohl vytvořit skutečné výrobní soubory bez odhadování z ilustračních obrázků původního projektu.

## Zvolená varianta

- Zásuvný **30pin ESP32 DevKit V1**; před uzavřením footprintu nutno změřit skutečně zakoupený kus (rozteč řad, délku a orientaci USB).
- Osm externích **dvouvodičových suchých kontaktů**, přibližně 5 m kabelu na dráhu. Žádné cizí napětí na tlačítkách.
- Nastavitelná **izolovaná napájecí větev 12–24 V DC** pro smyčky. Výchozí nastavení 24,0 V, hardwarově omezený horní rozsah max. 24,5 V za normálního provozu.
- Osm samostatných optočlenů s výstupy 3,3 V, stisk = LOW; firmware a mapa GPIO zůstávají beze změny.
- PCB má nahradit nebo být přišroubovaná na původní vaničce 140 × 76 mm; koncepce otvorů níže.

## Obsah

- [Zapojení a výpočty](ELECTRICAL.md)
- [Mechanika a pravidla návrhu](MECHANICAL_AND_LAYOUT.md)
- [BOM](BOM.csv) a [seznam propojení](NETS.csv)
- [Přejímací a výrobní postup](FABRICATION_AND_TEST.md)
- [Blokové schéma](block_diagram.svg)

## Rozhodnutí nutná před Gerbery

1. Změřit fyzickou desku DevKit V1 a vybrané svorkovnice, vypínač, držák 18650, RTC, indikátory a konkrétní dva boost moduly/obvody. Fotografie nebo marketingový název nestačí pro footprint.
2. Ověřit na stole **5V zdroj nejméně 2 A trvale** ze chráněné 1S 18650 při nejnižším povoleném napětí článku; původní MT3608/M407 nelze bez měření automaticky převzít.
3. Vybrat a změřit **izolovaný 5V→12–24V boost** na sekundární straně: po odběru 0–100 mA, při rozběhu a při odpojené zátěži nesmí překročit mez optočlenů a kondenzátorů. Reference TPS55340 je návrhový kandidát, nikoli hotový footprint.
4. Změřit sestavu v krabičce V1.7: výšku nad PCB, přístup USB a regulaci, kabelový oblouk za osmi průchodkami PG7 a prostor pro baterii. Teprve poté dokončit 3D kolize.
5. Vyhotovit nativní schéma + PCB, ERC/DRC, Gerber/Excellon a výrobní dokumentaci; nezaměnit tento podklad za výrobní data.

Původní [popis zapojení](../../05_DOCUMENTATION/07_HARDWARE_AND_WIRING.md) a fotografie M351 jsou reference staršího 5V provedení. Vše v této složce je nový návrh Rev A, nikoli důkaz, že staré moduly snesou 24 V.
