module MechSym.SnapperReplicator.Commands.Dump

open MechSym.Executor
open MechSym.ControlFlow
open MechSym.ControlFlow.Operators
open MechSym.SnapperReplicator.BtrfsCommand
open MechSym.SnapperReplicator.Types
open MechSym.SnapperReplicator.ShellCommand
open MechSym.SnapperReplicator.Snapper
open MechSym.SnapperReplicator.RuntimeConfiguration

open MechSym.SnapperReplicator.ReplicationRequest
open MechSym.SnapperReplicator.ReplicationBatch

type SendSnapshotsError =
    | SendSnapshotError of ReplicationRequest * CommandExecutionError
    | CopyInfoXmlError of ReplicationRequest * CommandExecutionError

module SendSnapshotsError =
    let makeSendSnapshotError (snapshot: ReplicationRequest) (err: CommandExecutionError) =
        SendSnapshotError(snapshot, err)

    let makeCopyInfoXmlError (snapshot: ReplicationRequest) (err: CommandExecutionError) =
        CopyInfoXmlError(snapshot, err)

let private sendSnapshots (runtimeConfig: RuntimeConfiguration)
                          (batch: ReplicationBatch)
                          (executor: ExecutorService)
                          (preferredDumpType: DumpType)
                          : Result<unit, SendSnapshotsError> =
    let configWorkdir =
        runtimeConfig
        |> RuntimeConfiguration.getSourceConfigWorkDir

    let shellExecute =
        executor
        |> ExecutorService.getSourceExecutorOf ShellCommand.command

    let btrfsExecute =
        executor
        |> ExecutorService.getSourceExecutorOf BtrfsCommand.command

    let delayedSendSnapshot (request: ReplicationRequest) =
        fun () ->
            request
            |> function
            | Full full ->
                printfn "Full %d (%s)"
                        full.Subject.Number.Value
                        (full.Subject.Date |> Option.map (fun dat -> dat.ToString("o")) |> Option.defaultValue "")
                Full full
            | Incremental incremental ->
                match preferredDumpType with
                | DumpType.Incremental ->
                    printfn "Incremental %d (%s) -> %d (%s)"
                        incremental.Parent.Number.Value
                        (incremental.Parent.Date |> Option.map (fun dat -> dat.ToString("o")) |> Option.defaultValue "")
                        incremental.Subject.Number.Value
                        (incremental.Subject.Date |> Option.map (fun dat -> dat.ToString("o")) |> Option.defaultValue "")
                    Incremental incremental
                | DumpType.Full ->
                    printfn "Full %d (%s)"
                            incremental.Subject.Number.Value
                            (incremental.Subject.Date |> Option.map (fun dat -> dat.ToString("o")) |> Option.defaultValue "")
                    Full { FullReplicationRequest.Subject = incremental.Subject }
            |> ReplicationRequest.sendSnapshot configWorkdir
            |> btrfsExecute
            |> Result.mapError (SendSnapshotsError.makeSendSnapshotError request)
            >>= (fun _ ->
                request
                |> ReplicationRequest.copyInfoXml configWorkdir
                |> shellExecute
                |> Result.mapError (SendSnapshotsError.makeCopyInfoXmlError request))

    if batch.Requests |> List.isEmpty then
        printfn "There is nothing to dump"
        Ok()
    else
        batch.Requests
        |> List.map delayedSendSnapshot
        |> Result.delayChain
        |> Result.ignore

type DumpError =
    | PendingChangesError of DetermineChanges.ParsePendingChangesError
    | SendSnapshotsError of SendSnapshotsError
    | DumpDoneCreationError of CommandExecutionError

module DumpError =
    let toMessage: DumpError -> string =
        function
        | PendingChangesError err -> sprintf "Dump failed: %s" (err |> DetermineChanges.ParsePendingChangesError.toMessage)
        | DumpDoneCreationError err ->
            sprintf "Dump failed. Cannot create dump.done file: %s" (err |> CommandExecutionError.toMessage)
        | SendSnapshotsError snapshotsError ->
            match snapshotsError with
            | SendSnapshotError (request, err) ->
                sprintf
                    "Dump failed. Cannot send snapshot %s: %s"
                    (request
                     |> ReplicationRequest.getSnapshot
                     |> Snapshot.dumpSnapshotFileName)
                    (err |> CommandExecutionError.toMessage)
            | CopyInfoXmlError (request, err) ->
                sprintf
                    "Dump failed. Cannot copy info xml %s: %s"
                    (request
                     |> ReplicationRequest.getSnapshot
                     |> Snapshot.dumpInfoFileName)
                    (err |> CommandExecutionError.toMessage)   

let execute (executor: ExecutorService)
            (config: RuntimeConfiguration)
            (preferredDumpType: DumpType)
            : Result<unit, DumpError> =
    printfn "Dumping source..."

    config
    |> DetermineChanges.parsePendingChanges executor
    |> Result.mapError PendingChangesError
    >>= (fun batch ->
        sendSnapshots config batch executor preferredDumpType
        |> Result.mapError SendSnapshotsError)
    >>= (fun _ ->
        let shellExecute =
            executor
            |> ExecutorService.getLocalExecutorOf ShellCommand.command

        ShellCommand.Touch(config |> RuntimeConfiguration.dumpDoneFile)
        |> shellExecute
        |> Result.mapError DumpDoneCreationError
        |> Result.ignore)
