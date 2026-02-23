# MOD OVERVIEW — Gatekeeper Protocol

## Concept

A roguelike Hacknet extension where each run seeds a different network topology, different CORE port distribution, and a different Gatekeeper behavior profile. The player upgrades hardware between runs, the AI companion assists (and occasionally betrays), and Gatekeepers actively counterattack the player's machine.

---

## Core Loop

```
START RUN
  ↓
Choose loadout (tools committed at run start)
  ↓
Seed determines: network layout, CORE port types, Gatekeeper aggression profile
  ↓
Hack nodes — open standard ports → crack CORE port → breach triggers
  ↓
Gatekeeper responds — counterattacks playerComp, plants false data, hunts logs
  ↓
AI companion assists via IRC / terminal hijack (trust-dependent behavior)
  ↓
Complete objective OR get traced/forkbombed
  ↓
END RUN — earn hardware upgrade credits based on performance
  ↓
Spend at Hardware Node → RAM / CPU / Network / SSD upgrades
  ↓
Next run seeds with higher-tier Gatekeepers
```

---

## Feature Pillars

### 1. Hardware RPG
Player machine starts weak. Every stat upgrade materially changes feel.
- **RAM** — how many tools run simultaneously
- **CPU** — how fast tools crack ports and analyze firewalls
- **Network** — how slow the trace fills, how fast scp completes
- **SSD** — how many tools and files can be stored

See: `HARDWARE_SYSTEM.md`

### 2. Two-Tier Port System
Every node has standard ports (fast, cheap) AND one CORE port (expensive, multi-phase, seeded per run).
- Standard ports must be open before CORE port can be cracked
- Only CORE port crack triggers breach and Gatekeeper response
- CORE port type is seeded — different runs require different loadouts

See: `PORT_SYSTEM.md`

### 3. Gatekeeper Counterattacks
Gatekeepers are not passive. They actively hunt the player's machine.
- Hacker scripts targeting playerComp — forkbomb, file deletion, storage spam
- `tracker` tag on Gatekeeper nodes — auto-counterattack if logs left behind
- Escalating aggression as player progresses through run
- Gatekeeper behavior profile seeded per run (fast/aggressive vs. slow/methodical)

See: `GATEKEEPER_AI.md`

### 4. AI Companion (SENTINEL)
AI companion communicates via IRC. Builds trust across runs.
- Low trust: informational only (warns of trace speed, suggests tools)
- Medium trust: proactive terminal hijack (cracks ports for player)
- High trust / corrupted: hallucinated commands (echoes a command that didn't execute)
- Trust is a flag that persists run-to-run

See: `GATEKEEPER_AI.md`

### 5. Multiple Networks
Each run reveals different network clusters via ShowNode/HideNode.
- Corporate, underground, government, research zones
- Zone unlocked as run progression reward
- Each zone has its own visual theme and Gatekeeper tier

See: `NETWORKS.md`

---

## What Is NOT Being Built

- Procedural node generation (too complex — flag-gated reveal instead)
- Custom defense layer types (not worth Harmony complexity — port system covers this)
- Multiplayer (single-player roguelike only)
- New firewall/proxy types (existing ones are sufficient with level tuning)
