# Il tracker senza VID: le sessioni di un aeroporto, a pagine, con il tipo di connessione (E10a)

**Data:** 30 settembre 2026 — fase E10a di M4, PR del nucleo
**Stato:** **scelta tecnica**, per dare forma nel codice all'estensione n.2 del design (`09-design-m4.md` §13 n.2, §9.1, §5.1),
decisa da Carmine sulla #180 ([conferma di §17.1 e §17.2][ok]; nota `2026-09-29-dopo-l-evento-e-gli-award` §2.1), con quello che
E0 ha trovato in più (`10-piano-implementazione-m4.md`, E0, «Trovato», punto 6: il tipo di connessione e le pagine oltre 200).
Nessuna domanda nuova per Carmine. **Una misura cambia il client di IVAO per tutti**: un tentativo aspetta 20 secondi invece di 10
(§3.4), perché con 10 una ricerca per aeroporto non riuscirebbe mai.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: si estende l'unico client di IVAO — `IIvaoApiClient`, `IvaoApiClient`,
`FixtureIvaoApiClient` — e il suo lettore unico, `IvaoTrackerReader`; il modulo degli eventi non parla con IVAO (`CLAUDE.md` §3). È
una PR del nucleo, prima di E13a che la usa (`CLAUDE.md` §0 regola 6).

[ok]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/180#issuecomment-5880522987

## 1. Che cosa serve, e perché il modulo non ne fa a meno

- **«Chi ha volato senza prenotare»** (design §5.1, nota `dopo-l-evento-e-gli-award` §2.1): il job `events-after` di E13a conta le
  sessioni con partenza o arrivo negli scali dell'evento, nella sua finestra, **senza sapere di chi**, meno chi ha prenotato; solo il
  numero va nelle statistiche.
- **Le presenze ATC dal tracker** quando la divisione ha `atcData: none` (design §9.3): le sessioni di un controllore, **per VID**,
  solo quelle di tipo `ATC`.
- Oggi `IvaoSessionQuery` vuole il VID (`int Vid`), non conosce il tipo di connessione — né come filtro né nel DTO — e si ferma a
  **200 sessioni** (`MaxSessions`), a pagine di 50: la sera di un RFE un aeroporto ne ha di più.
- Il modulo non può chiamare IVAO da sé: l'unico client è del nucleo. E anche il client delle fixture, che in sviluppo e sul banco
  risponde al posto di IVAO, deve saper rispondere alla domanda nuova, attraverso lo stesso lettore (`IvaoTrackerReader`: «a
  fixture that parsed itself differently from production would be a fixture that proves nothing»).

## 2. Che cosa è stato misurato

Il 30 settembre 2026, con il token dell'applicazione (`client_credentials`) di `config/ivao-oauth.json`, da script fuori dal
repository che stampano solo forme, conteggi, stati e tempi — mai un VID, un nome o il token —, su finestre del 21–28 settembre, già
chiuse.

1. **Il token dell'applicazione basta per ogni lettura**: nessuno scope chiesto, dura 1800 secondi. Legge `/v2/tracker/sessions`
   senza `userId` — per aeroporto, per tipo di connessione, e perfino senza nessun filtro (1979 sessioni in quattro ore di sera di
   tutta la rete) —, con `userId` e `connectionType=ATC`, e i piani e le tracce di una sessione trovata senza VID.
2. **La pagina**: `perPage` fino a **100**; 101, 150, 200 e 500 rispondono 400 «Should be lower than 100». L'hub ne chiedeva 50
   («what the API was measured to accept», T2). La risposta è sempre `{ items, totalItems, perPage, page, pages }`; una finestra vuota risponde
   `pages: 0`; la pagina dopo l'ultima risponde 200 con `items: []`; **`page=0` risponde 500**.
3. **L'ordine**: dalla sessione più recente (`createdAt` e `id` decrescenti).
4. **La finestra** (`from`, `to`) filtra **l'inizio della sessione** (`createdAt`), con gli estremi compresi: delle 528 sessioni
   partite nelle tre ore prima di una finestra di mezz'ora e ancora connesse al suo inizio, nessuna torna; con `from` = `to` =
   l'inizio di una sessione, quella torna. Chi vuole «chi era connesso» allarga la finestra all'indietro.
5. **Gli aeroporti**: `departureId` o `arrivalId` da solo trova una sessione se **una revisione qualunque** del piano ha
   quell'aeroporto (100 sessioni su 100 da EDDF: 99 lo hanno nella prima revisione, 97 nell'ultima); **tutti e due insieme** vogliono
   **una stessa revisione** — misurato su una sessione il cui piano è passato da LIPZ→LIRF a LIRF→LICR: LIPZ→LIRF e LIRF→LICR la
   trovano, LIPZ→LICR e LIRF→LIRF no. Una sessione ATC non ha piani (`flightPlans: []`), quindi non esce mai da una domanda per
   aeroporto.
6. **Il tipo di connessione**: ogni riga porta `connectionType`; il filtro accetta **`PILOT`, `ATC`, `OBS`, `FOLME`**, solo in
   maiuscolo (ogni altra parola: 400 con l'elenco). In mezz'ora di sera, su una pagina di tutta la rete: 69 piloti, 20 ATC, 11
   osservatori.
7. **Le persone**: ogni riga, con o senza `userId` nella domanda, porta l'oggetto `user` con **nome e cognome**, divisione e rating.
   Senza VID sono persone che con l'hub non hanno niente a che fare (§3.5).
8. **Una sessione ancora aperta** non ha `completedAt`, e `time` è la durata fin lì.
9. **`callsign`** filtra per l'inizio: il nominativo intero di una postazione (forma `AAAA_AAA`) trova 6 sessioni in una settimana,
   le sue prime quattro lettere 48. Misurato per chi verrà, non usato qui.
10. **I tempi** — la misura che conta di più:
    - una domanda **per VID**, com'è quella dei tour: **65–118 ms**, anche l'ultima pagina;
    - una domanda **per aeroporto**: 0,2–0,6 s una pagina, ma **la pagina che contiene l'ultima riga del risultato costa ~10–11,5
      s**, piena o no. Con 13 righe: `perPage` 13 → 11,3 s; `perPage` 6, pagina 2 → 0,34 s, pagina 3 → 11,2 s; `perPage` 1, pagina 13
      → 10,7 s. Riprodotto in due giri su cinque domande (EDDF e LIRF, partenze e arrivi, con e senza `connectionType`); una finestra
      vuota è veloce. Quindi **ogni domanda per aeroporto che trova qualcosa costa almeno ~10,5 s**, e dal client non si evita:
      nemmeno la coda chiesta a parte, con la finestra ristretta sull'ultima riga, è veloce;
    - `connectionType` senza aeroporto: 1,5–2,8 s.
11. **I limiti di chiamate**: nessun header di rate limit (solo quelli di Cloudflare, `cf-cache-status: DYNAMIC`). 60 domande
    veloci di fila (21 s) e 10 insieme: tutte 200. 60 domande lente di fila: tutte 200, ~10,5 s l'una. **10 domande lente insieme:
    tutte 504** dopo ~15 s — il gateway di IVAO, che rinuncia a 15 secondi (lo stesso misurato in A2 di M3). Il limite vero non è
    un numero di chiamate: sono **i 15 secondi del gateway e le domande lente in parallelo**.
12. **La pipeline dell'hub** (il codice di `main`, provato con un programma fuori dal repository costruito su
    `AddIvaoIntegration()`): `AddStandardResilienceHandler()` taglia ogni tentativo a **10 secondi**. La pagina lenta viene tagliata a
    10 s, ritentata, tagliata di nuovo, e a **30 s** esce una `TimeoutRejectedException`, **lanciata** al chiamante: oggi una domanda
    per aeroporto **non riuscirebbe mai**, farebbe a IVAO tre volte la stessa scansione lenta, e non risponderebbe `null` come
    promette `IIvaoApiClient` («`null` means IVAO could not be asked»).

## 3. La forma nel codice

### 3.1 La domanda — `IvaoSessionQuery`

- **Il VID facoltativo**: `IvaoSessionQuery(int? Vid, DateTime FromUtc, DateTime ToUtc, string? DepartureIcao = null, string?
  ArrivalIcao = null, IvaoConnectionType? ConnectionType = null)`. Senza VID la domanda non porta `userId`. I tour la scrivono come
  prima, `new IvaoSessionQuery(vid, from, to, …)`: nessuna riga del modulo dei tour cambia.
- **Il tipo di connessione** è `IvaoConnectionType` — `Pilot`, `Atc`, `Observer`, `FollowMe`, le quattro parole del tracker —,
  scritto e letto con le parole di IVAO in un posto solo, il lettore.
- **`Limit`**, dichiarato da chi chiama (`init`, almeno 1; predefinito **200**, il tetto di oggi, per un pilota che sceglie il suo
  volo): quante sessioni al più si leggono, dalla più recente. Il job di E13a dichiara il suo: quello che un giro può portare.
- **`PageSize` è 100** (§2 punto 2), e una pagina chiede `min(100, Limit)` righe (`PerPage`): un limite di cinque non scarica cento
  righe.
- **Una domanda senza VID né aeroporto resta permessa**: IVAO la accetta, il limite la tiene piccola, e nessuno la usa. Niente da
  vietare.

### 3.2 La risposta — `SearchSessionsAsync`, la stessa

- **La firma non cambia** (una lista, o `null` se IVAO non si è potuto chiedere): i quattro client di prova dei test esistenti la
  implementano, e i tour la usano. Le sessioni vengono **dalla più recente, al più `Limit`, senza doppioni**: una sessione nuova che
  arriva mentre si sfogliano le pagine le fa scorrere, e la stessa riga su due pagine conta una volta.
- **Intera o tagliata**: una risposta con meno sessioni del limite è tutto quello che c'è; con esattamente `Limit` può essere
  tagliata, e il resto sta nella parte della finestra prima della sessione più vecchia che si ha (le righe vengono dalla più
  recente). Chi vuole tutto chiede quella parte — ed è così che si lavora a lotti, anche da un giro all'altro. Il log dice quante ne
  conta IVAO e quante se ne sono lette («Read 250 of 305 …»).
- **Una pagina che non si ha fa `null` tutta la risposta**, anche quando non arriva affatto — un timeout, una connessione caduta:
  prima usciva un'eccezione (§2 punto 12). Ora le pagine passano da `ReadOrNothingAsync`, come le postazioni ATC (A2 di M3): «non
  abbiamo potuto guardare» non è «non c'è». ⚠️ **Per i tour** vuol dire che, se IVAO non risponde affatto, la pagina del pilota dice
  «tracker non disponibile» (`flightops:errors.trackerUnavailable`, quello che già dice per una risposta rifiutata) invece di un
  errore 500.
- **`IvaoTrackerSessionDto.ConnectionType`**, una proprietà `init` sotto il costruttore, come le quattro di `IvaoAirportDto` (T1): i
  test che costruiscono il DTO a mano non cambiano, e lì vale `null`. ⚠️ **Gli aeroporti del DTO restano quelli della prima
  revisione**: una sessione trovata per la sua partenza da un aeroporto può dirne un altro (la sessione LIPZ→LIRF poi LIRF→LICR,
  trovata per la partenza da LIRF, dice LIPZ); le revisioni sono in `RawJson`, e il commento del DTO lo dice.

### 3.3 Il lettore unico — `IvaoTrackerReader`

- **Il giro delle pagine** (`ReadPagesAsync`) sta nel lettore, con la pagina data da chi chiama: il client vero la chiede a IVAO, i
  test la danno dalle pagine registrate. Si ferma all'ultima pagina, a una pagina vuota (qualunque cosa dica il conteggio), al
  limite; comincia da 1, mai da 0. Un limite raggiunto prima della fine ha un vantaggio misurato: la pagina lenta, quella con
  l'ultima riga, non si chiede.
- **La regola del tracker** (`Answers`), com'è stata misurata (§2 punti 4–6): il VID se c'è; l'inizio nella finestra, estremi
  compresi; il tipo di connessione; un aeroporto in una revisione qualunque, due nella stessa. La usa `FixtureIvaoApiClient`, che
  senza VID risponde dai file degli aeroporti chiesti (`tracker-airport-<ICAO>.json`, della partenza e dell'arrivo) e ordina come
  IVAO. Per i file per VID dei tour cambiano soltanto l'ordine — dalla più recente, come IVAO; i tour ordinano da sé — e la regola
  delle revisioni, che per loro dà lo stesso: nessuna delle loro sessioni ha aeroporti diversi fra una revisione e l'altra
  (controllato su `tracker-sessions-780001.json` e `-780002.json`).

### 3.4 Aspettare IVAO — `IvaoServiceCollectionExtensions`

- Il gestore standard del client dei dati aspetta **20 secondi per tentativo**, più dei 15 del gateway di IVAO: torna la risposta
  di IVAO — la pagina, o il suo 504 —, mai un taglio dell'hub. **Il totale resta 30 secondi**: una chiamata che non risponde costa
  quanto prima (un tentativo e l'inizio di un secondo, invece di due e l'inizio di un terzo). Per le chiamate veloci non cambia
  niente.
- Il campionamento dell'interruttore passa da 30 a **40 secondi**: è la regola del gestore stesso (almeno il doppio di un
  tentativo), che altrimenti rifiuta le opzioni al primo uso — provato: con 30 la lettura delle opzioni lancia
  `OptionsValidationException`.
- Il client del token resta con i 10 secondi standard: risponde in un terzo di secondo.
- **Provato con IVAO vero** (lo stesso programma, sul codice nuovo): gli arrivi di LIRF in quattro ore di sera, 13 sessioni in 12,0
  s; le partenze di EDDF in una settimana con `Limit` 1000, **305 sessioni** in 12,6 s (tre pagine veloci e quella lenta); con
  `Limit` 250, 250 in 1,75 s (la pagina lenta non si chiede); per VID, com'è nei tour, 32 sessioni in 118 ms.

### 3.5 Le fixture — `tools/record-ivao-fixtures.mjs --sessions-at`

- Una modalità nuova dello script registra **quello che è successo in un aeroporto in una finestra**, chiesto come lo chiede l'hub
  senza VID: le partenze, gli arrivi e le sessioni ATC delle sue postazioni (`connectionType=ATC`, callsign che comincia con
  `<ICAO>_`), in `tracker-airport-<ICAO>.json`; e le partenze di nuovo, **due per pagina, come IVAO le ha date**, con la pagina dopo
  l'ultima e la risposta di una finestra vuota, in `tracker-pages-<ICAO>.json`, per i test del giro delle pagine.
- **Le persone tolte, più che nelle fixture dei tour**, perché qui sono sconosciuti e non i voli di un membro: via l'oggetto `user`
  (come lì); **ogni membro una VID** del campo che si dà allo script (i test contano persone, e chi si collega due volte resta uno),
  nell'ordine in cui compare — con più membri che VID lo script non scrive niente; il callsign di un pilota diventa `TST` e il numero
  del membro; **gli identificativi delle sessioni e dei piani sono rinumerati** (da 1000001 e da 2000001), perché è all'identificativo
  di una sessione che IVAO risponde con il nome. Il callsign di un controllore resta: nomina una postazione. Prima di scrivere, lo
  script controlla che nel testo non restino una VID vera né un nome.
- **Registrato**: LIRF, 28 settembre 2026, 16:00–17:59:59 UTC, come VID 761020–761028 (a E10a erano date le 761020–761029): 4
  partenze, 6 arrivi, la torre; 10 sessioni di 9 membri — uno collegato due volte, un volo di 38 secondi, e la sessione LIPZ→LIRF
  poi LIRF→LICR, partenza e arrivo di LIRF insieme. La finestra è scelta perché ci stiano nove persone e questi casi.
- Il tentativo ripetuto che lo script faceva per le postazioni (`getWorld`) serve anche qui, per la pagina lenta: diventa uno solo,
  `getPatiently`.

### 3.6 Per chi chiama (E13a)

- **Mai due domande per aeroporto insieme** (§2 punto 11): una alla volta, e poche per giro — ognuna che trova qualcosa costa almeno
  ~10,5 s.
- **Partenze e arrivi sono due domande**: chieste insieme valgono per la stessa revisione, non «l'una o l'altra». La stessa sessione
  può tornare da tutte e due (LIPZ→LIRF poi LIRF→LICR): si conta una volta, per `Id`.
- **La finestra è sull'inizio**: per chi si è connesso prima dell'evento e ha volato durante, `FromUtc` va allargato.
- **Il limite si dichiara**: con esattamente `Limit` sessioni la risposta può essere tagliata (§3.2).
- **Il tipo di connessione si chiede** (`Pilot` per chi ha volato, `Atc` per le presenze per VID) e si legge nel DTO.

## 4. Alternative scartate

| Alternativa | Perché no |
|---|---|
| Un metodo nuovo nell'interfaccia per la ricerca senza VID | due metodi per una ricerca sola, due giri di pagine; e i quattro client di prova dei test esistenti andrebbero toccati, o avrebbero un metodo predefinito in più |
| Una risposta nuova che dica «intera» o «tagliata» | cambia il tipo di ritorno che usano i tour e i client di prova; `Count < Limit` dice lo stesso, e la parte che manca si chiede restringendo la finestra |
| Una domanda a sé per l'aeroporto, con il VID obbligatorio nell'altra | due forme della stessa domanda, due `ToQueryString`, e due strade nel client e nelle fixture |
| Un tetto fisso più alto al posto del limite di chi chiama | il tetto di un pilota non è quello di un job; e un tetto alto farebbe chiedere anche ai tour la pagina lenta |
| Leggere a parte la coda lenta (pagine più piccole, o la finestra ristretta sull'ultima riga) | misurato: è lenta la pagina che contiene l'ultima riga, di qualunque misura sia; non si evita |
| Il tempo più lungo solo per le pagine del tracker (un generatore di timeout che guarda la richiesta) | più macchinoso, e senza guadagno: alle chiamate veloci 20 s non cambiano niente, e il caso peggiore resta 30 s |
| Anche il totale più lungo, perché dopo un 504 stia un secondo tentativo intero | una chiamata che non risponde costerebbe di più a chi aspetta una pagina; un 504 lo riprova il giro dopo del job |
| Rifiutare una domanda senza VID né aeroporto | il limite la tiene piccola, IVAO la accetta, e nessuno la usa |
| Tutte le persone di una registrazione in una VID sola, come `--list` dei tour | «chi ha volato senza prenotare» conta persone |
| Tenere callsign e identificativi veri, come le fixture dei tour | sono sconosciuti, e l'identificativo di una sessione riporta al nome, su IVAO |
| Le pagine per i test tagliate dal file dell'aeroporto | la forma della pagina (`pages: 0`, la pagina dopo l'ultima) si prova su pagine vere, come ogni forma di IVAO in queste fixture |
| `perPage` 50 come prima | misurato 100: metà delle chiamate |

## 5. Che cosa si tocca

Tutto del nucleo, ed è il perché di questa nota (`core-guard`):

- **Il client**: `src/IvaoHub.Core/Ivao/IvaoTracker.cs` (la domanda, il tipo di connessione, il DTO, il giro delle pagine e la regola
  nel lettore), `IvaoApiClient.cs`, `FixtureIvaoApiClient.cs`, `IIvaoApiClient.cs` (solo il commento),
  `IvaoServiceCollectionExtensions.cs` (i 20 secondi).
- **Lo strumento**: `tools/record-ivao-fixtures.mjs` (la modalità `--sessions-at`, e `getPatiently`).
- **Le fixture**: `tests/fixtures/ivao/tracker-airport-LIRF.json` e `tracker-pages-LIRF.json` (nuove), e la loro sezione in
  `tests/fixtures/ivao/README.md`.
- **I test**, nuovi: `IvaoTrackerWithoutVidTests` (unità: le pagine, la pagina vuota, l'errore a metà, il limite, oltre 200, i
  doppioni, la regola, le parole, il client delle fixture, e il client vero con un IVAO recitato dal test: la domanda senza
  `userId`, e la pagina che non arriva che dà `null` — con `ReadAsync` al posto di `ReadOrNothingAsync` il test cade) e
  `IvaoApiTimeoutTests` (unità: il tempo per tentativo, e la regola del gestore). **Nessun test esistente cambiato**; i test dei tour,
  d'integrazione e di unità, restano com'erano.
- **Nessuna riga** del modulo dei tour né di un altro modulo.

## Da portare nel piano

- **§10, riga «Eventi (M4b)»**: nella colonna *Auth*, il token dell'applicazione (`client_credentials`) basta, misurato il 30
  settembre 2026 (E10a); la forma nel codice è `IvaoSessionQuery` con il VID facoltativo, `ConnectionType` e il `Limit` di chi
  chiama; **la pagina con l'ultima riga di una domanda per aeroporto costa a IVAO ~10,5 s**, e le domande lente in parallelo ricevono
  504 dal gateway (15 s): una alla volta.
- **§10, riga «Tour (M2): il volo del PIREP»**: pagine da 100, non più 50; un IVAO che non risponde è «tracker non disponibile», non
  un errore.
- **§10, il paragrafo sotto la tabella** (`IvaoApiClient` con il circuit breaker): un tentativo aspetta 20 secondi, più del gateway
  di IVAO; il totale resta 30.
- `09-design-m4.md` §9.1 («Da misurare nella fase del nucleo»): misurato qui. Il design non si tocca; la misura vale da questa nota.
