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

Unzip into `snapshots/compendium/<date>/` (gitignored; `7z x` extracts a `.7z` — `apt-get install
7zip` if it's missing). The snapshot is never in git, so **every new session starts without it**:
upload the archive again when a session needs it. A session can also download it itself
once `docs.google.com` is in the environment's allowed domains:
`python3 tools/compendium/sheet_dump.py <sheet URL> snapshots/compendium/<date> --no-images`.
