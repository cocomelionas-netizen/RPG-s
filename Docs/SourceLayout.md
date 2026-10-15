# Pasaulis ir balansas pagal Source

Asmeninis bandomasis projektas: išdėstymas ir balanso skaičiai paimti iš Source kliento duomenų
(`game.db`) ir `Source.exe`. Grafika — mūsų pačių.

## Žemėlapis

- Pasaulis apima Source žemėlapio „fanadin“ sritį nuo starto iki pagrindinio miesto: x 0–560, y 30–340
  (pas mus 568 × 318 langelių). Duomenys — `Scripts/Editor/SourceStartArea.cs` (sugeneruota iš `game.db`).
- **Startas** — Pilgrim's Refuge (jų `start` teleportas 17, 106): čia atsiranda nauji veikėjai ir po mirties.
- **Pagrindinis miestas** — St. Revere (524, 250): prekeiviai stovi ten, kur Source sunkiųjų šarvų, šaulių,
  lazdų prekeiviai ir juvelyras.
- **Monstrai** — 277 jų vietose (be miestų), su jų lygiais (1–25) ir atsiradimo laikais (iki 10 min).
  Išvaizda: 1–2 lygis — Goblin, 3–5 — Goblin Brute, aukštesni — Orc. HP ir žala auga su lygiu
  (`MonsterAI.HealthScaleBetween`), jų elitiniai ir bosai pas mus — elitiniai.
- **Skrynios** — 97 jų vietose, su jų lygiais ir atsiradimo laikais (60 s / 5 min).
- **Teleportų akmenys** — jų waypoint ir teleport vietose: Pilgrim's Refuge, Merchant's Way, Fields of Dreams,
  Howler's Valley, Thicket of the Wanderer, St. Revere, Kandorys Passage, Kandorys Castle.
- **Požemio įėjimas** — Wolf's Den vietoje.
- **M** — viso pasaulio žemėlapis (vietos, akmenys, prekeiviai, portalai, žaidėjai ir tu), kaip Source.

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
