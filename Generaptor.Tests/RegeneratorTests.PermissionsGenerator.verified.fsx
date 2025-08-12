#r "nuget: Generaptor.Library, <GENERAPTOR_VERSION>"
open Generaptor
open Generaptor.GitHubActions
open type Generaptor.GitHubActions.Commands
let workflows = [
    workflow "1" [
        workflowPermission(PermissionKind.Actions, AccessKind.Read)
        workflowPermission(PermissionKind.Pages, AccessKind.Write)
        workflowPermission(PermissionKind.IdToken, AccessKind.Write)
        workflowConcurrency(
            group = "pages",
            cancelInProgress = false
        )
    ]
]
EntryPoint.Process fsi.CommandLineArgs workflows
