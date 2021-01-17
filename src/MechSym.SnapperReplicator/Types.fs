namespace MechSym.SnapperReplicator.Types

open System

type OperationMode =
    | Push
    | Pull

type TransferMode =
    | Sftp
    | Rsync

type DumpType =
    | Full
    | Incremental
    
type ICommand<'a> =
    abstract member GetExecutable: 'a -> string
    abstract member GetParameters: 'a -> string list
    abstract member GetStdin: 'a -> byte[] option

type CommandExecutionError =
    | LocalExecutionError of command: string * exitCode: int * output: string
    | RemoteExecutionError of command: string * exitCode: int * output: string
    | RemoteInputStreamNotSupportedError of command: string
    
module CommandExecutionError =
    let toMessage: CommandExecutionError -> string = function
        | LocalExecutionError (_command, exitCode, output) ->
            sprintf "Local command execution failure: %i, %s" exitCode output
        | RemoteExecutionError (_command, exitCode, output) ->
            sprintf "Remote command execution failure: %i, %s" exitCode output
        | RemoteInputStreamNotSupportedError _ ->
            sprintf "Remote command execution failure: remote execution doesn't support input stream"
    
type IExecutor =
    inherit IDisposable
    abstract member GetExecutor: ICommand<'a> -> 'a -> Result<string, CommandExecutionError>     