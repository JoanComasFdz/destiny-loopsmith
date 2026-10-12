# Bungie manifest excerpt

`rules/manifest.yaml` is Loopsmith's excerpt of the Bungie manifest: the official name, kind, type, Bungie's
description, icon, rarity and (for weapons, armor and armor mods) slot and damage type — and a weapon's trait
columns with the perks each can roll, its intrinsic perk and an exotic's catalyst, an exotic armor's perk, the
armor sets with their 2- and 4-piece perks, and the artifacts with the perks they unlock — of every hash Loopsmith names — each
`hash:` in `rules/` and every hash a saved DIM share (`builds/*/dim-loadout.json`) wears, plus every perk those
weapons' columns roll with, and every enhanced plug of a trait the rules author (by its name: "Enhanced Trait",
"Enhanced Origin Trait"), so the tests can tell an authored perk that lacks its `enhanced` block
([docs/rule-format.md](../../docs/rule-format.md#enhanced-traits-enhanced)). The web app shows a build the way DIM shows a loadout from it, a DIM link's weapons
become the build's weapons, and the designer's perk picker offers what each column rolls with
([docs/loop-format.md](../../docs/loop-format.md#starting-from-a-dim-link)). It also holds every plug that changes an armor stat
(`statPlugs`: armor-stat rolls, masterworks, tuning, stat mods, fragments), so the designer adds up a build's stats
from the player's own armor the way DIM does ([docs/loop-format.md](../../docs/loop-format.md#stats)), and the three champion
breakers (`breakerTypes`, with a weapon's `breaker`: its own, else its frame's), the six armor stats with their icons
(`armorStats`), each weapon's and armor piece's season watermark (`watermark`) and the overlays Bungie draws on an
item's icon (`itemConstants`: the gear tiers' diamonds and the masterwork glow), so the build's squares and a player's
copies look as in DIM: the season icon top-left, a legendary weapon's frame or a piece's stat focus top-right, the
tier and masterwork, and the breaker under the square. Icons are loaded from
bungie.net; the data is Bungie's, used under its API terms. It also holds the traits the glossary's keywords name
(`keywordTraits`: the game's own keywords, Jolted, Bolt Charge, Woven Mail), so each keyword shows its in-game icon.

Regenerate it after adding a hash to the rules, a saved DIM share, or when Bungie publishes a new manifest:

```bash
tools/manifest/get-manifest.sh /tmp/manifest            # downloads eleven English components (~11 MB compressed, ~215 MB on disk)
python3 -I tools/manifest/extract_manifest.py /tmp/manifest "$(cat /tmp/manifest/version.txt)"
dotnet test                                             # the catalog version changes: re-pin the example loops
```

The web app reads the same components in the browser for hashes the excerpt lacks
(`src/Loopsmith.Core/ManifestJoining`, the same reading in C#); `ManifestContentId` in
`src/Loopsmith.Web/wwwroot/appsettings.json` is the content id it falls back to — set it to the one
`get-manifest.sh` prints. To check that the two readings agree, run the tests with the download:
`LOOPSMITH_MANIFEST_DIR=/tmp/manifest dotnet test --filter ManifestJoin`.

`get-manifest.sh` reads the manifest index from `https://www.bungie.net/Platform/Destiny2/Manifest/` (with
`$BUNGIE_API_KEY` as `X-API-Key` when set). A cloud session got bungie.net's 500 error page for the index
(2026-10-10) but could download the component files; give their content id and version instead
(`MANIFEST_ID=… MANIFEST_VERSION=…`, see the script). The extractor reports every hash the manifest doesn't
have and writes nothing for it. Downloads are untrusted data: keep them out of the repository and run the
extractor with `python3 -I`.
