// Loopsmith web host — Bungie's manifest, downloaded on demand: each component file is fetched from bungie.net (the
// browser's HTTP cache keeps it; the app stores nothing), its bytes kept as the download gave them (in its chunks, never
// copied into one buffer) and every entry's place in them indexed by hash as the chunks arrive — in the same pass, which
// entries hold each "plugCategoryIdentifier". Core asks for entries by hash (and for the entries whose JSON holds a
// piece of text) and interprets the raw JSON itself: no game logic lives here. The download reports how far it got to
// .NET (a few times a second) and pauses every few megabytes, so the page keeps answering taps; once Core has what it
// asked for, the tables are let go (a later read downloads them again, from the HTTP cache). Called from
// Hosting/BrowserInterop.cs.
window.loopsmithManifest = (() => {
  const tables = new Map();   // component name → { store, offsets: Map(hash → [start, end]), categories: Map(category → [hash]) }
  const decoder = new TextDecoder("utf-8");
  const QUOTE = 34, BACKSLASH = 92, OPEN = 123, CLOSE = 125, COMMA = 44;
  const CATEGORY_KEY = new TextEncoder().encode("plugCategoryIdentifier");
  const CATEGORY_NEEDLE = /^"plugCategoryIdentifier":"([^"\\]*)"$/;
  const SLICE = 8 * 1024 * 1024;   // bytes indexed between two pauses, so the page keeps answering taps
  const REPORT_EVERY = 250;        // ms between two progress reports

  const pause = () => new Promise((resolve) => setTimeout(resolve, 0));

  // ── a component's bytes, in the chunks the download gave ───────────────────────

  function createStore() { return { chunks: [], starts: [], length: 0 }; }

  function addChunk(store, chunk) {
    store.starts.push(store.length);
    store.chunks.push(chunk);
    store.length += chunk.length;
  }

  // The chunk holding byte `at`.
  function findChunk(store, at) {
    let low = 0, high = store.starts.length - 1;
    while (low < high) {
      const middle = (low + high + 1) >> 1;
      if (store.starts[middle] <= at) low = middle; else high = middle - 1;
    }
    return low;
  }

  // Bytes [start, end): a view when one chunk holds them, else a copy of the pieces.
  function readBytes(store, start, end) {
    const first = findChunk(store, start);
    const offset = start - store.starts[first];
    if (offset + (end - start) <= store.chunks[first].length) return store.chunks[first].subarray(offset, offset + (end - start));
    const out = new Uint8Array(end - start);
    for (let c = first, at = start; at < end; c++) {
      const from = at - store.starts[c];
      const piece = store.chunks[c].subarray(from, Math.min(store.chunks[c].length, from + (end - at)));
      out.set(piece, at - start);
      at += piece.length;
    }
    return out;
  }

  // ── the index, built as the chunks arrive ──────────────────────────────────────

  // The top-level object's entries: "hash": { … } → the byte range of { … }; and each "plugCategoryIdentifier": "…"
  // inside an entry → that entry's hash. Strings are skipped whole (an escape may end one chunk and its character
  // start the next), so braces and quotes inside them don't count. Offsets are the component's, not the chunk's.
  function createIndexer(store) {
    const offsets = new Map(), categories = new Map();
    let depth = 0, inString = false, escaped = false, stringStart = -1, keyStart = -1, key = null, valueStart = -1, expectCategory = false;
    function feed(chunk, base) {
      for (let k = 0; k < chunk.length; k++) {
        const b = chunk[k];
        if (inString) {
          if (escaped) { escaped = false; continue; }
          if (b === BACKSLASH) { escaped = true; continue; }
          if (b === QUOTE) {
            const i = base + k;
            inString = false;
            if (depth === 1 && keyStart >= 0) { key = decoder.decode(readBytes(store, keyStart, i)); keyStart = -1; }
            else if (depth > 1 && key !== null) {
              if (expectCategory) { addCategory(categories, decoder.decode(readBytes(store, stringStart, i)), key); expectCategory = false; }
              else if (i - stringStart === CATEGORY_KEY.length && equals(readBytes(store, stringStart, i), CATEGORY_KEY)) expectCategory = true;
            }
          }
          continue;
        }
        if (b === QUOTE) { inString = true; stringStart = base + k + 1; if (depth === 1 && key === null) keyStart = base + k + 1; continue; }
        if (b === OPEN) { expectCategory = false; if (depth === 1 && key !== null) valueStart = base + k; depth++; continue; }
        if (b === CLOSE) {
          expectCategory = false;
          depth--;
          if (depth === 1 && key !== null && valueStart >= 0) { offsets.set(key, [valueStart, base + k + 1]); key = null; valueStart = -1; }
          continue;
        }
        if (b === COMMA) expectCategory = false;
      }
    }
    return { feed, offsets, categories };
  }

  function addCategory(categories, category, key) {
    const hashes = categories.get(category);
    if (!hashes) categories.set(category, [Number(key)]);
    else if (hashes[hashes.length - 1] !== Number(key)) hashes.push(Number(key));
  }

  function equals(bytes, other) {
    if (bytes.length !== other.length) return false;
    for (let j = 0; j < other.length; j++) if (bytes[j] !== other[j]) return false;
    return true;
  }

  // ── the download ───────────────────────────────────────────────────────────────

  // How far the reading got (bytes downloaded, all components), told to .NET a few times a second.
  function createProgress(reporter) {
    let bytes = 0, last = 0;
    return {
      add(count) {
        bytes += count;
        const now = performance.now();
        if (reporter && now - last >= REPORT_EVERY) { last = now; reporter.invokeMethodAsync("ReportDownload", bytes).catch(() => {}); }
      },
    };
  }

  async function loadTable(name, url, progress) {
    if (tables.has(name)) return;
    const response = await fetch(url, { mode: "cors", credentials: "omit" });
    if (!response.ok) throw new Error(`${name}: HTTP ${response.status}`);
    const store = createStore(), indexer = createIndexer(store);
    let sincePause = 0;
    const take = async (chunk) => {
      for (let from = 0; from < chunk.length; from += SLICE) {   // a big chunk (no stream) is indexed a slice at a time
        const piece = chunk.subarray(from, Math.min(chunk.length, from + SLICE));
        addChunk(store, piece);
        indexer.feed(piece, store.length - piece.length);
        progress.add(piece.length);
        sincePause += piece.length;
        if (sincePause >= SLICE) { sincePause = 0; await pause(); }
      }
    };
    if (response.body && response.body.getReader) {
      const reader = response.body.getReader();
      for (let read = await reader.read(); !read.done; read = await reader.read()) await take(read.value);
    } else {
      await take(new Uint8Array(await response.arrayBuffer()));
    }
    tables.set(name, { store, offsets: indexer.offsets, categories: indexer.categories });
  }

  return {
    // The manifest's index (its component paths), or null when bungie.net doesn't answer it.
    async readIndex(url, apiKey) {
      try {
        const response = await fetch(url, { headers: apiKey ? { "X-API-Key": apiKey } : {}, credentials: "omit" });
        return response.ok ? await response.text() : null;
      } catch (_) {
        return null;
      }
    },

    // Fetch and index the components ({ name: url }), telling `reporter` (a .NET object, or null) how many bytes came;
    // why one couldn't be, or null when all are in.
    async loadTables(urls, reporter) {
      try {
        const progress = createProgress(reporter);
        for (const [name, url] of Object.entries(urls)) await loadTable(name, url, progress);
        return null;
      } catch (error) {
        return String(error && error.message ? error.message : error);
      }
    },

    // Let the components go (their bytes are most of the tab's memory); a later read downloads them again.
    releaseTables() {
      tables.clear();
    },

    // Each entry asked for, as the manifest has it ({ table: [hashes] } → { table: { hash: { …entry… } } }: the entries'
    // own JSON, not strings of it, so Core parses each once); a missing one is left out. An empty list asks for the
    // whole table (a small one).
    readEntries(request) {
      const parts = [];
      for (const [name, hashes] of Object.entries(request)) {
        const table = tables.get(name);
        if (!table) continue;
        const keys = hashes.length ? hashes.map(String) : [...table.offsets.keys()];
        const entries = [];
        for (const key of keys) {
          const range = table.offsets.get(key);
          if (range) entries.push(JSON.stringify(key) + ":" + decoder.decode(readBytes(table.store, range[0], range[1])));
        }
        parts.push(JSON.stringify(name) + ":{" + entries.join(",") + "}");
      }
      return "{" + parts.join(",") + "}";
    },

    // The hashes of a table's entries whose JSON holds each needle ({ needle: [hashes] }). A plug category's needle
    // ("plugCategoryIdentifier":"…") is answered from the index the load built; any other, by a pass over the bytes,
    // each match placed in the entry that holds it.
    findEntriesContaining(table, needles) {
      const found = {};
      const store = tables.get(table);
      if (!store) return JSON.stringify(found);
      const encoder = new TextEncoder();
      let starts = null;
      for (const needle of needles) {
        const category = CATEGORY_NEEDLE.exec(needle);
        if (category) {
          found[needle] = [...(store.categories.get(category[1]) || [])].sort((a, b) => a - b);
          continue;
        }
        starts = starts || [...store.offsets].map(([key, [start, end]]) => [start, end, Number(key)]).sort((a, b) => a[0] - b[0]);
        const hashes = new Set();
        for (const at of findAll(store.store, encoder.encode(needle))) {
          const entry = findEntry(starts, at);
          if (entry) hashes.add(entry[2]);
        }
        found[needle] = [...hashes].sort((a, b) => a - b);
      }
      return JSON.stringify(found);
    },
  };

  function findEntry(starts, at) {
    let low = 0, high = starts.length - 1;
    while (low <= high) {
      const middle = (low + high) >> 1;
      if (starts[middle][0] > at) high = middle - 1;
      else if (starts[middle][1] <= at) low = middle + 1;
      else return starts[middle];
    }
    return null;
  }

  // Every place the needle starts, over the chunks (a match may cross from one chunk into the next).
  function findAll(store, needle) {
    const places = [];
    let tail = new Uint8Array(0);
    for (let c = 0; c < store.chunks.length; c++) {
      const window = new Uint8Array(tail.length + store.chunks[c].length);
      window.set(tail, 0);
      window.set(store.chunks[c], tail.length);
      const base = store.starts[c] - tail.length;
      // the tail is shorter than the needle, so a match in the window ends in this chunk: none is found twice
      for (let at = indexOf(window, needle, 0); at >= 0; at = indexOf(window, needle, at + 1)) places.push(at + base);
      tail = window.subarray(Math.max(0, window.length - (needle.length - 1)));
    }
    return places;
  }

  function indexOf(bytes, needle, from) {
    const first = needle[0], last = bytes.length - needle.length;
    outer: for (let i = bytes.indexOf(first, from); i >= 0 && i <= last; i = bytes.indexOf(first, i + 1)) {
      for (let j = 1; j < needle.length; j++) if (bytes[i + j] !== needle[j]) continue outer;
      return i;
    }
    return -1;
  }
})();
