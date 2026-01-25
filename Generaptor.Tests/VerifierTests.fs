// SPDX-FileCopyrightText: 2025 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module Generaptor.Tests.VerifierTests

open System.IO
open System.Threading.Tasks
open Generaptor.GitHubActions
open type Generaptor.GitHubActions.Commands
open Generaptor.Verifier
open TruePath
open Xunit

let private DoTest(files: (string * string) seq, workflows): Task<AbsolutePath * VerificationResult> = task {
    let tempDir =
        let path = Path.GetTempFileName()
        File.Delete path
        Directory.CreateDirectory path |> ignore
        AbsolutePath path
    try
        for fileName, fileContent in files do
            let filePath = tempDir / fileName
            File.WriteAllText(filePath.Value, fileContent)

        let! result = VerifyWorkflows(LocalPath tempDir, workflows, TestFramework.MockActionsClient)
        return tempDir, result
    finally
        Directory.Delete(tempDir.Value, true)
}

[<Fact>]
let ``Verification success``(): Task =
    let files = [|
        "wf.yml", """# This file is auto-generated.
on: {}
jobs:
  main:
    strategy:
      matrix:
        config:
        - image: macos-latest
          name: macos
        - image: ubuntu-24.04
          name: linux
        - image: windows-2022
          name: windows
    runs-on: ubuntu-latest
"""
    |]
    let wf = workflow "wf" [
        job "main" [|
            runsOn "ubuntu-latest"
            strategy(matrix = [|
                "config", [
                    Map.ofList [
                        "name", "macos"
                        "image", "macos-latest"
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
            |])
        |]
    ]
    task {
        let! _, content = DoTest(files, [|wf|])
        Assert.Equal({
            Errors = Array.empty
        }, content)
    }

[<Fact>]
let ``Verification failure: content not equal``(): Task =
    let files = [|
        "wf.yml", """# incorrect content
jobs:
  main:
    strategy:
      matrix:
        config:
        - image: macos-latest
          name: macos
        - image: ubuntu-24.04
          name: linux
        - image: windows-2022
          name: windows
    runs-on: ubuntu-latest
"""
    |]
    let wf = workflow "wf" [
        job "main" [|
            runsOn "ubuntu-latest"
            strategy(matrix = [|
                "config", [
                    Map.ofList [
                        "name", "macos"
                        "image", "macos-latest"
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
            |])
        |]
    ]
    task {
        let! (path, content) = DoTest(files, [|wf|])
        let file = path / (wf.Id + ".yml")
        Assert.Single(content.Errors) |> ignore
        let error = content.Errors[0]
        Assert.StartsWith(
            $"The content of the file \"{file.Value}\" differs from the generated content for the workflow \"wf\".",
            error
        )
        Assert.Contains("--- a/", error)
        Assert.Contains("+++ b/", error)
        Assert.Contains("-# incorrect content", error)
        Assert.Contains("+# This file is auto-generated.", error)
    }

[<Fact>]
let ``Verification failure: file is absent``(): Task =
    let wf = workflow "wf" []
    task {
        let! (path, content) = DoTest(Array.empty, [|wf|])
        let file = path / (wf.Id + ".yml")
        Assert.Equal({
            Errors = [|
                $"File for the workflow \"{wf.Id}\" doesn't exist: \"{file.Value}\"."
            |]
        }, content)
    }

[<Fact>]
let ``Verification failure: redundant file``(): Task =
    let files = [|
        "wf.yml", "# this is a redundant file"
    |]
    task {
        let! (path, content) = DoTest(files, Array.empty)
        let file = path / "wf.yml"
        Assert.Equal({
            Errors = [|
                $"File \"{file}\" does not correspond to any generated workflow."
            |]
        }, content)
    }
