#r "nuget: Generaptor.Library, <GENERAPTOR_VERSION>"
open Generaptor
open Generaptor.GitHubActions
open type Generaptor.GitHubActions.Commands
let workflows = [
    workflow "1" [
        job "main" [
            jobPermission(PermissionKind.Checks, AccessKind.Write)
            jobPermission(PermissionKind.Contents, AccessKind.Write)
            jobPermission(PermissionKind.PullRequests, AccessKind.Write)
            runsOn "ubuntu-24.04"
            jobTimeout 15
        ]
    ]
]
exit <| EntryPoint.Process fsi.CommandLineArgs workflows
