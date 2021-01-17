namespace MechSym.SnapperReplicator.RuntimeConfiguration

open System.IO
open MechSym.SnapperReplicator.Types
open MechSym.SnapperReplicator.Snapper

type RuntimeConfiguration =
    { SourceConfig: ConfigName
      DestinationConfig: ConfigName
      DestinationWorkingDir: string
      SourceWorkingDir: string
      MaximumBatchSize: int
      OperationMode: OperationMode }

module RuntimeConfiguration =
    let getLocalWorkDir (this: RuntimeConfiguration) =
        match this.OperationMode with
        | OperationMode.Pull -> this.DestinationWorkingDir
        | OperationMode.Push -> this.SourceWorkingDir

    let getRemoteWorkDir (this: RuntimeConfiguration) =
        match this.OperationMode with
        | OperationMode.Pull -> this.SourceWorkingDir
        | OperationMode.Push -> this.DestinationWorkingDir
        
    let getDestinationConfigWorkDir (this: RuntimeConfiguration) =
        Path.Join(this.DestinationWorkingDir, this.DestinationConfig.Value)
        
    let getSourceConfigWorkDir (this: RuntimeConfiguration) =
        Path.Join(this.SourceWorkingDir, this.SourceConfig.Value)
        
    let getLocalConfigWorkDir (this: RuntimeConfiguration) =
        match this.OperationMode with
        | OperationMode.Pull -> this |> getDestinationConfigWorkDir
        | OperationMode.Push -> this |> getSourceConfigWorkDir

    let getRemoteConfigWorkDir (this: RuntimeConfiguration) =
        match this.OperationMode with
        | OperationMode.Pull -> this |> getSourceConfigWorkDir
        | OperationMode.Push -> this |> getDestinationConfigWorkDir
            
    let pendingChangesJson (this : RuntimeConfiguration) =
        Path.Join(this |> getLocalConfigWorkDir, "pending_changes.json")
        
    let dumpDoneFile (this: RuntimeConfiguration) =
        Path.Join(this |> getLocalConfigWorkDir, "dump.done")
        
    let syncDoneFile (this: RuntimeConfiguration) =
        Path.Join(this |> getLocalConfigWorkDir, "sync.done")
        
    let restoreDoneFile (this: RuntimeConfiguration) =
        Path.Join(this |> getLocalConfigWorkDir, "restore.done")
        
    let snapperCleanupDoneFile (this: RuntimeConfiguration) =
        Path.Join(this |> getLocalConfigWorkDir, "snapper-cleanup.done")
        
    let workDirsDoneFile (this: RuntimeConfiguration) =
        Path.Join(this |> getLocalConfigWorkDir, "workdirs.done")        