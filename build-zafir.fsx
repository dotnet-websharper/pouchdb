#load "tools/includes.fsx"
open IntelliFactory.Build

let bt =
    BuildTool().PackageId("WebSharper.PouchDB")
        .VersionFrom("WebSharper")
        .WithFramework(fun fw -> fw.Net40)


let main =
    bt.WebSharper4.Extension("WebSharper.PouchDB")
        .Embed(["pouchdb.min.js"; "lie.min.js"])
        .SourcesFromProject()

(*let test =
    (bt.WebSharper.BundleWebsite("WebSharper.PouchDB.Tests")
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
