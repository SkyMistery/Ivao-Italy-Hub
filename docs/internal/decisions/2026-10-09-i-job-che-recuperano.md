# I job che recuperano e il POST pianificato

**Data:** 9 ottobre 2026
**Stato:** **Proposta**. Il lavoro è deciso: è la voce 2 della coda del codice del nucleo, la strada B e la strada E della nota
`2026-09-28-i-job-quando-passenger-spegne-l-hub` (decisa da Carmine sulla #165: [B e le quattro correzioni][d165a], [il POST
pianificato al posto del Worker][d165b]), affidata a `dalberone` da Carmine sulla issue #231 ([la risposta][a231], autore
`SkyMistery`: una fase del nucleo sua, una PR sua, e una nota nuova che fissa la forma nel codice). Questa è quella nota. Tre punti
della forma non sono decisi dalla nota del 28 settembre, o la cambiano: sono le **tre domande del §2**, sulla PR #239, e il codice
di questa PR è la raccomandazione di ognuna.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: si estendono lo scheduler (Quartz) e il registro dei giri (`hub_jobs_log`);
nessuna tabella nuova, nessuna migrazione, nessun job nuovo, nessun bus. È un cambio del nucleo, nella sua PR (`CLAUDE.md` §0
regola 6), e nessun job cambia: il meccanismo copre ogni job che Quartz conosce, quelli dei moduli compresi.
**Fase:** E10j di `10-piano-implementazione-m4.md`, branch `m4/e10j-jobs-catch-up`.

[d165a]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/165#issuecomment-5865067623
[d165b]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/165#issuecomment-5865413362
[a231]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/231#issuecomment-6070088780

## 1. La forma nel codice

Tutto in `src/IvaoHub.Core/Jobs/` (nuovo), più la meccanica del blocco con un nome in `Core/Services/DatabaseLock.cs`.

### 1.1 Quando un job è dovuto (`JobSchedule`)

**Un job è dovuto quando uno dei suoi trigger cron ha un'occorrenza dopo l'inizio del suo ultimo giro e non oltre adesso**, letta
nel fuso del trigger (`CronExpression.GetNextValidTimeAfter(ultimo) <= adesso`, la regola della nota del 28 settembre). Tre
precisazioni:

- **L'ultimo giro è l'ultimo finito**, qualunque sia l'esito: la riga più recente del job in `hub_jobs_log` con `finished_at`
  scritto. Un giro rimasto `running` perché il processo è morto non è finito, e si recupera; un giro fallito aspetta la sua
  occorrenza dopo, come oggi. La nota del 28 settembre dice «l'ultimo giro **riuscito**»: è la **domanda 1** (§2).
- **Oppure l'ultimo giro che questo processo ha fatto partire**, se è più recente. `FlightCheckJob` dei tour non scrive una riga
  quando non trova niente da fare («six empty lines an hour would hide the others»): senza questa memoria, per la regola sarebbe
  dovuto sempre, e il controllo di ogni minuto lo lancerebbe ogni minuto. Con la memoria lo lancia il suo cron, ogni dieci minuti,
  come oggi; un altro processo, che non ha la memoria, lo lancia una volta al suo primo controllo. Nessuna riga del registro in più.
- **Un'occorrenza che cade entro un secondo dall'inizio di un giro è di quel giro** (`JobSchedule.SameOccurrence`). Un giro
  lanciato un attimo prima di un'occorrenza fa il lavoro che l'occorrenza chiede: il riepilogo di ieri, perso, recuperato dal
  controllo delle 06:59:59 senza la tolleranza ripartirebbe alle 07:00, e chi lo riceve ne avrebbe due a un secondo l'uno
  dall'altro. Un secondo è molto meno del minuto che separa le occorrenze di ogni cron dell'hub (tutti hanno `0` nei secondi), quindi
  non toglie mai a un job una sua occorrenza. Quartz, invece, non fa scattare un trigger prima della sua ora: il ciclo di
  `QuartzSchedulerThread` della 3.20 aspetta finché il tempo che manca è maggiore di zero (letto nell'IL della DLL; il Quartz di Java
  si sveglia a 2 ms), e su 31 scatti misurati il listener è sempre arrivato dopo l'ora prevista, da 0,07 a 49 ms.

Un job che non ha mai finito un giro è dovuto; un job senza trigger cron non lo è mai (nessuno lo lancia da sé).

### 1.2 Il guardiano: un solo esecutore, e un giro per occorrenza (`ScheduledJobs`)

Un `ITriggerListener` di Quartz registrato su **tutti** i trigger (`EverythingMatcher<TriggerKey>.AllTriggers()`): sta davanti a
**ogni** giro che Quartz fa partire — il suo cron, il recupero (§1.4), il POST (§1.5) — e nessun job cambia. In
`VetoJobExecution`:

1. **Prende il blocco del job senza aspettare** (§1.3). Se lo tiene un altro processo, il giro è suo: questo **salta**. È la
   prima correzione della nota: *un solo esecutore per job fra più processi*. Dentro un processo bastava già
   `[DisallowConcurrentExecution]`.
2. **Con il blocco in mano rilegge l'ultimo giro** (§1.1) e **salta** il giro se nessuna occorrenza è passata da allora: un altro
   processo l'ha appena fatto, o il recupero l'ha già lanciato. Vale anche per il cron di Quartz, ed è quello che fa partire **una
   volta sola** il riepilogo delle 07:00 con due processi vivi alle 07:00 (§1.7).
3. Altrimenti il giro parte, e il blocco torna in `TriggerComplete`, alla fine del giro, o con il processo.

**Il guardiano non ferma mai un job perché fallisce lui**: un blocco che non si può chiedere (il database che non risponde a
`GET_LOCK`, un utente che non può chiamarlo) o un ultimo giro che non si legge lasciano partire il giro com'è partito fino a
oggi, con un avviso nel log. È la regola del blocco dell'inizializzazione («una protezione non è mai la cosa che ferma il sito»,
Carmine sulla #218). Uno scheduler che si ferma mentre il guardiano decide salta il giro.

**Che cosa non passa dal guardiano**: un giro chiamato direttamente con `RunAsync`, fuori da Quartz — i test, e `RefDataSyncJob`
dentro `InitializeAsync` quando la tabella dei centri è vuota (il primo avvio di un'installazione): due processi al primo avvio lo
fanno ancora tutti e due, come oggi, e scrivono gli stessi dati.

### 1.3 Il blocco: `GET_LOCK`, tutti i job di un processo su una connessione (`JobLocks`)

Il blocco di un job è il blocco con un nome di MariaDB, **`hub-job:<database>:<job>`** (il database nel nome perché il server
non isola i database: due installazioni non si aspettano mai; oltre i 64 caratteri un pezzo dell'hash, come `hub-init:`), chiesto
**senza aspettare** (`GET_LOCK(nome, 0)`).

- **Una connessione sola per processo**, fuori dal pool, aperta con il primo blocco e chiusa quando torna l'ultimo: tutti i
  blocchi dei job di un processo stanno su quella sessione (MariaDB tiene più blocchi per sessione dalla 10.0.2). Al primo
  risveglio del mattino il recupero lancia una decina di job insieme (§1.4) e Quartz ne fa girare fino a dieci: con una
  connessione per giro sarebbero dieci connessioni in più oltre il pool, su un server che ha un tetto per utente (25–50, piano
  §2.5) condiviso con vIPI. Così è **una**, e solo mentre un job gira.
- **Un processo che muore si porta via la connessione, e i blocchi con lei**: nessuna riga da ripulire, nessuna scadenza da
  indovinare (la ragione per cui la nota del blocco dell'inizializzazione, §3, ha scartato una riga con una scadenza). La nota del
  28 settembre diceva «un giro `running` con una scadenza»: è la **domanda 2** (§2).
- **La sessione darebbe lo stesso blocco due volte** (MariaDB conta i `GET_LOCK` ripetuti), quindi il processo chiede ogni job una
  volta sola e risponde «occupato» a una seconda richiesta del suo.
- **La meccanica è una sola**: apertura fuori dal pool, `GET_LOCK`, `RELEASE_LOCK`, il nome con l'hash stanno in
  `Core/Services/DatabaseLock.cs`, che usa anche il blocco dell'inizializzazione (`InitialisationLock`, la cui API pubblica, i
  messaggi del log e le parole di `starts.txt` non cambiano: lo provano `InitialisationMarkerTests` e `InitialisationKeyTests`).
- **Una connessione che cade mentre i job girano** perde tutti i loro blocchi insieme: ogni giro finisce com'è, e alla fine dice
  nel log che il suo blocco si era perso. Nessun controllo periodico, come per il blocco dell'inizializzazione (§5 di quella nota):
  ⚠️ il `wait_timeout` del server non l'ho letto, e una connessione ferma per tutto un giro lungo conta su quello.

### 1.4 Il recupero (`JobCatchUp`)

Un `BackgroundService`: **cinque secondi dopo che l'hub è partito** (dopo la pagina di chi l'ha svegliato) e poi **ogni minuto**,
lancia con `IScheduler.TriggerJob` ogni job dovuto (§1.1). Non aspetta i giri. Salta un job in pausa nello scheduler (ogni suo
trigger cron in pausa), uno che sta girando in questo processo, e uno già lanciato e non ancora partito.

- **Acceso per difetto solo in `Production`** (`Jobs:CatchUp`, `true` o `false` lo decide l'installazione). Altrove — l'hub di
  chi sviluppa, i test d'integrazione, il banco e2e — ogni job gira alle sue ore come prima: un test non vede giri che non ha
  chiesto (i job notturni di un database di test scaricherebbero i contorni dei FIR e chiamerebbero IVAO, `CONTRIBUTING.md`:
  «No call to IVAO or any external service in a test»), e il banco non cambia i suoi dati. Il guardiano invece c'è ovunque.
- **Che cosa non fa**: tenere vivo il processo. Un giro lungo lanciato dopo un risveglio può essere fermato da Passenger 10–30 s
  dopo l'ultima richiesta; non finisce, non scrive la sua fine, e il risveglio dopo lo recupera. Il lavoro che deve finire è del
  POST (§1.5).
- ⚠️ **La prima consegna che lo porta**: al primo risveglio dopo il caricamento girano, una volta, i job la cui ora è passata
  dall'ultimo giro finito, e **quelli che su un'installazione non sono mai girati** — sulla prova, con Passenger, i notturni: i dati
  di riferimento, i contorni dei FIR (scaricati per la prima volta), il promemoria dei documenti da rivedere (con le sue mail), la
  scadenza dei file (elimina quelli i cui usi sono tutti finiti), il ritiro dei PIREP, la conservazione di tracce, meteo e tour, la
  scadenza dei training. È quello che avrebbero fatto a processo vivo, ma tutto in quel momento: chi consegna lo sa prima.

### 1.5 Il POST pianificato (`JobRunEndpoints`)

**`POST /api/jobs/run`**: fa quello che fa un controllo del recupero, ma **dentro la richiesta**, e risponde quando i giri sono
finiti (Passenger non spegne un processo che sta rispondendo, nota del 28 settembre §5). È l'indirizzo dell'operazione pianificata
di Plesk (strada E).

- **Il token** sta in `Jobs:Token`, nel file dei segreti o in `Jobs__Token` (`CLAUDE.md` §6), e arriva come
  **`Authorization: Bearer <token>`**. È un segreto dell'installazione e non di una persona: non è un token personale, apre solo
  questo indirizzo, e l'unica cosa che fa fare è far girare adesso quello che è dovuto comunque. **Almeno 32 caratteri**: uno più
  corto ferma l'avvio con un messaggio che nomina la chiave (`JobOptionsValidator`); nessun token va bene.
- **Le risposte**: un'installazione senza token non ha l'indirizzo (**404**); una chiamata con un altro token è rifiutata (**401**,
  con `WWW-Authenticate: Bearer`, e una riga nel log, mai il token); una chiamata senza `Authorization` la ferma prima il
  guardiano di `/api` contro le richieste da un altro sito (**403**, `HubPipeline.UseCrossSiteRequestGuard`), che lascia passare
  un `Bearer` per la stessa ragione per cui lascia passare i token personali. Il confronto è in tempo costante, sugli hash.
- **Aspetta al più 80 secondi** (`JobRunEndpoints.LongestWait`) i giri che lancia **e quelli già in corso quando arriva** (un giro
  lanciato dal recupero un attimo prima): la risposta deve partire prima che i proxy davanti si arrendano (Cloudflare a 100 s),
  con l'avvio di un hub spento davanti. Un giro ancora in corso resta in corso finché il processo vive, e la risposta lo dice.
- **Che cosa risponde**: `200` con l'elenco dei job lanciati e l'esito di ognuno (`Ran`, `Skipped`, `Running`); i dettagli di ogni
  giro sono nella sua riga di `hub_jobs_log`.
- **Il pannello**: la nota del 28 settembre lascia a questa PR, secondo la risposta dell'amministratore, se accettare il token
  anche nell'indirizzo per «Recupera un URL», che fa solo una GET senza intestazioni. Il codice di oggi accetta **solo
  l'intestazione**: è la **domanda 3** (§2).
- È un verbo del nucleo fuori da ogni risorsa, come la diagnostica della richiesta (piano §16 punto 6): nessuna riga letta o
  scritta qui, i job scrivono le loro. Sta nel contratto OpenAPI (`RunDueJobs`) e nel client generato, che la pagina non usa.

### 1.6 L'esito di ogni mail salvato subito (`NotificationDispatchJob`)

La seconda correzione della nota: dopo ogni mail il suo esito (inviata, o il tentativo fallito) si salva **subito**, non una
volta sola alla fine del lotto. Un processo fermato a metà lotto ha scritto ogni mail che ha mandato, e il giro dopo non ne
rimanda nessuna. Il salvataggio non passa dal token del giro: una mail partita si scrive anche mentre l'host si ferma. Resta
**almeno una volta**: una mail consegnata e un processo ucciso prima del suo salvataggio la rimandano, ma è al più una per giro,
e non il lotto intero.

### 1.7 Il riepilogo delle 07:00 una volta al giorno

La terza correzione è **generica, nel guardiano**, e `ReviewDigestJob` (Flight Ops) non cambia: con due processi vivi alle 07:00
uno solo lo manda (il blocco), un processo che si sveglia alle 08:00 lo manda una volta (il recupero) e uno che si sveglia dopo non
lo manda di nuovo (l'ultimo giro finito è delle 08:00), e un giro fallito non si rifà prima del giorno dopo (§1.1). Il «già mandato
oggi» è la riga del giro in `hub_jobs_log`: il job non deve scrivere un segno suo. Vale per ogni riepilogo di un modulo, quello degli
eventi compreso. **Resta di Flight Ops** il suo fuso (§1.8): oggi le «07:00» sono quelle dell'orologio del server.

### 1.8 I fusi espliciti

La quarta correzione. **Nel nucleo**, i due trigger che non dicevano il fuso lo dicono:

- `notification-dispatch` (ogni minuto): **UTC**. Un minuto è un minuto in ogni fuso; un cron che non nomina il fuso è uno che
  qualcuno deve indovinare;
- `fir-boundaries-sync` (domenica 04:10): **il fuso della divisione**, come le altre notti del nucleo (i dati di riferimento alle
  03:15, i file alle 04:00), letto con la pipeline delle opzioni come loro. Il commento non diceva UTC, e la nota del 28
  settembre (§7) non lo nomina fra quelli da mettere in UTC.

**Nei moduli** non cambia niente in questa PR: è codice dei moduli, e il nucleo si legge da solo (`CLAUDE.md` §0 regola 6). Gli eventi
(`events-release`) e il training (`training-reminders`) girano ogni quarto d'ora, e un quarto d'ora cade agli stessi istanti in ogni
fuso (gli scarti dei fusi veri sono multipli di 15 minuti); `training-expiry` e i job del nucleo alle ore della divisione dicono
già il fuso. **Gli otto trigger di Flight Ops** sono del maintainer: §3.

Il recupero legge ogni cron nel fuso del suo trigger (`ICronTrigger.TimeZone`), quindi è coerente con Quartz qualunque fuso un
trigger abbia, detto o no.

## 2. Le domande a Carmine

Sulla PR #239, una per una; il codice è la raccomandazione.

1. **«L'ultimo giro finito» al posto di «l'ultimo giro riuscito»** (§1.1). Raccomandato: **finito**. Con «riuscito» alla lettera
   ogni giro che non è `succeeded` si rifarebbe al controllo dopo, cioè **ogni minuto**: `RefDataSyncJob` scrive `partial` o
   `skipped` quando una parte di IVAO torna vuota, e riscaricherebbe 14 MB di aeroporti ogni minuto; un'IVAO giù, o una credenziale
   scaduta, sarebbero chiamate ogni minuto finché non torna; e un riepilogo fallito a metà, dopo aver messo in coda una parte delle
   sue mail, le rimanderebbe a chi le ha già. Con «finito» un giro fallito aspetta la sua occorrenza dopo, come oggi, e si recupera
   solo il giro che non è finito (il processo morto) o che non è partito. L'alternativa — «riuscito» con un intervallo fra un
   tentativo e l'altro — è un meccanismo in più che la nota non chiede.
2. **Un solo esecutore con il blocco del database, non con un giro `running` con una scadenza** (§1.3). Raccomandato: **il
   blocco**, `GET_LOCK` su una connessione del processo. La riga con la scadenza chiede di indovinare la scadenza (più lunga del
   giro più lungo, `RefDataSyncJob`, ma un processo morto blocca il suo job fino ad allora, anche la coda delle mail), di renderla
   atomica fra due processi (un blocco comunque, o un deadlock voluto) e forse una colonna; il blocco del database muore con il
   processo. È la scelta che Carmine ha fatto il 6 ottobre per l'inizializzazione (#218), con le stesse ragioni. Costa una
   connessione fuori dal pool per processo, solo mentre un job gira.
3. **Il token nell'indirizzo, per «Recupera un URL»** (§1.5). La nota del 28 settembre lo lascia a questa PR secondo la risposta
   dell'amministratore, che non conosco. Raccomandato: **se il pannello concede «Esegui un comando», solo l'intestazione** (il
   codice di oggi: `curl -fsS -X POST -H "Authorization: Bearer <token>" https://<host>/api/jobs/run`); **se concede solo «Recupera
   un URL»**, una `GET /api/jobs/run?token=…` accanto al POST, sapendo che il token finisce nei log di nginx, di Cloudflare e dei
   proxy: è un token solo per questo indirizzo, si cambia senza toccare altro, e chi lo ruba può solo far girare adesso quello che
   è già dovuto (un risveglio dell'hub lo fa già chiunque apra una pagina). Su questo hosting (FTP, niente shell) mi aspetto la
   seconda. Il codice della GET, se la risposta è sì, entra in questa PR prima dell'unione: poche righe e un test.

## 3. Che cosa resta a Flight Ops, cioè al maintainer

Il codice dei tour non lo tocco (`CLAUDE.md` §0 regola 2). Una richiesta, per una sessione di Carmine dopo l'unione, come il
passaggio dei tour al nucleo dopo E10e ed E10g:

- **I fusi degli otto trigger** di `FlightOpsModule.cs`, secondo la nota del 28 settembre §7: in **UTC** quelli il cui commento dice
  UTC (`flightops-pirep-withdrawal` 03:20, `flightops-track-retention` 03:40, `flightops-weather-retention` 03:50) e `flightops-weather`
  (:05 e :35, le ore dei METAR); nel **fuso della divisione** `flightops-review-digest` (le 07:00 di chi legge: il suo commento dice
  «07:00 UTC, the morning in Europe», da correggere) e `flightops-tour-retention` (il 1° del mese alle 04:20, «on the server's
  clock»: dopo il job dei file del nucleo, che è nel fuso della divisione); `flightops-tour-release` e `flightops-flight-checks`
  hanno una cadenza, e il fuso non cambia quando scattano, ma si dice lo stesso. Il trigger con il fuso della divisione si aggiunge
  con la pipeline delle opzioni, come `training-expiry`.
- **Niente altro è necessario**: il riepilogo è una volta al giorno per il guardiano (§1.7), `FlightCheckJob` non è lanciato ogni
  minuto (§1.1), e ogni job dei tour è coperto dal recupero e dal blocco così com'è.

## 4. Le alternative scartate

- **Il recupero come job Quartz** (un cron ogni minuto): la nota del 28 settembre dice «nessun job nuovo», e un job che lancia i
  job sarebbe sotto il suo stesso guardiano e nel registro ogni minuto. È un `BackgroundService`.
- **I job che non scrivono più la loro riga, scritta dal nucleo intorno al giro**: una riga sola per giro e nessuna memoria per
  `FlightCheckJob`, ma un cambio di tutti e diciassette i job, otto dei quali di Flight Ops; e il nucleo non sa l'esito di un job che
  non lancia mai un'eccezione. Il registro resta dei job.
- **Un blocco per giro, ognuno sulla sua connessione** (come `InitialisationLock`): più semplice, ma una connessione in più per
  ogni job che gira (§1.3).
- **Il blocco su una connessione del pool**: occupa un posto del pool per tutto il giro, e reso al pool con il blocco in mano
  (un `RELEASE_LOCK` fallito) lo terrebbe vivo (nota del blocco dell'inizializzazione, §3).
- **`[DisallowConcurrentExecution]` più un controllo «già fatto» in ogni job**: una regola da ricordare in ogni job nuovo, e i
  job dei tour non si toccano.
- **Il POST che fa girare i job da sé, fuori da Quartz**: due strade per far partire un job, e la seconda senza
  `[DisallowConcurrentExecution]`. Il POST lancia con `TriggerJob` come il recupero, e aspetta.
- **Quartz con lo store persistente e il clustering** (strada D della nota del 28 settembre): già scartata lì.

## 5. Verificato

- **Integrazione, MariaDB 11.4.10 vera** (`ScheduledJobsTests`, `JobLocksTests`, `NotificationOutcomeTests`), con due job sonda
  del test con un cron di una volta l'anno e ogni altro job dell'host in pausa prima del primo controllo:
  - un giro perso mentre nessun processo era vivo si recupera da solo pochi secondi dopo l'avvio, e un job non dovuto non parte;
  - di due processi (due host con uno scheduler di nome suo: Quartz ne tiene uno per nome in un processo) uno fa il giro e l'altro
    lo lascia a lui; dopo, con il blocco libero, l'altro trova l'occorrenza fatta e salta di nuovo;
  - un giro fatto per la sua occorrenza non si rifà nello stesso processo;
  - il POST senza token (403), con un altro token (401, `WWW-Authenticate: Bearer`), su un'installazione senza token (404), e con il
    token: risponde solo dopo la fine del giro, con `Ran`, e la riga del giro è finita;
  - una corsa della coda fermata mentre consegna la terza mail: le prime due sono scritte `Sent`, le altre `Pending` senza
    tentativi, e il giro dopo manda solo le tre;
  - i blocchi di un processo stanno su una connessione sola (`IS_USED_LOCK` dà lo stesso id), un altro processo li trova occupati,
    uno restituito è subito dell'altro, l'ultimo restituito chiude la connessione; un database che non risponde è «non chiesto», non
    un'eccezione.
- **Al contrario, sul codice di `main`** (i file di produzione rimessi com'erano, toccati e ricompilati): cadono tutte e sei le prove
  di `ScheduledJobsTests` e `NotificationOutcomeTests`, ognuna per la sua ragione (due giri invece di uno, 404 invece di 401, nessun
  recupero, le due mail partite rimaste `Pending`).
- **Unità** (`ScheduledJobsTests`): la regola «dovuto» (occorrenze passate e no, il fuso del trigger, più trigger, la tolleranza del
  secondo, la coda di ogni minuto), il nome del blocco (database e job, 64 caratteri), il recupero acceso per ambiente, il
  validatore del token, il token come `Bearer`, i fusi dei due trigger del nucleo.

## 6. Non verificato

- **Passenger vero**: due processi veri, un processo spento e risvegliato, l'operazione pianificata di Plesk. Si verifica alla
  consegna: `diagnostics/starts.txt` dice i risvegli; `hub_jobs_log` dice che i job notturni girano la mattina, una volta, e che
  il riepilogo parte una volta; il log del giorno ha le righe «Launched … job(s) that were due» e «The scheduled task ran …»; la
  cronologia dell'operazione pianificata in Plesk dice il `200` e l'elenco.
- **Il `wait_timeout` del server** e il tetto delle connessioni dell'utente: non letti.
- **Quanto dura sul server un risveglio con i job notturni dietro**: la CPU di una decina di job insieme, mentre il visitatore
  naviga. Lo dirà `hub_jobs_log` con i tempi dei giri.
- **Il pannello di Plesk** (§2, domanda 3).

## Da portare nel piano

- **§5.2**, la riga «Job»: la forma — il guardiano davanti a ogni giro (il blocco `hub-job:<database>:<job>` su una connessione
  del processo, e l'ultimo giro riletto), il recupero cinque secondi dopo l'avvio e ogni minuto (acceso per difetto solo in
  `Production`, `Jobs:CatchUp`), il POST `/api/jobs/run` con il token `Jobs:Token`; un job è dovuto secondo l'ultimo giro **finito**
  (domanda 1).
- **§11.3 punto 6** (l'operazione pianificata): l'indirizzo, l'intestazione, i 32 caratteri, le risposte, l'attesa di 80 s; come la
  chiama il pannello (domanda 3).
- **§11.3 punto 9**, il trattino di Passenger: il codice c'è; resta all'host l'operazione pianificata e la domanda sull'inattività;
  il lavoro di un giro lungo finisce dentro il POST.
- **§16 punto 6** (gli endpoint scritti a mano del nucleo): `POST /api/jobs/run`, fra i verbi del nucleo fuori da ogni risorsa.
- **La coda del codice del nucleo** (`HANDOFF.md`): la voce 2 è fatta; la richiesta a Flight Ops del §3.
- **`CONTRIBUTING.md`**, la trappola «Quartz cron expressions run in local time»: resta vera per un trigger che non dice il fuso;
  il nucleo li dice tutti, e un modulo li dice (§1.8).
- `docs/DEPLOYING.md` è aggiornato in questa PR.
