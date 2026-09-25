# La conservazione dei tour (T20a)

**Data:** 25 settembre 2026 — fase T20a di M2
**Stato:** **decisa** (Carmine, 25 settembre 2026, tre risposte in apertura; il resto è design già scritto o scelta tecnica dichiarata
qui sotto)
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: il **che cosa** è nel design (`05-design-m2.md` §10 e §10.1, strada B); qui come lo
fa il modulo. **Nessuna estensione del nucleo** in T20a. La cancellazione dei dati di un pilota (§10.0) **è un caso (c)** e ha la sua fase,
T20b, con la sua nota.

## 1. Che cosa serve

Piano di implementazione, parte C, T20 punto 1: un job mensile del modulo che, 13 o 25 mesi dopo la chiusura di un tour, cancella ciò che
pesa (tracce, revisioni dei piani, esiti dei controlli, snapshot, note, ATC ed esenzioni; del tour briefing, regole, hub, rotazioni,
vincoli, iscrizioni). **Tour e leg restano**, archiviati; il registro disciplinare non si tocca mai.

## 2. Le tre risposte di Carmine

In apertura di T20 il censimento dei dati personali ha mostrato che **il nucleo non ha nessun modo di cancellare o anonimizzare un utente**:
i dati di un pilota stanno anche nei fili delle contestazioni (`cms_contact_*`), nelle notifiche (`hub_notifications`, con l'indirizzo e la
nota al pilota), negli award e in `hub_audit_log`, che copia intere le righe auditate (anche `hub_users`) e non si svuota mai.

| Domanda | Risposta |
|---|---|
| Come si divide T20? | **In tre fasi.** **T20a** la conservazione (questa nota, solo il modulo); **T20b** la cancellazione dei dati di un pilota; **T20c** rifiniture, giro completo e rapporto di chiusura di M2. Scartate: due fasi, una fase sola. |
| La cancellazione dei dati di un pilota: quando? | **Adesso, nel nucleo.** T20b apre con una nota di caso (c) che propone un meccanismo del nucleo: un'interfaccia che ogni modulo implementa, un'azione del superadmin sull'utente, e il nucleo che ripulisce fili, notifiche, preferenze, token e audit. Il modulo dei tour è il primo a implementarla, e Training la trova già fatta. Scartate: solo il modulo (la richiesta sarebbe soddisfatta a metà), rimandarla fuori da M2. |
| Quale tour tiene 25 mesi? | **Quello che dura più di 12 mesi**: `close_at` oltre `release_at` + 12 mesi. Un tour da novembre a febbraio resta a 13. Scartata: la definizione di §1.2.1 («chiusura nell'anno dopo»), che avrebbe dato 25 mesi a un tour di tre mesi. |

## 3. Com'è fatto

- **`TourRetentionJob`** (`Tours/` del modulo), il primo giorno del mese alle 04:20 UTC, con una riga in `hub_jobs_log`. Come gli altri
  job: passa dall'interceptor (niente `ExecuteDelete`), non rilancia mai, e un fallimento è una riga `failed`.
- **Quando un tour scade:** `close_at` + `retentionMonths` (13), o + `retentionMonthsLong` (25) se `close_at > release_at + 12 mesi`. Si
  conta dalla chiusura, non dalla fine della finestra di riporto: il design dice «dalla chiusura», e a 13 mesi pochi giorni non cambiano
  niente. Ogni tour sulle sue date: un sottotour con le date del contenitore scade con lui, uno con date sue scade da solo.
- **Un tour che ha ancora un PIREP aperto aspetta** (scelta tecnica): in coda, in revisione, da modificare, o con una contestazione aperta.
  Ripulirlo toglierebbe al validatore regole e piani mentre decide. Il job lo salta e lo conta nella riga del log («N in attesa»); il
  mese dopo riprova.
- **Il segno:** una colonna nuova **`fo_tours.purged_at`** (additiva, migrazione `AddTourPurgedAt`). Un tour ripulito:
  - **esce dal pubblico** (§1.2.1, «finché la conservazione non lo toglie»): la visibilità diventa `Staff`, come per un tour nascosto, e
    `TourState.IsPublic` risponde no. Per questo sparisce anche dalla ricerca, dal calendario e dalle pagine del pilota, mentre lo staff lo
    trova ancora nel back office e nella pagina del pilota (§10.1);
  - **non si modifica più**: tour, leg, hub, rotazioni, vincoli, regole e stato rispondono `flightops:errors.tourPurged`. Senza questa
    guardia, spostare `close_at` nel futuro riaprirebbe un tour senza regole né hub;
  - **le sue decisioni non si riaprono** (`flightops:errors.reviewPurged`): una decisione si rifarebbe senza piani, controlli e regole
    del momento;
  - il back office lo dice con un avviso nella barra del tour («archiviato il …») al posto delle azioni.
- **Che cosa va via, di ogni PIREP del tour** (la riga resta: è il registro):
  - tracce (`fo_pirep_tracks`, di solito già tolte a 90 giorni) ed esiti dei controlli (`fo_check_results`);
  - le revisioni dei piani (`fo_pirep_flights.flight_plans_json` → `[]`, `plan_at_takeoff_revision` → null). **Il volo resta**
    (callsign, sessione, orari), perché dice che cosa è stato volato;
  - lo snapshot delle regole, **snellito e non svuotato** (scelta tecnica, trovata leggendo il codice): la pagina del pilota e la pagina
    di validazione leggono **il nome degli errori confermati dallo snapshot** (`Pilots.Errors`, `PirepReview.PageAsync`), quindi
    svuotarlo renderebbe muto il registro, che è proprio ciò che §10.1 vuole leggibile. Restano solo le regole che portano un errore
    confermato, con codice, titolo e quegli errori; **testo e parametri vanno via**, insieme a tutte le altre regole. Il registro si legge
    con le parole del giorno della decisione, non con quelle di oggi del catalogo. **Lo snapshot della leg resta**: dice **dove** (§10.1),
    pesa poche decine di byte, e la pagina del pilota ci legge il numero della leg;
  - ATC ed esenzioni (`atc_contacts_json`, `atc_exemptions_json` → `[]`);
  - **le note**, lette così: `pilot_remarks`, `diversion_note`, `note_to_pilot`, `staff_note`, `override_reason`, `dispute_text`.
    ⚠️ `note_to_pilot` è il perché di una decisione scritto al pilota: il design la mette fra le note; se Carmine la vuole nel registro, si
    toglie da questo elenco;
  - **gli errori suggeriti e non confermati** (`fo_pirep_errors` con `confirmed = 0`): sono esiti dei controlli.
- **Che cosa resta, di ogni PIREP:** stato, decisione (chi e quando), contestazione (stato e chi l'ha decisa), **gli errori confermati**
  (con il nome dal catalogo `fo_errors`, che non si cancella: si ritira), **la storia** (`fo_pirep_events`, note comprese) e i ban.
- **Che cosa va via, del tour:** il briefing (→ documento vuoto), le **regole del tour** (`fo_rules` con quel `tour_id`, e i loro legami
  con gli errori), gli **hub** (con le rotazioni; le leg perdono la rotazione), i **vincoli** di un tour `Open`, i **vincoli sul callsign**,
  le **iscrizioni**. **Le foto** non le tocca questo job: i loro usi scadono un mese dopo la chiusura (§1.14) e le cancella
  `MediaExpiryJob` del nucleo, se nessun altro le usa.
- **Non nell'elenco del design, e restano:** le segnalazioni sulle leg (`fo_leg_issues`) e i fili delle contestazioni nel nucleo. Sono
  dati di una persona e li guarda T20b.
- ⚠️ **Iscrizioni e award:** cancellare un'iscrizione completata toglie la sua segnalazione dell'award **se è ancora in attesa** (è la
  regola della proiezione del nucleo). Il job **salta** un tour che ha segnalazioni ancora in attesa, come per i PIREP aperti: tredici
  mesi senza assegnare un award sono una dimenticanza, e il job non deve renderla definitiva.

## 4. Trovato

- Le etichette e i commenti di `retentionMonths` e `retentionMonthsLong` dicevano «mesi di conservazione dei report» e «mesi del registro
  disciplinare». Il registro non si cancella mai, e i report restano; corretti: «mesi dopo la chiusura prima dell'archivio» e «lo stesso,
  per un tour che dura più di un anno».

## Da portare nel piano

- `06-piano-implementazione-m2.md` parte C: T20 divisa in **T20a / T20b / T20c** (tabella e sezione), T20a fatta.
- `05-design-m2.md` §10: la definizione di «tour su più di un anno» (durata oltre 12 mesi), `purged_at`, il tour che aspetta un PIREP aperto
  o un award in attesa, e che cosa resta del PIREP; §10.0 rimanda a T20b.
- `00-piano-di-progettazione.md`: versione 1.07 e riga di changelog.
