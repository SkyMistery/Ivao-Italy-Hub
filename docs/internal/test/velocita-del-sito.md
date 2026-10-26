# La velocità del sito di prova

> Perché `test.it.ivao.aero` è lento, e dove: un'analisi dei file di diagnostica dell'installazione, del codice e degli header
> che il sito manda. Nessun codice cambia con questo documento; ogni cambio proposto è una issue (#397–#402). Analisi del
> 26 ottobre 2026, fase di test (`decisions/2026-10-18-la-fase-di-test.md`: la velocità è del master e delle sessioni di Claude).
> La produzione sarà un'installazione nuova su `it.ivao.aero`, con lo stesso hosting: **quello che si legge qui vale anche lì**,
> finché l'hosting non cambia.

## 0. In breve

1. **L'hub non resta acceso.** Passenger lo ferma **30 s dopo l'avvio in mediana**, e quasi mai oltre 60 s (408 processi su 432),
   **anche mentre qualcuno lo sta usando**: il silenzio più lungo che un processo è sopravvissuto è 42 s, e in mediana viene fermato
   8 s dopo l'ultima risposta. Nei giorni di prova l'hub si è avviato **90 volte al giorno** (una ogni 14 richieste).
2. **Ogni risveglio costa 8 s a chi lo provoca** (prima risposta: mediana 8,0 s, p90 13,5 s, in 0.10.5), e **le nove richieste dopo
   costano dieci volte quelle di un processo caldo** (mediana 135 ms contro 13 ms): il processo compila il suo codice mentre risponde.
   Le richieste fredde o iniziali sono **il 48% di tutte** e **dal 73 al 91% del tempo** che l'hub passa a rispondere, secondo la versione.
3. **Il database non è il problema**: i comandi sono il 6% del tempo delle richieste, le aperture delle connessioni il 7%. Le chiamate
   esterne il 5%, quasi tutte un solo blocco (`networkStats`).
4. **Il guadagno grande non è nel codice**: è far restare acceso il processo, una domanda per chi amministra il server (§4, H1). Dal
   codice si toglie forse un terzo di ogni risveglio (#398, #401) e qualche spreco che i processi brevi rendono caro (#397, #399, #400).

## 1. I dati

| File | Periodo | Che cosa dice |
|---|---|---|
| `diagnostics/starts.txt` | 28 set – 26 ott, 586 avvii, 584 arresti ordinati | ogni avvio (versione, `ready in`, i passi, la compilazione, il database) e ogni arresto (vita, richieste, `first answer at`, memoria) |
| `diagnostics/requests-AAAAMMGG.txt` e riassunti | 18 – 26 ott (dalla 0.9.2), 5 732 richieste, 400 processi | ogni richiesta: rotta (lo schema, mai l'indirizzo), tempi, database, connessioni, chiamate esterne, compilazione |
| `diagnostics/hub-AAAAMMGG.log` | 28 set – 26 ott | errori, avvisi, i giri dei job, e il tempo di ogni richiesta con il suo percorso |
| `diagnostics/startup.txt` | l'ultimo avvio | 0.10.5, .NET 10.0.13, `Production`, moduli events, flightops, training |

Le versioni sul server: 0.2.4–0.5.0 (28 set – 2 ott), 0.6.0 (2–16 ott), 0.7.0–0.9.0 (16–18 ott), **0.9.2** (18–19 ott), **0.10.0**
(19–21 ott), **0.10.5** (dal 22 ott). Le cifre «per versione» sono di queste; dove una tabella non dice la versione, è tutto il periodo
dei file delle richieste (0.9.2–0.10.5). Le cifre sono mediana (p50) e percentili per rango; «p90 13,5 s» vuol dire che una richiesta
su dieci ha aspettato di più.

Il repository è pubblico: qui ci sono **solo cifre aggregate**, rotte con `{id}` e conteggi. Nessuna riga dei log, nessun VID, nessun
indirizzo.

## 2. Dove va il tempo

### 2.1 Gli avvii: quanti, quanto vivono

| Versione | Avvii | `ready in` p50 / p90 | `first answer at` p50 / p90 | Vita p50 | Richieste per processo p50 / p90 |
|---|---:|---:|---:|---:|---:|
| 0.2.4 | 9 | 4,2 / 8,2 s | 5,4 / 12,0 s | 12 s | 12 / 102 |
| 0.5.0 | 41 | 4,9 / 6,6 s | 6,3 / 8,2 s | 36 s | 6 / 26 |
| 0.6.0 | 34 | 5,3 / 8,0 s | 7,0 / 9,6 s | 24 s | 1 / 9 |
| 0.9.2 | 63 | 5,7 / 10,2 s | 7,7 / 13,2 s | 36 s | 4 / 16 |
| 0.10.0 | 152 | 6,7 / 12,5 s | 9,3 / 15,8 s | 30 s | 7 / 31 |
| **0.10.5** | **186** | **5,8 / 10,3 s** | **8,0 / 13,5 s** | **48 s** | **11 / 41** |

- **Quanto vive un processo** (dal 18 ottobre, 432 arresti): sotto il minuto **408**, fra 1 e 5 minuti 23, oltre un'ora **1** (di notte,
  2 richieste in tre ore). Distribuzione per 5 s: piatta fra 10 e 60 s (20–50 processi per fascia), poi quasi niente. In nessuna ora
  del giorno la vita mediana supera i 50 s.
- **Quanto silenzio prima dell'arresto** (dall'ultima risposta alla riga `STOP`, 398 processi): p10 0 s, **p50 8 s**, p90 33 s, mai
  oltre 60 s (tranne il processo notturno). Il silenzio più lungo **dentro** un processo che poi è andato avanti: p50 6 s, max 42 s.
- **Ogni arresto è un `SIGTERM` dall'esterno, in ordine** (`SIGNAL … SIGTERM from the system`). Solo 2 avvii su 586 trovano il
  processo prima morto male (`!!`): uno al caricamento della 0.10.5, uno di notte.
- **Mai due processi insieme** nei file delle richieste (0 sovrapposizioni su 400): il processo nuovo nasce dopo che il vecchio è fermo.
- **Avvii al giorno** nei giorni di prova: 115 (19 ott), 91 (20), 91 (22), 49 (23), 36 (24).

La differenza dalla nota dell'avvio a freddo (28 set: «Passenger spegne l'hub dopo 10–30 s di inattività») è che **non è solo
inattività**: anche un processo con traffico continuo non vede il secondo minuto. È lo stesso comportamento misurato su vIPI con un
ping ogni 10 s (vita mediana 51 s, `decisions/2026-09-28-i-job-quando-passenger-spegne-l-hub.md` §2): **il ping riaccende, non tiene su**.

### 2.2 Dentro un avvio

I passi di `starts.txt`, mediana degli avvii che hanno saltato l'inizializzazione (il marcatore: è il caso normale, un risveglio).

| Passo (ms) | 0.2.4 | 0.6.0 | 0.9.2 | 0.10.5 p50 | 0.10.5 p90 | di cui compilazione (p50) |
|---|---:|---:|---:|---:|---:|---:|
| runtime (fino a `Main`) | 593 | 1 024 | 1 043 | **1 199** | 2 365 | 10 |
| services | 422 | 1 126 | 1 096 | 954 | 1 880 | 126 |
| endpoints | 171 | 288 | 308 | 316 | 758 | 80 |
| **models** | 1 429 | 1 699 | 1 980 | **2 048** | **4 382** | 562 |
| marker (la prima query, con la prima connessione) | — | 699 | 692 | 657 | 1 436 | 215 |
| superadmins | 275 | 107 | 130 | 141 | 252 | 79 |
| listening (Kestrel e Quartz) | 180 | 226 | 211 | 209 | 443 | 67 |
| **pronto** | 4 180 | 5 440 | 5 720 | **5 830** | 10 260 | |

- **Il passo più grande è la costruzione dei modelli EF, il 35% dell'avvio**, ed è cresciuto con i moduli (1,4 → 2,0 s): si
  costruiscono **tutti** i modelli (nucleo, Tour, Training, Eventi) a ogni avvio, per controlli che dipendono solo dal pacchetto (§3.2).
- **`runtime` 1,2 s** sul server, contro 25–31 ms sulla macchina di sviluppo con una CPU (nota dell'avvio a freddo, §3): è il caricamento
  del processo e del runtime, prima del codice dell'hub. Che sia CPU limitata dall'hosting o disco lento, dai file non si sa (§5).
- **La compilazione all'avvio** è 1,2 s su 5,8 (mediana dei `jit … ms in … methods`); il database prima di essere pronto 5 aperture,
  368 ms in mediana (p90 774 ms), e 5 comandi.
- **Dopo «pronto»** la richiesta che ha svegliato l'hub arriva al codice dell'hub a **7,7 s** (`up_ms`, p50; p90 13,4 s): ~1,8 s fra la
  porta aperta e la prima riga della pipeline, dove ASP.NET costruisce la tabella delle rotte e Passenger passa la richiesta.
- **Memoria**: picco p50 244 MB, p90 271 MB, max 789 MB. Nessun segno che il processo venga fermato per la memoria.

### 2.3 Le richieste: fredde, iniziali, calde

`n` è il numero della richiesta nel suo processo: fredda `n = 1`, iniziale `n` da 2 a 10, calda oltre.

| Versione | Classe | n | p50 | p90 | p95 | p99 | max | quota del tempo dell'hub |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| 0.9.2 | fredde | 63 | 306 | 686 | 728 | 6 193 | 6 193 | 23% |
| | iniziali | 279 | 221 | 510 | 641 | 1 977 | 12 753 | **69%** |
| | calde | 170 | 22 | 132 | 222 | 935 | 1 306 | 9% |
| 0.10.0 | fredde | 152 | 425 | 1 031 | 1 367 | 2 077 | 2 330 | 18% |
| | iniziali | 835 | 226 | 864 | 1 229 | 1 998 | 3 037 | **67%** |
| | calde | 875 | 33 | 175 | 334 | 916 | 1 532 | 15% |
| **0.10.5** | fredde | 185 | 299 | 950 | 1 137 | 1 738 | 5 919 | 16% |
| | iniziali | 1 243 | 135 | 577 | 876 | 1 444 | 2 716 | **57%** |
| | calde | 1 930 | **13** | 174 | 450 | 701 | 3 100 | 27% |

Tempi in ms, dalla prima riga della pipeline all'ultima. **L'attesa vera di chi sveglia l'hub** è `up_ms + total_ms` della fredda:
**p50 8,0 s, p90 13,6 s** in 0.10.5 (7,9 / 13,2 s in 0.9.2, 9,5 / 16,5 s in 0.10.0).

- **Il 48% delle richieste è fredda o iniziale.** In una giornata di prova le calde sono il 53–65%; in una giornata con poche visite il
  30% (24 e 25 ottobre).
- Una richiesta iniziale costa **dieci volte** una calda, sulla stessa rotta. Esempi (calda p50 → tutte p50): `/api/me` 27 → 172 ms,
  `/api/blocks/data/{type}` 44 → 125 ms, `/api/content/public/{kind}/{slug}` 23 → 105 ms, `/api/media/` 17 → 42 ms.

### 2.4 Per rotta

Le rotte che costano di più in totale (0.9.2–0.10.5). Tempi in ms.

| Rotta | n | p50 | p95 | max | calde p50 / p95 | comandi db p50 | tempo totale |
|---|---:|---:|---:|---:|---:|---:|---:|
| `GET /api/blocks/data/{type}` | 1 253 | 125 | 823 | 12 753 | 44 / 586 | 2 | 338 s |
| `GET /api/me` | 345 | 172 | 680 | 2 620 | 27 / 329 | 6 | 79 s |
| `GET /api/media/` | 437 | 42 | 719 | 2 077 | 17 / 172 | 4 | 75 s |
| `GET /api/content/public/{kind}/{slug}` | 347 | 105 | 658 | 2 330 | 23 / 137 | 5 | 71 s |
| `GET /api/content/{id}/publish-problems` | 430 | 22 | 700 | 3 037 | 12 / 233 | 1 | 61 s |
| `PUT /api/content/{id}` | 102 | 484 | 1 284 | 2 282 | 84 / 540 | 9 | 57 s |
| `GET /{*path:nonfile}` (le pagine della SPA) | 181 | 23 | 1 164 | 2 680 | 9 / 934 | 0 | 43 s |
| `GET /api/content/` | 260 | 50 | 564 | 1 568 | 35 / 122 | 2 | 37 s |
| `GET /api/content/address` | 366 | 17 | 515 | 1 807 | 9 / 136 | 2 | 36 s |
| `GET /api/content/{id}` | 369 | 14 | 477 | 918 | 8 / 95 | 1 | 30 s |
| `GET /api/content/pages` | 283 | 26 | 396 | 976 | 9 / 92 | 1 | 29 s |

**I blocchi dati per tipo** (dal log dell'hub, che scrive il percorso; 18–26 ott):

| Blocco | n | p50 | p95 | max | tempo totale |
|---|---:|---:|---:|---:|---:|
| **`networkStats`** | 323 | **429** | 1 198 | **12 738** | **163 s** |
| `myWork` | 217 | 79 | 521 | 779 | 33 s |
| `linkList` | 215 | 65 | 544 | 1 068 | 32 s |
| `newsList` | 176 | 96 | 613 | 1 359 | 31 s |
| `calendar` | 146 | 75 | 573 | 1 254 | 24 s |
| `stats` | 80 | 38 | 536 | 619 | 11 s |
| i blocchi dei moduli (16 tipi) | 10–19 ciascuno | 250–514 | 600–1 570 | | 2–8 s ciascuno |

`networkStats` da solo è **metà del tempo di tutti i blocchi** (§3.4).

**Le scritture con più comandi**: `POST /api/training/trainings/{id}/assign` 26, `PUT /api/flightops/tours/{id}` 20–26 (p50 660 ms,
db 169 ms), `POST /api/training/trainings/{id}/slots` 24, `POST /api/training/mine/{id}/choose` 20. Sono poche (1–5 richieste
ciascuna) e i comandi sono quelli dell'interceptor (audit, proiezioni) più le letture: da tenere d'occhio, non la causa della lentezza.

**Rare ma lente**: `GET /openapi/{documentName}.json` (4 richieste, p50 2,9 s) e il ritorno dell'accesso, `GET /auth/callback`
(nel file `(no endpoint) /auth`, 4 richieste, p50 2,7 s, db p95 618 ms): la seconda è l'accesso a IVAO, che crea o aggiorna l'utente; non misurata più a fondo.

### 2.5 Per ora (UTC)

Le richieste si concentrano 06–12 e 17–22 UTC (8–14 e 19–24 in Italia). Il p95 sale la sera (17–20 UTC: 1,0–1,3 s) rispetto al
mattino (0,5–0,8 s) con la stessa quota di richieste fredde: **forse un server più carico la sera** (altri siti dello stesso hosting),
ma con poche centinaia di richieste per fascia non lo affermo. Di notte le richieste sono una o due l'ora, quasi tutte fredde.

### 2.6 Database, connessioni, chiamate esterne

Del tempo totale passato nelle richieste (1 115 s):

| Dove | Quota | |
|---|---:|---|
| comandi al database (letture comprese) | **6,3%** | calde: 0 ms in mediana, p95 9–150 ms secondo la rotta |
| aperture di connessione | **7,3%** | p50 2 ms a processo caldo, 9–13 ms nella prima richiesta (pool vuoto) |
| chiamate esterne (IVAO, meteo) | **4,8%** | quasi tutte `networkStats` (202 richieste) e la creazione di una tratta di tour (4) |
| compilazione del runtime (contatore di processo, tagliato al tempo della richiesta) | **83%** | sovrastima: con richieste in parallelo ognuna conta anche quella delle altre (§5) |

- **Il pool di 15 connessioni non si vede**: nessun errore di connessioni esaurite; una richiesta ne apre al più 21 in sequenza (dal
  pool), e in parallelo le richieste sono 5–10.
- **Picchi isolati del database**: `GET /api/content/{id}/publish-problems` a processo caldo ha 1–3 comandi e di solito 0 ms, ma 17
  volte su 286 oltre 100 ms (max 895 ms); `PUT /api/content/{id}` 4 su 42 oltre 100 ms (max 692 ms). Con così pochi comandi è più
  probabile il MariaDB condiviso con altri siti che un indice mancante; non si distingue dai file.
- **Chiamate esterne**: 1 avviso «IVAO could not be reached» in quattro settimane, nessun circuito aperto, nessun tentativo ripetuto
  visibile nei tempi. Il tetto di 20 s per tentativo e 30 s in tutto (#325) non è mai stato toccato in queste richieste: il massimo di una
  chiamata è 2,6 s.

### 2.7 I job

- **Il giro di recupero gira in quasi ogni processo**: dal 19 ottobre una riga «Ran N job(s) that were due» dopo **348 avvii su 395**,
  sempre con `flightops-flight-checks` (che non lascia una riga quando non trova niente, quindi ogni processo nuovo lo trova dovuto). Il
  giro parte 5 s dopo «Application started» e finisce a 5,3 s con un job (p90 5,5 s), a 5,6 s con più job (p90 6,4 s, max 37 s): cioè
  **mentre arriva la prima pagina** (§3.5, #399).
- **L'operazione pianificata di Plesk** (`POST /api/jobs/run`) ha **una sola** richiesta in otto giorni di file: sul sito di prova non è
  impostata, o non arriva. Non è una questione di velocità (i job recuperano comunque alla visita dopo), ma va saputo.

### 2.8 Errori e richieste interrotte

- **46 richieste 499** (chi aspettava ha chiuso o è andato altrove): 10 su `/api/me`, 9 sui blocchi dati, p50 328 ms. Solo 4 sono a meno
  di 3 s da un arresto del processo: **non è Passenger che le taglia**, è la navigazione nella SPA (o l'impazienza).
- Le 62 righe «responded 500» del log vengono quasi tutte da una richiesta interrotta: nel log ci sono 55
  **`OperationCanceledException`** (la stessa cosa che il file delle richieste scrive come 499), poi 28 errori di nonce dell'accesso
  (28–30 settembre e 19 ottobre) e 4 conflitti di versione sul salvataggio. **Nessun errore costa tempo** agli altri.

### 2.9 I file statici e Cloudflare

Misurato da fuori il 26 ottobre (0.10.5), con `curl -I`:

| File | `Cache-Control` | Cloudflare |
|---|---|---|
| `/` (`index.html`, dal server web) | nessuno | `DYNAMIC` (non in cache) |
| `/assets/index-….js` e gli altri `assets/` (nomi con l'impronta) | **nessuno** | `REVALIDATED` |
| `/locales/it/common.json` | nessuno | `DYNAMIC` |
| `/branding/favicon.svg` | nessuno | `REVALIDATED` |

- **Nessun file statico ha `Cache-Control`.** Cloudflare li tiene, ma a ogni visita li **riconvalida** con il server; il browser fa da sé
  (cache euristica da `Last-Modified`). Per i file di `assets/`, che hanno l'impronta nel nome e non cambiano mai, è lavoro perso.
- **Quello che ogni pagina carica prima di disegnare** (`index.html` ne chiede 5): JS **2,09 + 0,19 + 0,49 + 0,12 MB** e CSS 0,23 MB,
  **~3,1 MB da leggere e compilare nel browser, ~760 KB compressi** (brotli). Ognuno 0,2–0,3 s da qui. Il file principale contiene
  **maplibre-gl**, la libreria della mappa dei tour: almeno ~0,55 MB dei 2,09 (§3.6, #400).
- `/api/*` risponde `no-store` per regola (`UseNoStoreForApi`): Cloudflare non risponde mai per l'hub, nemmeno per una risposta
  anonima uguale per tutti.

## 3. Perché, dal codice

### 3.1 Passenger ferma l'hub, e non è nel codice

Niente nel codice ferma il processo: l'arresto è un `SIGTERM` dall'esterno, ordinato, che arriva anche con richieste appena servite
(44 arresti su 398 meno di un secondo dopo l'ultima risposta). Le due ipotesi, che dai file non si separano:

- **un tempo d'inattività molto corto** (`passenger_pool_idle_time` o simile) più un controllo periodico che ferma anche chi ha appena
  risposto;
- **un pool di processi condiviso fra i siti del server** (`passenger_max_pool_size`): quando un altro sito chiede un processo,
  Passenger ferma quello usato meno di recente. Spiegherebbe perché **di notte** un processo è vissuto tre ore.

In più, `runtime` di 1,2 s (contro 0,03 s in sviluppo) e un server ~2,7 volte più lento della macchina di sviluppo con una CPU (nota
dell'avvio a freddo, §5) fanno pensare a una **CPU limitata per account** (per esempio CloudLinux LVE, comune sotto Plesk). È una
domanda per chi amministra il server (§4, H1).

### 3.2 L'avvio: i modelli di tutti i moduli, sempre (#398)

`InitializeAsync` (`src/IvaoHub.Web/HubPipeline.cs:193-220`) costruisce il modello EF di **ogni** contesto abilitato a ogni avvio, per
quattro controlli (`VerifyAlternatives`, `VerifyWithdrawals`, `VerifyErasedWith`, le aree con un FIR) il cui esito dipende solo dal
pacchetto e dalla configurazione: gli stessi ingressi della chiave del marcatore. I modelli compilati sono esclusi (nota dell'avvio a
freddo §4.2, i filtri globali); costruire al risveglio solo il modello del nucleo, e lasciare quello di un modulo alla prima richiesta
che lo usa, toglierebbe **~1 s stimato** dei 2,0 s del passo. Da misurare.

### 3.3 La compilazione delle prime richieste (#401)

ReadyToRun (dalla 0.2.4) precompila il codice, ma non quello che si genera mentre gira: i delegati degli endpoint minimal API (la
tabella delle rotte, dentro la prima richiesta), la compilazione delle query EF alla prima esecuzione di ognuna, i serializzatori JSON
per riflessione dei DTO (sei `JsonSerializerContext` in tutto `src/`), le prime istanze dei generici. Un processo compila **6,4 s di
codice nella sua vita** in mediana (p90 10,7 s), e vive 30 s. Il Request Delegate Generator fu misurato e messo da parte il 28 settembre
per 0,15 s su una CPU: sul server il passo `endpoints` vale 0,3–0,8 s e la tabella delle rotte è dentro la prima richiesta. Il guadagno è
incerto e va misurato sul banco prima di scegliere; conta molto **solo se l'hub continua a spegnersi ogni minuto**.

### 3.4 `networkStats`: una cache più corta di un processo (#397)

`IvaoApiClient.GetNetworkStatusAsync` (`src/IvaoHub.Core/Ivao/IvaoApiClient.cs:384-405`) tiene la fotografia della rete **in memoria per
60 s** (`IvaoWhazzup.Freshness`). Un processo vive meno di 60 s, quindi la cache quasi non serve: **183 processi su 400** hanno scaricato
di nuovo la fotografia (`/v2/tracker/whazzup`, alcuni MB), e la richiesta che lo fa costa p50 **536 ms**, p90 1,1 s, ~9 MB di memoria.
E la striscia in cima a ogni pagina pubblica (`LiveStatusStrip`, `web/src/routes/_public.tsx:24`) richiede ogni 60 s: con processi che
vivono meno, **una scheda pubblica aperta risveglia l'hub ogni minuto** (72 avvii svegliati da un blocco dati; 22 processi hanno
risposto a **un solo** blocco in tutta la vita). Le strade nella issue: tenere la fotografia ridotta oltre il processo, una sola
lettura alla volta, e lasciare che Cloudflare risponda per un minuto alla domanda anonima (che cambia la regola `no-store`, quindi una
nota di decisione).

### 3.5 Il giro dei job sulla prima pagina (#399)

`JobCatchUp.FirstCheck` (`src/IvaoHub.Core/Jobs/JobCatchUp.cs:28`) è 5 s: cade quando la pagina che ha svegliato l'hub manda le sue
richieste iniziali, sulla stessa CPU. E `FlightCheckJob` (`src/IvaoHub.Modules.FlightOps/Checks/FlightCheckJob.cs:17`) non lascia riga
quando non trova niente, quindi ogni processo nuovo lo esegue (la nota dei job che recuperano lo dice: «una volta al suo primo
controllo»; con processi da 30 s è ogni 30 s). Guadagno piccolo, fatica piccola.

### 3.6 La SPA: la mappa in ogni pagina (#400)

`RouteMap` è esportato dal barile `web/src/shared/ui/index.ts:33-40`, che il layout pubblico importa (`_public.tsx:4`, per
`LiveStatusStrip`). `RouteMap.tsx` importa `maplibre-gl`, il suo CSS e il suo worker in cima al file: un modulo con effetti collaterali
(l'import del CSS) che Rollup tiene anche se nessuno lo usa, e che `autoCodeSplitting` non può spostare. La mappa la disegna una sola
pagina, quella pubblica di un tour.

### 3.7 Il database, per escludere

Nessun N+1 visibile nelle letture: le rotte più chieste fanno 1–6 comandi. Le aperture di connessione costano più dei comandi (7,3% contro
6,3%): ogni comando di EF apre e chiude la connessione del pool, e con MySqlConnector una connessione ripresa dal pool fa un giro di
reset. Vale qualche millisecondo per richiesta: non è da toccare prima del resto.

## 4. Che cosa fare, in ordine di guadagno e di fatica

| # | Che cosa | Chi | Guadagno | Fatica |
|---|---|---|---|---|
| **H1** | **Chiedere a chi amministra il server che cosa ferma l'hub** entro 60 s anche con traffico (tempo d'inattività, pool condiviso, limiti per account), e se per questa applicazione si può avere **un processo sempre acceso** (`passenger_min_instances 1`) e un tempo d'inattività lungo. Per vIPI la risposta fu «non si può» (`lavori-aperti.md` di vIPI): va chiesto di nuovo per la produzione dell'hub, con questi numeri | Carmine, a Ivao.It | **il grosso**: via gli ~8 s di ogni risveglio (90 al giorno nei giorni di prova) e il ×10 delle richieste iniziali | una domanda |
| **H2** | **`Cache-Control` per i file con l'impronta**: una regola di Cloudflare (Cache Rules, percorso `/assets/*`: Edge TTL e Browser TTL lunghi), oppure nel pannello una direttiva del server web per `assets/` (`Cache-Control: public, max-age=31536000, immutable`). `locales/` ha già `?v=<impronta>` (0.8.1) e può avere lo stesso; `index.html` resta senza cache | Carmine (Cloudflare o Plesk) | 5+ riconvalide in meno per ogni visita (0,2–0,3 s ciascuna, in parallelo), di più sul telefono | una regola |
| C1 | **#398** i modelli dei moduli solo quando servono | sessione, nucleo | ~1 s stimato per risveglio | media |
| C2 | **#397** `networkStats`: la fotografia oltre il processo, una lettura alla volta, Cloudflare per la domanda anonima | sessione, nucleo (+ nota per `no-store`) | ~0,5 s sulla prima pagina pubblica di un processo; meno risvegli dalle schede aperte | piccola–media |
| C3 | **#400** `maplibre-gl` fuori dal file principale | sessione, sito | ~0,5 MB in meno da scaricare e compilare a ogni prima visita | piccola |
| C4 | **#401** misurare e tagliare la compilazione delle prime richieste (RDG, JSON generato, impostazioni del JIT) | sessione, nucleo | incerto; conta solo se H1 non riesce | media |
| C5 | **#399** il primo giro dei job dopo la prima pagina; `FlightCheckJob` non in ogni processo | sessione, nucleo + tour | piccolo | piccola |
| C6 | **#402** la diagnostica: il tipo di blocco, le richieste in corso, le richieste tagliate da un arresto | sessione, nucleo | nessuno da solo: rende esatta la prossima misura | piccola |
| H3 | **L'operazione pianificata** `POST /api/jobs/run` sul sito di prova (`docs/DEPLOYING.md`, «The scheduled jobs and the host's scheduled task»): una sola chiamata in otto giorni | Carmine (Plesk) | nessuno sulla velocità; i job notturni girano | piccola |

**Da non fare**: un ping per tenere sveglio l'hub (un Worker o un'operazione pianificata ogni pochi secondi). Su vIPI e qui il processo
si ferma comunque entro il minuto: il ping lo **riaccende** e basta, e ogni risveglio costa un avvio intero di CPU sullo stesso server
(5,8 s), che rallenta anche le visite vere.

**Se H1 riesce**, C1, C4 e C5 diventano quasi irrilevanti (pagano un avvio al giorno); restano utili C2 (la fotografia in un processo che
vive ore è già in cache, quindi diventa piccola), C3 e H2, che valgono per ogni visita.

## 5. Che cosa misurare ancora

- **Da fuori, il tempo che i file non vedono**: Cloudflare, nginx, Passenger che avvia il processo prima di `ready in`, il caricamento della
  SPA nel browser. Una sessione con gli strumenti del browser (scheda Rete, «Disable cache» spento e acceso) su tre pagine — la home,
  `/staff`, una pagina di un tour — a hub freddo e caldo, con i tempi di `index.html`, degli `assets/` e delle prime chiamate.
- **Le impostazioni di Passenger e i limiti dell'account** (§3.1): li sa solo chi amministra il server.
- **Nei file dell'hub** (#402): il tipo del blocco nella colonna della rotta (oggi `{type}`: i tipi sono un elenco chiuso, nessuno è una
  persona), le richieste in corso accanto a `jit_ms` (che oggi è di tutto il processo), le richieste tagliate da un arresto.
- **Sul banco, prima di #398 e #401**: il pacchetto pubblicato, una CPU, avvio e prime dieci richieste di una pagina, prima e dopo ogni
  cambio, come nella nota dell'avvio a freddo.

## 6. Che cosa non è verificato

- **Che cosa ferma il processo**: le due ipotesi del §3.1 sono ipotesi; nessuna impostazione dell'hosting è stata vista.
- **Il guadagno di #398 e #401**: stimato dal peso dei passi, non misurato.
- **Quanti dei 72 risvegli da un blocco siano la striscia di `networkStats`** e non un altro blocco: il file delle richieste scrive `{type}`;
  il log dice il tipo, ma non quale richiesta ha svegliato il processo.
- **La quota della compilazione** (83%) è una sovrastima, per il contatore di processo con richieste in parallelo; la direzione (le prime
  richieste costano dieci volte le calde, con lo stesso database) no.
- **Gli header dei file statici** sono stati letti un giorno solo, su quattro file, da un solo posto; le dimensioni dei file sono quelle
  della 0.10.5 pubblicata.
- **La sera più lenta** (§2.5): poche centinaia di richieste per fascia, non abbastanza per dirlo.
