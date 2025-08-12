// SPDX-FileCopyrightText: 2024-2025 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module internal Generaptor.Serializers

open System.Collections
open System.Collections.Generic

open System.IO
open System.Threading.Tasks
open TruePath
open YamlDotNet.Core
open YamlDotNet.Serialization
open YamlDotNet.Serialization.EventEmitters

open Generaptor.GitHubActions

let private convertTriggers(triggers: Triggers) =
    let map = Dictionary<string, obj>()

    let push = triggers.Push
    if push.Branches.Length > 0 || push.Tags.Length > 0 then
        map.Add("push", Map.ofArray [|
            if push.Branches.Length > 0 then
                "branches", push.Branches
            if push.Tags.Length > 0 then
                "tags", push.Tags
        |])
    if triggers.PullRequest.Branches.Length > 0 then
        map.Add("pull_request", Map.ofArray [| "branches", triggers.PullRequest.Branches |])
    match triggers.Schedule with
    | None -> ()
    | Some cron -> map.Add("schedule", [| Map.ofArray [| "cron", cron |] |])

    if triggers.WorkflowDispatch then
        map.Add("workflow_dispatch", null)

    map

let private addOptional (map: Dictionary<string, obj>) (key: string) value =
    match value with
    | Some v -> map.Add(key, v)
    | None -> ()

let private getActionUsesSpec (existingVersions: Map<string, ActionVersion>, client: IActionsClient) = function
    | ActionWithVersion av -> av
    | Auto name ->
        (task {
            let! version =
                match Map.tryFind name existingVersions with
                | Some version -> Task.FromResult version
                | None -> client.GetLastActionVersion name

            let (ActionVersion versionString) = version
            return $"{name}@{versionString}"
        }).GetAwaiter().GetResult()

let private convertSteps(steps, existingVersions, client) =
    steps |> Seq.map (fun (step: Step) ->
        let map = Dictionary()
        addOptional map "if" step.Condition
        addOptional map "id" step.Id
        addOptional map "name" step.Name

        let uses = step.Uses |> Option.map(getActionUsesSpec(existingVersions, client))
        addOptional map "uses" uses

        addOptional map "shell" step.Shell
        addOptional map "run" step.Run
        if not <| Map.isEmpty step.Options then
            map.Add("with", step.Options)
        if not <| Map.isEmpty step.Environment then
            map.Add("env", step.Environment)
        addOptional map "timeout-minutes" step.TimeoutMin
        map
    )

let private convertStrategy(strategy: Strategy) =
    let map = Dictionary<string, obj>()
    map.Add("matrix", strategy.Matrix)
    match strategy.FailFast with
    | None -> ()
    | Some v -> map.Add("fail-fast", v)
    map

let private convertPermissions permissions =
    permissions |> Map.toSeq |> Seq.map(fun(k, v) ->
        let mapPermission =
            match k with
            | PermissionKind.Actions -> "actions"
            | PermissionKind.Contents -> "contents"
            | PermissionKind.IdToken -> "id-token"
            | PermissionKind.Pages -> "pages"
        let mapAccess =
            match v with
            | AccessKind.None -> "none"
            | AccessKind.Read -> "read"
            | AccessKind.Write -> "write"
        mapPermission, mapAccess
    ) |> Map.ofSeq

let private convertJobBody(job: Job, existingVersions, client) =
    let mutable map = Dictionary<string, obj>()
    match job.Name with
    | None -> ()
    | Some n -> map.Add("name", n)
    match job.Strategy with
    | None -> ()
    | Some s -> map.Add("strategy", convertStrategy s)
    if not job.Permissions.IsEmpty then
        map.Add("permissions", convertPermissions job.Permissions)
    if not job.Needs.IsEmpty then
        map.Add("needs", job.Needs)
    addOptional map "runs-on" job.RunsOn
    if job.Environment.Count > 0 then
        map.Add("env", job.Environment)
    if job.Steps.Length > 0 then
        map.Add("steps", convertSteps(job.Steps, existingVersions, client))
    map

let private convertConcurrency concurrency =
    let map = Dictionary<string, obj>()
    map.Add("group", concurrency.Group)
    map.Add("cancel-in-progress", concurrency.CancelInProgress)
    map

let private convertJobs(jobs: Job seq, existingVersions, client) =
    let map = Dictionary()
    for job in jobs do
        map.Add(job.Id, convertJobBody(job, existingVersions, client))
    map

let private convertWorkflow(wf: Workflow, existingVersions, client) =
    let mutable map = Dictionary<string, obj>()
    addOptional map "name" wf.Name
    map.Add("on", convertTriggers wf.Triggers)
    addOptional map "concurrency" (wf.Concurrency |> Option.map convertConcurrency)
    if not <| Map.isEmpty wf.Permissions then map.Add("permissions", convertPermissions wf.Permissions)
    map.Add("jobs", convertJobs(wf.Jobs, existingVersions, client))
    map

let ExtractVersions(content: string): Map<string, ActionVersion> =
    let document =
        let deserializer = DeserializerBuilder().Build()
        deserializer.Deserialize<Dictionary<string, obj>> content
    let getValue (m: obj) k =
        match m with
        | :? IDictionary as m when m.Contains k -> Some m[k]
        | _ -> None
    let getSubdictionary m k =
        match getValue m k with
        | Some(:? IDictionary as s) -> Some s
        | _ -> None
    let jobs =
        getSubdictionary document "jobs"
        |> Option.map(fun x -> x.Values |> Seq.cast<obj>)
        |> Option.defaultValue Seq.empty
    let allSteps = jobs |> Seq.collect (fun j ->
        getValue j "steps"
        |> Option.bind(function | :? seq<obj> as s -> Some s | _ -> None)
        |> Option.defaultValue Seq.empty
    )
    let allUsesClauses =
        allSteps
        |> Seq.choose(fun s ->
            match s with
            | :? IDictionary as d when d.Contains "uses" ->
                match d["uses"] with
                | :? string as u -> Some u
                | _ -> None
            | _ -> None
        )
    allUsesClauses
    |> Seq.choose(fun v -> match v.Split('@', 2) with | [| n; v |] -> Some(n, v) | _ -> None)
    |> Seq.groupBy fst
    |> Seq.map(fun(k, xs) -> k, Seq.map snd xs)
    |> Seq.map(fun (name, allVersions) ->
        let distinctVersions = Seq.distinct allVersions |> Array.ofSeq
        let version =
            match distinctVersions.Length with
            | 1 -> Array.exactlyOne distinctVersions
            | _ -> distinctVersions
                   |> Seq.choose(fun v -> NumericVersion.TryParse v |> Option.map (fun n -> v, n))
                   |> Seq.sortByDescending snd
                   |> Seq.map fst
                   |> Seq.tryHead
                   |> Option.defaultWith(
                       fun() -> failwithf $"Cannot determine any parseable version for action {name}."
                   )
        name, ActionVersion version
    )
    |> Map.ofSeq

let internal Stringify(wf: Workflow) (existingVersions: Map<string, ActionVersion>) (client: IActionsClient): string =
    let serializer =
        SerializerBuilder()
            .WithNewLine("\n")
            .WithEventEmitter(fun nextEmitter ->
                { new ChainedEventEmitter(nextEmitter) with
                    override this.Emit(eventInfo: ScalarEventInfo, emitter: IEmitter): unit =
                        if eventInfo.Source.Type = typeof<string>
                           && (eventInfo.Source.Value :?> string).Contains "\n" then
                            eventInfo.Style <- ScalarStyle.Literal
                        nextEmitter.Emit(eventInfo, emitter)
                })
            .Build()
    let data = convertWorkflow(wf, existingVersions, client)
    serializer.Serialize data

let GenerateWorkflowContent(yaml: LocalPath, wf: Workflow, client: IActionsClient): Task<string> = task {
    printfn $"Generating workflow {wf.Id}…"
    let! existingVersions =
        if File.Exists yaml.Value
        then task {
            let! content = File.ReadAllTextAsync yaml.Value
            return ExtractVersions content
        }
        else Task.FromResult Map.empty

    let mutable header = defaultArg wf.Header "# This file is auto-generated.\n"
    if not(header.EndsWith "\n") then header <- $"{header}\n"

    return header + Stringify wf existingVersions client
}
