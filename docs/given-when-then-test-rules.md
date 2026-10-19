# How to write a Gherkin

A Gherkin is a Given/When/Then test of the app, written so that anyone can play it click by click, with nothing to
work out on their own. These rules apply to every Gherkin a session writes. A Gherkin is two things at once:

- **an acceptance criterion** of a pull request: every criterion about how the app behaves is one (section 1);
- **one end-to-end test**: each Gherkin is exactly one C# scenario in `tests/Loopsmith.Web.E2E`, which holds the
  Gherkin word for word in its `[TestDescription]` and does what it says, line by line. How a Gherkin becomes C# is
  [E2E-TEST-CODE-RULES.md](../tests/Loopsmith.Web.E2E/E2E-TEST-CODE-RULES.md), rule i. The VS Code extension
  (`src/Loopsmith.VsCodeExtension`) follows the same rules: each Gherkin about what it shows in VS Code (its CodeLens,
  play button and recording panel) is one scenario in `tests/Loopsmith.VsCodeExtension.E2E`, played in VS Code for the web on
  the desktop only (its
  [E2E-TEST-CODE-RULES.md](../tests/Loopsmith.VsCodeExtension.E2E/E2E-TEST-CODE-RULES.md)), and its PR shows its GIFs
  the same way.

The PR's e2e comment shows each scenario's title, the flow of the rule it names (a rule scenario's `# Rule` line,
drawn by the rule viewer), its Gherkin, and a GIF of it played on each screen its tags name
(🖥️ Desktop, 📱 Mobile) ([hosting.md](hosting.md), "E2E videos on pull requests").

This document holds what every Gherkin shares. Two kinds of scenario build on it, each with its own document for what
differs (its scope, the order of its rounds and its worked example):

- **[Loop scenarios](given-when-then-loop-scenarios.md)** (`tests/Loopsmith.Web.E2E/Scenarios/`): how the designer
  behaves, the subject a pull request changes.
- **[Rule scenarios](given-when-then-rule-scenarios.md)** (`tests/Loopsmith.Web.E2E/Rules/`): one scenario per claim of
  a rule, grouped like the rules (ADRs D40).

## Example

Each kind has its worked example: [EnemyCountTests.cs](../tests/Loopsmith.Web.E2E/Scenarios/Loop/Weapons/EnemyCount/EnemyCountTests.cs)
for a loop scenario ([its document](given-when-then-loop-scenarios.md)), and
[LethalCurrentTests.cs](../tests/Loopsmith.Web.E2E/Rules/Hunter/Arc/Aspects/LethalCurrent/LethalCurrentTests.cs) for a rule scenario
([its document](given-when-then-rule-scenarios.md)).

## 1. What is a Gherkin

1. **Every acceptance criterion about how the app behaves is a Gherkin:** what someone does and sees on Home or in the
   designer. A criterion checked in a file (a doc, a rules YAML, a test, a workflow) stays a plain line.
2. **Layout is a Gherkin too**, checked from where things are on the screen: a row wraps, two parts don't overlap, a
   button sits at a row's right end, a row fits inside the screen with no sideways scroll, a text isn't cut off. "Looks right",
   colours and spacing are never a Gherkin: nothing checks them but an eye.
3. **Not yet:** what needs a Bungie login (no e2e scenario can reach it yet). Such a criterion stays
   a plain line, checked by hand on the preview, and says why.

## 2. Scope

What a Gherkin covers depends on its kind: a loop scenario tests what the PR changes and every case that can change
its result ([loop scenarios](given-when-then-loop-scenarios.md), "Scope"); a rule scenario tests one claim of a rule,
whatever the PR changes ([rule scenarios](given-when-then-rule-scenarios.md), "Scope").

## 3. The shape

```gherkin
@desktop @mobile
Scenario: <what it proves, as a statement>
  Given <the build, and the state of the loop>
  When <one action>
  And <one action>
  Then <one check>
  And <one check>
  When …
  Then …
```

1. **Tags:** the screens it is played on, `@desktop` and/or `@mobile`, on the line before `Scenario:`. Both, unless
   the subject can't apply to one: a tooltip only shows on hover (desktop), a phone's layout only on a phone (mobile).
   A Gherkin with one screen says why in a `#` line right under its `Scenario:` line.
2. **Title:** the `Scenario:` line, one line, a statement ("Reaper's Tithe gives a stack per enemy killed at once").
   It is also the test's method name (each run of letters and digits a word, an apostrophe joining its word,
   capitalized: `ReapersTitheGivesAStackPerEnemyKilledAtOnce`), so it starts with a letter and reads well run
   together.
3. **Given:** the build ("Given the build "ogma-pr6-unrelenting" is imported, with no steps", its fixture's name
   without `.build.yaml`) and the state of the loop: "with no steps", or the steps the test needs already played (each
   one named). Or "Given the app is open on Home".
4. **One Gherkin per Given.** Checks that start from the same build and the same steps are one Gherkin: each becomes a
   round of it. Split only where the Given differs.
5. **Rounds of When and Then.** A Gherkin is a sequence of rounds. Each round:
   - **When** is the actions that lead to one thing to look at: the first line `When`, the next ones `And`;
   - **Then** is what to look at, right after those actions: the first line `Then`, the next ones `And`.

   Act, then check, then act again. A round never checks something its actions haven't produced yet, and never saves
   all the checks for the end.

## 4. When

1. **One action per line,** in the first person, with the control's exact on-screen name in quotes:
   - `I tap "Festival Flight" in the trigger panel`
   - `I select "3 in 3 s · One for All"`
   - `I tap "💀 kill"`
   - `I tick "why" above the loop`
   - `I remove step #1 with the "✕" on its card`

   "Tap" is a click on desktop and a tap on a phone.
2. **Select by the option's exact text,** never by position ("pick the second") and never by default ("leave 1
   enemy"). Write `I select "1 enemy"` even when it is already selected.
3. **Keep checking and doing apart.** "The dropdown offers…" is a Then. Selecting is a When. They are never one line.

## 5. Then

1. **One check per line.**
2. **Quote the exact on-screen text,** copied from the screen while playing it: a heading ("#1 Ogma PR6 (💀 kill)"),
   a line ("heals you 65 HP (chance)"), an option ("3 in 5 s · Unrelenting"), a chip ("Exhaust").
3. **Check only what the subject changes.** A state group (BUFFS, WEAPONS, DEBUFFS, GROUND) is checked when the subject
   puts something there ("its WEAPONS show only "One for All · Festival Flight""), or when its absence matters ("its
   WEAPONS show none" after another weapon's hit). The rest of the card is left out.
4. **Say what must not be there** when it matters: `"Reaper's Tithe" fires nothing`, `no line says "(chance)"`, `its
   WEAPONS show none`, `the "×" enemy count offers only "1 enemy"`. The test checks it too.
5. **A tooltip's text** is a check of its own and makes the Gherkin desktop only. A state chip's effects are read in its
   details instead, on both screens: "When I tap the "Stats for All · 1 effect · Ogma PR6" chip in card #1's WEAPONS",
   then "its "Right then, after #1" card reads only "10s" and …" (the same lines as its tooltip).
6. **Explain anything that looks surprising** in a `#` line right above the check it explains: "# Two Threadlings: the
   one every Hatchling gives, and the extra one only the enhanced trait gives." Something that is expected but looks
   wrong gets a comment, never silence.
7. **Layout checks** name the parts and what holds between them: `the row wraps onto more than one line`, `none of "🥊 hit", "💀 kill", the "×" enemy count
   and "🔄 reload" overlap`, `"🔄 reload" sits at the row's right end`, `the row fits inside the screen, with no
   sideways scroll`.

## 6. Order

The order of the rounds depends on the kind too: a loop scenario goes up one value at a time and isolates every round
([loop scenarios](given-when-then-loop-scenarios.md), "Order"); a rule scenario builds the state its claim needs, round
after round ([rule scenarios](given-when-then-rule-scenarios.md), "Order").

## 7. Builds

A Gherkin's build is a fixture of its C# test: a `.build.yaml` next to the test class (E2E-TEST-CODE-RULES.md,
rule h). Builds are made for the tests only.

1. **Only what the test needs:** class, subclass, and the weapons, mods or abilities it uses. Nothing else.
2. **Real items** that can roll what the test gives them (the weapon that really rolls the perk). When the test needs
   a combination no real item has, say so in the file's header comment.
3. **File name:** what it holds, in kebab-case (`ogma-pr6-unrelenting.build.yaml`). The Gherkin names it without
   `.build.yaml`.
4. **Build name:** `"E2E - <what it holds>"` (`"E2E - Ogma PR6 with Unrelenting"`), quoted.
5. **Header comment:** one line with what the build holds and what it is for.
6. Check it with `dotnet test --filter RepoFileValidationTests`: every fixture must open against the rules, and one
   meant to be refused is listed in that test with the error it must give.

## 8. In the pull request

The full Gherkins are agreed with the owner in the session. After that they live in their tests' `[TestDescription]`,
and the PR's e2e comment shows each one with its GIFs, so the PR description doesn't repeat them: under its
**Acceptance criteria**, each Gherkin is one checkbox line with its title and its test class.

```markdown
- [ ] Unrelenting heals for sure on 3 quick kills (`EnemyCountTests`)
```

The criteria checked in a file stay plain checkbox lines next to them.

## 9. Before handing it over

1. **Copy every expected text from the screen** (the preview or a local publish), never from memory or from the code.
2. **Its test passes on every screen its tags name:** while you work, `tools/e2e/record.sh --no-video <its class>`
   plays it there fast, unrecorded; right before the push (never between pushes), `tools/e2e/record.sh
   --changed-since origin/main` records it, and each GIF shows what the Gherkin says; `tools/e2e/push-gifs.sh` posts
   them right after the push. A scenario or web-app change after the recording and before the push means recording
   again.
3. **List anything you saw but didn't expect, and anything the owner has to decide,** in the message that hands the
   work over. Never fold either into a Gherkin as if it were expected.
