module Generaptor.Tests.GeneratorTests

open Xunit

open Generaptor
open Generaptor.GitHubActions
open type Generaptor.GitHubActions.Commands

let private doTest (expected: string) wf =
    let actual = Serializers.Stringify wf
    Assert.Equal(expected.ReplaceLineEndings "\n", actual)

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
    doTest expected wf

[<Fact>]
let ``Condition tests``(): unit =
    let wf = workflow("wf") [|
        onPushTo "main"
        job "main" [|
            step(uses = "aaa")
            step(uses = "bbb", condition = "goes here")
        |]
    |]
    let expected = """on:
  push:
    branches:
    - main
jobs:
  main:
    steps:
    - uses: aaa
    - if: goes here
      uses: bbb
"""
    doTest expected wf

[<Fact>]
let ``Environment tests``(): unit =
    let wf = workflow("wf") [|
        onPushTo "main"
        job "main" [|
            step(uses = "aaa")
            step(uses = "bbb", env = Map.ofList [ "foo", "bar" ])
        |]
    |]
    let expected = """on:
  push:
    branches:
    - main
jobs:
  main:
    steps:
    - uses: aaa
    - uses: bbb
      env:
        foo: bar
"""
    doTest expected wf
