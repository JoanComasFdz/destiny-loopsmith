// Loopsmith web host — Bungie's manifest, downloaded on demand: each component file is fetched from bungie.net (the
// browser's HTTP cache keeps it; the app stores nothing), its bytes kept as the download gave them (in its chunks, never
// copied into one buffer) and every entry's place in them indexed by hash as the chunks arrive — in the same pass, which
// entries hold each "plugCategoryIdentifier". Core asks for entries by hash (for the entries whose JSON holds a piece of
// text, and for the entries holding a key's value, cut down to the fields it names) and interprets the raw JSON itself:
// no game logic lives here. The download reports how far it got to
// .NET (a few times a second) and pauses every few megabytes, so the page keeps answering taps; once Core has what it
// asked for, the tables are let go (a later read downloads them again, from the HTTP cache). Called from
// Hosting/BrowserInterop.cs.
window.loopsmithManifest = (() => {
  const tables = new Map();   // component name → { store, offsets: Map(hash → [start, end]), categories: Map(category → [hash]) }
  const decoder = new TextDecoder("utf-8");
  const QUOTE = 34, BACKSLASH = 92, OPEN = 123, CLOSE = 125, COMMA = 44, CLOSE_LIST = 93;
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

  // How far the reading got (the bytes received, all components, unpacked: the browser undoes bungie.net's compression),
  // told to .NET a few times a second.
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

    // The entries of a table that hold "key":value for one of the values (each as Bungie's JSON writes it: 3, "x"), each
    // cut down to the paths (arrays of property names; one an entry lacks is left out), grouped by the value:
    // { value: { hash: { …parts… } } }. One pass over the bytes finds every "key":, however many values are asked.
    readEntryParts(table, key, values, paths) {
      const groups = {};
      const store = tables.get(table);
      if (!store) return JSON.stringify(groups);
      const wanted = new Set(values);
      const starts = [...store.offsets].map(([hash, [start, end]]) => [start, end, hash]).sort((a, b) => a[0] - b[0]);
      for (const at of findAll(store.store, new TextEncoder().encode(JSON.stringify(key) + ":"))) {
        const value = readValue(store.store, at + key.length + 3);
        const entry = wanted.has(value) ? findEntry(starts, at) : null;
        if (!entry) continue;
        const group = groups[value] || (groups[value] = {});
        if (!group[entry[2]]) group[entry[2]] = cutEntry(JSON.parse(decoder.decode(readBytes(store.store, entry[0], entry[1]))), paths);
      }
      return JSON.stringify(groups);
    },
  };

  // The scalar that starts at byte `from` (a key's value), as the JSON writes it: a string with its quotes, else up to
  // the next comma or closing bracket. Values longer than a short scalar aren't asked for.
  function readValue(store, from) {
    const bytes = readBytes(store, from, Math.min(store.length, from + 64));
    let end = 0;
    if (bytes[0] === QUOTE) {
      for (end = 1; end < bytes.length && bytes[end] !== QUOTE; end++) if (bytes[end] === BACKSLASH) end++;
      end++;
    } else {
      while (end < bytes.length && bytes[end] !== COMMA && bytes[end] !== CLOSE && bytes[end] !== CLOSE_LIST) end++;
    }
    return decoder.decode(bytes.subarray(0, end));
  }

  // An entry cut down to the paths it has, nested as they were.
  function cutEntry(entry, paths) {
    const parts = {};
    for (const path of paths) {
      let value = entry;
      for (const name of path) value = value !== null && typeof value === "object" && Object.prototype.hasOwnProperty.call(value, name) ? value[name] : undefined;
      if (value === undefined) continue;
      let into = parts;
      for (const name of path.slice(0, -1)) into = into[name] || (into[name] = {});
      into[path[path.length - 1]] = value;
    }
    return parts;
  }

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

  // Every place the needle starts, over the chunks: each chunk searched where it is, and at each boundary a small window
  // of the bytes around it (a match may cross from one chunk into the next: one that starts before the boundary and
  // ends after it, so none is found twice). A chunk shorter than the needle is covered by the windows on either side.
  function findAll(store, needle) {
    const places = [], reach = needle.length - 1;
    for (let c = 0; c < store.chunks.length; c++) {
      const chunk = store.chunks[c], base = store.starts[c];
      for (let at = indexOf(chunk, needle, 0); at >= 0; at = indexOf(chunk, needle, at + 1)) places.push(base + at);
      if (reach > 0 && c + 1 < store.chunks.length) {
        const from = Math.max(0, base + chunk.length - reach), to = Math.min(store.length, base + chunk.length + reach);
        const window = readBytes(store, from, to), boundary = base + chunk.length - from;
        for (let at = indexOf(window, needle, 0); at >= 0; at = indexOf(window, needle, at + 1)) {
          if (at < boundary && at + needle.length > boundary) places.push(from + at);
        }
      }
    }
    return places.sort((a, b) => a - b);
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
