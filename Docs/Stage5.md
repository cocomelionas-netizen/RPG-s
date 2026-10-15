# 5 etapas – monstrai, jų AI ir kelio paieška

## Ką padarėme

- **Goblinai.** Trys stovyklos po 3 goblinus, 5–9 vienetai nuo starto. Goblinas yra mažesnis už žaidėją, žaliu veidu ir rudu apsiaustu. Turi 45 HP, kerta 3–5 žalos kas 2 s, juda 8 kryptimis su ėjimo ir atakos animacija.
- **Viską sprendžia serveris.** Monstro AI, judėjimas, ataka ir žala veikia tik serveryje. Klientai mato:
  - poziciją, kryptį ir ar monstras eina – 0.15 s „praeityje“, sklandžiai, kaip kitus žaidėjus;
  - HP ir atakos animacijas – per tuos pačius `Health` ir `AutoAttack`, kaip žaidėjo.
- **Monstro elgsena (`MonsterAI`):**

  | Būsena | Kas vyksta |
  |--------|-----------|
  | **Ramybė** | Lėtai vaikšto aplink savo vietą (iki 1.2 vieneto), stabteli 3–8 s. |
  | **Kova** | Žaidėjas priartėja per 3 vienetus arba smogia goblinui. Goblinas eina prie jo ir muša. Netoliese ramiai stovintys goblinai (iki 2.5 vieneto) prisijungia. |
  | **Grįžimas** | Goblinas grįžta namo ir atgauna visą HP. Grįždamas jis nekreipia dėmesio į smūgius ir jų nejaučia. Jei kelyje kažkur užstringa ilgiau nei 10 s, perkeliamas namo. |
  | **Mirtis** | Pilkas kūnas guli 3 s ir dingsta. Po 20 s stovykla atsiunčia naują gobliną. |

  Goblinas grįžta namo, kai:
  - **12 s nepataiko** į savo taikinį (žaidėjas bėga ir goblinas jo nepasiveja);
  - nubėga daugiau nei 25 vienetus nuo vietos, kur prasidėjo kova. Kiekvienas goblino smūgis tau perkelia šią vietą ten, kur jis smogė, todėl kol goblinas tave pasiveja ir muša, jis seka toliau (pakeista pagal tavo pastabą, anksčiau buvo 9 vienetai nuo namų);
  - visi, su kuriais kovojo, mirė arba pabėgo;
  - 3 s neranda kelio iki taikinio.

- **Grėsmės lentelė (threat).** Goblinas puola tą, kuris jam padarė daugiausia žalos. Jei žaidėjas tik pateko į matymo zoną, grėsmė maža. Jei kitas žaidėjas muša stipriau, goblinas persijungia į jį.
- **Kelio paieška tilemap'e** (`WorldNavigation` + `NavGrid`). A* ieško kelio per izometrinius langelius 8 kryptimis:
  - aplenkia uolas, medžius ir manekenus;
  - nekerta kampų tarp kliūčių;
  - kelias išlyginamas: kur niekas netrukdo, einama tiesiai, o ne laiptuotai per langelių centrus.

  Paieška 40×40 žemėlapyje trunka apie 0.5 ms.
- **Žaidėjo click-to-move dabar irgi apeina kliūtis.** Paspaudus už uolų sienos, veikėjas ją apeina, o ne įsiremia. Paspaudus ant pačios uolos, nueinama iki artimiausios laisvos vietos. Tą patį kelią naudoja ir priėjimas prie taikinio.
- **Monstrai susiskirsto aplink taikinį.** Keli goblinai nestovi viename taške.
- **Žaidėjo HP atsistato** (`HealthRegeneration`). Po 5 s be žalos atsistato 5 % HP per sekundę.

> **Šiuo metu monstrai iš toli nepuola** (`aggroRadius` = 0, nustatyta scenos įrankyje). Jie kovoja tik tada, kai juos užpuoli, o jų stovyklos draugai prisideda.

## Failai

| Failas | Paskirtis |
|--------|-----------|
| `Scripts/Runtime/Navigation/NavGrid.cs` | A* langelių tinklelyje, artimiausio laisvo langelio paieška (gryna C#, testuojama be scenos) |
| `Scripts/Runtime/Navigation/WorldNavigation.cs` | Iš tilemap'ų ir kliūčių sukuria `NavGrid`, ieško ir išlygina kelią pasaulio koordinatėmis |
| `Scripts/Runtime/Monsters/MonsterMotion.cs` | Serveris veda monstrą keliu, klientai interpoliuoja (kaip kitus žaidėjus) |
| `Scripts/Runtime/Monsters/MonsterAI.cs` | Būsenos, grėsmės lentelė, pagalbos šauksmas, grįžimas namo, mirtis |
| `Scripts/Runtime/Monsters/MonsterSpawner.cs` | Stovykla: atsiunčia monstrus serveriui pasileidus ir po mirties |
| `Scripts/Runtime/Combat/HealthRegeneration.cs` | HP atsistatymas ne kovoje |
| `Prefabs/Monsters/Goblin.prefab` | Goblinas (sukuria scenos įrankis) |
| `Data/Characters/Goblin_AnimationSet.asset`, `Art/Placeholder/Goblin/*.png` | Goblino animacijos ir laikini kadrai (sukuria scenos įrankis) |
| `Tests/PlayMode/NavigationTests.cs`, `MonsterTests.cs` | Nauji testai |

Pakeisti failai:
- `Health`:
  - `ApplyDamage` dabar priima, kas smogė;
  - pridėti įvykis `DamageTaken`, `ServerInvulnerable` ir `Heal()`.
- `AutoAttack`: perduoda puoliką į `ApplyDamage`.
- `PlayerMovement`: click-to-move eina keliu, kurį randa `WorldNavigation`. Jei scenoje navigacijos nėra, eina tiesiai, kaip anksčiau.
- Scenos įrankis:
  - sukuria goblino prefab'ą ir įregistruoja jį `DefaultNetworkPrefabs`;
  - prie `Grid` prideda `WorldNavigation`;
  - sukuria 3 stovyklas (`Monster Camps`);
  - žaidėjui prideda `HealthRegeneration`.
- `PlaceholderArtGenerator.CharacterFrame`: galima nurodyti veido spalvą.

## Kaip patikrinti naršyklėje

https://cocomelionas-netizen.github.io/RPG-s/ (jei rodo seną versiją, perkrauk su Ctrl+F5)

1. **Rask goblinus.** Nuo starto eik į viršų ir truputį į dešinę (~5 vienetai), į kairę arba į dešinę žemyn (~7 vienetai). Goblinai lėtai vaikštinėja savo stovyklose.
2. **Priartėk per ~3 vienetus.** Goblinas turi tave pastebėti, ateiti ir pradėti kirsti: matysi raudonus žalos skaičius ir mažėjantį HP apačioje. Kartu turėtų atbėgti ir jo draugai.
3. **Kovok:** pažymėk gobliną (pele arba **Tab**) ir spausk **1** (Attack). Numuštas goblinas papilkėja ir po 3 s dingsta. Po ~20 s stovykloje atsiranda naujas.
4. **Pabėk toli** (daugiau nei ~25 vienetus nuo vietos, kur goblinas tau paskutinį kartą smogė). Goblinas apsisuka, grįžta namo, jo HP užsipildo, o grįžtančio jo sužeisti nepavyksta.
5. **Leisk goblinams tave nugalėti.** Atgimsi prie starto, o goblinai grįš į stovyklą.
6. **Pailsėk** – po 5 s be žalos tavo HP pamažu atsistato.
7. **Kelio paieška:**
   - spausk už uolų sienelės (dešinėje virš starto) arba už medžio – veikėjas turi ją apeiti, o ne įsiremti;
   - pasivyk gobliną už kliūties – jis irgi apeina.

## Automatiniai testai

Unity redaktoriuje: pirma `RPG > Build Test Scene` (testai naudoja goblino prefab'ą), tada `Window > General > Test Runner` → **PlayMode** → **Run All**. Turi būti **57** testai, iš jų 14 naujų:
- `NavigationTests` (6):
  - kelias per tarpą sienoje;
  - užblokuotas tikslas;
  - nekertami kampai;
  - artimiausias laisvas langelis;
  - kelias aplink uolas tikrame tilemap'e;
  - paspaudimas ant uolos.
- `MonsterTests` (8):
  - goblinas puola artimą žaidėją ir nepuola tolimo;
  - vejasi žaidėją;
  - atsisuka į tą, kuris smogė;
  - grįžta namo nesužeidžiamas ir pagyja;
  - miręs dingsta;
  - stovykla atsiunčia naują;
  - žaidėjo HP atsistato.

Šie testai parašyti debesyje be Unity. Kodas patikrintas .NET kompiliatoriumi, o `NavGrid` logika – atskirais testais. PlayMode testų paleisti negalėjau, todėl pirmą kartą juos paleisk pats.

## Kur keisti skaičius
- Goblino HP, žala, atakos greitis: `Goblin.prefab` → `Health`, `AutoAttack`.
- Matymo atstumas, pagalbos spindulys, pririšimo atstumas, vaikštinėjimas, kūno laikas: `Goblin.prefab` → `MonsterAI`.
- Greitis: `Goblin.prefab` → `MonsterMotion`.
- Stovyklos vieta, kiek monstrų ir per kiek laiko jie atsiranda iš naujo: `TestSceneBuilder.PlaceMonsterCamps`.

Prefab'us ir sceną sukuria `RPG > Build Test Scene`, ir CI tai daro prieš kiekvieną build'ą. Todėl skaičius keisk scenos įrankyje, o ne prefab'e rankomis.

## Žinomi apribojimai
- Monstrai pereina vienas pro kitą ir pro žaidėjus, kaip ir žaidėjai tarpusavyje. Kliūtis jie apeina kelio paieška, o ne fizika.
- Kol kas yra tik vienas monstro tipas. Daugiau tipų, loot ir patirtis bus 6–7 etapuose.
- Monstrų nerodome tik artimiems žaidėjams (interest management). To prireiks, kai pasaulis išaugs.
- Žemėlapio navigacija sukuriama vieną kartą. Jei vėliau žemėlapis keisis žaidimo metu (pvz. durys), reikės iškviesti `WorldNavigation.Build()`.
