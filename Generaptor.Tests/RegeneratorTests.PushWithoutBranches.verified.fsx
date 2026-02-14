#r "nuget: Generaptor.Library, <GENERAPTOR_VERSION>"
open Generaptor
open Generaptor.GitHubActions
open type Generaptor.GitHubActions.Commands
let workflows = [
    workflow "1" [
        onPush
        job "main" [
            runsOn "ubuntu-latest"
        ]
    ]
]
exit <| EntryPoint.Process fsi.CommandLineArgs workflows