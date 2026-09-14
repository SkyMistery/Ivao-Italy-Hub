# IVAO Division Hub — Design di M2 (il modulo dei tour)

> Documento **interno** (italiano). Fonte di verità: `00-piano-di-progettazione.md` (versione 0.78).
> Ingresso: **`decisions/2026-09-14-requisiti-dei-tour.md`** — i requisiti decisi da Carmine e dallo staff
> FOD. Questo documento li trasforma in modello, flussi, permessi, schermate e fasi; **non li ridiscute**.
> Dove un requisito lascia una scelta aperta, la scelta è proposta qui e segnata **⚖️ da confermare**.
> Le fasi di implementazione si scrivono nella parte C di `06-piano-implementazione-m2.md` **dopo** che
> Carmine ha letto questo documento.

**Stato:** bozza del 14 settembre 2026, da rivedere con Carmine.

---

## 0. Perimetro

### 0.1 Che cosa è «fatto»

M2 è fatta quando lo staff FOD può preparare la stagione 2027 nell'hub e i piloti possono volarla:

- il FOD crea tour di **tutti i tipi** di §2 (anche da template), con leg inserite a mano o importate da
  XLSX/CSV, regole generali e del tour, errori, validatori abilitati per tour;
- un tour **pronto** esce da solo alla data di rilascio e si chiude da solo alla data di chiusura;
- un pilota vede i tour, la mappa, il suo avanzamento, e invia un PIREP scegliendo il volo dal tracker;
- un validatore abilitato prende il PIREP dalla coda, segna gli errori con i suggerimenti del sistema e
  decide; il pilota riceve la mail; un rifiuto si contesta e il FOD risponde;
- il completamento di un tour produce una **segnalazione award**, e chi ha `Awards.Assign` assegna l'award
  dal catalogo del nucleo;
- lo staff ha le statistiche dei validatori e la pagina del pilota;
- i **controlli automatici** con i dati IVAO girano su ogni PIREP e **suggeriscono** errori (in sola
  informazione per la prima stagione).

### 0.2 Fuori perimetro

- **Manovre obbligatorie** (touch-and-go nei tour VFR): dopo il sistema principale (requisiti §4). Il
  modello lascia loro il posto (§1.5), ma niente schermate né controlli in M2.
- **Online Day** (passa all'ED, M4), **punti ed eventi**, **Discord**, **Pilot Life**, **controlli di livello
  B/C** (rotta, procedure, Eurocontrol).
- **Import dello stato dei tour attuali**: il sistema entra in uso nel 2027 (requisiti, intestazione).
- **Il pacchetto e lo staging Plesk** restano nella metà (b) di M2 (piano §13): non dipendono da questo
  modulo e non stanno in questo documento.
- **La pagina delle Virtual Airlines**: rimandata da Carmine il 13 settembre.

### 0.3 Che cosa M2 non rimette in discussione

- **I meccanismi del nucleo** (`CLAUDE.md` §2, piano §16): `MapCrud`, lista e form generati, l'unico
  handler, il filtro globale, `IOwnedByDepartment` a insieme (H2), i grant a una posizione (H1),
  `IProjectable`, il servizio notifiche, l'unico `IIvaoApiClient`, i blocchi Data, le dashboard (D1–D3).
- **I moduli fuori dai dipartimenti**: sezione `/staff/tours`, «a cura di» con il FOD sempre presente.
- **Chi gestisce il modulo** (piano 0.77): coordinator e assistant FOD tutto, per grant a una posizione;
  gli advisor come decide §7.
- **I dati condivisi con vIPI** (piano 0.78): l'archivio delle sessioni ATC si legge da una vista.

### 0.4 I nomi

| Che cosa | Nome |
|---|---|
| Chiave del modulo (`IModule.Key`, `division.json`, storia delle migrazioni) | `flightops` |
| Progetto .NET | `IvaoHub.Modules.FlightOps` |
| Frontend | `web/src/modules/flightops/` |
| Prefisso delle tabelle | `fo_` |
| API | `/api/flightops/...` (la chiave, `IModule.MapEndpoints`) |
| Area dei permessi | `Tours` (`Tours.Edit`, `Tours.Validate`…) |
| Rotte pubbliche | `/tours`, `/tours/{slug}` |
| Rotte dello staff | `/staff/tours/...` |
| Namespace i18n | `flightops` (`locales/{lang}/flightops.json`) |

«Tours» è il nome che vedono le persone; `flightops` è il nome del codice, perché il modulo è del
dominio Flight Operations e il piano lo chiama così dal 1° settembre.

### 0.5 Che cosa si prende da Toursystem, e che cosa no

Toursystem (`D:\Programmazione\IVAO_Test\Ivao Italy Toursystem`) è stato letto il 13 settembre. Si
prende il **dominio**, non l'architettura (Blazor, `schema.sql` a mano, ruoli suoi, tabelle di traduzione).

| Si prende | Da | Dove qui |
|---|---|---|
| Chi ha un interesse non decide (PIREP) | ADR-033 | §3.6 |
| Decisione con motivo (errori) e nota | ADR-008 | §3.5 |
| Il pilota non vede i contatori | ADR-009 | §3.8 |
| La soglia suggerisce, decide l'umano | ADR-010, ADR-035 | §4.3 |
| Tre esiti per un controllo (superato, non superato, **non disponibile**) | ADR-011 | §6.2 |
| PIREP come form strutturato precompilato dal tracker | ADR-013 | §3.2 |
| Esenzioni che dichiarano quali controlli ammorbidiscono | ADR-014 | §3.3 |
| Regole congelate sul PIREP | ADR-015 | §5.4 |
| Sessione del tracker **rivendicata** da un solo PIREP | ADR-017 | §3.4 |
| Limiti giornalieri misti con avviso | ADR-023 | §3.7 |
| Coordinate congelate alla scrittura della leg | ADR-024 | §1.4 |
| Sottotour come tour con un padre, profondità 1 | ADR-032 | §2.6 |
| La macchina propone e non rifiuta | ADR-035 | §6.3 |
| Prima stagione dei controlli in sola informazione | ADR-036 | §6.3 |
| Segnalazione di un problema su una leg | ADR-037 | §3.10 |
| Deviazione in più tratti, motivata | ADR-042 | §3.4 |
| Import che non cancella da solo | ADR-051 | §8.4 |
| Distanza GCD (formula di `GreatCircle.cs`, con i test) | codice F1 | §1.4 |
| Controlli per la pubblicazione (lingue, aeroporti, finestre) | `TourPublicationChecker` | §1.2 |
| Il catalogo dei 24 controlli, quelli di livello A | ARCHITETTURA App. A | §6.4 |

| Non si prende | Perché |
|---|---|
| Stagione e clonazione | sostituite dai **template di tour** (requisiti §4) |
| Quattro occhi sulla pubblicazione del tour | Carmine: «se è segnato pronto viene pubblicato» |
| Stato del tour con sette valori | lo stato vero è **derivato dalle date** (§1.2) |
| Tabelle di traduzione, `.resx` | `Localized<T>` e `locales/` |
| Ruoli propri (tour manager, validator, award officer) | permessi `Tours.*` e grant (§7) |
| Registro dei punti | fuori perimetro (§0.2) |
| Pannello HQ, poller ATC, Online Day, eventi | fuori o altrove |
| Parametri di tolleranza sovrascrivibili per leg | in M2 le tolleranze sono dei **controlli** (§6.4), impostate dal FOD per tutta la divisione; un tour le emenda attraverso le regole (§5.2) ⚖️ |

---

## 1. Il modello

Tutte le tabelle stanno in `FlightOpsDbContext : ModuleDbContext`, con la sua
`__EFMigrationsHistory_flightops`. Nessuna FK verso il nucleo: `vid`, `icao`, `media_id`, `award_id`
sono colonne non vincolate (`CLAUDE.md` §2).

### 1.1 Chi possiede una riga

- Le righe che lo staff scrive (tour, leg, regole, errori, template) implementano **`IOwnedByDepartment`**
  con **`OwnerDepartmentMask`**: il dipartimento di base (`modules.flightops.baseDepartment: "FOD"`) c'è
  sempre, gli altri si aggiungono in collaborazione come per ogni riga di modulo (H2). **`IAuditable`** e
  **`[Audited]`** su tutte: l'audit registra prima e dopo di ogni scrittura, quindi **non esiste una
  tabella delle modifiche** (requisiti §4, «con motivazione registrata»: il motivo è una colonna, §1.4).
- Le righe che il pilota scrive (PIREP, segnalazioni) sono **`ISubmittedByMembers`**: le crea un membro
  qualunque, dentro lo spazio del FOD; ogni scrittura successiva chiede il permesso come tutte.
- **`IVisible`**: i tour pubblicati sono `Public` (requisiti §1: visibili a tutti); le bozze e i template
  `Staff`. Un PIREP è `Members` e il filtro lo restringe al suo pilota o a chi ha i permessi di §7.

### 1.2 Il tour — `fo_tours`

| Colonna | Tipo | Note |
|---|---|---|
| `id`, `slug` | | `slug` unico fra i non template; è l'indirizzo `/tours/{slug}` |
| `is_template` | bool | un template è una riga di questa tabella, come i template dei contenuti (§1.9) |
| `kind` | enum | `Sequential`, `Free`, `Hub`, `SequentialChosenStart`, `Distance`, `Container` (§2) |
| `parent_tour_id` | long? | valorizzato solo per un **sottotour**; il padre è un `Container` |
| `required_subtours` | int? | solo per un `Container`: quanti sottotour completare |
| `required_nm` | int? | solo per `Distance` |
| `title` | `Localized<string>` | |
| `summary` | `Localized<string>` | una riga per il riquadro |
| `briefing_json` | BlockDocument | testo ricco (`CLAUDE.md` §2: stesso editor e renderer) |
| `cover_media_id` | long? | foto di sfondo, dalla media library |
| `status` | enum | `Draft`, `Ready` |
| `release_at`, `close_at` | UTC | date di rilascio e chiusura |
| `report_window_days` | int | X: giorni per inviare il PIREP **e** finestra di ricerca nel tracker (requisiti §1, risposta 38). Default dalle impostazioni (§1.11) |
| `progression` | enum | `FlyAhead` (le successive si volano prima della validazione) o `WaitForValidation` (requisiti §4) |
| `daily_leg_limit` | int? | leg al giorno per pilota; obbligatorio se il limite di divisione è spento (§3.7) |
| `allowed_aircraft_json` | string[] | tipi ICAO consentiti (vuoto = tutti); una leg può restringerli |
| `award_id` | long? | l'award del catalogo del nucleo che il completamento segnala (§3.9) |
| `rules_version` | int | cresce a ogni modifica delle regole del tour (§5.4) |
| `row_version`, audit, `owner_department_mask` | | |

**Lo stato pubblico non si memorizza: si calcola dalle date**, così nessun job deve «pubblicare» o
«chiudere» un tour e non esiste un momento in cui il job non è passato:

| Stato visto | Condizione |
|---|---|
| Bozza | `status = Draft` |
| In arrivo | `Ready` e `now < release_at` (visibile allo staff; al pubblico ⚖️ no) |
| Aperto | `Ready` e `release_at ≤ now ≤ close_at` |
| In chiusura | `Ready` e `close_at < now ≤ close_at + report_window_days`: il tour risulta chiuso ma accetta PIREP di voli con decollo `≤ close_at` |
| Chiuso | dopo |

Un tour su **due anni** è semplicemente un tour con `close_at` nell'anno dopo (requisiti §4).

**Segnare «pronto»** passa dai **controlli di pubblicazione** (da `TourPublicationChecker`), che bloccano:
titolo e riassunto in tutte le lingue della divisione (`LocalizedRules`), almeno una leg (tranne
`Container`), aeroporti noti in `ref_`, date coerenti (`release_at < close_at`, date di rilascio delle leg
dentro il periodo), vincoli di tipo (§2), limite giornaliero se quello di divisione è spento (§3.7),
un `Container` con almeno due sottotour e `required_subtours` non più grande di quanti sono.
Da `Ready` si torna a `Draft` finché il tour non è aperto; dopo, si modifica restando `Ready`.

### 1.3 Hub e rotazioni — `fo_hubs`, `fo_rotations`

- `fo_hubs`: `tour_id`, `icao`, `sort`. Solo per `Hub`.
- `fo_rotations`: `tour_id`, `hub_id`, `sort`, `size` (2, 4 o 6: il controllo di pubblicazione verifica
  che la rotazione abbia esattamente `size` leg, parta dall'hub e ci torni).
- **Il collegamento fra hub è una leg** con `kind = HubConnection` (§1.4) che va dall'hub A all'hub B: conta
  come una leg normale (requisiti §5). Due hub senza una leg di collegamento sono **liberi**; con la leg
  sono **collegati**, e il pilota può passare solo a un hub collegato a quello in cui ha finito.

### 1.4 La leg — `fo_legs`

| Colonna | Note |
|---|---|
| `tour_id`, `number` | ordine nel tour; unico per tour |
| `kind` | `Normal` o `HubConnection` |
| `rotation_id`, `seq_in_rotation` | per i tour `Hub` |
| `departure_icao`, `arrival_icao` | |
| `departure_lat/lon`, `arrival_lat/lon` | **congelate alla scrittura** dagli aeroporti `ref_` (ADR-024): la mappa e la distanza non cambiano se IVAO sposta un aeroporto a tour in corso |
| `distance_nm` | GCD calcolata dal server (non scritta dal FOD) |
| `real_callsign`, `flight_number` | callsign e numero di volo reali, se esistono (informativi) |
| `aircraft_json` | tipi ICAO della leg; vuoto = quelli del tour |
| `release_at` | data di rilascio propria, facoltativa (requisiti §4) |
| `retired_at`, `retired_reason` | una leg **si ritira**, non si cancella, e i suoi PIREP restano |
| `change_reason` | obbligatorio quando si modifica una leg che ha PIREP: finisce nell'audit con prima e dopo |

**Le comodità dell'editor non si memorizzano**: «la successiva duplica», «la successiva segue», «chiudi
tour» sono azioni dell'editor che compilano la leg nuova (§8.3). In tabella resta solo la leg.

⚖️ **IATA**: la leg non lo memorizza; lo legge dagli aeroporti `ref_` per mostrarlo.

### 1.5 Vincoli sul callsign, e il posto delle manovre

- `fo_callsign_rules`: `tour_id` (un sottotour è un tour), `leg_id?`, `mode` (`Allow`, `Deny`),
  `match` (`Prefix`, `Exact`, `Pattern`), `value`. Il callsign **effettivo consentito** di una leg è
  l'unione delle regole di leg, tour e tour padre; un `Deny` vince sempre (Itavia: `IHS870`, `IHS897`…).
  Il controllo automatico `callsign` (§6.4) e il form del PIREP le leggono dallo stesso servizio.
- **Manovre obbligatorie**: una tabella `fo_leg_manoeuvres` arriverà con la loro fase, dopo M2. Il modello
  qui non la anticipa: aggiungere una tabella è una migrazione additiva.

### 1.6 Regole ed errori — `fo_rules`, `fo_errors`, `fo_rule_errors`

- **`fo_rules`**: `tour_id?` (null = **generale**), `code` («GR4», «IR3»), `title` e `text`
  (`Localized<string>`, testo in markdown), `amends_rule_id?` (una regola del tour che **emenda** una
  generale: nel tour vale al suo posto), `sort`, `retired_at`.
- **`fo_errors`**: il catalogo **della divisione**, uno solo per tutti i tour (i contatori del pilota sono su
  tutti i tour, requisiti §2). Colonne: `name` (`Localized<string>`, breve), `description` e `examples`
  (`Localized<string>`, per il validatore), `category` (`Info`, `Warning`, `Dangerous`), `yearly_max`
  (solo `Warning`), `check_key?` (il controllo automatico collegato, §6), `is_public` ⚖️, `retired_at`.
- **`fo_rule_errors`**: `rule_id`, `error_id`. Molti a molti.
- **Il sistema segnala** una regola senza errori e un errore senza regole: una colonna calcolata nelle due
  liste, con il filtro «da sistemare».
- **Copiare le regole da un altro tour** copia le regole del tour con i loro collegamenti agli errori.

### 1.7 Il PIREP — `fo_pireps`, `fo_pirep_flights`, `fo_pirep_errors`, `fo_pirep_events`

**`fo_pireps`**

| Colonna | Note |
|---|---|
| `tour_id`, `leg_id`, `vid` | |
| `status` | §3.1 |
| `submitted_at`, `resubmitted_at?` | |
| `sid`, `star`, `approach` | se richiesti dalle regole della leg ⚖️ (§3.2) |
| `atc_exemptions_json` | autorizzazioni ricevute: posizione ATC, tipo (`FreeSpeed`, `DirectRouting`, `LevelChange`, `Other`), nota (§3.3) |
| `is_diversion`, `diversion_reason` | §3.4 |
| `pilot_remarks` | |
| `rules_snapshot_json` | regole ed errori **effettivi** della leg al momento dell'invio (§5.4) |
| `assigned_to_vid`, `lease_until` | presa in carico (§4.2) |
| `decided_by_vid`, `decided_at`, `outcome` | |
| `note_to_pilot`, `staff_note` | la prima va nella mail, la seconda resta allo staff |
| `threshold_overridden` | il validatore ha deciso contro il suggerimento (ADR-010) |
| `row_version`, audit | |

**`fo_pirep_flights`**: una riga per tratta volata. Una leg normale ne ha una; una **deviazione** ne ha due
(la tratta deviata e il riposizionamento). Colonne: `seq`, `tracker_session_id`, `callsign`, `aircraft`,
`departure_icao`, `arrival_icao`, `takeoff_at`, `landing_at`, `flight_plan_json` (il piano al decollo, per i
controlli). **Vincolo unico su `tracker_session_id`**: un volo del tracker vale per un solo PIREP (§3.4).

**`fo_pirep_errors`**: `pirep_id`, `error_id`, `category` (congelata), `suggested_by_check`
(bool), `confirmed` (bool: il validatore l'ha segnato). Da qui si contano gli errori del pilota.

**`fo_pirep_events`**: la storia, solo in aggiunta: `from_status`, `to_status`, `by_vid`, `at`, `note`.
È quello che mostra la pagina del pilota e che le statistiche dei validatori contano (§9).

### 1.8 L'iscrizione — `fo_enrolments`

Il **primo PIREP iscrive** il pilota (requisiti §1): la riga nasce in quella transazione.
`vid`, `tour_id`, `started_at`, `start_leg_id?` (per `SequentialChosenStart`, scelta al primo PIREP e non più
cambiabile, risposta 35), `hub_order_json` (per `Hub`: gli hub nell'ordine in cui il pilota li ha
iniziati), `completed_at?`.

**L'avanzamento non si memorizza**: si calcola dai PIREP del pilota nel tour (pochi: al massimo qualche
decina per tour), così non esiste una proiezione che si possa disallineare. `completed_at` invece **sì**,
perché è un fatto con una data e fa partire la segnalazione award (§3.9).

### 1.9 I template di tour

- Un template è una riga di `fo_tours` con `is_template = true`, **mai visibile al pubblico**, senza date.
- Lo creano, modificano ed eliminano **coordinator e assistant FOD** (`Tours.ManageTemplates`, §7).
- **«Nuovo tour da template»** copia impostazioni, briefing, foto, aerei, vincoli sul callsign, regole del
  tour con i collegamenti agli errori, hub e rotazioni ⚖️ **e le leg** ⚖️. Non copia date, PIREP, iscrizioni.
- Un template cambiato **non tocca** i tour già creati da lui (come i template dei contenuti).

### 1.10 Validatori, segnalazioni, contestazioni

- **Validatori abilitati per tour**: non una tabella del modulo, ma **grant** `Tours.Validate` con uno
  **scope per tour** (§7.2, estensione del nucleo n.1).
- **`fo_leg_issues`**: `leg_id`, `vid`, `text`, `status` (`Open`, `Handled`, `Dismissed`), `handled_by`,
  `handled_note`. `ISubmittedByMembers`.
- **Contestazioni**: nel nucleo, con i contatti estesi a un filo di risposte (§3.8, estensione n.2).

### 1.11 Le impostazioni della divisione per i tour

Sono dati che il FOD cambia dall'interfaccia, non configurazione da caricare via FTP: stanno in
`hub_division_settings` (esiste già, chiave e JSON) sotto la chiave `flightops`:

| Impostazione | Default proposto |
|---|---|
| `dailyLegLimit` | 10 (null = spento, §3.7) |
| `defaultReportWindowDays` | 7 |
| `rejectGraceHours` | 12 (Toursystem: 720 minuti) |
| `leaseMinutes` | 30 |
| `retentionYears` / `retentionYearsLong` | 2 / 4 (§10) ⚖️ |
| `checksMode` | `Informational` (§6.3) |

### 1.12 Gli aeroporti di tutto il mondo (estensione del nucleo n.4)

Oggi `ref_ivao_airports` tiene solo gli aeroporti del **paese** della divisione
(`IIvaoApiClient.GetAirportsAsync(countryId)`). I tour volano ovunque (Lufthansa, Ryanair), quindi:

- il job di sincronizzazione legge **`/v2/airports/all`** (lo usa già Toursystem, `AirportCatalogueSynchroniser`);
- `ref_ivao_airports` guadagna le colonne **`iata`**, **`latitude`**, **`longitude`** (migrazione additiva
  nel nucleo), promosse dal JSON grezzo che IVAO manda già (`IvaoAirportMapper` di Toursystem ne legge i
  campi `iata`, `latitude`, `longitude`);
- i numeri opzionali passano da convertitori tolleranti (Toursystem ADR-049: lo stesso campo torna `null` o
  `""` in due chiamate);
- **⚠️ `FirDirectory` e `networkStats`** oggi deducono «gli aeroporti della divisione» dalla tabella: con
  tutto il mondo dentro, devono filtrare per paese. Va verificato e provato nella fase che lo fa.

---

## 2. I tipi di tour

Ogni tipo risponde a tre domande, scritte **una volta** in un servizio del modulo (`TourRules`) e usate
dal riquadro, dalla pagina del tour, dal form del PIREP e dal controllo al salvataggio: **quali leg può
volare adesso il pilota**, **qual è la prossima**, **quando ha finito**.

Regole comuni a tutti: una leg ritirata non si vola; una leg con `release_at` futura non si vola (e in un
tour in sequenza ferma il pilota alla precedente, risposta 37); una leg già **accettata** o **in attesa**
non si rivola; una leg **rifiutata** si rivola.

### 2.1 `Sequential` — in sequenza

- **Volabili**: la prima leg non accettata né in attesa, se tutte le precedenti sono accettate
  (`WaitForValidation`) o accettate o in attesa (`FlyAhead`).
- **Prossima**: quella.
- **Finito**: tutte le leg accettate.

### 2.2 `Free` — libero

- **Volabili**: ogni leg non accettata e non in attesa.
- **Prossima**: nessuna (il riquadro dice «scegli una leg») ⚖️.
- **Finito**: tutte accettate.

### 2.3 `Hub`

- **All'inizio**: il pilota sceglie un hub qualunque.
- **Dentro un hub**: le rotazioni di quell'hub, ogni rotazione **in ordine** (§2.1 applicato alla rotazione);
  le rotazioni fra loro in qualunque ordine ⚖️.
- **Cambio di hub**: quando tutte le rotazioni dell'hub sono accettate (o accettate e in attesa, con
  `FlyAhead`). Se esistono leg `HubConnection` che partono dall'hub finito, il pilota sceglie uno degli hub
  **collegati non ancora fatti** e vola la leg di collegamento; se non esistono, sceglie liberamente fra gli
  hub non fatti.
- **Finito**: tutti gli hub (e le leg di collegamento volate).

### 2.4 `SequentialChosenStart` — in sequenza con partenza a scelta

- **Primo PIREP**: su qualunque leg; diventa `start_leg_id`.
- **Poi**: in ordine da `start_leg_id` all'ultima, poi dalla prima fino a `start_leg_id − 1`.
- **Finito**: tutte accettate.
- Il controllo di pubblicazione verifica che il tour sia **chiuso ad anello** (la destinazione dell'ultima è la
  partenza della prima): senza anello, «riparte dalla prima» sarebbe un salto ⚖️.

### 2.5 `Distance` — a distanza

- **Volabili**: come `Free` ⚖️.
- **Finito**: la somma di `distance_nm` delle leg accettate raggiunge `required_nm` ⚖️ (la GCD della leg, non
  la distanza realmente volata: un pilota che allunga non guadagna).
- Il controllo di pubblicazione verifica che la somma di tutte le leg arrivi a `required_nm`.

### 2.6 `Container` — con sottotour

- Un `Container` **non ha leg**; ha sottotour (tour con `parent_tour_id`), **un solo livello** (ADR-032).
- Ogni sottotour è di uno dei tipi sopra, con le sue leg e le sue regole; eredita dal padre aerei, vincoli sul
  callsign e regole del tour **se non ne ha di sue** ⚖️.
- Il pilota si iscrive e vola **i sottotour**, mai il padre.
- **Finito il padre**: `required_subtours` sottotour completati.
- ⚖️ **Gli award**: il padre ha l'award del tour intero; un sottotour può averne uno suo (Toursystem lo
  vietava). Proposta: il campo `award_id` esiste su entrambi, e decide il FOD.

---

## 3. Il flusso del pilota

### 3.1 Gli stati del PIREP

```
               ┌───────────── (il pilota corregge) ──────────────┐
               ▼                                                 │
  inviato ──► in coda ──► in validazione ──► accettato           │
                ▲             │  (lease scaduto torna in coda)   │
                │             ├──► da modificare ────────────────┘
                │             └──► rifiutato ──► (contestato: messaggio al FOD, §3.8)
                │
  ritirato ◄────┘ (il pilota, finché nessuno l'ha preso)
```

| Stato | Chi lo porta lì | Effetto sul tour |
|---|---|---|
| `Queued` | invio, o reinvio dopo «da modificare» | la leg è **in attesa** |
| `InReview` | un validatore lo prende (§4.2) | in attesa |
| `Accepted` | il validatore | leg fatta |
| `ToModify` | il validatore | in attesa: il pilota continua a volare (se `FlyAhead`), corregge **tutto** — anche la sessione del tracker — e reinvia; torna in coda **a chiunque** (risposta 33) |
| `Rejected` | il validatore | la leg si **rivola**; il PIREP si può **contestare** (§3.8) |
| `Withdrawn` | il pilota, solo da `Queued` | la leg torna volabile; la sessione del tracker si libera |

Ogni passaggio scrive una riga in `fo_pirep_events`. **Nessuna cancellazione** di un PIREP deciso.

### 3.2 Il report

1. Il pilota apre la leg (pagina del tour, §8.1) e preme «Invia il report»: il server verifica che la leg sia
   **volabile per lui** (§2) e che il tour accetti PIREP (aperto o in chiusura).
2. **Ricerca nel tracker** (estensione del nucleo n.5): `GET /v2/tracker/sessions` con `userId`,
   `departureId`, `arrivalId`, `from = now − report_window_days`, `to = now` (i parametri li usa già il
   validatore Python di `AutomaticValidatorTour`). Il server scarta le sessioni già rivendicate da un altro
   PIREP e quelle con decollo dopo `close_at`, e mostra le altre: callsign, aereo, decollo, atterraggio,
   durata.
3. Il pilota **sceglie il volo**. Il server legge i piani di volo della sessione
   (`/v2/tracker/sessions/{id}/flightPlans`) e congela quello valido al decollo in `flight_plan_json`.
4. **Campi del report**: SID, STAR, approccio (se le regole della leg li chiedono ⚖️: proposta, una bandiera
   sulla regola generale RR5 e sui tour VFR spenta), esenzioni ATC (§3.3), note.
5. **Deviazione** (§3.4), se il volo non è arrivato a destinazione.
6. **Controlli al salvataggio**, lato server (`ProblemDetails`, campo per campo): leg volabile, finestra dei
   giorni, data del volo non oltre la chiusura, sessione non rivendicata, **limiti giornalieri** (§3.7),
   callsign e aereo **solo come avviso** ⚖️ (il rifiuto lo decide il validatore; li segnala il controllo
   automatico).
7. Il PIREP nasce `Queued`, con `rules_snapshot_json` (§5.4); se è il primo del pilota nel tour, nasce anche
   l'iscrizione (§1.8). I controlli automatici partono in un job (§6).

### 3.3 Le esenzioni ATC

- Il pilota dichiara la posizione (`LIRF_TWR`) e l'autorizzazione ricevuta.
- Ogni tipo di autorizzazione **dichiara quali controlli ammorbidisce** (una tabella nel codice del modulo:
  `FreeSpeed` → `speed250` diventa informativo; `DirectRouting` → nessun controllo di rotta in M2; ecc.).
- Vale **solo se la posizione risultava online** durante il volo: lo dice l'archivio ATC (§6.5). Se il dato non
  è disponibile, l'esenzione è mostrata al validatore come «non verificabile» e decide lui.

### 3.4 Il volo del tracker e le deviazioni

- **Una sessione del tracker vale per un solo PIREP** (vincolo unico, ADR-017). Si libera quando il PIREP è
  ritirato, o quando il pilota la sostituisce correggendo un «da modificare».
- **Deviazione**: il pilota indica che il volo è finito altrove e sceglie **un secondo volo** del tracker (il
  riposizionamento) dall'aeroporto di deviazione alla destinazione della leg, entro la finestra; scrive il
  **motivo** (obbligatorio). Il PIREP ha due `fo_pirep_flights`. Il controllo verifica che il secondo parta da
  dove il primo è atterrato (Toursystem lo imponeva con una FK composta; qui è una regola del servizio).

### 3.5 Gli esiti e le mail

Tre tipi di notifica del modulo, pubblicati come **intenti** al servizio del nucleo:
`flightops.pirepAccepted`, `flightops.pirepToModify`, `flightops.pirepRejected`. Destinatario: il pilota,
nella sua lingua. Dati: tour, leg, esito, `note_to_pilot`, **le regole violate** (il nome degli errori
confermati e la regola a cui sono collegati), il link. I testi in `locales/{lang}/mail.json`.

### 3.6 Nessuno valida i propri PIREP

- Il validatore **non vede** i propri PIREP nella coda, **non può prenderli** né deciderli: il servizio lo
  rifiuta (403) anche se un grant glielo permetterebbe.
- ⚠️ È una regola **sulla riga** («il pilota del PIREP non è chi decide»), quindi non la può esprimere il
  grant: va nel meccanismo come un vincolo della risorsa, **non** come un controllo scritto a mano nel
  modulo. Proposta in §7.3.

### 3.7 I limiti giornalieri

- **Contano i PIREP** del pilota non rifiutati e non ritirati, per **giorno UTC del decollo** del volo.
- **Limite del tour** (`daily_leg_limit`): conta i PIREP del tour.
- **Limite di divisione** (`dailyLegLimit` delle impostazioni): conta i PIREP di tutti i tour. Superato,
  **avviso** e non blocco (Toursystem, «10 al giorno, warning») ⚖️; il limite del tour **blocca**.
- **Spegnere il limite di divisione** (risposta 8): il salvataggio delle impostazioni rifiuta il cambio se
  esiste un tour **aperto, in arrivo o in bozza con date future** senza `daily_leg_limit`, e l'errore elenca
  quei tour. Con il limite spento, «pronto» richiede `daily_leg_limit` (§1.2).

### 3.8 La contestazione (estensione del nucleo n.2)

- **Solo su un `Rejected`** (risposta 34), dal pilota, ⚖️ entro `report_window_days` dalla decisione.
- Un tasto «Contesta» apre un messaggio. Diventa un **contatto del FOD** (il meccanismo esistente:
  `ContactMessage` di proprietà del dipartimento), **legato al PIREP** e con in più **il validatore che ha
  deciso** fra chi può leggere e rispondere, anche se non è del FOD.
- **Le risposte** (che oggi i contatti non hanno) vanno al pilota per mail e restano nel filo; il pilota
  risponde dal link. Chi risponde dal FOD: chiunque abbia `Contacts.View` sul FOD (coordinator, assistant,
  advisor) e il validatore.
- L'esito di una contestazione accolta: un validatore con `Tours.Validate` **riapre** il PIREP (da `Rejected` a
  `Queued`, con una riga in `fo_pirep_events`) ⚖️; il pilota lo sa dal filo.
- **Il pilota vede quale regola ha violato, mai i contatori** (ADR-009): né nella pagina del tour né nella mail.

### 3.9 Il completamento e l'award

- Quando un PIREP accettato completa il tour (§2), il servizio scrive `completed_at` sull'iscrizione.
- `fo_enrolments` implementa **`IProjectable`** e, con `completed_at` e un `award_id` sul tour, proietta una
  **`AwardSignalProjection`** (`vid`, motivo «ha completato il tour …»): l'interceptor scrive la segnalazione
  nella stessa transazione, come già fa per il nucleo. Nessuna assegnazione automatica.
- Un `Container` completato segnala l'award del padre; un sottotour completato quello del sottotour, se c'è.
- **Catalogo e assegnazioni** sono del nucleo (estensione n.7, §11).

### 3.10 Segnalare un problema su una leg

Dalla pagina del tour, «Segnala un problema»: testo libero, una riga in `fo_leg_issues`, una notifica alla
casella del FOD (`flightops.legIssueReported`). Lo staff la chiude, o apre la leg e la modifica (con il
`change_reason`, §1.4).

---

## 4. La validazione

### 4.1 La coda

- **Una lista generata** (`DataTable` con configurazione di colonne, `MapCrud` in sola lettura con i filtri):
  tour, leg, pilota, data del volo, in coda da, stato, preso da.
- **Chi vede**: chiunque abbia `Tours.Validate` su **almeno un** tour vede **tutti** i PIREP in sola lettura
  (risposta 41 «può vedere le leg di tutti i tour, però solo in visione»); il tasto «Prendi» c'è solo sui tour
  per cui è abilitato.
- **I propri PIREP** non compaiono (§3.6).
- Ordine: dal più vecchio ⚖️ (Toursystem aveva lasciato aperta la priorità, REVISIONE B11).

### 4.2 La presa in carico

- «Prendi» porta il PIREP a `InReview` con `assigned_to_vid` e `lease_until = now + leaseMinutes`.
- Riaprirlo rinnova il lease; alla scadenza chiunque abilitato può prenderlo (il PIREP resta `InReview` con un
  lease scaduto, e la coda lo mostra come libero: **nessun job** che lo rimetta in coda).
- Due validatori che prendono insieme: vince il primo (`row_version`), il secondo riceve un conflitto.

### 4.3 La pagina di validazione

Una **schermata dedicata** (non un form generato: eccezione dichiarata come l'editor delle pagine, §8.5).

- **La leg e il volo**: tour, leg, mappa della tratta, piano di volo al decollo, SID/STAR/approccio,
  esenzioni con il loro stato (verificata / non verificabile / non valida), deviazione con il motivo, note del
  pilota, esiti dei **controlli automatici** (§6).
- **Il profilo del pilota**: nel tour leg volate, accettate, rifiutate.
- **La tabella degli errori**: tutti gli errori attivi collegati alle regole **effettive** della leg (dallo
  snapshot), con accanto per ciascuno **quante volte nell'anno solare** e **da sempre** è stato confermato al
  pilota; quelli **suggeriti** dai controlli automatici sono già evidenziati (non segnati).
- **Il suggerimento**, calcolato dal server sugli errori che il validatore segna:
  - un `Dangerous` segnato → suggerisce il rifiuto;
  - un `Warning` segnato che porta il conteggio dell'anno oltre `yearly_max` → suggerisce il rifiuto;
  - solo `Info`, o nulla → suggerisce l'accettazione.
- **La decisione**: accetta, da modificare, rifiuta, con `note_to_pilot` e `staff_note`. Se la decisione è
  diversa dal suggerimento, `threshold_overridden = true` e il validatore scrive perché (ADR-010).
- **Il conteggio «nell'anno»**: errori confermati su PIREP **decisi** con decollo nell'anno solare corrente ⚖️
  (anno del volo, non della decisione: un volo di dicembre deciso a gennaio conta nell'anno in cui è stato fatto).

### 4.4 La tolleranza sul rifiuto

Con `FlyAhead`, un rifiuto rende la leg da rivolare ma **non invalida** le leg successive il cui decollo è
avvenuto entro `decided_at + rejectGraceHours`. Dopo quel momento, un tour in sequenza **blocca** l'invio di
PIREP sulle leg successive finché la leg rifiutata non è di nuovo in attesa o accettata ⚖️.

---

## 5. Regole ed errori

### 5.1 Le schermate

- **Regole generali**, **errori**: liste e form **generati** (`MapCrud`), con le colonne «senza errori» /
  «senza regole» e i filtri. Il collegamento molti a molti è un campo del form (selezione multipla) ⚖️
  (se il generatore non lo sa fare, è un'estensione del generatore di form, come il gruppo richiudibile).
- **Regole del tour**: una scheda dell'editor del tour, con «copia da un altro tour» e, per ogni regola
  generale, «emenda nel tour».

### 5.2 Le regole effettive di una leg

Le **generali** non ritirate, **sostituite** da quelle del tour che le emendano, **più** quelle del tour; per un
sottotour, prima il padre poi il sottotour (il più specifico vince). Le calcola un solo servizio, usato dalla
pagina pubblica del tour, dal PIREP (snapshot) e dalla validazione.

### 5.3 Gli errori pubblici

Un **blocco Data** del modulo, `flightops.errorCatalog`: il FOD lo mette in una pagina o in un documento quando
vuole (risposta 42). Mostra nome, categoria, descrizione e le regole collegate; ⚖️ solo gli errori con
`is_public`, così il FOD può tenere interni gli esempi di un errore delicato. Sempre `live`.

### 5.4 Le regole congelate

Al **primo invio** il PIREP copia in `rules_snapshot_json` le regole effettive della leg con i loro errori
(nome, categoria, `yearly_max`). Un **reinvio** dopo «da modificare» **non** rifà la copia ⚖️: il volo è lo
stesso e le regole di quel giorno restano. La validazione legge lo snapshot; i **contatori** leggono gli errori
confermati di tutti i PIREP (quelli sì con la loro categoria congelata).

---

## 6. I controlli automatici (estensione del nucleo n.5 e n.8)

### 6.1 Che cosa fanno

Un PIREP inviato (o reinviato) mette in coda un **job del modulo**, che esegue i controlli sul volo e scrive
un risultato per controllo in `fo_check_results` (`pirep_id`, `check_key`, `outcome`, `evidence_json`,
`ran_at`). Per ogni controllo **non superato**, gli errori con quel `check_key` diventano **suggeriti** in
`fo_pirep_errors`.

### 6.2 La forma

- Un controllo è una classe del modulo `IFlightCheck` con una `Key` e un `EvaluateAsync(FlightCheckContext)`
  che restituisce `Passed`, `Failed` (con l'evidenza) o **`Unavailable`** (i dati non ci sono: mai «fallito»,
  ADR-011).
- Il contesto porta il PIREP, lo snapshot delle regole, il piano di volo al decollo, **le tracce** del volo
  (`/v2/tracker/sessions/{id}/tracks`, dal client unico) e l'archivio ATC (§6.5).
- Le **soglie** (tolleranze) sono impostazioni della divisione per controllo (`checks.<key>`), con i valori di
  partenza di `RULESET_PROPOSTA_IT.md` §8.

### 6.3 La macchina propone

- **Nessun controllo decide.** Suggerisce errori; il validatore conferma o no.
- **Prima stagione in sola informazione** (`checksMode = Informational`, ADR-036): i risultati si mostrano ma
  **non** evidenziano errori; si registra se il validatore ha poi confermato l'errore del controllo, per sapere
  quali controlli promuovere. Il FOD passa a `Suggest` quando vuole.

### 6.4 I controlli di M2 (livello A di Toursystem, solo dati IVAO e archivio ATC)

| Chiave | Controlla | Dati |
|---|---|---|
| `reportWindow` | PIREP entro X giorni dal volo | PIREP |
| `callsign` | callsign consentito / vietato | piano, §1.5 |
| `aircraft` | tipo consentito | piano |
| `landingAtArrival` | atterrato all'arrivo (o deviazione dichiarata) | tracce |
| `disconnections` | disconnessioni in volo (15 minuti, 25 in totale) | tracce |
| `parking` | 3 minuti fermo prima del push e dopo l'arrivo | tracce |
| `speed250` | 250 kt sotto FL100 (+ tolleranza) | tracce, esenzioni |
| `semicircularLevels` | livelli semicircolari | piano, tracce |
| `simRate` | velocità riportata coerente con quella di posizione | tracce |
| `alternate` | alternato presente e non `ZZZZ` | piano |
| `equipment` | equipaggiamento richiesto | piano |
| `dailyLimit` | limiti giornalieri | PIREP |
| `atcCoverage` | ATC online sul percorso, e verifica delle esenzioni | archivio ATC (§6.5) — informativo |

Il riferimento per la logica è il validatore Python (`AutomaticValidatorTour`, ~13 300 righe): si porta la
logica di questi controlli, non il programma. Ogni controllo ha i suoi test con **voli veri** (il corpus di
39 log di Toursystem, `tests/corpus/`).

### 6.5 L'archivio ATC

Un'interfaccia del nucleo, `IAtcActivitySource` («quali posizioni erano online in questo intervallo, e dove»),
con due implementazioni: la **vista di vIPI** `v_share_atc_sessions` (piano 0.78) e **nessuna** (tutto
`Unavailable`). Il modulo non nomina vIPI.

---

## 7. Permessi

### 7.1 Il catalogo del modulo

| Permesso | Che cosa permette |
|---|---|
| `Tours.View` | vedere nel back office tour in bozza, template, liste |
| `Tours.Edit` | creare, modificare, riprogrammare (date) tour e leg, segnare pronto, importare leg, gestire le segnalazioni sulle leg |
| `Tours.Delete` | eliminare un tour in bozza o ritirare un tour aperto ⚖️; ritirare una leg |
| `Tours.ManageRules` | regole generali e del tour, errori, collegamenti |
| `Tours.ManageTemplates` | template di tour |
| `Tours.Validate` | prendere e decidere PIREP; **con scope per tour** (§7.2) |
| `Tours.ManageValidators` | abilitare e togliere validatori |
| `Tours.ViewPilots` | pagina del pilota, statistiche dei validatori |
| `Tours.ManageSettings` | impostazioni della divisione (§1.11), soglie dei controlli |

### 7.2 Chi li ha (`division.json → positionGrants`)

| | Coordinator FOD | Assistant FOD | Advisor FOD | Validatore abilitato |
|---|---|---|---|---|
| `Tours.View` | ✓ | ✓ | ✓ | |
| `Tours.Edit` | ✓ | ✓ | ✓ | |
| `Tours.Delete` | ✓ | ✓ | — (grant al singolo VID) | |
| `Tours.ManageRules` | ✓ | ✓ | ✓ | |
| `Tours.ManageTemplates` | ✓ | ✓ | — | |
| `Tours.Validate` | ✓ tutti i tour | ✓ tutti i tour | ✓ tutti i tour | ✓ i tour abilitati |
| `Tours.ManageValidators` | ✓ | ✓ | — | |
| `Tours.ViewPilots` | ✓ | ✓ | ✓ ⚖️ | ⚖️ |
| `Tours.ManageSettings` | ✓ | ✓ | — | |
| rispondere alle contestazioni | ✓ | ✓ | ✓ | ✓ sulle proprie decisioni |

HQ e superadmin tengono tutto, come oggi. Le righe di `positionGrants` si scrivono in `division.json` (e in
`division.example.json` con il commento) nella fase del modulo.

### 7.3 Le due estensioni del meccanismo dei permessi (estensione del nucleo n.1)

Servono **due** cose che il meccanismo di oggi non dice, e vanno nell'unico handler, non nel modulo:

1. **Uno scope più stretto del dipartimento**: «`Tours.Validate` sul tour 42». Proposta: `hub_user_grants`
   guadagna una colonna **`resource_scope`** (per esempio `flightops:tour:42`); una risorsa che implementa
   **`IHasResourceScope`** dichiara il suo (`flightops:tour:{tour_id}` per un PIREP); l'handler conta un grant con
   scope **solo** su una risorsa con lo stesso scope, e un grant **senza** scope come oggi. La schermata «aggiungi
   validatore» scrive un grant; audit, sospensione e rinnovo della sessione vengono gratis.
2. **Un vincolo della riga contro chi agisce**: «chi decide non è il pilota». Proposta: un'interfaccia
   **`IHasStakeholder`** (`int StakeholderVid`) e un elenco di permessi che una risorsa con stakeholder **non**
   concede allo stakeholder (dichiarato dal modulo: `Tours.Validate`). L'handler lo applica a tutti i grant,
   superadmin compreso ⚖️.

Tutte e due vogliono una **nota di decisione** prima del codice (`CLAUDE.md` §5, caso c) e i test della spina
dorsale estesi.

---

## 8. Schermate

### 8.1 Pubblico

- **`/tours`**: i tour aperti (e in chiusura ⚖️) come **riquadri**: foto, titolo, riassunto; per un pilota con
  login, **barra di avanzamento** e **prossima leg** (§2). Filtri per tipo ⚖️. Anonimo: niente avanzamento.
- **`/tours/{slug}`**: briefing, date, aerei, regole effettive del tour, **mappa** (§8.6) con le leg **blu** da
  fare e **verdi** fatte (e ⚖️ grigie non ancora rilasciate, arancioni in attesa), elenco delle leg con stato,
  distanza, callsign reale, pulsante **SimBrief** per leg; per un pilota, «Invia il report» sulle leg volabili,
  i suoi PIREP con esito, «Contesta» sui rifiutati, «Segnala un problema».
- **Il form del PIREP**: una finestra dedicata (sceglie un volo da una lista, poi campi generati dallo schema
  zod) ⚖️ dedicata perché il primo passo è una scelta, non un campo.

### 8.2 Blocchi Data del modulo

| Blocco | Dove | Che cosa |
|---|---|---|
| `flightops.tourCards` | pagine pubbliche, `/me` | i riquadri di `/tours`, per chi guarda |
| `flightops.myTours` | `/me` | i tour iniziati, avanzamento, prossima leg, PIREP da correggere |
| `flightops.reviewQueue` | dashboard FOD, `/staff` | PIREP in coda sui tour che chi guarda può validare, i più vecchi |
| `flightops.openIssues` | dashboard FOD | segnalazioni aperte sulle leg |
| `flightops.errorCatalog` | pagine, documenti | gli errori pubblici (§5.3) |

Tutti `AlwaysLive`, rispondono per chi guarda (nota delle dashboard §3.1).

### 8.3 L'editor del tour (staff)

- **`/staff/tours`**: lista generata di tutti i tour passati, presenti e futuri, con stato calcolato, tipo, date,
  iscritti, PIREP in coda; un filtro «template».
- **`/staff/tours/{id}`**: schede.
  - **Impostazioni**: form generato (tipo, titolo, riassunto, foto, date, finestra, progressione, limite
    giornaliero, aerei, award, «a cura di»).
  - **Briefing**: l'editor dei blocchi (lo stesso delle pagine).
  - **Hub e rotazioni** (solo `Hub`), **sottotour** (solo `Container`), **vincoli sul callsign**: liste generate.
  - **Leg**: l'**editor delle leg** (§8.4).
  - **Regole del tour** (§5.1).
  - **Validatori** ⚖️ (o solo nella pagina delle statistiche, risposta 41).
  - Barra: «Segna pronto» con l'elenco dei problemi di pubblicazione, «Nuovo da template», «Salva come template».

### 8.4 L'editor delle leg (eccezione dichiarata, estensione del nucleo n.6)

- **Una tabella modificabile riga per riga** (non un form per leg): numero, partenza, arrivo, IATA e distanza
  calcolati al volo, callsign reale, numero di volo, aerei, rotazione, data di rilascio, stato.
- **Azioni sulla riga**: «aggiungi dopo: duplica» (copia tutto tranne gli aeroporti), «aggiungi dopo: segue» (la
  partenza è l'arrivo di questa), «chiudi tour» (l'arrivo diventa la partenza della prima leg), «ritira».
- **Import XLSX/CSV**: il file si legge **nel browser** ⚖️ (nessuna libreria nel server; una libreria di lettura XLSX
  nel frontend), il server riceve le righe e risponde con l'**anteprima delle differenze** (nuove, modificate,
  invariate, errori per riga: ICAO sconosciuti, numeri doppi). Poi «applica». Regole di ADR-051: chiave
  `(tour, numero di leg)`; **fonde** di default e non tocca le leg assenti dal file; «sostituisci» **ritira** le
  assenti (mai cancella) e solo con un motivo; una leg con PIREP si modifica solo con `change_reason`. Un modello
  di file scaricabile con le colonne.
- Salvataggio **a righe**, con `row_version` per riga e gli errori del server mappati sulla cella.

### 8.5 La validazione (staff)

- **`/staff/tours/review`**: la coda (§4.1).
- **`/staff/tours/review/{pirepId}`**: la pagina di validazione (§4.3), schermata dedicata.

### 8.6 La mappa (estensione del nucleo n.3)

- **Un componente nuovo** nell'elenco chiuso: `RouteMap` (linee fra coppie di coordinate, colori per stato,
  marcatori degli aeroporti). Libreria ⚖️ **Leaflet** (la più piccola, senza WebGL) oppure MapLibre.
- **Tessere**: da un fornitore **dichiarato** in `config/security.json` (`img-src` e, se servono, `connect-src`),
  con l'attribuzione visibile. ⚠️ Le tessere di `tile.openstreetmap.org` **non** si possono usare per un sito con
  traffico senza rispettarne la policy di uso: la nota di decisione sceglie il fornitore (per esempio un piano
  gratuito di un fornitore commerciale, o tessere proprie) ⚖️.
- Le linee sono **ortodromiche** (archi di cerchio massimo), non rette sulla proiezione.

### 8.7 Le altre pagine dello staff

- **Statistiche dei validatori** (`/staff/tours/validators`): per anno, per validatore, leg validate, accettate,
  rifiutate; per ogni tour dell'anno corrente e del precedente, per validatore, lo stesso. Da qui «aggiungi
  validatore» (un VID già staff della divisione, i tour per cui abilitarlo) e «togli». Liste generate su endpoint
  di aggregazione ⚖️ (un `MapCrud` in sola lettura su una query aggregata, se il motore lo permette).
- **Pagina del pilota** (`/staff/tours/pilots/{vid}`): riepilogo degli errori confermati per categoria e per errore
  (anno solare e da sempre), elenco di tutte le leg volate con esito, validatore, data; i suoi tour con
  avanzamento. La ricerca del pilota per VID o nome.
- **Regole generali**, **errori**, **template**, **segnalazioni sulle leg**, **impostazioni**: liste e form generati.
- **Punti e classifiche**: ⚖️ da definire — Toursystem li teneva per lo staff; requisiti §6 li vuole solo per lo staff,
  ma non dice quali. Proposta: rimandarli dopo M2 insieme all'Online Day.

---

## 9. Proiezioni, ricerca, calendario, notifiche

- **Ricerca** (`IProjectable` su `fo_tours` pubblicati): titolo e riassunto, visibilità pubblica, URL `/tours/{slug}`.
- **Calendario**: un tour pronto proietta **due voci** ⚖️ (rilascio e chiusura) di tipo `tour`, pubbliche. Il
  contratto di `IProjectable` oggi porta **una** `CalendarProjection` per riga: servono due voci, oppure una sola
  voce «aperto dal … al …». Proposta: **una** voce con inizio e fine (niente estensione).
- **Notifiche** (tipi del modulo, con preferenza del membro): `pirepAccepted`, `pirepToModify`, `pirepRejected`,
  `disputeReplied`; alla casella del FOD: `disputeReceived`, `legIssueReported`. ⚖️ `tourCompleted` al pilota.

---

## 10. Conservazione

- **Mai cancellati**: la decisione del PIREP (`outcome`, validatore, data), gli **errori confermati**
  (`fo_pirep_errors`), la storia (`fo_pirep_events`) — il **registro disciplinare** (requisiti §7).
- **Cancellati** dopo `retentionYears` (2) dalla chiusura del tour, `retentionYearsLong` (4) per un tour su più di
  un anno: tracce, piano di volo, esiti dei controlli, snapshot delle regole, note del pilota, esenzioni.
- Un job mensile del modulo, con una riga in `hub_job_log` per esecuzione ⚖️.

---

## 11. Che cosa chiede al nucleo

Ogni voce ha una fase sua, **prima** della fase del modulo che la usa, e le prime tre una nota di decisione.

| # | Estensione | Nota | Dove |
|---|---|---|---|
| 1 | Scope per risorsa nei grant e stakeholder della riga | sì | §7.3 |
| 2 | Contatti con filo di risposte, legati a una riga di modulo, con partecipanti in più | sì | §3.8 |
| 3 | Componente mappa e fornitore di tessere nella CSP | sì | §8.6 |
| 4 | Aeroporti di tutto il mondo con IATA e coordinate | no (approvata, risposta C.4) | §1.12 |
| 5 | Tracker nel client IVAO: sessioni per pilota e aeroporti, piani di volo, tracce | no (risposta C.5) | §3.2, §6 |
| 6 | Editor a tabella e import XLSX/CSV con anteprima | eccezione da dichiarare nel piano (§8.3 dei componenti) | §8.4 |
| 7 | Award: catalogo (`hub_awards`) e assegnazioni (`hub_award_assignments`) accanto a `hub_award_signals`, schermata di chi ha `Awards.Assign`, immagini dalla media library | no (piano §9.1, già deciso) | §3.9 |
| 8 | `IAtcActivitySource` con l'implementazione sulla vista di vIPI | coperta da piano 0.78 | §6.5 |
| 9 | Selezione multipla e (forse) aggregati nel generatore di form e nella lista | da verificare con il codice | §5.1, §8.7 |

⚠️ **Award dall'API IVAO** (risposta C.7): la documentazione `api.ivao.aero/docs` risponde **403** (verificato
il 14 settembre 2026, come aveva trovato Toursystem), e il catalogo award di IVAO non è documentato. Il catalogo
del nucleo si **carica a mano** (nome, immagine, criterio); se un endpoint IVAO si trova, un job lo sincronizza
dopo. Lo stesso catalogo terrà più avanti i badge dei rating per il training.

---

## 12. Tabelle e migrazioni

**Nucleo** (migrazioni additive in `HubDbContext`):

- `ref_ivao_airports`: `iata`, `latitude`, `longitude`.
- `hub_user_grants`: `resource_scope`.
- `hub_contact_messages`: `source_module`, `source_id`, `participants_json`; nuova `hub_contact_replies`.
- `hub_awards`, `hub_award_assignments`.

**Modulo** (`FlightOpsDbContext`, prima migrazione `Initial`): `fo_tours`, `fo_hubs`, `fo_rotations`, `fo_legs`,
`fo_callsign_rules`, `fo_rules`, `fo_errors`, `fo_rule_errors`, `fo_pireps`, `fo_pirep_flights`,
`fo_pirep_errors`, `fo_pirep_events`, `fo_check_results`, `fo_enrolments`, `fo_leg_issues`.

**vIPI** (nel suo repository): la vista `v_share_atc_sessions` e l'utente di sola lettura (piano 0.78 §4).

---

## 13. La rete di test

- **Unità**: `TourRules` per ogni tipo (volabili, prossima, finito) con tabelle di casi; GCD (i test di Toursystem);
  regole effettive; suggerimento con le soglie; limiti giornalieri; ogni `IFlightCheck` su **voli veri** del corpus.
- **Integrazione** (MariaDB vera, database condiviso: VID e slug propri del modulo, per esempio `VID 780001–780099`,
  slug `fo-test-…`): il ciclo di un PIREP con tutti gli stati; nessuno valida i propri; validatore abilitato su un
  tour e non su un altro; la sessione rivendicata una volta; il limite di divisione che non si spegne; il
  completamento che scrive la segnalazione award; la conservazione che non tocca il registro disciplinare.
- **Architettura**: il modulo non nomina vIPI; il modulo non chiama IVAO se non dal client del nucleo.
- **Smoke** (Playwright con API finta): `/tours`, `/tours/{slug}` con la mappa, il form del PIREP, la coda, la pagina
  di validazione, l'editor delle leg con un import.
- **Giro completo** (API vera): un tour creato da template, un PIREP inviato con un tracker finto (fixture del
  client), preso, validato, contestato e riaperto.
- **Divisione XX**: nessuna stringa italiana, nessun ICAO italiano nei seed del modulo.

---

## 14. Ordine di lavoro proposto (da scrivere in `06` parte C dopo la revisione)

| Fase | Contenuto |
|---|---|
| T0 | Note di decisione: permessi con scope e stakeholder (§7.3), contatti con risposte (§3.8), mappa e tessere (§8.6); piano 0.79 |
| T1 | Nucleo: aeroporti del mondo con IATA e coordinate; tracker nel client IVAO (con fixture) |
| T2 | Nucleo: scope per risorsa e stakeholder nell'unico handler, con i test della spina dorsale |
| T3 | Nucleo: catalogo e assegnazioni award, schermata di assegnazione |
| T4 | Modulo: scheletro `flightops` (progetto, contesto, `IModule`, sezione della barra, permessi, `positionGrants`), impostazioni |
| T5 | Tour: modello, lista, impostazioni, briefing, stato dalle date, controlli «pronto», template |
| T6 | Leg: editor a tabella, GCD, hub e rotazioni, sottotour, vincoli sul callsign |
| T7 | Import XLSX/CSV con anteprima |
| T8 | Regole ed errori: schermate, collegamenti, regole effettive, blocco `errorCatalog` |
| T9 | Pubblico: `/tours`, `/tours/{slug}`, componente mappa, `tourCards`, `myTours` |
| T10 | PIREP: ricerca nel tracker, form, deviazioni, esenzioni, limiti, iscrizione, `TourRules` per ogni tipo |
| T11 | Validazione: coda, presa in carico, pagina di validazione, suggerimenti, mail, `reviewQueue` |
| T12 | Contestazioni (nucleo: contatti con risposte) e segnalazioni sulle leg |
| T13 | Completamento e segnalazioni award; statistiche dei validatori; pagina del pilota |
| T14 | Controlli automatici: motore, job, primi controlli sul piano di volo |
| T15 | Controlli sulle tracce; `IAtcActivitySource` con vIPI; `atcCoverage` ed esenzioni verificate |
| T16 | Conservazione, calendario e ricerca, rifiniture, giro completo |

---

## 15. Da confermare con Carmine (⚖️)

Raccolte qui per rispondere a blocchi; il resto del documento le segna dove nascono.

1. **Tour «in arrivo»** (pronto, prima del rilascio): visibile al pubblico come anteprima, o solo allo staff?
2. **Template**: copiano anche **le leg** e hub/rotazioni, o solo impostazioni e regole?
3. **Errori pubblici**: una bandiera `is_public` per errore, o il blocco mostra sempre tutti gli errori attivi?
4. **SID/STAR/approccio**: obbligatori per tutti i PIREP IFR (regola RR5), con i tour VFR che la emendano? O una
   bandiera del tour?
5. **Callsign e aereo sbagliati all'invio**: avviso (decide il validatore) o blocco?
6. **Limite giornaliero di divisione superato**: avviso o blocco? (Il limite del tour blocca.)
7. **Contestazione**: entro quanti giorni dal rifiuto? E l'accoglimento **riapre** il PIREP in coda?
8. **Anno dei contatori**: anno del **volo** o della **decisione**?
9. **Tour `Free`**: il riquadro senza «prossima leg» dice «scegli una leg»?
10. **Tour `Hub`**: le rotazioni di un hub in qualunque ordine fra loro (ognuna in ordine al suo interno)?
11. **`SequentialChosenStart`**: il tour deve essere ad anello?
12. **`Distance`**: si conta la GCD delle leg accettate? Le leg si volano libere?
13. **Sottotour**: ereditano aerei, callsign e regole dal padre se non ne hanno? Un sottotour può avere un award suo?
14. **Rifiuto in un tour in sequenza**: dopo la tolleranza si blocca l'invio delle leg successive finché la leg
    rifiutata non è rivolata?
15. **Stakeholder**: nemmeno il superadmin può validare i propri PIREP?
16. **`Tours.Delete`**: vale anche per ritirare un tour già aperto (con PIREP)?
17. **Advisor e validatori**: vedono la pagina del pilota e le statistiche?
18. **Validatori**: si abilitano anche dalla scheda del tour, o solo dalla pagina delle statistiche?
19. **Punti e classifiche per lo staff**: rimandati dopo M2 con l'Online Day?
20. **Calendario**: una voce «tour aperto dal … al …» o due voci (rilascio, chiusura)?
21. **Mail di tour completato** al pilota?
22. **Conservazione**: 2 e 4 anni dalla chiusura del tour, confermati?
23. **Mappa**: Leaflet, e quale fornitore di tessere (da scegliere nella nota)?
24. **Import**: lettura del file XLSX nel browser (una libreria nel frontend) invece che nel server?
25. **Ordine della coda**: dal PIREP più vecchio?
