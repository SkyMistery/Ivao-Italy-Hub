# Le domande e l'iscrizione a un evento in presenza: le letture del design (E16)

**Data:** 12 ottobre 2026 — fase E16 di M4 (la prima di M4c: le domande e l'iscrizione)
**Stato:** **decisa** (Carmine, 12 ottobre 2026, in chat alla sessione master e pubblicata su sua istruzione: [le risposte sulla
#253][a]): **(A) a tutte e due** le domande di [l'issue #253][q], aperta all'inizio della fase perché il codice ne dipende (§5); e
**le letture che sono scelte**, portate a Carmine dal revisore della #255 ([le risposte sulla #255][b]): **sì** alle letture 15, 20 e 4 e
alla mail a ogni cambio (12), e alle domande e al luogo dati a chi legge senza accesso; **no** alle parole delle liste scritte dal server
nella lingua dell'account (§5, «Le risposte sulle letture»); e **sì alla lettura 11 com'è ora**, dopo la seconda lettura del revisore
([la risposta][c]). Le altre letture restano come scritte.
**Regola applicata:** `CLAUDE.md` §5, caso **(a)** per le letture del design M4 §4-bis, §1.10, §8.3, §11.1 e delle note
`2026-09-29-gli-eventi-in-presenza` e `2026-09-29-i-dati-dei-membri-negli-eventi` §2.5 — l'unico handler, il guardiano dell'interceptor
con il ritiro di chi ha mandato la riga (`[WithdrawnByStakeholder]`, E10h), la cura dell'evento sulle sue righe (`IEventChild`), il
motore CRUD, `Refusals`, `IPersonalDataEraser`, il servizio delle notifiche —; caso **(b)** per la domanda 1, che tocca il generatore dei
form del nucleo (`SchemaForm`). Una migrazione del modulo; niente nel nucleo in questa PR.

[q]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/253
[a]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/253#issuecomment-6120877540
[b]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/255#issuecomment-6131119914
[rv]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/255#issuecomment-6130535660
[c]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/255#issuecomment-6146461872
[rv2]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/255#issuecomment-6146420615

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
   se ne tolgono): `events:errors.questionAnsweredType`, `events:errors.questionAnsweredChoices`. **Si elimina** (sì di Carmine sulla #255):
   le sue risposte non si leggono e non si contano più, spariscono alla prossima modifica del membro, e **la cancellazione dei dati di una
   persona non le tiene mai** (lettura 20). Un valore rimasto di un tipo che non torna più con la domanda — un cambio di tipo nell'istante
   fra il controllo dello staff e il salvataggio di un membro — si salta in ogni lettura invece di far cadere la pagina (`Fits`, il punto 3
   del [revisore][rv]).
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
    incontrano sull'indice univoco, e **la seconda diventa un cambio dell'iscrizione della prima** (le ultime risposte mandate restano,
    con la loro mail; provato con una transazione tenuta dal test, dopo la revisione). Prima rispondeva «riprova»: la lettura è cambiata
    con le correzioni della revisione, e **Carmine ha detto sì com'è ora** ([la risposta sulla #255][c], 13 ottobre 2026).
12. **La mail `registrationReceived`** parte a ogni iscrizione **e a ogni cambio delle risposte**, con il luogo e le risposte come sono in
    quel momento, nella lingua del membro: l'ultima mail dice le risposte vere. Ritirarsi non manda niente.
13. **Annullare e cambiare gli orari** avvisano anche chi è iscritto: una mail per persona, anche a chi ha prenotato e si è iscritto
    (`EventSaving.PeopleToTellAsync`).

## 3. Lo staff (design §4-bis.2, §7.2)

14. **Le schede** di un evento in presenza: **«Domande»** (`Events.View`, la scrive chi ha `Events.Edit`) e **«Iscrizioni»**
    (`EventBookings.View`; «Togli» a chi ha `EventBookings.Edit`).
15. **Le somme stanno nella lista delle domande**, una colonna per domanda: un sì o no conta i sì e i no, una scelta conta ogni scelta,
    un numero dà il totale, un testo dice quanti hanno risposto. Sono **statistiche dell'evento**, lette con l'evento (§6.1: `Events.View`
    «e le statistiche»), senza persone (sì di Carmine sulla #255). Calcolate dal server a ogni lettura (`RegistrationSums`: ogni iscrizione
    letta una volta, ogni domanda sommata in un passaggio), mandate **come numeri** per chiave, e **dette in parole dalla pagina, nella
    lingua sullo schermo** (il no di Carmine alla lettura di prima, §5). **Nessun endpoint in più.**
16. **La lista delle iscrizioni** è una risorsa del motore in sola lettura (`/api/events/registrations`, come le prenotazioni di E6b): il
    membro detto da `col.person`, le risposte **come sono state date** — la pagina le scrive in parole, una riga per domanda, nella lingua
    sullo schermo, con le domande dell'evento —, quando si è iscritto e quando le ha cambiate. Così anche le proprie iscrizioni del membro:
    la sua lettura porta le domande del suo evento e le risposte.
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
    scrive in `vid`, **con le sole risposte che le somme contano** (sì o no, una scelta, più scelte, numeri) **alle domande che l'evento ha
    ancora**, come tornano con la loro domanda — e una scelta solo se nomina scelte della domanda (la [seconda lettura][rv2], piccolo 1) —,
    **e nient'altro**: né i testi, che sono parole del membro (come una prenotazione tenuta perde la nota), né le risposte a domande
    eliminate (il punto 1 del [revisore][rv]). **E la riga conta sempre come svuotata**, anche quando le
    risposte non cambiano, così il nucleo svuota le sue copie di prima nell'audit, dove resterebbe una parola tolta da un cambio di prima (il
    punto 2: provato da un test, che senza questo cade). Due righe dell'anteprima: `events:erasure.registrationsToCome` e
    `…registrationsOver`. Con **E10n** nella coda, `AnswersJson` porta `[ErasedWith(nameof(Vid))]`: un'iscrizione ritirata o tolta prima
    della cancellazione non lascia le risposte nell'audit. Sì di Carmine sulla #255, con queste correzioni.

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

### Le risposte sulle letture (12 ottobre 2026, sulla #255)

Il [revisore della #255][rv] ha portato a Carmine le letture che sono scelte, e che la #253 non copriva; Carmine ha risposto in chat alla
sessione master, che ha pubblicato le risposte [sulla #255][b] su sua istruzione:

1. **Lettura 15, le somme lette con `Events.View`**: sì — statistiche dell'evento, lette dallo staff, senza nomi.
2. **Lettura 20, l'iscrizione a un evento finito tenuta con lo pseudonimo**, con le risposte che le somme contano e senza i testi: sì,
   **con il punto 1 del revisore corretto**, così che nessun testo sopravviva.
3. **Lettura 4, una domanda con risposte si elimina**: sì, con la stessa correzione.
4. **Lettura 12, una mail a ogni cambio delle risposte**: sì.
5. **Le domande e il luogo a chi legge la pagina senza accesso**: sì.
6. **Le parole delle liste scritte dal server nella lingua dell'account**: **no**. Una pagina letta in inglese non mostra celle in
   italiano. In questa PR le liste — quella delle domande, quella delle iscrizioni, le proprie del membro — ricevono i dati (la domanda,
   il valore, le chiavi delle scelte, i conti) e il client scrive le parole nella lingua sullo schermo; **le mail tengono le parole del
   server, nella lingua di chi le riceve**. Fatto sulla #255 (letture 15 e 16).

E dopo la [seconda lettura del revisore][rv2], che gliel'ha portata perché era cambiata con le correzioni: **lettura 11 com'è ora, sì**
([la risposta][c], 13 ottobre 2026) — due iscrizioni dello stesso membro allo stesso evento nello stesso istante, la seconda diventa un
cambio della prima, le ultime risposte mandate restano con la loro mail, e non risponde «riprova».

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
- **Design M4 §11.1**: che cosa resta di un'iscrizione a un evento finito (lo pseudonimo, le sole risposte contate alle domande che ci
  sono, nient'altro, e nessuna storia nell'audit).
- **Design M4 §7.2**: le schede «Domande» e «Iscrizioni»; **le liste ricevono dati e la pagina scrive le parole nella lingua sullo
  schermo**, le mail tengono le parole del server nella lingua di chi le riceve (il no di Carmine sulla #255, risposta 6). La regola vale
  per ogni lista dei moduli che dica un valore in parole: forse una riga di §16 del piano.
- **Piano §16.6**: il conto degli endpoint a mano di M4 — tre verbi del membro e «togli» di E16.
- **La domanda 1**: la decide la fase del nucleo E10o, con la sua nota (piano §16, il generatore dei form); qui niente.
