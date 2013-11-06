open System.IO

let packages = [ "FSharp.Charting.0.87/lib/net40", ["System.Windows.Forms.DataVisualization.dll"; "FSharp.Charting.dll"]
                 "FSharp.Data.1.1.10/lib/net40", ["FSharp.Data.dll"]
                 "Deedle.0.9.4-beta/lib/net40", ["Deedle.dll"]
                 "MathNet.Numerics.2.6.2/lib/net40", ["MathNet.Numerics.dll"; "MathNet.Numerics.IO.dll"]
                 "MathNet.Numerics.FSharp.2.6.0/lib/net40", ["MathNet.Numerics.FSharp.dll"] ]

let rProviderPackage =  ["RProvider.1.0.3/lib", ["RDotNet.dll"; "RProvider.dll"] ]

let folders = [ "packages/"           // new F# project in VS with create directory for solution disabled
                "../packages/"        // new F# project in VS with create directory for solution enabled
                "../../packages/"     // fsharp-project-scaffold template
                "../../../packages/"] // just in case

let nowarn = ["#nowarn \"211\""]

let setupCharting = ["#load \"SetupCharting.fsx\""]

// If R is not installed, just doing #r "RProvider.dll" will raise and error, so we need two scripts
let generateScript includeRProvider = 

    let packages = if includeRProvider then packages @ rProviderPackage else packages

    let includes = 
        folders 
        |> List.collect (fun folder -> 
            packages 
            |> List.map fst
            |> List.map (fun package -> folder + package))
        |> List.map (sprintf "#I \"%s\"")


    let references = 
        packages
        |> List.collect snd
        |> List.map (sprintf "#r \"%s\"")

    File.WriteAllLines(__SOURCE_DIRECTORY__ + (if includeRProvider then "/FsLabWithR.fsx" else "/FsLab.fsx"), nowarn @ includes @ references @ setupCharting)

generateScript true
generateScript false
