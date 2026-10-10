# 15. Kontrola předávacího balíku

Kontrola provedena při vytvoření balíku 7. 10. 2026.

## Obsahově ověřeno

- aktuální serverový zdroj v0.9 obsahuje `UiTheme`, auto-confirm, per-lane TOP 5, Running/Waiting masky, import/export, Confirm/Reject/UNDO a SchemaVersion 5,
- firmware v1.3.1 obsahuje pinout, NVS queue, `esp_timer_get_time()`, status heartbeat, OTA a SERVICE dlouhý stisk,
- 3D V1.7 obsahuje OpenSCAD zdroj i 19 STL tiskových dílů,
- BOM a referenční schémata jsou přenesena z původního transfer balíku,
- starší serverové verze v0.5–v0.8 jsou odděleny do archivu.

## Co nebylo v tomto prostředí provedeno

- nebyl proveden `dotnet build`, protože v pracovním prostředí není .NET SDK,
- firmware nebyl zkompilován pomocí Arduino IDE/arduino-cli,
- nebyla provedena fyzická zkouška s ESP32, M351 ani tlačítky,
- STL nebyla v této revizi znovu slicována ani fyzicky tisknuta.

Před předáním do ostrého provozu projít `09_TEST_PLAN.md`.

## Statické kontroly při balení

- inline JavaScript z `admin.html`, `display.html`, `index.html` a `top.html` prošel `node --check`,
- všechny přiložené ZIP archivy prošly kontrolou integrity `unzip -t`,
- ověřena přítomnost hlavního `Program.cs`, Arduino `.ino` a OpenSCAD zdroje,
- ověřeno 19 STL souborů v tiskové sadě V1.7.
