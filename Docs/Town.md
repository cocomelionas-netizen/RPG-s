# Miestas, prekeiviai ir auksas (po 10 etapo)

Įkvėpta **Dreadmyst** (2026 m. izometrinis tab-target MMORPG): mieste yra NPC, kiekviena klasė turi savo ekipiruotę, o ekonomika remiasi auksu.

## Ką padarėme

- **Žemėlapis 4 kartus didesnis:** 80×80 langelių vietoj 40×40. Per vidurį eina keliai. Monstrų stovyklų yra 9:

  | Kur nuo miesto | Kas |
  |----------------|-----|
  | Aplink | 5 goblinų stovyklos |
  | Rytuose | 2 Goblin Brute stovyklos |
  | Vakaruose ir pietvakariuose | 2 Orc stovyklos |

- **Miestelis starto vietoje:**
  - grįsta aikštė, aplink kurią nėra uolų ir medžių;
  - požemio portalas ir treniruočių manekenai;
  - trys prekeiviai, virš kurių rodomas vardas ir ką jie parduoda.
- **Prekeiviai** (spustelėk NPC – veikėjas prieina ir atsidaro prekeivio langas kairėje, o inventorius dešinėje):

  | NPC | Kur | Parduoda |
  |-----|-----|----------|
  | **Thorin Ironhand** | Šiaurės rytuose | **Warrior:** Militia Sword, Steel Longsword, War Axe, Steel Helm, Steel Breastplate, Steel Sabatons |
  | **Sylva Swiftarrow** | Vakaruose | **Ranger:** Hunting Bow, Longbow, Elven Bow, Leather Hood, Studded Jerkin, Ranger Boots |
  | **Elara the Arcanist** | Rytuose | **Mage:** Apprentice Staff, Runed Staff, Staff of Embers, Silk Hood, Apprentice Robe, Enchanted Robe, Silk Slippers |

  Visi trys parduoda ir **Healing Potion** (10 aukso).
  - Kainos: Common 15–35, Uncommon 80–160, Rare 400 aukso.
  - Klasės daiktų kita klasė užsidėti negali. Lange tai rodoma raudonai, o bandant užsidėti – „Your class cannot use that“.
  - Kol prekeivio langas atidarytas, **spustelėjęs daiktą krepšyje jį parduodi už ¼ kainos**. Aprašyme matyti „Sells for N gold“.
  - Langas užsidaro paspaudus X, Esc arba nuėjus nuo prekeivio.
- **Auksas:**
  - pradžioje turi 20;
  - monstrai jo duoda **kiekvienam**, kas kovojo: goblinas 3–6, Goblin Brute 10–16, Orc 20–30, Goblin King 150–200;
  - gavus rodomas geltonas „+N gold“, o inventoriuje matyti „Gold N“;
  - auksas išsaugomas su veikėju.
- **Viską tikrina serveris:** ar stovi prie prekeivio, ar užtenka aukso, ar krepšyje yra vietos, ar daiktas tinka tavo klasei.

## Failai

| Failas | Paskirtis |
|--------|-----------|
| `Scripts/Runtime/Vendors/VendorDefinition.cs`, `Vendor.cs` | Prekeivio prekės ir NPC pasaulyje |
| `Scripts/Runtime/UI/VendorWindow.cs`, `NpcNameplates.cs` | Prekeivio langas, vardai virš NPC |
| `Data/Vendors/*.asset` | Prekeiviai (sukuria scenos įrankis) |
| `Tests/PlayMode/VendorTests.cs` | Nauji testai |

Pakeisti failai:
- `ItemDefinition`: `price` (kaina) ir `requiredClass` (kuriai klasei skirta).
- `Inventory`: auksas, pirkimas, pardavimas, klasės patikra užsidedant.
- `MonsterAI`: `goldReward`.
- `CharacterRecord`, `CharacterPersistence`: auksas išsaugomas.
- `LootPicker`: spustelėtas prekeivis – prieiti ir atidaryti langą.
- `InventoryWindow`: auksas, pardavimas, klasė aprašyme.
- Scenos įrankis: žemėlapis, miestelis, stovyklos, 19 naujų daiktų, prekeiviai, monstrų auksas.

## Kaip patikrinti naršyklėje

1. Prisijunk. Aplink startą turi būti grįsta aikštė ir trys NPC su vardais virš galvų.
2. Spustelėk savo klasės prekeivį (pvz. Warrior – Thorin Ironhand). Atsidaro jo prekės ir tavo inventorius. Nusipirk Healing Potion arba Common daiktą už pradinį auksą.
3. Spustelėk kitos klasės prekeivį: jo daiktai pažymėti raudonai „... only“. Jei tokį nusipirksi, užsidėti jo nepavyks.
4. Kol prekeivio langas atidarytas, spustelėk daiktą krepšyje – jis parduodamas, o tavo auksas padidėja.
5. Mušk monstrus: virš veikėjo pasirodo „+N gold“. Pasitaupęs, nusipirk Uncommon ar Rare daiktą.
6. Pasivaikščiok po didesnį žemėlapį ir surask Brute (rytuose) bei Orc (vakaruose) stovyklas.

## Automatiniai testai

Pridėti 6 nauji `VendorTests` testai:
- pirkimas kainuoja kainą ir įdeda daiktą į krepšį;
- be pakankamo aukso nupirkti nepavyksta;
- pardavimas duoda ¼ kainos;
- toli nuo prekeivio prekiauti negalima;
- klasės daiktas tinka tik savo klasei;
- už nužudytą monstrą gaunamas auksas.

`AccountTests` dabar tikrina ir aukso išsaugojimą. Iš viso **94** testai.

## Žinomi apribojimai
- Prekeivių atsargos neribotos, o kainos fiksuotos.
- Prekeivio NPC yra vieno kadro paveikslėlis, be animacijos.
- Klasės daiktai neturi atskirų atakos animacijų: lankininkas vis dar mosuoja kardu, o sviedinys skrenda kaip anksčiau.
- Senesni išsaugoti veikėjai, buvę toli nuo starto, žemėlapiui pasikeitus gali atsirasti kitoje vietoje. Jei ta vieta užstota, veikėjas atsiras prie starto.
