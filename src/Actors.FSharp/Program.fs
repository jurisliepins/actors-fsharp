namespace Actors.FSharp

open Actors.FSharp

module Program =
    [<EntryPoint>]
    let main args =
        if args.[0] = "-c" then
            AkkaRemoteDeployment.runClient () |> ignore
        elif args.[0] = "-s" then
            AkkaRemoteDeployment.runServer () |> ignore
        0