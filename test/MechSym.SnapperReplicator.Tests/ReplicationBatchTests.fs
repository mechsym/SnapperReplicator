module MechSym.SnapperReplicator.Tests.ReplicationBatchTests

open System
open System.Collections.Generic
open MechSym.SnapperReplicator.ReplicationBatch
open MechSym.SnapperReplicator.ReplicationRequest
open MechSym.SnapperReplicator.Snapper
open Xunit

type ExpectedSnapshot =
    | Full of SnapshotNumber
    | Incremental of parent: SnapshotNumber * child: SnapshotNumber

let createTestSnapperState (snapshotNumbers: int seq) (config: Config): SnapperConfigState =
    { SnapperConfigState.Config = config
      Snapshots =
          [ for snapshotNumber in snapshotNumbers do
              { Snapshot.Number = SnapshotNumber snapshotNumber
                SubVolume = config.SubVolume
                Date = Some(DateTimeOffset.FromUnixTimeSeconds(int64 snapshotNumber)) } ] }

type ReplicationBatchTestData() =
    static member SourceConfig with get() =
        { Config.Name = ConfigName "source"
          SubVolume = SubVolume "/sourceVolume" }    

    static member DestinationConfig with get() =
        { Config.Name = ConfigName "destination"
          SubVolume = SubVolume "/destinationVolume" }    
        
    member this.Enum(): IEnumerator<obj []> =
        (seq {
            // No snapshots, no work
            yield [|
                Int32.MaxValue |> box
                ReplicationBatchTestData.SourceConfig |> createTestSnapperState [] :> obj
                ReplicationBatchTestData.DestinationConfig |> createTestSnapperState [] :> obj
                ([]: ExpectedSnapshot list) :> obj
            |]

            // Identical states, no work
            yield [|
                Int32.MaxValue |> box
                ReplicationBatchTestData.SourceConfig |> createTestSnapperState [1; 2; 3] :> obj
                ReplicationBatchTestData.DestinationConfig |> createTestSnapperState [1; 2; 3] :> obj
                ([]: ExpectedSnapshot list) :> obj
            |]
            
            // Empty destination, source gets replicated
            yield [|
                Int32.MaxValue |> box
                ReplicationBatchTestData.SourceConfig |> createTestSnapperState [1; 2; 3] :> obj
                ReplicationBatchTestData.DestinationConfig |> createTestSnapperState [] :> obj
                [ Full (SnapshotNumber 1)
                  Incremental (SnapshotNumber 1, SnapshotNumber 2)
                  Incremental (SnapshotNumber 2, SnapshotNumber 3) ] :> obj
            |]
            
            // Destination is behind source, replicate the difference
            yield [|
                Int32.MaxValue |> box
                ReplicationBatchTestData.SourceConfig |> createTestSnapperState [1; 2; 3] :> obj
                ReplicationBatchTestData.DestinationConfig |> createTestSnapperState [1] :> obj
                [ Incremental (SnapshotNumber 1, SnapshotNumber 2)
                  Incremental (SnapshotNumber 2, SnapshotNumber 3) ] :> obj
            |]
            
            // Has no common snapshots, starting with full
            yield [|
                Int32.MaxValue |> box
                ReplicationBatchTestData.SourceConfig |> createTestSnapperState [ 2; 3] :> obj
                ReplicationBatchTestData.DestinationConfig |> createTestSnapperState [1] :> obj
                [ Full (SnapshotNumber 2)
                  Incremental (SnapshotNumber 2, SnapshotNumber 3) ] :> obj
            |]
            
            // Already replicated snapshot gets cleaned up in source, infers increment correctly
            yield [|
                Int32.MaxValue |> box
                ReplicationBatchTestData.SourceConfig |> createTestSnapperState [ 3; 5; 6] :> obj
                ReplicationBatchTestData.DestinationConfig |> createTestSnapperState [1; 2; 3; 4] :> obj
                [ Incremental (SnapshotNumber 3, SnapshotNumber 5)
                  Incremental (SnapshotNumber 5, SnapshotNumber 6) ] :> obj
            |]              
        }
        |> Seq.map (Array.map box)).GetEnumerator()
    
    interface IEnumerable<obj []> with
        member this.GetEnumerator(): IEnumerator<obj []> = this.Enum()
        member this.GetEnumerator(): Collections.IEnumerator = this.Enum() :> System.Collections.IEnumerator


[<Theory>]
[<ClassData(typeof<ReplicationBatchTestData>)>]
let ``Test RequestBatch.createBatch computes plan correctly`` (batchSize: int, source: SnapperConfigState, destination: SnapperConfigState, expectedRequests: ExpectedSnapshot list) =
    
    let batch =
        ReplicationBatch.createBatch batchSize source destination
        
    List.zip
        expectedRequests
        batch.Requests
    |> List.iter
        (fun (expectedRequest, actualRequest) ->
        match expectedRequest, actualRequest with
        | Full expectedNum, ReplicationRequest.Full actualRequest ->
            Assert.Equal(expectedNum, actualRequest.Subject.Number)
        | Incremental (expectedParent, expectedSubject), ReplicationRequest.Incremental actualRequest ->
            Assert.Equal(expectedParent, actualRequest.Parent.Number)
            Assert.Equal(expectedSubject, actualRequest.Subject.Number)
        | exp, act ->
            Assert.True(false, sprintf "Expected is different than actual: %A vs %s" exp (act.ToString())))
        
[<Theory>]
[<ClassData(typeof<ReplicationBatchTestData>)>]
let ``Test RequestBatch.createBatch replicates only source snapshots`` (batchSize: int, source: SnapperConfigState, destination: SnapperConfigState, expectedRequests: ExpectedSnapshot list) =
    let batch = 
        ReplicationBatch.createBatch batchSize source destination
    batch.Requests
    |> List.iter (function
                  | ReplicationRequest.Full request -> Assert.Equal(ReplicationBatchTestData.SourceConfig.SubVolume, request.Subject.SubVolume)
                  | ReplicationRequest.Incremental request ->
                      Assert.Equal(ReplicationBatchTestData.SourceConfig.SubVolume, request.Subject.SubVolume)
                      Assert.Equal(ReplicationBatchTestData.SourceConfig.SubVolume, request.Parent.SubVolume))
    