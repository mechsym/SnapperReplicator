namespace MechSym.SnapperReplicator.ReplicationBatch.Serialization

open MechSym.SnapperReplicator.Snapper.Serialization
open MechSym.SnapperReplicator.ReplicationBatch
open Thoth.Json.Net
open MechSym.SnapperReplicator.ReplicationRequest

module ReplicationBatch =
    let fromJson: Decoder<ReplicationBatch> =
        Decode.object (fun getters ->
            let source =
                getters.Required.Field "source_config" (Decode.object Config.fromJson)

            let target =
                getters.Required.Field "target_config" (Decode.object Config.fromJson)

            let requests =
                getters.Required.Field "requests" (Decode.list ReplicationRequest.fromJson)

            { ReplicationBatch.SourceConfig = source
              DestinationConfig = target
              Requests = requests })

    let toJson (this: ReplicationBatch): JsonValue =
        Encode.object
            [ "source_config", this.SourceConfig |> Config.toJson
              "target_config", this.DestinationConfig |> Config.toJson
              "requests", Encode.list
                            (this.Requests
                             |> List.map ReplicationRequest.toJson) ]

