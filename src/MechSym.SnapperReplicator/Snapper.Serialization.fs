namespace MechSym.SnapperReplicator.Snapper.Serialization

open System
open Thoth.Json.Net
open MechSym.SnapperReplicator.Snapper


module Config =
    let fromJson (getter: Decode.IGetters) =
        let name =
            getter.Required.Field "config" Decode.string
            |> ConfigName

        let subVolume =
            getter.Required.Field "subvolume" Decode.string
            |> SubVolume

        { Config.SubVolume = subVolume
          Name = name }

    let toJson (this: Config): JsonValue =
        Encode.object [ "subvolume", Encode.string this.SubVolume.Value
                        "config", Encode.string this.Name.Value ]

    let parseSnapperListConfigsOutput (configName: ConfigName) (snapperListConfigsOutput: string) =
        let configsDecoder: Decoder<Map<string, Config list>> =
            Decode.dict (Decode.list (Decode.object fromJson))

        let parse (result: Map<string, Config list>) =
            result.["configs"]
            |> List.tryFind (fun config -> config.Name = configName)
            |> Option.defaultWith (fun () ->
                let availableConfigs = result.["configs"] |> List.map (fun config -> config.Name.Value) |> String.concat ", "
                let message = $"Cannot find config '{configName.Value}'. Available configs: {availableConfigs}"
                failwith message)

        Decode.fromString configsDecoder snapperListConfigsOutput
        |> Result.map parse

module Snapshot =
    type private Snapshots = Map<string, Snapshot list>

    let fromJson: Decoder<Snapshot> =
        Decode.object (fun (getter: Decode.IGetters) ->
            let subVolume =
                getter.Required.Field "subvolume" Decode.string
                |> SubVolume

            let number =
                getter.Required.Field "number" Decode.int
                |> SnapshotNumber

            let dateRaw =
                getter.Required.Field "date" Decode.string

            let date =
                if String.IsNullOrWhiteSpace dateRaw then None else DateTimeOffset.Parse dateRaw |> Some

            { Snapshot.SubVolume = subVolume
              Number = number
              Date = date })


    let toJson (this: Snapshot) =
        Encode.object [ "subvolume", Encode.string this.SubVolume.Value
                        "number", Encode.int this.Number.Value
                        "date",
                        Encode.string
                            (this.Date
                             |> Option.map (fun date -> date.ToString())
                             |> Option.defaultValue null) ]

    let private snapshotsDecoder: Decoder<Snapshots> = Decode.dict (Decode.list fromJson)

    let parseSnapperListOutput (ConfigName configName) (snapperListOutput: string): Result<Snapshot list, string> =
        let parse (result: Map<string, Snapshot list>) =
            result.[configName]
            |> List.filter (fun snapshot -> snapshot.Number.Value > 0)
            |> List.sortBy (fun snapshot -> snapshot.Number)

        Decode.fromString snapshotsDecoder snapperListOutput
        |> Result.map parse

