// SPDX-FileCopyrightText: 2024-2025 Generaptor contributors <https://github.com/ForNeVeR/Generaptor>
//
// SPDX-License-Identifier: MIT

module Generaptor.EntryPoint

open System.IO
open System.Threading.Tasks
open Generaptor.GitHubActions
open TruePath

let private printUsage() =
    printfn "%s" ("""Possible arguments:
    generate - generate GitHub Actions workflows in .github/workflows subdirectory of the current directory
    regenerate <file-name.fsx> - generate Generaptor script from .github/workflows subdirectory of the current directory
""".ReplaceLineEndings "\n")

let private findRepositoryRoot() =
    let mutable current = Directory.GetCurrentDirectory()
    let mutable found = None

    while found.IsNone && not (isNull current) do
        if Directory.Exists(Path.Combine(current, ".git")) then
            found <- Some current
        else
            current <- Directory.GetParent(current) |> Option.ofObj |> Option.map _.FullName |> Option.toObj

    defaultArg found (Directory.GetCurrentDirectory())

let private workflowDirectory() =
    LocalPath(Path.Combine(findRepositoryRoot(), ".github", "workflows"))

let private generateWorkflows(workflows: Workflow seq): Task =
    let dir = workflowDirectory()
    let actionsClient = ActionsClient()
    task {
        for wf in workflows do
            Directory.CreateDirectory dir.Value |> ignore
            let yaml = dir / (wf.Id + ".yml")
            let! content = Serializers.GenerateWorkflowContent(yaml, wf, actionsClient)
            do! File.WriteAllTextAsync(yaml.Value, content)
    }

let private regenerate(fileName: LocalPath) =
    let dir = workflowDirectory()
    let script = ScriptGenerator.GenerateFrom dir
    File.WriteAllText(fileName.Value, script)

let private runSynchronously(t: Task) =
    t.GetAwaiter().GetResult()

module ExitCodes =
    let Success = 0
    let ArgumentsNotRecognized = 1
    let VerificationError = 2

let private Verify workflows =
    (task {
        let actionsClient = ActionsClient()
        let dir = workflowDirectory()
        let! result = Verifier.VerifyWorkflows(dir, workflows, actionsClient)
        for error in result.Errors do
            eprintfn $"%s{error}"
        return if result.Success then ExitCodes.Success else ExitCodes.VerificationError
    }).GetAwaiter().GetResult()

let Process(args: string seq) (workflows: Workflow seq): int =
    let args = Seq.toArray args
    let args =
        if args.Length > 0 && args[0].EndsWith ".fsx"
        then Array.skip 1 args
        else args

    match args with
    | [||] | [|"generate"|] -> runSynchronously <| generateWorkflows workflows; ExitCodes.Success
    | [|"regenerate"; fileName|] -> regenerate(LocalPath fileName); ExitCodes.Success
    | [|"verify"|] -> Verify workflows
    | _ -> printUsage(); ExitCodes.ArgumentsNotRecognized
