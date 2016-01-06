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



//ToDo: Move these to an integration test suite
//module test = 
//    open Jenkins
//    let config = JenkinsConfiguration ( baseUri = "http://localhost:8080", userName = "", password = "", timeout = None )
//
//    let jobInfo = Jenkins.GetJobInfo config "Test_stable" None 
//    let jobName = Jenkins.GetJobName config "Test_stable" None 
//    let debugJobInfo = Jenkins.DebugJobInfo config "Test_stable"  
//    let getJobs = Jenkins.GetJobs config
//    let regexJobInfo = Jenkins.GetJobInfoRegex config @"^Test" None
//    let quotedParams = Jenkins.EncodParameter "test name"
//    let getBuildInfo = Jenkins.GetBuildInfo config "Test_stable" 4 None
//    let getQueueInfo = Jenkins.GetQueueInfo config
//    let getPlugins = Jenkins.GetPlugins config