open System

open Generaptor
open Generaptor.GitHubActions
open type Generaptor.GitHubActions.Commands
open Generaptor.Library.Actions
open type Generaptor.Library.Patterns

let mainBranch = "main"
let images = [
    "macos-12"
    "ubuntu-22.04"
    "windows-2022"
]

let workflows = [
    workflow("main") [
        name "Main"
        onPushTo mainBranch
        onPullRequestTo mainBranch
        onSchedule(day = DayOfWeek.Saturday)
        job "main" [
            checkout
            yield! dotNetBuildAndTest()
        ] |> addMatrix images
    ]
]

[<EntryPoint>]
let main(args: string[]) : int =
    EntryPoint.Process args workflows
