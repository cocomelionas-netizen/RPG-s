# 4 etapas – taikinys ir realaus laiko kova

## Ką padarėme

- **Gyvybės (`Health`).** Žaidėjas turi 100 HP, treniruočių manekenas – 200. HP reikšmę laiko ir keičia tik serveris, klientai ją tik skaito.
- **Frakcijos (`Faction`).** Žaidėjai pulti gali tik kitos frakcijos vienetus (monstrus). PvP kol kas išjungtas (11 etapas): kitą žaidėją galima pažymėti, bet ne pulti.
- **Taikinio pasirinkimas** (`PlayerTargeting`):

  | Veiksmas | Kas vyksta |
  |----------|-----------|
  | Kairys pelės mygtukas ant priešo | Pažymi jį ir pradeda pulti: veikėjas prieina į atstumą ir muša pats |
  | Kairys pelės mygtukas ant žemės | Nustoja pulti ir eina ten (taikinys lieka pažymėtas) |
  | WASD | Nustoja pulti (taikinys lieka) |
  | **Tab** | Kitas artimiausias priešas (iki 10 vienetų) |
  | **F** | Pulti pažymėtą taikinį |
  | **Esc** | Atšaukti taikinį |

- **Automatinė ataka (`AutoAttack`).** Ją visą sprendžia serveris. Klientas tik paprašo „pulti šį“, o serveris pats patikrina:
  - ar taikinys priešas ir gyvas;
  - ar atstumas ne didesnis kaip 0.9 vieneto (su 0.35 atsarga dėl vėlavimo);
  - ar praėjo 1.5 s nuo paskutinio smūgio.

  Žalą (8–12, 10 % tikimybė kritinio smūgio ×2) skaičiuoja serveris, o visi mato atakos animaciją. Apgauti suklastotais pranešimais neįmanoma: serveris tikrina savo turimą poziciją, ne tą, kurią praneša klientas.
- **Mirtis ir atgimimas** (`PlayerRespawn`). Kai HP lygus 0:
  - veikėjas tampa pilkas ir pusiau permatomas, judėti negali;
  - po 5 s serveris jį perkelia prie starto su pilnu HP.
- **Treniruočių manekenai** (2 vnt. šalia starto):
  - neatsako smūgiais;
  - palikti ramybėje po 6 s grįžta į pilną HP;
  - „mirę“ po 4 s atsistoja.
- **Vaizdas:**
  - **viršuje** – taikinio rėmelis (vardas, HP juosta, skaičiai);
  - **apačioje** – tavo rėmelis;
  - **virš galvos** – HP juosta, kai vienetas sužeistas arba pažymėtas;
  - **raudonas žiedas** po pažymėtu taikiniu;
  - **žalos skaičiai** kyla ir blunka: balti – tavo žala, geltoni su „!“ – kritinis smūgis, raudoni – žala tau;
  - **raudonas blyksnis** pataikius;
  - **atakos animacija** – 3 kadrai su kardu, 8 kryptimis (vakarų pusė veidrodinė).

## Failai

| Failas | Paskirtis |
|--------|-----------|
| `Scripts/Runtime/Combat/Health.cs` | HP, frakcija, vardas; žala ir atgimimas serveryje |
| `Scripts/Runtime/Combat/Faction.cs` | Frakcijos |
| `Scripts/Runtime/Combat/AutoAttack.cs` | Serverio ataka: atstumas, intervalas, žala, animacija |
| `Scripts/Runtime/Combat/PlayerTargeting.cs` | Pelė, Tab, F, Esc; priėjimas prie taikinio |
| `Scripts/Runtime/Combat/PlayerRespawn.cs` | Žaidėjo atgimimas |
| `Scripts/Runtime/Combat/TrainingDummy.cs` | Manekeno elgsena |
| `Scripts/Runtime/Combat/HealthPresenter.cs` | Juosta virš galvos, blyksnis, žalos skaičiai, mirties vaizdas |
| `Scripts/Runtime/Combat/TargetIndicator.cs` | Žiedas po taikiniu |
| `Scripts/Runtime/UI/UnitFrame.cs` | Tavo ir taikinio rėmeliai |
| `Scripts/Runtime/UI/CombatTextLayer.cs` | Skraidantys žalos skaičiai |
| `Prefabs/World/TrainingDummy.prefab` | Manekenas |
| `Tests/PlayMode/CombatTests.cs` | Kovos testai su tikru Host'u |

Pakeisti failai:
- `PlayerInputReader`: pridėti Tab, F ir Esc.
- `PlayerMovement`: pridėti `FaceTowards()` ir `SuppressClickMoveUntilRelease()` (kad paspaudimas ant priešo kartu nebūtų ir ėjimo komanda).
- `PlayerNetworkMovement`: miręs žaidėjas negali judėti; pridėtas `ServerTeleport()` atgimimui.
- `NetworkBootstrap`: pridėtas viešas `GetSpawnPosition()`.
- Scenos įrankis: sukuria manekenus, HUD ir taikinio žiedą; Player prefab'e dabar yra kovos komponentai ir „Hitbox“ (trigger'is pelės pasirinkimui, judėjimo neblokuoja).

## Kaip patikrinti

1. `RPG > Build Test Scene`, **Play**, **Host**.
2. **Paspausk ant manekeno** (dešinėje nuo starto):
   - po juo atsiras raudonas žiedas, viršuje – jo rėmelis;
   - veikėjas prieis ir pradės mušti: matysi kardo mostą, žalos skaičius ir mažėjančią HP juostą.
3. **Spausk ant žemės** – puolimas sustoja, o rėmelis lieka. **F** – puolimas vėl prasideda.
4. **Tab** perjungia du manekenus, **Esc** atšaukia taikinį.
5. **Numušk manekeną iki 0** – jis tampa pilkas ir po 4 s atsistoja. Palikus jį sužeistą, po 6 s jis atsigydo.
6. **Multiplayer:** `RPG > Run > Local Server` ir `Local Bot Client`, redaktoriuje **Client**. Kova veikia taip pat, o žalą skaičiuoja serveris. Patikrinau: manekenas 200 → 146 → 35 HP per tikrą serverio procesą.

### Automatiniai testai
`Window > General > Test Runner` → **PlayMode** → **Run All** – **43** testai, iš jų 7 nauji kovos testai:
- pilnas HP ir vardas;
- smūgis tik kas 1.5 s;
- per toli – žalos nėra;
- draugo pulti negalima;
- ataka sustoja, kai taikinys miršta;
- manekenas atsistoja;
- žaidėjas miršta, negali judėti ir atgimsta prie starto.

## Kur keisti skaičius
- Žaidėjo žala, atakos greitis, atstumas, kritinio smūgio tikimybė: `Player.prefab` → `AutoAttack` komponentas. 6–7 etapuose tai perkelsime į klases ir ginklus.
- HP: `Health` komponentas (`Max Health`).
- Atgimimo laikas: `PlayerRespawn` ir `TrainingDummy`.

Jei prefab'us keisi rankomis, nepamiršk: `RPG > Build Test Scene` juos sukuria iš naujo.

## Žinomi apribojimai
- Kol kas žaidėją sužeisti gali tik testai, nes manekenai neatsako smūgiais. Tikri monstrai su AI bus 5 etape.
- Ataka tik artimoji (melee). Gebėjimai, nuotolinės atakos ir klasės – 6 etapas.
- UI naudoja paprastą Unity šriftą ir spalvotus stačiakampius. Gražus tapytas UI bus vėliau, kartu su tikra grafika.
