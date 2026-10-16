# Loopsmith

**See what your Destiny 2 build actually does.**

Loopsmith describes the **cause and effect** in a Destiny 2 build; it doesn't simulate the game
([requirements](docs/requirements.md), [ADRs D1](docs/ADRs.md)). For each trigger it shows every
outcome that fires, which element caused it, what that unlocks next and what doesn't work together.
Numbers are facts shown with their outcome ("+12% grenade energy", "Amplified (15s)"); what only
play decides — that Bolt Charge is at max, that a buff has ended, how many enemies a grenade kills —
you declare, and Loopsmith shows what it sets off.

DIM shows what you have equipped. Loopsmith takes that build (class, subclass, abilities, aspects,
fragments, exotic, armor set, mods, artifact perks, weapons and perks) and explains its
**gameplay loop**. In the web designer you can start from a **DIM link**: paste it on Home and the
designer opens with the loadout's build — what the rules know by hash; the rest is listed, never guessed
([docs/loop-format.md](docs/loop-format.md#starting-from-a-dim-link)). Home also shows the owner's own
Skip Grenade build as a DIM build card.

**The product is the loop you design**: pick triggers one by one (dodge, grenade kill, pick up an
orb, Bolt Charge at max) under the last step, move or remove any step (the loop replays in its new
order), and the result is a `*.loop.yaml` you can save, share as a link, replay and compare with
another loop. A loop is judged by its **order of triggers**: does each step get what it
needs from the steps before it, does the order repeat, and what is wasted where ("Tempest Strike's
Bolt Charge doesn't stack with Dielectric's").

> **Try it:** https://joancomasfdz.github.io/destiny-loopsmith/ — the web loop designer
> (runs entirely in your browser). Status: working prototype on authored rules for one build
> ([Skip Grenade Hunter](builds/skip-grenade-hunter/), plus the creator's own
> [Ascension variant](builds/skip-grenade-hunter-ascension/)); what's next is in the
> [Roadmap](#roadmap) and [docs/backlog.md](docs/backlog.md).

## Quick start

Online: open the link above. Locally (requires the .NET 10 SDK):

```bash
dotnet run --project src/Loopsmith.Web     # the web loop designer, http://localhost:5000
```

## Try the app

Open the web loop designer on GitHub Pages: https://joancomasfdz.github.io/destiny-loopsmith/

## How it works

```
rules/*.yaml ─┐                         ┌─ explain            (static: trigger → outcomes)
build.yaml ───┼─ parse → validate ──────┼─ play a step       (pure state machine: (state, step) → (state, fired))
*.loop.yaml ──┘   (railway, typestate)  └─ analyse / compare (a designed loop's order: needs, sets off, wasted)
```

* Every build element is "an element with rules": `on` trigger → `then` outcomes (+ always-on
  passives). Edges aren't stored; an edge appears when an outcome (a buff, an orb, a debuff,
  energy) matches another rule's trigger. Loops come from that cascade.
* The state is what is present: buffs on you, debuffs on the pack, pickups on the ground, and the
  states you declared ("Bolt Charge at max"). Each step is played against it and gives the next state
  and every rule it fired.
* Outcomes of one event apply in phase order (debuff → empower → damage → spawn → refund),
  then derived events cascade depth-first (depth ≤ 5; a rule never re-fires on an identical
  event up its own causal chain).
* Every number is a `GameValue`, shown as a fact with its outcome; every element keeps its provenance. **Unknown stays
  unknown**: a "?" is shown, never treated as 0.

What Loopsmith is and must do, starting with the level of abstraction:
[docs/requirements.md](docs/requirements.md). Rule and build file format and the exact engine
semantics: [docs/rule-format.md](docs/rule-format.md). Designed loops — the `.loop.yaml` format,
share links, analysis and comparison: [docs/loop-format.md](docs/loop-format.md). Example loops:
[builds/skip-grenade-hunter/loops/](builds/skip-grenade-hunter/loops/) and
[builds/skip-grenade-hunter-ascension/loops/](builds/skip-grenade-hunter-ascension/loops/).
Coding rules (binding): [docs/CODING_RULES.md](docs/CODING_RULES.md) · decisions: [docs/ADRs.md](docs/ADRs.md).

## Repository layout

```
src/Loopsmith.Core/      one project, slices = folders (kernel: Domain, Functional, Phrasing, Causality)
src/Loopsmith.Web/       host: Blazor WebAssembly loop designer (Home, Designer, Compare)
tests/Loopsmith.Core.Tests/   unit, golden and architecture tests
rules/                   authored causality (glossary, keywords, hunter, exotics, armor sets, mods, artifact, weapon perks)
builds/<slug>/           build.yaml, discrepancies, loops/*.loop.yaml, dim-loadout.json (+ the original note, note-map and
                         scenario.txt where the user wrote a note): skip-grenade-hunter and its Ascension variant
docs/                    requirements, rule format, loop format, hosting, backlog
tools/compendium/        Destiny Data Compendium download: sheet_dump.py + Docker/Python scripts
tools/manifest/          Bungie manifest excerpt: get-manifest.sh + extract_manifest.py → rules/manifest.yaml
tools/web/               prepare-pages.sh — readies a published site for GitHub Pages
.github/workflows/       CI, Pages deploy (main), PR previews
.claude/                 cloud-session hook + project subagents
```

Every build, loop and e2e fixture file is checked against the rules by a Core test:
`dotnet test --filter RepoFileValidationTests` names each file that doesn't open and its error.

## Data sources

| Source | Gives | Status |
|---|---|---|
| Authored rules (`rules/`) | Causality: what fires on what | ✅ used by the engine |
| [Clarity](https://github.com/Database-Clarity/Live-Clarity-Database) | Hash-keyed descriptions with numbers (mods, fragments, aspects, exotic perks, weapon traits) | v2.0625 gave the first build's hashes and cross-checks in the rules' comments; no rule takes a number from it; an ingestion slice is open |
| Destiny Data Compendium | Abilities, artifact perks, statuses, and facts such as cooldowns and chunk energy scalars | The 2026-10-09 snapshot's numbers are in the first build's rules, the 2026-10-13 one's in the rest of the Arcstrider, the weapon mods and the chest mods (by hand, `compendium/<date>/<tab>#<row>` sources); parser next |
| Bungie manifest | Identity (hashes), names, descriptions, icons, slots, damage types, perks, catalysts, armor sets | An excerpt for the hashes Loopsmith names (`rules/manifest.yaml`, generated by [tools/manifest/](tools/manifest/README.md)); for other hashes the web app reads the full manifest in the browser; icons load from bungie.net |
| [DIM](https://github.com/DestinyItemManager/DIM) (MIT) | How share links are read; the nine Light subclass hashes (in the glossary); the logo next to the DIM-link box and on DIM build cards | Used by the DIM-link start (FR-14); dim.gg links are read from DIM's open share API, no key ([docs/hosting.md](docs/hosting.md#dim-links)) |

**Getting a Compendium snapshot:** run `tools/compendium/get-compendium.ps1` (Windows) or
`get-compendium.sh` locally and hand the resulting zip to a session — see
[tools/compendium/README.md](tools/compendium/README.md). (`docs.google.com` isn't reachable from
the cloud environment unless it's added to its allowed domains.)

**Licensing.** The Compendium is one person's donation-supported work. Keep snapshots
private, out of any public repo (`snapshots/` is gitignored), never served as raw text,
and credit it. Clarity's partnerships page applies to a public site like this one (an owner check,
[docs/backlog.md](docs/backlog.md)). Bungie API use
falls under Bungie's API terms, and so do the manifest excerpt and the icons the web app loads from bungie.net. Creator video transcripts are third-party content: keep them
locally as `builds/<slug>/transcript.txt` (gitignored) and link the video instead.

## Development workflow

1. Branch from `main`, commit, open a pull request.
2. CI runs the full test suite, and the **PR preview** workflow publishes that branch's web app
   to `https://joancomasfdz.github.io/destiny-loopsmith/pr-preview/pr-<number>/` (the link is
   commented on the PR, updated on every push, removed when the PR closes; pull requests from
   forks get no preview).
3. Merge → the **Deploy web app** workflow republishes the live site.

`gh-pages` is the built site, owned by those workflows; a ruleset blocks its deletion and
force pushes. Details: [docs/hosting.md](docs/hosting.md).

## Roadmap

What works is in each requirement's status ([docs/requirements.md](docs/requirements.md)); the open
work in detail in [docs/backlog.md](docs/backlog.md). Next:

1. Fill a DIM build's gaps — rules for the supers,
   grenades, melees and exotics it wears, the other subclasses (FR-14).
2. Ingest: Compendium snapshot parsers (tab registry) + Clarity enrichment + coverage report (FR-11).
3. One reading of the manifest: the excerpt written with the C# join (FR-13, [ADRs D14](docs/ADRs.md)).
4. Rule drafting from the Compendium's "On X:" phrasing (FR-12, [ADRs D10](docs/ADRs.md)).
5. More builds; richer rules (the rule-format gaps in [docs/backlog.md](docs/backlog.md)).

Destiny 2 is a trademark of Bungie. Loopsmith is a fan project, not affiliated with Bungie.
