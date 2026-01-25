// SPDX-FileCopyrightText: 2025 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module Generaptor.Verifier

open System.Collections.Generic
open System.IO
open System.Threading.Tasks
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
    let oldLines = NormalizeText(oldContent).Split "\n"
    let newLines = NormalizeText(newContent).Split "\n"
    let maxLines = max oldLines.Length newLines.Length
    
    let diffLines = ResizeArray()
    diffLines.Add "--- Actual (current file)"
    diffLines.Add "+++ Expected (generated content)"
    
    for i in 0 .. maxLines - 1 do
        let oldLine = if i < oldLines.Length then Some oldLines[i] else None
        let newLine = if i < newLines.Length then Some newLines[i] else None
        
        match oldLine, newLine with
        | Some o, Some n when o = n -> 
            diffLines.Add $"  {o}"
        | Some o, Some n ->
            diffLines.Add $"- {o}"
            diffLines.Add $"+ {n}"
        | Some o, None ->
            diffLines.Add $"- {o}"
        | None, Some n ->
            diffLines.Add $"+ {n}"
        | None, None -> ()
    
    String.concat "\n" diffLines

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
