# L'elenco dei tour: che cosa il design non diceva

**Data:** 10 novembre 2026, fase 6, primo passo del codice (`11-design-pagine-dei-tour.md` §3; seconda PR delle due, in fila dopo
quella della pagina di un tour, nota `2026-11-10-la-pagina-di-un-tour`).
**Stato:** **scelte della sessione di lavoro**, dentro quello che il design ha già deciso (approvato da Carmine il 31 ottobre 2026,
§8: l'archivio è la risposta 1). Nessuna si scosta dalla lettera del §3; dicono dove e come. Il revisore le controlla una per una.
**Classificazione** (`CLAUDE.md` §5): (a) e (b) dentro il modulo dei tour. Nessuna migrazione, nessun componente nuovo, il nucleo non
cambia.

## 1. L'archivio

- **Il parametro** è `GET /api/flightops/tours/public?states=Closed`, come dice il §3: la stessa lettura (`PublicTours.CardsAsync`)
  con gli stati chiesti. Senza parametro, i tre stati di una card di oggi. Gli stati che l'elenco può chiedere sono quattro
  (`PublicTours.ListStates`); uno che non è fra questi **restringe a niente**, non allarga a tutto, come la proprietà del blocco.
- **L'ordine**: prima i tour che si volano (per rilascio), poi quelli in arrivo (il più vicino prima), poi l'archivio **dal più
  recente chiuso**: chi lo apre cerca il tour di quest'estate, non quello di tre anni fa.
- **Chiesto solo quando la scheda si apre**: chi non apre l'archivio non lo scarica.
- **Il blocco `flightops.tourCards` non ha l'archivio**: il §6 dice che i blocchi non cambiano, e le sue tre scelte restano quelle.

## 2. «I miei tour»

In cima, per chi è entrato, i tour **di primo livello** che il pilota ha iniziato e non finito, fra quelli che si volano (aperti o in
chiusura): sono la risposta di `/my-tours`, la stessa delle card. Un tour lì in cima **non è ripetuto** nella scheda sotto. Un tour
finito resta nella sua scheda, fra gli altri, con il suo segno («Completato» della barra). Un sottotour iniziato non è in cima: sta
nel suo contenitore, come nella pagina di quello. Un tour chiuso prima che il pilota lo finisse è nell'archivio con la sua barra.

## 3. La ricerca e il tipo

- **Nel browser**, sulle card della scheda: la lettura del server dà tutti i tour pubblici di uno stato in una volta (qualche decina),
  e una ricerca del server sul titolo tradotto, una colonna JSON per lingua, sarebbe un secondo meccanismo per lo stesso elenco. Il
  principio 5 parla dei numeri, che restano del server.
- **La ricerca** trova ogni parola nel titolo, nella lingua sullo schermo, senza maiuscole né accenti («citta giro» trova «Giro delle
  Città»).
- **Il tipo** offre solo i tipi che la scheda ha, come `/events` (un tipo che nessun tour ha porta a una scheda vuota), con le parole
  dei tipi che il modulo ha già: sette, non le sei del §3, perché «In sequenza con partenza a scelta» è un tipo suo.
- **Nell'indirizzo**: `?tab=` (`open`, che non si scrive, `upcoming`, `archive`), `?q=`, `?kind=` (`tourListSearchSchema`). Cambiando
  scheda la ricerca resta e il tipo si toglie: il tipo di una scheda raramente è nell'altra.

## 4. La card

- **Il periodo** («Dal … al …») al posto di «Apre il …» / «Aperto fino al …»: è la parola del §3, e regge anche per l'archivio.
- **La riga dei numeri** arriva dal server, campi **aggiunti** a `PublicTourCardDto`: `MinPilotRatingName` (il nome corto, disegnato
  con `RatingBadge`), `Award` (`PublicAwardDto`, per nome), `Subtours` (quanti sottotour pubblici ha un contenitore, «4 sottotour»).
  Un contenitore non ha tratte sue, e non dice «0 tratte».
- **Il riassunto su due righe** (`line-clamp-2`), come dice il §3.
- **Lo stato e il tipo** sono lo stesso pezzo della testata (`TourBadges`, principio 3), con un colore per stato: in arrivo indaco, aperto
  blu, in chiusura arancione, chiuso grigio.
- **Le card dei sottotour** nella pagina di un contenitore dicono il tipo del sottotour (`PublicSubtourDto.Kind`, campo aggiunto):
  prima dicevano quello del contenitore, «Con sottotour», su ogni sottotour.

## Da portare nel piano

- **Il design `11-design-pagine-dei-tour.md` §3**: l'archivio con `?states=Closed` e il suo ordine, chiesto all'apertura della scheda;
  «I miei tour» di primo livello e non ripetuti sotto; la ricerca e il tipo nel browser, nell'indirizzo, con i tipi che la scheda ha.
- **Design M2 §8.1 e §8.2**: la card dice il periodo e la riga dei numeri (rating minimo, award, sottotour); il blocco
  `flightops.tourCards` disegna la stessa card con gli stessi campi, e le sue tre scelte di stato non cambiano.
