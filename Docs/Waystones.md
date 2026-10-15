# Zonos ir teleportų akmenys

## Zonos

- `Zone` (vardas + spindulys) pažymi pasaulio vietas; kur zonos persidengia, laimi mažesnė.
  Už zonų ribų naudojamas žemėlapio vardas (`WorldNavigation.ZoneName`: „Greenvale Wilds“, požemyje — jo vardas).
- Zonos: **Greenvale** (miestelis), **Brute Hills** (rytai), **Northern Ridge** (šiaurė), **Orc Marsh** (vakarai), **Southern Fen** (pietūs).
- Įėjus į kitą zoną ekrano viršuje parodomas didelis pavadinimas (`ZoneBanner`) ir žinutė „Entering ...“; minimapas rodo dabartinę zoną.

## Teleportų akmenys (Waystones)

- Kiekvienoje zonoje stovi akmuo su švytinčiomis runomis (minimape — žydras taškas).
- Priėjus arčiau nei 3 vienetai akmuo **atrandamas** („Waystone discovered: ...“).
- Paspaudus akmenį atsidaro sąrašas: į atrastus akmenis galima keliauti už **10 aukso**, neatrasti — pilki.
- Serveris tikrina: žaidėjas stovi prie akmens, tikslas atrastas, užtenka aukso (`WaystoneTraveller.ServerTravel`).
- Atrasti akmenys saugomi paskyroje (`CharacterRecord.waystones`, bitai pagal akmens `Id`; id niekada nekeisti).

Testai: `Tests/PlayMode/WaystoneTests.cs`, papildytas `AccountTests`.
