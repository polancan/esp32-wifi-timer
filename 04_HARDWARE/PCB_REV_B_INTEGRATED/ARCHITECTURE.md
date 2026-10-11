# Elektrický návrh Rev B

## Napájecí cesta

USB-C 5 V vede přes ESD ochranu a vstupní jištění do BQ25606. BQ25606 zajišťuje 1S Li-ion nabíjení a SYS power-path; SYS je při provozu cca 3,5–4,6 V. Chráněný 18650 se připojí na BAT přes klíčovaný konektor spolu s 10k NTC připevněným k článku. Hlavní vypínač SW_MAIN vypíná povolovací vstupy obou systémových měničů, neodpojuje BAT od nabíječky; článek se tak může nabíjet i při vypnutém přístroji. Přesnou polaritu EN, pull-up/pull-down a ochranu před zpětným napájením je nutno uzavřít ve schématu. Z SYS vyrábí TPS63070 stabilních 3,3 V pro ESP32, CP2102N I/O a RTC; TPS61088 vyrábí 5 V pro izolovaný REC5-0505SRW/H4/A. Na jeho sekundární straně TPS55340 zvyšuje 5 V na nastavitelných 12–24 V pro osm kontaktních smyček. LOGIC_GND a FIELD_GND se nikde nespojí. USB VBUS se nesmí spojit přímo s výstupem 5V boostu.

USB D+/D− patří pouze CP2102N pro USB-UART. D+/D− vstupy BQ25606 zůstanou volné, aby nabíječka použila proudový limit ILIM. CC1/CC2 připojit na TUSB320I v režimu UFP podle jeho datasheetu; jeho GPIO výstupy OUT1/OUT2 rozliší výchozí, 1,5A a 3A proud nabízený zdrojem. Bezpečný hardwarový přepínač odporové sítě ILIM musí volit limit <= inzerované hodnoty a bezpečný výchozí stav při vypnutí TUSB320I. Přesné odpory a spínací logika jsou návrhový HOLD. TUSB320I není USB-PD řadič. BQ25606 může doplnit nedostatek výkonu z baterie. Na běžném 0,5A USB hostu při osmi sepnutých kontaktech nebude čisté nabíjení garantováno; na zdroji 1,5/3 A má návrh umožnit nabíjení současně s provozem až po ověření výkonové a tepelné bilance.

VSET nabíječky zvolit pro 4,208 V podle datasheetu, OTG LOW, /CE LOW pro nabíjení. Teplotní síť TS i proud ICHG musí být dopočítány podle konkrétního článku a NTC. USB vstup a 18650 vyžadují jištění a ochranu proti zpětnému proudu. BAT ADC má dělič 100k/100k na GPIO36, měří před měniči. Kapacita 3,3V větve a její transientní odezva se musí ověřit s Wi-Fi.

## Programování a periferie

ESP32-WROOM-32E-N4 je připájen přímo. CP2102N TXD vede na ESP GPIO3/U0RXD, ESP GPIO1/U0TXD na CP2102N RXD; VIO 3,3 V. DTR/RTS se vedou přes dvoutranzistorový auto-program obvod dle [DevKitC V4](https://dl.espressif.com/dl/schematics/esp32_devkitc_v4_sch.pdf), se správnou pravdivostní tabulkou a kondenzátorem alespoň 1 µF na EN. Vedle toho BOOT tlačítko stahuje GPIO0 k zemi, RESET stahuje EN k zemi. Ověřit nezávislost UART při vypnuté napájecí větvi.

DS3231SN je samostatný IC na desce; VCC 3,3 V, VBAT jen CR2032 bez nabíjecí cesty. SDA GPIO21 a SCL GPIO22 mají každý jeden 4,7k pull-up na 3,3 V. Wi-Fi LED GPIO23 a DATA LED GPIO18 jsou aktivní HIGH s vlastními omezovacími odpory. SERVICE je GPIO19 proti zemi.

## Osm galvanicky oddělených vstupů

Každý kanál: FIELD_12_24V → svorka Jn.1 → suché tlačítko → Jn.2 → 2k2/0,5W → anoda LED EL817C-G; katoda na FIELD_GND. Antiparalelní 1N4148 chrání LED před opačnou polaritou. Kolektor fototranzistoru vede na GPIOn a přes 10k na 3,3 V; emitor na LOGIC_GND. Při 12 V se čeká asi 4,9 mA, při 24 V 10,4 mA; na sériovém odporu asi 0,236 W. Všech osm sepnutých větví spotřebuje při 24 V asi 2 W. Je nutná samostatná ochrana FIELD+ proti přímému zkratu na FIELD_GND. Funkci optočlenu při dolní mezi 12 V, teplotě a stárnutí změřit.

Step-up TPS55340 vychází z [24V EVM TI](https://www.ti.com/lit/ug/slvu668b/slvu668b.pdf), 1S→5V boost z [TPS61088 EVM](https://www.ti.com/lit/ug/slvuaf2/slvuaf2.pdf). Referenční zapojení 24 V samo o sobě nedokládá stabilitu při trimru 12–24 V; výkonové stupně, kompenzace a ochrana proti přerušení trimru musí být dokončeny pro oba konce rozsahu. Při osmi stiscích je přibližně 2,5 W z izolovaných 5 V při účinnosti 80 %, tedy 0,5 A; na primární straně izolátoru cca 3,1 W při jeho 80% účinnosti. Celkový systémový příkon bude vyšší o ESP32, RTC a ztráty ostatních měničů.

Mezi LOGIC a FIELD mědí cílit na konstrukční mezeru alespoň 6 mm; pod anténou WROOM bez mědi, baterie a kovu dle datasheetu. Rozměr 140 × 76 mm a otvory (15,7), (125,7), (15,70), (125,70) mm vycházejí z OpenSCAD V1.7, nutno fyzicky ověřit.
