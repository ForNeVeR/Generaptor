🦖 Generaptor [![Status Ventis][status-ventis]][andivionian-status-classifier]
============

Generaptor helps you to maintain GitHub actions for your project. It can generate the YAML files, according to the specification defined in your code.

Now you can manage your action definitions via NuGet packages, and port the whole workflows between repositories.
A bit of strong typing will also help to avoid mistakes!

NuGet package links:
- [![Generaptor][nuget.badge.generaptor]][nuget.generaptor]
- [![Generaptor.Library][nuget.badge.generaptor-library]][nuget.generaptor-library]


Showcase
--------
Consider this F# program (this is actually used in this very repository):
```fsharp
let mainBranch = "main"
let images = [
    "macos-12"
    "ubuntu-22.04"
    "windows-2022"
]

let workflows = [
    workflow "main" [
        name "Main"
        onPushTo mainBranch
        onPullRequestTo mainBranch
        onSchedule(day = DayOfWeek.Saturday)
        onWorkflowDispatch
        job "main" [
            checkout
            yield! dotNetBuildAndTest()
        ] |> addMatrix images
    ]
]

[<EntryPoint>]
let main(args: string[]): int =
    EntryPoint.Process args workflows
```

(See the actual example with all the imports in [the main program file][example.main].)

It will generate the following GitHub action configuration:
```yaml
name: Main
on:
  push:
    branches:
      - main
  pull_request:
    branches:
      - main
  schedule:
    - cron: 0 0 * * 6
  workflow_dispatch:
jobs:
  main:
    strategy:
      matrix:
        image:
          - macos-12
          - ubuntu-22.04
          - windows-2022
      fail-fast: false
    runs-on: ${{ matrix.image }}
    env:
      DOTNET_CLI_TELEMETRY_OPTOUT: 1
      DOTNET_NOLOGO: 1
      NUGET_PACKAGES: ${{ github.workspace }}/.github/nuget-packages
    steps:
      - uses: actions/checkout@v4
      - name: Set up .NET SDK
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 8.0.x
      - name: NuGet cache
        uses: actions/cache@v4
        with:
          key: ${{ runner.os }}.nuget.${{ hashFiles('**/*.fsproj') }}
          path: ${{ env.NUGET_PACKAGES }}
      - name: Build
        run: dotnet build
      - name: Test
        run: dotnet test
        timeout-minutes: 10
```

How to Use
----------
We recommend two main modes of execution for Generaptor: from a .NET project and from a script file.

### .NET Project
This integration is useful if you already have a solution file, and it's more convenient for you to have your infrastructure in a new project in that solution. Follow this instruction.

1. Create a new F# project in your solution. The location doesn't matter, but we recommend calling it `GitHubActions` and put inside the `Infrastructure` solution folder, to not mix it with the main code.
2. Install the `Generaptor.Library` NuGet package.
3. Call the `Generaptor.EntryPoint.Process` method with the arguments passed to the `main` function and the list of workflows you want to generate.
4. Run the program from the repository root folder in your shell, for example:
   ```console
   $ cd <your-repository-root-folder>
   $ dotnet run --project ./Infrastructure/GitHubActions
   ```

   When called with empty arguments of with command `generate`, it will (re-)generate the workflow files in `.github/workflows` folder, relatively to the current directory.

### Script File
As an alternative execution mode, we also support execution from an F# script file.

Put your code (see an example below) into an `.fsx` file (say, `github-actions.fsx`), and run it with the following shell command:

```console
$ dotnet fsi github-actions.fsx [optional parameters may go here]
```

The script file example:
```fsharp
#r "nuget: Generaptor.Library, 1.1.0"
open System

open Generaptor
open Generaptor.GitHubActions
open type Generaptor.GitHubActions.Commands
open type Generaptor.Library.Actions
open type Generaptor.Library.Patterns

let mainBranch = "main"
let images = [
    "macos-12"
    "ubuntu-22.04"
    "windows-2022"
]

let workflows = [
    workflow "main" [
        name "Main"
        onPushTo mainBranch
        onPullRequestTo mainBranch
        onSchedule(day = DayOfWeek.Saturday)
        onWorkflowDispatch
        job "main" [
            checkout
            yield! dotNetBuildAndTest()
        ] |> addMatrix images
    ]
]

EntryPoint.Process fsi.CommandLineArgs workflows
```

### Available Features
For basic GitHub Action support (workflow and step DSL), see [the `GitHubActions.fs` file][api.github-actions]. The basic actions are in the main **Generaptor** package.

For advanced patterns and action commands ready for use, see [Actions][api.library-actions] and [Patterns][api.library-patterns] files. These are in the auxiliary **Generaptor.Library** package.

Feel free to create your own actions and patterns, and either send a PR to this repository, or publish your own NuGet packages!

Documentation
-------------
- [Changelog][docs.changelog]
- [License (MIT)][docs.license]
- [Maintainer Guide][docs.maintainer-guide]
- [Code of Conduct (adapted from the Contributor Covenant)][docs.code-of-conduct]

[andivionian-status-classifier]: https://andivionian.fornever.me/v1/#status-ventis-
[api.github-actions]: ./Generaptor/GitHubActions.fs
[api.library-actions]: ./Generaptor.Library/Actions.fs
[api.library-patterns]: ./Generaptor.Library/Patterns.fs
[docs.changelog]: ./CHANGELOG.md
[docs.code-of-conduct]: ./CODE_OF_CONDUCT.md
[docs.license]: ./LICENSE.md
[docs.maintainer-guide]: ./MAINTAINERSHIP.md
[example.main]: ./Infrastructure/GitHubActions/Program.fs
[nuget.badge.generaptor-library]: https://img.shields.io/nuget/v/Generaptor.Library?label=Generaptor.Library
[nuget.badge.generaptor]: https://img.shields.io/nuget/v/Generaptor?label=Generaptor
[nuget.generaptor-library]: https://www.nuget.org/packages/Generaptor.Library
[nuget.generaptor]: https://www.nuget.org/packages/Generaptor
[status-ventis]: https://img.shields.io/badge/status-ventis-yellow.svg
