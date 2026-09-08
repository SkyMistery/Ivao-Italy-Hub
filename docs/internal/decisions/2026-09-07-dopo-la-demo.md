# Che cosa ha trovato Carmine eseguendo la demo di M1

**Data:** 7 settembre 2026 — dopo il merge della PR #56, eseguendo `tools/demo-m1.md`
**Stato:** **quattro difetti corretti, dodici richieste su dodici fatte**, quattro decisioni prese
più una — i tipi di evento — decisa e costruita l'8 settembre
**Dove si lavora:** ramo `m1/g13-fixes`; il tag `v0.2.0-m1` **aspetta** che questa lista sia chiusa

Carmine si è fermato al punto 7 della demo: **i punti 8 e 9 non sono ancora stati eseguiti**, e sono
l'ultimo pezzo di accettazione della milestone.

---

## I quattro difetti

### D4 — Ogni data era mostrata due ore indietro — **corretto** (`6f8217e`)

L'API mandava `2026-09-07T14:22:35.99759`, **senza `Z` e senza offset**. Un browser legge una
stringa così come ora **locale**, quindi `new Date(...)` la spostava dell'offset e la schermata
mostrava «17:35 UTC» per un istante che erano le 19:35 UTC. Riguardava **ogni istante dell'hub** —
liste, audit, calendario, date di pubblicazione, messaggi — perché tutte formattano la stringa che
l'API ha mandato.

I valori nel database erano giusti da sempre: mancava che il **modello** dicesse che sono UTC.
`UtcDateTimeConverter` in `ConfigureConventions`, un posto solo, scrittura non toccata.

Test: `InstantsAreUtcOnTheWireTests` — asserisce sul **testo sul filo**, perché è il parsing ad aver
nascosto il difetto, e su una **lettura**, perché la risposta alla creazione serializza l'entità
ancora nel change tracker e passa anche senza la correzione.

### D2 — Cancellare lasciava la pagina aperta — **corretto** (`fc33848`)

⚠️ **Regressione della correzione del `loader` della stessa mattina.** La mutazione aspettava
`invalidateQueries`, che aspetta il refetch di ogni query **attiva** sotto quella chiave — compresa
quella della schermata che sta cancellando, la cui riga è appena sparita. Il refetch va in 404,
riprova, `onSuccess` non si risolve mai e i callback passati a `mutate` non partono: la riga sparisce
dal database e la schermata resta lì senza dire niente.

Prima che le schermate leggessero la query invece del loader, quella query non aveva osservatori e
invalidarla non rifaceva niente. **Una correzione ne ha scoperta un'altra.**

Corrette tutte e sei le mutazioni di cancellazione: l'invalidazione non si aspetta più.
Test: `web/src/features/menu/mutations.test.tsx`.

### D1 — Il logout non aggiorna la pagina — **corretto** (`686ee82`)

L'ipotesi era giusta, ed è stata **misurata prima di correggere**: un test che monta la forma
dell'applicazione — una radice che carica il bootstrap con `ensureQueryData`, una schermata che
legge `useRouteContext` — fallisce esattamente come la demo.

Il punto è che **il bootstrap non è solo una query**: la radice lo carica una volta e lo passa come
**contesto** del router, ed è quella copia che leggono l'header, la sidebar e ogni guardia.
Invalidare una query non rifà un `beforeLoad`, quindi la shell continuava a disegnare il nome di chi
era appena uscito. Stessa famiglia di D2 e del loader, come previsto.

Un solo posto lo dice adesso, `sessionChanged`: **rimuove** la risposta in cache — non la invalida,
perché `ensureQueryData` restituisce ciò che trova e la radice riceverebbe di nuovo il payload
vecchio — e chiama `router.invalidate()`, che rifà anche le guardie. Lo usa anche la risposta al
401, che voleva le stesse due righe.

⚠️ **Il logout va prima a casa.** Una schermata del back-office sta dietro una guardia che manda chi
non ha sessione a `/auth/login`: ridisegnare dove si è avrebbe risposto a un clic su «esci» con il
login di IVAO, che ha ancora la sua sessione e lo avrebbe fatto rientrare.

Test: `web/src/features/me/logout.test.tsx`, verificato rompendolo due volte — con il corpo vecchio,
e con la navigazione ma senza l'invalidazione.

### D3 — Un documento pubblicato con un'immagine non mostra l'immagine — **corretto** (`0e28db1`)

Guardato dal filo, come la nota chiedeva, ed è **la visibilità della riga media**. Un file arriva
nella libreria come `Staff` — «diventa pubblico perché qualcuno lo dice, mai per essere arrivato», ed
è la regola giusta — quindi un'immagine caricata e messa subito in una pagina è staff-only. La
pagina usciva lo stesso e il visitatore riceveva **404** sull'indirizzo del file: anche questo per
disegno, perché l'indirizzo di un file che non si può vedere non deve confermare che esiste.

Quello che mancava è che **la pagina non aveva titolo per essere pubblicata portandolo**. La regola
esisteva già: `VisibilityCeiling`, lo stesso soffitto sotto cui si cattura un blocco Data `frozen`.
Ora risponde anche per le immagini, al solo momento in cui può — la pubblicazione — e lo dice con il
percorso della proprietà, come per una traduzione mancante.

**Rifiuta invece di riparare**: pubblicare una pagina non deve rendere pubblico un file di nascosto.

Tre modi di nominare un file, un controllo solo: il corpo, la copertina di una news e il file di un
documento — che è la forma in cui Carmine l'ha incontrato. `BlockDocumentWalker` sa dire quali file
mostra un documento, con i nomi delle proprietà presi da `JsonQuery`, l'unico posto che già li
conosceva.

Test: `MediaEndToEndTests.PublishRefusesAPageShowingAPictureItsReadersMayNotSee`, verificato
rompendolo. ⚠️ Ha fatto uscire anche un accoppiamento fra classi di test: `ContentEndToEndTests`
scriveva un corpo che nominava la media `7`, e tutta l'assemblea scrive nello stesso database, quindi
il file caricato in più da questo test ha spostato gli identificatori finché «questa media non è usata
da nessuna parte» ha trovato quella pagina.

---

## Le dodici richieste

**Editor** (punti 2 e 7 della demo)

1. ~~L'indirizzo (`slug`) lo **propone il sistema** dal titolo~~ ✅ **fatta**: `slugFrom`,
   annotazione del generatore di form e non una funzione della schermata dei contenuti. Segue il
   titolo finché il campo contiene esattamente quello che è stato proposto; una riga che ha già un
   indirizzo non lo sposta mai.
2. ~~**Conferma esplicita** che l'editor ha fatto quello che è stato cliccato~~ ✅ **fatta**, come
   **toast** (scelta di Carmine): salvare, pubblicare ed eliminare rispondono, e una pubblicazione
   rifiutata pure — la ragione resta in `PublishProblems`, che sta sopra il form e può essere fuori
   schermo, mentre il toast dice almeno che il clic è stato risposto.
3. ~~**«Cosa manca per pubblicare»**, visibile *prima* di provare~~ ✅ **fatta**: un `GET` che fa la
   prova a vuoto, `/api/content/{id}/publish-problems`, che esegue gli stessi controlli senza
   scrivere niente. ⚠️ È il **quarto** verbo a mano appeso al gruppo `MapCrud`, e la scelta è di
   Carmine: l'alternativa era il client che ricalcola le regole di pubblicazione — le stesse regole
   scritte due volte, e la seconda copia è quella che invecchia. Una lista sola, in tono `warning`,
   che si svuota quando l'ultima cosa è sistemata.
4. ~~Editor più intuitivo in generale~~ ✅ **fatta**, e sono le quattro cose già scritte altrove:
   annulla sull'ultima mossa strutturale, il blocco Titolo che nasce a livello 2, un solo `h1` per
   pagina, e il pannello delle proprietà che resta fermo mentre l'albero scorre.

**Grafica** (punto 3)

5. ~~**Un componente di avviso a quattro stati**~~ ✅ **fatto**: `Notice` (riquadro) e
   `useNotice()` (la stessa frase in un angolo, poi via), che leggono una tabella sola di quattro
   toni. Due dei quattro sono le varianti di Atmosphere così come sono; gli altri due sono scritti
   nella stessa forma, perché il tema ha le scale `semantic-yellow` e `semantic-blue` e manca solo
   la variante. La riga in piano §8.3 c'è, e con lei `docs/UI-GUIDELINES.md` §3, `catalog.ts` e la
   sezione della ui-kit. ⚠️ Deciso da Carmine: **non** sostituisce `ProblemAlert`.
6. ~~**Calendario**: le voci distinte da una **chip colorata**~~ ✅ **fatta**. ⚠️ Senza vocabolario:
   i cinque tipi che l'entità documenta hanno un colore ciascuno, il resto lo deriva dalla parola.
   Quando arriva la 11 il colore va sulle sue righe e questa mezza pagina di codice sparisce.
7. ~~**`LiveStatusStrip`**: «troppo piatta»~~ ✅ **fatta**, e senza decorazione: il numero è la cosa
   più forte, le parole la più debole, un'icona per figura, il puntino che respira.
8. **Icone dei dipartimenti** — deciso, vedi sotto. ✅ **Fatta** (`6584438`): la sigla è il segno,
   disegnata nello slot dell'icona che la sidebar già incornicia, quindi di un'altra famiglia
   rispetto alle icone delle risorse — la trappola che questa nota si era segnata. Misurata in un
   browser: una sigla di tre lettere sta in 20,4 px dentro i 20 che il padding lascia. ⚠️ **Non**
   entra nell'elenco chiuso di §8.3: non prende props, si monta solo in uno slot di icona, e nasce
   dai dati. Se Carmine la vede diversamente è una riga da aggiungere.

**Calendario** (punto 4)

9. **I tipi di evento sono blindati e decisi centralmente** — vedi sotto.
10. ~~Orario **UTC** e, fra parentesi, **locale**~~ ✅ **fatta**.
11. ~~**Quattro viste**~~ ✅ **fatta**: le liste disegnano gli stessi giorni della griglia saltando
    quelli vuoti. `agenda` esce dalle viste della schermata e resta quella dei blocchi.

**Staff** (punto 6)

12. ~~Una **barra di ricerca**~~ ✅ **fatta**, con la scorciatoia scritta sopra. ⚠️ In cima alla
    colonna del contenuto: la sidebar è di Atmosphere e non ha slot.

---

## Che cosa ha trovato la **seconda** esecuzione (8 settembre 2026)

Carmine ha rifatto la scheda dal punto 1 con l'app in esecuzione. Due cose dalla parte 1:

1. ~~**L'indirizzo lo propone il sistema anche altrove.** «Tutto ciò che nel sito è editabile e ha un
   link deve avere il meccanismo introdotto per documenti e news»~~ ✅ **fatta** (`3973eb0`): il
   `path` di una voce di menu — con la barra davanti, che è la parola in più che il generatore ha
   imparato — la `key` di una categoria e la `key` di un tipo di calendario.
2. **`Order` e `Visible to` modificabili dalla tabella** in `/staff/wd/menu`. ⚠️ **Proposta scritta,
   aspetta Carmine**: `2026-09-08-modificare-da-una-lista.md`. Non è una schermata: `DataList` è
   della spina dorsale, e la lista non ha con che scrivere — riceve una proiezione, e il motore
   scrive con il DTO completo. Tre strade, la raccomandazione è quella che non allarga l'API.

Il resto della parte 1 è andato.

## Le quattro decisioni prese

### Il tag aspetta

`v0.2.0-m1` si mette **dopo** questa lista, non su quello che c'è ora.

### Le icone dei dipartimenti: nessuna icona, la sigla è il segno

Oggi tutti e nove i dipartimenti hanno la **stessa** icona (`ShieldCheck`) e li distingue solo la
sigla: nove scudi identici che non portano informazione.

**Deciso: la sigla come segno**, al posto dell'icona. Ragione di Carmine: *«tanto per un fork non
IVAO devo comunque rimettere mano al codice»* — cioè una mappa «dipartimento → icona» vivrebbe nel
perimetro IVAO accanto all'enum `Department`, che un fork non-IVAO riscrive comunque, quindi
l'icona non è il pezzo che gli costa. E la sigla **è già** l'identificatore che lo staff usa ogni
giorno: nove icone scelte a tavolino sarebbero nove convenzioni nuove da imparare.

⚠️ Trappola da ricordare quando si implementa: l'icona del dipartimento sta accanto alle icone delle
**risorse** (calendario, news, media, link). Qualunque segno si scelga deve essere visibilmente di
un'altra famiglia, o si aggiunge rumore.

### L'avviso a quattro stati è un componente condiviso

Non il riquadro degli errori del form: **un componente usabile ovunque**. Quinto della lista chiusa.

### I tipi di evento del calendario sono di divisione, non di dipartimento

*«L'elenco è deciso da HQ, da WD o dalla sezione admin e sono quelli per tutti.»*

⚠️ Conseguenza da non sbagliare: **non è riusare le categorie**, che sono **per dipartimento**
(`cms_categories`). Serve un vocabolario **di divisione**, con un permesso di scope diverso da
`Calendar.Edit`, che è dipartimentale. Dove metterlo va proposto prima di scriverlo.

---

## Due lezioni di metodo, pagate care

1. ⚠️ **`grep "error CS"` non dice se una build è riuscita.** Con l'API in esecuzione, MSBuild
   fallisce con `MSB3027`/`MSB3021` — file bloccati — e non emette **nessun** errore `CS`. Due volte
   ho letto «0 errori» e ho eseguito un **binario vecchio**, e una verifica «rotta apposta» è passata
   a vuoto. Si guarda `Error(s)` nel riepilogo, e si ferma l'API prima di compilare.
2. **Tre ipotesi plausibili di fila possono essere tutte sbagliate.** Su D2: componente smontato,
   promessa rifiutata, retry lento — tutte no. Le sonde dentro la mutazione hanno risolto in un
   giro: `onSuccess` iniziava e non finiva mai.

---

## Lo stato in due righe

Ramo `m1/g13-fixes`, cinque commit, niente di non committato. **I quattro difetti sono chiusi.**
Verde in locale, tutto rieseguito: **458 test .NET** (300 unit + 158 integrazione), **256 Vitest**
(30 file), **42 smoke Playwright**, lint, typecheck, format e i18n puliti.

⚠️ Rieseguito l'8 settembre: `pnpm e2e:full`, **12 verdi**, ed è servito — nei suoi log si vede
l'editor che chiede `publish-problems` all'API vera, cioè la richiesta 7 provata dove nessun test
unitario poteva provarla.