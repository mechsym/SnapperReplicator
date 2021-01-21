namespace MechSym.SnapperReplicator.ShellCommand

open System
open System.Text
open MechSym.SnapperReplicator.Types

[<RequireQualifiedAccess>]
type ShellCommand =
    | CreateDir of path: string
    | Rsync of mode: OperationMode * srcDir: string * srcFiles: string list * dest: string * host: string * user: string * keyFile: string
    | Touch of path: string
    | Remove of path: string
    | Tee of content: string * file: string
    | Copy of source: string * destination: string
    | Cat of path: string

module ShellCommand =
    let private getCommand: ShellCommand -> string =
        function
        | ShellCommand.CreateDir _ -> "mkdir"
        | ShellCommand.Rsync _ -> "rsync"
        | ShellCommand.Touch _ -> "touch"
        | ShellCommand.Remove _ -> "rm"
        | ShellCommand.Copy _ -> "cp"
        | ShellCommand.Tee _ -> "tee"
        | ShellCommand.Cat _ -> "cat"


    let private getParameters: ShellCommand -> string list =
        function
        | ShellCommand.CreateDir path -> [ "-p"; path ]
        | ShellCommand.Rsync (mode, sourceDir, _sourceFileNames, destinationDir, host, user, keyFile) ->
            [ "-e" // custom shell
              sprintf "\"ssh -i %s\"" keyFile

              "--files-from=-" //list of src files should be read from stdin
              
              match mode with
              | OperationMode.Pull ->
                  sprintf "%s@%s:%s" user host sourceDir
              | OperationMode.Push ->
                  sprintf "%s" sourceDir

              match mode with
              | OperationMode.Pull ->
                  destinationDir
              | OperationMode.Push ->
                  sprintf "%s@%s:%s" user host destinationDir ]

        | ShellCommand.Touch path
        | ShellCommand.Cat path -> [ path ]
        | ShellCommand.Remove path -> [ "-r"; "-f"; path ]
        | ShellCommand.Copy (source, destination) -> [ source; destination ]
        | ShellCommand.Tee (_content, file) -> [ file ]

    let private getStdin: ShellCommand -> byte [] option = function
        | ShellCommand.Tee (content, _file) ->
            content |> Encoding.UTF8.GetBytes |> Some
        | ShellCommand.Rsync(_mode, _sourceDir, sourceFileNames, _destinationDir, _host, _user, _keyFile) ->
            sourceFileNames
            |> String.concat Environment.NewLine
            |> Encoding.UTF8.GetBytes
            |> Some
        | _ -> None

    let command =
        { new ICommand<ShellCommand> with
            member this.GetParameters(command: ShellCommand) = getParameters command
            member this.GetExecutable(command: ShellCommand) = getCommand command
            member this.GetStdin(command: ShellCommand) = getStdin command }
