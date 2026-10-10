# 14. Checklist pro převzetí projektu

Nový správce/vývojář by měl být schopen odškrtnout:

- [ ] Rozumím, že server v0.9 je aktuální a starší ZIPy jsou jen archiv.
- [ ] Rozumím, že firmware v1.3.1 používá NVS, ne LittleFS.
- [ ] Vím, že čas se měří lokálně přes `esp_timer_get_time()`.
- [ ] Vím, že `DEVICE_API_KEY` musí být stejný v serveru a ESP.
- [ ] Vím, že `STATION_ID` je standardně `TIMER01`.
- [ ] Dokážu vytvořit aktivní sérii a importovat startovní listinu.
- [ ] Rozlišuji `Scheduled`, `Pending`, `Completed`, `Skipped`.
- [ ] Rozlišuji výsledky `Pending`, `Confirmed`, `Rejected`, `Undone`.
- [ ] Vím, jak funguje ruční a automatické potvrzení.
- [ ] Vím, že TOP 5 se počítá zvlášť pro každou dráhu.
- [ ] Vím, že display používá `RunningMask` jako kontrolu skutečného STARTu.
- [ ] Vím, že 10k pull-up je požadovaný na všech 8 vstupech.
- [ ] Vím, že GPIO34/35 nemají interní pull-up.
- [ ] Vím, že centrální box V1.7 je stále PG7 a GX16/M12 není rozhodnutý.
- [ ] Před ostrým provozem změním PIN, API key a OTA heslo.
- [ ] Před změnami umím zazálohovat DB a exportovat výsledky.
