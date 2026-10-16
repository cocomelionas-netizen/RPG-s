# Stage 8 – accounts and saving progress

## What we did

- **Login window** (in the middle of the screen, F1 hides it):
  - **Name** and **Password**;
  - **New character** – creates a new character. You choose the class (Warrior / Ranger / Mage) and it cannot be changed later. The test class buttons were removed;
  - **Play offline**, **Host**, **Join** (connect to a server), **Server only**. In the browser only Offline and Join are shown;
  - errors are shown in red: "Wrong name or password.", "That name is taken.", "This character is already in the game.";
  - the name is remembered on this device, the password never;
  - once logged in the panel shrinks to a status line with **Log out**.
- **The server checks accounts** (`AccountService`, connection approval in `NetworkBootstrap.ApproveConnection`):
  - name: 3–16 letters, digits or _. Upper and lower case are the same;
  - password: at least 4 characters. Only a salt and PBKDF2 hash pair (10 000 iterations) is stored, never the password itself;
  - the same name can be logged in only once at a time;
  - a client without a valid login is not let into the game.
- **What is saved** (`CharacterPersistence`): class, level, XP, bag, equipment and position in the world. The account name is shown above the character and in the frames.
- **When it is saved:** 2 s after a change (level, items), every 15 s while playing (position) and on disconnect (Log out, server shutdown).
- **Where it is saved:**

  | Where you play | Where it is saved |
  |----------------|-------------------|
  | In the browser (Offline) | Browser storage (IndexedDB through PlayerPrefs). Survives a page reload, but only in that browser on that computer. |
  | Host / dedicated server | JSON files `Application.persistentDataPath/Accounts/<name>.json`. Written to a temporary file first, so a server crash does not corrupt an account. |

  Storage is behind the `IAccountStore` interface, so it can later be replaced by a real database (e.g. PostgreSQL) without changing game code.
- Started from the command line (`-host`, `-client`, `-bot`), a throwaway character "BotNNNNN" is created automatically, so the test bot works as before.

## Files

| File | Purpose |
|------|---------|
| `Scripts/Runtime/Accounts/AccountService.cs` | Login checks, account creation, who is logged in |
| `Scripts/Runtime/Accounts/AccountRecord.cs`, `LoginRequest.cs` | Saved data and the login request (JSON) |
| `Scripts/Runtime/Accounts/PasswordHasher.cs` | Salt and PBKDF2 hash |
| `Scripts/Runtime/Accounts/AccountStores.cs` | `IAccountStore`: files, PlayerPrefs (browser), memory (tests) |
| `Scripts/Runtime/Accounts/CharacterPersistence.cs` | Loading the character on login and saving it |
| `Tests/PlayMode/AccountTests.cs` | New tests |

Changed files:
- `NetworkBootstrap`:
  - login checks;
  - `SetLogin`;
  - in the browser Offline no longer starts by itself but waits for a login.
- `DevConnectionPanel`: the login window.
- `CharacterProgress`:
  - `ServerLoad`;
  - removed class changes from the client.
- `Inventory`: `ServerLoad`, `BagSnapshot`, `EquipmentSnapshot`.
- `Health`: `ServerSetDisplayName`.
- Removed `ClassPicker`.
- Scene tool: `AccountService` on the NetworkManager, `CharacterPersistence` for the player.

## How to check in the browser

https://cocomelionas-netizen.github.io/RPG-s/ (if it shows an old version, reload with Ctrl+F5)

1. A login window appears in the middle of the screen. Enter a name and password, tick **New character**, choose a class and press **Play offline**.
2. Level up, pick up and equip an item, walk somewhere further from the start.
3. Wait a few seconds and **reload the page** (F5).
4. Enter the same name and password, **do not** tick New character and press **Play offline**. The same character must appear: level, class, items and position.
5. Try to log in with a wrong password – it must say "Wrong name or password."
6. Try to create a character with the same name – it must say "That name is taken."

## Automatic tests

**PlayMode** → **Run All**. 5 new `AccountTests`:
- password hash;
- account creation, login and refusals (wrong password, name taken, already in the game, invalid name or password);
- a saved character is restored and saved again;
- a new character gets the chosen class and is saved;
- with a wrong password the game does not start.

**83** tests in all.

## Known limitations
- **In the browser it is saved only in that browser.** On another computer the same character is not there until the game runs on a public server (VPS, see `WebGL.md`). Then the server saves everything.
- One account – one character. Choosing between several characters can be added later.
- There is no password change or recovery.
- When connecting over the network (Join), the password is sent at login. It is only safe with `wss://` (TLS), once there is a public server.
- Closing the browser tab can lose the last ~2–15 s of progress, because the browser does not allow saving reliably at that moment.
