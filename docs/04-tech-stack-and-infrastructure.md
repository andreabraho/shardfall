# 04 — Tech Stack, Tools and Infrastructure

Your machine, checked 2026-09-18: **Windows 11 Pro**, Git 2.53, **.NET SDK 10.0.202**,
Node 24.14, winget available. Missing: Godot, Blender, Python.

---

## 1. Engine — Godot 4.x with C# (.NET) · decision D1, confirmed

Install the **.NET/Mono build** of Godot (the standard build has no C# support).

```bash
winget install --id GodotEngine.GodotEngine.Mono -e
```

If winget's package id has drifted, download "Godot Engine – .NET" from godotengine.org and
unzip it to `C:\tools\godot\`. Godot is a portable executable; there is no installer.

**.NET note:** Godot 4.x C# projects target `net8.0`. Your SDK 10 can build that, but if the
export or build complains about a missing targeting pack, install the .NET 8 SDK alongside —
they coexist without conflict:

```bash
winget install --id Microsoft.DotNet.SDK.8 -e
```

### Why Godot here, concretely

- `.tscn` scenes and `.tres` resources are **plain text** → I author entire scenes, enemy
  prefabs, VFX setups and data assets directly, without you touching the editor.
- Git-friendly: real diffs, real merges, no binary asset lock-in.
- MIT licence: no royalties, no seats, no per-revenue terms.
- Small runtime, fast iteration, C# with the full .NET ecosystem for tooling.

**The honest trade-off:** Godot's 3D is less mature than Unity's and the asset ecosystem is
much smaller. With placeholder art for the MVP (Q2) the asset gap does not bite yet — but
factor it in when we pick the art pipeline.

---

## 2. Programs and apps — the full list

### Required now (Phase 0)

| Tool | Purpose | Install |
|---|---|---|
| **Godot 4.x .NET** | engine + editor | `winget install --id GodotEngine.GodotEngine.Mono -e` |
| **Git** | version control | ✅ already installed (2.53) |
| **Git LFS** | binary assets (models, textures, audio) | `winget install --id GitHub.GitLFS -e` |
| **.NET SDK** | C# build | ✅ 10.0.202 installed (add SDK 8 if needed) |
| **VS Code** or **Rider** | code editor | `winget install --id Microsoft.VisualStudioCode -e` · Rider is free for non-commercial: `winget install --id JetBrains.Rider -e` |
| **C# Dev Kit + godot-tools** extensions | VS Code Godot/C# support | install from the VS Code marketplace |

### Required soon (Phases 1–4)

| Tool | Purpose | Install |
|---|---|---|
| **GdUnit4** | in-engine + CI test framework for Godot C# | Godot AssetLib, or NuGet `gdUnit4.api` |
| **Python 3.12** | data pipeline scripts (validation, balance sim helpers, asset registry generation) | `winget install --id Python.Python.3.12 -e` |
| **Audacity** | SFX trimming/editing | `winget install --id Audacity.Audacity -e` |

### Required for the art phase (deferred by decision Q2)

| Tool | Purpose | Install |
|---|---|---|
| **Blender 4.x** | modelling, rigging, animation, glTF export | `winget install --id BlenderFoundation.Blender -e` |
| **Krita** or GIMP | textures, icons, UI | `winget install --id KDE.Krita -e` |
| **Mixamo** (web, free) | humanoid animations | mixamo.com — Adobe account |
| **Materialize** (free) | normal/roughness maps from photos | github.com/BoundingBoxSoftware/Materialize |

### Optional / later

| Tool | Purpose |
|---|---|
| **Aseprite** (~€20) | pixel-art icons, if we go that route for item icons |
| **LMMS** / **Reaper** | original music |
| **GodotSteam** | Steamworks integration (achievements, cloud saves) |
| **butler** (itch.io CLI) | one-command itch deploys |
| **Sentry** | crash reporting in released builds |
| **OBS Studio** | capture for trailers and bug reports |

### Free asset sources (when art starts)

Kenney.nl (CC0) · Poly Pizza (CC0/CC-BY) · KayKit (CC0) · Quaternius (CC0) ·
ambientCG (CC0 textures) · Freesound (check per-file licence) · OpenGameArt ·
Synty POLYGON packs (paid, commercial-friendly).
**Rule: every asset gets an `ASSET-LICENSES.md` entry the moment it enters the repo.**

---

## 3. Infrastructure

Single-player and offline, so the infrastructure need is genuinely small — this is a real
advantage of the design and we should not invent server work that does not exist.

| Concern | Choice | Cost |
|---|---|---|
| Source hosting | GitHub private repo | free |
| Large binaries | Git LFS (1 GB free, then $5/mo per 50 GB data pack) | free → $5/mo |
| CI | GitHub Actions with a Godot Docker image; builds + tests + export on push to `main` | free tier is ample |
| Build artifacts | GitHub Actions artifacts; tagged releases via GitHub Releases | free |
| Crash reports | Sentry free tier (only after first public build) | free |
| Distribution | itch.io (free, 0% or your choice) → Steam ($100 one-time app fee) | $100 one-time |
| Project tracking | GitHub Issues + Projects, or a `TODO.md` — your preference | free |
| Backups | GitHub + a local drive copy. **No single point of failure on your PC.** | free |

**No game servers, no database, no accounts, no matchmaking, no CDN.** If we ever add
leaderboards for the Tower (Tier C), that is one small serverless function, not a backend.

### Hardware

| | Minimum | Recommended |
|---|---|---|
| CPU | 4 cores | 8 cores |
| RAM | 16 GB | 32 GB |
| GPU | GTX 1060 / RX 580 | RTX 3060+ |
| Disk | 50 GB free SSD | 200 GB NVMe |

Keep a **GTX 1060-class or integrated-GPU machine** available for perf verification against
NFR-P.1 — it is very easy to build something that only runs on your dev machine.

---

## 4. Architecture

The one decision that shapes everything (D8): **game logic is engine-free C#.**

```
Sohan.sln
├─ src/Sohan.Core/          # pure C#, ZERO Godot references
│   ├─ Combat/              # damage pipeline, status effects, threat
│   ├─ Progression/         # XP, levels, attributes, skill mastery
│   ├─ Items/               # generation, bonus rolls, upgrade, sockets
│   ├─ Encounters/          # shard state machine, wave composition
│   ├─ Quests/              # quest state machine, objectives
│   ├─ Economy/             # yang sinks, vendor stock seeding
│   └─ Save/                # versioned save model + migrations
├─ src/Sohan.Data/          # JSON loading, schema validation, content registries
├─ src/Sohan.Tests/         # GdUnit4 + xUnit, runs headless in CI
├─ src/Sohan.Tools/         # console apps: balance simulator, data validator, codegen
└─ game/                    # the Godot project
    ├─ project.godot
    ├─ scenes/              # .tscn — I author these directly
    ├─ scripts/             # Godot-facing C#: nodes, input, rendering, UI
    ├─ data/                # JSON content: items, enemies, skills, quests, drops
    ├─ art/                 # placeholder primitives now, real assets later
    ├─ audio/
    └─ ui/
```

**Why this split earns its keep:**

- The damage formula, XP curve and item generation are unit-testable in milliseconds with no
  engine boot — so balance changes are verified, not guessed (NFR-R.2, NFR-M.1).
- The **balance simulator** (`Sohan.Tools`) can run 10,000 simulated encounters or walk the
  whole campaign XP table in seconds, because it links `Core` directly. This is how we
  enforce "no grinding required" as a measurable property instead of a hope.
- Co-op stays architecturally possible for Tier C without a rewrite.

### Content pipeline

```
game/data/*.json  ──►  schema validation (CI)  ──►  generated C# id constants
                                                    ──►  runtime registries
```

No content change requires a code change (NFR-M.2). Broken cross-references (an enemy citing
a missing drop table, a quest citing a missing NPC) **fail the build**, which is what keeps a
few thousand data rows from rotting.

---

## 5. Repo conventions

- `main` always builds and is playable. Work on `feat/*` branches, squash-merge.
- Conventional commits (`feat:`, `fix:`, `data:`, `chore:`).
- `.gitattributes` routes `*.png *.jpg *.glb *.ogg *.wav *.blend` to LFS.
- `.gitignore` covers `.godot/`, `bin/`, `obj/`, `.mono/`, `export/`.
- Every PR: CI must pass `dotnet test` + data validation + a headless Godot import check.

---

## 6. Cost summary

| Phase | Cost |
|---|---|
| MVP with placeholder art | **€0** (everything above is free) |
| Art pass (asset packs route) | €150–400 one-time |
| Steam release | $100 one-time |
| Git LFS if assets exceed 1 GB | $5/month |
| Optional Rider commercial licence | €150/yr (free for non-commercial use) |

The MVP costs nothing but time. That is a direct consequence of the Godot + placeholder-art
decisions.
