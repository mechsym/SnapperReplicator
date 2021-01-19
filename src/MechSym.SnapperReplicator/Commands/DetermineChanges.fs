module MechSym.SnapperReplicator.Commands.DetermineChanges

open MechSym.Executor
open MechSym.SnapperReplicator.ShellCommand
open MechSym.SnapperReplicator.Types
open MechSym.SnapperReplicator.SnapperCommand
open Thoth.Json.Net

open MechSym.ControlFlow
open MechSym.ControlFlow.Operators
open MechSym.SnapperReplicator.Snapper
open MechSym.SnapperReplicator.Snapper.Serialization
open MechSym.SnapperReplicator.RuntimeConfiguration
open MechSym.SnapperReplicator.ReplicationBatch
open MechSym.SnapperReplicator.ReplicationBatch.Serialization

type GetSnapperStateError =
    | ListConfigsError of CommandExecutionError
    | ParseListConfigsError of string
    | ListError of CommandExecutionError
    | ParseListError of string
    
module GetSnapperStateError =
    let toMessage: GetSnapperStateError -> string = function
        | ListConfigsError err ->
            sprintf "Cannot list snapper configs: %s" (err |> CommandExecutionError.toMessage)
        | ParseListConfigsError err ->
            sprintf "Cannot parse snapper config list output: %s" err
        | ListError err ->
            sprintf "Cannot list snapper snapshots: %s" (err |> CommandExecutionError.toMessage)
        | ParseListError err ->
            sprintf "Cannot parse snapper list output: %s" err
        

let getSnapperState (configName: ConfigName)
                            (executeSnapper: SnapperCommand -> Result<string, CommandExecutionError>)
                            : Result<SnapperConfigState, GetSnapperStateError> =

    let configResult: Result<Config, GetSnapperStateError> =
        SnapperCommand.ListConfigs
        |> executeSnapper
        |> Result.mapError ListConfigsError
        >>=
            (Config.parseSnapperListConfigsOutput configName
             >> Result.mapError ParseListConfigsError)

    let snapshotsResult: Result<Snapshot list, GetSnapperStateError> =
        SnapperCommand.List(Some configName)
        |> executeSnapper
        |> Result.mapError ListError
        >>=
            (Snapshot.parseSnapperListOutput configName
             >> Result.mapError ParseListError)

    configResult
    >>= (fun config ->
        snapshotsResult
        |> Result.map (fun snapshots ->
            { SnapperConfigState.Config = config
              Snapshots = snapshots }))


type DetermineChangesError =
    | GetSourceSnapperStateError of GetSnapperStateError
    | GetDestinationSnapperStateError of GetSnapperStateError
    | CannotCreatePendingChangesFileError of CommandExecutionError

module DetermineChangesError =
    let toMessage: DetermineChangesError -> string = function
        | GetSourceSnapperStateError err ->
            sprintf "Determine changes failed. Cannot determine source state: %s" (err |> GetSnapperStateError.toMessage)
        | GetDestinationSnapperStateError err ->
            sprintf "Determine changes failed. Cannot determine destination state: %s" (err |> GetSnapperStateError.toMessage)
        | CannotCreatePendingChangesFileError err ->
            sprintf "Determine changes failed. Cannot create pending changes file: %s" (err |> CommandExecutionError.toMessage)

let execute (executor: ExecutorService) (config: RuntimeConfiguration): Result<unit, DetermineChangesError> =
    printfn "Determining changes..."

    let source =
        executor
        |> ExecutorService.getSourceExecutorOf SnapperCommand.command

    let destination =
        executor
        |> ExecutorService.getDestinationExecutorOf SnapperCommand.command
        
    let local =
        executor
        |> ExecutorService.getLocalExecutorOf ShellCommand.command

    getSnapperState config.SourceConfig source
    |> Result.mapError GetSourceSnapperStateError
    >>= (fun sourceState ->
        getSnapperState config.DestinationConfig destination
        |> Result.mapError GetDestinationSnapperStateError
        |> Result.map (ReplicationBatch.createBatch config.MaximumBatchSize sourceState))
    >>= (fun replicationBatch ->
        let serializedBatch =
            replicationBatch
            |> ReplicationBatch.toJson
            |> Encode.toString 2
        let pendingChangesPath = config |> RuntimeConfiguration.pendingChangesJson
        ShellCommand.Tee(serializedBatch, pendingChangesPath)
        |> local
        |> Result.mapError CannotCreatePendingChangesFileError)
    |> Result.ignore

type ParsePendingChangesError =
    | CannotRead of CommandExecutionError
    | CannotParse of string

module ParsePendingChangesError =
    let toMessage: ParsePendingChangesError -> string = function
        | CannotParse err -> sprintf "Cannot parse pending changes: %s" err
        | CannotRead err -> sprintf "Cannot read pending changes file: %s" (err |> CommandExecutionError.toMessage)

let parsePendingChanges (executor: ExecutorService) (config: RuntimeConfiguration): Result<ReplicationBatch, ParsePendingChangesError> =
    let executeShell = executor |> ExecutorService.getLocalExecutorOf ShellCommand.command
    config
    |> RuntimeConfiguration.pendingChangesJson
    |> ShellCommand.Cat
    |> executeShell
    |> Result.mapError CannotRead
    >>= (Decode.fromString ReplicationBatch.fromJson
         >> Result.mapError CannotParse)