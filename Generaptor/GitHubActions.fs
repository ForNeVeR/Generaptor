module Generaptor.GitHubActions

open System.Collections.Immutable

[<RequireQualifiedAccess>]
type Trigger =
    | OnPush of string[]

type Job = {
    Id: string
    RunsOn: string option
    Steps: ImmutableArray<Step>
}
and Step = {
    ActionName: string
}

type Workflow = {
    Name: string
    Triggers: ImmutableArray<Trigger>
    Jobs: ImmutableArray<Job>
}

type JobCreationCommand =
    | RunsOn of string
    | AddStep of Step

type WorkflowCreationCommand =
    | AddTrigger of Trigger
    | AddJob of string * JobCreationCommand seq

let private addTrigger wf trigger =
    { wf with Triggers = wf.Triggers.Add(trigger) }
let private addJob wf id commands =
    let mutable job = { Id = id; RunsOn = None; Steps = ImmutableArray.Empty }
    for command in commands do
        job <-
            match command with
            | RunsOn runsOn -> { job with RunsOn = Some runsOn }
            | AddStep step -> { job with Steps = job.Steps.Add(step) }
    { wf with Jobs = wf.Jobs.Add(job) }

let workflow (name: string) (commands: WorkflowCreationCommand seq): Workflow =
    let mutable wf = { Name = name; Triggers = ImmutableArray.Empty; Jobs = ImmutableArray.Empty }
    for command in commands do
        wf <-
            match command with
            | AddTrigger trigger -> addTrigger wf trigger
            | AddJob (id, jobCommands) -> addJob wf id jobCommands
    wf

type Library =
    static member onPushTo(branchName: string): WorkflowCreationCommand =
        AddTrigger(Trigger.OnPush [| branchName |])
    static member job (id: string) (commands: JobCreationCommand seq): WorkflowCreationCommand =
        AddJob(id, commands)

    static member runsOn(image: string): JobCreationCommand =
        RunsOn image
    static member step(uses: string): JobCreationCommand =
        AddStep {
            ActionName = uses
        }
