# Mechanika a layout PCB Rev A

## Obálka a osazení

Původní OpenSCAD V1.7 má vnitřní krabici 160 × 110 × 50 mm a vyjímatelnou vaničku 140 × 76 × 1,8 mm. Čtyři podpory vaničky jsou po jejím vložení na souřadnicích lokálně **(15,7), (125,7), (15,70), (125,70) mm**; originální otvory mají průměr 3,4 mm. Pro PCB uvažovat obrys max. **140 × 76 mm**, tloušťku 1,6 mm FR-4 a stejné středové souřadnice otvorů Ø3,4 mm bez pokovení, pokud deska vaničku nahrazuje. Je-li PCB nad vaničkou na distancích, musí se zvlášť ověřit použitelná výška a šrouby. Pozice vznikly z [OpenSCAD zdroje](../../03_3D/SOURCE_V1.7/stopwatch_3d_models_v1_7_source/stopwatch_enclosures_v1_7.scad), nikoli z fyzického měření tiskové tolerance.

Zadní řada průchodek PG7 je v globálních X `14,5 + 18,7 × i` mm, `i=0…7`, průměr 12,5 mm, ve stěně Y=110 mm. Při desce posazené s počátkem na X=10,Y=10 končí zadní hrana PCB u Y=86 mm; zbývající prostor mezi deskou a zadní stěnou je přibližně 24 mm pro matice a ohyb kabelů. Osm svorkovnic J1–J8 směřovat k této hraně a označit 1 až 8 ve stejném pořadí jako PG7. Nepočítat s mechanickou podporou od kabelů. Držák 18650 nesmí blokovat konektory ani anténu ESP32.

## Rozdělení plochy

```text
Pohled shora, USB/ovládání vpředu, kabely PG7 vzadu
┌──────────────────────────── 140 mm ────────────────────────────┐
│  logic: ESP32 + RTC + LED      │  izolační bariéra  │  FIELD    │
│  baterie / 1S→5V boost         │   ≥6 mm bez mědi    │ DC/DC +   │
│                                │                     │ boost 24V │
│                                │  U1…U8 překračují   │ R1…R8    │
│                                │  bariéru jen uvnitř │ J1…J8    │
└─────────────────────────────────────────────────────────────────┘
                                      zadní hrana → PG7
```

Toto je funkční zónování, **ne rozměrový placement**. V KiCadu nejdříve vložit přesné 3D modely a reálné hranice ESP32, izolátoru, obou boostů, konektorů a baterie; pokud se nevejdou do 140 × 76 mm, řešit druhou desku nebo změnu vaničky před routováním.

- Spojení GND_LOGIC a F_GND zakázat. Mezi měděnými polygonami a spoji obou domén nechat konstrukční mezeru nejméně 6 mm; pod optočleny a izolátorem bez cizí mědi. Požadovaná izolační vzdálenost pro finální prostředí se stanoví normativním posouzením; 6 mm je interní návrhový požadavek, nikoli tvrzení o certifikaci.
- 24V vedení a všech osm dvouvodičových smyček držet v zóně FIELD. Vstupní omezovací rezistory a volitelné TVS umístit co nejblíže ke svorkám. Vést čísla drah srozumitelně na sítotisku i u svorek.
- Anténa ESP32 má zůstat u nekovového okraje skříně, bez mědi a baterie pod anténou podle požadavků konkrétního DevKitu. Umístit USB konektor přístupně pro servis a ověřit, že kabel nenaráží na víko.
- Spínané smyčky obou boostů vést malé, krátké, podle referenčních layoutů výrobců; nesměřovat pod ESP anténu ani pod nízkoúrovňové RTC/ADC signály. Kondenzátory těsně k napájecím pinům. U primárního měniče a konektoru baterie dimenzovat měď pro nejméně 2 A s tepelnou rezervou, nikoli podle pouhé logické šířky spojů.
- Ochrana pole a měřicí body musí být dosažitelné po sejmutí víka. Nastavovací trimr nesmí být přístupný uživateli během provozu nebo musí být opatřen plombou.

Rozměr USB, rozteč řad a celková šířka „30pin DevKit V1“ nejsou standardizovány; footprint se vytvoří teprve z konkrétní série. Obrys 140 × 76 mm lze považovat za obálku, nikoli za prokázané osazení všech součástek.
