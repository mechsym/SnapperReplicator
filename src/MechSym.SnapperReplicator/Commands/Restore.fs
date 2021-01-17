module MechSym.SnapperReplicator.Commands.Restore


open System.IO

open MechSym.Executor
open MechSym.ControlFlow
open MechSym.ControlFlow.Operators
open MechSym.SnapperReplicator.Types
open MechSym.SnapperReplicator.ShellCommand
open MechSym.SnapperReplicator.Snapper
open MechSym.SnapperReplicator.RuntimeConfiguration
open MechSym.SnapperReplicator.BtrfsCommand
open MechSym.SnapperReplicator.ReplicationRequest
open MechSym.SnapperReplicator.ReplicationBatch

type ReceiveSnapshotError =
    | BtrfsReceiveError of Snapshot * CommandExecutionError
    | InfoXmlCopyError of Snapshot * CommandExecutionError
    
module ReceiveSnapshotError =
    let makeInfoXmlCopyError snapshot error = InfoXmlCopyError (snapshot, error)
    
    let makeBtrfsReceiveError snapshot error = BtrfsReceiveError (snapshot, error)    

let private copyInfoXmlFile (config: RuntimeConfiguration) (snapshot: Snapshot) executeShell: Result<unit, ReceiveSnapshotError> =
    let fileName = snapshot |> Snapshot.dumpInfoFileName
    let infoFile = Path.Join(config |> RuntimeConfiguration.getDestinationConfigWorkDir, fileName)
    ShellCommand.Copy (infoFile, (snapshot |> Snapshot.infoXml))
    |> executeShell
    |> Result.mapError (ReceiveSnapshotError.makeInfoXmlCopyError snapshot)
    |> Result.ignore   
    
let private receiveSnapshot (config: RuntimeConfiguration) (snapshot: Snapshot) executeBtrfs: Result<unit, ReceiveSnapshotError> =
    let fileName = snapshot |> Snapshot.dumpSnapshotFileName
    printfn "Receiving %s" fileName
    let snapshotFile = Path.Join(config |> RuntimeConfiguration.getDestinationConfigWorkDir, fileName)
    BtrfsCommand.Receive(snapshot, snapshotFile)
    |> executeBtrfs
    |> Result.mapError (ReceiveSnapshotError.makeBtrfsReceiveError snapshot)    
    |> Result.ignore
    
type RestoreSnapshotError =
    | ParentDirectoryCreationFailure of CommandExecutionError
    | ReceiveSnapshotError of ReceiveSnapshotError
    
let private restoreSnapshot (config: RuntimeConfiguration) (executor: ExecutorService) (request: Snapshot): unit -> Result<unit, RestoreSnapshotError> =
    let executeShell = executor |> ExecutorService.getDestinationExecutorOf ShellCommand.command
    let executeBtrfs = executor |> ExecutorService.getDestinationExecutorOf BtrfsCommand.command    
    fun () ->
        request 
        |> Snapshot.absoluteParentPath
        |> ShellCommand.CreateDir
        |> executeShell
        |> Result.mapError ParentDirectoryCreationFailure
        >>= (fun _ -> copyInfoXmlFile config request executeShell |> Result.mapError ReceiveSnapshotError)
        >>= (fun _ -> receiveSnapshot config request executeBtrfs |> Result.mapError ReceiveSnapshotError)
        |> Result.ignore

type RestoreError =
    | PendingChangesError of DetermineChanges.ParsePendingChangesError
    | RestoreDoneFileCreationError of CommandExecutionError
    | RestoreSnapshotError of RestoreSnapshotError
    
module RestoreError =
    let toMessage: RestoreError -> string = function
        | PendingChangesError err -> sprintf "Restoration failed: %s" (err |> DetermineChanges.ParsePendingChangesError.toMessage)
        | RestoreDoneFileCreationError err -> sprintf "Restore failed. Cannot create restore.done file: %s" (err |> CommandExecutionError.toMessage)
        | RestoreSnapshotError restoreError ->
            match restoreError with
            | ParentDirectoryCreationFailure err -> sprintf "Restore failed. Cannot create parent directory: %s" (err |> CommandExecutionError.toMessage)
            | ReceiveSnapshotError snapshotError ->
                match snapshotError with
                | BtrfsReceiveError (snapshot, err) ->
                    sprintf "Restore failed. Cannot receive %s: %s" (snapshot |> Snapshot.dumpSnapshotFileName) (err |> CommandExecutionError.toMessage)
                | InfoXmlCopyError (snapshot, err) ->
                    sprintf "Restore failed. Cannot copy %s: %s" (snapshot |> Snapshot.dumpInfoFileName) (err |> CommandExecutionError.toMessage)

let private getDestinationSnapshots (batch: ReplicationBatch): Snapshot list =
    batch.Requests
    |> List.map (ReplicationRequest.getSnapshot >> Snapshot.convertTo batch.DestinationConfig)
    
let private touchRestoreDoneFile (config: RuntimeConfiguration) (executor: ExecutorService): Result<unit, RestoreError> =
    let executeShell = executor |> ExecutorService.getLocalExecutorOf ShellCommand.command
    ShellCommand.Touch (config |> RuntimeConfiguration.restoreDoneFile)
    |> executeShell
    |> Result.mapError RestoreDoneFileCreationError
    |> Result.ignore    

let execute (executor: ExecutorService) (config: RuntimeConfiguration): Result<unit, RestoreError> =
    printfn "Restoring..."
    config
    |> DetermineChanges.parsePendingChanges executor
    |> Result.mapError PendingChangesError
    |> Result.map getDestinationSnapshots
    >>= (fun snapshots ->
        if snapshots |> List.isEmpty then
            printfn "There is nothing to restore"
            Ok()
        else
            snapshots
            |> List.map (restoreSnapshot config executor)
            |> Result.delayChain
            |> Result.mapError RestoreSnapshotError
            |> Result.ignore)
    >>= (fun _ -> touchRestoreDoneFile config executor)

