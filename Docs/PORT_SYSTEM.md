# PORT SYSTEM — Gatekeeper Protocol

## Design Principle

Two categories of ports exist on every node. Standard ports are obstacles — open them quickly and move on. CORE ports are objectives — expensive, multi-phase, and their crack triggers the Gatekeeper response. Which CORE port appears on a given node is seeded per run.

---

## Standard Ports

Vanilla-style ports using Pathfinder's PFPorts XML system. Fast crack times, low RAM cost. Required to be open before a CORE port can be attacked.

| Port | Protocol | Tool | RAM | Crack Time |
|------|----------|------|-----|------------|
| 22 | ssh | SSHcrack | 55 MB | ~10s base |
| 21 | ftp | FTPBounce | 55 MB | ~10s base |
| 80 | http | WebServerWorm | 80 MB | ~15s base |
| 25 | smtp | SMTPoverflow | 100 MB | ~20s base |
| 1433 | sql | SQL_MemCorrupt | 100 MB | ~20s base |

All crack times scale with CPU tier. These are the same ports Hacknet uses — no new tool needed. The player must open the required count before CORE becomes available.

---

## CORE Ports

CORE ports are the objective layer. Assigned in the 2000–2999 port number range. Each has a matching custom Pathfinder executable (C# ExeModule subclass).

Three CORE port types exist at launch. Each run seeds which one appears on which node based on run seed flag.

---

### CORE Type 1: V3 (port 2001)

**Thematic concept:** V3 interface — cracking a cognitive system  
**Tool executable:** `V3Crack.exe`  
**RAM cost:** 300 MB  
**Dependency:** ssh + http must be open

**Solve mechanic:**
- Multi-phase progress: 3 sequential signal locks displayed as waveforms
- Each lock requires the player to type a 4-character code shown in the display
- No timer reset on disconnect — partial progress persists for 60 seconds (ZeroDayToolKit Trace V2 style)
- Failing a code entry adds 10% to the current trace bar

**Visual:** Oscilloscope-style waveform lines, each lock collapses into noise on solve

**Gatekeeper trigger:** V3 breach signals the Gatekeeper's cognitive layer — triggers the most sophisticated counterattack scripts

---

### CORE Type 2: VAULT (port 2002)

**Thematic concept:** Data vault — brute-force decryption sequence  
**Tool executable:** `VaultBreaker.exe`  
**RAM cost:** 250 MB  
**Dependency:** ftp + sql must be open

**Solve mechanic:**
- Single long progress bar but trace-sensitive: if trace crosses 50%, progress bar resets by 25%
- Forces player to manage TraceKill timing during the crack
- Crack time is slow (~45s base) — intentionally creates TraceKill timing tension

**Visual:** Stack of horizontal bars draining top to bottom, each representing a vault layer

**Gatekeeper trigger:** Vault breach is the "noisy" crack — triggers storage-denial counterattack (makeFile spam on playerComp)

---

### CORE Type 3: SOVEREIGN (port 2002)

**Thematic concept:** Sovereign key — root access crack requiring sequential authentication  
**Tool executable:** `SovereignKey.exe`  
**RAM cost:** 350 MB  
**Dependency:** ssh + ftp + smtp must be open (3 required — most demanding)

**Solve mechanic:**
- Sequential: 3 "authentication rounds" each requiring a different action
  - Round 1: Hold executable (progress fills automatically, do not kill it)
  - Round 2: Type `authenticate` in terminal (checked via custom command)
  - Round 3: Final progress bar, fastest of the three
- Cannot be running simultaneously with TraceKill (RAM cost designed to prevent it)

**Visual:** Three-stage lock icon, each stage lights up on completion

**Gatekeeper trigger:** Sovereign breach triggers the Gatekeeper's most aggressive retaliation — forkbomb attempt + instanttrace flagging

---

## Port System XML (PFPorts)

In the node XML files, standard ports are assigned as normal. CORE port assignment is controlled by a mission action that runs at run-seed time:

```xml
<!-- In node XML -->
<PFPorts>
  ssh:22:Secure_Shell
  ftp:21:File_Transfer
  http:80:Web_Server
  core_v3:2001:V3_Interface
</PFPorts>
```

For roguelike seeding, each node XML has conditional variants controlled by flags:

```xml
<!-- Action file: seed run at start -->
<SetFlag flag="run_core_type" value="v3" />
<!-- or "vault" or "sovereign" depending on seed logic -->
```

The CORE port executable reads this flag on `LoadContent()` to determine which variant it is, so a single `core_crack.exe` file can represent all three types based on the active flag.

---

## portsNeededForCrack

The `portsNeededForCrack` value on each node is set to include the CORE port. Standard port count varies per node tier:

| Node Tier | Standard Ports Required | CORE Ports Required | Total |
|-----------|------------------------|---------------------|-------|
| Entry | 2 | 1 | 3 |
| Mid | 3 | 1 | 4 |
| High | 4 | 1 | 5 |
| Gatekeeper | 5 | 1 | 6 |

Only the CORE port crack fires the breach — standard ports opening does not trigger Gatekeeper response.

---

## Dependency Enforcement

Inside the CORE port executable's `Update()` method, before accepting any crack input:

```csharp
// Check required standard ports are open
bool canProceed = os.connectedComp.isPortOpen("ssh") && 
                  os.connectedComp.isPortOpen("http");
if (!canProceed) {
    // Show error in the exe display
    // "PREREQUISITE PORTS REQUIRED: SSH + HTTP"
    return;
}
```

This is invisible to the game engine — the port count mechanic still uses `portsNeededForCrack`, but the exe refuses to progress until dependencies are met. Player sees a locked UI state with the required ports listed.

---

## Run Seeding

At the start of each run, a seed action file runs:

```xml
<SetFlag flag="core_node_A" value="v3" />
<SetFlag flag="core_node_B" value="vault" />
<SetFlag flag="core_node_C" value="sovereign" />
<SetFlag flag="core_node_D" value="v3" />
```

Each node's CORE executable reads its own flag on initialization. This gives the appearance of a different network each run without needing procedural generation.

The player's tool choice at run start (which CORE executables they download) is the roguelike build decision.

---

## Open Questions

- [ ] Can a single custom executable read a flag and switch between solve modes? (likely yes via Flags.HasFlag())
- [ ] Does `portsNeededForCrack` count custom PFPorts toward breach threshold? (needs test — Pathfinder bug fix #149 related)
- [ ] Multi-phase solve: can an ExeModule persist state between frames without a thread? (yes via static/instance fields — Update() is called every frame)
