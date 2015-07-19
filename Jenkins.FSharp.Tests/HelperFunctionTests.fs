namespace Jenkins.FSharp.Tests

open FsUnit
open System
open NUnit.Framework
open Jenkins.Fsharp

[<TestFixture>]
type ``When testing a URL escape function `` () = 

    [<Test>]
    member this.``a space should be encoded with '%20' `` () = 
        Jenkins.EncodPath "name test" |> should equal "name%20test"

    [<Test>]
    member this.``an '&' should be encoded with '&' `` () = 
        Jenkins.EncodPath "name&test" |> should equal "name&test"

    [<Test>]
    member this.``a '_' should be encoded with '_' `` () = 
        Jenkins.EncodPath "name_test" |> should equal "name_test"
