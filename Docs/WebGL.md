# The game in the browser (WebGL) and the automatic build

## How it works

1. Every push to `main` (mine from this computer, or from a cloud session while the computer is off) starts **GitHub Actions**.
2. Actions, with Unity 6000.6.4f1, first regenerates the scene and prefabs (`TestSceneBuilder`, the same as `RPG > Build Test Scene`), then builds the **WebGL** version and publishes it on **GitHub Pages**. It takes about 15–25 min; longer the first time.
3. You open the game on any computer: **https://cocomelionas-netizen.github.io/RPG-s/**
4. If the code does not compile, the build fails and the old version stays. You see the error under GitHub → **Actions**.

In the browser you first log in (name and password, see [Stage8.md](Stage8.md)), then press **Play offline**. The game runs in **Offline** mode: the server and your character run in the same browser, so no outside server is needed. That way you can try movement, combat and the dummies. Multiplayer in the browser needs a public server – see below.

## One-time setup (you have to do this)

### 1. Make the repo public
GitHub → repo **RPG-s** → **Settings** → at the bottom **Danger Zone** → **Change repository visibility** → **Change to public** → confirm.

I checked the repo: there are no passwords or keys in the code or history. All code and the commit authors' e-mail will be public.

### 2. Turn on GitHub Pages
**Settings** → **Pages** → **Build and deployment** → **Source**: choose **GitHub Actions**.

### 3. Add the Unity license (GitHub Secrets)
The Unity build on GitHub needs your Unity account. Do not give me these details – enter them yourself.

First create the license file. Your computer does not have it yet, because newer Unity Hub does not create it by itself:
Unity Hub → ⚙ **Preferences** → **Licenses** → **Add** → **Get a free personal license**. Then the file `C:\ProgramData\Unity\Unity_lic.ulf` appears. The `ProgramData` folder is hidden: in File Explorer turn on **View → Show → Hidden items** or type the path straight into the address bar.

Then on GitHub: **Settings** → **Secrets and variables** → **Actions** → **New repository secret**. Create three:

| Name | Value |
|------|-------|
| `UNITY_EMAIL` | Unity account e-mail |
| `UNITY_PASSWORD` | Unity account password |
| `UNITY_LICENSE` | The whole content of `C:\ProgramData\Unity\Unity_lic.ulf` (open it in Notepad, copy everything) |

Secrets are encrypted. Neither other people nor visitors of a public repo can see them.

### 4. Start the first build
**Actions** → **WebGL build** → **Run workflow**. After that it starts by itself after every push.

## Local WebGL build
`RPG > Build > WebGL (browser)` creates `Builds/WebGL/`. The browser will not open it straight from the file; it needs a local server:

```
cd Builds/WebGL
python -m http.server 8080
```

Then open http://localhost:8080.

## Controls in the browser
The same as on the computer: WASD / mouse, Tab, F, Esc. In the browser Esc also leaves full-screen mode. The login window (middle of the screen, F1) shows only **Play offline** and **Join**, because a browser cannot be a server.

## Multiplayer in the browser (later)
A browser can only connect through **WebSocket**, and the game page is `https`, so the server will need:
- a public computer, e.g. a VPS (about 5 €/month), running the Linux server build;
- WebSocket with TLS (`wss://`), a domain and a certificate.

The client side is ready: in WebGL `Client` connects through WebSocket automatically. The server side comes when it is time.

## Full screen and big screens
A browser in full screen draws at the screen's real pixels (4K and more on high-DPI screens), which made the game
stutter. `RenderResolution` (builds only) draws the world at most about 1080 pixels high and stretches it (URP render
scale), while the HUD stays sharp at the full size, and turns HDR off. F8 shows the screen size and the share the world
is drawn at.

## Download size and speed
Quality and smooth play come first. The atlases are plain high-quality block compression (crunching them made the
download about 15 MB smaller but left blocky artifacts); WebAssembly is optimized for runtime speed (optimizing it for
size made the game stutter). Unused managed code is stripped at Medium (`Assets/_Project/link.xml` keeps the game's own
assembly, Netcode, its transport and the Input System), which only makes the download smaller.
