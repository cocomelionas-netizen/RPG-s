# Mana, loot kokybė ir atributai

Įkvėpta Dreadmyst mechanikų (tik idėjos — jokie jų failai ar duomenys nenaudojami).

## Mana

- `Mana` komponentas žaidėjui: serveris valdo reikšmę, atsistato 3% per sekundę, mirus ir prisikėlus — pilna.
- Max mana = klasės `baseMana` + `manaPerLevel` × (lygis − 1) + Intelligence × 4 + daiktų `manaBonus`.
- Kiekvienas gebėjimas turi `manaCost`; be manos — „Not enough mana“ (`CastFailure.NoMana`).
  Veiksmų juostoje kaina rodoma po pavadinimu (raudona, kai neužtenka).
- Mana Potion (`ItemUse.RestoreMana`, 40%) — pardavėjuose, monstrų loot'e, 2 vnt. pradžioje.
- Mėlyna mana juosta virš patirties juostos.

## Šlamštas ir kokybė

- `ItemRarity`: Poor (pilka), Common, Uncommon, Rare, Epic, Legendary.
- Monstrai ~60% numeta šlamšto (Goblin Ear, Brute Tusk, Orc Fang, Crown Shard), kuris tinka tik parduoti.

## Atsitiktiniai afiksai

- `AffixDefinition` (priešdėlis „Sturdy Iron Helm“ arba priesaga „Iron Sword of the Bear“) su atributais,
  žala, HP, šarvais, mana, kokybe, lygių intervalu ir kainos daugikliu.
- `AffixCatalog` (`Data/Items/Affixes`): 60% šansas, kad numestas ginklas/šarvas gaus afiksą;
  retesnės kokybės renkamos rečiau (60/28/10/2), stipresni afiksai — tik iš aukštesnio lygio monstrų.
- Afiksas saugomas `ItemStack.AffixId` (id = indeksas + 1, sąrašą tik papildyti), išsaugomas paskyroje,
  daiktai su skirtingais afiksais nesikrauna į vieną krūvą. Kokybė = geresnė iš daikto ir afikso.

## Atributai

- Strength, Agility, Intelligence, Vitality. Klasė turi bazinius atributus ir pagrindinį atributą.
- Pagrindinis atributas / 4 → žala; Vitality × 4 + Strength → HP; Intelligence × 4 → mana; Agility × 0,4% → kritinis smūgis.
- Kiekvienas lygis duoda 5 taškus. Langas **C** (arba mygtukas „Character (C)“) — „+“ prideda tašką (serveris tikrina).
- Atributus duoda ir daiktai bei afiksai. Seni veikėjai gauna taškus už jau pasiektus lygius prisijungę.
- Lygio 1 bendri skaičiai nepasikeitė (bazinis HP ir žala sumažinti tiek, kiek prideda atributai).

## Testai

`Tests/PlayMode/AttributeAndManaTests.cs`, papildyti `AccountTests` ir `ProgressionTests`.

## Veikėjo langas (C) — Dreadmyst stiliumi

- Eilutės „Pavadinimas: reikšmė“ (Health, Mana, Armor Value, Strength, Agility, Intelligence, Vitality, Damage,
  Critical Chance), spalvos paimtos iš Dreadmyst lango: pilkai smėlinės etiketės, šviesios reikšmės, mana mėlyna.
- Prie Health, Mana ir keturių atributų — mažas violetinis „+“. Užvedus pelę: „+1 Strength: 1 point“ ir
  „You have invested N points.“ / „You haven't invested anything.“ (kaip Dreadmyst). Užvedus ant eilutės — aprašymas.
- Taškai **perkami už patirtį (XP)**, kaip Dreadmyst: gauta XP kelia lygį ir kartu kaupiasi „Experience to spend“;
  išleidus XP lygis nemažėja. Taškų už lygį nebėra.
- Kainos — iš Dreadmyst kliento (`Dreadmyst.exe`, funkcija `0x558b50`), kai jau investuota `x` taškų:
  - Strength, Agility, Intelligence, Vitality: `x^1.5 + 5` → 5, 6, 7, 10, 13, 16, 19, 23, 27, 32, 36…
  - Health, Mana (+1 už tašką): `x^(x/1500)`, mažiausiai 1 — ilgai po 1 XP, brangsta tik turint šimtus taškų.
  - (Kliente dar: Regeneration/Meditate `x^1.4 + 4`, kritiniai `x/2 + 1` ar `x/3 + 1`, Dodge/Block `x + 1`,
    atsparumai `x/3 + 1`, Bartering/Lockpicking `2x + 1`, ginklų įgūdžiai `3 × Strength kaina` — mes jų neturime.)
- Kainos spalva tokia pati kaip Dreadmyst (#B500AF), kai XP neužtenka — pilka.
