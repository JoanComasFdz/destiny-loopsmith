# End-to-end test code rules (binding)

These rules are binding for `tests/Loopsmith.Web.E2E`. [docs/CODING_RULES.md](../../docs/CODING_RULES.md) doesn't
apply to this project; these do. Its web host section still binds the app: what the tests locate by (`data-action`,
`data-testid`) is named there.

The aim: a scenario is one Gherkin, line for line. Every acceptance criterion about the app is a Gherkin, written by
[docs/given-when-then-test-rules.md](../../docs/given-when-then-test-rules.md), and every Gherkin is one scenario here,
played on the screens its tags name. A scenario reads like the Gherkin it implements, in the app's own words, and
everything that isn't the scenario lives in `Infrastructure/`.

## A full example

```csharp
using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Scenarios.Loop.Weapons;

/// <summary>Reloading a weapon as a step of its own (ADRs D37): it takes no enemy count.</summary>
public sealed class ReloadTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Reload stands apart from the count and takes none
          Given the build "festival-flight-one-for-all" is imported, with no steps
          When I tap "Festival Flight" in the trigger panel
          Then its row shows "🥊 hit" and "💀 kill" with the "×" enemy count
          And "🔄 reload" stands apart from the count
          When I select "3 in 3 s · One for All"
          And I tap "🔄 reload"
          Then step #1 is "kinetic:reload", with no enemy count
          When I tap "🥊 hit"
          Then step #2 is "kinetic:hit:3"
        """)]
    public async Task ReloadStandsApartFromTheCountAndTakesNone(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("festival-flight-one-for-all.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.FestivalFlight());

        // Then
        var row = await Frontend.Designer.TriggerPalette.ReadAsync();
        Asserting.That(row)
            .ShowsTheCountWith(new StepTrigger.Hit(), new StepTrigger.Kill())
            .ShowsApartFromTheCount(new StepTrigger.Reload());

        // When
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("3 in 3 s · One for All");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Reload());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var reloaded = await Frontend.Designer.Loop.ReadAsync();
        Asserting.That(reloaded).HasStep(1, new LoopStep.Reload(new WeaponSlot.Kinetic()));

        // When
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        var hit = await Frontend.Designer.Loop.ReadAsync();
        Asserting.That(hit).HasStep(2, new LoopStep.Hit(new WeaponSlot.Kinetic(), 3));
    }
}
```

## a. Test shape

- A class is `public sealed class <Topic>Tests : BaseE2E`, in `Scenarios/<domain folder>/` (rule h).
- **One Gherkin is one scenario:** one method, a `[Theory]` with:
  - `[PlayInDesktop]` and/or `[PlayInMobile]`: each plays it once, on that screen (rule b). They match the Gherkin's
    `@desktop` / `@mobile` tags. Each attribute can be there at most once (a second one doesn't compile);
  - `[TestDescription("""…""")]`: the Gherkin it implements, word for word as the PR's acceptance criterion writes it,
    tags and `#` lines included;
  - one parameter, `Screen screen`: the screen its attribute plays it on. BaseE2E reads it; the body never does (the
    project's `NoWarn` says so to xunit's analyzer).
- Its **name** is the Gherkin's `Scenario:` title, each run of letters and digits a word (an apostrophe joins its
  word), capitalized
  (`ScenarioNaming.ToMethodName`): "Reaper's Tithe gives a stack per enemy killed at once" →
  `ReapersTitheGivesAStackPerEnemyKilledAtOnce`. Its **title** in the PR's comment is the Gherkin's title, as written.
  Its **GIF name** is the method name kebaberized, then its screen:
  `reapers-tithe-gives-a-stack-per-enemy-killed-at-once-mobile` (`Infrastructure/ScenarioNaming.cs`).
- `ScenarioShapeTests` (`dotnet test`, no browser) fails a scenario that isn't a `[Theory]` with at least one screen
  attribute and its `Screen` parameter, has no `[TestDescription]`, isn't named after its Gherkin's title, or whose
  screen attributes aren't its Gherkin's tags.
- The body is `// Given` once, then a `// When` and a `// Then` for each round of the Gherkin, in its order (rule i).
  It calls only `Frontend`, the exported loop and `Asserting.That`: no Playwright API, no selectors, no `Assert.*`,
  no delays.
- Strings appear only for text the UI composes around names and numbers (an option, a card's heading or line, a state
  chip: rule e) and fixture file names.

## b. BaseE2E

`Infrastructure/BaseE2E.cs` owns everything that isn't the scenario:
- the skip: a scenario is skipped unless `E2E_URL` points at a running site;
- one browser for the whole run (`BrowserRun`, an assembly fixture);
- a fresh browser context per run of a scenario, with its own storage, so runs share nothing; it checks the site's
  IndexedDB and local storage are empty before opening Home;
- **the screen** (`Infrastructure/Screens.cs`): the context opens as the screen the run's `[PlayInDesktop]` /
  `[PlayInMobile]` names. Desktop is a 1280×800 window with a mouse. Mobile is Chromium's phone emulation: 390×844,
  touch, a phone's pixel density, so the page is laid out for a phone and matches `(hover: none)` and
  `(pointer: coarse)` (the app's touch styles: no hover previews, no drag handle). The video is the window's size;
- the Bungie routing (`BungieRouting`: no scenario reaches bungie.net except to fill the cache; README, "The manifest cache");
- the recorded page: it opens Home and waits until it is shown (behind the manifest's startup screen), noting when, so
  `tools/e2e/record.sh` trims the startup screen off the video;
- saving the video, also when the run fails, with its record next to it (title, Gherkin, screen, GIF name, domain
  folder, when Home was shown, whether it passed);
- the `Frontend` property.

A scenario never repeats any of that.

## c. Frontend

`Infrastructure/Frontend/` is an object model of the UI that mirrors the app:
- one class per page (`Pages/*.razor` → `Frontend/Pages/`) and per component it shows (`Components/*.razor` →
  `Frontend/Components/`), named as the app names them, nested as the UI nests them:
  `Frontend.Designer.Weapons.Kinetic`, `Frontend.Designer.Loop`. A part of a component that the user sees as its own
  box (the loadout's Subclass, Artifact, Weapons and Armor boxes; a step's card, `Frontend.Designer.Loop.Step(1)`) gets
  its own class, named as the UI labels it;
- every member touches the browser, so every member is async with an `Async` suffix:
  - **Act**, what a user does, by verb: `Click…Async` (a tap on a phone), `Fill…Async`, `Select…Async` (dropdowns),
    `Tick…Async` (checkboxes), `Pick…Async` (pickers), `Import…Async`, `Export…Async`;
  - **Wait**: `WaitUntil<what the user expects>Async` (`Designer.WaitUntilShownAsync()`), always for something the user
    would see;
  - **Read**: `ReadAsync()` returns an immutable record snapshot of what that part shows, never raw HTML: vocabulary
    values (rule e) where the part shows one kind of game thing (a trigger, a step's token), and the shown text where
    it can show any kind or composes a line (a step card's sources, lines and chips), which its assertions match
    against the vocabulary case's `ToShownText` or the quoted line; `ReadLayoutAsync()` returns where its parts are
    on the screen (their boxes, in CSS pixels) for the layout checks (rule d).
- **Show the box first.** Every Act and Read member goes through `OnScreen` (`OnScreen.PressAsync(box, target)` for a
  click or tap, `OnScreen.ActAsync(box, target, act)` for any other action, `OnScreen.ReadAsync(box, read)`), never
  straight to Playwright. The box is the part of the UI the user sees the action happen in or reads (the Next trigger
  panel, the Weapons box, the loop), not only the element clicked: Playwright scrolls a clicked element only just into
  view and doesn't scroll for a read, so without it the GIF would show part of the box, or none of it. `OnScreen` also
  paces a recorded scenario: one step at least `--action-gap` seconds after the previous one (README, "Run and
  record"). On a phone a press is a tap, and no mouse pointer glides to it. Waits don't go through it.
- Snapshots don't retry: a scenario waits for what it expects before it reads.
- Locators are private to their class and use only `data-action`, `data-testid` or role + accessible name (and the
  `data-*` attributes of an element found that way); never CSS classes, positions or XPath. A script a page object
  runs in the page to read a whole part at once (a step's card, a row's layout) follows the same rule, and its result
  comes back as JSON read with `ShownJson` (Playwright only builds types with a parameterless constructor). The app's
  side of this is docs/CODING_RULES.md's web host section: renaming a `data-action` or `data-testid` updates its page
  object in the same PR.

## d. Assertions

- Each snapshot record and each exported-loop type has its own static `<Type>Assertions` class next to it
  (`SubclassBoxSnapshotAssertions` next to `SubclassBox.cs`), with extension methods on `AssertingThat<T>` in domain
  language: `IsNotSet()`, `HasHeading("#1 Ogma PR6 (💀 kill)")`, `Fires(new WeaponPerk.Unrelenting(), "heals you 65
  HP")`, `HasNoSteps()`; never `TextIs(…)`.
- A Gherkin's "must not be there" line has its own assertion: `DoesNotFire(new WeaponPerk.ReapersTithe())`,
  `HasNoChance()`, `ShowsNone(new StateGroup.Weapons())`, `OffersCounts("1 enemy")` (exactly those).
- **Layout** assertions work on a `ReadLayoutAsync()` snapshot, from the boxes of its parts, never from a screenshot:
  `HasNoOverlap()`, `HasAtRightEnd(new StepTrigger.Reload())`, `WrapsOntoSeveralLines()`, `FitsInsideTheScreen()`
  (inside the window, and the page doesn't scroll sideways). A box less than a pixel over another touches it; it
  doesn't overlap.
- They return the `AssertingThat<T>` for chaining, use xunit's `Assert` inside, and their message says what the user
  sees against what was expected: "Under Unrelenting the card shows "heals you 65 HP", but "heals you 66 HP" was
  expected."
- Read and assert on separate lines:

  ```csharp
  var artifact = await Frontend.Designer.Artifact.ReadAsync();
  Asserting.That(artifact).IsNotSet();
  ```

## e. Vocabulary

- `Infrastructure/Vocabulary/` holds one Dunet union per kind of game thing a scenario sets up or checks (class,
  subclass, aspect, fragment, weapon, weapon perk, armor piece, armor set, armor set bonus, armor mod, artifact,
  artifact perk, step trigger, state group…), one file per kind.
- Each file has one function from a case to what the UI shows (`ToShownText`): the only place those strings live.
  `new Weapon.FestivalFlight()` is "Festival Flight" on the screen, so when the app renames something, one line
  changes, not every test that uses it. The reverse (`Read<Kind>`), when a snapshot needs one, is derived
  from it: it gives the case the UI's text names.
- A Gherkin's quoted **name** of a game thing that a scenario acts on or names as a source ("Festival Flight",
  "Unrelenting", "💀 kill", "WEAPONS") is a vocabulary case in the scenario. A quoted **text the UI composes** around
  names and numbers is the quoted string itself, exactly as the Gherkin quotes it: an option ("3 in 5 s ·
  Unrelenting"), a card's heading or line ("heals you 65 HP (chance)"), a state chip ("Orb of Power (chance)",
  "One for All · Festival Flight", "Exhaust").
- Every union has an `Unlisted(string Shown)` case: what the UI shows that no case names yet, so a failing assertion
  names it as the UI shows it.
- A case is added when a scenario needs it; no up-front lists.

## f. Exported loop

- `Designer.ExportLoopAsync()` catches the browser's download and reads it with Core's own parser
  (`LoopFiles.LoopFileParsing.ParseLoopFile` → `LoopDesign`, its steps typed as `PlayerAction`). The project never
  parses a loop file itself; assertions extend those types.
- Steps are numbered as the UI numbers them: `loop.Step(1)` is the first, after the fresh spawn (#0).

## g. Isolation

A scenario never depends on another scenario or on what ran before it: everything it needs comes from its own context
(BaseE2E), its fixtures and its own steps. Its rounds are isolated inside it the way its Gherkin says (a round removes
the step the one before it added). Scenarios run in parallel, in any order; the runs of one scenario on its screens
are separate contexts too.

## h. Adding a test

1. Write the **Gherkin** first (docs/given-when-then-test-rules.md): it is the PR's acceptance criterion.
2. Find its **domain folder** under `Scenarios/` (README, "Where tests go"): by what the player does, never by PR or task.
   A new area gets a new folder.
3. Find its **class**: a topic in that folder (`<Topic>Tests.cs`); add one when no topic fits.
4. Write the scenario (rules a–g, and rule i for the translation). Its **fixture files** (a minimal `.build.yaml` /
   `.loop.yaml`, named for what they hold: `ogma-pr6-unrelenting.build.yaml`, its build named
   `"E2E - Ogma PR6 with Unrelenting"`) sit next to the class, committed; the project copies them to the test output.
   Never under `builds/`: the web app embeds `builds/` and would list them on Home.
5. Grow `Infrastructure/` only by what this scenario needs: the page-object members, vocabulary cases and assertions it
   calls. Missing `data-action` / `data-testid` attributes are added to the app in the same PR.
6. Record it: `tools/e2e/record.sh <its class>`. It passes on every screen it is played on, and each GIF shows what
   the Gherkin says.

## i. From a Gherkin to a scenario

Line for line, in the Gherkin's order:

| In the Gherkin | In the scenario |
|---|---|
| `@desktop @mobile` | `[Theory, PlayInDesktop, PlayInMobile]` (only the tags it has) |
| The whole Gherkin | `[TestDescription("""…""")]`, word for word |
| `Scenario: Unrelenting heals for sure on 3 quick kills` | `public async Task UnrelentingHealsForSureOn3QuickKills(Screen screen)` |
| `Given the build "ogma-pr6-unrelenting" is imported, with no steps` | `// Given`: `Frontend.Home.ImportBuildAsync("ogma-pr6-unrelenting.build.yaml")`, then `Designer.WaitUntilShownAsync()` |
| `Given the app is open on Home` | `// Given`: `Frontend.Home.WaitUntilShownAsync()` |
| A `When`, and the `And` lines after it | One `// When` block: one Act call per line, in order, ending with the `WaitUntil…` for what the Then reads |
| `I tap "Ogma PR6" in the trigger panel` | `TriggerPalette.ClickWeaponAsync(new Weapon.OgmaPr6())`, `WaitUntilRowShownAsync(new Weapon.OgmaPr6())` |
| `I select "3 in 5 s · Unrelenting"` | `TriggerPalette.SelectTargetCountAsync("3 in 5 s · Unrelenting")`: the option's exact text |
| `I tap "💀 kill"` | `TriggerPalette.ClickAsync(new StepTrigger.Kill())`, `Loop.WaitUntilStepShownAsync(1)` |
| `I remove step #1 with the "✕" on its card` | `Loop.ClickRemoveStepAsync(1)`, `Loop.WaitUntilNoStepsAsync()` |
| `I tick "why" above the loop` | `Designer.TickWhyAsync()`, `Loop.Step(1).WaitUntilReasonsShownAsync()` |
| A `Then`, and the `And` lines after it | One `// Then` block: one read per part it checks, then one assertion per line, chained |
| `card #1's heading is "#1 Ogma PR6 (💀 kill)"` | `HasHeading("#1 Ogma PR6 (💀 kill)")` |
| `it reads "Ogma PR6 💀 killed 1 enemy"` / `it reads "nothing triggers"` | `Reads("Ogma PR6 💀 killed 1 enemy")` / `ReadsNothingTriggers()` |
| `"Unrelenting" fires "heals you 65 HP (chance)"` | `Fires(new WeaponPerk.Unrelenting(), "heals you 65 HP (chance)")` |
| `"Reaper's Tithe" fires nothing` / `no line says "(chance)"` | `DoesNotFire(new WeaponPerk.ReapersTithe())` / `HasNoChance()` |
| `its DEBUFFS show only "Exhaust"` / `its WEAPONS show none` | `Shows(new StateGroup.Debuffs(), "Exhaust")` / `ShowsNone(new StateGroup.Weapons())` |
| `the "×" enemy count offers "1 enemy" and "3 in 5 s · Unrelenting"` | `TriggerPalette.ReadAsync()`, `OffersCounts("1 enemy", "3 in 5 s · Unrelenting")` |
| `the loop has no steps` | `Loop.ReadAsync()`, `HasNoSteps()` |
| A layout line (`the row wraps onto more than one line`, `none of … overlap`, `… sits at the row's right end`, `the row fits inside the screen`) | `ReadLayoutAsync()`, `WrapsOntoSeveralLines()`, `HasNoOverlap()`, `HasAtRightEnd(…)`, `FitsInsideTheScreen()` |
| A quoted game thing's name | A vocabulary case (rule e) |
| A `#` line (why it surprises, why one screen) | Stays in the Gherkin; a `//` line above the read it explains, when it explains a check |

A Gherkin line with no page-object member or assertion yet gets one (rule h, step 5), named in the same words.
