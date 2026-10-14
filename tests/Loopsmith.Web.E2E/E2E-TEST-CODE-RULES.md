# End-to-end test code rules (binding)

These rules are binding for `tests/Loopsmith.Web.E2E`. [CONVENTIONS.md](../../CONVENTIONS.md)'s coding rules don't
apply to this project; these do. Its web host section still binds the app: what the tests locate by (`data-action`,
`data-testid`) is named there.

The aim: a scenario reads like the acceptance criterion it checks, in the app's own words, and everything that isn't
the scenario lives in `Infrastructure/`.

## A full example

```csharp
using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Scenarios.Starting;

/// <summary>Starting a loop from scratch on Home.</summary>
public sealed class StartFromScratchTests : BaseE2E
{
    [Fact]
    public async Task GivenAppIsOpenOnHome_WhenIClickStartFromScratch_ThenTheDesignerShowsAnArcHunterWithNothingEquipped()
    {
        // Given
        await Frontend.Home.WaitUntilShownAsync();

        // When
        await Frontend.Home.ClickStartFromScratchAsync();
        await Frontend.Designer.WaitUntilShownAsync();

        // Then
        var subclass = await Frontend.Designer.Subclass.ReadAsync();
        Asserting.That(subclass).HasClass(new GuardianClass.Hunter()).HasSubclass(new Subclass.Arc());

        var weapons = await Frontend.Designer.Weapons.ReadAsync();
        Asserting.That(weapons).HasNothingEquipped();

        var armor = await Frontend.Designer.Armor.ReadAsync();
        Asserting.That(armor).HasNothingEquipped();

        var artifact = await Frontend.Designer.Artifact.ReadAsync();
        Asserting.That(artifact).IsNotSet();

        var loop = await Frontend.Designer.Loop.ReadAsync();
        Asserting.That(loop).HasNoSteps();
    }
}
```

## a. Test shape

- A class is `public sealed class <Topic>Tests : BaseE2E`, in `Scenarios/<domain folder>/` (rule h).
- Each scenario is one `[Fact]` named `Given<…>_When<…>_Then<…>`, in domain words (what the player does and sees,
  never how the page is built).
- Its **title** is that name humanized: split on `_`, `Humanize()` each part, join with ", " — "Given app is open on
  home, When I click start from scratch, Then the designer shows an arc hunter with nothing equipped". Its **GIF name**
  is the method name kebaberized: `given-app-is-open-on-home-when-i-click-start-from-scratch-then-…`
  (`Infrastructure/ScenarioNaming.cs`).
- The body has three sections, marked `// Given`, `// When`, `// Then`, and calls only `Frontend`, the exported loop
  and `Asserting.That`: no Playwright API, no selectors, no `Assert.*`, no delays.
- Strings appear only for text the UI shows (typed into it, or a name it shows that no vocabulary case holds yet) and
  fixture file names.

## b. BaseE2E

`Infrastructure/BaseE2E.cs` owns everything that isn't the scenario:
- the skip: a scenario is skipped unless `E2E_URL` points at a running site;
- one browser for the whole run (`BrowserRun`, an assembly fixture);
- a fresh browser context per scenario, with its own storage, so scenarios share nothing; it checks the site's IndexedDB
  and local storage are empty before opening Home;
- the Bungie routing (`BungieRouting`: no scenario reaches bungie.net except to fill the cache; README, "The manifest cache");
- the recorded page: it opens Home and waits until it is shown (behind the manifest's startup screen), noting when, so
  `tools/e2e/record.sh` trims the startup screen off the video;
- saving the video, also when the scenario fails, with its record next to it (title, GIF name, domain folder, when
  Home was shown, whether it passed);
- the `Frontend` property.

A scenario never repeats any of that.

## c. Frontend

`Infrastructure/Frontend/` is an object model of the UI that mirrors the app:
- one class per page (`Pages/*.razor` → `Frontend/Pages/`) and per component it shows (`Components/*.razor` →
  `Frontend/Components/`), named as the app names them, nested as the UI nests them:
  `Frontend.Designer.Weapons.Kinetic`, `Frontend.Designer.Loop`. A part of a component that the user sees as its own
  box (the loadout's Subclass, Artifact, Weapons and Armor boxes) gets its own class, named as the UI labels it;
- every member touches the browser, so every member is async with an `Async` suffix:
  - **Act**, what a user does, by verb: `Click…Async`, `Fill…Async`, `Select…Async` (dropdowns), `Pick…Async`
    (pickers), `Import…Async`, `Export…Async`;
  - **Wait**: `WaitUntil<what the user expects>Async` (`Designer.WaitUntilShownAsync()`), always for something the user
    would see;
  - **Read**: `ReadAsync()` returns an immutable record snapshot of what that part shows, holding vocabulary values
    (rule e), never raw HTML text.
- Snapshots don't retry: a scenario waits for what it expects before it reads.
- Locators are private to their class and use only `data-action`, `data-testid` or role + accessible name (and the
  `data-*` attributes of an element found that way); never CSS classes, positions or XPath. The app's side of this
  is CONVENTIONS.md's web host section: renaming a `data-action` or `data-testid` updates its page object in the same PR.

## d. Assertions

- Each snapshot record and each exported-loop type has its own static `<Type>Assertions` class next to it
  (`SubclassBoxSnapshotAssertions` next to `SubclassBox.cs`), with extension methods on `AssertingThat<T>` in domain
  language: `IsNotSet()`, `IsEquipped(new Weapon.X())`, `HasFirstPerk(new WeaponPerk.Slice())`, `HasNoSteps()`; never
  `TextIs(…)`.
- They return the `AssertingThat<T>` for chaining, use xunit's `Assert` inside, and their message says what the user
  sees against what was expected: "The Subclass box shows the Arc subclass, but Solar was expected."
- Read and assert on separate lines:

  ```csharp
  var artifact = await Frontend.Designer.Artifact.ReadAsync();
  Asserting.That(artifact).IsNotSet();
  ```

## e. Vocabulary

- `Infrastructure/Vocabulary/` holds one Dunet union per kind of game thing a scenario sets up or checks (class,
  subclass, aspect, fragment, weapon, weapon perk, armor piece, armor set, armor set bonus, armor mod, artifact,
  artifact perk, step trigger…), one file per kind.
- Each file has one function from a case to what the UI shows (`ToShownText`): the only place those strings live. The
  reverse (`Read<Kind>`) is derived from it: it gives the case the UI's text names.
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
(BaseE2E), its fixtures and its own steps. Scenarios run in parallel, in any order.

## h. Adding a test

1. Find its **domain folder** under `Scenarios/` (README, "Where tests go"): by what the player does, never by PR or task.
   A new area gets a new folder.
2. Find its **class**: a topic in that folder (`<Topic>Tests.cs`); add one when no topic fits.
3. Write the scenario (rules a–g). Its **fixture files** (a minimal `.build.yaml` / `.loop.yaml`, named for what they
   hold: `arc-hunter-amplified.build.yaml`) sit next to the class, committed; the project copies them to the test
   output. Never under `builds/`: the web app embeds `builds/` and would list them on Home.
4. Grow `Infrastructure/` only by what this scenario needs: the page-object members, vocabulary cases and assertions it
   calls. Missing `data-action` / `data-testid` attributes are added to the app in the same PR.
5. Record it: `tools/e2e/record.sh <its class>`.
