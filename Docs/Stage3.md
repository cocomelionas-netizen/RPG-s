# Stage 3 – multiplayer foundation (Netcode for GameObjects)

## What we did

- **Netcode for GameObjects 2.13.3** and **Multiplayer Play Mode 3.0.0** packages.
- **The game can be started in three ways:**
  - **Server** – server only, without a character of its own (dedicated);
  - **Host** – server and player in one;
  - **Client** – connects to a server.

  The mode is chosen with a button or a command-line parameter.
- **The player appears on connecting.** The server creates a character next to the `Player Spawn` point for everyone who connects. There is no character in the scene beforehand. The camera and the click marker are attached to *your own* character.
- **Movement: client prediction and server checks.**
  - Your character moves at once, without delay. The client reports its position to the server 20 times a second.
  - The server checks every report:
    - **speed** – a "movement budget": up to 125 % of normal speed per second, small network hiccups are let through;
    - **walls** – whether there is an obstacle between the last accepted and the new position;
    - **ownership** – another client cannot move a character that is not its own.
  - When a check fails, the server puts the character back at the last accepted position (a correction) and writes it to the log. This stops speed hacks, teleports and walking through walls.
- **Other players** are shown 0.15 s "in the past", smoothly between two server states (interpolation), so they move smoothly, not in jumps. Their animation (Idle/Walk, 8 directions) works through the same `ICharacterMotionSource` interface. Players walk through each other, as in most MMOs.
- **Developer connection panel** (top left, F1 hides it): address, port and Host / Client / Server buttons. Once connected it shrinks to a status line with a Disconnect button.
- **Test bot.** With the `-bot` parameter your character walks around the start on its own, so you can test multiplayer alone.
- **Build menu:**
  - `RPG > Build > Windows Player`;
  - `RPG > Build > Windows Dedicated Server` – needs a Unity Hub module, see below;
  - `RPG > Run > Local Server / Local Client / Local Bot Client`.
- **Worked around an NGO bug.** UnityTransport reads `-ip`/`-port` by itself and wrongly tries to read the `-ip` value as the port. We pass the settings explicitly.

## Files

| File | Purpose |
|------|---------|
| `Scripts/Runtime/Networking/NetworkBootstrap.cs` | Starts server/host/client, sets the spawn point, connection approval (where the stage 8 login goes) |
| `Scripts/Runtime/Networking/NetworkLaunchOptions.cs` | Command-line parameters `-server`, `-host`, `-client`, `-ip`, `-port` |
| `Scripts/Runtime/Networking/PlayerNetworkMovement.cs` | Owner reports, server checks and corrections, interpolation of other players |
| `Scripts/Runtime/Networking/MovementValidator.cs` | Speed check ("movement budget") |
| `Scripts/Runtime/Networking/MotionState.cs` | State sent over the network: position, direction, moving or not, server time |
| `Scripts/Runtime/Networking/DevConnectionPanel.cs` | Developer connection panel |
| `Scripts/Runtime/Networking/DevBot.cs` | Test bot (`-bot`) |
| `Scripts/Editor/BuildMenu.cs` | Build and run menus |
| `Scripts/Editor/TestSceneBuilder.cs` | Creates the NetworkManager and `Player Spawn`; the Player prefab now has `NetworkObject` and `PlayerNetworkMovement` |
| `Tests/PlayMode/NetworkLaunchOptionsTests.cs`, `MovementValidatorTests.cs`, `NetworkHostTests.cs` | New tests |

Changed files:
- `PlayerMovement`: added `MoveSpeed` and `Teleport()`.
- `CharacterAnimator`: added `MotionSource`, which says what drives the animation.

## How to check

### 1. Quickest – Host in the editor
1. `RPG > Build Test Scene`, then **Play**.
2. Press **Host** at the top left. A character appears that you control as before (WASD and mouse).

### 2. Two players: build + editor
1. `RPG > Build > Windows Player` (the first time takes about a minute).
2. `RPG > Run > Local Server`. The server starts in the background without a window; its log is `Builds/Windows/server.log`.
3. `RPG > Run > Local Bot Client`. A window opens where the character walks on its own.
4. In the editor press **Play**, then **Client**. You must see your character and the bot's character walking smoothly with animation.
5. Move – in the bot's window your character must be seen moving.
6. To close: Disconnect in the editor, close the bot's window, and stop the server in Task Manager (process `RPG-s.exe`).

### 3. Several editors: Multiplayer Play Mode
`Window > Multiplayer > Multiplayer Play Mode` → tick Player 2 (and 3, 4). Pressing Play opens extra editor windows. Press **Host** in one and **Client** in the others.

### 4. Checking the server's protection
`Builds/Windows/server.log` shows `[Server] Rejected movement ...` when someone tries to move too fast or through a wall. In normal play there must be no such lines.

During this stage I checked it with a real server process and the editor as a client:
- normal movement – 0 corrections;
- a 5-unit teleport – rejected and put back;
- a 1-unit jump through a rock wall – rejected and put back;
- the bot client – movement, animation and direction visible in the other client, 0 corrections.

### Automatic tests
`Window > General > Test Runner` → **PlayMode** → **Run All** – **36** tests, among them:
- a real Host start with the Player prefab;
- speed check cases: normal walking, a teleport, double speed, network hiccups;
- reading command-line parameters.

## Command line

```
RPG-s.exe -batchmode -nographics -server -port 7777     dedicated server
RPG-s.exe -host -port 7777                              host
RPG-s.exe -client -ip 192.168.1.10 -port 7777           client
RPG-s.exe -client -ip 127.0.0.1 -bot                    bot client
```

A build started with `-batchmode` and no other parameters runs as a server.

## Dedicated server without graphics

A real server is better built as a Unity **Dedicated Server** build: it is smaller, without graphics and sound. Your Unity does not have that module yet. To install it:
Unity Hub → Installs → 6000.6.4f1 → ⚙ → Add modules → **Windows Dedicated Server Build Support** (and **Linux Dedicated Server Build Support** if the server will be a Linux VPS). Then `RPG > Build > Windows Dedicated Server`.

Until the module is there, the same `RPG-s.exe` with `-batchmode -nographics -server` works as a server.

For the server to be reachable from another computer, the Windows firewall must allow UDP port 7777.

## Known limitations (to solve later)
- **No accounts or passwords.** Anyone who knows the address can connect. That is stage 8 (login through `NetworkBootstrap.ApproveConnection`).
- **Everyone sees everyone.** Interest management (sending only nearby players) will be needed when there are many players or a bigger world.
- **Client prediction is simple.** A correction simply moves the character (teleport). In fair play there are no corrections, so it is not visible.
- One scene: the NetworkManager is in the world scene. A separate bootstrap scene and scene management will be needed for dungeons (stage 10).
