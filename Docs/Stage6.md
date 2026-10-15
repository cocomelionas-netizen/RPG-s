# 6 etapas – klasės, gebėjimai, lygiai ir patirtis

## Ką padarėme

- **Trys klasės:**

  | Klasė | HP (1 lyg. / +lyg.) | Ataka | Gebėjimai (1 / 2 / 3) |
  |-------|---------------------|-------|------------------------|
  | **Warrior** (mėlynas apsiaustas) | 110 / +11 | artima, 8–12 žalos kas 1.5 s | **Heavy Strike** – ×2.2 žalos taikiniui (6 s); **Whirlwind** – ×1.3 visiems aplink (spindulys 1.4, 10 s); **Second Wind** – +30 % HP (30 s) |
  | **Ranger** (žalias) | 90 / +9 | iš 5 vienetų, 6–9 kas 1.7 s, 15 % kritinio smūgio | **Aimed Shot** – ×2.2 iš 6 vienetų (7 s); **Volley** – ×1.2 visiems aplink taikinį (10 s); **Mend** – +25 % HP (30 s) |
  | **Mage** (violetinis) | 80 / +8 | iš 4.5 vieneto, 5–8 kas 1.8 s | **Fireball** – ×2.8 iš 5.5 vieneto (5 s); **Flamestrike** – ×1.8 visiems aplink taikinį (12 s); **Frost Nova** – ×1.4 visiems aplink save (14 s) |

  Žala didėja +1 (Ranger ir Mage +0.8) už kiekvieną lygį. Nuotolinės klasės šaudo spalvotą sviedinį.
- **Klasės pasirinkimas.** Dešinėje viršuje yra mygtukai **Warrior / Ranger / Mage**. Kol kas klasę galima keisti bet kada, kad būtų patogu testuoti. Lygis ir patirtis išlieka. Kai 8 etape atsiras paskyros, klasė bus renkama kuriant veikėją.
- **Veiksmų juosta** (pataisyta pagal tavo pastabą):
  - paspaudus ant monstro, jis tik **pažymimas** – veikėjas nepuola;
  - **1 – Attack:** veikėjas pribėga prie pažymėto priešo ir muša. Be taikinio rodoma „No enemy target“. **F** veikia taip pat;
  - **2, 3, 4 – gebėjimai.** Jei taikinys per toli, veikėjas pats pribėga į gebėjimo atstumą ir tada jį panaudoja. Mygtukas tuo metu paryškintas auksu. Bėgimą nutraukia WASD, paspaudimas ant žemės, Esc arba kito taikinio pasirinkimas.
- **Gebėjimų mygtukuose** matyti:
  - pavadinimas;
  - tamsėjantis cooldown sluoksnis su sekundėmis;
  - pranešimai „Out of range“, „No enemy target“, „... is not ready“.
- **Viską tikrina serveris.** Jis patikrina, ar:
  - esi gyvas;
  - gebėjimas paruoštas (savo cooldown ir bendras 1 s);
  - taikinys yra gyvas priešas ir pakankamai arti.

  Tada serveris apskaičiuoja žalą ir gydymą. Klientas tik paprašo, todėl apgauti cooldown'o ar atstumo neįmanoma. Ploto gebėjimai pataiko visiems priešams spindulyje.
- **Patirtis ir lygiai** (iki 20):
  - goblinas (2 lygio) duoda 20 XP **kiekvienam** žaidėjui, kuris su juo kovojo;
  - jei esi aukštesnio lygio už monstrą, gauni 20 % mažiau XP už kiekvieną lygio skirtumą (mažiausiai 10 %);
  - 2 lygiui reikia 60 XP, 3 – 133, 5 – 340, 10 – 1100.

  Pakilus lygiu, didėja HP ir žala, o HP užsipildo. Rodomas auksinis žiedas ir užrašas „Level N!“.
- **HUD:**
  - apačioje – patirties juosta („Level 2 Warrior – 45 / 133 XP“);
  - virš savo rėmelio – gebėjimų mygtukai;
  - rėmeliuose rodomas lygis ir klasė, pvz. „Player 1 Lv 3 Ranger“ arba „Goblin Lv 2“;
  - skraidantys užrašai: „+20 XP“ (violetinis), „+33“ gydymas (žalias), „Level 3!“ (auksinis).
- **Efektai.** Sviediniai, besiplečiantys žiedai ant žemės (Whirlwind, Frost Nova, Volley, Flamestrike) ir gydymo žiedas. Jie rodomi kiekviename kliente, bet tik vaizdui: pataikymą sprendžia serveris.

## Failai

| Failas | Paskirtis |
|--------|-----------|
| `Scripts/Runtime/Progression/CharacterClassDefinition.cs` | Klasė: išvaizda, rodikliai 1 lygyje, augimas, gebėjimai |
| `Scripts/Runtime/Progression/AbilityDefinition.cs` | Gebėjimas: rūšis, cooldown, atstumas, spindulys, žalos daugiklis, gydymas, spalva |
| `Scripts/Runtime/Progression/ClassCatalog.cs` | Visų klasių sąrašas (žaidėjas saugo klasės numerį) |
| `Scripts/Runtime/Progression/LevelCurve.cs` | Kiek XP reikia lygiui, XP už monstrą |
| `Scripts/Runtime/Progression/CharacterProgress.cs` | Žaidėjo klasė, lygis, XP (serveris), rodiklių pritaikymas visuose klientuose |
| `Scripts/Runtime/Progression/AbilityCaster.cs` | Gebėjimų naudojimas: kliento prašymas, serverio patikra ir poveikis, cooldown'ai |
| `Scripts/Runtime/Combat/CombatEffects.cs` | Sviediniai ir žiedai |
| `Scripts/Runtime/UI/AbilityBar.cs`, `ExperienceBar.cs`, `ClassPicker.cs`, `UIFactory.cs` | Nauji HUD elementai (UI susikuria patys) |
| `Data/Classes/*.asset`, `Data/Abilities/*.asset` | Klasės ir gebėjimai (sukuria scenos įrankis) |
| `Tests/PlayMode/LevelCurveTests.cs`, `ProgressionTests.cs` | Nauji testai |

Pakeisti failai:
- `Health`:
  - viešas visų vienetų sąrašas `Spawned`;
  - `ServerSetMaxHealth`;
  - `Heal(amount, announce)` su įvykiu `Healed`.
- `AutoAttack`:
  - `SetStats` ir `RollDamage`;
  - nuotolinės atakos sviedinys.
- `MonsterAI`: monstro lygis ir XP atlygis visiems kovojusiems.
- `PlayerInputReader`: klavišai 1–5.
- `UnitFrame`: lygis ir klasė.
- `HealthPresenter`: žali gydymo skaičiai.
- `CombatTextLayer`: `ShowText`.
- Scenos įrankis:
  - Ranger ir Mage išvaizda;
  - klasių ir gebėjimų asset'ai;
  - `CharacterProgress` ir `AbilityCaster` žaidėjui;
  - goblino lygis ir XP;
  - `Combat Effects` objektas ir nauji HUD elementai.

## Kaip patikrinti naršyklėje

https://cocomelionas-netizen.github.io/RPG-s/ (jei rodo seną versiją, perkrauk su Ctrl+F5)

1. **HUD.** Apačioje turi būti patirties juosta „Level 1 Warrior – 0 / 60 XP“, virš tavo rėmelio – 3 gebėjimų mygtukai, o dešinėje viršuje – klasių mygtukai.
2. **Warrior.** Prieik prie manekeno ir pažymėk jį:
   - **1** (Attack) – veikėjas pribėga ir muša;
   - **2** (Heavy Strike) – didelis skaičius ir 6 s cooldown ant mygtuko;
   - **3** (Whirlwind) – žiedas aplink tave, pataiko visiems arti;
   - **4** (Second Wind) – žalias „+N“ po to, kai gavai žalos.
3. **Taikinys ir atstumas.** Spausk **1** be taikinio – parašoma „No enemy target“. Pažymėk toli esantį gobliną ir spausk **2** – veikėjas pribėga ir smogia.
4. **Ranger.** Pasirink jį dešinėje viršuje: veikėjas tampa žalias, HP pasikeičia, o atakuojama iš toli su sviediniais. Išbandyk **2** ir **3**.
5. **Mage.** Pasirink jį: veikėjas tampa violetinis. Išbandyk Fireball, Flamestrike ir Frost Nova.
6. **Lygiai.** Numušk 3 goblinus: kiekvienas duoda „+20 XP“, o po trečio pasirodo „Level 2!“. HP padidėja ir užsipildo.

## Automatiniai testai

Unity redaktoriuje: `RPG > Build Test Scene`, tada `Window > General > Test Runner` → **PlayMode** → **Run All**. Turi būti **68** testai, iš jų 11 naujų:
- `LevelCurveTests`: XP kreivė, keli lygiai iš karto, maksimalus lygis, mažesnis XP už žemesnio lygio monstrus;
- `ProgressionTests`:
  - startas kaip 1 lygio Warrior;
  - lygio pakilimas padidina HP ir išgydo;
  - klasės keitimas pakeičia rodiklius;
  - XP už monstrą;
  - gebėjimo cooldown;
  - per toli arba be taikinio;
  - ploto gebėjimas pataiko tik artimiems;
  - gydymas.

Kaip ir 5 etape, kodas patikrintas .NET kompiliatoriumi, o XP kreivė – atskirai. PlayMode testų debesyje paleisti negaliu.

## Kur keisti skaičius
- Klasių rodikliai ir gebėjimai: `TestSceneBuilder.CreateClasses`. Asset'us `Data/Classes`, `Data/Abilities` scenos įrankis kaskart perrašo.
- XP kreivė ir XP mažinimas: `LevelCurve`.
- Goblino lygis ir XP: `TestSceneBuilder.CreateGoblinPrefab`.
- Bendras cooldown: `AbilityCaster.GlobalCooldown`.

## Žinomi apribojimai
- **Progresas neišsaugomas.** Perkrovus puslapį ar atsijungus, vėl esi 1 lygio. Saugojimas serveryje bus 8 etape.
- Kol kas yra tik vieno tipo monstrai (2 lygio goblinai), todėl nuo ~4 lygio XP gaunama labai lėtai. Stipresnių monstrų reikės kartu su loot (7 etapas).
- Nėra manos ar energijos: gebėjimus riboja tik cooldown'ai.
- Gebėjimai neturi būsenų efektų (sulėtinimas, apsvaiginimas) ir yra momentiniai, be „cast“ laiko.
- Klasių mygtukai skirti testavimui. Kai atsiras paskyros, klasė bus renkama kuriant veikėją.
