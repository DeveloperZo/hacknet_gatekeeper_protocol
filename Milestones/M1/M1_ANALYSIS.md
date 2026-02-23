# M1 ANALYSIS — Gatekeeper Protocol
## Milestone: Custom Port System + Tiered Crackers

**Date:** 2026-02-22  
**Plugin file:** `Plugin/GatekeeperProtocol.cs` (505 lines, v0.2.0)  
**Extension state:** Default Pathfinder template — no GP-specific nodes exist yet  
**Build state:** Compiles cleanly, DLL in `BepInEx/plugins/`, plugin loads without error

---

## 1. Scope of M1

M1 is the custom port layer — the foundation everything else builds on. Nothing else
in the design (CORE ports, Gatekeeper counterattacks, hardware upgrades) works until
custom ports register, executables load, and crackers complete correctly.

**M1 is done when:**
- 6 custom ports register and appear on nodes in-game
- All 6 cracker executables launch and complete (open the port)
- V2 tier gates: T1 slow / T2 normal / T3 fast
- V3 tier gates: T1 refused / key file checked / T2-T3 timed
- `gp_debug` accurately reflects port state on connected node
- A minimal 3-node test network exercises all paths

---

## 2. Plugin Audit — `GatekeeperProtocol.cs`

### 2.1 Port Registration (`Load()`)

```csharp
PortManager.RegisterPort("ssh_v2", "SSH V2", 10022);
// ... (6 total)
```

**Status: Correct.** Pathfinder `PortManager.RegisterPort(protocol, display, portNum)` is the
right call. Ports need to be registered before any node XML is loaded — `Load()` fires first,
so this is fine.

**Risk — port number collision check needed:**
The ranges 10022/10021/10080 and 20022/20021/20080 need to be confirmed not already claimed
by another loaded plugin (e.g. ZeroDayToolKit). Run `gp_debug` on a connected node and look
for the ports in the output to confirm they registered.

---

### 2.2 Executable Registration

```csharp
[Pathfinder.Meta.Load.Executable("#GP_SSH_V2#")]
public class GPSSHCrackV2 : GPCrackBaseV2 { ... }
```

**Status: Likely correct, needs in-game verification.**  
Pathfinder scans the loaded assembly for all types decorated with `[Executable]` and
auto-registers them. No explicit `ExecutableManager.RegisterExecutable()` call in `Load()`
is needed. However, the string token `"#GP_SSH_V2#"` is how Extension XML files
reference the exe (e.g. `FileContents="#GP_SSH_V2#"`). This must match exactly —
case-sensitive — across the node XML and the attribute.

**Action required:** Add `AddAsset` lines in `StartingActions.xml` for all 6 executables
using their exact token strings.

---

### 2.3 `GPCrackBaseV2.Update()` — Critical API Risks

#### Risk A: `Computer.openPort()` signature

```csharp
target.openPort(portName, os.thisComputer.ip);  // Line ~285
```

**⚠ HIGH RISK.** In vanilla Hacknet, `Computer.openPort()` takes `(int portNum, string ip)`,
not a string protocol name. The `portName` string `"ssh_v2"` will not work — it will
either throw or silently no-op.

**Fix required:** Store the port number alongside the name in the constructor and pass
the int overload instead.

```csharp
// In base class, add:
protected int portNumber;

// In GPCrackBaseV2 constructor:
protected GPCrackBaseV2(Rectangle location, OS os, string[] args, string port, int portNum)
    : base(location, os, args)
{
    portName   = port;
    portNumber = portNum;
    // ...
}

// In Update(), success path:
target.openPort(portNumber, os.thisComputer.ip);
```

Concrete classes pass the number explicitly:
```csharp
public GPSSHCrackV2(...) : base(location, os, args, "ssh_v2", 10022) { }
```

**Same fix applies identically to `GPCrackBaseV3`.**

---

#### Risk B: `Computer.isPortOpen()` signature

```csharp
if (target.isPortOpen(portName))  // Line ~239
```

**Medium risk.** Pathfinder likely adds an extension method `isPortOpen(string protocol)`
that wraps the underlying `GetPort(portNum).open` check. If Pathfinder provides this,
the current code is fine. If it only exposes `isPortOpen(int portNum)`, same fix as Risk A
applies (pass `portNumber` instead of `portName`).

**Test first:** Build and observe whether this throws a compile error. If it compiles,
Pathfinder has the extension method and this is safe.

---

#### Risk C: `target.hostileActionTaken()`

```csharp
if (!os.traceTracker.active)
    target.hostileActionTaken();
```

**Medium risk.** This method may not exist by this name. Hacknet uses `hostileActionTaken()`
internally, but it may be `private` or named differently in the actual binary. The try/catch
wrapper is correct defensive coding — the fallback is safe (trace just doesn't start).

**Verify:** Run the v2 cracker on a test node with `gp_debug` open. If trace doesn't
start on first use, this call is silently swallowed. The workaround is to accept it and note
that on proxy-less nodes, trace won't auto-start — player can add a proxy to any test node
to force trace via the normal proxy-overload path.

---

#### Risk D: `TraceTracker` field names

```csharp
float remaining = tt.startingTimer - tt.timer;
```

**Medium risk.** These field names (`startingTimer`, `timer`) are guesses. The actual
Hacknet TraceTracker fields may differ. The `try/catch` fallback handles this gracefully
for `gp_debug`, but if this same pattern is reused in exe logic (e.g. checking trace
remaining before attempting a crack), it would silently fail there too.

**Research path:** In Hacknet source or dnSpy, look for `TraceTracker` and read its public
fields. The relevant ones are: the max duration value and the current elapsed value.

Known candidates (to verify with dnSpy):
```
TraceTracker.active        — bool
TraceTracker.startingTimer — float (max trace time)
TraceTracker.timer         — float (current elapsed? or remaining?)
```

The subtraction `startingTimer - timer` assumes `timer` counts up. If `timer` counts down,
the formula gives seconds elapsed rather than remaining, which inverts the display. Verify
which direction `timer` runs.

---

#### Risk E: `isExiting` in GPCrackBaseV3 failure paths

```csharp
// T1 gate failure / key missing:
failed = true;
isExiting = true;   // ← is this a real ExeModule field?
return;
```

**Low-medium risk.** `needsRemoval = true` is the standard ExeModule field for "remove this
exe from the display." `isExiting` may not be a field on `BaseExecutable` / `ExeModule` at
all, which would be a compile error, or it may exist with different semantics.

**Fix if needed:** Replace `isExiting = true` with `needsRemoval = true` in both failure
paths. The cracker will be removed from the display cleanly. The error message written via
`os.write()` before setting the flag is sufficient feedback.

---

### 2.4 `gp_debug` Command

```csharp
foreach (var port in os.connectedComp.GetAllPortStates())
{
    if (port.Record.Protocol.EndsWith("_v2") || ...)
```

**Status: Correct Pathfinder API pattern.** `GetAllPortStates()` returns `IEnumerable<PortState>`,
each has `.Record.Protocol` (string) and `.Cracked` (bool). This is the right way to enumerate
custom ports.

**Minor:** The command calls `os.Flags.HasFlag("gp_cpu_t4")` etc., but `HardwareState.CpuMultiplier(os)`
already encapsulates this logic. Consider calling `HardwareState.CpuMultiplier(os)` directly
for consistency.

---

### 2.5 `gp_setscripts` Command

**Status: Correct for testing purposes.** The Hacknet flag system is additive-only, which is
noted in the comment. The current implementation correctly layers flags upward (setting T3
also sets T2). For production, this command should be removed or gated behind a debug flag —
it allows permanent tier skipping that bypasses the upgrade shop.

---

### 2.6 `HardwareState` Class

```csharp
public static float CpuMultiplier(OS os)
{
    if (os.Flags.HasFlag("gp_cpu_t4")) return 3.0f;
    // ...
}
```

**Status: Correct design.** The flag reads `gp_cpu_t2`/`t3`/`t4` which matches the flag names
in `HARDWARE_SYSTEM.md`. Two notes:

1. The hardware doc defines **5 CPU tiers** (up to 4.0x) but the plugin only implements **4**
   (up to 3.0x). Decide whether T5 is in-scope for M1 or deferred to the hardware upgrade M.

2. `gp_cpu_t4` returns 3.0x in the plugin but the doc says T4 = 3.0x AND T5 = 4.0x. With
   only 4 tiers (t1-t4) in the flag names, T5 is unreachable. If you intend 5 tiers, add
   `gp_cpu_t5` returning 4.0x before the t4 check.

---

## 3. Extension XML Audit

### 3.1 What Exists

| File | Status |
|------|--------|
| `ExtensionInfo.xml` | Template — placeholder name/description/factions |
| `Nodes/PlayerComp.xml` | Generic — no GP tools on it |
| `Actions/StartingActions.xml` | Gives `#RTSP_EXE#` + `#GP_EXE#` (UNDEFINED TOKEN) |
| `Nodes/ExampleComputer.xml` + others | Template nodes — unrelated to GP |
| `Extension/Nodes/` | No GP-specific test nodes |

**`#GP_EXE#` is not a valid Pathfinder token.** This will silently create an empty/broken
file in the player's bin. Replace with the actual 6 GP exe tokens.

---

### 3.2 What M1 Needs

#### A. `StartingActions.xml` — Fix and extend

Replace the broken `#GP_EXE#` line with all 6 GP crackers:

```xml
<Instantly>
  <!-- V2 tier crackers -->
  <AddAsset FileName="GPV2SSH.exe"  FileContents="#GP_SSH_V2#" TargetComp="playerComp" TargetFolderpath="bin" />
  <AddAsset FileName="GPV2FTP.exe"  FileContents="#GP_FTP_V2#" TargetComp="playerComp" TargetFolderpath="bin" />
  <AddAsset FileName="GPV2Web.exe"  FileContents="#GP_WEB_V2#" TargetComp="playerComp" TargetFolderpath="bin" />
  <!-- V3 tier crackers — not usable without key files + T2 scripts -->
  <AddAsset FileName="GPV3SSH.exe"    FileContents="#GP_SSH_V3#"   TargetComp="playerComp" TargetFolderpath="bin" />
  <AddAsset FileName="GPV3FTP.exe"    FileContents="#GP_FTP_V3#"   TargetComp="playerComp" TargetFolderpath="bin" />
  <AddAsset FileName="GPV3Web.exe"    FileContents="#GP_WEB_V3#"   TargetComp="playerComp" TargetFolderpath="bin" />
</Instantly>
```

---

#### B. `Nodes/GP/gp_test_v2.xml` — V2 port test node

Minimal node with all 3 v2 ports. Trace of 30s makes T1 miserable (~29s crack
window) and T2/T3 comfortable. Progress admin resets on disconnect.

```xml
<?xml version="1.0" encoding="UTF-8" ?>
<Computer id="gp_test_v2"
          name="GP Test Alpha — V2"
          ip="10.0.0.1"
          security="3"
          allowsDefaultBootModule="false"
          type="1">

  <adminPass pass="admin" />

  <!-- Standard ports (cracked with vanilla tools first) -->
  <ports>22, 21, 80</ports>

  <!-- V2 custom ports -->
  <PFPorts>ssh_v2:10022, ftp_v2:10021, web_v2:10080</PFPorts>

  <!-- 5 ports required: 3 standard + 3 v2 — set to 3 for initial test
       (allows breach without all v2 open; tighten for production) -->
  <portsForCrack val="3" />

  <!-- Short trace: 30s makes T1 barely winnable, T2/T3 comfortable -->
  <trace time="30" />

  <admin type="progress" resetPassword="false" isSuper="false" />

  <file path="home" name="README.txt">GP Test Node — V2 Tier
Crack ssh_v2 / ftp_v2 / web_v2 using GP crack tools.
T1 scripts: ~29s window. T2: 10s. T3: 4s.
</file>

</Computer>
```

---

#### C. `Nodes/GP/gp_relay_alpha.xml` — V3 key relay

Easy entry point (standard ports, no trace) that holds the 3 v3 key files.
Player must scp them to `/home` before v3 crackers will activate.

```xml
<?xml version="1.0" encoding="UTF-8" ?>
<Computer id="gp_relay_alpha"
          name="GP Relay Alpha"
          ip="10.0.0.2"
          security="1"
          allowsDefaultBootModule="false"
          type="3">

  <adminPass pass="relay" />

  <ports>22, 21</ports>
  <portsForCrack val="2" />
  <trace time="120" />

  <admin type="progress" resetPassword="false" isSuper="false" />

  <!-- V3 key files — player scps these to /home before running v3 crackers -->
  <file path="home" name="ssh_key_v3.dat">V3-KEY:SSH:ALPHA-0x4F2A</file>
  <file path="home" name="ftp_key_v3.dat">V3-KEY:FTP:ALPHA-0x7B1C</file>
  <file path="home" name="web_key_v3.dat">V3-KEY:WEB:ALPHA-0x9E33</file>

  <file path="home" name="README.txt">V3 authentication keys.
scp these to your /home before running v3 crack tools.
</file>

</Computer>
```

---

#### D. `Nodes/GP/gp_test_v3.xml` — V3 port test node

Longer trace (60s) so T2+CPU T2 can succeed marginally. T3 is comfortable.
Admin resets on disconnect.

```xml
<?xml version="1.0" encoding="UTF-8" ?>
<Computer id="gp_test_v3"
          name="GP Test Beta — V3"
          ip="10.0.0.3"
          security="3"
          allowsDefaultBootModule="false"
          type="1">

  <adminPass pass="admin" />

  <ports>22, 21, 80</ports>
  <PFPorts>ssh_v3:20022, ftp_v3:20021, web_v3:20080</PFPorts>
  <portsForCrack val="3" />

  <!-- 60s: T2 solve = 61s / CpuMult — CPU T2 (1.5x) → 40.7s → just survives -->
  <trace time="60" />

  <admin type="progress" resetPassword="false" isSuper="false" />

  <file path="home" name="README.txt">GP Test Node — V3 Tier
T1 scripts: REFUSED at launch.
T2 scripts + CPU T2: marginal (40s on 60s trace).
T3 scripts: 10s crack, comfortable.
Requires key_v3_*.dat in your /home.
</file>

</Computer>
```

---

#### E. `ExtensionInfo.xml` — Update for GP

Key changes:
- Add all 3 GP nodes to `StartingVisibleNodes`
- Update name/description (strip template text)
- Remove unused template factions

```xml
<Name>Gatekeeper Protocol</Name>
<StartingVisibleNodes>gp_test_v2, gp_relay_alpha, gp_test_v3</StartingVisibleNodes>
<Description>GATEKEEPER PROTOCOL
A roguelike hacking extension.
Crack v2 and v3 ports to breach Gatekeeper-protected nodes.
</Description>
```

---

## 4. Ordered Implementation Tasks

These are in dependency order — each step is testable before the next.

| # | Task | File(s) | Risk |
|---|------|---------|------|
| 1 | Fix `openPort()` — pass `portNumber` (int) not `portName` string | `GatekeeperProtocol.cs` | HIGH |
| 2 | Fix `isExiting` → `needsRemoval` in v3 failure paths | `GatekeeperProtocol.cs` | MEDIUM |
| 3 | Fix `StartingActions.xml` — replace `#GP_EXE#` with 6 real tokens | `Actions/StartingActions.xml` | HIGH |
| 4 | Create `Nodes/GP/gp_test_v2.xml` | new file | — |
| 5 | Create `Nodes/GP/gp_relay_alpha.xml` with 3 key files | new file | — |
| 6 | Create `Nodes/GP/gp_test_v3.xml` | new file | — |
| 7 | Update `ExtensionInfo.xml` — name, visible nodes, strip template factions | `ExtensionInfo.xml` | — |
| 8 | Build + launch, run `gp_debug` on each test node | — | — |
| 9 | Test v2 crackers T1→T3 via `gp_setscripts` | in-game | — |
| 10 | scp v3 keys from relay, test v3 crackers T2/T3 | in-game | — |
| 11 | Verify T1 refusal message for v3 crackers | in-game | — |
| 12 | Verify `openPort()` actually opens the port (not silently no-op) | in-game | HIGH |

---

## 5. M1 Definition of Done

- [ ] `gp_debug` shows all 6 GP ports correctly as OPEN/CLOSED on connected nodes
- [ ] V2 crackers:
  - [ ] T1: crack time ≈ traceTime - 1s (barely survives on 30s node)
  - [ ] T2: crack time ≈ 10s
  - [ ] T3: crack time ≈ 4s
  - [ ] Port shows OPEN in `gp_debug` after completion
- [ ] V3 crackers:
  - [ ] T1: refused immediately with error message
  - [ ] Missing key file: refused with `key_v3_<port>.dat required` message
  - [ ] T2 + CPU T1 on 60s node: fails (61s solve > 60s trace)
  - [ ] T2 + CPU T2 on 60s node: succeeds (~40s solve)
  - [ ] T3: 10s crack regardless of trace time
  - [ ] Port shows OPEN in `gp_debug` after completion
- [ ] Progress bars render correctly (orange for v2, blue for v3)
- [ ] No BepInEx errors in `LogOutput.log` related to GP

---

## 6. Open API Questions (Resolve Before Step 8)

These are unknowns that will cause runtime failures if wrong. Check with dnSpy or by
running a test build and observing errors.

| Question | Impact | How to check |
|----------|--------|--------------|
| Does `Computer.openPort(string, string)` exist as a Pathfinder extension method? | HIGH — crackers silently no-op if wrong | Compile; if it compiles, Pathfinder has the overload |
| Does `Computer.isPortOpen(string)` accept protocol names? | MEDIUM — port-already-open check broken | Same — compile test |
| What are the actual `TraceTracker` field names? | LOW (gp_debug fallback is safe) | dnSpy → search TraceTracker |
| Does `hostileActionTaken()` exist on `Computer`? | LOW (try/catch swallows it) | dnSpy or compile |
| Do `[Executable]` attributes auto-register on assembly scan? | HIGH — crackers won't launch at all | In-game: run `GPV2SSH.exe`; if "file not executable" → need explicit registration |

---

## 7. What M1 Does NOT Include

These are confirmed out of scope for M1 based on the roadmap:

- CORE port executables (V3/VAULT/SOVEREIGN) — M2
- Hardware upgrade shop / Harmony patches — M3
- Gatekeeper counterattack scripts — M2
- SENTINEL AI companion — M3
- Run seeding / roguelike loop — M4
- Economy / credits system — M3
