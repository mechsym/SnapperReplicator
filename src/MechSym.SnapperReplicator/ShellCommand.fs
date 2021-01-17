namespace MechSym.SnapperReplicator.ShellCommand

open System.IO
open System.Text
open MechSym.SnapperReplicator.Types

[<RequireQualifiedAccess>]
type ShellCommand =
    | CreateDir of path: string
    | Rsync of mode: OperationMode * srcs: string list * dest: string * host: string * user: string * keyFile: string
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
        | ShellCommand.Rsync (mode, sources, dest, host, user, keyFile) ->
            let sourceFileGroups =
                sources
                |> List.groupBy (fun file -> FileInfo(file).DirectoryName)
                |> List.map (fun (dirName, files) ->
                    dirName,
                    files
                    |> List.map (fun file -> FileInfo(file).Name))
                |> List.map (fun (dirName, fileNames) -> sprintf "%s/{%s}" dirName (fileNames |> String.concat ","))

            [ "-a" // archive
              "-P" // progress
              "-v" // verbose
              "-e" // custom shell
              sprintf "\"ssh -i %s\"" keyFile

              match mode with
              | OperationMode.Pull ->
                  for sourceFileGroup in sourceFileGroups do
                      sprintf "%s@%s:%s" user host sourceFileGroup
              | OperationMode.Push ->
                  for sourceFileGroup in sourceFileGroups do
                      sprintf "%s" sourceFileGroup

              match mode with
              | OperationMode.Pull -> dest
              | OperationMode.Push -> sprintf "%s@%s:%s" user host dest ]

        | ShellCommand.Touch path
        | ShellCommand.Cat path -> [ path ]
        | ShellCommand.Remove path -> [ "-r"; "-f"; path ]
        | ShellCommand.Copy (source, destination) -> [ source; destination ]
        | ShellCommand.Tee (_content, file) -> [ file ]

    let private getStdin: ShellCommand -> byte [] option = function
        | ShellCommand.Tee (content, _file) ->
            content |> Encoding.UTF8.GetBytes |> Some
        | _ -> None

    let command =
        { new ICommand<ShellCommand> with
            member this.GetParameters(command: ShellCommand) = getParameters command
            member this.GetExecutable(command: ShellCommand) = getCommand command
            member this.GetStdin(command: ShellCommand) = getStdin command }
