# I job pianificati quando Passenger spegne l'hub

**Data:** 28 settembre 2026
**Stato:** **Decisa da Carmine sulla PR, 28 settembre 2026**. Le domande 1, 2 e 4 del §6 come raccomandato
(<https://github.com/SkyMistery/Ivao-Italy-Hub/pull/165#issuecomment-5865067623>). La domanda 3 **no**: al posto del
Worker Cloudflare c'è un'operazione pianificata di Plesk che chiama l'hub con un POST protetto da un token
(<https://github.com/SkyMistery/Ivao-Italy-Hub/pull/165#issuecomment-5865413362>, §6 e §8). Il codice arriva in PR sue:
prima `diagnostics/starts.txt`, poi i job che recuperano insieme all'indirizzo chiamato dall'operazione pianificata.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: lo scheduler (Quartz) e il registro dei giri (`hub_jobs_log`) ci sono già,
e si **estendono**; nessun job nuovo, nessun bus, niente shell. È un cambio del nucleo: la sua PR, con questa nota.
**Da dove viene:** piano §11.3 punto 9, secondo trattino; `docs/DEPLOYING.md` «Known limits».

## 1. Che cosa fa l'hub oggi

- **Tredici job Quartz, con lo store in memoria.** Nessun `UsePersistentStore` in `src/`; l'unico hosted service è quello
  di Quartz, `AddQuartzHostedService(WaitForJobsToComplete = true)` (`src/IvaoHub.Core/Ivao/IvaoServiceCollectionExtensions.cs:66`,
  `src/IvaoHub.Core/Notifications/NotificationServiceCollectionExtensions.cs:36`). Nessun altro `IHostedService` o
  `BackgroundService`; Training non ha job.
- **Solo trigger cron, nessuno `StartNow`.** A ogni avvio Quartz calcola la prossima occorrenza a partire da *adesso*: un'ora
  caduta mentre il processo era spento non è un *misfire* (quello riguarda solo un processo vivo in ritardo), è **persa**.
- **Ogni job scrive `hub_jobs_log`** (job, inizio, fine, esito, messaggio; indice su `(Job, StartedAt)`,
  `src/IvaoHub.Core/Data/Configurations/HubSchemaConfiguration.cs:147-157`). Lo rilegge solo `TourReleaseJob`.
- **All'avvio gira un solo lavoro**: se `IvaoCenters` è vuota, `RefDataSyncJob` dentro `InitializeAsync`, prima di accettare
  traffico (`src/IvaoHub.Web/HubPipeline.cs:209-214`).
- **`/health`** (`src/IvaoHub.Web/Program.cs:318`) è anonimo, fa un `SELECT 1` (`src/IvaoHub.Core/Data/DatabaseHealthCheck.cs`),
  non è limitato e risponde `no-store`.

| Job | Quando | Come decide che cosa fare | Se un'ora salta |
|---|---|---|---|
| `NotificationDispatchJob` | ogni minuto | le righe `Pending` (`NotificationDispatchJob.cs:68-72`) | **recupera** al giro dopo; senza giri nessuna mail parte |
| `TourReleaseJob` | ogni 15' | da quando è iniziato l'ultimo giro riuscito, letto in `hub_jobs_log` (`TourReleaseJob.cs:41-61`) | **recupera** |
| `FlightCheckJob` | ogni 10' | i PIREP senza controlli (`FlightCheckJob.cs:47-53`); l'invio li fa già in linea | recupera; è una rete di sicurezza |
| `RefDataSyncJob` | 03:15 | fotografia intera da IVAO (`RefDataSyncJob.cs:47-51`) | dati di riferimento vecchi |
| `FirSyncJob` | domenica 04:10 | fotografia intera | contorni FIR vecchi; su un'installazione nuova la tabella resta **vuota** fino alla prima domenica in cui il processo è vivo alle 04:10 |
| `DocumentReviewJob`, `MediaExpiryJob` | 03:30, 04:00 | lo stato delle righe | recuperano |
| `PirepWithdrawalJob`, `TrackRetentionJob`, `WeatherRetentionJob` | 03:20, 03:40, 03:50 | lo stato delle righe | recuperano |
| `TourRetentionJob` | il 1° del mese, 04:20 | lo stato delle righe | recupera, ma **un mese dopo** |
| `ReviewDigestJob` | 07:00 | la coda di *adesso*, e nessun segno di «inviato oggi» (`ReviewDigestJob.cs:50-60`) | il riepilogo del giorno è perso; **girato due volte, parte due volte** |
| `WeatherJob` | ogni 30' | i METAR/TAF *correnti* | i bollettini di quell'ora sono **persi**; all'invio del PIREP un recupero da NOAA copre in parte (`WeatherArchive.cs:180-218`) |

## 2. Che cosa si rompe, e quando

Passenger spegne il processo inattivo e lo riaccende alla richiesta dopo. **Un job gira solo se il processo è vivo nel
secondo esatto del suo cron.** Sull'hub non è misurato (§5); sul server di vIPI, che il piano §11.3 dice essere lo stesso, sì
(`vIPI Ivao Italy/deploy/cloudflare/LEGGIMI-ATC-ARCHIVER.md`, `docs/lavori-aperti.md`, `HANDOFF.md` §BG e §A120):

- il processo si spegne **circa 10 secondi** dopo l'ultima richiesta (16 set);
- un ping al minuto da un Worker Cloudflare: **53–58 avvii l'ora**, vita mediana 15 s; con un ping ogni 30 s, vita mediana 44 s
  e processo acceso il 72% del tempo, ma **sempre 60 avvii l'ora** (3 set);
- con un ping ogni 10 s, vita mediana **51 s**, 260 vite su 367 fra 45 e 60 s, arresto per `SIGTERM` da fuori: **il ping
  riaccende, non tiene su** (§BG). L'unico processo vissuto 90 minuti teneva aperto un circuito SignalR;
- `passenger_min_instances` su quel server **non si può avere** (`lavori-aperti.md`, «Keep-alive con un cronjob»);
- in più, morti silenziose a **hh:56–57** con un secondo processo avviato mentre il primo scrive ancora: causa fuori da vIPI,
  scritto a Ivao.It il 23 set, si aspetta risposta (§A120). Due processi insieme li aveva già misurati §A55.

Che cosa vuol dire per l'hub, con il traffico di una divisione:

1. **I sette job notturni (03:15–04:20) non girano quasi mai**: a quell'ora non c'è nessuno sul sito. Dati di riferimento
   fermi all'installazione, contorni FIR mai scaricati, pulizie che non partono.
2. **Le mail partono solo quando qualcuno sta usando il sito**: chi non c'è riceve la notifica alla prossima visita di
   chiunque altro. Di notte, al mattino.
3. **Il riepilogo delle 07:00 e i bollettini meteo si perdono**, perché non sanno recuperare.
4. **Un processo ucciso a metà lotto rispedisce mail**: `NotificationDispatchJob` salva gli esiti una volta sola, dopo tutto il
  lotto (`NotificationDispatchJob.cs:79`, `:124`); le mail già consegnate restano `Pending`.
5. **Due processi insieme fanno due volte lo stesso lavoro**: ognuno ha il suo Quartz, e `[DisallowConcurrentExecution]` vale
  solo dentro un processo. Lo stesso lotto di mail, due riepiloghi.

## 3. Le strade

| | Che cosa | Costo | Limite |
|---|---|---|---|
| **A. Ping esterno** | un Worker Cloudflare con un cron su `/health?t=…` (fuori dal repository, come `atc-archiver`), oppure «Recupera un URL» delle operazioni pianificate di Plesk, se il pannello lo concede | niente codice; un Worker da tenere; **ogni risveglio paga un avvio intero** contro il MariaDB condiviso (vIPI: 8,4 s, di cui 3 di migrazioni) | misurato su vIPI: sveglia, non tiene su. Da solo non fa scattare un cron alle 03:15:00 |
| **B. Job che recuperano** (nucleo) | un job è **dovuto** quando il suo cron ha un'occorrenza fra l'inizio dell'ultimo giro riuscito in `hub_jobs_log` e adesso (`CronExpression.GetNextValidTimeAfter(ultimo) <= adesso`, nel fuso del trigger). Si controlla qualche secondo dopo l'avvio e poi ogni minuto, e si lancia con `IScheduler.TriggerJob` | un file del nucleo e i suoi test; **nessuna tabella nuova**. Con lui: un solo esecutore per job fra più processi (un giro `running` con una scadenza, l'altro processo salta), l'esito di ogni mail salvato subito, il riepilogo «uno al giorno» | il lavoro si fa quando un processo c'è: senza ping, di notte aspetta la prima visita |
| **C. Alzare l'inattività di Passenger** | `passenger_min_instances` o l'idle time, dal pannello | niente codice; **serve chi amministra il server** (Ivao.It); `passenger_pool_idle_time` è un'impostazione globale | per vIPI non si può avere; non ferma le morti a hh:56 |
| **D. Quartz con store persistente** | AdoJobStore su MariaDB, con il clustering | undici tabelle `qrtz_` da creare con il DDL a mano in una migrazione, connessioni in più da un pool ≤ 15 | recupera le ore perse, ma non le vite da 50 s né i job che lavorano a finestra: B fa lo stesso con il registro che c'è. **Scartata** |
| **E. Un indirizzo «tick»** chiamato dal ping, che fa il lavoro dovuto dentro la richiesta | un endpoint e la sua protezione | un processo non è inattivo mentre risponde, quindi il lavoro finisce | la **seconda mossa**, se la misura del §5 dice che un giro lanciato all'avvio non fa in tempo a finire |
| **F. I job fuori dall'hub** (un Worker che scrive nel database) | — | un secondo programma che riscrive le regole del nucleo (`CLAUDE.md` §2). **Scartata** |

## 4. La raccomandazione

> **Superata in parte dalla decisione** (§6): i passi 1 e 2 restano; il passo 3 è sostituito dalla strada **E** chiamata da
> un'operazione pianificata di Plesk (§8). La raccomandazione resta com'era scritta, perché la nota dice che cosa è stato
> proposto e che cosa è stato deciso.

Tre passi, in quest'ordine:

1. **Prima misurare** (nucleo, piccolo): `diagnostics/starts.txt`, una riga a ogni avvio e una a ogni arresto — ora, pid,
   versione, vita, richieste servite, arresto ordinato o no — con un tetto di righe. È l'`avvii.txt` di vIPI. Oggi
   `startup.txt` si riscrive a ogni avvio, quindi anche quando l'hub partirà non sapremo quanto vive; e `hub_jobs_log` dice già
   quanto dura ogni giro.
2. **B, i job che recuperano**, nel nucleo, con le quattro correzioni del §3 (un esecutore, mail salvate una per una, riepilogo
   uno al giorno, i fusi espliciti — §7). Serve in ogni caso: qualunque cosa faccia l'host, un processo che si spegne c'è sempre.
3. ~~**A, un ping ogni 5 minuti** su `/health` da un Worker Cloudflare suo~~ — **scartato da Carmine** (§6, domanda 3): il
   Worker starebbe sul suo account personale, e niente della divisione può dipendere da un account personale.

E, a parte, **C chiesto comunque a Ivao.It** per `test.it.ivao.aero` e per la produzione, nello stesso messaggio della domanda
di vIPI sulle morti a hh:56: non costa niente.

## 5. Che cosa si può misurare adesso, e che cosa no

L'installazione di prova risponde dal 28 set mattina (`0.2.1`, `fa089de`; il `500` delle 06:50 UTC era l'utente del
database d'esempio rimasto nel file dei segreti).

**Misurato da fuori, 28 set 07:00–07:29 UTC**: una `GET /api/version` dopo pause crescenti, e il tempo fino al primo byte.
Un tempo intorno a 0,2 s vuol dire processo vivo; intorno a 8–11 s vuol dire che Passenger ha dovuto avviarlo.

| Pausa prima della richiesta | Tempo al primo byte |
|---|---|
| 5 s, 10 s, 5 s | 0,21 · 0,19 · 0,29 s |
| 20 s | **11,0 s** |
| 30 s | 0,34 s, poi **7,8 s** |
| 45 s | 0,20 s |
| 60 s, 90 s, 120 s, 180 s, 300 s, 600 s, 120 s | **7,8 · 10,0 · 8,2 · 8,4 · 9,4 · 9,0 · 7,8 s** |

- **Oltre un minuto di silenzio, ogni richiesta paga un avvio a freddo di 8–10 s**, sempre (7 su 7). Fra 20 e 45 s a volte
  sì e a volte no: la finestra d'inattività sta fra i 10 e i 30 secondi, come quella misurata su vIPI (§2).
- ⚠️ **Il traffico non era solo il mio**: in quella mezz'ora Carmine e il master usavano la stessa installazione, quindi un
  tempo breve dopo una pausa lunga (i 0,34 s dopo 30 s, i 0,20 s dopo 45 s) può essere un processo svegliato da loro. Un
  tempo lungo, invece, è per forza un avvio.
- **Che cosa ne segue per la decisione**: ogni chiamata dell'operazione pianificata sveglierà un hub spento e pagherà un
  avvio intero prima di fare il lavoro. Il lavoro va fatto **dentro** la richiesta, perché Passenger non spegne un processo
  mentre risponde, e l'operazione pianificata deve aspettare una risposta che arriva dopo l'avvio più il lavoro.

**Non misurabile da fuori**: quante vite l'ora e quanto lunghe, quanta memoria usa il processo, quanto dura l'avvio visto
da dentro, quanto dura ogni giro. Lo diranno `diagnostics/starts.txt` (passo 1) e `hub_jobs_log`, in particolare per
`RefDataSyncJob`, il giro più lungo. Una misura sulla prova **non** dice il traffico di giorno della produzione.

## 6. Le domande a Carmine

**Risposta di Carmine, 28 set 2026: sì alle domande 1, 2 e 4, come raccomandato**
(<https://github.com/SkyMistery/Ivao-Italy-Hub/pull/165#issuecomment-5865067623>). La domanda a Ivao.It (4) la pone lui.

**La domanda 3: no, sostituita** da una seconda risposta
(<https://github.com/SkyMistery/Ivao-Italy-Hub/pull/165#issuecomment-5865413362>). Niente Worker Cloudflare: starebbe sul
suo account personale, e niente della divisione può dipendere da un account personale. **L'orologio è un'operazione
pianificata di Plesk della sottoscrizione della divisione** («Recupera un URL»). Chiama l'hub a ore fisse (per esempio ai
minuti :05 e :35, per i METAR) con un **POST a un indirizzo dell'hub protetto da un token** (per esempio `/api/jobs/run`).
L'hub si sveglia, esegue quello che è dovuto (domanda 1) e risponde, e Passenger lo spegne di nuovo. È la strada **E** del
§3, ed è quella che ha suggerito chi amministra il server («un'API leggera che fa solo questi compiti, chiamata a orari
definiti, così il sito è libero di scaricarsi»). Niente seconda applicazione: su questo hosting Passenger avvierebbe e
spegnerebbe anche quella. Se il pannello lo permette, Carmine lo chiede all'amministratore.
Nella stessa risposta ha chiesto di scrivere nella nota la regola dei dati che esistono solo «adesso» (§8) e due misure in
più in `diagnostics/starts.txt` (§8).

1. **B, i job che recuperano, nel nucleo?** Raccomandato: **sì**, con le quattro correzioni del §3, nella sua PR.
2. **Prima `diagnostics/starts.txt`?** Raccomandato: **sì**, piccolo e subito, nella PR di B o prima.
3. **Il ping?** Raccomandato: sì, un Worker Cloudflare suo, ogni 5 minuti, su `/health`. **Risposta: no**, un'operazione
   pianificata di Plesk (sopra, e §8).
4. **Scrivi a Ivao.It** per `passenger_min_instances` o l'idle time di `test.it.ivao.aero` e della produzione? Raccomandato:
   **sì**, con la domanda di vIPI sulle morti a hh:56.

## 7. Trovato per strada

Dieci trigger su tredici non dicono il fuso (gli otto di Flight Ops, `FirSyncJob` e `NotificationDispatchJob`), quindi
girano nell'ora del server; i commenti di quattro (`PirepWithdrawalJob`, `TrackRetentionJob`, `WeatherRetentionJob`,
`ReviewDigestJob`) dicono «UTC». Vero solo se il server è in UTC. Nella PR di B diventano espliciti: UTC dove il commento
dice UTC, il fuso della divisione dove conta l'ora locale di chi legge (il riepilogo delle 07:00). Nessuna domanda: è una
correzione.

## 8. Che cosa è deciso, in una pagina

**La regola dei dati che esistono solo «adesso»** (Carmine, seconda risposta):

- quello che va **campionato ogni minuto non passa mai dall'hub**, e da nessuna applicazione avviata da Passenger;
- quello che va **campionato a ore fisse passa dal POST pianificato**;
- **tutto il resto recupera** (domanda 1).

Oggi l'unico dato che si perde mentre l'hub dorme sono i **METAR di ripiego (IVAO, VATSIM) degli aeroporti che NOAA non
ha**. Verificato nel codice: NOAA tiene **30 giorni** di METAR e TAF (`src/IvaoHub.Core/Weather/NoaaWeatherClient.cs:65-100`,
`WeatherReport.cs:61`), e all'invio del PIREP l'hub se li riprende (`WeatherArchive.cs:180-218`). IVAO e VATSIM, invece,
danno solo il bollettino corrente (`src/IvaoHub.Core/Weather/WeatherSource.cs:7-13`), e il TAF non ha ripiego. Dalla
lettura del codice fatta dal revisore (il master), e **non verificato in questa nota**: le tracce le chiede dopo il volo
il tracker di IVAO, e le sessioni ATC vengono dall'archiviatore di vIPI attraverso le viste `v_share_`, fuori dall'hub. Per
questo `WeatherJob` è il job che il POST pianificato deve chiamare alle sue ore (:05 e :35).

**`diagnostics/starts.txt`** (domanda 2) scrive per ogni avvio anche **la memoria usata dal processo** e **quanto è durato
l'avvio**. Così una settimana sull'installazione di prova dice quanto costa ogni risveglio. Se quei numeri lo chiedono, resta
possibile più avanti un'applicazione separata per i job, con una nota sua.

**Il POST pianificato**, da decidere nella PR del codice con la sua nota:

- **Uno solo**, che esegue quello che è dovuto e risponde; mai un indirizzo per job.
- **Il lavoro si fa dentro la richiesta**: il processo non è inattivo finché risponde (§5).
- **Il token è un segreto dell'installazione** (`secrets/`), non di una persona: non è un token personale (`CLAUDE.md` §2),
  perché chi chiama è l'hosting, non un utente.
- ⚠️ **Il pannello, non verificato**. Ci sono due strade. Carmine chiede all'amministratore quale è concessa.
  - «Recupera un URL» è, a quanto ne sa il master, una semplice GET senza intestazioni.
  - «Esegui un comando» con `curl -X POST -H "…"` fa il POST con l'intestazione, ma solo se il pannello concede i comandi alla
    sottoscrizione.
- **Il codice non dipende dalla strada**. Il token si accetta in un'intestazione, e la strada consigliata è quella. Accettarlo
  anche nell'indirizzo (una GET con `?token=…`) ha un costo: il token finisce nei log di nginx, di Cloudflare e dei proxy,
  quindi va trattato come un segreto a vista. Serve allora un token solo per questo indirizzo, che non apre nient'altro, si
  cambia senza toccare il resto e non fa niente oltre a far girare quello che è già dovuto. Un chiamante che lo ruba può solo
  far lavorare l'hub prima del tempo. Se sia accettabile si decide nella PR del codice, secondo la risposta dell'amministratore.
- **Le mail partono al ritmo delle chiamate**: con due chiamate l'ora, fino a mezz'ora di ritardo quando nessuno usa il sito.
  Le ore dell'operazione pianificata le decide chi la configura, non il codice.

## Da portare nel piano

- **§11.3 punto 9**, secondo trattino: che cosa si rompe (§2), la misura del §5, la strada decisa (§8), e che cosa resta
  all'host (l'operazione pianificata, la domanda sull'inattività).
- **§5.2**, la riga «Job»: «Quartz.NET in-process (Plesk = un processo)» non è più vero. Passenger spegne e avvia processi,
  a volte due insieme; un job è dovuto secondo il suo registro, non secondo l'ora in cui il processo è vivo; c'è un solo
  esecutore per job; l'orologio fuori dall'hub è il POST pianificato; vale la regola dei dati «adesso» (§8).
- **§11.3 punto 2**: `diagnostics/starts.txt` accanto a `startup.txt`, con memoria e durata dell'avvio.
- **§11.3 punto 6 o un punto nuovo**: l'operazione pianificata di Plesk fa parte dell'installazione, come `tmp/restart.txt`.
- **`docs/DEPLOYING.md`** «Known limits»: la riga su Passenger, quando B è nel codice (non prima: oggi quella riga è vera).
