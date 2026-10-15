# How to write a Gherkin

A Gherkin is a Given/When/Then test of the app, written so that anyone can play it click by click, with nothing to
work out on their own. These rules apply to every Gherkin a session writes. A Gherkin is two things at once:

- **an acceptance criterion** of a pull request: every criterion about how the app behaves is one (section 1);
- **one end-to-end test**: each Gherkin is exactly one C# scenario in `tests/Loopsmith.Web.E2E`, which holds the
  Gherkin word for word in its `[TestDescription]` and does what it says, line by line. How a Gherkin becomes C# is
  [E2E-TEST-CODE-RULES.md](../tests/Loopsmith.Web.E2E/E2E-TEST-CODE-RULES.md), rule i.

The PR's e2e comment shows each scenario's title, its Gherkin, and a GIF of it played on each screen its tags name
(🖥️ Desktop, 📱 Mobile) ([hosting.md](hosting.md), "E2E videos on pull requests").

## Example

The worked example is [EnemyCountTests.cs](../tests/Loopsmith.Web.E2E/Scenarios/Loop/Weapons/EnemyCountTests.cs):
the nine tests of [PR #79](https://github.com/JoanComasFdz/destiny-loopsmith/pull/79)'s enemy counts (the weapon perks
that need several enemies in one action), each a Gherkin and its C#, with the minimal build each one imports next to
it. A short one:

```gherkin
@desktop @mobile
Scenario: Unrelenting heals for sure on 3 quick kills
  Given the build "ogma-pr6-unrelenting" is imported, with no steps
  When I tap "Ogma PR6" in the trigger panel
  Then the "×" enemy count offers "1 enemy" and "3 in 5 s · Unrelenting"
  When I tap "Ogma PR6" in the trigger panel
  And I select "1 enemy"
  And I tap "💀 kill"
  Then card #1's heading is "#1 Ogma PR6 (💀 kill)"
  And it reads "Ogma PR6 💀 killed 1 enemy"
  And "Unrelenting" fires "heals you 65 HP (chance)"
  When I remove step #1 with the "✕" on its card
  Then the loop has no steps
  When I tap "Ogma PR6" in the trigger panel
  And I select "3 in 5 s · Unrelenting"
  And I tap "💀 kill"
  Then card #1's heading is "#1 Ogma PR6 (💀 kill 3)"
  And it reads "Ogma PR6 💀 killed 3 enemies in 5 s"
  And "Unrelenting" fires "heals you 65 HP"
```

## 1. What is a Gherkin

1. **Every acceptance criterion about how the app behaves is a Gherkin:** what someone does and sees on Home or in the
   designer. A criterion checked in a file (a doc, a rules YAML, a CLI's output, a workflow) stays a plain line.
2. **Layout is a Gherkin too**, checked from where things are on the screen: a row wraps, two parts don't overlap, a
   button sits at a row's right end, a row fits inside the screen with no sideways scroll, a text isn't cut off. "Looks right",
   colours and spacing are never a Gherkin: nothing checks them but an eye.
3. **Not yet:** what needs a Bungie login or a DIM link (no e2e scenario can reach either yet). Such a criterion stays
   a plain line, checked by hand on the preview, and says why.

## 2. Scope

1. **Test only what the PR changes.** One Gherkin covers one subject (e.g. "One for All's enemy count"). Behaviour the
   PR doesn't touch, even if it shows on the same screen, is left out.
2. **Cover every case that can change the result.** For example:
   - each value the control offers;
   - the action variants (hit and kill);
   - the right element and another one ("this weapon" and another weapon);
   - the boundary below the value that matters (1 enemy against 3);
   - two rules that meet (one count named by two rules).

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
5. **A tooltip's text** is a check of its own ("the "Stats for All · 1 effect · Ogma PR6" chip's tooltip reads …"),
   and makes the Gherkin desktop only.
6. **Explain anything that looks surprising** in a `#` line right above the check it explains: "# Two Threadlings: the
   one every Hatchling gives, and the extra one only the enhanced trait gives." Something that is expected but looks
   wrong gets a comment, never silence.
7. **Layout checks** name the parts and what holds between them: `the row wraps onto more than one line`, `none of "🥊 hit", "💀 kill", the "×" enemy count
   and "🔄 reload" overlap`, `"🔄 reload" sits at the row's right end`, `the row fits inside the screen, with no
   sideways scroll`.

## 6. Order

1. **Go up, one value at a time:** the dropdown's values in the order it lists them ("1 enemy" first, then each larger
   count), and hit before kill.
2. **Isolate every round.** After a round's Then, remove the step with its "✕", unless the next round needs it. Each
   round then starts from the same state as the first, and what it shows comes from its own actions only.
   - Removing is the next round's When (`When I remove step #1 with the "✕" on its card`), and that round's Then is
     `Then the loop has no steps`.
   - Keep a step only when the next round is about what it left: a buff it gave and the next one uses, or a pickup on
     the ground the next one picks up. Say so in a `#` line in that round ("# keeping step #1: its Orb of Power is
     what #2 picks up").
3. **Put the dropdown round first** (what the selector offers), then the rounds that play the counts.

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
6. Check it with `dotnet run --project src/Loopsmith.Cli -- validate <file>`.

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
