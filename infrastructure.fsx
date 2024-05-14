// In the actual integration, you'll only need the one line below and no other references.
//#r "nuget: Generaptor.Library, 1.1.0"

// In this repository, since we are developing Generaptor and cannot rely on NuGet, we have to add these three.
#r "nuget: YamlDotNet, 15.1.1"
#r "Generaptor.Library/bin/Debug/net8.0/generaptor.library.dll"
#r "Generaptor.Library/bin/Debug/net8.0/generaptor.dll"

open System

open Generaptor
open Generaptor.GitHubActions
open type Generaptor.GitHubActions.Commands
open type Generaptor.Library.Actions
open type Generaptor.Library.Patterns

let mainBranch = "main"
let linuxImage = "ubuntu-latest"
let images = [
    "macos-latest"
    linuxImage
    "windows-latest"
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
            runsOn linuxImage
            checkout
            writeContentPermissions

            let configuration = "Release"

            let versionStepId = "version"
            let versionField = "${{ steps." + versionStepId + ".outputs.version }}"
            getVersionWithScript(stepId = versionStepId, scriptPath = "Scripts/Get-Version.ps1")
            dotNetPack(version = versionField)

            let releaseNotes = "./release-notes.md"
            prepareChangelog(releaseNotes)
            let artifacts projectName includeSNuPkg = [
                $"./{projectName}/bin/{configuration}/{projectName}.{versionField}.nupkg"
                if includeSNuPkg then $"./{projectName}/bin/{configuration}/{projectName}.{versionField}.snupkg"
            ]
            let allArtifacts = [
                yield! artifacts "Generaptor" true
                yield! artifacts "Generaptor.Library" true
            ]
            uploadArtifacts [
                releaseNotes
                yield! allArtifacts
            ]
            yield! ifCalledOnTagPush [
                createRelease(
                    name = $"Generaptor {versionField}",
                    releaseNotesPath = releaseNotes,
                    files = allArtifacts
                )
                yield! pushToNuGetOrg "NUGET_TOKEN" [
                    yield! artifacts "Generaptor" false
                    yield! artifacts "Generaptor.Library" false
                ]
            ]
        ]
    ]
]

EntryPoint.Process fsi.CommandLineArgs workflows
