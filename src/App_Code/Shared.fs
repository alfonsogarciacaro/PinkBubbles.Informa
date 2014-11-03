#if INTERACTIVE
[<ReflectedDefinition>]
#endif
module PinkBubbles.Informa.Types

open System.Data.Linq.Mapping

[<CLIMutable; Table(Name="Informa_Keyword")>]
type Keyword = {
    [<Column(IsPrimaryKey=true)>] keywords: string
    [<Column>] count: int
}

[<CLIMutable; Table(Name="Informa_Question")>]
type Question = {
    [<Column(IsPrimaryKey=true)>] id: int
    [<Column>] question: string
    [<Column>] keywords: string
    [<Column>] ratingRaw: float
    [<Column>] count: int
    answer: string
}

type AppState = {
    keyword: string
    suggestions: string[]
    questions: Question[]
}