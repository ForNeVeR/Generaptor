// SPDX-FileCopyrightText: 2024-2025 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

namespace Generaptor.Library

open Generaptor.GitHubActions
open type Generaptor.GitHubActions.Commands

type Actions =
    static member checkout: JobCreationCommand = step(usesSpec = Auto "actions/checkout")

    static member dotNetPack(version: string): JobCreationCommand =
        step(
            run = $"dotnet pack --configuration Release -p:Version={version}"
        )

    static member getVersionWithScript(stepId: string, scriptPath: string): JobCreationCommand =
        step(
            id = stepId,
            name = "Get version",
            shell = "pwsh",
            run = $"""echo "version=$({scriptPath} -RefName $env:GITHUB_REF)" >> $env:GITHUB_OUTPUT"""
        )

    static member prepareChangelog(outputPath: string): JobCreationCommand =
        step(
            name = "Read changelog",
            usesSpec = Auto "ForNeVeR/ChangelogAutomation.action",
            options = Map.ofList [
                "output", outputPath
            ]
        )

    static member uploadArtifacts(artifacts: string seq, ?actionVersion: string): JobCreationCommand =
        let uses, usesSpec =
            match actionVersion with
            | None -> None, Some <| Auto "actions/upload-artifact"
            | Some version -> Some $"actions/upload-artifact@{version}", None
        step(
            name = "Upload artifacts",
            ?uses = uses,
            ?usesSpec = usesSpec,
            options = Map.ofList [
                "path", String.concat "\n" artifacts
            ]
        )

    static member createRelease(name: string, releaseNotesPath: string, files: string seq, ?actionVersion: string): JobCreationCommand =
        let version = defaultArg actionVersion "v2"
        step(
            name = "Create a release",
            uses = $"softprops/action-gh-release@{version}",
            options = Map.ofList [
                "name", name
                "body_path", releaseNotesPath
                "files", String.concat "\n" files
            ]
        )

    static member pushToNuGetOrg (nuGetApiKeyId: string) (artifacts: string seq) : JobCreationCommand seq =
        artifacts
        |> Seq.map (fun artifact ->
            step (
                name = "Push artifact to NuGet",
                run =
                    $"dotnet nuget push {artifact} --source https://api.nuget.org/v3/index.json --api-key "
                    + "${{ secrets." + nuGetApiKeyId + " }}"
            ))
