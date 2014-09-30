module PinkBubbles.Informa.Literals

open System.Text.RegularExpressions

let regexWhitespace = Regex("\s+", RegexOptions.Compiled)
let regexHtmlTags   = Regex("<.*?>", RegexOptions.Compiled)
let regexQuestion   = Regex("""<strong>Pregunta: </strong>([\s\S]*?)<br>[\s\S]*?CODIGO=(\d+)""", RegexOptions.Compiled)
let regexAnswer     = Regex("""<h2>Respuesta</h2>\s*<ul>\s*<li>\s*<strong>([\s\S]*?)</strong>\s*</li>\s*</ul>""", RegexOptions.Compiled)

let [<Literal>] suggestionsCount    = 8
let [<Literal>] itemsPerPage        = 100
let [<Literal>] cnnStringName       = "SQLAzureConnection"
let [<Literal>] questionSelector    = "div#contenedor div p"
let [<Literal>] answerSelector      = "h2"
let [<Literal>] informaUrl          = @"https://www.agenciatributaria.gob.es/search?"
let [<Literal>] answerUrl           = @"https://www2.agenciatributaria.gob.es/ES13/S/IAFRIAFRC12F?TIPO=R&CODIGO="
let [<Literal>] incrementKeyword    = "Increment Informa_Keyword"
let [<Literal>] upsertQuestion      = "Upsert Informa_Question"

let getInformaArgs keywords = [
    "output", "xml_no_dtd"
    "proxystylesheet", "Informa"
    "ie", "UTF-8"
    "oe", "UTF-8"
    "conpalabras", keywords
    "as_epq", ""
    "as_oq", ""
    "as_eq", ""
    "site", "Informa"
    "filter", "0"
    "getfields", "pregunta.subcapitulo"
    "client", "Informa"
    "output", "xml_no_dtd"
    "proxystylesheet", "Informa"
    "ie", "UTF-8"
    "oe", "UTF-8"
    "site", "Informa"
    "filter", "0"
    "getfields", "pregunta.subcapitulo"
    "as_q", keywords
    "partialfields", ""
    "titulo", ""
    "capitulo", ""
    "subcapitulo", ""
    "num", (string itemsPerPage)
    "btnG", "B%C3%BAsqueda"
    "tlen", "120"
]

