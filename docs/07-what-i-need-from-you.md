# 07 — What I Need From You

Everything on this list is something I genuinely cannot do from here. Everything *not* on
this list, I can do.

---

## A. Right now — unblocks Phase 0 (~40 minutes of your time)

| # | Action | Why me asking is unavoidable |
|---|---|---|
| A1 | **Install Godot 4.x .NET build** — `winget install --id GodotEngine.GodotEngine.Mono -e` | Installers need your machine and consent |
| A2 | **Install Git LFS** — `winget install --id GitHub.GitLFS -e` | same |
| A3 | **Install VS Code + "C# Dev Kit" and "godot-tools" extensions** (or Rider) | same |
| A4 | **Create the GitHub private repo** and give me the remote URL | I can't create remotes or hold your credentials |
| A5 | **Open Godot once**, let it download export templates | One-time GUI step |

```bash
winget install --id GodotEngine.GodotEngine.Mono -e ; winget install --id GitHub.GitLFS -e ; winget install --id Microsoft.VisualStudioCode -e
```

Once A1–A5 are done, say so and I'll execute all of Phase 0 (ENG-02, 04–09) in one go.

---

## B. Decisions still open

| # | Question | Default if you don't answer |
|---|---|---|
| B1 | **Final project name** (needed before any public build — IP hygiene) | Keep codename `Kiln` through MVP |
| B2 | **Commercial release or portfolio project?** | Assume commercial → stricter asset licensing |
| B3 | **Languages at launch** | EN + IT (you'd correct my Italian) |
| B4 | **Controller support in v1.0?** | Deferred — click-to-move makes it genuinely awkward, decide at Tier B |
| B5 | **Setting/tone**: faithful oriental fantasy, or drift toward something more your own? | Faithful oriental fantasy, original names |

None of these block Phase 0 or 1.

---

## C. Things only you can judge — the feel reviews

These are the real gates in the plan. I can build to a spec, but I cannot tell whether
something feels good.

| Phase | Review | What to do |
|---|---|---|
| 1 | **MOV-08** — click-to-move responsiveness | Play 20 min in the greybox arena. Note anything that feels sluggish, imprecise or wrong. This is the most important review in the project |
| 2 | **CBT-14** — combat feel | Fight each enemy role. Are telegraphs readable? Do hits feel weighty with capsules? |
| 5 | **SHD-10** — shard encounter tuning | Play every shard tier. Too easy, too long, too chaotic? |
| 6 | **WLD-11** — zone layouts | Do the zones read as places? Are encounters spaced right? |
| 7 | **QST-09** — story and tone | Does the writing land? Are the names good? |
| 8 | **TST-01/02** — full playthroughs + the stranger test | The gate to Tier B |

Written notes are enough — bullet points, no formality. "The character hesitates before
moving when I click behind me" is exactly the kind of note I can act on.

---

## D. Editor work I'll hand you

I author `.tscn` files directly, so this is smaller than on most engines — but some things
are faster or only possible by hand:

| Task | Phase |
|---|---|
| Bake NavMesh after layout changes | 1, 6 |
| Greybox level layout iteration (I place a first pass from a spec; you shape it by eye) | 6 |
| Visual tuning of lights, fog, post-processing | 6, 8 |
| Godot export preset setup + signing config | 0, 8 |

---

## E. Assets — deferred by your placeholder decision

Nothing needed for the MVP. When we reach Phase 9, you'll need to decide: asset packs
(€150–400, ~40 h), model in Blender (~200–400 h), or commission (€2,000–8,000).

One exception, in Phase 8:

| # | Item | Notes |
|---|---|---|
| E1 | ~40 CC0 sound effects + 3 music tracks | Freesound / OpenGameArt / Kenney. You pick by taste and check licences; I wire them in and mix |

---

## F. Accounts and money

| # | What | When | Cost |
|---|---|---|---|
| F1 | GitHub account | now | free |
| F2 | Git LFS data pack, if assets exceed 1 GB | Phase 9 | $5/mo |
| F3 | itch.io account | first public build | free |
| F4 | Steamworks account | release | $100 one-time |
| F5 | Adobe account for Mixamo | Phase 9 | free |

**MVP total cost: €0.**

---

## G. What I do without asking

So you know where the line is. I will: write all C# (engine and logic), author all `.tscn`
scenes and `.tres` resources, write all JSON content data, build the tooling and simulators,
set up CI, write and run tests, write documentation, place a first-pass level layout from a
spec, wire up audio and VFX, do balance passes against the simulators, and fix bugs.

I will **check with you before**: renaming the project, adding any third-party dependency or
asset, changing a decision recorded in doc 00, pushing to a remote, or anything that costs
money.

---

## The single next step

Run the three installs in §A, create the GitHub repo, and tell me the remote URL.
I'll take Phase 0 from there.
