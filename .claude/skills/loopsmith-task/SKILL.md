---
name: loopsmith-task
description: Starts a piece of Loopsmith work from what the owner wants, written in any language and any shape (a sentence, a dictated note, a list), plus the screenshots and files they paste (images, logs, YAML, XML, JSON, loop files). Agrees the spec and the acceptance criteria with them, then does the work in this same session on one branch and one pull request, watched until it is merged. Use when the owner runs /loopsmith-task or asks to start a task.
argument-hint: what you want, in your own words and any language (paste screenshots and files too)
---

# Start a Loopsmith task

The owner writes what they want however it comes out, in any language, and may paste screenshots
and files. You agree with them on what to build and how to check it, then you build it: in this
session, on one branch and one pull request (CLAUDE.md, "Agent workflow").

`step-by-step.html` next to this file draws the flow: which steps run side by side, and why each one
is done by the session, by a subagent or in the background. Keep it in step with this file.

- **Language.** Whatever language the owner writes in, reply in English and write everything in
  English, like the rest of the repository. Don't quote their original words in the PR: say it with
  the app's own names for its screens and parts (the names the UI and the code use: "perk picker",
  "item details popup", "designer").
- **Names** follow CLAUDE.md, "Naming (agent work)".

## 1. Understand the request

- Their text is what follows `/loopsmith-task`, or what they wrote in the conversation. Their files
  are the images and files they pasted; the conversation shows each one's path on disk.
- Open every file. Look at each image, read each log, YAML, XML or JSON file, and use what's in them.
- Read CLAUDE.md's "Agent workflow" and "Naming (agent work)".
- Search the open pull requests. If one already covers the change, say so and stop there.
- Look at the code, rules and docs as far as you need to name the right files and to check
  that what they describe exists (a screen, a rule id, a perk). Don't change anything yet.

## 2. One task, one pull request

A session works one task as one PR, and PRs aren't stacked. If the text holds independent changes:
- take the one they put first, or the smallest one if they gave no order;
- for each of the others, give them a ready-to-paste `/loopsmith-task …` line for a new session.

A change that needs another one first waits until that one is merged.

## 3. Round 1: what you understood, questions, acceptance criteria

In one message:
- What you understood, in a few lines.
- Your questions, if any. They aren't mandatory: ask only where a point has two readings that would
  lead to different changes. When nothing is ambiguous, say so and ask none. Smaller gaps become
  `Open question:` lines in the notes. Each question is written as "How to write a question or a
  suggestion" below says, never as a one-liner.
- **Proposed acceptance criteria**, numbered: what someone does and sees. Each criterion about how the
  app behaves is a Gherkin, written by [docs/given-when-then-test-rules.md](../../../docs/given-when-then-test-rules.md)
  (its screens, its title, its rounds of When and Then), layout included (no overlap, at the right end,
  inside the screen); each one becomes one e2e scenario. A criterion checked in a file stays a plain line.
- **Each rule's flow, with its Gherkin.** A Gherkin for a rule (a rule scenario, under
  `tests/Loopsmith.Web.E2E/Rules/`) names its rule on its `# Rule <name>: …` line
  ([docs/given-when-then-rule-scenarios.md](../../../docs/given-when-then-rule-scenarios.md), "The shape"). Draw that
  rule's flow with the rule viewer, `dotnet run --project tools/rule-viewer/Loopsmith.RuleViewer -- --rule <name> --theme dark`
  (→ `out/rule-viewer/<name>.png`, dark like the PR's e2e post; build once, then `--no-build` for the others), and send it to the owner as a file
  (`SendUserFile`, `display: render`) right after the message that shows the Gherkin, captioned with the Gherkin's
  title: the owner reads the flow (trigger ➜ what must be in place ➜ what it gives) next to the claim it plays. Several
  rule Gherkins: one PNG each, in the Gherkins' order, in one call. Do the same in any later round that adds a rule
  Gherkin or changes one's `# Rule` line.
  **A rule that doesn't exist yet** (the viewer says "No rule is named '…'", and the task is to author it) is drafted
  first, so the owner still gets its flow: write it under `rules/` where its name says (its file's path then its `id`,
  CLAUDE.md, "Where things live"), by [docs/rule-format.md](../../../docs/rule-format.md), with its source cited and
  `"?"` for any number you can't find (never invent); leave it uncommitted, draw its flow from the draft, and add
  `(draft rule)` to the PNG's caption. A later round that changes that Gherkin's claim or `# Rule` line changes the
  draft and draws it again; one that drops the Gherkin deletes the draft. When the viewer still can't draw a rule (it
  says why: rules that don't parse, even with the draft), say so under that Gherkin in one line, with its words.
  Ask the owner to agree, or to say what to add, drop or change.
- Anything you'd suggest adding, as a suggestion, not as a criterion, written the same way.

### How to write a question or a suggestion

The owner answers from their phone, between other things, and hasn't read what you read: not the code, not the
investigation, not your earlier messages' shorthand. A question they can't answer without asking back costs a whole
round. So each question and each suggestion stands on its own:

1. **Its own heading**, numbered, saying what is being decided in plain words ("Where the list of name matches
   lives"), never a file name or an internal term alone.
2. **The problem, with context:** what the thing is and what it does today, in the app's own words; then why it
   comes up now, with a concrete example from this task (real names: "our rules say *Spark of Amplitude*, the
   Compendium's Arc tab says *Amplitude*"). Say what a term means the first time you use it (an ADR, a slice, a
   file, an option named earlier in the session): "D12, the decision that the Compendium is joined by name".
3. **The choices**, as a short list or a small table: for each, what changes for the owner or the app and what it
   costs. Mark the one you'd pick as **(my default)** and say why. Leave out a choice that can't work, or name it
   in one line with the reason it can't.
4. **For a suggestion:** what it adds, what it costs (a bigger PR, another round), and what happens if they say
   no (a follow-up, or nothing lost).

End the message with what you need back, as a numbered list matching the headings ("1. Is the default fine?
2. Artifact Perks in this PR, yes or no?"), so the owner can answer "1 ok, 2 yes". When a question goes through
`AskUserQuestion`, its question and each option's description carry the same context: the owner sees only them.

Then wait for their answer. Change the criteria as often as they want.

## 4. Round 2: the spec

Show the spec exactly as it will open the PR description, and ask whether to start. Its Gherkins
are one line each, title and test class: their full text was agreed in round 1, and the PR's e2e
comment shows it with the GIFs, so the description never repeats it.

**Title:** an imperative sentence of at most about 70 characters, e.g.
`Show the perk descriptions in the perk picker on the phone`.

**Body:**

```markdown
## Goal

What is true when this is done, from the player's or the owner's side, in one to three sentences.

## Acceptance criteria

- [ ] <A Gherkin's title> (`<its test class>`)
- [ ] <A criterion checked in a file, one line.>

## Out of scope

What this PR must not touch.

## Notes and pointers

Files, rules, related PRs, how the work splits, open questions.

## Files given in the session

- `01-perk-picker-phone.png`: the perk picker on a phone, descriptions missing.
```

- **Goal:** their intent in plain words. For a bug: what should happen, and what happens now
  (where, and on what device or browser if they said).
- **Acceptance criteria:**
  - Each one is what someone does and sees: on the preview at `…/pr-preview/pr-<n>/`, in a file, or in
    a test. A change with no preview (docs, tooling or tests only: CLAUDE.md, "Agent workflow", step 4) has none: its criteria are
    checked in a file or a test.
  - **Every criterion about how the app behaves is a Gherkin** (docs/given-when-then-test-rules.md),
    and **every Gherkin is one e2e scenario** that holds it word for word in its `[TestDescription]`
    (tests/Loopsmith.Web.E2E/E2E-TEST-CODE-RULES.md, rule i). Round 1 shows each Gherkin in full; the
    spec and the PR description list it by its title and test class only, because the PR's e2e
    comment shows the full Gherkin with its GIFs (rules, section 8). Its tags name the screens it is played on:
    `@desktop @mobile`, unless the subject can't apply to one (a tooltip shows on hover only; a phone's
    layout on a phone only), and then a `#` line says why. Layout (no overlap, at a row's end, inside
    the screen, text not cut off) is a Gherkin too; "looks right" and colours are never criteria.
  - Not yet a Gherkin: what needs a Bungie login (no e2e scenario can reach it yet).
    Such a criterion is a plain line, checked by hand on the preview, that says why.
  - A criterion checked in a file (a doc, a rules YAML, a test, a workflow) is a plain line.
  - Say how the app behaves, not how the code does it.
  - No filler such as "code is clean" or "tests pass" (CI checks those).
- **Out of scope:**
  - What they excluded.
  - What an eager session might touch by mistake: other rules, generated files like
    `rules/manifest.yaml`, unrelated screens, behaviour that should stay as it is.
  - If there is nothing, write `Nothing beyond the goal.`
- **Notes and pointers:**
  - Paths that exist (check them), rule ids, the build or loop file they mentioned, related PRs.
  - `Open question:` lines for what you couldn't settle.
  - For a bug, a cause you suspect but haven't verified is marked as not diagnosed. You reproduce
    the bug first.
  - Rule work names its source. When it needs the Compendium snapshot, the session downloads it
    (`tools/compendium/README.md`, "In a cloud session"); ask for the zip only if that fails.
  - How the work splits: "One session, no split", or each part with its subagent and folders
    (e.g. "rule-author × 3: `rules/hunter/arc/`, `rules/hunter/exotic-armor.yaml`, `rules/weapons/`;
    the session: manifest, docs"). Agreed with the owner like the rest of the spec (§5, "Subagents").
- **Files given in the session:** each file's name (Naming) and what it shows, in one line. They stay
  in this session and aren't committed; the list keeps a record of what the work was based on.
  Write `None.` if there were none.

Never invent. A game number, mechanic or name you can't find in the rules, the docs or their files
stays out, or becomes an open question (CLAUDE.md: unknown is data). Don't add scope they didn't ask
for; suggest it to them instead.

## 5. Start the work

When they say go, follow CLAUDE.md's "Agent workflow" from step 1. The spec becomes the PR's title and
description when the work is ready for review.

A rule drafted in round 1 (section 3) comes along, uncommitted, onto the work branch: the rule work starts from it
and commits it, reviewed like any other rule.

The work branch is always `claude/<slug>`, named from the agreed title (CLAUDE.md, "Naming (agent
work)"), even when this session was handed a generated branch name such as
`claude/zealous-euler-qu8uiv`. Don't push to the generated one; every commit, push and the pull
request use the slug branch. A follow-up session is handed its slug branch by the session that
launched it (section 6, `outcome_branch`): keep that one, even if the agreed title changes.

Commit as you go, but keep the branch local until the work is ready for review (CLAUDE.md, "Agent
workflow", steps 3–4). While developing, run the e2e scenarios unrecorded, as often as you like:
`tools/e2e/record.sh --no-video --changed-since origin/main` (or a class or domain folder; add
`--no-publish` when only scenario code changed, not the web app). It writes no GIF and leaves the
last recording's alone. Never record between pushes: a recording takes minutes, and only the one
right before a push is posted. Right before its first push:
1. `git fetch origin main` and `git rebase origin/main`. Resolve any conflict; when both sides
   changed the same logic, ask in the session.
2. Run `dotnet build Loopsmith.slnx` and `dotnet test` again (and the browser check again for a
   web change, if the rebase brought in web changes).
3. When the branch adds or changes e2e scenarios, record them, now and not before:
   `tools/e2e/record.sh --changed-since origin/main`. Every scenario passes on every screen it is
   played on before the push; a failing one is fixed first. Look at its GIFs under
   `out/<domain folder>/` (one per screen, `…-desktop.gif`, `…-mobile.gif`): each should show what
   its Gherkin says. The recording also draws the flow of each rule a recorded scenario names, next to
   its GIFs (`out/<domain folder>/<rule name>.png`); a rule the viewer couldn't draw is a line in its
   output ("no flow for …"), and the post says why in its place.
4. Push and open the pull request.
5. With scenarios recorded, push their GIFs: `tools/e2e/push-gifs.sh` (it finds the PR from the
   branch). It pushes a throwaway branch `e2e-gifs/pr-<n>-<short sha>`; the "E2E videos" workflow
   uploads the GIFs and the flows, posts them on the PR as `## ✅ Scenarios (V<N>) · <count>` (❌ and `· <n> failed`
   when any failed; each scenario a closed `<details>` under its title after ✅ or ❌, holding the flow PNG of the rule a rule scenario names, its Gherkin, then its `🖥️ Desktop`
   and/or `📱 Mobile` GIF; the scenarios only re-recorded because a fixture file
   they use changed come last, under "Re-recorded: their fixtures changed") and deletes the branch. "What
   changed" in that post is the body of the head commit's message, or `--message "<text>"`. Check
   the post appears with its flows and GIFs (the PR's comments, a minute or so later); if the run failed, its
   log says why.

Every later push that changes scenarios does the same: develop with `--no-video`, record right
before the push, `push-gifs.sh` right after it. When a scenario or the web app changes after the
recording and before the push, record again, so the posted GIFs match the pushed head. CI doesn't run or record e2e scenarios: the session's recording is their only run.
A session can't upload a file to GitHub, which is why the GIFs travel on a branch
([docs/hosting.md](../../../docs/hosting.md), "E2E videos on pull requests").

The rebase is only for the first push: once the branch is on GitHub, rebasing it would need a
force-push, which CLAUDE.md forbids. After that, a conflict is fixed by merging `origin/main` into
the branch, and a branch that is only behind `main` needs nothing: a PR doesn't have to be up to
date to be merged (CLAUDE.md, step 6). The rebase finds a conflict in the session, before the PR
opens, and makes the build and test run against the current `main`.

Once the pull request is open, rename this session so it shows the PR (CLAUDE.md, "Agent workflow",
step 4):
- Read the current title with the claude-code-remote `get_session` tool, with no `session_id`.
- If the title already starts with `#<PR number> `, leave it. Otherwise put that prefix in front with
  `set_session_title`: "D2 damage resistance calculation" → "#59 D2 damage resistance calculation".
- If the tools aren't available or the rename fails, say so in the session and carry on. The rename
  never blocks the PR.

Only two kinds of message end with both links as bullet points, with the real PR number (CLAUDE.md,
"Agent workflow", step 4):
- the one that reports the PR opened;
- the one after a push that answers the owner: a review round, or a change they asked for in the
  session.

They end like this:
- PR: https://github.com/JoanComasFdz/destiny-loopsmith/pull/59
- Preview: https://joancomasfdz.github.io/destiny-loopsmith/pr-preview/pr-59/

No other message repeats the links: CI running, passing or failing; the preview built; e2e GIFs
posted; check-ins; a conflict merge or a CI fix nobody asked for; waiting on review. The PR merged
or closed is one line with no links.

A PR with no preview (every changed path a Markdown file or under `docs/`, `.github/`, `.claude/`,
`.devcontainer/`, `tools/compendium/`, `tools/manifest/` or `tests/`, or a script directly in `tools/e2e/`; CLAUDE.md, "Agent
workflow", step 4) gets none, so those two messages end with the PR bullet only,
and its description has no "how to check it on the preview".

The owner's files never go into the repository by themselves. When the work needs one in the
repository, put it in its proper place, for example a loop file as a test case under
`tests/`. Before it goes in, check it for personal data and secrets, and say what you found. The same
goes for log lines you quote in the PR.

**Long-running commands.** A command that runs for more than about a minute (`tools/e2e/record.sh`, `dotnet test`,
`dotnet publish`, the Compendium download) runs in the background, and the session reports its progress in the session
about once a minute until it ends: one short line saying how many are done out of how many in all, how they went, how
long it has run and, once something is done, about how long is left (the time so far per item, times the items left):
"7 of 36 runs recorded, all passed · 6 min elapsed, about 25 min left"; "3 of 4 test projects done, 1 failed · 2 min
elapsed, about 1 min left". It is read from the command's output by a Monitor on its log that prints that line every
minute, never guessed; never wait for a long command with a silent loop (`until …; do sleep …; done`), which shows
nothing until it ends. A command with several phases is followed through each of them, each with its own count:
`tools/e2e/record.sh` publishes the app, plays the runs, then writes each run's GIF and MP4 ("31 of 48 GIFs written"). Every line gives the total, whatever is counted (runs, tests, files, steps):
work it out before the command starts, from what it will run (a recording's runs are its selected scenarios times the
screens each is played on), and when no total can be known, say so in the first line (and leave out the time left). When it
ends, one line says how (passed, failed with what failed, or stopped) and how long it took.

### Subagents

The session is the only one that talks to the owner, commits, pushes and writes on GitHub. Subagents
read, author in their own worktree, or check, and report back to it.
- **Understand (§1):** for a wide request, run Explore subagents in parallel (open pull requests that
  overlap; where the request lives in the code and rules). When rules are involved, start the
  Compendium download in the background at the same time.
- **Work:** split only as the spec's "How the work splits" says. Write the contract first (CLAUDE.md,
  "Parallel subagents"), give each writing subagent its own worktree and folders, and name its
  binding rules in its prompt. One `rule-author` per build element. After the merge, the session writes what everyone shares
  (`tools/manifest/`, docs, CLAUDE.md) and runs `dotnet build Loopsmith.slnx` and `dotnet test`.
- **Check, before the first push, in parallel:** `conventions-reviewer` on the diff; a read-only
  subagent that checks each acceptance criterion against the diff, and that each Gherkin is one
  scenario holding it word for word; for a web change, one that
  publishes the app and checks it in a browser at desktop and 390 px (CLAUDE.md, "Verify web changes
  in a browser"). Long commands (`tools/e2e/record.sh`, `dotnet test`) run in the background
  meanwhile.
- **Watching:** on a red check with a long log, a read-only subagent may diagnose it; the session
  fixes and pushes, one push at a time.
- Don't split a one-file change, either round with the owner, or anything the spec didn't split.

## 6. Follow-ups: launch each one in its own session

Work that this PR doesn't cover (something you noticed, or something the owner asks for after the PR)
is a follow-up. It never goes into this PR (section 2). Offer each one as a numbered item, written so the owner
can decide without asking back ("How to write a question or a suggestion", section 3), with a
proposed task title (CLAUDE.md, "Naming (agent work)") and one or two lines on what it is.

When the owner says to launch one ("launch", "launch 2", "launch all"), start a new session for
each one with the claude-code-remote `create_session` tool:
- `title`: `(#<n> follow-up) ` followed by the follow-up's task title, where `<n>` is this
  session's PR: "(#56 follow-up) Show perk descriptions on phone". When the new session opens its
  own PR, the rename in section 5 puts that PR's number in front: "#57 (#56 follow-up) Show perk
  descriptions on phone".
- `source_url`: `https://github.com/JoanComasFdz/destiny-loopsmith`. Leave the environment, model
  and permission mode as they are (the new session inherits them).
- `outcome_branch`: `claude/<slug>`, named from the follow-up's task title (CLAUDE.md, "Naming
  (agent work)"): "claude/perk-descriptions-on-phone". It gives the new session the repository as
  its outcome, which files it under destiny-loopsmith in the owner's session list instead of
  "Other", and it becomes the new session's work branch (section 5).
- `prompt`: `/loopsmith-task ` followed by the follow-up in the owner's words. Add what the new
  session can't see: that it comes from PR #<n> (link it), the paths, rule ids and findings it needs,
  and a description of any screenshot or file it depends on. The files themselves stay in this session
  (CLAUDE.md, "Agent workflow"). If a file is essential, say so in the prompt so the new session asks
  for it.

The new session agrees its own spec with the owner, on its own branch and pull request. Don't
send it more messages: the owner talks to it directly.

Then confirm in this session with one bullet per launched session, so the owner can see what was
launched:
- **(#<n> follow-up) <title>**: https://claude.ai/code/<session id>

Follow-ups that weren't launched stay on the list in your next message. If `create_session` isn't
available or fails, say so and give the owner the prompt to paste into a new session.
