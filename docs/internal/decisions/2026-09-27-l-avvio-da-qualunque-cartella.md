# L'avvio da qualunque cartella, e il motivo di un avvio fallito in un file

**Data:** 27 settembre 2026, notte, dopo la prima installazione di `0.2.0` su `test.it.ivao.aero`
**Stato:** **Decisa da Carmine in chat, 27 settembre 2026**: «tutte e due» — l'hub trova le sue cartelle anche da dove sta il
suo assembly, e un avvio fallito scrive il motivo in un file che si scarica via FTP. Il link al commento di Carmine sulla PR lo
aggiunge il master.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: i meccanismi ci sono già (`HubPaths`, `diagnostics/startup.txt`), e si
**estendono**. È un cambio del nucleo, nella sua PR, senza codice di modulo.

## 1. Che cosa è successo

Il pacchetto `v0.2.0` (su `d36df74`) è in `webapp/` dell'FTP di `test.it.ivao.aero`, con `config/division.json` giusto,
`secrets/<casuale>.json` valido e `tmp/restart.txt`. Passenger lo avvia con `dotnet IvaoHub.Web.dll`. Ogni richiesta che
arriva all'applicazione risponde con la pagina di Passenger «Web application could not be started», e **l'hub non ha scritto
niente in `webapp/`**: né `logs/`, né `hub-keys/`, né `diagnostics/`. Il log di Passenger, dice chi amministra il server, è
pulito. Il motivo l'hub lo scrive solo sulla sua uscita (piano §11.3 punto 9, `docs/DEPLOYING.md` «Known limits»).

La causa più probabile: Passenger avvia il processo da una cartella di lavoro che non è `webapp/`. `HubPaths.Resolve` sale
dalla content root, che è la cartella di lavoro, cercando `config/division.json`; non trovandolo prende la cartella di lavoro
come radice. Il file della divisione manca lì, l'avvio si ferma prima di creare `hub-keys/` e prima che esista un logger, e il
motivo va solo su stdout. Anche `wwwroot/` e `appsettings.json` si leggono dalla content root, quindi da lì.
**Non misurato su Passenger**: da quale cartella avvia davvero il processo non lo sappiamo; il file del §3 lo dirà.

## 2. Primo: la radice si trova anche dalla cartella dell'applicazione

- **L'ordine**: `IVAOHUB_ROOT` se c'è (vince su tutto, come oggi: i test lo usano); poi dalla cartella di lavoro in su, come
  oggi (lo sviluppo: la content root è il progetto web, le cartelle sono alla radice del repository); **poi dalla cartella di
  `IvaoHub.Web.dll` in su** (`AppContext.BaseDirectory`). Se non si trova da nessuna delle due, la radice è la cartella
  dell'applicazione (prima era la cartella di lavoro): è lì che chi installa deve mettere `config/division.json`, e il
  messaggio «The division file is missing» ora nomina quel percorso.
- **La radice si trova prima di `WebApplication.CreateBuilder`.** Quando l'ha trovata la cartella dell'applicazione, la
  content root diventa quella cartella (`WebApplicationOptions.ContentRootPath`): così `wwwroot/`, `index.html` del fallback
  della SPA e `appsettings.json` seguono. Negli altri casi la content root resta quella di sempre, e lo sviluppo e i test non
  cambiano.
- **`HubPaths` dice come l'ha trovata** (`Source`), e `diagnostics/startup.txt` lo scrive accanto alla radice.
- La riga di `docs/DEPLOYING.md` «se l'host avvia il processo da un'altra cartella, impostate `IVAOHUB_ROOT`» diventa: non
  serve, l'applicazione si trova da sola; `IVAOHUB_ROOT` resta per chi vuole le cartelle altrove.

## 3. Secondo: un avvio fallito scrive il motivo in `diagnostics/startup-error.txt`

- **Quando**: un'eccezione che esce dal programma prima che l'applicazione sia partita (`ApplicationStarted`). Copre quello
  che succede prima che esistano il logger e l'host: il file della divisione mancante, un file dei segreti illeggibile,
  `AllowedHosts` o le reti dei proxy mancanti, le opzioni validate all'avvio, il validatore di avvio, il database che non
  risponde, la porta occupata.
- **Dove**: `diagnostics/startup-error.txt` sotto la radice trovata al §2, che quando non c'è è la cartella
  dell'applicazione: il file si scarica sempre dalla cartella caricata via FTP.
- **Che cosa dice**: l'ora, versione e commit (`BuildInfo`), l'ambiente, la radice e come è stata trovata, la cartella di
  lavoro e quella dell'applicazione, poi tipo e messaggio dell'eccezione (`reason`), quelli della più interna quando è
  un'altra (`cause`: sotto «a transient failure» di EF c'è «Unable to connect to any of the specified MySQL hosts»), e
  lo stack con le interne. **Mai un segreto**: il
  file non legge la configurazione; e siccome il messaggio di un'eccezione può citare un valore, ogni valore dei file di
  `secrets/`, del file OAuth e di ogni connection string (anche da variabile d'ambiente) viene sostituito da `[redacted]`
  prima di scrivere. Un test lo prova con una connection string dentro il messaggio.
- **Si cancella** al primo avvio riuscito, che scrive `startup.txt` come oggi. Il processo esce comunque con errore e il
  messaggio resta su stdout come prima: il file si aggiunge, non sostituisce niente.
- **Solo il processo vero**: il gestore si registra quando l'assembly d'ingresso è l'hub. Sotto i test (`WebApplicationFactory`)
  e sotto lo strumento che genera il documento OpenAPI non si registra, e i test non scrivono file nella radice del repository.
- **Come vIPI** (`diagnostica/avvio-errore.txt`), ma in inglese come ogni nome del pacchetto, e senza l'«arresto»: un
  processo che muore dopo essere partito non scrive questo file.
- **Che cosa non copre**: quello che si ferma prima del codice gestito o lo uccide senza eccezione — ICU mancante,
  `dotnet` che non trova il runtime, un file `.dll` troncato dall'FTP, memoria finita. Lì resta solo stdout.

## 4. Che cosa è stato misurato

Sul pacchetto `dotnet publish -c Release -r linux-x64 --self-contained` di questo ramo, in un contenitore Linux con solo
.NET 8 (`mcr.microsoft.com/dotnet/runtime:8.0`, come il server di vIPI), il pacchetto in `/srv/webapp`, avviato con
`dotnet /srv/webapp/IvaoHub.Web.dll` **da `/tmp` o da `/`**, `ASPNETCORE_ENVIRONMENT=Production`, MariaDB 11.4.10:

| Caso | Esito |
|---|---|
| nessun `config/division.json` | esce con errore; `/srv/webapp/diagnostics/startup-error.txt` con `root /srv/webapp (not found: …)` e «The division file is missing: /srv/webapp/config/division.json» |
| division e segreti, niente OAuth | `root /srv/webapp (found from the folder of the application)`; `hub-keys/` e `logs/` creati **in `webapp/`**; il motivo è l'`OptionsValidationException` dei campi `Ivao`; nessun valore della connection string nel file |
| tutto a posto | parte: `/health` `Healthy`, `/api/version`, un indirizzo della SPA risponde `index.html` da `wwwroot/` (la content root ha seguito); `startup.txt` dice `root /srv/webapp (found from the folder of the application)`; **`startup-error.txt` cancellato** |
| database su una porta chiusa | `reason` di EF, `cause MySqlConnector.MySqlException: Unable to connect to any of the specified MySQL hosts.`; né l'host, né la porta, né la password nel file |

L'uscita è quella di sempre per un'eccezione non gestita: `134` (SIGABRT) da una shell, `139` quando il processo è il PID 1
del contenitore. **Non misurato**: Passenger stesso, cioè da quale cartella avvia davvero il processo su
`test.it.ivao.aero`; lo dirà la riga `working dir` del file, o la riga `root` di `startup.txt`.

## 5. Alternative scartate

| Alternativa | Perché no |
|---|---|
| Chiedere all'host di impostare `IVAOHUB_ROOT` | dipende da un pannello che non abbiamo, ed è il primo passo che un'installazione dimentica: la cartella dell'applicazione è un fatto che l'hub conosce da sé |
| Content root sempre sulla cartella dell'applicazione | in sviluppo è `bin/Debug/…`: il fallback della SPA e `appsettings.Development.json` cambierebbero, per un caso che solo il server ha |
| `try`/`catch` attorno a tutto `Program.cs` | 300 righe reindentate per lo stesso effetto, e sotto i test scriverebbe `startup-error.txt` nella radice del repository a ogni test che prova un avvio rifiutato |
| Scrivere il motivo nel log di Serilog | il logger è proprio la cosa che ancora non esiste quando l'avvio fallisce |

## Da portare nel piano

- **§11.3 punto 9**, primo trattino: chiuso. Un avvio fallito scrive `diagnostics/startup-error.txt` (cosa contiene, cosa non
  copre, si cancella al primo avvio riuscito); l'hub trova le sue cartelle anche dalla cartella dell'applicazione, quindi la
  cartella di lavoro di Passenger non conta.
- **§11.3 punto 2**: la riga delle cartelle aggiunge `diagnostics/startup-error.txt` accanto a `startup.txt`.
- **§11.3 punto 5**: `startup.txt` dice anche come è stata trovata la radice.
- **`docs/DEPLOYING.md`** «What you deploy» (la riga di `logs/`, `diagnostics/` e il paragrafo su `IVAOHUB_ROOT`) e «Known
  limits» (il primo punto): aggiornati in questa PR.
- **`docs/internal/deploy/LEGGIMI-INSTALLAZIONE-DI-PROVA.md`** «Se qualcosa non parte»: aggiornato in questa PR.
