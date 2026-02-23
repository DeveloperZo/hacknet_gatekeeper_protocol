# PRE-MORTEM ANALYSIS — M1, M2, M4
# Chain of verification: Design Intent → Technical Mechanism → Known API → Unverified Assumption → Failure Mode → Mitigation

---

## Method

For each assumption in the design:
1. State what we're relying on
2. Trace it to a specific technical mechanism
3. Mark it as VERIFIED (seen in game files/docs), PROBABLE (logical given what we know), or UNKNOWN (untested)
4. Identify the failure mode if the assumption is wrong
5. Propose a mitigation that doesn't require the assumption

Severity ratings:
- 🔴 CRITICAL — breaks the milestone entirely if wrong
- 🟡 MODERATE — degrades the experience significantly, needs workaround
- 🟢 LOW — acceptable fallback exists, minor impact

---

---

# MILESTONE 1 — Something Worth Hacking

## Weakest weaknesses, honest ranking

### 🔴 CRITICAL — The V3 passphrase can't store a variable string in a flag

**Assumption:** The passphrase mechanic works by seeding a unique passphrase string
to a relay node at run start, and GPV3CrackExe checks whether the player has
the correct passphrase.

**Verification chain:**
- Flags are boolean. `AddFlag("passphrase_correct")` — fine. `AddFlag("passphrase_val=X4K9")` — not how flags work. ❌
- File content on a node CAN contain arbitrary strings. ✅ (confirmed from IntroExtension)
- But can the executable read a file from the target node to compare against? Unknown. 🔵
- Can the executable read a file from the player's /home? Unknown. 🔵
- The executable has `os` and `targetIP` — `Programs.getComputer(os, targetIP)` returns the target Computer. Computer has a `files` root Folder. File content is `file.data` (string). This chain is PROBABLE but untested.

**Failure mode:** GPV3CrackExe can't verify a passphrase because it has no way
to know what the "correct" answer is — it can't store a dynamic string, and we
haven't verified it can read file content from either computer.

**Mitigation (removes the assumption entirely):**
Change V3 from a passphrase mechanic to a **key file mechanic**:
- Relay node contains a file: `key_v3.dat`
- Player must `scp key_v3.dat` to their /home before running the V3 crack
- GPV3CrackExe checks: does `playerComp.files.root.searchForFolder("home").searchForFile("key_v3.dat")` exist?
- If yes, proceeds. If no, prints "V3 HANDSHAKE FAILED — key file required."
- File presence = boolean. No string comparison needed.
- Which relay holds the key varies by a seed flag set at run start.
- This is fully verifiable and the "go find the key first" gameplay is equivalent fun.

**Verdict:** V3 mechanic as written won't work. The key-file redesign does work
and is arguably cleaner — it makes scp mechanically necessary for progress,
not just for profit.

---

### 🟡 MODERATE — SCP gating requires file manipulation in OnConnect actions, which is unverified

**Assumption:** `<OnConnect>` conditional action can remove a file from a node
and plant a stub in its place, conditional on `<DoesNotHaveFlags ssd_tier_2>`.

**Verification chain:**
- `<OnConnect>` trigger: confirmed present in IntroExtension examples. ✅
- `<HasFlags>` / `<DoesNotHaveFlags>` within actions: confirmed. ✅
- Action sub-command to delete a file from a node: NOT confirmed. 🔵
  - Pathfinder action docs show: `<AddFile>`, `<DeleteFile>`, `<AppendToFile>` — we haven't verified these exist in the conditional action XML system (vs. hacker scripts where they're definitely available).
- If `<DeleteFile>` doesn't exist in action XML, we can't remove the real file and plant the stub dynamically.

**Failure mode:** The node just always shows both files — the real one and the stub —
regardless of player SSD tier. Or neither file manipulation works and the node
always shows the real file, making SSD gating impossible via this mechanism.

**Mitigation:**
Two options depending on what we find:
1. If `<DeleteFile>` exists in action XML: proceed as designed.
2. If not: author two separate node XML files per gated node — one with the real file
   (for SSD-capable players, shown via `<ShowNode>` when flag is present), one
   with only the stub (for underpowered players). Doubles node count for gated nodes
   but is guaranteed to work. 5 gated nodes = 10 XML files total. Manageable.

**Verdict:** Test `<DeleteFile>` in action XML immediately before writing any SCP-gated nodes.
If it doesn't exist, node-variant approach is the fallback.

---

### 🟡 MODERATE — V3 key file seeding happens at run start but only once

**Assumption:** An action file fires at extension/session start and places `key_v3.dat`
on one of the two relay nodes based on a seed flag.

**Verification chain:**
- `<Instantly>` trigger fires on extension load. ✅ (confirmed in ExampleConditionalActionSet.xml)
- `<HasFlags seed_a>` → place key on relay_a, `<HasFlags seed_b>` → place key on relay_b. PROBABLE. 🔵
- Who sets `seed_a` vs `seed_b`? Plugin sets it randomly at session start via OSUpdateEvent. PROBABLE. 🔵
- If plugin sets the flag before the `<Instantly>` action fires, the action reads the flag correctly.
  ORDER OF OPERATIONS IS UNKNOWN. 🔵

**Failure mode:** Plugin sets the seed flag AFTER the Instantly action has already fired.
Both relays get the key, or neither does. Mechanic breaks.

**Mitigation:**
Don't rely on race condition ordering. Instead:
- Plugin sets seed flag on OSUpdateEvent (first tick after load)
- Action file uses a different trigger: a separate mission that fires on first player
  login (first terminal command typed) checks the flag and seeds the key
- Or: always put the key on relay_a. The relay_a/relay_b variety is nice-to-have,
  not load-bearing. Ship it deterministic, make it variable later.

**Verdict:** Start deterministic (key always on relay_a). Add randomization in M4
when we have the seeding infrastructure figured out.

---

### 🟢 LOW — V2 port custom executables may conflict with vanilla cracker names

**Assumption:** Registering `V2_SSH` as a PFPort and giving the player
`GP_SSH_V2.exe` doesn't interfere with vanilla `SSHCrack.exe` behavior.

**Verification chain:**
- Pathfinder PFPort registration: documented, works. ✅
- Custom executable for a PFPort: documented via `openPort("ssh_v2", ...)`. ✅
- Vanilla SSHCrack.exe opens the vanilla SSH port (port 22). Our v2 port is a
  different named port — they coexist. ✅

**Failure mode:** Minimal. The only issue would be if we accidentally name our
v2 port "ssh" (conflict with vanilla). As long as we name it "ssh_v2",
there's no overlap.

**Verdict:** Low risk. Just be careful with port name strings.

---

---

# MILESTONE 2 — Hacking Pays

## Weakest weaknesses, honest ranking

### 🔴 CRITICAL — The denomination flag system cannot represent arbitrary integer balances

**Assumption:** Credits stored as denomination flags (`credits_1`, `credits_5`,
`credits_50`, `credits_100`, `credits_500`) can represent any balance the player
accumulates.

**Verification chain:**
- Flags are boolean. One flag = one denomination present. ✅
- Earning 50cr sets `credits_50`. Earning another 50cr... tries to set `credits_50`
  again. It's already set. Flag system has no increment. ❌
- Maximum representable balance: 1 + 5 + 10 + 50 + 100 + 500 = 666 credits.
  CPU Tier 5 costs 2500cr. CANNOT BE REPRESENTED. ❌

**This is a fundamental design flaw. The denomination system is broken.**

**Mitigation — File-based wallet (STRONGLY RECOMMENDED):**
Store balance as an integer in a text file on the player's computer:
- Path: playerComp `/home/wallet.dat`
- Content: `"1247"` (just the number)
- Plugin reads: `int balance = int.Parse(walletFile.data.Trim())`
- Plugin writes: `walletFile.data = newBalance.ToString()`
- `gp_credits` prints the file content as a number
- No flags involved. No arithmetic constraints. No maximum balance.

This is clean, simple, and the player can see their balance by `cat home/wallet.dat`
which is actually a nice bit of immersion — their credits are a literal file.

Risk: player can manually edit `wallet.dat`. Acceptable — single player game.
Risk: `wallet.dat` gets deleted if player wipes /home. Need to handle gracefully
(if file missing, create it with balance 0).

**Verdict:** Abandon denomination flags entirely. File-based wallet is the correct
implementation. Every piece of currency logic gets simpler.

---

### 🔴 CRITICAL — No verified mechanism to trigger ZeroDayToolKit SetRAM from C# plugin code

**Assumption:** `gp_buy ram_2` can invoke the ZeroDayToolKit `<SetRAM>` action
to actually update the player's RAM display.

**Verification chain:**
- ZDTK `<SetRAM>` exists as an action: PROBABLE based on ZDTK README mentioning it. 🔵
- Calling a Pathfinder action from C# plugin code: `PathfinderAPI.Action.ActionManager`
  or similar. Exact API: UNKNOWN. 🔵
- Even if the API exists, it may require an XML context to execute, not callable raw. UNKNOWN. 🔵

**Failure mode:** `gp_buy ram_2` sets the `ram_tier_2` flag but doesn't actually
change the RAM display. Player sees no visual difference. The upgrade feels fake.

**Mitigation (avoids ZDTK entirely):**
From OpenHacknet OS.cs: `os.ram` is a `RamModule` object. `RamModule` has a `totalRam`
field (confirmed from decompile search results). Direct assignment:
```csharp
os.ram.totalRam = 1024; // sets RAM to 1024MB
```
This modifies the live object. RAM panel updates immediately on next draw tick.
No ZDTK needed. No action API needed. Tested approach: set it in `gp_buy` handler directly.

**Verdict:** Don't use ZDTK for RAM. Set `os.ram.totalRam` directly from C#.
Test this first — it's the only hardware upgrade we can't 100% verify from docs alone.
If `RamModule.totalRam` is the wrong field name, check `maxRam` or `moduleCap`.

---

### 🟡 MODERATE — Mission-based credit awards are one-shot and non-repeatable

**Assumption:** Completing a breach (final port cracked) fires a mission completion
that awards credits via `<addFlags>`.

**Verification chain:**
- Mission `<OnSuccess>` addFlags: confirmed pattern in Hacknet missions. ✅
- Mission fires once: by design, missions don't re-trigger after completion. ✅
- PROBLEM: Once all OUTSKIRTS nodes are breached, no more OUTSKIRTS income. ✅

**Failure mode:** Player completes all 6 OUTSKIRTS nodes, earns ~1050cr total from
breaches. CPU Tier 3 costs 600cr, Net Tier 2 costs 300cr — that's 900cr just to
unlock MERIDIAN, leaving 150cr from 6 nodes if they scp nothing. Player is
soft-gated: not enough credits to unlock MERIDIAN without the fence income.
They've exhausted all OUTSKIRTS breach rewards and can't progress.

**Math check:**
- relay_a + relay_b: 100cr
- corp_a + corp_b: 400cr
- secure_a: 500cr
- Total breach-only OUTSKIRTS income: 1000cr
- CPU T3 (600) + Net T2 (300) = 900cr minimum to unlock MERIDIAN
- Remaining: 100cr
- RAM T3 (500) + SSD T3 (400) also needed for IRONVEIL — player needs ~2200cr more
- Without fence income: LOCKED OUT

The fence/scp loop is not a nice-to-have. It's required for progression to work.
If `gp_sell` has bugs or the fence mechanic is confusing, players hit a wall.

**Mitigation:**
1. Tune credit values upward — OUTSKIRTS breach total should comfortably cover
   at least CPU T2 + Net T2 with some left over. Maybe 300cr per corp node, 700cr
   per secure node. Total: ~1600cr from breaches alone.
2. Add a repeatable low-value mission: fence buys common relay data files for 25cr each.
   Files respawn on relay nodes each session. This gives a grind floor.
3. Document the economy math before writing any XML. Don't tune blind.

**Verdict:** Economy math must be done on paper before any XML is written.
This is the most likely source of "this feels wrong" during playtesting.

---

### 🟡 MODERATE — Net tier trace-speed variant nodes doubles XML authoring burden

**Assumption:** Net tier translates to slower trace via ShowNode/HideNode swapping
between fast-trace and slow-trace node variants.

**Verification chain:**
- ShowNode / HideNode: confirmed in Hacknet action format. ✅
- Two separate node XML files per node, one per trace speed: achievable. ✅
- COST: 18 base nodes × 2 = 36 XML files. Plus the swap actions. High maintenance.

**Failure mode:** Not a breakage — just a maintenance tax. Every future node change
must be made in two files. Easy to have them drift out of sync. Bugs are twice as
likely to be introduced.

**Mitigation (recommended — simplify the design):**
Trace speed is a NETWORK property, not a player stat property.
- OUTSKIRTS: traceTime = 120s (all nodes, all players)
- MERIDIAN: traceTime = 60s (all nodes, all players)
- IRONVEIL: traceTime = 30s (all nodes, all players)

Net tier then becomes a stat that:
- Reduces trace speed via a Harmony patch (parked for now), OR
- Is used as the entry gate for harder networks (which naturally have faster traces)
  — you NEED Net T2 to survive MERIDIAN's 60s trace, not because we slow it, but
  because you'd have learned trace management in OUTSKIRTS
- Net tier still has a clear value: it's on the unlock gate. Players understand
  "I need this to get in."

Drop the variant-node approach. One XML file per node. Trace speed is fixed per network.

---

### 🟢 LOW — `gp_sell` file duplication exploit

**Assumption:** Player sells a file to the fence once per acquisition.

**Failure mode:** Player repeatedly scps the same file from an unbreached node
(they still have access after first scp) and sells it multiple times.

**Mitigation:** Track sold files with a flag: `sold_node_corp_a` set after first
sale of `corp_package.dat` sourced from corp_a. Or, simpler: don't care.
Single-player game. Economy tune so farming isn't attractive vs. just hacking new nodes.

---

---

# MILESTONE 4 — Networks Have Personality

## Weakest weaknesses, honest ranking

### 🔴 CRITICAL — Shared Alert mechanic is not implementable with Pathfinder XML actions

**Assumption:** When the player triggers a trace on any MERIDIAN node, a flag is set
and all other currently-visible MERIDIAN nodes immediately respond with faster trace.

**Verification chain:**
- Setting a flag from an `<OnHostileActionTaken>` (or trace trigger) action: UNKNOWN
  whether this specific trigger exists in Pathfinder action XML. 🔵
- Even if the flag is set: conditional actions on OTHER nodes don't fire reactively
  when a flag changes. They only fire on their own connection events. ❌
- Nodes already loaded don't re-evaluate their traceTime when a flag changes mid-session. ❌
- ShowNode/HideNode on a node the player is currently connected to: undefined behavior. 🔵

**This mechanic as designed is not achievable without Harmony patches.**

**Mitigation — Simplify to what IS achievable:**
Shared alert becomes a cosmetic/narrative property only:
- When trace triggers on any MERIDIAN node, a hacker script fires that writes
  an IRC message: "MERIDIAN SECURITY ALERT — network flagged. Increase caution."
- MERIDIAN nodes have slightly shorter traceTime than OUTSKIRTS across the board
  (representing the always-elevated corporate security posture)
- The "shared" nature is implied by the lore, not mechanically enforced
- Players feel it as "MERIDIAN is scarier" rather than "alert actively spreads"

This is a real downgrade from the design. Accept it honestly.

If we want real shared-alert behavior, it requires a Harmony patch that monitors
the ZDTK `ComputerHostileActionTaken` (already patched by ZDTK!) and modifies
`traceTime` on other computers when it fires. This would be building on top of ZDTK,
not fighting it. Flag for post-M4 if the simplified version feels too weak.

**Verdict:** Ship simplified shared alert. Mark real alert as a post-M4 feature
requiring Harmony work coordinated with ZDTK.

---

### 🔴 CRITICAL — No verified mechanism to fire ShowNode actions from C# on hardware purchase

**Assumption:** When `gp_buy` pushes the player over the MERIDIAN unlock threshold,
C# code triggers the ShowNode action to reveal MERIDIAN nodes on the map.

**Verification chain:**
- Pathfinder has a C#-callable action execution API: UNKNOWN. 🔵
  The Pathfinder docs describe actions as XML-driven, not C#-callable directly.
- `<Instantly><HasFlags cpu_tier_3,net_tier_2>` conditional action could work
  if it runs continuously — but `<Instantly>` only fires at extension load. ❌
- Alternative: `<OnConnect>` to a hub node checks flags and shows network nodes.
  But this requires the player to connect to a specific node to trigger the reveal.
  That's a workaround, not a clean solution.

**Failure mode:** Player buys CPU T3 + Net T2. Map doesn't update. Player has no
way to reach MERIDIAN without restarting the extension or connecting to a trigger node.

**Mitigation — Hub node reveal (achievable, slightly clunky):**
Designate the OUTSKIRTS shop node as a "system node." When player connects to it
after any hardware purchase, `<OnConnect>` conditional actions fire:
- `<HasFlags cpu_tier_3,net_tier_2><DoesNotHaveFlags meridian_revealed>` → `<ShowNode>` all MERIDIAN nodes + `<AddFlag meridian_revealed>`
- Shop gives the player a reason to return after buying: "network access updated"
- Player learns "go to the shop to sync your access" — this is a minor UX ritual
  but entirely consistent with the game's fiction

Alternative: investigate Pathfinder's `ExtensionLoader` or `ActionManager` classes
in PathfinderAPI.dll. If a `LoadConditionalActions(string path, OS os)` method exists
and is callable from C#, use it. Research this before committing to the hub approach.

**Verdict:** Build the hub node reveal as the primary approach. Research the C# API
in parallel. If the API exists, upgrade to instant reveal. If not, the hub works fine.

---

### 🟡 MODERATE — IRONVEIL "active monitoring" is not a native Hacknet capability

**Assumption:** Nodes can detect player presence every 30 seconds and escalate trace.

**Verification chain:**
- Hacker scripts triggered periodically while player is connected: NO mechanism for this. ❌
- The closest native mechanism: `tracker` tag — fires trace auto-retaliation if
  player leaves logs after disconnecting. Not periodic, not while-connected. ❌

**Failure mode:** IRONVEIL feels the same as MERIDIAN mechanically — just harder
ports and faster base trace. The "active monitoring" property doesn't exist in practice.

**Mitigation — Use what does work:**
IRONVEIL's unique property becomes:
- All nodes have the `<tracker />` tag (confirmed working from IntroExtension) — 
  retaliation fires if the player doesn't clean up after themselves
- IRONVEIL trace starts automatically on connect (security level set high in node XML
  — some Hacknet nodes auto-start trace, controlled by the `<security>` tag)
- traceTime set to 25-30s — dramatically shorter than MERIDIAN's 60s
- This combination means: you connect, trace starts, you have 25 seconds to crack
  everything and disconnect clean. Any mess you leave fires retaliation.
- The FEEL of "active monitoring" is achieved — you're always under the gun — even
  though the mechanism is just short trace + auto-start + tracker.

**Verdict:** Drop the "periodic 30-second check" language. Describe IRONVEIL as
"auto-trace, short window, unforgiving cleanup" — achievable with existing tools.

---

### 🟡 MODERATE — SOVEREIGN dual-connection mechanic needs os.shellIPs verification

**Assumption:** GPSovereignCrackExe can check `os.shellIPs` to verify the player
has an active shell connection to a relay node.

**Verification chain:**
- `os.shells` (List<ShellExe>) and `os.shellIPs` (List<string>) confirmed present
  in OpenHacknet OS.cs. ✅
- BaseExecutable has access to `os` object. ✅
- ShellExe running on a relay keeps its IP in `os.shellIPs` while active. PROBABLE. 🔵
- Shell persists when player connects to a different (SOVEREIGN) node: PROBABLE
  based on how shell architecture works, but NOT confirmed. 🔵

**Failure mode:** `os.shellIPs` is empty when checked from inside the SOVEREIGN
executable, either because shells don't persist across node connections, or because
the field isn't populated the way we think.

**Mitigation:**
Test this in isolation before building SOVEREIGN:
- Add a debug command `gp_debug_shells` that prints `os.shellIPs` contents
- Open a shell on any node, run this command, confirm the IP appears
- Connect to a different node, run the command again, confirm IP persists
- This test takes 15 minutes and resolves a significant unknown

If shells don't persist: SOVEREIGN's mechanic changes to "player must have gained
admin on a relay node this session" — check a flag set by the relay's `<OnAdminGained>`
action instead of live shell state. Less interesting but guaranteed to work.

---

### 🟢 LOW — VAULT 3-phase executable complexity

**Assumption:** A self-contained 3-phase VAULT executable handles its own sequencing
and timer — no external port state monitoring needed.

**Verification chain:**
- BaseExecutable with internal state machine: fully achievable in C#. ✅
- Timer countdown in Update(float t): straightforward. ✅
- 3 phases with progress bars, shared timer: standard UI work. ✅

**Failure mode:** Executable is complex but not impossible. Main risk is just
implementation bugs — off-by-one on phases, timer display glitches.

**Mitigation:** Build it iteratively — first phase only, then add phases 2 and 3.
Keep phase state as a simple int (0, 1, 2) with a shared float timer.

---

---

# Summary — Ranked Issues by Impact

| # | Issue | Milestone | Severity | Status |
|---|-------|-----------|----------|--------|
| 1 | Denomination flags can't store integer balance | M2 | 🔴 CRITICAL | **Redesign: file-based wallet** |
| 2 | SetRAM via ZDTK not verified callable from C# | M2 | 🔴 CRITICAL | **Redesign: set os.ram.totalRam directly** |
| 3 | V3 passphrase string storage impossible in flags | M1 | 🔴 CRITICAL | **Redesign: key-file mechanic** |
| 4 | Shared Alert reactive to flag changes is impossible | M4 | 🔴 CRITICAL | **Accept: cosmetic-only alert** |
| 5 | ShowNode from C# on hardware purchase unverified | M4 | 🔴 CRITICAL | **Mitigate: hub node reveal trigger** |
| 6 | SSD gating file manipulation in actions unverified | M1 | 🟡 MODERATE | **Test first; fallback: node variants** |
| 7 | V3 key seeding race condition (plugin vs Instantly) | M1 | 🟡 MODERATE | **Mitigate: start deterministic** |
| 8 | Breach credits one-shot → player can hit income wall | M2 | 🟡 MODERATE | **Mitigate: tune economy math upfront** |
| 9 | Net tier trace-speed via node variants = 36 XML files | M2 | 🟡 MODERATE | **Accept: drop variant approach** |
| 10 | IRONVEIL periodic monitoring not natively possible | M4 | 🟡 MODERATE | **Accept: auto-trace + tracker instead** |
| 11 | SOVEREIGN os.shellIPs persistence unverified | M4 | 🟡 MODERATE | **Test first with debug command** |
| 12 | File duplication exploit on fence | M2 | 🟢 LOW | Don't care (single player) |
| 13 | VAULT executable complexity | M4 | 🟢 LOW | Build iteratively |
| 14 | V2 port naming conflict with vanilla | M1 | 🟢 LOW | Just use distinct port name strings |

---

## Redesigns That Change the Roadmap

Three design changes that should be made NOW before writing any code or XML:

**1. Wallet is a file, not flags.**
`/home/wallet.dat` contains the player's credit balance as an integer string.
Plugin reads and writes it directly. All currency logic becomes simpler.

**2. V3 is a key-file mechanic, not a passphrase mechanic.**
Player must scp `key_v3.dat` from a relay before running the V3 crack.
Presence of the file = authorization. Cleaner, verifiable, same gameplay feel.

**3. Network trace speed is fixed per network, not per player hardware tier.**
OUTSKIRTS = 120s. MERIDIAN = 60s. IRONVEIL = 30s. Net tier is a gating requirement
for network entry, not a modifier of trace speed. Drop node variants entirely.

These three changes eliminate 4 of the 5 critical issues before a single line of
new code is written.
