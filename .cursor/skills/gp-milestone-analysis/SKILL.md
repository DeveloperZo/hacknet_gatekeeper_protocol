---
name: gp-milestone-analysis
description: Creates a thorough milestone analysis document for Gatekeeper Protocol. Use when the user asks to analyze a milestone, plan implementation, audit existing code against a milestone, or create a Milestones/MX/ analysis doc.
---

# GP Milestone Analysis

Creates `Milestones/MX/MX_ANALYSIS.md` — a structured implementation plan for a milestone.

## Before Writing — Read These Files

Always read all of these before drafting:

```
Plugin/GatekeeperProtocol.cs        — current plugin implementation
Docs/MOD_OVERVIEW.md                — feature pillars and milestone scope
Docs/PORT_SYSTEM.md                 — port design (if M involves ports)
Docs/HARDWARE_SYSTEM.md             — hardware design (if M involves HW)
Docs/GATEKEEPER_AI.md               — AI/hacker scripts (if M involves GK)
Extension/                          — all XML files (current extension state)
Milestones/                         — any prior milestone analyses
```

## Analysis Document Structure

```markdown
# MX ANALYSIS — Gatekeeper Protocol
## Milestone: [Name]

**Date / Plugin version / Build state**

---

## 1. Scope of MX
What MX delivers. "MX is done when:" — 3-5 bullet criteria.

---

## 2. Plugin Audit
Per-class/method review of existing code.
For each section:
- Status: Correct / Likely correct / RISK
- Risk level: HIGH / MEDIUM / LOW
- Specific fix if needed (code snippet)

---

## 3. Extension XML Audit
Table: what exists vs what's missing.
For each missing item: provide the full XML template.

---

## 4. Ordered Implementation Tasks
Table with: # | Task | File(s) | Risk
In dependency order — each step testable before the next.

---

## 5. MX Definition of Done
Checkbox list — verifiable in-game behaviors.

---

## 6. Open API Questions
Table: Question | Impact | How to check
Only genuinely unknown things — don't list things already verified.

---

## 7. What MX Does NOT Include
Out-of-scope items deferred to later milestones.
```

## Key Things to Always Check

**Plugin risks to flag:**
- `openPort()` — must take `int portNum`, not string protocol
- `os.write()` — ASCII only, no Unicode
- `args[1]` not `args[0]` for command arguments
- `needsRemoval = true` not `isExiting`
- `Log.LogInfo()` not `Logger.LogInfo()`

**Extension risks to flag:**
- `#GP_EXE#` or any undefined token in `AddAsset`
- Missing `<PFPorts>` tag when custom ports are intended
- Missing node from `StartingVisibleNodes`
- Admin type mismatch for node intent

## Output Location

```
Milestones/
  M1/M1_ANALYSIS.md   ← already exists
  M2/M2_ANALYSIS.md   ← next
  M3/M3_ANALYSIS.md
```

Create directory with `New-Item -ItemType Directory -Path "...\Milestones\MX" -Force`.
