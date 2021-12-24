namespace Actors.FSharp

open System
open System.Collections.Concurrent
open System.Collections.Generic

type Channel<'a>(collection: ICollection<_>) =
    let queue = ConcurrentQueue<_>(collection)
    
    new() = Channel([||])
    
    member _.Push(value: 'a) : unit = queue.Enqueue value
    member _.Pull() : 'a option = match queue.TryDequeue() with true, value -> Some value | _ -> None
    
    member _.Count with get() = queue.Count

type Actor<'a> private (mailbox: MailboxProcessor<'a>) =
    interface IDisposable with
        member this.Dispose() = (mailbox :> IDisposable).Dispose() 
    
    member _.Receive() = mailbox.Receive()
    member _.Receive(timeout) = mailbox.Receive(timeout)
    
    member _.Scan(scanner) = mailbox.Scan(scanner)
    member _.Scan(scanner, timeout) = mailbox.Scan(scanner, timeout)
    
    member _.Post(message: 'a) = mailbox.Post(message)
    member _.PostAndReply(buildMessage: AsyncReplyChannel<'Reply> -> 'a) : 'Reply = mailbox.PostAndReply(buildMessage)
    member _.PostAndAsyncReply(buildMessage: AsyncReplyChannel<'Reply> -> 'a) : Async<'Reply> = mailbox.PostAndAsyncReply(buildMessage)
    
    static member (<!) (actor: Actor<_>, message) = actor.Post(message)
    static member (<!) (actor: Actor<_>, buildMessage: AsyncReplyChannel<'Reply> -> 'a) : 'Reply = actor.PostAndReply(buildMessage)
    static member (<!!) (actor: Actor<_>, buildMessage: AsyncReplyChannel<'Reply> -> 'a) : Async<'Reply> = actor.PostAndAsyncReply(buildMessage)

    static member Start<'a>(body: Actor<'a> -> Async<unit>) = new Actor<_>(MailboxProcessor<_>.Start(fun inbox -> body (new Actor<_>(inbox))))
    static member Start<'a>(body: Actor<'a> -> Async<unit>, cancellationToken) = new Actor<_>(MailboxProcessor<_>.Start((fun inbox -> body (new Actor<_>(inbox))), cancellationToken))
    
module Actor =
    let spawn (body: Actor<'a> -> Async<unit>) : Actor<'a> = Actor<_>.Start(body)
    let spawnChild (parent: Actor<_>) (body: Actor<_> -> Actor<'a> -> Async<unit>) : Actor<'a> = Actor<_>.Start(body parent)

module Vanilla =
    open Actor
    // Examples from - https://mikhail.io/2016/03/functional-actor-patterns-with-akkadotnet-and-fsharp/.
    let run () =
        let sinkActor fn (inbox: Actor<_>) =
            let rec receive () =
                async {
                    let! value = inbox.Receive()
                    fn value 
                    return! receive ()
                }
            receive ()
//----------------------------------------------------------------------------------------------------------------------    
        let convertActor fn (outputRef: Actor<_>) (inbox: Actor<_>) =
            let rec receive () =
                async {
                    let! value = inbox.Receive()
                    outputRef <! fn value
                    return! receive ()
                }
            receive ()
//----------------------------------------------------------------------------------------------------------------------            
        let statefulSinkActor fn initialState (inbox: Actor<_>) =
            let rec receive state =
                async {
                    let! value = inbox.Receive()
                    let nextState = fn value state
                    return! receive nextState
                }
            receive initialState
//----------------------------------------------------------------------------------------------------------------------            
        let statefulConvertActor fn initialState (outputRef: Actor<_>) (inbox: Actor<_>) =
            let rec receive state =
                async {
                    let! value = inbox.Receive()
                    let result, nextState = fn value state
                    outputRef <! result
                    return! receive nextState
                }
            receive initialState
//----------------------------------------------------------------------------------------------------------------------       
        let childSinkActor fn (parent: Actor<_>) (inbox: Actor<_>) =
            let rec receive () =
                async {
                    let! value = inbox.Receive()
                    fn parent value 
                    return! receive ()
                }
            receive ()
            
        let statefulConverterSupervisor fn child (inbox: Actor<_>) =
            let rec receive state =
                async {
                    let childRef = 
                        match state with
                        | Some state -> state
                        | None -> child |> spawnChild inbox
                    let! value = inbox.Receive()
                    childRef <! (fn value)
                    return! receive (Some childRef)
                }
            receive None
//----------------------------------------------------------------------------------------------------------------------    
        // Sink Actor
        let printRef = sinkActor (printfn "%A")
                       |> spawn  
        printRef <! 1
//----------------------------------------------------------------------------------------------------------------------            
        // Converter Actor
        let squareRef = convertActor (fun value -> value * value) printRef
                        |> spawn
        squareRef <! 2
//----------------------------------------------------------------------------------------------------------------------        
        // Stateful Sink Actor
        let sumRef = statefulSinkActor (fun value state ->
                                            printfn "%A + %A = %A" value state (value + state)
                                            value + state)
                                        0
                     |> spawn
        sumRef <! 1
        sumRef <! 2
        sumRef <! 3
//----------------------------------------------------------------------------------------------------------------------        
        // Stateful Converter Actor
        let sumStateRef = statefulConvertActor (fun (value: int) (state: int) ->
                                                    ((value + state), (value + state)))
                                                    0
                                                    printRef
                          |> spawn
        // Useful - https://stackoverflow.com/questions/29166468/f-child-agent-is-there-a-this-pointer.
        let squareWithChildRef = statefulConverterSupervisor
                                     (fun value -> value * value)
                                     (childSinkActor (fun parent value ->
                                                        printfn "%A" parent
                                                        printfn "%A" value))
                                 |> spawn
        
        squareWithChildRef <! 1
        squareWithChildRef <! 2
        squareWithChildRef <! 3
//----------------------------------------------------------------------------------------------------------------------        
        Console.ReadKey() |> ignore
        0