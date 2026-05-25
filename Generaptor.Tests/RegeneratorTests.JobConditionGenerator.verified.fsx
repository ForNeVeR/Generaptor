#r "nuget: Generaptor.Library, <GENERAPTOR_VERSION>"
open Generaptor
open Generaptor.GitHubActions
open type Generaptor.GitHubActions.Commands
let workflows = [
    workflow "1" [
        job "main" [
            condition "startsWith(github.ref, 'refs/tags/v')"
            runsOn "ubuntu-24.04"
            step(
                uses = "actions/checkout@v4"
            )
        ]
    ]
]
exit <| EntryPoint.Process fsi.CommandLineArgs workflows
