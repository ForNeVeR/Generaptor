module Generaptor.EntryPoint

open System.IO

open Generaptor.GitHubActions

let private printUsage() =
    printfn "%s" ("""Possible arguments:
    generate - generate GitHub Actions workflows .github/workflows subdirectory of the current directory
""".ReplaceLineEndings @"\n")

let private generateWorkflows (workflows: Workflow seq) =
    let dir = Path.Combine(".github", "workflows")
    for wf in workflows do
        Directory.CreateDirectory dir |> ignore

        let id = wf.Id
        let path = Path.Combine(dir, id + ".yml")
        printfn $"Generating workflow {id}"
        File.WriteAllText(path, "# This file is auto-generated.\n" + Serializers.Stringify wf)

let Process(args: string seq) (workflows: Workflow seq): int =
    let args = Seq.toArray args
    match args with
    | [||] | [|"generate"|] -> generateWorkflows workflows; 0
    | _ -> printUsage(); 1
