# Skrynios, elitiniai monstrai ir laipsniai

## Skrynios

| Skrynia | Lygis | Auksas | Kas viduje | Atsiranda vėl |
|---|---|---|---|---|
| Wooden Crate | 2 | 3–8 | gėrimai, šlamštas, paprasti daiktai | po 2 min |
| Oak Chest | 4 | 10–25 | Uncommon daiktai, Chipped brangakmeniai, Den Key | po 4 min |
| Iron Strongbox | 6 | 30–60 | Rare/Epic daiktai, brangakmeniai, Den Key | po 7 min |

- 8 dėžės prie goblinų stovyklų ir aplink miestelį, 2 skrynios Brute Hills, 2 seifai Orc Marsh.
- Paspaudus prieinama ir atidaroma: auksas iškart į piniginę, daiktai — loot maiše (su afiksais ir lizdais).
- Atidaryta skrynia dingsta, o jos `ChestSpawner` po kurio laiko pastato naują. Virš skrynios — pavadinimas ir lygis, minimape — oranžinis taškas.

## Elitiniai monstrai

- ~8% stovyklų monstrų atsiranda elitiniai (`MonsterSpawner.eliteChance`): vardas „Elite …“,
  25% didesni, 2,5× HP, 1,5× žala, 3× XP ir auksas, loot ridenamas du kartus.

## Laipsniai

- Didžiausias lygis — 25. Kiekvienas lygis turi laipsnį: Recruit, Private, Lance Corporal, … General,
  Field Marshal, Knight, Baron, Count, Earl, Champion (`LevelCurve.RankName`).
- Virš kiekvieno žaidėjo galvos (ir tavo) — vardas, o po juo laipsnis, pvz. „<Corporal>“. Spalvos kaip Dreadmyst: žaidėjai šviesiai mėlyni (#8EACD2), monstrai raudoni (#B63E3D), skrynios beveik baltos (#E8EDE1).
- Laipsnis rodomas ir rėmelyje, veikėjo lange (C) bei žinutėje pakilus lygiu.

Testai: `ChestTests.cs` (skrynios ir elitai), `LevelCurveTests.cs` (laipsniai).
