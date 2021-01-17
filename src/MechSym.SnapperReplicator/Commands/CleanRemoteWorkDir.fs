module MechSym.SnapperReplicator.Commands.CleanRemoteWorkDir

open MechSym.ControlFlow
open MechSym.Executor
open MechSym.SnapperReplicator.Types
open MechSym.SnapperReplicator.ShellCommand
open MechSym.SnapperReplicator.RuntimeConfiguration

type CleanRemoteWorkDirError =
    | FailedRemoteWorkDirError of CommandExecutionError
    
module CleanRemoteWorkDirError =
    let toMessage: CleanRemoteWorkDirError -> string = function
        | FailedRemoteWorkDirError err ->
            sprintf "Clean remote workdir failed: %s" (err |> CommandExecutionError.toMessage)    

let execute (executor: ExecutorService): RuntimeConfiguration -> Result<unit, CleanRemoteWorkDirError> =
    printfn "Cleaning remote workdir"
    RuntimeConfiguration.getRemoteConfigWorkDir
    >> ShellCommand.Remove
    >> (executor |> ExecutorService.getRemoteExecutorOf ShellCommand.command)
    >> Result.mapError FailedRemoteWorkDirError
    >> Result.ignore