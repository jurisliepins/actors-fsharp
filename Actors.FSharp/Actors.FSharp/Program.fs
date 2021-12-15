namespace Actors.FSharp

module Program =
    open Actors.FSharp.Actor

    let sinkActor fn (inbox: Actor<_>) =
        let rec receive () =
            async {
                let! value = inbox.Receive()
                fn value 
                return! receive ()
            }
        receive ()
        
    let convertActor fn (outputRef: Actor<_>) (inbox: Actor<_>) =
        let rec receive () =
            async {
                let! value = inbox.Receive()
                outputRef <! fn value
                return! receive ()
            }
        receive ()
        
    let statefulSinkActor fn initialState (inbox: Actor<_>) =
        let rec receive state =
            async {
                let! value = inbox.Receive()
                let nextState = fn value state
                return! receive nextState
            }
        receive initialState
        
    let statefulConvertActor fn initialState (outputRef: Actor<_>) (inbox: Actor<_>) =
        let rec receive state =
            async {
                let! value = inbox.Receive()
                let result, nextState = fn value state
                outputRef <! result
                return! receive nextState
            }
        receive initialState
    
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
    
    // TODO: Add last type under - https://mikhail.io/2016/03/functional-actor-patterns-with-akkadotnet-and-fsharp/ !    
        
    [<EntryPoint>]
    let main args =
//        // Sink Actor
        let printRef = sinkActor (printfn "%A")
                       |> spawn  
//        printRef <! 1
//            
//        // Converter Actor
        let squareRef = convertActor (fun value -> value * value) printRef
                        |> spawn
//        squareRef <! 2
//        
//        // Stateful Sink Actor
        let sumRef = statefulSinkActor (fun value state ->
                                            printfn "%A + %A = %A" value state (value + state)
                                            value + state)
                                        0
                     |> spawn
//        sumRef <! 1
//        sumRef <! 2
//        sumRef <! 3
        
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
        
//        squareWithChildRef <! 1
//        squareWithChildRef <! 2
//        squareWithChildRef <! 3
        
        while true do ()
        0