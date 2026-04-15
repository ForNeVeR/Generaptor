// SPDX-FileCopyrightText: 2025 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module Generaptor.Tests.ActionsClientTests

open System.Threading.Tasks
open Generaptor
open Xunit

[<Fact>]
[<Trait("Category", "SkipOnCI")>]
let ``ChangelogAutomation.action last version``(): Task =
    let client = ActionsClient() :> IActionsClient
    task {
        let! version = client.GetLastActionVersion "ForNeVeR/ChangelogAutomation.action"
        Assert.Equal(ActionVersion "v2", version)
    }

[<Fact>]
let ``BestVersions for empty list``(): unit =
    Assert.Equal(None, ActionsClient.SelectBestVersion [])

[<Fact>]
let ``BestVersions for non-prefixed versions``(): unit =
    Assert.Equal(Some "1", ActionsClient.SelectBestVersion ["1.1"; "1"; "0.9"])

[<Fact>]
let ``BestVersions for prefixed versions``(): unit =
    Assert.Equal(Some "v2", ActionsClient.SelectBestVersion ["v1.1"; "v2"; "v0.9"])

[<Fact>]
let ``Major-only version has the highest priority``(): unit =
    Assert.Equal(Some "v1", ActionsClient.SelectBestVersion ["v1"; "v1.1"; "v1.1.1"])

[<Fact>]
let ``BestVersions when no major-only exists``(): unit =
    Assert.Equal(Some "v2.1", ActionsClient.SelectBestVersion ["v1.1"; "v2.1"; "v1"])

[<Fact>]
let ``BestVersions for mixed versions``(): unit =
    Assert.Equal(Some "10.1", ActionsClient.SelectBestVersion ["10.1"; "v2"; "v0.9"])

[<Fact>]
[<Trait("Category", "SkipOnCI")>]
let ``Pick the latest action version for specific path``(): Task =
    let client = ActionsClient() :> IActionsClient
    task {
        let! version = client.GetLastActionVersion "ForNeVeR/Todosaurus/action"
        Assert.Equal(ActionVersion "v1", version)
    }
