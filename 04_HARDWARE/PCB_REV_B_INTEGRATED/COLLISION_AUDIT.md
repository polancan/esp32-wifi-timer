# Rev B: kontrola kolizí rozmístění

Stav 2026-10-11. Kontrola se vztahuje k `placement.svg`, který kreslí **obálky funkčních zón**, nikoli skutečné courtyardy footprintů. Souřadnice níže jsou v mm od levého horního rohu desky 140 × 76 mm. Výsledek proto odhaluje zjevné chyby, ale nemůže nahradit kontrolu v PCB CADu.

| Závažnost | Místo | Zjištění | Nutná oprava před routováním |
|---|---|---|---|
| Blokující | Pravý horní otvor (125, 7) | Obálka 12–24V měniče x=99–136, y=4–32 **překrývá střed otvoru**; zasahuje i do prostoru hlavy šroubu / sloupku. | Přesunout měnič; zachovat volný kruh dle skutečného sloupku a zvoleného spojovacího materiálu. |
| Blokující | Izolátor / svorky J5 a J6 | Obálka REC5 x=75–109, y=53–72 se překrývá se J5 x=99–107,5, y=51–61 a J6 x=108,5–117, y=51–61. | Změnit uspořádání svorek a izolátoru s jejich skutečnými footprinty a přístupem vodičů. |
| Blokující | RTC + CR2032 | Společná zóna x=30–49, y=45–61 má jen 19 × 16 mm. Samotný článek CR2032 má průměr 20 mm, takže se do ní nevejde, natož držák a DS3231. | Vyhradit samostatný footprint držáku a samostatný prostor pro RTC. |
| K ověření | Dolní levý otvor (15, 70) | Bateriový konektor končí na y=66: od středu otvoru jsou pouze 4 mm. To ponechává 0,4 mm vůči předběžné obálce sloupku Ø7,2 mm, bez výrobní tolerance a těla konektoru. | Zkontrolovat přesný 3D model sloupku, hlavu šroubu a západku konektoru. |
| K ověření | Anténa ESP32 | Modul x=27–47, y=0–25,5 se účelově kryje se značenou RF oblastí x=24–49, y=0–11. Zóna je ale jen poznámka v obrázku; žádná skutečná RF keep-out pravidla ani poloha vůči krabičce neexistují. | Anténu umístit k hraně a převzít keep-out z dokumentace Espressif do CADu včetně všech vrstev a kovových dílů krabičky. |
| K ověření | Osm svorek a kabely | Čtyři páry svorek jsou ve dvou řadách vpravo. Schéma neprokazuje, že se lze dostat šroubovákem k vnitřní řadě a vyvést všech 16 žil od PG7 bez křížení. | Vybrat přesný typ svorek, orientaci šroubů a v CADu zkontrolovat kabelový prostor. |
| K ověření | Optočleny / izolační pás | Osm zón je v kroku 6 mm, ale chybí skutečné DIP-4 courtyardy a definice hranice mědi. Tělo optočlenu i REC5 mohou fyzicky **přemosťovat** izolační pás; jejich piny a měď se přemosťovat nesmějí. | Osadit skutečné footprinty a měřit vzdálenosti mezi FIELD a LOGIC mědí včetně padů, otvorů a všech vrstev. |

Předběžný kruh Ø7,2 mm kolem otvorů vychází z původního rozměru sloupku; není schválenou clearance. Jediný bezpečný závěr nyní je, že **placement obsahuje minimálně tři prokázané mechanické kolize** a routování se nesmí zahájit z tohoto rozmístění. Ostatní odstupy a výšky součástek nelze potvrdit bez přesných footprintů a 3D modelu krabičky.

Podklady pro rozměry: [ESP32-WROOM-32E](https://documentation.espressif.com/esp32-wroom-32e_esp32-wroom-32ue_datasheet_en.html) 18 × 25,5 mm; [RECOM REC5-RW](https://recom-power.com/pdf/Econoline/REC5-RW.pdf) DIP24 31,8 × 20,3 × 10,2 mm; [Everlight EL817-G](https://www.everlight.com/wp-content/plugins/ItemRelationship/product_files/pdf/EL817-G.pdf) standardní DIP-4. Rozměr CR2032 plyne z označení typu; konkrétní držák je nutné vybrat.
