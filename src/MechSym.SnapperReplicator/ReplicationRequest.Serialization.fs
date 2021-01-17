namespace MechSym.SnapperReplicator.ReplicationRequest.Serialization

open Thoth.Json.Net
open MechSym.SnapperReplicator.Snapper.Serialization
open MechSym.SnapperReplicator.ReplicationRequest

module ReplicationRequest =
    let fromJson: Decoder<ReplicationRequest> =
        Decode.object (fun getters -> 
            let ``type`` = getters.Required.Field "type" Decode.string
            let subject = getters.Required.Field "subject" Snapshot.fromJson
            match ``type`` with
            | "full" ->
                Full { FullReplicationRequest.Subject = subject }
            | "incremental" ->
                let parent = getters.Required.Field "parent" Snapshot.fromJson
                Incremental
                  { IncrementalReplicationRequest.Subject = subject 
                    Parent = parent } 
            | x -> failwithf "Unknown request type: %A" x)

    let toJson (this: ReplicationRequest): JsonValue =
      Encode.object
        [
          match this with
          | Full request ->
              "type", Encode.string "full"
              "subject", request.Subject |> Snapshot.toJson
          | Incremental request ->
              "type", Encode.string "incremental"
              "subject", request.Subject |> Snapshot.toJson
              "parent", request.Parent |> Snapshot.toJson
        ]
