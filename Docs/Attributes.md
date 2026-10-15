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
