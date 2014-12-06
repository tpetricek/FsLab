#nowarn "211"
#I "packages/Deedle.1.0.6/lib/net40"
#I "packages/Deedle.RPlugin.1.0.6/lib/net40"
#I "packages/FSharp.Charting.0.90.7/lib/net40"
#I "packages/FSharp.Data.2.0.14/lib/net40"
#I "packages/Foogle.Charts.0.0.2/lib/net40"
#I "packages/MathNet.Numerics.3.3.0/lib/net40"
#I "packages/MathNet.Numerics.FSharp.3.3.0/lib/net40"
#I "packages/RProvider.1.0.17/lib/net40"
#I "packages/R.NET.Community.1.5.15/lib/net40"
#I "packages/R.NET.Community.FSharp.0.1.8/lib/net40"
#I "../packages/Deedle.1.0.6/lib/net40"
#I "../packages/Deedle.RPlugin.1.0.6/lib/net40"
#I "../packages/FSharp.Charting.0.90.7/lib/net40"
#I "../packages/FSharp.Data.2.0.14/lib/net40"
#I "../packages/Foogle.Charts.0.0.2/lib/net40"
#I "../packages/MathNet.Numerics.3.3.0/lib/net40"
#I "../packages/MathNet.Numerics.FSharp.3.3.0/lib/net40"
#I "../packages/RProvider.1.0.17/lib/net40"
#I "../packages/R.NET.Community.1.5.15/lib/net40"
#I "../packages/R.NET.Community.FSharp.0.1.8/lib/net40"
#I "../../packages/Deedle.1.0.6/lib/net40"
#I "../../packages/Deedle.RPlugin.1.0.6/lib/net40"
#I "../../packages/FSharp.Charting.0.90.7/lib/net40"
#I "../../packages/FSharp.Data.2.0.14/lib/net40"
#I "../../packages/Foogle.Charts.0.0.2/lib/net40"
#I "../../packages/MathNet.Numerics.3.3.0/lib/net40"
#I "../../packages/MathNet.Numerics.FSharp.3.3.0/lib/net40"
#I "../../packages/RProvider.1.0.17/lib/net40"
#I "../../packages/R.NET.Community.1.5.15/lib/net40"
#I "../../packages/R.NET.Community.FSharp.0.1.8/lib/net40"
#I "../../../packages/Deedle.1.0.6/lib/net40"
#I "../../../packages/Deedle.RPlugin.1.0.6/lib/net40"
#I "../../../packages/FSharp.Charting.0.90.7/lib/net40"
#I "../../../packages/FSharp.Data.2.0.14/lib/net40"
#I "../../../packages/Foogle.Charts.0.0.2/lib/net40"
#I "../../../packages/MathNet.Numerics.3.3.0/lib/net40"
#I "../../../packages/MathNet.Numerics.FSharp.3.3.0/lib/net40"
#I "../../../packages/RProvider.1.0.17/lib/net40"
#I "../../../packages/R.NET.Community.1.5.15/lib/net40"
#I "../../../packages/R.NET.Community.FSharp.0.1.8/lib/net40"
#r "Deedle.dll"
#r "Deedle.RProvider.Plugin.dll"
#r "System.Windows.Forms.DataVisualization.dll"
#r "FSharp.Charting.dll"
#r "FSharp.Data.dll"
#r "Foogle.Charts.dll"
#r "MathNet.Numerics.dll"
#r "MathNet.Numerics.FSharp.dll"
#r "RProvider.Runtime.dll"
#r "RProvider.dll"
#r "RDotNet.dll"
#r "RDotNet.NativeLibrary.dll"
#r "RDotNet.FSharp.dll"

// ***FsLab.fsx*** (DO NOT REMOVE THIS COMMENT, everything below is copied to the output)
namespace FsLab

module FsiAutoShow = 
  open FSharp.Charting
  open RProvider

  fsi.AddPrinter(fun (printer:Deedle.Internal.IFsiFormattable) -> 
    "\n" + (printer.Format()))
  fsi.AddPrinter(fun (ch:FSharp.Charting.ChartTypes.GenericChart) -> 
    ch.ShowChart() |> ignore; "(Chart)")
  fsi.AddPrinter(fun (synexpr:RDotNet.SymbolicExpression) -> 
    synexpr.Print())

  open System.IO
  open Foogle
  open Foogle.SimpleHttp

  let server = ref None
  let tempDir = Path.GetTempFileName()
  let pid = System.Diagnostics.Process.GetCurrentProcess().Id
  let counter = ref 1

  do File.Delete(tempDir)
  do Directory.CreateDirectory(tempDir) |> ignore

  fsi.AddPrinter(fun (chart:FoogleChart) ->
    match !server with 
    | None -> server := Some (HttpServer.Start("http://localhost:8084/", tempDir))
    | _ -> ()
    let file = sprintf "chart_%d_%d.html" pid counter.Value
    File.WriteAllText(Path.Combine(tempDir, file), Internal.chartHtml chart)  
    System.Diagnostics.Process.Start("http://localhost:8084/" + file) |> ignore
    incr counter
    "(Foogle Chart)" )

namespace FSharp.Charting
open FSharp.Charting
open Deedle

[<AutoOpen>]
module FsLabExtensions =
  type FSharp.Charting.Chart with
    static member Line(data:Series<'K, 'V>, ?Name, ?Title, ?Labels, ?Color, ?XTitle, ?YTitle) =
      Chart.Line(Series.observations data, ?Name=Name, ?Title=Title, ?Labels=Labels, ?Color=Color, ?XTitle=XTitle, ?YTitle=YTitle)
    static member Column(data:Series<'K, 'V>, ?Name, ?Title, ?Labels, ?Color, ?XTitle, ?YTitle) =
      Chart.Column(Series.observations data, ?Name=Name, ?Title=Title, ?Labels=Labels, ?Color=Color, ?XTitle=XTitle, ?YTitle=YTitle)
    static member Pie(data:Series<'K, 'V>, ?Name, ?Title, ?Labels, ?Color, ?XTitle, ?YTitle) =
      Chart.Pie(Series.observations data, ?Name=Name, ?Title=Title, ?Labels=Labels, ?Color=Color, ?XTitle=XTitle, ?YTitle=YTitle)
    static member Area(data:Series<'K, 'V>, ?Name, ?Title, ?Labels, ?Color, ?XTitle, ?YTitle) =
      Chart.Area(Series.observations data, ?Name=Name, ?Title=Title, ?Labels=Labels, ?Color=Color, ?XTitle=XTitle, ?YTitle=YTitle)
    static member Bar(data:Series<'K, 'V>, ?Name, ?Title, ?Labels, ?Color, ?XTitle, ?YTitle) =
      Chart.Bar(Series.observations data, ?Name=Name, ?Title=Title, ?Labels=Labels, ?Color=Color, ?XTitle=XTitle, ?YTitle=YTitle)

[<AutoOpen>]
module FoogleExtensions =

  type Foogle.Chart with
    static member PieChart(frame:Frame<_, _>, column, ?label, ?title, ?pieHole) =
      Foogle.Chart.PieChart
        ( frame.GetColumn<float>(column) |> Series.observations, 
          ?label=label, ?title=title, ?pieHole=pieHole)
    static member GeoChart(frame:Frame<_, _>, column, ?label, ?region, ?mode, ?colorAxis, ?sizeAxis) =
      Foogle.Chart.GeoChart
        ( frame.GetColumn<float>(column) |> Series.observations, 
          ?label=label, ?region=region, ?mode=mode, ?colorAxis=colorAxis, ?sizeAxis=sizeAxis)
  
namespace MathNet.Numerics.LinearAlgebra
open MathNet.Numerics.LinearAlgebra
open Deedle

module Matrix =
  let inline toFrame matrix = matrix |> Matrix.toArray2 |> Frame.ofArray2D
module DenseMatrix =
  let inline ofFrame frame = frame |> Frame.toArray2D |> DenseMatrix.ofArray2
module SparseMatrix =
  let inline ofFrame frame = frame |> Frame.toArray2D |> SparseMatrix.ofArray2
module Vector =
  let inline toSeries vector = vector |> Vector.toSeq |> Series.ofValues
module DenseVector =
  let inline ofSeries series = series |> Series.values |> Seq.map (float) |> DenseVector.ofSeq
module SparseVector =
  let inline ofSeries series = series |> Series.values |> Seq.map (float) |> SparseVector.ofSeq


namespace Deedle
open Deedle
open MathNet.Numerics.LinearAlgebra

module Frame =
  let inline ofMatrix matrix = matrix |> Matrix.toArray2 |> Frame.ofArray2D
  let inline toMatrix frame = frame |> Frame.toArray2D |> DenseMatrix.ofArray2

  let ofCsvRows (data:FSharp.Data.Runtime.CsvFile<'T>) =
    match data.Headers with 
    | None -> Frame.ofRecords data.Rows
    | Some names -> Frame.ofRecords data.Rows |> Frame.indexColsWith names

module Series =
  let inline ofVector vector = vector |> Vector.toSeq |> Series.ofValues
  let inline toVector series = series |> Series.values |> Seq.map (float) |> DenseVector.ofSeq
