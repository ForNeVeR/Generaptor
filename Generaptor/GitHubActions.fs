// SPDX-FileCopyrightText: 2024-2025 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

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

type ConcurrencySetCommand = {
   Group: string
   CancelInProgress: bool
}

[<RequireQualifiedAccess>]
type PermissionKind =
    | Actions
    | Contents
    | IdToken
    | Pages
    | PullRequests
[<RequireQualifiedAccess>]
type AccessKind =
    | Read
    | Write
    | None

type ActionSpec =
    | ActionWithVersion of nameWithVersion: string
    /// This will read the highest major action version from the YAML file if it exists. If not found, will fall back
    /// to the latest available action tag on GitHub.
    | Auto of name: string

type JobEnvironment ={
    Name: string
    Url: string
}

type Job = {
    Id: string
    Name: string option
    Permissions: Map<PermissionKind, AccessKind>
    Needs: ImmutableArray<string>
    Strategy: Strategy option
    RunsOn: string option
    Environment: JobEnvironment option
    Env: Map<string, string>
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
    Concurrency: ConcurrencySetCommand option
    Permissions: Map<PermissionKind, AccessKind>
    Triggers: Triggers
    Jobs: ImmutableArray<Job>
}

type JobCreationCommand =
    | Name of string
    | AddJobPermission of PermissionKind * AccessKind
    | Needs of string
    | RunsOn of string
    | AddStep of Step
    | SetEnvironment of name: string * url: string
    | SetEnv of string * string
    | AddStrategy of Strategy

type WorkflowCreationCommand =
    | SetHeader of string
    | SetName of string
    | SetConcurrency of ConcurrencySetCommand
    | AddWorkflowPermission of PermissionKind * AccessKind
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
        Permissions = Map.empty
        Needs = ImmutableArray.Empty
        RunsOn = None
        Environment = None
        Env = Map.empty
        Steps = ImmutableArray.Empty
    }
    for command in commands do
        job <-
            match command with
            | Name n -> { job with Name = Some n }
            | AddJobPermission(p, a) -> { job with Permissions = Map.add p a job.Permissions }
            | Needs needs -> { job with Needs = job.Needs.Add needs }
            | RunsOn runsOn -> { job with RunsOn = Some runsOn }
            | AddStep step -> { job with Steps = job.Steps.Add(step) }
            | SetEnvironment(name, url) -> { job with Environment = Some { Name = name; Url = url } }
            | SetEnv (key, value) -> { job with Env = Map.add key value job.Env }
            | AddStrategy s -> { job with Strategy = Some s }
    job

let workflow (id: string) (commands: WorkflowCreationCommand seq): Workflow =
    let mutable wf = {
        Id = id
        Header = None
        Name = None
        Concurrency = None
        Permissions = Map.empty
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
            | SetConcurrency c -> { wf with Concurrency = Some c }
            | AddTrigger trigger -> addTrigger wf trigger
            | AddWorkflowPermission(p, a) -> { wf with Permissions = Map.add p a wf.Permissions }
            | AddJob job -> { wf with Jobs = wf.Jobs.Add job }
    wf

type Commands =
    static member name(name: string): WorkflowCreationCommand =
        SetName name
    static member header(headerText: string): WorkflowCreationCommand =
        SetHeader headerText

    static member workflowConcurrency(group: string, cancelInProgress: bool): WorkflowCreationCommand =
        SetConcurrency { Group = group; CancelInProgress = cancelInProgress }
    static member workflowPermission(permission: PermissionKind, access: AccessKind): WorkflowCreationCommand =
        AddWorkflowPermission(permission, access)

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
    [<Obsolete("Use jobPermission(PermissionKind.Contents, AccessKind.Write)")>]
    static member writeContentPermissions: JobCreationCommand =
        Commands.jobPermission(PermissionKind.Contents, AccessKind.Write)
    static member jobPermission(permission: PermissionKind, access: AccessKind): JobCreationCommand =
        AddJobPermission(permission, access)
    static member needs(jobId: string): JobCreationCommand =
        Needs jobId
    static member runsOn(image: string): JobCreationCommand =
        RunsOn image

    /// https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax#jobsjob_idenvironment
    static member environment(name: string, url: string): JobCreationCommand =
        SetEnvironment(name, url)

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
