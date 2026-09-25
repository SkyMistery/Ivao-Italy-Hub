# Le postazioni ATC da IVAO e il tipo `exam` (A2)

**Data:** 25 settembre 2026 — fase A2 di M3, PR del nucleo
**Stato:** **scelta tecnica**, per dare forma nel codice a decisioni già prese da Carmine: le estensioni n.5 e n.6 del design
(`07-design-m3.md` §8, deciso sulla PR #121) e la nota `2026-09-25-rating-e-postazioni-dal-nucleo`, che rimanda a questa la forma
delle postazioni. Nessuna domanda nuova: le misure qui sotto hanno scelto gli endpoint, e l'unico fatto che mancava (le postazioni
militari) l'ha dato `dalberone`, che lo sa da staffista TD.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: si estendono meccanismi del nucleo che esistono già — la sincronizzazione notturna
dei dati di riferimento (`RefDataSyncJob`, come gli aeroporti del mondo in T1), una directory come `IAirportDirectory`, il seme dei
tipi del calendario — e il modulo non ne scrive una copia sua. È una PR del nucleo, prima del codice del modulo che la usa (`CLAUDE.md`
§0 regola 6); il modulo la legge in A6 (la richiesta) e in A10 (gli esami).

## 1. Che cosa si è misurato, il 25 settembre 2026, con il token vero

- **La documentazione pubblica dell'API** sta in `https://api.ivao.aero/docs/{api}-json` (la pagina `/docs` la carica così; le API
  sono `data`, `core`, `tracker`, `atc` e altre). Per le postazioni conta `data`: `/v2/ATCPositions/all` accetta `mapType`
  (`regionMap` o `regionMapPolygon`) e `loadAirport`, `/v2/subcenters/all` accetta `mapType`, e ci sono le risposte di una divisione,
  `/v2/airports/{airportId}/ATCPositions` e `/v2/centers/{id}/subcenters`. **Nessuna accetta un paese.**
- **Le risposte del mondo**, con il token dell'applicazione (`client_credentials`, lo stesso della sincronizzazione):

  | Chiamata | Righe | Corpo | Tempo | Esito |
  |---|---|---|---|---|
  | `/v2/ATCPositions/all` | 11 863 | 20,2 MB | 6–8 s | 3 su 3 |
  | `/v2/ATCPositions/all?mapType=regionMapPolygon` | 11 863 | 10,6 MB | 3–7 s | 3 su 3 |
  | `/v2/subcenters/all` | — | — | 15 s | **0 su 4**: `504`, o la connessione chiusa |
  | `/v2/subcenters/all?mapType=regionMapPolygon` | 1 497 | 12,2 MB | 4,4–5,2 s | 4 su 4 |
  | `/v2/subcenters/all?mapType=regionMap` | 1 497 | 19,0 MB | 6–7 s | 3 su 3 |

  Senza `mapType` IVAO manda i due contorni di ogni settore (`regionMap` e `regionMapPolygon`), e la risposta dei settori non esce
  prima dei **15 secondi del suo gateway**: il `504`, o la connessione chiusa a metà che A1 aveva visto «due volte su tre». Non è
  instabilità, è la misura della risposta; con un contorno solo esce in quattro secondi. `mapType` non ha un valore «nessuno», quindi
  il più piccolo è `regionMapPolygon`. Compressa (brotli) la risposta pesa un quinto; il client dell'hub non chiede compressione.
- **Le risposte di una divisione**, per l'Italia: i 7 FIR (`/v2/centers?countryId=IT`: LIBB, LIMM, LIPP, LIRO, LIRR, LIVK, LIZZ)
  danno **37 settori in 7 chiamate** (1,6 s in tutto); i 221 aeroporti del paese danno **195 postazioni in 221 chiamate**, di cui 84
  con qualcosa (23 s in tutto), tutte riuscite. **Il contenuto è lo stesso** delle risposte del mondo filtrate: gli stessi 195 e 37
  nominativi. Ma sono righe più povere: quelle per aeroporto non hanno `military`, quelle per FIR nemmeno la frequenza.
- **La forma di una riga**: `id`, `airportId` (o `centerId` per un settore), `atcCallsign` (il nome detto in frequenza, «Fiume Tower»,
  «Roma Radar», fino a 57 caratteri), `middleIdentifier`, `position` (il tipo), **`composePosition`** (il nominativo con cui un
  controllore si connette, `LIRF_TWR`, `LIRR_NE_CTR`: è `aeroporto_[medio_]tipo` in 11 861 righe su 11 863 e `FIR_[medio_]tipo` in
  tutti i settori), `frequency`, `military`, `radarRange`, i genitori (sempre vuoti) e le date. Tipi nel mondo: `TWR` 5186, `APP`
  2241, `GND` 1883, `ATIS` 1875, `DEL` 590, `DEP` 88; settori `CTR` 1394 e `FSS` 103. **Nessun nominativo sta in tutte e due le
  risposte.**
- ⚠️ **Un nominativo non è unico**: quattro compaiono due volte nel mondo, due italiani (`LIBG_APP`, `LIRE_APP`, poi `DTTF_APP` ed
  `EGJJ_S_APP`) — stesso aeroporto, stesso nome, stessa frequenza, `id` diversi, la seconda riga creata nel 2025 o nel 2026. Sono
  doppioni di IVAO della stessa stazione.
- **L'Italia**: 195 postazioni in 84 aeroporti (`TWR` 84, `APP` 61, `ATIS` 25, `GND` 20, `DEL` 5) e 37 settori (`CTR` 32, `FSS` 5).
  **128 dei 221 aeroporti italiani non hanno `centerId`**, ma nessuno di questi ha una postazione: gli 84 che ne hanno uno lo hanno
  tutti, quindi il FIR di una postazione d'aeroporto è quello del suo aeroporto. **IVAO segna 50 postazioni italiane come militari**
  (basi aeree, poligoni, i PAR «Precision», i settori `MIL`); `dalberone` conferma che **il TD fa training su alcune di queste**:
  il segno non decide niente.
- **Gli FRA** (API `core`, `/v2/fras?countryId=IT`, aperti al token dell'applicazione): 353 regole della divisione su **chi può
  connettersi** a una postazione — un rating minimo (`minAtc`), giorni e ore, eccezioni per membro (`userId`), una lista nera. È ciò
  che A1 non aveva visto quando ha scritto «nessuna postazione porta un rating»: le righe delle postazioni non lo portano, e gli FRA
  non dicono **dove si allena** un rating, ma chi si connette e quando. L'hub non li legge.

## 2. Che cosa si fa

### 2.1 La tabella `ref_ivao_atc_positions`

- **Le postazioni del mondo**, nel contesto del nucleo (migrazione additiva `AddAtcPositions`), una riga per nominativo: le due
  risposte di IVAO (postazioni degli aeroporti e settori dei FIR) nella stessa tabella, perché per chi le legge sono la stessa cosa —
  il posto dove un controllore si connette. Colonne: `callsign` (la chiave, `composePosition` in maiuscolo), `position_type` (`TWR`,
  `APP`, `CTR`…: la parola che `Rating.PositionType` del vocabolario di A1 nomina), `airport_icao` (per una postazione d'aeroporto),
  `center_id` (per un settore), `name` (`atcCallsign`), `raw_json` (la riga **senza il contorno**, che pesa quasi tutto) e `synced_at`.
  Indici su tipo, aeroporto e FIR.
- **Il mondo e non la divisione**, come gli aeroporti di T1: la sincronizzazione copia IVAO e basta, senza sapere prima quali aeroporti
  sono della divisione, e **che cosa è della divisione lo dice la directory** (§2.3), come `FirDirectory` per lo spazio aereo. Sono
  13 356 righe, meno di un terzo degli aeroporti.
- **Un nominativo che IVAO elenca due volte è una riga**, la prima che arriva: sono la stessa stazione (§1), e il nominativo è ciò che
  il training scrive (design §1.2, `position`). Una riga senza nominativo, senza tipo o senza aeroporto (o FIR), o più larga delle
  colonne, si salta: una riga strana di IVAO non deve far fallire la notte di tutto lo snapshot.
- **`military` non diventa una colonna**: nessuno lo legge, perché il TD allena anche su postazioni militari (§1). Resta nel JSON grezzo,
  come la frequenza.

### 2.2 La sincronizzazione

- **Due chiamate del mondo**, `/v2/ATCPositions/all?mapType=regionMapPolygon` e `/v2/subcenters/all?mapType=regionMapPolygon`, in
  `IIvaoApiClient.GetAtcPositionsAsync`, che risponde con le due metà (postazioni degli aeroporti, settori), come
  `GetFlightPlanVocabulariesAsync` risponde con i due vocabolari. Il lettore delle righe (`IvaoAtcPositionReader`) è unico per il
  client vero e per quello delle fixture, come quello del tracker.
- **Una metà che non arriva è vuota, mai un'eccezione**: un errore di trasporto — il gateway che taglia a quindici secondi è uno — si
  scrive nel log e vale «nessuna risposta». Oggi un'eccezione in una chiamata di riferimento fa fallire tutto il giro (centri,
  aeroporti, aerei); per le postazioni, le più pesanti e le ultime arrivate, no. Il comportamento delle altre chiamate non cambia.
- **Nel giro notturno, dopo gli aerei e nello stesso salvataggio**: il giro resta tutto o niente. Upsert per nominativo con il
  rilevamento delle modifiche spento dentro il ciclo, come gli aeroporti. **Ogni metà per conto suo**: una metà vuota lascia le sue righe
  com'erano (la regola del 3 settembre, nota `2026-09-03-snapshot-ref-potatura`: una risposta vuota non pota mai); una metà piena pota
  **le sue** righe che non elenca più — le postazioni d'aeroporto la prima, i settori la seconda.
- **L'esito conta le postazioni**: un giro che rinfresca centri e aeroporti e non riceve le postazioni è `partial`, come uno senza
  aeroporti; il messaggio dice quante postazioni e quanti settori. `RunAsync` risponde ancora con centri e aeroporti: il test che lo legge
  è del maintainer.
- ⚠️ **Il membro nuovo di `IIvaoApiClient` ha un'implementazione predefinita** (due metà vuote). Tre doppi nei test del maintainer
  implementano l'interfaccia (`WeatherTests`, `PirepTests`, `RefDataSyncTests`) e la regola 3 di `CLAUDE.md` §0 vieta di toccarli; un
  client che non sa delle postazioni risponde «nessuna», e il giro tiene lo snapshot. È la prima implementazione predefinita
  dell'interfaccia: la sola alternativa era una seconda interfaccia dello stesso client (§3).
- **All'avvio** la sincronizzazione parte solo su un'installazione nuova (senza centri): su una che gira già, **le postazioni arrivano
  con il primo giro notturno** dopo il rilascio. Il modulo le legge da A6 in poi.

### 2.3 La directory

- **`IAtcPositionDirectory.ForRatingAsync(Rating)`**, in `Core/Ivao/`: le postazioni **della divisione** del tipo su cui il vocabolario
  allena quel rating (`Rating.PositionType`: ADC `TWR`, APC `APP`, ACC `CTR`), ordinate per nominativo; **nessuna** per un rating senza
  tipo (un pilota, un SEC). Ognuna con nominativo, nome, aeroporto e **FIR** — per una postazione d'aeroporto il FIR del suo aeroporto —,
  che è ciò che la richiesta copia sul training (design §1.2) e ciò che serve ai capi FIR (A11).
- **Della divisione** vuol dire ciò che vuol dire per lo spazio aereo (`FirDirectory`): una postazione d'aeroporto se il suo aeroporto
  è del paese della divisione (`division.countryId`), un settore se il suo FIR è uno dei FIR della divisione. Senza il filtro
  risponderebbe il mondo (⚠️ di `08`, A2).
- **Le militari restano**, e **`hiddenPositions` lo applica il modulo** (design §1.6, Carmine n.5): il TD toglie le postazioni su cui
  non fa training, militari o no. La directory non conosce le impostazioni di un modulo.
- **Nessun endpoint**: la richiesta chiede dal suo (A6), e l'elenco di un tipo in una divisione sta in una pagina — in Italia 84 `TWR`,
  59 `APP` senza i doppioni, 32 `CTR`. Nessuna cache: si legge a ogni richiesta, e cambia una volta per notte.
- **Il modulo non nomina un tipo di postazione**: chiede per un rating, e la risposta non porta il tipo.
- **Un tipo per rating**, quello del vocabolario di A1 (ADC `TWR`, APC `APP`, ACC `CTR`), che sostituisce di proposito l'elenco
  della prima stesura del design (ADC anche su `DEL` e `GND`, APC anche su `DEP`): `dalberone` l'ha confermato di nuovo il 25
  settembre, dopo la revisione di #128. Se un giorno un rating si allenasse su più tipi, `Rating.PositionType` diventerebbe un
  elenco e la directory chiederebbe più tipi: un cambio del nucleo, con la sua nota.

### 2.4 Il tipo `exam` e il seme

- **`exam`** in `seed/calendar-kinds/kinds.json`, rosso, subito dopo `training` (`sort` 25), con la sua chiave
  `seed.calendarKinds.exam` in `locales/*/seed.json` («Exam», «Esame»). Colore e posto si cambiano dal back office
  (`/staff/admin/calendar-kinds`), come per gli altri cinque.
- ⚠️ **Verificato in apertura, come chiedeva `08`**: `ContentSeeder.SeedCalendarKindsAsync` aggiungeva il tipo senza guardare la tabella,
  e `cms_calendar_kinds.key` ha un **indice univoco**: un `exam` scritto a mano dal back office prima di A2 avrebbe fatto fallire
  l'avvio. Ora il seeder **salta la chiave che esiste già** — scritta a mano, con i colori di chi l'ha scritta — **e la ricorda**, come
  ricorda quella che semina, e lo dice nel log. Un test lo fissa.
- **Il seme si ricorda chiave per chiave**, quindi `exam` arriva anche a un database già avviato con gli altri cinque, al primo avvio
  dopo il rilascio.

### 2.5 Le misure diventano fixture

- `tools/record-ivao-fixtures.mjs --positions` chiede le risposte del mondo **con `mapType=regionMapPolygon`**: senza, la risposta dei
  settori non arriva più (§1). Toglie il contorno come prima.
- **Le fixture del banco**, `atc-positions-world.json` e `subcenters-world.json`: le postazioni degli aeroporti del banco (LIRF, LIMC,
  LIBD, LFPG) e di LIBG, che IVAO elenca due volte, e i settori dei loro FIR (LIRR, LIMM, LIBB, LFFF). Le legge il client delle fixture:
  il banco e2e e i test d'integrazione hanno postazioni vere, e una divisione straniera da escludere. Le due di A1 (`-sample`) restano
  per i test del vocabolario.

## 3. Alternative scartate

| Alternativa | Perché no |
|---|---|
| Le risposte di una divisione, per FIR e per aeroporto | lo stesso contenuto con 228 chiamate per l'Italia, e **una per ogni aeroporto** di chi forka (migliaia per un paese grande), dove due bastano per tutti; e righe più povere (§1) |
| `/v2/subcenters/all` senza `mapType` | non esce prima dei 15 secondi del gateway di IVAO: quattro volte su quattro il 25 settembre |
| Solo la divisione nella tabella | la sincronizzazione dovrebbe conoscere gli aeroporti della divisione prima di scrivere; il mondo è una copia, come gli aeroporti, e la divisione la dice la directory, come per lo spazio aereo |
| L'`id` di IVAO come chiave | due sequenze diverse (postazioni e settori) con numeri che si sovrappongono; il nominativo è ciò con cui ci si connette e ciò che il training scrive |
| Le postazioni militari escluse dalla directory | il TD allena anche su alcune (`dalberone`, 25 settembre); le altre le toglie `hiddenPositions` (Carmine, n.5) |
| Gli FRA come legame postazione→rating | dicono chi si connette e quando, non dove si allena un rating; il legame resta nel vocabolario (nota di A1) |
| Una seconda interfaccia per le postazioni, per non toccare `IIvaoApiClient` | due porte per lo stesso client (`CLAUDE.md` §2: «the one typed `IIvaoApiClient`»); il membro predefinito tiene in piedi i doppi del maintainer senza toccarli |
| `ReadAsync` tollerante per tutte le chiamate | cambierebbe come fallisce oggi ogni chiamata di riferimento (un giro `failed`): non è di questa fase |
| Le postazioni salvate a parte, dopo il resto | un giro `failed` dopo aver scritto metà snapshot, proprio ciò che il ramo d'errore del job esiste per evitare |
| Chiedere la compressione al client di IVAO | un quinto dei byte, ma è il client di tutte le chiamate: non serve a far arrivare le postazioni, che arrivano in pochi secondi con `mapType` |

## 4. Che cosa si tocca

Tutto del nucleo, ed è il perché di questa nota (`core-guard`):

- **La tabella**: `Core/Ivao/IvaoAtcPosition.cs` (nuovo: la riga, quella del client e il lettore), `Core/Data/HubDbContext.cs`,
  `Core/Data/Configurations/RefSchemaConfiguration.cs`, la migrazione `AddAtcPositions` e lo snapshot del contesto del nucleo.
- **La sincronizzazione**: `Core/Ivao/IIvaoApiClient.cs`, `IvaoApiClient.cs`, `FixtureIvaoApiClient.cs`, `RefDataSyncJob.cs`.
- **La directory**: `Core/Ivao/AtcPositionDirectory.cs` (nuovo), `Core/Ivao/IvaoServiceCollectionExtensions.cs` (la registrazione).
- **Il seme**: `seed/calendar-kinds/kinds.json`, `locales/en/seed.json`, `locales/it/seed.json`, `Core/Content/ContentSeeder.cs`.
- **Le misure**: `tools/record-ivao-fixtures.mjs`, `tests/fixtures/ivao/README.md`, le due fixture nuove.
- **Test nuovi**, nessun test esistente cambiato: il lettore (unità, sulle fixture vere), la sincronizzazione e la directory
  (integrazione), il seme con un `exam` scritto a mano (integrazione).

`docs/FORKING.md` dice a chi forka che anche le postazioni si sincronizzano dal mondo.

## Da portare nel piano

- **§7, schema `ref_`**: la riga `ivao_atc_positions` — PK `callsign`; `position_type`, `airport_icao` o `center_id`, `name`,
  `raw_json`, `synced_at`; snapshot del mondo di `/v2/ATCPositions/all` e `/v2/subcenters/all`, indici su tipo, aeroporto e FIR.
- **§10, riga «Posizioni ATC»** (oggi `/atc/positions`, «verificare»): `/v2/ATCPositions/all` e `/v2/subcenters/all`, sempre con
  `mapType=regionMapPolygon` (senza, i settori superano i 15 secondi del gateway di IVAO), `client_credentials`, ogni notte con il resto
  dei dati di riferimento.
- **§9.1, riga «Dati di riferimento IVAO»**: le postazioni ATC del mondo (M3, A2), e le postazioni della divisione su cui si allena un
  rating come domanda del nucleo (`IAtcPositionDirectory`), accanto al vocabolario dei rating di A1.
- **§9.5 e §7, `calendar_entries.kind`**: `exam` è nel seme dei tipi dal rilascio di A2; il seme salta una chiave che il back office ha
  già scritto.
