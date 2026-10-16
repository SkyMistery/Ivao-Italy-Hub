# Le attività parallele, i posti e la conservazione breve (E17)

**Data:** 15 ottobre 2026 — fase E17 di M4 (la seconda e ultima di M4c: le attività, i posti, la conservazione breve, «Duplica»)
**Stato:** **decisa** (Carmine, 16 ottobre 2026, in chat alla sessione master e pubblicata su sua istruzione: [la risposta sull'issue #267][a]):
**(A)** alla domanda 1, **(A)** alla 2, **(B)** alla 3 — una piccola fase del nucleo prima, **E10s**, ed E17 in coda dopo di lei (§6). Il resto
sono letture del design.
**Regola applicata:** `CLAUDE.md` §5, caso **(a)** per le letture del design M4 §4-bis, §2.3-bis, §8.4, §11 e delle note
`2026-09-29-gli-eventi-in-presenza` e `2026-10-12-le-domande-e-l-iscrizione` — l'unico handler, il guardiano dell'interceptor con il ritiro
di chi ha mandato la riga (`[WithdrawnByStakeholder]`, E10h), la cura dell'evento sulle sue righe (`IEventChild`), il motore CRUD, `Refusals`,
`IPersonalDataEraser`, il lettore dei nomi del nucleo (`MemberNames`), le convenzioni dei job di E10j —; la domanda 1 perché due righe del
design unito leggono l'area in due modi; la domanda 3 perché la risposta (B) è un caso **(b)**, un'estensione del nucleo in una PR a sé.

## 1. Le attività (`evt_activities`, design §4-bis.1)

1. **Una risorsa del motore CRUD**, `/api/events/activities`, filtrata per evento, con lista e form generati nella scheda «Attività» di un
   evento in presenza. Una riga figlia (`IEventChild`, `[Audited]`, lo scope dell'evento), **nell'area che dice la domanda 1**.
2. **Le colonne**: il nome tradotto (in ogni lingua della divisione, al più 200 caratteri), il luogo tradotto e facoltativo (come il luogo
   dell'evento, 200 caratteri), la finestra (`from_utc`, `to_utc`), la durata del turno (`slot_minutes`, da 5 a 480) e i posti per turno
   (`seats_per_slot`, da 1 a 100). Al più **20 attività** per evento.
3. **La finestra sta dentro l'evento** (dal suo inizio alla sua fine), e **i turni sono quelli che ci stanno interi**: da `from_utc`, uno
   ogni `slot_minutes`, finché il turno finisce entro `to_utc` (10:00–17:00 a 45 minuti sono nove turni, l'ultimo alle 16:00). Una finestra
   che non ha nemmeno un turno è rifiutata; al più **200 turni** per attività.
4. **Solo su un evento in presenza** (`events:errors.activityNotInPerson`); l'interruttore «in presenza» non si spegne con attività, come
   con domande o iscrizioni (la stessa chiave, `events:errors.inPersonHasQuestions`, con le parole allargate).
5. **Un'attività con posti presi si cambia solo così che ogni posto preso resti su un suo turno** e che nessun turno abbia più posti presi
   di quanti ne offre (`events:errors.activitySeatsTaken`, sul campo che lo rompe). Se si elimina: la domanda 2.
6. **Un evento eliminato** porta via le sue attività (nessun posto, perché un posto chiede un'iscrizione e un evento con iscrizioni non si
   elimina).

## 2. I posti (`evt_activity_bookings`, design §4-bis.1, §4-bis.2)

7. **Le colonne**: l'attività, l'evento, il membro (`vid`), l'inizio del turno (`starts_at_utc`), `created_at`. Nessuna colonna di parole:
   niente da segnare con `[ErasedWith]`.
8. **La riga del membro**: `ISubmittedByMembers`, `IHasStakeholder` (il membro), `IVisible` ai soli membri, area `EventBookings` con la cura e
   lo scope dell'evento, `[Audited]`, **`[WithdrawnByStakeholder]`**: **un posto non porta decisioni dello staff** — niente da approvare,
   rifiutare o confermare, lo staff lo legge e lo conta —, come l'iscrizione (la regola di Carmine sulla #232, risposta 3). Ridarlo cancella
   la riga.
9. **Prendere un posto** (`POST /api/events/mine/seats`, l'attività e l'inizio del turno): solo chi ha un'iscrizione all'evento; un turno
   dell'attività; fino all'inizio dell'evento, come cambiare le risposte (`RegistrationRules.IsOpen`); un posto libero; **nessun turno
   sovrapposto per lo stesso membro** fra tutte le attività dell'evento (due turni che si toccano non si sovrappongono). **Ridarlo**
   (`DELETE /api/events/mine/seats/{id}`) fino all'inizio, anche di un evento annullato, come ritirarsi.
10. **Il blocco**: in una transazione `READ COMMITTED`, prima **la riga dell'iscrizione del membro** `FOR UPDATE` — serializza due richieste
    dello stesso membro, così il controllo delle sovrapposizioni vede i posti appena presi, e il ritiro dell'iscrizione nello stesso istante
    —, poi **la riga dell'attività** `FOR UPDATE`, sotto cui si contano i posti presi del turno. Due membri sull'ultimo posto nello stesso
    istante: uno vince, l'altro legge «turno pieno» (`events:errors.seatFull`). Sempre in quest'ordine, così nessun ciclo.
11. **Il limite, detto**: lo staff che cambia un'attività controlla i posti presi senza il blocco (il motore CRUD non apre una transazione
    attorno a `BeforeSave`); un posto preso nell'istante fra il controllo e il salvataggio può restare su un turno che non c'è più o oltre i
    posti. Come per le domande di E16 (lettura 4), ogni lettura lo regge: il posto resta del membro, si conta, e la pagina lo mostra con
    il suo orario.
12. **Il ritiro dell'iscrizione, «Togli» dello staff e la cancellazione** portano via i posti del membro nello stesso salvataggio.
13. **La mail**: `registrationReceived` parte anche a ogni posto preso o ridato, con le risposte e **i turni** come sono in quel momento (il
    design: «con il riepilogo delle risposte e dei turni»; la lettura 12 di E16, «una mail a ogni cambio», decisa da Carmine).
14. **Le pagine**: la pagina dell'evento mostra a chiunque le attività con i turni e **quanti posti liberi**, mai chi (§7.1, come gli slot);
    il membro iscritto prende e ridà i posti dalla sezione «Iscrizione», e li ritrova in `/events/mine` sotto la sua iscrizione
    (`MyRegistrationDto.Seats`). Nessuna lettura nuova: i turni viaggiano nella lettura della pagina, i posti del membro nelle sue
    iscrizioni.
15. **Lo staff**: la scheda «Attività» (lista e form generati; nella lista i posti presi, in numeri) e la scheda «Posti» (una risorsa del
    motore in sola lettura, `/api/events/seats`, `EventBookings.View`: il membro con `col.person`, l'attività, il turno). **Nessun «togli»
    di un posto solo**: il design dà allo staff «togliere un'iscrizione», che porta via i suoi posti. **Le liste mandano dati** — gli orari,
    i numeri, il nome tradotto intero — **e la pagina scrive le parole nella lingua sullo schermo** (il no di Carmine sulla #255, risposta 6).
16. **I nomi** per VID dal lettore del nucleo, `MemberNames.ReadAsync` (E10m), mai da `EventsPeople.NamesAsync`.

## 3. «Duplica» (design §2.3-bis)

17. **Copia le domande** — il tipo, le parole, le scelte **con le loro chiavi com'erano**, obbligatoria, l'ordine — **e le attività**, la
    finestra spostata della stessa differenza. Mai le iscrizioni né i posti. Chi duplica un evento con attività ha anche il permesso della
    loro area (`EventDuplicatePlan.AreasWritten`), come per le rotte.

## 4. La conservazione breve, `events-retention` (design §8.4, §11)

18. **Nasce qui** (piano, «Parallelismo possibile»: M4c prima di M4b), **una volta al mese**: il primo giorno del mese alle 04:40, **in UTC**
    (il trigger lo dice, E10j), dopo il job dei tour. Con le convenzioni di E10j: `[DisallowConcurrentExecution]`, una riga in `hub_jobs_log`
    con `FinishedAt` su ogni uscita, mai un'eccezione, `RunAsync` per i test.
19. **Decide dalle date**: un evento la cui fine è passata da almeno `inPersonRetentionMonths` mesi (`EventState.IsOver` e la fine), che ha
    ancora iscrizioni o posti. Per ognuno, **in un salvataggio solo**: scrive in `evt_event_stats` (la crea se manca, tocca solo le sue
    colonne) gli iscritti, **le somme delle risposte** (`RegistrationSums.Of`, i numeri di `QuestionSum`) e **i posti presi per attività**, e
    `computed_at`; poi cancella posti e iscrizioni, attraverso l'interceptor come ogni scrittura (come la conservazione dei tour). Un giro
    perso o doppio non fa danni: il secondo non trova niente.
20. **Le somme restano visibili**: per un evento oltre la sua conservazione, la lista delle domande e quella delle attività leggono i numeri
    da `evt_event_stats` invece che dalle righe che non ci sono più. (La scheda «Statistiche» è di E13a.)
21. Cambiare `inPersonRetentionMonths` vale dal giro dopo, per ogni evento.

## 5. La cancellazione dei dati di una persona (design §11.1)

22. **Un posto** di un evento non finito si cancella; di un evento finito resta con lo pseudonimo, perché le statistiche lo contano. Due righe
    dell'anteprima: `events:erasure.seatsToCome`, `events:erasure.seatsOver`. `ErasureTests` con `evt_activities.created_by`, `.updated_by`,
    `evt_activity_bookings.vid`.

## 6. Le domande a Carmine

Poste in inglese con [l'issue #267][q] (15 ottobre 2026, per scelta di dalberone: chiedere subito); in italiano dicono quello che segue.
L'issue dice anche che, se Carmine sceglie la (B) alla domanda 3, la fase del nucleo la prende dalberone, ed E17 si mette in coda dopo di
lei oltre che dopo E12.

[q]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/267
[a]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/267#issuecomment-6215521232

1. **Chi scrive le attività.** Il design le legge in due modi: §4-bis.1 («righe figlie dell'evento, area `EventBookings`»), il catalogo §6.1
   (`EventBookings.Edit`: «slot, import, generatore, attività»; `EventBookings.View`: «iscrizioni e attività con i VID») e la nota
   `gli-eventi-in-presenza` §2.4 dicono **`EventBookings`**; §4-bis.2 e la stessa nota §2.5 dicono «`Events.Edit` per domande e attività».
   **(A, raccomandata)** area `EventBookings`: le scrive chi ha `EventBookings.Edit`, le legge chi ha `EventBookings.View` — tre posti contro
   due, e il catalogo è la tabella che decide i permessi; chi prepara le domande (`Events.Edit`) e chi gestisce i posti sono gli stessi
   nella divisione di oggi (EC, EAC, EA). **(B)** area `Events`, come le domande: `Events.Edit` scrive, `Events.View` legge.
2. **Un'attività con posti presi.** **(A, raccomandata)** non si elimina (`events:errors.activityHasSeats`), come un evento con iscrizioni; si
   cambia solo nei limiti della lettura 5. **(B)** si elimina con i suoi posti, e ogni membro che ne aveva uno riceve di nuovo
   `registrationReceived` con i turni come sono ora.
3. **La conservazione e l'audit.** Cancellare un'iscrizione attraverso l'interceptor lascia in `hub_audit_log` la sua copia con le risposte,
   per sempre (E10n le svuota solo alla cancellazione dei dati di quella persona). **(A)** come i tour: si accetta, e le risposte restano
   nell'audit. **(B, raccomandata)** una piccola fase del nucleo prima, con la sua nota: un modulo che toglie righe per conservazione chiede
   al nucleo di svuotare nell'audit **le colonne segnate `[ErasedWith]`** delle righe che toglie (un passo accanto a `EmptyGoneAsync`); E17
   la usa e si mette in coda dopo. Le risposte si tengono poco perché dicono chi viene di persona e dove (design §4-bis.2): tenerle
   nell'audit svuota la regola.

### Le risposte di Carmine (16 ottobre 2026)

Date in chat alla sessione master e pubblicate su sua istruzione [sull'issue #267][a]:

1. **Chi scrive le attività: (A)**, area `EventBookings`: le scrive chi ha `EventBookings.Edit`, le legge chi ha `EventBookings.View`, come
   dice il catalogo §6.1. È quello che il codice fa (lettura 1).
2. **Un'attività con posti presi: (A)**. Non si elimina (`events:errors.activityHasSeats`), e si cambia solo così che ogni posto preso resti
   su un suo turno e nessun turno abbia più posti presi di quanti ne offre (lettura 5). È quello che il codice fa.
3. **La conservazione e l'audit: (B)**. Una piccola fase del nucleo prima, di dalberone, con la sua nota, **E10s**
   (`m4/e10s-retention-in-the-audit`): un modulo che cancella righe per conservazione chiede al nucleo di svuotare nell'audit le colonne
   segnate `[ErasedWith]` delle righe che cancella, accanto ad `AuditRedaction.EmptyGoneAsync`. **E17 la usa ed è in coda dopo di lei**:
   `events-retention` la chiama per le iscrizioni che toglie, le cui risposte (`AnswersJson`) portano già `[ErasedWith(nameof(Vid))]`.
   Il posto non ha colonne di parole (lettura 7): nell'audit resta che un membro ha preso un turno, come di un'iscrizione resta che si è
   iscritto.

## 7. Alternative scartate

| Alternativa | Perché no |
|---|---|
| Un indice univoco per il posto | un turno ha più posti: serve il conteggio sotto un blocco (nota `gli-eventi-in-presenza`) |
| Bloccare solo l'attività | due richieste dello stesso membro su due attività diverse passerebbero insieme il controllo delle sovrapposizioni |
| I turni come righe (`evt_activity_slots`) | sono un calcolo della finestra e della durata; righe in più da tenere allineate a ogni cambio |
| Un «togli» dello staff per un posto solo | il design non lo chiede; un verbo in più |
| La conservazione senza interceptor (`ExecuteDelete`) | nessuna scrittura gira attorno all'interceptor (la conservazione dei tour lo dice) |

## Da portare nel piano

- **Design M4 §4-bis.1**: le colonne e i limiti delle attività e dei posti; i turni che ci stanno interi; i due blocchi in quell'ordine;
  perché il posto porta `[WithdrawnByStakeholder]`; l'area delle attività (domanda 1).
- **Design M4 §4-bis.2**: fino a quando si prende e si ridà un posto; i posti che se ne vanno con l'iscrizione; la mail a ogni cambio dei
  posti; la scheda «Posti» senza «togli»; un'attività con posti (domanda 2).
- **Design M4 §2.3-bis**: «Duplica» copia domande (con le chiavi) e attività.
- **Design M4 §8.4, §11**: `events-retention` il primo del mese alle 04:40 UTC, le somme scritte prima di cancellare, le liste che le
  rileggono dalle statistiche; l'audit (domanda 3).
- **Design M4 §11.1**: i posti nella cancellazione.
- **Piano §16.6**: due verbi del membro in più (prendere e ridare un posto).
