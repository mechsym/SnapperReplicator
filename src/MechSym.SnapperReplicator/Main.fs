open System
open Argu
open Fake.Core
open MechSym.FileTransfer
open Renci.SshNet

open MechSym.ControlFlow
open MechSym.Executor
open MechSym.SnapperReplicator.Types
open MechSym.SnapperReplicator.Snapper
open MechSym.SnapperReplicator.RuntimeConfiguration
open MechSym.SnapperReplicator.Commands
open MechSym.SnapperReplicator.CLI

[<Literal>]
let DefaultWorkDir = "/tmp/snapper-replicator"

[<Literal>]
let DefaultConfig = "root"

let DefaultBatchSize = Int32.MaxValue

let DefaultUser = Environment.UserName

let DefaultDumpMode = DumpType.Incremental

let DefaultTransferMode = TransferMode.Sftp

let createRuntimeConfiguration (parseResult: ParseResults<CLI>): RuntimeConfiguration =
    let operationMode = parseResult.GetResult Mode

    let localConfig =
        parseResult.TryGetResult Local_Config
        |> Option.defaultValue DefaultConfig
        |> ConfigName

    let remoteConfig =
        parseResult.TryGetResult Remote_Config
        |> Option.defaultValue DefaultConfig
        |> ConfigName
        
    let sourceConfig, destinationConfig =
        match operationMode with
        | Pull ->
            remoteConfig, localConfig
        | Push ->
            localConfig, remoteConfig

    let localWorkDir =
        parseResult.TryGetResult Local_Working_Directory
        |> Option.defaultValue DefaultWorkDir

    let remoteWorkDir =
        parseResult.TryGetResult Remote_Working_Directory
        |> Option.defaultValue DefaultWorkDir

    let sourceWorkDir, destinationWorkDir =
        match operationMode with
        | Pull ->
            remoteWorkDir, localWorkDir
        | Push ->
            localWorkDir, remoteWorkDir
    
    let batchSize =
        parseResult.TryGetResult Batch_Size
        |> Option.defaultValue DefaultBatchSize

    { RuntimeConfiguration.SourceConfig = sourceConfig
      DestinationConfig = destinationConfig
      DestinationWorkDir = destinationWorkDir
      OperationMode = operationMode
      SourceWorkDir = sourceWorkDir
      MaximumBatchSize = batchSize }

type ApplicationError =
    | CreateWorkDirsError of CreateWorkDirs.CreateWorkDirsError
    | DetermineChangesError of DetermineChanges.DetermineChangesError
    | DumpError of Dump.DumpError
    | SynchronizeError of Synchronize.SynchronizationError
    | RestoreError of Restore.RestoreError
    | CleanRemoteWorkDirError of CleanRemoteWorkDir.CleanRemoteWorkDirError
    | CleanLocalWorkDirError of CleanLocalWorkDir.CleanLocalWorkDirError
    | SnapperCleanSourceSnapshotsError of SnapperCleanSourceSnapshots.SnapperCleanSourceSnapshotsError

module ApplicationError =
    let toMessage (verbose: bool) (this: ApplicationError): string =
        if verbose then
            sprintf "%A" this
        else
            match this with
            | CreateWorkDirsError err ->
                err
                |> CreateWorkDirs.CreateWorkDirsError.toMessage
            | DetermineChangesError err ->
                err
                |> DetermineChanges.DetermineChangesError.toMessage
            | DumpError err -> err |> Dump.DumpError.toMessage
            | SynchronizeError err -> err |> Synchronize.SynchronizationError.toMessage
            | RestoreError err -> err |> Restore.RestoreError.toMessage
            | CleanRemoteWorkDirError err ->
                err
                |> CleanRemoteWorkDir.CleanRemoteWorkDirError.toMessage
            | CleanLocalWorkDirError err ->
                err
                |> CleanLocalWorkDir.CleanLocalWorkDirError.toMessage
            | SnapperCleanSourceSnapshotsError err ->
                err
                |> SnapperCleanSourceSnapshots.SnapperCleanSourceSnapshotsError.toMessage

[<EntryPoint>]
let main argv =
    
    let parser =
        ArgumentParser.Create<CLI>(programName = "snapper-replicator.exe")

    try
        let parseResult =
            parser.ParseCommandLine(inputs = argv, raiseOnUsage = true)

        match parseResult.Contains Version with
        | true ->
            printfn "snapper-replicator:{%s}:{%s}" AssemblyVersionInformation.AssemblyVersion AssemblyVersionInformation.AssemblyInformationalVersion
            0
        | false ->
            let host = parseResult.GetResult Host

            let user =
                parseResult.TryGetResult User
                |> Option.defaultValue DefaultUser

            let key = parseResult.GetResult Key

            let remoteConnectionInfo =
                ConnectionInfo(host, user, new PrivateKeyAuthenticationMethod(user, new PrivateKeyFile(key)))

            let runtimeConfig =
                parseResult |> createRuntimeConfiguration
            
            let verbose =
                parseResult.TryGetResult Verbose |> Option.isSome

            use localExecutor = Executor.local verbose

            use remoteExecutor =
                Executor.remote remoteConnectionInfo verbose

            let executorService =
                { ExecutorService.Local = localExecutor
                  Remote = remoteExecutor
                  Mode = runtimeConfig.OperationMode }

            match parseResult.GetSubCommand() with
            | Create_WorkDirs _ ->
                CreateWorkDirs.execute executorService runtimeConfig
                |> Result.mapError CreateWorkDirsError

            | Determine_Changes _ ->
                DetermineChanges.execute executorService runtimeConfig
                |> Result.mapError DetermineChangesError

            | Dump parameters ->
                let preferredDumpType =
                    parameters.TryGetResult Preferred_Mode
                    |> Option.defaultValue DefaultDumpMode

                Dump.execute executorService runtimeConfig preferredDumpType
                |> Result.mapError DumpError

            | Synchronize parameters ->
                let transferMode =
                    parameters.TryGetResult Transfer_Mode
                    |> Option.defaultValue DefaultTransferMode

                use transferService =
                    match transferMode with
                    | Sftp ->
                        let client = new SftpClient(remoteConnectionInfo)
                        client.Connect()
                        FileTransferService.sftpService client
                    | Rsync ->
                        FileTransferService.rsyncService runtimeConfig.OperationMode host user key executorService.Local

                Synchronize.execute transferService executorService runtimeConfig
                |> Result.mapError SynchronizeError

            | Restore _ ->
                Restore.execute executorService runtimeConfig
                |> Result.mapError RestoreError

            | Clean_Remote_WorkDir _ ->
                CleanRemoteWorkDir.execute executorService runtimeConfig
                |> Result.mapError CleanRemoteWorkDirError

            | Clean_Local_WorkDir _ ->
                CleanLocalWorkDir.execute executorService runtimeConfig
                |> Result.mapError CleanLocalWorkDirError

            | Snapper_CleanUp_Source parameters ->
                let algorithm = parameters.GetResult Algorithm

                SnapperCleanSourceSnapshots.execute algorithm executorService runtimeConfig
                |> Result.mapError SnapperCleanSourceSnapshotsError

            | x -> failwithf "illegal subcommand: %A" x

            |> Result.raiseOnError (ApplicationError.toMessage verbose)

            0
    with e ->
        printfn "%s" e.Message
        -1
