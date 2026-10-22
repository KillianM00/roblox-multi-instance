# Multi Roblox Manager

A Windows desktop app to run multiple Roblox accounts side-by-side in separate Roblox processes. Built with .NET 8 + WPF.

## Features

- **Multi-instance**: holds the `ROBLOX_singletonEvent` named mutex so additional Roblox processes can launch.
- **Per-account auth**: stores `.ROBLOSECURITY` cookies and fetches a fresh authentication ticket for each launch (no cookie injection into a running client).
- **Encrypted storage**: account data is encrypted on disk with Windows DPAPI (`CurrentUser` scope — only your Windows account can decrypt it).
- **Batch launch**: select N accounts, paste a game URL or place ID, click Launch — staggered with a configurable delay.
- **Camera-grid Instances view**: live DWM thumbnails of every running Roblox window. Double-click any tile to enter Play mode — the Roblox window is repositioned over the app's play area and given focus so you can play inside it. "Back to Grid" sends it back.
- **Account avatars**: headshot thumbnails pulled from the Roblox thumbnails API.
- **Window management**: arrange all Roblox windows in a desktop grid, minimize all, or focus a single instance.
- **Tabbed UI**: Accounts and Instances tabs in a dark, card-based layout.

## Quick start

Requirements:

- Windows 10/11
- .NET 8 SDK (build) or .NET 8 Desktop Runtime (run)
- Roblox installed

Clone and run:

```powershell
git clone https://github.com/<you>/roblox-multi-instance.git
cd roblox-multi-instance
dotnet run --project src/RobloxMultiInstance
```

Or build a release binary:

```powershell
dotnet publish src/RobloxMultiInstance -c Release -r win-x64 --self-contained false -o publish
.\publish\RobloxMultiInstance.exe
```

## Using it

1. **Accounts tab → Add an account**: click `+ Add Account`, paste a `.ROBLOSECURITY` cookie, click `Validate` to confirm, then `Save`. Avatar headshot is fetched automatically.
2. **Enter a game**: paste a Roblox game URL (e.g. `https://www.roblox.com/games/920587237/...`) or the place ID directly.
3. **Select accounts**: check the boxes for the accounts you want to launch.
4. **Launch**: click `Launch Selected`. Each account opens in its own Roblox process.
5. **Switch to the Instances tab** to see live thumbnails of every Roblox window in a camera-grid layout.
6. **Click "Play Here"** on any tile (or double-click) to enter Play mode: that Roblox window is moved over the play area and brought to the foreground so you can move/control it directly. Other instances stay in a side panel of mini-thumbnails — click "Switch" on any of them to hand control over. Click "Back to Grid" to return.

### Getting your `.ROBLOSECURITY` cookie

1. Log into roblox.com in a browser.
2. F12 → Application tab → Cookies → `https://www.roblox.com`.
3. Copy the value of the `.ROBLOSECURITY` cookie (it starts with `_|WARNING:-DO-NOT-SHARE-THIS...`).

Treat this cookie like a password — anyone who has it can sign into your account.

## How it works

### The mutex trick

Roblox prevents itself from launching multiple times by checking a named mutex called `ROBLOX_singletonEvent`. On startup, this app opens a handle to that mutex and keeps it for the app's lifetime. With the mutex already in existence, the Roblox client's singleton check stops blocking additional launches.

### The auth ticket dance

`.ROBLOSECURITY` cookies are session tokens that the Roblox website uses. The Roblox player binary doesn't accept the cookie directly — it expects a short-lived **authentication ticket**. Each launch does:

1. POST `https://auth.roblox.com/v1/authentication-ticket/` with the cookie → server returns a `403` with an `x-csrf-token` header.
2. POST the same URL again with `X-CSRF-TOKEN` + `RBXAuthenticationNegotiation: 1` headers → server returns `rbx-authentication-ticket`.
3. Construct a `roblox-player:` URI with `gameinfo:<ticket>` + `placelauncherurl:<encoded launcher URL>` and start it with `Process.Start`.

### Storage

Accounts live in `%LOCALAPPDATA%\RobloxMultiInstance\accounts.dat`, encrypted with [DPAPI](https://learn.microsoft.com/en-us/dotnet/standard/security/how-to-use-data-protection) (`CurrentUser` scope). The file is unreadable on another machine or under another Windows user.

### Live thumbnails

The Instances view uses [DWM thumbnails](https://learn.microsoft.com/en-us/windows/win32/dwm/thumbnail-ovw) — the same API Windows uses for Alt+Tab previews. `DwmRegisterThumbnail` registers each Roblox window's HWND as a thumbnail source for our app, and `DwmUpdateThumbnailProperties` repositions the destination rectangle on every layout pass. DWM composites the live window contents into our app at near-zero cost.

Process-to-account association is done by snapshotting the set of `RobloxPlayerBeta.exe` PIDs immediately before launch, then claiming the next-appearing new PID for that account.

### Click-to-control

Play mode moves the selected Roblox window over the app's play area via `SetWindowPos`. When you click "Back to Grid", the window is restored to its prior position. This avoids the complexity (and crashes) of true `SetParent` window reparenting while still giving an embedded feel.

## Security notes

- Cookie storage is encrypted at rest, but the cookie is loaded into memory while the app runs. Don't run this on shared / untrusted machines.
- The authentication service communicates with several Roblox services for authentication, account details, avatars, presence, friend requests, and share-link resolution. Review [`RobloxAuth.cs`](src/RobloxMultiInstance/Services/RobloxAuth.cs) for the current endpoints and credential handling; the app is not limited to two hosts.
- This is a third-party tool, not affiliated with or endorsed by Roblox Corporation.

## Project layout

```
src/RobloxMultiInstance/
  App.xaml / App.xaml.cs              # entry, DI-lite singletons, dark theme
  MainWindow.xaml / .cs               # shell: top bar with nav tabs + mutex indicator
  Views/
    AccountsView.xaml / .cs           # account list, batch launch
    InstancesView.xaml / .cs          # camera-grid + play mode
    AddAccountWindow.xaml / .cs       # add/edit account dialog
  Models/
    Account.cs                        # alias, cookie, username, avatar URL
    RobloxInstance.cs                 # PID, HWND, account binding
  Services/
    MutexHolder.cs                    # ROBLOX_singletonEvent
    AccountStore.cs                   # DPAPI-encrypted JSON store
    RobloxAuth.cs                     # CSRF + auth ticket flow + avatar API
    RobloxLauncher.cs                 # roblox-player: URI construction
    RobloxProcessTracker.cs           # polls processes, associates with accounts
    WindowManager.cs                  # focus / move / arrange wrappers
    Win32.cs / Dwm.cs                 # P/Invoke
  Controls/
    DwmThumbnailHost.cs               # WPF FrameworkElement hosting a DWM thumbnail
  ViewModels/
    MainWindowViewModel.cs            # tab nav + mutex pill
    AccountsViewModel.cs              # account-side commands
    AccountItemViewModel.cs           # per-account row
    InstancesViewModel.cs             # live tracker → tiles
    InstanceTileViewModel.cs          # per-instance tile
    AddAccountViewModel.cs            # add/edit dialog
  Converters/Converters.cs
```

## License

MIT — see [LICENSE](LICENSE).
