# Le etichette dei numeri dei tour: nessun blocco ne prende una

**Data:** 15 novembre 2026, issue #521 (ramo `fix/tours-figure-labels`; `12-design-editor-fase-3.md` §5.7).
**Stato:** scelta della sessione di lavoro, dentro quello che la issue le chiede di decidere («Decide block by block whether a label
fits […] a number inside a sentence with its plural may not, and then the block stays as it is — write which and why in a short
note»). La issue a parte per i tour è di Carmine (14 novembre 2026, «[Issue a parte per i
tour](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/511#issuecomment-6883857820)»).
**Classificazione** (`CLAUDE.md` §5): nessuna, perché non cambia niente: né codice, né schemi, né parole.

## 1. Che cosa chiede il §5.7

Un'etichetta del §5.7 è **la parola sotto un numero**: in `stats` e `networkStats` il numero sta da solo, grande, e la didascalia sotto
è un nome («Membri», «Piloti in linea») che non concorda con il numero. Per questo una parola scritta da chi compone la pagina ci sta:
qualunque sia il numero, la didascalia si legge giusta. Un numero che sta **dentro una frase** con il suo plurale non ha una parola sotto:
ha una frase intorno, che il file delle lingue scrive in due forme (`_one`, `_other`) e che un campo tradotto, una stringa sola, non
sa scrivere.

## 2. I quattro blocchi, uno per uno

| Blocco | Il numero | Etichetta? | Perché |
|---|---|---|---|
| `flightops.openIssues` | tre righe, ognuna un collegamento: «2 segnalazioni aperte sulle leg», «1 contestazione senza risposta», «3 richieste di chiarimenti senza risposta» | **no** | Il numero è dentro la frase del collegamento, con il plurale (`legIssues_one` / `_other`, e così gli altri due): «1 contestazioni senza risposta» sarebbe l'etichetta di chi scrive sbagliata una volta su due. E la frase dice che cosa apre il collegamento: è la parola del modulo, non della pagina. |
| `flightops.reviewQueue` | una riga per tour: «4 report, il più vecchio dal <data> UTC» | **no** | Il numero è uno per tour, non uno per blocco: un'etichetta sarebbe una parola per tutte le righe, e sta dentro una frase con il plurale e una data (`waiting_one` dice «in attesa dal», `waiting_other` «il più vecchio dal»). Il nome della riga è il titolo del tour. |
| `flightops.myTours`, il riepilogo | una riga di tre: «11 leg accettate · 21 h 5 min volati · 1 tour completati» | **no** | Sono tre numeri, ma in una riga di frasi, non tre numeri con una parola sotto: «1 leg accettate» con la parola di chi scrive sarebbe sbagliata come «1 contestazioni». E il blocco è senza proprietà per scelta (T15b, il commento di `myToursBlock`: «No property — it is the reader's»): è di chi guarda, sta su `/me`, e un'etichetta sarebbe la sua prima proprietà. |
| `flightops.errorCatalog` | in ogni errore di tipo warning: «Warning, al massimo 3 volte all'anno» | **no** | Non è un numero del blocco: è la regola di quell'errore, scritta nel catalogo dei tour (`yearlyMax`), con il plurale (`warningMax_one` / `_other`). La parola che lo accompagna è del catalogo, non della pagina. |

Nessuno dei quattro ha un numero con una parola sotto: restano come sono, e il loro schema resta `z.object({})`. `questionOf` non
cambia niente per loro, e la loro richiesta dal vivo è quella di prima.

## 3. La strada scartata

**Ridisegnare il riepilogo di `myTours` come tre numeri con la parola sotto** (come `stats`), così che le etichette ci stiano. È una
scelta di aspetto di un blocco del pilota, non una conseguenza del §5.7, e la issue non la chiede: se un giorno la si vuole è una
issue sua, da Carmine. Scartata anche **l'etichetta come frase con un segnaposto** (`{{count}}` scritto da chi compone la pagina):
chiederebbe a uno staffer le due forme del plurale in ogni lingua, cioè il file delle lingue nel form.

## 4. Trovato strada facendo

Il riepilogo di `myTours` non ha le forme del plurale (`blocks.myTours.summary` usa `legs`, `tours`, non `count`): con un numero 1
dice «1 tour completati» e «1 leg accettate», «1 legs accepted» e «1 tours completed». È un difetto delle parole dell'hub, non delle
etichette: non si corregge qui, lo segnalo al master.

## Da portare nel piano

- **Il design dell'editor, `12-design-editor-fase-3.md` §5.7**, secondo punto: i blocchi di dati dei tour (`openIssues`,
  `reviewQueue`, il riepilogo di `myTours`, `errorCatalog`) **non prendono etichette**: in tutti il numero sta dentro una frase con il
  suo plurale, mai da solo con una parola sotto (issue #521, questa nota). L'etichetta del §5.7 è per un numero con una parola sotto.
