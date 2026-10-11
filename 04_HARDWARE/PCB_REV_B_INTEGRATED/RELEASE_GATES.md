# Podmínky pro vydání do výroby

1. Zvolit přesné objednací kódy a ověřené footprinty USB-C, svorek, bateriového konektoru, spínače, RTC baterie, všech QFN čipů včetně TUSB320I a RECOM. Potvrdit chráněný 18650 s 10k NTC, jeho maximální nabíjecí proud a teplotní rozsah.
2. Nakreslit nativní schéma v KiCadu včetně přesných hodnot nabíječky, obou primárních měničů, izolovaného 12–24V boostu, 8 optočlenů, auto-programu, ESD a jištění. Každý pin porovnat s výrobním datasheetem a spustit ERC.
3. V KiCadu osadit přesné footprinty do 140×76 mm a odstranit tři prokázané kolize podle `COLLISION_AUDIT.md`; předem fyzicky ověřit otvory v krabičce V1.7, USB výřez, výšku součástek, trasu kabelů od osmi PG7, anténní keep-out a izolaci. Současný `placement.svg` je neplatný pro routování.
4. Vyroutovat silové smyčky podle referenčních layoutů výrobců, dodržet proudové a izolační odstupy, dokončit ochranu 12–24V při odpojeném jezdci trimru a přímém zkratu pole. Spustit DRC, projít výjimky a vyexportovat Gerber, Excellon, BOM s objednacími kódy, osazovací data a 3D kolizi.
5. Na prototypu změřit start a regulaci při 3,0–4,2 V článku, USB bez článku, vybitém článku, se zdrojem inzerujícím default/1,5/3 A, zátěži Wi-Fi, 0 až 8 sepnutých kontaktech, 12 i 24 V, 5m kabelu, zkratu, NTC, teplotě, boot/programování a RTC záloze. Ověřit, kdy USB 0,5 A opravdu nabíjí při provozu.
6. Teprve po těchto měřeních vydat výrobní revizi. Tato složka **nemá výrobní data** a nesmí být označena jako připravená k objednání.
