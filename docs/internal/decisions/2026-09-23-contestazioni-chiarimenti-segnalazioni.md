# Contestazioni, chiarimenti, segnalazioni (T14b)

**Data:** 23 settembre 2026 — fase T14b di M2
**Stato:** **decisa** (Carmine, 23 settembre 2026, quattro risposte in apertura, tutte come proposte; il resto è design già scritto o
scelta tecnica dichiarata qui sotto)
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: il **che cosa** è nel design (`05-design-m2.md` §3.8, §3.10, §3.11, §8.2) e nella
nota `2026-09-15-contatti-con-risposte`; il **come** del nucleo è di T14a (`2026-09-23-i-fili-dei-contatti`). Qui le quattro scelte che
il design lasciava aperte e come il modulo usa i meccanismi di T14a. **Nessun meccanismo nuovo del nucleo.**

## 1. Che cosa serve

Piano di implementazione, parte C, T14 punti 2–5 (T14b): la contestazione che sblocca, il chiarimento dalle pagine dei tour,
`fo_leg_issues` con `flightops.legIssueReported`, il blocco `flightops.openIssues`, il giro e2e «contestato e riaperto». Il «fatta
quando» di T14 è di T14b: *un pilota contesta, il validatore risponde dal back office, il pilota riceve la mail e risponde da
`/me/contacts`.*

## 2. Le quattro risposte di Carmine

| Domanda | Risposta |
|---|---|
| Chi accoglie o respinge una contestazione? | **Solo chi ha `Tours.ReopenDecisions`** sul tour (FOC, FOAC, HQ, superadmin) **e non ha deciso quel PIREP**. Il validatore che ha deciso è partecipante del filo e risponde, ma non giudica la contestazione della propria decisione. Il pilota mai (`DeniedToStakeholder`). Scartato: lo stesso perimetro di «riapri» (§4.2.1), che lascia al validatore giudicare sé stesso. |
| Come sa il pilota l'esito? | **Una risposta nel filo.** Accogliere o respingere chiede un testo per il pilota, che diventa una risposta del dipartimento nel filo: parte `contact.threadReplied` (senza il nome di chi ha deciso, §3.5), e il perché resta nella conversazione che una contestazione o un ban citeranno. **Nessun tipo di notifica nuovo.** Scartati: `flightops.disputeDecided` (una seconda mail con lo stesso contenuto) e nessun avviso (il pilota scoprirebbe dalla mappa che la leg blocca di nuovo). |
| Quante contestazioni per PIREP? | **Una.** È la chiave «una volta sola» della proiezione (`flightops` + `pirep:{id}` + `dispute`). Se è accolta e il PIREP viene rifiutato di nuovo, il pilota scrive nello stesso filo o chiede un chiarimento. Scartato: una per decisione (`pirep:{id}:{n}`), più fili per lo stesso volo. |
| Dove si chiede un chiarimento dalle pagine del tour? | **Una pagina propria**, `/tours/{slug}/ask?pirep=|leg=|rule=`, con il form generato: il riferimento cliccato già scelto, gli altri aggiungibili (PIREP decisi del pilota, leg, regole del tour). Come il form del PIREP in T11b. Scartato: un dialog nella pagina del tour, stretto per una scelta multipla. |

## 3. Com'è fatto nel codice

### 3.1 La contestazione

- **`fo_pireps`** guadagna `dispute_status` (`Open`, `Upheld`, `Dismissed`; null finché nessuno contesta), `dispute_text`,
  `disputed_at`, `dispute_decided_at`, `dispute_decided_by_vid`. **`is_disputed` resta** (migrazioni solo additive) ma diventa la
  lettura di `dispute_status = Open`, scritta dal getter come `visibility`: la coda la filtra in SQL.
- **Aprirla** (`POST /api/flightops/reports/{id}/dispute`, il pilota): un suo PIREP `Rejected`, mai contestato, entro
  `disputeWindowDays` dalla decisione; il testo è obbligatorio. La storia guadagna un passo `Rejected → Rejected` con la nota
  `flightops:events.disputed`.
- **Il filo** si apre con la proiezione, nella transazione del PIREP: `Pirep` diventa **`IProjectable`** (`flightops`, `pirep:{id}`) e
  proietta una `ThreadOpeningProjection(dispute, dipartimento del PIREP, …, mittente: il pilota, partecipanti: [chi ha deciso],
  riferimenti: [il PIREP])`. Oggetto ed etichetta portano il titolo del tour, che il PIREP non ha: li compone il servizio della
  contestazione e li consegna alla riga in una proprietà **non mappata** (`DisputeThread`), letta da `Project` solo nel salvataggio
  che apre il filo. Nei salvataggi dopo `Project` dà `null`, che non tocca niente: il filo non si riscrive mai (nota T14a §3.3). Dopo
  il salvataggio, `ContactThreads.NotifyOpenedAsync` per le mail (al dipartimento `contact.received`, al validatore `threadOpened`).
- **Aperta, la leg non blocca più** (`TourRules`, già scritto in T11a sulla bandiera). **Respinta**, blocca di nuovo con la
  tolleranza contata da `dispute_decided_at` (design §15.2 punto 9): un volo decollato prima di allora più `rejectGraceHours` resta
  valido. **Accolta**, il PIREP torna **`Queued`** — in coda a chiunque, come dopo una correzione (§3.1) — con `queued_at` di adesso;
  gli errori della decisione vecchia restano finché la prossima non li sostituisce, e nel frattempo non contano (contano solo
  `Accepted` e `Rejected`).
- **Deciderla** (`POST /api/flightops/review/{id}/dispute`, `{ upheld, answer, rowVersion }`): `Tours.ReopenDecisions` sul PIREP e
  non chi l'ha deciso. Prima del salvataggio si verifica che chi decide legga il filo (`Contacts.View` sul dipartimento, come ogni
  FOC e FOAC): altrimenti la risposta non potrebbe partire, e la decisione si rifiuta invece di restare muta. Dopo il salvataggio la
  risposta entra nel filo con `ContactThreads.ReplyAsync` — due salvataggi in due contesti, come le mail dopo una decisione: se il
  secondo fallisce la decisione resta e la risposta si riscrive a mano nel filo.
- **Con una contestazione aperta «riapri» (§4.2.1) non c'è**, per nessuno: la decisione si riapre accogliendo la contestazione.
  Trovato scrivendo il giro e2e: chi aveva deciso poteva riaprire da sé, decidere di nuovo, e la contestazione restava `Open` per
  sempre — e il validatore avrebbe giudicato la contestazione della propria decisione per un'altra strada.
- **I contatori** del profilo nella pagina di validazione sono tre — aperte, accolte, respinte — sul tour, come le leg (§4.3); il
  pilota non li vede mai. La coda ha la colonna «contestato» e il filtro `filter[disputed]=true`.
- **Il pilota vede** sul suo PIREP lo stato della contestazione, fino a quando può contestare (`disputableUntil`, calcolato dal
  server) e il link al filo (`threadId`, letto dai messaggi di cui è mittente).

### 3.2 Il chiarimento

- **Dal nucleo**, com'era deciso: `POST /api/contacts` con `kind: "clarification"` e i riferimenti, verso il dipartimento del tour
  (`PublicTourDto.department`, nuovo).
- **`FlightOpsReferences`**, il risolutore dei tour, riconosce tre forme:
  - `pirep:{id}` — un PIREP non ritirato; lo vede il suo pilota (link `/tours/{slug}`) o chi può validarlo (link
    `/staff/tours/review/{id}`); il partecipante è **chi l'ha deciso**, se c'è (design §3.10);
  - `leg:{id}` — una leg non tolta di un tour che il pubblico vede;
  - `rule:{tourId}:{ruleId}` — una regola in vigore su quel tour (le regole effettive di T9): una regola generale vale su più tour e
    senza il tour il link non saprebbe dove portare.
  L'etichetta è `titolo del tour · leg 3 LIRF → LIML (20/09/2026)` in ogni lingua della divisione, presa all'apertura.
- ⚠️ **«Su un PIREP deciso»** lo fa la pagina (il bottone c'è solo sui decisi); il risolutore non sa se sta aprendo o leggendo un filo e
  un PIREP in coda lo riconosce. Un chiarimento su un PIREP in coda è innocuo, e un parametro «perché chiedi» nell'interfaccia del
  nucleo per questo solo caso sarebbe codice speculativo.

### 3.3 La segnalazione su una leg

- **`fo_leg_issues`**: `tour_id`, `leg_id`, `body`, `status` (`Open`, `Resolved`), `staff_note`, dipartimento del tour (`ITourChild`),
  audit; il pilota è `created_by` (`ISubmittedByMembers`). Il design la vuole una tabella del modulo, non un filo: non si riapre.
- **Il pilota** la scrive dalla riga della leg nella pagina del tour, in un dialog (un solo campo): `POST
  /api/flightops/tours/{tourId}/legs/{legId}/issues`. Intento **`flightops.legIssueReported`** alla **casella** del dipartimento del
  tour (design §9: solo la casella).
- **Lo staff** la legge e la chiude da `/staff/tours/issues` (lista e form generati, `MapCrud`; `Tours.View` legge, `Tours.Edit`
  chiude: design §7.1).

### 3.4 Il blocco `flightops.openIssues`

Sempre vivo, senza proprietà, per la dashboard: tre righe con il numero e il link — **segnalazioni aperte** (`fo_leg_issues` `Open`
sui tour che chi guarda vede nel back office), **contestazioni senza risposta** e **chiarimenti senza risposta** (fili `dispute` e
`clarification` che citano un oggetto dei tour, `New` o `Read`, fra quelli che chi guarda legge: il filtro del nucleo lo fa da sé).
Il link delle contestazioni è la coda con `filter[disputed]=true`, quello dei chiarimenti la coda dei contatti del dipartimento.

### 3.5 Il banco ha una terza persona

Il giro «contestato e riaperto» non si poteva provare con due persone: il WM del banco rifiuta il PIREP, e chi ha deciso non giudica
la contestazione. Il login del banco accetta **`?as=assistant`** (`E2E:Assistant`, VID 999003, posizione `IT-FOAC`, senza indirizzo),
gemello di `?as=pilot` di T13b: si tocca solo l'infrastruttura del banco (`E2ESignIn`, `e2e-server.mjs`). Scelta tecnica, non chiesta:
costa poco e si toglie in un minuto.

## 4. Alternative scartate

| Alternativa | Perché no |
|---|---|
| La contestazione aperta da un endpoint che scrive PIREP e filo in due salvataggi | la finestra fra i due che la nota del 15 settembre ha chiuso con la proiezione |
| Oggetto ed etichetta del filo senza il titolo del tour (solo la rotta), per una proiezione che legge solo la riga | nella coda del FOD `LIRF → LIML` non dice di quale tour; la proprietà non mappata è letta una volta sola |
| Un secondo tipo di proiezione o un campo `Label` nel PIREP | una colonna che serve a un salvataggio solo |
| La segnalazione su una leg come filo (`kind = legIssue`) | il design l'ha decisa come tabella del modulo con la casella del FOD; si riaprirebbe una scelta chiusa |
| Un permesso nuovo per decidere la contestazione | `Tours.ReopenDecisions` è già il permesso di chi rimette in discussione una decisione altrui |

## 5. Che cosa si tocca

- **Codice**: `Pirep` (+ `DisputeStatus`, colonne, `IProjectable`), `TourRules` (la tolleranza dalla contestazione respinta),
  `Threads/` (`PirepDisputes`, `FlightOpsReferences`, `LegIssue`, `OpenIssuesProvider`, gli endpoint), la coda e la pagina di validazione,
  `PirepDto`, `PublicTourDto.department`, migrazione `AddDisputesAndLegIssues`, `flightops.legIssueReported`, il blocco nelle due metà.
- **Web**: la contestazione e i link al filo in `MyReports`, la pagina `/tours/{slug}/ask`, il dialog della segnalazione, la sezione
  della contestazione nella pagina di validazione, `/staff/tours/issues`, `blocks/openIssues.tsx`.
- **Test**: unit `TourRules` (aperta sblocca, respinta riblocca dalla sua data); integrazione `PirepTests.Disputes.cs` (fuori finestra,
  non rifiutato, seconda volta; il filo una volta sola con il validatore; chi decide; accolta in coda, respinta che blocca; la
  risposta nel filo e la mail; il chiarimento con il validatore partecipante e il riferimento altrui rifiutato; la segnalazione e la
  sua mail; il blocco); giro `full/tours-dispute.spec.ts`.
- **Piano** 0.97; **design** §3.8, §3.10, §3.11, §12; **piano di implementazione** T14b.
