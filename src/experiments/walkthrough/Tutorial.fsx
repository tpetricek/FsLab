(*** hide ***)
#I ".."
#load "packages/FsLab.0.0.20/FsLab.fsx"
(**

FsLab Walkthrough
=================

This file is a sample experiment that demonstrates the capabilities of FsLab
journal. FsLab experiments automatically includes a reference to the [FsLab 
NuGet package][fslab], so you can use all the great data science F# packages.
The template also contains a runner that formats your script files nicely using
[F# Formatting][fsfmt] and generates nice HTML or LaTeX reports. To write your 
experiments, you can include Markdown-formatted text in comments starting with 
`**` such as this one. The report is generated and opened automatically when 
you hit **F5**. 

When you generate a report, the code in all scripts in the solution is executed,
the resulting charts and tables are embedded and everything is nicely formatted.
The rest of this experiment shows the available libraries and how to format 
your experiments.

FsLab libraries
---------------

The FsLab package automatically references the following F# libraries:

 * [Deedle][deedle] for working with data frames and data series
 * [F# R type provider][rprovider] for interoperating with R
 * [F# Charting][fschart] for building interactive charts
 * [F# Data][fsdata] with data-access with F# type providers
 * [Math.NET Numerics][mathnet] for writing numerical calculations

Sample data access with F# Data
-------------------------------

The following snippet builds a simple Deedle data frame using data obtained 
from the WorldBank type provider from the F# Data library:

*)
open Deedle
open FSharp.Data

// Get countries in the Euro area
let wb = WorldBankData.GetDataContext()
let countries = wb.Regions.``Euro area``

// Get a frame with debts as a percentage of GDP 
let debts = 
  [ for c in countries.Countries ->
      let debts = c.Indicators.``Central government debt, total (% of GDP)``
      c.Name => series debts ] |> frame
(**
The above snippet defines a `debt` value, which is a data frame with years as 
the row index and country names as the column index. You can use the 
`include-value` command to include a table summarizing the frame data:
*)

(*** include-value:debts ***)

(**
As you can see, you can even include simple F# expressions in the command. By default,
FsLab Journal uses the "G4" format string, but if you want to use other format string,
you can specify it in the `Main.fs` file when calling `Journal.Process`. You can also 
embed LaTeX in your reports and write
(for more options [see the documentation](http://tpetricek.github.io/FSharp.Formatting/sideextensions.html)):

$$$
R = \frac{\mathit{round}(100 \times \mathit{debt})}{100}

Sample data analysis with Deedle
--------------------------------

You can also use `define-output` to give a name to a code block. When the code 
block is an expression that returns a value, you can use `include-it` to 
include the formatted result:

*)
(*** define-output:top4 ***)
let recent = debts.Rows.[2005 ..]

recent
|> Stats.mean
|> Series.sort
|> Series.rev
|> Series.take 4
|> round
(*** include-it:top4 ***)

(**
Here, we calculate means of debts over years starting with 2005, take the 4
countries with the greatest average debt and round the debts.

Calculating with Math.NET
-------------------------

If you want to implement a more complex calculation, you can turn Deedle frame
or series to Math.NET matrix or vector, respectively, and use the linear algebra
features of Math.NET. For example:
*)
open MathNet.Numerics.LinearAlgebra

// Create matrix from debts & vector from means
let debtsMat = debts |> Frame.fillMissingWith 0.0 |> Frame.toMatrix
let avgVect = debts |> Stats.mean |> Series.toVector

// Multiply debts per year by means
debtsMat * avgVect

(*** include-value:debtsMat ***)
(**
FsLab Journal also supports embedding of matrices and vectors. Here, you can see
how the matrix with debts (filled with zeros for missing values) is formatted as a
matrix. Then, the code shows how to use a simple matrix multiplication using 
Math.NET.

Embedding sample F# Charting charts
-----------------------------------

The generated report chan also automatically embed charts created using the 
F# Charting library. Here, we plot the debts of the 3 countries with the largest
debt based on the previous table:

*)
(*** define-output:chart ***)
open FSharp.Charting

// Combine three line charts and add a legend
Chart.Combine(
  [ Chart.Line(recent?Cyprus, Name="Cyprus")
    Chart.Line(recent?Malta, Name="Malta")
    Chart.Line(recent?Greece, Name="Greece") ])
  .WithLegend()
(*** include-it:chart ***)

(**
More about the FsLab journal runner
-----------------------------------

When you hit **F5** in Visual Studio, the FsLab runner automatically processes all 
`*.fsx` and `*.md` files in the root directory of your project. The generated files 
are placed in the `output` folder (together with all the styles and JavaScript files 
that it requires). Then, the runner opens your default web browser with the generated
file.

If you have multiple files, the runner automatically generates index file with
links to all your notebooks and opens this instead. You can also create your 
own index file by adding a file named `Index.fsx` or `Index.md` (if you only 
want to write Markdown text in your index).

### Command line

The runner can be also invoked from command line - the template includes a simple
[FAKE][fake] build script that is copied to the root directory of your project
(if you modify this, it will be overwritten). The build script supports the following
commands:

 - `build html` Generate HTML output for all scripts 
   and store the results in `output` folder

 - `build latex` Generate LaTeX output for all scripts 
   and store the results in `output` folder

 - `build pdf` Generate LaTeX output as when using `build latex` and then run `pdflatex` 
   on the files (this only works when you have `pdflatex` accessible in `PATH`

 [fslab]: http://www.nuget.org/packages/FsLab
 [fsfmt]: http://tpetricek.github.io/FSharp.Formatting/
 [rprovider]: http://bluemountaincapital.github.io/FSharpRProvider/
 [deedle]: http://bluemountaincapital.github.io/Deedle/
 [fschart]: http://fsharp.github.io/FSharp.Charting/
 [fsdata]: http://fsharp.github.io/FSharp.Data/
 [mathnet]: http://numerics.mathdotnet.com/
 [fake]: http://fsharp.github.io/FAKE/

*)