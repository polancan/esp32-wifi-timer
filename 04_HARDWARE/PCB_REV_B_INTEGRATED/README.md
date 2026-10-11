# Integrovaná PCB Rev B — ESP32 Wi-Fi časomíra

Stav 2026-10-11: návrhové podklady, **zatím neobjednávat PCB**. Tato revize nahrazuje zásuvný DevKit z PCB_REV_A pájeným modulem ESP32-WROOM-32E-N4. Požadavek je jedna základní deska s USB-C programováním, nabíjením 18650 a provozem při nabíjení; USB-C CC režim hlídá TUSB320I, osm opticky oddělených smyček 12–24 V, RTC, stavovými LED, servisními tlačítky a expanzním headerem.

Soubory: `SCHEMATIC_DRAFT.md` je pracovní elektrické zapojení po čtyřech listech, `COLLISION_AUDIT.md` zaznamenává prokázané kolize; `ARCHITECTURE.md` popisuje architekturu, `PINOUT.md` mapuje GPIO, `BOM.csv` uvádí komponenty a otevřené volby, `NETS.csv` klíčové sítě, `placement.svg` ukazuje **neplatné** předběžné rozmístění, `RELEASE_GATES.md` obsahuje úkoly před výrobou.

Toto **není hotový CAD projekt**. Chybí nativní schéma, uzavřené hodnoty některých výkonových obvodů, přesné footprinty, routování, ERC/DRC, Gerber/Excellon a test prototypu. SVG je obálkový placement a má nejméně tři mechanické kolize; nepoužívat jej jako základ routování. Všechny elektronické obvody jsou koncipované na jedné PCB; vyměnitelná baterie s teplotním čidlem se připojí klíčovaným třížilovým konektorem. USB konektor vyžaduje nový otvor v krabičce V1.7.

Firmware v 02_FIRMWARE/SOURCE_v1.3.1 určuje osm vstupních GPIO 32,33,25,26,27,14,34,35, RTC 21/22, LED 23/18, SERVICE 19 a BAT ADC 36. Tato revize zachovává jejich význam a polaritu.

Primární zdroje: [ESP32-WROOM-32E](https://documentation.espressif.com/esp32-wroom-32e_esp32-wroom-32ue_datasheet_en.html), [Espressif DevKitC V4](https://dl.espressif.com/dl/schematics/esp32_devkitc_v4_sch.pdf), [CP2102N](https://www.silabs.com/documents/public/data-sheets/cp2102n-datasheet.pdf), [TUSB320I](https://www.ti.com/lit/ds/symlink/tusb320.pdf), [BQ25606](https://www.ti.com/lit/ds/symlink/bq25606.pdf), [DS3231](https://www.analog.com/media/en/technical-documentation/data-sheets/DS3231.pdf), [REC5-RW](https://recom-power.com/pdf/Econoline/REC5-RW.pdf), [EL817-G](https://www.everlight.com/wp-content/plugins/ItemRelationship/product_files/pdf/EL817-G.pdf).
