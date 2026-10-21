# Il client IVAO non lancia, in un posto solo

**Data:** 21 ottobre 2026 — revisione del codice, lotto 4 (nota `2026-10-20-la-revisione-del-codice.md`), PR del nucleo
`fix/ivao-client-never-throws`
**Stato:** correzione dei rilievi #324 (maggiore) e #325 punto 1 (minore). Nessuna domanda per Carmine: il contratto era già
scritto (`IIvaoApiClient`, «null quando IVAO non si è potuto chiedere», «Nothing here throws»); qui si registrano le due
conseguenze che la correzione porta con sé.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: l'unico client tipizzato resta quello; il suo punto di chiamata diventa uno.

## 1. Che cosa non andava

- `IvaoApiClient.ReadAsync` non proteggeva né la chiamata né la lettura del JSON: lo faceva solo `ReadOrNothingAsync`, usato dalle
  chiamate la cui caduta era stata misurata (posizioni ATC, FRA, pagine del tracker, prenotazioni). Piste, piani di volo, tracce e
  METAR passavano dall'altro e lanciavano: un PIREP già salvato rispondeva 500 (`PirepSubmission` → `RunwayDirectory.EnsureAsync`),
  le chiamate del tracker 500 invece di `trackerUnavailable`, il giro del meteo cadeva quando cadeva il METAR di IVAO.
- Due report allo stesso aeroporto senza piste, nello stesso momento, inserivano due volte (chiave duplicata, 500); un designatore
  ripetuto nella risposta di IVAO lanciava già all'`Add`.
- I filtri dei client del meteo e dei confini dei FIR prendevano `HttpRequestException` e `TaskCanceledException`, non le eccezioni
  del gestore di resilienza (`TimeoutRejectedException` del suo tempo totale, `BrokenCircuitException` del circuito aperto).

## 2. La forma

- **`IvaoApiClient.SendAsync`** è l'unico punto in cui il client chiama IVAO e l'unico in cui una chiamata fallisce: niente token,
  uno stato, una rete che non risponde, un tempo scaduto o un circuito aperto, un corpo che non è JSON sono tutti «nessuna risposta»,
  un avviso con il percorso e mai con il token o il corpo. Passano di lì anche whazzup (senza token) e `/v2/users/me` (con il token
  del membro). Solo la cancellazione di chi chiama arriva a chi chiama. `ReadOrNothingAsync` non c'è più.
- **`RunwayDirectory`** salva aeroporto per aeroporto, toglie i designatori ripetuti, e se il salvataggio trova le righe di un'altra
  richiesta (chiave duplicata, solo righe di piste) lascia andare le sue e prosegue.
- **`OutboundFailure.CouldNotReach`** (`Core/Services/`): il filtro unico dei client che leggono una caduta come «nessuna risposta»
  (NOAA, VATSIM, confini dei FIR): le eccezioni del trasporto e `ExecutionRejectedException` di Polly, che copre il tempo totale, il
  circuito e il limitatore.

## 3. Le due conseguenze

1. **La sincronizzazione notturna dei dati di riferimento** (`RefDataSyncJob`): una chiamata di riferimento che cade (centri,
   aeroporti, tipi di aereo, vocabolari) non fa più cadere tutta la corsa con «failed» buttando le parti già lette; dà una lista vuota,
   quella parte resta com'era, e la corsa è «partial» come per uno stato non riuscito. È quello che `IIvaoApiClient` prometteva già
   per i centri («Empty when IVAO cannot be reached: never an exception») e che la nota di classe del job dice («It never throws»).
2. **I tempi dei client erano righe morte.** Misurato il 21 ottobre 2026 (`OutboundFailureTests`, con
   `Microsoft.Extensions.Http.Resilience` 10.9.0): `AddStandardResilienceHandler()` lascia `HttpClient.Timeout` infinito, quindi i 60
   secondi di NOAA, i 15 di VATSIM e i 30 di IVAO non erano un limite; il limite è il totale del gestore, 30 secondi per tutti. Le
   righe sono tolte, il comportamento non cambia; un test dice se una versione futura del gestore smette di farlo. Il rilievo «60
   secondi dietro 30» del #325 era quindi vero nell'effetto (scattava il totale, con un'eccezione che nessuno prendeva) e non nella
   causa.

## 4. Alternative scartate

- **Un try/catch per metodo**: è la forma che aveva prodotto il difetto, una protezione per chiamata decisa caso per caso.
- **Tenere le chiamate di riferimento che lanciano** (spostando su `ReadOrNothingAsync` solo le quattro del #324): lascerebbe due
  contratti nello stesso client e la nota di classe falsa; e una corsa «failed» butta le parti buone per una cattiva.
- **Riportare VATSIM a 15 secondi** con il tempo totale del suo gestore: cambierebbe un comportamento che in produzione è 30 secondi
  da sempre; non serve alla correzione.

## Da portare nel piano

- §16 (IVAO API, riga 3863 circa): ogni chiamata del client passa da un punto solo e non lancia mai, cancellazione di chi chiama a
  parte; una chiamata di riferimento che cade lascia quella parte della sincronizzazione com'era («partial»), non fa cadere la corsa.
- §16 (stessa riga): nessun client ha un tempo suo; il limite è il totale del gestore standard, 30 secondi (IVAO, NOAA, VATSIM).
- Changelog: 0.10.3, il client IVAO non lancia più dove promette null (#324), i client del meteo e dei confini leggono le eccezioni
  di Polly come «non raggiungibile» (#325 punto 1).
