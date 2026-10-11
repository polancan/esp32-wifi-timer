# Elektrické zadání Rev A

## Napájecí strom a oddělení

```text
18650 1S (chráněná) → vypínač → 5V boost >=2 A trvale → ESP32 VIN/5V
                                                    ├→ LED a ostatní 5V zátěž
                                                    └→ izolovaný DC/DC 5V/5V, 5 W
                                                              ↓ IZOLACE ↓
                                                      5V_FIELD → step-up 12–24V
                                                                      ↓
                                                               F_FIELD+ → 8 smyček

ESP32 3V3 → logika optočlenů, DS3231 (ověřený 3,3V typ)
ESP32 GND a F_GND se nespojují; žádný společný zemní polygon pod bariérou.
```

Pro oddělovací stupeň je konkrétní návrhový díl **RECOM REC5-0505SRW/H4/A** (4,5–9 V vstup, 5 V/1 A výstup, 5 W, THT DIP24, 4 kV DC/1 s výrobní zkouška). Jeho `-Vin` jsou piny 2,3, `+Vin` 22,23, `+Vout` 14 a `-Vout` 16; pin 1 není bez varianty `/CTRL` osazen. Piny `NC` se nikam nepřipojují. Přesné pouzdro, kontakty a filtrace dle aktuálního datasheetu. Izolační parametr součástky není certifikací celého zařízení.

Step-up na izolované straně musí mít vstup 5 V, nastavení **12,0–24,0 V**, trvalý výstup nejméně 100 mA při 24 V, omezení přepětí a bezpečné chování naprázdno. Návrhový IC **TPS55340** má 40V spínač a výrobce publikuje 5–12V→24V referenční desku; převzetí jejího výkonového stupně, kompenzace a layoutu vyžaduje výpočet pro zdejší proud a pro celou regulaci 12–24 V. Samotný TPS55340 nebo neznámý MT3608 modul se nesmí vložit na PCB bez ověření. Na desce mají být měřicí body `TP_F24` a `TP_FGND`, přístupný víceotáčkový trimr, jasný popis „12–24 V / nastavit před připojením tlačítek“ a ochrana proti posunu trimru nad 24,5 V. Ochranný prvek na sběrnici musí být zvolen tak, aby při normálních 24 V nevodil a nepřekročil dovolené napětí zvolených kondenzátorů (min. 35 V, přednostně 50 V).

Oddělený zdroj při 24 V dodává osm větví po asi 10,4 mA, tedy přibližně **2,0 W** do kabelových smyček. Při účinnosti step-upu 80 % je to 2,5 W z izolovaných 5 V, tj. 0,5 A, pod 1A limitem měniče. Při 80 % účinnosti izolátoru vychází cca 3,1 W z primárních 5 V. ESP32 s Wi‑Fi a další zátěží musí mít samostatně rezervu; proto je návrhové minimum primárního 5V zdroje 2 A trvale. Při vybitém článku kolem 3 V může celá soustava odebírat přes 2,5 A z baterie, proto je třeba dimenzovat ochranu článku, vypínač a vodiče alespoň na 3 A trvale a ověřit rozběhovou špičku. Nejde o změřenou spotřebu ani potvrzenou výdrž 18650.

## Jeden z osmi totožných vstupů (index n = 1…8)

```text
F_FIELD+ ── Jn.1 ── suché tlačítko ── Jn.2 ── Rn 2k2 / 0,5W ──►| Un LED ── F_GND
                                                              │          │
                                                              └──|◄──────┘ Dn 1N4148

3V3 ── RPn 10k ──●── GPIO_n (ESP32 INPUT, stisk LOW)
                 │
             Un kolektor
             Un emitor ─────────── GND_LOGIC
```

`Un`: Everlight EL817C-G (CTR třída C); nebo ekvivalent se změřenou logikou. `Dn` je ochrana LED proti obrácené polaritě a musí být osazena antiparalelně. Před sériovým Rn je doporučený footprint pro TVS `Tn` mezi Jn.2 a F_GND; přesný typ se volí podle přepětí kabelu, kapacity a omezení step-upu (DNP v základní BOM). Zkrat kabelu mezi Jn.1 a Jn.2 proudově omezuje Rn. Zkrat Jn.1 nebo Jn.2 při stisknutém tlačítku přímo na F_GND musí řešit samostatné jištění/omezení zdroje. **24 V nesmí nikdy přijít na GPIO nebo 3V3.**

Výpočet se zjednodušeným úbytkem LED 1,2 V: při 12 V `(12−1,2)/2200 = 4,9 mA`, při 24 V `10,4 mA`; ztráta Rn při 24 V `((24−1,2)^2)/2200 = 0,236 W`, při 24,5 V asi 0,247 W. 0,5W rezistor poskytuje výkonovou rezervu, ale je nutné zohlednit teplotu uvnitř krabičky. Pull-up 10 kΩ vyžaduje přibližně 0,33 mA kolektorového proudu. Datasheet EL817C garantuje CTR 200–400 % pouze při IF = 5 mA a VCE = 5 V; **funkci na 12,0 V, v saturaci, při teplotě a stárnutí musí potvrdit měření všech osmi kanálů**. Firmware má 15ms odrušení, ale zapojení kabelů a EMC přesto ověřit na skutečné délce.

## Mapování a pomocné obvody

| Funkce | GPIO | Logická poznámka |
|---|---:|---|
| Dráhy 1–8 | 32, 33, 25, 26, 27, 14, 34, 35 | Externí 10k pull-up pro **každý** kanál; 34 a 35 vnitřní pull-up nemají. |
| RTC SDA/SCL | 21 / 22 | DS3231 na 3,3 V, oba I²C pull-upy výhradně na 3,3 V. Ověřit, že použitý modul nemá nabíjecí obvod pro nenabíjecí CR2032. |
| LED Wi‑Fi / DATA | 23 / 18 | Zachovat polaritu a rezistory dle zvolených LED a firmware. |
| SERVICE | 19 | Zachovat dle existujícího zapojení; ověřit klidový stav. |
| BAT ADC | 36 | Baterie před 5V boostem, dělič 100k/100k na GND_LOGIC, 100 nF volitelně. Ověřit dovolený rozsah ADC a kalibraci firmware. |

Není navržené nabíjení Li-ion přes USB DevKitu ani přes PCB. Článek má být chráněný a nabíjený schváleným externím 1S nabíječem podle specifikace článku. Pokud se dodatečně zavede USB servisní napájení současně s baterií, musí se zvlášť navrhnout OR-ing / ochrana proti zpětnému proudu; prosté spojení USB 5V a boost 5V není přípustné.

## Datasheety a podklady

- [Everlight EL817-G](https://www.everlight.com/wp-content/plugins/ItemRelationship/product_files/pdf/EL817-G.pdf) – CTR, pinout, izolace.
- [RECOM REC5-RW](https://recom-power.com/pdf/Econoline/REC5-RW.pdf) – varianta `/H4/A`, pinout a filtrace.
- [TI TPS55340](https://www.ti.com/product/TPS55340) a [24V referenční deska](https://www.ti.com/tool/TPS55340EVM-017) – kandidát na step-up, nikoli hotový výrobní návrh pro tuto desku.
- [TI TPS61088](https://www.ti.com/product/TPS61088) – kandidát pro primární 1S→5V, je třeba vlastní návrh/ověřený modul a tepelný návrh.
