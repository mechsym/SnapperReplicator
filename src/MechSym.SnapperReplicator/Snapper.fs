namespace MechSym.SnapperReplicator.Snapper

open System
open System.Diagnostics
open System.IO

type SubVolume =
    | SubVolume of string
    member this.Value =
        let (SubVolume value) = this
        value
        
module SubVolume =
    let value (SubVolume this) = this

type SnapshotNumber =
    | SnapshotNumber of int
    member this.Value =
        let (SnapshotNumber foo) = this
        foo

module SnapshotNumber =
    let value (SnapshotNumber this) = this

type ConfigName =
    | ConfigName of string
    member this.Value =
        let (ConfigName value) = this
        value

module ConfigName =
    let value (ConfigName this) = this

type Config =
    { SubVolume: SubVolume
      Name: ConfigName } with
    
    override this.ToString() = sprintf "%s: %s" this.Name.Value this.SubVolume.Value

type Snapshot =
    { SubVolume: SubVolume
      Number: SnapshotNumber
      Date: DateTimeOffset option } with
    override this.ToString() = sprintf "[%i]" this.Number.Value    
    

module Snapshot =
    let absoluteParentPath (this: Snapshot): string =
        Path.Join(this.SubVolume.Value, ".snapshots", sprintf "%i" this.Number.Value)

    let absolutePath (this: Snapshot): string =
        Path.Join(this |> absoluteParentPath, "snapshot")

    let infoXml (this: Snapshot): string =
        Path.Join(this.SubVolume.Value, ".snapshots", sprintf "%i" this.Number.Value, "info.xml")

    let convertTo (newConfig: Config) (this: Snapshot): Snapshot =
        { this with
              SubVolume = newConfig.SubVolume }
        
    let dumpSnapshotFileName (this: Snapshot): string = sprintf "%i.btrfs" this.Number.Value
    
    let dumpInfoFileName (this: Snapshot): string = sprintf "%i.info.xml" this.Number.Value

type SnapperConfigState =
    { Config: Config
      Snapshots: Snapshot list }
