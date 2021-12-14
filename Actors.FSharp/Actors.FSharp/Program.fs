namespace Actors.FSharp

open System

module Program =
    open Actors.FSharp.Actor
    
    type QueueMessage<'a> =
        | Pull of AsyncReplyChannel<'a option>
        | Push of 'a * AsyncReplyChannel<unit>

    let pull replyChannel = Pull replyChannel
    let push value replyChannel = Push (value, replyChannel)
    
    let queue (queue: Queue<_>) (inbox: Actor<_>) =
        let rec await (queue: Queue<_>) =
            async {
                match! inbox.Receive() with
                | Pull replyChannel -> replyChannel.Reply(queue.Pull())
                | Push (value, replyChannel) -> replyChannel.Reply(queue.Push value)
                return! await queue
            }
        await queue
            
    let worker handleSome handleNone queueRef (inbox: Actor<_>) =
        let rec work () =
            async {
                match! queueRef <!! pull  with
                | Some value ->
                    do! handleSome value queueRef
                    return! work ()
                | None ->
                    do! handleNone queueRef
            }
        work ()
    
    let handleSome idx value queueRef =             
        async {
            printfn $"Worker %d{idx} pulled work %d{value}"
            do! Async.Sleep value
            if Random().Next(0, 10) > 8 then
                do! queueRef <!! (push value)
                printfn $"Worker %d{idx} pushing work %d{value} back"
        }
        
    let handleNone idx queueRef =
        async {
            printfn $"Worker %d{idx} done"                    
        }
    
    [<EntryPoint>]
    let main args =
        use queueRef = queue (Queue([| for idx in 1..1_000 do idx |])) |> spawn 
        
        for idx in 1..50 do
            let handleSome = handleSome idx
            let handleNone = handleNone idx
            let workerRef = worker handleSome handleNone queueRef |> spawn 
            ()
            
        while true do ()
        0