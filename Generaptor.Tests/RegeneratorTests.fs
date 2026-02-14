// SPDX-FileCopyrightText: 2024-2026 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module Generaptor.Tests.RegeneratorTests

open System
open System.IO
open System.Threading.Tasks
open Generaptor
open TruePath
open VerifyXunit
open Xunit

let private InitializeVerify() =
    Environment.SetEnvironmentVariable("DiffEngine_Disabled", "true")
    Environment.SetEnvironmentVariable("Verify_DisableClipboard", "true")

let private DoTest(files: (string * string) seq): Task =
    let tempDir =
        let path = Path.GetTempFileName()
        File.Delete path
        Directory.CreateDirectory path |> ignore
        AbsolutePath path
    try
        for fileName, fileContent in files do
            let filePath = tempDir / fileName
            File.WriteAllText(filePath.Value, fileContent)
        let actualScript =
            ScriptGenerator.GenerateFrom(LocalPath tempDir)
                .Replace(
                    $"nuget: Generaptor.Library, {ScriptGenerator.PackageVersion()}",
                    "nuget: Generaptor.Library, <GENERAPTOR_VERSION>")

        InitializeVerify()
        Verifier.Verify(actualScript, extension = "fsx").ToTask()
    finally
        Directory.Delete(tempDir.Value, true)

[<Fact>]
let BasicRegeneratorWorkflow(): Task =
    let files = [|
        "1.yml", """
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
        - macos-latest
        - ubuntu-latest
        - windows-latest
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
    - name: Test
      run: dotnet test
      timeout-minutes: 10
"""
        "2.yml", """
name: Release
on:
  push:
    branches:
    - main
    tags:
    - v*
  pull_request:
    branches:
    - main
  schedule:
  - cron: 0 0 * * 6
  workflow_dispatch:
jobs:
  nuget:
    permissions:
      contents: write
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v4
    - id: version
      name: Get version
      shell: pwsh
      run: echo "version=$(Scripts/Get-Version.ps1 -RefName $env:GITHUB_REF)" >> $env:GITHUB_OUTPUT
    - run: dotnet pack --configuration Release -p:Version=${{ steps.version.outputs.version }}
    - name: Read changelog
      uses: ForNeVeR/ChangelogAutomation.action@v1
      with:
        output: ./release-notes.md
    - name: Upload artifacts
      uses: actions/upload-artifact@v4
      with:
        path: |-
          ./release-notes.md
          ./Generaptor/bin/Release/Generaptor.${{ steps.version.outputs.version }}.nupkg
          ./Generaptor/bin/Release/Generaptor.${{ steps.version.outputs.version }}.snupkg
          ./Generaptor.Library/bin/Release/Generaptor.Library.${{ steps.version.outputs.version }}.nupkg
          ./Generaptor.Library/bin/Release/Generaptor.Library.${{ steps.version.outputs.version }}.snupkg
"""
        "docs.yml", """name: Docs
on:
  push:
    branches:
      - main
  workflow_dispatch:

permissions:
  actions: read
  pages: write
  id-token: write

concurrency:
  group: "pages"
  cancel-in-progress: false

jobs:
  publish-docs:
    environment:
      name: github-pages
      url: ${{ steps.deployment.outputs.page_url }}
    runs-on: ubuntu-22.04
    steps:
      - name: Checkout
        uses: actions/checkout@v4
      - name: Dotnet Setup
        uses: actions/setup-dotnet@v4

      - run: dotnet tool restore
      - run: dotnet docfx docs/docfx.json

      - name: Upload artifact
        uses: actions/upload-pages-artifact@v3
        with:
          path: 'docs/_site'
      - name: Deploy to GitHub Pages
        id: deployment
        uses: actions/deploy-pages@v4
"""
    |]
    DoTest files

[<Fact>]
let StrategyGenerator(): Task =
    let files = [
        "1.yml", """
jobs:
  main:
    strategy:
      fail-fast: false
      matrix:
        config:
          - name: 'macos'
            image: 'macos-14'
          - name: 'linux'
            image: 'ubuntu-24.04'
          - name: 'windows'
            image: 'windows-2022'

    name: main.${{ matrix.config.name }}
"""
    ]
    DoTest files

[<Fact>]
let StepEnvGenerator(): Task =
    let files = [
        "1.yml", """
jobs:
  main:
    steps:
      - name: Create release
        if: startsWith(github.ref, 'refs/tags/v')
        id: release
        uses: actions/create-release@v1
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
        with:
          tag_name: ${{ github.ref }}
          release_name: ChangelogAutomation v${{ steps.version.outputs.version }}
          body_path: ./release-data.md
"""
    ]
    DoTest files

[<Fact>]
let PermissionsGenerator(): Task =
    let files = [
        "1.yml", """
permissions:
  actions: read
  pages: write
  id-token: write

concurrency:
  group: "pages"
  cancel-in-progress: false
"""
    ]
    DoTest files

[<Fact>]
let WorkingDirectoryGenerator(): Task =
    let files = [
        "1.yml", """
jobs:
  test:
    steps:
      - name: Run unit tests
        run: dotnet test
        working-directory: Fabricator.Tests
      - name: Run integration tests
        run: dotnet test
        working-directory: Fabricator.IntegrationTests
"""
    ]
    DoTest files

[<Fact>]
let JobTimeoutGenerator(): Task =
    let files = [
        "1.yml", """
jobs:
  main:
    permissions:
      checks: write
      contents: write
      pull-requests: write

    runs-on: ubuntu-24.04
    timeout-minutes: 15
"""
    ]
    DoTest files

[<Fact>]
let NeedsGenerator(): Task =
    let files = [
        "1.yml", """
jobs:
  build:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v4
  test:
    needs: [ build ]
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v4
"""
    ]
    DoTest files

[<Fact>]
let PullRequestWithoutBranches(): Task =
    let files = [
        "1.yml", """
on:
  pull_request:
jobs:
  main:
    runs-on: ubuntu-latest
"""
    ]
    DoTest files

[<Fact>]
let PushWithoutBranches(): Task =
    let files = [
        "1.yml", """
on:
  push:
jobs:
  main:
    runs-on: ubuntu-latest
"""
    ]
    DoTest files
