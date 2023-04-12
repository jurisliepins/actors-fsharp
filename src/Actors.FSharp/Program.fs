namespace Actors.FSharp

open System
open Akka.FSharp

module AkkaSystem = Akka.FSharp.System

[<Struct>]
type Command =
    | IsNullOrWhiteSpace of Value: string

[<Struct>]
type CommandResult =
    | IsNullOrWhiteSpaceTrue  of ValueTrue: string
    | IsNullOrWhiteSpaceFalse of ValueFalse: string

module Program =
    [<EntryPoint>]
    let main args =
        let system = AkkaSystem.create "akka-system" (Configuration.load ())
        
        let receiverRef = spawn system "receiver" (fun (mailbox: Actor<obj>) -> 
            let rec receive () = actor {
                match! mailbox.Receive() with
                | :? Command as command ->
                    return! handleCommand command
                    
                | message ->
                    return! unhandled message }

            and handleCommand (command: Command) =
                match command with
                | Command.IsNullOrWhiteSpace value ->
                    if String.IsNullOrWhiteSpace(value) then
                        mailbox.Context.Sender <! CommandResult.IsNullOrWhiteSpaceTrue value
                    else
                        mailbox.Context.Sender <! CommandResult.IsNullOrWhiteSpaceFalse value
                receive ()
                
            and unhandled message =
                mailbox.Unhandled(message)
                receive ()

            receive ())
        
        let senderRef = spawn system "sender" (fun (mailbox: Actor<obj>) -> 
            let rec receive () = actor {
                match! mailbox.Receive() with
                | :? CommandResult as result ->
                    return! handleCommandResult result
                    
                | message ->
                    return! unhandled message }
        
            and handleCommandResult (result: CommandResult) =
                match result with
                | IsNullOrWhiteSpaceTrue  value -> ()
                | IsNullOrWhiteSpaceFalse value -> ()  
                receive ()
                
            and unhandled message =
                mailbox.Unhandled(message)
                receive ()
            
            receive ())
        
        for i in 0..(1_000_000 - 1) do
            receiverRef.Tell((Command.IsNullOrWhiteSpace "Hello, World!"), senderRef)
            receiverRef.Tell((Command.IsNullOrWhiteSpace ""), senderRef)
                
        Console.ReadKey() |> ignore
        0