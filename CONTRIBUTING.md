# Contributing

## Writing an agent issue

Start a Claude Code session on this repository and type `/agent-issue` followed by what you want,
in your own words and any language:

- **Browser:** claude.ai/code → pick `JoanComasFdz/destiny-loopsmith` → start a session.
- **Android or iPhone:** the Claude app → **Code** tab → new session on this repository.
- **Windows or Mac:** the Claude desktop app → **Code** → **Cloud** (not Local) → this repository.

It drafts the issue in English with the Agent task form's sections, shows it to you, and opens it
when you agree, without the `agent` label. Add the label when you want a session to start on it.
The skill comes from this repository, so it works only in Code sessions on it, not in a plain chat.

## Agent workflow

Issues labelled `agent` are implemented by a Claude routine. The issue only starts the work;
everything after it happens on one draft pull request:

1. You add the `agent` label. A session starts, and its link appears on the issue.
2. A few minutes later the session has opened a **draft** pull request from `claude/issue-<number>`
   that starts with `Closes #<number>`, and it watches it.
3. It works on that branch and asks its questions on the PR. Answer there, and add anything else
   on the PR whenever you like, asked for or not: each comment or review wakes the session within
   seconds.
4. When it's done, it comments **Ready for review**. Check the change on the preview
   (`…/pr-preview/pr-<PR number>/`), click **Ready for review** (a session can't take a PR out of
   draft), review, resolve the threads and **Squash and merge**.
5. It keeps watching through any number of review rounds, until the PR is merged or closed.

The session never merges, approves or pushes to `main`, and it replies in review threads without
resolving them.

### Working with a session

- **Talk on the PR.** Comments and reviews there wake the session. Typing in the session (its link
  is on the issue) works too.
- **Notifications.** The session's comments carry your GitHub name, so GitHub doesn't notify you
  about them; the Claude app does.
- **Merge conflicts.** GitHub doesn't always tell a session when `main` moves under its pull request.
  The session checks its PR on every event and every few hours while it waits, and resolves a
  conflict by merging `main` into its branch. To have it done now, comment "merge origin/main into
  this branch".
- **Out of date.** A PR that is only behind `main` waits for you: click **Update branch**.
- **Dropping an issue.** Close the PR and the issue, then click **Delete branch** on the closed PR (a
  session can't delete branches).
