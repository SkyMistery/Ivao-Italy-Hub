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
| A6a | La richiesta: il server — **A6 divisa in apertura** | A1, A2, A4 | `trn_trainings`, `trn_bans` (tabella), i controlli per percorso, il teorico, l'annullamento, la mail, gli endpoint del trainee |
| A6b | La richiesta: le pagine | A6a | `/training/request` con la domanda sul teorico, `/training/mine` con l'annullamento, lo smoke e il giro sul banco |
| A7 | Accettare, rifiutare, assegnare | A3, A6b | le pagine dello staff, il grant del trainer, il job che lo toglie |
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
momento fra le due, in una sessione sua, accanto alle fasi del modulo.
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

**Com'è andata**: *(a fase chiusa)*

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
- **Non verificato**: la CI dopo la correzione dello smoke (la prima corsa è caduta sul toast, sopra; la dirà la PR). **Che i test nuovi
  cadano su una copia indebolita del codice**: non tentato, perché la
  modalità di permessi l'ha rifiutato in A5; i test sono stati letti contro il codice (lo smoke è caduto sulla sua prima versione, ed è
  così che è venuto fuori il difetto del nucleo). **La mail del «sì» in Mailpit sul banco**: il giro non la legge (il test d'integrazione
  di A6a prova l'intento in coda). **Un ban, un'attesa, una soglia di ore, il sito dell'esame e un 409 su un annullamento vecchio,
  attraverso le pagine sul banco**: il banco non ne ha (nessun ban, nessun training completato, nessuna soglia, nessun `theoryExamUrl`);
  le pagine li mostrano con l'API finta dello smoke, e il lato del server è dei test d'integrazione di A6a.

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
     tutte e due la sessione in corso e le date proposte vanno via, e parte la mail `trainingClosed`.
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
     proposta: `chosen_slot_id` resta vuoto, come A6a aveva scritto.
  3. **Le date si propongono insieme**, così il trainee riceve una mail sola, e con `Warn` la conferma è della proposta intera. Le regole
     di una proposta, sulla sua riga: una data ancora da venire, che finisca dopo l'inizio e duri al massimo 12 ore, non già proposta; al
     massimo 10 date ancora da venire in attesa. **Una data si ritira** finché il trainee non l'ha scelta (senza mail): il design non lo
     nomina, ma una data proposta per errore resterebbe altrimenti da scegliere fino all'override.
  4. **«Quel giorno»** sono i giorni che la data tocca **nel fuso della divisione** — una data a cavallo della mezzanotte ne tocca due, una
     che finisce a mezzanotte uno —, come «Eseguito»: il giorno si conta in un posto solo, `DivisionDays`, che ora anche
     `StaffQueue.HeldBefore` usa. **Gli altri training** sono le sessioni già fissate, non le date proposte. **Le voci del calendario** si
     leggono come le legge chi scrive la data (il filtro del nucleo), e una sessione di training non avvisa mai anche come voce.
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
      trainee ne sceglierebbe una che il nuovo trainer non può fare. Una data già fissata resta, come in A7.
  11. **La chiusura e l'override azzerano `reminded_at`**; la chiusura anche la sessione in corso, così il calendario la lascia andare.
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

**Com'è andata**: *(a fase chiusa)*

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

**Com'è andata**: *(a fase chiusa)*

### A12 — Cancellazione, conservazione, archivio di PATS, giro completo

Design §6, §6.1, §7, §8 n.10, §10, §12 n.6 e n.7; note `la-cancellazione-dei-dati-di-un-trainee`, `che-cosa-resta-fuori-da-m3`.
**Quattro PR, in ordine** (la terza solo se c'è di che farla).

- **A12a — nucleo** (branch `m3/a12a-deleted-person-core`), con la sua nota nuova: l'helper «persona cancellata» nel nucleo
  (estensione n.10; oggi `memberName` nel front end dei tour), e `ErasureTests.TheColumnsThatNameAPersonAreTheOnesTheErasureKnows` che
  legge anche `TrainingDbContext`, con le colonne `trn_` nella lista. ⚠️ La copia dei tour **non si tocca**: la sostituisce una
  sessione di Carmine, e la PR lo dice al revisore.
- **A12b — modulo** (branch `m3/a12b-training-erasure`): `TrainingPersonalData : IPersonalDataEraser` con la regola della nota — i
  training chiusi restano con lo pseudonimo e senza testi liberi, quelli aperti e gli esami del candidato si cancellano (i training
  aperti **con il grant con scope del loro trainer**), un ban in vigore resta con `ErasureRequest.Keep` —; «persona cancellata» nelle
  pagine del modulo; **la conservazione**: il registro resta, e le disponibilità vanno via a sessione decisa (già da A8: si verifica
  e si scrive qui).
- **A12c — l'archivio di PATS** (branch `m3/a12c-pats-archive`), **solo se** si ottiene il significato dei codici (n.6): i training di
  `trainingNEW` e gli esami di `exam` in sola lettura sul percorso del trainee, così come sono, da un dump nuovo, **mai nel
  repository**, né in una fixture. Se i codici non arrivano, la parte resta fuori e si scrive qui.
- **A12d — giro completo e chiusura di M3** (branch `m3/a12d-full-round`): `pnpm e2e:full` di tutto il percorso — richiesta →
  accettazione → assegnazione → disponibilità → scelta → report — con i personaggi del banco (A1); `FORKING.md` (il modulo, i
  `positionGrants`, le impostazioni, `theoryExamUrl`); **il rapporto di chiusura di M3** come `decisions/2026-09-07-m1-review.md`, con
  gli endpoint scritti a mano accanto al motore e l'eccezione della nota `le-note-riservate-e-il-trainee` contati (piano §16 punto 6).

**Test**: integrazione della cancellazione (design §10): i conteggi del registro uguali prima e dopo, nessun testo libero rimasto, i
training aperti e gli esami del candidato spariti con i grant dei loro trainer, il ban in vigore rimasto, «persona cancellata» nelle
pagine; `ErasureTests` con le colonne del training. Il giro completo verde.
**Fatta quando**: il giro completo passa in locale e in CI; la cancellazione di un trainee di prova lascia il registro contato uguale;
il rapporto di chiusura è scritto. A M3 chiusa **PATS resta acceso solo per il feed del calendario dei trainer**, fino all'iCal del
nucleo in M6 (nota `che-cosa-resta-fuori-da-m3`).

**Com'è andata**: *(a fase chiusa)*
