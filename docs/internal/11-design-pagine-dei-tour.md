# IVAO Division Hub — Design delle pagine dei tour (fase 6, m e q)

**Data:** 28 ottobre 2026, fase di test, fase 6 della nota `2026-10-22-il-piano-dopo-la-prova-dello-staff` (§4.G, voci m e q).
**Stato:** **Proposta**, da approvare da Carmine. **Nessun codice** finché non è approvata: la nota del 22 ottobre lo chiede («con il
loro design prima, da approvare»). Le domande sono nel §7.
**Che cosa tocca:** solo il modulo dei tour (`web/src/modules/flightops/`) e, per l'archivio, un parametro di una lettura che c'è già.
Nessuna migrazione, nessun componente nuovo nell'elenco chiuso (§6), il nucleo non cambia.

## 1. Che cosa c'è oggi

Letto nel codice di `main` (0.14.0), `web/src/modules/flightops/screens/`.

- **L'elenco, `/tours`** (`PublicToursPage`, `TourCards`): un titolo e una griglia di card dei tour aperti, in chiusura e in arrivo,
  ordinati per data di rilascio. Niente ricerca, niente filtri; un tour chiuso sparisce; un pilota vede la sua barra di progresso
  dentro la card, ma i tour che ha iniziato non sono in cima né separati dagli altri.
- **La pagina, `/tours/{slug}`** (`PublicTourPage`): **una colonna lunga** che impila, nell'ordine, banner, intestazione, briefing,
  «Il tour chiede» (aerei, rating, procedure, vincoli, callsign), sottotour, regole ed errori pubblici, il riquadro del pilota, mappa e
  tabella delle leg, i miei rapporti. Quello che il pilota viene a fare — vedere la prossima leg e mandare il rapporto — sta **sotto**
  il briefing e le regole, che sono la parte più lunga; la tabella delle leg è l'ultima cosa.
- **Le schermate per crearlo** (`TourEditor`): un tour nuovo nasce dal solo modulo «Impostazioni»; dopo il primo salvataggio la
  stessa schermata ha fino a otto schede (Impostazioni, Briefing, Leg, Hub, Sottotour, Callsign, Open, Regole), in un ordine che
  non è quello in cui si scrive un tour. Che cosa manca per «pronto» si vede (`ReadyProblems`) ma come un elenco di campi, non come la
  scheda dove andarli a scrivere.

## 2. I principi

1. **Il pilota prima.** Su ogni pagina, in alto, la risposta a «che cosa volo adesso?» e il tasto per farlo.
2. **Si legge dall'alto**: che cos'è il tour, come si vola, a che punto sono io. Il resto si apre quando serve.
3. **Lo stesso tour, gli stessi pezzi**: la card dell'elenco, la testata della pagina e l'anteprima nel back office sono la stessa
   cosa disegnata a tre misure, scritta una volta (`CLAUDE.md` §2).
4. **Nessun componente nuovo**: Atmosphere e l'elenco chiuso (`RouteMap`, `StatTile`, `Hero`, `EmptyState`, `Notice`, `RatingBadge`…).
5. **I numeri sono del server**: distanze, tempi, stato, progresso, «che cosa manca per pronto» — la pagina non ne calcola uno.

## 3. L'elenco dei tour (`/tours`)

- **I miei tour**, in cima e solo per chi è entrato: i tour iniziati e non finiti, ognuno con la barra e la prossima leg (quello che
  la card già sa, `my-tours`). Un tour finito va giù, tra gli altri, con il suo segno.
- **Le schede «Aperti» · «In arrivo» · «Archivio»**: l'archivio sono i tour chiusi (oggi spariscono; un pilota li cerca per l'award
  o per i suoi rapporti). Lato server, `PublicTours.CardsAsync` sa già filtrare per stato (lo usa il blocco `flightops.tourCards`):
  l'endpoint `/public` prende gli stati come parametro e l'archivio è `Closed` chiesto alla stessa lettura.
- **Una ricerca per nome e un filtro per tipo** (Sequenziale, Hub, Libero, Distanza, Open, Con sottotour), nell'indirizzo come la
  lista generata (`validateSearch`), così un link porta a una vista.
- **La card**: copertina, stato, tipo, titolo, riassunto su due righe, periodo; sotto, una riga di numeri — leg, miglia, rating minimo
  (`RatingBadge`), l'award se c'è. Un tour con sottotour dice quanti ne ha.

## 4. La pagina di un tour (`/tours/{slug}`)

- **La testata** (`Hero` con il banner): titolo, stato, tipo, periodo; sotto, una striscia di `StatTile` — leg, miglia, tempo stimato,
  rating minimo, aerei ammessi, award. È la carta d'identità del tour: oggi le stesse cose sono sparse fra intestazione, «Il tour
  chiede» e la riga dei totali.
- **Il tuo tour**, subito sotto, per chi è entrato: la barra, **la prossima leg con «Invia il rapporto» e SimBrief**, e — se non può
  volare — perché (rating, ban, un rapporto da correggere: i rifiuti che `mine` dà già). Per chi non è entrato: «Entra per volare
  questo tour». Per un tour con sottotour, i sottotour con il loro progresso.
- **Quattro schede** sotto (ancore nella pagina, `?tab=` nell'indirizzo come il back office):
  1. **Panoramica**: il briefing, com'è oggi.
  2. **Percorso**: la mappa e le leg, con lo stato del pilota leg per leg. Un tour `Hub` raggruppato per hub e rotazione; su telefono
     la tabella diventa un elenco di leg.
  3. **Regole**: «Il tour chiede» (aerei, rating, procedure, vincoli, callsign) e le regole, raggruppate e chiuse a fisarmonica con il
     loro titolo; gli errori pubblici in fondo; «Chiedi su questa regola» resta dov'è.
  4. **I miei rapporti** (solo per chi ne ha): com'è oggi.

## 5. Le schermate per creare un tour (q)

- **Un tour nuovo è un percorso a passi**, nell'ordine in cui lo si scrive: (1) **Tipo**, con una riga che spiega ogni tipo; (2) **Nome,
  date e immagini**; (3) **Percorso**: leg (griglia o file), hub e rotazioni, sottotour o obiettivo, secondo il tipo; (4) **Regole**;
  (5) **Briefing**; (6) **Controllo**: «che cosa manca per pronto» (`ready-problems` del server), ogni voce con il link al passo, e
  «Segna pronto». Si salva a ogni passo: il tour esiste come bozza dal passo 2, come oggi dopo il primo salvataggio.
- **Un tour che esiste** ha gli stessi passi come schede, nello stesso ordine, con un segno su quelle che hanno qualcosa da sistemare
  per «pronto». Callsign e Open stanno dentro «Percorso» e «Regole», non in schede loro.
- **L'anteprima**: la pagina pubblica del §4 disegnata con la bozza, dentro il back office, prima del rilascio.
- Restano il form generato (`SchemaForm`) e la griglia delle leg (`LegGrid`): cambia dove stanno, non come si scrivono.

## 6. Che cosa non cambia

Il modello, le API del back office, le regole del server, i blocchi del CMS (`flightops.tourCards` disegna le card del §3 e le segue).
Nessun componente nuovo: i passi del §5 sono le `Tabs` di Atmosphere con il numero davanti, la striscia del §4 è `StatTile`.

## 7. Le domande per Carmine ⚖️

1. **L'archivio dei tour chiusi** nell'elenco: (a) sì, una scheda «Archivio» — **raccomandata**; (b) no, i chiusi restano fuori.
2. **La pagina di un tour**: (a) testata, «Il tuo tour» e quattro schede — **raccomandata**; (b) la pagina lunga di oggi, riordinata
   con il pilota in alto.
3. **Creare un tour**: (a) un percorso a passi per il tour nuovo e le stesse schede per quello esistente — **raccomandata**; (b) le schede
   di oggi, riordinate, con «che cosa manca» che porta alla scheda giusta.
4. **L'ordine del codice**, dopo l'approvazione: (a) prima la pagina e l'elenco (li vedono i piloti), poi le schermate dello staff —
   **raccomandata**; (b) prima le schermate dello staff.

## Da portare nel piano

- **Design M2 §8.1** (le pagine pubbliche dei tour) e **§8.3** (le schermate dei tour nel back office): sostituite da questo documento
  quando è approvato.
