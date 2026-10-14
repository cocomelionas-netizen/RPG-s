# 3 etapas – multiplayer pagrindas (Netcode for GameObjects)

## Ką padarėme

- **Netcode for GameObjects 2.13.3** ir **Multiplayer Play Mode 3.0.0** paketai.
- **Žaidimą galima paleisti trimis būdais:**
  - **Server** – tik serveris, be savo veikėjo (dedikuotas);
  - **Host** – serveris ir žaidėjas viename;
  - **Client** – jungiasi prie serverio.

  Būdą galima pasirinkti mygtuku arba komandinės eilutės parametru.
- **Žaidėjas atsiranda prisijungus.** Serveris kiekvienam prisijungusiam sukuria veikėją šalia `Player Spawn` taško. Scenoje iš anksto veikėjo nebėra. Kamera ir paspaudimo žymeklis pririšami prie *savo* veikėjo.
- **Judėjimas: kliento prognozė ir serverio patikra.**
  - Tavo veikėjas juda iškart, be jokio vėlavimo. Klientas 20 kartų per sekundę praneša serveriui savo poziciją.
  - Serveris kiekvieną pranešimą tikrina:
    - **greitį** – „judėjimo biudžetas“: per sekundę galima nueiti iki 125 % normalaus greičio, o nedideli tinklo trūkčiojimai praleidžiami;
    - **sienas** – ar tarp paskutinės priimtos ir naujos pozicijos nėra kliūties;
    - **ar tai savininkas** – kitas klientas negali judinti ne savo veikėjo.
  - Jei patikra nepraeina, serveris grąžina veikėją į paskutinę priimtą poziciją (korekcija) ir parašo tai į žurnalą. Taip sustabdomas speed hack, teleportavimas ir ėjimas kiaurai sienas.
- **Kiti žaidėjai** rodomi 0.15 s „praeityje“, tolygiai tarp dviejų serverio būsenų (interpoliacija), todėl jie juda sklandžiai, ne šuoliais. Jų animacija (Idle/Walk, 8 kryptys) veikia per tą pačią `ICharacterMotionSource` sąsają. Žaidėjai pereina vienas pro kitą, kaip daugumoje MMO.
- **Prisijungimo langas kūrėjui** (viršuje kairėje, F1 paslepia): adresas, prievadas ir mygtukai Host / Client / Server. Prisijungus jis susitraukia į būsenos eilutę su Disconnect mygtuku.
- **Testavimo botas.** Su parametru `-bot` tavo veikėjas pats vaikšto aplink startą, todėl multiplayer'į gali testuoti vienas.
- **Build meniu:**
  - `RPG > Build > Windows Player`;
  - `RPG > Build > Windows Dedicated Server` – reikia Unity Hub modulio, žr. toliau;
  - `RPG > Run > Local Server / Local Client / Local Bot Client`.
- **Apeita NGO klaida.** UnityTransport pats skaito `-ip`/`-port` ir `-ip` reikšmę klaidingai bando perskaityti kaip prievadą. Mes nustatymus perduodame priverstinai.

## Failai

| Failas | Paskirtis |
|--------|-----------|
| `Scripts/Runtime/Networking/NetworkBootstrap.cs` | Paleidžia server/host/client, nustato atsiradimo vietą, prisijungimo patvirtinimas (čia 8 etape bus login) |
| `Scripts/Runtime/Networking/NetworkLaunchOptions.cs` | Komandinės eilutės parametrai `-server`, `-host`, `-client`, `-ip`, `-port` |
| `Scripts/Runtime/Networking/PlayerNetworkMovement.cs` | Savininko pranešimai, serverio patikra ir korekcijos, kitų žaidėjų interpoliacija |
| `Scripts/Runtime/Networking/MovementValidator.cs` | Greičio patikra („judėjimo biudžetas“) |
| `Scripts/Runtime/Networking/MotionState.cs` | Per tinklą siunčiama būsena: pozicija, kryptis, ar juda, serverio laikas |
| `Scripts/Runtime/Networking/DevConnectionPanel.cs` | Prisijungimo langas kūrėjui |
| `Scripts/Runtime/Networking/DevBot.cs` | Testavimo botas (`-bot`) |
| `Scripts/Editor/BuildMenu.cs` | Build ir paleidimo meniu |
| `Scripts/Editor/TestSceneBuilder.cs` | Sukuria NetworkManager ir `Player Spawn`; Player prefab'e dabar yra `NetworkObject` ir `PlayerNetworkMovement` |
| `Tests/PlayMode/NetworkLaunchOptionsTests.cs`, `MovementValidatorTests.cs`, `NetworkHostTests.cs` | Nauji testai |

Pakeisti failai:
- `PlayerMovement`: pridėti `MoveSpeed` ir `Teleport()`.
- `CharacterAnimator`: pridėtas `MotionSource`, kuriuo nurodoma, kas valdo animaciją.

## Kaip patikrinti

### 1. Greičiausias būdas – Host redaktoriuje
1. `RPG > Build Test Scene`, tada **Play**.
2. Viršuje kairėje spausk **Host**. Atsiras veikėjas, kurį valdai kaip anksčiau (WASD ir pele).

### 2. Du žaidėjai: build + redaktorius
1. `RPG > Build > Windows Player` (pirmą kartą užtrunka apie minutę).
2. `RPG > Run > Local Server`. Serveris pasileidžia fone be lango, jo žurnalas – `Builds/Windows/server.log`.
3. `RPG > Run > Local Bot Client`. Atsidaro langas, kuriame veikėjas pats vaikšto.
4. Redaktoriuje **Play**, tada **Client**. Turi matyti savo veikėją ir boto veikėją, kuris sklandžiai vaikšto su animacija.
5. Pajudėk – boto lange turi matytis, kaip juda tavo veikėjas.
6. Uždaryk: Disconnect redaktoriuje, boto langą uždaryk, o serverį sustabdyk per Task Manager (procesas `RPG-s.exe`).

### 3. Keli redaktoriai: Multiplayer Play Mode
`Window > Multiplayer > Multiplayer Play Mode` → pažymėk Player 2 (ir 3, 4). Paspaudus Play atsidarys papildomi redaktoriaus langai. Viename spausk **Host**, kituose **Client**.

### 4. Serverio apsaugos patikrinimas
`Builds/Windows/server.log` matysi `[Server] Rejected movement ...`, jei kas nors bandys judėti per greitai arba kiaurai sieną. Normaliai žaidžiant tokių eilučių neturi būti.

Per šį etapą patikrinau su tikru serverio procesu ir redaktoriumi kaip klientu:
- normalus judėjimas – 0 korekcijų;
- teleportas 5 vienetų – atmestas ir grąžintas;
- šuolis 1 vienetu kiaurai uolų sieną – atmestas ir grąžintas;
- boto klientas – judėjimas, animacija ir kryptis matomi kitame kliente, 0 korekcijų.

### Automatiniai testai
`Window > General > Test Runner` → **PlayMode** → **Run All** – **36** testai, tarp jų:
- tikras Host paleidimas su Player prefab'u;
- greičio patikros atvejai: normalus ėjimas, teleportas, dvigubas greitis, tinklo trūkčiojimas;
- komandinės eilutės parametrų skaitymas.

## Komandinė eilutė

```
RPG-s.exe -batchmode -nographics -server -port 7777     dedikuotas serveris
RPG-s.exe -host -port 7777                              host
RPG-s.exe -client -ip 192.168.1.10 -port 7777           klientas
RPG-s.exe -client -ip 127.0.0.1 -bot                    klientas-botas
```

Build'as, paleistas `-batchmode` režimu be kitų parametrų, pasileidžia kaip serveris.

## Dedikuotas serveris be grafikos

Tikram serveriui geriau naudoti Unity **Dedicated Server** build'ą: jis mažesnis, be grafikos ir garso. Šiuo metu tavo Unity tam modulio neturi. Kaip jį įdiegti:
Unity Hub → Installs → 6000.6.4f1 → ⚙ → Add modules → **Windows Dedicated Server Build Support** (ir **Linux Dedicated Server Build Support**, jei serveris bus Linux VPS). Tada `RPG > Build > Windows Dedicated Server`.

Kol modulio nėra, tas pats `RPG-s.exe` su `-batchmode -nographics -server` veikia kaip serveris.

Jei serveris turi būti pasiekiamas iš kito kompiuterio, Windows ugniasienėje reikia leisti UDP prievadą 7777.

## Žinomi apribojimai (spręsime vėliau)
- **Nėra paskyrų ir slaptažodžių.** Bet kas, žinantis adresą, gali prisijungti. Tai bus 8 etapas (login per `NetworkBootstrap.ApproveConnection`).
- **Visi mato visus.** Interest management (kad būtų siunčiami tik artimi žaidėjai) reikės, kai žaidėjų bus daug arba atsiras didesnis pasaulis.
- **Kliento prognozė paprasta.** Korekcija veikėją tiesiog perkelia (teleportas). Sąžiningai žaidžiant korekcijų nėra, todėl to nesimato.
- Viena scena: NetworkManager yra pasaulio scenoje. Atskira bootstrap scena ir scenų valdymas bus reikalingi požemiams (10 etapas).
