#!/usr/bin/env python3
"""
Dump every tab of a view-only (download-disabled) public Google Sheet into
LLM-friendly files, plus an offline Sheets-like viewer.

Uses the read-only views Google itself serves:
  /htmlview                      -> list of tabs (name + gid)
  /htmlview/sheet?gid=...        -> each tab rendered as an HTML table
  /gviz/tq?tqx=out:csv&gid=...   -> raw CSV per tab (secondary)

Output (in <out_dir>/, default: <sheet_title>_<YYYY-MM-DD_HHMM>/):
  README.md         - how the folder is organised (where is what)
  INDEX.md          - one line per tab: file, size, in-sheet sections
  NN_<tab>.md/.csv  - one file per tab (merged cells handled)
  viewer.html       - offline viewer that looks/navigates like Sheets (keep images/ next to it)
  images/           - every image used in the sheet (+ map.json, failed.txt if any)
  raw/              - Google's per-tab HTML (used to build viewer.html)
  gviz/             - raw gviz CSVs (may blank out mixed-type cells)
  tabs.json         - tab name -> gid
  sheet_dump.log    - detailed log of every request (status, headers, errors), appended per run

Usage:
  pip install requests beautifulsoup4
  python sheet_dump.py <SPREADSHEET_ID_or_URL> [out_dir]   # download + build (out_dir optional)
  python sheet_dump.py --rebuild <out_dir>                  # no re-download of tabs; fetch missing
                                                            # images, rebuild INDEX.md + viewer.html
  add --no-images to skip downloading images
"""
import csv, datetime, html as htmllib, json, logging, re, sys, time
from pathlib import Path
from urllib.parse import urlparse, parse_qs

from bs4 import BeautifulSoup

log = logging.getLogger("sheet_dump")


def setup_log(out: Path):
    """Append a detailed log to <out_dir>/sheet_dump.log (every request, status, error)."""
    out.mkdir(parents=True, exist_ok=True)
    h = logging.FileHandler(out / "sheet_dump.log", mode="a", encoding="utf-8")
    h.setFormatter(logging.Formatter("%(asctime)s %(levelname)-7s [%(threadName)s] %(message)s"))
    log.addHandler(h)
    log.setLevel(logging.DEBUG)
    log.info("=" * 70)
    log.info("run: %s", " ".join(sys.argv))
    return out / "sheet_dump.log"


def say(msg: str):
    print(msg, flush=True)
    log.info(msg)


class Progress:
    """Single-line live progress bar on the terminal (thread-safe, ~5 redraws/s)."""
    def __init__(self, label: str, total: int):
        import threading
        self.label, self.total, self.done, self.ok, self.fail = label, total, 0, 0, 0
        self.waiting = 0          # workers currently backing off
        self.t0 = self._last = time.time()
        self.lock = threading.Lock()
        self.draw(force=True)

    def step(self, ok: bool):
        with self.lock:
            self.done += 1
            self.ok += ok
            self.fail += (not ok)
        self.draw(force=self.done == self.total)

    def wait(self, delta: int):
        with self.lock:
            self.waiting += delta
        self.draw()

    def draw(self, force=False):
        now = time.time()
        if not force and now - self._last < 0.2:
            return
        self._last = now
        width = 30
        frac = self.done / self.total if self.total else 1
        bar = "#" * int(frac * width) + "-" * (width - int(frac * width))
        el = now - self.t0
        eta = (el / self.done * (self.total - self.done)) if self.done else 0
        fmt = lambda x: f"{int(x // 60)}m{int(x % 60):02d}s"
        extra = f" | {self.waiting} backing off (rate limited)" if self.waiting else ""
        line = (f"\r{self.label} [{bar}] {self.done}/{self.total} {frac:4.0%}"
                f" | ok {self.ok} fail {self.fail} | {fmt(el)} elapsed, ~{fmt(eta)} left{extra}")
        sys.stdout.write(line.ljust(130)[:160])
        sys.stdout.flush()

    def close(self):
        self.draw(force=True)
        sys.stdout.write("\n")
        sys.stdout.flush()


def describe(r) -> str:
    """One-line description of a response, including redirects (e.g. to a login page)."""
    hops = " -> ".join(f"{x.status_code} {x.headers.get('location', '')}" for x in r.history)
    parts = [f"HTTP {r.status_code}", f"type={r.headers.get('content-type', '-')}",
             f"bytes={len(r.content)}", f"elapsed={r.elapsed.total_seconds():.2f}s"]
    for k in ("retry-after", "x-ratelimit-remaining", "server", "cache-control"):
        if k in r.headers:
            parts.append(f"{k}={r.headers[k]}")
    if hops:
        parts.append(f"redirects=[{hops}] final={r.url}")
    return " ".join(parts)

UA = {"User-Agent": "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 "
                    "(KHTML, like Gecko) Chrome/126.0 Safari/537.36"}


# ----------------------------------------------------------------- helpers
def sheet_id(arg: str) -> str:
    m = re.search(r"/d/([a-zA-Z0-9_-]+)", arg)
    return m.group(1) if m else arg


def slug(s: str) -> str:
    s = re.sub(r"[^\w\- ]+", "", s).strip().replace(" ", "_")
    return s[:60] or "tab"


def stem_for(i: int, name: str) -> str:
    return f"{i:02d}_{slug(name)}"


def _js_unescape(s: str) -> str:
    return re.sub(r"\\x([0-9a-fA-F]{2})|\\u([0-9a-fA-F]{4})|\\(.)",
                  lambda m: chr(int(m.group(1) or m.group(2), 16)) if (m.group(1) or m.group(2))
                  else m.group(3), s)


def list_tabs(soup: BeautifulSoup, html: str):
    tabs = []
    # current htmlview: items.push({name: "Tab", pageUrl: "...", gid: "123", ...})
    for name, gid in re.findall(
            r'items\.push\(\{name: "((?:[^"\\]|\\.)*)",.*?gid: "(-?\d+)"', html):
        tabs.append((_js_unescape(name), gid))
    if tabs:
        return tabs
    # older htmlview: <ul id="sheet-menu"><li id="sheet-button-123">
    for li in soup.select("#sheet-menu li"):
        m = re.search(r"sheet-button-(\d+)", li.get("id", ""))
        if m:
            tabs.append((li.get_text(strip=True), m.group(1)))
    if not tabs:
        for gid in dict.fromkeys(re.findall(r"switchToSheet\('(\d+)'\)", html)):
            tabs.append((f"gid_{gid}", gid))
    return tabs


def table_to_grid(table) -> list[list[str]]:
    """Parse a 'waffle' table into a 2-D grid, expanding row/colspans and
    dropping Google's row-number / column-letter header cells."""
    grid: dict[tuple[int, int], str] = {}
    r = 0
    for tr in table.find_all("tr"):
        if tr.find_parent("thead") is not None:
            continue
        c = 0
        for cell in tr.find_all(["td", "th"], recursive=False):
            if cell.name == "th":
                continue
            while (r, c) in grid:
                c += 1
            for br in cell.find_all("br"):
                br.replace_with("\n")
            text = cell.get_text().strip()
            rs = int(cell.get("rowspan", 1) or 1)
            cs = int(cell.get("colspan", 1) or 1)
            for dr in range(rs):
                for dc in range(cs):
                    grid[(r + dr, c + dc)] = text if (dr, dc) == (0, 0) else ""
            c += cs
        r += 1
    if not grid:
        return []
    nrows = max(k[0] for k in grid) + 1
    ncols = max(k[1] for k in grid) + 1
    rows = [[grid.get((i, j), "") for j in range(ncols)] for i in range(nrows)]
    rows = [row for row in rows if any(v for v in row)]
    if not rows:
        return []
    keep = [j for j in range(ncols) if any(row[j] for row in rows)]
    return [[row[j] for j in keep] for row in rows]


def to_markdown(rows: list[list[str]]) -> str:
    if not rows:
        return "_(empty)_\n"
    esc = lambda v: v.replace("|", "\\|").replace("\n", "<br>")
    width = max(len(r) for r in rows)
    rows = [r + [""] * (width - len(r)) for r in rows]
    out = ["| " + " | ".join(esc(v) for v in rows[0]) + " |",
           "|" + "---|" * width]
    out += ["| " + " | ".join(esc(v) for v in r) + " |" for r in rows[1:]]
    return "\n".join(out) + "\n"


# ----------------------------------------------------------------- download
def download(sid: str, out: Path | None):
    """Download tabs. If out is None, the folder is <title>_<YYYY-MM-DD_HHMM>."""
    import requests
    now = datetime.datetime.now()
    base = f"https://docs.google.com/spreadsheets/d/{sid}"
    s = requests.Session()
    s.headers.update(UA)

    resp = s.get(f"{base}/htmlview", timeout=120)
    resp.raise_for_status()
    soup = BeautifulSoup(resp.text, "html.parser")
    title = soup.title.get_text(strip=True) if soup.title else sid
    title = re.sub(r"\s*-\s*Google (Drive|Sheets)\s*$", "", title)
    if out is None:
        out = Path(f"{slug(title).lower()}_{now:%Y-%m-%d_%H%M}")
    setup_log(out)
    log.info("GET %s -> %s", resp.url, describe(resp))
    say(f"Output folder: {out}")
    (out / "gviz").mkdir(parents=True, exist_ok=True)
    (out / "raw").mkdir(exist_ok=True)
    (out / "htmlview_raw.html").write_text(resp.text, encoding="utf-8")
    tabs = list_tabs(soup, resp.text)
    say(f"Found {len(tabs)} tabs")
    (out / "tabs.json").write_text(json.dumps(dict(tabs), indent=2, ensure_ascii=False), encoding="utf-8")
    (out / "meta.json").write_text(json.dumps({
        "title": title, "id": sid, "url": f"{base}/edit",
        "extracted": now.date().isoformat(),
        "extracted_at": f"{now:%Y-%m-%d %H:%M}"}, indent=2), encoding="utf-8")

    for i, (name, gid) in enumerate(tabs, 1):
        stem = stem_for(i, name)
        r = s.get(f"{base}/htmlview/sheet", params={"headers": "false", "gid": gid}, timeout=120)
        log.info("tab %r GET %s -> %s", name, r.url, describe(r))
        (out / "raw" / f"{stem}.html").write_text(r.text, encoding="utf-8")
        tsoup = BeautifulSoup(r.text, "html.parser")
        table = tsoup.select_one("table.waffle") or tsoup.find("table")
        rows = table_to_grid(table) if table is not None else []
        with open(out / f"{stem}.csv", "w", newline="", encoding="utf-8") as f:
            csv.writer(f).writerows(rows)
        (out / f"{stem}.md").write_text(f"## {name}\n\n{to_markdown(rows)}", encoding="utf-8")

        g = s.get(f"{base}/gviz/tq", params={"tqx": "out:csv", "gid": gid}, timeout=120)
        log.info("tab %r gviz -> %s", name, describe(g))
        if g.ok and not g.text.lstrip().startswith("<"):
            (out / "gviz" / f"{stem}.csv").write_text(g.text, encoding="utf-8")
        say(f"  [{i:02d}] {name!r:45} gid={gid:<12} rows={len(rows)}")
        time.sleep(0.5)
    return s, out


# ----------------------------------------------------------------- images
IMG_EXT = {"image/png": "png", "image/jpeg": "jpg", "image/gif": "gif",
           "image/webp": "webp", "image/svg+xml": "svg", "image/avif": "avif"}


def download_images(out: Path, session=None, workers: int = 4):
    """Save every image the tabs use into images/ so the viewer works offline.
    Google's 'sheets-images-rt' links only load when requested from a Google page
    (Referer/cookies), which is why they break when viewer.html is opened from disk."""
    import hashlib, requests
    from concurrent.futures import ThreadPoolExecutor

    meta, _ = load_meta(out)
    imgdir = out / "images"
    imgdir.mkdir(exist_ok=True)
    map_path = imgdir / "map.json"
    mapping = json.loads(map_path.read_text(encoding="utf-8")) if map_path.exists() else {}

    urls = set()
    for p in sorted((out / "raw").glob("*.html")):
        urls.update(htmllib.unescape(u) for u in re.findall(r'<img[^>]+src="(https?://[^"]+)"', p.read_text(encoding="utf-8")))
    todo = sorted(u for u in urls if u not in mapping)
    if not todo:
        say(f"Images: all {len(urls)} already downloaded")
        return
    s = session or requests.Session()
    s.headers.update(UA)
    referer = f"https://docs.google.com/spreadsheets/d/{meta.get('id', '')}/htmlview"
    if session is None and meta.get("id"):
        try:
            s.get(referer, timeout=60)  # pick up the cookies Google sets for viewers
        except Exception:
            pass

    prog = None

    def fetch(u):
        err = "?"
        for attempt in range(1, 7):
            try:
                r = s.get(u, headers={"Referer": referer,
                                      "Accept": "image/avif,image/webp,image/png,image/*,*/*;q=0.8"},
                          timeout=60)
                ctype = r.headers.get("content-type", "").split(";")[0].strip()
                if r.ok and ctype.startswith("image/"):
                    name = hashlib.sha1(u.encode()).hexdigest()[:16] + "." + IMG_EXT.get(ctype, "img")
                    (imgdir / name).write_bytes(r.content)
                    log.debug("img OK   try%d %s -> %s | %s", attempt, name, describe(r), u)
                    return u, name, None
                err = f"HTTP {r.status_code} {ctype}"
                body = r.text[:300].replace("\n", " ") if not ctype.startswith("image/") else ""
                log.warning("img FAIL try%d %s | body=%r | %s", attempt, describe(r), body, u)
                if r.status_code in (400, 403, 404, 410):
                    break  # permanent: expired/removed link, retrying won't help
                if attempt == 6:
                    break
                wait = r.headers.get("Retry-After", "")
                wait = min(int(wait) if wait.isdigit() else 3 * 2 ** (attempt - 1), 60)
                log.info("img wait %ss before retry | %s", wait, u)
                if prog: prog.wait(+1)
                time.sleep(wait)
                if prog: prog.wait(-1)
                continue
            except Exception as e:
                err = type(e).__name__ + ": " + str(e)[:150]
                log.warning("img EXC  try%d %s | %s", attempt, err, u, exc_info=True)
            if attempt < 6:
                time.sleep(2 * attempt)
        log.error("img GAVE UP: %s | %s", err, u)
        return u, None, err

    # probe one image per host first, so an unreachable host doesn't cost minutes of retries
    host = lambda u: urlparse(u).netloc
    for h in sorted({host(u) for u in todo}):
        u, name, err = fetch(next(x for x in todo if host(x) == h))
        if name:
            mapping[u] = name
        elif not str(err).startswith("HTTP"):  # host unreachable (not just one bad image)
            say(f"Images: {h} not usable ({str(err)[:100]}); keeping remote links for it")
            todo = [x for x in todo if host(x) != h]
    todo = [u for u in todo if u not in mapping]

    say(f"Images: downloading {len(todo)} of {len(urls)} ({workers} at a time) ...")
    fails = []
    from concurrent.futures import as_completed
    prog = Progress("Images", len(todo))
    with ThreadPoolExecutor(workers) as ex:
        futures = [ex.submit(fetch, u) for u in todo]
        for n, f in enumerate(as_completed(futures), 1):
            u, name, err = f.result()
            if name:
                mapping[u] = name
            else:
                fails.append((u, err))
            prog.step(bool(name))
            if n % 100 == 0:
                map_path.write_text(json.dumps(mapping), encoding="utf-8")  # resume-safe
                log.info("progress %d/%d ok=%d fail=%d", n, len(todo), prog.ok, prog.fail)
    prog.close()
    map_path.write_text(json.dumps(mapping), encoding="utf-8")
    say(f"Images: {len(mapping)} saved, {len(fails)} failed")
    if fails:
        (imgdir / "failed.txt").write_text("\n".join(f"{e}\t{u}" for u, e in fails), encoding="utf-8")
        from collections import Counter
        why = Counter((urlparse(u).netloc, e.split(":")[0][:40]) for u, e in fails)
        for (h, e), n in why.most_common():
            say(f"    {n:5}  {h:28} {e}")
        say("  (see images/failed.txt; failed ones stay as remote links;"
              " re-run with --rebuild to retry only those)")


# ----------------------------------------------------------------- shared
def load_meta(out: Path):
    tabs = list(json.loads((out / "tabs.json").read_text(encoding="utf-8")).items())
    meta = {}
    if (out / "meta.json").exists():
        meta = json.loads((out / "meta.json").read_text(encoding="utf-8"))
    else:
        raw = (out / "htmlview_raw.html").read_text(encoding="utf-8") if (out / "htmlview_raw.html").exists() else ""
        t = re.search(r"<title>(.*?)</title>", raw, re.S)
        m = re.search(r"spreadsheets\\?/d\\?/([a-zA-Z0-9_-]{20,})", raw)
        meta = {"title": re.sub(r"\s*-\s*Google (Drive|Sheets)\s*$", "", htmllib.unescape(t.group(1).strip())) if t else out.name,
                "id": m.group(1) if m else "",
                "extracted": datetime.date.fromtimestamp((out / "tabs.json").stat().st_mtime).isoformat()}
        meta["url"] = f"https://docs.google.com/spreadsheets/d/{meta['id']}/edit" if meta["id"] else ""
    meta.setdefault("extracted_at", meta.get("extracted", ""))
    return meta, tabs


def raw_table(out: Path, stem: str):
    p = out / "raw" / f"{stem}.html"
    if not p.exists():
        return None, None
    soup = BeautifulSoup(p.read_text(encoding="utf-8"), "html.parser")
    return soup, (soup.select_one("table.waffle") or soup.find("table"))


# ----------------------------------------------------------------- INDEX.md
def section_links(table) -> list[str]:
    if table is None:
        return []
    out = []
    for a in table.select("a[href^='#rangeid=']"):
        t = a.get_text(" ", strip=True)
        if t and len(t) <= 40 and not re.match(r"go back", t, re.I):
            out.append(t)
    return list(dict.fromkeys(out))


def build_index(out: Path):
    meta, tabs = load_meta(out)
    lines = [f"# {meta['title']} — index ({meta['extracted_at']})", ""]
    if meta.get("url"):
        lines.append(f"Source: {meta['url']}  ")
    lines += [f"Extracted: {meta['extracted_at']}", "",
              "One row per tab of the spreadsheet, in sheet order. Open only the file(s) "
              "that cover the topic; each `.md` has a `.csv` twin with the same data. "
              "Tabs whose name starts with OLD are archived, outdated content. "
              "Token counts are rough (characters / 4). See README.md for the folder layout.", "",
              "| # | Tab | File | Rows | ~Tokens | Sections |", "|---|---|---|---|---|---|"]
    for i, (name, gid) in enumerate(tabs, 1):
        stem = stem_for(i, name)
        md, cs = out / f"{stem}.md", out / f"{stem}.csv"
        rows = list(csv.reader(cs.open(encoding="utf-8"))) if cs.exists() else []
        toks = round(md.stat().st_size / 4 / 1000, 1) if md.exists() else 0
        _, table = raw_table(out, stem)
        secs = ", ".join(section_links(table)).replace("|", "/")
        lines.append(f"| {i} | {name} | [{stem}.md]({stem}.md) | {len(rows)} | {toks}k | {secs} |")
    (out / "INDEX.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
    old = out / "ALL.md"
    if old.exists():
        old.unlink()
    say(f"Wrote {out / 'INDEX.md'}")


def build_readme(out: Path):
    meta, tabs = load_meta(out)
    n = len(tabs)
    old_idx = [i for i, (name, _) in enumerate(tabs, 1) if re.match(r"old\b", name, re.I)]
    cur_idx = [i for i in range(1, n + 1) if i not in old_idx]
    def rng(ids):  # [1,2,3,5] -> "01–03, 05"
        if not ids:
            return "none"
        runs, start = [], ids[0]
        for a, b in zip(ids, ids[1:] + [None]):
            if b != a + 1:
                runs.append(f"{start:02d}" if start == a else f"{start:02d}–{a:02d}")
                start = b
        return ", ".join(runs)
    tok = sum((out / f"{stem_for(i, nm)}.md").stat().st_size for i, (nm, _) in enumerate(tabs, 1)
              if (out / f"{stem_for(i, nm)}.md").exists()) / 4 / 1000
    text = f"""# {meta['title']} — offline copy ({meta['extracted_at']})

Snapshot of the public Google Sheet {meta.get('url') or ''}, taken {meta['extracted_at']}.
It has {n} tabs. Every tab is kept three ways: as text for LLMs, as a visual copy, and as Google's original HTML.

## Where is what

| What you want | Where it is |
|---|---|
| Find which tab covers a topic | `INDEX.md`: one row per tab with its file, size and in-sheet sections |
| The data, for an LLM or for reading | `NN_<Tab>.md`: one Markdown table per tab (~{tok:.0f}k tokens in total) |
| The same data for spreadsheets/scripts | `NN_<Tab>.csv`: same content as the `.md` with the same number |
| Browse it like the original sheet | `viewer.html` (open in a browser; keep `images/` next to it) |
| The pictures and icons | `images/` |
| Google's original pages (used to rebuild) | `raw/`, `htmlview_raw.html`, `tabs.json`, `meta.json` |
| Google's own CSV export (secondary) | `gviz/` |
| What happened during the download | `sheet_dump.log` |

## The tab files

- `NN` is the tab's position in the sheet, so files sort in the same order as the tabs.
- Current tabs: {rng(cur_idx)}.
- Archived tabs (name starts with OLD, outdated values): {rng(old_idx)}.
- The tables mirror the sheet's visual layout rather than a clean database: section headings,
  navigation cells and notes appear as rows; a merged cell's text sits in its top-left cell
  and the cells it covered are empty; line breaks inside a cell are written as `<br>`.
- Empty rows and columns are removed.

## Using it with an LLM

1. Give it `INDEX.md` first (it is small).
2. Then attach only the tab file(s) it points to, rather than everything.
3. Prefer the `.md` files; the `.csv` files hold the same data for tools that want CSV.

## The viewer

`viewer.html` shows every tab with the sheet's colours, merged cells and frozen header rows.
Tabs are at the bottom (≡ lists them all, Alt+↑/↓ switches), Ctrl+K searches all tabs,
and links inside the sheet jump to their tab or section. It needs the `images/` folder
next to it for pictures; without it, image cells show empty.

## Other folders and files

- `images/`: one file per picture, with hashed names. `map.json` maps each original Google
  link to its file. `failed.txt` lists links Google reports as missing; those cells stay
  empty, as they do in the live sheet.
- `raw/`: Google's HTML for each tab, the source used to build the `.md`, `.csv` and viewer.
- `htmlview_raw.html`: Google's tab-list page. It looks blank when opened from disk; that is expected.
- `gviz/`: Google's CSV export per tab. It can blank out cells in columns that mix numbers
  and text, so the `NN_` files are the reliable version.
- `tabs.json` / `meta.json`: tab names and IDs, and the sheet's title, link and snapshot time.

## Rebuilding

Run from the folder that contains this one and `sheet_dump.py`:

- Rebuild README, INDEX and viewer without downloading anything:
  `python sheet_dump.py --rebuild {out.name} --no-images`
- Fresh snapshot (new timestamped folder): `python sheet_dump.py "{meta.get('url') or '<sheet URL>'}"`
"""
    (out / "README.md").write_text(text, encoding="utf-8")
    say(f"Wrote {out / 'README.md'}")


# ----------------------------------------------------------------- viewer.html
RULE_RE = re.compile(r"\.ritz[^{}]*\{[^}]*\}")


def unwrap_google_url(href: str) -> str:
    if href.startswith("https://www.google.com/url?"):
        q = parse_qs(urlparse(href).query).get("q")
        if q:
            return q[0]
    return href


def build_viewer(out: Path):
    meta, tabs = load_meta(out)
    map_path = out / "images" / "map.json"
    imgmap = json.loads(map_path.read_text(encoding="utf-8")) if map_path.exists() else {}
    # images Google reports as gone (404 etc.) are left out, like the live sheet shows them: empty
    dead = set()
    failed_path = out / "images" / "failed.txt"
    if failed_path.exists():
        for line in failed_path.read_text(encoding="utf-8").splitlines():
            err, _, u = line.partition("\t")
            if re.match(r"HTTP (400|403|404|410)\b", err):
                dead.add(u)
    css_parts, templates, tabmeta = [], [], []
    for i, (name, gid) in enumerate(tabs, 1):
        stem = stem_for(i, name)
        soup, table = raw_table(out, stem)
        tabmeta.append({"gid": gid, "name": name, "file": f"{stem}.md"})
        if table is None:
            templates.append(f'<template id="tpl-{gid}"><p class="missing">This tab could not be loaded.</p></template>')
            continue
        # cell styles, renamed per tab so .s0 of one tab doesn't clash with another
        for st in soup.find_all("style"):
            for rule in RULE_RE.findall(st.get_text()):
                rule = re.sub(r"\.s(\d+)\b", rf".t{i}s\1", rule)
                rule = re.sub(r"docs-Roboto", "Roboto", rule)
                rule = re.sub(r"docs-([A-Za-z][\w ]*?)(?=[,;}])", r'"\1"', rule)
                css_parts.append(rule)
        for el in table.find_all(class_=True):
            el["class"] = [f"t{i}{c}" if re.fullmatch(r"s\d+", c) else c for c in el["class"]]
        for el in table.find_all(["script", "style"]):
            el.decompose()
        for img in table.find_all("img", src=True):
            if img["src"] in dead:
                img.decompose()
                continue
            local = imgmap.get(img["src"])
            if local:
                img["src"] = f"images/{local}"
            else:
                img["referrerpolicy"] = "no-referrer"
            img["loading"] = "lazy"
        for a in table.find_all("a", href=True):
            href = unwrap_google_url(a["href"])
            a["href"] = href
            if href.startswith("#"):
                a.attrs.pop("target", None)
            else:
                a["target"], a["rel"] = "_blank", "noopener noreferrer"
        templates.append(f'<template id="tpl-{gid}">{table}</template>')

    page = VIEWER_TEMPLATE
    page = page.replace("/*__TITLE__*/", htmllib.escape(f"{meta['title']} ({meta['extracted_at']})"))
    page = page.replace("/*__SHEET_CSS__*/", "\n".join(dict.fromkeys(css_parts)))
    page = page.replace("<!--__TEMPLATES__-->", "\n".join(templates))
    page = page.replace("/*__TABS__*/[]", json.dumps(tabmeta, ensure_ascii=False))
    page = page.replace("/*__META__*/{}", json.dumps(meta, ensure_ascii=False))
    (out / "viewer.html").write_text(page, encoding="utf-8")
    if dead:
        say(f"Viewer: left out {len(dead)} images Google reports as missing")
    say(f"Wrote {out / 'viewer.html'} ({(out / 'viewer.html').stat().st_size / 1e6:.1f} MB)")


VIEWER_TEMPLATE = r"""<!doctype html>
<html lang="en"><head>
<meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>/*__TITLE__*/</title>
<link rel="preconnect" href="https://fonts.googleapis.com">
<!-- font loads in the background: a slow/blocked fonts.googleapis.com must not stall the page -->
<link href="https://fonts.googleapis.com/css2?family=Roboto:wght@400;500;700&display=swap" rel="stylesheet" media="print" onload="this.media='all'">
<style>
:root{--chrome:#f9fbfd;--line:#c4c7c5;--text:#1f1f1f;--muted:#5f6368;--accent:#0b57d0;--green:#188038;--hdr:#f8f9fa;--hdr-line:#c0c0c0;--sheet:#fff}
*{box-sizing:border-box}
html,body{height:100%;margin:0}
body{display:flex;flex-direction:column;background:var(--chrome);color:var(--text);font:14px Roboto,Arial,sans-serif;overflow:hidden}
header{display:flex;align-items:center;gap:12px;padding:8px 12px;border-bottom:1px solid var(--line);background:var(--chrome);flex-wrap:wrap}
.logo{width:28px;height:28px;flex:none}
.title{display:flex;flex-direction:column;min-width:0;flex:1}
.title h1{font-size:18px;font-weight:400;margin:0;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.title small{color:var(--muted);font-size:12px}
.title small a{color:var(--muted)}
.search{position:relative;flex:0 1 360px;min-width:200px}
.search input{width:100%;padding:8px 12px 8px 34px;border:1px solid transparent;border-radius:20px;background:#e9eef6;font:inherit;outline:none}
.search input:focus{background:#fff;border-color:var(--accent)}
.search svg{position:absolute;left:10px;top:9px;width:16px;height:16px;fill:var(--muted)}
#results{position:absolute;right:0;top:42px;width:min(560px,92vw);max-height:70vh;overflow:auto;background:#fff;border:1px solid var(--line);border-radius:8px;box-shadow:0 4px 16px rgba(0,0,0,.18);z-index:50;display:none}
#results.open{display:block}
#results .hit{display:block;width:100%;text-align:left;border:0;border-bottom:1px solid #eee;background:none;padding:8px 12px;cursor:pointer;font:13px Roboto,Arial}
#results .hit:hover,#results .hit.sel{background:#e8f0fe}
#results .tab{display:inline-block;font-size:11px;color:var(--green);font-weight:500;margin-right:6px}
#results .row{color:var(--muted);font-size:11px}
#results mark{background:#fde293;padding:0}
#results .info{padding:8px 12px;color:var(--muted);font-size:12px}
main{flex:1;position:relative;min-height:0;background:var(--sheet)}
.pane{position:absolute;inset:0;overflow:auto;display:none}
.pane.active{display:block}
footer{display:flex;align-items:stretch;border-top:1px solid var(--line);background:var(--chrome);height:40px;flex:none}
.tbtn{border:0;background:none;padding:0 12px;cursor:pointer;color:var(--muted);display:flex;align-items:center}
.tbtn:hover{background:#e9eef6}
.tbtn svg{width:20px;height:20px;fill:currentColor}
#tabs{display:flex;overflow-x:auto;scrollbar-width:thin;flex:1}
#tabs button{flex:none;border:0;background:none;padding:0 16px;font:500 13px Roboto,Arial;color:#444746;cursor:pointer;white-space:nowrap;border-bottom:3px solid transparent}
#tabs button:hover{background:#e9eef6}
#tabs button.active{background:#e1e9f7;color:var(--accent);border-bottom-color:var(--accent)}
#tabs button.old{color:#80868b}
#menu{position:absolute;left:8px;bottom:46px;background:#fff;border:1px solid var(--line);border-radius:8px;box-shadow:0 4px 16px rgba(0,0,0,.18);max-height:70vh;overflow:auto;z-index:60;display:none;padding:6px 0}
#menu.open{display:block}
#menu button{display:block;width:100%;text-align:left;border:0;background:none;padding:8px 20px;font:13px Roboto,Arial;cursor:pointer}
#menu button:hover{background:#e8f0fe}
#menu button.active{color:var(--accent);font-weight:500}
#toast{position:fixed;left:50%;bottom:56px;transform:translateX(-50%);background:#323232;color:#fff;padding:10px 16px;border-radius:6px;font-size:13px;opacity:0;transition:opacity .2s;pointer-events:none;z-index:70}
#toast.show{opacity:1}
.missing{padding:24px;color:var(--muted)}

/* ---- Google Sheets "waffle" grid base ---- */
.ritz{display:inline-block;min-width:100%}
.ritz .waffle{border-collapse:separate;border-spacing:0;table-layout:fixed;width:0;font-family:Arial,sans-serif;font-size:10pt;user-select:text}
.ritz .waffle td{overflow:hidden;padding:2px 3px;vertical-align:bottom;white-space:nowrap;direction:ltr;color:#000;font-size:10pt;font-family:Arial,sans-serif}
.ritz .waffle a{color:inherit}
.ritz .waffle td a{cursor:pointer}
.ritz .waffle .softmerge{overflow:visible}
.ritz .waffle .softmerge-inner{overflow:hidden;position:relative;text-overflow:clip;white-space:nowrap}
.ritz .waffle thead th{position:sticky;top:0;z-index:3;height:20px;background:var(--hdr);color:var(--muted);font:11px Roboto,Arial;font-weight:400;text-align:center;border-right:1px solid var(--hdr-line);border-bottom:1px solid var(--hdr-line);padding:0;overflow:hidden}
.ritz .waffle th.row-header-shim{width:46px;min-width:46px}
.ritz .waffle thead th.row-header-shim{left:0;z-index:5}
.ritz .waffle tbody th.row-headers-background{position:sticky;left:0;z-index:2;background:var(--hdr);color:var(--muted);font:11px Roboto,Arial;text-align:center;border-right:1px solid var(--hdr-line);border-bottom:1px solid var(--hdr-line);padding:0}
tr.frz > td{background-color:#fff}  /* low specificity: the sheet's own cell colours win */
.ritz .waffle tr.frz > th{z-index:4}
.ritz .waffle .freezebar-cell{background:#c4c7c5 !important;padding:0;height:4px;border:0}
.ritz .waffle tbody th.freezebar-cell{position:sticky;left:0}
.ritz .waffle td.hl{outline:3px solid #1a73e8;outline-offset:-3px;animation:hl 2.4s ease-out}
@keyframes hl{0%{box-shadow:inset 0 0 0 999px rgba(26,115,232,.35)}100%{box-shadow:inset 0 0 0 999px rgba(26,115,232,0)}}

/* ---- per-tab cell styles extracted from Google ---- */
/*__SHEET_CSS__*/
</style>
</head>
<body>
<header>
  <svg class="logo" viewBox="0 0 48 48" aria-hidden="true"><path fill="#0f9d58" d="M29 4H11a3 3 0 0 0-3 3v34a3 3 0 0 0 3 3h26a3 3 0 0 0 3-3V15z"/><path fill="#87ceac" d="M29 4v11h11z"/><path fill="#f1f1f1" d="M15 21h18v15H15zm2 2v3h6v-3zm8 0v3h6v-3zm-8 5v3h6v-3zm8 0v3h6v-3z"/></svg>
  <div class="title"><h1 id="docTitle"></h1><small id="docMeta"></small></div>
  <div class="search">
    <svg viewBox="0 0 24 24"><path d="M15.5 14h-.79l-.28-.27A6.47 6.47 0 0 0 16 9.5 6.5 6.5 0 1 0 9.5 16c1.61 0 3.09-.59 4.23-1.57l.27.28v.79l5 4.99L20.49 19zm-6 0C7.01 14 5 11.99 5 9.5S7.01 5 9.5 5 14 7.01 14 9.5 11.99 14 9.5 14"/></svg>
    <input id="q" type="search" placeholder="Search all tabs  (Ctrl+K)" autocomplete="off">
    <div id="results"></div>
  </div>
</header>
<main id="main"></main>
<footer>
  <button class="tbtn" id="allBtn" title="All sheets"><svg viewBox="0 0 24 24"><path d="M3 18h18v-2H3zm0-5h18v-2H3zm0-7v2h18V6z"/></svg></button>
  <nav id="tabs"></nav>
</footer>
<div id="menu"></div>
<div id="toast"></div>

<!--__TEMPLATES__-->

<script>
// if anything below throws, say so on the page instead of showing a blank viewer
window.addEventListener('error', e => {
  if (!e.message) return;  // resource load errors (images) have no message
  const m = document.getElementById('main');
  if (m && !m.querySelector('.pane')) m.innerHTML = '<p class="missing">Viewer error: ' +
    String(e.message).replace(/</g, '&lt;') + ' (line ' + e.lineno + '). Press F12 → Console for details.</p>';
});
</script>
<script>
const TABS = /*__TABS__*/[];
const META = /*__META__*/{};
const byGid = Object.fromEntries(TABS.map(t => [t.gid, t]));
const $ = s => document.querySelector(s);
let current = null;

document.title = (META.title || document.title) + (META.extracted_at ? ' (' + META.extracted_at + ')' : '');
$('#docTitle').textContent = (META.title || '') + (META.extracted_at ? '  (' + META.extracted_at + ')' : '');
$('#docMeta').innerHTML = 'Offline copy · snapshot ' + (META.extracted_at || META.extracted || '') +
  (META.url ? ' · <a href="' + META.url + '" target="_blank" rel="noopener">open original</a>' : '');

function colName(n){let s='';n++;while(n>0){const m=(n-1)%26;s=String.fromCharCode(65+m)+s;n=Math.floor((n-1)/26);}return s;}

function toast(msg){const t=$('#toast');t.textContent=msg;t.classList.add('show');clearTimeout(toast._t);toast._t=setTimeout(()=>t.classList.remove('show'),2600);}

function pane(gid){
  let p = document.getElementById('pane-' + gid);
  if (p) return p;
  p = document.createElement('div');
  p.className = 'pane'; p.id = 'pane-' + gid;
  const wrap = document.createElement('div'); wrap.className = 'ritz';
  const tpl = document.getElementById('tpl-' + gid);
  if (tpl) wrap.appendChild(tpl.content.cloneNode(true));
  p.appendChild(wrap);
  $('#main').appendChild(p);
  const table = wrap.querySelector('table');
  if (table) decorate(table);
  return p;
}

function decorate(table){
  // column letters in the header shims
  let c = 0;
  table.querySelectorAll('thead th.header-shim').forEach(th => {
    if (th.classList.contains('row-header-shim')) return;
    th.textContent = colName(c++);
  });
  // frozen rows: everything above the horizontal freeze bar sticks to the top
  const rows = [...table.tBodies[0]?.rows || []];
  const fi = rows.findIndex(r => r.querySelector('.freezebar-horizontal-handle'));
  if (fi >= 0 && fi < 30) {
    requestAnimationFrame(() => {
      let top = table.tHead ? table.tHead.getBoundingClientRect().height : 0;
      for (let i = 0; i <= fi; i++) {
        const r = rows[i]; r.classList.add('frz');
        for (const cell of r.cells) { cell.style.position = 'sticky'; cell.style.top = top + 'px';
          if (cell.tagName === 'TH') cell.style.left = '0'; }
        top += r.getBoundingClientRect().height;
      }
    });
  }
}

function show(gid, push = true){
  if (!byGid[gid]) gid = TABS[0].gid;
  const p = pane(gid);
  document.querySelectorAll('.pane.active').forEach(x => x.classList.remove('active'));
  p.classList.add('active');
  current = gid;
  document.querySelectorAll('#tabs button, #menu button').forEach(b => b.classList.toggle('active', b.dataset.gid === gid));
  const btn = document.querySelector('#tabs button[data-gid="' + gid + '"]');
  if (btn) btn.scrollIntoView({block: 'nearest', inline: 'nearest'});
  if (push && location.hash !== '#gid=' + gid) history.replaceState(null, '', '#gid=' + gid);
  document.title = byGid[gid].name + ' – ' + (META.title || '') + (META.extracted_at ? ' (' + META.extracted_at + ')' : '');
  return p;
}

function flash(td){
  if (!td) return;
  td.scrollIntoView({block: 'center', inline: 'center'});
  td.classList.remove('hl'); void td.offsetWidth; td.classList.add('hl');
  setTimeout(() => td.classList.remove('hl'), 2500);
}

function gotoRow(gid, rowNum){
  const p = show(gid);
  const th = document.getElementById(gid + 'R' + (rowNum - 1));
  if (th) flash(th.parentElement.querySelector('td') || th);
  else p.scrollTop = 0;
}

// --- images that fail to load: show an empty cell, like the live sheet does
document.addEventListener('error', e => {
  if (e.target.tagName === 'IMG') e.target.style.visibility = 'hidden';
}, true);

// --- tab bar + menu
const tabsEl = $('#tabs'), menu = $('#menu');
TABS.forEach(t => {
  const b = document.createElement('button');
  b.textContent = t.name; b.dataset.gid = t.gid; b.title = t.file;
  if (/^old\b/i.test(t.name)) b.classList.add('old');
  b.onclick = () => show(t.gid);
  tabsEl.appendChild(b);
  const m = b.cloneNode(true); m.onclick = () => { show(t.gid); menu.classList.remove('open'); };
  menu.appendChild(m);
});
$('#allBtn').onclick = e => { e.stopPropagation(); menu.classList.toggle('open'); };

// --- text index for search and named-range resolution (built lazily from templates)
let INDEX = null;
function buildIndex(){
  if (INDEX) return INDEX;
  INDEX = [];
  TABS.forEach(t => {
    const tpl = document.getElementById('tpl-' + t.gid); if (!tpl) return;
    tpl.content.querySelectorAll('td').forEach((td, k) => {
      const text = td.textContent.replace(/\s+/g, ' ').trim();
      if (!text) return;
      const th = td.parentElement.querySelector('th[id]');
      const row = th ? (+th.id.split('R').pop() + 1) : null;
      INDEX.push({gid: t.gid, k, row, text, lower: text.toLowerCase(), isLink: !!td.querySelector('a')});
    });
  });
  return INDEX;
}
function cellOf(gid, k){ show(gid); return document.getElementById('pane-' + gid).querySelectorAll('td')[k]; }

// --- links inside the sheet
document.addEventListener('click', e => {
  if (!e.target.closest('#menu, #allBtn')) menu.classList.remove('open');
  if (!e.target.closest('.search')) $('#results').classList.remove('open');
  const a = e.target.closest('.pane a[href^="#"]');
  if (!a) return;
  e.preventDefault();
  const h = a.getAttribute('href');
  let m = h.match(/^#gid=(-?\d+)(?:&range=([A-Z]+)(\d+))?/);
  if (m) { if (!byGid[m[1]]) return toast('That tab is not part of this copy.');
           return m[3] ? gotoRow(m[1], +m[3]) : (show(m[1]).scrollTop = 0); }
  if (/^#rangeid=/.test(h)) {
    const label = a.textContent.replace(/\s+/g, ' ').trim();
    const p = document.getElementById('pane-' + current);
    if (/back to the top/i.test(label)) { p.scrollTop = 0; p.scrollLeft = 0; return; }
    // named ranges aren't exported; jump to the matching heading/cell instead
    const idx = buildIndex(), want = label.toLowerCase();
    const srcTd = a.closest('td'), srcK = [...p.querySelectorAll('td')].indexOf(srcTd);
    const cands = idx.filter(x => !x.isLink && (x.lower === want || x.lower.startsWith(want)));
    const pick = cands.find(x => x.gid === current && x.k !== srcK && x.lower === want)
              || cands.find(x => x.gid === current && x.k !== srcK)
              || cands.find(x => x.lower === want) || cands[0];
    if (pick) return flash(cellOf(pick.gid, pick.k));
    toast('Couldn’t find the target of “' + label + '” in this copy.');
  }
});

// --- search
const q = $('#q'), res = $('#results');
let hits = [], sel = -1;
function esc(s){return s.replace(/[&<>"]/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;'}[c]));}
function snippet(text, term){
  const i = text.toLowerCase().indexOf(term); const start = Math.max(0, i - 50);
  const s = (start ? '…' : '') + text.slice(start, i + term.length + 110) + (i + term.length + 110 < text.length ? '…' : '');
  return esc(s).replace(new RegExp(term.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'), 'ig'), m => '<mark>' + m + '</mark>');
}
function runSearch(){
  const term = q.value.trim().toLowerCase();
  if (term.length < 2) { res.classList.remove('open'); return; }
  const all = buildIndex().filter(x => x.lower.includes(term));
  // name-like cells (start with the term) first
  all.sort((a, b) => (b.lower.startsWith(term) - a.lower.startsWith(term)) || (a.text.length - b.text.length));
  hits = all.slice(0, 200); sel = -1;
  res.innerHTML = '<div class="info">' + all.length + ' match' + (all.length === 1 ? '' : 'es') + (all.length > 200 ? ' · showing 200' : '') + '</div>' +
    hits.map((h, i) => '<button class="hit" data-i="' + i + '"><span class="tab">' + esc(byGid[h.gid].name) + '</span><span class="row">' + (h.row ? 'row ' + h.row : '') + '</span><br>' + snippet(h.text, term) + '</button>').join('');
  res.classList.add('open');
}
let timer; q.addEventListener('input', () => { clearTimeout(timer); timer = setTimeout(runSearch, 150); });
q.addEventListener('focus', () => { if (q.value.trim().length >= 2) runSearch(); });
res.addEventListener('click', e => { const b = e.target.closest('.hit'); if (b) open(+b.dataset.i); });
function open(i){ const h = hits[i]; if (!h) return; res.classList.remove('open'); flash(cellOf(h.gid, h.k)); }
q.addEventListener('keydown', e => {
  const btns = res.querySelectorAll('.hit');
  if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
    e.preventDefault(); sel = Math.max(0, Math.min(btns.length - 1, sel + (e.key === 'ArrowDown' ? 1 : -1)));
    btns.forEach((b, i) => b.classList.toggle('sel', i === sel)); btns[sel]?.scrollIntoView({block: 'nearest'});
  } else if (e.key === 'Enter') { open(sel >= 0 ? sel : 0); }
  else if (e.key === 'Escape') { res.classList.remove('open'); q.blur(); }
});
document.addEventListener('keydown', e => {
  if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') { e.preventDefault(); q.focus(); q.select(); }
  if (e.altKey && (e.key === 'ArrowDown' || e.key === 'ArrowUp')) {   // Sheets: Alt+↑/↓ switches tabs
    e.preventDefault(); const i = TABS.findIndex(t => t.gid === current);
    show(TABS[(i + (e.key === 'ArrowDown' ? 1 : TABS.length - 1)) % TABS.length].gid);
  }
});

window.addEventListener('hashchange', () => { const m = location.hash.match(/gid=(-?\d+)/); if (m && m[1] !== current) show(m[1], false); });
const m0 = location.hash.match(/gid=(-?\d+)/);
show(m0 ? m0[1] : TABS[0].gid);
</script>
</body></html>
"""


# ----------------------------------------------------------------- main
def main():
    args = sys.argv[1:]
    no_images = "--no-images" in args
    args = [a for a in args if a != "--no-images"]
    if not args:
        sys.exit(__doc__)
    session = None
    if args[0] == "--rebuild":
        out = Path(args[1] if len(args) > 1 else ".")
        if not (out / "tabs.json").exists():
            sys.exit(f"'{out.resolve()}' has no tabs.json. Point --rebuild at the folder that holds "
                     f"tabs.json and raw/ (use '.' if you're already inside it).")
        setup_log(out)
    else:
        sid = sheet_id(args[0])
        session, out = download(sid, Path(args[1]) if len(args) > 1 else None)
    if not no_images:
        try:
            download_images(out, session)
        except ImportError:
            say("Images: skipped (pip install requests to download them)")
    build_index(out)
    build_readme(out)
    build_viewer(out)
    say(f"Done -> {out.resolve()}  (details in {out / 'sheet_dump.log'})")


if __name__ == "__main__":
    main()
