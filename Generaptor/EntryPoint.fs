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

let private generateWorkflows(workflows: Workflow seq): Task =
    let dir = LocalPath ".github/workflows"
    let actionsClient = ActionsClient()
    task {
        for wf in workflows do
            Directory.CreateDirectory dir.Value |> ignore

            let id = wf.Id
            let path = dir / (id + ".yml")
            printfn $"Generating workflow {id}"
            let! existingVersions =
                if File.Exists path.Value
                then task {
                    let! content = File.ReadAllTextAsync path.Value
                    return Serializers.ExtractVersions content
                }
                else Task.FromResult Map.empty
            let content = Serializers.Stringify wf existingVersions actionsClient
            do! File.WriteAllTextAsync(path.Value, "# This file is auto-generated.\n" + content)
    }

let private regenerate(fileName: LocalPath) =
    let dir = LocalPath(Path.Combine(".github", "workflows"))
    let script = ScriptGenerator.GenerateFrom dir
    File.WriteAllText(fileName.Value, script)

let private runSynchronously(t: Task) =
    t.GetAwaiter().GetResult()

let Process(args: string seq) (workflows: Workflow seq): int =
    let args = Seq.toArray args
    match args with
    | [||] | [|"generate"|] -> runSynchronously <| generateWorkflows workflows; 0
    | [|x|] | [|x;"generate"|] when x.EndsWith(".fsx") -> runSynchronously <| generateWorkflows workflows; 0
    | [|"regenerate"; fileName|] -> regenerate(LocalPath fileName); 0
    | [|x; "regenerate"; fileName|] when x.EndsWith(".fsx") -> regenerate(LocalPath fileName); 0
    | _ -> printUsage(); 1
