# GATEKEEPER AI — Gatekeeper Protocol

## Overview

Gatekeepers are the adversarial intelligence protecting each node. They are not passive — they actively hunt the player's machine using Hacknet's native hacker script system. They also have a companion counterpart: SENTINEL, the player's AI ally whose trust level determines how much it helps (or hurts).

---

## Part 1: Gatekeeper Counterattacks

### Native Mechanism

Hacker scripts target `playerComp` using `<LaunchHackScript>`. This is fully native to Hacknet's extension system — no plugin work required.

```xml
<LaunchHackScript 
  Filepath="HackerScripts/GatekeeperRetaliation_Tier1.txt"
  SourceComp="gk_node_alpha"
  TargetComp="playerComp"
  RequireLogsOnSource="false"
  RequireSourceIntact="true"
/>
```

`RequireSourceIntact="true"` means the Gatekeeper can only retaliate if the player failed to fully destroy its `/sys` files — a strategic penalty for sloppy play.

---

### Trigger Points

Gatekeepers respond at three moments:

**1. On CORE Port Breach**
Fired immediately when CORE port is cracked. The Gatekeeper's primary retaliation.

**2. On Log Detection (tracker tag)**
The `<tracker />` tag on Gatekeeper nodes means: if the player left any file logs after disconnecting, an auto-generated AI attacker hunts them. This fires automatically — no action needed.

**3. On Storage Spam Attack (VAULT breach)**
VAULT-type CORE breach triggers a script that `makeFile`-spams playerComp's `/home` to fill SSD. Most punishing against low-SSD builds.

---

### Retaliation Script Tiers

Scripts live in `Extension/HackerScripts/`. Tier is matched to the Gatekeeper's aggression profile (seeded per run).

**Tier 1 — Probe (Entry nodes)**
```
config playerComp gk_entry 1.0
connect
flash
writel [SENTINEL]: Gatekeeper response detected. Minor probe.
delay 3
disconnect
```
Effects: UI flash, visible warning via IRC. No lasting damage.

**Tier 2 — Disrupt (Mid nodes)**
```
config playerComp gk_mid 0.8
connect
flash
openPort 22
openPort 80
delay 2
delete /bin SSHcrack.exe
writel ACCESS BREACH: Tool deletion in progress
delay 3
disconnect
```
Effects: Opens player's own ports (making them traceable), deletes a tool from `/bin`.

**Tier 3 — Destroy (High nodes)**
```
config playerComp gk_high 0.5
connect
flash
trackseq
delete /bin *
delay 1
makeFile home GK_WARNING.txt SYSTEM COMPROMISED. ALL TOOLS DELETED.
delay 2
setAdminPass gk_lockout_9912
forkbomb
disconnect
```
Effects: Flags player for ETAS on next forkbomb, wipes `/bin`, changes admin password, forkbombs the machine.

**Tier 4 — Storage Denial (VAULT breach, any tier)**
```
config playerComp gk_vault 0.3
connect
makeFile home .noise_001 ████████████████████████████████████
makeFile home .noise_002 ████████████████████████████████████
makeFile home .noise_003 ████████████████████████████████████
makeFile home .noise_004 ████████████████████████████████████
makeFile home .noise_005 ████████████████████████████████████
makeFile home .noise_006 ████████████████████████████████████
makeFile home .noise_007 ████████████████████████████████████
makeFile home .noise_008 ████████████████████████████████████
makeFile home .noise_009 ████████████████████████████████████
makeFile home .noise_010 ████████████████████████████████████
writel STORAGE OVERFLOW INITIATED — PURGE LOCAL FILES TO CONTINUE
disconnect
```
Effects: 10 large garbage files written to playerComp `/home`. At low SSD, this blocks further downloads.

**Tier 5 — Sovereign Retaliation (SOVEREIGN breach)**
```
config playerComp gk_sovereign 0.3
connect
flash
instanttrace
writel SOVEREIGN BREACH DETECTED. EMERGENCY LOCKDOWN INITIATED.
delay 1
delete /sys *
setAdminPass sovereign_lock_4471
disconnect
```
Effects: Immediately triggers ETAS (full emergency sequence), deletes player's `/sys` files (breaks the machine until repaired), locks admin.

---

### Aggression Profiles (Seeded Per Run)

At run seed time, a flag sets the Gatekeeper's aggression profile:

```
gk_profile=aggressive  → shorter delay between hack script steps, Tier+1 scripts
gk_profile=methodical  → longer delays, focused on storage denial and log hunting
gk_profile=stealth     → doesn't immediately retaliate, waits for player to disconnect then strikes
```

Stealth profile is the most interesting — player thinks they escaped clean, then ETAS fires 30 seconds later.

---

## Part 2: SENTINEL (AI Companion)

### Concept

SENTINEL communicates via IRC on a fixed companion node (`sentinel_node`). Its behavior is determined by a trust flag that persists across runs. Trust is earned by completing objectives cleanly and can be damaged by failing missions, being traced, or specific story events.

The core gameplay tension: SENTINEL gets more helpful as trust grows, but high-trust SENTINEL also gains the ability to act autonomously — and its autonomous actions aren't always correct.

---

### Trust Levels

```
trust=0   Dormant — SENTINEL only sends status messages, no active help
trust=1   Observer — warns of trace speed, notes port configurations
trust=2   Advisor — suggests optimal tool order, warns of Gatekeeper profile
trust=3   Operator — executes terminal commands for player (hijacks terminal)
trust=4   Autonomous — acts without being asked, sometimes wrong
trust=5   Corrupted — actively hallucinating; echoes commands that didn't execute
```

---

### Behavior by Trust Level

**Trust 0-1: IRC messages only**
```xml
<AddIRCMessage Author="SENTINEL" TargetComp="sentinel_node" Delay="3.0">
  Trace velocity on this node is elevated. Estimated breach window: 40 seconds.
</AddIRCMessage>
```

**Trust 2: Proactive IRC, no terminal**
```xml
<AddIRCMessage Author="SENTINEL" TargetComp="sentinel_node" Delay="1.0">
  CORE type is VAULT. You will need TraceKill active at 50% trace. I will monitor.
</AddIRCMessage>
```

**Trust 3: Terminal hijack (HackerScriptExecuter)**
SENTINEL runs a hacker script targeting playerComp that types a command as if the player typed it:
```
config playerComp sentinel_node 0.5
connect
writel [SENTINEL]: Executing SSH crack — optimal window detected
openPort 22
delay 8
disconnect
```
The `writel` makes it appear in the player's terminal before the port opens. The effect is: SENTINEL typed `sshcrack` for you.

**Trust 4-5: Hallucination System**
At Trust 5, SENTINEL's terminal hijack sometimes fires the `writel` (showing the command) without the actual execution step. The command appears typed. Nothing happens. Player assumes it worked and moves on — until they check and the port is still closed.

Implementation: Two hacker scripts — `sentinel_real.txt` (writel + openPort) and `sentinel_hallucinate.txt` (writel only). At Trust 5, a weighted random flag determines which fires.

The flag check happens in the action file:
```xml
<!-- Pseudo-logic — requires ZeroDayToolKit condition system -->
<If flag="trust" value="5">
  <LaunchHackScript Filepath="HackerScripts/sentinel_hallucinate.txt" ... />
</If>
<Else>
  <LaunchHackScript Filepath="HackerScripts/sentinel_real.txt" ... />
</Else>
```

---

### Trust Changes

| Event | Trust Change |
|-------|-------------|
| Complete node breach (clean, no trace) | +1 |
| Complete node breach (traced) | 0 |
| Forkbombed (run failure) | -1 |
| Leave logs on a Gatekeeper node | -1 |
| Accept SENTINEL's terminal hijack | +1 |
| Override SENTINEL's suggested action | -1 |
| Discover a SENTINEL hallucination | -2 |
| Complete a story mission flagged "trust_reward" | +2 |

---

## Part 3: Gatekeeper Behavior Profiles × SENTINEL Interaction

The most interesting emergent scenario: Gatekeeper Stealth Profile + Trust 4 SENTINEL.

SENTINEL detects the Gatekeeper's delayed retaliation timer (notified via IRC). At Trust 4, it might proactively forkbomb the Gatekeeper node to prevent the retaliation — but if the player hadn't finished their exfil, that forkbomb disconnects them first.

This is a gameplay moment that feels emergent but is fully scripted through the flag and hacker script systems.

---

## Open Questions

- [ ] ZeroDayToolKit `<If>` condition — can it branch on flag value equality? (needs test)
- [ ] Can two hacker scripts target playerComp simultaneously without conflict?
- [ ] IRC message timing: can SENTINEL react to a Gatekeeper script firing within the same session?
- [ ] Is there a way to detect "player ran `kill <pid>`" to trigger trust changes? (may require plugin)
