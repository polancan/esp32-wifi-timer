# GPIO a expanzní header

| Funkce | GPIO | Stav |
|---|---|---|
| Dráhy 1–8 | 32,33,25,26,27,14,34,35 | optočlen, aktivní LOW, každý externí 10k pull-up |
| RTC SDA/SCL | 21/22 | obsazené |
| Wi-Fi / data LED | 23/18 | obsazené |
| SERVICE | 19 | obsazený |
| BAT ADC | 36 | obsazený, pouze vstup |
| USB UART TX/RX | 1/3 | obsazené |
| Běžné volné GPIO | 4,13,16,17 | expanzní header |
| Volný vstup | 39 | expanzní header, pouze vstup bez interního pull-upu |
| Boot strapping GPIO | 0,2,5,12,15 | expanzní header s varováním; neovlivnit úroveň při resetu |
| Interní flash piny | 6–11 | nevyvádět |

Header J_EXP: 2×7, 2,54 mm. Piny: 1=3V3_OUT, 2=LOGIC_GND, 3=GPIO4, 4=GPIO13, 5=GPIO16, 6=GPIO17, 7=GPIO39_IN, 8=GPIO0_BOOT, 9=GPIO2_BOOT, 10=GPIO5_BOOT, 11=GPIO12_BOOT, 12=GPIO15_BOOT, 13=EN_RESET, 14=LOGIC_GND. Polohu pinu 1 a polarizaci určit při volbě footprintu. 3V3_OUT má jen proudovou rezervu zvoleného regulátoru, kterou určí měření. GPIO0/2/5/12/15 nesmí externí zařízení při bootu vynutit na nevhodnou úroveň, zejména GPIO12 ovlivňuje flash. Sériové 0Ω footprinty pro jejich odpojení jsou vhodné.

Zdroj: [ESP32-WROOM-32E datasheet](https://documentation.espressif.com/esp32-wroom-32e_esp32-wroom-32ue_datasheet_en.html).
