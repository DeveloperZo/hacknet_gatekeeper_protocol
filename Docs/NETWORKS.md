# NETWORKS — Gatekeeper Protocol

## Design Principle

Networks give the roguelike its spatial identity. Each run reveals a different configuration of the same underlying node pool via ShowNode/HideNode mission actions and flag-gated progression. The player feels like they're in a different city each time — but it's authored, not procedural.

---

## Network Zones

Five zones exist in the extension. Not all are visible in any given run.

### Zone 1: OUTSKIRTS (always visible)
Entry-level nodes. Standard corporate servers, low-tier Gatekeepers.
- 6 nodes always visible from run start
- 2-3 standard port requirement
- CORE port tier: Entry (V3 or Vault)
- Gatekeeper profile: Tier 1-2 scripts
- Visual theme: Default Hacknet blue

### Zone 2: MERIDIAN (unlocked at run start, run-seed dependent)
Mid-tier corporate or underground hub. Determines run character.
- 4-5 nodes revealed by `ShowNode` action at run seed
- 3-4 standard port requirement
- CORE port tier: Mid (any type)
- Gatekeeper profile: Tier 2-3 scripts
- Visual theme: Hacknet Teal or custom amber

### Zone 3: IRONVEIL (mid-run unlock, story gated)
High-security government/research zone. Unlocked by completing a MERIDIAN objective.
- 3-4 nodes revealed by mission `missionCompleted` action
- 4-5 standard port requirement  
- CORE port tier: High (SOVEREIGN dominant)
- Gatekeeper profile: Tier 3-4 scripts, Stealth profile more common
- Visual theme: Custom red-tinted dark theme

### Zone 4: DEEPCORE (late-run, optional)
Underground black market and legacy infrastructure. Optional path.
- 2-3 nodes revealed if player finds a specific hidden file in MERIDIAN
- Mixed port requirement (some nodes intentionally have non-standard configs)
- CORE port tier: High (VAULT dominant)
- Gatekeeper profile: Tier 2 (surprising — DEEPCORE is dangerous differently)
- Visual theme: Custom green-on-black hacker aesthetic

### Zone 5: NEXUS (final zone, one node only)
The final objective. One node, maximum defense, highest tier Gatekeeper.
- Single node, always hidden until IRONVEIL objective complete
- Full 5-port requirement + CORE SOVEREIGN
- Gatekeeper: All-tier script, Tier 5 retaliation, Stealth profile
- Immediate counterattack on CORE breach (no delay)
- Visual theme: Switches to custom white-on-black at NEXUS connection

---

## Network Topology XML

Nodes use `positionNear` to cluster around their zone hub. Each zone has an invisible anchor node.

```xml
<!-- OUTSKIRTS cluster around zone hub -->
<positionNear target="outskirts_hub" position="1" total="6" extraDistance="0.05" force="false"/>
<positionNear target="outskirts_hub" position="2" total="6" extraDistance="0.05" force="false"/>
<!-- etc -->
```

The hub node itself is just a linking node — no daemon, no ports. Purely spatial.

---

## Run Seeding via ShowNode/HideNode

At extension start, all nodes except OUTSKIRTS are hidden:

```xml
<!-- StartingActions or opening mission -->
<HideAllNodes />
<ShowNode id="outskirts_hub" />
<ShowNode id="outskirts_01" />
<ShowNode id="outskirts_02" />
<ShowNode id="outskirts_03" />
<ShowNode id="outskirts_04" />
<ShowNode id="outskirts_05" />
<ShowNode id="outskirts_06" />
```

Run seed determines which MERIDIAN cluster is shown:

```xml
<!-- Seed action: fires from mission at run start -->
<!-- Variant A -->
<SetFlag flag="run_seed" value="A" />
<ShowNode id="meridian_corp_01" />
<ShowNode id="meridian_corp_02" />
<ShowNode id="meridian_corp_03" />
<ShowNode id="meridian_corp_04" />
<!-- Variant B -->
<SetFlag flag="run_seed" value="B" />
<ShowNode id="meridian_under_01" />
<ShowNode id="meridian_under_02" />
<ShowNode id="meridian_under_03" />
```

Seed variant is selected based on run number flag (incremented each run). This creates a simple rotation that feels like variety.

---

## Zone Themes

Each zone switches Hacknet's terminal theme on connection to the zone hub.

```xml
<!-- On connect to ironveil_hub -->
<SwitchToTheme ThemePathOrName="Themes/IronveilTheme.xml" FlickerInDuration="2.0" />
```

Custom theme files live in `Extension/Themes/`. The theme switch creates a strong environmental cue — player immediately knows which zone they're in by color.

| Zone | Theme | Primary Color |
|------|-------|---------------|
| OUTSKIRTS | HacknetBlue (default) | #4169E1 |
| MERIDIAN Corp | HacknetTeal | #008080 |
| MERIDIAN Underground | HackerGreen | #00FF00 |
| IRONVEIL | Custom: ironveil.xml | Dark red |
| DEEPCORE | Custom: deepcore.xml | Bright green on black |
| NEXUS | Custom: nexus.xml | White on black |

---

## Network Map Clustering Behavior

The netmap renders based on `positionNear` relationships. Each zone should visually read as a distinct cluster with clear visual separation.

Recommended layout:
```
[OUTSKIRTS cluster]     [IRONVEIL cluster]
         \                    /
          [MERIDIAN cluster]
         /                    \
[DEEPCORE cluster]      [NEXUS — solo]
```

The `extraDistance` parameter controls how spread a cluster is. NEXUS should be far from everything — isolated on the map.

---

## Node File Plan

Minimum node count for a playable run:

| Zone | Nodes | Files Needed |
|------|-------|-------------|
| OUTSKIRTS | 6 + 1 hub | 7 XML files |
| MERIDIAN (Corp) | 4 + 1 hub | 5 XML files |
| MERIDIAN (Underground) | 3 + 1 hub | 4 XML files |
| IRONVEIL | 4 + 1 hub | 5 XML files |
| DEEPCORE | 3 + 1 hub | 4 XML files |
| NEXUS | 1 | 1 XML file |
| SENTINEL companion | 1 | 1 XML file |
| Hardware vendor | 1 | 1 XML file |
| **Total** | **~25** | **~28 XML files** |

---

## Open Questions

- [ ] Does `HideAllNodes` hide the playerComp and ISP node? (likely no — test needed)
- [ ] Can zone hub nodes be completely invisible (no daemon, no ports) without causing errors?
- [ ] Is there a maximum number of ShowNode calls in a single action file?
- [ ] Theme switching on connection — is `OnConnect` action available per-node? (yes — confirmed in Steam guide)
