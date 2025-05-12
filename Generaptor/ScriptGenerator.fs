module Generaptor.ScriptGenerator

open System
open System.Collections.Generic
open System.Collections
open System.IO
open System.Text
open TruePath
open YamlDotNet.Serialization

// Auto-updated by /Scripts/Update-Version.ps1
let PackageVersion = "1.2.0" // TODO: Just pick this from the assembly?

let private ParseYaml(file: LocalPath): Dictionary<string, obj> =
    let deserializer = DeserializerBuilder().Build()
    deserializer.Deserialize<_>(File.ReadAllText file.Value)

let private StringLiteral(x: obj) =
    let s = x :?> string
    let s = s.Replace("\"", "\\\"").Replace("\n", "\\n")
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

let private SerializeStrategyMatrix (m: IDictionary) =
    let rec serializeItem(item: obj) =
        let builder = StringBuilder()
        let append (x: string) = builder.Append x |> ignore
        match item with
        | :? string as s -> append <| StringLiteral s
        | :? IEnumerable as collection ->
            append "["
            for x in collection do
                append $"\n                    {serializeItem x}"
            append "\n                ]"
        | unknown -> failwithf $"Unknown element of strategy's matrix: \"{unknown}\"."
        builder.ToString()

    let builder = StringBuilder().Append("[")
    let append (x: string) = builder.Append x |> ignore
    for kvp in m do
        let kvp = kvp :?> DictionaryEntry
        append $"\n                \"{kvp.Key}\", {serializeItem kvp.Value}"
    if m.Count > 0 then append "\n"
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

let private SerializeEnv(data: obj, indent: unit -> string): string =
    let result = StringBuilder()
    let append(x: string) = result.AppendLine $"{indent()}{x}" |> ignore
    let data = data :?> Dictionary<obj, obj>
    for kvp in data do
        let key = kvp.Key :?> string
        let value = kvp.Value
        append $"setEnv {StringLiteral key} {StringLiteral value}"
    result.ToString()

let private Indent(spaces: int) () = String(' ', spaces)

let private SerializeOptions(map: obj, indent: unit -> string) =
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
        for kvp in step do
            let key = kvp.Key :?> string
            let value = kvp.Value
            match key with
            | "if" -> append $"    if = {StringLiteral value}"
            | "id" -> append $"    id = {StringLiteral value}"
            | "name" -> append $"    name = {StringLiteral value}"
            | "uses" -> append $"    uses = {StringLiteral value}"
            | "shell" -> append $"    shell = {StringLiteral value}"
            | "run" -> append $"    run = {StringLiteral value}"
            | "with" -> append $"    options = {SerializeOptions(value, Indent 16)}"
            | "env" -> append $"    env = {SerializeEnv(value, Indent 2)}"
            | "timeout-minutes" -> append $"    timeoutMin = {value}"
            | other -> failwithf $"Unknown key in the 'steps' section: \"{other}\"."
        append ")"
    builder.ToString()

let private SerializePermissions(value: obj, indent: unit -> string) =
    let permissions = value :?> Dictionary<obj, obj>
    let builder = StringBuilder()
    let append v = builder.AppendLine $"{indent()}{v}" |> ignore
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
        let append v = append $"    {v}"
        for kvp in content do
            let key = kvp.Key :?> string
            let value = kvp.Value
            match key with
            | "strategy" -> append <| $"strategy{SerializeStrategy value}"
            | "runs-on" -> append $"runsOn {StringLiteral value}"
            | "env" -> builder.Append(SerializeEnv(value, Indent 12)) |> ignore
            | "steps" -> builder.Append(SerializeSteps(value, Indent 12)) |> ignore
            | "permissions" -> builder.Append(SerializePermissions(value, Indent 12)) |> ignore
            | other -> failwithf $"Unknown key in the 'jobs' section: \"{other}\"."
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
EntryPoint.Process fsi.CommandLineArgs workflows""".ReplaceLineEndings "\n"
