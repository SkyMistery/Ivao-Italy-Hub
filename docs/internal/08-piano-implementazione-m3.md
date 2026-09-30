# IVAO Division Hub — Piano di implementazione di M3 (il modulo Training)

> Documento **interno** (italiano). Fonte di verità: `00-piano-di-progettazione.md` (§13, M3) e il design `07-design-m3.md`,
> **deciso** il 25 settembre 2026 (PR #121). Lo scrive il collaboratore (`dalberone` e le sue sessioni, `CLAUDE.md` §0): qui ci
> sono l'ordine, il perimetro di ogni fase, i test e quando è fatta; **il perché** di ogni scelta sta nel design e nelle otto note
> della fase A0. Sotto ogni fase, a fase chiusa, **«Com'è andata»** con ogni scostamento dal design (`CLAUDE.md` §9). Scritto nella
> fase **A0** (25 settembre 2026), sul modello della parte C di `06-piano-implementazione-m2.md`.

## Regole di tutte le fasi

Per non ripeterle tredici volte:

- **Una fase per sessione, un branch `m3/a<N>-<slug>`, una PR verso `main`** con il template compilato onestamente, compresa la
  sezione «For the reviewer» (`CLAUDE.md` §0 regola 4, §9; `CONTRIBUTING.md`, «Working a phase»).
- **Le fasi vanno in coda** (dal 25 settembre 2026, nota `2026-09-25-le-fasi-in-coda`; `CONTRIBUTING.md`, «Phases in a queue»): la
  fase dopo non aspetta il merge di quella prima. Il suo branch parte da quello della fase prima; la PR va **sempre verso `main`**,
  in bozza, con `(after #N)` nel titolo e `Queued after #N.` in testa al corpo, finché quella sotto non è unita, e «For the
  reviewer» nomina l'intervallo che è della fase (`git diff m3/<prima>...m3/<dopo>`). Una correzione chiesta su una fase sotto si
  fa sul suo branch e sale con un merge nei branch sopra, mai con un rebase. **Una fase che dipende da una risposta di Carmine**
  (una nota «Proposta», un cambio del nucleo ancora in revisione) **non si mette in coda sopra la domanda**: le parti che ne
  dipendono aspettano.
- **Le fasi del nucleo sono PR a sé**, prima del codice del modulo che le usa: **A1, A2, A3, A3b, A11a, A12a** (`CLAUDE.md` §0
  regola 6). **Ognuna aggiunge una nota nuova** in `decisions/` — la chiede `core-guard` per ogni file del nucleo toccato — che dice quale
  meccanismo si estende e perché il modulo non ne fa a meno. Se la forma nel codice apre una domanda, la nota è «Proposta», la
  domanda va a Carmine con un commento sulla PR, e il codice che ne dipende aspetta la risposta (`CLAUDE.md` §5). Le note di A0
  registrano le decisioni che scelgono queste estensioni; la forma nel codice è della nota della fase. Le fasi del modulo che ne
  usano una (A7 usa A3, A10 usa A3b, A11b usa A11a, A12b usa A12a) seguono la regola della coda qui sopra.
- **Nessun file del maintainer** (`CLAUDE.md` §0 regola 2): il piano 00, `HANDOFF.md`, i documenti 00–06, le note già unite,
  `CLAUDE.md`, `CONTRIBUTING.md`, `.github/`, `.claude/`, `ArchitectureTests.cs`, il modulo `flightops`. Un controllo di
  architettura che riguarda solo il training sta in un file di test del modulo. **Nessun test che non si è scritto** si cambia per
  farlo passare (regola 3): se un test condiviso va toccato, lo si dice nella nota della fase e al revisore.
- **Migrazioni solo additive**, una per fase e per contesto. L'`Initial` del modulo nasce in A4 e da lì non si tocca. Due fasi che
  migrano lo stesso contesto non vanno avanti insieme.
- **Test d'integrazione** sulla MariaDB condivisa con **VID `790001–790099`** (grep di un VID prima di usarlo) e **slug
  `trn-test-…`**; si crea e si toglie nel test, niente dati lasciati. ⚠️ **Nessun utente con una posizione del TD e un indirizzo
  email** seminato nei test del modulo: i test dei contatti affermano chi riceve un messaggio al TD, e cadrebbero solo in CI
  (`CONTRIBUTING.md`). I permessi si danno con grant a un VID; un test che ha bisogno di una posizione del TD la semina senza
  indirizzo (da verificare in A4 che basti). ⚠️ **Un grant scritto fa rientrare il titolare**: test e spec rifanno il login.
- **Nessuna chiamata a IVAO nei test**: le forme delle risposte si misurano con il token vero in sviluppo, nella fase che usa
  l'endpoint, e diventano fixture (`tools/record-ivao-fixtures.mjs`).
- **Divisione XX**: nessun ICAO, nessuna postazione, nessuna stringa italiana nei semi, nei predefiniti e nei file di lingua
  inglesi; i predefiniti delle impostazioni non conoscono la divisione.
- **Tutto gira in locale prima del push** (`CONTRIBUTING.md`, «Tests»): build, unit, **integrazione intera senza filtro**, `pnpm
  lint`, `pnpm typecheck`, `pnpm test`, e `pnpm e2e:full` quando c'è una schermata. I file generati si rigenerano (`pnpm gen:api`,
  `pnpm i18n:sync`). ⚠️ **Un worktree non ha `tiles/`**: per `pnpm e2e:full` serve un hard link a `tiles/basemap.pmtiles` della
  cartella principale, o le spec dei tour cadono sui 404 della mappa (trovato in T20b).
- **Ogni scostamento dal design** si scrive sotto «Com'è andata» della fase; se è una decisione, anche in una nota nuova, con la
  domanda a Carmine. A fine fase, «Che cosa ha lasciato <fase>» in cima allo stato di `HANDOFF-M3.md`.

## Le fasi

| Fase | Titolo | Dipende da | In una riga |
|---|---|---|---|
| A0 | Note di decisione e questo piano — **questa PR** | design deciso (#121) | otto note sulle quindici decisioni di §12; le fasi qui sotto |
| A1 | Nucleo: ore, vocabolario dei rating, `RatingBadge`, banco e2e | A0 | le ore dal profilo IVAO; le regole dei rating nel perimetro IVAO; il badge; i personaggi del banco con rating e ore |
| A2 | Nucleo: postazioni ATC da IVAO, tipo `exam` | A1 | `ref_ivao_atc_positions` nella sincronizzazione e la sua directory; `exam` nel seme dei tipi del calendario |
| A3 | Nucleo: più permessi alternativi, anche alla creazione | A0 | `[AlsoWrittenWith]` ripetibile e, per l'entità che lo dichiara, anche alla creazione; test della spina dorsale |
| A3b | Nucleo: le righe affidate a chi scrive | A3 | un permesso che raggiunge solo le righe affidate a chi scrive, nell'handler e nel guardiano: un esame lo cambiano e lo tolgono HQ, TC, TAC e il TA a cui è assegnato |
| A4a | Nucleo: le parole di più moduli — **trovata scrivendo A4** | A0 | il catalogo delle lingue del server tiene le parole di due moduli, ciascuno con il suo namespace |
| A4 | Modulo: lo scheletro | A0, A4a | progetto, contesto, `Initial`, catalogo, `positionGrants` del TD, impostazioni, menu, segmento riservato |
| A5 | Le voci della scheda | A1, A4 | `trn_sheet_items` tradotte, lista e form generati |
| A6c | Nucleo: il suggerimento chiuso tiene la scelta — **trovata scrivendo A6b** | — | l'opzione cliccata dopo averne scritto una parte è quella scelta: la casella e la sua lista sono un campo solo |
| A6a | La richiesta: il server — **A6 divisa in apertura** | A1, A2, A4 | `trn_trainings`, `trn_bans` (tabella), i controlli per percorso, il teorico, l'annullamento, la mail, gli endpoint del trainee |
| A6b | La richiesta: le pagine | A6a | `/training/request` con la domanda sul teorico, `/training/mine` con l'annullamento, lo smoke e il giro sul banco |
| A7 | Accettare, rifiutare, assegnare | A3, A6b | le pagine dello staff, il grant del trainer, il job che lo toglie |
| A7b | Il trainer sulla regola delle righe affidate — **decisa da Carmine sulla #146** | A3b (#135), A7–A10a | `IHasAssignee` sul training, `Training.Conduct` segnato `OnlyForAssignee` e per posizione a TA e trainer; via il grant con scope e la metà di `training-expiry` che lo toglie |
| A8a | Le date: il server — **A8 divisa in apertura** | A7 | `trn_slots`, avvisi e politiche, proposta, scelta, override, «Eseguito», calendario, promemoria, chiusura per tempo e a mano, le mail |
| A8b | Le date: le pagine | A8a | i riquadri in `/training/mine/$id`, le date e la chiusura nella pagina dello staff, lo smoke e il giro sul banco |
| A9a | Dopo la sessione: il server — **A9 divisa in apertura** | A5, A8b | `trn_sessions`, `trn_evaluations`, rischedula, no-show, scheda con N/A, report, mock exam, le note riservate e il trainee, le mail |
| A9b | Dopo la sessione: le pagine | A9a | le azioni del dopo sessione nella pagina dello staff, il report nella pagina del trainee, lo smoke e il giro sul banco |
| A10 | Blocchi, pagine pubbliche, percorso, esami, ban | A2, A3, A3b, A9b | i quattro blocchi Data, `/training` e la sessione, il percorso del trainee, `trn_exams`, i ban |
| A11a | Nucleo: i capi FIR | A7 | un permesso di modulo a una posizione FIR, contato solo sul suo FIR |
| A11b | I capi FIR nel modulo | A10, A11a | assegnazione, lista e `approvalQueue` per FIR |
| A12a | Nucleo: «persona cancellata» e le colonne del training | A10 | l'helper nel nucleo; `ErasureTests` con `TrainingDbContext` |
| A12b | La cancellazione e la conservazione | A12a | `TrainingPersonalData : IPersonalDataEraser` |
| A12c | L'archivio di PATS — **solo se** si ottiene il significato dei codici | A10 | `trainingNEW` ed `exam` in sola lettura sul percorso del trainee |
| A12d | Giro completo e chiusura di M3 | A11b, A12b | `pnpm e2e:full` di tutto il percorso, `FORKING.md`, il rapporto di chiusura |

**Parallelismo possibile** (se servisse): A1, A3 e A4 non si toccano — A1 migra il contesto del nucleo, A3 non migra (il modulo di
prova sta nei test), A4 fa nascere il contesto del modulo — e possono andare avanti insieme in sessioni diverse, ognuna in coda sopra
A0. A2 viene dopo A1 (stesso contesto, e il legame postazione→rating). Dalle fasi del modulo in poi tutto migra `TrainingDbContext`:
**in fila**, una sopra l'altra. **A3b** (nucleo, aggiunta il 25 settembre 2026 con la risposta di Carmine sulla #131) viene dopo A3,
di cui estende il meccanismo, e prima di A10, che la usa; non migra il contesto del modulo, quindi può andare avanti in qualunque
momento fra le due, in una sessione sua, accanto alle fasi del modulo. **A6c** (nucleo, trovata scrivendo A6b il 26 settembre 2026)
tocca solo il front end del nucleo e non migra niente: va verso `main` quando è pronta, accanto alle fasi del modulo, senza coda.
**L'ordine del design** (§11) resta: i capi FIR stanno in fondo di proposito, perché tutto il resto funziona senza e l'estensione
più delicata non blocca il modulo (§12 n.3).

### A0 — Note di decisione e questo piano

Design §11, §12, §14. Branch `m3/a0-decisions`, PR #125. Documenti, nessun codice.

1. **Otto note** in `decisions/`, una per decisione o per gruppo coerente di §12, ognuna con il link al commento di Carmine che la
   decide:
   - `2026-09-25-chi-conduce-e-chi-scrive-un-training` — n.1 (il grant con scope del trainer), n.2 (`[AlsoWrittenWith]`
     ripetibile e alla creazione), n.3 (i capi FIR in A11), n.10 (gli esami da tutto lo staff del training);
   - `2026-09-25-le-note-riservate-e-il-trainee` — n.13, **una nota sua**, come Carmine ha chiesto;
   - `2026-09-25-il-teorico-lo-dichiara-il-trainee` — n.15 (lo scostamento dal piano §9.2 e §14) e n.12 (`theoryExamUrl`);
   - `2026-09-25-rating-e-postazioni-dal-nucleo` — n.5, con la correzione 3 della prima revisione che la regge;
   - `2026-09-25-che-cosa-resta-fuori-da-m3` — n.6 (lo storico di PATS), n.8 (group training, GCA, briefing), n.14 (il feed);
   - `2026-09-25-il-training-in-pubblico` — n.4;
   - `2026-09-25-la-cancellazione-dei-dati-di-un-trainee` — n.7;
   - `2026-09-25-il-tempo-per-la-data-e-le-voci-della-scheda` — n.9 e n.11.
2. **Questo piano.**
3. **`HANDOFF-M3.md`**: «Che cosa ha lasciato A0».

**Test**: nessuno (documenti). Si controlla che ogni decisione di §12 stia in una nota con il suo link, e le regole di `core-guard`
sul diff del branch.
**Fatta quando**: Carmine approva e unisce la PR.

**Com'è andata** (25 settembre 2026, branch `m3/a0-decisions`):

- **Otto note, non quindici**: le decisioni che si tengono (chi scrive la riga, il teorico, il perimetro) stanno insieme; la n.13 ha
  la sua. Ognuna ha «Da portare nel piano»; insieme ripetono tutta la §14 del design, compresa la sitemap (§8.2), che non è una
  domanda di §12 ed è nella nota `il-training-in-pubblico`.
- ⚠️ **Scostamento dal design §11**: A0 doveva scrivere anche le note **delle estensioni del nucleo**. Non le scrive: `core-guard`
  vuole che una PR che tocca il nucleo **aggiunga** una nota, e per la n.2 Carmine ha chiesto proprio una PR a sé con una nota
  nuova e i test della spina dorsale. Scritte qui, ogni fase del nucleo avrebbe dovuto aggiungerne una seconda sullo stesso tema.
  Le note di A0 registrano le decisioni; A1, A2, A3, A11a e A12a portano ciascuna la nota del suo meccanismo.
- **Scostamenti dall'elenco del design §11**, come fece T0 in M2: **`trn_bans` nasce in A6** (la richiesta deve già rifiutare un
  trainee bannato) e la sua schermata resta in A10, come `fo_bans` fra T11 e T15; **la funzione che decide il mock exam** nasce in
  A6 (la richiesta la mostra), la casella che la alimenta e il giro completo sono di A9; **`trn_trainings` nasce intera** in A6,
  così A7–A9 non la rimigrano; **A11 e A12 si dividono in PR** (nucleo prima, modulo dopo; l'archivio di PATS a parte perché è
  condizionato).
- **Trovato leggendo il codice**, e scritto nelle fasi che ne dipendono:
  1. ⚠️ **`ErasureTests.TheColumnsThatNameAPersonAreTheOnesTheErasureKnows` non vede da solo le colonne del training**, come il
     design §6.1 dava per scontato: legge `HubDbContext` e `FlightOpsDbContext`, scritti nel test, e confronta con una lista a mano.
     Allargarlo è toccare un test condiviso: A12a.
  2. ⚠️ **Un blocco nuovo alza due conteggi scritti in test condivisi** (`web/src/features/admin/uiKit.test.ts`, oggi 38 blocchi;
     `DataBlockEndToEndTests`, oggi 13 blocchi Data): A10 aggiunge quattro blocchi, e la domanda su come alzarli va a Carmine in
     apertura di A10.
  3. **Il seme dei tipi del calendario si ricorda chiave per chiave** (`ContentSeeder.SeedCalendarKindsAsync`), quindi `exam`
     arriva anche a un database già avviato; ma non guarda se un tipo con la stessa chiave esiste già, scritto a mano: da verificare
     in A2.
  4. **`ITheoryExamSource` non esiste nel codice**: il piano la nomina soltanto. Nasce in A6, nel modulo (nota
     `il-teorico-lo-dichiara-il-trainee`).
  5. **`AlsoWrittenWithAttribute` ha `AttributeUsage` senza `AllowMultiple`**, e il guardiano legge la prima alternativa solo in
     modifica: come il design §3.4 diceva. **Non c'è un test di architettura** «un modulo non nomina IVAO»: i controlli del design
     §10 per il training stanno in un file di test del modulo.
  6. **La PR del modulo di T20b è unita** (#123, piano 1.10): l'helper «persona cancellata» è `memberName` nel front end dei tour,
     e il collaboratore non lo tocca (A12a).
- **`main` è andato avanti durante A0**: la #124 (le fasi in coda, nota `2026-09-25-le-fasi-in-coda`) è stata unita mentre il
  piano si scriveva. Portata nel branch con un merge; le regole qui sopra la seguono.
- **Verificato**: che ogni decisione n.1–n.15 stia in una nota con il link al suo commento, e che ogni nota citata esista; le
  regole di `core-guard` rifatte in PowerShell sul diff del branch, perché su questa macchina non c'è una bash (nessun file del
  maintainer, nessun file del nucleo, otto note aggiunte). ⚠️ Il comando «Try it locally» in testa a `core-guard.sh` passa gli
  stati di git (`A`, `M`) dove la CI passa quelli dell'API (`added`, `modified`): letto alla lettera, segna come file del
  maintainer ogni nota nuova. È un file del maintainer: detto al revisore nella PR. **Non verificato**: niente da compilare né da
  provare; la CI di una PR di soli documenti la dice la PR.
- **Le risposte di Carmine sulla revisione di A0** ([commento sulla PR #125][c125], 25 settembre 2026, tutte come raccomandato),
  scritte qui e non nelle note: #125 è stata unita prima che entrassero, e una nota unita non si tocca. Registrate nel commit che
  unisce `main` in A1, come ha chiesto il revisore su #128:
  1. **n.13, la forma confermata**: **una funzione sola** del modulo costruisce ogni risposta dello staff su un training e toglie i
     campi riservati quando chi legge è il trainee della riga; il suo test d'integrazione nasce in A9 e si allarga in A10.
  2. **`ITheoryExamSource` sta nel modulo** (A6). Leggere gli esiti degli esami da IVAO, se un giorno si potrà, sarà del perimetro
     IVAO del nucleo, con una nota sua in quella fase.
  3. **I conteggi dei blocchi in A10 si alzano**: scritto sotto A10, al punto 1.

[c125]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/125#issuecomment-5835026941

### A1 — Nucleo: le ore, il vocabolario dei rating, `RatingBadge`, il banco e2e

Design §1.7, §2.2, §8 n.1, n.4, n.8; nota `2026-09-25-rating-e-postazioni-dal-nucleo`. Branch `m3/a1-ratings-and-hours`. **PR del
nucleo**, con la sua nota nuova (caso b: il minimo dei dati IVAO con uno scopo, il vocabolario nel perimetro IVAO, un componente
nell'elenco chiuso, il banco).

1. **Le ore di connessione**: `IvaoUserProfileReader` legge `hours` dal profilo (`/v2/users/me`: oggi il campo è fra quelli misurati
   e non letti); la **forma si misura** con il token vero (ATC e pilota separate? secondi o ore?) e diventa una fixture. Due colonne
   su `hub_users` (migrazione additiva del nucleo), scritte da `UserSyncService` a ogni login come i rating. Il minimo dei dati IVAO
   con uno scopo, come l'email il 6 settembre (nota `2026-09-06-indirizzo-di-un-destinatario`). ⚠️ Come i rating, **si aggiornano
   solo al login**: il modulo legge l'ultima fotografia, e la richiesta la copia in `trainee_hours_at_request`.
2. **Il vocabolario dei rating** nel perimetro IVAO del nucleo (`Core/Ivao/`): per ATC e piloti il numero di IVAO, l'ordine, la
   sigla, il nome tradotto (chiavi del nucleo), quali hanno un training pratico, quale tipo di postazione serve a ciascuno; e le
   tre domande del modulo — il successivo con un training, «almeno», le postazioni per rating. **Prima di scrivere il legame
   postazione→rating si misura** `/v2/ATCPositions/all` con il token vero: se le postazioni di IVAO portano già un rating minimo, il
   vocabolario non scrive il tipo di postazione e il legame si legge da lì in A2 (design §8 n.4).
3. **`RatingBadge`** nell'elenco chiuso (`web/src/shared/ui/catalog.ts`; piano §8.3; `docs/UI-GUIDELINES.md` §3, che oggi lo
   rimanda ai moduli): un badge **di testo**, nessuna immagine di IVAO; nella galleria `/staff/admin/ui-kit`.
4. **Il banco e2e con rating e ore** (n.8): i personaggi di `/e2e/signin` (`E2ESignIn`) ricevono rating ATC e pilota e ore, così il
   giro del training ha un trainee (rating sotto quello allenato, ore sopra la soglia) e un trainer (rating sopra).

**Test**: unit sul vocabolario vero (l'ordine ATC e pilota, il successivo di ogni rating, chi ha un training pratico, «almeno» ai
bordi) e sul lettore del profilo con la fixture registrata (ore presenti, assenti, zero); integrazione: un login di prova scrive ore
e rating, uno successivo li aggiorna; la galleria mostra il badge (se un test della galleria conta i componenti, il conteggio sale
qui, e la nota della fase lo dice).
**Fatta quando**: in sviluppo, con il token vero, dopo un login `hub_users` ha le ore ATC e pilota di chi è entrato (numeri nella
PR); il vocabolario risponde come IVAO oggi (AS3 → ADC, ACC → nessun training pratico, SEC almeno ADC); `RatingBadge` è nella
galleria; il banco entra con un trainee e un trainer con rating e ore.

**Com'è andata** (25 settembre 2026, branch `m3/a1-ratings-and-hours`, PR #128, in coda dopo #125):

- **Misurato prima del codice, con il token vero** (nota nuova `2026-09-25-le-ore-e-il-vocabolario-dei-rating`, §1): `hours` di
  `/v2/users/me` è un **array** di righe `{ type, hours }` per `pilot`, `atc` e `staff`, **in secondi** — lo schema pubblico di IVAO
  lo dice, e le ore di `dalberone` (7 502 599 s = 2 084,05 h ATC, 6 273 832 s = 1 742,73 h pilota) sono quelle del suo profilo;
  `/v2/ATCPositions/all` (11 863 postazioni, 21 MB) e `/v2/subcenters/all` (1 497 settori, 32 MB) **non portano nessun rating**; il
  vocabolario (ATC 2–10, AS1…CAI; piloti 2–10, FS1…CFI) l'ha scritto l'API stessa, nei rating dei clienti connessi al tracker,
  perché nessun endpoint lo elenca. La nota è una **scelta tecnica**: dà forma a decisioni di Carmine (design §8 n.1, n.4, n.8;
  nota di A0) e non apre domande.
- **Fatto**: le ore in `hub_users` (`hours_atc`, `hours_pilot`, in ore, migrazione `AddConnectionHours`), lette dal profilo e
  scritte a ogni login; il vocabolario `Core/Ivao/RatingVocabulary.cs` (`Ladder`, `Find`, `NextTraining`, `IsAtLeast`, il tipo di
  postazione di ogni rating allenato) registrato nel nucleo, i nomi in `locales/*/common.json`; `RatingBadge`, il ventiquattresimo
  dell'elenco chiuso, nella galleria e in `docs/UI-GUIDELINES.md`; il banco e2e con rating e ore, il pilota che fa anche da trainee
  e un trainer nuovo; `tools/record-ivao-fixtures.mjs --me` e `--positions`, con tre fixture.
- **«Fatta quando», in sviluppo con il token vero**: dopo un login di `dalberone` sull'hub di questo branch, la sua riga di
  `hub_users` ha `hours_atc` 2 084,05, `hours_pilot` 1 742,73, rating 6 (APC) e 5 (PP); il vocabolario risponde AS3 → ADC, ACC →
  niente, SEC almeno ADC (test di unità); il badge è nella galleria; il banco entra con il trainee (AS3, FS3, 120 e 150 ore) e il
  trainer (SEC, ATP), provato da `web/e2e/full/training-bench.spec.ts`.
- **Scostamenti dal piano, piccoli e scritti nella nota**:
  1. **Un quarto personaggio del banco**, il trainer (`?as=trainer`, `IT-T01`, SEC e ATP, una casella di Mailpit). Il piano diceva
     che i personaggi ricevono rating e ore; il trainee è il pilota di sempre, ma nessuno dei tre è staff del training (design §2.4),
     e il coordinator del web non lo è. Gli altri tre, e le spec che li usano, non cambiano. `VID` e nome dei personaggi stanno ora in
     una classe base comune, `E2EPersonOptions`.
  2. **Le ore restano le ultime quando IVAO non le manda** (i rating invece si sovrascrivono): crescono soltanto, quindi un numero
     vecchio non fa mai passare una soglia. Le ore da staff non si leggono.
  3. **Lo strumento delle fixture ha una modalità con il login** (`--me`): `/v2/users/me` si apre solo al token di un membro, e lo
     strumento prima aveva solo quello dell'applicazione. Scrive i soli campi che l'hub legge, con la persona tolta e ore inventate
     nella forma vera.
  4. Il legame postazione→rating sta nel vocabolario (`PositionType`: ADC `TWR`, APC `APP`, ACC `CTR`, fatto confermato da
     `dalberone`): era il ramo che il design §8 n.4 prevedeva se le postazioni di IVAO non portano un rating, e non lo portano.
     **Un tipo per rating, di proposito**: la prima stesura del design (§1.6, `facilityRatings`, da d2 e da PATS `facilities`) dava
     all'ADC `DEL`, `GND` e `TWR` e all'APC `APP` e `DEP`; il training si fa solo su `TWR`, `APP` e `CTR`, e `dalberone` l'ha
     confermato di nuovo il 25 settembre, dopo la revisione di #128. Se un giorno un rating si allenasse su più tipi,
     `PositionType` diventerebbe un elenco: un cambio del nucleo, con la sua nota.
- **Trovato, e scritto per chi viene dopo**:
  1. ⚠️ **`IvaoUserProfileReaderTests.RealShape` scrive una forma di `hours` inventata** (`{ "atc": 100, "pilot": 200 }`, un oggetto):
     allora il campo non si leggeva. Il test resta verde e non si tocca (è del maintainer, `CLAUDE.md` §0 regola 3); i test nuovi
     leggono la fixture vera. Detto al revisore nella PR.
  2. ⚠️ **Per A2: i settori (`CTR`, `FSS`) stanno solo in `/v2/subcenters/all`**, non in `/v2/ATCPositions/all`; e il primo ha chiuso
     la connessione a metà due volte su tre (32 MB). Le due risposte sono il mondo intero, senza un filtro per paese: il filtro sulla
     divisione è della directory, come per `FirDirectory` in T1 (o `/v2/airports/{id}/ATCPositions` per aeroporto: da misurare lì).
  3. Le descrizioni dei rating di IVAO portano le ore minime **degli esami** (ADC 50, APC 100, ACC 200; PP 50, SPP 100, CP 200):
     `minimumHours` del training resta un'impostazione della divisione senza predefinito (design §1.6).
  4. **`dotnet run --environment` in .NET 10 è un'opzione di `dotnet run`** (variabili d'ambiente), non dell'applicazione:
     l'hub parte in sviluppo con il profilo di `launchSettings.json`, come dice il README.
  5. Il database di sviluppo di `dalberone` (`ivaohub`) ha già `AddConnectionHours`: la migrazione è additiva e l'hub di `main` parte
     lo stesso (applica solo le migrazioni che mancano).
- **Verificato, in locale** (25 settembre 2026): `dotnet build` senza avvisi; unità 708/708 e **integrazione intera senza filtro**
  290/290 (gli eseguibili xUnit); `pnpm lint`, `typecheck`, `format:check`, `i18n:check` verdi; `pnpm test` 480 in 62 file; `pnpm e2e`
  91; **`pnpm e2e:full` 37**, compresa la spec nuova del banco, e senza la mappa di base (le spec dei tour non ne hanno avuto
  bisogno); `pnpm gen:api` senza differenze; `dotnet format --verify-no-changes` sui file toccati; le regole di `core-guard` rifatte in
  PowerShell sul diff verso `main` e su quello della fase (nessun file del maintainer, 22 del nucleo, la nota aggiunta); la galleria
  guardata sul banco, nel tema scuro e in quello chiaro.
- **`main` è andato avanti durante A1** (#126, la chiusura di T20c: la galleria ha ora anche i componenti dei moduli). Il merge con
  il branch è pulito (`git merge-tree`), quindi la PR resta unibile e la CI prova il merge; **provato anche in locale**, su un branch
  temporaneo poi tolto: unità 709, integrazione 290, `pnpm test` 481, `pnpm e2e:full` 38, tutto verde. Non è unito nel branch:
  per le fasi in coda `main` entra quando la fase sotto (#125) è unita, e così `git diff m3/a0-decisions...m3/a1-ratings-and-hours`
  resta solo di A1.
- **Non verificato**: la CI (la dirà la PR); lo strumento `--me` su macOS e Linux (apre il browser con `open` o `xdg-open`, provato
  solo su Windows); un profilo IVAO **senza** una delle righe di `hours` (un membro che non si è mai connesso come pilota: il lettore
  la legge come null, non come zero, ma nessuno l'ha misurata); un rating 1 o 0, mai visto; la casella del trainer, che nessuna spec
  usa ancora.
- **Dopo la revisione** ([revisione di A1 su #128][r128], «approvabile»): #125 è stata unita il 25 settembre alle 19:18, e `main` (#125,
  #126, #127) è entrato nel branch con un merge; nello stesso commit, come il revisore ha chiesto, le risposte di Carmine su #125
  (sotto A0 e A10) e la frase sul tipo di postazione (punto 4 qui sopra). Rifatto tutto sul merge: unità 709, integrazione 290,
  `pnpm test` 481, `pnpm e2e` 91, `pnpm e2e:full` 38 su un banco nuovo. L'ha fatto la sessione di A2, con il permesso di `dalberone`,
  su un branch temporaneo poi spinto su `m3/a1-ratings-and-hours`.

[r128]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/128#issuecomment-5836482100

### A2 — Nucleo: le postazioni ATC da IVAO, il tipo `exam`

Design §1.7, §5.1, §8 n.5, n.6; nota `2026-09-25-rating-e-postazioni-dal-nucleo`. Branch `m3/a2-atc-positions`. **PR del nucleo**,
con la sua nota nuova (caso b: una tabella `ref_ivao_*` in più nella sincronizzazione, come gli aeroporti del mondo in T1; un tipo nel
seme del calendario).

1. **`ref_ivao_atc_positions`** da `/v2/ATCPositions/all` (e `/v2/subcenters/all`, se serve a legare una postazione al suo FIR), con i
   campi **misurati** con il token vero e salvati come fixture; nella sincronizzazione notturna dei dati di riferimento
   (`RefDataSyncJob`), che non pota su una risposta vuota.
2. **Una directory** come `IAirportDirectory`: le postazioni di un rating (il legame del vocabolario di A1) in un FIR o in un
   aeroporto, **solo della divisione** (⚠️ come `FirDirectory` in T1: senza il filtro su `division.countryId` risponderebbe il
   mondo). La legge il modulo per l'elenco della richiesta (A6); `hiddenPositions` lo applica il modulo.
3. **Il tipo `exam`** in `seed/calendar-kinds/kinds.json`, con la sua chiave in `locales/*/seed.json`. Il seme si ricorda chiave per
   chiave, quindi arriva anche a un database già avviato. ⚠️ **Da verificare in apertura**: `ContentSeeder` non guarda se un tipo
   con la stessa chiave esiste già, scritto a mano dal back office; se la chiave è unica, un `exam` creato a mano prima di A2
   farebbe fallire il seme all'avvio. Se è così, il seeder salta la chiave che esiste (e la ricorda), e un test lo fissa.

**Test**: unit sul lettore delle postazioni con la fixture vera; la directory su righe di prova (un rating, un FIR, un aeroporto,
un'altra divisione esclusa); integrazione: il job scrive, aggiorna e non pota su una risposta vuota; il seme porta `exam` in un
database che ha già gli altri cinque tipi; XX: nessuna postazione nei semi.
**Fatta quando**: in sviluppo, con il token vero, la tabella si riempie (numeri nella PR) e la directory risponde con le postazioni
di un rating ATC in un FIR della divisione; `exam` è fra i tipi del calendario dopo un riavvio su un database già avviato.

**Com'è andata** (25 settembre 2026, branch `m3/a2-atc-positions`, PR #129):

- **Misurato prima del codice, con il token vero** (nota nuova `2026-09-25-le-postazioni-atc-e-il-tipo-exam`, §1). La documentazione
  pubblica dell'API sta in `https://api.ivao.aero/docs/{api}-json`: **nessuna** risposta delle postazioni accetta un paese. **Senza
  `mapType`, i settori del mondo superano i 15 secondi del gateway di IVAO** (`504` o connessione chiusa, zero volte su quattro: è ciò
  che A1 aveva visto «due volte su tre»); con `mapType=regionMapPolygon` arrivano in 4–5 s (12 MB), e le postazioni degli aeroporti
  in 3–7 s (10,6 MB). Le risposte di una divisione danno per l'Italia lo stesso contenuto — 195 postazioni in 84 aeroporti, 37
  settori — in **228 chiamate** (una per aeroporto e una per FIR), e righe più povere. Quattro nominativi del mondo sono doppioni
  di IVAO (due italiani); IVAO segna militari 50 postazioni italiane; gli FRA (API `core`) dicono chi si connette, non dove si
  allena un rating. La nota è una **scelta tecnica**, senza domande nuove.
- **Fatto**: `ref_ivao_atc_positions` (il mondo, una riga per nominativo, migrazione `AddAtcPositions`) nella sincronizzazione
  notturna, con le due liste di IVAO rinfrescate e potate ciascuna per conto suo e mai su una risposta vuota, e un giro `partial`
  quando non arrivano; `IAtcPositionDirectory.ForRatingAsync(Rating)`, le postazioni della divisione del tipo che il vocabolario dà
  al rating, con aeroporto e FIR; `exam` nel seme dei tipi del calendario (rosso, dopo `training`); lo strumento delle fixture con
  `mapType`, e due fixture nuove del banco.
- ⚠️ **Il punto da verificare in apertura era vero**: `ContentSeeder` aggiungeva il tipo senza guardare la tabella, e l'indice univoco
  su `cms_calendar_kinds.key` avrebbe fatto fallire l'avvio di un'installazione con un `exam` scritto a mano. Ora il seeder salta la
  chiave che esiste e la ricorda; `CalendarKindSeedTests` lo fissa, ed è provato che il test cade togliendo la correzione (come quelli
  della directory togliendo il filtro della divisione).
- **«Fatta quando», in sviluppo con il token vero**, su un database di prova (`ivaohub_a2`) avviato prima con il codice di A1 — cinque
  tipi e i dati di riferimento veri —: riavviato con A2 applica `AddAtcPositions`, **semina `exam` accanto agli altri cinque**, e la
  sincronizzazione scrive **11 859 postazioni** (le 11 863 di IVAO meno i quattro doppioni) **e 1 497 settori**, con le due chiamate in
  4 e 5 secondi e 4,7 MB di JSON grezzo senza contorni. La directory risponde per l'Italia **84 torri all'ADC** (LIBB 5, LIMM 23, LIPP
  16, LIRR 40), **59 avvicinamenti all'APC**, **32 settori in 7 FIR all'ACC**, nessuna al SEC, e nessuna postazione straniera;
  `LIRF_TWR` porta l'aeroporto LIRF e il FIR LIRR.
- **Scostamenti dal piano, piccoli e scritti nella nota**:
  1. **Il mondo nella tabella** e la divisione nella directory, come gli aeroporti di T1 e `FirDirectory`: il design diceva «le
     postazioni ATC della divisione», che è ciò che la directory risponde.
  2. **Le militari restano nella directory**: `dalberone` ha corretto la sua prima risposta («mai, regola di IVAO») guardando
     l'elenco delle 50: il TD allena su alcune. Le altre le toglie `hiddenPositions` (A4, A6), come Carmine ha deciso (n.5).
  3. **Un membro predefinito in `IIvaoApiClient`**, il primo dell'interfaccia: tre doppi dei test del maintainer la implementano, e
     la regola 3 vieta di toccarli. Un client scritto prima di A2 risponde «nessuna postazione» e il giro tiene lo snapshot.
  4. **Le due chiamate delle postazioni non lanciano**: un errore di trasporto vale «nessuna risposta», mentre le altre chiamate di
     riferimento fanno fallire il giro come prima.
  5. **Nessun endpoint della directory**: la legge il modulo dal suo (A6). **Un tipo per rating**, quello del vocabolario, confermato
     di nuovo da `dalberone` dopo la revisione di #128.
  6. **I test**: oltre a quelli del piano, uno che un hub configurato per un'altra divisione (`countryId` FR) risponde le sue
     postazioni e nessuna italiana. «XX: nessuna postazione nei semi» vale per costruzione: nessun seme nomina una postazione, e il
     test XX del maintainer (che da T20c legge anche le etichette dei tipi del calendario) passa con `exam`.
- **Trovato, e scritto per chi viene dopo**:
  1. ⚠️ **All'avvio la sincronizzazione parte solo su un'installazione senza centri**: su una che gira già, le postazioni arrivano con
     il primo giro notturno (03:15) dopo il rilascio. Lo stesso per il **banco e2e locale** (`ivaohub_e2e`), che sopravvive fra le
     corse: per avere le postazioni nel banco, `DROP DATABASE ivaohub_e2e; CREATE DATABASE ivaohub_e2e;`. La CI parte sempre da un
     banco nuovo, e lì le postazioni ci sono (43 e 29 dalle fixture).
  2. **128 dei 221 aeroporti italiani non hanno `centerId` in IVAO**, ma nessuno di loro ha una postazione. In un'altra divisione una
     postazione d'aeroporto potrebbe arrivare con il FIR vuoto: la directory la risponde con `Fir` null.
  3. **Le `_I_TWR`** (25 in Italia, le informazioni degli aeroporti non controllati) per IVAO sono torri: la directory le dà
     all'ADC. Se il TD non ci allena, vanno in `hiddenPositions` con le militari che non usa.
  4. **Il seme dei template e delle pagine ha lo stesso difetto** che aveva quello dei tipi: una release che semina uno slug già
     scritto a mano farebbe fallire l'avvio sull'indice `(kind, slug, is_template)`. Non è di A2 (nessun seme nuovo, codice del
     maintainer): detto al revisore.
  5. **#125 è stata unita durante A2** (19:18): il passo della coda di A1 l'ha fatto questa sessione, con il permesso di `dalberone`
     (sotto A1, «Dopo la revisione»), e A1 aggiornato è entrato qui con un merge. **Alle 20:07 anche #128 è stata unita**: `main` è
     entrato qui con un merge che non porta file — l'albero è quello su cui sono girati i test qui sotto — e #129 è passata a pronta.
- **Verificato, in locale** (25 settembre 2026, dopo il merge di A1 con `main`): `dotnet build` senza avvisi; unità 715/715 e
  **integrazione intera senza filtro** 298/298; `pnpm lint`, `typecheck`, `format:check`, `i18n:check` verdi; `pnpm test` 481 in
  62 file; `pnpm e2e` 91; **`pnpm e2e:full` 38 su un banco nuovo** (43 postazioni e 29 settori dalle fixture), senza la mappa di
  base; `pnpm gen:api` senza differenze;
  `dotnet format --verify-no-changes` sui file toccati; le regole di `core-guard` rifatte in PowerShell sul diff della fase (nessun
  file del maintainer, 18 del nucleo, la nota aggiunta). Prima del merge, sul branch da solo: unità 714, integrazione 298,
  `e2e:full` 37 due volte, sul banco vecchio e su uno nuovo.
- **Non verificato**: la CI (la dirà la PR); il giro notturno delle 03:15 (provati l'avvio e il job chiamato dai test); le postazioni
  di un'altra divisione con dati veri (solo la fixture di Parigi); un nominativo presente in tutte e due le risposte, mai visto; il
  gateway di IVAO sotto carico con `mapType`, misurato solo il 25 settembre.

### A3 — Nucleo: più permessi alternativi in scrittura, anche alla creazione

Design §3.4, §8 n.7, §12 n.2; nota `2026-09-25-chi-conduce-e-chi-scrive-un-training`. Branch
`m3/a3-alternative-write-permissions`. **PR del nucleo, con la sua nota nuova e i test della spina dorsale** (Carmine, n.2).

1. **`AlsoWrittenWithAttribute` ripetibile** (`AllowMultiple = true`); il guardiano di `HubSaveChangesInterceptor.EnsureWriteIsAllowed`
   prova **ogni** alternativa, ognuna come oggi: con lo scope della riga (`IHasResourceScope`), mai all'interessato
   (`IHasStakeholder`), senza spostare la riga fra dipartimenti.
2. **Anche alla creazione**, per l'entità che lo dichiara (la forma — una proprietà dell'attributo o un attributo a parte — la
   propone la nota): senza scope, perché la riga non ne ha ancora uno, e con il permesso su almeno uno dei dipartimenti della riga,
   come `Edit`.
3. **Chi lo usa oggi non cambia**: `ContactMessage` e il PIREP dei tour (`src/IvaoHub.Modules.FlightOps/`, file del maintainer, che
   non si tocca) continuano a funzionare come sono; lo provano i loro test, che restano verdi senza essere toccati.
4. **Il modulo di prova dei test** (`SampleModule`) guadagna un'entità con due alternative, di cui una anche alla creazione.

**Test** (spina dorsale): una riga del modulo di prova si scrive con la prima alternativa e con la seconda, ciascuna con lo scope
della riga e non con quello di un'altra; l'interessato non la scrive con nessuna; una riga nuova la crea chi ha l'alternativa segnata
«anche alla creazione», non chi ha l'altra, e non su un dipartimento dove non la tiene; nessuna alternativa sposta una riga; `Edit`
continua a bastare.
**Fatta quando**: i test della spina dorsale passano, compresi quelli che c'erano (contatti, validazione dei tour).

**Com'è andata** (25 settembre 2026, branch `m3/a3-alternative-write-permissions`, PR #131):

- **Classificata prima del codice** (`CLAUDE.md` §5, caso b) e **scritta per prima la nota nuova**,
  `2026-09-25-i-permessi-alternativi-e-la-creazione`: una **scelta tecnica** che dà forma alla decisione n.2 di Carmine, senza
  domande sulla forma (una per A10, posta in anticipo: sotto, «Trovato» 1). **La forma dell'«anche alla creazione»**, che il punto 2
  lasciava alla nota, è **una proprietà dell'attributo**, `AlsoOnCreation`: il permesso è lo stesso, la segnatura è di un'alternativa
  e non dell'entità (sul training nessuna delle tre alternative crea), e chi usa l'attributo oggi non cambia una riga (nota §3.2).
- **Fatto**: `[AlsoWrittenWith]` ripetibile e con `AlsoOnCreation` (`Core/Division/DomainContracts.cs`); nel guardiano
  (`HubSaveChangesInterceptor`) il blocco della «seconda» alternativa è diventato una funzione sola, `IsWrittenWithAnAlternative`, che
  le prova tutte — in modifica come prima, alla creazione solo quelle segnate, senza scope, su almeno un dipartimento della riga e mai
  per una riga su chi scrive, all'eliminazione nessuna. Nel modulo di prova `SampleRecord` (`smp_records`), scritta da `Sample.Decide`
  e dal nuovo `Sample.Record` (anche alla creazione); cinque test della spina dorsale, `AlternativeWritePermissionTests`.
- **Scostamenti dal piano, piccoli e scritti nella nota**:
  1. **A3 migra, ma solo il contesto del modulo di prova** («A3 non migra», in «Parallelismo possibile», voleva dire nessun contesto
     dell'hub né di un modulo): `AddSampleRecords` crea `smp_records`. Lo snapshot prende anche le tre tabelle che `ModuleDbContext`
     mappa fuori dalle migrazioni da T14 e T19a, lo scarto innocuo di T4b, senza operazioni nella migrazione.
  2. **I test non usano `TestCurrentUser`**: tiene solo i permessi del nucleo, e il suo `Has` non passa lo scope della riga. Chi scrive
     è l'identità che un login mette nel cookie (`HubClaims.BuildIdentity`), messa nella richiesta dello scope del test e letta dal vero
     `HttpContextCurrentUser`; nessun utente seminato in `hub_users` (VID 790005–790008), e le righe si tolgono a fine test.
  3. **Un test in più dell'elenco**: nessuna alternativa **elimina** una riga, nemmeno quella che crea. La nota lo dice, il test lo
     fissa.
  4. `SampleModule.cs`: gli `using` riordinati da `dotnet format`, come `CONTRIBUTING.md` chiede per un file toccato.
- **Trovato, e scritto per chi viene dopo** (nota §3.3 e §5):
  1. ⚠️ **Per A10: eliminare un esame resta di `Edit`.** Crearlo passerà con `AlsoOnCreation`; se lo eliminano TC e TAC basta
     `CrudOptions.DeletePolicy = Training.Edit`, e il guardiano è già d'accordo; se deve eliminarlo anche chi l'ha inserito, è un'altra
     estensione del nucleo. La domanda è andata a Carmine in anticipo su A10, come ha chiesto `dalberone`, con tre commenti su #131
     (nota §3.5): la domanda, i fatti del TD e la loro precisazione — gli esami si gestiscono su IVAO e all'hub servono solo per il
     calendario; dall'hub un esame lo tolgono **HQ, TC, TAC e il TA a cui è assegnato**, nessun altro, ed è solo una rimozione
     «cosmetica» dal calendario; la postazione la decide chi ha l'esame. **Carmine ha scelto la 4, la regola del TD** ([il suo
     commento][c131]): una regola del nucleo che oggi non c'è, con una nota sua e la fase **A3b**, prima di A10; questa PR resta com'è.
  2. **Per A10**: `MapCrud` chiede all'handler il permesso di scrittura **sulla riga**, prima del guardiano: per gli esami
     `WritePolicy = Training.ManageExams`. Il commento di `CrudOptions.WritePolicy` parla di risorse senza dipartimento, ma il motore
     lo chiede sulla riga anche alle altre.
  3. **Per A7**: lo scope che il training dichiara (`IHasResourceScope`) e quello del grant che l'assegnazione scrive (`ModuleGrants`)
     devono essere la stessa stringa, `training:training:{id}`: il guardiano e l'handler le confrontano così come sono.
  4. **Per il revisore, non cambiati** (è il comportamento di oggi, che A3 ripete per ogni alternativa, come deciso): il guardiano
     guarda l'interessato e lo scope di una riga **solo dopo** la scrittura; `TestCurrentUser.Has` non passa lo scope a
     `PermissionSet`.
  5. **Gli eseguibili xUnit non ricompilano**: dopo la prova dei test sul guardiano di `main` (sotto), la prima corsa intera è partita
     senza ricompilare, e i tre test nuovi sono caduti sui binari della prova. Non conta; rifatta dopo la build.
- **`main` è andato avanti due volte durante A3**: la #130 di Carmine (il piano con M3, e `IvaoUserProfileReaderTests.RealShape` con
  la forma vera delle ore, trovata in A1), unita alle 20:26 e portata nel branch con un merge prima del primo push; poi **la #129
  (A2), unita alle 21:14**, entrata con un secondo merge. Con A2 aperta, #131 era in conflitto con `main` e **GitHub non faceva
  girare `build-test`** sui commit dei documenti (solo `core-guard`): un segno da riconoscere. L'unico conflitto era in cima a
  `HANDOFF-M3.md` (tenuti tutti e due i paragrafi, A3 sopra); `08` si è unito da solo. Tutto rifatto sul secondo merge.
- **Verificato, in locale** (25 settembre 2026, sul merge con `main` che porta A2): `dotnet build` senza avvisi; unità 715/715;
  **integrazione intera senza filtro** 303/303 (i 298 di `main` e i 5 nuovi), compresi i test che passano dal guardiano con
  un'alternativa (contatti, validazione dei tour, token personali); la classe nuova da sola 5/5, e **3 dei 5 cadono sul guardiano di
  `main`** (rimesso per la prova, poi riscritto e confrontato con il diff salvato); `pnpm lint`, `typecheck`, `format:check`,
  `i18n:check` verdi; `pnpm test` 481 in 62 file; `pnpm e2e` 91; `pnpm e2e:full` 38 su un banco nuovo, senza la mappa di base;
  `pnpm gen:api` senza differenze; `dotnet format --verify-no-changes` sui file toccati; le regole di `core-guard` rifatte in
  PowerShell sul diff verso `main` (nessun file del maintainer, 5 del nucleo, la nota aggiunta). ⚠️ Una prima corsa intera sul
  merge, fatta mentre Vitest girava in parallelo, ha dato un test dei contatti rosso per un errore di TLS di Windows all'avvio
  dell'host («Impossibile contattare l'autorità di sicurezza locale»), e Vitest stesso non è riuscito ad avviare 18 dei suoi file: la
  classe da sola è verde, e tutte e due le corse, rifatte senza niente in parallelo, sono verdi (sopra). Le suite pesanti vanno una
  alla volta.
  Prima dei merge: sul branch da solo unità 709, integrazione 295, `pnpm e2e` 91, `pnpm e2e:full` 38 su un banco nuovo; sul merge con
  la #130 unità 709, integrazione 295, `e2e` 91, `e2e:full` 38.
- **Non verificato**: la CI (la dirà la PR); l'estensione su un modulo vero (il training dichiara le sue alternative in A7, gli esami in
  A10); un'alternativa dichiarata su una classe base (il guardiano legge gli attributi della classe dell'entità, `inherit: false`, come
  prima).
- **Dopo la revisione** ([revisione di A3 su #131][r131], 25 settembre 2026, «approvable as it is»): il revisore ha rifatto tutto sul
  branch (unità 715, integrazione 303, `pnpm test` 481, `e2e:full` 38 su un database nuovo) e ha letto il guardiano riga per riga; i due
  punti trovati sul guardiano (nota §5) sono confermati, per un compito di rafforzamento del maintainer. **Carmine ha risposto sugli
  esami** ([il suo commento][c131]): **la 4, la regola del TD**. Registrata nella nota §3.5 con il link, e la fase del nucleo che
  chiede è **A3b**, qui sotto; questa PR resta com'è.

[r131]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/131#issuecomment-5839714140
[c131]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/131#issuecomment-5840224757

### A3b — Nucleo: le righe affidate a chi scrive

Nota di A3 `2026-09-25-i-permessi-alternativi-e-la-creazione` §3.5: la domanda, i fatti del TD e [la risposta di Carmine][c131] (la 4).
**Aggiunta il 25 settembre 2026**, dopo quella risposta: viene dopo A3, di cui estende il meccanismo, e prima di A10, che la usa. Branch
`m3/a3b-entrusted-rows`. **PR del nucleo, con la sua nota nuova (caso (c), «Proposta») e i test della spina dorsale** (Carmine, sulla
#131).

1. **La regola**: un permesso che raggiunge **solo le righe affidate a chi scrive** — la riga dice a chi è affidata; per un esame, il TA
   a cui è assegnato —, **nell'unico handler e nel guardiano**, come oggi lo scope della riga, così che l'endpoint e la rete dicano la
   stessa cosa. Oggi il nucleo non ce l'ha: `Training.ManageExams` tenuto su tutto il dipartimento raggiunge ogni esame, e il grant con
   scope del trainer (n.1) non basta, perché un grant per esame farebbe rientrare l'esaminatore a ogni esame inserito (nota di A3, §3.5).
2. **La forma la propone la nota della fase**, caso (c) di `CLAUDE.md` §5, stato «Proposta», con la sua domanda a Carmine: **il codice
   aspetta la risposta**. Da decidere lì, fra l'altro: come la riga dice a chi è affidata; dove un permesso si segna «solo sulle righe
   affidate» (nel catalogo, o sull'alternativa dell'entità); che cosa vale alla creazione (oggi, n.10, un esame lo inserisce chi ce l'ha
   assegnato); e che la rimozione della riga, che A3 lascia a `Edit`, valga anche per la persona a cui è affidata.
3. **Gli esami** (A10): li **cambiano e li tolgono** dall'hub **HQ, TC, TAC e il TA a cui l'esame è assegnato**, nessun altro, e togliere
   è **solo una rimozione dal calendario** (l'annullamento vero è su ivao.aero). **Chiarito da `dalberone` il 26 settembre 2026**: un
   esame si assegna **solo a un esaminatore**, e gli esaminatori sono **solo HQ, TC, TAC e i TA (TA1–9)**, come da regole, **mai i
   trainer** (T01–T99); il design (d4) diceva «anche un TA o un trainer». HQ, TC e TAC hanno già `Training.Edit` su ogni esame; la
   regola nuova serve ai TA, ognuno sui suoi. **Confermato da Carmine il 26 settembre 2026** (nota
   `2026-09-26-gli-esaminatori`): `Training.ManageExams` va a TC, TAC e TA1–9, non ai trainer; A4 lo scrive così nei `positionGrants`.
4. **Chi c'è oggi non cambia**: il training, i PIREP, i contatti e le righe di prova si comportano come prima, e i loro test restano
   verdi senza essere toccati; il modulo di prova guadagna una riga affidata a una persona.

**Test** (spina dorsale): una riga affidata a X la cambia e la toglie X con il permesso segnato, e non Y che lo tiene allo stesso modo;
`Edit` basta ancora; l'handler e il guardiano rispondono uguale; l'interessato resta escluso come in A3; le alternative che c'erano non
cambiano.
**Fatta quando**: la nota è decisa da Carmine e i test della spina dorsale passano, compresi quelli che c'erano.

**Com'è andata** (26 settembre 2026, branch `m3/a3b-entrusted-rows`, PR #135):

- **La nota prima del codice** (`CLAUDE.md` §5, caso c): `2026-09-26-le-righe-affidate-a-chi-scrive`, «Proposta», con due domande a
  Carmine sulla #135 ([il commento][q135]). La PR è partita in bozza, in coda sopra la #131, con la sola nota. **Carmine ha risposto sì a
  tutte e due** ([il suo commento][a135]): la forma della nota §3, e il trainer di A7 con la stessa regola. Il revisore ha chiesto tre cose
  per il codice ([i suoi rilievi][rv135]), entrate nella nota prima del codice. Intanto **la #131 (A3) è stata unita**, con #136 (la nota
  del maintainer `2026-09-26-gli-esaminatori`), #132 e #137: `main` è entrato nel branch con un merge senza conflitti, e la PR ha perso
  «(after #131)».
- **Fatto**, come la nota §3:
  - `IHasAssignee` (`Core/Division/DomainContracts.cs`);
  - `PermissionDescriptor.OnlyForAssignee`, letto con `PermissionCatalog.IsOnlyForAssignee`, e `PermissionCatalog.EditOf`;
  - nell'unico handler, un permesso segnato vale come `{Area}.Edit` su una riga non affidata a chi chiede, e senza riga resta `HasAny`;
  - nel guardiano, un'alternativa segnata conta solo per chi ha la riga: prima e dopo in modifica, la riga nuova alla creazione, e con
    **`AlsoOnDeletion`** (nuovo su `[AlsoWrittenWith]`) all'eliminazione. Il guardiano prende il catalogo dal contenitore;
  - `PermissionCatalog.VerifyAlternatives`, chiamato all'avvio da `HubPipeline.InitializeAsync` prima delle migrazioni, sul modello di
    ogni contesto.

  Nel modulo di prova: `SampleRecord.AssigneeVid` (la migrazione `AddSampleAssignee`, del solo contesto di prova) e `Sample.Manage`.
  I test: sei della spina dorsale, `AssignedRowPermissionTests`, e nove di unità, `AssigneePermissionTests`.
- **Scostamenti dal piano, piccoli e scritti nella nota**:
  1. **Due domande e non una**: la seconda, sul trainer di A7, l'ha voluta `dalberone`. È decisa, e la registra A7.
  2. **I rilievi 1 e 2 del revisore sono un controllo all'avvio** in `src/IvaoHub.Web/HubPipeline.cs`, un file del nucleo che il piano
     non nominava, e non un test di architettura, perché `ArchitectureTests.cs` è del maintainer. Il catalogo rifiuta anche il segno su un
     permesso che legge, come la nota proponeva.
  3. **`Sample.Manage` è anche `DeniedToStakeholder`**, perché sull'interessato l'handler e il guardiano dicano lo stesso (sotto,
     «Trovato» 1).
  4. **I test chiedono anche l'handler**, sulla riga come fa il motore (prima e dopo il payload, e sulla riga nuova). I test di A3
     provavano solo il guardiano.
- **Trovato, e scritto per chi viene dopo**:
  1. **Il guardiano esclude l'interessato da ogni alternativa, l'handler solo dai permessi `DeniedToStakeholder`**, e così è da A3.
     Con un'alternativa che non è segnata così, l'endpoint lascia passare e la rete ferma chi non ha `Edit`. Non è un buco, perché la
     rete è la più stretta delle due, ma le due non dicono lo stesso. Per A10: se l'esame dice il suo candidato, `ManageExams` va segnato
     `DeniedToStakeholder` (nota §3.6).
  2. **Per A10** (nota §3.6): la dichiarazione degli esami, e un TA che deve vedere nella lista quali esami sono i suoi.
  3. **Per A7**: la risposta 2 di Carmine. `Training.Conduct` va dato per posizione ai TA1–9 e ai T01–T99, perché i `positionGrants`
     di A4 danno ai trainer solo `View`: una voce nuova del seme si applica al primo avvio che la trova.
- **Verificato, in locale** (26 settembre 2026, sul merge con `main` che porta A3), una suite alla volta:
  - `dotnet build` senza avvisi;
  - unità 724/724: le 715 di `main` e le 9 nuove;
  - **integrazione intera senza filtro** 313/313: le 307 di `main` e le 6 nuove. La classe nuova da sola passa 6/6;
  - `pnpm lint`, `typecheck`, `format:check` e `i18n:check` verdi;
  - `pnpm test`: 481 test in 62 file;
  - `pnpm gen:api` senza differenze;
  - `pnpm e2e`: 91;
  - `pnpm e2e:full`: 38, sul banco di questa sessione (porta 5082, database nuovo `ivaohub_e2e_a3b`), senza la mappa di base,
    perché la porta 5080 e `ivaohub_e2e` li usa la sessione di A4;
  - `dotnet format --verify-no-changes` sui file toccati, e le regole di `core-guard` rifatte in PowerShell.

  **Sul guardiano e sull'handler di `main`**, rimessi per la prova, cadono 4 dei 6 test d'integrazione e 2 dei 9 di unità. Restano
  verdi solo le garanzie che valgono già prima: l'interessato escluso, le alternative non segnate, la domanda senza riga, il
  catalogo. Poi il codice è tornato com'era, confrontato con il diff salvato.

  Due inciampi:
  1. ⚠️ **Dopo la prova, la prima build non ha ricompilato.** I file rimessi con `Copy-Item` avevano la data della copia, più vecchia
     dei binari della prova, e la suite di unità ha girato sui binari vecchi. Li ho toccati e ricompilati, e ogni corsa qui sopra
     viene dopo quella build.
  2. ⚠️ **Una corsa intera sul codice finale ha fatto cadere un test dei contatti all'avvio dell'host**: «Unable to connect to any of
     the specified MySQL hosts», sul container di Testcontainers, dopo il controllo nuovo. La classe da sola è passata 11/11, e la
     corsa intera rifatta 313/313.
- **Non verificato**: la CI (la dirà la PR); la regola su un modulo vero (gli esami in A10, il trainer in A7); un avvio che fallisce
  davvero per una dichiarazione sbagliata. I due rifiuti sono provati dai test di unità su `VerifyAlternatives`, e ogni avvio dei test
  d'integrazione fa girare il controllo sui modelli veri, che passano.
- **Dopo la revisione del codice** ([il revisore][rv135b], 26 settembre 2026): è **approvabile appena il branch è in pari con `main`**.
  - Il revisore ha rifatto tutto sul merge con `main` a 5dda35e: unità 761, integrazione 317.
  - Ha provato anche una mutazione dell'handler (il confronto con chi ha la riga rovesciato), che fa cadere 2 dei 9 test di unità.
- **In pari con `main`** (27 settembre 2026):
  - `main` era 68 commit più avanti di 5ddba1f: A4, A4a, A5, A6a, #152 (i rifiuti di un form nel nucleo), le altre del maintainer
    fino a #158, e le correzioni #138, #141 e #142. È entrato nel branch con un merge.
  - **L'unico conflitto** era in `HANDOFF-M3.md`, risolto tenendo tutti i paragrafi, il più recente sopra: l'intestazione nuova di A3b
    sopra quella di A6a, e «Che cosa ha lasciato A3b» sopra A6a, A5, A4 e A4a. Dal testo di `main` non manca nessuna riga.
  - `08` si è unito da solo. `HubPipeline.cs` si è unito da solo con la riga nuova della diagnostica di `main`.
  - **Il punto di parole del revisore**, fatto: il messaggio di `PermissionCatalog` dice esattamente che cosa controlla, cioè che il
    permesso `View` dell'area non è mai `OnlyForAssignee`. Il test di unità che lo prova ha preso lo stesso nome.
  - **Rifatto tutto, una suite alla volta**, sul merge:
    - `dotnet build` senza avvisi;
    - unità 794/794: le 785 di `main` e le 9 nuove;
    - **integrazione intera senza filtro** 336/336: le 330 di `main` e le 6 nuove;
    - `dotnet format --verify-no-changes` sui file C# della fase;
    - `pnpm lint`, `typecheck`, `format:check` e `i18n:check` (752 chiavi) verdi;
    - `pnpm test`: 495 test in 63 file;
    - `pnpm gen:api` senza differenze;
    - `pnpm e2e`: 91, con il lucchetto della porta 4173 che si passano le sessioni che lavorano in parallelo;
    - `pnpm e2e:full`: 41 sul banco di questa sessione (5082). Il database è lo stesso `ivaohub_e2e_a3b` del 26 settembre, portato
      avanti dalle migrazioni di `main`.

[q135]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/135#issuecomment-5841258158
[a135]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/135#issuecomment-5844250425
[rv135]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/135#issuecomment-5844250526
[rv135b]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/135#issuecomment-5847984026

### A4a — Nucleo: le parole di più moduli

**Non era nel piano**: l'ha trovata la sessione di A4, il 26 settembre 2026, al primo test d'integrazione dello scheletro, e
`dalberone` ha scelto di farla subito, come fase del nucleo a sé (`CLAUDE.md` §0 regola 6). Nota nuova
`2026-09-26-le-parole-di-piu-moduli`, **Decisa** da Carmine il 26 settembre 2026 come raccomandato ([risposta su #133][a133]). Branch
`m3/a4a-module-locales`, da `main`, PR #133. **PR del nucleo**, prima di A4, che le va in coda (`CONTRIBUTING.md`, «Phases in a queue»).

1. **Il problema**: `LocaleCatalog` appiattisce tutti i file di una lingua in un solo dizionario e rifiuta una chiave dichiarata due
   volte. Con due moduli si ripetono per forza `_source` (lo scrive `pnpm i18n:sync` in ogni copia) e `nav.section` (lo esige la barra
   dello staff da ogni modulo): **l'hub non parte**.
2. **La proposta** (nota §3): `_source` saltata, e usata per riconoscere il file di un modulo; le chiavi di un modulo anche con il loro
   namespace (`training:nav.section`); senza namespace, come oggi, quelle che un solo modulo dichiara; una chiave di due moduli solo
   con il namespace; i doppioni che toccano il nucleo ancora rifiutati. I tour non cambiano.

**Test** (spina dorsale, file nuovo `LocaleCatalogModuleTests`, su file di lingua scritti dal test): due moduli con `_source` e
`nav.section` si caricano e ciascuno si legge con il suo namespace; la chiave di un modulo solo anche senza; quella di due moduli solo
con; `_source` non è una parola; un modulo che ridice una parola del nucleo, e due file del nucleo con la stessa chiave, fermano l'avvio
come prima.
**Fatta quando**: i test nuovi e quelli che c'erano passano, e lo scheletro di A4 parte con il suo file di lingua accanto a quello dei
tour.

**Com'è andata** (26 settembre 2026, branch `m3/a4a-module-locales`, PR #133):

- **Trovata, non pensata**: lo scheletro di A4 compilava e i suoi test di unità passavano; i due test d'integrazione nuovi sono caduti
  all'avvio dell'host, con `The translation key '_source' is declared twice for the same language`. Misurato sui file veri: fra
  `training.json` e `flightops.json` collidono `_source`, `nav.section` e quattro chiavi che il training avrebbe potuto chiamare
  diversamente; nessuna con i file del nucleo. Cadrebbero anche i due test di unità del maintainer che caricano le lingue del
  repository (`NotificationTemplateTests`, `RatingVocabularyTests`). Il codice di A4 è stato messo da parte, **solo in locale**, e la
  scelta — fermarsi o fare la fase del nucleo subito — l'ha fatta `dalberone`.
- **Fatto**: `LocaleCatalog` (`Core/Localization/LocaleCatalog.cs`) come nella nota §3 — `ModuleSourceKey`, i file dei moduli letti a
  parte e aggiunti dopo quelli del nucleo (`AddModules`), l'errore di prima in una funzione sola (`DeclaredTwice`) —; il test nuovo;
  la nota, **Proposta**, con la domanda a Carmine in un [commento su #133][q133].
- **Provato che il test cade senza la correzione**: con il comportamento di `main` (la sola costante aggiunta, perché il test la
  nomina) il primo test cade proprio sull'errore dell'avvio, e gli altri due — la regola che resta — passano.
- **Provato con A4**: su un branch temporaneo, poi tolto, A4a unita con lo scheletro di A4: l'host parte, e passano i quattro test
  d'integrazione del training e i test di unità che leggono le lingue del repository.
- **Verificato, in locale** (26 settembre 2026, sul branch da `main`): `dotnet build` senza avvisi; unità 718/718 (le 715 di `main` e
  le 3 nuove); **integrazione intera senza filtro** 298/298; `pnpm lint`, `typecheck`, `format:check`, `i18n:check` verdi; `pnpm test`
  481 in 62 file; `pnpm e2e` 91; `pnpm e2e:full` 38, sul banco di A3, senza la mappa di base; `pnpm gen:api` senza differenze;
  `dotnet format --verify-no-changes` sui file toccati. Le suite pesanti una alla volta.
- **Non verificato**: un avvio sull'host di produzione (lo stesso codice dell'host dei test, che legge la stessa cartella).
- **Dopo la risposta e la revisione** (26 settembre 2026): **Carmine ha risposto sì, come al §3** ([risposta][a133]), e la nota è
  *Decisa*, con la risposta e il link. I [rilievi del revisore][r133]:
  1. **`main` unito nel branch** (#131, #132, #136, #137): i conflitti in questo file e in `HANDOFF-M3.md` risolti tenendo ciò che ha
     scritto #131 (A3, A3b, la risposta 4, l'avviso sugli esami in cima), con A4a sopra. ⚠️ Nella fusione git aveva perso la riga
     d'intestazione della sezione di A4: rimessa.
  2. **La trappola del fallback senza namespace** scritta nella nota (§3 punto 4 e «Da portare nel piano»): una chiave che un modulo
     legge nuda in C# smette di rispondere, in silenzio, quando un altro modulo la dichiara. La chiude #138 del maintainer, in coda
     dopo questa PR: in C# una chiave di un modulo si chiede con il namespace.
  3. **`TryAdd` con `DeclaredTwice`** anche per le chiavi dei moduli: una chiave del nucleo scritta come una chiave di modulo con il
     namespace ferma l'avvio con il messaggio di sempre, non con un errore del dizionario; un test in più lo prova.
  **Rifatto tutto sul merge** (26 settembre 2026): `dotnet build` senza avvisi; unità 719/719; **integrazione intera senza filtro**
  307/307; `pnpm lint`, `typecheck`, `format:check`, `i18n:check` verdi; `pnpm test` 481 in 62 file; `pnpm e2e` 91; `pnpm e2e:full`
  38 su un banco nuovo; `pnpm gen:api` senza differenze. ⚠️ La prima corsa dell'integrazione è caduta tutta in dieci secondi perché
  Docker Desktop si era fermato (`DockerUnavailableException`): riacceso, e rifatta.

[q133]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/133#issuecomment-5840424471
[a133]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/133#issuecomment-5844250303
[r133]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/133#issuecomment-5844271855

### A4 — Modulo: lo scheletro

Design §0.4, §1.6, §3.1, §3.2; note `chi-conduce-e-chi-scrive-un-training`, `il-teorico-lo-dichiara-il-trainee`,
`rating-e-postazioni-dal-nucleo`, `il-tempo-per-la-data-e-le-voci-della-scheda`. Branch `m3/a4-training-skeleton`.

1. **`IvaoHub.Modules.Training`** (referenzia solo `Core`), `TrainingDbContext : ModuleDbContext`, `__EFMigrationsHistory_training`,
   migrazione **`Initial`** con le tabelle di questa fase: nessuna del modulo, solo le tabelle del nucleo che il contesto mappa escluse
   dalle migrazioni (una migrazione senza tabelle c'è già: `MapAuditLog` dei tour). Registrato in `IvaoHub.Web/Modules.cs`.
2. **`web/src/modules/training/`** con il manifest, la sezione «Training» nella barra dello staff (`/staff/training`, per ora le
   impostazioni), i18n `training` in `it` e `en` (`pnpm i18n:sync`); `web/src/modules/index.ts`.
3. **I permessi** del design §3.1 nel catalogo del modulo, con `DeniedToStakeholder` su `Approve`, `Assign`, `Conduct`, `Edit` e
   `Ban`; **i `positionGrants` del TD** del design §3.2 in `config/division.json` e in `division.example.json` — TC e TAC tutto; i
   TA1–9 (livello Advisor del TD in `StaffRoleMap`) `View`, `Approve` e `ManageExams`; i trainer T01–T99 (livello Member) solo
   `View` — **non** `ManageExams`: gli esaminatori sono HQ, TC, TAC e i TA, mai i trainer (Carmine, nota `2026-09-26-gli-esaminatori`).
   `modules.training.baseDepartment: TD` c'è già.
4. **Le impostazioni** `TrainingSettings` (design §1.6) con i predefiniti che non conoscono la divisione — `maxResponseDays` e
   `theoryExamUrl` vuoti, `hiddenPositions` vuoto, nessuna soglia di ore —, dietro `Training.ManageSettings`, schermata generata.
5. **Il segmento riservato** `training` (`IModule.ReservedSegments`), perché `/training` è del modulo.
6. **Un file di test del modulo** per i controlli del design §10 (il modulo non nomina IVAO, non scrive numeri di rating).

**Test**: integrazione: i grant del TD arrivano una volta e raggiungono la sessione (⚠️ la posizione del TD seminata **senza**
indirizzo email, e si verifica leggendo i test dei contatti che così non riceve); chi ha `ManageSettings` cambia un'impostazione e
la rilegge, chi non l'ha no, un valore fuori limite rifiutato sul campo; il fork XX parte con il modulo e i predefiniti vuoti (in un
test del modulo: `ForkabilityXxDivisionTests` è condiviso). Unit: i predefiniti e le regole delle impostazioni. E2e: le impostazioni
salvate e rilette.
**Fatta quando**: l'utente del banco con i permessi del TD vede la sezione Training, cambia un'impostazione e la rilegge.

**Com'è andata** (26 settembre 2026, branch `m3/a4-training-skeleton`, PR #139):

- **Classificata prima del codice** (`CLAUDE.md` §5): codice del modulo, dentro meccanismi che ci sono — `IModule`, `ModuleDbContext`,
  le impostazioni dei moduli, `positionGrants`, `SchemaForm`, il vocabolario dei rating (A1) e la directory delle postazioni (A2) —;
  nessun file del nucleo. ⚠️ **Al primo test d'integrazione l'hub non è partito**: il catalogo delle lingue del server non regge due
  moduli (`_source` e `nav.section` ripetuti). `dalberone` ha scelto la fase del nucleo **A4a** subito, a sé (#133, nota
  `2026-09-26-le-parole-di-piu-moduli`), e il codice di A4 è rimasto fermo finché Carmine non l'ha decisa (sì, come raccomandato);
  ora A4 va in coda dopo #133 e ne unisce il branch, che porta anche `main`.
- **Fatto**, come il perimetro qui sopra:
  1. `IvaoHub.Modules.Training` (solo `Core`), `TrainingDbContext`, `__EFMigrationsHistory_training`, e **`Initial`** senza tabelle
     del modulo: lo snapshot ha le sette tabelle del nucleo escluse, la migrazione soltanto l'`AlterDatabase` del set di caratteri,
     come l'`Initial` dei tour. Registrato in `Modules.cs`, nel `.sln`, nell'host e nei test di unità.
  2. `web/src/modules/training/`: il manifest, la sezione «Training» con una voce, **`/staff/training/settings`** (design §4.2), le
     lingue it ed en copiate da `pnpm i18n:sync`; `modules/index.ts`.
  3. I nove permessi di §3.1, `DeniedToStakeholder` su `Approve`, `Assign`, `Conduct`, `Edit` e `Ban`; i nove `positionGrants` del TD
     di §3.2 in `division.json` e in `division.example.json`, **con `ManageExams` senza i trainer** (sotto, scostamento 1).
  4. **`TrainingSettings`** con i dieci campi di §1.6 e i loro predefiniti — nessuna soglia di ore, `maxResponseDays` e `theoryExamUrl`
     vuoti (`null`), `hiddenPositions` vuoto, attese 5 e 14 giorni, avviso dopo 3, `Warn`, `["event"]`, promemoria a 24 ore —, dietro
     `Training.ManageSettings`, schermata generata.
  5. Il segmento riservato `training`.
  6. **`TrainingArchitectureTests`**, i controlli del design §10, in un file del modulo.
- **Scostamenti dal piano, piccoli**:
  1. ⚠️ **`Training.ManageExams` non va ai trainer (T01–T99)**, che il design §3.2 invece elencava, con `View` e basta. **Il fatto è
     cambiato**, non la scelta: la decisione n.10 di Carmine è «gli esami li inserisce chi ha l'esame assegnato», e il design dava
     l'esame anche ai trainer per la risposta d4 («anche un TA o un trainer»); **il 26 settembre 2026 `dalberone` ha precisato che un
     esame si assegna solo a un esaminatore, e gli esaminatori sono HQ, TC, TAC e i TA, come da regole, mai un trainer**. Tolto prima
     che A4 arrivi in qualunque installazione: un seme di `positionGrants` si applica una volta sola, e cambiarlo dopo non toglierebbe
     il grant già scritto. Il fatto vale anche per **A3b** (che la sezione della fase, sul branch di A3, lasciava «da chiarire con
     `dalberone` in apertura») e per **A10**. Poiché cambia l'elenco che la decisione n.10 scrive, **ha la sua nota**,
     `2026-09-26-gli-esami-li-inserisce-chi-esamina`, con la domanda a Carmine nella issue #134 (A4 non aveva ancora una PR): lo ha
     fatto notare la sessione di A3. **Deciso da Carmine** il 26 settembre 2026, come raccomandato: [«yes» sulla #134][a134], la stessa
     decisione che ha preso sulla #131 ([commento][c131b]) e che vale nella sua nota `2026-09-26-gli-esaminatori` (#136, piano 1.14),
     che corregge la n.10. Il seme resta com'è; la nota di A4 è *Decisa* e rimanda a quella.
  2. **Due endpoint di lettura** che il piano non nominava, perché la schermata generata sceglie e non fa scrivere:
     `/api/training/ratings` (i rating con un training pratico, dal vocabolario del nucleo; a ogni membro, perché li useranno anche
     A5 e A6) e `/api/training/positions` (le postazioni della divisione di quei rating, dalla directory, ognuna con il suo rating; a
     chi gestisce le impostazioni). **Il modulo non scrive un numero di rating**: una soglia di ore si sceglie fra quelli del server,
     e il valore della scelta porta percorso e numero (`Atc:5`), perché i due percorsi numerano i gradini allo stesso modo.
  3. **Le regole delle impostazioni leggono il nucleo**: il rating di una soglia deve avere un training pratico nel vocabolario, una
     riga per rating; `conflictKinds` sono tipi del calendario che esistono (la schermata offre quelli del bootstrap e lascia fuori un
     tipo che non c'è più); `hiddenPositions` sono postazioni su cui la divisione allena — una che IVAO toglie è rifiutata sulla sua
     riga e resta visibile, così il TD la toglie —; `theoryExamUrl` un indirizzo http o https, la regola dei link della libreria, con
     `errors.url.absolute` e la lunghezza di `LinkWriteDtoValidator`.
  4. **L'errore di una riga porta il nome del campo della riga** (`minimumHours[0].rating`, `hiddenPositions[0].callsign`): il form
     generato non disegna un errore sulla lista intera, e quello sparirebbe.
  5. **La sezione è per ora solo la voce delle impostazioni**: la lista dei training a `/staff/training` è di A7, e fino ad allora TA
     e trainer non hanno voci nel back office.
  6. **«Il modulo non nomina IVAO»** (§10) è un modello e non una prova: nel codice del modulo nessun «ivao» fuori dal nome del
     prodotto, dal perimetro `IvaoHub.Core.Ivao` e dal pacchetto di Atmosphere; nei suoi file di lingua nessun indirizzo di IVAO;
     nessun numero accanto a un rating, nessun nome di rating o di tipo di postazione della rete in una stringa (i nomi letti dal
     vocabolario vero); nessun client HTTP. Due `Theory` mostrano che cosa prende e che cosa lascia passare, ed è provato che cade su
     file di prova messi e tolti. Il primo giro ha preso davvero una riga: la stringa di connessione della factory di `dotnet ef`
     nomina il database `ivaohub`, il nome del prodotto — eccezione allargata a ogni maiuscola.
- **Trovato, e scritto per chi viene dopo**:
  1. ⚠️ **Una posizione del TD senza indirizzo non riceve i messaggi al TD**: `NotificationService.Resolve` salta un membro senza
     indirizzo, e `NotificationUsesRecipientLocale` sceglie le mail per oggetto e indirizzo. I test del modulo seminano TC, TA1 e T03
     senza email, e il permesso sulle impostazioni lo danno con un grant a un VID — verificato leggendo i test dei contatti, come il
     piano chiedeva.
  2. ⚠️ **`web/e2e/address.spec.ts`** (del maintainer) va su `/training/team` e si aspetta il router delle pagine: una rotta del
     modulo che prendesse ogni `/training/…` (un `/training/$id`) la farebbe cadere; `/training/request`, `/training/mine` e
     `/training/sessions/$id` no.
  3. ⚠️ **Le impostazioni dei tour hanno lo stesso caso dello scostamento 4 qui sopra**: l'errore di una riga di `northSouthLevelCountries` o
     di `routeProcedurePrefixes` arriva come `…[0]` e non si vede. È codice del maintainer: detto al revisore.
  4. I VID **790009–790013** sono di A4; il prossimo libero è 790014.
- **Dopo le risposte di Carmine** (26 settembre 2026):
  1. **`m3/a4a-module-locales` unito nel branch**, con `main` (#131, #132, #136, #137): l'unico conflitto era in cima a
     `HANDOFF-M3.md`, risolto tenendo tutti i paragrafi, A4 sopra A4a sopra A3. `08` si è unito da solo, con il punto 3 di A4 come
     l'ha scritto #136.
  2. **A10 era già allineato** da #136 (il test «un TA crea un esame senza `Edit` e un trainer no», e «un TA inserisce un esame» nel
     «fatta quando»): quello che il revisore chiedeva su #131 ([commento][c131b]) non ha lasciato niente da fare qui.
  3. **Le chiavi nel C# del modulo** (heads-up di #138 del maintainer, in coda dopo #133): tutte quelle di `training.json` sono già
     scritte `training:…` (`training:nav.settings`, `training:errors.*`), e le sole nude sono del nucleo (`errors.*`). Niente da
     cambiare.
  4. **Il passo della coda**: #133 è stata unita alle 11:51 (fa575fb). `main` è entrato nel branch con un merge **che non porta
     file**: l'albero è lo stesso (122c54e) su cui sono girate tutte le suite qui sotto, come per A2 con #129. Tolti `(after #133)` e
     `Queued after #133.`, e #139 è passata a pronta.
- **Verificato, in locale, sul branch con A4a e `main` uniti** (26 settembre 2026): `dotnet build` senza avvisi; unità **751/751** (le
  719 di A4a e le 32 nuove); **integrazione intera senza filtro** **311/311** (le 307 e le 4 nuove); `pnpm lint`, `typecheck`,
  `format:check`, `i18n:check` verdi; `pnpm test` 488 in 63 file; `pnpm e2e` 91; **`pnpm e2e:full` 40** su un **banco nuovo**, con
  le due spec nuove (la sezione nella tavolozza, l'impostazione salvata e riletta, rimessa com'era; il trainer del banco con il solo
  `View` e un 403 sulle impostazioni); `pnpm gen:api` senza differenze. Prima, su branch temporanei con A4a, poi tolti: le stesse
  suite, e le classi nuove d'integrazione da sole. ⚠️ Un banco che ha girato con il seme di prima (esami anche ai trainer) lo tiene,
  perché un seme si applica una volta sola: per questo il banco nuovo.
- **Non verificato**: la CI (la dirà la PR); le impostazioni con le postazioni vere della divisione (sul banco e nei test ci sono
  quelle delle fixture, 43 d'aeroporto e 29 settori); la schermata guardata a mano con tutte e due le lingue e i due temi (`dalberone`
  l'ha vista sul banco il 26 settembre, in un'altra sessione).

[a134]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/134#issuecomment-5844363804
[c131b]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/131#issuecomment-5844102750

### A5 — Le voci della scheda

Design §1.4, §3.1; nota `il-tempo-per-la-data-e-le-voci-della-scheda`. Branch `m3/a5-sheet-items`.

1. **`trn_sheet_items`** (migrazione `AddSheetItems`): percorso (`Atc`, `Pilot`), rating (solo quelli con un training pratico, dal
   vocabolario di A1), sezione **pratica** (voto 1–5) o **teoria** (fatto / non fatto / da migliorare), titolo `Localized` con tutte
   le lingue della divisione, ordine, attiva. `IOwnedByDepartment` (il TD sempre), `IAuditable`, `[Audited]`.
2. **Lista e form generati** (`MapCrud`, `DataList`, `SchemaForm`) dietro `Training.ManageSheets`, con i filtri per percorso e
   rating; `/staff/training/sheets`.
3. **Una voce usata da un report non si elimina**, si disattiva: la domanda «è usata?» risponde no finché A9 non la sostituisce (come
   «ha PIREP?» in T6a dei tour), e il test la prova sostituendo la risposta.

**Test**: unit sulle regole della voce; integrazione: chi ha `ManageSheets` (per grant) crea una voce in due lingue, una lingua
mancante e un rating senza training rifiutati sul campo, chi non l'ha no; l'eliminazione di una voce «usata» rifiutata. E2e: una voce
scritta e riletta.
**Fatta quando**: dal back office si compone la scheda di un rating ATC con voci pratiche e di teoria in due lingue, e l'ordine si
rilegge uguale.

**Com'è andata** (26 settembre 2026, branch `m3/a5-sheet-items`, PR #140):

- **Classificata prima del codice** (`CLAUDE.md` §5): codice del modulo (caso a) dentro meccanismi che ci sono, usati così come sono
  (caso b) — `Localized<string>` con `LocalizedRules.Required`, `IOwnedByDepartment` con la maschera e il dipartimento base,
  `IAuditable`, `[Audited]`, `MapCrud` (filtri, ordine, ricerca, `Delete`), `DataList`, `ListFilter`, `SchemaForm`, il vocabolario dei
  rating (A1) attraverso `TrainingReference` —; **nessun file del nucleo**, nessuna nota nuova, nessuna domanda a Carmine.
- **Fatto**, come il perimetro qui sopra:
  1. **`trn_sheet_items`** (`Sheets/SheetItem.cs`) con la migrazione **`AddSheetItems`**, solo additiva: una tabella e un indice su
     percorso, rating e ordine. `RatingKind` e `SheetSection` (`Practice`, `Theory`) come testo in `ConfigureModuleConventions`;
     l'`Initial` di A4 non è toccata.
  2. **`/api/training/sheet-items`** (`Sheets/SheetItemEndpoints.cs`), una risorsa di `MapCrud` letta e scritta con
     `Training.ManageSheets`: `filter[kind]` e `filter[rating]`, l'ordine della scheda, la ricerca nel titolo nella lingua di chi legge.
     La lista porta la sigla del rating (`ratingShortName`) chiesta al vocabolario, perché il modulo non ne scrive. Le regole sono
     `SheetItemWriteDtoValidator`: un rating con un training pratico sul percorso della voce (`training:errors.ratingNotTrained` sul
     campo `rating`), il titolo in ogni lingua della divisione, l'ordine da 0 a 999. I validatori del modulo sono registrati per il
     motore con `AddValidatorsFromAssemblyContaining`, come nei tour (il ⚠️ di A4).
  3. **«È usata?»** è `ISheetItemReports`, che risponde no (`NoSheetItemReports`) finché A9 non lo sostituisce con le schede compilate:
     l'eliminazione di una voce usata è rifiutata sul campo `id` con `training:errors.sheetItemUsed`, e la voce si spegne (`isActive`).
     Il test lo prova sostituendo la risposta, come T6a con `ITourReports`.
  4. **`/staff/training/sheets`** e **`/staff/training/sheets/$id`** (`web/src/modules/training/screens/sheets.tsx`): la lista
     generata con i filtri per percorso e per rating — il secondo offre i rating del percorso scelto, e scegliere un rating sceglie il suo
     percorso —, il form generato, e la voce «Scheda di valutazione» nella sezione Training della barra dello staff, dietro
     `ManageSheets`. Il rating si sceglie fra quelli del server e il valore della scelta porta il percorso, come le soglie di A4. Una voce
     nuova chiesta dalla scheda di un rating parte su quella scheda, dopo la sua ultima voce.
- **Scostamenti e precisazioni, piccoli**:
  1. ⚠️ **Chi scrive le voci ha `ManageSheets` e `Edit`.** Il test qui sopra dice «chi ha `ManageSheets` (per grant) crea una voce»:
     nel test la ha insieme a `Training.Edit`, come TC e TAC per posizione (design §3.2). `ManageSheets` è ciò che chiedono la schermata e
     il motore; il guardiano chiede `Training.Edit` a ogni riga dello staff — il design §3.1 lo scrive: `Edit` «è anche il permesso che il
     guardiano chiede alle righe dello staff» —, come chiede `Tours.Edit` a chi ha `Tours.ManageAircraft` o `Tours.ManageRules`. Con
     `ManageSheets` da solo la schermata si apre e il salvataggio risponde 403, e il test lo fissa. **Nessun `[AlsoWrittenWith]`**: nella
     divisione i due permessi vanno sempre insieme, e l'estensione di A3 è per chi scrive senza `Edit` (il training, gli esami).
  2. **La lista si legge con `ManageSheets`**, non con `View` come gli aerei e le regole dei tour: il perimetro dice «dietro
     `Training.ManageSheets`», e il trainer leggerà le voci dalla scheda del suo training (A9), non da questa lista. TA e trainer non
     hanno la voce nel menu.
  3. **Le scelte dei rating sono un aiuto solo** (`screens/ratings.ts`), per le impostazioni di A4 e per la scheda, e la loro etichetta è
     salita da `settings.ratingChoice` a `ratingChoice`; `fromRatingChoice` legge il valore di una scelta, che prima si leggeva dentro
     `settingsFromFormValues`. Le impostazioni si comportano come prima (i loro test non sono cambiati).
  4. **Il form legge l'ultima voce della scheda all'apertura** (`isFetchedAfterMount`) e si ridisegna alla versione della riga
     (`key={rowVersion}`, come le segnalazioni dei tour): `SchemaForm` legge i suoi valori una volta sola, e un altro membro dello staff
     può aver scritto nel frattempo. Nel giro di una persona sola la cache è già fresca: il salvataggio rilegge le query aperte prima di
     tornare alla lista.
  5. **La lista non ha una colonna del percorso**: la sigla del rating lo dice, e i filtri lo scelgono. L'ordine predefinito è quello
     della scheda (`sort`), come le regole e i menu: una lista senza filtri mescola le schede, e i filtri la stringono a una.
  6. **Guardata a mano sul banco** (l'anteprima su 5090, in italiano e in inglese, tema scuro e chiaro): la lista, i due filtri, il form
     nuovo e quello di una voce esistente. La sezione si chiamava con la frase intera («Pratica — voto da 1 a 5»), che nella colonna
     della lista andava a capo su quattro righe, perché lista e form leggono le stesse parole: ora è una parola, «Pratica» o «Teoria», e
     come si segna la voce lo dice il suggerimento del campo.
- **Trovato, e scritto per chi viene dopo**:
  1. **A9** mette al posto di `NoSheetItemReports` la risposta delle schede compilate (`trn_evaluations`), nella stessa registrazione
     del modulo (`TryAddScoped`), come T11 dei tour con `PirepTourReports`. Le schede compilate fotografano titolo e sezione della voce
     (nota `il-tempo-per-la-data-e-le-voci-della-scheda`); una scheda nuova si fa con le voci **attive** del percorso e del rating del
     training, nell'ordine di `sort`.
  2. I VID **790014–790016** sono di A5; A3b usa 790040–790044 e 790050–790051: il prossimo libero è **790017**.
  3. La spec e2e ritrova le sue voci per `trn-bench`, che sta nel titolo in tutte e due le lingue: la ricerca della lista le trova in
     qualunque lingua cerchi.
  4. ⚠️ **Per il revisore, non toccato** (codice del maintainer): `TourTests.TheReleaseJobMakesAReadyTourPublicWithoutWritingIt` cade
     quando il job `tour-release`, pianificato da Quartz ogni quarto d'ora al secondo zero dentro l'host del test, parte nel secondo fra
     il rilascio che il test scrive (`UtcNow − 1 s`) e la sua chiamata a `RunAsync`: il job parte dall'inizio dell'ultima corsa riuscita
     e non trova più il tour. È successo una volta, alle 12:45:00, in una corsa intera sul merge con `main`; rifatta, è verde.
- **La coda si è sciolta durante la fase**: #139 (A4) e #138 del maintainer sono state unite alle 12:22, a PR #140 già aperta in bozza.
  `main` è entrato nel branch con un merge (48a1219) che porta #138 — il test di architettura sulle chiavi dei moduli con il namespace,
  che il C# di A5 rispetta già (`training:nav.sheets`, `training:errors.*`) —, e tutto è stato rifatto sul merge; tolti
  `(after #139)` e `Queued after #139.`.
- **Verificato, in locale, sul merge con `main`** (26 settembre 2026): `dotnet build` senza avvisi; unità **757/757** (le 751 di A4, le
  5 nuove e quella di #138); **integrazione intera senza filtro** **315/315** (le 311 e le 4 nuove; la classe nuova da sola 4/4); `pnpm
  lint`, `typecheck`, `format:check`, `i18n:check` verdi; `pnpm test` 494 in 63 file (6 nuovi); `pnpm e2e` 91; **`pnpm e2e:full` 41**
  su un **banco nuovo** (le 40 di A4 e la spec nuova); `pnpm gen:api` e `pnpm i18n:sync` senza differenze; `dotnet format
  --verify-no-changes` sui file C# toccati, test compresi; le regole di `core-guard` rifatte in PowerShell sul diff verso `main`:
  nessun file del maintainer, nessuno del nucleo. Prima del merge, sul branch da A4: unità 756, integrazione 315, `e2e:full` 41.
- **Non verificato**: la CI (la dirà la PR). **Che i test nuovi cadano su una copia indebolita del codice** — il rifiuto
  dell'eliminazione tolto, `[AlsoWrittenWith(ManageSheets, AlsoOnCreation = true)]` messo sull'entità —: la prova è stata rifiutata dalla
  modalità di permessi della sessione, e non l'ho aggirata; i test sono stati letti contro il codice. La scheda compilata e il report (A9).

### A6c — Nucleo: il suggerimento chiuso tiene la scelta

**Non era nel piano**: l'ha trovata la sessione di A6b il 26 settembre 2026, scrivendo lo smoke della richiesta (PR #144), come A4a fu
trovata scrivendo A4; `dalberone` ha scelto di farla come fase del nucleo a sé (`CLAUDE.md` §0 regola 6). Nota nuova
`2026-09-26-il-suggerimento-chiuso-tiene-la-scelta`, **Decisa** da Carmine il 27 settembre 2026 come raccomandato ([risposta su
#145][a145]), sulla domanda in un [commento su #145][q145]. Branch `m3/a6c-closed-suggestion`, da `main`, PR #145. **Non va in
coda**: tocca solo il nucleo del front end e non migra `TrainingDbContext`, quindi la PR va verso `main` accanto a #143 (A6a) e #144
(A6b), come A3b va avanti per conto suo. Sta qui, prima di A6, come A4a prima di A4.

1. **Il problema**: nel suggerimento chiuso di `SchemaForm` (`Suggest` con `suggestionsOnly`) chi scrive una parte del valore per
   cercare e poi clicca un'opzione si ritrova la casella con il valore di prima — vuota su una riga nuova —: la pressione porta il
   fuoco nella lista, l'`onBlur` della casella rimette quello che c'era perché il testo scritto non è un'opzione, la lista torna intera
   sotto il puntatore e il clic va a un'altra riga. Vale per ogni campo chiuso dell'hub: l'indirizzo di una voce del menu, le postazioni
   nascoste delle impostazioni del training, la postazione della richiesta, gli aerei dei tour.
2. **La proposta** (nota §3): **la casella e la sua lista sono un campo solo** — la regola del campo chiuso vale quando il fuoco esce da
   tutte e due, anche quando esce dalla lista; tornare nella casella dalla lista non ricomincia la ricerca; una scelta è «quello che
   c'era».

**Test** (smoke, file nuovo `web/e2e/closed-suggestion.spec.ts`, sull'indirizzo di una voce del menu con l'API finta): l'opzione
cliccata dopo averne scritto una parte è la scelta, e uscire dopo con un testo che non è un'opzione rimette la scelta; una pressione
nella lista che non sceglie tiene la ricerca, e uscire da lì rimette il valore di prima; tornando nella casella la ricerca continua;
Escape chiude e lascia il testo; la barra di scorrimento della lista si trascina. Cade sul codice di `main`.
**Fatta quando**: la nota è decisa da Carmine, e la spec nuova e quelle che c'erano passano.

**Com'è andata** (26 settembre 2026, branch `m3/a6c-closed-suggestion`, PR #145):

- **Classificata prima del codice** (`CLAUDE.md` §5): caso (b), il meccanismo c'è — il campo suggerito chiuso, nota
  `2026-09-08-dove-puo-portare-una-voce-di-menu` — e ha un difetto; si corregge nel suo posto unico. Un file del nucleo
  (`web/src/shared/forms/SchemaForm.tsx`, il componente `Suggest`), la nota nuova, la spec nuova; nessun file del maintainer, nessun
  test che non ho scritto, nessuna schermata cambiata.
- **Provato che la spec cade sul codice di oggi**, prima della correzione e di nuovo alla fine con `SchemaForm.tsx` di `main`: 4 prove
  su 5 cadono proprio sul difetto — `/pilots` al posto di `/calendar` dopo il clic sull'opzione; `/pilots` al posto di `cal` dopo la
  pressione su un'intestazione; 7 opzioni al posto di 1 tornando nella casella; `/pilots` al posto di `e` dopo aver trascinato la barra
  —; la prova di Escape passa, perché è una promessa del campo di oggi.
- ⚠️ **Scostamento dalla correzione proposta**: quella che A6b suggeriva — tenere il fuoco nella casella con
  `onMouseDown={(event) => event.preventDefault()}` sulla lista — l'ho **fatta per prima e scartata**. Le prove erano tutte verdi, ma
  **la barra di scorrimento della lista non si trascinava più**: Chromium non trascina una barra il cui `mousedown` è annullato
  (misurato su un riquadro di prova nella pagina dello smoke: 611 px di scorrimento senza, 0 con). Oggi aprire la lista e trascinarne la
  barra funziona, e le liste sono lunghe (le torri, i tipi di aereo). La forma rimasta è quella della nota §3, e la scelta fra le due è
  la domanda a Carmine; la prova della barra è nella spec, con le barre accese per quel file (headless le nasconde).
- **Provato che ogni pezzo serve**: tolto uno alla volta, cade la prova che lo tiene — senza la regola alla chiusura della lista, la
  seconda (il campo lasciato dalla lista tiene `cal`); senza la guardia sul ritorno nella casella, la terza (7 opzioni); senza la scelta
  scritta in `opened`, la prova del menu di `back-office.spec.ts` (clicca di nuovo la casella mentre la lista si sta chiudendo, e la
  lista si riapre stretta sulla scelta).
- **Trovato, per il revisore** (test del maintainer, non toccati):
  1. `back-office.spec.ts`, «…a page past the hundredth can still be chosen»: scrive e clicca, e **passava anche con il difetto**,
     perché la riga cliccata è per caso la prima anche della lista tornata intera (le pagine vengono prima delle schermate).
  2. `back-office.spec.ts`, «…offers the addresses that exist, and stays open to be read»: il secondo clic sulla casella arriva
     **mentre la lista si sta ancora chiudendo** (Radix ne anima l'uscita, e Playwright conta visibile un elemento che svanisce). Con la
     correzione è quel clic a tenere il quarto pezzo; con la prima forma, senza un clic che riaprisse la lista, passava lo stesso.
- **Verificato, in locale** (26 settembre 2026, sul branch da `main` a 4561b5b), le suite pesanti una alla volta: `dotnet build` senza
  avvisi; unità **757/757** e **integrazione intera senza filtro** **315/315** (come `main`: nessun C# toccato); `pnpm lint`,
  `typecheck`, `format:check`, `i18n:check` verdi; `pnpm test` 494 in 63 file (come `main`); `pnpm e2e` **96** (le 91 e le 5 nuove);
  **`pnpm e2e:full` 41** su un **banco nuovo**, senza la mappa di base; `pnpm gen:api` e `pnpm i18n:sync` senza differenze; le regole di
  `core-guard` rifatte in PowerShell sul diff verso `main`: nessun file del maintainer, uno del nucleo, la nota aggiunta. Con
  `git merge-tree` contro i branch di #143 e #144: conflitti solo in cima a `HANDOFF-M3.md` e sulla riga della tabella qui sopra, dove
  si tengono tutte le righe.
- **Non verificato**: la CI (la dirà la PR); browser diversi da Chromium — Firefox e Safari spostano il fuoco su una pressione con le
  loro regole, e la correzione legge solo `relatedTarget` e `document.activeElement`, ma nessuna prova ci ha girato —; uno schermo touch
  (il tocco su un'opzione, il dito che scorre la lista); una risposta di Carmine diversa da quella raccomandata.
- **Le correzioni della revisione** (28 settembre 2026; [i rilievi del revisore][r145], letti su `b065e49`: approvabile appena la nota
  registra la risposta):
  1. **La risposta di Carmine registrata** (afec5ce): la nota è *decisa*, con la riga di stato e il §5 che citano
     [il suo commento][a145]; lo dicono anche l'intestazione di questa sezione e le parti di A6c in `HANDOFF-M3.md`.
  2. **Tab dalla lista** (b04604f), una frase nella nota al §3.2. Il rilievo: la lista sta in un portale di Radix in fondo a `<body>`
     e dopo non c'è niente che prende il fuoco, quindi in un Chrome vero il Tab andrebbe alla barra del browser, la lista non si
     chiuderebbe sul Tab, e la regola si applicherebbe al fuoco o al clic successivo. **Misurato, non va così**: finché la lista è
     aperta Radix mette uno `<span data-radix-focus-guard tabindex="0">` all'inizio e in fondo a `<body>`, dopo il portale. Il Tab
     arriva lì, la lista si chiude e `/pilots` torna sul Tab stesso; Shift+Tab va all'ultimo link della pagina (`/legal`), con lo
     stesso esito. Misurato con una spec usa-e-getta, mai spinta, su una porta mia (4197), nel Chromium headless di Playwright e nel
     Chrome installato con la finestra (`channel: 'chrome'`, `--headed`). La frase dice questo, per scelta di `dalberone`, e che nessuna
     prova della spec lo tiene.
     - ⚠️ Letto, e raggiunto solo con uno script: se il fuoco lasciasse la pagina dalla lista senza posarsi altrove (un `blur()` da
       script), un clic di ritorno nella casella avrebbe `relatedTarget` nullo e conterebbe come un arrivo: `cal` diventerebbe «quello
       che c'era», e uscendo resterebbe `cal`. Nessuno dei gesti provati ci porta: il Tab si ferma sulla guardia; passando a
       un'altra scheda e tornando il fuoco resta nella lista, e la ricerca continua (ma con l'emulazione del fuoco di Playwright,
       quindi non è una prova piena).
  3. **Il punto 4 lo tiene solo il tempo** (rilievo, scritto qui con 6e42143): la scelta scritta in `opened` la vede una prova solo
     se si torna nella casella mentre la lista si sta ancora chiudendo, perché Radix ne anima l'uscita; a lista sparita il ritorno è
     un arrivo che rilegge il valore, e il punto 4 non serve. Il 26 settembre, tolto il punto 4, cadeva la prova del menu di
     `back-office.spec.ts`, e solo quella. Il revisore nomina anche la seconda metà della seconda prova della spec. Letta sul codice,
     la spec non lo tiene: la prova che sceglie è la prima, e prima di tornare nella casella aspetta che la lista sia nascosta
     (`toBeHidden`), quindi quel ritorno è un arrivo; la seconda non sceglie niente. Non l'ho rifatto oggi: togliere il pezzo per
     prova è stato rifiutato dalla modalità di permessi della sessione, e non l'ho aggirato.
  4. **La scelta da sola tastiera** (rilievo, scritto qui con 6e42143) — scrivere, freccia giù, Invio — **non funziona neanche su
     `main`**: la casella sta fuori dalla radice di `cmdk`, che quindi non riceve né le frecce né Invio. Per lo stesso motivo Invio in
     una casella chiusa invia il form, e questo conta per #144 (A6b). Non è di questa PR: il maintainer la prende come seguito.
  5. **`main` unito nel branch** (b468d24), come il revisore ha chiesto: #142–#172, fra cui A6a (#143), A3b (#135) e i rifiuti di un
     form nel nucleo (#152); nessuno tocca `SchemaForm.tsx` o la spec. Due conflitti, solo nei documenti, risolti tenendo tutto il
     testo di `main` e rimettendo le parti di A6c: la tabella delle fasi qui sopra (le righe A6a, A6b e A7 di `main`, con A6c prima di
     A6a, come A4a prima di A4) e la cima di «Lo stato» in `HANDOFF-M3.md` (A6c, poi A3b e A6a). Rispetto a `main` i due documenti
     perdono una riga sola, quella del «Parallelismo possibile» che A6c allunga.

  **Rifatto tutto sul merge** (28 settembre 2026), le suite pesanti una alla volta: `dotnet build` da capo (`--no-incremental`) senza
  avvisi; unità **817/817**; **integrazione intera senza filtro** **345/345**; `pnpm lint`, `typecheck`, `format:check`, `i18n:check`
  verdi; `pnpm test` 495 in 63 file; `pnpm e2e` **96** (con le 5 di `closed-suggestion.spec.ts`), sotto il lucchetto della 4173;
  **`pnpm e2e:full` 41** su un **banco nuovo** (porta 5097, `ivaohub_e2e_a6c`), senza la mappa di base; `pnpm gen:api` e
  `pnpm i18n:sync` senza differenze; le regole di `core-guard` rifatte in PowerShell sul diff verso `main` (qui non c'è la bash di
  Git): nessun file del maintainer, uno del nucleo (`SchemaForm.tsx`), con la nota aggiunta. ⚠️ La prima corsa di `pnpm e2e` è caduta
  su una prova del maintainer che A6c non tocca: `public-lists.spec.ts`, «a document whose slug is a department code is still
  reachable», il cui titolo non è comparso in 5 s. Il suo file da solo, tre volte (`--repeat-each 3`), ha dato 15/15, e la seconda
  corsa intera 96/96.
  **Non verificato**: la CI su questo head, che leggo una volta alla fine; la prova del punto 4 (qui sopra); browser diversi da
  Chromium e Chrome, e uno schermo touch, come prima.

[q145]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/145#issuecomment-5849495355
[a145]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/145#issuecomment-5855560813
[r145]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/145#issuecomment-5855612725

### A6 — La richiesta

Design §1.1, §1.2, §1.5-bis, §2.1, §2.2, §2.8, §4.1, §5.2; note `il-teorico-lo-dichiara-il-trainee`, `rating-e-postazioni-dal-nucleo`.
Branch `m3/a6-training-request`. Se in apertura risulta troppo per una PR, si divide (A6a il server, A6b le pagine), scritto qui.

1. **`trn_trainings` intera** (tutte le colonne del design §1.2, così A7–A9 non la rimigrano): `IOwnedByDepartment` con la maschera,
   `IAuditable`, `[Audited]`, `ISubmittedByMembers`, `IHasStakeholder` (il trainee), `IVisible` (`Members`, ristretto al trainee e a
   chi ha `Training.View`), `IHasFir` (il FIR della postazione, vuoto per i piloti), `IHasResourceScope` (`training:training:{id}`),
   `row_version`; area `Training`. Niente `IHasParticipants` (design §1.1).
2. **`trn_bans`, solo la tabella e la lettura**: la richiesta deve già rifiutare un trainee bannato; schermata e azioni in A10.
3. **La pagina `/training/request`** (membri): precompilati in sola lettura VID, nome, rating e ore — **mai l'email**
   (`ArchitectureTests.NoDtoCarriesAnEmailAddress`), anche se il form di PATS la mostra —; il percorso; il rating proposto dal
   vocabolario; per l'ATC la postazione scelta scrivendo, dalla directory di A2 meno `hiddenPositions`; i due testi liberi.
4. **I controlli sul server, in quest'ordine** (design §2.2), con i `ProblemDetails` campo per campo: il ban; una richiesta aperta
   **per percorso**; l'attesa **per percorso** (`cooldownDays`, `noShowCooldownDays`, `cooldown_waived`); le ore (`minimumHours`);
   **il mock exam** deciso dalla storia (l'ultimo `Completed` dello stesso percorso e rating con «pronto per il mock exam» e non già
   mock exam: la funzione nasce qui, la casella la scrive A9); **il teorico** attraverso `ITheoryExamSource`, nel modulo — **no** →
   `Rejected` con `TheoryNotPassed`, registrato, messaggio a schermo, nessuna mail; **sì** → `Requested`, `theory_confirmed_at`.
5. **L'annullamento** di una richiesta `Requested` da parte del trainee (l'eccezione del guardiano per chi modifica la propria riga:
   `ISubmittedByMembers` più `IHasStakeholder`).
6. **`/training/mine`** (membri): richieste e training, stato, attesa residua; gli endpoint del trainee hanno un DTO **senza** campi
   riservati.
7. **La mail `requestReceived`**: i tipi di notifica del modulo (`IModule.NotificationTypes`), i modelli in
   `web/src/modules/training/locales/{it,en}/training.json`.

**Test**: unit, **su un vocabolario di prova** (design §10): il rating proposto, le soglie di ore, l'attesa per percorso (training,
no-show, casella), il mock exam dalla storia, il ban con e senza scadenza; integrazione: una richiesta alla volta **per percorso**,
ATC e pilota insieme sì; un bannato non chiede; «no» al teorico registrato e senza mail, «sì» con la mail in coda; l'annullamento solo
da `Requested` e solo dal trainee; nessuno legge le richieste di un altro dai suoi endpoint. Smoke: la richiesta con la domanda sul
teorico.
**Fatta quando**: sul banco il trainee (A1) chiede il training del rating successivo al suo scegliendo una postazione e lo vede in
`/training/mine`; una seconda richiesta ATC è rifiutata, una da pilota passa.

**Divisa il 26 settembre 2026 in apertura**, come la frase qui sopra prevede e come T11 dei tour (`06`, T11a e T11b): il server da solo
è già una PR come quella di T11a — una tabella intera, i controlli nell'ordine del design, il teorico, l'annullamento, la mail e i loro
test —, e le pagine, con la finestra della domanda, lo smoke e il giro sul banco, la raddoppierebbero.

- **A6a — il server** (branch `m3/a6a-request-server`, preparato come `m3/a6-training-request` e rinominato prima del primo push): i
  punti 1, 2, 4, 5 e 7; dei punti 3 e 6 gli endpoint — quello che la pagina della richiesta legge (VID, nome, rating e ore, il rating
  proposto, le postazioni meno `hiddenPositions`, la domanda sul teorico con il suo link) e le richieste e i training del trainee, con
  un DTO senza campi riservati —; i test unit e d'integrazione. Il «fatta quando» lo prova un test d'integrazione, attraverso l'API.
- **A6b — le pagine** (branch `m3/a6b-request-pages`, da `m3/a6a-request-server`): il punto 3 e la pagina del punto 6, con «Annulla»;
  lo smoke della richiesta con la domanda sul teorico; il giro sul banco con `pnpm e2e:full` e il «fatta quando» di A6. Se una pagina
  chiede al server qualcosa che A6a non dà, è un cambio del modulo nella PR di A6b, detto nel suo «Com'è andata».

**Com'è andata (A6a)** (26 settembre 2026, branch `m3/a6a-request-server`, PR #143):

- **Classificata prima del codice** (`CLAUDE.md` §5): codice del modulo (caso a) dentro meccanismi che ci sono, usati così come sono
  (caso b) — `ISubmittedByMembers` con `IHasStakeholder` e l'eccezione del guardiano per chi modifica la propria riga, come il PIREP;
  `IOwnedByDepartment` con la maschera e il dipartimento base, `IAuditable`, `[Audited]`, `IVisible` con il filtro globale, `IHasFir`,
  `IHasResourceScope`; il servizio notifiche con i tipi del modulo; le impostazioni del modulo; il vocabolario dei rating (A1) e la
  directory delle postazioni (A2) —. **Nessun file del nucleo**, nessuna nota nuova, nessuna domanda a Carmine. `ITheoryExamSource` nasce
  nel modulo, come la nota `il-teorico-lo-dichiara-il-trainee` decide (§2 punto 3).
- **Fatto**, come il perimetro di A6a qui sopra:
  1. **`trn_trainings`** (`Training.cs`, alla radice del modulo: sotto, scostamento 7) con **tutte le colonne di §1.2**, e **`trn_bans`**
     (`Bans/TraineeBan.cs`: il VID, il motivo, fino a quando, chi l'ha tolto e quando; chi l'ha dato è chi ha scritto la riga), nella
     migrazione **`AddTrainings`**, solo additiva; `TrainingState` e `TrainingRejection` come testo in `ConfigureModuleConventions`;
     l'`Initial` e `AddSheetItems` non sono toccate. Il training è `ISubmittedByMembers`, `IHasStakeholder` (il trainee),
     `IOwnedByDepartment` con la maschera, `IAuditable`, `[Audited]`, `IVisible` (`Members`), `IHasFir` (il FIR della postazione),
     `IHasResourceScope` (`training:training:{id}`, `Training.ScopeOf`), con `row_version`; **niente `IHasParticipants`**. Il ban è
     `IOwnedByDepartment`, `IAuditable`, `[Audited]`, `IHasStakeholder`, e dice se vale in un momento (`Holds`).
  2. **Le regole della richiesta, in funzioni pure** (`Requests/RequestRules.cs`): `Standing`, dove sta il trainee su un percorso — il
     rating proposto, se sarà un mock exam, la prima regola che rifiuta con ciò che serve per dirlo —, e `WaitUntil`, `IsMockExam`,
     `MinimumHours`, `EndedAt`. L'ordine è quello di §2.2: il ban, una richiesta aperta per percorso, l'attesa, le ore; il mock exam non
     rifiuta niente; il teorico viene dopo tutte, nella richiesta, perché un «no» si registra.
  3. **`ITheoryExamSource`** (`Requests/ITheoryExamSource.cs`): `AsksTheTrainee` e `HasPassedAsync(vid, rating, declared)`.
     L'implementazione di oggi, `TraineeDeclaration`, risponde con la dichiarazione del trainee; registrata con `TryAddScoped`, così un
     test o un'altra fonte risponde prima. **No** → `Rejected` con `TheoryNotPassed` e `decided_at`, registrato, nessuna mail; **sì** →
     `Requested` con `theory_confirmed_at` e la mail.
  4. **Gli endpoint del trainee**, `/api/training/mine` (`Requests/RequestEndpoints.cs`, `Requests/TrainingRequests.cs`): `GET` la sua
     pagina — VID, nome, e per percorso il suo rating e le sue ore, il rating proposto, il mock exam, se si sceglie una postazione e
     quali (la directory meno `hiddenPositions`), la prima regola che rifiuta con fino a quando vale il ban, quale training è aperto,
     fino a quando si aspetta, la soglia di ore —; la domanda sul teorico (`asksTheory`, `theoryExamUrl`); i suoi training, dal più
     nuovo. `POST` la richiesta, `GET /{id}` un suo training, `POST /{id}/cancel` l'annullamento, solo da `Requested` (409 su una
     versione vecchia). Il DTO del trainee, **`TraineeTrainingDto`, non ha campi dello staff**, e nessun DTO ha l'email. Un altro membro
     riceve 404.
  5. **La mail `training.requestReceived`** (`TrainingNotifications`, dichiarata in `IModule.NotificationTypes`): oggetto e testo in
     `mail.training.requestReceived`, l'etichetta del profilo in `notifications.requestReceived`, in italiano e in inglese; nella lingua
     del trainee, con il percorso, la sigla del rating e la postazione, e «sarà un mock exam» quando lo è. `pnpm i18n:sync` e
     `pnpm gen:api`.
- **Scostamenti e precisazioni, piccoli**:
  1. **`reminded_at` è su `trn_trainings`**: il design lo nomina in §5.3 («una colonna `reminded_at` lo fa partire una volta sola») e non
     nella tabella di §1.2; la sessione in corso sta sul training, quindi anche il suo promemoria, e senza A8 rimigrerebbe la tabella.
  2. **Una colonna in più, `open_kind`**, scritta dal getter come `is_disputed` dei PIREP: il percorso finché il training è aperto, vuota
     dopo. In un indice unico con `trainee_vid` fa di «una richiesta aperta per percorso» (§2.2 punto 2) anche un vincolo del database:
     due richieste mandate nello stesso istante non passano tutte e due, e la seconda riceve lo stesso rifiuto della regola. Un test lo
     prova scrivendo senza i controlli.
  3. **Il dettaglio di un rifiuto sta nella pagina, non nel messaggio**: i `ProblemDetails` portano solo chiavi (`CrudProblems`), quindi
     «fino a quando» del ban (§2.2 punto 1) e «la soglia e le ore» (punto 4) arrivano alla pagina dal `GET` (`bannedUntil`, `waitUntil`,
     `minimumHours`, `hours`), che A6b mostra accanto al messaggio; il `POST` ridice solo la chiave, sul campo `kind`.
  4. **La richiesta rimanda il rating** che la pagina ha proposto, e il server la rifiuta sul campo `rating` se non è più quello che
     propone ora: i rating cambiano al login, e il trainee chiede ciò che ha visto.
  5. **Due regole senza cui una richiesta non si fa**, al loro posto nell'ordine: «niente da chiedere» (dopo il suo rating non c'è un
     training pratico, o l'hub non conosce il suo rating) prima delle ore, e «nessuna postazione offerta» per ultima (il rating si allena
     su una postazione e la divisione, meno quelle nascoste, non ne offre). E **le ore che l'hub non conosce non sono zero** (nota di
     A1): con una soglia sono un rifiuto loro, «esci ed entra di nuovo».
  6. **L'attesa conta dall'ultimo training chiuso sul percorso**, alla lettera di §2.2 — la data del report, della decisione o della
     chiusura, secondo lo stato —. Una richiesta passa solo a attesa finita, quindi è lo stesso che contare da ogni training, tranne se
     si cambiano le impostazioni a metà: conta l'ultimo.
  7. **`Training` sta alla radice del modulo** (`IvaoHub.Modules.Training.Training`): in un namespace sotto quello del modulo il nome
     della classe è nascosto dal namespace `IvaoHub.Modules.Training`, che C# trova prima degli `using`. Il commento della classe lo dice.
  8. **`Refusals`**, i rifiuti campo per campo, è una classe `internal` del modulo, come quella privata di `PirepSubmission` nei tour:
     il nucleo non ne ha una, e metterla lì sarebbe un cambio del nucleo con la sua PR. Detto al revisore.
  9. **I testi**: disponibilità, note, motivo di un rifiuto e di una chiusura fino a 2000 caratteri, come i testi del PIREP; i due
     commenti del report sono `text`, che il limite della riga non conta, e il loro limite è di A9.
- **Trovato, e scritto per chi viene dopo** (anche in `HANDOFF-M3.md`):
  1. ⚠️ **Il filtro globale nasconde un training a chi non è entrato**: fuori da una richiesta — un test che pulisce o conta — la lettura
     vuole `IgnoreQueryFilters()` (`TrainingRequestTests` lo fa; nel codice di `src/` è vietato fuori dal motore CRUD).
  2. **Per A7**: lo staff legge il training con `Training.View`, con un DTO suo; la funzione unica che toglie i campi riservati al trainee
     della riga è di A9 (nota `le-note-riservate-e-il-trainee`). Il grant del trainer ha lo scope `Training.ScopeOf(id)`.
  3. **Per A8**: `reminded_at` è sulla riga; **la fine di una sessione non è una colonna del training**: il design la tiene nella
     disponibilità scelta (`chosen_slot_id`), e l'override, che non ne ha una, deciderà dove tiene la sua. Il tempo da quando le date
     sono proposte (`responseReminderDays`, `maxResponseDays`) si legge dalle disponibilità.
  4. **Per A12**: le colonne di persona seguono la convenzione (`trainee_vid`, `trainer_vid`, `decided_by`, `assigned_by`, `closed_by`;
     nei ban `vid` e `lifted_by`) e il nucleo le rende pseudonimo da solo; i testi liberi li toglie `TrainingPersonalData` (A12b).
  5. I VID **790017–790021** sono di A6a.
- **La coda si è sciolta prima della PR**: #140 (A5) è stata unita alle 18:44, mentre girava la prima integrazione intera. `main` è
  entrato nel branch con un merge (da90c3e) che porta A5, che il branch aveva già, e la #141 del maintainer (la sessione master:
  `CLAUDE.md`, `CONTRIBUTING.md`, il template, il piano, `HANDOFF.md`, una nota) — nessun file del modulo —, e tutto è stato rifatto sul
  merge. La PR di A6a è nata verso `main` senza `(after #140)`.
- **Verificato, in locale, sul merge con `main`** (26 settembre 2026): `dotnet build` senza avvisi; unità **767/767** (le 757 di A5 e le 10
  nuove); **integrazione intera senza filtro** **322/322** (le 315 e le 7 nuove; la classe nuova da sola 7/7, al primo giro); `pnpm lint`,
  `typecheck`, `format:check`, `i18n:check` verdi; `pnpm test` 494 in 63 file (nessun file nuovo: A6a non ha codice del front end);
  `pnpm e2e` 91; **`pnpm e2e:full` 41** su un **banco nuovo**, che all'avvio applica `AddTrainings`; `pnpm gen:api` e `pnpm i18n:sync`
  senza differenze dopo il commit che li porta; `dotnet format --verify-no-changes` sui file C# toccati, test compresi; le regole di
  `core-guard` rifatte in PowerShell sul diff verso `main`: nessun file del maintainer, nessuno del nucleo. Prima del merge, sul branch da
  A5: unità 767, integrazione 322.
- **Non verificato**: la CI (la dirà la PR). **Due richieste nello stesso istante** attraverso l'API: la chiave del database è provata
  scrivendo senza i controlli, e il ramo che la trasforma nel rifiuto `requestOpen` (`DbUpdateException` con «Duplicate») è letto, non
  eseguito, come quello dei PIREP. **Che i test nuovi cadano su una copia indebolita del codice**: non tentato, perché la modalità di
  permessi della sessione di A5 l'ha rifiutato; i test sono stati letti contro il codice. Le pagine e il «fatta quando» sul banco sono di
  A6b.

**Com'è andata (A6b)** (26 settembre 2026, branch `m3/a6b-request-pages`, PR #144, in coda dopo #143):

- **Classificata prima del codice** (`CLAUDE.md` §5): codice del modulo (caso a) dentro meccanismi che ci sono, usati così come sono
  (caso b) — le rotte dei membri del manifest (`area: 'member'`, come il report dei tour: il login davanti, e al ritorno la stessa
  pagina), `SchemaForm` con il suo `id` e `actionsElsewhere`, il suggerimento chiuso (`suggestionsOnly`), `ConfirmDialog` con una domanda
  nei `children` e `confirmDisabled` (G14, T7a), `Notice`, `useNotice`, `EmptyState`, `RatingBadge`, `describeProblem`, `useMoment` —.
  **Nessun file del nucleo**, nessuna nota nuova, nessuna domanda a Carmine; **nessun cambio del server**: le due pagine leggono
  `GET /api/training/mine` di A6a così com'è (`pnpm gen:api` senza differenze).
- **Fatto**, come il perimetro di A6b qui sopra:
  1. **`/training/request`** (`web/src/modules/training/screens/request.tsx`): i dati del trainee in sola lettura — VID, nome, e del
     percorso scelto il rating (`RatingBadge`) e le ore; **mai l'email** —; il percorso, ATC o pilota, a scelta con accanto il training
     che sarebbe, e nell'indirizzo (`?kind=`); il training proposto, con «questo sarà un mock exam, come concordato con il trainer» quando
     il server lo dice (R.6); per l'ATC la postazione fra quelle offerte (un suggerimento chiuso); disponibilità e note. **«Richiedi
     training» apre la domanda sul teorico** (R.2: «Hai superato l'esame teorico per *rating*?», con il sito dell'esame quando
     `theoryExamUrl` c'è), sì o no, e «Invia la richiesta» si accende solo con una risposta. Con il **«no»** la pagina dice, al posto del
     form, «Prima di richiedere il training devi superare l'esame teorico *rating*», che la richiesta è registrata come rifiutata e che
     non arriva nessuna mail; con il **«sì»** la conferma nell'angolo e `/training/mine`. Un percorso che il server rifiuta mostra la
     frase del rifiuto e, dalla stessa risposta, fino a quando vale il ban (o «finché il dipartimento training non lo toglie»), che il
     training aperto va chiuso prima, fino a quando e quanti giorni dura ancora l'attesa, la soglia di ore e le ore.
  2. **`/training/mine`** (`screens/mine.tsx`): per ogni percorso il rating, le ore, il training che si può chiedere con «Richiedi
     training» (verso `/training/request?kind=…`) o il rifiuto con il suo dettaglio — **l'attesa residua** compresa —, il mock exam, e
     «pronto per l'esame»; poi **le richieste e i training**, dal più nuovo: lo stato (le parole di R.4), percorso, rating, postazione,
     mock exam, quando è stata chiesta e il momento del suo stato, il motivo di un rifiuto (dell'hub o dello staff), le caselle del report
     («pronto per il mock exam», «pronto per l'esame»), i due testi del trainee, e **«Annulla la richiesta»** solo su `Requested`, con
     `ConfirmDialog` e la versione che il trainee ha visto.
  3. **Le funzioni pure** in `screens/trainee.ts` (il percorso scelto, il dettaglio di un rifiuto, dove va un rifiuto della richiesta, il
     momento di uno stato, «pronto per l'esame», i giorni che mancano, le ore) e i pezzi comuni alle due pagine in `screens/parts.tsx`;
     `api.ts` (`mineQuery`, `useRequestTraining`, `useCancelTraining`, che rileggono la pagina), `schemas.ts` (`requestSchema`,
     `requestFromFormValues`, `requestSearchSchema`), le due rotte nel manifest, le parole in `training.json` (`request`, `mine`,
     `states`, `refusal`, `mockExam`, `theoryExam`, `unknown`), in italiano e in inglese, copiate da `pnpm i18n:sync`.
  4. **I test**: Vitest `schemas.test.ts` (4 nuovi) e `screens/trainee.test.ts` (11); lo smoke `web/e2e/training-request.spec.ts` (5,
     con l'API finta: la richiesta con il «sì», il «no», i rifiuti al loro posto, un percorso rifiutato con il suo dettaglio e il mock
     exam dell'altro, `/training/mine` con l'annullamento); il giro sul banco `web/e2e/full/training-request.spec.ts`, **il «fatta
     quando» di A6**: il trainee del banco chiede il training del rating dopo il suo scegliendo una postazione e lo trova in
     `/training/mine`; una seconda richiesta ATC è rifiutata (la pagina non la offre, e il server rifiuta quella mandata di lato con
     `requestOpen` sul campo `kind`); una da pilota passa. All'inizio e nel `finally` annulla le richieste rimaste in attesa.
- **Scostamenti e precisazioni, piccoli**:
  1. **La domanda sul teorico è `ConfirmDialog`**, il componente dell'elenco chiuso che fa già domande (G14): la risposta sta nei suoi
     `children`, e la conferma **manda il form generato** per il suo `id` (`requestSubmit`), così i rifiuti del server arrivano campo per
     campo come in ogni form. La finestra si apre dal suo pulsante, quindi prima che il form sia mandato: il form non ha regole nel
     browser (sotto, 2), e un rifiuto del server dopo la risposta non registra niente — il teorico viene per ultimo (A6a) —; la domanda
     si fa di nuovo, da capo, a ogni «Richiedi training». Il pulsante di `ConfirmDialog` è solo `ghost` o `secondary`: «Richiedi
     training» è `secondary`.
  2. **Nessuna regola nel browser**: la postazione obbligatoria e la lunghezza dei testi sono del server, che le rifiuta sul campo con le
     parole dei file di lingua; `.min(1)` o `.max()` di zod le direbbero con le frasi inglesi di zod.
  3. **Dove va un rifiuto**: quelli sui campi del form (postazione, testi) sul loro campo; gli altri (`kind`, `rating`, `theoryPassed`)
     sopra il form, e la pagina rilegge il `GET`, così un rifiuto del percorso mette al posto del form la sua frase con i dettagli. Il
     form ha per chiave il percorso e basta: un rating che il server ha cambiato tiene i testi scritti e il rifiuto che lo dice.
  4. **«Annulla» è «Annulla la richiesta»**, e nella finestra «Sì, annulla la richiesta»: il pulsante della finestra che la chiude è
     «Annulla» (`common.cancel`), e due «Annulla» uno accanto all'altro direbbero due cose opposte.
  5. **Le date sono in UTC**, e la frase lo dice (l'attesa, il ban, una sessione); il giorno di una richiesta, di una decisione o di una
     chiusura è una data.
  6. **Come ci arriva un membro**: dall'indirizzo e dalla mail della richiesta ricevuta (`/training/mine`). Il menu pubblico è
     editoriale — il TD ci mette una voce —, e `/training` con il pulsante e il blocco di `/me` sono di A10.
- **Trovato, e scritto per chi viene dopo** (anche in `HANDOFF-M3.md`):
  1. ⚠️ **Un difetto del nucleo, non toccato**: nel **suggerimento chiuso** di `SchemaForm` (`Suggest`, `suggestionsOnly`), **chi scrive
     per cercare e poi clicca un'opzione perde la scelta**: la casella torna vuota. Al `pointerdown` sull'opzione la casella perde il
     fuoco, `onBlur` rimette il valore di prima perché il testo scritto non è un'opzione, la lista si ridisegna intera sotto il
     puntatore, e il clic non arriva più all'opzione. Cliccare la casella e poi l'opzione, o scrivere il nominativo intero, funziona.
     Misurato nel browser il 26 settembre 2026 (in jsdom non si vede: non consegna gli eventi di puntatore). Vale per ogni campo chiuso
     dell'hub (la voce del menu, le postazioni nascoste delle impostazioni, qui la postazione della richiesta). È codice del nucleo:
     detto al revisore su #144, e le spec scelgono la postazione dall'elenco o la scrivono intera, e lo dicono. **La correzione è la
     fase del nucleo A6c, PR #145** (da `main`, non in coda, nota `2026-09-26-il-suggerimento-chiuso-tiene-la-scelta`, «Proposta»): la
     casella e la sua lista sono un campo solo, e la regola del campo chiuso vale quando il fuoco esce da tutte e due. L'idea scritta qui
     per prima — tenere il fuoco nella casella annullando la pressione sulla lista — A6c l'ha provata e scartata: la barra di scorrimento
     della lista non si trascina più.
  2. **`pnpm i18n:check` non legge le chiavi con il namespace**: il suo schema (`[\w.-]+`) si ferma ai due punti di `t('training:…')`,
     quindi le chiavi dei moduli non sono controllate. Le spec leggono le parole dai file di lingua e cadono su una chiave mostrata
     nuda. Detto al revisore (lo script è del nucleo).
  3. Nessun VID nuovo: A6b non ha test d'integrazione. Il prossimo libero resta **790022**.
  4. ⚠️ **Una conferma nell'angolo si cerca con il testo esatto** (`{ exact: true }`): il toast di Radix la annuncia anche, per un
     momento, in una `span` «Notification …» con `aria-live`. In locale era già sparita al controllo; **la prima CI della PR (a0961ae) è
     caduta lì**, nello smoke, e il test è stato corretto.
  5. **Guardato a mano**, e per chi guarda dopo: il pulsante «Richiedi training» è grigio (`secondary`, scostamento 1), più debole del
     blu di un form senza domanda; un `triggerVariant` primario di `ConfirmDialog` sarebbe un'estensione del nucleo, e sta con il
     difetto qui sopra fra le cose dette al revisore. L'intestazione del sito è larga 1044 px su un telefono di 375 px, in ogni pagina e
     anche nella home: è del nucleo, non di questa fase.
- **La coda**: A6b è nata in coda dopo #143 (A6a, pronta con la CI verde, in attesa della sessione master): la PR #144 è in bozza con
  `(after #143)` e `Queued after #143.`. Quando #143 sarà unita, il passo della coda (`CONTRIBUTING.md`, «Phases in a queue»): `main` nel
  branch con un merge, build e tutti i test di nuovo, via la coda, e la PR pronta con la CI verde.
- **Verificato, in locale** (26 settembre 2026, sul branch da `m3/a6a-request-server`, cece262): `dotnet build` senza avvisi; unità
  **767/767** (come A6a: la fase non ha C#; `TrainingArchitectureTests` legge anche il TypeScript nuovo del modulo, ed è verde);
  **integrazione intera senza filtro** **322/322** (come A6a); `pnpm lint`, `typecheck`, `format:check`, `i18n:check` verdi; `pnpm test`
  **509** in **64** file (le 494 in 63 di A6a e 15 nuovi); `pnpm e2e` **96** (le 91 e le 5 nuove); **`pnpm e2e:full` 42** su un **banco
  nuovo** (le 41 e la spec nuova). La prima corsa, anch'essa su un banco nuovo, è finita 41/42: `tours-rules.spec.ts` è caduta su
  `net::ERR_NO_BUFFER_SPACE`, un errore di socket di Windows nella navigazione di Chromium e non un'asserzione; rifatta sul banco
  ricreato, 42/42. `pnpm gen:api` senza differenze (nessun endpoint cambiato); `pnpm i18n:sync` senza differenze dopo il commit che porta
  le parole; nessun file C# toccato; le regole di `core-guard` rifatte in PowerShell sull'intervallo della fase e sul diff verso `main`:
  nessun file del maintainer, nessuno del nucleo. **A mano**, sul banco di anteprima (127.0.0.1:5090, `ivaohub_preview`, spento il banco di
  A5 su richiesta a quella sessione): il trainee chiede ADC su una postazione con il «sì» e lo trova in `/training/mine`, risponde «no» sul
  percorso pilota e legge la frase di R.2, annulla la richiesta ATC; in italiano e in inglese, tema chiaro e scuro, e largo 375 px.
- **La CI** (`build-test` e `core-guard`) è verde dopo la correzione dello smoke: su e156e9b ed e178b1b, e sul merge del passo della coda
  dopo #143 (f5e3cd6).
- **Non verificato**: **che i test nuovi cadano su una copia indebolita del codice**: non tentato, perché la modalità di permessi l'ha
  rifiutato in A5; i test sono stati letti contro il codice (lo smoke è caduto sulla sua prima versione, ed è così che è venuto fuori il
  difetto del nucleo). **La mail del «sì» in Mailpit sul banco**: il giro non la legge (il test d'integrazione
  di A6a prova l'intento in coda). **Un ban, un'attesa, una soglia di ore, il sito dell'esame e un 409 su un annullamento vecchio,
  attraverso le pagine sul banco**: il banco non ne ha (nessun ban, nessun training completato, nessuna soglia, nessun `theoryExamUrl`);
  le pagine li mostrano con l'API finta dello smoke, e il lato del server è dei test d'integrazione di A6a.
- **Le correzioni della revisione** (28 settembre 2026, [la revisione](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/144#issuecomment-5855612519);
  la PR era tornata in bozza perché il passo della coda dopo #143 l'aveva segnata pronta prima di questa correzione,
  [commento](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/144#issuecomment-5857989492)). Su un branch temporaneo da
  `origin/m3/a6b-request-pages` (f5e3cd6), spinto sul branch della fase; il merge verso l'alto della coda lo fa una volta sola la sessione
  che coordina le correzioni.
  1. **`main` nel branch** (3c79786), come il master ha chiesto su #144
     ([commento](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/144#issuecomment-5859555627)): A3b (#135) e le PR del maintainer
     #160–#172. L'unico conflitto era in `HANDOFF-M3.md`. L'intestazione resta quella di A6b, che i branch sopra riscrivono per conto
     loro; in «Lo stato» restano tutti i paragrafi, quello di A3b subito sotto quello di A6b, così il merge verso l'alto non tocca le righe
     che i branch sopra hanno cambiato. `08` si è unito da solo. **Il catalogo di A3b non cambia niente di A6b**: le pagine del trainee sono
     solo front end, e gli endpoint di A6a che leggono chiedono solo di essere entrati (`HubPolicies.SignedIn`); il training non ha
     `IHasAssignee`, `[AlsoWrittenWith]` né permessi `OnlyForAssignee` (li porta A7, per il trainer). `TrainingRequestTests` è verde dopo il
     merge.
  2. **Invio nella postazione mandava la richiesta senza la domanda sul teorico** (da correggere; 2d20da4). Con `asksTheory` il form non
     ha un pulsante di invio (`actionsElsewhere`), e la postazione è la sua sola casella di una riga. Per l'invio implicito dell'HTML, Invio
     lì manda il form, e `form.submitHint` lo dice anche a chi usa un lettore di schermo. La richiesta partiva senza risposta se la finestra
     non si era mai aperta (il server risponde `theoryPassed: errors.required`), o con una risposta data e poi annullata.
     - **Ora ogni invio del form passa da una guardia** (`letThrough`, sull'evento `submit` in fase di cattura, prima che `SchemaForm` lo
       veda). Va avanti solo l'invio che parte dalla conferma della finestra, dentro `requestSubmit`, con la risposta data; ogni altro invio
       **apre la domanda come fa il pulsante** e non manda niente. La risposta si dimentica quando la finestra si chiude.
     - **Perché Invio apre la domanda**, invece di non fare niente come nella strada del revisore (un ref che `sendWithAnswer` imposta e
       `submit` controlla): con un Invio muto, «Premi Invio per salvare» sarebbe falso per questo form; così Invio porta al salvataggio,
       passando dalla domanda. E la guardia prende la conferma nel momento in cui l'invio comincia, quindi una conferma che il form poi
       rifiutasse non resterebbe indietro per l'invio dopo.
     - **La finestra si apre dal suo pulsante** (un `click` sul pulsante di `ConfirmDialog`), perché `ConfirmDialog` tiene per sé il suo
       `open`. Un `open` controllato sarebbe un cambio del nucleo, e qui non serve.
     - **Lo smoke** ha due casi nuovi: Invio con la finestra mai aperta; «No», «Annulla», poi Invio. **Cadono tutti e due sul codice di
       prima**, provato prima della correzione: la domanda non compare, e la richiesta parte.
  3. **«Torna alla richiesta» dopo un «no» perdeva i testi** (nit; edfe7cb), perché il pulsante di `Declined` rimontava `RequestForm`. Il
     form ora resta montato, **solo nascosto** mentre si legge il rifiuto dell'hub, e tornando c'è com'era. Il caso del «no» dello smoke
     torna indietro e rilegge postazione e disponibilità; **cade sul codice di prima** (la postazione torna vuota).
  4. **L'etichetta della postazione** (nit; 2912154): `` `${callsign} — ${name}` `` è nella chiave `training:positionChoice`, nelle due
     lingue, come `ratingChoice`, con una funzione sola accanto a `ratingOptions` (`positionLabel`, `screens/ratings.ts`). La stessa
     composizione era scritta anche nelle impostazioni (A4), che ora usano la stessa funzione. Il giro sul banco cerca l'opzione con le
     stesse parole, lette dal file di lingua.
  5. **La riga vecchia di «Non verificato»** qui sopra (nit): la CI dopo la correzione dello smoke è verde, e ora la voce «La CI» lo dice.
  6. **Restano fuori, con il perché**:
     - **I nit di `screens/mine.tsx`**: il `CardRoot` di Atmosphere al posto della card fatta a mano, il separatore `' · '` in una chiave,
       `line-clamp-3` sui testi del trainee senza un modo di leggere il resto. A8b, A9b e A10b cambiano `mine.tsx`, e il merge verso l'alto
       andrebbe in conflitto: **li fa una fase in cima alla coda**.
     - **`canCancel` dal server** al posto di `isCancellable`, e `readyForExam`, in `trainee.ts`: oggi dicono la stessa cosa di A6a. Il
       revisore dice di farlo **quando A7 e A8 aggiungono stati**.
     - **`src/IvaoHub.Modules.Training/Refusals.cs`**: la copia del modulo la toglie **A10c**, che usa `Refusals` del nucleo (#152), come è
       scritto in `HANDOFF-M3.md` sul branch di A10b.
     - **La scelta da tastiera nel suggerimento chiuso** (scrivere, freccia giù, Invio) non funziona, anche su `main`. `Suggest` è del
       nucleo, e il maintainer la prende come seguito (revisione di #145): non toccato.
  7. **Verificato, in locale** (28 settembre 2026, sul merge e le tre correzioni, 2912154): `dotnet build` senza avvisi; unità **817/817**
     (le 767 di A6b e le nuove di `main`); **integrazione intera senza filtro 345/345**; `pnpm lint`, `typecheck`, `format:check`,
     `i18n:check` verdi; `pnpm test` **510** in **64** file; `pnpm e2e` **98** (le 96 e i due casi di Invio); **`pnpm e2e:full` 42/42** su un
     banco nuovo (127.0.0.1:5096, `ivaohub_e2e_a6b`, creato dal primo avvio). `pnpm gen:api` senza differenze; `pnpm i18n:sync` fatto, e le
     copie in `locales/` sono nel commit dell'etichetta. **I test nuovi cadono sul codice di prima**: con lo smoke della richiesta scritto e
     `request.tsx` ancora com'era (dopo il merge), 3 casi su 7 cadono — i due di Invio (la domanda non compare) e quello del «no» al
     ritorno (la postazione è vuota) —; con le correzioni, 7 su 7.
  8. **Non verificato**: Invio con un lettore di schermo vero, e in un browser diverso da Chromium (lo smoke gira solo lì; la guardia sta
     sull'evento `submit`, che l'invio implicito manda in ogni browser). Invio sul banco: il giro completo sceglie la postazione dall'elenco
     e manda con il pulsante, come prima; Invio lo prova lo smoke, con l'API finta. Nessuna prova a mano sul banco di anteprima.
- **`main` dopo #177, e un Invio in più** (28–29 settembre 2026; [la revisione dopo le correzioni](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/144#issuecomment-5877191956),
  approvabile appena `main` entra nel branch, e [la correzione che vale](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/144#issuecomment-5877395930):
  il maintainer ha unito prima #177, quindi l'adattamento spetta a questo branch). Su un branch temporaneo da
  `origin/m3/a6b-request-pages` (b4bd304), spinto sul branch della fase con un push solo; i branch sopra prendono `main` al loro passo
  della coda.
  1. **`main` nel branch, in due merge**:
     - c90dea9, `origin/main` a 1ae9100: A6c (#145), le correzioni di hosting del maintainer #173–#176, **#177** (la scelta da tastiera
       nel campo suggerito, nota `2026-09-28-il-suggerimento-dalla-tastiera`) e #179 (i giri dei tour si riprendono i loro report); #171
       e #172 c'erano già da 3c79786;
     - 4b9f6bd, `origin/main` a efe057a: **A11a (#159)**, unita mentre giravano le suite del primo merge. Senza, #144 restava in
       conflitto, e una PR in conflitto non ha la CI e non si unisce; la sessione che coordina le correzioni era d'accordo. Se `main` si
       muove ancora prima del push, non si insegue.
     - Tutte e due le volte l'unico conflitto era in `HANDOFF-M3.md`: l'intestazione resta quella del branch, che i branch sopra
       riscrivono per conto loro; in «Lo stato» i blocchi nuovi di `main` vanno subito sotto quello di A6b, nel loro ordine (A11a, poi
       A6c, sopra A3b), così il merge verso l'alto non tocca le righe che i branch sopra hanno cambiato. Dei blocchi di `main` non si
       toglie niente; `08` si è unito da solo.
     - **A11a e le pagine di A6b**: le pagine leggono gli endpoint del trainee di A6a, che chiedono solo di essere entrati; la regola del
       FIR di A11a vale per i grant al team di un FIR, che il training avrà con A11b. L'integrazione intera, con `TrainingRequestTests`, è
       verde dopo il merge.
  2. **Un Invio in più** (c3db117) nel caso dello smoke «Enter in the position asks the question, as the button does». Da #177 Invio su
     un'opzione accesa la sceglie e non manda il form, e in un campo chiuso che si cerca è accesa la prima opzione mostrata. Il caso
     scriveva `XXAA_TWR` e premeva Invio una volta: quell'Invio ora sceglie la postazione, e la domanda non compare. Con un Invio in più
     dopo il `fill` il primo sceglie, il secondo manda il form e `letThrough` fa la domanda, come prima; le asserzioni non cambiano. Il caso
     «an answer taken back with «Cancel»» passa così com'è (non si cerca niente, niente è acceso, e Invio è del form), e `letThrough` non
     cambia. **Misurato**: la spec sul solo primo merge (c90dea9) dà 6 su 7, e cade proprio quel caso; con la riga, 7 su 7.
  3. **Verificato, in locale** (29 settembre 2026, su 4b9f6bd, con A11a): `dotnet build` senza avvisi; unità **836/836** (le 825 e
     le 11 di A11a); **integrazione intera senza filtro 359/359** (le 353 e le 6 di A11a); `pnpm lint`, `typecheck`, `format:check`,
     `i18n:check` verdi; `pnpm gen:api` e `pnpm i18n:sync` senza differenze; `pnpm test` **529** in **68** file; `pnpm e2e` **111/111**
     al primo giro (le 110 e quella di A11a), sotto il lucchetto della porta 4173; **`pnpm e2e:full` 42/42** al primo giro su un banco
     nuovo (127.0.0.1:5096, `ivaohub_e2e_a6b_main2`, creato dal primo avvio). Prima, sul solo primo merge (c3db117), tutto verde anche
     lì: unità 825/825, integrazione 353/353, `pnpm test` 527 in 67 file, smoke 110/110, `e2e:full` 42/42 su `ivaohub_e2e_a6b_main`. Le
     regole di `core-guard` rifatte in PowerShell su `origin/main...HEAD`: nessun file del maintainer, nessuno del nucleo.
  4. **Non verificato**: come sopra (8), Invio con un lettore di schermo vero, in un browser diverso da Chromium e sul banco; nessuna prova
     a mano sul banco di anteprima. Il primo caso dello smoke sceglie ancora la postazione dall'elenco, e il suo commento dice ancora il
     perché di prima di A6c (un clic dopo aver scritto andava perso): con A6c e #177 dentro, una spec può scrivere una parte del
     nominativo. Resta com'è: l'adattamento chiesto era una riga. La CI su questo head si legge dopo il push.

### A7 — Accettare, rifiutare, assegnare

Design §2.3, §2.4, §3.2, §3.3, §3.4, §4.2, §5.2, §5.3; note `chi-conduce-e-chi-scrive-un-training`, `il-teorico-lo-dichiara-il-trainee`
(il promemoria). Branch `m3/a7-approve-and-assign`.

1. **`[AlsoWrittenWith]` sul training** per `Training.Approve`, `Training.Assign` e `Training.Conduct` (A3).
2. **Accetta** e **rifiuta con un motivo** (`Training.Approve`); mail `requestAccepted`, `requestRejected` (con il motivo).
3. **Assegna** (`Training.Assign`: TC e TAC; i capi FIR in A11b): i trainer proposti sono lo staff del training che ha fatto login
   almeno una volta — HQ, TC, TAC, TA e trainer —, con il rating del percorso almeno quello allenato (vocabolario), mai il trainee; il
   server ricontrolla il rating. **Scrive il grant** `Training.Conduct` con lo scope del training (`ModuleGrants`) e toglie quello del
   trainer di prima; mail `trainerAssigned` a trainee e trainer. Un training resta `Accepted` senza trainer quanto serve.
4. **Il job notturno `training-expiry`**, prima metà: toglie i grant con scope dei training chiusi (la chiusura per tempo la aggiunge
   A8). Convenzioni di M2: `[DisallowConcurrentExecution]`, una riga in `hub_jobs_log`, mai un'eccezione, `RunAsync` per i test.
5. **Le pagine dello staff**: `/staff/training`, lista generata con i filtri Da approvare, Da assegnare, In corso, Da chiudere,
   Storico; `/staff/training/{id}`, schermata dedicata come la pagina di validazione di M2 — la richiesta con ore e rating, il
   **promemoria del teorico** con il link di `theoryExamUrl`, le azioni che chi guarda può fare.

**Test**: integrazione: un TA (per grant, senza `Edit`) accetta e non assegna; TC assegna un trainer adatto e non uno con il rating
sotto; **nessuno approva o assegna il proprio training, superadmin compreso**; il trainer riceve il grant (in `/api/me`, con lo scope)
e conduce quel training e non un altro; riassegnare lo toglie al primo; il job lo toglie a training chiuso. Smoke: la lista con i
filtri, la pagina con il promemoria.
**Fatta quando**: sul banco una richiesta si accetta e si assegna, e il trainer, rientrato, ha `Training.Conduct` su quel training
soltanto.

**Com'è andata (A7)** (26 settembre 2026, branch `m3/a7-approve-and-assign`, PR #146, in coda dopo #144):

- **Classificata prima del codice** (`CLAUDE.md` §5): codice del modulo (caso a) dentro meccanismi che ci sono, usati così come sono
  (caso b) — `[AlsoWrittenWith]` ripetuto di A3; l'unico handler, chiesto sulla riga con lo scope e con il «no» all'interessato
  (`DeniedToStakeholder`, anche al superadmin), come la validazione dei tour; `ModuleGrants` del nucleo per il grant con scope del
  trainer, come «aggiungi validatore»; `MapCrud` in sola lettura con `CustomFilters`, `Filterable`, `SearchFields` e `ToListPage`;
  `DataList`, `ListFilter`, `PageShell`, `ConfirmDialog` con un campo generato nei `children` (come la riapertura dei tour),
  `SchemaForm`, `Notice`, `useNotice`, `RatingBadge`; il servizio notifiche con i tipi del modulo; un job come quelli dei tour, nel
  fuso della divisione come i job notturni del nucleo; il roster del nucleo (`hub_user_staff_positions`) —. **Nessun file del
  nucleo**, nessuna nota nuova, nessuna domanda a Carmine, **nessuna migrazione**: la tabella è intera da A6a.
- ⚠️ **Lo scostamento dalla risposta 2 su #135**, trovato da Carmine alla revisione ([il suo commento su #146][d146], 27 settembre
  2026). A7 scrive un grant con scope a ogni assegnazione e aggiunge il job che lo toglie (i punti 5 e 6 qui sotto): è il contrario della
  [risposta 2 di Carmine su #135][a2-135] (26 settembre 2026). Quella risposta portava il trainer sulla regola di A3b — `IHasAssignee` sul
  training, `Training.Conduct` segnato `OnlyForAssignee` e tenuto per posizione — e toglieva il grant per assegnazione e il suo job.
  A3b non era unita, quindi A7 non poteva usare la regola senza aspettarla; ma il design sul branch diceva ancora n.1, e gli scostamenti
  qui sotto non nominavano la risposta. **La decisione: A7 resta com'è, e dopo l'unione di #135 una fase A7b porta il trainer sulla
  regola** (sotto, dopo A7; nota `2026-09-27-il-trainer-sulla-regola-delle-righe-affidate`). Il punto d'arrivo è la risposta 2: cambia
  solo il momento.
- **Fatto**, come il perimetro qui sopra:
  1. **`[AlsoWrittenWith]` sul training** per `Training.Approve`, `Training.Assign` e `Training.Conduct`, senza `AlsoOnCreation`: il
     guardiano lascia scrivere il training a chi approva (i TA), a chi assegna e al trainer sul suo, ognuno con lo scope della riga, mai
     al trainee. `Training.IdOf` legge lo scope all'indietro.
  2. **La lista dello staff**, `/api/training/queue` (`Staff/StaffEndpoints.cs`): una risorsa di `MapCrud` in sola lettura, letta con
     `Training.View` — TC, TAC, TA e trainer: chi fa training vede tutti i training (R.1) —, con le viste in `filter[queue]`
     (`toApprove`, `toAssign`, `inProgress`, `toClose`, `history`: `Staff/StaffQueue.cs`), `filter[kind]`, `filter[traineeVid]`,
     `filter[trainerVid]`, `?q=` sulla postazione e sul VID del trainee; le righe con i nomi del trainee e del trainer (`ToListPage`,
     una query per pagina).
  3. **La pagina di un training**, `GET /api/training/trainings/{id}` (`Staff/StaffTrainings.cs`, `StaffTrainingDto`): la richiesta
     con il rating e le ore alla richiesta, quando il trainee ha dichiarato il teorico, `theoryExamUrl` per il promemoria, la
     decisione, il trainer, e **che cosa può fare chi legge** (`actions.canDecide`, `actions.canAssign`), cioè la risposta dell'unico
     handler sulla riga, come la validazione dei tour. Letta con `Training.View`, che il nucleo non nega mai: il DTO dello staff **non
     ha campi riservati** al trainee, che arrivano con A9 e la sua funzione unica.
  4. **Accetta e rifiuta** (`/accept`, `/reject`), con `Training.Approve` sulla riga, solo da `Requested`, con la versione vista (409
     se vecchia). Il rifiuto vuole un motivo (`errors.required`, fino a 2000 caratteri), che il trainee legge nella mail e in
     `/training/mine`. Mail `requestAccepted` e `requestRejected`.
  5. **Assegna** (`/assign`, e i candidati in `/trainers`), con `Training.Assign` sulla riga, da `Accepted`, `Assigned` e `Scheduled`:
     i candidati sono lo staff del training che l'hub conosce (scostamento 1) con il rating del percorso almeno quello allenato, mai il
     trainee (`Staff/TrainerChoice.cs`), e il server rifà la domanda su quello mandato (`trainerIsTrainee`, `trainerNotStaff`,
     `trainerRatingTooLow`, `trainerAlready`). **Scrive il grant** `Training.Conduct` con lo scope del training
     (`ModuleGrants.GiveAsync`, sul dipartimento del training, con il motivo `training: trainer`) **prima** della riga, poi il training
     (`Accepted` → `Assigned`), poi toglie il grant del trainer di prima; se la riga è cambiata nel frattempo (409) toglie subito il
     grant appena scritto. Mail `trainerAssigned` al trainee (verso `/training/mine`) e al trainer (verso la pagina del training), ognuno
     nella sua lingua e con la sua frase.
  6. **Il job `training-expiry`** (`Staff/TrainingExpiryJob.cs`), la prima metà: ogni notte alle 04:15 nel fuso della divisione,
     `[DisallowConcurrentExecution]`, una riga in `hub_jobs_log`, mai un'eccezione, `RunAsync` per i test. Toglie i grant
     `Training.Conduct` con lo scope di un training che non è più aperto (`Completed`, `NoShow`, `Closed`; per sicurezza anche
     `Rejected`, `Cancelled` e uno che non esiste), su ogni dipartimento.
  7. **Le mail del modulo passano da `TrainingMail`**: la descrizione del training (percorso · rating · postazione), l'indirizzo, la
     lingua del destinatario. Anche `requestReceived` di A6a la usa ora (un cambio di `Requests/TrainingRequests.cs`, senza toccare i
     suoi test): una descrizione sola in tutte le mail.
  8. **Le pagine** (`web/src/modules/training/screens/staff.tsx`): `/staff/training`, la lista generata con i filtri **Mostra** (Da
     approvare, Da assegnare, In corso, Da chiudere, Storico) e **Percorso**, e la voce «Richieste e training» nella barra dello staff
     (`Training.View`); `/staff/training/$id`, la schermata dedicata — lo stato con percorso, rating e postazione; **il promemoria del
     teorico** finché la richiesta aspetta («Prima di accettare, controlla che *trainee* abbia superato l'esame teorico *rating*», il
     giorno in cui il trainee l'ha dichiarato, il link al sito dell'esame); la richiesta; la decisione; il trainer con la sua scelta
     (un form generato fra i candidati del server) —, e **Accetta** e **Rifiuta** (il motivo in un campo generato dentro
     `ConfirmDialog`) solo a chi il server dice. Le funzioni pure in `screens/trainings.ts`, le parole in `training.json`, in italiano
     e in inglese.
- **Scostamenti e precisazioni, piccoli**:
  1. **«Lo staff del training»** (§2.4: HQ, TC, TAC, TA e i trainer) è chi ha, nel roster del nucleo (`hub_user_staff_positions`: chi
     è entrato almeno una volta), una posizione **del dipartimento base del modulo** — ogni livello: TC, TAC, TA, trainer — **o della
     direzione** (`Department.HQ`: DIR e ADIR). Non il web (WM, AWM), che per il nucleo raggiunge ogni dipartimento ma non è staff del
     training; non l'HQ della rete (le posizioni `HQ-…`, senza dipartimento); non chi ha soltanto un grant. Sul banco il web master non
     è proposto (e non ha rating).
  2. **Si riassegna anche un training `Scheduled`**, e la data resta: il nuovo trainer la tiene o la cambia con l'override (A8). Si
     assegna da `Accepted` (che diventa `Assigned`), da `Assigned` e da `Scheduled`; da `Requested` no, prima si accetta. Lo stesso
     trainer di nuovo è `trainerAlready`.
  3. **Il job toglie anche il grant di chi non è più il trainer** del suo training: lo lascia una scrittura fermata a metà, perché
     grant e riga sono due salvataggi di due contesti. Solo dopo un'ora (un'assegnazione può essere ancora in corso), e guardando i
     grant su ogni dipartimento. Il design dice «i grant dei training chiusi»: è lo stesso patto, reso robusto.
  4. **Anche un TC o un TAC che assegna se stesso riceve il grant, e rientra**: il costo del grant (§12 n.1) vale per chiunque sia il
     trainer, e il job lo toglie come agli altri.
  5. **«Da chiudere»** (eseguiti senza report) è `Scheduled` con la sessione in un giorno già finito nel fuso della divisione:
     `StaffQueue.HeldBefore` (l'inizio di oggi là, in UTC) nasce qui per la vista, con i suoi test unitari, e **A8 lo riusa** per
     mostrare «Eseguito» (§1.2). Prima di A8 nessun training è `Scheduled`, e la vista è vuota.
  6. **L'ordine della lista**: senza un ordine scelto, le code di lavoro (Da approvare, Da assegnare, Da chiudere) dalla richiesta più
     vecchia, il resto dalla più nuova.
  7. **Le parole degli stati nella lista** sono in `staff.options.state`, una copia di `states`: la lista generata legge
     `<labels>.options.<campo>.<valore>`, e i tour ripetono le loro allo stesso modo.
  8. **Una persona** si scrive «Nome (VID)» con `memberLabel` del modulo (`api.ts`): l'helper del nucleo per la «persona cancellata»
     è di A12a, e prima di A12b nessun dato di un trainee si cancella.
  9. **`trainerAssigned` è un tipo solo** (§5.2) per due destinatari: la frase che cambia (`assignedTrainee`, `assignedTrainer`) e la
     pagina sono nei dati, nella lingua di ognuno. Al trainer la mail dice anche che l'hub gli chiederà di entrare di nuovo.
  10. **`/training/mine` non cambia**, come aveva scritto A6b: il trainee legge il nome del trainer nella mail, e la pagina di un suo
      training è di A8.
  11. **Con un solo trainer adatto** — il banco —, dopo l'assegnazione la pagina non offre «Cambia il trainer» e dice che nessun altro
      può allenarlo (`noOtherCandidates`): la prima corsa sul banco diceva «nessuno può allenarlo», ed era sbagliato.
  12. **I momenti della pagina sono giorni** (la richiesta, la dichiarazione del teorico, la decisione, l'assegnazione), come nelle
      pagine del trainee (A6b): un'ora avrebbe dovuto dire che è UTC. Trovato guardando a mano; una riga dello smoke lo legge.
- **I due giri sul banco** (il ⚠️ di A6b): Playwright fa girare i file in ordine di nome con un worker solo, e `training-staff.spec.ts`
  viene **dopo** `training-request.spec.ts`. Il giro di A6b trova i due percorsi del trainee liberi e annulla le sue richieste; quello
  di A7 chiede la sua (ATC, attraverso l'API: le pagine della richiesta sono di A6b), la fa accettare e assegnare dalle pagine dello
  staff, e la **lascia `Assigned`**: il trainee annulla solo una richiesta che nessuno ha accettato, e la chiusura dello staff è di A8.
  Se si ferma prima dell'accettazione, annulla la sua richiesta nel `finally`. **Nessun test di A6b è cambiato.** Il banco va ricreato
  prima di ogni corsa, come già scritto: su un banco non ricreato il giro di A6b cadrebbe sul training lasciato da A7, e quello di A7
  lo dice con il suo messaggio. **Per A8**: il training assegnato al trainer del banco resta lì per chi viene dopo in ordine di nome;
  A8 sceglie se riprenderlo (una spec con un nome che viene dopo `training-staff`) o chiederne uno suo e chiuderlo con la chiusura
  dello staff.
- **Trovato, e scritto per chi viene dopo** (anche in `HANDOFF-M3.md`):
  1. ⚠️ **PowerShell 5.1 rovina i caratteri non ASCII di un sorgente** riscritto con `Get-Content` e `Set-Content` (legge un file senza
     BOM come Windows-1252 e lo riscrive in UTF-8 con il BOM): `staff.tsx` ne è uscito con «Ã‚Â·» al posto di «·», e lo smoke l'ha
     trovato. Ripristinato; un sorgente si tocca solo con l'editor.
  2. **Un cookie vecchio si prova su un endpoint che chiede un permesso**: `/api/me` risponde anche a chi non è entrato, quindi con il
     cookie respinto dà 200 e nessun utente, non 401.
  3. `SearchFields` con `TraineeVid.ToString()` si traduce su MariaDB (`CAST … AS char`): la lista cerca per VID.
  4. **Per A8**: il trainer conduce con il grant sullo scope `training:training:{id}`, e l'override di TC e TAC passa con il loro
     `Conduct` per posizione; `StaffQueue.HeldBefore` c'è; `StaffTrainings.IsAssignable` comprende `Scheduled`; il job
     `training-expiry` ha la sua prima metà, e la chiusura per tempo va nello stesso `RunAsync`, prima di togliere i grant (così una
     chiusura della notte toglie il grant nella stessa corsa). La pagina dello staff ha le sezioni della richiesta, della decisione e
     del trainer: le disponibilità e la sessione vanno sotto.
  5. **Per A9**: il DTO dello staff non ha `StaffComment` né le note della scheda; la funzione unica che li toglie al trainee della
     riga li aggiunge.
  6. **Per A10**: le viste della lista sono le code di `approvalQueue`, e `filter[trainerVid]` quella di `trainerQueue`.
  7. I VID **790022–790031** sono di A7; A3b usa 790040–790044 e 790050–790051: il prossimo libero è **790032**.
- **Trovato guardando a mano, non toccato (nucleo)**, detto al revisore:
  1. **Il back office non si usa largo 375 px**: la barra laterale dello staff resta aperta (522 px) e il contenuto resta largo 87 px,
     in ogni pagina dello staff — anche la lista della scheda di A5 —; con la barra chiusa la pagina è larga quanto l'intestazione
     (il 1044 px già trovato da A6b).
  2. **Un select vuoto di `SchemaForm` dice «Select an option» in ogni lingua**: è il segnaposto di Atmosphere, perché `SchemaForm`
     non ne passa uno; lo fa anche il form di una voce nuova della scheda (A5).
  3. Scelto un trainer dall'elenco, il primo clic su «Assegna il trainer» a volte chiude soltanto l'elenco (il select di Radix che si
     chiude), e il secondo assegna; Playwright aspetta che il pulsante riceva il clic e non lo vede.
- **La coda**: A7 è nata in coda dopo #144 (A6b, in bozza in coda dopo #143): la PR è in bozza con `(after #144)` e `Queued after #144.`.
  Quando #144 sarà unita, il passo della coda (`CONTRIBUTING.md`, «Phases in a queue»): `main` nel branch con un merge, build e tutti
  i test di nuovo, via la coda, e la PR pronta con la CI verde.
- **Verificato, in locale** (26 settembre 2026, sul branch da `m3/a6b-request-pages`, e178b1b): `dotnet build` senza avvisi; unità
  **773/773** (le 767 di A6b e le 6 nuove; `TrainingArchitectureTests` legge anche il C# e il TypeScript nuovi del modulo, ed è verde);
  **integrazione intera senza filtro** **329/329** (le 322 e le 7 nuove; la classe nuova da sola 7/7 al primo giro, e con le altre
  classi del training e `AlternativeWritePermissionTests` 20/20); `pnpm lint`, `typecheck`, `format:check`, `i18n:check` verdi; `pnpm
  test` **519** in **65** file (le 509 in 64 di A6b e 10 nuovi); `pnpm e2e` **101** (le 96 e le 5 nuove); **`pnpm e2e:full` 43** su un
  **banco nuovo** di questo worktree (127.0.0.1:5084, `ivaohub_e2e_a7`): le 42 e la spec nuova — rifatto, sempre su un banco nuovo,
  dopo la correzione dello scostamento 12. La prima corsa delle sole spec del training si era fermata sul difetto dello scostamento 11.
  `pnpm gen:api` e `pnpm i18n:sync` senza differenze dopo il commit che li porta; `dotnet format --verify-no-changes` sui file C#
  toccati, test compresi; le regole di `core-guard` rifatte in PowerShell sull'intervallo della fase e sul diff verso `main`: nessun
  file del maintainer, nessuno del nucleo. **A mano**, sul banco di anteprima (127.0.0.1:5090, `ivaohub_preview`; la sessione di A6b
  ha spento il suo su richiesta): il trainee del banco chiede un training ATC e uno pilota; lo staff apre la lista e la vista «Da
  approvare», la pagina della richiesta ATC con il promemoria e il sito dell'esame (impostato per la prova), la accetta e la assegna
  al trainer del banco — che prima di entrare una volta non era nel roster, e la pagina diceva giusto che nessuno poteva allenarlo —;
  rifiuta la richiesta pilota con un motivo; il trainer, rientrato, ha `Training.Conduct` sul solo training assegnato, e la pagina
  senza pulsanti; il trainee legge il rifiuto con il motivo in `/training/mine`, e il training ATC «Trainer assegnato» senza
  «Annulla»; in Mailpit le mail di richiesta accettata, di richiesta rifiutata (con il motivo) e di trainer assegnato, al trainee e al
  trainer, con le loro frasi e le loro pagine. In italiano e in inglese, tema scuro e chiaro, e larga 375 px (sopra, «Trovato guardando
  a mano»).
- **Non verificato**: la CI (la dirà la PR). **Che i test nuovi cadano su una copia indebolita del codice** — `[AlsoWrittenWith]`
  tolto, il «no» all'interessato —: non tentato, perché la modalità di permessi l'ha rifiutato in A5; i test sono stati letti contro il
  codice. **Il job alle 04:15 dal suo trigger**: il test lo fa partire con `RunAsync`, come i job dei tour. **Un training `Scheduled`
  attraverso le pagine**: prima di A8 niente ne fa uno, quindi «Da chiudere» e la riassegnazione di un training datato (che tiene stato
  e data) sono provati con righe scritte dal test d'integrazione, non dalle pagine.
- **Le correzioni della revisione** (28 settembre 2026: [la revisione][r146], [la decisione di Carmine][d146], [i tre punti della regola
  di A3b][m146]). Sul branch temporaneo `fix/a7-review`, da `69781af`, spinto su `m3/a7-approve-and-assign`.
  1. **Un 409 poteva togliere il grant al trainer a cui il training è assegnato** (d23a812).
     - Il difetto: `ModuleGrants.GiveAsync` non fa niente se il grant c'è già, ma il `catch` del 409 lo toglieva lo stesso. Con due
       assegnazioni dello stesso trainer dalla stessa versione (un doppio invio, o due coordinatori insieme) la seconda trovava il grant
       della prima, prendeva il 409 e lo toglieva: il training restava `Assigned` a un trainer senza `Training.Conduct`.
     - La correzione: il `catch` rilegge il training e toglie il grant **solo se la riga non nomina quel trainer**. L'altra strada
       proposta dal revisore, togliere solo il grant che questa richiesta ha scritto, da sola non bastava: la richiesta che scrive il
       grant può essere proprio quella che perde il salvataggio (la prima lo scrive, la seconda lo trova e salva la riga per prima, la
       prima prende il 409).
     - Il test nuovo di `TrainingStaffTests` fa due assegnazioni dello stesso trainer dalla stessa versione: la seconda legge il
       training prima che la prima salvi, attraverso `StaffTrainings` con l'identità del coordinatore, come gli altri test del
       guardiano della classe. Poi assegna un altro trainer dalla versione vecchia: 409, e il suo grant se ne va. **Sul codice di prima
       cade** dove cerca il grant (atteso `[790025]`, trovato `[]`).
     - Il revisore avrebbe accettato anche un buco noto fino ad A7b, «se A7b arriva prima di ogni installazione». Non vale più: il
       maintainer ha fatto la prima installazione di prova (#160, #167, #169).
     - ⚠️ Resta una finestra di tre richieste insieme: un'assegnazione di X trova il grant proprio mentre il `catch` di un'altra, che
       ha letto sulla riga un trainer diverso, lo toglie. Grant e riga sono due salvataggi di due contesti; A7b toglie il grant, e con
       lui la finestra. La notte toglie i grant ma non li ridà: per questo **A7b arriva prima di ogni installazione con trainer
       veri** ([il revisore, dopo le correzioni][r146b]).
  2. **Il giro completo non passava due volte sullo stesso banco** (2327c05).
     - L'asserzione dello scheletro (`full/training-skeleton.spec.ts`, di A4, una sessione di `dalberone`) lascia fuori i grant con
       scope: il trainer tiene `Training.View` sul dipartimento, e un grant su un training solo non è un potere sul dipartimento.
     - La spec dello staff annulla all'inizio una richiesta rimasta in attesa da un giro fermato prima di accettarla. Il suo «quel
       training soltanto» rifiuta ancora un grant su tutto il dipartimento o su un training ancora in corso, ma lascia stare quello su un
       training finito, che aspetta la notte (`training-expiry`).
     - **Quello che resta fuori, e perché.** Sul codice di A7 **nessuna API chiude un training accettato**: il trainee annulla solo
       `Requested`, lo staff rifiuta solo `Requested`, e assegnare lo lascia aperto. La chiusura dello staff è di A8a (`/close`), e
       nemmeno lei toglie il grant: lo toglie la notte. Quindi **un secondo giro di A7 da solo**, su un banco sopravvissuto al primo,
       trova ancora il percorso ATC occupato, in `full/training-request.spec.ts` (A6b) e in `full/training-staff.spec.ts`, e lo dice
       con il suo messaggio.
     - **La chiusura va sul branch di A8a**, che ha `/close`: nel `finally` della spec dello staff, e all'inizio per un training ATC
       lasciato aperto da un giro prima. La aggiunge la correzione di A8a, che prova lì due giri di fila (deciso con la sessione
       «Verifica risposte e correzioni», 28 settembre).
     - `web/e2e/full/README.md` non si tocca: per `core-guard` è un file del nucleo, condiviso, già esistente sotto `web/e2e/` e senza
       «training» nel nome. La riga del revisore «fino ad allora il giro vuole un banco nuovo» sta quindi nell'intestazione della spec,
       qui e in `HANDOFF-M3.md`.
  3. **La pagina dopo un 409** (caf6d4e, il primo nit).
     - `useStaffStep` rilegge la pagina e i trainer su un errore. Dopo un conflitto il form, che ha la chiave sulla versione, si
       ridisegna con quella nuova, e il passo dopo parte da lì.
     - Il form ridisegnato non tiene la frase di quello di prima: il conflitto dell'assegnazione lo dice un avviso, come già accetta e
       rifiuta (`useRefused`, uno per i tre; `isConflict` in `screens/trainings.ts`, con il suo test). Un rifiuto di un campo resta
       sotto il campo: la versione riletta è la stessa, e il form non si ridisegna.
     - Lo smoke nuovo risponde 409 all'assegnazione e dà la pagina assegnata da qualcun altro. **Senza `onError` cade** dove la pagina
       deve nominare l'altro trainer; **sul codice di prima**, già all'avviso.
  4. **`Department.HQ` in `StaffTrainings.cs`** («la direzione», il secondo nit): resta, con il §2.4 che lo regge. Se A7b ricava i
     candidati da chi tiene `Training.Conduct`, possono dirlo le `positionGrants`: è nella lista di A7b.
  5. **Dalla decisione di Carmine** (842745a): lo scostamento qui sopra, la fase A7b qui sotto e la nota
     `2026-09-27-il-trainer-sulla-regola-delle-righe-affidate`.
  6. **I tre punti della regola di A3b** ([il commento del revisore][m146], 28 settembre): nella lista di A7b. La parte di A10c
     (`Training.ManageExams`) la scrive la sessione di A10b sul suo branch.
  7. **`main` nel branch, con A6b** (a00fcd5): `b4bd304`, cioè A6b con `main` a 3c79786 (A3b, #135, e #160–#172) e le correzioni di
     A6b, come il master ha chiesto su #144 per tutta la coda. Nessun conflitto. `HANDOFF-M3.md` tiene l'intestazione di A7, e i blocchi
     nuovi (A3b) stanno sotto quello di A7: i branch sopra riscrivono l'intestazione e mettono il loro blocco sopra, quindi il merge verso
     l'alto resta pulito.
  8. **Il punto 3 del master su #144** (se il catalogo di A3b cambia qualcosa su cui A7 conta): **niente**.
     - Nessun permesso del training è segnato `OnlyForAssignee`: il ramo nuovo dell'handler e i casi nuovi del guardiano non valgono per
       il training.
     - Le tre alternative del training (`Approve`, `Assign`, `Conduct`) non sono segnate e non hanno `AlsoOnDeletion`:
       `PermissionCatalog.VerifyAlternatives` le lascia passare all'avvio.
     - Togliere un training resta di `Edit`, come prima. A7b sarà il primo permesso del training segnato.
  9. **La seconda revisione** ([il commento][r146b], 28 settembre: approvabile nella forma decisa da Carmine):
     - **Il nit della rilettura nel `catch`** (1feb55a). Se la rilettura del training, o la rimozione del grant, falliva nel `catch`,
       la sua eccezione prendeva il posto di quella del conflitto, e chi legge avrebbe avuto un 500 invece del 409. Ora rilettura e
       rimozione stanno in `TakeBackAfterConflictAsync`, che non lancia mai: un errore è un avviso nel log. Il grant che non ha
       potuto giudicare resta alla notte, che lo toglie un'ora dopo se il training nomina un altro. Nessun test fa fallire il
       database fra il salvataggio e la rilettura; `TrainingStaffTests` passa ancora, 8/8.
     - **Il punto 1**, «A7b prima di ogni installazione con trainer veri»: scritto al punto 1 qui sopra, in A7b e in `HANDOFF-M3.md`.
     - **Il nit di `Department.HQ`**: è già nella lista di A7b (punto 8).
  - **Verificato, in locale** (28 settembre 2026, dopo il merge, su a00fcd5 e i documenti):
    - `dotnet build` senza avvisi; unità **823/823**; **integrazione intera senza filtro** **353/353** (`TrainingStaffTests` da sola 8/8,
      anche prima del merge);
    - `pnpm lint`, `typecheck`, `format:check`, `i18n:check` verdi; `pnpm test` **521** in **65** file; `pnpm gen:api` e `pnpm i18n:sync`
      senza differenze; `pnpm e2e` **104**, con il lucchetto del 4173; `dotnet format --verify-no-changes` sui file C# toccati.
    - **Le prove sul codice di prima**: il test nuovo del 409 cade sul `StaffTrainings.cs` di `69781af` (atteso `[790025]`, trovato
      `[]`) e passa con la correzione; lo smoke nuovo cade senza `onError` (la pagina non nomina l'altro trainer) e sul codice di prima
      (all'avviso); la vecchia asserzione dello scheletro cade sul banco con gli avanzi (riceve anche `Training.Conduct`).
    - **Il giro sul banco 5084** (`ivaohub_e2e_a7`). Sul banco com'era, con gli avanzi della vecchia sessione di A7 (il training 3, ATC
      `Assigned` a 999004, con il suo grant con scope), le spec del training: il banco, la scheda e lo scheletro passano, e cadono solo la
      richiesta di A6b e la spec dello staff, sul percorso ATC. Poi il banco ricreato, e **`pnpm e2e:full` due volte di fila senza
      svuotarlo**: il primo giro **43/43**; il secondo **41/43**. Nel secondo cadono solo `full/training-request.spec.ts` (A6b) e
      `full/training-staff.spec.ts`, sul percorso ATC che il primo giro ha lasciato aperto, ognuna con il suo messaggio; lo scheletro
      passa.
  - **Non verificato**:
    - la CI (la dirà la PR);
    - due giri di fila che passano entrambi: vogliono la chiusura di A8a, e li prova la correzione di A8a sul 5086;
    - la finestra di tre richieste del punto 1, che non ha un test: servirebbe una terza assegnazione fra la lettura e la rimozione nel
      `catch`.

### A7b — Il trainer sulla regola delle righe affidate

**Aggiunta il 27 settembre 2026** dalla [decisione di Carmine su #146][d146] (nota `2026-09-27-il-trainer-sulla-regola-delle-righe-affidate`):
porta il trainer sulla [risposta 2 di Carmine su #135][a2-135]. Il punto d'arrivo è quello; cambia solo il momento. **Parte solo dopo
l'unione di #135** (A3b, unita il 27 settembre 2026), dalla cima della coda quando comincia, perché cambia i test di A7–A10a; in coda come
le altre. Branch `m3/a7b-trainer-assignee`. Codice del modulo: la regola è del nucleo da A3b, e se le mancasse qualcosa è una fase del
nucleo a sé, con la sua nota.

⚠️ **Arriva prima di ogni installazione con trainer veri** ([il revisore sulla #146][r146b]). Fino ad A7b, con tre assegnazioni insieme
il trainer che la riga nomina può restare senza grant (A7, «Le correzioni della revisione», punto 1), e la notte toglie i grant ma non
li ridà.

1. **Il training dichiara il suo trainer** con `IHasAssignee`: l'assegnatario è `TrainerVid`.
2. **`Training.Conduct` è segnato `OnlyForAssignee`** nel catalogo del modulo, e lo tengono **per posizione** i TA e i trainer
   (`positionGrants` del TD in `config/division.json` e `division.example.json`; oggi solo TC e TAC). Su una riga che non è sua il
   permesso conta come `Training.Edit`: TC e TAC, che hanno `Edit`, restano con l'override di A8; un TA o un trainer no.
3. **Via il grant con scope scritto all'assegnazione**: in `StaffTrainings.AssignAsync`, `GiveAsync` prima della riga, il `catch` del
   409, `TakeAsync` del trainer di prima e `GrantReason`.
4. **Via la metà di `training-expiry` che lo toglie** (dalla [revisione di A8a][r147-a7b]): in `TrainingExpiryJob` vanno via «closings
   first so the grants go tonight» e `TakeBackAsync`, e cambia il ritorno di `RunAsync`.
5. **Via le parole che dicono al trainer che dovrà rientrare**: nella mail `trainerAssigned` (A7, scostamento 9) e nell'avviso
   `staff.assign.assigned`.
6. **Il guardiano** (dalla revisione di A8a): l'`[AlsoWrittenWith(Conduct)]` sul training deve rispettare la regola dell'assegnatario,
   altrimenti `Touch` rifiuta le scritture del trainer.
7. **I tre punti che la regola di A3b chiede** ([il commento del revisore su #146][m146], 28 settembre 2026):
   - **`[PermissionArea("Training")]` su ogni entità la cui alternativa è segnata.** Senza, il guardiano ricade sull'`Edit` dell'area
     presa dal nome del `DbSet`, mentre l'handler ricade su quello del prefisso del permesso. `Training` ce l'ha da A6a; va su ogni altra
     entità che A7b segnasse. **Un test della spina dorsale**: TC e TAC (`Edit`, non assegnatari) cambiano la riga attraverso
     l'endpoint.
   - **Che cosa fa DELETE**: `DeletePolicy = Training.Edit` (oppure `AllowDelete = false`) su ogni `MapCrud` del training con
     `Training.Conduct` come `WritePolicy`, con un test. Oggi la lista dello staff è in sola lettura, con `WritePolicy = Training.Edit`.
   - **`DeniedToStakeholder` insieme a `OnlyForAssignee`**: il training è `IHasStakeholder`, e `Conduct` è `DeniedToStakeholder` da A4.
     Resta, e un test lo prova con un trainee che è anche l'assegnatario, uguale nell'handler e nel guardiano.
8. **I candidati** (il nit del revisore su #146): se A7b li ricava da chi tiene `Training.Conduct` per posizione, «la direzione» che
   `StaffTrainings.StaffAsync` oggi scrive con `Department.HQ` possono dirla le `positionGrants`. ⚠️ Il web (WM, AWM), che il nucleo
   fa arrivare a ogni dipartimento, A7 lo lascia fuori di proposito (scostamento 1): resta fuori.
9. **I test di A7–A10a che seminano il grant** si adattano:
   - i `GiveConductAsync` dei test e l'asserzione `HoldersOfConductAsync` (dalla revisione di A8a);
   - `TheNightTakesBackTheGrantOfATrainingThatIsOver` e il test del 409 delle correzioni di A7;
   - la spec del giro dello staff, che guarda il `Training.Conduct` per posizione in `/api/me` e la risposta dell'handler sulla riga,
     non più uno scope.
10. **I documenti**: la modifica a `07` (§3.3, §5.3 per `training-expiry`, §12 n.1) nella PR di A7b; il commento in cima a
    `TrainingDates.cs` (dalla revisione di A8a); `HANDOFF-M3.md`.

**Test**: integrazione: il trainer assegnato conduce il suo training e non un altro, **senza nessun grant scritto**; riassegnato, non
conduce più quello; TC e TAC lo cambiano senza esserne gli assegnatari; il trainee che fosse anche l'assegnatario no, nell'handler e nel
guardiano; DELETE come deciso; `training-expiry` non tocca più i grant. Smoke e giro sul banco come in A7.
**Fatta quando**: sul banco una richiesta si accetta e si assegna, e il trainer, **senza rientrare** (nessun grant cambia), conduce quel
training e non un altro.

**Com'è andata (A7b)** (28 settembre 2026, branch `m3/a7b-trainer-assignee`, PR #181, in coda dopo #178):

- **Classificata prima del codice** (`CLAUDE.md` §5): codice del modulo (caso a) dentro un meccanismo che c'è, usato così com'è (caso b)
  — la regola delle righe affidate di A3b (`IHasAssignee`, `OnlyForAssignee`, il ripiego su `{Area}.Edit` nell'unico handler e nel
  guardiano, il controllo all'avvio di `VerifyAlternatives`), com'è scritta per il trainer nella [risposta 2 di Carmine sulla
  #135][a2-135] e nella nota `2026-09-27-il-trainer-sulla-regola-delle-righe-affidate` —, più la configurazione (`positionGrants`, caso a).
  **La regola di A3b basta al trainer**: nessun file del nucleo, nessuna nota nuova, nessuna domanda a Carmine, **nessuna migrazione** (lo
  scope del training non era una colonna).
- **Fatto**, come la lista qui sopra:
  1. **Il training dichiara il suo trainer** (`IHasAssignee`, con `TrainerVid`); via `IHasResourceScope`, `ResourceScope`, `ScopeOf` e
     `IdOf` (scostamento 1).
  2. **`Training.Conduct` è `OnlyForAssignee`** nel catalogo del modulo, e resta `DeniedToStakeholder`. In `config/division.json` e in
     `division.example.json` la voce di `Training.Conduct` va ai quattro livelli del TD — TC, TAC, i TA e i trainer — (scostamento 2).
     Chi lo tiene per posizione riceve anche `Training.View`, che aveva già.
  3. **Via il grant dell'assegnazione**: in `StaffTrainings.AssignAsync` via `GiveAsync`, il `catch` del 409 con la sua rilettura,
     `TakeAsync` del trainer di prima, `GrantReason` e la dipendenza da `ModuleGrants`. Assegnare scrive la riga e basta; una versione
     vecchia resta un 409.
  4. **Via la metà di `training-expiry` che toglieva i grant**: `TakeBackAsync`, «closings first so the grants go tonight», `InFlight`, le
     dipendenze da `TrainingDbContext` e `ModuleGrants`. `RunAsync` **restituisce quanti training ha chiuso** (prima: quanti grant aveva
     tolto), e la riga di `hub_jobs_log` dice solo le chiusure. Il nome del job e l'ora restano.
  5. **Le parole** che chiedevano al trainer di rientrare, in italiano e in inglese: l'avviso `staff.assign.assigned` («Da ora il training
     è suo da condurre») e la frase `mail.training.assignedTrainer` della mail `trainerAssigned`; la copia in `locales/` da `pnpm i18n:sync`.
  6. **Il guardiano**: `[AlsoWrittenWith(Conduct)]` resta, e con `IHasAssignee` e il segno rispetta la regola — il trainer scrive il suo
     training, anche attraverso il `Touch` delle proposte (A8a), prima e dopo la scrittura suo, e nessun altro —; niente `AlsoOnCreation`
     né `AlsoOnDeletion`. `VerifyAlternatives` lo lascia passare all'avvio: l'entità segnata è `IHasAssignee`.
  7. **I tre punti della regola di A3b** ([il revisore sulla #146][m146]), ognuno con un test:
     - **l'area**: `Training` ha `[PermissionArea("Training")]` da A6a, e resta. TC con `Conduct` ed `Edit` — senza `Approve` né `Assign`,
       che al guardiano basterebbero comunque — rischedula e ridata **dall'endpoint** un training di un altro trainer
       (`TrainingSessionsTests.TheCoordinatorConductsATrainingAssignedToSomebodyElseThroughTheEndpoint`); il TAC lo tiene uguale per
       posizione;
     - **DELETE**: nessuna `MapCrud` del training ha `Training.Conduct` come `WritePolicy`; l'unica, la lista dello staff, è in sola lettura
       e non mappa `DELETE`, che risponde **404** come ogni indirizzo di `/api` che nessuno serve — al trainer, al coordinatore e al
       superadmin —; a mano il guardiano rifiuta al trainer l'eliminazione, con `Training.Edit` (`TrainingStaffTests.NobodyDeletesATraining`).
       Nessun `DeletePolicy` né `AllowDelete = false` da aggiungere: non c'è un `DELETE` da restringere;
     - **chi ha interesse**: `Conduct` resta `DeniedToStakeholder`. Un trainee che è anche il trainer del proprio training, e il superadmin
       nella stessa posizione, non lo conducono: l'handler dice no, la pagina non offre niente e ogni passo è un 403
       (`TrainingStaffTests.ATraineeWhoIsTheTrainerOfTheirOwnTrainingConductsNothingOfIt`). Nel guardiano: scostamento 4.
  8. **I candidati** (il nit del revisore su #146): **non ricavati** da chi tiene `Training.Conduct` (scostamento 3); `Department.HQ` resta
     in `StaffTrainings.StaffAsync`, con il §2.4 che lo regge.
  9. **I test di A7–A10a che seminavano il grant** (sotto, «I test cambiati di altre fasi»).
  10. **I documenti**: `07` — l'intestazione, §1.1, §2.4, §3.2 (la colonna dei TA), §3.3, §5.3, §10, la riga di A7 in §11, §12 n.1 —; i
      commenti del codice che dicevano il grant: `TrainingDates.cs` (in cima e sulla chiusura, dalla revisione di A8a), `Training.cs`,
      `TrainingPermissions.cs`, `StaffTrainings.cs`, `StaffEndpoints.cs`, `TrainingSessions.cs`, `TrainerQueueProvider.cs`,
      `TrainingModule.cs`; e qui, sotto A12, A12b e i suoi test, che toglievano i grant dei trainer con i training aperti.
- **Scostamenti e precisazioni**:
  1. **Via `IHasResourceScope` dal training.** Lo scope `training:training:{id}` serviva solo al grant del trainer (design §1.1), e nessun
     modulo né schermata ne scrive un altro: il nucleo lascia scrivere un grant con scope solo al modulo che conosce le righe
     (`UserGrant.ResourceScope`). Lasciato, sarebbe stato codice senza uso. Il test di unità che leggeva lo scope all'indietro
     (`TheScopeOfTheTrainersGrantIsReadBackToItsTraining`, A7) se ne va con il codice che provava, e quello di A6a che leggeva lo scope
     (`ATrainingIsScopedToItselfAndAboutItsTrainee`) prova ora l'assegnatario.
  2. **La voce di `positionGrants` cambiata, non aggiunta**: una voce per permesso, come il resto del file e come il test di architettura
     del modulo lo legge. ⚠️ **In un'installazione già avviata** l'impronta della voce cambia (i livelli ne fanno parte): al primo avvio con
     A7b il seme applica la voce nuova, e la vecchia (TC e TAC) resta come riga, doppione innocuo dei due livelli. La schermata dei
     permessi ne mostra due; si può togliere la vecchia. Un'installazione nuova ne ha una.
  3. **I candidati restano quelli di A7** (§2.4: lo staff del dipartimento base e la direzione che l'hub conosce). Ricavarli da chi tiene
     `Training.Conduct` farebbe entrare il web (WM, AWM) e il superadmin, che il nucleo fa arrivare a ogni dipartimento con ogni permesso e
     che A7 lascia fuori di proposito; e «per posizione» un modulo non lo sa distinguere — un grant a una posizione e uno a un VID sono
     tutti e due `grant:` fra i permessi effettivi, e la direzione tiene tutto per ruolo come il web —: dirlo con le `positionGrants`
     vorrebbe il nucleo. Con i `positionGrants` di A7b **ogni candidato tiene `Training.Conduct`** sul dipartimento base — i livelli del TD
     per la voce del seme, la direzione per il nucleo —, quindi chi riceve un training lo conduce; lo provano i test (T91, T92, ADIR). ⚠️
     Una divisione che togliesse `Conduct` a un livello dalla schermata dei permessi vedrebbe ancora quel livello fra i candidati: non
     gestito, perché il design definisce i candidati per posizione.
  4. **«Uguale nell'handler e nel guardiano», sul trainee che fosse l'assegnatario**: l'handler dice no, prima di guardare a chi è
     affidata la riga; il guardiano invece **non si chiede nel test, apposta**. La sua risposta al trainee di un training è l'eccezione
     del membro sulla propria riga (A6: `ISubmittedByMembers` e `IHasStakeholder`), che non chiede permessi, chiunque sia il trainer, e che
     gli endpoint del trainee restringono ad annullare e scegliere la data; le alternative del training, `Conduct` compreso, il guardiano
     non le conta mai per chi la riga riguarda, qualunque cosa dica il catalogo. La premessa del revisore («the guard says no») vale per una
     riga che il membro non ha mandato, come un esame; sul training l'unico a fermare il trainee è l'handler, e lo ferma per primo. Senza
     `DeniedToStakeholder`, il trainee assegnatario condurrebbe il proprio training dall'endpoint: il segno serve ancora di più.
  5. ⚠️ **I grant che A7 scrive in un'installazione che la avesse prima di A7b** restano, inerti: l'handler chiede l'assegnatario, e su un
     training non affidato a chi chiede un grant con scope di `Conduct` vale come `Training.Edit`, che non dà. Il job non li toglie più. Se
     serve, si tolgono dalla schermata dei permessi (motivo `training: trainer`). Sui banchi (`ivaohub_e2e*`, `ivaohub_preview`) ce ne sono
     di A7–A10c.
  6. **Tre giri sul banco di altre fasi non fanno più rientrare il trainer** dopo l'assegnazione (`full/training-the-dates.spec.ts` di A8b,
     `full/training-the-report.spec.ts` di A9b, `full/training-upcoming.spec.ts` di A10b): il commento diceva che un'assegnazione scrive un
     grant. Ora usano la sessione del trainer aperta all'inizio, e sono tre prove in più che il trainer conduce senza rientrare.
  7. **Il giro dello staff fa entrare il trainer all'inizio**, prima dell'assegnazione, e da lì non rientra più: prima dell'assegnazione
     `GET …/conflicts` del training gli risponde 403 e `/api/me` gli dà `Training.Conduct` sul TD senza scope; dopo, con la stessa sessione,
     200, `canConduct` vero, e `/api/me` uguale. Il «non un altro» è lo stesso training prima di essere suo: sul banco c'è un trainer solo.
     Così il giro non ha più bisogno di un file prima di lui perché il trainer sia nel roster.
- **I test cambiati di altre fasi, e perché** (tutti del collaboratore; li cambia la decisione di Carmine, ognuno detto nella PR):
  1. `TrainingArchitectureTests.TheDivisionFilesGiveTheTrainingDepartmentWhatTheDesignSays` (A4, unità): `Conduct` a tutti e quattro i
     livelli, con il §3.2 cambiato. `TrainingSkeletonTests.TheGrantsOfTheTrainingDepartmentArriveOnceAndReachItsPeople` (A4, integrazione):
     il TA tiene anche `Conduct`, il trainer `View` e `Conduct`. `web/e2e/full/training-skeleton.spec.ts` (A4): il trainer del banco tiene
     `Training.Conduct` e `Training.View`. `TrainingSettingsTests.FiveOfTheNinePermissionsAreDeniedToWhoeverATrainingIsAbout` **non cambia**:
     `Conduct` era già fra i cinque.
  2. `TrainingRequestRulesTests.ATrainingIsScopedToItselfAndAboutItsTrainee` (A6a) → `ATrainingIsAboutItsTraineeAndAssignedToItsTrainer`;
     `TrainingStaffRulesTests.TheScopeOfTheTrainersGrantIsReadBackToItsTraining` (A7) via (scostamento 1).
  3. `TrainingStaffTests` (A7): l'assegnazione del coordinatore (il trainer conduce, nessun grant scritto); il trainer che conduce il suo
     (riscritto: il «fatta quando» di A7b); il 409 dell'assegnazione (il training resta del trainer che nomina, nessun grant); il test sulla
     notte (`TheNightTakesBackTheGrantOfATrainingThatIsOver` → `TheNightTakesNoGrantBack`); «nessuno approva il suo» senza la riga sul grant;
     nuovi `NobodyDeletesATraining` e `ATraineeWhoIsTheTrainerOfTheirOwnTrainingConductsNothingOfIt`.
  4. `TrainingDatesTests` (A8a): via i `GiveConductAsync` e `HoldersOfConductAsync`; il trainer `ScopedTrainerVid` si chiama
     `DepartmentTrainerVid` (il nome parlava del grant); il coordinatore tiene anche `Training.Edit`, com'è un TC — conduce il training di un
     altro con `Edit`, non più con `Conduct` sul dipartimento —; la notte dice quanti training ha chiuso (0, poi almeno 1).
  5. `TrainingSessionsTests` (A9a): via i `GiveConductAsync`, `HoldersOfConductAsync` e la riga della notte nel no-show; il report del mock
     exam con la sessione che il trainer aveva già; nuovo il test del punto 1.
  6. Le tre spec del banco dello scostamento 6, e `full/training-staff.spec.ts` (A7).
  7. `TrainingBlocksTests` (A10b) **non cambia**: dà `Conduct` al trainer sul dipartimento, e il blocco elenca solo i training affidati a
     lui.
- **La prova sul codice di prima**: con i test nuovi e il codice e i file della divisione di A10c (cc14598), il progetto d'integrazione
  compila e **12 dei 63 test del training cadono**: cinque in cui il trainer del dipartimento conduce per posizione (tre di
  `TrainingDatesTests` e due di `TrainingSessionsTests`: 403) e uno in cui la pagina non gli offre il report (`TrainingSessionsTests`); la
  notte che dice quanti training ha chiuso (la vecchia restituiva i grant tolti); lo scheletro (al TA manca `Training.Conduct`); e quattro
  di `TrainingStaffTests` — il trainer senza `Conduct` sul TD in `/api/me`, i grant che l'assegnazione scriveva (due), il grant che la
  notte toglieva —. Restano verdi le garanzie che valevano già: DELETE, il trainee assegnatario, TC dall'endpoint, «nessuno tranne chi
  conduce». I test di unità sul codice di prima non compilano (il training non è `IHasAssignee`). **Sul codice indebolito**, ognuno rimesso
  com'era: senza `[PermissionArea("Training")]` cadono 5 test (fra cui quello di TC dall'endpoint, e DELETE, perché il guardiano chiede
  `Trainings.Edit`); senza il segno `OnlyForAssignee` cadono i 4 del «non un altro». Dopo, i file rimessi toccati e ricompilati, **63/63**.
- **Trovato, e scritto per chi viene dopo** (anche in `HANDOFF-M3.md`):
  1. ⚠️ **Il guardiano lascia scrivere il training a chi tiene `Approve`**, cioè i TA, qualunque cosa scrivano: le alternative del
     training si sommano (A7). Un TA che conduce un training non suo lo ferma l'endpoint, non la rete — com'era da A7. Nei test «uguale
     nell'handler e nel guardiano» il trainer tiene solo `View` e `Conduct`.
  2. **`DELETE` su un indirizzo di `/api` servito solo in `GET` risponde 404**, non 405: il fallback della SPA risponde 404 a ogni metodo
     sotto `/api`.
  3. **Per A11b** (i capi FIR): un capo FIR che conduce lo fa solo come trainer assegnato, se è anche staff del training; `Conduct` non va
     ai capi FIR.
  4. **Per A12b** (la cancellazione): `trainer_vid` è ora anche l'assegnatario della riga; l'eraser lo pseudonimizza come ogni colonna di
     persona, e il training di un trainer cancellato non è più di nessuno che esista.
  5. Nessun VID nuovo: A7b riusa le persone dei test che cambia.
- **La coda**: A7b è nata in coda dopo #178 (A10c, in bozza in coda dopo #153, #151, #150, #149, #148, #147, #146 e #144): la PR è in bozza
  con `(after #178)` e `Queued after #178.`. Il branch è quello di A10c a cc14598. **La PR nasce in conflitto con `main` e senza CI**
  (l'handoff, dopo #145 e #176), come tutta la coda sotto: `main` entra in ogni branch al suo passo della coda (la sessione che coordina, 28
  settembre). Quando #178 sarà unita, il passo della coda (`CONTRIBUTING.md`, «Phases in a queue»): `main` nel branch con un merge,
  l'intestazione di A7b in cima all'handoff e i blocchi nuovi di `main` sotto, build e tutti i test di nuovo, via la coda, e la PR pronta
  a CI verde.
- **Verificato, in locale** (28 settembre 2026, sul branch da `m3/a10c-exams`, cc14598), una suite alla volta. La base è quella di A10c,
  sullo stesso commit (unità 851, integrazione 390, `pnpm test` 559, smoke 139, `e2e:full` 48). Sul codice finale:
  - `dotnet build` senza avvisi;
  - unità **850/850** (le 851 di A10c meno il test dello scope, andato con il codice);
  - **integrazione intera senza filtro 393/393** al primo giro (le 390 e le 3 nuove); le classi del training da sole **63/63**, prima e
    dopo la prova sul codice di prima; `TrainingStaffTests` da sola **10/10**;
  - `pnpm lint`, `typecheck`, `format:check`, `i18n:check` (782 chiavi) verdi, e lo script delle chiavi letterali `training:` (374,
    nessuna manca); `pnpm test` **559** in **72** file (nessun test del front end cambia); `pnpm gen:api` senza differenze; `pnpm
    i18n:sync` nel commit che porta le parole;
  - `pnpm e2e` **139/139** al primo giro, con il lucchetto della porta 4173;
  - **`pnpm e2e:full`** su un banco nuovo di questo worktree (127.0.0.1:5100, `ivaohub_e2e_a7b`): al primo giro **47/48**, è caduta
    `contacts.spec.ts` (del nucleo): la pagina `/contact` non si è avviata nel browser — dopo il JS principale nessuna richiesta per quattro
    minuti, e la spec dopo è partita normale —; rifatto su un banco ricreato, **48/48**. Le spec del training verdi tutte e due le volte:
    lo scheletro, lo staff (il «fatta quando»), le date, il report e i prossimi training, senza che il trainer rientri;
  - `dotnet format --verify-no-changes` sui 17 file C# della fase, test compresi; le regole di `core-guard` rifatte in PowerShell:
    sull'intervallo della fase nessun file del maintainer e nessuno del nucleo; verso `main`, i due conteggi di A10b con la sua nota.
  - **La prova sul codice di prima e su quello indebolito**: sopra.
  - **A mano, sul banco di anteprima** (127.0.0.1:5090, `ivaohub_preview`, con la build di questa fase): all'avvio il seme dà al trainer
    del banco `Training.Conduct` sul TD senza scope, accanto ai tre grant con scope che A7 aveva scritto lì (#6, #7, #8: gli avanzi dello
    scostamento 5). Chiuso il #6 come staff, con un motivo, per liberare il percorso ATC; il trainee chiede un training ADC su LIMC_TWR (#9),
    lo staff lo accetta; il trainer entra, e la pagina del #9 gli mostra la richiesta senza niente da fare; lo staff glielo assegna; nella
    stessa sessione del trainer, **senza rientrare**, la pagina ha «Le date» con «Proponi le date» e «Fissa la data a mano». In Mailpit la
    mail al trainer dice «da ora è tuo da condurre». In italiano; la schermata a dalberone.
- **Non verificato**:
  - **la CI**: la PR è in conflitto con `main` sull'handoff (#145 e #176), come tutta la coda sotto, e un conflitto non fa partire
    `build-test`; parte al passo della coda, quando #178 sarà unita;
  - **un TA che conduce**: nessun test lo fa condurre; il permesso e la regola sono quelli del trainer, e che lo tenga per posizione lo
    provano il test dello scheletro e quello di architettura;
  - **i candidati se una divisione toglie `Conduct` a un livello** (scostamento 3) e **i grant di A7 in un'installazione** (scostamento 5):
    non gestiti, detti;
  - **l'avviso nuovo della pagina** dopo un'assegnazione fatta dalla pagina: lo legge lo smoke, non l'ho visto a mano (sul banco di
    anteprima l'assegnazione è passata dall'API);
  - **la pagina in inglese e a tema chiaro, e larga 375 px**: niente di nuovo da vedere (le schermate non cambiano), e il difetto noto del
    back office a 375 px (A7) resta.
- **La revisione di #181** (29 settembre 2026, [i rilievi](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/181#issuecomment-5891400972);
  la sessione di A7b non c'era più, la coda l'ha portata la sessione che la coordina): approvabile.
  1. **Il corpo della PR non era più vero**: diceva la PR in conflitto con `main` e senza `build-test`, e la CI fra le cose non verificate.
     La CI c'è dal 29 settembre, da quando `main` è sceso nella coda; il corpo è aggiornato con i numeri del 29 settembre. Anche il «Non
     verificato» qui sopra, sulla CI, vale solo per il 28.
  2. **Per Carmine, niente da cambiare qui**: A7 (#146) è in `main` e nelle versioni 0.4.0 e 0.4.1, e ogni assegnazione lì scrive un
     `Training.Conduct` con scope. Dopo A7b quei grant non servono più e il job notturno non li toglie (scostamento 5); la vecchia riga di
     TC e TAC su `Conduct` resta accanto alla nuova in un'installazione già avviata (scostamento 2). **La consegna che porta A7b deve dire
     come ripulirli** (i grant con motivo `training: trainer`).
  3. Nit, già detti: nessun test fa condurre un TA il training assegnato a lui; un TA passa il guardiano su qualunque training con
     l'alternativa di `Approve`, e lo fermano gli endpoint (già noto da A7).
- **Dopo le risposte di Carmine su #149, #150 e #178** (29 settembre 2026, 528edc6, [il commento](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/181#issuecomment-5893690389)):
  la testa nuova di A10c unita, con tre conflitti, ognuno risolto tenendo le due parti: l'intestazione di `HANDOFF-M3.md`; il commento di
  `TrainingDates.CloseAsync` (la regola di A9, non su una sessione cominciata, e la frase di A7b, al trainer non resta niente da
  condurre); `TrainingSessionsTests`, che tiene il test di A7b sul coordinatore attraverso l'endpoint e quello nuovo di A9a sulla sessione
  cominciata. `StaffTrainings` si è unito da sé: `canConduct` e `canClose` falsi a sessione cominciata, sopra la regola di A7b. Verificato:
  build senza avvisi, unità 869, integrazione 411, Vitest 578, smoke 152, `e2e:full` 48 (127.0.0.1:5103).
- **Il passo della coda dopo #178** (30 settembre 2026: #178 unita alle 22:10 UTC del 29; l'ha fatto la sessione che coordina la coda):
  `main` nel branch con un merge (bde3f05) — nessun codice nuovo rispetto alla coda, `main` portava i documenti dei passi della coda; un
  conflitto, l'intestazione di `HANDOFF-M3.md`, che tiene quella di A7b riscritta; il merge toglie anche il conflitto che teneva la PR
  senza `build-test` —, via `(after #178)` dal titolo e `Queued after #178.` dal corpo, il corpo aggiornato, la PR pronta a CI verde.
  **La richiesta del revisore** ([#181](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/181#issuecomment-5900069674)): come
  un'installazione che ha già girato A7 pulisce i grant con scope del trainer (motivo `training: trainer`) è scritto in `HANDOFF-M3.md`,
  nel paragrafo di A7b — dalla schermata «Permessi», o con la stessa condizione in SQL quando sono molti. **Verificato di nuovo, in
  locale** (1ab6040): `dotnet build` senza avvisi; unità **869/869**; `pnpm gen:api` e `pnpm i18n:sync` senza differenze; `lint`,
  `typecheck`, `format:check`, `i18n:check` verdi; Vitest **578/578** in 76 file; **`e2e:full` 48/48** su un banco nuovo (127.0.0.1:5103).
  Due giri caduti per la macchina, scritti tutti:
  1. **Integrazione intera 410/411** al primo giro: `PirepTests.ABanStopsTheReportsNotTheValidationAndThePilotsPageShowsIt` (M2) non si è
     collegato a MariaDB («Unable to connect to any of the specified MySQL hosts»); di nuovo intera, **411/411**.
  2. **Smoke 151/152** tre volte di fila, sempre `blocks.spec.ts:150` (del nucleo: l'immagine di un blocco), la prima con
     `net::ERR_NO_BUFFER_SPACE`, le altre due senza che l'immagine comparisse in 30 s; la spec da sola con `--repeat-each 3` **12/12**; lo
     smoke intero con `--workers=2` **152/152**. La macchina aveva circa 1.800 connessioni in TIME_WAIT, a fine giornata; sullo stesso
     codice, alle 17:21 del 29, lo smoke intero era stato 152/152.

[d146]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/146#issuecomment-5855560982
[r146]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/146#issuecomment-5855673527
[m146]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/146#issuecomment-5869116757
[a2-135]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/135#issuecomment-5844250425
[r147-a7b]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/147#issuecomment-5855683074
[r146b]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/146#issuecomment-5877192345

### A8 — Le date

Design §1.3, §2.5, §5.1, §5.3; note `il-training-in-pubblico`, `il-tempo-per-la-data-e-le-voci-della-scheda`. Branch `m3/a8-dates`.

1. **`trn_slots`** (migrazione): le disponibilità del trainer per quel training, con gli avvisi calcolati quando le scrive, in JSON;
   spariscono a sessione confermata o a training chiuso.
2. **Gli avvisi**: altri training con una sessione quel giorno (qualunque trainer); le voci del calendario unico di un tipo in
   `conflictKinds` che toccano quel giorno, lette dal calendario del nucleo in sola lettura. `conflictPolicy`: `Warn` mostra e chiede
   conferma, `Block` rifiuta, `None` non controlla. Mail `datesProposed`.
3. **Il trainee sceglie** fra i riquadri (`/training/mine/{id}`): `Scheduled`, le disponibilità spariscono, mail `dateConfirmed` a
   tutti e due. **L'override** del trainer, o di TC e TAC: una data qualunque, con gli stessi avvisi e la stessa mail.
4. **«Eseguito» non si scrive**: è `Scheduled` con la data passata, dal giorno dopo nel fuso della divisione; nessun job.
5. **Il calendario**: il training proietta la sessione in corso (e dopo A9 le `Held`) come voce `training` pubblica, titolo **senza
   nomi né VID** — rating e postazione —, indirizzo `/training/sessions/{id}` (la pagina arriva in A10).
6. **Il promemoria**: job `training-reminders` ogni 15 minuti, con `reminded_at` perché parta una volta; mail `reminder` a trainee e
   trainer `reminderLeadHours` prima. ⚠️ Le espressioni cron di Quartz girano in ora locale.
7. **La chiusura per tempo** nel job `training-expiry`, solo con `maxResponseDays` impostato (`Closed`, mail `trainingClosed`); **la
   chiusura a mano** dello staff, con un motivo (`Training.Approve`).

**Test**: unit: gli avvisi (altri training, voci del calendario, le tre politiche); «Eseguito» dal fuso della divisione; integrazione:
una disponibilità con avviso confermata e rifiutata con `Block`; la scelta del trainee e l'override; la voce di calendario senza nomi
che segue la data e sparisce a training chiuso senza sessioni tenute; il promemoria una volta; la chiusura per tempo solo con
l'impostazione. Smoke: i riquadri.
**Fatta quando**: il trainer propone due disponibilità (una con l'avviso di un altro training quel giorno), il trainee ne sceglie
una, la voce compare nel calendario pubblico senza nomi, e il promemoria arriva una volta in Mailpit.

**Divisa il 27 settembre 2026 in apertura**, come A6 (sopra): il server da solo — una tabella, gli avvisi con le tre politiche, la
proposta, la scelta, l'override, la voce del calendario, due job, la chiusura a mano, quattro mail e i loro test — è già una PR come
quella di A7, che con le sue pagine ne ha aggiunte 5000 righe; le pagine, con i riquadri, le sezioni dello staff, lo smoke e il giro sul
banco, la raddoppierebbero.

- **A8a — il server** (branch `m3/a8a-dates-server`, preparato come `m3/a8-dates` e rinominato prima del primo push): i punti 1–7 sul
  server, con gli endpoint che scrivono e i DTO che le pagine leggono — per il trainee le date da scegliere, per lo staff le date con i
  loro avvisi, «Eseguito», la sessione, la chiusura e che cosa può fare chi legge —; i test unit e d'integrazione. Il «fatta quando» lo
  prova un test d'integrazione, attraverso l'API, come in A6a; il promemoria in Mailpit è di A8b.
- **A8b — le pagine** (branch `m3/a8b-dates-pages`, da `m3/a8a-dates-server`): i riquadri in `/training/mine/$id` (una rotta
  `member`) e il collegamento da `/training/mine`; nella pagina dello staff le date proposte con i loro avvisi (la conferma con `Warn`,
  il rifiuto con `Block`), il ritiro di una data, l'override, «Eseguito», la chiusura con un motivo; lo smoke dei riquadri; il giro sul
  banco con `pnpm e2e:full` e il «fatta quando» di A8. Se una pagina chiede al server qualcosa che A8a non dà, è un cambio del modulo
  nella PR di A8b, detto nel suo «Com'è andata».

**Com'è andata (A8a)** (27 settembre 2026, branch `m3/a8a-dates-server`, PR #147, in coda dopo #146):

- **Classificata prima del codice** (`CLAUDE.md` §5): codice del modulo (caso a) dentro meccanismi che ci sono, usati così come sono
  (caso b) — `IProjectable` con l'unico interceptor per la voce del calendario, nella stessa transazione del training; le righe figlie
  scritte con il training, come `PirepError` (§1.1), sotto il guardiano con i permessi alternativi di A3; il calendario del nucleo letto
  in sola lettura, come FlightOps legge `hub_users` (design §8 n.3: non serve un'estensione); `[NotAudited]` del nucleo per la colonna di
  servizio del promemoria (come l'ultimo uso di un token, T19a); le impostazioni del modulo; il servizio notifiche con i tipi del modulo;
  due job come quelli dei tour —. **Nessun file del nucleo**, nessuna nota nuova, nessuna domanda a Carmine. Una migrazione,
  **`AddSlots`**, solo additiva: una tabella, il suo indice, la sua chiave verso il training.
- **Fatto**, come il perimetro di A8a qui sopra:
  1. **`trn_slots`** (`Dates/TrainingSlot.cs`): inizio e fine, gli avvisi in JSON (`warnings_json`), chi l'ha proposta e quando (i
     timbri di `IAuditable`: da lì si conta il tempo per scegliere); figlia del training, con la chiave in cascata. Una data proposta
     esiste solo mentre il training aspetta la sua data.
  2. **Gli avvisi** (`Dates/DateConflicts.cs`, `Dates/DivisionDays.cs`): sui giorni che una data tocca nel fuso della divisione, gli
     altri training `Scheduled` con la sessione in quei giorni, di qualunque trainer, e le voci del calendario dei tipi di
     `conflictKinds` che li toccano, **senza le sessioni dei training** (le conta già la prima regola: un training non avvisa due volte).
     `Warn` chiede la conferma (`confirmed`), `Block` rifiuta la data sulla sua riga, `None` non guarda. Un avviso tiene di un altro
     training il percorso, la sigla, la postazione e l'id, di una voce il tipo, il titolo in ogni lingua e l'indirizzo: **nessun nome,
     nessun VID**.
  3. **I verbi dello staff** (`Staff/StaffEndpoints.cs`, `Dates/TrainingDates.cs`), con `Training.Conduct` sulla riga — il trainer con il
     grant sullo scope del training, TC e TAC per posizione —, mai il trainee: `GET …/conflicts` (che cosa incontra una data, prima di
     scriverla), `POST …/slots` (le date proposte insieme, una mail sola), `POST …/slots/{slotId}/withdraw`, `POST …/date` (l'override);
     con `Training.Approve`, `POST …/close` (la chiusura con un motivo). Ognuno risponde con la pagina com'è dopo, i rifiuti campo per
     campo, 409 su una versione vecchia.
  4. **La scelta del trainee**, `POST /api/training/mine/{id}/choose` (`Requests/RequestEndpoints.cs`), scritta per l'eccezione del
     guardiano per la propria riga: una data proposta ancora da venire, mentre il training la aspetta. Il training passa a `Scheduled`
     con `chosen_slot_id`, le date proposte vanno via, e la mail `dateConfirmed` parte per tutti e due.
  5. **«Eseguito» non si scrive**: `held` nei DTO — la lista, la pagina dello staff, quella del trainee — da `StaffQueue.IsHeld`, che
     ora conta i giorni con `DivisionDays` come la vista «Da chiudere».
  6. **Il calendario**: il training è `IProjectable` (`Training.Project`). Finché è `Scheduled` con la data, una voce `training`
     pubblica all'inizio della sessione, con il titolo «sigla · postazione» (di un pilota la sigla sola) e l'indirizzo
     `/training/sessions/{id}`; niente altrimenti. La voce segue la data e sparisce quando il training si chiude.
  7. **Il promemoria** (`Dates/TrainingRemindersJob.cs`): ogni quarto d'ora, ai minuti 5, 20, 35 e 50, le sessioni che iniziano entro
     `reminderLeadHours` e non sono ancora state ricordate; il segno `reminded_at` prima delle mail, così una mail non parte mai due
     volte; mail `reminder` a trainee e trainer. Una data nuova lo azzera.
  8. **La chiusura**: a mano dallo staff, da `Accepted`, `Assigned` e `Scheduled`, con il motivo che il trainee legge; per tempo nel job
     `training-expiry`, **prima** di togliere i grant (così la stessa notte toglie quello del trainer), e solo con `maxResponseDays`. In
     tutte e due la sessione in corso e le date proposte vanno via, e parte la mail `trainingClosed`. *(Dalla revisione la sessione in
     corso resta nel registro: sotto, «Le correzioni della revisione», 3.)*
  9. **I DTO**: il trainee legge le date da scegliere (senza avvisi), chi è il suo trainer, `held` e il motivo di una chiusura; lo staff le
     date con i loro avvisi e chi le ha proposte e quando, `held`, se la data l'ha scelta il trainee, il motivo della chiusura, e
     `actions.canConduct` e `actions.canClose`. Tutto in `web/src/shared/api/schema.d.ts`.
  10. **Le mail** `datesProposed`, `dateConfirmed`, `reminder` e `trainingClosed`: i tipi, le voci del profilo, oggetti e testi in italiano
      e in inglese; i momenti in UTC, come le pagine (A6b).
  11. **I test**: `TrainingDatesRulesTests` (unità, 9), `TrainingDatesTests` (integrazione, 7, VID 790032–790038).
- **Scostamenti e precisazioni, piccoli**:
  1. **Le date proposte spariscono tutte alla scelta**, alla lettera di §1.3 e §2.5; il training tiene l'inizio della sessione e **quale**
     proposta era (`chosen_slot_id`, senza chiave: la riga non c'è più). **La sessione in corso è il suo inizio** («data e ora», R.6-bis):
     la fine di una proposta serve al trainee per scegliere, e la voce del calendario non ha fine, come quelle dei tour. Tenere la proposta
     scelta avrebbe chiesto una chiave dal training alle date — circolare con quella in cascata dalle date al training — o due salvataggi
     per l'override.
  2. **L'override scrive solo l'inizio**, anche nel passato («una data qualunque»: una sessione tenuta prima del previsto), e nessuna
     proposta: `chosen_slot_id` resta vuoto, come A6a aveva scritto. ⚠️ *Superato per il passato* dalla risposta di Carmine su #149:
     nessuno data un training nel passato (A9, «Le risposte di Carmine su #149»).
  3. **Le date si propongono insieme**, così il trainee riceve una mail sola, e con `Warn` la conferma è della proposta intera. Le regole
     di una proposta, sulla sua riga: una data ancora da venire, che finisca dopo l'inizio e duri al massimo 12 ore, non già proposta; al
     massimo 10 date ancora da venire in attesa. **Una data si ritira** finché il trainee non l'ha scelta (senza mail): il design non lo
     nomina, ma una data proposta per errore resterebbe altrimenti da scegliere fino all'override.
  4. **«Quel giorno»** sono i giorni che la data tocca **nel fuso della divisione** — una data a cavallo della mezzanotte ne tocca due, una
     che finisce a mezzanotte uno —, come «Eseguito»: il giorno si conta in un posto solo, `DivisionDays`, che ora anche
     `StaffQueue.HeldBefore` usa. **Gli altri training** sono le sessioni già fissate, non le date proposte. **Le voci del calendario** si
     leggono come le legge chi scrive la data (il filtro del nucleo), e una sessione di training non avvisa mai anche come voce. *(Dalla
     revisione la data tiene solo le voci che ogni lettore del training può leggere: sotto, «Le correzioni della revisione», 1.)*
  5. **Che cosa incontra una data si chiede prima di scriverla** (`GET …/conflicts`): un rifiuto porta solo chiavi (`CrudProblems`,
     scostamento 3 di A6a), e la pagina deve mostrare gli avvisi per chiederne la conferma; il server rifiuta comunque una data con avvisi
     non confermata.
  6. **Il titolo della voce del calendario è «sigla · postazione»**, uguale in ogni lingua, e la sigla la dice il vocabolario del nucleo.
     Una proiezione la calcola la riga stessa, che non chiede servizi: **il contesto del modulo dice a ogni training che traccia la sigla
     del suo rating** (`TrainingDbContext`, all'evento `Tracked` di EF Core, con il vocabolario iniettato; `Training.RatingShortName` non
     è una colonna). Il contesto di `dotnet ef` non ha il vocabolario e non ne ha bisogno. Una colonna con la sigla avrebbe copiato il
     vocabolario nel training e rimigrato la tabella che A6a ha fatto intera.
  7. **`reminded_at` è `[NotAudited]`**: il segno del job è la sua contabilità, quindi nessuna riga d'audit, `updated_at` fermo, nessuna
     proiezione; la versione della riga cambia comunque (la aggiorna il database).
  8. **La chiusura a mano manda `trainingClosed`**, con il motivo: il design la elenca per «nessuna risposta o no-show» (§5.2), e lo
     staff chiude a mano proprio quando il trainee non risponde (R.3); quella dell'hub dice che la data non è stata scelta in tempo. Una
     richiesta ancora in attesa non si chiude, si rifiuta (A7). Il grant del trainer va via la notte stessa (il job di A7).
  9. **Il tempo per scegliere si conta dall'ultima data proposta** (il trainee ha ricevuto la mail allora), e solo per un training con
     date proposte: senza, non è il trainee a non rispondere. Chiude l'hub: `closed_by` e `close_reason` vuoti. La frase della mail non
     dice i giorni: le parole del server non hanno il plurale.
  10. **Riassegnare il trainer toglie le date proposte dal trainer di prima** (`StaffTrainings.AssignAsync`, di A7): erano le sue, e il
      trainee ne sceglierebbe una che il nuovo trainer non può fare. Una data già fissata resta, come in A7. *(Le toglie tutte, anche
      quelle di un TC: sotto, «Le correzioni della revisione», 7.)*
  11. **La chiusura e l'override azzerano `reminded_at`**; la chiusura anche la sessione in corso, così il calendario la lascia andare.
      *(Dalla revisione solo l'override: sotto, «Le correzioni della revisione», 3.)*
  12. **I nomi delle persone** (`TrainingPeople`: i nomi dal nucleo, la persona di una pagina e quella di una mail) escono da
      `StaffTrainings` in un servizio solo, perché ora servono anche al trainee e alle date.
  13. **`TrainingExpiryJob.RunAsync` risponde ancora con i grant tolti** (il test di A7 lo legge); la riga del registro dei job conta
      anche le chiusure.
- **Test di A6b e di A7 toccati, e perché**:
  1. `TrainingStaffTests.CleanAsync` (integrazione, A7) toglie anche le voci del calendario dei suoi training: due suoi test scrivono
     training `Scheduled` con la data, che da A8 si proiettano, e la pulizia che li cancella in blocco, senza l'interceptor, le
     lascerebbe nel database condiviso. Nessuna asserzione è cambiata.
  2. `screens/trainee.test.ts` (A6b) e `screens/trainings.test.ts` (A7), Vitest: i costruttori di un DTO hanno i campi nuovi, con valori
     neutri (nessuna data, `held` falso, le azioni nuove false), perché il tipo generato li chiede. Nessuna asserzione è cambiata.
- **Trovato, e scritto per chi viene dopo** (anche in `HANDOFF-M3.md`):
  1. ⚠️ **Le parole del server sono la copia in `locales/`**: una chiave nuova del modulo arriva al server (e ai test d'integrazione)
     solo dopo `pnpm i18n:sync` e una build, altrimenti la mail dice la chiave stessa. La prima corsa dei test nuovi è caduta lì.
  2. ⚠️ **I dati di una mail sono JSON, che scrive i caratteri non ASCII come escape** (la lineetta fra due ore, U+2013, e il punto in
     mezzo della descrizione del training, U+00B7, diventano sequenze di escape): un test cerca le parti ASCII, i momenti e gli indirizzi.
  3. ⚠️ **Un `Localized` scritto `null` in un JSON del modulo rientra vuoto**, non nullo (il convertitore del nucleo): il JSON degli avvisi
     omette i valori nulli.
  4. ⚠️ **Il banco non ha un modo di far partire un job a comando** (`/e2e/signin` è l'unico endpoint del banco, in `src/IvaoHub.Web`,
     che è nucleo): il promemoria in Mailpit sul banco (il «fatta quando» di A8, di A8b) o aspetta il giro del quarto d'ora, o chiede un
     modo di far partire il job, che è un cambio del nucleo con la sua nota. Il test d'integrazione lo prova attraverso il job.
  5. **Per A8b**: il flusso della pagina dello staff è `GET …/conflicts` (la politica e gli avvisi) e poi `POST …/slots` o `…/date` con
     `confirmed` — con `Warn` la conferma dopo aver mostrato gli avvisi, con `Block` gli avvisi come rifiuto, con `None` niente —;
     `actions.canConduct` accende proposta, ritiro e override (la proposta e il ritiro solo in `Assigned`, l'override anche in
     `Scheduled`), `actions.canClose` la chiusura. Il trainee legge in `GET /api/training/mine/{id}` le date da scegliere (solo quelle da
     venire, solo in `Assigned`) e sceglie con `POST …/choose` alla versione vista; un 409 vuol dire che il trainer ha cambiato le date
     nel frattempo. Le mail al trainee puntano già a `/training/mine/{id}`, che la pagina di A8b fa esistere.
  6. **Per A9**: la rischedulazione riporta il training ad `Assigned`, azzera la sessione in corso (che diventa una riga di
     `trn_sessions`) e le date si propongono di nuovo con `…/slots`; il no-show e il report lo chiudono. `Training.Project` va esteso alle
     sessioni `Held`: la voce della sessione in corso sparisce da sola quando il training non è più `Scheduled`.
  7. **Per A10**: «in attesa di scelta da N giorni» (`responseReminderDays`) si conta come la chiusura per tempo, dall'ultima data
     proposta: `TrainingDates.Unanswered` con `now − responseReminderDays` dà la coda del trainer. La pagina pubblica della sessione è
     l'indirizzo della voce del calendario.
  8. I VID **790032–790038** sono di A8a; A3b usa 790040–790044 e 790050–790051: il prossimo libero è **790039**, poi **790045**.
- **La coda**: A8a è nata in coda dopo #146 (A7, in bozza in coda dopo #144, in coda dopo #143): la PR è in bozza con `(after #146)` e
  `Queued after #146.`. Quando #146 sarà unita, il passo della coda (`CONTRIBUTING.md`, «Phases in a queue»): `main` nel branch con un
  merge, build e tutti i test di nuovo, via la coda, e la PR pronta con la CI verde.
- **Verificato, in locale** (27 settembre 2026, sul branch da `m3/a7-approve-and-assign`, 28caec8): `dotnet build` senza avvisi; unità
  **782/782** (le 773 di A7 e le 9 nuove; `TrainingArchitectureTests` legge anche il C# nuovo del modulo, ed è verde); **integrazione
  intera senza filtro** **336/336** (le 329 e le 7 nuove; la classe nuova da sola 7/7, dopo una prima corsa caduta sulla mail della
  chiusura che diceva la chiave, sopra, «Trovato» 1); `pnpm lint`, `typecheck` (fermato prima dai due costruttori di DTO di Vitest,
  sopra), `format:check`, `i18n:check` verdi; `pnpm test` **519** in **65** file (come A7: la fase non ha codice del front end); `pnpm
  e2e` **101** — la prima corsa 100/101, un `toBeVisible` che non ha trovato l'elemento, e non ho tenuto quale test (lo smoke gira su
  un'API finta e la fase non cambia schermate); le due dopo 101/101 —; **`pnpm e2e:full` 43** su un **banco nuovo** di questo worktree
  (127.0.0.1:5086, `ivaohub_e2e_a8`): i giri di A6b e di A7 leggono i DTO nuovi dal server vero. `pnpm gen:api` e `pnpm i18n:sync` nel
  commit; `dotnet format --verify-no-changes` sui file C# toccati, test compresi; le regole di `core-guard` rifatte in PowerShell
  sull'intervallo della fase e sul diff verso `main`: nessun file del maintainer, nessuno del nucleo. **A mano**: niente da guardare, la
  fase non ha schermate (il banco di anteprima è di A8b).
- **Non verificato**: la CI (la dirà la PR). **Le pagine**: A8a non ne ha; il «fatta quando» con le pagine è il giro di A8b sul banco.
  **Il promemoria in Mailpit**: il banco non fa partire un job a comando («Trovato» 4); il test d'integrazione lo prova attraverso il job,
  leggendo le mail in coda. **I job dai loro trigger** (ogni quarto d'ora; 04:15): li fa partire `RunAsync` nei test, come quelli dei
  tour e di A7. **Che i test nuovi cadano su una copia indebolita del codice**: non tentato, perché la modalità di permessi l'ha rifiutato
  in A5; i test sono stati letti contro il codice. **Due scritture dello stesso training nello stesso momento** (una proposta e una
  scelta, il promemoria e una data nuova): la versione della riga fa della seconda un 409, e i job lasciano al giro dopo un training che
  si è mosso — letto, non eseguito. **Una data a cavallo di un cambio dell'ora**: `DivisionDays` tiene la regola di A7 per un giorno che
  comincia in un buco dell'orologio (il suo test di unità), e nessun test qui ne attraversa uno.
- **Le correzioni della revisione** ([revisione di A8a su #147][r147], 27 settembre 2026, «approvabile dopo due correzioni piccole»),
  fatte il 28 settembre sul branch temporaneo `fix/a8a-review` e spinte su `m3/a8a-dates-server`:
  1. **Gli avvisi salvati si fermano a ciò che ogni lettore del training può leggere** (`6f0c171`, «Da correggere» 1). Il calendario si
     legge con il filtro di chi propone, e la data teneva tutto: una voce che legge un solo dipartimento (`Visibility.Department`),
     mostrata alla direzione o a chi sta in più dipartimenti, arrivava con la pagina dello staff a ogni trainer e advisor del TD. Ora chi
     propone vede ancora tutto ciò che può leggere (`GET …/conflicts`), e la politica lo conta; la data **tiene solo ciò che una pagina
     per lo staff può portare**, il tetto del nucleo (`VisibilityCeiling.For(Staff)`: le voci di tutti, dei membri e dello staff), in
     `DateConflicts.Kept`. È la prima delle due strade del revisore: la forma degli avvisi non cambia, e A8b (#148) la legge così com'è.
     Gli altri training non hanno bisogno del tetto: un loro avviso dice ciò che il calendario pubblico dice della loro sessione. Il test
     d'integrazione nuovo (la direzione propone sopra una voce del solo ED, e un trainer del TD legge la pagina) **cade sul codice di
     prima**: rimesso il `TrainingDates.cs` di prima, il trainer legge «trn-test department». La descrizione di `DateWarning` finisce
     nei tipi generati dell'API: `pnpm gen:api` in `bbb6ffe`.
  2. **I test dei rifiuti** (`c6638b8`, «Da correggere» 2; VID 790072 e 790073):
     - un trainer del TD con `View` per posizione e senza grant su quel training legge la pagina e riceve 403 su `conflicts`, `slots`,
       `withdraw` e `date` (§3.3); il training è di un altro trainer, così il test regge anche il passaggio di A7b alla regola
       dell'assegnatario;
     - uno dello staff che conduce ogni training (`View`, `Conduct` ed `Edit` per grant, come un coordinator) riceve gli stessi 403 sul
       proprio, e conduce quelli degli altri; il ritiro del trainee è un 403; niente viene scritto;
     - `SlotTooLong`; `SlotsTooMany`, undici date insieme e una sopra dieci in attesa; `NotProposable`, proposta e ritiro su un training
       datato; `NotSettable`, senza trainer e chiuso;
     - il 409 su una versione vecchia per la proposta e per la scelta, senza scrivere niente e senza mail; la pagina riletta scrive.

     Coprono un comportamento che c'era: non cadono sul codice di prima, e una copia indebolita non l'ho provata (sotto, «Non verificato»).
  3. **La chiusura tiene la data della sessione** (`9300001`, «Da guardare»): deciso di non azzerarla più. Azzerarla cancellava la data
     di una sessione che forse si è tenuta, e il design §6 tiene stati e date nel registro. Al calendario non serve: `Training.Project`
     proietta solo un training `Scheduled`, e lo stesso vale per il promemoria e per «Eseguito». La chiusura lascia la sessione com'era —
     la data, quale proposta era, se è partito il promemoria — e toglie solo le date proposte; lo scostamento 11 vale ora solo per
     l'override. Il test della chiusura legge la data tenuta, e cade sul codice di prima (la data era vuota). Le pagine di A8b mostrano la
     sessione solo di un training `Scheduled`: mostrare la data di uno chiuso è una loro scelta.
     ⚠️ **Per A9a**, da controllare al merge verso l'alto (il suo branch non l'ho toccato):
     - il commento di `Training.ScheduledStartUtc` su A9a dice «none again once the training closes without it — rescheduled, not
       attended, closed»: «closed» non vale più, e il merge darà un conflitto proprio su quelle righe;
     - lo scostamento 2 di A9a dice «La rischedula e il no-show azzerano la data, come la chiusura di A8a»: la rischedula e il no-show
       possono continuare ad azzerarla, perché la riga di `trn_sessions` tiene la data, ma non più «come la chiusura di A8a»;
     - nient'altro di A9a legge la data senza lo stato: `IsRecordable`, `Project` e le viste guardano prima lo stato.
  4. **La chiusura per tempo non salva un motivo** («Da guardare»): nessun segno nuovo. «`Closed`, con il motivo» della nota
     `il-tempo-per-la-data-e-le-voci-della-scheda` §1 lo coprono la mail, che dice perché (`closedUnanswered`), e i dati: `Closed` con
     `closed_by` e `close_reason` vuoti è la chiusura dell'hub, e l'hub chiude solo per questo; una chiusura dello staff ha sempre chi e
     il motivo, che è obbligatorio. Le pagine di A8b la riconoscono già dal motivo vuoto. Un segno avrebbe chiesto una colonna, cioè una
     migrazione nuova sotto quella di A9a. Scritto anche nel commento di `CloseUnansweredAsync` (`6fee3f1`). ⚠️ **Per A12**: il segno
     sicuro è `closed_by` vuoto; se l'eraser svuota `close_reason` di una chiusura dello staff (è un testo libero sul trainee), una pagina
     che distingue le due chiusure dal motivo la leggerebbe come quella dell'hub.
  5. **La frase «A closure makes you wait for nothing» di `trainingClosed`** («Da guardare»): l'ha già corretta A9a. L'attesa ora è nei
     dati della mail (`after`), e le due chiusure di A8a mandano `closedNoWait` («Com'è andata (A9a)», scostamento 3).
  6. **L'override su una sessione già tenuta** (scostamento 2, «Da guardare»): lo decide A9. A9a non lo rifiuta: in `TrainingDates.cs`
     cambia solo le frasi della mail, e l'esito di una sessione si registra dal suo inizio (`TrainingSessions.IsRecordable`, «Com'è andata
     (A9a)», scostamento 1, che dice anche «prima dell'inizio, una sessione si sposta con la data a mano di A8a»). Il server però non lo
     impone: su un training `Scheduled` con la sessione cominciata l'override riscrive ancora la data, senza una riga di `trn_sessions`.
     Resta la domanda del revisore, per A9.
  7. **Il commento di `AssignAsync`** (`6fee3f1`, «Da guardare»): parlava delle «date proposte dal trainer di prima», ma il codice le toglie
     tutte, anche quelle di un TC. Il comportamento resta, e le parole ora dicono il codice.
  8. **L'hook `Tracked`** (`6fee3f1`, «Da guardare», scostamento 6): un commento dove si imposta. Solo un training tracciato riceve la
     sigla: uno letto con `AsNoTracking` e passato a `ProjectionRefresh`, o uno di un contesto senza il vocabolario, proietta il titolo
     senza rating, e quello di un pilota diventa `#id`.
  9. **`open_kind` e la chiusura per tempo** (`83cd552`; [revisione di A6a su #143][r143], punto 2): controllato che la chiusura della notte
     passi dall'entità — il training si legge tracciato, lo stato cambia sulla riga, e il getter di `OpenKind` scrive la colonna —. Il
     test della notte ora lo dice: finché il training aspetta, la chiave rifiuta un secondo training aperto sullo stesso percorso; chiuso
     dalla notte, un training nuovo entra. Il ⚠️ è in `HANDOFF-M3.md`.
  10. **La lista di A7b** la scrive la correzione di A7 (#146), sotto «A7b» in questo piano, con le voci che la revisione di #147 le
      aggiunge. ⚠️ Una in più, da questa correzione: il coordinator dei test di A8a (`TrainingDatesTests`, `CoordinatorVid`) ha
      `Conduct` per grant e non `Edit`; con `Conduct` segnato `OnlyForAssignee` varrebbe `Edit` sui training che non sono suoi, e i test
      dove sposta la data o riceve `NotSettable` cadrebbero con un 403. Gli serve `Edit`, come TC e TAC l'hanno per posizione.
  11. **Per A12**, dalla revisione: l'eraser cancella i training aperti **passando dal change tracker**, non con `ExecuteDelete`, o la
      loro voce del calendario resta. In `HANDOFF-M3.md`.
  12. **La coda in pari con `main`** ([richiesta del revisore su #144][m144]): `m3/a7-approve-and-assign` a `3073b59`, che porta le
      correzioni di A6b e di A7 e `main` a `4d424f9` (A3b, #135, e #160–#172), è entrato con un merge (`373be7c`). Un conflitto solo, la
      tabella delle fasi qui sopra: la riga di A7b resta, e dopo A8a, A8b e A9. **Il catalogo di A3b non cambia niente di ciò su cui A8a
      conta**: nessun permesso del training è `OnlyForAssignee`, il training non è `IHasAssignee` e non ha `AlsoOnDeletion`, quindi il
      ramo nuovo dell'handler e quello del guardiano non lo toccano, e `VerifyAlternatives` all'avvio lo lascia passare; l'handler con
      lo scope, `DeniedToStakeholder` e `[AlsoWrittenWith]` in modifica sono quelli di prima. Cambierà con A7b.
  13. **Il banco due volte** ([revisione di A7 su #146][r146], punto 2; il piano della sessione che coordina la coda): sul codice di A7
      niente chiude un training accettato, e la chiusura dello staff è di A8a, quindi la fa qui `web/e2e/full/training-staff.spec.ts`
      (`424e3d2`): all'inizio, dopo le richieste in attesa che A7 annulla, lo staff chiude con un motivo il training ATC che un giro
      precedente ha lasciato accettato, assegnato o datato; nel `finally`, quello del giro, una volta accettato. L'intestazione e il
      messaggio del rifiuto non dicono più che un secondo giro vuole un banco nuovo. **Tre giri di fila** sul banco di questo worktree
      (127.0.0.1:5086, `ivaohub_e2e_a8`, mai ricreato):
      - il **primo** sul banco lasciato dal giro del 27 settembre, con il training ATC `Assigned` di allora: **41/43**. È caduta la
        richiesta di A6b (`requestOpen`): gira prima della spec dello staff e ha trovato quel training. La spec dello staff l'ha chiuso
        all'inizio, e nel `finally` ha chiuso il suo. È caduto anche `tours-briefing` (M2): la ricerca anonima fatta nel secondo in cui
        il tour diventa pronto non l'ha trovato, e la riga dell'indice è pubblica; al giro dopo passa;
      - il **secondo** **43/43**;
      - il **terzo** **41/43**: le spec del training tutte verdi. Due spec dei tour (M2), `tours-review` e `tours-round`, si sono viste
        rifiutare un report (`POST …/reports`, 400), dopo che quattro report dello stesso giro erano passati. Al terzo giro della
        giornata il pilota del banco ha diciotto report di oggi. Non l'ho indagato oltre: le spec e il codice sono di M2, e non li tocco.
- **Verificato, in locale, dopo le correzioni e il merge** (28 settembre 2026, `bbb6ffe`): `dotnet build` senza avvisi; unità **833/833**
  (con quelle di `main`); **integrazione intera senza filtro** **364/364**. Prima del merge la classe `TrainingDatesTests` da sola era
  11/11 (le 7 e le 4 nuove), e le sei classi del training insieme 33/33. Le due prove sul codice di prima (correzioni 1 e 3): rimesso
  il file di prima, compilato e cadute; poi rimesso il file nuovo, toccato, ricompilato, e `git diff` uguale a prima della prova.
  `pnpm lint`, `typecheck`, `format:check`, `i18n:check` verdi; `pnpm test` **521** in **65** file; `pnpm e2e`, con il lucchetto:
  **103/104** la prima volta, con l'integrazione che girava in parallelo (la mappa di un tour, `tours-map`, un `toBeVisible` scaduto a
  5 secondi), **104/104** la seconda; `pnpm e2e:full`, i tre giri della correzione 13. `pnpm gen:api` in `bbb6ffe`, `pnpm i18n:sync`
  senza differenze; `dotnet format --verify-no-changes` sui file C# toccati; le regole di `core-guard` rifatte in PowerShell sul diff
  verso `main` e sull'intervallo della fase: nessun file del maintainer, nessuno del nucleo, e l'unica nota nuova è quella di A7.
- **Non verificato (le correzioni)**: la CI, che dirà la PR. **Che i test dei rifiuti cadano su una copia indebolita del codice**: non
  tentato, perché togliere un controllo su chi scrive la modalità di permessi l'ha rifiutato in A5 e in A9a; sono coperture di un
  comportamento che c'era. **Il giro intero verde due volte di fila sullo stesso banco**: le spec del training sì, al secondo e al terzo
  giro; le due dei tour del terzo giro no (sopra, correzione 13).

[r147]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/147#issuecomment-5855683074
[r143]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/143#issuecomment-5855666152
[m144]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/144#issuecomment-5859555627

**Com'è andata (A8b)** (27 settembre 2026, branch `m3/a8b-dates-pages`, PR #148, in coda dopo #147):

- **Classificata prima del codice** (`CLAUDE.md` §5): codice del modulo (caso a) dentro meccanismi che ci sono, usati così come sono
  (caso b) — la rotta `member` del manifest, come `/training/request` e `/training/mine`; il form generato con il campo `datetime` del
  nucleo (il valore ISO in UTC, l'ora della divisione sotto: **nessun tipo di campo nuovo**) e la sua lista ripetibile per le date
  proposte insieme; `ConfirmDialog` (con il campo generato nei `children` per la chiusura, come «Rifiuta» di A7), `Notice`, `useNotice`;
  `useMoment` con il fuso della divisione, `useLocalized` e i tipi del calendario del bootstrap; `DataList` con la colonna dello stato —.
  **Nessun file del nucleo**, nessuna nota nuova, nessuna domanda a Carmine. **Un cambio del server del modulo**, come la frase di A8
  prevede (sotto, scostamento 1).
- **Fatto**, come il perimetro di A8b qui sopra:
  1. **`/training/mine/$id`** (`screens/traineeTraining.tsx`, rotta `member`): lo stato — «Eseguito» dal giorno dopo la sessione —, il
     percorso, il rating e la postazione; quando è stata chiesta e il momento del suo stato; il trainer; **i riquadri delle date da
     scegliere** (le date ancora da venire mentre il training aspetta la data), ognuno con inizio e fine in UTC e sotto nel fuso della
     divisione, e «Scegli questa data», chiesto ancora una volta prima che parta, alla versione vista; **un 409** dice che il trainer ha
     cambiato le date nel frattempo, e la pagina rilegge e mostra quelle di adesso; un rifiuto (`slotUnknown`, `slotPassed`, `state`) si
     legge nell'angolo, e la pagina rilegge. Poi **la sessione** (UTC e fuso della divisione), **che cosa succede dopo** per ogni stato che
     va avanti (in attesa, accettata, senza date, programmata, eseguita), **perché si è chiuso** — dal dipartimento training con il suo
     motivo, o dall'hub perché la data non è stata scelta in tempo —, i due testi della richiesta e «Annulla la richiesta» su `Requested`.
     Un training di un altro membro è «non trovato».
  2. **`/training/mine`**: ogni training porta alla sua pagina («Apri»; «Scegli la data», in blu, con «Il trainer ha proposto N date:
     scegli la tua.» quando ce ne sono); «Eseguito»; la chiusura con il suo motivo accanto al rifiuto, che c'era già.
  3. **La pagina dello staff** (`screens/staff.tsx`), sotto la sezione del trainer, **«Le date»** mentre il training ha il trainer e va
     avanti: **la sessione** (UTC e fuso della divisione; «La data l'ha scelta il trainee fra quelle proposte» o «fissata a mano»;
     «Eseguita»); **le date proposte**, con ciò che l'hub ha trovato quando sono state scritte — un altro training per sigla e postazione,
     con il link alla sua pagina; una voce del calendario per tipo, titolo nella lingua a schermo e indirizzo — e chi le ha proposte e
     quando, «Già passata» per una data che non si sceglie più, e **«Ritira»** (chiesto prima); **«Proponi le date»**, il form generato con
     la lista delle date (inizio e fine); **«Fissa la data a mano»**. In alto **«Chiudi il training»**, con il motivo in un campo generato
     dentro `ConfirmDialog`; una sezione **«La chiusura»** dice chi l'ha chiuso e quando, e perché. `actions.canConduct` accende la
     proposta e il ritiro in `Assigned` e l'override anche in `Scheduled` (`dateSteps`), `actions.canClose` la chiusura. **La lista** dice
     «Eseguito» per una riga `held`.
  4. **Gli avvisi prima di scrivere** (`useDatesWriter`, lo stesso per la proposta e per l'override): per ogni data scritta intera la
     pagina chiede `GET …/conflicts`; con **`Warn`** e qualcosa trovato non scrive, e sotto il form mostra le date e ciò che incontrano
     con «Proponi lo stesso» / «Fissa lo stesso», che manda di nuovo il form (per il suo `id`, come A6b) con `confirmed: true` — solo se le
     date sono ancora quelle chieste, altrimenti chiede di nuovo —; con **`Block`** le date partono non confermate, il server rifiuta
     quelle incontrate sul loro campo (`slots[i].startsAtUtc`) e la pagina mostra accanto ciò che ha trovato; con **`None`** nessun
     avviso. **L'ultima parola resta del server**: un avviso nuovo fra la domanda e la scrittura è un rifiuto `confirmed`, detto sopra il
     form, e la pressione dopo richiede. Una casella vuota non si chiede: il server dice che manca. I rifiuti sui campi del form (le
     caselle di ogni riga, `startsAtUtc`) vanno sul campo, gli altri (`slots`, `confirmed`, `state`) sopra il form (`splitRefusal`, che ora
     accetta anche una regola sui nomi dei campi, per le righe di una lista).
  5. **Le funzioni pure** in `screens/dates.ts` (`choosableSlots`, `spanText`, `isWhole`, `sameDates`, `whatTheyMeet`, `asksConfirmation`,
     `isRefused`, `warningSays`, `isHubAddress`, `dateSteps`, `closingOf`), `shownState` in `api.ts`; `WhenText`, `OutcomeText` e
     `CancelRequest` (spostato da `mine.tsx`) in `parts.tsx`, per le due pagine del trainee e per quella dello staff; le chiamate in
     `api.ts` (`mineOneQuery`, `useChooseDate`, i passi `propose`, `withdraw`, `date`, `close` di `useStaffStep`, `dateConflicts`,
     `useRereadStaffTraining`), gli schemi in `schemas.ts` (`proposalSchema`, `dateSchema`, `closeSchema`); le parole (`states.Held`,
     `time`, `detail`, `mine.*` nuove, `staff.sections`, `staff.session`, `staff.dates`, `staff.close`, `staff.closing`) in italiano e in
     inglese, copiate da `pnpm i18n:sync`.
  6. **I test**: Vitest `screens/dates.test.ts` (8); lo smoke `web/e2e/training-dates.spec.ts` (7, con l'API finta: i riquadri e la
     scelta da `/training/mine`, la scelta superata dal trainer (409), la chiusura letta dal trainee, gli avvisi con `Warn` confermati
     prima di scrivere e una data ritirata, il rifiuto con `Block`, la sessione e l'override, la chiusura con il motivo, «Eseguito» nella
     lista); il giro sul banco `web/e2e/full/training-the-dates.spec.ts`, **il «fatta quando» di A8** (sotto); un fatto nuovo
     d'integrazione in `TrainingDatesTests` (scostamento 1).
- **Scostamenti e precisazioni, piccoli**:
  1. **Un cambio del server del modulo**: A8a leggeva una casella di data lasciata vuota come l'istante zero, quindi un inizio vuoto «è già
     passato» e una fine vuota «viene prima dell'inizio»; l'override rifiutava un inizio vuoto solo come valore predefinito, che il JSON
     non manda (un `null` era un 400 generico, senza campo). I due DTO di scrittura ora hanno le date nullabili (`TrainingSlotWriteDto`,
     `TrainingDateWriteDto`): una casella vuota è `errors.required` sul suo campo, e dell'altra casella della riga si dice ancora quello che
     c'è da dire. Il tipo generato segue (`pnpm gen:api`). Provato da `ADateLeftEmptyIsRequiredOnItsOwnField`, **un fatto nuovo nella
     classe di A8a** (nessun fatto esistente cambiato), che **cade sul codice di A8a** rimesso com'era — il server risponde con l'errore
     generico di lettura del JSON — e passa su quello di A8b.
  2. **La conferma degli avvisi non è una finestra**: `ConfirmDialog` si apre dal suo pulsante, e la domanda arriva dopo le risposte del
     server; quindi è una `Notice` sotto il form, con il pulsante che manda di nuovo il form. Una finestra che si apre da sola sarebbe
     un'estensione di `ConfirmDialog` (nucleo). La scelta della data del trainee, che per lui è per sempre, è invece una `ConfirmDialog`.
  3. **I momenti in UTC, e sotto nel fuso della divisione** (`docs/UI-GUIDELINES.md`, «Times», come `DataList` e il campo `datetime`): le
     date da scegliere e la sessione sono le ore che contano, e il trainee le legge dove vive. I giorni (la richiesta, la decisione, la
     proposta) restano giorni, come in A6b e A7. **Il giorno della settimana nei riquadri non c'è**: `useMoment` non lo dà, e un altro
     formatore sarebbe una seconda copia (nucleo; detto al revisore).
  4. **La versione della riga non è un campo dei form delle date**: la pagina manda quella che ha quando le date partono, così dopo un 409
     la pagina si rilegge e il form tiene ciò che è scritto; un form che è andato ricomincia vuoto (la sua `key` è un contatore). **Un
     passo dello staff superato da qualcun altro (409) rilegge la pagina** (`useRefused`), anche Accetta e Rifiuta di A7, che prima lo
     dicevano soltanto.
  5. **«Eseguito» è uno stato mostrato** (`shownState`, `Held`), mai scritto: nel badge delle pagine e nella colonna della lista, dove la
     riga porta `Held` al posto di `Scheduled` (`staff.options.state.Held`); il colore è il blu di ciò che aspetta, come `Requested`.
  6. **Le regole della proposta dette prima** («al massimo 10 alla volta, di 12 ore al massimo») stanno nella frase sopra il form, oltre
     che nei rifiuti del server: sono le stesse parole dei rifiuti, non regole nel browser.
- **Test di A6b, A7 e A8a toccati, e perché**:
  1. `web/e2e/training-request.spec.ts` (A6b) e `web/e2e/training-staff.spec.ts` (A7), lo smoke: i costruttori dei DTO finti hanno i
     campi nuovi di A8a con valori neutri (`trainer: null`, `slots: []`, `held: false`, `closeReason: null`, `dateChosenByTrainee: false`,
     le azioni nuove false), perché le pagine ora li leggono (una lista di date assente fa cadere `/training/mine`). Nessuna asserzione è
     cambiata.
  2. `TrainingDatesTests` (A8a): il fatto nuovo dello scostamento 1. Nessun fatto esistente è cambiato.
- **I giri sul banco**: `training-the-dates.spec.ts` ha un nome che viene dopo `training-staff.spec.ts` e **riprende il training ATC che
  A7 lascia `Assigned`** al trainer del banco (se gira da sola, ne chiede uno suo attraverso l'API); per «un altro training quel giorno»
  il trainee chiede un training **pilota** (il giro di A6b annulla le sue richieste, quello di A7 lascia libero il percorso pilota), che lo
  staff accetta, assegna al trainer del banco e data a mano alle 10:00 UTC del giorno della prima data, attraverso l'API. Il trainer, che
  l'assegnazione ha fatto uscire, rientra e **propone due date dalla pagina** — la prima quel giorno, avvisata dalla sessione pilota con il
  link alla sua pagina, e confermata —; il trainee **sceglie quella** da `/training/mine`; **un visitatore la trova nel calendario
  pubblico** («sigla · postazione») e la pagina non nomina nessuno (né il trainee né il trainer, né i loro VID); **la mail `dateConfirmed`
  arriva in Mailpit a tutti e due**; poi **lo staff chiude il training dalla pagina** con un motivo, la voce lascia il calendario e il
  trainee legge il motivo. Nel `finally` chiude attraverso l'API ciò che la corsa ha lasciato aperto. **Nessun test di A6b o A7 è
  cambiato** nel giro sul banco. Il banco va ricreato prima di ogni corsa, come già scritto.
- **Il promemoria in Mailpit** (A8a, «Trovato» 4): **deciso di non aspettarlo nella spec** — il job gira ogni quarto d'ora, e aspettarlo
  costerebbe fino a 15 minuti di CI —; il test d'integrazione di A8a lo prova attraverso il job. **Visto a mano sul banco di anteprima**
  (27 settembre, sotto): una sessione entro le 24 ore di `reminderLeadHours`, scelta dal trainee alle 03:19; il giro del job delle 03:20
  ha mandato **un** promemoria al trainee e **uno** al trainer, e il giro delle 03:35 nessun altro.
- **Trovato, e scritto per chi viene dopo** (anche in `HANDOFF-M3.md`):
  1. ⚠️ **La voce del calendario porta a `/training/sessions/{id}`, che ancora non esiste**: un visitatore che clicca una sessione nel
     calendario pubblico trova «non trovato» finché A10 non fa la pagina (design §4.1). Detto al revisore.
  2. **Per A9**: il flusso degli avvisi e i form delle date sono `useDatesWriter`, `ProposeDates` e `SetDate` in `staff.tsx`: la
     rischedulazione, che riporta il training ad `Assigned`, li ritrova così come sono; le azioni del dopo sessione vanno nella sezione
     «Le date», accanto alla sessione `Held`. La pagina del trainee ha già «che cosa succede dopo» per ogni stato: `Completed` e `NoShow`
     sono di A9.
  3. **Per A10**: «in attesa di scelta da N giorni» si conta dalle date proposte (`TrainingDates.Unanswered` di A8a); la pagina pubblica
     della sessione è l'indirizzo della voce del calendario (sopra, 1).
  4. ⚠️ **La trappola di A6b si è ripresentata**: una chiave del modulo che non esiste (`detail.dates.leadNoTrainer`, lasciata da una
     stesura) è arrivata in un commit, e né `pnpm i18n:check` né le suite l'hanno vista (il ramo non si disegna mai: un training
     `Assigned` ha sempre il trainer). Trovata prima della PR con uno script che confronta le chiavi letterali `t('training:…')` del
     modulo con i suoi file di lingua, e tolta. Estendere `i18n:check` alle chiavi con il namespace è un cambio del nucleo (`web/scripts/`):
     detto di nuovo al revisore, e nessun controllo di ripiego scritto nel modulo.
  5. VID: A8b non ne usa di nuovi; il prossimo libero resta **790039**, poi **790045** (A3b usa 790040–790044 e 790050–790051).
- **La coda**: A8b è nata in coda dopo #147 (A8a, in bozza in coda dopo #146, in coda dopo #144, in coda dopo #143): la PR è in bozza con
  `(after #147)` e `Queued after #147.`. Quando #147 sarà unita, il passo della coda (`CONTRIBUTING.md`, «Phases in a queue»): `main` nel
  branch con un merge, build e tutti i test di nuovo, via la coda, e la PR pronta con la CI verde.
- **Verificato, in locale** (27 settembre 2026, sul branch da `m3/a8a-dates-server`, 25f1ac3): `dotnet build` senza avvisi; unità
  **782/782** (come A8a: la fase non ha test di unità C#; `TrainingArchitectureTests` legge anche il TypeScript nuovo del modulo, ed è
  verde); **integrazione intera senza filtro** **337/337** (le 336 di A8a e il fatto nuovo; la classe `TrainingDatesTests` da sola 8/8);
  `pnpm lint`, `typecheck`, `format:check`, `i18n:check` verdi; `pnpm test` **527** in **66** file (le 519 in 65 di A8a e 8 nuovi); `pnpm
  e2e` **108** (le 101 e le 7 nuove), al primo giro; **`pnpm e2e:full` 44** su un **banco nuovo** di questo worktree (127.0.0.1:5088,
  `ivaohub_e2e_a8b`): le 43 e la spec nuova, al primo giro (su 249662b). Dopo l'ultima correzione (ddf0d31, la chiave inesistente di
  «Trovato» 4): lint, typecheck e format verdi, lo smoke del training **17/17**, il Vitest del modulo **44**, le spec del training del giro
  completo **7/7** su un banco ricreato di nuovo. Il fatto nuovo d'integrazione **cade sul codice di A8a** rimesso com'era (`git restore
  --source=m3/a8a-dates-server` dei due file del server) e passa su quello di A8b, i file rimessi, toccati e ricompilati. `pnpm gen:api` e
  `pnpm i18n:sync` nel commit che li porta; `dotnet format --verify-no-changes`
  sui file C# toccati, test compresi; le regole di `core-guard` rifatte in PowerShell sull'intervallo della fase e sul diff verso `main`:
  nessun file del maintainer, nessuno del nucleo. **A mano**, sul banco di anteprima (127.0.0.1:5090, `ivaohub_preview`; la sessione di A7
  ha spento il suo su richiesta): il trainer propone due date al training ATC del trainee, la prima nel giorno di una sessione pilota
  (datata a mano dallo staff), e la conferma dall'avviso; il trainee le trova da `/training/mine` («Scegli la data») e sceglie la prima;
  il calendario pubblico mostra «ADC · LIBD_TWR» e «PP» senza nomi; Mailpit ha «Date proposte», «Data fissata» a tutti e due e **un**
  promemoria a testa (sopra; `hub_jobs_log`: il giro delle 01:20 UTC «2 session(s) reminded», quello delle 01:35 «0»). Lo staff sposta
  la data a mano (vuota: «Questo campo è obbligatorio.» sul campo), ritira una data proposta, vede sulle caselle di una riga «Questa data
  è già passata.» e «La fine viene prima dell'inizio.», chiude un training con il motivo, che il trainee legge; una data a cavallo della
  mezzanotte UTC si legge con i due giorni in UTC e con uno solo nel fuso della divisione; «Eseguito» nella lista e nella pagina di un
  training datato a mano nel passato. In italiano e in inglese, tema chiaro e scuro; le pagine del trainee larghe 375 px (quelle dello
  staff hanno il difetto noto del nucleo a quella larghezza, A7).
- **Non verificato**: la CI (la dirà la PR). **Il promemoria in Mailpit nella CI**: il giro sul banco non lo aspetta (sopra); visto a mano,
  e provato dal test d'integrazione di A8a attraverso il job. **Un 409 della scelta del trainee e degli avvisi apparsi fra la domanda e
  la scrittura, sul server vero**: la pagina li prova con l'API finta dello smoke, il server li prova A8a. **Una data a cavallo della
  mezzanotte del fuso della divisione nei riquadri**: `spanText` la dice con i due giorni (il suo test Vitest), a mano non vista. **Che le
  spec nuove cadano su una copia indebolita delle pagine** (la conferma degli avvisi tolta, per esempio): non tentato; le spec sono state
  lette contro il codice, e il fatto nuovo del server è provato sul codice di A8a (sopra). **Le pagine dello staff larghe 375 px**: hanno
  il difetto noto del nucleo a quella larghezza (A7).
- **Il passo della coda dopo #147 e la revisione di #148** (29 settembre 2026, [i rilievi](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/148#issuecomment-5891398227);
  la sessione di A8b non c'era più, l'ha fatto la sessione che coordina le correzioni): `main` nel branch con un merge (A7, A8a, #185,
  #188), via `(after #147)`, l'intestazione di `HANDOFF-M3.md` rimessa in pari. I nit:
  1. **Un tipo del calendario senza etichetta** nella lingua di chi legge non si dice più con la sua chiave grezza: «Nel calendario»
     (`training:staff.dates.warning.anyKind`).
  2. **La conferma della data scelta dal trainee** dice l'ora anche nel fuso della divisione, come il riquadro sopra
     (`detail.dates.confirmTitle` con `{{local}}`).
  3. **Il commento di `choosableSlots`** diceva che una data che passa a pagina aperta non si offre più; `now` è il momento in cui la
     pagina è disegnata, quindi resta fino alla lettura dopo, e il server la rifiuta: corretto il commento, non il codice.
  4. **Scritto, non corretto**: un avviso porta sempre alla pagina dello staff dell'altro training, e chi conduce con il permesso su una
     riga sola (A7b) può non poterla aprire. Serve che il server dica se chi legge può aprirla; ⚠️ per A7b o una fase dopo.

### A9 — Dopo la sessione

Design §1.3, §1.4, §2.6, §2.7, §2.8; note `le-note-riservate-e-il-trainee`, `il-tempo-per-la-data-e-le-voci-della-scheda`. Branch
`m3/a9-after-the-session`.

1. **`trn_sessions`** e **`trn_evaluations`** (migrazione).
2. **Rischedula** (poco traffico): la sessione diventa `Rescheduled` con i suoi appunti interni, il training torna `Assigned`, nuove
   disponibilità (A8). Nessun report.
3. **No-show**: sessione `NoShow`, training `NoShow`, si applica `noShowCooldownDays`; mail `trainingClosed`.
4. **La scheda**: generata dalle voci attive di quel percorso e rating, con la **fotografia** di titolo e tipo di voto; per voce voto,
   spunta o **N/A**, commento per il trainee, nota riservata. La domanda «la voce è usata?» di A5 ora risponde davvero.
5. **Il report**: commento generale, commento riservato, «pronto per il mock exam», «pronto per l'esame», «togli l'attesa»;
   **Pubblica** → `Completed`, sessione `Held`, mail `reportPublished`. Il trainer pubblica da solo; nessun superato / non superato.
6. **Il mock exam**: la casella alimenta la funzione di A6, e la richiesta successiva del trainee è un mock exam, con la stessa scheda.
7. **Le note riservate e il trainee** (nota `le-note-riservate-e-il-trainee`): **una funzione sola** costruisce la risposta dello staff
   di un training e toglie i campi riservati quando chi legge è il trainee della riga.

**Test**: unit: la scheda con N/A e la fotografia; integrazione: il ciclo con ogni esito (report, rischedula, no-show) e l'attesa
giusta dopo ciascuno; il mock exam alla richiesta dopo la casella; **un trainer che è anche trainee non legge le note riservate del
proprio training dall'endpoint dello staff**, e le legge su quello di un altro (il test della nota, con l'elenco dei campi tolti); il
trainee dai suoi endpoint non le legge mai. Smoke: la scheda e il report.
**Fatta quando**: il trainer pubblica un report con una voce N/A e «pronto per il mock exam»; il trainee lo legge senza note
riservate, e la sua richiesta successiva dice «questo sarà un mock exam, come concordato con il trainer».

**Divisa il 27 settembre 2026 in apertura**, come A6 e A8 (sopra): il server da solo — due tabelle, la rischedula, il no-show, la
scheda con la fotografia delle voci, il report, la funzione che toglie le note riservate, la sessione tenuta nel calendario, la risposta
vera a «la voce è usata?», due mail e i loro test — è già una PR come quella di A8a; le pagine, con la scheda come form generato, il
report letto dal trainee, lo smoke e il giro sul banco, la raddoppierebbero.

- **A9a — il server** (branch `m3/a9a-after-the-session-server`, preparato come `m3/a9-after-the-session` e rinominato prima del primo
  push): i punti 1–7 sul server, con gli endpoint che scrivono e i DTO che le pagine leggono — per lo staff la scheda da compilare e
  quella compilata, lo storico delle sessioni con gli appunti, il report, che cosa può fare chi legge, e i campi riservati tolti quando
  chi legge è il trainee della riga; per il trainee il report senza note riservate e le sue sessioni senza appunti —; i test unit e
  d'integrazione. Il «fatta quando» lo prova un test d'integrazione, attraverso l'API, come in A6a e A8a.
- **A9b — le pagine** (branch `m3/a9b-after-the-session-pages`, da `m3/a9a-after-the-session-server`): nella pagina dello staff, accanto
  alla sessione nella sezione «Le date», la rischedula con gli appunti, il no-show, la scheda come form generato dalle voci, il report e
  «Pubblica», e lo storico delle sessioni; nella pagina del trainee il report e «che cosa succede dopo» per gli stati nuovi; lo smoke
  della scheda e del report; il giro sul banco con `pnpm e2e:full` e il «fatta quando» di A9. Se una pagina chiede al server qualcosa
  che A9a non dà, è un cambio del modulo nella PR di A9b, detto nel suo «Com'è andata».

**Com'è andata (A9a)** (27 settembre 2026, branch `m3/a9a-after-the-session-server`, PR #149, in coda dopo #148):

- **Classificata prima del codice** (`CLAUDE.md` §5): codice del modulo (caso a) dentro meccanismi che ci sono, usati così come sono
  (caso b) — le righe figlie scritte con il training, come le date di A8a e gli errori di un PIREP (§1.1), sotto il guardiano con i
  permessi alternativi di A3; `IProjectable` del training, esteso nel suo `Project`; `ISheetItemReports` sostituito nella stessa
  registrazione (`TryAddScoped`), come T11 dei tour con `PirepTourReports`; `Localized<string>` per la fotografia del titolo; il servizio
  notifiche con i tipi del modulo; le impostazioni del modulo —. La regola delle note riservate è la nota
  `le-note-riservate-e-il-trainee`, già decisa in A0 (caso c deciso): qui c'è il suo codice. **Nessun file del nucleo**, nessuna nota
  nuova, nessuna domanda a Carmine. Una migrazione, **`AddSessionsAndEvaluations`**, solo additiva: due tabelle, tre indici, le chiavi
  verso il training.
- **Fatto**, come il perimetro di A9a qui sopra:
  1. **`trn_sessions`** (`Sessions/TrainingSession.cs`): la data della sessione, l'esito (`Held`, `Rescheduled`, `NoShow`, come testo), gli
     appunti interni di una sessione rischedulata, i timbri (chi ha registrato l'esito e quando). **`trn_evaluations`**
     (`Sheets/TrainingEvaluation.cs`): una riga per voce, con la **fotografia** della voce — titolo in ogni lingua, sezione, posto nella
     scheda — e quale voce era, il voto 1–5 (pratica) o la spunta `Done`, `NotDone`, `ToImprove` (teoria) — nessuno dei due: N/A —, il
     commento per il trainee e la nota riservata. Figlie del training, con la chiave in cascata; **nessuna chiave verso le voci**, come gli
     errori di un PIREP verso il catalogo: il report legge la sua copia. Nessuna delle due è `[Audited]`.
  2. **La scheda** (`Sheets/EvaluationSheet.cs`, funzioni pure): le voci che un report segna sono le **attive** del percorso e del rating
     del training, nell'ordine di `sort` (`ItemsOf`); `Fill` ne fa la scheda compilata, una copia per voce con la riga del payload che la
     segna, N/A dove niente la segna, e i rifiuti riga per riga.
  3. **I verbi dello staff** (`Sessions/TrainingSessions.cs`, `Staff/StaffEndpoints.cs`), con `Training.Conduct` sulla riga — il trainer con il
     grant sullo scope del training, TC e TAC per posizione —, mai il trainee, **dall'inizio della sessione** (scostamento 1): `POST
     …/reschedule` (gli appunti, facoltativi: la sessione `Rescheduled`, il training di nuovo `Assigned` senza data, la voce del
     calendario via, nessuna mail), `POST …/no-show` (la sessione `NoShow`, il training `NoShow` chiuso da chi lo registra, la voce via, la
     mail `trainingClosed` con l'attesa del no-show), `POST …/report` (la scheda, il commento generale e quello riservato, «pronto per il
     mock exam», «pronto per l'esame», «togli l'attesa»: la sessione `Held`, il training `Completed`, la mail `reportPublished`). Ognuno
     risponde con la pagina com'è dopo, i rifiuti campo per campo, 409 su una versione vecchia.
  4. **«La voce è usata?» risponde davvero**: `EvaluationSheetItemReports` (le schede compilate, sull'indice di `sheet_item_id`) al posto
     di `NoSheetItemReports`, nella stessa registrazione; una voce che un report ha segnato non si elimina più.
  5. **Il mock exam**: la casella del report alimenta `RequestRules.IsMockExam` di A6a così com'è — la richiesta dopo sullo stesso rating
     è un mock exam, e la pagina della richiesta lo dice già (A6b) —; sul report di un mock exam la casella è rifiutata (scostamento 7).
  6. **Le note riservate e il trainee** (nota `le-note-riservate-e-il-trainee`): **`ReservedFields.For`** (`Staff/ReservedFields.cs`) è
     la regola, e **`StaffTrainings.PageAsync`** la funzione unica che costruisce la risposta dello staff di un training e la passa per
     la regola — la risposta di `GET /api/training/trainings/{id}` e di ogni passo —: quando chi legge è il trainee della riga, chiunque
     sia, toglie `staffComment`, la `staffNote` di ogni voce della scheda e gli `internalNotes` di ogni sessione, e lo dice in
     `reservedLeftOut`. Gli endpoint del trainee hanno il loro DTO, che quei campi non li ha.
  7. **Il calendario**: `Training.Project` proietta anche il training `Completed` con la sua data: la sessione tenuta resta (§5.1); la
     rischedula e il no-show azzerano la data, e la voce se ne va (scostamento 2).
  8. **I DTO**: lo staff legge `cooldownWaived`, `generalComment`, `staffComment`, `sheet` (la copia del report per un training
     completato; per uno datato, le voci attive come le segnerebbe un report adesso, senza niente segnato; vuota negli altri stati),
     `sessions` (data, esito, appunti, chi e quando), `reservedLeftOut`, `actions.canRecordOutcome`; il trainee `cooldownWaived`,
     `generalComment`, `sheet` (titolo, sezione, voto o spunta, commento per lui) e `sessions` (data ed esito), **senza** campi riservati.
     Tutto in `web/src/shared/api/schema.d.ts`.
  9. **Le mail**: il tipo nuovo **`reportPublished`** (chi l'ha pubblicato, le caselle «pronto per…», fino a quando si aspetta, la pagina
     del training) e **`trainingClosed`** con la frase del no-show; le parole in italiano e in inglese.
  10. **I test**: `TrainingSessionRulesTests` (unità, 6), `TrainingSessionsTests` (integrazione, 6, VID 790039 e 790045–790049, con il
      «fatta quando» attraverso l'API).
- **Scostamenti e precisazioni, piccoli**:
  1. **L'esito si registra dall'inizio della sessione**, non dal giorno dopo: il design §2.6 dice «dal giorno dopo la data il training si
     mostra Eseguito» e poi le tre strade; le tre strade valgono appena la sessione è cominciata (`TrainingSessions.IsRecordable`), così il
     report di una sessione della sera si scrive la sera stessa. «Eseguito» resta ciò che le pagine mostrano dal giorno dopo, e la vista
     «Da chiudere» resta quella di A7. Prima dell'inizio, una sessione si sposta con la data a mano di A8a.
  2. **Il training completato tiene la data della sessione tenuta** (`scheduled_start_utc`), e la voce del calendario resta: il design §5.1
     dice «il training proietta la sessione in corso e le sessioni Held». Un training ha al più una sessione `Held` — solo il report la
     scrive, e il report chiude il training —, quindi una voce sola, che la riga calcola da sé (una proiezione non legge le righe figlie).
     La riga `Held` di `trn_sessions` è lo storico. La rischedula e il no-show azzerano la data, come la chiusura di A8a.
  3. **La mail `trainingClosed` dice l'attesa nei dati** (`after`): il modello di A8a finiva con «una chiusura non ti fa aspettare», falso
     per un no-show. Le due chiusure di A8a (`TrainingDates.cs`, un file di A8a) mandano la stessa frase (`closedNoWait`); il no-show dice
     fino a quando si aspetta (`waitUntil`), o che si può chiedere quando si vuole con `noShowCooldownDays` a 0.
  4. **Il report prende le voci attive al momento della pubblicazione**: una voce spenta nel frattempo, che la pagina manda, è
     `sheetChanged` sulla lista (`sheet`) e la pagina rilegge; una voce accesa nel frattempo, che la pagina non aveva, entra N/A. Una voce
     pratica ha solo il voto, una di teoria solo la spunta: il contrario è un rifiuto sul campo della riga (`sheet[i].grade`,
     `sheet[i].mark`), con l'indice della riga del payload.
  5. **I limiti dei testi**: commento e nota di una voce e appunti di una sessione fino a 2000 caratteri, come i testi della richiesta; i
     due commenti del report fino a 10.000 (`Training.MaxCommentLength`), dentro ciò che la loro colonna `text` tiene.
  6. **Gli appunti della rischedula sono facoltativi**, e la rischedula non manda mail: il design non li dice obbligatori, e §5.2 non ha una
     mail per la rischedula (la prossima è `datesProposed`).
  7. **«Pronto per il mock exam» su un mock exam è rifiutato** (`reportMockExamAgain`): la regola di A6a non fa di nuovo mock exam il
     training dopo un mock exam, quindi la casella direbbe il falso al trainee.
  8. **Il DTO del trainee porta anche le sessioni passate** (data ed esito, mai gli appunti) e `cooldownWaived`: il design §4.1 nomina il
     report; una sessione rischedulata o un no-show si leggono così dalla pagina del training (A9b).
- **Test di A6b, A7 e A8b toccati, e perché**:
  1. `screens/dates.test.ts` (A8b), `screens/trainee.test.ts` (A6b) e `screens/trainings.test.ts` (A7), Vitest: i costruttori dei DTO hanno i
     campi nuovi con valori neutri (`cooldownWaived: false`, `generalComment: null`, `staffComment: null`, `sheet: []`, `sessions: []`,
     `reservedLeftOut: false`, `canRecordOutcome: false`), perché il tipo generato li chiede; prettier ha riscritto su più righe gli oggetti
     `actions`. Nessuna asserzione è cambiata.
  2. ⚠️ **Un'asserzione di A7 cambiata**, l'unica: `TrainingStaffTests.AnAdvisorAcceptsARequestAndAssignsNothing` diceva che la pagina dello
     staff **non ha** `staffComment` (`Assert.False(page.TryGetProperty("staffComment", out _))`): era il segno, in A7, che i campi
     riservati non c'erano ancora — A7, «Trovato» 5: «il DTO dello staff non ha `StaffComment` né le note della scheda; la funzione unica
     che li toglie al trainee della riga li aggiunge» —. A9 li aggiunge per design, e l'integrazione intera è caduta lì. Ora la riga dice
     ciò che A9 rende vero: il campo c'è, vuoto su una richiesta, e a un advisor, che non è il trainee della riga, la pagina non toglie
     niente (`reservedLeftOut` falso). Nient'altro del test è cambiato. Detto al revisore.
- **Trovato, e scritto per chi viene dopo** (anche in `HANDOFF-M3.md`):
  1. **Per A9b**: i verbi e i DTO del punto 3 e del punto 8; i rifiuti `state` (`sessionNotRecordable`), `notes`, `sheet` (`sheetChanged`),
     `sheet[i].grade|mark|traineeComment|staffNote`, `generalComment`, `staffComment`, `readyForMockExam` (`reportMockExamAgain`).
     ⚠️ **La scheda non è una lista di `SchemaForm`**: le righe hanno campi diversi (il voto per la pratica, la spunta per la teoria) e
     un'etichetta che viene dai dati (il titolo della voce), e `SchemaForm` disegna le stesse caselle per ogni riga con le etichette dei file
     di lingua. La pagina di validazione dei tour (`web/src/modules/flightops/screens/review.tsx`) segna gli errori del catalogo con i
     controlli di Atmosphere: è il precedente da guardare, e da classificare prima di scrivere (una pagina dedicata del modulo con i pezzi
     dell'elenco chiuso è codice del modulo; un campo di `SchemaForm` con l'etichetta dai dati sarebbe nucleo, con la sua nota).
  2. **Per A10**: il percorso del trainee usa la stessa funzione (`StaffTrainings.PageAsync` e `ReservedFields.For`); il blocco
     `training.trainerQueue` ha «report da scrivere» in `actions.canRecordOutcome` (o `TrainingSessions.IsRecordable`).
  3. I VID **790039** e **790045–790049** sono di A9a; A3b usa 790040–790044 e 790050–790051: il prossimo libero è **790052**.
  4. **Già detto dalla nota, non toccato**: il training è `[Audited]`, quindi il registro dell'audit copia `staff_comment` quando un report
     è pubblicato, e chi ha `Audit.View` lo legge dalla schermata del nucleo (nota `le-note-riservate-e-il-trainee` §5). Le note della
     scheda e gli appunti delle sessioni non ci finiscono: le due tabelle nuove non sono `[Audited]`.
- **La coda**: A9a è nata in coda dopo #148 (A8b, in bozza in coda dopo #147, dopo #146, dopo #144, dopo #143): la PR è in bozza con
  `(after #148)` e `Queued after #148.`. Quando #148 sarà unita, il passo della coda (`CONTRIBUTING.md`, «Phases in a queue»): `main` nel
  branch con un merge, build e tutti i test di nuovo, via la coda, e la PR pronta con la CI verde.
- **Verificato, in locale** (27 settembre 2026, sul branch da `m3/a8b-dates-pages`, 83c362c): `dotnet build` senza avvisi; unità **788/788**
  (le 782 di A8b e le 6 nuove; `TrainingArchitectureTests` legge anche il C# nuovo del modulo, e il test di #138 le chiavi `training:` del
  C#: verdi); **integrazione intera senza filtro** **343/343** (le 337 e le 6 nuove; la classe nuova da sola 6/6 al primo giro). La prima
  corsa intera è finita 342/343: è caduta l'asserzione di A7 sopra («Test … toccati» 2); cambiata quella riga, le cinque classi del
  training insieme 32/32 e la corsa intera di nuovo 343/343. `pnpm lint`, `typecheck` (fermato prima dai costruttori di Vitest, sopra),
  `format:check` (prettier sui due file di Vitest), `i18n:check` verdi, e lo script che confronta le chiavi letterali `training:` del
  modulo con i file di lingua; `pnpm test` **527** in **66** file (come A8b: la fase non ha codice del front end); `pnpm e2e` **108**;
  **`pnpm e2e:full` 44** su un **banco nuovo** di questo worktree (127.0.0.1:5092, `ivaohub_e2e_a9`), al primo giro: i giri di A6b, A7 e
  A8b leggono i DTO nuovi dal server vero e la migrazione nuova parte all'avvio. `pnpm gen:api` e `pnpm i18n:sync` nel commit, senza
  differenze dopo; `dotnet format --verify-no-changes` sui file C# toccati, test compresi; le regole di `core-guard` rifatte in PowerShell
  sull'intervallo della fase e sul diff verso `main`: nessun file del maintainer, nessuno del nucleo. **A mano**: niente da guardare, la
  fase non ha schermate (il banco di anteprima è di A9b).
- **Non verificato**: la CI (la dirà la PR). **Le pagine**: A9a non ne ha; il «fatta quando» con le pagine è il giro di A9b sul banco.
  **Che il test della nota cada su una copia indebolita del codice** — la chiamata a `ReservedFields.For` tolta da
  `StaffTrainings.PageAsync` —: **rifiutata dalla modalità automatica dei permessi** («Security Weaken»), e non l'ho aggirata; la regola è
  provata da sola dal test di unità (`TheStaffsPageLeavesOutWhatIsReservedWhenItsReaderIsTheTrainee`), e il test d'integrazione è stato
  letto contro il codice: senza la chiamata, `reservedLeftOut` sarebbe falso e i tre campi pieni. I test nuovi sul codice di A8b non
  compilano (i tipi e gli endpoint nascono qui). **Due scritture dello stesso training nello stesso momento** (un report e una
  rischedula): la versione della riga fa della seconda un 409 — provato con una versione vecchia, non con due richieste insieme. **La
  mail in Mailpit**: i test d'integrazione leggono le mail in coda; la consegna è del servizio del nucleo.
- **La revisione di #149** (29 settembre 2026, [i rilievi](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/149#issuecomment-5891398650);
  la sessione di A9a non c'era più, i nit li ha corretti la sessione che coordina la coda):
  1. **Una voce della scheda scritta due volte** in un report non si rifiuta più con `sheetChanged` («ricarica la pagina», che non
     aiuterebbe) ma con `training:errors.evaluationItemTwice`; una voce che non è sulla scheda resta `sheetChanged`.
  2. **Un giudizio fuori dall'elenco** su una voce di teoria si rifiuta con `training:errors.evaluationMarkUnknown`, non con
     `errors.required`. Il test di unità `AReportIsRefusedOnTheFieldOfTheItemItGetsWrong` dice tutti e due.
  3. **La mail del no-show** («The trainer marked…») non nomina più il trainer: il no-show lo registrano anche TC e TAC.
  4. Due punti aspettavano Carmine: **una data forzata su una sessione già iniziata** (la domanda che A8a aveva lasciato per A9) e **gli
     scostamenti 1, 2 e 7**. Le risposte sono nella voce qui sotto.
- **Le risposte di Carmine su #149** (29 settembre 2026, [la risposta](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/149#issuecomment-5891427158),
  data in chat al master e postata da lui; il codice l'ha scritto la sessione che coordina la coda, sul branch temporaneo `fix3/a9a` da
  `6020f33`):
  1. **Gli scostamenti 1, 2 e 7 sono accettati**, con il link qui: l'esito si registra dall'inizio della sessione, un training `Completed`
     resta nel calendario, «pronto per il mock exam» si rifiuta su un mock exam.
  2. **Una sessione cominciata si registra, non si data più né si chiude**: da quando vale `TrainingSessions.IsRecordable`,
     `TrainingDates.SetAsync` e `CloseAsync` rifiutano sullo stato con la chiave nuova **`training:errors.sessionStarted`** (in inglese e
     in italiano), prima di guardare il resto del payload. La pagina dello staff smette di offrirli: in `StaffTrainings`, `canConduct` e
     `canClose` sono falsi sulla sessione cominciata, e resta `canRecordOutcome`. Il commento di `dateSteps` (`screens/dates.ts`) lo dice.
  3. **E nessuno data un training nel passato**: la data a mano prima di adesso si rifiuta con `slotPassed` su `startsAtUtc`, come la
     scelta del trainee fra le date proposte — la risposta dice «(and a date in the past)». ⚠️ **Lo scostamento 2 di A8a non vale più**
     per questa parte: l'override scriveva «anche nel passato» (una sessione tenuta prima del previsto); ora una sessione tenuta si
     registra, e l'override è solo per un momento ancora da venire.
  4. **I test**: il nuovo `TrainingSessionsTests.ASessionThatHasStartedIsRecordedNeitherDatedAgainNorClosed` (un `Scheduled` cominciato da
     mezz'ora: la pagina non offre la data né la chiusura, i due passi rifiutati su `state` con `sessionStarted`, niente scritto; un
     `Assigned` datato dieci minuti fa: `slotPassed`). Al coordinatore della classe il test dà `Training.Approve`, che gli altri test non
     gli danno: senza, la chiusura sarebbe vietata (403) e non rifiutata. **Il test cade senza il rifiuto**, come Carmine chiede: su una
     copia di `TrainingDates.cs` senza i tre controlli nuovi (e senza la `using` che restava inutile), la data a mano su una sessione
     cominciata passa (200, spostata a domani) e il test cade alla riga del primo rifiuto; il file poi rimesso e ricompilato.
  5. ⚠️ **Due test di A9a toccati, e perché**: `ASessionRescheduledTakesTheTrainingBackToItsDatesWithItsNotesAndMakesNobodyWait` e l'aiuto
     `ReportedWithNotesAsync` datavano la seconda sessione a mano nel passato (`-30` e `-10` minuti), che ora si rifiuta. La datano un'ora
     avanti con lo stesso passo, e l'aiuto nuovo `StartedAMomentAgoAsync` sposta `scheduled_start_utc` a dieci minuti fa come fa
     l'installazione; la versione si rilegge dalla pagina. Le asserzioni non cambiano.
  6. **Nessun test di A8a toccato**: le date a mano e le chiusure di `TrainingDatesTests` sono tutte nel futuro; le spec del banco di A8b
     pure (`daysAhead(4)`).
- **Il passo della coda dopo #148** (29 settembre 2026: #148 unita alle 15:23 UTC; l'ha fatto la sessione che coordina la coda): `main` nel
  branch con un merge (9f812b9) — nessuna differenza di contenuto, `main` aveva solo il merge di #148, già nel branch —, via `(after #148)`
  dal titolo e `Queued after #148.` dal corpo, la PR pronta a CI verde. **Verificato di nuovo, in locale** (7b3f29a): `dotnet build` senza
  avvisi; unità **858/858**; integrazione intera **388/388**; `pnpm gen:api` e `pnpm i18n:sync` senza differenze; `lint`, `typecheck`,
  `format:check`, `i18n:check` verdi; Vitest **548/548** in 70 file; smoke **123/124** al primo giro — è caduta `tours-map.spec.ts:160`, la
  mappa di un tour (M2), il flake noto sotto carico, con tre giri di suite insieme —, e la spec da sola con `--repeat-each 5` **10/10**;
  **`e2e:full` 44/44** su un banco nuovo (127.0.0.1:5106).

**Com'è andata (A9b)** (27 settembre 2026, branch `m3/a9b-after-the-session-pages`, PR #150, in coda dopo #149):

- **Classificata prima del codice** (`CLAUDE.md` §5): codice del modulo (caso a) dentro meccanismi che ci sono, usati così come sono
  (caso b) — la pagina dedicata dello staff (A7) e quella del trainee (A8b); `SchemaForm` per il report nel suo insieme (i due commenti e
  le tre caselle, con `hidden` sulla casella del mock exam) e per gli appunti della rischedula dentro `ConfirmDialog` (come «Rifiuta» di A7
  e la chiusura di A8b), con `id` e `actionsElsewhere` per «Pubblica» chiesto prima (come la domanda sul teorico di A6b); `ConfirmDialog`
  per il no-show e per la pubblicazione; i controlli di Atmosphere (`RadioGroupRoot`, `RadioGroupItem`, `Label`, `Textarea`, `Badge`) per le
  righe della scheda, come la pagina di validazione dei tour segna gli errori del catalogo con `Checkbox` e `Label` e come la pagina della
  richiesta usa già `RadioGroupRoot` (A6b); `describeProblem` per i rifiuti di una riga; `useLocalized`, `useMoment`, `Notice`,
  `useNotice`. **Nessun file del nucleo**, nessuna nota nuova, nessuna domanda a Carmine, **nessun cambio del server**: le pagine leggono i
  DTO di A9a così come sono (`pnpm gen:api` senza differenze).
  - **La scheda non è un campo di `SchemaForm`** (A9a, «Trovato» 1): le sue righe hanno un'etichetta che viene dai dati (il titolo della
    voce) e un controllo che dipende dalla sezione (il voto per la pratica, la spunta per la teoria), mentre `SchemaForm` disegna le stesse
    caselle per ogni riga con le etichette dei file di lingua: un campo con l'etichetta dai dati, o un tipo di campo nuovo, sarebbe
    un'estensione del nucleo, con la sua nota e la sua PR. La scheda è quindi una parte della pagina dedicata del modulo, composta con i
    controlli di Atmosphere, e il report nel suo insieme è il form generato: la stessa divisione della pagina di validazione dei tour (M2
    §4.3: la tabella degli errori con i controlli di Atmosphere, la decisione con il form generato). Il design §4.2 dice «la scheda (form
    generato dalle voci)»: le righe sono generate dalle voci che il server manda, i controlli no (scostamento 1, detto al revisore).
- **Fatto**, come il perimetro di A9b qui sopra:
  1. **La pagina dello staff** (`screens/staff.tsx`): nella sezione «Le date», accanto alla sessione, quando il server dice
     `actions.canRecordOutcome` (dall'inizio della sessione, A9a), una riga che dice le tre strade, **«Rischedula»** — gli appunti interni,
     facoltativi, in un campo generato dentro `ConfirmDialog`: la sessione va fra quelle passate con i suoi appunti, il training torna ad
     aspettare la data e la sezione ripropone da sola la proposta e la data a mano (`dateSteps`) — e **«No-show»**, chiesto prima. Una
     sezione **«Il report»**: per ogni voce di `sheet`, nell'ordine del server, il titolo nella lingua a schermo e la sezione, i voti da 1 a
     5 (pratica) o le spunte Fatto, Non fatto, Da migliorare (teoria) con **«N/A» per primo, e scelto finché non si sceglie altro**, il
     commento per il trainee e la nota per lo staff; poi il form generato — il commento generale per il trainee, il commento per lo staff,
     «Pronto per il mock exam» (mai su un mock exam: `isMockExam`), «Pronto per l'esame», «Nessuna attesa dopo questo training» —, e
     **«Pubblica il report»**, chiesto prima. Una sezione **«Le sessioni»** con lo storico (la data in UTC e nel fuso della divisione,
     l'esito, gli appunti interni, chi l'ha registrata e quando), e per un training completato **il report pubblicato** (chi l'ha pubblicato
     e quando, dalla sessione `Held`; per voce il voto, la spunta o N/A, «Per il trainee: …» e «Per lo staff: …»; i due commenti; le
     caselle). **`reservedLeftOut`**: in cima alla pagina, a un trainer che legge il proprio training, un avviso che le note che lo staff
     scrive per sé non gli sono mostrate.
  2. **Il report manda una voce per ogni riga della pagina**, nel suo ordine, una riga non toccata con niente segnato (N/A, d4): così
     `sheet[2]` di un rifiuto è la terza riga a schermo, e un commento su una voce N/A arriva. **I rifiuti** (`splitReportRefusal`): quelli
     dei campi del form generato sul loro campo; quelli di una riga (`sheet[i].grade|mark|traineeComment|staffNote`) sotto il controllo della
     riga, nelle parole dei file di lingua (`describeProblem` su un rifiuto ridotto a quel campo, `refusalOn`); gli altri (`sheet` con
     `sheetChanged`, `state`) sopra il form. **`sheetChanged` e un 409 rileggono la pagina** tenendo ciò che è scritto: le righe si
     ritrovano per voce, una voce accesa nel frattempo entra N/A, e i rifiuti di riga di prima si lasciano cadere (contavano le righe di
     prima).
  3. **La pagina del trainee** (`screens/traineeTraining.tsx`): per un training completato **il report** — pubblicato il…, per voce il
     voto, la spunta o N/A e il commento per lui, il commento generale, le caselle «Pronto per il mock exam», «Pronto per l'esame», «Attesa
     tolta dal trainer» —, **mai** note riservate né appunti: il suo DTO non li ha, e la pagina non disegna nemmeno le etichette dello staff;
     **le sessioni passate** (rischedulata, no-show, eseguita, con la data); **«che cosa succede dopo»** per `Completed` e `NoShow`
     (`detail.next.*`) e, dal percorso di quel training in `GET /api/training/mine`, fino a quando dura l'attesa, o «Puoi chiedere il
     prossimo training…» con «Richiedi training» e — quando il server lo dice — «Questo sarà un mock exam, come concordato con il trainer».
  4. **`/training/mine`**: un training completato dice «Il trainer ha pubblicato il report.» e porta alla sua pagina con «Leggi il
     report»; le caselle del report sono il pezzo comune `ReportBoxes`, con «Attesa tolta dal trainer» accanto alle due che c'erano.
  5. **Le funzioni pure** in `screens/report.ts` (`recordsOutcome`, `choicesOf`, `sheetEntries`, `evaluationSays`, `splitReportRefusal`,
     `refusalOn`, `asksRereading`, `publishedBy`, `nextOnTheLadder`, `OUTCOME_COLOURS`); i pezzi comuni in `screens/parts.tsx`
     (`ReportView`, `ReportBoxes`, `SessionList`), per la pagina dello staff e per quelle del trainee; `api.ts` (i tipi nuovi, i passi
     `reschedule`, `noShow` e `report` di `useStaffStep`); `schemas.ts` (`rescheduleSchema`, `notesFromFormValues`, `reportSchema`,
     `EMPTY_REPORT`, `reportFromFormValues`); le parole (`outcomes`, `marks`, `report`, `sessions`, `mine.cooldownWaived|reportReady|readReport`,
     `detail.next.Completed|NoShow`, `detail.askAgain|report|sessions`, `staff.reserved`, `staff.sections.sessions|report`,
     `staff.session.recordable`, `staff.reschedule`, `staff.noShow`, `staff.report`) in italiano e in inglese, copiate da `pnpm i18n:sync`.
  6. **I test**: Vitest `screens/report.test.ts` (10) e `schemas.test.ts` (3 nuovi); lo smoke `web/e2e/training-report.spec.ts` (7, con
     l'API finta: la scheda compilata e il report pubblicato, chiesto prima; un rifiuto su una riga e la scheda cambiata che rilegge la
     pagina; la rischedula con gli appunti e la proposta che torna; il no-show chiesto prima; il mock exam senza la sua casella e il trainer
     che legge il proprio training; il report letto dal trainee senza niente dello staff; il no-show letto dal trainee con l'attesa); il giro
     sul banco `web/e2e/full/training-the-report.spec.ts`, **il «fatta quando» di A9** (sotto).
- **Scostamenti e precisazioni, piccoli**:
  1. **La scheda è disegnata dal modulo, il report nel suo insieme dal form generato** (sopra): il design §4.2 dice «la scheda (form generato
     dalle voci)». Detto al revisore: se Carmine vuole la scheda nel form generato, è un'estensione di `SchemaForm` (un campo con
     l'etichetta dai dati e il controllo per sezione), fase del nucleo con la sua nota.
  2. **«Pubblica» è chiesto prima** (`ConfirmDialog`), come ogni passo di questa pagina che non si disfa (accetta, rifiuta, ritira una
     data, chiudi, no-show): il report non si cambia più, e il trainee lo legge subito e riceve una mail.
  3. **Il report manda tutte le righe**, anche quelle N/A: il server accetta le due forme (A9a: una voce che nessuna riga segna, o la cui
     riga non segna niente, è N/A), e così l'indice di un rifiuto è la riga a schermo.
  4. **La rischedula e il no-show stanno nella sezione «Le date», accanto alla sessione; il report in una sezione sua sotto**, «Il report»,
     che per un training completato mostra il report pubblicato: la scheda è lunga, e la sezione serve anche dopo, quando «Le date» non c'è
     più. **Lo storico delle sessioni** è anch'esso una sezione, «Le sessioni», in ogni stato che ne ha una (anche un training chiuso dopo
     una rischedula).
  5. **«Che cosa succede dopo» legge anche `GET /api/training/mine`**, il percorso del training: l'attesa (`waitUntil`) o la richiesta
     successiva con il mock exam (`refusal`, `isMockExam`), che ci sono da A6a. Nessun cambio del server.
  6. **L'esito `Held` di una sessione si dice «Eseguita»**, come lo stato mostrato «Eseguito» (A8b).
- **Test di A6b, A7 e A8b toccati, e perché**: lo smoke `web/e2e/training-request.spec.ts` (A6b), `web/e2e/training-staff.spec.ts` (A7) e
  `web/e2e/training-dates.spec.ts` (A8b): i costruttori dei DTO finti hanno i campi nuovi di A9a con valori neutri (`cooldownWaived: false`,
  `generalComment: null`, `staffComment: null`, `sheet: []`, `sessions: []`, `reservedLeftOut: false`, `canRecordOutcome: false`), perché le
  pagine ora li leggono (una lista assente fa cadere la pagina); prettier ha riscritto su più righe due oggetti `actions`. **Nessuna
  asserzione è cambiata.**
- **Il giro sul banco** (`training-the-report.spec.ts`, un nome che viene dopo tutti gli altri giri del training): il trainee chiede un
  training **pilota** (il giro di A8b lascia liberi i due percorsi), che lo staff accetta, assegna al trainer del banco e data a mano a
  **ieri**, attraverso l'API; la spec scrive attraverso l'API tre voci della scheda di quel rating (due di pratica e una di teoria, con un
  segno nel titolo). Il trainer, rientrato, **rischedula dalla pagina** con gli appunti — la proposta torna, e la sessione è fra quelle
  passate con i suoi appunti —; datato di nuovo a ieri, **compila la scheda dalla pagina** (un voto con un commento e una nota, una spunta,
  una voce lasciata N/A), il commento generale e quello per lo staff, «Pronto per il mock exam» e «Nessuna attesa…», e **pubblica**. Il
  trainee lo legge da `/training/mine` («Leggi il report»): il voto, la spunta, N/A, il suo commento, il commento generale, le caselle, la
  sessione rischedulata e quella eseguita — **e il testo della sua pagina non contiene né la nota, né il commento per lo staff, né gli
  appunti** —; la sua pagina e **la richiesta successiva dicono «Questo sarà un mock exam, come concordato con il trainer»**; **la mail del
  report arriva in Mailpit**. Alla fine le voci si spengono (una voce segnata da un report non si elimina; una che nessun report ha usato,
  lasciata da una corsa fermata a metà, si elimina). **Nessun test di A6b, A7 o A8b è cambiato** nel giro sul banco. ⚠️ Il training resta
  completato, con la sua sessione nel calendario pubblico, e la richiesta pilota successiva del trainee è un mock exam senza attesa: il banco
  va ricreato prima di ogni corsa, come già scritto.
- **Trovato, e scritto per chi viene dopo** (anche in `HANDOFF-M3.md`):
  1. **Per A10**: `ReportView`, `ReportBoxes` e `SessionList` (`screens/parts.tsx`) leggono il DTO dello staff e quello del trainee: il
     percorso del trainee (§4.2) e il blocco `training.myTraining` («l'ultimo report», §4.3) li riusano; «report da scrivere» di
     `training.trainerQueue` è `actions.canRecordOutcome` (A9a).
  2. **Non toccato (nucleo)**, detto al revisore: il log delle richieste scrive **500**, con un ERR e la traccia, per una
     `DomainRefusalException` che il client riceve come **400** — `app.UseExceptionHandler()` sta fuori da `app.UseSerilogRequestLogging()`
     (`src/IvaoHub.Web/Program.cs`), quindi il log della richiesta vede l'eccezione passare prima che il gestore la traduca —. Lo si vede sul
     banco eliminando una voce usata da un report (`sheetItemUsed`), e vale per ogni rifiuto di questo tipo (anche i tour).
  3. VID: A9b non ne usa; il prossimo libero resta **790052**.
- **La coda**: A9b è nata in coda dopo #149 (A9a, in bozza in coda dopo #148, dopo #147, dopo #146, dopo #144, dopo #143): la PR è in bozza
  con `(after #149)` e `Queued after #149.`. Quando #149 sarà unita, il passo della coda (`CONTRIBUTING.md`, «Phases in a queue»): `main` nel
  branch con un merge, build e tutti i test di nuovo, via la coda, e la PR pronta con la CI verde.
- **Verificato, in locale** (27 settembre 2026, sul branch da `m3/a9a-after-the-session-server`, af16a73): `dotnet build` senza avvisi
  (nessun file C# cambiato); unità **788/788** (come A9a: la fase non ha test C#; `TrainingArchitectureTests` legge anche il TypeScript
  nuovo del modulo, ed è verde); **integrazione intera senza filtro** **343/343** (come A9a: nessun cambio del server); `pnpm lint`,
  `typecheck`, `format:check`, `i18n:check` verdi, e lo script che confronta le chiavi letterali `training:` del modulo con i file di lingua;
  `pnpm test` **540** in **67** file (le 527 in 66 di A9a e le 13 nuove); `pnpm e2e` **115** (le 108 e le 7 nuove; il solo training prima
  23/24: il filtro della spec nuova su «rescheduled» prendeva anche l'avviso nell'angolo, un `li` anch'esso, e ora filtra per il badge);
  **`pnpm e2e:full` 45** su un **banco nuovo** di questo worktree (127.0.0.1:5093, `ivaohub_e2e_a9b`) al primo giro intero, e di nuovo
  **45/45** su un banco ricreato con una pubblicazione nuova, dopo le due correzioni trovate a mano (sotto); la spec nuova da sola si era
  fermata prima sul posto di una voce (`sort` fino a 999: la spec aveva 9000), corretto nella spec. **Lo smoke nuovo cade sul codice di
  A9a**: con i sei file delle pagine e delle chiamate rimessi da `m3/a9a-after-the-session-server`, `training-report.spec.ts` 0/7; rimessi
  com'erano sul branch (e toccati), 7/7. `pnpm gen:api` senza differenze (nessun endpoint cambiato); `pnpm i18n:sync` nel commit; nessun
  file C# toccato, quindi niente `dotnet format`; le regole di `core-guard` rifatte in PowerShell sull'intervallo della fase e sul diff
  verso `main`: nessun file del maintainer, nessuno del nucleo. **A mano**, sul banco di anteprima (127.0.0.1:5090, `ivaohub_preview`; la
  sessione di A8b ha spento il suo su richiesta), in italiano: come trainer, sul training pilota #7 (la sessione del 25 settembre,
  eseguita), le tre strade accanto alla sessione, la sezione «Il report» con l'avviso di un rating senza voci, poi — scritte tre voci per
  quel rating (pratica, teoria, pratica) — la scheda; **rischedulato dalla pagina** con gli appunti (il training di nuovo «Trainer
  assegnato», la proposta e la data a mano di nuovo lì, «Le sessioni» con gli appunti e chi li ha registrati); datato di nuovo a mano nel
  passato; la scheda compilata (4 con un commento e una nota, «Da migliorare», N/A), i due commenti, «Pronto per il mock exam» e «Nessuna
  attesa…», **pubblicato**, chiesto prima; il report pubblicato. Come trainee: `/training/mine` («Il trainer ha pubblicato il report.», le
  caselle, «Leggi il report»), la pagina di #7 con il report, le sessioni e «che cosa succede dopo» con «Questo sarà un mock exam, come
  concordato con il trainer.» e «Richiedi training» — **e nel suo testo nessuna delle note dello staff** —; `/training/request?kind=Pilot`
  dice il mock exam; la mail «Report pubblicato» in Mailpit. **Trovato a mano e corretto**: le linee fra le voci del report avevano il
  colore del testo (`divide-y` senza colore, in Tailwind v4: aggiunto `divide-border`); la conferma di «Rischedula» era rossa (il
  predefinito di `ConfirmDialog` è distruttivo): ora blu, e il no-show resta rosso. Sul banco ripubblicato con le correzioni: la pagina del
  trainee in inglese larga 375 px, in tema chiaro e scuro; la pagina dello staff in inglese a tema scuro, con il report pubblicato e — su un
  training nuovo del trainee, #8, un **mock exam** chiesto, accettato, assegnato e datato a ieri attraverso l'API — la scheda da compilare,
  **senza la casella «Pronto per il mock exam»**. #8 resta sul banco di anteprima, pronto per un report scritto a mano.
- **Non verificato**: la CI (la dirà la PR). **Un rifiuto di una riga e `sheetChanged` contro il server vero**: le pagine li trattano nello
  smoke, con l'API finta; il lato del server è dei test d'integrazione di A9a. **Un trainer che legge il proprio training sul server vero**
  (`reservedLeftOut`): il banco non ha un trainer che sia anche trainee; lo smoke lo disegna, e il test d'integrazione di A9a prova la
  regola. **Il no-show attraverso le pagine sul server vero**: lo smoke lo fa con l'API finta, A9a prova il lato del server; sul banco i due
  percorsi del trainee servono al giro del report. **Le tre strade e la scheda larghe come un telefono**: le pagine dello staff hanno il
  difetto noto del nucleo a 375 px (A7).
- **La revisione di #150** (29 settembre 2026, [i rilievi](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/150#issuecomment-5891399141);
  la sessione di A9b non c'era più, il nit l'ha corretto la sessione che coordina la coda):
  1. **La chiave di `SessionList`**: le sessioni dello staff portano il loro `id` e la lista lo usa; quelle del trainee non lo portano
     (`TraineeSessionDto` ha solo l'inizio e l'esito) e restano all'indice, nell'ordine del server che niente cambia a pagina aperta.
  2. Un punto aspettava Carmine: **la scheda è disegnata a mano** (`SheetRow`, scostamento 1), mentre il design §4.2 dice «la scheda
     (form generato dalle voci)» e `CLAUDE.md` §2 vuole i form generati. La risposta è nella voce qui sotto.
- **La risposta di Carmine su #150** (29 settembre 2026, [la risposta](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/150#issuecomment-5891427556),
  data in chat al master e postata da lui; scritta dalla sessione che coordina la coda, sul branch temporaneo `fix3/a9b`):
  1. **La scheda disegnata nel modulo è accettata**: **scostamento dal design §4.2**, con il link qui. `SheetRow` resta com'è —
     `RadioGroupRoot` e `Textarea` di Atmosphere, come `flightops/screens/review.tsx` —, e **nessuna fase del nucleo estende `SchemaForm`**
     per la scheda. Il report nel suo insieme resta del form generato.
  2. **Il giro sul banco non data più a ieri** (la risposta di Carmine su #149, sotto A9a: nessuno data un training nel passato): la
     spec `training-the-report.spec.ts` data la sessione a mano **dieci secondi avanti** e aspetta che il server la dica da registrare
     (`actions.canRecordOutcome`), sia la prima volta sia dopo la rischedula (`startedInAMoment`, al posto di `yesterdayAt`). Alla fine,
     un training di questa corsa con la sessione cominciata e non registrata — una corsa fermata a metà — si chiude con un **no-show**,
     non con la chiusura dello staff, che ora lo rifiuta. Il resto della spec non cambia. ⚠️ Il racconto del giro qui sopra («datato a
     ieri») e il giro a mano (datato «nel passato») sono di prima della risposta.
- **Il passo della coda dopo #149** (29 settembre 2026: #149 unita alle 16:22 UTC; l'ha fatto la sessione che coordina la coda): `main`
  nel branch con un merge (01016d9) — nessun codice nuovo, `main` portava solo i documenti del passo della coda di A9a; un conflitto,
  l'intestazione di `HANDOFF-M3.md`, che tiene quella di A9b riscritta —, via `(after #149)` dal titolo e `Queued after #149.` dal corpo,
  la PR pronta a CI verde. **Verificato di nuovo, in locale** (e232236), tutto al primo giro: `dotnet build` senza avvisi; unità
  **858/858**; integrazione intera **388/388**; `pnpm gen:api` e `pnpm i18n:sync` senza differenze; `lint`, `typecheck`, `format:check`,
  `i18n:check` verdi; Vitest **561/561** in 71 file; smoke **131/131**; **`e2e:full` 45/45** su un banco nuovo (127.0.0.1:5107).

### A10 — Blocchi, pagine pubbliche, percorso, esami, ban

Design §1.5, §1.5-bis, §2.8, §2.9, §4.1, §4.2, §4.3, §5.1; note `il-training-in-pubblico`, `chi-conduce-e-chi-scrive-un-training` (gli
esami), `le-note-riservate-e-il-trainee` (il percorso). Branch `m3/a10-blocks-exams-bans`. Se in apertura risulta troppo per una PR,
si divide (A10a esami, ban e percorso; A10b blocchi e pagine pubbliche), scritto qui.

1. **I blocchi Data**, ognuno nelle due metà (TypeScript e C#; `ArchitectureTests.TheServerKnowsEveryBlockTypeTheBrowserRegisters`):
   `training.upcomingSessions` (senza nomi per chi non ha fatto il login), `training.myTraining` (per percorso: la richiesta aperta,
   la prossima data, «scegli la data», l'ultimo report, «pronto per…», l'attesa, un ban), `training.trainerQueue` (date da
   proporre, **in attesa di scelta da N giorni** dopo `responseReminderDays`, report da scrivere), `training.approvalQueue`. A un
   visitatore i blocchi personali rispondono `signedIn: false`, come `myTours`; entrano in `/me` e `/staff` come `myTours` in M2.
   ⚠️ **Quattro blocchi alzano due conteggi scritti in test condivisi** (`web/src/features/admin/uiKit.test.ts`, oggi 38 blocchi;
   `DataBlockEndToEndTests`, oggi 13 blocchi Data): toccarli è nucleo per `core-guard`, e la regola 3 di `CLAUDE.md` §0 vieta di
   cambiare un test che non si è scritto per farlo passare. ~~La domanda va a Carmine in apertura di A10~~ **Deciso da Carmine
   il 25 settembre 2026** ([commento sulla PR #125][c125], come raccomandato): **i due conteggi si alzano** dei blocchi che A10
   aggiunge. È il caso che `CONTRIBUTING.md` descrive già («A new block bumps two counts»), non far passare un test; `core-guard`
   classifica i due file come nucleo, quindi la PR di A10 aggiunge una nota breve che lo dice, con il link a quel commento.
   Nient'altro cambia in quei test.
2. **La pagina pubblica `/training`**: i prossimi training ed esami (il blocco) e «Richiedi training»; **`/training/sessions/{id}`**:
   postazione, rating, data e ora; VID e nomi solo con il login (nota `il-training-in-pubblico`).
3. **Il percorso del trainee** `/staff/training/trainees/{vid}`: tutti i training per percorso e rating, «pronto per…», attesa, ban,
   con la funzione della risposta dello staff di A9 (un trainer che guarda il proprio percorso non vede i campi riservati); da qui
   «Banna».
4. **Gli esami**, `trn_exams` (migrazione): candidato, esaminatore (chi scrive), percorso, rating, postazione, data e ora; lista e form
   generati dietro `Training.ManageExams`, la creazione con `[AlsoWrittenWith]` anche alla creazione (A3); **cambiarli e toglierli
   dall'hub: solo HQ, TC, TAC e il TA a cui l'esame è assegnato** (Carmine, risposta 4 sulla #131; la regola nel nucleo è di A3b), e
   togliere è solo una rimozione dal calendario; una voce di calendario `exam` (A2) pubblica, senza nomi per i visitatori. Niente
   esito, niente voto. ⚠️ **Del candidato e dell'esaminatore solo il
   VID**: nessun nome, nessun indirizzo, nessun altro dato personale nella riga (richiesta del TD, `dalberone`, 25 settembre 2026;
   in cima a `HANDOFF-M3.md`).
5. **I ban**: `/staff/training/bans` (lista e form generati, `Training.Ban`, negato all'interessato), «Banna» con motivo e scadenza
   facoltativa, «Togli ban» con chi e quando; mail `banned`; un ban vale per i due percorsi, e i training già aperti vanno avanti.

**Test**: integrazione: i blocchi rispondono per chi guarda (un visitatore `signedIn: false` e senza nomi); un TA crea
un esame **senza `Edit`** e un trainer no (nota `2026-09-26-gli-esaminatori`), e solo il TA a cui è assegnato (con HQ, TC e TAC) lo cambia e lo toglie; un bannato non chiede, un ban scaduto o tolto sì; nessuno banna sé stesso; il percorso del trainer-trainee
senza campi riservati. Smoke: la pagina pubblica, la sessione con e senza login.
**Fatta quando**: la pagina `/training` mostra a un visitatore i prossimi training ed esami senza VID né nomi, e con il login la pagina
della sessione li mostra; un TA inserisce un esame; un ban blocca la richiesta successiva.

**Divisa il 27 settembre 2026 in apertura**, in tre parti e non nelle due scritte qui sopra. **A3b (#135) non è unita**: la sua nota è
decisa da Carmine ([il suo commento sulla #135][a1-135]) e il codice è approvabile, ma il branch aspetta ancora `main` e il via; e di ciò
che la riga di un esame dichiara — `IHasAssignee`, `OnlyForAssignee`, `AlsoOnDeletion` (nota `2026-09-26-le-righe-affidate-a-chi-scrive`
§3.6, sul branch di #135) — il nucleo di questo branch non ha niente. Una parte che dipende da un cambio del nucleo ancora in revisione non
si mette in coda sopra la domanda (regole di tutte le fasi), e non si scrive codice che finge che A3b ci sia: **gli esami vanno in una parte
loro, l'ultima**. Il resto della prima parte scritta sopra («esami, ban e percorso») non usa A3b e viene per primo; i blocchi e le pagine
pubbliche, che da soli sono già una PR come quelle di A8 e A9, in mezzo.

- **A10a — il percorso e i ban** (branch `m3/a10a-path-and-bans`, preparato come `m3/a10-blocks-exams-bans` e rinominato prima del primo
  push): il punto 3 — il percorso del trainee, l'endpoint e la pagina, con la funzione della risposta dello staff di A9 e il test della
  nota allargato lì — e il punto 5 — i ban con la lista e il form generati, «Banna» dal percorso, «Togli ban», la mail `banned` —. Nessuna
  migrazione (`trn_bans` è intera da A6a), nessun file del nucleo. **Test**: integrazione: il percorso per chi guarda, il trainer-trainee
  senza campi riservati; un bannato non chiede, un ban scaduto o tolto sì; nessuno banna sé stesso, superadmin compreso; chi non ha
  `Training.Ban` non banna. Smoke: il percorso e i ban. **Fatta quando**: dal percorso lo staff banna il trainee, la sua richiesta successiva
  è rifiutata per il ban, e tolto il ban la richiesta si può fare.
- **A10b — i blocchi e le pagine pubbliche** (branch `m3/a10b-blocks-and-public-pages`, da `m3/a10a-path-and-bans`): i punti 1 e 2 — i
  quattro blocchi nelle due metà, con i due conteggi e la loro nota breve; `/training` e `/training/sessions/{id}` —. Nessuna migrazione.
  `training.upcomingSessions` mostra i training; gli esami li aggiunge A10c. **Fatta quando**: la pagina `/training` mostra a un
  visitatore i prossimi training senza VID né nomi, e con il login la pagina della sessione li mostra.
- **A10c — gli esami** (branch `m3/a10c-exams`, da quello di A10b, **solo dopo che #135 è unita**: `main` entra nel branch con un merge):
  il punto 4, con la forma di A3b per la riga (nota `le-righe-affidate-a-chi-scrive` §3.6: `[AlsoWrittenWith(ManageExams, AlsoOnCreation =
  true, AlsoOnDeletion = true)]`, `IHasAssignee` con l'esaminatore, `ManageExams` segnato `OnlyForAssignee` — e `DeniedToStakeholder` se
  l'esame dice il suo candidato —, `MapCrud` senza `DeletePolicy`); come un TA vede quali esami sono i suoi e come HQ, TC e TAC scelgono
  l'esaminatore di un esame che inseriscono per un altro (§3.6 lo lascia ad A10); la voce `exam` del calendario; gli esami nel blocco e in
  `/training`. Una migrazione. **Fatta quando**: un TA inserisce un esame, e solo lui (con HQ, TC e TAC) lo cambia e lo toglie; la pagina
  `/training` lo mostra a un visitatore senza VID né nomi. Se #135 non è unita quando A10b finisce, A10c aspetta, e la fase dopo A10b è
  un'altra: così è andata, e la fase dopo A10b è stata A11a.

  **#135 è unita il 27 settembre 2026 alle 20:24 UTC: A10c può partire.** Il branch `m3/a10c-exams` l'ha preparato la sessione di A11a da
  quello di A10b (67ab179), e `main` ci entra con un merge: da A10b, che l'ha preso il 28 settembre con il merge verso l'alto della coda
  chiesto dal revisore ([commento sulla #144][m144]; sotto, «Com'è andata (A10b)»), o da solo. **Dal revisore, il 28 settembre**
  ([commento sulla #146][m146]), tre cose che la regola di A3b chiede alla riga di un esame, e che A10c scrive e prova:
  1. **L'area dell'entità, dichiarata**: `[PermissionArea("Training")]` su `Exam`. Su una riga non affidata a chi scrive l'handler ripiega
     sull'`Edit` dell'area del permesso (`PermissionCatalog.EditOf`: `Training.ManageExams` → `Training.Edit`), il guardiano sull'`Edit`
     dell'area dell'entità (`[PermissionArea]`, altrimenti il nome del `DbSet`: `Exams.Edit`, che nessuno ha). Senza l'attributo, TC e TAC
     che cambiano l'esame di un TA avrebbero «sì» dall'endpoint e un `ForbiddenDomainException` dal guardiano. Con **un test della spina
     dorsale: TC e TAC, che hanno `Edit` e non sono gli esaminatori, cambiano l'esame dall'endpoint**.
  2. **Dire e provare che cosa fa DELETE**: un esame lo tolgono **HQ, TC, TAC e il TA a cui è assegnato, nessun altro** ([risposta 4 di
     Carmine sulla #131][c131]). Quindi `AlsoOnDeletion = true` sull'alternativa e `MapCrud` senza `DeletePolicy`, come la nota di A3b §3.6,
     e i test di chi lo toglie e di chi no. Il punto 2 del revisore è scritto per A7, dove `Training.Conduct` non ha `AlsoOnDeletion` e la
     risposta è `DeletePolicy = Training.Edit` (o `AllowDelete = false`): sull'esame il TA non toglierebbe più il suo, contro la risposta di
     Carmine.
  3. **Chi ha interesse** (nota di A3b §3.6): se l'esame nomina il suo candidato (`IHasStakeholder`), `ManageExams` è anche
     `DeniedToStakeholder`, altrimenti un candidato che è anche l'esaminatore avrebbe «sì» dall'handler e «no» dal guardiano.

  Il revisore proporrà una piccola PR del nucleo perché il controllo all'avvio (`PermissionCatalog.VerifyAlternatives`) rifiuti il punto 1 e
  altre due dichiarazioni silenziose: se è unita prima di A10c, un esame dichiarato male non fa partire l'hub. ⚠️ A10c toglie anche la copia
  `src/IvaoHub.Modules.Training/Refusals.cs` (la nota di #152; `HANDOFF-M3.md`, A10b).

[a1-135]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/135#issuecomment-5844250425

**Com'è andata (A10a)** (27 settembre 2026, branch `m3/a10a-path-and-bans`, PR #151, in coda dopo #150):

- **Classificata prima del codice** (`CLAUDE.md` §5): codice del modulo (caso a) dentro meccanismi che ci sono, usati così come sono
  (caso b) — `MapCrud` per la lista e il form dei ban, come i ban dei tour (`ReadOnlyRows` per un ban già dato, `AllowDelete = false`,
  `BeforeSave` per il ban che vale già, `AfterSave` per la mail); l'unico handler chiesto sulla riga con il «no» all'interessato
  (`Training.Ban` è `DeniedToStakeholder`, anche per il superadmin); il servizio notifiche con un tipo nuovo del modulo; la funzione unica
  della risposta dello staff (`StaffTrainings.PageAsync` con `ReservedFields.For`, nota `le-note-riservate-e-il-trainee`), di cui il
  percorso è il secondo lettore; la pagina del pilota dei tour come modello (una pagina dedicata del modulo e il suo endpoint scritto a
  mano, come `PilotPageDto` in M2 §8.7); `DataList`, `SchemaForm`, `ConfirmDialog`, `Notice`, e l'`Accordion` di Atmosphere, già usato dal
  blocco `accordion` —. **Nessun file del nucleo**, nessuna nota nuova, nessuna domanda a Carmine, **nessuna migrazione** (`trn_bans` è
  intera da A6a).
- **Fatto**, come il perimetro di A10a qui sopra:
  1. **Il percorso del trainee** (`Staff/TraineePaths.cs`): `GET /api/training/trainees/{vid}` con `Training.View` — chi fa training legge
     ogni training (R.1) —: chi è; **dove si trova su ogni percorso**, la stessa risposta della sua pagina (`MyTrainingPathDto`, dalle stesse
     regole: `TrainingRequests.PathsOfAsync` calcola i percorsi per un VID qualunque, e `MineAsync` la usa per chi è entrato); **tutti i
     suoi training**, dal più nuovo, ognuno come la pagina dello staff di quel training (`StaffTrainings.PageAsync`: un trainer che legge il
     proprio percorso non vi trova i campi riservati), senza quelli che l'unico handler non gli lascia leggere; **i suoi ban**; e
     **`canBan`**, la risposta dell'handler sul ban che scriverebbe (mai su sé stesso). 404 per un VID di cui l'hub non sa niente: nessun
     utente, nessun training, nessun ban.
  2. **I ban** (`Bans/TrainingBans.cs`, `Bans/BanEndpoints.cs`): la lista e il form generati in `/api/training/bans`, letti con
     `Training.View` e scritti con `Training.Ban`; `filter[vid]`, `?q=` sul motivo e sul VID, dal più nuovo. Un ban si dà con un motivo e,
     se si vuole, una fine ancora da venire (`training:errors.banEndsInThePast`); **non si cambia e non si elimina**; **uno nuovo su un membro
     che ne ha già uno in vigore è rifiutato** (`training:errors.banAlreadyHolds`, sul campo `vid`). **«Togli ban»** è un verbo suo,
     `POST /api/training/bans/{id}/lift` con la `rowVersion` (409 se vecchia): chi e quando, solo su un ban in vigore
     (`training:errors.banNotHolding`), con `Training.Ban` sulla riga. Un ban vale per i due percorsi, e i training già aperti vanno avanti:
     la richiesta lo legge da A6a.
  3. **La mail `banned`** (`TrainingNotifications.Banned`, design §5.2): il motivo, fino a quando — o finché qualcuno non lo toglie —, la
     pagina dei training del membro; la voce del profilo per spegnerla. `TrainingMail` manda ora anche una mail che non parla di un training.
  4. **Le pagine** (`screens/trainees.tsx`, `screens/bans.tsx`, le funzioni pure in `screens/path.ts`): **`/staff/training/trainees`**, il
     VID, come la pagina dei piloti dei tour; **`/staff/training/trainees/$id`**, il percorso — per percorso il rating, le ore, che cosa può
     chiedere o la prima regola che rifiuta, detta dello staff («Ha un ban…», «Aspetta…», «Ha un training aperto…» con il link), il mock
     exam, **«Pronto per l'esame»** (la lettura di A6b, `readyForExam`); i ban con lo stato, da quando e fino a quando, il motivo, chi l'ha
     dato e chi l'ha tolto, e **«Togli il ban»** chiesto prima; i training per percorso e rating, ognuno una riga che si apre sul trainer, la
     sessione, il report pubblicato con chi l'ha pubblicato, le sessioni passate e il link alla sua pagina; **«Banna»** a chi il server dice
     (`canBan`); l'avviso di `reservedLeftOut` a un trainer che legge il proprio percorso —; **`/staff/training/bans`**, la lista generata (il
     membro, lo stato, dal, fino al, il motivo, chi l'ha dato), dal più nuovo, con «Percorso» e «Togli il ban»; **`/staff/training/bans/new`**,
     il form generato, con il membro già scritto quando lo apre il percorso (`?vid=`) e che torna lì. Due voci nella barra dello staff,
     «Trainee» e «Ban», con `Training.View`. **Nella pagina di un training il nome del trainee porta al suo percorso** (§4.2, «il percorso del
     trainee a fianco»).
  5. **I test**: unità `TrainingBanRulesTests` (2), integrazione `TrainingTraineeTests` (5, VID 790052–790059: il «fatta quando» attraverso
     l'API, la fine già passata e il ban finito, nessuno banna sé stesso e chi legge soltanto non banna, il percorso, e **il test della nota
     allargato al percorso**, con l'elenco dei campi tolti), Vitest `screens/path.test.ts` (6), lo smoke `web/e2e/training-trainee.spec.ts`
     (5), il giro sul banco `web/e2e/full/training-the-trainee.spec.ts` (il «fatta quando» di A10a, sotto).
- **Scostamenti e precisazioni, piccoli**:
  1. **Un ban non si cambia**: il design (§2.9) dice «un motivo e, se si vuole, una scadenza» e «Togli ban registra chi e quando», niente
     sul cambiarlo. Per cambiarlo lo si toglie e se ne dà un altro: la mail dice sempre il ban che vale, e la storia tiene ogni ban com'era.
     Un ban già dato è in sola lettura per il motore (`ReadOnlyRows`: il `PUT` è un 403, il superadmin compreso).
  2. **Un ban nuovo non si somma a uno in vigore** (`banAlreadyHolds`): due ban insieme direbbero due fini al membro. E **la fine è ancora da
     venire** (`banEndsInThePast`): un ban già finito non varrebbe mai.
  3. **Chi banna ha anche `Training.Edit`**, come chi scrive le voci della scheda (A5): il guardiano chiede `Edit` a ogni riga dello staff
     (§3.1), e nella divisione `Ban` ed `Edit` vanno insieme (TC, TAC; HQ e il web per il nucleo). Nessun `[AlsoWrittenWith]` sul ban.
  4. **I ban si leggono con `Training.View`**, trainer compresi: il percorso li mostra, e chi allena un trainee deve sapere perché non chiede
     più. Le voci del menu sono di `Training.View`, i pulsanti di `Training.Ban`.
  5. **«Pronto per l'esame» sul percorso è la lettura di A6b** (`readyForExam` in `screens/trainee.ts`, il cui tipo ora accetta anche le
     pagine dello staff). Una prima stesura lo calcolava anche sul server, in un campo di `MyTrainingPathDto`, che l'avrebbe scritto due
     volte: tolto prima del commit.
  6. **I training del percorso si aprono uno alla volta** (`Accordion`): con tutti i report aperti il percorso di un trainee con molti
     training sarebbe lungo pagine.
  7. **Il percorso esiste anche per un membro senza training** (che l'hub conosce, o che ha un ban): lo staff lo apre per bannarlo prima di
     una richiesta. 404 solo per un VID di cui l'hub non sa niente.
  8. **«Il percorso a fianco» del training** (§4.2) è un link dal nome del trainee nella pagina del training: la pagina è già lunga, e il
     percorso ha la sua.
  9. **Togliere un ban non manda mail**: §5.2 non ne elenca una.
- **Codice di fasi sotto toccato, e perché** (nessun test di un'altra fase è cambiato):
  1. `Requests/TrainingRequests.cs` (A6a): i percorsi si calcolano per un VID qualunque (`PathsOfAsync`), e la pagina del trainee e la
     richiesta passano il VID di chi è entrato: la risposta è la stessa.
  2. `TrainingMail.cs` (A7): una mail che non parla di un training (il ban); quelle di un training passano di lì come prima.
  3. `screens/staff.tsx` (A7): il nome del trainee è un link al suo percorso. `screens/trainee.ts` (A6b): `REFUSALS` ha anche `nothingToAsk`
     e `noPosition`, e `readyForExam` accetta le pagine dello staff (solo il tipo).
- **Trovato, e scritto per chi viene dopo** (anche in `HANDOFF-M3.md`):
  1. ⚠️ **La risposta 2 di Carmine sulla #135 non è in nessuna fase della coda.** Carmine ha deciso ([il suo commento][a1-135]) che anche il
     trainer conduce con la regola di A3b — il training dichiara il suo trainer con `IHasAssignee`, `Training.Conduct` è segnato
     `OnlyForAssignee` ed è tenuto per posizione, anche da TA1–9 e T01–T99 —, senza il grant con scope a ogni assegnazione né il suo job
     notturno, e che «lo registra A7, nella sua nota, in `08` e in `07`». A7 (#146) è nata prima che A3b fosse unita, e usa il grant con
     scope del design n.1. Quando #135 sarà unita, serve una fase del modulo che porti la risposta 2: detto al revisore.
  2. **Per A10b**: il blocco `training.myTraining` ha già la sua risposta per percorso (`MyTrainingPathDto` di `GET /api/training/mine`), e
     «pronto per l'esame» è `readyForExam`; le code di `training.trainerQueue` e `training.approvalQueue` sono le viste di `StaffQueue` (A7)
     e `TrainingDates.Unanswered` (A8a). La voce del calendario porta ancora a `/training/sessions/{id}`, che è di A10b.
  3. **Per A10c**: la forma della riga di un esame è nella nota di A3b §3.6 (sul branch di #135), con il rilievo del revisore: un TA deve
     vedere quali esami sono i suoi.
  4. **Il router scrive il `?vid=` di un link fra virgolette nell'`href`** (`…/bans/new?vid=%22999002%22`) e lo rilegge giusto: la pagina
     del form ha il VID. Le spec guardano l'indirizzo dopo il clic, non l'`href`.
  5. **`ConfirmDialog` non ha una dimensione per il suo pulsante** (nucleo, non toccato): «Togli il ban» in una riga della lista è grande
     quanto un pulsante normale, accanto ai `ghost` piccoli.
  6. I VID **790052–790059** sono di A10a; il prossimo libero è **790060**.
- **La coda**: A10a è nata in coda dopo #150 (A9b, in bozza in coda dopo #149, dopo #148, dopo #147, dopo #146, dopo #144, dopo #143): la PR
  è in bozza con `(after #150)` e `Queued after #150.`. Quando #150 sarà unita, il passo della coda (`CONTRIBUTING.md`, «Phases in a
  queue»): `main` nel branch con un merge, build e tutti i test di nuovo, via la coda, e la PR pronta con la CI verde.
- **Verificato, in locale** (27 settembre 2026, sul branch da `m3/a9b-after-the-session-pages`, cbbfc1c): `dotnet build` senza avvisi;
  unità **790/790** (le 788 di A9b e le 2 nuove; `TrainingArchitectureTests` legge anche il C# e il TypeScript nuovi del modulo, ed è verde);
  **integrazione intera senza filtro** **348/348** (le 343 e le 5 nuove; la classe nuova da sola 5/5 al primo giro); `pnpm lint`,
  `typecheck`, `format:check`, `i18n:check` verdi, e lo script che confronta le chiavi letterali `training:` del modulo con i file di lingua;
  `pnpm test` **546** in **68** file (le 540 in 67 di A9b e le 6 nuove); `pnpm e2e` **120** (le 115 e le 5 nuove); **`pnpm e2e:full` 46** su
  un **banco nuovo** di questo worktree (127.0.0.1:5094, `ivaohub_e2e_a10`) al primo giro intero, e di nuovo **46/46** su un banco ricreato
  con una pubblicazione nuova, dopo la correzione trovata a mano (sotto). **Lo smoke nuovo cade sul codice di A9b**: con il manifest del
  modulo rimesso da `m3/a9b-after-the-session-pages` (senza le rotte nuove), `training-trainee.spec.ts` 4 cadute su 5 — la quinta, «un VID
  sconosciuto non si trova», vale anche prima —; rimesso com'è sul branch (e toccato), 5/5. `pnpm gen:api` e `pnpm i18n:sync` nel commit
  che li porta; `dotnet format --verify-no-changes` sui file C# toccati, test compresi; le regole di `core-guard` rifatte in PowerShell
  sull'intervallo della fase e sul diff verso `main`: nessun file del maintainer, nessuno del nucleo. **A mano**, sul banco di anteprima
  (127.0.0.1:5090, `ivaohub_preview`; la sessione di A9b ha spento il suo su richiesta), in italiano: come staff, `/staff/training/trainees`
  con il VID del trainee del banco, e il suo percorso — ATC e pilota con «Ha un training aperto su questo percorso. Aprilo», «Nessun ban.»,
  i training per percorso e rating —; il training pilota #7 aperto nella sua riga (il trainer, la sessione in UTC e nell'ora della
  divisione, «Pubblicato da…», le voci con «Per il trainee…» e «Per lo staff…», «Da migliorare», N/A, i due commenti, le caselle, le sessioni
  rischedulata con gli appunti ed eseguita, «Apri il training»); **«Banna» dal percorso**, il form con il VID già scritto, un motivo e una
  fine (4 ottobre, 18:00 UTC; sotto, 20:00 Europe/Rome), dato: di nuovo sul percorso il ban «In vigore» con da quando, fino a quando, il
  motivo e chi l'ha dato, e i due percorsi «Ha un ban dal training fino al 4 ott 2026, 18:00 UTC.»; come trainee, `/training/request?kind=Atc`
  e `/training/mine` dicono il ban e fino a quando, e il training ATC aperto va avanti («Scegli la data»); **la mail «[Training] Ban dal
  training»** in Mailpit, con il motivo, la fine in UTC e «I training che hai già aperti vanno avanti come sono.»; come staff, la lista dei
  ban (il membro, «In vigore», dal e fino al in UTC e nell'ora della divisione, il motivo, chi l'ha dato, «Percorso» e «Togli il ban»),
  **«Togli il ban»** chiesto prima e confermato in blu: «Tolto», e il pulsante sparito; il percorso in inglese e a tema scuro, con il ban
  «Lifted», chi l'ha dato e chi l'ha tolto; nella pagina di #7 il nome del trainee porta al suo percorso; come trainer, niente «Banna»,
  «Nuovo ban» né «Togli il ban». **Trovato a mano e corretto**: il nome accessibile della riga di un training sul percorso incollava i pezzi
  («Completatochiesto il 27 set 2026»): la riga ora li dice separati (`aria-label`).
- **Non verificato**: la CI (la dirà la PR). **Un trainer che legge il proprio percorso sul server vero**: il banco non ha un trainer che sia
  anche trainee; il test d'integrazione prova la regola sul percorso, e lo smoke disegna l'avviso. **Un 409 di «Togli il ban» dalle pagine**:
  provato dal test d'integrazione con una versione vecchia, non dalle pagine. **Due ban dati nello stesso momento allo stesso membro**:
  `banAlreadyHolds` legge gli altri ban prima del salvataggio, e due scritture insieme possono passare tutte e due (nessuna chiave del
  database lo impedisce); il percorso e la richiesta sanno leggere più ban (A6a). **Le pagine dello staff larghe 375 px**: hanno il difetto
  noto del nucleo a quella larghezza (A7). **I test nuovi del server sul codice di A9b**: non compilano (gli endpoint, i tipi e la mail
  nascono qui). **Il test della nota sul percorso su una copia indebolita del codice** (la chiamata a `ReservedFields.For` tolta): non
  tentato, perché la modalità di permessi l'ha rifiutato ad A9a; il test è stato letto contro il codice.
- **La revisione di #151** (29 settembre 2026, [i rilievi](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/151#issuecomment-5891399650);
  la sessione di A10a non c'era più, le correzioni le ha fatte la sessione che coordina la coda): approvabile. Le due cose da correggere
  erano documenti:
  1. **`HANDOFF-M3.md` non era più vero**: la risposta 2 di Carmine sulla #135 oggi la porta A7b (#181), e l'intestazione diceva lo stato
     del 27 settembre. Corretti tutti e due, e il «Trovato» 1 del corpo della PR.
  2. **La persona cancellata non è ancora trattata** (design §6.1): è di A12, e l'elenco dei punti — il link al percorso in
     `screens/staff.tsx`, `path.ts` che ignora il segno, `memberLabel` che scrive il numero, la rotta `{vid:int}` che accetta i negativi —
     è in `HANDOFF-M3.md`, nel paragrafo di A10a, perché A12b non ne salti nessuno.

  I nit restano scritti lì: la corsa di due ban nello stesso istante (dichiarata), la ricerca per VID senza un test, la copia di
  `Refusals` che A10c toglie.
- **Dopo le risposte di Carmine su #149 e #150** (29 settembre 2026, sul branch temporaneo `fix3/a10a`, con A9a e A9b nuove unite):
  ⚠️ **un test di A10a toccato, e perché**: l'aiuto `TrainingTraineeTests.ReportedWithNotesAsync` datava la seconda sessione a mano dieci
  minuti fa, che ora si rifiuta ([la risposta](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/149#issuecomment-5891427158): nessuno
  data un training nel passato). La data un'ora avanti con lo stesso passo, e l'aiuto nuovo `StartedAMomentAgoAsync` sposta
  `scheduled_start_utc` a dieci minuti fa come fa l'installazione, come in A9a; la versione si rilegge dalla pagina. Le asserzioni non
  cambiano. Le spec del banco di A10a non datano né chiudono niente.
- **Il passo della coda dopo #150** (29 settembre 2026: #150 unita alle 17:23 UTC; l'ha fatto la sessione che coordina la coda): `main`
  nel branch con un merge (b03691e) — nessun codice nuovo, `main` portava solo i documenti del passo della coda di A9b; un conflitto,
  l'intestazione di `HANDOFF-M3.md`, che tiene quella di A10a riscritta —, via `(after #150)` dal titolo e `Queued after #150.` dal corpo,
  la PR pronta a CI verde. I due punti della revisione (l'handoff sulla seconda risposta di #135 con il «Trovato» 1 del corpo, e l'elenco
  per A12b dei posti che non trattano ancora una persona cancellata) erano già corretti, e ci sono. **Verificato di nuovo, in locale**
  (d28cc98), tutto al primo giro: `dotnet build` senza avvisi; unità **860/860**; integrazione intera **393/393**; `pnpm gen:api` e
  `pnpm i18n:sync` senza differenze; `lint`, `typecheck`, `format:check`, `i18n:check` verdi; Vitest **567/567** in 72 file; smoke
  **136/136**; **`e2e:full` 46/46** su un banco nuovo (127.0.0.1:5106).

**Com'è andata (A10b)** (27 settembre 2026, branch `m3/a10b-blocks-and-public-pages`, PR #153, in coda dopo #151):

- **Classificata prima del codice** (`CLAUDE.md` §5): codice del modulo (caso a) dentro meccanismi che ci sono, usati così come sono
  (caso b) — i blocchi Data nelle due metà (`IDataBlockProvider` con `BlockDescriptor` `AlwaysLive`, `BlockRegistration` nel manifest),
  come i cinque dei tour; l'unico handler chiesto sulla riga per i blocchi dello staff (`Training.Conduct`, `Approve`, `Assign`, mai sulla
  propria riga); le code che le fasi sotto hanno già scritto — le viste di `StaffQueue` (A7), `TrainingDates.Unanswered` (A8a),
  `TrainingSessions.IsRecordable` (A9a), la risposta di `TrainingRequests.MineAsync` (A6a, A10a); le pagine pubbliche di un modulo come
  rotte `public` del manifest, sotto il segmento che A4 riserva, come `/tours` (M2, T10); `CrudSource.BackOffice` per leggere la sessione
  pubblica di un training, che il filtro dei membri nasconde a un visitatore, e poi solo ciò che la voce del calendario già mostra (come
  `MyTours.ToursCompletedAsync` legge i tour nascosti); `EmptyState`, `NotFound`, `Notice`, `RatingBadge`, `loginHref` del nucleo e i pezzi
  del modulo (`WhenText`, `StateBadge`) —. **Nessuna migrazione**, nessuna domanda a Carmine. **File del nucleo**: solo i due conteggi dei
  blocchi, come Carmine ha deciso ([commento sulla PR #125][c125], risposta 2), con la nota breve che `core-guard` chiede,
  `decisions/2026-09-27-i-conteggi-dei-blocchi-del-training.md`.
- **Fatto**, come il perimetro di A10b qui sopra:
  1. **Una regola sola per la sessione pubblica**: `Training.SessionIsPublic` — datato, o completato con la sessione del suo report —, un'
     espressione che leggono il database e la proiezione nel calendario (`Training.Project` la usa al posto della condizione che aveva
     dentro). Un test di unità prova che la pagina della sessione e la voce del calendario non si contraddicono in nessuno stato.
  2. **Le sessioni del sito** (`Public/PublicSessions.cs`): `GET /api/training/sessions` — le sessioni ancora da tenere, le più vicine
     prima, al massimo 50 — e `GET /api/training/sessions/{id}` — la sessione di un training che ne ha una pubblica, anche tenuta, 404
     altrimenti —, anonimi. `PublicSessionDto`: percorso, rating (la sigla), postazione, inizio, `held` (il giorno è finito nel fuso della
     divisione, o il report è pubblicato); **trainee e trainer, per VID e nome, solo a chi ha fatto il login** — per un visitatore i nomi
     non si chiedono nemmeno (nota `il-training-in-pubblico`).
  3. **I quattro blocchi** (`Blocks/`, le metà in `web/src/modules/training/blocks/`), tutti `AlwaysLive` perché rispondono per chi guarda
     (nota `frozen-e-visibilita`):
     - **`training.upcomingSessions`**: la lista di `/training`, dallo stesso servizio e disegnata dallo stesso componente
       (`UpcomingSessionList`); una proprietà, `limit`; `signedIn` accanto, per l'invito ad accedere.
     - **`training.myTraining`**: la risposta di `GET /api/training/mine` con `signedIn: true`. Per percorso il training aperto e che cosa
       aspetta — «Il trainer ha proposto N date: scegli la tua» con «Scegli la data», la sessione in UTC e a Roma, «Eseguito» e il report
       che il trainer scriverà —, che cosa si può chiedere o la prima regola che rifiuta (l'attesa, un ban anche accanto a un training
       aperto), «pronto per l'esame», l'ultimo report; i pezzi di `/training/mine` (sotto).
     - **`training.trainerQueue`**: i training di cui il lettore è il trainer e che l'handler gli lascia condurre, in tre parti
       (`Staff/TrainerQueue.cs`, funzioni pure): **in evidenza le scelte in ritardo** dopo `responseReminderDays` — i giorni dall'ultima data
       proposta, `TrainingDates.Unanswered` —, le date da proporre, i report da scrivere (`IsRecordable`).
     - **`training.approvalQueue`**: le viste «da approvare» e «da assegnare» di `StaffQueue`, dalla richiesta più vecchia, filtrate
       dall'handler (`Approve`, `Assign`, mai sulla propria riga): quante e le 10 più vecchie, e il titolo di ognuna un link alla lista
       filtrata.
     A un visitatore i tre personali rispondono soltanto `signedIn: false`. Nessuno entra da solo in `/me` o `/staff`: ce li mette chi
     compone la dashboard, come `myTours` in M2.
  4. **Le pagine** (`screens/public.tsx`, le funzioni pure in `screens/site.ts`): **`/training`** — «Richiedi training», «I miei training»
     a chi ha fatto il login, «I prossimi training» con la loro pagina, e a un visitatore «Accedi per vedere chi fa ogni training» —;
     **`/training/sessions/$id`** — lo stato (programmato o eseguito), il percorso, «Training ADC · LIRF_TWR», postazione, rating, data e
     ora in UTC e nel fuso della divisione, con il login trainee e trainer, senza «Accedi per vedere il trainee e il trainer» —. L'indirizzo
     delle voci del calendario (A8a) ora porta a una pagina.
  5. **I pezzi in comune**: `AskOrRefusal` e `ReadyForExamLine` escono da `PathCard` (`screens/mine.tsx`) in `screens/parts.tsx`, e
     `lastReported` da `readyForExam` (`screens/trainee.ts`): `/training/mine` e il blocco del trainee li disegnano con gli stessi pezzi.
  6. **I due conteggi**: `uiKit.test.ts` da 38 a 42 blocchi, `DataBlockEndToEndTests` da 13 a 17 blocchi Data — il numero e la frase del
     commento accanto che dice da che cosa viene —, con la nota breve.
  7. **I test**: unità `TrainingBlocksRulesTests` (5); integrazione `TrainingBlocksTests` (5, VID 790060–790067: il «fatta quando»
     attraverso l'API, i blocchi personali a un visitatore, il blocco del trainee uguale alla sua pagina, la coda del trainer con la
     soglia, la coda dello staff secondo l'handler); Vitest `blocks/reading.test.ts` (4) e `screens/site.test.ts` (2); lo smoke
     `web/e2e/training-public.spec.ts` (8: le due pagine con e senza login, una sessione che non c'è, i blocchi su una pagina e sulle due
     dashboard); il giro sul banco `web/e2e/full/training-upcoming.spec.ts` (sotto).
- **Scostamenti e precisazioni, piccoli**:
  1. **«I prossimi training» sono le sessioni `Scheduled` dal giorno di oggi nel fuso della divisione**: una sessione di oggi, cominciata o
     no, resta finché non si mostra «Eseguita» (§1.2), come la vista «In corso» di A7. Un training completato non è più «prossimo», ma la
     sua pagina resta: la voce del calendario della sessione tenuta c'è ancora (A9a).
  2. **`training.upcomingSessions` ha una proprietà, `limit`** (10 se non scritta, 0 tutte fino a 50): il design non ne dice, e una pagina
     del CMS che spiega il percorso ne mostra poche. Nessun filtro per percorso: non chiesto.
  3. **Con il login anche il blocco e `/training` dicono chi** (trainee e trainer, per nome e VID), come la pagina della sessione: la nota
     dice «senza nomi per chi non ha fatto il login», e chi ha fatto il login li legge già lì.
  4. **La coda del trainer è di chi è il trainer** (`TrainerVid`), non di chi può condurre: TC e TAC conducono ogni training per posizione, e
     la loro tessera sarebbe la lista intera.
  5. **«In attesa di scelta»** sono i training con date ancora da venire che il trainee non ha scelto per più di `responseReminderDays`
     giorni dall'ultima proposta; con tutte le date passate il training torna fra «date da proporre»; prima della soglia non c'è niente da
     muovere, e non si mostra.
  6. **La coda dello staff mostra quante sono e le 10 più vecchie**, leggendone al massimo 500 prima dei permessi, come la coda dei
     validatori dei tour (T13b).
  7. **La pagina della sessione dice solo postazione, rating, data e ora**, e se è eseguita: niente mock exam, niente del report (nota).
  8. **`training.myTraining` è la risposta intera di `/api/training/mine`**, compresi gli elenchi delle postazioni offerte: qualche KB in
     più su `/me`, per non avere una seconda risposta da tenere in pari.
  9. **I blocchi non hanno un titolo loro**, come quelli dei tour: su una pagina il titolo è della sezione; le parti delle due code hanno il
     loro.
  10. **Nessuna voce di menu**: `/training` entra nel menu pubblico quando il web team la aggiunge (il menu è del CMS).
- **Codice di fasi sotto toccato, e perché** (nessun test di un'altra fase è cambiato; i due conteggi non sono del training):
  1. `Training.cs` (A6a, A8a, A9a): `SessionIsPublic`, che `Project` ora legge; il comportamento non cambia, e i test di A8a e A9a sulla
     proiezione lo provano.
  2. `screens/mine.tsx`, `screens/parts.tsx` (A6b, A9b), `screens/trainee.ts` (A6b): i pezzi in comune (sopra); `/training/mine` è identica,
     e lo smoke di A6b lo prova. `api.ts`: le due letture nuove.
- **Il giro sul banco** (`training-upcoming.spec.ts`, un nome che viene dopo tutti gli altri giri del training): il trainee chiede un
  training ATC attraverso l'API — la richiesta è nella coda dello staff —, lo staff lo accetta e lo assegna al trainer del banco — che,
  rientrato, lo trova fra le date da proporre —, e lo data a mano fra tre giorni; il blocco del trainee lo nomina. Un **visitatore** legge
  `/training` — la sessione per rating e postazione, **nessun nome né VID** nella pagina, «Richiedi training» — e il blocco dal server vero,
  senza persone; **dal calendario segue la voce della sessione alla sua pagina**, che dice postazione, rating, data e ora e offre
  l'accesso; **con il login** la pagina della sessione e `/training` dicono «Bench Pilot (999002)» e «Bench Trainer (999004)». Chiuso dallo
  staff, il training esce da `/training` e la sua pagina non si trova. All'inizio i tre blocchi personali dicono a un visitatore soltanto
  `signedIn: false`. **Nessun test di un'altra fase è cambiato**; il training resta chiuso, senza attesa.
- **Trovato, e scritto per chi viene dopo** (anche in `HANDOFF-M3.md`):
  1. **Trovato a mano e corretto**: il blocco del trainer diceva «i tuoi training aspettano il loro giorno» anche a chi non allena nessun
     training (il web master): ora «Nessun training da muovere, per ora.».
  2. **Per A10c**: gli esami entrano in `PublicSessions` (la lista e il blocco) e in `/training`; la voce `exam` del calendario ha bisogno di
     un indirizzo — la pagina di un esame o `/training` —, da decidere lì. La regola pubblica di un esame, come quella di un training, va
     scritta una volta e letta dalla proiezione e dalle pagine.
  3. **Le pagine pubbliche non hanno i metadati SEO** (`PageMetadata`), come `/tours`; la sitemap non conosce le pagine dei moduli (nucleo).
  4. Nel pannello del browser **lo screenshot della galleria `/staff/admin/ui-kit` non arriva** (la pagina non smette di disegnarsi): i
     quattro blocchi lì si sono letti dal testo, e disegnano gli esempi.
  5. I VID **790060–790067** sono di A10b; il prossimo libero è **790068**.
- **Il passo della coda di #144, fatto da questa sessione**: **#143 (A6a) è stata unita il 27 settembre alle 14:34**, mentre A10b lavorava, e
  la sessione di A6b non c'era più. Su un branch temporaneo da `origin/m3/a6b-request-pages`: `main` dentro con un merge (**f5e3cd6**: porta solo
  `TourTests.cs` di #142), poi build e **tutte** le suite sul merge, una alla volta — unità **767/767**, integrazione intera **322/322**, `pnpm
  lint`, `typecheck`, `format:check`, `i18n:check` verdi, `pnpm gen:api` senza differenze, `pnpm test` **509**, `pnpm e2e` **96**, `pnpm e2e:full`
  **42/42** su un banco nuovo al primo giro —; il push su `m3/a6b-request-pages`, e da #144 via `(after #143)` e `Queued after #143.`, con il
  passo della coda scritto nel suo corpo; pronta a CI verde. Poi **il merge verso l'alto, in ordine**, ognuno su un branch temporaneo da
  `origin` e spinto con `git push origin HEAD:<branch>`: A7 (69781af), A8a (c8cf468), A8b (ce39ecf), A9a (29a3e54), A9b (18d175f), A10a
  (2d4b0d6), e A10b; ogni merge pulito (solo `TourTests.cs`), con la build senza avvisi e la suite di unità di quel branch (773, 782, 782,
  788, 788, 790); l'integrazione e i giri di quei branch sono della loro CI, che parte al push. Le sessioni di A8b, A9a, A9b e A10a, vive,
  sono state avvisate prima e hanno aspettato. **Nel frattempo è arrivata anche #152** (alle 15:28, `Refusals` nel nucleo, nota
  `2026-09-27-i-rifiuti-di-un-form-nel-nucleo`): non tocca il training, e la coda non l'ha ancora; se il revisore vuole #144 in pari anche
  con lei, lo chiede sulla PR. ⚠️ **La nota chiede che la copia `src/IvaoHub.Modules.Training/Refusals.cs` la tolga la prima fase del
  collaboratore aperta dopo #152**: A10b era già aperta, e A11a è del nucleo (non si mescola con il modulo, `CLAUDE.md` §0 regola 6), quindi
  tocca alla prossima fase del modulo, A10c.
- **La coda**: A10b è nata in coda dopo #151 (A10a, in bozza in coda dopo #150, dopo #149, dopo #148, dopo #147, dopo #146, dopo #144; #143 è
  unita): la PR è in bozza con `(after #151)` e `Queued after #151.`. Quando #151 sarà unita, il passo della coda (`CONTRIBUTING.md`, «Phases
  in a queue»): `main` nel branch con un merge, build e tutti i test di nuovo, via la coda, e la PR pronta con la CI verde. **A10c aspetta
  #135** (A3b), ancora in bozza. **La fase dopo è A11a** (i capi FIR nel nucleo), da `main` e fuori dalla coda come A3b e A6c: la nota e la
  domanda a Carmine subito; il codice dopo la risposta e dopo l'unione di #135, perché tocca lo stesso handler e lo stesso guardiano e
  migrerebbe lo stesso contesto di prova, e una base del nucleo ancora da unire non si usa (sopra, la divisione di A10). A11b, A12a e A12b
  vengono dopo A10c.
- **Verificato, in locale** (27 settembre 2026, sul branch da `m3/a10a-path-and-bans`, 36f7b7a): `dotnet build` senza avvisi; unità
  **795/795** (le 790 di A10a e le 5 nuove; `TrainingArchitectureTests` legge anche il C# e il TypeScript nuovi del modulo, ed è verde);
  **integrazione intera senza filtro** **353/353** (le 348 e le 5 nuove; la classe nuova da sola 5/5 al primo giro); `pnpm lint`,
  `typecheck`, `format:check`, `i18n:check` verdi, e lo script che confronta le chiavi letterali `training:` del modulo con i file di lingua;
  `pnpm test` **552** in **70** file (le 546 in 68 di A10a e le 6 nuove in 2); `pnpm e2e` **128** (le 120 e le 8 nuove); **`pnpm e2e:full`
  47** su un **banco nuovo** di questo worktree (127.0.0.1:5095, `ivaohub_e2e_a10b`) al primo giro intero, e di nuovo **47/47** su un banco
  ricreato con una pubblicazione nuova, dopo la correzione trovata a mano; **e di nuovo tutto dopo il merge che porta `main` dopo #143**
  (e025855, sopra): unità **795/795**, integrazione intera **353/353**, `pnpm lint`, `typecheck`, `format:check`, `i18n:check` verdi, `pnpm
  test` **552**, `pnpm e2e` **128**, `pnpm e2e:full` **47/47** su un banco ricreato. **Lo smoke nuovo cade sul codice di A10a**: con il manifest del
  modulo rimesso da `m3/a10a-path-and-bans` (senza le rotte pubbliche e senza i blocchi), `training-public.spec.ts` 7 cadute su 8 — l'ottava,
  «una sessione senza pagina non si trova», vale anche prima —; rimesso com'è sul branch (e toccato), 8/8. `pnpm gen:api` e `pnpm i18n:sync`
  nei commit che li portano; `dotnet format --verify-no-changes` sui file C# toccati, test compresi; le regole di `core-guard` rifatte in
  PowerShell sull'intervallo della fase e sul diff verso `main`: nessun file del maintainer, **nucleo 2** (i due conteggi) con la nota
  aggiunta, quindi passa. **A mano**, sul banco di anteprima (127.0.0.1:5090, `ivaohub_preview`; la sessione di A10a ha spento il suo su
  richiesta), con le dashboard `me` e `staff` composte con i quattro blocchi attraverso l'API del contenuto, come farebbe il web team: da
  visitatore `/training` (nessuna sessione in programma: «Nessun training in programma.») e la pagina della sessione #7 (pilota, eseguita:
  «Eseguito · Pilota», PP, data e ora, «Accedi per vedere il trainee e il trainer», nessun nome); come trainee `/me` — ATC con «Il trainer ha
  proposto 2 date: scegli la tua» e «Scegli la data», pilota con il mock exam eseguito, «il trainer scriverà il report», «Ultimo report… Leggi
  il report» —; dalla pagina di #6 **scelta la prima data** (29 settembre, 18:00 UTC); `/training` con il login («Trainee: Bench Pilot
  (999002) · Trainer: Bench Trainer (999004)», «I miei training») e da visitatore (nessun nome né VID nel testo della pagina, «Accedi per
  vedere chi fa ogni training»), la pagina di #6 da visitatore; in italiano e in inglese, a tema scuro e chiaro, larghe 375 px; come trainer
  `/staff` — «Report da scrivere» con il mock exam di Bench Pilot e il link al training, «Nessuna richiesta aspetta te.», e la sessione di #6
  nel calendario interno —; come web master `/staff` in inglese a tema scuro; la galleria con i quattro blocchi e i loro esempi (dal testo).
- **Non verificato**: la CI (la dirà la PR). **Le parti «in attesa» della coda del trainer e le due code dello staff piene sul server vero, a
  mano**: il banco di anteprima non ha né una scelta in ritardo né richieste in attesa; le provano il test d'integrazione (con la soglia) e lo
  smoke, e il giro sul banco legge le due code dal server vero con una richiesta e un'assegnazione. **I test nuovi del server sul codice di
  A10a**: non compilano (i provider, gli endpoint e i DTO nascono qui). **Un capo FIR nella coda dello staff**: è di A11b.
- **In pari con `main` dopo #135** (28 settembre 2026): **#135 (A3b) è stata unita il 27 settembre alle 20:24 UTC**, e il revisore ha chiesto
  alla coda un merge di `main` ([commento sulla #144][m144]). Dopo le correzioni di revisione di A6b, A7 e A8a, la sessione che coordina ha
  portato `main` (a 4d424f9: #152, #135 e le PR del maintainer da #154 a #172) nella coda fino ad A10a (9e82ad1), e **A10b l'ha preso con un
  merge** (5e349b4, mai un rebase): **nessun conflitto**, `08` e `HANDOFF-M3.md` compresi. `main` è andato avanti ancora durante il passo
  (#173 e #174, fino alle 17:17 UTC): non si insegue, e se il revisore vuole anche loro lo chiede sulla PR.
  - **Il punto 3 del revisore** («se il catalogo di A3b cambia qualcosa su cui le fasi contano»): **per A10b niente**. Nessun permesso del
    training è segnato `OnlyForAssignee` né ha `AlsoOnDeletion`, e `Training` dichiara già la sua area (`[PermissionArea]`), quindi
    `VerifyAlternatives` passa a ogni avvio — dei test d'integrazione e del banco — e sul training il ramo nuovo dell'handler non scatta mai.
    I blocchi dello staff chiedono all'unico handler `Approve`, `Assign` e `Conduct` sulla riga, come prima; le pagine pubbliche leggono
    soltanto. Quando A7b segnerà `Training.Conduct` con il trainer come assegnatario, `training.trainerQueue` resterà la stessa: elenca solo
    i training di cui il lettore è il trainer, cioè le righe affidate a lui. E `TrainingBlocksTests` dà `Conduct` al trainer su tutto il
    dipartimento, non con il grant sulla riga che A7b toglie: il test resta vero anche dopo.
  - **Dalle correzioni delle fasi sotto, niente da cambiare qui**: un training chiuso ora tiene la data della sua sessione (A8a), ma la regola
    pubblica (`SessionIsPublic`), «tenuta» (`StaffQueue.IsHeld`), «da registrare» (`IsRecordable`) e la coda del trainer leggono anche lo
    stato, e il blocco del trainee mostra la data solo a un training programmato: una sessione chiusa non è né in `/training` né nella sua
    pagina, come prima.
  - **Rifatto tutto, una suite alla volta**, sul merge: `dotnet build` senza avvisi; unità **846/846**; **integrazione intera senza filtro**
    **381/381**; `pnpm gen:api` e `pnpm i18n:sync` senza differenze; `pnpm lint`, `typecheck`, `format:check`, `i18n:check` (777 chiavi) verdi,
    e lo script delle chiavi `training:`; `pnpm test` **554** in **70** file; `pnpm e2e` **131/131**, con il lucchetto della porta 4173, al
    quarto giro — nei primi due un test del nucleo diverso (`calendar.spec.ts:98`, poi `smoke.spec.ts:52`) ha aspettato invano che la pagina
    si disegnasse, e il terzo si è chiuso con un crash del runner (0xC0000409) prima di cominciare; quei due spec, ripetuti cinque volte,
    passano 45/45 —; `pnpm e2e:full` **47/47** al primo giro su un banco nuovo (127.0.0.1:5095, `ivaohub_e2e_a10b` ricreato); `dotnet format
    --verify-no-changes` sui file C# della fase; le regole di `core-guard` in PowerShell, sull'intervallo della fase e sul diff verso `main`:
    nessun file del maintainer, nucleo 2 (i due conteggi) con la nota, quindi passa.
  - **A10c può partire** (sopra, sotto A10: i tre punti del revisore per la riga di un esame, e la copia di `Refusals.cs` da togliere).
- **La revisione di #153** (29 settembre 2026, [i rilievi](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/153#issuecomment-5891400128);
  la sessione di A10b non c'era più, il nit l'ha corretto la sessione che coordina la coda): approvabile.
  1. **Il limite di `training.upcomingSessions` non scritto** era 50 sul server (`PublicSessions.MaxItems`) e 10 nel browser (`.default(10)`),
     mentre questo piano dice «10 se non scritto»: un blocco salvato con `{}` (l'API, un seme) ne mostrava 50. Ora il server fa come i blocchi
     del nucleo, `?? DefaultLimit` con `DefaultLimit = 10`; lo zero resta «tutte, fino a 50», e lo schema zod ha `.max(50)`.
  2. **Scritto, non cambiato**: `TrainingBlocksTests` cerca la sessione a +3 h con `Assert.Single` in una lista di 50 al massimo, sul
     database condiviso. Diventerebbe instabile solo se altre classi lasciassero più di 49 training datati più vicini.
- **Il passo della coda dopo #151** (29 settembre 2026: #151 unita alle 19:00 UTC; l'ha fatto la sessione che coordina la coda): `main`
  nel branch con un merge (90f3cab) — nessun codice nuovo rispetto alla coda, `main` portava i documenti dei passi della coda di A9a, A9b e
  A10a; un conflitto, l'intestazione di `HANDOFF-M3.md`, che tiene quella di A10b riscritta —, via `(after #151)` dal titolo e
  `Queued after #151.` dal corpo, la PR pronta a CI verde. **Verificato di nuovo, in locale** (3679678), tutto al primo giro: `dotnet build`
  senza avvisi; unità **865/865**; integrazione intera **398/398**; `pnpm gen:api` e `pnpm i18n:sync` senza differenze; `lint`,
  `typecheck`, `format:check`, `i18n:check` verdi; Vitest **573/573** in 74 file; smoke **144/144**; **`e2e:full` 47/47** su un banco nuovo
  (127.0.0.1:5104).

**Com'è andata (A10c)** (28 settembre 2026, branch `m3/a10c-exams`, PR #178, in coda dopo #153):

- **Classificata prima del codice** (`CLAUDE.md` §5): codice del modulo (caso a) dentro meccanismi che ci sono, usati così come sono
  (caso b) — la regola delle righe affidate di A3b, com'è scritta per gli esami nella sua nota §3.6 (`IHasAssignee`, `OnlyForAssignee`,
  `AlsoOnDeletion`) con i tre punti del revisore sulla #146; `MapCrud` per la lista e il form, senza `DeletePolicy`, con `ToListPage` per
  chiedere all'unico handler su ogni riga; `IPermissionHolders` del nucleo per sapere chi esamina, com'è il digest dei validatori dei
  tour (T13); `IProjectable` per la voce `exam` del calendario (il tipo è nel seme da A2); `PublicSessions` e il blocco
  `training.upcomingSessions` di A10b; `Refusals` del nucleo (#152) —. **Nessun file del nucleo** (i tipi generati dell'API a parte),
  nessuna nota nuova, nessuna domanda a Carmine. **Una migrazione**, `AddExams` del contesto del training (la tabella e due indici).
- **Fatto**, come il perimetro di A10c qui sopra:
  1. **`trn_exams`** (`Exams/Exam.cs`): il candidato e l'esaminatore **solo per VID** (`candidate_vid`, `examiner_vid`), il percorso, il
     rating, la postazione di un esame ATC, l'inizio; `[Audited]`, **`[PermissionArea("Training")]`**, **`[AlsoWrittenWith(ManageExams,
     AlsoOnCreation = true, AlsoOnDeletion = true)]`**, `IHasAssignee` con l'esaminatore, nessun `IHasStakeholder`. Nel catalogo
     `Training.ManageExams` è **`OnlyForAssignee`**, e resta negato a nessuno (design §3.1). Niente esito, niente voto, niente stato.
  2. **La lista e il form** (`Exams/ExamEndpoints.cs`, `Exams/TrainingExams.cs`): `/api/training/exams`, letta con `Training.View` e
     scritta con `Training.ManageExams`, **senza `DeletePolicy`**; ogni riga dice **`mine`** (il lettore è l'esaminatore) e **`mayEdit`**
     (la risposta dell'unico handler sulla riga); `filter[examinerVid]` («Io»), `filter[kind]`, `?q=` sulla postazione e sul VID del
     candidato. **`/api/training/exam-choices`**, con `ManageExams`: gli esaminatori che l'unico handler lascia dare al lettore — un TA
     solo sé stesso, chi ha `Edit` tutti — per nome, e le postazioni. Le regole del form: un rating che la divisione allena, la postazione
     di un esame ATC fra quelle della divisione per quel rating e nessuna per un esame pilota, la data, il candidato, un esaminatore che
     non è il candidato (`examinerIsCandidate`) e che mette esami in calendario (`examinerNotExaminer`).
  3. **Il calendario**: ogni esame è **una voce pubblica di tipo `exam`**, al suo inizio, con il titolo rating · postazione, senza nomi
     né VID, che porta a **`/training`**; togliere l'esame toglie la voce.
  4. **Il sito**: `GET /api/training/sessions/exams`, anonimo — gli esami ancora da venire (dal giorno di oggi nel fuso della divisione,
     i più vicini prima, al massimo 50), con i due VID **solo a chi ha fatto il login** —; nel blocco `training.upcomingSessions` gli esami
     sono **`exams`**, accanto a `items`; **`/training`** e il blocco li disegnano con le sessioni **in un elenco solo** (`upcomingLines` in
     `screens/site.ts`), un esame come «Esame ADC · LIRF_TWR», senza una pagina sua.
  5. **Le pagine dello staff** (`screens/exams.tsx`): **`/staff/training/exams`**, la lista generata (quando, il rating, la postazione, il
     candidato e l'esaminatore per VID, «Tuo»), «Io» per restringerla ai propri, «Modifica» solo dove il server dice `mayEdit`, «Nuovo
     esame» a chi tiene `ManageExams`; **`/staff/training/exams/$id`**, il form generato, con l'esaminatore già scelto quando è il lettore, e
     «Elimina» chiesto prima. La voce «Esami» nella barra dello staff, con `Training.View`.
  6. **Via `src/IvaoHub.Modules.Training/Refusals.cs`**: i verbi del modulo usano `IvaoHub.Core.Data.Crud.Refusals`, come chiede la nota
     di #152 (`2026-09-27-i-rifiuti-di-un-form-nel-nucleo`) alla prima fase del collaboratore aperta dopo il merge ([commento di Carmine
     sulla #143][c143]). Le risposte non cambiano: stesse chiavi, ognuna una volta, nello stesso ordine; gli endpoint passano ancora il
     dizionario a `CrudProblems.Validation`, come la nota lascia fare a un verbo finché non lo si tocca per altro.
  7. **I test**: unità `TrainingExamRulesTests` (5); integrazione `TrainingExamTests` (9, VID 790090–790094 e 790068–790071); Vitest
     `screens/upcoming.test.ts` (2) e `exams.test.ts` (3); lo smoke `web/e2e/training-exams.spec.ts` (8); il giro sul banco
     `web/e2e/full/training-exams.spec.ts` (sotto).
- **I tre punti del revisore sulla #146**, ognuno con il suo test (e ognuno fatto cadere sul codice indebolito, sotto):
  1. **L'area dell'entità**: `[PermissionArea("Training")]` su `Exam`; TC e TAC, con `Edit` e non esaminatori, cambiano, passano a un
     altro e tolgono l'esame di un TA **dall'endpoint** (`TheCoordinatorAndTheAssistantWriteAnAdvisorsExamThroughTheEndpoint`), e l'handler
     e il guardiano dicono sì tutti e due (`TheHandlerAndTheGuardAnswerAlikeOnEveryExam`).
  2. **DELETE**: lo fanno il TA a cui l'esame è assegnato e chi ha `Edit`, nessun altro — né un altro TA, né un trainer, né un membro
     (`AnExamIsTakenOffTheCalendarByItsExaminerAndByWhoeverEditsTheAreaOnly`) —, con `AlsoOnDeletion` e **senza `DeletePolicy`**: un
     `DeletePolicy = Training.Edit`, la risposta del punto 2 per A7, toglierebbe al TA il suo esame, contro la risposta 4 di Carmine sulla
     #131.
  3. **Chi ha interesse**: la nota di A3b §3.6 chiede `DeniedToStakeholder` solo «se l'esame dice il suo candidato». **L'esame non lo dice**
     (niente `IHasStakeholder`), e `ManageExams` resta negato a nessuno, com'è nel design §3.1 e nel test di A4 che lo tiene
     (`TrainingSettingsTests.FiveOfTheNinePermissionsAreDeniedToWhoeverATrainingIsAbout`). Così l'handler e il guardiano dicono lo stesso
     anche sul TA che fosse candidato ed esaminatore — sì, tutti e due, su una riga che nessun endpoint lascia nascere: il form rifiuta il
     candidato esaminatore (`examinerIsCandidate`) — (lo stesso test del punto 1), e un test di unità tiene insieme l'esame e il catalogo:
     un esame «sul» candidato con il permesso non negato è il caso del punto 3, e cade.
- **Scostamenti e precisazioni, piccoli**:
  1. **La voce `exam` porta a `/training`** (A10b, «Per A10c» 2: da decidere qui). Una pagina per esame direbbe rating, postazione, data e
     ora come la voce e la riga di `/training`; l'esame vero è della rete. Un esame passato non è più fra i prossimi di `/training`, ma la
     sua voce resta nel calendario, con il suo titolo.
  2. **Degli esami solo il VID anche nelle pagine**: la lista dello staff e `/training` con il login mostrano i VID del candidato e
     dell'esaminatore, **mai i nomi**. La richiesta del TD (in cima a `HANDOFF-M3.md`) parla di ciò che l'hub tiene; qui vale anche per ciò
     che mostra, e la nota `il-training-in-pubblico` — VID e nomi a chi ha fatto il login — è rispettata mostrando meno. I nomi ci sono
     solo nella scelta dell'esaminatore del form: è lo staff che l'hub conosce, non un dato dell'esame.
  3. **L'esame non dice il suo candidato come la persona di cui è** (niente `IHasStakeholder`), com'è nel design (§3.1: `ManageExams` non
     è negato all'interessato): l'esame è una voce del calendario, non una decisione sul candidato. Una prima stesura lo faceva, con
     `ManageExams` negato all'interessato, e il test di A4 sui cinque permessi negati l'ha fermata: cambiare §3.1 è di Carmine. Resta il form,
     che rifiuta sul campo un esaminatore che è il candidato; un TC candidato di un esame lo può cambiare, come il design lascia.
  4. **Il rating di un esame è uno di quelli che la divisione allena** (il vocabolario del nucleo, `HasPracticalTraining`), come per le
     voci della scheda: l'esame alla fine di un percorso del modulo. ⚠️ **Gli esami di PATS arrivano al rating 8** (§P: SEC, ATP): se il TD
     deve mettere in calendario anche quelli, è una domanda, perché il vocabolario del nucleo non dice quali rating hanno un esame e il
     modulo non può scriverlo (estensione del nucleo, perimetro IVAO). ⚠️ *Superato* dalla risposta di Carmine su #178: ogni rating del
     percorso, qui sotto nell'ultima voce.
  5. **Chi esamina è chi tiene `Training.ManageExams` sul dipartimento base**, come lo calcola un login (`IPermissionHolders`): TC, TAC, i
     TA e la direzione, e anche il web master e il superadmin, che tengono tutto per il nucleo. Nessuna regola di livelli scritta nel
     modulo: la dice `positionGrants`.
  6. **La lista la leggono tutti con `Training.View`**, trainer compresi, e la voce del menu è di `View`, come i ban di A10a (la nota di
     A3b §3.6: «la lista la leggono tutti con `Training.View`»); i pulsanti sono di `ManageExams`, e «Modifica» solo dove il server dice
     `mayEdit`. Un TA che apre a mano il form dell'esame di un altro riceve il 403 al salvataggio.
  7. **La postazione di un esame ATC** è una della divisione per quel rating (il direttorio del nucleo), scritta come il direttorio la
     scrive; le postazioni nascoste (`hiddenPositions`) valgono per le richieste di training, non per gli esami. Il form offre le
     postazioni di tutti i rating allenati (un suggerimento chiuso, raggruppato per rating), e il server rifiuta quella di un altro.
  8. **Nessuna durata**: un esame ha l'inizio (§1.5, «data e ora»), e la sua voce del calendario nessuna fine. **Una data passata si può
     scrivere**: l'esame resta la traccia nel calendario.
  9. **Nel blocco gli esami sono `exams`, accanto a `items`**: il test di A10b legge `items`, che non cambia. La pagina e il blocco li
     mettono in un elenco solo, e il `limit` del blocco vale per i due insieme (il server ne dà al massimo `limit` di ognuno).
  10. **Se la lettura degli esami cade, `/training` disegna le sessioni e lo dice** (`public.examsUnread`), senza ripetere la lettura.
  11. **Il `View` che ogni permesso di un'area porta con sé** (A11a): chi riceve `ManageExams` per posizione — TC, TAC, TA — tiene già
      `Training.View` per posizione. Non cambia niente per nessuno.
- **Codice di fasi sotto toccato, e perché** (nessun test di un'altra fase è cambiato):
  1. `Requests/TrainingRequests.cs` (A6a) e `Sheets/EvaluationSheet.cs` (A9a): solo l'`using` del nucleo, per `Refusals`.
  2. `Public/PublicSessions.cs`, `Blocks/UpcomingSessionsProvider.cs`, `screens/public.tsx`, `screens/site.ts`, `blocks/upcomingSessions.tsx`,
     `blocks/index.ts` (A10b): gli esami accanto alle sessioni; ciò che il server risponde delle sessioni e le loro righe sono gli stessi.
     Le parole di `/training` e del blocco (`public.lead`, `upcoming`, `none`, il nome del blocco) dicono anche gli esami.
  3. `Data/TrainingDbContext.cs`: la tabella, e il vocabolario dato anche agli esami che il contesto segue.
- **La prova sul codice indebolito** (i tre punti del revisore), ognuna rimessa com'era e seguita da una build dei file toccati: senza
  `[PermissionArea]` cadono 1 test di unità e 4 d'integrazione, fra cui quello di TC e TAC dall'endpoint; con `DeletePolicy =
  Training.Edit` cadono i 2 test in cui il TA toglie il suo esame; con l'esame «sul» candidato (`IHasStakeholder`) e il permesso non negato
  — il caso del punto 3 — cadono il test di unità che tiene insieme l'esame e il catalogo e quello in cui l'handler e il guardiano devono dire
  lo stesso («The guard refused a write the handler allows»). **I test nuovi sul codice di A10b non compilano**: l'entità, gli endpoint e i
  DTO nascono qui.
- **Trovato, e scritto per chi viene dopo** (anche in `HANDOFF-M3.md`):
  1. ⚠️ **Gli smoke di A10b non fingono la lettura degli esami**: la loro finzione di `/api/training/sessions/*` le risponde 404, e la
     pagina disegna le sessioni con l'avviso; i loro test passano senza essere toccati. Lo smoke nuovo finge la lettura.
  2. **Il test di A4 sui cinque permessi negati all'interessato** (design §3.1) ha fermato una prima stesura che segnava `ManageExams`
     `DeniedToStakeholder`: è il posto dove il design di chi può che cosa è scritto in un test, e il revisore lo trova lì.
  3. `PermissionCatalog.VerifyAlternatives` non guarda ancora `[PermissionArea]`: la piccola PR del nucleo che il revisore ha annunciato
     sulla #146 lo farà; `Exam` la passerà.
  4. I VID **790068–790071** e **790090–790094** sono di A10c.
  5. ⚠️ **La colonna booleana del nucleo** (`col.boolean`) ha le parole di un interruttore, «Attivo» e «Non attivo»: una colonna sì/no che
     non è un interruttore la scrive il modulo come parola sua (`col.badge` con le sue `options`), come «Tuo» nella lista degli esami.
- **La coda**: A10c è nata in coda dopo #153 (A10b, in bozza in coda dopo #151, dopo #150, dopo #149, dopo #148, dopo #147, dopo #146, dopo
  #144): la PR è in bozza con `(after #153)` e `Queued after #153.`. Il branch è quello di A10b a 8b4cb95, che ha già `main` fino a #172
  (A3b compresa, il merge verso l'alto della coda del 28 settembre); **`main` è andato avanti ancora** (#173–#176, e #145 di A6c, unita il
  28 settembre, che tocca `HANDOFF-M3.md`): la PR è in conflitto e senza CI, come la coda sotto, e `main` non si insegue — lo prende ogni
  branch al suo passo della coda (la sessione che coordina, il 28 settembre). Quando #153 sarà unita, il passo della coda
  (`CONTRIBUTING.md`, «Phases in a queue»): `main` nel branch con un merge, con l'intestazione di A10c in cima all'handoff e i blocchi
  nuovi di `main` sotto, build e tutti i test di nuovo, via la coda, e la PR pronta con la CI verde.
- **Verificato, in locale** (28 settembre 2026, sul branch da `m3/a10b-blocks-and-public-pages`, 8b4cb95): **prima di scrivere codice**, sul
  branch com'era (un avanzamento veloce, nessuna combinazione nuova): `dotnet build` senza avvisi, unità **846/846**, integrazione intera
  **381/381**, `pnpm lint`, `typecheck` e `pnpm test` **554**. **Sul codice finale**, una suite alla volta: `dotnet build` senza avvisi;
  unità **851/851** (le 846 e le 5 nuove; `TrainingArchitectureTests` legge anche il C# e il TypeScript nuovi); **integrazione intera
  senza filtro** **390/390** al primo giro (le 381 e le 9 nuove); la classe nuova da sola 9/9 — al suo primo giro 4/9, perché senza gli
  altri test il database non aveva le postazioni della rete: il test fa ora girare `RefDataSyncJob`, come quelli della richiesta —; `pnpm
  lint`, `typecheck`, `format:check`, `i18n:check` (782 chiavi) verdi, e lo script delle chiavi letterali `training:` (374, nessuna manca);
  `pnpm test` **559** in **72** file (i 554 in 70 e i 5 nuovi in 2); `pnpm e2e` **139/139** al primo giro, con il lucchetto della porta
  4173 (i 131 e gli 8 nuovi); **`pnpm e2e:full` 48/48** al primo giro su un **banco nuovo** di questo worktree (127.0.0.1:5099,
  `ivaohub_e2e_a10c`), di nuovo **48/48** su un banco ricreato dopo l'ultima correzione del server, e una terza volta **48/48** dopo la
  correzione della colonna «Tuo» (sotto), con lo smoke di nuovo **139/139** e `pnpm test` **559**; `pnpm gen:api` e `pnpm i18n:sync`
  nei commit che li portano; `dotnet ef migrations has-pending-model-changes` senza modifiche; `dotnet format --verify-no-changes` sui 12
  file C# della fase, test compresi; le regole di `core-guard` rifatte in PowerShell: sull'intervallo della fase nessun file del
  maintainer e nessuno del nucleo; verso `main`, i due conteggi di A10b con la sua nota. **La prova sul codice indebolito**: sopra.
  **A mano**, sul banco di anteprima (127.0.0.1:5090, `ivaohub_preview`, con la build di questa fase), in italiano: come web master
  `/staff/training/exams` vuota, con «Nuovo esame» e «Esaminatore: Chiunque», e «Esami» nel menu fra «Trainee» e «Ban»; il form — il rating
  fra quelli allenati, la postazione dal suggerimento chiuso (LIRR_NE_CTR fra i settori di Roma), data e ora con l'ora di Roma sotto, il VID
  del candidato, l'esaminatore già scelto, «Bench Coordinator (999001)», fra quelli che il server offre —; salvato, nella lista con
  «Modifica»; `/training` con il login («Esame ACC · LIRR_NE_CTR», «Candidato: 999002 · Esaminatore: 999001», dopo il training del 29
  settembre); il calendario della settimana, con la voce «Esame» del 2 ottobre che porta a `/training`; da visitatore `/training` senza
  VID né nomi (letto dal testo della pagina); come trainer la lista senza «Modifica» né «Nuovo esame». **Trovato a mano e corretto**: la
  colonna «Tuo» diceva «Attivo» e «Non attivo» — la colonna booleana del nucleo ha le parole di un interruttore —: ora «Sì» e «No», parole
  del modulo, e lo smoke le legge.
- **Non verificato**: **la CI**: la PR è in conflitto con `main` sull'handoff (#145, A6c, unita il 28 settembre, e #176), come tutta la
  coda sotto, e un conflitto non fa partire `build-test`; per la sessione che coordina il merge di `main` entra nel branch al passo della
  coda, quando #153 sarà unita. **Un TA sul banco**: il banco non ne ha, e il suo giro inserisce l'esame con il web master;
  che un TA scriva solo i suoi esami lo provano i test d'integrazione, con l'identità del cookie. **Gli esami SEC e ATP** (sopra, 4).
  **Le pagine dello staff larghe 375 px**: hanno il difetto noto del nucleo a quella larghezza (A7). **La PR del nucleo del revisore**
  che farà rifiutare a `VerifyAlternatives` un'entità senza `[PermissionArea]`: non c'è ancora; `Exam` la dichiara. **A mano, la pagina in
  inglese e a tema chiaro**, e **un 409** del form degli esami dalle pagine: provati dallo smoke e dal motore (`MapCrud`), non a mano.
- **La revisione di #178** (29 settembre 2026, [i rilievi](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/178#issuecomment-5891400515);
  la sessione di A10c non c'era più, la coda l'ha portata la sessione che la coordina):
  1. ⚠️ **Aspetta Carmine**, e il master posta la risposta sulla PR: **i rating degli esami**. `Exams/TrainingExams.cs` accetta solo i
     rating con `HasPracticalTraining`, mentre il design §P dice da 5 a 8, quindi il rating 8 non si può programmare. Lo scostamento 4 lo
     diceva una domanda, ma non era stato chiesto sulla PR.
  2. **La CI** mancava perché il branch era in conflitto con `main`: ora `main` è sceso nella coda fino a qui, e la PR ha la sua CI.
  3. Tre cose per Carmine, niente da cambiare se non lo chiede lui: un esame vecchio il cui TA ha perso la posizione risponde
     `examinerNotExaminer`; web master e superadmin compaiono fra gli esaminatori offerti (scostamento 5); un TC o TAC candidato di un esame
     può modificarlo (il design §3.1 non nega `ManageExams` all'interessato).
- **La risposta di Carmine su #178** (29 settembre 2026, [la risposta](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/178#issuecomment-5891427992),
  data in chat al master e postata da lui; scritta dalla sessione che coordina la coda, sul branch temporaneo `fix3/a10c`, con la coda
  sotto nuova unita): **gli esami prendono ogni rating da 5 a 8, l'8 compreso**, non solo quelli con un training pratico; resta nel modulo.
  **Lo scostamento 4 non vale più**, e al suo posto:
  1. **La regola** (`ExamWriteDtoValidator`, `Examined`): il rating dell'esame è **uno che il vocabolario del nucleo conosce sul suo
     percorso**, allenato o no; se no, `training:errors.examRatingUnknown` sul campo (in inglese e in italiano), al posto di
     `ratingNotTrained`, che resta degli altri form. **La postazione** la chiede solo un rating che ha un tipo di postazione (ADC, APC, ACC
     su IVAO); per ogni altro è `examPositionNotAsked`, le cui parole ora dicono «di questo rating» e non «di questo percorso». Un esame SEC
     o ATP va in calendario senza postazione, con il titolo del solo rating.
  2. **Il form**: le scelte (`/api/training/exam-choices`) portano anche **`ratings`**, ogni rating dei due percorsi com'è nel vocabolario
     (`TrainingRatingDto`, la stessa forma di `/api/training/ratings`), e `screens/exams.tsx` prende da lì i rating al posto di
     `ratingsQuery`; `/api/training/ratings` resta dei rating allenati, per la scheda, le impostazioni e la richiesta. Gli aiuti del rating e
     della postazione dicono la regola nuova. `schema.d.ts` rigenerato.
  3. ⚠️ **Un'interpretazione, detta al revisore**: la risposta dice «da 5 a 8» e «ogni rating del vocabolario per il suo percorso». Il
     vocabolario non dice quali rating hanno un esame, e il modulo non scrive numeri di rating (`TrainingArchitectureTests` lo ferma),
     quindi la regola prende **ogni** rating del percorso — su IVAO dal 2 al 10 — e il TD sceglie fra 5 e 8. Se Carmine vuole che il form
     offra e il server accetti solo da 5 a 8, è una parola del vocabolario del nucleo (quali rating hanno un esame), in una fase del nucleo
     con la sua nota.
  4. **I test**: il nuovo `TrainingExamTests.AnExamTakesAnyRatingOfItsLadderTheEighthToo` programma **un esame di rating 8 per ogni
     percorso** (SEC e ATP: senza postazione, nel calendario con il titolo del rating; con una postazione, `examPositionNotAsked`) e uno del
     primo rating. **Cade sulla regola vecchia**: su una copia di `TrainingExams.cs` con `HasPracticalTraining` di nuovo nella regola,
     l'esame SEC è rifiutato (400 su `rating`) e il test cade lì; il file poi rimesso e ricompilato. ⚠️ **Due test di A10c toccati, e
     perché**: `AnExamIsRefusedFieldByField` rifiutava il primo rating del percorso con `ratingNotTrained`, che ora si accetta: rifiuta un
     rating che il vocabolario non conosce (`Unknown`, uno oltre il più alto) con `examRatingUnknown`; `TheFormOffersAnAdvisorThemselves…`
     dice anche i `ratings` delle scelte, ogni rating del vocabolario. Nello smoke `training-exams.spec.ts` le scelte finte portano i
     `ratings` con l'ottavo, e il form lo offre. L'aiuto `Untrained` è diventato `Unknown`.
- **Il passo della coda dopo #153** (29 settembre 2026: #153 unita alle 21:08 UTC; l'ha fatto la sessione che coordina la coda): `main`
  nel branch con un merge (197698e) — nessun codice nuovo rispetto alla coda, `main` portava i documenti dei passi della coda; un conflitto,
  l'intestazione di `HANDOFF-M3.md`, che tiene quella di A10c riscritta; il merge toglie anche il conflitto che teneva la PR senza
  `build-test` —, via `(after #153)` dal titolo e `Queued after #153.` dal corpo, la PR pronta a CI verde. Il revisore ha letto la risposta
  di Carmine su #178 come messa nel codice ([il commento](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/178#issuecomment-5898945166)).
  **Verificato di nuovo, in locale** (40c0dff): `dotnet build` senza avvisi; unità **870/870**; integrazione intera **407/408** al primo
  giro — è caduto `ErasureTests.WhatIsAboutThePersonGoesAndWhatTheyDidForOthersStaysUnderThePseudonym` (del maintainer) alla riga che
  cerca il VID `780095` come testo nel JSON di **tutto** il registro dell'audit del database condiviso: un numero di un'altra classe che lo
  contiene per caso (i microsecondi di un'ora, per esempio) basta —, e di nuovo l'integrazione intera **408/408**; `pnpm gen:api` e
  `pnpm i18n:sync` senza differenze; `lint`, `typecheck`, `format:check`, `i18n:check` verdi; Vitest **578/578** in 76 file; smoke
  **152/152**; **`e2e:full` 48/48** su un banco nuovo (127.0.0.1:5104).

[c143]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/143#issuecomment-5855666298

### A11 — I capi FIR

Design §1.1, §3.2, §4.2, §8 n.2, §12 n.3; nota `chi-conduce-e-chi-scrive-un-training`. **Due PR, in ordine.**

- **A11a — nucleo** (branch `m3/a11a-fir-heads-core`), **con la sua nota nuova e i test della spina dorsale**: un permesso di un modulo
  dato a una posizione FIR (CH, ACH), contato **solo sulle righe `IHasFir` del suo FIR**, nell'handler e nel guardiano. Oggi una
  posizione FIR non porta permessi, un grant a una posizione si scrive per dipartimento (`StaffPositionSubject`) e `firStaffScope`
  vale per tutta la divisione. La nota propone la forma (un soggetto FIR del grant a una posizione, e come si scrive in
  `positionGrants`); se apre una domanda è «Proposta», e il codice aspetta Carmine.
- **A11b — modulo** (branch `m3/a11b-fir-heads`): i `positionGrants` dei capi FIR (`Training.View` e `Training.Assign`, solo il loro
  FIR); l'assegnazione, la lista `/staff/training` e `training.approvalQueue` per FIR.

**Test**: spina dorsale (A11a): un grant a CH di un FIR vale sulle righe di quel FIR e su nessun'altra, né su una riga senza FIR; ACH lo
stesso; chi lascia la posizione lo perde; il guardiano lascia scrivere la riga del suo FIR e non quella di un altro. Integrazione
(A11b): il capo FIR assegna nel suo FIR e non in un altro, e vede solo i suoi nella lista e nel blocco.
**Fatta quando**: un CH assegna un training del suo FIR e riceve un rifiuto su quello di un altro FIR.

**Com'è andata (A11a)** (27–28 settembre 2026, branch `m3/a11a-fir-heads-core`, PR #159 verso `main`, fuori dalla coda) — **la nota
e le domande il 27, la nota decisa e il codice il 28**:

- **Perché A11a adesso, e solo con la nota** (la scelta della sessione di A10b, sopra in A10, «Com'è andata (A10b)»): A10c aspetta #135
  (A3b), A11b, A12a e A12b vengono dopo A10c, A12c solo con i codici di PATS. Il codice di A11a tocca lo stesso handler e lo stesso
  guardiano di A3b, e i suoi test migrerebbero lo stesso contesto di prova (`AddSampleAssignee`): una base del nucleo ancora da unire
  non si usa, e due fasi che migrano lo stesso contesto non vanno avanti insieme. Il branch è nato da `main` (51f946b, preparato dalla
  sessione di A10b) e ha preso `main` fino a 32e8acd prima del primo commit, senza merge (nessun commit suo); la PR va verso `main`
  senza `(after #N)`, come #135 e #145.
- **Classificata prima di scrivere** (`CLAUDE.md` §5): caso **(b)**, tre meccanismi che si estendono — i grant a una posizione, la
  regola del FIR dell'unico handler (`IHasFir`, `firStaffScope`), il filtro di dipartimento delle liste generate —, con due scelte che
  sono di Carmine: la nota è **«Proposta»**.
- **Fatto**: la nota `decisions/2026-09-27-i-capi-fir-sul-loro-fir.md`; la PR #159 in bozza; le due domande a Carmine in [un commento
  sulla #159][q159]. **La forma proposta** (nota §3): il **team di un FIR** come seconda specie di posizione di un grant (`positionGrants`
  con `"firTeam": true` e i livelli, senza nominare un FIR; la colonna `hub_user_grants.position_fir_team`); ogni permesso che dà porta
  **il FIR della posizione** di chi lo tiene (`EffectivePermission.Fir`, nel claim `perm`), e raggiunge solo le righe `IHasFir` di quel
  FIR; il FIR della riga viaggia con ogni domanda dell'handler e del guardiano, come lo scope, e una riga che cambia FIR chiede `Edit` sui
  due FIR; la lista generata di un'entità `IHasFir` tiene anche le righe del FIR di chi legge, e il suo dipartimento non diventa «per
  vedere». **Domanda 2**: chi dice che il permesso vale solo sul FIR — raccomandata la (a), `firStaffScope` com'è nel piano §4.1, con
  IT da `all` a `own` in A11b; (b) sempre; (c) grant per grant.
- **Scostamenti dal piano e dal design, scritti nella nota**:
  1. **anche la lista generata**, non solo l'handler e il guardiano (qui sopra e design §8 n.2): senza, la lista per FIR di A11b
     (design §4.2) sarebbe un filtro scritto a mano nel modulo;
  2. **il soggetto non nomina un FIR** (qui sopra: «un soggetto FIR del grant a una posizione»): è il team di ogni FIR, e il FIR lo dà
     la posizione di chi lo tiene, così `positionGrants` non scrive codici di FIR, che vengono da IVAO;
  3. **`firStaffScope` resta l'interruttore**, nella raccomandazione della domanda 2: il design lo diceva «per tutta la divisione» come
     un limite; la nota lo tiene perché è l'unica regola del FIR che c'è e il piano §4.1 le dà proprio questo significato. La lettera del
     design è la (b).
- **Trovato** (nota §7, e per chi viene dopo):
  1. ⚠️ **Con `firStaffScope: own` la regola del FIR di oggi ferma tutti** quelli che non hanno il FIR della riga, il personale dei
     dipartimenti compreso: TC, TAC, TA e trainer su ogni training ATC. Il piano §4.1 dice che limita i team FIR, e mai i coordinatori
     di dipartimento. Non si vede perché IT ha `all` e nessun test la prova. **Il guardiano non guarda il FIR**.
  2. **Per A11b**: un capo FIR terrà `Training.View` «da qualche parte», e alla domanda senza riga l'handler gli dice sì; ogni lettore
     del modulo che non chiede l'handler sulla riga né passa dal motore (il percorso del trainee e i suoi ban, per esempio) va guardato
     uno per uno.
  3. **Per il codice**: né `/api/me` né la firma di `HubClaims.ParsePermission` cambiano, perché un test del nucleo costruisce la risposta di
     `/api/me` (`staffDestinations.test.tsx`) e un altro confronta la tupla di `ParsePermission`
     (`ResourceScopeAndStakeholderTests`); `ICurrentUser` guadagna la domanda con il FIR con una risposta predefinita, così
     `TestCurrentUser` resta com'è (nota §6).
- **Le risposte e la revisione** (28 settembre 2026): **Carmine ha deciso** ([il suo commento][a159]) sì alla forma, alla condizione che
  la nota correggesse prima del codice i punti 1 e 2 del revisore e rispondesse ai punti 3–5 ([i rilievi][r159]), e **la (a)**:
  `firStaffScope` com'è nel piano §4.1, con IT da `all` a `own` in A11b. La nota, ora **decisa**, li porta dentro (nota §5):
  1. **il FIR nel claim sta nel pezzo dello scope, dopo un `#`** (`Training.Assign:TD@#LIRR`): un lettore che non conosce il FIR — un
     pacchetto di prima dopo un ritorno indietro, o `ParsePermission` — ci legge uno scope che nessuna riga dichiara, e lo legge chiuso;
     la prima forma (`Training.Assign:TD#LIRR`) gli dava un dipartimento illeggibile, cioè «ogni dipartimento». E **un dipartimento che il
     lettore del cookie non sa leggere non vale più «ogni dipartimento»**;
  2. **la lista generata si allarga solo con i permessi con un FIR che sono il suo permesso di lettura** (`EffectiveReadPolicy`);
  3. **un grant al team vale solo su un'area che ha un'entità `IHasFir`**, rifiutato dalla schermata e saltato dal seme, e la nota dice
     dove l'handler chiede `HasAny`;
  4. il senso nuovo di `firStaffScope` per chi è già su `own` o su `all` andrà in `FORKING.md` e in `division.example.json`;
  5. i tre rami di `IsWrittenWithAnAlternative` con il FIR della riga, e la migrazione di prova dopo `AddSampleAssignee`.
- **#135 è unita** (27 settembre, 20:24) e **`main` (4d424f9, con #135 e #160–#172) è entrato nel branch con un merge** il 28 settembre:
  l'unico conflitto era in `HANDOFF-M3.md`, risolto tenendo tutti i paragrafi. Il codice parte da qui, con i VID **790080–790089**
  (790068–790079 sono delle correzioni di A7 e A8a) e il banco **127.0.0.1:5098** (`ivaohub_e2e_a11a`).
- **Il codice** (28 settembre), come nota §3 e §6:
  1. **Il soggetto**: `UserGrant.PositionFirTeam` (la migrazione `AddGrantFirTeam` del nucleo, una colonna booleana), `IsHeldThrough` e
     `HeldThrough` (le posizioni FIR, con un FIR e senza dipartimento, ai livelli del grant), `StaffPositionSubject` con il team;
     `PositionGrantSeed.FirTeam` con `Department` facoltativo e il validatore di `division.json` («un dipartimento o il team, uno
     solo»); il seme che salta con un avviso un grant al team su un'area senza FIR, e l'impronta `firTeam|…` (quella di un seme di oggi
     non cambia); i DTO e il validatore della schermata (un soggetto solo fra tre; i livelli per le due posizioni; `firTeamArea`); la
     casella nel form generato e la colonna nella lista (`web/src/features/admin/grants/`), le parole in `common.json` ed `errors.json`.
  2. **Le aree con il FIR**: `PermissionCatalog.LearnAreasWithAFir` e `IsOfAnAreaWithAFir`, imparate da `HubPipeline.InitializeAsync`
     dagli stessi modelli di `VerifyAlternatives`, con l'area del guardiano (`HubSaveChangesInterceptor.PermissionAreaOf`, prima privata).
  3. **Il permesso sul FIR**: `EffectivePermission.Fir`; il calcolo lo scrive con `own`, uno per FIR (`Calculate(…, firStaffScope)`,
     `own` se non si dice, dalla lettura del codice qui sotto); `PermissionSet.Has(…, fir)` e `ICurrentUser.Has(…, scope, fir)`, con la risposta predefinita; nel claim
     `Nome:DIP@scope#FIR`, letto da `HubClaims.ReadPermission` (il lettore del cookie scarta un claim illeggibile), e `ParsePermission`, con
     la sua firma, che rifiuta un dipartimento che non sa leggere; `BuildIdentity` che non porta «per vedere» il dipartimento di un
     permesso con un FIR.
  4. **L'unico handler**: la domanda sulla riga porta il FIR; la regola di `firStaffScope` di prima è tolta.
  5. **Il guardiano**: `RequireAny` e i tre rami di `IsWrittenWithAnAlternative` con il FIR della riga (quello di prima per
     un'eliminazione); una riga che cambia FIR chiede `Edit` sui due, e nessuna alternativa la sposta; le sessioni di chi tiene una
     posizione FIR ai livelli di un grant al team (`HoldersOf`).
  6. **La lista**: `TryNarrowToDepartments` riceve il permesso di lettura della lista e, su un'entità `IHasFir`, tiene le righe del
     dipartimento e del FIR dei permessi con un FIR che hanno quel nome; nessun 403 «nessun dipartimento» a chi ne ha.
  7. **I documenti pubblici**: `docs/FORKING.md` e i commenti di `config/division.example.json` (come si scrive un grant al team, che
     cosa vuol dire `firStaffScope`, che cosa cambia per chi è già su `own` o su `all`).
  8. **I test**: la spina dorsale `FirTeamPermissionTests` (6, su un host con `own` e due FIR finti, `XXAA` e `XXBB`, dati da una
     directory dei FIR di prova: nessuna riga nei dati di riferimento condivisi); `SampleRecord` con il FIR (`AddSampleFir`, dopo
     `AddSampleAssignee`) e la sua lista generata `/api/sample/records`; le unità `FirTeamPermissionRulesTests` (11); Vitest
     `grants/firTeam.test.ts` (2); lo smoke `web/e2e/permissions-fir-team.spec.ts` (il form manda il team e i livelli).
- **Scostamenti e precisazioni del codice**:
  1. **`UserSyncService` carica anche i grant al team**: il login, il ricalcolo e «chi tiene un permesso» leggevano solo i grant `Vid = …`
     o `PositionDepartment != null`, e un grant al team sarebbe rimasto fuori in silenzio. La nota nominava il file solo per
     `firStaffScope`; il test «chi lascia la posizione» passa dal login vero e lo prova.
  2. **L'handler ha un costruttore scritto**, non più primario: `division` non si legge più, e un parametro primario non letto ferma la
     build (CS9113); resta nella firma, perché tre test di unità costruiscono l'handler con quei tre argomenti.
  3. **Ogni permesso di un'area implica il suo `View`, non solo `Edit`**: il calcolo scrive il `View` accanto a ogni permesso dell'area
     (`ViewOf`), con lo stesso FIR. L'esempio del punto 2 del revisore («`Training.Assign` non implica `View`») non si può costruire: un
     capo con `Assign` dal team legge le righe del suo FIR, com'è giusto. Corretto nella nota (§3.5), e il caso negativo del test 6 è un
     permesso di un'altra area.
  4. **I sette test di §3.9 sono sei**: «l'handler e il guardiano dicono lo stesso» è in ognuno.
  5. **L'aiuto della casella del team sta nell'aiuto dei livelli**: `SchemaForm` disegna un booleano come interruttore e non ne mostra
     l'aiuto.
  6. **Cambiare `firStaffScope` arriva a ogni capo al suo login dopo**: il calcolo lo legge al login, e il cookie lo porta. Scritto in
     `FORKING.md`; in IT il passaggio ad `own` arriva in A11b insieme ai primi grant al team, quindi nessun cookie vecchio ha un permesso
     del team.
- **Trovato, scrivendo il codice**:
  1. **La regola del calcolo** (punto 3 qui sopra) è più larga del suo commento («Edit implies View»): non cambiata, detta al revisore.
  2. **Il vecchio calcolo delle sessioni** di un grant a una posizione avrebbe preso, per un soggetto senza dipartimento, anche le
     posizioni HQ: `HoldersOf` chiede una posizione con un FIR.
  3. **Il test delle sessioni regge anche sul guardiano di `main`**, per la stessa ragione (sotto, «Verificato»).
- **Verificato, per la nota** (27 settembre): la nota letta contro il codice di `main` (32e8acd) e contro quello del training sui branch
  della coda (A7–A10b); le regole di `core-guard` rifatte in PowerShell su `origin/main...HEAD` (nessun file del maintainer, nessuno del
  nucleo, una nota aggiunta).
- **Verificato, in locale** (28 settembre, sul branch dopo il merge di `main` 4d424f9, una suite alla volta):
  1. `dotnet build IvaoHub.sln`: 0 avvisi, 0 errori; `dotnet format --verify-no-changes` sui 27 file C# toccati: pulito;
  2. unità **828/828** (rifatte sullo stato finale, dopo l'ultimo ritocco alle lingue); integrazione intera, senza filtro,
     **351/351**; le due classi nuove da sole, 11/11 e 6/6;
  3. **la prova sul codice vecchio**: con `HubAuthorization.cs`, `HubSaveChangesInterceptor.cs`, `HubPipeline.cs` e
     `MapCrudExtensions.cs` rimessi come su `main`, cadono **5 test di integrazione su 6** e **1 di unità su 11**, quello
     dell'handler. Reggono dalle due parti i test di unità dei pezzi che restano (il claim, il calcolo, il catalogo, i validatori) e
     quello delle sessioni, perché il vecchio calcolo prendeva già le posizioni senza dipartimento («Trovato, scrivendo il codice» 2 e
     3). Rimessi i file della fase e toccati, perché la build li ricompilasse, tornano 11/11 e 6/6;
  4. `pnpm lint`, `pnpm typecheck` e `pnpm format:check` puliti; `pnpm i18n:check`: 752 chiavi in en e it; `pnpm test`: **497/497** in
     64 file; `pnpm gen:api`: le quattro righe di `positionFirTeam` in `schema.d.ts`, nel commit;
  5. `pnpm e2e` **92/92**, con il lucchetto dello smoke su 4173. La prima volta era caduto il mio spec nuovo sull'ultima riga: la lista
     aggiunge `?page=1…` all'indirizzo;
  6. `pnpm e2e:full` **41/41** sul banco della fase, 127.0.0.1:5098, con `ivaohub_e2e_a11a` ricreato;
  7. le regole di `core-guard` rifatte in PowerShell su `origin/main...HEAD`: nessun file del maintainer, 31 del nucleo, una nota
     aggiunta, PASS.
- **Non verificato**:
  1. la CI: la dirà la PR;
  2. la regola sul modulo vero, che arriva in A11b con i grant al team e `own`;
  3. un cookie emesso sotto `all` dopo il passaggio a `own`: tiene il permesso del team su tutto il dipartimento fino al login dopo.
     È scritto in `FORKING.md`, e in IT non ce n'è;
  4. la schermata provata a mano in un browser: la disegna lo smoke, che spunta il team e guarda che cosa manda il form.
- **La lettura del codice** (28 settembre, [il commento del revisore][c159], su 4880dc7 e contro `main` 663a355): **approvabile dopo due
  cose**. I cinque rilievi della nota sono fatti, e due mutazioni di `FirTeamPermissionRulesTests` cadono come devono: `ReachesFir`
  aperto alle righe senza FIR (2 test su 11) e il FIR tolto dall'esclusione dei dipartimenti «per vedere» (1 su 11). Il revisore segnala
  al maintainer che la nota, dopo il suo sì, ha cambiato §3.5 da domanda in fatto (760dbc0). Le correzioni, in un push solo:
  1. **`main` nel branch** (3beaa7f): 663a355, con #145 (A6c) e #171–#176. L'unico conflitto era in `HANDOFF-M3.md`, risolto tenendo
     tutti i paragrafi, A11a sopra A6c; `HubPipeline.cs` si è unito da solo.
  2. **La versione 0.3.0** (3f03aea): `main` era a 0.2.7, e una migrazione additiva del nucleo con una capacità nuova sono MINOR per la
     regola scritta accanto al numero in `Directory.Build.props`. La nota lo dice nell'intestazione.
  3. **Il calcolo, se non gli si dice `firStaffScope`, chiude** (3027dc0): il predefinito era `all`, che apre, e un chiamante futuro che
     lo dimenticasse darebbe ai capi FIR tutto il dipartimento sotto `own`. **Scostamento dalla richiesta**: il revisore lo voleva
     obbligatorio, ma così non compilerebbero tre test del maintainer (`EffectivePermissionsTests`, `ReachesEveryDepartmentTests`,
     `ResourceScopeAndStakeholderTests`), che chiamano il calcolo con cinque argomenti e che il collaboratore non tocca (`CLAUDE.md` §0
     regola 3). Per loro, senza grant al team, i due valori sono lo stesso. Il predefinito è diventato **`own`**, il lato che chiude, e
     i chiamanti dell'hub passano quello della divisione. Nella nota, §6. Il test di unità che affermava il predefinito ora afferma
     `own`, e **cade con il calcolo di prima** (1 su 11); rimesso il file e toccato, torna 11/11.
  4. **Per A11b**, tutte e due chiudono e nessuna apre (anche in `HANDOFF-M3.md`):
     - ⚠️ **`PermissionHolder.Has` non passa il FIR** (`Core/Auth/Permissions/PermissionHolders.cs`): un permesso con un FIR non
       raggiunge mai una riga, quindi un capo FIR non è mai «titolare» di una riga, e nessun digest né notifica «a chi può farlo» gli
       arriva. Se A11b ne vuole una, `Has` deve prendere il FIR: è una modifica del nucleo, una PR a sé (`CLAUDE.md` §0 regola 6);
     - ⚠️ **`/api/me` non porta il FIR**, com'è deciso (nota §3.2): la SPA crede che un capo tenga `Training.Assign` su tutto il TD, e un
       bottone disegnato dai permessi di `/api/me` compare anche sulle righe degli altri FIR, dove il server risponde 403. Le pagine del
       training disegnano già i verbi di una riga dagli `actions` che l'handler ha risposto su quella riga (`permissions.ts`): A11b guarda
       che ogni bottone su una riga passi di lì.
- **Verificato, dopo la lettura del codice** (28 settembre, sul branch con `main` 663a355, una suite alla volta):
  1. `dotnet build IvaoHub.sln`: 0 avvisi, 0 errori; `dotnet format --verify-no-changes` sui 27 file C# della fase: pulito;
  2. unità **836/836**; integrazione intera, senza filtro, **359/359** (gli 8 in più sono di `main`);
  3. `pnpm lint`, `pnpm typecheck` e `pnpm format:check` puliti; `pnpm i18n:check`: 752 chiavi; `pnpm test`: **507/507** in 67 file;
     `pnpm gen:api` senza differenze;
  4. `pnpm e2e` **99/99**, con il lucchetto su 4173 (le 7 in più sono di `main`); `pnpm e2e:full` **41/41** su 127.0.0.1:5098, con `ivaohub_e2e_a11a` ricreato;
  5. le regole di `core-guard` in PowerShell su `origin/main...HEAD`: nessun file del maintainer, 32 del nucleo (in più
     `Directory.Build.props`), una nota aggiunta, PASS.

[c159]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/159#issuecomment-5877193067
[q159]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/159#issuecomment-5857885144
[r159]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/159#issuecomment-5859604416
[a159]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/159#issuecomment-5864855723

**Com'è andata (A11b)** (29–30 settembre 2026, branch `m3/a11b-fir-heads`, PR #182 verso `main`, in coda dopo #181 e, con #181 unita, in
cima alla coda):

- **Il branch e `main`**: il branch è nato da quello di A7b (ae28278, preparato dalla sessione di A7b) e **porta `main`** (efe057a:
  A11a, #159, unita il 28 settembre alle 21:54 UTC, #173–#177 e #179), entrato con un merge (b5b6dee) **prima di scrivere codice**, perché
  la fase usa sia le pagine della coda sia il meccanismo di A11a, che sta solo in `main`. È l'eccezione alla regola della coda («`main`
  entra in ogni branch al suo passo»), approvata dalla sessione che coordina il 29 settembre. Finché la coda sotto non ha preso `main`,
  l'intervallo `m3/a7b-trainer-assignee...m3/a11b-fir-heads` mostrava anche le modifiche di `main`; la PR, con `main` dentro, non era in
  conflitto e ha fatto girare `build-test` (su 522fffa: verde). I conflitti del merge, risolti tenendo tutto: `HANDOFF-M3.md`
  (l'intestazione di A7b in cima e quelle di `main` sotto; i blocchi di A11a e A6c subito sotto «Che cosa ha lasciato A7b»; nessuna riga
  dei due lati manca, controllato con uno script) e `config/division.example.json` (le due aggiunte a `$comment.positionGrants`, di A7b e
  di A11a).
- **L'Invio in più di A6b** (8807e8a): con `main` c'è #177 (la tastiera del suggerimento), e lo smoke della richiesta di A6b cadeva su un
  caso (`training-request.spec.ts`, «Enter in the position asks the question»): il primo Invio ora sceglie la postazione. È **la stessa
  modifica di una riga di c3db117 di A6b**, fatta qui perché questo branch portava #177 prima che la coda portasse su A6b, come il master
  l'ha chiesta a #144 ([il suo commento][c144-a11b]) e come l'ha chiesta a questo branch la sessione che coordina. Il pezzo è quello di
  A6b: il merge che ha portato su c3db117 attraverso A7b è stato pulito.
- **Il passo della coda, dopo #144** (29 settembre): #144 (A6b) è unita alle 10:52 UTC, e il master ha chiesto su #146 il passo di A7 e
  poi di portare il merge su per la coda ([il suo commento][q146-a11b]); l'ha fatto la sessione che coordina, perché le sessioni di A7–A7b
  non ci sono più: ogni branch da A7 ad A7b ha `main` a 47e2f70 (#144 e #183, il piano 1.24 e il design di M4) e le seconde correzioni di
  A7 e A8a; su A7b `StaffTrainings.cs` è quello di A7b, identico ad ae28278. **La cima nuova di A7b (e7b530a) è entrata qui con un merge
  (1baf8fa)**, con due conflitti:
  - `config/division.example.json`: A7b ha ora la frase di A11a come questo branch; resta l'aggiunta di A11b («The last two grants…»);
  - `HANDOFF-M3.md` (merge a incroci, due basi): il file di A7b com'è nella coda, con l'intestazione di A11b al posto di quella di A7b e
    il blocco di A11b in cima a «Lo stato». Le intestazioni vecchie di A11a, A3b e A6a, che `main` ha tolto con #144, restano tolte; i
    blocchi di A11a e A6c stanno dove li mettono `main` e la coda, dopo quello di A6b. Controllato con uno script: del file di A7b manca
    solo la sua intestazione; di quello di A11b, solo ciò che `main` o la coda hanno cambiato.

  **Ora l'intervallo `m3/a7b-trainer-assignee...m3/a11b-fir-heads` mostra solo la fase** (23 file). Due regole nuove della sessione che
  coordina: **un solo `e2e:full` alla volta** sulla macchina, con il lucchetto `$env:TEMP\ivaohub-e2efull-mailpit.lock` (Mailpit è
  condiviso: due giri insieme contano le mail l'uno dell'altro); e
  `InitialisationMarkerTests.TwoProcessesStartingTogetherBothInitialiseAndBothWriteTheMark` (del nucleo, #175) ogni tanto va in deadlock
  di MariaDB: se cade, si rilancia la classe e si scrive.
- **La revisione della #182** (29 settembre, su 0b62481, [il commento del revisore][r182]): **approvabile**. Tre nit, nessuna correzione
  chiesta: il menu che offre «Esami» e «Ban» a un capo FIR (del nucleo, da programmare per il maintainer, come dice la PR); il test di
  architettura che vuole `own` anche in `division.example.json`, così una divisione che scegliesse `all` avrebbe un test rosso, come già
  per i livelli delle voci; e il percorso di un VID qualunque, che a un capo FIR risponde 200 con la lista vuota, una perdita minima come
  per lo staff oggi. Per la consegna, la funzione vuole il `config/division.json` del tag.
- **Altri due passi della coda** (29 settembre), fatti dalla sessione che coordina da A8b ad A7b e portati qui con un merge ciascuno:
  - **b2ba9b5** (merge 01c21a5, solo in locale): `main` a 2af5133 dopo #146 e #147 (con #185 e #188), e le correzioni delle revisioni del
    master su #148, #149, #150, #151, #153, #178 e #181. Un conflitto, l'intestazione di `HANDOFF-M3.md`: quella di A11b in cima,
    riscritta come le altre, e «Accanto alle fasi del modulo», che la coda ha tolto con A6c unita, resta tolto;
  - **528edc6** (merge 6462912), subito dopo e senza spingere nel mezzo, come chiesto: **le risposte di Carmine** alle revisioni di #149,
    #150 e #178 — una sessione cominciata si registra e non si data più a mano né si chiude (`training:errors.sessionStarted`), nessuno
    data un training nel passato, la scheda disegnata nel modulo è accettata, gli esami prendono ogni rating del percorso, l'8 compreso
    (`ExamChoicesDto.Ratings`, `schema.d.ts` rigenerato) —. Un conflitto, di nuovo l'intestazione. I test e lo smoke di A11b non datano
    training e non ne chiudono: niente da cambiare.

  `main` resta a 2af5133: #148 (A8b), unita alle 17:23 UTC, sale al passo della coda di ogni branch, e un merge con `main` sarebbe pulito
  (`git merge-tree`), quindi la PR resta senza conflitti e con la sua CI.
- **In conflitto dopo #149, com'è atteso** (29 settembre, 18:22): con A9a unita, la PR è CONFLICTING con `main` sulla sola intestazione di
  `HANDOFF-M3.md`, come #151, #153, #178 e #181; `main` sale in ogni branch al suo passo, e non si rifà il merge in su. La CI di a24268b,
  partita prima, è verde.
- **Il passo della coda dopo #181** (30 settembre): #181 (A7b) è unita il 29 settembre alle 23:04 UTC, e il revisore ha chiesto sulla #182
  il passo della coda ([il suo commento][q182]). `main` (17941c0) è entrato con un merge (08cfbbb): porta solo documenti — `08` e
  `HANDOFF-M3.md`, con il passo della coda di A7b e come un'installazione che ha girato A7 pulisce i grant del trainer —, e il codice è
  quello di a24268b. Un conflitto, l'intestazione di `HANDOFF-M3.md`: quella di A11b in cima, ora in cima alla coda, con le notizie di
  `main`; i blocchi di `main` sotto quello di A11b. Controllato con uno script: del file di `main` manca solo la sua intestazione. Via
  `(after #181)` dal titolo e `Queued after #181.` dal corpo; la PR pronta dopo aver letto una volta la CI e i commenti.
- **Classificata prima di scrivere** (`CLAUDE.md` §5): configurazione (caso a) e il meccanismo di A11a usato com'è (caso b): il team di
  un FIR come soggetto di un grant, `firStaffScope`, l'unico handler e il guardiano con il FIR della riga, la lista generata ristretta al
  FIR. **Il meccanismo basta**: nessun file del nucleo, nessuna nota nuova, nessuna domanda a Carmine, nessuna migrazione.
- **Fatto**:
  1. **`config/division.json`**: due voci di `positionGrants` al team di un FIR — `Training.View` e `Training.Assign`, livelli
     `Coordinator` e `Assistant` (CH e ACH, non i CHA: design §3.2), `scope: TD`, `"firTeam": true` — e **`firStaffScope: own`** (la
     risposta 2 di Carmine sulla #159). Il training è `IHasFir` nell'area `Training`: il seme le prende (una voce al team su un'area senza
     FIR la salterebbe).
  2. **`config/division.example.json`** dice lo stesso alle altre divisioni (scostamento 2), con il perché nel commento.
  3. **I lettori del modulo, uno per uno** (l'avviso di A11a: ogni permesso dell'area implica il suo `View`, e alla domanda senza riga
     l'handler dice sì):
     - la lista `/api/training/queue`: `MapCrud` con `ReadPolicy = Training.View`, ristretta dal motore al FIR di chi legge — ogni vista,
       anche «Da approvare»; nessun training pilota, nessuno di un altro FIR. Nessun codice;
     - la pagina, i trainer proposti e l'assegnazione (`/api/training/trainings/{id}`, `…/trainers`, `…/assign`): l'unico handler sulla
       riga (`StaffTrainings.MayAsync`), e il guardiano con `[AlsoWrittenWith(Training.Assign)]` (A3) sul FIR della riga. Accettare,
       rifiutare e chiudere (`Approve`), le date e il report (`Conduct`) rispondono 403, e gli `actions` della pagina lo dicono: nessun
       bottone viene da `/api/me`. Nessun codice;
     - `training.approvalQueue`: niente da approvare (non tiene `Approve`), i training del suo FIR da assegnare; `training.trainerQueue`:
       vuoto (non tiene `Conduct`). Nessun codice;
     - **il percorso di un trainee** (`/api/training/trainees/{vid}`): chiedeva `Training.View` all'endpoint e l'handler solo su ogni
       training, e dava tutti i ban e «dove si trova» sui percorsi, ricavato dai ban e da tutti i training. Ora i ban e i percorsi sono di
       chi l'unico handler lascia leggere un ban del trainee (`TrainingBans.MayReadAsync`, chiesto come `MayBanAsync` chiede
       `Training.Ban`): lo staff del training come prima; un capo FIR no — il server non li manda (`null`, non una lista vuota) —, e legge
       i training del suo FIR, uno per uno come prima (scostamento 1). La pagina disegna ciò che arriva, con un avviso
       (`trainees.firOnly`) e «nessun suo training sul tuo FIR» al posto di «ancora nessun training»;
     - le liste degli esami e dei ban (`/api/training/exams`, `/api/training/bans`): righe che non dicono un FIR, e il capo non è di un
       dipartimento: il motore risponde 403. Le voci della scheda e le impostazioni chiedono `ManageSheets` e `ManageSettings`. Nessun
       codice;
     - `/api/me` non porta il FIR (deciso in A11a): le guardie delle rotte e il menu vedono `Training.View` e `Training.Assign` sul TD
       (sotto, «Trovato» 1).
  4. **I commenti del modulo** che dicevano A11 al futuro, o i capi FIR: `Training.cs`, `ApprovalQueueProvider.cs`, `StaffEndpoints.cs`,
     `TrainingModule.cs`, `approvalQueue.tsx`, `index.ts`.
  5. **I documenti**: `07` §4.2 (che cosa legge un capo FIR sul percorso) e la sua intestazione; qui; `HANDOFF-M3.md`.
- **Scostamenti e precisazioni**:
  1. **Il percorso di un trainee a un capo FIR**: il design (§4.2) dice che il percorso mostra i training, «pronto per…», l'attesa e i
     ban; a un capo FIR ora mostra i soli training del suo FIR, senza i percorsi e senza i ban. È la nota di A11a §3.2, decisa: un permesso
     tenuto sul FIR non raggiunge una riga che il FIR non lo dice (un ban), e «dove si trova» si ricava dai ban e da tutti i training. La
     domanda è una sola, su un ban del trainee, e decide i ban e i percorsi insieme: chi legge un ban tiene `View` sul dipartimento, e
     legge anche ogni training. Scritto in `07` §4.2.
  2. **L'esempio porta le voci dei capi e `own`**: l'esempio dice alle altre divisioni ciò che i moduli si aspettano per il loro
     dipartimento (design §3.2: «scritti in `config/division.json` e in `division.example.json`»), e per i capi FIR il design dice «solo
     il suo FIR». Con le voci e `all`, ogni CH e ACH vedrebbe e assegnerebbe ogni training del dipartimento; senza le voci, l'esempio
     non direbbe la colonna dei capi FIR. Il commento di `firStaffScope` (A11a) resta: `all` è il predefinito.
  3. **I VID 790074–790078** per la classe nuova: il range è tutto assegnato; dalberone li ha dati ad A11b il 29 settembre (erano della
     correzione di A8a, che non li ha usati: su nessun branch).
  4. **Una classe di test nuova** (`TrainingFirHeadsTests`) e non i test di `TrainingStaffTests`: serve un host con una directory di FIR
     di prova (come `FirTeamPermissionTests` di A11a, per non scrivere nei dati di riferimento condivisi), e persone senza posizioni del TD.
- **I test cambiati di altre fasi, e perché** (tutti del collaboratore; li cambia il design, §3.2, con la decisione di Carmine sulla #159):
  1. `TrainingArchitectureTests.TheDivisionFilesGiveTheTrainingDepartmentWhatTheDesignSays` (A4, unità): leggeva ogni voce `Training.*`
     come un grant al dipartimento e ne voleva nove; ora legge a parte le voci al dipartimento (nove, come prima) e quelle al team di un
     FIR (`View` e `Assign`, `Coordinator` e `Assistant`, `scope` TD, nessun `department`), e `firStaffScope: own`.
  2. `TrainingSkeletonTests.TheGrantsOfTheTrainingDepartmentArriveOnceAndReachItsPeople` (A4, integrazione): contava nove grant
     `Training.*` seminati da `division.json`, ognuno una volta; ora nove al dipartimento, ognuno una volta, e i due al team.
  3. `web/e2e/training-request.spec.ts` (A6b, smoke): l'Invio in più, sopra.
  4. `web/e2e/training-trainee.spec.ts` (A10a, smoke): un caso nuovo, il percorso come lo legge un capo FIR.
- **I test nuovi**: `TrainingFirHeadsTests` (integrazione, 2), su un host con due FIR di prova (`XXAA`, `XXBB`) e **i grant del file
  della divisione** — niente dato a mano —:
  1. **il «fatta quando»**: il CH di un FIR assegna un training del suo FIR, ed è rifiutato su quello di un altro FIR e su uno pilota —
     l'endpoint (403), l'unico handler chiesto sulla riga e il guardiano senza endpoint (`ForbiddenDomainException` su `Training.Edit`)
     dicono lo stesso —; l'ACH dell'altro FIR al contrario; nessuno dei due accetta, conduce o modifica; `/api/me` dà loro `Training.View`
     e `Training.Assign` e nient'altro del training;
  2. **che cosa legge**: la lista (ogni vista, solo il suo FIR), la pagina di una richiesta senza «accetta», il blocco (niente da
     approvare, il suo da assegnare), il blocco del trainer vuoto, il percorso (i training del suo FIR, niente percorsi né ban, niente
     «banna») contro quello del trainer del dipartimento (tutti i training, i percorsi, il ban); le liste degli esami e dei ban, 403.
- **La prova sul codice di prima**: con il codice del percorso (`TrainingBans.cs`, `TraineePaths.cs`) com'era sulla base (8807e8a) cade
  **il test della lettura** (`ladders`: atteso null, arriva un array) e regge quello dell'assegnazione, che non ne dipende; con anche i file
  della divisione com'erano cadono **tutti e due** i test nuovi, **il test del seme** dello scheletro e **il test di unità** dei file della
  divisione. Con `trainees.tsx` com'era cade **il caso nuovo dello smoke** (la pagina si rompe su `null`). Rimessi i file e toccati, perché
  la build li ricompilasse: 5/5 (le due classi), 27/27 (l'unità), 6/6 (lo spec).
- **Trovato, e scritto per chi viene dopo** (anche in `HANDOFF-M3.md`):
  1. ⚠️ **Il menu offre a un capo FIR «Esami» e «Ban»**: le voci chiedono `HasAny(Training.View)`, che un permesso sul FIR soddisfa (A11a,
     voluto), e le liste dietro rispondono 403, che `DataList` disegna come una lista vuota. Chiuso, non bello. Toglierle vuole il nucleo
     (il menu dovrebbe sapere che le righe di quella lista non dicono un FIR): non toccato, detto al revisore come proposta di una fase del
     nucleo, se Carmine la vuole.
  2. **Un avviso di una data** sulla pagina di un training porta a un altro training (`/staff/training/{id}`): se è di un altro FIR, per un
     capo FIR è «non trovato».
  3. **`PermissionHolder.Has` non passa il FIR** (A11a): nessun digest né notifica «a chi può farlo» arriva a un capo FIR. Il training non
     ne manda per `Assign`: oggi non manca niente.
  4. **Il training è l'unica entità `IHasFir`**: `own` cambia solo i grant al team di un FIR; il personale dei dipartimenti non è mai
     fermato dal FIR (A11a).
- **Verificato, in locale** (29 settembre 2026), una suite alla volta:
  - **dopo il merge di `main`, prima del codice** (b5b6dee): `dotnet build` senza avvisi; unità **869/869**; integrazione intera senza
    filtro **407/407**; `pnpm lint`, `typecheck`, `format:check` puliti; `i18n:check` 782 chiavi; `pnpm test` **578/578** in 76 file;
    `pnpm gen:api` senza differenze; le chiavi letterali `training:` 374, nessuna manca; `pnpm e2e` **151/152** (il caso di #177, sopra;
    con 8807e8a lo spec della richiesta **7/7**); `pnpm e2e:full` **48/48** su 127.0.0.1:5101 con `ivaohub_e2e_a11b` ricreato;
  - **sul codice finale** (24188dc; i documenti vengono dopo): `dotnet build` senza avvisi; unità **869/869** (nessun test di unità
    nuovo, uno cambiato); **integrazione intera senza filtro 409/409** al primo giro (le 407 e le 2 nuove); `TrainingFirHeadsTests` e
    `TrainingSkeletonTests` da sole **5/5**, `TrainingArchitectureTests` **27/27**, prima e dopo la prova sul codice di prima; `pnpm
    lint`, `typecheck`, `format:check` puliti; `i18n:check` 782 chiavi; `pnpm test` **578/578** in 76 file; `pnpm gen:api` senza
    differenze (le due righe di `schema.d.ts` sono nel commit del percorso); le chiavi letterali `training:` **376** (le 374 e le 2
    nuove), nessuna manca; `pnpm i18n:sync` nel commit che porta le parole; `pnpm e2e` **153/153** al primo giro, con il lucchetto su
    4173; `pnpm e2e:full` **48/48** al primo giro su 127.0.0.1:5101 con `ivaohub_e2e_a11b` ricreato — il banco parte con `own` e i due
    grant al team seminati, e nessun giro ne risente —; `dotnet format --verify-no-changes` sui 9 file C# della fase: pulito; le regole di
    `core-guard` in PowerShell: sulla fase (`b5b6dee...HEAD`) nessun file del maintainer e nessuno del nucleo; verso `main`
    (`origin/main...HEAD`) i due file del nucleo di A10b con la sua nota, che la coda porta già: PASS. La CI della PR su 522fffa:
    `build-test` e `core-guard` verdi;
  - **dopo il passo della coda** (1baf8fa, con `main` a 47e2f70 e le seconde correzioni di A7 e A8a), tutto di nuovo: `dotnet build` senza
    avvisi; unità **869/869**; **integrazione intera senza filtro 409/409** al primo giro (il test del marcatore d'inizializzazione non è
    caduto); `pnpm lint`, `typecheck`, `format:check` puliti; `i18n:check` 782 chiavi; `pnpm test` **578/578** in 76 file; `pnpm gen:api`
    senza differenze; `pnpm i18n:sync` senza differenze; le chiavi letterali `training:` 376, nessuna manca; `pnpm e2e` **153/153** al primo
    giro, con il suo lucchetto; `pnpm e2e:full` **48/48** al primo giro, sul banco ricreato e con il lucchetto di Mailpit; `dotnet format`
    sui 9 file C# della fase: pulito; `core-guard`: sulla fase (`origin/m3/a7b-trainer-assignee...HEAD`, 23 file) nessun file del
    maintainer né del nucleo; verso `main` (131 file) i due di A10b con la sua nota: PASS. La CI della PR su 0b62481: `build-test` e
    `core-guard` verdi;
  - **dopo gli altri due passi della coda** (6462912, sopra 01c21a5), tutto di nuovo, una volta sola: `dotnet build` senza avvisi; unità
    **869/869**; **integrazione intera senza filtro 413/413** al primo giro (le 411 di A7b e le 2 di A11b); `pnpm lint`, `typecheck`,
    `format:check` puliti; `i18n:check` 782 chiavi; `pnpm test` **578/578** in 76 file; `pnpm gen:api` e `pnpm i18n:sync` senza
    differenze; le chiavi letterali `training:` 381, nessuna manca; `pnpm e2e` **153/153** al primo giro (le 152 di A7b e il caso di
    A11b), con il suo lucchetto; `pnpm e2e:full` **48/48** al primo giro, sul banco ricreato e sotto il lucchetto di Mailpit; `dotnet
    format` sui 9 file C#: pulito; `core-guard`: sulla fase (23 file) nessun file del maintainer né del nucleo; verso `main` (116 file) i
    due di A10b con la sua nota: PASS. La CI della PR su a24268b: `build-test` e `core-guard` verdi;
  - **dopo il passo della coda dopo #181** (08cfbbb: `main` a 17941c0, solo documenti in più, il codice di a24268b), tutto di nuovo:
    `dotnet build` senza avvisi; unità **869/869**; **integrazione intera senza filtro 413/413** al primo giro; `pnpm lint`, `typecheck`,
    `format:check` puliti; `i18n:check` 782 chiavi; `pnpm test` **578/578** in 76 file; `pnpm gen:api` senza differenze; le chiavi
    letterali `training:` 381, nessuna manca; `pnpm e2e` **153/153** al primo giro, con il suo lucchetto; **`pnpm e2e:full`: al primo giro
    47/48**, sotto il lucchetto di Mailpit (preso alle 01:16, dopo quello di A12b): è caduto `full/tours-rules.spec.ts:81` (dei tour, del
    maintainer), un errore pubblico di una regola che non compare in 5 secondi — il codice è quello di a24268b, dove lo stesso giro ha dato
    48/48 tre volte —; **rifatto su un banco ricreato, 48/48**. `dotnet format` sui 9 file C#: pulito; `core-guard` verso `main` (ora 23
    file, la sola fase): nessun file del maintainer né del nucleo, PASS.
- **Non verificato**:
  - **il «fatta quando» sul banco**: il banco e2e non ha un capo FIR (i personaggi di `/e2e/signin` e `e2e-server.mjs` sono nucleo);
    aggiungerne uno sarebbe una fase del nucleo a sé, con la sua nota. Lo provano i test d'integrazione, con i grant veri del file;
  - **le schermate a mano**, come un capo FIR le vede: per la stessa ragione nessun capo sul banco di anteprima. Lo smoke disegna la pagina
    del percorso con l'API finta; la lista, la pagina e il blocco non cambiano;
  - **la CI**: la dirà la PR;
  - **un capo di due FIR**: nessun test; il calcolo gli dà un permesso per FIR (A11a, i suoi test di unità).

[c144-a11b]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/144#issuecomment-5877395930
[q146-a11b]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/146#issuecomment-5886918007
[r182]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/182#issuecomment-5891401467
[q182]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/182#issuecomment-5900753915

### A12 — Cancellazione, conservazione, archivio di PATS, giro completo

Design §6, §6.1, §7, §8 n.10, §10, §12 n.6 e n.7; note `la-cancellazione-dei-dati-di-un-trainee`, `che-cosa-resta-fuori-da-m3`.
**Quattro PR, in ordine** (la terza solo se c'è di che farla).

- **A12a — nucleo** (branch `m3/a12a-deleted-person-core`), con la sua nota nuova: l'helper «persona cancellata» nel nucleo
  (estensione n.10; oggi `memberName` nel front end dei tour), e `ErasureTests.TheColumnsThatNameAPersonAreTheOnesTheErasureKnows` che
  legge anche `TrainingDbContext`, con le colonne `trn_` nella lista. ⚠️ La copia dei tour **non si tocca**: la sostituisce una
  sessione di Carmine, e la PR lo dice al revisore.
- **A12b — modulo** (branch `m3/a12b-training-erasure`): `TrainingPersonalData : IPersonalDataEraser` con la regola della nota — i
  training chiusi restano con lo pseudonimo e senza testi liberi, quelli aperti e gli esami del candidato si cancellano (da A7b il
  trainer non ha un grant su un training: non c'è niente da togliere con loro), un ban in vigore resta con `ErasureRequest.Keep` —;
  «persona cancellata» nelle pagine del modulo; **la conservazione**: il registro resta, e le disponibilità vanno via a sessione decisa
  (già da A8: si verifica e si scrive qui).
- **A12c — l'archivio di PATS** (branch `m3/a12c-pats-archive`), **solo se** si ottiene il significato dei codici (n.6): i training di
  `trainingNEW` e gli esami di `exam` in sola lettura sul percorso del trainee, così come sono, da un dump nuovo, **mai nel
  repository**, né in una fixture. Se i codici non arrivano, la parte resta fuori e si scrive qui.
- **A12d — giro completo e chiusura di M3** (branch `m3/a12d-full-round`): `pnpm e2e:full` di tutto il percorso — richiesta →
  accettazione → assegnazione → disponibilità → scelta → report — con i personaggi del banco (A1); `FORKING.md` (il modulo, i
  `positionGrants`, le impostazioni, `theoryExamUrl`); **il rapporto di chiusura di M3** come `decisions/2026-09-07-m1-review.md`, con
  gli endpoint scritti a mano accanto al motore e l'eccezione della nota `le-note-riservate-e-il-trainee` contati (piano §16 punto 6).

**Test**: integrazione della cancellazione (design §10): i conteggi del registro uguali prima e dopo, nessun testo libero rimasto, i
training aperti e gli esami del candidato spariti, il ban in vigore rimasto, «persona cancellata» nelle
pagine; `ErasureTests` con le colonne del training. Il giro completo verde.
**Fatta quando**: il giro completo passa in locale e in CI; la cancellazione di un trainee di prova lascia il registro contato uguale;
il rapporto di chiusura è scritto. A M3 chiusa **PATS resta acceso solo per il feed del calendario dei trainer**, fino all'iCal del
nucleo in M6 (nota `che-cosa-resta-fuori-da-m3`).

**Com'è andata (A12a)** (29 settembre 2026, branch `m3/a12a-deleted-person-core`, PR #187 verso `main`, in bozza in coda dopo #182) —
**la nota e le domande, poi le risposte di Carmine e il codice, lo stesso giorno**:

- **Perché prima la nota**: A12a è una fase del nucleo (qui sopra, «Regole di tutte le fasi»), e la forma apriva due scelte che sono di
  Carmine, una delle quali sul suo test (`ErasureTests`, T20b): la nota è nata **«Proposta»** e il codice ha aspettato le risposte
  (`CLAUDE.md` §5), come in A11a. Il branch è nato dalla cima della coda, `m3/a11b-fir-heads` a 0b62481, preparato dalla sessione di
  A11b con `--no-track`; ha `main` a 47e2f70, e si unisce senza conflitti con il `main` di oggi (dopo #146, #147 e #184–#186).
  L'intervallo della fase è `m3/a11b-fir-heads...m3/a12a-deleted-person-core`.
- **Classificata prima di scrivere** (`CLAUDE.md` §5): caso **(b)**. Si porta nel nucleo l'helper che la nota di T20b (§3) aveva già
  deciso di portarci; si estende la lista generata di un tipo di colonna; si tocca un test del maintainer, e come lo decide lui.
- **La nota e le domande**: `decisions/2026-09-29-la-persona-cancellata-nel-nucleo.md`, la PR #187 in bozza, le due domande a Carmine in
  [un commento sulla #187][q187]: la forma nel nucleo (nota §3), e come `ErasureTests` legge le 21 colonne del training — (a)
  `TrainingDbContext` scritto nel test, (b) un test accanto nei file del training, **(c) i contesti di ogni modulo dal registro**, come
  li scorre la cancellazione, tranne quello di prova.
- **Le risposte** (29 settembre, Carmine in chat alla sessione master, pubblicate su sua istruzione: [il commento sulla #187][a187]):
  **sì alla forma** e **la (c)**, tutte e due come raccomandato; la copia dei tour e `memberLabel` del training restano come sono in
  questa PR, e la conseguenza per M4 (il punto 1 di E8a) la porta nel piano il master dopo l'unione. **Su A12c**, nello stesso
  commento: il codice sorgente di PATS non esiste, c'è solo il database già condiviso, quindi il significato dei codici oggi non si
  conosce; **A12c resta condizionata**. La nota, ora **decisa**, lo registra con il link.
- **Fatto** (commit `b8f515a`, `c41a43e`, `608d843`):
  - **`web/src/shared/ui/people.ts`**, esportato da `shared/ui`: `NamedPerson` (`{ vid, name }`, la forma di `TrainingMemberDto` e del
    `MemberDto` dei tour), `isErased(vid)` (`vid < 0`) e `personName(person, t)` — «Deleted person» per uno pseudonimo, `Nome (VID)`, o
    il VID da solo;
  - **la colonna `person` della lista generata**: `col.person('trainee')` su un campo `NamedPerson | null`, disegnata da `DataList` con
    `personName`; una cella vuota è una riga senza nessuno (un training non assegnato). La riga in `docs/UI-GUIDELINES.md`, accanto a
    `col.file`;
  - **la parola `people.deleted`** in `locales/{en,it}/common.json` («Deleted person», «Persona cancellata») — non `people.erased`,
    qui sotto;
  - **`ErasureTests.TheColumnsThatNameAPersonAreTheOnesTheErasureKnows`** legge i contesti del nucleo e di ogni modulo abilitato dal
    `ModuleRegistry`, tranne `SampleModule`, e la sua lista ha le 21 righe `trn_` (97 righe);
  - i test Vitest `web/src/shared/ui/people.test.ts` (2) e `web/src/shared/list/DataList.test.tsx` (1); `07` §6.1 corretto.
- **Scostamenti dal piano e dal design, scritti nella nota**:
  1. **anche una colonna della lista**, non solo la funzione (qui sopra: «l'helper»): il design §6.1 chiede «le liste», e la copia dei
     tour non ci arriva, perché una lista calcola i nomi nella query, dove non c'è `t`;
  2. **con la (c)**, `ErasureTests` legge i contesti di ogni modulo e non soltanto `TrainingDbContext` (qui sopra: «che legge anche
     `TrainingDbContext`»). Cambia anche il piano di M4: la prima fase degli eventi che crea una colonna di persona scrive le sue righe
     nella lista, e del punto 1 di E8a non resta niente (nota §4);
  3. **la parola è `people.deleted`**, non `people.erased` come la nota proponeva e Carmine ha preso (nota §7): **il catalogo delle
     lingue del server rifiuta all'avvio** una chiave del nucleo che un modulo dichiara di nuovo (`LocaleCatalog.AddModules`, nota
     `2026-09-26-le-parole-di-piu-moduli`), e `people.erased` c'è già in `flightops.json`, la copia dei tour, che non si tocca. Trovato
     al primo giro di `ErasureTests`: l'host non partiva («declared twice for the same language»), e i quattro test cadevano. È la stessa
     decisione con un'altra chiave; quando una sessione di Carmine sostituirà la copia dei tour, `flightops:people.erased` andrà via con
     lei.
- **Trovato** (nota §2, e per chi viene dopo):
  1. **La coda dei PIREP mostrerebbe lo pseudonimo come numero**: la copia dei tour lo dice nel suo commento. Non si tocca (`CLAUDE.md`
     §0 regola 2); la sostituisce una sessione di Carmine.
  2. **Il piano di M4 aspetta lo stesso pezzo** per la scheda «Prenotazioni» dello staff, una lista generata (E6b), e **allarga lo
     stesso test** agli eventi (E8a): chi arriva secondo tiene tutte e due le liste.
  3. **Le colonne di persona del training sono 21**, tutte con un nome della convenzione; `trn_evaluations` non ne ha, nessuna è una
     chiave, nessuna colonna JSON porta VID. Lette prima nelle entità e nello snapshot, ora le produce il test: sono le stesse.
  4. **Per A12b**: `memberLabel` sta in 28 punti; i link al percorso di un trainee (`traineeHref`: la pagina di un training, l'azione
     della lista dei ban) vanno tolti per uno pseudonimo; le pagine degli esami mostrano solo VID, e `personName({ vid, name: null }, t)`
     dà lì il VID o «Deleted person». ⚠️ **Un training aperto affidato a un trainer che si cancella** resta affidato allo pseudonimo (la
     risposta 4 della nota di T20b: ciò che ha fatto come trainer resta), quindi lo conduce solo chi tiene `Training.Edit`, e la mail
     della sessione al trainee nominerebbe il trainer con il numero (`TrainingPeople.Label`): la regola del design (§6.1) parla dei
     training del trainee e di quelli condotti, non di questo. Da decidere in A12b, forse con una domanda a Carmine.
- **Verificato** (29 settembre, in locale, una suite alla volta, sul codice finale `608d843`):
  - `dotnet build IvaoHub.sln` 0 avvisi; `IvaoHub.UnitTests.exe` **869/869**; **`IvaoHub.IntegrationTests.exe` intero, senza filtro,
    409/409** al primo giro (nessun test nuovo: `ErasureTests` cambia il suo; `InitialisationMarkerTests` non è caduto); `ErasureTests`
    da sola **4/4**;
  - `pnpm lint`, `typecheck`, `format:check` puliti; `pnpm i18n:check` **783** chiavi (le 782 di prima e `people.deleted`); `pnpm test`
    **581/581** in 78 file (i 578 di prima e i 3 nuovi, in 2 file nuovi); `pnpm gen:api` senza differenze (l'API non cambia); le chiavi
    letterali `training:` 376, nessuna mancante; **`pnpm e2e` 153/153** al primo giro, sotto il lucchetto di 4173; **`pnpm e2e:full`
    48/48** al primo giro, sul banco ricreato (127.0.0.1:5102, `ivaohub_e2e_a12a`) e sotto il lucchetto di Mailpit, preso quando la
    sessione che coordina l'ha lasciato;
    `dotnet format --verify-no-changes` sul file C# della fase (`ErasureTests.cs`): pulito; le regole di `core-guard` in PowerShell:
    sulla fase (`origin/m3/a11b-fir-heads...HEAD`) nessun file del maintainer, 10 del nucleo con la nota aggiunta, PASS; verso `main`
    anche i due di A10b con la sua nota, che la coda porta già: PASS;
  - **la prova sul codice vecchio**: con `columns.ts`, `DataList.tsx` e i due `common.json` come sulla base (0b62481), **cadono tutti e
    due i file Vitest nuovi** (`col.person is not a function`; «expected 'people.deleted' to be 'Deleted person'»); con i due contesti
    scritti a mano com'erano e la lista nuova **cade il test delle colonne** (mancano le righe `trn_`); con il registro ma **senza
    togliere il modulo di prova**, cade con `smp_items.created_by` al posto 76 — il contesto di un modulo entra da solo. Rimessi i file
    della fase e toccati perché la build li ricompilasse: 3/3 e 4/4.
  - La CI della PR sul secondo push, di soli documenti (b185554): `build-test` e `core-guard` verdi; **sul codice (e3b84d7)** verdi tutti e
    due (`build-test` in 21 minuti).
- **Il passo della coda** (29 settembre, sera): la catena dopo #147 — `main` a 2af5133 (#146, #147, #184–#186, #188), le correzioni delle
  revisioni da #148 a #181 e le risposte di Carmine alle revisioni di #149, #150 e #178 — è salita fino ad A11b (a24268b), che la sessione di
  A11b mi ha scritto; è entrata qui con un merge (`8c64794`). **Un conflitto solo**, l'intestazione di `HANDOFF-M3.md`: quella di A12a resta
  in cima, riscritta, e il paragrafo «Accanto alle fasi del modulo» resta tolto, come in A7b. Nessuna riga che uno dei due lati teneva è
  andata persa (controllato riga per riga su `HANDOFF-M3.md`, `08` e i due `common.json`, dove restano i link legali di #188 e
  `people.deleted`). **L'intervallo `m3/a11b-fir-heads...m3/a12a-deleted-person-core` mostra di nuovo solo la fase** (14 file). Poi tutto di
  nuovo, una suite alla volta: `dotnet build` 0 avvisi; unità **869/869**; **integrazione intera, senza filtro, 413/413** al primo giro (i
  409 e i 4 della catena); `pnpm lint`, `typecheck`, `format:check` puliti; `i18n:check` 783 chiavi; `pnpm test` **581/581** in 78 file;
  `pnpm gen:api` e `pnpm i18n:sync` senza differenze; le chiavi `training:` 381, nessuna mancante; **`pnpm e2e` 153/153** al primo giro,
  sotto il suo lucchetto; **`pnpm e2e:full` 48/48** al primo giro, sul banco ricreato e sotto il lucchetto di Mailpit; `core-guard` sulla
  fase: nessun file del maintainer, i 10 del nucleo con la nota, PASS. ⚠️ **Mentre il passo girava, #149 (A9a) è stata unita** (b2409aa,
  16:22 UTC): il suo `HANDOFF-M3.md` va in conflitto con ogni branch della coda, A11b compreso, quindi la PR è in conflitto con `main` e
  **non ha CI** finché `main` non entra al prossimo passo della coda; nessun merge di `main` di iniziativa.
- **Il secondo passo della coda** (30 settembre, notte): unite #150, #151, #153, #178 e #181, A11b è in cima alla coda, e il suo passo ha
  portato `main` (17941c0) nel suo branch (99421c2), che la sessione di A11b mi ha scritto; è entrato qui con un merge (`2c5b37b`). Porta
  **solo documenti** (`08` e `HANDOFF-M3.md`: nessun file fuori da `docs`), quindi il codice è quello delle suite qui sopra; un conflitto solo,
  l'intestazione dell'handoff, riscritta. Il branch si unisce senza conflitti con il `main` di dopo (6704aad, con #190): **la PR non è più in
  conflitto e ha di nuovo la CI**. Le suite rifatte comunque, una alla volta, come chiede `CONTRIBUTING.md` dopo un merge di `main`, tutte
  al primo giro: `dotnet build` 0 avvisi; unità **869/869**; integrazione intera **413/413**; `pnpm lint`, `typecheck`, `format:check`
  puliti; `i18n:check` 783 chiavi; `pnpm test` **581/581** in 78 file; `pnpm gen:api` senza differenze; le chiavi `training:` 381, nessuna
  mancante; **`pnpm e2e` 153/153**; **`pnpm e2e:full` 48/48** sul banco ricreato, sotto il lucchetto di Mailpit; `core-guard` sulla fase:
  PASS.
- **Il passo della coda di #187** (30 settembre, 00:03 UTC: **#182 è unita**): `main` (a004c20, con #182, che il branch aveva già dalla coda,
  e con #190 del maintainer, la versione 0.4.2 e la sua nota) è entrato con un merge **senza conflitti** (`2a35868`): la PR mostra verso
  `main` solo i 14 file della fase, e `core-guard` verso `main` passa (nessun file del maintainer, i 10 del nucleo con la nota). Via
  `(after #182)` dal titolo e `Queued after #182.` dal corpo. Tutte le suite di nuovo, una alla volta, tutte al primo giro: `dotnet build`
  0 avvisi; unità **869/869**; integrazione intera **420/420** (i 413 e i 7 di #190); `pnpm lint`, `typecheck`, `format:check` puliti;
  `i18n:check` 783 chiavi; `pnpm test` **581/581** in 78 file; `pnpm gen:api` senza differenze; le chiavi `training:` 381, nessuna mancante;
  **`pnpm e2e` 153/153**; **`pnpm e2e:full` 48/48** sul banco ricreato, sotto il lucchetto di Mailpit (preso quando A12d l'ha lasciato).
- **La revisione del master** ([il suo commento sulla #187][r187], 30 settembre): **approvabile**, niente di bloccante; «should know» la
  chiave `people.deleted` (nota §7: il master scrive nel piano quella vera). **Due nit**, a scelta del collaboratore:
  1. **su che cosa ordina il server una colonna `person`**: ora lo dicono il commento di `col.person` e `UI-GUIDELINES.md` — `sortable`
     solo quando il server la dichiara in `CrudOptions.Sortable`, come ogni colonna, e allora per nome, che è quello che la cella mostra.
     Solo commenti e linee guida: rifatti `pnpm lint`, `format:check`, `typecheck` (puliti) e i Vitest della lista e delle persone (8/8);
  2. **`ErasureTests` vede i moduli abilitati nell'host di prova**, come la cancellazione (`PersonalDataErasure.cs:104`): **non cambia**, è
     la lettera della risposta di Carmine («every enabled module, the way the erasure goes through them»). Oggi nell'host di prova i moduli
     sono tutti accesi. Il punto più largo — un'installazione che spegne un modulo lo toglie anche dalla cancellazione, e le sue righe
     restano con il VID — è del meccanismo del nucleo, non di questa fase: detto al revisore, per Carmine se lo vuole.
- **Non verificato**:
  - **le pagine**: il nucleo dà il pezzo, ma nessuna pagina lo usa ancora (A12b per il training, una sessione di Carmine per i tour);
    la colonna `person` è provata in Vitest, non su una schermata vera né nella galleria, che non la mostra;
  - **una mail** che nomini una persona cancellata: nessuna la scrive qui (nota §3, punto 2);
  - **la CI dopo il passo della coda**: la dirà la PR.

[q187]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/187#issuecomment-5890079195
[a187]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/187#issuecomment-5891244551
[r187]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/187#issuecomment-5901449982

**Com'è andata (A12b)** (29–30 settembre 2026, branch `m3/a12b-training-erasure`, PR #189 verso `main`, in cima alla coda da quando #187
è unita) —
**l'eraser del modulo, «persona cancellata» nelle pagine e la conservazione; una domanda a Carmine, decisa il 30 settembre, e la sua
parte**:

- **Il branch**: preparato dalla sessione di A12a con `--no-track` dalla cima della coda, `m3/a12a-deleted-person-core` a ba557bb (con
  `main` a 2af5133). L'intervallo della fase è `m3/a12a-deleted-person-core...m3/a12b-training-erasure`. Nasce in conflitto con `main`
  (l'handoff, dopo #149, #150, #151 e #153, unita due minuti prima che la PR si aprisse) e senza CI, come tutta la coda: `main` entra al
  suo passo della coda.
- **Classificata prima di scrivere** (`CLAUDE.md` §5): caso **(b)** — il modulo si aggancia al meccanismo del nucleo
  (`IPersonalDataEraser`, `ErasureRequest.Keep`, la convenzione delle colonne, «persona cancellata» di A12a) —, **tranne una parte (c)**:
  un training aperto di un altro membro, affidato a un trainer che si cancella, che la regola del modulo non dice (A7b n.4, A12a
  «Trovato» n.4). Prima la nota **«Proposta»** `decisions/2026-09-29-il-training-affidato-a-chi-si-cancella.md` e la PR in bozza con
  [la domanda][q189] (raccomandata la (a): resta affidato alla persona cancellata, l'anteprima lo conta, torna nella vista «da
  assegnare», le mail dicono «Persona cancellata»); quella parte ha aspettato la risposta, il resto no. Dall'eraser il training non si
  rimette «Accettato» né si chiude: in modalità cancellazione l'interceptor svuoterebbe la storia d'audit di un training di un altro
  membro (nota §2). **La risposta** (30 settembre, Carmine in chat alla sessione master, pubblicata su sua istruzione: [il commento sulla
  #189][a189]): **la (a)**, come raccomandato; la nota ora è **decisa** e dice com'è scritta (§6).
- **Fatto** (commit `84c9df5`, `0310805`, `821b080`, `beb2901`, `3259336`, `4874823`, `49d81f1`):
  - **`TrainingPersonalData : IPersonalDataEraser`** (`src/IvaoHub.Modules.Training/TrainingPersonalData.cs`, registrato in
    `TrainingModule`), con la regola della nota `la-cancellazione-dei-dati-di-un-trainee`: i training **finiti** del trainee restano
    nel registro e perdono i testi — i due della richiesta, il motivo di un rifiuto e di una chiusura, i due commenti del report, gli
    appunti delle sessioni, i commenti e le note della scheda —; quelli **aperti** si cancellano **dal change tracker** (A8a n.11: la
    voce del calendario va nello stesso salvataggio), con date, sessioni e scheda; gli **esami** in cui è candidato si cancellano, con la
    loro voce; un **ban in vigore** resta con `ErasureRequest.Keep`, uno finito o tolto perde il motivo. Anteprima ed esecuzione con cinque
    righe `training:erasure.*`; idempotente: una seconda cancellazione trova solo il ban tenuto, e lo anonimizza quando è finito;
  - **le mail nominano le persone anche per VID** (`TrainingMail.Name`: `traineeVid`, `trainerVid` accanto al nome), così la
    cancellazione del nucleo le trova (qui sotto, «Trovato» n.1);
  - **nessun percorso per uno pseudonimo**: la rotta è `GET /api/training/trainees/{vid:int:min(1)}` (404 sotto 1), `traineeHref` non dà
    un indirizzo per uno pseudonimo, la pagina del percorso non chiede un VID negativo;
  - **un ban dato da una persona cancellata la nomina** (`givenBy` con lo pseudonimo) invece di nessuno: solo lo `0` dell'installazione
    è nessuno (`TrainingBans.Row`);
  - **«Persona cancellata» nelle pagine** (i quattro punti della revisione di #151 compresi): `personName` del nucleo nei 28 punti di
    `memberLabel`, che va via con il suo test Vitest; `col.person` nelle liste dei training (`trainee`, `trainer`), dei ban (`trainee`,
    `givenBy`) e degli esami (`candidate`, `examiner`, senza nome: resta solo il VID), con le chiavi dei campi rinominate; la pagina di un
    training nomina uno pseudonimo senza link, la lista dei ban non offre il suo percorso; `/training` e la pagina di una sessione dicono
    «Persona cancellata» per un trainee, un trainer o un esaminatore cancellato;
  - **la chiusura dello staff si riconosce da chi l'ha chiusa** (A8a n.4: `closed_by` vuoto è il segno sicuro): `closingOf` legge
    `closedBy` quando la risposta lo porta (quella dello staff), e la pagina dello staff dice ancora chi ha chiuso un training il cui
    motivo è andato con i dati del trainee; la risposta del trainee, che non lo porta, si legge ancora dal motivo;
  - **la conservazione, verificata**: il registro resta (nessun `DELETE` di un training, A7b); le disponibilità vanno via a sessione
    decisa — nel codice di A8 (`TrainingDates.Date`, `Close`, `StaffTrainings.AssignAsync`) e nei test di A8a (la scelta del trainee, la
    data a mano, la chiusura della notte, un altro trainer); il test nuovo prova l'ultima strada, la chiusura dello staff di un training
    che aspetta la data (`TrainingDatesTests.TheStaffsClosingOfATrainingThatWaitsForItsDateTakesTheDatesProposed`);
  - **la parte della risposta** (commit `b28e37f`, `1cd03bb`, `9d0c4df`): un training aperto di un altro membro affidato a un trainer che
    si cancella resta affidato alla persona cancellata, e l'eraser non lo tocca ma lo conta (la sesta riga, `training:erasure.assigned`);
    la vista **«da assegnare»** (`StaffQueue.ToAssign`), e con lei il blocco `training.approvalQueue`, prende un training `Assigned` o
    `Scheduled` con `trainer_vid < 0`, che esce da «in corso» e «da chiudere», finché «Assegna» non lo dà a un altro trainer con la sua
    data; **ogni mail del modulo che nomina qualcuno** passa da un metodo solo (`TrainingMail.Name`, nella lingua di chi la riceve), che
    per uno pseudonimo scrive la parola del nucleo `people.deleted`, e a un trainer cancellato non si scrive niente
    (`TrainingPeople.IsErased`). Un esame affidato a un esaminatore cancellato resta com'è;
  - **i test**: `TrainingTraineeTests.Erasure.cs` (parziale della classe di A10a, con le sue persone, come `PirepTests.Erasure` nei
    tour; la pulizia della classe prende anche gli esami): la cancellazione di un trainee — il registro contato uguale prima e dopo,
    nessun testo con il segno del test nelle righe né nell'audit, i training aperti e l'esame del candidato spariti con le loro voci del
    calendario (quello di un altro resta), il ban in vigore tenuto e poi anonimizzato da una seconda cancellazione a ban finito, la mail
    della data al trainer sparita, le pagine dello staff con lo pseudonimo, nessun percorso per lui —, quella di un membro dello staff —
    il suo lavoro e le sue parole restano con lo pseudonimo, il ban dato compreso — e quella di un trainer con due training aperti di
    altri — contati, fra quelli da assegnare nella lista e nel blocco, il promemoria con «Persona cancellata» nella lingua del trainee,
    «Assegna» che ne riprende uno con la sua data, l'esame com'era —; un test di unità delle viste (`TrainingStaffRulesTests`: un
    training aperto di un trainer cancellato è da assegnare e in nessun'altra vista); Vitest (`path.test.ts`, `dates.test.ts`); smoke
    (`training-staff`, `training-trainee`, `training-exams`: quattro casi nuovi).
- **Scostamenti dal piano e dal design**:
  1. **anche le mail** (qui sotto, «Trovato» n.1): `08` non le nomina, ma senza il VID accanto al nome la cancellazione lasciava il nome
     di una persona cancellata nelle mail dell'altra;
  2. **il motivo di una chiusura dello staff va via** con gli altri testi: la nota del trainee elenca i testi prima che A8 aggiungesse
     `close_reason`, ma dice «tutti i testi liberi che parlano di lui»; A8a n.4 l'aveva previsto, e la pagina ora distingue da `closedBy`;
  3. **restano le ore e il rating del trainee al momento della richiesta** (`trainee_hours_at_request`, `trainee_rating_at_request`): la
     regola tiene «stati, date, rating, voti e spunte», e non sono testi; con lo pseudonimo non nominano nessuno. Detto al revisore;
  4. **uno pseudonimo non ha un percorso** (404), non una pagina «Persona cancellata»: il design dice «senza link», e la revisione di
     #151 chiedeva che la rotta non prendesse i negativi.
- **Trovato** (anche in `HANDOFF-M3.md`):
  1. **Le mail del training nominavano le persone solo a parole.** Il nucleo trova le notifiche *su* una persona dal suo VID in una
     proprietà che si chiama come una persona (`vid`, `*Vid`, `*By`: `AuditRedaction.Mentions`), come portano le mail dei fili
     (`ContactThreads`) e delle segnalazioni dei tour; quelle del training scrivevano «Nome (VID)» in `trainee`/`trainer`, e dopo la
     cancellazione di uno dei due la mail all'altro restava con il suo nome. Caso (b): il meccanismo c'è, il modulo lo usa. Il test lo
     prova, e sul codice vecchio cade (la mail resta: 1 invece di 0).
  2. **`TrainingBans.Row` diceva «nessuno»** per chi aveva dato un ban con un VID non positivo (`CreatedBy > 0`): una persona cancellata
     spariva dal ban invece di esserci come tale.
  3. **La domanda** (nota nuova, [sulla #189][q189], decisa [la (a)][a189]): un training aperto affidato a un trainer che si cancella
     restava affidato allo pseudonimo, lo conduceva solo chi tiene `Training.Edit`, nessuna vista lo segnalava, e il promemoria al trainee
     nominava il trainer con il numero; ora torna fra quelli da assegnare e le mail dicono «Persona cancellata». Un esame affidato a un
     esaminatore che si cancella resta com'è: il form lo mostra senza esaminatore, e il salvataggio ne chiede uno.
  4. ⚠️ **Il promemoria sposta la versione del training** (il suo segno `reminded_at` è una scrittura della riga): una pagina letta prima
     del giro del job riceve 409 a «Assegna». Il test d'integrazione rilegge la pagina dopo il giro; nelle pagine vere il 409 fa già
     rileggere la pagina.
- **Verificato** (29–30 settembre 2026, in locale, una suite alla volta; vedi anche il corpo della PR):
  - sul codice prima dell'ultimo test (`4874823`): `dotnet build IvaoHub.sln` 0 avvisi; `IvaoHub.UnitTests.exe` **869/869**;
    **`IvaoHub.IntegrationTests.exe` intero, senza filtro, 415/415** al primo giro (i 413 di A12a e i 2 nuovi); `TrainingTraineeTests` da
    sola **7/7**; dopo il test della conservazione (`49d81f1`) `TrainingDatesTests` da sola **13/13**; su `49d81f1` di nuovo: `dotnet
    build` 0 avvisi, unità **869/869**, **integrazione intera, senza filtro, 416/416** al primo giro;
  - **dopo la risposta** (`9d0c4df`): `TrainingTraineeTests` da sola **8/8**, `TrainingStaffRulesTests` **6/6**; `dotnet build` 0 avvisi,
    unità **870/870**, **integrazione intera, senza filtro, 417/417** al primo giro; le chiavi letterali `training:` **387** (con
    `erasure.assigned`); il web e lo smoke di nuovo, qui sotto;
  - `pnpm lint`, `typecheck`, `format:check` puliti; `pnpm i18n:check` **783** chiavi; `pnpm test` **582/582** in 78 file (i 581 di A12a,
    meno il test di `memberLabel`, più i due nuovi); `pnpm gen:api` e `pnpm i18n:sync` senza differenze; le chiavi letterali `training:`
    **386**, nessuna mancante (le 381 e le cinque `erasure.*`); **`pnpm e2e` 157/157** al primo giro, sotto il lucchetto di 4173 (i 153 e
    i 4 nuovi); **`pnpm e2e:full` 48/48** al primo giro, sul banco ricreato (127.0.0.1:5105, `ivaohub_e2e_a12b`) e sotto il lucchetto di
    Mailpit. Dopo la risposta di nuovo: lint, typecheck e format puliti, `i18n:check` 783, `pnpm test` 582/582, `gen:api` e `i18n:sync`
    senza differenze, **`pnpm e2e` 157/157**, **`pnpm e2e:full` 48/48** al primo giro, sul banco ricreato;
    `dotnet format --verify-no-changes` sugli 11 file C# della fase: pulito; le regole di `core-guard` in PowerShell sulla fase: nessun file
    del maintainer, nessuno del nucleo, la nota aggiunta, **PASS**;
  - **la prova sul codice vecchio**: con gli 8 sorgenti della fase come sulla base (ba557bb) **cadono tutti e due i test d'integrazione
    nuovi** (le righe dell'anteprima vuote; `givenBy` nullo); con l'eraser ma **senza il VID nelle mail** cade quello del trainee sulla
    mail del trainer (1 invece di 0); con **la rotta di prima** cade sul percorso dello pseudonimo (200 invece di 404); in Vitest, con
    `path.ts` e `dates.ts` come sulla base, **cadono i due casi nuovi**; nello smoke, con il codice web del modulo come sulla base,
    **cadono esattamente i quattro casi nuovi** (20 passano). Rimessi i file e toccati perché la build li ricompilasse: 7/7 e 16/16, e
    lo smoke intero, dopo, 157/157. Il test della conservazione passa anche sul codice di A8, e deve: prova una strada che c'era già.
    **Per la parte della risposta**: con gli 8 sorgenti come prima di lei (`38e3988`) il test d'integrazione nuovo cade sulla riga
    dell'anteprima; con solo `StaffQueue.cs` di prima, sulla vista «da assegnare» (vuota invece dei due); con solo le mail di prima, sul
    promemoria (`-1` invece di «Persona cancellata»); il test di unità nuovo cade con `StaffQueue.cs` di prima. Rimessi e toccati: 8/8 e
    6/6.
- **La coda sotto** (30 settembre, notte): la nuova cima di A12a (62eee6f) — il passo di #182 dopo #181, con `main` a 17941c0, arrivato
  per A11b (99421c2), solo documenti — è entrata qui con un merge (`2fdc3a5`), come mi ha scritto la sessione di A12a. **Un conflitto
  solo**, l'intestazione di `HANDOFF-M3.md`: quella di A12b in cima, riscritta, con ciò che quella di A12a diceva della coda e della
  consegna di A11b; i paragrafi di A12a e A11b sotto prendono i loro. Nessuna riga che uno dei due lati teneva è andata persa
  (controllato riga per riga su `HANDOFF-M3.md` e `08`). **Il codice non cambia** (nessun file fuori da `docs/` fra 71e04df e il
  merge), quindi le suite qui sopra valgono com'erano; l'intervallo della fase mostra ancora solo la fase (41 file), e il branch
  **si unisce senza conflitti con il `main` di oggi** (6704aad, con #190 del maintainer): la PR ha di nuovo la CI. **La CI su
  `4d91517`**, letta una volta a giro finito: `build-test` (21 minuti, 00:00–00:21 UTC) e `core-guard` **verdi**.
- **La coda sotto, la seconda volta** (30 settembre, 00:03 UTC: **#182 è unita**): la nuova cima di A12a (7675de3) — il passo di #187
  dopo #182, con `main` a a004c20 (#182, che il branch aveva già dalla coda, e #190 del maintainer: l'accesso dalla pagina d'errore, la
  versione 0.4.2 e la sua nota), e il nit 1 della revisione di #187 (che cosa ordina una colonna `person`: il commento di `col.person` e
  `UI-GUIDELINES.md`) — è entrata qui con un merge (`7d9a062`), come mi ha scritto la sessione di A12a. **Un conflitto solo**,
  l'intestazione di `HANDOFF-M3.md`: quella di A12b in cima, riscritta; i paragrafi di A12a sotto prendono i loro. Nessuna riga che uno
  dei due lati teneva è andata persa (controllato riga per riga su `HANDOFF-M3.md`, `08`, `UI-GUIDELINES.md` e `columns.ts`). **Questa
  volta il codice cambia** (#190, nel nucleo dell'accesso, con i suoi test), quindi tutte le suite di nuovo, una alla volta, al primo
  giro tranne una caduta del giro completo (sotto): `dotnet build` 0 avvisi; unità **870/870**; **integrazione intera, senza filtro,
  424/424** (i 417 e i 7 di #190); `pnpm lint`, `typecheck`, `format:check` puliti; `i18n:check` **783** chiavi; `pnpm test`
  **582/582** in 78 file; `pnpm gen:api` e `pnpm i18n:sync` senza differenze; le chiavi letterali `training:` **387**, nessuna mancante;
  **`pnpm e2e` 157/157**, sotto il lucchetto di 4173; `dotnet format --verify-no-changes` sui 15 file C# della fase: pulito; le regole
  di `core-guard` in PowerShell sulla fase: nessun file del maintainer, nessuno del nucleo, la nota aggiunta, **PASS**. L'intervallo
  della fase mostra ancora solo la fase (41 file). **`pnpm e2e:full`**, sul banco ricreato e sotto il lucchetto di Mailpit, ogni giro:
  1. **47/48**: è caduto **`template.spec.ts:139`, un test del nucleo** (l'anteprima a tre larghezze): alla riga 185 legge **una volta
     sola, senza aspettare**, quante colonne ha la sezione a finestra larga, e ne ha trovata 1 invece di 2 — la riga 199, per la
     finestra stretta, aspetta con `expect.poll`. Nei due giri di prima (qui sopra) era passato;
  2. quel test da solo, `--repeat-each=5`: **5/5**;
  3. il giro intero di nuovo, sul banco ricreato: **48/48**.

  La fase non tocca l'editor né l'anteprima, e il file è del nucleo: non toccato, detto al revisore. È lo stesso genere di caduta che
  la CI aveva trovato in G19 nello stesso file (`11ddb38`: una misura presa prima che la pagina fosse disegnata).
- **Il passo della coda di #189** (30 settembre, 00:48 UTC: **#187 è unita**; [il commento del master sulla #189][m189]): `main`
  (4e21fbc) è entrato con un merge (`0087db1`) senza conflitti e **senza nessun file**: il suo albero è quello della cima di A12a
  (7675de3), già entrata qui con il merge qui sopra, e l'albero del branch dopo il merge è lo stesso di `7d9a062`, su cui sono girate
  tutte le suite qui sopra — valgono per lui. La PR mostra verso `main` solo la fase (41 file). Via `(after #187)` dal titolo e
  `Queued after #187.` dal corpo. La parte della risposta (a) era già nel codice, con i suoi test, e la nota ha il link alla risposta.
  La PR diventa pronta dopo aver letto una volta la CI verde e i rilievi del master.
- **La revisione del master** ([i suoi rilievi sulla #189][r189], 30 settembre): **approvabile nel merito**, niente di bloccante nel
  codice; lo «should fix» era il conflitto con `main` sull'handoff, cioè il passo della coda qui sopra. **I nit**:
  1. **la forma neutra**: `training:erasure.assigned` diceva «affidati a lui», al maschile; ora «affidati a questa persona come
     trainer: restano affidati a una persona cancellata», e `erasure.exams`, due righe sopra, che aveva lo stesso difetto («in cui è il
     candidato»), dice «in cui questa persona è candidata» (`dd40214`). Le chiavi non cambiano; l'inglese diceva già «them»;
  2. **la parola dell'esito della riga `assigned`** (`Anonymised`, ma quelle righe le tocca il nucleo): **resta**. Nel nucleo vuol dire
     «le righe restano, senza la persona: il suo VID diventa lo pseudonimo» (`IPersonalDataEraser.cs`), che è quello che succede, chiunque
     lo scriva, ed è la parola che il nucleo stesso usa per le righe in cui scrive lo pseudonimo senza che un eraser le tocchi
     (`erasure.lines.named`, `erasure.lines.participation`); `Kept` vuol dire «restano come sono», e non è vero: il VID cambia;
  3. **lo scostamento 3** (le ore e il rating al momento della richiesta): il master lo porta a Carmine;
  4. **le mail del trainer assegnato e del report pubblicato con uno pseudonimo**: **non ci si arriva**, quindi nessun test le può fare
     senza piantare uno stato che l'hub non ha mai: «Assegna» rifiuta un VID sotto 1 e un training aperto non ha mai un trainee
     cancellato (l'eraser cancella i suoi training aperti prima che il nucleo scriva lo pseudonimo); il report nomina chi lo pubblica,
     che è entrato nell'hub. Il «Non verificato» qui sotto ora lo dice.

  Dopo la correzione tutte le suite di nuovo, una alla volta, tutte al primo giro: `dotnet build` 0 avvisi; unità **870/870**;
  **integrazione intera, senza filtro, 424/424**; `pnpm lint`, `typecheck`, `format:check` puliti; `i18n:check` **783** chiavi;
  `pnpm test` **582/582** in 78 file; `pnpm gen:api` e `pnpm i18n:sync` senza differenze; le chiavi letterali `training:` **387**,
  nessuna mancante; **`pnpm e2e` 157/157**, sotto il lucchetto di 4173; **`pnpm e2e:full` 48/48**, sul banco ricreato e sotto il
  lucchetto di Mailpit; le regole di `core-guard` verso `main`: 41 file, nessuno del maintainer né del nucleo, la nota aggiunta,
  **PASS**. Nessun file C# cambia, quindi niente `dotnet format`.
- **Non verificato**:
  - **una cancellazione sul banco**: nessun giro `e2e:full` cancella una persona — i personaggi del banco servono agli altri giri —; le
    pagine con una persona cancellata sono provate nello smoke, con l'API finta, e dalle risposte vere nei test d'integrazione;
  - **le mail del trainer assegnato e del report pubblicato con uno pseudonimo**: passano dallo stesso `TrainingMail.Name` del promemoria,
    ma **non ci si arriva** (nit 4 della revisione, qui sopra): «Assegna» rifiuta un VID sotto 1, un training aperto non ha mai un
    trainee cancellato, e il report nomina chi lo pubblica; nessun test le fa con uno pseudonimo;
  - **la CI dopo il secondo merge e il passo della coda** (qui sopra): verde su `4d91517`; sulla cima nuova la dirà la PR.

[q189]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/189#issuecomment-5898971168
[a189]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/189#issuecomment-5900464516
[m189]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/189#issuecomment-5901860770
[r189]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/189#issuecomment-5901909851

**Com'è andata (A12d)** (30 settembre 2026, branch `m3/a12d-full-round`, PR #191 verso `main`, in bozza in coda dopo #189) — **il giro
completo, il modulo in `FORKING.md` e il rapporto di chiusura; nessuna domanda a Carmine, nessun file del nucleo**:

- **Il branch**: preparato dalla sessione di A12b con `--no-track` dalla cima della coda, `m3/a12b-training-erasure` a 71e04df (la fase
  intera, con la parte decisa da Carmine sulla #189), con `main` a 2af5133. L'intervallo della fase è
  `m3/a12b-training-erasure...m3/a12d-full-round`. **La coda l'ha raggiunto prima della PR**: il passo di #182 dopo #181, con `main` a
  17941c0, è salito per A11b, A12a e A12b (4d91517, solo documenti) ed è entrato qui con un merge (3e5065f), senza conflitti e senza righe
  perse. **Poi, con la PR aperta, il passo di #187 dopo #182** (#182 unita il 30 alle 00:03 UTC, #187 alle 00:48): `main` a 4e21fbc — #182,
  #187 e #190 del maintainer (l'accesso dalla pagina d'errore, 0.4.2) — e il nit 1 della revisione di #187, dalla cima di A12b 4b5a6a9, con
  un merge (91af2e5): un conflitto solo, l'intestazione dell'handoff (quella di A12d in cima, riscritta), e nessuna riga persa. Il codice
  cambia per #190 (`IvaoAuthenticationExtensions.cs`, `AuthenticationTests.cs`, `Directory.Build.props`) e per un commento di `columns.ts`:
  le suite di nuovo, qui sotto. Il branch si unisce senza conflitti con il `main` di oggi (4e21fbc): la PR ha la CI.
- **Classificata prima di scrivere** (`CLAUDE.md` §5): caso **(a)** — una spec del modulo in `web/e2e/full/`, un documento pubblico, una
  nota che conta —; nessun meccanismo nuovo, nessuna scelta da chiedere. Il giro non ha trovato difetti del modulo da correggere.
- **Fatto** (commit `1644f6c` la spec, `1e5f8bf` `FORKING.md`, `815905b` il rapporto, e quello di questi documenti):
  - **il giro completo**, `web/e2e/full/training-the-full-round.spec.ts`: un training ATC del trainee del banco dalla richiesta al report,
    per le pagine vere e ogni passo sulla pagina di chi lo fa, nessuno per l'API — la richiesta su una postazione con la domanda sul
    teorico; lo staff che la trova fra quelle da approvare, la legge con il promemoria e con i testi del trainee, la accetta e la assegna
    al trainer del banco; il trainer che propone due date; il trainee che sceglie la prima fra i riquadri, e la sessione nel calendario
    pubblico senza nomi; a sessione cominciata, la scheda segnata e il report pubblicato; il trainee che lo legge senza le note dello
    staff; lo staff che trova il training nello storico —, e **la mail di ogni passo, una volta sola, a chi è per** (otto mail di sei tipi:
    quattro nessun giro le guardava). Pulisce all'inizio ciò che un giro fermato a metà ha lasciato aperto sul percorso ATC — una sessione
    già cominciata la rischedula e poi la chiude, che non fa aspettare nessuno — e alla fine il suo training, se non è arrivato al report;
    le voci della scheda le scrive per l'API e alla fine le spegne;
  - **`docs/FORKING.md`**: la sezione «The training module» — che cosa fa; chi lo gestisce, con le posizioni del TD come l'hub le legge e
    i `positionGrants` dell'esempio in una tabella, il team di un FIR compreso; le impostazioni con i predefiniti, e `theoryExamUrl` con
    il perché; da dove vengono rating, postazioni e ore; pagine, blocchi, mail e job; che cosa tiene e che cosa porta via una
    cancellazione; che cosa non fa — e lo stato in cima (M3 su `main`, le release sono tag con il loro pacchetto);
  - **il rapporto di chiusura**, `decisions/2026-09-30-m3-review.md`, come quelli di M1 e M2: il conto contro il design, i **26 endpoint
    scritti a mano** accanto a 4 risorse `MapCrud`, per famiglia e con la decisione di ognuno (0 CRUD a mano, 0 eccezioni dichiarate, 0
    endpoint del nucleo), **l'eccezione della nota `le-note-riservate-e-il-trainee` contata** (una funzione, un chiamante, due endpoint,
    tre campi, due test), le estensioni del nucleo, gli scostamenti, il volume, che cosa resta aperto e che cosa portare nel piano;
  - **in questo piano**, l'intestazione `### A9 — Dopo la sessione`, persa in un commit di documenti del 29 settembre (09cfeeb, la
    revisione di #151): il testo della fase c'era, il titolo no.
- **Scostamenti dal piano e dal design**:
  1. **le voci della scheda il giro le scrive per l'API**, non dalla loro schermata: comporre la scheda è il giro di A5, e la catena di §10
     comincia dalla richiesta. Come nel giro del report;
  2. **il giro guarda anche le mail di ogni passo e lo storico dello staff**, che §10 non nomina: sono il «fatto» di §0.1 («partono le mail
     di ogni passaggio», «trainer e staff vedono lo storico»);
  3. **la seconda postazione** offerta, e un report **senza attesa e senza mock exam**: perché le mail di questo giro si riconoscano da
     quelle degli altri ancora in viaggio (l'oggetto nomina la postazione), e perché il percorso ATC resti libero per i giri dopo e per
     questo stesso su un banco sopravvissuto;
  4. **`FORKING.md` dice anche lo stato in cima**: «the only tag is still `v0.1.0-m0`» non era più vero;
  5. **il rapporto misura sul codice di questo branch**, che porta ogni fase, non su `main`, dove A12b non è ancora.
- **Verificato** (30 settembre 2026, in locale, una suite alla volta, sul codice dei due commit, che il merge 3e5065f — solo documenti —
  non cambia):
  - il giro completo da solo, sul banco ricreato (127.0.0.1:5108, `ivaohub_e2e_a12d`) e sotto il lucchetto di Mailpit: **1/1** al primo
    giro, in 3,9 minuti (fino alla scelta della data pochi secondi, poi l'attesa della sessione e delle mail);
  - **la prova che non passa comunque**: con la mail dell'accettazione tolta dal codice (`StaffTrainings.AcceptAsync`, mai in un commit),
    sullo stesso banco, **il giro cade proprio lì** — «requestAccepted to bench-pilot@bench.test» — e tutto il resto passa, anche su un
    banco sopravvissuto al primo giro. Rimesso il file e toccato perché la build lo ricompilasse;
  - `dotnet build IvaoHub.sln` 0 avvisi; `IvaoHub.UnitTests.exe` **870/870**; **`IvaoHub.IntegrationTests.exe` intero, senza filtro,
    417/417** al primo giro; `pnpm lint`, `typecheck`, `format:check` puliti; `pnpm i18n:check` **783** chiavi; `pnpm test` **582/582** in
    78 file; `pnpm gen:api` senza differenze; le chiavi letterali `training:` **387**, nessuna mancante; **`pnpm e2e` 157/157** al primo
    giro, sotto il lucchetto di 4173; **`pnpm e2e:full` 49/49** al primo giro (10,3 minuti), sul banco ricreato e sotto il lucchetto di
    Mailpit — il giro completo e, subito dopo, quello del report, che lui non disturba; nessun test saltato;
    le regole di `core-guard` in PowerShell sulla fase: nessun file del maintainer, nessuno del nucleo, la nota aggiunta, **PASS**.
    Nessun file C# toccato: `dotnet format` non serve;
  - la CI della PR su 252919c: `core-guard` e **`build-test` verdi**, con lo smoke 157 e **`e2e:full` 49**, il giro completo compreso;
  - **dopo il passo della coda di #187** (91af2e5, sul codice con #190), di nuovo una suite alla volta: `dotnet build` 0 avvisi; unità
    **870/870**; **integrazione intera, senza filtro, 424/424** al primo giro (i 417 e i sette casi nuovi di #190 in `AuthenticationTests`);
    lint, typecheck e format puliti, `i18n:check` 783, `pnpm test` **582/582**, `gen:api` senza differenze, le chiavi `training:` 387;
    **`pnpm e2e` 157/157**; **`pnpm e2e:full`** al primo giro **47/49**, con il giro completo e quello del report verdi e due spec del
    nucleo cadute — `contacts.spec.ts:115`, perché il timbro casuale del giro conteneva «new» («munewaq2») e `getByText('New')`, che non è
    esatto, trovava anche l'oggetto del messaggio; `round.spec.ts:204`, scaduta a 30 secondi aspettando un titolo dell'anteprima —; rifatto
    sul banco ricreato, **49/49**. `core-guard` sulla fase: PASS.
- **Non verificato**:
  - **la CI dopo il passo della coda di #187**: su 252919c era verde, con il giro completo; su questa testa la dice la PR;
  - **una cancellazione sul banco**: la prova del registro contato uguale è di A12b, in integrazione
    (`TrainingTraineeTests.AnErasedTraineeLeavesTheRegisterCountedTheSameWithoutTheirTextsAndABanInForce`); cancellare un personaggio del
    banco romperebbe gli altri giri;
  - **il promemoria nel giro**: il job gira ogni quarto d'ora, cinque minuti dopo, e il banco non lo fa partire a comando (A8a, «Trovato»
    n.4).

A12c, se arriveranno i codici di PATS, scriverà qui il suo «Com'è andata».
