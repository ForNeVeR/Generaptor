﻿module Generaptor.GitHubActions

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
    Triggers: Triggers
    Jobs: ImmutableArray<Job>
}

type JobCreationCommand =
    | RunsOn of string
    | AddStep of Step
    | SetEnv of string * string

type WorkflowCreationCommand =
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
    let mutable job = { Id = id; Strategy = None; RunsOn = None; Environment = Map.empty; Steps = ImmutableArray.Empty }
    for command in commands do
        job <-
            match command with
            | RunsOn runsOn -> { job with RunsOn = Some runsOn }
            | AddStep step -> { job with Steps = job.Steps.Add(step) }
            | SetEnv (key, value) -> { job with Environment = Map.add key value job.Environment}
    job

let workflow (id: string) (commands: WorkflowCreationCommand seq): Workflow =
    let mutable wf = {
        Id = id
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
            | SetName name -> { wf with Name = Some name }
            | AddTrigger trigger -> addTrigger wf trigger
            | AddJob job -> { wf with Jobs = wf.Jobs.Add job }
    wf

type Commands =
    static member name(name: string): WorkflowCreationCommand =
        SetName name

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
