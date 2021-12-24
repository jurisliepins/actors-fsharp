namespace Actors.FSharp

open System
open System.Collections
open System.Collections.Generic
open System.Collections.Concurrent
open Akka.Actor
open Akka.Util

module AkkaCoordinator =
    open Akka.FSharp
    
    type CoordinatorMessage<'a> =
        | Request   of 'a
        | Processed of 'a
        
    type WorkerMessage<'a> =
        | Response of 'a option
    
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
        
        let coordinator = 
            spawnOpt system coordinatorName
                (fun (mailbox: Actor<CoordinatorMessage<int>>) ->
                    let rec iterate (incomplete: ConcurrentBag<_>) (processing: ConcurrentSet<_>) = actor {
                        match! mailbox.Receive() with
                        | Request request ->
                            logDebugf mailbox $"Request from %A{request}"
                            match request with
                            | _ when incomplete.IsEmpty ->
                                logDebugf mailbox "Incomplete collection empty - taking from processing items"
                                mailbox.Sender() <! Response (Seq.tryHead processing)
                            | _ ->
                                logDebugf mailbox "Taking from incomplete collection"
                                match incomplete.TryTake() with
                                | true, value ->
                                    match processing.TryAdd value with
                                    | true -> 
                                        mailbox.Sender() <! Response (Some value)
                                    | false ->
                                        failwith "Failed to add work to the processing collection"
                                | _ ->
                                    mailbox.Sender() <! Response None
                        | Processed value ->
                            logDebugf mailbox $"%A{value} has been processed"
                            processing.TryRemove value |> ignore
                        return! iterate incomplete processing                       
                    }
                    iterate (ConcurrentBag<_>([| for idx in 1..10 do yield idx |])) (ConcurrentSet<_>()))
                [ SpawnOption.SupervisorStrategy (Strategy.OneForOne (fun _ -> Directive.Stop)) ]    
        let workers =
            [for idx in 1..5 do
                spawnOpt system (workerName idx)
                    (fun (mailbox: Actor<WorkerMessage<int>>) ->
                        let rec iterate () = actor {
                            match! mailbox.Receive() with
                            | Response response ->
                                match response with
                                | Some value ->
                                    logDebugf mailbox $"Processing value %A{value}"
                                    Async.Sleep 5000 |> Async.RunSynchronously
                                    coordinator <! Processed value
                                    coordinator <! Request idx
                                | None ->
                                    logDebugf mailbox "Done"
                            return! iterate ()
                        }
                        iterate (coordinator <! Request idx))
                    [ SpawnOption.SupervisorStrategy (Strategy.OneForOne (fun _ -> Directive.Stop)) ]
            ]
        
        Console.ReadKey() |> ignore
        0