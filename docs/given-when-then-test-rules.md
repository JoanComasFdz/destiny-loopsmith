# How to write Given/When/Then tests

These rules apply to every Given/When/Then a session writes:

- a PR's acceptance criteria;
- the test plan that comes with a PR;
- end-to-end scenarios.

Someone who follows a test clicks and reads texts, with nothing to work out on their own. The same text can be played
by hand on the PR's preview, or by an end-to-end test.

## Example

[test-plan-example/](test-plan-example/) follows every rule below: the plan for
[PR #79](https://github.com/JoanComasFdz/destiny-loopsmith/pull/79) (the weapon perks that need several enemies in one
action), in [test-plan.md](test-plan-example/test-plan.md), and the minimal build each of its tests imports
(`01-one-for-all.build.yaml` … `09-radiolaria-transposer.build.yaml`). Start a new plan from its shape: the shared
steps, one section per build with its one test, and the rounds of When and Then in it. Its builds use the perks PR #79
adds, so they import once that PR is merged.

## 1. Scope

1. **Test only what the PR changes.** One plan covers one subject (e.g. "enemy counts"). Behaviour the PR doesn't
   touch, even if it shows on the same screen, is left out.
2. **Cover every case that can change the result.** For example:
   - each value the control offers;
   - the action variants (hit and kill);
   - the right element and another one ("this weapon" and another weapon);
   - the boundary below the value that matters (1 enemy against 3);
   - two rules that meet (one count named by two rules).

## 2. Build files

1. **One build file per subject**, with only what the test needs: class, subclass, and the weapons, mods or abilities
   it uses. Nothing else.
2. **Use real items** that can roll what the test gives them (the weapon that really rolls the perk). When the test
   needs a combination no real item has, say so in the file's header comment.
3. **File name:** `<NN>-<subject>.build.yaml`, numbered in the order the plan uses them (`01-one-for-all.build.yaml`).
4. **Build name:** `"PR <n> - Build <NN>: <Subject>"`, quoted because of the colon. The designer then names the loop
   "PR 79 - Build 06: Hatchling enhanced loop", so a screenshot always says which build it is.
5. **Header comment:** one line with the PR, the subject and what the build holds.
6. Before handing a build over, check it with `dotnet run --project src/Loopsmith.Cli -- validate <file>`, then
   import it in the browser.

## 3. Shared steps

The plan starts with a section that explains, once, the actions every test repeats: importing a build, playing a
step, removing a step, reading "(chance)". Tests then refer to them by name ("**Given** build 03 is imported").

## 4. One test

1. **Title:** `T<n> · <what it proves>`, in one line, as a statement ("A kill of 3 counts as hitting 3").
2. **Given:** the build, and the state of the loop: "with no steps", or the steps the test needs already played
   (each one named).
3. **One test per Given.** Tests that start from the same build and the same steps are one test: what each one checks
   becomes a round of it. Split a test only where its Given differs.
4. **Rounds of When and Then.** A test is a sequence of rounds. Each round:
   - **When** is the clicks that lead to one thing to look at;
   - **Then** is what to look at, right after those clicks.

   Click, then check, then click again. A round never checks something its clicks haven't produced yet, and never
   saves all the checks for the end.
5. **Number the When steps continuously** across the rounds of a test (1, 2, 3 in the first round; 4, 5, 6 in the
   next), so "step 5" means one thing.
6. **Write one action per When step,** in the first person, with the control's exact on-screen name in bold:
   - "I tap **Festival Flight** in the trigger panel."
   - "I open the **×** dropdown."
   - "I select "3 in 3 s · One for All"."
   - "I tap **💀 kill**."
7. **Select by the option's exact text,** never by position ("pick the second") and never by default ("leave 1
   enemy"). Write "I select "1 enemy"" even when it is already selected.
8. **Keep checking and doing apart.** "I check that the dropdown offers…" is a Then. Selecting is a When. They are
   never one step.

## 5. Then

1. **One thing to verify per line,** in a bullet list.
2. **Quote the exact on-screen text,** copied from the screen while playing the test.
3. **Say what must not be there** when it matters: "no "(chance)"", "no Reaper's Tithe line", "the dropdown has no
   "2 enemies"".
4. **After every step, give the state** the subject touches. For each of BUFFS, WEAPONS, DEBUFFS and GROUND, say which
   chips are there, with their exact text, or "none". Skip a group only when the subject can never change it, and say
   so once at the top of the plan.
5. **Give a tooltip's text** when it holds something the test checks, and say which chip to hover.
6. **Explain anything that looks surprising** in a **Note** under that Then, e.g. two "Threadling (chance)" lines. Say
   why it is right, and where the app says it (the **why** checkbox shows each line's reason). Something that is
   expected but looks wrong gets a Note, never silence.

## 6. Order

1. **Go up, one value at a time:** the dropdown's values in the order it lists them ("1 enemy" first, then each larger
   count), and hit before kill.
2. **Isolate every interaction.** After a round's Then, remove the step with the **✕** at the top right of its card
   ("Remove this step"), unless the next round needs it. Each round then starts from the same state as the first, and
   what it shows comes from its own clicks only.
   - Removing is a When step of the next round ("I remove step #1 with its **✕**"), and that round's Then starts with
     "The LOOP list has no steps".
   - Keep a step only when the next one is about what it left: a buff it gave and the next one uses, or a pickup on the
     ground the next one picks up. Say so in that round's When ("keeping step #1: its Orb of Power is what #2 picks
     up").
3. **Within a build's test, put the dropdown round first** (what the selector offers), then the rounds that play the
   counts.

## 7. Markdown

1. **Put a blank line after "\*\*When\*\*", "\*\*Then\*\*" and "\*\*Given\*\* …",** and before every list. Markdown only
   starts a numbered list after a paragraph when it starts at 1. Without the blank line, round 2's "4. I tap …" runs
   into the "When" line as one paragraph.
2. **Numbered lists for When, bullets for Then.** A sub-list (the two lines under a source, the four state groups) is
   indented by two spaces under its bullet.
3. **Before handing a test plan over, look at it rendered** (a preview in the editor, or GitHub), not only at the
   source.

## 8. Before handing it over

1. **Play every test exactly as written,** click by click, on the PR's preview or a local publish. Do it on desktop
   and at 390 px.
2. **Copy every expected text from the screen.** Never write one from memory or from the code.
3. **List anything you saw but didn't expect, and anything the owner has to decide,** in the message that hands the
   plan over. Never fold either into the plan as if it were expected.
