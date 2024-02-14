namespace Generaptor.Library

open Generaptor.GitHubActions
open type Generaptor.GitHubActions.Commands

type Actions =
    static member checkout: JobCreationCommand = step(uses = "actions/checkout@v4")

    static member dotNetPack(version: string): JobCreationCommand =
        step(
            run = $"dotnet pack XamlMath.All.sln --configuration Release -p:Version={version}"
        )

    static member getVersionWithScript(stepId: string, scriptPath: string): JobCreationCommand =
        step(
            id = stepId,
            name = "Get version",
            shell = "pwsh",
            run = scriptPath
        )

    static member prepareChangelog(outputPath: string): JobCreationCommand =
        step(
            name = "Read changelog",
            uses = "ForNeVeR/ChangelogAutomation.action@v1",
            options = Map.ofList [
                "outputPath", outputPath
            ]
        )

    static member uploadArtifacts(artifacts: string seq): JobCreationCommand =
        step(
            name = "Upload artifacts",
            uses = "actions/upload-artifact@v3",
            options = Map.ofList [
                "path", String.concat "\n" artifacts
            ]
        )
