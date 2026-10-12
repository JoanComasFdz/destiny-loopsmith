# Contributing

## Starting a task

Start a new Claude Code session on this repository and type `/loopsmith-task` followed by what you
want, in your own words and any language, as its first message. Paste screenshots and any files that
help: logs, YAML, XML, JSON, loop files.

- **Browser:** claude.ai/code → pick `JoanComasFdz/destiny-loopsmith` → start a session.
- **Android or iPhone:** the Claude app → **Code** tab → new session on this repository.
- **Windows or Mac:** the Claude desktop app → **Code** → **Cloud** (not Local) → this repository.

The `/` menu doesn't list the command before the session has started, because the skill comes from
this repository: type it anyway. The first answer takes a minute or two while the session installs
.NET.

## What happens

The session always answers in English. One task is one pull request.

1. **Round 1:** what it understood, its questions, and **proposed acceptance criteria** for you to
   agree with or change. If you asked for several independent things, it takes one and gives you a
   ready-to-paste `/loopsmith-task …` line for each of the others.
2. **Round 2:** the spec (title, goal, acceptance criteria, out of scope, notes, the files you gave),
   exactly as it will describe the pull request. You say go.
3. It works on its branch, `claude/<slug>`. Talk to it in the session at any time: questions,
   answers, more screenshots.
4. When it's ready for review, it opens the pull request with the spec as its description, the
   acceptance criteria ticked, and how to check the change on the preview
   (`…/pr-preview/pr-<PR number>/`), and gives you the link. Review there or in the session:
   comments and reviews on the PR wake it within seconds. Resolve the threads and
   **Squash and merge**.
5. It keeps watching through any number of review rounds, until the PR is merged or closed.

The session never merges, approves or pushes to `main`, and it replies in review threads without
resolving them. Names follow CLAUDE.md, "Naming (agent work)".

## Working with a session

- **Screenshots and files** go into the session, at the start or any time later. They stay there:
  nothing is committed unless the work needs a file in the repository (a loop file as a test case),
  and then the session checks it for personal data first. A session can't open images dropped into
  a GitHub comment.
- **Notifications.** The session's comments carry your GitHub name, so GitHub doesn't notify you
  about them; the Claude app does.
- **Merge conflicts.** GitHub doesn't always tell a session when `main` moves under its pull request.
  The session checks its PR on every event and every few hours while it waits, and resolves a
  conflict by merging `main` into its branch. To have it done now, tell it "merge origin/main into
  this branch".
- **Out of date.** A PR that is only behind `main` waits for you: click **Update branch**.
- **Dropping a task.** Tell the session, close the PR if it has one, click **Delete branch** (a
  session can't delete branches), and archive the session.
