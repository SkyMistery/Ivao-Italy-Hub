# Lo storico delle modifiche di un training (A13b)

**Data:** 30 settembre 2026 — fase A13b di M3, in coda dopo A13a
**Stato:** **proposta**: la domanda di §5 va a Carmine sulla PR di A13b, e il codice aspetta la sua risposta.
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

## 5. La domanda per Carmine

Lo storico di un training va nella pagina dello staff, **letto dal registro di audit del nucleo** come in §3?

**Raccomandazione:** sì, la (a) di §3.

## Da portare nel piano

- Design M3 §4.2 (`/staff/training/{id}`): la sezione «Storico», letta dal registro di audit.
- Piano §9.2, riga Training: lo storico delle modifiche di un training, dal registro di audit del nucleo.
