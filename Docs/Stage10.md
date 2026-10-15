# 10 etapas – požemiai (instancijos)

## Ką padarėme

- **Požemis „Goblin Den“.**
  - Įėjimas – violetinis portalas šiek tiek į pietvakarius nuo starto.
  - Spustelėk portalą: veikėjas prieina, ir **visi gyvi žaidėjai iki 3 vienetų nuo portalo** (tavo grupė) kartu perkeliami į naują požemio kopiją.
  - Kitos grupės, įėjusios vėliau, gauna **savo atskirą kopiją**, todėl monstrai ir loot nesidalija tarp grupių.
- **Požemio planas:** 4 kambariai eilėje, sujungti koridoriais. Sienos yra tikros kliūtys, o kelio paieška veikia ir viduje.

  | Kambarys | Kas ten |
  |----------|---------|
  | 1 | Atvykimas ir išėjimo portalas (visada atviras) |
  | 2 | 2 Goblin Brute |
  | 3 | 2 Orc ir 1 Goblin Brute |
  | 4 (didelis) | **Goblin King** ir antras išėjimas, kuris atsidaro tik nugalėjus bosą |

  **Požemio monstrai puola patys**, kai priartėji per ~3,5 vieneto. Lauko monstrai to vis dar nedaro.
- **Bosas Goblin King:** 8 lygis, 900 HP, 14–20 žalos, 300 XP. Kas 9 s (pirmą kartą po 4 s kovos) jis daro **Slam**:
  - aplink jį ant žemės 1,5 s pulsuoja **raudonas žiedas**;
  - po to visi žaidėjai, likę žiedo viduje, gauna 25–35 žalos;
  - spėjęs išbėgti iš žiedo, žalos negauni.
- **Boso loot:** 2 Healing Potion visada, Orcish Greataxe ir Orcish Plate po 50 %, Orc Skull Helm ir Goblin Cleaver po 40 %.
- **Išėjimas:** pro portalą pirmame kambaryje arba, nugalėjus bosą, pro portalą boso kambaryje. Grįžti prie įėjimo pasaulyje. Jei požemyje žūsti, atgimsti prie starto.
- **Požemio uždarymas:** kai požemyje **20 s nebelieka žaidėjų**, serveris jį uždaro kartu su monstrais ir nepaimtu loot. Kitą kartą įėjus, požemis vėl pilnas.
- **Kaip veikia viduje:**
  - požemis yra atskira žemėlapio dalis toje pačioje scenoje, toli nuo pasaulio (nuo x = 500, kas 150 vienetų – nauja kopija);
  - serveris per tinklą siunčia tik kopijos numerį, o kiekvienas klientas pagal tą patį planą (`DungeonLayout`) pats susikuria sienas, grindis, navigaciją ir portalus;
  - monstrus, bosą ir patį požemį valdo serveris;
  - navigacija palaiko kelis žemėlapius vienu metu (`WorldNavigation.At`);
  - per ilgą šuolį (portalas, atgimimas) kamera peršoka kartu su veikėju.
- **Išsaugojimas:** jei atsijungei požemyje, kitą kartą atsirasi prie starto, nes požemio kopija jau bus uždaryta.

## Failai

| Failas | Paskirtis |
|--------|-----------|
| `Scripts/Runtime/Dungeons/DungeonLayout.cs` | Požemio planas langeliais: kambariai, koridoriai, sienos, atvykimo ir išėjimų vietos |
| `Scripts/Runtime/Dungeons/DungeonDefinition.cs` | Požemio aprašas: tile'ai, portalo vaizdas, monstrų grupės, bosas |
| `Scripts/Runtime/Dungeons/DungeonInstance.cs` | Viena kopija (tinklo objektas): numeris, ar bosas nugalėtas, jos monstrai |
| `Scripts/Runtime/Dungeons/DungeonManager.cs` | Kopijų kūrimas, statymas, įėjimas, išėjimas, uždarymas |
| `Scripts/Runtime/Dungeons/Portal.cs`, `DungeonTraveller.cs` | Portalai ir žaidėjo prašymas jais pasinaudoti |
| `Scripts/Runtime/Dungeons/BossMechanics.cs` | Boso Slam su įspėjimu |
| `Data/Dungeons/GoblinDen.asset`, `Prefabs/World/DungeonInstance.prefab`, `Prefabs/Monsters/GoblinKing.prefab` | Sukuria scenos įrankis |
| `Tests/PlayMode/DungeonTests.cs` | Nauji testai |

Pakeisti failai:
- `WorldNavigation`: keli žemėlapiai, `At(position)` vietoj `Instance`.
- `LootPicker`: spustelėtas portalas – prieiti ir pasinaudoti.
- `PlayerNetworkMovement`: kamera peršoka per ilgą teleportą.
- `CombatEffects`: `Telegraph` (pulsuojantis įspėjimo žiedas).
- `CharacterPersistence`: atkuria vietą tik tada, jei ji yra žemėlapyje.
- Scenos įrankis: požemis, bosas, įėjimo portalas, `DungeonManager`, `DungeonTraveller` žaidėjui.

## Kaip patikrinti naršyklėje

https://cocomelionas-netizen.github.io/RPG-s/ (jei rodo seną versiją, perkrauk su Ctrl+F5)

1. Prisijunk. Šalia starto, šiek tiek žemiau ir į kairę, stovi **violetinis portalas**. Spustelėk jį: veikėjas prieina, pasirodo „Entering Goblin Den“, ir atsiduri tamsiame požemyje.
2. Eik į dešinę per koridorius. Monstrai puola patys, kai priartėji. Patartina būti bent ~4–5 lygio ir turėti gėrimų.
3. Paskutiniame kambaryje laukia **Goblin King**. Stebėk **raudoną žiedą** po juo ir spėk iš jo išbėgti.
4. Nugalėjus bosą, iškrenta loot, o boso kambario gale atsidaro (nebėra blyškus) išėjimo portalas. Spustelėk jį: grįši prie įėjimo pasaulyje.
5. Išeik, palauk ~20 s ir vėl įeik: požemis vėl pilnas monstrų.

## Automatiniai testai

**PlayMode** → **Run All**. Pridėti 5 nauji `DungeonTests` testai:
- įėjimas sukuria kopiją su navigacija, monstrais ir bosu, o žaidėjas atsiduria viduje;
- išėjimas grąžina prie įėjimo;
- boso išėjimas uždarytas, kol bosas gyvas;
- tuščia kopija uždaroma kartu su monstrais;
- Slam pataiko į žaidėją žiede tik po įspėjimo.

Iš viso **88** testai. Kodas patikrintas .NET kompiliatoriumi, PlayMode testai debesyje nepaleisti.

## Kur keisti
- Kambariai ir koridoriai: `DungeonLayout`.
- Monstrų grupės, bosas, tile'ai: `TestSceneBuilder.CreateDungeon`.
- Boso stiprumas ir loot: `TestSceneBuilder` („Goblin King“).
- Slam: `GoblinKing.prefab` → `BossMechanics`.
- Grupės spindulys, uždarymo laikas: scenoje `Dungeon Manager`.

## Žinomi apribojimai
- Tikros grupių (party) sistemos nėra. Grupė – tai visi, kas stovi prie portalo.
- Vienas požemis, vienas planas. Atsitiktinai generuojamų požemių nėra.
- Visos kopijos yra toje pačioje scenoje, todėl klientai gauna informaciją ir apie kitų grupių monstrus (interest management dar nėra). Kai žaidėjų bus daug, to reikės.
- Lauko monstrų puolimas iš toli tebėra išjungtas, kaip prašei.

## Įėjimo sąlygos

- `DungeonDefinition.minLevel` (Goblin Den: 3), `key` (Den Key) ir `maxPlayers` (4).
- Portalą paspaudęs žaidėjas turi būti reikiamo lygio ir turėti raktą — vienas raktas sunaudojamas.
  Kiti žaidėjai prie portalo eina kartu, jei atitinka lygį (rakto jiems nereikia), iki `maxPlayers`.
- Atsisakymo priežastis rodoma virš galvos ir žinučių lange („You need a Den Key to enter Goblin Den“).
- Den Key krenta iš monstrų: Goblin 4%, Goblin Brute 25%, Orc 35%.

## Ribotos prekeivių atsargos

- `VendorDefinition.stockLimits` (0 = neribota) ir `restockSeconds` (300 s).
  Klasių prekeiviai: Rare daiktai — 1 vnt., Uncommon — 2 vnt., Common ir gėrimai — neriboti.
- Likutį saugo serveris (`Vendor.ServerRemaining`), klientas jį gauna atidaręs langą ir po pirkimo;
  lange rodoma „(2 left)“ arba „(sold out)“.
