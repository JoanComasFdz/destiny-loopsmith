// Loopsmith e2e recordings: shows each e2e scenario's recordings (the GIFs and MP4s the "E2E videos" workflow keeps in
// the prs-resources release, or a rule scenario's in its rule group's rules-<group>-resources) next to its code in VS Code: a CodeLens above the scenario's method, a ▷ button in
// the editor's title bar and a panel beside the editor that plays the MP4 (README.md).
// One file, no build and no dependencies: VS Code loads it as it is, in a browser (vscode.dev) and on the desktop.
// The pure functions come first (test/extension.test.js tests them); activate() is the shell around them: it reads the
// releases from GitHub, the scenario files from the editor, and draws what the pure functions describe.
'use strict';

/** The longest asset name the workflow writes (tools/e2e ReleaseNaming.MaxName). */
const MAX_ASSET_NAME = 200;
/** The screens a scenario is played on, in the order they show. */
const SCREENS = ['desktop', 'mobile'];
/** pr-<n>-v<version>-<run>.gif / .mp4 (docs/hosting.md, "E2E videos on pull requests", "Naming"). */
const ASSET_NAME = /^pr-(\d+)-v(\d+)-.+\.(gif|mp4)$/;
/** How long a read of a release stays fresh before the next CodeLens reads it again. */
const FRESH_FOR_MS = 10 * 60 * 1000;
/** The GitHub sign-ins VS Code may already hold (vscode.dev, the GitHub extensions): any of them reads a public release. */
const KNOWN_GITHUB_SCOPES = [
  ['repo', 'workflow'],
  ['repo'],
  ['read:user', 'repo', 'user:email', 'workflow'],
  ['read:user', 'user:email'],
  ['user:email'],
  ['read:user'],
];
const SIGN_IN_SCOPES = ['read:user'];
const PLAY_COMMAND = 'loopsmith.e2eRecordings.play';
const PLAY_HERE_COMMAND = 'loopsmith.e2eRecordings.playHere';
const REFRESH_COMMAND = 'loopsmith.e2eRecordings.refresh';
const SIGN_IN_COMMAND = 'loopsmith.e2eRecordings.signIn';
const SCENARIO_FILES = '**/{Scenarios,Rules}/**/*Tests.cs';

// ---- Pure: names --------------------------------------------------------------------------------------------------

/** "StartFromScratchOpensAnArcHunter" → "start-from-scratch-opens-an-arc-hunter": Humanizer's Kebaberize. */
function toKebabCase(name) {
  return name
    .replace(/(\p{Lu}+)(\p{Lu}\p{Ll})/gu, '$1_$2')
    .replace(/([\p{Ll}\d])(\p{Lu})/gu, '$1_$2')
    .replace(/[-\s]/g, '_')
    .toLowerCase()
    .replace(/_/g, '-');
}

/** A run's GIF name: the method kebab-cased, then its screen (ScenarioNaming.ToGifName). */
function toGifName(method, screen) {
  return `${toKebabCase(method)}-${screen}`;
}

/**
 * "Starting", "Build/Subclass": the namespace after its ".Scenarios."; "Rules/Hunter/Arc/Melees": a rule scenario's, its
 * namespace from "Rules" on (ScenarioNaming.ToDomainFolder).
 */
function toDomainFolder(namespace) {
  const scenarios = namespace.indexOf('.Scenarios.');
  if (scenarios >= 0) {
    return namespace.slice(scenarios + '.Scenarios.'.length).replace(/\./g, '/');
  }
  const rules = namespace.indexOf('.Rules.');
  return rules < 0 ? 'Unsorted' : namespace.slice(rules + 1).replace(/\./g, '/');
}

/**
 * The release that holds a scenario's recordings (tools/e2e ReleaseNaming.FindRelease): a rule scenario's (Rules/Hunter/Arc/Melees)
 * is its rule group's, rules-hunter-arc-melees-resources; any other's is the setting's (prs-resources).
 */
function toRelease(domain, release) {
  const folders = domain.split('/');
  return folders.length < 2 || folders[0] !== 'Rules' ? release : `rules-${folders.slice(1).map(toKebabCase).join('-')}-resources`;
}

/** "Starting/start-from-scratch-…-desktop.gif": what the workflow hashes for a name past 200 characters. */
function toGifPath(domain, gifName) {
  return `${domain}/${gifName}.gif`;
}

/**
 * The asset name the workflow gives a run's GIF or MP4 (tools/e2e ReleaseNaming.NameAsset): pr-<n>-v<version>-<GIF name>,
 * or, past 200 characters, its start and "-<digest>", the first 8 hex digits of the SHA-256 of its GIF path.
 */
function nameAsset(pr, version, gifName, extension, digest) {
  const stem = `pr-${pr}-v${version}-${gifName}`;
  if (stem.length + extension.length <= MAX_ASSET_NAME) {
    return stem + extension;
  }
  const keep = MAX_ASSET_NAME - `-${digest}${extension}`.length;
  return `${stem.slice(0, keep).replace(/-+$/, '')}-${digest}${extension}`;
}

/**
 * https://<owner, lowercase>.github.io/<repo>/e2e-player.html?v=<MP4>, and &r=<release> for a rule group's release (the
 * page reads prs-resources when it names none, and plays from no other): the site's player page (link_player).
 */
function toPlayerUrl(repository, mp4Name, release) {
  const [owner, repo] = repository.split('/');
  const from = release.startsWith('rules-') ? `&r=${encodeURIComponent(release)}` : '';
  return `https://${owner.toLowerCase()}.github.io/${repo}/e2e-player.html?v=${encodeURIComponent(mp4Name)}${from}`;
}

/** "PR #109 · V1". */
function toVersionLabel(recording) {
  return `PR #${recording.pr} · V${recording.version}`;
}

/** "Desktop", "Mobile". */
function toScreenLabel(screen) {
  return screen === 'desktop' ? 'Desktop' : 'Mobile';
}

// ---- Pure: scenarios in a file ------------------------------------------------------------------------------------

/**
 * The scenarios of a C# file: each method with a [TestDescription("""…""")] (tests/*E2E/E2E-TEST-CODE-RULES.md, rule a),
 * with its Gherkin's title, the screens its [PlayInDesktop] / [PlayInMobile] name, its class and domain folder, the line
 * its attributes start on (where "Play e2e recording of this scenario" finds it from) and the line of its declaration
 * (its CodeLens goes there, between the attributes and the declaration, with other extensions' CodeLens).
 */
function findScenarios(text) {
  const lines = text.split(/\r?\n/);
  const namespace = /^\s*namespace\s+([\w.]+)/m.exec(text);
  const domain = toDomainFolder(namespace ? namespace[1] : '');
  return lines
    .map((line, at) => (/^\s*\[.*\bTestDescription\s*\(/.test(line) ? readScenario(lines, at, domain) : null))
    .filter((scenario) => scenario !== null);
}

function readScenario(lines, described, domain) {
  const open = findIndexFrom(lines, described, (line) => line.includes('"""'));
  const close = open < 0 ? -1 : findIndexFrom(lines, open + 1, (line) => line.trim().startsWith('"""'));
  const declared = close < 0 ? -1 : findIndexFrom(lines, close, (line) => /\b(?:public|internal|protected|private)\b/.test(line));
  const declaration = declared < 0 ? null : /\b(?:public|internal|protected|private)\b.*?\b(\w+)\s*\(/.exec(lines[declared]);
  if (declaration === null || declared - close > 3) {
    return null;
  }
  const method = declaration[1];
  const first = findAttributesStart(lines, described);
  const attributes = [...lines.slice(first, open + 1), ...lines.slice(close, declared)].join('\n');
  const title = lines.slice(open + 1, close).map((line) => line.trim()).find((line) => line.startsWith('Scenario:'));
  return Object.freeze({
    method,
    title: title ? title.slice('Scenario:'.length).trim() : method,
    className: findClassName(lines, described),
    domain,
    screens: SCREENS.filter((screen) => new RegExp(`\\bPlayIn${toScreenLabel(screen)}\\b`).test(attributes)),
    attributesLine: first,
    nameLine: declared,
  });
}

function findIndexFrom(lines, from, matches) {
  const found = lines.slice(from).findIndex(matches);
  return found < 0 ? -1 : from + found;
}

// The attribute lines right above the [TestDescription] line ([Theory, PlayInDesktop, PlayInMobile]).
function findAttributesStart(lines, described) {
  const above = lines.slice(0, described).reverse().findIndex((line) => !line.trim().startsWith('['));
  return above < 0 ? 0 : described - above;
}

function findClassName(lines, before) {
  const classes = lines.slice(0, before).map((line) => /\bclass\s+(\w+)/.exec(line)).filter((found) => found !== null);
  return classes.length === 0 ? '' : classes[classes.length - 1][1];
}

/**
 * The scenario the cursor is in, for "Play e2e recording of this scenario": the last one whose attributes start on or
 * above the line, or the file's first one when the line is above them all (the class's lines). Null when the file has none.
 */
function findScenarioAround(scenarios, line) {
  const above = scenarios.filter((scenario) => scenario.attributesLine <= line);
  return above.length > 0 ? above[above.length - 1] : scenarios[0] ?? null;
}

// ---- Pure: recordings ---------------------------------------------------------------------------------------------

/**
 * A scenario's recordings among the release's assets ({ name, url }), newest first (the highest PR, then its highest
 * version): each { pr, version, runs: { desktop: { gif, mp4 }, mobile: … } }, a run holding the assets it has.
 * digests holds, per screen, the digest of its GIF path (toGifPath), for the names past 200 characters.
 */
function findRecordings(scenario, assets, digests) {
  const found = assets.flatMap((asset) => matchAsset(scenario, asset, digests));
  const versions = [...new Set(found.map((run) => `${run.pr} ${run.version}`))].map((key) => {
    const [pr, version] = key.split(' ').map(Number);
    const own = found.filter((run) => run.pr === pr && run.version === version);
    const runs = Object.fromEntries(
      scenario.screens
        .filter((screen) => own.some((run) => run.screen === screen))
        .map((screen) => [screen, Object.fromEntries(own.filter((run) => run.screen === screen).map((run) => [run.kind, run.asset]))]));
    return Object.freeze({ pr, version, runs });
  });
  return versions.sort((a, b) => b.pr - a.pr || b.version - a.version);
}

function matchAsset(scenario, asset, digests) {
  const parsed = ASSET_NAME.exec(asset.name);
  if (parsed === null) {
    return [];
  }
  const [pr, version, kind] = [Number(parsed[1]), Number(parsed[2]), parsed[3]];
  return scenario.screens
    .filter((screen) => nameAsset(pr, version, toGifName(scenario.method, screen), `.${kind}`, digests[screen]) === asset.name)
    .map((screen) => ({ pr, version, screen, kind, asset }));
}

// ---- Pure: what VS Code shows -------------------------------------------------------------------------------------

/**
 * The CodeLens items above a scenario, from where the release's read stands ({ state: 'reading' | 'failed' | 'read' })
 * and the scenario's recordings: each { title } with, when it does something, the screen it plays or the command it runs.
 */
function describeLenses(catalog, recordings) {
  if (catalog.state === 'reading') {
    return [{ title: '$(sync~spin) Reading the e2e recordings…' }];
  }
  if (catalog.state === 'failed') {
    return [
      { title: `$(warning) No e2e recordings: ${catalog.reason}` },
      { title: 'Refresh', command: REFRESH_COMMAND },
      ...(catalog.canSignIn ? [{ title: 'Sign in to GitHub', command: SIGN_IN_COMMAND }] : []),
    ];
  }
  if (recordings.length === 0) {
    return [{ title: 'No recording yet' }];
  }
  const newest = recordings[0];
  return [
    ...SCREENS.filter((screen) => newest.runs[screen]).map((screen) => ({
      title: `$(device-${screen}) Play ${screen} recording`,
      play: screen,
    })),
    { title: toVersionLabel(newest) },
  ];
}

/**
 * The panel's page: the data of every version of the scenario's recordings, and the script that draws the chosen one
 * (drawPanel), with its screens as tabs. Only the release's https URLs load in it (its content security policy).
 */
function writePanel(scenario, recordings, chosen, repository, release, nonce, cspSource) {
  const versions = recordings.map((recording) => ({
    label: toVersionLabel(recording),
    screens: SCREENS.filter((screen) => recording.runs[screen]).map((screen) => ({
      screen,
      label: toScreenLabel(screen),
      gif: recording.runs[screen].gif ?? null,
      mp4: recording.runs[screen].mp4 ?? null,
      player: recording.runs[screen].mp4 ? toPlayerUrl(repository, recording.runs[screen].mp4.name, release) : null,
    })),
  }));
  const at = Math.max(0, recordings.findIndex((r) => r.pr === chosen.pr && r.version === chosen.version));
  const data = {
    title: scenario.title,
    about: [scenario.className, scenario.domain].filter((part) => part).join(' · '),
    release,
    versions,
    version: at,
    screen: chosen.screen,
  };
  const json = JSON.stringify(data).replace(/</g, '\\u003c');
  return `<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta http-equiv="Content-Security-Policy" content="default-src 'none'; img-src https: ${cspSource}; media-src https:; style-src 'nonce-${nonce}'; script-src 'nonce-${nonce}';">
<meta name="viewport" content="width=device-width, initial-scale=1">
<style nonce="${nonce}">${PANEL_STYLE}</style>
</head>
<body>
<main data-testid="panel">
  <header>
    <div>
      <h1 data-testid="title"></h1>
      <p data-testid="about"></p>
    </div>
    <a data-testid="player-link" hidden>Open in e2e player ↗</a>
  </header>
  <div role="tablist" aria-label="Screens" data-testid="screens"></div>
  <div data-testid="media"></div>
  <p data-testid="note" hidden></p>
  <div data-testid="versions"><span>Recorded in:</span></div>
  <p data-testid="source"></p>
</main>
<script nonce="${nonce}">(${drawPanel.toString()})(${json});</script>
</body>
</html>`;
}

/**
 * Runs inside the panel (serialized into its page): draws the chosen version and screen, switches screen at a tab's
 * click and version at a "Recorded in" button's, and shows the GIF when the MP4 can't play.
 */
function drawPanel(data) {
  const find = (id) => document.querySelector(`[data-testid="${id}"]`);
  const make = (tag, attributes, text) => {
    const element = document.createElement(tag);
    Object.entries(attributes).forEach(([name, value]) => element.setAttribute(name, value));
    element.textContent = text ?? '';
    return element;
  };
  let chosen = { version: data.version, screen: data.screen };

  const draw = () => {
    const version = data.versions[chosen.version];
    const run = version.screens.find((s) => s.screen === chosen.screen) ?? version.screens[0];
    chosen = { version: chosen.version, screen: run.screen };
    find('title').textContent = data.title;
    find('about').textContent = `${data.about} · ${version.label}`;
    const link = find('player-link');
    link.hidden = run.player === null;
    link.setAttribute('href', run.player ?? '');
    find('screens').replaceChildren(...version.screens.map((s) => {
      const tab = make('button', { role: 'tab', 'aria-selected': String(s.screen === run.screen), 'data-screen': s.screen }, s.label);
      tab.addEventListener('click', () => { chosen = { version: chosen.version, screen: s.screen }; draw(); });
      return tab;
    }));
    drawMedia(run);
    const versions = find('versions');
    versions.replaceChildren(versions.firstElementChild, ...data.versions.map((v, at) => {
      const button = make('button', { 'data-action': 'version', 'aria-pressed': String(at === chosen.version) }, v.label);
      button.addEventListener('click', () => { chosen = { version: at, screen: chosen.screen }; draw(); });
      return button;
    }));
    find('source').textContent = `${data.release} / ${(run.mp4 ?? run.gif).name}`;
  };

  const drawMedia = (run) => {
    const note = find('note');
    note.hidden = true;
    const makeGif = () => make('img', { 'data-testid': 'gif', src: run.gif.url, alt: `${run.label} recording` });
    if (run.mp4 === null) {
      find('media').replaceChildren(makeGif());
      return;
    }
    const video = make('video', { 'data-testid': 'video', src: run.mp4.url, controls: '', autoplay: '', muted: '', playsinline: '', loop: '' });
    video.muted = true;
    video.addEventListener('error', () => {
      if (run.gif === null) {
        note.textContent = "This VS Code can't play the MP4, and the recording has no GIF.";
      } else {
        note.textContent = "This VS Code can't play the MP4, so its GIF shows instead.";
        find('media').replaceChildren(makeGif());
      }
      note.hidden = false;
    });
    find('media').replaceChildren(video);
  };

  draw();
}

const PANEL_STYLE = `
  body { color: var(--vscode-foreground); background: var(--vscode-editor-background); font-family: var(--vscode-font-family); font-size: 13px; margin: 0; }
  main { padding: 18px 22px; max-width: 1100px; }
  header { display: flex; justify-content: space-between; align-items: flex-start; gap: 16px; }
  h1 { margin: 0 0 6px; font-size: 17px; font-weight: 600; }
  header p { margin: 0; color: var(--vscode-descriptionForeground); }
  a { color: var(--vscode-textLink-foreground); white-space: nowrap; margin-top: 3px; }
  [role=tablist] { display: flex; gap: 2px; margin-top: 18px; border-bottom: 1px solid var(--vscode-panel-border, #444); }
  [role=tab] { background: none; border: 0; border-bottom: 2px solid transparent; color: var(--vscode-descriptionForeground); padding: 7px 14px; font: inherit; cursor: pointer; }
  [role=tab][aria-selected=true] { color: var(--vscode-foreground); border-bottom-color: var(--vscode-focusBorder); }
  [data-testid=media] { margin-top: 14px; }
  video, [data-testid=media] img { display: block; max-width: 100%; max-height: calc(100vh - 230px); background: #000; border: 1px solid var(--vscode-panel-border, #444); border-radius: 6px; }
  [data-testid=note] { color: var(--vscode-descriptionForeground); }
  [data-testid=versions] { margin-top: 18px; display: flex; flex-wrap: wrap; gap: 8px; align-items: center; color: var(--vscode-descriptionForeground); }
  [data-action=version] { border: 1px solid var(--vscode-panel-border, #555); border-radius: 12px; padding: 3px 10px; background: none; color: var(--vscode-foreground); font: inherit; cursor: pointer; }
  [data-action=version][aria-pressed=true] { border-color: var(--vscode-focusBorder); background: var(--vscode-list-activeSelectionBackground); color: var(--vscode-list-activeSelectionForeground); }
  [data-testid=source] { margin-top: 12px; color: var(--vscode-descriptionForeground); font-family: var(--vscode-editor-font-family); font-size: 12px; word-break: break-all; }
`;

// ---- Impure: GitHub -----------------------------------------------------------------------------------------------

/**
 * The release's assets ({ name, url }), read from GitHub's API: { ok: true, assets } or { ok: false, reason, limited }. A
 * rule release that doesn't exist yet holds no recording: the workflow creates it on its first upload.
 */
async function readReleaseAssets(repository, release, token) {
  const headers = { Accept: 'application/vnd.github+json', ...(token ? { Authorization: `Bearer ${token}` } : {}) };
  const found = await fetchJson(`https://api.github.com/repos/${repository}/releases/tags/${encodeURIComponent(release)}`, headers, release);
  if (!found.ok) {
    return found.missing && release.startsWith('rules-') ? { ok: true, assets: [] } : found;
  }
  return readAssetPages(`https://api.github.com/repos/${repository}/releases/${found.value.id}/assets?per_page=100`, headers, release, 1, []);
}

async function readAssetPages(url, headers, release, page, read) {
  const answer = await fetchJson(`${url}&page=${page}`, headers, release);
  if (!answer.ok) {
    return answer;
  }
  const assets = [...read, ...answer.value.map((asset) => ({ name: asset.name, url: asset.browser_download_url }))];
  return answer.value.length < 100 ? { ok: true, assets } : readAssetPages(url, headers, release, page + 1, assets);
}

async function fetchJson(url, headers, release) {
  const response = await fetch(url, { headers }).catch(() => null);
  if (response === null) {
    return { ok: false, reason: "GitHub can't be reached", limited: false };
  }
  if (response.ok) {
    const value = await response.json().catch(() => undefined);
    return value === undefined
      ? { ok: false, reason: "GitHub's answer isn't JSON", limited: false }
      : { ok: true, value };
  }
  const limited = (response.status === 403 || response.status === 429) && response.headers.get('x-ratelimit-remaining') === '0';
  const reason = limited ? "GitHub's hourly limit is used up"
    : response.status === 404 ? `GitHub has no release "${release}"`
      : `GitHub answered ${response.status}`;
  return { ok: false, reason, limited, missing: response.status === 404 };
}

/**
 * A GitHub sign-in VS Code already holds, asked silently (no prompt), or undefined. A silent ask fails when VS Code has
 * no GitHub sign-in to offer (no GitHub provider, as in VS Code for the web without one): that is no token, not an error.
 */
async function findGitHubToken(vscode) {
  for (const scopes of KNOWN_GITHUB_SCOPES) {
    const asked = Promise.resolve(vscode.authentication.getSession('github', scopes, { silent: true }));
    const session = await asked.catch(() => undefined);
    if (session) {
      return session.accessToken;
    }
  }
  return undefined;
}

async function digestText(text) {
  const bytes = await globalThis.crypto.subtle.digest('SHA-256', new TextEncoder().encode(text));
  return [...new Uint8Array(bytes)].map((b) => b.toString(16).padStart(2, '0')).join('').slice(0, 8);
}

async function digestScreens(scenario) {
  const pairs = await Promise.all(SCREENS.map(async (screen) => {
    const path = toGifPath(scenario.domain, toGifName(scenario.method, screen));
    const digest = await digestText(path);
    return [screen, digest];
  }));
  return Object.fromEntries(pairs);
}

function makeNonce() {
  return [...globalThis.crypto.getRandomValues(new Uint8Array(16))].map((b) => b.toString(16).padStart(2, '0')).join('');
}

// ---- The shell ----------------------------------------------------------------------------------------------------

function activate(context) {
  const vscode = require('vscode');
  const changed = new vscode.EventEmitter();
  // Each release read so far (prs-resources, and each rule release a scenario file needed): { catalog, readAt, reads }.
  const releases = new Map();
  let panel = null;

  const readSettings = () => {
    const settings = vscode.workspace.getConfiguration('loopsmith.e2eRecordings');
    return { repository: settings.get('repository'), release: settings.get('release') };
  };

  // A read of a release started while another is on its way wins: the older one's answer is dropped.
  const readRelease = async (release) => {
    const held = releases.get(release) ?? { catalog: { state: 'reading' }, readAt: 0, reads: 0 };
    const mine = held.reads + 1;
    releases.set(release, { ...held, catalog: { state: 'reading' }, reads: mine });
    changed.fire();
    const { repository } = readSettings();
    const token = await findGitHubToken(vscode);
    const read = await readReleaseAssets(repository, release, token);
    if (releases.get(release)?.reads !== mine) {
      return;
    }
    const catalog = read.ok ? { state: 'read', assets: read.assets } : { state: 'failed', reason: read.reason, canSignIn: read.limited && !token };
    releases.set(release, { catalog, readAt: Date.now(), reads: mine });
    changed.fire();
  };

  // Reads every release again: prs-resources, and each rule release read so far.
  const refresh = async () => {
    const { release } = readSettings();
    await Promise.all([...new Set([release, ...releases.keys()])].map(readRelease));
  };

  const findRelease = (scenario) => toRelease(scenario.domain, readSettings().release);

  // The scenario's release as read so far; a release not read yet, or read too long ago, is read (again).
  const readCatalog = (scenario) => {
    const release = findRelease(scenario);
    const held = releases.get(release);
    if (held === undefined || (held.catalog.state !== 'reading' && Date.now() - held.readAt > FRESH_FOR_MS)) {
      readRelease(release);
    }
    return releases.get(release).catalog;
  };

  const readRecordings = async (scenario) => {
    const read = readCatalog(scenario);
    if (read.state !== 'read') {
      return [];
    }
    const digests = await digestScreens(scenario);
    return findRecordings(scenario, read.assets, digests);
  };

  const findScenario = async (uri, method) => {
    const document = await vscode.workspace.openTextDocument(vscode.Uri.parse(uri));
    const text = document.getText();
    return findScenarios(text).find((scenario) => scenario.method === method) ?? null;
  };

  const play = async (args) => {
    const scenario = await findScenario(args.uri, args.method);
    const recordings = scenario === null ? [] : await readRecordings(scenario);
    if (recordings.length === 0) {
      vscode.window.showInformationMessage(`No e2e recording of ${args.method} yet.`);
      return;
    }
    const chosen = { pr: args.pr ?? recordings[0].pr, version: args.version ?? recordings[0].version, screen: args.screen };
    if (panel === null) {
      panel = vscode.window.createWebviewPanel('loopsmith.e2eRecording', '', { viewColumn: vscode.ViewColumn.Beside, preserveFocus: false }, { enableScripts: true, localResourceRoots: [] });
      panel.onDidDispose(() => { panel = null; });
    }
    const { repository } = readSettings();
    panel.title = `Recording · ${scenario.method}`;
    const nonce = makeNonce();
    const html = writePanel(scenario, recordings, chosen, repository, findRelease(scenario), nonce, panel.webview.cspSource);
    panel.webview.html = html;
    panel.reveal(vscode.ViewColumn.Beside);
  };

  // The scenario the cursor is in, played on its first screen: what the editor's play button and the Command Palette
  // run, and what a phone can tap (a CodeLens is hard to reach with a finger).
  const playHere = async () => {
    const editor = vscode.window.activeTextEditor;
    const scenarios = editor ? findScenarios(editor.document.getText()) : [];
    const scenario = editor ? findScenarioAround(scenarios, editor.selection.active.line) : null;
    if (scenario === null) {
      vscode.window.showInformationMessage('No e2e scenario in this file.');
      return;
    }
    await play({ uri: editor.document.uri.toString(), method: scenario.method });
  };

  const lenses = {
    onDidChangeCodeLenses: changed.event,
    async provideCodeLenses(document) {
      const text = document.getText();
      const scenarios = findScenarios(text);
      const described = await Promise.all(scenarios.map(async (scenario) => {
        const range = new vscode.Range(scenario.nameLine, 0, scenario.nameLine, 0);
        const recordings = await readRecordings(scenario);
        const items = describeLenses(readCatalog(scenario), recordings);
        return items.map((item) => new vscode.CodeLens(range, {
          title: item.title,
          command: item.play ? PLAY_COMMAND : item.command ?? '',
          arguments: item.play ? [{ uri: document.uri.toString(), method: scenario.method, screen: item.play }] : [],
        }));
      }));
      return described.flat();
    },
  };

  const signIn = async () => {
    const asked = Promise.resolve(vscode.authentication.getSession('github', SIGN_IN_SCOPES, { createIfNone: true }));
    const signedIn = await asked.then(() => ({ ok: true }), (error) => ({ ok: false, reason: String(error?.message ?? error) }));
    if (!signedIn.ok) {
      vscode.window.showWarningMessage(`GitHub sign-in didn't finish: ${signedIn.reason}`);
      return;
    }
    await refresh();
  };

  const selector = { language: 'csharp', pattern: SCENARIO_FILES };
  context.subscriptions.push(
    changed,
    vscode.languages.registerCodeLensProvider(selector, lenses),
    vscode.commands.registerCommand(PLAY_COMMAND, play),
    vscode.commands.registerCommand(PLAY_HERE_COMMAND, playHere),
    vscode.commands.registerCommand(REFRESH_COMMAND, refresh),
    vscode.commands.registerCommand(SIGN_IN_COMMAND, signIn),
    vscode.workspace.onDidChangeConfiguration((event) => {
      if (event.affectsConfiguration('loopsmith.e2eRecordings')) {
        refresh();
      }
    }),
    { dispose: () => panel?.dispose() });
  refresh();
}

function deactivate() {}

module.exports = {
  activate,
  deactivate,
  toKebabCase,
  toGifName,
  toDomainFolder,
  toRelease,
  toGifPath,
  nameAsset,
  toPlayerUrl,
  findScenarios,
  findScenarioAround,
  findRecordings,
  describeLenses,
  writePanel,
  digestText,
};
