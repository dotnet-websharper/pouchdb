# Introduction

PouchDB is a client-side JavaScript library for dealing with CouchDB. That means anyone can create a local instance which – depending on the adapter – will store the data in IndexedDB or Web SQL. It can then be seamlessly synchronized with remote CouchDB databases.

# PouchDB with WebSharper

## Static typing

This extension provides statically typed bindings for the PouchDB library. An example of this the constructor ``PouchDB<T>()``. The semantics of this notation do not fully conform to that of its JavaScript counterpart. CouchDB – and therefore PouchDB – is a schemaless database meaning that you can store very different objects in it. In JS this is perfectly natural because of the dynamic typesystem but in F# this would get really annyoing. If you would like to treat your database the same you can use ``PouchDB<obj>()`` to get the job done but if you'd like additional type safety you should use the type-paramterized constructor. If you need to deal with different types consider using F#'s union types. The database stores its content as simple JSON objects so your use case can differ from this single type approach and obviously you cannot use union types to couple very disperate types of objects. Unfortunately in this case the type system cannot aid you; the only thing you can do is to store ``objs`` in the DB.

```fsharp
	type Fruit =
		| Apple
		| Orange
		| Other of string

	let db = new PouchDB<Fruit>("fruitdb")	
```

Now you have a database instance that stores objects of type ``Fruit``. Please be aware that this is purely static and is only present at compile time. Nothing guarantees that objects from different sources (e.g. JavaScript code) cannot be added to the same database.

## Querying

PouchDB uses the ``MapReduce`` model for querying databases. This clearly doesn't make much sense when dealing with in memory instances in a single threaded language but as mentioned above you can connect to remote CouchDB databases in which case the map and reduce functions will be run remotely, meaning that the code you write in JavaScript will be sent as text to the server for processing. The querying part of this binding also aims to be type safe but due to some quirks in WebSharper the resulting interface may not be very intuitive or appealing. Let's take a look at an example using the code above as basis.

We have the following types we want to store in the database:

```fsharp
    type Fruit =
        | Apple
        | Orange
        | Other of string

    [<Measure>] type l

    type Juice =
        {
            Quantity : float<l>
            Kind : Fruit
        }

	let mkJuice q k = { Quantity = q; Kind = k }
```

Let's load some default data into the database (the ``FillDefault`` function only adds objects if the database is empty):

```fsharp
    let uncurry f (a,b) = f a b

    let Juices =
        [
            5.0<l>, Apple
            10.0<l>, Orange
            7.5<l>, Other "Mango"
            2.0<l>, Other "Pineapple"            
        ]
        |> List.map (uncurry mkJuice)

    let db = new PouchDB<Juice>("juicedb")

    let FillDefault () =
        db.Info().Then(fun info ->
            if info.Doc_count = 0 then
                Juices
                |> List.map (fun a -> db.Put(a, string <| EcmaScript.Date.Now()))
                |> List.toArray
            else
                [||]
            |> Promise.All
        )
```

Now that we have some data we can start querying the database:

```fsharp
    let DoQuery () =
        db.Query(
            MapReduce(
                Func<_,_,_,_>(
                    fun this doc (emitter : Emitter<obj, float<l>>) ->
                        emitter.Call(null, doc.Quantity)
                ),
                BuiltinReduce._stats
            )
        ).Then(fun a -> JavaScript.Log a.Rows.[0])
```
	
And finally we can combine them:

```fsharp
	let Run () =
        let r = FillDefault ()
        r.Then(fun _ -> DoQuery ())
        |> ignore
```

which yields this object:

```javascript
{
	key: null,
	value: {
		count: 4,
		max: 10,
		min: 2,
		sum: 24.5,
		sumsqr: 185.25
	}
}
```

Please do note that you are very limited inside the scope of map and reduce functions as they are meant to be run on remote servers. That's why these functions are preserved in their string representations and they get executed with the ``eval`` function. Because of this you are limited to access things only from global scope as your functions will be executed in the scope of PouchDB or remotely if you are managing a CouchDB instance. The PouchDB documentation suggests that you should only use the built in reduce methods and in case yo do not you are probably doing it wrong. You only need to roll your own reduce function in very unqiue situations.

## Promises

PouchDB makes extensive use of HTML5's promises for asynchronouse workflows. I do not intend to give a tutorial on how to use them right here, there are countless other blogposts and articles which do that much better than I could; rather I'd like to outline how PouchDB uses them and how you can make them even better in F# with WebSharper!
Basically in PouchDB every function that returns something does return it wrapped in a ``Promise`` or you can access it via a callback. From a manageability standpoint you are almost always better off using promises. Luckily F# has a very nice abstraction to deal with these long running tasks called "async workflows". Most of the functionality of `Async` is also present in WebSharper and that makes for very nice abstractions over long workflows with PouchDB. The ``Async`` namespace has this very crafty function ``FromContinuations`` with which you can easily convert a ``Promise<T>()`` into an ``Async<T>()`` to be able to use all the functionality F# gives you in form of async workflows. For what is supported refer to [the WebSharper doc on Async](https://github.com/intellifactory/websharper/blob/master/docs/Async.md). With all this we can create a nice function that does this conversion for us. For example let's make a type extension.

```fsharp
    type Promise<'a> with
        member this.ToAsync() : Async<'a> =
            Async.FromContinuations(fun (ok, err, _) ->
                this.Then(ok, err) |> ignore
            )
```

Now we can write the above ``Run`` function like so:

```fsharp
	let Run () =
        async {
            let! _ = FillDefault().ToAsync()
            do! DoQuery().ToAsync()
        }
        |> Async.Start
```

This may not seem to be a big improvement but if you are dealing with application size code this can make a big difference in code size, readibility and maintainability.

## Docs

The [official PouchDB documentation](http://pouchdb.com/api.html) goes much more in detail but I wouldn't like to mention them here as they should be the same in F# with minor differences.