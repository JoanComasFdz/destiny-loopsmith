// Loopsmith web host — Bungie's manifest, downloaded on demand: each component file is fetched from bungie.net (the
// browser's HTTP cache keeps it; the app stores nothing), its bytes kept as they came, and every entry's place in
// them indexed by hash. Core asks for entries by hash (and for the entries whose JSON holds a piece of text) and
// interprets the raw JSON itself: no game logic lives here. Called from Hosting/BrowserInterop.cs.
window.loopsmithManifest = (() => {
  const tables = new Map();   // component name → { bytes, offsets: Map(hash → [start, end]) }
  const decoder = new TextDecoder("utf-8");
  const QUOTE = 34, BACKSLASH = 92, OPEN = 123, CLOSE = 125, COLON = 58;

  // The top-level object's entries: "hash": { … } → the byte range of { … }. Strings are skipped whole, so braces
  // and quotes inside them don't count.
  function indexEntries(bytes) {
    const offsets = new Map();
    let depth = 0, inString = false, keyStart = -1, key = null, valueStart = -1;
    for (let i = 0; i < bytes.length; i++) {
      const b = bytes[i];
      if (inString) {
        if (b === BACKSLASH) { i++; continue; }
        if (b === QUOTE) {
          inString = false;
          if (depth === 1 && keyStart >= 0) { key = decoder.decode(bytes.subarray(keyStart, i)); keyStart = -1; }
        }
        continue;
      }
      if (b === QUOTE) { inString = true; if (depth === 1 && key === null) keyStart = i + 1; continue; }
      if (b === OPEN) { if (depth === 1 && key !== null) valueStart = i; depth++; continue; }
      if (b === CLOSE) {
        depth--;
        if (depth === 1 && key !== null && valueStart >= 0) { offsets.set(key, [valueStart, i + 1]); key = null; valueStart = -1; }
        continue;
      }
      if (b === COLON) continue;
    }
    return offsets;
  }

  async function loadTable(name, url) {
    if (tables.has(name)) return;
    const response = await fetch(url, { mode: "cors", credentials: "omit" });
    if (!response.ok) throw new Error(`${name}: HTTP ${response.status}`);
    const bytes = new Uint8Array(await response.arrayBuffer());
    tables.set(name, { bytes, offsets: indexEntries(bytes) });
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

    // The raw JSON of each entry asked for ({ table: [hashes] } → { table: { hash: json } }); a missing one is left out.
    // An empty list asks for the whole table (a small one).
    readEntries(request) {
      const answer = {};
      for (const [name, hashes] of Object.entries(request)) {
        const table = tables.get(name);
        if (!table) continue;
        const keys = hashes.length ? hashes.map(String) : [...table.offsets.keys()];
        const entries = {};
        for (const key of keys) {
          const range = table.offsets.get(key);
          if (range) entries[key] = decoder.decode(table.bytes.subarray(range[0], range[1]));
        }
        answer[name] = entries;
      }
      return JSON.stringify(answer);
    },

    // The hashes of a table's entries whose JSON holds each needle ({ needle: [hashes] }): one pass over the bytes per
    // needle, each match placed in the entry that holds it.
    findEntriesContaining(table, needles) {
      const found = {};
      const store = tables.get(table);
      if (!store) return JSON.stringify(found);
      const encoder = new TextEncoder();
      const starts = [...store.offsets].map(([key, [start, end]]) => [start, end, Number(key)]).sort((a, b) => a[0] - b[0]);
      for (const needle of needles) {
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
