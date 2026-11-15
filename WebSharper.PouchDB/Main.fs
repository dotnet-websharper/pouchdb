// $begin{copyright}
//
// This file is part of WebSharper
//
// Copyright (c) 2008-2026 IntelliFactory
//
// Licensed under the Apache License, Version 2.0 (the "License"); you
// may not use this file except in compliance with the License.  You may
// obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or
// implied.  See the License for the specific language governing
// permissions and limitations under the License.
//
// $end{copyright}
namespace WebSharper.PouchDB

open WebSharper
open WebSharper.InterfaceGenerator

#nowarn "3886" // single-element Required/Optional lists are intentional

/// Statically typed bindings for PouchDB 9, the client-side database that
/// speaks the CouchDB replication protocol and stores documents in IndexedDB.
///
/// The binding imports PouchDB's self-contained browser bundle
/// (`pouchdb/dist/pouchdb.js`), which ships the IndexedDB and HTTP adapters, the
/// map/reduce engine and replication. The package's `main` entry point
/// (`pouchdb`) imports the Node built-in `events` module, which bundlers such as
/// esbuild cannot resolve without an extra polyfill package.
module Definition =

    let ModuleName = "pouchdb/dist/pouchdb.js"

    // ------------------------------------------------------------------
    // Type helpers
    // ------------------------------------------------------------------

    let O = T<unit>
    let B = T<bool>
    let I = T<int>
    let Str = T<string>
    let Blob = T<JavaScript.Blob>
    let Date = T<JavaScript.Date>

    /// A JavaScript array of `t`.
    let Arr (t : Type.IType) = Type.ArrayOf t

    /// `Promise<t>` — the global ES promise, as modelled by
    /// `WebSharper.JavaScript.Promise`, so `Promise.AsAsync` and `promise { }`
    /// work on every value this binding returns.
    let PromiseOf (t : Type.IType) = T<JavaScript.Promise<_>>[t]

    /// PouchDB's node-style completion callback: on success the error is `null`.
    let Callback (t : Type.IType) = (T<exn> * t) ^-> O

    /// `Pattern.Config` with documented optional fields: the constructor takes
    /// the required fields, the settable properties are the optional ones.
    let Config (name : string) (required : list<string * Type.Type>) (optional : list<string * Type.Type * string>) =
        Pattern.Config name {
            Required = required
            Optional = []
        }
        |+> Instance [
            for (n, t, c) in optional ->
                (n =@ t) |> WithComment c :> _
        ]

    // ------------------------------------------------------------------
    // Errors
    // ------------------------------------------------------------------

    /// The error object PouchDB reports: a JavaScript `Error` decorated with
    /// CouchDB-specific fields. Callbacks receive it as an `exn`.
    let PouchError =
        Class "PouchError"
        |+> Instance [
            "status" =? I
            |> WithComment "HTTP status code during HTTP or HTTP-like operations."

            "name" =? Str
            |> WithComment "Error name, for example `\"not_found\"` or `\"conflict\"`."

            "message" =? Str
            |> WithComment "Human-readable message."

            "reason" =? Str
            |> WithComment "The reason reported by the server, if any."

            "error" =? T<obj>
            |> WithComment "`true`/`false` or a CouchDB error code such as `\"not_found\"`, depending on the backend."

            "id" =? Str
            |> WithComment "Id of the document the error refers to, if any."

            "rev" =? Str
            |> WithComment "Revision of the document the error refers to, if any."
        ]

    // ------------------------------------------------------------------
    // Configuration
    // ------------------------------------------------------------------

    /// Credentials of a remote (HTTP) database.
    let AuthOptions =
        Config "AuthOptions" [
            "username", Str
        ] [
            "password", Str,
            "Password of the user."
        ]

    /// PouchDB's `fetch` implementation: `(url, options) => Promise<Response>`.
    let FetchImpl =
        (Str * !?T<JavaScript.RequestOptions>) ^-> T<JavaScript.Promise<JavaScript.Response>>

    /// Options of the `new Database(name, options)` constructor.
    let PouchDBOptions =
        Config "PouchDBOptions" [] [
            "name", Str,
            "Database name. Only read by the `new Database(options)` constructor overload."

            "adapter", Str,
            "Adapter to use: `\"idb\"` (the default in browsers and part of the bundled build), `\"http\"`/`\"https\"` (remote CouchDB), or the name of an adapter registered by a plugin."

            "auto_compaction", B,
            "Compacts the database after every change. Defaults to `false`."

            "revs_limit", I,
            "How many old revisions to track (not copy). Defaults to `1000`."

            "size", I,
            "Maximum size in MB that Safari grants to the local database (`10`, `50`, `100`, `500` or `1000`)."

            "prefix", Str,
            "Prefix appended to the database name."

            "deterministic_revs", B,
            "Derives revision ids from an MD5 hash; when `false` they are random UUIDs. Defaults to `true`."

            "purged_infos_limit", I,
            "How many purged document infos to keep. Defaults to `1000`."

            "auth", AuthOptions.Type,
            "Credentials for a remote (HTTP) database."

            "skip_setup", B,
            "Disables the automatic creation of a remote database."

            "fetch", FetchImpl,
            "Custom `fetch` implementation used for HTTP databases."
        ]

    // ------------------------------------------------------------------
    // Responses
    // ------------------------------------------------------------------

    /// The result of operations that only report success: `destroy`,
    /// `compact` and `viewCleanup`.
    let BasicResponse =
        Class "BasicResponse"
        |+> Instance [
            "ok" =? B
            |> WithComment "`true` if the operation was successful."
        ]

    /// The result of a write: `put`, `post`, `remove`, `putAttachment` and
    /// `removeAttachment`.
    let PutResponse =
        Class "PutResponse"
        |+> Instance [
            "ok" =? B
            |> WithComment "`true` if the write was successful."

            "id" =? Str
            |> WithComment "Id of the written document."

            "rev" =? Str
            |> WithComment "Revision the write produced."
        ]

    /// One entry of the `bulkDocs` result, in the order of the input documents.
    let BulkDocsResult =
        Class "BulkDocsResult"
        |+> Instance [
            "ok" =? B
            |> WithComment "`true` when this document was written."

            "id" =? Str
            |> WithComment "Id of the document. Only present for successful writes."

            "rev" =? Str
            |> WithComment "New revision. Only present for successful writes."

            "error" =? Str
            |> WithComment "Error name of a failed write, for example `\"conflict\"`."

            "reason" =? Str
            |> WithComment "Reason of a failed write."

            "status" =? I
            |> WithComment "HTTP status code of a failed write."
        ]

    /// Information about a database, as reported by `info()`.
    let DatabaseInfo =
        Class "DatabaseInfo"
        |+> Instance [
            "db_name" =? Str
            |> WithComment "The name the database was opened with."

            "doc_count" =? I
            |> WithComment "Number of non-deleted documents."

            "update_seq" =? T<obj>
            |> WithComment "Sequence number of the database: a number for local databases, a string for CouchDB 2.0 and later."
        ]

    /// One entry of a document's `_revs_info` list.
    let RevisionInfo =
        Class "RevisionInfo"
        |+> Instance [
            "rev" =? Str
            |> WithComment "Revision id."

            "status" =? Str
            |> WithComment "`\"available\"`, `\"compacted\"` or `\"missing\"`."
        ]

    /// The `_revisions` field of a document fetched with `revs: true`.
    let Revisions =
        Class "Revisions"
        |+> Instance [
            "ids" =? Arr Str
            |> WithComment "Revision ids, newest first."

            "start" =? I
            |> WithComment "Position of the first id in the document's revision history."
        ]

    /// A document attachment.
    let Attachment =
        Class "Attachment"
        |+> Instance [
            "content_type" =? Str
            |> WithComment "MIME type of the attachment."

            "digest" =? Str
            |> WithComment "MD5 digest of the attachment, prefixed with `md5-`."

            "length" =? I
            |> WithComment "Length of the attachment in bytes. Only present on stubs."

            "stub" =? B
            |> WithComment "`true` when PouchDB did not inline the attachment data."

            "revpos" =? I
            |> WithComment "Revision the attachment was added in."

            "data" =? T<obj>
            |> WithComment "The attachment itself: a base64 `string`, or a `Blob` when fetched with `binary: true`."
        ]

    /// The `_attachments` object of a document.
    let Attachments =
        Class "Attachments"
        |+> Instance [
            "get" => Str?attachmentId ^-> !?Attachment
            |> WithInline "$this[$attachmentId]"
            |> WithComment "Returns the attachment with the given id, or `undefined`."
        ]

    // ------------------------------------------------------------------
    // Read options
    // ------------------------------------------------------------------

    /// Options of `get(docId, options)`.
    let GetOptions =
        Config "GetOptions" [] [
            "rev", Str,
            "Fetch a specific revision instead of the winning one."

            "revs", B,
            "Include the document's revision history in `_revisions`."

            "revs_info", B,
            "Include the revision list with their availability in `_revs_info`."

            "conflicts", B,
            "Include the conflicting leaf revisions in `_conflicts`."

            "attachments", B,
            "Include the attachment data as base64 strings."

            "binary", B,
            "Return attachment data as `Blob`s instead of base64 strings."

            "latest", B,
            "Forces the latest leaf revision, whatever revision was requested."
        ]

    /// Options of the `get(docId, openRevs)` overload, which fetches leaf
    /// revisions instead of a single document.
    let GetOpenRevisions =
        (Config "GetOpenRevisions" [
            "open_revs", T<obj>
        ] [
            "revs", B,
            "Include each revision's history."
        ])
        // `Pattern.Config` carries no comments for required fields, so the
        // `open_revs` contract is documented on the type.
        |> WithComment "Fetches leaf revisions. `open_revs` is `\"all\"` or an array of revision ids; it is typed `obj` because upstream accepts both a string and an array."

    /// One entry of the `get(docId, openRevs)` result.
    let Revision =
        Generic - fun Doc ->
            Class "Revision"
            |+> Instance [
                "ok" =? Doc
                |> WithComment "The document at this leaf revision."
            ]

    /// Options of `put`/`post`/`remove`.
    let PutOptions =
        Config "PutOptions" [] [
            "force", B,
            "Saves a document even though it has no `_rev`, allowing the last writer to win."
        ]

    /// Options of `bulkDocs`.
    let BulkDocsOptions =
        Config "BulkDocsOptions" [] [
            "new_edits", B,
            "When `false`, documents are stored with the revisions they carry instead of being assigned new ones (replication use)."
        ]

    /// Options of `allDocs`.
    let AllDocsOptions =
        Config "AllDocsOptions" [] [
            "include_docs", B,
            "Include the document bodies in `rows[i].doc`. Deleted documents are only included when `keys` is specified."

            "conflicts", B,
            "Include each document's `_conflicts`. Requires `include_docs`."

            "attachments", B,
            "Include attachment data as base64 strings. Requires `include_docs`."

            "binary", B,
            "Return attachment data as `Blob`s. Requires `attachments`."

            "descending", B,
            "Reverse the order of the rows."

            "limit", I,
            "Maximum number of rows to return."

            "skip", I,
            "Number of rows to skip (poor performance on IndexedDB/LevelDB)."

            "inclusive_end", B,
            "Include the document identified by `endkey`. Defaults to `true`."

            "update_seq", B,
            "Include an `update_seq` value in the response."

            "startkey", Str,
            "Low end of the `_id` range, or the high end when `descending`."

            "endkey", Str,
            "High end of the `_id` range, or the low end when `descending`."

            "key", Str,
            "Only return the document with this id."

            "keys", Arr Str,
            "Only return the documents with these ids. Missing ids are reported as error rows."
        ]

    /// The `value` of an `allDocs` row.
    let AllDocsRowValue =
        Class "AllDocsRowValue"
        |+> Instance [
            "rev" =? Str
            |> WithComment "Current revision of the document."

            "deleted" =? B
            |> WithComment "`true` when the row is a deleted document."
        ]

    /// One row of an `allDocs` response.
    let AllDocsRow =
        Generic - fun Doc ->
            Class "AllDocsRow"
            |+> Instance [
                "id" =? Str
                |> WithComment "Id of the document. Absent from a `keys` row that reports a missing document."

                "key" =? Str
                |> WithComment "Id of the document. Also present on a `keys` row that reports a missing document."

                "value" =? AllDocsRowValue
                |> WithComment "Revision information of the document."

                "doc" =? Doc
                |> WithComment "The document itself, when `include_docs` was set."

                "error" =? Str
                |> WithComment "`\"not_found\"` on a `keys` row whose document does not exist."
            ]

    /// The `allDocs` response.
    let AllDocsResponse =
        Generic - fun Doc ->
            Class "AllDocsResponse"
            |+> Instance [
                "offset" =? I
                |> WithComment "The `skip` that was applied."

                "total_rows" =? I
                |> WithComment "Number of documents in the database."

                "update_seq" =? T<obj>
                |> WithComment "Sequence the database reflects, when `update_seq` was set."

                "rows" =? Arr AllDocsRow.[Doc]
                |> WithComment "The matching documents."
            ]

    /// One `{ id, rev }` pair requested by `bulkGet`.
    let BulkGetDoc =
        (Config "BulkGetDoc" [
            "id", Str
        ] [
            "rev", Str,
            "Revision to fetch. Defaults to the winning revision."
        ])
        |> WithComment "A document (and optional revision) requested by `bulkGet`."

    /// Options of `bulkGet`.
    let BulkGetOptions =
        Config "BulkGetOptions" [
            "docs", Arr BulkGetDoc.Type
        ] [
            "revs", B,
            "Include the revision history of each document."

            "attachments", B,
            "Include attachment data as base64 strings."

            "binary", B,
            "Return attachment data as `Blob`s."
        ]

    /// One requested revision of the `bulkGet` result: either `ok` or `error`.
    let BulkGetDocResult =
        Generic - fun Doc ->
            Class "BulkGetDocResult"
            |+> Instance [
                "ok" =? Doc
                |> WithComment "The document, when the revision exists."

                "error" =? PouchError
                |> WithComment "The error, when the revision does not exist."
            ]

    /// One entry of the `bulkGet` result.
    let BulkGetResult =
        Generic - fun Doc ->
            Class "BulkGetResult"
            |+> Instance [
                "id" =? Str
                |> WithComment "Id the entry was requested with."

                "docs" =? Arr BulkGetDocResult.[Doc]
                |> WithComment "One entry per requested revision."
            ]

    /// The `bulkGet` response.
    let BulkGetResponse =
        Generic - fun Doc ->
            Class "BulkGetResponse"
            |+> Instance [
                "results" =? Arr BulkGetResult.[Doc]
                |> WithComment "The fetched documents, in the requested order."
            ]

    /// `style` values of `changes`.
    let ChangesStyle =
        Pattern.EnumStrings "ChangesStyle" ["main_only"; "all_docs"]

    /// Options of `changes`.
    let ChangesOptions =
        Config "ChangesOptions" [] [
            "live", B,
            "Keep listening for changes instead of completing after the current batch."

            "since", T<obj>,
            "Start after this sequence number, or `\"now\"` to only receive new changes. Typed `obj` because the sequence is a number for local databases and a string for CouchDB 2.0 and later."

            "timeout", T<obj>,
            "Request timeout in milliseconds, or `false` to disable it."

            "include_docs", B,
            "Include the document bodies in the change entries."

            "limit", T<obj>,
            "Maximum number of changes to return, or `false` for no limit."

            "conflicts", B,
            "Include `_conflicts` in the change entries."

            "attachments", B,
            "Include attachment data in the change entries."

            "binary", B,
            "Return attachment data as `Blob`s."

            "descending", B,
            "Reverse the order of the changes."

            "heartbeat", T<obj>,
            "For the HTTP adapter: milliseconds between heartbeats on a long-lived request, or `false` to disable them."

            "filter", T<obj>,
            "Name of a design-document filter (or `\"_view\"`), or a filter function `(doc, params) => bool`. Typed `obj` because upstream accepts both."

            "doc_ids", Arr Str,
            "Only return changes for these documents."

            "query_params", T<obj>,
            "Parameters passed to the filter function, keyed by name."

            "view", Str,
            "View to use as the filter, when `filter` is `\"_view\"`."

            "return_docs", B,
            "Defaults to `true`. When `false`, the feed keeps no documents in memory and the `change` event is the only way to receive them."

            "batch_size", I,
            "For the HTTP adapter: how many changes to fetch at a time. Defaults to `25`."

            "style", ChangesStyle.Type,
            "`main_only` (the default) returns only the winning revision, `all_docs` returns every leaf revision."

            "seq_interval", I,
            "For the HTTP adapter: emit sequence information every N changes."
        ]

    /// One leaf revision of a change.
    let ChangesChange =
        Class "ChangesChange"
        |+> Instance [
            "rev" =? Str
            |> WithComment "Revision id."
        ]

    /// One change reported by `changes`.
    let ChangesResponseChange =
        Generic - fun Doc ->
            Class "ChangesResponseChange"
            |+> Instance [
                "id" =? Str
                |> WithComment "Id of the changed document."

                "seq" =? T<obj>
                |> WithComment "Sequence of the change: a number for local databases, a string for CouchDB 2.0 and later."

                "changes" =? Arr ChangesChange
                |> WithComment "The leaf revisions the change produced."

                "deleted" =? B
                |> WithComment "`true` when the document was deleted."

                "doc" =? Doc
                |> WithComment "The document itself, when `include_docs` was set."
            ]

    /// The `complete` payload of `changes`.
    let ChangesResponse =
        Generic - fun Doc ->
            Class "ChangesResponse"
            |+> Instance [
                "status" =? Str
                |> WithComment "`\"complete\"`."

                "last_seq" =? T<obj>
                |> WithComment "Sequence of the last reported change."

                "results" =? Arr ChangesResponseChange.[Doc]
                |> WithComment "The reported changes."
            ]

    /// Options of `compact`.
    let CompactOptions =
        Config "CompactOptions" [] [
            "interval", I,
            "Minimum number of milliseconds between compactions, used with `auto_compaction`."
        ]

    /// Options of `destroy`.
    let OperationOptions =
        Config "OperationOptions" [] [
            "fetch", FetchImpl,
            "Custom `fetch` implementation used for HTTP databases."
        ]

    // ------------------------------------------------------------------
    // Replication
    // ------------------------------------------------------------------

    /// Options of `replicate`/`sync`.
    let ReplicationOptions =
        Config "ReplicationOptions" [] [
            "filter", T<obj>,
            "Name of a design-document filter, or a filter function. Typed `obj` because upstream accepts both."

            "query_params", T<obj>,
            "Parameters passed to the filter function, keyed by name."

            "doc_ids", Arr Str,
            "Only replicate these documents."

            "since", T<obj>,
            "Replicate changes after this sequence, or `\"now\"`."

            "live", B,
            "Keep replicating instead of completing after the current batch."

            "create_target", B,
            "Create the target database if it does not exist."

            "batch_size", I,
            "Number of documents to fetch per batch. Defaults to `100`."

            "batches_limit", I,
            "Maximum number of batches to run at once. Defaults to `10`."

            "checkpoint", B,
            "Whether to record checkpoints in the source and target. Defaults to `true`."

            "retry", B,
            "Retry failed requests instead of aborting the replication."

            "back_off_function", (I ^-> I),
            "Computes the delay before retrying, given the current delay."
        ]

    /// The payload of a replication's `change` event.
    let ReplicationInfo =
        Class "ReplicationInfo"
        |+> Instance [
            "ok" =? B
            |> WithComment "`true` when the replication is progressing."

            "start_time" =? Date
            |> WithComment "When the replication started."

            "docs_read" =? I
            |> WithComment "Number of documents read from the source so far."

            "docs_written" =? I
            |> WithComment "Number of documents written to the target so far."

            "doc_write_failures" =? I
            |> WithComment "Number of documents that could not be written."

            "errors" =? Arr PouchError
            |> WithComment "Errors hit during the replication."

            "last_seq" =? T<obj>
            |> WithComment "Sequence of the source the replication has reached."

            "status" =? Str
            |> WithComment "State of the replication, for example `\"pending\"`, `\"active\"` or `\"complete\"`."
        ]

    /// The payload of a replication's `complete` event and of its promise.
    let ReplicationResult =
        Class "ReplicationResult"
        |=> Inherits ReplicationInfo
        |+> Instance [
            "end_time" =? Date
            |> WithComment "When the replication finished."
        ]

    /// Everything PouchDB's event emitters offer, typed loosely: the per-event
    /// members of `Database`, `Changes`, `Replication` and `Sync` are the typed
    /// entry points.
    let EventEmitter =
        Class "EventEmitter"
        |+> Instance [
            "on" => Str?``event`` * T<JavaScript.Function>?listener ^-> TSelf
            |> WithComment "Registers a listener for the given event."

            "once" => Str?``event`` * T<JavaScript.Function>?listener ^-> TSelf
            |> WithComment "Registers a listener that is removed after its first call."

            "addListener" => Str?``event`` * T<JavaScript.Function>?listener ^-> TSelf

            "removeListener" => Str?``event`` * T<JavaScript.Function>?listener ^-> TSelf
            |> WithComment "Removes a listener."

            "removeAllListeners" => !?Str?``event`` ^-> TSelf
            |> WithComment "Removes every listener of an event, or every listener."

            "emit" => Str?``event`` * !?T<obj>?argument ^-> B
            |> WithComment "Calls the listeners of an event."

            "listeners" => Str?``event`` ^-> Arr T<JavaScript.Function>
            |> WithComment "Returns the listeners registered for an event."

            "listenerCount" => Str?``event`` ^-> I

            "eventNames" => T<unit> ^-> Arr Str

            "setMaxListeners" => I?count ^-> TSelf

            "getMaxListeners" => T<unit> ^-> I
        ]

    /// The object `changes` returns: an event emitter that is also a promise of
    /// the complete feed.
    let Changes =
        Generic - fun Doc ->
            Class "Changes"
            |=> Inherits (PromiseOf ChangesResponse.[Doc])
            |+> Instance [
                "OnChange" => (ChangesResponseChange.[Doc] ^-> O)?callback ^-> TSelf.[Doc]
                |> WithInline "$this.on(\"change\", $callback)"
                |> WithComment "Calls `callback` for every change. Requires `live` to keep receiving them."

                "OnComplete" => (ChangesResponse.[Doc] ^-> O)?callback ^-> TSelf.[Doc]
                |> WithInline "$this.on(\"complete\", $callback)"
                |> WithComment "Calls `callback` once the feed has completed."

                "OnError" => (T<exn> ^-> O)?callback ^-> TSelf.[Doc]
                |> WithInline "$this.on(\"error\", $callback)"
                |> WithComment "Calls `callback` when the feed fails."

                "cancel" => T<unit> ^-> O
                |> WithComment "Stops listening for changes and removes every listener."
            ]

    /// The object `replicate` returns: an event emitter that is also a promise of
    /// the replication result.
    let Replication =
        Class "Replication"
        |=> Inherits (PromiseOf ReplicationResult)
        |+> Instance [
            "OnChange" => (ReplicationInfo ^-> O)?callback ^-> TSelf
            |> WithInline "$this.on(\"change\", $callback)"
            |> WithComment "Calls `callback` as documents are replicated."

            "OnComplete" => (ReplicationResult ^-> O)?callback ^-> TSelf
            |> WithInline "$this.on(\"complete\", $callback)"
            |> WithComment "Calls `callback` once the replication has completed."

            "OnPaused" => (T<exn> ^-> O)?callback ^-> TSelf
            |> WithInline "$this.on(\"paused\", $callback)"
            |> WithComment "Calls `callback` when the replication pauses because of an error."

            "OnActive" => (T<unit> ^-> O)?callback ^-> TSelf
            |> WithInline "$this.on(\"active\", $callback)"
            |> WithComment "Calls `callback` when a paused replication resumes."

            "OnDenied" => (T<exn> ^-> O)?callback ^-> TSelf
            |> WithInline "$this.on(\"denied\", $callback)"
            |> WithComment "Calls `callback` when the target denies a document."

            "OnError" => (T<exn> ^-> O)?callback ^-> TSelf
            |> WithInline "$this.on(\"error\", $callback)"
            |> WithComment "Calls `callback` when the replication fails."

            "OnRequest" => (T<obj> ^-> O)?callback ^-> TSelf
            |> WithInline "$this.on(\"request\", $callback)"
            |> WithComment "Calls `callback` with the request PouchDB is about to send."

            "cancel" => T<unit> ^-> O
            |> WithComment "Cancels the replication."
        ]

    /// The `complete` payload of a `sync`: the results of the two directions.
    let SyncResult =
        Class "SyncResult"
        |+> Instance [
            "push" =? ReplicationResult
            |> WithComment "Result of replicating from the source to the target."

            "pull" =? ReplicationResult
            |> WithComment "Result of replicating from the target to the source."
        ]

    /// The object `sync` returns: a bidirectional replication, also a promise of
    /// both directions' results.
    let Sync =
        Class "Sync"
        |=> Inherits (PromiseOf SyncResult)
        |+> Instance [
            "OnChange" => (ReplicationInfo ^-> O)?callback ^-> TSelf
            |> WithInline "$this.on(\"change\", $callback)"
            |> WithComment "Calls `callback` as documents are replicated in either direction."

            "OnComplete" => (SyncResult ^-> O)?callback ^-> TSelf
            |> WithInline "$this.on(\"complete\", $callback)"
            |> WithComment "Calls `callback` once both directions have completed."

            "OnPaused" => (T<exn> ^-> O)?callback ^-> TSelf
            |> WithInline "$this.on(\"paused\", $callback)"
            |> WithComment "Calls `callback` when the sync pauses because of an error."

            "OnActive" => (T<unit> ^-> O)?callback ^-> TSelf
            |> WithInline "$this.on(\"active\", $callback)"
            |> WithComment "Calls `callback` when a paused sync resumes."

            "OnDenied" => (T<exn> ^-> O)?callback ^-> TSelf
            |> WithInline "$this.on(\"denied\", $callback)"
            |> WithComment "Calls `callback` when the target denies a document."

            "OnError" => (T<exn> ^-> O)?callback ^-> TSelf
            |> WithInline "$this.on(\"error\", $callback)"
            |> WithComment "Calls `callback` when the sync fails."

            "cancel" => T<unit> ^-> O
            |> WithComment "Cancels the sync."
        ]

    /// The base class of `Database`, used where the binding accepts either a
    /// database instance or a database URL/name.
    let PouchDBBase =
        Class "PouchDBBase"
        |=> Inherits EventEmitter

    /// The `replicate` property of a database: `db.Replicate.To(target)` and
    /// `db.Replicate.From(source)`.
    let Replicatable =
        Class "Replicatable"
        |+> Instance [
            "to" => Str?target * !?ReplicationOptions?options ^-> Replication
            |> WithComment "Replicates from this database to `target` — a database URL, or the name of a local database."

            "to" => PouchDBBase?target * !?ReplicationOptions?options ^-> Replication
            |> WithComment "Replicates from this database to another `Database` instance."

            "from" => Str?source * !?ReplicationOptions?options ^-> Replication
            |> WithComment "Replicates from `source` — a database URL, or the name of a local database — to this database."

            "from" => PouchDBBase?source * !?ReplicationOptions?options ^-> Replication
            |> WithComment "Replicates from another `Database` instance to this database."
        ]

    // ------------------------------------------------------------------
    // Map/reduce views
    // ------------------------------------------------------------------

    /// The map function of a temporary view: `fun doc emit -> emit.Call(key, value)`.
    let Emitter =
        Generic -- fun Key Value ->
            Class "Emitter"
            |+> Instance [
                "call" => Key?key * Value?value ^-> O
                |> WithInline "$this($key, $value)"
                |> WithComment "Emits a key/value pair from a map function."
            ]

    /// CouchDB's built-in reduce functions.
    let BuiltinReduce =
        Pattern.EnumStrings "BuiltinReduce" ["_sum"; "_count"; "_stats"]

    /// A map/reduce view definition with a custom reduce function.
    let MapReduce =
        Generic ---- fun Key Value Doc Result ->
            (Config "MapReduce" [
                "map", (Doc * Emitter.[Key, Value] ^-> O)
            ] [
                "reduce", (Arr (Key * Str) * Arr Value * B ^-> Result),
                "The reduce function: called with the `[docId, key]` pairs, the emitted values and a `rereduce` flag."
            ])
            |> WithComment "A map/reduce view: `map` is called with each document and emits key/value pairs through `emit`."

    /// A map/reduce view definition with one of CouchDB's built-in reducers.
    let MapReduceBuiltin =
        Generic --- fun Key Value Doc ->
            (Config "MapReduceBuiltin" [
                "map", (Doc * Emitter.[Key, Value] ^-> O)
                "reduce", BuiltinReduce.Type
            ] [])
            |> WithComment "A map/reduce view that uses `_sum`, `_count` or `_stats` as its reducer."

    /// `stale` values of `query`.
    let Stale =
        Pattern.EnumStrings "Stale" ["ok"; "update_after"]

    /// Options of `query`.
    let QueryOptions =
        Generic - fun Key ->
            Config "QueryOptions" [] [
                "reduce", B,
                "Set to `false` to skip the reduce function and return the map rows."

                "include_docs", B,
                "Include the document bodies."

                "conflicts", B,
                "Include `_conflicts` in the documents."

                "attachments", B,
                "Include attachment data as base64 strings."

                "binary", B,
                "Return attachment data as `Blob`s."

                "startkey", Key.Type,
                "Low end of the key range, or the high end when `descending`."

                "endkey", Key.Type,
                "High end of the key range, or the low end when `descending`."

                "inclusive_end", B,
                "Include the rows identified by `endkey`. Defaults to `true`."

                "limit", I,
                "Maximum number of rows to return."

                "skip", I,
                "Number of rows to skip."

                "descending", B,
                "Reverse the order of the rows."

                "key", Key.Type,
                "Only return the rows emitted with this key."

                "keys", Arr Key,
                "Only return the rows emitted with these keys."

                "group", B,
                "Group the reduced rows by key."

                "group_level", I,
                "Number of key elements to group by, for array keys."

                "stale", Stale.Type,
                "`ok` returns the current index without updating it, `update_after` updates it afterwards."

                "update_seq", B,
                "Include the index's `update_seq` in the response."
            ]

    /// One row of a `query` response.
    let QueryRow =
        Generic --- fun Key Value Doc ->
            Class "QueryRow"
            |+> Instance [
                "id" =? Str
                |> WithComment "Id of the document that emitted the row."

                "key" =? Key
                |> WithComment "The emitted key."

                "value" =? Value
                |> WithComment "The emitted value."

                "doc" =? Doc
                |> WithComment "The document itself, when `include_docs` was set."
            ]

    /// The `query` response of a map-only view.
    let QueryResponse =
        Generic --- fun Key Value Doc ->
            Class "QueryResponse"
            |+> Instance [
                "total_rows" =? I
                |> WithComment "Number of rows in the whole index."

                "offset" =? I
                |> WithComment "The `skip` that was applied."

                "update_seq" =? T<obj>
                |> WithComment "Sequence the index reflects, when `update_seq` was set."

                "rows" =? Arr QueryRow.[Key, Value, Doc]
                |> WithComment "The matching rows."
            ]

    /// One reduced row of a `query` response.
    let ReducedRow =
        Generic - fun Result ->
            Class "ReducedRow"
            |+> Instance [
                "key" =? T<obj>
                |> WithComment "The grouping key, or `null` without `group`."

                "value" =? Result
                |> WithComment "The reduced value."
            ]

    /// The `query` response of a reduced view.
    let ReducedResponse =
        Generic - fun Result ->
            Class "ReducedResponse"
            |+> Instance [
                "rows" =? Arr ReducedRow.[Result]
                |> WithComment "The reduced rows."
            ]

    // ------------------------------------------------------------------
    // Database
    // ------------------------------------------------------------------

    /// The `created`/`destroyed` events the `PouchDB` constructor emits.
    let DatabaseEvent =
        Pattern.EnumStrings "DatabaseEvent" ["created"; "destroyed"]

    /// A PouchDB adapter implementation, as listed by `PouchDB.Adapters`.
    let AdapterImpl =
        Class "AdapterImpl"
        |+> Instance [
            "valid" => T<unit> ^-> B
            |> WithComment "Whether the adapter can be used in this environment."
        ]

    /// The adapters registered with the bundled PouchDB build.
    let AdapterRegistry =
        Class "AdapterRegistry"
        |+> Instance [
            "get" => Str?name ^-> !?AdapterImpl
            |> WithInline "$this[$name]"
            |> WithComment "Returns the adapter registered under `name`, or `undefined` when it is not part of the build or not usable in this environment."
        ]

    /// Options of `getAttachment`.
    let GetAttachmentOptions =
        Config "GetAttachmentOptions" [] [
            "rev", Str,
            "Revision to fetch the attachment from. Defaults to the winning revision."
        ]

    /// A PouchDB database. `Doc` is the shape of the documents it stores.
    let Database =
        Generic - fun Doc ->
            Class "Database"
            |> ImportDefault ModuleName
            |=> Inherits PouchDBBase
            |+> Static [
                Constructor (Str?name * !?PouchDBOptions?options)
                |> WithComment "Opens (and by default creates) the database `name`. The adapter is inferred from the name: IndexedDB in browsers, HTTP for URLs."

                Constructor (PouchDBOptions?options)
                |> WithComment "Opens (and by default creates) the database described by `options`, whose `name` is required."
            ]
            |+> Instance [
                "name" =? Str
                |> WithComment "The name the database was opened with."

                "type" => T<unit> ^-> Str
                |> WithComment "The adapter this database is using, for example `\"idb\"` or `\"http\"`."

                "info" => T<unit> ^-> PromiseOf DatabaseInfo
                |> WithComment "Gets information about the database."

                "info" => Callback DatabaseInfo ^-> O
                |> WithComment "Gets information about the database."

                "get" => Str?docId * !?GetOptions?options ^-> PromiseOf Doc
                |> WithComment "Fetches a document by id. PouchDB adds `_id`, `_rev` and, depending on the options, `_conflicts`, `_revs_info`, `_revisions` and `_attachments` — include them in `Doc`, or cast the result to `DocumentMeta`."

                "get" => Str?docId * !?GetOptions?options * Callback Doc ^-> O
                |> WithComment "Fetches a document by id."

                "get" => Str?docId * GetOpenRevisions ^-> PromiseOf (Arr Revision.[Doc])
                |> WithComment "Fetches the leaf revisions of a document."

                "get" => Str?docId * GetOpenRevisions * (Callback (Arr Revision.[Doc])) ^-> O
                |> WithComment "Fetches the leaf revisions of a document."

                "put" => Doc?doc * !?PutOptions?options ^-> PromiseOf PutResponse
                |> WithComment "Creates a new document or updates an existing one. Updating requires the document's `_id` and `_rev`."

                "put" => Doc?doc * !?PutOptions?options * Callback PutResponse ^-> O
                |> WithComment "Creates a new document or updates an existing one."

                "post" => Doc?doc * !?PutOptions?options ^-> PromiseOf PutResponse
                |> WithComment "Creates a new document and lets PouchDB generate its `_id`. Prefer `put` so that `allDocs` can sort the documents."

                "post" => Doc?doc * !?PutOptions?options * Callback PutResponse ^-> O
                |> WithComment "Creates a new document and lets PouchDB generate its `_id`."

                "remove" => Doc?doc * !?PutOptions?options ^-> PromiseOf PutResponse
                |> WithComment "Deletes the document. `doc` needs at least `_id` and `_rev`."

                "remove" => Doc?doc * !?PutOptions?options * Callback PutResponse ^-> O
                |> WithComment "Deletes the document."

                "remove" => Str?docId * Str?rev * !?PutOptions?options ^-> PromiseOf PutResponse
                |> WithComment "Deletes the document with the given id and revision."

                "remove" => Str?docId * Str?rev * !?PutOptions?options * Callback PutResponse ^-> O
                |> WithComment "Deletes the document with the given id and revision."

                "bulkDocs" => (Arr Doc)?docs * !?BulkDocsOptions?options ^-> PromiseOf (Arr BulkDocsResult)
                |> WithComment "Creates, updates or deletes multiple documents in one transaction. Each entry of the result reports success or failure."

                "bulkDocs" => (Arr Doc)?docs * !?BulkDocsOptions?options * (Callback (Arr BulkDocsResult)) ^-> O
                |> WithComment "Creates, updates or deletes multiple documents in one transaction."

                "allDocs" => !?AllDocsOptions?options ^-> PromiseOf AllDocsResponse.[Doc]
                |> WithComment "Fetches multiple documents at once. Deleted documents are only included when `keys` is specified."

                "allDocs" => !?AllDocsOptions?options * (Callback AllDocsResponse.[Doc]) ^-> O
                |> WithComment "Fetches multiple documents at once."

                "bulkGet" => BulkGetOptions?options ^-> PromiseOf BulkGetResponse.[Doc]
                |> WithComment "Fetches the given id/revision pairs, reporting a per-document error when a revision is missing."

                "bulkGet" => BulkGetOptions?options * (Callback BulkGetResponse.[Doc]) ^-> O
                |> WithComment "Fetches the given id/revision pairs."

                "revsDiff" => T<obj>?diff ^-> PromiseOf T<obj>
                |> WithComment "Reports which of the given revisions are missing. `diff` maps document ids to arrays of revision ids; typed `obj` because PouchDB models the argument and the result as string-keyed objects."

                "revsDiff" => T<obj>?diff * (Callback T<obj>) ^-> O
                |> WithComment "Reports which of the given revisions are missing."

                "changes" => !?ChangesOptions?options ^-> Changes.[Doc]
                |> WithComment "Lists the changes made to the documents, in the order they were made. The returned object is an event emitter and a promise of the complete feed."

                "close" => T<unit> ^-> PromiseOf O
                |> WithComment "Closes the database, after which it cannot be used any more."

                "compact" => !?CompactOptions?options ^-> PromiseOf BasicResponse
                |> WithComment "Triggers a compaction of the underlying storage."

                "destroy" => !?OperationOptions?options ^-> PromiseOf BasicResponse
                |> WithComment "Deletes the database."

                "destroy" => !?OperationOptions?options * Callback BasicResponse ^-> O
                |> WithComment "Deletes the database."

                "viewCleanup" => T<unit> ^-> PromiseOf BasicResponse
                |> WithComment "Removes the indexes of deleted or modified design documents."

                "replicate" =? Replicatable
                |> WithComment "`Replicate.To(target)` and `Replicate.From(source)`."

                "sync" => Str?target * !?ReplicationOptions?options ^-> Sync
                |> WithComment "Replicates from this database to `target` and from `target` to this database."

                "sync" => PouchDBBase?target * !?ReplicationOptions?options ^-> Sync
                |> WithComment "Replicates in both directions between this database and another `Database` instance."

                "putAttachment" => Str?docId * Str?attachmentId * Str?rev * (Blob + Str + T<JavaScript.ArrayBuffer>)?attachment * Str?``type`` ^-> PromiseOf PutResponse
                |> WithComment "Attaches a binary object (a `Blob`, base64 `string` or `ArrayBuffer`) to a document, creating the document when it does not exist. `rev` may be `null` for a new document; `type` is the attachment's MIME type."

                "putAttachment" => Str?docId * Str?attachmentId * Str?rev * (Blob + Str + T<JavaScript.ArrayBuffer>)?attachment * Str?``type`` * Callback PutResponse ^-> O
                |> WithComment "Attaches a binary object to a document."

                "putAttachment" => Str?docId * Str?attachmentId * (Blob + Str + T<JavaScript.ArrayBuffer>)?attachment * Str?``type`` ^-> PromiseOf PutResponse
                |> WithComment "Attaches a binary object to a document that does not exist yet."

                "putAttachment" => Str?docId * Str?attachmentId * (Blob + Str + T<JavaScript.ArrayBuffer>)?attachment * Str?``type`` * Callback PutResponse ^-> O
                |> WithComment "Attaches a binary object to a document that does not exist yet."

                "getAttachment" => Str?docId * Str?attachmentId ^-> PromiseOf Blob
                |> WithComment "Fetches attachment data as a `Blob`."

                "getAttachment" => Str?docId * Str?attachmentId * (Callback Blob) ^-> O
                |> WithComment "Fetches attachment data as a `Blob`."

                "getAttachment" => Str?docId * Str?attachmentId * GetAttachmentOptions?options ^-> PromiseOf Blob
                |> WithComment "Fetches attachment data of a specific revision, as a `Blob`."

                "getAttachment" => Str?docId * Str?attachmentId * GetAttachmentOptions?options * (Callback Blob) ^-> O
                |> WithComment "Fetches attachment data of a specific revision, as a `Blob`."

                "removeAttachment" => Str?docId * Str?attachmentId * Str?rev ^-> PromiseOf PutResponse
                |> WithComment "Deletes an attachment from a document."

                "removeAttachment" => Str?docId * Str?attachmentId * Str?rev * Callback PutResponse ^-> O
                |> WithComment "Deletes an attachment from a document."

                "OnCreated" => (TSelf.[Doc] ^-> O)?callback ^-> TSelf.[Doc]
                |> WithInline "$this.on(\"created\", $callback)"
                |> WithComment "Calls `callback` once the database has been created."

                "OnDestroyed" => (T<unit> ^-> O)?callback ^-> TSelf.[Doc]
                |> WithInline "$this.on(\"destroyed\", $callback)"
                |> WithComment "Calls `callback` once the database has been destroyed."

                "OnError" => (T<exn> ^-> O)?callback ^-> TSelf.[Doc]
                |> WithInline "$this.on(\"error\", $callback)"
                |> WithComment "Calls `callback` when the database reports an error."

                Generic -- fun (Key : CodeModel.TypeParameter) (Value : CodeModel.TypeParameter) ->
                    "query" => (Doc * Emitter.[Key, Value] ^-> O)?``fun`` * !?QueryOptions.[Key]?options ^-> PromiseOf (QueryResponse.[Key, Value, Doc])
                    |> WithComment "Runs a temporary map-only view: `fun doc emit -> emit.Call(key, value)`."

                Generic -- fun (Key : CodeModel.TypeParameter) (Value : CodeModel.TypeParameter) ->
                    "query" => (Doc * Emitter.[Key, Value] ^-> O)?``fun`` * !?QueryOptions.[Key]?options * (Callback (QueryResponse.[Key, Value, Doc])) ^-> O
                    |> WithComment "Runs a temporary map-only view."

                Generic --- fun (Key : CodeModel.TypeParameter) (Value : CodeModel.TypeParameter) (Reduced : CodeModel.TypeParameter) ->
                    "query" => MapReduce.[Key, Value, Doc, Reduced]?``fun`` * !?QueryOptions.[Key]?options ^-> PromiseOf (ReducedResponse.[Reduced])
                    |> WithComment "Runs a temporary map/reduce view with a custom reduce function."

                Generic --- fun (Key : CodeModel.TypeParameter) (Value : CodeModel.TypeParameter) (Reduced : CodeModel.TypeParameter) ->
                    "query" => MapReduce.[Key, Value, Doc, Reduced]?``fun`` * !?QueryOptions.[Key]?options * (Callback (ReducedResponse.[Reduced])) ^-> O
                    |> WithComment "Runs a temporary map/reduce view with a custom reduce function."

                Generic --- fun (Key : CodeModel.TypeParameter) (Value : CodeModel.TypeParameter) (Reduced : CodeModel.TypeParameter) ->
                    "query" => MapReduceBuiltin.[Key, Value, Doc]?``fun`` * !?QueryOptions.[Key]?options ^-> PromiseOf (ReducedResponse.[Reduced])
                    |> WithComment "Runs a temporary map/reduce view with one of CouchDB's built-in reducers."

                Generic --- fun (Key : CodeModel.TypeParameter) (Value : CodeModel.TypeParameter) (Reduced : CodeModel.TypeParameter) ->
                    "query" => MapReduceBuiltin.[Key, Value, Doc]?``fun`` * !?QueryOptions.[Key]?options * (Callback (ReducedResponse.[Reduced])) ^-> O
                    |> WithComment "Runs a temporary map/reduce view with one of CouchDB's built-in reducers."
            ]

    /// The document metadata PouchDB adds to stored documents: `_id`, `_rev`
    /// and the optional `conflicts`/`revs`/`attachments` fields. Cast a result of
    /// `get`/`allDocs` to this type to read them.
    let DocumentMeta =
        Class "DocumentMeta"
        |+> Instance [
            "_id" =? Str
            |> WithComment "Id of the document."

            "_rev" =? Str
            |> WithComment "Current revision of the document."

            "_deleted" =? B
            |> WithComment "`true` on deleted documents returned by `allDocs`/`changes`."

            "_conflicts" =? Arr Str
            |> WithComment "Conflicting leaf revisions, when `conflicts` was set."

            "_revs_info" =? Arr RevisionInfo
            |> WithComment "Revision list with their availability, when `revs_info` was set."

            "_revisions" =? Revisions
            |> WithComment "Revision history, when `revs` was set."

            "_attachments" =? Attachments
            |> WithComment "The document's attachments."
        ]

    /// The module API: `PouchDB.Version`, `PouchDB.Adapters`, `PouchDB.Plugin`,
    /// `PouchDB.Replicate` and `PouchDB.Sync`. PouchDB 9 no longer has a static
    /// `destroy`: open the database (`new Database(name)`) and call `Destroy()`
    /// on it.
    let Static =
        Class "PouchDB"
        |> ImportDefault ModuleName
        |+> Static [
            "version" =? Str
            |> WithComment "Version of the bundled PouchDB."

            "adapters" =? AdapterRegistry
            |> WithComment "The adapters this build registers."

            "fetch" =? FetchImpl
            |> WithComment "The `fetch` implementation PouchDB uses by default."

            "on" => DatabaseEvent?``event`` * (Str ^-> O)?listener ^-> TSelf
            |> WithComment "Listens to the `created`/`destroyed` events of every database, with the database name as the argument."

            "replicate" => Str?source * Str?target * !?ReplicationOptions?options ^-> Replication
            |> WithComment "Replicates from `source` to `target`, both database URLs or local database names."

            "replicate" => PouchDBBase?source * PouchDBBase?target * !?ReplicationOptions?options ^-> Replication
            |> WithComment "Replicates between two `Database` instances."

            "sync" => Str?source * Str?target * !?ReplicationOptions?options ^-> Sync
            |> WithComment "Replicates in both directions between two database URLs or local database names."

            "sync" => PouchDBBase?source * PouchDBBase?target * !?ReplicationOptions?options ^-> Sync
            |> WithComment "Replicates in both directions between two `Database` instances."

            "plugin" => T<obj>?plugin ^-> TSelf
            |> WithComment "Registers a PouchDB plugin such as `pouchdb-find`. Typed `obj` because a plugin is either an object or a function that decorates the prototype."

            "defaults" => PouchDBOptions?options ^-> T<obj>
            |> WithComment "Returns a PouchDB constructor that applies `options` to every database it opens. Typed `obj` because it returns a JavaScript constructor function."
        ]

    // ------------------------------------------------------------------
    // Assembly
    // ------------------------------------------------------------------

    let Assembly =
        Assembly [
            Namespace "WebSharper.PouchDB" [
                PouchError
                AuthOptions
                PouchDBOptions
                BasicResponse
                PutResponse
                BulkDocsResult
                DatabaseInfo
                RevisionInfo
                Revisions
                Attachment
                Attachments
                GetOptions
                GetOpenRevisions
                Revision
                PutOptions
                BulkDocsOptions
                AllDocsOptions
                AllDocsRowValue
                AllDocsRow
                AllDocsResponse
                BulkGetDoc
                BulkGetOptions
                BulkGetDocResult
                BulkGetResult
                BulkGetResponse
                ChangesStyle
                ChangesOptions
                ChangesChange
                ChangesResponseChange
                ChangesResponse
                CompactOptions
                OperationOptions
                ReplicationOptions
                ReplicationInfo
                ReplicationResult
                SyncResult
                EventEmitter
                Changes
                Replication
                Sync
                PouchDBBase
                Replicatable
                Emitter
                BuiltinReduce
                MapReduce
                MapReduceBuiltin
                Stale
                QueryOptions
                QueryRow
                QueryResponse
                ReducedRow
                ReducedResponse
                DatabaseEvent
                AdapterImpl
                AdapterRegistry
                GetAttachmentOptions
                Database
                DocumentMeta
                Static
            ]
        ]

open WebSharper.InterfaceGenerator

[<Sealed>]
type Extension() =
    interface IExtension with
        member ext.Assembly =
            Definition.Assembly

[<assembly: Extension(typeof<Extension>)>]
do ()