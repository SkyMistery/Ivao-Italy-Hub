# I job pianificati quando Passenger spegne l'hub

**Data:** 28 settembre 2026
**Stato:** **Proposta.** Le domande sono al §6, poste a Carmine sulla PR; il codice arriva solo dopo la sua risposta, in PR
sue.
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

Tre passi, in quest'ordine:

1. **Prima misurare** (nucleo, piccolo): `diagnostics/starts.txt`, una riga a ogni avvio e una a ogni arresto — ora, pid,
   versione, vita, richieste servite, arresto ordinato o no — con un tetto di righe. È l'`avvii.txt` di vIPI. Oggi
   `startup.txt` si riscrive a ogni avvio, quindi anche quando l'hub partirà non sapremo quanto vive; e `hub_jobs_log` dice già
   quanto dura ogni giro.
2. **B, i job che recuperano**, nel nucleo, con le quattro correzioni del §3 (un esecutore, mail salvate una per una, riepilogo
   uno al giorno, i fusi espliciti — §7). Serve in ogni caso: qualunque cosa faccia l'host, un processo che si spegne c'è sempre.
3. **A, un ping ogni 5 minuti** su `/health` di prova e di produzione, da un Worker Cloudflare suo (`hub-keepalive`, non dentro
   `atc-archiver`, che serve i tour di vIPI). Con B basta un processo ogni tanto: 5 minuti sono il ritardo massimo di una mail, e
   12 avvii l'ora invece di 60. Il sorgente nel repository (`tools/keepalive-worker/`, l'indirizzo come variabile del Worker):
   la lezione di vIPI è che un Worker che vive solo sulla dashboard si perde alla prima ripubblicazione.

E, a parte, **C chiesto comunque a Ivao.It** per `test.it.ivao.aero` e per la produzione, nello stesso messaggio della domanda
di vIPI sulle morti a hh:56: non costa niente, e se lo concedono il ping diventa una riserva.

## 5. Che cosa si può misurare adesso, e che cosa no

- **Adesso no**: il 28 set, 06:50 UTC, `https://test.it.ivao.aero/health` risponde `500` dopo 16 s e `/api/version` `500`
  dopo 5 s, attraverso Cloudflare: l'applicazione non parte ancora (si aspetta il log di Passenger). Nessuna vita dell'hub,
  nessun giro di job è misurabile.
- **Quando parte**, con il passo 1: quante vite l'ora e quanto lunghe, senza traffico (l'installazione di prova è privata,
  quindi è la notte della produzione) e con un ping; quanto dura un avvio; quanto dura ogni giro in `hub_jobs_log`, in
  particolare `RefDataSyncJob`, il più lungo. Una misura sulla prova **non** dice il traffico di giorno della produzione.

## 6. Le domande a Carmine

1. **B, i job che recuperano, nel nucleo?** Raccomandato: **sì**, con le quattro correzioni del §3, nella sua PR.
2. **Prima `diagnostics/starts.txt`?** Raccomandato: **sì**, piccolo e subito, nella PR di B o prima.
3. **Il ping?** Raccomandato: **sì, un Worker Cloudflare suo, ogni 5 minuti, su `/health` di prova e di produzione, con il
   sorgente in `tools/keepalive-worker/`**. In alternativa: nessun ping, e le mail della notte partono al mattino.
4. **Scrivi a Ivao.It** per `passenger_min_instances` o l'idle time di `test.it.ivao.aero` e della produzione? Raccomandato:
   **sì**, con la domanda di vIPI sulle morti a hh:56.

## 7. Trovato per strada

Dieci trigger su tredici non dicono il fuso (gli otto di Flight Ops, `FirSyncJob` e `NotificationDispatchJob`), quindi
girano nell'ora del server; i commenti di quattro (`PirepWithdrawalJob`, `TrackRetentionJob`, `WeatherRetentionJob`,
`ReviewDigestJob`) dicono «UTC». Vero solo se il server è in UTC. Nella PR di B diventano espliciti: UTC dove il commento
dice UTC, il fuso della divisione dove conta l'ora locale di chi legge (il riepilogo delle 07:00). Nessuna domanda: è una
correzione.

## Da portare nel piano

- **§11.3 punto 9**, secondo trattino: che cosa si rompe (§2), la strada decisa, e che cosa resta all'host.
- **§5.2**, la riga «Job»: «Quartz.NET in-process (Plesk = un processo)» non è più vero — Passenger ne spegne e ne avvia, e a
  volte due insieme; un job è dovuto per il suo registro, non per l'ora in cui il processo è vivo; un solo esecutore per job.
- **§11.3 punto 2**: `diagnostics/starts.txt` accanto a `startup.txt`, se la domanda 2 è sì.
- **`docs/DEPLOYING.md`** «Known limits»: la riga su Passenger, quando B è nel codice (non prima: oggi quella riga è vera).
