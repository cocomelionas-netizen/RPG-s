# 2 etapas – veikėjo pateikimas

## Ką padarėme

- **8 krypčių animacijos.** Veikėjas rodomas ta kryptimi, kuria juda: E, NE, N, NW, W, SW, S arba SE (kryptys skaičiuojamos ekrane).
  - Animacijos laikomos `CharacterAnimationSet` asset'e: kiekvienai animacijai (Idle, Walk ir kt.) nurodomi kadrai kiekvienai krypčiai.
  - **Veidrodinis atspindys.** Jei vakarų pusės kryptis (NW, W, SW) neturi kadrų, naudojami rytų pusės kadrai, apversti horizontaliai. Dailininkui užtenka nupiešti 5 kryptis vietoj 8.
  - Jei kryptis neturi kadrų visai, rodomi pietų (S) kadrai, todėl veikėjas niekada neišnyksta.
  - `PlayOneShot("Attack")` paleidžia vienkartinę animaciją, kuri baigusi grįžta į Idle arba Walk. Ji bus naudojama kovoje (4 etapas).
- **Animacija atskirta nuo valdymo.** `CharacterAnimator` neskaito `PlayerMovement`, o skaito sąsają `ICharacterMotionSource`. Tas pats komponentas tiks monstrams (AI) ir kitiems žaidėjams (tinklas).
- **Žymeklis toje vietoje, kur paspaudei** (`ClickMarker`). Ant žemės pulsuoja auksinis žiedas. Kai veikėjas atvyksta, judėjimas atšaukiamas arba pradedamas judėti WASD, žiedas dingsta.
- **Medžiai** (`Tree.prefab`). Aukšti objektai su pivot ties kamieno pagrindu, todėl veikėjas gali užeiti už lajos. Susiduriama tik su kamienu, ne su visa laja.
- **Importo taisyklės HD grafikai** (`ArtImportRules`). Kai įmeti paveikslėlį į `Assets/_Project/Art/`, jam automatiškai nustatoma: Sprite, 256 PPU, Bilinear, aukštos kokybės suspaudimas ir teisingas pivot.
- **Pataisyta judėjimo pele klaida.** Jei kadras užtrukdavo ilgiau nei 0.25 s (lagas arba nesufokusuotas langas), tikslas būdavo atšaukiamas, veikėjui dar nepajudėjus. Dabar judėjimas pele valdomas ir fizikos žingsnyje, o užstrigimas tikrinamas tik tada, kai buvo bandyta judėti.
- Scenos įrankis pervadintas: dabar jis vadinasi **`RPG > Build Test Scene`** (anksčiau `RPG > Stage 1 > Build Test Scene`).

## Failai

| Failas | Paskirtis |
|--------|-----------|
| `Scripts/Runtime/Characters/Direction8.cs` | 8 krypčių enum ir vektoriaus pavertimas kryptimi |
| `Scripts/Runtime/Characters/ICharacterMotionSource.cs` | Sąsaja „kas judina veikėją“ |
| `Scripts/Runtime/Characters/CharacterAnimationSet.cs` | ScriptableObject su animacijų kadrais (`DirectionalClip`) |
| `Scripts/Runtime/Characters/CharacterAnimator.cs` | Rodo teisingą kadrą pagal judėjimą ir kryptį |
| `Scripts/Runtime/World/ClickMarker.cs` | Paspaudimo žymeklis |
| `Scripts/Runtime/Player/PlayerMovement.cs` | Dabar realizuoja `ICharacterMotionSource`, pataisytas užstrigimo tikrinimas |
| `Scripts/Editor/ArtImportRules.cs` | Automatiniai grafikos importo nustatymai |
| `Scripts/Editor/TestSceneBuilder.cs` | Buvęs `Stage1SceneBuilder`, papildytas medžiais, animacijomis ir žymekliu |
| `Scripts/Editor/PlaceholderArtGenerator.cs` | Generuoja veikėjo kadrus, medį ir žymeklio žiedą |
| `Data/Characters/Player_AnimationSet.asset` | Žaidėjo animacijų rinkinys |
| `Art/Placeholder/Player/*.png` | 30 laikinų veikėjo kadrų: 5 kryptys × (2 Idle + 4 Walk) |
| `Prefabs/World/Tree.prefab` | Medis |
| `Tests/PlayMode/CharacterAnimationTests.cs`, `ClickMarkerTests.cs` | Nauji testai |

Visi keliai yra `Assets/_Project/` viduje.

## Kaip patikrinti

### Rankinis patikrinimas
1. Jei scena sena: `RPG > Build Test Scene`.
2. Atidaryk `World_Test.unity` ir spausk **Play**.
3. **Vaikščiok WASD visomis 8 kryptimis** (pvz. W+D = NE). Veikėjas turi žiūrėti judėjimo kryptimi:
   - į kamerą (S, SE, SW, E, W) matyti akys ir auksinė sagtis;
   - nuo kameros (N, NE, NW) matyti tik gobtuvas.
4. **Einant** veikėjas kilnoja kojas ir siūbuoja, **stovint** lėtai „kvėpuoja“.
5. **Paspausk pele ant žemės.** Ten atsiranda pulsuojantis žiedas, kuris dingsta, kai veikėjas atvyksta.
6. **Medis kairėje nuo starto:**
   - užeik **už** jo (aukščiau ekrane) – veikėją turi uždengti laja;
   - praeik **priešais** jį – veikėjas turi būti priekyje;
   - pro kamieną praeiti negalima, pro lają galima.
7. **Console** – jokių raudonų klaidų.

### Automatiniai testai
`Window > General > Test Runner` → **PlayMode** → **Run All**. Turi praeiti **24** testai:
- `CharacterAnimationTests`:
  - 10 krypčių atvejų;
  - Idle ir Walk perjungimas;
  - vakarų pusės atspindys;
  - atsarginė S kryptis;
  - vienkartinė animacija.
- `ClickMarkerTests`: žymeklis matomas tik tada, kai yra tikslas.
- `PlayerMovementTests`: papildytas lėtų kadrų (frame hitch) testu.

## Kaip įdėti tikrą tapytą veikėją

1. Nupiešk 5 kryptis (E, NE, N, SE, S) kiekvienai animacijai. Vienas kadras – atskiras PNG arba lapas, kurio pavadinimas baigiasi `_Sheet`.
2. Įmesk failus į `Assets/_Project/Art/Characters/<VeikėjoVardas>/`. Importo taisyklės pačios nustatys 256 PPU ir pivot apačioje per vidurį. Jei kojos nėra pačioje paveikslėlio apačioje, Sprite Editor'iuje patempk pivot iki kojų.
3. Sukurk naują rinkinį: `Create > RPG > Characters > Animation Set`. Kiekvienai animacijai įrašyk `id` (`Idle`, `Walk`), fps ir sutempk kadrus į kryptis. Vakarų kryptis palik tuščias.
4. Prefab'e `CharacterAnimator` komponente nurodyk naują rinkinį.

Vieno vieneto aukštis = 256 px. Žmogaus dydžio veikėjas turėtų būti maždaug 180–200 px aukščio, kaip placeholder'is.

## Žinomi apribojimai
- Click-to-move vis dar eina tiesia linija. Kelio paieška bus 5 etape.
- Placeholder grafika yra tik laikina. Tikrą tapytą grafiką reikės nupiešti arba užsakyti.
