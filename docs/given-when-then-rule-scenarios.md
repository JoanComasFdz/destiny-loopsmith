# Rule scenarios

A **rule scenario** plays one claim of a rule in the designer, so every claim the rules make has a recording of its own
to watch: in the PR's e2e comment, and in the VS Code extension's player next to the test. It lives in
`tests/Loopsmith.Web.E2E/Rules/`, grouped like the rules.

This document is a child of [How to write a Gherkin](given-when-then-test-rules.md), which holds what every Gherkin
shares (its shape, When, Then, builds, the PR, the checks before handing it over). It adds what a rule scenario does
differently: its scope, where it goes, its build, its state and the order of its rounds. Its sibling is
[loop scenarios](given-when-then-loop-scenarios.md).

## Example

The worked example is [LethalCurrentTests.cs](../tests/Loopsmith.Web.E2E/Rules/Hunter/Arc/Aspects/LethalCurrent/LethalCurrentTests.cs):
Lethal Current's four claims, each a Gherkin and its C#, with the minimal build each one imports next to it. One:

```gherkin
@desktop @mobile
Scenario: Lethal Current's armed melee hit jolts, strikes and is consumed
  # Rule hunter-arc-aspects-lethal-current-3: Melee hit ➜ Lethal Current equipped and Lethal Current on you ➜ Lethal Current strike, Jolt, ends Lethal Current
  Given the build "lethal-current-marksmans-dodge-combination-blow" is imported, with no steps
  When I tap "Marksman's Dodge" in the trigger panel
  Then card #1's heading is "#1 Class ability"
  And its BUFFS show only "Lethal Current"
  # keeping step #1: its Lethal Current is what #2 spends
  When I tap "Combination Blow" in the trigger panel
  And I tap "🥊 hit"
  Then card #2's heading is "#2 Melee (🥊 hit)"
  And it reads "Melee 🥊 hit"
  And "Lethal Current" fires "Jolt target (10s)", "consumes Lethal Current" and "Lethal Current strike"
  And its BUFFS show none
  And its DEBUFFS show only "Jolt"
```

## Scope

1. **One claim, one Gherkin.** A claim is one flow of the rule viewer (`tools/rule-viewer/`, ADRs D39): what sets it
   off ➜ what must be in place ➜ what it gives. A rule is one claim per trigger when it names several (a `melee` rule
   is a Melee claim and a Sliding melee claim), and each half of a count pair (2+ kills against fewer) is a claim. A
   passive ("Always") is a claim too.
2. **Its subject is the claim, whatever the PR changes.** This replaces "test only what the PR changes"
   ([loop scenarios](given-when-then-loop-scenarios.md), "Scope") for rule scenarios only: a PR that adds rule scenarios
   tests rules it didn't change.
3. **No negatives.** A rule scenario shows what its claim gives, never that its element fires nothing without its
   trigger or its state: those can't all be tested.
4. **What the designer can't show is a finding.** A claim whose result the designer doesn't show, or shows differently
   from the rule viewer, gets no scenario that papers over it: the PR lists it as a finding, and the class's summary
   names the claim it leaves out.

## Where it goes

The rules are grouped by file (ADRs D40, [rule-format.md](rule-format.md), "Files"), and the scenarios follow them:

| The rule | Its scenarios | Their recordings |
|---|---|---|
| `rules/hunter/arc/melees.yaml` | `Rules/Hunter/Arc/Melees/<Element>/<Element>Tests.cs` | `rules-hunter-arc-melees-resources` |
| `rules/hunter/class-abilities.yaml` | `Rules/Hunter/ClassAbilities/<Element>/<Element>Tests.cs` | `rules-hunter-class-abilities-resources` |
| `rules/all-classes/arc/fragments.yaml` | `Rules/AllClasses/Arc/Fragments/<Element>/<Element>Tests.cs` | `rules-all-classes-arc-fragments-resources` |

- **Folders:** each folder of the rule file's path, in PascalCase (`all-classes` → `AllClasses`), then its file's name
  (`class-abilities.yaml` → `ClassAbilities`); namespace `Loopsmith.Web.E2E.Rules.<the same>`, which names the
  recordings' release.
- **Class:** one per element, named for it (`LethalCurrentTests`), in a folder of its own with its builds, named for
  the element too (`Aspects/LethalCurrent/`: E2E-TEST-CODE-RULES.md, rule h); one method per claim.
- **Recordings:** the "E2E videos" workflow keeps the latest of each in its rule file's release
  ([hosting.md](hosting.md), "E2E videos on pull requests").

## The shape

1. **Tags:** `@desktop @mobile`.
2. **Title:** the claim as a statement, with its element's name ("Lethal Current's dodge arms the next melee hit").
3. **The rule line:** right under `Scenario:`, a `#` line with the claim's full name (its file's path, then the rule's
   id: `hunter-arc-aspects-lethal-current-3`) and its flow as the rule viewer draws it, so the scenario can be found
   from the rule and the rule from the scenario. The same name draws the flow as a picture
   (`dotnet run --project tools/rule-viewer/Loopsmith.RuleViewer -- --rule <its name>` →
   `out/rule-viewer/<its name>.png`): the session shows it to the owner under the Gherkin when they agree on it, and the
   recording draws it for the PR's e2e comment, between the scenario's title and its Gherkin.

## Its build

A rule scenario's build follows [How to write a Gherkin](given-when-then-test-rules.md), section 7, and holds:

1. **The claim's element,**
2. **the ability its trigger needs,** when the element isn't one: one with no rules of its own (Marksman's Dodge), or
   whose rules don't fire on that trigger (Combination Blow's on a hit),
3. **the elements that put the claim's states in place,** when the scenario can't build them otherwise (Skip Grenade
   and Spark of Shock for Jolt on the target),

and nothing else. Its header comment says why each element beyond the claim's is there.

## Its state

Every state the claim needs ("on you", "on the target", a Super cast, a threshold declared) is built by the scenario's
own rounds, before the round of its trigger. Each of those rounds ends with a Then on what it left ("its BUFFS show only
"Lethal Current""), and the next round's `# keeping step #N` line says what it uses. A scenario never relies on another
scenario's steps.

## Then

The trigger's round checks:

- the card's heading and, when it helps, what it reads ("Melee 🥊 hit on Jolted target");
- every line the claim's element fires, exactly (`"Lethal Current" fires …`);
- the state its results change: a buff it gives or ends, a debuff it puts on the target;
- for a passive, the damage resistance it gives: the row's PvE and PvP, and the element among its sources once
  "▸ DAMAGE RESISTANCE" is open; or the damage bonus it gives: the row's value, and the element among its sources once
  "▸ DAMAGE BONUS" is open.

Other elements' lines on the same card are left out ([How to write a Gherkin](given-when-then-test-rules.md), section 5,
"Check only what the subject changes").

## Order

The rounds go in the order the state needs: first the rounds that build it, then the trigger's. No round removes a
step: each one builds on the one before.
