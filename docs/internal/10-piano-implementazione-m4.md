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
| E2b | Nucleo: il permesso, non il dipartimento | E2 (la domanda della sua nota, decisa sulla #209) | un grant a una posizione su un altro dipartimento dà il permesso e non il dipartimento: nessun claim `dept`, le righe nella lista di quel permesso |
| E3a | L'evento nello staff | E1, E2, E2b (i grant di chi collabora: nota di E2, decisa sulla #209) | lista e form generati, descrizione, banner, scali e capacità, annullare, eliminare; i nove grant di chi collabora |
| E3b | La vita dell'evento | E3a | pubblicare, l'uscita programmata, la fine; calendario, ricerca, usi dei file; `events-release` |
| E4 | Il pubblico e le rotte | E3b | `/events`, `/events/{slug}`, `events.eventList`; `evt_routes` del FOD |
| E4b | Nucleo: chi è online sugli scali | E4 (la sua nota, la (b) decisa da Carmine sulla #223) | `networkStats` con gli scali che una schermata chiede, un whazzup al minuto per tutti, la striscia con gli scali; la pagina dell'evento la monta nella prima fase del modulo dopo |
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
| E10f | Nucleo: `Awards.Assign` con un grant | E10d | `Awards.Assign` si dà con un grant, detto sul permesso; la divisione lo dà all'MD (decisa da Carmine sulla #205) |
| E10g | Nucleo: la versione di un contratto | E0 (la chiede E5: il punto 9 di Carmine sulla #228) | `ContractVersion`: l'intestazione di un contratto, le versioni, il 400 con le accettate; la copia dei tour resta, e il passaggio dei tour al nucleo è di una sessione di Carmine |
| E10h | Nucleo: il ritiro di chi ha mandato la riga | E0 (la chiede E6a: «ritirare cancella la riga», design §1.6) | `[WithdrawnByStakeholder]`: il membro che una riga `ISubmittedByMembers` riguarda la cancella, com'era caricata, se l'entità lo dice; l'avvio rifiuta il segno dove il guardiano non lo onorerebbe |
| E10k | Nucleo: le parole delle liste e i titoli delle schede | E0 (l'issue #224, decisa da Carmine) | la paginazione della lista generata tradotta; il titolo della scheda di ogni pagina, il nome della divisione come predefinito; una frase vuota per lista; «Premi Invio per salvare» solo dove Invio salva |
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

**Com'è andata** (30 settembre 2026, branch `m4/e2-events-skeleton`, PR #209, da `main` a `c107c98`; prima fase del modulo, nessuna
coda):

- **Fatto**:
  1. **Il modulo** `IvaoHub.Modules.Events` (referenzia solo il nucleo), in `IvaoHub.Web/Modules.cs`, nel `.sln`, nell'host e nei test
     di unità; `EventsDbContext : ModuleDbContext` con `__EFMigrationsHistory_events` e la migrazione **`Initial`**
     (`20260930170049_Initial`): **`evt_events` intera** — tutte le colonne del punto 1, con i tre limiti dello scostamento 6 di E0 — e
     **`evt_event_airports`**, con la chiave verso l'evento (a cascata) e l'indice univoco `(event_id, icao)`. Nessuna tabella del
     nucleo nella migrazione (le proiezioni e l'audit restano fuori, come per gli altri moduli).
  2. **Le entità**: `Event` (`IOwnedByDepartment` con la maschera, `IVisible`, `IPublishable`, `IAuditable`, `[Audited]`,
     `IHasResourceScope` `events:event:{id}`, area `Events`) ed `EventAirport` (maschera, audit, lo scope del suo evento, area
     `Events`). La proiezione arriva in E3b, gli endpoint in E3a.
  3. **Il catalogo** `EventsPermissions`: 12 permessi in 5 aree, `DeniedToStakeholder` su `EventAtc.Edit` ed `EventReports.Edit`.
     **I grant**: gli 11 dell'ED (EC ed EAC tutto tranne `EventReports.Edit`, EA1–9 senza `Events.Delete` né `Events.ManageSettings`) e
     i 2 del team di un FIR su `EventAtc.*` (coordinator, assistant, advisor: CH, ACH, CHA), in `config/division.json` e in
     `config/division.example.json`, con `scope: ED`. ⚠️ **Non quelli di AOD, FOD e MD**: scostamento 1, qui sotto.
  4. **`EventsSettings`** (`kindPresets`, `bookingGapMinutes` 10, `pilotRetentionMonths` 24, `reminderLeadHours` 24) dietro
     `Events.ManageSettings`, e la schermata generata `/staff/events/settings`, unica voce della sezione «Eventi» del back office. Un
     preset si sceglie fra i tipi del calendario del bootstrap; il salvataggio rifiuta sulla riga un tipo che il calendario non ha
     (`events:errors.calendarKindUnknown`) e un tipo ripetuto (`events:errors.kindTwice`).
  5. **Il segmento riservato** `events` (`IModule.ReservedSegments`).
  6. **`EventsArchitectureTests`**: il modulo non nomina la rete, non scrive rating, ICAO né tipi di evento, non ha un client suo; i due
     file della divisione danno all'ED e al team di un FIR quello che dice il design, e nessun grant degli eventi a chi collabora finché
     la nota non ha risposta (scostamento 1).
  7. **Le due verifiche del design §6.3**: nessuna chiede il nucleo (qui sotto, «Le due verifiche»).
  8. **I moduli `events` finti dei test del nucleo**: nessuno si scontra con quello vero. `ModuleCompositionTests` e `ContactThreadTests`
     compongono a mano un registro con un modulo inventato, `staffDestinations.test.tsx` un bootstrap scritto a mano, e il modulo di
     prova dell'integrazione è `sample` (`smp_`); `manifest.test.ts` legge le chiavi dai sorgenti, e ora trova `events` sulle due metà.
     Nessuna PR del nucleo.
- **Le due verifiche del design §6.3**, lette nel codice (la forma si scrive qui, il codice è di E11a):
  1. **L'intestazione dell'evento per chi non ha `Events.View`** (lo staff dei FIR). L'unico handler, chiesto **sulla riga dell'evento**
     con `EventAtc.View`, risponde no allo staff di un FIR con `firStaffScope: own`: il suo permesso porta il FIR
     (`EffectivePermission.Fir`) e l'evento non ne dice nessuno (`PermissionSet.ReachesFir`: mai una riga senza FIR). **La forma, senza il
     nucleo**: le schermate dell'area leggono l'intestazione (titolo, tipo, date, stato, scali) da un endpoint **dell'area**, con
     `RequireAuthorization(EventAtc.View)` senza risorsa — l'unico handler risponde «lo tiene da qualche parte»
     (`HubAuthorization.cs:163–166`) — e l'evento letto con `CrudSource.BackOffice`; le postazioni con la lista generata, che per lo staff di
     un FIR aggiunge le righe del suo FIR (`IHasFir`, `onTheirFir`). Lo stesso varrà per `EventRoutes` ed `EventReports`, se qualcuno ne
     terrà i permessi senza `Events.View` (FOD e MD lo hanno, design §6.2). **Che cosa comporta, detto**: chi tiene `EventAtc.View` da
     qualche parte legge l'intestazione di **ogni** evento — lo staff di un FIR deve poterlo, per aprire la prima postazione del suo FIR —,
     e un grant a un VID su un evento solo leggerebbe anche le intestazioni degli altri (niente dei membri). Se è troppo, E11a restringe
     l'endpoint (per esempio agli eventi con `has_roster`), non il nucleo.
  2. **Le disponibilità per le postazioni del FIR.** La disponibilità non ha un FIR, e la lista generata la chiude allo staff di un FIR (non
     è `IHasFir`, e lo staff di un FIR non appartiene a un dipartimento: `TryNarrowToDepartments` risponde 403). **La forma, senza il
     nucleo**: la schermata del roster legge le disponibilità **per postazione**, da un endpoint dell'area che carica la postazione, chiede
     all'unico handler `EventAtc.View` **sulla postazione** (con il suo FIR: passa solo lo staff di quel FIR, o chi non è tenuto a un FIR)
     e risponde le disponibilità dell'evento che coprono la finestra della postazione, lette dal modulo e non dalla lista generata. Lo
     staff di un FIR vede i candidati delle sue postazioni; la lista intera resta a chi tiene `EventAtc.View` sull'ED.
- ⚠️ **Scostamenti**:
  1. **I nove grant di AOD, FOD e MD non sono nei file** (punto 3). Un grant sull'ED fa entrare chi lo tiene nell'ED per tutto quello
     che vede (`HubClaims.BuildIdentity`, la regola del 6 settembre, scritta per un grant a una persona), e i nove sarebbero i primi grant
     a una posizione fra due dipartimenti: **misurato**, con i 22 grant del design la suite d'integrazione intera dà 430 test e **tre
     rossi del maintainer** (`SeveralDepartmentsTests` righe 81 e 120, `SearchEndpointTests` riga 82). Nota nuova
     `2026-09-30-i-grant-di-chi-collabora-sugli-eventi`, con la domanda sulla PR; **Carmine ha deciso la (b)** (30 settembre, in chat al
     master, pubblicata su sua istruzione [sulla #209][a209]): un grant a una posizione su un dipartimento che non è il suo dà il
     permesso, non il dipartimento, in una fase del nucleo a sé, **E2b**, prima di E3a, che la sessione che coordina prepara; i nove si
     seminano dopo di lei, non in E2. **E3a aspetta E2b**: prova «FOD e AOD non modificano il testo» e «chi collabora non elimina».
  2. **L'ordine dei moduli**: gli eventi **per primi** in `Modules.cs` e in `web/src/modules/index.ts` (eventi, tour, training), l'ordine
     delle sezioni del back office della nota `2026-09-13-moduli-non-subordinati-ai-dipartimenti` §3.1; tour e training restano nel loro.
     **Confermato da Carmine** [sulla #209][a209] («Yes, the Events section first»).
  3. **`evt_event_airports` nasce con le colonne del nucleo** che il punto 1 non elenca — maschera, audit, `row_version` —, perché E3a
     non ha migrazione e le righe degli scali copiano maschera e scope dell'evento (punto 3 di E3a).
  4. **Le forme delle colonne**, che il design non dice: `starts_at_utc` ed `ends_at_utc` obbligatorie (lo stato si legge dalle date, e
     un evento non ha modelli); `visible_from_utc` facoltativa (vuota = alla pubblicazione, design §1.2) e **`booking_opens_at_utc`
     facoltativa** (un evento senza slot non apre prenotazioni: che cosa chiede «Pubblica» lo dice E3b); `shift_minutes` e i tre limiti
     vuoti = l'impostazione; `venue` e `cancellation_note` tradotti e facoltativi; `slug` 100 (come i tour), `kind` 32 (la chiave di un
     tipo del calendario), `external_url` 1024 (un link della libreria). Indici: `slug` univoco e `(status, starts_at_utc)`.
  5. **I limiti delle impostazioni**, che il design non dice: `bookingGapMinutes` 0–1440, `pilotRetentionMonths` 1–120,
     `reminderLeadHours` 1–168 (come il promemoria del training). Un tipo scritto con le maiuscole diverse (`RFE`) è rifiutato: il
     database lo troverebbe, il browser no (la nota di E1, §3).
  6. **`EventsArchitectureTests` controlla anche i tipi**: nessuna chiave del seme del calendario scritta nel codice del modulo (nota
     `i-tipi-di-evento`: «il codice non conosce nessun tipo»). vIPI no: lo chiede già a ogni modulo
     `ArchitectureTests.NoModuleNamesTheAtcArchiveOrTheBoundaryDataset`, e non si scrive due volte.
  7. **Un test condiviso toccato**: le cinque righe `evt_` in `ErasureTests` (`evt_event_airports.created_by`, `.updated_by`,
     `evt_events.cancelled_by`, `.created_by`, `.updated_by`), con la nota breve `2026-09-30-le-colonne-degli-eventi-in-erasuretests`
     perché `core-guard` conta il file come nucleo — il caso che le «Regole di tutte le fasi» prevedono.
- **Trovato, e scritto per chi viene dopo**: i due grant del team di un FIR fanno scrivere al seme, **a ogni avvio** fino a E11a,
  «the grant of EventAtc.View to the team of a FIR … is not applied: no row of its area says its FIR» (e lo stesso per `Edit`): è il
  comportamento voluto (`PositionGrantSeeder.cs`), e il primo avvio con una riga `IHasFir` dell'area li applica.
- **Verificato, in locale** (30 settembre 2026, sul branch prima del commit dei documenti): `dotnet build` senza avvisi, e `dotnet
  format --verify-no-changes` sui dodici file C# toccati; unità **919/919**; **integrazione intera senza filtro 434/434** (8,9
  minuti), le classi toccate da sole 18/18 e `EventsSkeletonTests` da sola 3/3; **la misura della nota**: la stessa suite con i 22
  grant del design dà 430 test e 4 rossi — i tre del maintainer, e `ErasureTests` senza ancora le righe `evt_`; `pnpm lint`,
  `typecheck`, `format:check`, `i18n:check` verdi; `pnpm test` 599 in 81 file; `pnpm gen:api` e `i18n:sync` senza differenze dopo la
  copia; `pnpm e2e --workers=2` **163/163** al primo giro, dietro il lock dello smoke; **`pnpm e2e:full` 51/51 al primo giro** (10,4
  minuti, la spec nuova compresa) su un banco suo (`http://127.0.0.1:5112`, `ivaohub_e2e_e2` tolto prima, dietro il lock di Mailpit). Una prima corsa è stata fermata durante il
  publish, prima di ogni spec: lanciata per sbaglio con `--workers=2`, che la configurazione di `e2e:full` non vuole (un worker, per
  non pubblicare una sull'altra). Le regole di `core-guard` rifatte in PowerShell dalla base di merge: nessun file del maintainer, uno
  del nucleo (`ErasureTests.cs`), due note nuove. La CI della prima spinta (`b3b4849`) verde: `build-test` in 21,4 minuti, `core-guard`.
- **Dopo la revisione** ([osservazioni del revisore sulla #209][r209], «approvable on the merits», niente di bloccante; [risposte di
  Carmine][a209]) — 1 ottobre 2026:
  - **la nota** registra la decisione (la (b), E2b), con i link alla domanda e alla risposta; `EventsArchitectureTests` dice che i nove
    aspettano E2b;
  - **i due nit**: `EventsSkeletonTests` si riprende alla fine di ogni test i grant e le posizioni che dà ai suoi VID (i membri restano:
    l'hub non cancella un membro a mano); `events-skeleton.spec.ts` toglie all'inizio un preset `rfe` che una corsa interrotta avesse
    lasciato — il suo sarebbe il secondo, rifiutato con `kindTwice` — e rimette alla fine il banco senza;
  - **`main` unito** (merge, non rebase) dopo E10b (#208), E10e (#206), i tour sulla distanza del nucleo (#211) ed E10d (#205): un solo
    conflitto, in `HANDOFF-M4.md`, con i paragrafi di tutte le fasi tenuti; `config/division.example.json` (l'`awardDigestTime` di E10d)
    e questo file uniti da soli;
  - **i due test d'integrazione degli eventi avviano l'host con `useIvaoFixtures: true`**: l'avviso di E10b in `HANDOFF-M4.md` (senza,
    un host chiede un token a IVAO mentre i dati di riferimento sono vuoti).
- **Verificato di nuovo, dopo il merge** (1 ottobre 2026, sul branch prima del commit di questi documenti): `dotnet build` senza avvisi,
  e `dotnet format --verify-no-changes` sui file C# ritoccati; unità **937/937**; **integrazione intera senza filtro 441/441** (8,4
  minuti), le due classi degli eventi da sole 4/4, senza nessuna richiesta a IVAO nel log; `pnpm lint`, `typecheck`, `format:check`,
  `i18n:check` verdi; `pnpm test` 599 in 81 file; `pnpm gen:api` e `i18n:sync` senza differenze; `pnpm e2e --workers=2` **163/163** al
  primo giro, dietro il lock dello smoke; **`pnpm e2e:full` 51/51 al primo giro** (11,2 minuti, con il suo worker solo) sul banco
  `http://127.0.0.1:5112`, `ivaohub_e2e_e2` tolto prima, dietro il lock di Mailpit; le regole di `core-guard` dalla nuova base di
  merge (`db9268f`): PASS, un file del nucleo (`ErasureTests.cs`) e due note nuove.
- **Non verificato**: la CI dopo il merge (la dice la PR); i nove grant, che si seminano dopo E2b; le due verifiche del §6.3 sono
  scritte, non provate da un codice (E11a); la migrazione su un'installazione vera già avviata (la CI applica la catena su una MariaDB
  11.4.10 vera); `pnpm e2e:full` con la mappa di base, che non c'è in nessun worktree (le spec la tollerano, come in CI).

[a209]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/209#issuecomment-5917066144
[r209]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/209#issuecomment-5917043727

### E2b — Nucleo: il permesso, non il dipartimento

**Da dove viene**: da E2 (PR #209). Scrivendo i grant del design §6.2, la nota di E2 `2026-09-30-i-grant-di-chi-collabora-sugli-eventi`
ha misurato che i nove grant di chi collabora (AOD, FOD e MD con `scope: ED`) farebbero entrare tre dipartimenti nell'ED per tutto
quello che vedono — la «portata» della nota del 6 settembre, scritta per un grant a una persona — e tre test del maintainer andrebbero
rossi. **Carmine ha deciso la (b)** il 30 settembre 2026 ([la risposta sulla #209][ok209]): un grant a una posizione su un dipartimento
che non è il suo dà il permesso, non il dipartimento, in una fase del nucleo a sé **prima di E3a**. Nota nuova
`2026-10-01-il-permesso-non-il-dipartimento`. Branch `m4/e2b-grant-without-department`. **PR del nucleo**, senza coda.

1. Il permesso effettivo sa di venire da un grant a una posizione su un dipartimento non suo (come sa il suo FIR), e il claim lo porta.
2. `HubClaims.BuildIdentity` lascia quel dipartimento fuori dai claim `dept`, come per un permesso tenuto su un FIR.
3. La lista generata aggiunge le righe dei dipartimenti su cui chi legge tiene per quella via il permesso di lettura della lista — la
   forma di `onTheirFir`.
4. L'unico handler, il guardiano e il filtro globale non cambiano; un grant a una persona resta come il 6 settembre; i test del
   maintainer (`SeveralDepartmentsTests`, `SearchEndpointTests.SearchRespectsVisibility`) restano come sono. Il team di un FIR con
   `firStaffScope: all` è della stessa specie (nota di E2): si verifica.
5. I nove grant di chi collabora, se E2 è unita prima della fine di E2b; altrimenti li porta E3a (sopra, E3a punto 5).

**Test**: unità: quali grant danno il permesso da fuori, il claim, nessun `dept`, l'handler. Integrazione: una posizione con un permesso
di lettura su un altro dipartimento ne legge le righe nella lista di quel permesso e nient'altro del dipartimento — nessun `dept`,
nessun'altra lista, nessuna riga nella ricerca, nessun gruppo nella barra —; un grant a una persona allarga ancora come il 6 settembre.
**Fatta quando**: i test del maintainer restano verdi senza essere toccati, e i nove grant, quando arrivano, non fanno entrare nessuno
nell'ED.

**Com'è andata** (1 ottobre 2026, branch `m4/e2b-grant-without-department`, PR #212, del nucleo senza coda, da `main` a `db9268f`):

- **Fatto** (nota §3):
  1. **`EffectivePermission.FromOutside`**: il calcolo lo scrive su ogni permesso di un grant
     `UserGrant.GivesThePermissionNotTheDepartment` — a una posizione su un dipartimento non suo, o al team di un FIR — quando il permesso
     non porta un FIR; il `View` implicato lo porta con sé; lo stesso permesso raggiunto anche per nome, o da un ruolo, resta quello che
     dà il dipartimento.
  2. **Nel claim** un `!` in testa al pezzo dello scope (`Events.View:ED@!`): `FormatPermission` lo scrive, `ReadPermission` lo legge, e
     chi non lo conosce (`ParsePermission`, un pacchetto di prima) legge uno scope che nessuna riga dichiara, cioè chiuso.
  3. **`BuildIdentity`** non scrive il claim `dept` di un permesso da fuori; **`TryNarrowToDepartments`** aggiunge i dipartimenti su cui
     chi legge tiene da fuori il permesso di lettura della lista (ogni dipartimento se lo tiene su tutti; niente se lo tiene su una riga
     sola).
  4. **Le parole**: l'aiuto del form dei permessi dice le due portate (`grants.formHint`); `docs/FORKING.md` e i commenti di
     `config/division.example.json` dicono che cosa dà un grant a una posizione su un altro dipartimento, e che cosa cambia con `all`.
  5. **I test**: `PermissionFromOutsideRulesTests` (unità, 8 dopo la revisione) e `PermissionFromOutsideTests` (integrazione, 3 dopo la
     revisione, VID 761091–761093; 761090 è un'identità dei test di unità).
- **Precisazioni, scritte nella nota** (nessuna è una domanda nuova):
  1. **Anche un grant a una posizione senza dipartimento** (su tutti) è da fuori: vale anche sui dipartimenti che non sono della
     posizione, e altrimenti farebbe entrare i suoi titolari in ogni dipartimento. Oggi il seme di IT non ne ha.
  2. **Il team di un FIR con `all`**: verificato che entrava nel dipartimento del grant (e `FORKING.md` lo diceva); ora è da fuori. Con
     `own` il permesso porta il FIR e il suo claim non cambia.
  3. **Lo stesso permesso da fuori e per nome**: vince quello per nome, che dà il dipartimento; senza, l'ordine delle sorgenti
     (`grant:10` prima di `grant:9`) poteva togliere il dipartimento a chi un grant per nome aveva fatto entrare.
  4. **Un permesso da fuori tenuto su una riga sola** non allarga la lista: il lato che chiude, per un caso che oggi nessuno scrive.
  5. **L'aiuto della schermata dei permessi** non era chiesto: la nota del 6 settembre lo prometteva e non c'era. Una frase, in una
     commit a sé.
- ⚠️ **I nove grant di chi collabora non sono qui**: nominano permessi degli eventi, che esistono solo con E2 (#209), non unita il 1
  ottobre. **Li porta E3a** (punto 5), con `EventsArchitectureTests` che li accetta. **Misurato** in un worktree di prova mai spinto
  (E2b, più E2 a `b3b4849`, più i nove grant, cioè i 22 del design): **integrazione intera 443/443**, i tre test del maintainer che la
  nota di E2 aveva visto rossi compresi, senza toccarli; unità 944 con **un solo rosso, voluto**: `EventsArchitectureTests` di E2, che
  rifiuta i nove grant finché la nota non ha risposta.
- **Trovato, per chi viene dopo**:
  1. ⚠️ nel browser **`writableDepartments`** non offre il dipartimento di un permesso da fuori (non è fra quelli raggiunti): una
     schermata del modulo che fa creare una riga a chi collabora la crea sotto l'evento, con maschera e scope dell'evento, e chiede al
     server le `actions`, non `writableDepartments` (nota §3.7);
  2. ⚠️ **E10f** (in corso) dà `Awards.Assign` all'MD con un grant a una posizione **senza `scope`**: su `main` farebbe entrare
     coordinator e assistant dell'MD in **ogni** dipartimento (`BuildIdentity` scrive tutti i claim `dept` per un permesso da un grant
     senza dipartimento); con E2b no, il grant è da fuori (nota §3.8). Detto alla sessione di E10f, che lo chiude anche da sé con lo
     stesso campo e la stessa forma: chi arriva seconda a `main` somma le due condizioni del calcolo.
- **Verificato, in locale** (1 ottobre 2026, sul branch prima del commit dei documenti): `dotnet build` della soluzione senza avvisi, e
  `dotnet format --verify-no-changes` sui sette file C#; unità **910/910** (le 7 nuove comprese); **integrazione intera senza filtro
  439/439** (6,3 minuti), la classe nuova da sola 2/2. **Al contrario**: con il codice del nucleo di `main` il primo test d'integrazione
  cade sul primo `Assert` (`/api/me` dice `["AOD", "SOD"]`) e il secondo resta verde; con E2b senza il pezzo della lista cade sulla
  lista (riga 92); senza l'ordine delle voci uguali cade il test di unità della persona. `pnpm lint`, `typecheck`, `format:check`,
  `i18n:check` verdi; `pnpm test` 594 in 80 file; `pnpm gen:api` senza differenze; `pnpm e2e` **163/163** al primo giro, dietro il lock
  dello smoke; **`pnpm e2e:full` 50/50 al primo giro** (10,2 minuti) su un banco suo (`http://127.0.0.1:5120`, `ivaohub_e2e_e2b` tolto
  prima, dietro il lock di Mailpit). Le regole di `core-guard` rifatte in PowerShell dalla base di merge `db9268f`: nessun file del
  maintainer, sette del nucleo, la nota nuova.
- **Non verificato**: la CI (la dice la PR); il comportamento su un'installazione vera con cookie di prima (chi ha già un claim `dept` da
  un grant a una posizione su un altro dipartimento lo tiene fino al prossimo ingresso; su IT oggi nessuno); il team di un FIR con `all`
  sul database (lo provano le unità: IT è su `own`).
- **Dopo la revisione** ([i rilievi del revisore sulla #212][r212], «approvable», CI verde su `6613aac`; [la richiesta di unire
  `main`][m212]):
  1. **Il ramo «tutti i dipartimenti» della lista non era provato** (il rilievo da correggere): un permesso di lettura tenuto da fuori
     senza dipartimento. Ora lo prova `PermissionFromOutsideTests` (il terzo caso, VID 761093): gli advisor dell'AOD con `Links.View` su
     tutti i dipartimenti leggono i link di AOD, SOD e FOD e restano nel solo AOD (`/api/me`, la ricerca), e **un divieto** di
     `Links.View` sul FOD alla stessa posizione toglie il link del FOD; nelle unità, il divieto espande «tutti» negli altri otto
     dipartimenti, ognuno ancora da fuori. **E il ramo dà ora le righe di ogni dipartimento** (`RolePermissionMatrix.AllDepartments`)
     invece della lista senza filtro: il revisore notava che una riga in cura a nessun dipartimento sarebbe passata, che i claim `dept`
     di prima non lasciavano passare. Per ogni riga vera la risposta è la stessa. Provati al contrario: senza il ramo cade la lista,
     con il codice del nucleo di `main` cade `/api/me` (tutti e nove i dipartimenti).
  2. **`main` unito** (`c441839`, E2 con la #209), con un merge: i conflitti erano solo in questo file e in `HANDOFF-M4.md`, e ogni
     paragrafo è rimasto (il blocco di E2b sopra quello di E2, un'intestazione sola). **I nove grant non entrano**: li porta E3a, la cui
     sessione nasce sopra E2b, in coda dopo la #212 (detto dalla sessione che coordina). **E di nuovo** dopo E15a (#207, `99ab043`),
     unita mentre girava la CI di questo giro e che rendeva la #212 in conflitto: un altro merge, conflitto solo in `HANDOFF-M4.md` (il
     blocco di E2b sopra quelli di E15a e di E2). E15a tocca la metà IVAO del nucleo e nessun file di E2b.
  3. **L'ordine con E10f (#213)**: chi arriva seconda a `main` tiene una dichiarazione sola di `FromOutside`, somma le due condizioni del
     calcolo e rifà `AwardsAssignByGrantTests`, `GrantableGlobalPermissionTests` e le due classi di E2b sul codice unito. Durante questo
     giro la #213 era ancora aperta; poi è entrata prima, e **la seconda è E2b** (punto 5).
  4. **Verificato di nuovo, dopo il merge e i test nuovi** (1 ottobre 2026, sul branch prima del commit di questi documenti, `main` a
     `c441839`): `dotnet build` della soluzione senza avvisi, e `dotnet format --verify-no-changes` sui tre file C# ritoccati; unità
     **945/945** (le 937 di `main` e le 8 di E2b); **integrazione intera senza filtro 444/444** (5,6 minuti), la classe nuova da sola 3/3;
     `pnpm lint`, `typecheck`, `format:check`, `i18n:check` verdi; `pnpm test` 599; `pnpm gen:api` senza differenze; `pnpm e2e`
     **163/163** al primo giro, dietro il lock dello smoke; **`pnpm e2e:full` 51/51 al primo giro** (9,9 minuti) sul banco
     `http://127.0.0.1:5120`, `ivaohub_e2e_e2b` ricreato, dietro il lock di Mailpit (preso alle 10:52, dopo quello di E10f); le regole di
     `core-guard` dalla nuova base di merge (`c441839`): PASS, nessun file del maintainer, sette del nucleo, la nota nuova. La CI di
     `978d6ac` verde (`build-test` e `core-guard`). **Dopo il merge di E15a** (`main` a `99ab043`): `dotnet build` senza avvisi; unità
     **970/970** (le 25 di E15a in più); **integrazione intera 444/444** (5,5 minuti); `pnpm gen:api` senza differenze, e nessun file
     web portato dal merge (lint, Vitest e smoke del giro prima valgono); **`pnpm e2e:full` 51/51 al primo giro** (10,4 minuti) sul banco
     5120 ricreato, dietro il lock di Mailpit.
  5. **La riconciliazione con E10f** ([la richiesta del revisore][c212], dopo l'unione della #213 alle 11:05 UTC; il master aveva
     consigliato di aspettarla per unire `main` una volta sola, [qui][w212]): `main` unito a `ee43ec2` (`a33965b`, con E10a #210 ed E10f
     #213), conflitti in `HubClaims.cs`, `EffectivePermissionsCalculator.cs`, `docs/FORKING.md` e `HANDOFF-M4.md`. **Un solo
     `FromOutside`**, con le due vie nel suo `<param>`; **una condizione** nel calcolo, `fir is null &&
     (grant.GivesThePermissionNotTheDepartment || catalogue.IsGlobal(grant.Value))`; **un solo `.ThenBy(FromOutside)`**, con le due ragioni;
     **una riga in `BuildIdentity`**, con i due commenti; le due aggiunte tenute in `docs/FORKING.md` e in `config/division.example.json`.
     ⚠️ **Il segno ora viaggia anche per i globali**: chi assegna gli award per grant ha `Awards.Assign@!` e `Awards.View@!` nel cookie.
     **Come `Awards.View@!` apre ancora ogni riga di `Award`** (la domanda del revisore): il catalogo è condiviso in lettura (`Award` è
     `ISharedForReading`, e la sua lista dice `SharedForReading = award => true`), e l'unico handler risponde alla lettura di una riga
     condivisa con `HasAny(Awards.View)`, che non guarda né il dipartimento né il segno; e `Awards.View@!` si rilegge senza dipartimento,
     cioè tenuto da fuori su tutti, così la lista ci aggiunge ogni dipartimento e chi non è in nessuno non riceve il 403 «nessun
     dipartimento». Lo prova il quarto caso di `PermissionFromOutsideTests` (VID 761094): una posizione di HQ, in nessun dipartimento,
     con `Awards.Assign` per nome legge gli award di SOD e FOD e uno riga per riga; senza il ramo di tutti i dipartimenti la lista dà 403,
     quello che `main` dava prima di E2b (la nota di E10f lo diceva). **Verificato sul codice unito** (1 ottobre 2026, prima del commit
     di questi documenti, `main` a `ee43ec2`): `dotnet build` senza avvisi, e `dotnet format --verify-no-changes` sui file C# toccati;
     le classi chieste — `GrantableGlobalPermissionTests` e `PermissionFromOutsideRulesTests` **25/25**, `AwardsAssignByGrantTests` e
     `PermissionFromOutsideTests` **8/8** —; unità intere **1003/1003**; **integrazione intera senza filtro 449/449** (4,9 minuti);
     `pnpm lint`, `typecheck`, `format:check`, `i18n:check` verdi, `pnpm test` **601 in 82 file**, `pnpm gen:api` senza differenze; `pnpm
     e2e` **163/163** al primo giro, dietro il lock dello smoke; **`pnpm e2e:full` 51/51 al primo giro** (10,5 minuti) sul banco 5120
     ricreato, dietro il lock di Mailpit; le regole di `core-guard` dalla base di merge `ee43ec2`:
     PASS, nessun file del maintainer, sette del nucleo, la nota nuova. La CI di `909fe36` verde, e il revisore ha controllato la
     riconciliazione ([«approvable»][a212]).
  6. **`main` unito ancora dopo E10c** (#204, `ca80563`; [la richiesta][e212]: la #212 è la prossima nella coda, poi la #214): un merge
     (`1b9f9f1`), conflitto solo in `HANDOFF-M4.md` (il blocco di E2b sopra quello di E10c). `config/division.example.json`,
     `docs/FORKING.md` e le lingue del nucleo si sono uniti da soli: i rating preferiti e gli FRA di E10c stanno accanto alle righe di E2b,
     senza toccarle; `config/division.json` E2b non lo tocca. **Verificato sul codice unito**: `dotnet build` senza avvisi; unità
     **1101/1101**; **integrazione intera senza filtro 459/459** (7 minuti); `pnpm i18n:check` verde e `pnpm gen:api` senza differenze,
     nessun file web portato dal merge; `pnpm e2e` **163/163** al primo giro; **`pnpm e2e:full` 51/51 al primo giro** (10,8 minuti)
     sul banco 5120 ricreato; le regole di `core-guard` dalla base di
     merge `ca80563`: PASS, nessun file del maintainer, sette del nucleo, la nota nuova.

[ok209]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/209#issuecomment-5917066144
[r212]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/212#issuecomment-5926652025
[m212]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/212#issuecomment-5926813269
[w212]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/212#issuecomment-5929486987
[c212]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/212#issuecomment-5930060422
[a212]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/212#issuecomment-5931873565
[e212]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/212#issuecomment-5932263068

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
5. **I nove grant di chi collabora** (aggiunto da E2b): AOD `Events.View` ed `EventAtc.*`, FOD `Events.View` ed `EventRoutes.*`, MD
   `Events.View` ed `EventReports.*`, a tutti i livelli e con `scope: ED`, in `config/division.json` e in `config/division.example.json`,
   se E2b non li ha già portati (li porta solo se E2 è unita prima della sua fine: sotto, E2b); `EventsArchitectureTests` di E2, che li
   rifiuta finché la nota non ha risposta, li accetta. Da E2b danno il permesso e non l'ED: i test che raggiungono la loro parte e non
   l'ED, come la riga «Test» qui sotto.

**Test**: integrazione: l'ED crea e modifica; FOD e AOD non modificano il testo; chi collabora non elimina; EC elimina una bozza
vuota; un ICAO sconosciuto rifiutato sulla sua riga; `kindPresets` applicati. Unit: lo stato dalle date, ai bordi. E2e: il personaggio
dell'ED crea un RFO con due scali, lo riapre, lo annulla.
**Fatta quando**: sul banco un RFO con due scali e la capacità si crea, si riapre, si annulla; una bozza vuota si elimina.

**Com'è andata** (1 ottobre 2026, branch `m4/e3a-event-staff`, PR #214, in coda dopo la #212 di E2b, nato da
`m4/e2b-grant-without-department` a `6613aac`; uniti poi, con dei merge, `main` dopo E2 (#209), la nuova testa di E2b (`978d6ac`) dopo
la sua revisione, `main` dopo E15a (#207), che avrebbe messo la PR in conflitto in `HANDOFF-M4.md`, e la testa di E2b dopo il suo merge
di E15a (`d9e8f90`, solo documenti); nessuna migrazione):

- **Fatto**:
  1. **Lo stato dalle date** (`EventState.Of`, `src/IvaoHub.Modules.Events/EventState.cs`), come `TourState`: bozza, programmato,
     annunciato, prenotazioni aperte, in corso, concluso, annullato — dallo stato, dalle quattro date e dall'annullamento. Ogni istante è
     il primo momento di ciò che apre (un evento che finisce alle 22 alle 22 è concluso); l'annullamento vince su tutto, la bozza su ogni
     data. **Le cinque viste** della lista (`EventViews`, `filter[view]`): bozze, prossimi (programmati, annunciati, con le prenotazioni
     aperte), in corso, conclusi, annullati; ogni vista è scritta una seconda volta in ciò che SQL chiede, accanto alla funzione, e
     `EventsStateTests` tiene le due alla stessa risposta su una griglia di eventi e di istanti.
  2. **`MapCrud` dell'evento** (`/api/events/events`, `Staff/EventEndpoints.cs`): letto con `Events.View`, scritto con `Events.Edit`,
     eliminato anche con `Events.Delete` (`DeletePolicy`); ricerca su titolo e indirizzo, ordine per inizio, e nella lista la parola del
     calendario per il tipo (`ToListPage`, una query per pagina). **`EventSaving`**: un indirizzo che nessun altro evento ha, un tipo
     del calendario e attivo quando lo si sceglie (creazione o cambio, come una voce del calendario), nessuno scalo su un evento di
     tutta la divisione. **Eliminare** porta via gli scali nello stesso salvataggio, ognuno con la sua riga di audit; la regola «solo
     senza righe dei membri» non ha ancora tabelle da guardare: la prima è di E6a, in `EventSaving.DeleteAsync`.
  3. **Gli scali con la capacità** (`/api/events/airports`, `Staff/EventAirportEndpoints.cs`): righe figlie `IEventChild` (come
     `ITourChild`), che prendono dipartimento e maschera dall'evento prima che il permesso sia chiesto (`CrudOptions.BeforeAuthorize`,
     `EventChildren.AdoptAsync`) e rispondono con il suo scope; un aeroporto che il nucleo conosce (`IAirportDirectory.FindAsync`), una
     volta per evento, la capacità in movimenti oppure in arrivi e partenze. Lista e form generati nella scheda «Scali» della pagina.
  4. **Le schermate** (`web/src/modules/events/`): `/staff/events`, la lista generata con il filtro delle viste; `/staff/events/{id}`,
     la pagina con le schede impostazioni (il form generato), descrizione (l'editor dei blocchi, salvata con il `PUT` dell'evento come
     il briefing del tour) e scali; la barra «Annulla l'evento» ed «Elimina», offerte solo a chi il server lo lascia fare;
     `/staff/events/{id}/cancel`, la nota. **Gli interruttori preimpostati al cambio del tipo**: il form segue quello che si scrive e,
     quando il tipo cambia, si ridisegna con gli stessi valori e gli interruttori che il preset dà a quel tipo (`presetSwitches`).
  5. **Annulla** (`POST /api/events/events/{id}/cancel`, `Events.Edit` sulla riga): quando, chi (`cancelled_by`) e la nota in ogni
     lingua della divisione; una volta; mai su un evento concluso; mai sopra una versione più nuova (409). Il tipo di notifica
     **`events.eventCancelled`** è dichiarato, con la mail e la parola del profilo; nessun destinatario ancora (E6a, E12, E16).
  6. **I nove grant di chi collabora** (punto 5) in `config/division.json` e `config/division.example.json` (con una frase nel commento
     dei `positionGrants`); `EventsArchitectureTests` li sposta dall'elenco «in attesa» alla tabella del design.
  7. **I test**: `EventsStateTests` (unità, 6); `EventsStaffTests` (integrazione, 7, VID 761006–761010, slug `evt-test-e3a-…`, aeroporti
     `XEA1` e `XEA2` seminati e tolti); `web/src/modules/events/eventForm.test.ts` (vitest, 4); `web/e2e/full/events-staff.spec.ts`.
- **Scostamenti e scelte piccole** (nessuna è una domanda nuova, tranne la prima, che il revisore ha portato a Carmine):
  1. **Una lettura scritta a mano accanto al CRUD**, `GET /api/events/kind-presets` (`Events.Edit`): il design §7.2 conta fra gli
     endpoint a mano solo i verbi. I preset servono al form di chi scrive eventi, e le impostazioni le legge solo chi le gestisce
     (`ModuleSettingsEndpoints`, `Events.ManageSettings`), che un advisor dell'ED non ha. È il modulo che legge le sue impostazioni dal
     server, come fanno tour e training; il nucleo non cambia. Endpoint a mano di E3a: un verbo (annulla), una lettura (i preset).
     **Scostamento accettato da Carmine** il 1 ottobre 2026 ([la sua risposta][ok214], pubblicata dal master su sua istruzione): la
     lettura resta com'è e `ModuleSettingsDescriptor` non prende un permesso di lettura; nota
     `decisions/2026-10-01-la-lettura-dei-preset-dei-tipi.md`, con il suo «Da portare nel piano».
  2. **Il preset si applica nel browser, al cambio del tipo**, e il server salva gli interruttori come arrivano («preimpostati, mai
     imposti», nota `i-tipi-di-evento` §2.2). «`kindPresets` applicati» è quindi provato in tre posti: l'integrazione (i preset arrivano
     a chi scrive eventi e a nessun altro; un evento tiene gli interruttori con cui lo si salva), `eventForm.test.ts` (il preset di un
     tipo, e un form appena aperto che passa il suo schema: è ciò che gli fa seguire il tipo dalla prima scelta) e la spec e2e (scelto
     l'RFO, pubblici e privati accesi).
  3. **`has_roster` e `in_person` non sono nel form** (punto 2): il preset li porta, ma il form di M4a ne applica tre (slot pubblici,
     slot privati, tutta la divisione). Il dettaglio dell'evento li legge già tutti e cinque.
  4. **Annullare**: la nota in ogni lingua della divisione (la pagina pubblica la mostra: la regola di ogni testo pubblicato); un evento
     annullato resta annullato (nessun «riapri»: il design non lo prevede); un evento concluso non si annulla
     (`events:errors.eventOver`: è successo). Chi ha annullato sta in `cancelled_by` e nell'audit, non sulla pagina.
  5. **I limiti degli scali**, che il design non dice: capacità da 1 a 999 all'ora, ordine da 0 a 999, tutto facoltativo (la capacità
     serve agli slot privati, E7); movimenti oppure arrivi e partenze, mai insieme (`events:errors.capacityEitherOr`).
  6. **Tutta la divisione**: un evento con scali non diventa di tutta la divisione (`wholeDivisionHasAirports`, sul campo), uno di tutta
     la divisione non prende scali (`wholeDivisionHasNoAirports`) e non mostra la scheda.
  7. **Chi collabora** (AOD, FOD, MD) apre la pagina di un evento e ne legge gli scali: il form senza «Salva», con una frase che lo
     dice, e senza la scheda della descrizione (la legge la pagina pubblica, E4).
  8. **Lo stato sulla pagina è la `note` del `PageShell`**, non la `description`, che nel back office è solo il tooltip del titolo
     (trovato dalla spec e2e: lo stato non si vedeva).
  9. **Il nome della classe degli scali** è `EventAirportEndpoints`: `AirportEndpoints` esiste nel nucleo (`Core/Ivao`, gli aeroporti
     di riferimento), e i test che usano i due namespace non compilavano.
- **Trovato, e scritto per chi viene dopo**:
  1. ⚠️ **`SchemaForm` disegna i suoi default una volta**: un campo che ne cambia altri si fa ridisegnando il form con una chiave nuova
     e gli stessi valori, come gli interruttori al cambio del tipo; ⚠️ l'indirizzo proposto dal titolo smette di seguirlo dopo un
     ridisegno, se il titolo c'era già (il tipo è il primo campo apposta).
  2. ⚠️ **E11a ed E16**, quando portano `has_roster` e `in_person` nel form, li aggiungono anche a `presetSwitches`, a `EventWriteDto` e
     a `EventMapper.Apply`.
  3. ⚠️ **Le righe figlie che vengono** (rotte E4, slot E5, postazioni E11a, regole di award E14b) implementano `IEventChild` e
     adottano l'evento con `EventChildren.AdoptAsync` in `BeforeAuthorize`; ⚠️ nel browser `writableDepartments` non offre l'ED a chi
     collabora (E2b): una schermata che fa creare una riga al FOD la crea sotto l'evento, con il suo dipartimento.
  4. ⚠️ **La prima tabella dei membri** (E6a) mette il suo rifiuto in `EventSaving.DeleteAsync` e i destinatari di
     `events.eventCancelled` dopo il salvataggio di `CancelAsync`.
  5. **La spec e2e scrive un preset dell'RFO** nelle impostazioni del banco e lo rimette com'era: un'altra spec che chiede un preset fa lo
     stesso (come `events-skeleton.spec.ts` con l'RFE).
  6. **Il tipo di un evento si sceglie fra tutte le parole del calendario**, anche quelle di altri moduli (training, esame, tour,
     riunione, scadenza), come i preset di E2: il codice non conosce nessun tipo (nota `i-tipi-di-evento`) e niente, su un tipo del
     calendario, dice che è di un evento (visto sul banco, 1 ottobre). Se la divisione vorrà un elenco più stretto è una domanda per
     dopo, non di E3a: un segno sui tipi del calendario sarebbe del nucleo.
- **Verificato, in locale** (1 ottobre 2026, sul branch prima del commit di questi documenti): `dotnet build` della soluzione senza
  avvisi, e `dotnet format --verify-no-changes` sui dodici file C# toccati; unità **951/951** prima del merge di `main` con E15a e
  **976/976** dopo (le 25 di E15a); **integrazione intera senza filtro 451/451** prima (5,2 minuti) e **451/451** dopo (5,9 minuti),
  `EventsStaffTests` da sola 7/7 e con `EventsSkeletonTests` 10/10, **nessuna richiesta a IVAO** nel log delle due classi degli eventi
  (quelle del giro intero vengono da classi avviate senza le fixture: l'avviso di E10b). **Al contrario**: senza i nove grant in
  `config/division.json` il test di chi collabora cade sul suo primo `Assert.Contains` (`Events.View`), e con loro torna verde (il file
  rimesso com'era). `pnpm lint`, `typecheck`, `format:check`, `i18n:check` verdi; `pnpm test` **603 in 82 file**; `pnpm gen:api` e
  `i18n:sync` senza differenze; `pnpm e2e` **163/163** al primo giro, prima e dopo il merge, dietro il lock dello smoke; la spec nuova da
  sola sul banco `http://127.0.0.1:5121` (`ivaohub_e2e_e3a` tolto prima): rossa al primo giro (lo stato non si vedeva: scelta 8 qui
  sopra), verde dopo la correzione (1/1, 17 secondi); **`pnpm e2e:full` 52/52 al primo giro** (10,6 minuti, con il suo worker solo)
  sullo stesso banco ricreato, dietro il lock di Mailpit (preso alle 11:42, dopo quello di E2b), sul codice unito con `main` dopo E15a (la
  testa di E2b unita dopo porta solo documenti); le regole di `core-guard` rifatte in PowerShell dalla base di merge con `main`
  (`99ab043`): PASS, nessun file del maintainer, i sette file del nucleo di E2b con la sua nota, nessuno di E3a.
- **Non verificato**: la CI (la dice la PR); la descrizione con un blocco disegnato nel browser oltre il salvataggio (il server la prova
  in `EventsStaffTests`; la spec e2e non apre la scheda); il banner scelto nel `MediaPicker` (il banco non ha file degli eventi); le
  viste «prossimi», «in corso» e «conclusi» nel browser (senza «Pubblica», che è di E3b, il banco ha solo bozze: le prova
  l'integrazione con righe scritte sul database); `pnpm e2e:full` con la mappa di base, che non c'è in nessun worktree (le spec la
  tollerano, come in CI).
- **La CI della prima spinta** (`c1f0a98`): verde, `build-test` in 25,3 minuti, `core-guard`.
- **Dopo la revisione** ([i rilievi del revisore sulla #214][r214], «approvable on the merits»), 1 ottobre 2026:
  1. **La lettura dei preset** (`GET /api/events/kind-presets`, scostamento 1): il revisore chiedeva a Carmine di tenerla, o di chiedere
     al nucleo un permesso di lettura sulle impostazioni di un modulo (`ModuleSettingsDescriptor`, una PR del nucleo con la sua nota).
     **Carmine l'ha tenuta** ([la sua risposta][ok214], 1 ottobre 2026, autore `SkyMistery`): uno scostamento accettato dal design §7.2,
     scritto nella nota nuova `decisions/2026-10-01-la-lettura-dei-preset-dei-tipi.md` (E3a non ne aveva una), che porta nel piano §16.6
     la lettura fra gli endpoint a mano di M4. Nessun cambio al codice.
  2. **Annullare senza una versione** non fa il controllo del 409 (`request.RowVersion != default`): come `LegEndpoints`, e il browser la
     manda sempre. Lasciato com'è.
  3. **La pagina «Annulla» dice ciò che succede oggi**: tolta la frase sulla mail a chi ha prenotato, ha un turno o si è iscritto, e la
     mail dall'aiuto della nota, perché prima di E6a, E12 ed E16 non parte nessuna mail. ⚠️ La fase che manda la prima mail di
     `events.eventCancelled` rimette la frase (scritto nell'handoff).
  4. **Il banner non è ancora un uso di un file** (`Event` non è `IProjectable`): fino a E3b un banner si può eliminare dalla libreria
     mentre un evento lo mostra. È di E3b (punto 2 della sua fase); ⚠️ E3b viene subito dopo, scritto nell'handoff.
  5. **E2b riconciliata con E10f unita** (`909fe36`, che porta `main` dopo E10a (#210) ed E10f (#213) e metteva fine al conflitto della
     PR), con un merge, su richiesta della sessione che coordina: un solo conflitto, l'intestazione di `HANDOFF-M4.md`; i due file della
     divisione uniti da soli, con il grant `Awards.Assign` dell'MD di E10f accanto ai nove di chi collabora. **Verificato di nuovo** (1
     ottobre 2026, sul codice unito): `dotnet build` senza avvisi, e `pnpm gen:api` senza differenze (il contratto unito da solo è quello
     rigenerato); unità **1009/1009**, `EventsArchitectureTests` 30/30; **integrazione intera senza filtro 456/456** (5 minuti); `pnpm
     lint`, `typecheck`, `format:check`, `i18n:check` verdi; `pnpm test` **605 in 83 file**; `pnpm e2e` **163/163** al primo giro;
     **`pnpm e2e:full` 52/52 al primo giro** (11,1 minuti) sul banco `http://127.0.0.1:5121` ricreato, dietro il lock di Mailpit; le
     regole di `core-guard` dalla nuova base di merge (`ee43ec2`): PASS, nessun file del maintainer, i sette del nucleo di E2b con la sua
     nota, nessuno di E3a.
  6. **E2b dopo il suo merge di E10c unita** (`af0d7df`, che porta `main` dopo E10c (#204), di nuovo la fine del conflitto della PR),
     con un merge, su richiesta della sessione che coordina: un solo conflitto, l'intestazione di `HANDOFF-M4.md`, con il paragrafo di
     E10c tenuto fra quelli di E2b ed E10f; i due file della divisione uniti da soli, con i `preferredAtcRatings` di E10c accanto ai grant.
     **Verificato di nuovo** (1 ottobre 2026, sul codice unito): `dotnet build` senza avvisi e `pnpm gen:api` senza differenze; unità
     **1107/1107**, `EventsArchitectureTests` 30/30; **integrazione intera senza filtro 466/466** (7,6 minuti); `pnpm lint`, `typecheck`,
     `format:check`, `i18n:check` verdi; `pnpm test` **605 in 83 file**; `pnpm e2e` **163/163** al primo giro; **`pnpm e2e:full` 52/52 al
     primo giro** (10,9 minuti) sul banco `http://127.0.0.1:5121` ricreato, dietro il lock di Mailpit; le regole di `core-guard` dalla
     nuova base di merge (`ca80563`): PASS, come sopra.

[r214]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/214#issuecomment-5929130403
[ok214]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/214#issuecomment-5934118725

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

**Com'è andata** (5 ottobre 2026, branch `m4/e3b-event-life`, PR #221, nato da `main` a `78df526` — con E3a, E2b, E10a–f, E15a e il
piano 1.29 —, senza coda; nessuna migrazione, nessun file del nucleo):

- **Fatto**:
  1. **«Pubblica»** (`POST /api/events/events/{id}/publish`, `Events.Edit` sulla riga, in `Staff/EventEndpoints.cs` accanto ad
     «Annulla»): i controlli stanno in una classe sola, **`EventPublishing`** (`Staff/EventPublishing.cs`), con i `Refusals` del nucleo
     campo per campo — titolo e riassunto in ogni lingua della divisione (con le lingue che mancano); `visibleFromUtc ≤
     bookingOpensAtUtc ≤ startsAtUtc < endsAtUtc` (`events:errors.seenAfterItStarts`, `bookingBeforeItIsSeen`, `bookingAfterItStarts`,
     `endsBeforeItStarts`); per un evento con slot l'apertura delle prenotazioni (`bookingOpensRequired`) e almeno uno scalo
     (`slotsNeedAirports`, sotto il campo `airports`); il link per un evento della rete o di un'altra divisione (`externalUrlRequired`).
     Una volta (`alreadyPublished`), mai su un evento annullato (`alreadyCancelled`), 409 su una versione vecchia come «Annulla».
  2. **Un evento pubblicato resta pubblicabile**, come un tour pronto resta pronto: `EventSaving.PrepareAsync` chiede gli stessi
     controlli a ogni scrittura di un evento pubblicato e rifiuta con le loro chiavi; l'ultimo scalo di un evento pubblicato con slot non
     si elimina (`CrudOptions.Delete` degli scali, `DomainRefusalException` su `id`). È ciò che la nota `la-vita-di-un-evento` §2.2
     presuppone: all'uscita esce la riga com'è, «già passata dai controlli di Pubblica», e nessun rifiuto capita a quell'ora.
  3. **`Event` è `IProjectable`** (`Event.Project`): quando l'evento **si vede** — `EventState.IsSeen`, pubblicato, da `visible_from` (o
     dalla pubblicazione) alla fine, annullato o no — una voce di calendario con il tipo dell'evento, la sua visibilità (pubblico o
     membri), inizio e fine, verso `/events/{slug}`, se non è annullato; una riga della ricerca (titolo; riassunto e testo della
     descrizione) per lingua, se l'evento è per tutti. **Gli usi dei file**: il banner e le immagini della descrizione fino alla fine + 7
     giorni (`Event.MediaKeptAfterEnd`) per un evento pubblicato, e senza fine per una bozza finché è una bozza (dopo la revisione,
     punto 3 sotto; il nucleo tiene i file a una riga non pubblicata): chiude il rilievo 4 della #214, il banner che si poteva eliminare
     dalla libreria.
  4. **`events-release`** (`EventReleaseJob`, ogni 15 minuti, registrato in `EventsModule` come i job dei tour): riproietta con
     `ProjectionRefresh` gli eventi pubblicati il cui `visible_from` o la cui fine cadono fra l'inizio dell'ultimo giro riuscito e
     adesso. Decide dai suoi dati e non dall'ora (nota `i-job-quando-passenger-spegne-l-hub` §8): un giro perso o fallito lo recupera il
     giro dopo, uno doppio riscrive le stesse righe uguali; non scrive l'evento (nessuna versione nuova, nessun audit).
  5. **`events.eventChanged`** (orari cambiati) dichiarato in `EventsNotifications`, con la mail e la parola del profilo in tutte e due
     le lingue; nessun destinatario ancora (arrivano con le righe dei membri, come per `eventCancelled`).
  6. **La schermata**: nella barra della pagina dell'evento il bottone **«Pubblica»** per una bozza, a chi scrive l'evento; un rifiuto
     torna come elenco «campo: che cosa manca» sotto la barra (`PublishProblems`, con le lingue nominate come nel form), una risposta di
     altro tipo come una frase sola. La barra si ridisegna a ogni versione nuova della riga, così l'elenco va via con la correzione.
  7. **I test**: `EventsLifeTests` (integrazione, 6, VID 761011, scalo `XEB1`, slug `evt-test-e3b-…`, file della libreria
     761011001–003 che nessuno ha), con l'orologio dell'host spostato dal test (`WithWebHostBuilder` + un `IClock` suo, senza toccare
     la factory condivisa) e il job messo in pausa nello scheduler, come `TourTests`; `EventsStateTests` (unità, +1: `IsSeen` ai
     bordi); `web/e2e/full/events-staff.spec.ts` (+1 test: «Pubblica» elenca scali e apertura delle prenotazioni che mancano a un evento
     con slot; senza slot l'evento è pubblicato e annunciato, e sta fra i prossimi).
- **Scostamenti e scelte piccole** (letture del design, scritte qui per il revisore; la 1 e la 4, con il punto 2 di «Fatto», le ha poi
  decise Carmine: «Dopo la revisione», punto 1):
  1. **Un evento con slot chiede l'apertura delle prenotazioni** per essere pubblicato: il design dice «date coerenti» e la catena
     `visible_from ≤ booking_opens ≤ starts`; senza `booking_opens` un evento a slot non apre mai le prenotazioni (§3.3: «da
     `booking_opens_at_utc`»), e il suggerimento del campo lo dice già da E3a («vuoto per un evento senza slot»).
  2. **Un evento annullato tiene la riga della ricerca fino alla fine**: §8.1 toglie al calendario «bozza, non ancora visibile,
     annullato o concluso», alla ricerca solo ciò che non è pubblico, visibile e non concluso; la pagina di un annullato resta con la
     nota fino alla fine (§2.3), e chi lo cerca la trova.
  3. **Un evento per i membri** ha la voce nel loro calendario (la sua visibilità) e **nessuna riga nella ricerca**, che §8.1 dà agli
     «eventi pubblici visibili e non conclusi».
  4. **Il tipo della riga di ricerca è `events`**, la chiave del modulo, e non `event`: `event` è una parola seminata del calendario, e
     `EventsArchitectureTests` vieta al modulo di scriverne una (l'ha trovato il test, al primo giro). Il distintivo della ricerca mostra
     la chiave (`search.kinds.events` non c'è in `common.json`, che è del nucleo), come oggi `tour` per i tour.
  5. **«Pubblica» non ha un contrario**: il design non prevede di riportare un evento in bozza; un evento pubblicato si cambia restando
     pubblicabile, si annulla o, senza righe dei membri, si elimina.
  6. **I segnaposto della mail `eventChanged`** (`{{event}}`, `{{starts}}`, `{{ends}}`, `{{url}}`) sono una proposta per la fase che la
     manderà, che li riempie o li cambia (oggi nessuno la manda).
  7. **Il test e2e sta nella spec di E3a**, come secondo test, per usarne gli aiuti (`newEvent`, `removeOurEvents`) senza copiarli.
  8. **`EventsStaffTests` toglie anche le proiezioni** dei suoi eventi: i pubblicati di `TheListHasAViewForEachStateOfAnEvent`, scritti
     sul database, ora hanno voce e riga, e un `ExecuteDelete` passa accanto all'interceptor. Un aiuto solo, `EventsTestRows.ForgetAsync`,
     per le due classi.
- **Trovato, e scritto per chi viene dopo**:
  1. ⚠️ **Gli eventi scritti prima di E3b** (il banco, l'installazione di prova con la `0.6.0`) non hanno proiezioni finché non si
     salvano: oggi sono tutte bozze (Pubblica non c'era), e il loro banner diventa un uso alla prossima scrittura. Il primo giro di
     `events-release` su un'installazione riproietta solo i pubblicati con uscita o fine passate.
  2. ⚠️ **Spostare l'orologio di un test** si fa con un host derivato (`_base.WithWebHostBuilder(...ConfigureTestServices(... IClock ...))`).
     `EventsLifeTests` non fa richieste firmate mentre l'orologio è spostato: per prudenza, perché come la sessione di un membro legga
     un orologio spostato di giorni non è provato. ⚠️ E le righe di `hub_jobs_log` del job sono di tutte le classi: `EventsLifeTests` le
     toglie all'inizio di ogni test, perché ogni test conti i suoi giri da nessuno.
  3. ⚠️ **La fase che manda `eventChanged`** (E6a, con le prenotazioni) la manda dopo il salvataggio di un evento pubblicato i cui orari
     cambiano: il punto è `EventSaving`, che oggi rifiuta e basta.
  4. ⚠️ **E4** trova in `EventState.IsSeen` la regola «si vede» (pubblicato, da `visible_from` alla fine, annullato compreso) per la
     pagina pubblica e la lista, e in `Event.Project` l'indirizzo `/events/{slug}` che calendario e ricerca già usano.
- **Al contrario** (5 ottobre 2026): con il job che conta anche i giri falliti, `TheJobDoesTheSameRunTwiceAndAfterRunsLost` cade
  (`Assert.Single`: il secondo evento non entra); con `IsSeen` che ignora `visible_from`, cadono il test del «fatta quando» e quello dei
  giri (`Assert.Empty`: la voce c'è prima dell'uscita). Il codice rimesso com'era, verdi.
- **Verificato, in locale** (5 ottobre 2026, sul branch): `dotnet build` della soluzione senza avvisi, e `dotnet format
  --verify-no-changes` sui quattordici file C# toccati; unità **1108/1108** (la prima volta 1107/1108: `EventsArchitectureTests` ha
  trovato `"event"`, scelta 4 qui sopra); **integrazione intera senza filtro 472/472** (6,2 minuti), `EventsLifeTests` con
  `EventsStaffTests` da sole 13/13 e **nessuna menzione di `ivao.aero`** nel loro log; `pnpm lint`, `typecheck`, `format:check`,
  `i18n:check` verdi; `pnpm test` **605 in 83 file**; `pnpm gen:api` e `pnpm i18n:sync` rigenerati (il contratto e le copie delle lingue
  nel commit); `pnpm e2e` **163/163** al primo giro, dietro il lock dello smoke; la spec degli eventi da sola sul banco
  `http://127.0.0.1:5123` (`ivaohub_e2e_e3b` tolto prima) 2/2; **`pnpm e2e:full` 53/53 al primo giro** (11,3 minuti, il suo worker
  solo) sullo stesso banco ricreato, dietro il lock di Mailpit. Un primo giro di `e2e:full` si era fermato dopo due spec verdi, chiuso
  da fuori insieme a Docker Desktop, che si è spento due volte da solo: riavviato, il giro intero è quello sopra. Le regole di
  `core-guard` rifatte in PowerShell dalla base di merge (`78df526`): nessun file del maintainer, nessun file del nucleo.
- **Non verificato**: la CI (la dice la PR); il job fatto partire da Quartz alla sua ora (i test lo chiamano con `RunAsync`, con il job
  in pausa nello scheduler; la registrazione è quella dei job dei tour); la cancellazione dei file da parte di `media-expiry` alla
  scadenza degli usi (è del nucleo, la prova `MediaExpiryTests`: qui si prova la data); la voce nel calendario pubblico del browser
  (la provano le righe proiettate e la ricerca di un visitatore, nell'integrazione; la spec e2e prova lo stato e la lista); un evento
  `Members` letto da un membro (la visibilità della voce sì, la lettura no); la sessione di un membro con l'orologio spostato;
  `eventChanged` mandata (nessun destinatario); `pnpm e2e:full` con la mappa di base, che non c'è in nessun worktree.
- **La CI della prima spinta** (`93f1db7`): verde, `build-test` e `core-guard` (dai rilievi del revisore).
- **Dopo la revisione** ([i rilievi del revisore sulla #221][r221], «approvable on the merits»), 6 ottobre 2026:
  1. **Le tre regole** — un evento pubblicato si salva solo se passerebbe ancora «Pubblica», e il suo ultimo scalo con slot non si
     elimina (punto 2 di «Fatto»); un evento con slot, anche solo privati, esce solo con `bookingOpensAtUtc` (scelta 1); il tipo
     `events` nella ricerca (scelta 4) — **le ha decise Carmine** il 6 ottobre 2026 ([la sua risposta][ok221], autore `SkyMistery`,
     pubblicata dal master su sua istruzione): nota nuova `decisions/2026-10-06-le-regole-di-pubblica.md`, con il suo «Da portare nel
     piano» (design §2.2 e §8.1).
  2. **La sequenza**, stessa risposta: **nessuna consegna e nessun «Pubblica» sull'installazione di prova fra E3b ed E4**, perché la
     voce di calendario e la riga di ricerca puntano a `/events/{slug}`, che porta E4. Scritto come ⚠️ in `HANDOFF-M4.md`.
  3. **I file di una bozza** (punto 5, lasciato da Carmine alla sessione): una bozza li tiene **finché è una bozza** (uso senza fine),
     un evento pubblicato fino alla fine + 7 giorni, e il conto segue la fine. Con la regola di prima una bozza scritta con una finestra
     già passata, per sbaglio o per spostarla dopo, perdeva il banner al primo giro di `media-expiry`. Nella nota, §3; il test
     `ADraftKeepsItsFilesAndAPublishedEventUntilAWeekAfterItsEnd` prende il posto di quello sugli usi.
  4. Il distintivo della ricerca senza parola (punto 3): lo sistema il nucleo, una sessione del maintainer; niente qui. «Pubblica»
     senza versione salta il 409 (punto 4), come «Annulla»: lasciato com'è. Nessun «torna in bozza» e gli eventi di prima di E3b senza
     usi fino al prossimo salvataggio (punto 6): già scritti sopra.
  5. **Ciò che il revisore non ha verificato** — il bottone sullo schermo, e le modifiche del form non salvate quando «Pubblica»
     sostituisce la riga in cache —: dalla lettura del codice, non provato nel browser, «Pubblica» pubblica la riga **salvata**, e il
     form si ridisegna sulla riga pubblicata (la sua chiave è la versione della riga), quindi una modifica non salvata si perde senza
     un avviso. Scritto come ⚠️ in `HANDOFF-M4.md` per la fase che tocca di nuovo la pagina (E4, con la scheda delle rotte).
  6. **Verificato di nuovo** (6 ottobre 2026, dopo il punto 3, sul codice cambiato): `dotnet build` senza avvisi e `dotnet format
     --verify-no-changes` sui due file C# toccati; unità **1108/1108**; **integrazione intera senza filtro 472/472** (6,4 minuti);
     `pnpm gen:api` senza differenze; `pnpm lint`, `typecheck`, `format:check`, `i18n:check` verdi; `pnpm test` **605 in 83 file**;
     `pnpm e2e` **163/163** al primo giro, dietro il lock dello smoke; **`pnpm e2e:full` 53/53 al primo giro** (10,9 minuti) sul banco
     `http://127.0.0.1:5123` ricreato, dietro il lock di Mailpit.

[r221]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/221#issuecomment-6012258987
[ok221]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/221#issuecomment-6012333670

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

**Com'è andata** (6 ottobre 2026, branch `m4/e4-public-and-routes`, PR #223, **in coda dopo la #221** di E3b: nato dal suo branch a
`93f1db7` e unito di nuovo alla sua testa `f1c8c02`, dopo la revisione, con un merge; una migrazione additiva, `AddEventRoutes`):

- **Fatto**:
  1. **`evt_routes`** (punto 4; design §1.4): `EventRoute` (`src/IvaoHub.Modules.Events/EventRoute.cs`), riga `IEventChild` nell'area
     **`EventRoutes`** — `[PermissionArea]`, `[Audited]`, lo scope dell'evento —, con `departure_icao`, `arrival_icao`, `route` e le note,
     e le colonne del nucleo (maschera, audit, `row_version`); la chiave verso l'evento a cascata. Il CRUD `/api/events/routes`
     (`Staff/EventRouteEndpoints.cs`): letto con `EventRoutes.View`, scritto con `EventRoutes.Edit`, `filter[eventId]`, l'evento adottato
     in `BeforeAuthorize` come per gli scali, i due scali chiesti a `IAirportDirectory.FindAsync`. Nella pagina dell'evento del back office
     la scheda **«Rotte»** (lista e form generati, `/staff/events/{id}/routes/{routeId}`), a chi tiene `EventRoutes.View` sull'evento, con
     «Nuova rotta» a chi tiene `.Edit`. **Il FOD scrive le rotte e non l'evento**: il suo grant sull'ED (E3a, da fuori con E2b) gli dà
     l'area e nient'altro. **Eliminare un evento** porta via anche le sue rotte nello stesso salvataggio, ognuna con la sua riga di audit,
     come gli scali (`EventSaving.DeleteAsync`).
  2. **La pagina** `/events/{slug}` (punto 2): `GET /api/events/public/{slug}`, anonima, composta in `Public/PublicEvents.cs` — banner,
     stato e tipo (la parola e il colore del calendario), quando **in UTC e nell'ora della divisione**, chi organizza con la sua pagina, gli
     scali e le rotte con il nome che il nucleo conosce, la nota di un annullato, la descrizione con il renderer dei blocchi. **Il 404**:
     chi è il pubblico dell'evento la legge mentre l'evento si vede (`EventState.IsSeen`, sul filtro globale: un evento `Members` solo a
     chi è entrato); dopo la fine, per una bozza e per uno non ancora visibile risponde 404 a tutti, **tranne a chi tiene `Events.View`
     sulla riga** (l'unico handler), che la legge in ogni stato con `seen: false`: la pagina gli dice che nessun altro la vede e lo porta
     al back office (scelta 1 qui sotto).
  3. **Il blocco `events.eventList`** (punto 3; design §7.3), le due metà nella stessa PR: `EventListProvider` (sempre vivo; `kinds` come
     il blocco del calendario, `limit` come quello delle sessioni: dieci se non scritto, zero tutti fino a 50) e la registrazione in
     `web/src/modules/events/blocks/`, con le schede di `screens/EventCards.tsx`, le stesse di `/events`, e il link a tutti gli eventi. I
     conteggi: **`uiKit.test.ts` da 42 a 43, `DataBlockEndToEndTests` da 17 a 18**.
  4. **`/events`** (punto 1): le schede degli eventi che si vedono — in arrivo e in corso, gli annullati fino alla fine con il loro
     stato —, dal più vicino; i **filtri per tipo e per scalo** nell'indirizzo (`validateSearch`, con `catch` come `/calendar`), le scelte
     prese da quello che le schede hanno; sotto, **il `CalendarView` del nucleo sugli stessi eventi** (le quattro viste dello schermo del
     calendario), senza gli annullati, come il calendario della divisione. **Nessun archivio.** La pagina **legge il blocco**
     (`/api/blocks/data/events.eventList`), come `/calendar` legge il blocco del calendario: la lista non ha un endpoint suo, e chi decide
     quali eventi vede il pubblico è un posto solo (`PublicEvents.CardsAsync`). `EventState.Seen(now)` scrive `IsSeen` in quello che SQL
     chiede, e `EventsStateTests` tiene le due alla stessa risposta sulla griglia degli eventi.
  5. **«Pubblica» aspetta le impostazioni salvate** — l'⚠️ che E3b lasciava alla fase che tocca di nuovo la pagina (E3b, «Dopo la
     revisione», punto 5): il form delle impostazioni dice alla pagina quando qualcuno ci scrive, e finché non salva «Pubblica» è spento,
     con una riga che dice perché; il salvataggio, o il cambio di scheda, che ridisegna le impostazioni dalla riga, lo riaccende.
  6. **I test**: `EventsPublicTests` (integrazione, 4, VID 761033–761036, scali `XEC1`/`XEC2`, slug `evt-test-e4-…`); `EventsStateTests`
     (+1); `screens/cards.test.ts` (vitest, 6) e `schemas.test.ts` (+3); la smoke `web/e2e/events-public.spec.ts` (8: le schede, i filtri
     dall'indirizzo, la pagina con orari, scali, rotte e descrizione, l'annullato, il 404, lo staff, il blocco in una pagina); il giro
     `web/e2e/full/events-public.spec.ts` (il «fatta quando») e un terzo test in `events-staff.spec.ts` («Pubblica» che aspetta).
     `EventsTestRows` toglie anche le rotte.
- **⚠️ «Chi è online sugli scali» non c'è** (punto 2): **classificato prima di scrivere** (`CLAUDE.md` §5) — il blocco `networkStats` conta
  solo l'area della divisione e le sue proprietà non dicono scali; la chiave della cache di `IvaoAirspace` non distingue due insiemi di
  soli aeroporti con lo stesso numero; il modulo non può chiamare `IIvaoApiClient` (`EventsArchitectureTests`); la `LiveStatusStrip` fa una
  domanda fissa. Ogni strada passa dal nucleo, quindi **resta fuori da E4**. Nota nuova **«Proposta»**
  `decisions/2026-10-06-chi-e-online-sugli-scali-di-un-evento.md`, con la domanda a Carmine sulla #223: una fase del nucleo **E4b**
  (`networkStats` con gli scali chiesti da una schermata, come `from`/`to` del calendario; la chiave della cache che li nomina; la striscia
  con gli scali facoltativi), e la pagina che la monta nella prima fase del modulo dopo — oppure fuori da M4.
- **Scelte e scostamenti** (le prime cinque sono comportamento che il design non dice: nota nuova **«Proposta»**
  `decisions/2026-10-06-il-pubblico-degli-eventi.md`, con la domanda a Carmine sulla #223, come le regole di E3b che Carmine ha voluto in
  una nota):
  1. **La pagina allo staff degli eventi in ogni stato**, bozze comprese, e non solo dopo la fine come dice §2.4: una condizione sola; lo
     staff vede la pagina prima di pubblicare («Pubblica» non ha un contrario); chi collabora ci legge la descrizione (E3a, scelta 7).
  2. **Un annullato** nelle schede e nel blocco fino alla fine, con il suo stato; **non** nel calendario sotto le schede (E3b, §8.1).
  3. **Il filtro per scalo** tiene gli eventi che nominano lo scalo; un evento di tutta la divisione non ne nomina nessuno e compare solo
     senza filtro.
  4. **Le rotte**: le note **tradotte** (`remarks_i18n`, in tutte le lingue quando scritte, 500 per lingua; il design dice `remarks`); la
     rotta obbligatoria e non tradotta, 1024 caratteri; **più rotte fra gli stessi due scali** (nessun indice univoco); nessun vincolo che
     un capo sia uno scalo dell'evento; ordine per partenza e arrivo (poi l'ordine di scrittura: «Dopo la revisione», punto 8).
  5. **Eliminare un evento elimina le sue rotte**, con l'audit: il guardiano chiede `EventRoutes.Edit` a chi elimina (EC ed EAC ce l'hanno).
  6. **`/events` legge il blocco** invece di un endpoint suo (punto 4): i tour e il training hanno un endpoint della lista accanto al loro
     blocco; qui il precedente è `/calendar`. Endpoint a mano di E4: **una lettura composta**, la pagina dell'evento (pubblica) —
     accettata da Carmine come scostamento dichiarato dal design §7.2 («Le risposte di Carmine», sotto) —; le rotte sono `MapCrud`.
  7. **Le schede non hanno l'apertura delle prenotazioni** né la pagina gli slot: arrivano con E5 ed E6b. La pagina non ha la mappa delle
     rotte (`RouteMap` c'è nel nucleo, il design non la chiede).
  8. **Il punto 5 di «Fatto»** non era nel perimetro di E4: lo chiedeva l'⚠️ di E3b, piccolo e nel modulo; è una commit a sé.
  9. **Lo snapshot del modello** ha preso con `AddEventRoutes` anche `notified_at` di `cms_award_signals` (E10d), una tabella del nucleo
     mappata ed esclusa dalle migrazioni del modulo: nella migrazione nessun SQL per lei, solo il modello al passo.
- **Trovato, e scritto per chi viene dopo**: ⚠️ `EventsArchitectureTests` ha trovato al primo giro la parola `'event'` in una chiave di
  query (`[...publicKey, 'event', slug]`, ora `'page'`): fra apici il modulo non scrive nessuna chiave seminata del calendario, nemmeno
  dove non è un tipo — come la riga di ricerca di E3b. ⚠️ Gli esempi della galleria non scrivono un ICAO di quattro maiuscole né un tipo
  seminato (`XX01`, `gallery`). ⚠️ `EventSaving.DeleteAsync` è il posto delle righe figlie di ogni area (in `HANDOFF-M4.md`). ⚠️ La copia
  delle parole alla radice va rifatta (`pnpm i18n:sync`) dopo ogni cambio delle parole del modulo, anche dopo l'ultima commit: una era
  rimasta indietro (commit a sé).
- **Al contrario** (6 ottobre 2026): con la pagina che legge l'evento senza `EventState.Seen`, il test della pagina cade sul visitatore
  (`/api/events/public/evt-test-e4-over: OK` invece di 404); il codice rimesso com'era, verde.
- **Verificato, in locale** (6 ottobre 2026, sul branch): `dotnet build` della soluzione senza avvisi, e `dotnet format
  --verify-no-changes` sui tredici file C# toccati; unità **1109/1109** (la prima volta 1108/1109: la chiave `'event'` qui sopra);
  **integrazione intera senza filtro 476/476** prima del merge di E3b (4,9 minuti) e **476/476** dopo (7,8 minuti), `EventsPublicTests` da
  sola 4/4 e **nessuna menzione di `ivao.aero`** nel suo log; `pnpm lint`, `typecheck`, `format:check`, `i18n:check` verdi (il typecheck
  ha trovato al primo giro quattro indici della spec smoke, tipizzati); `pnpm test` **614 in 84 file**; `pnpm gen:api` e `pnpm i18n:sync`
  senza differenze; `pnpm e2e` **171/171** al primo giro (`--workers=2`, dietro il lock dello smoke; la spec nuova da sola 8/8 prima);
  **`pnpm e2e:full` 55/55 al primo giro** (11,4 minuti, il suo worker solo) sul banco `http://127.0.0.1:5124` (`ivaohub_e2e_e4` tolto
  prima), dietro il lock di Mailpit: le due spec nuove e quelle di E2, E3a ed E3b comprese. Un primo lancio non è partito: lo script con
  `$ErrorActionPreference = 'Stop'` ha preso l'avviso di pnpm su stderr per un errore (PowerShell 5.1) e si è fermato prima del publish,
  senza nessuna spec. Le regole di `core-guard` rifatte in PowerShell dalla base di merge (`78df526`): **PASS** — nessun file del
  maintainer; tre del nucleo, i tre test condivisi (`uiKit.test.ts`, `DataBlockEndToEndTests.cs`, `ErasureTests.cs`); quattro note nuove
  (le tre di E4 e quella di E3b).
- **Non verificato**: la CI (la dice la PR); la migrazione su un'installazione vera già avviata (la CI applica la catena su una MariaDB
  11.4.10 vera); una pagina con il banner e le immagini della descrizione nel browser (il banco non ha file degli eventi); il blocco
  `events.eventList` messo dall'editor su una pagina del banco (lo provano la smoke, con una pagina finta, e l'integrazione); il calendario
  di `/events` nel browser vero (lo prova la smoke con le risposte finte); `pnpm e2e:full` con la mappa di base, che non c'è in nessun
  worktree.
- **La CI della prima spinta** (`ff8ff9f`): verde, `build-test` (25,2 minuti) e `core-guard`.
- **Dopo la revisione** ([i rilievi del revisore sulla #223][r223], «approvable on the code», in attesa delle risposte di Carmine; la
  #221 unita nel frattempo, `168fa25`, e la #223 mostra solo E4), 6 ottobre 2026:
  1. **Le due note «Proposta»** (punto 1) aspettano Carmine, e con loro **la seconda lettura scritta a mano**,
     `GET /api/events/public/{slug}` (punto 2; scelta 6 qui sopra): il revisore chiede a Carmine di accettarla per il conto di §16.6,
     come la lettura dei preset sulla #214. Con la sua risposta entra nella nota `il-pubblico-degli-eventi`, con il link.
  2. **`search.kinds.events`** (punto 3): la parola del tipo `events` nella ricerca la porta la #222 del nucleo, che fa chiedere a
     `manifest.test.ts` le due righe. Come chiede il revisore, niente qui: quando la #222 è unita, il merge di `main` su questo branch le
     tiene (il conflitto atteso è nei due `events.json` del modulo e nelle loro copie alla radice).
  3. **Una rotta da uno scalo a sé stesso** (punto 4) è **rifiutata**: una regola del validatore sull'arrivo, che confronta i due capi
     senza spazi intorno e senza distinguere maiuscole e minuscole, con la chiave sua `events:errors.routeToItself` (le parole nelle due
     lingue), e la riga nel test delle rotte (`XEC1` → `xec1`). Nella nota `il-pubblico-degli-eventi`, punto 4.
  4. **`/events` si ferma a 50** (punto 5): il commento di `EVERY_CARD` in `screens/public.tsx` diceva «tutti gli eventi in arrivo», ma il
     server dà a una lista al più `PublicEvents.MaxItems` (50), i più vicini, e i filtri e il calendario lavorano su quelli. Corretti il
     commento e **l'aiuto di `limit` del blocco**, che diceva lo stesso a chi compone una pagina (ora «i cinquanta più vicini»); che cosa
     vedrebbe una divisione con più di cinquanta eventi annunciati insieme è in `HANDOFF-M4.md`.

  Nello stesso giro, **quello che dalberone ha visto sul banco di prova** (l'hub pubblicato di `ff8ff9f`, con sei eventi e le rotte del
  FOD), portato dalla sessione che coordina:
  5. **Il form dell'evento offre ogni tipo del calendario**, anche Training, Esame, Tour, Riunione e Scadenza. **Classificato prima di
     scrivere** (`CLAUDE.md` §5): nessun dato distingue i tipi degli eventi — i tipi del calendario hanno chiave, etichetta, colore e ordine;
     una riga di `kindPresets` oggi non dice «è un evento», e un tipo senza riga si sceglie lo stesso —, e le note unite (E1 §1,
     `i-tipi-di-evento` §4) dicono che il form sceglie fra tutti i tipi del bootstrap. Quindi **nessun codice**: nota nuova **«Proposta»**
     `decisions/2026-10-06-i-tipi-che-un-evento-sceglie.md`, con la terza domanda a Carmine sulla #223 — la raccomandazione: i tipi degli
     eventi sono quelli con una riga di `kindPresets`, e tutti quando non ce n'è nessuna (un fork nuovo non resta con la scelta vuota).
  6. **La riga «solo lo staff»** elencava i tre motivi insieme. Ora dice quello vero, e **lo dice il server**: `EventState.Unseen` (bozza,
     non ancora visibile, concluso), al posto del booleano `seen` del DTO, `unseen`, nullo per chi l'evento lo vede; le parole sono tre,
     `events:public.staffOnly.{Draft|NotSeenYet|Over}`. Dallo stato soltanto il browser non poteva dirlo: un annullato è annullato
     qualunque siano le sue date, visibile o no. `EventsStateTests` tiene `Unseen` alla stessa risposta di `IsSeen` sulla griglia, e
     `EventsPublicTests` legge il motivo della bozza, del programmato, del concluso e di un programmato annullato.
  7. **I nomi degli scali** nelle schede e nel filtro di `/events`, come sulla pagina («LIRF · Roma Fiumicino»): le schede del blocco
     portano gli scali come `{ icao, name }` (`PublicEventCardDto.Airports`, una domanda sola a `IAirportDirectory` per tutte le schede),
     `AirportName` è passato dalla pagina a `EventCards.tsx` e serve a tutte e due, il filtro mostra codice e nome e l'indirizzo tiene il
     codice. Senza un nome conosciuto, il codice solo.
  8. **L'ordine delle rotte**: per partenza, il ritorno LIMC→LIRF veniva prima dell'andata LIRF→LIMC. **Scelto l'ordine in cui il FOD le
     scrive** (l'`Id`), sulla pagina e come ordine di partenza della scheda del back office, dove le colonne degli scali si ordinano
     ancora a mano: è l'ordine che chi scrive ha in mente, e non chiede una colonna nuova né una regola su quali scali vengono prima.
     L'altra strada, l'ordine degli scali dell'evento, non sa dove mettere una rotta con un capo fuori dall'evento né quelle di un evento
     di tutta la divisione. Nella nota `il-pubblico-degli-eventi`, punto 4; il test della pagina scrive l'andata dal secondo scalo, che
     un ordine dei codici metterebbe dopo.
  - **Al contrario** (6 ottobre 2026): con le rotte della pagina rimesse in ordine di partenza, il test della pagina cade (`[2, 1]` invece
    di `[1, 2]`); il codice rimesso com'era, ricompilato.
  - **Verificato di nuovo** (6 ottobre 2026, sul codice di tutti e otto i punti): `dotnet build` della soluzione senza avvisi e `dotnet
    format --verify-no-changes` sui file C# toccati; unità **1110/1110**; **integrazione intera senza filtro 476/476** (5,7 minuti; prima
    dei punti 5–8, con i soli 4 e 5, 476/476 in 7,2 minuti), `EventsPublicTests` da sola 4/4; `pnpm gen:api` (la forma nuova del DTO:
    `unseen`, `EventUnseen`) e `pnpm i18n:sync` rifatti; `pnpm lint`, `typecheck`, `format:check`, `i18n:check` verdi; `pnpm test` **615
    in 84 file**; `pnpm e2e` **171/171** al primo giro (`--workers=2`, dietro il lock dello smoke); **`pnpm e2e:full e2e/full/events-` 6/6** al
    primo giro — le sei prove degli eventi soltanto, non il giro intero — sul banco `http://127.0.0.1:5124` ricreato (`ivaohub_e2e_e4`
    tolto prima), dietro il lock di Mailpit. Una commit per punto (4, 5, 6, 7, 8); quelle dei punti 6, 7 e 8 toccano gli stessi file,
    divise per blocchi del diff, e **non sono state compilate una per una**: l'ultima è il codice provato.
- **La CI della seconda spinta** (`94ca28b`): verde, `build-test` (25 minuti e 42 secondi) e `core-guard`.
- **Le risposte di Carmine** ([sulla #223][ok223], 6 ottobre 2026, autore `SkyMistery`, pubblicata dal master su sua istruzione): **sì a
  tutte e quattro** — le cinque letture del pubblico come sono scritte; «chi è online sugli scali» è la fase del nucleo **E4b**, in una PR
  sua con la sua nota, non urgente, che può venire dopo E5 e che la prima fase del modulo dopo di lei monta (non è di E4: la sessione che
  coordina la prepara da `main`); la seconda lettura scritta a mano **accettata**, uno scostamento dichiarato dal design §7.2, contato per
  il piano §16.6; i tipi di un evento come raccomandato. Le tre note sono «decise», con il link. Carmine lasciava scegliere dove fare i
  tipi, su questa PR o nella fase dopo: **qui**, perché E5 parta pulita, e la #223 lo dice.
- **Dopo le risposte**, 6 ottobre 2026:
  1. **`main` unito** (`90c0937`): la #222 (la parola degli eventi nella ricerca, `search.kinds.events`, tenuta nelle due lingue del
     modulo e nelle copie alla radice), la #218 e la #220; nessun conflitto.
  2. **I tipi degli eventi** (nota `i-tipi-che-un-evento-sceglie`, §5): il form offre i tipi del calendario con una riga di
     `kindPresets`, tutti finché non ce n'è nessuna, e il tipo dell'evento stesso se ha perso la riga (`eventKinds` in `schemas.ts`); il
     server rifiuta gli altri su un evento nuovo o al cambio di tipo, con `events:errors.kindNotOfEvents` (`EventSaving.PrepareAsync`,
     accanto al controllo del calendario e come lui solo quando il tipo si sceglie); gli aiuti di `kindPresets` e del campo «Tipo di
     evento» dicono la regola. Test: `EventsStaffTests.AnEventChoosesAKindOfTheEventsAndAnyKindWhileTheSettingsListNone` (integrazione),
     `eventForm.test.ts` (+1), e nella spec `events-staff` il form che offre RFO, che ha la riga, e non l'Esame; la bozza da eliminare di
     quella spec ora è un RFO, perché in quel test l'RFE non ha la riga e il form non lo offrirebbe più.
  3. **Al contrario**: con la regola spenta (`Count: > 99` invece di `> 0`), il test dei tipi cade — l'evento `exam` è creato invece di
     essere rifiutato —; rimessa e ricompilato.
  4. ⚠️ **Docker Desktop si era fermato**: al primo giro le tre classi degli eventi sono cadute tutte in 10 secondi
     (`DockerUnavailableException`, nessun test eseguito davvero); riavviato Docker Desktop, 15/15. Nessun test cambiato per questo.
  5. **Verificato di nuovo** (sul codice con `main` e i tipi): `dotnet build` della soluzione senza avvisi e `dotnet format
     --verify-no-changes` sui due file C# toccati; unità **1112/1112**; **integrazione intera senza filtro 481/481** (5,7 minuti), le tre
     classi degli eventi da sole 15/15; `pnpm i18n:sync` rifatto e `pnpm gen:api` senza differenze; `pnpm lint`, `typecheck`,
     `format:check`, `i18n:check` verdi; `pnpm test` **624 in 85 file**; `pnpm e2e` **171/171** al primo giro (`--workers=2`, dietro il
     lock dello smoke); **`pnpm e2e:full` 55/55 al primo giro** (11,2 minuti, il giro intero) sul banco `http://127.0.0.1:5124` ricreato
     (`ivaohub_e2e_e4` tolto prima), dietro il lock di Mailpit; le regole di `core-guard` in PowerShell dalla base di merge `584eb72`:
     **PASS** (nessun file del maintainer; del nucleo i tre test condivisi; quattro note nuove).

[r223]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/223#issuecomment-6014660539
[ok223]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/223#issuecomment-6017107039

### E4b — Nucleo: chi è online sugli scali

Design §7.1, §9.1; nota `2026-10-06-chi-e-online-sugli-scali-di-un-evento` (E4, #223), la cui (b) **Carmine ha scelto** il 6 ottobre
2026 ([punto 2 sulla #223][a223]): una fase piccola del nucleo, nella sua PR con la sua nota, montata dalla prima fase del modulo dopo;
«non urgente, può seguire E5». Branch `m4/e4b-online-at-airports`. **PR del nucleo**, senza coda, con la sua nota (caso b).

1. **`NetworkStatsProvider`** prende gli scali che una schermata chiede (`airports`, un elenco di ICAO), fuori dallo schema zod come
   `from` e `to` del calendario: con gli scali lo spazio è il loro — controllori la cui stazione è uno di loro, piloti il cui piano di
   volo parte da uno di loro o ci arriva, le due regole di `IvaoWhazzup` su un altro spazio.
2. **`IvaoAirspace`** con una `CacheKey` che nomina gli scali quando non ci sono centri; ⚠️ da misurare che un whazzup per insieme di
   scali al minuto regga (oggi uno per la divisione), con la data nella nota.
3. **`LiveStatusStrip`** con gli scali facoltativi e la parola del titolo per quel caso, nelle due lingue.
4. **Non in E4b**: la pagina dell'evento che monta la striscia, che fa la prima fase del modulo dopo il merge.

**Test**: il provider con gli scali sulle fixture (nessuna chiamata a IVAO); due insiemi con chiavi diverse; la risposta della divisione
invariata; la striscia con e senza scali (vitest). Nessuna migrazione.
**Fatta quando**: una schermata chiede gli scali e il blocco li conta; la striscia della divisione chiede e risponde come prima.

**Com'è andata** (6 ottobre 2026, branch `m4/e4b-online-at-airports`, PR #226, del nucleo senza coda, da `main` a `584eb72`):

- **Fatto** (nota nuova `2026-10-06-chi-e-online-sugli-scali-nel-nucleo`, scelta tecnica, nessuna domanda nuova):
  - **`NetworkStatsProvider`** (`src/IvaoHub.Core/Content/CoreDataBlockProviders.cs`) legge `airports` con `BlockProps.ReadTexts`
    (nuovo, `Content/DataBlocks.cs`): con l'elenco lo spazio è `IvaoAirspace.OfAirports`, senza è quello della divisione come prima;
    **un elenco senza nessuno scalo non conta nessuno, mai la divisione**; al più `DataBlockScope.MaxItems` (50) scali. Le cifre
    `divisionAtc` e `divisionPilots` contano lo spazio chiesto, nessuna cifra nuova;
  - **`IvaoAirspace`** (`Core/Ivao/IvaoNetworkStatus.cs`): `OfAirports` (ripuliti, maiuscoli, una volta, al più 4 caratteri come
    `ref_ivao_airports.icao`, nessun centro) e la `CacheKey` che **senza centri nomina gli scali** (`0/2/LIRA,LIRF`); con i centri la
    chiave di prima, quella della divisione;
  - **un whazzup al minuto per tutti** (`IvaoApiClient.GetNetworkStatusAsync`, `IvaoWhazzup.cs`): la lettura `IvaoNetworkPicture`
    (totali, controllori con nominativo, stazione e frequenza, le due estremità dei piani di volo; nessun VID, nome o traccia) sotto
    una chiave sola per un minuto, il fallimento compreso; ogni spazio si conta una volta per lettura, con la sua `CacheKey`, e la
    risposta se ne va con lei. `IvaoWhazzup.Read(root)` dà la lettura e `Count` è la regola «in area», per il client vero e per
    quello delle fixture. `IIvaoApiClient` non cambia;
  - **`LiveStatusStrip`**: `airports?: readonly string[]`, la domanda con gli scali e il titolo `liveStatus.airportsTitle` («Su questi
    scali adesso», «At these airports now»); senza, la domanda e il titolo di prima. Una riga in `docs/UI-GUIDELINES.md`;
  - **i test**: `tests/IvaoHub.UnitTests/LiveStatusAirportsTests.cs` (unità, 5: due insiemi mai con la stessa chiave, la chiave della
    divisione com'era, che cosa tiene `OfAirports`, uno spazio di scali conta solo loro, **una lettura sola per la divisione, due
    insiemi e venti inventati**); `tests/IvaoHub.IntegrationTests/NetworkStatsAirportsTests.cs` (integrazione, 4, anonima come il
    browser, sulla fixture `whazzup.json`: Francoforte contata anche se non è della divisione, `LIRF` senza il centro sopra, un
    elenco vuoto o senza scali non conta nessuno, la divisione invariata dopo una domanda con gli scali);
    `web/src/shared/ui/LiveStatusStrip.airports.test.tsx` (vitest, 4: il titolo, la domanda con gli scali, la domanda della divisione
    parola per parola, l'elenco vuoto). Nessun VID, nessuno slug: nessuna persona nei test.
- ⚠️ **Scostamento dal punto 2** (nota §2 e §4): la chiave nomina gli scali, come chiesto, ma **il whazzup è uno al minuto per tutti, non
  uno per insieme**. Misurato il 6 ottobre 2026 alle 16:00 e alle 16:18 UTC (nota §2): 0,77–0,79 MB, 75–513 ms, dalla cache di
  Cloudflare; per gli eventi di un giorno uno per insieme reggerebbe, ma l'insieme lo sceglie chiunque chieda all'endpoint anonimo, e
  ogni insieme inventato sarebbe uno scaricamento intero. Con la lettura comune: circa 75 KiB tenuti per un minuto, 0,05 ms per
  contare uno spazio, mille insiemi inventati in 24 ms e circa 246 KiB invece di circa 770 MB.
- **Le prove al contrario**, con il codice del nucleo di `main` rimesso al posto del mio (e i miei file rimessi e toccati dopo): dei 4
  test d'integrazione nuovi ne cadono 3 (gli scali ignorati: risponde la divisione, 3 controllori) e passa quello della divisione
  invariata; con la striscia di `main` e le parole nuove cadono 3 dei 4 vitest nuovi e passa quello della domanda della divisione. I 5
  test di unità chiedono `OfAirports` e `IvaoNetworkPicture`, che su `main` non ci sono: non compilano, non li ho provati là.
- **Verificato, in locale** (6 ottobre 2026, una suite alla volta, tutte al primo giro): `dotnet build IvaoHub.sln` 0 avvisi; unità
  **1115/1115** (5 nuovi); **integrazione intera, senza filtro, 480/480** (5,8 minuti); `dotnet format --verify-no-changes` sui nove file
  C# pulito; in `web/` `pnpm lint`, `typecheck` e `format:check` verdi, `i18n:check` (en, it: 7 namespace, 795 chiavi), `pnpm test`
  **617/617** in 85 file, `pnpm gen:api` senza differenze; **lo smoke** `pnpm e2e --workers=2` sulla 4173, dietro il suo lock,
  **163/163** (1,1 minuti, i tre di `live-status.spec.ts` compresi); **`pnpm e2e:full` 53/53** (10,1 minuti) sul banco
  `http://127.0.0.1:5125` con `ivaohub_e2e_e4b` nuovo, dietro il lock di Mailpit: la striscia della divisione su ogni pagina pubblica
  del banco ha chiesto `networkStats` 47 volte, tutte 200, dalla fixture; le regole di `core-guard` rifatte in PowerShell sul branch
  contro la sua base con `main` (`584eb72`): nessun file del maintainer, dodici file del nucleo con la nota nuova, PASS.
- **Il merge di `main`** (chiesto dalla sessione che coordina, 6 ottobre 2026): unita la #223 (E4) alle 17:03 UTC, dopo l'apertura di
  questa PR, la PR era in conflitto con `main` su `HANDOFF-M4.md`, dove tutte e due le fasi avevano scritto in cima, e la sua CI non
  poteva finire. `origin/main` (`77a2031`, con E4 e la #225, la `0.6.5`) è entrato con un merge (`b6fcd7c`), mai un rebase. **Un
  conflitto solo**, `HANDOFF-M4.md`: l'intestazione di E4b con E4, la #222 e la #225 fra le unite e il prossimo passo di E4 aggiornato;
  «Che cosa ha lasciato E4b» in cima e quello di E4 sotto, intero. `10` si è unito da solo (la «Com'è andata» di E4, poi E4b, poi E5).
  La nota dice ora che quella di E4 è su `main`, «decisa». Poi di nuovo: `dotnet build` 0 avvisi; unità **1117/1117** (le 2 di E4 in
  più); `NetworkStatsAirportsTests` con `DataBlockEndToEndTests` **10/10** (il conteggio dei blocchi è quello di E4); `pnpm lint`,
  `typecheck` e `format:check` verdi, `i18n:check` (807 chiavi), `pnpm test` **629/629**, `pnpm gen:api` senza differenze; `core-guard`
  contro la nuova base (`77a2031`): gli stessi dodici file del nucleo e la nota, PASS. **Non rifatti dopo il merge**: l'integrazione
  intera, lo smoke ed `e2e:full` — il merge ha portato il codice di E4 e della #225 e nessun file di questa fase è cambiato; l'integrazione
  intera la rifà la CI.
- **Dopo la revisione** ([rilievi del revisore sulla #226][r226], «the design is sound»; il punto 1, il merge di `main`, è sopra):
  - **punto 2, le risposte tenute senza tetto**: `IvaoNetworkPicture` tiene le risposte di al più `MaxKeptAnswers` (16) spazi — la
    divisione e gli eventi di un giorno, con margine — e conta ogni volta tutti gli altri (un ventesimo di millisecondo), così chi
    inventa insiemi di scali sull'endpoint anonimo non fa crescere quello che una lettura tiene; il tetto si controlla prima di
    aggiungere, e richieste nello stesso istante possono superarlo al più di quante sono. Test di unità
    `AReadingKeepsTheAnswersOfAFewAirspacesAndCountsTheRestEveryTime`: dieci volte più insiemi del tetto, i primi tenuti (la stessa
    risposta), gli altri contati ogni volta (una risposta nuova, giusta);
  - **punto 3, il limite degli scali prima della pulizia**: `IvaoAirspace.OfAirports(scali, limite)` ripulisce, toglie i doppi e poi
    prende al più il limite, nell'ordine chiesto; il provider gli passa `DataBlockScope.MaxItems`. Test: di unità
    `TheCeilingOfTheAirportsIsCountedAfterTheCleaning` (cinquanta voci non valide o lo stesso scalo ripetuto non spingono fuori uno vero;
    oltre il limite restano i primi chiesti) e d'integrazione `EntriesThatAreNoAirportDoNotPushOneThatIsPastTheCeiling` (anonimo, come il
    browser: cinquanta voci non valide davanti a `EDDF`, e `EDDF` è contato);
  - **punto 4, solo la lunghezza**: uno scalo è da una a quattro **lettere o cifre** (`char.IsAsciiLetterOrDigit`), così la virgola che
    separa la chiave non può farne due insiemi uguali. Test di unità `ACommaNeverMakesTwoSetsOneKey` (`{"A,B","C"}` e `{"A","B,C"}`) e
    `OnlyCodesAnAirportCanHaveAreKept` con un trattino, uno spazio e una lettera accentata;
  - **punto 5, per Carmine**: la nota non diceva più «nessuna domanda» senza nominarle: ora dice le due cose che il revisore gli porta,
    lo scostamento (una lettura al minuto per tutti, tenuta in memoria) e le parole del titolo.
  - **Le prove al contrario**, con il comportamento di prima rimesso per un momento (il limite prima della pulizia, solo la lunghezza,
    nessun tetto) e poi tolto: cadono i 4 test di unità dei punti 2–4 e il test d'integrazione nuovo, passano gli altri 4 e 4.
  - **Verificato, in locale** (6 ottobre 2026, al primo giro): `dotnet build` 0 avvisi; unità **1120/1120** (le 3 nuove);
    **integrazione intera, senza filtro, 486/486** (5,6 minuti: le 480 di prima, le 5 di E4 e la nuova); `dotnet format
    --verify-no-changes` sui sei file C# pulito; in `web/` (nessun file cambiato) `pnpm lint`,
    `typecheck`, `format:check` verdi, `i18n:check` (807 chiavi), `pnpm test` **629/629** in 86 file.
- **Non verificato**: la CI (la dice la PR); il whazzup di una sera di punta (circa 4 MB in proporzione, non misurato: un martedì
  pomeriggio); i limiti di chiamate di IVAO (il design §9.1 li lascia a una misura, e questa fase non fa più chiamate di prima); la
  striscia con gli scali su una pagina vera, nel browser: non la monta ancora niente, la monta la fase del modulo (nota §6);
  `tiles/basemap.pmtiles` non c'è su questa macchina, e il giro intero è andato senza la mappa di base, come un'installazione che non ce
  l'ha.

[a223]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/223#issuecomment-6017107039
[r226]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/226#issuecomment-6021367518

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

**Com'è andata** (6 ottobre 2026, branch `m4/e5-public-slots`, PR #228, nato **in coda dopo la #223** di E4: dal suo branch a `94ca28b`, e
unito alla sua ultima spinta `3224a9f` — le risposte di Carmine, `main` con la #222, i tipi degli eventi — prima di scrivere il resto del
codice; la #223 è unita, `77a2031`, prima che la #228 si aprisse; una migrazione additiva, `AddEventSlots`):

- **Fatto**:
  1. **`evt_slots` intera** (punto 1; design §1.5): `EventSlot` (`src/IvaoHub.Modules.Events/EventSlot.cs`), per i pubblici e i privati, con
     tutte le colonne del piano e quelle del nucleo (maschera, audit, `row_version`); `aircraft_types` è una colonna `json` con il nome
     del design (come `body_json` dell'evento lo porta nel nome, questa no); univoco `(event_id, callsign, off_block_utc)`; la chiave verso
     l'evento a cascata. Riga `IEventChild` nell'area **`EventBookings`** (`[PermissionArea]`, `[Audited]`, lo scope dell'evento).
  2. **Incolla e carica** (punto 2; §3.1): un endpoint, `POST /api/events/events/{id}/slots/load` (`EventBookings.Edit` sulla riga
     dell'evento, all'unico handler), con il testo e il modo. Il lettore (`Staff/SlotSheet.cs`) separa per tabulazioni, punto e virgola o
     virgole secondo l'intestazione, tiene le virgolette, conta le righe come la tabella, salta quelle vuote; una riga diventa uno slot
     (`SlotDraft.Read`) o un rifiuto per cella; poi, per tutta la tabella insieme, gli aeroporti (`IAirportDirectory.FindAsync`) e i tipi
     (`IAircraftTypeDirectory.UnknownAsync`) del nucleo, il verso (`SlotDirection`), callsign e off block una volta nell'evento, le
     rotazioni (`SlotChains`, con `bookingGapMinutes` delle impostazioni e gli slot salvati che restano). Tutto o niente, con
     `Refusals` sotto `rows[N].colonna`; una transazione: via i pubblici liberi se si sostituisce, dentro quelli della tabella.
  3. **Lista e form generati** (punto 3): `/api/events/slots` (`MapCrud`, `EventBookings.View`/`.Edit`, `filter[eventId]`, l'evento
     adottato in `BeforeAuthorize`), e `SlotSaving` nel `BeforeSave`: il form di uno slot tiene le regole del caricamento. **«Elimina i
     liberi»**: `POST /api/events/events/{id}/slots/delete-free`. Nella pagina dell'evento del back office la scheda **«Slot»**
     (`screens/slots.tsx`) con «Incolla o carica», «Nuovo slot», «Elimina i liberi» e «Modifica»; la pagina del caricamento con
     l'intestazione da copiare, il file CSV letto nella casella e i rifiuti elencati per riga e colonna (`screens/slotList.ts`,
     `sheetProblems`). **La pagina pubblica** (`/events/{slug}`) elenca gli slot pubblici, in sola lettura, nella lettura che c'è già
     (`PublicEventDto.Slots`): libero o preso, mai chi, le rotazioni raggruppate, gli orari in UTC con il giorno detto una volta.
  4. **L'esportazione** (punto 4; §7.4): `GET /api/events/{slug}/bookings/export` (`Export/BookingsExport.cs`), con l'`audience`
     **`events.bookings`** dichiarata dal modulo (`EventsModule.TokenAudiences`, `EventBookings.View`) e il permesso chiesto anche sulla riga
     dell'evento; un array con i nomi del Gate Manager e gli orari UTC; una bozza no (409 `draft`).
  5. **Le regole che crescono** con gli slot: in `EventSaving` l'interruttore degli slot pubblici non si spegne sotto gli slot, e
     eliminare un evento porta via i suoi slot; in `EventAirportEndpoints` uno scalo con slot non si elimina né cambia codice.
  6. **I test**: `EventsSlotsTests` (unità, 29), `EventsSlotsTests` (integrazione, 4, VID 761012–761014, scali `XED1`–`XED4`, tipi
     `XE5A`/`XE5B`, slug `evt-test-e5-…`), `screens/slotList.test.ts` (vitest, 4), un test nuovo nella smoke `web/e2e/events-public.spec.ts`,
     il giro `web/e2e/full/events-slots.spec.ts` (il «fatta quando»: il personaggio dell'ED incolla una tabella con una rotazione, legge il
     rifiuto di una riga, la carica corretta, e un visitatore la vede sulla pagina). `EventsTestRows` toglie anche gli slot; `ErasureTests`
     ha le due righe di `evt_slots` (nota `2026-10-06-le-colonne-degli-slot-in-erasuretests`).
- **Scelte e scostamenti** (comportamento che il design non dice: nota nuova
  `decisions/2026-10-06-il-foglio-degli-slot-e-l-esportazione.md`, «Proposta» con la domanda a Carmine sulla #228, poi **decisa** —
  sotto, «Le risposte di Carmine»; il dettaglio è lì):
  1. **Il foglio lo legge il server**, non il browser come le leg dei tour: il design dice che arriva come testo e scrive i rifiuti con il
     nome della colonna.
  2. **La tabella**: i nomi del design in qualunque ordine; il punto e virgola accanto alle tabulazioni e alle virgole; le righe contate
     come la tabella; al più mille; **gli orari solo `2026-10-17 14:30` in UTC**.
  3. **Il verso fra due scali dell'evento** è la partenza; **uno slot sempre su uno scalo dell'evento**, e così «ogni due tratte uno scalo»
     viene da sé.
  4. **Le rotazioni**: i posti dagli orari quando nessuno è scritto; il rifiuto sempre sulla tratta che si scrive; il form di uno slot con le
     stesse regole, **anche per uno slot nuovo** («Nuovo slot»: il design dice il form «per le correzioni»).
  5. **«Sostituisci»** toglie i pubblici liberi; **«Elimina i liberi»** ogni slot libero, privati compresi.
  6. **Le regole che crescono**, sopra (punto 5 di «Fatto»); la finestra dell'evento è venuta con le risposte di Carmine (sotto, «Dopo la
     revisione e le risposte», punto 1).
  7. **L'esportazione**: un array, i nomi del Gate Manager, nell'ordine dell'orario allo scalo dell'evento, una bozza 409 con
     `code: "draft"` invece di 404, un evento pubblicato in ogni stato; un privato con il suo scalo e il suo orario.
  8. **La lista pubblica nella lettura della pagina**, senza un endpoint suo.
  9. **La scheda «Slot»** c'è su un evento con slot (pubblici o privati) e scali suoi, a chi legge le prenotazioni; un privato non ha
     «Modifica» (`events:errors.slotNotPublic` sul server).
  10. **Gli endpoint scritti a mano di E5** sono tre verbi che il design nomina (§7.2): caricare, eliminare i liberi, esportare; gli slot uno
      per uno sono `MapCrud`. Nessuna lettura nuova.
- **Trovato, e scritto per chi viene dopo**: ⚠️ in FluentValidation un `.When` alla fine di una catena vale per tutta la catena: la regola
  del formato del callsign avrebbe spento «obbligatorio» (trovato rileggendo, prima dei test; ora sta in un `RuleFor` suo, e il test manda
  un callsign vuoto). ⚠️ `ArchitectureTests.AModuleKeyIsAskedWithItsNamespaceOnTheServer` cade finché una chiave `events:…` che il server
  scrive non è nella copia delle parole alla radice: le parole prima, poi `pnpm i18n:sync`. ⚠️ E4 si è mosso a metà fase: la modifica
  non ancora committata di `EventSaving.cs`, che E4 toccava anche lui, è stata messa da parte come patch e rimessa dopo il merge (un
  conflitto nel solo commento in testa).
- **Al contrario** (6 ottobre 2026): senza il rifiuto di una bozza, il test dell'esportazione cade (`Expected: Conflict`, `Actual: OK`);
  senza i rifiuti delle catene, quello del caricamento (`rows[7].departure_icao` non c'è). Il codice rimesso com'era — con la `using` di
  `IvaoHub.Core.Division`, che senza il primo controllo IDE0005 rifiuta — e ricompilato: la classe di nuovo 4/4.
- **Verificato, in locale** (6 ottobre 2026, sul branch con E4 dentro, `3224a9f`): `dotnet build` della soluzione senza avvisi, e `dotnet
  format --verify-no-changes` sui quindici file C# toccati; unità **1141/1141**; **integrazione intera senza filtro 485/485** (6,9 minuti),
  `EventsSlotsTests` da sola 4/4 al primo giro, e le classi degli eventi con `ErasureTests` e `PersonalTokenTests` 31/31; `pnpm lint`,
  `typecheck`, `format:check`, `i18n:check` verdi; `pnpm test` **628 in 86 file**; `pnpm gen:api` e `pnpm i18n:sync` senza differenze;
  `pnpm e2e` **172/172** al primo giro (`--workers=2`, dietro il lock dello smoke; la spec della pagina pubblica da sola 9/9 prima);
  **`pnpm e2e:full` 56/56 al primo giro** (10,9 minuti, il suo worker solo) sul banco `http://127.0.0.1:5126` (`ivaohub_e2e_e5` ricreato
  prima), dietro il lock di Mailpit, e la spec nuova da sola 1/1 prima. Le regole di `core-guard` rifatte in PowerShell dalla base di merge
  (`3224a9f`): **PASS** — nessun file del maintainer; un file del nucleo, `ErasureTests.cs`; due note nuove.
- **Non verificato**: la CI (la dice la PR), che prova il merge con `main` dopo la #225 (`git merge-tree`: nessun conflitto); la
  migrazione su un'installazione vera già avviata (la CI applica la catena su una MariaDB 11.4.10 vera); il Gate Manager vero che legge
  l'esportazione (E9, fuori dal repository); una tabella incollata davvero dagli appunti di un foglio di calcolo (la spec scrive il testo
  nella casella, come lo darebbe un incolla; le tabulazioni le provano i test di unità) e un file CSV scelto dal disco (nessuna spec carica
  un file: lo legge `File.text()` del browser); un CSV in una codifica che non è UTF-8 (`File.text()` legge UTF-8: uno stand con lettere
  accentate salvato in Windows-1252 arriverebbe storpiato); la pagina con centinaia di slot nel browser (la lista è una query sola, ma
  nessuna prova ne disegna 441).
- **La CI della prima spinta** (`da6f772`): verde, `build-test` e `core-guard`.
- **Le risposte di Carmine** ([sulla #228][a228], 6 ottobre 2026, autore `SkyMistery`, pubblicate dal master su sua istruzione), dopo [i
  rilievi del revisore][v228] («approvable on the code», in attesa delle risposte): **sì agli otto punti** della nota, ora **decisa**, e
  **due in più** sulle domande che il revisore gli aveva girato, codice di questa PR: **9**, l'esportazione porta la versione del suo
  contratto; **10**, uno slot cade nella finestra del suo evento, con un margine.
- **Dopo la revisione e le risposte**, 6 ottobre 2026:
  1. **La finestra dell'evento** (Carmine, punto 10): `SlotWindow` in `Staff/SlotRules.cs`. L'orario allo scalo dell'evento — l'off
     block di una partenza, l'on block di un arrivo — sta fra sei ore prima dell'inizio e sei ore dopo la fine; l'orario all'altro
     aeroporto è libero. Fuori, `events:errors.slotOutsideWindow` sulla colonna di quell'orario, nel caricamento (`SlotLoading`) e nel
     form (`SlotSaving`) allo stesso modo; la pagina del caricamento lo dice fra i formati. Il perché delle sei ore è nella nota (§2,
     punto 10). Test: unità (i bordi della finestra, e quale orario conta), integrazione
     `ASlotFallsInsideTheWindowOfItsEventWithSixHoursEachWay`: una partenza tre giorni dopo e un arrivo sette ore dopo la fine, rifiutati
     insieme sulle loro due colonne; un arrivo cinque ore dopo la fine, dopo un volo di undici, caricato; il form che rifiuta una partenza
     del giorno prima.
  2. **La versione del contratto dell'esportazione** (Carmine, punto 9). **Classificata prima di scrivere**, come chiedeva la sessione
     che coordina: il controllo dei tour (`AgentContract.RequireVersionAsync`) sta nel loro modulo, che gli eventi non referenziano e
     che E5 non tocca, e una copia negli eventi sarebbe lo stesso pezzo scritto due volte (`CLAUDE.md` §2). Tre strade a dalberone — la
     copia con un'eccezione di Carmine, il pezzo nel nucleo prima, la domanda a Carmine —: **ha scelto il nucleo**. È la fase **E10g**
     (la **#230**, branch `m4/e10g-contract-version`, da `main` a `e9702b27`, in una sessione sua, con la sua nota), e la #228 sta in
     coda dopo di lei: il branch di E10g è unito a questo a `cc1b46c`. Qui l'esportazione usa il `ContractVersion` del nucleo con i
     suoi valori — l'intestazione `Hub-Bookings-Contract`, la
     versione 1, `code: "bookingsContract"`, il titolo `events:errors.bookingsContract` — dopo il token e prima dell'evento, e il contratto
     per chi scrive il programma è `docs/events-bookings-export.md` (inglese), come `docs/agent-contract.md` dei tour.
  3. **Lo stesso volo ricaricato con «sostituisci»** (revisione, punto 3): il test del caricamento ricarica in `ReplaceFree` il foglio
     corretto del volo `XEA301` — stesso callsign e stesso off block, lo stand cambiato —. Lo slot che va e quello che viene dividono la
     chiave dell'indice univoco nello stesso `SaveChanges`, e la cancellazione arriva prima dell'inserimento: provato.
  4. **409 solo per un'altra scrittura delle stesse righe** (revisione, punto 4): il `catch` dopo `LoadAsync` prende soltanto una chiave
     che l'indice univoco ha già, o il deadlock di due insert della stessa chiave — `MySqlException` con `DuplicateKeyEntry` o
     `LockDeadlock`, cercata nella catena dell'eccezione come fa `InitialisationMarker`, perché EF consegna il deadlock dentro una
     `InvalidOperationException` —; ogni altro errore esce com'è, invece di dire «carica di nuovo» per sempre.
  5. **Il commento dell'esportazione** (revisione, punto 5): 401 senza token o con il cookie, 403 con un token di un'altra `audience`,
     come dicono il test e la nota.
  6. **Due caricamenti dello stesso evento nello stesso momento** (revisione, punto 6) li tiene solo l'indice univoco: ognuno controlla
     le catene con gli slot che legge, quindi insieme possono salvare una catena che nessuno dei due avrebbe accettato da solo. Nessun
     codice: un caricamento alla volta per evento chiederebbe una transazione serializzabile o un blocco esplicito, per due persone sullo
     stesso evento nello stesso secondo. Scritto nell'HANDOFF.
  7. **Il 404 prima del 403** (revisione, punto 7): voluto, ed è detto nel commento dell'esportazione e nella nota (§1, punto 7): è
     l'ordine dei verbi del back office, un token di questa `audience` lo fa solo chi ha `EventBookings.View` da qualche parte, e
     l'indirizzo di un evento pubblicato è comunque sul sito.
  8. **Il verso fissato quando lo slot si scrive** (revisione, punto 8): uno scalo aggiunto dopo lascia uno slot salvato com'era. Scritto
     nell'HANDOFF.
  9. **L'ordine dell'esportazione**, trovato rileggendola per il documento pubblico: la nota e il commento dicevano «per l'orario allo
     scalo dell'evento», la query ordinava per off block, che è quell'orario solo per una partenza. Ora va per l'on block di un arrivo
     (`IsArrival ? OnBlockUtc : OffBlockUtc`); nel test l'arrivo `XEA503` atterra alle 17:30, dopo la partenza `XEA501` delle 17:00, ma è
     decollato alle 16:00, quindi l'ordine degli off block non lo fa passare.
  10. **`main` è arrivato con il branch di E10g** (`e9702b27`: la #227, che vuole gli spec del giro completo con `afterwards(…)` al posto
      del `finally`, e il piano 1.30, #229). `events-slots.spec.ts` ora dice con `afterwards(…)` che cosa rimette a posto, come chiede la
      regola nuova di `CONTRIBUTING.md`; `events-public.spec.ts` ed `events-staff.spec.ts`, di E4 e di prima, restano col `finally`
      (nell'HANDOFF).
  - **Al contrario** (6 ottobre 2026): con il margine della finestra a dieci anni, il test della finestra cade (la tabella con le due righe
    fuori è caricata: `OK: {"added":2,"removed":0}`); con l'ordine di prima (`OffBlockUtc ?? OnBlockUtc`), cade quello dell'esportazione
    (`Expected: "XEA501"`, `Actual: "XEA503"`); senza il filtro della versione sull'esportazione, la richiesta senza intestazione riceve
    il 409 della bozza invece del 400 (`Expected: BadRequest`, `Actual: Conflict`). Il codice rimesso com'era e ricompilato: tutti e tre
    di nuovo verdi. Il test del volo
    ricaricato prova un comportamento che c'era già, e il 409 ristretto non ha un errore del database diverso da provocare in un test:
    nessuna prova al contrario per i punti 3 e 4.
  - **Verificato di nuovo** (7 ottobre 2026, sull'ultimo commit, con E10g e `main` dentro): `dotnet build` della soluzione senza avvisi e
    `dotnet format --verify-no-changes` sui file C# del giro; unità **1169/1169** (`EventsSlotsTests` 36, `ContractVersionTests` 21);
    **integrazione intera senza filtro 486/486** (8,6 minuti), `EventsSlotsTests` 5/5; `pnpm gen:api` (il 400 dell'esportazione) e `pnpm
    i18n:sync` (la parola nuova) rifatti; `pnpm lint`, `typecheck`, `format:check`, `i18n:check` verdi; `pnpm test` **629 in 86 file**;
    `pnpm e2e --workers=2` dietro il lock dello smoke: **171/172** al primo giro — è caduto `closed-suggestion.spec.ts:121`, del nucleo, la
    barra trascinata con `scrollTop` 0, lo stesso di A13d —, poi quello spec da solo `--repeat-each=5` 5/5 e lo smoke intero di nuovo
    **172/172**; **`pnpm e2e:full` 57/57 al primo giro** (11,9 minuti, il suo worker solo) sul banco `http://127.0.0.1:5126`
    (`ivaohub_e2e_e5` ricreato prima), dietro il lock di Mailpit; le regole di `core-guard` in PowerShell dalla base di merge `e9702b2`:
    **PASS** (nessun file del maintainer;
    del nucleo `ContractVersion.cs`, della #230, ed `ErasureTests.cs`; tre note nuove).
- **La CI della seconda spinta** (`d901f43`): verde, `build-test` (24 minuti e 32 secondi) e `core-guard`.
- **Dopo la prova sul banco** (7 ottobre 2026). Dalberone ha guardato E5 sul banco di prova — la build di E5 sulla 5090, un evento su
  LIRF e LIMC, quattro slot incollati con una rotazione, pubblicato — e ha riaperto la #228 per la pagina degli slot, da fare in E5 e
  non in E6b. Il messaggio è arrivato dalla sessione che coordina. Sono comportamenti che il design non dice, scelti da lui come chi tiene
  il modulo: nota nuova **«Proposta»** `decisions/2026-10-07-gli-slot-sulla-pagina-dell-evento.md`, con
  [la domanda a Carmine sulla #228](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/228#issuecomment-6039543785);
  la nota decisa di E5 non si tocca, e la sua lettura 8 cambia (§5 della nota nuova). Fatti come la nota raccomanda:
  1. **Il tipo principale**: il primo di `aircraft_types`, senza migrazione. Il form di uno slot ha due campi, «Tipo principale» e
     «Altri tipi», salvati principale per primo (`SlotValues.MainFirst`), con ogni rifiuto sul suo campo; la pagina del caricamento dice
     che nella cella `A320/A20N` il primo è il principale.
  2. **La colonna del tipo** mostra il principale; gli altri compaiono nel tooltip di Atmosphere al passaggio del mouse, al focus e al
     tocco.
  3. **«Partenze» e «Arrivi»** in due tabelle, rispetto allo scalo dell'evento, per l'orario allo scalo.
  4. **Una sezione per scalo** quando gli slot sono a più d'uno; il volo fra due scali dell'evento resta una partenza del primo.
  5. **Le tratte di una rotazione** stanno ognuna nella sua tabella, segnate da un'icona `Repeat` che lo dice.
  6. **Una riga apre lo slot** in sola lettura, in un dialog di Atmosphere: tutti i tipi, gli orari, lo stand, le tratte della
     rotazione. È dove E6b metterà «Prenota»; nessun endpoint e nessun campo nuovo.
  7. **`aircraft_types` nell'esportazione.** La richiesta diceva che l'esportazione «tiene la lista in ordine», ma non la portava:
     chiesto a dalberone, ha scelto il campo nuovo, un'aggiunta alla versione 1. `docs/events-bookings-export.md` lo dice.
  - ⚠️ **Il tocco, misurato prima di scriverlo.** Il tooltip di Radix si apre solo al passaggio del mouse e al focus. Il bottone che lo
    porta rovescia, al click, quello che si vedeva **quando la pressione è cominciata**. **Al contrario**: con un semplice «al click si
    rovescia» la smoke su un telefono (`hasTouch`, Chromium) cade sul secondo tocco, che lascia il tooltip aperto (`Expected: 0`,
    `Received: 1`); rimesso, la smoke passa.
  - **Verificato di nuovo** (7 ottobre 2026, sull'ultimo commit):
    - `dotnet build` della soluzione senza avvisi; `dotnet format --verify-no-changes` sui file C# del giro;
    - unità **1174/1174**, con `EventsSlotsTests` 41;
    - **integrazione intera senza filtro 486/486** (9,1 minuti), con `EventsSlotsTests` 5/5;
    - `pnpm gen:api` (i due campi del form) e `pnpm i18n:sync` rifatti; `pnpm lint`, `typecheck`, `format:check`, `i18n:check` verdi;
    - `pnpm test` **636 in 87 file**, con `slotList.test.ts` 6 ed `EventSlots.test.tsx` 5;
    - la smoke degli eventi da sola 10/10, poi `pnpm e2e --workers=2` **173/173** al primo giro, dietro il lock dello smoke;
    - **`pnpm e2e:full` 57/57 al primo giro** (11,7 minuti) sul banco `http://127.0.0.1:5126` (`ivaohub_e2e_e5` ricreato prima), dietro
      il lock di Mailpit, preso dopo 80 secondi di attesa per il giro di E6a;
    - le regole di `core-guard` in PowerShell dalla base di merge `e9702b2`: **PASS** (quattro note nuove).
- **Il merge di E10g dopo E4b** (7 ottobre 2026, `ce110ba`). La #226 (E4b) è entrata in `main` mentre la CI di `25d23f5` girava, e la
  #228 è diventata «CONFLICTING»: il branch di E10g che porta andava in conflitto con `main` su `HANDOFF-M4.md`. Come ha chiesto la
  sessione che coordina, nessun merge di `main` da solo: la sessione di E10g ha unito `main` (`7b84a75`) e le risposte di Carmine sulla
  #230, ed E5 ha unito la sua testa `477a0f8` in un merge solo. Il conflitto era il solo `HANDOFF-M4.md`: l'intestazione di E5, e in «Lo
  stato» il paragrafo di E5, poi quello di E10g, poi quello di E4b. Il merge porta, oltre a `main`, solo documenti di E10g, e il codice
  di E4b, verde su `main`; quindi, come ha chiesto la sessione che coordina, solo i controlli che quel codice tocca:
  - `dotnet build` senza avvisi; unità **1182/1182**;
  - `pnpm gen:api` e `pnpm i18n:sync` senza differenze; `pnpm lint`, `typecheck`, `format:check`, `i18n:check` verdi;
  - `pnpm test` **640 in 88 file**; la smoke **173/173** al primo giro;
  - `core-guard` dalla base nuova `7b84a75`: **PASS**.

  L'integrazione intera e il giro completo restano quelli dell'ultimo codice di E5, sopra.
- **La CI della terza spinta** (`ecf88b9`): verde.
- **La seconda lettura e le risposte di Carmine** (7 ottobre 2026). [La seconda lettura del revisore][v228b]: il giro di revisione è fatto
  come chiesto, e i commit dopo il banco sono «approvable on the code» con due correzioni. [Le risposte di Carmine][a228b] (autore
  `SkyMistery`, pubblicate dal master su sua istruzione): **sì ai sette punti** della nota del 7 ottobre, ora **decisa** — il punto 5
  sostituisce, sapendolo, la lettura 8 della nota del 6 ottobre —, e **un ottavo**: `Hint` resta un pezzo di questa schermata. Il giorno
  che una seconda schermata vorrà un tooltip che si apre al tocco, è una decisione da portargli, non una copia. Fatto, 7 ottobre 2026:
  1. **Il focus quando il dialog si chiude** (da correggere, punto 1): `SlotDetail` monta il `Dialog` aperto e senza un bottone suo, quindi
     Radix non aveva niente a cui rendere il focus. `EventSlots` tiene in `openedBy` il nominativo che ha aperto lo slot — il bottone,
     oppure quello della riga cliccata — e glielo rimette quando il dialog è sparito: dentro, la trappola del focus lo riprenderebbe.
  2. **Il form a due campi nel browser** (da correggere, punto 2): il giro completo apre lo slot caricato come `A320/A20N`, legge A320 in
     «Main type» e A20N in «Other types», scrive un tipo che l'hub non conosce fra gli altri e vede il rifiuto sotto quel campo, e niente
     sotto il principale. Per tornare alla scheda il giro va al suo indirizzo: le parole comuni degli spec (`web/e2e/locales.ts`, del
     nucleo) non hanno «Cancel».
  3. **`shownAtPress` di una pressione che non diventa un click** (basso, punto 3): un click della tastiera (`detail` 0) rovescia quello che
     si vede, senza guardare una pressione vecchia; una pressione annullata (`pointercancel`, uno scorrimento) si dimentica.
  4. **La frase della rotazione detta due volte** (basso, punto 4): con un nome suo, il bottone non ha più il tooltip come descrizione
     (`aria-describedby` tolto), e chi legge lo schermo la sente una volta.
  5. **Una selezione nella riga** (basso, punto 5): resta l'intera riga ad aprire lo slot, come chiesto e deciso (punto 6), perché su un
     telefono è il bersaglio del pollice; ma un click che chiude una selezione di testo non apre niente.
  - Il punto 8 della seconda lettura — un volo fra due scali dell'evento manca dagli arrivi del secondo — è la risposta 4 di Carmine:
    niente da cambiare.
  - **Al contrario**: tolte insieme le quattro correzioni del codice (1, 3, 4, 5), in `EventSlots.test.tsx` cadono esattamente i loro
    quattro test, 4 su 8. Rimesse, 8 su 8. Il test della selezione è caduto anche una volta per sé: una selezione prende un intervallo
    solo quando non ne ha, e un click di un test prima lascia un cursore. Ora la selezione si svuota prima.
  - **Verificato di nuovo** (7 ottobre 2026, sull'ultimo commit; solo il browser è cambiato):
    - `pnpm test` **643 in 88 file**, con `EventSlots.test.tsx` 8; `pnpm lint`, `typecheck`, `format:check`, `i18n:check` verdi;
    - la smoke degli eventi da sola 10/10 (il focus dopo Escape in Chromium), poi `pnpm e2e --workers=2` **173/173** al primo giro;
    - lo spec `full/events-slots.spec.ts` da solo **1/1** sul banco `http://127.0.0.1:5126`, con il web ricostruito, dietro il lock di
      Mailpit, preso dopo 160 secondi di attesa per E6a. Il giro completo intero no: il resto non è cambiato, e il lock era conteso;
    - le regole di `core-guard` in PowerShell dalla base di merge `7b84a75`: **PASS**.

    Server, unità e integrazione non sono toccati da questo giro: restano 1182/1182 e 486/486.

[a228b]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/228#issuecomment-6040717010
[v228b]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/228#issuecomment-6040474527

[a228]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/228#issuecomment-6022686808
[v228]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/228#issuecomment-6021830879

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

**Com'è andata** (30 settembre 2026, branch `m4/e10a-tracker-without-vid`, PR #210, del nucleo senza coda, da `main` a `c107c98`):

- **Misurato prima del codice** (nota nuova `2026-09-30-il-tracker-senza-vid`, §2), con il token dell'applicazione e script fuori dal
  repository che stampano solo forme, conteggi e tempi: **il token dell'applicazione basta** per ogni lettura (senza VID, per VID con
  `connectionType`, piani e tracce di una sessione trovata senza VID); `perPage` fino a **100** (101 → 400); la finestra filtra
  **l'inizio** della sessione, estremi compresi; un aeroporto da solo si trova in **una revisione qualunque** del piano, due insieme
  nella **stessa**; i tipi di connessione sono `PILOT`, `ATC`, `OBS`, `FOLME`; ogni riga porta l'oggetto `user` con **nome e
  cognome**; nessun rate limit dichiarato, il gateway rinuncia a 15 s; e **la pagina con l'ultima riga di una domanda per aeroporto
  costa a IVAO ~10–11,5 s**, piena o no (le altre 0,2–0,6 s, per VID 65–118 ms), e dieci insieme ricevono 504. Sul codice di `main`
  la pipeline dell'hub tagliava ogni tentativo a 10 s: una ricerca per aeroporto finiva sempre in una `TimeoutRejectedException`
  lanciata dopo 30 s.
- **Fatto** (scelta tecnica, nessuna domanda nuova):
  - `IvaoSessionQuery` con il **VID facoltativo**, `ConnectionType` (`IvaoConnectionType`: `Pilot`, `Atc`, `Observer`,
    `FollowMe`) e il **`Limit` di chi chiama** (`init`, almeno 1, predefinito 200, il tetto di prima); `PageSize` 100 e `PerPage`
    (`min(100, Limit)`); `IvaoTrackerSessionDto.ConnectionType`, una proprietà `init`;
  - nel lettore unico, **il giro delle pagine** (`IvaoTrackerReader.ReadPagesAsync`: dalla più recente, senza doppioni, fino
    all'ultima pagina, a una pagina vuota o al limite; `null` se una pagina manca) e **la regola del tracker** (`Answers`); il client
    vero chiede le pagine con `ReadOrNothingAsync`, quello delle fixture risponde senza VID da `tracker-airport-<ICAO>.json` con la
    regola e l'ordine di IVAO;
  - **il gestore del client dei dati aspetta 20 s per tentativo** (più dei 15 del gateway di IVAO), campiona l'interruttore su 40 s
    (la sua regola: almeno due tentativi), e il totale resta 30 s;
  - `tools/record-ivao-fixtures.mjs --sessions-at <ICAO> <from> <to> <firstVid> <lastVid>`, con le persone tolte (l'oggetto `user`,
    una VID per membro, i callsign dei piloti, gli identificativi rinumerati) e un ultimo controllo che non scrive se resta una VID
    vera o un nome; il tentativo ripetuto delle postazioni diventa di tutte e due le modalità (`getPatiently`);
  - **le fixture** `tracker-airport-LIRF.json` e `tracker-pages-LIRF.json`: LIRF, una sera fra le 16:00 e le 17:59:59 UTC (dopo la
    revisione spostata sul 1° gennaio 2001: qui sotto), 10 sessioni
    di 9 membri come VID 761020–761028 (uno collegato due volte, un volo di 38 s, la torre, e un piano passato da LIPZ→LIRF a
    LIRF→LICR); la loro sezione in `tests/fixtures/ivao/README.md`;
  - **i test**, di unità: `IvaoTrackerWithoutVidTests` (16: le pagine registrate, la finestra vuota, la pagina vuota, l'errore a
    metà, il limite, oltre 200, i doppioni, la regola misurata, le parole del tracker, il client delle fixture, e il client vero con
    un IVAO recitato dal test — la domanda senza `userId` a pagine da 100, e una pagina che non arriva che dà `null`) e
    `IvaoApiTimeoutTests` (1: il tempo per tentativo e la regola del gestore).
- **Scostamenti** dal piano e dal design, ognuno nella nota:
  1. ⚠️ **Il tempo per tentativo del client di IVAO passa da 10 a 20 s per tutte le chiamate** (nota §3.4): il piano non lo
     chiedeva, ma senza nessun aeroporto si legge. Il totale resta 30 s, quindi una chiamata che non risponde costa quanto prima.
  2. ⚠️ **Una ricerca del tracker a cui IVAO non risponde affatto ora dà `null` invece di lanciare** (nota §3.2): per i tour,
     «tracker non disponibile» invece di un 500 — quello che il commento dell'interfaccia prometteva già.
  3. **Le pagine da 100 anche per i tour**, e il client delle fixture ordina dalla più recente con la regola delle revisioni: per le
     fixture dei tour il risultato non cambia (nessuna loro sessione ha aeroporti diversi fra una revisione e l'altra: controllato).
  4. **Le fixture tolgono più della persona** di quelle dei tour — i callsign dei piloti e gli identificativi —, perché sono
     sconosciuti (nota §3.5).
  5. **Nessun test d'integrazione nuovo**: «i tour invariati» sono i loro test, che ci sono, verdi e non toccati; la domanda senza VID
     del client delle fixture — lo stesso oggetto che serve il banco — è provata in unità.
- **Trovato, e scritto per chi viene dopo** (E13a):
  1. ⚠️ **Ogni domanda per aeroporto che trova qualcosa costa ~10,5 s, e due lente insieme ricevono 504**: il job chiede una domanda
     alla volta, poche per giro.
  2. ⚠️ **La finestra è sull'inizio della sessione**: per chi era già connesso all'inizio dell'evento, `FromUtc` va allargato.
  3. ⚠️ **«Partenza o arrivo» sono due domande**: chiesti insieme, i due aeroporti vogliono la stessa revisione; una sessione che
     torna da tutte e due si conta una volta (`Id`).
  4. ⚠️ **Il DTO porta gli aeroporti della prima revisione**: la sessione trovata per la partenza da LIRF può dire LIPZ.
  5. **`callsign` filtra per l'inizio del nominativo** (misurato, non usato): se un giorno servirà chi ha aperto una postazione senza
     conoscerne il VID.
- **Verificato, in locale** (30 settembre 2026, sul branch, prima del commit dei documenti): `dotnet build` senza avvisi; unità
  **902/902** (17 nuove); **integrazione intera senza filtro 430/430** (6,9 minuti); `dotnet format --verify-no-changes` sui sette
  file C#; in `web/` `pnpm lint`, `typecheck`, `format:check`, `i18n:check` verdi, `pnpm test` 594 in 80 file, `pnpm gen:api` senza
  differenze. Due prove al contrario: con il campionamento dell'interruttore a 30 s la lettura delle opzioni lancia
  (`IvaoApiTimeoutTests` rosso), e con `ReadAsync` al posto di `ReadOrNothingAsync` il test della pagina che non arriva cade con
  l'eccezione. **Contro IVAO vero**, con un programma fuori dal repository costruito su `AddIvaoIntegration()` (non un test): sul
  codice di `main` la ricerca per aeroporto finisce in una `TimeoutRejectedException` dopo 30 s; sul codice nuovo gli arrivi di LIRF
  in una sera (13) in 12,0 s, le partenze di EDDF in una settimana (**305**, oltre 200) in 12,6 s, con `Limit` 250 in 1,75 s, e una
  ricerca per VID come quelle dei tour in 118 ms. Le regole di `core-guard` rifatte in PowerShell sul diff dalla base: nessun file
  del maintainer, sette del nucleo, la nota nuova.
- **Dopo il merge di `main`** (E10b, #208, unita mentre questa PR partiva; la PR era nata in conflitto, e la sessione di coordinamento
  ha chiesto il merge perché la CI girasse): il conflitto era solo in `HANDOFF-M4.md`, l'intestazione e la cima di «Lo stato»,
  tenuti tutti e due i paragrafi con E10a sopra; `10` si è unito da sé. Rifatti: build senza avvisi, unità **902/902**,
  **integrazione intera senza filtro 435/435** (7,5 minuti, i test di E10b compresi), le regole di `core-guard` dalla base nuova
  (uguali: sette file del nucleo, la nota). Il web non è toccato dal merge.
- **Dopo la revisione** ([osservazioni del revisore sulla #210][r210], «approvabile dopo due correzioni»; fatte il 1° ottobre 2026):
  1. **Le fixture su un giorno inventato**: la data vera, al secondo, con l'aeroporto, ritrovava su IVAO le sessioni vere, e con
     loro VID e nome. Lo script sposta tutta la sera sul **1° gennaio 2001** (il tracker non ha sessioni nel 2001: misurato, 0 in
     tutto l'anno; lo stesso giorno delle prenotazioni di E15a), ogni istante con lo stesso scarto, e toglie `rating`, `serverId` e
     `software*`; il suo controllo finale rifiuta i campi tolti e i giorni veri. Registrata di nuovo dalla stessa finestra: stesse
     sessioni, stesse VID, stessi identificativi. I test leggono il giorno nuovo, e uno controlla la data e i campi nel file. La data
     vera è tolta anche da questi documenti. ⚠️ **La prima registrazione resta nella storia del branch** (il commit `92c7a84`, il suo
     messaggio e il primo «Com'è andata» hanno la data vera, e le fixture di allora `rating`, `serverId` e il software): la storia
     spinta non si riscrive, e come unire la #210 è una scelta del master; scritto sulla PR.
  2. **Un tetto sul `Limit`**: `MaxLimit` = 1000, tenuto dal nucleo nell'`init`, con il suo test.
  3. **Carmine ha detto sì** alle due domande del revisore — i 20 s per tentativo (campionamento 40, totale 30) e `null` per la
     ricerca dei tour quando IVAO non risponde ([risposta sulla #210][ok210]) —, registrato nella nota (§6).
  4. **`main` unito di nuovo** (E10e #206, il passaggio dei tour al calcolo del nucleo #211, E10d #205): il conflitto era solo in
     `HANDOFF-M4.md`; l'intestazione di E10a tiene anche lo stato di `main`, e restano i paragrafi di tutte le fasi. Rifatti: build
     senza avvisi, unità **920/920**, **integrazione intera senza filtro 437/437** (7,8 minuti), in `web/` `pnpm lint`,
     `typecheck`, `format:check`, `i18n:check` verdi, `pnpm test` 594, `pnpm gen:api` senza differenze, `dotnet format` sui file
     toccati, le regole di `core-guard` (sette file del nucleo, la nota). E15a (#207) è ancora aperta: se entra prima, si unisce di
     nuovo.
  5. **`main` unito ancora, dopo E2** (#209), come ha chiesto il revisore
     ([commento sulla #210](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/210#issuecomment-5926812818)), con lo strumento
     dell'app che porta il branch al passo con la base: il conflitto era di nuovo solo in `HANDOFF-M4.md`, tenuti i paragrafi di E10a
     ed E2 e nell'intestazione lo stato di tutte e due. Rifatti: build senza avvisi, unità **954/954**, **integrazione intera senza
     filtro 441/441** (5,6 minuti, i test di E2 compresi), in `web/` `pnpm lint`, `typecheck`, `format:check`, `i18n:check` verdi,
     `pnpm test` 599, `pnpm gen:api` senza differenze, le regole di `core-guard` (uguali).
  6. **`main` unito dopo E15a** (#207), che tocca gli stessi file: E10a è la seconda, e i conflitti erano suoi, come ha chiesto il
     revisore ([commento sulla #210](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/210#issuecomment-5928517622)). Quattro file:
     il commento di `ReadOrNothingAsync` in `IvaoApiClient.cs` (ora nomina le postazioni, le prenotazioni di E15a e le pagine del
     tracker); lo script, con **tutte e due le modalità** (`--bookings` e `--sessions-at`) nel controllo degli argomenti e nell'uso; il
     README delle fixture, con le due sezioni; l'handoff, con i paragrafi di tutte le fasi. `IIvaoApiClient.cs`,
     `FixtureIvaoApiClient.cs` e `IvaoServiceCollectionExtensions.cs` si sono uniti da soli, con le prenotazioni accanto alla ricerca.
     **Il giorno inventato era scritto due volte** nello script (le stesse tre righe nel blocco di E15a e nel mio): ora è uno,
     `standIn` con `movedFrom(day)` in cima, usato da tutte e due. Rifatti: build senza avvisi, unità **979/979** (i test delle
     prenotazioni compresi), **integrazione intera senza filtro 441/441** (5 minuti), `node --check` dello script e il rifiuto degli
     argomenti mancanti delle due modalità, e **LIRF registrata di nuovo con lo script unito: le fixture escono identiche** byte per
     byte (quella di E15a non si può registrare di nuovo: il suo giorno vero non è scritto da nessuna parte, e il cambio nel suo blocco è
     la stessa formula spostata); le regole di `core-guard` (uguali). Il merge non tocca il web.
- **Non verificato**: la CI (la dice la PR); un chiamante vero della domanda senza VID, perché il job di E13a non c'è ancora; una sera
  di RFE vera (la più grande misurata: EDDF in una settimana, 305 sessioni); IVAO sotto il carico della sera di un evento — se la
  pagina lenta passasse i 15 s, il gateway risponderebbe 504 e la ricerca `null`, e il giro dopo del job riproverebbe —; una chiamata
  a cui IVAO vero non risponde affatto, con i 20 s per tentativo (in unità sì, con la pagina che non arriva); la pagina del pilota
  dei tour quando IVAO non risponde (ora «tracker non disponibile»: provato sul client, non sulla pagina); `pnpm e2e` ed `e2e:full`,
  perché nessuna schermata cambia — il client delle fixture, che il banco usa anche per i tour, è provato in unità con la fixture dei
  tour (780001), non sul banco.

[r210]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/210#issuecomment-5917019730
[ok210]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/210#issuecomment-5917033792

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

**Com'è andata** (30 settembre – 1° ottobre 2026, branch `m4/e10c-ratings-and-positions`, PR #204, del nucleo senza coda, da `main` a
`c107c98`, con `main` preso due volte con un merge: E10b, poi E10d ed E10e):

- **Prima del codice, da dove vengono le regole** (nota nuova `2026-09-30-il-rating-preferito-e-il-minimo-di-una-postazione`, §2):
  le «Regulations» di IVAO (ATC Operations, A.1) dividono le postazioni per servizio, ma **nessuna regola di IVAO dice un rating
  «preferito»**: è la regola dell'ED; **il minimo di una postazione è il suo FRA**, una regola di IVAO per postazione, con giorni, ore
  o una data, che l'API `core` dà (`/v2/fras`). Misurati per l'Italia con il token dell'applicazione: 392 righe, 353 per postazione su
  202 postazioni e 39 eccezioni per membro; minimi da AS1 a CAI, che cambiano con l'ora e il giorno. La nota era **«Proposta»**, con
  due domande sulla PR ([domande][q204]); il codice che ne dipendeva ha aspettato.
- **Deciso da Carmine** il 30 settembre, in chat al master, che ha pubblicato le risposte sulla #204 su sua istruzione
  ([risposte][ok204]): **il minimo è l'FRA**, come raccomandato, **con un'aggiunta** — chi fa i turni può andare sotto l'FRA, e l'hub
  gli dice di toglierlo su IVAO per quel controllore: un avviso, mai un rifiuto —; **i preferiti**: AS3 su `DEL`, APC su `DEP`, ADC su
  `FSS` (la raccomandazione era ADC, APC, nessuno), `ATIS` nessuno.
- **Fatto**:
  - **il vocabolario**: `Rating.PreferredOn` e `RatingVocabulary.PreferredFor(tipo)`, con i dati di Carmine; un tipo su due rating
    è rifiutato;
  - **la directory**: `OfDivisionAsync()`, `FindAsync(nominativi)` e il tipo in `AtcPositionDto`; `MinimaAsync(nominativi)` con
    `AtcPositionMinimum.Over(da, a)`, il più alto minimo attivo nella finestra; una query sola «della divisione» per le domande delle
    postazioni;
  - **gli FRA nel nucleo**, come le postazioni in A2: `ref_ivao_fras` (migrazione `AddIvaoFras`), `IIvaoApiClient.GetFrasAsync` con
    il corpo predefinito vuoto, il client vero a pagine (`members=false&expand=true&perPage=100`: una pagina che fallisce vale nessuna
    risposta), un lettore solo per i due client (`Core/Ivao/IvaoFra.cs`), la sincronizzazione notturna dopo le postazioni, con il
    conteggio nel messaggio e la potatura solo su una risposta piena;
  - **le parole dell'avviso** nel nucleo, `atcPositions.belowMinimum` in `locales/*/common.json` (`AtcPositionMinimum.BelowMinimumKey`);
  - **le misure**: `tools/record-ivao-fixtures.mjs --fras`, la fixture `fras-IT.json` (94 FRA delle postazioni del banco, registrata
    il 30 settembre con il client OAuth di `dalberone`, con il suo permesso), il README delle fixture, `docs/FORKING.md`.
- **Scostamenti**:
  1. **Il minimo lo dice la directory, non il vocabolario** (design §1.13): è un dato di una postazione, che cambia con l'ora, non
     una regola di un tipo. Deciso con la risposta 1.
  2. **La fase è cresciuta** di una tabella di riferimento, una chiamata di IVAO e una modalità dello strumento delle fixture, come
     A2: il piano la dava come un'estensione del vocabolario.
  3. **Una chiave del nucleo per l'avviso**, che nessuno legge ancora: le sue parole nominano IVAO, e il modulo non può scriverle
     (`CLAUDE.md` §3); le usa E11b.
  4. **`FindAsync` risponde a lotti** (un dizionario per nominativo), come `IAirportDirectory.FindAsync`: serve al form (un
     nominativo) e al proponente (tutti quelli di un evento).
  5. **`AtcPositionTests`** (scritto in A2 da questa stessa mano): i tre DTO attesi prendono il tipo; nessuna asserzione tolta.
  6. **La migrazione è stata rifatta sopra quella di E10d** (`AddAwardSignalNotifiedAt`): le due fasi migrano il contesto del nucleo, e
     E10d è stata unita prima.
- **Trovato, e scritto per chi viene dopo**:
  1. ⚠️ **IVAO manda il VID di un membro due volte** in una riga di un FRA (`userId` e `user_id`): chi toglie le persone da una
     risposta di IVAO le toglie tutte e due. Lo strumento e il lettore lo fanno.
  2. **Le forme vere non sono quelle della documentazione**: gli orari `23:00:00` (non `23:00`), la data `2026-09-12` (non una data con
     l'ora); il nominativo arriva solo con `expand=true`, perché la riga nomina la postazione con l'identificativo di IVAO.
  3. **IVAO ha il suo controllo** «questo VID può aprire questa postazione ora?» (`/v2/fras/check/{callsign}/{vid}`): esatto, ma una
     chiamata per candidato; non usato (nota §4).
  4. **Il nome di IVAO dell'ACC è «Centre Controller»** (il vocabolario lo scrive così dal 25 settembre), non «Area Control Centre».
  5. **La sessione si è fermata a metà** la sera del 30 settembre (il limite d'uso dell'account) e ha ripreso il 1° ottobre.
- **Verificato, in locale**: il 30 settembre, sulla prima metà (vocabolario con i quattro tipi decisi e directory): `dotnet build` senza
  avvisi, unità 911/911, **integrazione intera senza filtro 433/433** (505 s), `pnpm lint`, `typecheck`, `format:check`, `i18n:check`,
  `pnpm test` 594 in 80 file, `pnpm gen:api` senza differenze. Il 1° ottobre, dopo il merge di E10d ed E10e e con tutto E10c: `dotnet
  build` senza avvisi; unità **984/984**; **integrazione intera senza filtro 444/444** (492 s); `AtcPositionTests` da sola 13/13; `pnpm
  lint`, `typecheck`, `format:check`, `i18n:check` verdi; `pnpm test` 594 in 80 file; `pnpm gen:api` senza differenze; `dotnet format
  --verify-no-changes` sui file C# toccati; le regole di `core-guard` rifatte in PowerShell sul diff dalla base del merge.
- **Non verificato**: la CI (la dice la PR); **il fuso degli orari degli FRA** (letti UTC, come ogni orario di IVAO: nessuna fonte lo
  dice); come IVAO combina due FRA che valgono insieme (il più alto, deciso da Carmine); il giro notturno delle 03:15 con IVAO vero
  (provati il client delle fixture, la lettura a pagine con un IVAO finto e la sincronizzazione chiamata dai test); una divisione con
  più di 5000 FRA; `pnpm e2e` ed `e2e:full`, perché nessuna schermata cambia.
- **Dopo la revisione** (1° ottobre 2026; [rilievi del revisore][r204], «approvabile tranne il punto 1, che va al maintainer»):
  1. **I rating preferiti sono la regola di una divisione, scritta nel vocabolario di IVAO del nucleo** (il rilievo 1). **Deciso da
     Carmine**, in chat al master che l'ha pubblicato su sua istruzione ([seconda risposta][ok204b]): **vanno in `config/division.json`**.
     Fatto: la chiave `preferredAtcRatings` (tipo di postazione → sigla del rating ATC, `DivisionOptions.PreferredAtcRatings`), con i
     valori di Carmine nel file di IT e la spiegazione per chi forka nel file d'esempio; **`PreferredAtcRatingsValidator`** in
     `Core/Ivao/` ferma l'avvio su un tipo che IVAO non usa (`IvaoAtcPosition.Kinds`, i suoi otto tipi) o su un rating che non è della
     scala ATC, con il file, la chiave e i valori possibili; il vocabolario registrato è la scala di IVAO con la mappa
     (`IvaoRatings.WithPreferred`), e risponde `PreferredFor` come prima; `Rating.PreferredOn` non c'è più. `division.xx.json` non
     cambia: il fork «XX» non mette nessuno per primo.
  2. **«IVAO non ha risposto» e «la divisione non ha FRA» erano la stessa lista vuota** (il rilievo 2, basso): una divisione che
     toglie tutti i suoi FRA li avrebbe tenuti per sempre. **Corretto**: `GetFrasAsync` risponde `null` quando IVAO non risponde (una
     pagina fallita, una risposta che non è una pagina, oltre 50 pagine; anche il corpo predefinito dell'interfaccia e il client delle
     fixture senza il file di un paese), e una lista vuota quando la divisione non ne ha; la sincronizzazione lascia la tabella su
     `null` e la svuota sulla lista vuota, e il messaggio del giro dice quale dei due. `docs/FORKING.md` lo dice a chi forka.
  3. **`main` è entrato di nuovo con un merge** (E2, #209): il conflitto su `HANDOFF-M4.md` risolto tenendo i paragrafi di tutte e due le
     fasi, E10c sopra.
  - **I test**: `PreferredAtcRatingsValidatorTests` (unità, nuovo: i tre file della divisione passano; un tipo o un rating sconosciuti
    fermano l'avvio, tutti gli errori insieme); `RatingVocabularyTests` legge la regola dal file di IT e prova che la scala di IVAO da
    sola non mette nessuno per primo, e che i tipi del nucleo sono quelli delle fixture del mondo; `IvaoFraReaderTests` con la
    risposta vuota vera e la risposta che non è una pagina; `AtcPositionTests` con la divisione che toglie tutti i suoi FRA, il
    vocabolario dell'hub che dice la regola di IT e un host con una mappa sbagliata che non parte.
  - **Verificato, in locale, sul merge con E2**: `dotnet build` senza avvisi; unità **1035/1035**; **integrazione intera senza filtro
    451/451** (314 s); `AtcPositionTests` da sola 16/16; `pnpm lint`, `typecheck`, `format:check`, `i18n:check` (784 chiavi) verdi; `pnpm
    test` 599 in 81 file; `pnpm gen:api` senza differenze; `dotnet format --verify-no-changes` sui file C# toccati; le regole di
    `core-guard` in PowerShell dalla base del merge.
  - **Non verificato**, in più: un avvio vero di un'installazione con una mappa sbagliata (provato con un host dei test che riceve la
    mappa per la stessa via del file).
  - **`main` ancora, con E15a** (#207, unita mentre la PR era in revisione): un merge con quattro conflitti — la registrazione dei
    servizi di IVAO (le prenotazioni di E15a accanto al vocabolario con la mappa e al suo validatore), lo strumento delle fixture (le
    modalità `--bookings` e `--fras` tutte e due), il README delle fixture (le due sezioni) e `HANDOFF-M4.md` (i paragrafi di tutte le
    fasi, E10c sopra); `IIvaoApiClient`, `IvaoApiClient` e `FixtureIvaoApiClient` si sono uniti da soli, con i due membri nuovi. E15a non
    migra il contesto del nucleo: `AddIvaoFras` resta com'è. Sul merge: `dotnet build` senza avvisi; unità **1060/1060**; **integrazione
    intera senza filtro 451/451** (312 s); `pnpm lint`, `typecheck`, `format:check`, `i18n:check` verdi; `pnpm test` 599 in 81 file; `pnpm
    gen:api` senza differenze.
  - **E poi E10a** (#210, unita il 1° ottobre; il revisore ha chiesto il merge [sulla PR][m204]): due conflitti — lo strumento delle
    fixture, che ora ha le modalità di tutte e tre le fasi (`--bookings`, `--fras`, `--sessions-at`: l'intestazione, le variabili, il
    controllo degli argomenti, il messaggio d'uso e i due blocchi, ognuno con il suo `process.exit`), e `HANDOFF-M4.md` (i paragrafi di
    tutte le fasi, E10c sopra). Il client di IVAO, il README delle fixture e la registrazione dei servizi si sono uniti da soli. E10a non
    migra il contesto del nucleo. Sul merge: `dotnet build` senza avvisi; unità **1077/1077** (con i test di E10a sul tracker e i miei sulle
    fixture degli FRA); **integrazione intera senza filtro 451/451** (357 s); lo strumento si legge (`node --check`) e rifiuta gli argomenti
    mancanti di `--fras` e `--sessions-at`. Il web non cambia con questo merge: valgono i giri sul merge con E15a.
  - **E infine E10f** (#213, `Awards.Assign` dato con un grant; il revisore ha chiesto il merge [sulla PR][m204b]): un conflitto solo,
    `HANDOFF-M4.md` (i paragrafi di tutte le fasi, E10c sopra). `config/division.json` e `division.example.json` si sono uniti da soli
    e portano tutte e due le cose — il grant di `Awards.Assign` all'MD di E10f e i rating preferiti di E10c —, come `docs/FORKING.md`.
    E10f non migra il contesto del nucleo. Sul merge: `dotnet build` senza avvisi; unità **1093/1093** (i test dei file della divisione
    compresi: il validatore dei preferiti e i grant degli eventi); **integrazione intera senza filtro 455/455** (298 s); `pnpm lint`,
    `typecheck`, `format:check`, `i18n:check` verdi; `pnpm test` 601 in 82 file; `pnpm gen:api` senza differenze.

[q204]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/204#issuecomment-5915876615
[ok204]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/204#issuecomment-5916282164
[r204]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/204#issuecomment-5926667625
[ok204b]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/204#issuecomment-5926811688
[m204]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/204#issuecomment-5929486656
[m204b]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/204#issuecomment-5930060106

### E10d — Nucleo: la mail a chi assegna gli award

Design §5.4, §8.3, §13 n.5; nota `dopo-l-evento-e-gli-award`. Branch `m4/e10d-award-assigner-mail`. **PR del nucleo**, con la sua
nota.

1. **Un tipo di notifica del nucleo**: quando entra un segnale nuovo nella coda degli award, chi ha `Awards.Assign` riceve una mail;
   spegnibile dal profilo; **vale anche per i tour**. Una mail per segnale, o un riepilogo: lo dice la nota, con la domanda a Carmine
   se la forma non è ovvia.

**Test**: integrazione: un segnale di un tour e uno di prova avvisano chi assegna, non chi l'ha spento, una volta.
**Fatta quando**: un segnale nuovo arriva nella casella di chi assegna.

**Com'è andata** (30 settembre 2026, branch `m4/e10d-award-assigner-mail`, PR #205, del nucleo senza coda, da `main` a `c107c98`; `main`
a `c98b272`, con E10b (#208), unita prima dei documenti; `main` a `04718e6`, con E10e (#206), unita dopo la prima pubblicazione, quando
#205 era diventata CONFLICTING — solo `HANDOFF-M4.md`, l'intestazione e la riga «Che cosa manca», risolte tenendo il paragrafo di ogni
fase):

- **La domanda prima del codice.** La forma non era ovvia: con E14b, validare i PIREP di un RFE fa da cento a trecento segnali in pochi
  giorni. La nota è andata a Carmine come «Proposta», con la PR in bozza e le domande in un [commento][q205]; il codice che ne
  dipendeva ha aspettato la [risposta][a205] (Carmine, in chat al master, pubblicata su sua istruzione).
- **Fatto** (nota nuova `2026-09-30-la-mail-a-chi-assegna-gli-award`, **decisa**):
  - **il riepilogo al giorno**: `AwardQueueMailJob` (`award-queue-mail`, `src/IvaoHub.Core/Awards/`) gira all'ora di
    `division.json → awardDigestTime` (`HH:mm` nell'ora della divisione, 07:00 se manca). Solo se sono entrati segnali nuovi, manda a
    chi ha `Awards.Assign` (`IPermissionHolders`) una mail con una riga per motivo e award proposto e il numero, senza VID. Il tipo del
    nucleo è `award.toAssign`, nel profilo da solo; le parole stanno in `locales/{en,it}/mail.json` e `common.json`;
  - **il segno** `cms_award_signals.notified_at`, con l'indice `(status, notified_at)`: la migrazione `AddAwardSignalNotifiedAt` segna
    come dette le righe già in coda, e il job lo scrive nello stesso salvataggio delle righe della mail;
  - **l'ora della divisione**: `DivisionOptions.AwardDigestTime` e il suo controllo in `DivisionOptionsValidator`; il trigger si
    costruisce da lì (`AwardQueueMailJob.CronAt`, in `AddHubAwards`); `config/division.example.json` e `docs/FORKING.md` la spiegano;
  - **i test**, con i VID `761050–761055`:
    - `AwardQueueMailTests` (integrazione, nuovo). Un segnale dei tour (la proiezione dell'iscrizione, salvata dal contesto dei tour)
      e uno del modulo di prova arrivano a chi assegna, in una mail, una volta. Non arrivano a chi l'ha spenta dal profilo, né a chi
      non ha il permesso. Un segnale scartato prima del giro non si racconta. L'ora dell'host è quella della divisione, nel suo fuso;
    - `AwardQueueMailLinesTests` e `AwardDigestTimeTests` (unità).
- **Scostamenti**:
  1. **Il ritmo.** Il design (§8.3) e la nota di E0 dicevano «quando entra un segnale nuovo in coda», e `10` lasciava alla nota la
     scelta. **Carmine ha scelto il riepilogo al giorno, con l'ora configurabile**: la mail parte all'ora di `awardDigestTime` dopo il
     segnale, non subito. Il «fatta quando» di E10d vale a quell'ora; quello di E14b cambia allo stesso modo (nota, «Da portare nel
     piano»).
  2. **Un'impostazione nuova della divisione**, `awardDigestTime`, che `10` non prevedeva: la chiede la risposta di Carmine.
  3. ⚠️ **Trovato: l'MD non può avere `Awards.Assign`**. Un grant non dà mai un permesso globale, e un test di Carmine lo fissa. Il piano
     (§9.1), la nota di T4b e il design di M4 dicevano l'MD. Carmine ha deciso che `Awards.Assign` diventi concedibile, **in una fase
     del nucleo sua**: qui non si toccano le regole dei grant né quel test. Fino ad allora coda e mail sono di DIR, ADIR, WM, AWM e dei
     superadmin.
- **Scelte piccole, scritte nella nota**:
  - il segno si scrive anche quando nessuno riceve la mail, come in `DocumentReviewJob`;
  - la migrazione segna come dette le righe già in coda;
  - nei test chi assegna è un superadmin senza posizioni: un grant non può dare il permesso, e senza posizioni non entra fra i
    destinatari che `ContactsAndNotificationsTests` conta esatti;
  - il test mette in pausa il job del suo host, come `TourTests`.
- **Trovato, e scritto per chi viene dopo**:
  1. ⚠️ **I primi giri della classe nuova, da sola, avviavano l'host senza le fixture di IVAO**: su una fotografia vuota l'host chiede
     un token a IVAO all'avvio (l'avviso di E10b). Ora la classe passa `useIvaoFixtures: true` (un commit a sé), e da sola non fa
     nessuna chiamata (contate nel log: zero).
  2. `dotnet format --verify-no-changes` su `src/IvaoHub.Web/Program.cs` segnala l'ordine degli `using` (IMPORTS). C'è già su `main`;
     E10d aggiunge solo una riga lontana dagli `using`, e i file della fase sono in ordine.
  3. Gli snapshot dei contesti dei moduli prendono `notified_at` al loro prossimo `migrations add`: è lo scarto innocuo di T4b.
- **Verificato, in locale** (30 settembre 2026):
  - **prima della risposta**, sul codice di (B) con l'ora fissa: build senza avvisi, unità 889/889, integrazione intera senza filtro
    431/431 (8,4 minuti), `pnpm test` 594/594;
  - **le controprove**, sul codice della fase rimesso e ritoccato dopo ogni prova. Senza il filtro «in attesa» cade l'integrazione
    (il segnale scartato viene raccontato). Senza il segno cade (`notified_at` vuoto). Con l'ora scritta nel codice cade la prova del
    trigger (atteso `0 30 18 * * ?`, trovato `0 0 7 * * ?`);
  - **sul codice definitivo, con `main` unita**:
    - `dotnet build` senza avvisi; `dotnet format --verify-no-changes` sui file C# della fase;
    - unità **898/898**; **integrazione intera senza filtro 437/437** (7,7 minuti); `AwardQueueMailTests` da sola 2/2;
    - `pnpm lint`, `typecheck`, `format:check`, `i18n:check` verdi; `pnpm test` **594 in 80 file**; `pnpm gen:api` e `i18n:sync`
      senza differenze;
    - `pnpm e2e --workers=2`: **162/163** al primo giro, poi **163/163**. Era caduta `training-staff.spec.ts:379` con
      `net::ERR_ADDRESS_IN_USE` alla navigazione, con circa 900 socket in TIME_WAIT; da sola, con `--repeat-each=3`, ha dato 3/3;
    - `pnpm e2e:full` **50/50** al primo giro (9,9 minuti), su un banco suo (`http://127.0.0.1:5116`, `ivaohub_e2e_e10d` tolto
      prima), dietro il lock di Mailpit. ⚠️ Un primo tentativo con `--workers=2` è stato fermato a metà, per un errore mio:
      `playwright.full.config.ts` vuole **un worker solo** («two workers would publish over each other's page»). Sul banco nuovo i
      due worker si sono contesi il primo accesso del web master (`Duplicate entry '999001'`), e `full/awards.spec.ts:24` è caduta in
      206 ms. `--workers=2` vale per lo smoke, non per `e2e:full`;
    - le regole di `core-guard` rifatte in PowerShell dalla merge base: nessun file del maintainer, 15 del nucleo, la nota nuova —
      passa;
  - **dopo il merge di E10e** (#206): `dotnet build` senza avvisi, unità **906/906**, integrazione intera senza filtro **437/437** (6,8 minuti). Il
    merge porta solo `Core/Airspace/GreatCircle.cs`, i suoi test di unità e documenti, e nessun file web, schermata o migrazione: le
    suite web ed e2e qui sopra non sono state rifatte.
- **Non verificato**:
  - la CI (la dice la PR);
  - **la mail vera in Mailpit o nel browser**. Il banco non ha nessuno con `Awards.Assign` e una casella, e il job gira solo alla sua
    ora. La prova il test d'integrazione, che la rende con le parole vere e controlla che non resti un segnaposto;
  - due processi che fanno girare il job nello stesso secondo: è il limite di ogni job (nota, §3 punto 3);
  - un orario cambiato in `division.json` su un'installazione vera (vale dal riavvio, come il fuso);
  - la migrazione su un database con segnali già in coda: il test parte da una coda vuota, e la catena intera la prova
    `MigrationsApplyOnRealMariaDbTests`.

[q205]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/205#issuecomment-5915953993
[a205]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/205#issuecomment-5916282643

### E10e — Nucleo: la distanza fra due aeroporti

Design §1.8, §1.9 (`MinLegDistance`); nota `dopo-l-evento-e-gli-award`. Branch `m4/e10e-great-circle-core`. **PR del nucleo**, con la
sua nota (caso b: un pezzo usato in due posti si scrive una volta).

1. **Il calcolo sul cerchio massimo** (`GreatCircle` del modulo dei tour) passa nel nucleo, accanto alle coordinate di
   `IAirportDirectory`; i tour lo usano da lì, e i loro test non cambiano.

**Test**: gli unit dei tour, verdi; unit del nucleo sulla distanza fra due aeroporti noti.
**Fatta quando**: i tour e il nucleo hanno un calcolo solo. (Vera anche nel codice dalla #211, 30 set 2026: i tour usano `GreatCircle`
del nucleo e la copia non c'è più — nota `2026-09-30-i-tour-sulla-distanza-del-nucleo`, piano 1.28.)

**Com'è andata** (30 settembre 2026, branch `m4/e10e-great-circle-core`, PR #206, del nucleo senza coda, da `main` a `c107c98`):

- **Fatto** (nota nuova `2026-09-30-la-distanza-fra-due-aeroporti-nel-nucleo`, scelta tecnica, con una richiesta a Carmine):
  - **`src/IvaoHub.Core/Airspace/GreatCircle.cs`**, namespace `IvaoHub.Core.Airspace`: `GeoPoint`, `GreatCircle.DistanceNm` e
    `GreatCircle.DistanceNmRounded`, con gli stessi nomi, le stesse firme e lo stesso codice della copia dei tour (la costante, la
    formula, il `Min`, l'arrotondamento al decimo). Un modulo chiede a `IAirportDirectory.FindAsync` dove sono i due aeroporti e
    misura con `GreatCircle`. Nessuna registrazione, nessuna migrazione, nessun endpoint, niente nel browser;
  - **`tests/IvaoHub.UnitTests/GreatCircleTests.cs`** (unità, 8): le domande di `LegTests` al nucleo, con le stesse risposte; due
    antipodi sono mezza circonferenza (a 0°, 8°, 12°, 34°, su due meridiani) e un grado attraverso l'antimeridiano è un grado; **il
    test gemello**, che confronta il nucleo e la copia dei tour su 189 punti ognuno con ognuno (35.721 coppie: gli aeroporti dei
    test, i bordi, una griglia del globo) e vuole lo stesso double, bit per bit, e lo stesso decimo.
- ⚠️ **Scostamento dalla lettera del punto 1** («accanto alle coordinate di `IAirportDirectory`», nota §3): il calcolo sta in
  `Core/Airspace/`, accanto ai contorni dei FIR, non accanto a `AirportDirectory.cs` in `Core/Ivao/`. Non nomina IVAO (`CLAUDE.md`
  §3), e **in `IvaoHub.Core.Ivao` gli stessi nomi fanno cadere la build dei tour**: `TrackChecks.cs`, `PirepSubmission.cs` e
  `PirepTests.Checks.cs` (un test del maintainer) importano sia `IvaoHub.Core.Ivao` sia `IvaoHub.Modules.FlightOps.Legs` —
  provato, `CS0104` («'GeoPoint' è un riferimento ambiguo») su `TrackChecks.cs`.
- ⚠️ **Scostamento dal punto 1 e dalla «Fatta quando»** («i tour lo usano da lì», «un calcolo solo»): passare i tour al nucleo è una
  modifica di `src/IvaoHub.Modules.FlightOps/`, che il collaboratore non fa (`CLAUDE.md` §0 regola 2, `core-guard`). **La copia dei
  tour resta**, e la sostituisce una sessione di Carmine, come `memberName` dopo A12a di M3: [la richiesta sulla #206][r206], e il
  passaggio scritto riga per riga nella nota (§5; nessuna migrazione, i numeri sono gli stessi). Fino ad allora un calcolo solo per i
  numeri — il test gemello — e due copie nel codice.
- **Un test in più** di quelli che questa fase chiedeva: il test gemello. Le prove sui valori sono al decimo, e non vedono un raggio
  cambiato (nota §6, punto 3).
- **Trovato** (nota §6): agli antipodi `h` passa 1 di un'unità nell'ultima cifra (77.455 volte su due milioni di coppie a caso;
  misurato con uno script su .NET 10), ma `Math.Sqrt` riporta 1 più un'unità esattamente a 1, quindi l'arcoseno non dà mai NaN, **anche
  senza `Math.Min(1, h)`**. Il `Min` resta com'è nei tour: lo stesso codice, e una guardia che non costa niente.
- **Verificato, in locale** (30 settembre 2026, una suite alla volta): `dotnet build IvaoHub.sln` 0 avvisi; unità **893/893**
  (gli 8 nuovi); `GreatCircleTests` con `LegTests` da sole **18/18**; **integrazione intera, senza filtro, 430/430** al primo giro
  (7,6 minuti); **le prove al contrario**, sul file del nucleo e poi rimesso: con il raggio arrotondato a 3440,065 le sette prove sui
  valori passano e cade solo il test gemello; arrotondando a due decimali cadono il test gemello e quello del decimo; senza
  `Math.Min(1, h)` resta tutto verde (il perché qui sopra); con il namespace `IvaoHub.Core.Ivao` la build dei tour cade (`CS0104`);
  `dotnet format --verify-no-changes` sui due file C# pulito; in `web/`, dove niente cambia, `pnpm lint`, `typecheck`,
  `format:check` e `i18n:check` (783 chiavi) verdi, `pnpm test` **594/594** in 80 file, `pnpm gen:api` senza differenze; le regole
  di `core-guard` rifatte in PowerShell su tutto il branch contro `main` (`c107c98`): nessun file del maintainer né dei tour, un file
  del nucleo (`GreatCircle.cs`) con la nota nuova, PASS. La CI sulla cima di allora (`2a8ef3e`): `build-test` e `core-guard` verdi.
- **Dopo la revisione** ([osservazioni del revisore sulla #206][v206], «approvable», niente da correggere: il calcolo com'è nei tour,
  `Core/Airspace/` giusto come `FirBoundary.cs`) e **le risposte di Carmine** (30 settembre 2026, pubblicate dal master sulla #206 su
  sua istruzione: [risposte][a206]): i tour passano al `GreatCircle` del nucleo **in una sua sessione, dopo l'unione** di questa PR;
  **sì alla riga in `CLAUDE.md` §2**, che aggiunge il master. Registrate nella nota (intestazione, §5, «Da portare nel piano») e
  nell'handoff; nessun cambio al codice.
- **Il merge di `main`** (chiesto dal revisore nello stesso commento): unita la #208 (E10b), la PR era in conflitto con `main` su
  `HANDOFF-M4.md`, dove tutte e due le fasi avevano scritto in cima. `origin/main` (`c98b272`) è entrato con un merge (`291cc17`), mai
  un rebase. **Un conflitto solo**, `HANDOFF-M4.md`: l'intestazione di E10e, con E10b unita e quello che E11b ed E13a ci trovano; le
  due voci barrate nella riga di che cosa mancava; «Che cosa ha lasciato E10e» in cima e quello di E10b sotto. Nessuna riga dei due
  lati è andata persa (controllato riga per riga); `10` si è unito da solo, con tutte e due le «Com'è andata». Poi di nuovo, una suite
  alla volta, tutte al primo giro: `dotnet build` 0 avvisi; unità **893/893**; **integrazione intera, senza filtro, 435/435** (i 430 e
  i 5 di E10b, 8 minuti); `pnpm lint`, `typecheck`, `format:check`, `i18n:check` (783 chiavi) verdi, `pnpm test` **594/594** in 80
  file; `core-guard` contro `main` (`c98b272`): la PR mostra solo i cinque file della fase, nessun file del maintainer né dei tour, un
  file del nucleo con la nota nuova, PASS.
- **Non verificato**: la CI (la dice la PR); `pnpm e2e` e `pnpm e2e:full` (nessuna schermata cambia); i numeri su Linux — il test
  gemello confronta due calcoli nello stesso processo e vale anche lì, ma che `h` passi 1 a 8°, 12° e 34° è misurato su Windows (il
  test afferma solo la mezza circonferenza); il passaggio dei tour al nucleo, che è di Carmine.

[r206]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/206#issuecomment-5916051005
[v206]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/206#issuecomment-5916572883
[a206]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/206#issuecomment-5916695685

### E10f — Nucleo: `Awards.Assign` con un grant

**Da dove viene**: non c'era in E0. L'ha trovata E10d (nota `2026-09-30-la-mail-a-chi-assegna-gli-award` §5): il piano (§9.1, riga
Award), la nota di T4b e il design di M4 dicono che assegna l'MD, e il codice non lo permetteva, perché un grant non dava mai un permesso
globale. **Carmine ha deciso** ([risposta 2 sulla #205][a205], alla [domanda][q205] §2) che `Awards.Assign` diventi un permesso globale
che un grant può dare, detto sul permesso e solo per lui (mai `Permissions.Manage` né lo stato di superadmin), che la divisione lo dia
all'MD con un `positionGrant`, e che lo faccia una PR del nucleo sua. Branch `m4/e10f-grantable-award-assign`. **PR del nucleo**, con la
sua nota; nessuna migrazione; nessuna fase del modulo la aspetta.

1. **Il campo sul permesso** (`PermissionDescriptor`) e una domanda del catalogo, che il calcolatore, la schermata dei permessi e il
   seme dei `positionGrants` fanno al posto di «è globale?».
2. **`Permissions.Manage` e lo stato di superadmin** restano fuori da ogni grant.
3. **Il `positionGrant` dell'MD** in `config/division.json`, e nell'esempio.
4. **Il caso `Awards.Assign`** di `EffectivePermissionsTests.AGrantCanNeverConferAGlobalPermission` cambia, apposta e con la nota.

**Test**: unità: un grant a una posizione dà `Awards.Assign` e nessun altro permesso globale; `Permissions.Manage` resta rifiutato dal
calcolatore, dalla schermata e dal seme. Integrazione: l'MD del seme di `division.json` apre la coda ed è fra i destinatari del riepilogo.
**Fatta quando**: l'MD ha `Awards.Assign` dal seme della divisione, e nient'altro di globale.

**Com'è andata** (1 ottobre 2026, branch `m4/e10f-grantable-award-assign`, PR #213, del nucleo senza coda, da `main` a `db9268f`):

- **Fatto** (nota nuova `2026-10-01-chi-assegna-gli-award-con-un-grant`, **decisa**; la forma nel codice è una scelta tecnica, §3):
  - **il campo `GrantableAlthoughGlobal`** su `PermissionDescriptor`, falso se non è detto, vero solo per `Awards.Assign`;
    **`PermissionCatalog.IsClosedToGrants`**, la domanda sola che fanno il calcolatore, `GrantWriteDtoValidator` e `PositionGrantSeeder`;
    il catalogo **non nasce** con un permesso concedibile che non sia `Awards.Assign` (`Permissions.Manage` per primo; nella prima
    stesura solo lui: il paletto largo è venuto dalla revisione, sotto);
  - **solo intero**: il calcolatore tiene un globale concedibile solo da un grant (o un rifiuto) senza dipartimento, senza scope e non
    al team di un FIR; la schermata rifiuta il dipartimento con **`errors.grant.globalDepartment`** (chiave nuova, `locales/{en,it}`);
    il seme salta uno `scope`; il team di un FIR lo rifiuta già la regola di A11a;
  - **il permesso, non il dipartimento**: `EffectivePermission.FromOutside`, vero per un grant di un permesso globale e per la `View`
    che porta; `HubClaims.BuildIdentity` lo lascia fuori dai claim `dept`, e la deduplicazione preferisce la voce che porta dentro. È la
    stessa forma di E2b (#212, decisa sulla #209), che corre insieme;
  - **il bootstrap** porta `grantableAlthoughGlobal` accanto a `isGlobal`, e la schermata dei permessi offre `Awards.Assign`
    (`schema.ts`); `pnpm gen:api` ha riscritto `schema.d.ts`;
  - **la divisione**: `{ "department": "MD", "levels": ["Coordinator", "Assistant"], "permission": "Awards.Assign" }` in testa ai
    `positionGrants` di `config/division.json` e di `config/division.example.json` (con il suo commento), e una frase in
    `docs/FORKING.md`;
  - **i test**, con i VID `761080–761083`: `GrantableGlobalPermissionTests` (unità, 17), `AwardsAssignByGrantTests` (integrazione, 4),
    `web/src/features/admin/grants/grantable.test.ts` (2).
- **Il test di Carmine**: in `EffectivePermissionsTests.AGrantCanNeverConferAGlobalPermission` è tolta la sola riga
  `[InlineData(CorePermissions.AwardsAssign)]`, con un commento che rimanda alla nota; le altre quattro restano e passano, e nessun altro
  suo test cambia. L'ha deciso lui, nella risposta 2.
- **Scostamenti e scelte, scritti nella nota**:
  1. **`ModuleGrants` non cambia comportamento** (il compito lo elencava fra i posti della regola): rifiuta ogni globale, anche
     `Awards.Assign`, perché un suo grant è su un dipartimento e può essere su una riga, e un globale chiesto «in generale» varrebbe
     ovunque. Solo un commento.
  2. **Il dipartimento di un grant globale si rifiuta** invece di leggerlo come «ovunque»: una chiave d'errore nuova, che la risposta di
     Carmine non nominava.
  3. **Un rifiuto intero di `Awards.Assign` ora vale**, anche per chi lo ha per ruolo: prima un rifiuto di un globale non valeva niente
     (per gli altri globali resta così).
  4. **Il testo di `errors.grant.globalPermission`** («un permesso non legato a un dipartimento non si assegna a mano») non era più vero, e
     cambia.
  5. **I livelli dell'MD** (coordinatore e assistente) erano una mia lettura del piano 0.77: il piano dice «l'MD», non i livelli.
     Confermati da Carmine dopo la revisione, con il rifiuto intero (3) e la `Awards.View` su ogni dipartimento (sotto).
  6. **Il commento di `AwardQueueMailTests`** (E10d) diceva «un grant non dà mai un permesso globale»: una frase corretta, nessuna
     riga di codice del test.
- **Trovato leggendo**: le risorse di `Awards.Assign` (la coda, il registro) non sono `IOwnedByDepartment`, quindi l'handler e la SPA
  chiedono solo il nome (`HasAny`, `holdsPermissionAnywhere`): un grant con un dipartimento, uno scope o un FIR si leggerebbe «ovunque».
  È il perché del «solo intero». E `Awards.Assign` porta `Awards.View` dove è tenuto lui (la regola «un permesso dell'area porta la sua
  `View`»): chi assegna legge ogni award, come voleva la T4b.
- ⚠️ **Trovato da un'altra sessione, dopo la prima stesura**: la sessione di E2b mi ha scritto il 1° ottobre che un grant senza
  dipartimento mette chi lo tiene dentro **ogni** dipartimento (`HubClaims.BuildIdentity`, i claim `dept`), quindi il coordinatore e
  l'assistente dell'MD avrebbero visto le righe `Department` di tutti. Misurato con il test d'integrazione: `user.departments` era tutti
  e nove. Corretto con `FromOutside` (sopra), nella forma che E2b stava scrivendo, concordata con quella sessione per non scrivere due
  volte lo stesso campo. La prima stesura della nota non ne parlava; ora ha il suo punto (§2 punto 7, §3 punto 3-bis).
- **Verificato, in locale** (1 ottobre 2026, una suite alla volta, sul codice definitivo, `main` a `db9268f`):
  - `dotnet build IvaoHub.sln` 0 avvisi; `dotnet format --verify-no-changes` sui 12 file C# toccati: pulito;
  - unità **913/913**; **integrazione intera, senza filtro, 441/441** (7,4 minuti); `AwardsAssignByGrantTests` da sola 4/4, nessuna
    chiamata a `ivao.aero` nel suo log. La prima stesura, senza `FromOutside`, aveva dato 912/912 e 441/441;
  - in `web/`: `pnpm lint`, `typecheck`, `format:check`, `i18n:check` (783 chiavi) verdi; `pnpm test` **596/596** in 81 file;
    `pnpm gen:api` senza differenze dopo la sua riscrittura;
  - `pnpm e2e` (smoke, dietro il suo lock): **163/163** al primo giro, compresa `permissions-fir-team.spec.ts` del maintainer, che
    costruisce l'elenco dei permessi senza il campo nuovo;
  - `pnpm e2e:full` sul banco suo (`http://127.0.0.1:5119`, `ivaohub_e2e_e10f` tolto prima, dietro il lock di Mailpit, un worker):
    **50/50** al primo giro (10,6 minuti). Il banco ha applicato 24 grant da `division.json`, `Awards.Assign` all'MD compreso, senza
    avvisi (letto nel log e nella tabella);
  - le prove al contrario, sul codice della fase, rimesso e toccato dopo ognuna: senza il filtro «solo intero» cade
    `OnlyAWholeGrantConfersIt`; senza il campo su `Awards.Assign` cadono sei test nuovi di unità, e i 27 di `EffectivePermissionsTests`
    restano verdi; senza il rifiuto nel catalogo cade il suo test; **senza `FromOutside` in `BuildIdentity`** cadono il test di unità
    sui claim e due d'integrazione (`user.departments` = `["MD","HQ","SOD","FOD","AOD",…]` invece di `["MD"]`);
  - le regole di `core-guard` rifatte in PowerShell dalla merge base: nessun file del maintainer, 14 del nucleo, la nota nuova — passa.
- **Non verificato**:
  - la CI (la dice la PR);
  - **la mail vera all'MD**: servirebbe uno staff dell'MD con un indirizzo, che i test dei contatti non vogliono. Che il riepilogo vada a
    chi ha il permesso lo prova `AwardQueueMailTests` (E10d); che l'MD lo abbia, `AwardsAssignByGrantTests`;
  - la schermata dei permessi che offre `Awards.Assign` in un browser vero: nessuna spec la guida (le scelte le prova
    `grantable.test.ts`, il server `AwardsAssignByGrantTests`);
  - il seme su un'installazione già avviata (la prova): si applica al primo avvio dopo il rilascio, per la regola di T5, e qui non è
    stato fatto girare;
  - chi non è dentro nessun dipartimento e riceve `Awards.Assign` per nome: la lista degli award gli risponde 403 fino alla metà
    «liste» di E2b. Oggi nessuno ce l'ha così.
- **La CI** sulla prima cima (`360045d`): `build-test` (23,6 minuti) e `core-guard` verdi.
- **Dopo la revisione** ([i rilievi del revisore][v213], «approvable») e **le risposte di Carmine** ([sulla #213][a213], in chat al
  master il 1° ottobre, pubblicate su sua istruzione):
  - **sì alle tre scelte** della sessione: coordinatore e assistente dell'MD; il rifiuto intero che toglie `Awards.Assign` anche a chi lo
    ha per ruolo; la `Awards.View` su ogni dipartimento che il grant porta. Registrate nella nota (intestazione, §3 punti 3, 3-bis e 8,
    «Da portare nel piano») e nell'handoff;
  - **il paletto facoltativo, preso**: il catalogo non nasce se `GrantableAlthoughGlobal` lo dice un permesso che non sia
    `Awards.Assign` (prima solo `Permissions.Manage`). `GrantableGlobalPermissionTests` passa da 11 a 17 test (una teoria su sei
    permessi e il globale di un modulo); senza il paletto ne cadono 7;
  - **l'ordine di merge con E2b** (#212), scritto nella nota (§3 punto 3-bis), nell'handoff e nel corpo della PR: in E2b il segno scrive
    anche un `!` nel cookie, quindi chi arriva seconda tiene una dichiarazione sola, somma le condizioni e rifà sul codice unito
    `AwardsAssignByGrantTests`, `GrantableGlobalPermissionTests` e le due classi di E2b;
  - **il merge di `main`** (`c441839`, con E2, #209), mai un rebase: **un conflitto solo**, `HANDOFF-M4.md` — l'intestazione di E10f con
    E2 unita e il prossimo passo di E2 (E2b, poi E3a); in «Lo stato» il paragrafo di E10f in cima e quello di E2 sotto, nessuna riga
    persa. I due file della divisione si sono uniti da soli (il grant all'MD per primo, quelli dell'ED dopo il training).
  - **Rifatto dopo il merge**, una suite alla volta: `dotnet build` 0 avvisi; `dotnet format --verify-no-changes` sui file C# toccati di
    nuovo: pulito; unità **953/953**; **integrazione intera, senza filtro, 445/445** (5,4 minuti); in `web/` `pnpm lint`, `typecheck`,
    `format:check`, `i18n:check` (784 chiavi) verdi, `pnpm test` **601/601** in 82 file, `pnpm gen:api` senza differenze; `pnpm e2e`
    **163/163**; `pnpm e2e:full` sul banco 5119 (`ivaohub_e2e_e10f` tolto prima, dietro il lock di Mailpit) **51/51** al primo giro
    (10,9 minuti, la spec in più è quella dello scheletro degli eventi); le regole di `core-guard` dalla merge base nuova (`c441839`): 23
    file, nessuno del maintainer, 14 del nucleo, la nota nuova — passa.
  - **E15a (#207) è entrata in `main` mentre giravano i controlli** della nuova cima, e la PR è tornata CONFLICTING: `main` (`99ab043`)
    unito sopra, mai un rebase, un conflitto solo in `HANDOFF-M4.md` (l'intestazione con E15a unita e il passo di E15b; in «Lo stato»
    E10f, poi E15a, poi E2). E15a porta codice del nucleo in `Core/Ivao/`, test di unità, una fixture e lo strumento: nessun file web,
    nessuna schermata, nessuna migrazione. Rifatti: `dotnet build` 0 avvisi; unità **978/978**; **integrazione intera, senza filtro,
    445/445** (5,3 minuti); `pnpm gen:api` senza differenze; `core-guard` dalla merge base `99ab043`: passa. Le suite web ed e2e qui
    sopra non sono state rifatte: il merge non tocca nessun file sotto `web/`. CI sulla cima `39d1f89`: `build-test` (24,5 minuti) e
    `core-guard` verdi.
  - **E10a (#210) unita, e il revisore ha chiesto di fondere `main`** ([commento sulla #213][m213]): `main` (`b88460a`) unito, mai un
    rebase, un conflitto solo in `HANDOFF-M4.md` (l'intestazione con E10a unita, la sua riga su E13a e la coda del master: **E10f entra
    prima di E2b, e la riconciliazione di `FromOutside` è di E2b**; in «Lo stato» E10f, poi E10a, poi gli altri). E10a porta codice del
    nucleo in `Core/Ivao/`, test di unità, fixture e lo strumento: nessun file web, nessuna schermata, nessuna migrazione. Rifatti:
    `dotnet build` 0 avvisi; unità **995/995**; **integrazione intera, senza filtro, 445/445** (5,5 minuti); `pnpm gen:api` senza
    differenze; `core-guard` dalla merge base `b88460a`: passa. Le suite web ed e2e non rifatte, per la stessa ragione.

[m213]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/213#issuecomment-5929486224

[v213]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/213#issuecomment-5926660925
[a213]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/213#issuecomment-5926811970

### E10g — Nucleo: la versione di un contratto

**Da dove viene**: non c'era in E0. L'ha portata la revisione di E5 (#228): il revisore ha girato a Carmine la domanda della versione
dell'esportazione per il Gate Manager, e **Carmine ha deciso** ([risposta 9 sulla #228][a228g]) che l'esportazione porta la versione
del suo contratto in un'intestazione sua — senza, o con una versione che l'hub non parla, 400 con le versioni accettate — e che il
contratto si scrive in un documento pubblico, come quello dell'agente dei tour. Il controllo c'era solo nei tour
(`AgentContract.RequireVersionAsync`), che gli eventi non referenziano: sulla classificazione chiesta dalla sessione che coordina prima
del codice, dalberone ha scelto il 6 ottobre di portarlo nel nucleo (caso b, come E10e). Branch `m4/e10g-contract-version`. **PR del
nucleo**, con la sua nota; nessuna migrazione; **E5 la aspetta**: la #228 è in coda dopo di lei.

1. **`ContractVersion`** in `Core/Auth/`, accanto ai token personali: l'intestazione del contratto, la versione corrente, le accettate,
   la chiave del titolo (del modulo) e il `code`; il filtro `RequireAsync`, che risponde esattamente come quello dei tour.
2. **Il costruttore** rifiuta quello che non può essere un contratto.
3. **La copia dei tour resta** in questa PR: la sostituisce una sessione di Carmine, come `GreatCircle` dopo E10e. La richiesta, e la
   domanda di una riga in `CLAUDE.md` §2, vanno a Carmine sulla PR.

**Test**: unità: i rifiuti e le accettate, la versione parlata, il titolo nella lingua di chi chiede, il costruttore; il test gemello con
il filtro dei tour.
**Fatta quando**: il nucleo ha la versione di un contratto e risponde come il filtro dei tour (il test gemello), così E5 la usa con i
suoi valori invece di scriverne una sua. La copia dei tour se ne va con la sessione di Carmine.

**Com'è andata** (6–7 ottobre 2026, branch `m4/e10g-contract-version`, PR #230, del nucleo senza coda, da `main` a `e9702b2`):

- **Fatto** (nota nuova `2026-10-06-la-versione-di-un-contratto-nel-nucleo`, scelta tecnica, con una richiesta e una domanda a Carmine):
  - **`src/IvaoHub.Core/Auth/ContractVersion.cs`**, namespace `IvaoHub.Core.Auth`: la forma che E5 aspettava, senza cambi —
    `ContractVersion(header, current, accepted, titleKey, code)`, le cinque proprietà e `RequireAsync(context, next)`, il codice dei tour
    riga per riga con i valori del contratto. Nessuna registrazione, nessuna migrazione, nessun endpoint, nessuna chiave, niente nel
    browser;
  - **`tests/IvaoHub.UnitTests/ContractVersionTests.cs`** (unità, 21): una richiesta passa nel filtro come la fa passare un endpoint, e il
    rifiuto si scrive come lo scrive il server; i rifiuti, le accettate, la versione parlata di un contratto alla versione 2, il titolo
    in inglese, in italiano, nella lingua della divisione e senza catalogo, i rifiuti del costruttore, le versioni copiate; **il test
    gemello**, che confronta il nucleo con i valori dei tour e `AgentContract.RequireVersionAsync` su 120 coppie (venti richieste, con e
    senza catalogo, in due lingue e senza utente) e vuole la stessa risposta byte per byte, e che dice quali richieste i tour accettano:
    `1`, `1` fra due spazi, `\t1`, `01`.
- **Scelte, scritte nella nota** (§3):
  1. **due rifiuti in più** del costruttore, oltre a quelli della fase: un'intestazione che non è un token di HTTP, e una versione
     ripetuta. I valori dei tour e quelli di E5 li passano;
  2. **niente `Announce`** per l'endpoint aperto: il passaggio dei tour non ne ha bisogno (`AgentContract` tiene `Header` e `Current`, e
     la riga 32 di `AgentEndpoints` resta com'è), ed E5 non ha un endpoint aperto;
  3. **il nome della nota è del 6 ottobre**: la fase è nata quel giorno sulla #228, e la nota di E5 la cita già così; è scritta nella
     notte sul 7.
- **Trovato** (nota §6): **il test gemello non vede la versione della risposta** — con la corrente al posto della parlata i suoi 120
  confronti passano tutti, perché i tour accettano solo la 1 —, e la vede solo il test del contratto alla versione 2.
- **La copia dei tour resta** (`CLAUDE.md` §0 regola 2, `core-guard`): due copie dello stesso filtro, tenute uguali dal test gemello,
  finché una sessione di Carmine non passa i tour al nucleo; la nota (§5) scrive quel passaggio riga per riga.
- **Letta la fase del nucleo che corre, E4b** (#226): niente in `Core/Auth/`; scrive l'intestazione di `HANDOFF-M4.md` e altri punti di
  `10` — un conflitto di documenti per chi arriva seconda, nessuna sovrapposizione di codice. Nessun messaggio alla sua sessione.
- **Verificato, in locale** (6–7 ottobre 2026, una suite alla volta, `main` a `e9702b2`):
  - `dotnet build IvaoHub.sln --no-incremental` 0 avvisi; `dotnet format --verify-no-changes` sui due file C#: pulito;
  - unità **1133/1133** (i 21 nuovi, i test di architettura compresi); `ContractVersionTests` da sola **21/21**;
  - **integrazione intera, senza filtro, 481/481** al primo giro (7,2 minuti), la prova della divisione «XX» compresa;
  - **le prove al contrario**, sul file del nucleo e poi rimesso (nota §6): con `NumberStyles.Integer` cadono il gemello e il rifiuto di
    `+1`; senza `Trim` il gemello e l'accettata con gli spazi; con la corrente al posto della parlata nell'intestazione solo il test del
    contratto alla versione 2;
  - in `web/`, dove niente cambia (i `node_modules` installati dal lockfile, senza cambiarlo): `pnpm lint` e `pnpm typecheck` verdi,
    `pnpm test` **625/625** in 85 file, `pnpm gen:api` senza differenze;
  - le regole di `core-guard` rifatte in PowerShell dalla merge base (`e9702b2`): cinque file, nessuno del maintainer né dei tour, un file
    del nucleo (`ContractVersion.cs`) con la nota nuova — passa.
- **Non verificato**:
  - la CI (la dice la PR);
  - `pnpm e2e` e `pnpm e2e:full`: nessuna schermata cambia;
  - `ContractVersion` su un endpoint vero, nella catena dell'hub (la policy del token prima, il servizio dei problemi di
    `AddProblemDetails`): i test di questa fase chiamano il filtro con un `DefaultHttpContext` e scrivono il problema senza quel servizio.
    Lo provano i test d'integrazione dell'esportazione di E5, e per i tour `PirepTests.Agent` dopo il passaggio;
  - il passaggio dei tour al nucleo, che è di Carmine.
- **La CI** sulla prima cima (`cc1b46c`): `build-test` (21,3 minuti) e `core-guard` verdi.
- **Dopo la revisione** ([i rilievi del revisore sulla #230][v230g], «approvable on the code») e **le risposte di Carmine** ([sulla
  #230][a230g], in chat al master il 7 ottobre, pubblicate su sua istruzione):
  - **sì al nucleo** invece di una copia nel modulo; **il passaggio dei tour** a `ContractVersion` lo fa una sua sessione dopo l'unione
    di questa PR, come lo scrive la nota (§5); **sì alla riga in `CLAUDE.md` §2**, che aggiunge il master con il piano. Registrate nella
    nota, ora **decisa** (intestazione, §5, «Da portare nel piano»), e nell'handoff;
  - **il rilievo basso**: la riga di E10g nella tabella delle fasi diceva «il filtro dei tour passa nel nucleo»; ora dice che la copia
    dei tour resta e che il passaggio è di una sessione di Carmine;
  - **il merge di `main`** (`7b84a75`, con E4b, #226), mai un rebase: **un conflitto solo**, `HANDOFF-M4.md` — l'intestazione di E10g
    con E4b unita e le risposte di Carmine; in «Lo stato» il paragrafo di E10g in cima e quello di E4b sotto, nessuna riga persa
    (controllato con il diff contro `main`: cambiano solo l'intestazione e la riga di che cosa mancava). `10` si è unito da solo, con la
    riga e la sezione di E4b accanto a E4. E4b non tocca `Core/Auth/`;
  - **rifatto dopo il merge**, come chiesto: `dotnet build` 0 avvisi; unità **1141/1141** (le 1133 e gli 8 di E4b). Non rifatte, perché
    il merge porta solo il codice di E4b, già verde sulla CI di `main`, e nessun file di E10g cambia: l'integrazione intera e le suite
    di `web/`; le corre la CI della nuova cima.

[a228g]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/228#issuecomment-6022686808
[v230g]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/230#issuecomment-6039666570
[a230g]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/230#issuecomment-6039777720

### E10h — Nucleo: il ritiro di chi ha mandato la riga

**Da dove viene**: non c'era in E0. L'ha trovata la sessione di E6a, il 7 ottobre 2026, leggendo il guardiano dell'interceptor prima di
scrivere il ritiro: il design (§1.6, §3.6, deciso da Carmine sulla #180) dice «ritirare cancella la riga», e il guardiano lascia al membro
creare e cambiare una riga `ISubmittedByMembers` e `IHasStakeholder`, non cancellarla (nota `2026-09-23-il-pirep` §5: «Cancellarla resta
del dipartimento»). Fra le tre strade offerte — una fase del nucleo prima (raccomandata), la domanda a Carmine prima, E6a senza il
ritiro — dalberone ha scelto la fase del nucleo. Branch `m4/e10h-stakeholder-withdraws`, da `main` a `e9702b2`. **PR del nucleo**, con
la sua nota (caso b); nessuna migrazione del nucleo; **E6a la aspetta**: unisce questo branch e va in coda dopo la sua PR.

1. **Il segno sull'entità**: `[WithdrawnByStakeholder]`, accanto a `ISubmittedByMembers`.
2. **Il guardiano**: un'entrata `Deleted` di un'entità `ISubmittedByMembers` e `IHasStakeholder` con il segno passa se l'interessato, com'era
   salvato, è chi scrive; tutto il resto come prima (il PIREP, un altro membro, lo staff con `Edit`, il superadmin).
3. **All'avvio**, accanto ai sei rifiuti della nota `2026-09-30-il-controllo-all-avvio-rinforzato`: il segno su un'entità che non è
   `ISubmittedByMembers` e `IHasStakeholder` ferma l'hub; la nota lo decide.

**Test**: integrazione, sul modulo di prova e la MariaDB vera: il membro se la riprende e l'audit dice che è stato lui; un altro membro no;
lo staff con `Edit` sì; una riga senza il segno resta del dipartimento. Unità: il rifiuto all'avvio.
**Fatta quando**: il membro cancella la riga che ha mandato dove l'entità lo dice, e solo lì; E6a ritira una prenotazione.

**Com'è andata** (7 ottobre 2026, branch `m4/e10h-stakeholder-withdraws`, PR #232, del nucleo senza coda, da `main` a `e9702b2`):

- **Fatto** (nota nuova `2026-10-07-il-ritiro-di-chi-ha-mandato-la-riga`, **Proposta**, con la domanda a Carmine sulla #232):
  - **`WithdrawnByStakeholderAttribute`** in `src/IvaoHub.Core/Division/DomainContracts.cs` (namespace `IvaoHub.Core.Division`,
    `AttributeUsage(AttributeTargets.Class)`), la forma che E6a aspettava, senza cambi di nome; il commento di `ISubmittedByMembers` dice
    ora che cancellare resta del dipartimento «unless the entity says its member takes it back»;
  - **il guardiano** (`HubSaveChangesInterceptor.EnsureWriteIsAllowed`), subito dopo l'eccezione di T11: un'entrata `Deleted` di
    un'entità `ISubmittedByMembers` e `IHasStakeholder` con il segno passa se l'interessato letto dai valori originali
    (`entry.OriginalValues.ToObject()`) è chi scrive; nessun controllo dei dipartimenti, perché un'eliminazione non sposta niente;
  - **il settimo rifiuto all'avvio**: `HubSaveChangesInterceptor.VerifyWithdrawals(entities)`, chiamato da `HubPipeline.InitializeAsync`
    subito dopo `VerifyAlternatives`, sul modello di ogni contesto;
  - **il modulo di prova**: `SampleSubmission` (`smp_submissions`, con il segno, come una prenotazione) e `SampleReport` (`smp_reports`,
    senza, come un PIREP), migrazione `AddSampleSubmissions` del solo contesto di prova;
  - **i test**, con i VID 761037, 761047 e 761048 (lasciati da E6a): `WithdrawnByStakeholderTests` (integrazione, 5) e
    `VerifyWithdrawalsTests` (unità, 5).
- **Scelte, scritte nella nota** (§3):
  1. **una terza condizione del rifiuto**, `IOwnedByDepartment`, oltre alle due della fase: il guardiano non guarda affatto una riga senza
     dipartimento, e il segno lì sarebbe ignorato in silenzio;
  2. **il rifiuto fuori da `VerifyAlternatives`**, accanto al guardiano che legge il segno: il segno non è un permesso, e il catalogo non
     serve;
  3. **l'interessato com'era caricato** (al primo giro scritto «salvato»: vale solo per una riga letta, sotto), non come lo dice l'istanza
     in mano: un test prova che chi scrive il suo VID nella riga di un altro prima di toglierla non passa.
- **Trovato**: `dotnet format` sul file toccato `src/IvaoHub.Web/HubPipeline.cs` chiedeva uno spazio alla riga 234 (`=await`, venuto con
  la #218): sistemato in un commit a parte (`style`), senza effetti.
- **Il lato del modulo**: la sessione di E6a ha unito il branch a `55d7658` (il suo merge `96ddd21`, nessun conflitto) e ha messo il
  segno su `EventBooking`; il ritiro del pilota, che prima cadeva con 403 (`Expected: NoContent`, `Actual: Forbidden`), risponde 204 con
  l'audit a nome del pilota, e lì `WithdrawnByStakeholderTests` ed `EventsBookingsTests` danno 16 su 16.
- **Verificato, in locale** (7 ottobre 2026, una suite alla volta, `main` a `e9702b2`):
  - `dotnet build IvaoHub.sln` 0 avvisi; `dotnet format --verify-no-changes` sui dieci file C# toccati, migrazioni comprese: pulito;
  - unità **1117/1117** (i 5 nuovi e i test di architettura compresi);
  - integrazione intera, senza filtro, **486/486** al primo giro (9,7 minuti, con la suite di E6a che girava accanto, ognuna con il suo
    container); `WithdrawnByStakeholderTests` da sola 5/5;
  - **la prova al contrario**: con l'interceptor e `HubPipeline.cs` di `main` rimessi (il segno resta, il modulo di prova lo usa), della
    classe nuova cade solo il ritiro del membro (4/5, `VID 761037 does not hold Sample.Edit on any of ED`); rimessi i file della fase,
    toccati e ricompilati, 5/5;
  - in `web/`, dove niente cambia (i `node_modules` installati dal lockfile, senza cambiarlo): `pnpm lint` e `pnpm typecheck` verdi,
    `pnpm test` **625/625** in 85 file, `pnpm gen:api` senza differenze;
  - le regole di `core-guard` rifatte in PowerShell dalla merge base (`e9702b2`): tredici file, nessuno del maintainer, cinque del nucleo
    con la nota nuova — passa.
- **Non verificato**:
  - la CI (la dice la PR);
  - `pnpm e2e` e `pnpm e2e:full`: nessuna schermata cambia, quindi né la porta 5129 né `ivaohub_e2e_e10h` sono stati usati;
  - un endpoint vero del ritiro su questo branch: la fase prova il guardiano senza un endpoint davanti, e l'endpoint del pilota è di E6a,
    che lo prova sul suo branch (sopra).

**Dopo la revisione** (7 ottobre 2026, [i rilievi][rv232] e [le risposte di Carmine][a232] sulla #232, quelle date in chat alla sessione
master e pubblicate su sua istruzione):

- **`main` unito sul branch** a `7b84a75` (la #226 di E4b): conflitto solo in `HANDOFF-M4.md`, risolto con l'intestazione di E10h
  aggiornata e il blocco di E4b subito sotto quello di E10h; `10` si è unito da solo.
- **La nota è decisa**: sì alla forma (risposta 1); **il limite della riga mai caricata si accetta e si scrive, come per T11** (risposta 2,
  sul rilievo 2): il guardiano legge l'interessato dai valori originali del tracker, quindi **l'endpoint deve caricare la riga; uno stub
  passa**. La frase sta nel riassunto dell'attributo, nel commento del guardiano, nella nota (§3.2) e nella trappola di `HANDOFF-M4.md`; il
  test `AStubNeverLoadedIsBelievedAsItsCallerWroteIt` fissa il caso com'è. «Com'era salvata» diventa «com'era caricata» dove lo diceva
  (il test `TheRowAsItWasLoadedSaysWhoseItIs`, la variabile `loaded` del guardiano).
- **Il rilievo 3**: il riassunto dell'attributo dice anche l'alternativa segnata `AlsoOnDeletion`; quello di `VerifyWithdrawals` dice
  «insieme», con le parole della risposta 1.
- **Il rilievo 6** (il segno su un'entità con un permesso `DeniedToStakeholder`) era ancora aperto: nella nota (§6.3) con la
  raccomandazione — una frase, non un ottavo rifiuto — e nessun codice finché Carmine non rispondeva (sotto, la risposta).
- **Verificato** dopo il merge e le correzioni: `dotnet build IvaoHub.sln` 0 avvisi; `dotnet format --verify-no-changes` sui tre file C#
  toccati: pulito; unità **1125/1125** (le 8 di E4b comprese); integrazione intera, senza filtro, **492/492** al primo giro (5,8 minuti);
  `WithdrawnByStakeholderTests` da sola 6/6; le regole di `core-guard` dalla nuova merge base (`7b84a75`): tredici file, cinque del nucleo
  con la nota nuova — passa. Il web non cambia con questa fase e non si è rifatto: lo rifà la CI.

**Dopo la risposta sul rilievo 6** (7–8 ottobre 2026, [la risposta 3 corretta][a3232] di Carmine sulla #232, data in chat alla sessione
master e pubblicata su sua istruzione):

- **Una frase nel riassunto del segno, non un ottavo rifiuto all'avvio**, come raccomandato: *mai su una riga su cui lo staff decide
  qualcosa del membro — cancellarla cancellerebbe la decisione; il guardiano non sa né lo stato né l'ora, e fino a quando un membro si
  ritira lo dice l'endpoint del modulo*; e **la nota di ogni fase che mette il segno dice perché la sua riga non porta decisioni**. Sta nel
  riassunto di `[WithdrawnByStakeholder]` (`DomainContracts.cs`), nella nota (§3.1, §6.3, «Da portare nel piano») e nell'handoff; nessun
  altro codice. La risposta delle 15:41 ([qui][a3232old]), che chiedeva l'ottavo rifiuto con un test, era data prima che Carmine vedesse la
  raccomandazione, ed è sostituita da questa: niente di quella risposta è entrato nel codice.
- **`main` unito di nuovo** a `1f2a687` (le #230 di E10g, #228 di E5 e #234, il piano 1.31): conflitti solo nei documenti — in `10` la
  riga e la sezione di E10g prima di quelle di E10h; in `HANDOFF-M4.md` l'intestazione di E10h aggiornata (con il testo di E5 che resta
  vero), il blocco di E10h in cima e poi quelli di E5, E10g ed E4b, e in «Che cosa manca» prima E10g poi E10h.
- **Verificato**: `dotnet build IvaoHub.sln` 0 avvisi; `dotnet format --verify-no-changes` su `DomainContracts.cs`: pulito; unità
  **1187/1187**; integrazione intera, senza filtro, **497/497** al primo giro (5,5 minuti), `WithdrawnByStakeholderTests` compresa; le
  regole di `core-guard` dalla merge base `1f2a687`: tredici file, cinque del nucleo con la nota nuova — passa.

[rv232]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/232#issuecomment-6039667157
[a232]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/232#issuecomment-6039778269
[a3232]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/232#issuecomment-6041679155
[a3232old]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/232#issuecomment-6041353964

### E10k — Nucleo: le parole delle liste e i titoli delle schede

**Da dove viene**: non c'era in E0. Sono quattro cose che dalberone ha visto sul banco di prova il 6 e il 7 ottobre 2026 (sui branch di
E4 e di E5), tutte nel codice di `main`. Le ha portate a Carmine sull'issue #224 (le prime due, con l'offerta di prenderla) e in un
commento della stessa issue (le altre due), e **Carmine ha deciso** ([la sua risposta sulla #224][a224k], 9 ottobre, data in chat alla
sessione master e pubblicata su sua istruzione): sì ai quattro punti, una PR piccola del nucleo con una nota nuova, le chiavi nuove del
nucleo nelle due lingue, nessun test del maintainer cambiato. Branch `m4/e10k-lists-and-titles`, da `main` a `0f72737`. **PR del
nucleo** senza coda, con la sua nota (caso b); niente in C#.

1. **La paginazione** della lista generata con le parole dei file di lingua, non con quelle di `Pagination` di Atmosphere.
2. **Il titolo della scheda**: il nome della divisione come predefinito, mai quello del prodotto; un titolo proprio per le pagine elenco
   del nucleo e per la pagina che non c'è; il modo per le pagine elenco dei moduli.
3. **Una frase vuota per lista**, facoltativa; quella di oggi resta il predefinito.
4. **«Premi Invio per salvare»** tolto o riformulato sotto una casella di più righe.

**Test**: vitest per le parole della paginazione nelle due lingue, i titoli, la frase vuota (la predefinita e quella di una lista) e la
frase di Invio sotto una casella di più righe.
**Fatta quando**: nessuna lista dice una parola inglese sotto un'altra lingua, nessuna scheda dice il nome del prodotto, una lista di un
modulo può dire la sua frase vuota, e il lettore di schermo non promette un salvataggio che Invio non fa.

**Com'è andata** (9 ottobre 2026, branch `m4/e10k-lists-and-titles`, PR #238, del nucleo senza coda, da `main` a `0f72737`):

- **Fatto** (nota nuova `2026-10-09-le-parole-delle-liste-e-i-titoli-delle-schede`, **decisa**, con il link alla risposta di Carmine):
  - **la paginazione** (`Pages` in `web/src/shared/list/DataList.tsx`): i pezzi di `Pagination` di Atmosphere (`PaginationRoot`,
    `PaginationContent`, `PaginationItem`, `PaginationLink`) con le icone di `lucide-react` e le chiavi `list.pages.label`, `.previous`,
    `.next`, `.first`, `.last`; lo stesso aspetto e la stessa finestra di prima;
  - **la frase vuota**: `emptyDescription` di `DataList`, accanto a `emptyAction`;
  - **i titoli** (`web/src/shared/seo/PageMetadata.tsx`): `DivisionTitle`, montato da `Root` in `web/src/routes/__root.tsx`; `title` e
    `description` di `PageMetadata` prendono anche una frase tradotta, e `divisionName` è facoltativo; `NotFound` dice il suo titolo;
    `/news` e `/documents` (`PublicListScreen`), `/calendar` (`PublicCalendarScreen`) e `/search` (`SearchResults`) il loro; `_public/$.tsx`,
    `HomePage` e `PublicEntryScreen` non passano più il nome a mano;
  - **la frase di Invio**: `whereEnterSaves` in `web/src/shared/forms/SchemaForm.tsx`, i tre casi, la chiave `form.submitHintOneLine`;
  - **sei chiavi nuove** in `locales/{en,it}/common.json`, nessun doppione nei file dei moduli (cercato prima, piano §16 punto 16);
    `docs/UI-GUIDELINES.md` dice le quattro cose in inglese;
  - **i test** (vitest, 16): `web/src/shared/list/DataList.words.test.tsx` (7), `web/src/shared/forms/SchemaForm.enter.test.tsx` (5),
    `web/src/routes/-titles.test.tsx` (4, sulla radice vera dell'app).
- **Scelte di dalberone** (9 ottobre, chieste prima del codice con la raccomandazione per prima; tutte e tre accolte):
  1. **la parte degli eventi in una fase dopo**. Il prompt chiedeva di usare in questa PR i pezzi nuovi negli eventi (il titolo di
     `/events`, le frasi vuote delle schede «Slot» e «Rotte»), ma `CLAUDE.md` §0 regola 6 vuole il nucleo in una PR a sé, mai insieme
     al codice del modulo che lo usa (come E4b con la striscia). La fase dopo è scritta nella nota (§4) e nell'handoff;
  2. **parole e non frecce** nella paginazione;
  3. **tre casi** per la frase di Invio: niente frase, la frase riformulata, la frase di oggi.
- **Scelte della fase, scritte nella nota** (§2, §5):
  1. **un «…» solo dove nasconde una pagina**: Atmosphere ne disegna uno anche sulla prima e sull'ultima di tre pagine, accanto al numero
     a cui porta;
  2. **il titolo predefinito alla radice** e non nel layout: la pagina che non c'è della radice non ha layout, e lì `DivisionTitle` sta
     sopra tutto. Regge sull'ordine in cui React 19 mette i `<title>`, letto nel sorgente di react-dom 19.2.8 e provato sulla radice vera;
  3. **`divisionName` facoltativo, non tolto**: lo passa la pagina di un tour, che è del maintainer;
  4. **le tre pagine del nucleo senza il nome a mano**, perché nel nucleo ci sia una strada sola;
  5. **`/search` fra le pagine elenco**: la pagina dei risultati ha il suo titolo, come le altre;
  6. **una prop per la frase vuota**, non una convenzione su `<labels>.empty`: lo stesso `labels` lo usano più liste.
- **Trovato**:
  - ⚠️ **`WeatherTests.AForecastIsAskedForWithADateAndWithoutHours`** (unità, del maintainer, T2) cade dalle 06:00 UTC del 9 ottobre su
    ogni branch, `main` compreso: chiede la storia del 9 settembre 2026, e oltre i 30 giorni di `IWeatherSource.HistoryWindow`
    `NoaaWeatherClient.GetHistoryAsync` risponde `null`. L'ha visto la sessione di E10i, che l'ha detto a questa; dalberone ha aperto
    l'issue #236 per Carmine. Non toccato (regola 3): fino alla correzione `build-test` è rosso su ogni PR, questa compresa;
  - in un test che costruisce un router suo, `router.navigate({ to })` prende i tipi delle rotte registrate dall'app (`/search` vuole i
    suoi parametri): il test naviga con `router.history.push`.
- **Lette le fasi che corrono accanto** (9 ottobre): E10j ha scritto a questa sessione quali file tocca (i job, `Program.cs`,
  `schema.d.ts`), nessuno di questa fase; E10i tocca l'interceptor e il modulo di prova; E6b i file degli eventi. Conflitti solo nei
  documenti di M4, per chi arriva secondo.
- **Verificato, in locale** (9 ottobre 2026, `main` a `0f72737`):
  - `pnpm lint`, `pnpm typecheck` e `pnpm i18n:check` (822 chiavi letterali in `en` e `it`) verdi; `pnpm test` **659/659** in 91 file;
  - i test nuovi da soli 16/16; **la prova al contrario**: con il codice di `main` rimesso (le parole e i test nuovi tenuti) cadono 12 test
    su 16, e i quattro che passano sono quelli di ciò che non cambia; rimessi i file della fase, 16/16;
  - `dotnet build IvaoHub.sln` 0 avvisi; unità **1186/1187** (cade solo `WeatherTests…`, sopra); integrazione intera, senza filtro,
    **497/497** al primo giro (5,7 minuti);
  - la smoke `pnpm e2e` **173/173** (con 16 worker: il `--workers=2` dato dopo il `--` di `pnpm run` non è arrivato a Playwright);
  - `pnpm e2e:full` sulla porta 5131 con `ivaohub_e2e_e10k` nuovo: **57/57** al primo giro (10,8 minuti);
  - **guardato in un Chrome vero** (il pannello del browser dell'app, Chrome 152, sul banco di questa fase rimesso in piedi senza
    ripubblicare né mandare mail): le schede dicono «Calendario — IVAO Italia», «News — IVAO Italia», «Documenti — IVAO Italia», «Cerca
    — IVAO Italia», «Questa pagina non esiste — IVAO Italia» e «Home — IVAO Italia»; `/events`, `/tours` e il back office «IVAO Italia».
    Nella testa del documento l'ordine è quello che la nota dice: il titolo della pagina, poi quello della divisione, poi quello di
    `index.html`. L'audit (531 righe, pagina 5) dice «Pagine», «Precedente», «Prima pagina», 4, 5, 6, «Ultima pagina», «Successiva», con
    l'aspetto di prima e nessuna parola inglese;
  - le regole di `core-guard` rifatte in PowerShell dalla merge base (`0f72737`): venti file, nessuno del maintainer, diciassette del
    nucleo con la nota nuova — passa.
- **Non verificato**:
  - la CI (la dice la PR, rossa su `build-test` per `WeatherTests` finché Carmine non lo corregge);
  - `/tours` (del maintainer, da adottare con una riga) e la fase degli eventi dopo questa;
  - un lettore di schermo vero sulla frase di Invio: i test leggono il testo nascosto nel DOM, e nel Chrome vero non l'ho guardata;
  - un browser diverso da Chrome; i titoli in inglese nel Chrome vero (le stesse chiavi, provate in inglese e in italiano da vitest).

**Dopo il primo push** (9 ottobre 2026, la sessione che coordina, con la misura della sessione di E4c sulla testa `d307774`):

- **`pnpm -C web run format:check` cadeva su quattro file della fase** (`-titles.test.tsx`, `SchemaForm.enter.test.tsx`,
  `SchemaForm.tsx`, `DataList.words.test.tsx`: righe oltre 110 colonne, un ternario che sta su una riga, uno `<span>` fra parentesi). Non
  l'avevo eseguito: la lista dei passi della fase non lo nominava. Corretto con `prettier --write` sui quattro file, solo formattazione
  (26+/15-), in un commit `style` a sé.
- ⚠️ **La CI oggi non l'avrebbe detto**: quando «Test .NET» cade per `WeatherTests` (#236), ogni passo dopo è saltato — lint, formattazione,
  typecheck, vitest, la smoke, il giro completo e il pacchetto. Finché #236 è aperta, quei passi si fanno in locale e la PR li elenca.
- **Rifatto sulla nuova testa**: `pnpm lint`, `pnpm format:check` («All matched files use Prettier code style!»), `pnpm typecheck`,
  `pnpm i18n:check` (822 chiavi) verdi; `pnpm gen:api` senza differenze; `pnpm test` **659/659** in 91 file; la smoke **173/173**. Un giro
  della smoke cominciato poco prima di un riavvio dell'app ne aveva dati 160/173, con le cadute solo nelle spec della formazione
  (`toBeVisible`); rifatta a macchina ferma, 173/173, e i quattro file della formazione con `--repeat-each=2` 62/62.

[a224k]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/224#issuecomment-6070089222

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
accanto a E2 ed E10a–E10e partite lo stesso giorno; dopo la revisione, fusa con `main` a `c98b272`, poi a `db9268f` e a `c441839`):

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
    --name-status origin/main...HEAD`: nessun file del maintainer, i file del nucleo con la nota;
  - **`main` fuso altre due volte su richiesta del master**, sempre con un merge e con i conflitti solo in `HANDOFF-M4.md` (risolti
    tenendo il paragrafo di ogni fase): il 30 settembre dopo E10e (#206), i tour sulla distanza del nucleo (#211) ed E10d (#205) — build
    senza avvisi, unità 928/928; l'integrazione non rifatta in locale lì, `build-test` e `core-guard` verdi su quell'head (`9b35418`)
    —, e il 1° ottobre dopo E2 (#209), con lo strumento
    dell'app che fonde la base: build senza avvisi, unità **962/962**, integrazione intera **441/441** (5,6 minuti, al primo giro), in `web/` lint, typecheck,
    format:check e i18n:check verdi, 599 test in 81 file, `pnpm gen:api` senza differenze. Docker Desktop era spento: avviato prima del
    giro.
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
