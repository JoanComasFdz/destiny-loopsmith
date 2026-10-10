# Bungie manifest excerpt

`rules/manifest.yaml` is Loopsmith's excerpt of the Bungie manifest: the official name, kind, type, icon,
rarity and (for weapons, armor and armor mods) slot and damage type of every hash Loopsmith names — each
`hash:` in `rules/` and every hash a saved DIM share (`builds/*/dim-loadout.json`) wears. The web app shows a
build the way DIM shows a loadout from it, and a DIM link's weapons become the build's weapons
([docs/loop-format.md](../../docs/loop-format.md#starting-from-a-dim-link)). Icons are loaded from
bungie.net; the data is Bungie's, used under its API terms.

Regenerate it after adding a hash to the rules, a saved DIM share, or when Bungie publishes a new manifest:

```bash
tools/manifest/get-manifest.sh /tmp/manifest            # downloads three English components (~3 MB compressed)
python3 -I tools/manifest/extract_manifest.py /tmp/manifest "$(cat /tmp/manifest/version.txt)"
dotnet test                                             # the catalog version changes: re-pin the example loops
```

`get-manifest.sh` reads the manifest index from `https://www.bungie.net/Platform/Destiny2/Manifest/` (with
`$BUNGIE_API_KEY` as `X-API-Key` when set). A cloud session got bungie.net's 500 error page for the index
(2026-10-10) but could download the component files; give their content id and version instead
(`MANIFEST_ID=… MANIFEST_VERSION=…`, see the script). The extractor reports every hash the manifest doesn't
have and writes nothing for it. Downloads are untrusted data: keep them out of the repository and run the
extractor with `python3 -I`.
