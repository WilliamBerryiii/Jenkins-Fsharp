namespace Jenkins.Fsharp

open System
open System.Web
open HttpClient
open FSharp.Data
open FSharp.Data.JsonExtensions
open System.Text.RegularExpressions

type JenkinsConfiguration (baseUri:string, userName:string, password:string, timeout) =
    member this.BaseUri = baseUri
    member this.UserName = userName
    member this.Password = password
    member this.Timeout = match timeout with 
                            | Some(timeout) -> timeout
                            | None -> 30

type Result<'TSuccess, 'TFailure> =
     | Success of 'TSuccess
     | Failure of 'TFailure 


module Jenkins =
    [<Literal>]
    let Info = "api/json"
    [<Literal>]
    let JobInfo = "job/{0}/api/json?depth={1}"
    [<Literal>]
    let JobName = "job/{0}/api/json?tree=name"
    [<Literal>] 
    let PluginInfo = "pluginManager/api/json?depth={0}"
    [<Literal>] 
    let CrumbUrl = "crumbIssuer/api/json"
    [<Literal>] 
    let QueueInfo = "queue/api/json?depth=0"
    [<Literal>] 
    let CancleQueue = "queue/cancelItem?id={0}"
    [<Literal>] 
    let CreateJob = "createItem?name={0}"
    [<Literal>] 
    let ConfigJob = "job/{0}/config.xml"
    [<Literal>] 
    let DeleteJob = "job/{0}/doDelete"
    [<Literal>] 
    let EnableJob = "job/{0}/enable"
    [<Literal>] 
    let DisableJob = "job/{0}/disable"
    [<Literal>] 
    let CopyJob = "createItem?name={0}&mode=copy&from={1}"
    [<Literal>] 
    let RenameJob = "job/{0}/doRename?newName={1}"
    [<Literal>] 
    let BuildJob = "job/{0}/build"
    [<Literal>] 
    let StopBuild = "job/{0}/{1}/stop"
    [<Literal>] 
    let BuildWithParametersJob = "job/{0}/buildWithParameters"
    [<Literal>] 
    let BuildInfo = "job/{0}/{1}/api/json?depth={2}"
    [<Literal>] 
    let BuildConsoleOutput = "job/{0}/{1}/consoleText"
    [<Literal>] 
    let NodeList = "computer/api/json"
    [<Literal>] 
    let CreateNode = "computer/doCreateItem?{0}"
    [<Literal>] 
    let DeleteNode = "computer/{0}/doDelete"
    [<Literal>] 
    let NodeInfo = "computer/{0}/api/json?depth={1}"
    [<Literal>] 
    let NodeType = "hudson.slaves.DumbSlave$DescriptorImpl"
    [<Literal>] 
    let ToggleOffline = "computer/{0}/toggleOffline?offlineMessage={1}"
    [<Literal>] 
    let ConfigNode = "computer/{0}/config.xml"



    let GetDepth depth = match depth with 
                            | Some(depth) -> depth
                            | None -> 0

    let EncodParameter parameter = HttpUtility.UrlPathEncode(parameter)

    let JenkinsOpen resource (configuration:JenkinsConfiguration) = 
        
        let request = String.Format("{0}/{1}", configuration.BaseUri, resource)
        printfn "%A" request
        let addAuthorization (configuration:JenkinsConfiguration) request = 
            match (configuration.UserName, configuration.Password) with 
            | ("","") -> request 
            | (_,_) -> withBasicAuthentication configuration.UserName configuration.Password request
                    

        let response = createRequest Get request 
                        |> addAuthorization configuration
                        |> getResponse

        let result = match response.StatusCode with 
                        | 200 -> Success response.EntityBody.Value 
                        | 401 | 403 | 500 -> Failure (String.Format("Status Code:{0}; Body: {1} ", response.StatusCode, response.EntityBody))
                        | 404 -> Failure (String.Format("Status Code:{0}; Body: {1} ", response.StatusCode, response.EntityBody))
                        | _ -> Failure (String.Format("Status Code:{0}; Body: {1} ", response.StatusCode, response.EntityBody))
        result

    let GetInfo (configuration:JenkinsConfiguration) = 
        let resource = JenkinsOpen Info configuration
        resource 

    let GetJobs (configuration:JenkinsConfiguration) =
        let resp = GetInfo configuration
        let jobs = match resp with
                    | Success s -> Success ((JsonValue.Parse(s)?jobs).AsArray())
                    | Failure f -> Failure f
        jobs

    let GetJobInfo (configuration:JenkinsConfiguration) (name:string) (depth) =
        let resource = String.Format(JobInfo, (EncodParameter name), GetDepth depth)
        let resp = JenkinsOpen resource configuration
        resp

    let GetJobName (configuration:JenkinsConfiguration) (jobName:string) (depth) =

        let resource = String.Format(JobName, (EncodParameter jobName), GetDepth depth)
        let response = JenkinsOpen resource configuration

        let (|Equals|_|) arg x = if (arg = x) then Some() else None

        let resp = match response with 
                    | Success s -> match (JsonValue.Parse(s)?name).AsString() with 
                                    | Equals jobName ->  
                                        Success s
                                    | _ -> 
                                        Failure (String.Format("Jenkins returned an unexpected job name {0}", JsonValue.Parse(s)?name))
                    | Failure f -> Failure f
        resp
    
    let DebugJobInfo (configuration:JenkinsConfiguration) (name:string) =
        let jobInfo = GetJobInfo configuration (EncodParameter name) (Some 10)
        let resp = match jobInfo with 
                    | Success s -> 
                        (JsonValue.Parse(s) |> printfn "%A") |> ignore 
                        JsonValue.Parse(s).AsString()
                    | Failure f -> 
                        printfn "%A" f
                        f
        resp
        
    let GetJobInfoRegex (configuration:JenkinsConfiguration) (regExPattern:string) depth =
        let jobs = GetJobs configuration 
        let regexMatches pattern input =
            if (Regex.Matches(input, pattern)).Count > 0 then Some(input) else None

        let jobInfo = match jobs with 
                        | Success s -> 
                            s 
                            |> Array.filter (fun x -> (Regex.Matches((x?name.AsString()), regExPattern)).Count > 0)
                            |> Seq.map ( fun y -> 
                                GetJobInfo configuration (y?name.AsString()) depth)
                        | Failure f -> Seq.empty
        jobInfo

    let GetBuildInfo (configuration:JenkinsConfiguration) (name:string) (number:int) depth =
        let resource = String.Format(BuildInfo, EncodParameter name, number, GetDepth depth)
        let resp = JenkinsOpen resource configuration
        resp

    let GetQueueInfo (configuration:JenkinsConfiguration) = 
        let response = JenkinsOpen QueueInfo configuration
        let qInfo = match response with 
                    | Success s -> Success ((JsonValue.Parse(s)?items).AsString())
                    | Failure f -> Failure f
        qInfo

module test = 
    open Jenkins
    let config = JenkinsConfiguration ( baseUri = "http://localhost:8080", userName = "", password = "", timeout = None )

    let jobInfo = Jenkins.GetJobInfo config "Test_stable" None 
    let jobName = Jenkins.GetJobName config "Test_stable" None 
    let debugJobInfo = Jenkins.DebugJobInfo config "Test_stable"  
    let getJobs = Jenkins.GetJobs config
    let regexJobInfo = Jenkins.GetJobInfoRegex config @"^Test" None
    let quotedParams = Jenkins.EncodParameter "test name"
    let getBuildInfo = Jenkins.GetBuildInfo config "Test_stable" 4 None
    let getQueueInfo = Jenkins.GetQueueInfo config

