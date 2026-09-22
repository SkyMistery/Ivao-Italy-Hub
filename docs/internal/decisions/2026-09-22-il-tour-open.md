# Il tour `Open`: l'obiettivo in una scheda sua, i vincoli solo lì

**Data:** 22 settembre 2026 — fase T7c di M2
**Stato:** **decisa** (Carmine, 22 settembre 2026, due domande di `06` §T7c in apertura e tre nate leggendo il codice; il resto sono
estensioni di meccanismi esistenti e scelte di forma, dette qui perché toccano il nucleo o correggono il design).
**Regola applicata:** `CLAUDE.md` §5: **(c)** per dove si scrive l'obiettivo, su quali tour valgono i vincoli, quante righe di un tipo,
il cambio di tipo e gli elenchi; **(b)** per le due domande nuove ai dizionari del nucleo e per `AircraftTypes`, già coperto.

## 1. Che cosa serviva

Il design §2.6.1 fissa i tre pezzi di un tour `Open` — un obiettivo, dei filtri per volo, delle regole di sequenza — presi da insiemi
chiusi scritti nel codice, ognuno con il suo schema di parametri. Aprendo T7c:

1. **Dove si scrive l'obiettivo** (lasciata aperta da T7b): il form del tour è generato, e il generatore non sa dire «prima il tipo,
   poi i suoi parametri» — `hidden` si decide quando lo schema si costruisce, non dal valore di un campo. I vincoli chiedono la stessa
   cosa.
2. **Se un filtro vale anche su un tour con leg** (lasciata aperta da T7b).
3. **Quante righe dello stesso tipo**: due `DistanceBetween` che non si toccano, o due `TouchesAirport` che insieme vogliono dire «ogni
   volo è LIRF↔LIMC», sono contraddizioni che «pronto» dovrebbe cercare.
4. **Un `Open` con vincoli che cambia tipo**: la scheda dei vincoli si vede solo sugli `Open`, quindi le righe resterebbero invisibili.
5. **Gli elenchi di `CollectList` e `CollectRegions`**: scritti in ogni tour, o elenchi con un nome condivisi fra i tour.

## 2. Le decisioni

1. **L'obiettivo ha una scheda sua** (Carmine), «Obiettivo e vincoli», solo sui tour `Open`: in alto l'obiettivo — si sceglie il tipo,
   poi si apre il form dei suoi parametri —, sotto la lista generata dei filtri e delle regole. Il pezzo «tipo, poi il form dei suoi
   parametri» è **uno**, e lo usano l'obiettivo e i vincoli. L'obiettivo si salva con il tour (`openGoal`, `openGoalParameters` in
   `TourWriteDto`; assenti, restano come sono, come il briefing). *Scartato*: tipo e parametri nel form del tour (campi condizionali
   dentro `SchemaForm`, un'estensione più grande, e un form già lungo).
2. **Filtri e regole di sequenza valgono solo sui tour `Open`** (Carmine). Su un tour con leg partenza, arrivo e distanza li fissano le
   leg, gli aerei li dice già `AllowedAircraft` e la sequenza il tipo. Il server rifiuta un vincolo fuori da `Open`. Un «solo VFR» su un
   tour con leg, se servirà, sarà una regola di T9. *Scartati*: tutti i tipi (molti filtri si contraddicono con le leg, e «pronto»
   dovrebbe provarli contro ognuna); `Open` e `Distance`.
3. **Una riga per tipo** (Carmine), rifiutata sul campo la seconda; fa eccezione **`MinFlightsAt`**, una per aeroporto («3 su LIRF, 2 su
   LIMC»). Per dire «LIRF o LIMC», **`TouchesAirport` prende un elenco** di aeroporti e non uno solo (corregge il design §2.6.1).
   *Scartato*: righe libere che si sommano (le contraddizioni da cercare crescono con le righe).
4. **Un `Open` con vincoli non cambia tipo** (Carmine), come un `Container` con sottotour: si tolgono prima. **L'obiettivo invece si
   svuota da solo** quando il tipo non è più `Open`: è una colonna del tour, e fuori da `Open` non vuol dire niente. *Scartato*: il
   cambio che passa e «pronto» che protesta (per togliere i vincoli bisognerebbe tornare a `Open`).
5. **Gli elenchi si scrivono nel tour** (Carmine): un campo che si ripete, dentro i parametri dell'obiettivo. Un template li porta con
   sé, quindi «le capitali d'Europa» si scrive una volta, nel template. *Scartato*: elenchi con un nome condivisi (un meccanismo nuovo).

## 3. Che cosa si estende, e perché

- **`IAirportDirectory.KnownCountriesAsync`** e **`IFirLocator.KnownAsync`** (nucleo, caso b): un parametro che nomina un paese o un FIR
  va verificato sul server come si verifica un aeroporto, e il modulo non legge le tabelle `ref_` da sé. Un paese è il `countryId` degli
  aeroporti (ISO a due lettere, come lo dà IVAO); un FIR è un confine di VATSpy (T1), del mondo e non solo della divisione. Due domande
  in più a due dizionari che esistono: nessun servizio nuovo.
- **`AircraftTypes` non è un filtro** (caso b): gli aerei ammessi di un tour, tipi e gruppi, sono già `AllowedAircraft` (design §1.5,
  T7a), e valgono anche su un `Open`. Resta **`AircraftCategory`** (la categoria di scia, `L`, `M`, `H`, `J`), che `AllowedAircraft` non
  dice. Corregge il design §2.6.1.

## 4. Le scelte piccole, dette qui

- **I parametri** sono un oggetto JSON (`open_goal_json`, `fo_tour_constraints.parameters_json`) con **solo** i campi del tipo: il server
  li normalizza (maiuscole, ognuno una volta) e butta il resto. Il catalogo dei tipi e dei loro campi è uno, in C#
  (`OpenCatalog`), e dice per ogni campo tipo, obbligatorio e limiti; i form sono zod, come sempre (design M0 §7.5): lo schema dice i
  campi, le regole le dice il server, sul campo (`openGoalParameters.airports`, `parameters.minNm`).
- **`CollectRegions`** prende paesi **o** FIR, mai tutti e due; `CollectList` e `CollectRegions` hanno un «quanti» facoltativo, al più
  quanti ne ha l'elenco (vuoto: tutti).
- **`DistanceBetween`** vuole almeno uno dei due estremi, e il minimo non sopra il massimo.
- **«Pronto» di un `Open`**: un obiettivo; i suoi parametri ancora buoni (controllo puro, senza i dizionari: un aeroporto sparito dallo
  snapshot lo dice la scrittura); nessuna leg (T7a); `Eastbound` con `Westbound` è una contraddizione (è l'unica che si vede senza
  volare). I parametri dei vincoli si provano alla scrittura.
- **I vincoli sono righe figlie del tour** come hub e callsign (`ITourChild`, `BeforeAuthorize`, la cura del tour, `TourSaving` li fa
  seguire), con lista e form generati e una pagina per il form (`/staff/tours/{id}/constraints/{…}`). **Il tipo di un vincolo non cambia**
  dopo la creazione: un altro tipo è un'altra riga. Un vincolo di un template chiede `Tours.ManageTemplates`, come un callsign.
- **La copia** (template → tour, tour → template) porta obiettivo e vincoli.
- **La verifica su un volo** resta di T11.
