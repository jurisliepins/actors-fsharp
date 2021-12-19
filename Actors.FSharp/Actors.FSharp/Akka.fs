namespace Actors.FSharp

module Akka =
    open System
    open Akka.FSharp
    open Akka.Actor

    type MyDisposable() =
        interface IDisposable with
            member this.Dispose() = printfn "MyDisposable.Dispose"

    type MyMessage =
        | Print of string
        | Throw of string
        | MyMessageDone
        
    type MyPubSubMessage =
        | Pub of IActorRef * string
        | Sub
        | UnSub
        | MyPubSubMessageDone
        
    type MySupervisorMessage =
        | MySupervisorMessagePrint of string
        | MySupervisorMessageThrow of exn
        | MySupervisorMessageDone
    
    // Examples from - https://github.com/akkadotnet/getakka.net/blob/master/src/docs/FSharp%20API.md.    
    let run () = 
        use system = System.create "akka-fsharp-system" (Configuration.load ())
//----------------------------------------------------------------------------------------------------------------------
        let myMessageActorRef =
            spawn system "akka-fsharp-my-message-actor"
                (fun mailbox ->
                    let disposable = new MyDisposable()
                    mailbox.Defer (disposable :> IDisposable).Dispose
                    let rec receive () = actor {
                        match! mailbox.Receive() with
                        | Print message ->
                            printfn $"%A{message}"
                            return! receive ()
                        | Throw error ->
                            printfn $"%A{error}"
                            failwith error
                        | MyMessageDone ->
                            printfn "MyMessageDone"
                            ()
                    }
                    receive ())
        let myMessageActorRef2 =
            select "akka://akka-fsharp-system/user/bittorrent-fsharp-my-message-actor" system
                
        myMessageActorRef  <! Print "Hello, World!"
        myMessageActorRef2 <! Print "Hello, World!"
        
        myMessageActorRef  <! Throw "Error!"
        myMessageActorRef2 <! Throw "Error!"
        
        myMessageActorRef  <! MyMessageDone
        myMessageActorRef2 <! MyMessageDone
//----------------------------------------------------------------------------------------------------------------------        
        let sub =
            spawn system "bittorrent-fsharp-pubsub-message-sub"
                (fun mailbox ->
                    let rec receive () = actor {
                        match! mailbox.Receive() with
                        | Pub (sender, message) ->
                            printfn $"%A{sender.Path} says %s{message}"
                            return! receive ()
                        | Sub ->
                            subscribe typeof<MyPubSubMessage> mailbox.Self mailbox.Context.System.EventStream |> ignore
                            return! receive ()
                        | UnSub ->
                            unsubscribe typeof<MyPubSubMessage> mailbox.Self mailbox.Context.System.EventStream |> ignore
                            return! receive ()
                        | MyPubSubMessageDone ->
                            printfn "MyPubSubMessageDone"
                            ()
                    }
                    receive ())
                
        let pub =
            spawn system "bittorrent-fsharp-pubsub-message-pub"
                (fun mailbox ->
                    let rec receive () = actor {
                        match! mailbox.Receive() with
                        | Pub (sender, message) ->
                            publish (Pub (sender, message)) mailbox.Context.System.EventStream
                            return! receive ()
                        | MyPubSubMessageDone ->
                            printfn "MyPubSubMessageDone"
                            ()
                        | _ ->
                            return! receive ()
                    }
                    receive ())
            
        sub <! Sub
        
        Async.Sleep 50 |> Async.RunSynchronously
        
        pub <! Pub (pub, "Hello, Subscriber!")
        sub <! UnSub
        
        Async.Sleep 50 |> Async.RunSynchronously
        
        pub <! Pub (pub, "Hello, Subscriber (again)!")
//----------------------------------------------------------------------------------------------------------------------
        let mySupervisorMessageActorRef =
            spawnOpt system "bittorrent-fsharp-my-message-supervisor-actor"
                (fun mailbox ->
                    let rec receive () = actor {
                        match! mailbox.Receive() with
                        | MySupervisorMessagePrint message ->
                            printfn $"%A{message}"
                            return! receive ()
                        | MySupervisorMessageThrow exn ->
                            printfn $"%A{exn}"
                            raise exn
                        | MySupervisorMessageDone ->
                            printfn "MySupervisorMessageDone"
                            ()
                    }
                    receive ())
                [ SpawnOption.SupervisorStrategy (Strategy.OneForOne ((fun error ->
                    match error with
                    | :? ArithmeticException -> Directive.Restart
                    | _ -> SupervisorStrategy.DefaultDecider.Decide error ), 10, TimeSpan.FromMilliseconds(1000))) ]
                
        for _ in 1..20 do
            mySupervisorMessageActorRef <! MySupervisorMessageThrow (ArithmeticException())
        mySupervisorMessageActorRef <! MySupervisorMessagePrint "Hello, World!"
//----------------------------------------------------------------------------------------------------------------------        
        Console.ReadKey() |> ignore
        0

