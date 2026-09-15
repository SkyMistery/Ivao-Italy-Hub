# IVAO Division Hub — Design di M2 (il modulo dei tour)

> Documento **interno** (italiano). Fonte di verità: `00-piano-di-progettazione.md` (versione 0.78).
> Ingresso: **`decisions/2026-09-14-requisiti-dei-tour.md`** — i requisiti decisi da Carmine e dallo staff
> FOD. Questo documento li trasforma in modello, flussi, permessi, schermate e fasi; **non li ridiscute**.
> Dove resta una scelta aperta è segnata **⚖️** e raccolta in §15.
> Le fasi di implementazione si scrivono nella parte C di `06-piano-implementazione-m2.md` **dopo** la
> revisione di Carmine.

**Stato:** seconda bozza, 15 settembre 2026. Integra la revisione di Carmine del 15 settembre (le 25 risposte
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
- Le righe che il pilota scrive (PIREP, segnalazioni) sono **`ISubmittedByMembers`**.
- **`IVisible`**: un tour visibile al pubblico è `Public`; bozze, template e tour nascosti `Staff`; un PIREP è
  `Members`, ristretto al suo pilota o a chi ha i permessi di §7.

### 1.2 Il tour — `fo_tours`

| Colonna | Tipo | Note |
|---|---|---|
| `id`, `slug` | | `slug` unico fra i non template: `/tours/{slug}` |
| `is_template` | bool | §1.10 |
| `kind` | enum | `Sequential`, `Free`, `Hub`, `SequentialChosenStart`, `Distance`, `Container` (§2) |
| `parent_tour_id` | long? | solo per un **sottotour**; il padre è un `Container` |
| `required_subtours` | int? | solo `Container` |
| `required_nm` | int? | solo `Distance` |
| `distance_mode` | enum? | solo `Distance`: `PublishedLegs` o `OpenFlying` (§2.6) |
| `title`, `summary` | `Localized<string>` | |
| `briefing_json` | BlockDocument | testo ricco, stesso editor e renderer |
| `cover_media_id` | long? | foto di sfondo |
| `status` | enum | `Draft`, `Ready` |
| `is_hidden` | bool | nascosto (§1.2.2) |
| `show_preview` | bool | un tour pronto è visibile al pubblico **prima** del rilascio, come anteprima (risposta 1) |
| `release_at`, `close_at` | UTC | |
| `report_window_days` | int | X: giorni per inviare il PIREP **e** finestra di ricerca nel tracker |
| `progression` | enum | `FlyAhead` o `WaitForValidation` |
| `hub_rotation_order` | enum? | solo `Hub`: `Fixed` (rotazioni nell'ordine del tour) o `Free` (a scelta dentro l'hub) (risposta 10) |
| `requires_procedures` | bool | SID, STAR e IAP obbligatori nel PIREP (risposta 4, §3.2) |
| `daily_leg_limit` | int? | obbligatorio se il limite di divisione è spento (§3.7) |
| `allowed_aircraft_json` | string[] | tipi ICAO consentiti; vuoto = tutti |
| `reference_aircraft_icao` | string? | l'aereo di riferimento per il tempo stimato (§1.5) |
| `award_id` | long? | solo su un tour senza padre (un sottotour non ha award, risposta 13) |
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

Un tour su due anni è un tour con `close_at` nell'anno dopo. **Segnare «pronto»** passa dai controlli di
pubblicazione: titolo e riassunto in tutte le lingue della divisione, almeno una leg (tranne `Container` e
`Distance/OpenFlying`), aeroporti noti in `ref_`, `release_at < close_at`, date delle leg dentro il periodo,
vincoli di tipo (§2), limite giornaliero se quello di divisione è spento (§3.7), aereo di riferimento con un
profilo (§1.5) ⚖️, un `Container` con almeno due sottotour e `required_subtours` non oltre il loro numero.

#### 1.2.2 Cancellare, nascondere, spostare la chiusura (risposta 16)

- **Un tour senza PIREP** si **elimina** (`Tours.Delete`), in qualunque stato: sparisce con leg, regole e hub.
- **Un tour con almeno un PIREP non si elimina mai.** Si può solo **nascondere** (`is_hidden`, `Tours.Edit`):
  - sparisce da `/tours`, dai blocchi, dalla ricerca e dal calendario;
  - ⚖️ i piloti che lo hanno iniziato lo vedono ancora nella loro pagina e nei loro PIREP, ma **non** possono
    inviarne di nuovi;
  - i PIREP in coda si validano normalmente;
  - il tour arriva alla sua **naturale scadenza**, e poi la conservazione (§10) lo toglie.
- **La data di chiusura si può cambiare** anche a tour aperto, ma la nuova data dev'essere **almeno
  `2 × report_window_days` giorni da oggi**, così nessun pilota si trova il tour chiuso sotto i piedi.

### 1.3 Hub e rotazioni — `fo_hubs`, `fo_rotations`

- `fo_hubs`: `tour_id`, `icao`, `sort`. Solo `Hub`.
- `fo_rotations`: `tour_id`, `hub_id`, `sort`, `size` (2, 4 o 6; il controllo di pubblicazione verifica che
  abbia esattamente `size` leg, parta dall'hub e ci torni).
- **Il collegamento fra hub è una leg** con `kind = HubConnection`: conta come una leg normale. Due hub con
  una leg di collegamento sono **collegati**, senza sono **liberi**.

### 1.4 La leg — `fo_legs`

| Colonna | Note |
|---|---|
| `tour_id`, `number` | ordine nel tour, unico per tour |
| `kind` | `Normal` o `HubConnection` |
| `rotation_id`, `seq_in_rotation` | per `Hub` |
| `departure_icao`, `arrival_icao` | |
| `departure_lat/lon`, `arrival_lat/lon` | **congelate alla scrittura** dagli aeroporti `ref_` (ADR-024) |
| `distance_nm` | GCD calcolata dal server |
| `estimated_minutes` | tempo stimato (§1.5), ricalcolato quando cambiano distanza o aereo di riferimento |
| `real_callsign`, `flight_number` | del volo reale, se esiste (informativi; il vincolo è in §1.6) |
| `aircraft_json` | tipi ICAO della leg; vuoto = quelli del tour |
| `release_at` | rilascio proprio, facoltativo |
| `retired_at`, `retired_reason` | §1.4.1 |
| `change_reason` | obbligatorio quando si modifica una leg che ha PIREP: finisce nell'audit |

Le comodità dell'editor («duplica», «segue», «chiudi tour») non si memorizzano: compilano la leg nuova (§8.4).

#### 1.4.1 Che cosa succede quando si toglie una leg

Tre casi, decisi dal server e non dall'editor:

| Situazione | Che cosa succede |
|---|---|
| **La leg non ha PIREP** (in qualunque stato del tour) | si **elimina davvero**; le leg dopo di lei **non** si rinumerano da sole ⚖️ (un buco nel numero è meglio di numeri che cambiano sotto i piloti) |
| **La leg ha PIREP** | non si elimina: si **ritira** (`retired_at`, `retired_reason` obbligatorio) |
| **Il tour ha PIREP ma la leg no** | come il primo caso: si elimina |

Una leg **ritirata**:

- **non si vola più**: sparisce dalle leg volabili e il form del PIREP la rifiuta;
- **resta visibile** nella pagina del tour, barrata, con il motivo, e sulla mappa in grigio;
- **i PIREP già inviati restano e si validano normalmente**: un pilota che l'ha volata prima del ritiro non
  perde niente;
- **non conta più per il completamento**: «tutte le leg» vuol dire tutte le leg **non ritirate**. Chi l'aveva già
  accettata la tiene nel suo storico, ma non gli serve;
- **in un tour in sequenza viene saltata**: la prossima leg di chi era fermo lì è quella dopo;
- **in una rotazione** rende la rotazione incompleta: il controllo di pubblicazione lo segnala, e il FOD deve
  sistemare la rotazione (aggiungere una leg o ritirare la rotazione intera) ⚖️;
- **si può ripristinare** (`retired_at = null`), con un motivo, finché il tour non è chiuso.

Nell'**import** (§8.4) la modalità «sostituisci» applica le stesse regole: elimina le leg assenti senza PIREP e
ritira quelle con PIREP, mostrando la differenza prima di applicare.

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
- **Ogni tour ha un aereo di riferimento** (`reference_aircraft_icao`) e ogni leg il suo **tempo stimato**:

  **minuti = 60 × GCD × (1 + k) / velocità + c**

  con `k` e `c` nelle impostazioni (§1.11). **Proposta**: `k = 5 %`, `c = 20 minuti`.

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
  | 2000 NM | 5 h 20 | 4 h 40 | 4 h 35–4 h 50 |

  ⚖️ I due numeri si **tarano sui voli veri** del corpus di test (§13): si confronta la stima con la durata delle
  sessioni del tracker e si sceglie la coppia che sbaglia meno.

- Il tempo stimato si mostra nella pagina del tour e nell'editor; **non** è un vincolo del PIREP ⚖️.

### 1.6 Vincoli sul callsign

- `fo_callsign_rules`: `tour_id`, `leg_id?`, `mode` (`Allow`, `Deny`), `match` (`Prefix`, `Exact`, `Pattern`),
  `value`. Si impostano **per tour, per sottotour o per leg**. Il consentito di una leg è l'unione di leg, tour e
  tour padre; un `Deny` vince sempre.
- **È un vincolo che blocca l'invio** (risposta 5, §3.2).

### 1.7 Regole, parametri ed errori — `fo_rules`, `fo_errors`, `fo_rule_errors`

- **`fo_rules`**: `tour_id?` (null = **generale**), `code` («GR4», «IR3»), `title`, `text` (`Localized<string>`,
  markdown), `amends_rule_id?`, **`check_key?`**, **`parameters_json`**, `sort`, `retired_at`.
- **I numeri del regolamento sono parametri delle regole** (ADR-027, e le richieste del 15 settembre: finestre di
  disconnessione e tempo minimo di parcheggio configurabili come regola generale **e** come regola del tour).
  Una regola generale collegata a un controllo porta i valori della divisione
  (`{"maxSingleDisconnectMinutes": 15, "maxTotalDisconnectMinutes": 25}`); una regola del tour che la **emenda** ne
  cambia uno o più. Il controllo legge i parametri dalle **regole effettive** della leg (§5.2), quindi **le soglie
  non stanno nelle impostazioni**: stanno dove il FOD le scrive e il pilota le legge. Lo schema dei parametri è
  dichiarato dal controllo (§6.2), e il form della regola lo disegna.
- **`fo_errors`**: catalogo **della divisione**. `name` (`Localized<string>`), `description` ed `examples`
  (`Localized<string>`), `category` (`Info`, `Warning`, `Dangerous`), `yearly_max` (solo `Warning`), `check_key?`,
  **`is_public`** (risposta 3), `retired_at`.
- **`fo_rule_errors`**: molti a molti. Il sistema segnala regole senza errori ed errori senza regole.
- **«Copia le regole da un altro tour»** copia le regole del tour, con parametri e collegamenti.

### 1.8 Il PIREP — `fo_pireps`, `fo_pirep_flights`, `fo_pirep_errors`, `fo_pirep_events`

**`fo_pireps`**

| Colonna | Note |
|---|---|
| `tour_id`, `leg_id?`, `vid` | `leg_id` è null solo in un tour `Distance/OpenFlying` (§2.6) |
| `status`, `is_disputed` | §3.1 |
| `submitted_at`, `resubmitted_at?` | |
| `flight_rules` | `I`, `V`, `Y`, `Z`, dal piano al decollo |
| `sid`, `star`, `approach` | §3.2 |
| `atc_contacts_json` | ATC contattati: callsign, frequenza, `proposed` (dal sistema) o `added` (dal pilota) (§3.3) |
| `atc_exemptions_json` | autorizzazioni ricevute: posizione, tipo, nota |
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

### 1.9 L'iscrizione — `fo_enrolments`

Il **primo PIREP iscrive**. `vid`, `tour_id`, `started_at`, `start_leg_id?` (per `SequentialChosenStart`, fissa),
`hub_order_json` (per `Hub`), `completed_at?`. **L'avanzamento non si memorizza**: si calcola dai PIREP.

### 1.10 I template di tour (risposta 2)

- Una riga di `fo_tours` con `is_template = true`, mai pubblica, senza date.
- **Copia solo impostazioni e regole**: tipo, progressione, finestra, limite, `requires_procedures`, aerei, aereo di
  riferimento, vincoli sul callsign del tour, regole del tour con parametri e collegamenti, briefing e foto ⚖️.
  **Niente leg, hub, rotazioni, sottotour.**
- Solo coordinator e assistant FOD (`Tours.ManageTemplates`).

### 1.11 Le impostazioni della divisione per i tour

In `hub_division_settings` sotto la chiave `flightops` (dati che il FOD cambia dall'interfaccia):

| Impostazione | Valore proposto |
|---|---|
| `dailyLegLimit` | 10 (null = spento, §3.7) |
| `defaultReportWindowDays` | 7 |
| `disputeWindowDays` | 7 (risposta 7: uguale per tutti i tour) |
| `rejectGraceHours` | 12 |
| `leaseMinutes` | 30 |
| `durationFactor`, `durationFixedMinutes` | 0,05 e 20 (§1.5) |
| `retentionMonths`, `retentionMonthsLong` | 13 e 25 (§10) |
| `thresholdToleranceMeters` | 150 ⚖️ (§6.4) |
| `weatherRetentionDays` | la finestra massima dei tour aperti (§1.13) |

Le **soglie dei controlli** non stanno qui: sono parametri delle regole (§1.7).

### 1.12 Aeroporti e piste di tutto il mondo (estensione del nucleo n.4)

- Il job di sincronizzazione legge **`/v2/airports/all`** invece degli aeroporti del paese.
- `ref_ivao_airports` guadagna `iata`, `latitude`, `longitude` (migrazione additiva). Nuova `ref_ivao_runways`
  (`airport_icao`, `runway`, `latitude`, `longitude` **della testata**, `bearing`, `length`, `width`), da
  `/v2/airports/{icao}/runways` (i campi li legge già Toursystem). ⚖️ Le piste di tutto il mondo sono molte
  chiamate: si scaricano **per gli aeroporti delle leg dei tour** e degli aeroporti toccati dai PIREP, non per tutti.
- Numeri opzionali con convertitori tolleranti (ADR-049).
- ⚠️ `FirDirectory` e `networkStats` oggi deducono «gli aeroporti della divisione» dalla tabella: con tutto il mondo
  dentro devono filtrare per paese. Da verificare nella fase che lo fa.

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
  1. un job ogni **30 minuti** ⚖️ salva METAR e TAF degli **aeroporti delle leg dei tour aperti o in chiusura**;
  2. **all'invio di un PIREP**, il server scarica anche gli aeroporti **toccati dal volo** che non erano in elenco
     (deviazione, tour a distanza senza leg), chiedendo a NOAA la **storia dei METAR** delle ore del volo ⚠️
     (il parametro `hours` di NOAA va verificato: fin dove arriva indietro). Il TAF di un volo passato, per questi
     aeroporti, non c'è: il validatore lo vede scritto.
- **Quando si cancella** (la regola di Carmine): un bollettino si elimina quando è più vecchio di
  `weatherRetentionDays` **e** tutti i PIREP con un volo in quel giorno su quell'aeroporto sono decisi. Un job
  giornaliero.
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

- **All'inizio** il pilota sceglie un hub.
- **Dentro l'hub**: con `hub_rotation_order = Fixed` le rotazioni nell'ordine del tour, con `Free` a scelta; **ogni
  rotazione sempre in ordine** (risposta 10).
- **Cambio di hub**: finite tutte le rotazioni. Con leg di collegamento, solo verso un hub collegato non fatto, volando
  la leg; senza, a scelta fra i non fatti.
- **Finito**: tutti gli hub.

### 2.4 `SequentialChosenStart` — in sequenza con partenza a scelta

- Il primo PIREP fissa `start_leg_id`; poi in ordine fino all'ultima, e dalla prima fino a quella prima della partenza.
- **Il tour dev'essere ad anello** (risposta 11): controllo di pubblicazione.

### 2.5 Il rifiuto e la tolleranza

In **tutti i tipi tranne `Free` e `Distance/OpenFlying`** (risposta 14 e revisione del 15 settembre §4.4): una leg
rifiutata **blocca** l'invio di PIREP sulle leg successive finché non è di nuovo in attesa o accettata, **eccetto** le
leg il cui decollo è avvenuto entro `decided_at + rejectGraceHours`. Nei tour liberi la leg si rivola quando il pilota
vuole, e senza non si completa.

### 2.6 `Distance` — a distanza

Due modalità (`distance_mode`):

- **`PublishedLegs`**: leg pubblicate, volabili libere (risposta 12). **Finito**: somma delle GCD delle leg accettate
  `≥ required_nm`. Il controllo di pubblicazione verifica che tutte insieme ci arrivino.
- **`OpenFlying`**: **nessuna leg pubblicata**; ognuno vola quello che vuole, con **vincoli del tour**
  (`fo_tour_constraints`, §2.6.1). Il PIREP non ha `leg_id`: porta partenza e arrivo del volo. **Finito**: somma delle
  GCD dei voli accettati `≥ required_nm`.

In tutti e due si conta **la GCD**, mai la distanza volata (chi allunga non guadagna), e **non si vola due volte la
stessa rotta** (risposta 12) ⚖️ (A→B e B→A sono la stessa rotta?).

#### 2.6.1 I vincoli di un tour a distanza — `fo_tour_constraints`

Una riga per vincolo: `tour_id`, `kind`, `parameters_json`. Un insieme **chiuso** di tipi, scritto nel codice del
modulo, ciascuno con il suo schema di parametri e la sua verifica all'invio:

| Tipo | Parametri | Esempio |
|---|---|---|
| `DepartureOrArrivalIn` | paesi (codici IVAO) | partire **o** arrivare in Italia |
| `DepartureIn`, `ArrivalIn` | paesi | |
| `MinFlightsAtAirport` | aeroporto, numero | almeno 3 voli su LIRF |
| `MinFlightsAtAnyOf` | aeroporti, numero | |
| `LegDistanceBetween` | minimo e massimo NM | tratte fra 200 e 1500 NM |
| `NoRepeatedRoute` | — | sempre acceso nei tour a distanza |
| `AircraftOfCategory` | categoria di scia | |

Un vincolo **per volo** (paese, distanza) si verifica all'invio e **blocca**; un vincolo **sul tour** («almeno 3 voli su
LIRF») si verifica al **completamento**, e la pagina del tour mostra quanto manca. Aggiungere un tipo è codice e una
chiave i18n, non una decisione ⚖️.

### 2.7 `Container` — con sottotour

- Nessuna leg; sottotour di un livello. Il pilota si iscrive e vola i sottotour.
- **Un sottotour eredita** aerei, vincoli sul callsign e regole del tour dal padre **se non ne ha di suoi** (risposta 13).
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
| `ToModify` | il validatore | in attesa: il pilota continua a volare (con `FlyAhead`), corregge **tutto** — anche la sessione del tracker — e reinvia; torna in coda **a chiunque** |
| `Rejected` | il validatore | leg da rivolare; blocca le successive (§2.5) **finché non è contestata** |
| `Withdrawn` | il pilota, solo da `Queued` | leg volabile; sessione libera |

`is_disputed` è una bandiera su un `Rejected`, non uno stato. Ogni passaggio scrive in `fo_pirep_events`.

### 3.2 Il report

1. **La leg** (o, in `OpenFlying`, «nuovo volo»): il server verifica che sia volabile, che il tour accetti PIREP, che il
   pilota non sia bannato.
2. **Ricerca nel tracker** (estensione n.5): `GET /v2/tracker/sessions` con `userId`, `departureId`, `arrivalId`,
   `from = now − report_window_days`, `to = now` (senza aeroporti in `OpenFlying`). Esclude le sessioni già rivendicate
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
   - vincoli per volo di un tour a distanza (§2.6.1); rotta non già volata;
   - pilota non bannato.
6. Il PIREP nasce `Queued`, con `rules_snapshot_json`; il primo del pilota nel tour crea l'iscrizione. Partono in un job
   i controlli automatici (§6) e lo scarico del meteo mancante (§1.13).

### 3.3 Gli ATC contattati e le esenzioni

- **Mentre il pilota compila**, il sistema **propone** gli ATC che erano online lungo il volo, con lo stesso meccanismo
  del controllo di copertura (§6.5): archivio ATC per l'intervallo del volo, incrociato con le tracce. Il pilota
  **toglie** quelli che non ha contattato e **aggiunge** quelli che mancano; resta scritto chi è stato proposto e chi
  aggiunto.
- **Prima versione leggera** (per non caricare il server): le posizioni online negli aeroporti di partenza, arrivo e
  deviazione e nei FIR attraversati, calcolati dai punti delle tracce a campione ⚖️. La geometria dei settori (vIPI) si
  aggiunge più avanti.
- **Esenzioni**: il pilota sceglie la posizione fra gli ATC contattati e il tipo (`FreeSpeed`, `DirectRouting`,
  `LevelChange`, `Other`); ogni tipo dichiara quali controlli ammorbidisce. Vale se la posizione risultava online; se il
  dato manca, «non verificabile» e decide il validatore.

### 3.4 Il volo del tracker e le deviazioni

- **Una sessione vale per un solo PIREP**. Si libera se il PIREP è ritirato o se il pilota la sostituisce correggendo.
- **Deviazione**: il pilota indica che il volo è finito altrove, sceglie il **volo di riposizionamento**
  dall'aeroporto di deviazione alla destinazione della leg, e sceglie il **motivo** (strutturato più testo). Il server
  verifica che il secondo parta da dove il primo è atterrato. Con motivo `Weather` la pagina di validazione mostra in
  evidenza METAR e TAF dell'aeroporto di destinazione all'ora della deviazione (§1.13).

### 3.5 Gli esiti e le mail

Intenti al servizio del nucleo: `flightops.pirepAccepted`, `flightops.pirepToModify`, `flightops.pirepRejected`. Al
pilota, nella sua lingua: tour, leg, esito, `note_to_pilot`, **le regole violate**, il link. **Nessuna mail di tour
completato** (risposta 21).

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

### 3.9 I ban — `fo_bans`

- `vid`, `tour_id?` (null = **tutti i tour**), `starts_at`, `ends_at?` (null = **permanente**), `reason`, audit.
- Un pilota bannato **non invia PIREP** sui tour del ban; vede i tour, i suoi PIREP, e gli esiti di quelli già inviati,
  che si validano normalmente ⚖️.
- Lo decide chi ha `Tours.Ban` (§7). Il pilota riceve una mail (`flightops.banned`) con motivo e durata ⚖️.

### 3.10 Richiedi chiarimenti

- Su **qualunque PIREP deciso** (e ⚖️ su una leg o una regola del tour, dalla pagina del tour), il pilota chiede che gli si
  spieghi una procedura, un meccanismo o una regola. **Non chiede di cambiare il verdetto**, non cambia lo stato e
  **non conta** come contestazione.
- Stesso meccanismo del filo (estensione n.2), con il tipo `Clarification`: arriva al FOD e al validatore del PIREP, se
  c'è.

### 3.11 Il completamento, l'award, le segnalazioni

- Quando un PIREP accettato completa il tour, l'iscrizione scrive `completed_at` e, con `IProjectable`, proietta una
  **`AwardSignalProjection`** nella stessa transazione. Nessuna assegnazione automatica. Un sottotour completato conta per
  il padre, non segnala niente di suo.
- **Segnalare un problema su una leg**: `fo_leg_issues` e una notifica alla casella del FOD
  (`flightops.legIssueReported`).

---

## 4. La validazione

### 4.1 Le code

- **Una coda unica** e **una coda per tour** (la stessa lista generata, con il filtro sul tour).
- **Il validatore sceglie l'ordine** (revisione del 15 settembre): **per data** (dal PIREP che aspetta di più, risposta
  25) o **per tour** (raggruppati per tour, e dentro il tour per data), perché validare in fila le leg dello stesso tour è
  più veloce. La scelta resta memorizzata per lui ⚖️ (nel browser, o come preferenza dell'utente).
- **Chi vede**: chi ha `Tours.Validate` su almeno un tour vede **tutti** i PIREP in sola lettura; «Prendi» c'è solo sui
  tour per cui è abilitato.
- **I propri PIREP**: il validatore **li vede**, in **sola lettura**, con il loro esito e gli errori segnati (revisione del
  15 settembre); non li prende e non li decide (§7.3).
- Colonne: tour, leg, pilota, data del volo, in coda da, stato, preso da, contestato, suggerimento dei controlli.

### 4.2 La presa in carico

`InReview` con `assigned_to_vid` e `lease_until = now + leaseMinutes`; alla scadenza chiunque abilitato lo può
prendere (nessun job). Due prese insieme: vince la prima (`row_version`).

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
  `yearly_max` → rifiuto; altrimenti accettazione.
- **La decisione** con `note_to_pilot` e `staff_note`; contro il suggerimento, `threshold_overridden` e il perché.

---

## 5. Regole ed errori

### 5.1 Le schermate

- **Regole generali**, **errori**: liste e form generati, con le colonne «senza errori» / «senza regole». Il form della
  regola disegna i **parametri** dallo schema del controllo collegato (§6.2) ⚖️ (estensione del generatore di form se
  serve).
- **Regole del tour**: una scheda dell'editor, con «copia da un altro tour» e «emenda» accanto a ogni generale.

### 5.2 Le regole effettive

Le generali non ritirate, sostituite da quelle del tour che le emendano (con i parametri del tour), più quelle del tour;
per un sottotour, prima il padre e poi il sottotour. Un solo servizio, usato da pagina, PIREP, controlli e validazione.

### 5.3 Gli errori pubblici

Blocco Data `flightops.errorCatalog`: gli errori con `is_public`, con categoria, descrizione e regole collegate. Sempre
`live`. Il FOD lo mette in una pagina o in un documento.

### 5.4 Le regole congelate

Al **primo invio** il PIREP copia le regole effettive della leg, con i **parametri** e gli errori, in
`rules_snapshot_json`; il reinvio dopo «da modificare» non la rifà. Controlli e validazione leggono lo snapshot.

---

## 6. I controlli automatici (estensioni del nucleo n.5, n.8, n.12)

### 6.1 Che cosa fanno

Un PIREP inviato mette in coda un job che esegue i controlli e scrive `fo_check_results` (`pirep_id`, `check_key`,
`outcome`, `evidence_json`, `ran_at`). Per ogni controllo non superato, gli errori con quel `check_key` diventano
**suggeriti**.

### 6.2 La forma

- `IFlightCheck`: `Key`, lo **schema dei parametri** (con i default della regola generale di partenza) e
  `EvaluateAsync(FlightCheckContext)` → `Passed`, `Failed` (con evidenza) o **`Unavailable`** (dati mancanti: mai «fallito»).
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
| `speed250` | 250 kt sotto FL100 | tracce, esenzioni | tolleranza |
| `simRate` | velocità riportata coerente con quella di posizione | tracce | tolleranza |
| `alternate` | alternato presente (e `ZZZZ` ⚖️, sotto) | piano | — |
| `equipment` | equipaggiamento richiesto | piano | lettere |
| `takeoffFromThreshold` | decollo dalla testata | tracce, `ref_ivao_runways` | `thresholdToleranceMeters` |
| `vmc` | VMC a partenza e arrivo, **solo piani `V`** (e le metà VFR di `Y`/`Z`) | meteo salvato | visibilità e base nubi minime |
| `atcCoverage` | ATC online lungo il volo, e verifica delle esenzioni | archivio ATC | — (informativo) |
| `repeatedRoute` | rotta già volata nel tour a distanza (anche bloccato all'invio) | PIREP | — |

Note sui controlli delicati:

- **Livelli semicircolari: nessun controllo automatico in M2** (Carmine, 15 settembre). La regola vale solo nello spazio
  aereo a rotte libere (FRA), e **nessuna fonte aperta espone i volumi FRA**: cercati il 15 settembre SkyVector (nessuna
  API pubblica), OpenAIP (API gratuita con chiave, ma fra i tipi di spazio aereo non c'è la FRA), EUROCONTROL (punti,
  riferimenti AIP e carte, non geometrie), IVAO France (descrizione a parole). Tenere a mano una tabella della FRA vorrebbe
  dire un'altra cosa da aggiornare al cambio di ciclo, e Carmine non la vuole. **L'errore «livelli semicircolari» resta nel
  catalogo e lo segna il validatore.** Se una fonte aperta esporrà la FRA, il controllo si aggiunge come ogni altro
  `IFlightCheck`, senza toccare il resto.
- **`alternate` e `ZZZZ`** (Carmine, 15 settembre): `ZZZZ` è sia un aeroporto reale in Cina sia il codice «aeroporto
  senza ICAO», molto usato nei VFR. Il controllo **non supera** solo se `ZZZZ` è l'**alternato** e nelle remarks del piano
  **manca `ALTN/`**; con `ALTN/` presente l'alternato è un campo volo senza ICAO, ed è valido. Un piano senza alternato
  resta un errore dove la regola lo chiede.
- **`takeoffFromThreshold`**: dall'ultimo punto fermo prima della corsa di decollo nelle tracce, la distanza dalla testata
  della pista usata (quella con la prua più vicina alla prua di decollo). Oltre `thresholdToleranceMeters` il controllo non
  «fallisce»: segnala al validatore il **decollo da un'intersezione**, e il validatore verifica se da quel punto c'è una TORA
  pubblicata (dato che nessuna API dà). ⚠️ Il campionamento delle tracce IVAO va misurato: se i punti a terra sono radi, la
  posizione di inizio corsa è approssimativa, e la tolleranza va tarata sui voli veri.
- **`vmc`**: sul METAR più vicino all'ora di decollo e di atterraggio; senza METAR salvato, `Unavailable`.

Il riferimento per la logica è il validatore Python (`AutomaticValidatorTour`); ogni controllo si prova su **voli veri**
(§13).

### 6.5 L'archivio ATC

`IAtcActivitySource` nel nucleo («quali posizioni erano online in questo intervallo, e dove»): la vista di vIPI
`v_share_atc_sessions` o nessuna (`Unavailable`). Lo usano `atcCoverage` e la **proposta degli ATC contattati** (§3.3).

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
| `Tours.ManageAircraft` | ✓ | ✓ | ✓ ⚖️ | |
| `Tours.Validate` | ✓ tutti | ✓ tutti | ✓ tutti | ✓ i tour abilitati |
| `Tours.ManageValidators` | ✓ | ✓ | — | |
| `Tours.ViewPilots` | ✓ | ✓ | ✓ | ✓ (risposta 17) |
| `Tours.Ban` | ✓ | ✓ | — ⚖️ | |
| `Tours.ManageSettings` | ✓ | ✓ | — | |
| rispondere a contestazioni e chiarimenti | ✓ | ✓ | ✓ | ✓ sui propri PIREP decisi |

HQ e superadmin tutto. **I validatori si abilitano solo dalla pagina delle statistiche** (risposta 18).

### 7.3 Le due estensioni del meccanismo dei permessi (estensione del nucleo n.1)

1. **Scope per risorsa**: `hub_user_grants.resource_scope` (`flightops:tour:42`); una risorsa con `IHasResourceScope`
   dichiara il suo; l'handler conta un grant con scope solo sulla risorsa con lo stesso scope. «Aggiungi validatore» scrive
   un grant `Tours.Validate` con lo scope del tour; audit, sospensione e sessione vengono gratis.
2. **Stakeholder della riga**: `IHasStakeholder` (`StakeholderVid`) e i permessi che una risorsa **non** concede al suo
   stakeholder (`Tours.Validate` sul PIREP). Vale per tutti, **superadmin compreso** (risposta 15). La lettura resta
   concessa: il validatore vede i propri PIREP (§4.1).

Tutte e due vogliono una **nota di decisione** e i test della spina dorsale estesi.

---

## 8. Schermate

### 8.1 Pubblico

- **`/tours`**: tour aperti, in chiusura, e in arrivo con anteprima, come **riquadri** (foto, titolo, riassunto; per un
  pilota barra di avanzamento e prossima leg). Anonimo: niente avanzamento.
- **`/tours/{slug}`**: briefing, date, aerei, regole effettive con i parametri, **mappa** (blu da fare, verdi fatte, grigie
  ritirate o non ancora rilasciate ⚖️, arancioni in attesa), elenco delle leg con distanza, **tempo stimato**, callsign reale,
  pulsante **SimBrief**; per un tour a distanza senza leg, i vincoli e quanto manca; per il pilota i suoi PIREP, «Invia il
  report», «Contesta», «Richiedi chiarimenti», «Segnala un problema».
- **Il form del PIREP**: finestra dedicata (prima la scelta del volo, poi i campi).

### 8.2 Blocchi Data

| Blocco | Dove | Che cosa |
|---|---|---|
| `flightops.tourCards` | pagine pubbliche, `/me` | i riquadri, per chi guarda |
| `flightops.myTours` | `/me` | tour iniziati, avanzamento, prossima leg, PIREP da correggere, chiarimenti con risposta |
| `flightops.reviewQueue` | dashboard FOD, `/staff` | PIREP in coda sui tour che chi guarda può validare |
| `flightops.openIssues` | dashboard FOD | segnalazioni aperte, contestazioni e chiarimenti senza risposta |
| `flightops.errorCatalog` | pagine, documenti | gli errori pubblici |

### 8.3 L'editor del tour

- **`/staff/tours`**: tutti i tour passati, presenti e futuri (stato calcolato, tipo, date, nascosto, iscritti, in coda); filtro
  «template».
- **`/staff/tours/{id}`**, schede: **impostazioni** (form generato, compresi aereo di riferimento, anteprima, procedure,
  ordine delle rotazioni), **briefing** (editor dei blocchi), **hub e rotazioni**, **sottotour**, **vincoli sul callsign**,
  **vincoli del tour a distanza**, **leg** (§8.4), **regole del tour**. Barra: «Segna pronto» con i problemi, «Nascondi»,
  «Elimina» (solo senza PIREP), «Nuovo da template», «Salva come template».

### 8.4 L'editor delle leg (eccezione dichiarata, estensione n.6)

- **Tabella modificabile**: numero, partenza, arrivo, IATA, distanza e tempo stimato calcolati al volo, callsign reale, numero
  di volo, aerei, rotazione, rilascio, stato (con i PIREP).
- **Azioni sulla riga**: «aggiungi dopo: duplica», «aggiungi dopo: segue», «chiudi tour», «elimina o ritira» (il server sceglie
  secondo §1.4.1 e lo dice prima di confermare), «ripristina».
- **Import XLSX/CSV**, letto **nel browser** (risposta 24): anteprima delle differenze dal server; «fondi» non tocca le leg
  assenti; «sostituisci» elimina le assenti senza PIREP e ritira quelle con PIREP, con un motivo. Modello di file con le colonne.
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
- ⚠️ **Da misurare nella nota di decisione**: il peso del file mondiale fino al livello 7 (va caricato via FTP), e la CSP di
  MapLibre (serve la sua build «CSP», con il worker servito da noi, per non aggiungere `blob:` a `worker-src`).
  **Ripiego** se una delle due non regge: Leaflet con tessere raster di un fornitore dichiarato in `config/security.json`.
- Componente nuovo nell'elenco chiuso: `RouteMap`.

### 8.7 Le altre pagine dello staff

- **Statistiche dei validatori** (`/staff/tours/validators`): per anno e per validatore leg validate, accettate, rifiutate; per ogni
  tour dell'anno corrente e del precedente, per validatore, lo stesso. Qui «aggiungi validatore» e «togli».
- **Pagina del pilota** (`/staff/tours/pilots/{vid}`): errori confermati per categoria e per errore (anno solare e da sempre),
  tutte le leg volate con esito e validatore, contestazioni, chiarimenti, ban, tour con avanzamento; da qui «banna».
- **Ban** (`/staff/tours/bans`), **regole generali**, **errori**, **template**, **profili degli aerei**, **segnalazioni**,
  **impostazioni**: liste e form generati.

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
- **Cancellati** dopo **13 mesi** dalla chiusura per un tour normale e **25 mesi** per un tour su più di un anno: il tour con
  leg, regole, hub, vincoli; dei PIREP tracce, piani, esiti dei controlli, snapshot, note, ATC ed esenzioni, iscrizioni.
- **Meteo**: §1.13.
- Job mensile del modulo, con una riga nel log dei job.
- ⚖️ Il registro disciplinare, cancellato il tour, deve ancora dire **quale** tour e **quale** leg: il PIREP conserva nome del tour
  e partenza/arrivo come testo al momento della cancellazione.

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
| 7 | Award: catalogo e assegnazioni, schermata, immagini dalla media library (IVAO: documentazione 403, caricamento a mano) | no (piano §9.1) | §3.11 |
| 8 | `IAtcActivitySource` sulla vista di vIPI | coperta da piano 0.78 | §6.5 |
| 9 | Selezione multipla e parametri da schema nel generatore di form; aggregati nella lista | da verificare con il codice | §5.1, §8.7 |
| 10 | Più voci di calendario per riga in `IProjectable` | no (estensione piccola) | §9 |
| 11 | Tipi di aereo IVAO (`ref_ivao_aircraft`) da `/v2/aircrafts/all`, con equipaggiamenti e transponder | no | §1.5 |
| 12 | `IWeatherSource` (NOAA → IVAO → VATSIM), come vIPI | sì, breve (una fonte esterna nuova) | §1.13 |

---

## 12. Tabelle e migrazioni

**Nucleo** (additive): `ref_ivao_airports` (+ `iata`, `latitude`, `longitude`), `ref_ivao_runways`, `ref_ivao_aircraft`,
`hub_user_grants` (+ `resource_scope`), `hub_contact_messages` (+ `source_module`, `source_id`, `kind`, `participants_json`),
`hub_contact_replies`, `hub_awards`, `hub_award_assignments`.

**Modulo** (`Initial`): `fo_tours`, `fo_hubs`, `fo_rotations`, `fo_legs`, `fo_callsign_rules`, `fo_tour_constraints`,
`fo_aircraft_profiles`, `fo_rules`, `fo_errors`, `fo_rule_errors`, `fo_pireps`, `fo_pirep_flights`, `fo_pirep_errors`,
`fo_pirep_events`, `fo_check_results`, `fo_enrolments`, `fo_bans`, `fo_leg_issues`, `fo_weather_reports`.

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
- **Divisione XX**: nessuna stringa italiana né ICAO italiano nei seed.

---

## 14. Ordine di lavoro proposto (da scrivere in `06` parte C dopo la revisione)

| Fase | Contenuto |
|---|---|
| T0 | Note di decisione: permessi con scope e stakeholder; contatti con risposte; mappa (con le misure); fonte del meteo. Piano 0.79 |
| T1 | Nucleo: aeroporti del mondo con IATA e coordinate, piste con le testate, tipi di aereo |
| T2 | Nucleo: tracker nel client IVAO (sessioni, piani, tracce) con fixture; `IWeatherSource` |
| T3 | Nucleo: scope per risorsa e stakeholder nell'unico handler, test della spina dorsale |
| T4 | Nucleo: award (catalogo, assegnazioni, schermata); più voci di calendario per riga |
| T5 | Modulo: scheletro, impostazioni, profili degli aerei, `positionGrants` |
| T6 | Tour: modello, stato dalle date, nascondere/eliminare/chiusura, controlli «pronto», template |
| T7 | Leg: editor a tabella, GCD e tempo stimato, ritiro, hub e rotazioni, sottotour, callsign, vincoli a distanza |
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
| T19 | Conservazione, calendario, ricerca, rifiniture, giro completo |

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
   **FRA**: nessuna fonte aperta, niente controllo automatico dei livelli semicircolari in M2 (§6.4). **Tipi di aereo**:
   dagli endpoint `/v2/aircrafts` (§1.5).
2. **Tour nascosto**: chi l'aveva iniziato lo vede ancora nella sua pagina, senza poter inviare? I PIREP in coda si validano?
3. **Eliminare una leg senza PIREP**: le leg dopo **non** si rinumerano (buco nel numero)?
4. **Leg ritirata dentro una rotazione**: il FOD deve sistemare la rotazione (aggiungere o ritirare la rotazione intera)?
5. **Tempo stimato**: la formula con parte fissa (5 % + 20 minuti) al posto del solo +20 %, tarata sui voli veri? È solo
   informativo, o un vincolo?
6. **Aereo di riferimento obbligatorio** per segnare pronto un tour?
7. **Tour a distanza**: A→B e B→A sono la stessa rotta?
8. **Vincoli del tour a distanza**: vanno bene i tipi di §2.6.1? Ne mancano?
9. **Contestazione respinta**: la leg torna a bloccare, con la tolleranza contata da quel momento?
10. **Ban**: lo decidono solo coordinator e assistant, o anche gli advisor? Mail al pilota bannato? I PIREP già inviati si validano?
11. **Richiedi chiarimenti**: solo sui PIREP decisi, o anche su una leg o una regola dalla pagina del tour?
12. **Meteo**: un job ogni 30 minuti sugli aeroporti delle leg dei tour aperti, più lo scarico all'invio per gli altri: va bene?
13. **Soglie VMC** per il controllo `vmc`: quelle standard (5 km, nubi a 1500 ft) come parametri della regola generale?
14. ~~**FRA**~~ chiusa: niente controllo automatico dei livelli semicircolari (§6.4).
15. **Decollo dalla testata**: 150 m di tolleranza come partenza, da tarare?
16. **ATC contattati, prima versione**: aeroporti e FIR attraversati dalle tracce, e la geometria dei settori più avanti?
17. **Ordine della coda memorizzato**: nel browser del validatore o come preferenza del suo utente (lo segue su più computer)?
18. **Advisor**: gestiscono anche i profili degli aerei?
19. **Registro disciplinare dopo la cancellazione del tour**: il PIREP conserva nome del tour e aeroporti come testo?
20. **I voli di test**: me li mandi con l'esito atteso per ogni controllo, e se possibile con la decisione reale?
