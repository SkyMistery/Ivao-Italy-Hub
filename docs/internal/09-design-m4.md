# IVAO Division Hub — Design di M4 (il modulo Events)

> Documento **interno** (italiano). Fonte di verità: `00-piano-di-progettazione.md` (versione 1.23).
> Ingresso: quello che il piano e le note hanno già deciso sugli eventi (§R.1), quello che dicono gli strumenti di oggi
> (§R.2), la documentazione pubblica dell'API di IVAO (§9) e **i requisiti dell'ED dati da Carmine** in quattro giri di
> risposte il 29 settembre 2026 (§R.3). Le scelte sono segnate **⚖️**; in §17 ci sono tutte, con la raccomandazione e la
> risposta di Carmine. Le fasi si scrivono in `10-piano-implementazione-m4.md` **dopo**
> l'approvazione di questo documento. I modelli sono `05-design-m2.md` e `07-design-m3.md`.

**Stato:** **deciso in chat da Carmine il 29 settembre 2026, da confermare sulla PR**. Nessun codice. La prima stesura
(28 settembre) faceva ipotesi sul lavoro dell'ED; questa le sostituisce con le risposte di Carmine (§R.3), e Carmine ha
risposto in chat anche alle sette domande che restavano (§17.2). **Le risposte date in chat vanno confermate da Carmine con
un commento sulla PR**, perché la decisione porti il suo link (`CLAUDE.md` §5). Il prossimo passo è la fase E0 (§16).

---

## 0. Perimetro

### 0.1 Che cosa è «fatto»

M4 si fa in **tre blocchi** (Carmine, §17.1 n.8 e §17.2 n.6), con un calendario solo:

**M4a — gli eventi e le prenotazioni.** Basta a **spegnere `ivao-booking`** (piano §13, riga M4):

- lo staff crea un evento — RFE, RFO, MSE, online day, un evento libero, un evento di HQ in collaborazione — con titolo e
  descrizione tradotti, banner, aeroporti, orari; lo **pubblica**, anche **programmandone l'uscita** (§2.2); l'evento
  entra nel **calendario unico** e nella ricerca finché non finisce, poi **sparisce dal pubblico** e resta allo staff
  (§2.4);
- lo staff carica gli **slot pubblici** e le loro **rotazioni** da un CSV o da una tabella incollata (§3.1); il FOD
  scrive le **rotte** (§1.4); dagli slot pubblici e dalla **capacità** di ogni aeroporto il sistema **genera gli slot
  privati** nei buchi (§3.2);
- i piloti **prenotano quanti slot vogliono**, se compatibili fra loro, fino all'EOBT, anche a evento in corso (§3.3–§3.6);
- il **Gate Manager** della divisione legge le prenotazioni dall'hub invece che da `booking.it.ivao.aero` (§7.4).

**M4b — l'ATC e il dopo evento:**

- le **postazioni ATC** le decidono AOD e staff dei FIR; i controllori danno la **disponibilità**; il sistema **propone il
  roster** secondo rating, esperienza e affidabilità, lo staff lo corregge, e il roster **si pubblica da solo** x giorni
  prima, con le mail; dopo si modifica ancora, e ogni modifica avvisa (§4);
- dopo l'evento: le **statistiche** (chi ha prenotato e volato, chi no, chi ha volato senza prenotare, chi si era dato
  disponibile e ha controllato, chi no), i **no-show** proposti dal sistema e confermati dallo staff, il **registro di
  affidabilità** di ogni controllore (§5.1, §4.5);
- il **PIREP di supporto**, pilota o ATC — scritto a mano o creato dal sistema per chi l'ha chiesto — **verificato dal
  sistema** e **validato** da chi ne ha il permesso (oggi l'MD); le **regole di award dell'evento** decidono quali award
  segnalare, e l'award lo assegna una persona (§5.2–§5.4).

**M4c — gli eventi in presenza.** Un evento che non ha un roster può chiedere altro (§17.2 n.6): chi viene di persona
**si iscrive** rispondendo alle **domande dell'evento** — che materiale porta (PC, monitor…), se viene alle cene — e
**prenota le attività parallele** a posti limitati, come un turno a un simulatore di volo (§4-bis).

### 0.2 Fuori perimetro

- **Lo storico di `ivao-booking`**: nessun import (piano §12 punto 3, `CLAUDE.md` §7).
- **L'assegnazione degli stand**, anche semi-automatica: servono i dati degli stand di ogni aeroporto, che oggi stanno solo
  nel Gate Manager. Carmine la vuole vedere in dettaglio dopo; fino ad allora lo stand di uno slot pubblico è un campo
  scritto dallo staff, quello di uno slot privato resta vuoto (§3.2).
- **Le rotazioni, lo stato condiviso fra le postazioni la sera dell'evento**: del **Gate Manager** e del suo ponte su
  `atc.it.ivao.aero`. L'hub gli dà le prenotazioni (§7.4) e nient'altro.
- **Scrivere la prenotazione della postazione su IVAO** per il controllore, dal roster: Carmine la vorrebbe (§17.2 n.3), ma
  chiede il token IVAO del controllore con `bookings:write`, cioè il consenso a quello scope al login e un token conservato
  dall'hub per conto del membro. È un **meccanismo nuovo** (`CLAUDE.md` §5, caso c): una nota sua, dopo M4, con la
  sicurezza del token davanti. In M4 l'hub **legge** le prenotazioni di IVAO accanto al roster.
- **Gli eventi di HQ che non coinvolgono la divisione**, e qualunque evento letto da un'API: l'API pubblica non ne ha (§9.2).
- **Mail a tutti i membri per un evento nuovo**: no (§17.2 n.5); il **feed iCal** (M6, piano §15.9).

### 0.3 Che cosa M4 non rimette in discussione

- **I meccanismi del nucleo** (`CLAUDE.md` §2, piano §16): `MapCrud`, lista e form generati, l'unico handler, il filtro
  globale, `IOwnedByDepartment` a insieme con `ModuleBaseDepartment.Keep`, i grant a una posizione, al team di un FIR
  (A11a) e per riga (`IHasResourceScope`), `IHasStakeholder` e `DeniedToStakeholder`, `ISubmittedByMembers`, `IHasFir`,
  `IProjectable` (calendario, ricerca, award, usi dei file), il servizio notifiche e le preferenze del membro, le
  impostazioni dei moduli, i blocchi Data, `Refusals`, i token personali con la loro `audience`, `IPersonalDataEraser`,
  l'unico `IIvaoApiClient`, `IAtcActivitySource` per le viste `v_share_`, il vocabolario dei rating.
- **I moduli fuori dai dipartimenti**: sezione `/staff/events`, «a cura di» con l'ED sempre presente
  (`division.json → modules.events.baseDepartment: ED`, già scritto; `ModuleBaseDepartment` lo dice nel suo commento).
- **Chi gestisce il modulo** (piano 0.77): coordinator e assistant dell'ED tutto, per grant a una posizione; chi collabora
  modifica e **non cancella** (§6.3); HQ e web tutto per il nucleo.
- **Mai un'assegnazione automatica di award** (piano §9.7): il modulo segnala, una persona assegna (§5.4). **La macchina
  propone** (design M2 §6.3): il roster e i no-show li propone il sistema, li decide lo staff.

### 0.4 I nomi

| Che cosa | Nome |
|---|---|
| Chiave del modulo (`IModule.Key`, `division.json`, storia delle migrazioni) | `events` |
| Progetto .NET | `IvaoHub.Modules.Events` |
| Frontend | `web/src/modules/events/` |
| Prefisso delle tabelle | `evt_` |
| API | `/api/events/...` |
| Aree dei permessi | `Events`, `EventRoutes`, `EventBookings`, `EventAtc`, `EventReports` (§6.1) |
| Rotte pubbliche e dei membri | `/events`, `/events/{slug}`, `/events/{slug}/roster`, `/events/mine` (segmento riservato `events`) |
| Rotte dello staff | `/staff/events/...` |
| Namespace i18n | `events` |
| Fasi e branch | `E0`…`E17`, `m4/e<N>-<slug>` |

La sitemap del piano (§8.2) scrive `/me/bookings`: come in M3, `/me` è la dashboard del nucleo fatta di blocchi, e il
modulo ci entra con il blocco `events.myEvents` (§7.3); le pagine del membro stanno sotto `/events/mine`.

### 0.5 Che cosa si prende dagli strumenti di oggi, e che cosa no

| Si prende | Da | Dove qui |
|---|---|---|
| Lo slot pubblico come riga preparata dallo staff, che un pilota prende | `ivao-booking`, come lo legge il Gate Manager (`spec/SPEC.md` §3) | §1.5 |
| Lo slot privato con il solo orario allo scalo dell'evento e lo stand da assegnare | idem (21 slot privati a Napoli il 19 settembre) | §3.2 |
| I campi che il Gate Manager già legge, orari in UTC | idem | §7.4 |
| Ambito e tipo di un evento, e lo SES come modo di prenotare | backend del template HQ (piano §2.3-ter, §9.6) | §1.2 |
| La presenza di pilota e controllore verificata nella finestra dell'evento, award pilota e ATC separati | Toursystem (`event_participation`) | §5 |

| Non si prende | Perché |
|---|---|
| Lo slot libero come «`booked_by` nullo», nessuno stato | la prenotazione è **una riga sua** (§1.6) |
| La direzione dello slot da `type_of_flight` | si deduce dagli ICAO rispetto agli aeroporti dell'evento |
| Una chiave API condivisa (`x-api-key`) | un **token personale** con la sua `audience` (§7.4) |
| L'evento consultabile in sola lettura fino alla chiusura | da noi l'evento concluso **sparisce dal pubblico** (§2.4) |
| Punti bonus e mini-tour degli eventi del Toursystem | sono dei tour (M2), se un giorno servissero |

### 0.6 Scostamenti dal piano

| Il piano dice | Il design fa | Perché | Dove |
|---|---|---|---|
| `events.airport_icao FK → ref_.ivao_airports` (§7, schizzo `evt_`) | nessuna FK: `evt_event_airports`, validata con `IAirportDirectory`, con la capacità | nessuna FK fra contesti (§16.12); più scali per evento | §1.3 |
| `events.tipo`: RFE, online day, **training, exam**… | training ed esami sono di M3; il tipo è una **chiave del vocabolario dei tipi del calendario**, e il comportamento sta in due interruttori | un fork rinomina i tipi senza toccare il codice | §1.2 |
| `event_slots.booked_by_vid` sulla riga dello slot | la prenotazione è **una riga a sé** | lo slot è una riga dello staff e il membro non la scrive (guardiano); l'unicità la tiene il database | §1.6 |
| `event_participants (event_id, vid)` | nessuna iscrizione: la partecipazione è il **PIREP di supporto** | così l'ha descritta Carmine (§R.3) | §5.2 |
| «`EventPublished` → il nucleo crea la voce di calendario» (§9.2) | `IProjectable`, nella stessa transazione | il contratto deciso (§9.7, §16.4) | §8.1 |
| `/me/bookings` (§8.2) | `/events/mine`; il blocco `events.myEvents` in `/me` | `/me` è la dashboard del nucleo | §0.4 |
| Prenotazioni ATC di IVAO «job ogni 5 min» (§10) | lette alla richiesta, facoltative | la regola dei dati «adesso» | §9.1 |
| «Events: eventi e partecipanti nel periodo, via calendario unico» come segnale di award (§9.7) | il segnale lo manda un **PIREP di supporto validato**, secondo le **regole di award dell'evento** | lo vuole l'ED: un validatore prima di chi assegna | §5 |

---

## R. Che cosa sappiamo, e da dove

### R.1 Già deciso (piano e note)

- **Il catalogo** (§9.2 riga 1): eventi divisionali, di HQ, RFE, RFO, online o live; slot con prenotazione; postazioni
  ATC; il calendario unico. Sostituisce `ivao-booking` e il calendario del Blazor. Modulo obbligatorio, con la sola
  `maintenance` a caldo.
- **SES = modo di prenotazione di Events** (changelog 0.13, §9.6). **Un calendario per tutto** (§9.5).
- **Moduli fuori dai dipartimenti, collaborazione, «chi collabora non cancella»** (piano 0.72, 0.77; note
  `moduli-non-subordinati`, `ordine-dei-moduli`).
- **La granularità sta nel catalogo** (nota `2026-09-06-autorizzare-su-un-pezzo-di-un-altro-dipartimento`): «il FOD
  inserisce le rotte di un evento **ma non** tocca le postazioni da aprire» sono **due entità con due aree**.
- **Un permesso temporaneo per un evento** (§6.3): `resource_scope`.
- **L'uscita programmata di un evento** (nota `2026-09-09-il-documento-dice-di-se` §2).
- **I file con scadenza** (design M2 §1.14): «un evento dichiarerà i suoi allo stesso modo».
- **Il blocco `eventList`** (design M1); **«eventi della settimana senza ATC»** (nota `moduli-non-subordinati` §3.4).
- **L'Online Day passa all'ED**; Training lo aspetta come tipo di calendario (`TrainingSettings.ConflictKinds`).
- **Award**: il modulo segnala, chi ha `Awards.Assign` (in IT: MD e HQ) assegna (§9.1, §9.7).

### R.2 Che cosa dicono gli strumenti di oggi (letti in sola lettura il 28 settembre 2026)

- **`ivao-booking`**: nessuna copia su questo PC; lo si conosce dal Gate Manager (`RFO Gate manager/spec/SPEC.md` §3,
  `spec/riferimenti/integrazioni.md` §2): `GET https://booking.it.ivao.aero/api/flights` con `x-api-key`, un array dei voli
  dell'evento corrente, slot libero se `booked_by` è vuoto, `gate` anche `TBD`, orari UTC; «risponde 200 anche sugli
  errori PHP».
- **I numeri veri**: Napoli, 18 settembre: 173 slot, 105 prenotati, 21 privati. Roma, per il 3 ottobre: **441 slot** (219
  arrivi, 222 partenze) dalle 00:10 alle 23:55Z su 103 stand. Il Gate Manager rilegge ogni 5 minuti.
- **Il Gate Manager aspetta l'hub**: «il servizio centrale arriverà col nuovo sito di IVAO Italy»; chiede un'identità
  stabile dello slot (`SPEC.md` §14). Le prove si fanno sull'evento `prova-ponte-rfo`.
- **Toursystem**: partecipazione `PILOT|ATC` verificata, award «Event PILOT» ed «Event ATC» separati.
- **vIPI**: nessun evento né prenotazione, di proposito. Condivide con l'hub `v_share_atc_sessions` (§9.3), che ha il VID
  del controllore.

### R.3 I requisiti dell'ED (Carmine, 29 settembre 2026, in chat)

Quattro giri: **(c1)** le risposte alle domande della prima stesura, **(c2)** le precisazioni, **(c3)** le ultime, **(c4)** le risposte a §17.2. Da
confermare con un commento sulla PR (§17.1).

**Chi fa che cosa**
- **Chiunque dell'ED** crea l'evento, carica gli slot, assegna gli stand (c1).
- **Le postazioni ATC** le decidono **l'AOD e/o lo staff dei FIR** (CH, ACH, CHAx) (c1).
- **Il FOD, chiunque del FOD**, interviene se servono **rotte** (c1).
- **I no-show** li propone il sistema e li conferma **uno staffista fra ED, AOD e staff del FIR** (c1).
- **I PIREP di supporto** li valida chi ne ha il compito, **oggi l'MD**; gli award li assegna **oggi l'MD** (c1).

**I tipi di evento** (c1, c2)
- **RFE**: solo slot pubblici. **RFO**: slot pubblici e privati. **MSE** (Mega Slot Event): solo slot privati.
- **Eventi liberi**, senza slot: la gente viene e vola.
- **Online Day**: una serata in cui ognuno apre una postazione della divisione e ogni pilota vola, anche più voli, purché
  ogni volo tocchi almeno un aeroporto della divisione.
- **Eventi di HQ**: solo quelli in collaborazione con la divisione, con una scheda loro sul sito.

**Gli slot pubblici e le rotazioni** (c1, c2)
- Uno slot pubblico pubblica **callsign, numero di volo, tipo o tipi di aereo ammessi, origine, EOBT, destinazione, EIBT
  (on-block) e gate**. Chi lo prenota vola a quell'ora, più o meno, **con quel callsign** (se sulla rete è già occupato da
  chi non sapeva dell'evento, ne usa per forza un altro).
- **Una rotazione** è una catena arrivo, partenza, arrivo, partenza…: l'arrivo di una tratta è la partenza della
  successiva, e **ogni due tratte si tocca un aeroporto dell'evento** (evento a LIRF: LIRF-LIRN, LIRN-LIRF, LIRF-LIML,
  LIML-LIRF). È **un suggerimento per chi vuole fare real ops**: si prenota tutta, a pezzi (due voli di quattro, anche non
  attaccati) o una tratta sola.
- **Le rotazioni pubbliche si caricano** da un foglio Excel, un CSV o una tabella copiata a mano; il caricamento del file
  `.xlsx` **non serve** (c3).

**Gli slot privati e la capacità** (c1, c2)
- Uno slot privato ha un aeroporto **per forza** dell'evento; il pilota sceglie l'altro. Può essere un arrivo, una
  partenza, o tutti e due: un arrivo può chiedere **una partenza collegata**, così atterra e riparte dallo stesso gate.
- **Il sistema genera gli slot privati** dagli slot pubblici e dal massimo dell'aeroporto — **movimenti totali, oppure
  arrivi e partenze, per ora, configurabile per evento** — riempiendo i buchi in modo regolare. Il pilota prende uno slot
  generato e scrive l'altro aeroporto e l'altro orario.

**Prenotare** (c1, c2)
- **Quanti slot si vuole**, purché compatibili: fra la fine di uno (EIBT) e l'inizio dell'altro (EOBT) almeno **x minuti**,
  in un senso o nell'altro; x lo configura l'ED. Non serve che il secondo parta da dove è arrivato il primo.
- **Si prenota finché l'EOBT è futuro**, anche a evento in corso (c2); si ritira fino allo stesso momento (c3).

**L'ATC** (c1, c2)
- **Un roster con i turni**; la durata del turno si decide con l'evento (**predefinita 1 ora**).
- Il controllore compila un **form di disponibilità oraria**; il sistema, **secondo il rating e le postazioni che apre più
  spesso** (dato di `atc.it.ivao.aero`), gli **assegna uno o più turni**.
- **Rating minimo per controllare a un evento: AS3.** Per le postazioni: **TWR e GND meglio ADC+, APP meglio APC+, ACC
  meglio ACC+**; se non c'è nessuno, si scende al rating minimo della postazione.
- **x turni di pausa dopo y turni consecutivi**.
- Il roster proposto è **modificabile dallo staff**, che toglie o aggiunge anche chi non si è candidato (purché sia entrato
  nell'hub almeno una volta).
- **Le candidature chiudono x+y giorni prima** dell'evento; il roster **si pubblica esattamente x giorni prima**; ogni
  controllore trova il suo turno sulla dashboard, e c'è **una pagina con tutti i turni**. Dopo la pubblicazione il roster
  **si modifica e si notifica** (qualcuno dà buca all'ultimo).
- **Chi controlla, chi no, chi non si presenta**: i no-show **penalizzano** l'assegnazione dei turni successivi. **Contano
  per sempre**, pesati sugli eventi fatti (un no-show su dieci eventi pesa molto meno di uno su due); **li vedono il
  controllore e lo staff**; lo staff li toglie **solo a mano**, anche dopo. **Un turno ceduto per un buon motivo** (il
  trainer che lascia il turno al suo trainee) **non è un no-show**.

**Dopo l'evento** (c1, c2, c3)
- Servono: chi si era dato disponibile e ha controllato, chi no; chi ha prenotato e ha volato, chi no; **chi ha volato
  senza prenotare**.
- **Il PIREP per l'award «pilot support»** (e **«ATC support»** per chi ha controllato): **lo può mandare chiunque**; il
  sistema **verifica** se è valido e lo **segnala a chi valida**.
- **Award specifici di un evento**: per chi ha volato **una certa rotta**, **una tratta più lunga di x** (una distanza),
  **x tratte** volate, **un certo numero di voli** nell'evento, e simili.
- **La partecipazione riportata in automatico**: il sistema controlla solo i VID che sono entrati nell'hub **e hanno
  spuntato «riporta automaticamente la mia partecipazione»**; per chi l'ha spuntata crea **un PIREP che va ai
  validatori**, per piloti e controllori; tutti possono comunque riportare a mano. Vale per l'Online Day e per gli eventi
  liberi (c3).

**Le mail** (c1): a chi è stato messo in un turno o se il turno è cambiato; allo staff che valida i PIREP (oggi l'MD) che ci
sono PIREP da validare; a chi assegna gli award (oggi l'MD) che qualcuno deve riceverne uno. **Tutte disattivabili** dalle
impostazioni.

**La fine dell'evento** (c2): per il pubblico l'evento **sparisce quando termina**; allo staff restano statistiche e
informazioni; **i banner si eliminano 7 giorni dopo la fine**, per risparmiare spazio.

**La conservazione** (c3): le prenotazioni e i voli dei piloti, dopo un periodo, si cancellano e **restano solo le
statistiche**; i turni e le presenze ATC restano, perché il no-show conta per sempre.

**Decisi da me nel secondo giro, e approvati da Carmine** (c3): l'evento concluso sparisce anche dal calendario e dalla
ricerca, e il membro vede le sue prenotazioni passate in `/events/mine`; «prenota tutta la rotazione» prende le tratte
libere e compatibili e dice quali erano già prese; la penalità è proporzionale (§4.5); il periodo dei dati dei piloti è di
24 mesi, un'impostazione.

**Il quarto giro** (c4, risposte alle domande di §17.2): **non tutti gli eventi hanno un roster**; un evento **in presenza**
può chiedere a chi viene **che materiale porta** (PC, monitor…), **se partecipa alle cene** organizzate, e fargli
**prenotare le attività parallele** (per esempio i simulatori di volo) (§4-bis). E la prenotazione su IVAO si potrebbe fare
in automatico (§0.2).

---

## 1. Il modello

Tutte le tabelle stanno in `EventsDbContext : ModuleDbContext`, con la sua `__EFMigrationsHistory_events`. Nessuna FK
verso il nucleo: VID, ICAO, callsign, id di award e di file sono colonne non vincolate. Orari in UTC, `datetime(6)`.

### 1.1 Chi possiede una riga

- **L'evento** è **`IOwnedByDepartment`** con `OwnerDepartmentMask` (l'ED c'è sempre, `ModuleBaseDepartment.Keep`),
  **`IVisible`**, **`IPublishable`**, **`IAuditable`** e **`[Audited]`**, area `Events`, **`IHasResourceScope`**
  (`events:event:{id}`), **`IProjectable`**.
- **Le righe dello staff figlie dell'evento** — aeroporti, rotte, slot, postazioni, regole di award — **copiano maschera e
  scope dell'evento** a ogni scrittura, come le leg di un tour (`CrudOptions.BeforeAuthorize`). Ognuna nella **sua area**
  (§6.1). **Le postazioni e i turni sono `IHasFir`**: il FIR della postazione, dal nucleo.
- **Le righe dei membri** — prenotazione, disponibilità ATC, PIREP di supporto — sono **`ISubmittedByMembers`** e
  **`IHasStakeholder`**: il membro le crea e le ritira, e **non le decide** (`DeniedToStakeholder`). Hanno la maschera
  dell'evento.
- **Chi collabora non entra nel «a cura di»**: AOD, staff dei FIR, FOD e MD ricevono i loro permessi **con `scope: ED`**
  (§6.2), quindi lavorano su ogni evento, perché l'ED è in ogni evento. Il «a cura di» resta per chi cura **tutto**
  l'evento insieme all'ED (un evento con il SOD).
- **Niente `IHasParticipants`**: darebbe al membro `View` sull'area, cioè i VID di tutti. Il membro legge le sue righe dai
  **suoi** endpoint (come i PIREP in M2 e il training in M3).

### 1.2 L'evento — `evt_events`

| Campo | Che cosa |
|---|---|
| `slug` | univoco; `/events/{slug}` |
| `kind` | una chiave del **vocabolario dei tipi del calendario**: `rfe`, `rfo`, `mse`, `onlineDay`, `event`… (⚖️ §17.2 n.1: sì) |
| `public_slots`, `private_slots` | i due interruttori che fanno il comportamento; il tipo li **preimposta** (`kindPresets`, §1.12): RFE pubblici, RFO entrambi, MSE privati, evento libero e online day nessuno |
| `whole_division` | l'evento vale su tutti gli aeroporti e le postazioni della divisione (l'online day) invece che sui suoi scali |
| `organizer`, `external_url` | `Division`, `Network` (HQ in collaborazione), `OtherDivision`; la pagina di chi organizza |
| `title`, `summary` | `Localized<string>` |
| `body_json` | la descrizione: un `BlockDocument` (piano §9.3) |
| `banner_media_id` | un file della media library |
| `starts_at_utc`, `ends_at_utc` | la finestra dell'evento |
| `visible_from_utc` | l'uscita programmata; vuoto = alla pubblicazione (§2.2) |
| `booking_opens_at_utc` | da quando si prenota; si chiude slot per slot, all'EOBT (§3.3) |
| `has_roster`, `shift_minutes` | l'evento ha un roster ATC (non tutti ce l'hanno); durata del turno (predefinita dall'impostazione, 60) |
| `in_person`, `venue` | evento in presenza (M4c, §4-bis): iscrizione con domande e attività; il luogo, testo tradotto |
| `status`, `published_at` | `PublishStatus` del nucleo |
| `visibility` | `Public` o `Members` |
| `cancelled_at`, `cancelled_by`, `cancellation_note` | l'annullamento (§2.3) |
| `roster_proposed_at`, `after_done_at` | fin dove sono arrivati i job (§8.4): così un giro perso si recupera |
| `row_version` | concorrenza ottimistica |

Lo stato si calcola dalle date (§2.1), come per i tour.

### 1.3 Gli aeroporti e la capacità — `evt_event_airports`

`event_id`, `icao`, `ordinal`, e la **capacità** (c1): `max_movements_per_hour`, oppure `max_arrivals_per_hour` e
`max_departures_per_hour`. Validati con `IAirportDirectory.FindAsync`. Un evento `whole_division` non ne ha.

### 1.4 Le rotte — `evt_routes`

`event_id`, `departure_icao`, `arrival_icao`, `route` (testo), `remarks`. Area **`EventRoutes`**: le scrive il FOD (c1). È
l'esempio della nota `autorizzare-su-un-pezzo…`, qui come entità sua.

### 1.5 Gli slot — `evt_slots`

| Campo | Che cosa |
|---|---|
| `event_id`, `kind` | `Public` o `Private` |
| `event_airport_icao`, `is_arrival` | lo scalo dell'evento e il verso (per un pubblico, dedotti dagli ICAO) |
| `callsign`, `flight_number` | solo pubblico |
| `aircraft_types` | solo pubblico: uno o più tipi ICAO ammessi, validati con `IAircraftTypeDirectory` (lista di codici in JSON) |
| `departure_icao`, `arrival_icao` | solo pubblico |
| `off_block_utc`, `on_block_utc` | pubblico: EOBT ed EIBT; privato: l'orario allo scalo dell'evento sta in uno dei due |
| `stand` | pubblico: testo libero; privato: vuoto (§0.2) |
| `rotation_code`, `rotation_leg` | pubblico: la catena di cui la tratta fa parte, e il suo posto (§3.1) |
| `generated` | privato: creato dal generatore (§3.2) |
| `row_version` | |

Area **`EventBookings`**. Un pubblico è univoco su `(event_id, callsign, off_block_utc)`. Uno slot prenotato non si elimina:
lo staff prima toglie la prenotazione.

### 1.6 Le prenotazioni — `evt_bookings`

| Campo | Che cosa |
|---|---|
| `event_id`, `slot_id` | **indice univoco su `slot_id`**: uno slot, una prenotazione |
| `booker_vid` | lo stakeholder |
| `aircraft_icao` | il tipo scelto fra quelli ammessi (pubblico) o dichiarato (privato) |
| `callsign`, `other_icao`, `other_time_utc` | solo privato: il volo del pilota, l'altro aeroporto e il suo orario |
| `paired_booking_id` | privato: l'arrivo e la partenza collegati, stesso gate (c1) |
| `flown_at`, `flown_session_id`, `flown_checked_at` | la verifica dopo l'evento (§5.1), e quando è stata fatta |
| `created_at` | |

`ISubmittedByMembers`, `IHasStakeholder`, area `EventBookings`, `IVisible = Members`. **Ritirare cancella la riga**; il
registro di chi ha fatto che cosa è l'audit del nucleo (`[Audited]`).

### 1.7 L'ATC — `evt_atc_positions`, `evt_atc_availability`, `evt_atc_shifts`

- **`evt_atc_positions`**: le postazioni da aprire — `callsign` dall'elenco del nucleo (`ref_ivao_atc_positions`), `fir`,
  `from_utc`, `to_utc` (predefinita la finestra dell'evento), una nota. `IHasFir`. Area `EventAtc`.
- **`evt_atc_availability`**: la disponibilità di un controllore per l'evento — `controller_vid`, finestre orarie (righe
  figlie `from_utc`, `to_utc`), una nota. Stakeholder il controllore.
- **`evt_atc_shifts`**: un turno — `position_id`, `controller_vid`, `from_utc`, `to_utc`, `origin` (`Proposed` dal sistema
  o `Staff`), `notified_at`, e l'esito (§4.5): `attendance` (`Unknown`, `Attended`, `NoShowProposed`, `NoShow`,
  `Excused`), `attendance_by`, `attendance_at`, `attendance_note`. `IHasFir` (quello della postazione), stakeholder il
  controllore. **I turni restano per sempre** (c3): sono il registro di affidabilità.

### 1.8 Il PIREP di supporto — `evt_reports`, `evt_report_items`

- **`evt_reports`**: `event_id`, `vid`, `kind` (`Pilot`, `Atc`), `source` (`Manual`, `Auto`), `state` (`Submitted`,
  `Accepted`, `Rejected`), `verification` (`Valid`, `Partial`, `Invalid`, `Unavailable`), `decided_by`, `decided_at`,
  `decision_note`, `submitted_at`. Univoco su `(event_id, vid, kind)`. `ISubmittedByMembers`, `IHasStakeholder`, area
  **`EventReports`**, `IProjectable` (i segnali di award, §5.4).
- **`evt_report_items`**: un volo (callsign, partenza, arrivo, giorno) o un turno (postazione, da, a) dichiarato, con
  quello che la verifica ha trovato: la sessione, i minuti, la distanza in miglia, `verified`.

### 1.9 Le regole di award — `evt_award_rules`

`event_id`, `award_id`, `kind` (`Pilot`, `Atc`), `criterion` da un **elenco chiuso** (c3), `parameters`:

| Criterio | Parametri | Esempio |
|---|---|---|
| `AnyFlight` / `AnyShift` | — | «pilot support», «ATC support» |
| `MinFlights` | N | almeno N tratte volate nell'evento |
| `Route` | partenza, arrivo | ha volato LIRF-LIRN |
| `RouteTimes` | partenza, arrivo, N | ha volato quella rotta almeno N volte |
| `MinLegDistance` | miglia | una tratta più lunga di x (dagli aeroporti del nucleo) |
| `MinAtcMinutes` | minuti, tipo di postazione facoltativo | almeno N minuti di controllo |

Righe figlie dell'evento, area `Events`. Un criterio nuovo è una riga dell'elenco nel codice, non un'espressione libera.

### 1.10 Le statistiche — `evt_event_stats`

Una riga per evento, **solo numeri, per sempre** (c3): slot pubblici e privati, prenotati, prenotati e volati, prenotati
e non volati, **volati senza prenotare**, disponibilità ATC date, turni, controllati, no-show, giustificati, PIREP mandati
e accettati, award segnalati; per un evento in presenza, iscritti, le somme delle risposte e i posti presi nelle attività.
Calcolata dopo l'evento (§5.1), ricalcolabile dallo staff.

### 1.11 Le preferenze del membro

Una preferenza del modulo (`IModule.Preferences`, come M2): **«riporta automaticamente la mia partecipazione agli
eventi»**, spenta di predefinito (c3). Le mail si spengono con le preferenze delle notifiche del nucleo.

### 1.12 Le impostazioni — `EventsSettings`

Impostazioni del modulo del nucleo, schermata generata, `Events.ManageSettings`. **I predefiniti non conoscono la
divisione** (test «XX»): i valori di IT (AS3, i tipi) li scrive la divisione.

| Impostazione | Predefinito | Fonte |
|---|---|---|
| `kindPresets` — per tipo di evento, gli interruttori `public_slots`, `private_slots`, `has_roster`, `whole_division`, `in_person` | vuoto; IT: `rfe`, `rfo`, `mse`, `onlineDay` | c1, c2 |
| `bookingGapMinutes` — distanza minima fra due prenotazioni dello stesso pilota | **10** | c1; §17.2 n.6 |
| `shiftMinutes` — durata del turno se l'evento non la dice | 60 | c1 |
| `minimumAtcRating` — il rating minimo per candidarsi, dal vocabolario | nessuno; IT: AS3 | c1 |
| `maxConsecutiveShifts` (y), `breakShifts` (x) | 2, 1 | c1; §17.2 n.6 |
| `applicationsCloseDays` (x+y), `rosterPublishDays` (x) | **3**, **2**: lo staff ha un giorno per correggere | c2; §17.2 n.6 |
| `experienceMonths` — quanto indietro guardare le postazioni aperte | 12 | c1 |
| `noShowWeight` — peso della penalità (§4.5) | 1 | c3 |
| `attendanceMinimumMinutes` — minuti in un turno per dire «c'era» | 30 | §17.2 n.6 |
| `reportDays` — giorni dopo l'evento per mandare un PIREP di supporto | 14 | §17.2 n.6 |
| `pilotRetentionMonths` — prenotazioni e PIREP dei piloti dopo l'evento | 24 | c3 |
| `inPersonRetentionMonths` — iscrizioni e attività di un evento in presenza dopo l'evento | 3 | §4-bis |

### 1.13 Dal nucleo, senza scriverlo nel modulo

- **Rating** (c1): «AS3 minimo» è un'impostazione scelta dal vocabolario. «TWR e GND ADC+, APP APC+, ACC ACC+, poi il
  minimo della postazione» sono **regole di IVAO**: stanno nel **vocabolario dei rating del nucleo** (come deciso in M3,
  n.4), che deve rispondere a due domande — il rating **preferito** per un tipo di postazione e il **minimo** di una
  postazione (estensione n.4). Il modulo non scrive numeri di rating.
- **Postazioni, aeroporti, aerei, distanze**: `IAtcPositionDirectory`, `IAirportDirectory` (con le coordinate),
  `IAircraftTypeDirectory`.
- **Il nome di una persona**: dal nucleo, mai copiato; «persona cancellata» con l'helper del nucleo.

---

## 2. Il ciclo di vita dell'evento

### 2.1 Gli stati, dalle date

| Stato | Quando | Chi lo vede |
|---|---|---|
| **Bozza** | `status = Draft` | staff |
| **Programmato** | pubblicato, prima di `visible_from_utc` | staff |
| **Annunciato** | visibile, prima di `booking_opens_at_utc` | tutti |
| **Prenotazioni aperte** | da `booking_opens_at_utc`, finché resta uno slot con EOBT futuro | tutti |
| **In corso** | fra `starts_at_utc` ed `ends_at_utc` (si prenota ancora, slot per slot) | tutti |
| **Concluso** | dopo `ends_at_utc` | **solo staff** e, per le proprie righe, il membro (c2) |
| **Annullato** | `cancelled_at` scritto | tutti fino alla fine, con la nota |

Per l'ATC: **candidature aperte** fino a inizio − `applicationsCloseDays`; **roster pubblicato** da inizio −
`rosterPublishDays` (§4.4). Nessun job cambia uno stato: il server lo calcola a ogni richiesta (§10.2).

### 2.2 Pubblicare, e l'uscita programmata

- **Pubblica** (`Events.Edit`) controlla: titolo e riassunto in tutte le lingue della divisione, date coerenti
  (`visible_from ≤ booking_opens ≤ starts < ends`), gli scali per un evento a slot, il link per un evento di altri. I
  rifiuti con `Refusals`.
- **L'uscita programmata** ha la forma dei tour: la riga è pubblicata e la **data** dice da quando si vede. Alle tre
  domande della nota `il-documento-dice-di-se`: esce la riga com'è (è già passata dai controlli); il rifiuto non può
  capitare all'ora X; il contrario (sparire a una data) è la fine dell'evento (§2.4). La proiezione segue con il job
  `events-release` (§8.4). Tour ed eventi hanno la stessa forma: al terzo modulo passa nel nucleo (⚖️ §17.2 n.4: sì).

### 2.3 Annullare, eliminare

- **Annulla** (`Events.Edit`): `cancelled_at` e una nota tradotta; mail a chi ha prenotato o ha un turno.
- **Elimina** (`Events.Delete`, `DeletePolicy`): solo senza prenotazioni, disponibilità, turni o PIREP; altrimenti si
  annulla. Chi collabora non elimina (§6.3).

### 2.4 La fine dell'evento

Dopo `ends_at_utc` (c2):

- **per il pubblico l'evento non esiste più**: la pagina risponde 404 a chi non è staff, la lista e il blocco lo tolgono,
  e anche **calendario e ricerca** (`events-release` riproietta, e la proiezione di un evento concluso è vuota);
- il membro vede le sue prenotazioni, i suoi turni e i suoi PIREP in `/events/mine`;
- lo staff ha tutto, con le statistiche (§5.1);
- **i banner e le immagini della descrizione** sono dichiarati come usi fino alla fine + **7 giorni**
  (`MediaUseProjection`): il job del nucleo `media-expiry` li elimina se nessun altro li usa.

---

## 3. I piloti

### 3.1 Gli slot pubblici e le rotazioni dal foglio

Lo staff (`EventBookings.Edit`) **incolla una tabella** copiata da un foglio di calcolo (arriva come testo separato da
tabulazioni) **o carica un CSV** (c2; niente `.xlsx`, c3), con una riga d'intestazione: `callsign`, `flight_number`,
`aircraft_types` (separati da `/`), `departure_icao`, `off_block_utc`, `arrival_icao`, `on_block_utc`, `stand`, e facoltativi
`rotation` (un codice qualunque) e `leg` (il posto nella catena). L'import:

- valida **tutto o niente**, con i rifiuti per riga in `Refusals` (`rows[12].aircraft_types`);
- controlla ogni **catena**: tratte ordinate nel tempo, l'arrivo di una è la partenza della successiva, **ogni due tratte
  uno scalo dell'evento**, la distanza di `bookingGapMinutes` fra una tratta e l'altra (così una rotazione intera è
  prenotabile);
- **aggiunge**, oppure **sostituisce gli slot pubblici liberi** (quelli prenotati non si toccano);
- una sola transazione, un solo endpoint.

Poi lista e form generati per le correzioni. Nessuna griglia scritta a mano.

### 3.2 Gli slot privati generati

Per un evento con `private_slots`, lo staff preme **«genera gli slot privati»** (§1.3; c1, c2):

1. per ogni scalo e ogni ora della finestra di prenotazione, il posto libero è la capacità meno gli slot pubblici di
   quell'ora (per verso, o in totale se la capacità è in movimenti);
2. i privati si distribuiscono **a intervalli regolari** nell'ora, lontano dagli orari dei pubblici;
3. rigenerare **sostituisce i privati liberi**; quelli prenotati restano e contano nella capacità.

Uno slot privato ha solo scalo, verso e orario. Per un **MSE** (solo privati) la capacità è tutta privata.

### 3.3 Prenotare uno slot pubblico

Con il login, da `booking_opens_at_utc` e **finché l'EOBT dello slot è futuro** (c2):

1. il pilota sceglie uno slot libero (filtri: arrivi o partenze, orario, tipo di aereo, compagnia, rotazione) e il tipo di
   aereo fra quelli ammessi;
2. il server controlla la finestra, lo slot, e la **compatibilità** (§3.5);
3. **inserisce** la prenotazione; se un altro l'ha presa un istante prima, l'indice univoco risponde e il pilota legge
   «slot appena preso da un altro pilota» (§10.1).

**«Prenota tutta la rotazione»** prende in una transazione **le tratte libere e compatibili** e dice quali erano già prese
(c3). Si prenota anche una tratta sola, o due non attaccate (c2).

### 3.4 Prenotare uno slot privato

Il pilota sceglie uno slot generato (scalo, verso, orario) e scrive **callsign, aereo, l'altro aeroporto e il suo
orario** (c1, c2). Un arrivo può chiedere **la partenza collegata**: il pilota sceglie anche uno slot privato di partenza
dallo stesso scalo, e le due prenotazioni nascono insieme, collegate (`paired_booking_id`), perché il Gate Manager dia lo
stesso gate. Nessuna conferma dello staff: la capacità è già decisa dal generatore.

### 3.5 La compatibilità

Fra due prenotazioni dello stesso pilota nello stesso evento — intervalli `[EOBT, EIBT]`, per un privato dall'orario allo
scalo e dall'altro orario — ci sono **almeno `bookingGapMinutes`**, in un senso o nell'altro (c1, c2). Nessun limite di
numero. Due richieste dello stesso pilota nello stesso istante si serializzano con un blocco (`SELECT … FOR UPDATE`) sulla
riga della sua prima prenotazione dell'evento, o sulla riga dell'evento se è la prima: la regola la controlla il server,
una volta, con le prenotazioni già salvate davanti.

### 3.6 Ritirare, togliere

Il pilota ritira **fino all'EOBT** (c3): la riga si cancella e lo slot torna libero. Lo staff (`EventBookings.Edit`) toglie
una prenotazione con un motivo: mail `bookingRemoved`.

---

## 4. L'ATC (M4b)

### 4.1 Le postazioni

L'AOD e lo staff del FIR (`EventAtc.Edit`, §6.2) scelgono le postazioni dall'elenco del nucleo e le loro finestre (c1). Il
FIR della postazione viene dal nucleo: con `firStaffScope: own` (IT ci passa con A11b) lo staff di un FIR vede e decide solo
le postazioni del suo FIR (A11a, nota `2026-09-27-i-capi-fir-sul-loro-fir`).

### 4.2 La disponibilità

Fino a inizio − `applicationsCloseDays`, un membro con rating almeno `minimumAtcRating` compila **le sue finestre orarie**
(c1) dalla pagina dell'evento, e una nota. Le cambia o la ritira fino alla chiusura.

### 4.3 La proposta del roster

Alla chiusura delle candidature il job `events-roster` (§8.4) **propone il roster** (c1). È codice del modulo,
**deterministico** (stessi dati, stesso roster), e per ogni turno dice **perché** l'ha dato a quella persona.

1. **I turni**: ogni postazione, nella sua finestra, a pezzi di `shift_minutes`.
2. **Chi può**, per ogni turno: disponibile per tutto il turno; rating almeno `minimumAtcRating` e almeno **il minimo della
   postazione** (vocabolario, §1.13); non già in un altro turno a quell'ora; non oltre `maxConsecutiveShifts` turni di fila
   senza `breakShifts` turni di pausa.
3. **Chi prima**, fra quelli che possono:
   1. chi ha il **rating preferito** per quel tipo di postazione (ADC+ per TWR e GND, APC+ per APP, ACC+ per ACC:
      vocabolario); gli altri solo se non c'è nessuno (c1);
   2. chi **apre più spesso quella postazione, o quel tipo**, negli ultimi `experienceMonths` (le sessioni condivise da
      vIPI, §9.3; senza la sorgente, questo criterio non c'è e gli altri valgono lo stesso);
   3. la **penalità di affidabilità** più bassa (§4.5);
   4. chi ha **meno turni** in questo roster, così i turni si spargono;
   5. a parità, chi si è candidato prima.
4. **L'ordine dei turni**: prima quelli con meno candidati possibili, così i turni difficili non restano vuoti.

Un turno senza nessuno resta **scoperto**, in evidenza (blocco `events.atcCoverage`).

### 4.4 Correggere, pubblicare, cambiare

- **Fra la chiusura e la pubblicazione** lo staff (`EventAtc.Edit`) corregge: sposta, toglie, **aggiunge anche chi non si è
  candidato**, purché sia entrato nell'hub almeno una volta (c1: il roster del nucleo è chi ha fatto login). Un'aggiunta
  fuori dalle regole del §4.3 (rating sotto il preferito, turni di fila) è un **avviso**, non un rifiuto: decide lo staff.
- **A inizio − `rosterPublishDays` il roster è pubblicato** (c2), per data, senza un pulsante: ogni controllore vede il suo
  turno in `/me` (`events.myEvents`) e in `/events/mine`, e tutti — con i nomi solo a chi ha fatto il login — la pagina
  `/events/{slug}/roster`. Il job `events-roster` manda `atcShiftAssigned` a ognuno (`notified_at`).
- **Dopo la pubblicazione** il roster si modifica ancora (c1): ogni turno aggiunto, spostato o tolto manda
  `atcShiftAssigned`, `atcShiftChanged` o `atcShiftRemoved` all'interessato, subito.

### 4.5 No-show e registro di affidabilità

- **Dopo l'evento** (§5.1) il sistema confronta ogni turno con le sessioni di quel VID su quella postazione: almeno
  `attendanceMinimumMinutes` → `Attended`; meno → **`NoShowProposed`**, con accanto chi ha coperto la postazione in quella
  finestra, se qualcuno l'ha fatto (il trainee che ha preso il turno del trainer, c1).
- **Lo staff conferma** (`NoShow`) o **giustifica** (`Excused`, con una nota) (c1): ED, AOD o staff del FIR, con
  `EventAtc.Edit`, **mai sul proprio turno**. Senza la sorgente delle sessioni, tutti i turni restano `Unknown` e lo staff
  li segna a mano.
- **Il registro** di un controllore: turni fatti, no-show, giustificati, per evento. **Lo vedono lui** (`/events/mine`) **e
  lo staff** (c3). **Un no-show lo toglie solo lo staff, a mano**, con una nota, anche mesi dopo (c3): l'esito diventa
  `Excused`, e l'audit tiene chi e quando.
- **La penalità** (c3), usata dal §4.3: `noShowWeight × no-show ÷ (turni fatti + no-show + 2)`. Un no-show su dieci eventi
  pesa 0,08; su due, 0,25. Il «+ 2» evita che il primo no-show di un nuovo controllore pesi il massimo. **Conta per sempre**
  (c3): nessuna scadenza.

---

## 4-bis. Gli eventi in presenza (M4c)

Carmine (§17.2 n.6): un evento che non ha un roster può chiedere altro — **se vieni di persona, che materiale porti (PC,
monitor…), se partecipi alle cene organizzate, se vuoi prenotare le attività parallele** (per esempio i simulatori di
volo). È **una funzione del modulo** (`CLAUDE.md` §5, caso c, decisa qui): nessun meccanismo del nucleo la copre, e non
ne serve uno nuovo — lo staff scrive le domande come righe, e il form del membro è il `SchemaForm` di sempre, con lo schema
costruito dalle righe.

### 4-bis.1 Il modello

- **`evt_questions`**: le domande dell'evento — `label` tradotta, `type` da un **elenco chiuso** (`YesNo`, `Choice`,
  `MultiChoice`, `Quantity`, `Text`), le scelte tradotte, obbligatoria o no, ordine. Righe figlie dell'evento, area
  `Events`. «Che materiale porti» è una `MultiChoice` (PC, monitor…) o una `Quantity` per voce; «vieni alla cena del
  sabato» è un `YesNo`.
- **`evt_registrations`**: l'iscrizione di un membro — `vid`, `answers` (per domanda, validate dal server secondo il tipo),
  `created_at`. Univoca su `(event_id, vid)`. `ISubmittedByMembers`, `IHasStakeholder`, area `EventBookings`.
- **`evt_activities`**: un'attività parallela — `name` tradotto, luogo, finestra, **durata del turno** e **posti per turno**
  (due simulatori = 2 posti ogni 30 minuti). Righe figlie dell'evento, area `EventBookings`.
- **`evt_activity_bookings`**: un posto preso — `activity_id`, `starts_at_utc`, `vid`. Solo per chi è iscritto all'evento.
  I posti si contano con un blocco sulla riga dell'attività (`SELECT … FOR UPDATE`), perché un turno ha più posti e un
  indice univoco non basta; un membro non prende due turni sovrapposti.

### 4-bis.2 Come va

- **Lo staff** (`Events.Edit` per domande e attività, `EventBookings.Edit` per le iscrizioni) prepara domande e attività,
  vede le iscrizioni e le **somme** (quanti a cena, quanti monitor), toglie un'iscrizione.
- **Il membro**, con il login, si iscrive dalla pagina dell'evento rispondendo alle domande, prenota i turni delle attività,
  cambia risposte e turni o si ritira **fino all'inizio dell'evento**; tutto in `/events/mine`.
- **Mail**: `registrationReceived` (con il riepilogo delle risposte e dei turni), `eventChanged` e `eventCancelled` anche a
  chi è iscritto.
- **Dati personali**: le risposte parlano di una persona e di un posto fisico, quindi si tengono **poco**:
  `inPersonRetentionMonths` (3) dopo l'evento, poi si cancellano; le somme restano nelle statistiche. Visibili solo allo
  staff con `EventBookings.View` e al membro.
- Un evento in presenza può avere anche slot o roster: gli interruttori sono indipendenti (§1.2).

---

## 5. Dopo l'evento (M4b)

### 5.1 Le verifiche e le statistiche

Il job `events-after` (§8.4), dopo `ends_at_utc`, a lotti:

- **chi ha prenotato e ha volato**: per ogni prenotazione, `SearchSessionsAsync(vid, finestra, partenza, arrivo)` — la
  chiamata dei controlli dei PIREP di M2 — scrive `flown_at`; il callsign non conta, perché poteva essere occupato (c1);
- **chi ha volato senza prenotare**: le sessioni con partenza o arrivo negli scali dell'evento nella finestra, **senza
  VID** (`/v2/tracker/sessions?departureId=…&from=…&to=…`, estensione n.2), meno chi ha prenotato: **solo il numero** va
  nelle statistiche, nessun VID si salva (c1);
- **i turni**: §4.5;
- **i PIREP automatici**: per chi ha la preferenza del §1.11 (c3), le sue sessioni nella finestra — voli che toccano gli
  scali dell'evento, o un aeroporto della divisione per un evento `whole_division`; sessioni ATC su una postazione
  dell'evento o della divisione — e, se ce ne sono, un PIREP `Auto` per pilota e uno per ATC (§5.2);
- **le statistiche** (§1.10).

Per l'online day e gli eventi liberi **si guardano solo i VID con la preferenza** e chi manda un PIREP a mano (c3): nessuna
ricerca per ogni aeroporto della divisione.

### 5.2 Il PIREP di supporto

Chiunque abbia volato o controllato (c2) lo manda da `/events/mine`, entro `reportDays` dalla fine: **pilota** con uno o
più voli (callsign, partenza, arrivo), **ATC** con uno o più turni (postazione, da, a). Uno per tipo per evento; si
corregge finché non è deciso. Il sistema crea lo stesso PIREP da solo per chi ha la preferenza (§5.1).

### 5.3 La verifica e la validazione

- **La verifica** è del sistema, a ogni invio: per ogni voce cerca la sessione (tracker per i voli, sessioni condivise o
  tracker per l'ATC), scrive che cosa ha trovato — sessione, minuti, distanza — e dà al PIREP `Valid`, `Partial`,
  `Invalid` o `Unavailable` (la sorgente non risponde) (c2).
- **La validazione** è di una persona (`EventReports.Edit`, oggi l'MD, §6.2): accetta o rifiuta con una nota, mai il
  proprio. Mail al membro (`reportDecided`). **I validatori ricevono un riepilogo** dei PIREP da validare
  (`reportsToValidate`), come il riepilogo dei validatori dei tour (M2), una volta al giorno.

### 5.4 Le regole di award e il segnale

**Accettare** un PIREP valuta le regole di award dell'evento (§1.9) sulle sue voci **verificate**, e per ogni regola
soddisfatta il PIREP proietta un **`AwardSignalProjection`** (VID, motivo, `award_id`) nella stessa transazione (c3). Chi ha
`Awards.Assign` (oggi l'MD) lo trova nella coda degli award del nucleo e **assegna a mano** (piano §9.7). Il nucleo oggi
**non avvisa** chi assegna: la mail «qualcuno deve ricevere un award» (c1) è l'estensione n.5, che vale anche per i tour.

---

## 6. Permessi

### 6.1 Il catalogo

Cinque aree, perché ogni capacità che si delega a un altro dipartimento è **una riga sua** (R.1).

| Permesso | Che cosa permette | Negato all'interessato |
|---|---|---|
| `Events.View` | eventi nel back office, bozze e conclusi compresi, e le statistiche | no |
| `Events.Edit` | creare, modificare, pubblicare, annullare; aeroporti, capacità, regole di award | no |
| `Events.Delete` | eliminare un evento senza righe dei membri (`DeletePolicy`) | no |
| `Events.ManageSettings` | impostazioni del modulo | no |
| `EventRoutes.View`, `.Edit` | le rotte | no |
| `EventBookings.View` | slot, prenotazioni, iscrizioni e attività con i VID; l'esportazione per il Gate Manager | no |
| `EventBookings.Edit` | slot, import, generatore, attività; togliere una prenotazione o un'iscrizione | no |
| `EventAtc.View` | postazioni, disponibilità, roster, registro di affidabilità | no |
| `EventAtc.Edit` | postazioni, correggere il roster, confermare o giustificare un no-show | **sì** |
| `EventReports.View` | i PIREP di supporto | no |
| `EventReports.Edit` | validare un PIREP di supporto | **sì** |

Ogni area ha il suo `View` e il suo `Edit`: il guardiano chiede `{Area}.Edit` per ogni riga dello staff (trappola di A5).

### 6.2 Chi li ha (`division.json → positionGrants`)

| | ED: EC, EAC | ED: EA1–9 | AOD | Staff dei FIR (CH, ACH, CHA) | FOD | MD |
|---|---|---|---|---|---|---|
| `Events.View` | ✓ | ✓ | ✓ | | ✓ | ✓ |
| `Events.Edit` | ✓ | ✓ | | | | |
| `Events.Delete`, `.ManageSettings` | ✓ | | | | | |
| `EventRoutes.*` | ✓ | ✓ | | | ✓ | |
| `EventBookings.*` | ✓ | ✓ | | | | |
| `EventAtc.*` | ✓ | ✓ | ✓ | ✓ (`firTeam`) | | |
| `EventReports.*` | ✓ (`View`) | ✓ (`View`) | | | | ✓ |

- **Tutte con `scope: ED`**: l'ED è in ogni evento, quindi chi collabora lavora su ogni evento **nella sua parte** senza
  essere nel «a cura di» (§1.1). Lo scope indipendente dal dipartimento della posizione il nucleo lo ha già
  (`PositionGrantSeed.Scope`, e il suo commento porta proprio l'esempio dell'AOD sulle postazioni degli eventi).
- **AOD, FOD, MD: tutti i livelli** (c1: «chiunque del FOD»); ⚖️ §17.2 n.2: sì, anche per AOD e MD.
- **Lo staff dei FIR** con `"firTeam": true` (A11a): vale solo su un'area con un'entità `IHasFir`, e `EventAtc` ce l'ha
  (postazioni e turni).
- HQ (DIR, ADIR) e il web (WM, AWM) tutto per il nucleo; il superadmin tutto. `Awards.Assign` resta del nucleo. Qualcuno
  fuori da queste posizioni riceve un grant a un VID, anche **su un evento solo** (`resource_scope`).

### 6.3 La collaborazione e la cancellazione

La nota `ordine-dei-moduli` §3.3 teme «una sfumatura» nell'unico handler. **Non serve**: `Events.Delete` lo tengono solo EC
ed EAC, e nessuna riga di collaborazione lo dà, quindi chi collabora non elimina su nessuna riga. È lo stesso modo in cui
`Tours.Delete` sta solo a coordinator e assistant del FOD (`TourEndpoints`, `DeletePolicy`). Il limite, detto: chi un giorno
desse `Events.Delete` a un altro dipartimento lo darebbe davvero; è configurazione visibile, come per i tour. ⚖️ §17.2 n.2: sì.

⚠️ **Da verificare nella fase dello scheletro**:
- che le schermate delle aree `EventAtc`, `EventRoutes` ed `EventReports` leggano quello che serve dell'evento padre attraverso
  i loro endpoint, per chi non ha `Events.View` (lo staff dei FIR: un grant `firTeam` non vale sull'area `Events`, che non
  ha righe `IHasFir`);
- che con `own` lo staff di un FIR veda le disponibilità dei candidati per le **sue** postazioni: la disponibilità non ha un
  FIR, e la schermata del roster le dà per postazione.

---

## 7. Schermate e blocchi

### 7.1 Pubblico e membri

- **`/events`** (pubblico): i prossimi eventi e quelli in corso, come schede, con filtri per tipo e scalo, e il
  `CalendarView` del nucleo sugli stessi eventi. Nessun archivio pubblico (c2).
- **`/events/{slug}`** (pubblico fino alla fine): banner, titolo, date in UTC e nell'ora della divisione, tipo,
  organizzatore, scali, rotte, descrizione. Poi:
  - **slot pubblici**: la lista, con libero o preso, filtri, le rotazioni raggruppate, **«Prenota»** e **«Prenota tutta la
    rotazione»**; un visitatore vede solo libero o preso, **mai chi** (piano §9.7; la regola di M3 §12 n.4);
  - **slot privati**: per scalo, verso e ora, con il form del volo;
  - **ATC**: le postazioni e «dai la tua disponibilità» fino alla chiusura; il roster, pubblicato, in
    **`/events/{slug}/roster`**;
  - **in presenza**: luogo, «iscriviti» con le domande, le attività con i turni e i posti liberi (§4-bis);
  - **il giorno dell'evento**: la `LiveStatusStrip` con chi è online sugli scali dell'evento (§9.1).
- **`/events/mine`** (membri): le mie prenotazioni (anche passate), le mie disponibilità e i miei turni, il mio **registro
  ATC**, i miei PIREP di supporto con «manda il PIREP», e la preferenza del §1.11.

### 7.2 Staff

- **`/staff/events`**: lista generata — **Bozze**, **Prossimi**, **In corso**, **Conclusi**, **Annullati**.
- **`/staff/events/{id}`**: il form generato dell'evento (campi tradotti, descrizione con l'editor dei blocchi, banner,
  scali con la capacità, regole di award), come l'editor del tour; poi le schede, ognuna con la sua area:
  - **Rotte** (FOD): lista e form generati;
  - **Slot**: lista generata, incolla o carica, «genera gli slot privati», «elimina i liberi»;
  - **Prenotazioni**: lista generata, «togli»;
  - **ATC**: postazioni; disponibilità; il **roster** come lista generata per postazione e ora, con il perché di ogni turno, gli
    avvisi, «aggiungi», «sposta», «togli»; dopo l'evento, i no-show da confermare;
  - **PIREP di supporto**: lista generata con la verifica di ogni voce, «accetta», «rifiuta»;
  - **In presenza**: domande e attività (liste e form generati), iscrizioni con le somme, turni delle attività;
  - **Statistiche**: i numeri del §1.10.
- **`/staff/events/controllers`**: il registro di affidabilità di tutti, con «togli il no-show».
- **`/staff/events/settings`**: impostazioni generate.

**Nessun componente nuovo** nell'elenco chiuso: `DataList`, `SchemaForm`, `CalendarView`, `LiveStatusStrip`, `MediaPicker`,
`StatusBadge`, `RatingBadge`, `ConfirmDialog`. L'`EventTimeline` del piano §8.3 non serve (il roster si legge in lista).
**Nessuna eccezione dichiarata** al motore CRUD; gli endpoint a mano sono verbi — prenotare, ritirare, incollare, generare,
esportare, dare la disponibilità, correggere il roster, confermare un no-show, mandare e decidere un PIREP, il «chi è
online» — e il rapporto di chiusura li conta per famiglia (§16.6 del piano).

### 7.3 Blocchi Data

| Blocco | Dove | Che cosa |
|---|---|---|
| `events.eventList` | home, `/events`, pagine | i prossimi eventi, filtrabili per tipo |
| `events.myEvents` | `/me` | le mie prossime prenotazioni e i miei turni; «Nessuna prenotazione — vai agli eventi» |
| `events.atcCoverage` | `/staff`, dashboard ED, AOD | eventi dei prossimi giorni con turni scoperti o candidature da chiudere |
| `events.staffQueue` | dashboard ED, AOD, MD | no-show da confermare, PIREP da validare |

A un visitatore i blocchi personali rispondono `signedIn: false`, come `myTours`. ⚠️ Ogni blocco ha le sue due metà nella
stessa PR e alza i conteggi di `uiKit.test.ts` e `DataBlockEndToEndTests`.

### 7.4 Il Gate Manager

Nell'hub il Gate Manager legge con un **token personale** (`CLAUDE.md` §2), non con una chiave condivisa:

- **audience `events.bookings`** in `IModule.TokenAudiences`; il token lo crea da `/me/tokens` chi ha
  `EventBookings.View`, e i permessi si ricostruiscono a ogni richiesta;
- **`GET /api/events/{slug}/bookings/export`**: i campi che il Gate Manager già legge (`callsign`, `booked_by`,
  `aircraft_icao`, `gate`, `eobt`, `eat`, `origin_icao`, `destination_icao`, orari UTC) **più** `slot_id` (l'identità
  stabile che chiede), `flight_number`, `rotation`, `leg` e, per un privato, `paired_slot_id` (arrivo e partenza sullo
  stesso gate); i privati con `gate` vuoto;
- un errore è uno stato HTTP di errore.

Il cambio nel Gate Manager è un lavoro del suo repository, provato su `prova-ponte-rfo` (fase E9).

---

## 8. Calendario, ricerca, notifiche, job

### 8.1 Calendario e ricerca

- **Una voce per evento**, dalla visibilità alla fine: tipo = il `kind` dell'evento, visibilità dell'evento, indirizzo
  `/events/{slug}`. Bozza, non ancora visibile, annullato o concluso: nessuna voce (§2.4); la pagina di un annullato resta,
  con la nota, fino alla fine.
- **Il tipo nel vocabolario**: il seme dei tipi riceve `rfe`, `rfo`, `mse`, `onlineDay` (estensione n.1); Training può
  allora aggiungere `onlineDay` ai suoi `conflictKinds`.
- **Ricerca**: gli eventi pubblici visibili e non conclusi.
- **Usi dei file**: banner e immagini fino alla fine + 7 giorni (c2).

### 8.2 Award

§5.4. Nessuna assegnazione automatica.

### 8.3 Notifiche (`events.*`)

| Tipo | A chi | Quando | Fonte |
|---|---|---|---|
| `bookingRemoved` | pilota | lo staff ha tolto la prenotazione, con il motivo | |
| `eventChanged` | chi ha prenotato o ha un turno | orari dell'evento cambiati | |
| `eventCancelled` | idem | evento annullato | |
| `atcShiftAssigned` | controllore | alla pubblicazione, o aggiunto dopo | c1 |
| `atcShiftChanged`, `atcShiftRemoved` | controllore | dopo la pubblicazione | c1 |
| `reportsToValidate` | validatori | una volta al giorno, se ce ne sono | c1 |
| `reportDecided` | membro | PIREP di supporto accettato o rifiutato | |
| `registrationReceived` | membro | iscrizione a un evento in presenza, con risposte e turni delle attività | §4-bis |
| *(nucleo)* un award da assegnare | chi ha `Awards.Assign` | un segnale nuovo in coda | c1; estensione n.5 |

Tutte disattivabili dal profilo (c1). Modelli in `web/src/modules/events/locales/{it,en}/events.json`.

### 8.4 Job del modulo

| Job | Ogni | Che cosa | Da che cosa decide |
|---|---|---|---|
| `events-release` | 15 minuti | riproietta gli eventi diventati visibili o conclusi | l'ultimo giro riuscito, come `TourReleaseJob` |
| `events-roster` | 15 minuti | propone il roster alla chiusura; manda le mail alla pubblicazione | `roster_proposed_at` sull'evento, `notified_at` sul turno |
| `events-after` | ogni ora | verifiche, no-show proposti, PIREP automatici, statistiche (§5.1), a lotti | `after_done_at` sull'evento, `flown_checked_at` sulle righe |
| `events-digest` | una volta al giorno | `reportsToValidate` | «già mandato oggi» scritto, come deciso per il riepilogo dei tour |
| `events-retention` | una volta al mese | §11 | le date delle righe |

Convenzioni di M2: `[DisallowConcurrentExecution]`, una riga in `hub_jobs_log`, mai un'eccezione, `RunAsync` per i test.
**Ognuno decide che cosa fare dai suoi dati, non dall'ora in cui gira** (§10.2).

---

## 9. Che cosa legge da IVAO e da vIPI

### 9.1 L'API di IVAO — misurata nella documentazione

La documentazione pubblica (`https://api.ivao.aero/docs/{core,data,tracker,atc}-json`, letta il 28 e 29 settembre 2026):

- **`/v2/tracker/sessions`** accetta `userId`, `callsign`, `connectionType` (`PILOT`, `ATC`…), `departureId`, `arrivalId`,
  `from`, `to`, `page`, `perPage` (massimo 100). Il client del nucleo la usa oggi **con il VID** (`SearchSessionsAsync`, M2);
  la ricerca **senza VID**, per aeroporto e finestra, è l'estensione n.2.
- **`/v2/atc/bookings/daily?date=&position=`** e `/v2/atc/bookings`: le prenotazioni delle postazioni su IVAO (utente,
  postazione, inizio, fine, training o esame). **Si leggono accanto al roster**, per vedere chi ha anche prenotato su IVAO
  (§17.2 n.3). Scriverle per il controllore è fuori da M4 (§0.2).
- **Nessun endpoint di eventi** in nessuna delle quattro.
- **Il «chi è online adesso»** della pagina è `GetNetworkStatusAsync` (whazzup, 60 secondi di cache): letto alla richiesta,
  mai campionato.
- **Da misurare** nella fase del nucleo: se il token dell'applicazione (`client_credentials`) basta per ogni lettura (la
  documentazione dichiara `oauth2` o `consumer`), e i limiti di chiamate.

### 9.2 Gli eventi di IVAO

Nessuna API: un evento di HQ in collaborazione si scrive a mano, con `organizer` e `external_url` (c1).

### 9.3 vIPI

Solo attraverso `IAtcActivitySource` e la vista `v_share_atc_sessions` (piano 1.18). Il modulo non nomina vIPI. Serve a due
cose: **le postazioni aperte più spesso** da un controllore (§4.3, c1: «dato estraibile da atc.it.ivao.aero») e **la
presenza nei turni** (§4.5, §5.3). La vista ha già `vid`; `IAtcActivitySource` oggi non lo espone e non dà una storia per
VID: estensione n.3. Con `atcData.source: none` il roster perde il criterio dell'esperienza, la presenza ATC si legge dal
tracker (`connectionType=ATC`, per VID) e il resto funziona uguale.

---

## 10. Hosting

### 10.1 La sera dell'apertura

Molti piloti nello stesso minuto, su un processo che forse dorme (risveglio misurato 8,4 s, ~4,9 s stimati dopo i tagli
della nota `2026-09-28-l-avvio-a-freddo`), a volte con due processi insieme, un pool di ≤ 15 connessioni:

- **l'apertura è una data**, non un lavoro del server: nessun job deve «aprire»;
- **l'unicità sta nel database** (§1.6): due processi, due richieste nello stesso istante, un vincitore; la compatibilità
  dello stesso pilota si serializza con un blocco sulla sua riga (§3.5); nessun lock in memoria, nessuna cache della
  disponibilità;
- **una prenotazione è una transazione breve**; la lista degli slot è una query sola anche con 441 righe;
- conto alla rovescia sulla pagina, nessuna coda.

### 10.2 I job e i dati «adesso»

La regola decisa (nota `2026-09-28-i-job-quando-passenger-spegne-l-hub` §8):

- **niente si campiona ogni minuto**: «chi è online» e le prenotazioni ATC di IVAO si leggono alla richiesta;
- **niente a ore fisse**;
- **tutto il resto recupera**: stati dalle date, roster proposto e mail da colonne «fatto il», verifiche dopo l'evento da
  dati **storici** (tracker e sessioni condivise), che si leggono anche giorni dopo.

⚠️ **Le verifiche dopo l'evento chiamano IVAO molte volte**: una per prenotazione (441 a Roma), qualche pagina per «volato
senza prenotare», una per membro con la preferenza. Il lavoro va **a lotti piccoli** per giro — ogni giro deve finire dentro la
richiesta del POST pianificato — e riprende al giro dopo; il ritmo si fissa con i limiti misurati (§9.1).

### 10.3 La manutenzione

Con `maintenance` del modulo le pagine restano leggibili e i verbi rispondono 503 (piano §9.7: «il booking ha un problema,
spegnilo»).

---

## 11. Conservazione e dati personali

| Che cosa | Quanto | Fonte |
|---|---|---|
| L'evento, gli slot, le rotte, le regole, le statistiche | per sempre (allo staff) | c2 |
| Prenotazioni e PIREP di supporto **dei piloti** | `pilotRetentionMonths` (24) dopo la fine, poi cancellati | c3 |
| Disponibilità ATC | cancellate a roster pubblicato + la fine dell'evento | — |
| **Turni ATC ed esiti** (il registro di affidabilità) | **per sempre** | c3 |
| PIREP di supporto **ATC** | come i turni | c3 |
| Iscrizioni a un evento in presenza e turni delle attività | `inPersonRetentionMonths` (3) dopo la fine, poi cancellati; le somme restano | §4-bis |
| Chi ha volato senza prenotare | mai salvato per VID: solo il numero | — |

### 11.1 La cancellazione dei dati di una persona

Il meccanismo è del nucleo (T20b): le colonne `booker_vid`, `controller_vid`, `vid`, `decided_by`, `cancelled_by`,
`attendance_by` seguono la convenzione e il nucleo ci scrive lo pseudonimo. **`EventsPersonalData : IPersonalDataEraser`**
fa quello che è **sulla** persona:

- prenotazioni, disponibilità e turni di **eventi non conclusi** si **cancellano** (lo slot torna libero, il turno si
  scopre e lo staff lo vede);
- quelli di eventi conclusi e i PIREP **restano con lo pseudonimo**, senza i dati del volo privato e senza note;
- **il registro di affidabilità diventa di nessuno**: i turni restano con lo pseudonimo, e le statistiche non cambiano;
- quello che ha fatto **come staff** resta con lo pseudonimo.

⚠️ `ErasureTests` non vede da solo i contesti dei moduli: va allargato nella fase che crea le tabelle.

---

## 12. Forkabilità

- **Nessun ICAO, nessuna postazione, nessun rating, nessun tipo di evento** nel codice, nei semi del modulo e nei
  predefiniti; test «XX». RFE, RFO, MSE e online day sono **chiavi del vocabolario** con i loro interruttori in
  `kindPresets`: un fork le rinomina o le toglie.
- **Il modulo non nomina IVAO né vIPI**: tracker, prenotazioni ATC e «chi è online» passano dal client del nucleo; sessioni
  condivise da `IAtcActivitySource`; le regole dei rating dal vocabolario.
- **Chi collabora** (AOD, FIR, FOD, MD) è **configurazione** (`positionGrants`).
- **«Della divisione»** (l'online day) è il paese della divisione e le sue postazioni, dal nucleo.
- **Il Gate Manager** è un programma esterno: l'hub gli dà un token e un formato.

---

## 13. Che cosa chiede al nucleo

Ognuna è una PR a sé, **prima** del codice del modulo che la usa, con la sua nota (`CLAUDE.md` §0, regola 6).

| # | Estensione | Nota | Blocco |
|---|---|---|---|
| 1 | **I tipi `rfe`, `rfo`, `mse`, `onlineDay`** nel seme dei tipi del calendario (il seme non tocca una chiave già scritta a mano) | no (come `exam` in M3) | M4a |
| 2 | **Le sessioni del tracker senza VID**, per aeroporto e finestra, a pagine (`IvaoSessionQuery` con il VID facoltativo) | breve | M4b |
| 3 | **Il VID e la storia per VID** in `IAtcActivitySource`: le sessioni di un controllore negli ultimi mesi, per postazione | breve | M4b |
| 4 | **Il vocabolario dei rating**: il rating preferito per un tipo di postazione e il minimo di una postazione (§1.13), se non risponde già; si misura nella fase | breve, se serve | M4b |
| 5 | **Una mail a chi assegna gli award** quando entra un segnale nuovo in coda, spegnibile dal profilo; vale anche per i tour | sì, breve | M4b |
| 6 | **Le prenotazioni ATC della rete** (`/v2/atc/bookings/daily`) dietro un'interfaccia del nucleo, in lettura | breve | M4b (§17.2 n.3) |
| 7 | ~~Cancellare solo con il permesso sul dipartimento di base~~ **non serve** (§6.3) | — | — |
| 8 | **VID e slug dei test** di Events in `CONTRIBUTING.md` (per esempio `761001–761099`, `evt-test-`, dopo un grep) | no; lo scrive il master | M4a |

Il resto c'è: `IProjectable` con più voci, usi dei file e award; i grant `firTeam` (A11a, unita il 29 settembre); lo scope
di un grant indipendente dal dipartimento della posizione; `TokenAudiences`; `Preferences`; `ModuleSettings`;
`DeletePolicy`; `BeforeAuthorize`; `Refusals`; «persona cancellata» (A12a di M3, da aspettare se non è ancora unita).

---

## 14. Tabelle e migrazioni

**Nucleo** (additive): nessuna tabella; i tipi del seme (n.1); quello che le estensioni n.2–n.6 decideranno.

**Modulo**: M4a `evt_events`, `evt_event_airports`, `evt_routes`, `evt_slots`, `evt_bookings`; M4b `evt_atc_positions`,
`evt_atc_availability` (con le finestre), `evt_atc_shifts`, `evt_reports`, `evt_report_items`, `evt_award_rules`,
`evt_event_stats`; M4c `evt_questions`, `evt_registrations`, `evt_activities`, `evt_activity_bookings`. La migrazione `Initial` nasce nella fase dello scheletro e non si tocca più; le altre arrivano con la
loro fase, sempre additive.

---

## 15. La rete di test

- **Unità**: lo stato dalle date; la direzione degli slot; l'import e le catene (ordine, aeroporto che coincide, uno scalo
  ogni due, la distanza); il **generatore** (capacità per verso e totale, pubblici che la consumano, intervalli regolari,
  rigenerazione che non tocca i prenotati); la **compatibilità** in tutti e due i sensi; il **roster** su dati finti
  (ammissibili, rating preferito e ripiego, esperienza, penalità, turni sparsi, pause, deterministico, il perché di ogni
  turno); la **penalità** (i due esempi del §4.5); le **regole di award** una per una, con la distanza fra due aeroporti.
- **Integrazione** (MariaDB vera, database condiviso, VID e slug dell'estensione n.8):
  - **due prenotazioni dello stesso slot nello stesso istante**: una vince; due prenotazioni incompatibili dello stesso
    pilota nello stesso istante: una vince;
  - si prenota a evento in corso con l'EOBT futuro, non con l'EOBT passato;
  - l'evento concluso: 404 al pubblico, nessuna voce di calendario, visibile allo staff e al membro per le sue righe;
  - il FOD scrive le rotte e non gli slot; l'AOD e un capo FIR scrivono l'ATC e non il testo dell'evento; con `own`, un capo
    FIR solo le postazioni del suo FIR; l'MD valida i PIREP e non tocca il resto;
  - chi collabora non elimina; EC ed EAC sì, solo senza righe dei membri;
  - nessuno valida il proprio PIREP né conferma il proprio no-show (superadmin compreso);
  - il roster si pubblica alla data giusta e manda una mail per turno, una volta; una modifica dopo la pubblicazione avvisa;
  - un no-show tolto a mano resta tolto, e il registro lo mostra;
  - un PIREP accettato proietta i segnali delle regole soddisfatte, e nessun altro;
  - l'esportazione con un token `events.bookings`, senza, e con un'altra audience;
  - la conservazione: prenotazioni dei piloti cancellate dopo il periodo, turni e statistiche no;
  - la cancellazione di una persona (§11.1);
  - in presenza: le risposte validate per tipo, una domanda obbligatoria senza risposta rifiutata; **l'ultimo posto di un
    turno preso da due membri nello stesso istante**: uno vince; iscrizioni cancellate dopo `inPersonRetentionMonths`.
- ⚠️ **L'ED e l'MD nei test**: i test dei contatti affermano i destinatari esatti dell'ED e dell'MD, quindi **nessuno staff ED
  o MD seminato con un'email** nei test del modulo; i permessi con grant a un VID.
- **Architettura**: il modulo non nomina IVAO né vIPI, non scrive ICAO né rating, nessuna chiamata HTTP.
- **Divisione XX**: nessuna stringa italiana, nessun ICAO, nessun tipo di evento nei predefiniti.
- **Fixture di IVAO**: tracker per VID e per aeroporto, registrati con `tools/record-ivao-fixtures.mjs`; nessuna chiamata
  vera nei test.
- **Smoke e giro completo**: M4a — evento RFO, incolla gli slot, genera i privati, pubblica, un pilota prenota una rotazione
  e un privato, ritira; M4b — disponibilità, roster proposto, corretto, pubblicato; PIREP di supporto, validato, segnale;
  M4c — un evento in presenza, iscrizione con le risposte, un turno al simulatore.

---

## 16. Ordine di lavoro proposto

Le fasi vere si scrivono in `10-piano-implementazione-m4.md` dopo l'approvazione; qui la forma e una misura grossolana
(**P**, **M**, **G** rispetto alle fasi di M3). Una fase, un branch `m4/e<N>-<slug>`, una PR; le fasi del modulo migrano
lo stesso contesto e vanno in fila, quelle del nucleo possono correre in parallelo.

**M4a — spegne `ivao-booking`**

| Fase | Contenuto | Misura |
|---|---|---|
| E0 | Le note di decisione di §17 e delle estensioni; `10-piano-implementazione-m4.md` | P |
| E1 | Nucleo: i tipi del calendario (n.1) | P |
| E2 | Modulo: scheletro, `Initial` (eventi, aeroporti), catalogo, `positionGrants`, impostazioni, menu, segmento riservato | M |
| E3 | L'evento nello staff: form, descrizione, banner, capacità, pubblicazione, uscita programmata, annullamento, eliminazione, la fine; calendario, ricerca, usi dei file; `events-release` | G |
| E4 | Il pubblico: `/events`, `/events/{slug}` senza prenotazioni, `events.eventList`; le rotte del FOD | M |
| E5 | Gli slot pubblici: tabella, incolla e carica, catene, liste; l'esportazione con il token `events.bookings` | M |
| E6 | Prenotare: verbi, compatibilità, rotazione intera, `/events/mine`, `events.myEvents`, mail | G |
| E7 | Gli slot privati: generatore, prenotazione, partenza collegata | M |
| E8 | Giro completo di M4a | P |
| E9 | Fuori dal repository: il Gate Manager legge l'hub (prove su `prova-ponte-rfo`); `ivao-booking` spento (Carmine) | P |

**M4b — l'ATC e il dopo evento**

| Fase | Contenuto | Misura |
|---|---|---|
| E10 | Nucleo: tracker senza VID, sessioni condivise per VID, vocabolario dei rating, mail degli award (n.2–n.5) | M |
| E11 | Postazioni, disponibilità, proposta del roster, correzione | G |
| E12 | Pubblicazione per data, mail, `/events/{slug}/roster`, turni in `/me`, modifiche notificate, `events.atcCoverage` | M |
| E13 | Dopo l'evento: verifiche, statistiche, no-show proposti e confermati, registro di affidabilità, `events.staffQueue` | G |
| E14 | PIREP di supporto: manuale, automatico con la preferenza, verifica, validazione, riepilogo, regole di award, segnali | G |
| E15 | Conservazione, `EventsPersonalData`, giro completo di M4b; le prenotazioni ATC di IVAO accanto al roster (n.6) | M |

**M4c — gli eventi in presenza**

| Fase | Contenuto | Misura |
|---|---|---|
| E16 | Domande e iscrizione: tabelle, form costruito dalle domande, somme per lo staff, `/events/mine`, mail | M |
| E17 | Attività parallele: turni e posti, prenotazione con il blocco, conservazione breve, giro completo di M4c | M |

M4c non dipende da M4b: può venire prima, se il primo evento in presenza arriva prima.

---

## 17. Domande per Carmine

### 17.1 Decise in chat il 29 settembre 2026 — da confermare sulla PR

Carmine ha risposto in chat, nei primi tre giri, alle domande della prima stesura e alle precisazioni (§R.3). Una risposta in chat
non ha un link: **un suo commento sulla PR che confermi questa sezione** la rende la decisione registrata.

1. **Chi risponde sui fatti dell'ED**: Carmine stesso (§R.3).
2. **Eventi di HQ**: solo quelli in collaborazione, scritti a mano con la loro scheda.
3. **Tipi**: RFE solo pubblici, RFO pubblici e privati, MSE solo privati, eventi liberi, online day.
4. **Slot pubblici e rotazioni**: i campi del §1.5; rotazioni come catene, prenotabili intere, a pezzi o a tratte.
5. **Prenotazioni**: nessun limite, compatibili a `bookingGapMinutes`; fino all'EOBT, anche a evento in corso; ritiro fino
   all'EOBT.
6. **Slot privati**: generati dalla capacità per evento (movimenti, o arrivi e partenze, per ora); il pilota scrive l'altro
   aeroporto e l'altro orario; l'arrivo può avere la partenza collegata.
7. **Import**: CSV o tabella incollata; niente `.xlsx`.
8. **Due blocchi**, M4a e M4b, un calendario solo.
9. **ATC**: roster proposto dal sistema (rating minimo AS3, rating preferito per tipo di postazione, esperienza, pause),
   corretto dallo staff, pubblicato esattamente x giorni prima, candidature chiuse x+y giorni prima, modifiche notificate.
10. **No-show**: proposti dal sistema, confermati o giustificati da ED, AOD o staff del FIR; contano per sempre, pesati;
    visibili al controllore e allo staff; tolti solo a mano.
11. **Dopo l'evento**: le cinque liste; il PIREP di supporto di chiunque, verificato dal sistema, validato da chi ne ha il
    permesso (oggi l'MD); regole di award dell'evento (rotta, distanza di una tratta, numero di tratte, di voli).
12. **Partecipazione automatica**: solo per chi ha spuntato la preferenza; crea un PIREP che va ai validatori; per l'online
    day e gli eventi liberi, solo chi ha la spunta o manda a mano.
13. **Mail**: turno assegnato o cambiato; PIREP da validare; award da assegnare; tutte disattivabili.
14. **Fine evento**: sparisce dal pubblico, calendario e ricerca compresi; resta allo staff; banner eliminati dopo 7 giorni.
15. **Conservazione**: prenotazioni e voli dei piloti cancellati dopo 24 mesi, restano le statistiche; turni ATC per sempre.
16. **Rotazione intera** prende le tratte libere e compatibili; **penalità** proporzionale (§4.5).
17. **Stand**: fuori da M4, da vedere dopo.

### 17.2 Le sette domande della seconda stesura — decise in chat il 29 settembre 2026, da confermare sulla PR

Poste sulla PR ([commento][q2]); Carmine ha risposto in chat. Come §17.1, aspettano il suo commento di conferma.

[q2]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/180#issuecomment-5880359954

1. **Il tipo dell'evento come chiave del vocabolario del calendario** (§1.2), con `kindPresets` per gli interruttori.
   Raccomandato: sì. **Deciso: sì.**
2. **I permessi** (§6.2, §6.3): collaboratori con `scope: ED` invece del «a cura di»; `Events.Delete` e le impostazioni solo a
   EC ed EAC; AOD e MD a tutti i livelli come il FOD. Raccomandato: sì. **Deciso: sì.**
3. **Le prenotazioni ATC di IVAO accanto al roster** (§9.1, n.6). Raccomandato: sì, in fondo a M4b. **Deciso: sì**, e
   Carmine vorrebbe anche **fare la prenotazione su IVAO in automatico**: fuori da M4, un meccanismo nuovo con una nota sua
   (§0.2).
4. **L'uscita programmata** con la forma dei tour (§2.2). Raccomandato: sì, nel nucleo al terzo modulo. **Deciso: sì, come
   raccomandato.**
5. **Mail di massa per un evento nuovo**. Raccomandato: no. **Deciso: no.**
6. **I predefiniti** (§1.12). **Deciso**: **10 minuti** fra due prenotazioni di un pilota; candidature chiuse **3 giorni**
   prima e roster pubblicato **2 giorni** prima; il resto come raccomandato (2 turni di fila e 1 di pausa, 30 minuti per dire
   «c'era», 14 giorni per il PIREP di supporto). **E una richiesta nuova**: non tutti gli eventi hanno un roster, e un evento
   **in presenza** può chiedere materiale portato, cene e attività parallele prenotabili come i simulatori. Diventa M4c
   (§4-bis), con le scelte di forma del design (domande da un elenco chiuso di tipi, attività a turni con posti, dati tenuti 3
   mesi).
7. **Lo spegnimento di `ivao-booking`** (E9). Raccomandato: dopo il primo evento vero fatto sull'hub, con il Gate Manager già
   passato all'hub; poi un 301 verso `/events`. **Deciso: come raccomandato.**

---

## 18. Da portare nel piano

Lo scrive il master dopo il merge (`CLAUDE.md` §0); le note della fase E0 lo ripetono per le loro decisioni.

- **§7, schizzo `evt_`**: le tabelle del §14 al posto dello schizzo; niente FK verso `ref_`; il tipo dal vocabolario;
  niente training ed esami; la prenotazione come riga a sé; niente iscrizioni (§0.6).
- **§8.2, sitemap**: `/events/mine`, `/events/{slug}/roster`, il blocco `events.myEvents` in `/me`; l'evento concluso
  sparisce dal pubblico.
- **§9.2, riga Events**: RFE, RFO, MSE, eventi liberi, online day, eventi in presenza con iscrizione e attività; slot pubblici con rotazioni, privati generati dalla
  capacità; roster ATC proposto dal sistema; PIREP di supporto validati; gli eventi di HQ a mano (l'API non li ha); il Gate
  Manager legge l'hub con un token personale.
- **§9.2 e §12 punto 3**: `ivao-booking` spento dopo il primo evento sull'hub (§17.2 n.7).
- **§9.7, «Collaborazioni tra moduli»**: ATC↔Events — le postazioni si scrivono sull'evento, da `ref_ivao_atc_positions`,
  decise da AOD e staff dei FIR; **award** — il segnale di un evento viene da un PIREP di supporto validato e dalle regole
  dell'evento, non dal calendario.
- **§9.7, «Privacy dei membri»**: il registro di affidabilità dei controllori, per sempre, visibile all'interessato e allo
  staff; chi ha volato senza prenotare solo come numero.
- **§10, tabella dell'API**: `/v2/tracker/sessions` anche senza VID; `/v2/atc/bookings/daily` alla richiesta; niente job ogni
  5 minuti.
- **§13, riga M4**: tre blocchi, M4a (spegne `ivao-booking`), M4b (ATC e dopo evento), M4c (eventi in presenza); «chi
  collabora non cancella» precisato (§6.3); fasi E0–E17 di `10-piano-implementazione-m4.md`.
- **§15, aperte**: scrivere la prenotazione della postazione su IVAO per il controllore, con il suo token conservato
  dall'hub — da studiare con una nota sua (§0.2, §17.2 n.3).
- **§9.1, Award**: la mail a chi assegna (estensione n.5).
- **Nota `il-documento-dice-di-se` §2**: l'uscita programmata di un evento ha la forma dei tour.
- **`CONTRIBUTING.md`**: VID e slug dei test di Events (n.8).
