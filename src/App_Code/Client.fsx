#r "System.Data.Linq.dll" // This is only necessary because of the SQL attributes in Shared types
#r "..\..\lib\Funscript.dll"
#r "..\..\lib\Funscript.Interop.dll"
#r "..\..\lib\Funscript.TypeScript.Binding.lib.dll"
#r "..\..\lib\Funscript.HTML.dll"
#r "..\..\lib\Funscript.HTML.Ractive.dll"

#load "Shared.fs"

[<ReflectedDefinition>]
module Client =

    open System
    open System.Net
    open System.Collections.Generic
    open FunScript.TypeScript
    open FunScript.HTML
    open PinkBubbles.Informa.Types

    type AnswerAction =
        | Request
        | Rated
    
    type MainAction =
        | Request
        | Wait

    let rec overlayLoop action (st: RactiveState<Question>) = async {
        let! st = async {
            match action with
            | AnswerAction.Request ->
                st.ractive.toggle("isLoading")
                let url = "api/answer?id=" + st.data.id.ToString()
                let req = System.Net.WebRequest.Create(url)
                let! answer = req.AsyncGetJSON<string>()
                st.ractive.toggle("isLoading")
                return RactiveState(st, {st.data with answer = answer})
            | Rated ->
                return st
        }
        let ev1, ev2 = st.ractive.onStream("rateAnswer", "closeAnswer")
        let! choice = Async.AwaitObservable(ev1, ev2)
        match choice with
        | Choice1Of2 (ev, arg) ->
            let q = {st.data with ratingRaw = unbox arg; count = 1};
            return! overlayLoop Rated (RactiveState(st, q))
        | Choice2Of2 _ ->                                       // Leave the loop
            match action with           
            | AnswerAction.Request ->
                return RactiveState(st, {st.data with answer=""})
            | Rated ->
                let req = WebRequest.Create("api/question")
                let! q = req.AsyncPostJSON({st.data with answer=""})
                return RactiveState(st, q)
    }

    let rec mainLoop action (st: RactiveState<AppState>) = async {
        let! st = async {
            match action with
            | Request ->
                st.ractive.toggle("isLoading")
                let url = "api/question?keywords=" + WebUtility.UrlEncode(st.data.keyword)
                let req = System.Net.WebRequest.Create(url)
                let! data = req.AsyncGetJSON<AppState>()
                st.ractive.toggle("isLoading")
                return RactiveState(st, data)
            | Wait ->
                return st
        }
        let ev1, ev2 = st.ractive.onStream("getQuestions", "getAnswer")
        let! choice = Async.AwaitObservable(ev1, ev2)
        match choice with
        | Choice1Of2 (ev, arg) ->
            let st = RactiveState(st, { st.data with keyword=unbox arg })
            return! mainLoop Request st
        | Choice2Of2 (ev, arg) ->
            let! qSt = overlayLoop AnswerAction.Request (st.scope(ev.keypath))
            return! mainLoop Wait st
    }

    let main() =
        // Add custom Ractive event for Enter key press
        Globals.Ractive.events.Add("enter", Globals.Ractive.makeCustomKeyEvent(int Keys.enter))

        // Create a ractive instance, the initial state, and start the async loop
        let ractive = Globals.Ractive.CreateFast("#ractive-container", "#ractive-template")
        RactiveState.init(ractive, { keyword=""; suggestions = [||]; questions = [||] })
        |> mainLoop Request
        |> Async.StartImmediate

        // Add a event so the input is always blurred after enter is pressed
        let kwInput =
            Globals.document.getElementById("keyword-input")
            |> unbox<HTMLInputElement>
        kwInput.onkeyupStream
        |> Observable.filter (fun ev -> ev.which = Keys.enter)
        |> Observable.add (fun _ -> kwInput.blur())

    // The code below will call FunScript and compile the project to Javascript
    let code = FunScript.Compiler.Compiler.Compile(expression= <@ main() @> , noReturn=true, shouldCompress=true)
    let path = System.IO.Path.Combine(__SOURCE_DIRECTORY__, @"..\script\app.js")
    System.IO.File.WriteAllText(path, code)

