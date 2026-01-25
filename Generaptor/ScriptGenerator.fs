// SPDX-FileCopyrightText: 2024-2026 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module Generaptor.ScriptGenerator

open System
open System.Collections.Generic
open System.Collections
open System.IO
open System.Reflection
open System.Runtime.CompilerServices
open System.Text
open TruePath
open YamlDotNet.Serialization

[<MethodImpl(MethodImplOptions.NoInlining)>]
let PackageVersion(): string =
    let assembly = Assembly.GetExecutingAssembly()
    assembly.GetName().Version.ToString 3

let private ParseYaml(file: LocalPath): Dictionary<string, obj> =
    let deserializer = DeserializerBuilder().Build()
    deserializer.Deserialize<_>(File.ReadAllText file.Value)

let private StringLiteral(x: obj) =
    let s = x :?> string
    let s = s.Replace("\"", "\\\"").Replace("\n", "\\n")
    $"\"{s}\""

let private BoolLiteral(x: obj) =
    let v = Convert.ToBoolean x
    if v then "true" else "false"

let private Indent(spaces: int) () = String(' ', spaces)

let private AddIndent(existing: unit -> string, level: int) (): string =
    existing() + String(' ', level)

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

let private SerializeStrategyMatrix (m: IDictionary) =
    let rec serializeItem(item: obj, indent: unit -> string) =
        let builder = StringBuilder()
        let append(x: string) = builder.Append x |> ignore
        match item with
        | :? string as s -> append $"{indent()}{StringLiteral s}"
        | :? Dictionary<obj, obj> as map ->
            append $"{indent()}Map.ofList [\n"
            for kvp in map do
                append $"{indent()}    {StringLiteral kvp.Key}, {StringLiteral kvp.Value}\n"
            append $"{indent()}]"
        | :? IEnumerable as collection ->
            append "[\n"
            for x in collection do
                append $"{serializeItem(x, AddIndent(indent, 4))}\n"
            append $"{indent()}]"
        | unknown -> failwithf $"Unknown element of strategy's matrix: \"{unknown}\"."
        builder.ToString()

    let builder = StringBuilder().AppendLine("[")
    let append (x: string) = builder.Append x |> ignore
    for kvp in m do
        let kvp = kvp :?> DictionaryEntry
        append $"                \"{kvp.Key}\", {serializeItem(kvp.Value, Indent 16)}\n"
    builder.Append("            ]").ToString()

let private SerializeStrategy(data: obj): string =
    let map = data :?> Dictionary<obj, obj>
    let builder = StringBuilder().Append "("
    let mutable hasArguments = false
    let append k v =
        if hasArguments then builder.Append ", " |> ignore
        builder.Append $"{k} = {v}" |> ignore
        hasArguments <- true

    // fail-fast should go first
    match map.GetValueOrDefault "fail-fast" with
    | null -> ()
    | v -> append "failFast" v

    for kvp in map do
        let key = kvp.Key :?> string
        let value = kvp.Value
        match key with
        | "fail-fast" -> () // already processed
        | "matrix" -> append "matrix" (SerializeStrategyMatrix(value :?> IDictionary))
        | other -> failwithf $"Unknown key in the 'strategy' section: \"{other}\"."
    builder.Append(")").ToString()

let private SerializeEnvironment(data: obj): string =
    let map = data :?> Dictionary<obj, obj>
    let name = map["name"]
    let url = map["url"]
    $"            environment(name = {StringLiteral name}, url = {StringLiteral url})\n"

let private SerializeEnv(data: obj, indent: unit -> string): string =
    let result = StringBuilder()
    let append(x: string) = result.AppendLine $"{indent()}{x}" |> ignore
    let data = data :?> Dictionary<obj, obj>
    for kvp in data do
        let key = kvp.Key :?> string
        let value = kvp.Value
        append $"setEnv {StringLiteral key} {StringLiteral value}"
    result.ToString()

let private SerializeStringMap(map: obj, indent: unit -> string) =
    let map = map :?> Dictionary<obj, obj>
    let result = StringBuilder().AppendLine("Map.ofList [")
    for kvp in map do
        result.AppendLine $"{indent()}    {StringLiteral kvp.Key}, {StringLiteral kvp.Value}" |> ignore
    result.Append($"{indent()}]").ToString()

let private SerializeSteps(data: obj, indent: unit -> string): string =
    let builder = StringBuilder()
    let append(x: string) = builder.AppendLine $"{indent()}{x}" |> ignore
    let data = data :?> obj seq
    for step in data do
        let step = step :?> Dictionary<obj, obj>
        append "step("
        let mutable first = true
        for kvp in step do
            let key = kvp.Key :?> string
            let value = kvp.Value
            let appendArg k v =
                if first then
                    first <- false
                    builder.Append $"{indent()}    {k} = {v}"
                else
                    builder.Append $",\n{indent()}    {k} = {v}"
                |> ignore

            match key with
            | "if" -> appendArg "condition" <| StringLiteral value
            | "id" -> appendArg "id" <| StringLiteral value
            | "name" -> appendArg "name" <| StringLiteral value
            | "uses" -> appendArg "uses" <| StringLiteral value
            | "shell" -> appendArg "shell" <| StringLiteral value
            | "run" -> appendArg "run" <| StringLiteral value
            | "working-directory" -> appendArg "workingDirectory" <| StringLiteral value
            | "with" -> appendArg "options" <| SerializeStringMap(value, Indent 16)
            | "env" -> appendArg "env" <| SerializeStringMap(value, Indent 16)
            | "timeout-minutes" -> appendArg "timeoutMin" value
            | other -> failwithf $"Unknown key in the 'steps' section: \"{other}\"."
        builder.AppendLine $"\n{indent()})" |> ignore
    builder.ToString()

let private SerializePermissions(kind: string, value: obj, indent: unit -> string) =
    let permissions = value :?> Dictionary<obj, obj>
    let builder = StringBuilder()
    let append v = builder.AppendLine $"{indent()}{v}" |> ignore
    for kvp in permissions do
        let key = kvp.Key :?> string
        let value = kvp.Value :?> string

        let permission =
            match key with
            | "actions" -> "Actions"
            | "contents" -> "Contents"
            | "id-token" -> "IdToken"
            | "pages" -> "Pages"
            | "pull-requests" -> "PullRequests"
            | other -> failwithf $"Unknown key in the 'permissions' section: \"{other}\"."

        let access =
            match value with
            | "none" -> "None"
            | "read" -> "Read"
            | "write" -> "Write"
            | other -> failwithf $"Unknown value in the 'permissions' section: \"{other}\"."

        append $"{kind}Permission(PermissionKind.{permission}, AccessKind.{access})"
    builder.ToString()

let private SerializeConcurrency(data: obj) =
    let builder = StringBuilder()
    let append(x: string) = builder.AppendLine $"        {x}" |> ignore
    let map = data :?> Dictionary<obj, obj>
    append "workflowConcurrency("
    let mutable first = true
    for kvp in map do
        let key = kvp.Key :?> string
        let value = kvp.Value
        let appendArg k v =
            if first then
                first <- false
                builder.Append $"            {k} = {v}"
            else
                builder.Append $",\n            {k} = {v}"
            |> ignore

        match key with
        | "group" -> appendArg "group" <| StringLiteral value
        | "cancel-in-progress" -> appendArg "cancelInProgress" <| BoolLiteral value
        | other -> failwithf $"Unknown key in the 'concurrency' section: \"{other}\"."
    builder.AppendLine "\n        )" |> ignore
    builder.ToString()


let private SerializeJobs(jobs: obj): string =
    let builder = StringBuilder()
    let append v = builder.AppendLine $"        {v}" |> ignore

    let jobs = jobs :?> Dictionary<obj, obj>
    for kvp in jobs do
        let name = kvp.Key
        let content = kvp.Value :?> Dictionary<obj, obj>

        append $"job \"{name}\" ["
        let append v = append $"    {v}"
        for kvp in content do
            let key = kvp.Key :?> string
            let value = kvp.Value
            match key with
            | "name" -> append $"jobName {StringLiteral value}"
            | "strategy" -> append <| $"strategy{SerializeStrategy value}"
            | "runs-on" -> append $"runsOn {StringLiteral value}"
            | "environment" -> builder.Append(SerializeEnvironment value) |> ignore
            | "env" -> builder.Append(SerializeEnv(value, Indent 12)) |> ignore
            | "steps" -> builder.Append(SerializeSteps(value, Indent 12)) |> ignore
            | "permissions" -> builder.Append(SerializePermissions("job", value, Indent 12)) |> ignore
            | other -> failwithf $"Unknown key in the body of the job \"{name}\": \"{other}\"."
        builder.AppendLine "        ]" |> ignore

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
        | "permissions" -> appendSection <| SerializePermissions("workflow", value, Indent 8)
        | "concurrency" -> builder.Append(SerializeConcurrency value) |> ignore
        | other -> failwithf $"Unknown key at the root level of the workflow \"{name}\": \"{other}\"."
    builder.Append("    ]").ToString()

let GenerateFrom(workflowDirectory: LocalPath): string =
    let files = Directory.GetFiles(workflowDirectory.Value, "*.yml") |> Seq.map LocalPath
    let workflows =
        files
        |> Seq.sortBy _.Value
        |> Seq.map(fun path ->
            let name = path.GetFilenameWithoutExtension()
            let content = ParseYaml path
            SerializeWorkflow name content
        )
        |> String.concat "\n"
    $"""#r "nuget: Generaptor.Library, {PackageVersion()}"
open Generaptor
open Generaptor.GitHubActions
open type Generaptor.GitHubActions.Commands
let workflows = [
{workflows}
]
exit <| EntryPoint.Process fsi.CommandLineArgs workflows""".Trim().ReplaceLineEndings "\n"
