# 7. Hardware a zapojení

## ESP32 pinout

| Dráha / funkce | GPIO |
|---|---:|
| 1 | 32 |
| 2 | 33 |
| 3 | 25 |
| 4 | 26 |
| 5 | 27 |
| 6 | 14 |
| 7 | 34 |
| 8 | 35 |
| DS3231 SDA | 21 |
| DS3231 SCL | 22 |
| Wi‑Fi LED | 23 |
| DATA LED | 18 |
| SERVICE | 19 |
| Battery ADC | 36 |
| Rezerva | 13 |

## M351 / PC817

Použity 4× dvoukanálové moduly:

| Modul | Kanál | GPIO |
|---|---|---:|
| M351 #1 | CH1 | 32 |
| M351 #1 | CH2 | 33 |
| M351 #2 | CH1 | 25 |
| M351 #2 | CH2 | 26 |
| M351 #3 | CH1 | 27 |
| M351 #3 | CH2 | 14 |
| M351 #4 | CH1 | 34 |
| M351 #4 | CH2 | 35 |

Princip jednoho vstupu:

```text
+5 V ── tlačítko ── INx (M351)
0 V  ────────────── G vstupní strany

3,3 V ── 10k ──┬── GPIO ESP32
                │
           výstup PC817
                │
               GND
```

**Nikdy nepřivést 5 V přímo na GPIO ESP32.**

Externí 10k pull-up na 3,3 V použít na všech 8 kanálech.

## Napájení

```text
18650
  ↓
1S ochrana (M402I, pokud není ochrana v článku)
  ↓
hlavní vypínač L458
  ↓
MT3608 / M407 → 5,0 V
  ├─ ESP32 VIN/5V
  └─ vstupní smyčky M351
```

## Měření baterie

```text
BAT+ ── 100k ──┬── GPIO36
                │
              100k
                │
               GND
```

Volitelně 100 nF z GPIO36 na GND. BAT+ se odebírá před step-upem.

## Tlačítka a kabely

- 8× velké cca 60mm tlačítko.
- montážní otvor ve V1.7: cca 24,6 mm.
- cca 5 m kabelu 2×0,75 mm² na dráhu.
- tlačítkové boxy mají být pro finál z PETG.

## Konektory centrálního boxu

Aktuální stav: **PG7**. V1.7 má 8× otvor 12,5 mm na zadní stěně. GX16 a M12 nejsou schválená změna; nic nepřekreslovat, dokud nebude vybrán konkrétní typ a ověřeny montážní rozměry.
