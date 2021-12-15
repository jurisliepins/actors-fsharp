namespace Actors.FSharp

open System

type Actor<'a> private (mailbox: MailboxProcessor<'a>) =
    interface IDisposable with
        member this.Dispose() = (mailbox :> IDisposable).Dispose() 
    
    member _.Receive() = mailbox.Receive()
    member _.Receive(timeout) = mailbox.Receive(timeout)
    
    member _.Scan(scanner) = mailbox.Scan(scanner)
    
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
