# Příspěvky do projektu

Před větší změnou popište záměr v issue a projděte [roadmapu](05_DOCUMENTATION/13_OPEN_POINTS_AND_ROADMAP.md). Licence zatím není stanovena; podmínky převzetí externích příspěvků je třeba dohodnout s vlastníkem.

Pracujte ve vlastní větvi a změnu předložte jako pull request. Uveďte důvod, provedené kontroly a dopad na konfiguraci či hardware.

Server ověřte příkazem:

```powershell
dotnet build 01_SERVER/SOURCE_v0.9/TimerServer.csproj -c Release
```

U firmware uveďte typ desky, verzi ESP32 core a knihoven i to, zda proběhl test na fyzickém zařízení. U 3D změn přiložte zdroj OpenSCAD a odpovídající STL.

Zachovejte nezávislost drah, lokální měření času, snapshot EntryId při START a idempotentní opakované odesílání DeviceEventId. Nahrávejte pouze ukázková data a zástupné přístupové údaje, nikoli databáze ze soutěží, Wi-Fi hesla nebo skutečné API klíče.
