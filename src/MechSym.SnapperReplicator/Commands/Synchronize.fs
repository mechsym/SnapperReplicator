module MechSym.SnapperReplicator.Commands.Synchronize

open System.IO


open MechSym.Executor
open MechSym.FileTransfer

open MechSym.ControlFlow
open MechSym.ControlFlow.Operators
open MechSym.SnapperReplicator.Types
open MechSym.SnapperReplicator.ShellCommand
open MechSym.SnapperReplicator.Snapper
open MechSym.SnapperReplicator.RuntimeConfiguration
open MechSym.SnapperReplicator.ReplicationRequest
open MechSym.SnapperReplicator.ReplicationBatch

type CopySnapshotsError =
    | FailedFileTransferError of FileTransferError
    
let private copySnapshots (config: RuntimeConfiguration)
                          (batch: ReplicationBatch)
                          (executor: ExecutorService)
                          (fileTransferService: IFileTransferService)
                          : Result<unit, CopySnapshotsError> =
    let executeShellOnLocal =
        executor
        |> ExecutorService.getLocalExecutorOf ShellCommand.command

    batch.Requests
    |> List.map (ReplicationRequest.getSnapshot >> Snapshot.dumpSnapshotFileName)
    |> List.append
        (batch.Requests
         |> List.map (ReplicationRequest.getSnapshot >> Snapshot.dumpInfoFileName))
    |> fun (fileNames: string list) ->
        let sourceConfigWorkDir =
            config
            |> RuntimeConfiguration.getSourceConfigWorkDir

        let destinationConfigWorkDir =
            config
            |> RuntimeConfiguration.getDestinationConfigWorkDir        
        
        match config.OperationMode with
        | OperationMode.Pull -> fileTransferService.download sourceConfigWorkDir fileNames destinationConfigWorkDir
        | OperationMode.Push -> fileTransferService.upload sourceConfigWorkDir fileNames destinationConfigWorkDir
    |> Result.mapError FailedFileTransferError

type SynchronizationError =
    | CopySnapshotsError of CopySnapshotsError
    | PendingChangesError of DetermineChanges.ParsePendingChangesError
    | SyncDoneFileCreationError of CommandExecutionError
    
module SynchronizationError =
    let toMessage: SynchronizationError -> string = function
        | PendingChangesError err -> sprintf "Synchronization failed: %s" (err |> DetermineChanges.ParsePendingChangesError.toMessage)
        | CopySnapshotsError snapshotsError ->
            match snapshotsError with
            | FailedFileTransferError transferError ->
                sprintf "Synchronization failed. Cannot transfer files: %s" (transferError |> FileTransferError.toMessage)
        | SyncDoneFileCreationError err -> sprintf "Synchronization failed. Cannot create sync.done file: %s" (err |> CommandExecutionError.toMessage)

let touchDoneFile (config: RuntimeConfiguration) (executor: ExecutorService) =
    let executeShellOnLocal =
        executor
        |> ExecutorService.getLocalExecutorOf ShellCommand.command    
    
    ShellCommand.Touch(config |> RuntimeConfiguration.syncDoneFile)
    |> executeShellOnLocal
    |> Result.mapError SyncDoneFileCreationError
    |> Result.ignore 

let execute (fileTransferService: IFileTransferService)
            (executor: ExecutorService)
            (config: RuntimeConfiguration)
            : Result<unit, SynchronizationError> =
    printfn "Synchronizing..."

    config
    |> DetermineChanges.parsePendingChanges executor
    |> Result.mapError PendingChangesError
    >>= (fun batch ->
        if batch.Requests |> List.isEmpty then
            printfn "There is nothing to transfer"
            Ok ()
        else
            copySnapshots config batch executor fileTransferService
            |> Result.mapError CopySnapshotsError)
    >>= (fun _ -> touchDoneFile config executor)
