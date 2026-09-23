# Le pagine delle persone (T15b)

**Data:** 24 settembre 2026 — fase T15b di M2
**Stato:** **decisa** (Carmine, 24 settembre 2026, due risposte in apertura, tutte e due come proposte; il resto è design già scritto
o scelta tecnica dichiarata qui sotto)
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: il **che cosa** è nel design (`05-design-m2.md` §8.1, §8.2, §8.7) e nella nota
di T15a (`2026-09-23-completamento-validatori-piloti-ban`, §3.4 per `myTours`). Qui le due scelte che il design lasciava aperte e
come le pagine leggono il server di T15a. **Nessuna estensione del nucleo.**

## 1. Che cosa serve

Piano di implementazione, parte C, T15b: `/staff/tours/validators` (statistiche, aggiungi e togli), `/staff/tours/pilots/{vid}` (con
«banna»), `/staff/tours/bans` (lista e form generati), le voci di menu, il blocco `flightops.myTours` nelle due metà, l'avanzamento del
pilota sui riquadri di `/tours`, e il giro e2e «completato → award assegnato», che è il «fatta quando» di T15.

## 2. Le due risposte di Carmine

| Domanda | Risposta |
|---|---|
| Da dove si arriva alla pagina del pilota? Il design dà la pagina e non l'ingresso. | **Una voce «Piloti»** (`Tours.ViewPilots`) che apre `/staff/tours/pilots` con un campo VID, **più i link** dalla sezione del pilota nella pagina di validazione e dalla lista dei ban. Nessun codice nuovo sul server. Scartati: una lista generata di tutti gli iscritti (un endpoint nuovo sulle iscrizioni per una ricerca che il VID fa già); soli link (chi ha un VID in mano da una mail o da Discord non saprebbe dove scriverlo). |
| Le statistiche dei validatori: il design dice «per ogni tour dell'anno corrente e del precedente», l'API dà un anno per volta. | **Un selettore d'anno**, l'anno corrente per primo e il precedente a un clic (ma anche gli altri): la tabella per validatore e quelle per tour seguono l'anno scelto. Una chiamata per pagina. Scartato: i due anni sempre insieme (due chiamate e una pagina lunga il doppio per l'anno che si guarda una volta). |

## 3. Com'è fatto

### 3.1 `myTours` e i riquadri leggono una risposta sola

- **`MyTours`** (`People/`): una risposta a «i tour di questo pilota» — le iscrizioni ai tour che il pilota vede (un tour nascosto sparisce,
  §1.2.2; un sottotour sparisce con il suo contenitore), ognuna con la misura di `PilotProgress` e la prossima leg, prima quelle da finire;
  i PIREP `ToModify`; i fili dei tour (contestazioni e chiarimenti) con stato `Answered`; il riepilogo — leg accettate, **minuti volati**
  dal decollo all'atterraggio registrati dal tracker sui PIREP accettati, tour di primo livello completati. Nessun contatore degli errori.
- **Due lettori**: il blocco `flightops.myTours` (`MyToursProvider`, sempre vivo, senza proprietà) e **`GET /api/flightops/my-tours`**
  (chi ha fatto login), che i riquadri leggono. **Il riquadro resta uguale per tutti** (nota di T10): è il componente dei riquadri che,
  se chi guarda ha fatto login, aggiunge sopra la barra e la prossima leg dei tour iniziati. Così il pubblico, il blocco `tourCards` e
  `/tours` restano una risposta sola e cacheabile, e l'avanzamento arriva da una chiamata del pilota — anche nel blocco `tourCards` su
  `/me`, dallo stesso componente.
- I fili del pilota sono quelli che il filtro del nucleo gli lascia leggere (il mittente legge i suoi), come in `PirepDisputes`.

### 3.2 Le pagine dello staff

- **`/staff/tours/validators`**: selettore d'anno, la tabella per validatore (accettati, rifiutati, da modificare, i tour abilitati o
  «tutti», sospeso) con «togli» per ogni abilitazione, le tabelle per tour; «aggiungi validatore» è un form generato (VID, tour di primo
  livello o «tutti i tour») con i rifiuti del server sul campo. Scrive chi ha `Tours.ManageValidators`; legge chi ha `Tours.ViewPilots`.
- **`/staff/tours/pilots`** (il campo VID) e **`/staff/tours/pilots/{vid}`** (selettore d'anno come sopra, profilo di `PilotPageDto`), con
  «banna» che apre il form del ban con il VID già scritto e torna alla pagina.
- **`/staff/tours/bans`**: lista e form generati sul `MapCrud` di T15a; nessun «elimina» (un ban si toglie spostandone la fine).
- **Menu**: «Validatori», «Piloti», «Ban», tutte con `Tours.ViewPilots`.

### 3.3 Due aggiunte al server di T15a, trovate scrivendo le pagine

- **I titoli dei tour abilitati** viaggiano con le statistiche (`ValidatorsDto.titles`): la pagina li leggeva dalla lista dei tour, che
  chiede `Tours.View`, e un validatore abilitato con il solo grant ha `Tours.ViewPilots` e non `Tours.View`.
- **Il dipartimento di un filo** nella pagina del pilota (`PilotThreadDto.department`): i contatti sono una coda per dipartimento, e il
  link al filo lo deve nominare, come fa già la sezione della contestazione nella pagina di validazione.

## 4. Che cosa si tocca

- **Modulo, server**: `People/MyTours.cs` (servizio, endpoint, provider), le tre voci di menu, il descrittore del blocco, le due aggiunte
  di §3.3.
- **Modulo, pagine**: `screens/people.tsx`, `blocks/myTours.tsx`, l'avanzamento in `screens/TourCards.tsx` (con `ProgressLine`), il link dalla
  pagina di validazione.
- **Test**: integrazione in `PirepTests.People.cs`; i conteggi dei blocchi a 38 (galleria) e 13 (Data); il giro `full/tours-people.spec.ts`.
- **Piano** 0.99; **design** §8.1, §8.2, §8.7; **piano di implementazione** T15.
