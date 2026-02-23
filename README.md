# Gatekeeper Protocol — Hacknet Mod

**Platform:** Hacknet + Pathfinder API + ZeroDayToolKit  
**Type:** Roguelike extension with custom BepInEx plugin  
**Status:** Planning / Pre-development

---

## Repository Structure

```
Hacknet_GP/
├── README.md               ← This file
├── Docs/                   ← Design documents
│   ├── MOD_OVERVIEW.md     ← High-level design & feature list
│   ├── HARDWARE_SYSTEM.md  ← RAM / CPU / Network / SSD design
│   ├── PORT_SYSTEM.md      ← Standard + CORE port architecture
│   ├── GATEKEEPER_AI.md    ← Gatekeeper behavior, counterattack, trust
│   └── NETWORKS.md         ← Network topology, roguelike seeding
├── Plugin/                 ← C# BepInEx plugin source
│   └── (GatekeeperPlugin.cs stubs to follow)
└── Extension/              ← Hacknet extension XML files
    └── (Nodes, missions, hacker scripts to follow)
```

---

## Quick Reference

| Feature | Implementation | Status |
|---|---|---|
| RAM upgrade | ZeroDayToolKit `<SetRAM>` | ✅ Native |
| CPU upgrade | Harmony patch on ExeModule | 🔧 Needs plugin |
| Network upgrade | Harmony patch on trace + scp | 🔧 Needs plugin |
| SSD upgrade | Harmony patch on file write | 🔧 Needs plugin |
| Gatekeeper attacks player | Native hacker scripts | ✅ Native |
| CORE ports | Pathfinder custom executables | ✅ Supported |
| Standard ports | Pathfinder PFPorts XML | ✅ Native |
| AI companion (IRC) | Native IRCDaemon + ZeroDayToolKit | ✅ Native |
| AI terminal hijack | HackerScriptExecuter.runScript() | ✅ Native |
| Multiple networks | ShowNode/HideNode + positionNear | ✅ Native |
| Roguelike seeding | Flag-based unlock/reveal | ✅ Native |

---

## Key Dependencies

- **Hacknet** (Steam, Labyrinths DLC recommended)
- **Hacknet Pathfinder** v5.3.2+ — https://github.com/Arkhist/Hacknet-Pathfinder
- **ZeroDayToolKit** — https://github.com/prodzpod/ZeroDayToolKit
- **.NET Framework 4.0** (for plugin compilation)
- **BepInEx** (bundled with Pathfinder)
