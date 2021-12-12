namespace Actors.FSharp

open System.Collections.Concurrent
open System.Collections.Generic

type Queue<'a>(collection: ICollection<_>) =
    let queue = ConcurrentQueue<_>(collection)
    
    new() = Queue([||])
    
    member _.Push(value: 'a) : unit = queue.Enqueue value
    member _.Pull() : 'a option = match queue.TryDequeue() with true, value -> Some value | _ -> None
    
    member _.Count with get() = queue.Count

