# Una data passata non è una scelta mancata: la chiusura della notte e i limiti delle date

**Data:** 1 novembre 2026 — issue #327 (revisione del codice, lotto 7, Training lato server)
**Stato:** **decisa** (Carmine, 21 ottobre 2026, [risposta sull'issue #327][r1]: «Tutte come suggerisci tu», sulla
raccomandazione del master per il punto 2). Il §2 e il §3 sono la direzione che il master ha scritto nei punti 3 e 1 della stessa
issue, coperta dalla stessa risposta; il valore del limite (§2, 366 giorni) e il secondo promemoria (§3), scelte di questa PR, li
ha confermati Carmine il 1° novembre 2026 ([risposta sulla PR #431][r2]: «Tutte come consigli»), con il punto 4 spostato
nell'issue #434.
**Regola applicata:** `CLAUDE.md` §5, caso **(a)**: la regola del job notturno e i rifiuti di un form dentro il modulo, con i
meccanismi che ci sono (`Refusals`, le chiavi i18n del modulo). Design `07-design-m3.md` §2.4, §2.5, §5.3, §12 n.9.

[r1]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/327#issuecomment-6310356849
[r2]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/431#issuecomment-6555197779

## 1. Una data già passata non conta come scelta (punto 2)

- **Il caso.** La nota `2026-09-25-il-tempo-per-la-data-e-le-voci-della-scheda` §1 fa chiudere al job notturno i training «rimasti
  senza scelta» oltre `maxResponseDays`. Il codice (`TrainingDates.Unanswered`) chiedeva solo che una data fosse stata proposta e
  nessuna dopo il taglio: con `maxResponseDays = 7`, una sola data proposta per il giorno dopo, lasciata passare, chiudeva il training
  al settimo giorno con la mail `closedUnanswered`, che dà la colpa al trainee, anche se dal secondo giorno non c'era niente da
  scegliere.
- **La decisione.** Una data già passata **non** è una scelta che il trainee ha mancato. La notte chiude un training «senza
  risposta» solo se, al momento della chiusura, fra le date proposte prima del taglio ce n'è **almeno una ancora da venire** e il
  trainee non l'ha scelta. Se restano solo date passate il training **torna al trainer**, che ne propone di nuove: nessuna chiusura,
  nessuna mail `closedUnanswered`. La coda del trainer lo mostrava già fra «Date da proporre» (nessuna data ancora da venire).
- **Come si legge la nota del 25 settembre.** «Rimasti senza scelta» vuol dire: con una scelta possibile che il trainee non ha
  fatto. La nota resta com'è; questa ne fissa la lettura.
- **Dove.** `TrainingDates.Unanswered(trainings, slots, before, now)`, letta dalla chiusura della notte e dalla coda del trainer, che
  così chiedono la stessa cosa (la coda filtrava già le date da venire per conto suo: quel filtro ora è nella regola).

## 2. I limiti di una data (punto 3)

- **Il caso.** Alcuni corpi dello staff davano un 500 invece di un rifiuto: una riga `null` fra le date proposte o fra le voci della
  scheda, una data vicina al 9999-12-31 (c'era solo il limite in basso, e il giorno dopo l'ultimo del calendario non esiste), e la
  domanda «che cosa incontra questa data» per il primo istante dell'anno 1, la cui mezzanotte locale a est di UTC non esiste.
- **La decisione.** Una riga mandata come niente è **obbligatoria sulla sua riga** (`slots[i]`, `sheet[i]`, `errors.required`).
  Una data si propone o si fissa a mano **al massimo un anno avanti** (`TrainingDates.MaxLead`, 366 giorni: anche un anno
  bisestile), rifiutata sul suo campo con `training:errors.slotTooFar`. La domanda dei conflitti per una data che la scrittura
  rifiuterebbe per i suoi limiti — più di un anno da adesso in un verso o nell'altro, o più lunga di una sessione — risponde con la
  politica e **nessun avviso**: il rifiuto arriva sul campo quando la si scrive, e la pagina non cambia.
- **Il valore.** Un anno è molto più di quanto un training aspetta la sua data, e lontano dalla fine del calendario. È la scelta di
  questa PR dentro la direzione del master («bound the dates»), **confermata da Carmine** ([risposta sulla PR #431][r2]).

## 3. Un altro trainer per un training che ha la data (punto 1)

- **Il caso.** Cambiare il trainer di un training `Scheduled` teneva la data (design §2.4) ma non azzerava `RemindedAt`: il
  promemoria già partito era andato al trainer di prima, e il nuovo non ne riceveva. La mail diceva al trainee «il tuo trainer
  proporrà le date» per un training che la data l'ha già, e al nuovo trainer niente di quando sia.
- **La decisione.** Il promemoria **riparte**, a tutti e due, col nome del nuovo trainer: il trainee può riceverne un secondo, che
  gli dice anche chi lo allena adesso (**confermato da Carmine**, [risposta sulla PR #431][r2]). Le mail dell'assegnazione di un training datato dicono la data: al trainee che resta quella,
  al trainer quando è (`training:mail.training.assignedTraineeDated`, `assignedTrainerDated`).

## Che cosa si tocca

Solo il modulo Training: `Dates/TrainingDates.cs`, `Staff/TrainerQueue.cs`, `Staff/StaffTrainings.cs`, `Sheets/EvaluationSheet.cs`,
`Blocks/ApprovalQueueProvider.cs` (il punto 5: il blocco legge tutte le pagine dei training in attesa, non solo le prime 500), le
chiavi nei file di lingua del modulo. Nessuna migrazione, nessun file del nucleo. Il punto 4 (due ban dati insieme) resta fuori:
tocca i file dei ban, su cui lavora la PR dell'issue #312, e ha ora la sua issue, #434.

## Da portare nel piano

- Design `07-design-m3.md` §2.5 (e §12 n.9): la chiusura della notte chiede una data proposta **ancora da venire**; con sole date
  passate il training torna al trainer. Una data si propone o si fissa al massimo un anno avanti.
- Design `07-design-m3.md` §2.4: un cambio di trainer su un training datato fa ripartire il promemoria, e le due mail dicono la data.
