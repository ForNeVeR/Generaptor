module internal Generaptor.Serializers

open System.Collections.Generic

open YamlDotNet.Serialization

open Generaptor.GitHubActions

let private convertTriggers (triggers: Trigger seq) =
    let map = Dictionary<string, obj>()
    for trigger in triggers do
        match trigger with
        | Trigger.OnPush branches -> map.Add("push", Map.ofArray [| "branches", branches |])
        | Trigger.OnPullRequest branches -> map.Add("pull_request", Map.ofArray [| "branches", branches |])
        | Trigger.OnSchedule cron -> map.Add("schedule", [ Map.ofArray [| "cron", cron |] ])
    map

let private addOptional (map: Dictionary<string, obj>) (key: string) value =
    match value with
    | Some v -> map.Add(key, v)
    | None -> ()

let private convertSteps steps =
    steps |> Seq.map (fun (step: Step) ->
        let map = Dictionary()
        addOptional map "name" step.Name
        addOptional map "uses" step.UsesAction
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
