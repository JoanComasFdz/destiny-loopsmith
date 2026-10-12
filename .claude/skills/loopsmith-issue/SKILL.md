---
name: loopsmith-issue
description: Turns what the owner wants, written in any language and any shape (a sentence, a dictated note, a list), plus the screenshots and files they paste (images, logs, YAML, XML, JSON, loop files), into "Agent task" issues for this repository. Agrees the acceptance criteria with them, writes the issue in English with the template's sections, stores the files on the issue's resources branch, links them from the issue and opens it unlabelled. Use when the owner asks to write, draft or open an issue for the agent or the routine, or runs /loopsmith-issue.
argument-hint: what you want, in your own words and any language (paste screenshots and files too)
---

# Write a Loopsmith issue

The owner writes what they want however it comes out, in any language, and may paste screenshots
and files. You turn it into issues a routine session can work alone:
- one change each, in English;
- in the shape of `.github/ISSUE_TEMPLATE/agent-task.yml`;
- the owner's files stored where every session can open them;
- nothing invented.

The owner adds the `agent` label afterwards; that label starts a session (CLAUDE.md, "Agent
workflow").

Whatever language the owner writes in, reply in English and write the issues in English, like the
rest of the repository. Don't quote their original words in an issue: say it with the app's own names
for its screens and parts (the names the UI and the code use: "perk picker", "item details popup",
"designer").

Names follow CLAUDE.md, "Naming (agent work)": issue titles, the resources branch and folder, file
names and commit messages.

## 1. Understand the request

- Their text is what follows `/loopsmith-issue`, or what they wrote in the conversation. Their files
  are the images and files they pasted; the conversation shows each one's path on disk.
- Open every file. Look at each image, read each log, YAML, XML or JSON file, and use what's in them.
- Read `.github/ISSUE_TEMPLATE/agent-task.yml` (the sections and what each asks for) and CLAUDE.md's
  "Agent workflow" and "Naming (agent work)".
- Search the open issues and pull requests. If one already covers a change, say so and don't draft
  it again.
- Look at the code, rules, builds and docs only as far as you need to name the right files and to
  check that what they describe exists (a screen, a build, a rule id, a perk). You write the issue;
  you don't implement it or diagnose a bug.

## 2. Split into one change per issue

A session works one issue as one pull request, and PRs aren't stacked. Text with independent changes
becomes one issue per change. Order them so each can be merged on its own, the smallest first.
Write "Do after <title> is merged" in an issue's notes only when it can't be done or checked without
the other; otherwise the other goes under "Related". Each file goes with the issue it belongs to.

## 3. Round 1: what you understood, questions, acceptance criteria

In one message:
- What you understood, in a few lines, and the split if there is one.
- Your questions: one short question for each point with two readings that would lead to different
  changes. Smaller gaps become `Open question:` lines in the notes later.
- **Proposed acceptance criteria**, numbered, per issue: what someone does and sees, one check per
  line. Ask the owner to agree, or to say what to add, drop or change.
- Anything you'd suggest adding, as a suggestion, not as a criterion.
- **What's personal in their files.** The repository is public, and so will the files be, for good.
  - Images: name what you see (a Bungie name, an email, a DIM link, an account id).
  - Text files: name tokens, cookies, `Authorization` headers, API keys and emails.

  Propose redacting each one (`<redacted>` in a text file; leaving a screenshot out, or asking for a
  cropped one). Never push a secret.

Then wait for their answer. Change the criteria as often as they want.

## 4. Round 2: the final issue

Show each issue exactly as it will be posted: title, body, and the list of files with their final
names. Then ask whether to open them.

**Title:** `[agent] ` followed by an imperative sentence of at most about 70 characters (not counting
the prefix).

**Body:** the same Markdown GitHub writes for a form submission, so it reads like the others:

```markdown
### Goal

What is true when this is done, from the player's or the owner's side, in one to three sentences.

### Acceptance criteria

- [ ] The agreed criteria, one per line.

### Out of scope

What the session must not touch.

### Notes and pointers

Files, rules, builds, related issues or PRs, open questions.

### Screenshots and files

(filled in after the files are pushed: step 5)
```

- **Goal:** their intent in plain words. For a bug: what should happen, and what happens now
  (where, and on what device or browser if they said).
- **Acceptance criteria:** what someone does and sees on the preview at `…/pr-preview/pr-<n>/`
  (which screen, what you tap, what you see), a file, a test. A web change is checked on desktop and
  at phone width (390 px). Say how the app behaves, not how the code does it. No filler such as "code
  is clean" or "tests pass" (CI checks those).
- **Out of scope:** what they excluded, plus what an eager session might touch by mistake (other
  builds, generated files like `rules/manifest.yaml`, unrelated screens, behaviour that should stay
  as it is). If there is nothing, write `Nothing beyond the goal.`
- **Notes and pointers:** paths that exist (check them), rule ids, the DIM link or build slug they
  mentioned, related issues and PRs, and `Open question:` lines for what you couldn't settle.
  - For a bug, a cause you suspect but haven't verified is marked as not diagnosed. The session
    reproduces the bug first and asks on its PR if it can't.
  - Rule work names its source. When it needs the Compendium snapshot, say so: a routine session
    doesn't have it and will ask for it.
  - Write `_No response_` if there is nothing.
- **Screenshots and files:** `_No response_` when there are none.

Never invent. A game number, mechanic or name you can't find in the rules, the docs or their files
stays out, or becomes an open question (CLAUDE.md: unknown is data). Don't add scope they didn't ask
for; suggest it to them instead.

## 5. Publish

When they say which to open, do this for each issue, in the order of step 2:

1. **Open the issue** with the text and **no labels**: the built-in GitHub tools (`issue_write`,
   method `create`), or `gh api repos/JoanComasFdz/destiny-loopsmith/issues -f title=… -F body=@<file>`
   (REST; `gh issue` fails in cloud sessions). Note its number N. A "Do after" or "Related" line names
   an issue opened before it by number.
2. **If it has files, push its resources branch.** It's an orphan branch, holding only these files,
   in its own worktree so your checkout stays as it is:
   ```bash
   git worktree add --orphan -b claude/issue-N-resources /tmp/issue-N-resources
   mkdir -p /tmp/issue-N-resources/docs/issues/issue-N/resources
   cp <file> /tmp/issue-N-resources/docs/issues/issue-N/resources/<final-name>   # each file, redacted as agreed
   cd /tmp/issue-N-resources && git add docs && git commit -m "Resources for #N: <title without [agent] >"
   git push -u origin claude/issue-N-resources && git rev-parse HEAD             # the commit for the links
   ```
3. **Edit the issue** (`issue_write`, method `update`, or `gh api -X PATCH …/issues/N -F body=@<file>`)
   and fill in "Screenshots and files", linking each file at that commit:
   ```markdown
   ### Screenshots and files

   ![01-perk-picker-phone.png](https://raw.githubusercontent.com/JoanComasFdz/destiny-loopsmith/<commit>/docs/issues/issue-N/resources/01-perk-picker-phone.png)
   - [02-designer-console.log](https://github.com/JoanComasFdz/destiny-loopsmith/blob/<commit>/docs/issues/issue-N/resources/02-designer-console.log)

   Resources also available in [`claude/issue-N-resources`](https://github.com/JoanComasFdz/destiny-loopsmith/tree/claude/issue-N-resources/docs/issues/issue-N/resources).
   ```
   - Images (`png`, `jpg`, `webp`, `gif`) are shown inline, from `raw.githubusercontent.com`.
   - Any other file is a link to its page on GitHub. For a log, quote the few lines that matter in a
     code block under its link.
4. Give them each issue's link, and say: add the `agent` label to an issue when you want a session to
   start on it.

Never add `agent` yourself: that's the owner's call, and it starts a session.
