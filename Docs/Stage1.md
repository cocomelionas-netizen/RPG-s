# 1 etapas – izometrinis žemėlapis, kamera, judėjimas

## Ką padarėme

- **Izometrinis pasaulis.** `Grid` su Isometric layout ir 1 × 0.5 langeliu. Joje du tilemap'ai:
  - `Ground` – žolė ir takai, be fizikos, sorting order -100;
  - `Obstacles` – uolos su `TilemapCollider2D`, kraštai uždaryti, kad žaidėjas neišeitų iš žemėlapio.
- **Izometrinis rūšiavimas.** Transparency Sort Mode = Custom Axis (0, 1, 0). Kas žemiau ekrane, tas piešiamas priekyje, todėl veikėjas gali užeiti už uolos.
- **Veikėjas** (`Player.prefab`). `Rigidbody2D` be gravitacijos, mažas `CircleCollider2D` ties kojomis (izometrijoje susiduria tik „pėdos“), `PlayerInputReader` ir `PlayerMovement`.
- **Judėjimas:**
  - WASD arba rodyklės – judama ekrano kryptimis;
  - kairys pelės mygtukas – click-to-move; laikant mygtuką veikėjas seka žymeklį;
  - klaviatūra visada atšaukia pelės tikslą;
  - jei veikėjas atsitrenkia į kliūtį ilgiau nei 0.25 s, pelės tikslas atšaukiamas.
- **Kamera** (`CameraFollow`). Ortografinė, sklandžiai seka žaidėją (SmoothDamp), pelės ratukas keičia priartinimą nuo 2.5 iki 8.
- **Global Light 2D** ir **EventSystem**. EventSystem reikalingas tam, kad paspaudimai per UI nejudintų veikėjo.

## Failai

| Failas | Paskirtis |
|--------|-----------|
| `Assets/_Project/Scripts/Runtime/Controls/PlayerInputReader.cs` | Skaito klaviatūrą ir pelę per Input System |
| `Assets/_Project/Scripts/Runtime/Player/PlayerMovement.cs` | WASD ir click-to-move judėjimas per `Rigidbody2D` |
| `Assets/_Project/Scripts/Runtime/CameraSystem/CameraFollow.cs` | Kameros sekimas ir priartinimas |
| `Assets/_Project/Scripts/Runtime/RPG.Runtime.asmdef` | Runtime kodo assembly |
| `Assets/_Project/Scripts/Editor/Stage1SceneBuilder.cs` | Meniu `RPG > Stage 1 > Build Test Scene`: sukuria 2D Renderer nustatymus, tile'us, prefab'ą ir sceną |
| `Assets/_Project/Scripts/Editor/PlaceholderArtGenerator.cs` | Generuoja laikinus sprite'us |
| `Assets/_Project/Tests/PlayMode/*.cs` | Automatiniai judėjimo ir kameros testai |
| `Assets/_Project/Scenes/World_Test.unity` | Testinė scena |

## Kaip patikrinti

### Rankinis patikrinimas
1. Atidaryk `Assets/_Project/Scenes/World_Test.unity` ir spausk **Play**.
2. **WASD / rodyklės** – veikėjas juda, kamera sklandžiai seka.
3. **Kairys pelės paspaudimas** ant žemės – veikėjas nueina į tą vietą ir sustoja.
4. **Laikyk pelės mygtuką** ir vedžiok – veikėjas seka žymeklį.
5. **Paspausk už uolos** – veikėjas atsitrenkia, trumpai pastovi ir tikslas atšaukiamas (neina „į sieną“ amžinai).
6. **Prieik prie uolų sienelės** (dešinėje virš starto) iš viršaus ir iš apačios. Iš viršaus veikėjas turi būti uždengtas uolos, iš apačios – priekyje jos.
7. **Pelės ratukas** – priartina ir nutolina.
8. **Console** – neturi būti raudonų klaidų. Pranešimas „GPUResidentDrawer Disabled…“ yra normalus su 2D Renderer.

### Automatiniai testai
`Window > General > Test Runner` → skirtukas **PlayMode** → **Run All**. Turi praeiti 6 testai:
- `PlayerMovementTests`: nueina į tikslą, atnaujina žiūrėjimo kryptį, nepraeina pro sieną ir atšaukia tikslą, `Stop()` sustabdo;
- `CameraFollowTests`: kamera pasiveja taikinį ir išlaiko Z gylį, `Follow(..., snap: true)` peršoka iškart.

### Scenos atkūrimas
Jei scena sugadinta: `RPG > Stage 1 > Build Test Scene`. Įrankis viską sugeneruos iš naujo (žemėlapis kaskart toks pats, seed 12345).

## Tikros grafikos keitimas

Placeholder'iai jau paruošti HD tapytai grafikai, todėl tikrus failus galima tiesiog įdėti vietoj jų:
- **256 pixels per unit**, Filter Mode Bilinear, be pixel-art nustatymų;
- **žemės tile'as** – 256 × 128 px rombas, pivot centre;
- **aukšti objektai** (uolos, medžiai) – pivot ties pagrindu, kad rūšiavimas būtų teisingas;
- **veikėjas** – pivot ties kojomis (`SpriteSortPoint.Pivot`).

## Žinomi apribojimai (spręsime vėliau)
- Veikėjas be animacijų (2 etapas).
- Click-to-move eina tiesia linija, kelio paieškos dar nėra (5 etapas, kartu su monstrų AI).
- Viskas veikia tik lokaliai, be tinklo (3 etapas).
