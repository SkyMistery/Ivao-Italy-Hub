# Le pagine della validazione (T13b)

**Data:** 23 settembre 2026 — fase T13b di M2
**Stato:** **decisa** (Carmine, 23 settembre 2026, due risposte in apertura; il resto è design già scritto o scelta tecnica
dichiarata qui sotto)
**Regola applicata:** `CLAUDE.md` §5. Due **precisazioni** decise da Carmine (il giro e2e con due persone e Mailpit, il contenuto del
blocco `reviewQueue`) e **tre estensioni di meccanismi esistenti** (caso b): `RouteMap` con la traccia, la prima lettura di una
preferenza dal browser, il login del banco con un secondo membro.

## 1. Che cosa serve

Piano di implementazione, parte C, T13b (nota `2026-09-23-la-validazione` §2.1): `/staff/tours/review` (coda unica e per tour, l'ordine
come preferenza), `/staff/tours/review/{id}` (mappa con la traccia, revisioni del piano, tabella degli errori, suggerimento chiesto al
server, decisione, riapertura), la voce di menu, il blocco `flightops.reviewQueue` nelle due metà, lo smoke e il giro e2e. Il «fatta
quando» di T13 è di T13b: *un PIREP del corpus si prende, si decide con un errore e il pilota riceve la mail in Mailpit.*

## 2. Le due risposte di Carmine

1. **Il giro e2e con due persone e Mailpit.** Il banco era una persona sola (il WM, senza indirizzo) e nessuno valida i propri PIREP:
   il «fatta quando» non si poteva provare. **Deciso**: il login del banco accetta **`?as=pilot`**, un secondo membro configurato
   (`E2E:Pilot`, senza posizioni, con un indirizzo `bench-pilot@bench.test`); **Mailpit diventa un servizio della CI** (la stessa
   immagine di `docker-compose.yml`) e l'SMTP del banco punta lì. Il giro `full/tours-review.spec.ts`: il pilota invia il volo
   registrato (`replayFlight`), il WM lo prende, spunta un errore `Dangerous` di una regola del tour, legge il suggerimento girare a
   «rifiutato», rifiuta; la spec aspetta la mail (il job gira ogni minuto) e controlla che porti la regola e la nota e **non** il nome
   né il VID di chi ha deciso. Si tocca solo l'infrastruttura del banco. Scartati: il secondo membro senza Mailpit (la mail solo a
   mano), e nessun cambio al banco.
2. **Il blocco `flightops.reviewQueue` è per tour**: una riga per tour che chi guarda può validare, quanti PIREP aspettano e da quando
   il più vecchio, con il link alla coda di quel tour — il riepilogo giornaliero su una dashboard. Aspettare vuol dire quello che vuol
   dire per il riepilogo: in coda, o presi con il lease scaduto; i PIREP di chi guarda non contano. Nessuna proprietà. Scartato: i
   PIREP uno per uno con un limite (lungo con 50 in coda, e la coda c'è già).

## 3. Le estensioni (caso b)

### 3.1 `RouteMap` disegna la traccia

La pagina di validazione vuole «la mappa della tratta con la traccia volata» (design §4.3), e `RouteMap` disegnava solo archi fra
aeroporti. **Deciso**: una proprietà in più, **`tracks`** (punti in ordine), disegnata **in rosso** — nessuna leg usa quel colore — sopra
le leg, senza marcatori, dentro la vista che la mappa inquadra. Il passaggio dell'antimeridiano è lo stesso degli archi
(`trackPath` in `greatCircle.ts`, con il suo test). Non è un componente nuovo dell'elenco chiuso: è `RouteMap` con un dato in più. La
galleria mostra una traccia d'esempio.

### 3.2 La prima preferenza letta dal browser

T4b aveva l'endpoint `/api/me/preferences/{key}` e nessuna schermata che lo leggesse. `preferenceQuery(key)` e
`useSavePreference(key)` stanno in `features/me/queries.ts`, accanto alle preferenze di notifica: un modulo li usa, il nucleo non
nomina la chiave. La coda legge `flightops.reviewQueueOrder` e la manda come `sort` (`queuedAt` | `tourId`); la scelta si salva
all'istante.

### 3.3 Il secondo membro del banco

`E2EOptions.Pilot` e `?as=pilot` su `/e2e/signin` (§2.1): dietro gli stessi due lucchetti di sempre (ambiente `E2E` e
`E2E:Enabled`). Un `?as=` sconosciuto o un pilota non configurato rispondono 404.

## 4. Le scelte tecniche (dichiarate, non domandate)

- **Il browser non legge il JSON di IVAO.** T13a mandava le revisioni del piano come erano salvate, il payload grezzo della rete.
  Ora la pagina le riceve già lette (`ReviewPlanDto`: revisione, orari, regole, aereo, equipaggiamento, livello, velocità, rotta,
  alternati, partenza ed EET in minuti), con il lettore del client del nucleo (`IvaoTrackerReader.ReadFlightPlans`), lo stesso che le
  ha lette all'invio. Il doppio del tracker nei test salvava `{"revision":1}`: ora salva un payload come quello vero.
- **Gli aeroporti con la loro posizione** (`ReviewDto.airports`: partenza, arrivo, deviazione, e gli aeroporti dei voli) per la mappa,
  da `IAirportDirectory`. Un aeroporto senza posizione non ha la sua linea.
- **Il suggerimento** si chiede al server a ogni spunta (`…/suggestion?errorIds=`); il browser non sa quando una decisione va
  «contro»: il campo del perché c'è sempre, con l'aiuto «obbligatorio quando l'esito va contro il suggerimento», e il rifiuto del
  server arriva sotto il campo. La regola resta scritta una volta.
- **La coda** è la lista generica con tre filtri nella barra: tour (se chi guarda legge i tour: un validatore con il solo grant su un
  tour non ha `Tours.View`, e allora il filtro non c'è), «in coda e in validazione» / «decisi», ordine. Le colonne del design meno
  due: **«contestato»** arriva con T14 (oggi è sempre falso) e **«suggerimento dei controlli»** con T17. Le righe portano le persone
  già scritte («Nome (VID)»): una cella disegna un valore, non un oggetto.
- **Prendere** si fa dalla pagina, non dalla coda: la riga non ha la `rowVersion`, e un validatore prende ciò che ha aperto.
- **La storia** traduce le note che il server scrive (chiavi `flightops:…`) e lascia le parole di chi riapre.
- **Meteo e controlli** hanno le loro sezioni e dicono «non ancora disponibile» (T16, T17).

## 5. Trovato scrivendo

- ⚠️ **Un validatore aggiunto con un grant ma senza posizioni staff non entra in `/staff`** (`_staff` chiede `isStaff`, e la voce
  di menu è fra quelle dello staff): la coda e la pagina non gli sono raggiungibili, anche se l'API gli risponde. Oggi i validatori
  hanno una posizione FOD; il caso nasce con «aggiungi validatore» di **T15**, che dovrà decidere dove lo manda.
- **A 400 px** la sidebar dello staff non si chiude e la colonna principale si allarga con qualsiasi tabella: è lo stesso su
  `/staff/tours`, il difetto del nucleo già segnalato in T11b, non di queste pagine.
- **Il banco accumula errori generali** di altri giri: nella tabella degli errori del giro compaiono otto «Bench disconnection …»
  delle regole generali lasciate da altre spec (vedi la memoria sul banco che accumula). Non rompe niente; da ripulire nella spec
  che li crea.

## 6. Che cosa si è toccato

- Nucleo: `RouteMap` (`tracks`) e `trackPath`; `preferenceQuery`/`useSavePreference`; la galleria (una traccia); il banco
  (`E2EOptions.Pilot`, `?as=pilot`), `web/scripts/e2e-server.mjs` (pilota e SMTP), Mailpit in `build-test.yml`.
- Modulo: `ReviewDto.Airports`, `ReviewPlanDto`; `ReviewQueueProvider` e il suo descrittore; la voce di menu «Validazione»
  (`Tours.Validate`); `screens/review.tsx`, `screens/reviewing.ts`, `blocks/reviewQueue.tsx`; le parole `review.*`.
- Test: integrazione `ThePageCarriesPlansAndAirportsAndTheBlockCountsOnlyTheReadersTours`; Vitest `reviewing.test.ts` e
  `greatCircle.test.ts`; smoke `e2e/tours-review.spec.ts`; giro `e2e/full/tours-review.spec.ts` (con `releasedTourWithOneLeg` e
  `removeBenchTours` portati in `bench.ts` da `tours-report.spec.ts`).
- Piano 0.95; design M2 §4.3, §8.2, §8.6; piano di implementazione, parte C, T13b.
