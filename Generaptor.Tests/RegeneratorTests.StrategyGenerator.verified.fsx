#r "nuget: Generaptor.Library, <GENERAPTOR_VERSION>"
open Generaptor
open Generaptor.GitHubActions
open type Generaptor.GitHubActions.Commands
let workflows = [
    workflow "1" [
        job "main" [
            strategy(failFast = false, matrix = [
                "config", [
                    Map.ofList [
                        "name", "macos"
                        "image", "macos-14"
                    ]
                    Map.ofList [
                        "name", "linux"
                        "image", "ubuntu-24.04"
                    ]
                    Map.ofList [
                        "name", "windows"
                        "image", "windows-2022"
                    ]
                ]
            ])
            jobName "main.${{ matrix.config.name }}"
        ]
    ]
]
EntryPoint.Process fsi.CommandLineArgs workflows