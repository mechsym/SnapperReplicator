module MechSym.SnapperReplicator.Commands.CleanLocalWorkDir

open MechSym.ControlFlow
open MechSym.Executor
open MechSym.SnapperReplicator.Types
open MechSym.SnapperReplicator.ShellCommand
open MechSym.SnapperReplicator.RuntimeConfiguration

type CleanLocalWorkDirError =
    | FailedLocalWorkDirError of CommandExecutionError
    
module CleanLocalWorkDirError =
    let toMessage: CleanLocalWorkDirError -> string = function
        | FailedLocalWorkDirError err ->
            sprintf "Clean local workdir failed: %s" (err |> CommandExecutionError.toMessage)

let execute (executor: ExecutorService): RuntimeConfiguration -> Result<unit, CleanLocalWorkDirError> =
    printfn "Cleaning local workdir"
    RuntimeConfiguration.getLocalConfigWorkDir
    >> ShellCommand.Remove
    >> (executor |> ExecutorService.getLocalExecutorOf ShellCommand.command)
    >> Result.mapError FailedLocalWorkDirError
    >> Result.ignore