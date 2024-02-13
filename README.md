🦖 Generaptor [![Status Zero][status-zero]][andivionian-status-classifier]
============

Generaptor helps you to maintain GitHub actions for your project. It can generate the YAML files and verify that they are correct, according to your specification.

Now you can manage your action definitions via NuGet packages, and port the whole workflows between repositories.
A bit of strong typing will also help to avoid mistakes!

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
    workflow("main") [
        name "Main"
        onPushTo mainBranch
        onPullRequestTo mainBranch
        onSchedule(day = DayOfWeek.Saturday)
        job "main" [
            checkout
            yield! dotNetBuildAndTest()
        ] |> addMatrix images
    ]
]
```

It will generate the following GitHub action configuration:
```yaml
# This file is auto-generated.
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

Documentation
-------------
- [License (MIT)][docs.license]

[andivionian-status-classifier]: https://andivionian.fornever.me/v1/#status-zero-
[docs.license]: ./LICENSE.md
[status-zero]: https://img.shields.io/badge/status-zero-lightgrey.svg
