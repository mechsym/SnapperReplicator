namespace MechSym.Executor

open System.IO
open Renci.SshNet
open Fake.Core
open MechSym.SnapperReplicator.Types

module Executor =
    let local (verbose: bool) =
        { new IExecutor with
            member this.GetExecutor (commandFacade: ICommand<'a>) (command: 'a) =
                let parse (command: string) (output: ProcessResult<ProcessOutput>) = 
                    if output.ExitCode = 0 then
                        Ok output.Result.Output
                    else
                        Error (LocalExecutionError (command, output.ExitCode, output.Result.Error))
                let executable = commandFacade.GetExecutable command
                let parameters = commandFacade.GetParameters command
                let maybeStdin = commandFacade.GetStdin command
                let commandText = [ executable; yield! parameters ] |> String.concat " "
                if verbose then
                    printfn "Executing locally: %s" commandText
                match maybeStdin with
                | Some stdin ->
                    use ms = new MemoryStream(stdin)
                    CreateProcess.fromRawCommandLine
                        executable
                        (parameters |> String.concat " ")
                    |> CreateProcess.redirectOutput
                    |> CreateProcess.withStandardInput(StreamSpecification.UseStream(true, ms))
                    |> CreateProcess.map (parse commandText)
                    |> Proc.run                    
                | None ->
                    CreateProcess.fromRawCommandLine
                        executable
                        (parameters |> String.concat " ")
                    |> CreateProcess.redirectOutput
                    |> CreateProcess.map (parse commandText)
                    |> Proc.run
            member this.Dispose() = () }

    let remote (connection: ConnectionInfo) (verbose: bool) =
        let client = new SshClient(connection)
        client.Connect()
        { new IExecutor with
            member this.GetExecutor (commandFacade: ICommand<'a>) (command: 'a) =
                let commandText = [ commandFacade.GetExecutable command; yield! commandFacade.GetParameters command ] |> String.concat " "
                if verbose then
                    printfn "Executing remotely: %s" commandText
                    
                let command = client.CreateCommand(commandText)
                let result = command.Execute()
                if command.ExitStatus = 0 then
                    Ok result
                else
                    let code = command.ExitStatus
                    let error = command.Error
                    Error (RemoteExecutionError (commandText, code, error))         
    
            member this.Dispose() =
                client.Disconnect()
                client.Dispose() }    
    
type ExecutorService =
    { Local: IExecutor
      Remote: IExecutor
      Mode: OperationMode }
    
module ExecutorService =
    let getSource (this: ExecutorService): IExecutor =
        match this.Mode with
        | OperationMode.Pull ->
            this.Remote
        | OperationMode.Push ->
            this.Local
    
    let getDestination (this: ExecutorService): IExecutor =
        match this.Mode with
        | OperationMode.Pull ->
            this.Local
        | OperationMode.Push ->
            this.Remote
        
    let getLocalExecutorOf (command: ICommand<'a>) (this: ExecutorService) =
        this.Local.GetExecutor command
        
    let getRemoteExecutorOf (command: ICommand<'a>) (this: ExecutorService) =
        this.Remote.GetExecutor command
        
    let getSourceExecutorOf (command: ICommand<'a>) (this: ExecutorService) =
        this
        |> getSource
        |> fun source -> source.GetExecutor command
        
    let getDestinationExecutorOf (command: ICommand<'a>) (this: ExecutorService) =
        this
        |> getDestination
        |> fun source -> source.GetExecutor command              