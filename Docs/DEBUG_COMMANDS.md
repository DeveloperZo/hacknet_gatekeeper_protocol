# GP Debug Commands & Node Reset Reference

## GP Custom Commands

| Command | Effect |
|---|---|
| `gp_debug` | Print hardware tiers, CPU multiplier, trace state, and GP port states on connected node |
| `gp_drawtest` | Show current draw params and spawn instructions |
| `gp_drawtest header <n>` | Shift matrix start Y live (no rebuild) |
| `gp_drawtest rows <n>` | Change target row count live (no rebuild) |
| `SSHcrack_v2 --test` | Spawn a looping draw-test cracker — no node connection needed |
| `SSHcrack_v3 --test` | Same for V3 (cyan) |

---

## Resetting Node Port State

There is no single in-game command to reset a node's ports mid-session.

### Option 1 — Full extension reload (recommended)
Return to the main menu and re-enter the extension. All nodes reload from XML — ports reset to their `<ports>` / `<PFPorts>` closed state.

```
dc
exit          ← or use the menu
(re-enter Gatekeeper Protocol extension)
```

### Option 2 — New save slot
Start the extension on a fresh save. Fastest for clean-state testing.

---

## Useful Vanilla Commands for Testing

| Command | What it tells you |
|---|---|
| `probe` | List all open/closed ports on connected node (includes PF ports) |
| `probe <ip>` | Probe a specific IP without connecting |
| `scan` | Show nodes visible from current position |
| `connect <ip>` | Connect to a node |
| `dc` | Disconnect |
| `ls` | List files — confirm key files are present for V3 gates |
| `scp <file> <ip>` | Copy a file to another node |

---

## Test Node IPs

| IP | Node | Purpose |
|---|---|---|
| `10.0.0.20` | GP Crack Test | Isolated — port 22 closed, no proxy/firewall |
| `10.0.0.10` | GP Test Node | Full — proxy + firewall + V2/V3 ports |
| `10.0.0.11` | GP Relay Alpha | Holds V3 key files in `/home` |

---

## Live Config (no rebuild)

Edit while the game is running — changes apply within 1 second:

```
D:\SteamLibrary\steamapps\common\Hacknet\BepInEx\plugins\gp_drawtest.cfg
```

```ini
headerH    = 28   # matrix start Y offset
targetRows = 6    # rows in crack animation
```

---

## What to Check in LogOutput.log

```
[GP] TIMER STARTED: ssh solveTime=10s target=10.0.0.20
[GP] TIMER DONE: ssh elapsed=10.1s
[GP] PORT VERIFY: SSHcrack_v2 isOpen=true
[GP] gp_drawtest.cfg reloaded — headerH=28 rows=6
[GP] ABORT: port=22 missing on <ip>      ← node has no port 22
[GP] port 22 already open.               ← need fresh node state
```
