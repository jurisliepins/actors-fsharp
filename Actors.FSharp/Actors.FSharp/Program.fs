namespace Actors.FSharp

open System

module Program =
    open Actors.FSharp.Actor
    
    type QueueMessage<'a> =
        | Pull of AsyncReplyChannel<'a option>
        | Push of 'a * AsyncReplyChannel<unit>

    let pullMessage replyChannel = Pull replyChannel
    let pushMessage value replyChannel = Push (value, replyChannel)
    
    let queue collection (inbox: Actor<QueueMessage<'a>>) =
        let queue = Queue<_>(collection)
        let rec await () =
            async {
                match! inbox.Receive() with
                | Pull replyChannel -> replyChannel.Reply(queue.Pull())
                | Push (value, replyChannel) -> replyChannel.Reply(queue.Push value)
                return! await ()
            }
        await ()
            
    let worker handleSome handleNone (queue: Actor<QueueMessage<_>>) (_: Actor<QueueMessage<_>>) =
        let rec work () =
            async {
                match! queue <!! pullMessage  with
                | Some value ->
                    do! handleSome value queue
                    return! work ()
                | None ->
                    do! handleNone queue
            }
        work ()
    
    let handleSome idx value queue =             
        async {
            printfn $"Worker %d{idx} pulled work %d{value}"
            do! Async.Sleep value
            if Random().Next(0, 10) > 8 then
                do! queue <!! (pushMessage value)
                printfn $"Worker %d{idx} pushing work %d{value} back"
        }
        
    let handleNone idx queue =
        async {
            printfn $"Worker %d{idx} done"                    
        }
    
    [<EntryPoint>]
    let main args =
        use queue = spawn (queue [| for idx in 1..1_000 do idx |])
        for idx in 1..50 do
            let handleSome = handleSome idx
            let handleNone = handleNone idx
            let worker = spawn (worker handleSome handleNone queue)
            ()
            
        while true do ()
        0