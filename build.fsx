// --------------------------------------------------------------------------------------
// FAKE build script
// --------------------------------------------------------------------------------------

#I "packages/FAKE/tools"
#r "packages/FAKE/tools/FakeLib.dll"
#r "packages/DotNetZip/lib/net20/DotNetZip.dll"
#r "System.Xml.Linq"
open System.IO
open System.Xml.Linq
open System.Linq
open Fake
open Fake.ReleaseNotesHelper

// --------------------------------------------------------------------------------------
// FsLab packages and configuration
// --------------------------------------------------------------------------------------

let buildDir = "bin"
let project = "FsLab"
let projectRunner = "FSharp.Literate.Scripts"
let authors = ["FsLab Contributors"]
let summary = "F# packages for data science"
let summaryRunner = "FsLab report generator"

let description = """
  FsLab is a combination package that supports doing data science with
  F#. FsLab includes literate scripting converted to HTML and PDF, and
  by default references Deedle (a data frame library), FSharp.Data (for
  data access) and XPlot (for visualization). You can optionally add any
  other nuget packages."""

let descriptionRunner = """
  This package contains a library for turning FsLab experiments written as script files
  into HTML and LaTeX reports."""
let tags = "F# fsharp deedle series statistics data science r type provider mathnet machine learning ML"

System.Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

/// List of packages included in FsLab
/// (Version information is generated automatically based on 'FsLab.nuspec')
let packages =
  [ "Deedle"
    //"Deedle.RPlugin"
    //"FSharp.Charting"
    "FSharp.Data"
    "MathNet.Numerics"
    "MathNet.Numerics.FSharp"
    //"DynamicInterop"
    //"R.NET.Community"
    //"R.NET.Community.FSharp"
    //"RProvider"
    "Suave"
    // XPlot + dependencies
    "XPlot.Plotly"
    "XPlot.GoogleCharts"
    "XPlot.GoogleCharts.Deedle"
    "Google.DataTable.Net.Wrapper"
    "Newtonsoft.Json" ]
  |> List.map (fun p -> p, GetPackageVersion "packages" p)

/// Returns the subfolder where the DLLs are located
let getNetSubfolder package =
    match package with
    | "Google.DataTable.Net.Wrapper" -> "lib"
    | "FSharp.Data" -> "lib/net45"
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

let exec exe args workingDir =
    printfn "%s %s (in %s)" exe args workingDir
    let code = Shell.Exec(exe, args, workingDir) 
    if code <> 0 then failwithf "%s %s failed, error code %d" exe args code


let execMono exe args workingDir =
    if isUnix then exec "mono" (exe + " " + args) workingDir 
    else exec exe args workingDir

// --------------------------------------------------------------------------------------
// FAKE targets for building FsLab and FSharp.Literate.Scripts NuGet packages
// --------------------------------------------------------------------------------------

// Read release notes & version info from RELEASE_NOTES.md
let release = LoadReleaseNotes "RELEASE_NOTES.md"
let packageVersions = dict (packages @ ["FSharp.Literate.Scripts", release.NugetVersion])

Target "Clean" (fun _ ->
  CleanDirs ["temp"; "nuget"; buildDir ]
)
let fsc = (if isUnix then "fsharpc" else "fsc.exe")


Target "UpdateFsLabScript" (fun _ ->
  // Get directory with binaries for a given package
  let getLibDir package = package + "/" + (getNetSubfolder package)
  let getLibDirVer package = package + "." + packageVersions.[package] + "/" + (getNetSubfolder package)

  // Additional lines to be included in FsLab.fsx
  let nowarn = ["#nowarn \"211\""; "#I __SOURCE_DIRECTORY__"]
  let extraInitAll  = File.ReadLines(__SOURCE_DIRECTORY__ + "/src/FsLab.fsx")  |> Array.ofSeq
  let extraInit = extraInitAll |> Array.skipWhile (fun s -> not  (s.Contains "***FsLab.fsx***")) |> List.ofArray

  // Generate #I for all library, for all possible folder
  let includes =
    [ for package, _ in packages do
        yield sprintf "#I \"../packages/%s\"" (getLibDir package)
        yield sprintf "#I \"../%s\"" (getLibDir package)
        yield sprintf "#I \"../packages/%s\"" (getLibDirVer package)
        yield sprintf "#I \"../%s\"" (getLibDirVer package) ]

  // Generate #r for all libraries
  let references =
    packages
    |> List.collect (fst >> getAssemblies)
    |> List.map (sprintf "#r \"%s\"")

  // Generate #load commands to load AddHtmlPrinter and AddPrinter calls
  let loads =
    [ yield ""
      for f in Directory.GetFiles("src/Shared") do
        yield sprintf "#load \"Shared/%s\"" (Path.GetFileName f)
      yield "#if !NO_FSI_ADDPRINTER"
      yield "#if HAS_FSI_ADDHTMLPRINTER"
      for f in Directory.GetFiles("src/Html") do
        yield sprintf "#load \"Html/%s\"" (Path.GetFileName f)
      yield "#else"
      for f in Directory.GetFiles("src/Text") do
        yield sprintf "#load \"Text/%s\"" (Path.GetFileName f)
      yield "#endif"
      yield "#endif\n" ]

  // Write everything to the 'temp/FsLab.fsx' file
  let lines = nowarn @ includes @ references @ loads @ extraInit
  let oldLines = File.ReadAllLines(__SOURCE_DIRECTORY__ + "/src/FsLab.fsx")
  if Array.ofList lines <> oldLines then
      File.WriteAllLines(__SOURCE_DIRECTORY__ + "/src/FsLab.fsx", lines)

  // Check that FsLab.fsx now compiles in FSI.EXE mode
  exec fsc "src/FsLab.fsx -r:FSharp.Compiler.Interactive.Settings.dll --nowarn:988 --nocopyfsharpcore --out:bin/test-compile-FsLab.exe" "."

  // Check that FsLab.fsx now compiles in HAS_FSI_ADDHTMLPRINTER (fsx2html, iFSharp) ) mode
  exec fsc "src/Mock/Mock.fsx src/FsLab.fsx -r:FSharp.Compiler.Interactive.Settings.dll --nowarn:988 --define:HAS_FSI_ADDHTMLPRINTER --nocopyfsharpcore --out:bin/test-compile-FsLab-HtmlPrinters.exe" "."

)

Target "BuildProjects" (fun _ ->
    MSBuildRelease "" "Restore" [ "FsLab.sln" ] |> ignore
    MSBuildRelease "" "Rebuild" [ "FsLab.sln" ] |> ignore
)

Target "UpdateNuSpec" (fun _ ->
    // Update included files in FsLab.nuspec to include formatting scripts
    let (!) n = XName.Get(n)
    let path = "src/FsLab.nuspec"
    let doc = XDocument.Load(path)
    let files = doc.Descendants(XName.Get "files").First()
    files.RemoveAll()
    files.Add(XElement(!"file", XAttribute(!"src", "../src/FsLab.fsx"), XAttribute(!"target", ".")))
    let includes =
      [ "src/Shared"; "src/Text"; "src/Html"; "src/Themes" ]
      |> Seq.collect Directory.GetFiles
    for f in includes do
      let subdir = Path.GetDirectoryName(f).Substring(4)
      files.Add(XElement(!"file", XAttribute(!"src", "../" + f.Replace("\\", "/")), XAttribute(!"target", subdir)))
    doc.Save(path + ".updated")
    DeleteFile path
    Rename path (path + ".updated")
)

Target "BuildNuGets" (fun _ ->
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
            OutputPath = buildDir
            WorkingDir = "nuget"
            AccessKey = getBuildParamOrDefault "nugetkey" ""
            Publish = hasBuildParam "nugetkey" })
        ("src/" + project + ".nuspec")
    NuGet (fun p ->
        { p with
            Dependencies = packages |> List.map specificVersion
            Authors = authors
            Project = projectRunner
            Summary = summaryRunner
            Description = descriptionRunner
            Version = release.NugetVersion
            ReleaseNotes = release.Notes |> toLines
            Tags = tags
            OutputPath = buildDir
            WorkingDir = "nuget"
            AccessKey = getBuildParamOrDefault "nugetkey" ""
            Publish = hasBuildParam "nugetkey" })
        ("src/fsx2html/FSharp.Literate.Scripts.nuspec")
)


// Update some files in the template directories and check the templates build and run
Target "UpdateAndCheckTemplates" (fun _ ->

  // Place the paket support in the templates
  ensureDirectory "src/journal/.paket"

  !! ".paket/paket.bootstrapper.exe" |> CopyFiles "src/journal/.paket"
  !! ".paket/Paket.Restore.targets" |> CopyFiles "src/journal/.paket"

  // Copy the paket.dependencies to the various templates
  let paketDepLines = File.ReadAllLines("paket.dependencies") |> Seq.takeWhile (fun s -> not (s.Contains("--CUT--")))
  File.WriteAllLines("src/journal/paket.dependencies", paketDepLines)
  File.AppendAllLines("src/journal/paket.dependencies", [sprintf "source ../../bin"])
  File.AppendAllLines("src/journal/paket.dependencies", [sprintf "nuget FsLab %s" release.NugetVersion])
  File.AppendAllLines("src/journal/paket.dependencies", [sprintf "nuget FSharp.Literate.Scripts %s" release.NugetVersion])
  
  // Create/update the paket.lock and do a local install of packages
  execMono (__SOURCE_DIRECTORY__ + "/.paket/paket.exe") "update" "src/journal"

  // Check that journal/build.fsx compiles in FSI.EXE mode
  exec fsc "build.fsx -r:FSharp.Compiler.Interactive.Settings.dll --nowarn:988 --nocopyfsharpcore --out:../../bin/test-compile-journal-build.fsx" "src/journal"

  // Check that journal/build.fsx runs in FAKE mode
  execMono (__SOURCE_DIRECTORY__ + "packages/FAKE/tools/FAKE.exe") "build.fsx html" "src/journal"
  execMono (__SOURCE_DIRECTORY__ + "packages/FAKE/tools/FAKE.exe") "build.fsx latex" "src/journal"

  // Replace the reference to the local source with the place where it will be when published
  File.WriteAllText("src/journal/paket.dependencies", File.ReadAllText("src/journal/paket.dependencies").Replace("source ../../bin", "source https://api.nuget.org/v3/index.json"))
  File.WriteAllText("src/journal/paket.lock", File.ReadAllText("src/journal/paket.lock").Replace("remote: ../../bin", "remote: https://api.nuget.org/v3/index.json"))

)


// --------------------------------------------------------------------------------------
// FAKE targets for building the FsLab templates
// --------------------------------------------------------------------------------------

Target "PlaceTempFiles" (fun _ ->

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
  ensureDirectory "temp/journal/styles"

  !! "src/journal/build.*"
  ++ "src/journal/paket.dependencies"
  ++ "src/journal/paket.references"
  ++ "src/journal/paket.lock"
  ++ "src/journal/*.fs*"
  |> CopyFiles "temp/journal"

  CopyRecursive "src/styles" "temp/journal/styles" true |> ignore

  ensureDirectory "temp/journal/.paket"
  ".paket/paket.bootstrapper.exe" |> CopyFile "temp/journal/.paket/paket.exe"
  ".paket/paket.bootstrapper.exe" |> CopyFile "temp/journal/.paket/paket.bootstrapper.exe"
  ".paket/Paket.Restore.targets" |> CopyFile "temp/journal/.paket/Paket.Restore.targets"

)

// Build Zip templates
Target "BuildZipTemplates" (fun _ ->
  !! "temp/journal/**" |> Zip "temp/journal" "temp/journal.zip"
)

Target "UpdateVSIXManifest" (fun _ ->
  /// Update version number in the VSIX manifest file of the template
  let (!) n = XName.Get(n, "http://schemas.microsoft.com/developer/vsx-schema/2011")
  let path = "src/vs-templates/source.extension.vsixmanifest"
  let vsix = XDocument.Load(path)
  let ident = vsix.Descendants(!"Identity").First()
  ident.Attribute(XName.Get "Version").Value <- release.AssemblyVersion
  vsix.Save(path + ".updated")
  DeleteFile path
  Rename path (path + ".updated")
)


Target "BuildVsTemplates" (fun _ ->

  // Create directory for the Template project
  CopyRecursive "temp/journal" "temp/vsjournal/" true |> ignore
  CopyRecursive "src/vs-templates/journal" "temp/vsjournal/" true |> ignore
  !! "temp/vsjournal/**" |> Zip "temp/vsjournal" "temp/vsjournal.zip"

  "misc/item.png" |> CopyFile "temp/vsjournal/__TemplateIcon.png"
  "misc/preview.png" |> CopyFile "temp/vsjournal/__PreviewImage.png"

  // Zip it up
  CopyRecursive "src/vs-templates" "temp/vs-templates/" true |> ignore

  // Copy ItemTemplates
  ensureDirectory "temp/vs-templates/ItemTemplates"

  !! "temp/experiments/*.zip"
  |> CopyFiles "temp/vs-templates/ItemTemplates"

  // Copy ProjectTemplates
  ensureDirectory "temp/vs-templates/ProjectTemplates"
  "temp/vsjournal.zip" |> CopyFile "temp/vs-templates/FsLab Journal.zip"
  "temp/vsjournal.zip" |> CopyFile "temp/vs-templates/ProjectTemplates/FsLab Journal.zip"

  // Copy other files
  "misc/logo.png" |> CopyFile "temp/vs-templates/logo.png"
  "misc/preview.png" |> CopyFile "temp/vs-templates/preview.png"

  !! "temp/vs-templates/FsLab.VsTemplates.sln"
  |> MSBuildDebug "" "Rebuild"
  |> ignore
  "temp/vs-templates/bin/Debug/FsLab.VsTemplates.vsix" |> CopyFile (buildDir + "/FsLab.VsTemplates.vsix")
)

// Build a BuildNuGets package containing "Dotnet new" templates
Target "BuildDotnetTemplates" (fun _ ->

  // Create directory for the Template project
  CopyRecursive "src/dotnet-templates" "temp/dotnet-templates/" true |> ignore
  // Copy ItemTemplates
  //ensureDirectory "temp/dotnet-templates/ItemTemplates"
  //!! "temp/experiments/*.zip"
  //|> CopyFiles "temp/vs-templates/ItemTemplates"
  // Copy ProjectTemplates
  //ensureDirectory "temp/vs-templates/ProjectTemplates"
  CopyRecursive "temp/journal" "temp/dotnet-templates/journal/" true |> ignore
  // Copy other files
  "misc/logo.png" |> CopyFile "temp/dotnet-templates/logo.png"
  "misc/preview.png" |> CopyFile "temp/dotnet-templates/preview.png"

  NuGetHelper.NuGetPack (fun p -> 
        { p with
            WorkingDir = "temp/dotnet-templates"
            OutputPath = "./" + buildDir + "/"
            Version = release.NugetVersion
            ReleaseNotes = toLines release.Notes}) @"temp/dotnet-templates/FsLab.Templates.nuspec"
)
Target "TestDotnetTemplatesNuGet" (fun _ ->

    // Globally install the templates from the template nuget package we just built
    DotNetCli.RunCommand id ("new -i " + buildDir + "/FsLab.Templates." + release.NugetVersion + ".nupkg")

    let testAppName = "testapp2" + string (abs (hash System.DateTime.Now.Ticks) % 100)
    // Instantiate the template. TODO: additional parameters and variations
    CleanDir testAppName
    DotNetCli.RunCommand id (sprintf "new fslab-journal -n %s -lang F#" testAppName)

    // Enable the local package source in paket.dependencies and paket.lock.  A total hack.
    let paketDepFile = sprintf "%s/paket.dependencies" testAppName
    File.WriteAllText(paketDepFile, File.ReadAllText(paketDepFile).Replace("source https://api.nuget.org/v3/index.json","source ../bin"))

    let paketLockFile = sprintf "%s/paket.lock" testAppName
    File.WriteAllText(paketLockFile, File.ReadAllText(paketLockFile).Replace("remote: https://api.nuget.org/v3/index.json","remote: ../bin"))

    let pkgs = Path.GetFullPath(buildDir)
    // When restoring, using the bin as a package source to pick up the package we just compiled
    DotNetCli.RunCommand id (sprintf "restore %s/%s.fsproj  --source https://api.nuget.org/v3/index.json --source %s" testAppName testAppName pkgs)
    
    let slash = if isUnix then "\\" else ""
    for c in ["Debug"; "Release"] do 
        for p in ["Any CPU"] do 
            exec "msbuild" (sprintf "%s/%s.fsproj /p:Platform=\"%s\" /p:Configuration=%s /p:PackageSources=%s\"https://api.nuget.org/v3/index.json%s;%s%s\"" testAppName testAppName p c  slash slash pkgs slash) "."

    let slash = if isUnix then "\\" else ""
    exec fsc (sprintf "%s/%s.fsx -r:FSharp.Compiler.Interactive.Settings.dll --nocopyfsharpcore" testAppName testAppName) "."
    
    // Check the processing of the scripts to HTML and LaTeX works
    execMono (__SOURCE_DIRECTORY__ + "packages/FAKE/tools/FAKE.exe") "build.fsx html" testAppName
    execMono (__SOURCE_DIRECTORY__ + "packages/FAKE/tools/FAKE.exe") "build.fsx latex"  testAppName


    (* Manual steps without building nupkg
        .\build BuildDotnetTemplates
        dotnet new -i  bin/journal.*.nupkg
        rmdir /s /q testapp2
        dotnet new fslab-journal -n testapp2 -lang F#
        dotnet restore testapp2/testapp2.fsproj -s bin/
        dotnet restore testapp2/testapp2.fsproj -s bin/
        msbuild testapp2/testapp2.fsproj
        fsc testapp2\journal.fsx -r:FSharp.Compiler.Interactive.Settings.dll
        fsi  -r:FSharp.Compiler.Interactive.Settings.dll testapp2/Tutorial.fsx
        fsi  -r:FSharp.Compiler.Interactive.Settings.dll --use:testapp2/Tutorial.fsx
        *)

)


Target "Publish" DoNothing
Target "All" DoNothing

//"Clean"
//  ==> "TestInDirectoryBuildOfTemplates"
//  ==> "BuildNuGets"

"Clean"
  ==> "BuildProjects"
  ==> "UpdateFsLabScript"
  ==> "UpdateNuSpec"
  ==> "BuildNuGets"
  ==> "All"

"Clean"
  ==> "UpdateFsLabScript"
  ==> "BuildNuGets"
  ==> "UpdateAndCheckTemplates"
  ==> "PlaceTempFiles"

"PlaceTempFiles"
  ==> "BuildZipTemplates"
  ==> "All"

"PlaceTempFiles"
  ==> "UpdateVSIXManifest"
  ==> "BuildVsTemplates"
  ==> "All"

"PlaceTempFiles"
  ==> "BuildZipTemplates"
  ==> "All"

"PlaceTempFiles"
  ==> "BuildDotnetTemplates"
  ==> "TestDotnetTemplatesNuGet"
  ==> "All"

RunTargetOrDefault "All"
