namespace FSharp.Literate.Scripts

open System.IO
open FSharp.Literate
open FSharp.Markdown
open System.Reflection
open System.Collections.Generic

/// Represents state passed around during processing
type ProcessingContext = 
  { /// Root path with journals (see comment on `ProcessingContext.Create`)
    Root : string 

    /// Output folder (see comment on `ProcessingContext.Create`)
    Output : string

    /// What kind of output should be produced
    OutputKind : OutputKind 

    /// Template location (see comment on `ProcessingContext.Create`)
    Styles : string option 

    /// Should the output be standalone (without any background servers)?
    Standalone : bool

    /// Set this if you only want to process files in this list.
    /// (For example, use `["MyJournal.fsx"]` to only generate this one jounral)
    FileWhitelist : string list option

    /// Specify an error handler when evaluation fails
    FailedHandler : FsiEvaluationFailedInfo -> unit 
  }

  /// Creates a default processing context with all the 
  /// basic things needed to produce journals. 
  ///
  /// ## Parameters
  ///  - `root` is the root folder where you have your `*.fsx` journals
  static member Create(root) = 
    { Root = root 
      Output = Path.Combine(root, "output")
      OutputKind = OutputKind.Html 
      Styles = Some (if Directory.Exists (root + "/styles") then  root 
                     elif Directory.Exists (root + "/../styles") then  root + "/.."
                     else failwith "couldn't find styles")
      FileWhitelist = None 
      Standalone = false
      FailedHandler = ignore }

module ScriptProcessing =

  /// Correctly combine two paths
  let (@@) a b = Path.Combine(a, b)

  /// Ensure that directory exists
  let ensureDirectory path =
    let dir = DirectoryInfo(path)
    if not dir.Exists then dir.Create()

  /// Copy all files from source to target
  let rec copyFiles source target =
    ensureDirectory target
    for f in Directory.GetDirectories(source) do
      copyFiles f (target @@ Path.GetFileName(f))
    for f in Directory.GetFiles(source) do
      let targetf = target @@ Path.GetFileName(f)
      if not (File.Exists(targetf)) then File.Copy(f, targetf, true)

  /// Lookup a specified key in a dictionary, possibly
  /// ignoring newlines or spaces in the key.
  let (|LookupKey|_|) (dict:IDictionary<_, _>) (key:string) =
    [ key; key.Replace("\r\n", ""); key.Replace("\r\n", " ");
      key.Replace("\n", ""); key.Replace("\n", " ") ]
    |> Seq.tryPick (fun key ->
      match dict.TryGetValue(key) with
      | true, v -> Some v
      | _ -> None)

  // Process scripts in the 'root' directory and put them into output
  let htmlTemplate (ctx:ProcessingContext) = 
    File.ReadAllText(ctx.Output @@ "styles" @@ "template.html")

  let texTemplate (ctx:ProcessingContext) =  
    File.ReadAllText(ctx.Output @@ "styles" @@ "template.tex")

  // ----------------------------------------------------------------------------
  // Markdown document processing tools
  // ----------------------------------------------------------------------------

  /// Extract text from a list of spans such as document heading
  let rec extractText (spans:MarkdownSpans) =
    spans |> List.collect (function
      | Matching.SpanNode(_, spans) -> extractText spans
      | Literal text | InlineCode text -> [text]
      | _ -> [])

  /// Extract the text of the <h1> element
  let rec extractTitle (pars:MarkdownParagraphs) =
    pars |> List.tryPick (function
      | Heading(1, text) -> Some(String.concat " " (extractText text))
      | Matching.ParagraphNested(_, pars) -> extractTitle (List.concat pars)
      | _ -> None )

  /// In LaTeX documents, we use the first heading as document name
  /// so we can remove it from the rest of the document
  let rec dropTitle (pars:MarkdownParagraphs) : MarkdownParagraphs =
    pars |> List.collect (function
      | Heading(1, _text) -> []
      | Matching.ParagraphNested(o, pars) ->
          [ Matching.ParagraphNested(o, pars |> List.map dropTitle) ]
      | other -> [other] )

  /// When generating LaTeX, we need to save all files locally
  let rec downloadSpanImages (saver, links) = function
    | IndirectImage(body, _, LookupKey links (link, title))
    | DirectImage(body, (link, title)) -> DirectImage(body, (saver link, title))
    | Matching.SpanNode(s, spans) -> Matching.SpanNode(s, List.map (downloadSpanImages (saver, links)) spans)
    | Matching.SpanLeaf(l) -> Matching.SpanLeaf(l)

  let rec downloadImages ctx (pars:MarkdownParagraphs) : MarkdownParagraphs =
    pars |> List.map (function
      | Matching.ParagraphSpans(s, spans) ->
          Matching.ParagraphSpans(s, List.map (downloadSpanImages ctx) spans)
      | Matching.ParagraphNested(o, pars) ->
          Matching.ParagraphNested(o, List.map (downloadImages ctx) pars)
      | Matching.ParagraphLeaf p -> Matching.ParagraphLeaf p )

  /// Generate file for the specified document, using a given template and title
  let generateFile (ctx:ProcessingContext) path (doc:LiterateDocument) title head = 
    if ctx.OutputKind = OutputKind.Html then
      let template = htmlTemplate ctx
      let html =
        template.Replace("{head}", head)
                .Replace("{tooltips}", doc.FormattedTips)
                .Replace("{document}", Literate.WriteHtml(doc))
                .Replace("{page-title}", title)
      File.WriteAllText(path, html)
    else
      // Download images so that they can be embedded
      use wc = new System.Net.WebClient()
      let counter = ref 0
      let saver (url:string) =
        if url.StartsWith("http") || url.StartsWith("https") then
          incr counter
          let ext = Path.GetExtension(url)
          let fn = sprintf "./savedimages/saved%d%s" counter.Value ext
          wc.DownloadFile(url, ctx.Output @@ fn)
          fn
        else url

      ensureDirectory (ctx.Output @@ "savedimages" )
      let pars =
        doc.Paragraphs
        |> downloadImages (saver, doc.DefinedLinks)
        |> dropTitle

      let doc = doc.With(paragraphs = pars)
      let template = texTemplate ctx
      let tex =
        template.Replace("{tooltips}", doc.FormattedTips)
                .Replace("{contents}", Literate.WriteLatex(doc))
                .Replace("{page-title}", title)
      File.WriteAllText(Path.ChangeExtension(path, "tex"), tex)

  // ----------------------------------------------------------------------------
  // Process script files in the root folder & generate HTML files
  // ----------------------------------------------------------------------------

  /// Extend the `fsi` object with `fsi.AddHtmlPrinter` 
  /// ForwardInteractiveSettings comes from FAKE Yaaf scripting support. When your scripts are processed
  /// fsx2html literate programming support the scripts are actually processed using FAKE.exe 
  /// (FAKE version 4.x), which is a little mysterious but gives more hooks to intercept settings etc.
  ///
  /// https://github.com/fsharp/FAKE/blob/575330d8eab58177152bc79963b3b21599aef76e/paket-files/matthid/Yaaf.FSharp.Scripting/src/source/Yaaf.FSharp.Scripting/YaafFSharpScripting.fs#L1388


  let addHtmlPrinter = """
    module FsInteractiveService = 
      let mutable htmlPrinters = []
      let tryFormatHtml o = htmlPrinters |> Seq.tryPick (fun f -> f o)
      let htmlPrinterParams = System.Collections.Generic.Dictionary<string, obj>()
      do htmlPrinterParams.["html-standalone-output"] <- @html-standalone-output

    type __ReflectHelper.ForwardingInteractiveSettings with
      member x.HtmlPrinterParameters = FsInteractiveService.htmlPrinterParams
      member x.AddHtmlPrinter<'T>(f:'T -> seq<string * string> * string) = 
        FsInteractiveService.htmlPrinters <- (fun (value:obj) ->
          match value with
          | :? 'T as value -> Some(f value)
          | _ -> None) :: FsInteractiveService.htmlPrinters"""


  /// Create FSI evaluator - this loads `addHtmlPrinter` and calls the registered
  /// printers when processing outputs. Printed <head> elements are added to the
  /// returned ResizeArray
  let createFsiEvaluator ctx = 
    let fsi = new FsiEvaluator([| "--define:HAS_FSI_ADDHTMLPRINTER" |], FsiEvaluatorConfig.CreateNoOpFsiObject())
    fsi.EvaluationFailed.Add(printfn "%A")
    fsi.EvaluationFailed.Add(ctx.FailedHandler)
    try
      let addHtmlPrinter = addHtmlPrinter.Replace("@html-standalone-output", if ctx.Standalone then "true" else "false")
      match (fsi :> IFsiEvaluator).Evaluate(addHtmlPrinter, false, None) with
      | :? FsiEvaluationResult as res when res.ItValue.IsSome -> ()
      | _ -> failwith "Evaluating addHtmlPrinter code failed"
    with e ->
      printfn "%A" e
      reraise ()

    let tryFormatHtml =
      match (fsi :> IFsiEvaluator).Evaluate("(FsInteractiveService.tryFormatHtml : obj -> option<seq<string*string>*string>)", true, None) with
      | :? FsiEvaluationResult as res -> 
          let func = unbox<obj -> option<seq<string*string>*string>> (fst res.Result.Value)
          fun (o:obj) -> func o
      | _ -> failwith "Failed to get tryFormatHtml function"

    let head = new ResizeArray<_>()
    fsi.RegisterTransformation(fun (o, _t) ->
      match tryFormatHtml o with
      | Some (args, html) -> 
          for _k, v in args do if not (head.Contains(v)) then head.Add(v)
          Some [InlineBlock("<div class=\"fslab-html-output\">" + html + "</div>")]
      | None -> None )

    fsi :> IFsiEvaluator, head


  /// Creates the 'output' directory and puts all formatted script files there
  let processScriptFiles overwrite ctx =
    // Ensure 'output' directory exists
    let root = ctx.Root
    ensureDirectory ctx.Output

    // use the provided template location or use one in the NuGet package source
    let templateLocation =
      match ctx.Styles with
      | Some loc -> loc
      | _ ->
        let rootPackages =
          if Directory.Exists(root @@ "packages") then root @@ "packages"
          else root @@ "../packages"
        Directory.GetDirectories(rootPackages) |> Seq.find (fun p ->
          Path.GetFileName(p).StartsWith "fsx2html")

    // Copy content of 'styles' to the output
    copyFiles (templateLocation @@ "styles") (ctx.Output @@ "styles")
    // Create fsi evaluator & resize array collecting <head> elements
    let fsi, headElements = createFsiEvaluator ctx

    /// Recursively process all files in the directory tree
    let processDirectory indir outdir =
      ensureDirectory outdir
      // Get all *.fsx and *.md files and yield functions to parse them
      // If a whitelist exist, use only files in whitelist
      let filterWhitelist (file:string) : bool =
          match ctx.FileWhitelist with
           | Some(files) -> files |> List.exists(fun f -> file.EndsWith(f))
           | None -> true
      let files =
        [ for f in Directory.GetFiles(indir, "*.fsx") |> Array.filter filterWhitelist do
            if Path.GetFileNameWithoutExtension(f).ToLower() <> "build" then
              yield f, fun () -> Literate.ParseScriptFile(f, fsiEvaluator=fsi)
          for f in Directory.GetFiles(indir, "*.md") |> Array.filter filterWhitelist  do
            yield f, fun () -> Literate.ParseMarkdownFile(f, fsiEvaluator=fsi) ]

      // If whitelist is specified, skip all files not in the white list
      let files =
        match ctx.FileWhitelist with
        | None -> files
        | Some(xs)->
            let wfs = set xs
            files |> List.filter (fun (f, _) -> wfs.Contains(Path.GetFileName(f)))

      // Process all the files that have not changed since the last time
      [ for file, func in files do
          headElements.Clear()
          let name = Path.GetFileNameWithoutExtension(file)
          let output = outdir @@ (name + ".html")

          // When overwrite is specified, we always regenerate (useful on first run
          // because then the function returns all files with their titles too!)
          let changeTime = File.GetLastWriteTime(file)
          let generateTime = File.GetLastWriteTime(output)
          if overwrite || changeTime > generateTime then
            // Parse the document, save it & return its name with a title
            printfn "Generating '%s.html'" name
            let doc = func ()
            let title = defaultArg (extractTitle doc.Paragraphs) "Untitled"
            generateFile ctx output doc title (String.concat "\n" headElements)
            yield (name + ".html"), title ]

    processDirectory root ctx.Output


  // ----------------------------------------------------------------------------
  // Startup script to generate default file
  // ----------------------------------------------------------------------------

  /// Find or generate a default file that we want to show in browser
  let getDefaultFile ctx = function
    | [] -> failwith "No script files found!"
    | [file, _] -> file // If there is just one file, return it
    | generated ->
        // If there is custom default or index file, use it
        let existingDefault =
          Directory.GetFiles(ctx.Root) |> Seq.tryPick (fun f ->
            match Path.GetFileNameWithoutExtension(f).ToLower() with
            | "default" | "index" -> Some(Path.GetFileNameWithoutExtension(f) + ".html")
            | _ -> None)
        match existingDefault with
        | None ->
            // Otherwise, generate simple page with list of all files
            let items =
              [ for file, title in generated ->
                  [Paragraph [ DirectLink([Literal title], (file,None)) ]] ]
            let pars =
              [ Heading(1, [Literal "FSharp Literate Scripts"])
                ListBlock(Unordered, items) ]
            let doc = LiterateDocument(pars, "", dict[], LiterateSource.Markdown "", "", Seq.empty)
            generateFile ctx (ctx.Output @@ "index.html") doc "FSharp Literate Scripts" ""
            "index.html"
        | Some fn -> fn

  /// Process journals as specified by the provided `ProcessingContext`
  /// and return a list of names and headings of the journals
  let processJournals ctx = processScriptFiles true ctx

  /// Update all journals. Call this after `processJournals` to 
  /// regenerate journals that have been changed.
  let updateJournals ctx = processScriptFiles false ctx |> ignore

  /// Get default "index" journal. If there are multiple journals,
  /// this generates a new index file. If there is one, returns it.
  let getIndexJournal ctx files = getDefaultFile ctx files