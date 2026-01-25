// SPDX-FileCopyrightText: 2025 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module Generaptor.Verifier

open System.Collections.Generic
open System.IO
open System.Threading.Tasks
open DiffPlex.Renderer
open Generaptor.GitHubActions
open TruePath
open TruePath.SystemIo

type VerificationResult =
    {
        Errors: string[]
    }
    member this.Success = this.Errors.Length = 0

let private NormalizeText(s: string): string =
    s.Trim().Split "\n" |> Seq.map _.TrimEnd() |> String.concat "\n"

let private GenerateDiff(
    oldContent: string,
    newContent: string,
    repoRoot: AbsolutePath,
    file: AbsolutePath
): string =
    let oldFileName = LocalPath "a" / file.RelativeTo repoRoot
    let newFileName = LocalPath "b" / file.RelativeTo repoRoot
    UnidiffRenderer.GenerateUnidiff(
        oldContent,
        newContent,
        oldFileName.Value.Replace(Path.DirectorySeparatorChar, '/'),
        newFileName.Value.Replace(Path.DirectorySeparatorChar, '/'),
        ignoreWhitespace = false
    )

let VerifyWorkflows(
    workflowDir: LocalPath,
    workflows: Workflow seq,
    client: IActionsClient
): Task<VerificationResult> = task {
    let repoRoot = lazy (
        let containsGitDir(folder: AbsolutePath) =
            (folder / ".git").ExistsDirectory()

        let mutable current = workflowDir.ResolveToCurrentDirectory()
        while not(containsGitDir current) && current.Parent.HasValue do
            current <- current.Parent.Value
        current
    )
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
            let diff = GenerateDiff(x, y, repoRoot.Value, yaml.ResolveToCurrentDirectory())
            errors.Add (
                $"The content of the file \"{yaml.Value}\" differs " +
                $"from the generated content for the workflow \"{wf.Id}\".\n\n{diff}"
            )

        files.Remove yaml |> ignore

    for remaining in files do
        errors.Add $"File \"{remaining.Value}\" does not correspond to any generated workflow."

    return {
        Errors = Array.ofSeq errors
    }
}
