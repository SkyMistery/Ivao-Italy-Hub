# I campi fuori dalla versione

**Data:** 21 ottobre 2026 — fase di test dopo M4 (nota `2026-10-18-la-fase-di-test.md`), revisione del codice del master, lotto 3
(nota `2026-10-20-la-revisione-del-codice.md`); accanto alla correzione `2026-10-21-il-pubblico-legge-la-versione.md`.
**Stato:** **Proposta** — la domanda è di Carmine (§4). Nessun codice in questa nota: la PR `fix/content-published-version` porta
ricerca, elenchi e lettura pubblica sulla versione per **titolo e corpo**, e si ferma qui.
**Regola applicata:** `CLAUDE.md` §5, caso **(c)**: che cosa appartiene alla versione è una scelta di progetto, non un dettaglio della
correzione.

## 1. Che cosa serve

Una versione (`cms_content_versions`) porta **titolo e corpo** e basta. Tutto il resto di quello che il pubblico vede di un contenuto
sta solo sulla riga, che è la bozza e si scrive con `Content.Edit` attraverso il CRUD generico, senza pubblicazione e senza
approvazione; cambia online nel momento in cui si salva:

| Campo | Dove lo vede il pubblico |
|---|---|
| `Summary` | la pagina pubblica, gli elenchi di notizie e documenti |
| `Seo` | la pagina pubblica (titolo e descrizione della scheda, anteprime) |
| `Collections` | gli elenchi (in quale scaffale compare), la pagina pubblica |
| `CoverMediaId` (notizie), `FileMediaId` (documenti) | la pagina pubblica, gli elenchi |
| `Visibility` | chi legge la pagina, la ricerca, gli elenchi, il frame interattivo |
| `Slug`, `ParentId` | l'indirizzo, il menu che lo punta, la sitemap |
| e, minori: `Pinned`, `Sort`, `ShowFooter`, le date di un documento (`EffectiveOn`, `RetiredAt`, `SupersededById`) | la pagina pubblica, l'ordine degli elenchi |

Due regole scritte non tengono, per questi campi:

- design M0 §5.5 e la nota `2026-09-13-contenuti-centralizzati` §3.2: il pubblico vede quello che è stato pubblicato, e dove il tipo
  passa dall'approvazione (le pagine) andare online passa dall'approvazione. Un riassunto, un'immagine di copertina, un indirizzo
  cambiano online senza nessuna delle due.
- La pubblicazione è il momento dei **controlli**: tutte le lingue (`Incomplete`), le immagini visibili ai lettori della pagina
  (`PicturesTheReaderCannotSeeAsync`, che guarda già copertina e file), le catture `frozen` sotto il tetto della pagina (nota
  `2026-09-04-frozen-e-visibilita`). Un campo che cambia dopo senza passare di lì non ripassa quei controlli: per esempio una
  copertina cambiata dopo la pubblicazione non è confrontata con la visibilità della pagina.

## 2. Perché nessun meccanismo esistente basta

Il meccanismo c'è — la versione e la pubblicazione — ma copre due campi. Estenderlo vuol dire decidere, campo per campo, se è
**contenuto** (fa parte di quello che si pubblica) o **posizione** (dove e per chi la pagina sta), e per i secondi chi può cambiarli su
una pagina online.

## 3. Alternative

1. **Tutto nella versione.** I campi di contenuto si copiano nella versione alla pubblicazione e i lettori pubblici li prendono da lì
   (colonne in più in `cms_content_versions`, additive; gli elenchi li leggono come ora leggono il titolo); visibilità e indirizzo pure,
   con il filtro globale che legge la visibilità pubblicata. Coerente fino in fondo, ma il filtro globale oggi è un'espressione sulla
   riga: spostarlo su un'altra tabella è il cambio più grosso del nucleo dal M0.
2. **Contenuto nella versione, posizione dietro la pubblicazione.** `Summary`, `Seo`, `Collections`, `CoverMediaId`, `FileMediaId`
   (e `ShowFooter`) nella versione, come in 1; `Visibility`, `Slug`, `ParentId` restano colonne della riga, ma su una riga **con una
   versione online** li cambia solo chi può pubblicarla (`Content.Publish`, e `Content.Approve` dove il tipo passa dall'approvazione,
   come già fa l'approvatore che corregge l'indirizzo in `ApproveAsync`), e la modifica ripassa i controlli della pubblicazione
   (immagini, catture). Le date di un documento e `Pinned`/`Sort` restano della riga: sono gestione, non testo.
3. **Tutto resta com'è**, e il piano dice che quei campi sono «vivi» per scelta: chi ha `Content.Edit` è staff del dipartimento, e
   correggere un riassunto senza ripubblicare è comodo. Costo zero, ma le due regole del §1 vanno riscritte con un'eccezione.

**Raccomandazione: la 2.** Il testo segue la versione come il titolo e il corpo, senza toccare il filtro globale; la posizione di una
pagina online la cambia chi la mette online, che è la regola della §3.2; e i controlli della pubblicazione restano l'unico posto che
decide che cosa può stare davanti a quali lettori. In una PR sua, dopo la tua risposta: migrazione additiva di `cms_content_versions`,
i lettori pubblici sulla versione, il rifiuto di visibilità e indirizzo nel `BeforeSave` del CRUD dei contenuti attraverso l'unico
gestore, e un test per campo.

## 4. La domanda per Carmine

**Quali campi della riga appartengono alla versione, e chi può cambiare visibilità e indirizzo di una pagina già online?**
Raccomandazione: la 2 del §3 — riassunto, SEO, collezioni, copertina, file e piè di pagina nella versione; visibilità e indirizzo di
una riga online solo da chi può pubblicarla (e approvarla, per le pagine), con i controlli della pubblicazione.

## Da portare nel piano

Dopo la risposta:

- Design M0 §5.5 (pubblicazione) e §3.5 (visibilità): quali campi porta una versione; chi cambia visibilità e indirizzo di una riga
  online.
- Nota `2026-09-13-contenuti-centralizzati` §3.2 e §3.7: la posizione di una pagina online passa dalla pubblicazione.
- §9.3 (modello dei contenuti): le colonne di `cms_content_versions`.
