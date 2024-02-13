module Generaptor.GitHubActions

open System
open System.Collections.Immutable

[<RequireQualifiedAccess>]
type Trigger =
    | OnPush of string[]
    | OnPullRequest of string[]
    | OnSchedule of string

type Job = {
    Id: string
    RunsOn: string option
    Strategy: Strategy option
    Environment: Map<string, string>
    Steps: ImmutableArray<Step>
}
and Strategy = {
    Matrix: Map<string, obj>
    FailFast: bool
}
and Step = {
    Name: string option
    UsesAction: string option
    Run: string option
    Options: Map<string, string>
    TimeoutMin: int option
}

type Workflow = {
    Id: string
    Name: string option
    Triggers: ImmutableArray<Trigger>
    Jobs: ImmutableArray<Job>
}

type JobCreationCommand =
    | RunsOn of string
    | AddStep of Step
    | SetEnv of string * string

type WorkflowCreationCommand =
    | SetName of string
    | AddTrigger of Trigger
    | AddJob of Job

let private addTrigger wf trigger =
    { wf with Triggers = wf.Triggers.Add(trigger) }
let private createJob id commands =
    let mutable job = { Id = id; Strategy = None; RunsOn = None; Environment = Map.empty; Steps = ImmutableArray.Empty }
    for command in commands do
        job <-
            match command with
            | RunsOn runsOn -> { job with RunsOn = Some runsOn }
            | AddStep step -> { job with Steps = job.Steps.Add(step) }
            | SetEnv (key, value) -> { job with Environment = Map.add key value job.Environment}
    job

let workflow (id: string) (commands: WorkflowCreationCommand seq): Workflow =
    let mutable wf = { Id = id; Name = None; Triggers = ImmutableArray.Empty; Jobs = ImmutableArray.Empty }
    for command in commands do
        wf <-
            match command with
            | SetName name -> { wf with Name = Some name }
            | AddTrigger trigger -> addTrigger wf trigger
            | AddJob job -> { wf with Jobs = wf.Jobs.Add job }
    wf

type Commands =
    static member name(name: string): WorkflowCreationCommand =
        SetName name

    static member onPushTo(branchName: string): WorkflowCreationCommand =
        AddTrigger(Trigger.OnPush [| branchName |])
    static member onPullRequestTo(branchName: string): WorkflowCreationCommand =
        AddTrigger(Trigger.OnPullRequest [| branchName |])
    static member onSchedule(cron: string): WorkflowCreationCommand =
        AddTrigger(Trigger.OnSchedule cron)
    static member onSchedule(day: DayOfWeek): WorkflowCreationCommand =
        AddTrigger(Trigger.OnSchedule $"0 0 * * {int day}")

    static member job (id: string) (commands: JobCreationCommand seq): WorkflowCreationCommand =
        AddJob(createJob id commands)

    static member runsOn(image: string): JobCreationCommand =
        RunsOn image
    static member setEnv (key: string) (value: string): JobCreationCommand =
        SetEnv(key, value)
    static member step(?name: string, ?uses: string, ?run: string, ?options: Map<string, string>, ?timeoutMin: int): JobCreationCommand =
        AddStep {
            Name = name
            UsesAction = uses
            Run = run
            Options = defaultArg options Map.empty
            TimeoutMin = timeoutMin
        }
