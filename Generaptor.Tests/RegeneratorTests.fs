module Generaptor.Tests.RegeneratorTests

open System.IO
open Generaptor
open TruePath
open Xunit

[<Fact>]
let ``Basic regenerator workflow``(): unit =
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
    |]
    let expectedScript = """#r "nuget: Generaptor.Library, <GENERAPTOR_VERSION>"
let workflows = [
    workflow "1" [
        name "Main"
        onPushTo "main"
        onPullRequestTo "main"
        onSchedule "0 0 * * 6"
        onWorkflowDispatch
        job "main" [
            checkout
            yield! dotNetBuildAndTest()
        ] |> addMatrix images
    ]
    workflow "release" [
        name "Release"
        yield! mainTriggers
        onPushTags "v*"
        job "nuget" [
            runsOn linuxImage
            checkout
            writeContentPermissions

            let configuration = "Release"

            let versionStepId = "version"
            let versionField = "${{ steps." + versionStepId + ".outputs.version }}"
            getVersionWithScript(stepId = versionStepId, scriptPath = "Scripts/Get-Version.ps1")
            dotNetPack(version = versionField)

            let releaseNotes = "./release-notes.md"
            prepareChangelog(releaseNotes)
            let artifacts projectName includeSNuPkg = [
                $"./{projectName}/bin/{configuration}/{projectName}.{versionField}.nupkg"
                if includeSNuPkg then $"./{projectName}/bin/{configuration}/{projectName}.{versionField}.snupkg"
            ]
            let allArtifacts = [
                yield! artifacts "Generaptor" true
                yield! artifacts "Generaptor.Library" true
            ]
            uploadArtifacts [
                releaseNotes
                yield! allArtifacts
            ]
            yield! ifCalledOnTagPush [
                createRelease(
                    name = $"Generaptor {versionField}",
                    releaseNotesPath = releaseNotes,
                    files = allArtifacts
                )
                yield! pushToNuGetOrg "NUGET_TOKEN" [
                    yield! artifacts "Generaptor" false
                    yield! artifacts "Generaptor.Library" false
                ]
            ]
        ]
    ]
]
]
EntryPoint.Process fsi.CommandLineArgs workflows
"""
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
                    $"nuget: Generaptor.Library, {ScriptGenerator.PackageVersion}",
                    "nuget: Generaptor.Library, <GENERAPTOR_VERSION>")
        Assert.Equal(expectedScript.ReplaceLineEndings "\n", actualScript.ReplaceLineEndings "\n")
    finally
        Directory.Delete(tempDir.Value, true)
