# Contributing

## Writing an agent issue

Open a Claude Code session on this repository (claude.ai/code, or the Code tab in the Claude app)
and run `/agent-issue` followed by what you want, in your own words and any language. It drafts
the issue in English with the Agent task form's sections, shows it to you, and opens it when you
agree, without the `agent` label. Add the label when you want a session to start on it.

## Agent workflow

Issues labelled `agent` are implemented by a Claude routine. When @JoanComasFdz labels an issue, a Claude session branches from `main` as `claude/issue-<number>` and implements it. It opens a pull request that starts with `Closes #<number>` and says how to check the change on the PR preview. The session never merges, approves or pushes to `main`, and it replies in review threads without resolving them. If the issue is unclear, the session comments its questions on the issue and stops.
