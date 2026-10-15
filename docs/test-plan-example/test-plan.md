# PR #79: enemy-count test plan

Preview: https://joancomasfdz.github.io/destiny-loopsmith/pr-preview/pr-79/ · written by the rules in
[given-when-then-test-rules.md](../given-when-then-test-rules.md). The builds are in this folder, next to this plan.

This plan covers only what PR #79 changes about enemy counts:

- what each weapon's enemy-count dropdown offers;
- which count fires a perk for sure, which as a chance, and which not at all;
- hits against kills;
- "this weapon" only, not another one;
- two rules that need the same count.

Every test was played exactly as written, on desktop and at 390 px, and every quoted text is copied from the screen.

## Shared steps

**Import a build (build NN).**

1. In the designer, click **Change build** at the top. You're on Home.
2. At the bottom of Home, in the line "Or start from a build written by hand: … · **import a build.yaml**", click
   **import a build.yaml**.
3. Pick the file `NN-….build.yaml`.
4. If the browser asks "Start a new loop from this build?", click **OK**.
5. The designer opens. The **LOOP NAME** field reads "PR 79 - Build NN: … loop", and the LOOP heading reads
   "LOOP · 0 STEPS".

You don't need "Start from scratch": importing replaces whatever is open.

**The trigger panel** is the **NEXT TRIGGER** box below the steps. Its weapons are the wide tiles on the right, one per
weapon, each with the weapon's name ("Festival Flight").

- Tapping a weapon's tile opens a row under it: the weapon's name, **🥊 hit**, **💀 kill**, and after **×** the
  enemy-count dropdown.
- Tapping **🥊 hit** or **💀 kill** adds a step to the end of the LOOP list, and the step's card opens.

**A step's card** shows, in this order:

1. its heading, e.g. "#1 Festival Flight (🥊 hit)";
2. what it fired;
3. **STATE AFTER #N**, with four groups: **BUFFS**, **WEAPONS**, **DEBUFFS** and **GROUND**. Each group shows its chips,
   or "none".

**Remove a step:** click the **✕** at the top right of its card (its tooltip says "Remove this step"). There's no
confirmation. Every round below starts from an empty loop, so each step shows only what its own clicks did.

**"(chance)"** at the end of a line means that outcome may happen. A line without it happens for sure.

---

## Build 01: One for All (`01-one-for-all.build.yaml`)

Festival Flight has One for All. Ogma PR6 has no perk: it plays "another weapon".

### T1 · Only Festival Flight's dropdown names One for All's count

**Given** build 01 is imported, with no steps

**When**

1. I tap **Festival Flight** in the trigger panel.
2. I open the **×** dropdown.

**Then**

- The dropdown offers exactly these options, in this order:
  - "1 enemy"
  - "3 in 3 s · One for All"

**When**

3. I tap **Ogma PR6** in the trigger panel.
4. I open the **×** dropdown.

**Then**

- The dropdown offers only "1 enemy".

### T2 · One for All: a chance on 1 hit, sure on 3 hits and on a kill of 3, nothing from another weapon

**Given** build 01 is imported, with no steps

**When**

1. I tap **Festival Flight** in the trigger panel.
2. I select "1 enemy".
3. I tap **🥊 hit**.

**Then**

- The new card's heading is "#1 Festival Flight (🥊 hit)".
- It reads "Festival Flight 🥊 hit 1 enemy".
- Under "WEAPON PERK · FESTIVAL FLIGHT" / "One for All":
  - "One for All (10s) (chance)"
  - "+35% kinetic weapon damage (chance)"
- STATE AFTER #1:
  - BUFFS: none
  - WEAPONS: the chip "One for All · Festival Flight" (hover: "10s")
  - DEBUFFS: none
  - GROUND: none

**When**

4. I remove step #1 with the **✕** at the top right of its card.

**Then**

- The LOOP heading reads "LOOP · 0 STEPS", and the list shows only "#0 Fresh spawn".

**When**

5. I tap **Festival Flight** in the trigger panel.
6. I select "3 in 3 s · One for All".
7. I tap **🥊 hit**.

**Then**

- The new card's heading is "#1 Festival Flight (🥊 hit 3)".
- It reads "Festival Flight 🥊 hit 3 enemies in 3 s".
- Under "One for All", neither with "(chance)":
  - "One for All (10s)"
  - "+35% kinetic weapon damage"
- STATE AFTER #1:
  - BUFFS: none
  - WEAPONS: "One for All · Festival Flight"
  - DEBUFFS: none
  - GROUND: none

**When**

8. I remove step #1 with the **✕** at the top right of its card.

**Then**

- The LOOP heading reads "LOOP · 0 STEPS", and the list shows only "#0 Fresh spawn".

**When**

9. I tap **Festival Flight** in the trigger panel.
10. I select "3 in 3 s · One for All".
11. I tap **💀 kill**.

**Then**

- The new card's heading is "#1 Festival Flight (💀 kill 3)".
- It reads "Festival Flight 💀 killed 3 enemies in 3 s".
- Under "One for All", neither with "(chance)":
  - "One for All (10s)"
  - "+35% kinetic weapon damage"
- STATE AFTER #1:
  - BUFFS: none
  - WEAPONS: "One for All · Festival Flight"
  - DEBUFFS: none
  - GROUND: none

**When**

12. I remove step #1 with the **✕** at the top right of its card.

**Then**

- The LOOP heading reads "LOOP · 0 STEPS", and the list shows only "#0 Fresh spawn".

**When**

13. I tap **Ogma PR6** in the trigger panel.
14. I select "1 enemy".
15. I tap **🥊 hit**.

**Then**

- The new card's heading is "#1 Ogma PR6 (🥊 hit)".
- It reads "nothing triggers".
- STATE AFTER #1:
  - BUFFS: none
  - WEAPONS: none
  - DEBUFFS: none
  - GROUND: none

## Build 02: Stats for All (`02-stats-for-all.build.yaml`)

### T3 · The dropdown names Stats for All's count

**Given** build 02 is imported, with no steps

**When**

1. I tap **Ogma PR6** in the trigger panel.
2. I open the **×** dropdown.

**Then**

- The dropdown offers exactly these options, in this order:
  - "1 enemy"
  - "3 in 3 s · Stats for All"

### T4 · Stats for All: a chance on 1 hit, sure on 3; one chip with its stats

**Given** build 02 is imported, with no steps

**When**

1. I tap **Ogma PR6** in the trigger panel.
2. I select "1 enemy".
3. I tap **🥊 hit**.

**Then**

- The new card's heading is "#1 Ogma PR6 (🥊 hit)".
- It reads "Ogma PR6 🥊 hit 1 enemy".
- Under "WEAPON PERK · OGMA PR6" / "Stats for All": "Stats for All (10s) (chance)".
- STATE AFTER #1:
  - BUFFS: none
  - WEAPONS: one chip, "Stats for All · 1 effect · Ogma PR6"; there is no second "Stats for All" chip; hovering it shows "10s · Energy weapon: +10 Range, +10 Stability, +35 Handling, +35 Reload Speed"
  - DEBUFFS: none
  - GROUND: none

**When**

4. I remove step #1 with the **✕** at the top right of its card.

**Then**

- The LOOP heading reads "LOOP · 0 STEPS", and the list shows only "#0 Fresh spawn".

**When**

5. I tap **Ogma PR6** in the trigger panel.
6. I select "3 in 3 s · Stats for All".
7. I tap **🥊 hit**.

**Then**

- The new card's heading is "#1 Ogma PR6 (🥊 hit 3)".
- It reads "Ogma PR6 🥊 hit 3 enemies in 3 s".
- Under "Stats for All": "Stats for All (10s)", without "(chance)".
- STATE AFTER #1:
  - BUFFS: none
  - WEAPONS: one chip, "Stats for All · 1 effect · Ogma PR6"; there is no second "Stats for All" chip; hovering it shows "10s · Energy weapon: +10 Range, +10 Stability, +35 Handling, +35 Reload Speed"
  - DEBUFFS: none
  - GROUND: none

## Build 03: Reaper's Tithe (`03-reapers-tithe.build.yaml`)

### T5 · The dropdown offers each of Reaper's Tithe's counts

**Given** build 03 is imported, with no steps

**When**

1. I tap **Bellowing Giant** in the trigger panel.
2. I open the **×** dropdown.

**Then**

- The dropdown offers exactly these options, in this order:
  - "1 enemy"
  - "2 · Reaper's Tithe"
  - "3 · Reaper's Tithe"
  - "4 · Reaper's Tithe · Reaper's Tithe x4"

**Note:** "· Reaper's Tithe x4" says the buff reaches its cap at 4 kills.

### T6 · Kills at once: 1 gives nothing, 2, 3 and 4 give that many stacks; hits give nothing

**Given** build 03 is imported, with no steps

**When**

1. I tap **Bellowing Giant** in the trigger panel.
2. I select "1 enemy".
3. I tap **💀 kill**.

**Then**

- The new card's heading is "#1 Bellowing Giant (💀 kill)".
- It reads "nothing triggers". There's no "(chance)" either: kills "at once" can't come from earlier steps.
- STATE AFTER #1:
  - BUFFS: none
  - WEAPONS: none
  - DEBUFFS: none
  - GROUND: none

**When**

4. I remove step #1 with the **✕** at the top right of its card.

**Then**

- The LOOP heading reads "LOOP · 0 STEPS", and the list shows only "#0 Fresh spawn".

**When**

5. I tap **Bellowing Giant** in the trigger panel.
6. I select "2 · Reaper's Tithe".
7. I tap **💀 kill**.

**Then**

- The new card's heading is "#1 Bellowing Giant (💀 kill 2)".
- It reads "Bellowing Giant 💀 killed 2 enemies", with no "in … s".
- Under "WEAPON PERK · BELLOWING GIANT" / "Reaper's Tithe", neither with "(chance)":
  - "+2 Reaper's Tithe (15s)"
  - "+20% heavy weapon damage"
- STATE AFTER #1:
  - BUFFS: none
  - WEAPONS: "Reaper's Tithe · Bellowing Giant" (hover: "up to x4 · 15s")
  - DEBUFFS: none
  - GROUND: none

**When**

8. I remove step #1 with the **✕** at the top right of its card.

**Then**

- The LOOP heading reads "LOOP · 0 STEPS", and the list shows only "#0 Fresh spawn".

**When**

9. I tap **Bellowing Giant** in the trigger panel.
10. I select "3 · Reaper's Tithe".
11. I tap **💀 kill**.

**Then**

- The new card's heading is "#1 Bellowing Giant (💀 kill 3)".
- It reads "Bellowing Giant 💀 killed 3 enemies", with no "in … s".
- Under "WEAPON PERK · BELLOWING GIANT" / "Reaper's Tithe", neither with "(chance)":
  - "+3 Reaper's Tithe (15s)"
  - "+30% heavy weapon damage"
- STATE AFTER #1:
  - BUFFS: none
  - WEAPONS: "Reaper's Tithe · Bellowing Giant" (hover: "up to x4 · 15s")
  - DEBUFFS: none
  - GROUND: none

**When**

12. I remove step #1 with the **✕** at the top right of its card.

**Then**

- The LOOP heading reads "LOOP · 0 STEPS", and the list shows only "#0 Fresh spawn".

**When**

13. I tap **Bellowing Giant** in the trigger panel.
14. I select "4 · Reaper's Tithe · Reaper's Tithe x4".
15. I tap **💀 kill**.

**Then**

- The new card's heading is "#1 Bellowing Giant (💀 kill 4)".
- It reads "Bellowing Giant 💀 killed 4 enemies", with no "in … s".
- Under "WEAPON PERK · BELLOWING GIANT" / "Reaper's Tithe", neither with "(chance)":
  - "+4 Reaper's Tithe (15s)"
  - "+40% heavy weapon damage"
- STATE AFTER #1:
  - BUFFS: none
  - WEAPONS: "Reaper's Tithe · Bellowing Giant" (hover: "up to x4 · 15s")
  - DEBUFFS: none
  - GROUND: none

**When**

16. I remove step #1 with the **✕** at the top right of its card.

**Then**

- The LOOP heading reads "LOOP · 0 STEPS", and the list shows only "#0 Fresh spawn".

**When**

17. I tap **Bellowing Giant** in the trigger panel.
18. I select "2 · Reaper's Tithe".
19. I tap **🥊 hit**.

**Then**

- The new card's heading is "#1 Bellowing Giant (🥊 hit 2)".
- It reads "nothing triggers": Reaper's Tithe needs kills.
- STATE AFTER #1:
  - BUFFS: none
  - WEAPONS: none
  - DEBUFFS: none
  - GROUND: none

**When**

20. I remove step #1 with the **✕** at the top right of its card.

**Then**

- The LOOP heading reads "LOOP · 0 STEPS", and the list shows only "#0 Fresh spawn".

**When**

21. I tap **Bellowing Giant** in the trigger panel.
22. I select "4 · Reaper's Tithe · Reaper's Tithe x4".
23. I tap **🥊 hit**.

**Then**

- The new card's heading is "#1 Bellowing Giant (🥊 hit 4)".
- It reads "nothing triggers": Reaper's Tithe needs kills.
- STATE AFTER #1:
  - BUFFS: none
  - WEAPONS: none
  - DEBUFFS: none
  - GROUND: none

## Build 04: two rules that need the same count (`04-shared-count.build.yaml`)

Crux Celeritas IV (a Strand rocket launcher) has Reaper's Tithe, and the build wears the Strand Siphon mod, which needs
2 Strand weapon kills within 3 s.

### T7 · One count names both rules, each with its own window

**Given** build 04 is imported, with no steps

**When**

1. I tap **Crux Celeritas IV** in the trigger panel.
2. I open the **×** dropdown.

**Then**

- The dropdown offers exactly these options, in this order:
  - "1 enemy"
  - "2 · Strand Siphon in 3 s, Reaper's Tithe"
  - "3 · Reaper's Tithe"
  - "4 · Reaper's Tithe · Reaper's Tithe x4"

### T8 · 1 kill gives only Strand Siphon's chance; 2 kills give both, for sure

**Given** build 04 is imported, with no steps

**When**

1. I tap **Crux Celeritas IV** in the trigger panel.
2. I select "1 enemy".
3. I tap **💀 kill**.

**Then**

- The new card's heading is "#1 Crux Celeritas IV (💀 kill)".
- It reads "Crux Celeritas IV 💀 killed 1 enemy".
- Under "ARMOR MOD · HELMET" / "Strand Siphon": "Orb of Power (chance)".
- There's no Reaper's Tithe line.
- STATE AFTER #1:
  - BUFFS: none
  - WEAPONS: none
  - DEBUFFS: none
  - GROUND: "Orb of Power (chance)"

**When**

4. I remove step #1 with the **✕** at the top right of its card.

**Then**

- The LOOP heading reads "LOOP · 0 STEPS", and the list shows only "#0 Fresh spawn".

**When**

5. I tap **Crux Celeritas IV** in the trigger panel.
6. I select "2 · Strand Siphon in 3 s, Reaper's Tithe".
7. I tap **💀 kill**.

**Then**

- The new card's heading is "#1 Crux Celeritas IV (💀 kill 2)".
- It reads "Crux Celeritas IV 💀 killed 2 enemies in 3 s".
- Under "WEAPON PERK · CRUX CELERITAS IV" / "Reaper's Tithe":
  - "+2 Reaper's Tithe (15s)"
  - "+20% heavy weapon damage"
- Under "ARMOR MOD · HELMET" / "Strand Siphon": "Orb of Power".
- No line says "(chance)".
- STATE AFTER #1:
  - BUFFS: none
  - WEAPONS: "Reaper's Tithe · Crux Celeritas IV" (hover: "up to x4 · 15s")
  - DEBUFFS: none
  - GROUND: "Orb of Power", without "(chance)"

## Build 05: Hatchling (`05-hatchling.build.yaml`)

### T9 · The dropdown names Hatchling's count

**Given** build 05 is imported, with no steps

**When**

1. I tap **Festival Flight** in the trigger panel.
2. I open the **×** dropdown.

**Then**

- The dropdown offers exactly these options, in this order:
  - "1 enemy"
  - "3 in 3 s · Hatchling"

### T10 · Hits never hatch; 1 kill hatches as a chance, 3 kills for sure

**Given** build 05 is imported, with no steps

In this test the four state groups stay at "none": a Threadling acts at once and isn't kept in the state.

**When**

1. I tap **Festival Flight** in the trigger panel.
2. I select "1 enemy".
3. I tap **🥊 hit**.

**Then**

- The new card's heading is "#1 Festival Flight (🥊 hit)".
- It reads "nothing triggers".
- STATE AFTER #1: BUFFS, WEAPONS, DEBUFFS and GROUND are all "none".

**When**

4. I remove step #1 with the **✕** at the top right of its card.

**Then**

- The LOOP heading reads "LOOP · 0 STEPS", and the list shows only "#0 Fresh spawn".

**When**

5. I tap **Festival Flight** in the trigger panel.
6. I select "3 in 3 s · Hatchling".
7. I tap **🥊 hit**.

**Then**

- The new card's heading is "#1 Festival Flight (🥊 hit 3)".
- It reads "nothing triggers".
- STATE AFTER #1: all "none".

**When**

8. I remove step #1 with the **✕** at the top right of its card.

**Then**

- The LOOP heading reads "LOOP · 0 STEPS", and the list shows only "#0 Fresh spawn".

**When**

9. I tap **Festival Flight** in the trigger panel.
10. I select "1 enemy".
11. I tap **💀 kill**.

**Then**

- The new card's heading is "#1 Festival Flight (💀 kill)".
- It reads "Festival Flight 💀 killed 1 enemy".
- Under "WEAPON PERK · FESTIVAL FLIGHT" / "Hatchling": "Threadling (chance)".
- STATE AFTER #1: all "none".

**When**

12. I remove step #1 with the **✕** at the top right of its card.

**Then**

- The LOOP heading reads "LOOP · 0 STEPS", and the list shows only "#0 Fresh spawn".

**When**

13. I tap **Festival Flight** in the trigger panel.
14. I select "3 in 3 s · Hatchling".
15. I tap **💀 kill**.

**Then**

- The new card's heading is "#1 Festival Flight (💀 kill 3)".
- It reads "Festival Flight 💀 killed 3 enemies in 3 s".
- Under "Hatchling": "Threadling", without "(chance)".
- STATE AFTER #1: all "none".

## Build 06: Hatchling, enhanced (`06-hatchling-enhanced.build.yaml`)

### T11 · The dropdown names the enhanced trait's count

**Given** build 06 is imported, with no steps

**When**

1. I tap **Festival Flight** in the trigger panel.
2. I open the **×** dropdown.

**Then**

- The dropdown offers exactly these options, in this order:
  - "1 enemy"
  - "3 in 3 s · Hatchling (enhanced)"

### T12 · The enhanced trait adds a second Threadling, always as a chance

**Given** build 06 is imported, with no steps

**When**

1. I tap **Festival Flight** in the trigger panel.
2. I select "1 enemy".
3. I tap **💀 kill**.

**Then**

- The new card's heading is "#1 Festival Flight (💀 kill)".
- It reads "Festival Flight 💀 killed 1 enemy".
- Under "WEAPON PERK · FESTIVAL FLIGHT" / "Hatchling (enhanced)", the same line twice:
  - "Threadling (chance)"
  - "Threadling (chance)"
- STATE AFTER #1: all "none".

**When**

4. I tick the **why** checkbox at the top right of the LOOP list.

**Then**

- Below the two lines, card #1 shows two reasons:
  - "why: Fewer than three kills in this step: a precision kill, or the kills of the steps before, may hatch it."
  - "why: When it hatches one, the enhanced trait may hatch a second."

**Note:** the two "(chance)" lines are two different Threadlings, each with its own reason.

- The first is the Threadling every Hatchling gives. On 1 kill it's a chance, because the perk needs a precision kill
  or 3 kills.
- The second is the extra one only the enhanced trait gives, at a chance the Compendium gives as "10?%".

**When**

5. I remove step #1 with the **✕** at the top right of its card.

**Then**

- The LOOP heading reads "LOOP · 0 STEPS", and the list shows only "#0 Fresh spawn".

**When**

6. I tap **Festival Flight** in the trigger panel.
7. I select "3 in 3 s · Hatchling (enhanced)".
8. I tap **💀 kill**.

**Then**

- The new card's heading is "#1 Festival Flight (💀 kill 3)".
- It reads "Festival Flight 💀 killed 3 enemies in 3 s".
- Under "Hatchling (enhanced)":
  - "Threadling", without "(chance)"
  - "Threadling (chance)"
- With **why** still ticked, the reasons are:
  - "why: Three quick kills with this weapon hatch a Threadling."
  - "why: When it hatches one, the enhanced trait may hatch a second."
- STATE AFTER #1: all "none".

## Build 07: Unrelenting (`07-unrelenting.build.yaml`)

### T13 · The dropdown names Unrelenting's count and window

**Given** build 07 is imported, with no steps

**When**

1. I tap **Ogma PR6** in the trigger panel.
2. I open the **×** dropdown.

**Then**

- The dropdown offers exactly these options, in this order:
  - "1 enemy"
  - "3 in 5 s · Unrelenting"

### T14 · 1 kill heals as a chance, 3 kills heal 65 HP for sure

**Given** build 07 is imported, with no steps

**When**

1. I tap **Ogma PR6** in the trigger panel.
2. I select "1 enemy".
3. I tap **💀 kill**.

**Then**

- The new card's heading is "#1 Ogma PR6 (💀 kill)".
- It reads "Ogma PR6 💀 killed 1 enemy".
- Under "WEAPON PERK · OGMA PR6" / "Unrelenting":
  - "heals you 65 HP (chance)"
- STATE AFTER #1: all "none".

**When**

4. I remove step #1 with the **✕** at the top right of its card.

**Then**

- The LOOP heading reads "LOOP · 0 STEPS", and the list shows only "#0 Fresh spawn".

**When**

5. I tap **Ogma PR6** in the trigger panel.
6. I select "3 in 5 s · Unrelenting".
7. I tap **💀 kill**.

**Then**

- The new card's heading is "#1 Ogma PR6 (💀 kill 3)".
- It reads "Ogma PR6 💀 killed 3 enemies in 5 s".
- Under "Unrelenting", without "(chance)":
  - "heals you 65 HP"
- STATE AFTER #1: all "none".

## Build 08: Veteran's Wisdom (`08-veterans-wisdom.build.yaml`)

### T15 · The dropdown names Veteran's Wisdom's count

**Given** build 08 is imported, with no steps

**When**

1. I tap **Scathelocke** in the trigger panel.
2. I open the **×** dropdown.

**Then**

- The dropdown offers exactly these options, in this order:
  - "1 enemy"
  - "3 in 3 s · Veteran's Wisdom"

### T16 · 1 kill sets off the shockwave as a chance, 3 kills for sure

**Given** build 08 is imported, with no steps

**When**

1. I tap **Scathelocke** in the trigger panel.
2. I select "1 enemy".
3. I tap **💀 kill**.

**Then**

- The new card's heading is "#1 Scathelocke (💀 kill)".
- It reads "Scathelocke 💀 killed 1 enemy".
- Under "WEAPON PERK · SCATHELOCKE" / "Veteran's Wisdom":
  - "Exhaust target (10s) (chance)"
  - "kinetic weapon strike, Kinetic damage (chance)"
- STATE AFTER #1:
  - BUFFS: none
  - WEAPONS: none
  - DEBUFFS: "Exhaust" (hover: "10s")
  - GROUND: none

**When**

4. I remove step #1 with the **✕** at the top right of its card.

**Then**

- The LOOP heading reads "LOOP · 0 STEPS", and the list shows only "#0 Fresh spawn".

**When**

5. I tap **Scathelocke** in the trigger panel.
6. I select "3 in 3 s · Veteran's Wisdom".
7. I tap **💀 kill**.

**Then**

- The new card's heading is "#1 Scathelocke (💀 kill 3)".
- It reads "Scathelocke 💀 killed 3 enemies in 3 s".
- Under "Veteran's Wisdom", without "(chance)":
  - "Exhaust target (10s)"
  - "kinetic weapon strike, Kinetic damage"
- STATE AFTER #1:
  - BUFFS: none
  - WEAPONS: none
  - DEBUFFS: "Exhaust" (hover: "10s")
  - GROUND: none

## Build 09: Radiolaria Transposer (`09-radiolaria-transposer.build.yaml`)

### T17 · The dropdown names Radiolaria Transposer's count and window

**Given** build 09 is imported, with no steps

**When**

1. I tap **Speleologist** in the trigger panel.
2. I open the **×** dropdown.

**Then**

- The dropdown offers exactly these options, in this order:
  - "1 enemy"
  - "3 in 8 s · Radiolaria Transposer"

### T18 · 1 kill makes the two Arc strikes as a chance, 3 kills for sure

**Given** build 09 is imported, with no steps

**When**

1. I tap **Speleologist** in the trigger panel.
2. I select "1 enemy".
3. I tap **💀 kill**.

**Then**

- The new card's heading is "#1 Speleologist (💀 kill)".
- It reads "Speleologist 💀 killed 1 enemy".
- Under "WEAPON PERK · SPELEOLOGIST" / "Radiolaria Transposer":
  - "heavy weapon strike, Arc damage (chance)" (the explosion)
  - "heavy weapon strike, Arc damage (chance)" (the pool)
- STATE AFTER #1: all "none".

**When**

4. I remove step #1 with the **✕** at the top right of its card.

**Then**

- The LOOP heading reads "LOOP · 0 STEPS", and the list shows only "#0 Fresh spawn".

**When**

5. I tap **Speleologist** in the trigger panel.
6. I select "3 in 8 s · Radiolaria Transposer".
7. I tap **💀 kill**.

**Then**

- The new card's heading is "#1 Speleologist (💀 kill 3)".
- It reads "Speleologist 💀 killed 3 enemies in 8 s".
- Under "Radiolaria Transposer", without "(chance)":
  - "heavy weapon strike, Arc damage" (the explosion)
  - "heavy weapon strike, Arc damage" (the pool)
- STATE AFTER #1: all "none".

---

## Notes

- **The dropdown offers only counts that matter:** "1 enemy", the counts a rule needs, and counts that fill something.
  So the case "fewer than the count" is always played with "1 enemy".
- **"kinetic weapon", "energy weapon", "heavy weapon"** is how a perk's "this weapon" reads, by the weapon's slot
  (the weapon mods already read this way).
