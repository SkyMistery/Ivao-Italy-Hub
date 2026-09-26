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

**Ultimo aggiornamento:** 26 settembre 2026 — **fase A3b** (nucleo: le righe affidate a chi scrive), sul branch
`m3/a3b-entrusted-rows`, **PR #135** verso `main`. **A3 (#131) è unita**, con #136 (la nota del maintainer sugli esaminatori), #132 e
#137, ed è entrata nel branch con un merge. Carmine ha deciso la nota di A3b: sì alla forma, e sì al trainer di A7 con la stessa regola.
**A4** (lo scheletro del modulo) e **A4a** (#133) vanno avanti in un'altra sessione; A5 la apre la sessione di A4. A7 usa A3 e A3b, A10
usa A3b.

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

### Che cosa ha lasciato A3b (26 settembre 2026, branch `m3/a3b-entrusted-rows`, PR #135)

- **Che cosa c'è** (nota `decisions/2026-09-26-le-righe-affidate-a-chi-scrive.md`, caso c, decisa da Carmine sulla #135):
  - **`IHasAssignee { int? AssigneeVid }`** (`Core/Division/DomainContracts.cs`): la riga dice a chi è affidata.
  - **`PermissionDescriptor.OnlyForAssignee`** (`CorePermissions.cs`; `PermissionCatalog.IsOnlyForAssignee`, `EditOf`): un permesso
    segnato raggiunge una riga solo se è affidata a chi chiede. Su ogni altra riga vale come `{Area}.Edit`: nell'unico handler
    (`HubAuthorization.cs`) e nel guardiano (`HubSaveChangesInterceptor.IsWrittenWithAnAlternative`) allo stesso modo. Senza riga
    resta `HasAny`. Il catalogo rifiuta il segno su un permesso che legge.
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
  `Edit`. Per questo `Sample.Manage` è anche `DeniedToStakeholder`, e la nota §3.6 lo chiede agli esami.

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
