module Generaptor.Tests.TestFramework

open System.Threading.Tasks
open Generaptor

let MockActionsClient = {
    new IActionsClient with
        member this.GetLastActionVersion(ownerAndName) =
            Task.FromResult(
                match ownerAndName with
                | "ForNeVeR/ChangelogAutomation.action" -> ActionVersion "v10"
                | _ -> failwithf $"Not allowed to fetch action version for {ownerAndName}."
            )
}
