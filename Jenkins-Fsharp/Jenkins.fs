namespace Jenkins.Fsharp

open System
open System.Web
open System.Text
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

    let PostJenkinsRequest resource (configuration:JenkinsConfiguration) = 
        
        let request = String.Format("{0}/{1}", configuration.BaseUri, resource)
        printfn "%A" request
        let addAuthorization (configuration:JenkinsConfiguration) request = 
            match (configuration.UserName, configuration.Password) with 
            | ("","") -> request 
            | (_,_) -> withBasicAuthentication configuration.UserName configuration.Password request
                    

        let request = createRequest Post request 
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
        let resource = GetJenkinsRequest JenkinsRoutes.Info configuration
        resource 

    let GetJobs (configuration:JenkinsConfiguration) =
        let resp = GetInfo configuration |> GetResponse
        let jobs = match resp with
                    | Success s -> Success ((JsonValue.Parse(s)?jobs).AsArray())
                    | Failure f -> Failure f
        jobs

    let GetJobInfo (configuration:JenkinsConfiguration) (name:string) (depth) =
        let resource = String.Format(JenkinsRoutes.JobInfo, (EncodPath name), GetDepth depth)
        let resp = GetJenkinsRequest resource configuration |> GetResponse
        resp

    let GetJobName (configuration:JenkinsConfiguration) (jobName:string) (depth) =

        let resource = String.Format(JenkinsRoutes.JobName, (EncodPath jobName), GetDepth depth)
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
        let resource = String.Format(JenkinsRoutes.BuildInfo, EncodPath name, number, GetDepth depth)
        let response = GetJenkinsRequest resource configuration |> GetResponse
        response

    let GetQueueInfo (configuration:JenkinsConfiguration) = 
        let response = GetJenkinsRequest JenkinsRoutes.QueueInfo configuration |> GetResponse
        let qInfo = match response with 
                    | Success s -> Success ((JsonValue.Parse(s)?items).AsString())
                    | Failure f -> Failure f
        qInfo

    let CancelQueue (configuration:JenkinsConfiguration) (queueItemId:int)= 
        let resource = String.Format(JenkinsRoutes.CancelQueue, queueItemId)
        let response = GetJenkinsRequest resource configuration |> GetResponse
        let cancelInfo = match response with 
                            | Success s -> Success s
                            | Failure f -> Success f 
        cancelInfo

    let GetVersion (configuration:JenkinsConfiguration) = 
        let response = GetJenkinsRequest JenkinsRoutes.Info configuration
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
        version

    let CreateJob (configuration:JenkinsConfiguration) (name:string) (config:string) = 
        let resource = String.Format(JenkinsRoutes.CreateJob, name)
        let response = PostJenkinsRequest resource configuration 
                        |> withBodyEncoded config "UTF-8"
                        |> GetResponse

        let buildResponse = match response with 
                            | Success s -> Success s
                            | Failure f -> Success f 
        buildResponse

    let DeleteJob (configuration:JenkinsConfiguration) (name:string) = 
        let resource = String.Format(JenkinsRoutes.DeleteJob, name)
        let response = PostJenkinsRequest resource configuration |> GetResponse

        let buildResponse = match response with 
                            | Success s -> Success s
                            | Failure f -> Success f 
        buildResponse

    let EnableJob (configuration:JenkinsConfiguration) (name:string) = 
        let resource = String.Format(JenkinsRoutes.EnableJob, name)
        let response = PostJenkinsRequest resource configuration |> GetResponse

        let buildResponse = match response with 
                            | Success s -> Success s
                            | Failure f -> Success f 
        buildResponse

    let DisableJob (configuration:JenkinsConfiguration) (name:string) = 
        let resource = String.Format(JenkinsRoutes.DisableJob, name)
        let response = PostJenkinsRequest resource configuration |> GetResponse

        let buildResponse = match response with 
                            | Success s -> Success s
                            | Failure f -> Success f 
        buildResponse

    let ReconfigJob (configuration:JenkinsConfiguration) (name:string) (config:string) =
        let resource = String.Format(JenkinsRoutes.ConfigJob, name)
        let response = PostJenkinsRequest resource configuration
                       |> withBodyEncoded config "UTF-8"
                       |> GetResponse

        let buildResponse = match response with 
                            | Success s -> Success s
                            | Failure f -> Success f 
        buildResponse

    let BuildJob (configuration:JenkinsConfiguration) (name:string) parameters (token : string option)=
        let resource = String.Format(JenkinsRoutes.BuildJob, name)
        let request = PostJenkinsRequest resource configuration 

        parameters |> Array.iter (fun x -> request 
                                          |> withQueryStringItem x
                                          |> ignore)

        match token with
        | Some token -> request |> withQueryStringItem { name = "token"; value = token} |> ignore
        | None -> ()

        let response = request |> GetResponse

        let buildResponse = match response with 
                            | Success s -> Success s
                            | Failure f -> Success f 
        buildResponse