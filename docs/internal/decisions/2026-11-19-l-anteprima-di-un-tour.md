# L'anteprima di un tour: un endpoint del modulo

**Data:** 19 novembre 2026, fase 6, voce (q), seconda PR delle due (`11-design-pagine-dei-tour.md` §5, ultimo punto: «L'anteprima: la
pagina pubblica del §4 disegnata con la bozza, dentro il back office, prima del rilascio»). In fila dopo i passi (#543).
**Stato:** **deciso da Carmine**, il 19 novembre 2026, in chat al master: «Sì, l'endpoint»; riportato sulla #544:
https://github.com/SkyMistery/Ivao-Italy-Hub/pull/544#issuecomment-7014155806. Il master l'aveva proposto alla sessione di lavoro il
18 novembre. È una **deviazione dal §6 del design 11** («le API del back office non cambiano»), che il §5 rende necessaria.
**Classificazione** (`CLAUDE.md` §5): (b) dentro il modulo dei tour. Nessun file del nucleo cambia (lo `schema.d.ts` generato a parte),
nessuna migrazione, nessun componente nuovo.

## La domanda

Il §5 vuole la pagina pubblica disegnata con la bozza. La pagina legge `PublicTourDto` da `/api/flightops/tours/public/{slug}`, che a una
bozza risponde 404: è giusto, il pubblico non vede le bozze. I numeri della pagina — distanze, tempi, totali, regole in vigore, errori
pubblici, award per nome — sono del server (principio 5 del design 11), composti da `PublicTours.ReadAsync`.

- (a) **Un endpoint del modulo**, `GET /api/flightops/tours/{id}/preview`, che risponde lo stesso `PublicTourDto` composto dallo stesso
  codice, con la riga letta dal back office — **deciso**.
- (b) Ricomporre `PublicTourDto` nel browser da sei letture del back office (tour, leg, hub e rotazioni, vincoli, callsign, regole in
  vigore): una seconda copia di `PublicTours.ReadAsync`, e i numeri calcolati in due posti.
- (c) L'anteprima della sola testata (`TourHead`) con i campi della bozza: niente server, ma niente percorso, regole né numeri.

## Com'è fatto

- **Il server**: `PublicTours.ReadAsync` è diviso in due. La lettura pubblica trova la riga (con `Public(...)`, come prima) e poi chiama
  `ComposeAsync`; `PreviewAsync(tour)` prende la riga che l'endpoint ha letto con `CrudSource.BackOffice<Tour>` e passato all'handler unico
  (`Tours.View`, come `ready-problems`), e chiama lo stesso `ComposeAsync`. La pagina si compone in un posto solo.
- **Senza indirizzo o senza date non c'è pagina**: `PublicTourDto` ha indirizzo e date non nulli, e non diventa nullable per l'anteprima.
  L'endpoint rifiuta con `flightops:errors.previewNeedsAddressAndDates` sul primo campo che manca (`slug`, `releaseAt`, `closeAt`); il
  browser non chiede nemmeno, e manda al passo 2. Un tour ha l'indirizzo dal primo salvataggio; resta senza date quello creato da un modello,
  e un modello non ha né l'uno né le altre.
- **Che cosa mostra**: lo stato vero della riga (una bozza dice «Bozza»); il contenitore di un sottotour solo se ha un indirizzo; i
  sottotour di un contenitore come li vedrebbe il pubblico, cioè solo quelli pubblici, come sulla pagina.
- **Il browser**: `TourPreview` (`screens/TourPreview.tsx`) sta nel passo Controllo, sotto «Segna pronto»: la testata (`TourHead`) e le
  schede che un visitatore trova — panoramica, percorso, regole — con i pezzi della pagina pubblica (`Overview`, `RouteTab`, `TourFacts`,
  `TourRulesSection`, ora esportati da `screens/public.tsx`, che non cambia altrimenti). Niente «Il tuo tour», niente rapporti, niente tasti
  per inviare un rapporto o aprire SimBrief: nessuno vola una bozza. La scheda aperta dell'anteprima resta sullo schermo, non
  nell'indirizzo, che è dell'editor.
- **Due `H1`** nello schermo: quello dell'editor e quello della pagina disegnata. È la pagina com'è; la sezione dell'anteprima ha un nome
  (il suo titolo), e chi la cerca la trova per nome.

## I test

`TourPreviewTests` (integrazione, MariaDB vera): una bozza si vede con la risposta del sito mentre il sito le risponde 404; un modello e
un tour senza date sono rifiutati sul campo; un membro senza `Tours.View` riceve 403; uno staff con `Tours.View` solo sul suo
dipartimento riceve 403 su un tour di un altro, e lo legge quando la cura del tour si allarga al suo. `TourPreview.test.tsx` (vitest): la
pagina disegnata senza niente di un pilota, e senza date il link al passo 2 e nessuna chiamata.

## Da portare nel piano

- **Design 11 §6**: «le API del back office non cambiano» ha un'eccezione, l'anteprima (`GET /api/flightops/tours/{id}/preview`), decisa
  da Carmine il 19 novembre 2026 (https://github.com/SkyMistery/Ivao-Italy-Hub/pull/544#issuecomment-7014155806) perché il §5 la richiede e l'alternativa era copiare `PublicTours.ReadAsync`.
- **Piano §16.6** (gli endpoint scritti a mano, per famiglia): uno in più nella famiglia dei tour, la lettura dell'anteprima, accanto a
  `ready-problems`.
