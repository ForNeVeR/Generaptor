module Generaptor.GitHubActions

open System
open System.Collections.Immutable

type Triggers = {
    Push: PushTrigger
    PullRequest: PullRequestTrigger
    Schedule: string option
    WorkflowDispatch: bool
}
and PushTrigger = {
    Branches: ImmutableArray<string>
    Tags: ImmutableArray<string>
}
and PullRequestTrigger = {
    Branches: ImmutableArray<string>
}

type TriggerCreationCommand =
    | OnPushBranches of string seq
    | OnPushTags of string seq
    | OnPullRequestToBranches of string seq
    | OnSchedule of string
    | OnWorkflowDispatch

type Permission = ContentWrite

type ActionSpec =
    | ActionWithVersion of nameWithVersion: string
    /// This will read the highest major action version from the YAML file if it exists. If not found, will fall back
    /// to the latest available action tag on GitHub.
    | Auto of name: string

type Job = {
    Id: string
    Name: string option
    Permissions: Set<Permission>
    Needs: ImmutableArray<string>
    Strategy: Strategy option
    RunsOn: string option
    Environment: Map<string, string>
    Steps: ImmutableArray<Step>
}
and Strategy = {
    Matrix: Map<string, obj>
    FailFast: bool option
}
and Step = {
    Condition: string option
    Id: string option
    Name: string option
    Uses: ActionSpec option
    Shell: string option
    Run: string option
    Options: Map<string, string>
    Environment: Map<string, string>
    TimeoutMin: int option
}

type Workflow = {
    Id: string
    Header: string option
    Name: string option
    Triggers: Triggers
    Jobs: ImmutableArray<Job>
}

type JobCreationCommand =
    | Name of string
    | AddPermissions of Permission
    | Needs of string
    | RunsOn of string
    | AddStep of Step
    | SetEnv of string * string
    | AddStrategy of Strategy

type WorkflowCreationCommand =
    | SetHeader of string
    | SetName of string
    | AddTrigger of TriggerCreationCommand
    | AddJob of Job

let private addTrigger wf = function
    | OnPushBranches branches -> { wf with Workflow.Triggers.Push.Branches = wf.Triggers.Push.Branches.AddRange(branches) }
    | OnPushTags tags -> { wf with Workflow.Triggers.Push.Tags = wf.Triggers.Push.Tags.AddRange(tags) }
    | OnPullRequestToBranches branches -> { wf with Workflow.Triggers.PullRequest.Branches = wf.Triggers.PullRequest.Branches.AddRange(branches) }
    | OnSchedule cron -> { wf with Workflow.Triggers.Schedule = Some cron }
    | OnWorkflowDispatch -> { wf with Workflow.Triggers.WorkflowDispatch = true }

let private createJob id commands =
    let mutable job = {
        Id = id
        Name = None
        Strategy = None
        Permissions = Set.empty
        Needs = ImmutableArray.Empty
        RunsOn = None
        Environment = Map.empty
        Steps = ImmutableArray.Empty
    }
    for command in commands do
        job <-
            match command with
            | Name n -> { job with Name = Some n }
            | AddPermissions p -> { job with Permissions = Set.add p job.Permissions }
            | Needs needs -> { job with Needs = job.Needs.Add needs }
            | RunsOn runsOn -> { job with RunsOn = Some runsOn }
            | AddStep step -> { job with Steps = job.Steps.Add(step) }
            | SetEnv (key, value) -> { job with Environment = Map.add key value job.Environment}
            | AddStrategy s -> { job with Strategy = Some s }
    job

let workflow (id: string) (commands: WorkflowCreationCommand seq): Workflow =
    let mutable wf = {
        Id = id
        Header = None
        Name = None
        Triggers = {
            Push = {
                Branches = ImmutableArray.Empty
                Tags = ImmutableArray.Empty
            }
            PullRequest = {
                Branches = ImmutableArray.Empty
            }
            Schedule = None
            WorkflowDispatch = false
        }
        Jobs = ImmutableArray.Empty
    }
    for command in commands do
        wf <-
            match command with
            | SetHeader header -> { wf with Header = Some header }
            | SetName name -> { wf with Name = Some name }
            | AddTrigger trigger -> addTrigger wf trigger
            | AddJob job -> { wf with Jobs = wf.Jobs.Add job }
    wf

type Commands =
    static member name(name: string): WorkflowCreationCommand =
        SetName name
    static member header(headerText: string): WorkflowCreationCommand =
        SetHeader headerText

    static member onPushTo(branchName: string): WorkflowCreationCommand =
        AddTrigger(OnPushBranches [| branchName |])
    static member onPushTags(tagName: string): WorkflowCreationCommand=
        AddTrigger(OnPushTags [| tagName |])
    static member onPullRequestTo(branchName: string): WorkflowCreationCommand =
        AddTrigger(OnPullRequestToBranches [| branchName |])
    static member onSchedule(cron: string): WorkflowCreationCommand =
        AddTrigger(OnSchedule cron)
    static member onSchedule(day: DayOfWeek): WorkflowCreationCommand =
        AddTrigger(OnSchedule $"0 0 * * {int day}")
    static member onWorkflowDispatch: WorkflowCreationCommand =
        AddTrigger OnWorkflowDispatch

    static member job (id: string) (commands: JobCreationCommand seq): WorkflowCreationCommand =
        AddJob(createJob id commands)

    static member jobName(name: string): JobCreationCommand =
        Name name
    static member writeContentPermissions: JobCreationCommand =
        AddPermissions(ContentWrite)
    static member needs(jobId: string): JobCreationCommand =
        Needs jobId
    static member runsOn(image: string): JobCreationCommand =
        RunsOn image
    static member setEnv (key: string) (value: string): JobCreationCommand =
        SetEnv(key, value)
    static member step(?id: string,
                       ?condition: string,
                       ?name: string,
                       ?uses: string,
                       ?usesSpec: ActionSpec,
                       ?shell: string,
                       ?run: string,
                       ?options: Map<string, string>,
                       ?env: Map<string, string>,
                       ?timeoutMin: int): JobCreationCommand =
        let actionSpec =
            match uses, usesSpec with
            | Some nameWithVersion, None -> Some <| ActionWithVersion nameWithVersion
            | None, Some spec -> Some spec
            | None, None -> None
            | Some nameWithVersion, Some spec ->
                failwithf $"Invalid action spec: both {nameWithVersion} and {spec} are specified."
        AddStep {
            Condition = condition
            Id = id
            Name = name
            Uses = actionSpec
            Shell = shell
            Run = run
            Options = defaultArg options Map.empty
            Environment = defaultArg env Map.empty
            TimeoutMin = timeoutMin
        }
    static member strategy(matrix: seq<string * obj>, ?failFast: bool): JobCreationCommand =
        AddStrategy {
           FailFast = failFast
           Matrix = Map.ofSeq matrix
        }
