# I controlli sulle tracce e le tarature (T18)

**Data:** 24 settembre 2026 — fase T18 di M2
**Stato:** **decisa** (Carmine, 24 settembre 2026: quattro risposte in apertura, §2; il resto è design già scritto o scelta tecnica
dichiarata qui sotto)
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: i controlli sono nel design (`05-design-m2.md` §6.4) e nella nota
`2026-09-24-i-controlli-dai-pirep-veri`; il motore è quello di T17 (`IFlightCheck`), che T18 estende con un contesto più ricco. Due decisioni
di forma (il parametro di `maxAltitude`, l'esenzione non online) e le due tarature che il piano voleva decise con Carmine.

## 1. Che cosa serve

Piano di implementazione, parte C, T18: `disconnections`, `parking`, `speed250` con le esenzioni, `simRate`, `landingAtArrival`,
`takeoffFromThreshold`, `vmc`, `maxAltitude`; e le tarature sul corpus — `thresholdToleranceMeters` secondo il campionamento misurato in
T2, `durationFactor` e `durationFixedMinutes` confrontando la stima con la durata dei voli. «Fatta quando»: il corpus dà gli esiti attesi e
i numeri delle tarature sono decisi.

## 2. Le risposte di Carmine

| Domanda | Risposta | Scartato |
|---|---|---|
| Che forma ha il parametro di `maxAltitude`? | **Un limite per regola di volo**, come `equipment`: `maxFeetI|V|Y|Z`, si parte da **V 19 500 ft** e **I, Y, Z 66 000** (i numeri del sistema di oggi). Y e Z prendono il limite IFR: dove cambiano le regole lo sa solo la rotta, che legge l'agente. | Un solo limite per regola (una regola generale non distinguerebbe VFR e IFR). |
| Un'esenzione `FreeSpeed` su una posizione che l'archivio **non ha visto online** (`NotOnline`) ammorbidisce `speed250`? | **No**: ammorbidiscono solo `Online` e `Unverifiable`. Con `NotOnline` il controllo fallisce come senza esenzione, e l'evidenza lo dice. | Ammorbidire sempre, lasciando lo stato alla sezione ATC. |
| `thresholdToleranceMeters` | **150 m, si tiene** (§4.1). | 300 m (un'intersezione vicina alla testata passerebbe), 100 m (segnalerebbe anche LFKF). |
| `durationFactor` e `durationFixedMinutes` | **5 % e 15 minuti** (erano 5 % e 20; §4.2). | Invariati (+4 minuti in media); 1 % e 18 (il miglior adattamento, ma su 14 voli e quasi senza parte proporzionale). |

## 3. Com'è fatto

- **Il contesto** (`FlightChecks.ContextAsync`) aggiunge al T17 quattro cose, ognuna vuota se manca (e un controllo che non ha i dati dice
  «non disponibile», mai «fallito»): le **posizioni degli aeroporti** del PIREP (`IAirportDirectory`), le **piste** (`IRunwayDirectory`,
  già scaricate all'invio), i **METAR** tenuti in `fo_weather_reports` nella finestra del volo (T16) e le **esenzioni** dichiarate, con lo
  stato e i controlli che ammorbidiscono come l'invio li ha congelati. E l'aeroporto di deviazione, perché il primo volo di una deviazione
  deve atterrare lì.
- **Il decollo e l'atterraggio** sono quelli dell'invio (`TrackedFlight`): il primo punto in volo, e il primo a terra dopo l'ultimo in volo
  — così un **touch and go** lungo la strada non è l'atterraggio (tre voli VFR del corpus ne hanno uno, a circa 30 NM dall'arrivo).
- **Una riga per volo**, come i controlli sul piano; un volo senza traccia dice «traccia non tenuta». Un controllo fallisce se una riga
  fallisce, è non disponibile se nessuna riga ha potuto giudicare, passa altrimenti.

| Chiave | Che cosa guarda |
|---|---|
| `disconnections` | Un buco fra due punti **oltre un minuto** (il tracker campiona ogni 15 s circa, 40 al massimo sul corpus) con almeno un estremo in volo; quelli a terra si mostrano e non contano. La più lunga contro `maxSingleDisconnectMinutes`, la somma contro `maxTotalDisconnectMinutes`. Una sessione **finita in volo** fallisce. |
| `parking` | Fermo (≤ 2 kt, a terra) dal primo punto al primo che si muove, e dall'ultimo che si muove all'ultimo punto; contro `minParkingMinutesBefore` e `minParkingMinutesAfter`. Senza atterraggio il dopo non si misura. |
| `speed250` | Sotto 10 000 ft, in volo: la **velocità indicata stimata** dalla velocità al suolo nell'atmosfera standard, senza vento (come il validatore Python quando non ha il vento), contro 250 + `toleranceKt`. I **1 000 ft appena sotto FL100** si mostrano e non falliscono mai (l'accelerazione passato il livello, come il validatore Python). L'evidenza dice il punto peggiore e che la stima è senza vento. |
| `simRate` | Per ogni finestra di **cinque minuti** in volo, la **mediana** del rapporto fra la velocità fra le posizioni e quella riportata; la peggiore contro 1 + `tolerancePercent`. La mediana perché il primo punto dopo una riconnessione salta (880159: 1264 kt per un campione). Un rate più lento non si guarda: non guadagna niente. |
| `maxAltitude` | La quota più alta volata contro `maxFeet` delle regole del piano al decollo. |
| `landingAtArrival` | Il punto dell'atterraggio entro `radiusNm` dalla posizione dell'aeroporto **o da una delle sue testate** (l'arrivo della leg, o la deviazione per il primo volo di una deviazione). Senza atterraggio fallisce; senza posizione né piste è non disponibile. |
| `takeoffFromThreshold` | La pista è quella con la prua più vicina a quella al distacco (entro 30°) e **sul cui asse** sta la corsa (entro 150 m: le piste parallele); la corsa comincia all'ultimo punto sotto i 30 kt prima del distacco, **riportato indietro** di v²/2a con l'accelerazione del campione dopo. Oltre `thresholdToleranceMeters` **non fallisce mai**: dice «decollo da un'intersezione». |
| `vmc` | Solo le parti VFR: partenza e arrivo di un piano V, l'arrivo di un Y, la partenza di uno Z; un piano I lo dice e passa. Il METAR tenuto **più vicino** al decollo o all'atterraggio, entro un'ora; il **ceiling** è lo strato BKN, OVC o VV più basso (FEW e SCT non lo fanno), contro `minCloudBaseFeet`, e la visibilità contro `minVisibilityMeters`. Un'estremità senza METAR non è disponibile; se nessuna lo è, il controllo non è disponibile. |

- **Il METAR si legge nel modulo** (`MetarReading`): visibilità in metri o in miglia (`1 1/2SM`, `P6SM`), `CAVOK`, `9999`, strati e `VV`,
  fino a `TEMPO`, `BECMG`, `NOSIG` o `RMK`. Il nucleo continua a non interpretare i bollettini (T16).
- **Le esenzioni**: un'esenzione che ammorbidisce `speed250` (oggi solo `FreeSpeed`) lo fa **passare** con una riga che la nomina, e
  nessun errore suggerito; con lo stato `NotOnline` (§2) fallisce, e l'evidenza lo dice.
- **Il catalogo** ha la chiave `maxAltitude` fra `simRate` e `takeoffFromThreshold`, con i quattro campi; la metà TypeScript la stessa
  (`CHECK_KEYS`, `CHECK_PARAMETERS`). Nessuna migrazione: i parametri stanno nel JSON della regola.
- **Le fixture nuove**, dati pubblici, nessun VID: `airports-corpus.json` (24 aeroporti del corpus, posizione e testate da
  `/v2/airports/{icao}` e `/runways`, con `tools/record-ivao-fixtures.mjs --airports corpus …`, un modo nuovo dello strumento) e
  `metars-corpus.json` (i 25 METAR che NOAA teneva ancora dei tre voli VFR; Pisa, Lošinj e Trento non ne pubblicano).

## 4. Le tarature

### 4.1 La testata

Con i punti ogni 15 secondi l'aereo **quasi mai è colto fermo sulla pista**, e un decollo senza fermarsi mai: la «posizione ferma prima
della corsa» del design (§6.4) su tre voli era sul raccordo, a un chilometro. Il controllo parte quindi dall'ultimo punto sotto i 30 kt e lo
riporta indietro della distanza che l'aereo ha fatto per arrivare a quella velocità. Sui 15 voli, in metri dalla testata IVAO:

| PIREP | Pista | m | PIREP | Pista | m |
|---|---|---|---|---|---|
| 879691 | LIMP 02 | 0 | 877196 | LICR 33 | 0 |
| 877596 | EGJJ 26 | 0 | 880159 | LICR 33 | 0 |
| 881169 | LIBP 22 | 0 | 876413 | LIPE 30 | 0 |
| 877464 | EGLC 27 | 3 | 881923 | LOWI 08 | 10 |
| 880760 | LIPN 26 | 36 | 881263 | LDDU 11 | 56 |
| 882171 | LJPZ 15 | 85 | 879558 | LFKF 05 | 99 |
| **877187** | **LFPM 10** | **255** | **879788** | **LIPB 19** | **343** |
| **879610** | **LIEO 05** | **445** | | | |

Dodici entro 100 m, tre oltre 250: 150 m li separa. Se quei tre siano intersezioni vere o testate che IVAO mette altrove non lo si sa da
qui: è esattamente ciò che la riga dice al validatore di guardare.

### 4.2 Il tempo stimato

Il tempo **in volo** dei 15 voli (dal distacco all'atterraggio), tolto 877196 (sim rate), contro `60 × distanza × (1 + k) / TAS + c`, con la
distanza fra il punto di distacco e quello di atterraggio e una **TAS nominale per tipo** come la scriverebbe il FOD nei profili (B350 300,
C152 100, C208 170, F26T 180, B737 430, B736 440, C700 470, B738 450, A320 450, A20N 450, F28 420). Le velocità depositate dai piloti
non servono: `K0095` per un C152, `N0283` per un B736.

| k, c | Scarto medio | Errore medio | Errore massimo |
|---|---|---|---|
| 5 %, 20 min (prima) | +4,3 min | 7,7 min | 15 min |
| **5 %, 15 min (scelto)** | **−0,7 min** | **7,6 min** | **14 min** |
| 1 %, 18 min (miglior adattamento) | +0,1 min | 7,5 min | 13 min |

Gli errori più grandi sono le tratte VFR corte (+15 minuti su 37–43 NM): la parte fissa pesa di più dove il volo è breve. `durationFactor`
resta 0,05; `durationFixedMinutes` parte da **15**. Un'installazione che ha già salvato le impostazioni tiene il suo numero.

## 5. Il corpus

Con i valori di partenza di ogni controllo, i controlli sulle tracce danno gli esiti della nota `2026-09-24-i-controlli-dai-pirep-veri` §5:

| PIREP | Non passano | Numeri |
|---|---|---|
| 877464 | `parking`, `speed250` | fermo 0,8 min prima; circa 322 kt indicati a 4 800 ft |
| 877596 | `speed250` | circa 343 kt a 8 100 ft, già oltre 260 a 5 700 (i 345 a 9 200 stanno nella fascia) |
| 877187 | `speed250` | circa 302 kt a 1 400 ft, dopo il decollo (il pilota ha dichiarato un'emergenza) |
| 877196 | `simRate` | circa quattro volte la velocità riportata per diversi minuti |
| 880159 | nessuno | due disconnessioni in volo di 1,7 e 1,1 minuti, sotto i 15 e i 25; atterrato entro 0,6 NM da Parma |
| gli altri dieci | nessuno | il più veloce sotto FL100 fuori dalla fascia è 255 kt (881263); 876413 fa 259 a 9 500 ft, nella fascia |

`parking` a **2 minuti** (il valore di partenza) tiene 877596 (2,3 minuti prima) e prende 877464: i 3 minuti del sistema di oggi li
avrebbero presi tutti e due, e i controllori ne hanno segnalato uno solo.

## 6. Trovato strada facendo

- **IVAO mescola le unità della lunghezza delle piste**: Parma 2124 e Figari 2480 sono metri, Portorož 3937, London City 4948 e
  Innsbruck 6562 sono piedi. `IvaoRunway.LengthMetres` li prende tutti per metri. T18 non legge la lunghezza (solo testate e prue), ma
  il filtro della pista più lunga dei tour `Open` sì: da guardare a parte.
- Le testate IVAO di alcune piste non coincidono con l'inizio pavimentato: a Parma la corsa comincia 230 m **prima** della testata IVAO.
  Il controllo tratta «prima della testata» come zero.

## 7. Correzioni ad altri documenti

- Design §1.5 e §1.11: `durationFixedMinutes` parte da 15. §6.4: `maxAltitude` per regola di volo, e come leggono le tracce
  `speed250`, `simRate`, `takeoffFromThreshold`, `vmc`.
- `tests/fixtures/ivao/README.md`: le due fixture nuove.

## 8. Che cosa non si è verificato

- **La stima della velocità indicata è senza vento**: un vento in coda di 40 kt a 9 000 ft fa sembrare 285 kt un aereo che ne indica 250.
  Sul corpus non ha creato falsi positivi; la riga lo dice sempre al validatore. Il vento in quota non c'è in nessuna fonte che l'hub usa.
- **La quota** è quella che il tracker dà (altitudine, non livello di volo): intorno a FL100 con un QNH alto o basso la differenza è di
  qualche centinaio di piedi, e la fascia dei 1 000 ft la assorbe.
- Le tre partenze oltre 150 m (LFPM, LIPB, LIEO) non sono state confrontate con le carte.
- `vmc` usa il **ceiling** (BKN, OVC, VV) come «base delle nubi»: è la lettura delle VMC per il decollo e l'atterraggio; uno strato SCT
  basso non fa fallire.
- La taratura del tempo stimato è su 14 voli, con TAS scelte da me: il FOD scriverà le sue.
- Il controllo sui voli veri è provato solo sul corpus; sul banco gira con il volo registrato del giro `tours-review`.
