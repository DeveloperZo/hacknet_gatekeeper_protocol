# SETUP GUIDE — Get Hello World Running

Follow these steps in order. Each section ends with a test to confirm before moving on.

---

## Step 1 — Find Your Hacknet Install Path

Default Steam path:
```
C:\Program Files (x86)\Steam\steamapps\common\Hacknet\
```

If you installed to a different drive, find it via:  
Steam → Right-click Hacknet → Properties → Local Files → Browse...

**You need this path for Step 3 and Step 4.**

---

## Step 2 — Install Pathfinder

1. Go to: https://github.com/Arkhist/Hacknet-Pathfinder/releases
2. Download the latest `PathfinderInstaller.exe`
3. Run it — it should auto-detect your Hacknet folder
4. Click Install

**Test:** Launch Hacknet from Steam. The loading screen should have a small Pathfinder version tag in the corner, OR you'll see a BepInEx console window appear behind Hacknet.

If the console window appears, Pathfinder is working. You'll see lines like:
```
[Info   :   BepInEx] BepInEx 5.x.x - Hacknet
[Info   : PathfinderAPI] Pathfinder loaded
```

---

## Step 3 — Install ZeroDayToolKit

1. Go to: https://github.com/prodzpod/ZeroDayToolKit/releases
2. Download `ZeroDayToolKit.dll`
3. Drop it into:
   ```
   C:\...\Hacknet\BepInEx\plugins\ZeroDayToolKit.dll
   ```

**Test:** Launch Hacknet, check BepInEx console for:
```
[Info   : ZeroDayToolKit] ZeroDayToolKit loaded
```

---

## Step 4 — Set Up the Plugin Project

### Option A: Visual Studio (recommended)
1. Install Visual Studio 2022 Community (free)
   - During install, select workload: **.NET desktop development**
2. Open `C:\Users\awill\Hacknet_GP\Plugin\GatekeeperPlugin.csproj` in VS
3. Open `GatekeeperPlugin.csproj` in a text editor and update the Hacknet path:
   ```xml
   <HacknetPath>C:\Program Files (x86)\Steam\steamapps\common\Hacknet</HacknetPath>
   ```
   Change this to match your actual install path.

### Option B: VS Code + .NET CLI
1. Install VS Code + C# extension
2. Install .NET SDK (any version — we target net40 but the SDK handles cross-compilation)
3. Same path update in the .csproj file

---

## Step 5 — Verify References Exist

Before building, confirm these files exist in your Hacknet folder:
```
Hacknet/
├── HacknetPathfinder.exe    ← Main game exe (after Pathfinder install)
├── FNA.dll                  ← XNA replacement
└── BepInEx/
    ├── core/
    │   ├── BepInEx.dll
    │   └── 0Harmony.dll
    └── plugins/
        └── PathfinderAPI.dll
```

If `PathfinderAPI.dll` is in a different location (sometimes it's in `core/`), update the `.csproj` HintPath accordingly.

---

## Step 6 — Build the Plugin

### Visual Studio:
- Press `Ctrl+Shift+B` or Build → Build Solution

### .NET CLI:
```bash
cd C:\Users\awill\Hacknet_GP\Plugin
dotnet build
```

**Expected output:**
```
Build succeeded.
  GatekeeperPlugin -> ...\bin\Debug\net40\GatekeeperPlugin.dll
  Deploying to Hacknet BepInEx plugins...
  Done — GatekeeperPlugin.dll copied to ...\BepInEx\plugins
```

The `.csproj` auto-copies the DLL on successful build.

---

## Step 7 — Test In Game

1. Launch Hacknet from Steam (make sure to use Extensions mode)
2. Load or start any extension (even the blank intro)
3. In the terminal, type:
   ```
   gp_hello
   ```
4. You should see:
   ```
   ╔═══════════════════════════════════════╗
   ║   GATEKEEPER PROTOCOL v0.1            ║
   ║   Plugin loaded successfully.         ║
   ...
   ```

**If the command isn't recognized:** Check the BepInEx console for any error on load. The most common issue is a missing reference DLL — confirm all HintPaths in the .csproj are correct.

---

## Troubleshooting

### "Could not load file or assembly"
One of the referenced DLLs is missing or at a different path.
- Open BepInEx console, read the full error — it names the missing assembly
- Update the HintPath in .csproj

### Build succeeds but command doesn't appear in game
- Confirm the DLL copied to `BepInEx/plugins/` — check the output folder manually
- Hacknet may need to be restarted (it doesn't hot-reload plugins)

### "CommandManager not found" compile error
- `PathfinderAPI.dll` reference is wrong — Pathfinder.Command.CommandManager lives there
- Confirm the DLL path and that Pathfinder is installed

### BepInEx console doesn't appear
- Pathfinder may not be installed correctly
- Try running `HacknetPathfinder.exe` directly instead of through Steam

---

## What Comes Next (After Hello World Works)

Once `gp_hello` prints to your terminal, the full development path is:

| Step | Task | Complexity |
|------|------|-----------|
| 2 | Add Harmony — patch ExeModule for CPU speed | Medium |
| 3 | Add Harmony — patch trace for Network | Medium |
| 4 | Add Harmony — SSD enforcement on scp | Medium |
| 5 | Create first test Extension XML (blank nodes) | Low |
| 6 | Build first CORE port executable (VAULT) | High |
| 7 | Write first Gatekeeper hacker script | Low |
| 8 | Wire up upgrade shop node | Medium |

See `Docs/OPEN_QUESTIONS.md` for what to research while the build toolchain is being set up.
