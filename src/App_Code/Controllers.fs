module PinkBubbles.Informa.Controllers

open System
open System.Net
open System.Net.Http
open System.Web
open System.Web.Http
open System.Linq
open System.Data
open System.Data.Linq
open System.Configuration
open System.Threading.Tasks
open System.Text.RegularExpressions

open PinkBubbles.Informa
open PinkBubbles.Informa.Types

// UTILITY ---------------------------------------------------------
type Table<'T when 'T: not struct> with
    member x.ReplaceOnSubmit(oldEntity: 'T, newEntity: 'T) =
        x.DeleteOnSubmit(oldEntity)
        x.InsertOnSubmit(newEntity)

let selectSingle<'T when 'T:equality and 'T:not struct> (table: Table<'T>) predicate =
    let res =
        (query.Where(query.Source(table), predicate), id)
        |> query.Select
        |> query.ExactlyOneOrDefault
    if res = Unchecked.defaultof<'T> then None else Some res

let replace (regex: Regex) (replacement: string) (input: string) =
    regex.Replace(input, replacement)

let canonize (input: string) stripHTML =
    input.Replace(@"\r", "")
    |> if stripHTML then (replace Literals.regexHtmlTags " ") else id
    |> replace Literals.regexWhitespace " "
// ------------------------------------------------------------------

type QuestionController() =
    inherit System.Web.Http.ApiController()

    let buildUrl keywords =
        let rec buildUrl parameters =
            match parameters with
            | [] -> ""
            | (k, v)::kvs -> "&" + k + "=" + v + (buildUrl kvs)
        Literals.informaUrl + buildUrl(Literals.getInformaArgs keywords)

    let getResults keywords = async {
        let cnn = ConfigurationManager.ConnectionStrings.[Literals.cnnStringName].ConnectionString
        use ctx = new DataContext(cnn)

        // Insert or update the keywords into the database
        do
            let kwTable = ctx.GetTable<Keyword>()
            match selectSingle kwTable (fun k -> k.keywords = keywords) with
            | None -> kwTable.InsertOnSubmit { keywords = keywords; count = 1}
            | Some kw -> kwTable.ReplaceOnSubmit(kw, { kw with count = kw.count + 1})
            ctx.SubmitChanges()

        // Select new keyword suggestions
        let newSuggestions =
            query { for q in ctx.GetTable<Keyword>() do
                    where (q.keywords.Contains(keywords) && q.keywords <> keywords)
                    sortByDescending q.count
                    select q.keywords
                    take Literals.suggestionsCount }
            |> Seq.toArray

        // Select rated questions for these keywords
        let rated =
            query { for r in ctx.GetTable<Question>() do
                    where (r.keywords.Contains(keywords))
                    sortByDescending r.ratingRaw
                    select r
                    take Literals.itemsPerPage }
            |> Seq.toList
        let ratedSet = seq{for r in rated -> r.id} |> set 

        // Get other questions from Informa database
        let url = buildUrl(keywords)
        let req = WebRequest.Create(Uri(url)) 
        use! resp = req.AsyncGetResponse()
        use stream = resp.GetResponseStream()
        use reader = new IO.StreamReader(stream)
        let html = reader.ReadToEnd()
        let notRated =
            Literals.regexQuestion.Matches(html)
            |> Seq.cast<Match>
            |> Seq.map (fun m ->
                let question = canonize m.Groups.[1].Value true
                let id = Int32.Parse m.Groups.[2].Value
                { id = id; question = question; keywords = keywords; ratingRaw = 0.; count = 0; answer = "" })
            |> Seq.filter (fun p -> not <| String.IsNullOrWhiteSpace(p.question) || ratedSet.Contains(p.id))
            |> Seq.toList
        return { keyword=keywords; suggestions = newSuggestions; questions = List.toArray(rated@notRated)  } 
    }

    let getSuggestions = async {
        let cnn = ConfigurationManager.ConnectionStrings.[Literals.cnnStringName].ConnectionString
        use ctx = new DataContext(cnn)
        let kws =
            query { for q in ctx.GetTable<Keyword>() do
                    where (q.keywords.Contains(" ") = false)
                    sortByDescending q.count
                    select q.keywords
                    take 5 }
            |> Seq.toArray
        return { keyword=""; suggestions = kws; questions = Array.empty }
    }

    member x.Get(keywords: string) =
        if String.IsNullOrEmpty(keywords) then
            Async.StartAsTask(getSuggestions)
        else
            let keywords =
                keywords.ToLower()
                |> Literals.regexWhitespace.Split
                |> Array.sort
                |> fun arr -> String.Join(" ", arr)
            Async.StartAsTask(getResults keywords)

    member x.Post([<FromBody>] q: Question) =
        let post q = async {
            let cnn = ConfigurationManager.ConnectionStrings.[Literals.cnnStringName].ConnectionString
            use ctx = new DataContext(cnn)
            let qTable = ctx.GetTable<Question>()
            let newQ =
                match selectSingle qTable (fun q' -> q'.id = q.id) with
                | None ->
                    qTable.InsertOnSubmit(q)
                    q
                // If that answer has already been rated calculate the average.
                // I'm experimenting with pipeline operations here, but maybe this is not very readable.
                | Some qInDb ->
                    let newRating =
                        qInDb.ratingRaw
                        |> (*) (float qInDb.count)
                        |> (+) q.ratingRaw
                        |> (/) <| float (qInDb.count + 1)
                    let q = { q with count = qInDb.count + 1; ratingRaw = newRating }
                    qTable.ReplaceOnSubmit(qInDb, q)
                    q
            ctx.SubmitChanges()
            return newQ
        }
        Async.StartAsTask(post q)

type AnswerController() =
    inherit System.Web.Http.ApiController()

    member x.Get(id: string) =
        let getAnswer id = async {
            let req = WebRequest.Create(Uri(Literals.answerUrl + id))
            use! resp = req.AsyncGetResponse()
            use stream = resp.GetResponseStream()
            use reader = new IO.StreamReader(stream, Text.Encoding.GetEncoding(28605))
            let html = reader.ReadToEnd()
            let m = Literals.regexAnswer.Match(html)
            return match m.Success with
                   | true -> canonize m.Groups.[1].Value false
                   | false -> "Not found"
        }
        Async.StartAsTask(getAnswer id)
