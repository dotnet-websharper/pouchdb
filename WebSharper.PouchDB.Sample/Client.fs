namespace WebSharper.PouchDB.Sample

open WebSharper
open WebSharper.JavaScript
open WebSharper.UI
open WebSharper.UI.Client
open WebSharper.UI.Templating
open WebSharper.PouchDB

[<JavaScript>]
module Client =

    // The template is the whole document, loaded from the DOM.
    type IndexTemplate = Template<"index.html", ClientLoad.FromDocument>

    /// The content of a note. PouchDB stores plain JSON objects and adds `_id`
    /// and `_rev` to them, so the type parameter of `Database` is the document
    /// shape without the metadata; `metaOf` reads it back through
    /// `DocumentMeta`.
    type Note =
        {
            Text : string
            Done : bool
            Created : float
        }

    let dbName = "websharper-notes"
    let backupName = "websharper-notes-backup"
    let attachmentId = "attachment-demo"
    let attachmentName = "note.txt"

    // ------------------------------------------------------------------
    // Page state
    // ------------------------------------------------------------------

    let status = Var.Create "Starting…"
    let versionText = Var.Create "-"
    let adapterText = Var.Create "-"
    let infoText = Var.Create "-"
    let docText = Var.Create "(no documents yet)"
    let bulkText = Var.Create ""
    let attachmentText = Var.Create "-"
    let changesText = Var.Create "(no changes yet)"
    let queryText = Var.Create "-"
    let replicationText = Var.Create "-"
    let noteText = Var.Create ""
    let theme = Var.Create "light"
    let themeLabel = Var.Create "🌙 Dark mode"

    let mutable db : option<Database<Note>> = None
    let mutable docs : Note[] = [||]
    let mutable liveChanges : option<Changes<Note>> = None

    // ------------------------------------------------------------------
    // Theme: light is the default; the choice is persisted in localStorage.
    // The storage key is shared with the bootstrap script in index.html.
    // ------------------------------------------------------------------

    let themeStorageKey = "websharper-pouchdb-theme"

    let applyTheme () =
        JS.Document.DocumentElement.SetAttribute("data-theme", theme.Value)
        themeLabel.Value <-
            if theme.Value = "dark" then "☀️ Light mode" else "🌙 Dark mode"

    let toggleTheme () =
        theme.Value <- (if theme.Value = "dark" then "light" else "dark")
        try JS.Window.LocalStorage.SetItem(themeStorageKey, theme.Value) with _ -> ()
        applyTheme ()

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    let setStatus (line : string) =
        status.Value <- line

    /// `true` when a JavaScript value is neither `undefined` nor `null`.
    let isSet (x : obj) =
        not (x ==. JS.Undefined)

    let errorText (e : obj) =
        let err = As<PouchError> e
        if isSet (box err.Name) then err.Name + ": " + err.Message
        else "unexpected error"

    /// Runs a promise, reporting a rejection in the status line.
    let fireP (p : Promise<'T>) =
        p.Then(
            (fun (_ : 'T) -> ()),
            (fun (e : obj) -> setStatus ("Error — " + errorText e))
        ) |> ignore

    let metaOf (note : Note) =
        As<DocumentMeta> (box note)

    // Writes must not carry `_rev` when the document is new (PouchDB rejects an
    // empty revision), so the documents passed to PouchDB are built as plain JS
    // objects instead of F# records.
    let newNote (text : string) : Note =
        New [
            "Text" => text
            "Done" => false
            "Created" => Date.Now()
        ]

    let newNoteWithId (id : string) (text : string) (done' : bool) : Note =
        New [
            "_id" => id
            "Text" => text
            "Done" => done'
            "Created" => Date.Now()
        ]

    let updated (note : Note) (done' : bool) : Note =
        let meta = metaOf note
        New [
            "_id" => meta._id
            "_rev" => meta._rev
            "Text" => note.Text
            "Done" => done'
            "Created" => note.Created
        ]

    let renderDocs () =
        if docs.Length = 0 then
            "(no documents)"
        else
            docs
            |> Array.map (fun note ->
                let meta = metaOf note
                let text = if isSet (box note.Text) then note.Text else "(no text)"
                (if note.Done then "[x] " else "[ ] ") + meta._id + "  " + text + "  (" + meta._rev + ")")
            |> String.concat "\n"

    let describeAdapters () =
        [| "idb"; "http"; "https"; "websql"; "memory"; "localstorage"; "leveldb" |]
        |> Array.map (fun name ->
            let found = PouchDB.Adapters.Get(name)
            if Optional.isDefined found && found.Value.Valid() then name + " ✓" else name + " –")
        |> String.concat "   "

    let withDb (f : Database<Note> -> unit) =
        match db with
        | Some database -> f database
        | None -> setStatus "Open the database first."

    // ------------------------------------------------------------------
    // Operations
    // ------------------------------------------------------------------

    let refresh (database : Database<Note>) : Promise<unit> =
        promise {
            let! res = database.AllDocs(AllDocsOptions(Include_docs = true, Limit = 100))
            docs <- res.Rows |> Array.map (fun row -> row.Doc)
            docText.Value <- renderDocs ()
            let! info = database.Info()
            infoText.Value <-
                info.Db_name + " · " + string info.Doc_count + " documents · update_seq " + string info.Update_seq
        }

    let openDb () =
        try
            let instance =
                new Database<Note>(
                    dbName,
                    PouchDBOptions(
                        Auto_compaction = true,
                        Revs_limit = 50
                    )
                )
            db <- Some instance
            versionText.Value <- "PouchDB " + PouchDB.Version
            adapterText.Value <- describeAdapters () + "   (this database: " + instance.Type() + ")"
            fireP (refresh instance)
            setStatus ("Opened \"" + instance.Name + "\" — add a note below.")
        with e ->
            db <- None
            setStatus ("Could not open the database: " + e.Message)

    let showInfo () =
        withDb (fun database ->
            // The node-style callback form, to exercise the non-promise API.
            database.Info(fun (err : exn) (info : DatabaseInfo) ->
                if isSet (box err) then setStatus ("Info failed: " + err.Message)
                else
                    infoText.Value <-
                        info.Db_name + " · " + string info.Doc_count + " documents · update_seq " + string info.Update_seq
                    setStatus "Info() came back through the callback."))

    let compact () =
        withDb (fun database ->
            fireP (promise {
                let! res = database.Compact()
                setStatus ("Compaction finished (ok = " + string res.Ok + ").")
            }))

    let addNote () =
        withDb (fun database ->
            let text =
                if noteText.Value.Trim() = "" then
                    "Note at " + string (Date.Now() |> int)
                else
                    noteText.Value
            fireP (promise {
                let! res = database.Post(newNote text)
                noteText.Value <- ""
                do! refresh database
                setStatus ("Posted " + res.Id + " (rev " + res.Rev + ").")
            }))

    let addThree () =
        withDb (fun database ->
            let stamp = string (Date.Now() |> int)
            let batch =
                [| 1; 2; 3 |]
                |> Array.map (fun i ->
                    newNoteWithId ("bulk-" + stamp + "-" + string i) ("Bulk note " + string i) (i = 2))
            fireP (promise {
                let! results = database.BulkDocs(batch)
                let written = results |> Array.filter (fun r -> r.Ok) |> Array.length
                let failed =
                    results
                    |> Array.filter (fun r -> not r.Ok)
                    |> Array.map (fun r -> r.Error + ": " + r.Reason)
                do! refresh database
                bulkText.Value <-
                    "BulkDocs wrote " + string written + " of " + string results.Length
                    + (if failed.Length = 0 then "" else " · failures: " + String.concat ", " failed)
                setStatus "BulkDocs finished."
            }))

    let refreshAll () =
        withDb (fun database ->
            fireP (promise {
                do! refresh database
                setStatus "Reloaded with AllDocs."
            }))

    let getFirst () =
        withDb (fun database ->
            if docs.Length = 0 then setStatus "There are no documents yet."
            else
                let meta = metaOf docs.[0]
                fireP (promise {
                    let! note = database.Get(meta._id)
                    setStatus ("Get " + meta._id + " → \"" + note.Text + "\" (done = " + string note.Done + ").")
                }))

    let toggleFirst () =
        withDb (fun database ->
            if docs.Length = 0 then setStatus "There are no documents yet."
            else
                let note = docs.[0]
                let meta = metaOf note
                fireP (promise {
                    let! res = database.Put(updated note (not note.Done))
                    do! refresh database
                    setStatus ("Updated " + meta._id + " to rev " + res.Rev + ".")
                }))

    let deleteFirst () =
        withDb (fun database ->
            if docs.Length = 0 then setStatus "There are no documents yet."
            else
                let meta = metaOf docs.[0]
                fireP (promise {
                    let! res = database.Remove(meta._id, meta._rev)
                    do! refresh database
                    setStatus ("Removed " + res.Id + " (rev " + res.Rev + ").")
                }))

    let keysDemo () =
        withDb (fun database ->
            if docs.Length = 0 then setStatus "There are no documents yet."
            else
                let ids = docs |> Array.map (fun note -> (metaOf note)._id)
                let keys = Array.append (Array.truncate 2 ids) [| "does-not-exist" |]
                fireP (promise {
                    let! res = database.AllDocs(AllDocsOptions(Include_docs = true, Keys = keys))
                    bulkText.Value <-
                        res.Rows
                        |> Array.map (fun row ->
                            if isSet (box row.Error) then row.Key + " → missing"
                            else row.Key + " → " + row.Doc.Text)
                        |> String.concat "   "
                    setStatus ("AllDocs(keys) returned " + string res.Rows.Length + " rows.")
                }))

    let bulkGetDemo () =
        withDb (fun database ->
            if docs.Length = 0 then setStatus "There are no documents yet."
            else
                let pairs =
                    docs
                    |> Array.truncate 2
                    |> Array.map (fun note -> BulkGetDoc((metaOf note)._id))
                fireP (promise {
                    let! res = database.BulkGet(BulkGetOptions(pairs))
                    bulkText.Value <-
                        res.Results
                        |> Array.map (fun result ->
                            let entry = result.Docs.[0]
                            if isSet (box entry.Ok) then result.Id + " ✓" else result.Id + " ✗")
                        |> String.concat "   "
                    setStatus ("BulkGet returned " + string res.Results.Length + " entries.")
                }))

    let revsDiffDemo () =
        withDb (fun database ->
            if docs.Length = 0 then setStatus "There are no documents yet."
            else
                let meta = metaOf docs.[0]
                let diff = New [ meta._id => box [| meta._rev; "1-bogus" |] ]
                fireP (promise {
                    let! res = database.RevsDiff(diff)
                    bulkText.Value <- "RevsDiff → " + JSON.Stringify res
                    setStatus "RevsDiff reports the revisions the database does not have."
                }))

    let attach () =
        withDb (fun database ->
            let content =
                new Blob(
                    [| "Hello from WebSharper + PouchDB" |],
                    BlobPropertyBag(Type = "text/plain")
                )
            fireP (promise {
                let! res = database.PutAttachment(attachmentId, attachmentName, content, "text/plain")
                do! refresh database
                attachmentText.Value <-
                    "Attached " + attachmentName + " (" + string content.Size
                    + " bytes) to " + res.Id + " at rev " + res.Rev + "."
                setStatus "Attachment written."
            }))

    let readAttachment () =
        withDb (fun database ->
            fireP (promise {
                let! blob = database.GetAttachment(attachmentId, attachmentName)
                attachmentText.Value <- "Read back " + string blob.Size + " bytes of type " + blob.Type + "."
                setStatus "Attachment read as a Blob."
            }))

    let changesOnce () =
        withDb (fun database ->
            changesText.Value <- ""
            let feed = database.Changes(ChangesOptions(Include_docs = true, Limit = 20))
            feed.OnChange(fun change ->
                    changesText.Value <-
                        changesText.Value + "change " + change.Id + " seq " + string change.Seq + "\n")
                .OnComplete(fun res ->
                    changesText.Value <-
                        changesText.Value + "complete — " + string res.Results.Length + " changes, last_seq " + string res.Last_seq + "\n")
                .OnError(fun e ->
                    changesText.Value <- changesText.Value + "error — " + e.Message + "\n")
            |> ignore
            fireP feed
            setStatus "One-shot changes feed started.")

    let cancelChanges () =
        match liveChanges with
        | Some feed ->
            feed.Cancel()
            liveChanges <- None
            changesText.Value <- changesText.Value + "cancelled\n"
        | None -> ()

    let changesLive () =
        withDb (fun database ->
            cancelChanges ()
            changesText.Value <- ""
            let feed = database.Changes(ChangesOptions(Live = true, Include_docs = true))
            liveChanges <- Some feed
            feed.OnChange(fun change ->
                    changesText.Value <-
                        changesText.Value + "live change " + change.Id + " seq " + string change.Seq + "\n")
                .OnError(fun e ->
                    changesText.Value <- changesText.Value + "feed error — " + e.Message + "\n")
            |> ignore
            fireP feed
            setStatus "Live changes feed started — write a note to see it.")

    let queryMapOnly () =
        withDb (fun database ->
            fireP (promise {
                let! res =
                    database.Query(
                        (fun (note : Note) (emit : Emitter<string, float>) ->
                            emit.Call((if note.Done then "done" else "todo"), (if note.Done then 1.0 else 0.0))),
                        QueryOptions<string>(Include_docs = false)
                    )
                queryText.Value <-
                    res.Rows
                    |> Array.map (fun row -> row.Key + " → " + string row.Value)
                    |> String.concat "\n"
                setStatus ("Temporary map-only view: " + string res.Total_rows + " rows in the index.")
            }))

    let queryCount () =
        withDb (fun database ->
            let view =
                MapReduceBuiltin<string, float, Note>(
                    (fun (note : Note) (emit : Emitter<string, float>) ->
                        emit.Call((if note.Done then "done" else "todo"), 1.0)),
                    BuiltinReduce._count
                )
            fireP (promise {
                let! res = database.Query<string, float, float>(view, QueryOptions<string>(Group = true))
                queryText.Value <-
                    res.Rows
                    |> Array.map (fun row -> string row.Key + " → " + string row.Value)
                    |> String.concat "\n"
                setStatus "Temporary map/reduce view, reduced with _count."
            }))

    let replicate () =
        withDb (fun database ->
            replicationText.Value <- ""
            let replication = database.Replicate.To(backupName)
            replication
                .OnChange(fun info ->
                    replicationText.Value <-
                        replicationText.Value + "replicate — " + string info.Docs_written + " written, seq " + string info.Last_seq + "\n")
                .OnComplete(fun res ->
                    replicationText.Value <-
                        replicationText.Value + "replicate complete — " + string res.Docs_read + " read, " + res.Status + "\n")
                .OnError(fun e ->
                    replicationText.Value <- replicationText.Value + "replicate error — " + e.Message + "\n")
            |> ignore
            fireP replication
            setStatus ("Replicating " + dbName + " → " + backupName + "."))

    let sync () =
        withDb (fun database ->
            replicationText.Value <- ""
            let syncFeed = database.Sync(backupName)
            syncFeed
                .OnChange(fun info ->
                    replicationText.Value <-
                        replicationText.Value + "sync — " + string info.Docs_read + " read, " + string info.Docs_written + " written\n")
                .OnComplete(fun res ->
                    replicationText.Value <-
                        replicationText.Value + "sync complete — push " + string res.Push.Docs_written
                        + " written, pull " + string res.Pull.Docs_written + " written\n")
                .OnError(fun e ->
                    replicationText.Value <- replicationText.Value + "sync error — " + e.Message + "\n")
            |> ignore
            fireP syncFeed
            setStatus ("Syncing " + dbName + " ↔ " + backupName + "."))

    let destroyBackup () =
        // PouchDB 9 dropped the static `PouchDB.destroy(name)`: open the database
        // and destroy the instance instead.
        let backup = new Database<Note>(backupName)
        fireP (promise {
            let! res = backup.Destroy()
            replicationText.Value <- "Backup destroyed (ok = " + string res.Ok + ").\n"
            setStatus "The backup database is gone; replicate again to recreate it."
        })

    // ------------------------------------------------------------------
    // Entry point
    // ------------------------------------------------------------------

    [<SPAEntryPoint>]
    let Main () =
        try
            let stored = JS.Window.LocalStorage.GetItem(themeStorageKey)
            if stored = "dark" || stored = "light" then theme.Value <- stored
        with _ -> ()
        applyTheme ()

        // The static API: `PouchDB.version`, `PouchDB.adapters`, the
        // `created`/`destroyed` events, `PouchDB.destroy`, `replicate`, `sync`.
        PouchDB.On(DatabaseEvent.Created, fun (name : string) ->
            setStatus ("PouchDB created the database \"" + name + "\"."))
        |> ignore

        IndexTemplate()
            .status(status.View)
            .versionText(versionText.View)
            .adapterText(adapterText.View)
            .infoText(infoText.View)
            .docText(docText.View)
            .bulkText(bulkText.View)
            .attachmentText(attachmentText.View)
            .changesText(changesText.View)
            .queryText(queryText.View)
            .replicationText(replicationText.View)
            .themeLabel(themeLabel.View)
            .noteText(noteText)
            .ToggleTheme(fun _ -> toggleTheme ())
            .OpenDb(fun _ -> openDb ())
            .Info(fun _ -> showInfo ())
            .Compact(fun _ -> compact ())
            .AddNote(fun _ -> addNote ())
            .AddThree(fun _ -> addThree ())
            .Refresh(fun _ -> refreshAll ())
            .GetFirst(fun _ -> getFirst ())
            .ToggleFirst(fun _ -> toggleFirst ())
            .DeleteFirst(fun _ -> deleteFirst ())
            .KeysDemo(fun _ -> keysDemo ())
            .BulkGetDemo(fun _ -> bulkGetDemo ())
            .RevsDiffDemo(fun _ -> revsDiffDemo ())
            .Attach(fun _ -> attach ())
            .ReadAttachment(fun _ -> readAttachment ())
            .ChangesOnce(fun _ -> changesOnce ())
            .ChangesLive(fun _ -> changesLive ())
            .CancelChanges(fun _ -> cancelChanges ())
            .QueryMapOnly(fun _ -> queryMapOnly ())
            .QueryCount(fun _ -> queryCount ())
            .Replicate(fun _ -> replicate ())
            .Sync(fun _ -> sync ())
            .DestroyBackup(fun _ -> destroyBackup ())
            .Bind()

        openDb ()