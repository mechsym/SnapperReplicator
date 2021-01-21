#r "paket:
nuget FSharp.Core ~> 4
nuget Fake.DotNet.Cli
nuget Fake.IO.FileSystem
nuget Fake.Core.ReleaseNotes
nuget Fake.DotNet.AssemblyInfoFile
nuget Fake.DotNet.Testing.XUnit2
nuget Fake.IO.Zip
nuget Fake.Tools.Git
nuget Fake.Core.Target //"
#load ".fake/build.fsx/intellisense.fsx"
open Fake.Core
open Fake.IO
open Fake.DotNet
open Fake.Tools
open Fake.IO.Globbing.Operators
open Fake.Core.TargetOperators

Target.initEnvironment ()

Target.create "Clean" (fun _ ->
    !! "src/**/bin"
    ++ "publish"
    ++ "src/**/obj"
    |> Shell.cleanDirs 
)

Target.create "Build" (fun _ ->
    !! "src/**/*.*proj"
    |> Seq.iter (DotNet.build id)
)

let productName = "MechSym.SnapperReplicator"

let description = "CLI tool to replicate btrfs snapshots remotely"

let release = ReleaseNotes.load "RELEASE_NOTES.md"

Target.create "AssemblyInfo" (fun _ ->
    let attribs =
        [ AssemblyInfo.Title productName
          AssemblyInfo.Product productName
          AssemblyInfo.Description description
          AssemblyInfo.Version release.AssemblyVersion
          AssemblyInfo.InformationalVersion (Git.Information.getCurrentHash())
          AssemblyInfo.FileVersion release.AssemblyVersion ]

    AssemblyInfoFile.createFSharp "src/MechSym.SnapperReplicator/AssemblyInfo.fs" attribs    
)

Target.create "Test" (fun _ ->
    DotNet.test id "test/MechSym.SnapperReplicator.Tests/MechSym.SnapperReplicator.Tests.fsproj"
)

Target.create "Publish" (fun _ ->
    DotNet.publish (fun (options: DotNet.PublishOptions) ->
        let msbuildParams = MSBuild.CliArguments.Create()
        { options with
            Configuration = DotNet.BuildConfiguration.Release
            SelfContained = Some true
            Runtime = Some "linux-x64"
            MSBuildParams = { msbuildParams with Properties = ("PublishSingleFile", "true") :: msbuildParams.Properties }
            OutputPath = Some "publish" }) "src/MechSym.SnapperReplicator/MechSym.SnapperReplicator.fsproj")
    
Target.create "CopyDocuments" (fun _ ->

    [ "example/Makefile.example"
      "example/run.example.sh"
      "RELEASE_NOTES.md"
      "README.md" ]
    |> Shell.copy "publish")

Target.create "CreateZip" (fun _ ->    
    let fileName = sprintf "snapper-replicator-linux-x64-%s.zip" release.SemVer.AsString
    let workDir = "."
    let comment = "comment"
    let compressionLevel = 9
    let flatten = true
    !! "publish/**"
    |> Zip.createZip workDir fileName comment compressionLevel flatten)

    

Target.create "All" ignore

"Clean"
  ==> "AssemblyInfo"
  ==> "Build"
  ==> "Test"
  ==> "All"

"Clean"
  ==> "AssemblyInfo"
  ==> "Publish"  
  ==> "CopyDocuments"
  ==> "CreateZip"


Target.runOrDefault "All"
