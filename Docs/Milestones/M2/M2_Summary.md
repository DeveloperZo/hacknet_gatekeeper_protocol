# M2 — Gatekeeper Attack

## Scope
Gatekeeper simulation engine, player home network, breach/response loop, bluescreen
resolution. Loss formula is live but currency/hardware impact is stubbed (actual
deduction happens in M3 when economy exists).

---

## MVP (M2 Ship Requirement)

The minimum viable M2 is these four things working together:

1. **GK simulation** — GK cracks player nodes autonomously, pass by pass
2. **Network trace timer** — global countdown, always visible in a dedicated display
3. **Node trace display** — when player is on a GK-active node, shows GK progress on that node — visually distinct from the network timer
4. **Two bluescreen conditions** — timer elapses OR all nodes captured

Everything else in this document (energize, upgrade, player node lockout) is post-MVP and
can be deferred within M2 or to M3.

---

## The Flow

### 1. Incursion Alert
A scripted event fires. The player receives a terminal message:

```
[GATEKEEPER] INCURSION DETECTED
Network trace active — T-90s remaining.
Run gp_setentry <nodeID> to designate entry point.
Run gp_scan to profile the incoming gatekeeper.
```

The **network trace timer** begins immediately and runs for the entire incursion.
The gatekeeper does not start until `gp_setentry` is called OR the timer expires.

---

### 2. Entry Selection
Player runs `gp_setentry <nodeID>` to choose which of their nodes the gatekeeper enters
from first.

- Funnel the gatekeeper into a well-defended bottleneck (buys time on other nodes)
- Or designate a low-value node to take the first hit (preserve high-value nodes longer)

Player can also run:
- `gp_plan` — node table: ID, defense profile, estimated GK passes needed, value score
- `gp_scan` — gatekeeper speed profile (how fast it cracks each defense type)

If the timer expires before `gp_setentry` is called, entry node is chosen automatically
(lowest-defense accessible node).

---

### 3. Gatekeeper Simulation
Runs in `Update()` as a state machine once entry is set.

The gatekeeper cracks nodes **the same way a player does** — crack progress advances each
tick as a float (0→1) based on the node's defense profile and the GK's speed profile.
No special pass model needed; it is just an automated player.

Gatekeeper traverses nodes in priority order (highest value score first), starting from
the designated entry node. When progress hits 1.0 it captures the node and moves on.

Crack progress rate driven by:

| Defense type | GK speed |
|---|---|
| Proxy | Slow (high resistance) |
| Firewall | Fast (low resistance) |
| SSH port | Slow |
| FTP / Web port | Moderate |
| No defenses on node | Instant capture |

#### Node outcomes

**Progress reaches 1.0 (normal case):**
- Node flagged **captured** (`gp_node_<id>_captured = true`)
- All defense levels drop by 1 (floor 0)
- Energized defense exempt from that drop; energize consumed
- If player is **on that node at moment of capture**: node bluescreen (§6)
- GK moves to next node in priority order

**GK node trace elapses before progress reaches 1.0 (rare):**
- Node stays **uncaptured** — defenses untouched
- GK expelled, moves on
- Heavier defenses slow GK progress → more time for this to trigger

---

### 4. Timers — Display and Distinction

Two timers are visible during an incursion. They must be visually distinct so the player
always knows which clock is which.

#### Network Trace Timer (global)
- **Flag:** `gp_net_trace_remaining`
- **Scope:** Entire incursion — runs from alert to resolution, never pauses
- **Display:** Persistent header/HUD line, always on screen during incursion
- **Format:** `[NETWORK TRACE: 42s]` — color: red, counts down urgently
- Shown in: incursion HUD, `gp_monitor`, `gp_plan` output

#### GK Node Trace (per node, local)
- **Flag:** `gp_node_<id>_gk_trace_remaining`
- **Scope:** Only while GK is actively cracking this specific node
- **Display:** Shown in terminal *only* when player is connected to a GK-active node
- **Format:** `[GK INTRUSION: ██████░░ pass 4/6]` — color: orange/amber, shows pass progress
- Disappears when player disconnects or GK moves on

> **Key UX rule:** Network trace is always visible and red. Node trace is contextual and
> amber. A player glancing at the screen should never confuse "how long until bluescreen"
> with "how far through this node is the GK."

---

### 5. Player Counterplay (post-MVP)

These are implemented after the MVP is verified working.

#### Energize (no Harmony required)
Mark one defense as energized — exempt from one GK downgrade pass. Consumed on use.
Not freely reversible; changing requires hacking the node (Timer 3 risk).

`gp_energize proxy|firewall|ssh|ftp|web`

#### Upgrade — *Harmony required*
Run crack executables on own nodes to increase defense level.
Starts player node trace (Timer 3) — if it elapses, player locked out of node.
Requires Harmony patch on `openPort()` to redirect outcome to defense increment.
Deferred to M3/M4 Harmony milestone.

#### Player Node Lockout
If Timer 3 elapses while player is hacking own node:
`gp_node_<id>_player_locked = true` — node inaccessible for rest of incursion.
Deferred alongside Upgrade.

#### Executable Lock (on GK-active nodes)
While GK is active on a node, player can observe but not run crack exes.
`ACCESS DENIED — hostile presence detected`

---

### 6. Monitoring
`gp_monitor` (run from any node) prints a live table:

```
NETWORK STATUS — live

  NODE          DEFENSE              GK PROGRESS    STATUS
  home_alpha    ssh:2 ftp:1 prx:3    --             HELD
  home_beta     ssh:1                 pass 2/2       GK ACTIVE
  home_gamma    prx:1                 --             HELD
  home_delta    ssh:0 ftp:0           --             CAPTURED

  [NETWORK TRACE: 42s]          Nodes held: 3/4
```

Refreshes every 3 seconds. Usable while connected elsewhere.

---

### 7. Resolution — Bluescreen

#### Node bluescreen (disruption — incursion continues)
**Trigger:** Player is on a node at the exact moment GK fully degrades it (capture).
Player is ejected. No resource loss. Incursion continues.

#### Network bluescreen (incursion ends)
**Trigger:** `gp_net_trace_remaining` hits 0  **OR**  all nodes captured.

Incursion ends. Resources retained based on nodes held at resolution:

```
resources_retained = nodes_held / nodes_total
loss_amount        = total_resources * (1 - resources_retained) * decay_curve
```

- More nodes held when timer hits 0 → more resources kept
- All nodes captured → minimum floor retained (cannot be zeroed)

`resources_retained %` printed in M2. Actual deduction goes live in M3.

---

## Flag Schema

### Node Defense Flags
```
gp_node_<id>_proxy_level    int   0-3
gp_node_<id>_firewall_level int   0-3
gp_node_<id>_ssh_level      int   0-3
gp_node_<id>_ftp_level      int   0-3
gp_node_<id>_web_level      int   0-3
gp_node_<id>_energized      str   proxy|firewall|ssh|ftp|web|empty  (post-MVP)
```

### Gatekeeper State Flags
```
gp_gk_active                    bool  true while simulation is running
gp_gk_node_current              str   nodeID currently being cracked
gp_node_<id>_gk_progress        float 0.0-1.0 crack progress on this node
gp_node_<id>_gk_active          bool  true while GK is actively on this node
gp_node_<id>_gk_trace_remaining float GK's time limit per node (seconds)
gp_node_<id>_captured           bool  true after progress reaches 1.0
```

### Network State Flags
```
gp_entry_node                   str   nodeID designated by player (gp_setentry)
gp_net_trace_remaining          float global incursion clock (seconds)
gp_nodes_total                  int
gp_nodes_held                   int   recalculated each tick
```

### Player Node Flags (post-MVP)
```
gp_node_<id>_player_trace_remaining  float  counts down while player hacks own node
gp_node_<id>_player_locked           bool   node inaccessible if this is true
```

### Gatekeeper Speed Profile
```
gp_gk_proxy_resist          float  high = slow on proxies
gp_gk_firewall_resist       float  low  = fast on firewalls
gp_gk_ssh_resist            float
gp_gk_ftp_resist            float
gp_gk_web_resist            float
```

---

## gp_scan Output (Example)
```
GATEKEEPER PROFILE — SCAN COMPLETE

Proxy resistance:    HIGH    [crack speed: 0.25x — slow]
Firewall resistance: LOW     [crack speed: 2.0x — fast]
SSH resistance:      HIGH    [crack speed: 0.33x — slow]
FTP resistance:      MEDIUM  [crack speed: 1.0x]
Web resistance:      MEDIUM  [crack speed: 1.0x]

Recommendation: keep proxy and SSH levels high on your most valuable nodes.
```

---

## Harmony Dependency Note

| Mechanic | Dependency | Milestone |
|---|---|---|
| Crack executables upgrade own-node defenses | Harmony patch on `openPort()` | M3 or M4 |
| Port demotion visual (from M1) | Harmony patch on `GetAllPortStates()` | M3 or M4 |

Both ship in the same Harmony PR.

---

## Completion Criteria

### MVP (required for M2)
- Incursion alert fires, `gp_net_trace_remaining` begins counting down
- `gp_setentry` accepts a nodeID and sets the entry flag
- `gp_scan` returns gatekeeper speed profile
- `gp_plan` prints node table with defense profiles and network timer
- GK crack progress advances per tick, respects defense resistance speeds
- GK node trace counts down per node; if elapsed, node stays uncaptured, GK moves on
- GK capture triggers when progress reaches 1.0; defense levels drop by 1
- **Network timer displayed persistently (red) — always visible during incursion**
- **GK node trace displayed contextually (amber) — only when player is on that node**
- **The two displays are visually distinct — no ambiguity between global and local timers**
- Node bluescreen: player ejected on capture while connected (no resource loss)
- Network bluescreen: triggers on timer expiry or all nodes captured
- `resources_retained %` calculated and printed correctly

### Post-MVP (complete within M2 if time allows, else M3)
- `gp_energize` command implemented and energized defense exempt from one GK pass
- Executable lock active on GK-active nodes
- `gp_monitor` live table with both timer display and pass progress
