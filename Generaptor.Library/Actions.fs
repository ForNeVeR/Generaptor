module Generaptor.Library.Actions

open type Generaptor.GitHubActions.Commands

let checkout = step(uses = "checkout@v4")
