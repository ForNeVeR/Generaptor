module Generaptor.Tests

open Xunit

open Generaptor
open Generaptor.GitHubActions
open type Generaptor.GitHubActions.Commands

[<Fact>]
let ``Basic workflow gets generated``(): unit =
    let wf = workflow("wf") [|
        name "Main"
        onPushTo "main"

        job "main" [|
            needs "another"
            runsOn "ubuntu-latest"
            step(uses = "actions/checkout@v4")
        |]
    |]
    let expected = """name: Main
on:
  push:
    branches:
    - main
jobs:
  main:
    needs:
    - another
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v4
"""
    Assert.Equal(expected.ReplaceLineEndings "\n", Serializers.Stringify wf)
