// SPDX-FileCopyrightText: 2025 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

namespace Generaptor

open System
open System.Globalization
open System.Threading.Tasks
open Octokit

type ActionVersion = ActionVersion of string

type IActionsClient =
    abstract member GetLastActionVersion: ownerAndName: string -> Task<ActionVersion>

type internal NumericVersion =
    | NumericVersion of major: int * minor: int option * patch: int option

    member this.Major = let (NumericVersion(m, _, _)) = this in m

    static member TryParse(s: string) =
        let (|Number|_|) (s: string): int option =
            match Int32.TryParse(s, CultureInfo.InvariantCulture) with
            | true, n -> Some n
            | false, _ -> None

        let strippedPrefix = if s.StartsWith 'v' then s.Substring 1 else s
        match strippedPrefix.Split '.' with
        | [| Number(m) |] -> Some <| NumericVersion(m, None, None)
        | [| Number(major); Number(minor) |] -> Some <| NumericVersion(major, Some minor, None)
        | [| Number(major); Number(minor); Number(patch) |] -> Some <| NumericVersion(major, Some minor, Some patch)
        | _ -> None

type ActionsClient() =

    static member SelectBestVersion(versions: string seq): string option =
        let parsedVersions =
            versions
            |> Seq.choose(fun v -> NumericVersion.TryParse v |> Option.map(fun n -> v, n))
            |> Seq.cache

        let latestVersion =
            parsedVersions
            |> Seq.sortByDescending snd
            |> Seq.tryHead

        latestVersion
        |> Option.map(fun(string, version) ->
            let versionsWithSameMajor = parsedVersions |> Seq.filter(fun(_, x) -> x.Major = version.Major)
            let majorOnlyVersion =
                versionsWithSameMajor
                |> Seq.filter(fun(_, x) -> x = NumericVersion(version.Major, None, None))
                |> Seq.tryHead
            match majorOnlyVersion with
            | Some(s, _) -> s
            | None -> string
        )

    interface IActionsClient with
        member this.GetLastActionVersion(ownerAndName) =
            let client = GitHubClient(ProductHeaderValue("generaptor"))
            let owner, name =
                match ownerAndName.Split '/' with
                | [| o; n |] -> o, n
                | _ -> failwithf $"Invalid repository owner/name: {ownerAndName}."
            task {
                let! tags = client.Repository.GetAllTags(owner, name)
                let versions = tags |> Seq.map _.Name
                return
                    match ActionsClient.SelectBestVersion versions with
                    | Some v -> ActionVersion v
                    | None -> failwithf $"Cannot find any version for action {ownerAndName}."
            }
