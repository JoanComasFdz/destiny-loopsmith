# Loopsmith CLI

The CLI does the same from a terminal (the golden tests snapshot the same text it prints):

```bash
dotnet build Loopsmith.slnx
dotnet run --project src/Loopsmith.Cli -- explain  builds/skip-grenade-hunter/build.yaml
dotnet run --project src/Loopsmith.Cli -- trace    builds/skip-grenade-hunter/build.yaml \
    --scenario builds/skip-grenade-hunter/scenario.txt --state
dotnet run --project src/Loopsmith.Cli -- play     builds/skip-grenade-hunter/build.yaml --save my.loop.yaml
dotnet run --project src/Loopsmith.Cli -- loop     builds/skip-grenade-hunter/loops/infinite-skip-grenades.loop.yaml
dotnet run --project src/Loopsmith.Cli -- compare  builds/skip-grenade-hunter/loops/infinite-skip-grenades.loop.yaml \
    builds/skip-grenade-hunter/loops/melee-first.loop.yaml
dotnet run --project src/Loopsmith.Cli -- loops    builds/skip-grenade-hunter/build.yaml
dotnet run --project src/Loopsmith.Cli -- graph    builds/skip-grenade-hunter/build.yaml --loops-only > loop.mmd
```

| Command | What you get |
|---|---|
| `explain` | Every trigger in the build → the outcomes it fires `[source]`, in the same shape as a hand-written build note (`--tree` for an aligned tree) |
| `trace` | A sequence of steps (`--actions class,grenade:kill:3,max:bolt-charge,energy:kill` or `--scenario file`), one block per step: the event → outcomes `[source]`, cascades indented `↳`; `--state` adds what is present after each step (buffs on you, buffs on your weapons, debuffs on the pack, pickups on the ground) and what a status switches on (`While Amplified  +1 Bolt Charge per gain [Spark of Frequency]`). `--why` shows the reason text; `--caveats` the caveats (amount unknown, already active, …). `simulate` is an alias |
| `play` | Interactive: pick the next step by number or token — an action, a pickup on the ground or a state you declare — and see what fires and what's available now. Every step becomes part of the loop you design: `u` undo, `n <note>`, `d <description>`, `a` analyse it so far; `--save <file.loop.yaml>` writes it on quit (`w` saves now), `--name` names it |
| `loop` | A designed loop (`*.loop.yaml`): its build, its order of steps and the verdict — it repeats, or it breaks at the step that can't happen — then each step with what it needs (and which step provided it), what it sets off in this order and on its own, and what is wasted there, and where the first pass from a fresh spawn differs. `--trace` adds the full trace of the first pass, and of the repeating pass when it differs |
| `compare` | Two designed loops' orders side by side (they may use different builds): each order on one line with its verdict, then trigger by trigger its step in each loop, where its needs come from there, and the elements that fire in only one of them; then the links of each loop's chain the other lacks |
| `loops` | Discovered loops: cycles in the cause → effect graph, ability loops first ("ability loop — gives grenade energy back"). Reaching a stacking buff's max is a link you declare; where a rule gives way to one it doesn't stack with, the loop passes a "Doesn't stack" node (not counted as a step) |
| `graph` | A Mermaid flowchart of the graph, with loop edges drawn thick; `Gain Bolt Charge → Max Bolt Charge` is a dotted edge labelled "you declare"; the arrows of rules that don't stack meet in a "Doesn't stack" rhombus, and only the rule that applies leaves it. It renders on GitHub and at mermaid.live |
| `validate` | The build checked against the rule catalog (unknown elements, wrong slots, inert elements, rules that don't stack) |

Every command takes `--rules <dir>` (default: the nearest `rules/` with a `glossary.yaml` above the build or loop
file, then above the current directory) and `--no-color` (also when `NO_COLOR` is set or the output is redirected); `loops` and
`graph` show `--limit <n>` loops (default 10); `--verbose` is `--why --caveats`. `--help` lists every
option.

## What it looks like

`explain` on the [Skip Grenade Hunter](../../builds/skip-grenade-hunter/) (an excerpt), compare with the
[original note](../../builds/skip-grenade-hunter/note.txt):

```text
Class ability -> +?% melee energy [Gambler's Dodge] + +12% grenade energy [Bomber] + Reaper (10s) [Reaper] + +1 Slice (8s) [Slice]
Grenade damage -> Jolt target (10s) [Spark of Shock] + +1 Bolt Charge and +4.2% grenade energy [Shinobu's Vow]
Ability damage -> consumes Bolt Charge and Bolt Charge strike (kills) (while Bolt Charge at max) [Bolt Charge]
Kill Jolted target -> +1 Bolt Charge (doesn't stack with Dielectric) [Tempest Strike] + Amplified (15s) [Flow State] + Ionic Trace [Shock and Clear] + +1 Bolt Charge [Dielectric]
Pick up Ionic Trace -> +1 Bolt Charge [Spark of Discharge] + +1 Armor Charge [Elemental Charge] (chance) + +11.3% grenade and melee energy and +13.5% class ability energy [Ionic Trace]
Gain Bolt Charge -> +?% grenade energy [Shinobu's Vow] + +2.5% melee energy [Bolt Charge]
Max Bolt Charge (x10) -> New Tricks and +~40% grenade energy and heals you and allies [Shinobu's Vow] + Amplified (15s) [Flashover]
While Amplified -> +1 Bolt Charge per gain [Spark of Frequency] + Linear Fusion Rifles, Fusion Rifles and Heat Weapons: +? Handling, +? Reload [Ionic Overclock]
```

`trace` / `play`: a step, cascades indented, then the state (`--state`); then a state you declare
(`max:bolt-charge`):

```text
#2 Grenade (kill)
  Grenade hit → Jolt target (10s) [Spark of Shock] + +1 Bolt Charge and +4.2% grenade energy [Shinobu's Vow]
    ↳ Bolt Charge gained → +?% grenade energy [Shinobu's Vow] + +2.5% melee energy [Bolt Charge]
  Grenade kill on Jolted target → Amplified (15s) [Flow State] + +1 Bolt Charge [Dielectric] + Ionic Trace [Shock and Clear] + doesn't stack with Dielectric [Tempest Strike]
    ↳ Bolt Charge gained → +?% grenade energy [Shinobu's Vow] + +2.5% melee energy [Bolt Charge]
    ↳ Picked up Ionic Trace → +1 Bolt Charge [Spark of Discharge] + +1 Armor Charge [Elemental Charge] (chance) + +11.3% grenade and melee energy and +13.5% class ability energy [Ionic Trace]
      ↳ Bolt Charge gained → +?% grenade energy [Shinobu's Vow] + +2.5% melee energy [Bolt Charge]
  Buffs         Reaper · Bolt Charge · Amplified · Armor Charge
  Weapons       Slice
  Debuffs       Jolt
  Ground        none
  While Amplified  +1 Bolt Charge per gain [Spark of Frequency]
#3 Bolt Charge at max (x10)
  Max Bolt Charge → New Tricks and +~40% grenade energy and heals you and allies [Shinobu's Vow] + Amplified (15s) [Flashover]
  Buffs         Reaper · Bolt Charge (at max) · Amplified · Armor Charge · New Tricks
  Weapons       Slice
  Debuffs       Jolt
  Ground        none
  While Amplified  +1 Bolt Charge per gain [Spark of Frequency]
```

`loop` on a short loop (a new pack, dodge, shoot, pick up the orb):

```text
Dodge, then shoot — Skip Grenade Hunter · 4 steps
New pack → Class ability → Festival Flight (kill) → Pick up Orb of Power
✓ Repeats — each pass ends with what the next one needs

#1 New pack

#2 Class ability
   on its own       Reaper · Slice · Gambler's Dodge · Bomber

#3 Festival Flight (kill)
   needs            Slice ← #2 [Slice] · Unraveling Rounds ← previous pass #4 [Unraveling Orbs] · Reaper ← #2 [Reaper]
   in this order    Slice · Unraveling Rounds · Reaper · To Shreds
   on its own       Attrition Orbs (chance) · Strand Siphon (chance)

#4 Pick up Orb of Power
   needs            Orb of Power ← #3 [Attrition Orbs (chance); Reaper; Strand Siphon (chance)]
   in this order    Unraveling Orbs · Orb of Power

First pass (from a fresh spawn, where it differs)
#3 Festival Flight (kill)
   doesn't set off  Unraveling Rounds
```

`compare` of that loop with the same steps in another order — the shot before the dodge gets Slice
and Reaper from the previous pass (`…` elides):

```text
A  Dodge, then shoot   New pack → Class ability → Festival Flight (kill) → Pick up Orb of Power   ✓ Repeats   (Skip Grenade Hunter)
B  Shoot, then dodge   New pack → Festival Flight (kill) → Class ability → Pick up Orb of Power   ✓ Repeats   (Skip Grenade Hunter)
…
Festival Flight (kill)  A #3 · B #2: sets off the same
   A needs     Slice ← #2 · Unraveling Rounds ← previous pass #4 · Reaper ← #2 (differs on A's first pass)
   B needs     Slice ← previous pass #3 · Unraveling Rounds ← previous pass #4 · Reaper ← previous pass #3 (differs on B's first pass)
…
Links only in A
   #2 Class ability → #3 Festival Flight (kill): Slice · Reaper

Links only in B
   previous pass #3 Class ability → #2 Festival Flight (kill): Slice · Reaper
```

`?` = the source doesn't say (shown, never treated as 0) · `~` = approximate ·
`(chance)` = may not happen in game; it always fires here and is marked ([ADRs D8](../../ADRs.md)) ·
`doesn't stack with X` = this rule gives nothing when X's fires on the same event
([ADRs D6](../../ADRs.md)); `loop` lists it as wasted · `Reaper ← #2 [Reaper]` = this step needs Reaper,
which step #2 gave (from the element in brackets) · *in this order* = fires because of an earlier step,
*on its own* = fires wherever the step goes. Abilities are always available; an energy outcome
is a fact about what gives energy back ([ADRs D5](../../ADRs.md)). The loop graph renders on GitHub:
[builds/skip-grenade-hunter/loop-graph.md](../../builds/skip-grenade-hunter/loop-graph.md).

Action tokens: `grenade[:hold]|melee[:slide|air]|super[:hit|kill[:N]]`, `class[:air]`, `kinetic|energy|power[:hit|kill[:N]]`, `finisher`,
`pickup:<pickup-id>`, `max:<status-id>`, `end:<status-id>`, `pack:new` — `N` is how many enemies that one action
hits or kills (1..20, default 1): `grenade:kill:3`, `kinetic:hit:5` ([ADRs D4](../../ADRs.md)); a secondary ability use
goes before the hit — `melee:slide:kill` is Tempest Strike's sliding uppercut, `class:air` Ascension's air move,
`grenade:hold` a held grenade ([ADRs D9](../../ADRs.md)); `finisher` finishes one enemy of
the pack (it kills it); `pickup:orb-of-power`
picks up an orb that is on the ground; `max:bolt-charge` declares Bolt Charge at max, `end:amplified`
that Amplified has ended, and `pack:new` that the next enemies are a new pack ([ADRs D3, D7](../../ADRs.md)). A step that can't happen is blocked and changes nothing
(`No orb-of-power on the ground — nothing happens.`).
