namespace MechSym.SnapperReplicator.ReplicationRequest

open System.IO
open Thoth.Json.Net
open MechSym.SnapperReplicator.ShellCommand
open MechSym.SnapperReplicator.Snapper
open MechSym.SnapperReplicator.Snapper.Serialization
open MechSym.SnapperReplicator.BtrfsCommand

type FullReplicationRequest =
  { Subject: Snapshot }  

type IncrementalReplicationRequest =
  { Parent: Snapshot
    Subject: Snapshot }

type ReplicationRequest =
  | Full of FullReplicationRequest
  | Incremental of IncrementalReplicationRequest

module ReplicationRequest =
    let sendSnapshot (targetPathBase : string) (this: ReplicationRequest): BtrfsCommand =
        match this with
        | Full request ->
            let fileName = request.Subject |> Snapshot.dumpSnapshotFileName
            let snapshotTargetFile = Path.Join(targetPathBase, fileName)
            BtrfsCommand.Send (request.Subject, None, Some snapshotTargetFile)
          
        | Incremental request ->
            let fileName = request.Subject |> Snapshot.dumpSnapshotFileName
            let snapshotTargetFile = Path.Join(targetPathBase, fileName)    
            BtrfsCommand.Send (request.Subject, Some request.Parent, Some snapshotTargetFile)
        
    let copyInfoXml (targetPathBase : string) (this: ReplicationRequest): ShellCommand =
        let requestSubject =
            match this with
            | Full request -> request.Subject 
            | Incremental request -> request.Subject
            
        let dumpFileName = requestSubject |> Snapshot.dumpInfoFileName
        let dumpFilePath = Path.Join(targetPathBase, dumpFileName)
        let infoFilePath = requestSubject |> Snapshot.infoXml
        ShellCommand.Copy (infoFilePath, dumpFilePath)      

    let getSnapshot (this: ReplicationRequest): Snapshot =
        match this with
        | Full request -> request.Subject
        | Incremental request -> request.Subject

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
