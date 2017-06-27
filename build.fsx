// --------------------------------------------------------------------------------------
// FAKE build script
// --------------------------------------------------------------------------------------

#I "packages/FAKE/tools"
#r "packages/FAKE/tools/FakeLib.dll"
#r "packages/Paket.Core/lib/net45/Paket.Core.dll"
#r "packages/DotNetZip/lib/net20/DotNetZip.dll"
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

System.Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

/// List of packages included in FsLab
/// (Version information is generated automatically based on 'FsLab.nuspec')
let packages =
  [ "Deedle"
    "Deedle.RPlugin"
    "FSharp.Charting"
    "FSharp.Data"
    "MathNet.Numerics"
    "MathNet.Numerics.FSharp"
    "DynamicInterop"
    "R.NET.Community"
    "R.NET.Community.FSharp"
    "RProvider"
    "Suave"
    // XPlot + dependencies
    "XPlot.Plotly"
    "XPlot.GoogleCharts"
    "XPlot.GoogleCharts.Deedle"
    "Google.DataTable.Net.Wrapper"
    "Newtonsoft.Json" ]
  |> List.map (fun p -> p, GetPackageVersion "packages" p)

let journalPackages =
  [ "FSharp.Compiler.Service"
    "FSharpVSPowerTools.Core"
    "FSharp.Formatting" ]
 |> List.map (fun p -> p, GetPackageVersion "packages" p)

/// Returns the subfolder where the DLLs are located
let getNetSubfolder package =
    match package with
    | "Google.DataTable.Net.Wrapper" -> "lib"
    | "FSharpVSPowerTools.Core" -> "lib/net45"
    | _ when package.StartsWith("XPlot") -> "lib/net45"
    | _ -> "lib/net40"

/// Returns assemblies that should be referenced for each package
let getAssemblies package =
    match package with
    | "Deedle.RPlugin" -> ["Deedle.RProvider.Plugin.dll"]
    | "FSharp.Charting" -> ["System.Windows.Forms.DataVisualization.dll"; "FSharp.Charting.dll"]
    | "RProvider" -> ["RProvider.Runtime.dll"; "RProvider.dll"]
    | "R.NET.Community" -> ["RDotNet.dll"; "RDotNet.NativeLibrary.dll"]
    | "R.NET.Community.FSharp" -> ["RDotNet.FSharp.dll"]
    | package -> [package + ".dll"]

// --------------------------------------------------------------------------------------
// FAKE targets for building FsLab and FsLab.Runner NuGet packages
// --------------------------------------------------------------------------------------

// Read release notes & version info from RELEASE_NOTES.md
let release = LoadReleaseNotes "RELEASE_NOTES.md"
let packageVersions = dict (packages @ journalPackages @ ["FsLab.Runner", release.NugetVersion])

Target "Clean" (fun _ ->
  CleanDirs ["temp"; "nuget"; "bin"]
)

Target "DownloadDependencies" (fun _ ->
  CleanDir "paket-files/fslaborg"
  Git.Repository.cloneSingleBranch "paket-files/fslaborg" "https://github.com/fslaborg/FsLab.Templates.git" "journal" "FsLab.Templates"
  Git.Repository.cloneSingleBranch "paket-files/fslaborg" "https://github.com/fslaborg/FsLab.Formatters.git" "master" "FsLab.Formatters"
)

Target "GenerateFsLab" (fun _ ->
  // Get directory with binaries for a given package
  let getLibDir package = package + "/" + (getNetSubfolder package)
  let getLibDirVer package = package + "." + packageVersions.[package] + "/" + (getNetSubfolder package)

  // Additional lines to be included in FsLab.fsx
  let nowarn = ["#nowarn \"211\""; "#I __SOURCE_DIRECTORY__"]
  let extraInitAll  = File.ReadLines(__SOURCE_DIRECTORY__ + "/src/FsLab.fsx")  |> Array.ofSeq
  let startIndex = extraInitAll |> Seq.findIndex (fun s -> s.Contains "***FsLab.fsx***")
  let extraInit = extraInitAll.[startIndex + 1 ..] |> List.ofSeq

  // Generate #I for all library, for all possible folder
  let includes =
    [ for package, _ in packages do
        yield sprintf "#I \"../%s\"" (getLibDir package)
        yield sprintf "#I \"../%s\"" (getLibDirVer package) ]

  // Generate #r for all libraries
  let references =
    packages
    |> List.collect (fst >> getAssemblies)
    |> List.map (sprintf "#r \"%s\"")

  // Copy formatter source files to the temp directory
  let formattersDir = "paket-files/fslaborg/FsLab.Formatters/src"
  let copyAsFsx target fn =
    ensureDirectory target
    CopyFile (target </> (Path.GetFileNameWithoutExtension(fn) + ".fsx")) fn
  !! (formattersDir + "/Shared/*.*") -- "**/Mock.fs" |> Seq.iter (copyAsFsx "temp/Shared")
  !! (formattersDir + "/Text/*.*") |> Seq.iter (copyAsFsx "temp/Text")
  !! (formattersDir + "/Html/*.*") |> Seq.iter (copyAsFsx "temp/Html")
  !! (formattersDir + "/Themes/*.*") |> Seq.iter (copyAsFsx "temp/Themes")

  // Generate #load commands to load AddHtmlPrinter and AddPrinter calls
  let loads =
    [ yield ""
      for f in Directory.GetFiles("temp/Shared") do
        yield sprintf "#load \"Shared/%s\"" (Path.GetFileName f)
      yield "#if NO_FSI_ADDPRINTER"
      yield "#else"
      yield "#if HAS_FSI_ADDHTMLPRINTER"
      for f in Directory.GetFiles("temp/Html") do
        yield sprintf "#load \"Html/%s\"" (Path.GetFileName f)
      yield "#else"
      for f in Directory.GetFiles("temp/Text") do
        yield sprintf "#load \"Text/%s\"" (Path.GetFileName f)
      yield "#endif"
      yield "#endif\n" ]

  // Write everything to the 'temp/FsLab.fsx' file
  let lines = nowarn @ includes @ references @ loads @ extraInit
  File.WriteAllLines(__SOURCE_DIRECTORY__ + "/temp/FsLab.fsx", lines)
)

Target "BuildRunner" (fun _ ->
    !! (project + ".sln")
    |> MSBuildRelease "" "Rebuild"
    |> ignore
)

Target "UpdateNuSpec" (fun _ ->
    // Update included files in FsLab.nuspec to include formatting scripts
    let (!) n = XName.Get(n)
    let path = "src/FsLab.nuspec"
    let doc = XDocument.Load(path)
    let files = doc.Descendants(XName.Get "files").First()
    files.RemoveAll()
    files.Add(XElement(!"file", XAttribute(!"src", "..\\temp\\FsLab.fsx"), XAttribute(!"target", ".")))
    let includes =
      [ "temp\\Shared"; "temp\\Text"; "temp\\Html"; "temp\\Themes" ]
      |> Seq.collect Directory.GetFiles 
    for f in includes do
      let subdir = Path.GetDirectoryName(f).Substring(5)
      files.Add(XElement(!"file", XAttribute(!"src", "..\\" + f), XAttribute(!"target", subdir)))
    doc.Save(path + ".updated")
    DeleteFile path
    Rename path (path + ".updated")
)

Target "NuGet" (fun _ ->
    let specificVersion (name, version) = name, sprintf "[%s]" version
    NuGet (fun p ->
        { p with
            Dependencies = packages |> List.map specificVersion
            Authors = authors
            Project = project
            Summary = summary
            Description = description
            Version = release.NugetVersion
            ReleaseNotes = release.Notes |> toLines
            Tags = tags
            OutputPath = "bin"
            AccessKey = getBuildParamOrDefault "nugetkey" ""
            Publish = hasBuildParam "nugetkey" })
        ("src/" + project + ".nuspec")
    NuGet (fun p ->
        { p with
            Dependencies = packages @ journalPackages |> List.map specificVersion
            Authors = authors
            Project = projectRunner
            Summary = summaryRunner
            Description = descriptionRunner
            Version = release.NugetVersion
            ReleaseNotes = release.Notes |> toLines
            Tags = tags
            OutputPath = "bin"
            AccessKey = getBuildParamOrDefault "nugetkey" ""
            Publish = hasBuildParam "nugetkey" })
        ("src/" + project + ".Runner.nuspec")
)

// --------------------------------------------------------------------------------------
// FAKE targets for building the FsLab template project
// --------------------------------------------------------------------------------------

Target "UpdateVSIXManifest" (fun _ ->
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

Target "GenerateTemplate" (fun _ ->
  // Generate ZIPs with item templates
  ensureDirectory "temp/experiments"
  for experiment in [(*"walkthrough-with-r";*) "walkthrough"; "experiment"] do
    ensureDirectory ("temp/experiments/" + experiment)
    CopyRecursive ("src/experiments/" + experiment) ("temp/experiments/" + experiment)  true |> ignore
    "misc/item.png" |> CopyFile ("temp/experiments/" + experiment + "/__TemplateIcon.png")
    "misc/preview.png" |> CopyFile ("temp/experiments/" + experiment + "/__PreviewImage.png")
    !! ("temp/experiments/" + experiment + "/**")
    |> Zip ("temp/experiments/" + experiment) ("temp/experiments/" + experiment + ".zip")

  // Generate ZIP with project template
  ensureDirectory "temp/journal"
  !! "paket-files/fslaborg/FsLab.Templates/build.*"
  ++ "paket-files/fslaborg/FsLab.Templates/*.dependencies"
  ++ "paket-files/fslaborg/FsLab.Templates/*.fs*"
  |> CopyFiles "temp/journal"
  CopyRecursive "src/journal" "temp/journal/" true |> ignore
  ".paket/paket.bootstrapper.exe" |> CopyFile "temp/journal/paket.bootstrapper.exe"
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

Target "Publish" DoNothing
Target "All" DoNothing

"Clean"
  ==> "DownloadDependencies"
  ==> "GenerateFsLab"
  ==> "BuildRunner"
  ==> "UpdateNuSpec"
  ==> "NuGet"

"NuGet"
  ==> "UpdateVSIXManifest"
  ==> "GenerateTemplate"
  ==> "BuildTemplate"
  ==> "All"

RunTargetOrDefault "All"
