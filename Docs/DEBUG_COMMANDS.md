# GP Debug Commands & Node Reset Reference

## GP Custom Commands

| Command | Effect |
|---|---|
| `gp_debug` | Hardware tiers, CPU multiplier, trace state, **all ports** (vanilla + GP) on connected node |
| `gp_resetports` | Close all vanilla + GP PF ports on connected node — instant reset, no reload needed |
| `gp_resetports <ip>` | Reset ports on any node by IP (no need to be connected) |
| `gp_drawtest` | Show current draw params and spawn instructions |
| `gp_drawtest header <n>` | Shift matrix start Y live (no rebuild) |
| `SSHcrack_v2 --test` | One-cycle draw test — runs animation once then closes (no node connection needed) |
| `SSHcrack_v2 --infinity` | Loop draw test forever (for visual iteration) |
| `SSHcrack_v3 --test` | Same for V3 (cyan) |

---

## Test All Tiers Against All Ports

`gp_test_node` (10.0.0.10) has all 9 ports co-present — use it for cross-tier testing:

```
connect 10.0.0.10

# T1 — vanilla ports (all three)
SSHcrack 22        → opens ssh(22)  [vanilla, red→green]
FTPBounce 21       → opens ftp(21)  [vanilla]
WebServerWorm 80   → opens web(80)  [vanilla]

# T2 — GP V2 ports
SSHcrack_v2 22     → opens ssh_v2(22)  [orange matrix]
FTPBounce_v2 21    → opens ftp_v2(21)  [orange packets]
WebServerWorm_v2 80 → opens web_v2(80) [orange matrix]

# T3 — GP V3 ports (key files pre-seeded in /home for M1 testing)
SSHcrack_v3 22     → opens ssh_v3(22)  [cyan matrix]
FTPBounce_v3 21    → opens ftp_v3(21)  [cyan packets]
WebServerWorm_v3 80 → opens web_v3(80) [cyan matrix]

# Check all states
gp_debug

# Reset and repeat
gp_resetports
gp_debug           → all ports CLOSED again
```

### Cross-tier tests (soft gate / fallback mechanics):

```
# V2 cracker on T3 node (ssh_v2 absent → escalates to ssh_v3)
connect 10.0.0.23    ← gp_t3_test has only ssh_v3
SSHcrack_v2 22       → logs "escalating ssh_v2 -> ssh_v3", proceeds
gp_resetports

# Vanilla cracker on T2 node (soft-gate timing path)
connect 10.0.0.22    ← gp_t2_test has vanilla 22 + ssh_v2
SSHcrack 22          → opens vanilla port 22 (ssh_v2 stays CLOSED)
gp_debug             → :22 OPEN (vanilla), ssh_v2:22 CLOSED
gp_resetports
```

---

## Dedicated Tier Test Nodes

| IP | Node | Ports | Trace | Use |
|---|---|---|---|---|
| `10.0.0.21` | T1 Test | vanilla 22/21/80 | 9999s | Vanilla cracker isolation |
| `10.0.0.22` | T2 Test | vanilla 22 + ssh_v2/ftp_v2/web_v2 | 9999s | V2 crackers + soft-gate test |
| `10.0.0.23` | T3 Test | ssh_v3/ftp_v3/web_v3 only | 9999s | V3 crackers + V2 escalation |
| `10.0.0.10` | All-Tiers | all 9 ports + proxy + firewall | 120s | Full-run combined test |
| `10.0.0.11` | Relay Alpha | vanilla 22/21 | 120s | V3 key file store |

---

## Resetting Port State

### Quick reset (in-game, instant)
```
gp_resetports            ← connected node
gp_resetports 10.0.0.10  ← any node by IP
```
Closes all vanilla ports (22/21/80) and all GP PF ports on the target node.
No disconnect needed. Run `gp_debug` or `probe` to confirm.

### Full reset (all nodes)
```
gp_resetports 10.0.0.10
gp_resetports 10.0.0.21
gp_resetports 10.0.0.22
gp_resetports 10.0.0.23
```

### Nuclear — extension reload
Return to main menu and re-enter the extension. Reloads all XML from disk.

---

## Useful Vanilla Commands

| Command | What it tells you |
|---|---|
| `probe` | All open/closed ports on connected node (includes PF ports) |
| `probe <ip>` | Probe a specific IP without connecting |
| `scan` | Nodes visible from current position |
| `connect <ip>` | Connect to a node |
| `dc` | Disconnect |
| `ls /home` | Confirm V3 key files are present |
| `scp <file> <ip>` | Copy a file to another node |

---

## Live Config (no rebuild)

Edit while the game is running — changes apply within ~1 second:

```
D:\SteamLibrary\steamapps\common\Hacknet\BepInEx\plugins\gp_drawtest.cfg
```

```ini
headerH     = 28    # header height in px
targetRows  = 6     # rows in crack animation
accentH     = 3     # accent bar height
labelOffset = 11    # header text baseline offset
sshStyle    = matrix   # matrix | packets | waveform
ftpStyle    = packets
webStyle    = matrix
```

---

## What to Check in LogOutput.log

```
[GP] TIMER STARTED: ssh_v2 solveTime=10s target=10.0.0.22
[GP] TIMER DONE: ssh_v2 elapsed=10.1s
[GP] PORT VERIFY: SSHcrack_v2 isOpen=true
[GP] SSHcrack_v2 escalating target: ssh_v2 -> ssh_v3   ← fallback triggered
[GP] gp_resetports: closed 9 ports on 10.0.0.10
[GP] cfg reloaded — headerH=28 rows=6
[GP] ABORT: port=ssh_v2 missing on <ip>   ← node has neither ssh_v2 nor ssh_v3
```
