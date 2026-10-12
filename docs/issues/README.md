# Issue resources

The screenshots and files attached to an agent issue don't live on `main`. Each issue that has any
gets its own branch, `claude/issue-<N>-resources`: an orphan branch holding only that issue's files,
under this same path:

```
docs/issues/issue-<N>/resources/01-<what>.<ext>
                                02-<what>.<ext>
```

The `loopsmith-issue` skill creates the branch when it opens the issue and links every file from
the issue's "Screenshots and files" section, at the commit that added it. The session working the
issue reads them from there and adds files you paste into it later. These branches are never merged
and never deleted: the issue's links point at their commits.

Naming: CLAUDE.md, "Naming (agent work)".
