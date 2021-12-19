namespace Actors.FSharp

module AkkaTcp =
    open System
    open System.Net
    open System.Text
    open Akka.FSharp
    open Akka.IO
    
    let baseName = "actors-fsharp"
    let systemName = $"%s{baseName}-system"
    let listenerName = $"%s{baseName}-listener"
    let listenerConnectionName endpoint = $"%s{baseName}-listener-connection-%A{endpoint}"
    let connectionName endpoint = $"%s{baseName}-connection-%A{endpoint}"
    
    let config =
        @"akka {
            stdout-loglevel = ""DEBUG""
            loglevel = ""DEBUG""
        }"

    let system = System.create systemName (Configuration.parse config)
    
    let private (|TcpBound|TcpConnected|TcpReceived|TcpClosed|TcpPeerClosed|TcpUnknown|) (message: obj) =
        match message with
        | :? Tcp.Bound      as bound      -> TcpBound      bound
        | :? Tcp.Connected  as connected  -> TcpConnected  connected 
        | :? Tcp.Received   as received   -> TcpReceived   received
        | :? Tcp.Closed     as closed     -> TcpClosed     closed
        | :? Tcp.PeerClosed as peerClosed -> TcpPeerClosed peerClosed
        | _ -> TcpUnknown

    let run () =
        let listen spawnConnected (endpoint: IPEndPoint) =
            spawn system listenerName
                (fun (mailbox: Actor<obj>) ->
                    let rec receive () = actor {
                        match! mailbox.Receive() with
                        | TcpBound bound ->
                            logDebugf mailbox $"Bound to %A{bound.LocalAddress}"
                            
                        | TcpConnected connected ->
                            mailbox.Sender() <! Tcp.Register (spawnConnected connected mailbox)
                            logDebugf mailbox $"New connection %A{connected.RemoteAddress}"
                        
                        | message ->
                            logDebugf mailbox $"Unhandled message %A{message} in %A{mailbox.Self.Path}"
                            mailbox.Unhandled()
                            
                        return! receive () }
                    
                    mailbox.Context.System.Tcp() <! Tcp.Bind(mailbox.Self, endpoint)
                    receive ())
        
        let connect (endpoint: IPEndPoint) =
            spawn system (connectionName endpoint)
                (fun (mailbox: Actor<obj>) ->
                    let rec receive state = actor {
                        match! mailbox.Receive() with
                        | TcpConnected connected ->
                            mailbox.Sender() <! Tcp.Register mailbox.Self
                            logDebugf mailbox $"Connected to %A{connected.RemoteAddress}"
                        
                            mailbox.Sender() <! Tcp.Write.Create(ByteString.FromString "Hello, World!")
                            logDebugf mailbox $"Wrote 'Hello, World!' to %A{connected.RemoteAddress}"
                        
                            return! receive (Some ((mailbox.Sender()), connected))
                        
                        | TcpReceived received ->
                            match state with
                            | Some (_, connected) -> 
                                logDebugf mailbox $"Received '%s{Encoding.ASCII.GetString(received.Data.ToArray())}' from %A{connected.RemoteAddress}"
                            | None ->
                                logDebugf mailbox $"Received '%s{Encoding.ASCII.GetString(received.Data.ToArray())}' while no connection was established (shouldn't happen)"
                            return! receive state
                        
                        | TcpClosed closed ->
                            match state with
                            | Some (_, connected) ->
                                logDebugf mailbox $"Closed %A{connected.RemoteAddress}"
                            | None ->
                                logDebugf mailbox "Closed before a connection was established (shouldn't happen)"
                            return! receive state
                            
                        | TcpPeerClosed peerClosed ->
                            match state with
                            | Some (_, connected) ->
                                logDebugf mailbox $"Peer closed %A{connected.RemoteAddress}"
                            | None ->
                                logDebugf mailbox "Peer closed before a connection was established (shouldn't happen)"
                            return! receive state
                            
                        | message ->
                            logDebugf mailbox $"Unhandled message %A{message} in %A{mailbox.Self.Path}"
                            mailbox.Unhandled()
                            return! receive state }
                    
                    mailbox.Context.System.Tcp() <! (Tcp.Connect endpoint)
                    receive None)
          
        let listener =
            IPEndPoint(IPAddress.Any, 9090)
            |> listen
                (fun connected (parent: Actor<obj>) ->
                    spawn parent (listenerConnectionName connected.RemoteAddress)
                        (fun (mailbox: Actor<obj>) ->
                            let rec receive () = actor {
                                match! mailbox.Receive() with
                                | TcpReceived received ->
                                    let data = (Encoding.ASCII.GetString (received.Data.ToArray()))
                                    logDebugf mailbox $"Received '%s{data}' from %A{connected.RemoteAddress}"
                                    
                                    parent.Sender() <! Tcp.Write.Create(ByteString.FromString data)
                                    logDebugf mailbox $"Wrote '%s{data}' to %A{connected.RemoteAddress}"
                                
                                | TcpClosed closed ->
                                    logDebugf mailbox $"Closed %A{connected.RemoteAddress}"
                                    
                                | TcpPeerClosed peerClosed ->
                                    logDebugf mailbox $"Peer closed %A{connected.RemoteAddress}"
                                
                                | message ->
                                    logDebugf mailbox $"Unhandled message %A{message} in %A{mailbox.Self.Path}"
                                    mailbox.Unhandled()
                                    
                                return! receive () }
                            receive ())) 
           
        let connection = IPEndPoint(IPAddress.Loopback, 9090) |> connect
                
        Console.ReadKey() |> ignore
        0