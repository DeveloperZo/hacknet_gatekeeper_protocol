# M1 Status

**Last updated:** 2026-02-22
**Status:** BUILT + DEPLOYED — awaiting in-game verification

---

## What's Built and Deployed

| Item | State |
|---|---|
| 6 PF ports registered (ssh/ftp/web × v2/v3 at 22/21/80) | DONE |
| 6 crack executables registered + animated | DONE |
| `--test` (one cycle) and `--infinity` (loop) modes | DONE |
| Per-cracker draw styles: matrix / packets / waveform | DONE |
| `gp_debug` — hardware tiers + full port table | DONE |
| `gp_resetports [ip]` — closes all ports, no reload needed | DONE |
| CPU multiplier applied to solve time | DONE |
| V3 key file gate | DONE |
| V2 tier-up fallback (ssh_v2 absent → tries ssh_v3) | DONE |
| T1/T2/T3 isolated test nodes (10.0.0.21/22/23) | DONE |
| Combined all-tiers test node (10.0.0.10) | DONE |
| V3 key relay node (10.0.0.11) | DONE |
| DLL deployed to BepInEx/plugins/ | DONE |

---

## Deferred to M2

**Port demotion (ssh_v3 → ssh_v2 → ssh after cracking):**
Requires a Harmony patch on `GetAllPortStates()` for the visual side to work properly.
Behavioral flag-based design is documented and ready to implement alongside the visual patch.

**Energize mechanic:**
Prevents demotion on one crack — blocked on demotion being in place.

---

## Remaining: In-Game Verification

Run these tests to call M1 done:

| # | Test | Expected |
|---|---|---|
| 1 | Extension loads | GP in Extensions menu, no crash |
| 2 | All 5 nodes on map | 10.0.0.10/11/21/22/23 visible |
| 3 | `gp_debug` (not connected) | CPU T1, RAM shown, "not connected" |
| 4 | `SSHcrack_v2 --test` | Orange matrix, ~10s, closes cleanly |
| 5 | `SSHcrack_v3 --test` | Key check first, then cyan matrix, closes |
| 6 | `connect 10.0.0.22` → `SSHcrack_v2 22` | ssh_v2 port opens, `gp_debug` shows OPEN |
| 7 | `connect 10.0.0.23` → `SSHcrack_v3 22` | ssh_v3 port opens |
| 8 | V3 without key in /home | Key error message, exits cleanly |
| 9 | `connect 10.0.0.23` → `SSHcrack_v2 22` | Escalation logged, ssh_v3 opens |
| 10 | `gp_resetports` after cracking | `gp_debug` shows all ports CLOSED |

**All 10 pass → M1 complete → move to M2.**
