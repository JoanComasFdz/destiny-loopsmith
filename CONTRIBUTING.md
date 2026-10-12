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
   exactly as it will open the pull request. You say go.
3. It opens a **draft** pull request from `claude/<slug>` with the spec as its description, and
   watches it.
4. It works on that branch. Talk to it in the session, or on the PR: comments and reviews there wake
   it within seconds, asked for or not.
5. When it's done, it ticks the acceptance criteria and says **Ready for review**. Check the change
   on the preview (`…/pr-preview/pr-<PR number>/`), click **Ready for review** (a session can't take a
   PR out of draft), review, resolve the threads and **Squash and merge**.
6. It keeps watching through any number of review rounds, until the PR is merged or closed.

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
- **Dropping a task.** Close the PR, then click **Delete branch** on the closed PR (a session can't
  delete branches), and archive the session.
