---
name: loopsmith-task
description: Starts a piece of Loopsmith work from what the owner wants, written in any language and any shape (a sentence, a dictated note, a list), plus the screenshots and files they paste (images, logs, YAML, XML, JSON, loop files). Agrees the spec and the acceptance criteria with them, then does the work in this same session on one branch and one pull request, watched until it is merged. Use when the owner runs /loopsmith-task or asks to start a task.
argument-hint: what you want, in your own words and any language (paste screenshots and files too)
---

# Start a Loopsmith task

The owner writes what they want however it comes out, in any language, and may paste screenshots
and files. You agree with them on what to build and how to check it, then you build it: in this
session, on one branch and one pull request (CLAUDE.md, "Agent workflow").

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
- Look at the code, rules, builds and docs as far as you need to name the right files and to check
  that what they describe exists (a screen, a build, a rule id, a perk). Don't change anything yet.

## 2. One task, one pull request

A session works one task as one PR, and PRs aren't stacked. If the text holds independent changes:
- take the one they put first, or the smallest one if they gave no order;
- for each of the others, give them a ready-to-paste `/loopsmith-task …` line for a new session.

A change that needs another one first waits until that one is merged.

## 3. Round 1: what you understood, questions, acceptance criteria

In one message:
- What you understood, in a few lines.
- Your questions, if any. They aren't mandatory: ask one short question only where a point has two
  readings that would lead to different changes. When nothing is ambiguous, say so and ask none.
  Smaller gaps become `Open question:` lines in the notes.
- **Proposed acceptance criteria**, numbered: what someone does and sees, one check per line. Ask the
  owner to agree, or to say what to add, drop or change.
- Anything you'd suggest adding, as a suggestion, not as a criterion.

Then wait for their answer. Change the criteria as often as they want.

## 4. Round 2: the spec

Show the spec exactly as it will open the PR description, and ask whether to start.

**Title:** an imperative sentence of at most about 70 characters, e.g.
`Show the perk descriptions in the perk picker on the phone`.

**Body:**

```markdown
## Goal

What is true when this is done, from the player's or the owner's side, in one to three sentences.

## Acceptance criteria

- [ ] The agreed criteria, one per line.

## Out of scope

What this PR must not touch.

## Notes and pointers

Files, rules, builds, related PRs, how the work splits, open questions.

## Files given in the session

- `01-perk-picker-phone.png`: the perk picker on a phone, descriptions missing.
```

- **Goal:** their intent in plain words. For a bug: what should happen, and what happens now
  (where, and on what device or browser if they said).
- **Acceptance criteria:**
  - Each one is what someone does and sees: on the preview at `…/pr-preview/pr-<n>/` (which
    screen, what you tap, what you see), in a file, or in a test.
  - A web change is checked on desktop and at phone width (390 px).
  - Say how the app behaves, not how the code does it.
  - No filler such as "code is clean" or "tests pass" (CI checks those).
- **Out of scope:**
  - What they excluded.
  - What an eager session might touch by mistake: other builds, generated files like
    `rules/manifest.yaml`, unrelated screens, behaviour that should stay as it is.
  - If there is nothing, write `Nothing beyond the goal.`
- **Notes and pointers:**
  - Paths that exist (check them), rule ids, the DIM link or build slug they mentioned, related PRs.
  - `Open question:` lines for what you couldn't settle.
  - For a bug, a cause you suspect but haven't verified is marked as not diagnosed. You reproduce
    the bug first.
  - Rule work names its source. When it needs the Compendium snapshot, the session downloads it
    (`tools/compendium/README.md`, "In a cloud session"); ask for the zip only if that fails.
  - How the work splits: "One session, no split", or each part with its subagent and folders
    (e.g. "rule-author × 3: `rules/hunter/arc.yaml`, `rules/exotics/armor.yaml`, `rules/weapons/`;
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

The work branch is always `claude/<slug>`, named from the agreed title (CLAUDE.md, "Naming (agent
work)"), even when this session was handed a generated branch name such as
`claude/zealous-euler-qu8uiv`. Don't push to the generated one; every commit, push and the pull
request use the slug branch.

Once the pull request is open, rename this session so it shows the PR (CLAUDE.md, "Agent workflow",
step 4):
- Read the current title with the claude-code-remote `get_session` tool, with no `session_id`.
- If the title already starts with `#<PR number> `, leave it. Otherwise put that prefix in front with
  `set_session_title`: "D2 damage resistance calculation" → "#59 D2 damage resistance calculation".
- If the tools aren't available or the rename fails, say so in the session and carry on. The rename
  never blocks the PR.

Every message that reports work as finished (the PR opened, a review round or a CI fix pushed) ends
with both links as bullet points, with the real PR number:
- PR: https://github.com/JoanComasFdz/destiny-loopsmith/pull/59
- Preview: https://joancomasfdz.github.io/destiny-loopsmith/pr-preview/pr-59/

The owner's files never go into the repository by themselves. When the work needs one in the
repository, put it in its proper place, for example a loop file as a test case under `builds/` or
`tests/`. Before it goes in, check it for personal data and secrets, and say what you found. The same
goes for log lines you quote in the PR.

### Subagents

The session is the only one that talks to the owner, commits, pushes and writes on GitHub. Subagents
read, author in their own worktree, or check, and report back to it.
- **Understand (§1):** for a wide request, run Explore subagents in parallel (open pull requests that
  overlap; where the request lives in the code, rules and builds). When rules are involved, start the
  Compendium download in the background at the same time.
- **Work:** split only as the spec's "How the work splits" says. Write the contract first (CLAUDE.md,
  "Parallel subagents"), give each writing subagent its own worktree and folders, and name its
  binding rules in its prompt. One `rule-author` per build element; `build-extractor` for a build
  note. After the merge, the session writes what everyone shares (`tools/manifest/`,
  `discrepancies.md`, docs, CLAUDE.md) and runs `dotnet build Loopsmith.slnx` and `dotnet test`.
- **Check, before the first push, in parallel:** `conventions-reviewer` on the diff; a read-only
  subagent that checks each acceptance criterion against the diff; for a web change, one that
  publishes the app and checks it in a browser at desktop and 390 px (CLAUDE.md, "Verify web changes
  in a browser"). Long commands (`tools/e2e/record.sh`, `dotnet test`) run in the background
  meanwhile.
- **Watching:** on a red check with a long log, a read-only subagent may diagnose it; the session
  fixes and pushes, one push at a time.
- Don't split a one-file change, either round with the owner, or anything the spec didn't split.

## 6. Follow-ups: launch each one in its own session

Work that this PR doesn't cover (something you noticed, or something the owner asks for after the PR)
is a follow-up. It never goes into this PR (section 2). Offer each one as a numbered item with a
proposed task title (CLAUDE.md, "Naming (agent work)") and one or two lines on what it is.

When the owner says to launch one ("launch", "launch 2", "launch all"), start a new session for
each one with the claude-code-remote `create_session` tool:
- `title`: `(#<n> follow-up) ` followed by the follow-up's task title, where `<n>` is this
  session's PR: "(#56 follow-up) Show perk descriptions on phone". When the new session opens its
  own PR, the rename in section 5 puts that PR's number in front: "#57 (#56 follow-up) Show perk
  descriptions on phone".
- `source_url`: `https://github.com/JoanComasFdz/destiny-loopsmith`. Leave the environment, model
  and permission mode as they are (the new session inherits them).
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
