# OPEN QUESTIONS & RESEARCH TASKS

Last updated: Post-Pathfinder-install research session.

---

## RESOLVED

| Item | Resolution |
|------|-----------|
| Pathfinder executable base class | `Pathfinder.Executable.BaseExecutable` |
| Update method signature | `void Update(float t)` — plain float, NOT GameTime |
| Flag check API | `os.Flags.HasFlag("flagname")` — confirmed in OpenHacknet OS.cs |
| Flag add API | `os.Flags.AddFlag("flagname")` |
| Get computer by IP | `Programs.getComputer(os, targetIP)` |
| Open a port | `Programs.getComputer(os, targetIP).openPort("ssh", os.thisComputer.ip)` |
| Start a trace | `computer.hostileActionTaken()` — confirmed in Pathfinder docs |
| CPU stat implementation | Custom GP cracker executables — no Harmony needed |
| Executable registration | `[Pathfinder.Meta.Load.Executable("#TAG#")]` attribute |

---

## Priority 1 — Blockers for compilation

### Q1: `Computer.traceTime` — exact field name and access
**Blocker for:** TracePatch  
**Current code assumes:** `__instance.traceTime` (float, seconds)  
**To verify:** Open HacknetPathfinder.exe in ILSpy. Find the `Computer` class, look for a float field related to trace duration.  
**Alternative names to try:** `traceTime`, `traceSeconds`, `traceRemaining`  
**Risk:** If wrong, compilation fails. Fix: rename field reference.

### Q2: `Programs.scp` — exact method signature
**Blocker for:** SsdPatch  
**Current approach:** `AccessTools.Method(typeof(Programs), "scp")` — reflection by name only  
**Resilient:** If the method isn't found, SsdPatch.TargetMethod() returns null and Harmony skips the patch. No crash.  
**To verify:** ILSpy → Programs class → find scp method. If it's named differently (e.g., "SCPFile"), update the string.

### Q3: `Folder` class API — field names for files and subfolders
**Blocker for:** SsdPatch.GetTotalBytes and CountFolder  
**Current code assumes:** `folder.files` (List<FileEntry>), `folder.folders` (List<Folder>), `file.data` (string)  
**To verify:** ILSpy → Folder class. Look for the collections.  
**Alternative names:** `files`, `fileList`, `data`, `content`

---

## Priority 2 — Verify before first test

### Q4: Does `os.Flags.HasFlag` exist or is it `os.Flags.HasFlag(string)`?
From OpenHacknet OS.cs: `Flags.HasFlag("TutorialComplete")` — this is the vanilla Hacknet Flags class.  
Pathfinder may have replaced this. **Test:** Try compiling with `os.Flags.HasFlag("test")` and see if the Flags type resolves.

### Q5: ZeroDayToolKit SetRAM — does it persist to save?
**Current plan:** Use `<SetRAM ram="1024">` mission action for RAM upgrades.  
**Risk:** If not saved, RAM resets on reload. If that's the case, need to re-apply RAM on extension load from flag.  
**Test:** Fire SetRAM, save game, reload, check RAM panel.

### Q6: `Computer.portsNeededForCrack` vs PFPorts counting
From Pathfinder release notes: "Fixed #149: Computer.portsNeededForCrack was one less than Vanilla Hacknet's values."  
**Status:** Fixed in 5.3.4 (our installed version). Should be safe to use `portsForCrack val="3"` meaning player needs 3 ports.  
**Test:** Create test node with 2 standard + 1 custom PFPort. Set portsForCrack to 3. Confirm porthack fires after all 3 open.

---

## Priority 3 — Design verification

### Q7: Can `openPort` take a Pathfinder custom port name?
The crackers call `target.openPort("ssh", ...)`. For CORE ports with custom names (e.g., "v3"), does `openPort` accept the custom name?  
**Expected:** Yes — Pathfinder's port system should support this. Test needed.

### Q8: `Programs.getComputer` — does it work from inside an executable's Update?
The executable has access to `os` and `targetIP`. Calling `Programs.getComputer(os, targetIP)` should return the connected computer.  
**Risk:** If targetIP is not set correctly by the time Update fires, this returns null (we guard against null).

### Q9: SsdPatch — removing last file from `home` may break things
If the player scps a tool to `/bin` instead of `/home`, the SSD check removes from `/home` which might be wrong.  
**Better approach:** Track the actual file written during scp (need the scp method args), then remove that specific file.  
**Current state:** Remove-last-from-home is a simple fallback. May cause incorrect removals.

### Q10: Two Harmony patches simultaneously — TracePatch and SsdPatch
No known conflict between these two patches. TracePatch targets `Computer.hostileActionTaken`, SsdPatch targets `Programs.scp`. No overlap.

---

## Compilation Checklist

Before building the .dll, verify these items compile correctly:

- [ ] `using Pathfinder.Executable;` resolves (from PathfinderAPI.dll)
- [ ] `using Pathfinder.Meta.Load;` resolves (for Executable attribute)
- [ ] `Pathfinder.Command.CommandManager.RegisterCommand(string, Action<OS,string[]>)` — check delegate signature
- [ ] `BaseExecutable` constructor parameters — `(Rectangle location, OS os, string[] args)` matches
- [ ] `Hacknet.Gui.RenderedRectangle.doRectangle` — confirm method exists (may be `doRectangle` or `drawRectangle`)
- [ ] `Hacknet.Gui.TextItem.doLabel` — confirm method name and parameter types
- [ ] `OS.currentInstance` — confirm static property exists for LoadFromCurrentOS()

---

## Next Step: ILSpy Session

To resolve Q1-Q3, need ILSpy on `D:\SteamLibrary\steamapps\common\Hacknet\HacknetPathfinder.exe`.

Targets to inspect:
1. `Computer` class → trace-related fields
2. `Programs` class → `scp` method signature  
3. `Folder` class → file/subfolder collection names
4. `OS` class → `currentInstance` static

Download ILSpy: https://github.com/icsharpcode/ILSpy/releases
