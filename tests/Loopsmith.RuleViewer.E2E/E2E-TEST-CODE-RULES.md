# End-to-end test code rules of the rule viewer's page (binding)

These rules are binding for `tests/Loopsmith.RuleViewer.E2E`, the e2e scenarios of the rule viewer's page
(`rules.html`, drawn by `tools/rule-viewer`: its tree of the rules folder beside every rule's flows; `record.sh` draws it
from `Fixtures/rules/` into `out/.rule-viewer-page/`).
[docs/CODING_RULES.md](../../docs/CODING_RULES.md) doesn't apply to this project; these do.

They are the web app's e2e rules, [tests/Loopsmith.Web.E2E/E2E-TEST-CODE-RULES.md](../Loopsmith.Web.E2E/E2E-TEST-CODE-RULES.md),
rule for rule: one Gherkin is one scenario, played on the screens its tags name, reading like the Gherkin line by line,
with everything that isn't the scenario in `Infrastructure/`. What the e2e projects share (a Gherkin and its
`[TestDescription]`, `Screen` and `[PlayInDesktop]` / `[PlayInMobile]`, `ScenarioNaming`, the record next to a video,
the recording's pointer and click ring) is `tests/Loopsmith.E2E.Shared`. This file says only where this project
differs.

## A full example

```csharp
using JoanComasFdz.AssertingThat;
using Loopsmith.RuleViewer.E2E.Infrastructure;
using Loopsmith.RuleViewer.E2E.Infrastructure.RulesPage;

namespace Loopsmith.RuleViewer.E2E.Scenarios.RuleTree;

public sealed class TreeTickingTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Unticking a folder hides every flow under it
          Given the rules page of the fixture rules is open
          When I untick "arc/"
          Then every item under "arc/" is unticked
          And "hunter/" is unticked
          And "rules/" shows a dash
          And no flow of "melees.yaml" or "supers.yaml" shows
          And the flows of "arms.yaml" still show, with its wires
        """)]
    public async Task UntickingAFolderHidesEveryFlowUnderIt(Screen screen)
    {
        // When
        await RulesPage.Tree.UntickAsync("arc/");

        // Then
        var tree = await RulesPage.Tree.ReadAsync();
        Asserting.That(tree)
            .HasEveryItemUnderUnticked("arc/")
            .HasUnticked("hunter/")
            .HasDashed("rules/");
        var flows = await RulesPage.Flows.ReadAsync("arms.yaml");
        Asserting.That(flows)
            .Hides("melees.yaml", "supers.yaml")
            .ShowsWithItsWires("arms.yaml");
    }
}
```

## a. Test shape

As the web rules' rule a, except:
- the Given is always `Given the rules page of the fixture rules is open`: BaseE2E opens it, so the body starts at the
  first `// When`, or at a `// Then` that checks the page as it opens;
- the body calls only `RulesPage` (the page as the user sees it) and `Asserting.That`.

## b. BaseE2E

`Infrastructure/BaseE2E.cs` owns everything that isn't the scenario, as the web rules' rule b, except:
- the skip: a scenario is skipped unless `E2E_RULE_VIEWER_URL` points at the rules page the rule viewer drew from
  `Fixtures/rules/` (`tools/e2e/record.sh` draws and serves it: README, "Run and record");
- **no request leaves the machine:** the page's Google Fonts stylesheet is answered with an empty one, so the page shows
  the system's fonts on every run;
- it opens the page and waits until its tree shows and every flow has its wires, noting when and taking a screenshot,
  so `record.sh` trims the blank start off the video;
- **a console error fails the scenario:** an error the page logs to its console, or a script error it throws, fails a
  scenario that otherwise passed, naming the errors;
- it gives the scenario its `RulesPage`.

## c. RulesPage

`Infrastructure/RulesPage/` is an object model of the page as the user sees it, with the web rules' rule c's members
(Act, Read), each going through `OnScreen` (a copy of the web project's):
- `RulesPage.Tree` (the tree: `ClickArrowAsync`, `TickAsync`, `UntickAsync`, `ClickNameAsync`, `ReadAsync`),
  `RulesPage.Flows` (every rule's flows: `ReadAsync(<the part the check is about>)`, `ReadInPlaceAsync()`) and
  `RulesPage.ReadLayoutAsync()`;
- **an item is found by the name its row shows** (`"arc/"`, `"melees.yaml"`, `"Combination Blow"`,
  `"combination-blow-1"`), and so is a part of the flows (its tree item's: the part carries the item's `data-item`).
  Locators are the page's own class names (`.rtree`, `.trow`, `.twisty`, `.tname`, `.rflows`, `.flow`), which
  `tools/rule-viewer/Loopsmith.RuleViewer/Page/` names;
- an action waits for what the page does after it: a checkbox or an arrow for the next two frames (the page wires its
  flows on the next one), a name until the window stops scrolling;
- **a read of where the page scrolled to reads in place** (`OnScreen.ReadInPlaceAsync`): `Flows.ReadInPlaceAsync()` and
  `ReadLayoutAsync()` scroll nothing, since bringing a box into view would undo what they check. Every other read
  brings its box into view first, as in the web project, so the video shows it.

## d. Assertions

As the web rules' rule d: one `<Type>Assertions` class next to each snapshot, in the page's words (`HasFolded(…)`,
`HasDashed(…)`, `HasOnlySelected(…)`, `ShowsWithItsWires(…)`, `HasAtTheTopOfTheView(…)`,
`HasTheTreeAboveTheFlowsFullWidth()`). "With its wires" means every flow of the part that shows has an arrow ending at
each of its result boxes (and at its first condition's), where the boxes are now.

## e. Fixture rules

`Fixtures/rules/` is the rules folder the page is drawn from: copies of real rule files (`hunter/arc/melees.yaml`,
`hunter/arc/supers.yaml`, `mods/arms.yaml`) and `glossary.yaml`, chosen to need no other file, so the scenarios
don't change when `rules/` does. A scenario needing another kind of item adds its file there (a copy of a real one
that opens on its own). `FixtureRulesTests` (`dotnet test`, no browser) fails when they no longer draw a tree.

## f. Isolation

As the web rules' rule g: each run has its own context; the page is only read.

## g. Adding a test

As the web rules' rule h, with `Scenarios/<domain folder>/` of this project (today `RuleTree`), and
`tools/e2e/record.sh --no-video <its class>` (it draws and serves the fixture rules' page instead of the web app).
Except step 4's fixture files and folder of its own: every class opens the same page, drawn from `Fixtures/rules/`, so a
class stays in its domain folder (`Scenarios/RuleTree/TreeTickingTests.cs`), and a change to `Fixtures/` re-records
all of them.

## h. From a Gherkin to a scenario

| In the Gherkin | In the scenario |
|---|---|
| `@desktop @mobile` / `@desktop` / `@mobile` | `[Theory, PlayInDesktop, PlayInMobile]` / `[Theory, PlayInDesktop]` / `[Theory, PlayInMobile]` |
| `Given the rules page of the fixture rules is open` | nothing: BaseE2E opens it |
| `I tap the arrow of "Combination Blow"` | `Tree.ClickArrowAsync("Combination Blow")` |
| `I untick "arc/"` / `I tick "melees.yaml"` | `Tree.UntickAsync("arc/")` / `Tree.TickAsync("melees.yaml")` |
| `I tap the name "Firepower"` | `Tree.ClickNameAsync("Firepower")` |
| `"rules/" and "arc/" are open in the tree` / `"Combination Blow" is folded` | `Tree.ReadAsync()`, `HasOpen("rules/", "arc/")` / `HasFolded("Combination Blow")` |
| `"combination-blow-1" and "combination-blow-2" show under it` | `ShowsUnder("Combination Blow", "combination-blow-1", "combination-blow-2")` |
| `"Combination Blow" and "Disorienting Blow" are hidden in the tree` | `HidesInTheTree("Combination Blow", "Disorienting Blow")` |
| `every checkbox in the tree is ticked` | `HasEveryItemTicked()` |
| `every item under "melees.yaml" is ticked` / `… is unticked` | `HasEveryItemUnderTicked("melees.yaml")` / `HasEveryItemUnderUnticked(…)` |
| `"…" are ticked, with no dash` / `"…" is unticked` / `"…" show a dash` | `HasTicked(…)` / `HasUnticked(…)` / `HasDashed(…)` |
| `"Firepower" is the only item highlighted` | `HasOnlySelected("Firepower")` |
| `the flows of "…" still show, each with its wires` | `Flows.ReadAsync("<the first one>")`, `ShowsWithItsWires(…)` |
| `the flows of "…" are hidden` / `no flow of "…" shows` | `Flows.ReadAsync("<a part that shows>")`, `Hides(…)` |
| `the flows of "Firepower" are at the top of the view` | `Flows.ReadInPlaceAsync()`, `HasAtTheTopOfTheView("Firepower")` |
| `the tree is left of the flows, their tops in line` / `the tree is above the flows, as wide as they are` | `ReadLayoutAsync()`, `HasTheTreeLeftOfTheFlows()` / `HasTheTreeAboveTheFlowsFullWidth()` |
| `the tree and the flows don't overlap` / `the page doesn't scroll sideways` | `HasNothingOverlapping()` / `DoesNotScrollSideways()` |
