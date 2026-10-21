# Bungie manifest excerpt

`rules/manifest.yaml` is Loopsmith's excerpt of the Bungie manifest: the official name, kind, type, Bungie's
description, icon, rarity and (for weapons, armor and armor mods) slot and damage type — and a weapon's ammo type, trait
columns with the perks each can roll, its intrinsic perk, the weapon mods its mod socket takes and an exotic's catalyst, an exotic armor's perk, the
armor sets with their 2- and 4-piece perks — of every hash Loopsmith names — each
`hash:` in `rules/` and every hash `tools/manifest/test-items.yaml` lists (the weapons, armor and mods the Core tests
equip that no rule names: the Core tests read an item given by hash only from the excerpt), plus every perk those
weapons' columns roll with, and every enhanced plug of a trait the rules author (by its name: "Enhanced Trait",
"Enhanced Origin Trait"), so the tests can tell an authored perk that lacks its `enhanced` block
([docs/rule-format.md](../../docs/rule-format.md#enhanced-traits-enhanced)). The web app shows a build the way DIM shows a loadout, and the designer's perk
picker offers what each column rolls with ([docs/loop-format.md](../../docs/loop-format.md#the-build-in-the-designer)). It also holds every plug that changes an armor stat
(`statPlugs`: armor-stat rolls, masterworks, tuning, stat mods, fragments), so the designer adds up a build's stats
from the player's own armor the way DIM does ([docs/loop-format.md](../../docs/loop-format.md#stats)), and the three champion
breakers (`breakerTypes`, with a weapon's `breaker`: its own, else its frame's), the six armor stats with their icons
(`armorStats`), each weapon's and armor piece's season watermark (`watermark`) and the overlays Bungie draws on an
item's icon (`itemConstants`: the gear tiers' diamonds and the masterwork glow), so the build's squares and a player's
copies look as in DIM: the season icon top-left, a legendary weapon's frame or a piece's stat focus top-right, the
tier and masterwork, and the breaker under the square. Icons are loaded from
bungie.net; the data is Bungie's, used under its API terms. It also holds the traits the glossary's keywords name
(`keywordTraits`: the game's own keywords, Jolted, Bolt Charge, Woven Mail), so each keyword shows its in-game icon.
Every artifact of Artifacts 2.0 (the seven) is written with all its perks: its `tiers` (Tier 1 to 3, seven perks each) and the
highest tier each of its seven perk slots takes (`slots`), and its flavour text (`flavor`), which the designer's artifact editor shows.
Each subclass (the glossary's) also lists every plug it can hold (`plugs`: super, abilities, jump, aspects, fragments),
which the designer's subclass editor offers.

Regenerate it after adding a hash to the rules or an item to `test-items.yaml`, or when Bungie publishes a new manifest.
A Core test (`TestItemHashesTests`) fails, naming the hash, when an item of `test-items.yaml` isn't in the excerpt:

```bash
dotnet run --project tools/manifest/Loopsmith.ManifestTools -- download /tmp/manifest   # twelve English components (~11 MB compressed, ~215 MB on disk) and version.txt
dotnet run --project tools/manifest/Loopsmith.ManifestTools -- extract /tmp/manifest    # writes rules/manifest.yaml (run from the repository's root, or pass --root <repo>)
dotnet test                                                                             # the catalog version changes; nothing pins it
```

`extract` takes the manifest's version from the folder's `version.txt` (`--version <version>` to give another). It prints
how many items, armor sets and stat plugs it wrote, and reports on stderr every hash the manifest doesn't have
(`not in the manifest: <hash>`), writing nothing for it. The output is sorted and stable, so a rerun on the same
manifest changes nothing.

The web app reads the same components in the browser for hashes the excerpt lacks
(`src/Loopsmith.Core/ManifestJoining`, the same reading in C#); `ManifestContentId` in
`src/Loopsmith.Web/wwwroot/appsettings.json` is the content id it falls back to — set it to the one
`download` prints. Three checks run with the download (`LOOPSMITH_MANIFEST_DIR=/tmp/manifest dotnet test`):
- that the committed `rules/manifest.yaml` is what `extract` writes from its manifest version, byte for byte
  (`--filter ExcerptMatchesTheManifest`; skipped when the download is of another version);
- that the two readings agree (`--filter ManifestJoin`);
- that the pickers' "Every item" (`ManifestListing`) lists every weapon and armor piece of the excerpt as the join names
  it, breakers included (`--filter ManifestListing`).

`download` reads the manifest index from `https://www.bungie.net/Platform/Destiny2/Manifest/` (with
`$BUNGIE_API_KEY` as `X-API-Key` when set) and keeps it as `index.json`. A cloud session got bungie.net's 500 error page
for the index (2026-10-10) but could download the component files; give their content id and version instead:

```bash
MANIFEST_ID=e677f6ea-2a1e-48d4-a8c8-700a39a62585 MANIFEST_VERSION=245114.26.10.13.2000-1-bnet.66137 \
  dotnet run --project tools/manifest/Loopsmith.ManifestTools -- download /tmp/manifest
```

The component files themselves (`…/common/destiny2_content/json/en/…`) need no key. Downloads are untrusted data: keep
them out of the repository.

## The manifest tool

Three projects, like the e2e tool's ([tools/e2e/README.md](../e2e/README.md)):
- `Loopsmith.ManifestTools`: the host. It reads the command line and the environment, downloads the manifest
  (`Hosting/ManifestDownload`), reads the components, the rule files and `test-items.yaml`, and writes
  `rules/manifest.yaml` (`Hosting/ExcerptExtraction`, `Hosting/ManifestFiles`).
- `Loopsmith.ManifestTools.Core`: its logic, pure, following [CODING_RULES](../../docs/CODING_RULES.md): the components'
  JSON (`ManifestTables`), the hashes the rules and test items name (`NamedHashReading`), what an item's definition says
  (`ItemReading`: its kind and slot, trait columns, mods, intrinsic, catalyst, breaker, an artifact's tiers, a subclass's
  plugs, its description), the stat plugs (`StatPlugReading`), Bungie's text as YAML (`TextWriting`), the excerpt whole
  (`ExcerptWriting`), the index and the components' addresses (`ManifestDownloading`) and the command line
  (`OptionsReading`).
- `Loopsmith.ManifestTools.Tests`: their tests, which `dotnet test` runs with the rest, in CI too: each reading on small
  hand-made manifests (no Bungie data is committed but the excerpt), the excerpt whole, and the opt-in check above.

Neither project references `Loopsmith.Core`, only `Loopsmith.Functional` (an architecture test holds it). It replaced
`extract_manifest.py` and `get-manifest.sh` (Python and shell), writing the same `rules/manifest.yaml` byte for byte
from the same manifest but for the line that names the generator.
