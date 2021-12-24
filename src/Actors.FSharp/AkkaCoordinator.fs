namespace Actors.FSharp

open System
open System.Collections.Concurrent
open System.Collections.Generic
open Akka.Actor
open Akka.FSharp

module AkkaCoordinator =

    type CoordinatorMessage =
        | Request
        | Return    of KeyValuePair<int, int>
        | Processed of KeyValuePair<int, int>
        
    type WorkerMessage =
        | Response of KeyValuePair<int, int> option
    
    let run () =
        let baseName = "akka-coordinator-fsharp"
        let systemName = $"%s{baseName}-system"
        let coordinatorName = $"%s{baseName}-coordinator"
        let workerName idx = $"%s{baseName}-worker-%A{idx}"
        
        let config =
            @"akka {
                loglevel = ""DEBUG""
            }"

        let system = System.create systemName (Configuration.parse config)
        
        let rec values () =
        
            let complete = new BlockingCollection<_>(ConcurrentQueue())
            
            let coordinator = 
                spawnOpt system coordinatorName
                    (fun (mailbox: Actor<CoordinatorMessage>) ->
                        let rec iterate (incomplete: Queue<KeyValuePair<_, _>>) (processing: Dictionary<_, _>) = actor {
                            match! mailbox.Receive() with
                            | Request ->
    //                            logDebugf mailbox $"Request from %A{request}"
                                match incomplete with
                                | _ when incomplete.Count < 1 ->
    //                                logDebugf mailbox "Incomplete collection empty - taking from processing items"
                                    mailbox.Sender() <! Response (Seq.tryHead processing)
                                | _ ->
    //                                logDebugf mailbox "Taking from incomplete collection"
                                    let kv = incomplete.Dequeue()
                                    match processing.TryAdd (kv.Key, kv.Value) with
                                    | true ->
                                        ()
                                    | false ->
    //                                    logDebugf mailbox $"Work %A{kv.Key} is already in processing queue, which means processing it had failed previously"
                                        ()
                                    mailbox.Sender() <! Response (Some kv)
                            | Return kv ->
                                incomplete.Enqueue kv
                            | Processed kv ->
    //                            logDebugf mailbox $"%A{value} has been processed"
                                match processing.Remove kv.Key with
                                | true when
                                    incomplete.Count < 1 &&
                                    processing.Count < 1 ->
                                    complete.Add (Some kv)
                                    complete.Add (None)
                                | true when
                                    incomplete.Count > 0 ||
                                    processing.Count > 0 ->
                                    complete.Add (Some kv)
                                | _ ->
    //                                logDebugf mailbox $"Failed to remove %A{value} from the processing collection"
                                    ()
                            return! iterate incomplete processing                       
                        }
                        iterate (Queue<_>([| for idx in 1..100 do yield KeyValuePair(idx, idx) |])) (Dictionary<_, _>()))
                    [ SpawnOption.SupervisorStrategy (Strategy.OneForOne (fun _ -> Directive.Stop)) ]    
            
            let workers =
                [for idx in 1..10 do
                    spawnOpt system (workerName idx)
                        (fun (mailbox: Actor<WorkerMessage>) ->
                            let rec iterate () = actor {
                                match! mailbox.Receive() with
                                | Response response ->
                                    match response with
                                    | Some kv ->
    //                                    logDebugf mailbox $"Processing value %A{value}"
                                        match (Random().Next(0, 2)) with
                                        | value when value = 0 -> 
                                            Async.Sleep (Random().Next(1_000, 10_000)) |> Async.RunSynchronously
                                            coordinator <! Processed kv
                                            coordinator <! Request
                                        | value when value = 1 ->
                                            // Simulating a failure to process the work.
                                            Async.Sleep (Random().Next(5_000, 10_000)) |> Async.RunSynchronously
    //                                        logDebugf mailbox $"Processing %A{kv.Key} failed - sending it back to the coordinator"
                                            coordinator <! Return kv
                                            coordinator <! Request
                                        | _ ->
                                            ()
                                        return! iterate ()
                                    | None ->
    //                                  // Terminal state - we've received poison-pill so time to shut down.
    //                                    logDebugf mailbox $"Worker done"
                                        ()
                            }
                            iterate (coordinator <! Request))
                        [ SpawnOption.SupervisorStrategy (Strategy.OneForOne (fun _ -> Directive.Stop)) ]
                ]
                
            let rec take () = seq {
                match complete.Take() with
                | Some value ->
                    yield  value
                    yield! take ()
                | None ->
                    printfn "Take done"
            }
            take ()
            
        for value in values () do
            printfn $"Completed %A{value}"  
        
        Console.ReadKey() |> ignore
        0