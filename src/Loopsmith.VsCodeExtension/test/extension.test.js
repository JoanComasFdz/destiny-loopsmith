// The extension's pure functions: node --test src/Loopsmith.VsCodeExtension/test/ (no dependencies).
'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const extension = require('../extension.js');

const START_FROM_SCRATCH = `using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;

namespace Loopsmith.Web.E2E.Scenarios.Starting;

/// <summary>Starting a loop from scratch on Home.</summary>
public sealed class StartFromScratchTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Start from scratch opens an Arc Hunter with nothing equipped
          Given the app is open on Home
          When I tap "Start from scratch"
          Then the loop has no steps
        """)]
    public async Task StartFromScratchOpensAnArcHunterWithNothingEquipped(Screen screen)
    {
        // Given
        await Frontend.Home.WaitUntilShownAsync();
    }

    [Theory, PlayInDesktop]
    [TestDescription("""
        @desktop
        Scenario: A tooltip shows on hover
        # Desktop only: a phone has no hover.
          Given the app is open on Home
        """)]
    public async Task ATooltipShowsOnHover(Screen screen)
    {
    }

    private static string Describe() => "not a scenario";
}
`;

const RELEASE = [
  'pr-89-v1-given-app-is-open-on-home-when-i-click-start-from-scratch-then-the-designer-shows-an-arc-hunter-with-nothing-equipped.gif',
  'pr-101-v1-start-from-scratch-opens-an-arc-hunter-with-nothing-equipped-desktop.gif',
  'pr-101-v1-start-from-scratch-opens-an-arc-hunter-with-nothing-equipped-mobile.gif',
  'pr-104-v1-start-from-scratch-opens-an-arc-hunter-with-nothing-equipped-desktop.gif',
  'pr-104-v1-start-from-scratch-opens-an-arc-hunter-with-nothing-equipped-desktop.mp4',
  'pr-104-v1-start-from-scratch-opens-an-arc-hunter-with-nothing-equipped-mobile.gif',
  'pr-104-v1-start-from-scratch-opens-an-arc-hunter-with-nothing-equipped-mobile.mp4',
  'pr-106-v3-start-from-scratch-opens-an-arc-hunter-with-nothing-equipped-desktop.gif',
  'pr-106-v3-start-from-scratch-opens-an-arc-hunter-with-nothing-equipped-desktop.mp4',
  'pr-109-v1-start-from-scratch-opens-an-arc-hunter-with-nothing-equipped-desktop.gif',
  'pr-109-v1-start-from-scratch-opens-an-arc-hunter-with-nothing-equipped-desktop.mp4',
  'pr-109-v1-start-from-scratch-opens-an-arc-hunter-with-nothing-equipped-mobile.gif',
  'pr-109-v1-start-from-scratch-opens-an-arc-hunter-with-nothing-equipped-mobile.mp4',
  'pr-109-v1-unrelenting-heals-for-sure-on3-quick-kills-desktop.gif',
  'pr-11-v1-start-from-scratch-opens-an-arc-hunter-with-nothing-equipped-desktop.gif',
].map((name) => ({ name, url: `https://github.com/o/r/releases/download/prs-resources/${name}` }));

const read = { state: 'read', assets: RELEASE };

async function findStartFromScratch() {
  const [scenario] = extension.findScenarios(START_FROM_SCRATCH);
  return { scenario, recordings: await findRecordingsOf(scenario, RELEASE) };
}

async function findRecordingsOf(scenario, assets) {
  const digests = {};
  for (const screen of ['desktop', 'mobile']) {
    digests[screen] = await extension.digestText(extension.toGifPath(scenario.domain, extension.toGifName(scenario.method, screen)));
  }
  return extension.findRecordings(scenario, assets, digests);
}

test('kebab-cases like Humanizer 3.0.10 (its output for every web scenario and some edge cases)', () => {
  const cases = [
    ['StartFromScratchOpensAnArcHunterWithNothingEquipped', 'start-from-scratch-opens-an-arc-hunter-with-nothing-equipped'],
    ['OneForAllNeeds3HitsOrKillsFromItsOwnWeapon', 'one-for-all-needs3-hits-or-kills-from-its-own-weapon'],
    ['UnrelentingHealsForSureOn3QuickKills', 'unrelenting-heals-for-sure-on3-quick-kills'],
    ['ReapersTitheGives3StacksForAKillOf3', 'reapers-tithe-gives3-stacks-for-a-kill-of3'],
    ['OgmaPR6Unrelenting', 'ogma-pr6-unrelenting'],
    ['HTTPServerX', 'http-server-x'],
    ['ABC', 'abc'],
    ['A1B2', 'a1-b2'],
    ['Kill3In5S', 'kill3-in5-s'],
    ['UsesDIMLinkToOpen', 'uses-dim-link-to-open'],
    ['Ab12Cd', 'ab12-cd'],
    ['AÉtoile', 'a-étoile'],
    ['Top10Picks', 'top10-picks'],
  ];
  for (const [method, kebab] of cases) {
    assert.equal(extension.toKebabCase(method), kebab, method);
  }
});

test('reads a scenario file: its scenarios, their titles, screens, class, domain and where the CodeLens goes', () => {
  const [scratch, tooltip, ...others] = extension.findScenarios(START_FROM_SCRATCH);
  assert.equal(others.length, 0, 'the private method has no [TestDescription]: it is no scenario');
  assert.deepEqual({ ...scratch }, {
    method: 'StartFromScratchOpensAnArcHunterWithNothingEquipped',
    title: 'Start from scratch opens an Arc Hunter with nothing equipped',
    className: 'StartFromScratchTests',
    domain: 'Starting',
    screens: ['desktop', 'mobile'],
    attributesLine: 8,
    nameLine: 16,
  });
  assert.deepEqual(tooltip.screens, ['desktop']);
  assert.equal(tooltip.attributesLine, 22);
});

test('finds the scenario the cursor is in: the last one starting on or above its line, else the first', () => {
  const [scratch, tooltip] = extension.findScenarios(START_FROM_SCRATCH);
  assert.equal(extension.findScenarioAround([scratch, tooltip], 2), scratch, 'above every scenario: the first');
  assert.equal(extension.findScenarioAround([scratch, tooltip], 8), scratch);
  assert.equal(extension.findScenarioAround([scratch, tooltip], 21), scratch);
  assert.equal(extension.findScenarioAround([scratch, tooltip], 22), tooltip);
  assert.equal(extension.findScenarioAround([scratch, tooltip], 40), tooltip);
  assert.equal(extension.findScenarioAround([], 3), null);
});

test('a file outside Scenarios has the domain folder "Unsorted", and a file with no scenario has none', () => {
  assert.equal(extension.toDomainFolder('Loopsmith.Web.E2E.Infrastructure'), 'Unsorted');
  assert.equal(extension.toDomainFolder('Loopsmith.Web.E2E.Scenarios.Build.Subclass'), 'Build/Subclass');
  assert.deepEqual(extension.findScenarios('namespace X;\npublic sealed class A { public void B() { } }\n'), []);
});

test('a rule scenario\'s domain folder is its namespace from Rules on, and its release its rule group\'s', () => {
  assert.equal(extension.toDomainFolder('Loopsmith.Web.E2E.Rules.Hunter.Arc.Melees'), 'Rules/Hunter/Arc/Melees');
  assert.equal(extension.toRelease('Rules/Hunter/Arc/Melees', 'prs-resources'), 'rules-hunter-arc-melees-resources');
  assert.equal(extension.toRelease('Rules/AllClasses/Arc/Fragments', 'prs-resources'), 'rules-all-classes-arc-fragments-resources');
  assert.equal(extension.toRelease('Rules/Hunter/ClassAbilities', 'prs-resources'), 'rules-hunter-class-abilities-resources');
  assert.equal(extension.toRelease('Loop/Weapons', 'prs-resources'), 'prs-resources');
  assert.equal(extension.toRelease('Starting', 'my-release'), 'my-release');
});

test('the player link names the release only when it isn\'t prs-resources', () => {
  assert.equal(extension.toPlayerUrl('JoanComasFdz/destiny-loopsmith', 'pr-1-v1-a-desktop.mp4', 'prs-resources'),
    'https://joancomasfdz.github.io/destiny-loopsmith/e2e-player.html?v=pr-1-v1-a-desktop.mp4');
  assert.equal(extension.toPlayerUrl('JoanComasFdz/destiny-loopsmith', 'pr-1-v1-a-desktop.mp4', 'rules-mods-arms-resources'),
    'https://joancomasfdz.github.io/destiny-loopsmith/e2e-player.html?v=pr-1-v1-a-desktop.mp4&r=rules-mods-arms-resources');
});

test('finds a scenario\'s recordings, newest PR first, each version with the assets it has', async () => {
  const { recordings } = await findStartFromScratch();
  assert.deepEqual(recordings.map((r) => `${r.pr}-${r.version}`), ['109-1', '106-3', '104-1', '101-1', '11-1']);
  assert.deepEqual(Object.keys(recordings[0].runs), ['desktop', 'mobile']);
  assert.equal(recordings[0].runs.desktop.mp4.name, 'pr-109-v1-start-from-scratch-opens-an-arc-hunter-with-nothing-equipped-desktop.mp4');
  assert.deepEqual(Object.keys(recordings[1].runs), ['desktop'], 'PR #106 · V3 has only the desktop run here');
  assert.deepEqual(Object.keys(recordings[3].runs.mobile), ['gif'], 'PR #101 has GIFs, no MP4s');
});

test('names a run past 200 characters like the workflow: its start, then the hash of its GIF path', async () => {
  const method = 'AScenarioWhoseNameGoesOnAndOn'.repeat(5) + 'PastTwoHundredCharacters';
  const gif = extension.toGifName(method, 'desktop');
  const hash = await extension.digestText(extension.toGifPath('Recordings', gif));
  // Written by tools/e2e/post-scenarios.py's name_asset for the same GIF.
  const expected = 'pr-112-v3-' + 'a-scenario-whose-name-goes-on-and-on-'.repeat(4) + 'a-scenario-whose-name-goes-on-860a961a';
  assert.equal(extension.nameAsset(112, 3, gif, '.gif', hash), `${expected}.gif`);
  assert.equal(extension.nameAsset(112, 3, gif, '.mp4', hash), `${expected}.mp4`);
  assert.equal(extension.nameAsset(7, 1, 'short-desktop', '.gif', hash), 'pr-7-v1-short-desktop.gif');
});

test('the CodeLens plays each screen of the newest recording, then names it', async () => {
  const { recordings } = await findStartFromScratch();
  assert.deepEqual(extension.describeLenses(read, recordings), [
    { title: '$(device-desktop) Play desktop recording', play: 'desktop' },
    { title: '$(device-mobile) Play mobile recording', play: 'mobile' },
    { title: 'PR #109 · V1' },
  ]);
  assert.deepEqual(extension.describeLenses(read, []), [{ title: 'No recording yet' }]);
  assert.deepEqual(extension.describeLenses({ state: 'reading' }, []), [{ title: '$(sync~spin) Reading the e2e recordings…' }]);
  assert.deepEqual(extension.describeLenses({ state: 'failed', reason: "GitHub's hourly limit is used up", canSignIn: true }, []), [
    { title: "$(warning) No e2e recordings: GitHub's hourly limit is used up" },
    { title: 'Refresh', command: 'loopsmith.e2eRecordings.refresh' },
    { title: 'Sign in to GitHub', command: 'loopsmith.e2eRecordings.signIn' },
  ]);
});

test('the panel holds every version and opens on the one and the screen chosen', async () => {
  const { scenario, recordings } = await findStartFromScratch();
  const page = extension.writePanel(scenario, recordings, { pr: 104, version: 1, screen: 'mobile' }, 'JoanComasFdz/destiny-loopsmith', 'prs-resources', 'abc', 'vscode-resource:');
  const data = JSON.parse(/\}\)\((\{.*\})\);<\/script>/s.exec(page)[1]);
  assert.deepEqual(data.versions.map((v) => v.label), ['PR #109 · V1', 'PR #106 · V3', 'PR #104 · V1', 'PR #101 · V1', 'PR #11 · V1']);
  assert.equal(data.version, 2);
  assert.equal(data.screen, 'mobile');
  assert.equal(data.about, 'StartFromScratchTests · Starting');
  assert.match(page, /script-src 'nonce-abc'/);
});

test('the .vsix manifest names the same version as package.json', () => {
  const version = JSON.parse(fs.readFileSync(path.join(__dirname, '..', 'package.json'), 'utf8')).version;
  const manifest = fs.readFileSync(path.join(__dirname, '..', 'vsix', 'extension.vsixmanifest'), 'utf8');
  assert.match(manifest, new RegExp(`Version="${version.replace(/\./g, '\\.')}"`));
});
