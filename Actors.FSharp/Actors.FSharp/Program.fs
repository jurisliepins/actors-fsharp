namespace Actors.FSharp

open Actors.FSharp

module Program =
    [<EntryPoint>]
    let main _ =
        AkkaTcp.run()