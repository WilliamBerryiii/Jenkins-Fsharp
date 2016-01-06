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

    let EncodPath (parameter:string) = HttpUtility.UrlPathEncode(parameter)
    let EncodQueryString (parameter:string) = HttpUtility.UrlEncode(parameter)

    let GetJenkinsRequest resource (configuration:JenkinsConfiguration) = 
        
        let request = String.Format("{0}/{1}", configuration.BaseUri, resource)
        printfn "%A" request
        let addAuthorization (configuration:JenkinsConfiguration) request = 
            match (configuration.UserName, configuration.Password) with 
            | ("","") -> request 
            | (_,_) -> withBasicAuthentication configuration.UserName configuration.Password request
                    

        let request = createRequest Get request 
                        |> addAuthorization configuration
        request

    let GetResponse (request:Request) =

        let response = request |> getResponse
        let result = match response.StatusCode with 
                        | 200 -> Success response.EntityBody.Value 
                        | 401 | 403 | 500 -> Failure (String.Format("Status Code:{0}; Body: {1} ", response.StatusCode, response.EntityBody))
                        | 404 -> Failure (String.Format("Status Code:{0}; Body: {1} ", response.StatusCode, response.EntityBody))
                        | _ -> Failure (String.Format("Status Code:{0}; Body: {1} ", response.StatusCode, response.EntityBody))
        result

    let GetInfo (configuration:JenkinsConfiguration) = 
        let resource = GetJenkinsRequest Info configuration
        resource 

    let GetJobs (configuration:JenkinsConfiguration) =
        let resp = GetInfo configuration |> GetResponse
        let jobs = match resp with
                    | Success s -> Success ((JsonValue.Parse(s)?jobs).AsArray())
                    | Failure f -> Failure f
        jobs

    let GetJobInfo (configuration:JenkinsConfiguration) (name:string) (depth) =
        let resource = String.Format(JobInfo, (EncodPath name), GetDepth depth)
        let resp = GetJenkinsRequest resource configuration |> GetResponse
        resp

    let GetJobName (configuration:JenkinsConfiguration) (jobName:string) (depth) =

        let resource = String.Format(JobName, (EncodPath jobName), GetDepth depth)
        let response = GetJenkinsRequest resource configuration |> GetResponse

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
        let jobInfo = GetJobInfo configuration (EncodPath name) (Some 10) 
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
        let resource = String.Format(BuildInfo, EncodPath name, number, GetDepth depth)
        let response = GetJenkinsRequest resource configuration |> GetResponse
        response

    let GetQueueInfo (configuration:JenkinsConfiguration) = 
        let response = GetJenkinsRequest QueueInfo configuration |> GetResponse
        let qInfo = match response with 
                    | Success s -> Success ((JsonValue.Parse(s)?items).AsString())
                    | Failure f -> Failure f
        qInfo

    let CancelQueue (configuration:JenkinsConfiguration) (queueItemId:int)= 
        let resource = String.Format(CancleQueue, queueItemId)
        let response = GetJenkinsRequest resource configuration |> GetResponse
        let cancelInfo = match response with 
                            | Success s -> Success s
                            | Failure f -> Success f 
        cancelInfo

    let GetVersion (configuration:JenkinsConfiguration) = 
        let response = GetJenkinsRequest Info configuration
                        |> withHeader (Custom {name="X-Jenkins";value="0.0"} )
                        |> getResponse // use lib get response so we can unpack the header
        let version = match response.StatusCode with 
                        | 200 -> response.Headers.[NonStandard("X-Jenkins")] 
                        | _ -> String.Format("Error communicating with server {0}", configuration.BaseUri)
        version

    let GetPlugins (configuration:JenkinsConfiguration) = 
        let resource = String.Format(PluginInfo, 2) 
        let response = GetJenkinsRequest resource configuration |> GetResponse
        let plugins = match response with 
                        | Success s -> 
                            JsonValue.ParseMultiple(s) 
                            |> (fun plugin -> plugin)
                        | Failure f -> Seq.empty
        plugins