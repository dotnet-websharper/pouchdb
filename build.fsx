#load "tools/includes.fsx"
open IntelliFactory.Build

let bt =
    (BuildTool().PackageId("WebSharper.PouchDB", "2.5")
    |> fun bt -> bt.WithFramework(bt.Framework.Net40))


let main =
    (bt.WebSharper.Extension("IntelliFactory.WebSharper.PouchDB")
    |> FSharpConfig.BaseDir.Custom "websharper.pouchdb")
        .Embed(["pouchdb.min.js"])
        .SourcesFromProject("websharper.pouchdb.fsproj")

(*let test =
    (bt.WebSharper.BundleWebsite("IntelliFactory.WebSharper.PouchDB.Tests")
    |> FSharpConfig.BaseDir.Custom "Tests")
        .SourcesFromProject("Tests.fsproj")
        .References(fun r -> [r.Project main])*)

bt.Solution [
    main
    //test

    bt.NuGet.CreatePackage()
        .Configure(fun c ->
            { c with
                Title = Some "WebSharper.PouchDB"
                LicenseUrl = Some "http://websharper.com/licensing"
                ProjectUrl = Some "https://bitbucket.org/intellifactory/websharper.pouchdb"
                Description = "WebSharper Extensions for PouchDB"
                Authors = ["IntelliFactory"]
                RequiresLicenseAcceptance = true })
        .Add(main)

]
|> bt.Dispatch
