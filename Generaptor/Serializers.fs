module internal Generaptor.Serializers

open System.Collections.Generic

open System.Threading.Tasks
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
            return versionString
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
    Map.ofArray [|
        if Set.contains ContentWrite permissions then
            "contents", "write"
    |]

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

let private convertJobs(jobs: Job seq, existingVersions, client) =
    let map = Dictionary()
    for job in jobs do
        map.Add(job.Id, convertJobBody(job, existingVersions, client))
    map

let private convertWorkflow(wf: Workflow, existingVersions, client) =
    let mutable map = Dictionary<string, obj>()
    addOptional map "name" wf.Name
    map.Add("on", convertTriggers wf.Triggers)
    map.Add("jobs", convertJobs(wf.Jobs, existingVersions, client))
    map

let ExtractVersions(content: string): Map<string, ActionVersion> = failwithf "TODO"

let Stringify(wf: Workflow) (existingVersions: Map<string, ActionVersion>) (client: IActionsClient): string =
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
