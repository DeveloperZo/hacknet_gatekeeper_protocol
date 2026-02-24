# M1 Status

**Last updated:** 2026-02-22
**Status:** COMPLETE

---

## What's Built and Deployed

| Item | State |
|---|---|
| 6 PF ports registered (ssh/ftp/web × v2/v3 at 22/21/80) | DONE |
| 6 crack executables registered + animated | DONE |
| `--test` (one cycle) and `--infinity` (loop) modes | DONE |
| Per-cracker draw styles: matrix / packets / waveform (V2) | DONE |
| V3 mini-games: Signal Sync / Packet Sort / Injection Timing | DONE |
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

## Tech Debt

**Mini-game input: SPACE key conflicts with terminal.**
`Keyboard.GetState().IsKeyDown(Keys.Space)` reads raw hardware state — if the player
types in the terminal while a V3 cracker is running, SPACE registers in both.
Options to resolve later:
- Intercept terminal input (patch `OS.runCommand`) and suppress SPACE while a mini-game is active
- Replace SPACE with a different input per mini-game (e.g. a dedicated key like `F`, `G`)
- Detect whether the terminal input field is focused and only pass SPACE to the mini-game otherwise

**Deferred** — functional but slightly leaky. Document in M2 or M3 as a polish task.
