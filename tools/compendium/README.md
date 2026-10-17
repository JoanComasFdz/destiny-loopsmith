# Destiny Data Compendium snapshot

`sheet_dump.py` downloads every tab of the (view-only, public) Destiny Data Compendium Google
Sheet into a folder: one `NN_<Tab>.md` + `.csv` per tab, `INDEX.md`, `README.md`, `tabs.json`, an
offline `viewer.html`, Google's raw HTML (`raw/`) and CSV export (`gviz/`), and the images unless
`--no-images`. Rule authors read it and write its numbers by hand into `rules/` (statuses,
abilities, aspects, fragments, mods, exotics, artifact perks), citing each row as
`compendium/<date>/<Tab>#<row>` ([docs/rule-format.md](../../docs/rule-format.md), Provenance) and
paraphrasing, never copying, its text. Ability cooldowns, chunk energy scalars and extra charges are
recorded in the rules as facts about their element (ADRs D1, D5).

**Keep it private.** The Compendium is one person's donation-supported work: the raw snapshot is
never committed (`snapshots/` and `tools/compendium/compendium-*` are gitignored) and never served.

## Checking the rules against a snapshot

The Compendium check reads a snapshot's tabs (the subclass tabs, Class Abilities, Armor Mods, Artifact Perks and Weapon
Perks; `src/Loopsmith.Core/CompendiumReading`) and checks every rule citing one of them: its cited row must name the element,
and every number the element takes from its source must be one that row states. The snapshot checked against may be newer
than the one a rule cites: a row that moved or a number that changed shows up, and the rule is re-cited to the new date.

```bash
LOOPSMITH_COMPENDIUM_DIR=$PWD/snapshots/compendium/<date> dotnet test --filter CompendiumCheckTests
```

Without `LOOPSMITH_COMPENDIUM_DIR` the test is skipped, so CI (which never has the snapshot) skips it. A failing run names
each element, its citation and what is wrong ("row 11 of Armor Mods names 'Siphon', …, not 'Arc Siphon'", "ability.baseCooldown
151.5 is not a number 'Skip Grenade' states"), then lists the citations into tabs it doesn't read yet. When the Compendium
names an element differently, add it to [`aliases.yaml`](aliases.yaml) (ADRs D12) with its reason; an alias for an element
that no longer exists fails the check too.

## Windows (PowerShell)

Needs Docker Desktop (running) or Python 3.10+ (python.org).

```powershell
cd <folder with get-compendium.ps1 and sheet_dump.py>
powershell -ExecutionPolicy Bypass -File .\get-compendium.ps1              # tabs only (small)
powershell -ExecutionPolicy Bypass -File .\get-compendium.ps1 -WithImages  # also the images
```

It prints the path of `compendium-<date>.zip` — send that zip to the Loopsmith session.

## Linux / macOS / WSL

```bash
tools/compendium/get-compendium.sh            # tabs only, zipped (Docker or Python); --images for images
tools/compendium/get-compendium-docker.sh     # everything (tabs + images + viewer) via Docker, not zipped
```

`get-compendium-docker.sh` leaves a `compendium-<date>/` folder: zip it to send it (PowerShell
`Compress-Archive -Path compendium-<date>\* -DestinationPath compendium-<date>.zip`, or 7-Zip — a `.7z`
works too). Leave out `images/` and `raw/` if the archive is too big: the tabs (`NN_<Tab>.md/.csv`),
`INDEX.md` and `gviz/` are what a session reads.

## Troubleshooting

**Docker: `lookup registry-1.docker.io: no such host`.** Docker Desktop resolves names through
Windows, so check Windows first: `Resolve-DnsName registry-1.docker.io` (not `nslookup`, which asks
the DNS server directly and bypasses Windows' rules). If that times out too, look for a leftover VPN
DNS rule with `Get-DnsClientNrptPolicy`. A `.` rule pointing at `100.100.100.100` is Tailscale's:
it stays after Tailscale disconnects and swallows every lookup. Reconnect Tailscale, restart
Windows, or run `tailscale set --accept-dns=false`.

## In a cloud session

Unzip it so the tab files sit directly in `snapshots/compendium/<date>/` (gitignored): the zip made by
`get-compendium.*` holds a `compendium-<date>/` folder, whose contents go there. `7z x` extracts a
`.7z` (`apt-get install 7zip` if it's missing). The snapshot is never in git, so **every new session
starts without it**. A session downloads it itself: `docs.google.com` answers a session (2026-10-13; the
sheet URL is in `get-compendium.sh`). If that fails, upload the archive.

```bash
pip install requests beautifulsoup4   # a cloud session has them: the session-start hook installs them
python3 tools/compendium/sheet_dump.py <sheet URL> snapshots/compendium/<date> --no-images
```
