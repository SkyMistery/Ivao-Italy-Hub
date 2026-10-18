# I tempi delle richieste: `diagnostics/requests-AAAAMMGG.txt`

**Data:** 18 ottobre 2026
**Stato:** **Proposta** per una sola domanda (§6, l'impostazione in produzione); il resto è la diagnostica chiesta da Carmine
al master il 18 ottobre («il sito di prova è molto, molto lento»), scritta da una sessione di lavoro. Versione **0.9.2**,
PATCH: nessuna migrazione, nessuna pagina.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: si estendono la diagnostica che c'è già (`starts.txt`, `StartupTimings`,
la cartella `diagnostics/`) e il modo in cui i contesti prendono un diagnostico (un `IInterceptor` registrato, che
`HubDbContextServiceCollectionExtensions` aggiunge a ogni contesto: è scritto lì per questo). Nessun meccanismo nuovo per
i moduli, che non vedono niente. È un cambiamento del nucleo, quindi una pull request sua (`CLAUDE.md` §0, regola 6).

## 1. Il problema

`test.it.ivao.aero` (0.9.0) è molto più lento di vIPI sullo stesso hosting. Dalla copia di `starts.txt` di Carmine (155
avvii) il master ha letto: Passenger uccide il processo dopo 8–53 s di silenzio (SIGTERM), quindi quasi ogni visita dopo una
pausa è un avvio a freddo; `first answer at` mediana 6,7 s, p90 9,9 s; `ready in` mediana 5,2 s, p90 8,0 s. vIPI si tiene
sveglio con un Worker di Cloudflare, che per l'hub è deciso solo quando il sito sarà pubblico.

`starts.txt` dice quanto dura l'avvio e i suoi passi, ma non:

- **dove va il tempo fra «pronto» e «prima risposta»** (~1,7 s sul server: 6,4 → 8,1 s il 18 ottobre);
- **quanto pesano le richieste calde**, rotta per rotta, e quanto di quel tempo è database o IVAO;
- dentro i passi dell'avvio, **quanto è compilazione** (ReadyToRun non copre tutto) e quanto è il primo collegamento al
  database (il passo `marker`, 0,6–1,7 s, è la prima query: contiene l'apertura della connessione).

## 2. Che cosa scrive l'hub

### `diagnostics/requests-AAAAMMGG.txt`, una riga per richiesta

Un file per giorno UTC, separato da tabulazioni, con un'intestazione `#` che spiega le colonne:

```
time  pid  n  up_ms  method  status  route  total_ms  ttfb_ms  db_n  db_ms  open_n  open_ms  http_n  http_ms  jit_ms  jit_n  alloc_kb
2026-10-18 09:28:00.473Z  2491177  2  6612  GET  200  /api/me  217  210  3  45  1  30  0  0  120  800  2048
```

- **`pid`, `n`**: il processo e il numero della richiesta in esso. `n = 1` è la richiesta che ha svegliato l'hub.
- **`up_ms`**: dalla creazione del processo a quando la richiesta arriva al codice dell'hub. Per `n = 1` è quasi tutta
  l'attesa del visitatore prima che l'hub cominci a rispondere (l'avvio), più quello che Passenger fa prima.
- **`route`**: lo **schema** della rotta che l'hub dichiara (`/api/content/public/{kind}/{slug}`, `{*path:nonfile}` per le
  pagine della SPA), **mai** l'indirizzo chiesto né la query. Una richiesta che non arriva a nessuna rotta è
  `(no endpoint) /favicon.ico`: la prima parola del percorso solo se è fatta di lettere, `.`, `-`, `_` (mai una cifra:
  potrebbe essere un numero di qualcuno), altrimenti `(no endpoint)` e basta.
- **`total_ms`, `ttfb_ms`**: dalla prima riga della pipeline alla fine della risposta, e al momento in cui partono gli header.
- **`db_n`/`db_ms`**: comandi al database e il loro tempo, lettura delle righe compresa (un reader si cronometra quando viene
  chiuso). **`open_n`/`open_ms`**: connessioni aperte e il loro tempo: dal pool è zero, una nuova è la rete; nel processo
  appena nato il pool è vuoto, ed è uno dei sospetti del secondo e mezzo dopo «pronto».
- **`http_n`/`http_ms`**: chiamate in uscita (IVAO, meteo, confini dei FIR) e il loro tempo, tentativi e attese di Polly
  compresi: il gestore è il più esterno di ogni client della factory (`ConfigureHttpClientDefaults`). Fuori resta solo il
  client OIDC dell'accesso, che non passa dalla factory.
- **`jit_ms`/`jit_n`**: codice compilato dal runtime mentre la richiesta girava, **in tutto il processo** (`JitInfo`): con
  una richiesta alla volta, com'è al freddo, è della richiesta. **`alloc_kb`**: lo stesso per la memoria allocata.

### `diagnostics/requests-AAAAMMGG-summary.txt`, il riassunto di un giorno

Per ogni giorno finito: quante richieste e quanti processi; mediana, p90, p99 e massimo del tempo totale per **tutte**, per le
**fredde** (`n = 1`), le **iniziali** (`n` da 2 a 10, compilano ancora) e le **calde**; quando è cominciata in mediana la
richiesta fredda (`up_ms`); poi le stesse cifre **per rotta**, le più chieste prima, con la mediana di database, connessioni,
HTTP e compilazione; e le **dieci più lente** com'erano nel file.

Nessun processo vede la mezzanotte (vivono meno di un minuto), quindi lo scrive il **primo processo del giorno dopo che vive
20 s** (`HousekeepingDelaySeconds`), in background, lontano dalla richiesta per cui è nato. Due processi vivi insieme ne
scrivono uno solo: il file si sposta al suo posto solo se non c'è già. Il giorno corrente non ha riassunto: chi analizza lo
calcola dalle righe.

### `starts.txt`, l'avvio scomposto

- Nella riga `START`, dopo la memoria: `jit 2400 ms in 9100 methods  db 2 opens 640 ms, 7 commands 95 ms`, cioè il codice
  compilato e il database toccato prima di essere pronto; e in ogni passo `models 1890 (jit 1200)`, la compilazione dentro
  il passo (solo se c'è). Così i 5–8 s si dividono fra compilazione, primo collegamento al database e il resto.
- Nella riga `STOP`, in fondo: `jit 4100 ms in all`, la compilazione di tutta la vita del processo.
- Le righe vecchie restano leggibili: le parti nuove sono in coda, la lettura per pid non cambia.

**Non misurato a parte** (resta nel tempo dei passi o della prima richiesta, senza nome): il caricamento delle chiavi di
Data Protection (avviene alla prima lettura del cookie), la costruzione della tabella delle rotte (dentro la prima
richiesta), il tempo che Passenger passa prima di dare la richiesta all'hub.

## 3. Che cosa non c'è, e perché

- **I file che il server web consegna da `wwwroot/`** (la home `index.html`, `assets/`, `locales/`, `branding/`) **non
  arrivano mai all'hub** (nota `2026-09-28-gli-header-dei-file-statici`, §1): non hanno righe. Il loro tempo si misura da
  fuori.
- **Il tempo prima del processo**: Cloudflare, nginx, Passenger che avvia il processo. `up_ms` di una richiesta fredda e
  `first answer at` di `starts.txt` sono le due misure dal lato dell'hub; il resto si misura da fuori, come nella nota
  `2026-09-28-l-avvio-a-freddo`.
- **Una persona.** Nessun VID, nessun indirizzo IP, nessun user agent, nessun cookie, nessun indirizzo chiesto, nessuna query,
  nessun testo SQL, nessun indirizzo delle chiamate in uscita. Il test d'integrazione manda un numero a forma di VID nel
  percorso e nella query, un user agent e un `X-Forwarded-For`, e controlla che il file non contenga niente di questo.

## 4. Quanto costa

- Per richiesta: un oggetto di contatori, una riga in una coda limitata (4 096, oltre si perde la riga), tre letture di
  contatori del runtime. Il file lo scrive **un solo task** in background, a blocchi; la richiesta non aspetta mai il disco.
- Per comando al database e per chiamata in uscita: due somme atomiche su contatori.
- Ogni errore (disco pieno, cartella non scrivibile) perde righe, **mai una richiesta**, e si legge su stderr.
- **Non l'ho misurato** in numeri: è la stessa classe di costo del contatore di `StartsWatch`, che c'è già. Se il master
  lo vuole misurato, si confronta `total_ms` mediano con il file acceso e spento sul banco.

## 5. Limiti e conservazione

| Impostazione (`Diagnostics:Requests:…`) | Predefinito | |
|---|---|---|
| `Enabled` | vuoto: **acceso nel processo dell'hub**, spento sotto un host di test | `true`/`false` decidono per ogni processo |
| `RetainedDays` | 14 | giorni tenuti, oggi compreso; i più vecchi (righe e riassunti) li cancella l'hub |
| `MaxMegabytesPerDay` | 20 | oltre, una riga `#` lo dice e il giorno smette di crescere (~150 000 richieste) |
| `HousekeepingDelaySeconds` | 20 | dopo quanto un processo scrive i riassunti dei giorni finiti |
| `Directory` | vuoto: `diagnostics/` | un'altra cartella; lo usa il test |

Il database e le chiamate in uscita si contano sempre (sono contatori), anche con `Enabled: false`: la riga `START` di
`starts.txt` li legge.

**Due processi insieme** (Passenger a volte ne tiene due) scrivono lo stesso file del giorno: ogni blocco è una sola
scrittura in coda al file, quindi le righe non si mescolano in pratica; un'intestazione doppia, se nascono insieme, è
innocua (righe `#`).

## 6. La domanda per Carmine

⚖️ **In produzione, il file delle richieste acceso o spento per impostazione?**

- **A. Acceso, come sul sito di prova** *(raccomandata)*: non nomina nessuno, costa due contatori per richiesta, si
  cancella da sé dopo 14 giorni e non supera i 20 MB al giorno; il giorno in cui la produzione è lenta i dati ci sono già.
  Spegnerlo è una riga nel file dei segreti (`"Diagnostics": { "Requests": { "Enabled": false } }`).
- B. Spento in produzione (`Enabled: false` nel pacchetto per `Production`, acceso a mano sul sito di prova): ma il sito di
  prova gira **anch'esso** come `Production`, quindi andrebbe acceso nel suo file dei segreti, cioè un'azione di Carmine via
  FTP prima che i dati comincino.

Il codice di questa PR è la A. Se Carmine sceglie B, la differenza è una riga di `appsettings.json` e una di questo foglio.

## 7. Che cosa non è verificato

- **Sul server**: niente. Le righe vere le dirà la 0.9.2 caricata su `test.it.ivao.aero`.
- Che `SIGTERM` lasci il tempo di scrivere le ultime righe in coda: l'arresto ordinato le scrive (aspetta al massimo 2 s), e
  `starts.txt` dice che Passenger ferma l'hub in ordine; un processo ucciso con `SIGKILL` perde le righe ancora in coda
  (al più quelle dell'ultimo istante).
- La scrittura in coda da **due processi** insieme sullo stesso file: ragionata, non provata.
- Il costo per richiesta in millisecondi (§4).

## Da portare nel piano

- **§11.3 punto 2** (diagnostica): `diagnostics/requests-AAAAMMGG.txt` e il suo riassunto giornaliero, com'è fatto (§2), che
  cosa non contiene (§3), le impostazioni `Diagnostics:Requests:*` (§5); in `starts.txt` la compilazione e il database
  prima di «pronto» e in ogni passo.
- **§2.5**, la riga di Passenger: i tempi dell'avvio a freddo ora si dividono fra compilazione, database e resto; i numeri
  quando Carmine porta i file.
- **§16** (meccanismi generici), la riga della diagnostica dei contesti: l'`IInterceptor` registrato vale anche per il
  contesto delle viste condivise, che prende **solo** quello che misura (`DatabaseTimingInterceptor`).
- La risposta alla domanda del §6.
- `docs/internal/deploy/LEGGIMI-INSTALLAZIONE-DI-PROVA.md` §4 è aggiornato in questa PR (che cosa copiare e quando).
