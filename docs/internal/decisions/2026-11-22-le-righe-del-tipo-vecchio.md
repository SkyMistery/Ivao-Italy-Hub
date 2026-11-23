# Le righe che il tipo nuovo di un tour non usa: si vedono e si eliminano

**Data:** 22 novembre 2026, issue #549, nata dalla revisione del master della #543 (i passi di un tour).
**Stato:** **deciso da Carmine**, il 22 novembre 2026, in chat al master: «A, eliminare»; riportato sulla issue:
https://github.com/SkyMistery/Ivao-Italy-Hub/issues/549#issuecomment-7086892248. Il worker aveva proposto le tre strade al master con la
raccomandazione (a).
**Classificazione** (`CLAUDE.md` §5): (b) dentro il modulo dei tour. Una guardia del modulo si allarga, nessun verbo nuovo, nessuna
migrazione, nessun componente nuovo, il nucleo non cambia.

## La domanda

Il salvataggio di un tour in bozza lascia cambiare il tipo e tiene le righe del tipo di prima. Il server le segnala a «pronto»
(`TourShape`): `kindHasNoLegs` (`legs`), `kindHasNoHubs` (`hubs`), `kindHasNoConstraints` (`constraints`). L'editor non le disegnava più,
perché il passo «Percorso» segue il tipo (`routeParts`): «Vai al passo» portava a un passo senza le righe, e il tour non si segnava mai
pronto. Cancellare vincoli, hub e rotazioni il server lo lasciava già fare con qualunque tipo, perché guarda il tipo solo al salvataggio.
Le leg no: `WritableTourAsync` rifiutava anche `remove` e `removal` su un tipo senza leg.

- (a) **Le righe rimaste si vedono nel passo dove porta «Vai al passo»**, in sola lettura, ognuna con «elimina» e una riga che dice
  perché sono lì; sul server si allarga solo la guardia della rimozione di una leg — **deciso**.
- (b) Il salvataggio del tour cancella al cambio di tipo, con una conferma, le righe che il tipo nuovo non usa: un comportamento nuovo del
  salvataggio, le leg con rapporti da ritirare dentro di esso, e i tour già bloccati restano bloccati.
- (c) Il salvataggio rifiuta il cambio di tipo finché le righe ci sono (`kindLeavesRows`): una regola nuova; un tour già bloccato si
  sblocca solo rimettendo il tipo di prima.

## Com'è fatto

- **Il server**: `WritableTourAsync(tourId, http, removing: true)` lascia passare `GET legs/{id}/removal` e `POST legs/{id}/remove` anche su
  un tour `Open` o `Container`. Creare, modificare, ripristinare e importare restano rifiutati con `kindHasNoLegs`. La rimozione è quella di
  sempre (§1.4.1): cancellata senza rapporti, ritirata con un motivo altrimenti; una leg ritirata non conta per «pronto» (`TourShape`).
- **Il browser**: quali righe restano lo dice il server, non il browser. `leftoverParts` (`screens/tourSteps.ts`) legge i campi e le
  chiavi di `ready-problems`. «Percorso» disegna sotto le parti del tipo:
  - le leg rimaste: `LegGrid` con `leftover`, celle bloccate e solo «Rimuovi»; niente «Aggiungi», file o «Aggiungi dopo», e da vuota non
    invita ad aggiungere;
  - hub e rotazioni rimasti: `TourHubsTab` con `leftover`, cioè la stessa lista generata senza «Nuovo» e con «Elimina» confermato al
    posto di «Modifica»; il rifiuto del server (un hub con rotazioni, una rotazione con leg) compare sopra la lista.

  «Regole» disegna i vincoli rimasti nello stesso modo (`TourConstraints` con `leftover`). Ogni parte ha sopra una riga (`Notice`) che dice
  perché è lì.
- **Il passo 1** avvisa, prima del salvataggio, quali righe resterebbero se il tour ne ha (`partsLeftBy`): le leg verso un tipo senza leg,
  hub e rotazioni lasciando un tour a hub per un altro tipo **con** leg. ⚠️ Su un tipo senza leg (`Open`, `Container`) `TourShape` nomina
  le leg e si ferma lì: hub e rotazioni rimasti non sono un problema di «pronto», quindi l'editor non li mostra e il passo 1 non li nomina.
  Restano nel database senza effetto, e tornano un problema se il tour riprende un tipo con leg. La prima corsa della CI l'ha trovato
  (la spec si aspettava `kindHasNoHubs` su un `Open`); cambiare `TourShape` sarebbe una regola nuova, fuori da questa decisione. È solo un avviso: non legge le righe e non blocca niente. I vincoli non ci sono, perché il
  server rifiuta già un `Open` con vincoli che cambia tipo (`openHasConstraints`, design M2 §2.6.1). Per la stessa ragione oggi un tour
  arriva a vincoli rimasti solo per strade diverse dall'editor, ma la riga li tratta comunque come le altre parti.
- **Il salvataggio del tour non cambia**, il design 11 §5 nemmeno: le righe stanno nel passo che già le elencava.

## Che cosa resta fuori

Da un tour a hub verso un altro tipo con leg (`Sequential`, `Free`…), le leg di una rotazione restano nella griglia normale con
`legHubOnly`: la colonna della rotazione si vede solo su un tour a hub. Si tolgono rimuovendole, poi si eliminano rotazioni e hub;
staccarle dalla rotazione senza perderle non è possibile dall'editor. Non era nella issue: se serve, è una domanda a parte.

## I test

- `OpenTourTests.TheLegsOfATourThatBecameOpenAreRemovedAndNeverWritten` (integrazione, MariaDB vera): un tour `Sequential` con due leg
  diventa `Open`; «pronto» dice `kindHasNoLegs`; creare, modificare, l'anteprima del file e l'importazione restano 400; `removal` risponde
  `Delete`, le due `remove` passano, e «pronto» non nomina più le leg.
- `LegGrid.test.tsx` (vitest): una leg rimasta ha le celle bloccate, niente «Aggiungi», file né «Aggiungi dopo», e «Rimuovi» chiama il
  server. Rimettendo la barra e «Aggiungi dopo» senza la condizione, il test diventa rosso.
- `tourSteps.test.ts` (vitest): `leftoverParts` e `partsLeftBy`.
- `e2e/full/tours-leftover.spec.ts` (server vero): un tour a hub con un hub e due leg diventa `Open` dal passo 1, con l'avviso; il
  Controllo nomina le leg (non l'hub) e porta a «Percorso»; lì niente «Aggiungi», le leg rimosse, «pronto» non le nomina più. Poi il tour
  diventa `Sequential`: il Controllo nomina l'hub, «Percorso» lo mostra senza «Nuovo hub», l'hub si elimina e «pronto» non lo nomina più.

## Da portare nel piano

- **Design 11 §5** («Com'è andata»): le righe del tipo di prima, in sola lettura con «elimina», nel passo dove porta la loro voce; l'avviso
  al passo 1.
- **Design M2 §1.4.1** (la rimozione di una leg) e **§2.6/§2.7** (i tipi senza leg): la rimozione di una leg passa anche su un tour `Open`
  o `Container`, per le leg rimaste da un tipo di prima; ogni altra scrittura di una leg resta rifiutata (`kindHasNoLegs`). Deciso da Carmine
  il 22 novembre 2026 (https://github.com/SkyMistery/Ivao-Italy-Hub/issues/549#issuecomment-7086892248).
