# Loop scenarios

A **loop scenario** tests how the designer behaves: the subject a pull request changes, on Home or in the designer
(a weapon's enemy count, the trigger panel's row, importing a build). It lives in `tests/Loopsmith.Web.E2E/Scenarios/`,
by domain (its [README](../tests/Loopsmith.Web.E2E/README.md), "Where tests go").

This document is a child of [How to write a Gherkin](given-when-then-test-rules.md), which holds what every Gherkin
shares (its shape, When, Then, builds, the PR, the checks before handing it over). It adds what a loop scenario does
differently: its scope and the order of its rounds. Its sibling is [rule scenarios](given-when-then-rule-scenarios.md).

## Example

The worked example is [EnemyCountTests.cs](../tests/Loopsmith.Web.E2E/Scenarios/Loop/Weapons/EnemyCount/EnemyCountTests.cs):
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

## Scope

1. **Test only what the PR changes.** One Gherkin covers one subject (e.g. "One for All's enemy count"). Behaviour the
   PR doesn't touch, even if it shows on the same screen, is left out.
2. **Cover every case that can change the result.** For example:
   - each value the control offers;
   - the action variants (hit and kill);
   - the right element and another one ("this weapon" and another weapon);
   - the boundary below the value that matters (1 enemy against 3);
   - two rules that meet (one count named by two rules).

## Order

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
