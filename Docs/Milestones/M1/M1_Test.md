# M1 Manual Test Walkthrough

**Prerequisites:** Hacknet running with the Gatekeeper Protocol extension loaded.
Start a **fresh game** (new save) so all nodes are in their default state.

Estimated time: ~10 minutes.

---

## Step 1 — Extension Loads

Launch Hacknet. From the main menu, open **Extensions** and select **Gatekeeper Protocol**.

**Pass:** Extension appears in the list, no crash, game starts normally.
**Fail:** Crash or missing extension → check `BepInEx/LogOutput.log` for errors.

---

## Step 2 — Network Map

Once in-game, open the network map.

**Expected nodes visible:**
| IP | Name |
|---|---|
| 10.0.0.10 | GP Test Node |
| 10.0.0.11 | GP Relay Alpha |
| 10.0.0.21 | T1 Test Node |
| 10.0.0.22 | T2 Test Node |
| 10.0.0.23 | T3 Test Node |

**Pass:** All 5 nodes appear on the map, linked to your workstation.
**Fail:** Missing nodes → ExtensionInfo.xml `StartingVisibleNodes` or node XML may be broken.

---

## Step 3 — gp_debug (Unconnected)

Without connecting to any node, type:

```
gp_debug
```

**Expected output:**
```
[GP] ===== GATEKEEPER PROTOCOL DEBUG =====

CPU  : T1 (1.00x)
RAM  : T1 [...]
HDD  : T1 [M3]
NIC  : T1 [M3]
...
TRACE: inactive

Node : [not connected]
```

**Pass:** Hardware shows T1, trace inactive, no crash.

---

## Step 4 — Animation Test (No Node Needed)

```
SSHcrack_v2 --test
```

**Expected:**
- Orange matrix animation appears in RAM panel
- Hex characters flip from red → orange over ~10 seconds
- Process closes automatically when complete, prints `test complete.`

```
SSHcrack_v3 --test
```

**Expected:**
- Cyan matrix animation, ~15 seconds, closes cleanly

**Pass:** Both animations run and close without hanging in RAM.
**Fail:** Stuck in RAM or no animation → check `LogOutput.log` for `TIMER STARTED`.

---

## Step 5 — T1 Node (Vanilla Crackers)

```
connect 10.0.0.21
SSHcrack 22
```

**Expected:** Red → green matrix, ~10s, port 22 opens.

```
gp_debug
```

**Expected port line:**
```
  :22    [OPEN  ]  vanilla
```

```
gp_resetports
gp_debug
```

**Expected:** `:22 [CLOSED]` — reset confirmed.

---

## Step 6 — T2 Node (V2 Crackers)

```
dc
connect 10.0.0.22
```

### 6a — Vanilla on T2 (soft gate baseline)

```
SSHcrack 22
```

**Expected:** Opens vanilla port 22 (red → green). V2 PF port `ssh_v2` stays CLOSED.

```
gp_debug
```

**Expected:**
```
  :22    [OPEN  ]  vanilla
  ssh_v2:22      [CLOSED]  T2
```

```
gp_resetports
```

### 6b — V2 crackers

```
SSHcrack_v2 22
```

**Expected:** Orange matrix, ~10s, `ssh_v2` opens.

```
FTPBounce_v2 21
```

**Expected:** Orange packets animation, ~10s, `ftp_v2` opens.

```
WebServerWorm_v2 80
```

**Expected:** Orange matrix, ~10s, `web_v2` opens.

```
gp_debug
```

**Expected all three V2 ports OPEN:**
```
  ssh_v2:22      [OPEN  ]  T2
  ftp_v2:21      [OPEN  ]  T2
  web_v2:80      [OPEN  ]  T2
```

**Pass:** All three V2 crackers animate and open their respective ports.

```
gp_resetports
```

---

## Step 7 — T3 Node (Key Gate — Negative Test)

First, delete the SSH key from your `/home` to test the rejection path.

```
dc
cd /home
rm ssh_v3_key.dat
connect 10.0.0.23
SSHcrack_v3 22
```

**Expected:**
```
[GP] V3 HANDSHAKE FAILED.
[GP] Key file required: ssh_v3_key.dat
[GP] Obtain the key from a relay node and scp it to /home.
```
Process exits immediately — no animation, no port opened.

**Pass:** Key gate rejects correctly.

---

## Step 8 — T3 Node (V3 Crackers With Key)

Restore the key (reload the extension via main menu, or copy from relay):

```
dc
```
> **Quickest restore:** return to main menu and re-enter the extension. Keys are re-seeded from PlayerComp.xml on a fresh load. Or: `connect 10.0.0.11` → `cd /home` → `scp ssh_v3_key.dat <your_ip>`.

```
connect 10.0.0.23
SSHcrack_v3 22
```

**Expected:** Cyan matrix, ~15s, `ssh_v3` opens.

```
FTPBounce_v3 21
```

**Expected:** Cyan packets, ~15s, `ftp_v3` opens.

```
WebServerWorm_v3 80
```

**Expected:** Cyan matrix, ~15s, `web_v3` opens.

```
gp_debug
```

**Expected:**
```
  ssh_v3:22      [OPEN  ]  T3
  ftp_v3:21      [OPEN  ]  T3
  web_v3:80      [OPEN  ]  T3
```

**Pass:** All three V3 crackers animate, key gate passes, ports open.

```
gp_resetports
```

---

## Step 9 — V2 Escalation Fallback

Still connected to T3 node (10.0.0.23 — no `ssh_v2` port present):

```
SSHcrack_v2 22
```

**Expected terminal output:**
```
[GP] ssh_v2 absent — attempting ssh_v3 (buffs recommended).
```

**Expected:** Animation runs (~10s), `ssh_v3` opens.

**Expected in `LogOutput.log`:**
```
[GP] SSHcrack_v2 escalating target: ssh_v2 -> ssh_v3
```

**Pass:** V2 cracker falls back to V3 protocol automatically when V2 is absent.

```
gp_resetports
```

---

## Step 10 — gp_resetports (Explicit Verification)

```
SSHcrack_v2 22
```
Wait for it to complete (ssh_v3 opens via fallback).

```
gp_debug
```
Confirm `ssh_v3:22 [OPEN]`.

```
gp_resetports
gp_debug
```

**Expected:** `ssh_v3:22 [CLOSED]` — all ports back to closed.

**Also test remote reset (no connection needed):**
```
dc
SSHcrack_v2 --test
```
Wait for completion (exits — no port side effects since `--test`).

```
gp_resetports 10.0.0.23
```

**Expected:** `[GP] T3 Test Node (10.0.0.23): X port(s) reset.`

**Pass:** Both connected and remote reset work correctly.

---

## Result

| # | Test | Result |
|---|---|---|
| 1 | Extension loads | |
| 2 | All 5 nodes on map | |
| 3 | gp_debug (unconnected) | |
| 4 | --test animation modes | |
| 5 | T1 vanilla crackers | |
| 6a | Vanilla on T2 node (soft gate) | |
| 6b | V2 crackers on T2 node | |
| 7 | V3 key gate rejection | |
| 8 | V3 crackers with key | |
| 9 | V2 escalation to V3 | |
| 10 | gp_resetports | |

**All pass → M1 complete.**

---

## If Something Fails

Check `D:\SteamLibrary\steamapps\common\Hacknet\BepInEx\LogOutput.log` for:

| Log line | Meaning |
|---|---|
| `[GP] Registered 6 custom ports` | Ports registered correctly on load |
| `[GP] Registered 6 executables` | Executables registered correctly |
| `[GP] TIMER STARTED: ssh_v2 solveTime=10s` | Cracker init succeeded |
| `[GP] PORT VERIFY: SSHcrack_v2 isOpen=true` | Port opened and confirmed |
| `[GP] ABORT: port=ssh_v2 missing` | Node doesn't have that PF port |
| `[GP] escalating target: ssh_v2 -> ssh_v3` | Tier-up fallback triggered |
| `[GP] V3 HANDSHAKE FAILED` | Key file not found in /home |
| `[GP] gp_resetports: closed X ports` | Reset succeeded |
