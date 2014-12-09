// --------------------------------------------------------------------------------------
// FAKE build script 
// --------------------------------------------------------------------------------------

#I "packages/FAKE/tools"
#r "packages/FAKE/tools/FakeLib.dll"
#r "System.Xml.Linq"
open System
open System.IO
open System.Xml.Linq
open System.Linq
open Fake 
open Fake.Git
open Fake.AssemblyInfoFile
open Fake.ReleaseNotesHelper
open System.Text.RegularExpressions

// --------------------------------------------------------------------------------------
// FsLab packages and configuration
// --------------------------------------------------------------------------------------

let project = "FsLab"
let projectRunner = "FsLab.Runner"
let authors = ["F# Data Science Working Group"]
let summary = "F# Data science package"
let summaryRunner = "F# Data science report generator"
let description = """
  FsLab is a single package that gives you all you need for doing data science with
  F#. FsLab includes explorative data manipulation library, type providers for easy
  data access, simple charting library, support for integration with R and numerical
  computing libraries. All available in a single package and ready to use!"""
let descriptionRunner = """
  This package contains a library for turning FsLab experiments written as script files
  into HTML and LaTeX reports. The easiest way to use the library is to use the 
  'FsLab Journal' Visual Studio template."""
let tags = "F# fsharp deedle series statistics data science r type provider mathnet"

/// List of packages included in FsLab
/// (Version information is generated automatically based on 'FsLab.nuspec')
let packages = 
  [ "Deedle", "1.0.6"
    "Deedle.RPlugin", "1.0.6"
    "FSharp.Charting", "0.90.7"
    "FSharp.Data", "2.0.14"
    "Foogle.Charts", "0.0.2"
    "MathNet.Numerics", "3.3.0"
    "MathNet.Numerics.FSharp", "3.3.0"
    "RProvider", "1.0.17"
    "R.NET.Community", "1.5.15" 
    "R.NET.Community.FSharp", "0.1.8" ]

let journalPackages = 
  [ "FSharp.Compiler.Service", "0.0.67"
    "FSharp.Formatting", "2.4.36" 
    "Microsoft.AspNet.Razor", "2.0.30506.0"
    "RazorEngine", "3.3.0" ]

/// Returns assemblies that should be referenced for each package
let getAssemblies package = 
    match package with
    | "Deedle.RPlugin" -> ["Deedle.RProvider.Plugin.dll"]
    | "FSharp.Charting" -> ["System.Windows.Forms.DataVisualization.dll"; "FSharp.Charting.dll"]
    | "RProvider" -> ["RProvider.Runtime.dll"; "RProvider.dll"]
    | "R.NET.Community" -> ["RDotNet.dll"; "RDotNet.NativeLibrary.dll"]
    | "R.NET.Community.FSharp" -> ["RDotNet.FSharp.dll"]
    | package -> [package + ".dll"]

// Generate #I directive for the following folders:
let folders = 
  [ "packages/"           // new F# project in VS with create directory for solution disabled
    "../packages/"        // new F# project in VS with create directory for solution enabled
    "../../packages/"     // fsharp-project-scaffold template
    "../../../packages/"] // just in case

// --------------------------------------------------------------------------------------
// FAKE build targets
// --------------------------------------------------------------------------------------

// Read release notes & version info from RELEASE_NOTES.md
System.Environment.CurrentDirectory <- __SOURCE_DIRECTORY__
let release = LoadReleaseNotes "RELEASE_NOTES.md"
let packageVersions = dict (packages @ journalPackages @ ["FsLab.Runner", release.NugetVersion])

Target "Clean" (fun _ ->
    CleanDirs ["temp"; "nuget"; "bin"]
)

Target "UpdateVersions" (fun _ ->
  // Helpers for generating "packages.config" file
  let (!) n = XName.Get(n)
  let makePackage (name, ver) = 
    XElement(!"package", XAttribute(!"id", name), XAttribute(!"version", ver))
  let makePackages packages = 
    XDocument(XElement(! "packages", packages |> Seq.map makePackage))

  // "src/packages.config" is used just for development (so that we can
  // edit the "FsLab.fsx" file and get decent autocomplete)
  makePackages(packages).Save("src/packages.config")
  
  // "src/FsLab.Runner/packages.config" are packages used by the Journal runner
  let allPackages = packages @ journalPackages
  makePackages(allPackages).Save("src/FsLab.Runner/packages.config")

  // "src/journal/packages.config" lists the packages that 
  // are referenced in the FsLab Journal project
  let allPackages = 
    [ "FsLab", release.NugetVersion
      "FsLab.Runner", release.NugetVersion] @ packages @ journalPackages
  makePackages(allPackages).Save("src/journal/packages.config")

  // "src/journal/Tutorial.fsx" needs to be updated to 
  // reference correct version of FsLab in the #load command
  let pattern = "packages/FsLab.(.*)/FsLab.fsx"
  let replacement = sprintf "packages/FsLab.%s/FsLab.fsx" release.NugetVersion
  let paths = Seq.append (!! "./src/experiments/**/*.fsx") ["./src/journal/Tutorial.fsx"]
  for path in paths do 
    let text = File.ReadAllText(path)
    let text = Regex.Replace(text, pattern, replacement)
    File.WriteAllText(path, text)

  // "src\journal\FsLab.Journal.fsproj" and "src\FsLab.Runner\FsLab.Runner.fsproj"
  // contain <HintPath> elements that points to the specific version in packages directory
  // This bit goes over all the <HintPath> elements & updates them
  let (!) n = XName.Get(n, "http://schemas.microsoft.com/developer/msbuild/2003")
  let paths = [ "src/journal/FsLab.Journal.fsproj"; "src/FsLab.Runner/FsLab.Runner.fsproj" ]
  for path in paths do 
    let fsproj = XDocument.Load(path)
    // Update contents of <HintPath>..</HintPath> and of <Copy SourceFiles=".." />
    let xvalues = 
      [ for copy in fsproj.Descendants(!"Copy") do 
          let sf = copy.Attribute(XName.Get "SourceFiles")
          yield sf.Value, sf.SetValue
        for hint in fsproj.Descendants(!"HintPath") do
          yield hint.Value, hint.SetValue ]

    let reg = Regex(@"\$\(SolutionDir\)\\packages\\([a-zA-Z\.]*)\.[^\\]*\\(.*)")
    for value, setter in xvalues do
      let res = reg.Match(value)
      if res.Success then
        let package = res.Groups.[1].Value
        let rest = res.Groups.[2].Value
        let version = packageVersions.[package]
        setter(sprintf @"$(SolutionDir)\packages\%s.%s\%s" package version rest)
    fsproj.Save(path + ".updated")  
    DeleteFile path
    Rename path (path + ".updated")

  // Specify <probing privatePath="..."> value in app.config of the journal
  // project, so that it automatically loads references from packages
  let (!) n = XName.Get(n, "urn:schemas-microsoft-com:asm.v1")
  let path = "src/journal/app.config"
  let appconfig = XDocument.Load(path)
  let probing = appconfig.Descendants(!"probing").First()
  let privatePath = probing.Attributes(XName.Get "privatePath").First()
  let value = 
    [ for p, v in packages @ journalPackages -> 
        sprintf "%s.%s\\lib\\net40" p v ] |> String.concat ";"
  privatePath.Value <- value
  appconfig.Save(path + ".updated")
  DeleteFile path
  Rename path (path + ".updated")

  /// Update version number in the VSIX manifest file of the template
  let (!) n = XName.Get(n, "http://schemas.microsoft.com/developer/vsx-schema/2011")
  let path = "src/template/source.extension.vsixmanifest"
  let vsix = XDocument.Load(path)
  let ident = vsix.Descendants(!"Identity").First()
  ident.Attribute(XName.Get "Version").Value <- release.AssemblyVersion
  vsix.Save(path + ".updated")
  DeleteFile path
  Rename path (path + ".updated")
)

Target "RestorePackages" (fun _ -> 
  Seq.concat [!! "./src/packages.config"; !! "./src/FsLab.Runner/packages.config"] 
  |> Seq.iter (RestorePackage (fun p -> { p with ToolPath = "./.nuget/NuGet.exe" })) 
)

Target "GenerateFsLab" (fun _ ->
  // Get directory with binaries for a given package
  let getLibDir package =
    package + "." + packageVersions.[package] + "/lib/net40"

  // Additional lines to be included in FsLab.fsx
  let nowarn = ["#nowarn \"211\""]
  let extraInitAll  = File.ReadLines(__SOURCE_DIRECTORY__ + "/src/FsLab.fsx")  |> Array.ofSeq
  let startIndex = extraInitAll |> Seq.findIndex (fun s -> s.Contains "***FsLab.fsx***")
  let extraInit = extraInitAll .[startIndex + 1 ..] |> List.ofSeq

  // Generate #I for all library, for all possible folder
  let includes = 
    [ for folder in folders do
        for package, _ in packages do
          yield sprintf "#I \"%s%s\"" folder (getLibDir package) ]
  
  // Generate #r for all libraries
  let references = 
    packages
    |> List.collect (fst >> getAssemblies)
    |> List.map (sprintf "#r \"%s\"")

  // Write everything to the 'temp/FsLab.fsx' file
  let lines = nowarn @ includes @ references @ extraInit
  File.WriteAllLines(__SOURCE_DIRECTORY__ + "/temp/FsLab.fsx", lines)
)

Target "BuildRunner" (fun _ ->
    !! (project + ".sln")
    |> MSBuildRelease "" "Rebuild"
    |> ignore
)

Target "NuGet" (fun _ ->
    CopyFile "bin/NuGet.exe" ".nuget/NuGet.exe"
    let nugetPath = ".nuget/nuget.exe"
    NuGet (fun p -> 
        { p with   
            Dependencies = packages
            Authors = authors
            Project = project
            Summary = summary
            Description = description
            Version = release.NugetVersion
            ReleaseNotes = release.Notes |> toLines
            Tags = tags
            OutputPath = "bin"
            ToolPath = nugetPath
            AccessKey = getBuildParamOrDefault "nugetkey" ""
            Publish = hasBuildParam "nugetkey" })
        ("src/" + project + ".nuspec")
    NuGet (fun p -> 
        { p with   
            Dependencies = packages @ journalPackages
            Authors = authors
            Project = projectRunner
            Summary = summaryRunner
            Description = descriptionRunner
            Version = release.NugetVersion
            ReleaseNotes = release.Notes |> toLines
            Tags = tags
            OutputPath = "bin"
            ToolPath = nugetPath
            AccessKey = getBuildParamOrDefault "nugetkey" ""
            Publish = hasBuildParam "nugetkey" })
        ("src/" + project + ".Runner.nuspec")
)

// --------------------------------------------------------------------------------------
// Build the FsLab template project
// --------------------------------------------------------------------------------------

Target "GenerateTemplate" (fun _ ->
  // Generate ZIPs with item templates
  ensureDirectory "temp/experiments"
  for experiment in ["walkthrough-with-r"; "walkthrough"; "experiment"] do
    ensureDirectory ("temp/experiments/" + experiment)
    CopyRecursive ("src/experiments/" + experiment) ("temp/experiments/" + experiment)  true |> ignore
    "misc/item.png" |> CopyFile ("temp/experiments/" + experiment + "/__TemplateIcon.png")
    "misc/preview.png" |> CopyFile ("temp/experiments/" + experiment + "/__PreviewImage.png")
    !! ("temp/experiments/" + experiment + "/**") 
    |> Zip ("temp/experiments/" + experiment) ("temp/experiments/" + experiment + ".zip")

  // Generate ZIP with project template
  ensureDirectory "temp/journal"
  CopyRecursive "src/journal" "temp/journal/" true |> ignore
  "misc/item.png" |> CopyFile "temp/journal/__TemplateIcon.png"
  "misc/preview.png" |> CopyFile "temp/journal/__PreviewImage.png"
  !! "temp/journal/**" |> Zip "temp/journal" "temp/journal.zip"

  // Create directory for the Template project
  CopyRecursive "src/template" "temp/template/" true |> ignore
  // Copy ItemTemplates
  ensureDirectory "temp/template/ItemTemplates"
  !! "temp/experiments/*.zip" 
  |> CopyFiles "temp/template/ItemTemplates"
  // Copy ProjectTemplates
  ensureDirectory "temp/template/ProjectTemplates"
  "temp/journal.zip" |> CopyFile "temp/template/FsLab Journal.zip" 
  "temp/journal.zip" |> CopyFile "temp/template/ProjectTemplates/FsLab Journal.zip" 
  // Copy other files
  "misc/logo.png" |> CopyFile "temp/template/logo.png"
  "misc/preview.png" |> CopyFile "temp/template/preview.png"
)

Target "BuildTemplate" (fun _ ->
  !! "temp/template/FsLab.Template.sln" 
  |> MSBuildDebug "" "Rebuild"
  |> ignore
  "temp/template/bin/Debug/FsLab.Template.vsix" |> CopyFile "bin/FsLab.Template.vsix"
)

Target "All" DoNothing

"Clean" 
  ==> "UpdateVersions"
  ==> "RestorePackages"
  ==> "GenerateFsLab"
  ==> "BuildRunner"
  ==> "NuGet"
  ==> "GenerateTemplate"
  ==> "BuildTemplate"
  ==> "All"

RunTargetOrDefault "All"