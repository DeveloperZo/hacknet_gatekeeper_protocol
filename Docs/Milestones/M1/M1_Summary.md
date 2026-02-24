# M1 — Tiered Systems

## Scope
Core port and executable tier system. Foundation everything else builds on.
Port demotion and Energize mechanics are designed here but implemented in M2
(requires Harmony patch on `GetAllPortStates` for visual correctness).

---

## Deliverables

### Tiered Ports
Six custom ports registered via Pathfinder `PortManager`.
All tiers share the same port **number** as vanilla — tier is encoded in the **protocol name** only.
A node carries exactly one flavor of each port family (no coexistence on live nodes).

| Protocol | Port # | Tier | Tool required |
|---|---|---|---|
| ssh | 22 | T1 | SSHcrack (vanilla) |
| ssh_v2 | 22 | T2 | SSHcrack_v2 |
| ssh_v3 | 22 | T3 | SSHcrack_v3 + key file |
| ftp / ftp_v2 / ftp_v3 | 21 | T1/T2/T3 | same pattern |
| web / web_v2 / web_v3 | 80 | T1/T2/T3 | same pattern |

**Soft gate:** lower-tier tools can attempt higher-tier ports. The node's trace timer
is the gate — without CPU hardware upgrades, the crack won't complete in time.

**Tier-up fallback:** V2 crackers automatically escalate to the V3 protocol if the V2
port is absent on the target node (e.g. `SSHcrack_v2` on a pure T3 node).

### Crack Executables
Six executables registered via `ExecutableManager`. All are in player `/bin` at start.

| Executable | Token | Tier | Base time | Key required |
|---|---|---|---|---|
| SSHcrack_v2.exe | `#SSH_V2#` | T2 | 10s | No |
| FTPBounce_v2.exe | `#FTP_V2#` | T2 | 10s | No |
| WebServerWorm_v2.exe | `#WEB_V2#` | T2 | 10s | No |
| SSHcrack_v3.exe | `#SSH_V3#` | T3 | 15s | ssh_v3_key.dat |
| FTPBounce_v3.exe | `#FTP_V3#` | T3 | 15s | ftp_v3_key.dat |
| WebServerWorm_v3.exe | `#WEB_V3#` | T3 | 15s | web_v3_key.dat |

`effective_time = base_time / CpuMultiplier`

V3 key files are checked in player `/home` before the animation starts.
If absent: prints an error and exits immediately without starting the timer.

### Draw Animations
| Style | Used by | Description |
|---|---|---|
| `matrix` | SSH, Web crackers | Hex grid, cells flip from red → tier colour as progress advances |
| `packets` | FTP crackers | Rows complete top-to-bottom, left-to-right |
| `waveform` | Optional (SSH) | Diagonal sine-wave sweep, configurable via `gp_drawtest.cfg` |

Animation layout is hot-reloadable from `BepInEx/plugins/gp_drawtest.cfg` — no rebuild needed.

### Hardware Flags
Read from `os.Flags` in M1. Writing (upgrades) implemented in M3.

| Component | Flags | M1 effect |
|---|---|---|
| CPU | `cpu_t2/t3/t4` | Crack speed: 1.5× / 2.25× / 3.0× |
| RAM | `ram_t2/t3/t4` | Displayed in `gp_debug` |
| HDD | `hdd_t2/t3/t4` | Displayed in `gp_debug` (functional in M3) |
| NIC | `nic_t2/t3/t4` | Displayed in `gp_debug` (functional in M3) |

### Debug Commands
| Command | Effect |
|---|---|
| `gp_debug` | Hardware tiers, CPU mult, trace state, all ports (vanilla + GP) on connected node |
| `gp_resetports [ip]` | Close all vanilla (22/21/80) + all GP PF ports — no reload needed |
| `gp_drawtest` | Show current draw layout params |
| `SSHcrack_v2 --test` | One-cycle animation then closes (no node connection needed) |
| `SSHcrack_v2 --infinity` | Loop animation forever (visual tuning) |

### Test Nodes
All nodes are visible from the start and linked to playerComp on the network map.

| IP | ID | Ports | Trace | Purpose |
|---|---|---|---|---|
| 10.0.0.21 | gp_t1_test | vanilla 22/21/80 | 9999s | Isolated T1 — vanilla crackers |
| 10.0.0.22 | gp_t2_test | vanilla 22 + ssh_v2/ftp_v2/web_v2 | 9999s | Isolated T2 — V2 crackers + soft gate |
| 10.0.0.23 | gp_t3_test | ssh_v3/ftp_v3/web_v3 only | 9999s | Isolated T3 — V3 crackers + V2 escalation |
| 10.0.0.10 | gp_test_node | all 9 + proxy 50s + firewall | 120s | Full-run combined node |
| 10.0.0.11 | gp_relay_alpha | vanilla 22/21 | 120s | V3 key file store |

V3 key files (`ssh_v3_key.dat`, `ftp_v3_key.dat`, `web_v3_key.dat`) are pre-seeded
in player `/home` via `PlayerComp.xml` for M1 testing convenience.

---

## Deferred to M2

### Port Demotion
After cracking `ssh_v3`, it demotes to `ssh_v2` for the next visit, then to vanilla `ssh`.
Requires a Harmony patch on `GetAllPortStates()` so `probe` and the port panel reflect the
demoted state. The flag-based design is finalised and ready for M2 implementation.

### Energize Mechanic
One-shot token per node that blocks a single demotion. Blocked on demotion being live.
Flag schema: `gp_energized_<nodeId>_<family>` (ssh / ftp / web).

---

## Completion Criteria

| # | Criterion | Source of truth |
|---|---|---|
| 1 | Extension loads without crash | ExtensionInfo.xml, BepInEx log |
| 2 | All 5 test nodes visible on map | StartingVisibleNodes in ExtensionInfo.xml |
| 3 | `gp_debug` shows hardware tiers + port table | GpDebugCommand in GatekeeperProtocol.cs |
| 4 | All 6 GP executables animate and open port | GPCrackBase.Update + concrete subclasses |
| 5 | V3 key gate rejects when key absent | keyFileName check in GPCrackBase.Update |
| 6 | V2 cracker escalates on pure V3 node | tier-up fallback in GPCrackBase.Update |
| 7 | `gp_resetports` closes all ports | GpResetPortsCommand |
| 8 | CPU multiplier scales solve time | `elapsed += t * HardwareState.CpuMultiplier(os)` |

See `M1_Test.md` for the step-by-step walkthrough.
