open System

open Generaptor
open Generaptor.GitHubActions
open type Generaptor.GitHubActions.Commands
open type Generaptor.Library.Actions
open type Generaptor.Library.Patterns

let mainBranch = "main"
let linuxImage = "ubuntu-24.04"
let images = [
    "macos-14"
    linuxImage
    "windows-2025"
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

        job "verify-workflows" [
            runsOn "ubuntu-24.04"
            setEnv "DOTNET_CLI_TELEMETRY_OPTOUT" "1"
            setEnv "DOTNET_NOLOGO" "1"
            setEnv "NUGET_PACKAGES" "${{ github.workspace }}/.github/nuget-packages"
            step(
                uses = "actions/checkout@v4"
            )
            step(
                uses = "actions/setup-dotnet@v4"
            )
            step(
                uses = "actions/cache@v4",
                options = Map.ofList [
                    "key", "${{ runner.os }}.nuget.${{ hashFiles('**/*.fsproj') }}"
                    "path", "${{ env.NUGET_PACKAGES }}"
                ]
            )
            step(
                run = "dotnet run --project Infrastructure/GitHubActions -- verify"
            )
        ]

        job "licenses" [
            runsOn linuxImage
            step(usesSpec = Auto "actions/checkout")
            step(usesSpec = Auto "fsfe/reuse-action")
        ]

        job "encodings" [
            runsOn linuxImage
            step(uses = "actions/checkout@v4")
            let verifyEncodingVersion = "2.2.0"
            step(
                shell = "pwsh",
                run = "Install-Module VerifyEncoding " +
                      "-Repository PSGallery " +
                      $"-RequiredVersion {verifyEncodingVersion} " +
                      "-Force && Test-Encoding"
            )
        ]
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

[<EntryPoint>]
let main(args: string[]): int =
    EntryPoint.Process args workflows
