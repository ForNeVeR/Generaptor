module Generaptor.EntryPoint

open Generaptor.GitHubActions

let private printUsage() =
    printfn "Possible arguments: <none>"

let Process(args: string seq) (workflows: Workflow seq): int =
    let args = Seq.toArray args
    match args with
    | _ -> printUsage(); 1
