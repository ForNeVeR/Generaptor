#r "nuget: Generaptor.Library, <GENERAPTOR_VERSION>"
open Generaptor
open Generaptor.GitHubActions
open type Generaptor.GitHubActions.Commands
let workflows = [
    workflow "1" [
        job "test" [
            step(
                name = "Run unit tests",
                run = "dotnet test",
                workingDirectory = "Fabricator.Tests"
            )
            step(
                name = "Run integration tests",
                run = "dotnet test",
                workingDirectory = "Fabricator.IntegrationTests"
            )
        ]
    ]
]
exit <| EntryPoint.Process fsi.CommandLineArgs workflows