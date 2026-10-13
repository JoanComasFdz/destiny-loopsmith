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
- Your questions: one short question for each point with two readings that would lead to different
  changes. Smaller gaps become `Open question:` lines in the notes.
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

Files, rules, builds, related PRs, open questions.

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
- If the title already ends with ` #<PR number>`, leave it. Otherwise append that suffix with
  `set_session_title`: "D2 damage resistance calculation" → "D2 damage resistance calculation #59".
- If the tools aren't available or the rename fails, say so in the session and carry on. The rename
  never blocks the PR.

The owner's files never go into the repository by themselves. When the work needs one in the
repository, put it in its proper place, for example a loop file as a test case under `builds/` or
`tests/`. Before it goes in, check it for personal data and secrets, and say what you found. The same
goes for log lines you quote in the PR.
