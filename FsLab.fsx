#r "System.Windows.Forms.DataVisualization.dll"
#nowarn "211"
#I "bin"
#I "../bin"
#I "lib"
#I "../lib"
#I "packages/FSharp.Charting.0.90.5/lib/net40"
#I "packages/FSharp.Data.1.1.10/lib/net40"
#I "packages/Deedle.0.9.12/lib/net40"
#I "packages/RProvider.1.0.5/lib"
#I "packages/MathNet.Numerics.3.0.0-alpha7/lib/net40"
#I "packages/MathNet.Numerics.FSharp.3.0.0-alpha7/lib/net40"
#I "../packages/FSharp.Charting.0.90.5/lib/net40"
#I "../packages/FSharp.Data.1.1.10/lib/net40"
#I "../packages/Deedle.0.9.12/lib/net40"
#I "../packages/RProvider.1.0.5/lib"
#I "../packages/MathNet.Numerics.3.0.0-alpha7/lib/net40"
#I "../packages/MathNet.Numerics.FSharp.3.0.0-alpha7/lib/net40"
#I "../../packages/FSharp.Charting.0.90.5/lib/net40"
#I "../../packages/FSharp.Data.1.1.10/lib/net40"
#I "../../packages/Deedle.0.9.12/lib/net40"
#I "../../packages/RProvider.1.0.5/lib"
#I "../../packages/MathNet.Numerics.3.0.0-alpha7/lib/net40"
#I "../../packages/MathNet.Numerics.FSharp.3.0.0-alpha7/lib/net40"
#r "FSharp.Charting.dll"
#r "FSharp.Data.dll"
#r "Deedle.dll"
#r "RProvider.dll"
#r "RDotNet.dll"
#r "MathNet.Numerics.dll"
#r "MathNet.Numerics.FSharp.dll"
namespace FsLab

module FsiAutoShow = 
  open FSharp.Charting
  open RProvider

  fsi.AddPrinter(fun (printer:Deedle.Internal.IFsiFormattable) -> 
    "\n" + (printer.Format()))
  fsi.AddPrinter(fun (ch:FSharp.Charting.ChartTypes.GenericChart) -> 
    ch.ShowChart(); "(Chart)")
  fsi.AddPrinter(fun (synexpr:RDotNet.SymbolicExpression) -> 
    synexpr.Print())

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
