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
    | SyncDoneFileCreationError of CommandExecutionError

let private copySnapshots (config: RuntimeConfiguration)
                          (batch: ReplicationBatch)
                          (executor: ExecutorService)
                          (fileTransferService: IFileTransferService)
                          : Result<unit, CopySnapshotsError> =
    let executeShellOnLocal =
        executor
        |> ExecutorService.getLocalExecutorOf ShellCommand.command

    let sourceConfigWorkDir =
        config
        |> RuntimeConfiguration.getSourceConfigWorkDir

    let destinationConfigWorkDir =
        config
        |> RuntimeConfiguration.getDestinationConfigWorkDir

    batch.Requests
    |> List.map (fun request ->
        let snapshot =
            request |> ReplicationRequest.getSnapshot

        let snapshotFileName = snapshot |> Snapshot.dumpSnapshotFileName

        let sourceSnapshotFile =
            Path.Join(sourceConfigWorkDir, snapshotFileName)

        sourceSnapshotFile)

    |> List.append
        (batch.Requests
         |> List.map (fun request ->
             let snapshot =
                 request |> ReplicationRequest.getSnapshot

             let infoFileName =
                 snapshot |> Snapshot.dumpInfoFileName

             let sourceInfoFile =
                 Path.Join(sourceConfigWorkDir, infoFileName)

             sourceInfoFile))
    |> fun (transfers: string list) ->
        match config.OperationMode with
        | OperationMode.Pull -> fileTransferService.download transfers destinationConfigWorkDir
        | OperationMode.Push -> fileTransferService.upload transfers destinationConfigWorkDir
    |> Result.mapError FailedFileTransferError
    >>= (fun _ ->
        ShellCommand.Touch(config |> RuntimeConfiguration.syncDoneFile)
        |> executeShellOnLocal
        |> Result.mapError SyncDoneFileCreationError
        |> Result.ignore)

type SynchronizationError =
    | CopySnapshotsError of CopySnapshotsError
    | PendingChangesError of DetermineChanges.ParsePendingChangesError
    
module SynchronizationError =
    let toMessage: SynchronizationError -> string = function
        | PendingChangesError err -> sprintf "Synchronization failed: %s" (err |> DetermineChanges.ParsePendingChangesError.toMessage)
        | CopySnapshotsError snapshotsError ->
            match snapshotsError with
            | SyncDoneFileCreationError err -> sprintf "Synchronization failed. Cannot create sync.done file: %s" (err |> CommandExecutionError.toMessage)
            | FailedFileTransferError transferError ->
                sprintf "Synchronization failed. Cannot transfer files: %s" (transferError |> FileTransferError.toMessage)

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
            Ok()
        else
            copySnapshots config batch executor fileTransferService
            |> Result.mapError CopySnapshotsError)
