# 8. 3D modely V1.7

## Umístění

- `03_3D/SOURCE_V1.7/` — rozbalený OpenSCAD zdroj a STL.
- `03_3D/STL_PRINT_ONLY_V1.7/` — STL připravená k tisku.
- původní ZIPy jsou ponechány ve `03_3D/`.

Hlavní editovatelný soubor:

`stopwatch_enclosures_v1_7.scad`

## Přibližné rozměry

- tlačítkový box: cca 90 × 100 × **55 mm**,
- hlavní box: cca 160 × 110 × 50 mm,
- tray: cca 140 × 76 mm.

Výška tlačítkového boxu byla zvýšena z 45 na 55 mm kvůli kolizi mikrospínače se dnem.

## V1.7

V1.7 opravuje zadní výztuhu hlavního boxu: osm otvorů pro PG7 už není překryto tenkou membránou vytvořenou výztuhou.

## Tisk

Finální materiál: **PETG**.

Doporučený výchozí profil:

- 0,20 mm vrstva,
- 0,4 mm tryska,
- 3–4 perimetry,
- cca 12–15 % výplň,
- support pouze pokud jej slicer skutečně vyžaduje.

## Rozložení centrální krabičky

Doporučený koncept:

```text
ZADNÍ STĚNA / 8× PG7

[ PC817 #1 ] [ PC817 #3 ]
[ PC817 #2 ] [ PC817 #4 ]

         [ ESP32 ]

[18650] [MT3608] [DS3231]
[1S ochr.]       [battery divider]

PŘEDNÍ ČÁST
```

PC817 držet blízko vstupních kabelů, napájecí část pohromadě a signálové vodiče vést přehledně.

## Neuzavřená mechanická změna

Rychlé odpojování kabelů na centrální krabičce je stále otevřené. Diskutovalo se GX16-2 a M12, ale rozhodnutí **nepadlo**. Proto je aktuální platná geometrie V1.7 stále PG7.
