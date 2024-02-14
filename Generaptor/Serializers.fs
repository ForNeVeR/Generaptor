module internal Generaptor.Serializers

open System.Collections.Generic

open YamlDotNet.Serialization

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
    | Some cron -> map.Add("schedule", Map.ofArray [| "cron", cron |])

    if triggers.WorkflowDispatch then
        map.Add("workflow_dispatch", null)

    map

let private addOptional (map: Dictionary<string, obj>) (key: string) value =
    match value with
    | Some v -> map.Add(key, v)
    | None -> ()

let private convertSteps steps =
    steps |> Seq.map (fun (step: Step) ->
        let map = Dictionary()
        addOptional map "id" step.Id
        addOptional map "name" step.Name
        addOptional map "uses" step.UsesAction
        addOptional map "shell" step.Shell
        addOptional map "run" step.Run
        if not <| Map.isEmpty step.Options then
            map.Add("with", step.Options)
        addOptional map "timeout-minutes" step.TimeoutMin
        map
    )

let private convertStrategy(strategy: Strategy) =
    let map = Dictionary<string, obj>()
    map.Add("matrix", strategy.Matrix)
    if not strategy.FailFast then
        map.Add("fail-fast", strategy.FailFast) // default is true anyway
    map

let private convertJobBody(job: Job) =
    let mutable map = Dictionary<string, obj>()
    match job.Strategy with
    | None -> ()
    | Some s -> map.Add("strategy", convertStrategy s)
    addOptional map "runs-on" job.RunsOn
    if job.Environment.Count > 0 then
        map.Add("env", job.Environment)
    if job.Steps.Length > 0 then
        map.Add("steps", convertSteps job.Steps)
    map

let private convertJobs(jobs: Job seq) =
    let map = Dictionary()
    for job in jobs do
        map.Add(job.Id, convertJobBody job)
    map

let private convertWorkflow(wf: Workflow) =
    let mutable map = Dictionary<string, obj>()
    addOptional map "name" wf.Name
    map.Add("on", convertTriggers wf.Triggers)
    map.Add("jobs", convertJobs wf.Jobs)
    map

let Stringify(wf: Workflow): string =
    let serializer =
        SerializerBuilder()
            .WithNewLine("\n")
            .Build()
    let data = convertWorkflow wf
    serializer.Serialize data
