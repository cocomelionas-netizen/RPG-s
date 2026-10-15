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
| Elitiniai: daugiau medžiagų; bosas: garantuotas auksinis (Rare) + 25 % violetinis (Epic) | `npc_template` loot tikimybės | `MonsterAI`, `LootDrop`, `AffixCatalog.RollQuality` |
| Prekeiviai: Rare 1 vnt., Uncommon 3 vnt., atsinaujina kas 60 s | `npc_vendor` | `VendorDefinition` |
| Atributų kainos XP | `Source.exe` | `AttributeRules.PointCost` |
| Klasių atributai ir HP/mana 1 lygyje (karys 75/30, šaulys 60/45, magas 40/70 — pas mus pusė, kitą dalį duoda atributai) | `player_class_stats` | `CharacterClassDefinition` |
| HP ir mana auga × lygis, nuo 21 lygio dar ×1,5 | `player_class_stats` | `LevelCurve.HealthGrowth` |
| Monstrų žala auga taip pat, kaip žaidėjų HP (savo lygio monstras vienodai pavojingas 1 ir 25 lygyje) | — | `MonsterAI.DamageScaleBetween` |
| Afiksai: 685 Source afiksai („of the Novice“, „of Magical Vim“...) su jų lygių ribomis ir statais | `affix_template` | `SourceAffixes`, `AffixCatalog` |
| Gėrimai: Minor/Lesser/–/Greater/Major Life ir Mana Potion (1, 5, 12, 18, 24 lygis, per 20 s) ir Serum (iš karto), 60 s bendras laukimas, 5 → 1 jungimas | `item_template`, `spell_template`, `crafting_recipes` | `TestSceneBuilder.CreatePotions`, `Inventory` |

Paprastų monstrų numatytosios drop tikimybės yra Source serveryje, todėl jų nėra nei `game.db`, nei kliente —
naudojame savo, laiptuotas kaip jų bosų.

## Daiktai pagal lygį

- Kiekvienas ginklas ir šarvas turi lygį (`ItemDefinition.itemLevel`, kaip Source `required_level`):
  užsidėti galima tik nuo to lygio, aprašyme — „Requires level N“.
- **Gatavi daiktai nekrenta — jie gaminami (craft) iš medžiagų.** Monstrai ir skrynios meta medžiagas
  **pagal savo lygį**, ta pačia tvarka, kaip Source parenka savo daiktų medžiagą (`material_chance_*`):
  nuo 3 lygių žemiau iki 1 aukščiau, dažniausiai savo lygio (`LootDrop.LevelWeight`, `LootDrop.PickMaterial`).
  Pvz. 1 lygio monstras — Bronze Ingot, Aspen Log, Leather Hide, Linen Cloth; Mythril — tik nuo 6 lygio.
- Medžiagos krenta 40 % tikimybe kiekvienam ridenimui, kartais 2 vnt. Elitiniai ridena 2 kartus + 3 papildomus,
  bosai + 6 papildomus ir dar garantuotą auksinį daiktą (+25 % violetinį) — tik bosai meta gatavus daiktus.
  Skrynios ridena 1 (dėžė), 2 (Oak Chest) ar 3 (Strongbox) kartus.
- **Gaminimas** — St. Revere klasių prekeiviai (lange „Craft“): kardas/lankas/lazda ir krūtinė — 6 medžiagos,
  galva ir kojos — 4. Rodomi daiktai, kurių medžiagos turi, aukščiausio lygio, kurį gali dėvėti.
  Kariui — kardai, oda, grandinės, plokštės; šauliui — lankai, oda, grandinės; magui — lazdos, mantijos
  (Source `player_desirable_armor`). Pagamintas daiktas kartais geresnis: auksinis 2 %, mėlynas 8 %,
  žalias 25 % (`LootDrop.CraftChances`), 20 % su lizdu brangakmeniui.
- Monstrų ir skrynių lygiai — iš Source duomenų (kiekviena vieta turi savo lygį).
- **Daiktai kaip Source** (`Scripts/Editor/SourceGear.cs`): kiekvienam lygiui 1–25 — tos medžiagos, kurias
  Source `material_chance_weapon` / `material_chance_armor` leidžia tame lygyje, su ta pačia tikimybe
  (`ItemDefinition.dropWeight`). Medžiagų pavadinimai — iš `Source.exe`:
  - ginklai: kariui kardas (Bronze, Steel, Mythril, Emerald, Crystal, Diamond, Titanium), šauliui lankas ir magui
    lazda (Aspen, Elm, Cherry, Black Walnut, White Oak, Hard Maple, Hickory);
  - šarvai (galva, krūtinė, kojos), tinka visiems: oda (Leather, Brigandine, Gambeson — Cap/Jerkin/Boots),
    grandinės (Bronze, Steel, Mythril, Emerald — Coif/Chainmail/Greaves), plokštės (Crystal, Diamond, Titanium —
    Helmet/Breastplate/Sabatons), mantijos (Linen, Wool, Silk, Spiritsilk — Hood/Vestments/Slippers, duoda manos);
  - žala ir šarvai auga kartu su Source pardavimo kaina (ginklas 50 + 10 × lygis + 2 × lygis × (pakopa − 1),
    krūtinė 50 + 2,5 × pakopa × lygis). Pvz. Bronze Sword 1 lygio — +2 žala, Titanium Sword 25 lygio — +21.
  - Medžiagos: metalai — „Ingot“ (ginklams, grandinėms ir plokštėms), mediena — „Log“, oda — „Hide“, audinys — „Cloth“.
  - Seni rankų darbo daiktai lieka tik prekeiviams (retieji) ir senuose išsaugojimuose.
- Spalvos kaip Source: žalia → mėlyna → **auksinė** (Epic) → **violetinė** (Legendary, aukščiausia).
  Bosas — garantuotas auksinis + 25 % violetinis.

## Statai (C langas, 3 puslapiai)

Visi Source statai (pavadinimai, ID ir XP kainos — iš `Source.exe`), kiekvienam galima pirkti taškus už XP:

| Puslapis | Statai | Ką daro pas mus |
|---|---|---|
| Attributes | Health, Mana, Strength, Agility, Willpower, Intelligence, Courage, Regeneration, Meditate | Willpower +0,5 % gebėjimų žalos ir gydymo; Regeneration / Meditate — 1 HP / mana kas 5 s už tašką, ir kovoje |
| Combat | Melee / Ranged / Spell Critical, Dodge, Block, Resist Frost / Fire / Shadow / Holy, Bartering, Lockpicking | kritiniai +0,2 % už tašką; Dodge +0,2 % (iki 40 %) išvengti smūgio; Block +0,2 % (iki 50 %) — pusė žalos; atsparumas r / (r + 100); Bartering −0,5 % pirkti / +0,5 % parduoti (iki 25 %); Lockpicking — skrynioms (2 × lygis) |
| Weapon Skills | Staves, Maces, Axes, Swords, Ranged, Daggers, Wands, Shields | +0,5 % žalos dėvimu tos rūšies ginklu; Shields +0,1 % blokavimo |

Kainos: kritiniai (melee, spell) x/2 + 1, ranged kritiniai ir atsparumai x/3 + 1, Dodge / Block x + 1, Bartering /
Lockpicking 2x + 1, Regeneration / Meditate x^1,4 + 4, ginklų įgūdžiai 3 × Strength kaina.

Monstrai nuo 3 lygio kas ketvirtoje vietoje (ir elitiniai) muša magija — Frost, Fire, Shadow ar Holy (skaičiai tos
spalvos), todėl atsparumai turi prasmę.

## Afiksai

- Visi 7145 Source afiksai (visi — priesagos: „Steel Sword of the Novice“, „Aspen Bow of Magical Vim“), su jų
  lygių ribomis ir visais statais (iki 5 viename). Laikomi `AffixCatalog.asset` viduje.
- Source afikso vertė — daugiklis (0,3–3,0), kurį serveris paverčia skaičiumi pagal lygį (formulės kliente nėra).
  Pas mus: statas = daugiklis × (lygis + 2) / 2 afikso lygių viduryje (Novice, 1–5 lyg.: +5; Elder, 20–25: +34),
  HP ir mana — 3 kartus tiek, Weapon Value — prie žalos.
- Kokybė pagal statų skaičių: 1 — žalias, 2 — mėlynas, 3 — auksinis, 4+ arba 25 lygio — violetinis.
- Seni mūsų afiksai lieka daiktams, kurie juos jau turi, bet nebekrenta.

## Grobis

- Kur monstras paliko grobį, žiba žiežirbos. Paspaudus — prieini ir atsidaro mažas grobio langas: daiktai su
  aprašymais; paspaudus daiktą — paimi jį, „Take all“ — viską. Tuščias maišas dingsta.

## Gėrimai

- 5 pakopos: Minor (1 lyg.), Lesser (5), – (12), Greater (18), Major (24). Gerti galima tik nuo to lygio;
  5 ir 6 klavišai išgeria geriausią, kurį gali.
- Potion gydo per 20 s (kas 2 s), Serum (žalias) — iš karto ir daugiau. Visi gėrimai turi bendrą 60 s laukimą, kaip Source.
- Kiekiai — pusė Source (kaip ir mūsų HP): Minor Life Potion 45, Major 200; Minor Life Serum 68, Major 300.
- Monstrai meta gėrimus pagal savo lygį, 15 % — Serum vietoj Potion. Klasių prekeiviai sujungia 5 vienodus į kitą pakopą
  (5 Major Potion → Major Serum).

## Skrynios

Source konteineriai (`gameobject_template`, laukų pavadinimai — iš `Source.exe`):

| Kas | Užrakinta | Viduje |
|---|---|---|
| Dėžės ir statinės (Crate, Barrel) | ne | tik auksas (lygis × 2–4) |
| Skrynios (Chest) | taip: Lockpicking ≥ 2 × lygis | daiktas: žalias 100 %, vietoj jo mėlynas 12 %, auksinis 5 %, violetinis 1 %; gėrimas 75 %; brangakmenis 75 %; auksas × 2 |
| Strongbox (toliau pasaulyje) | taip | dar 2 garantuoti daiktai, gėrimas ir brangakmenis 100 %, auksas × 3 |

- Paspaudus užrakintą skrynią: jei Lockpicking per mažas — „Lockpicking not high enough“ (kiek reikia ir kiek turi);
  jei pakanka — 2 s rakinama (Pick Lock), tada atsidaro grobio langas su tuo, kas viduje. Nuėjus šalin — nutrūksta.
- Virš užrakintos skrynios parašyta „Locked (Lockpicking N)“. Lockpicking keliamas C lange, Combat puslapyje (2x + 1 XP).
- Skrynios — antras (po bosų) gatavų daiktų šaltinis; visa kita gaminama iš medžiagų.

## Burtai (iki 20 lygio)

Source burtai (`spell_template`, Tome knygos `item_template`): Warrior turi Paladin burtus, Ranger — Ranger,
Mage — Mage (`Scripts/Editor/SourceSpells.cs`). Laukimo laikai, užkeikimo laikai, mana (bazė + už lygį), ginklo
žalos procentai ir burtų formulės (rangas + (statas + statas2 × k) × koef. / (105 − rangas × d)) — Source; sumos
padalintos per pusę, kaip ir mūsų HP. Atstumai: Source jardai / 120 (melee 130 → 1,1; burtai 610 → 5,1).

| Klasė | Burtai (lygis) |
|---|---|
| Warrior | Mighty Blow, Holy Wrath, Radiance (1), Hammer of Might (2), Charge (3), Blessing of Champions (4), Divine Strike (5), Cleanse (6), Vengeance Aura (7), Blessing of Defense (8), Touch of Salvation (10) |
| Ranger | Sinister Strike, Entangling Shot, Mark Target (1), Multi-Shot (2), Stunning Shot (3), Focused Evasion (4), Blindside (5), Kidney Shot (6), Vanish (7), Sabotage (8), Aimed Shot (9), Devotion (10) |
| Mage | Fireball, Ignite, Chains of Ice (1), Scorch (2), Ice Shard (3), Boon of Clairvoyance (4), Ice Blast (5), Vim of Wisdom (6), Bind Spirit (7), Boon of Protection (8), Warmth (9), Teleport (10) |
| Warrior 11–20 | Salvation Aura (11), Retribution (12), Righteous Storm (13), Fortification Aura (14), Wings of Freedom (15), Aegis of Valor (16), Redemption (18), Shield Block (19), Divine Protection (20) |
| Ranger 11–20 | Sleep Arrow (11), Hamstring (12), Tactical Retreat (14), Ethereal Sting (15), Flame Arrow (16), Explosive Arrow (18), Eye Gouge (19), Arrow Flurry (20) |
| Mage 11–20 | Deep Freeze (11), Magical Amplification (12), Wisdom of Lazarus (14), Illusion Gate (15), Remove Curse (16), Magical Dampening (18), Antimagic (19), Inferno (20) |

- Pradžioje žinomi 3 pirmo lygio burtai. Kitus moko Tome knygos (kaina 10 × lygis aukso) pas mokytojus St. Revere
  aikštėje: Halfrid (Warrior), Kelda (Ranger), Gyldan (Mage) — Source mokytojai. Skaityti galima nuo burto lygio.
- **Rangai**: kiekvienas burtas 1–10 rangų, keliama už XP burtų knygoje (P). Kaina: 10 × burto lygis + 10 × rangas^1,5
  (Source kliente šios formulės nėra — ji serveryje). Rangas didina žalą/gydymą (formulėje) ir efektų stiprumą.
- **Efektai** (`StatusEffects`): apsvaiginimas, sukaustymas, sulėtinimas, žala ir gydymas per laiką, daugiau/mažiau
  gaunamos ar daromos žalos, atributų ir Intelligence stiprinimai, spell critical, išsisukimas, „Evade“, skydas
  (Absorb), „Incapacitate“ (bet kokia žala nutraukia), nematomumas monstrams (Vanish), mana pasibaigus (Devotion),
  apsauga nuo apsvaiginimo. Ikonos — po žaidėjo ir taikinio juostomis (žalias rėmelis — stiprinimas, raudonas — silpninimas).
- **Juosta**: 1 — Attack, burtai 2, 3, 4, 7, 8, 9, 0; 5, 6 — gėrimai. Knygoje paspaudus burtą, paskui juostos vietą — burtas
  padedamas ten. Užkeikiant rodoma juosta; pajudėjus užkeikimas nutrūksta.
- II etapo efektai: Pacify (negali kirsti), pagreitis, nepažeidžiamumas, manos deginimas, mažesnis gydymas, greitesnės
  atakos, blokavimo reitingas, Silence (negali burti). Redemption atgaivina artimiausią kritusį draugą vietoje
  (40 % HP, 20 % manos); Illusion Gate — po 10 s nukelia į St. Revere; Tactical Retreat — šuolis atgal nuo priešo.
- Supaprastinta: Charge — iškart šalia taikinio; Retribution veikia visada (ne tik po parry); Eye Gouge — mažiau žalos;
  Antimagic — Silence; Remove Curse — kaip Cleanse; Multi-Shot — 3 × 70 %; Ice Shard veikia tik ant sulėtintų (Chains of Ice),
  Kidney Shot — tik ant „Incapacitate“; Sabotage — tik monstrams, kurie dar nekovoja; trūkstamos trukmės parinktos.
