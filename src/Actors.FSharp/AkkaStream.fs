namespace Actors.FSharp

open System

module AkkaStream =
    open Akka.FSharp
    open Akka.IO
    open Akka.Streams
    open Akka.Streams.Dsl
    
    let config =
        @"akka {
            loglevel = ""DEBUG""
        }"

    let system = System.create "system" (Configuration.parse config)
    
    let server () = task {
        let connections = system
                              .TcpStream()
                              .Bind("127.0.0.1", 8888)
        do! connections.RunForeach((fun connection ->
            Console.WriteLine $"New connection from {connection.RemoteAddress}"
            
            let echo = Flow.Create<ByteString>()
                           .Via(Framing.Delimiter(ByteString.FromString("\n"), 256, true))
                           .Select(fun c -> c.ToString())
                           .Select(fun c -> c + "!!!\n")
                           .Select(fun message -> ByteString.FromString message)
            connection.HandleWith(echo, system.Materializer()) |> ignore
            
//                Console.WriteLine $"Closing {connection.RemoteAddress}"
//                
//                let closed = Flow.FromSinkAndSource(Sink.Cancelled<ByteString>(), Source.Empty<ByteString>())
//                connection.HandleWith(closed, system.Materializer()) |> ignore
            
            ), system.Materializer())
        }
    let client () = task {
        let connection = system
                             .TcpStream()
                             .OutgoingConnection("127.0.0.1", 8888)

        let replParser = Flow.Create<string>()
                             .TakeWhile(fun c -> c <> "q")
                             .Concat(Source.Single("BYE"))
                             .Select(fun elem -> ByteString.FromString $"{elem}\n")

        let repl = Flow.Create<ByteString>()
                        .Via(Framing.Delimiter(
                            ByteString.FromString("\n"), 256, true))
                        .Select(fun c -> c.ToString())
                        .Select(fun text ->
                            Console.WriteLine($"Server: {text}")
                            text)
                        .Select(fun text -> "Hello, World!\n")
                        .Via(replParser)

        return connection.Join(repl).Run(system.Materializer())
    }
    server () |> ignore
    client () |> ignore