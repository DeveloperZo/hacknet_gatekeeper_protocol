# HARDWARE SYSTEM — Gatekeeper Protocol

## Design Principle

Hardware stats are the persistent RPG layer across roguelike runs. They are stored as Hacknet flags (which survive between sessions automatically) and enforced/applied by the GatekeeperPlugin BepInEx plugin.

The upgrade shop is a custom daemon node (`hw_vendor`) always visible on the netmap. Player connects, runs `upgrade <stat>`, mission action fires, flag updates, plugin reads flag and applies multiplier.

---

## Stat 1: RAM

**What it does:** Total memory pool for running executables simultaneously.  
**Base value:** 761 MB (Hacknet default)  
**Upgrade increments:** 761 → 1024 → 1536 → 2048 → 3072 MB

**Implementation:**
- ZeroDayToolKit `<SetRAM ram="1024">` mission action
- No plugin work required
- Fired by upgrade shop mission when player purchases RAM tier

**Gameplay impact:**
- Higher RAM = run TraceKill (600 MB) + other tools simultaneously
- Determines which CORE port executables player can afford to run
- Low RAM is the primary constraint at run start — forces prioritization

**Flags:**
```
ram_tier_1 = 1024 MB
ram_tier_2 = 1536 MB
ram_tier_3 = 2048 MB
ram_tier_4 = 3072 MB
```

---

## Stat 2: CPU

**What it does:** Multiplier on executable solve speed — how fast ports crack, firewall analyzes, proxy overloads.  
**Base multiplier:** 1.0x  
**Upgrade increments:** 1.0x → 1.5x → 2.25x → 3.0x → 4.0x

**Implementation:**
- Harmony postfix patch on `ExeModule.Update(GameTime)`
- Patch multiplies `deltaTime` (or equivalent progress increment) by `GatekeeperPlugin.CpuMultiplier`
- Static field `CpuMultiplier` set on plugin load by reading flag

**Patch target:**
```csharp
// Pseudo-code — actual method name TBD by reflection inspection
[HarmonyPostfix]
[HarmonyPatch(typeof(ExeModule), "Update")]
static void CpuSpeedPatch(ExeModule __instance, GameTime gameTime) {
    // Multiply progress delta by CpuMultiplier
    // Apply to __instance.progress or equivalent field
}
```

**Gameplay impact:**
- CPU 1 (base): SSHcrack takes ~10 seconds, feels slow
- CPU 4 (max): same crack takes ~2.5 seconds, feels fluid and powerful
- Directly affects how long player is exposed on a node before breach
- More CPU = tighter trace windows = more aggressive play style viable

**Flags:**
```
cpu_tier=1  (1.0x)
cpu_tier=2  (1.5x)
cpu_tier=3  (2.25x)
cpu_tier=4  (3.0x)
cpu_tier=5  (4.0x)
```

---

## Stat 3: Network

**What it does:** Two effects — trace fill rate reduction AND scp transfer speed.  
**Base values:** Trace at 1.0x fill rate, scp is instant  
**Upgrade increments:** Trace 1.0x → 0.8x → 0.6x → 0.4x → 0.25x | scp delay 2.0s → 1.0s → 0.5s → instant

**Implementation — Trace Resistance:**
- Harmony postfix on trace update method
- Multiplies trace increment by `GatekeeperPlugin.NetworkTraceMultiplier` (< 1.0 = slower fill)
- Higher Network tier = longer time on node before trace completes

**Implementation — scp Speed:**
- Harmony postfix on scp execution
- Introduces `Thread.Sleep()` or artificial coroutine delay based on `GatekeeperPlugin.NetworkDelay`
- Files over a size threshold (defined per tier) experience delay

**Gameplay impact:**
- Network 1 (base): trace fills fast, scp is instant but you're exposed
- Network 3 (mid): trace fills 40% slower — opens up more aggressive port cracking sequences
- Network 5 (max): exfil-heavy playstyles become viable, trace barely a concern
- Creates a meaningful build split: CPU players race to crack, Network players play slow and methodical

**Flags:**
```
net_tier=1  (1.0x trace, 2.0s scp delay)
net_tier=2  (0.8x trace, 1.0s scp delay)
net_tier=3  (0.6x trace, 0.5s scp delay)
net_tier=4  (0.4x trace, instant scp)
net_tier=5  (0.25x trace, instant scp)
```

---

## Stat 4: SSD

**What it does:** Total storage cap on playerComp. Limits how many tools and files can be stored.  
**Base value:** 50,000 bytes (approximately 5 standard exe files)  
**Upgrade increments:** 50K → 100K → 200K → 400K → 1,000K bytes

**Implementation:**
- Harmony postfix on `scp` command execution and any `makeFile` targeting playerComp
- After write succeeds, calculate total bytes in playerComp file tree
- If total > `GatekeeperPlugin.SsdLimit`, delete the just-written file and print error

**Storage calculation:**
```csharp
int totalBytes = 0;
foreach (FileEntry f in os.thisComputer.files.root.getAllFiles())
    totalBytes += f.data.Length;
if (totalBytes > GatekeeperPlugin.SsdLimit) {
    // Delete file, write error to terminal
    os.write("STORAGE LIMIT EXCEEDED — upgrade SSD or delete files");
}
```

**Gameplay impact:**
- SSD 1 (base): carry 5 tools max — forces tight loadout decisions
- SSD 3 (mid): carry ~20 tools — comfortable for most playstyles
- SSD 5 (max): effectively unlimited for normal play
- Gatekeeper hacker scripts that `makeFile`-spam playerComp become a real threat at low SSD
- Creates a new "storage denial" attack vector for Gatekeepers

**Attack scenario:**  
Gatekeeper counterattack script drops 10 garbage files onto playerComp `/home`. Player at 80% capacity hits limit. Cannot download the CORE cracker needed for the next node. Must disconnect, delete garbage, re-engage.

**Flags:**
```
ssd_tier=1  (50,000 bytes)
ssd_tier=2  (100,000 bytes)
ssd_tier=3  (200,000 bytes)
ssd_tier=4  (400,000 bytes)
ssd_tier=5  (1,000,000 bytes)
```

---

## Plugin Architecture

All four stats live in a single plugin file.

```csharp
// GatekeeperHardware.cs
public static class HardwareState {
    public static float CpuMultiplier = 1.0f;
    public static float NetworkTraceMultiplier = 1.0f;
    public static float NetworkScpDelay = 2.0f;
    public static int SsdLimit = 50000;

    public static void LoadFromFlags(OS os) {
        // Read flag strings, set above statics
        // Called on extension load and after each upgrade
    }
}
```

---

## Upgrade Shop Node (hw_vendor)

- Always visible on netmap, no hacking required
- Custom daemon displays current tier of each stat
- Player runs `upgrade ram|cpu|net|ssd` in terminal
- Command checks flag for current tier, checks credit flag, fires action chain:
  1. Deduct credits (set credits flag)
  2. Set new tier flag
  3. Fire `<SetRAM>` if RAM (or call `HardwareState.LoadFromFlags()` for others)
  4. Print confirmation to terminal

---

## Credits System

- Credits earned as a flag integer after each run based on performance score
- Performance factors: nodes breached, time per breach, trace events avoided, tools used
- Credits persist between runs — they are the primary meta-progression currency
- No in-run spending — credits only spendable at hw_vendor between runs

---

## Open Questions

- [ ] What is the actual Hacknet method name for exe progress increment? (needs reflection or decompile)
- [ ] What is the actual Hacknet method name for trace increment? (needs reflection)
- [ ] Is `scp` a terminal command handler or an ExeModule? (determines patch target)
- [ ] ZeroDayToolKit SetRAM — does it persist to save, or only for current session? (needs test)
