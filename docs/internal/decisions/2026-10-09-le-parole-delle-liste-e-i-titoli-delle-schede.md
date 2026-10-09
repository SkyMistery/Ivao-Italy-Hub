# Le parole delle liste e i titoli delle schede (E10k)

**Data:** 9 ottobre 2026 — fase E10k di M4, PR del nucleo #238
**Stato:** **decisa**. Carmine sull'issue #224 ([la sua risposta][a224], data in chat alla sessione master il 9 ottobre e pubblicata
su sua istruzione): sì ai quattro punti, in una PR piccola del nucleo con una nota nuova (caso b), con le chiavi nuove del nucleo nelle
due lingue e nessun test del maintainer cambiato. Le due scelte che la risposta lascia aperte — parole o frecce nella paginazione,
togliere o riformulare la frase di Invio — le ha prese dalberone il 9 ottobre (§2). L'uso nei moduli è fuori da questa PR (§4).
**Dopo la revisione**, Carmine sulla #238 ([la sua risposta][a238], data in chat alla sessione master il 9 ottobre e pubblicata su sua
istruzione, al punto 3 dei [rilievi][r238]): **sì alla lettura più larga** del punto 2 della #224 che questa nota dichiara (§2.2) — il
nome della divisione è il titolo predefinito di **ogni** pagina, il back office compreso, e anche `/news`, `/documents` e `/search`
dicono il loro titolo —, e **la dipendenza da React 19 è accettata** com'è scritta, con `-titles.test.tsx` a tenerla (§6).
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: tre meccanismi che ci sono — la lista generata (`DataList`), i metadati di una
pagina (`PageMetadata`), il form generato (`SchemaForm`) — si estendono nel nucleo, in una PR a sé prima del codice dei moduli che li
usa (§0 regola 6).

[a224]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/224#issuecomment-6070089222
[r238]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/238#issuecomment-6083800690
[a238]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/238#issuecomment-6083876286

## 1. Che cosa serve

Quattro cose viste da dalberone sul banco di prova (6 e 7 ottobre, sui branch di E4 e di E5), tutte nel codice di `main`, segnalate a
Carmine sull'issue #224 (le prime due) e in un commento della stessa issue (le altre due):

1. **«Previous» e «Next» in inglese** sotto ogni lista generata, anche con l'interfaccia in italiano. `DataList` disegnava `Pagination`
   di Atmosphere, che scrive da sé «Previous», «Next», l'etichetta «pagination» e, sui suoi due «…», «Go to next page» (anche su quello
   che va alla prima pagina) e «More pages», e non prende parole. Il commento in testa al file prometteva il contrario.
2. **Il titolo della scheda.** `/events`, `/tours`, `/calendar` e la pagina che non c'è — e ogni schermata del back office — tenevano il
   `<title>` di `web/index.html`, «IVAO Division Hub», il nome del prodotto. La home, le pagine del CMS e le pagine di un evento e di
   un tour dicevano «… — IVAO Italia» con `PageMetadata`. Con qualche scheda aperta le prime non si distinguono.
3. **La frase di una lista vuota** è sempre `list.empty.description`, «Tutto quello che questo dipartimento crea comparirà in questo
   elenco». È sbagliata dove le righe non sono di un dipartimento, come nelle liste dei moduli (piano 0.72): le schede «Slot» e «Rotte»
   di un evento, per esempio.
4. **«Premi Invio per salvare»** (`form.submitHint`) è letto dal lettore di schermo sotto ogni form generato, anche dove Invio non
   salva: in una casella di più righe va a capo. Misurato da dalberone sulla pagina del caricamento degli slot di E5, che ha una casella
   di più righe e una select.

## 2. La forma nel codice

1. **La paginazione** (`web/src/shared/list/DataList.tsx`). `Pages` è un pezzo di `DataList`, non un componente dell'elenco chiuso (il
   componente è `DataList`). È composto con i pezzi di `Pagination` che Atmosphere esporta — `PaginationRoot`, `PaginationContent`,
   `PaginationItem`, `PaginationLink` — e con le icone di `lucide-react` che Atmosphere usa lì (`ChevronLeft`, `ChevronRight`,
   `Ellipsis`). **Parole e non frecce** (scelta di dalberone): l'aspetto di prima, «‹ Precedente 1 2 3 Successiva ›». La finestra è la
   stessa (tre numeri, quello corrente in mezzo se nessun capo è più vicino, come `displayedPages` di Atmosphere). Restano i due «…» che
   portano alla prima e all'ultima pagina, con un nome tradotto per il lettore di schermo. Chiavi nuove del nucleo: `list.pages.label`,
   `.previous`, `.next`, `.first`, `.last` («Pagine», «Precedente», «Successiva», «Prima pagina», «Ultima pagina»). **Una differenza
   voluta**: un «…» si disegna solo quando la pagina a cui porta non è già un numero della riga. Atmosphere lo disegna anche sulla prima
   e sull'ultima di tre pagine, accanto al numero a cui porta.
2. **I titoli delle schede** (`web/src/shared/seo/PageMetadata.tsx`):
   - **`DivisionTitle`** è montato **una volta**, dalla radice dell'albero delle rotte (`Root` in `web/src/routes/__root.tsx`), sopra
     ogni layout e sopra la pagina che non c'è. Scrive `<title>` con il nome della divisione nella lingua a schermo, che è il
     predefinito: nessuna scheda dice più il nome del prodotto. Passa quel nome all'albero sotto con un contesto React privato del file.
     Non disegna niente, come `PageMetadata`: non è un componente dell'elenco chiuso (§8.3), che raccoglie pezzi che si vedono.
   - **`PageMetadata`**: `title` e `description` prendono anche una frase già tradotta, oltre al `LocalizedString` di una riga.
     `divisionName` diventa **facoltativo**: senza, il nome viene dalla radice. Resta per le pagine scritte prima, che passano lo stesso
     nome: la pagina di un tour (del modulo dei tour, del maintainer) e quella di un evento, finché la fase degli eventi dopo questa
     non lo toglie (§4).
   - **`NotFound`** (`web/src/shared/ui/status-pages.tsx`) dice `notFound.title` nella scheda ovunque sia disegnata: da una rotta, o
     dalla pagina di un modulo la cui riga non c'è (un evento o un tour che non esiste).
   - **Le pagine elenco del nucleo** dicono nella scheda la chiave del loro titolo, e la descrizione dove ce l'hanno: `/news` e
     `/documents` (`PublicListScreen`), `/calendar` (`PublicCalendarScreen`), `/search` (`SearchResults`).
   - **Le tre pagine del nucleo che passavano il nome a mano** (`_public/$.tsx`, `HomePage`, `PublicEntryScreen`) non lo passano più:
     nel nucleo una strada sola. I loro titoli non cambiano.
   - ⚠️ **Funziona per dove sta, e l'ordine è di React.** React 19 mette un `<title>` che monta **prima** di quelli già nella testa del
     documento (`mountHoistable` di react-dom 19.2.8, letto nel sorgente installato: `insertBefore` sul primo `head > title`), e il
     browser mostra il primo. La radice monta prima di ogni pagina: il titolo di una pagina, montato dopo, vince finché c'è, e quando
     se ne va resta il predefinito. Un predefinito montato da una pagina, o sotto di essa, toglierebbe la scheda alle pagine sotto. Lo
     prova il test sulla radice vera (§3).
3. **La frase vuota per lista** (`DataList`): `emptyDescription?: string`, la frase della schermata già tradotta, accanto a
   `emptyAction`. Senza, resta la frase del nucleo di oggi. È detta solo quando non si cerca niente, come prima. **Non è una convenzione
   su `<labels>.empty`**, come i suggerimenti di un campo (`<labels>.hints.<campo>`): lo stesso `labels` lo usano più liste
   (`flightops:tours` ne ha due), e una chiave così cambierebbe da sola la frase di ognuna.
4. **La frase di Invio** (`SchemaForm`). **Tre casi** (scelta di dalberone), letti dai campi dello schema con `whereEnterSaves`, i campi
   annidati compresi e quelli nascosti esclusi:
   - **nessuna casella di una riga** → **niente frase** (la pagina del caricamento degli slot: una casella di più righe e una select);
   - **caselle di una riga e di più righe** → `form.submitHintOneLine`, «Premi Invio in un campo di una riga per salvare.»;
   - **solo caselle di una riga** → `form.submitHint` di oggi, che non cambia.

   Di una riga: un testo senza scelte e non `multiline`, un testo tradotto non `multiline`, un numero senza scelte, un giorno o un
   istante, un campo suggerito — **fuori da una lista ripetibile** (§6, punto 1). Di più righe: un testo o un testo tradotto
   `multiline`, anche dentro una lista. Né l'una né l'altra: una select (un testo o un numero con le scelte, un `z.enum`), un
   interruttore, le caselle da spuntare, un file, le icone.
5. **Niente in C#**: nessun endpoint, nessuna migrazione. Nessun componente nuovo nell'elenco chiuso. **Sei chiavi nuove del
   nucleo** nelle due lingue, cercate prima nei file dei moduli senza trovare doppioni (piano §16 punto 16: un doppione ferma l'avvio).
   `docs/UI-GUIDELINES.md` dice in inglese la paginazione, la frase vuota, il titolo di una pagina e la frase di Invio.

## 3. I test

- `web/src/shared/list/DataList.words.test.tsx` (7): le parole in inglese e in italiano, nessuna di quelle di Atmosphere; la finestra e
  i «…» su dieci pagine, e ogni bottone chiede la sua pagina; l'ultima pagina; la frase vuota del nucleo e quella della schermata; la
  ricerca che non trova niente non dice nessuna delle due.
- `web/src/shared/forms/SchemaForm.enter.test.tsx` (7): i tre casi; i campi annidati, tradotti e nascosti; il bottone disegnato altrove
  e l'italiano; dopo la revisione, la lista senza voci e la casella di più righe dentro una lista (§6).
- `web/src/routes/-titles.test.tsx` (5), **sulla radice vera** (`__root.tsx`): una pagina che non dice niente ha il nome della
  divisione; `/calendar`, `/news` e `/search` hanno il loro titolo, e uscirne ridà la scheda alla divisione; la pagina che non c'è ha
  il suo titolo, anche dopo il cambio di lingua; una pagina che passa ancora il nome e il titolo di una riga si legge come prima; dopo
  la revisione, niente di vuoto senza titolo né divisione (§6).
- **La prova al contrario**: con il codice di `main` rimesso (le parole e i test nuovi tenuti) cadono 12 test su 16. I quattro che
  passano sono quelli di ciò che non deve cambiare.
- Nessun test del maintainer cambia, e nessuna spec e2e legge queste parole.

## 4. Che cosa resta ai moduli

- **Gli eventi** (di dalberone): una **fase del modulo dopo questa**, scelta da dalberone il 9 ottobre (`CLAUDE.md` §0 regola 6: il
  nucleo e il modulo mai nella stessa PR). `/events` dice il suo titolo
  (`<PageMetadata title={t('events:public.title')} description={t('events:public.description')} />` in `EventsPublicPage`), la pagina di
  un evento non passa più `divisionName`, e le schede «Slot» e «Rotte» passano `emptyDescription` con parole loro, chiavi nuove in
  `events.json`.
- **I tour** (del maintainer, che questa fase non tocca): **`/tours` lo adotta il maintainer** con una riga in `PublicToursPage`
  (`web/src/modules/flightops/screens/public.tsx`). Fino ad allora la sua scheda dice il nome della divisione, non più quello del
  prodotto. `PublicTourPage` può togliere `divisionName` quando vuole: finché lo passa, vale come prima. Una lista dei tour che non è
  di un dipartimento può passare `emptyDescription`.

## 5. Le alternative

| Che cosa | Alternativa | Pro | Contro |
|---|---|---|---|
| Paginazione | **parole tradotte** (scelta) | l'aspetto di prima, in ogni lingua | cinque chiavi |
| | solo frecce, con un nome tradotto | due chiavi in meno | cambia l'aspetto di ogni lista |
| Titolo predefinito | **alla radice, e i titoli delle pagine montati sotto** (scelta) | un posto solo; `PageMetadata` com'era | regge sull'ordine di React (§2.2, provato) |
| | un solo `<title>` del layout, e le pagine che gli dicono il loro con un contesto | un `<title>` alla volta | stato nel layout e un effetto per pagina: un ridisegno a ogni navigazione, e il predefinito per un istante |
| | `head` e `HeadContent` di TanStack Router | è del router | un secondo meccanismo accanto a `PageMetadata`; il titolo non segue il cambio di lingua finché non si naviga |
| | il server che scrive il nome della divisione in `index.html` | lo vede anche chi non esegue JavaScript | C# e una lingua sola; niente prerender per ora (§16.11) |
| Frase vuota | **una prop della lista** (scelta) | ogni lista la sua | la schermata la passa |
| | una convenzione su `<labels>.empty` | nessun codice nel modulo | lo stesso `labels` per più liste: una frase per tutte |
| Invio | **tre casi** (scelta) | dice il vero in ogni form | una funzione che legge i campi |
| | toglierla dove c'è più righe | più corto | chi usa il lettore di schermo perde il «Invio salva» di una riga |
| | riformularla dove c'è più righe | più corto | parla di una riga anche dove non c'è |

## 6. Dopo la revisione

I [rilievi del revisore sulla #238][r238]: approvabile sul codice, niente da correggere; da unire con `main` quando #236 (il test del
meteo) è corretto, e con la CI intera. Fatti i due rilievi bassi:

1. **una lista ripetibile senza voci** non ha caselle sullo schermo, ma `whereEnterSaves` contava le sue caselle di una riga, e un form
   poteva dire «Premi Invio in un campo di una riga» di una casella che non c'è. Ora le caselle di una riga di una lista **non contano**;
   quelle di più righe sì, perché la frase che nomina la casella di una riga resta vera in ogni caso. Il prezzo: un form le cui sole
   caselle di una riga stanno in una lista non dice niente, anche quando le voci ci sono — una frase che manca, mai una falsa;
2. **`og:title`** non si scrive più vuoto, quando non c'è né un titolo né una divisione (fuori da `DivisionTitle`, cioè in un test):
   come `<title>` e `og:site_name`.

Un test per ciascuno (`SchemaForm.enter.test.tsx`, `-titles.test.tsx`), e tutti e due cadono sul codice di prima. Gli altri punti dei
rilievi sono per Carmine o già scritti qui: il punto 3 l'ha deciso lui (intestazione); il 4 (React 19) l'ha accettato; il 5 (i «…») è
§2.1; il 6 (`/events`, `/tours`, le schede degli eventi) è §4; il 7 (nessun aumento di `<Version>`) come nelle altre fasi del nucleo.

## Da portare nel piano

- **§16, punto 6** (il motore lista e form): la paginazione della lista è composta con i pezzi di Atmosphere e le parole del nucleo;
  una lista dice la sua frase vuota con `emptyDescription`; il form generato dice «Invio salva» solo dove è vero.
- **§16** (un punto nuovo, o §8.1): **il titolo della scheda** — ogni pagina lo dice con `PageMetadata`, il nome della divisione viene
  dalla radice (`DivisionTitle`), e una scheda non dice mai il nome del prodotto.
- **`CLAUDE.md` §2**, se il master lo vuole: una riga «Il titolo della scheda di una pagina → `PageMetadata`, con il nome della
  divisione dalla radice; mai un `<title>` scritto a mano».
- **`10-piano-implementazione-m4.md`**: la riga e la sezione di E10k (scritte da questa fase) e la fase degli eventi dopo.
- Il design M1 §8.4 (`PageMetadata`) resta vero. Si aggiunge che una pagina che non è una riga dà le sue frasi, e che il nome viene
  dalla radice.
