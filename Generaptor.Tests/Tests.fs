module Generaptor.Tests

open Xunit

open Generaptor
open Generaptor.GitHubActions
open type Generaptor.GitHubActions.Library

[<Fact>]
let ``Basic workflow gets generated``(): unit =
    let wf = workflow("wf") [|
        onPushTo "main"

        job "main" [|
            runsOn "ubuntu-latest"
            step(uses = "actions/checkout@v4")
        |]
    |]
    let expected = """name: wf
on:
  push:
    branches:
    - main
jobs:
  main:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v4
"""
    Assert.Equal(expected.ReplaceLineEndings @"\n", Serializers.Stringify wf)
