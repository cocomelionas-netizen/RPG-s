# 8 etapas – paskyros ir progreso išsaugojimas

## Ką padarėme

- **Prisijungimo langas** (ekrano viduryje, F1 slepia):
  - **Name** ir **Password**;
  - **New character** – sukuria naują veikėją. Pasirenki klasę (Warrior / Ranger / Mage), ir jos pakeisti vėliau nebegalima. Testiniai klasės mygtukai pašalinti;
  - **Play offline**, **Host**, **Join** (prisijungti prie serverio), **Server only**. Naršyklėje rodomi tik Offline ir Join;
  - klaidos rodomos raudonai: „Wrong name or password.“, „That name is taken.“, „This character is already in the game.“;
  - vardas įsimenamas šiame įrenginyje, slaptažodis – niekada;
  - prisijungus panelė susitraukia į būsenos eilutę su **Log out**.
- **Paskyras tikrina serveris** (`AccountService`, prisijungimo patvirtinimas `NetworkBootstrap.ApproveConnection`):
  - vardas: 3–16 raidžių, skaičių arba _. Didžiosios ir mažosios raidės nesiskiria;
  - slaptažodis: bent 4 simboliai. Saugomas tik druskos ir PBKDF2 hash'o pora (10 000 iteracijų), ne pats slaptažodis;
  - tuo pačiu vardu vienu metu prisijungti galima tik kartą;
  - klientas be teisingo prisijungimo į žaidimą neįleidžiamas.
- **Kas išsaugoma** (`CharacterPersistence`): klasė, lygis, XP, krepšys, ekipiruotė ir vieta pasaulyje. Virš veikėjo ir rėmeliuose rodomas paskyros vardas.
- **Kada išsaugoma:** 2 s po pakeitimo (lygis, daiktai), kas 15 s žaidžiant (vieta) ir atsijungiant (Log out, išjungus serverį).
- **Kur saugoma:**

  | Kur žaidi | Kur saugoma |
  |-----------|-------------|
  | Naršyklėje (Offline) | Naršyklės atmintyje (IndexedDB per PlayerPrefs). Išlieka perkrovus puslapį, bet tik toje naršyklėje ir tame kompiuteryje. |
  | Host / dedikuotas serveris | JSON failai `Application.persistentDataPath/Accounts/<vardas>.json`. Įrašoma pirma į laikiną failą, todėl lūžus serveriui paskyra nesugadinama. |

  Saugykla yra už `IAccountStore` sąsajos, todėl vėliau ją galima pakeisti tikra duomenų baze (pvz. PostgreSQL), nekeičiant žaidimo kodo.
- Paleidus iš komandinės eilutės (`-host`, `-client`, `-bot`), automatiškai sukuriamas vienkartinis veikėjas „BotNNNNN“, todėl testavimo botas veikia kaip anksčiau.

## Failai

| Failas | Paskirtis |
|--------|-----------|
| `Scripts/Runtime/Accounts/AccountService.cs` | Prisijungimų tikrinimas, paskyrų kūrimas, kas prisijungęs |
| `Scripts/Runtime/Accounts/AccountRecord.cs`, `LoginRequest.cs` | Saugomi duomenys ir prisijungimo užklausa (JSON) |
| `Scripts/Runtime/Accounts/PasswordHasher.cs` | Druska ir PBKDF2 hash |
| `Scripts/Runtime/Accounts/AccountStores.cs` | `IAccountStore`: failai, PlayerPrefs (naršyklė), atmintis (testai) |
| `Scripts/Runtime/Accounts/CharacterPersistence.cs` | Veikėjo įkėlimas prisijungus ir išsaugojimas |
| `Tests/PlayMode/AccountTests.cs` | Nauji testai |

Pakeisti failai:
- `NetworkBootstrap`:
  - prisijungimo tikrinimas;
  - `SetLogin`;
  - naršyklėje Offline nebepasileidžia savaime, o laukia prisijungimo.
- `DevConnectionPanel`: prisijungimo langas.
- `CharacterProgress`:
  - `ServerLoad`;
  - pašalintas klasės keitimas iš kliento.
- `Inventory`: `ServerLoad`, `BagSnapshot`, `EquipmentSnapshot`.
- `Health`: `ServerSetDisplayName`.
- Pašalintas `ClassPicker`.
- Scenos įrankis: `AccountService` prie NetworkManager, `CharacterPersistence` žaidėjui.

## Kaip patikrinti naršyklėje

https://cocomelionas-netizen.github.io/RPG-s/ (jei rodo seną versiją, perkrauk su Ctrl+F5)

1. Ekrano viduryje atsiranda prisijungimo langas. Įvesk vardą ir slaptažodį, pažymėk **New character**, pasirink klasę ir spausk **Play offline**.
2. Pakilk lygiu, pasiimk ir užsidėk kokį daiktą, nueik kur nors toliau nuo starto.
3. Palauk kelias sekundes ir **perkrauk puslapį** (F5).
4. Įvesk tą patį vardą ir slaptažodį, **nežymėk** New character ir spausk **Play offline**. Turi atsirasti tas pats veikėjas: lygis, klasė, daiktai ir vieta.
5. Bandyk prisijungti su neteisingu slaptažodžiu – turi būti „Wrong name or password.“
6. Bandyk sukurti veikėją tuo pačiu vardu – turi būti „That name is taken.“

## Automatiniai testai

**PlayMode** → **Run All**. Pridėti 5 nauji `AccountTests` testai:
- slaptažodžio hash;
- paskyros kūrimas, prisijungimas ir atmetimai (blogas slaptažodis, užimtas vardas, jau žaidime, netinkamas vardas ar slaptažodis);
- išsaugotas veikėjas atkuriamas ir vėl išsaugomas;
- naujas veikėjas gauna pasirinktą klasę ir yra išsaugomas;
- su neteisingu slaptažodžiu žaidimas nepasileidžia.

Iš viso **83** testai.

## Žinomi apribojimai
- **Naršyklėje saugoma tik toje naršyklėje.** Kitame kompiuteryje to paties veikėjo nebus, kol žaidimas neveiks per viešą serverį (VPS, žr. `WebGL.md`). Tada viską saugos serveris.
- Viena paskyra – vienas veikėjas. Kelių veikėjų pasirinkimą galima pridėti vėliau.
- Slaptažodžio pakeitimo ir atkūrimo nėra.
- Kai jungiamasi per tinklą (Join), slaptažodis siunčiamas prisijungimo metu. Saugu bus tik su `wss://` (TLS), kai bus viešas serveris.
- Uždarius naršyklės skirtuką, gali dingti paskutinės ~2–15 s progreso, nes uždarymo momentu naršyklė neleidžia patikimai išsaugoti.
