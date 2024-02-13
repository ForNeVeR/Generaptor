module Generaptor.Library.Actions

open type Generaptor.GitHubActions.Commands

let checkout = step(uses = "actions/checkout@v4")
