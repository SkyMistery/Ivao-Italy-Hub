# Il rating preferito, il minimo di una postazione e le postazioni per nominativo (E10c)

**Data:** 30 settembre 2026 — fase E10c di M4, PR del nucleo (#204)
**Stato:** **decisa** (Carmine, 30 settembre 2026, in chat al master, che ha pubblicato le risposte sulla #204 su sua istruzione:
[risposte][ok204]). Le due domande di §5 erano sulla PR ([domande][q204]): **il minimo di una postazione è il suo FRA su IVAO**, come
raccomandato, **con un'aggiunta** (§3.2: chi fa i turni può mettere qualcuno sotto l'FRA, e l'hub gli dice di togliere l'FRA su
IVAO per quel controllore — un avviso, mai un rifiuto); **il rating preferito** è AS3 su `DEL`, APC su `DEP`, **ADC su `FSS`**,
accanto a TWR e GND ADC, APP APC, CTR ACC; `ATIS` resta senza, come raccomandato. **Dopo la revisione** ([rilievi][r204]) Carmine ha
deciso, il 1° ottobre 2026, in chat al master che l'ha pubblicato su sua istruzione ([seconda risposta][ok204b]), che **i rating
preferiti stanno in `config/division.json`**: sono la regola della divisione, non di IVAO (§3.1). Le postazioni per nominativo (§3.3) e
la forma nel codice sono una scelta tecnica.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: si estendono il vocabolario dei rating (`Core/Ivao/RatingVocabulary.cs`, A1 di
M3), la directory delle postazioni (`Core/Ivao/AtcPositionDirectory.cs`, A2 di M3), la sincronizzazione dei dati di riferimento di
IVAO (`RefDataSyncJob`, come le postazioni in A2), nel perimetro IVAO del nucleo, e il file della divisione (`DivisionOptions`), come
ogni regola di una divisione (`CLAUDE.md` §3). Il modulo non ne scrive una copia sua. È una PR del nucleo, prima di E11a ed E11b che
la usano (`CLAUDE.md` §0 regola 6). Design `09-design-m4.md` §1.13, §4.1, §4.3, §4.4, §13 n.4; nota `2026-09-29-il-roster-atc` §2.2.

[ok204]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/204#issuecomment-5916282164
[q204]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/204#issuecomment-5915876615
[r204]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/204#issuecomment-5926667625
[ok204b]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/204#issuecomment-5926811688

## 1. Che cosa serve, e perché il modulo non ne fa a meno

- **Il proponente del roster** (E11b, design §4.3) chiede per ogni turno **chi può** — almeno `minimumAtcRating` e almeno **il
  minimo della postazione** — e **chi prima**: chi ha **il rating preferito** per il tipo della postazione. Il modulo non scrive
  numeri né nomi di rating (nota `il-roster-atc` §2.2): le due risposte sono del nucleo (design §1.13, estensione n.4). Il
  vocabolario sapeva soltanto su quale tipo si allena un rating (`Rating.PositionType`: ADC `TWR`, APC `APP`, ACC `CTR`), niente di
  `GND`, `DEL`, `DEP`, `FSS` e `ATIS`, e nessun minimo di una postazione (`10`, E0, «Trovato», punto 8).
- **La scheda ATC dello staff** (E11a, design §4.1) sceglie le postazioni dell'evento dall'elenco del nucleo e copia sulla riga il
  **FIR** (`IHasFir`), che fa vedere a un capo FIR le postazioni del suo FIR. La directory rispondeva solo «le postazioni su cui si
  allena questo rating» (`ForRatingAsync`), senza il tipo e senza una ricerca per nominativo (`10`, E0, «Trovato», punto 9).

## 2. Da dove vengono le regole

Letto e misurato il 30 settembre 2026, con il token dell'applicazione (`client_credentials`, lo stesso della sincronizzazione).

- **Le «Regulations» di IVAO** ([ATC Operations, A.1][regs]) dividono le postazioni per servizio: il **controllo d'aerodromo** sono
  le `DEL`, le `GND` e le `TWR` (A.1.1); il **controllo terminale** le `APP` e le `DEP`, e le `TWR` che hanno anche una TMA (A.1.2);
  il **controllo d'area** le postazioni d'area, i `CTR` (A.1.3); le **FSS** sono un servizio a sé (A.1.4). `ATIS` non c'è: non è
  una postazione di controllo. I tre rating con un training pratico portano il nome di uno dei tre servizi di controllo —
  **Aerodrome Controller** (ADC), **Approach Controller** (APC), **Centre Controller** (ACC), i nomi di `ratings.Atc.*`.
- **Un rating «preferito» IVAO non lo scrive da nessuna parte**: è la regola dell'ED, di Carmine — TWR e GND l'ADC, APP l'APC, ACC
  l'ACC (c1, design §R.3 e §17.1 n.9) — e, per gli altri quattro tipi che IVAO elenca, la sua risposta sulla #204: AS3 su `DEL`, APC
  su `DEP`, ADC su `FSS`, nessuno su `ATIS`. La raccomandazione era ADC su `DEL` e nessuno su `FSS`, dalle definizioni di IVAO: ha
  deciso diversamente, e i valori sono i suoi. **Essendo la regola di una divisione, sta nel file della divisione** (seconda
  risposta di Carmine sulla #204, dopo il rilievo 1 del revisore): un'altra divisione la scrive diversa senza toccare il nucleo.
- **Il minimo di una postazione è il suo FRA** (*Facility Rating Assignment*): la regola di IVAO su chi può connettersi a una
  postazione — un rating minimo, per giorni della settimana e ore oppure per una data, attiva o spenta —, più le righe **per
  membro**, che fanno un'eccezione per una persona. Gli FRA delle postazioni di una divisione li decide la divisione; quelli delle
  postazioni fuori dalle divisioni HQ (A.3.1), con una [policy per tipo][hq] (AS2 `DEL` e `GND`, AS3 `TWR`, ADC `APP`, APC `CTR`)
  **che dentro una divisione non vale**. Sono nell'API `core`, `/v2/fras` ([documentazione][api]), aperta al token
  dell'applicazione: A2 di M3 li aveva visti e non letti («dicono chi si connette, non dove si allena un rating»).
- **Gli FRA dell'Italia** (`/v2/fras?countryId=IT&expand=true`: 4 pagine da 100 righe, 0,3–0,4 secondi e 58 KB l'una):
  - **392 righe**: **353 per postazione**, su **202 postazioni** (la directory ne conta circa 230 in Italia, A2), e **39 per
    membro** — tutte eccezioni, senza un minimo —; nessuna lista nera (`isBlacklist`); 381 attive, 11 spente.
  - **I minimi** (il numero di IVAO: 2 AS1, 3 AS2, 4 AS3, 5 ADC, 6 APC, 10 CAI): AS1 21 righe, AS2 16, AS3 75, **ADC 181**, APC
    40, **CAI 20** — le postazioni chiuse a chi non è CAI: i planner, un settore di Roma, alcuni avvicinamenti (tre di Roma), la
    delivery di Malpensa, e le `_I_TWR`, con la riga spenta. Per tipo: `TWR` da AS1 a CAI, `GND` da AS1 ad ADC, `DEL` da AS3 a
    CAI, `APP` da AS3 a CAI, `CTR` ADC, APC o CAI, `FSS` ADC o CAI. **Nessun minimo per tipo**, quindi: ogni postazione ha il suo.
  - **Il minimo cambia con l'ora e con il giorno**: 114 postazioni hanno più righe, quasi sempre giorno e notte (`LIBD_TWR` AS2
    dalle 08 alle 23, ADC dalle 23 alle 08) o feriali e fine settimana (`LIMC_ANE_APP` ADC fino alle 17 nei feriali e fino alle 12
    il sabato e la domenica, APC dopo).
  - **Cinque righe hanno una data**, tutte di settembre: due chiudono due avvicinamenti per una sera (CAI, 18:30–21:30), tre aprono
    una postazione a una persona per qualche ora — l'eccezione che lo staff dà per un evento o per un esame.
  - **La forma di una riga**: `id`, `userId` (e il suo doppione `user_id`: vuoti nelle righe per postazione), `atcPositionId` o
    `subcenterId` (gli identificativi di IVAO, non il nominativo: il nominativo arriva con `expand=true`, nella postazione espansa,
    `composePosition`), `minAtc`, `startTime` ed `endTime` come `23:00:00` (la documentazione scrive `23:00`), `dayMon`…`daySun`,
    `date` come `2026-09-12` (la documentazione la dice una data con l'ora), `active`, `isBlacklist`.

[regs]: https://wiki.ivao.aero/en/home/ivao/regulations
[hq]: https://wiki.ivao.aero/en/home/atcoperations/HQAirspace/FRAPolicy-AreasOutsideDivisions
[api]: https://api.ivao.aero/docs/core-json

## 3. Le decisioni, e la forma nel codice

### 3.1 Il rating preferito, nel file della divisione; la risposta, dal vocabolario

- **La regola è della divisione**: **`config/division.json → preferredAtcRatings`**, una mappa dal tipo di postazione, come IVAO lo
  scrive, alla sigla del rating ATC che un controllore deve avere almeno (`DivisionOptions.PreferredAtcRatings`). Un tipo lasciato
  fuori non ha nessuno per primo; vuota — il predefinito, e il fork «XX» — non mette nessuno per primo da nessuna parte. **I valori
  di Carmine** nel file di IT: **AS3** su `DEL`; **ADC** su `FSS`, `GND`, `TWR`; **APC** su `APP`, `DEP`; **ACC** su `CTR`; **nessuno**
  su `ATIS` — su un tipo senza preferito il proponente non divide i candidati con questo criterio, e valgono gli altri.
  `config/division.example.json` spiega la chiave a chi forka, con la stessa mappa come esempio.
- **Controllata all'avvio** contro quello che il nucleo sa di IVAO: un tipo che IVAO non usa (`IvaoAtcPosition.Kinds`: `DEL`, `GND`,
  `TWR`, `APP`, `DEP`, `ATIS`, `CTR`, `FSS`, misurati in A1 e A2) o un rating che non è della scala ATC del vocabolario **fermano
  l'avvio** con un messaggio che dice il file, la chiave e i valori possibili (`PreferredAtcRatingsValidator`, in `Core/Ivao/`,
  accanto a `DivisionOptionsValidator`, perché le due liste sono di IVAO). In qualunque maiuscola.
- **La risposta resta del nucleo**: il vocabolario che il nucleo registra è la scala di IVAO **con la mappa della divisione**
  (`IvaoRatings.WithPreferred`), e risponde **`PreferredFor(tipo)`**: il rating che è il preferito su quel tipo, in qualunque
  maiuscola; nessuno per un tipo che la divisione non nomina. «Ha il rating preferito» è `IsAtLeast` con quel rating: un SEC è
  preferito dove lo è l'ADC. Il modulo non nomina un rating. `IvaoRatings.Vocabulary`, la scala di IVAO da sola, non mette nessuno
  per primo; il costruttore del vocabolario rifiuta un rating che non ha, come un numero scritto due volte.
- `PositionType` resta com'è: dice **su che cosa si allena** un rating, una domanda del training, e resta nel vocabolario (è come
  funziona il training di IVAO). L'ADC si allena sulla `TWR` ed è il preferito anche su `GND` e `FSS`; l'AS3 non si allena su niente
  ed è il preferito su `DEL`: sono due dati, non uno.
- ⚠️ Una `TWR` che ha anche una TMA (A.1.2) resta una `TWR`: il tipo è quello che IVAO pubblica, e la TMA nei dati non c'è.

### 3.2 Il minimo di una postazione: gli FRA, nella directory

- **Il minimo di una postazione è quello dei suoi FRA su IVAO**, e lo dice la **directory**, non il vocabolario: è un dato di una
  postazione, che la divisione cambia e che cambia con l'ora (§2), non una regola di un tipo. **Scostamento dal design §1.13**, che
  lo metteva nel vocabolario pensando a una regola per tipo che non esiste.
- **Gli FRA per postazione della divisione** — mai quelli per membro: `members=false`, e il lettore salta comunque una riga che
  nomina qualcuno, quindi **nessuna persona entra nell'hub** — in una tabella di riferimento, **`ref_ivao_fras`** (la chiave di IVAO,
  il nominativo, il minimo, i giorni come bit di `DayOfWeek`, l'inizio e la fine come ore del giorno, la data, «attiva», la riga
  grezza, `synced_at`; un indice sul nominativo), con la migrazione additiva **`AddIvaoFras`** del contesto del nucleo, scritta dopo
  quella di E10d (`AddAwardSignalNotifiedAt`: le due fasi migrano lo stesso contesto, e vanno in fila).
- **Da IVAO**: `IIvaoApiClient.GetFrasAsync(paese)`, con un'**implementazione predefinita** che risponde «nessuna risposta», come le
  postazioni di A2 (i doppi dei test del maintainer non cambiano); il client vero legge
  `/v2/fras?countryId=…&members=false&expand=true&perPage=100` **pagina per pagina**, mai un'eccezione (come le postazioni). **Due
  risposte diverse** (rilievo 2 del revisore sulla #204): **`null` = IVAO non ha risposto** — una pagina che fallisce, anche a metà
  (metà degli FRA poterebbe l'altra metà), una risposta che non è una pagina, oltre 50 pagine —; **una lista vuota = la divisione
  non ha FRA**. **Un lettore solo** per i due client (`IvaoFraReader`, `Core/Ivao/IvaoFra.cs`); il client delle fixture, senza il file
  di quel paese, non risponde.
- **Ogni notte**, nella sincronizzazione dei dati di riferimento, dopo le postazioni e nello stesso salvataggio: upsert per chiave
  di IVAO, e potatura delle righe che IVAO non elenca più. **Senza risposta la tabella resta com'è; con una lista vuota si svuota**:
  diversamente dagli altri snapshot (centri, aeroporti, aerei), dove una risposta vuota non è mai vera, una divisione può togliere
  tutti i suoi FRA, e quelli vecchi non devono alzare i minimi per sempre. Il conteggio, o «nessuna risposta», va **nel messaggio**
  del giro, non nel suo esito: una divisione può non avere FRA, e un giro «partial» ogni notte insegnerebbe a non leggerlo più.
- **La risposta**: `IAtcPositionDirectory.MinimaAsync(nominativi)` → per ogni nominativo un **`AtcPositionMinimum`**, che dice
  **`Over(da, a)`**: il numero di IVAO del rating più alto fra gli FRA **attivi** della postazione che valgono in qualche momento
  della finestra — quelli con una data in quella data, gli altri nei loro giorni, anche oltre la mezzanotte (una fine che non viene
  dopo l'inizio è del giorno dopo; `00:00`–`00:00` è il giorno intero) —, perché chi è sotto sarebbe rifiutato per una parte del
  turno. **Nessun FRA che vale, nessun minimo** (null): vale `minimumAtcRating`, e il proponente prende il più alto dei due. Il
  numero si confronta con `IsAtLeast`, per cui un rating che il vocabolario non conosce non è almeno niente.
- **L'aggiunta di Carmine**: chi fa i turni **può** mettere un controllore sotto il minimo — lo staff toglie l'FRA a quella persona
  su IVAO —, e l'hub **glielo dice**, con l'istruzione di togliere l'FRA della postazione su IVAO per quel controllore: **un avviso,
  mai un rifiuto** (design §4.4, la correzione; anche la cessione di un turno, §4.4-bis). Il proponente invece non va mai sotto: è
  la regola di «chi può». Il nucleo dà il minimo; **l'avviso lo mostra il modulo** (E11b, E12), e le sue parole — nominano IVAO e
  il suo FRA, quindi stanno nel perimetro IVAO (`CLAUDE.md` §3) — sono una chiave del nucleo, `atcPositions.belowMinimum` in
  `locales/*/common.json`, che il modulo usa così com'è.
- ⚠️ **Il fuso degli orari degli FRA** si legge **UTC**, come ogni orario di IVAO (le prenotazioni ATC, il tracker, il sito della
  divisione scrivono UTC): la documentazione non lo dice e nessuna fonte pubblica lo mostra. Non verificato.
- ⚠️ **Come IVAO combina due righe che valgono insieme** non è scritto: il più alto, deciso da Carmine, è anche la lettura prudente
  (le due righe con una data misurate alzano il minimo).

### 3.3 Le postazioni della divisione per nominativo, nella directory

- **`OfDivisionAsync()`**: tutte le postazioni della divisione, per nominativo: l'elenco da cui lo staff sceglie quelle di un evento
  (E11a), militari e ATIS compresi — quali aprire lo decide lo staff. Nessuna cache né endpoint, come `ForRatingAsync`: il modulo lo
  chiede dal suo, e cambia una volta per notte.
- **`FindAsync(nominativi)`**: le postazioni della divisione fra quei nominativi, per nominativo in qualunque maiuscola, a lotti come
  `IAirportDirectory.FindAsync`; un nominativo che IVAO non elenca, o di un'altra divisione, semplicemente non c'è. È il controllo del
  form di E11a, con la copia del FIR, e la domanda del proponente.
- **`AtcPositionDto` porta il tipo** (`Type`) accanto a nominativo, nome, aeroporto e FIR: il modulo lo passa al vocabolario
  (`PreferredFor`) senza nominarlo. Lo porta anche `ForRatingAsync`: il training legge nominativo, nome, aeroporto e FIR, e non
  cambia.
- **«Della divisione»** come in A2 (`FirDirectory`): una postazione d'aeroporto se l'aeroporto è del paese della divisione, un
  settore se il suo FIR è della divisione. Una query sola per le tre domande.

## 4. Alternative scartate

| Alternativa | Perché no |
|---|---|
| Nessun minimo della postazione, solo `minimumAtcRating` | meno codice, ma il roster proporrebbe turni che IVAO non lascia aprire: `LIBD_TWR` di notte a un AS3, gli avvicinamenti di Roma chiusi a chi non è CAI |
| Un minimo per tipo nel vocabolario (la policy di HQ: AS2 `DEL` e `GND`, AS3 `TWR`, ADC `APP`, APC `CTR`) | è la regola delle postazioni fuori dalle divisioni; in Italia ogni postazione ha il suo FRA, che cambia con l'ora |
| Il minimo nel vocabolario, come il design §1.13 | il vocabolario è la conoscenza fissa di IVAO; il minimo di una postazione cambia con l'ora e con le scelte della divisione |
| Il controllo di IVAO per ogni candidato (`/v2/fras/check/{callsign}/{vid}`) | esatto — vede anche le eccezioni e le date —, ma una chiamata per candidato, postazione e turno, centinaia per un roster, e la proposta dipenderebbe da IVAO in quel momento |
| Anche le righe per membro | i dati di una persona in una tabella di riferimento, per eccezioni che lo staff dà su IVAO dopo aver deciso il roster |
| Un rifiuto sotto l'FRA | Carmine: lo staff toglie l'FRA a una persona su IVAO, quindi l'hub avvisa e lascia fare |
| L'esito del giro «partial» senza FRA, come per le postazioni | una divisione senza FRA sarebbe «partial» ogni notte; il conteggio sta nel messaggio |
| Una pagina fallita letta come le pagine arrivate | la potatura toglierebbe gli FRA delle pagine mancanti |
| «Nessuna risposta» e «nessun FRA» con la stessa lista vuota, come gli altri snapshot (la prima forma di questa PR) | una divisione che toglie tutti i suoi FRA li terrebbe nell'hub per sempre, e alzerebbero i minimi: rilievo 2 del revisore sulla #204 |
| I rating preferiti nel vocabolario, come dati di IVAO (la prima forma di questa PR e il design §1.13) | sono la regola di una divisione, non di IVAO: un'altra divisione dovrebbe cambiare il nucleo. **Carmine li ha spostati in `division.json`** (seconda risposta sulla #204), dopo il rilievo 1 del revisore |
| Una mappa senza controllo all'avvio | un tipo o un rating scritto male non metterebbe nessuno per primo, e niente lo direbbe: Carmine ha chiesto che l'avvio si fermi |
| Il validatore della mappa in `DivisionOptionsValidator` | le due liste che lo controllano (i tipi e la scala ATC) sono di IVAO, quindi nel suo perimetro (`CLAUDE.md` §3): un validatore suo, in `Core/Ivao/`, registrato accanto all'altro |
| Il minimo come `Rating` del vocabolario | un numero che il vocabolario non conosce diventerebbe «nessun minimo»; con il numero e `IsAtLeast` diventa «nessuno può» |
| Il preferito dove il rating si allena (`PositionType`) | un tipo per rating: l'ADC resterebbe senza la `GND`, che Carmine gli dà |
| Una seconda classe di postazione con il tipo, per non toccare `AtcPositionDto` | la stessa cosa scritta due volte; il training non legge il campo nuovo |
| Il tipo della postazione salvato sulla riga dell'evento | lo sa la directory; il modulo lo chiede quando serve |
| Le parole dell'avviso nel modulo | nominano IVAO: fuori dal perimetro (`CLAUDE.md` §3) |

## 5. Le domande a Carmine, e le risposte

1. **Il minimo di una postazione è il suo FRA su IVAO?** Il nucleo legge ogni notte gli FRA per postazione della divisione (non
   quelli per membro), e per un turno vale il più alto fra quelli attivi in quelle ore; senza FRA vale solo `minimumAtcRating`.
   Raccomandato: sì. **Risposta: sì**, con l'aggiunta dell'avviso che chiede di togliere l'FRA su IVAO (§3.2).
2. **Il rating preferito di `DEL`, `DEP`, `FSS` e `ATIS`**: raccomandato l'ADC su `DEL` e l'APC su `DEP`, nessuno su `FSS` e `ATIS`.
   **Risposta: AS3 su `DEL`, APC su `DEP`, ADC su `FSS`**; `ATIS` non nominato, quindi nessuno, come raccomandato (§3.1).
3. **Dopo la revisione** ([rilievi][r204], punto 1, che il revisore ha portato a Carmine): i rating preferiti restano nel
   vocabolario, come dati di IVAO, o vanno nel file della divisione? **Risposta, 1° ottobre 2026 ([seconda risposta][ok204b]): in
   `config/division.json`**, una mappa dal tipo di postazione al rating, controllata all'avvio contro il vocabolario dei rating del
   nucleo (un tipo o un rating sconosciuti fermano l'avvio con un messaggio chiaro); il nucleo risponde ancora `PreferredFor`, il
   modulo non nomina rating, il file d'esempio spiega la chiave a chi forka; i valori restano i suoi (§3.1). Il punto 2 dei rilievi
   (la risposta vuota sugli FRA) l'ha lasciato a questa fase: è corretto (§3.2).

## 6. Che cosa si tocca

Tutto del nucleo, ed è il perché di questa nota (`core-guard`):

- **Il vocabolario**: `Core/Ivao/RatingVocabulary.cs` (`PreferredFor`, `Named`, `IvaoRatings.WithPreferred`); i tipi di IVAO in
  `Core/Ivao/IvaoAtcPosition.cs` (`Kinds`).
- **La regola della divisione**: `Core/Division/DivisionOptions.cs` (`PreferredAtcRatings`), `Core/Ivao/PreferredAtcRatingsValidator.cs`
  (nuovo), `Core/Ivao/IvaoServiceCollectionExtensions.cs` (il vocabolario con la mappa, il validatore), `config/division.json` (i
  valori di IT), `config/division.example.json` (la chiave spiegata a chi forka). `config/division.xx.json` non cambia: il fork «XX»
  non mette nessuno per primo.
- **La directory**: `Core/Ivao/AtcPositionDirectory.cs` (`OfDivisionAsync`, `FindAsync`, `MinimaAsync`, il tipo nel DTO,
  `AtcPositionRule`, `AtcPositionMinimum`).
- **Gli FRA**: `Core/Ivao/IvaoFra.cs` (nuovo: la riga, quella del client e il lettore), `Core/Ivao/IIvaoApiClient.cs`,
  `IvaoApiClient.cs`, `FixtureIvaoApiClient.cs`, `RefDataSyncJob.cs`, `Core/Data/HubDbContext.cs`, `RefSchemaConfiguration.cs`, la
  migrazione `AddIvaoFras` e lo snapshot del contesto del nucleo.
- **Le parole dell'avviso**: `locales/en/common.json`, `locales/it/common.json` (`atcPositions.belowMinimum`).
- **Le misure**: `tools/record-ivao-fixtures.mjs` (la modalità `--fras`), `tests/fixtures/ivao/README.md`, la fixture
  `fras-IT.json` (94 FRA delle postazioni del banco, nessuna persona), `docs/FORKING.md`.
- **Test**: `RatingVocabularyTests` (unità: i tipi, i bordi, un tipo sconosciuto) e `AtcPositionTests` (integrazione, sulle fixture:
  per nominativo, tutta la divisione, un'altra divisione, gli FRA scritti, rinfrescati e potati, il minimo per un turno), scritti in
  A1 e A2 di M3 da questa stessa mano: in `AtcPositionTests` i tre DTO attesi prendono il tipo, nessuna asserzione tolta.
  `IvaoFraReaderTests`, `AtcPositionMinimumTests` e `PreferredAtcRatingsValidatorTests` (unità, nuovi). Nessun test del maintainer
  cambiato.

## Da portare nel piano

- **§4.1** (il file della divisione): la chiave **`preferredAtcRatings`** — chi viene per primo su un tipo di postazione quando si
  propone il roster di un evento, il tipo come IVAO lo scrive e la sigla di un rating ATC; la regola della divisione, non di IVAO;
  l'avvio si ferma su un tipo o un rating che il nucleo non conosce. IT: AS3 `DEL`; ADC `FSS`, `GND`, `TWR`; APC `APP`, `DEP`; ACC
  `CTR`; nessuno `ATIS`.
- **§9.1, riga «Dati di riferimento IVAO»**, e **§4.2** (il perimetro IVAO): il vocabolario dei rating risponde anche **il rating
  preferito per un tipo di postazione**, dalla mappa della divisione; `IAtcPositionDirectory` dà anche **le postazioni della divisione
  per nominativo**, con tipo e FIR, e **il minimo di una postazione per un turno**, dagli FRA di IVAO.
- **§7, schema `ref_`**: la riga `ivao_fras` — PK `id` di IVAO; `callsign`, `minimum_rating`, `days`, `starts_at`, `ends_at`,
  `on_date`, `is_active`, `raw_json`, `synced_at`; gli FRA per postazione del paese della divisione: senza risposta restano, con una
  risposta vuota se ne vanno (la divisione non ne ha più); indice sul nominativo.
- **§10, tabella dell'API**: la riga degli FRA — `/v2/fras?countryId=…&members=false&expand=true`, cento per pagina, `client_credentials`,
  ogni notte con il resto dei dati di riferimento; mai le righe per membro.
- **§9.7, collaborazioni ATC↔Events** e design §1.13, §4.4 (che non si toccano): il minimo di una postazione è il suo FRA e lo dice la
  directory, non il vocabolario; sotto l'FRA la correzione e la cessione avvisano, con l'istruzione di toglierlo su IVAO, e non
  rifiutano.
