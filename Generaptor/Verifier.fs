// SPDX-FileCopyrightText: 2025 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module Generaptor.Verifier

open System.Collections.Generic
open System.IO
open System.Text
open System.Threading.Tasks
open DiffPlex
open DiffPlex.DiffBuilder
open DiffPlex.DiffBuilder.Model
open Generaptor.GitHubActions
open TruePath

type VerificationResult =
    {
        Errors: string[]
    }
    member this.Success = this.Errors.Length = 0

let private NormalizeText(s: string): string =
    s.Trim().Split "\n" |> Seq.map _.TrimEnd() |> String.concat "\n"

let private GenerateDiff(oldContent: string, newContent: string): string =
    let diffBuilder = InlineDiffBuilder(Differ())
    let diff = diffBuilder.BuildDiffModel(NormalizeText oldContent, NormalizeText newContent)
    
    let output = StringBuilder()
    output.AppendLine("--- Actual (current file)") |> ignore
    output.AppendLine("+++ Expected (generated content)") |> ignore
    
    for line in diff.Lines do
        match line.Type with
        | ChangeType.Inserted -> output.AppendLine($"+ {line.Text}") |> ignore
        | ChangeType.Deleted -> output.AppendLine($"- {line.Text}") |> ignore
        | ChangeType.Imaginary -> () // Skip imaginary lines (used for side-by-side alignment)
        | ChangeType.Unchanged -> output.AppendLine($"  {line.Text}") |> ignore
        | ChangeType.Modified ->
            // Modified lines in inline diff should not occur, but handle as unchanged if they do
            output.AppendLine($"  {line.Text}") |> ignore
        | _ ->
            // Handle any unexpected ChangeType values as unchanged
            output.AppendLine($"  {line.Text}") |> ignore
    
    output.ToString().TrimEnd()

let VerifyWorkflows(
    workflowDir: LocalPath,
    workflows: Workflow seq,
    client: IActionsClient
): Task<VerificationResult> = task {
    let files = HashSet(Directory.GetFiles(workflowDir.Value, "*.yml") |> Seq.map LocalPath)
    let errors = ResizeArray()
    for wf in workflows do
        printfn $"Verifying workflow {wf.Id}…"
        let yaml = workflowDir / (wf.Id + ".yml")
        let! newContent = Serializers.GenerateWorkflowContent(yaml, wf, client)
        let! oldContent =
            if File.Exists yaml.Value
            then task {
                let! content = File.ReadAllTextAsync(yaml.Value)
                return Some content
            }
            else Task.FromResult None
        match oldContent, newContent with
        | None, _ -> errors.Add $"File for the workflow \"{wf.Id}\" doesn't exist: \"{yaml.Value}\"."
        | Some x, y when NormalizeText x = NormalizeText y -> ()
        | Some x, y ->
            let diff = GenerateDiff(x, y)
            errors.Add (
                $"The content of the file \"{yaml.Value}\" differs " +
                $"from the generated content for the workflow \"{wf.Id}\".\n{diff}"
            )

        files.Remove yaml |> ignore

    for remaining in files do
        errors.Add $"File \"{remaining.Value}\" does not correspond to any generated workflow."

    return {
        Errors = Array.ofSeq errors
    }
}
