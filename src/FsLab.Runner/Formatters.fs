module internal FsLab.Formatters

open System.IO
open Deedle
open Deedle.Internal
open FSharp.Literate
open FSharp.Markdown
open FSharp.Charting
open Foogle
open XPlot

// --------------------------------------------------------------------------------------
// Implements Markdown formatters for common FsLab things - including Deedle series
// and frames, F# Charting charts, System.Image values and Math.NET matrices & vectors
// --------------------------------------------------------------------------------------

// --------------------------------------------------------------------------------------
// Helper functions etc.
// --------------------------------------------------------------------------------------

open System.Windows.Forms
open FSharp.Charting.ChartTypes

/// Extract values from any series using reflection
let (|SeriesValues|_|) (value:obj) = 
  let iser = value.GetType().GetInterface("ISeries`1")
  if iser <> null then
    let keys = value.GetType().GetProperty("Keys").GetValue(value) :?> System.Collections.IEnumerable
    let vector = value.GetType().GetProperty("Vector").GetValue(value) :?> IVector
    Some(Seq.zip (Seq.cast<obj> keys) vector.ObjectSequence)
  else None

let (|Float|_|) (v:obj) = if v :? float then Some(v :?> float) else None
let (|Float32|_|) (v:obj) = if v :? float32 then Some(v :?> float32) else None

let inline (|PositiveInfinity|_|) (v: ^T) =
  if (^T : (static member IsPositiveInfinity: 'T -> bool) (v)) then Some PositiveInfinity else None
let inline (|NegativeInfinity|_|) (v: ^T) =
  if (^T : (static member IsNegativeInfinity: 'T -> bool) (v)) then Some NegativeInfinity else None
let inline (|NaN|_|) (v: ^T) =
  if (^T : (static member IsNaN: 'T -> bool) (v)) then Some NaN else None

/// Format value as a single-literal paragraph
let formatValue (floatFormat:string) def = function
  | Some(Float v) -> [ Paragraph [Literal (v.ToString(floatFormat)) ]] 
  | Some(Float32 v) -> [ Paragraph [Literal (v.ToString(floatFormat)) ]] 
  | Some v -> [ Paragraph [Literal (v.ToString()) ]] 
  | _ -> [ Paragraph [Literal def] ]

/// Format body of a single table cell
let td v = [ Paragraph [Literal v] ]

/// Use 'f' to transform all values, then call 'g' with Some for 
/// values to show and None for "..." in the middle
let mapSteps (startCount, endCount) f g input = 
  input 
  |> Seq.map f |> Seq.startAndEnd startCount endCount
  |> Seq.map (function Choice1Of3 v | Choice3Of3 v -> g (Some v) | _ -> g None)
  |> List.ofSeq

/// Reasonably nice default style for charts
let chartStyle ch =
  let grid = ChartTypes.Grid(LineColor=System.Drawing.Color.LightGray)
  ch 
  |> Chart.WithYAxis(MajorGrid=grid)
  |> Chart.WithXAxis(MajorGrid=grid)

/// Checks if the given directory exists. If not then this functions creates the directory.
let ensureDirectory dir =
  let di = new DirectoryInfo(dir)
  if not di.Exists then di.Create()

/// Combine two paths
let (@@) a b = Path.Combine(a, b)

// --------------------------------------------------------------------------------------
// Handling of R
// --------------------------------------------------------------------------------------

open RDotNet
open RProvider
open RProvider.graphics
open RProvider.grDevices
open System.Drawing
open System

/// Evaluation context that also captures R exceptions
type ExtraEvaluationResult = 
  { Results : IFsiEvaluationResult
    CapturedImage : Bitmap option }
  interface IFsiEvaluationResult

let isEmptyBitmap (img:Bitmap) =
  seq { 
    let bits = img.LockBits(Rectangle(0,0,img.Width, img.Height), Imaging.ImageLockMode.ReadOnly, Imaging.PixelFormat.Format32bppArgb)
    let ptr0 = bits.Scan0 : IntPtr
    let stride = bits.Stride
    for i in 0 .. img.Width - 1 do
      for j in 0 .. img.Height - 1 do
        let offset = i*4 + stride*j
        if System.Runtime.InteropServices.Marshal.ReadInt32(ptr0,offset) <> -1 then
          yield false }
  |> Seq.isEmpty            

let captureDevice f = 
  let file = Path.GetTempFileName() + ".png"   
  let isRavailable =
    try R.png(file) |> ignore; true 
    with _ -> false

  let res = f()
  let img = 
    if isRavailable then
      R.dev_off() |> ignore
      try
        let bmp = Image.FromStream(new MemoryStream(File.ReadAllBytes file)) :?> Bitmap
        File.Delete(file)
        if isEmptyBitmap bmp then None else Some bmp 
      with :? System.IO.IOException -> None
    else None

  { Results = res; CapturedImage = img } :> IFsiEvaluationResult

// --------------------------------------------------------------------------------------
// Handling of Math.NET Numerics Matrices
// --------------------------------------------------------------------------------------

open MathNet.Numerics
open MathNet.Numerics.LinearAlgebra

let inline formatMathValue (floatFormat:string) = function
  | PositiveInfinity -> "\\infty"
  | NegativeInfinity -> "-\\infty"
  | NaN -> "\\times"
  | Float v -> v.ToString(floatFormat)
  | Float32 v -> v.ToString(floatFormat)
  | v -> v.ToString()

let formatMatrix config (formatValue: 'T -> string) (matrix: Matrix<'T>) =
  let mappedColumnCount = min (config.MatrixStartColumnCount + config.MatrixEndColumnCount + 1) matrix.ColumnCount
  String.concat Environment.NewLine
    [ "\\begin{bmatrix}"
      matrix.EnumerateRows()
        |> mapSteps config.mrows id (function
          | Some row -> row |> mapSteps config.mcols id (function Some v -> formatValue v | _ -> "\\cdots") |> String.concat " & "
          | None -> Array.zeroCreate matrix.ColumnCount |> mapSteps config.mcols id (function Some v -> "\\vdots" | _ -> "\\ddots") |> String.concat " & ")
        |> String.concat ("\\\\ " + Environment.NewLine)
      "\\end{bmatrix}" ]

let formatVector (config:FormatConfig) (formatValue: 'T -> string) (vector: Vector<'T>) =
  String.concat Environment.NewLine
    [ "\\begin{bmatrix}"
      vector.Enumerate()
        |> mapSteps config.vitms id (function | Some v -> formatValue v | _ -> "\\cdots")
        |> String.concat " & "
      "\\end{bmatrix}" ]

// --------------------------------------------------------------------------------------
// Build FSI evaluator
// --------------------------------------------------------------------------------------

let mutable currentOutputKind = OutputKind.Html
let InlineMultiformatBlock(html, latex) =
  let block =
    { new MarkdownEmbedParagraphs with
        member x.Render() =
          if currentOutputKind = OutputKind.Html then [ InlineBlock html ] else [ InlineBlock latex ] }
  EmbedParagraphs(block)

let MathDisplay(latex) = Span [ LatexDisplayMath latex ]

/// Builds FSI evaluator that can render System.Image, F# Charts, series & frames
let wrapFsiEvaluator root output (floatFormat:string) (fsiEvaluator:FsiEvaluator) (config:FormatConfig) =

  /// Counter for saving files
  let createCounter () = 
    let count = ref 0
    (fun () -> incr count; !count)
  let imageCounter = createCounter ()
  let foogleCounter = createCounter ()

  let transformation (value:obj, typ:System.Type) =
    match value with 
    | :? System.Drawing.Image as img ->
        // Pretty print image - save the image to the "images" directory 
        // and return a DirectImage reference to the appropriate location
        let id = imageCounter().ToString()
        let file = "chart" + id + ".png"
        ensureDirectory (output @@ "images")
        img.Save(output @@ "images" @@ file, System.Drawing.Imaging.ImageFormat.Png) 
        Some [ Paragraph [DirectImage ("", (root + "/images/" + file, None))]  ]

    | :? GoogleCharts.GoogleChart as ch ->
        // Just return the inline HTML of a Google chart
        let ch = ch |> XPlot.GoogleCharts.Chart.WithSize(600, 300)
        Some [ InlineBlock ch.InlineHtml ]

    | :? Plotly.PlotlyChart as ch ->
        // Just return the inline HTML for a Plotly chart
        Some [ InlineBlock (ch.GetInlineHtml()) ]

    | :? ChartTypes.GenericChart as ch ->
        // Pretty print F# Chart - save the chart to the "images" directory 
        // and return a DirectImage reference to the appropriate location
        let id = imageCounter().ToString()
        let file = "chart" + id + ".png"
        ensureDirectory (output @@ "images")
      
        // We need to reate host control, but it does not have to be visible
        ( use ctl = new ChartControl(chartStyle ch, Dock = DockStyle.Fill, Width=1200, Height=720)
          ch.CopyAsBitmap().Save(output @@ "images" @@ file, System.Drawing.Imaging.ImageFormat.Png) )
        Some [ Paragraph [DirectImage ("", (root + "/images/" + file, None))]  ]

    | :? FoogleChart as fch -> 

        // TODO: Does not work for LaTex!
        let fch = Foogle.Formatting.Google.CreateGoogleChart(fch)

        let count = foogleCounter()
        let id = "foogle_" + count.ToString()
        let data = fch.Data.ToString(FSharp.Data.JsonSaveOptions.DisableFormatting)
        let opts = fch.Options.ToString(FSharp.Data.JsonSaveOptions.DisableFormatting)

        let script =
          [ "foogleCharts.push(function() {"
            sprintf "var data = google.visualization.arrayToDataTable(%s);" data
            sprintf "var options = %s;" opts
            sprintf "var chart = new google.visualization.%s(document.getElementById('%s'));" fch.Kind id
            "chart.draw(data, options);"
            "});" ]
        let htmlChart = 
          """<script type="text/javascript">""" + (String.concat "\n" script) + "</script>" +
          (sprintf "<div id=\"%s\" style=\"height:400px; margin:0px 45px 0px 25px\"></div>" id)
         
        let htmlOnce = """
          <script type="text/javascript">
            var foogleCharts = []
            function foogleInit() {
              for (var i = 0; i < foogleCharts.length; i++) {
                foogleCharts[i]();
              }
            }
            google.load('visualization', '1', { 'packages': ['corechart','geochart'] });
            google.setOnLoadCallback(foogleInit);
          </script>"""

        let html = if count = 1 then htmlOnce + htmlChart else htmlChart
        [ InlineBlock(html) ] |> Some

    | SeriesValues s ->
        // Pretty print series!
        let heads  = s |> mapSteps config.sitms fst (function Some k -> td (k.ToString()) | _ -> td " ... ")
        let row    = s |> mapSteps config.sitms snd (function Some v -> formatValue floatFormat "N/A" (OptionalValue.asOption v) | _ -> td " ... ")
        let aligns = s |> mapSteps config.sitms id (fun _ -> AlignDefault)
        [ InlineMultiformatBlock("<div class=\"deedleseries\">", "\\vspace{1em}")
          TableBlock(Some ((td "Keys")::heads), AlignDefault::aligns, [ (td "Values")::row ]) 
          InlineMultiformatBlock("</div>","\\vspace{1em}") ] |> Some

    | :? IFrame as f ->
      // Pretty print frame!
      {new IFrameOperation<_> with
        member x.Invoke(f) = 
          let heads  = f.ColumnKeys |> mapSteps config.fcols id (function Some k -> td (k.ToString()) | _ -> td " ... ")
          let aligns = f.ColumnKeys |> mapSteps config.fcols id (fun _ -> AlignDefault)
          let rows = 
            f.Rows |> Series.observationsAll |> mapSteps config.frows id (fun item ->
              let def, k, data = 
                match item with 
                | Some(k, Some d) -> "N/A", k.ToString(), Series.observationsAll d |> Seq.map snd 
                | Some(k, _) -> "N/A", k.ToString(), f.ColumnKeys |> Seq.map (fun _ -> None)
                | None -> " ... ", " ... ", f.ColumnKeys |> Seq.map (fun _ -> None)
              let row = data |> mapSteps config.fcols id (function Some v -> formatValue floatFormat def v | _ -> td " ... ")
              (td k)::row )
          Some [ 
            InlineMultiformatBlock("<div class=\"deedleframe\">","\\vspace{1em}")
            TableBlock(Some ([]::heads), AlignDefault::aligns, rows) 
            InlineMultiformatBlock("</div>","\\vspace{1em}")
          ] }
      |> f.Apply

    | :? Matrix<float> as m -> Some [ MathDisplay (m |> formatMatrix config (formatMathValue floatFormat)) ]
    | :? Matrix<float32> as m -> Some [ MathDisplay (m |> formatMatrix config (formatMathValue floatFormat)) ]
    | :? Vector<float> as v -> Some [ MathDisplay (v |> formatVector config (formatMathValue floatFormat)) ]
    | :? Vector<float32> as v -> Some [ MathDisplay (v |> formatVector config (formatMathValue floatFormat)) ]

    | _ -> None 
    
  // Create FSI evaluator, register transformations & return
  fsiEvaluator.RegisterTransformation(transformation)
  let fsiEvaluator = fsiEvaluator :> IFsiEvaluator
  { new IFsiEvaluator with
      member x.Evaluate(text, asExpr, file) = 
        captureDevice (fun () -> 
          fsiEvaluator.Evaluate(text, asExpr, file))

      member x.Format(res, kind) = 
        let res = res :?> ExtraEvaluationResult
        match kind, res.CapturedImage with
        | FsiEmbedKind.Output, Some img -> 
            [ match (res.Results :?> FsiEvaluationResult).Output with
              | Some s  when not (String.IsNullOrWhiteSpace(s)) ->
                  yield! fsiEvaluator.Format(res.Results, kind)
              | _ -> ()
              yield! transformation(img, typeof<Image>).Value ]
        | _ -> fsiEvaluator.Format(res.Results, kind) }