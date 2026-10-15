# 7 etapas – inventorius, ginklai, šarvai, loot

## Ką padarėme

- **Daiktai** (`ItemDefinition`, `ItemCatalog`):
  - keturios ekipiruotės vietos: ginklas, šalmas, šarvai ir batai, plius gydymo gėrimai;
  - retumas pažymėtas spalva: Common (balta), Uncommon (žalia), Rare (mėlyna), Epic (violetinė);
  - daiktai duoda papildomos **žalos**, **HP** ir **armor**. Armor mažina gaunamą žalą: 100 armor perpus, 12 armor – apie 11 %.

  | Vieta | Common | Uncommon | Rare | Epic |
  |-------|--------|----------|------|------|
  | Ginklas | Rusty Sword +2 žalos | Iron Sword +4 | Goblin Cleaver +7, +10 HP | Orcish Greataxe +12, +20 HP |
  | Šalmas | Leather Cap 4 armor | Iron Helm 8, +5 HP | Orc Skull Helm 12, +15 HP | |
  | Šarvai | Padded Vest 6 | Chainmail 12, +10 HP | | Orcish Plate 22, +30 HP |
  | Batai | Worn Boots 3 | Iron Boots 6, +5 HP | | |

  **Healing Potion** atstato 35 % HP. Gėrimai kraunami į krūvą iki 10, o tarp jų išgėrimų yra 5 s cooldown. Kiekvienas naujas veikėjas gauna 3 gėrimus.
- **Inventorius** (`Inventory`): 20 vietų krepšys ir 4 ekipiruotės vietos. Visą turinį laiko serveris. Klientas tik paprašo užsidėti, nusiimti, išgerti ar pasiimti, o serveris patikrina, ar tai galima.
- **Inventoriaus langas**: klavišas **I** (arba **B**) arba mygtukas **Bag (I)** dešinėje apačioje.
  - **Spustelėk daiktą krepšyje**, kad jį užsidėtum. Gėrimas tokiu atveju išgeriamas. Jei toje vietoje jau buvo daiktas, jis grįžta į krepšį.
  - **Spustelėk užsidėtą daiktą**, kad jį nusiimtum.
  - Užvedus pelę, matyti daikto aprašymas.
  - Viršuje rodoma dabartinė žala, HP ir armor.
- **Loot.** Nužudytas monstras pagal savo loot lentelę gali palikti **krepšelį** ant žemės. Spustelėk jį: veikėjas prieina ir pasiima viską, kas telpa. Rodomas užrašas „+ Iron Helm“ daikto retumo spalva. Krepšelis dingsta, kai ištuštinamas, arba po 90 s. Ar žaidėjas pakankamai arti, tikrina serveris.
- **Stipresni monstrai ir jų stovyklos:**

  | Monstras | Lygis | HP | Žala | XP | Stovykla | Geriausias loot |
  |----------|-------|----|------|----|----------|-----------------|
  | Goblin | 2 | 45 | 3–5 | 20 | 3 stovyklos po 3, 5–7 vnt. nuo starto | Iron Sword (3 %) |
  | Goblin Brute (pilkas, didesnis) | 4 | 120 | 6–10 | 45 | 2, ~10 vnt. į dešinę | Goblin Cleaver (4 %) |
  | Orc (raudonas, didžiausias) | 6 | 220 | 10–15 | 90 | 2, ~10 vnt. į kairę | Orcish Plate, Orcish Greataxe (po 5 %) |

  Stipresni monstrai atsiranda iš naujo po 40 s, goblinai – po 20 s.

## Failai

| Failas | Paskirtis |
|--------|-----------|
| `Scripts/Runtime/Items/ItemDefinition.cs`, `ItemCatalog.cs`, `ItemStack.cs`, `LootEntry.cs` | Daiktai, jų katalogas (daiktas saugomas kaip numeris), krūva, loot įrašas |
| `Scripts/Runtime/Items/Inventory.cs` | Krepšys ir ekipiruotė (serveris), daiktų bonusai, gėrimai, pasiėmimas |
| `Scripts/Runtime/Items/LootBag.cs`, `LootPicker.cs` | Krepšelis ant žemės; spustelėjimas, priėjimas ir pasiėmimas |
| `Scripts/Runtime/UI/InventoryWindow.cs`, `ItemSlotView.cs` | Inventoriaus langas |
| `Data/Items/*.asset`, `Prefabs/World/LootBag.prefab`, `Prefabs/Monsters/*.prefab` | Sukuria scenos įrankis |
| `Tests/PlayMode/InventoryTests.cs` | Nauji testai |

Pakeisti failai:
- `Health`: `ServerArmor` ir `ReduceByArmor` (žalos mažinimas).
- `CharacterProgress`: prie klasės ir lygio prideda daiktų žalą, HP ir armor.
- `MonsterAI`: loot lentelė ir krepšelis po mirties.
- `PlayerInputReader`: klavišai I ir B.
- Scenos įrankis:
  - daiktai ir katalogas;
  - bendras monstrų prefab'ų kūrimas (`MonsterSpec`);
  - Goblin Brute ir Orc su stovyklomis;
  - loot krepšelis;
  - žaidėjui `Inventory` su 3 gėrimais ir `LootPicker`;
  - inventoriaus langas.

## Kaip patikrinti naršyklėje

https://cocomelionas-netizen.github.io/RPG-s/ (jei rodo seną versiją, perkrauk su Ctrl+F5)

1. **I** – atsidaro inventorius su 3 Healing Potion. Užvesk pelę, kad pamatytum aprašymą.
2. Gauk žalos nuo goblino ir spustelėk gėrimą: pasirodo žalias „+N“, gėrimų lieka 2. Antrą iš karto išgerti nepavyks („Not ready yet“).
3. Mušk goblinus, kol vienas paliks krepšelį (gėrimas iškrenta 30 % atvejų, daiktai rečiau). Spustelėk krepšelį: veikėjas prieina, pasirodo „+ daiktas“.
4. Spustelėk daiktą krepšyje: jis atsiranda ekipiruotės vietoje, o viršuje padidėja Damage, Health ar Armor. Spustelėk jį ten, kad nusiimtum.
5. Aplankyk Goblin Brute (dešinėje) ir Orc (kairėje) stovyklas. Jos pavojingesnės, bet duoda daugiau XP ir geresnį loot.

## Automatiniai testai

`RPG > Build Test Scene`, tada **PlayMode** → **Run All**. Pridėti 7 nauji `InventoryTests` testai:
- pradiniai gėrimai;
- ginklas prideda žalos, o nusiėmus ji dingsta;
- šarvai prideda HP ir sumažina žalą;
- gėrimas gydo, sunaudojamas ir turi cooldown;
- pilnas krepšys nebepriima daiktų;
- monstras palieka krepšelį, kurį galima pasiimti;
- per toli esančio krepšelio pasiimti negalima.

Kartu su 5–6 etapų pataisymų testais iš viso yra **78** testai. Kaip ir anksčiau, kodas patikrintas .NET kompiliatoriumi, bet PlayMode testai debesyje nepaleisti.

## Kur keisti skaičius
- Daiktai ir jų rodikliai: `TestSceneBuilder.CreateItems`. Naujus daiktus visada dėk į sąrašo galą, nes žaidėjai saugo daikto numerį.
- Monstrai, jų loot tikimybės ir stovyklos: `TestSceneBuilder` (`MonsterSpec`, `PlaceMonsterCamps`).
- Krepšio dydis, gėrimų cooldown, pasiėmimo atstumas: `Player.prefab` → `Inventory`.
- Armor formulė: `Health.ReduceByArmor`.

## Žinomi apribojimai
- **Daiktai neišsaugomi.** Perkrovus puslapį, jie dingsta. Išsaugojimas serveryje bus 8 etape.
- Daiktai rodomi spalvotais kvadratais su pavadinimu, nes ikonų dar nėra.
- Daiktų negalima išmesti, perkelti tarp krepšio vietų ar parduoti. Pirkliai buvo numatyti su NPC (9 etapas), kurio atsisakėme.
- Užsidėti daiktai nekeičia veikėjo išvaizdos.
- Loot nepriklauso konkrečiam žaidėjui: pasiimti gali bet kas, kas pirmas prieina.
