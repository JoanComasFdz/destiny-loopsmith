# Contributing

## Agent workflow

Issues labelled `agent` are implemented by a Claude routine. When @JoanComasFdz labels an issue, a Claude session branches from `main` as `claude/issue-<number>` and implements it. It opens a pull request that starts with `Closes #<number>` and says how to check the change on the PR preview. The session never merges, approves or pushes to `main`, and it replies in review threads without resolving them. If the issue is unclear, the session comments its questions on the issue and stops.
