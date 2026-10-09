# Le pagine delle prenotazioni: le letture del design (E6b)

**Data:** 9 ottobre 2026 — fase E6b di M4 (prenotare: le pagine), PR #240
**Stato:** **Proposta** — la domanda a Carmine è un commento sulla #240 (§5). La #240 è in coda dopo la #233 di E6a.
**Regola applicata:** `CLAUDE.md` §5, casi **(a)** e **(b)**: nessun meccanismo nuovo — la lista e il form generati (`MapCrud` in sola
lettura, `DataList`, `col.person`, `SchemaForm`), il `ConfirmDialog` del nucleo, i blocchi Data, il servizio delle notifiche, la
striscia del nucleo con gli scali (E4b), le impostazioni del modulo (`reminderLeadHours`), `[NotAudited]`. Sono letture del design M4
§3.3, §3.8, §7.1, §7.2, §7.3, §8.4, della nota `2026-09-29-gli-slot-e-le-prenotazioni` §2.9 e della nota
`2026-10-07-gli-slot-sulla-pagina-dell-evento` §6 («E6b ci mette Prenota»), più **tre richieste di `dalberone`** dopo una prova sul banco
(le letture 4, 5 e 6), scritte nel compito della fase, come chi tiene il modulo.

## 1. Che cosa serviva decidere

Il piano di E6b dice *che cosa* c'è — la lista degli slot con i filtri e «Prenota», `/events/mine`, il blocco `events.myEvents`, la
scheda «Prenotazioni» dello staff, il promemoria del giorno prima — e non *come* si comporta in una decina di punti: quando «Prenota» si
offre e a chi, che cosa vuol dire «la compagnia» di uno slot, che cosa sono «le prenotazioni vicine» del promemoria, che cosa fa il job
se gira tardi, due volte o in due processi, dove sta la striscia «il giorno dell'evento» e che cosa è «il giorno». Più le tre richieste
di `dalberone`: la conferma prima di «Pubblica», la riga che dice quando aprono le prenotazioni, la striscia di E4b sulla pagina.

## 2. Le letture

1. **«Prenota» nel dialog dello slot** (§3.3; nota del 7 ottobre §6). Si offre **solo quando il server lo accetterebbe**: a un membro
   che ha fatto il login, su uno slot libero, con le prenotazioni aperte (da `booking_opens_at_utc`, evento non annullato) e l'off block
   ancora da venire. Altrimenti il dialog dice perché no — lo slot è tuo; è chiuso (`events:errors.slotClosed`); le prenotazioni non
   sono ancora aperte; un visitatore accede, e torna alla pagina —, e **niente** su uno slot preso da un altro pilota: la pagina non dice
   mai da chi. **L'aereo** si sceglie fra i tipi ammessi, **il principale per primo e già scelto** (bottoni radio quando sono più d'uno).
   **«Prenota tutta la rotazione»** su una tratta, con lo stesso aereo (lettura 5 di E6a, decisa: un aereo per la rotazione): il dialog
   elenca le tratte prenotate e quelle no, ognuna con il perché del server. Dopo «Prenota» il dialog dice che lo slot è tuo, con il tuo
   aereo, e porta a `/events/mine`. Un rifiuto è detto con le parole del server (`describeProblem`), un 409 è «riprova».
2. **Lo slot «tuo»**. La pagina scrive **«Tuo»** sugli slot del lettore, letti **dalla sua lista** (`GET /api/events/mine/bookings`, di
   E6a), mai dalla pagina, che di uno slot continua a dire solo se è preso (piano §9.7). Un visitatore e un altro pilota leggono «Preso».
3. **I filtri** (§3.3): sulla pagina, nel browser — la pagina ha già tutti gli slot nella sua lettura (E5) — e **nell'indirizzo**, come i
   filtri di `/events`: partenze o arrivi; **le ore allo scalo dell'evento**, in UTC, un'ora alla volta, «dalle» e «fino alle» (uno slot
   alle 20:00 non sta «fino alle 20:00», uno alle 19:59 sì); il **tipo d'aereo** ammesso, il principale o un altro; **la compagnia**, cioè
   **le lettere con cui comincia il nominativo**, fino alla prima cifra (`XYZ` di `XYZ101`) — uno slot non ha una colonna per la compagnia,
   e il numero di volo è vuoto spesso quanto pieno —; **la rotazione**, cioè le sue tratte. Ogni filtro si offre solo quando restringe
   qualcosa (due versi, più di un'ora, di un tipo, di una compagnia; una rotazione). Niente rimasto: «Nessuno slot con questi filtri».
4. **Quando aprono le prenotazioni, e il conto alla rovescia** (`dalberone`, punto 7). Fra i fatti dell'evento una riga
   **«Prenotazioni»**, quando l'evento ha `booking_opens_at_utc`: prima, **«Aprono il»** con il momento in UTC e nell'ora della divisione,
   e il conto alla rovescia al secondo («Fra 2 g 03:15:42»; `role="timer"`, che un lettore di schermo non annuncia a ogni secondo); a zero
   la pagina rilegge l'evento e gli slot si offrono; dopo, **«Aperte dal»** lo stesso momento, e «ogni slot si prenota fino al suo off
   block»; un evento annullato, «Chiuse: l'evento è annullato». `PublicEventDto` prende `bookingOpensAtUtc`: un campo in più nella lettura
   della pagina, come diceva l'handoff di E5.
5. **Chi è online sugli scali, il giorno dell'evento** (`dalberone`, punto 8; E4b, nota `2026-10-06-chi-e-online-sugli-scali-di-un-evento`).
   **«Il giorno»** sono i giorni dell'evento **nell'ora della divisione**, da quello in cui comincia a quello in cui finisce, da mezzanotte
   a mezzanotte (un evento che finisce a mezzanotte finisce il giorno prima). Non per un evento di tutta la divisione (la striscia della
   divisione è già in cima al sito), né per uno annullato. **Dentro la pagina**, sotto il titolo, in un riquadro: non nello spazio del
   banner del layout, dove `docs/UI-GUIDELINES.md` vuole una striscia — quello spazio tiene la striscia della divisione, e metterci quella
   dell'evento chiede che il layout sappia gli scali dell'evento: una modifica del nucleo, una fase sua (l'handoff di E4b). La domanda 1(b).
6. **La conferma prima di «Pubblica»** (`dalberone`, punto 6): il `ConfirmDialog` del nucleo, **blu** — non si perde niente —, «Pubblicare
   l'evento?», «Comparirà sul sito, nel calendario e nella ricerca, e non tornerà in bozza: da quel momento si potrà solo annullare.»
   Annullato il dialog, niente è pubblicato.
7. **`/events/mine`** (§7.1), a un membro che ha fatto il login (il layout `_member` manda un visitatore al login e indietro): **«Da
   volare»** — l'on block ancora da venire, quindi anche un volo in aria — per off block, e **«Passate»**, le più recenti prima; sotto i
   loro eventi (con la pagina e lo stato); ognuna con nominativo, numero di volo, l'aereo scelto, partenza e arrivo con gli orari in UTC, lo
   stand, la rotazione e la tratta; **«Ritira»** finché si può (fino all'off block, `withdrawable` di E6a), chiesto una volta di più
   (`ConfirmDialog` rosso: lo slot torna a tutti). Disponibilità, turni, registro e PIREP di supporto arrivano con M4b. **L'indirizzo
   `mine` è riservato**: nessun evento lo prende (`events:errors.slugReserved`), perché sarebbe una pagina che nessuno raggiunge.
8. **Il blocco `events.myEvents`** (§7.3): le prenotazioni **ancora da volare** (la regola di `/events/mine`), sotto i loro eventi, con
   l'aereo e l'off block; la stessa risposta di `/events/mine` (`PilotBookings.MineAsync`); a un visitatore `signedIn: false` («Accedi per
   vedere le tue prenotazioni»); nessuna, «Nessuna prenotazione. Vai agli eventi»; il link a tutte. I turni con E12. Va su `/me` dal back
   office, come `flightops.myTours`.
9. **La scheda «Prenotazioni»** dello staff (§7.2): **una risorsa del motore CRUD in sola lettura**, `/api/events/bookings`,
   `EventBookings.View` sulla cura dell'evento, `filter[eventId]`, `?q=` un VID; **per l'orario del volo allo scalo dell'evento** (l'ordine
   dell'esportazione: l'off block di una partenza, l'on block di un arrivo) — il motore tiene l'ordine della sua sorgente quando non ha un
   ordine predefinito, e l'orario è una colonna dello slot —, ordinabile per data di prenotazione. Colonne: il pilota (`col.person` del
   nucleo: una persona cancellata è detta con la parola del nucleo), nominativo, l'aereo scelto, da, off block, per, on block, rotazione,
   prenotato il, promemoria inviato. **«Togli»**: il `ConfirmDialog` del nucleo con il campo del motivo (obbligatorio; la conferma resta
   spenta finché è vuoto), il verbo di E6a. I nomi dei piloti li legge dai membri del nucleo un lettore piccolo del modulo
   (`EventsPeople`), come fanno training e tour: ⚠️ **è il quarto lettore di nomi per VID dell'hub** (tour, training, contatti del nucleo,
   eventi) — la domanda 2. **Nessun endpoint scritto a mano in più**: la lista è `MapCrud`, il blocco un fornitore di blocchi Data.
10. **Il promemoria del giorno prima** (§3.8, §8.4; nota del 29 settembre §2.9):
    - il job **`events-reminders`**, ogni quarto d'ora ai minuti 10, 25, 40 e 55, **in UTC**: il trigger dice il suo fuso, come la nota
      della fase del nucleo E10j (#239, il recupero dei giri) vuole che un modulo dica; con il suo guardiano un giro recuperato o ripetuto
      non cambia niente, perché il job decide dai suoi dati (sotto);
    - una prenotazione è **dovuta** quando l'off block del suo slot è dopo adesso e non oltre `reminderLeadHours`, l'evento non è annullato
      e non è stata ancora ricordata; **«le prenotazioni vicine»**: la mail prende ogni prenotazione dello stesso pilota nello stesso evento,
      non ancora ricordata, il cui off block cade **entro `reminderLeadHours` dopo la prima dovuta** — le tratte di una sera sono una mail,
      non una ciascuna mentre entrano nella finestra a un quarto d'ora l'una dall'altra; un volo un giorno dopo ha la sua (la domanda 1(a));
    - la mail: per ogni volo `- nominativo (numero), aereo: partenza orario → arrivo orario, stand X`, e sotto ogni rotta del FOD per quella
      coppia di aeroporti, con le sue note nella lingua del pilota; il modello dice che gli orari sono in UTC, la nota del nominativo
      occupato, la pagina dell'evento e `/events/mine`; il tipo `events.bookingReminder` si spegne dal profilo come ogni altro;
    - **una volta**: `reminded_at` si scrive prima della mail (come il promemoria del training: una mail che non entra in coda è un
      promemoria perso, mai uno mandato due volte), **`[NotAudited]`** (la contabilità del job, nessuna riga d'audit); **due giri insieme**
      (due processi di Passenger) si mettono in fila su un `SELECT … FOR UPDATE` delle prenotazioni del pilota non ancora ricordate, in una
      transazione `READ COMMITTED` come i verbi di E6a: il secondo le legge ricordate e non manda niente;
    - **decide dai suoi dati**, mai dall'ora: un giro in ritardo ricorda quello che è ancora dovuto (l'off block da venire); un volo partito
      mentre l'hub dormiva non si ricorda più; un giro ripetuto non trova niente;
    - una prenotazione fatta dentro la finestra si ricorda al giro dopo; uno slot spostato dallo staff dopo il suo promemoria non si ricorda
      di nuovo: lo ha già detto `bookingChanged` di E6a; gli slot privati aspettano E7 (il job legge l'off block di uno slot pubblico);
    - ⚠️ con il log binario in formato `STATEMENT` anche il segno del job sarebbe rifiutato: lo stesso punto aperto di E6a (la risposta 2 di
      Carmine sulla #233), che si chiude con la prima prenotazione sull'installazione di prova.

## 3. Le scelte di Claude, e come sono provate

- **I test d'integrazione** (`EventsBookingPagesTests`, VID 761063–761066): il promemoria una volta per pilota, le tratte vicine in una
  mail con le rotte, nessuna riga d'audit, un secondo giro senza niente; un evento annullato e un volo partito non ricordati; **due giri
  insieme**: una transazione del test tiene la prenotazione segnata e non confermata, il giro aspetta nel database (`INNODB_TRX`), il test
  conferma, il giro non manda niente; il blocco a un visitatore, a un pilota e a un membro senza prenotazioni; la lista dello staff per
  orario, con i nomi e uno pseudonimo, 403 a un membro, 401 a un visitatore; l'indirizzo `mine` rifiutato. **Al contrario**: senza il
  `FOR UPDATE` il giro manda la mail una seconda volta; senza `[NotAudited]` compare la riga d'audit; senza le «vicine» la seconda tratta
  manca dalla mail.
- **Unità** (`EventsRemindersTests`): la finestra di una mail ai suoi bordi.
- **Vitest**: i filtri e le loro scelte, il conto alla rovescia e il giorno dell'evento, le prenotazioni divise e raggruppate, «Prenota»
  contro un'API finta, il blocco.
- **Smoke** (`events-public.spec.ts`, +6): un visitatore legge «preso» e nessun nome; un membro prenota con l'aereo che sceglie; la riga
  dell'apertura e il conto alla rovescia; la striscia nel giorno dell'evento con gli scali chiesti; i filtri nell'indirizzo; `/events/mine`
  che ritira.
- **Il giro completo** (`full/events-bookings.spec.ts`): il pilota prenota una rotazione intera, la ritrova in `/events/mine`, ne ritira
  una tratta; lo staff toglie l'altra con un motivo, che il pilota legge in Mailpit. `full/events-staff.spec.ts` conferma «Pubblica».

## 4. Che cosa si è toccato

Solo il modulo, **nessuna migrazione** (le colonne ci sono da E6a): il server — `Bookings/StaffBookings.cs`, `Bookings/MyEventsProvider.cs`,
`Bookings/BookingRemindersJob.cs`, `EventsPeople.cs`, `Bookings/BookingEndpoints.cs` (la risorsa), `EventsMail.cs`
(`BookingReminderAsync`), `EventsNotifications.cs`, `EventBooking.cs` (`[NotAudited]`), `EventsModule.cs`, `Public/PublicEvents.cs`
(`BookingOpensAtUtc`), `Staff/EventDtos.cs` (l'indirizzo `mine`) —; il browser — `screens/EventSlots.tsx`, `SlotBooking.tsx`,
`BookingOpening.tsx`, `bookingTimes.ts`, `slotList.ts`, `myBookings.ts`, `mine.tsx`, `bookings.tsx`, `events.tsx`, `public.tsx`,
`blocks/myEvents.tsx`, `schemas.ts`, `api.ts`, `index.ts` e le parole —; i test. **Del nucleo** solo i due conteggi dei blocchi, che un
blocco nuovo alza di uno (`DataBlockEndToEndTests` 18 → 19, `uiKit.test.ts` 43 → 44: Carmine sulla #125, `CONTRIBUTING.md`).

## 5. Le domande a Carmine

Poste in inglese sulla #240; in italiano dicono:

> 1. Le dieci letture del §2, come scritte? In particolare: (a) «le prenotazioni vicine» del promemoria sono quelle dello stesso pilota
>    nello stesso evento il cui off block cade entro `reminderLeadHours` dopo la prima dovuta; (b) la striscia di chi è online sugli scali
>    sta **dentro** la pagina dell'evento, il suo giorno nell'ora della divisione — nello spazio del banner servirebbe una fase del nucleo;
>    (c) la compagnia di uno slot sono le lettere con cui comincia il nominativo.
> 2. Gli eventi leggono i nomi dei piloti con un lettore piccolo loro, come tour, training e contatti del nucleo fanno ciascuno per sé:
>    è il quarto. Raccomandato: va bene così adesso, e un lettore dei nomi nel nucleo — che i quattro userebbero — è una fase del nucleo
>    a parte, quando la vuoi. Oppure la vuoi prima di unire E6b?

## Da portare nel piano

- Design M4 §3.3: «Prenota» nel dialog dello slot, offerto solo quando il server lo accetterebbe, l'aereo principale già scelto; la
  rotazione intera con lo stesso aereo, e il perché di ogni tratta non presa; «tuo» dalla lista del pilota; i filtri nell'indirizzo, la
  compagnia dal nominativo.
- Design M4 §3.8 e §8.4: le prenotazioni vicine; il segno prima della mail, `[NotAudited]`; due giri insieme sotto `FOR UPDATE`; in
  ritardo o due volte; i minuti 10, 25, 40, 55 in UTC; i privati con E7.
- Design M4 §7.1: la riga «Prenotazioni» con l'apertura e il conto alla rovescia; la striscia degli scali dentro la pagina, il giorno
  dell'evento nell'ora della divisione; `/events/mine` com'è; l'indirizzo `mine` riservato.
- Design M4 §7.2: la scheda «Prenotazioni» (la lista in sola lettura del motore, per orario del volo; «togli» con il motivo nel dialog);
  la conferma prima di «Pubblica».
- Design M4 §7.3: `events.myEvents` sono le prenotazioni ancora da volare.
- Piano §16.6: E6b non aggiunge endpoint scritti a mano; la lista dello staff è una risorsa `MapCrud`.
- `docs/UI-GUIDELINES.md`, se Carmine tiene la lettura 5: la striscia di uno spazio che una schermata chiede (gli scali di un evento) può
  stare nella pagina, sotto il suo titolo; quella della divisione resta nel banner.
- La domanda 2, se Carmine vuole il lettore dei nomi nel nucleo.
