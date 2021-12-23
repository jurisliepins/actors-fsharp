namespace Actors.FSharp

open System
open System.Collections.Concurrent
open Akka.Actor
open Akka.Util

module AkkaCoordinator =
    open Akka.FSharp
    
    type CoordinatorMessage<'a> =
        | Request   of 'a
        | Processed of 'a
        
    type WorkerMessage<'a> =
        | Response of 'a
        | ResponsePriority of 'a
    
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
                    let rec iterate (incomplete: ConcurrentBag<_>) (complete: ConcurrentBag<_>) = actor {
                        match! mailbox.Receive() with
                        | Request request ->
                            logDebugf mailbox $"Request %A{request}"
                            match request with
                            | _ when incomplete.Count < complete.Count ->
                                for value in incomplete do
                                    mailbox.Sender() <! ResponsePriority (Some value)
                            | _ -> 
                                match incomplete.TryTake() with
                                | true, value ->
                                    mailbox.Sender() <! Response (Some value)
                                | _ ->
                                    logDebugf mailbox $"Nothing to take"
                        | Processed value ->
                            logDebugf mailbox $"Processed %A{mailbox.Sender().Path} %A{value}"
                            complete.Add value
                        return! iterate incomplete complete                       
                    }
                    iterate (ConcurrentBag<_>([| for idx in 1..10 do yield idx |])) (ConcurrentBag<_>()))
                [ SpawnOption.SupervisorStrategy (Strategy.OneForOne (fun _ -> Directive.Stop)) ]    
        let workers =
            [for idx in 1..5 do
                spawnOpt system (workerName idx)
                    (fun (mailbox: Actor<WorkerMessage<int option>>) ->
                        // TODO: Figure out how to short-circuit when a different worker already processed the same work! 
                        let rec iterate () = actor {
                            match! mailbox.Receive() with
                            | Response response ->
                                match response with
                                | Some value ->
                                    logDebugf mailbox $"Response %A{value}"
                                    Async.Sleep 1000 |> Async.RunSynchronously
                                    coordinator <! Processed value
                                    coordinator <! Request 2
                                | None ->
                                    logDebugf mailbox $"Done"
                            | ResponsePriority response ->
                                match response with
                                | Some value ->
                                    logDebugf mailbox $"Response priority %A{value}"
                                    Async.Sleep 1000 |> Async.RunSynchronously
                                    coordinator <! Processed value
                                | None ->
                                    logDebugf mailbox $"Done priority"
                            return! iterate ()
                        }
                        iterate (coordinator <! Request 0))
                    [ SpawnOption.SupervisorStrategy (Strategy.OneForOne (fun _ -> Directive.Stop)) ]
            ]
        
        Console.ReadKey() |> ignore
        0