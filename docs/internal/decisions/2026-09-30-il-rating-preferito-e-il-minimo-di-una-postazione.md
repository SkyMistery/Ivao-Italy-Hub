# Il rating preferito, il minimo di una postazione e le postazioni per nominativo (E10c)

**Data:** 30 settembre 2026 — fase E10c di M4, PR del nucleo
**Stato:** **Proposta** — due domande a Carmine (§5), sulla PR di questa fase. Le postazioni per nominativo (§3.3) e la forma del
rating preferito (§3.1) sono una scelta tecnica, senza domande. **Il codice che dipende dalle risposte aspetta**: il minimo di una
postazione, e il preferito di `DEL`, `DEP`, `FSS` e `ATIS`.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: si estendono il vocabolario dei rating (`Core/Ivao/RatingVocabulary.cs`, A1 di
M3) e la directory delle postazioni (`Core/Ivao/AtcPositionDirectory.cs`, A2 di M3), nel perimetro IVAO del nucleo (§3); con la
risposta raccomandata alla domanda 1, anche la sincronizzazione dei dati di riferimento di IVAO (`RefDataSyncJob`, come le
postazioni in A2). Il modulo non ne scrive una copia sua. È una PR del nucleo, prima di E11a ed E11b che la usano (`CLAUDE.md` §0
regola 6). Design `09-design-m4.md` §1.13, §4.1, §4.3, §13 n.4; nota `2026-09-29-il-roster-atc` §2.2.

## 1. Che cosa serve, e perché il modulo non ne fa a meno

- **Il proponente del roster** (E11b, design §4.3) chiede per ogni turno **chi può** — almeno `minimumAtcRating` e almeno **il
  minimo della postazione** — e **chi prima**: chi ha **il rating preferito** per il tipo della postazione. Il modulo non scrive
  numeri né nomi di rating (nota `il-roster-atc` §2.2): le due risposte sono del nucleo (design §1.13, estensione n.4). Oggi il
  vocabolario sa soltanto su quale tipo si allena un rating (`Rating.PositionType`: ADC `TWR`, APC `APP`, ACC `CTR`), niente di
  `GND`, `DEL`, `DEP`, `FSS` e `ATIS`, e nessun minimo di una postazione (`10`, E0, «Trovato», punto 8).
- **La scheda ATC dello staff** (E11a, design §4.1) sceglie le postazioni dell'evento dall'elenco del nucleo e copia sulla riga il
  **FIR** (`IHasFir`), che fa vedere a un capo FIR le postazioni del suo FIR. Oggi la directory risponde solo «le postazioni su cui
  si allena questo rating» (`ForRatingAsync`), senza il tipo e senza una ricerca per nominativo (`10`, E0, «Trovato», punto 9).

## 2. Da dove vengono le regole

Letto e misurato il 30 settembre 2026, con il token dell'applicazione (`client_credentials`, lo stesso della sincronizzazione).

- **Le «Regulations» di IVAO** ([ATC Operations, A.1][regs]) dividono le postazioni per servizio: il **controllo d'aerodromo** sono
  le `DEL`, le `GND` e le `TWR` (A.1.1); il **controllo terminale** le `APP` e le `DEP`, e le `TWR` che hanno anche una TMA (A.1.2);
  il **controllo d'area** le postazioni d'area, i `CTR` (A.1.3); le **FSS** sono un servizio a sé (A.1.4). `ATIS` non c'è: non è
  una postazione di controllo. I tre rating con un training pratico portano il nome di uno dei tre servizi di controllo —
  **Aerodrome Controller** (ADC), **Approach Controller** (APC), **Area Control Centre** (ACC), i nomi di `ratings.Atc.*` —; nessuno
  porta quello delle FSS.
- **Un rating «preferito» IVAO non lo scrive da nessuna parte**: è la regola dell'ED (Carmine, c1, design §R.3 e §17.1 n.9), che
  mette su ogni tipo il rating che porta il nome del suo servizio — TWR e GND l'ADC, APP l'APC, ACC l'ACC. Per `DEL` e `DEP` la
  stessa regola ha una risposta dalle definizioni di IVAO (A.1.1 e A.1.2 li mettono con GND e TWR, e con APP); per `FSS` e `ATIS`
  nessuna regola pubblicata dà un rating.
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

[regs]: https://wiki.ivao.aero/en/home/ivao/regulations
[hq]: https://wiki.ivao.aero/en/home/atcoperations/HQAirspace/FRAPolicy-AreasOutsideDivisions
[api]: https://api.ivao.aero/docs/core-json

## 3. La proposta

### 3.1 Il rating preferito, nel vocabolario

- **Ogni rating dice su quali tipi di postazione è il preferito** (`Rating.PreferredOn`, i tipi come IVAO li scrive), e il
  vocabolario risponde **`PreferredFor(tipo)`**: il rating più basso che è il preferito su quel tipo; nessuno per un tipo su cui
  nessun rating lo è, e per un tipo che non conosce. «Ha il rating preferito» è `IsAtLeast` con quel rating: un SEC è preferito
  dove lo è l'ADC. Un tipo scritto su due rating è un errore del vocabolario, come un numero scritto due volte.
- **I dati di IVAO**, proposti: ADC su `DEL`, `GND`, `TWR`; APC su `APP`, `DEP`; ACC su `CTR`; **nessuno** su `FSS` e `ATIS`.
  `TWR`, `GND`, `APP` e `CTR` sono la regola di Carmine (c1) e vanno nel codice subito; `DEL` e `DEP` sono la stessa regola sulle
  definizioni di IVAO; `FSS` e `ATIS` restano senza, perché IVAO non dà loro un rating: su un tipo senza preferito il proponente
  non divide i candidati con questo criterio, e valgono gli altri. **Domanda 2**, per questi quattro.
- `PositionType` resta com'è: dice **su che cosa si allena** un rating, una domanda del training. L'ADC si allena sulla `TWR` ed è
  il preferito anche su `DEL` e `GND`: sono due dati, non uno.
- ⚠️ Una `TWR` che ha anche una TMA (A.1.2) resta una `TWR`: il tipo è quello che IVAO pubblica, e la TMA nei dati non c'è.

### 3.2 Il minimo di una postazione: gli FRA, nella directory — raccomandato

- **Il minimo di una postazione è quello dei suoi FRA su IVAO**, e lo dice la **directory**, non il vocabolario: è un dato di una
  postazione, che la divisione cambia e che cambia con l'ora (§2), non una regola di un tipo. **Scostamento dal design §1.13**, che
  lo metteva nel vocabolario pensando a una regola per tipo che non esiste.
- **Gli FRA per postazione della divisione** (`members=false`: nessuna persona entra nell'hub) in una tabella di riferimento,
  `ref_ivao_fras`, rinfrescata ogni notte con i dati di IVAO, dopo le postazioni, e potata solo su una risposta piena (la regola del
  3 settembre, come in A2); `IIvaoApiClient` con un membro in più, predefinito vuoto come quello delle postazioni; un lettore solo
  per i due client; le fixture registrate con una modalità nuova di `tools/record-ivao-fixtures.mjs`.
- **La risposta**: il minimo di una postazione **per una finestra** (un turno) — il rating più alto fra gli FRA attivi della
  postazione che valgono in qualche momento della finestra: quelli con una data in quella data, gli altri nei loro giorni, anche a
  cavallo della mezzanotte. Chi è sotto non potrebbe aprire la postazione per tutto il turno. **Nessun FRA, nessun minimo suo**:
  vale `minimumAtcRating`. Il proponente prende il più alto dei due.
- **Le eccezioni per membro restano su IVAO**: il roster non le vede. Lo staff che aggiunge un controllore sotto il minimo riceve un
  **avviso**, non un rifiuto (design §4.4), e gli dà l'eccezione su IVAO come fa oggi (le righe con una data).
- ⚠️ **Da verificare nel codice**, dopo la risposta: il fuso degli orari degli FRA (UTC, come tutto IVAO, è la lettura più
  probabile) e come IVAO combina due righe che valgono insieme (il più alto è la lettura prudente, e le due righe con una data
  misurate alzano il minimo).
- **Domanda 1.** Con questa risposta la fase cresce di una tabella del nucleo (migrazione additiva) e di una chiamata di IVAO, come
  A2. E11a non ne ha bisogno, E11b sì.

### 3.3 Le postazioni della divisione per nominativo, nella directory — scelta tecnica

- **`OfDivisionAsync()`**: tutte le postazioni della divisione, per nominativo: l'elenco da cui lo staff sceglie quelle di un evento
  (E11a). Nessuna cache né endpoint, come `ForRatingAsync`: il modulo lo chiede dal suo, e cambia una volta per notte.
- **`FindAsync(nominativo)`**: la postazione della divisione con quel nominativo, scritto in qualunque maiuscola; nessuna per un
  nominativo che IVAO non elenca o che è di un'altra divisione. È il controllo del form di E11a, con la copia del FIR, e la domanda
  del proponente.
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
| Il preferito su `FSS` all'ACC, perché è una stazione del FIR come i `CTR` | IVAO non dà un rating alle FSS, che danno informazioni e non controllano il traffico |
| Il preferito dove il rating si allena (`PositionType`) | un tipo per rating: l'ADC resterebbe senza la `GND`, che Carmine gli dà |
| Una seconda classe di postazione con il tipo, per non toccare `AtcPositionDto` | la stessa cosa scritta due volte; il training non legge il campo nuovo |
| Il tipo della postazione salvato sulla riga dell'evento | lo sa la directory; il modulo lo chiede quando serve |

## 5. Le domande a Carmine

1. **Il minimo di una postazione è il suo FRA su IVAO?** Il nucleo legge ogni notte gli FRA per postazione della divisione (non
   quelli per membro), e per un turno vale il più alto fra quelli attivi in quelle ore; senza FRA vale solo `minimumAtcRating`.
   **Raccomandato: sì** (§3.2). L'alternativa è nessun minimo della postazione (§4, prima riga).
2. **Il rating preferito di `DEL`, `DEP`, `FSS` e `ATIS`**: **raccomandato** l'ADC su `DEL` e l'APC su `DEP`, come le definizioni
   di IVAO; nessuno su `FSS` e `ATIS` (§3.1).

## 6. Che cosa si tocca

Tutto del nucleo, ed è il perché di questa nota (`core-guard`):

- **Il vocabolario**: `Core/Ivao/RatingVocabulary.cs` (`PreferredOn`, `PreferredFor`, i dati di IVAO).
- **La directory**: `Core/Ivao/AtcPositionDirectory.cs` (`OfDivisionAsync`, `FindAsync`, il tipo nel DTO).
- **Con la risposta 1 raccomandata**: `Core/Ivao/IIvaoApiClient.cs`, `IvaoApiClient.cs`, `FixtureIvaoApiClient.cs`, un file nuovo
  per la riga e il lettore degli FRA, `RefDataSyncJob.cs`, `Core/Data/HubDbContext.cs`, `RefSchemaConfiguration.cs`, una
  migrazione additiva del contesto del nucleo e il suo snapshot, `tools/record-ivao-fixtures.mjs`, `tests/fixtures/ivao/README.md`
  e le fixture nuove.
- **Test**: `RatingVocabularyTests` (unità: i tipi, i bordi, un tipo sconosciuto) e `AtcPositionTests` (integrazione, sulle fixture
  delle postazioni: per nominativo, tutta la divisione, un'altra divisione), scritti in A1 e A2 di M3 da questa stessa mano; in
  `AtcPositionTests` i tre DTO attesi prendono il tipo, nessuna asserzione tolta. Nessun test del maintainer cambiato.

## Da portare nel piano

- **§9.1, riga «Dati di riferimento IVAO»**, e **§4.2** (il perimetro IVAO): il vocabolario dei rating dice anche **il rating
  preferito per un tipo di postazione** (ADC sulle postazioni d'aerodromo, APC su quelle terminali, ACC sui `CTR`: le definizioni
  di IVAO e la regola dell'ED); `IAtcPositionDirectory` dà anche **le postazioni della divisione per nominativo**, con tipo e FIR.
- **Con la risposta 1**: §9.1 e **§7, schema `ref_`**, la riga `ivao_fras`; **§10**, la riga degli FRA (`/v2/fras` per paese, solo
  le righe per postazione, `client_credentials`, ogni notte); il minimo di una postazione lo dice la directory, dagli FRA, e non il
  vocabolario (scostamento dal design §1.13, che non si tocca).
