# Žaidimas naršyklėje (WebGL) ir automatinis build'as

## Kaip tai veikia

1. Kiekvienas push'as į `main` (mano iš šio kompiuterio arba debesų sesijos, kai kompiuteris išjungtas) paleidžia **GitHub Actions**.
2. Actions su Unity 6000.6.4f1 pirmiausia iš naujo sugeneruoja sceną ir prefab'us (`TestSceneBuilder`, tas pats kaip `RPG > Build Test Scene`), tada sukuria **WebGL** versiją ir paskelbia ją **GitHub Pages**. Tai užtrunka maždaug 15–25 min; pirmą kartą ilgiau.
3. Žaidimą atsidarai bet kuriame kompiuteryje: **https://cocomelionas-netizen.github.io/RPG-s/**
4. Jei kodas nesikompiliuoja, build'as nepavyksta ir senoji versija lieka. Klaidą matysi GitHub → **Actions** skiltyje.

Naršyklėje pirmiausia prisijungi (vardas ir slaptažodis, žr. [Stage8.md](Stage8.md)), tada spaudi **Play offline**. Žaidimas veikia **Offline** režimu: serveris ir tavo veikėjas veikia toje pačioje naršyklėje, todėl išorinio serverio nereikia. Taip gali bandyti judėjimą, kovą ir manekenus. Multiplayer'iui naršyklėje reikės viešo serverio – žr. toliau.

## Vienkartinis paruošimas (tai padaryti turi tu)

### 1. Padaryk repo viešą
GitHub → repo **RPG-s** → **Settings** → apačioje **Danger Zone** → **Change repository visibility** → **Change to public** → patvirtink.

Repo patikrinau: kode ir istorijoje nėra slaptažodžių ar raktų. Viešai bus matomas visas kodas ir commit'ų autorių el. paštas.

### 2. Įjunk GitHub Pages
**Settings** → **Pages** → **Build and deployment** → **Source**: pasirink **GitHub Actions**.

### 3. Pridėk Unity licenciją (GitHub Secrets)
Unity build'ui GitHub'e reikia tavo Unity paskyros. Šių duomenų man neduok – įvesk juos pats.

Pirmiausia sukurk licencijos failą. Tavo kompiuteryje jo dar nėra, nes naujesnis Unity Hub jo pats nesukuria:
Unity Hub → ⚙ **Preferences** → **Licenses** → **Add** → **Get a free personal license**. Po to atsiras failas `C:\ProgramData\Unity\Unity_lic.ulf`. Aplankas `ProgramData` paslėptas: File Explorer'yje įjunk **View → Show → Hidden items** arba įvesk kelią tiesiai į adreso juostą.

Tada GitHub: **Settings** → **Secrets and variables** → **Actions** → **New repository secret**. Sukurk tris:

| Pavadinimas | Reikšmė |
|-------------|---------|
| `UNITY_EMAIL` | Unity paskyros el. paštas |
| `UNITY_PASSWORD` | Unity paskyros slaptažodis |
| `UNITY_LICENSE` | Viso failo `C:\ProgramData\Unity\Unity_lic.ulf` turinys (atidaryk Notepad, kopijuok viską) |

Secrets yra užšifruoti. Jų nemato nei kiti žmonės, nei viešo repo lankytojai.

### 4. Paleisk pirmą build'ą
**Actions** → **WebGL build** → **Run workflow**. Po to jis pasileis pats po kiekvieno push'o.

## Lokalus WebGL build'as
`RPG > Build > WebGL (browser)` sukuria `Builds/WebGL/`. Atidaryti tiesiai iš failo naršyklė neleidžia, reikia vietinio serverio:

```
cd Builds/WebGL
python -m http.server 8080
```

Tada atsidaryk http://localhost:8080.

## Valdymas naršyklėje
Toks pat kaip kompiuteryje: WASD / pelė, Tab, F, Esc, pelės ratukas. Esc naršyklėje taip pat išjungia pilno ekrano režimą. Prisijungimo langas (ekrano viduryje, F1) rodo tik **Play offline** ir **Join**, nes naršyklė negali būti serveriu.

## Multiplayer naršyklėje (vėliau)
Naršyklė gali jungtis tik per **WebSocket**, o žaidimo puslapis yra `https`, todėl serveriui reikės:
- viešo kompiuterio, pvz. VPS (apie 5 €/mėn.), kuriame veiktų Linux serverio build'as;
- WebSocket su TLS (`wss://`), domeno ir sertifikato.

Kliento pusė jau paruošta: WebGL'e `Client` automatiškai jungiasi per WebSocket. Serverio pusę darysime, kai ateis laikas.
