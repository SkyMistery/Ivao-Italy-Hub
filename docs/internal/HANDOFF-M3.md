# HANDOFF — stato di M3 (Training)

> Documento **interno** (italiano). È il punto d'ingresso di ogni sessione che lavora al modulo Training. Lo scrive
> **il collaboratore** (`dalberone` e le sue sessioni) alla fine di ogni fase, con un paragrafo «Che cosa ha lasciato
> <fase>» in cima alla sezione «Lo stato». Lo stato generale del progetto (M0–M2) sta in `HANDOFF.md`, che scrive solo
> il maintainer. Le regole — chi unisce, che cosa non si tocca, come si ottiene una decisione — sono in `CLAUDE.md` §0 e
> non si ripetono qui.

> ⚠️ **Gli esami: nessun dato personale, solo il VID.** Di un esame (`trn_exams`, fase A10) l'hub **non tiene nessun dato
> personale** delle persone — né del candidato né dell'esaminatore —: **solo il VID**. Nessun nome, nessun indirizzo, nient'altro
> della persona. È una richiesta precisa del TD (`dalberone`, 25 settembre 2026): gli esami si gestiscono su IVAO, e all'hub
> servono solo per metterli nel calendario.

**Ultimo aggiornamento:** 27 settembre 2026 — **fase A8a** (le date: il server; A8 divisa in apertura come A6), sul branch
`m3/a8a-dates-server`, **PR #147** verso `main`, in bozza **in coda dopo #146** (A7, in bozza in coda dopo #144, in coda dopo #143).
**A6a** (il server della richiesta) è la **PR #143**, pronta con la CI verde, in attesa della **sessione master** di Carmine (nota
`2026-09-26-la-sessione-master`, `CLAUDE.md` §0), che unisce sul via di Carmine e, se un branch del collaboratore va rimesso in pari con
`main`, lo chiede sulla PR senza spingerci niente. **A3 (#131), A4a (#133), A4 (#139) e A5 (#140) sono unite**; la fase del nucleo **A3b**
(#135) è in bozza in una sessione sua, e **A6c** (#145, il suggerimento chiuso di `SchemaForm`) è pronta, da `main` e fuori dalla coda.
**Il prossimo passo** è **A8b** (le date: le pagine), sul branch `m3/a8b-dates-pages` preparato da `m3/a8a-dates-server`, in coda dopo
A8a; poi A9 (dalle fasi del modulo in poi tutto migra `TrainingDbContext`: in fila); A3b va avanti per conto suo prima di A10 (`08`,
«Parallelismo possibile»). In C# una chiave di un modulo si chiede con il namespace (`training:…`, #138).

**Accanto alle fasi del modulo** (26 settembre 2026): la fase del nucleo **A6c** — il suggerimento chiuso di `SchemaForm` tiene la scelta
cliccata dopo aver scritto —, sul branch `m3/a6c-closed-suggestion`, **PR #145** verso `main`, **non in coda** (tocca solo il nucleo del
front end e non migra niente): va avanti accanto ad A6a e A6b come A3b. La sua nota, `2026-09-26-il-suggerimento-chiuso-tiene-la-scelta`,
è **decisa** da Carmine il 27 settembre 2026, come raccomandato
([il suo commento](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/145#issuecomment-5855560813)): la casella e la sua lista sono un
campo solo. Il revisore l'ha trovata approvabile appena la nota registra la risposta: il 28 settembre la risposta è registrata, e
`main` è entrato nel branch con un merge (#142–#172), con build e suite rifatte.

## Da leggere, nell'ordine

1. `CLAUDE.md` (tutto, §0 per primo) e `CONTRIBUTING.md`.
2. Il piano `00-piano-di-progettazione.md`: l'intestazione, **§9** (catalogo dei moduli; la riga Training di §9.2),
   **§9.3**, **§9.5** (calendario unico), **§9.7** (contratti nucleo↔moduli), **§16** (meccanismi generici), **§13**
   (la riga M3), §4 (forkabilità, i ruoli di `StaffRoleMap`: `TC`, `TAC`, `TA1–9` → `Training`, `T01–T99` → `Trainer`),
   §2.2 e §2.3-ter (i servizi di oggi: `training.ivao.it` e il TDCenter del template HQ, come elenco di funzioni).
3. Le note che decidono come stanno i moduli: `decisions/2026-09-13-moduli-non-subordinati-ai-dipartimenti.md`,
   `2026-09-13-ordine-dei-moduli.md`, `2026-09-13-le-dashboard-a-tutto-schermo.md`,
   `2026-09-15-permessi-su-una-riga-e-chi-ha-interesse.md`, `2026-09-24-un-secondo-sviluppatore.md`.
4. **Il modello da copiare**: `05-design-m2.md` (il design dei tour: la forma di un design di modulo, §0 perimetro, §0.5
   «che cosa si prende dal sistema di oggi e che cosa no», i permessi, le estensioni del nucleo numerate) e la parte C
   di `06-piano-implementazione-m2.md` (la forma delle fasi: dipende da, perimetro, test, «fatta quando», «Com'è andata»).
5. **Il modulo che esiste già**: `src/IvaoHub.Modules.FlightOps/` e `web/src/modules/flightops/`. Si legge per capire
   come si scrive un modulo; **non si modifica** e non si importa (un modulo non conosce l'altro).
6. **Il design e il piano di M3**: `07-design-m3.md` (deciso il 25 settembre 2026), `08-piano-implementazione-m3.md` — le
   regole di tutte le fasi e la fase che si apre — e le note della fase A0 (`decisions/2026-09-25-*`, elencate in `08`, A0).

## Che cosa il nucleo dà già a Training

Costruito per i tour, generico, pronto; M3 lo usa e non lo riscrive (`CLAUDE.md` §2):

- **Il modulo** (`IModule`): permessi `Training.<Azione>` nel catalogo, impostazioni del modulo (`ModuleSettings`),
  tipi di notifica (`NotificationTypes`), audience dei token personali (`TokenAudiences`), segmenti riservati per le
  pagine pubbliche (`ReservedSegments`), il `DbContext` con la sua storia delle migrazioni e il prefisso `trn_`.
- **Chi gestisce**: `division.json → modules.training.baseDepartment` è già `TD`; coordinator e assistant del TD hanno
  tutto con `positionGrants`, da scrivere nel design; gli advisor e i trainer li decide il design.
- **Righe a cura di più dipartimenti** (`IOwnedByDepartment` a insieme), **permessi su una riga sola**
  (`resource_scope`), **chi ha interesse non decide** (`IHasStakeholder`), **chi partecipa legge** (`IHasParticipants`),
  i grant di un modulo (`ModuleGrants`).
- **Calendario, ricerca, award, fili dei contatti**: `IProjectable`, nella stessa transazione. Il piano prevede già le
  voci di calendario `training` ed `exam`, e che le sessioni di training siano **pubbliche** nel calendario.
- **Le dashboard** `/me` e `/staff` sono righe `Dashboard`: una tessera di Training è un **blocco Data** del modulo.
- **Lista e form generati** (`MapCrud`, `DataList`, `SchemaForm`), testo ricco con `BlockDocument`, i fili dei contatti
  (`ContactThreads`), le mail per intenti.

## Per i test

VID `790001–790099`, slug `trn-test-`. **Attenzione al TD**: i test dei contatti affermano l'insieme esatto di chi riceve
un messaggio al TD; seminare un coordinator del TD in un test di Training può romperli solo in CI (`CONTRIBUTING.md`).

## La prima sessione: il design

**Una PR con un solo documento**, `docs/internal/07-design-m3.md`, su un branch `m3/design`. Nessun codice. Come per i tour,
il design **si scrive facendo domande**: il primo lavoro è capire come funziona il training oggi (`training.ivao.it`,
il TDCenter, quello che il collega sa da staffista TD) e che cosa lo staff vuole tenere, cambiare, lasciare. **Sui fatti**
(come funziona oggi) risponde il collega. **Le scelte** le decide Carmine: il documento le raccoglie in una sezione «Domande
per Carmine», ognuna con una raccomandazione; Carmine risponde in un commento sulla PR, e la risposta entra nel documento con
la data e il link al commento. Il design è **chiuso** quando Carmine lo approva, e solo allora nasce
`08-piano-implementazione-m3.md` (le fasi) in una seconda PR, con le note di decisione. Ogni estensione del nucleo che il design scopre si numera nel design, come in
`05-design-m2.md`, e diventa una fase a sé, prima delle fasi del modulo.

Il prompt di apertura, da incollare nella prima sessione:

```text
Lavoriamo al modulo Training (M3) dell'hub. Sei un collaboratore: leggi CLAUDE.md, §0 per primo, e rispettalo alla
lettera — non fai push su main, non unisci, non modifichi i file riservati al maintainer. Poi leggi CONTRIBUTING.md e
docs/internal/HANDOFF-M3.md, e i documenti nell'ordine che HANDOFF-M3.md indica.

Questa fase è il design: docs/internal/07-design-m3.md, sul branch m3/design, sul modello di 05-design-m2.md. Nessun
codice. Prima di scrivere il documento fammi le domande che servono per capire il training di oggi, a gruppi. Le scelte
non le prendiamo noi due: mettile nella sezione "Domande per Carmine", ognuna con la tua raccomandazione. Scrivi nel design
da dove viene ogni scelta. Quando il documento è pronto, apri la PR con il template compilato, compresa la sezione
"For the reviewer", aggiorna questo HANDOFF-M3.md, e fermati.
```

## Lo stato

*(Qui, in cima, il paragrafo «Che cosa ha lasciato <fase>» di ogni fase chiusa, la più recente per prima.)*

### Che cosa ha lasciato A8a (27 settembre 2026, branch `m3/a8a-dates-server`, PR #147)

- **A8 è divisa in apertura**, come A6 (scritto in `08`, sotto A8): **A8a il server** (questa), **A8b le pagine** (la prossima, sul branch
  `m3/a8b-dates-pages` da `m3/a8a-dates-server`). A9 viene dopo A8b.
- **Che cosa c'è** (codice del modulo, nessun file del nucleo, nessuna nota nuova; una migrazione, `AddSlots`, solo additiva):
  - **`trn_slots`** (`src/IvaoHub.Modules.Training/Dates/TrainingSlot.cs`): le date proposte dal trainer, con inizio, fine, gli avvisi in
    JSON e i timbri (chi e quando). **Esistono solo mentre il training aspetta la data**: alla scelta, all'override e alla chiusura vanno
    via tutte, e il training tiene l'inizio della sessione (`scheduled_start_utc`) e quale proposta era (`chosen_slot_id`, vuoto per
    l'override). La sessione in corso è solo il suo inizio.
  - **Gli avvisi e le politiche** (`Dates/DateConflicts.cs`, `Dates/DivisionDays.cs`): i giorni che una data tocca nel fuso della
    divisione; gli altri training `Scheduled` con la sessione in quei giorni e le voci del calendario dei tipi di `conflictKinds` (senza le
    sessioni dei training, già contate); `Warn` chiede `confirmed`, `Block` rifiuta, `None` non guarda. Nessun nome né VID negli avvisi.
  - **I verbi** (`Dates/TrainingDates.cs`): dello staff in `/api/training/trainings/{id}` — `GET conflicts?startsAtUtc=&endsAtUtc=`,
    `POST slots` (le date insieme, con `confirmed` e la `rowVersion`), `POST slots/{slotId}/withdraw`, `POST date` (l'override, solo
    l'inizio), con `Training.Conduct` sulla riga; `POST close` con un motivo, con `Training.Approve` —; del trainee `POST
    /api/training/mine/{id}/choose` (`slotId`, `rowVersion`).
  - **Il calendario**: `Training` è `IProjectable` — una voce `training` pubblica all'inizio della sessione finché è `Scheduled`, titolo
    «sigla · postazione», indirizzo `/training/sessions/{id}` —. La sigla la dice il contesto del modulo a ogni training che traccia
    (`TrainingDbContext`, evento `Tracked`; `Training.RatingShortName`, non è una colonna).
  - **I job**: `training-reminders` (`Dates/TrainingRemindersJob.cs`, ogni quarto d'ora ai minuti 5, 20, 35, 50; `reminded_at` è
    `[NotAudited]`, il segno prima delle mail); `training-expiry` ora chiude per tempo (solo con `maxResponseDays`, dall'ultima data
    proposta) prima di togliere i grant.
  - **I DTO**: `held` («Eseguito», dal giorno dopo nel fuso della divisione) nella riga, nella pagina dello staff e in quella del trainee; il
    trainee ha `slots` (da venire, senza avvisi), `trainer`, `closeReason`; lo staff `slots` con `warnings`, `proposedBy` e `proposedAt`,
    `dateChosenByTrainee`, `closeReason`, `actions.canConduct`, `actions.canClose`. Tutto in `web/src/shared/api/schema.d.ts`.
  - **Le mail** `datesProposed`, `dateConfirmed`, `reminder`, `trainingClosed` (`TrainingMail.SessionAsync` per le due a tutti e due), in
    UTC; i nomi delle persone in `TrainingPeople`.
  - **I test**: `TrainingDatesRulesTests` (unità), `TrainingDatesTests` (integrazione, VID 790032–790038, con il «fatta quando» attraverso
    l'API).
- **Che cosa deve sapere la fase dopo**:
  - **A8b** (le pagine): la pagina dello staff chiede `conflicts` prima di scrivere e mostra gli avvisi — con `Warn` li fa confermare e
    manda `confirmed: true`, con `Block` sono un rifiuto —; la proposta e il ritiro valgono solo in `Assigned`, l'override anche in
    `Scheduled` (`actions.canConduct`); la chiusura è `actions.canClose`. I riquadri del trainee sono `slots` di `GET
    /api/training/mine/{id}`; la scelta risponde con il training com'è dopo, e un 409 vuol dire date cambiate nel frattempo. Le mail al
    trainee puntano già a `/training/mine/{id}`. ⚠️ **Il promemoria in Mailpit sul banco**: il banco non ha un modo di far partire un job a
    comando (l'unico endpoint del banco è `/e2e/signin`, in `src/IvaoHub.Web`, che è nucleo): o la spec aspetta il giro del quarto
    d'ora, o serve un modo di far partire il job, che è un cambio del nucleo con la sua nota. Il test d'integrazione di A8a lo prova
    attraverso il job.
  - **A9**: la rischedulazione riporta ad `Assigned` e azzera la sessione in corso, e le date si propongono di nuovo; `Training.Project`
    va esteso alle sessioni `Held` (la voce della sessione in corso sparisce da sola quando il training non è più `Scheduled`).
  - **A10**: «in attesa di scelta da N giorni» è `TrainingDates.Unanswered` con `now − responseReminderDays`.
  - ⚠️ **Le parole nuove del modulo arrivano al server solo dopo `pnpm i18n:sync` e una build** (la copia in `locales/`): senza, una mail
    dice la chiave. ⚠️ **I dati di una mail sono JSON con i caratteri non ASCII in escape**: un test cerca le parti ASCII.
  - ⚠️ **Un training `Scheduled` con la data scritto in un test si proietta nel calendario**: una pulizia che cancella in blocco toglie
    anche le sue voci (`TrainingStaffTests.CleanAsync` ora lo fa).
  - VID: il prossimo libero è **790039**, poi **790045** (A3b usa 790040–790044 e 790050–790051).
- **Le correzioni della revisione di #147** (28 settembre 2026; il dettaglio con i commit è in `08`, «Com'è andata (A8a)»):
  - **Gli avvisi salvati con una data si fermano a ciò che ogni lettore del training può leggere** (`DateConflicts.Kept`, il tetto del
    nucleo `VisibilityCeiling.For(Staff)`): chi propone vede e conferma tutto ciò che legge; la data tiene le voci di tutti, dei membri e
    dello staff, mai quelle di un solo dipartimento. La forma degli avvisi non cambia.
  - **La chiusura tiene la data della sessione** (il registro, design §6) e toglie solo le date proposte: il calendario, il promemoria e
    «Eseguito» guardano lo stato. ⚠️ **Per A9a**: il suo commento di `Training.ScheduledStartUtc` («… closed») e il suo scostamento 2
    («come la chiusura di A8a») non valgono più (`08`, «Com'è andata (A8a)», correzione 3).
  - **La chiusura dell'hub** si riconosce da `closed_by` e `close_reason` vuoti: nessun segno nuovo, lo dicono la mail e i dati.
  - **I test dei rifiuti**: il trainer con `View` per posizione e senza grant sul training, uno dello staff che è trainee di un training
    suo, i limiti di una proposta, gli stati e i 409; VID **790072–790073** (790074–790079 restano della correzione di A8a, liberi).
  - **Il giro dello staff chiude ciò che lascia** (`web/e2e/full/training-staff.spec.ts`): il training ATC del giro nel `finally`, e
    all'inizio quello che un giro precedente ha lasciato aperto, con la chiusura dello staff (revisione di #146, punto 2). Sullo stesso
    banco, mai ricreato, le spec del training passano giro dopo giro. ⚠️ Al terzo giro della giornata sullo stesso banco due spec dei
    tour (M2) si sono viste rifiutare un report, con un 400; non l'ho indagato (`08`, «Com'è andata (A8a)», correzione 13).
  - ⚠️ **Per A12**: l'eraser cancella i training aperti **passando dal change tracker**, non con `ExecuteDelete`: la voce del calendario
    di un training datato è una proiezione, e la toglie solo l'interceptor quando salva la riga (revisione di #147).
  - ⚠️ **`open_kind` lo scrive solo il getter** (`Training.cs`, `OpenKind`; l'indice unico in `TrainingDbContext`): un cambio di stato
    con `ExecuteUpdate`, o qualsiasi scrittura che salta l'entità, lo lascia impostato, e la chiave unica blocca allora **per sempre** il
    percorso di quel trainee. Due esempi: una chiusura notturna in `training-expiry`, o l'eraser di A12b. La chiusura per tempo di A8a
    passa dall'entità (controllato; il test della notte prova che dopo la chiusura il percorso è libero). Lo chiede la revisione di
    #143, punto 2.
  - **La coda è in pari con `main`** attraverso A7 (`3073b59`, con A6b e `main` dopo #135 e #160–#172), entrato con un merge. Il
    catalogo di A3b non tocca A8a: nessun permesso del training è `OnlyForAssignee`, fino ad A7b.
- **La coda**: la PR è in bozza con `(after #146)` e `Queued after #146.`; #146 è in coda dopo #144, in coda dopo #143. Quando #146 sarà
  unita, il passo della coda — `main` nel branch con un merge (mai un rebase), build e **tutti** i test di nuovo, via la coda dal titolo e
  dal corpo, la PR pronta a CI verde — lo fa la sessione di A8a se è ancora viva, altrimenti quella di A8b prima di cominciare.

### Che cosa ha lasciato A7 (26 settembre 2026, branch `m3/a7-approve-and-assign`, PR #146)

- **Che cosa c'è** (codice del modulo, nessun file del nucleo, nessuna nota nuova, nessuna migrazione):
  - **Il training si scrive anche con `Training.Approve`, `Training.Assign` e `Training.Conduct`** (`[AlsoWrittenWith]` di A3 su
    `Training.cs`, mai alla creazione), ognuno con lo scope della riga e mai dal trainee; `Training.IdOf` legge uno scope all'indietro.
  - **Il lato dello staff** in `src/IvaoHub.Modules.Training/Staff/`: la lista `/api/training/queue` (`MapCrud` in sola lettura con
    `Training.View`, le viste `filter[queue]` = `toApprove`, `toAssign`, `inProgress`, `toClose`, `history` in `StaffQueue`, e
    `filter[kind]`, `filter[traineeVid]`, `filter[trainerVid]`, `?q=` su postazione e VID del trainee); la pagina
    `/api/training/trainings/{id}` (`StaffTrainingDto`, con `actions.canDecide` e `actions.canAssign` dall'unico handler sulla riga) e i
    verbi `/accept`, `/reject` (con il motivo), `/assign`, e `/trainers` (i candidati: `TrainerChoice`); il servizio `StaffTrainings`.
  - **Il grant del trainer**: assegnare scrive `Training.Conduct` con lo scope del training (`ModuleGrants`, motivo `training: trainer`)
    prima della riga e toglie quello del trainer di prima; **il job `training-expiry`** (`TrainingExpiryJob`, 04:15 nel fuso della
    divisione) toglie i grant dei training non più aperti, e quelli lasciati da una scrittura fermata a metà (dopo un'ora).
    ⚠️ **È uno scostamento dalla risposta 2 su #135**, e Carmine ha deciso che resta fino ad A7b
    ([commento su #146](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/146#issuecomment-5855560982)): dopo
    l'unione di #135 la fase **A7b** porta il trainer sulla regola di A3b (`IHasAssignee` sul training, `Training.Conduct` segnato
    `OnlyForAssignee` e tenuto per posizione da TA e trainer, via il grant e la sua metà del job). Nota
    `2026-09-27-il-trainer-sulla-regola-delle-righe-affidate`; la lista della fase è in `08`, A7b. ⚠️ **A7b arriva prima di ogni
    installazione con trainer veri** ([il revisore](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/146#issuecomment-5877192345)):
    fino ad allora, con tre assegnazioni insieme il trainer che la riga nomina può restare senza grant, e la notte non lo ridà.
  - **Le mail** `requestAccepted`, `requestRejected` (con il motivo) e `trainerAssigned` (al trainee e al trainer, ognuno nella sua
    lingua), attraverso **`TrainingMail`**, che usa anche la `requestReceived` di A6a.
  - **Le pagine** `web/src/modules/training/screens/staff.tsx`: `/staff/training` (lista generata, filtri Mostra e Percorso, voce «Richieste
    e training» nella barra) e `/staff/training/$id` (il promemoria del teorico con il sito dell'esame, la richiesta, la decisione, il
    trainer e la sua scelta, Accetta e Rifiuta); le funzioni pure in `screens/trainings.ts`.
  - **I test**: `TrainingStaffRulesTests` (unità), `TrainingStaffTests` (integrazione, VID 790022–790031), `screens/trainings.test.ts` e
    `schemas.test.ts` (Vitest), `web/e2e/training-staff.spec.ts` (lo smoke), `web/e2e/full/training-staff.spec.ts` (il «fatta quando»).
- **Che cosa deve sapere la fase dopo**:
  - **A8** (le date): il trainer conduce con il grant sullo scope `training:training:{id}`, e TC e TAC con `Conduct` per posizione
    (l'override); `StaffQueue.HeldBefore` dice già da quando una sessione è «Eseguita» nel fuso della divisione, e va riusato;
    `StaffTrainings.IsAssignable` comprende `Scheduled` (si riassegna tenendo la data); la chiusura per tempo va nello stesso
    `TrainingExpiryJob.RunAsync`, **prima** di togliere i grant, così la stessa notte li toglie. La pagina dello staff ha le sezioni della
    richiesta, della decisione e del trainer: le disponibilità e la sessione vanno sotto. La chiusura a mano dello staff è `Training.Approve`
    (§2.5), come accetta e rifiuta.
  - ⚠️ **Il banco dopo il giro di A7**: il trainee del banco (999002) resta con un training **ATC `Assigned`** al trainer del banco (999004),
    che nessuno chiude prima di A8. `training-staff.spec.ts` gira dopo `training-request.spec.ts` (ordine di nome, un worker): il giro di A6b
    trova i percorsi liberi. Una spec di A8 che vuole un training assegnato può riprendere quello (con un nome che viene dopo
    `training-staff`), o chiederne uno suo e chiuderlo con la chiusura dello staff; **il banco va ricreato prima di ogni corsa**.
    Dopo le correzioni della revisione vale ancora, **su A7 da solo**: nessuna API di A7 chiude un training accettato, quindi un secondo
    giro sullo stesso banco cade sul percorso ATC occupato in `full/training-request.spec.ts` (A6b) e in `full/training-staff.spec.ts`,
    ognuno con il suo messaggio. La chiusura nel `finally` della spec dello staff, e all'inizio per un training ATC lasciato aperto da un
    giro prima, la aggiunge la correzione di A8a, che ha `/close`.
  - **A9**: il DTO dello staff non ha `StaffComment` né le note della scheda: li aggiunge la funzione unica che li toglie al trainee della
    riga. **A10**: le viste della lista sono le code di `approvalQueue`, e `filter[trainerVid]` quella di `trainerQueue`.
  - ⚠️ **Lo staff del training** (i candidati) è chi ha una posizione del dipartimento base o della direzione (DIR, ADIR) nel roster: non il
    web master, non chi ha solo un grant. Un TC che assegna se stesso riceve anche lui il grant, e rientra.
  - ⚠️ **Un cookie vecchio si prova su un endpoint con un permesso** (per esempio `/api/training/queue`): `/api/me` con un cookie respinto
    risponde 200 senza utente, non 401.
  - ⚠️ **In PowerShell 5.1 un sorgente non si riscrive con `Get-Content`/`Set-Content`**: i caratteri non ASCII si rovinano (è successo a
    `staff.tsx`, «Ã‚Â·» al posto di «·»). Solo l'editor.
  - VID: il prossimo libero è **790032** (A3b usa 790040–790044 e 790050–790051).
- **Trovato guardando a mano, non toccato (nucleo)**, detto al revisore: il back office **non si usa largo 375 px** (la barra laterale
  dello staff resta aperta a 522 px e il contenuto a 87 px, in ogni pagina dello staff); **un select vuoto di `SchemaForm` dice «Select an
  option»** in ogni lingua (il segnaposto di Atmosphere: `SchemaForm` non ne passa uno); scelto un valore dall'elenco, **il primo clic su un
  pulsante a volte chiude soltanto l'elenco**. Sul banco di anteprima il trainer va fatto entrare una volta, o non è nel roster e la pagina
  dice che nessuno può allenare il training (giusto).
- **Le correzioni della revisione** (28 settembre 2026, [la revisione](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/146#issuecomment-5855673527);
  i dettagli, commit per commit, in `08`, A7):
  - **Un 409 non toglie più il grant al trainer che il training nomina** (d23a812): il `catch` dell'assegnazione rilegge il training e
    toglie il grant solo se la riga non nomina quel trainer. Il test nuovo di `TrainingStaffTests` cade sul codice di prima. Dopo la
    seconda revisione (1feb55a) rilettura e rimozione stanno in `TakeBackAfterConflictAsync`, che non lancia mai: se falliscono, il 409
    esce lo stesso, e il grant che non hanno potuto giudicare resta alla notte.
  - **Dopo un 409 la pagina dello staff si rilegge** (caf6d4e): `useStaffStep` invalida la pagina e i trainer su un errore, il form si
    ridisegna con la versione nuova, e il conflitto dell'assegnazione lo dice un avviso (`useRefused`, `isConflict`).
  - **Il giro sul banco** (2327c05): lo scheletro lascia fuori i grant con scope; la spec dello staff annulla all'inizio una richiesta
    rimasta in attesa, e lascia stare il grant su un training finito, che aspetta la notte. Quello che resta fuori è scritto sopra, al ⚠️
    del banco. `web/e2e/full/README.md` non si tocca: per `core-guard` è un file del nucleo.
  - **La decisione di Carmine** (842745a): la nota nuova, lo scostamento in `08` e la fase **A7b**, con quello che le hanno aggiunto la
    revisione di A8a e i tre punti della regola di A3b.
  - **`main` è entrato con A6b** (a00fcd5: `b4bd304`, con `main` a 3c79786), come il master ha chiesto su #144. Il catalogo di A3b non
    cambia niente di A7: nessun permesso del training è segnato `OnlyForAssignee`, e le tre alternative del training passano il
    controllo all'avvio.
- **La coda**: la PR è in bozza con `(after #144)` e `Queued after #144.`. Quando #144 sarà unita, il passo della coda (`CONTRIBUTING.md`,
  «Phases in a queue») e la PR pronta a CI verde. Il merge verso l'alto delle correzioni (A8a…A10b) lo fa una volta sola la sessione
  «Verifica risposte e correzioni». Una correzione chiesta su #144 si fa sul suo branch e sale con un merge.

### Che cosa ha lasciato A6b (26 settembre 2026, branch `m3/a6b-request-pages`, PR #144)

- **Che cosa c'è** (codice del modulo, solo front end: nessun file del nucleo, nessun cambio del server, nessuna nota nuova):
  - **`/training/request`** (`web/src/modules/training/screens/request.tsx`, rotta `member` nel manifest, `?kind=` per il percorso):
    i dati del trainee in sola lettura (mai l'email), il percorso con il training che sarebbe, il rating proposto e il mock exam, la
    postazione (suggerimento chiuso), disponibilità e note; **«Richiedi training» apre la domanda sul teorico** (`ConfirmDialog` con la
    risposta nei `children`; la conferma manda il form generato per il suo `id`, `requestSubmit`); il «no» lo dice a schermo al posto
    del form; un percorso rifiutato mostra la frase e i dettagli dal `GET`.
  - **`/training/mine`** (`screens/mine.tsx`): per percorso rating, ore, che cosa si può chiedere o perché no (l'attesa residua
    compresa), il mock exam, «pronto per l'esame»; le richieste e i training dal più nuovo, con lo stato, il motivo di un rifiuto, le
    caselle del report e **«Annulla la richiesta»** su `Requested`.
  - Le funzioni pure in `screens/trainee.ts` (con i percorsi `MINE` e `REQUEST`), i pezzi comuni in `screens/parts.tsx`
    (`RefusalDetailText`, `TheoryExamLink`, `StateBadge`); `api.ts` (`mineQuery`, `useRequestTraining`, `useCancelTraining`), `schemas.ts`
    (`requestSchema`, `requestFromFormValues`, `requestSearchSchema`, `EMPTY_REQUEST`); le parole `request`, `mine`, `states`, `refusal`,
    `mockExam`, `theoryExam`, `unknown` in `training.json`.
  - **I test**: `schemas.test.ts` e `screens/trainee.test.ts` (Vitest), `web/e2e/training-request.spec.ts` (lo smoke),
    `web/e2e/full/training-request.spec.ts` (il «fatta quando» di A6 sul banco).
- **Che cosa deve sapere la fase dopo**:
  - **A7** (le pagine dello staff): la pagina del trainee **non cambia** con A7, ma i suoi stati sì: una richiesta `Accepted` non si
    annulla più (`isCancellable`), e `stateMoment` dice già il giorno della decisione. La mail di una richiesta rifiutata dallo staff
    porta il motivo, che `/training/mine` mostra già (`rejectionReason`). ⚠️ **Il banco non ha più richieste in attesa dopo il giro**:
    la spec di A6b annulla le sue; una spec di A7 che vuole una richiesta da accettare la chiede da sé (come trainee, `?as=pilot`) e
    la chiude alla fine (un training accettato non si annulla dal trainee: la chiude lo staff, A8, o la si rifiuta).
  - **A8** (le date): i riquadri vanno in `/training/mine/$id` (design §4.1), una rotta `member` come queste due; `/training/mine`
    oggi non ha un dettaglio per training. `stateMoment` mostra già l'ora di una sessione `Scheduled` (UTC).
  - ⚠️ **Il suggerimento chiuso del nucleo perde una scelta cliccata dopo aver scritto** (sotto, «Trovato»): finché la correzione, la
    fase del nucleo A6c (#145), non è in `main`, una spec sceglie dall'elenco (clic sulla casella, poi sull'opzione) o scrive il valore
    intero; dopo, le spec di A6b possono scrivere una parte del nominativo.
  - ⚠️ **`pnpm i18n:check` non controlla le chiavi con il namespace** (`t('training:…')`): una chiave sbagliata del modulo la trovano
    solo le spec che leggono le parole dai file di lingua.
  - ⚠️ **Una conferma nell'angolo (`useNotice`) si cerca in una spec con il testo esatto**: il toast la annuncia anche in una `span`
    «Notification …»; in locale era già sparita, in CI no, e la prima CI di #144 è caduta lì.
  - VID: A6b non ne usa; il prossimo libero resta **790022** (A3b usa 790040–790044 e 790050–790051).
- **Trovato, non toccato (nucleo)**: nel **suggerimento chiuso** di `SchemaForm` (`Suggest` con `suggestionsOnly`) chi scrive per
  cercare e poi clicca un'opzione perde la scelta — la casella torna vuota —, perché `onBlur` rimette il valore di prima e la lista si
  ridisegna sotto il puntatore; misurato nel browser, in jsdom non si vede. Detto al revisore su #144. **La correzione è la fase del
  nucleo A6c, PR #145** (da `main`, non in coda; nota `2026-09-26-il-suggerimento-chiuso-tiene-la-scelta`, «Proposta»): la casella e la
  sua lista sono un campo solo, e la regola del campo chiuso vale quando il fuoco esce da tutte e due. La prima idea, tenere il fuoco
  nella casella annullando la pressione sulla lista, l'ha provata e scartata: la barra di scorrimento della lista non si trascina più.
- **Anche questo, guardando a mano** (sul banco di anteprima, 5090): «Richiedi training» è un pulsante grigio, perché `ConfirmDialog` ha
  solo i pulsanti `ghost` e `secondary`; un pulsante primario sarebbe un'estensione del nucleo, detta al revisore. L'intestazione del
  sito è larga 1044 px su un telefono in ogni pagina (nucleo).
- **La coda**: #143 è unita (27 settembre), e il passo della coda l'ha fatto la sessione di A10b, in cima alla coda (f5e3cd6). Ha segnato
  la PR pronta prima della correzione di Invio che il revisore aveva chiesto, e la PR è tornata in bozza
  ([commento](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/144#issuecomment-5857989492)). Con le correzioni qui sotto torna pronta a
  CI verde, senza coda.
- **Le correzioni della revisione** (28 settembre 2026, [la revisione](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/144#issuecomment-5855612519);
  i dettagli, commit per commit, in `08`, A6b):
  - **`main` è entrato nel branch** (3c79786: A3b, #135, e #160–#172), come il master ha chiesto su #144. Il catalogo di A3b non cambia
    niente di A6b.
  - **Invio nella postazione apre la domanda sul teorico**, come il pulsante (2d20da4). Ogni invio del form passa da `letThrough`
    (`onSubmitCapture` di `RequestForm`), e va avanti solo quello che parte dalla conferma della finestra; la risposta si dimentica quando
    la finestra si chiude. ⚠️ Per una spec: con la domanda, Invio nel form apre la finestra e non manda niente, e la richiesta parte solo
    da «Invia la richiesta».
  - **«Torna alla richiesta» dopo un «no» tiene quello che il trainee ha scritto** (edfe7cb): il form resta montato, nascosto, mentre si
    legge il rifiuto dell'hub.
  - **L'etichetta di una postazione** è la chiave `training:positionChoice` (2912154), con `positionLabel` in `screens/ratings.ts`, che
    usano anche le impostazioni.
  - ⚠️ **Restano da fare**, scritti in `08` con il perché:
    - i nit di `mine.tsx` (`CardRoot`, il `' · '` in una chiave, `line-clamp-3`) li fa una fase in cima alla coda, perché A8b, A9b e A10b
      cambiano quel file;
    - `canCancel` dal server, al posto di `isCancellable` e `readyForExam`, quando A7 e A8 aggiungono stati;
    - `Training/Refusals.cs` lo toglie A10c.
- **`main` dopo #177** (28–29 settembre 2026; i dettagli in `08`, A6b): `main` è entrato nel branch con due merge, come il revisore ha
  chiesto per unire la PR: c90dea9 (A6c, #145, #173–#176, **#177**, la scelta da tastiera nel campo suggerito, e #179) e 4b9f6bd
  (**A11a, #159**, unita nel frattempo). Tutte e due le volte l'unico conflitto era qui: i blocchi di A11a e di A6c stanno subito sotto
  questo.
  - ⚠️ **Per una spec, da #177**: in un campo chiuso che si cerca, il primo Invio sceglie l'opzione accesa (la prima mostrata) e non manda
    il form; il form parte dal secondo. Per questo il caso dello smoke «Enter in the position asks the question» ha **un Invio in più**
    dopo il `fill` (c3db117, [la correzione del revisore](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/144#issuecomment-5877395930)).
    `letThrough` non cambia.
  - Con A6c e #177 dentro, una spec può scrivere una parte del nominativo e sceglierlo, con un clic o con Invio; le spec di A6b scelgono
    ancora dall'elenco.
  - Suite rifatte sull'head con A11a: unità 836, integrazione intera 359, `pnpm test` 529, smoke 111, `e2e:full` 42/42 su un
    banco nuovo.

### Che cosa ha lasciato A11a (28 settembre 2026, branch `m3/a11a-fir-heads-core`, PR #159)

- **Che cosa c'è** (nucleo; nota `decisions/2026-09-27-i-capi-fir-sul-loro-fir.md`, **decisa** da Carmine sulla #159,
  [il suo commento](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/159#issuecomment-5864855723), con i cinque
  [rilievi del revisore](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/159#issuecomment-5859604416) dentro):
  - **Il team di un FIR** come seconda specie di posizione di un grant: `UserGrant.PositionFirTeam` (migrazione `AddGrantFirTeam`), in
    `positionGrants` `{ "firTeam": true, "levels": [...], "permission": "...", "scope": "..." }` senza `department` e senza nominare un
    FIR (il `Coordinator` è il capo, l'`Assistant` il vice, l'`Advisor` un CHA). Il seme e la schermata dei permessi lo accettano solo per
    un permesso di un'area che ha un'entità `IHasFir` (le impara il catalogo all'avvio: `LearnAreasWithAFir`, `IsOfAnAreaWithAFir`).
  - **Con `firStaffScope: own`** ogni permesso che dà porta il FIR della posizione (`EffectivePermission.Fir`; nel claim
    `Nome:DIP@#FIR`, letto chiuso da chi non conosce il FIR) e raggiunge solo le righe `IHasFir` di quel FIR: nell'unico handler, nel
    guardiano (una riga che cambia FIR chiede `Edit` sui due) e nella lista generata (solo con i permessi che sono la sua lettura). Con
    `all` è del dipartimento come ogni grant. Il personale dei dipartimenti non è mai fermato dal FIR: la regola di prima è tolta.
  - Un claim con un dipartimento illeggibile non vale più niente (era «ogni dipartimento»).
  - I test: `FirTeamPermissionTests` (spina dorsale, 6), `FirTeamPermissionRulesTests` (unità, 11), `grants/firTeam.test.ts`, lo smoke
    `permissions-fir-team.spec.ts`.
- **Che cosa deve sapere A11b** (il modulo):
  - In `config/division.json` due righe al team del FIR — `Training.View` e `Training.Assign`, livelli `Coordinator` e `Assistant`,
    `scope: TD` — e **`firStaffScope: own`** (risposta 2 di Carmine). Il training è già `IHasFir` e la sua area è `Training`: i grant
    passano. La pagina, l'assegnazione e `training.approvalQueue` seguono l'unico handler, `/staff/training` il motore; i training dei
    piloti restano di TC e TAC; un capo FIR assegna un training già accettato (non ha `Approve`).
  - ⚠️ **Ogni permesso di un'area implica il suo `View`** (il calcolo), sullo stesso FIR: un capo con `Assign` dal team legge i training
    del suo FIR anche senza il grant di `View`. E alla domanda senza riga l'handler gli dice sì: ogni lettore del modulo che non chiede
    l'handler sulla riga né passa dal motore (il percorso del trainee e i suoi ban, per esempio) va guardato uno per uno.
  - ⚠️ **Cambiare `firStaffScope` arriva a ogni capo al suo login dopo** (il calcolo lo legge al login): mettere `own` insieme ai grant.
  - ⚠️ **`PermissionHolder.Has` non passa il FIR** (dalla lettura del codice): un capo FIR non è mai «titolare» di una riga, quindi nessun
    digest né notifica «a chi può farlo» gli arriva. Se A11b ne vuole una, `Has` deve prendere il FIR: modifica del nucleo, una PR a sé.
  - ⚠️ **`/api/me` non porta il FIR**, com'è deciso: un bottone disegnato dai permessi di `/api/me` compare anche sulle righe degli altri
    FIR, e il server risponde 403. Ogni bottone su una riga del training passa dagli `actions` che l'handler ha risposto su quella riga.
  - Il calcolo, se non gli si dice `firStaffScope`, prende `own`, il lato che chiude; i chiamanti dell'hub passano quello della divisione.
- **Per chi scrive un test con i FIR**: l'host di `FirTeamPermissionTests` mostra come dire `own` e due FIR finti senza toccare i dati di
  riferimento condivisi (una directory dei FIR di prova); VID **790080–790089** sono di A11a.
- **Trovato, detto al revisore**: la regola del calcolo è più larga del suo commento («Edit implies View»); il vecchio calcolo delle
  sessioni di un grant a una posizione senza dipartimento avrebbe preso anche le posizioni HQ.
- **La fase dopo**: **A10c** (gli esami), da `m3/a10b-blocks-and-public-pages` con `main` dentro, in coda dopo #153; A11b, A12a e A12b dopo
  A10c, e A11b anche dopo l'unione di #159, perché usa il team del FIR. Questa sessione prepara il branch di A10c e la avvia.

### Che cosa ha lasciato A6c (26 settembre 2026, branch `m3/a6c-closed-suggestion`, PR #145)

- **Perché c'è**: la sessione di A6b l'ha trovata scrivendo lo smoke della richiesta (#144): nel **suggerimento chiuso** di `SchemaForm`
  (`suggestionsOnly`) chi scrive una parte del valore e poi clicca un'opzione si ritrova la casella con il valore di prima — vuota su
  una riga nuova. Vale per ogni campo chiuso dell'hub (menu, postazioni nascoste, postazione della richiesta, aerei dei tour).
  `dalberone` ha scelto una fase del nucleo a sé, come A4a.
- **Che cosa c'è** (nota `decisions/2026-09-26-il-suggerimento-chiuso-tiene-la-scelta.md`, **decisa** da Carmine il 27 settembre 2026
  come raccomandato — [la sua risposta](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/145#issuecomment-5855560813) — alla
  [domanda su #145](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/145#issuecomment-5849495355)):
  - in `Suggest` (`web/src/shared/forms/SchemaForm.tsx`) **la casella e la sua lista sono un campo solo**: la regola del campo chiuso
    («uscire con qualcosa che nessuno ha offerto rimette quello che c'era») vale quando il fuoco esce da tutte e due — dall'`onBlur`
    della casella, o dalla chiusura della lista quando il fuoco non è nella casella —; tornare nella casella dalla lista non ricomincia
    la ricerca; una scelta scrive `opened`. Nessuna schermata cambiata.
  - la spec nuova **`web/e2e/closed-suggestion.spec.ts`** (5 prove sull'indirizzo di una voce del menu, con l'API finta), che cade sul
    codice di `main`.
- **Che cosa deve sapere la fase dopo**:
  - **Dopo il merge una spec può scrivere per cercare in un campo chiuso** e poi cliccare l'opzione. Le spec di A6b
    (`web/e2e/training-request.spec.ts`, `web/e2e/full/training-request.spec.ts`) scelgono la postazione dall'elenco **apposta**, e sono
    in coda su #144: non si toccano in questa PR; potranno scrivere una parte del nominativo in una fase dopo il merge.
  - ⚠️ **Non si annulla la pressione su una lista in un popover** (`onMouseDown` con `preventDefault`) per tenere il fuoco altrove:
    **Chromium non trascina più la barra di scorrimento** di quella lista (misurato: 611 px senza, 0 con). Era la correzione proposta da
    A6b, fatta per prima e scartata. E **Playwright headless nasconde le barre** (`--hide-scrollbars`): una prova che ne preme una le
    riaccende con `test.use({ launchOptions: { ignoreDefaultArgs: ['--hide-scrollbars'] } })` in cima al file.
  - ⚠️ **Dopo una pressione sulla lista il fuoco sta nella lista** (come prima di A6c): un tasto scritto lì non va nella casella; le
    frecce e Invio sono di `cmdk`, e un clic sulla casella ci torna con la ricerca di prima.
  - Nessun VID e nessuno slug usati.
- **La PR non va in coda**: se #143 o #144 sono unite prima, `main` entra nel branch con un merge; i conflitti stanno in cima a questo
  file e nella tabella di `08`, e si tengono tutti i paragrafi.
- **Dopo la risposta e la revisione** (28 settembre 2026;
  [i rilievi del revisore](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/145#issuecomment-5855612725)):
  - la nota è decisa, con la risposta e il link; lo dicono anche `08` e questo file;
  - `main` è entrato nel branch con un merge (b468d24: #142–#172, fra cui A6a, A3b e #152), con i conflitti solo in `08` e qui,
    risolti tenendo tutto; build e suite rifatte (i numeri sono in `08`, A6c, «Le correzioni della revisione»);
  - ⚠️ **il Tab dalla lista non esce dalla pagina**: finché la lista è aperta, Radix mette uno
    `<span data-radix-focus-guard tabindex="0">` in fondo a `<body>`, dopo il portale. Il Tab ci arriva, e la lista si chiude con la
    regola (misurato; nota §3.2);
  - ⚠️ **la scelta da sola tastiera** (scrivere, freccia giù, Invio) non funziona, neanche su `main`: la casella sta fuori dalla radice
    di `cmdk`, e per questo Invio in una casella chiusa invia il form (conta per #144). La prende il maintainer come seguito;
  - il punto 4 (la scelta scritta in `opened`) lo tiene solo il tempo: lo vede la prova del menu di `back-office.spec.ts`, che torna
    nella casella mentre la lista si sta chiudendo.

### Che cosa ha lasciato A3b (26 settembre 2026, branch `m3/a3b-entrusted-rows`, PR #135)

- **Che cosa c'è** (nota `decisions/2026-09-26-le-righe-affidate-a-chi-scrive.md`, caso c, decisa da Carmine sulla #135):
  - **`IHasAssignee { int? AssigneeVid }`** (`Core/Division/DomainContracts.cs`): la riga dice a chi è affidata.
  - **`PermissionDescriptor.OnlyForAssignee`** (`CorePermissions.cs`; `PermissionCatalog.IsOnlyForAssignee`, `EditOf`): un permesso
    segnato raggiunge una riga solo se è affidata a chi chiede. Su ogni altra riga vale come `{Area}.Edit`: nell'unico handler
    (`HubAuthorization.cs`) e nel guardiano (`HubSaveChangesInterceptor.IsWrittenWithAnAlternative`) allo stesso modo. Senza riga
    resta `HasAny`. Il catalogo rifiuta il segno sul permesso `View` dell'area, l'unico che sa riconoscere fra quelli che leggono.
  - **Nel guardiano**, per un'alternativa segnata:
    - in modifica la riga è di chi scrive prima e dopo, quindi non si passa e non si prende;
    - alla creazione (`AlsoOnCreation`) la riga nuova è di chi la crea;
    - con **`AlsoOnDeletion`**, nuovo su `[AlsoWrittenWith]`, la toglie chi l'aveva. Conta solo per un permesso segnato.
  - **All'avvio**, prima delle migrazioni, `HubPipeline.InitializeAsync` chiama `PermissionCatalog.VerifyAlternatives` sul modello di
    ogni contesto. Rifiuta `AlsoOnDeletion` su un permesso non segnato, e un permesso segnato su un'entità che non è `IHasAssignee`
    (i rilievi del revisore).
  - Nel modulo di prova: `SampleRecord.AssigneeVid` (migrazione `AddSampleAssignee`) e `Sample.Manage`, segnato e anche
    `DeniedToStakeholder`. I test: `AssignedRowPermissionTests` (sei, integrazione) e `AssigneePermissionTests` (nove, unità).
- **Che cosa deve sapere la fase dopo**:
  - **A10**: la riga degli esami si dichiara così.
    - `trn_exams` porta `[AlsoWrittenWith(TrainingPermissions.ManageExams, AlsoOnCreation = true, AlsoOnDeletion = true)]` e
      `IHasAssignee` (`int? IHasAssignee.AssigneeVid => ExaminerVid;`).
    - Nel catalogo del modulo, `ManageExams` ha `OnlyForAssignee: true`, e anche `DeniedToStakeholder: true` se l'esame dice il suo
      candidato con `IHasStakeholder`.
    - `MapCrud` ha `WritePolicy = Training.ManageExams`, senza `DeletePolicy`.
    - ⚠️ **Un TA deve vedere quali esami sono i suoi** (il revisore): la lista la leggono tutti con `Training.View`, e un'azione
      sull'esame di un altro è un 403.
  - **A7**: **Carmine ha scelto la stessa regola per il trainer** (risposta 2 sulla #135).
    - Il training dichiara il suo trainer con `IHasAssignee`, e `Training.Conduct` è `OnlyForAssignee`.
    - Niente grant con scope per assegnazione, e niente job notturno.
    - A7 lo registra nella sua nota, in `08` e in `07`, perché corregge la n.1 del design, nella stessa PR.
    - ⚠️ `Training.Conduct` va dato per posizione ai TA1–9 e ai T01–T99, perché i `positionGrants` di A4 danno ai trainer solo `View`
      (R.7: si assegna chiunque sia staff del training). Lo aggiunge A7: una voce nuova del seme si applica al primo avvio che la trova.
  - ⚠️ **Un'entità con un'alternativa segnata che non è `IHasAssignee` fa fallire l'avvio**, anche quello dei test d'integrazione.
    Lo stesso per `AlsoOnDeletion` su un permesso non segnato. Il guardiano prende `PermissionCatalog` nel costruttore, dal contenitore.
- ⚠️ **Trovato, per il revisore**: il guardiano esclude l'interessato da ogni alternativa, l'handler solo dai permessi
  `DeniedToStakeholder`, e così è da A3. Per un'alternativa non segnata così, l'endpoint lascia passare e la rete ferma chi non ha
  `Edit`. Per questo `Sample.Manage` è anche `DeniedToStakeholder`, e la nota §3.6 lo chiede agli esami. Il revisore l'ha annotato:
  per A10 la risposta è quella della nota.
- **In pari con `main` il 27 settembre**, come il revisore ha chiesto prima del merge.
  - `main` era 68 commit più avanti (A4, A4a, A5, A6a, #152 e gli altri) ed è entrato con un merge.
  - L'unico conflitto era in questo file, risolto tenendo tutti i paragrafi; `08` si è unito da solo.
  - Build e suite rifatte (i numeri sono in `08`, A3b, «Com'è andata»).
  - Il messaggio di `PermissionCatalog` ora dice esattamente che cosa controlla: il permesso `View` dell'area.

### Che cosa ha lasciato A6a (26 settembre 2026, branch `m3/a6a-request-server`, PR #143)

- **Che cosa c'è** (codice del modulo, nessun file del nucleo, nessuna nota nuova; A6 divisa in apertura, scritto in `08`):
  - **Il training, intero**: `trn_trainings` (`src/IvaoHub.Modules.Training/Training.cs`, **alla radice del modulo**: una classe `Training`
    in un namespace sotto quello del modulo sarebbe nascosta dal namespace `IvaoHub.Modules.Training`) con tutte le colonne di design §1.2
    e `reminded_at` (§5.3), `TrainingState` e `TrainingRejection`; **`trn_bans`** (`Bans/TraineeBan.cs`, con `Holds(at)`), solo la tabella
    e la lettura. Migrazione `AddTrainings`, solo additiva.
  - **Una richiesta aperta per percorso anche nel database**: `open_kind`, scritta dal getter come `is_disputed` dei PIREP, in un indice
    unico con `trainee_vid`.
  - **Le regole** in funzioni pure (`Requests/RequestRules.cs`: `Standing`, `WaitUntil`, `IsMockExam`, `MinimumHours`, `EndedAt`) e
    **`ITheoryExamSource`** (`Requests/ITheoryExamSource.cs`), oggi la dichiarazione del trainee (`TraineeDeclaration`, `TryAddScoped`).
  - **Gli endpoint del trainee**, `/api/training/mine` (`Requests/RequestEndpoints.cs`, `Requests/TrainingRequests.cs`): `GET` la pagina
    (`MyTrainingDto`: VID, nome, `asksTheory`, `theoryExamUrl`, `paths` — per percorso rating e ore, `next`, `isMockExam`, `asksPosition`,
    `positions`, `refusal` con `bannedUntil`, `openTrainingId`, `waitUntil`, `minimumHours` —, `trainings`); `POST` la richiesta
    (`TrainingRequestWriteDto`: `kind`, `rating`, `position`, `availabilityText`, `notesText`, `theoryPassed`); `GET /{id}`; `POST
    /{id}/cancel` con la `rowVersion`. Il DTO del trainee (`TraineeTrainingDto`) non ha campi dello staff; nessun DTO ha l'email. Tutto già
    in `web/src/shared/api/schema.d.ts`.
  - **La mail** `training.requestReceived` (`TrainingNotifications`) e gli errori `training:errors.request*`, in `training.json`.
  - **I test**: `TrainingRequestRulesTests` (unità, su un vocabolario di prova), `TrainingRequestTests` (integrazione, VID 790017–790021).
- **Che cosa deve sapere la fase dopo**:
  - **A6b** (le pagine): il server c'è tutto, e le due pagine leggono **un endpoint solo**, `GET /api/training/mine`. ⚠️ **Un rifiuto è una
    chiave nuda** (`refusal`; nel `POST` i `ProblemDetails` sul campo `kind`): **fino a quando, la soglia e le ore la pagina le prende dal
    `GET`** — `bannedUntil` (vuoto, con il rifiuto del ban, vuol dire «finché qualcuno non lo toglie»), `waitUntil`, `minimumHours`,
    `hours` — e le scrive accanto al messaggio. La richiesta **rimanda `next.number`** in `rating`; `asksPosition` dice se si sceglie una
    postazione fra `positions`. Con `asksTheory` la finestra della domanda, con il link `theoryExamUrl` quando c'è; la risposta va in
    `theoryPassed`, e **il «no» risponde 201** con il training `Rejected` / `TheoryNotPassed`: il messaggio a schermo lo dice. In
    `/training/mine` i `trainings` dal più nuovo, «Annulla» solo su `Requested` con la sua `rowVersion` (409 se vecchia), l'attesa residua
    è `waitUntil` del percorso, «pronto per…» sono `readyForMockExam` e `readyForExam`.
  - ⚠️ **Un training non si elimina mai**: una spec che ne chiede uno lo **annulla** nel `finally`, e all'inizio annulla quelli che una
    corsa di prima ha lasciato `Requested`; un rifiuto e un annullamento non fanno aspettare.
  - **Il banco**: a `?as=pilot` (VID 999002, AS3 e FS3) si propongono il primo rating ATC e il primo pilota con un training pratico; le
    postazioni sono quelle delle fixture. La sua casella (`bench-pilot@bench.test`) riceve la mail del «sì».
  - **A7**: lo staff legge il training con `Training.View`, con un DTO suo; la funzione unica che toglie i campi riservati al trainee della
    riga è di A9. Il grant del trainer ha lo scope `Training.ScopeOf(id)`.
  - **A8**: `reminded_at` c'è; la fine di una sessione non è una colonna del training (sta nella disponibilità scelta, `chosen_slot_id`).
  - ⚠️ **Il filtro globale nasconde un training a chi non è entrato**: nei test, pulire e contare vogliono `IgnoreQueryFilters()`.
  - VID: il prossimo libero è **790022** (A3b usa 790040–790044 e 790050–790051).
- **La coda si è sciolta prima della PR**: #140 è stata unita alle 18:44; `main` è entrato nel branch con un merge (da90c3e) che non porta
  file del modulo, tutto è stato rifatto sul merge, e la PR è nata verso `main` senza coda.

### Che cosa ha lasciato A5 (26 settembre 2026, branch `m3/a5-sheet-items`, PR #140)

- **Che cosa c'è** (codice del modulo, nessun file del nucleo, nessuna nota nuova):
  - **Le voci della scheda di valutazione**: `trn_sheet_items` (`src/IvaoHub.Modules.Training/Sheets/SheetItem.cs`, migrazione
    `AddSheetItems`, solo additiva) — percorso, rating, sezione `Practice` (voto 1–5) o `Theory` (fatto, non fatto, da migliorare),
    titolo `Localized<string>` in ogni lingua della divisione, ordine (`sort`), attiva —, del dipartimento base, `IAuditable`,
    `[Audited]`. `RatingKind` e `SheetSection` come testo in `ConfigureModuleConventions`.
  - **`/api/training/sheet-items`** (`Sheets/SheetItemEndpoints.cs`), `MapCrud` letto e scritto con `Training.ManageSheets`, con
    `filter[kind]`, `filter[rating]`, l'ordine della scheda e la sigla del rating dal vocabolario (`ratingShortName`). Le regole in
    `SheetItemWriteDtoValidator`. **I validatori del modulo ora sono registrati per il motore** (`AddValidatorsFromAssemblyContaining`).
  - **«È usata?»**: `ISheetItemReports`, che oggi risponde no (`NoSheetItemReports`); una voce usata non si elimina
    (`training:errors.sheetItemUsed` sul campo `id`), si spegne.
  - **Il front end**: `/staff/training/sheets` e `/staff/training/sheets/$id` (`screens/sheets.tsx`), la voce «Scheda di valutazione»
    nella barra dello staff; le scelte dei rating sono un aiuto solo, `screens/ratings.ts`, con la chiave `ratingChoice` (era
    `settings.ratingChoice`) e `fromRatingChoice` in `schemas.ts`.
  - **I test**: `TrainingSheetItemTests` (unità), `TrainingSheetTests` (integrazione, VID 790014–790016), `schemas.test.ts`,
    `web/e2e/full/training-sheets.spec.ts`.
- **Che cosa deve sapere la fase dopo**:
  - **A6** (la richiesta): il secondo `DbSet` e la sua migrazione, in fila dopo `AddSheetItems`; i validatori per il motore **sono già
    registrati** (non si registrano due volte). Gli enum nuovi (lo stato, il rifiuto) vanno in `ConfigureModuleConventions` accanto a
    `RatingKind`. Le scelte dei rating per il form della richiesta: `ratingOptions` in `screens/ratings.ts`.
  - **A9** (dopo la sessione) mette al posto di `NoSheetItemReports` la risposta delle schede compilate, nella stessa registrazione
    (`TryAddScoped`), come T11 dei tour con `PirepTourReports`; una scheda nuova si fa con le voci **attive** del percorso e del rating
    del training, nell'ordine di `sort`, e fotografa titolo e sezione.
  - ⚠️ **Chi scrive le voci ha `ManageSheets` e `Edit`**: il guardiano chiede `Training.Edit` a ogni riga dello staff (design §3.1),
    come `Tours.Edit` a chi ha `Tours.ManageAircraft`. Con `ManageSheets` da solo la schermata si apre e il salvataggio risponde 403.
    Nella divisione i due vanno insieme (TC, TAC); un test d'integrazione lo fissa.
  - ⚠️ **La lista si legge con `ManageSheets`**, non con `View`: TA e trainer non vedono la voce nel menu; il trainer leggerà le voci
    dalla scheda del suo training (A9).
  - ⚠️ **`SchemaForm` legge i suoi valori una volta sola**: un form che calcola un valore iniziale da un'altra query aspetta una lettura
    fatta dopo l'apertura (`isFetchedAfterMount`), e un form su una riga si ridisegna alla sua versione (`key={rowVersion}`).
  - VID: il prossimo libero è **790017** (A3b usa 790040–790044 e 790050–790051).
- **La coda si è sciolta durante A5**: #139 (A4) e #138 del maintainer sono state unite alle 12:22, a PR di A5 già aperta in bozza.
  `main` è entrato nel branch con un merge (48a1219) che porta #138, e build e test sono stati rifatti sul merge; tolti `(after #139)` e
  `Queued after #139.`. A6 va in coda dopo #140.

### Che cosa ha lasciato A4 (26 settembre 2026, branch `m3/a4-training-skeleton`, PR #139)

- **Che cosa c'è** (codice del modulo; una nota sola, **Decisa**, `2026-09-26-gli-esami-li-inserisce-chi-esamina`, per gli esami
  senza i trainer, che rimanda a quella del maintainer, `2026-09-26-gli-esaminatori`; lo scostamento grande è la fase del nucleo A4a,
  trovata qui):
  - **Il modulo**: `src/IvaoHub.Modules.Training/` — `TrainingModule` (chiave `training`, la voce `/staff/training/settings`, il
    segmento riservato `training`), `TrainingPermissions` (i nove di design §3.1), `Data/TrainingDbContext` con l'`Initial` senza
    tabelle del modulo, `Settings/TrainingSettings` (i dieci campi di §1.6, `TrainingSettingsValidator` sul vocabolario,
    `TrainingSettingsSaveValidator` che legge anche i tipi del calendario e le postazioni), `Reference/TrainingReference` (i rating con
    un training pratico e le loro postazioni, chiesti al nucleo) con `/api/training/ratings` e `/api/training/positions`.
  - **Il front end**: `web/src/modules/training/` — manifest, `screens/settings.tsx` (il form generato), `schemas.ts`, `api.ts`, le lingue.
  - **La configurazione**: i nove `positionGrants` del TD in `config/division.json` e nell'esempio. ⚠️ **`ManageExams` a TC, TAC e
    TA1–9, non ai trainer**, a differenza del design §3.2: **un esame si assegna solo a un esaminatore, e gli esaminatori sono HQ, TC,
    TAC e i TA, mai un trainer** (`dalberone`, 26 settembre 2026), e la regola n.10 di Carmine è che l'esame lo inserisce chi ce l'ha.
    **Vale anche per A3b** (la domanda «anche a un trainer?» della sua sezione ha già la risposta: no) **e per A10**. Cambiava
    l'elenco scritto nella n.10, quindi ha una nota sua, **decisa** da Carmine
    ([«yes» sulla issue #134](https://github.com/SkyMistery/Ivao-Italy-Hub/issues/134#issuecomment-5844363804)), come la sua nota
    `2026-09-26-gli-esaminatori` (#136), che corregge la n.10.
  - **I test**: `TrainingSettingsTests` e `TrainingArchitectureTests` (unità), `TrainingSkeletonTests` e `TrainingXxDivisionTests`
    (integrazione, VID 790009–790013), `schemas.test.ts`, `web/e2e/full/training-skeleton.spec.ts`.
- **Che cosa deve sapere la fase dopo**:
  - **A5** (le voci della scheda): il primo `DbSet` e la migrazione `AddSheetItems`; i rating del form sono `TrainingReference.Ratings`
    (o `/api/training/ratings`); gli enum come testo vanno in `ConfigureModuleConventions`, come nei tour. ⚠️ **I validatori per il
    motore CRUD non sono registrati**: A4 non ne ha, e le impostazioni li creano da sé; con il primo `MapCrud` serve
    `services.AddValidatorsFromAssemblyContaining<TrainingModule>(includeInternalTypes: true)`, come nei tour.
  - **A6** (la richiesta): le impostazioni si leggono con `ModuleSettingsStore.GetAsync<TrainingSettings>(TrainingModule.ModuleKey)`;
    `MaxResponseDays` e `TheoryExamUrl` possono essere `null`; le postazioni da offrire sono quelle del rating proposto
    (`IAtcPositionDirectory.ForRatingAsync`) meno `HiddenPositions`. `TrainingReference.PositionsAsync` le dà tutte, per le impostazioni.
  - ⚠️ **Il controllo di architettura del modulo morde**: nel codice di `src/IvaoHub.Modules.Training/` e `web/src/modules/training/`
    niente «IVAO» (nemmeno nei commenti: si scrive «la rete»), nessun numero accanto a un rating, nessun «ADC» o «TWR» in una stringa,
    nessun client HTTP. I test del modulo possono costruire rating loro (sono esclusi).
  - ⚠️ **Una chiave del training in C# si legge senza namespace solo se nessun altro modulo la dichiara** (A4a): `nav.section`,
    `nav.settings`, `settings.title`, `settings.description`, `settings.saved` sono anche dei tour. Le mail di A6 vanno in
    `mail.training.<tipo>`.
  - ⚠️ **Un errore su una riga di una lista** si nomina con il campo della riga (`minimumHours[0].rating`), o il form non lo mostra.
  - ⚠️ **`web/e2e/address.spec.ts`** va su `/training/team`: nessuna rotta del modulo deve prendere ogni `/training/…`.
- **Il passo della coda è fatto**: #133 è stata unita alle 11:51, `main` è entrato nel branch con un merge che non porta file (l'albero,
  122c54e, è quello su cui sono girate tutte le suite), e #139 è pronta. Se prima di #139 viene unita **#138** del maintainer, il suo
  test di architettura legge il C# del training: le chiavi sono già tutte `training:…`.
- ⚠️ **Un banco e2e locale che ha già girato con il seme di prima** (esami anche ai trainer) lo tiene, perché un seme si applica una
  volta sola: prima di `pnpm e2e:full`, `DROP DATABASE ivaohub_e2e; CREATE DATABASE ivaohub_e2e;` (quello di questo worktree è stato
  ricreato il 26 settembre). La CI parte sempre da un banco nuovo.

### Che cosa ha lasciato A4a (26 settembre 2026, branch `m3/a4a-module-locales`, PR #133)

- **Perché c'è**: la sessione di A4 ha scritto lo scheletro del modulo, e al primo test d'integrazione l'hub non è partito:
  `LocaleCatalog`, il catalogo delle lingue del server, appiattisce tutti i file di una lingua in un solo dizionario e rifiuta una
  chiave dichiarata due volte, e con due moduli si ripetono per forza `_source` (lo scrive `pnpm i18n:sync` in ogni copia) e
  `nav.section` (lo esige la barra dello staff). `dalberone` ha scelto di fare subito la fase del nucleo, a sé, prima di A4.
- **Che cosa c'è** (nota `decisions/2026-09-26-le-parole-di-piu-moduli.md`, **Decisa**: Carmine ha risposto sì in un
  [commento su #133](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/133#issuecomment-5844250303)):
  `LocaleCatalog` salta `_source` e la usa per riconoscere il file di un modulo; tiene le chiavi di un modulo anche con il namespace
  (`training:nav.section`); senza namespace, come prima, quelle che un solo modulo dichiara; una chiave di due moduli solo con il
  namespace; i doppioni che toccano il nucleo ancora rifiutati. Il test nuovo `LocaleCatalogModuleTests` scrive i suoi file di lingua.
- **Che cosa deve sapere la fase dopo (A4)**:
  - **Il codice di A4 è su `m3/a4-training-skeleton`**, spinto, senza PR: unisce questo branch, rifà build e **tutti** i test, e apre
    la sua PR in coda dopo #133.
  - ⚠️ **In C# una chiave di un modulo si chiede con il namespace**, `training:<chiave>`: lo impone **#138** (bozza del maintainer, in
    coda dopo #133, nota `2026-09-26-le-chiavi-dei-moduli-con-il-namespace`), con un test di architettura che rifiuta una chiave di
    modulo scritta nuda in `src/` e una con il namespace che il file di lingua del modulo non dichiara. Le chiavi del nucleo e le mail
    dei tipi di notifica (`mail.{tipo}`) restano nude.
  - ⚠️ **La trappola del fallback senza namespace** (revisore, rilievo 2 su #133): una chiave che un modulo legge nuda in C# **smette
    di rispondere, in silenzio**, quando un altro modulo la dichiara — `LocaleCatalog.Resolve` risponde con la chiave stessa. #138 la
    chiude per i tour.

### Che cosa ha lasciato A3 (25 settembre 2026, branch `m3/a3-alternative-write-permissions`, PR #131)

- **Che cosa c'è** (nota `decisions/2026-09-25-i-permessi-alternativi-e-la-creazione.md`, scelta tecnica; una domanda per A10 posta
  in anticipo, §3.5):
  - **`[AlsoWrittenWith]` si ripete** (`Core/Division/DomainContracts.cs`), e il guardiano di `HubSaveChangesInterceptor` prova ogni
    alternativa (`IsWrittenWithAnAlternative`): **ne basta una**, ognuna come prima — sul dipartimento della riga con lo scope della
    riga, mai per l'interessato, senza spostare la riga.
  - **`AlsoOnCreation = true`** segna un'alternativa che vale **anche alla creazione**: senza scope (conta chi la tiene sul
    dipartimento, non su una riga), su almeno un dipartimento della riga come `Edit`, mai per una riga su chi scrive. **Nessuna
    alternativa elimina**: resta di `Edit`.
  - Nel modulo di prova `SampleRecord` (`smp_records`, migrazione del solo contesto di prova), il permesso `Sample.Record`, e i cinque
    test della spina dorsale `AlternativeWritePermissionTests`.
- **Che cosa deve sapere la fase dopo**:
  - **A7** dichiara sul training `[AlsoWrittenWith(...)]` per `Approve`, `Assign` e `Conduct`, **senza** `AlsoOnCreation` (il
    training lo crea il trainee, `ISubmittedByMembers`). Lo scope che il training dichiara e quello del grant del trainer
    (`ModuleGrants`) sono la stessa stringa, `training:training:{id}`, confrontata così com'è.
  - **A10**: `trn_exams` con `[AlsoWrittenWith(TrainingPermissions.ManageExams, AlsoOnCreation = true)]`, e `MapCrud` con
    `WritePolicy = Training.ManageExams`, perché il motore chiede il permesso all'handler sulla riga prima del guardiano. ⚠️ **Eliminare
    un esame resta di `Edit`**: con `DeletePolicy = Training.Edit` lo eliminano TC e TAC; se deve poterlo eliminare chi l'ha inserito,
    è un'altra estensione del nucleo. **I fatti del TD** (tre commenti su #131, nota §3.5): gli esami si gestiscono su IVAO, all'hub
    servono solo per il calendario (niente «annullato»); dall'hub un esame lo tolgono **HQ, TC, TAC e il TA a cui è assegnato**,
    nessun altro, ed è solo una rimozione «cosmetica»; la postazione la decide chi ha l'esame. **Carmine ha scelto la 4, la regola del
    TD** ([commento su #131](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/131#issuecomment-5840224757)): la regola nel nucleo
    arriva con **A3b**, una fase del nucleo con la sua nota (caso c, «Proposta») e i suoi test della spina dorsale, **prima di A10**
    (in `08`). **Chiarito da `dalberone` il 26 settembre**: un esame si assegna **solo a un esaminatore**, e gli esaminatori sono
    **solo HQ, TC, TAC e i TA (TA1–9)**, mai i trainer; la conseguenza sui `positionGrants` del TD è di A4.
  - ⚠️ **Un test di permessi con scope non usa `TestCurrentUser`**: il suo `Has` non passa lo scope, e tiene solo i permessi del nucleo.
    `AlternativeWritePermissionTests.AsAsync` scrive senza endpoint con l'identità del cookie (`HubClaims.BuildIdentity`) letta dal vero
    `HttpContextCurrentUser`.
  - ⚠️ **Gli eseguibili xUnit non ricompilano**: dopo una modifica, `dotnet build` prima di lanciarli.
- ⚠️ **Trovato per il revisore**, non cambiato (è il comportamento di oggi, che A3 ripete per ogni alternativa): il guardiano guarda
  l'interessato e lo scope di una riga **solo dopo** la scrittura (nota §5). **Il revisore li ha confermati** ([revisione di A3 su
  #131](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/131#issuecomment-5839714140), «approvable as it is»): un compito di
  rafforzamento del maintainer, che non blocca questa fase.
- **`main` è andato avanti due volte durante A3**: la #130 di Carmine (il piano con M3), unita nel branch prima del primo push; e
  **la #129 (A2), unita alle 21:14**, entrata con un merge. L'unico conflitto era in cima a questo file: tenuti tutti e due i
  paragrafi, A3 sopra; `08` si è unito da solo. Build e test rifatti sul merge.

### Che cosa ha lasciato A2 (25 settembre 2026, branch `m3/a2-atc-positions`, PR #129)

- **Che cosa c'è** (nota `decisions/2026-09-25-le-postazioni-atc-e-il-tipo-exam.md`, scelta tecnica, nessuna domanda nuova):
  - **Le postazioni ATC del mondo** in `ref_ivao_atc_positions` (`Core/Ivao/IvaoAtcPosition.cs`: `Callsign` è la chiave,
    `PositionType`, `AirportIcao` per una postazione d'aeroporto, `CenterId` per un settore, `Name`, `RawJson` senza contorno;
    migrazione `AddAtcPositions` del nucleo), riempita da `RefDataSyncJob` ogni notte da `/v2/ATCPositions/all` e
    `/v2/subcenters/all`, **sempre con `mapType=regionMapPolygon`** (`IIvaoApiClient.GetAtcPositionsAsync`). Le due liste si
    rinfrescano e si potano ciascuna per conto suo, mai su una risposta vuota; un giro senza di loro è `partial`.
  - **La directory** `IAtcPositionDirectory.ForRatingAsync(Rating)` (`Core/Ivao/AtcPositionDirectory.cs`): le postazioni **della
    divisione** del tipo che il vocabolario dà al rating, ognuna `AtcPositionDto(Callsign, Name, AirportIcao, Fir)` — il FIR di una
    postazione d'aeroporto è quello del suo aeroporto —, per nominativo; nessuna per un rating senza tipo. **Le militari ci sono.**
  - **Il tipo `exam`** nel seme del calendario (rosso, `sort` 25, «Exam», «Esame»), e il seeder che **salta una chiave già scritta a
    mano** e la ricorda.
  - **Le fixture del banco** `atc-positions-world.json` e `subcenters-world.json` (LIRF, LIMC, LIBD, LFPG, LIBG; LIRR, LIMM, LIBB,
    LFFF), lette dal client delle fixture; lo strumento `--positions` chiede con `mapType`.
- **Che cosa deve sapere la fase dopo**:
  - **A6** chiede `ForRatingAsync(vocabolario.NextTraining(kind, rating))`, toglie `hiddenPositions` (A4) e copia `Callsign`,
    `AirportIcao` e `Fir` sul training (`position`, `airport_icao`, `fir`); alla richiesta il server ricontrolla che la postazione
    scelta sia nell'elenco. Il modulo **non nomina un tipo di postazione** (la risposta non lo porta). In Italia l'elenco è di 84
    torri, 59 avvicinamenti, 32 settori.
  - **A4**: `hiddenPositions` è ciò con cui il TD toglie le postazioni su cui non allena — **alcune militari sì e altre no**
    (`dalberone`, 25 settembre), e le 25 `_I_TWR`, che per IVAO sono torri. Un nominativo per voce.
  - **A10**: la chiave `exam` esiste in ogni installazione, anche in una già avviata prima di A2.
  - ⚠️ **Un'installazione che gira già riceve le postazioni al primo giro notturno** (03:15) dopo il rilascio: all'avvio la
    sincronizzazione parte solo senza centri. Lo stesso vale per il **banco e2e locale** (`ivaohub_e2e`), che sopravvive fra le corse:
    le spec che leggono le postazioni (da A6) vogliono un banco nuovo, `DROP DATABASE ivaohub_e2e; CREATE DATABASE ivaohub_e2e;`.
  - ⚠️ **`IIvaoApiClient.GetAtcPositionsAsync` ha un'implementazione predefinita** («nessuna»): un doppio di test che non la scrive
    risponde così, e il giro tiene lo snapshot. Un test di A6 che vuole postazioni usa il client delle fixture.
- ⚠️ **#125 è stata unita durante A2** (19:18): questa sessione, con il permesso di `dalberone`, ha fatto il passo della coda di A1 —
  `main` unito in `m3/a1-ratings-and-hours` insieme a ciò che il revisore aveva chiesto su #128 (le risposte di Carmine su #125 in
  `08`, sotto A0 e A10; la frase sul tipo di postazione unico, sotto A1) —, ha rifatto i test, e ha passato #128 a pronta. Il branch
  locale del worktree di A1 (`vigorous-dijkstra-d64442`) resta indietro rispetto a `origin`. **Alle 20:07 anche #128 è stata unita**:
  `main` è entrato qui con un merge che non porta file (l'albero è quello su cui sono girati i test), e #129 è passata a pronta.
- ⚠️ **Trovato per il maintainer**: il seme dei template e delle pagine ha lo stesso difetto che aveva quello dei tipi (una chiave
  nuova su uno slug già scritto a mano farebbe fallire l'avvio sull'indice univoco). Detto al revisore nella PR.

### Che cosa ha lasciato A1 (25 settembre 2026, branch `m3/a1-ratings-and-hours`, PR #128, in coda dopo #125)

- **Che cosa c'è** (nota `decisions/2026-09-25-le-ore-e-il-vocabolario-dei-rating.md`, scelta tecnica, nessuna domanda nuova):
  - **Le ore di connessione** in `hub_users.hours_atc` e `hours_pilot` (`decimal(9,2)`, **in ore**; migrazione `AddConnectionHours`
    del nucleo), lette da `IvaoUserProfileReader` e scritte da `UserSyncService` a ogni login, accanto ai rating. `HubUser.HoursAtc`,
    `HoursPilot`. Si aggiornano **solo al login**, e restano le ultime se IVAO non le manda.
  - **Il vocabolario dei rating**, `src/IvaoHub.Core/Ivao/RatingVocabulary.cs`: `RatingKind` (`Atc`, `Pilot`), `Rating` (numero di
    IVAO, sigla, `HasPracticalTraining`, `PositionType`, `NameKey`), `RatingVocabulary` con `Ladder`, `Find`, `NextTraining`,
    `IsAtLeast`; i dati di IVAO in `IvaoRatings.Vocabulary`, **registrato come singleton** (si inietta `RatingVocabulary`). I nomi sono
    `ratings.Atc.ADC`, `ratings.Pilot.PP` in `locales/*/common.json`, in inglese anche in italiano.
  - **`RatingBadge`** in `web/src/shared/ui/badges.tsx` (esportato da `shared/ui`): `<RatingBadge kind="Atc" shortName="ADC" />`,
    la sigla come testo e il nome come `title`. Prende la **sigla**, non il numero: il server la manda dal vocabolario.
  - **Il banco**: `/e2e/signin?as=pilot` è anche **il trainee** (VID 999002: AS3 = 4, FS3 = 4, 120 ore ATC, 150 pilota, casella
    `bench-pilot@bench.test`); **`?as=trainer`** è nuovo (VID 999004, `IT-T01`, SEC = 8, ATP = 8, casella `bench-trainer@bench.test`).
  - **Le fixture**: `users-me-790001.json` (il profilo vero, anonimizzato), `atc-positions-sample.json` e `subcenters-sample.json`;
    lo strumento `tools/record-ivao-fixtures.mjs --me <asVid>` e `--positions <name> <ICAO...>`.
- **Che cosa deve sapere la fase dopo**:
  - Il modulo **non scrive numeri di rating**: chiede a `RatingVocabulary` (iniettato) `NextTraining(kind, rating)` per il rating
    proposto (A6), `IsAtLeast(kind, trainer, rating)` per il trainer adatto (A7), `Find(kind, rating)?.PositionType` per il tipo di
    postazione (A2, A6). I **test del modulo** costruiscono un `new RatingVocabulary([...])` loro, con rating che non sono di IVAO
    (design §10): la classe lo permette, e `RatingVocabularyTests.AVocabularyOfAnotherNetworkAnswersTheSameQuestions` lo mostra.
  - Le ore si leggono da `HubDbContext.Users` (`HoursAtc`, `HoursPilot`), come i rating: la richiesta (A6) le copia in
    `trainee_hours_at_request`. Null vuol dire «IVAO non ha detto niente» (un membro che non è rientrato dopo A1), non zero.
  - **Per A2**: nessuna postazione di IVAO porta un rating, quindi la directory filtra per `PositionType` del vocabolario. ⚠️ I settori
    `CTR` e `FSS` stanno **solo in `/v2/subcenters/all`**, non in `/v2/ATCPositions/all`; tutti e due rispondono con il mondo intero
    (21 e 32 MB), e il secondo ha chiuso la connessione a metà due volte su tre il 25 settembre. Le fixture di A1 sono un campione
    (LIRF, LIMC, LIRR): A2 decide se servono quelle della divisione.
- ⚠️ **`IvaoUserProfileReaderTests.RealShape` ha una forma di `hours` inventata** (un oggetto): è un test del maintainer, non toccato;
  detto al revisore.
- ⚠️ **`pnpm e2e:full` in un worktree nuovo**: serve il browser di Playwright (`pnpm exec playwright install chromium`, scaricato il
  25 settembre con il permesso di `dalberone`), e per le spec dei tour la mappa di base in `tiles/` (vedi sotto, A0).
- ⚠️ **Lo strumento `--me` ascolta sull'indirizzo di ritorno registrato** (`localhost:5173/auth/callback`): con Vite acceso non
  parte. L'hub di sviluppo della cartella principale è stato fermato per la misura, con il permesso di `dalberone`, e il database di
  sviluppo `ivaohub` ha già la migrazione `AddConnectionHours`.
- ~~⚠️ **`main` ha la #126 (T20c) che il branch non ha**~~ **Fatto il 25 settembre**: #125 è stata unita alle 19:18, e `main` (con
  #125, #126 e #127) è entrato nel branch con un merge; nello stesso commit, come ha chiesto il revisore su #128, le risposte di Carmine
  su #125 (in `08`, sotto A0 e A10) e la frase che dice che «un tipo di postazione per rating» sostituisce di proposito l'elenco della
  prima stesura del design (in `08`, sotto A1). Build e test rifatti, `(after #125)` tolto, la PR passata a pronta (`CONTRIBUTING.md`,
  «Phases in a queue»). Il merge l'ha fatto la sessione di A2, su un branch temporaneo spinto su `m3/a1-ratings-and-hours`: il branch
  locale del worktree di A1 resta indietro rispetto a `origin`.

### Che cosa ha lasciato A0 (25 settembre 2026, branch `m3/a0-decisions`, PR #125)

- **Che cosa c'è**: otto note in `decisions/`, una per decisione o per gruppo coerente di §12 del design, ognuna con il link
  al commento di Carmine che la decide — `chi-conduce-e-chi-scrive-un-training` (n.1, n.2, n.3, n.10),
  `le-note-riservate-e-il-trainee` (n.13, da sola come Carmine ha chiesto), `il-teorico-lo-dichiara-il-trainee` (n.15,
  n.12), `rating-e-postazioni-dal-nucleo` (n.5 e la correzione 3 della prima revisione), `che-cosa-resta-fuori-da-m3` (n.6,
  n.8, n.14), `il-training-in-pubblico` (n.4), `la-cancellazione-dei-dati-di-un-trainee` (n.7),
  `il-tempo-per-la-data-e-le-voci-della-scheda` (n.9, n.11); tutte con «Da portare nel piano», e insieme coprono la §14 del
  design. E `08-piano-implementazione-m3.md`: le regole di tutte le fasi e le fasi A0–A12, con A11 divisa in A11a (nucleo) e
  A11b, A12 in A12a (nucleo), A12b, A12c (l'archivio di PATS, solo se arrivano i codici) e A12d; «Com'è andata» di A0 è già
  scritto.
- **Che cosa deve sapere la fase dopo**: A1 è una PR del nucleo, e **porta la sua nota nuova**: le note di A0 registrano le
  decisioni, non la forma nel codice delle estensioni, e `core-guard` vuole una nota **aggiunta** in ogni PR che tocca il
  nucleo (A1, A2, A3, A11a, A12a). In A1 si misurano con il token vero `hours` del profilo e `/v2/ATCPositions/all`, prima di
  scrivere il legame postazione→rating.
- ⚠️ **`ErasureTests.TheColumnsThatNameAPersonAreTheOnesTheErasureKnows` non vede da solo le colonne del training** (il design
  §6.1 lo dava per scontato): legge due contesti scritti nel test. Si allarga in A12a, con la nota.
- ⚠️ **Quattro blocchi Data alzano due conteggi scritti in test condivisi** (`uiKit.test.ts`, `DataBlockEndToEndTests`):
  toccarli è nucleo, e la regola 3 lo vieta a chi non li ha scritti. La domanda va a Carmine **in apertura di A10**.
- ⚠️ **Il seme dei tipi del calendario** si ricorda chiave per chiave (`exam` arriva anche a un database avviato), ma non guarda
  se un tipo con la stessa chiave è già stato scritto a mano: da verificare in A2.
- ⚠️ **`ITheoryExamSource` non esiste nel codice** (il piano la nomina soltanto): nasce in A6, nel modulo. **Non c'è un test di
  architettura** «un modulo non nomina IVAO», e `ArchitectureTests.cs` è del maintainer: i controlli del training stanno in un
  file di test del modulo.
- ⚠️ **L'helper «persona cancellata»** è `memberName` nel front end dei tour (PR #123, piano 1.10): il collaboratore non lo
  tocca; A12a scrive quello del nucleo e lo dice al revisore.
- ⚠️ **Scostamenti dall'elenco del design §11**, scritti in `08`, A0: `trn_bans` nasce in A6 (la richiesta rifiuta un bannato)
  e la schermata resta in A10; la funzione del mock exam nasce in A6, la casella in A9; `trn_trainings` nasce intera in A6.
- ⚠️ **Un worktree non ha `tiles/`**: per `pnpm e2e:full` serve un hard link a `tiles/basemap.pmtiles` della cartella
  principale (trovato da Carmine in T20b).
- **Il dump di PATS non è più sulla macchina di `dalberone`**, come Carmine aveva chiesto: i database importati per il design
  non ci sono nel MariaDB locale (verificato il 25 settembre: c'è solo `ivaohub`), e i file del dump li ha cancellati
  `dalberone` (nemmeno nel cestino). Un import vero, se A12c si farà, parte da un dump nuovo, mai nel repository.
- ⚠️ **Le fasi vanno in coda** (PR #124, unita durante A0; nota `2026-09-25-le-fasi-in-coda`): la fase dopo parte dal branch
  della fase prima e la sua PR va verso `main` in bozza con `(after #N)`; una correzione sotto sale con un merge, mai un rebase;
  una fase che aspetta una risposta di Carmine non si mette in coda sopra la domanda. Il revisore pubblica da solo i suoi
  rilievi sulla PR; le decisioni e il merge restano di Carmine.

### Che cosa ha lasciato il design (25 settembre 2026, branch `m3/design`)

- **Che cosa c'è**: `07-design-m3.md`, bozza completa per la revisione. §R sono i requisiti del TD, raccolti a domande
  con `dalberone` in quattro giri e segnati uno per uno (d1–d4); §0–§11 il design sul modello di `05-design-m2.md`; §12 le
  15 domande per Carmine; §8 le dieci estensioni del nucleo (una non serve), ognuna una PR a sé prima del modulo; §6.1
  la cancellazione dei dati di una persona sul meccanismo di T20b (piano 1.08), già nel nucleo; §0.6
  gli scostamenti dal piano. ⚠️ **Il modulo non scrive numeri di rating né regole di IVAO** (revisione del 25
  settembre): stanno nel vocabolario del nucleo (n.4).
- **PATS**: il dump del 12 settembre 2026 l'ha fornito `dalberone` e **non entra nel repository**, né in una PR né in
  una fixture: contiene dati personali. `trainingNEW` è PATS vivo, `exam` gli esami, `training` il vecchio PATS fermo
  al 2020. Si importano senza errori su MariaDB 11.4.10; i codici numerici non hanno significato senza
  il codice PHP, che non abbiamo (§P, §7).
- **Che cosa deve sapere A0**: le 15 decisioni sono in §12, con i link ai due commenti di Carmine; ognuna va nella sua
  nota. Da tenere presenti: il trainer conduce con un grant con scope (n.1); `[AlsoWrittenWith]` ripetibile e anche alla
  creazione è un cambio del nucleo con **nota e test della spina dorsale**, prima del modulo (n.2, fase A3); i capi FIR in
  A11 (n.3); **la regola «il trainee non legge le note riservate del proprio training» ha una nota sua e un test
  d'integrazione** (n.13); **il feed del calendario non è in M3** e PATS resta acceso solo per quello fino a M6 (n.14);
  il teorico lo dichiara il trainee (n.15); le postazioni vengono da IVAO, legate al rating dal vocabolario del nucleo,
  e nelle impostazioni c'è solo `hiddenPositions` (n.5). In §14 del design c'è l'elenco di ciò che il revisore porta nel
  piano.
- ⚠️ **`[AlsoWrittenWith]` vale una volta per entità e solo in modifica** (`HubSaveChangesInterceptor`, la prima
  alternativa e basta): il training lo scrivono tre ruoli senza `Edit`, e un esame lo crea chi non ha `Edit`. È
  l'estensione n.7.
- ⚠️ **Un partecipante (`IHasParticipants`) riceve il `View` dell'area sulla riga**: sul training darebbe al trainee la
  risposta dello staff con le note riservate. Il design non lo usa (§1.1).
- ⚠️ **Scrivere un grant fa rientrare il titolare** (security stamp): il grant con scope del trainer costa un login a ogni
  assegnazione (§3.3, §12 n.1). I grant con scope viaggiano nel cookie: vanno tolti a training chiuso.
- ⚠️ **Una posizione FIR non porta permessi**, e un grant a una posizione si scrive per dipartimento: i capi FIR sono
  l'estensione n.2.
- ⚠️ **`ICurrentUser` non ha i rating**: si leggono da `HubDbContext.Users`, e si aggiornano solo al login. I personaggi
  del banco e2e non hanno rating (n.8).
- ⚠️ **Il seme dei tipi del calendario non ha `exam`**, anche se il piano §7 lo elenca (n.6); **il nucleo non programma
  mail nel futuro**: il promemoria è un job del modulo (§5.3).
- ⚠️ **L'API IVAO** (documentazione pubblica, vista il 25 settembre 2026) ha le postazioni ATC (`/v2/ATCPositions/all`) ma
  nessun endpoint di training o di esami; **rating e GCA** di un membro stanno nel suo profilo (`/v2/users/me`: `rating`,
  `gcas`), e l'hub legge solo i rating. I campi di `hours` e delle postazioni **vanno misurati** nella fase del nucleo.
