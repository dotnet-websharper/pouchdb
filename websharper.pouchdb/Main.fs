namespace websharper.pouchdb

open IntelliFactory.WebSharper.InterfaceGenerator

module Definition =


    let O = T<unit>
    let Err = T<exn>
    let Blob = T<IntelliFactory.WebSharper.Html5.Blob>

    let Promise =
        let self = Type.New()
        Generic / fun S ->
            Class "Promise"
            |+> [
                Constructor (((S ^-> O)?reslove * (Err ^-> O)?reject) ^-> O)
            ]
            |+> Protocol [
                Generic - fun U ->
                    "then" => (S ^-> U)?onFulfilled * !? (Err ^-> U)?onRejected ^-> self.[U]
                Generic - fun U ->
                    "catch" => (Err ^-> U)?onRejected ^-> self.[U]
            ]
        |=> self

    let StaticPromise =
        Class "Promise"
        |+> [
            Generic - fun S ->
                "resolve" => S?value ^-> Promise S
            Generic - fun S ->
                "reject" => T<string>?reason ^-> Promise S
            Generic - fun S ->
                "all" => Type.ArrayOf(Promise S) ^-> Promise (Type.ArrayOf(S))
            Generic - fun S ->
                "race" => Type.ArrayOf(Promise S) ^-> Promise S
        ]

    let PouchAdapter =
        Pattern.EnumStrings "Adapter" ["idb";"leveldb";"websql";"http"]

    let PouchDBCfg =
        Pattern.Config "PouchDBCfg"
            {
                Required = []
                Optional = 
                    [
                        "auto_compaction", T<bool>
                        "adapter", PouchAdapter.Type
                        "ajax", T<obj>
                        "db", T<obj>
                    ]
            }

    let Callback (t : Type.IType) = (Err * t) ^-> O

    let DestroyResponse =
        Class "DestroyResponse"
        |+> Protocol [
            "ok" =? T<bool>
        ]

    let PutResponse =
        Class "PutResponse"
        |+> Protocol [
            "ok" =? T<bool>
            "id" =? T<string>
            "rev" =? T<string>
        ]

    let RemoveResponse = PutResponse

    let Document =
        Class "Document"
        |+> Protocol [
            "_id"  =? T<string>
            "_rev" =? T<string>
        ]

    let OpenRevs =
        Pattern.EnumStrings "OpenRevs" ["all"]

    let GetCfg =
        Pattern.Config "GetCfg"
            {
                Required = []
                Optional = 
                    [
                        "rev", T<string>
                        "revs", T<bool>
                        "revs_info", T<bool>
                        "open_revs", (OpenRevs + Type.ArrayOf(T<string>))
                        "conflicts", T<bool>
                        "attachments", T<bool>
                        "local_seq", T<bool>
                        "ajax", T<obj>
                    ]
            }

    let AllDocsCfg =
        Pattern.Config "AllDocsCfg"
            {
                Required = []
                Optional = 
                    [
                        "include_docs", T<bool>
                        "conflicts", T<bool>
                        "attachments", T<bool>
                        "startkey", T<string>
                        "endkey", T<string>
                        "inclusive_end", T<bool>
                        "limit", T<int>
                        "skip", T<int>
                        "descending", T<bool>
                        "key", T<string>
                        "keys", Type.ArrayOf(T<string>)
                    ]
            }

    let DocRow =
        Generic / fun S ->
            Class "DocRow"
            |+> Protocol [
                "doc" =? S
                "id"  =? T<string>
            ]

    let AllDocsResponse =
        Generic / fun S ->
            Class "AllDocsResponse"
            |+> Protocol [
                "offset" =? T<int>
                "rows" =? Type.ArrayOf (DocRow S)
                "total_rows" =? T<int>
            ]

    let EventType =
        Pattern.EnumStrings "EventType"
            [
                "change"
                "complete"
                "create"
                "update"
                "delete"
            ]

    let DBEventType =
        Pattern.EnumStrings "DBEventType"
            [
                "created"
                "destroyed"
            ]

    let EventEmitter =
        let self = Type.New()

        Generic / fun S E ->
            Class "EventEmitter"
            |=> self
            |+> Protocol [
                "on" => E?event * (S ^-> O)?callback ^-> self.[S]
                "on" => (Err ^-> O)?callback ^-> self.[S]

                "once" => E?event * (S ^-> O)?callback ^-> self.[S]
                "once" => (Err ^-> O)?callback ^-> self.[S]
                
                "cancel" => O ^-> O
            ]

    let ChangesCfg =
        Pattern.Config "ChangesCfg" 
            {
                Required = []
                Optional = 
                    [
                        "include_docs", T<bool>
                        "conflicts", T<bool>
                        "attachments", T<bool>
                        "descending", T<bool>
                        "filter", T<string>
                        "since", T<int>
                        "live", T<bool>
                        "limit", T<int>
                        "style", T<string>
                        "view", T<IntelliFactory.WebSharper.EcmaScript.Function>
                        "returnDocs", T<bool>
                        "batch_size", T<int>
                    ]
            }

    let ReplicateCfg =
        Pattern.Config "ReplicateCfg"
            {
                Required = []
                Optional = 
                    [
                        "filter", T<string>
                        "query_params", T<string>
                        "doc_ids", Type.ArrayOf(T<string>)
                        "live", T<bool>
                        "since", T<int>
                        "create_target", T<bool>
                        "batch_size", T<int>
                        "batches_limit", T<int>
                    ]
            }

    let ReplicateResponse =
        Class "ReplicateResponse"
        |+> Protocol [
            "doc_write_failures" =? T<int>
            "docs_read" =? T<int>
            "docs_written" =? T<int>
            "errors" =? Type.ArrayOf(T<exn>)
            "last_seq" =? T<int>
            "ok" =? T<bool>
            "start_time" =? T<IntelliFactory.WebSharper.EcmaScript.Date>
            "end_time" =? T<IntelliFactory.WebSharper.EcmaScript.Date>
            "status" =? T<string>
        ]

    let Replicatable =
        Class "Replicatable"
        |+> Protocol [
            "to" => T<string>?target * !? ReplicateCfg?options ^-> EventEmitter ReplicateResponse EventType
            "from" => T<string>?source * !? ReplicateCfg?options ^-> EventEmitter ReplicateResponse EventType
        ]


    let BuiltinReduce =
        Pattern.EnumStrings "BuiltinReduce" ["_sum"; "_count"; "_stats"]

    let Stale =
        Pattern.EnumStrings "Stale" ["ok"; "update_after"]

    let QueryCfg =
        Generic / fun K ->
            Pattern.Config "QueryCfg" 
                {
                    Required = []
                    Optional = 
                        [
                            "reduce", T<bool>
                            "include_docs", T<bool>
                            "conflicts", T<bool>
                            "attachments", T<bool>
                            "startkey", K
                            "endkey", K
                            "inclusive_end", T<bool>
                            "limit", T<int>
                            "skip", T<int>
                            "descending", T<bool>
                            "key", K
                            "keys", Type.ArrayOf K
                            "group", T<bool>
                            "group_level", T<int>
                            "stale", Stale.Type
                        ]
                }

    let Emitter =
        Generic / fun K V ->
            Class "Emitter"
            |+> Protocol [
                "call" => K?key * V?value ^-> O
                |> WithInline "$0($key, $value)"
            ]

    let MapReduce =
        Generic / fun K V D R ->
            Pattern.Config "MapReduce"
                {
                    Required = 
                        [
                            "map", (T<obj> -* D * Emitter K V ^-> O)
                            "reduce", (BuiltinReduce + (T<obj> -* Type.ArrayOf(K * T<string>)?keys * Type.ArrayOf(V)?values * T<bool>?rereduce ^-> R))
                        ]
                    Optional = []
                }

    let Row =
        Generic / fun K V ->
            Class "Row"
            |+> Protocol [
                "id" =? T<string>
                "key" =? K
                "value" =? V
            ]

    let QueryResponse =
        Generic / fun K V ->
            Class "QueryResponse"
            |+> Protocol [
                "offset" =? T<int>
                "rows" =? Type.ArrayOf (Row K V)
                "total_rows" =? T<int>
            ]

    let ReducedRow =
        Generic / fun R ->
            Class "ReducedRow"
            |+> Protocol [
                "value" =? R
            ]

    let ReducedResponse =
        Generic / fun R ->
            Class "ReducedResponse"
            |+> Protocol [
                "rows" =? Type.ArrayOf (ReducedRow R)
            ]

    let InfoResponse =
        Class "InfoResponse"
        |+> Protocol [
            "db_name" =? T<string>
            "doc_count" =? T<int>
            "update_seq" =? T<int>
        ]

    let CompactCfg =
        Pattern.Config "PatternCfg"
            {
                Required = []
                Optional =
                    [
                        "interval", T<int>
                    ]
            }

    let PouchDBClass =
        Generic / fun (S : Type.Type) ->
//            let S = Su |> WithConstraint [ Document.Type ]

            Class "PouchDB"
            |=> Inherits (EventEmitter T<string> DBEventType)
            |+> [
                Constructor (T<string>?name * !? PouchDBCfg?options)
            ]
            |+> Protocol [
                "destroy" => !? T<obj>?options ^-> Promise DestroyResponse
                |> WithComment "Delete database."
                "destroy" => !? T<obj>?options * (Callback DestroyResponse)?callback ^-> O
                |> WithComment "Delete database."

                "put" => S?doc * !? T<string>?docId * !? T<string>?docRev * !? T<obj>?options ^-> Promise PutResponse
                |> WithComment "Create a new document or update an existing document."
                "put" => S?doc * !? T<string>?docId * !? T<string>?docRev * !? T<obj>?options * (Callback PutResponse)?callback ^-> O
                |> WithComment "Create a new document or update an existing document."

                "post" => S?doc * !? T<obj>?options ^-> Promise PutResponse
                |> WithComment "Create a new document and let PouchDB generate an _id for it."
                "post" => S?doc * !? T<obj>?options* (Callback PutResponse)?callback ^-> O
                |> WithComment "Create a new document and let PouchDB generate an _id for it."

                "get" => T<string>?docId * !? GetCfg?options ^-> Promise S
                |> WithComment "Retrieves a document, specified by docId."
                "get" => T<string>?docId * !? GetCfg?options * (Callback S)?callback ^-> O
                |> WithComment "Retrieves a document, specified by docId."

                "remove" => S?doc * !? T<obj>?options ^-> Promise RemoveResponse
                |> WithComment "Deletes the document. doc is required to be a document with at least an _id and a _rev property. Sending the full document will work as well."
                "remove" => S?doc * !? T<obj>?options ^-> (Callback RemoveResponse)?callback ^-> O
                |> WithComment "Deletes the document. doc is required to be a document with at least an _id and a _rev property. Sending the full document will work as well."
                "remove" => T<string>?docId * T<string>?docRev * !? T<obj>?options ^-> Promise RemoveResponse
                |> WithComment "Deletes the document. doc is required to be a document with at least an _id and a _rev property. Sending the full document will work as well."
                "remove" => T<string>?docId * T<string>?docRev * !? T<obj>?options ^-> (Callback RemoveResponse)?callback ^-> O
                |> WithComment "Deletes the document. doc is required to be a document with at least an _id and a _rev property. Sending the full document will work as well."

                "bulkDocs" => Type.ArrayOf(S)?docs * !? T<obj>?options ^-> Promise (Type.ArrayOf PutResponse)
                |> WithComment "Create, update or delete multiple documents. The docs argument is an array of documents."
                "bulkDocs" => Type.ArrayOf(S)?docs * !? T<obj>?options ^-> (Callback <| Type.ArrayOf PutResponse)?callback ^-> O
                |> WithComment "Create, update or delete multiple documents. The docs argument is an array of documents."

                "allDocs" => !? AllDocsCfg?options ^-> Promise (AllDocsResponse S)
                |> WithComment "Fetch multiple documents. Deleted documents are only included if options.keys is specified."
                "allDocs" => !? AllDocsCfg?options * (Callback <| AllDocsResponse S)?callback ^-> O
                |> WithComment "Fetch multiple documents. Deleted documents are only included if options.keys is specified."

                "changes" => ChangesCfg ^-> EventEmitter (DocRow S) EventType
                |> WithComment "A list of changes made to documents in the database, in the order they were made."

                "replicate" =? Replicatable
                |> WithComment "Replicate data from source to target. Both the source and target can be a PouchDB instance or a string representing a CouchDB database URL or the name of a local PouchDB database."

                "sync" => T<string>?target * !? ReplicateCfg?options ^-> EventEmitter ReplicateResponse EventType
                |> WithComment "Sync data from this database to target and from target to this."

                "putAttachment" => T<string>?docId * T<string>?attachmentId * T<string>?rev * Blob?doc ^-> Promise PutResponse
                |> WithComment "Attaches a binary object to a document."
                "putAttachment" => T<string>?docId * T<string>?attachmentId * T<string>?rev * Blob?doc * (Callback PutResponse)?callback ^-> O
                |> WithComment "Attaches a binary object to a document."

                "getAttachment" => T<string>?docId * T<string>?attachmentId * !? T<obj>?options ^-> Promise Blob
                |> WithComment "Get attachment data."
                "getAttachment" => T<string>?docId * T<string>?attachmentId * !? T<obj>?options * (Callback Blob)?callback ^-> O
                |> WithComment "Get attachment data."

                "removeAttachment" => T<string>?docId * T<string>?attachmentId * T<string>?rev ^-> Promise PutResponse
                |> WithComment "Delete an attachment from a doc."
                "removeAttachment" => T<string>?docId * T<string>?attachmentId * T<string>?rev * (Callback PutResponse)?callback ^-> O
                |> WithComment "Delete an attachment from a doc."

                Generic - fun K V ->
                    "query" => (S * Emitter K V ^-> O)?``fun`` * !? (QueryCfg K)?options ^-> Promise (QueryResponse K V)
                    |> WithComment "Retrieves a view, which allows you to perform more complex queries on PouchDB. "

                Generic - fun K V ->
                    "query" => (S * Emitter K V ^-> O)?``fun`` * !? (QueryCfg K)?options * (Callback (QueryResponse K V))?callback ^-> O
                    |> WithComment "Retrieves a view, which allows you to perform more complex queries on PouchDB. "

                Generic - fun K V R ->
                    "query" => MapReduce K V S R * !? (QueryCfg K)?options ^-> Promise (ReducedResponse R)
                    |> WithComment "Retrieves a view, which allows you to perform more complex queries on PouchDB. "

                Generic - fun K V R ->
                    "query" => MapReduce K V S R * !? (QueryCfg K)?options * (Callback (ReducedResponse R))?callback ^-> O
                    |> WithComment "Retrieves a view, which allows you to perform more complex queries on PouchDB. "

                "viewCleanup" => !? T<obj>?options ^-> Promise DestroyResponse
                |> WithComment "Cleans up any stale map/reduce indexes."
                "viewCleanup" => !? T<obj>?options * (Callback DestroyResponse)?callback ^-> O
                |> WithComment "Cleans up any stale map/reduce indexes."

                "info" => O ^-> Promise InfoResponse
                |> WithComment "Get information about a database."
                "info" => (Callback InfoResponse) ^-> O
                |> WithComment "Get information about a database."

                "compact" => !? CompactCfg?options ^-> O
                |> WithComment "Triggers a compaction operation on the local or remote database."
            ]

    let StaticPouchDB =
        Class "PouchDB"
        |+> [
            "destroy" => T<string>?dbname * !? T<obj>?options ^-> Promise DestroyResponse
            |> WithComment "Delete database."
            "destroy" => T<string>?dbname * !? T<obj>?options * (Callback DestroyResponse)?callback ^-> O
            |> WithComment "Delete database."

            "replicate" => T<string>?source * T<string>?target * !? ReplicateCfg?options ^-> EventEmitter ReplicateResponse EventType
            |> WithComment "Replicate data from source to target. Both the source and target can be a PouchDB instance or a string representing a CouchDB database URL or the name of a local PouchDB database."

            "sync" => T<string>?source * T<string>?target * !? ReplicateCfg?options ^-> EventEmitter ReplicateResponse EventType
            |> WithComment "Sync data from src to target and target to src. This is a convenience method for bidirectional data replication."
        ]

    module Res = 
        let Lie = 
            Resource "Lie" "lie.min.js"
        let Pouch = 
            (Resource "PouchDB" "pouchdb.min.js").AssemblyWide()
            |> Requires [Lie]

    let Assembly =
        Assembly [
            Namespace "IntelliFactory.WebSharper.PouchDB.Resources" [
                Res.Lie
                Res.Pouch
            ]
            Namespace "IntelliFactory.WebSharper.PouchDB" [
               Generic - Promise
               StaticPromise
               PouchAdapter
               PouchDBCfg
               DestroyResponse
               PutResponse
               OpenRevs
               GetCfg
               AllDocsCfg
               Generic - AllDocsResponse
               EventType
               DBEventType
               Generic - EventEmitter
               ChangesCfg
               ReplicateCfg
               ReplicateResponse
               Replicatable
               BuiltinReduce
               Stale
               Generic - QueryCfg
               Generic - MapReduce
               Generic - Row
               Generic - QueryResponse
               Generic - ReducedRow
               Generic - ReducedResponse
               InfoResponse
               CompactCfg
               Generic - PouchDBClass
               StaticPouchDB

               Generic - DocRow

               Document
               Generic - Emitter
            ]
        ]

open IntelliFactory.WebSharper.InterfaceGenerator

[<Sealed>]
type Extension() =
    interface IExtension with
        member ext.Assembly =
            Definition.Assembly

[<assembly: Extension(typeof<Extension>)>]
do ()
