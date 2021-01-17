namespace MechSym.FileTransfer

open System.IO
open MechSym.ControlFlow
open MechSym.SnapperReplicator.Types
open MechSym.SnapperReplicator.ShellCommand
open Renci.SshNet

type FileTransferError =
    | DownloadException of exn
    | UploadException of exn
    | RsyncError of CommandExecutionError

module FileTransferError =
    let toMessage =
        function
        | DownloadException ex -> sprintf "Error in download: %s" ex.Message
        | UploadException ex -> sprintf "Error in upload: %s" ex.Message
        | RsyncError err -> sprintf "Rsync error: %A" err

type IFileTransferService =
    inherit System.IDisposable
    abstract download: sourceFiles:string list -> targetDir:string -> Result<unit, FileTransferError>

    abstract upload: sourceFiles:string list -> targetDir:string -> Result<unit, FileTransferError>

module FileTransferService =

    module private SftpService =
        let download (client: SftpClient) (sourceFiles: string list) (targetDir: string): Result<unit, FileTransferError> =
            try
                for sourceFile in sourceFiles do
                    let fileName = FileInfo(sourceFile).Name
                    printfn "Downloading: %s" fileName
                    let targetPath = Path.Join(targetDir, fileName)
                    use output = File.OpenWrite(targetPath)
                    client.DownloadFile(sourceFile, output)

                Ok()
            with e -> Error(DownloadException e)

        let upload (client: SftpClient) (sourceFiles: string list) (targetDir: string): Result<unit, FileTransferError> =
            try
                for sourceFile in sourceFiles do
                    use input = File.OpenRead(sourceFile)
                    let sourceFileName = FileInfo(sourceFile).Name
                    printfn "Uploading: %s" sourceFileName
                    let targetPath = Path.Join(targetDir, sourceFileName)
                    client.UploadFile(input, targetPath)

                Ok()
            with e -> Error(UploadException e)

    let sftpService (client: SftpClient) =
        { new IFileTransferService with
            member __.download (sourceFiles: string list) (targetDir: string): Result<unit, FileTransferError> =
                SftpService.download client sourceFiles targetDir

            member __.upload (sourceFiles: string list) (targetDir: string): Result<unit, FileTransferError> =
                SftpService.upload client sourceFiles targetDir

            member __.Dispose() = client.Dispose() }

    module private RsyncFileTransferService =
        let download (mode: OperationMode)
                     (host: string)
                     (user: string)
                     (keyFile: string)
                     (localExecutor: IExecutor)
                     (sourceFiles: string list)
                     (targetDir: string)
                     : Result<unit, FileTransferError> =
            let executeShell =
                localExecutor.GetExecutor ShellCommand.command

            ShellCommand.Rsync(mode, sourceFiles, targetDir, host, user, keyFile)
            |> executeShell
            |> Result.mapError RsyncError
            |> Result.ignore


        let upload = download

    let rsyncService (mode: OperationMode) (host: string) (user: string) (keyFile: string) (localExecutor: IExecutor) =
        { new IFileTransferService with
            member __.download (sourceFiles: string list) (targetDir: string): Result<unit, FileTransferError> =
                RsyncFileTransferService.download mode host user keyFile localExecutor sourceFiles targetDir

            member __.upload (sourceFiles: string list) (targetDir: string): Result<unit, FileTransferError> =
                RsyncFileTransferService.upload mode host user keyFile localExecutor sourceFiles targetDir

            member __.Dispose() = () }
