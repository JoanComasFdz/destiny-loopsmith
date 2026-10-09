# Destiny Data Compendium snapshot

`sheet_dump.py` downloads every tab of the (view-only, public) Destiny Data Compendium Google
Sheet into a folder: one `.md` + `.csv` per tab, `tabs.json`, `INDEX.md`, an offline
`viewer.html` and the raw HTML. Loopsmith ingests the numbers from it (cooldowns, chunk energy
scalars, artifact perks, statuses) into `rules/` with `compendium/<date>/<tab>#<row>` provenance.

**Keep it private.** The Compendium is one person's donation-supported work: the raw snapshot is
never committed (`snapshots/` and `tools/compendium/compendium-*` are gitignored) and never served.

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
tools/compendium/get-compendium.sh            # or --images
```

## In a cloud session

Unzip into `snapshots/compendium/<date>/` (gitignored). A session can also download it itself
once `docs.google.com` is in the environment's allowed domains:
`python3 tools/compendium/sheet_dump.py <sheet URL> snapshots/compendium/<date> --no-images`.
