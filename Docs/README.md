# RPG-s – 2D izometrinis fantasy MMORPG

Unity 6 (6000.6.4f1), URP su 2D Renderer, C#, Input System.
Grafika: detali, tapyta HD 2D (ne pixel art, ne 3D). Kol kas naudojami sugeneruoti placeholder'iai.

Kalbos taisyklė: dokumentacija lietuviškai, o visas kodas, failų, klasių ir kintamųjų pavadinimai bei komentarai – angliškai.

## Projekto struktūra

Visas mūsų turinys yra `Assets/_Project/`. Kiti `Assets/` aplankai (`Settings`, `TutorialInfo`, `Scenes`, `Readme.asset`) liko iš Unity šablono.

```
Assets/_Project/
  Art/
    Characters/        tapyti veikėjų sprite'ai (būsimi)
    Environment/       tapyti žemės tile'ai, uolos, medžiai (būsimi)
    Placeholder/       laikini sugeneruoti sprite'ai (keisime tikrais)
    UI/                UI grafika
  Audio/
  Prefabs/
    Characters/        Player.prefab ir kt.
    Monsters/          Goblin.prefab ir kt.
    World/             pasaulio objektai
  Scenes/              World_Test.unity
  Data/
    Characters/        animacijų rinkiniai (CharacterAnimationSet)
    Classes/           klasės (Warrior, Ranger, Mage) ir ClassCatalog
    Abilities/         gebėjimai
  Settings/Rendering/  Renderer2D.asset (2D Renderer, izometrinis rūšiavimas)
  Tiles/               Tile asset'ai tilemap'ams
  Scripts/
    Runtime/           žaidimo kodas (assembly RPG.Runtime, namespace RPG.*)
      CameraSystem/    CameraFollow
      Characters/      Direction8, CharacterAnimationSet, CharacterAnimator
      Combat/          Health, HealthRegeneration, AutoAttack, PlayerTargeting, PlayerRespawn, TrainingDummy, CombatEffects
      Monsters/        MonsterAI, MonsterMotion, MonsterSpawner
      Navigation/      NavGrid (A*), WorldNavigation (kelio paieška tilemap'e)
      Progression/     klasės, gebėjimai, lygiai ir patirtis (CharacterProgress, AbilityCaster, LevelCurve)
      Controls/        PlayerInputReader
      Networking/      NetworkBootstrap, PlayerNetworkMovement, MovementValidator, OfflineTransport, DevConnectionPanel, DevBot
      Player/          PlayerMovement
      UI/              UnitFrame, CombatTextLayer, AbilityBar, ExperienceBar, ClassPicker
      World/           ClickMarker
      Core/            rezervuota kitiems etapams
    Editor/            redaktoriaus įrankiai (RPG meniu), grafikos importo taisyklės, build meniu
  Tests/
    PlayMode/          automatiniai testai (assembly RPG.Tests.PlayMode)
.github/workflows/     GitHub Actions: WebGL build'as ir paskelbimas GitHub Pages
Docs/                  ši dokumentacija (Unity jos neimportuoja)
Builds/                build'ai (git'e ignoruojama)
```

Principai:
- **Input atskirtas nuo logikos.** Įvestį skaito tik `PlayerInputReader`. Vėliau jį bus galima pakeisti tinklo komandomis arba klavišų perskyrimu.
- **Runtime kodas turi savo assembly** (`RPG.Runtime.asmdef`). Taip greičiau kompiliuojasi, o vėliau lengviau atskirti serverio ir kliento kodą.
- **Scena generuojama įrankiu** (`RPG > Build Test Scene`), todėl bet kada ją galima atkurti nuo nulio.
- **Serveris turi paskutinį žodį.** Klientas prognozuoja savo judėjimą, bet serveris jį tikrina ir taiso. Taip pat bus daroma su kova, daiktais ir progresu.
- **Pateikimas atskirtas nuo judėjimo.** `CharacterAnimator` skaito sąsają `ICharacterMotionSource`, todėl tas pats komponentas tiks žaidėjui, monstrams ir tinklo veikėjams.

## Žaidimas naršyklėje

Naujausia versija po kiekvieno push'o į `main` automatiškai sukuriama ir paskelbiama: **https://cocomelionas-netizen.github.io/RPG-s/** (Offline režimas). Kaip tai paruošti: [WebGL.md](WebGL.md).

## Etapų planas

| # | Etapas | Būsena |
|---|--------|--------|
| 1 | Projekto struktūra, izometrinis žemėlapis, kamera, judėjimas pele ir WASD | ✅ [Stage1.md](Stage1.md) |
| 2 | Veikėjo pateikimas: 8 krypčių animacijos, paspaudimo žymeklis, aukštų objektų rūšiavimas, HD tapytos grafikos importo taisyklės | ✅ [Stage2.md](Stage2.md) |
| 3 | Multiplayer pagrindas: dedikuotas serveris, serverio autoritetas judėjimui, prisijungimas | ✅ [Stage3.md](Stage3.md) |
| 4 | Taikinio pasirinkimas ir realaus laiko kova (HP, žala, auto-attack) | ✅ [Stage4.md](Stage4.md) |
| 5 | Monstrai ir jų AI (spawn, aggro, kelio paieška tilemap'e) | ✅ [Stage5.md](Stage5.md) |
| 6 | Gebėjimai, klasės, lygiai ir patirtis | ✅ [Stage6.md](Stage6.md) |
| 7 | Inventorius, ginklai, šarvai, loot | ⏳ |
| 8 | Saugus progreso išsaugojimas serveryje (paskyros, duomenų bazė) | ⏳ |
| 9 | Užduotys ir NPC | ⏳ |
| 10 | Požemiai (instancijos) | ⏳ |
| 11 | PvP | ⏳ |

Multiplayer'is planuojamas anksti (3 etapas) sąmoningai. Kovą, monstrus ir daiktus iškart rašysime serverio autoriteto principu, kad vėliau nereikėtų jų perrašinėti.
