### 1.1.6
- Fix incorrect use of __SOURCE_DIRECTORY__

### 1.1.5
- Update www.google.com --> gstatic.com

### 1.1.4
- Update to XPlot 1.5.0

### 1.1.3
- Fix silly bug

### 1.1.2
- Don't use ReadKey

### 1.1.1
- Simplify FSharp.Literate.Scripts so it only contains a tool fsx2html
- Update packages to more recent versions
- Remove RProvider from package set by default (too unreliable)
- Remove FSharp.Charting from package set by default (Windows only)

### 1.1.0
 - Ensure packages are consistent
 - Rename FsLab.Runners to FSharp.Literate.Scripts
 - Add "dotnet new" templates

### 1.0.4
 - Update doc links

### 1.0.3 (07 June 2017)
 - Update template to target VS2017

### 1.0.2 (30 October 2016)
 - Update XPlot (with new Plotly.js wrapper) and Math.Net

### 1.0.1 (6 September 2016)
 - Update fsx2html with latest build.fsx

### 1.0.0 (6 July 2016)
 - Ionide support via AddHtmlPrinter, update dependencies

* 0.0.10-beta - Update RProvider and Deedle references
* 0.0.11-beta - Add logo
* 0.0.12-beta - Fix the NuGet package
* 0.0.13-beta - Update Deelde and RProvider, correct bug
* 0.0.14-beta - Update references
* 0.0.15 - Update references, improve Math.NET integration
* 0.0.16 - Improve Math.NET integration
* 0.0.17 - Include initial version of Foogle Charts
* 0.0.18 - Fix extension methods for Foogle Charts
* 0.0.19 - Reference specific version of dependencies
* 0.1.0 - Updating to Paket-based projects
* 0.1.1 - Fix issues in the load script (Foogle Charts mostly)
* 0.1.2 - Update dependencies (Deeple, RProvider, Math.NET and FSharp.Formatting)
* 0.1.3 - Recover Latex support in FsLab Journal
* 0.1.4 - Update dependencies (Foogle.Charts)
* 0.2.0 - Update dependencies, rework runner for FsLab Journal
* 0.2.1 - Update FSharp.Formatting, more cross-platform journals
* 0.2.2 - Minor fixes in new fsx2html (for journals)
* 0.2.3 - Update FSharp.Formatting
* 0.2.4 - Initial integration with XPlot
* 0.2.5 - Include Google DataTable wrapper and JSON.NET references
* 0.2.6 - Autoload Google Charts in the Journal template
* 0.2.7 - Add fsi printer for XPlot charts
* 0.3.0 - Use no-op 'fsi' in journal, update dependencies
* 0.3.1 - Include new FSharp.Formatting styles in fsx2html
* 0.3.2 - Update Deedle and remove FAKE workaround (now fixed)
* 0.3.3 - Update JSON.NET and other references (to avoid NuGet resolve issues)
* 0.3.4 - Fixed Plotly chart size issue. Removed default (#91, #93)
* 0.3.5 - Update Deedle (including BigDeedle release)
* 0.3.6 - Add journal extension support for Visual Studio 2012 and 2015 (#23, #82)
* 0.3.7 - Require lower version of F# Compiler Service
* 0.3.8 - Require lower version of F# Compiler Service and FSharp.Formatting
* 0.3.9 - Update to the latest version (with FSharp.Formatting based on FCS 1.4)
* 0.3.10 - Update to the new version of R provider
* 0.3.11 - Update Math.NET Numerics and fix journal template
* 0.3.12 - Update Math.NET, JSON and XPplot
* 0.3.13 - Fix the load script for new XPlot.Plotly
* 0.3.14 - Update FSharp.Formatting dependency
* 0.3.15 - Add DynamicInterop to the load script
* 0.3.16 - Fix the load script (XPlot.Plotly)
* 0.3.17 - Update the FsLab VS template (Suave 1.0 support), infrastructure
* 0.3.18 - RProvider and R.NET update
* 0.3.19 - Update RProvider (cross-plat support)
* 0.3.20 - Paket update, fix websocket location in HTML template
* 0.3.21 - Update Deedle and F# Data dependencies
* 0.4.0-beta - Support AddHtmlPrinter, deprecate Foogle
* 0.4.1-beta - Attach files as fsx for auto-loading
* 0.4.2-beta - Fix dependencies and load scripts
* 0.4.3-beta - Fix scripts and styles in formatters
* 0.4.4-beta - Fix grid server and template margins
* 0.4.5-beta - Experimental BigDeedle support
* 0.4.6-beta - Experimental BigDeedle support, including Series
* 0.4.7-beta - Update Deedle dependency, tweaking styles
* 0.4.8-beta - Fix bug in Deedle formatter
* 0.4.9-beta - HtmlPrinters for Plotly, Math.NET, FSharp.Charting
* 0.4.10-beta - Fix default styles
