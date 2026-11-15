# WebSharper.PouchDB

[![NuGet](https://img.shields.io/nuget/v/WebSharper.PouchDB.svg)](https://www.nuget.org/packages/WebSharper.PouchDB)

WebSharper bindings for [PouchDB](https://pouchdb.com/), the client-side database
that speaks the CouchDB replication protocol and stores documents in IndexedDB,
tracking **PouchDB 9.0**.

```fsharp
open WebSharper.PouchDB

type Note =
    {
        Text : string
        Done : bool
    }

let db = new Database<Note>("notes")

promise {
    let! _ = db.Put({ Text = "Try PouchDB"; Done = false })
    let! all = db.AllDocs(AllDocsOptions(Include_docs = true))
    for row in all.Rows do
        Console.Log(row.Doc.Text)
}
|> ignore
```

This repository contains:

| Project | Purpose |
| --- | --- |
| `WebSharper.PouchDB` | The binding: WIG definitions for `Database`, the option/response shapes, `Changes`, `Replication`/`Sync`, map/reduce views and the `PouchDB` module API. |
| `WebSharper.PouchDB.Sample` | A static SPA that exercises the whole surface against a real IndexedDB database. |

## Live demos

**Try it live:** [Open the sample in your browser](https://dotnet-websharper.github.io/pouchdb/)

![PouchDB sample](docs/sample.png)

The sample doubles as the binding's [test case](#running-the-test-case): it opens
a database, writes/updates/deletes documents (individually, in bulk and through
`allDocs`/`bulkGet`/`revsDiff`), stores and reads back a `Blob` attachment, runs a
live and a one-shot `changes` feed, queries temporary map/reduce views (map only
and reduced with `_count`), replicates and syncs to a second local database, and
compacts and destroys it again.

- **Sample (this repository):** <https://dotnet-websharper.github.io/pouchdb/>
- **Source repository:** <https://github.com/dotnet-websharper/pouchdb>
- **WebSharper bindings:** <https://websharper.com/bindings>
- **Upstream:** <https://github.com/pouchdb/pouchdb> · <https://pouchdb.com/api.html>

## Features

| PouchDB API | Binding | Notes |
| --- | --- | --- |
| `new PouchDB(name, options)` | `Database<'Doc>(name, options)` | `Database<'Doc>` is generic over the document shape; both constructor overloads (with and without a name) are mapped. |
| `put`, `post`, `remove`, `bulkDocs` | `Put`, `Post`, `Remove`, `BulkDocs` | Promise and node-style callback overloads; `BulkDocs` reports per-document success/failure (`BulkDocsResult`). |
| `get`, `allDocs`, `bulkGet`, `revsDiff` | `Get`, `AllDocs`, `BulkGet`, `RevsDiff` | `get` also has the `open_revs` overload returning `Revision<'Doc>[]`; `allDocs` accepts `key`/`keys`/`startkey`+`endkey`. |
| `changes` | `Changes(options)` | Returns `Changes<'Doc>`, which **is** a `Promise<ChangesResponse<'Doc>>` and also has `OnChange`/`OnComplete`/`OnError`/`Cancel`. |
| `replicate`, `sync` | `Replicate.To/From`, `Sync`, `PouchDB.Replicate`, `PouchDB.Sync` | Endpoints are a URL/local name or another `Database` instance. `Replication` and `Sync` are promises with typed events; `SyncResult` carries the `push`/`pull` results. |
| `info`, `compact`, `viewCleanup`, `close`, `destroy` | `Info`, `Compact`, `ViewCleanup`, `Close`, `Destroy` |  |
| `putAttachment`, `getAttachment`, `removeAttachment` | same names | Attachments are `Blob`s (or base64 strings/`ArrayBuffer`s on the way in). |
| `query` (temporary views) | `Query` | Map-only, custom-reduce and built-in-reduce overloads, both promise and callback forms. |
| `PouchDB.version`, `adapters`, `fetch`, `on`, `plugin`, `defaults` | `PouchDB.Version`, `Adapters`, `Fetch`, `On`, `Plugin`, `Defaults` | `PouchDB` (the non-generic class) is the module API. |

The binding imports PouchDB's self-contained browser bundle
(`pouchdb/dist/pouchdb.js`), which ships the IndexedDB and HTTP adapters, the
map/reduce engine and replication. It records the reference version as an npm
dependency (`>= 9.0.0 < 10.0.0`), so any bundler that resolves `npm` packages
(esbuild, Vite, …) works out of the box.

## Installation

```bash
dotnet add package WebSharper.PouchDB
```

```fsharp
open WebSharper.PouchDB
```

Bundle the npm package with your client code. The sample uses esbuild:

```js
await build({
  entryPoints: [`./build/WebSharper.PouchDB.Sample.js`],
  bundle: true,
  format: 'iife',
  outfile: `../dist/Scripts/WebSharper.PouchDB.Sample.min.js`
})
```

## Usage

### Documents and the type parameter

PouchDB is schemaless: documents are plain JSON objects, and PouchDB adds `_id`,
`_rev` and (depending on the options) `_conflicts`, `_revs_info`, `_revisions` and
`_attachments` to them. The type parameter of `Database<'Doc>` is the shape the
binding enforces; read the metadata by casting a document to `DocumentMeta`:

```fsharp
type Note =
    {
        Text : string
        Done : bool
    }

let db = new Database<Note>("notes")

promise {
    let! all = db.AllDocs(AllDocsOptions(Include_docs = true))
    for row in all.Rows do
        let meta = As<DocumentMeta> (box row.Doc)
        Console.Log(meta._id + " at revision " + meta._rev)
}
|> ignore
```

A write must not carry `_rev` when the document is new (PouchDB rejects an empty
revision), so new documents are plain objects without that field — the sample
builds them with `New [ ... ]` and types them as `Note`.

### Promises and `Async`

Every method that PouchDB promises returns `WebSharper.JavaScript.Promise<'T>`, so
`promise { }`, `Promise.AsAsync` and `Promise.Then` work directly:

```fsharp
let notes = db.AllDocs()

promise {
    let! all = notes
    return all.Rows.Length
}

(async {
    let! all = Promise.AsAsync (db.AllDocs())
    return all.Total_rows
})
|> Async.Start
```

The node-style `(err, result)` callbacks are kept as overloads; on success the
error is `null`.

### Options

Option types are `Pattern.Config` classes: the required fields are constructor
arguments and the optional ones are settable properties, so they read like the
JavaScript object literals:

```fsharp
let options = AllDocsOptions(Include_docs = true, Limit = 20)
options.Descending <- true

db.AllDocs(options)
```

Options whose upstream type is a union that cannot be modelled as a .NET property
are typed `obj` and documented as such — for example `ChangesOptions.Since`
(`number | string | "now"`), `ChangesOptions.Filter` (`string | function`),
`AllDocsResponse.Update_seq` and the string-keyed maps of `RevsDiff`.

### Changes

`changes()` returns an event emitter that is also a promise, which the binding
models as `Changes<'Doc> : Promise<ChangesResponse<'Doc>>` with typed event
members:

```fsharp
let feed = db.Changes(ChangesOptions(Live = true, Include_docs = true))

feed.OnChange(fun change -> Console.Log(change.Id + " changed"))
    .OnError(fun e -> Console.Log("feed failed: " + e.Message))
    .OnComplete(fun res -> Console.Log("complete"))
|> ignore

// The feed is a promise of its completion payload as well.
Promise.AsAsync feed |> Async.StartAsTask |> ignore
```

### Replication and sync

Replication endpoints are a database URL, a local database name, or another
`Database` instance; both forms are overloads:

```fsharp
let replication = db.Replicate.To("https://user:pass@example.org/notes")
replication.OnComplete(fun res -> Console.Log(res.Status)) |> ignore

let sync = db.Sync(backupDb)            // or db.Sync("notes-backup")
sync.OnComplete(fun res ->
    Console.Log(res.Push.Docs_written + res.Pull.Docs_written))
|> ignore
```

`Replication` resolves with a `ReplicationResult`; `Sync` resolves with a
`SyncResult` (`{ push, pull }`), which is what PouchDB reports for a
bidirectional sync.

### Map/reduce

Temporary views take a two-argument map function — `fun doc emit ->
emit.Call(key, value)` — which PouchDB calls with the documents and an `emit`
function:

```fsharp
let view =
    MapReduceBuiltin<string, float, Note>(
        (fun note emit -> emit.Call((if note.Done then "done" else "todo"), 1.0)),
        BuiltinReduce._count
    )

db.Query<string, float, float>(view, QueryOptions<string>(Group = true))
```

A `MapReduce` with a custom `reduce`, and `Query` without a reduce (map only),
are also available. Note that PouchDB `eval`s reduce functions (and map
functions that take a single argument) from their source, so those must be
self-contained JavaScript; the two-argument map functions the binding models are
called directly and may close over F# values.

### The module API

```fsharp
PouchDB.Version                        // the bundled PouchDB version
PouchDB.Adapters.Get("idb")            // Optional<AdapterImpl>
PouchDB.On(DatabaseEvent.Created, fun name -> Console.Log(name))
PouchDB.Replicate("notes", "notes-backup")
PouchDB.Sync(db1, db2)
PouchDB.Plugin(pluginObject)           // for plugins such as pouchdb-find
PouchDB.Defaults(PouchDBOptions(Auto_compaction = true))
```

PouchDB 9 no longer has a static `PouchDB.destroy(name)`: open the database and
call `Destroy()` on the instance. The `pouchdb-find` plugin is not bound (the
bundled build does not include it); `PouchDB.Plugin` takes it as an `obj`.

### Not bound

- **`pouchdb-find`** (`db.find`/`createIndex`/`getIndexes`) — a separate plugin
  that the bundled build does not ship.
- **Custom reduce functions written in F#** are compiled functions, and PouchDB
  `eval`s reducers from their source text; use the built-in reducers, or a
  self-contained JavaScript function.

## Changes from the previous binding

This revision targets PouchDB 9 and WebSharper 11 and is a breaking change from
the 4.7-era extension:

- The CDN `<script>` resources (PouchDB and the `lie` promise polyfill) are gone:
  the binding imports the npm package, and PouchDB 9 uses native promises.
- The binding's own `Promise`, `StaticPromise` and `EventEmitter` classes are
  replaced by `WebSharper.JavaScript.Promise<'T>` (and its `promise { }` /
  `Promise.AsAsync` helpers) and typed event members.
- `PouchDBCfg`, `GetCfg`, `AllDocsCfg`, `ChangesCfg`, `ReplicateCfg`,
  `QueryCfg` and `CompactCfg` are now `PouchDBOptions`, `GetOptions`,
  `AllDocsOptions`, `ChangesOptions`, `ReplicationOptions`, `QueryOptions` and
  `CompactOptions`, with upstream's field names and documented members.
- The database class is `Database<'Doc>` and the module API is `PouchDB`.
  `DestroyResponse` is now `BasicResponse`, `ReplicateResponse` is
  `ReplicationResult`, `DocRow` is `ChangesResponseChange`, and `Row`/`Emitter`
  are `QueryRow`/`Emitter`.
- New surface: `bulkGet`, `revsDiff`, `close`, `viewCleanup`, `type()`,
  attachments with a MIME type, `open_revs`, `SyncResult`, `PouchDB.Plugin` and
  `PouchDB.Defaults`.

## Running the test case

Build the sample in Release (this also bundles the npm package with esbuild) and
serve its static output:

```bash
dotnet build WebSharper.PouchDB.Sample/WebSharper.PouchDB.Sample.fsproj -c Release
dotnet tool install -g dotnet-serve   # once
dotnet serve -d dist -p 5235
```

Then open <http://localhost:5235/> and work through the cards:

- **Database** — the PouchDB version, the adapters compiled into the bundle and
  `Info()`;
- **Documents** — post notes, write a batch with `BulkDocs`, list with
  `AllDocs`, `Get`/`Toggle`/`Remove` the first document, and inspect the
  `AllDocs(keys)`, `BulkGet` and `RevsDiff` results;
- **Attachments** — store a `Blob` and read it back;
- **Changes feed** — a one-shot and a live feed, with `Cancel()`;
- **Map/reduce** — a map-only temporary view and one reduced with `_count`;
- **Replication** — replicate to and sync with a second local database, then
  destroy it.

The data lives in IndexedDB, so it survives a reload; the header switches between
the light and dark themes and remembers the choice.

## Build from source

The repository uses [Paket](https://fsprojects.github.io/Paket/). The sample
targets `net10.0`; the binding targets `netstandard2.0`.

```bash
dotnet tool restore
dotnet paket restore
dotnet build WebSharper.PouchDB.Sample/WebSharper.PouchDB.Sample.fsproj -c Release
```

`./build` (or `build.cmd` on Windows) runs the build-stack targets, for example:

```bash
./build WS-Package          # pack the NuGet package
./build CI-Release          # build, update dependencies, tag
```

## Pack

```bash
./build WS-Package
# or, without the build stack:
WSPackageFolder=/tmp/pkg dotnet fsi ./build.fsx -t WS-Package
```

The package contains `WebSharper.PouchDB.dll`, its `.xml` documentation and the
WebSharper 11 `.websharper` metadata sidecar.

## Deployment

`.github/workflows/ghpages.yml` builds the sample in Release on `.NET 11`
(preview) and publishes `dist/` to GitHub Pages on every push to `main`. The
screenshot in `docs/sample.png` is a real capture of that build.

## Repository layout

| Path | Contents |
| --- | --- |
| `WebSharper.PouchDB/Main.fs` | The WIG definition. |
| `WebSharper.PouchDB.Sample/` | The sample: `Client.fs`, `index.html`, `site.css`, `esbuild.config.mjs`, `package.json`. |
| `dist/` | Build output of the sample (generated, not committed). |
| `docs/sample.png` | The sample screenshot used in this README. |
| `nuget/` | The Paket pack template. |

## License

Apache 2.0 — see [LICENSE.md](LICENSE.md).

PouchDB itself is **Apache 2.0** (SPDX `Apache-2.0`), the same license as this
binding, for every 9.x release; see the
[upstream LICENSE](https://github.com/pouchdb/pouchdb/blob/master/LICENSE). The
npm package that the sample bundles into `dist/` carries the same Apache 2.0
terms and the upstream copyright notice.