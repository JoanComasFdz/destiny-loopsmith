// Loopsmith web host — Bungie's manifest, downloaded on demand: each component file is fetched from bungie.net (the
// browser's HTTP cache keeps it; the app stores nothing), its bytes kept as they came, and every entry's place in
// them indexed by hash — in the same pass, which entries hold each "plugCategoryIdentifier". Core asks for entries by
// hash (and for the entries whose JSON holds a piece of text) and interprets the raw JSON itself: no game logic lives
// here. Called from Hosting/BrowserInterop.cs.
window.loopsmithManifest = (() => {
  const tables = new Map();   // component name → { bytes, offsets: Map(hash → [start, end]), categories: Map(category → [hash]) }
  const decoder = new TextDecoder("utf-8");
  const QUOTE = 34, BACKSLASH = 92, OPEN = 123, CLOSE = 125, COMMA = 44;
  const CATEGORY_KEY = new TextEncoder().encode("plugCategoryIdentifier");
  const CATEGORY_NEEDLE = /^"plugCategoryIdentifier":"([^"\\]*)"$/;
  const SLICE = 8 * 1024 * 1024;   // bytes indexed between two pauses, so the page keeps answering taps

  // The top-level object's entries: "hash": { … } → the byte range of { … }; and each "plugCategoryIdentifier": "…"
  // inside an entry → that entry's hash. Strings are skipped whole, so braces and quotes inside them don't count. The
  // pass pauses every few megabytes (the item component is ~200 MB) so the page isn't frozen while it runs.
  async function indexEntries(bytes) {
    const offsets = new Map(), categories = new Map();
    let depth = 0, inString = false, stringStart = -1, keyStart = -1, key = null, valueStart = -1, expectCategory = false;
    for (let i = 0, pause = SLICE; i < bytes.length; i++) {
      if (i >= pause) { pause += SLICE; await new Promise((resolve) => setTimeout(resolve, 0)); }
      const b = bytes[i];
      if (inString) {
        if (b === BACKSLASH) { i++; continue; }
        if (b === QUOTE) {
          inString = false;
          if (depth === 1 && keyStart >= 0) { key = decoder.decode(bytes.subarray(keyStart, i)); keyStart = -1; }
          else if (depth > 1 && key !== null) {
            if (expectCategory) { addCategory(categories, decoder.decode(bytes.subarray(stringStart, i)), key); expectCategory = false; }
            else if (i - stringStart === CATEGORY_KEY.length && startsWith(bytes, stringStart, CATEGORY_KEY)) expectCategory = true;
          }
        }
        continue;
      }
      if (b === QUOTE) { inString = true; stringStart = i + 1; if (depth === 1 && key === null) keyStart = i + 1; continue; }
      if (b === OPEN) { expectCategory = false; if (depth === 1 && key !== null) valueStart = i; depth++; continue; }
      if (b === CLOSE) {
        expectCategory = false;
        depth--;
        if (depth === 1 && key !== null && valueStart >= 0) { offsets.set(key, [valueStart, i + 1]); key = null; valueStart = -1; }
        continue;
      }
      if (b === COMMA) expectCategory = false;
    }
    return { offsets, categories };
  }

  function addCategory(categories, category, key) {
    const hashes = categories.get(category);
    if (!hashes) categories.set(category, [Number(key)]);
    else if (hashes[hashes.length - 1] !== Number(key)) hashes.push(Number(key));
  }

  function startsWith(bytes, at, prefix) {
    for (let j = 0; j < prefix.length; j++) if (bytes[at + j] !== prefix[j]) return false;
    return true;
  }

  async function loadTable(name, url) {
    if (tables.has(name)) return;
    const response = await fetch(url, { mode: "cors", credentials: "omit" });
    if (!response.ok) throw new Error(`${name}: HTTP ${response.status}`);
    const bytes = new Uint8Array(await response.arrayBuffer());
    tables.set(name, { bytes, ...(await indexEntries(bytes)) });
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

    // Fetch and index the components ({ name: url }); why one couldn't be, or null when all are in.
    async loadTables(urls) {
      try {
        for (const [name, url] of Object.entries(urls)) await loadTable(name, url);
        return null;
      } catch (error) {
        return String(error && error.message ? error.message : error);
      }
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
          if (range) entries.push(JSON.stringify(key) + ":" + decoder.decode(table.bytes.subarray(range[0], range[1])));
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
        const bytes = encoder.encode(needle);
        const hashes = new Set();
        for (let at = indexOf(store.bytes, bytes, 0); at >= 0; at = indexOf(store.bytes, bytes, at + 1)) {
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

  function indexOf(bytes, needle, from) {
    const first = needle[0], last = bytes.length - needle.length;
    outer: for (let i = bytes.indexOf(first, from); i >= 0 && i <= last; i = bytes.indexOf(first, i + 1)) {
      for (let j = 1; j < needle.length; j++) if (bytes[i + j] !== needle[j]) continue outer;
      return i;
    }
    return -1;
  }
})();
