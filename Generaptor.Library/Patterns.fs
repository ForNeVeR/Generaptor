// SPDX-FileCopyrightText: 2024-2025 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

namespace Generaptor.Library

open Generaptor
open Generaptor.GitHubActions
open type GitHubActions.Commands

#nowarn "25"

type Patterns =
    static member addMatrix (images: string seq) (AddJob job): WorkflowCreationCommand =
        AddJob { job with
                   RunsOn = Some "${{ matrix.image }}"
                   Strategy = Some {
                       Matrix = Map.ofSeq [ "image", images ]
                       FailFast = Some false
                   }
               }

    static member dotNetBuildAndTest(?sdkVersion: string, ?projectFileExtensions: string seq): JobCreationCommand seq =
        let sdkVersion = defaultArg sdkVersion "8.0.x"
        let projectFileExtensions = defaultArg projectFileExtensions [ ".fsproj" ]
        [
            setEnv "DOTNET_NOLOGO" "1"
            setEnv "DOTNET_CLI_TELEMETRY_OPTOUT" "1"
            setEnv "NUGET_PACKAGES" "${{ github.workspace }}/.github/nuget-packages"

            step(
                name = "Set up .NET SDK",
                uses = "actions/setup-dotnet@v4",
                options = Map.ofList [
                    "dotnet-version", sdkVersion
                ]
            )
            let hashFiles =
                projectFileExtensions
                |> Seq.map (fun ext -> $"'**/*{ext}'")
                |> String.concat ", "
            step(
                name = "NuGet cache",
                uses = "actions/cache@v4",
                options = Map.ofList [
                    "path", "${{ env.NUGET_PACKAGES }}"
                    "key", "${{ runner.os }}.nuget.${{ hashFiles(" + hashFiles + ") }}"
                ]
            )
            step(
                name = "Build",
                run = "dotnet build"
            )
            step(
                name = "Test",
                run = "dotnet test",
                timeoutMin = 10
            )
        ]

    static member ifCalledOnTagPush(steps: JobCreationCommand seq): JobCreationCommand seq =
        steps |>
        Seq.map(
            function
            | AddStep ({ Condition = None } as step) ->
                AddStep { step with
                            Condition = Some "startsWith(github.ref, 'refs/tags/v')"
                        }
            | AddStep step -> failwith $"Step {step} has a condition already."
            | x -> x
        )
