---
name: agent-issue
description: Turns what the owner wants, written in any language and any shape (a sentence, a dictated note, a list), into "Agent task" issues for this repository, in English with the template's four sections, shows them for approval and opens them unlabelled. Use when the owner asks to write, draft or open an issue for the agent or the routine, or runs /agent-issue.
argument-hint: what you want, in your own words and any language
---

# Write an Agent task issue

The owner writes what they want however it comes out, in any language. You turn it into issues a
routine session can work alone: one change each, in English, in the shape of
`.github/ISSUE_TEMPLATE/agent-task.yml`, with nothing invented. The owner adds the `agent` label
afterwards; that label starts a session (CLAUDE.md, "Agent workflow").

Whatever language the owner writes in, reply in English and write the issues in English, like the
rest of the repository. Don't quote their original words in an issue; say it in the app's own
names for its screens and parts (the names the UI and the code use: "perk picker", "item details
popup", "designer").

## 1. Understand the request

- Their text is what follows `/agent-issue`, or what they wrote in the conversation.
- Read `.github/ISSUE_TEMPLATE/agent-task.yml` (the sections and what each asks for) and CLAUDE.md's
  "Agent workflow".
- Search the open issues and pull requests. If one already covers a change, say so and don't draft
  it again.
- Look at the code, rules, builds and docs only as far as you need to name the right files and to
  check that what they describe exists (a screen, a build, a rule id, a perk). You write the issue;
  you don't implement it or diagnose a bug.

## 2. Split into one change per issue

A session works one issue as one pull request, and PRs aren't stacked. Text with independent changes
becomes one issue per change. Order them so each can be merged on its own, the smallest first.
Write "Do after <title> is merged" in an issue's notes only when it can't be done or checked without
the other; otherwise the other goes under "Related".

## 3. Write each issue

**Title:** `[agent] ` followed by an imperative sentence of at most about 70 characters (not
counting the prefix), e.g. `[agent] Show the finisher on the HUD for every build`.

**Body:** the same Markdown GitHub writes for a form submission, so it reads like the others:

```markdown
### Goal

What is true when this is done, from the player's or the owner's side, in one to three sentences.

### Acceptance criteria

- [ ] One checkable item per line: something visible on the PR preview, a file's content, a test.

### Out of scope

What the session must not touch.

### Notes and pointers

Files, rules, builds, related issues or PRs, open questions.
```

- **Goal:** their intent in plain words. For a bug: what should happen, and what happens now
  (where, and on what device or browser if they said).
- **Acceptance criteria:** what someone does and sees, one check per line: on the preview at
  `…/pr-preview/pr-<n>/` (which screen, what you tap, what you see), a file, a test. A web change is
  checked on desktop and at phone width (390 px). Say how the app behaves, not how the code does it.
  No filler such as "code is clean" or "tests pass" (CI checks those).
- **Out of scope:** what they excluded, plus what an eager session might touch by mistake (other
  builds, generated files like `rules/manifest.yaml`, unrelated screens, behaviour that should stay
  as it is). If there is nothing, write `Nothing beyond the goal.`
- **Notes and pointers:** paths that exist (check them), rule ids, the DIM link or build slug they
  mentioned, related issues and PRs, and `Open question:` lines for what you couldn't settle.
  - For a bug, a cause you suspect but haven't verified is marked as not diagnosed. The session
    reproduces the bug first and asks on the issue if it can't.
  - Rule work names its source. When it needs the Compendium snapshot, say so: a routine session
    doesn't have it and will stop to ask for it.
  - Write `_No response_` if there is nothing.

Never invent. A game number, mechanic or name you can't find in the rules, the docs or their text
stays out, or becomes an open question (CLAUDE.md: unknown is data). Don't add scope they didn't
ask for; suggest it to them instead.

## 4. Show, ask once, open

In one message:
- the split, if there is one;
- each issue whose meaning is clear, with its title and body exactly as they will be posted (refer
  to another unopened issue by its title);
- one short question for each change you can't draft yet, because two readings would lead to
  different changes; draft that issue once they answer;
- any scope you'd suggest adding, as a suggestion, not in a draft;
- then ask which issues to open.

Change the drafts as often as they want. When they say which to open, open them in the order of
step 2, so each "Do after" or "Related" line can name the number of the issue opened before it.
Use the built-in GitHub tools (`issue_write`, method `create`), or
`gh api repos/JoanComasFdz/destiny-loopsmith/issues -f title=… -F body=@<file>` (REST; `gh issue`
fails in cloud sessions).

Open them with **no labels**. Never add `agent`: that's the owner's call, and it starts a session.
Then give them the links and say: add the `agent` label to an issue when you want a session to
start on it.
