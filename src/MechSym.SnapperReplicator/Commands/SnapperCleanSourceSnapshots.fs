module MechSym.SnapperReplicator.Commands.SnapperCleanSourceSnapshots

open MechSym.Executor
open MechSym.SnapperReplicator.Types
open MechSym.SnapperReplicator.ShellCommand
open MechSym.ControlFlow.Operators
open MechSym.SnapperReplicator.SnapperCommand
open MechSym.ControlFlow
open MechSym.SnapperReplicator.RuntimeConfiguration

type SnapperCleanSourceSnapshotsError =
    | SnapperCleanSourceError of CommandExecutionError
    | SnapperCleanupDoneCreationError of CommandExecutionError
    
module SnapperCleanSourceSnapshotsError =
    let toMessage: SnapperCleanSourceSnapshotsError -> string = function
        | SnapperCleanSourceError err ->
            sprintf "Cannot clean up snapshots on source: %s" (err |> CommandExecutionError.toMessage)
        | SnapperCleanupDoneCreationError err ->
            sprintf "Cannot create snapper-cleanup.done file: %s" (err |> CommandExecutionError.toMessage)
        
let execute (algo: SnapperAlgorithm) (executor: ExecutorService) (config: RuntimeConfiguration): Result<unit, SnapperCleanSourceSnapshotsError> =
    printfn "Running snapper cleanup on source"
    SnapperCommand.Cleanup (Some config.SourceConfig, algo)
    |> (executor |> ExecutorService.getSourceExecutorOf SnapperCommand.command)
    |> Result.ignore
    |> Result.mapError SnapperCleanSourceError
    >>= (fun _ ->
        ShellCommand.Touch (config |> RuntimeConfiguration.snapperCleanupDoneFile)
        |> (executor |> ExecutorService.getLocalExecutorOf ShellCommand.command)
        |> Result.ignore
        |> Result.mapError SnapperCleanupDoneCreationError)