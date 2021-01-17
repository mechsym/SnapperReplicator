module MechSym.SnapperReplicator.CLI

open Argu
open MechSym.SnapperReplicator.Types
open MechSym.SnapperReplicator.SnapperCommand

type EmptyArgs =
    | [<Hidden>] EmptyArgs
    interface IArgParserTemplate with
        member this.Usage =
            "Foo"

type CleanRemoteSnapshotsArgs =
    | [<Mandatory; AltCommandLine("-alg")>] Algorithm of SnapperAlgorithm
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Algorithm _ -> "Snapper algorithm to use for cleanup <timeline|number>"

type SynchronizeArgs =
    | [<AltCommandLine("-m")>] Transfer_Mode of TransferMode
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Transfer_Mode _ -> "Transfer mode to use"
            
type DumpArgs =
    | [<AltCommandLine("-m")>] Preferred_Mode of DumpType
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Preferred_Mode _ -> "prefer full snapshots or incremental ones"

type CLI =
    | [<CliPrefix(CliPrefix.None)>] Determine_Changes of ParseResults<EmptyArgs>
    | [<CliPrefix(CliPrefix.None)>] Create_WorkDirs of ParseResults<EmptyArgs>
    | [<CliPrefix(CliPrefix.None)>] Dump of ParseResults<DumpArgs>
    | [<CliPrefix(CliPrefix.None)>] Synchronize of ParseResults<SynchronizeArgs>
    | [<CliPrefix(CliPrefix.None)>] Restore of ParseResults<EmptyArgs>
    | [<CliPrefix(CliPrefix.None)>] Clean_Remote_WorkDir of ParseResults<EmptyArgs>
    | [<CliPrefix(CliPrefix.None)>] Clean_Local_WorkDir of ParseResults<EmptyArgs>
    | [<CliPrefix(CliPrefix.None)>] Snapper_CleanUp_Source of ParseResults<CleanRemoteSnapshotsArgs>
    | [<AltCommandLine("-h"); Mandatory>] Host of hostname: string
    | [<AltCommandLine("-m"); Mandatory>] Mode of OperationMode
    | [<AltCommandLine("-k"); Mandatory>] Key of path: string
    
    | [<AltCommandLine("-u")>] User of username: string
    | [<AltCommandLine("-rwd")>] Remote_Working_Directory of path: string
    | [<AltCommandLine("-lwd")>] Local_Working_Directory of path: string
    | [<AltCommandLine("-rc")>] Remote_Config of configName: string
    | [<AltCommandLine("-lc")>] Local_Config of configName: string
    | [<AltCommandLine("-bs")>] Batch_Size of size: int
    | [<AltCommandLine("-v")>] Verbose

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Determine_Changes _ -> "determine-changes: Determine difference between local and remote"
            | Synchronize _ -> "Pull snapshots from remote"
            | Dump _ -> "Dump snapshots on source"
            | Restore _ -> "Restore snapshots on destination"
            | Clean_Local_WorkDir _ -> "Clean up local working directory"
            | Clean_Remote_WorkDir _ -> "Clean up remote working directory"
            | Snapper_CleanUp_Source _ -> "Runs `snapper cleanup` on source with the specified cleanup algorithm"
            | User _ -> "User name on remote host"
            | Create_WorkDirs _ -> "Create local and remote workdirs"
            | Key _ -> "Path to ssh key"
            | Host _ -> "Remote host"
            | Remote_Config _ -> "Name of snapper config on remote"
            | Local_Config _ -> "Name of snapper config locally"
            | Local_Working_Directory _ -> "Path to the local working directory"
            | Remote_Working_Directory _ -> "Path to the remote working directory"
            | Batch_Size _ -> "Maximum size of a replication batch"
            | Verbose _ -> "Verbose mode"
            | Mode _ -> "Operation mode: whether to pull or push snapshots"
