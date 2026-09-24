# IVAO Division Hub — Design di M2 (il modulo dei tour)

> Documento **interno** (italiano). Fonte di verità: `00-piano-di-progettazione.md` (versione 0.79, che porta le decisioni di questo documento).
> Ingresso: **`decisions/2026-09-14-requisiti-dei-tour.md`** — i requisiti decisi da Carmine e dallo staff
> FOD. Questo documento li trasforma in modello, flussi, permessi, schermate e fasi; **non li ridiscute**.
> Dove resta una scelta aperta è segnata **⚖️** e raccolta in §15.
> Le fasi di implementazione si scrivono nella parte C di `06-piano-implementazione-m2.md` **dopo** la
> revisione di Carmine.

**Stato:** **chiuso** il 15 settembre 2026, dopo quattro giri di revisione con Carmine. **La fase T0 è fatta** (16 settembre 2026):
sei note di decisione in `decisions/2026-09-15-*`, piano 0.79, parte C di `06-piano-implementazione-m2.md` con le fasi T1–T21. Le
ultime proposte aperte sono decise (§15.2 n.16 e n.21), e T0 ha corretto tre punti con le misure: il TAF passato c'è (§1.13),
MapLibre 6 non ha la build CSP (§8.6), le tabelle dei contatti sono `cms_` (§12). Integra la revisione di Carmine del 15 settembre (le 25 risposte
alla prima bozza e le aggiunte: aereo di riferimento e tempo stimato, cancellazione delle leg, METAR e TAF,
decollo dalla testata, tour a distanza senza leg, tutti i piani di volo, ATC proposti, contestazioni che non
bloccano, ban, richiesta di chiarimenti, code per tour, parametri nelle regole, limiti che bloccano).

---

## 0. Perimetro

### 0.1 Che cosa è «fatto»

M2 è fatta quando lo staff FOD può preparare la stagione 2027 nell'hub e i piloti possono volarla:

- il FOD crea tour di **tutti i tipi** di §2 (anche da template), con leg inserite a mano o importate da
  XLSX/CSV, regole generali e del tour con i loro parametri, errori, validatori abilitati per tour;
- un tour **pronto** esce da solo alla data di rilascio e si chiude da solo alla data di chiusura;
- un pilota vede i tour, la mappa, il suo avanzamento e il tempo stimato di ogni leg, e invia un PIREP
  scegliendo il volo dal tracker, con gli ATC contattati proposti dal sistema;
- un validatore abilitato prende il PIREP dalla coda (unica o per tour), segna gli errori con i suggerimenti
  del sistema — controlli automatici, METAR e TAF salvati, decollo dalla testata — e decide; il pilota riceve
  la mail; un rifiuto si contesta, e qualunque esito permette di chiedere chiarimenti;
- il completamento di un tour produce una **segnalazione award**, e chi ha `Awards.Assign` assegna l'award
  dal catalogo del nucleo;
- lo staff ha le statistiche dei validatori, la pagina del pilota e i ban;
- i **controlli automatici** girano su ogni PIREP e **suggeriscono** errori **dalla prima stagione**.

### 0.2 Fuori perimetro

- **Manovre obbligatorie** (touch-and-go nei tour VFR): dopo il sistema principale (requisiti §4).
- **Punti e classifiche dei tour: mai** (Carmine, 15 settembre). Esistono solo le statistiche dei validatori.
- **Online Day** (passa all'ED, M4), **eventi**, **Discord**, **Pilot Life**, **controlli di livello B/C** (rotta,
  procedure, Eurocontrol).
- **Import dello stato dei tour attuali**: il sistema entra in uso nel 2027.
- **Il pacchetto e lo staging Plesk**: metà (b) di M2 (piano §13), non in questo documento.
- **La pagina delle Virtual Airlines**: rimandata.

### 0.3 Che cosa M2 non rimette in discussione

- **I meccanismi del nucleo** (`CLAUDE.md` §2, piano §16): `MapCrud`, lista e form generati, l'unico
  handler, il filtro globale, `IOwnedByDepartment` a insieme (H2), i grant a una posizione (H1),
  `IProjectable`, il servizio notifiche, l'unico `IIvaoApiClient`, i blocchi Data, le dashboard (D1–D3).
- **I moduli fuori dai dipartimenti**: sezione `/staff/tours`, «a cura di» con il FOD sempre presente.
- **Chi gestisce il modulo** (piano 0.77): coordinator e assistant FOD tutto, per grant a una posizione.
- **I dati condivisi con vIPI** (piano 0.78): l'archivio delle sessioni ATC si legge da una vista.

### 0.4 I nomi

| Che cosa | Nome |
|---|---|
| Chiave del modulo (`IModule.Key`, `division.json`, storia delle migrazioni) | `flightops` |
| Progetto .NET | `IvaoHub.Modules.FlightOps` |
| Frontend | `web/src/modules/flightops/` |
| Prefisso delle tabelle | `fo_` |
| API | `/api/flightops/...` |
| Area dei permessi | `Tours` (`Tours.Edit`, `Tours.Validate`…) |
| Rotte pubbliche | `/tours`, `/tours/{slug}` |
| Rotte dello staff | `/staff/tours/...` |
| Namespace i18n | `flightops` |

### 0.5 Che cosa si prende da Toursystem e da vIPI, e che cosa no

Toursystem (`D:\Programmazione\IVAO_Test\Ivao Italy Toursystem`) è stato letto il 13 settembre; vIPI il 14 e il
15. Si prende il **dominio** e ciò che è **misurato**, non l'architettura.

| Si prende | Da | Dove qui |
|---|---|---|
| Chi ha un interesse non decide (PIREP) | ADR-033 | §7.3 |
| Decisione con errori e nota | ADR-008 | §4.3 |
| Il pilota non vede i contatori | ADR-009 | §3.8 |
| La soglia suggerisce, decide l'umano | ADR-010, ADR-035 | §4.3 |
| Tre esiti per un controllo (superato, non superato, **non disponibile**) | ADR-011 | §6.2 |
| PIREP come form strutturato precompilato dal tracker | ADR-013 | §3.2 |
| Esenzioni che dichiarano quali controlli ammorbidiscono | ADR-014 | §3.3 |
| Regole congelate sul PIREP | ADR-015 | §5.4 |
| Sessione del tracker **rivendicata** da un solo PIREP | ADR-017 | §3.4 |
| Coordinate congelate alla scrittura della leg | ADR-024 | §1.4 |
| Una tabella di prestazioni degli aerei curata dallo staff | ADR-025 | §1.5 |
| Numeri del regolamento come parametri, default generale e override del tour | ADR-027 | §1.7 |
| Sottotour come tour con un padre, profondità 1 | ADR-032 | §2.7 |
| Segnalazione di un problema su una leg | ADR-037 | §3.11 |
| Deviazione in più tratti, motivata | ADR-042 | §3.4 |
| Import che non cancella da solo | ADR-051 | §8.4 |
| Distanza GCD (`GreatCircle.cs`, con i test) | codice F1 | §1.4 |
| Controlli per la pubblicazione | `TourPublicationChecker` | §1.2 |
| Il catalogo dei controlli di livello A | ARCHITETTURA App. A | §6.4 |
| **Catena del meteo**: NOAA per METAR e TAF, ripiego del METAR su IVAO (`/v2/airports/{icao}/metar`, **minuscolo**) poi VATSIM; il TAF non ha ripiego | vIPI `NoaaWeatherClient`, `IvaoMetarClient`, `VatsimMetarClient` (misurati l'8 set 2026) | §1.13 |
| Campi degli aeroporti e delle piste IVAO (IATA, coordinate; per ogni testata latitudine, longitudine, prua, lunghezza) | Toursystem `IvaoAirportMapper` | §1.12 |

| Non si prende | Perché |
|---|---|
| Stagione e clonazione | sostituite dai **template di tour** (solo impostazioni e regole) |
| Quattro occhi sulla pubblicazione del tour | «se è segnato pronto viene pubblicato» |
| Stato del tour con sette valori | lo stato è **derivato dalle date** (§1.2) |
| Tabelle di traduzione, `.resx` | `Localized<T>` e `locales/` |
| Ruoli propri (tour manager, validator, award officer) | permessi `Tours.*` e grant (§7) |
| Registro dei punti, classifiche | mai (§0.2) |
| Prima stagione dei controlli in sola informazione (ADR-036) | il FOD adotta i suggerimenti **dalla prima stagione** |
| Pannello HQ, poller ATC, Online Day, eventi | fuori o altrove |

---

## 1. Il modello

Tutte le tabelle stanno in `FlightOpsDbContext : ModuleDbContext`, con la sua `__EFMigrationsHistory_flightops`.
Nessuna FK verso il nucleo: `vid`, `icao`, `media_id`, `award_id` sono colonne non vincolate.

### 1.1 Chi possiede una riga

- Le righe che lo staff scrive (tour, leg, regole, errori, template, profili degli aerei) implementano
  **`IOwnedByDepartment`** con **`OwnerDepartmentMask`** (il FOD c'è sempre, H2), **`IAuditable`** e
  **`[Audited]`**: l'audit registra prima e dopo di ogni scrittura, quindi non esiste una tabella delle modifiche.
  **Una riga figlia di un tour** (leg, hub, rotazione, vincolo) **copia dipartimento e maschera del tour** a ogni scrittura e li
  segue quando cambiano (Carmine, 18 settembre, nota `2026-09-18-le-leg-dei-tour`): l'unico handler la legge come il tour.
- Le righe che il pilota scrive (PIREP, segnalazioni) sono **`ISubmittedByMembers`**.
- **`IVisible`**: un tour visibile al pubblico è `Public`; bozze, template e tour nascosti `Staff`; un PIREP è
  `Members`, ristretto al suo pilota o a chi ha i permessi di §7.

### 1.2 Il tour — `fo_tours`

| Colonna | Tipo | Note |
|---|---|---|
| `id`, `slug` | | `slug` unico fra i non template: `/tours/{slug}`; anche un sottotour ha il suo (Carmine, 21 settembre) |
| `is_template` | bool | §1.10 |
| `kind` | enum | `Sequential`, `Free`, `Hub`, `SequentialChosenStart`, `Distance`, `Open`, `Container` (§2) |
| `parent_tour_id` | long? | solo per un **sottotour**; il padre è un `Container` che non è un template, scelto alla creazione e mai cambiato |
| `required_subtours` | int? | solo `Container` |
| `required_nm` | int? | solo `Distance` |
| `open_goal`, `open_goal_json` | enum?, json | solo `Open`: obiettivo e parametri (§2.6.1) |
| `title`, `summary` | `Localized<string>` | |
| `briefing_json` | BlockDocument | testo ricco, stesso editor e renderer |
| `cover_media_id` | long? | foto di sfondo del riquadro, dalla media library (§1.14) |
| `banner_media_id` | long? | banner della pagina del tour, dalla media library (§1.14) |
| `status` | enum | `Draft`, `Ready` — nel codice `PublishStatus.Draft` e `Published`, così la regola della bozza è quella dell'interceptor (T6a, nota `2026-09-16-i-tour-nel-back-office`) |
| `is_hidden` | bool | nascosto (§1.2.2) |
| `show_preview` | bool | un tour pronto è visibile al pubblico **prima** del rilascio, come anteprima (risposta 1) |
| `release_at`, `close_at` | UTC | su un sottotour, quelle in vigore: le sue, dentro il periodo del padre, o quelle del padre (Carmine, 21 settembre, nota `2026-09-21-la-forma-dei-tour`) |
| `release_from_parent`, `close_from_parent` | bool | solo un sottotour: la data è del padre, **copiata** a ogni scrittura e **seguita** quando il padre la cambia, come la maschera |
| `report_window_days` | int | X: giorni per inviare il PIREP **e** finestra di ricerca nel tracker |
| `progression` | enum | `FlyAhead` o `WaitForValidation` |
| `hub_rotation_order` | enum? | solo `Hub`: `Fixed` (rotazioni nell'ordine del tour) o `Free` (a scelta dentro l'hub) (risposta 10) |
| `requires_procedures` | bool | SID, STAR e IAP obbligatori nel PIREP (risposta 4, §3.2) |
| `daily_leg_limit` | int? | obbligatorio se il limite di divisione è spento (§3.7) |
| `allowed_aircraft_json` | json | aerei consentiti: tipi ICAO e gruppi di aerei (§1.5), `{ types, groupIds }`; vuoto = tutti. **Senza** la spunta «anche le varianti» (Carmine, 16 settembre) |
| `min_pilot_rating` | int? | rating pilota minimo per inviare PIREP (§3.2); vuoto = nessuno |
| `reference_aircraft_icao` | string? | l'aereo di riferimento per il tempo stimato (§1.5) |
| `award_id` | long? | **un solo award per tour, sempre** (Carmine, 15 settembre); solo su un tour senza padre. Gli award a più livelli sono di eventi e training |
| `row_version`, audit, `owner_department_mask` | | |

#### 1.2.1 Lo stato si calcola dalle date

Nessun job pubblica o chiude un tour:

| Stato visto | Condizione | Chi lo vede |
|---|---|---|
| Bozza | `Draft` | staff |
| In arrivo | `Ready`, `now < release_at` | staff; pubblico se `show_preview` (senza il tasto del report) |
| Aperto | `Ready`, `release_at ≤ now ≤ close_at` | tutti |
| In chiusura | `Ready`, `close_at < now ≤ close_at + report_window_days`: risulta chiuso, ma accetta PIREP di voli con decollo `≤ close_at` | tutti |
| Chiuso | dopo | tutti, finché la conservazione non lo toglie (§10) |

Un tour su due anni è un tour con `close_at` nell'anno dopo. **Ricerca e calendario** seguono lo stato: un tour pronto ma non ancora
visibile proietta per lo staff, e al rilascio un **job del modulo lo riproietta senza scriverlo** (`TourReleaseJob`, ogni quarto d'ora;
Carmine, 16 settembre, nota `2026-09-16-i-tour-nel-back-office`). Il job non pubblica niente: lo stato resta delle date. **Segnare «pronto»** passa dai controlli di
pubblicazione: titolo e riassunto in tutte le lingue della divisione, almeno una leg (tranne `Container` e
`Open`), aeroporti noti in `ref_`, `release_at < close_at`, date delle leg dentro il periodo,
vincoli di tipo (§2), limite giornaliero se quello di divisione è spento (§3.7), un profilo per l'aereo di
riferimento **se il tour ne indica uno** (non è obbligatorio, §1.5), un `Container` con almeno due sottotour e `required_subtours` non oltre il loro numero.

**Il tipo si blocca appena il tour è pubblico** (Carmine, 15 settembre): dal momento in cui è visibile (rilascio, o anteprima
con `show_preview`), `kind` non si cambia più, anche senza PIREP. Prima del rilascio, tornando in bozza, si può cambiare.

#### 1.2.2 Cancellare, nascondere, spostare la chiusura (risposta 16)

- **Un tour senza PIREP** si **elimina** (`Tours.Delete`), in qualunque stato: sparisce con leg, regole e hub.
- **Un tour con almeno un PIREP non si elimina mai.** Si può solo **nascondere** (`is_hidden`, `Tours.Edit`):
  - **non lo vede più nessuno fuori dallo staff** (Carmine, 15 settembre): sparisce da `/tours`, dai blocchi, dalla
    ricerca, dal calendario, e anche dalla pagina e dai PIREP dei piloti che lo avevano iniziato; nessuno può inviare
    PIREP;
  - lo staff lo vede ancora nel back office, e i PIREP già in coda si validano normalmente (le mail dell'esito arrivano
    comunque al pilota);
  - il tour arriva alla sua **naturale scadenza**, e poi la conservazione (§10) lo toglie.
- **La data di chiusura si può cambiare** anche a tour aperto, ma la nuova data dev'essere **almeno
  `2 × report_window_days` giorni da oggi**, così nessun pilota si trova il tour chiuso sotto i piedi.

### 1.3 Hub e rotazioni — `fo_hubs`, `fo_rotations`

- `fo_hubs`: `tour_id`, `icao`, `sort`. Solo `Hub`, mai su un template; un aeroporto è hub di un tour una volta.
- `fo_rotations`: `tour_id`, `hub_id`, `sort`, `size` (2, 4 o 6; il controllo di pubblicazione verifica che
  abbia esattamente `size` leg, parta dall'hub e ci torni — non la continuità fra le leg di mezzo). Una rotazione con tutte le leg
  ritirate non conta più. **Un hub con rotazioni e una rotazione con leg non si eliminano** (T7b).
- Una leg dice la sua rotazione (`rotation_id`); il suo posto nella rotazione (`seq_in_rotation`) **lo tiene il server**
  dall'ordine delle leg, come il numero (T7b). Ogni leg normale di un tour `Hub` sta in una rotazione.
- **Il collegamento fra hub è una leg** con `kind = HubConnection`: conta come una leg normale. Due hub con
  una leg di collegamento sono **collegati**, senza sono **liberi**.

### 1.4 La leg — `fo_legs`

| Colonna | Note |
|---|---|
| `tour_id`, `number` | ordine nel tour, unico per tour: lo tiene il server, che rinumera tutto il tour a ogni inserimento o eliminazione, e non l'indice (MariaDB controlla un indice unico riga per riga, e uno spostamento collide a metà; T7a) |
| `kind` | `Normal` o `HubConnection` |
| `rotation_id`, `seq_in_rotation` | per `Hub` |
| `departure_icao`, `arrival_icao` | |
| `departure_lat/lon`, `arrival_lat/lon` | **congelate alla scrittura** dagli aeroporti `ref_` (ADR-024) |
| `distance_nm` | GCD calcolata dal server |
| `callsigns_json`, `flight_numbers_json` | i callsign e i numeri di volo **suggeriti**, dei voli reali: **più d'uno** quando la tratta si vola più volte al giorno (Carmine, 22 settembre 2026, T8; al massimo 24). Informativi: il vincolo è in §1.6. Le vecchie `real_callsign`, `flight_number` non si scrivono più e cadono in una release successiva |
| `aircraft_json` | tipi ICAO della leg; vuoto = quelli del tour |
| `release_at` | rilascio proprio, facoltativo |
| `retired_at`, `retired_reason` | §1.4.1 |
| `change_reason` | obbligatorio quando si modifica una leg che ha PIREP: finisce nell'audit |

Le comodità dell'editor («duplica», «segue», «chiudi tour») non si memorizzano: compilano la leg nuova (§8.4).

#### 1.4.1 Che cosa succede quando si toglie una leg

Tre casi, decisi dal server e non dall'editor:

| Situazione | Che cosa succede |
|---|---|
| **La leg non ha PIREP** (in qualunque stato del tour) | si **elimina davvero**, e **le leg dopo di lei si rinumerano** (Carmine, 15 settembre). Si può sempre: un PIREP punta alla leg per identificativo, non per numero, quindi rinumerare non tocca nessun report; la pagina del pilota mostra il numero attuale con partenza e arrivo |
| **La leg ha PIREP** | non si elimina: si **ritira** (`retired_at`, `retired_reason` obbligatorio) |
| **Il tour ha PIREP ma la leg no** | come il primo caso: si elimina |

Una leg **ritirata**:

- **non si vola più**: sparisce dalle leg volabili e il form del PIREP la rifiuta;
- **sparisce dalla parte pubblica** (Carmine, 15 settembre): non compare più né nella pagina del tour né sulla mappa dei
  piloti; resta visibile solo **nell'editor del tour**, sulla sua mappa, con il motivo. Nella pagina del pilota un PIREP
  su una leg ritirata mostra partenza e arrivo, con la dicitura «leg non più nel tour»;
- **i PIREP già inviati restano e si validano normalmente**: un pilota che l'ha volata prima del ritiro non
  perde niente;
- **non conta più per il completamento**: «tutte le leg» vuol dire tutte le leg **non ritirate**. Chi l'aveva già
  accettata la tiene nel suo storico, ma non gli serve;
- **in un tour in sequenza viene saltata**: la prossima leg di chi era fermo lì è quella dopo;
- **in una rotazione non si ritira da sola** (Carmine, 15 settembre): si ritira **la rotazione intera**, con tutte le sue
  leg, e se ne crea una nuova. Chi aveva iniziato la rotazione ritirata la trova tolta dal suo percorso e vola la nuova;
- **si può ripristinare** (`retired_at = null`), con un motivo, finché il tour non è chiuso.

Nell'**import** (§8.4) la modalità «sostituisci» applica le stesse regole: elimina le leg assenti senza PIREP e
ritira quelle con PIREP, mostrando la differenza prima di applicare. In tutte e due le modalità una leg ritirata che il file
nomina **torna nel tour**, con il motivo dell'import e non in un tour in chiusura o chiuso (Carmine, 22 settembre 2026, T8).

### 1.5 Aerei, prestazioni e tempo stimato

- **I tipi di aereo vengono da IVAO** (estensione del nucleo n.11): una tabella di riferimento del nucleo
  `ref_ivao_aircraft` (codice ICAO, costruttore, modello, categoria di scia, varianti), sincronizzata come gli aeroporti
  da **`GET /v2/aircrafts/all`**, con `/v2/aircrafts/{icaoCode}`, `/v2/aircrafts/{aircraftId}/variants` e
  `/v2/aircrafts/manufacturers` (endpoint mostrati da Carmine dalla documentazione IVAO il 15 settembre; la forma delle
  risposte va misurata con il token vero nella fase che li usa). Gli stessi endpoint danno
  **`/v2/aircrafts/equipments`** e **`/v2/aircrafts/transponderTypes`**: sono il vocabolario delle lettere di
  equipaggiamento e dei transponder, e il controllo `equipment` (§6.4) li legge da lì invece di tenerne un elenco suo.
- **Le prestazioni le inserisce il FOD**: `fo_aircraft_profiles` (`icao_type`, `cruise_tas_kt`, `note`), una lista
  e un form generati. Un profilo vale per tutti i tour.
- ~~**Tipi e varianti** (Carmine, 15 settembre): il tour sceglie per ogni tipo se valgono **anche le varianti**.~~ **Corretto il 16
  settembre** (Carmine, apertura di T6; nota `2026-09-16-i-tour-nel-back-office`): misurato in T1, le varianti di IVAO sono livree e motori
  dello **stesso** tipo (`A320w`, `A320CFM`), non i tipi imparentati. Il tour ammette **tipi e gruppi**: un tour easyJet che vuole A320 e
  A20N usa un gruppo, un tour Volotea elenca A319 e A320. Il controllo `aircraft` e il blocco all'invio leggono lo stesso elenco.
- **Gruppi di aerei**: `fo_aircraft_groups` (`name` tradotto, tipi ICAO), definiti dal FOD — «Bizjet», «Airliner», «Turboelica»,
  «Aerei storici» — e usabili dovunque si scelgono aerei: aerei consentiti di un tour o di una leg, compresi i tour `Open` (che non
  hanno un filtro `AircraftTypes` a parte: nota `2026-09-22-il-tour-open`). Cambiare un gruppo cambia tutti i tour che lo usano ⚖️ (come per le velocità: si calcola a ogni lettura).
- **L'aereo di riferimento è facoltativo** (`reference_aircraft_icao`, Carmine, 15 settembre): si indica **solo se si
  vogliono dare ai piloti le durate indicative**. Con l'aereo, ogni leg mostra il suo **tempo stimato** e la pagina del
  tour mostra il **totale** («la somma degli air-time è stimata in xx ore xx minuti»), così un pilota sa quanto dovrà
  volare prima di iniziare. Senza, niente stime. La formula:

  **minuti = 60 × GCD × (1 + k) / velocità + c**

  con `k` e `c` nelle impostazioni (§1.11). **Proposta**: `k = 5 %`, `c = 20 minuti`. **Tarato in T18** (Carmine, 24 settembre; nota
  `2026-09-24-i-controlli-sulle-tracce` §4.2): **`k = 5 %`, `c = 15 minuti`** — sul tempo in volo di 14 voli del corpus i 20 minuti
  sovrastimavano di 4 minuti in media, i 15 di meno di uno.

  **Perché una parte fissa e non solo una percentuale** (la domanda di Carmine: «+20 %, o proporzionale alla
  lunghezza?»). Quello che una rotta aggiunge al volo in crociera ha due nature diverse:
  - **salita, discesa, avvicinamento e rullaggio** costano più o meno lo stesso tempo su una rotta di 200 NM e su
    una di 2000: sono una **parte fissa**;
  - **vento, rotta non diritta e velocità non costante** crescono con la distanza: sono una **percentuale piccola**.

  Una percentuale sola sbaglia agli estremi: il 20 % è poco su una tratta corta e troppo su una lunga. Una
  percentuale che scende con la lunghezza approssima proprio la parte fissa, ma con un numero in più da tarare.
  Esempio con un A320 a 450 kt:

  | GCD | Solo +20 % | 5 % + 20 min | Volo tipico reale (ordine di grandezza) |
  |---|---|---|---|
  | 200 NM | 32 min | 48 min | 50–55 min |
  | 800 NM | 2 h 08 | 2 h 12 | 2 h 10 |
  | 2000 NM | 5 h 20 | 5 h 00 | 4 h 35–4 h 50 |

  ⚠️ **Corretto il 18 settembre (T7a)**: la prima stesura scriveva 4 h 40 nella seconda colonna a 2000 NM, che è la sola parte
  proporzionale (60 × 2000 × 1,05 / 450 = 280 min); con i 20 minuti fissi sono 5 h 00. Sulle tratte lunghe la coppia proposta
  sbaglia quindi più di quanto la tabella faceva credere: la taratura sul corpus deciderà.

  ~~⚖️ I due numeri si **tarano sui voli veri** del corpus di test (§13): si confronta la stima con la durata delle
  sessioni del tracker e si sceglie la coppia che sbaglia meno.~~ **Tarati in T18**: 5 % e 15 minuti (sopra).

- Il tempo stimato si mostra nella pagina del tour e nell'editor; è **solo un'informazione per il pilota**, mai un vincolo del PIREP né un controllo (Carmine, 15 settembre).
- **Si calcola a ogni lettura, non si memorizza**: GCD della leg, velocità del profilo dell'aereo di riferimento, `k` e `c`
  delle impostazioni. Così, se il FOD cambia la velocità di un profilo o i due numeri, **le stime di tutti i tour, anche
  pubblicati, cambiano da sole** (Carmine, 15 settembre), senza un job di ricalcolo.

### 1.6 Vincoli sul callsign

- `fo_callsign_rules`: `tour_id`, `leg_id?`, `mode` (`Allow`, `Deny`), `match` (`Airline`, `Exact`), `value`. Si impostano
  **per tour, per sottotour o per leg**. **Il vincolo è sulla compagnia** (Carmine, 21 settembre, nota
  `2026-09-21-la-forma-dei-tour`): `Airline` sono le tre lettere, e quello che segue lo sceglie il pilota — il callsign reale di una
  leg (§1.4) è solo un suggerimento. `Exact` è un callsign intero e **solo per `Deny`** (il Vintage Jet di Toursystem vieta quattro
  callsign Itavia). *Erano* `Prefix`, `Exact` e `Pattern`, liberi.
- **Fra i livelli** (Carmine, 21 settembre): gli `Allow` valgono dal **livello più vicino** che ne ha — leg, poi tour, poi tour
  padre —; i `Deny` di tutti i livelli si sommano e **vincono sempre**; nessun `Allow` a nessun livello, ogni compagnia. *Era*
  «l'unione di leg, tour e tour padre». Un template copia i vincoli del tour, non quelli delle leg (§1.10).
- **È un vincolo che blocca l'invio** (risposta 5, §3.2).

### 1.7 Regole, parametri ed errori — `fo_rules`, `fo_errors`, `fo_rule_errors`

- **`fo_rules`**: `tour_id?` (null = **generale**), `code` («GR4», «IR3»), `title`, `text` (`Localized<string>`,
  markdown), `amends_rule_id?`, **`check_key?`**, **`parameters_json`**, `sort`, `retired_at`.
- **I numeri del regolamento sono parametri delle regole** (ADR-027, e le richieste del 15 settembre: finestre di
  disconnessione e tempo minimo di parcheggio configurabili come regola generale **e** come regola del tour).
  Una regola generale collegata a un controllo porta i valori della divisione
  (`{"maxSingleDisconnectMinutes": 15, "maxTotalDisconnectMinutes": 25}`); una regola del tour che la **emenda** ne
  cambia uno o più: salva **solo** quelli, e gli altri li **eredita** dalla generale a ogni lettura (Carmine, 22 settembre, nota
  `2026-09-22-regole-ed-errori`). Il controllo legge i parametri dalle **regole effettive** della leg (§5.2), quindi **le soglie
  non stanno nelle impostazioni**: stanno dove il FOD le scrive e il pilota le legge (l'eccezione è la tolleranza del decollo dalla
  testata, una per il sistema, §1.11). Lo schema dei parametri e i valori di partenza sono di `CheckCatalog` (T9), che i controlli
  di T17 leggono, e il form della regola li disegna.
- **`fo_errors`**: catalogo **della divisione**. `name` (`Localized<string>`), `description` ed `examples`
  (`Localized<string>`), `category` (`Info`, `Warning`, `Dangerous`), `yearly_max` (solo `Warning`), `check_key?`,
  **`is_public`** (risposta 3), `retired_at`.
- **`fo_rule_errors`**: molti a molti. Il sistema segnala regole senza errori ed errori senza regole.
- **«Copia le regole da un altro tour»** aggiunge le regole proprie in vigore del tour, con parametri e collegamenti; quello che il
  tour ha già (lo stesso emendamento, lo stesso codice) resta e la copia lo salta (Carmine, 22 settembre).

### 1.8 Il PIREP — `fo_pireps`, `fo_pirep_flights`, `fo_pirep_errors`, `fo_pirep_events`

**`fo_pireps`**

| Colonna | Note |
|---|---|
| `tour_id`, `leg_id?`, `vid` | `leg_id` è null solo in un tour `Open` (§2.6) |
| `status`, `is_disputed` | §3.1 |
| `submitted_at`, `resubmitted_at?` | |
| `flight_rules` | `I`, `V`, `Y`, `Z`, dal piano al decollo |
| `sid`, `star`, `approach` | §3.2 |
| `atc_contacts_json` | ATC contattati: callsign, frequenza, origine `Proposed` (proposto e tenuto), `Added` (dal pilota) o `Removed` (proposto e tolto dal pilota) (§3.3; T12) |
| `atc_exemptions_json` | autorizzazioni ricevute: posizione, tipo, nota, stato all'invio (`Online`, `NotOnline`, `Unverifiable`), controlli ammorbiditi congelati (§3.3; T12) |
| `atc_archive_available` | se l'archivio ATC ha risposto all'invio: senza, una lista vuota non dice chi era online (T12) |
| `is_diversion`, `diversion_reason` | §3.4, con i motivi strutturati (`Weather`, `Technical`, `Medical`, `AtcInstruction`, `Other`) |
| `pilot_remarks` | |
| `rules_snapshot_json` | §5.4 |
| `assigned_to_vid`, `lease_until` | §4.2 |
| `decided_by_vid`, `decided_at`, `outcome` | |
| `note_to_pilot`, `staff_note`, `threshold_overridden` | |
| `row_version`, audit | |

**`fo_pirep_flights`**: una riga per tratta (due in una deviazione). `seq`, `tracker_session_id` (**unico**: una
sessione vale per un solo PIREP), `callsign`, `aircraft`, `departure_icao`, `arrival_icao`, `takeoff_at`,
`landing_at`, **`flight_plans_json`** (**tutte** le revisioni del piano della sessione, come le manda IVAO),
**`plan_at_takeoff_revision`** (quale revisione vale per i controlli). Il validatore le vede tutte (§4.3).

**`fo_pirep_errors`**: `pirep_id`, `error_id`, `category` (congelata), `suggested_by_check`, `confirmed`.

**`fo_pirep_events`**: la storia, solo in aggiunta: `from_status`, `to_status`, `by_vid`, `at`, `note`.

**Precisato in T11a** (23 settembre, nota `2026-09-23-il-pirep` §7): il PIREP porta anche la rotta su cui si giudica
(`departure_icao`, `arrival_icao`, `distance_nm`: quella della leg, o quella del volo in un `Open`), `takeoff_at` (il giorno UTC dei
limiti) e `leg_snapshot_json` (la leg com'era, §3.2 punto 6); la deviazione ha `diversion_icao` e il motivo è `diversion_reason` più
`diversion_note`. La sessione rivendicata è `fo_pirep_flights.claimed_session_id`, unica e annullabile, che il ritiro svuota.
`fo_pirep_errors`, `note_to_pilot`, `staff_note`, `threshold_overridden` nascono con chi li scrive (T13); l'esito è lo stato.

**Precisato in T13a** (23 settembre, nota `2026-09-23-la-validazione`): `fo_pireps` ha anche `queued_at` (quando è entrato in coda
l'ultima volta: l'ordine della coda), `override_reason` (il perché di una decisione contro il suggerimento, obbligatorio allora e
solo allora); `fo_pirep_errors` non ha una chiave verso il catalogo (nome e categoria si leggono dallo snapshot, quindi un errore
cancellato dal catalogo non toglie niente alle decisioni) e una decisione nuova **sostituisce** le righe della precedente. **La
traccia** di ogni volo si salva all'invio in **`fo_pirep_tracks`** (`pirep_flight_id`, `points_gzip`, `point_count`, `stored_at`):
i punti come li legge il client IVAO, compressi gzip, ~10 KB a volo; si cancella `trackRetentionDays` dopo la decisione (§10).

**Precisato in T15a** (23 settembre, nota `2026-09-23-completamento-validatori-piloti-ban` §3.2): `fo_pireps.scope_tour_id` è il tour
su cui un validatore è abilitato a prendere il PIREP — il suo, o il contenitore per un sottotour (§7.3) —, scritto al primo invio.

### 1.9 L'iscrizione — `fo_enrolments`

Il **primo PIREP iscrive** (su un sottotour, anche al `Container`). `vid`, `tour_id`, `started_at`, `completed_at?`.
**L'avanzamento non si memorizza**: si calcola dai PIREP. ~~`start_leg_id?` (per `SequentialChosenStart`, fissa),
`hub_order_json` (per `Hub`)~~ — **tolte il 23 settembre** (Carmine, apertura di T11, nota `2026-09-23-il-pirep`): la partenza è
la leg del primo PIREP non ritirato, l'hub è quello della prima leg volata; si leggono dai PIREP come il resto.

**Precisato in T15a**: `completed_at` si scrive nel salvataggio che accetta il PIREP che finisce il tour (`TourCompletion`), e
l'iscrizione proietta la segnalazione dell'award nella stessa transazione (§3.11); un `Container` si completa quando lo sono
`required_subtours` suoi sottotour, letti dalle loro iscrizioni.

### 1.10 I template di tour (risposta 2)

- Una riga di `fo_tours` con `is_template = true`, mai pubblica, senza date.
- **Copia solo impostazioni e regole**: tipo, progressione, finestra, limite, `requires_procedures`, aerei, aereo di
  riferimento, vincoli sul callsign del tour, regole del tour con parametri e collegamenti, briefing e foto ⚖️.
  **Niente leg, hub, rotazioni, sottotour.**
- Solo coordinator e assistant FOD (`Tours.ManageTemplates`).

### 1.11 Le impostazioni della divisione per i tour

In `hub_division_settings` sotto la chiave `modules.flightops.settings` (dati che il FOD cambia dall'interfaccia), attraverso il meccanismo
delle impostazioni dei moduli del nucleo (`IModule.Settings`, T5, nota `2026-09-16-impostazioni-dei-moduli`):

| Impostazione | Valore proposto |
|---|---|
| `dailyLegLimit` | 10 (null = spento, §3.7) |
| `defaultReportWindowDays` | 7 |
| `disputeWindowDays` | 7 (risposta 7: uguale per tutti i tour) |
| `rejectGraceHours` | 12 |
| `leaseMinutes` | 30 |
| `durationFactor`, `durationFixedMinutes` | 0,05 e **15** (§1.5): **configurabili dal FOD**; 0,05 e 20 confermati da Carmine il 15 settembre, `c` tarato a 15 sul corpus il 24 settembre (T18) |
| `northSouthLevelCountries` | i paesi dove i livelli semicircolari vanno nord–sud (§6.4); non cambia con l'AIRAC |
| `routeProcedurePrefixes` | le prime due lettere dei codici ICAO degli aeroporti che vogliono SID e STAR scritte nella rotta (`flightPlanForm`, §6.4); parte da `ED`, `LO`: un fatto dell'AIP, non della divisione (T17) |
| `retentionMonths`, `retentionMonthsLong` | 13 e 25 (§10) |
| `trackRetentionDays` | 90: giorni dopo la decisione (o il ritiro) in cui si tiene la traccia di un PIREP (Carmine, 23 settembre, T13a; §10) |
| `thresholdToleranceMeters` | 150 (§6.4), uno per tutto il sistema; **tenuto dopo la taratura sul corpus** (T18: dodici decolli entro 100 m, tre oltre 250) |
| `weatherRetentionDays` | la finestra massima dei tour aperti (§1.13) — **non è un'impostazione** (T16, Carmine 24 set 2026): la `report_window_days` più lunga dei tour aperti o in chiusura, `defaultReportWindowDays` se non ce n'è |

Le **soglie dei controlli** non stanno qui: sono parametri delle regole (§1.7).

**Chi le cambia**: solo chi ha `Tours.ManageSettings`, cioè **FOC e FOAC** (e HQ e superadmin). Vale in particolare per la
tolleranza del decollo dalla testata e per i due numeri del tempo stimato (Carmine, 15 settembre): se in esercizio il FOD
si accorge che non vanno, li cambia senza una release.

### 1.12 Aeroporti e piste di tutto il mondo (estensione del nucleo n.4)

- Il job di sincronizzazione legge **`/v2/airports/all`** invece degli aeroporti del paese.
- `ref_ivao_airports` guadagna `iata`, `latitude`, `longitude` (migrazione additiva). Nuova `ref_ivao_runways`
  (`airport_icao`, `runway`, `latitude`, `longitude` **della testata**, `bearing`, `length`, `width`), da
  `/v2/airports/{icao}/runways` (i campi li legge già Toursystem). ⚖️ Le piste di tutto il mondo sono molte
  chiamate: si scaricano **per gli aeroporti delle leg dei tour** e degli aeroporti toccati dai PIREP, non per tutti.
- Numeri opzionali con convertitori tolleranti (ADR-049).
- ⚠️ `FirDirectory` e `networkStats` oggi deducono «gli aeroporti della divisione» dalla tabella: con tutto il mondo
  dentro devono filtrare per paese. Da verificare nella fase che lo fa.

### 1.14 Banner e immagini dei tour nella media library (estensione del nucleo n.16)

**Che cosa serve** (Carmine, 15 settembre): banner e immagini dei tour li prepara e **li carica il PRD** nella media library, e
vengono **collegati a un tour**; **un mese dopo la chiusura del tour un job li elimina da solo** per risparmiare spazio. **Lo
stesso meccanismo servirà agli eventi** (M4): quindi è del nucleo, non del modulo.

**Che cosa c'è già**: la media library del nucleo (G20). Un file appartiene al dipartimento che lo carica (qui il PRD) ed è
**letto da tutti i dipartimenti**, quindi il FOD lo sceglie senza permessi in più. Un file **usato** non si cancella: si
archivia. «Dove è usato» lo sa l'indice `cms_content_references`, ma **solo per le pagine pubblicate**: oggi un tour che usa un
file non lo dice a nessuno, e la media library lascerebbe cancellare il banner di un tour aperto.

**Che cosa si tocca**:

- **L'indice degli usi si allarga alle righe dei moduli**: una riga che mostra un file lo dichiara con `IProjectable`, come fa
  già per ricerca, calendario e award (`MediaReferences` nel `ProjectionSnapshot`), e l'interceptor scrive l'uso nella stessa
  transazione. Nessun controllo scritto a mano nella media library: continua a chiedere all'indice.
- **Ogni uso porta una scadenza**: una riga di modulo dichiara il file **con la data fino a cui le serve**. Il tour dichiara
  banner e foto del riquadro con scadenza `close_at + 1 mese` ⚖️ (le immagini dentro il briefing seguono la stessa regola se sono
  file caricati per quel tour). Un evento, in M4, dichiarerà i suoi allo stesso modo.
- **Un job del nucleo**, giornaliero, **elimina i file i cui usi sono tutti scaduti**. Un file ancora usato da qualcos'altro — una
  pagina pubblicata, un altro tour aperto, un evento — **non si tocca**: basta un uso non scaduto per tenerlo. Ogni eliminazione
  lascia una riga nell'audit e nel log dei job.
- **Se il tour viene prorogato** (nuova `close_at`), la scadenza dell'uso si sposta con lui, nella stessa transazione: il file non
  sparisce sotto un tour ancora aperto.
- **Il tour senza immagine** (dopo l'eliminazione) mostra un fondo neutro: nessun errore, nessun link rotto.
- **Il collegamento al tour** lo fa chi modifica il tour (`Tours.Edit`) scegliendo dal selettore della media library (**deciso** da
  Carmine il 15 settembre; nessun permesso nuovo per il PRD). La forma: nota `2026-09-15-file-con-scadenza`.

### 1.13 METAR e TAF salvati (estensione del nucleo n.12)

**Perché**: il validatore deve poter verificare una deviazione motivata dal meteo, e i tour VFR chiedono le VMC
almeno all'aeroporto di partenza e di arrivo. Il meteo di un giorno passato non si recupera sempre dopo.

- **La fonte è nel nucleo**: `IWeatherSource`, con la catena **misurata in vIPI**: NOAA (`aviationweather.gov`,
  METAR e TAF, senza chiave) → ripiego del **solo METAR** su IVAO (`/v2/airports/{icao}/metar`, **minuscolo**:
  maiuscolo risponde 404) → VATSIM. **Il TAF non ha ripiego**: nessuna fonte gratuita e indipendente da NOAA lo dà
  (provato da vIPI l'8 settembre). La parte IVAO passa dall'unico `IIvaoApiClient`.
- **Che cosa si salva**: `fo_weather_reports` (`icao`, `kind` METAR/TAF, `issued_at`, `raw`, `source`,
  `fetched_at`), una riga per bollettino, senza doppioni.
- **Quali aeroporti** (per non scaricare il mondo):
  1. un job ogni **30 minuti** (confermato il 15 settembre) salva METAR e TAF degli **aeroporti delle leg dei tour aperti o in chiusura**;
  2. **all'invio di un PIREP**, il server scarica anche gli aeroporti **toccati dal volo** che non erano in elenco
     (deviazione, tour a distanza senza leg), chiedendo a NOAA la **storia di METAR e TAF** delle ore del volo.
     **Verificato in T0** (nota `2026-09-15-meteo-e-confini-dei-fir`): con `date` e `hours` NOAA dà METAR fino ad almeno
     18 giorni indietro e **anche i TAF passati** (almeno 7 giorni; qui la prima stesura diceva che non c'erano). Oltre, il
     validatore vede «non disponibile».
- **Quando si cancella** (la regola di Carmine): un bollettino si elimina quando è più vecchio di
  `weatherRetentionDays` **e** tutti i PIREP con un volo in quel giorno su quell'aeroporto sono decisi. Un job
  giornaliero. **Deciso in T16** (nota `2026-09-24-il-meteo-salvato`): `weatherRetentionDays` è la finestra di riporto più lunga dei
  tour aperti o in chiusura, più un giorno di margine; «deciso» vuol dire né in coda, né in revisione, né da modificare, né rifiutato con
  una contestazione aperta; il giorno prima del volo resta con il volo (il TAF in vigore). Il meteo di un PIREP deciso non si tiene.
  All'invio si chiede la storia di ogni aeroporto del report **senza un METAR salvato nella finestra del volo**, non solo di quelli fuori
  elenco.
- **Come si usa**: nella pagina di validazione i METAR e i TAF dell'intervallo del volo per partenza, arrivo e
  deviazione (§4.3); il controllo `vmc` (§6.4) sui tour VFR.
- Nessun modulo nomina NOAA o VATSIM: le nomina solo l'implementazione del nucleo, come IVAO.

---

## 2. I tipi di tour

Ogni tipo risponde a tre domande, scritte **una volta** in `TourRules` e usate da riquadro, pagina, form e
salvataggio: **quali leg può volare adesso**, **qual è la prossima**, **quando ha finito**.

Regole comuni: una leg ritirata o con `release_at` futura non si vola (in sequenza ferma il pilota alla precedente);
una leg accettata o in attesa non si rivola; una leg rifiutata si rivola; un pilota **bannato** (§3.9) non vola.
**Contestata** una leg rifiutata, la leg **non blocca** più le successive (§3.8).

### 2.1 `Sequential` — in sequenza

- **Volabile**: la prima leg non accettata né in attesa, se le precedenti sono accettate (`WaitForValidation`) o
  accettate o in attesa (`FlyAhead`).
- **Finito**: tutte le leg non ritirate accettate.

### 2.2 `Free` — libero

- **Volabili**: tutte quelle non accettate né in attesa; **una rifiutata si rivola quando il pilota vuole**, ma serve
  per completare.
- **Prossima**: nessuna; il riquadro dice «scegli una leg» (risposta 9).
- **Finito**: tutte accettate.

### 2.3 `Hub`

- **All'inizio** il pilota sceglie un hub **volando la prima leg di una sua rotazione** (Carmine, 23 settembre, nota
  `2026-09-23-il-pirep`): nessun passo in più, l'hub è quello della prima leg volata.
- **Dentro l'hub**: con `hub_rotation_order = Fixed` le rotazioni nell'ordine del tour, con `Free` a scelta; **ogni
  rotazione sempre in ordine** (risposta 10).
- **Cambio di hub**: finite tutte le rotazioni. Con leg di collegamento, solo verso un hub collegato non fatto, volando
  la leg; senza, a scelta fra i non fatti.
- **Finito**: tutti gli hub.

### 2.4 `SequentialChosenStart` — in sequenza con partenza a scelta

- La leg del primo PIREP **non ritirato** è la partenza (Carmine, 23 settembre: un PIREP ritirato la lascia libera, uno rifiutato
  no); poi in ordine fino all'ultima, e dalla prima fino a quella prima della partenza.
- **Il tour dev'essere ad anello** (risposta 11): controllo di pubblicazione.

### 2.5 Il rifiuto e la tolleranza

In **tutti i tipi tranne `Free` e `Open`** (risposta 14 e revisione del 15 settembre §4.4): una leg
rifiutata **blocca** l'invio di PIREP sulle leg successive finché non è di nuovo in attesa o accettata, **eccetto** le
leg il cui decollo è avvenuto entro `decided_at + rejectGraceHours`. Nei tour liberi la leg si rivola quando il pilota
vuole, e senza non si completa.

### 2.6 `Distance` e `Open` — i tour senza un percorso obbligato

- **`Distance`**: leg pubblicate, volabili libere (risposta 12). **Finito**: somma delle GCD delle leg accettate
  `≥ required_nm`. Il controllo di pubblicazione verifica che tutte insieme ci arrivino.
- **`Open`** (proposta del 15 settembre, §2.6.1): **nessuna leg pubblicata**; ognuno vola quello che vuole dentro le
  regole del tour. Il PIREP non ha `leg_id`: porta partenza e arrivo del volo.

In tutti e due si conta **la GCD**, mai la distanza volata, e **non si vola due volte la stessa rotta**. **A→B e B→A
sono rotte diverse** (Carmine, 15 settembre).

#### 2.6.1 Il tour `Open`: un obiettivo, dei filtri, una catena

Carmine lo trova il formato più interessante e malleabile. Proposta: un tour `Open` si compone da **tre pezzi**, tutti
presi da **insiemi chiusi** scritti nel codice del modulo (ogni tipo ha il suo schema di parametri, la sua verifica e le
sue chiavi i18n). Il FOD li combina nell'editor senza scrivere codice; un tipo nuovo è codice e una chiave i18n.

**1. L'obiettivo** (uno per tour, `fo_tours.open_goal` con i parametri): che cosa bisogna accumulare per finire.

| Obiettivo | Si completa quando | Esempio |
|---|---|---|
| `Distance` | somma delle GCD `≥ N` NM | «10 000 miglia in un anno» |
| `FlightCount` | `N` voli accettati | «50 voli fra aeroporti italiani» |
| `DistinctAirports` | `N` aeroporti diversi toccati | «atterra in 30 aeroporti diversi» |
| `DistinctCountries` | `N` paesi diversi toccati | «giro d'Europa: 20 paesi» |
| `CollectList` | tutti (o `N` di) gli aeroporti di un **elenco** | «le 20 capitali europee», «tutti gli aeroporti della Sardegna» |
| `CollectRegions` | tutti (o `N` di) i paesi o i FIR di un elenco | «tutti i FIR italiani» |

**2. I filtri per volo** (zero o più, `fo_tour_constraints`): un volo che non li rispetta **non si invia** (blocca, come
callsign e aereo).

| Filtro | Parametri | Esempio |
|---|---|---|
| `DepartureOrArrivalIn` / `DepartureIn` / `ArrivalIn` | paesi | parti **o** arrivi in Italia |
| `TouchesAirport` | aeroporti (uno o più) | ogni volo parte o arriva a LIRF (tour «a stella»), o a LIRF o LIMC |
| `DistanceBetween` | minimo e/o massimo NM | tratte fra 200 e 1500 NM |
| `AircraftCategory` | categorie di scia (`L`, `M`, `H`, `J`) | solo aerei leggeri |
| `ArrivalRunwayMax` | lunghezza massima in metri | «piste corte»: arrivi su piste sotto i 1500 m (dalle piste `ref_`) |
| `ArrivalElevationMin` | quota minima in piedi | «aeroporti in quota» (dall'elevazione IVAO) |
| `FlightRules` | `I`, `V` | solo VFR |

**3. Le regole di sequenza** (zero o più): legano un volo ai precedenti del pilota nel tour.

| Regola | Significato | Esempio |
|---|---|---|
| `NoRepeatedRoute` | la stessa coppia ordinata non si ripete | sempre accesa |
| `Chained` | la partenza è l'arrivo del volo precedente accettato o in attesa | «coast to coast» senza teletrasporti |
| `Eastbound` / `Westbound` | ogni arrivo è a est (ovest) della partenza | «giro del mondo verso est» con `Distance` |
| `IncreasingDistance` | ogni volo più lungo del precedente | «la scala» |
| `MinFlightsAt` | almeno `N` voli su un aeroporto, verificato **al completamento** | «almeno 3 voli su LIRF» |

**Esempi di tour componibili**, per dare un'idea di quanto è malleabile:

- **Giro del mondo verso est**: `Distance` 21 600 NM, `Chained`, `Eastbound`.
- **Le capitali d'Europa**: `CollectList` (20 aeroporti), `DistanceBetween` 150–1500 NM.
- **Piste corte d'Italia**: `DistinctAirports` 15, `DepartureOrArrivalIn` Italia, `ArrivalRunwayMax` 1500 m, `FlightRules` V.
- **Hub di Fiumicino**: `FlightCount` 30, `TouchesAirport` LIRF, `NoRepeatedRoute`.
- **La scala**: `FlightCount` 10, `IncreasingDistance`, `Chained`.

**Entrano tutti in M2** (Carmine, 15 settembre): obiettivi, filtri e regole di sequenza delle tre tabelle. Usano dati che
l'hub avrà già (GCD, paesi e FIR, piste e quote degli aeroporti, tipi di aereo). La pagina del tour mostra **quanto manca**
all'obiettivo e ai vincoli «al completamento».

**Precisato il 22 settembre 2026** in apertura di T7c (Carmine, nota `decisions/2026-09-22-il-tour-open.md`):

- **L'obiettivo ha una scheda sua**, «Obiettivo e vincoli», solo sui tour `Open`: si sceglie il tipo, poi si scrivono i suoi
  parametri; si salva con il tour. Sotto, la lista generata dei filtri e delle regole, ognuno con il suo form.
- **Filtri e regole valgono solo sui tour `Open`**: su un tour con leg li fissano le leg, gli aerei consentiti e il tipo.
- **Una riga per tipo**, tranne `MinFlightsAt` (una per aeroporto); `TouchesAirport` prende un elenco.
- **`AircraftTypes` non è un filtro**: tipi e gruppi ammessi sono gli aerei consentiti del tour (§1.5), anche su un `Open`.
- **Un `Open` con filtri o regole non cambia tipo**; l'obiettivo si svuota quando il tipo non è più `Open`.
- **Gli elenchi** di `CollectList` e `CollectRegions` (paesi **o** FIR, mai tutti e due) si scrivono nel tour; un template li porta.
- **«Pronto»** chiede un obiettivo con i suoi parametri, e non accetta `Eastbound` con `Westbound`.

### 2.7 `Container` — con sottotour

- Nessuna leg; sottotour di un livello. Il pilota si iscrive e vola i sottotour.
- **Un sottotour eredita** aerei, vincoli sul callsign e regole del tour dal padre **se non ne ha di suoi** (risposta 13); per il
  callsign: gli `Allow` del livello più vicino, i `Deny` di tutti (§1.6).
- **Un sottotour ha le sue date, dentro il periodo del padre; quelle che non ha sono del padre**, ognuna da sola, e lo seguono quando
  cambia; ha **il suo slug** (Carmine, 21 settembre, nota `2026-09-21-la-forma-dei-tour`). La cura dei dipartimenti è del padre.
  **Ricerca e calendario li porta solo il padre**: un sottotour proietta solo i suoi file, e la pagina del padre lo elenca (T10). Nella
  lista `/staff/tours` un sottotour non compare: sta nella scheda del padre. Un `Container` con sottotour non cambia tipo e si elimina
  dopo di loro.
- **Un sottotour non ha award**; l'award è del padre, segnalato con `required_subtours` sottotour completati.

---

## 3. Il flusso del pilota

### 3.1 Gli stati del PIREP

```
               ┌───────────── (il pilota corregge e reinvia) ───────────┐
               ▼                                                       │
  inviato ──► in coda ──► in validazione ──► accettato                 │
                ▲             │  (lease scaduto: di nuovo libero)      │
                │             ├──► da modificare ──────────────────────┘
                │             └──► rifiutato ──► contestato (non blocca le successive, §3.8)
                │                                     └──► riaperto in coda (se accolta)
  ritirato ◄────┘ (il pilota, finché nessuno l'ha preso)

  «richiedi chiarimenti»: su qualunque PIREP deciso, non cambia lo stato (§3.10)
```

| Stato | Chi lo porta lì | Effetto sul tour |
|---|---|---|
| `Queued` | invio, reinvio, riapertura | leg **in attesa** |
| `InReview` | un validatore | in attesa |
| `Accepted` | il validatore | leg fatta |
| `ToModify` | il validatore | in attesa; il pilota corregge **tutto** — anche la sessione del tracker — e reinvia; torna in coda **a chiunque**. **Finché non corregge non può riportare altre leg** (Carmine, 15 settembre): così è sicuro che lo faccia. Se non corregge entro `report_window_days`, il PIREP **si ritira da solo** e la leg torna da volare |
| `Rejected` | il validatore | leg da rivolare; blocca le successive (§2.5) **finché non è contestata** |
| `Withdrawn` | il pilota, solo da `Queued` | leg volabile; sessione libera |

`is_disputed` è una bandiera su un `Rejected`, non uno stato. Ogni passaggio scrive in `fo_pirep_events`.

### 3.2 Il report

1. **La leg** (o, in un tour `Open`, «nuovo volo»): il server verifica che sia volabile, che il tour accetti PIREP, che il
   pilota non sia bannato.
2. **Ricerca nel tracker** (estensione n.5): `GET /v2/tracker/sessions` con `userId`, `departureId`, `arrivalId`,
   `from = now − report_window_days`, `to = now` (senza aeroporti in un tour `Open`). Esclude le sessioni già rivendicate
   e quelle con decollo dopo `close_at`.
3. **Il pilota sceglie il volo.** Il server legge **tutte** le revisioni del piano (`/flightPlans`) e le tracce
   (`/tracks`), e segna la revisione valida al decollo.
4. **Campi**:
   - **SID, STAR, IAP** obbligatori se `requires_procedures` (risposta 4), secondo le regole di volo del piano:
     `I` tutti e tre; `Y` (IFR poi VFR) **solo la SID**; `Z` (VFR poi IFR) **solo STAR e IAP**; `V` nessuno.
   - **ATC contattati**, proposti dal sistema (§3.3).
   - **Esenzioni ATC**, **note**, **deviazione** (§3.4).
5. **Controlli al salvataggio, che bloccano** (`ProblemDetails` campo per campo):
   - leg volabile e tour aperto; finestra dei giorni; data del volo non oltre la chiusura; sessione non rivendicata;
   - **callsign consentito** e **aereo consentito** (risposta 5);
   - **limiti giornalieri**, del tour e di divisione (§3.7);
   - filtri e regole di sequenza di un tour `Open` (§2.6.1); rotta non già volata;
   - pilota non bannato;
   - **rating pilota** almeno `min_pilot_rating` del tour (dal profilo IVAO letto al login, `hub_users.rating_pilot`; ⚠️ è
     aggiornato all'ultimo login, quindi un pilota appena promosso rifà il login);
   - **volo non prima del rilascio** del tour e della leg (date in **UTC**, sempre, Carmine, 15 settembre);
   - nessun suo PIREP «da modificare» in attesa di correzione in quel tour (§3.1).
6. Il PIREP nasce `Queued`, con `rules_snapshot_json` e **i dati della leg com'erano all'invio** (partenza, arrivo, aerei,
   callsign: se la leg cambia dopo, il PIREP si giudica su quella inviata, Carmine, 15 settembre); il primo del pilota nel tour crea l'iscrizione. Partono in un job
   i controlli automatici (§6) e lo scarico del meteo mancante (§1.13).

### 3.3 Gli ATC contattati e le esenzioni

- **Mentre il pilota compila**, il sistema **propone** gli ATC che erano online lungo il volo, con lo stesso meccanismo
  del controllo di copertura (§6.5): archivio ATC per l'intervallo del volo, incrociato con le tracce. Il pilota
  **toglie** quelli che non ha contattato e **aggiunge** quelli che mancano; resta scritto chi è stato proposto e chi
  aggiunto, **e chi è stato proposto e tolto** (Carmine, 23 settembre 2026). La proposta si rifà sul server all'invio: quella
  che il browser mostra serve solo al pilota.
- **Versione leggera, sul server** (per non caricarlo, e perché il pilota non ha i dati di navigazione): le posizioni
  online negli aeroporti di partenza, arrivo e deviazione e nei FIR attraversati, calcolati dai punti delle tracce a
  campione con i confini dei FIR di OpenAIP. La verifica precisa lungo la rotta è dell'agente del validatore (§6.6).
- **Esenzioni**: il pilota sceglie la posizione fra gli ATC contattati e il tipo (`FreeSpeed`, `DirectRouting`,
  `LevelChange`, `Other`); ogni tipo dichiara quali controlli ammorbidisce (Carmine, 23 settembre 2026): `FreeSpeed` →
  `speed250`, `LevelChange` → `semicircularLevels`, `DirectRouting` → nessun controllo di M2 (l'aderenza alla rotta, quando ci
  sarà), `Other` → nessuno, con la nota obbligatoria. Vale se la posizione risultava online; lo stato all'invio è **`Online`**,
  **`NotOnline`** (l'archivio copre tutto il volo e non la elenca) o **`Unverifiable`** (nessun archivio, o uno che non arriva
  così indietro), e **non blocca mai l'invio**: decide il validatore. Nota `decisions/2026-09-23-gli-atc-contattati.md`.

### 3.4 Il volo del tracker e le deviazioni

- **Una sessione vale per un solo PIREP, quindi per un solo tour** (Carmine, 15 settembre): lo stesso volo non conta per due tour.
  Si libera se il PIREP è ritirato o se il pilota la sostituisce correggendo.
- **Deviazione**: il pilota indica che il volo è finito altrove, sceglie il **volo di riposizionamento**
  dall'aeroporto di deviazione alla destinazione della leg, e sceglie il **motivo** (strutturato più testo). Il server
  verifica che il secondo parta da dove il primo è atterrato. Con motivo `Weather` la pagina di validazione mostra in
  evidenza METAR e TAF dell'aeroporto di destinazione all'ora della deviazione (§1.13).

### 3.5 Gli esiti e le mail

Intenti al servizio del nucleo: `flightops.pirepAccepted`, `flightops.pirepToModify`, `flightops.pirepRejected`. Al
pilota, nella sua lingua: tour, leg, esito, `note_to_pilot`, **le regole violate**, il link. **Nessuna mail di tour
completato** (risposta 21).

**Il pilota non vede il nome del validatore** (Carmine, 15 settembre): né nella pagina né nella mail, dove la decisione è «del
FOD». Il nome resta visibile allo staff.

**Il completamento non si toglie mai**: se a un tour aperto si aggiunge una leg, chi l'aveva già completato resta completato (e la
segnalazione dell'award è già partita).

### 3.6 I limiti giornalieri — bloccano

**Confermato**: il limite **impedisce l'invio**, non lo accetta per poi rifiutarlo (revisione del 15 settembre e
risposta 6).

- **Contano** i PIREP del pilota non rifiutati e non ritirati, per **giorno UTC del decollo**.
- **Limite del tour** (`daily_leg_limit`): i PIREP del tour. **Limite di divisione** (`dailyLegLimit`): i PIREP di tutti
  i tour. Superato l'uno o l'altro, il form rifiuta l'invio dicendo quale limite e quando si libera.
- ⚠️ Il limite conta i **voli di quel giorno**, non gli invii. Con un limite di 12, un pilota può inviare **12**
  PIREP di voli decollati lo stesso giorno UTC anche se li riporta in tre giorni diversi; il **tredicesimo** volo di quel
  giorno non si può inviare, in qualunque giorno lo riporti (Carmine, 15 settembre).

### 3.7 Spegnere il limite di divisione

Il salvataggio delle impostazioni rifiuta `dailyLegLimit = null` se esiste un tour **aperto, in arrivo o in bozza con
date future** senza `daily_leg_limit`, ed elenca quei tour. Con il limite spento, «pronto» richiede `daily_leg_limit`.

### 3.8 La contestazione (estensione del nucleo n.2)

- **Solo su un `Rejected`**, entro **`disputeWindowDays`** dalla decisione (risposta 7).
- Diventa un **filo** nei contatti del FOD, legato al PIREP, con il validatore che ha deciso fra chi legge e risponde.
  Rispondono chiunque del FOD con `Contacts.View` (advisor compresi) e il validatore; il pilota riceve le risposte per
  mail e risponde dal link.
- **Aperta la contestazione, la leg non blocca più le successive** (revisione del 15 settembre): il pilota continua a
  volare mentre qualcuno riguarda il caso. Se la contestazione è **respinta**, la leg torna a bloccare con la sua
  tolleranza calcolata da quel momento ⚖️; se è **accolta**, un validatore **riapre** il PIREP in coda.
- **Si contano le contestazioni** di ogni pilota (aperte, accolte, respinte), nella pagina del pilota e nel pannello di
  validazione: chi le usa per andare avanti con le leg si vede.
- **Il pilota non vede i contatori** (ADR-009), né nella pagina né nella mail.
- **Precisato in T14b** (23 settembre, nota `2026-09-23-contestazioni-chiarimenti-segnalazioni`, quattro risposte di Carmine): la
  decide **solo chi ha `Tours.ReopenDecisions`** sul tour e **non ha deciso quel PIREP** (il validatore che ha deciso partecipa e
  risponde, non giudica); accogliere o respingere chiede **una risposta al pilota**, che entra nel filo come risposta del dipartimento
  ed è la mail dell'esito; **una contestazione per PIREP**; accolta, il PIREP torna **`Queued`** a chiunque. Le colonne sono
  `dispute_status` (`Open`, `Upheld`, `Dismissed`), `dispute_text`, `disputed_at`, `dispute_decided_at`, `dispute_decided_by_vid`;
  `is_disputed` è la lettura di `Open`. Con una contestazione aperta **«riapri» (§4.2.1) non c'è**: si riapre accogliendola.

### 3.9 I ban — `fo_bans`

- `vid`, `tour_id?` (null = **tutti i tour**), `starts_at`, `ends_at?` (null = **permanente**), `reason`, audit.
- Un pilota bannato **non invia PIREP** sui tour del ban; vede i tour, i suoi PIREP, e gli esiti di quelli già inviati,
  che **si validano normalmente** (confermato il 15 settembre).
- Lo decide chi ha `Tours.Ban`: **HQ, superadmin, coordinator e assistant coordinator FOD** (FOC e FOAC; Carmine,
  15 settembre). Il pilota **riceve una mail** (`flightops.banned`) **con il motivo** e la durata.
- **Precisato in T15a**: un ban nomina un tour di primo livello o nessuno (un ban sul contenitore vale per i sottotour); **non si
  cancella** — si toglie spostandone la fine —, perché è parte del registro del pilota (§10.1); la mail parte quando il ban si scrive,
  non quando si cambia. Lista e form generati, lettura con `Tours.ViewPilots` (la pagina del pilota li mostra), scrittura con
  `Tours.Ban`.

### 3.10 Richiedi chiarimenti

- Su **qualunque PIREP deciso**, e anche su **una leg** o **una regola** del tour dalla pagina del tour (Carmine,
  15 settembre), il pilota chiede che gli si spieghi una procedura, un meccanismo o una regola. **Non chiede di cambiare il
  verdetto**, non cambia lo stato e **non conta** come contestazione.
- **Un messaggio può chiedere più spiegazioni insieme**: il filo porta un elenco di riferimenti (PIREP, leg, regole),
  non uno solo. Chi risponde vede tutti gli oggetti citati.
- Stesso meccanismo del filo (estensione n.2), con il tipo `Clarification`: arriva al FOD e al validatore del PIREP, se
  c'è.
- **Precisato in T14b**: si chiede da una pagina propria, `/tours/{slug}/ask` (dalla riga di un PIREP deciso, da una regola, dal
  tour), con il form generato; i riferimenti sono `pirep:{id}`, `leg:{id}` e `rule:{tourId}:{ruleId}`, risolti dal modulo
  (`FlightOpsReferences`) per chi legge; va al dipartimento del tour. «Deciso» lo fa la pagina: il risolutore non sa se apre o legge.

### 3.11 Il completamento, l'award, le segnalazioni

- Quando un PIREP accettato completa il tour, l'iscrizione scrive `completed_at` e, con `IProjectable`, proietta una
  **`AwardSignalProjection`** nella stessa transazione, **con l'`award_id` del tour** come proposta (T4b, nota
  `2026-09-16-award-e-preferenze`). Nessuna assegnazione automatica: chi ha `Awards.Assign` risponde dalla coda
  `/staff/awards/queue`, e il membro vede l'award sul suo profilo IVAO, mai nell'hub. Un sottotour completato conta per
  il padre, non segnala niente di suo.
- **Precisato in T15a** (nota `2026-09-23-completamento-validatori-piloti-ban` §3.1): è l'**iscrizione** a proiettare la
  segnalazione (`enrolment:{id}`), con il motivo nella lingua della divisione e l'award proposto dal tour, nel salvataggio che accetta
  il PIREP: PIREP accettato, completamento e segnalazione in una transazione. Mai tolto, nemmeno se la decisione si riapre e diventa un
  rifiuto.
- **Segnalare un problema su una leg**: `fo_leg_issues` e una notifica alla casella del FOD
  (`flightops.legIssueReported`). **Precisato in T14b**: `tour_id`, `leg_id`, `body`, `status` (`Open`, `Resolved`), `staff_note`,
  dipartimento del tour, audit (il pilota è `created_by`); il pilota la scrive da un dialog sulla riga della leg, lo staff la chiude
  da `/staff/tours/issues` (lista e form generati, `Tours.View` legge, `Tours.Edit` chiude); nessuna risposta al pilota.

---

## 4. La validazione

### 4.1 Le code

- **Una coda unica** e **una coda per tour** (la stessa lista generata, con il filtro sul tour).
- **Il validatore sceglie l'ordine** (revisione del 15 settembre): **per data** (dal PIREP che aspetta di più, risposta
  25) o **per tour** (raggruppati per tour, e dentro il tour per data), perché validare in fila le leg dello stesso tour è
  più veloce. La scelta resta memorizzata **come preferenza del suo utente** (Carmine, 15 settembre), così la ritrova su
  qualunque computer (estensione del nucleo n.15).
- **Chi vede**: chi ha `Tours.Validate` su almeno un tour vede **tutti** i PIREP in sola lettura; «Prendi» c'è solo sui
  tour per cui è abilitato.
- **I propri PIREP**: il validatore **li vede**, in **sola lettura**, con il loro esito e gli errori segnati (revisione del
  15 settembre); non li prende e non li decide (§7.3).
- Colonne: tour, leg, pilota, data del volo, in coda da, stato, preso da, contestato, suggerimento dei controlli.
- **Precisato in T13a**: la coda è la lista generica, `GET /api/flightops/review/queue`, ordinata su `queued_at`; «per tour» è
  `sort=tourId`, e la lista generica tiene l'ordine per data dentro ogni tour. La preferenza si chiama `flightops.reviewQueueOrder`
  (`date` | `tour`). Il suggerimento dei controlli entra con T17.

### 4.2 La presa in carico

`InReview` con `assigned_to_vid` e `lease_until = now + leaseMinutes`; alla scadenza chiunque abilitato lo può
prendere (nessun job). Due prese insieme: vince la prima (`row_version`).

### 4.2.1 Correggere una decisione

Una decisione presa si **riapre** (il PIREP torna `InReview` con una riga in `fo_pirep_events` e una motivazione obbligatoria) da:
il **validatore che l'ha presa** e **FOC e FOAC**, **senza limiti di tempo** (Carmine, 15 settembre). La nuova decisione sostituisce
la vecchia nei contatori e manda di nuovo la mail al pilota. **FOC e FOAC** sono il permesso **`Tours.ReopenDecisions`** (§7.1),
negato all'interessato; chi ha deciso riapre la sua con il solo `Tours.Validate` sul tour (Carmine, 23 settembre, T13a). Il PIREP
riaperto torna `InReview` **in mano a chi l'ha riaperto**.

### 4.2.2 Il riepilogo giornaliero

Una mail al giorno a **tutti i validatori** (chi ha `Tours.Validate`), con la coda dei tour che ciascuno può validare: quanti PIREP per
tour e da quanto aspetta il più vecchio (Carmine, 15 settembre: «magari qualcuno si fa lo scrupolo e butta l'occhio»). Tipo di
notifica `flightops.reviewDigest`, con la preferenza del membro per spegnerla; non parte se la coda è vuota.

### 4.3 La pagina di validazione (schermata dedicata)

- **La leg e il volo**: tour, leg, mappa della tratta con la traccia volata, **tutte le revisioni del piano di volo** con
  quella al decollo evidenziata (revisione del 15 settembre), SID/STAR/IAP, **ATC contattati** (proposti e aggiunti),
  esenzioni con il loro stato, deviazione con il motivo, note.
- **Il meteo** (§1.13): METAR e TAF di partenza, arrivo e deviazione nell'intervallo del volo, con la fonte.
- **Il decollo** (§6.4): la testata da cui è partito, o la distanza dalla testata più vicina se è partito da
  un'intersezione, con l'avviso «verificare la TORA pubblicata da quel punto».
- **I controlli automatici** con l'evidenza.
- **Il profilo del pilota**: nel tour leg volate, accettate, rifiutate; **contestazioni** (aperte, accolte, respinte);
  **ban** in corso o passati.
- **La tabella degli errori** delle regole effettive, con il conteggio **nell'anno solare del volo** (risposta 8) e **da
  sempre** per il pilota; i suggeriti dai controlli già segnati come suggeriti.
- **Il suggerimento**: un `Dangerous` segnato → rifiuto dalla prima occorrenza; un `Warning` che porta l'anno oltre
  `yearly_max` → rifiuto; altrimenti accettazione. **L'anno** conta l'errore confermato sui PIREP **accettati e rifiutati** del
  pilota, di **tutti i tour**, per anno UTC del decollo, più questo; un «da modificare» non conta (Carmine, 23 settembre, T13a). La
  pagina lo chiede al server mentre si spuntano gli errori (`…/review/{id}/suggestion`), non lo ricalcola.
- **La decisione** con `note_to_pilot` e `staff_note`; contro il suggerimento, `threshold_overridden` e il perché.
- **Precisato in T13b** (nota `2026-09-23-le-pagine-della-validazione`): la pagina riceve le revisioni del piano già lette dal client
  del nucleo e gli aeroporti con la posizione, mai il JSON di IVAO; la mappa disegna la leg congelata, i voli di una deviazione e la
  traccia (finché c'è, 90 giorni dopo la decisione). Si prende dalla pagina, non dalla coda.

---

## 5. Regole ed errori

### 5.1 Le schermate

- **Regole generali**, **errori**: liste e form generati, con le colonne «senza errori» / «senza regole». Il form della
  regola disegna i **parametri** dallo schema del controllo collegato (§6.2) ⚖️ (estensione del generatore di form se
  serve).
- **Regole del tour**: una scheda dell'editor, con «copia da un altro tour» e «emenda» accanto a ogni generale.

### 5.2 Le regole effettive

Le generali non ritirate, sostituite da quelle del tour che le emendano (con i parametri del tour sopra quelli della generale, e
gli errori di tutte e due), più quelle del tour; per un sottotour, prima il padre e poi il sottotour, che vince se emenda la stessa
generale. Un emendamento di una generale ritirata non vale. Un solo servizio (`EffectiveRules`, T9), usato da pagina, PIREP,
controlli e validazione.

### 5.3 Gli errori pubblici

Blocco Data `flightops.errorCatalog`: gli errori con `is_public`, con categoria, descrizione, esempi e le regole **generali** in
vigore collegate. Sempre `live`, senza proprietà (T9). Il FOD lo mette in una pagina o in un documento.

### 5.4 Le regole congelate

Al **primo invio** il PIREP copia le regole effettive della leg, con i **parametri** e gli errori, in
`rules_snapshot_json`; il reinvio dopo «da modificare» non la rifà. Controlli e validazione leggono lo snapshot.

---

## 6. I controlli automatici (estensioni del nucleo n.5, n.8, n.12)

### 6.1 Che cosa fanno

Un PIREP inviato mette in coda un job che esegue i controlli e scrive `fo_check_results` (`pirep_id`, `check_key`,
`outcome`, `evidence_json`, `ran_at`). Per ogni controllo non superato, gli errori con quel `check_key` diventano
**suggeriti**.

**Com'è fatto (T17, nota `2026-09-24-il-motore-dei-controlli`)**: i controlli girano **subito dopo l'invio e il reinvio**, senza mai
rifiutarlo, e **un job ogni dieci minuti** riprende i PIREP in coda o in revisione con `checks_ran_at` più vecchio di `queued_at`.
`fo_check_results` ha anche `ran_by` (`Server`, `Agent`), unico per PIREP, controllo e chi l'ha eseguito. Gli errori suggeriti sono righe di
`fo_pirep_errors` con `suggested_by_check` e non confermate; la decisione le tiene accanto a quelle spuntate, e contano solo le confermate.
L'evidenza del server è un elenco di chiavi i18n con i loro valori, quella dell'agente testo.

### 6.2 La forma

- `IFlightCheck`: `Key`, lo **schema dei parametri** (con i default della regola generale di partenza) e
  `EvaluateAsync(FlightCheckContext)` → `Passed`, `Failed` (con evidenza) o **`Unavailable`** (dati mancanti: mai «fallito»).
  **In T17** `Evaluate` è una funzione **pura e sincrona** (il contesto si raccoglie una volta, prima), e lo schema dei parametri resta
  in `CheckCatalog`; un controllo che lancia un'eccezione è `Unavailable`.
- Il contesto porta il PIREP, lo snapshot con i parametri, tutte le revisioni del piano, le tracce, gli aeroporti e le piste
  `ref_`, il meteo salvato e l'archivio ATC.

### 6.3 La macchina propone, dalla prima stagione

**Nessun controllo decide.** I suggerimenti sono attivi **dalla prima stagione** (revisione del 15 settembre: il FOD adotta
subito il metodo). Si registra comunque se il validatore conferma l'errore suggerito, per sapere quali controlli sbagliano.

### 6.4 I controlli di M2

| Chiave | Controlla | Dati | Parametri (nelle regole) |
|---|---|---|---|
| `callsign` | callsign consentito o vietato (anche bloccato all'invio) | piano | — |
| `aircraft` | tipo consentito (anche bloccato all'invio) | piano | — |
| `landingAtArrival` | atterrato all'arrivo o deviazione dichiarata | tracce | raggio |
| `disconnections` | disconnessioni in volo | tracce | `maxSingleDisconnectMinutes`, `maxTotalDisconnectMinutes` |
| `parking` | fermo prima del push e dopo l'arrivo | tracce | `minParkingMinutesBefore`, `minParkingMinutesAfter` |
| `speed250` | 250 kt sotto FL100: la velocità indicata **stimata** dalla velocità al suolo senza vento; i 1 000 ft sotto FL100 non falliscono (T18) | tracce, esenzioni | tolleranza |
| `simRate` | velocità riportata coerente con quella di posizione: la mediana su finestre di cinque minuti, solo più veloce (T18) | tracce | tolleranza |
| `alternate` | alternato presente, diverso dalla destinazione (uguale alla partenza: solo nell'evidenza), e `ZZZZ` ⚖️, sotto | piano | — |
| `equipment` | equipaggiamento richiesto | piano | lettere delle caselle **10a e 10b per regola di volo**; W e J1 solo con un livello pianificato sopra FL285 (nella casella 15 o nella rotta; T17) |
| `flightRules` | regole di volo del piano ammesse | piano | lettere I, V, Y, Z |
| `planAtTakeoff` | un piano valido al decollo; le revisioni dopo non contano (GR9) | piano | — |
| `flightPlanForm` | forma del piano: REG/ con un callsign da volo di linea, RMK/, Z con COM/DAT/NAV, R con PBN/ (T17), SID e STAR nella rotta solo nei paesi dell'impostazione, VFR senza DCT e con `VFR` come livello (un livello scritto fallisce, T17) | piano, impostazione dei paesi | — |
| `maxAltitude` | quota massima | tracce | un limite **per regola di volo** (`maxFeetI|V|Y|Z`): V 19 500 ft, I, Y, Z 66 000 (T18) |
| `takeoffFromThreshold` | decollo dalla testata | tracce, `ref_ivao_runways` | — (`thresholdToleranceMeters` è un'impostazione, §1.11; T9) |
| `vmc` | VMC a partenza e arrivo, **solo piani `V`** (e le metà VFR di `Y`/`Z`) | meteo salvato | visibilità e base nubi minime |
| `repeatedRoute` | rotta già volata in un tour `Distance` o `Open` (anche bloccato all'invio; A→B diversa da B→A) | PIREP | — |

**Due controlli non girano sul server ma sul PC del validatore** (§6.6, Carmine, 15 settembre): **`semicircularLevels`** e
**`atcCoverage`**. Tutti e due hanno bisogno di sapere **dove passa la rotta**, cioè delle coordinate dei punti del piano, e
quelle stanno nei dati di navigazione (Navigraph) che l'hub non ha e non può ridistribuire.

Note sui controlli delicati:
- **`alternate` e `ZZZZ`** (Carmine, 15 settembre): `ZZZZ` è sia un aeroporto reale in Cina sia il codice «aeroporto
  senza ICAO», molto usato nei VFR. Il controllo **non supera** solo se `ZZZZ` è l'**alternato** e nelle remarks del piano
  **manca `ALTN/`**; con `ALTN/` presente l'alternato è un campo volo senza ICAO, ed è valido. Un piano senza alternato
  resta un errore dove la regola lo chiede.
- **`takeoffFromThreshold`**: ~~dall'ultimo punto fermo prima della corsa di decollo nelle tracce~~, la distanza dalla testata
  della pista usata (quella con la prua più vicina alla prua di decollo). Oltre `thresholdToleranceMeters` il controllo non
  «fallisce»: segnala al validatore il **decollo da un'intersezione**, e il validatore verifica se da quel punto c'è una TORA
  pubblicata (dato che nessuna API dà). ~~⚠️ Il campionamento delle tracce IVAO va misurato~~ **Misurato in T18** (nota
  `2026-09-24-i-controlli-sulle-tracce` §4.1): con un punto ogni 15 secondi l'aereo quasi mai è colto fermo sulla pista, e un decollo
  senza fermarsi mai; la corsa parte quindi dall'**ultimo punto sotto i 30 kt**, riportato indietro di v²/2a, e la pista è anche quella
  **sul cui asse** sta la corsa (le parallele). 150 m tenuti.
- **`vmc`**: sul METAR più vicino all'ora di decollo e di atterraggio (entro un'ora); senza METAR salvato, `Unavailable`. La «base
  nubi» è il **ceiling** (BKN, OVC, VV); un piano I passa, Y guarda l'arrivo, Z la partenza (T18).
- **`speed250` e le esenzioni** (Carmine, 24 settembre, T18): un'esenzione `FreeSpeed` fa passare il controllo con una riga che la
  nomina, se la posizione era `Online` o `Unverifiable`; con `NotOnline` il controllo fallisce come senza.
- **`landingAtArrival`**: l'atterraggio è il primo punto a terra **dopo l'ultimo in volo** (un touch and go lungo la strada non conta),
  entro il raggio dalla posizione dell'aeroporto o da una sua testata; il primo volo di una deviazione deve atterrare alla deviazione.

Il riferimento per la logica è il validatore Python (`AutomaticValidatorTour`); ogni controllo si prova su **voli veri**
(§13).

**Dai PIREP veri** (24 settembre 2026, nota `2026-09-24-i-controlli-dai-pirep-veri`): 45 PIREP e 363 commenti dei controllori del
sistema di oggi hanno aggiunto `flightRules`, `planAtTakeoff`, `flightPlanForm`, `maxAltitude`, la forma per regola di volo di
`equipment` e l'alternato uguale alla destinazione. Le **manovre obbligatorie** della leg (touch and go, pista, VRP) restano fuori:
oggi nessuno le valida in automatico, e si valutano più avanti.

### 6.6 L'agente sul PC del validatore (livello B)

**L'idea di Carmine** (15 settembre): i controlli che dipendono dalla geometria della rotta vanno sul PC del validatore, dove c'è
Navigraph e quindi la posizione dei fix. **È la strada giusta**, ed è quella che Toursystem chiamava «livello B» (ADR-011): il
server non ha i dati di navigazione, e un'approssimazione sul server (il «trucco del `DCT`» sulle tracce) sbaglierebbe proprio
dove serve.

**Che cosa fa l'agente** (l'app Python `AutomaticValidatorTour`, adattata, o una sua erede):

| Controllo | Con i fix di Navigraph |
|---|---|
| `semicircularLevels` | ricostruisce la rotta segmento per segmento: dove il segmento è **`DCT`** assume FRA (circa 90 %, errore accettato dal FOD), dove è un'**aerovia** non giudica; per ogni tratto `DCT` calcola la **rotta magnetica** e il paese (dal FIR), e la confronta con il livello (est–ovest, o nord–sud per i paesi di `northSouthLevelCountries`) |
| `atcCoverage` | incrocia la rotta con le posizioni ATC online nell'intervallo (dall'archivio che l'hub gli passa) e dice quali settori ha attraversato con un ATC aperto; verifica le esenzioni dichiarate |
| livelli volati contro pianificati | salite e discese pianificate sui fix del piano, confrontate con le tracce (nota `2026-09-24-i-controlli-dai-pirep-veri`) |
| più avanti | aderenza alla rotta, SID e STAR dichiarate contro quelle volate (i controlli B di Toursystem: 07, 10, 14, 16) |

**Il contratto con l'hub** (estensione del nucleo n.14):

- **Autenticazione**: un **token personale** del validatore, creato dalla sua pagina, revocabile, con scadenza, legato ai suoi
  permessi (`Tours.Validate` con i suoi scope): l'agente vede solo quello che il validatore vede. Audit di ogni uso.
- **Lettura**: `GET /api/flightops/agent/pireps/{id}` — il PIREP con tutte le revisioni del piano, le tracce, i parametri delle
  regole congelati, la fetta di archivio ATC dell'intervallo del volo, gli aeroporti.
- **Scrittura**: `POST /api/flightops/agent/pireps/{id}/checks` — per ogni controllo `outcome` ed `evidence`; finiscono in
  `fo_check_results` con `ran_by = agent` e la versione dell'agente, e **suggeriscono** errori come quelli del server.
- **Quando gira**: quando il validatore apre il PIREP (l'agente lo vede in coda e lo elabora), oppure a lotti sulla coda dei tour
  che può validare. **Nessun dato di Navigraph va all'hub**: solo esiti ed evidenze in testo (la licenza di Navigraph è
  personale; da verificare che anche l'evidenza testuale sia ammessa ⚠️).
- **Senza agente**, i due controlli risultano `Unavailable` e il validatore giudica come oggi: l'hub funziona anche per una
  divisione che non ha l'agente.

**I costi, detti prima**: ogni validatore che lo vuole ha bisogno di un abbonamento Navigraph e dell'app installata; l'app è un
secondo prodotto, fuori da questo repository, con il suo rilascio; il contratto API va **versionato** perché l'app e l'hub si
aggiornano separatamente. **Deciso** (Carmine, 15 settembre): in M2 l'hub espone il contratto con i token e i test (T19), e
l'adattamento dell'app Python lo fa **Claude**, nel suo repository, in una fase dopo (T21). La forma del contratto e la verifica della
licenza di Navigraph: nota `2026-09-15-token-personali-e-agente-del-validatore`.

**Vale per tutti i controlli che hanno bisogno del programma** (domanda di Carmine, 15 settembre): sì. Il contratto non conosce i
controlli: porta una `check_key`, un esito e un'evidenza. Aderenza alla rotta, SID e STAR, spazi aerei attraversati, qualunque
controllo che richieda Navigraph o un browser (Eurocontrol) si aggiunge **nel programma**, con una chiave nuova e l'errore
collegato nel catalogo; l'hub non cambia.

**Perché passare dall'hub, se il programma sul PC vede già fix e rotta** (l'altra metà della domanda). Il programma è gli
**occhi** del validatore sulla geometria, ed è giusto che mostri mappa, fix e rotta **in locale**. Ma la **decisione** sta
nell'hub, e deve starci: lì ci sono gli errori del catalogo con i contatori del pilota, la soglia che suggerisce il rifiuto,
«nessuno valida i propri PIREP», la presa in carico, l'audit, la mail, le statistiche. Mandare all'hub **anche** l'esito del
programma costa poco e dà tre cose:

1. il **suggerimento** compare nella pagina di validazione accanto agli altri, invece che su un'altra finestra da ricopiare;
2. l'esito resta **scritto** sul PIREP: una contestazione o un ban si motivano anche con quello;
3. si **misura** quanto il controllo sbaglia (quante volte il validatore conferma l'errore suggerito), cioè il modo per sapere se
   la regola del `DCT` regge davvero al 90 %.

L'alternativa — il programma solo in locale, senza scrivere niente nell'hub — è possibile e più semplice all'inizio, ma perde
queste tre cose. **Deciso** (Carmine, 15 settembre): il programma mostra tutto in locale **e manda subito gli esiti** all'hub, già dalla
prima versione.

**La proposta degli ATC contattati al pilota resta sul server** (§3.3): il pilota non ha Navigraph. È la versione leggera —
aeroporti e FIR attraversati dai punti delle tracce (i FIR da OpenAIP, estensione n.13), incrociati con l'archivio ATC — e al
validatore basta come punto di partenza.

### 6.5 L'archivio ATC

`IAtcActivitySource` nel nucleo («quali posizioni erano online in questo intervallo, e dove»): la vista di vIPI
`v_share_atc_sessions` o nessuna (`Unavailable`). Lo usano la **proposta degli ATC contattati** (§3.3) e, attraverso
l'hub, l'agente del validatore per `atcCoverage` (§6.6).

---

## 7. Permessi

### 7.1 Il catalogo

| Permesso | Che cosa permette |
|---|---|
| `Tours.View` | vedere nel back office bozze, template, liste |
| `Tours.Edit` | creare, modificare, riprogrammare, nascondere tour; leg (ritiro compreso); import; segnalazioni sulle leg; vincoli |
| `Tours.Delete` | eliminare un tour **senza PIREP** |
| `Tours.ManageRules` | regole generali e del tour, parametri, errori |
| `Tours.ManageTemplates` | template di tour |
| `Tours.ManageAircraft` | profili degli aerei |
| `Tours.Validate` | prendere e decidere PIREP; **con scope per tour** (§7.3) |
| `Tours.ReopenDecisions` | riaprire una decisione presa da altri (§4.2.1); negato all'interessato (T13a) |
| `Tours.ManageValidators` | abilitare e togliere validatori |
| `Tours.ViewPilots` | pagina del pilota, statistiche dei validatori |
| `Tours.Ban` | bannare un pilota da un tour o da tutti |
| `Tours.ManageSettings` | impostazioni della divisione |

### 7.2 Chi li ha (`division.json → positionGrants`)

| | Coordinator FOD | Assistant FOD | Advisor FOD | Validatore abilitato |
|---|---|---|---|---|
| `Tours.View` | ✓ | ✓ | ✓ | |
| `Tours.Edit` | ✓ | ✓ | ✓ | |
| `Tours.Delete` | ✓ | ✓ | — (grant al singolo VID) | |
| `Tours.ManageRules` | ✓ | ✓ | ✓ | |
| `Tours.ManageTemplates` | ✓ | ✓ | — | |
| `Tours.ManageAircraft` | ✓ | ✓ | ✓ | |
| `Tours.Validate` | ✓ tutti | ✓ tutti | ✓ tutti | ✓ i tour abilitati |
| `Tours.ReopenDecisions` | ✓ | ✓ | — | le proprie decisioni con `Tours.Validate` |
| `Tours.ManageValidators` | ✓ | ✓ | — | |
| `Tours.ViewPilots` | ✓ | ✓ | ✓ | ✓ (risposta 17) |
| `Tours.Ban` | ✓ | ✓ | — | |
| `Tours.ManageSettings` | ✓ | ✓ | — | |
| rispondere a contestazioni e chiarimenti | ✓ | ✓ | ✓ | ✓ sui propri PIREP decisi |

Scritti in `config/division.json` e in `division.example.json` in T5, con `scope: FOD`; ogni grant del file si applica una volta anche a
un'installazione già avviata (nota `2026-09-16-impostazioni-dei-moduli`). HQ e superadmin tutto. **I validatori si abilitano solo dalla pagina delle statistiche** (risposta 18). **Un validatore che non è
più staff perde l'abilitazione da solo**: il suo grant viene sospeso dalla sincronizzazione dello staff, come ogni grant del nucleo
(Carmine, 15 settembre).

**Precisato in T15a** (Carmine, 23 settembre, nota `2026-09-23-completamento-validatori-piloti-ban`): un validatore si abilita **su un
tour di primo livello** — che copre i suoi sottotour, anche quelli aggiunti dopo — **o su tutti i tour** (un grant senza scope); solo chi
è staff della divisione. «Aggiungi validatore» scrive con `Tours.Validate` anche **`Tours.ViewPilots`** (la colonna qui sopra) se il
membro non ne ha uno suo, e «togli» l'ultimo tour lo toglie solo se l'aveva scritto lui. I grant li scrive il servizio del nucleo
`ModuleGrants`, con le regole della schermata dei permessi.

### 7.3 Le due estensioni del meccanismo dei permessi (estensione del nucleo n.1)

1. **Scope per risorsa**: `hub_user_grants.resource_scope` (`flightops:tour:42`); una risorsa con `IHasResourceScope`
   dichiara il suo; l'handler conta un grant con scope solo sulla risorsa con lo stesso scope. «Aggiungi validatore» scrive
   un grant `Tours.Validate` con lo scope del tour; audit, sospensione e sessione vengono gratis.
   **Precisato in T15a**: lo scope di un PIREP è quello del **tour di primo livello** (`fo_pireps.scope_tour_id`), quindi un
   validatore abilitato su un `Container` valida i suoi sottotour.
2. **Stakeholder della riga**: `IHasStakeholder` (`StakeholderVid`) e i permessi che una risorsa **non** concede al suo
   stakeholder (`Tours.Validate` sul PIREP). Vale per tutti, **superadmin compreso** (risposta 15). La lettura resta
   concessa: il validatore vede i propri PIREP (§4.1).

Tutte e due vogliono una **nota di decisione** e i test della spina dorsale estesi: scritta in T0,
`decisions/2026-09-15-permessi-su-una-riga-e-chi-ha-interesse.md`.

---

## 8. Schermate

### 8.1 Pubblico

- **`/tours`**: tour aperti, in chiusura, e in arrivo con anteprima, come **riquadri** (foto, titolo, riassunto; per un
  pilota barra di avanzamento e prossima leg). Anonimo: niente avanzamento. **Precisato in T15b** (nota
  `2026-09-24-le-pagine-delle-persone` §3.1): il riquadro che il server manda resta uguale per tutti; barra e prossima leg arrivano da
  `GET /api/flightops/my-tours`, chiesto dal componente dei riquadri solo a chi ha fatto login, su `/tours` e nel blocco `tourCards`.
- **`/tours/{slug}`**: briefing, date, aerei, regole effettive con i parametri, **mappa** (blu da fare, verdi fatte, arancioni in
  attesa, grigie non ancora rilasciate; le leg **ritirate non compaiono**, si vedono solo nella mappa dell'editor), elenco delle leg con distanza, **tempo stimato**, callsign suggeriti,
  pulsante **SimBrief**; per un tour a distanza senza leg, i vincoli e quanto manca; per il pilota i suoi PIREP, «Invia il
  report», «Contesta», «Richiedi chiarimenti», «Segnala un problema».
- **Il form del PIREP**: finestra dedicata (prima la scelta del volo, poi i campi).

### 8.2 Blocchi Data

| Blocco | Dove | Che cosa |
|---|---|---|
| `flightops.tourCards` | pagine pubbliche, `/me` | i riquadri, per chi guarda |
| `flightops.myTours` | `/me` | tour iniziati, avanzamento, prossima leg, PIREP da correggere, chiarimenti con risposta; **il riepilogo del pilota** (leg volate, ore stimate, tour completati), senza contatori degli errori. **Nessun elenco pubblico** di chi ha completato un tour: ognuno vede i suoi |
| `flightops.reviewQueue` | dashboard FOD, `/staff` | PIREP in coda sui tour che chi guarda può validare: **una riga per tour**, quanti e da quando il più vecchio, con il link alla coda del tour (Carmine, 23 settembre, T13b) |
| `flightops.openIssues` | dashboard FOD | segnalazioni aperte, contestazioni e chiarimenti senza risposta |
| `flightops.errorCatalog` | pagine, documenti | gli errori pubblici |

**Precisato in T15a** (Carmine, 23 settembre): le ore del riepilogo di `myTours` sono **le ore volate**, dal decollo all'atterraggio
registrati dal tracker sui PIREP accettati, non le ore stimate delle leg. Il blocco, nelle sue due metà, è di T15b.

**Fatto in T15b** (nota `2026-09-24-le-pagine-delle-persone`): `myTours` è `MyToursProvider` sopra `MyTours` (`People/`), la stessa
risposta di `GET /api/flightops/my-tours`: prima ciò che aspetta il pilota (i PIREP da correggere, le risposte da leggere), poi i tour
iniziati con la barra e la prossima leg, poi il riepilogo. Un tour nascosto sparisce dall'elenco e resta nel riepilogo. A un visitatore
il blocco risponde `signedIn: false`.

### 8.3 L'editor del tour

- **`/staff/tours`**: tutti i tour passati, presenti e futuri (stato calcolato, tipo, date, nascosto, iscritti, in coda); filtro
  «template».
- **`/staff/tours/{id}`**, schede: **impostazioni** (form generato, compresi aereo di riferimento, anteprima, procedure,
  ordine delle rotazioni), **briefing** (editor dei blocchi), **hub e rotazioni**, **sottotour**, **vincoli sul callsign**,
  **obiettivo e vincoli** (solo `Open`, §2.6.1), **leg** (§8.4), **regole del tour**. Barra: «Segna pronto» con i problemi, «Nascondi»,
  «Elimina» (solo senza PIREP), «Nuovo da template», «Salva come template».

### 8.4 L'editor delle leg (eccezione dichiarata, estensione n.6)

- **Tabella modificabile**: numero, partenza, arrivo, IATA, distanza e tempo stimato calcolati al volo, callsign suggeriti, numeri
  di volo, aerei, rotazione, rilascio, stato (con i PIREP).
- **Azioni sulla riga**: «aggiungi dopo: duplica», «aggiungi dopo: segue», «chiudi tour», «elimina o ritira» (il server sceglie
  secondo §1.4.1 e lo dice prima di confermare), «ripristina».
- **Import XLSX/CSV**, letto **nel browser** (risposta 24) con **SheetJS**, caricata solo all'import: anteprima delle differenze
  dal server; «fondi» non tocca le leg assenti; «sostituisci» elimina le assenti senza PIREP e ritira quelle con PIREP, con un motivo.
  **La stessa leg è la stessa coppia partenza→arrivo** (abbinata nell'ordine se ripetuta) e l'ordine delle righe è l'ordine del tour:
  nessuna colonna di numeri; una leg ritirata che il file nomina torna nel tour. **Non sui tour `Hub`**. Si applica solo ciò che
  l'anteprima ha mostrato (un'impronta delle leg). Modello di file con le colonne (`departure`, `arrival`, `callsign`,
  `flightNumber`, `aircraft`, `release`; il file porta solo tipi ICAO, i gruppi di una leg restano). Carmine, 22 settembre 2026, nota
  `2026-09-22-l-import-delle-leg`.
- Salvataggio a righe con `row_version`, errori sulla cella.

### 8.5 La validazione

- **`/staff/tours/review`**: la coda (§4.1), unica o per tour, ordinata per data o per tour.
- **`/staff/tours/review/{pirepId}`**: la pagina di validazione (§4.3).

### 8.6 La mappa (estensione n.3)

**Scelta proposta** (risposta 23, «scegli tu»): **MapLibre GL** con una **mappa di base vettoriale ospitata dall'hub** (un file
**PMTiles** di Protomaps, dal livello 0 a circa il 7), servita dal nostro server.

- **Perché**: nessun fornitore esterno, nessuna chiave, nessun limite di richieste, nessun tracciamento dei visitatori, e la CSP
  resta `'self'` per immagini e connessioni. Le tessere di `tile.openstreetmap.org` **non** sono usabili per un sito con
  traffico (policy di uso), e un fornitore commerciale gratuito ha quote e chiavi.
- **Linee ortodromiche** (archi di cerchio massimo), marcatori degli aeroporti, colori per stato.
- **Misurato in T0** (nota `2026-09-15-la-mappa`): il file mondiale fino al livello 7 pesa **179 MB** (fino al 6: 43 MB), e
  Carmine ha scelto il 7. **Correzione**: MapLibre 6 **non ha più una build «CSP»**; il worker è comunque un file servito da noi
  (`worker-src` resta `'self'`), ma `img-src` guadagna `blob:`. Il ripiego su Leaflet è scartato (servirebbe un plugin in manutenzione
  per le tessere vettoriali).
- Componente nuovo nell'elenco chiuso: `RouteMap`.
- **Esteso in T13b**: `RouteMap` disegna anche **una traccia volata** (`tracks`, in rosso sopra le leg, senza marcatori) per la pagina
  di validazione.
- **Corretto il 22 settembre 2026 in T10** (nota `2026-09-22-il-pubblico-dei-tour`): la base **non porta i nomi dei luoghi** —
  terra, acqua e confini fra stati, e basta — quindi sotto `/tiles` non ci sono né caratteri né sprite: **un file solo**,
  `basemap.pmtiles`. Le uniche parole sulla mappa sono i codici degli aeroporti, che sono marcatori HTML del componente.

### 8.7 Le altre pagine dello staff

- **Statistiche dei validatori** (`/staff/tours/validators`): per anno e per validatore leg validate, accettate, rifiutate; per ogni
  tour dell'anno corrente e del precedente, per validatore, lo stesso. Qui «aggiungi validatore» e «togli».
- **Pagina del pilota** (`/staff/tours/pilots/{vid}`): errori confermati per categoria e per errore (anno solare e da sempre),
  tutte le leg volate con esito e validatore, contestazioni, chiarimenti, ban, tour con avanzamento; da qui «banna».
- **Ban** (`/staff/tours/bans`), **regole generali**, **errori**, **template**, **profili degli aerei**, **segnalazioni**,
  **impostazioni**: liste e form generati.
- **Precisato in T15a** (nota `2026-09-23-completamento-validatori-piloti-ban`): le statistiche contano **la decisione che un PIREP
  ha adesso** (accettati, rifiutati, da modificare, per anno di `decided_at`), ed elencano chi ha un grant suo e chi ha deciso
  nell'anno; la pagina del pilota conta gli errori sui PIREP **accettati e rifiutati** (come il suggerimento, T13a) per anno solare del
  decollo, con il nome congelato nei PIREP, e mostra i fili che chi guarda legge nei contatti del suo dipartimento.
- **Precisato in T15b** (Carmine, 24 settembre, nota `2026-09-24-le-pagine-delle-persone`): alla pagina del pilota si arriva da una voce
  **«Piloti»** (`/staff/tours/pilots`, un campo VID) e dai link della pagina di validazione e dei ban; le statistiche dei validatori e la
  pagina del pilota hanno **un selettore d'anno** (`?year=`), il corrente per primo. «Banna» apre il form del ban con il VID scritto e
  torna alla pagina. Voci di menu «Validatori», «Piloti», «Ban», tutte con `Tours.ViewPilots`.

---

## 9. Proiezioni, ricerca, calendario, notifiche

- **Ricerca**: `IProjectable` su un tour visibile (titolo, riassunto, `/tours/{slug}`).
- **Calendario**: **due voci** per tour, rilascio e chiusura (risposta 20). ⚠️ `ProjectionSnapshot` oggi porta **una**
  `CalendarProjection`: diventa un **elenco** (estensione n.10, piccola). Un tour nascosto non proietta niente.
- **Notifiche** del modulo: `pirepAccepted`, `pirepToModify`, `pirepRejected`, `threadReplied` (contestazione o
  chiarimento), `banned`; alla casella del FOD: `threadOpened`, `legIssueReported`.

---

## 10. Conservazione (risposta 22)

- **Mai cancellati**: la decisione del PIREP, gli **errori confermati**, la storia, i ban — il **registro disciplinare**.
- **Cancellati** dopo **13 mesi** dalla chiusura per un tour normale e **25 mesi** per un tour su più di un anno: dei PIREP
  tracce, tutte le revisioni dei piani, esiti dei controlli, snapshot delle regole, note, ATC ed esenzioni; del tour briefing,
  foto (se non usata altrove), regole del tour, hub, rotazioni, vincoli, iscrizioni.
- **Meteo**: §1.13.
- **Le tracce** vanno molto prima: **`trackRetentionDays` (90) dopo la decisione** di un PIREP accettato o rifiutato e non
  contestato, o dopo il ritiro (Carmine, 23 settembre: «non possiamo avere GB di dati di tracce inutili»). Job giornaliero
  `TrackRetentionJob` (T13a). Restano PIREP, piani, errori ed esiti dei controlli.
- Job mensile del modulo, con una riga nel log dei job.

### 10.0 La richiesta di cancellazione dei dati

Alla richiesta di un pilota (GDPR) si cancellano i suoi PIREP e i suoi dati personali del modulo; **il registro disciplinare resta,
anonimizzato** (esiti ed errori senza il VID), perché serve alle statistiche e ai contatori aggregati (Carmine, 15 settembre). ⚠️ È
una regola che conferma la **direzione**, perché è una questione legale; e va coordinata con come il nucleo tratta la stessa richiesta
per gli altri dati dell'utente (oggi l'export dei dati utente è scartato, la cancellazione non è descritta).

### 10.1 Il problema: il registro disciplinare punta al tour

**Perché la domanda** (Carmine ha chiesto di argomentarla): il registro disciplinare **non si cancella mai**, ma ogni sua riga è
un PIREP che dice «il pilota X, sulla leg Y del tour Z, il giorno D, ha commesso gli errori E». Se dopo 13 mesi cancelliamo il
tour e le sue leg, quelle righe restano **orfane**: sappiamo che il pilota ha preso un warning, ma non **dove**. E due cose che
lo staff ha chiesto ne hanno bisogno:

- la **pagina del pilota** mostra «tutte le leg volate con l'esito» (requisiti §6), anche degli anni passati;
- una **contestazione** o una decisione di **ban** si motiva guardando lo storico: «il terzo warning per SID sbagliata, sulla leg
  LIRF–LIMC del tour Alitalia 2027».

**Le due strade**:

| | Come | Pro | Contro |
|---|---|---|---|
| **A. Copiare il testo** | alla cancellazione, il PIREP si copia nome del tour, numero della leg, partenza e arrivo come testo | il tour sparisce davvero | codice di copia che gira una volta all'anno, e se si dimentica un campo lo si scopre anni dopo |
| **B. Tenere le righe leggere** | `fo_tours` e `fo_legs` **non si cancellano**: diventano archiviate; si cancella solo ciò che pesa (sopra) | nessuna copia, lo storico resta navigabile | le righe restano nel database |

**Quanto pesano davvero** (stima): una riga di tour qualche kilobyte, una leg qualche centinaio di byte. Con 20 tour e 30 leg per
tour all'anno sono **decine di kilobyte all'anno**. Quello che occupa spazio sono **le tracce e i piani di volo**: migliaia di
punti per volo per circa 7000 PIREP all'anno, **centinaia di megabyte o più**, ed è esattamente ciò che la conservazione cancella
comunque.

**Decisa: B** (Carmine, 15 settembre). Tour e leg restano come righe archiviate; **quando il tour scade** (cioè quando passa
il periodo di conservazione, 13 o 25 mesi dalla chiusura, confermato il 15 settembre) vanno via tracce, piani e tutto ciò che pesa. Il registro
disciplinare resta leggibile per sempre, senza codice di copia. Le immagini del tour seguono §1.14.

---

## 11. Che cosa chiede al nucleo

| # | Estensione | Nota di decisione | Dove |
|---|---|---|---|
| 1 | Scope per risorsa nei grant e stakeholder della riga | sì | §7.3 |
| 2 | Contatti con filo di risposte (contestazioni e chiarimenti), legati a una riga di modulo, con partecipanti in più | sì | §3.8, §3.10 |
| 3 | Mappa: MapLibre, PMTiles ospitate, CSP | sì | §8.6 |
| 4 | Aeroporti del mondo con IATA e coordinate; piste con le testate | no (risposta C.4) | §1.12 |
| 5 | Tracker nel client IVAO: sessioni, tutte le revisioni dei piani, tracce | no (risposta C.5) | §3.2, §6 |
| 6 | Editor a tabella e import XLSX/CSV | eccezione da dichiarare | §8.4 |
| 7 | Award: catalogo e assegnazioni, schermata, immagini dalla media library (IVAO: documentazione 403, caricamento a mano) — **fatta in T4b**: catalogo del dipartimento, assegna chi ha `Awards.Assign`, la segnalazione propone l'award | sì, breve: `2026-09-16-award-e-preferenze` (piano 0.82) | §3.11 |
| 8 | `IAtcActivitySource` sulla vista di vIPI | coperta da piano 0.78 | §6.5 |
| 9 | Selezione multipla e parametri da schema nel generatore di form; aggregati nella lista — **verificata in T9: non serve**, il generatore li sa già disegnare (`KindPicker`, `multi`) e gli aggregati sono `ToListPage` | no (`2026-09-22-regole-ed-errori` §3) | §5.1, §8.7 |
| 10 | Più voci di calendario per riga in `IProjectable` | no (estensione piccola) | §9 |
| 11 | Tipi di aereo IVAO (`ref_ivao_aircraft`) da `/v2/aircrafts/all`, con equipaggiamenti e transponder | no | §1.5 |
| 12 | `IWeatherSource` (NOAA → IVAO → VATSIM), come vIPI | sì, breve (una fonte esterna nuova) | §1.13 |
| 13 | Confini dei FIR da **OpenAIP** (`ref_firs`: codice, paese, poligono), sincronizzati da un job con la chiave API nei segreti, per la proposta degli ATC contattati; licenza e attribuzione dei dati OpenAIP da verificare | sì, breve (una fonte esterna nuova) | §3.3 |
| 14 | **Token personali per un agente esterno** (creati dall'utente, revocabili, con scadenza, con i suoi permessi, auditati) e il contratto versionato dell'agente del validatore — **il nucleo fatto in T19a**: `IModule.TokenAudiences`, `PersonalTokenPolicy.For(audience)`, `/me/tokens` (nota `2026-09-24-i-token-personali`) | sì | §6.6 |
| 15 | **Preferenze dell'utente** generiche (chiave e valore per utente), per l'ordine della coda del validatore — **fatta in T4b**: chiavi dichiarate da `IModule.Preferences` | nella stessa nota di T4b, §4 | §4.1 |
| 16 | **Usi dei file con scadenza** dalle righe dei moduli nell'indice della media library (`MediaReferences` in `IProjectable`), e il **job che elimina i file con tutti gli usi scaduti** (servirà anche agli eventi) | breve (estende G20, ma elimina file da solo) | §1.14 |

---

## 12. Tabelle e migrazioni

**Nucleo** (additive): `ref_ivao_airports` (+ `iata`, `latitude`, `longitude`), `ref_ivao_runways`, `ref_ivao_aircraft`,
`hub_user_grants` (+ `resource_scope`), `cms_contact_messages` (+ `kind`, `participants_json`), `cms_contact_references` (un messaggio cita uno o più oggetti di
modulo: `source_module`, `source_id`), `cms_contact_replies`, `hub_awards`, `hub_award_assignments`, `cms_award_signals` (+ `award_id`, T4b), `cms_media_uses`, `hub_user_preferences`,
`hub_personal_tokens`. (I contatti stanno in `cms_`: la prima stesura scriveva `hub_`, T0 l'ha corretto leggendo il codice.)

**Modulo** (`Initial`): `fo_tours`, `fo_hubs`, `fo_rotations`, `fo_legs`, `fo_callsign_rules`, `fo_tour_constraints`,
`fo_aircraft_profiles`, `fo_rules`, `fo_errors`, `fo_rule_errors`, `fo_pireps`, `fo_pirep_flights`, `fo_pirep_errors`,
`fo_pirep_events`, `fo_check_results`, `fo_enrolments`, `fo_bans`, `fo_leg_issues`, `fo_weather_reports`. Nel nucleo
anche `ref_firs` (confini dei FIR da OpenAIP). (Scritte fase per fase: `fo_leg_issues` e le colonne della contestazione su `fo_pireps`
sono `AddDisputesAndLegIssues`, T14b.)

**vIPI** (nel suo repository): `v_share_atc_sessions` e l'utente di sola lettura.

---

## 13. La rete di test

- **Voli veri**: Carmine ha offerto **una serie di voli** (15 settembre). Servono **con l'esito atteso** per controllo
  («disconnessione di 18 minuti», «decollo da intersezione», «livello pari verso est»), meglio se con il PIREP e la decisione
  reale: diventano il **corpus** dei controlli, sono la base per tarare tempo stimato e tolleranze, e si aggiungono ai 39 log di
  Toursystem. Le tracce si salvano come fixture del client IVAO, così i test non chiamano IVAO.
- **Unità**: `TourRules` per ogni tipo e per il ritiro delle leg; vincoli del tour a distanza; GCD e tempo stimato; regole
  effettive con parametri; suggerimento; limiti giornalieri; ogni `IFlightCheck` sul corpus.
- **Integrazione** (MariaDB vera, database condiviso: VID `780001–780099`, slug `fo-test-…`): ciclo del PIREP con tutti gli stati;
  nessuno valida i propri (superadmin compreso); validatore abilitato su un tour e non su un altro; sessione rivendicata una volta;
  limite che blocca l'invio; limite di divisione che non si spegne; contestazione che sblocca le successive; ban; tour con PIREP
  che non si elimina e chiusura non anticipabile sotto `2 × X`; leg ritirata che non conta; completamento con segnalazione award;
  conservazione che non tocca il registro disciplinare; meteo cancellato solo a PIREP decisi.
- **Architettura**: il modulo non nomina vIPI, NOAA, VATSIM; non chiama IVAO se non dal client del nucleo.
- **Smoke**: `/tours`, `/tours/{slug}` con la mappa, form del PIREP, coda unica e per tour, pagina di validazione, editor delle leg
  con un import.
- **Giro completo**: tour da template, PIREP con tracker finto, validato, contestato, riaperto.
- **Divisione XX**: nessuna stringa italiana né ICAO italiano nei seed; `northSouthLevelCountries` vuoto di default.
- **Architettura**, in più: il modulo non nomina OpenAIP (i FIR arrivano da `ref_firs` del nucleo).

---

## 14. Ordine di lavoro proposto (scritto in dettaglio in `06` parte C, fase T0)

> **Dal 16 settembre 2026 le fasi vere stanno in `06-piano-implementazione-m2.md` parte C.** Tre scostamenti da questa tabella: `myTours`
> passa da T10 a T15, `fo_bans` nasce in T11 (la schermata resta in T15), e nasce **T21** (l'app del validatore, fuori dal repository).

| Fase | Contenuto |
|---|---|
| T0 | Note di decisione: permessi con scope e stakeholder; contatti con risposte; mappa (con le misure); fonti esterne (meteo, FIR di OpenAIP); token e contratto dell'agente del validatore; file con scadenza nella media library. Piano 0.79 |
| T1 | Nucleo: aeroporti del mondo con IATA e coordinate, piste con le testate, tipi di aereo, confini dei FIR da OpenAIP |
| T2 | Nucleo: tracker nel client IVAO (sessioni, piani, tracce) con fixture; `IWeatherSource` |
| T3 | Nucleo: scope per risorsa e stakeholder nell'unico handler, test della spina dorsale |
| T4 | Nucleo: award (catalogo, assegnazioni, schermata); più voci di calendario per riga; usi dei file con scadenza e job di eliminazione; preferenze dell'utente |
| T5 | Modulo: scheletro, impostazioni, profili degli aerei, `positionGrants` |
| T6 | Tour: modello, stato dalle date, nascondere/eliminare/chiusura, controlli «pronto», template. **Divisa** il 16 settembre in T6a (server e schermate generate) e T6b (la scheda del briefing con l'editor dei blocchi) |
| T7 | Leg: editor a tabella, GCD e tempo stimato, ritiro, hub e rotazioni, sottotour, callsign, vincoli a distanza — **divisa in T7a (le leg) e T7b (hub, sottotour, callsign, `Open`)** il 18 settembre; **T7b divisa ancora** il 21 settembre: il tour `Open` è **T7c** |
| T8 | Import XLSX/CSV con anteprima |
| T9 | Regole con parametri ed errori, regole effettive, blocco `errorCatalog` |
| T10 | Pubblico: `/tours`, `/tours/{slug}`, mappa, `tourCards`, `myTours` |
| T11 | PIREP: ricerca nel tracker, form, procedure per regole di volo, deviazioni, limiti che bloccano, ban, `TourRules` |
| T12 | ATC contattati proposti, esenzioni; `IAtcActivitySource` con vIPI |
| T13 | Validazione: code, presa in carico, pagina, suggerimenti, mail, `reviewQueue` |
| T14 | Contatti con risposte: contestazioni (che sbloccano) e chiarimenti; segnalazioni sulle leg |
| T15 | Completamento e award; statistiche dei validatori; pagina del pilota; ban |
| T16 | Meteo salvato (job, scarico all'invio, cancellazione) |
| T17 | Controlli automatici: motore, job, controlli sul piano |
| T18 | Controlli sulle tracce: disconnessioni, parcheggio, 250 kt, sim rate, atterraggio, decollo dalla testata, `vmc` |
| T19 | Nucleo e modulo: token personali e contratto dell'agente del validatore (lettura del PIREP, scrittura degli esiti), con i test; l'adattamento dell'app Python fuori da questo repository. Divisa il 24 set 2026 in **T19a** (nucleo) e **T19b** (modulo) |
| T20 | Conservazione, calendario, ricerca, rifiniture, giro completo |

---

## 15. Aperte

### 15.1 Decise il 15 settembre (sulle 25 domande della prima bozza)

1 anteprima come opzione del tour · 2 template con sole impostazioni e regole · 3 `is_public` per errore · 4 procedure come
bandiera del tour, secondo Y/Z · 5 callsign e aereo bloccano · 6 limite di divisione blocca · 7 contestazione entro X giorni,
impostazione unica · 8 anno del volo · 9 «scegli una leg» · 10 ordine delle rotazioni fisso o libero, scelto dal tour · 11 anello
obbligatorio · 12 GCD, leg libere, niente rotta ripetuta · 13 sottotour eredita, senza award · 14 blocco dopo la tolleranza tranne
i liberi · 15 nemmeno il superadmin · 16 un tour con PIREP si nasconde e non si elimina, chiusura non prima di 2X giorni · 17 advisor e
validatori vedono pilota e statistiche · 18 validatori solo dalle statistiche · 19 niente punti né classifiche, mai · 20 due voci di
calendario · 21 niente mail di completamento · 22 13 e 25 mesi · 23 la mappa la sceglie Claude · 24 XLSX letto nel browser · 25 coda
dal PIREP più vecchio, più code per tour e ordine a scelta.

### 15.2 Ancora da decidere

1. ~~**`ZZZZ`**~~ **deciso il 15 settembre**: errore solo se `ZZZZ` è l'alternato e manca `ALTN/` nelle remarks (§6.4).
   **FRA**: nessun volume; livelli semicircolari controllati sui voli in `DCT`, FIR da OpenAIP (§6.4). **Tipi di aereo**:
   dagli endpoint `/v2/aircrafts` (§1.5).
2. ~~**Tour nascosto**~~ **deciso**: non lo vede più nessuno fuori dallo staff (§1.2.2).
3. ~~**Eliminare una leg senza PIREP**~~ **deciso**: le successive si rinumerano (§1.4.1).
4. ~~**Leg ritirata dentro una rotazione**~~ **deciso**: si ritira la rotazione intera e se ne crea una nuova.
5. ~~**Tempo stimato**~~ **deciso**: 5 % + 20 minuti configurabili dal FOD, solo un'informazione per il pilota; **tarati a 5 % + 15** sul
   corpus in T18.
6. ~~**Aereo di riferimento**~~ **deciso**: facoltativo; se c'è, stime per leg e totale del tour, ricalcolate a ogni lettura.
7. ~~**Tour a distanza**~~ **deciso**: A→B e B→A sono rotte diverse.
8. ~~**Tour `Open`**~~ **deciso**: tutti gli obiettivi, filtri e regole di §2.6.1 in M2.
9. ~~**Contestazione respinta**~~ **deciso**: la leg torna a bloccare, con la tolleranza contata da quel momento.
10. ~~**Ban**~~ **deciso**: HQ, superadmin, FOC, FOAC; mail al pilota con il motivo; i PIREP già inviati si validano.
11. ~~**Richiedi chiarimenti**~~ **deciso**: su PIREP, leg e regole, anche più spiegazioni in un messaggio (§3.10).
12. ~~**Meteo**~~ **deciso**: job ogni 30 minuti sugli aeroporti delle leg dei tour aperti, più lo scarico all'invio.
13. ~~**Soglie VMC**~~ **decise**: quelle standard (5 km, nubi a 1500 ft) come parametri della regola generale.
14. ~~**FRA**~~ **decisa**: nessun volume FRA; il controllo dei livelli semicircolari lavora sui voli in `DCT` (circa 90 % in
    FRA, il resto lo valuta il validatore) e prende il paese dai FIR di OpenAIP (§6.4). Da verificare licenza e attribuzione
    dei dati OpenAIP.
15. ~~**Decollo dalla testata**~~ **deciso**: 150 m, uno per il sistema, lo cambiano FOC e FOAC.
16. ~~**ATC contattati**~~ **deciso**: versione leggera sul server per il pilota; `atcCoverage` e `semicircularLevels` sull'agente del
    validatore con Navigraph (§6.6). **Decisi in T0**: l'app Python la adatta Claude dopo T19 (Carmine, 15 settembre); la licenza di
    Navigraph è verificata a metà, e la forma ammessa delle evidenze sta nella nota `2026-09-15-token-personali-e-agente-del-validatore`.
17. ~~**Ordine della coda**~~ **deciso**: preferenza dell'utente.
18. ~~**Advisor**~~ **deciso**: gestiscono i profili degli aerei; le stime dei tour pubblicati cambiano con le velocità.
19. ~~**Registro disciplinare**~~ **deciso**: strada B; tracce e dati pesanti vanno via alla fine della conservazione (§10.1).
21. ~~**Immagini dei tour**~~ **deciso**: un job del nucleo le elimina un mese dopo la chiusura, se non servono ad altro; lo stesso
    meccanismo per gli eventi (§1.14). Il collegamento al tour lo fa chi modifica il tour (**deciso** da Carmine il 15 settembre).
22. ~~**Agente del validatore**~~ **deciso**: manda subito gli esiti all'hub (§6.6).
20. **I voli di test**: in arrivo fra il 16 e il 17 settembre, con un esito dettagliato. ⚠️ Oggi la validazione è soggettiva: gli
    esiti attesi vanno scritti secondo lo **standard** che il sistema vuole fissare, non secondo com'è stato deciso allora.
