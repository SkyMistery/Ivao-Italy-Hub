# La cura che una riga prende dal genitore (#447)

**Data:** 5 novembre 2026, fase di test (nota `2026-10-18-la-fase-di-test`), branch `fix/review-lot1b-guard`.
**Stato:** **decisa** — **Carmine in chat al master, 4 novembre 2026** («A, come consigli»), pubblicato su sua istruzione sulla PR
([la risposta][a447]).
**Regola applicata:** `CLAUDE.md` §5, caso **(c)**: un meccanismo nuovo del nucleo, piccolo, che due moduli usano.

[a447]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/447#issuecomment-6632448319

## Che cosa serve

Le righe di un evento (slot, aeroporti, rotte, domande, regole dei premi, attività, posizioni e turni ATC, prenotazioni, iscrizioni) e
quelle della forma di un tour (hub, rotazioni, regole, vincoli) hanno i dipartimenti del loro genitore. Prima che si chieda il permesso
li prendono dal genitore **com'è adesso** (`EventChildren.AdoptAsync`, `TourChildren.AdoptAsync`, in `CrudOptions.BeforeAuthorize` e nei
verbi scritti a mano): la revisione della #233, punto 1, vuole che lo staff tolga una prenotazione con il permesso sull'evento com'è ora,
non com'era il giorno della prenotazione.

La #447 (issue #319, elemento 1) fa giudicare al guardiano delle scritture una cancellazione sui dipartimenti **com'erano caricati**, per
chiudere «carico una riga, ci metto il mio dipartimento, la cancello». Le due regole si scontravano: la copia che una riga di un evento
tiene nella tabella può essere vecchia — l'evento ha cambiato cura dopo —, e chi è entrato nella cura dell'evento non poteva più
cancellarla (`EventsBookingsTests.TheStaffTakeABookingAwayWithThePermissionOnItsEventAsItIsNow`, rosso in CI). Lo stesso per ogni riga
figlia cancellata dal motore generico, che chiama `BeforeAuthorize` anche prima di cancellare; e una modifica di una riga così era già
rifiutata prima della #447, perché lo spostamento chiedeva il permesso sui dipartimenti vecchi.

## La decisione

**`RowCare.Adopt`** (`Core/Data/RowCare.cs`): scrive il dipartimento e la cura del genitore sulla riga **come se la riga fosse stata
salvata con quelli** — valori correnti e valori originali —, e li segna cambiati quando la tabella tiene ancora la copia vecchia, così una
modifica li scrive.

- Il guardiano non cambia, ed è sempre uno: giudica la riga sul suo genitore com'è ora (la #233, punto 1, resta com'è) e non su un
  dipartimento messo da chi scrive (l'elemento 1 della #319 resta chiuso), perché i dipartimenti vengono dal genitore.
- `EventChildren.AdoptAsync` e `TourChildren.AdoptAsync` lo usano, una riga ciascuno. La rimozione di una prenotazione e di un'iscrizione,
  scritte a mano, passano già da `EventChildren.AdoptAsync`.
- Le altre copie della cura nei tour (`RuleEndpoints`, `LegBook`, `LegEndpoints`, `TourRules`) non cambiano: le righe di un tour seguono il
  tour nella tabella quando la sua cura cambia, quindi la loro copia non resta vecchia.
- **Il «prima» dell'audit** di una riga adottata mostra i dipartimenti del genitore, non la copia vecchia: è il valore originale che
  l'helper ha scritto. Il «dopo» di una modifica è lo stesso; la copia vecchia non compare più.

## Alternative scartate

- (B) Le righe seguono il genitore nella tabella anche negli eventi, quando la cura cambia: una scrittura su ogni riga figlia a ogni cambio,
  e una migrazione di dati per le righe già vecchie.
- (C) Togliere l'elemento 1: si riapre il buco trovato dalla revisione.
- (D) Un'eccezione nel guardiano per le righe di un genitore: il guardiano non sa quale sia il genitore, e sarebbe un caso per modulo.

## Da portare nel piano

- `00-piano-di-progettazione.md` §16 (il guardiano delle scritture e i meccanismi generici): `RowCare.Adopt`, una riga che prende la cura
  dal genitore la prende come valore salvato; il «prima» dell'audit di una tale riga mostra la cura del genitore; versione e changelog.
- `09-design-m4.md` §3.6 (lo staff toglie una prenotazione) e la nota della #233: la regola «come l'evento è adesso» passa per `RowCare`.
