namespace MechSym.SnapperReplicator.ReplicationBatch

open MechSym.SnapperReplicator.Snapper
open MechSym.SnapperReplicator.ReplicationRequest

type ReplicationBatch =
    { SourceConfig: Config
      DestinationConfig: Config
      Requests: ReplicationRequest list }

module ReplicationBatch =
    let private trimResult (maxSize: int) (result: ReplicationRequest list) =
        if result.Length > maxSize then
            result |> List.take maxSize
        else
            result
    
    let createBatch (maxSize: int)
                    (originalSource: SnapperConfigState)
                    (originalDestination: SnapperConfigState)
                    : ReplicationBatch =
                        
        let rec loop (source: Snapshot list)
                     (destination: Snapshot list)
                     (maybeParent: Snapshot option)
                     : ReplicationRequest list =
                         
            match (destination, source) with
            | destinationHead :: destinationTail, sourceHead :: sourceTail ->
                if destinationHead.Number < sourceHead.Number then
                    loop source destinationTail maybeParent
                else if destinationHead.Number = sourceHead.Number then
                    loop sourceTail destinationTail (Some sourceHead)
                else
                    loop sourceTail destination maybeParent

            | [], sourceHead :: _sourceTail ->
                let firstRequest =
                    match maybeParent with
                    | Some parent ->
                        Incremental
                            { IncrementalReplicationRequest.Parent = parent
                              Subject = sourceHead }

                    | None -> Full { FullReplicationRequest.Subject = sourceHead }

                let subsequentRequests =
                    source
                    |> List.pairwise
                    |> List.map (fun (first, second) ->
                        Incremental
                            { IncrementalReplicationRequest.Parent = first
                              Subject = second })

                firstRequest :: subsequentRequests

            | _sourceHead :: _sourceTail, [] -> []
            | [], [] -> []

        loop originalSource.Snapshots originalDestination.Snapshots None
        |> trimResult maxSize
        |> fun requests ->
            { ReplicationBatch.Requests = requests
              SourceConfig = originalSource.Config
              DestinationConfig = originalDestination.Config }

