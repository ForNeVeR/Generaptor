#r "nuget: Generaptor.Library, <GENERAPTOR_VERSION>"
open Generaptor
open Generaptor.GitHubActions
open type Generaptor.GitHubActions.Commands
let workflows = [
    workflow "1" [
        job "main" [
            step(
                name = "Create release",
                condition = "startsWith(github.ref, 'refs/tags/v')",
                id = "release",
                uses = "actions/create-release@v1",
                env = Map.ofList [
                    "GITHUB_TOKEN", "${{ secrets.GITHUB_TOKEN }}"
                ],
                options = Map.ofList [
                    "tag_name", "${{ github.ref }}"
                    "release_name", "ChangelogAutomation v${{ steps.version.outputs.version }}"
                    "body_path", "./release-data.md"
                ]
            )
        ]
    ]
]
EntryPoint.Process fsi.CommandLineArgs workflows