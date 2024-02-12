module Generaptor.Tests

open Xunit

open Generaptor.GitHubActions
open type Generaptor.GitHubActions.Library

[<Fact>]
let ``Basic workflow gets generated``(): unit =
    let wf = workflow("wf") [|
        onPush "main"

        job "main" [|
            runsOn "ubuntu-latest"
            step(uses = "actions/checkout@v4")
        |]
    |]
    let expected = """
name: wf
on:
  push:
    branches:
      - main
  pull_request:
    branches:
      - main
jobs:
  main:
    steps:
      - uses: actions/checkout@v4
"""
    Assert.Equal(expected.ReplaceLineEndings @"\n", (Stringify wf))
