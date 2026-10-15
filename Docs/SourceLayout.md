# Pasaulis ir balansas pagal Source

Asmeninis bandomasis projektas: išdėstymas ir balanso skaičiai paimti iš Source kliento duomenų
(`game.db`) ir `Source.exe`. Grafika — mūsų pačių.

## Žemėlapis

- Pasaulis — Source startinės zonos dydžio: jų žemėlapio „fanadin“ sritis x 0–250, y 30–260
  (pas mus 256 × 236 langelių vietoj 80 × 80). Duomenys — `Scripts/Editor/SourceStartArea.cs`
  (sugeneruota iš `game.db`).
- Miestelis (prekeiviai, manekenai) — jų starto vietoje (`start` teleportas 17, 106).
- **Monstrai** — 87 jų vietose, atsiranda iš naujo per tiek pat, kiek pas juos (Antling 5 s, kiti 60 s,
  elitiniai 90 s). Jų lygiai 1–2 → Goblin, 3–4 → Goblin Brute, 5–6 → Orc; jų elitiniai (Antling Broodmother,
  Drauglyen) pas mus visada elitiniai. Aukštesnio lygio (11–25) monstrų kol kas nėra.
- **Skrynios** — 34 jų vietose: dėžės ir statinės → Wooden Crate (atsiranda po 60 s), skrynios iki 4 lygio →
  Oak Chest, aukštesnės → Iron Strongbox (po 5 min).
- **Teleportų akmenys** — miestelyje ir jų waypoint vietose; zonos: Pilgrim's Refuge, Merchant's Way,
  Fields of Dreams, Howler's Valley, Thicket of the Wanderer, Wolf's Den, Trog Cave (kai kurių zonų vardai
  priskirti pagal artimiausią vietą). Seni atrasti akmenys gali atitikti kitus akmenis.
- **Požemio įėjimas** — Wolf's Den vietoje.

## Balansas

| Kas | Source | Kur |
|---|---|---|
| XP lygiui (1→2: 100, 10→11: 1087, 24→25: 97 380) ir už nužudymą (lygis 1: 20 … 25: 2389) | `player_exp_levels` | `LevelCurve` |
| Elitinių HP ~5,6× | `npc_template` | `MonsterAI.EliteHealthScale` |
| Elitiniai: garantuotas žalias arba mėlynas daiktas; bosas: garantuotas auksinis (Rare) + 25 % violetinis (Epic) | `npc_template` loot tikimybės | `MonsterAI`, `LootDrop`, `AffixCatalog.RollQuality` |
| Prekeiviai: Rare 1 vnt., Uncommon 3 vnt., atsinaujina kas 60 s | `npc_vendor` | `VendorDefinition` |
| Atributų kainos XP | `Source.exe` | `AttributeRules.PointCost` |

Paprastų monstrų numatytosios drop tikimybės yra Source serveryje, todėl jų nėra nei `game.db`, nei kliente —
naudojame savo, laiptuotas kaip jų bosų.

## Daiktai pagal lygį

- Kiekvienas ginklas ir šarvas turi lygį (`ItemDefinition.itemLevel`, kaip Source `required_level`):
  užsidėti galima tik nuo to lygio, aprašyme — „Requires level N“.
- Monstrai ir skrynios meta daiktus **pagal savo lygį** (kaip Source `material_chance_*` lentelės):
  nuo 3 lygių žemiau iki 1 aukščiau, dažniausiai savo lygio (`LootDrop.LevelWeight`). Pvz. 3 lygio monstras —
  1–4 lygio daiktai; 25 lygio — iki 25. Kol aukštesnių daiktų nėra, krenta aukščiausi esami.
- Monstrų ir skrynių lygiai — iš Source duomenų (kiekviena vieta turi savo lygį).
- Kokybė kiekvienam ridenimui: auksinis 0,3 %, mėlynas 2 %, žalias 8 %, baltas 6 % (`LootDrop.GearChances`);
  paprastesnis daiktas pakeliamas iki tos kokybės afiksu. Skrynios ridena 1 (dėžė), 2 (Oak Chest) ar 3 (Strongbox) kartus.
- Spalvos kaip Source: žalia → mėlyna → **auksinė** (Epic) → **violetinė** (Legendary, aukščiausia).
  Bosas — garantuotas auksinis + 25 % violetinis; elitiniai — garantuotas žalias arba mėlynas.
