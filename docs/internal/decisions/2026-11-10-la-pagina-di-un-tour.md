# La pagina di un tour: che cosa il design non diceva

**Data:** 10 novembre 2026, fase 6, primo passo del codice (`11-design-pagine-dei-tour.md` §4; prima PR delle due, la pagina; l'elenco
del §3 è la seconda, in fila).
**Stato:** **scelte della sessione di lavoro**, dentro quello che il design ha già deciso (approvato da Carmine il 31 ottobre 2026,
§8). Il punto 1 si scosta dalla lettera del §4 («`Hero` con il banner») e va **confermato da Carmine**; gli altri dicono dove e come,
non che cosa. Il revisore li controlla uno per uno.
**Classificazione** (`CLAUDE.md` §5): (a) e (b) dentro il modulo dei tour. Nessuna migrazione, nessun componente nuovo, il nucleo non
cambia: il §6 del design lo chiede e la PR lo rispetta.

## 1. La testata non è `Hero` del nucleo ⚖️

Il §4 dice «la testata (`Hero` con il banner)». `Hero` (`shared/ui/layout-pieces.tsx`) disegna il titolo come `H2` dentro un riquadro e
**non prende un'immagine**: con `Hero` la pagina perderebbe il suo unico `H1` (l'accessibilità e i test e2e lo chiedono, `heading level 1`)
e il banner andrebbe comunque disegnato fuori. Le due strade:

- (a) **la testata scritta nel modulo con i pezzi di Atmosphere** (`H1`, `Lead`, `Badge`) e il banner sopra, come la testa di un evento
  (`EventHead`, nota `2026-10-11-la-testa-della-pagina-di-un-evento`), con la striscia di `StatTile` del nucleo sotto — **fatta così**;
- (b) estendere `Hero` nel nucleo (un'immagine e il livello del titolo): una PR del nucleo a parte, con una nota sua, prima di questa.

**Raccomandata la (a)**: `Hero` oggi non lo usa nessuna pagina (solo la galleria), il suo scopo è «la cima di una pagina pubblica, in una
frase», e una testata con banner, stato, tipo, periodo e numeri è un pezzo del tour, scritto una volta (`TourHead`, `TourFace.tsx`) e
ripreso dall'anteprima del back office al passo dopo. Se Carmine sceglie la (b), cambia solo l'involucro di `TourHead`.

## 2. I numeri della testata

La striscia (`tourFigures`, `screens/tourPage.ts`) mostra solo ciò che il tour ha, nell'ordine: tratte, distanza, tempo stimato (con
l'aereo di riferimento sotto), distanza da percorrere (`Distance`), sottotour e quanti da completare (`Container`), obiettivo (`Open`),
rating minimo (il nome corto, e quello lungo sotto), aerei ammessi, award. Un numero che il tour non ha non c'è, invece di un trattino.

- **Gli aerei**: al più due per nome, e «e altri N»; l'elenco intero resta nella scheda «Regole», sotto «Che cosa chiede il tour». Un
  contenitore non dice aerei: li dicono i suoi sottotour.
- **L'award per nome** arriva dal server: `PublicTourDto.Award` (`PublicAwardDto`, id e nome), letto dal catalogo del nucleo, che è
  condiviso in lettura (`Award : ISharedForReading`) e che il modulo legge già per controllarlo (`TourRules`). Un campo **aggiunto** alla
  risposta pubblica del modulo: il §6 del design dice «le API del back office non cambiano», e questa non è del back office.
- **I totali** della tabella delle tratte (la riga «Totale» in fondo) non ci sono più: sono nella testata.

## 3. «Il tuo tour»

- **La barra** è quella delle card (`ProgressLine`, da `/my-tours`): un pilota che non ha ancora iniziato il tour non ha una barra,
  ha la prossima tratta. Il pannello chiede `/my-tours` accanto a `…/reports/mine`; è la stessa risposta che le card dell'elenco chiedono,
  e il browser la tiene una volta.
- **La prossima tratta** è `mine.next`, o la prima di `mine.flyable` (`nextLeg`): numero, aeroporti, miglia, tempo, callsign, con «Invia il
  report» e SimBrief. Su un tour `Open` «Invia il report» senza tratta, come prima.
- **Se non può volare**, il primo rifiuto che `mine` dà (`blocked`), in un avviso. Quando lo ferma un report «da modificare», accanto c'è
  **«Correggi il report»**, che apre quel report: il principio 1 vuole il tasto per fare ciò che va fatto adesso.
- **Un contenitore**: la barra dei suoi sottotour, e i sottotour uno per riga con il loro avanzamento o «Non ancora iniziato».
- **Un visitatore**: un riquadro con una riga e «Entra per volare questo tour» (`signInToReport`, parola cambiata in quella del design).

## 4. Le quattro schede

- **I nomi nell'indirizzo** sono `overview`, `route`, `rules`, `reports` (`TOUR_PAGE_TABS`, `tourPageSearchSchema`); la prima non scrive
  niente (`/tours/{slug}` resta l'indirizzo del tour). Un nome che il tour non ha, o scritto a mano, apre la prima.
- **Quali schede**: «Percorso» solo se c'è qualcosa da disegnare (tratte, o i sottotour di un contenitore); «I tuoi report» solo a un
  pilota che ne ha — e anche mentre la sua risposta arriva, così `?tab=reports` non cade sulla prima scheda durante il caricamento.
- **Dopo l'invio di un report** la pagina del report torna su `?tab=reports`, dove c'è il report appena mandato: la prima scheda è il
  briefing, e il pilota cercherebbe il suo report.
- **I link del server** aprono la scheda giusta: la mail di un report deciso apre i report, il riferimento di un chiarimento a una tratta
  il percorso, a una regola le regole (`PublicTours.RouteTab`, `RulesTab`, `ReportsTab`).
- **Su telefono** la tabella delle tratte è la stessa tabella, con ogni riga disposta come un blocco (una sola marcatura, non un secondo
  elenco da tenere in pari); le colonne «Partenza» e «Arrivo» diventano una, «Percorso», con la freccia.
- **Un tour `Hub`**: le tratte raggruppate per rotazione, nell'ordine in cui il tour le vola, ognuna con «Rotazione N da HUB» (numerata
  fra le rotazioni dello stesso hub, nell'ordine dell'editor), e i collegamenti fra due hub come gruppi loro («Fra due hub»).
- **Le regole** in due gruppi — quelle di ogni tour (della divisione) e quelle di questo tour —, ognuna chiusa sotto codice, titolo e
  valori; aperta, il testo, «modifica R…» quando corregge una regola della divisione, e «Chiedi» per chi è entrato.

## Da portare nel piano

- **Il design `11-design-pagine-dei-tour.md` §4**: la testata è scritta nel modulo con i pezzi di Atmosphere e `StatTile`, non con `Hero`
  (punto 1, se Carmine lo conferma); i nomi delle schede nell'indirizzo, la scheda dei report dopo l'invio, i link del server che aprono
  la scheda (punto 4).
- **Design M2 §8.1**: la pagina pubblica dice l'award del tour per nome (`PublicTourDto.Award`); la tabella delle tratte non ha più la
  riga dei totali, che sono nella testata.
