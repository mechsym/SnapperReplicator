module MechSym.SnapperReplicator.Commands.CreateWorkDirs

open MechSym.Executor
open MechSym.SnapperReplicator.Types
open MechSym.SnapperReplicator.ShellCommand

open MechSym.ControlFlow
open MechSym.ControlFlow.Operators
open MechSym.SnapperReplicator.RuntimeConfiguration

type CreateWorkDirsError =
    | SourceConfigDirCreationError of CommandExecutionError
    | DestinationConfigDirCreationError of CommandExecutionError
    | DoneFileCreationError of CommandExecutionError
    
module CreateWorkDirsError =
    let toMessage: CreateWorkDirsError -> string = function
        | SourceConfigDirCreationError err ->
            sprintf "Source config dir creation failed: %s" (err |> CommandExecutionError.toMessage)
        | DestinationConfigDirCreationError err ->
            sprintf "Destination config dir creation failed.: %s" (err |> CommandExecutionError.toMessage)
        | DoneFileCreationError err ->
            sprintf "Config dir creation failed. Cannot create done file: %s" (err |> CommandExecutionError.toMessage)            

let execute (executor: ExecutorService) (config: RuntimeConfiguration): Result<unit, CreateWorkDirsError> =
    printfn "Creating workdirs..."

    let executeShellOnDestination =
        executor
        |> ExecutorService.getDestinationExecutorOf ShellCommand.command

    let executeShellOnSource =
        executor
        |> ExecutorService.getSourceExecutorOf ShellCommand.command

    let executeShellOnLocal =
        executor
        |> ExecutorService.getLocalExecutorOf ShellCommand.command

    let ensureDestinationConfigWorkDir () =
        ShellCommand.CreateDir
            (config
             |> RuntimeConfiguration.getDestinationConfigWorkDir)
        |> executeShellOnDestination
        |> Result.mapError DestinationConfigDirCreationError

    let ensureSourceConfigWorkDir () =
        ShellCommand.CreateDir
            (config
             |> RuntimeConfiguration.getSourceConfigWorkDir)
        |> executeShellOnSource
        |> Result.mapError SourceConfigDirCreationError

    let createDoneFile () =
        let file =
            config |> RuntimeConfiguration.workDirsDoneFile

        ShellCommand.Touch file
        |> executeShellOnLocal
        |> Result.mapError DoneFileCreationError

    ensureDestinationConfigWorkDir ()
    >>= (fun _ -> ensureSourceConfigWorkDir ())
    >>= (fun _ -> createDoneFile ())
    |> Result.ignore
