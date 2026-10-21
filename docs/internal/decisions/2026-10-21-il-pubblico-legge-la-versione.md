# Il pubblico legge la versione

**Data:** 21 ottobre 2026 — fase di test dopo M4 (nota `2026-10-18-la-fase-di-test.md`), revisione del codice del master, lotto 3
(nota `2026-10-20-la-revisione-del-codice.md`); PR del nucleo `fix/content-published-version`, con la #317.
**Stato:** correzione di un difetto, nella direzione già scritta dal design M0 §5.5 («il pubblico legge solo la versione
pubblicata») e dalla nota `2026-09-13-contenuti-centralizzati` §3.2 («nel frattempo il pubblico vede la versione precedente»). La
nota esiste perché la correzione **estende due meccanismi del nucleo** (il filtro globale e la proiezione) e il piano deve saperlo.
I campi della riga che non sono nella versione sono una scelta aperta: nota `2026-10-21-i-campi-fuori-dalla-versione.md`, «Proposta».
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: si estendono i meccanismi che ci sono, nessuno nuovo accanto.

## 1. Che cosa serve

Una pagina, una notizia, un documento hanno due facce: la **riga** (`cms_contents`), che è la bozza e si continua a scrivere dopo la
prima pubblicazione, e la **versione pubblicata** (`cms_content_versions`), che è quello che il pubblico legge. La lettura pubblica
della pagina prende la versione; con questa correzione la prendono anche gli altri lettori pubblici:

- **la ricerca** (`ContentEntry.Project`, riproiettata dall'interceptor a ogni salvataggio);
- **gli elenchi** `newsList` e `documentList` (vivi o catturati), per il titolo;
- **il successore** di un documento ritirato, nell'avviso della pagina pubblica, per il titolo;
- e **la #317**: segnare «pronta» la versione successiva di una pagina già online mette `Status = Ready`, e filtro globale e
  interceptor guardavano `Status == Published`: la pagina spariva da lettura pubblica, ricerca, elenchi, frame interattivi, sitemap,
  finché qualcuno non decideva.

In più il frame interattivo di una pagina non pubblica si tiene ora come un file non pubblico della biblioteca (`private`).

## 2. La forma

- **`IPublishedAsVersion`** (`Core/Division/DomainContracts.cs`): una riga pubblicabile la cui faccia pubblica è una versione sua,
  con `PublishedVersionId`. Per una riga così **online vuol dire «ha una versione pubblicata»**, e `Status` dice solo dove sta la
  bozza (`Draft`, `Ready`, `Published`). Il filtro globale (`VisibilityQueryFilter`) e la regola «una bozza non si proietta»
  dell'interceptor chiedono `PublishedVersionId != null` invece di `Status == Published`; per ogni altra riga pubblicabile (tour,
  eventi, …) non cambia niente. `ContentEntry` la implementa. Così la #317 si chiude senza toccare la macchina a stati della
  revisione: `Ready` resta quello che è, e una pagina con una versione online resta online mentre la successiva aspetta.
- **`IProjectsFromAnotherRow`** (`Core/Content/Projections.cs`): una riga la cui proiezione è fatta anche di un'altra riga del suo
  contesto dichiara tipo e chiave di quella riga; l'interceptor la legge (con `Find`, sul suo contesto, nei due percorsi del
  salvataggio) e gliela passa prima di chiedere la proiezione. Una chiave e non una query: chi legge resta l'interceptor, e una
  versione appena scritta nella stessa unità di lavoro si trova dove già sta. `ContentEntry` proietta nella ricerca **titolo e testo
  della versione pubblicata**, indirizzo e visibilità della riga; senza versione non proietta niente.
- **Gli elenchi** (`ContentListProvider`) leggono il titolo dalla versione pubblicata, con una seconda query per non perdere
  l'ordine; il successore di un documento, con una join sulla sua versione.
- **Il frame interattivo** (`EmbedEndpoints`): `public, max-age=31536000, immutable` solo per una pagina `Public`; per le altre
  `private, max-age=3600`, la regola dei file della biblioteca (`MediaEndpoints`): chi può leggere una pagina può cambiare.
- **L'indice già scritto** si riscrive una volta: `ContentSeeder` ha un passo ricordato dalla chiave
  `search.content:published-version` in `hub_division_settings`, che riproietta ogni riga con una versione pubblicata attraverso
  `ProjectionRefresh` (cioè attraverso l'interceptor), con le versioni caricate prima. Le righe scritte prima della 0.10.3 portavano il
  testo della bozza, e le pagine in revisione non ci stavano; il primo avvio della release le rimette a posto, gli altri trovano la
  chiave.

## 3. Alternative

1. **Togliere `Ready` dallo stato e segnare la revisione con `ReadyAt`** (una pagina online resta `Published`) — scartata: cambia il
   significato di `status` per la SPA (filtro «Ready» del back office, editor bloccato, pannello di revisione, «il mio lavoro») e
   chiede una migrazione dei dati, per arrivare allo stesso punto.
2. **Proiettare solo alla pubblicazione e «tenere» titolo e testo negli altri salvataggi** — scartata: chiede alla scrittura della
   proiezione un modo «aggiorna solo alcune colonne», per tutti i moduli, e un segnale nascosto sulla riga per dire «questa è una
   pubblicazione».
3. **Una navigazione EF da `cms_contents` alla versione, con chiave esterna** — scartata: una seconda chiave esterna tra le due
   tabelle, in senso opposto alla prima e con la cancellazione a cascata in mezzo.
4. **Copiare sulla riga titolo e testo pubblicati** — scartata: la versione è già quella copia.

## 4. Le prove

- `PublishedVersionReadTests`: una notizia pubblicata e poi riscritta senza pubblicare — la ricerca trova le parole pubblicate e non
  quelle della bozza, con il titolo pubblicato; l'elenco vivo `newsList` mostra il titolo pubblicato; il passo una tantum riscrive una
  riga dell'indice lasciata con il testo di prima e scrive la sua chiave.
- `PageReviewTests.APageOnlineStaysWhatThePublicReadsWhileItsNextVersionWaits` (#317): pubblicata la v1, riscritta e segnata
  «pronta», la pagina si legge in pubblico con il testo della v1, la ricerca trova la v1 e non la v2; approvata, la ricerca trova la v2.
- `EmbedFrameTests.TheFrameOfAPageNotForEverybodyIsKeptOnlyByTheBrowserThatAsked`.
- I test che scrivevano a mano una riga «pubblicata» mettendo solo `Status = Published`, senza versione, ora scrivono anche la
  versione, come fa `ContentPublishService`: è lo stato che la pubblicazione produce, e l'unico che il pubblico vede.

## Da portare nel piano

- §9.7 (contratti trasversali) e §16 (meccanismi generici): `IPublishedAsVersion` — per una riga con una versione sua, «pubblicata»
  vuol dire «ha una versione pubblicata», nel filtro globale e nella regola della proiezione; `Status` dice dove sta la bozza.
- §16 (proiezioni): `IProjectsFromAnotherRow` — una proiezione fatta anche di un'altra riga del contesto, letta dall'interceptor per
  chiave; la ricerca di un contenuto è la sua versione pubblicata.
- Design M0 §5.5: il punto (4) diventa «l'interceptor proietta nella ricerca la versione pubblicata», e la ricerca non cambia finché
  non si pubblica di nuovo; design M1 (gli elenchi di notizie e documenti): il titolo è quello della versione.
- Nota `2026-09-13-contenuti-centralizzati` §3.2: «nel frattempo il pubblico vede la versione precedente» ora è vero anche per
  ricerca, elenchi e frame.
- Design M0 (il blocco interattivo, nota `2026-09-12-il-blocco-interattivo`): il frame di una pagina non pubblica si tiene solo nel
  browser, un'ora.
