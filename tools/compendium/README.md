# Destiny Data Compendium snapshot

The Compendium download (`Loopsmith.CompendiumDownload`, .NET) downloads every tab of the (view-only, public) Destiny
Data Compendium Google Sheet into a folder: one `NN_<Tab>.md` + `.csv` per tab, `INDEX.md`, `README.md`, `tabs.json`,
`meta.json`, an offline `viewer.html`, Google's raw HTML (`raw/`) and CSV export (`gviz/`), the images unless
`--no-images`, and `download.log`. Rule authors read it and write its numbers by hand into `rules/` (statuses,
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

## The download

```bash
dotnet run --project tools/compendium/Loopsmith.CompendiumDownload -- <sheet URL or id> [<folder>] [--no-images] [--zip]
dotnet run --project tools/compendium/Loopsmith.CompendiumDownload -- --rebuild [<folder>] [--no-images] [--zip]
```

`<folder>` is by default `<sheet title>_<YYYY-MM-DD_HHMM>`; `--rebuild` downloads no tab again (it fetches the images
still missing, unless `--no-images`, and rebuilds `INDEX.md`, `README.md` and `viewer.html` from `raw/`); `--zip` then
writes `<folder>.zip`, holding the folder. Three projects, like the e2e tools' ([tools/e2e/README.md](../e2e/README.md)):

- `Loopsmith.CompendiumDownload`: the host. Its requests to Google (`Hosting/SheetClient`), the files it writes
  (`SnapshotFiles`), the log and the console (`SnapshotLog`, `Progress`): the tabs (`TabDownloading`), the images, 4 at a
  time (`ImageDownloading`), then INDEX, README and the viewer (`SnapshotBuilding`).
- `Loopsmith.CompendiumDownload.Core`: its logic, pure, following [CODING_RULES](../../docs/CODING_RULES.md): the tab
  list (`TabListing`), a tab's table → its grid (`HtmlReading`, `GridReading`; AngleSharp reads the HTML), the tab files
  (`TabWriting`), `tabs.json` and `meta.json` (`MetaReading`, `JsonWriting`), `INDEX.md` (`IndexWriting`), `README.md`
  (`ReadmeWriting`), the viewer (`ViewerWriting`, `ViewerTemplate`), the images (`ImageListing`), the log's lines
  (`ResponseDescribing`) and the command line (`OptionsReading`).
- `Loopsmith.CompendiumDownload.Tests`: their tests, which `dotnet test` runs with the rest, in CI too. With
  `LOOPSMITH_COMPENDIUM_DIR` set, `SnapshotParityTests` also checks that snapshot's tab files and `INDEX.md` against
  what the tool makes of its `raw/` pages, byte for byte.

Neither project references `Loopsmith.Core`, only `Loopsmith.Functional` (an architecture test holds it).

It replaced `sheet_dump.py` (Python, requests + BeautifulSoup) and writes the same files: a snapshot's tab files,
`tabs.json`, `meta.json`, `INDEX.md` and `gviz/` are byte for byte the Python version's, read where Python and .NET
differ the way Python did (BeautifulSoup's text, which turns a run of spaces between tags into one; `csv.writer`'s
quoting and `\r\n`; `json.dumps`' spacing and escapes; `round`). The log is `download.log` (it was `sheet_dump.log`),
`README.md`'s rebuild lines name this tool, and `viewer.html` holds the same tables written out by AngleSharp instead of
BeautifulSoup (the same cells, styles, images and links). Google's own pages (`raw/`, `htmlview_raw.html`) and its
image links change with every request, so two downloads, by either version, differ there.

## Windows (PowerShell)

Needs a clone of this repository, and Docker Desktop (running) or the .NET 10 SDK.

```powershell
cd <the clone>
powershell -ExecutionPolicy Bypass -File .\tools\compendium\get-compendium.ps1              # tabs only (small)
powershell -ExecutionPolicy Bypass -File .\tools\compendium\get-compendium.ps1 -WithImages  # also the images
```

It runs the download in Docker's .NET 10 SDK image (`mcr.microsoft.com/dotnet/sdk:10.0`, about 1.3 GB on disk, pulled the first time),
or with the local .NET SDK when Docker isn't running or can't pull the image, and prints the path of
`tools\compendium\compendium-<date>.zip` — send that zip to the Loopsmith session.

## Linux / macOS / WSL

```bash
tools/compendium/get-compendium.sh            # tabs only, zipped (Docker or the .NET SDK); --images for images
tools/compendium/get-compendium-docker.sh     # everything (tabs + images + viewer) via Docker, not zipped
```

`get-compendium-docker.sh` leaves a `compendium-<date>/` folder: zip it to send it (PowerShell
`Compress-Archive -Path compendium-<date>\* -DestinationPath compendium-<date>.zip`, or 7-Zip — a `.7z`
works too). Leave out `images/` and `raw/` if the archive is too big: the tabs (`NN_<Tab>.md/.csv`),
`INDEX.md` and `gviz/` are what a session reads.

In Docker, the scripts build into the container's `/tmp` (`--artifacts-path`), so the clone's `bin/` and `obj/` stay
the host's; each run restores the tool's packages again (a few seconds).

## Troubleshooting

**Docker: `lookup registry-1.docker.io: no such host`** (or `mcr.microsoft.com`). Docker Desktop resolves names through
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
dotnet run --project tools/compendium/Loopsmith.CompendiumDownload -- <sheet URL> snapshots/compendium/<date> --no-images
```
