namespace Actors.FSharp

open Actors.FSharp

module Program =
    [<EntryPoint>]
    let main _ =
        AkkaCoordinator.run()