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
- **Požemių įėjimai** — ten, kur Dreadmyst (žr. „Požemiai“ žemiau).
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

## Statai (C langas, 3 puslapiai)

Visi Dreadmyst statai (pavadinimai, ID ir XP kainos — iš `Dreadmyst.exe`), kiekvienam galima pirkti taškus už XP:

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

- Visi 7145 Dreadmyst afiksai (visi — priesagos: „Steel Sword of the Novice“, „Aspen Bow of Magical Vim“), su jų
  lygių ribomis ir visais statais (iki 5 viename). Laikomi `AffixCatalog.asset` viduje.
- Dreadmyst afikso vertė — daugiklis (0,3–3,0), kurį serveris paverčia skaičiumi pagal lygį (formulės kliente nėra).
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
- Potion gydo per 20 s (kas 2 s), Serum (žalias) — iš karto ir daugiau. Visi gėrimai turi bendrą 60 s laukimą, kaip Dreadmyst.
- Kiekiai — pusė Dreadmyst (kaip ir mūsų HP): Minor Life Potion 45, Major 200; Minor Life Serum 68, Major 300.
- Monstrai meta gėrimus pagal savo lygį, 15 % — Serum vietoj Potion. Klasių prekeiviai sujungia 5 vienodus į kitą pakopą
  (5 Major Potion → Major Serum).

## Skrynios

Dreadmyst konteineriai (`gameobject_template`, laukų pavadinimai — iš `Dreadmyst.exe`):

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

Dreadmyst burtai (`spell_template`, Tome knygos `item_template`): Warrior turi Paladin burtus, Ranger — Ranger,
Mage — Mage (`Scripts/Editor/DreadmystSpells.cs`). Laukimo laikai, užkeikimo laikai, mana (bazė + už lygį), ginklo
žalos procentai ir burtų formulės (rangas + (statas + statas2 × k) × koef. / (105 − rangas × d)) — Dreadmyst; sumos
padalintos per pusę, kaip ir mūsų HP. Atstumai: Dreadmyst jardai / 120 (melee 130 → 1,1; burtai 610 → 5,1).

| Klasė | Burtai (lygis) |
|---|---|
| Warrior | Mighty Blow, Holy Wrath, Radiance (1), Hammer of Might (2), Charge (3), Blessing of Champions (4), Divine Strike (5), Cleanse (6), Vengeance Aura (7), Blessing of Defense (8), Touch of Salvation (10) |
| Ranger | Sinister Strike, Entangling Shot, Mark Target (1), Multi-Shot (2), Stunning Shot (3), Focused Evasion (4), Blindside (5), Kidney Shot (6), Vanish (7), Sabotage (8), Aimed Shot (9), Devotion (10) |
| Mage | Fireball, Ignite, Chains of Ice (1), Scorch (2), Ice Shard (3), Boon of Clairvoyance (4), Ice Blast (5), Vim of Wisdom (6), Bind Spirit (7), Boon of Protection (8), Warmth (9), Teleport (10) |
| Warrior 11–20 | Salvation Aura (11), Retribution (12), Righteous Storm (13), Fortification Aura (14), Wings of Freedom (15), Aegis of Valor (16), Redemption (18), Shield Block (19), Divine Protection (20) |
| Ranger 11–20 | Sleep Arrow (11), Hamstring (12), Tactical Retreat (14), Ethereal Sting (15), Flame Arrow (16), Explosive Arrow (18), Eye Gouge (19), Arrow Flurry (20) |
| Mage 11–20 | Deep Freeze (11), Magical Amplification (12), Wisdom of Lazarus (14), Illusion Gate (15), Remove Curse (16), Magical Dampening (18), Antimagic (19), Inferno (20) |

- Pradžioje žinomi 3 pirmo lygio burtai. Kitus moko Tome knygos (kaina 10 × lygis aukso) pas mokytojus St. Revere
  aikštėje: Halfrid (Warrior), Kelda (Ranger), Gyldan (Mage) — Dreadmyst mokytojai. Skaityti galima nuo burto lygio.
- **Rangai**: kiekvienas burtas 1–10 rangų, keliama už XP burtų knygoje (P). Kaina: 10 × burto lygis + 10 × rangas^1,5
  (Dreadmyst kliente šios formulės nėra — ji serveryje). Rangas didina žalą/gydymą (formulėje) ir efektų stiprumą.
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

## Požemiai

Visi Dreadmyst požemiai, į kuriuos įeinama iš mūsų pasaulio dalies (ir iš jų vidaus), sugeneruoti
`Tools/DreadmystData/dungeons.py` → `Scripts/Editor/DreadmystDungeons.cs`:

- **Planas** — iš Dreadmyst `maps/<žemėlapis>.map` (langelio žymė 0 — praeinama, 0x20 / 0x60 — siena): kambariai,
  koridoriai ir kolonos tokie pat. Atvykimo vieta, išėjimai, monstrai (`npc`) ir konteineriai (`gameobject`) — jų vietose.
- **Įėjimo taisyklės** — `dungeon_template`: lygis, raktas (sunaudojamas vienas, tik atidarančiojo), žaidėjų skaičius.
- Kiekviena grupė gauna savo kopiją; monstrai ir skrynios neatsiranda iš naujo; išėjimai visada atviri.

| Požemys (Dreadmyst žemėlapis) | Įėjimas | Taisyklės | Bosai | Ką meta bosai |
|---|---|---|---|---|
| Wolf's Den (`wolf_cave_1`) | Wolf's Den | laisvai | — (Cave Howler, Risen Miner, 5 lyg.) | — |
| Trog Cave (`goblin_cave`) | Trog Cave | laisvai | — (Cave Trog, 3 lyg.) | — |
| The Lower Garrison (`stormrock_ruins`) | pietinis kraštas* | 25 lyg., iki 4 | Melchon (28) | **10 Tower Key**, 3 Divine Accessory Orb |
| Decrepit Tower (`fort_amir`) | pietinis kraštas* | 25 lyg., **Tower Key**, iki 4 | Lord Zargan (30), Tchazzar (30) | Divine orbai, 4 Magic Dust |
| Abandoned Barracks (`fort_nasu`) | (267, 191) | 25 lyg., **Keys to the Barracks**, vienas | Sarloch the Betrayer (25) | **Depleted Key** |
| Kandorys Undercourse (`dilapidated_sewers`) | pietinis kraštas* | 25 lyg., **Undercourse Key**, vienas | Knight Maximus (28) | **2 Keys to St. Revere**, Divine Armor Orb |
| Desecrated Altar (`st_maria_1`) | prie St. Revere (508, 143) | 25 lyg., **Keys to St. Revere**, vienas | Ebondeath (30) | **Salmaster's Key**, orbai, Magic Dust |
| Salmaster's Descent (`temple_of_mez_1`) | Desecrated Altar viduje | 25 lyg., **Salmaster's Key**, vienas | Salmaster (30), Balagos (30) | Major orbai, Divine Weapon Orb, brangakmeniai |
| Telamont's Demiplane (`wizards_tower_2`) | (370, 272) | 25 lyg., **Demiplane Key**, iki 4 | Demiplanar Guardian (30) | **20 Sanctum Key**, Flawless ir paprasti brangakmeniai |
| Telamont's Sanctuary (`st_maria_3`) | Demiplane viduje | 25 lyg., **Sanctum Key**, iki 4 | Prince Telamont (30) | 5 kiekvieno Flawless brangakmenio, Arcane Essence |

\* Dreadmyst šiuos įėjimus turi toliau į pietus, nei siekia mūsų pasaulis — jie stovi pietiniame krašte (tame pačiame x).

**Raktų grandinė (kaip Dreadmyst):**

- Vienas: Horned Charger / Horned Beast / Lesser Wyrm (5 %) → *Keys to the Barracks* → Sarloch → *Depleted Key*;
  + *Dale Gem* (Zhendale stovykla, 15 %) — paspaudus sujungiama į *Undercourse Key* → Knight Maximus → *Keys to St. Revere*
  → Ebondeath → *Salmaster's Key* → Salmaster's Descent.
- Grupė: Lower Garrison (be rakto) → Melchon → *Tower Key* → Decrepit Tower.
- Grupė: Tantorus / Fasthorn (100 %) → *Inactive Demiplane Key* + *Magic Dust* + *Major Treasury Gold* (juvelyras) — paspaudus
  sujungiama į *Demiplane Key* → Demiplanar Guardian → *Sanctum Key* → Telamont's Sanctuary.
- Zhendale stovykla ir Tantorus / Fasthorn Dreadmyst'e yra už mūsų pasaulio krašto — jie perkelti į pietinį kraštą
  (stovykla išlaiko savo formą).

**Sunkumas.** Lygiai — Dreadmyst (bosai 28–30; monstrų lygis gali būti iki 30). Dreadmyst elitiniai ir „boss“ pažymėti
monstrai — pas mus elitiniai. Kur Dreadmyst nurodo savo HP, jis perskaičiuojamas kaip kartotinis paprasto to lygio monstro
(Dreadmyst paprastas 25 lygio — 7340 HP): pvz. Melchon ≈ 16,5×, Lord Zargan ≈ 29×, Demiplanar Guardian ≈ 81×, Fallen
Guard ≈ 2,7×. Žala — pagal Dreadmyst `weapon_value` (400 — įprastas elitinis): Kandorys Knight 7,5×, Guardian 11×.
Auksas — `custom_gold_ratio`. Bosai turi žemės smūgį (mūsų mechanika).

**Skrynios** — Dreadmyst konteineriai jų vietose, su jų pavadinimais ir lygiais: Rosewood Crate (auksas), Rosewood Chest
(užrakinta, Lockpicking 50; daiktas, gėrimas 75 %, brangakmenis 75 %), Platinum Strongbox (dar 2 daiktai, gėrimas ir
brangakmenis 100 %, auksas × 3), urvuose — Elm / Aspen / Copper.

**Bosų grobis** — Dreadmyst `loot` lentelės (vienodos eilutės skaičiuojamos kartą) ir jų kokybės tikimybės (`loot_blue/gold/
purple_chance`, pvz. Lord Zargan — violetinis 100 %). Orbai, Magic Dust, Arcane Essence kol kas tik parduodami (Dreadmyst
kaina); Dreadmyst ametistai ir statų ritiniai praleisti (pas mus jų nėra); Refined → mūsų paprastas, Flawless → Flawless brangakmenis.
