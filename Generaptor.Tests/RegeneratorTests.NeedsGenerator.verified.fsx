#r "nuget: Generaptor.Library, <GENERAPTOR_VERSION>"
open Generaptor
open Generaptor.GitHubActions
open type Generaptor.GitHubActions.Commands
let workflows = [
    workflow "1" [
        job "build" [
            runsOn "ubuntu-latest"
            step(
                uses = "actions/checkout@v4"
            )
        ]
        job "test" [
            needs "build"
            runsOn "ubuntu-latest"
            step(
                uses = "actions/checkout@v4"
            )
        ]
    ]
]
exit <| EntryPoint.Process fsi.CommandLineArgs workflows