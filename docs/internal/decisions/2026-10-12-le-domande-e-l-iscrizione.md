# Le domande e l'iscrizione a un evento in presenza: le letture del design (E16)

**Data:** 12 ottobre 2026 — fase E16 di M4 (la prima di M4c: le domande e l'iscrizione)
**Stato:** **decisa** (Carmine, 12 ottobre 2026, in chat alla sessione master e pubblicata su sua istruzione: [le risposte sulla
#253][a]): **(A) a tutte e due** le domande di [l'issue #253][q], aperta all'inizio della fase perché il codice ne dipende (§5). Le
letture di §1–§4 sono scritte come le raccomandava la nota.
**Regola applicata:** `CLAUDE.md` §5, caso **(a)** per le letture del design M4 §4-bis, §1.10, §8.3, §11.1 e delle note
`2026-09-29-gli-eventi-in-presenza` e `2026-09-29-i-dati-dei-membri-negli-eventi` §2.5 — l'unico handler, il guardiano dell'interceptor
con il ritiro di chi ha mandato la riga (`[WithdrawnByStakeholder]`, E10h), la cura dell'evento sulle sue righe (`IEventChild`), il
motore CRUD, `Refusals`, `IPersonalDataEraser`, il servizio delle notifiche —; caso **(b)** per la domanda 1, che tocca il generatore dei
form del nucleo (`SchemaForm`). Una migrazione del modulo; niente nel nucleo in questa PR.

[q]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/253
[a]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/253#issuecomment-6120877540

## 1. Le domande (`evt_questions`, design §4-bis.1)

1. **Una risorsa del motore CRUD**, `/api/events/questions`, filtrata per evento, letta con `Events.View` e scritta con `Events.Edit`
   sulla cura dell'evento, come gli scali (l'area è quella dell'evento, §6.1). Una riga figlia (`IEventChild`) con lo scope dell'evento:
   un grant su un evento solo ne scrive le domande. Nessun verbo scritto a mano.
2. **Le colonne**: l'ordine (da 0 a 999; la più bassa prima, poi come scritte), il tipo dall'elenco chiuso (`YesNo`, `Choice`,
   `MultiChoice`, `Quantity`, `Text`, scritto come testo), la domanda tradotta, le scelte tradotte (una colonna JSON), obbligatoria o no.
   **La domanda e le scelte si scrivono in ogni lingua della divisione** (le leggono i membri, come le note di una rotta); al più **300
   caratteri** per lingua. Al più **30 domande** per evento, **da 2 a 20 scelte** per una domanda a scelta, **nessuna** per gli altri tipi.
3. **Una scelta ha una chiave**, che il server le dà alla creazione (`"1"`, `"2"`, …, il numero dopo il più alto) e che non cambia più:
   le risposte nominano la chiave, così le scelte si riscrivono e si riordinano senza toccare una risposta. Il form la porta nascosta;
   una scelta senza chiave, o con una chiave che la domanda non ha mai dato, è nuova.
4. **Una domanda a cui qualcuno ha risposto tiene il suo tipo e ogni sua scelta** (si riscrivono, si riordinano, se ne aggiungono, non
   se ne tolgono): `events:errors.questionAnsweredType`, `events:errors.questionAnsweredChoices`. **Si elimina**: le sue risposte non si
   leggono e non si contano più, e spariscono alla prossima modifica del membro.
5. **Solo su un evento in presenza** (`events:errors.questionNotInPerson`); e **l'interruttore «in presenza» non si spegne** finché
   l'evento ha domande o iscrizioni (`events:errors.inPersonHasQuestions`, solo da acceso a spento, come i privati dopo la #246).
6. **Una domanda obbligatoria aggiunta dopo le prime iscrizioni** non tocca chi è già iscritto: la risposta gli serve alla sua prossima
   modifica.
7. **«Duplica» non copia ancora le domande**: lo fa E17 (piano, E17 punto 3), con le attività.

## 2. L'iscrizione (`evt_registrations`, design §4-bis.1, §4-bis.2)

8. **Le colonne**: l'evento, il membro (`vid`), le risposte (una colonna JSON per domanda, `q{id}`: `true`/`false`, la chiave di una
   scelta, le chiavi di più scelte nell'ordine della domanda, un intero, un testo), `created_at` e **`updated_at`** (quando il membro ha
   cambiato le risposte l'ultima volta: lo staff lo vede nella lista; una colonna in più del design, nessuna persona). Univoca su
   `(event_id, vid)`; **nessuna chiave verso l'evento**, come la prenotazione: un evento con iscrizioni non si elimina
   (`events:errors.eventHasRegistrations`), si annulla.
9. **La riga del membro**: `ISubmittedByMembers`, `IHasStakeholder` (il membro), `IVisible` ai soli membri, area `EventBookings` con la
   cura e lo scope dell'evento, `[Audited]`, **`[WithdrawnByStakeholder]`**: ritirarsi cancella la riga. **Perché la riga non porta
   decisioni** (la regola di Carmine sulla #232, risposta 3): un'iscrizione non ha nessuno stato che lo staff decida — niente da
   approvare, rifiutare o confermare —; lo staff la legge, la somma e la toglie. Cancellarla cancella solo quello che il membro ha mandato.
10. **Il server valida le risposte secondo il tipo** (`RegistrationAnswers`): un sì o no è un booleano (`events:errors.answerYesNo`); una
    scelta è una chiave della domanda e più scelte sono chiavi della domanda (`events:errors.answerChoice`); un numero è un intero da 0 a
    999 (`events:errors.answerQuantity`); un testo è una stringa, tolti gli spazi in testa e in coda, al più 1000 caratteri
    (`events:errors.answerText`, `errors.text.tooLong`). **Una scelta vuota, nessuna scelta spuntata, un numero vuoto, un testo vuoto
    sono «nessuna risposta»**; un'obbligatoria senza risposta è `errors.required`. Ogni rifiuto sta sul campo della risposta,
    `answers.q{id}`. Le risposte a domande che l'evento non ha più non si tengono.
11. **I verbi del flusso del membro** (piano §16.6), sotto `/api/events/mine/registrations`: la lettura delle proprie (`GET`), iscriversi
    o cambiare le risposte (`PUT /{eventId}`: 201 la prima volta, 200 dopo), ritirarsi (`DELETE /{eventId}`). **Fino all'inizio**: ci si
    iscrive e si cambiano le risposte a un evento in presenza visto, non annullato, prima del suo inizio; **ci si ritira fino all'inizio
    anche da un evento annullato**. Dopo: `events:errors.registrationClosed`. Due richieste dello stesso membro nello stesso istante si
    incontrano sull'indice univoco, e la seconda dice «riprova».
12. **La mail `registrationReceived`** parte a ogni iscrizione **e a ogni cambio delle risposte**, con il luogo e le risposte come sono in
    quel momento, nella lingua del membro: l'ultima mail dice le risposte vere. Ritirarsi non manda niente.
13. **Annullare e cambiare gli orari** avvisano anche chi è iscritto: una mail per persona, anche a chi ha prenotato e si è iscritto
    (`EventSaving.PeopleToTellAsync`).

## 3. Lo staff (design §4-bis.2, §7.2)

14. **Le schede** di un evento in presenza: **«Domande»** (`Events.View`, la scrive chi ha `Events.Edit`) e **«Iscrizioni»**
    (`EventBookings.View`; «Togli» a chi ha `EventBookings.Edit`).
15. **Le somme stanno nella lista delle domande**, una colonna per domanda: un sì o no conta i sì e i no, una scelta conta ogni scelta,
    un numero dà il totale, un testo dice quanti hanno risposto. Sono **statistiche dell'evento**, lette con l'evento (§6.1: `Events.View`
    «e le statistiche»), senza persone. Calcolate dal server a ogni lettura (`RegistrationSums`), dette con le parole della lingua di chi
    legge. **Nessun endpoint in più.**
16. **La lista delle iscrizioni** è una risorsa del motore in sola lettura (`/api/events/registrations`, come le prenotazioni di E6b): il
    membro detto da `col.person`, le risposte in parole (una riga per domanda), quando si è iscritto e quando le ha cambiate.
17. **«Togli» chiede un motivo, e il membro lo legge in una mail nuova, `events.registrationRemoved`** — **la domanda 2**, decisa (A) (§5). Il verbo
    `POST /api/events/registrations/{id}/remove` chiede `EventBookings.Edit` all'unico handler sull'iscrizione con la cura dell'evento
    com'è ora, come «togli» di una prenotazione; il motivo ha le regole di quello di una prenotazione (lo stesso `BookingRemovalRequest`).

## 4. Le statistiche, la conservazione, la cancellazione

18. **`evt_event_stats` nasce intera** (piano «Parallelismo possibile»: M4c prima di M4b), una riga per evento (indice univoco, cancellata
    con l'evento), **solo numeri**: slot pubblici e privati, prenotati, prenotati e volati, prenotati e non volati, volati senza prenotare,
    disponibilità ATC, turni, controllati, no-show, giustificati, PIREP mandati e accettati, segnali di award, iscritti, **le somme delle
    risposte** (JSON, per domanda: i numeri di `QuestionSum`), **i posti presi** (JSON, per attività), quando sono state calcolate; la cura
    e lo scope dell'evento, nell'area `Events`. **In E16 nessuno la scrive**: le somme dello staff si calcolano dalle iscrizioni;
    **E17** scrive quelle di un evento in presenza prima che la conservazione cancelli le iscrizioni, **E13a** il resto dopo l'evento.
19. **`inPersonRetentionMonths`** (predefinito 3, da 1 a 120) è nelle impostazioni; lo legge `events-retention` di E17.
20. **La cancellazione dei dati di una persona** (nota `2026-09-29-i-dati-dei-membri-negli-eventi` §2.5: «si trattano come le
    prenotazioni»): l'iscrizione a un evento **non finito** si cancella; quella a un evento **finito** resta con lo pseudonimo che il nucleo
    scrive in `vid`, **con le risposte che le somme contano** (sì o no, scelte, numeri) **e senza i testi**, che sono parole del membro
    (come una prenotazione tenuta perde la nota). Due righe dell'anteprima: `events:erasure.registrationsToCome` e `…registrationsOver`.
    **E10n** (#251, non ancora unita): un'iscrizione ritirata o tolta prima della cancellazione lascia le risposte nelle sue righe
    d'audit; con E10n unita, `AnswersJson` prende `[ErasedWith(nameof(Vid))]` (lo dice l'handoff).

## 5. Le domande a Carmine

Poste in inglese con [l'issue #253][q]; in italiano dicono:

> 1. **Le etichette del form del membro.** Il form si costruisce dalle domande, ma `SchemaForm` (nucleo) prende ogni etichetta solo da
>    i18n (`<labels>.fields.<path>`, e «nessuna» di una scelta facoltativa da `<labels>.options.<path>.none`), e qui l'etichetta è il
>    testo della domanda. (A, **raccomandata**) una fase del nucleo prima, E10o, con la sua nota: un campo porta il suo nome nello schema,
>    `.meta({ label })`, scritto da chi chiama già nella lingua a schermo come le etichette di `choices`, e «nessuna» con una parola del
>    nucleo; E16 si mette in coda dopo di lei. (B) nel modulo: la pagina carica i testi delle domande in i18n a runtime
>    (`i18n.addResourceBundle`) — nessun file del nucleo, ma usa i file di lingua come deposito di righe (`CLAUDE.md` §5 lo chiama
>    aggirare un meccanismo).
> 2. **«Togli» un'iscrizione**: (A, **raccomandata**) con un motivo e una mail nuova, `events.registrationRemoved`, come per una
>    prenotazione; (B) in silenzio: il membro la trova sparita in `/events/mine`.

La domanda 1 l'ha voluta dalberone prima di ogni codice del form (12 ottobre 2026, in chat: fra la fase del nucleo subito, la domanda a
Carmine prima e la strada nel modulo, **la domanda a Carmine prima**).

### Le risposte di Carmine (12 ottobre 2026)

Date in chat alla sessione master e pubblicate su sua istruzione [sulla #253][a]:

1. **Le etichette: (A), prima una fase del nucleo.** Una PR del nucleo a sé, **E10o** (`m4/e10o-field-names-in-schema`), con la sua nota:
   un campo può portare il suo nome nel suo schema, scritto da chi chiama già nella lingua a schermo, come le etichette di `choices`; un
   select facoltativo con un nome suo dice «nessuna» con una parola del nucleo. Il form del membro di E16 la usa, ed **E16 si mette in coda
   dopo la sua PR**; il resto di E16 non aspetta. **Non la (B)**: i file di lingua non sono un posto per tenere righe.
2. **«Togli» un'iscrizione: (A), come una prenotazione.** Chiede un motivo, e il membro lo legge in una mail nuova,
   `events.registrationRemoved`, che il profilo spegne come ogni mail del modulo. È quello che E16 ha scritto (§3, lettura 17).

## 6. Alternative scartate

| Alternativa | Perché no |
|---|---|
| Le risposte in una tabella per risposta (`evt_registration_answers`) | il design dice «`answers` (per domanda)» sulla riga; una riga per risposta non serve a nessuna lettura, e le somme si fanno sul JSON di una pagina |
| Le scelte come indici nella lista | riordinare o togliere una scelta cambierebbe il senso delle risposte già date |
| Le somme in un endpoint scritto a mano | sono un campo della lista delle domande, che il motore già serve |
| Le somme a chi ha `EventBookings.View` | sono numeri senza persone, statistiche dell'evento (§6.1) |
| Rifiutare l'eliminazione di una domanda con risposte | lo staff non potrebbe più togliere una domanda sbagliata dopo la prima iscrizione; le risposte smettono solo di contare |
| Cancellare sempre l'iscrizione di una persona cancellata | le somme di un evento finito cambierebbero prima che E17 le scriva nelle statistiche |
| Ritiro come uno stato della riga | contro «ritirarsi … fino all'inizio» del design e contro la regola di E10h: la riga non porta decisioni |

## 7. Che cosa si tocca

Solo il modulo, i suoi test e i suoi documenti: le entità `EventQuestion`, `EventRegistration`, `EventStats`, la migrazione
`AddInPersonRegistration`, `Staff/EventQuestionEndpoints.cs`, `Registrations/` (risposte, somme, verbi del membro, lista dello staff),
`EventsPersonalData`, `EventsMail`, `EventsNotifications`, `EventSaving`, `EventPublishing` (il luogo per pubblicare), `EventDtos` (in
presenza e il luogo nel form, in coda al DTO con un predefinito), `PublicEvents` (il luogo e le domande nella pagina), le impostazioni;
le pagine (`web/src/modules/events/`); le parole del modulo. **Due test condivisi**, nei casi in cui il test lo chiede (`10`, «Regole di
tutte le fasi»): `ErasureTests` (`evt_questions.created_by`, `.updated_by`, `evt_registrations.vid`). E i test del modulo di fasi prima,
dove una forma che cresce lo chiede: le due righe dell'anteprima in `EventsErasureTests` (E8b), `inPerson` e `venue` in `eventForm.test.ts`
(E3a: il test esiste per dire che il form rispecchia `EventWriteDto`), i campi nuovi nelle fixture di `PublicEventDto` e delle
impostazioni (`EventHead.test.tsx`, `public.test.tsx`, `schemas.test.ts`, lo smoke `events-public.spec.ts`).

## Da portare nel piano

- **Design M4 §4-bis.1**: le chiavi delle scelte; che cosa tiene una domanda a cui qualcuno ha risposto; l'eliminazione di una domanda; i
  limiti (30 domande, 2–20 scelte, 300 caratteri, numeri da 0 a 999, testi di 1000); `updated_at` sull'iscrizione; nessuna chiave verso
  l'evento; perché l'iscrizione porta `[WithdrawnByStakeholder]`.
- **Design M4 §4-bis.2**: fino a quando ci si iscrive, si cambia e ci si ritira (anche da un evento annullato); la mail a ogni cambio; le
  somme nella lista delle domande, con `Events.View`; «togli» con il motivo e la mail (la domanda 2).
- **Design M4 §1.10**: le colonne di `evt_event_stats` e chi la scrive (E17, E13a).
- **Design M4 §8.3**: `registrationRemoved` (la domanda 2).
- **Design M4 §11.1**: che cosa resta di un'iscrizione a un evento finito (lo pseudonimo, le risposte contate, senza i testi).
- **Design M4 §7.2**: le schede «Domande» e «Iscrizioni».
- **Piano §16.6**: il conto degli endpoint a mano di M4 — tre verbi del membro e «togli» di E16.
- **La domanda 1**: la decide la fase del nucleo E10o, con la sua nota (piano §16, il generatore dei form); qui niente.
