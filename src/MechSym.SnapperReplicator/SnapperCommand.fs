namespace MechSym.SnapperReplicator.SnapperCommand

open MechSym.SnapperReplicator.Types
open MechSym.SnapperReplicator.Snapper

type SnapperAlgorithm =
    | Timeline = 1
    | Number = 2

[<RequireQualifiedAccess>]
type SnapperCommand =
    | ListConfigs
    | List of ConfigName option
    | Cleanup of ConfigName option * SnapperAlgorithm

module SnapperCommand =
    let private getParameters: SnapperCommand -> string list =
        function
        | SnapperCommand.ListConfigs -> [ "--jsonout"; "list-configs" ]
        | SnapperCommand.List maybeConfig ->
            [ "--jsonout"

              match maybeConfig with
              | Some config ->
                  "-c"
                  config.Value
              | None -> ()

              "list"
              "--columns"
              "subvolume,number,date" ]
        | SnapperCommand.Cleanup (maybeConfig, algorithm) ->
            [ match maybeConfig with
              | Some config ->
                  "-c"
                  config.Value
              | None -> ()
              
              "cleanup"
              
              match algorithm with
              | SnapperAlgorithm.Timeline -> "timeline"
              | SnapperAlgorithm.Number -> "number"
              | x -> failwithf "Illegal snapper algorithm %A" x]

    let private getStdin (_: SnapperCommand): byte [] option = None
    
    let private commandPath = "snapper"
    
    let command =
        { new ICommand<SnapperCommand> with
            member this.GetExecutable _ = commandPath
            
            member this.GetParameters command = getParameters command
            
            member this.GetStdin command = getStdin command }
    
    
