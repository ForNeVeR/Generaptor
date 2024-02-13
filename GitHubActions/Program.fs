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
    let mainTriggers = [
        onPushTo mainBranch
        onPullRequestTo mainBranch
        onSchedule(day = DayOfWeek.Saturday)
        onWorkflowDispatch
    ]

    workflow "main" [
        name "Main"
        yield! mainTriggers
        job "main" [
            checkout
            yield! dotNetBuildAndTest()
        ] |> addMatrix images
    ]
    workflow "release" [
        name "Release"
        yield! mainTriggers
        onPushTags "v*"
        job "nuget" [
            checkout

            let configuration = "Release"

            let versionStepId = "version"
            let versionField = "${{ steps." + versionStepId + ".outputs.version }}"
            getVersionWithScript versionStepId "Scripts/Get-Version.ps1"
            dotNetPack(version = versionStepId)

            let releaseNotes = "./release-notes.md"
            prepareChangelog(releaseNotes)
            let artifacts projectName includeSNuPkg = [
                $"./{projectName}/bin/{configuration}/{projectName}.{versionField}.nupkg"
                if includeSNuPkg then $"./{projectName}/bin/{configuration}/{projectName}.{versionField}.snupkg"
            ]
            uploadArtifacts [
                releaseNotes
                yield! artifacts "Generaptor" true
                yield! artifacts "Generaptor.Library" true
            ]
            ifCalledOnTagPush [
                createRelease(
                    name = $"Generaptor {versionField}",
                    releaseNotes = releaseNotes
                )
                pushToNuGetOrg "NUGET_TOKEN" [
                    yield! artifacts "Generaptor" false
                    yield! artifacts "Generaptor.Library" false
                ]
            ]
        ]
    ]
]

[<EntryPoint>]
let main(args: string[]) : int =
    EntryPoint.Process args workflows
