# Lo storico delle modifiche di un training (A13b)

**Data:** 30 settembre 2026 — fase A13b di M3, PR #197, dopo #196 (A13a)
**Stato:** **decisa** (Carmine, 30 settembre 2026, in chat alla sessione master, e pubblicata su sua istruzione [sulla #197][a197]):
alla domanda di §5 ([il commento che la pone][q197]) **la (a)**, come raccomandato, con una condizione del revisore accettata insieme
alla risposta (§6). La parte è scritta su questo branch con i suoi test (§7).

[q197]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/197#issuecomment-5909936323
[a197]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/197#issuecomment-5910098296
**Regola applicata:** `CLAUDE.md` §5, caso **(c)**: è una funzione nuova del modulo, anche se non scrive niente di nuovo. Il meccanismo
che tiene le modifiche c'è già (§2); nuova è la pagina che le legge.

## 1. Che cosa serve

`dalberone` (TD) ha provato il modulo sul banco il 30 settembre 2026, e ha chiesto questo: «i cambi di training vanno lasciati in log,
quindi se viene cambiato deve essere registrato».

Oggi chi segue un training dallo staff lo vede solo com'è **adesso**. La pagina (`/staff/training/{id}`, design §4.2) mostra il
trainer attuale, la data attuale e chi ha fatto l'ultima assegnazione (`AssignedBy`, `AssignedAt`). Non dice che cosa c'era prima, chi
l'ha cambiato né quando. Ci sono tre casi che si perdono:

- un trainer cambiato;
- una data fissata a mano e poi spostata;
- una sessione rischedulata e datata di nuovo.

Sul banco il trainer del #29, cambiato da HQ, non lascia nessuna traccia sulla pagina.

## 2. Che cosa c'è già

**Il nucleo scrive già ogni modifica.** `Training` è `[Audited]` (A4). A ogni scrittura l'interceptor aggiunge una riga in
`hub_audit_log`, nella stessa transazione: chi, quando, e i campi cambiati, prima e dopo. Anche il cambio del #29 c'è:
`TrainerVid` da 999004 a 999012, scritto da 999011.

Due cose finiscono nel registro anche se non cambiano un campo:

- **le date proposte**: `TrainingDates` le scrive toccando il training, per la sua versione. Nel registro sono una riga che cambia solo
  `UpdatedAt`;
- **le sessioni finite** (eseguite, rischedulate, no-show): sono già in `trn_sessions`, e la pagina le mostra.

Oggi il registro lo legge solo la schermata dell'audit, con `Audit.View`: è una lista grezza, senza i campi cambiati, per gli
amministratori.

## 3. La proposta (raccomandata): lo storico letto dal registro di audit

Nella pagina dello staff di un training c'è una sezione **«Storico»**. Contiene le righe del registro di quel training
(`trn_trainings` e il suo id), dalla più vecchia. Ognuna è detta a parole, con chi e quando:

- richiesta;
- accettata, oppure rifiutata con il motivo, o dall'hub per la teoria;
- assegnata a X, oppure **trainer cambiato da X a Y**;
- date proposte;
- data scelta dal trainee, fissata a mano, oppure **cambiata da … a …**;
- sessione rischedulata, oppure no-show;
- report pubblicato;
- chiusa, dallo staff con il motivo oppure dall'hub;
- annullata dal trainee.

Le regole:

- **Non si scrive niente di nuovo.** Nessuna tabella, nessuna migrazione, nessuna scrittura in più. Lo storico c'è anche per i training
  di prima: sul banco il cambio del #29 si vedrebbe subito.
- **Si leggono solo i campi che dicono qualcosa.** È una lista chiusa del modulo, letta con `nameof` dalle proprietà di `Training`:
  stato, trainer, data, scelta del trainee, postazione e motivi. Il resto di una riga non si mostra: i commenti del report restano nel
  report.
- **La legge chi legge la pagina**, con la stessa regola, anche per un capo FIR sul suo FIR. Il trainee no: la sua pagina dice già dove
  è arrivato il training, e lo storico nomina lo staff.
- **Una persona cancellata** la gestisce già il nucleo (`AuditRedaction`): nel registro ha lo pseudonimo, e la pagina la chiama
  «persona cancellata» con `personName`. Di un training svuotato dalla cancellazione lo storico dice che cosa è successo e quando,
  senza il contenuto.
- **Che cosa tocca**: il servizio della pagina dello staff (`StaffTrainings`), il suo DTO, una sezione della pagina e le parole.
  **Nessun file del nucleo.** Il modulo legge `hub_audit_log` per `HubDbContext`, come legge già il calendario e gli utenti.

## 4. Le alternative

- **(b) Una tabella del modulo con gli eventi** (`trn_training_events`), scritta a ogni passo. Sarebbe più semplice da leggere. Però
  scrive una seconda volta ciò che l'audit scrive già (`CLAUDE.md` §2), non vale per i training di prima, e porta una colonna di
  persona in più (la cancellazione, `ErasureTests`). **Sconsigliata.**
- **(c) Un lettore dello storico nel nucleo** (`AuditTrail`), per ogni modulo. È più pulito se lo storico lo vorranno anche i tour o
  gli eventi, ma è una fase del nucleo da fare prima di questa. Si può fare quando lo chiederà un secondo modulo, e questo codice
  passerà lì.
- **(d) Solo un link dalla pagina alla schermata dell'audit**, filtrata sul training. Non serve codice, ma la vede solo chi ha
  `Audit.View`, e non dice che cosa è cambiato.

## 5. La domanda e la risposta

**Carmine ha preso la raccomandazione** (30 settembre 2026, [il suo commento sulla #197][a197], scritto dalla sessione master su sua
istruzione).

| # | Domanda | Risposta | Scartate |
|---|---|---|---|
| 1 | Lo storico di un training va nella pagina dello staff, **letto dal registro di audit del nucleo** come in §3? | **(a)**: la sezione «Storico» nella pagina dello staff, letta dal registro di audit del nucleo — richiesta, accettata o rifiutata, assegnata, trainer cambiato da X a Y, date proposte e scelte, fissate a mano o spostate, rischedulata, no-show, report, chiusa, annullata —, ognuna con chi e quando. Niente di nuovo scritto: nessuna tabella, nessuna migrazione, nessun file del nucleo. La legge chi legge la pagina, con la stessa regola (un capo FIR sul suo FIR); il trainee no. Una persona cancellata come la lascia `AuditRedaction` («persona cancellata») | (b) la tabella degli eventi; (c) il lettore nel nucleo, ora; (d) il link alla schermata dell'audit |

## 6. La condizione del revisore, accettata con la risposta

Il training è **il primo modulo che legge le righe d'audit del nucleo**, e dipende dalla loro forma. Per questo:

1. **la lettura sta in un posto solo del modulo**: `src/IvaoHub.Modules.Training/Staff/TrainingHistory.cs`. Solo lì il modulo sa com'è
   fatta una riga del registro (§7). La pagina riceve righe già dette, con le persone per VID;
2. **un test cade se la forma cambia**: `TrainingStaffTests.History.cs` (integrazione) fa ogni passo per le sue API, passando per
   l'interceptor vero, e poi legge lo storico riga per riga. La prova che cade davvero è in §7;
3. **quando un secondo modulo vorrà uno storico** (M4 potrebbe), **la lettura passa nel nucleo** — la (c) di §4 — invece di essere
   copiata. Sarà una fase del nucleo, con la sua nota: `TrainingHistory` le dà la forma da cui partire, e il training la userà come gli
   altri moduli.

## 7. Com'è scritto

- **La lettura**, `TrainingHistory` (`Staff/TrainingHistory.cs`). Legge da `HubDbContext.AuditLog` le righe di `trn_trainings` (il nome
  della tabella lo dà il modello, come all'interceptor) con l'id del training come testo, in ordine di scrittura (`Id`). Sa questo della
  forma di una riga:
  - `Vid` è chi ha scritto; 0 è l'hub stesso (un job, come la chiusura della notte), che la pagina chiama «l'hub»;
  - `created` ha tutta la riga in `AfterJson`: è la richiesta. Se la riga nasce `Rejected` per `TheoryNotPassed`, c'è anche il rifiuto
    dell'hub, nello stesso momento;
  - `updated` ha solo le proprietà cambiate, prima e dopo, con i nomi C#: gli enum per nome, gli istanti in ISO UTC. Il passo lo dice lo
    stato (`State`); a stato fermo, il trainer (`TrainerVid`), poi la data (`ScheduledStartUtc`), poi il solo timbro (`UpdatedAt`,
    `UpdatedBy`), che sono le date proposte;
  - `erased` non ha niente: la cancellazione di un trainee (A12b) svuota anche le righe prima (`AuditRedaction`).

  Le proprietà si leggono con `nameof` di `Training`: stato, rifiuto, trainer, data, scelta del trainee e i due motivi. **La postazione
  no**: nessun passo la cambia dopo la richiesta, e la pagina la dice già. Quello per cui non ha parole — una riga svuotata, un'azione
  che non conosce, un valore che non sa leggere — resta una riga, «modificato», con chi e quando: lo storico non salta nessuna riga del
  registro.
- **Le date proposte**: proporre e ritirare scrivono la stessa riga (cambia solo il timbro; le date proposte sono righe che il registro
  non tiene). Lo storico dice quindi «**ha cambiato le date proposte**», che è vero per tutte e due; le date proposte di adesso, con chi
  e quando, la pagina le mostra già.
- **La pagina**: `StaffTrainingDto.History`, riempita da `StaffTrainings.PageAsync` con le persone nominate come nel resto della pagina
  (`TrainingPeople`, uno pseudonimo senza nome); la sezione «Lo storico» in fondo alla pagina, riga per riga: quando, in UTC e sotto
  nell'ora della divisione; che cosa, con chi, le date in UTC; il motivo di un rifiuto o di una chiusura su una riga sua
  (`screens/history.ts`, `historySays`). Anche la pagina del percorso di un trainee, che riusa `PageAsync`, porta lo storico di ogni
  training, e non lo disegna.
- **Il trainee no**, anche quando legge la pagina dello staff del suo training (un trainer che è anche trainee): lo storico è uno dei
  campi che `ReservedFields` toglie (nota `le-note-riservate-e-il-trainee`, che lo prevede: «una fase che aggiunge un campo riservato lo
  aggiunge qui»), e la frase delle note riservate lo dice. Il DTO del trainee (`TraineeTrainingDto`) non cambia.
- **I test**:
  - `TrainingStaffTests.History.cs` (integrazione), due test. Il primo fa per le API tutta la strada di un training: richiesta,
    accettazione, assegnazione, date proposte, trainer cambiato, date proposte e una ritirata, data scelta, data spostata a mano, la
    sessione cominciata (spostata dall'installazione, come nei test delle sessioni), rischedulata, datata di nuovo, chiusa con il motivo.
    Poi legge le tredici righe con chi, i trainer, le date e il motivo. Il secondo fa le altre fini: il rifiuto dell'hub per la teoria,
    quello dello staff con il motivo, l'annullamento, il no-show, il report e la chiusura della notte, di nessuno;
  - `TrainingHistoryRulesTests` (unità): le righe difficili da avere passando per l'interceptor, scritte a mano nella forma vista sul
    banco — svuotate dalla cancellazione, un'azione sconosciuta, uno stato per numero, un testo che non è un oggetto;
  - i test di cancellazione di A12b (`TrainingTraineeTests.Erasure.cs`) leggono lo storico dopo la cancellazione vera del nucleo: quello
    del training di un trainee cancellato è svuotato — la richiesta, poi solo «modificato», e per ultima la cancellazione, del superadmin
    —, quello di un training condotto da uno staff cancellato ha i suoi passi con lo pseudonimo;
  - i due test della nota delle note riservate (`TrainingSessionsTests`, e `TrainingTraineeTests` per il percorso) hanno lo storico nella
    loro lista; `history.test.ts` (Vitest) legge le frasi dal file delle parole; `training-staff.spec.ts` (e2e) disegna la sezione; il giro
    completo la legge sul server vero.
- **La prova che il test cade**: con il serializzatore dell'interceptor cambiato solo in locale, e poi rimesso com'era, i due test
  d'integrazione cadono tutti e due, una volta con le chiavi in camelCase e una volta con gli enum come numeri.

## Da portare nel piano

- Design M3 §4.2 (`/staff/training/{id}`): la sezione «Storico», letta dal registro di audit del nucleo; il trainee non la legge, anche
  dalla pagina dello staff (con le note riservate, §12 n.13).
- Piano §9.2, riga Training: lo storico delle modifiche di un training, dal registro di audit del nucleo.
- Piano §9.7 (contratti nucleo↔moduli): il training legge la forma delle righe d'audit del nucleo in un posto solo
  (`TrainingHistory`), e un secondo modulo che vuole uno storico porta quella lettura nel nucleo (la (c) di §4).
