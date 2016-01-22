#!/usr/bin/env fsharpi
#I @"packages/FAKE/tools"
#r @"FakeLib.dll"
open Fake
open System
open System.IO
open Fake.AssemblyInfoFile
open SemVerHelper

RestorePackages()

// Paths
let jenkinsFSharpDir = "./Jenkins-Fsharp/"
let unitTestsDir = "./Jenkins.FSharp.Tests/"

let releaseDir = "Release/"
let nuGetDir = releaseDir + "NuGet/"
let nuGetProjectDll = nuGetDir + "lib/net45/Jenkins.Fsharp.dll"
let nUnitToolPath = "packages/NUnit.Runners/tools/"


// Helper Functions
let outputFolder baseDir =
    baseDir + "bin/Debug/"

let projectFolder baseDir =
    baseDir + "*.fsproj"

let binFolder baseDir =
    baseDir + "bin/"

let assemblyInfo baseDir =
    baseDir + "AssemblyInfo.fs"

// version info
let version = SemVerHelper.parse (ReadFileAsString ".semver") // or retrieve from CI server

// Targets
Target "Clean" (fun _ ->
    CleanDirs [
        jenkinsFSharpDir |> binFolder
        unitTestsDir |> binFolder
    ]
)

Target "Update Assembly Version" (fun _ ->
    CreateFSharpAssemblyInfo (jenkinsFSharpDir |> assemblyInfo) [
         Attribute.Title "Jenkins-FSharp"
         Attribute.Description "A F# Wrapper for Python Jenkins API"
         Attribute.Guid "4ead3524-8220-4f0b-b77d-edd088597fcf"
         Attribute.Product "Jenkins.fs"
         Attribute.Version (version.ToString())
         Attribute.FileVersion (version.ToString())
    ]
)

Target "BuildApp" (fun _ ->
   !! (jenkinsFSharpDir |> projectFolder)
        |> MSBuildReleaseExt (jenkinsFSharpDir |> outputFolder) ["TreatWarningsAsErrors","true"] "Build"
        |> Log "AppBuild-Output: "
)

Target "BuildTest" (fun _ ->
    !! (unitTestsDir |> projectFolder)
        |> MSBuildReleaseExt (unitTestsDir |> outputFolder) ["TreatWarningsAsErrors","true"] "Build"
        |> Log "AppBuild-Output: "
)

Target "Test" (fun _ ->
    !! "./Jenkins.FSharp.Tests/bin/Debug/Jenkins.FSharp.Tests.dll"
    |> NUnit (fun p ->
        { p with
            ToolPath = nUnitToolPath;
            DisableShadowCopy = true
            TimeOut = TimeSpan.FromMinutes 20.
            OutputFile = "NUnit.xml" })
)

// copy the distributable source files & dll into the Release folder 
Target "Copy Release Files" (fun _ ->

    CopyFiles 
        releaseDir 
        [
            jenkinsFSharpDir + "Jenkins.fs"
            jenkinsFSharpDir + "JenkinsRoutes.fs"
            (jenkinsFSharpDir |> outputFolder) + "Jenkins.Fsharp.dll"
        ]
)

Target "Upload to NuGet" (fun _ ->
    // Copy the dll into the right place
    CopyFiles 
        (releaseDir + "NuGet/lib/net45")
        [(jenkinsFSharpDir |> outputFolder) + "Jenkins.Fsharp.dll"]

    trace <| "buildParam nuget-version: " + getBuildParam "nuget-version"
    trace <| "buildParam nuget-api-key: " + getBuildParam "nuget-api-key"

    let version = getBuildParam "nuget-version"
    let nuspec = Path.Combine(nuGetDir, "Jenkins.fs.nuspec")
    File.WriteAllText(nuspec,
                      """<?xml version="1.0" encoding="utf-8"?>
<package xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
  <metadata xmlns="http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd">
    <id>@project@</id>
    <version>@build.number@</version>
    <authors>@authors@</authors>
    <owners>@authors@</owners>
    <summary>@summary@</summary>
    <licenseUrl>https://raw.githubusercontent.com/WilliamBerryiii/Jenkins-Fsharp/master/LICENSE.md</licenseUrl>
    <projectUrl>https://github.com/WilliamBerryiii/Jenkins-Fsharp</projectUrl>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>@description@</description>
    <releaseNotes>@releaseNotes@</releaseNotes>
    <copyright>Copyright William Berry 2015</copyright>
    <tags>Jenkins api fsharp f#</tags>
    @dependencies@
    @references@
  </metadata>
  @files@
</package>""")

    // Create and upload package
    NuGet (fun n ->
        {n with
            Authors = ["William Berry"]
            Summary = "F# wrapper for working with the JenkinsCI ReST API"
            OutputPath = nuGetDir
            WorkingDir = nuGetDir
            Project = "Jenkins-FSharp.fs"
            Version = version
            AccessKey = getBuildParam "nuget-api-key"
            ReleaseNotes = getBuildParam "nuget-release-notes"
            PublishTrials = 3
            Publish = bool.Parse(getBuildParamOrDefault "nuget-publish" "true")
            ToolPath = FullName "./packages/NuGet.CommandLine/tools/NuGet.exe"
            Files =
                [ "lib\\net45\\*.dll", Some "lib\\net45", None
                  "lib\\net45\\*.mdb", Some "lib\\net45", None 
                  "lib\\net45\\*.xml", Some "lib\\net45", None ]
            Dependencies =
                [ "FSharp.Core", GetPackageVersion "./packages" "FSharp.Core"
                  "Http.fs-prerelease", GetPackageVersion "./packages" "Http.fs-prerelease"
                  "FSharp.Data", GetPackageVersion "./packages" "FSharp.Data" ] })
        nuspec
)


Target "All" (fun _ ->
    // A dummy target so I can build everything easily
    ()
)

// Dependencies
"Clean"
  ==> "Update Assembly Version"
  ==> "BuildApp"
  ==> "BuildTest"
  ==> "Test"
  ==> "Copy Release Files"
  ==> "Upload to NuGet"
  ==> "All"

// start build
RunTargetOrDefault "All"