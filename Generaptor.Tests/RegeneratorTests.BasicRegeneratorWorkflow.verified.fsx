#r "nuget: Generaptor.Library, <GENERAPTOR_VERSION>"
open Generaptor
open Generaptor.GitHubActions
open type Generaptor.GitHubActions.Commands
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
            step(
                name = "Set up .NET SDK",
                uses = "actions/setup-dotnet@v4",
                options = Map.ofList [
                    "dotnet-version", "8.0.x"
                ]
            )
            step(
                name = "NuGet cache",
                uses = "actions/cache@v4",
                options = Map.ofList [
                    "key", "${{ runner.os }}.nuget.${{ hashFiles('**/*.fsproj') }}"
                    "path", "${{ env.NUGET_PACKAGES }}"
                ]
            )
            step(
                name = "Test",
                run = "dotnet test",
                timeoutMin = 10
            )
        ]
    ]
    workflow "2" [
        name "Release"
        onPushTo "main"
        onPushTags "v*"
        onPullRequestTo "main"
        onSchedule "0 0 * * 6"
        onWorkflowDispatch
        job "nuget" [
            jobPermission(PermissionKind.Contents, AccessKind.Write)
            runsOn "ubuntu-latest"
            step(
                uses = "actions/checkout@v4"
            )
            step(
                id = "version",
                name = "Get version",
                shell = "pwsh",
                run = "echo \"version=$(Scripts/Get-Version.ps1 -RefName $env:GITHUB_REF)\" >> $env:GITHUB_OUTPUT"
            )
            step(
                run = "dotnet pack --configuration Release -p:Version=${{ steps.version.outputs.version }}"
            )
            step(
                name = "Read changelog",
                uses = "ForNeVeR/ChangelogAutomation.action@v1",
                options = Map.ofList [
                    "output", "./release-notes.md"
                ]
            )
            step(
                name = "Upload artifacts",
                uses = "actions/upload-artifact@v4",
                options = Map.ofList [
                    "path", "./release-notes.md\n./Generaptor/bin/Release/Generaptor.${{ steps.version.outputs.version }}.nupkg\n./Generaptor/bin/Release/Generaptor.${{ steps.version.outputs.version }}.snupkg\n./Generaptor.Library/bin/Release/Generaptor.Library.${{ steps.version.outputs.version }}.nupkg\n./Generaptor.Library/bin/Release/Generaptor.Library.${{ steps.version.outputs.version }}.snupkg"
                ]
            )
        ]
    ]
    workflow "docs" [
        name "Docs"
        onPushTo "main"
        onWorkflowDispatch
        workflowPermission(PermissionKind.Actions, AccessKind.Read)
        workflowPermission(PermissionKind.Pages, AccessKind.Write)
        workflowPermission(PermissionKind.IdToken, AccessKind.Write)
        workflowConcurrency(
            group = "pages",
            cancelInProgress = false
        )
        job "publish-docs" [
            environment(name = "github-pages", url = "${{ steps.deployment.outputs.page_url }}")
            runsOn "ubuntu-22.04"
            step(
                name = "Checkout",
                uses = "actions/checkout@v4"
            )
            step(
                name = "Dotnet Setup",
                uses = "actions/setup-dotnet@v4"
            )
            step(
                run = "dotnet tool restore"
            )
            step(
                run = "dotnet docfx docs/docfx.json"
            )
            step(
                name = "Upload artifact",
                uses = "actions/upload-pages-artifact@v3",
                options = Map.ofList [
                    "path", "docs/_site"
                ]
            )
            step(
                name = "Deploy to GitHub Pages",
                id = "deployment",
                uses = "actions/deploy-pages@v4"
            )
        ]
    ]
]
EntryPoint.Process fsi.CommandLineArgs workflows
