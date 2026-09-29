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

**Ultimo aggiornamento:** 29 settembre 2026 — **fase A12a** (nucleo: la persona cancellata e le colonne del training in
`ErasureTests`), sul branch `m3/a12a-deleted-person-core`, **PR #187** verso `main`, in bozza **in coda dopo #182** (A11b, in bozza in coda
dopo #181, A7b, dopo #178, A10c, dopo #153, A10b, dopo #151, A10a, dopo #150, A9b, dopo #149, A9a, dopo #148, A8b, dopo #147, A8a: **#146,
A7, è unita** il 29 settembre, dopo #144, A6b). **Per ora solo la nota** (`decisions/2026-09-29-la-persona-cancellata-nel-nucleo.md`,
**«Proposta»**) **e due domande a Carmine**: **il codice aspetta le risposte**. Il branch è nato dalla cima della coda (0b62481, preparato
dalla sessione di A11b) e ha `main` a 47e2f70; con il `main` di oggi — **#146**, **#184** (il piano di M4, E0), **#185** e **#186** (la
versione 0.4.0, tag `v0.4.0`) — si unisce senza conflitti, e la PR fa girare `build-test`. `main` entra in ogni branch al suo passo della
coda, non prima. La **sessione master** di Carmine (nota `2026-09-26-la-sessione-master`, `CLAUDE.md` §0) unisce sul via di Carmine e, se
un branch del collaboratore va rimesso in pari con `main`, lo chiede sulla PR senza spingerci niente. **A3 (#131), A4a (#133), A4 (#139),
A5 (#140), A6a (#143), A3b (#135), A6c (#145), A11a (#159), A6b (#144) e A7 (#146) sono unite**, e con loro **#152** del maintainer
(`Refusals` nel nucleo), **#177** (la tastiera del suggerimento) e **#183** (il piano 1.24 e il design di M4). **A10 è divisa in tre**
(`08`, A10): **A10a** (#151), **A10b** (#153) e **A10c** (#178); **A11 in due**, A11a (unita) e **A11b** (#182); **A12 in quattro**, A12a
(questa), A12b, A12c (solo con i codici di PATS) e A12d. In C# una chiave di un modulo si chiede con il namespace (`training:…`, #138).

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

### Che cosa ha lasciato A12a, per ora (29 settembre 2026, branch `m3/a12a-deleted-person-core`, PR #187 in bozza)

- **Che cosa c'è**: **solo la nota**, `decisions/2026-09-29-la-persona-cancellata-nel-nucleo.md`, **«Proposta»**, con due domande a
  Carmine in [un commento sulla #187](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/187#issuecomment-5890079195). **Nessun codice**,
  nessun file del nucleo (per `core-guard`: nucleo 0, una nota aggiunta). La forma proposta (nota §3):
  - `personName(person, t)` e `isErased(vid)` in `web/src/shared/ui/people.ts`, esportati da `shared/ui`: «Deleted person» per uno
    pseudonimo (un VID negativo), altrimenti `Nome (VID)` o il VID da solo; `NamedPerson` è `{ vid, name }`, la forma di
    `TrainingMemberDto`;
  - la parola `people.erased` in `locales/{en,it}/common.json` («Deleted person», «Persona cancellata»);
  - la colonna **`person`** della lista generata, `col.person('trainee')` su un campo `{ vid, name } | null`, così anche una lista dice
    «Deleted person» (la copia dei tour non ci arriva);
  - **domanda 2**, `ErasureTests` (di Carmine): raccomandata la (c), il test legge i contesti di ogni modulo dal registro, come la
    cancellazione, e la lista prende le 21 colonne `trn_`.
- **Che cosa deve sapere la fase dopo**:
  - **A12b usa l'helper di A12a, e non parte prima delle risposte di Carmine**: una fase non si mette in coda sopra una domanda (`08`,
    «Regole di tutte le fasi»). Il branch `m3/a12b-training-erasure` **non** è preparato.
  - ⚠️ **La copia dei tour** (`memberName`, `flightops:people.erased`) non si tocca: la sostituisce una sessione di Carmine.
  - ⚠️ **Per A12b** (`08`, «Com'è andata (A12a)», «Trovato» n.4): `memberLabel` sta in 28 punti; i link al percorso di un trainee vanno
    tolti per uno pseudonimo; **un training aperto affidato a un trainer che si cancella** resta affidato allo pseudonimo, e la mail
    della sessione al trainee nominerebbe il trainer con il numero: la regola del design non ne parla, da decidere in A12b.
  - Il banco di A12a: 127.0.0.1:**5102**, `ivaohub_e2e_a12a` (ancora nessun giro). Il banco dopo: **5105** (5103 e 5104 sono della sessione
    che coordina).
  - VID: A12a non ne usa (il test delle colonne non semina nessuno).
- **La fase dopo**: il codice di A12a, sulla stessa PR, quando Carmine risponde; poi **A12b**.

### Che cosa ha lasciato A11b (29 settembre 2026, branch `m3/a11b-fir-heads`, PR #182)

- **Che cosa c'è** (configurazione e codice del modulo; nessun file del nucleo, nessuna nota nuova, nessuna domanda a Carmine, nessuna
  migrazione): **i capi FIR nel modulo**, sul nucleo di A11a (#159) e come dice la sua nota (`2026-09-27-i-capi-fir-sul-loro-fir` §3.8),
  decisa da Carmine ([il suo commento sulla #159](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/159#issuecomment-5864855723)):
  - in `config/division.json` due voci di `positionGrants` al **team di un FIR** — `Training.View` e `Training.Assign`, livelli
    `Coordinator` e `Assistant` (CH e ACH, non i CHA), `scope: TD`, `"firTeam": true` — e **`firStaffScope: own`** (risposta 2 di
    Carmine). `division.example.json` dice lo stesso alle altre divisioni, con il perché nel commento;
  - la pagina di un training, i trainer proposti, l'assegnazione e `training.approvalQueue` chiedevano già l'unico handler sulla riga, e
    la lista `/api/training/queue` passa dal motore: **seguono il FIR senza una riga del modulo che lo nomini**;
  - **il percorso di un trainee** (`TraineePaths`) dà i ban, e «dove si trova» sui percorsi che se ne ricava, solo a chi l'unico handler
    lascia leggere un ban del trainee (`TrainingBans.MayReadAsync`, come `MayBanAsync`): allo staff del training come prima, a un capo FIR
    **no** (`null`, non una lista vuota); i training, uno per uno come prima. La pagina lo dice (`trainees.firOnly`,
    `trainees.noTrainingsOnFir`). `07` §4.2 lo precisa;
  - i test: `TrainingFirHeadsTests` (integrazione, 2, VID **790074–790078**), i due test di A4 che imparano le voci del team, un caso
    nuovo nello smoke del percorso.
- **Che cosa deve sapere la fase dopo**:
  - **Un capo FIR** (CH o ACH) vede e assegna i training del suo FIR, e nient'altro del training: non accetta, non conduce, non modifica;
    un training pilota non ha FIR e non è suo. Esami, ban, voci della scheda e impostazioni non dicono un FIR e gli restano chiusi.
  - ⚠️ **Il menu gli offre «Esami» e «Ban»** (le voci chiedono `HasAny(Training.View)`, che un permesso sul FIR soddisfa), e le liste
    dietro rispondono 403, che `DataList` disegna come una lista vuota. Toglierle vuole il nucleo: detto al revisore come proposta.
  - ⚠️ **Un avviso di una data** sulla pagina di un training può portare a un training di un altro FIR: per un capo FIR è «non trovato».
  - ⚠️ **`firStaffScope` di IT è `own`** da questa fase: il solo effetto è sui grant al team di un FIR (il training è l'unica entità
    `IHasFir`); il personale dei dipartimenti non è mai fermato dal FIR. Un grant al team nuovo, su un'altra area, dovrà avere righe con
    il FIR (il seme lo salta, la schermata lo rifiuta).
  - ⚠️ **Il banco e2e non ha un capo FIR** (i personaggi sono nucleo): il «fatta quando» lo provano i test d'integrazione. Un CH sul banco
    sarebbe una fase del nucleo a sé.
  - **Questo branch ha preso `main` prima della coda** (efe057a: A11a, #173–#177 e #179), e con esso **l'Invio in più di A6b** come
    commit suo (8807e8a, lo stesso pezzo di c3db117). Poi la coda l'ha raggiunto: con A7b (e7b530a, merge 1baf8fa) è entrato `main` a
    47e2f70, l'Invio di A6b si è unito senza conflitti, e `m3/a7b-trainer-assignee...m3/a11b-fir-heads` mostra di nuovo solo la fase.
    **Una fase che parte da qui** (A12a) ha già `main` a 47e2f70.
  - ⚠️ **Un solo `e2e:full` alla volta** sulla macchina (la sessione che coordina, 29 settembre): Mailpit è condiviso, e due giri insieme
    contano le mail l'uno dell'altro. Si prende `$env:TEMP\ivaohub-e2efull-mailpit.lock` come il lucchetto dello smoke.
  - ⚠️ **`InitialisationMarkerTests.TwoProcessesStartingTogetherBothInitialiseAndBothWriteTheMark`** (del nucleo, #175) ogni tanto va in
    deadlock di MariaDB, anche da solo: se cade, si rilancia la classe con `-class` e si scrive.
  - VID: **790074–790078** sono di A11b (dalberone, 29 settembre; erano della correzione di A8a, che non li ha usati). Il range del
    training è tutto assegnato.
- **La fase dopo**: **A12a** (nucleo: l'helper «persona cancellata» e `ErasureTests` con le colonne del training, con la sua nota nuova),
  dalla cima della coda.

### Che cosa ha lasciato A7b (28 settembre 2026, branch `m3/a7b-trainer-assignee`, PR #181)

- **Che cosa c'è** (codice del modulo e configurazione; nessun file del nucleo, nessuna nota nuova, nessuna domanda a Carmine, nessuna
  migrazione): **il trainer sulla regola delle righe affidate** di A3b, come l'ha decisa Carmine ([risposta 2 sulla
  #135](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/135#issuecomment-5844250425), [decisione sulla
  #146](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/146#issuecomment-5855560982), nota
  `2026-09-27-il-trainer-sulla-regola-delle-righe-affidate`):
  - il training **dichiara il suo trainer** (`IHasAssignee` con `TrainerVid`, in `Training.cs`) e non ha più uno scope suo
    (`IHasResourceScope`, `ScopeOf`, `IdOf` via);
  - **`Training.Conduct` è `OnlyForAssignee`** (e `DeniedToStakeholder`, com'era), e `positionGrants` lo dà ai quattro livelli del TD: TC,
    TAC, TA, trainer. Ognuno conduce i training affidati a lui; su ogni altro il permesso vale `Training.Edit` (TC e TAC);
  - **assegnare scrive solo la riga**: nessun grant, nessun rientro del trainer. **`training-expiry`** chiude soltanto (per tempo, con
    `maxResponseDays`) e **`RunAsync` restituisce quanti training ha chiuso**;
  - le parole dell'avviso e della mail dell'assegnazione non chiedono più al trainer di rientrare;
  - `07` corretto (§1.1, §2.4, §3.2, §3.3, §5.3, §10, §11, §12 n.1).
- **Che cosa deve sapere la fase dopo**:
  - **Chi conduce un training** è il suo trainer (con `Conduct` per posizione) e chi ha `Training.Edit`; mai chi la riga riguarda. Si chiede
    all'unico handler sulla riga (`StaffTrainings.MayAsync`), come prima: nessun codice del modulo guarda `TrainerVid` per decidere.
  - ⚠️ **Il guardiano lascia scrivere il training a chi tiene `Approve`** (i TA), qualunque cosa scrivano: le tre alternative si sommano
    (A7). Chi conduce lo decide l'endpoint.
  - ⚠️ **Al trainee di un training il guardiano risponde con l'eccezione del membro sulla propria riga**, che non chiede permessi: che un
    trainee non conduca il proprio training lo dice l'handler (`DeniedToStakeholder`), prima di tutto.
  - ⚠️ **In un'installazione già avviata** la voce di `positionGrants` di `Training.Conduct` è nuova (i livelli fanno l'impronta): la
    vecchia, a TC e TAC, resta come riga doppione. **I grant con scope scritti da A7** (motivo `training: trainer`) restano, inerti: il
    job non li toglie più. Sui banchi ce ne sono.
  - **`DELETE` di un training non esiste**: la lista dello staff è in sola lettura, e `DELETE` risponde 404.
  - **Il giro dello staff** (`full/training-staff.spec.ts`) fa entrare il trainer all'inizio e non lo fa più rientrare; anche i giri delle
    date, del report e dei prossimi training usano la sessione del trainer aperta all'inizio.
  - **Il banco di anteprima** (127.0.0.1:5090, `ivaohub_preview`, acceso da A7b con la sua build e spento a fine fase, con il database che
    resta; lo script è `preview-bench.ps1` nello scratchpad di A7b): **#6 è chiuso** (motivo «trn-test: chiuso per l'anteprima di A7b.»);
    **#9** (ATC, ADC, LIMC_TWR) è **assegnato** al trainer del banco e aspetta le date; #8 (il mock exam) come prima. Il trainer tiene
    `Training.Conduct` per posizione, e i tre grant con scope che A7 aveva scritto lì (#6, #7, #8), inerti.
  - **A11b** parte solo quando #159 (A11a) è unita; altrimenti la prossima nell'ordine di `08` è **A12a** (nucleo).
  - VID: A7b non ne usa di nuovi (riusa quelli delle classi che cambia); il range del training è tutto assegnato.
- **Trovato, detto al revisore**: la premessa del punto 3 del revisore («the guard says no») non vale per il training, per l'eccezione del
  membro (`08`, A7b, scostamento 4); i candidati non si ricavano da chi tiene `Conduct` (scostamento 3); `DELETE` risponde 404, non 405.
- **La coda**: la PR è in bozza con `(after #178)` e `Queued after #178.`, **in conflitto con `main` e senza CI** (l'handoff, dopo #145 e
  #176), come la coda sotto. Quando #178 sarà unita, il passo della coda di A7b — `main` nel branch con un merge (mai un rebase),
  l'intestazione di A7b in cima a questo file e i blocchi nuovi di `main` sotto, build e **tutti** i test di nuovo, via la coda dal titolo
  e dal corpo, la PR pronta a CI verde — lo fa la sessione di A7b se è ancora viva, altrimenti quella della fase dopo prima di cominciare.

### Che cosa ha lasciato A10c (28 settembre 2026, branch `m3/a10c-exams`, PR #178)

- **Che cosa c'è** (codice del modulo, una migrazione, `AddExams`; nessun file del nucleo, nessuna domanda a Carmine):
  - **`trn_exams`** (`Exams/Exam.cs`), con la forma di A3b per la riga: `[PermissionArea("Training")]`, `[AlsoWrittenWith(ManageExams,
    AlsoOnCreation = true, AlsoOnDeletion = true)]`, `IHasAssignee` con l'esaminatore; nel catalogo `Training.ManageExams` è
    `OnlyForAssignee`. L'esame **non** dice il suo candidato come persona di cui è (niente `IHasStakeholder`), e `ManageExams` resta negato a
    nessuno, com'è nel design §3.1; il form rifiuta un esaminatore che è il candidato. Un TA inserisce, cambia e toglie i suoi esami; HQ, TC
    e TAC tutti. **Del candidato e dell'esaminatore solo il VID**, nella riga e nelle pagine.
  - **La lista e il form** (`Exams/ExamEndpoints.cs`, `Exams/TrainingExams.cs`, `screens/exams.tsx`): `/staff/training/exams` (letta con
    `Training.View`; ogni riga dice `mine` e `mayEdit`, la risposta dell'unico handler; «Io» restringe ai propri) e `/staff/training/exams/$id`
    (scritta con `ManageExams`, **senza `DeletePolicy`**). Le scelte del form (`/api/training/exam-choices`): gli esaminatori che l'handler
    lascia dare al lettore — un TA solo sé stesso, chi ha `Edit` tutti; chi esamina lo dice `IPermissionHolders` del nucleo — e le postazioni.
  - **Il calendario e il sito**: ogni esame è una voce pubblica di tipo `exam` (rating · postazione) che porta a `/training`;
    `GET /api/training/sessions/exams` e il campo `exams` del blocco `training.upcomingSessions` danno gli esami ancora da venire, con i VID
    solo a chi ha fatto il login; `/training` e il blocco li mettono con le sessioni in un elenco solo.
  - **Via la copia `Refusals.cs`** del modulo: si usa quella del nucleo.
  - **I test**: `TrainingExamRulesTests` (unità), `TrainingExamTests` (integrazione, VID 790068–790071 e 790090–790094), `screens/upcoming.test.ts`
    e `exams.test.ts` (Vitest), `web/e2e/training-exams.spec.ts` (smoke), `web/e2e/full/training-exams.spec.ts` (il «fatta quando» sul banco).
- **Che cosa deve sapere la fase dopo**:
  - **A7b** (il trainer sulla regola delle righe affidate): la forma è la stessa degli esami — `[PermissionArea("Training")]` c'è già su
    `Training`; `Conduct` segnato `OnlyForAssignee` **e** `DeniedToStakeholder` (lo è già), e su `Training` DELETE non esiste (un training
    non si elimina: nessun `AlsoOnDeletion`). `TrainingExamTests` è il modello per i suoi test: l'endpoint e, per ogni caso, l'handler e il
    guardiano chiesti direttamente.
  - **A11b** (i capi FIR nel modulo): gli esami non dicono il loro FIR (`IHasFir` no): un capo FIR non ne vede né ne scrive, com'è nel
    design (§3.2).
  - **A12b** (la cancellazione): gli esami del candidato si cancellano (design §6.1); le colonne sono `candidate_vid` ed `examiner_vid`, e la
    voce del calendario va via con l'esame solo se lo si elimina passando dal change tracker (come per i training, ⚠️ di A8a).
  - ⚠️ **Il rating di un esame è uno di quelli allenati** (`HasPracticalTraining`): se il TD deve mettere in calendario anche gli esami SEC e
    ATP (PATS li ha, rating 8), è una domanda — il vocabolario del nucleo non dice quali rating hanno un esame.
  - ⚠️ **Gli smoke di A10b non fingono la lettura degli esami** (`/api/training/sessions/exams`): la pagina disegna le sessioni con l'avviso
    `public.examsUnread`. Uno spec nuovo di `/training` finga anche la lettura degli esami.
  - ⚠️ **Il banco dopo il giro di A10c**: `training-exams.spec.ts` inserisce un esame del pilota del banco (999002) esaminato dal web master
    (999001) e lo toglie alla fine (e all'inizio, se un giro fallito l'ha lasciato); non tocca nessun training.
  - ⚠️ **La colonna booleana del nucleo** (`col.boolean`) dice «Attivo» e «Non attivo»: una colonna sì/no che non è un interruttore si scrive
    come parola del modulo (`col.badge` con le sue `options`), come «Tuo» negli esami — trovato a mano sul banco di anteprima.
  - **Il banco di anteprima** (127.0.0.1:5090, `ivaohub_preview`, spento a fine fase, con il database che resta; lo script è
    `preview-bench.ps1` nello scratchpad di A10c): c'è un esame ACC su LIRR_NE_CTR il 2 ottobre alle 18:00 UTC, del pilota del banco,
    esaminato dal web master.
  - VID: **790068–790071** e **790090–790094** sono di A10c.
- **Trovato, detto al revisore**: il test di A4 sui cinque permessi negati all'interessato (design §3.1) ha fermato una prima stesura che
  segnava `ManageExams` `DeniedToStakeholder` — se Carmine vuole che il candidato non scriva il suo esame, è una modifica di §3.1 e di quel
  test —; `VerifyAlternatives` non guarda ancora `[PermissionArea]` (la piccola PR del nucleo annunciata dal revisore sulla #146).
- **La coda**: la PR è in bozza con `(after #153)` e `Queued after #153.`, **in conflitto con `main` e senza CI** (l'handoff, dopo #145):
  per la sessione che coordina, `main` entra in ogni branch al suo passo della coda, e le suite locali sono verdi. Quando #153 sarà unita, il
  passo della coda di A10c — `main` nel branch con un merge (mai un rebase), l'intestazione di A10c in cima a questo file e i blocchi nuovi di
  `main` sotto, build e **tutti** i test di nuovo, via la coda dal titolo e dal corpo, la PR pronta a CI verde — lo fa la sessione di A10c se è
  ancora viva, altrimenti quella della fase dopo prima di cominciare.

### Che cosa ha lasciato A10b (27 settembre 2026, branch `m3/a10b-blocks-and-public-pages`, PR #153)

- **Che cosa c'è** (codice del modulo, nessuna migrazione, nessuna domanda a Carmine; del nucleo solo **i due conteggi dei blocchi**, 38 → 42 in
  `uiKit.test.ts` e 13 → 17 in `DataBlockEndToEndTests`, come Carmine ha deciso sulla #125, con la nota breve
  `decisions/2026-09-27-i-conteggi-dei-blocchi-del-training.md`):
  - **Le sessioni del sito** (`Public/PublicSessions.cs`): `GET /api/training/sessions` (le sessioni ancora da tenere, dal giorno di oggi nel
    fuso della divisione, al massimo 50) e `GET /api/training/sessions/{id}` (la pagina a cui porta ogni voce del calendario; 404 per un
    training senza sessione pubblica), anonimi; trainee e trainer, per VID e nome, **solo a chi ha fatto il login**. Che cosa è pubblico è una
    regola sola, `Training.SessionIsPublic`, che legge anche la proiezione nel calendario.
  - **I quattro blocchi Data**, tutti `AlwaysLive` (`Blocks/`, le metà in `web/src/modules/training/blocks/`): `training.upcomingSessions`
    (la lista di `/training`, proprietà `limit`), `training.myTraining` (la risposta di `GET /api/training/mine`: per percorso il training
    aperto e che cosa aspetta, che cosa si può chiedere o perché no, «pronto per l'esame», l'ultimo report), `training.trainerQueue` (dei
    training di cui il lettore è il trainer: in evidenza le scelte in ritardo dopo `responseReminderDays`, le date da proporre, i report da
    scrivere — `Staff/TrainerQueue.cs`), `training.approvalQueue` (da approvare e da assegnare, secondo l'unico handler, mai sulla propria
    riga: quante e le 10 più vecchie). A un visitatore i tre personali rispondono soltanto `signedIn: false`. **Non sono in nessuna dashboard
    del seme**: ce li mette chi compone `/me` e `/staff`, come `myTours`.
  - **Le pagine** (`screens/public.tsx`, `screens/site.ts`): **`/training`** («Richiedi training», «I miei training» con il login, i prossimi
    training) e **`/training/sessions/$id`** (postazione, rating, data e ora; con il login chi). `AskOrRefusal` e `ReadyForExamLine` (in
    `screens/parts.tsx`) e `lastReported` (in `screens/trainee.ts`) sono i pezzi che `/training/mine` e il blocco del trainee hanno in comune.
  - **I test**: `TrainingBlocksRulesTests` (unità), `TrainingBlocksTests` (integrazione, VID 790060–790067), `blocks/reading.test.ts` e
    `screens/site.test.ts` (Vitest), `web/e2e/training-public.spec.ts` (smoke), `web/e2e/full/training-upcoming.spec.ts` (il «fatta quando»
    sul banco).
- **Che cosa deve sapere la fase dopo**:
  - **A10c** (gli esami, **può partire**: #135 è unita il 27 settembre alle 20:24 UTC): gli esami entrano in `PublicSessions` (la lista, il
    blocco `training.upcomingSessions` e `/training`) senza VID né nomi per i visitatori; la regola pubblica di un esame si scrive una volta e la
    leggono la proiezione e le pagine, come `Training.SessionIsPublic`; la voce `exam` del calendario ha bisogno di un indirizzo (la pagina di un
    esame, o `/training`): da decidere lì. I due conteggi non si alzano: nessun blocco nuovo. **I tre punti del revisore sulla #146** per la riga
    di un esame sono in `08`, sotto A10c: `[PermissionArea("Training")]` su `Exam`, con un test della spina dorsale in cui TC e TAC cambiano
    l'esame di un altro dall'endpoint; DELETE, che lo tolgono HQ, TC, TAC e il TA a cui è assegnato, nessun altro (risposta 4 di Carmine sulla
    #131), detto e provato; `DeniedToStakeholder` se l'esame nomina il suo candidato. ⚠️ **A10c toglie anche
    `src/IvaoHub.Modules.Training/Refusals.cs`** e usa `IvaoHub.Core.Data.Crud.Refusals` con `CrudProblems.Validation(Refusals, …)`: lo chiede
    la nota di #152 (`2026-09-27-i-rifiuti-di-un-form-nel-nucleo`, unita il 27 settembre alle 15:28, [commento di
    Carmine](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/143#issuecomment-5855666298)) alla prima fase del collaboratore aperta dopo
    il merge — A10b era già aperta, e A11a è del nucleo — e va scritto nel suo «Com'è andata» con quel link.
  - **A7b** (il trainer sulla regola delle righe affidate): `training.trainerQueue` elenca già solo i training di cui il lettore è il trainer,
    cioè le righe che A7b gli affida; e `TrainingBlocksTests` dà `Conduct` al trainer su tutto il dipartimento, non con il grant sulla riga che
    A7b toglie: resta vero anche con la regola nuova.
  - **A11a** (i capi FIR nel nucleo, la fase dopo): parte da `main`, fuori dalla coda, con la nota e la domanda a Carmine; oggi l'handler conosce
    già un filtro per FIR (`FirStaffScope.Own` su una riga `IHasFir`, in `HubAuthorization`), e nel nucleo l'unica riga `IHasFir` è il
    training: i test della spina dorsale vorranno con ogni probabilità il FIR sulla riga di prova, cioè una migrazione del contesto di prova,
    lo stesso che A3b migra con `AddSampleAssignee` — per questo, e per l'handler e il guardiano in comune, il suo codice aspettava l'unione di
    #135, arrivata il 27 settembre.
  - **A11b** (i capi FIR nel modulo): la coda di `training.approvalQueue` è già la risposta dell'unico handler su ogni riga; quando A11a gli
    insegna il FIR, il blocco lo segue da solo, e A11b lo prova.
  - ⚠️ **Il banco dopo il giro di A10b**: `training-upcoming.spec.ts` (il nome viene dopo tutti gli altri giri del training) chiede un training
    ATC del trainee del banco, lo fa accettare, assegnare e datare fra tre giorni attraverso l'API, e alla fine **lo chiude** (nessuna attesa
    dopo una chiusura): il percorso ATC resta libero; il pilota resta come dopo A9b (mock exam dopo). Il banco va ricreato prima di ogni corsa.
  - **Il banco di anteprima** (127.0.0.1:5090, `ivaohub_preview`, acceso dalla sessione di A10b con il codice di A10b e spento il 28
    settembre, con il database che resta; lo script è `preview-bench.ps1` nel suo scratchpad): **#6** (ATC, LIRF_TWR) è ora **programmato
    per il 29 settembre alle 18:00 UTC** (scelta la prima delle due date, dalla sua pagina, come trainee); #8 (il mock exam) è eseguito e
    aspetta il report; le dashboard **`me` e `staff`** hanno i quattro blocchi del training (composte attraverso l'API del contenuto, come
    farebbe il web team). Il trainer è nel roster.
  - VID: il prossimo libero è **790068**.
- **Trovato, detto al revisore**: a mano, il blocco del trainer diceva «i tuoi training aspettano il loro giorno» anche a chi non allena nessun
  training — corretto in «Nessun training da muovere, per ora.» —; le pagine pubbliche dei moduli non hanno metadati SEO e la sitemap non le
  conosce (nucleo, come `/tours`); nel pannello del browser lo screenshot della galleria non arriva.
- **La coda**: la PR è in bozza con `(after #151)` e `Queued after #151.`; #151 è in coda dopo #150, dopo #149, dopo #148, dopo #147, dopo
  #146, dopo #144. **#143 è stata unita alle 14:34 e il passo della coda di #144 l'ha fatto la sessione di A10b** (la sessione di A6b non c'era
  più): `main` in `m3/a6b-request-pages` (f5e3cd6), tutte le suite, via la coda da #144, pronta a CI verde; poi il merge verso l'alto fino ad
  A10b, in ordine (`08`, «Com'è andata (A10b)»). **#152 (`Refusals` nel nucleo) è arrivata dopo**, alle 15:28. **Il 28 settembre**, dopo
  l'unione di #135 e le correzioni di revisione di A6b, A7 e A8a, `main` (a 4d424f9, con #152, #135 e le PR del maintainer fino a #172) è sceso
  nella coda come il revisore ha chiesto sulla #144, e in A10b con il merge della testa nuova di A10a (9e82ad1, merge 5e349b4): nessun
  conflitto, tutte le suite rifatte (i numeri in `08`). `main` è andato avanti ancora (#173, #174): se il revisore li vuole, lo chiede sulla
  PR. Quando #151 sarà unita, il passo della coda di A10b — `main` nel branch con un merge (mai un rebase), build e **tutti** i test di nuovo,
  via la coda dal titolo e dal corpo, la PR pronta a CI verde — lo fa la sessione di A10b se è ancora viva, altrimenti quella della fase dopo
  prima di cominciare.

### Che cosa ha lasciato A10a (27 settembre 2026, branch `m3/a10a-path-and-bans`, PR #151)

- **A10 è divisa in apertura, in tre** (scritto in `08`, sotto A10): **A10a il percorso e i ban** (questa), **A10b i blocchi e le pagine
  pubbliche** (la prossima, sul branch `m3/a10b-blocks-and-public-pages` da `m3/a10a-path-and-bans`), **A10c gli esami**, solo dopo che
  **#135** (A3b) è unita: la riga di un esame si dichiara con ciò che A3b porta (`IHasAssignee`, `OnlyForAssignee`, `AlsoOnDeletion`), e una
  parte che dipende da un cambio del nucleo in revisione non si mette in coda sopra la domanda.
- **Che cosa c'è** (codice del modulo, nessun file del nucleo, nessuna nota nuova, **nessuna migrazione**):
  - **Il percorso del trainee**, `GET /api/training/trainees/{vid}` (`Staff/TraineePaths.cs`, `Training.View`): dove si trova su ogni
    percorso — la risposta della sua pagina, `MyTrainingPathDto`, da `TrainingRequests.PathsOfAsync` (i percorsi per un VID qualunque) —,
    tutti i suoi training come le pagine dello staff (`StaffTrainings.PageAsync`: niente di riservato a un trainer che legge il proprio
    percorso), i suoi ban, `canBan`. 404 per un VID di cui l'hub non sa niente.
  - **I ban**, `/api/training/bans` (`Bans/TrainingBans.cs`, `Bans/BanEndpoints.cs`): lista e form generati, letti con `Training.View`,
    scritti con `Training.Ban` (negato all'interessato, superadmin compreso); non si cambiano (`ReadOnlyRows`), non si eliminano, uno nuovo non
    si somma a uno in vigore (`banAlreadyHolds`), la fine è ancora da venire (`banEndsInThePast`); **«Togli ban»** è `POST …/{id}/lift` con
    la `rowVersion`, chi e quando. La mail **`training.banned`**.
  - **Le pagine**: `/staff/training/trainees` (il VID) e `/staff/training/trainees/$id` (`screens/trainees.tsx`: i percorsi, i ban con «Togli
    il ban», i training per percorso e rating in un `Accordion` con il report, «Banna»); `/staff/training/bans` e `/staff/training/bans/new`
    (`screens/bans.tsx`); le funzioni pure in `screens/path.ts`; due voci nella barra dello staff; nella pagina di un training il nome del
    trainee porta al suo percorso.
  - **I test**: `TrainingBanRulesTests` (unità), `TrainingTraineeTests` (integrazione, VID 790052–790059, con il test della nota allargato
    al percorso), `screens/path.test.ts` (Vitest), `web/e2e/training-trainee.spec.ts` (smoke), `web/e2e/full/training-the-trainee.spec.ts`
    (il «fatta quando» sul banco).
- **Che cosa deve sapere la fase dopo**:
  - **A10b** (i blocchi e le pagine pubbliche): `training.myTraining` ha già la sua risposta per percorso (`MyTrainingPathDto` di `GET
    /api/training/mine`, con il ban in `refusal` e `bannedUntil`) e «pronto per l'esame» è `readyForExam` di `screens/trainee.ts`;
    `training.trainerQueue` e `training.approvalQueue` sono le viste di `StaffQueue` (A7) e `TrainingDates.Unanswered` (A8a). La voce del
    calendario porta ancora a `/training/sessions/{id}`, che è di A10b. **I due conteggi dei blocchi** (`uiKit.test.ts`,
    `DataBlockEndToEndTests`) si alzano con la nota breve che A10 deve aggiungere (Carmine,
    [commento sulla #125](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/125#issuecomment-5835026941)).
  - **A10c** (gli esami): la forma della riga è nella nota di A3b §3.6, sul branch di #135; un TA deve vedere quali esami sono i suoi.
  - ⚠️ **Il banco dopo il giro di A10a**: `training-the-trainee.spec.ts` (il nome viene dopo tutti gli altri giri del training) banna il
    trainee del banco dal suo percorso e poi toglie il ban: il trainee resta con un ban **tolto** nella storia, e niente in vigore. All'inizio
    toglie un ban lasciato in vigore da una corsa fermata a metà. Il resto del banco è come dopo A9b.
  - ⚠️ **La risposta 2 di Carmine sulla #135** (il trainer conduce con la regola di A3b, senza grant con scope né job notturno) **non è in
    nessuna fase**: A7 (#146) usa il grant con scope. Quando #135 sarà unita serve una fase del modulo che la porti (`08`, A10, «Com'è andata
    (A10a)», «Trovato» 1). Detto al revisore.
  - ⚠️ **Il router scrive il `?vid=` di un link fra virgolette nell'`href`** e lo rilegge giusto: una spec guarda l'indirizzo dopo il clic.
  - **Il banco di anteprima** (127.0.0.1:5090, `ivaohub_preview`, lasciato acceso dalla sessione di A10a con il codice di A10a; lo script è
    `preview-bench.ps1` nel suo scratchpad): il trainee 999002 ha un ban **tolto** (dato dal percorso e tolto dalla lista, a mano, il 27
    settembre) e niente in vigore; il resto come dopo A9b — #6 (ATC, LIRF_TWR) aspetta la scelta fra le due date, #7 (pilota) è completato con
    il report, #8 è il mock exam datato e pronto per un report —. Il trainer è nel roster.
  - VID: il prossimo libero è **790060**.
- **Trovato, detto al revisore**: la risposta 2 sulla #135 (sopra); `ConfirmDialog` non ha una dimensione per il suo pulsante (nucleo).
- **La coda**: la PR è in bozza con `(after #150)` e `Queued after #150.`; #150 è in coda dopo #149, dopo #148, dopo #147, dopo #146, dopo
  #144, dopo #143. Quando #150 sarà unita, il passo della coda — `main` nel branch con un merge (mai un rebase), build e **tutti** i test di
  nuovo, via la coda dal titolo e dal corpo, la PR pronta a CI verde — lo fa la sessione di A10a se è ancora viva, altrimenti quella di A10b
  prima di cominciare.

### Che cosa ha lasciato A9b (27 settembre 2026, branch `m3/a9b-after-the-session-pages`, PR #150)

- **Che cosa c'è** (codice del modulo, solo front end: nessun file del nucleo, nessun cambio del server, nessuna nota nuova, nessuna
  migrazione):
  - **La pagina dello staff** (`web/src/modules/training/screens/staff.tsx`): nella sezione «Le date», accanto alla sessione, quando il
    server dice `actions.canRecordOutcome` (dall'inizio della sessione), **«Rischedula»** (gli appunti interni in un campo generato dentro
    `ConfirmDialog`; dopo, il training è `Assigned` e la sezione ripropone da sola la proposta e la data a mano) e **«No-show»** (chiesto
    prima). La sezione **«Il report»**: la scheda — per voce i voti da 1 a 5 o le spunte, con **«N/A» per primo e scelto**, il commento per
    il trainee e la nota per lo staff, con i controlli di Atmosphere (`RadioGroupRoot`, `Textarea`) — e il form generato del report (i due
    commenti, «pronto per il mock exam» mai su un mock exam, «pronto per l'esame», «nessuna attesa»), con **«Pubblica il report»** chiesto
    prima; per un training completato il report pubblicato. La sezione **«Le sessioni»** (lo storico), e l'avviso di **`reservedLeftOut`**
    in cima a un trainer che legge il proprio training.
  - **La pagina del trainee** (`screens/traineeTraining.tsx`): il report (voti, spunte, N/A, i commenti per lui, il commento generale, le
    caselle) **senza niente dello staff**, le sessioni passate, e «che cosa succede dopo» per `Completed` e `NoShow` con l'attesa o la
    richiesta successiva — un mock exam quando il server lo dice — dal percorso di `GET /api/training/mine`. **`/training/mine`**: «Il
    trainer ha pubblicato il report.» e «Leggi il report».
  - **I pezzi**: le funzioni pure in `screens/report.ts` (la scheda come la manda il report — **una voce per ogni riga a schermo**, così
    `sheet[2]` di un rifiuto è la terza riga —, i rifiuti divisi fra il form, le righe e la pagina, che cosa rilegge la pagina, chi ha
    pubblicato, che cosa viene dopo sul percorso), `ReportView`, `ReportBoxes` e `SessionList` in `screens/parts.tsx` (per lo staff e per il
    trainee), i passi `reschedule`, `noShow` e `report` di `useStaffStep`, `reportSchema` e `rescheduleSchema` in `schemas.ts`.
  - **I test**: `screens/report.test.ts` e `schemas.test.ts` (Vitest), `web/e2e/training-report.spec.ts` (lo smoke),
    `web/e2e/full/training-the-report.spec.ts` (il «fatta quando» di A9 sul banco, con la rischedula dalla pagina e la mail del report).
- **Che cosa deve sapere la fase dopo**:
  - **A10**: `ReportView`, `ReportBoxes` e `SessionList` leggono il DTO dello staff e quello del trainee: il percorso del trainee e il
    blocco `training.myTraining` («l'ultimo report») li riusano; «report da scrivere» di `training.trainerQueue` è
    `actions.canRecordOutcome`. ⚠️ **La scheda non è un campo di `SchemaForm`** (A9a, «Trovato» 1): A9b l'ha disegnata nella pagina con i
    controlli di Atmosphere, come la validazione dei tour; se Carmine la vuole nel form generato, è un'estensione del nucleo (`08`, A9b,
    scostamento 1).
  - ⚠️ **Il banco dopo il giro di A9b**: `training-the-report.spec.ts` (il nome viene dopo tutti i giri del training) lascia un training
    **pilota `Completed`** del trainee del banco, con la sua sessione di ieri nel calendario pubblico, «pronto per il mock exam» e senza
    attesa: la richiesta pilota successiva del trainee è un **mock exam**. Le tre voci della scheda che scrive (segno `trn-report` nel titolo)
    le spegne alla fine. Una spec di A10 che vuole un percorso libero usa l'ATC, o sa del mock exam; il banco va ricreato prima di ogni
    corsa.
  - ⚠️ **Il log delle richieste dice 500 per un rifiuto del dominio** che il client riceve come 400 (`UseExceptionHandler` fuori da
    `UseSerilogRequestLogging`, nucleo): un ERR con la traccia nel log del banco, per esempio eliminando una voce usata, non è un errore
    della spec. Detto al revisore.
  - **Il banco di anteprima** (127.0.0.1:5090, `ivaohub_preview`, lasciato acceso dalla sessione di A9b con il codice di A9b; lo script è
    `preview-bench.ps1` nel suo scratchpad): il training pilota #7 è **completato** (rischedulato una volta, poi il report con 4/5, «Da
    migliorare», N/A, «pronto per il mock exam», senza attesa); **#8** è il pilota successivo del trainee, un **mock exam** datato a ieri e
    assegnato al trainer del banco, **pronto per un report** scritto a mano; #6 (ATC, LIRF_TWR) aspetta ancora la scelta fra le due date;
    per il rating PP ci sono tre voci della scheda scritte da A9b (due di pratica e una di teoria). Il trainer è nel roster.
  - VID: A9b non ne usa; il prossimo libero resta **790052** (A3b usa 790040–790044 e 790050–790051).
- **Trovato, detto al revisore**: lo scostamento della scheda (sopra) e il log del nucleo (sopra); le asserzioni di A6b, A7 e A8b non sono
  cambiate: i loro costruttori dei DTO finti dello smoke hanno i campi nuovi con valori neutri (`08`, A9b).
- **La coda**: la PR è in bozza con `(after #149)` e `Queued after #149.`; #149 è in coda dopo #148, dopo #147, dopo #146, dopo #144, dopo
  #143. Quando #149 sarà unita, il passo della coda — `main` nel branch con un merge (mai un rebase), build e **tutti** i test di nuovo, via la
  coda dal titolo e dal corpo, la PR pronta a CI verde — lo fa la sessione di A9b se è ancora viva, altrimenti quella di A10 prima di
  cominciare.

### Che cosa ha lasciato A9a (27 settembre 2026, branch `m3/a9a-after-the-session-server`, PR #149)

- **A9 è divisa in apertura**, come A6 e A8 (scritto in `08`, sotto A9): **A9a il server** (questa), **A9b le pagine** (la prossima, sul
  branch `m3/a9b-after-the-session-pages` da `m3/a9a-after-the-session-server`). A10 viene dopo A9b.
- **Che cosa c'è** (codice del modulo, nessun file del nucleo, nessuna nota nuova; una migrazione, `AddSessionsAndEvaluations`, solo
  additiva):
  - **`trn_sessions`** (`src/IvaoHub.Modules.Training/Sessions/TrainingSession.cs`): le sessioni passate, con la data, l'esito (`Held`,
    `Rescheduled`, `NoShow`), gli appunti interni di una rischedulata e i timbri (chi ha registrato e quando). **`trn_evaluations`**
    (`Sheets/TrainingEvaluation.cs`): la scheda compilata, una riga per voce con la **fotografia** della voce (titolo, sezione, posto) e quale
    voce era, il voto 1–5 o la spunta (`Done`, `NotDone`, `ToImprove`) — nessuno dei due: N/A —, il commento per il trainee e la nota
    riservata. Figlie del training (chiave in cascata), nessuna chiave verso le voci, nessuna delle due `[Audited]`.
  - **La scheda** (`Sheets/EvaluationSheet.cs`): `ItemsOf` (le voci attive del percorso e del rating, per `sort`) e `Fill` (la copia per voce,
    N/A dove niente segna, i rifiuti riga per riga). **«La voce è usata?»** risponde dalle schede compilate (`EvaluationSheetItemReports`).
  - **I verbi dello staff** (`Sessions/TrainingSessions.cs`), in `/api/training/trainings/{id}`, con `Training.Conduct` sulla riga e **dall'inizio
    della sessione** (`TrainingSessions.IsRecordable`, `actions.canRecordOutcome`): `POST reschedule` (`notes`, `rowVersion`: la sessione
    `Rescheduled`, il training di nuovo `Assigned` senza data, nessuna mail), `POST no-show` (`rowVersion`: il training `NoShow`, chiuso da chi
    lo registra, mail `trainingClosed` con l'attesa del no-show), `POST report` (`sheet` — `itemId`, `grade`, `mark`, `traineeComment`,
    `staffNote` —, `generalComment`, `staffComment`, `readyForMockExam`, `readyForExam`, `cooldownWaived`, `rowVersion`: la sessione `Held`, il
    training `Completed`, mail `reportPublished`). Ognuno risponde con la pagina com'è dopo.
  - **Le note riservate e il trainee**: `StaffTrainings.PageAsync` è la funzione unica che costruisce la risposta dello staff di un training,
    e `ReservedFields.For` (`Staff/ReservedFields.cs`) la regola: quando chi legge è il trainee della riga toglie `staffComment`, la
    `staffNote` di ogni voce e gli `internalNotes` di ogni sessione, e lo dice in `reservedLeftOut`. Il DTO del trainee quei campi non li ha.
  - **Il calendario**: un training `Completed` tiene la data della sessione tenuta e la sua voce resta; la rischedula e il no-show la tolgono.
  - **I DTO**: lo staff `cooldownWaived`, `generalComment`, `staffComment`, `sheet` (la copia del report per un `Completed`, le voci attive
    vuote per uno `Scheduled`), `sessions`, `reservedLeftOut`, `actions.canRecordOutcome`; il trainee `cooldownWaived`, `generalComment`,
    `sheet` (senza note) e `sessions` (data ed esito). Tutto in `web/src/shared/api/schema.d.ts`.
  - **I test**: `TrainingSessionRulesTests` (unità), `TrainingSessionsTests` (integrazione, VID 790039 e 790045–790049, con il «fatta quando»
    attraverso l'API).
- **Che cosa deve sapere la fase dopo**:
  - **A9b** (le pagine): nella pagina dello staff (`screens/staff.tsx`) le azioni vanno accanto alla sessione nella sezione «Le date», quando
    `actions.canRecordOutcome`; dopo una rischedula il training è `Assigned` e `dateSteps` ripropone da sola la proposta e l'override. Lo
    storico delle sessioni è `sessions`; la scheda da compilare è `sheet` di un training `Scheduled`, quella compilata di uno `Completed`;
    `reservedLeftOut` dice a un trainer che legge il proprio training che le note non gli sono mostrate. La pagina del trainee
    (`screens/traineeTraining.tsx`) ha `generalComment`, `sheet`, `sessions`, le caselle; «che cosa succede dopo» per `Completed` e `NoShow`
    (`detail.next.*`). ⚠️ **La scheda non è una lista di `SchemaForm`**: le righe hanno campi diversi (voto per la pratica, spunta per la
    teoria) e un'etichetta che viene dai dati (il titolo della voce); la pagina di validazione dei tour
    (`web/src/modules/flightops/screens/review.tsx`) segna gli errori del catalogo con i controlli di Atmosphere. Da classificare prima di
    scrivere: una pagina dedicata del modulo con i pezzi dell'elenco chiuso è codice del modulo; un campo di `SchemaForm` con l'etichetta dai
    dati sarebbe nucleo, con la sua nota. ⚠️ I rifiuti di una riga portano l'indice della riga del payload (`sheet[2].grade`).
  - ⚠️ **Il banco**: una spec di A9b che vuole un training con la sessione passata lo chiede da sé attraverso l'API (come
    `training-the-dates.spec.ts`), lo data a mano nel passato, e lo porta al report o al no-show, con un nome che viene dopo
    `training-the-dates`. **Un training `Completed` resta nel calendario pubblico** con la sua sessione: una spec che conta le voci lo sappia.
    Le voci della scheda sul banco le scrive `training-sheets.spec.ts` (A5), che le toglie alla fine: una spec del report scrive le sue.
  - ⚠️ **Una voce segnata da un report non si elimina più** (anche sul banco): una spec che scrive voci e poi un report le spegne, non le
    elimina.
  - **A10**: il percorso del trainee usa la stessa funzione (`StaffTrainings.PageAsync` con `ReservedFields.For`) e il test della nota va
    allargato lì; «report da scrivere» del blocco `training.trainerQueue` è `TrainingSessions.IsRecordable`.
  - VID: il prossimo libero è **790052** (A3b usa 790040–790044 e 790050–790051).
- **Trovato, detto al revisore**: l'unica asserzione cambiata di una fase sotto, in `TrainingStaffTests` (A7): diceva che la pagina dello
  staff non ha `staffComment`, il segno di A7 che i campi riservati non c'erano ancora; ora dice che c'è, vuoto su una richiesta, e che a un
  advisor non si toglie niente (`08`, A9, «Com'è andata (A9a)»).
- **La coda**: la PR è in bozza con `(after #148)` e `Queued after #148.`; #148 è in coda dopo #147, dopo #146, dopo #144, dopo #143. Quando
  #148 sarà unita, il passo della coda — `main` nel branch con un merge (mai un rebase), build e **tutti** i test di nuovo, via la coda dal
  titolo e dal corpo, la PR pronta a CI verde — lo fa la sessione di A9a se è ancora viva, altrimenti quella di A9b prima di cominciare.

### Che cosa ha lasciato A8b (27 settembre 2026, branch `m3/a8b-dates-pages`, PR #148)

- **Che cosa c'è** (codice del modulo, nessun file del nucleo, nessuna nota nuova, nessuna migrazione; un cambio piccolo del server del
  modulo, sotto):
  - **`/training/mine/$id`** (`web/src/modules/training/screens/traineeTraining.tsx`, rotta `member`): lo stato («Eseguito» dal giorno
    dopo la sessione), il trainer, **i riquadri delle date da scegliere** con «Scegli questa data» (chiesto ancora una volta; un 409 dice
    che il trainer ha cambiato le date e la pagina mostra quelle di adesso), la sessione, che cosa succede dopo, perché si è chiuso.
    `/training/mine` porta a ogni training («Scegli la data» quando ci sono date).
  - **La pagina dello staff** (`screens/staff.tsx`), sezione **«Le date»** sotto il trainer: la sessione (scelta dal trainee o fissata a
    mano, «Eseguita»), le date proposte con i loro avvisi e chi le ha proposte, «Ritira», **«Proponi le date»** e **«Fissa la data a
    mano»** (i form generati, con il campo `datetime` del nucleo); **«Chiudi il training»** in alto e la sezione «La chiusura». La lista
    dice «Eseguito».
  - **Gli avvisi prima di scrivere**, in `useDatesWriter` (`staff.tsx`): `GET …/conflicts` per ogni data intera; con `Warn` e qualcosa
    trovato, un avviso sotto il form e «Proponi lo stesso» / «Fissa lo stesso», che manda di nuovo il form (`confirmed: true`, solo per le
    stesse date); con `Block` il server rifiuta sul campo e la pagina mostra ciò che ha trovato. L'ultima parola è del server.
  - **I momenti in UTC e sotto nel fuso della divisione** (`WhenText` in `parts.tsx`, `spanText` in `screens/dates.ts`).
  - **Il server**: `TrainingSlotWriteDto` e `TrainingDateWriteDto` hanno le date nullabili, e una casella vuota è `errors.required` sul
    suo campo (prima una data vuota «era già passata»); un fatto nuovo in `TrainingDatesTests`.
  - **I test**: `screens/dates.test.ts` (Vitest), `web/e2e/training-dates.spec.ts` (lo smoke), `web/e2e/full/training-the-dates.spec.ts`
    (il «fatta quando» di A8 sul banco).
- **Che cosa deve sapere la fase dopo**:
  - **A9** (dopo la sessione): la rischedulazione riporta il training ad `Assigned`, e la sezione «Le date» ripropone da sola la proposta
    e l'override (`dateSteps` guarda `actions.canConduct` e lo stato); le azioni del dopo sessione vanno accanto alla sessione `Held`
    (`shownState` in `api.ts`). La pagina del trainee ha «che cosa succede dopo» per ogni stato che va avanti (`detail.next.*`):
    `Completed` e `NoShow`, e il report per il trainee (design §4.1), sono di A9.
  - ⚠️ **Il banco dopo il giro di A8b**: `training-the-dates.spec.ts` riprende il training ATC che A7 lascia `Assigned` e alla fine
    **chiude** sia quello sia il training pilota che chiede per sé: dopo il giro il trainee del banco non ha training aperti. Una spec di A9
    che vuole un training con la sessione passata lo chiede da sé (richiesta, accettazione e assegnazione attraverso l'API, come fa questa
    spec) e lo data a mano nel passato — l'override lo permette —, con un nome che viene dopo `training-the-dates`. Il banco va ricreato
    prima di ogni corsa.
  - ⚠️ **La voce del calendario porta a `/training/sessions/{id}`, che ancora non c'è** (la pagina pubblica della sessione è di A10): un
    visitatore che la clicca trova «non trovato». Detto al revisore.
  - ⚠️ **Il promemoria in Mailpit non si aspetta nel giro sul banco** (fino a 15 minuti di CI): lo prova il test d'integrazione di A8a, e
    l'ha visto a mano A8b sul banco di anteprima (un promemoria a testa al giro delle 03:20, nessuno a quello delle 03:35).
  - ⚠️ **`pnpm i18n:check` non vede le chiavi `t('training:…')`**: A8b ne ha avuta una inesistente in un commit, trovata con uno script
    che confronta le chiavi letterali del modulo con i file di lingua (lo trovi descritto in `08`, A8b, «Trovato» 4). Estenderlo è nucleo.
  - VID: A8b non ne usa; il prossimo libero resta **790039**, poi **790045** (A3b usa 790040–790044 e 790050–790051).
- **Trovato, non toccato (nucleo)**, detto al revisore: `useMoment` non dà il giorno della settimana (i riquadri dicono solo la data);
  `pnpm i18n:check` e le chiavi con il namespace (sopra).
- **La coda**: la PR è in bozza con `(after #147)` e `Queued after #147.`; #147 è in coda dopo #146, dopo #144, dopo #143. Quando #147 sarà
  unita, il passo della coda — `main` nel branch con un merge (mai un rebase), build e **tutti** i test di nuovo, via la coda dal titolo e
  dal corpo, la PR pronta a CI verde — lo fa la sessione di A8b se è ancora viva, altrimenti quella di A9 prima di cominciare.

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
    dello staff, mai quelle di un solo dipartimento. La forma degli avvisi non cambia. ⚠️ **Il tetto è lo staff** (seconda revisione di
    #147, punto 1): chi tenesse `Training.View` per un grant senza essere staff leggerebbe, negli avvisi salvati, i titoli delle voci
    visibili solo allo staff. Oggi `View` lo tiene solo lo staff del TD; se un giorno lo si desse a un membro qualunque, il tetto va
    abbassato.
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
