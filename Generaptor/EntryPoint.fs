module Generaptor.EntryPoint

open System.IO
open Generaptor.GitHubActions
open TruePath

let private printUsage() =
    printfn "%s" ("""Possible arguments:
    generate - generate GitHub Actions workflows in .github/workflows subdirectory of the current directory
    regenerate <file-name.fsx> - generate Generaptor script from .github/workflows subdirectory of the current directory
""".ReplaceLineEndings "\n")

let private generateWorkflows (workflows: Workflow seq) =
    let dir = Path.Combine(".github", "workflows")
    for wf in workflows do
        Directory.CreateDirectory dir |> ignore

        let id = wf.Id
        let path = Path.Combine(dir, id + ".yml")
        printfn $"Generating workflow {id}"
        File.WriteAllText(path, "# This file is auto-generated.\n" + Serializers.Stringify wf)

let private regenerate(fileName: LocalPath) =
    let dir = LocalPath(Path.Combine(".github", "workflows"))
    let script = ScriptGenerator.GenerateFrom dir
    File.WriteAllText(fileName.Value, script)

let Process(args: string seq) (workflows: Workflow seq): int =
    let args = Seq.toArray args
    match args with
    | [||] | [|"generate"|] -> generateWorkflows workflows; 0
    | [|x|] | [|x;"generate"|] when x.EndsWith(".fsx") -> generateWorkflows workflows; 0
    | [|"regenerate"; fileName|] -> regenerate(LocalPath fileName); 0
    | [|x; "regenerate"; fileName|] when x.EndsWith(".fsx") -> regenerate(LocalPath fileName); 0
    | _ -> printUsage(); 1
