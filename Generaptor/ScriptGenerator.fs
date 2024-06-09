﻿module Generaptor.ScriptGenerator

open System.Collections.Generic
open System.IO
open System.Text
open TruePath
open YamlDotNet.Serialization

// Auto-updated by /Scripts/Update-Version.ps1
let PackageVersion = "1.2.0"

let private ParseYaml(file: LocalPath): Dictionary<string, obj> =
    let deserializer = DeserializerBuilder().Build()
    deserializer.Deserialize<_>(File.ReadAllText file.Value)

let private StringLiteral(x: obj) =
    let s = x :?> string
    $"\"{s}\""

let private SerializeOn(data: Dictionary<obj, obj>): string =
    let builder = StringBuilder()
    let append v = builder.AppendLine $"        {v}" |> ignore
    for kvp in data do
        let key = kvp.Key :?> string
        let value = kvp.Value
        match key with
        | "push" ->
            let children = value :?> Dictionary<obj, obj>
            children.GetValueOrDefault "branches" |> Option.ofObj |> Option.iter(fun branches ->
                branches :?> obj seq
                |> Seq.iter(fun branch ->
                    append $"onPushTo {StringLiteral branch}"
                )
            )
            children.GetValueOrDefault "tags" |> Option.ofObj |> Option.iter(fun tags ->
                tags :?> _ seq
                |> Seq.iter(fun tag ->
                    append $"onPushTags {StringLiteral tag}"
                )
            )
        | "pull_request" ->
            let children = value :?> Dictionary<obj, obj>
            children.GetValueOrDefault "branches" |> Option.ofObj |> Option.iter(fun branches ->
                branches :?> _ seq
                |> Seq.iter(fun branch ->
                    append $"onPullRequestTo {StringLiteral branch}"
                )
            )
        | "schedule" ->
            value :?> obj seq |> Seq.iter(fun entry ->
                let cron = (entry :?> Dictionary<obj, obj>)["cron"]
                append $"onSchedule {StringLiteral(cron :?> string)}"
            )
        | "workflow_dispatch" -> append "onWorkflowDispatch"
        | other -> failwithf $"Unknown key in the 'on' section: \"{other}\"."
    builder.ToString()

let private SerializeStrategy(data: obj): string =
    let map = data :?> Dictionary<obj, obj>
    let builder = StringBuilder().Append "strategy ["
    let append v = builder.Append $"    {v}" |> ignore
    for kvp in map do
        let key = kvp.Key :?> string
        let value = kvp.Value
        match key with
        | "matrix" -> append $"matrix {value}"
        | "fail-fast" -> append $"fail-fast {value}"
        | other -> failwithf $"Unknown key in the 'strategy' section: \"{other}\"."
    builder.Append "    ]" |> ignore
    builder.ToString()

let private SerializeEnv(data: obj): string =
    let builder = StringBuilder().Append "env ["
    let append v = builder.Append $"    {v}" |> ignore
    let data = data :?> Dictionary<obj, obj>
    for kvp in data do
        let key = kvp.Key :?> string
        let value = kvp.Value
        append $"""    {key}: {StringLiteral value}"""
    builder.Append "    ]" |> ignore
    builder.ToString()

let private SerializeSteps(data: obj): string =
    let builder = StringBuilder().Append "steps ["
    let append v = builder.Append $"    {v}" |> ignore
    let data = data :?> obj seq
    for step in data do
        let step = step :?> Dictionary<obj, obj>
        append "step ["
        for kvp in step do
            let key = kvp.Key :?> string
            let value = kvp.Value
            match key with
            | "if" -> append $"if {StringLiteral value}"
            | "id" -> append $"id {StringLiteral value}"
            | "name" -> append $"name {StringLiteral value}"
            | "uses" -> append $"uses {StringLiteral value}"
            | "shell" -> append $"shell {StringLiteral value}"
            | "run" -> append $"run {StringLiteral value}"
            | "with" -> append $"with {SerializeEnv value}"
            | "env" -> append $"env {SerializeEnv value}"
            | "timeout-minutes" -> append $"timeout-minutes {value}"
            | other -> failwithf $"Unknown key in the 'steps' section: \"{other}\"."
        builder.Append "    ]" |> ignore
    builder.Append "    ]" |> ignore
    builder.ToString()

let private SerializePermissions(value: obj) =
    let permissions = value :?> Dictionary<obj, obj>
    let builder = StringBuilder()
    let append v = builder.Append $"    {v}" |> ignore
    for kvp in permissions do
        let key = kvp.Key :?> string
        let value = kvp.Value :?> string
        match key with
        | "contents" ->
            match value with
            | "write" -> append "writeContentPermissions"
            | other -> failwithf $"Unknown value in the 'permissions' section: \"{other}\"."
        | other -> failwithf $"Unknown key in the 'permissions' section: \"{other}\"."

    builder.ToString()

let private SerializeJobs(jobs: obj): string =
    let builder = StringBuilder()
    let append v = builder.AppendLine $"        {v}" |> ignore

    let jobs = jobs :?> Dictionary<obj, obj>
    for kvp in jobs do
        let name = kvp.Key
        let content = kvp.Value :?> Dictionary<obj, obj>

        append $"job \"{name}\" ["
        let append v = builder.Append $"    {v}" |> ignore
        for kvp in content do
            let key = kvp.Key :?> string
            let value = kvp.Value
            match key with
            | "strategy" -> append <| $"strategy {SerializeStrategy value}"
            | "runs-on" -> append $"runs-on {StringLiteral value}"
            | "env" -> append <| SerializeEnv value
            | "steps" -> append <| SerializeSteps value
            | "permissions" -> append <| SerializePermissions value
            | other -> failwithf $"Unknown key in the 'jobs' section: \"{other}\"."
        builder.Append "    ]" |> ignore

    builder.ToString()

let private SerializeWorkflow (name: string) (content: Dictionary<string, obj>): string =
    let builder = StringBuilder().AppendLine $"    workflow \"{name}\" ["
    let append v = builder.AppendLine $"        {v}" |> ignore
    let appendSection(v: string) = builder.Append v |> ignore
    for kvp in content do
        let key = kvp.Key
        let value = kvp.Value
        match key with
        | "name" -> append $"name {StringLiteral(value :?> string)}"
        | "on" -> appendSection <| SerializeOn(value :?> Dictionary<obj, obj>)
        | "jobs" -> appendSection <| SerializeJobs value
        | other -> failwithf $"Unknown key at the root level of the workflow \"{name}\": \"{other}\"."
    builder.Append("    ]").ToString()

let GenerateFrom(workflowDirectory: LocalPath): string =
    let files = Directory.GetFiles(workflowDirectory.Value, "*.yml") |> Seq.map LocalPath
    let workflows =
        files
        |> Seq.map(fun path ->
            let name = path.GetFilenameWithoutExtension()
            let content = ParseYaml path
            SerializeWorkflow name content
        )
        |> String.concat "\n"
    $"""#r "nuget: Generaptor.Library, {PackageVersion}"
let workflows = [
{workflows}
]
EntryPoint.Process fsi.CommandLineArgs workflows
    """.ReplaceLineEndings "\n"
