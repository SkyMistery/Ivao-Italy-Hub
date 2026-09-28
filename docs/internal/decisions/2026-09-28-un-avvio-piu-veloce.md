# Un avvio più veloce, e `diagnostics/starts.txt`

**Data:** 28 settembre 2026
**Stato:** **codice di decisioni già prese da Carmine**, nessuna domanda nuova. È la PR 3 della coda del codice (piano 1.22),
versione **0.2.4**, PATCH: nessuna migrazione, nessuna pagina.
- Il taglio dell'avvio: nota `2026-09-28-l-avvio-a-freddo`, domande 1 e 4
  (<https://github.com/SkyMistery/Ivao-Italy-Hub/pull/166#issuecomment-5867344587>).
- `diagnostics/starts.txt`: nota `2026-09-28-i-job-quando-passenger-spegne-l-hub`, risposta 2
  (<https://github.com/SkyMistery/Ivao-Italy-Hub/pull/165#issuecomment-5865067623>) e l'aggiunta della memoria e della
  durata dell'avvio (<https://github.com/SkyMistery/Ivao-Italy-Hub/pull/165#issuecomment-5865413362>).
- Le due correzioni trovate rivedendo la #162, verificate dal master (piano 1.22, «La coda del codice», punto 3).

**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: si estendono il pacchetto, `InitializeAsync`, `HubDatabaseInitializer`,
`HubPaths` e la diagnostica d'avvio che ci sono già. Nessun meccanismo nuovo.

## 1. Che cosa fa la PR

1. **ReadyToRun** sul pacchetto, solo quando la pubblicazione ha un RID (`IvaoHub.Web.csproj`): `dotnet build`, i test e il
   banco e2e (che pubblica senza `-r`) restano come prima.
2. **Le migrazioni dei moduli solo se pendenti**, come il nucleo: `HubDatabaseInitializer.MigrateAsync(DbContext)` chiede
   `GetPendingMigrationsAsync` e chiama `MigrateAsync` solo se c'è qualcosa. Le migrazioni applicate a un modulo ora compaiono
   anche in `startup.txt`, accanto a quelle del nucleo.
3. **TieredPGO spento** (`<TieredPGO>false</TieredPGO>`, finisce in `runtimeconfig.json`).
4. **`diagnostics/starts.txt`**, sotto.
5. **Le due correzioni della #162**:
   - `HubPaths`: se la cartella dell'applicazione ha il suo `config/division.json`, vince lei, subito dopo `IVAOHUB_ROOT`.
     Prima la risalita dalla cartella di lavoro (fino a sei livelli) veniva prima, e un `division.json` di un'altra
     installazione più in alto nello stesso albero FTP vinceva in silenzio. In sviluppo `bin/` non ne ha mai uno, quindi la
     radice resta il repository.
   - `StartupFailure`: ogni valore `ConnectionStrings:*` si scompone con `DbConnectionStringBuilder` e **ogni parte** (host,
     database, utente, password, dai sei caratteri in su come oggi) si nasconde in `startup-error.txt`. Un driver cita
     «Access denied for user 'hub'@'db.example.org'», mai la stringa intera.

## 2. `diagnostics/starts.txt`

Una riga per avvio, una per arresto ordinato, una per segnale, sempre in coda; oltre le 2 000 righe, all'avvio successivo
restano le ultime 1 000 con l'intestazione. Mai un segreto; mai un'eccezione che fermi l'avvio o l'arresto. È l'`avvii.txt`
di vIPI, con le sue lezioni: il verdetto sul processo prima si legge **per pid** (Passenger a volte ne tiene due), e il
segnale ha una riga sua, perché l'ordine dei due gestori non lo decidiamo noi.

```
2026-09-28 13:51:35Z  START   pid 8        0.2.4+4cbcd38  ready in 1.86 s  previous: pid 8 stopped in order 00:00:09 ago  memory 184 MB (peak 184 MB, managed heap 18 MB)  steps ms: runtime 31, services 188, endpoints 96, validation 11, models 777, migrations 218, module migrations 22, superadmins 148, position grants 10, content 283, reference data 7, startup.txt 2, listening 61
2026-09-28 13:51:36Z  STOP    pid 8        lived 00:00:02  requests 4  first answer at 2.39 s  memory 200 MB (peak 200 MB, managed heap 22 MB)
2026-09-28 13:51:36Z  SIGNAL  pid 8        SIGTERM from the system
```

- **`ready in`**: dalla creazione del processo a quando accetta richieste. **`first answer at`** (nella riga `STOP`): quando è
  uscita la prima risposta, contato dallo stesso istante; è l'attesa del visitatore che ha svegliato l'hub, tabella delle
  rotte compresa (~0,5 s, la costruisce ASP.NET dentro la prima richiesta).
- **Scelte di questa sessione**, non decisioni di Carmine: la lingua (inglese, come `startup.txt`), i nomi dei passi, il tetto
  2 000/1 000, la riga `SIGNAL`, il `first answer at` nella riga `STOP` (la riga `START` si scrive prima della prima richiesta),
  e che il file lo scriva **solo il processo dell'hub** (come `startup-error.txt`): i test d'integrazione accendono e spengono
  decine di host nello stesso processo, e le loro righe sembrerebbero una fila di crash. Il banco e2e invece lo scrive.

## 3. Le misure

Come la nota dell'avvio a freddo (§2 di quella): `dotnet publish -c Release -r linux-x64 --self-contained` nel contenitore
`mcr.microsoft.com/dotnet/sdk:10.0`; avvio con `dotnet IvaoHub.Web.dll` da un host `aspnet:8.0`, cartella dell'app come
cartella di lavoro, `Production`, MariaDB 11.4.10 già migrato e seminato; un contenitore nuovo per avvio, **una CPU**
(`--cpus=1`), **9 avvii per variante, alternati**, mediana. «Prima» è `main` (`dda2aa5`, 0.2.3), «dopo» è questo ramo.

| | porta aperta | `/api/me` risposto | intervallo |
|---|---:|---:|---:|
| Prima (0.2.3) | 3,89 s | **4,00 s** | 3,70–4,69 s |
| Dopo (0.2.4) | 2,31 s | **2,40 s** | 2,17–2,80 s |
| | | **−1,60 s (−40%)** | |

- ⚠️ Il «prima» di oggi (4,00 s) è più lento dei 3,03 s della nota: altra ora, altro carico della macchina, codice di
  `main` cresciuto. Il rapporto regge: −40% qui, −42% là. Sul server, se il rapporto vale anche lì: **8,4 s × 0,6 ≈ 5 s**,
  come stimava la nota (~4,9 s). **È una stima**: lo dirà `starts.txt` del server.
- Una prima serie, girata mentre sulla stessa macchina correva la suite d'integrazione, dava 5,08 → 2,64 s: scartata per il
  rumore, stessa direzione.
- **A cache del disco svuotata** (`drop_caches`, 5 avvii alternati): 3,75 → 2,19 s. Non ho verificato che la cache fosse
  davvero fredda dentro la macchina virtuale di Docker Desktop: il «prima» non è più lento del caso a cache calda, come
  invece nella nota. Lo riporto, non ci conto.
- **I passi, da `starts.txt` del pacchetto nuovo** (mediana dei 9 avvii a una CPU; `ready in` 1,80 s, `first answer at`
  2,31 s): runtime 26 ms · servizi 196 · endpoint 85 · validazione 9 · **modelli EF 718** · migrazioni del nucleo 210 ·
  migrazioni dei moduli **43** (erano 264 più lo stallo) · superadmin 141 · permessi delle posizioni 11 · **contenuti 270** ·
  dati di riferimento 8 · `startup.txt` 2 · Kestrel e Quartz 83. Quello che resta è per lo più il modello EF, il seeder dei
  contenuti, la tabella delle rotte e le migrazioni del nucleo: sono i passi che il marcatore d'inizializzazione
  salterebbe (tranne i modelli), e la domanda 2 della nota si decide con questi numeri presi sul server.
- **Il pacchetto**: 600 file in tutti e due; **139 → 171 MB** su disco, zip **59 → 73 MB** (MiB: 61,7 → 76,8 MB).
  La pubblicazione nel contenitore: 17 s prima, 27 s dopo.
- **Memoria**: RSS dopo `/api/me` 197 → 200 MB; `starts.txt` scrive 184–199 MB quando l'hub è pronto.

## 4. La consegna

- **Fra 0.2.3 e 0.2.4 cambiano 62 file su 600** (confronto per hash dei due pacchetti, lo stesso che fa l'azione `Diff`):
  le **53 DLL** che diceva la nota (le 4 dell'hub e 49 librerie di terzi ora precompilate), più `.pdb`, `.xml`,
  `deps.json`, `runtimeconfig.json` e `staticwebassets.endpoints.json` dell'hub; **26 → 58 MB** di file da caricare. Il
  runtime (`libcoreclr.so`, `System.Private.CoreLib.dll`) **non** cambia, `wwwroot/` nemmeno (questa PR non tocca la SPA).
- **Determinismo verificato**: fra il pacchetto ReadyToRun della #166 (codice di `6c20b22`) e questo cambiano solo le 4 DLL
  dell'hub su 366. Dalla 0.2.5 le consegne a pochi file tornano piccole come prima.
- **`tools/prepare-delivery.ps1` non l'ho potuto lanciare**: il cancello globale rifiuta lo script quando parte da una chat
  (`CLAUDE.local.md`), e non l'ho aggirato. Il confronto qui sopra l'ho fatto con `sha256sum` sui due pacchetti. Lo script
  lavora per hash e per nome (`IvaoHub.*.dll` insieme, il timbro dalla risorsa di versione di `IvaoHub.Web.dll`, che in
  un'assembly ReadyToRun c'è ancora), quindi non vedo che cosa dovrebbe cambiare; **va provato da Carmine** con `Fetch` e
  `Diff` della 0.2.4. `docs/DELIVERING.md` dice ora che le 49 librerie di terzi, una volta, vanno.
- **Che cosa carica Carmine**: i file che propone il `Diff` contro la consegna che è davvero sul server (`-Against`), cioè
  ~62 file più quello che è cambiato dopo di essa; un pacchetto a file cambiati, con la solita regola del rinomina. Il
  pacchetto intero non serve: il runtime non cambia.
- **Che cosa rilegge**: `diagnostics/starts.txt` dopo **qualche visita distanziata di più di un minuto** (per esempio cinque
  `GET /staff` a un minuto e mezzo l'una dall'altra): ogni visita dopo il silenzio è un avvio a freddo, e il file dice
  `ready in`, `first answer at` e i passi. Con quei numeri si decidono il marcatore (domanda 2 della nota dell'avvio a
  freddo) e la strada A della #164 (domanda 3). E da fuori, come nella nota della #165, il tempo al primo byte di una
  richiesta dopo più di un minuto di silenzio.

## 5. Che cosa non è verificato

- **I numeri del server**: tutto il §3 è misurato in locale.
- La pubblicazione ReadyToRun per linux-x64 **da Windows**: non provata (la CI e il rilascio pubblicano da Linux, e il banco
  e2e pubblica senza RID, quindi senza ReadyToRun).
- La riga `SIGNAL` e l'arresto ordinato con **Passenger**: misurati con `kill` (SIGTERM) in un contenitore, 20 arresti con la
  loro riga `SIGNAL` su 20. Come li manda Passenger lo dirà il file del server.
- Con le migrazioni dei moduli saltate quando non ce n'è, all'avvio non gira più il controllo di EF 9 «il modello ha
  cambiamenti senza migrazione» per i moduli: gira ancora in CI e nei test, dove il database parte vuoto e le migrazioni
  sono sempre pendenti. Per il nucleo era già così.

## Da portare nel piano

- **Intestazione, «La coda del codice» punto 3**: fatto, 0.2.4.
- **§11.3 punto 1** (Pacchetto): ReadyToRun e TieredPGO spento sono nel codice dalla 0.2.4; misurati 139 → 171 MB, zip 59 → 73
  MB, 62 file cambiati alla prima consegna (53 DLL), determinismo verificato; la misura locale (4,00 → 2,40 s a una CPU).
- **§11.3 punto 2**: `diagnostics/starts.txt` è nel codice, com'è fatto (§2); la radice si trova prima dalla cartella
  dell'applicazione se ha il suo `config/division.json`; in `startup-error.txt` si nascondono anche le parti delle stringhe
  di connessione.
- **§11.3 punto 5** (Migrazioni): i moduli controllano le migrazioni pendenti come il nucleo (nel codice dalla 0.2.4), e
  `startup.txt` elenca anche le loro.
- **§2.5**, la riga di Passenger: la misura locale dopo i tagli; quella del server quando c'è.
- `docs/DEPLOYING.md` («What you deploy», il paragrafo della radice e di `starts.txt`, i controlli dopo il deploy, «Known
  limits») e `docs/DELIVERING.md` (le librerie di terzi della prima consegna ReadyToRun) sono aggiornati in questa PR.
