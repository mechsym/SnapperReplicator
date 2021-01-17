namespace MechSym.SnapperReplicator.BtrfsCommand

open MechSym.SnapperReplicator.Types
open MechSym.SnapperReplicator.Snapper

[<RequireQualifiedAccess>]
type BtrfsCommand =
    | Send of subject: Snapshot * maybeParent: Snapshot option * targetFile: string option
    | Receive of subject: Snapshot * file: string

module BtrfsCommand =
    let private getParameters: BtrfsCommand -> string list =
        function
        | BtrfsCommand.Send (subject, maybeParent, maybeTargetFile) ->
            [ "send"

              match maybeParent with
              | Some parent ->
                  "-p"
                  parent |> Snapshot.absolutePath
              | None -> ()

              match maybeTargetFile with
              | Some target ->
                  "-f"
                  target
              | None -> ()

              subject |> Snapshot.absolutePath ]

        | BtrfsCommand.Receive (subject, file) ->
            [ "receive"
              "-f"
              file

              subject |> Snapshot.absoluteParentPath ]

    let private getStdin (_: BtrfsCommand): byte [] option = None

    let executable = "btrfs"

    let command =
        { new ICommand<BtrfsCommand> with
            member this.GetParameters command = getParameters command
            member this.GetExecutable command = executable
            member this.GetStdin command = getStdin command }
