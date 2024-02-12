open Generaptor
open Generaptor.GitHubActions

let workflows = [
    workflow("main") []
]

let main(args: string[]) : int =
    EntryPoint.Process args workflows
