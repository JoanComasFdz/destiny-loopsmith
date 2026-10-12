# Contributing

## Writing an agent issue

Start a Claude Code session on this repository and type `/loopsmith-issue` followed by what you
want, in your own words and any language. Paste screenshots and any files that help: logs, YAML,
XML, JSON, loop files.

- **Browser:** claude.ai/code → pick `JoanComasFdz/destiny-loopsmith` → start a session.
- **Android or iPhone:** the Claude app → **Code** tab → new session on this repository.
- **Windows or Mac:** the Claude desktop app → **Code** → **Cloud** (not Local) → this repository.

The skill always answers in English:

1. **Round 1:** what it understood, its questions, and **proposed acceptance criteria** for you to
   agree with or change. It also points out anything personal in your files (a Bungie name, an
   email, a token): the repository is public, and so will the files be.
2. **Round 2:** the final issue and the file names, exactly as they'll be posted.
3. **When you say so,** it opens the issue (without the `agent` label) and pushes your files to the
   branch `claude/issue-<N>-resources`, in `docs/issues/issue-<N>/resources/`
   ([docs/issues/README.md](docs/issues/README.md)). Then it adds them to the issue: images inline,
   other files as links, and "Resources also available in `claude/issue-<N>-resources`".

Add the `agent` label once it has given you the issue's link. The skill comes from this
repository, so it works only in Code sessions on it, not in a plain chat. Names follow CLAUDE.md,
"Naming (agent work)".

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
- **Screenshots later on** go into the session, not into a GitHub comment: a session can't open
  GitHub's attachments. The session adds them to the issue's resources branch and links them on the
  PR.
- **Notifications.** The session's comments carry your GitHub name, so GitHub doesn't notify you
  about them; the Claude app does.
- **Merge conflicts.** GitHub doesn't always tell a session when `main` moves under its pull request.
  The session checks its PR on every event and every few hours while it waits, and resolves a
  conflict by merging `main` into its branch. To have it done now, comment "merge origin/main into
  this branch".
- **Out of date.** A PR that is only behind `main` waits for you: click **Update branch**.
- **Dropping an issue.** Close the PR and the issue, then click **Delete branch** on the closed PR (a
  session can't delete branches).
