# ROADMAP — Gatekeeper Protocol
# Scope: M1, M2, M3, M4. Retaliation and SENTINEL parked.

---

## Core Design Principles

- Plugin owns all economic state. Nothing value-related lives on virtual nodes.
- Player gets clear feedback on state and failure reasons. No mutation ability.
- Vanilla systems (proxy/firewall/shell) used as-is.
- Two custom port tiers only. Same executables across all networks and all port families.
- Economy flows through gp_scp file transfers. No buy/sell shop commands.
- Crack executables are generic — port families are data, not code.
- Each crack executable is its own tier. No global script tier flag.

---

## Port System

### Port Families
Each family has three tiers: base (vanilla), v2, v3.
Current families: ssh, ftp, web
Adding a new family (e.g. foobar) requires:
  - Register foobar, foobar_v2, foobar_v3 as PFPorts (~10 lines)
  - Write FoobarCrackExe for the vanilla tier (~20 lines)
  - GPV2CrackExe and GPV3CrackExe handle foobar_v2 and foobar_v3 automatically

### Crack Executable Architecture
Each crack executable is its own tier — no global script tier flag.
Running a V2 cracker means the player has V2-capable cracking software.

| Crack exe          | Base time | Gate                       | CPU effect |
|--------------------|-----------|----------------------------|------------|
| Vanilla (SSHcrack) | ~5-10s    | None                       | None       |
| V2 (GPSSHv2)       | 10s       | None                       | / CpuMult  |
| V3 (GPSSHv3)       | 10s       | Key file in /home required | / CpuMult  |

Effective crack time = 10s / CpuMultiplier:
| CPU Tier | Multiplier | V2/V3 time |
|----------|------------|------------|
| T1       | 1.0x       | 10.0s      |
| T2       | 1.5x       | 6.7s       |
| T3       | 2.25x      | 4.4s       |
| T4       | 3.0x       | 3.3s       |

V3 key files: `<portBase>_v3_key.dat` in player /home.
  ssh_v3 requires ssh_v3_key.dat, ftp_v3 requires ftp_v3_key.dat, etc.
  Player acquires keys by cracking relay nodes and scp-ing the file.

---

## Full Node Attack Sequence

1. Shell → `overload` (proxy, vanilla, occupies RAM slot)
2. `analyze` → `solve <answer>` (firewall, vanilla)
3. Vanilla ports (SSHcrack / FTPBounce / WebServerWorm)
4. V2 ports (GP V2 crackers — 10s base, CPU mult)
5. V3 ports (GP V3 crackers — key file required, 10s base, CPU mult)
6. Admin gained = full disarm complete

---

## Hardware

### CPU
Controls crack speed multiplier. Applied to all GP executables.
Effective time = 10s / CpuMult.

| Tier | Multiplier | Cost  | Effect                     |
|------|------------|-------|----------------------------|
| 1    | 1.0x       | —     | Default                    |
| 2    | 1.5x       | 250cr | V2 cracks in 6.7s          |
| 3    | 2.25x      | 600cr | V2 in 4.4s. Unlocks th3    |
| 4    | 3.0x       | 1200cr| V2 in 3.3s. Unlocks p14n3t |

### RAM
Determines how many and how large processes can run simultaneously.
Shell occupies one slot. Set via os.ram.totalRam in plugin.

| Tier | Cost  | MB  | Effect                              |
|------|-------|-----|-------------------------------------|
| 1    | —     | 512 | Default (4 process slots)           |
| 2    | 200cr | 768 | 5 slots — run more exes at once     |
| 3    | 500cr | 1024| 6 slots. Unlocks p14n3t             |

### HDD
Determines inventory size. Larger crack executables require more HDD space.
Player stores executables and acquired files in their inventory.

| Tier | Cost  | Effect                                  |
|------|-------|-----------------------------------------|
| 1    | —     | Default capacity                        |
| 2    | 150cr | +50% inventory space                   |
| 3    | 400cr | +100% inventory space                  |

### NIC
Increases effective trace time (survivable window) and upload/download speed.
Network entry gates still require CPU + NIC thresholds.

| Tier | Cost  | Trace bonus | Transfer speed | Gate                  |
|------|-------|-------------|----------------|-----------------------|
| 1    | —     | +0s         | Base           | h4ck (120s trace)     |
| 2    | 300cr | +15s        | 1.5x           | Unlocks th3 (60s)     |
| 3    | 750cr | +30s        | 2.0x           | Unlocks p14n3t (30s)  |

---

## Economy

### State ownership
All state in BepInEx/config/gp_state.json. Not accessible via in-game terminal.
Player has read feedback only. Zero mutation ability.

### gp_scp
`gp_scp <source> <destination>`

Upload (player → server):
- Transfers file, adds credit value
- `[GP] Uploaded <file>. +Xcr. Balance: Ycr.`

Download (server → player):
- Checks balance >= cost
- Success: `[GP] Downloaded <file>. -Xcr. Balance: Ycr.`
- Fail: `[GP] Insufficient credits. Cost: Xcr. Balance: Ycr. Shortfall: Zcr.`

File values hardcoded in plugin FileValueTable. Never on virtual nodes.

### gp_credits
Read-only. Balance + last 5 transactions.

### Crack executable upgrades
V2 and V3 crack exes are acquired from exchange nodes via gp_scp.
Each download installs the exe to player /bin.
No global tier flag — having the exe means having the capability.

---

## Networks

All networks use the same port families (ssh/ftp/web at vanilla/V2/V3).
Difficulty comes from trace speed, proxy/firewall presence, and port composition.

### h4ck
- Entry: none
- traceTime: 120s
- Nodes: 5

| Node          | Proxy | Firewall | Ports                       | Notes                  |
|---------------|-------|----------|-----------------------------|------------------------|
| h4ck_relay_a  | No    | No       | ssh, ftp                    | holds ftp_v3_key.dat   |
| h4ck_relay_b  | No    | No       | ssh, web                    | holds ssh_v3_key.dat   |
| h4ck_corp_a   | Yes   | No       | ssh, ftp, ssh_v2, ftp_v3    | needs key from relay_a |
| h4ck_corp_b   | No    | Yes      | ssh, ftp_v2, web_v2         | pure V2 node           |
| h4ck_exchange | No    | No       | None                        | economy node           |

### th3
- Entry: CPU T3 + NIC T2
- traceTime: 60s
- Nodes: 6
- Authored in M4

### p14n3t
- Entry: CPU T4 + NIC T3 + RAM T3
- traceTime: 30s
- Nodes: 5
- Authored in M4

### Network reveal
After hardware state change: check thresholds in plugin.
If newly met: fire ShowNode actions for that network.
Fallback: h4ck_exchange OnConnect fires conditional reveal.

---

## Milestones

### M1 — The Hack Feels Different (~1.5hrs)
Plugin + test node XML. No economy, no missions.

- [x] Register ssh_v2, ftp_v2, web_v2 as PFPorts
- [x] Register ssh_v3, ftp_v3, web_v3 as PFPorts
- [x] GPV2CrackExe: 10s base, no gate, CPU mult applied
- [x] GPV3CrackExe: key file gate, 10s base, CPU mult applied
- [x] HardwareState: CPU/RAM/HDD/NIC tier readers
- [x] gp_debug: prints hardware tiers, trace state, GP port status on connected node
- [x] gp_test_node XML: all 9 ports (3 vanilla + 3 V2 + 3 V3), proxy, firewall
- [x] gp_relay_alpha XML: V3 key files
- [x] ExtensionInfo.xml: GP name/description, visible nodes set

Done when: V2 crack takes ~10s, V3 fails without key, succeeds with key, CPU tier changes crack speed.

### M2 — h4ck Has Nodes (~1.5hrs)
XML only. Remove M1 test key pre-seeding from StartingActions.

- [ ] 5 h4ck node XML files
- [ ] ftp_v3_key.dat on relay_a, ssh_v3_key.dat on relay_b
- [ ] Opening mission: intro, points at relay_a, first breach goal
- [ ] Breach and full disarm mission goals per node
- [ ] Remove M1 pre-seeded key files from StartingActions.xml

Node economy:
| Node      | Total  | Breach 35% | Full disarm 65% |
|-----------|--------|------------|-----------------|
| relay_a   | 120cr  | 42cr       | 78cr            |
| relay_b   | 120cr  | 42cr       | 78cr            |
| corp_a    | 400cr  | 140cr      | 260cr           |
| corp_b    | 400cr  | 140cr      | 260cr           |
| Total max | 1040cr | 364cr      | 676cr           |

CPU T3 (600) + NIC T2 (300) = 900cr to unlock th3.
Upload economy on h4ck_exchange covers the margin.

Done when: extension loads, intro mission works, h4ck fully navigable, timing correct on all port types.

### M3 — Economy Runs (~1hr)
Plugin: CreditManager, gp_scp, gp_credits, FileValueTable, hardware upgrade shop.

- [ ] gp_state.json persistence (balance + last 5 transactions)
- [ ] FileValueTable with h4ck file values
- [ ] gp_scp with full player feedback
- [ ] gp_credits read-only display
- [ ] Mission actions wired: breach and full disarm payments per node
- [ ] V2 crack exe upgrade available on h4ck_exchange
- [ ] Hardware upgrade commands: gp_buy cpu_t2 / ram_t2 / hdd_t2 / nic_t2
- [ ] RAM tier: set os.ram.totalRam directly from C# on purchase
- [ ] HDD tier: track inventory limit in plugin state

Done when: player earns credits, buys hardware, crack speed improves with CPU, can attempt V3 with key.

### M4 — th3 and p14n3t (~1hr)
XML + network reveal.

- [ ] 6 th3 node XML files (vanilla/V2/V3 mix, 60s trace)
- [ ] 5 p14n3t node XML files (vanilla/V2/V3 mix, 30s trace)
- [ ] th3 and p14n3t exchange nodes
- [ ] Network reveal logic in plugin post-purchase threshold check
- [ ] FileValueTable expanded for th3 and p14n3t files

Done when: all three networks navigable, gates enforced, full upgrade path h4ck → p14n3t.

---

## Status

| Milestone               | Status                        |
|-------------------------|-------------------------------|
| M1 — Hack feels different | 🟡 BUILT, NEEDS IN-GAME TEST |
| M2 — h4ck nodes          | 🔲                           |
| M3 — Economy runs         | 🔲                           |
| M4 — th3 and p14n3t       | 🔲                           |
| Plugin compile proof      | ✅ gp_debug works             |

---

## Parked
- Retaliation scripts
- SENTINEL AI
- NEXUS endgame
- Roguelike seeding
- Trace Harmony patch
- Additional port families (e.g. smtp family, sql family)
- VAULT / SOVEREIGN mechanics
