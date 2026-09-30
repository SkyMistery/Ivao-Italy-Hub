# IVAO Division Hub — Piano di implementazione di M4 (il modulo Events)

> Documento **interno** (italiano). Fonte di verità: `00-piano-di-progettazione.md` (§13, M4; versione 1.24) e il design
> `09-design-m4.md`, **deciso** da Carmine il 29 settembre 2026 (PR #180, [§17.1 e §17.2][ok], [§17.3][ok3]). Lo scrivono le
> **sessioni di lavoro** di Carmine (`CLAUDE.md` §0), ognuna nel suo worktree; lo legge il master. Qui ci sono l'ordine, il perimetro
> di ogni fase, i test e quando è fatta; **il perché** di ogni scelta sta nel design e nelle dieci note della fase E0. Sotto ogni
> fase, a fase chiusa, **«Com'è andata»** con ogni scostamento dal design (`CLAUDE.md` §9). Scritto nella fase **E0** (29 settembre
> 2026), sul modello di `08-piano-implementazione-m3.md`.

[ok]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/180#issuecomment-5880522987
[ok3]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/180#issuecomment-5880732900

## Regole di tutte le fasi

Per non ripeterle trenta volte:

- **Chi scrive**: **`dalberone`**, il collaboratore, tutta M4, fasi del nucleo comprese (nota `2026-09-30-m4-al-collaboratore`,
  piano 1.25; questa riga diceva «una sessione di lavoro di Carmine»). Una sessione per fase; si ferma a CI verde (`build-test`,
  `core-guard`), segna la PR pronta e **non unisce**: il master la legge e la unisce sul via di Carmine. Scrive «Com'è andata» qui e
  «Che cosa ha lasciato <fase>» in `HANDOFF-M4.md`; **non tocca** la versione dell'hub, il piano né `HANDOFF.md`, che porta il
  master dopo il merge. Le regole assolute del collaboratore sono in `CLAUDE.md` §0.
- **Una fase per sessione, un branch `m4/e<N>-<slug>`, una PR verso `main`** con il template compilato onestamente, compresa la
  sezione «For the reviewer» (`CONTRIBUTING.md`, «Working a phase»): fase e sezioni del design, note, file del nucleo toccati e
  perché, scostamenti, i comandi eseguiti con il risultato, **che cosa non è stato verificato**.
- **Le fasi vanno in coda** (`CONTRIBUTING.md`, «Phases in a queue», con `m4/` al posto di `m3/`): la fase dopo parte dal branch di
  quella prima; la PR va **sempre verso `main`**, in bozza, con `(after #N)` nel titolo e `Queued after #N.` in testa al corpo, e
  «For the reviewer» nomina l'intervallo della fase (`git diff m4/<prima>...m4/<dopo>`). Una correzione sotto sale con un merge,
  mai con un rebase. **Una fase che aspetta una risposta di Carmine non si mette in coda sopra la domanda.**
- **Le fasi del modulo vanno in fila**: migrano tutte `EventsDbContext`, e due fasi che migrano lo stesso contesto non vanno avanti
  insieme. **Le fasi del nucleo** (E1, E10a–E10e, E15a) **possono correre in parallelo** accanto a quelle del modulo, ognuna
  verso `main` quando è pronta, senza coda, purché non migrino anche loro lo stesso contesto (il nucleo); devono essere **unite
  prima** della fase del modulo che le usa, che fino ad allora aspetta.
- **Una fase del nucleo è una PR a sé** (`CLAUDE.md` §0 regola 6, §5 caso (b)), prima del codice del modulo che la usa, e **aggiunge
  una nota nuova** in `decisions/` che dice quale meccanismo si estende e perché il modulo non ne fa a meno. Le note di E0 registrano
  le decisioni; **la forma nel codice è della nota della fase** (la lezione di A0 di M3). Se la forma apre una domanda, la nota è
  «Proposta», la domanda va a Carmine con un commento sulla PR, e il codice che ne dipende aspetta.
  `core-guard` giudica le PR del collaboratore: dal piano 1.25 `core-guard.sh` riconosce come suoi anche i file degli eventi
  (`OWN='[Tt]raining|/[Ee]vents'`: il modulo, i suoi test e le sue spec; `SampleEvents.cs`, il modulo di prova, resta del nucleo),
  e un file del nucleo senza una nota nuova fa cadere il check.
- **I file del maintainer** non li tocca una fase: il piano 00, `HANDOFF.md`, i documenti 00–06 e 09, le note già unite, `CLAUDE.md`,
  `CONTRIBUTING.md`, `.github/`, `.claude/`, `ArchitectureTests.cs`. Un controllo di architettura che riguarda solo gli eventi sta in
  un file di test del modulo (`EventsArchitectureTests`, E2). **Un test condiviso si tocca solo nei due casi in cui il test lo
  chiede**, e la PR lo dice al revisore:
  - **i conteggi dei blocchi** (`web/src/features/admin/uiKit.test.ts`, `DataBlockEndToEndTests`) si alzano di uno per ogni blocco
    nuovo, come ha deciso Carmine per A10 di M3 ([commento sulla #125][c125]) e come dice `CONTRIBUTING.md`;
  - **le colonne che nominano una persona** in `ErasureTests` (da **E2**, che crea `evt_events` con `cancelled_by`): il test legge
    da solo i contesti di ogni modulo (A12a di M3, #187), quindi una fase che aggiunge una colonna `…Vid` o `…By` la scrive nella
    lista, o il test va rosso; esiste per far pensare alla cancellazione di quella colonna.
- **Migrazioni solo additive**, una per fase. L'`Initial` del modulo nasce in **E2** e da lì non si tocca. Una tabella nasce
  **intera** nella fase che la crea, con le colonne che le fasi dopo useranno, così nessuna fase la rimigra (come `trn_trainings` in
  A6 di M3).
- **Test d'integrazione** sulla MariaDB condivisa con **VID `761001–761099`** e **slug `evt-test-…`** (liberi, verificato con un grep
  il 29 settembre 2026; li scrive il master in `CONTRIBUTING.md`, estensione n.8 del design, **prima che E2 sia unita**); grep di un
  VID prima di usarlo; si crea e si toglie nel test. ⚠️ **Nessuno staff dell'ED o dell'MD con un indirizzo email** seminato nei test
  del modulo: `ContactsAndNotificationsTests` afferma i destinatari esatti dell'MD (riga 200) e semina `IT-EC` con un indirizzo (riga
  115). I permessi con grant a un VID; una posizione, quando serve, **senza** indirizzo (come `TrainingSkeletonTests`). ⚠️ **Un grant
  scritto fa rientrare il titolare**: test e spec rifanno il login.
- **Nessuna chiamata a IVAO nei test**: le forme delle risposte si misurano con il token vero in sviluppo, nella fase del nucleo che
  usa l'endpoint, e diventano fixture (`tools/record-ivao-fixtures.mjs`, che oggi registra solo per VID: E10a ed E15a aggiungono le
  loro modalità).
- **Divisione XX**: nessun ICAO, nessuna postazione, nessun rating, nessun tipo di evento, nessuna stringa italiana nei semi del
  modulo, nei predefiniti e nei file di lingua inglesi.
- **I job** seguono le convenzioni di M2 (`[DisallowConcurrentExecution]`, una riga in `hub_jobs_log`, mai un'eccezione, `RunAsync`
  per i test) e la regola della nota `2026-09-28-i-job-quando-passenger-spegne-l-hub` §8: **ognuno decide che cosa fare dai suoi
  dati** («fatto il» sulle righe, l'ultimo giro riuscito), mai dall'ora in cui gira, così un giro perso o doppio non fa danni. ⚠️ Il
  recupero dei giri persi e il `POST` pianificato **non sono ancora nel codice** del nucleo (coda del maintainer): un job degli eventi
  deve essere giusto anche se gira tardi o due volte, e il riepilogo dei validatori scrive da sé il suo «già mandato oggi»
  (`ReviewDigestJob` dei tour non lo ha).
- **Tutto gira in locale prima del push** (`CONTRIBUTING.md`, «Tests»): build, unit, **integrazione intera senza filtro**, `pnpm
  lint`, `pnpm typecheck`, `pnpm test`, e `pnpm e2e:full` quando c'è una schermata; i file generati si rigenerano (`pnpm gen:api`,
  `pnpm i18n:sync`). ⚠️ **Un worktree non ha `tiles/`**: per `pnpm e2e:full` serve un hard link a `tiles/basemap.pmtiles` della
  cartella principale.
- **Ogni scostamento dal design** si scrive sotto «Com'è andata» della fase; se è una decisione, anche in una nota nuova, con la
  domanda a Carmine.

[c125]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/125#issuecomment-5835026941

## Le fasi

**M4a — gli eventi e le prenotazioni** (spegne `ivao-booking`)

| Fase | Titolo | Dipende da | In una riga |
|---|---|---|---|
| E0 | Note di decisione e questo piano — **questa PR** | design deciso (#180) | dieci note sulle decisioni di §17; le fasi qui sotto; `HANDOFF-M4.md` |
| E1 | Nucleo: i tipi del calendario e l'ED sul banco | E0 | `rfe`, `rfo`, `mse`, ~~`onlineDay`~~ `online-day` (piano 1.27) nel seme dei tipi; il personaggio `?as=events` (`IT-EC`) sul banco e2e |
| E2 | Modulo: lo scheletro | E0 | progetto, contesto, `Initial` (`evt_events` intera, `evt_event_airports`), catalogo, `positionGrants`, impostazioni, menu, segmento |
| E3a | L'evento nello staff | E1, E2 | lista e form generati, descrizione, banner, scali e capacità, annullare, eliminare |
| E3b | La vita dell'evento | E3a | pubblicare, l'uscita programmata, la fine; calendario, ricerca, usi dei file; `events-release` |
| E4 | Il pubblico e le rotte | E3b | `/events`, `/events/{slug}`, `events.eventList`; `evt_routes` del FOD |
| E5 | Gli slot pubblici e l'esportazione | E4 | `evt_slots`, incolla e carica con le catene, liste; l'esportazione con il token `events.bookings` |
| E6a | Prenotare: il server | E5 | `evt_bookings`, i verbi, la compatibilità sotto blocco, la rotazione intera, togliere |
| E6b | Prenotare: le pagine | E6a | la lista degli slot con «Prenota», `/events/mine`, `events.myEvents`, il promemoria del giorno prima |
| E7 | Gli slot privati | E6b | il generatore, la prenotazione del privato, la partenza collegata |
| ~~E8a~~ | ~~Nucleo: la cancellazione vede gli eventi~~ — **tolta** | — | A12a di M3 (#187) fa leggere a `ErasureTests` ogni modulo; le righe `evt_` le scrive E2 e chi aggiunge una colonna |
| E8b | «Duplica», la cancellazione, il giro di M4a | E7 | «Duplica» per M4a, `EventsPersonalData` per le righe di M4a, `pnpm e2e:full` di M4a |
| E9 | Fuori dal repository | E8b, la produzione | il Gate Manager legge l'hub (prove su `prova-ponte-rfo`); il primo evento vero; `ivao-booking` spento (Carmine) |

**M4b — l'ATC e il dopo evento**

| Fase | Titolo | Dipende da | In una riga |
|---|---|---|---|
| E10a | Nucleo: le sessioni del tracker senza VID | E0 | `IvaoSessionQuery` con il VID facoltativo, il tipo di connessione, le pagine oltre 200; limiti misurati |
| E10b | Nucleo: le sessioni condivise per VID | E0 | `IAtcActivitySource` con il VID e la storia di un controllore |
| E10c | Nucleo: rating e postazioni della divisione | E0 | il rating preferito per tipo di postazione e il minimo di una postazione; le postazioni della divisione per nominativo, con tipo e FIR |
| E10d | Nucleo: la mail a chi assegna gli award | E0 | un segnale nuovo in coda avvisa chi ha `Awards.Assign`, spegnibile; vale anche per i tour |
| E10e | Nucleo: la distanza fra due aeroporti | E0 | il calcolo sul cerchio massimo passa dal modulo dei tour al nucleo |
| E11a | Postazioni e disponibilità | E8b, E10c | `evt_atc_positions`, `evt_atc_availability`; i grant `firTeam` prendono effetto |
| E11b | La proposta del roster e la correzione | E11a, E10b | `evt_atc_shifts`, il proponente deterministico, `events-roster` alla chiusura, la correzione con gli avvisi |
| E12 | Pubblicazione, mail, cessione | E11b | il roster pubblicato per data, le mail, `/events/{slug}/roster`, i turni in `/me`, `evt_atc_shift_transfers`, `events.atcCoverage` |
| E13a | Dopo l'evento: verifiche e statistiche | E12, E10a, E10b | `evt_event_stats`, `events-after` a lotti: volato, volato senza prenotare, presenze ATC proposte |
| E13b | No-show, registri, limiti | E13a | confermare e togliere i no-show, i due registri, i limiti a chi non vola, `events.staffQueue` |
| E14a | Il PIREP di supporto | E13b, E10e | `evt_reports`, `evt_report_items`, invio, verifica, validazione |
| E14b | Regole di award, PIREP automatici, riepilogo | E14a, E10d | `evt_award_rules`, i segnali, la preferenza, i PIREP `Auto`, `events-digest` |
| E15a | Nucleo: le prenotazioni ATC della rete | E0 | `/v2/atc/bookings/daily` dietro un'interfaccia del nucleo, in lettura |
| E15b | Conservazione, «Duplica» per l'ATC, il giro di M4b | E14b, E15a | `events-retention`, la cancellazione per le righe di M4b, le prenotazioni di IVAO accanto al roster, `pnpm e2e:full` di M4b |

**M4c — gli eventi in presenza** (non dipende da M4b)

| Fase | Titolo | Dipende da | In una riga |
|---|---|---|---|
| E16 | Domande e iscrizione | E8b | `evt_questions`, `evt_registrations`, il form costruito dalle domande, le somme, la mail |
| E17 | Attività parallele | E16 | `evt_activities`, `evt_activity_bookings` sotto blocco, la conservazione breve, «Duplica», il giro di M4c |

**Parallelismo possibile.** Le fasi del nucleo non migrano `EventsDbContext` e vanno avanti accanto al modulo: **E1** accanto a E2 (E3a
ne ha bisogno); **E10a–E10e** già durante M4a, ognuna in una sessione sua (nessuna migra il contesto del nucleo, per quanto si vede
oggi; se due lo migrano, vanno in fila); **E15a** in qualunque momento prima di E15b. (E8a, che doveva seguire E6a, è tolta: sotto, la sua sezione.)
**Se M4c viene prima di M4b**, E16 fa nascere anche `evt_event_stats` (intera, come in E13a, che allora non la migra più) per le
somme delle risposte, ed E17 `events-retention`; `ErasureTests` legge già il contesto degli eventi (da A12a di M3),
e E16 scrive nella lista le sue colonne.
Dalle fasi del modulo in poi tutto migra `EventsDbContext`: **in fila**. **M4c** si mette in coda dopo E8b **oppure** dopo E15b: se
arriva prima un evento in presenza, E16–E17 vanno prima di M4b, e le fasi di M4b si accodano sopra E17 (la regola della conservazione
qui sotto dice chi fa nascere `events-retention`).

**Le divisioni rispetto al design §16** (le ragioni in «Com'è andata» di E0): E3, E6, E11, E13, E14 in due PR (server e forma prima,
pagine o seconda metà dopo, come A6a/A6b di M3); E10 in cinque PR del nucleo, due in più del design (E10c allarga anche le postazioni,
E10e è nuova); E15 in due (il nucleo prima); **E8b**: la cancellazione nasce con M4a (E8a, la sua metà del nucleo, è tolta dopo
A12a di M3).

### E0 — Note di decisione e questo piano

Design §16, §17, §18. Branch `m4/e0-decisions`, PR #184. Documenti, nessun codice.

1. **Dieci note** in `decisions/`, una per decisione o gruppo coerente di §17, ognuna con il link al commento di Carmine che la
   decide e con «Da portare nel piano»:
   - `2026-09-29-i-tre-blocchi-e-che-cosa-resta-fuori-da-m4` — §17.1 n.8 e n.17, §17.2 n.3 (la parte fuori), n.5 e n.7, §17.3 n.5 e
     n.6;
   - `2026-09-29-i-tipi-di-evento` — §17.1 n.2 e n.3, §17.2 n.1;
   - `2026-09-29-chi-lavora-sugli-eventi` — §17.1 n.1, §17.2 n.2;
   - `2026-09-29-gli-slot-e-le-prenotazioni` — §17.1 n.4–n.7 e n.16 (la rotazione intera), §17.3 n.1 e n.2;
   - `2026-09-29-la-vita-di-un-evento` — §17.1 n.14, §17.2 n.4, §17.3 n.4;
   - `2026-09-29-il-roster-atc` — §17.1 n.9, n.10 e n.16 (la penalità), §17.2 n.3 (la lettura), §17.3 n.3;
   - `2026-09-29-dopo-l-evento-e-gli-award` — §17.1 n.11–n.13;
   - `2026-09-29-gli-eventi-in-presenza` — §17.2 n.6, la richiesta nuova;
   - `2026-09-29-le-impostazioni-degli-eventi` — §17.2 n.6, i predefiniti, con quelli di §17.3 n.1 e n.2;
   - `2026-09-29-i-dati-dei-membri-negli-eventi` — §17.1 n.10 (chi vede il registro) e n.15, e la cancellazione (design §11).
2. **Questo piano.**
3. **`HANDOFF-M4.md`**, nuovo: «Che cosa ha lasciato E0».

**Test**: nessuno (documenti). Si controlla che ogni decisione di §17 stia in una nota con il suo link, e che ogni cosa del nucleo che
una fase dà per esistente esista o abbia la sua fase.
**Fatta quando**: il master l'ha letta e Carmine dà il via al merge.

**Com'è andata** (29 settembre 2026, branch `m4/e0-decisions`, PR #184):

- **Dieci note, per trenta decisioni** (§17.1 n.1–n.17, §17.2 n.1–n.7, §17.3 n.1–n.6): le decisioni che si tengono stanno insieme;
  i predefiniti di §17.2 n.6 hanno una nota sola, perché ogni fase ci trovi il suo. Il piano 1.24 porta già quasi tutta la §18 del
  design (il master l'ha portata con #183): ogni nota dice che cosa c'è già nel piano e che cosa resta.
- ⚠️ **Scostamento dal design §16** (come A0 di M3): E0 doveva scrivere anche le note **delle estensioni del nucleo**. Non le scrive:
  ogni fase del nucleo porta la nota del suo meccanismo, con la forma nel codice, e scritte qui ne avrebbe dovuta aggiungere una
  seconda sullo stesso tema. Le note di E0 registrano le decisioni. La riga M4 di §13 del piano dice che E0 scrive «le note di §17
  e delle estensioni del nucleo»: va corretta (nota `i-tre-blocchi…`, «Da portare nel piano»).
- **Scostamenti dall'elenco del design §16**, ognuno con la sua ragione:
  1. **E3, E6, E11, E13, E14 in due PR**: sono le fasi G del design, e ognuna ha una metà che si prova da sola (i verbi e la
     concorrenza di E6a, il proponente di E11b, il job di E13a) e una che si guarda a schermo.
  2. **E10 in cinque PR del nucleo**, una per meccanismo, perché toccano perimetri diversi (il client di IVAO, i dati condivisi, il
     vocabolario, le notifiche, la geografia) e ognuna ha la sua nota; possono andare in parallelo. **E10c** allarga anche la
     directory delle postazioni, **E10e** è nuova (sotto, «Trovato», punti 9 e 10).
  3. **E15 in due**: le prenotazioni ATC della rete sono del nucleo (n.6) e vengono prima; il resto è del modulo.
  4. **La cancellazione nasce con M4a (E8a, E8b; E8a poi tolta dopo A12a di M3)** invece che in E15 — proposta di E0, **decisa da Carmine** il 29 settembre 2026, in chat, come raccomandato («sì, come raccomandi tu», alla domanda della PR #184): M4a va in produzione da solo, e senza `EventsPersonalData` la prenotazione di una persona cancellata resterebbe su uno slot
     di un evento non concluso, sotto uno pseudonimo (nota `i-dati-dei-membri-negli-eventi` §4).
  5. **E1 porta anche il personaggio dell'ED sul banco**, e quindi una nota breve: il seme dei tipi da solo sarebbe senza nota (caso
     (a)), come dice il piano 1.24.
  6. **`evt_events` nasce intera in E2** con tre colonne che il design §1.2 non elenca: i limiti di chi non vola che «l'evento può
     cambiare per sé» (design §3.7) — `restricted_window_hours`, `restricted_max_per_window`, `restricted_max_per_event`, vuote =
     l'impostazione.
- **Trovato leggendo il codice** (29 settembre 2026, `main` a `47e2f70`), e scritto nelle fasi che ne dipendono:
  1. ⚠️ **`core-guard` non giudica le PR del proprietario** (`core-guard.yml`, «Judge them»), e `core-guard.sh` riconosce come modulo
     del collaboratore solo il training (`OWN='[Tt]raining'`): nelle «Regole di tutte le fasi». *Superato dal piano 1.25: M4 la
     scrive `dalberone` e `core-guard.sh` riconosce anche gli eventi.*
  2. **Il seme dei tipi del calendario** (`seed/calendar-kinds/kinds.json`: `event`, `training`, `exam`, `tour`, `meeting`,
     `deadline`) si ricorda chiave per chiave e lascia com'è una chiave scritta a mano (`ContentSeeder.SeedCalendarKindsAsync`):
     `rfe`, `rfo`, `mse`, ~~`onlineDay`~~ `online-day` (piano 1.27) sono un seme e basta (E1). Training li aspetta: `TrainingSettings.ConflictKinds` è `["event"]`
     e il suo commento dice che l'Online Day arriva con M4.
  3. ⚠️ **Il banco e2e non ha staff degli eventi** (`web/scripts/e2e-server.mjs`: il coordinator del web, il pilota, l'assistant del
     FOD, il trainer): E1 aggiunge `?as=events` (`IT-EC`). L'assistant del FOD (`IT-FOAC`) basta per le rotte (E4).
  4. **I `positionGrants` hanno la forma `department` + `levels` + `permission` + `scope`**, e `firTeam` senza dipartimento
     (`DivisionOptions.cs`, `PositionGrantSeed`); **un grant `firTeam` aspetta un'area con righe `IHasFir` e non si ricorda finché
     non si applica** (`PositionGrantSeeder.cs`): E2 li scrive, prendono effetto con E11a. `TrainingArchitectureTests` blocca i grant
     del TD nei due file della divisione: E2 fa lo stesso per gli eventi. `modules.events.baseDepartment: ED` c'è già nei due file.
  5. ⚠️ **`config/division.json` non ha `atcData`**: IT gira con `none`, quindi il roster senza il criterio dell'esperienza e le
     presenze ATC dal tracker, finché la divisione non sceglie `vipi` (una scelta di configurazione di Carmine, non di M4: E11b).
  6. ⚠️ **`IvaoSessionQuery` vuole il VID**, non filtra il tipo di connessione (il DTO non lo porta) e si ferma a 200 sessioni
     (`IvaoTracker.cs`, `IvaoApiClient.cs`): E10a fa più dell'estensione n.2 del design — anche `connectionType` (le presenze ATC
     dal tracker, design §9.3) e le pagine oltre 200 (un aeroporto la sera di un RFE).
  7. **`IAtcActivitySource` non espone il VID**, anche se la vista lo ha (`SharedAtcSession.Vid`), e un modulo non può leggere la
     vista (`ArchitectureTests.NoModuleNamesTheAtcArchiveOrTheBoundaryDataset`): l'estensione n.3 serve (E10b).
  8. ⚠️ **Il vocabolario dei rating non risponde alle due domande del design §1.13**: ha un tipo di postazione solo per ADC (`TWR`),
     APC (`APP`) e ACC (`CTR`), niente per `GND`, `DEL`, `DEP`, `FSS`, e nessun minimo per postazione (`RatingVocabulary.cs`; A1 di
     M3 ha misurato che nessuna postazione di IVAO porta un rating). L'estensione n.4 «se serve» **serve** (E10c).
  9. ⚠️ **`IAtcPositionDirectory` ha solo `ForRatingAsync`**: niente ricerca per nominativo, niente elenco di tutte le postazioni della
     divisione, e il DTO porta il FIR ma non il tipo (`AtcPositionDirectory.cs`). Il design §1.13 lo dava per esistente: **E10c** lo
     allarga, prima di E11a.
  10. ⚠️ **La distanza sul cerchio massimo sta solo nel modulo dei tour** (`GreatCircle.DistanceNm`, `IvaoHub.Modules.FlightOps`), e
     un modulo non ne referenzia un altro: la regola `MinLegDistance` (design §1.9) e la distanza nelle voci del PIREP (§1.8) la
     chiedono al nucleo. **E10e**, nuova: il calcolo passa nel nucleo e i tour lo usano da lì (niente scritto due volte).
  11. **Nessuna mail a chi ha `Awards.Assign`** quando entra un segnale (`CorePermissions`, `AwardEndpoints`): l'estensione n.5 serve
     (E10d). **Nessuna prenotazione ATC nel client** (`IIvaoApiClient`): la n.6 serve (E15a).
  12. **I blocchi**: `uiKit.test.ts` ne conta 38, `DataBlockEndToEndTests` 13 Data, prima dei quattro di A10 di M3. Gli eventi ne
     aggiungono quattro (E4, E6b, E12, E13b), e ogni PR alza i due conteggi di uno da quello che trova su `main`. Le due metà dei
     blocchi di un modulo le controlla `web/src/modules/manifest.test.ts`, non `ArchitectureTests` (che guarda solo i blocchi del
     nucleo). Nessun blocco `eventList` esiste già: il design M1 lo nominava soltanto.
  13. **`ErasureTests.TheColumnsThatNameAPersonAreTheOnesTheErasureKnows` legge solo il contesto del nucleo e quello dei tour**, con
     una lista scritta a mano; **a runtime** invece la cancellazione scorre i contesti di tutti i moduli e scrive lo pseudonimo
     (`PersonalDataErasure`), ma non cancella niente: E8a ed E8b. **Risolto da A12a di M3** (#187, 29 settembre): il test legge i
     contesti di ogni modulo abilitato (l'opzione (c)), quindi vede gli eventi da E2; E8a è tolta, E8b resta.
  14. ⚠️ **L'helper «persona cancellata» non è nel nucleo** (A12a di M3, aperta): c'è solo `memberName` nel front end dei tour, che un
     altro modulo non importa. La prima pagina degli eventi con i nomi è E6b: lì la domanda. **Risolto da A12a di M3** (#187, 29
     settembre): `personName` e `isErased` in `web/src/shared/ui/people.ts`, la parola `people.deleted`, la colonna `col.person`.
  15. **`/me` non ha un seme con i blocchi dei moduli** (`seed/content-pages/me.json`): `events.myEvents` si aggiunge dal back office,
     come `flightops.myTours`. Nessun seme da toccare.
  16. **La finestra dei tour si chiama `ReleaseAt`**, non `visible_from`: la forma è la stessa, il nome del campo degli eventi resta
     quello del design. Il job da copiare è `TourReleaseJob` (dall'inizio dell'ultimo giro riuscito, `ProjectionRefresh`).
  17. **`LiveStatusStrip`** sta nella cornice pubblica e legge il blocco `networkStats` (lo spazio aereo della divisione, senza VID):
     «chi è online sugli scali dell'evento» (design §7.1) non c'è così com'è. Da misurare in E4 (sotto).
  18. **Il nucleo sa già che cosa è «della divisione»** (`IvaoAirspace.Covers` e `Serves`, dal paese della divisione): l'Online Day
     (`whole_division`) non chiede niente di nuovo.
  19. **Un modulo `events` finto** vive in tre test del nucleo (`ModuleCompositionTests`, `staffDestinations.test.tsx`,
     `manifest.test.ts`): da verificare in E2 che non si scontrino con quello vero.
  20. **Lo scope per riga ha la forma `{modulo}:{tipo}:{id}`** (`flightops:tour:{id}`, `training:training:{id}`): `events:event:{id}`
     del design è giusto.
  21. **Le directory degli aeroporti e degli aerei ci sono** (`IAirportDirectory.FindAsync` con le coordinate,
     `IAircraftTypeDirectory.UnknownAsync`); le impostazioni si leggono campo per campo sopra i predefiniti
     (`ModuleSettingsStore.ReadAsync`), quindi un'impostazione nuova arriva anche a un database avviato.
  22. **Nessun bump della versione per E0**: la regola accanto a `<Version>` in `Directory.Build.props` (0.3.0) non conta i documenti.
- **Verificato**: che ogni decisione di §17 stia in una nota con il link al suo commento, e che ogni nota citata esista; i punti qui
  sopra letti nel codice di `main` a `47e2f70`, con `file:riga` nella PR. **Non verificato**: niente da compilare né da provare; la
  CI di una PR di soli documenti la dice la PR; le misure con il token vero (E10a, E10c, E15a) e il Gate Manager (E9) sono delle loro
  fasi.

### E1 — Nucleo: i tipi del calendario e l'ED sul banco

Design §1.2, §8.1, §13 n.1; nota `i-tipi-di-evento`. Branch `m4/e1-calendar-kinds`. **PR del nucleo**, con una nota breve (caso b: un
personaggio del banco, come A1 di M3).

1. **I quattro tipi** in `seed/calendar-kinds/kinds.json` — `rfe`, `rfo`, `mse`, ~~`onlineDay`~~ `online-day` (piano 1.27) —, con le etichette in
   `locales/*/seed.json` come i tipi che ci sono, un colore e un ordine vicino a `event`.
2. **Il personaggio dell'ED** sul banco e2e: `?as=events`, un VID libero del banco (dopo `999004`), posizione `IT-EC`, senza casella
   di Mailpit finché nessuna mail dello staff degli eventi lo chiede. Fino a E2 non ha permessi degli eventi: li porta il seme dei
   `positionGrants`.

**Test**: integrazione: i quattro tipi arrivano anche a un database già avviato, e una chiave scritta a mano resta com'era
(`CalendarKindSeedTests`); il fork «XX» li ha con le etichette inglesi (senza toccare `ForkabilityXxDivisionTests`, che afferma
`tour` e `deadline`). E2e: `?as=events` entra come `IT-EC`.
**Fatta quando**: su un banco nuovo `/api/me` elenca i quattro tipi e il personaggio dell'ED entra.

**Com'è andata** (30 settembre 2026, branch `m4/e1-calendar-kinds`, PR #200, del nucleo senza coda, da `main` a `21da19d`):

- **Fatto** (nota nuova `2026-09-30-i-tipi-degli-eventi-e-l-ed-sul-banco`, scelta tecnica, nessuna domanda nuova):
  - **i quattro tipi** in `seed/calendar-kinds/kinds.json`: `rfe`, `rfo`, `mse`, **`online-day`**, blu come `event` e subito dopo
    (`sort` 11–14); le etichette «RFE», «RFO», «MSE», «Online Day» in `locales/*/seed.json` (chiavi `seed.calendarKinds.rfe`, `.rfo`,
    `.mse`, `.onlineDay`). **Nessun codice del seeder**: si ricorda chiave per chiave e salta una chiave scritta a mano da A2 di M3, e
    il marcatore d'inizializzazione conta anche `seed/`, quindi i quattro arrivano anche a un database già avviato;
  - **il personaggio dell'ED** sul banco: `/e2e/signin?as=events`, VID 999005, «Bench Events», `IT-EC`, nessuna casella, nessun rating
    né ora. In `E2ESignIn` le opzioni dell'assistente dei tour diventano `E2EStaffOptions` (posizioni, nessuna casella) e servono a
    tutti e due; `E2EOptions.Events`, `E2ESignIn.AsEvents`; `web/scripts/e2e-server.mjs` e il README delle spec;
  - **i test**: `CalendarKindSeedTests` (integrazione, scritto in A2) con due casi nuovi — i quattro arrivano accanto agli altri sei, e
    un `rfe` scritto a mano resta com'è mentre gli altri tre arrivano — e gli aiuti resi generali (i due casi di `exam` uguali);
    `CalendarKindsXxDivisionTests` (integrazione, nuovo): il fork «XX», avviato su un database suo (`ivaohub_xx_kinds`), nasce con
    ogni tipo del seme, i quattro compresi, con la **sola** parola inglese del suo file — senza toccare `ForkabilityXxDivisionTests`;
    `CalendarKindSeedFileTests` (unità, nuovo): ogni tipo del seme passa dal validatore del back office, e i quattro ci sono;
    `web/e2e/full/events-bench.spec.ts`: il personaggio entra come `IT-EC`, e il suo `/api/me` dice un membro del solo ED
    (`hasAllDepartments: false`, `departments: ['ED']`) ed elenca i quattro tipi con le parole del seme;
  - `docs/FORKING.md`: le parole del calendario che un fork riceve dal seme, e dove si cambiano.
- ⚠️ **Scostamento dal design (§1.2, §1.12, §8.1, §13 n.1), dalla nota `i-tipi-di-evento` e dal piano (§9.5): la chiave
  dell'Online Day è `online-day`, non `onlineDay`** (nota, §2.2). La chiave di un tipo ha la forma di uno slug
  (`CalendarKindWriteDtoValidator`, `^[a-z0-9]+(?:-[a-z0-9]+)*$`), e il validatore la rilegge **a ogni salvataggio**, anche di una riga
  che c'è già: un `onlineDay` seminato sarebbe una riga che il web master non salva più, nemmeno per cambiarle colore, senza
  rinominarla. `online-day` è la grafia che il back office stesso propone da «Online Day» (`slugify`). **Confermata da Carmine** il
  30 settembre 2026, in chat al master, che l'ha pubblicata sulla PR su sua istruzione ([risposta sulla #200][ok200]). **Le fasi dopo
  scrivono `online-day`**: `kindPresets` (E2), il form (E3a), e i `conflictKinds` del training quando la divisione lo vorrà.
- **Dopo la revisione** ([osservazioni del revisore sulla #200][r200], «approvable», niente di bloccante): la conferma di Carmine qui
  sopra, registrata nella nota, qui e nell'handoff; nessun cambio al codice. Le due osservazioni per dopo — il commento di
  `TrainingSettings.cs:57` e l'elenco dei personaggi del banco in `CONTRIBUTING.md` — sono del training e del maintainer (sotto,
  «Trovato», punti 2 e 3).
- **Scelte piccole, scritte nella nota**: il colore (blu, come `event`: la tavolozza ha tre colori liberi per quattro tipi, e il colore
  di un tipo esiste per raggruppare); il nome «Bench Events», come gli altri personaggi dal loro `?as=`; **un test di unità in più**,
  che `10` non chiedeva: nessun test leggeva `seed/calendar-kinds/` (`ContentSeedTests` legge template e pagine), e il seeder non passa
  dal validatore, quindi `onlineDay` sarebbe partito senza che niente lo dicesse. Provato al contrario: con `onlineDay` nel seme il test
  dice «Key errors.slug.invalid», con un'etichetta che non esiste «Label errors.localized.missing».
- **Trovato, e scritto per chi viene dopo**:
  1. ⚠️ **Il web master del banco (`IT-WM`) raggiunge ogni dipartimento** (`RolePermissionMatrix.ReachesEveryDepartment`) e ha ogni
     permesso di ogni modulo: una spec degli eventi che entra come lui passa con qualunque `positionGrants`. Le spec che provano che
     cosa può l'ED entrano con `?as=events` (dopo il login: il grant scritto fa rientrare, `CONTRIBUTING.md`).
  2. **Il commento di `TrainingSettings.ConflictKinds`** dice che l'Online Day si aggiunge quando M4 ne crea il tipo: ora c'è, ed è
     `online-day`. Il predefinito resta `["event"]` (una divisione lo aggiunge dalle impostazioni del training); il commento è del
     modulo del training, e una PR del nucleo non lo tocca.
  3. **`CONTRIBUTING.md`** («Tests») nomina solo `?as=pilot` e `?as=assistant`: è del maintainer; il README delle spec li elenca tutti
     e cinque.
- **Verificato, in locale** (30 settembre 2026, sul branch prima del commit dei documenti): `dotnet build` senza avvisi; unità
  **880/880**; **integrazione intera senza filtro 428/428** (6,8 minuti), le due classi toccate da sole 5/5, e
  `CalendarKindsXxDivisionTests` rossa togliendo l'inglese di `rfe` da `locales/en/seed.json` («… which the English file lacks»), poi
  il file rimesso; `dotnet format --verify-no-changes` sui quattro file C#; `pnpm lint`, `typecheck` (che comprende `e2e/`),
  `format:check`, `i18n:check` verdi; `pnpm test` 591 in 79 file; `pnpm gen:api` e `i18n:sync` senza differenze; `pnpm e2e
  --workers=2` **161/161** al primo giro, dietro il lock dello smoke; **`pnpm e2e:full` 50/50 al primo giro** su un banco suo
  (`http://127.0.0.1:5111`, `ivaohub_e2e_e1` tolto prima, dietro il lock di Mailpit), la spec nuova compresa; le regole di
  `core-guard` rifatte in PowerShell su `git diff origin/main...HEAD`: nessun file del maintainer, otto del nucleo, la nota nuova.
- **Non verificato**: la CI (la dice la PR); i quattro tipi su un'installazione vera già avviata (il test lo rifà sul database
  d'integrazione, con il seme passato due volte); le etichette italiane nel browser oltre «una parola, non una chiave» (le afferma
  esatte il test d'integrazione); i permessi degli eventi del personaggio, che arrivano con E2; `pnpm e2e:full` con la mappa di base,
  che non c'è né nella cartella principale né negli altri worktree (le spec tollerano il 404 di `/tiles/`, come in CI).

[ok200]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/200#issuecomment-5912372176
[r200]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/200#issuecomment-5912327906

### E2 — Modulo: lo scheletro

Design §0.4, §1.1, §1.2, §1.3, §1.12, §6; note `chi-lavora-sugli-eventi`, `le-impostazioni-degli-eventi`, `i-tipi-di-evento`. Branch
`m4/e2-events-skeleton`.

1. **`IvaoHub.Modules.Events`** (referenzia solo `Core`), `EventsDbContext : ModuleDbContext`, registrato con la chiave `events`
   (`__EFMigrationsHistory_events`), in `IvaoHub.Web/Modules.cs`, nel `.sln`, nell'host e nei test di unità. **Migrazione
   `Initial`** con:
   - **`evt_events` intera** (design §1.2): `slug` univoco, `kind`, i cinque interruttori, `organizer`, `external_url`, `title` e
     `summary` tradotti, `body_json`, `banner_media_id`, le quattro date (`visible_from_utc`, `booking_opens_at_utc`, `starts_at_utc`, `ends_at_utc`), `has_roster` e `shift_minutes`, `in_person` e `venue`
     tradotto, `status` e `published_at`, `visibility`, `cancelled_at`, `cancelled_by`, `cancellation_note` tradotta,
     `roster_proposed_at`, `after_done_at`, **i tre limiti per l'evento** (scostamento 6 di E0), `row_version`, e le colonne del
     nucleo (maschera, audit);
   - **`evt_event_airports`**: `event_id`, `icao`, `ordinal`, `max_movements_per_hour`, `max_arrivals_per_hour`,
     `max_departures_per_hour`.
   L'entità evento è `IOwnedByDepartment` (maschera, `ModuleBaseDepartment.Keep`), `IVisible`, `IPublishable`, `IAuditable`,
   `[Audited]`, `IHasResourceScope` (`events:event:{id}`); la proiezione arriva in E3b.
2. **`web/src/modules/events/`** con il manifest, la sezione «Events» nella barra dello staff (`/staff/events`, per ora le
   impostazioni), i18n `events` in `it` e `en` (`pnpm i18n:sync`); `web/src/modules/index.ts`.
3. **Il catalogo** delle cinque aree (design §6.1), `DeniedToStakeholder` su `EventAtc.Edit` ed `EventReports.Edit`; **i
   `positionGrants`** della nota `chi-lavora-sugli-eventi` §2.4 in `config/division.json` e in `config/division.example.json`, con
   `scope: ED`: ED `Coordinator`/`Assistant` tutto **tranne `EventReports.Edit`** (dei PIREP solo `View`: li valida l'MD, design §6.2);
   ED `Advisor` come la nota; AOD, FOD e MD a tutti i loro livelli (`Coordinator`,
   `Assistant`, `Advisor`); lo staff dei FIR con `firTeam: true` su `EventAtc.*`, che prende effetto in E11a.
4. **`EventsSettings`** con i campi di M4a — `kindPresets` (chiavi che esistono nei tipi del calendario, come `conflictKinds` del
   training), `bookingGapMinutes` (10), `pilotRetentionMonths` (24), `reminderLeadHours` (24) —, dietro `Events.ManageSettings`,
   schermata generata. Predefiniti che non conoscono la divisione.
5. **Il segmento riservato** `events`.
6. **`EventsArchitectureTests`**, sul modello di `TrainingArchitectureTests`: il modulo non nomina la rete né vIPI, non scrive rating
   né ICAO, non chiama nessuno fuori dall'hub; i due file della divisione danno all'ED e a chi collabora quello che dice il design.
7. **Le due verifiche del design §6.3**, prima di scrivere E3a: come le schermate delle aree figlie (`EventAtc`, `EventRoutes`,
   `EventReports`) leggono l'intestazione dell'evento per chi non ha `Events.View` (lo staff dei FIR); e come lo staff di un FIR con
   `own` vede le disponibilità per le **sue** postazioni. La forma si scrive qui sotto; se chiede il nucleo, ci si ferma (nota,
   domanda, fase del nucleo).
8. **I moduli `events` finti** dei test del nucleo (E0, «Trovato», punto 19): se uno si scontra con quello vero, si cambia il test del
   nucleo in una PR a sé, con la nota.

**Test**: integrazione: i grant arrivano una volta e raggiungono la sessione (posizioni seminate **senza** indirizzo); chi ha
`ManageSettings` cambia un'impostazione e la rilegge, chi non l'ha no, un valore fuori limite rifiutato sul campo, un tipo che non
esiste rifiutato in `kindPresets`; il fork «XX» parte con il modulo e i predefiniti vuoti (in un test del modulo). Unit: predefiniti e
regole. E2e: `?as=events` vede la sezione, salva un'impostazione e la rilegge.
**Fatta quando**: il personaggio dell'ED vede la sezione Events, cambia un'impostazione e la rilegge.

**Com'è andata**: *(a fase chiusa)*

### E3a — L'evento nello staff

Design §1.1–§1.3, §2.3, §6.1, §7.2; note `i-tipi-di-evento`, `la-vita-di-un-evento`, `chi-lavora-sugli-eventi`. Branch
`m4/e3a-event-staff`. Nessuna migrazione.

1. **`MapCrud`** dell'evento con la policy del dipartimento; **`/staff/events`**, lista generata con le viste Bozze, Prossimi, In
   corso, Conclusi, Annullati — lo **stato dalle date** in una funzione sola, come `TourState`.
2. **Il form generato** (`/staff/events/{id}`): `kind` dai tipi del bootstrap, e gli interruttori **preimpostati da `kindPresets`** al
   cambio del tipo; `organizer` ed `external_url`; titolo e riassunto tradotti; la descrizione con l'editor dei blocchi, come l'editor
   del tour; il banner con `MediaPicker`; le date; la visibilità. I campi di M4b e M4c (`has_roster`, `in_person`…) entrano nel form
   nelle loro fasi.
3. **Gli scali con la capacità**: righe figlie nell'editor, lista e form generati, validati con `IAirportDirectory.FindAsync`;
   maschera e scope copiati dall'evento (`CrudOptions.BeforeAuthorize`, come le leg). Un evento `whole_division` non ne ha.
4. **Annulla** (`Events.Edit`): `cancelled_at`, `cancelled_by`, la nota tradotta; il tipo di notifica `eventCancelled` dichiarato (i
   destinatari arrivano con le righe dei membri: E6a, E12, E16). **Elimina** (`Events.Delete`, `DeletePolicy`): solo senza righe dei
   membri — la regola cresce con ogni tabella dei membri.

**Test**: integrazione: l'ED crea e modifica; FOD e AOD non modificano il testo; chi collabora non elimina; EC elimina una bozza
vuota; un ICAO sconosciuto rifiutato sulla sua riga; `kindPresets` applicati. Unit: lo stato dalle date, ai bordi. E2e: il personaggio
dell'ED crea un RFO con due scali, lo riapre, lo annulla.
**Fatta quando**: sul banco un RFO con due scali e la capacità si crea, si riapre, si annulla; una bozza vuota si elimina.

**Com'è andata**: *(a fase chiusa)*

### E3b — La vita dell'evento

Design §2.1, §2.2, §2.4, §8.1, §8.4 (`events-release`); nota `la-vita-di-un-evento`. Branch `m4/e3b-event-life`. Nessuna migrazione.

1. **Pubblica** (`Events.Edit`) con `Refusals`: titolo e riassunto in tutte le lingue della divisione, `visible_from ≤ booking_opens ≤
   starts < ends`, gli scali per un evento a slot, il link per un evento di altri.
2. **`IProjectable`**: una voce di calendario dalla visibilità alla fine, con il tipo dell'evento e la sua visibilità, verso
   `/events/{slug}`; nessuna voce per bozza, non ancora visibile, annullato o concluso; la ricerca per gli eventi pubblici visibili e
   non conclusi; **gli usi dei file** (banner e immagini della descrizione) fino alla fine + 7 giorni (`MediaUseProjection`).
3. **`events-release`**, ogni 15 minuti: riproietta gli eventi diventati visibili o conclusi dall'inizio dell'ultimo giro riuscito,
   come `TourReleaseJob`.
4. Il tipo di notifica `eventChanged` (orari cambiati), dichiarato; i destinatari arrivano con le righe dei membri.

**Test**: integrazione: i rifiuti di «Pubblica» campo per campo; la voce compare dopo `visible_from` con il job (`RunAsync`,
orologio spostato) e sparisce dopo la fine; annullato senza voce; la ricerca; la scadenza degli usi dei file; il job due volte, e dopo
un giro perso, fa la stessa cosa.
**Fatta quando**: un evento pubblicato con l'uscita fra un'ora non è nel calendario; dopo l'uscita sì; dopo la fine no.

**Com'è andata**: *(a fase chiusa)*

### E4 — Il pubblico e le rotte

Design §1.4, §2.4, §7.1, §7.3 (`events.eventList`); note `la-vita-di-un-evento`, `chi-lavora-sugli-eventi`. Branch
`m4/e4-public-and-routes`.

1. **`/events`**: i prossimi eventi e quelli in corso, come schede, filtri per tipo e scalo, e il `CalendarView` sugli stessi eventi;
   nessun archivio.
2. **`/events/{slug}`**: banner, titolo, date in UTC e nell'ora della divisione, tipo, organizzatore, scali, rotte, descrizione; **404
   a chi non è staff dopo la fine**; un annullato con la nota. ⚠️ **«Chi è online sugli scali»** il giorno dell'evento (E0, «Trovato»,
   punto 17): se il blocco `networkStats` non si restringe agli scali dell'evento senza toccare il nucleo, la parte resta fuori e si
   scrive qui, o diventa una fase del nucleo con la sua nota.
3. **Il blocco `events.eventList`**, nelle due metà; i due conteggi si alzano di uno.
4. **`evt_routes`** (migrazione): `departure_icao`, `arrival_icao`, `route`, `remarks`; area `EventRoutes`, lista e form generati nella
   scheda «Rotte» dell'evento; le scrive il FOD.

**Test**: integrazione: il 404 al visitatore dopo la fine, la pagina allo staff; un evento `Members` nascosto ai visitatori; il FOD
scrive le rotte e non l'evento; il blocco per un visitatore. Smoke: `/events` e la pagina. E2e: l'assistant del FOD aggiunge una
rotta, il visitatore la vede.
**Fatta quando**: un visitatore vede l'evento pubblicato in `/events` e sulla sua pagina con la rotta del FOD; dopo la fine, 404.

**Com'è andata**: *(a fase chiusa)*

### E5 — Gli slot pubblici e l'esportazione

Design §1.5, §3.1, §7.4; note `gli-slot-e-le-prenotazioni`, `i-tre-blocchi…` (l'esportazione in M4a, mai di una bozza). Branch
`m4/e5-public-slots`.

1. **`evt_slots` intera** (migrazione), per pubblici e privati: `kind`, `event_airport_icao`, `is_arrival`, `callsign`,
   `flight_number`, `aircraft_types` (JSON), `departure_icao`, `arrival_icao`, `off_block_utc`, `on_block_utc`, `stand`,
   `rotation_code`, `rotation_leg`, `generated`, `row_version`; univoco `(event_id, callsign, off_block_utc)`. Area
   `EventBookings`, maschera e scope dall'evento.
2. **Incolla e carica** (`EventBookings.Edit`): testo separato da tabulazioni o CSV, con l'intestazione del design §3.1; tutto o
   niente, i rifiuti per riga con `Refusals` (`rows[12].aircraft_types`); i tipi di aereo con `IAircraftTypeDirectory.UnknownAsync`;
   il verso dedotto dagli ICAO; **le catene** (ordine, aeroporto che coincide, uno scalo dell'evento ogni due tratte,
   `bookingGapMinutes` fra una tratta e l'altra); aggiunge, o sostituisce i pubblici liberi. Una transazione, un endpoint.
3. **Lista e form generati** per le correzioni; «elimina i liberi». La lista pubblica degli slot sulla pagina dell'evento, in sola
   lettura (libero o preso, rotazioni raggruppate); «Prenota» arriva in E6b.
4. **L'esportazione** `GET /api/events/{slug}/bookings/export`, con l'audience **`events.bookings`** (`TokenAudienceDescriptor` con
   `EventBookings.View`): i campi del design §7.4, orari UTC; un evento in bozza non si esporta.

**Test**: unit: il lettore (tabulazioni, CSV con le virgolette, righe vuote), le catene caso per caso, il verso. Integrazione: tutto o
niente con i rifiuti per riga; l'esportazione con il token `events.bookings`, senza token, con un'altra audience; una bozza non
esportata. E2e: il personaggio dell'ED incolla una tabella con una rotazione e la vede sulla pagina.
**Fatta quando**: una tabella incollata crea gli slot con le rotazioni, e l'esportazione li legge con un token personale.

**Com'è andata**: *(a fase chiusa)*

### E6a — Prenotare: il server

Design §1.6, §3.3, §3.5, §3.6, §10.1; nota `gli-slot-e-le-prenotazioni`. Branch `m4/e6a-booking-server`.

1. **`evt_bookings` intera** (migrazione): `event_id`, `slot_id` **univoco**, `booker_vid`, `aircraft_icao`, `callsign`, `other_icao`,
   `other_time_utc`, `paired_booking_id`, `flown_at`, `flown_session_id`, `flown_checked_at`, `unflown_excused_by`,
   `unflown_excused_note`, `reminded_at`, `created_at`. `ISubmittedByMembers`, `IHasStakeholder`, `IVisible = Members`, `[Audited]`.
2. **I verbi del pilota**: prenota uno slot pubblico (la finestra, l'EOBT futuro, l'aereo fra quelli ammessi, la **compatibilità**
   con `bookingGapMinutes` sotto un `SELECT … FOR UPDATE` sulla sua prima prenotazione dell'evento o sulla riga dell'evento);
   **prenota tutta la rotazione** (le tratte libere e compatibili in una transazione, e quali erano già prese); **ritira** fino
   all'EOBT (la riga si cancella). L'indice univoco violato risponde «slot appena preso da un altro pilota». I verbi dei privati sono
   di E7.
3. **Lo staff toglie** una prenotazione con un motivo (`EventBookings.Edit`), mail `bookingRemoved`.
4. **Le regole che crescono**: uno slot prenotato non si elimina, e «sostituisci» dell'import lo lascia; un evento con prenotazioni
   non si elimina; annullare avvisa chi ha prenotato (`eventCancelled`), cambiare gli orari anche (`eventChanged`).
5. **Il membro legge le sue righe** dai suoi endpoint; nessun `IHasParticipants`.

**Test**: integrazione: **due prenotazioni dello stesso slot nello stesso istante: una vince**; **due prenotazioni incompatibili dello
stesso pilota nello stesso istante: una vince**; a evento in corso con l'EOBT futuro sì, passato no; il ritiro dopo l'EOBT rifiutato;
la rotazione intera con una tratta già presa; lo staff toglie e la mail parte; un membro non legge le prenotazioni degli altri;
l'esportazione ha `booked_by`. Unit: la compatibilità nei due sensi, ai bordi.
**Fatta quando**: i test della concorrenza passano sulla MariaDB vera, in locale e in CI.

**Com'è andata**: *(a fase chiusa)*

### E6b — Prenotare: le pagine

Design §3.3, §3.8, §7.1, §7.2, §7.3 (`events.myEvents`); nota `gli-slot-e-le-prenotazioni`. Branch `m4/e6b-booking-pages`.

1. **La lista degli slot** sulla pagina dell'evento: libero o preso, **mai chi**; filtri (arrivi o partenze, orario, tipo di aereo,
   compagnia, rotazione); **«Prenota»** con la scelta dell'aereo; **«Prenota tutta la rotazione»**; il conto alla rovescia
   all'apertura.
2. **`/events/mine`**: le mie prenotazioni, anche passate, con «ritira».
3. **Il blocco `events.myEvents`** nelle due metà (`signedIn: false` a un visitatore), i conteggi più uno; si mette in `/me` dal back
   office, come `flightops.myTours`.
4. **La scheda «Prenotazioni»** dello staff: lista generata, «togli» con il motivo. **I nomi di una persona cancellata**
   (E0, «Trovato», punto 14) con l'helper del nucleo di A12a di M3 (#187, unita): `personName` nelle pagine, `isErased` prima di un
   link, `col.person` nella lista; mai una copia degli eventi.
5. **`events-reminders`**, ogni 15 minuti: **`bookingReminder`** `reminderLeadHours` prima dell'EOBT, le prenotazioni vicine dello
   stesso evento in una mail sola, con la rotta del FOD se c'è; una volta sola (`reminded_at`). Tipo di notifica spegnibile.

**Test**: integrazione: il promemoria una volta per prenotazione, raggruppato, con la rotta; il job ripetuto non rimanda; il blocco
per un visitatore. Smoke: la pagina a un visitatore mostra «preso» senza nomi. E2e: il pilota prenota una rotazione intera, la vede in
`/events/mine`, ritira una tratta; lo staff toglie una prenotazione.
**Fatta quando**: sul banco il pilota prenota una rotazione, la ritrova in `/events/mine` e in `/me`, ne ritira una tratta, e il
promemoria arriva una volta in Mailpit.

**Com'è andata**: *(a fase chiusa)*

### E7 — Gli slot privati

Design §1.3, §3.2, §3.4, §7.4; nota `gli-slot-e-le-prenotazioni`. Branch `m4/e7-private-slots`. Nessuna migrazione (le colonne ci
sono da E5 ed E6a).

1. **«Genera gli slot privati»** (`EventBookings.Edit`): per scalo e per ora della finestra di prenotazione, il posto libero è la
   capacità meno i pubblici (per verso, o in totale se la capacità è in movimenti); i privati a intervalli regolari, lontano dagli
   orari dei pubblici; rigenerare sostituisce i privati liberi, quelli prenotati restano e contano. Un MSE ha tutta la capacità
   privata.
2. **Prenotare un privato**: callsign, aereo, l'altro aeroporto (`IAirportDirectory`) e il suo orario; la compatibilità usa l'orario
   allo scalo e l'altro orario. **La partenza collegata**: un arrivo sceglie anche una partenza privata dallo stesso scalo, e le due
   prenotazioni nascono insieme (`paired_booking_id`). ⚠️ **Il ritiro di una delle due** il design non lo dice: in apertura di E7 la
   domanda a Carmine, con la raccomandazione «ritirarne una scioglie il legame, l'altra resta».
3. **L'esportazione** porta i privati con il gate vuoto e `paired_slot_id`.
4. **La pagina**: i privati per scalo, verso e ora, con il form del volo.

**Test**: unit: il generatore (capacità per verso e in totale, pubblici che la consumano, intervalli regolari, rigenerazione che non
tocca i prenotati). Integrazione: la partenza collegata tutta o niente; la compatibilità di un privato; `paired_slot_id`
nell'esportazione. E2e: lo staff genera i privati di un RFO, il pilota prenota un arrivo con la partenza collegata.
**Fatta quando**: sul banco i privati si generano dalla capacità e un arrivo con la partenza collegata esce nell'esportazione con lo
stesso gate da assegnare.

**Com'è andata**: *(a fase chiusa)*

### E8a — Nucleo: la cancellazione vede gli eventi

~~PR del nucleo che faceva leggere `EventsDbContext` a `ErasureTests`.~~ **Tolta** (piano 1.25, dal master su incarico di Carmine,
nota `2026-09-29-la-persona-cancellata-nel-nucleo`): A12a di M3 (#187) ha scelto l'opzione (c), e il test legge da solo i contesti di
ogni modulo abilitato, quindi anche quello degli eventi. Le prime righe `evt_` della lista le scrive **E2**, che crea `evt_events` con
`cancelled_by` (senza, il test va rosso), e poi ogni fase che aggiunge una colonna di persona (regole di tutte le fasi). Il vecchio
punto 2, mettersi in pari con A12a, non serve più: A12a è unita. La sezione resta perché i collegamenti a E8a non si rompano; le
altre fasi non si rinumerano.

### E8b — «Duplica», la cancellazione, il giro completo di M4a

Design §2.3-bis, §11.1, §15; note `la-vita-di-un-evento`, `i-dati-dei-membri-negli-eventi`. Branch `m4/e8b-duplicate-and-m4a-round`.

1. **«Duplica»** (`Events.Edit`): la nuova data d'inizio e un nuovo slug; una **bozza** con tutti gli orari spostati della stessa
   differenza; copia titolo, descrizione, banner, scali con la capacità, rotte e, **a scelta**, gli slot pubblici con le rotazioni;
   niente dei membri; i privati si rigenerano. Un endpoint, una transazione.
2. **`EventsPersonalData : IPersonalDataEraser`** per le righe di M4a: le prenotazioni di eventi non conclusi si cancellano (lo slot
   torna libero); quelle di eventi conclusi restano con lo pseudonimo, senza callsign, altro aeroporto e altro orario di un privato;
   quello che la persona ha fatto come staff resta con lo pseudonimo.
3. **Il giro completo di M4a** (`pnpm e2e:full`): un RFO, gli slot incollati, i privati generati, la pubblicazione; il pilota prenota
   una rotazione e un privato, ne ritira uno; l'esportazione letta con un token.
4. **`docs/FORKING.md`**: il modulo, i `positionGrants` dell'ED e di chi collabora, `kindPresets`.

**Test**: integrazione: «Duplica» sposta tutti gli orari della stessa differenza, crea una bozza, non copia nessuna riga dei membri;
la cancellazione di un pilota di prova (una prenotazione aperta sparita, una chiusa con lo pseudonimo e senza i dati del volo). Il
giro completo verde.
**Fatta quando**: il giro completo di M4a passa in locale e in CI, e un RFE si duplica su una nuova data.

**Com'è andata**: *(a fase chiusa)*

### E9 — Fuori dal repository: il Gate Manager, il primo evento, `ivao-booking` spento

Design §7.4, §17.2 n.7; nota `i-tre-blocchi…`. Nessuna PR in questo repository.

1. **Il Gate Manager legge l'hub**: il cambio è un lavoro del suo repository, con un token personale `events.bookings` e l'identità
   `slot_id`; le prove sull'evento **`prova-ponte-rfo`**, mai su un evento vero.
2. **Il primo evento vero fatto sull'hub**, con il Gate Manager già passato.
3. **`ivao-booking` spento** e il **301 verso `/events`**: lo fa Carmine.

⚠️ **Prima di E9**: l'hub **in produzione** (piano §15 punti 2c e 3: il nome e il posto della produzione non sono ancora decisi) e, nel
nucleo, **il recupero dei giri persi dei job** (nota `2026-09-28-i-job-quando-passenger-spegne-l-hub`): senza, su un Passenger che
dorme, il promemoria del giorno prima può non partire.
**Fatta quando**: `booking.it.ivao.aero` risponde con il 301 e il Gate Manager legge le prenotazioni dall'hub.

**Com'è andata**: *(a fase chiusa)*

### E10a — Nucleo: le sessioni del tracker senza VID

Design §5.1, §9.1, §13 n.2; nota `dopo-l-evento-e-gli-award`. Branch `m4/e10a-tracker-without-vid`. **PR del nucleo**, con la sua
nota.

1. **`IvaoSessionQuery`** con il VID **facoltativo**: le sessioni per aeroporto (`departureId`, `arrivalId`) e finestra, a pagine
   (la documentazione dice `perPage` fino a 100, il client ne chiede 50, «what the API was measured to accept», `IvaoTracker.cs`: la
   misura è di E10a) **oltre il tetto di oggi di 200**, con un limite dichiarato da chi chiama (il job lavora a lotti).
2. **Il tipo di connessione** (`connectionType`: `PILOT`, `ATC`) come filtro e nel DTO (E0, «Trovato», punto 6).
3. **Misurato con il token vero**, prima del codice: se il token dell'applicazione basta a ogni lettura, i limiti di chiamate, la forma
   di una pagina senza VID. Le fixture con una modalità nuova di `tools/record-ivao-fixtures.mjs`, con le persone tolte.

**Test**: unit sul lettore con le fixture (pagine, pagina vuota, errore a metà); integrazione: i tour, che usano il VID, invariati.
**Fatta quando**: le sessioni di un aeroporto in una finestra si leggono a pagine, e le misure sono nella nota.

**Com'è andata**: *(a fase chiusa)*

### E10b — Nucleo: le sessioni condivise per VID

Design §4.3, §4.5, §9.3, §13 n.3; nota `il-roster-atc`. Branch `m4/e10b-shared-sessions-by-vid`. **PR del nucleo**, con la sua nota.

1. **`IAtcActivitySource`** con il VID nelle presenze e una domanda nuova: le sessioni di un controllore in un periodo, per postazione
   (l'esperienza, design §4.3) e in una finestra (la presenza in un turno, §4.5). La vista `v_share_atc_sessions` ha già `vid`;
   con `atcData.source: none` la risposta è «non disponibile», come oggi.
2. Il modulo continua a non nominare la vista (`ArchitectureTests`).

**Test**: integrazione sulla vista di prova (come quelle di `IAtcActivitySource` oggi); `none` risponde «non disponibile».
**Fatta quando**: la storia di un controllore per postazione si legge dal nucleo.

**Com'è andata** (30 settembre 2026, branch `m4/e10b-shared-sessions-by-vid`, PR #208, del nucleo senza coda, da `main` a `c107c98`):

- **Fatto** (nota nuova `2026-09-30-le-sessioni-condivise-per-vid`, scelta tecnica, nessuna domanda nuova), tutto in
  `src/IvaoHub.Core/Atc/`:
  - **il VID nelle presenze**: `AtcPresence.Vid` (`int?`), una proprietà `init` sotto il costruttore, letta dalla colonna `vid` della
    vista (`UserId` di vIPI); `null` dove l'archivio non lo dice. Non esce dai tour, che costruiscono le loro risposte campo per campo
    (`AtcContactDto`, `AgentAtcPresenceDto`): nessun cambio del contratto OpenAPI;
  - **la domanda nuova** `IAtcActivitySource.SessionsOfAsync(vid, fromUtc, toUtc)`: le connessioni di un controllore aperte
    nell'intervallo, nella forma della domanda di T12 (`AtcActivity`), per i due usi del design — un anno per l'esperienza (§4.3,
    `Of(callsign)` è una postazione), l'ora di un turno per la presenza (§4.5, `Covers` separa «non c'era» da «non si sa»); chi ha
    coperto la postazione è la domanda di T12 con i VID;
  - `VipiAtcActivitySource` legge le due domande con **una query** sulla vista (quella di T12, con il filtro su `vid`);
    `UnavailableAtcActivitySource` risponde `null` a tutte e due;
  - `AtcActivitySourceTests` (integrazione, nuovo): la vista scritta come la migrazione di vIPI, **in un database suo**, con all'utente
    dell'hub solo il `SELECT` sulla vista; l'ora di un turno e chi ha coperto, un anno per postazione con la copertura per metà, un VID
    mai visto, `none`, un archivio scritto prima di E10b, la vista sparita, la stringa di connessione mancante.
- ⚠️ **Scostamento: una correzione che la fase non chiedeva** (nota, §4; un commit a sé, `fix(core): …`). Con `atcData: vipi` e
  `ConnectionStrings:AtcData` non ancora nei segreti **l'hub non partiva**: il contesto della vista arrivava dal costruttore della
  sorgente, quindi si costruiva fuori dal suo `try`, e all'avvio la sorgente la costruisce il seeder dei contenuti (attraverso i
  fornitori dei blocchi Data dei tour, fino ad `AtcProposer`). La classe prometteva «non disponibile» e il suo `catch` aspettava già
  quell'eccezione: ora il contesto si chiede dentro il `try` (`IServiceProvider`, come `PersonalDataErasure`). La prova nuova cade con
  la sorgente di prima, all'avvio dell'host. La nota la tratta come quello che è: il comportamento già scritto, non una decisione nuova.
- **Scelte piccole, scritte nella nota**:
  1. la domanda nuova ha **un corpo predefinito nell'interfaccia** che risponde «non disponibile», come
     `IIvaoApiClient.GetAtcPositionsAsync` (A2 di M3): senza, il doppio di `AtcContactsTests` (del maintainer) non compilerebbe;
  2. `Vid` è una proprietà sotto il costruttore e `int?`: il costruttore di T12 non cambia, e lo 0 è già l'installazione nelle colonne
     di persona;
  3. **un controllore per domanda**: vIPI indicizza `(UserId, StartUtc)` (letto nel suo modello, `Digital_vIPI`, il 30 settembre);
  4. **il tipo di una postazione non passa** da qui: lo dice la directory di E10c; la colonna `position` della vista e il `SessionId`
     restano fuori finché una fase non li chiede.
- **Trovato, e scritto per chi viene dopo**:
  1. ⚠️ **Un host dei test d'integrazione senza `useIvaoFixtures` chiede un token a IVAO** quando `ref_ivao_centers` è vuota
     (`HubPipeline`, la sincronizzazione all'avvio): 400, ma cinque chiamate vere a ogni avvio finché la tabella resta vuota — nel primo
     giro della classe da sola, a ogni suo host. `AtcActivitySourceTests` passa `useIvaoFixtures: true`; l'host di prova in generale è
     del nucleo e fuori da questa fase (nella PR, per il revisore).
  2. **E10b non tocca `Core/Ivao/`**, che «Per chi prende M4» dell'handoff metteva fra le sue: l'archivio è `Core/Atc/`.
  3. **Il `SessionId` della vista è l'id della sessione di IVAO** (vIPI lo copia dal whazzup, `IvaoWhazzupClient`): se E14a vorrà
     scrivere quale sessione ha trovato per un turno, è lo stesso id del tracker, qualunque sia la fonte.
- **Verificato, in locale** (30 settembre 2026, sul branch prima del commit dei documenti): `dotnet build` senza avvisi, anche
  `--no-incremental`; unità **885/885**; **integrazione intera senza filtro 435/435** (8,7 minuti; 5 nuove), sul codice dei primi due
  commit — il terzo cambia solo un commento —; `AtcActivitySourceTests` da sola 5/5, con le fixture nessuna chiamata a IVAO (senza,
  cinque per host); le controprove: senza il filtro su `vid`, e senza il VID nelle presenze, cadono le due prove del VID (2 rosse su 4
  ogni volta); la prova della correzione cade con la sorgente di prima, all'avvio dell'host; `dotnet format --verify-no-changes` sui
  tre file C#; `pnpm lint`, `typecheck`, `format:check`, `i18n:check` verdi; `pnpm test` **594 in 80 file**; `pnpm gen:api` senza
  differenze; le regole di `core-guard` rifatte in PowerShell dalla merge base: nessun file del maintainer, due del nucleo
  (`Core/Atc/`), la nota nuova — passa.
- **Non verificato**: la CI (la dice la PR); la vista vera di vIPI su `itivao_atc` (il test la riscrive dalla migrazione di vIPI, e la
  prova e la produzione non hanno ancora `atcData`); il costo di `SessionsOfAsync` sul server condiviso (l'indice c'è nel modello di
  vIPI, non l'ho misurato sul server); `pnpm e2e` e `pnpm e2e:full`, non girati perché nessuna schermata cambia.

### E10c — Nucleo: rating e postazioni della divisione

Design §1.13, §4.1, §4.3, §13 n.4; nota `il-roster-atc`. Branch `m4/e10c-ratings-and-positions`. **PR del nucleo**, con la sua nota.

1. **Il vocabolario dei rating** risponde alle due domande del design §1.13: **il rating preferito per un tipo di postazione** (c1:
   TWR e GND almeno ADC, APP almeno APC, ACC almeno ACC; la nota dice `DEL`, `DEP`, `FSS`, `ATIS`) e **il minimo di una postazione**.
   ⚠️ Nessuna postazione di IVAO porta un rating (misurato in A1 di M3): la nota dice da dove viene la regola; se non è una regola
   pubblicata di IVAO, è una domanda a Carmine prima del codice.
2. **`IAtcPositionDirectory`** con le postazioni della divisione per **nominativo**, con **tipo** e **FIR** (E0, «Trovato», punto 9):
   la scelta delle postazioni di un evento e il FIR delle righe `IHasFir`.

**Test**: unit sul vocabolario (i tipi, i bordi, un rating sconosciuto); integrazione sulla directory con le fixture delle postazioni.
**Fatta quando**: il vocabolario dice il preferito e il minimo, e la directory trova una postazione della divisione per nominativo.

**Com'è andata**: *(a fase chiusa)*

### E10d — Nucleo: la mail a chi assegna gli award

Design §5.4, §8.3, §13 n.5; nota `dopo-l-evento-e-gli-award`. Branch `m4/e10d-award-assigner-mail`. **PR del nucleo**, con la sua
nota.

1. **Un tipo di notifica del nucleo**: quando entra un segnale nuovo nella coda degli award, chi ha `Awards.Assign` riceve una mail;
   spegnibile dal profilo; **vale anche per i tour**. Una mail per segnale, o un riepilogo: lo dice la nota, con la domanda a Carmine
   se la forma non è ovvia.

**Test**: integrazione: un segnale di un tour e uno di prova avvisano chi assegna, non chi l'ha spento, una volta.
**Fatta quando**: un segnale nuovo arriva nella casella di chi assegna.

**Com'è andata**: *(a fase chiusa)*

### E10e — Nucleo: la distanza fra due aeroporti

Design §1.8, §1.9 (`MinLegDistance`); nota `dopo-l-evento-e-gli-award`. Branch `m4/e10e-great-circle-core`. **PR del nucleo**, con la
sua nota (caso b: un pezzo usato in due posti si scrive una volta).

1. **Il calcolo sul cerchio massimo** (`GreatCircle` del modulo dei tour) passa nel nucleo, accanto alle coordinate di
   `IAirportDirectory`; i tour lo usano da lì, e i loro test non cambiano.

**Test**: gli unit dei tour, verdi; unit del nucleo sulla distanza fra due aeroporti noti.
**Fatta quando**: i tour e il nucleo hanno un calcolo solo.

**Com'è andata**: *(a fase chiusa)*

### E11a — Postazioni e disponibilità

Design §1.7, §4.1, §4.2, §6.3; note `il-roster-atc`, `chi-lavora-sugli-eventi`. Branch `m4/e11a-positions-and-availability`. Dopo E8b
e E10c.

1. **`evt_atc_positions`** (migrazione, insieme alla tabella dopo): `callsign` dalla directory (E10c), `fir`, `from_utc`, `to_utc`
   (predefinita la finestra dell'evento), una nota; `IHasFir`, area `EventAtc`. Con la prima riga `IHasFir` dell'area **i grant
   `firTeam` del seme prendono effetto** all'avvio.
2. **`evt_atc_availability`** con le finestre (righe figlie `from_utc`, `to_utc`) e una nota; `controller_vid` stakeholder,
   `ISubmittedByMembers`.
3. **La scheda ATC** dello staff: postazioni (lista e form generati) e disponibilità **per postazione** (la forma decisa in E2, punto
   7); `has_roster` e `shift_minutes` entrano nel form dell'evento.
4. **La disponibilità del membro** sulla pagina dell'evento, fino a inizio − `applicationsCloseDays`, con un rating almeno
   `minimumAtcRating`; la cambia o la ritira fino alla chiusura.
5. **Le impostazioni dell'ATC**: `shiftMinutes`, `minimumAtcRating` (dal vocabolario), `maxConsecutiveShifts`, `breakShifts`,
   `applicationsCloseDays`, `rosterPublishDays`, `experienceMonths`.

**Test**: integrazione: AOD e capo FIR scrivono le postazioni e non il testo dell'evento; **con `own` un capo FIR solo le postazioni
del suo FIR**; la disponibilità rifiutata sotto il rating minimo e dopo la chiusura; il grant `firTeam` applicato al primo avvio con
l'area `IHasFir`. E2e: il pilota del banco (AS3) dà la disponibilità.
**Fatta quando**: sul banco l'AOD (con un grant) apre due postazioni e il pilota dà la sua disponibilità.

**Com'è andata**: *(a fase chiusa)*

### E11b — La proposta del roster e la correzione

Design §4.3, §4.4 (la correzione); nota `il-roster-atc`. Branch `m4/e11b-roster-proposal`. Dopo E11a e E10b.

1. **`evt_atc_shifts` intera** (migrazione): `position_id`, `controller_vid`, `from_utc`, `to_utc`, `origin`, `notified_at`,
   `attendance`, `attendance_by`, `attendance_at`, `attendance_note`; `IHasFir` dalla postazione, stakeholder il controllore.
2. **Il proponente**: una funzione pura e deterministica, con il perché di ogni turno — i turni a pezzi di `shift_minutes`, chi può,
   chi prima (rating preferito, esperienza da E10b se c'è, penalità, meno turni, candidatura prima), i turni con meno candidati per
   primi; scoperti in evidenza. La penalità si calcola dai turni passati (zero finché E13b non ha no-show).
3. **`events-roster`**, prima metà: alla chiusura delle candidature propone una volta (`roster_proposed_at`).
4. **La correzione** (`EventAtc.Edit`): il roster come lista generata per postazione e ora, con il perché e gli avvisi; aggiungi
   (anche chi non si è candidato, purché sia in `hub_users`), sposta, togli; un'aggiunta fuori dalle regole è un avviso.

⚠️ **`atcData` di IT** (E0, «Trovato», punto 5): con `none` il criterio dell'esperienza non c'è. Se IT deve passare a `vipi` prima del
primo roster vero, è una scelta di Carmine: la domanda in apertura di E11b.
**Test**: unit: il proponente su dati finti (chi può, rating preferito e ripiego, esperienza, penalità, turni sparsi, pause,
determinismo, il perché). Integrazione: il job propone una volta, ripetuto non raddoppia; la correzione con gli avvisi; chi non è mai
entrato non si aggiunge. E2e: lo staff sposta un turno.
**Fatta quando**: sul banco il roster proposto compare alla chiusura, con il perché, e lo staff lo corregge.

**Com'è andata**: *(a fase chiusa)*

### E12 — Pubblicazione, mail, cessione, `events.atcCoverage`

Design §4.4, §4.4-bis, §7.1, §7.3; nota `il-roster-atc`. Branch `m4/e12-roster-publication`.

1. **La pubblicazione per data** (inizio − `rosterPublishDays`), seconda metà di `events-roster`: `atcShiftAssigned` una volta per
   turno (`notified_at`); **dopo**, ogni turno aggiunto, spostato o tolto manda subito `atcShiftAssigned`, `atcShiftChanged` o
   `atcShiftRemoved`.
2. **`/events/{slug}/roster`**: tutti i turni, i nomi solo a chi ha fatto il login. **I turni in `/events/mine`** e in
   `events.myEvents`.
3. **`evt_atc_shift_transfers`** (migrazione): turno, chi cede, a chi (anche nessuno), nota, stato; `ISubmittedByMembers`,
   stakeholder chi cede. Lo staff approva (mai la propria) o rifiuta; `atcShiftTransferDecided` a chi cede e `atcShiftAssigned` al
   nuovo titolare; la presenza si verifica sul nuovo titolare.
4. **Il blocco `events.atcCoverage`** nelle due metà, i conteggi più uno; annullare e cambiare gli orari avvisano anche chi ha un
   turno.

**Test**: integrazione: il roster pubblicato alla data giusta, una mail per turno, una volta; una modifica dopo la pubblicazione
avvisa; la cessione approvata passa il turno e nessuno approva la propria; la pagina del roster senza nomi a un visitatore.
**Fatta quando**: sul banco, spostando l'orologio, il roster si pubblica e il pilota trova il suo turno e la mail.

**Com'è andata**: *(a fase chiusa)*

### E13a — Dopo l'evento: le verifiche e le statistiche

Design §1.10, §4.5 (la proposta), §5.1, §10.2; note `dopo-l-evento-e-gli-award`, `il-roster-atc`. Branch `m4/e13a-after-checks`.
Dopo E12, E10a ed E10b.

1. **`evt_event_stats`** (migrazione, se E16 non l'ha già fatta nascere): i numeri del design §1.10, per sempre.
2. **`events-after`**, ogni ora, **a lotti** che finiscono dentro una richiesta, da `after_done_at` e `flown_checked_at`: per ogni
   prenotazione le sessioni del pilota fra partenza e arrivo (`flown_at`, il callsign non conta); **chi ha volato senza prenotare**
   (E10a), solo il numero; i turni con le sessioni (E10b, o il tracker ATC per VID) → presente o **no-show proposto**, con chi ha
   coperto la postazione; le statistiche; «ricalcola» per lo staff.
3. **La scheda «Statistiche»**; l'impostazione `attendanceMinimumMinutes`.

**Test**: integrazione con le fixture: un lotto che riprende al giro dopo; la sorgente che non risponde lascia le righe da
verificare; nessun VID di chi ha volato senza prenotare salvato; le statistiche ricalcolate uguali.
**Fatta quando**: dopo un evento di prova con le fixture, le cinque liste del design e le statistiche sono giuste.

**Com'è andata**: *(a fase chiusa)*

### E13b — No-show, registri, limiti, `events.staffQueue`

Design §3.7, §4.5, §7.2 (`/staff/events/controllers`), §7.3; note `il-roster-atc`, `gli-slot-e-le-prenotazioni`. Branch
`m4/e13b-registers`. Nessuna migrazione (le colonne ci sono da E6a ed E11b).

1. **I no-show**: lo staff conferma o giustifica (`EventAtc.Edit`, mai sul proprio turno); **toglie un no-show a mano**, anche dopo,
   con una nota (`Excused`, l'audit tiene chi e quando).
2. **Il registro dei controllori** (`/staff/events/controllers`, e il proprio in `/events/mine`) con la penalità del design §4.5.
3. **Il registro dei piloti**: le prenotazioni non volate, «togli» dello staff con una nota; **i limiti** di chi supera
   `unflownThreshold` nei verbi di E6a e di E7, sotto lo stesso blocco, con i limiti dell'evento se li ha; il rifiuto dice perché e
   fino a quando.
4. **Le impostazioni**: `noShowWeight`, `unflownThreshold`, `restrictedWindowHours`, `restrictedMaxPerWindow`,
   `restrictedMaxPerEvent`.
5. **Il blocco `events.staffQueue`** nelle due metà (no-show da confermare, cessioni da decidere; i PIREP entrano in E14a), i
   conteggi più uno.

**Test**: unit: la penalità (0,08 e 0,25); integrazione: nessuno conferma il proprio no-show, superadmin compreso; un no-show tolto
resta tolto e il registro lo mostra; il pilota oltre la soglia non supera i limiti per fascia e per evento; una voce tolta non conta.
**Fatta quando**: sul banco un no-show proposto si conferma, si toglie, e il registro lo racconta.

**Com'è andata**: *(a fase chiusa)*

### E14a — Il PIREP di supporto

Design §1.8, §5.2, §5.3; nota `dopo-l-evento-e-gli-award`. Branch `m4/e14a-support-reports`. Dopo E13b ed E10e.

1. **`evt_reports`** e **`evt_report_items`** (migrazione): univoco `(event_id, vid, kind)`; `ISubmittedByMembers`, stakeholder il
   membro, area `EventReports`.
2. **L'invio** da `/events/mine` entro `reportDays`: pilota con i voli, ATC con i turni; uno per tipo per evento; correggibile finché
   non è deciso. L'impostazione `reportDays`.
3. **La verifica** a ogni invio: per ogni voce la sessione, i minuti, la distanza (E10e); `Valid`, `Partial`, `Invalid`,
   `Unavailable`.
4. **La validazione** (`EventReports.Edit`, mai il proprio): accetta o rifiuta con una nota, `reportDecided`; la scheda dello staff
   con la verifica di ogni voce; i PIREP da validare in `events.staffQueue`.

**Test**: integrazione: nessuno valida il proprio (superadmin compreso); l'MD valida e non tocca il resto; la verifica con le
fixture, e `Unavailable` a sorgente spenta; oltre `reportDays` rifiutato.
**Fatta quando**: sul banco un PIREP pilota verificato viene accettato da chi ha il permesso.

**Com'è andata**: *(a fase chiusa)*

### E14b — Regole di award, PIREP automatici, riepilogo

Design §1.9, §1.11, §5.1 (i PIREP `Auto`), §5.4, §8.3; nota `dopo-l-evento-e-gli-award`. Branch `m4/e14b-award-rules`. Dopo E14a ed
E10d.

1. **`evt_award_rules`** (migrazione) nell'editor dell'evento (`Events.Edit`), criteri dall'elenco chiuso del design §1.9.
2. **Accettare valuta le regole** sulle voci verificate: un `AwardSignalProjection` per regola soddisfatta, nella stessa transazione.
3. **La preferenza** «riporta automaticamente la mia partecipazione agli eventi» (`IModule.Preferences`, spenta); **`events-after`**
   crea i PIREP `Auto` per chi l'ha (per un evento `whole_division`, gli aeroporti e le postazioni della divisione dal nucleo).
4. **`events-digest`**, una volta al giorno: `reportsToValidate`, con il suo «già mandato oggi».

**Test**: unit: ogni criterio; integrazione: un PIREP accettato proietta i segnali delle regole soddisfatte e nessun altro; i PIREP
automatici solo per chi ha la preferenza; il riepilogo una volta al giorno anche con due giri.
**Fatta quando**: sul banco un PIREP accettato mette un segnale nella coda degli award, e chi assegna riceve la mail (E10d).

**Com'è andata**: *(a fase chiusa)*

### E15a — Nucleo: le prenotazioni ATC della rete

Design §9.1, §13 n.6, §17.2 n.3; nota `il-roster-atc`. Branch `m4/e15a-network-atc-bookings`. **PR del nucleo**, con la sua nota.

1. **`/v2/atc/bookings/daily?date=&position=`** dietro un'interfaccia del nucleo, **in lettura**, alla richiesta (niente job); la forma
   misurata con il token vero e registrata come fixture.

**Test**: unit sul lettore con la fixture; l'errore di IVAO risponde «non disponibile».
**Fatta quando**: le prenotazioni di un giorno per una postazione si leggono dal nucleo.

**Com'è andata** (30 settembre 2026, branch `m4/e15a-network-atc-bookings`, PR #207, del nucleo senza coda, da `main` a `c107c98`,
accanto a E2 ed E10a–E10e partite lo stesso giorno; dopo la revisione, fusa con `main` a `c98b272`):

- **Fatto** (nota nuova `2026-09-30-le-prenotazioni-atc-della-rete`, scelta tecnica, nessuna domanda nuova):
  - **misurato con il token vero, prima del codice** (nota §2), con uno script usa e getta che non stampava né nomi né VID: il token
    dell'applicazione basta, senza scope (senza token, 401); un array nudo di 20–70 prenotazioni al giorno per tutta la rete (68 il
    giorno più pieno degli ultimi sessanta); una prenotazione è di un aeroporto (`atcPosition`) o di un settore (`subcenter`), mai tutte
    e due; **il giorno elenca ogni prenotazione che lo tocca**, e una a cavallo della mezzanotte sta in tutti e due i giorni; **`position`
    è il principio del nominativo**; circa 370 chiamate in un quarto d'ora senza un 429 e senza intestazioni di limite;
  - **l'unico client esteso**: `IIvaoApiClient.GetDailyAtcBookingsAsync` (con «non disponibile» predefinito, per i doppi dei test),
    `IvaoApiClient` (il token dell'applicazione, `ReadOrNothingAsync`, nessuna cache), `FixtureIvaoApiClient`, e **un lettore solo**,
    `IvaoAtcBookingReader` (`IvaoAtcBookings.cs`), che del membro tiene il VID;
  - **la domanda del modulo**, `IAtcBookingSource.BookedAsync(fromUtc, toUtc, callsign?)` (`AtcBookingSource.cs`, in `Core/Ivao/`, con
    un nome che non nomina IVAO), con `AtcBookingDto` e `AtcBookingKind`: le prenotazioni che si sovrappongono alla finestra, una volta
    sola, di tutte le postazioni o di un nominativo intero; `null` = non disponibile; al più sette giorni;
  - **lo strumento** (`tools/record-ivao-fixtures.mjs --bookings`, con la persona tolta) e **la fixture** `atc-bookings-day.json`: le dieci
    prenotazioni di un giorno vero sulle stazioni del banco, più l'esame di `EDDF_APP` e `SBGR_TWR` a cavallo della mezzanotte, VID
    761070–761079; il suo paragrafo nel README delle fixture;
  - **i test**: `AtcBookingTests` (unità, 25 casi): il lettore sul giorno registrato e sulle righe strane, il client vero contro un IVAO
    finto (sei risposte sbagliate, IVAO irraggiungibile e il token rifiutato rispondono «non disponibile»), il client delle fixture su un
    giorno qualunque, la sorgente dalla DI del nucleo (la finestra, la mezzanotte una volta, il nominativo intero, un giorno che non
    risponde, la settimana, la finestra vuota).
- ⚠️ **Scostamenti dalla lettera della fase**, ognuno con la sua ragione (nota §3.2, §4):
  1. **La domanda del modulo è una finestra, non un giorno.** «Le prenotazioni di un giorno per una postazione» sono la finestra di
     quel giorno con quel nominativo; una domanda per giorno avrebbe messo nel modulo che IVAO elenca per giorno di UTC e ripete le
     prenotazioni a cavallo della mezzanotte. Il giorno di IVAO com'è resta nel client (`GetDailyAtcBookingsAsync`).
  2. **Una postazione chiesta è il nominativo intero**, non il principio come per IVAO: con «LIRR» IVAO dà tre settori.
  3. **Il client delle fixture ripete il giorno registrato su ogni data**, come `whazzup.json`, invece di rispondere solo a quel giorno:
     il banco senza credenziali ha delle prenotazioni accanto a un evento di qualunque data (E15b).
  4. **Oltre sette giorni di UTC la sorgente risponde «non disponibile»** con un avviso nel log, invece di chiedere o di lanciare: una
     data sbagliata di un evento non deve far cadere la pagina.
- **Dopo la revisione** ([rilievi del revisore sulla #207][r207], «approvable»; [risposta di Carmine][ok207]):
  1. **Carmine conferma nessuna cache e al più sette giorni per richiesta**, così com'è (in chat al master, che l'ha pubblicata sulla
     PR su sua istruzione): la pagina del roster legge le prenotazioni fresche a ogni apertura e, con IVAO lento, aspetta e poi dice che
     non sono disponibili, mentre il resto del roster funziona. Scritto nella nota (lo stato, §3.1, §3.2, «Da portare nel piano») e
     nell'handoff.
  2. **Da correggere, la fixture**: teneva l'`id` della prenotazione, il `createdAt` e il giorno vero, che con l'API pubblica di IVAO
     ritrovano la persona. Lo strumento ora toglie `id` e `createdAt` e **sposta il giorno sul 1° gennaio 2001** (IVAO non ha
     prenotazioni prima del 2023, misurato), con gli orari del giorno: il rilievo chiedeva i primi due, ma anche il giorno con il
     nominativo ritrova la prenotazione con `/daily` (nota §3.3, §4). Registrata di nuovo: le stesse dieci prenotazioni, confrontate
     campo per campo con la prima, e il giorno vero non è più scritto nei documenti. ⚠️ **La prima registrazione resta nella storia del
     branch** (`fbb11ac`; il giorno anche nei testi di `fbb11ac` e `43528ea`): fuori da `main` la tiene solo un merge a squash, e la
     PR lo dice al maintainer.
  3. **I due nit**: lo strumento non cade più su un `user` che manca; il lettore legge solo l'array nudo del giorno, e la forma a
     pagine di `/v2/atc/bookings` (`{ items, … }`) è «non disponibile».
  4. **`main` fuso** (E10b, #208; mai un rebase): i conflitti solo in `HANDOFF-M4.md`, risolti tenendo il paragrafo di ogni fase; `10`
     si è fuso da sé.
- **Verificato, in locale** (30 settembre 2026):
  - `dotnet build` della soluzione senza avvisi; `dotnet format --verify-no-changes` sui sette file C# toccati;
  - unità **910/910**; la classe nuova 25/25 al primo giro; **integrazione intera senza filtro 430/430** (8,3 minuti, Docker acceso,
    la macchina carica di sette sessioni), al primo giro;
  - **il codice vero contro IVAO vero**, una volta, con un programma usa e getta fuori dal repository che usa `IvaoApiClient` e prende
    `IAtcBookingSource` dalla DI del nucleo (stampava nominativi, orari e conteggi, mai un VID): il giorno registrato ha 34 prenotazioni
    e **le dieci della fixture ci sono tutte** (nominativo, orari, tipo); `position=lirr` dà i tre settori di Roma; la finestra dalle
    22 alle 2 dà `SBGR_TWR` **una volta sola**, e un'altra che comincia alle 00:00 del giorno dopo; `LIRR` come nominativo, niente; le
    prossime 24 ore, 27 prenotazioni in ordine d'inizio; otto giorni, «non disponibile» senza chiedere;
  - **provato al contrario**: sei mutazioni, ognuna presa dal test che deve — senza `Distinct`, e con la sovrapposizione inclusiva, cade
    la mezzanotte; con il nominativo per principio, il nominativo intero; il fallimento come lista vuota nel client fa cadere otto casi,
    nel lettore due; il client delle fixture senza il giorno prima, uno —; poi i file rimessi, ricompilati, 25/25;
  - in `web/` (nessun file toccato): `pnpm lint`, `typecheck`, `format:check`, `i18n:check` verdi, `pnpm test` 594 in 80 file, `pnpm
    gen:api` senza differenze;
  - le regole di `core-guard` rifatte in PowerShell su `git diff --name-status c107c98...HEAD`: nessun file del maintainer, i file del
    nucleo con la nota nuova;
  - **dopo la revisione e il merge di `main`** (E10b), sull'ultimo commit: `dotnet build` senza avvisi, `dotnet format` pulito sui due
    file C# cambiati; unità **910/910**, la classe 25/25; **integrazione intera 435/435** (7,9 minuti, al primo giro; i cinque in più
    sono di E10b); il test nuovo della fixture provato al contrario sulla prima registrazione (cade, con i due che leggono il giorno),
    poi 25/25; la fixture nuova confrontata campo per campo con la prima (le stesse dieci prenotazioni, spostate); in `web/` le stesse
    verifiche verdi, 594 test in 80 file, `pnpm gen:api` senza differenze; le regole di `core-guard` rifatte su `git diff
    --name-status origin/main...HEAD`: nessun file del maintainer, i file del nucleo con la nota.
- **Non verificato**: la CI (la dice la PR); il client dentro l'hub avviato (nessun endpoint: la schermata è di E15b; il codice sì,
  contro IVAO vero, qui sopra); IVAO giù sul serio (provato con un IVAO finto: stati, corpo che non è JSON, rete che non risponde,
  token rifiutato), e quanto aspetta una pagina prima di «non disponibile» con la resilienza vera (fino a 30 secondi per chiamata); i
  limiti di chiamate oltre le ~370 della misura; `pnpm e2e` ed `e2e:full`, perché nessuna schermata cambia.

[r207]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/207#issuecomment-5916573282
[ok207]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/207#issuecomment-5916738183

### E15b — Conservazione, «Duplica» per l'ATC, il giro completo di M4b

Design §2.3-bis, §4.4 (le prenotazioni di IVAO), §11, §15; note `i-dati-dei-membri-negli-eventi`, `il-roster-atc`. Branch
`m4/e15b-retention-and-m4b-round`.

1. **`events-retention`**, una volta al mese: prenotazioni e PIREP dei piloti dopo `pilotRetentionMonths`, le disponibilità a roster
   pubblicato e a evento finito; turni, esiti, PIREP ATC e cessioni per sempre. (Se M4c è venuta prima, il job c'è già da E17: qui si
   allarga.)
2. **`EventsPersonalData`** per le righe di M4b (disponibilità e turni di eventi non conclusi cancellati; il registro con lo
   pseudonimo), e le colonne nuove in `ErasureTests`.
3. **«Duplica»** copia anche postazioni con le finestre e regole di award.
4. **Le prenotazioni di IVAO accanto al roster** (E15a), alla richiesta.
5. **Il giro completo di M4b**: disponibilità, roster proposto, corretto, pubblicato; PIREP di supporto, validato, segnale.
6. **`docs/FORKING.md`** per l'ATC; **il rapporto di chiusura di M4** se M4c è già chiusa (come `decisions/2026-09-07-m1-review.md`,
   con gli endpoint scritti a mano contati per famiglia, piano §16 punto 6).

**Test**: integrazione: la conservazione (le righe dei piloti cancellate dopo il periodo, turni e statistiche no); la cancellazione
di un controllore di prova (il registro contato uguale); «Duplica» per l'ATC. Il giro completo verde.
**Fatta quando**: il giro completo di M4b passa in locale e in CI.

**Com'è andata**: *(a fase chiusa)*

### E16 — Domande e iscrizione

Design §4-bis.1, §4-bis.2, §11; nota `gli-eventi-in-presenza`. Branch `m4/e16-in-person-registration`.

1. **`evt_questions`** e **`evt_registrations`** (migrazione): le domande con il tipo dall'elenco chiuso, le scelte tradotte,
   obbligatoria, ordine; l'iscrizione univoca `(event_id, vid)` con le risposte, `ISubmittedByMembers`, stakeholder il membro.
2. **`in_person`** e **`venue`** entrano nel form dell'evento; domande nell'editor (`Events.Edit`).
3. **Il form del membro** è `SchemaForm` con lo schema costruito dalle domande; **il server valida** le risposte secondo il tipo;
   iscriversi, cambiare, ritirarsi fino all'inizio; tutto in `/events/mine`; `registrationReceived`; annullare e cambiare gli orari
   avvisano anche gli iscritti.
4. **Lo staff** (`EventBookings.View`/`Edit`): le iscrizioni con **le somme**, «togli».
5. **`EventsPersonalData`** e `ErasureTests` per le iscrizioni; l'impostazione `inPersonRetentionMonths`.
6. **Se M4b non c'è ancora**: `evt_event_stats` nasce qui, intera (tutte le colonne del design §1.10), per le somme; E13a allora
   non la migra.

**Test**: integrazione: le risposte validate per tipo, un'obbligatoria senza risposta rifiutata; le somme; il ritiro dopo l'inizio
rifiutato; la cancellazione di un iscritto. E2e: il pilota si iscrive rispondendo a tre domande.
**Fatta quando**: sul banco un evento in presenza raccoglie un'iscrizione e lo staff ne vede le somme.

**Com'è andata**: *(a fase chiusa)*

### E17 — Attività parallele, e il giro completo di M4c

Design §4-bis.1, §4-bis.2, §2.3-bis, §11; nota `gli-eventi-in-presenza`. Branch `m4/e17-in-person-activities`.

1. **`evt_activities`** e **`evt_activity_bookings`** (migrazione): nome tradotto, luogo, finestra, durata del turno, posti per
   turno; il posto preso solo da un iscritto, **contato sotto un `SELECT … FOR UPDATE` sulla riga dell'attività**; nessun turno
   sovrapposto per lo stesso membro.
2. **La conservazione breve**: iscrizioni e posti dopo `inPersonRetentionMonths`, le somme nelle statistiche (`events-retention` nasce
   qui se M4b non c'è ancora).
3. **«Duplica»** copia domande e attività.
4. **Il giro completo di M4c**; **il rapporto di chiusura di M4** se M4b è già chiusa.

**Test**: integrazione: **l'ultimo posto di un turno preso da due membri nello stesso istante: uno vince**; iscrizioni e posti
cancellati dopo il periodo, le somme restano. Il giro completo verde.
**Fatta quando**: il giro completo di M4c passa in locale e in CI.

**Com'è andata**: *(a fase chiusa)*
