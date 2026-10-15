# Pasaulis ir balansas pagal Dreadmyst

Asmeninis bandomasis projektas: išdėstymas ir balanso skaičiai paimti iš Dreadmyst kliento duomenų
(`game.db`) ir `Dreadmyst.exe`. Grafika — mūsų pačių.

## Žemėlapis

- Pasaulis apima Dreadmyst žemėlapio „fanadin“ sritį nuo starto iki pagrindinio miesto: x 0–560, y 30–340
  (pas mus 568 × 318 langelių). Duomenys — `Scripts/Editor/DreadmystStartArea.cs` (sugeneruota iš `game.db`).
- **Startas** — Pilgrim's Refuge (jų `start` teleportas 17, 106): čia atsiranda nauji veikėjai ir po mirties.
- **Pagrindinis miestas** — St. Revere (524, 250): prekeiviai stovi ten, kur Dreadmyst sunkiųjų šarvų, šaulių,
  lazdų prekeiviai ir juvelyras.
- **Monstrai** — 277 jų vietose (be miestų), su jų lygiais (1–25) ir atsiradimo laikais (iki 10 min).
  Išvaizda: 1–2 lygis — Goblin, 3–5 — Goblin Brute, aukštesni — Orc. HP ir žala auga su lygiu
  (`MonsterAI.HealthScaleBetween`), jų elitiniai ir bosai pas mus — elitiniai.
- **Skrynios** — 97 jų vietose, su jų lygiais ir atsiradimo laikais (60 s / 5 min).
- **Teleportų akmenys** — jų waypoint ir teleport vietose: Pilgrim's Refuge, Merchant's Way, Fields of Dreams,
  Howler's Valley, Thicket of the Wanderer, St. Revere, Kandorys Passage, Kandorys Castle.
- **Požemio įėjimas** — Wolf's Den vietoje.
- **M** — viso pasaulio žemėlapis (vietos, akmenys, prekeiviai, portalai, žaidėjai ir tu), kaip Dreadmyst.

## Balansas

| Kas | Dreadmyst | Kur |
|---|---|---|
| XP lygiui (1→2: 100, 10→11: 1087, 24→25: 97 380) ir už nužudymą (lygis 1: 20 … 25: 2389) | `player_exp_levels` | `LevelCurve` |
| Elitinių HP ~5,6× | `npc_template` | `MonsterAI.EliteHealthScale` |
| Elitiniai: daugiau medžiagų; bosas: garantuotas auksinis (Rare) + 25 % violetinis (Epic) | `npc_template` loot tikimybės | `MonsterAI`, `LootDrop`, `AffixCatalog.RollQuality` |
| Prekeiviai: Rare 1 vnt., Uncommon 3 vnt., atsinaujina kas 60 s | `npc_vendor` | `VendorDefinition` |
| Atributų kainos XP | `Dreadmyst.exe` | `AttributeRules.PointCost` |
| Klasių atributai ir HP/mana 1 lygyje (karys 75/30, šaulys 60/45, magas 40/70 — pas mus pusė, kitą dalį duoda atributai) | `player_class_stats` | `CharacterClassDefinition` |
| HP ir mana auga × lygis, nuo 21 lygio dar ×1,5 | `player_class_stats` | `LevelCurve.HealthGrowth` |
| Monstrų žala auga taip pat, kaip žaidėjų HP (savo lygio monstras vienodai pavojingas 1 ir 25 lygyje) | — | `MonsterAI.DamageScaleBetween` |
| Afiksai: 685 Dreadmyst afiksai („of the Novice“, „of Magical Vim“...) su jų lygių ribomis ir statais | `affix_template` | `DreadmystAffixes`, `AffixCatalog` |
| Gėrimai: Minor/Lesser/–/Greater/Major Life ir Mana Potion (1, 5, 12, 18, 24 lygis, per 20 s) ir Serum (iš karto), 60 s bendras laukimas, 5 → 1 jungimas | `item_template`, `spell_template`, `crafting_recipes` | `TestSceneBuilder.CreatePotions`, `Inventory` |

Paprastų monstrų numatytosios drop tikimybės yra Dreadmyst serveryje, todėl jų nėra nei `game.db`, nei kliente —
naudojame savo, laiptuotas kaip jų bosų.

## Daiktai pagal lygį

- Kiekvienas ginklas ir šarvas turi lygį (`ItemDefinition.itemLevel`, kaip Dreadmyst `required_level`):
  užsidėti galima tik nuo to lygio, aprašyme — „Requires level N“.
- **Gatavi daiktai nekrenta — jie gaminami (craft) iš medžiagų.** Monstrai ir skrynios meta medžiagas
  **pagal savo lygį**, ta pačia tvarka, kaip Dreadmyst parenka savo daiktų medžiagą (`material_chance_*`):
  nuo 3 lygių žemiau iki 1 aukščiau, dažniausiai savo lygio (`LootDrop.LevelWeight`, `LootDrop.PickMaterial`).
  Pvz. 1 lygio monstras — Bronze Ingot, Aspen Log, Leather Hide, Linen Cloth; Mythril — tik nuo 6 lygio.
- Medžiagos krenta 40 % tikimybe kiekvienam ridenimui, kartais 2 vnt. Elitiniai ridena 2 kartus + 3 papildomus,
  bosai + 6 papildomus ir dar garantuotą auksinį daiktą (+25 % violetinį) — tik bosai meta gatavus daiktus.
  Skrynios ridena 1 (dėžė), 2 (Oak Chest) ar 3 (Strongbox) kartus.
- **Gaminimas** — St. Revere klasių prekeiviai (lange „Craft“): kardas/lankas/lazda ir krūtinė — 6 medžiagos,
  galva ir kojos — 4. Rodomi daiktai, kurių medžiagos turi, aukščiausio lygio, kurį gali dėvėti.
  Kariui — kardai, oda, grandinės, plokštės; šauliui — lankai, oda, grandinės; magui — lazdos, mantijos
  (Dreadmyst `player_desirable_armor`). Pagamintas daiktas kartais geresnis: auksinis 2 %, mėlynas 8 %,
  žalias 25 % (`LootDrop.CraftChances`), 20 % su lizdu brangakmeniui.
- Monstrų ir skrynių lygiai — iš Dreadmyst duomenų (kiekviena vieta turi savo lygį).
- **Daiktai kaip Dreadmyst** (`Scripts/Editor/DreadmystGear.cs`): kiekvienam lygiui 1–25 — tos medžiagos, kurias
  Dreadmyst `material_chance_weapon` / `material_chance_armor` leidžia tame lygyje, su ta pačia tikimybe
  (`ItemDefinition.dropWeight`). Medžiagų pavadinimai — iš `Dreadmyst.exe`:
  - ginklai: kariui kardas (Bronze, Steel, Mythril, Emerald, Crystal, Diamond, Titanium), šauliui lankas ir magui
    lazda (Aspen, Elm, Cherry, Black Walnut, White Oak, Hard Maple, Hickory);
  - šarvai (galva, krūtinė, kojos), tinka visiems: oda (Leather, Brigandine, Gambeson — Cap/Jerkin/Boots),
    grandinės (Bronze, Steel, Mythril, Emerald — Coif/Chainmail/Greaves), plokštės (Crystal, Diamond, Titanium —
    Helmet/Breastplate/Sabatons), mantijos (Linen, Wool, Silk, Spiritsilk — Hood/Vestments/Slippers, duoda manos);
  - žala ir šarvai auga kartu su Dreadmyst pardavimo kaina (ginklas 50 + 10 × lygis + 2 × lygis × (pakopa − 1),
    krūtinė 50 + 2,5 × pakopa × lygis). Pvz. Bronze Sword 1 lygio — +2 žala, Titanium Sword 25 lygio — +21.
  - Medžiagos: metalai — „Ingot“ (ginklams, grandinėms ir plokštėms), mediena — „Log“, oda — „Hide“, audinys — „Cloth“.
  - Seni rankų darbo daiktai lieka tik prekeiviams (retieji) ir senuose išsaugojimuose.
- Spalvos kaip Dreadmyst: žalia → mėlyna → **auksinė** (Epic) → **violetinė** (Legendary, aukščiausia).
  Bosas — garantuotas auksinis + 25 % violetinis.

## Afiksai

- Visi Dreadmyst afiksai yra priesagos: „Steel Sword of the Novice“, „Aspen Bow of Magical Vim“. Imami tie 685, kurių
  status turime (mana, HP, šarvai, Str, Agi, Int, Courage → Vitality).
- Dreadmyst afikso vertė — daugiklis (1,7–3,0), kurį serveris paverčia skaičiumi pagal lygį (formulės kliente nėra).
  Pas mus: atributas = daugiklis × (lygis + 2) / 2 afikso lygių viduryje (Novice, 1–5 lyg.: +5; Elder, 20–25: +34),
  HP ir mana — 3 kartus tiek, šarvai — tiek pat.
- Kokybė pagal statų skaičių: 1 — žalias, 2 — mėlynas, 3+ — auksinis; tik 25 lygio — violetiniai.
- Seni mūsų afiksai lieka daiktams, kurie juos jau turi, bet nebekrenta.

## Gėrimai

- 5 pakopos: Minor (1 lyg.), Lesser (5), – (12), Greater (18), Major (24). Gerti galima tik nuo to lygio;
  5 ir 6 klavišai išgeria geriausią, kurį gali.
- Potion gydo per 20 s (kas 2 s), Serum (žalias) — iš karto ir daugiau. Visi gėrimai turi bendrą 60 s laukimą, kaip Dreadmyst.
- Kiekiai — pusė Dreadmyst (kaip ir mūsų HP): Minor Life Potion 45, Major 200; Minor Life Serum 68, Major 300.
- Monstrai meta gėrimus pagal savo lygį, 15 % — Serum vietoj Potion. Klasių prekeiviai sujungia 5 vienodus į kitą pakopą
  (5 Major Potion → Major Serum).
