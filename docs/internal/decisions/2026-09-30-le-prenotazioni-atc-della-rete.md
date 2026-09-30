# Le prenotazioni ATC della rete, lette dal nucleo (E15a)

**Data:** 30 settembre 2026 — fase E15a di M4, PR del nucleo
**Stato:** **scelta tecnica**, per dare forma nel codice a una decisione già presa da Carmine: leggere le prenotazioni delle
postazioni su IVAO **accanto al roster, alla richiesta, in sola lettura** (§17.2 n.3 del design `09-design-m4.md`, decisa sulla
#180; nota `2026-09-29-il-roster-atc` §2.9; estensione n.6 del design §13; piano §10). Nessuna domanda nuova: la forma — una domanda
per finestra, il VID e non il nome, una fixture che il banco ripete ogni giorno — sta dentro quello che è deciso, e le alternative
sono al §4.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: si estende l'unico client di IVAO (`IIvaoApiClient`, `IvaoApiClient` e
`FixtureIvaoApiClient`, con un lettore solo per tutti e due) dentro il perimetro di IVAO (`CLAUDE.md` §3, piano §4.2), e il modulo
degli eventi non chiama IVAO né lo nomina. È una PR del nucleo, prima di E15b che la usa (`CLAUDE.md` §0 regola 6).

## 1. Che cosa serve, e perché il modulo non ne fa a meno

- **E15b** mette le prenotazioni di IVAO accanto al roster (design §4.4, §9.1): chi dei controllori del roster ha anche prenotato la
  sua postazione su IVAO, e chi altro l'ha presa — anche per un esame o un training, che su IVAO occupano la postazione.
- **Il modulo non può chiamare IVAO** (un solo client, piano §4.2) **né nominarlo**: `EventsArchitectureTests` di E2, sul modello di
  `TrainingArchitectureTests`, vieta al modulo il nome della rete (`10`, E2 punto 6). Serve una domanda del nucleo con un nome che
  non nomina IVAO, come `IAirportDirectory` e `IAtcPositionDirectory`, che il training già usa così.
- **Il nucleo non l'ha**: nessuna prenotazione ATC nel client (`10`, E0, «Trovato», punto 11).

## 2. Che cosa si è misurato (30 settembre 2026, con il token vero)

Con uno script usa e getta fuori dal repository, che stampava forme, conteggi e nominativi e mai un nome, un VID o un segreto: circa
370 chiamate in un quarto d'ora, **nessun 429**.

- **Il token**: quello dell'applicazione (`client_credentials`, senza scope: `Ivao:ApiScopes` è vuoto) basta, 200; **senza token,
  401** («not_authenticated»). La documentazione dichiara `oauth2` o `consumer`. Nessuna intestazione di limite di chiamate
  (`x-ratelimit-*`), nessuna cache davanti (`cf-cache-status: DYNAMIC`), una risposta in 50–180 ms.
- **La forma**: `/v2/atc/bookings/daily` risponde **un array nudo**, 20–70 prenotazioni al giorno per tutta la rete (68 il giorno più
  pieno degli ultimi sessanta, 32 KB). Una riga ha `id`, `startDate` e `endDate` (UTC, con la `Z`), `voice` (sempre `true`, 607 righe
  su 607), `training` (`training`, `exam` o `null`), `createdAt`, e **o** `atcPosition` con `atcPositionRef` (una postazione di un
  aeroporto: `id`, `airportId`, `atcCallsign`, `military`, `frequency`, `composePosition`) **o** `subcenter` con `subcenterRef` (un
  settore, con `centerId` al posto di `airportId`) — mai tutte e due, mai nessuna; il nominativo è sempre il `composePosition` del
  riferimento (607 su 607). `user` porta `id` (il VID), `divisionId`, `firstName`, `lastName` e `rating: { atcRatingId }`, che la
  documentazione non dice.
- **Il giorno**: `date=yyyy-MM-dd` (anche con `T00:00:00Z`; con un'altra ora l'elenco è vuoto; una data che non è una data, 400
  «invalid date format»); senza `date` è oggi. **Un giorno elenca ogni prenotazione che lo tocca**: una a cavallo della mezzanotte sta
  nell'elenco di tutti e due i giorni, una che finisce alle 00:00 anche in quello del giorno dopo, una che comincia alle 00:00 **non**
  in quello del giorno prima. L'ordine è quello dell'`id`, non dell'inizio.
- **`position`** è **il principio del nominativo, senza maiuscole**, per gli aeroporti e per i settori: `LICC` dà torre e
  avvicinamento di Cagliari, `LI` tutte le italiane, `licc_twr` la torre, `TWR` niente; una postazione che non esiste, 200 e un array
  vuoto.
- **Quanto indietro e quanto avanti**: lo storico risponde ancora a due anni; avanti, qualche prenotazione fino a tre o quattro
  settimane.
- Anche `/v2/atc/bookings?date=` (a pagine, `perPage` fino a 100) e `/v2/atc/bookings/{id}` rispondono con lo stesso token: non
  servono.

## 3. La forma nel codice

### 3.1 Il client: una chiamata, com'è

- **`IIvaoApiClient.GetDailyAtcBookingsAsync(DateOnly date, string? position = null)`**: il giorno di IVAO così com'è (§2).
  **`null` quando IVAO non si è potuto chiedere** — uno stato che non è 200, la rete che non risponde, un corpo che non è JSON o non è
  un elenco, un token rifiutato —, mai un'eccezione (tranne l'annullamento di chi chiede) e **mai una lista vuota**, che direbbe
  «nessuno ha prenotato». L'interfaccia ha una risposta predefinita, «non disponibile», come `GetAtcPositionsAsync` di A2 di M3: i
  quattro doppi dei test scritti prima continuano a compilare, e nessuno li tocca.
- **`IvaoApiClient`**: la GET con il token dell'applicazione, attraverso `ReadOrNothingAsync`, che serviva già alle postazioni.
  **Nessuna cache**: la leggono in pochi (lo staff di un roster), e una risposta vecchia di un minuto nasconderebbe la prenotazione
  appena fatta.
- **`IvaoAtcBookingReader`** (`IvaoAtcBookings.cs`): **un lettore solo** per il client vero e per quello delle fixture, come quello
  del tracker. Una riga senza nominativo, senza i due orari o senza il VID si salta; una risposta che non è un elenco è `null`. Del
  membro tiene **solo il VID**. I suoi `Text` e `Moment` sono suoi, come negli otto file del nucleo che leggono il JSON di un
  servizio esterno.

### 3.2 La domanda del modulo: `IAtcBookingSource`

- In `Core/Ivao/`, con un nome che non nomina IVAO, registrata con le directory: **`BookedAsync(fromUtc, toUtc, callsign?)`** → le
  prenotazioni che **si sovrappongono** alla finestra, di tutte le postazioni o di una, nell'ordine in cui cominciano; `null` = non
  disponibile.
- **La finestra, non il giorno.** Il modulo ha un evento con un inizio e una fine in UTC. Che IVAO elenchi per giorno di UTC, ripeta
  una prenotazione a cavallo della mezzanotte e prenda una postazione per l'inizio del nominativo, lo sa il nucleo: chiede **ogni
  giorno che la finestra tocca**, e **una prenotazione viene una volta sola** (i doppioni tolti per valore); una postazione chiesta è
  **il nominativo intero**, in qualunque maiuscola (con «LIRR» IVAO darebbe tre settori); una prenotazione che finisce quando la
  finestra comincia non c'è. **Un giorno che non risponde fa «non disponibile» tutta la risposta**: mezza lista direbbe che una
  prenotazione non c'è. Una finestra vuota non chiede niente.
- **Al più sette giorni** (`IAtcBookingSource.MaxDays`), una chiamata per giorno: un evento dura ore, e un Online Day della divisione
  tocca due giorni di UTC. Oltre, la risposta è «non disponibile» con un avviso nel log, **non un'eccezione**: una data sbagliata in un
  evento non deve far cadere la pagina.
- **`AtcBookingDto(Callsign, StartsAt, EndsAt, Vid, Kind)`**, con `AtcBookingKind` `Controlling`, `Training`, `Exam`. **Il VID e non
  il nome**: la pagina nomina un membro dell'hub come nomina chiunque (`personName`), e chi non è mai entrato nell'hub con il suo
  numero; i nomi che IVAO dà di chi non è dell'hub non arrivano al modulo. Niente divisione, rating, frequenza né `voice`: nessuna
  fase li chiede.

### 3.3 Lo strumento e la fixture

- **`tools/record-ivao-fixtures.mjs --bookings <nome> <asVid> <yyyy-mm-dd> <prefisso…>`**: il giorno di IVAO, **le prenotazioni che
  cominciano quel giorno** sulle postazioni dei prefissi (per principio, come IVAO); **la persona tolta**: ogni membro diventa
  `asVid`, `asVid + 1`… nell'ordine in cui compare, e l'oggetto `user` tiene solo quel numero; il resto com'è. Stampa i VID usati.
- **`atc-bookings-day.json`**, registrato il 30 settembre con `--bookings day 761070 2026-07-27 LIRF LIMC LIBD LIBG LFPG LIRR LIMM
  LIBB LFFF SBGR_TWR EDDF_APP`: il 27 luglio 2026 sulle stazioni del banco (le postazioni di `atc-positions-world.json` e
  `subcenters-world.json`), più l'esame di `EDDF_APP` e `SBGR_TWR` dalle 23 all'1. Dieci prenotazioni, cinque su un settore, dieci
  persone: **VID 761070–761079**, quelli di E15a (liberi al grep).
- **`FixtureIvaoApiClient`: un giorno che si ripete**, come `whazzup.json`. Il giorno chiesto, qualunque sia, riceve le prenotazioni
  registrate spostate su di lui, più quella a cavallo della mezzanotte del giorno prima; con la regola del giorno misurata (§2) e la
  `position` per principio. Così sul banco senza credenziali un evento di qualunque data ha delle prenotazioni accanto al roster
  (E15b): la torre di Fiumicino dalle 18 alle 20, tre settori di Roma, Malpensa, Parigi.

### 3.4 I test

`AtcBookingTests` (unità, nuovo): il lettore sul giorno registrato (e che del membro resti solo il numero) e sulle righe strane; il
client vero contro un IVAO finto — la richiesta con il giorno, la postazione e il token dell'applicazione; sei risposte sbagliate, IVAO
irraggiungibile e il token rifiutato rispondono «non disponibile» —; il client delle fixture su un giorno qualunque; la sorgente presa
dalla DI del nucleo: la finestra, la mezzanotte una volta sola, il nominativo intero, un giorno che non risponde, la settimana e la
finestra vuota. Nessuna chiamata a IVAO.

## 4. Alternative scartate

| Alternativa | Perché no |
|---|---|
| Il modulo che chiama `IIvaoApiClient`, come i tour | il modulo non nomina la rete (E2, `EventsArchitectureTests`, come il training) |
| Una domanda per giorno (`il giorno, la postazione`), come dice il piano alla lettera | il modulo dovrebbe sapere che IVAO elenca per giorno di UTC e ripete le prenotazioni a cavallo della mezzanotte: conoscenza di IVAO fuori dal perimetro, ricopiata da ogni chiamante. «Le prenotazioni di un giorno per una postazione» (`10`, «Fatta quando») sono la finestra di quel giorno |
| La `position` di IVAO passata al modulo com'è | «LIRR» darebbe tre settori a chi ne ha chiesto uno |
| Un elenco di nominativi nella domanda | il modulo chiede la finestra e tiene le sue postazioni con un filtro; una chiamata per giorno basta per tutte |
| Un job che le copia, o una cache | le prenotazioni si leggono alla richiesta (nota `2026-09-28-i-job-quando-passenger-spegne-l-hub` §8, design §10.2); lo staff di un roster è poco, e una copia nasconderebbe la prenotazione appena fatta |
| I nomi di chi ha prenotato | quelli di IVAO di chi non è entrato nell'hub non servono al roster; l'hub nomina i suoi membri da sé |
| L'`id` di IVAO nel DTO | serviva solo a togliere i doppioni della mezzanotte, che il nucleo toglie per valore; un modulo non ci costruisce un link a IVAO |
| Un'eccezione oltre i sette giorni | una data sbagliata in un evento farebbe cadere la pagina |
| Una fixture per data | il banco mostrerebbe delle prenotazioni solo nei giorni registrati, già passati |
| Tutto il giorno della rete nella fixture | una settantina di persone da togliere, per dieci VID dei test; le stazioni del banco bastano |
| Gli aiuti di lettura del JSON messi in comune | otto file del nucleo hanno i loro (`IvaoTracker.cs`, `IvaoAtcPosition.cs`, `IvaoWhazzup.cs`, `NoaaWeatherClient.cs`…); metterli insieme è un'altra PR |

## 5. Che cosa si tocca

Tutto del nucleo, ed è il perché di questa nota (`core-guard`):

- **`src/IvaoHub.Core/Ivao/`**: `IIvaoApiClient.cs`, `IvaoApiClient.cs`, `FixtureIvaoApiClient.cs`, `IvaoServiceCollectionExtensions.cs`
  (la registrazione); nuovi `IvaoAtcBookings.cs` (il lettore) e `AtcBookingSource.cs` (la domanda, il DTO, l'implementazione).
- **`tools/record-ivao-fixtures.mjs`** (la modalità `--bookings`), **`tests/fixtures/ivao/atc-bookings-day.json`** (nuovo) e
  **`tests/fixtures/ivao/README.md`** (la sua sezione).
- **`tests/IvaoHub.UnitTests/AtcBookingTests.cs`** (nuovo). Nessun test del maintainer cambiato, nessuna migrazione, nessun endpoint:
  la schermata è di E15b.

## Da portare nel piano

- **§10**, la riga «Prenotazioni ATC dell'evento»: misurato (§2) — il token dell'applicazione basta, senza scope; il giorno elenca ogni
  prenotazione che lo tocca, e `position` è il principio del nominativo; la domanda del nucleo è **`IAtcBookingSource`** (una finestra,
  al più sette giorni, `null` = non disponibile); nessuna cache.
- **§4.2**, il perimetro di IVAO: `IAtcBookingSource`, in `Core/Ivao/`, è una domanda che un modulo fa senza nominare la rete, come le
  directory.
