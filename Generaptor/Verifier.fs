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

let private NormalizeText(s: string) = s.ReplaceLineEndings("\n").Trim()

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
        | Some _, _ ->
            errors.Add (
                $"The content of the file \"{yaml.Value}\" differs " +
                $"from the generated content for the workflow \"{wf.Id}\"."
            )

        files.Remove yaml |> ignore

    for remaining in files do
        errors.Add $"File \"{remaining.Value}\" does not correspond to any generated workflow."

    return {
        Errors = Array.ofSeq errors
    }
}
