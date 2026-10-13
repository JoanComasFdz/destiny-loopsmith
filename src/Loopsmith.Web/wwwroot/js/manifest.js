// Loopsmith web host — Bungie's manifest, loaded when the app starts and kept for the tab: each component file is fetched
// from bungie.net, its bytes kept as the download gave them (in its chunks, never copied into one buffer) and every
// entry's place in them indexed by hash as the chunks arrive — in the same pass, which entries hold each
// "plugCategoryIdentifier". A download that breaks starts that file again from its start, a few times, before the load
// gives up (the files already in stay). The files are saved in the browser (IndexedDB, gzipped, as DIM saves its copy)
// with the URLs they came from (their version); a later visit reads them from there when .NET finds them still current,
// unpacking and indexing them as a download does. The listing of every weapon and armor piece is saved beside them,
// under the key .NET gives it. Core asks for entries by hash (for the entries whose JSON holds a piece of text, and for
// the entries holding a key's value, cut down to the fields it names) and interprets the raw JSON itself: no game logic
// lives here. Reading the files reports how far it got to .NET (a few times a second, and at once when a file starts
// again) and pauses every few megabytes, so the page keeps answering taps. Called from Hosting/BrowserInterop.cs.
window.loopsmithManifest = (() => {
  const tables = new Map();   // component name → { url, store, offsets: Map(hash → [start, end]), categories: Map(category → [hash]) }
  const decoder = new TextDecoder("utf-8");
  const QUOTE = 34, BACKSLASH = 92, OPEN = 123, CLOSE = 125, COMMA = 44, CLOSE_LIST = 93;
  const CATEGORY_KEY = new TextEncoder().encode("plugCategoryIdentifier");
  const CATEGORY_NEEDLE = /^"plugCategoryIdentifier":"([^"\\]*)"$/;
  const SLICE = 8 * 1024 * 1024;   // bytes indexed between two pauses, so the page keeps answering taps
  const REPORT_EVERY = 250;        // ms between two progress reports
  const RETRY_WAIT = 1000;         // ms before a broken file's next try (times the try's number)
  const DATABASE = "loopsmith-manifest", SAVED = "saved", FILES_KEY = "files", LISTING_KEY = "listing";
  let found = null;                // the saved copy readSaved found, until loadSaved reads it (one read of the database)

  const wait = (ms) => new Promise((resolve) => setTimeout(resolve, ms));
  const pause = () => wait(0);

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

  // How far the reading got (the bytes received, all components, unpacked: the browser undoes bungie.net's compression)
  // and which try the file being read is on (0: its first), told to .NET a few times a second, and at once when a file
  // starts again or comes in after a retry.
  function createProgress(reporter) {
    let bytes = 0, attempt = 0, last = 0;
    const report = () => {
      last = performance.now();
      if (reporter) reporter.invokeMethodAsync("ReportDownload", bytes, attempt).catch(() => {});
    };
    return {
      add(count) {
        bytes += count;
        if (performance.now() - last >= REPORT_EVERY) report();
      },
      // A file broke after `count` of its bytes: they no longer count, and it starts again, on its `next`-th try.
      restart(count, next) {
        bytes -= count;
        attempt = next;
        report();
      },
      // A file is in: the next one starts on its first try.
      finish() {
        if (attempt === 0) return;
        attempt = 0;
        report();
      },
      // Every file is in: the last bytes are told.
      flush() { report(); },
    };
  }

  // A component, downloaded and indexed once (a later load of the same file keeps it). A download that breaks starts
  // again from the file's start, up to `retries` more times, a moment later each time: bungie.net sends the files
  // compressed, and a browser can ask for the rest of one only uncompressed (~12 times the bytes of starting again).
  async function loadTable(name, url, retries, progress) {
    const kept = tables.get(name);
    if (kept && kept.url === url) return;
    for (let attempt = 0; ; attempt++) {
      let received = 0;
      try {
        const table = await downloadTable(name, url, (count) => { received += count; progress.add(count); });
        tables.set(name, { url, ...table });
        progress.finish();
        return;
      } catch (error) {
        if (attempt >= retries) throw error;
        progress.restart(received, attempt + 1);
        await wait(RETRY_WAIT * (attempt + 1));
      }
    }
  }

  // One try at a component: its bytes, indexed as they come (`counted` is told how many came each time).
  async function downloadTable(name, url, counted) {
    const response = await fetch(url, { mode: "cors", credentials: "omit" });
    if (!response.ok) throw new Error(`${name}: HTTP ${response.status}`);
    if (response.body && response.body.getReader) return await indexStream(response.body, counted);
    const { store, indexer, take } = createTaker(counted);
    await take(new Uint8Array(await response.arrayBuffer()));
    return { store, offsets: indexer.offsets, categories: indexer.categories };
  }

  // A component's bytes from a stream (a download, or a saved file unpacking), indexed as they come.
  async function indexStream(stream, counted) {
    const { store, indexer, take } = createTaker(counted);
    const reader = stream.getReader();
    for (let read = await reader.read(); !read.done; read = await reader.read()) await take(read.value);
    return { store, offsets: indexer.offsets, categories: indexer.categories };
  }

  // Where a component's bytes go: its store, indexed as they come (a big chunk a slice at a time), `counted` told how
  // many came each time, with a pause every few megabytes.
  function createTaker(counted) {
    const store = createStore(), indexer = createIndexer(store);
    let sincePause = 0;
    const take = async (chunk) => {
      for (let from = 0; from < chunk.length; from += SLICE) {
        const piece = chunk.subarray(from, Math.min(chunk.length, from + SLICE));
        addChunk(store, piece);
        indexer.feed(piece, store.length - piece.length);
        counted(piece.length);
        sincePause += piece.length;
        if (sincePause >= SLICE) { sincePause = 0; await pause(); }
      }
    };
    return { store, indexer, take };
  }

  // ── the copy saved in the browser ──────────────────────────────────────────────

  // The database, or null where the browser keeps none (a private window that refuses it, storage blocked).
  function openDatabase() {
    return new Promise((resolve) => {
      try {
        const opening = indexedDB.open(DATABASE, 1);
        opening.onupgradeneeded = () => opening.result.createObjectStore(SAVED);
        opening.onsuccess = () => resolve(opening.result);
        opening.onerror = () => resolve(null);
        opening.onblocked = () => resolve(null);
      } catch (_) {
        resolve(null);
      }
    });
  }

  // A saved record, or null when there's none or it can't be read.
  async function readRecord(key) {
    const database = await openDatabase();
    if (!database) return null;
    try {
      return await new Promise((resolve) => {
        const request = database.transaction(SAVED, "readonly").objectStore(SAVED).get(key);
        request.onsuccess = () => resolve(request.result ?? null);
        request.onerror = () => resolve(null);
      });
    } catch (_) {
      return null;
    } finally {
      database.close();
    }
  }

  // Saves a record in place of the one under its key; throws when it can't (no database, storage full).
  async function writeRecord(key, value) {
    const database = await openDatabase();
    if (!database) throw new Error("this browser keeps nothing saved for the app");
    try {
      await new Promise((resolve, reject) => {
        const transaction = database.transaction(SAVED, "readwrite");
        transaction.objectStore(SAVED).put(value, key);
        transaction.oncomplete = () => resolve();
        transaction.onerror = () => reject(transaction.error);
        transaction.onabort = () => reject(transaction.error || new Error("saving was cut short"));
      });
    } finally {
      database.close();
    }
  }

  // A component's bytes gzipped, chunk by chunk (never copied into one buffer first).
  async function compress(store) {
    let next = 0;
    const chunks = new ReadableStream({
      pull(controller) {
        if (next < store.chunks.length) controller.enqueue(store.chunks[next++]);
        else controller.close();
      },
    });
    return await new Response(chunks.pipeThrough(new CompressionStream("gzip"))).arrayBuffer();
  }

  const describe = (error) => String(error && error.message ? error.message : error);

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

    // Fetch and index the components ({ name: url }) not in yet, each tried again up to `retries` times when its
    // download breaks, telling `reporter` (a .NET object, or null) how many bytes came and which try a file is on; why
    // one couldn't be, or null when all are in. They stay for the tab.
    async loadTables(urls, retries, reporter) {
      found = null;   // a download instead of the saved copy: it isn't read
      try {
        const progress = createProgress(reporter);
        for (const [name, url] of Object.entries(urls)) await loadTable(name, url, retries, progress);
        return null;
      } catch (error) {
        return describe(error);
      }
    },

    // The copy the browser saved, as { urls: { name: url }, bytes } (bytes: its size unpacked), or null when there's none.
    async readSaved() {
      const record = await readRecord(FILES_KEY);
      found = record && record.urls && record.files ? record : null;
      return found ? JSON.stringify({ urls: found.urls, bytes: found.bytes || 0 }) : null;
    },

    // Reads the saved components ({ name: url }) not in yet into the tab, unpacking and indexing them as a download does,
    // telling `reporter` how many bytes are in; why it couldn't (the copy lacks one, or doesn't unpack), or null.
    async loadSaved(urls, reporter) {
      try {
        if (typeof DecompressionStream === "undefined") return "this browser can't unpack the saved copy";
        const record = found || await readRecord(FILES_KEY);
        found = null;
        const progress = createProgress(reporter);
        for (const [name, url] of Object.entries(urls)) {
          const kept = tables.get(name);
          if (kept && kept.url === url) continue;
          if (!record || !record.urls || record.urls[name] !== url || !record.files || !record.files[name]) return `the saved copy lacks ${name}`;
          const unpacked = new Response(record.files[name]).body.pipeThrough(new DecompressionStream("gzip"));
          const table = await indexStream(unpacked, (count) => progress.add(count));
          tables.set(name, { url, ...table });
        }
        progress.flush();
        return null;
      } catch (error) {
        return describe(error);
      }
    },

    // Saves the components the tab holds ({ name: url }), gzipped, in place of the copy saved before; why it couldn't
    // (a component isn't in, the browser can't compress, storage is full or blocked), or null.
    async saveTables(urls) {
      try {
        if (typeof CompressionStream === "undefined") return "this browser can't compress the copy";
        const files = {};
        let bytes = 0;
        for (const [name, url] of Object.entries(urls)) {
          const table = tables.get(name);
          if (!table || table.url !== url) return `${name} isn't in`;
          files[name] = await compress(table.store);
          bytes += table.store.length;
          await pause();
        }
        await writeRecord(FILES_KEY, { urls, bytes, files });
        return null;
      } catch (error) {
        return describe(error);
      }
    },

    // The listing saved under `key`, or null when there's none under it.
    async readSavedListing(key) {
      const record = await readRecord(LISTING_KEY);
      return record && record.key === key && typeof record.text === "string" ? record.text : null;
    },

    // Saves the listing under `key`, in place of the one saved before; why it couldn't, or null.
    async saveListing(key, text) {
      try {
        await writeRecord(LISTING_KEY, { key, text });
        return null;
      } catch (error) {
        return describe(error);
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

    // The entries of a table that hold "key":value for one of the values `pathsByValue` names (each as Bungie's JSON
    // writes it: 3, "x"), each cut down to that value's paths (arrays of property names, "*" for each element of an
    // array; one an entry lacks is left out), grouped by the value: { value: { hash: { …parts… } } }. One pass over the
    // bytes finds every "key":, however many values are asked; none asked, none read.
    readEntryParts(table, key, pathsByValue) {
      const groups = {};
      const store = tables.get(table);
      const wanted = new Map(Object.entries(pathsByValue));
      if (!store || wanted.size === 0) return JSON.stringify(groups);
      const starts = [...store.offsets].map(([hash, [start, end]]) => [start, end, hash]).sort((a, b) => a[0] - b[0]);
      for (const at of findAll(store.store, new TextEncoder().encode(JSON.stringify(key) + ":"))) {
        const value = readValue(store.store, at + key.length + 3);
        const entry = wanted.has(value) ? findEntry(starts, at) : null;
        if (!entry) continue;
        const group = groups[value] || (groups[value] = {});
        if (!group[entry[2]]) group[entry[2]] = cutEntry(JSON.parse(decoder.decode(readBytes(store.store, entry[0], entry[1]))), wanted.get(value)) || {};
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

  // A value cut down to the paths it has, nested as they were: an object keeps the properties the paths name (one that
  // keeps none is left out), an array maps "*" over its elements (an element that keeps nothing is null, so the
  // elements keep their places), and an empty path keeps the value whole.
  function cutEntry(value, paths) {
    if (paths.some((path) => path.length === 0)) return value;
    if (Array.isArray(value)) {
      const rest = paths.filter((path) => path[0] === "*").map((path) => path.slice(1));
      return rest.length ? value.map((element) => { const kept = cutEntry(element, rest); return kept === undefined ? null : kept; }) : undefined;
    }
    if (value === null || typeof value !== "object") return undefined;
    const byName = new Map();
    for (const path of paths) {
      if (path[0] === "*") continue;
      if (!byName.has(path[0])) byName.set(path[0], []);
      byName.get(path[0]).push(path.slice(1));
    }
    const parts = {};
    for (const [name, rest] of byName) {
      if (!Object.prototype.hasOwnProperty.call(value, name)) continue;
      const kept = cutEntry(value[name], rest);
      if (kept !== undefined) parts[name] = kept;
    }
    return Object.keys(parts).length ? parts : undefined;
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
