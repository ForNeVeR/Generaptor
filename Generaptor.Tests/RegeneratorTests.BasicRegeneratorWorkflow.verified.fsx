#r "nuget: Generaptor.Library, <GENERAPTOR_VERSION>"
let workflows = [
    workflow "1" [
        name "Main"
        onPushTo "main"
        onPullRequestTo "main"
        onSchedule "0 0 * * 6"
        onWorkflowDispatch
        job "main" [
            strategy(failFast = false, matrix = [
                "image", [
                    "macos-latest"
                    "ubuntu-latest"
                    "windows-latest"
                ]
            ])
            runsOn "${{ matrix.image }}"
            setEnv "DOTNET_CLI_TELEMETRY_OPTOUT" "1"
            setEnv "DOTNET_NOLOGO" "1"
            setEnv "NUGET_PACKAGES" "${{ github.workspace }}/.github/nuget-packages"
            step(
              uses = "actions/checkout@v4"
            )
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
]
EntryPoint.Process fsi.CommandLineArgs workflows
