namespace Actors.FSharp

open System

module AkkaRemoteDeployment =
    open Akka.FSharp
    open Akka.Actor  
    
    let runClient () =
        printfn "Running client"
        
        let config =
            @"akka {
                loglevel = ""ERROR""
                actor.provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
                remote.helios.tcp {
                    hostname = localhost
                    port = 0
                }
            }"

        use system = System.create "akka-local-system" (Configuration.parse config)
        
        let rref =
            spawne system "akka-remote-actor"
                <@ (fun (mailbox: Actor<string>) ->
                        let rec iterate () : Cont<string, unit> = actor {
                            match! mailbox.Receive() with
                            | message ->
                                printfn $"Remote - %s{message}"
                            return! iterate ()    
                        }
                        iterate ()
                    ) @>
                [SpawnOption.Deploy (Deploy (RemoteScope (Address.Parse "akka.tcp://akka-remote-system@localhost:9001/")))]
        rref <! "Hello, remote world!"

        let sref = select "akka://akka-local-system/user/akka-remote-actor" system  
        sref <! "Hello, remote again!"

        let lref =
            spawn system "akka-local-actor"
                (fun (mailbox: Actor<string>) ->
                            let rec iterate () : Cont<string, unit> = actor {
                                match! mailbox.Receive() with
                                | message ->
                                    printfn $"Local - %s{message}"
                                return! iterate ()    
                            }
                            iterate ())
        lref <! "Hello, local world!"  

        Console.ReadKey() |> ignore
        0
        
    let runServer () =
        printfn "Running server"
        
        let config =  
                @"akka {
                    loglevel = ""ERROR""
                    actor.provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
                    remote.helios.tcp {
                        hostname = localhost
                        port = 9001
                    }
                }"

        use system = System.create "akka-remote-system" (Configuration.parse config)
        
        Console.ReadKey() |> ignore
        0