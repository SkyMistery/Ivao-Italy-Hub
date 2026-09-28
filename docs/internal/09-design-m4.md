# IVAO Division Hub — Design di M4 (il modulo Events)

> Documento **interno** (italiano). Fonte di verità: `00-piano-di-progettazione.md` (versione 1.23).
> Ingresso: quello che il piano e le note hanno già deciso sugli eventi (§R.1), quello che dicono gli strumenti di oggi
> (§R.2) e la documentazione pubblica dell'API di IVAO, letta il 28 settembre 2026 (§6). **I fatti sullo staff ED — come
> lavora oggi, che cosa vuole — non sono ancora stati chiesti a nessuno dell'ED**: sono in §R.3 come domande, e la prima
> domanda a Carmine è a chi farle (§14 n.1). **Le scelte le decide Carmine**: nel testo sono segnate **⚖️**, e in §14 c'è
> ognuna con la raccomandazione. Le fasi si scrivono in `10-piano-implementazione-m4.md` **dopo** l'approvazione di questo
> documento. I modelli sono `05-design-m2.md` e `07-design-m3.md`.

**Stato:** **proposta**, 28 settembre 2026. Nessun codice. Le domande di §14 sono sulla PR come commento; quando Carmine
risponde, ogni risposta entra qui con il link al suo commento, e il prossimo passo è la fase E0 (§13).

---

## 0. Perimetro

### 0.1 Che cosa è «fatto»

M4 è fatta quando lo staff ED può **spegnere `ivao-booking`** (piano §13, riga M4) e il calendario del sito Blazor non
serve più per gli eventi:

- lo staff crea un evento — divisionale, di HQ, RFE, RFO, online day — con titolo e descrizione tradotti, banner,
  aeroporti, orari, e lo **pubblica**, anche **programmandone l'uscita** a una data (§2.2); l'evento entra nel
  **calendario unico** e nella ricerca (§5.1);
- un evento ha **un modo di prenotazione** (§2.4): nessuno, **iscrizione** (ci sarò), o **slot** (RFE/RFO: lo SES del
  template di HQ, piano §9.6);
- per un evento a slot lo staff **carica gli slot da un file** (sono centinaia: 441 per Roma il 3 ottobre, §R.2), i membri
  **prenotano** dall'ora di apertura, e il **Gate Manager** della divisione legge le prenotazioni dall'hub invece che da
  `booking.it.ivao.aero` (§4.4);
- lo staff indica le **postazioni ATC** da aprire, i controllori **chiedono un turno**, lo staff lo conferma; la pagina
  mostra anche chi ha prenotato quelle postazioni **su IVAO** (§2.8, §6.1);
- partono le mail di ogni passaggio e il promemoria (§5.3); il membro vede i suoi eventi in `/me` (§4.3);
- dopo l'evento l'hub **verifica chi ha volato e chi ha controllato**, e segnala l'award dell'evento a chi lo assegna
  (§2.9; ⚖️ §14 n.12).

### 0.2 Fuori perimetro

- **Lo storico di `ivao-booking`**: nessun import (piano §12 punto 3, `CLAUDE.md` §7). Il vecchio booking resta
  consultabile in sola lettura per un periodo, poi un 301 verso `/events`: si fa fuori dal repository (§13, fase E10).
- **La prenotazione della postazione su IVAO** (`atc.ivao.aero`): resta di IVAO. L'hub la **legge** e non la scrive
  (`bookings:write` chiederebbe il token del controllore, §6.1; ⚖️ §14 n.8).
- **Gli eventi di HQ presi da un'API**: l'API pubblica di IVAO non ha eventi (§6.2). Un evento di HQ si scrive a mano, con
  il link alla sua pagina (⚖️ §14 n.2).
- **L'assegnazione degli stand durante l'evento**, le rotazioni, il ponte fra le postazioni: sono del **Gate Manager**
  (`RFO Gate manager`, repository a sé) e del suo ponte su `atc.it.ivao.aero`. L'hub gli dà le prenotazioni (§4.4) e
  nient'altro.
- **Mail a tutti i membri per un evento nuovo**: no (⚖️ §14 n.14). Lo annunciano calendario, home e, in M6, Discord.
- **Il feed iCal**: resta in M6 con la domanda aperta del piano §15.9, come per M3.
- **Award a più livelli** (per esempio «dieci eventi»): l'hub segnala la presenza a un evento; una serie si decide quando
  lo staff la chiede (§5.2).

### 0.3 Che cosa M4 non rimette in discussione

- **I meccanismi del nucleo** (`CLAUDE.md` §2, piano §16): `MapCrud`, lista e form generati, l'unico handler, il filtro
  globale, `IOwnedByDepartment` a insieme con `ModuleBaseDepartment.Keep`, i grant a una posizione e per riga
  (`IHasResourceScope`), `IHasStakeholder` e `DeniedToStakeholder`, `ISubmittedByMembers`, `IProjectable` (calendario,
  ricerca, award, usi dei file, apertura di un contatto), il servizio notifiche, le impostazioni dei moduli, i blocchi
  Data, `Refusals`, i token personali con la loro `audience`, `IPersonalDataEraser`, l'unico `IIvaoApiClient`,
  `IAtcActivitySource` per le viste `v_share_`.
- **I moduli fuori dai dipartimenti**: sezione `/staff/events`, «a cura di» con l'ED sempre presente
  (`division.json → modules.events.baseDepartment: ED`, già scritto; il commento di `ModuleBaseDepartment` dice proprio
  «events are always the Events department's»).
- **Chi gestisce il modulo** (piano 0.77): coordinator e assistant dell'ED tutto, per grant a una posizione; gli advisor li
  decide questo documento (§3.2); HQ e web tutto per il nucleo; chi collabora modifica e **non cancella** (§3.3).

### 0.4 I nomi

| Che cosa | Nome |
|---|---|
| Chiave del modulo (`IModule.Key`, `division.json`, storia delle migrazioni) | `events` |
| Progetto .NET | `IvaoHub.Modules.Events` |
| Frontend | `web/src/modules/events/` |
| Prefisso delle tabelle | `evt_` |
| API | `/api/events/...` |
| Aree dei permessi | `Events`, `EventBookings`, `EventAtc` (§3.1) |
| Rotte pubbliche e dei membri | `/events`, `/events/{slug}`, `/events/mine` (segmento riservato `events`) |
| Rotte dello staff | `/staff/events/...` |
| Namespace i18n | `events` |
| Fasi e branch | `E0`…`E10`, `m4/e<N>-<slug>` |

La sitemap del piano (§8.2) scrive `/me/bookings`: come in M3, `/me` è la dashboard del nucleo fatta di blocchi, e il
modulo ci entra con il blocco `events.myEvents` (§4.3); le pagine del membro stanno sotto `/events/mine`.

### 0.5 Che cosa si prende dagli strumenti di oggi, e che cosa no

| Si prende | Da | Dove qui |
|---|---|---|
| Lo slot come riga preparata dallo staff (callsign, partenza, arrivo, orari, aereo, stand) che un membro prende | `ivao-booking`, come lo legge il Gate Manager (`spec/SPEC.md` §3, `DAT-BOOKING`) | §1.4 |
| Lo **slot privato**: il pilota prenota un volo suo, con il solo orario allo scalo dell'evento e stand da assegnare | idem (21 slot privati a Napoli il 19 settembre) | §2.6 |
| Il formato dei campi letti dal Gate Manager (`callsign`, `booked_by`, `aircraft_icao`, `gate`, `eobt`, `eat`, `origin_icao`, `destination_icao`), orari in UTC | idem | §4.4 |
| Ambito e tipo di un evento (Divisional, HQ, RFO, RFE; Online, Live) e lo SES come modo di prenotazione | backend del template HQ (piano §2.3-ter, §9.6) | §1.2, §2.4 |
| La presenza di un pilota e di un controllore **verificata** dentro la finestra dell'evento, per l'award | Toursystem (`event_participation`, minuti su postazioni ammesse) | §2.9 |
| Eventi, RFE e calendario delle attività in un posto solo | sito Blazor, «Eventi» e «Calendario attività» (piano §2.1) | §4.1, §5.1 |

| Non si prende | Perché |
|---|---|
| Lo slot libero come «`booked_by` nullo, 0 o non numerico», nessuno stato | la prenotazione è **una riga sua** (§1.5): chi, quando, in che stato |
| La direzione dello slot da `type_of_flight` (inaffidabile, `SPEC.md` §3) | si deduce dagli ICAO rispetto agli aeroporti dell'evento |
| Una chiave API condivisa (`x-api-key`) per leggere le prenotazioni | un **token personale** con la sua `audience` (§4.4, `CLAUDE.md` §2) |
| L'endpoint che serve «l'evento corrente» senza data | un indirizzo per evento, per slug |
| I punti bonus e i mini-tour degli eventi del Toursystem | sono dei tour (M2), se un giorno servissero |

### 0.6 Scostamenti dal piano

Dichiarati qui perché il revisore li trovi senza cercarli.

| Il piano dice | Il design fa | Perché | Dove |
|---|---|---|---|
| `events.airport_icao FK → ref_.ivao_airports` (§7, schizzo `evt_`) | nessuna FK: una tabella figlia `evt_event_airports`, validata con `IAirportDirectory` | nessuna FK fra contesti (§16.12); un RFE può avere più scali | §1.3 |
| `events.tipo`: RFE, online day, **training, exam**… | training ed esami sono di M3; il tipo dell'evento è una **chiave del vocabolario dei tipi del calendario** | una voce sola per tipo, colori compresi, e la divisione che forka ha i suoi | §1.2; ⚖️ §14 n.3 |
| `event_slots.booked_by_vid` sulla riga dello slot | la prenotazione è **una riga a sé**, `evt_bookings` | lo slot è una riga dello staff e il membro non la scrive (guardiano); l'unicità della prenotazione la tiene il database, la sera dell'apertura | §1.5, §7.1 |
| `event_slots` univoco su `(event_id, callsign)` | univoco su `(event_id, callsign, orario)` | lo stesso callsign vola più volte nella giornata (Gate Manager, `#2`, `#3`) | §1.4 |
| «`EventPublished` → il nucleo crea la voce di calendario» (§9.2) | `IProjectable`, nella stessa transazione | è il contratto deciso (§9.7, §16.4) | §5.1 |
| `/me/bookings` (§8.2) | `/events/mine`; `/me` riceve il blocco `events.myEvents` | `/me` è la dashboard del nucleo | §0.4 |
| Prenotazioni ATC dell'evento: `/atc/bookings`, «job ogni 5 min» (§10) | `/v2/atc/bookings/daily`, **letto alla richiesta** con la cache del client, nessun job | la regola dei dati «adesso» (nota `2026-09-28-i-job-quando-passenger-spegne-l-hub` §8) | §6.1, §7 |

---

## R. Che cosa sappiamo, e da dove

### R.1 Già deciso (piano e note)

- **Il catalogo** (§9.2 riga 1): eventi divisionali, di HQ, RFE, RFO, online o live; slot con prenotazione; partecipanti;
  postazioni ATC; il calendario unico. Sostituisce `ivao-booking` e il calendario del Blazor. Modulo obbligatorio, senza
  flag in `division.json`, con la sola `maintenance` a caldo.
- **SES = `booking_mode`** di Events (changelog 0.13, §9.6).
- **Un calendario per tutto** (§9.5): un RFE è `public`; eventi e sessioni di training sono pubblici (piano 0.72).
- **I moduli fuori dai dipartimenti** (piano 0.72, 0.77; note `moduli-non-subordinati`, `ordine-dei-moduli`): «a cura di»
  multiplo, l'ED sempre; chi crea mette almeno un dipartimento su cui ha il permesso («il SOD crea eventi ED + SOD»); chi
  collabora modifica e **non cancella**, «da precisare nel design degli eventi».
- **La granularità sta nel catalogo** (nota `2026-09-06-autorizzare-su-un-pezzo-di-un-altro-dipartimento`): «il FOD
  inserisce le rotte di un evento **ma non** tocca le postazioni da aprire» vuol dire **due entità con due aree**, non due
  colonne. La scelta fra «la riga è dell'evento, e serve un grant» e «la riga è del FOD» è di questo design (§3.3).
- **Un permesso temporaneo per un evento** (§6.3): un grant con `resource_scope` su quell'evento.
- **La pubblicazione programmata** (nota `2026-09-09-il-documento-dice-di-se` §2): «se rilascio un nuovo evento voglio
  poterne programmare l'uscita pubblica per una certa data»; la nota chiede che eventi e contenuti non abbiano due
  meccanismi diversi (§2.2).
- **I file con scadenza** (nota `2026-09-15-file-con-scadenza`, design M2 §1.14): «un evento dichiarerà i suoi allo
  stesso modo» dei tour.
- **I contatti su qualcosa** (nota `contatti-con-risposte`): gli eventi avranno lo stesso bisogno dei PIREP.
- **Il blocco `eventList`** atteso dal design M1 (§9.3); **«Eventi della settimana senza ATC»** come tessera di esempio
  (nota `moduli-non-subordinati` §3.4).
- **L'Online Day passa all'ED** (nota `2026-09-14-requisiti-dei-tour`); Training lo aspetta come tipo di calendario
  (`TrainingSettings.ConflictKinds`, design M3 §1.6).
- **Award**: «Events: eventi e partecipanti nel periodo»; l'award si segnala, lo assegna una persona (§9.7).

### R.2 Che cosa dicono gli strumenti di oggi (letti in sola lettura il 28 settembre 2026)

- **`ivao-booking`** (PHP, Slim, MySQL, OAuth IVAO): nessuna copia su questo PC. Lo si conosce **dal lato di chi lo
  legge**, il Gate Manager (`RFO Gate manager/spec/SPEC.md` §3, `spec/riferimenti/integrazioni.md` §2):
  `GET https://booking.it.ivao.aero/api/flights` con `x-api-key`, nessuna data nel percorso, un array dei voli
  dell'evento corrente; «risponde 200 anche sugli errori PHP». Lo slot è libero se `booked_by` è vuoto; `gate` può
  essere `TBD`; gli orari sono UTC.
- **I numeri veri**: Napoli (LIRN), 18 settembre: 173 slot, 105 prenotati, 21 slot privati. Roma (LIRF), 25 settembre per
  l'evento del 3 ottobre: **441 slot** (219 arrivi, 222 partenze) dalle 00:10 alle 23:55Z su 103 stand, 57 già
  prenotati. Il Gate Manager rilegge ogni 5 minuti «perché la gente prenota fino all'ultimo».
- **Errori dello staff sono dati veri**: a Roma il booking usava stand chiusi o inesistenti (`SPEC.md` §15, punto 21). Un
  campo stand libero resta libero (§1.4): la verifica degli stand è del Gate Manager.
- **Il Gate Manager aspetta l'hub**: «il servizio centrale arriverà col nuovo sito di IVAO Italy» (`SPEC.md` §1); chiede
  «identità stabili: … lo slot del booking, o l'id del servizio centrale» (`SPEC.md` §14). Le prove si fanno sull'evento
  `prova-ponte-rfo`.
- **Toursystem** (`doc/ARCHITETTURA.md` §7.3): eventi con `source` locale o esterna, partecipazione `PILOT|ATC` con prova
  e punti, award «Event PILOT» ed «Event ATC» separati; nel pannello HQ 1.422 report pilota e 431 ATC di eventi.
- **vIPI**: nessun evento né prenotazione, di proposito («non si duplica uno strumento che IVAO ha già»). Condivide con
  l'hub solo `v_share_atc_sessions` (§6.3).

### R.3 Da chiedere allo staff ED

Nessuno dell'ED ha ancora risposto: queste sono le domande di fatto, da fare prima dell'approvazione (§14 n.1). Il design
qui sotto usa, per ognuna, la risposta che sembra più probabile, e la segna **(R.3)**.

1. **Chi fa che cosa oggi** in `ivao-booking` e nel calendario: chi crea l'evento, chi carica gli slot, chi assegna gli
   stand, chi decide le postazioni ATC; se AOD e FOD ci lavorano, e su che cosa.
2. **Le regole della prenotazione**: quanti slot per pilota per evento (un arrivo e una ripartenza?); fino a quando si
   cancella; se c'è una lista d'attesa; se lo staff toglie una prenotazione e perché.
3. **Gli slot privati**: chi li approva, se c'è un tetto per ora, se hanno uno stand.
4. **L'ATC di un evento**: si fa un roster con turni? Di quanto? Chi lo conferma? Con che rating minimo?
5. **Che cosa serve dopo l'evento**: presenze verificate, award, statistiche (prenotati contro volati).
6. **Gli eventi di HQ e degli altri**: si ripubblicano tutti o solo quelli che toccano la divisione?
7. **L'Online Day**: che cos'è oggi in pratica (una giornata con aeroporti e postazioni? iscrizione?).
8. **Le mail** che lo staff e i membri si aspettano.
9. **Il periodo** in cui `ivao-booking` resta in sola lettura prima del 301.

---

## 1. Il modello

Tutte le tabelle stanno in `EventsDbContext : ModuleDbContext`, con la sua `__EFMigrationsHistory_events`. Nessuna FK
verso il nucleo: VID, ICAO, callsign delle postazioni e id dei file sono colonne non vincolate. Orari in UTC,
`datetime(6)`.

### 1.1 Chi possiede una riga

- **L'evento** è **`IOwnedByDepartment`** con `OwnerDepartmentMask` (l'ED c'è sempre, `ModuleBaseDepartment.Keep`),
  **`IVisible`**, **`IPublishable`**, **`IAuditable`** e **`[Audited]`**, area `[PermissionArea("Events")]`,
  **`IHasResourceScope`** (`events:event:{id}`), **`IProjectable`**.
- **Le righe dello staff figlie dell'evento** (aeroporti, slot, postazioni ATC) **copiano dipartimenti e maschera
  dell'evento** a ogni scrittura, come le leg di un tour (`CrudOptions.BeforeAuthorize`, nota `2026-09-18-le-leg-dei-tour`),
  e ne copiano lo scope, così un grant su quell'evento vale anche su di loro. Ognuna nella **sua area** (§3.1): gli slot
  in `EventBookings`, le postazioni in `EventAtc`, gli aeroporti con l'evento.
- **Le righe dei membri** — prenotazione, iscrizione, turno ATC — sono **`ISubmittedByMembers`** e **`IHasStakeholder`**
  con il VID di chi le ha scritte: il membro le crea, le ritira, e non le decide (§3.1, `DeniedToStakeholder`). Hanno la
  maschera dell'evento e la sua area, `IVisible = Members`.
- **Niente `IHasParticipants`**: darebbe al membro `View` sull'area, cioè le prenotazioni di tutti con i VID. Il membro
  legge le sue righe dai **suoi** endpoint, con un DTO suo (come i PIREP in M2 e il training in M3).
- **Niente `IHasAssignee`**: nessuna riga è «affidata» a qualcuno che la conduce.

### 1.2 L'evento — `evt_events`

| Campo | Che cosa |
|---|---|
| `slug` | univoco; l'indirizzo `/events/{slug}` |
| `kind` | una chiave del **vocabolario dei tipi del calendario** (`cms_calendar_kinds`): `event`, `rfe`, `rfo`, `onlineDay`… letta in sola lettura dal nucleo (⚖️ §14 n.3) |
| `organizer` | `Division`, `Network` (HQ), `OtherDivision` |
| `external_url` | la pagina dell'evento di HQ o di un'altra divisione, quando l'organizzatore non è la divisione |
| `title`, `summary` | `Localized<string>`: titolo e riga della scheda |
| `body_json` | la descrizione: un `BlockDocument` (piano §9.3; stesso editor e stesso renderer dei contenuti) |
| `banner_media_id` | un file della media library (`MediaPicker`) |
| `starts_at_utc`, `ends_at_utc` | la finestra dell'evento |
| `visible_from_utc` | l'uscita programmata; vuoto = alla pubblicazione (§2.2) |
| `booking_mode` | `None`, `Registration`, `Slots` (§2.4) |
| `booking_opens_at_utc`, `booking_closes_at_utc` | la finestra delle prenotazioni; chiusura vuota = inizio dell'evento |
| `max_bookings_per_member` | vuoto = l'impostazione del modulo (§1.8) |
| `allows_private_slots` | solo con `Slots` (§2.6) |
| `award_id` | l'award che l'evento segnala a chi ha partecipato (§2.9); facoltativo |
| `status`, `published_at` | `PublishStatus` del nucleo: `Draft`, `Published` |
| `visibility` | `Public` (predefinito) o `Members` |
| `cancelled_at`, `cancelled_by`, `cancellation_note` | l'annullamento (§2.3); la nota è tradotta |
| `row_version` | concorrenza ottimistica |

**Lo stato non si scrive**, come per i tour (design M2 §1.2.1): annunciato, prenotazioni aperte, chiuse, in corso,
concluso si calcolano da `visible_from_utc`, `booking_*` e `starts/ends_at_utc` (§2.1).

### 1.3 Gli aeroporti — `evt_event_airports`

`event_id`, `icao`, `ordinal`. Validati con `IAirportDirectory.FindAsync` al salvataggio (un ICAO che il nucleo non conosce
è un rifiuto). Servono alla direzione degli slot, alla lista pubblica («eventi a LIRF»), al controllo di chi ha volato.
Nessun aeroporto nei semi: un evento senza aeroporti (un online day di FIR) è valido.

### 1.4 Gli slot — `evt_slots`

| Campo | Che cosa |
|---|---|
| `event_id` | |
| `callsign` | |
| `departure_icao`, `arrival_icao` | validati come in §1.3 |
| `off_block_utc`, `arrival_utc` | almeno uno dei due; lo scalo dell'evento dice quale conta |
| `aircraft_icao` | validato con `IAircraftTypeDirectory` |
| `stand` | testo libero, anche vuoto; il Gate Manager lo legge come `gate` |
| `remarks` | testo libero dello staff, pubblico |
| `row_version` | |

Univoco su `(event_id, callsign, off_block_utc, arrival_utc)`. Area `EventBookings`, maschera dell'evento. **La direzione
non si scrive**: arrivo se `arrival_icao` è uno scalo dell'evento, partenza se lo è `departure_icao`, entrambe per un volo
fra due scali dell'evento. Uno slot prenotato non si elimina: lo staff prima toglie la prenotazione (§2.4).

### 1.5 Le prenotazioni — `evt_bookings`

| Campo | Che cosa |
|---|---|
| `event_id`, `slot_id` | `slot_id` vuoto per uno slot privato (§2.6) |
| `booker_vid` | lo stakeholder |
| `ordinal` | 1…`max_bookings_per_member`: la prima, la seconda prenotazione del membro in quell'evento |
| `callsign`, `other_icao`, `is_departure`, `time_utc`, `aircraft_icao` | solo per uno slot privato: il volo del pilota, con l'orario allo scalo dell'evento |
| `state` | `Confirmed`, `Requested` (privato in attesa), `Refused` |
| `decided_by`, `decided_at`, `refusal_reason` | lo staff su uno slot privato o su una prenotazione tolta |
| `flown_at` | la presenza verificata (§2.9) |
| `reminded_at` | il promemoria è partito (§5.4) |

**Due indici univoci fanno il lavoro della sera dell'apertura** (§7.1): `(slot_id)` — uno slot, una prenotazione;
MariaDB accetta più righe con `slot_id` vuoto, cioè i privati — e `(event_id, booker_vid, ordinal)` — il membro non supera
il suo massimo, anche con due richieste nello stesso istante. **Ritirare cancella la riga**: il registro di chi ha fatto
che cosa è l'audit del nucleo (`[Audited]`), e una riga tenuta «annullata» terrebbe occupato l'indice dello slot.

### 1.6 Le iscrizioni — `evt_participants`

Per gli eventi `Registration` (e l'online day): `event_id`, `vid`, `role` (`Pilot`, `Atc`), `attended_at`, `reminded_at`.
Chiave `(event_id, vid)`. Un evento `Slots` non ha iscrizioni: la prenotazione è già la partecipazione.

### 1.7 L'ATC dell'evento — `evt_atc_positions`, `evt_atc_shifts`

- **`evt_atc_positions`**: le postazioni da aprire (`callsign` della postazione, `from_utc`, `to_utc`, predefiniti la
  finestra dell'evento; una nota). Il callsign si sceglie dall'elenco del nucleo (`ref_ivao_atc_positions`,
  `IAtcPositionDirectory`: estensione n.3 se serve una ricerca per testo). Area `EventAtc`.
- **`evt_atc_shifts`**: il turno che un controllore chiede su una postazione: `position_id`, `controller_vid`,
  `from_utc`, `to_utc`, `state` (`Requested`, `Confirmed`, `Refused`), `decided_by`, `decided_at`, `attended_at`,
  `reminded_at`. Area `EventAtc`, stakeholder il controllore. Due turni confermati sulla stessa postazione non si
  sovrappongono (rifiuto dal server).

### 1.8 Le impostazioni — `EventsSettings`

Impostazioni del modulo del nucleo (`ModuleSettingsDescriptor`, riga `modules.events.settings`), schermata generata,
permesso `Events.ManageSettings`. **I predefiniti non conoscono la divisione** (test «XX»).

| Impostazione | Predefinito | Fonte |
|---|---|---|
| `maxBookingsPerMember` — quando l'evento non lo dice | 1 | R.3; ⚖️ §14 n.5 |
| `memberCancelUntil` — `BookingClose` o `EventStart` | `BookingClose` | R.3 |
| `reminderLeadHours` — anticipo del promemoria | 24 | come M3 |
| `attendanceMinimumMinutes` — minuti online nella finestra per dire «c'era» | 30 | Toursystem; ⚖️ §14 n.12 |
| `retentionMonths` — prenotazioni, iscrizioni e turni dopo la fine dell'evento | 24 | come i tour; ⚖️ §14 n.13 |

### 1.9 Dal nucleo, senza scriverlo nel modulo

- **Rating e postazioni**: il vocabolario dei rating e `IAtcPositionDirectory` (M3, A1–A2). Il modulo non scrive numeri di
  rating: un turno chiesto su una postazione sopra il rating del controllore è un **avviso** allo staff, non un blocco
  (R.3), con la domanda al vocabolario «questo rating basta per questo tipo di postazione?» (estensione n.3).
- **Aeroporti e aerei**: `IAirportDirectory`, `IAircraftTypeDirectory`.
- **Il nome di una persona**: dal nucleo, mai copiato; un VID negativo diventa «persona cancellata» con l'helper del nucleo
  (M3, A12a).

---

## 2. Il ciclo di vita

### 2.1 Gli stati, dalle date

| Stato mostrato | Quando |
|---|---|
| **Bozza** | `status = Draft` |
| **Programmato** | pubblicato, prima di `visible_from_utc`: lo vede solo lo staff |
| **Annunciato** | pubblico, prenotazioni non ancora aperte (o `booking_mode = None`) |
| **Prenotazioni aperte** | fra `booking_opens_at_utc` e la chiusura |
| **Prenotazioni chiuse** | dopo la chiusura, prima dell'inizio |
| **In corso** | fra `starts_at_utc` ed `ends_at_utc` |
| **Concluso** | dopo `ends_at_utc` |
| **Annullato** | `cancelled_at` scritto; resta visibile, con la nota |

Nessun job cambia uno stato: il server lo calcola a ogni richiesta con l'orologio del nucleo (§7.2). Solo la
**proiezione** nel calendario e nella ricerca ha bisogno di un giro quando cambia la visibilità (§2.2).

### 2.2 Pubblicare, e l'uscita programmata

- **Pubblica** (`Events.Edit`) controlla: titolo e riassunto in tutte le lingue della divisione, date coerenti
  (`visible_from ≤ booking_opens ≤ booking_closes ≤ starts < ends`), almeno uno scalo per un evento `Slots`, il link per
  un evento di altri. I rifiuti con `Refusals`, campo per campo.
- **L'uscita programmata** (R.1, nota `il-documento-dice-di-se` §2) è la forma che i tour hanno già: la riga è pubblicata,
  e la **data** dice da quando si vede. Alle tre domande della nota: esce **la riga com'è** a quell'ora (è già pubblicata:
  una modifica successiva passa dagli stessi controlli di sempre); il rifiuto non può capitare all'ora X, perché i
  controlli si fanno quando si pubblica; il contrario (sparire a una data) non serve, perché un evento concluso resta
  nell'archivio.
- **La proiezione segue la data** con un job come `TourReleaseJob` (§5.4): la pagina e la lista sono giuste anche senza
  di lui, il calendario e la ricerca si allineano al primo giro. Con i tour e gli eventi sono due moduli sulla stessa forma:
  al terzo si porta nel nucleo (⚖️ §14 n.11).

### 2.3 Annullare, nascondere, eliminare

- **Annulla** (`Events.Edit`): scrive `cancelled_at` e una nota tradotta; le prenotazioni restano, le mail partono a chi
  ha prenotato, si è iscritto o ha un turno (§5.3). Un evento annullato non si riapre (se ne crea un altro).
- **Torna in bozza**: solo senza prenotazioni, iscrizioni o turni.
- **Elimina** (`Events.Delete`, `CrudOptions.DeletePolicy`, come `Tours.Delete`): solo un evento **senza** prenotazioni,
  iscrizioni o turni; altrimenti si annulla. Chi collabora non elimina (§3.3).

### 2.4 Prenotare uno slot

Nella pagina `/events/{slug}`, con il login, mentre le prenotazioni sono aperte:

1. il membro sceglie uno slot libero dalla lista (filtri: arrivi o partenze, orario, aereo, compagnia dal callsign);
2. il server controlla, in quest'ordine: finestra aperta; lo slot esiste ed è di quell'evento; il membro non ha già
   `max_bookings_per_member` prenotazioni; niente sovrapposizione con un'altra sua prenotazione nello stesso evento;
3. **inserisce** la prenotazione `Confirmed`. Se un altro l'ha presa un istante prima, l'indice univoco risponde e il
   membro legge «slot appena preso da un altro pilota» con la lista aggiornata (§7.1);
4. mail `bookingConfirmed` al membro.

**Ritira** (lo stakeholder, fino a `memberCancelUntil`): la riga si cancella, lo slot torna libero. **Lo staff toglie una
prenotazione** (`EventBookings.Edit`) con un motivo, e mail `bookingRemoved`. Nessuna lista d'attesa (R.3): chi vuole uno
slot torna a guardare.

### 2.5 Gli eventi con iscrizione

`Registration`: un pulsante «ci sarò», come pilota o come ATC, fino all'inizio; «non ci sarò più» lo ritira. Nessun
tetto. Serve a contare, al promemoria e alla presenza (§2.9).

### 2.6 Gli slot privati

Con `allows_private_slots`: il membro scrive il suo volo (callsign, l'altro scalo, arrivo o partenza, **l'orario allo
scalo dell'evento**, aereo); la prenotazione nasce **`Requested`** senza slot. Lo staff (`EventBookings.Edit`, mai il
richiedente) la conferma o la rifiuta con un motivo, e mail `privateSlotDecided`. Conta nel massimo del membro. Il Gate
Manager la legge con stand vuoto (`gate` `TBD`, come oggi). ⚖️ §14 n.6.

### 2.7 Gli slot dal file

Centinaia di righe non si scrivono una per una (R.2): lo staff **importa un file CSV** (UTF-8, una riga d'intestazione con
i nomi dei campi dell'esportazione di §4.4), preparato in un foglio di calcolo. L'import:

- valida **tutto o niente**, con i rifiuti per riga (`rows[12].aircraft_icao`) in `Refusals`;
- **aggiunge**, oppure **sostituisce gli slot liberi** (quelli prenotati non si toccano mai);
- un solo endpoint (`POST /api/events/{id}/slots/import`), una sola transazione.

Poi lista e form generati per le correzioni e le azioni di massa (elimina i liberi). Nessuna griglia scritta a mano e
nessuna dipendenza nuova per l'XLSX: un foglio si salva in CSV. ⚖️ §14 n.7.

### 2.8 L'ATC

1. Lo staff (`EventAtc.Edit`) sceglie le postazioni da aprire e le loro finestre.
2. Un membro con rating ATC **chiede un turno** su una postazione (dentro la sua finestra), dalla pagina dell'evento; se il
   rating non basta, lo staff lo vede in evidenza (§1.9).
3. Lo staff conferma o rifiuta (mai il proprio turno); mail `atcShiftDecided`. Il controllore **prenota anche su IVAO**:
   la pagina glielo ricorda con il link, perché la prenotazione di IVAO resta sua (§0.2).
4. **La copertura**: per ogni postazione, i turni confermati e — dall'API di IVAO, alla richiesta — le prenotazioni su
   IVAO di quel giorno (§6.1). Il blocco `events.atcCoverage` mostra allo staff gli eventi dei prossimi giorni con
   postazioni scoperte (R.1).

⚖️ §14 n.8.

### 2.9 Dopo l'evento: le presenze

Un job (§5.4), dopo `ends_at_utc`, scrive le presenze:

- **Piloti**: per ogni prenotazione `Confirmed`, `IIvaoApiClient.SearchSessionsAsync(vid, finestra, partenza, arrivo)` —
  la stessa chiamata dei controlli dei PIREP (M2) — dice se il pilota ha volato quella tratta nella finestra; `flown_at`.
  Per un evento `Registration` basta una sessione con partenza o arrivo su uno scalo dell'evento.
- **Controllori**: `IAtcActivitySource` (le sessioni di `v_share_atc_sessions`) dice se il controllore era online sulla
  postazione del turno per almeno `attendanceMinimumMinutes`; `attended_at`. Oggi la sorgente non espone il VID:
  estensione n.4.
- **L'award**: una prenotazione, un'iscrizione o un turno con la presenza proiettano un `AwardSignalProjection` con
  l'`award_id` dell'evento, nella stessa transazione, come l'iscrizione completata di M2. Chi ha `Awards.Assign` decide.

Sono dati storici, non «adesso»: il job li legge quando gira, anche giorni dopo (§7.2). ⚖️ §14 n.12: è l'ultima fase e si
può togliere senza toccare il resto.

---

## 3. Permessi

### 3.1 Il catalogo

Tre aree, perché le capacità che si delegano a un altro dipartimento sono **righe loro** (R.1): gli slot al FOD, l'ATC
all'AOD.

| Permesso | Che cosa permette | Negato all'interessato |
|---|---|---|
| `Events.View` | vedere nel back office eventi, bozze comprese | no |
| `Events.Edit` | creare, modificare, pubblicare, annullare un evento; i suoi aeroporti | no |
| `Events.Delete` | eliminare un evento senza prenotazioni (`DeletePolicy`) | no |
| `Events.ManageSettings` | impostazioni del modulo | no |
| `EventBookings.View` | slot, prenotazioni e iscrizioni con i VID; l'esportazione per il Gate Manager | no |
| `EventBookings.Edit` | slot e import; togliere una prenotazione; decidere uno slot privato | **sì** |
| `EventAtc.View` | postazioni e turni con i VID | no |
| `EventAtc.Edit` | postazioni; confermare o rifiutare un turno | **sì** |

Ogni area ha il suo `View` e il suo `Edit` (regola del catalogo). ⚠️ Il guardiano chiede `{Area}.Edit` per ogni riga
dello staff (trappola di A5, `HANDOFF-M3.md`): per questo non c'è un `EventBookings.ManageSlots` a parte.

### 3.2 Chi li ha (`division.json → positionGrants`)

| | EC | EAC | EA1–9 (advisor) | AOD, coordinator e assistant | FOD, coordinator e assistant |
|---|---|---|---|---|---|
| `Events.View` | ✓ | ✓ | ✓ | ✓ (`scope: AOD`) | ✓ (`scope: FOD`) |
| `Events.Edit` | ✓ | ✓ | ✓ | | |
| `Events.Delete` | ✓ | ✓ | | | |
| `Events.ManageSettings` | ✓ | ✓ | | | |
| `EventBookings.View`, `.Edit` | ✓ | ✓ | ✓ | | ✓ (`scope: FOD`) |
| `EventAtc.View`, `.Edit` | ✓ | ✓ | ✓ | ✓ (`scope: AOD`) | |

`scope: ED` dove non è scritto altro. HQ (DIR, ADIR) e il web (WM, AWM) tutto per il nucleo; il superadmin tutto. Le righe
di AOD e FOD valgono **solo sugli eventi che hanno quel dipartimento nel «a cura di»**: un RFE «ED + AOD + FOD» lo
modificano tutti e tre, ognuno nella sua parte; un evento solo ED no. Qualcuno fuori da queste posizioni riceve un grant a
un VID, anche **su un evento solo** (`resource_scope`, R.1). ⚖️ §14 n.9 e n.10. Scritte in `config/division.json` e in
`division.example.json` nella fase dello scheletro, come M2 e M3.

### 3.3 La collaborazione e la cancellazione

Il piano chiede che chi collabora non cancelli, e la nota `ordine-dei-moduli` §3.3 teme che serva «una sfumatura» nell'unico
handler, che concede un permesso tenuto su **uno qualsiasi** dei dipartimenti della riga. **Non serve**: `Events.Delete`
lo tengono solo EC ed EAC, con `scope: ED`, e l'ED è in ogni evento. Un dipartimento che collabora non ha `Events.Delete`
su nessun dipartimento, quindi l'handler non glielo concede su nessuna riga. È lo stesso modo in cui `Tours.Delete` sta
solo a coordinator e assistant del FOD, già nel codice (`TourEndpoints`, `DeletePolicy`).

Il limite, detto: se un giorno qualcuno dà `Events.Delete` a un altro dipartimento, quello elimina anche gli eventi che
cura insieme all'ED. È una scelta di configurazione, visibile nel pannello dei permessi, non un buco: il nucleo non la
vieta, come non la vieta per i tour. ⚖️ §14 n.9.

⚠️ **Da verificare nella fase dello scheletro**: che una lista dell'area `EventAtc` o `EventBookings` legga l'evento padre
per chi ha solo quell'area (per l'AOD e il FOD `Events.View` c'è apposta, nella tabella sopra).

---

## 4. Schermate e blocchi

### 4.1 Pubblico e membri

- **`/events`** (pubblico): i prossimi eventi come schede, con filtri per tipo e per scalo, e il `CalendarView` del nucleo
  (vista mese e agenda) sugli stessi eventi; l'archivio dei conclusi sotto. Lo spazio che il piano chiama «Calendario +
  lista eventi» (§8.2).
- **`/events/{slug}`** (pubblico): banner, titolo, date in UTC e nell'ora della divisione, tipo, organizzatore (con il
  link, se è di altri), scali, descrizione (`BlockDocument` con il renderer del nucleo). Poi, secondo il modo:
  - **slot**: la lista con libero o preso, e **«Prenota»** per chi ha fatto il login; un visitatore vede solo libero o
    preso, **mai chi** (piano §9.7; la regola di M3 §12 n.4); a chi ha fatto il login il nome e il link al profilo IVAO;
  - **iscrizione**: «ci sarò», e quanti;
  - **ATC**: postazioni, turni confermati, «chiedi un turno», la copertura su IVAO del giorno (§2.8);
  - **il giorno dell'evento**: la striscia `LiveStatusStrip` con chi è online sugli scali dell'evento (§6.1).
- **`/events/mine`** (membri): le mie prenotazioni, iscrizioni e turni, con «ritira» e lo stato di un privato.

### 4.2 Staff

- **`/staff/events`**: lista generata con i filtri **Bozze**, **Prossimi**, **In corso**, **Conclusi**, **Annullati**.
- **`/staff/events/{id}`**: il form generato dell'evento (campi tradotti con `LocaleFields`, descrizione con l'editor dei
  blocchi, banner con `MediaPicker`), come l'editor del tour (design M2 §8.3); poi le schede:
  - **Slot**: lista generata, import (§2.7), «elimina i liberi»;
  - **Prenotazioni**: lista generata con i privati da decidere in evidenza, «togli»;
  - **Iscrizioni**: lista generata;
  - **ATC**: postazioni e turni, liste generate con «conferma» e «rifiuta».
- **`/staff/events/settings`**: impostazioni generate.

**Nessun componente nuovo** nell'elenco chiuso: `DataList`, `SchemaForm`, `CalendarView`, `LiveStatusStrip`, `MediaPicker`,
`StatusBadge`, `ConfirmDialog` bastano. L'`EventTimeline` del piano §8.3 non serve a M4 (i turni si leggono in lista per
postazione). **Nessuna eccezione dichiarata** al motore CRUD (§16.6); gli endpoint a mano sono i verbi — prenotare,
ritirare, iscriversi, chiedere un turno, decidere, annullare, importare, esportare, la copertura, il «chi è online» — e il
rapporto di chiusura li conta per famiglia.

### 4.3 Blocchi Data

| Blocco | Dove | Che cosa |
|---|---|---|
| `events.eventList` | home, `/events`, pagine | i prossimi eventi, filtrabili per tipo; il blocco che il design M1 aspetta |
| `events.myEvents` | `/me` | le mie prossime prenotazioni, iscrizioni e turni; «Nessuna prenotazione — vai agli eventi» |
| `events.atcCoverage` | `/staff`, dashboard ED e AOD | gli eventi dei prossimi sette giorni con postazioni senza turno confermato |
| `events.bookingQueue` | dashboard ED | privati e turni da decidere |

A un visitatore i blocchi personali rispondono `signedIn: false`, come `myTours`. ⚠️ Ogni blocco ha le sue due metà
(TypeScript e C#) nella stessa PR e alza i due conteggi dei test (`uiKit.test.ts`, `DataBlockEndToEndTests`).

### 4.4 Il Gate Manager e gli altri programmi

L'unico programma che oggi legge `ivao-booking` è il Gate Manager. Nell'hub lo legge con un **token personale** (`CLAUDE.md`
§2), non con una chiave condivisa:

- **audience `events.bookings`**, dichiarata in `IModule.TokenAudiences`; il token lo crea da `/me/tokens` chi ha
  `EventBookings.View` e fa girare il Gate Manager, e i permessi si ricostruiscono a ogni richiesta;
- **`GET /api/events/{slug}/bookings/export`**: un array con i campi che il Gate Manager già legge (`callsign`,
  `booked_by`, `aircraft_icao`, `gate`, `eobt`, `eat`, `origin_icao`, `destination_icao`, orari UTC) **più l'id dello
  slot**, l'identità stabile che il Gate Manager chiede (`SPEC.md` §14); i privati confermati con `gate` vuoto. Il Gate
  Manager cambia indirizzo e intestazione, non il parsing;
- la risposta dice un errore con uno stato HTTP di errore (non un 200, come oggi).

Il cambio nel Gate Manager è un lavoro del suo repository, e le prove si fanno su `prova-ponte-rfo`.

---

## 5. Calendario, ricerca, award, notifiche, job

### 5.1 Calendario e ricerca

- **Una voce per evento**: tipo = il `kind` dell'evento (`rfe`, `onlineDay`…, colore dal vocabolario), visibilità quella
  dell'evento, indirizzo `/events/{slug}`, titolo tradotto. Una bozza, un evento non ancora visibile o annullato non
  proiettano (l'annullato resta nella pagina, non nel calendario).
- **Il tipo nel vocabolario**: il seme dei tipi (`seed/calendar-kinds/kinds.json`) riceve `rfe`, `rfo` e `onlineDay`
  accanto a `event` (estensione n.2); `TrainingSettings.ConflictKinds` di M3 può allora aggiungere `onlineDay`, come dice il
  suo commento.
- **Ricerca**: sì, per gli eventi pubblici visibili: titolo, riassunto, testo della descrizione (il camminatore generico
  del nucleo), indirizzo.
- **Usi dei file**: il banner e le immagini della descrizione fino a `ends_at_utc` + 1 mese, come i tour
  (`MediaUseProjection`).

### 5.2 Award

`AwardSignalProjection` da prenotazioni, iscrizioni e turni con la presenza (§2.9). Nessuna assegnazione automatica (§9.7).
Un award per evento; le serie (piloti e controllori di più eventi) fuori da M4 (§0.2).

### 5.3 Notifiche (`events.*`)

| Tipo | A chi | Quando |
|---|---|---|
| `bookingConfirmed` | membro | slot prenotato |
| `bookingRemoved` | membro | lo staff ha tolto la prenotazione, con il motivo |
| `privateSlotDecided` | membro | slot privato confermato o rifiutato |
| `atcShiftDecided` | controllore | turno confermato o rifiutato |
| `eventChanged` | chi ha prenotato, iscritto, turno | orari dell'evento cambiati dopo la prima prenotazione |
| `eventCancelled` | idem | evento annullato, con la nota |
| `reminder` | idem | `reminderLeadHours` prima dell'inizio (del turno, per un controllore) |

Modelli in `web/src/modules/events/locales/{it,en}/events.json`, copiati da `pnpm i18n:sync`; ogni membro le spegne dal
profilo. Nessuna mail di massa per un evento nuovo (§0.2).

### 5.4 Job del modulo

- **`events-release`**, ogni 15 minuti: riproietta gli eventi la cui visibilità è cambiata dall'ultimo giro riuscito
  (`ProjectionRefresh`, come `TourReleaseJob`, §2.2).
- **`events-reminders`**, ogni 15 minuti: le righe che iniziano entro `reminderLeadHours` e non hanno `reminded_at`.
- **`events-attendance`**, ogni notte: gli eventi conclusi senza presenze calcolate (§2.9), a lotti.
- **`events-retention`**, il 1° del mese: §8.

Convenzioni di M2: `[DisallowConcurrentExecution]`, una riga in `hub_jobs_log`, mai un'eccezione, `RunAsync` per i test.
**Ognuno decide che cosa fare dal proprio registro**, non dall'ora in cui gira: sono tutti job che recuperano (§7.2).

---

## 6. Che cosa legge da IVAO e da vIPI

### 6.1 Le prenotazioni ATC di IVAO — misurate nella documentazione

La documentazione pubblica (`https://api.ivao.aero/docs/atc-json`, letta il 28 settembre 2026) ha:

- `GET /v2/atc/bookings/daily?date=&position=` → un array di prenotazioni di quel giorno;
- `GET /v2/atc/bookings?date=&page=&perPage=` (massimo 100 per pagina), `GET /v2/atc/bookings/{id}`;
- `POST`, `PUT`, `DELETE` con lo scope `bookings:write`;
- una prenotazione: `id`, `user` (`id`, `divisionId`, `firstName`, `lastName`), `atcPosition`, `subcenter`,
  `startDate`, `endDate`, `voice`, `training` (`training`, `exam` o vuoto), `createdAt`.

Le letture dichiarano `oauth2` o `consumer`: **da misurare** nella fase del nucleo se il token dell'applicazione
(`client_credentials`, quello della sincronizzazione) basta, e con che cosa risponde. Si legge **alla richiesta** della
pagina o del blocco, con la cache del client (qualche minuto), mai con un job (§7.2). Nome e VID di chi ha prenotato solo
a chi ha fatto il login (come in §4.1). Estensione n.1.

Il **«chi è online adesso»** della pagina dell'evento è `GetNetworkStatusAsync` (whazzup, 60 secondi di cache), già nel
client: letto alla richiesta, mai campionato.

### 6.2 Gli eventi di IVAO

Le quattro API pubbliche documentate (`core`, `data`, `tracker`, `atc`) **non hanno endpoint di eventi**. Un evento di HQ o
di un'altra divisione si scrive a mano, con `organizer` e `external_url` (§1.2). Se IVAO un giorno li esporrà, si
aggiungerà una lettura al client del nucleo, non al modulo.

### 6.3 vIPI

Solo attraverso `IAtcActivitySource` e la vista `v_share_atc_sessions` (piano 1.18): chi era online su quale postazione, per
le presenze (§2.9). Il modulo non nomina vIPI; con `atcData.source: none` le presenze ATC restano da scrivere a mano (lo
staff spunta `attended_at`). La vista espone già `vid`; `AtcPresence` no: estensione n.4.

---

## 7. Hosting

### 7.1 La sera dell'apertura

L'apertura delle prenotazioni di un RFE porta molti membri nello stesso minuto, su un processo che forse dorme
(risveglio misurato 8,4 s, ~4,9 s stimati con i tagli della nota `2026-09-28-l-avvio-a-freddo`), a volte con due processi
insieme (nota della #165), e un pool di ≤ 15 connessioni.

- **L'apertura non è un evento del server**: è una data. La prima richiesta dopo `booking_opens_at_utc` trova le
  prenotazioni aperte; nessun job deve «aprirle» (e non girerebbe, se il processo dorme).
- **L'unicità sta nel database** (§1.5): due processi, due richieste nello stesso istante, un solo vincitore. Nessun lock
  in memoria, nessuna cache della disponibilità che possa mentire.
- **Una prenotazione è una transazione breve** (un insert); la lista degli slot è una query sola, senza N+1, anche con 441
  righe.
- La pagina mostra il **conto alla rovescia** all'apertura e, a chi arriva prima, «le prenotazioni aprono alle…»: non c'è
  una coda. Il limite delle richieste è per indirizzo (le note dei proxy di settembre): una prenotazione per volta non lo
  tocca.

### 7.2 I job e i dati «adesso»

La regola decisa (nota `2026-09-28-i-job-quando-passenger-spegne-l-hub` §8) si applica così:

- **niente si campiona ogni minuto**: il «chi è online» e le prenotazioni ATC di IVAO si leggono alla richiesta (§6.1);
- **niente a ore fisse**: nessun dato degli eventi esiste solo «adesso»;
- **tutto il resto recupera**: la visibilità e lo stato si calcolano dalle date, i promemoria da `reminded_at`, le
  presenze da dati storici (tracker e sessioni di vIPI), la conservazione dal suo registro.

Le mail partono al ritmo delle chiamate del POST pianificato: con due chiamate l'ora, un promemoria di 24 ore arriva in
tempo.

### 7.3 La manutenzione

Con `maintenance` del modulo (§9.7 del piano) le pagine restano leggibili e i verbi — prenotare, ritirare, iscriversi,
chiedere un turno — rispondono 503: è l'esempio del piano («il booking ha un problema, spegnilo»).

---

## 8. Conservazione e dati personali

- **Che cosa è personale**: il VID di chi prenota, si iscrive, controlla; i dati di un volo privato; i motivi di un
  rifiuto. Nome ed email si leggono dal nucleo.
- **Che cosa resta**: **l'evento resta** nell'archivio pubblico. Prenotazioni, iscrizioni e turni di un evento concluso
  restano `retentionMonths` (24) per le statistiche e gli award, poi `events-retention` li **cancella**; gli slot restano
  (sono dello staff, senza persone). ⚖️ §14 n.13.

### 8.1 La cancellazione dei dati di una persona

Il meccanismo è del nucleo (T20b): le colonne `booker_vid`, `controller_vid`, `vid`, `decided_by`, `cancelled_by` seguono la
convenzione e il nucleo ci scrive lo pseudonimo. **`EventsPersonalData : IPersonalDataEraser`** fa quello che è **sulla**
persona:

- le prenotazioni, iscrizioni e turni di **eventi non ancora conclusi** si **cancellano** (lo slot torna libero);
- quelli di eventi conclusi **restano con lo pseudonimo**, senza i dati del volo privato e senza motivi;
- quello che ha fatto **come staff** (decisioni, annullamenti) resta con lo pseudonimo.

«Persona cancellata» dove il modulo mostra un VID, con l'helper del nucleo di A12a. ⚠️ `ErasureTests` elenca le colonne di
persona e non vede da solo i contesti dei moduli (`HANDOFF-M3.md`): va allargato nella fase che crea le tabelle.

---

## 9. Forkabilità

- **Nessun ICAO, nessuna postazione, nessun nome della divisione** nel codice, nei semi e nei predefiniti; test «XX».
- **Il modulo non nomina IVAO** (piano §4.2): le prenotazioni ATC e il «chi è online» passano dal client e da una
  interfaccia del nucleo (estensione n.1), le presenze da `IAtcActivitySource` e dal client; «RFE» e «RFO» sono **chiavi di
  un vocabolario** della divisione, non costanti nel codice, e un fork le rinomina o le toglie.
- **I dipartimenti delle collaborazioni** (AOD, FOD) sono **configurazione** (`positionGrants`), non codice.
- **Il Gate Manager** è un programma esterno come un altro: l'hub gli dà un token e un formato, non sa chi è.

---

## 10. Che cosa chiede al nucleo

Ognuna è una PR a sé, **prima** del codice del modulo che la usa, con la sua nota (`CLAUDE.md` §0, regola 6).

| # | Estensione | Nota | Dove |
|---|---|---|---|
| 1 | **Le prenotazioni ATC della rete** di un giorno: una lettura nel client (`/v2/atc/bookings/daily`, forma e token da misurare, fixture registrate) dietro un'interfaccia del nucleo, come `IAtcActivitySource`, così il modulo non nomina IVAO | sì, breve | §6.1 |
| 2 | **I tipi `rfe`, `rfo`, `onlineDay`** nel seme dei tipi del calendario (il seme non tocca una chiave già scritta a mano) | no (come `exam` in M3) | §5.1 |
| 3 | **Postazioni e rating**: cercare una postazione per testo in `IAtcPositionDirectory`, e chiedere al vocabolario se un rating basta per un tipo di postazione — **solo se** quello che c'è non risponde già; si misura nella fase del nucleo | breve, se serve | §1.7, §1.9 |
| 4 | **Il VID in `AtcPresence`**: la vista lo espone già; la sorgente del nucleo lo passa, per le presenze ATC | breve | §2.9, §6.3 |
| 5 | ~~Cancellare solo con il permesso sul dipartimento di base~~ **non serve**: `Events.Delete` solo alle posizioni dell'ED (§3.3) | — | §3.3 |
| 6 | **Un intervallo di VID e un prefisso di slug per i test** di Events in `CONTRIBUTING.md` (per esempio `761001–761099`, `evt-test-`, dopo un grep) | no; lo scrive il master | §12 |

Il resto c'è già: `IProjectable` con più voci, usi dei file, award e contatti; `TokenAudiences`; `ModuleSettings`;
`DeletePolicy`; `BeforeAuthorize`; `Refusals`; `ISubmittedByMembers` e `IHasStakeholder`; l'helper «persona cancellata»
(A12a di M3, da aspettare se non è ancora unito).

---

## 11. Tabelle e migrazioni

**Nucleo** (additive): nessuna tabella; i tipi del seme (n.2).

**Modulo**: `evt_events`, `evt_event_airports`, `evt_slots`, `evt_bookings`, `evt_participants`, `evt_atc_positions`,
`evt_atc_shifts`. La migrazione `Initial` nasce nella fase dello scheletro con le tabelle di quella fase e non si tocca più;
le altre arrivano con la loro fase, sempre additive.

---

## 12. La rete di test

- **Unità**: lo stato dalle date (ogni confine, fuso della divisione); la direzione di uno slot dagli scali; l'import
  (tutto o niente, i rifiuti per riga, «sostituisci i liberi» che non tocca i prenotati); la sovrapposizione dei turni; la
  presenza da sessioni finte (minuti, finestra, postazione).
- **Integrazione** (MariaDB vera, database condiviso, VID e slug riservati dell'estensione n.6):
  - **due prenotazioni dello stesso slot nello stesso istante**: una vince, l'altra riceve il rifiuto giusto; lo stesso
    per il massimo del membro;
  - nessuno decide il proprio slot privato o il proprio turno (superadmin compreso);
  - il FOD con `EventBookings.Edit` scrive gli slot di un evento «ED + FOD» e non quelli di un evento solo ED, e non
    tocca postazioni né testo; lo stesso per l'AOD con l'ATC;
  - chi collabora non elimina; EC ed EAC sì, solo senza prenotazioni;
  - un grant su un evento solo vale su quell'evento e sui suoi slot, non su un altro;
  - il membro legge le sue righe e non quelle degli altri; un visitatore non vede chi ha prenotato;
  - l'esportazione con un token `events.bookings` e senza; con un token di un'altra audience, 403;
  - il promemoria parte una volta; la conservazione cancella dopo `retentionMonths`;
  - la cancellazione di una persona: evento futuro liberato, evento concluso con lo pseudonimo.
- ⚠️ **L'ED nei test** (`CONTRIBUTING.md`): i test dei contatti affermano i destinatari esatti dell'ED, quindi **nessun EC,
  EAC o EA seminato con un'email** nei test del modulo; i permessi si danno con grant a un VID.
- **Architettura**: il modulo non nomina IVAO né vIPI, non scrive ICAO, nessuna chiamata HTTP.
- **Divisione XX**: nessuna stringa italiana, nessun ICAO, nessun tipo di evento nei predefiniti.
- **Smoke e giro completo** (`pnpm e2e:full`): lo staff crea un evento a slot, importa, pubblica; un membro prenota e
  ritira; un controllore chiede un turno e lo staff lo conferma. Le specifiche si riprendono le righe (nota del banco).

---

## 13. Ordine di lavoro proposto

Le fasi vere si scrivono in `10-piano-implementazione-m4.md` dopo l'approvazione; qui la forma e una misura grossolana
(**P** piccola, **M** media, **G** grande, rispetto alle fasi di M3). Una fase, un branch `m4/e<N>-<slug>`, una PR; le
fasi del modulo migrano lo stesso contesto e vanno in fila, quelle del nucleo possono correre in parallelo.

| Fase | Contenuto | Misura |
|---|---|---|
| E0 | Le note di decisione delle scelte di §14 e delle estensioni; `10-piano-implementazione-m4.md` | P |
| E1 | Nucleo: prenotazioni ATC della rete, tipi del calendario, VID delle presenze, postazioni e rating se serve (n.1–n.4) | M |
| E2 | Modulo: scheletro, `Initial` (eventi e aeroporti), catalogo, `positionGrants`, impostazioni, menu, segmento riservato | M |
| E3 | L'evento nello staff: form, descrizione, banner, pubblicazione, uscita programmata, annullamento, eliminazione; calendario, ricerca, usi dei file; `events-release` | G |
| E4 | Il pubblico: `/events`, `/events/{slug}` senza prenotazioni, `events.eventList` | M |
| E5 | Gli slot: tabella, import, liste; l'esportazione con il token `events.bookings` | M |
| E6 | Prenotare: verbi, vincoli, `/events/mine`, `events.myEvents`, mail, promemoria | G |
| E7 | Iscrizioni e slot privati; `events.bookingQueue` | M |
| E8 | L'ATC: postazioni, turni, copertura su IVAO, `events.atcCoverage`, «chi è online» | G |
| E9 | Dopo l'evento: presenze, award, conservazione, `EventsPersonalData`; giro completo | M |
| E10 | Fuori dal repository: il Gate Manager legge l'hub (prove su `prova-ponte-rfo`); `ivao-booking` in sola lettura, poi il 301 (Carmine) | P |

E9 sta in fondo di proposito: senza, il modulo sostituisce `ivao-booking` lo stesso (⚖️ §14 n.12).

---

## 14. Domande per Carmine

Ognuna con la raccomandazione; la risposta entra qui con il link al commento, e poi nella nota della fase E0.

1. **Chi dell'ED risponde ai fatti** di §R.3? Raccomandato: **un nome dello staff ED**, a cui la sessione fa le domande
   come `dalberone` per il TD; fino alle risposte, i punti segnati **(R.3)** restano ipotesi.
2. **Gli eventi di HQ e degli altri** (§6.2): l'API non li ha. Raccomandato: **a mano**, con organizzatore e link, solo
   quelli che toccano la divisione.
3. **Il tipo dell'evento come chiave del vocabolario del calendario** (§1.2, n.2), con `rfe`, `rfo`, `onlineDay` nel
   seme. Raccomandato: **sì**; il modo di prenotazione resta un campo a parte, perché un tipo non decide come si prenota.
4. **Tre modi di prenotazione** — nessuno, iscrizione, slot — **uno per evento**. Raccomandato: **sì**; SES è `Slots`.
5. **Quante prenotazioni per membro** (§1.5): raccomandato **1 di predefinito, cambiabile per evento** (2 per chi vuole
   arrivo e ripartenza), tenuto dall'indice del database; nessuna lista d'attesa.
6. **Gli slot privati** (§2.6): raccomandato **sì, per evento, con la conferma dello staff**, senza tetto per ora in M4.
7. **Gli slot da un file CSV** (§2.7), senza griglia a mano e senza XLSX. Raccomandato: **sì**.
8. **L'ATC** (§2.8): postazioni scelte dallo staff, turni chiesti dai controllori e confermati dallo staff, prenotazioni di
   IVAO **lette** e non scritte. Raccomandato: **sì**; scrivere su IVAO chiederebbe il token del controllore con
   `bookings:write`, e resta fuori.
9. **La collaborazione** (§3.2, §3.3): tre aree (`Events`, `EventBookings`, `EventAtc`); AOD e FOD con i loro grant
   predefiniti; `Events.Delete` solo a EC ed EAC, senza toccare l'handler. Raccomandato: **sì**. In alternativa, AOD e FOD
   senza grant predefiniti, e un grant a un VID quando serve.
10. **Gli advisor dell'ED** (EA1–9): raccomandato **tutto tranne eliminare e le impostazioni**, sulla linea degli advisor
    del FOD nei tour, che modificano e validano ma non eliminano e non toccano le impostazioni.
11. **L'uscita programmata** (§2.2) con la forma dei tour (la data sulla riga, un job che riproietta). Raccomandato:
    **sì**, e portarla nel nucleo quando un terzo modulo ne ha bisogno, non ora.
12. **Presenze e award** (§2.9): raccomandato **sì, nell'ultima fase (E9)**, piloti dal tracker e controllori dalle
    sessioni di vIPI; si può togliere senza toccare il resto.
13. **La conservazione** (§8): raccomandato **24 mesi** dopo l'evento per prenotazioni, iscrizioni e turni, poi
    cancellati; l'evento resta.
14. **Mail di massa per un evento nuovo** (§0.2): raccomandato **no**.
15. **Lo spegnimento di `ivao-booking`** (E10): raccomandato **dopo il primo evento vero fatto sull'hub**, con il Gate
    Manager già passato all'hub; il periodo di sola lettura lo concorda Carmine con l'ED.
16. **L'Online Day** (R.1): raccomandato **un tipo di evento** (`onlineDay`) con iscrizione e postazioni ATC, e il tipo
    aggiunto a `conflictKinds` di Training quando esiste.

---

## 15. Da portare nel piano

Lo scrive il master dopo il merge (`CLAUDE.md` §0); le note della fase E0 lo ripetono per le loro decisioni.

- **§7, schizzo `evt_`**: le sette tabelle di §11 al posto dello schizzo; niente FK verso `ref_`; il tipo dal vocabolario
  del calendario; niente training ed esami; la prenotazione come riga a sé (§0.6).
- **§8.2, sitemap**: `/events/mine` e il blocco `events.myEvents` in `/me`, al posto di `/me/bookings`.
- **§9.2, riga Events**: i tre modi di prenotazione; gli eventi di HQ a mano (l'API non li ha, 28 settembre 2026); il Gate
  Manager legge l'hub con un token personale.
- **§9.2, riga 1, «Nessuna migrazione dello storico»** e **§12 punto 3**: il periodo di sola lettura e il 301 dopo il
  primo evento sull'hub (§14 n.15).
- **§9.7, «Collaborazioni tra moduli»**, riga ATC↔Events: le postazioni si scrivono sull'evento, da `ref_ivao_atc_positions`.
- **§10, tabella dell'API**: `/v2/atc/bookings/daily`, letto alla richiesta, niente job ogni 5 minuti.
- **§13, riga M4**: «chi collabora non cancella» precisato (§3.3): `Events.Delete` solo al dipartimento di base, per
  configurazione; le fasi sono E0–E10 di `10-piano-implementazione-m4.md`.
- **Nota `il-documento-dice-di-se` §2**: l'uscita programmata di un evento ha la forma dei tour; il nucleo la prende al
  terzo modulo (§14 n.11).
- **`CONTRIBUTING.md`**: l'intervallo di VID e il prefisso di slug dei test di Events (n.6).
