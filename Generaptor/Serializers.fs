module internal Generaptor.Serializers

open System.Collections.Generic

open YamlDotNet.Serialization

open Generaptor.GitHubActions

let private convertTriggers (triggers: Trigger seq): obj =
    let map = Dictionary()
    for trigger in triggers do
        match trigger with
        | Trigger.OnPush branches -> map.Add("push", Map.ofArray [| "branches", branches |])
    map

let private addOptional (map: Dictionary<string, obj>) (key: string) (value: string option) =
    match value with
    | Some v -> map.Add(key, v)
    | None -> ()

let private convertSteps steps =
    steps |> Seq.map (fun step ->
        let map = Dictionary()
        map.Add("uses", step.ActionName)
        map
    )

let private convertJobBody(job: Job) =
    let mutable map = Dictionary<string, obj>()
    addOptional map "runs-on" job.RunsOn
    if job.Steps.Length > 0 then
        map.Add("steps", convertSteps job.Steps)
    map

let private convertJobs (jobs: Job seq): obj =
    let map = Dictionary()
    for job in jobs do
        map.Add(job.Id, convertJobBody job)
    map

let private convertWorkflow (wf: Workflow) =
    let mutable map = Dictionary<string, obj>()
    map.Add("name", wf.Name)
    map.Add("on", convertTriggers wf.Triggers)
    map.Add("jobs", convertJobs wf.Jobs)
    map

let Stringify(wf: Workflow): string =
    let serializer =
        SerializerBuilder()
            .WithNewLine(@"\n")
            .Build()
    let data = convertWorkflow wf
    serializer.Serialize data
