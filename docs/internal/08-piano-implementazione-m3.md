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
| A6 | La richiesta | A1, A2, A4 | `trn_trainings`, `trn_bans` (tabella), `/training/request`, i controlli per percorso, il teorico, l'annullamento, `/training/mine` |
| A7 | Accettare, rifiutare, assegnare | A3, A6 | le pagine dello staff, il grant del trainer, il job che lo toglie |
| A8 | Le date | A7 | disponibilità, avvisi, scelta a riquadri, override, calendario, promemoria, chiusura per tempo |
| A9 | Dopo la sessione | A5, A8 | rischedula, no-show, scheda con N/A, report, mock exam, le note riservate e il trainee |
| A10 | Blocchi, pagine pubbliche, percorso, esami, ban | A2, A3, A3b, A9 | i quattro blocchi Data, `/training` e la sessione, il percorso del trainee, `trn_exams`, i ban |
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

### A6c — Nucleo: il suggerimento chiuso tiene la scelta

**Non era nel piano**: l'ha trovata la sessione di A6b il 26 settembre 2026, scrivendo lo smoke della richiesta (PR #144), come A4a fu
trovata scrivendo A4; `dalberone` ha scelto di farla come fase del nucleo a sé (`CLAUDE.md` §0 regola 6). Nota nuova
`2026-09-26-il-suggerimento-chiuso-tiene-la-scelta`, **Proposta**, con la domanda a Carmine in un [commento su #145][q145]. Branch
`m3/a6c-closed-suggestion`, da `main`, PR #145. **Non va in coda**: tocca solo il nucleo del front end e non migra `TrainingDbContext`, quindi la
PR va verso `main` accanto a #143 (A6a) e #144 (A6b), come A3b va avanti per conto suo. Sta qui, prima di A6, come A4a prima di A4.

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

[q145]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/145#issuecomment-5849495355

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

**Com'è andata**: *(a fase chiusa)*

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

**Com'è andata**: *(a fase chiusa)*

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

**Com'è andata**: *(a fase chiusa)*

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

**Com'è andata**: *(a fase chiusa)*

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
