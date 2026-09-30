# Le sessioni condivise per VID (E10b)

**Data:** 30 settembre 2026 — fase E10b di M4, PR del nucleo
**Stato:** **scelta tecnica**, per dare forma nel codice all'estensione n.3 del design (`09-design-m4.md` §13: «il VID e la storia
per VID in `IAtcActivitySource`»), decisa con il design (#180) e nella nota `2026-09-29-il-roster-atc` (§2.3 l'esperienza, §2.7 la
presenza nei turni). Nessuna domanda nuova. In più **una correzione** trovata strada facendo (§4): con `atcData: vipi` e la stringa di
connessione non ancora nei segreti **l'hub non partiva**, mentre la sorgente prometteva «non disponibile».
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: si estende il meccanismo del nucleo che esiste già — `IAtcActivitySource`, l'unica
porta verso un archivio delle sessioni ATC (note `2026-09-14-dati-condivisi-con-vipi` §3.4 e `2026-09-23-gli-atc-contattati`) — e il
modulo non nomina la vista né la legge (`ArchitectureTests.NoModuleNamesTheAtcArchiveOrTheBoundaryDataset`, che non cambia). È una PR
del nucleo, prima di E11b ed E13a che la usano (`CLAUDE.md` §0 regola 6).

## 1. Che cosa serve, e perché il modulo non ne fa a meno

- **L'esperienza** (design §4.3, punto 3.2): il proponente del roster (E11b) mette prima chi apre più spesso quella postazione, o quel
  tipo, negli ultimi `experienceMonths` (12). Serve la storia di **un** controllore: le sue connessioni in un anno, postazione per
  postazione.
- **La presenza in un turno** (design §4.5, E13a): le connessioni di quel VID su quella postazione nella finestra del turno (almeno
  `attendanceMinimumMinutes` → presente), e **chi ha coperto la postazione** in quella finestra, se il titolare non c'era.
- **Oggi** `IAtcActivitySource` fa una domanda sola, «quali posizioni erano online in questo intervallo» (T12 dei tour), e le sue
  presenze non dicono di chi erano: la vista ha `vid`, `SharedAtcSession.Vid` la legge, ma il nucleo non la passava (E0, «Trovato»,
  punto 7). Il modulo non può leggere la vista (il test di architettura) né scriversi una domanda sua: l'archivio è un'integrazione
  facoltativa del nucleo, con la sua risposta «non disponibile», e una sola.

## 2. Che cosa si fa

### 2.1 Il VID in ogni presenza

- **`AtcPresence.Vid`** (`int?`), una proprietà `init` **sotto** il costruttore, come le quattro di `IvaoAirportDto` arrivate con T1:
  il costruttore resta quello di T12, così i test dei tour che costruiscono presenze (`AtcContactsTests`, del maintainer) compilano e
  passano come sono. `null` dove l'archivio non lo dice (un doppio scritto prima di E10b): non è il VID di nessuno, quindi una
  presenza senza VID non conferma mai il turno di qualcuno.
- `VipiAtcActivitySource` lo legge dalla colonna `vid` della vista, che vIPI riempie con `UserId` (la sua migrazione
  `VistaCondivisaSessioniAtc`, letta il 30 settembre 2026).
- **Non esce dai tour**: le risposte dei tour si costruiscono campo per campo (`AtcContactDto` nella proposta e nel JSON dei contatti
  del PIREP, `AgentAtcPresenceDto` per l'agente del validatore), quindi il VID di un controllore non arriva né al browser del pilota né
  all'agente, e il contratto OpenAPI non cambia (`pnpm gen:api` senza differenze).

### 2.2 La domanda nuova: le connessioni di un controllore

- **`IAtcActivitySource.SessionsOfAsync(vid, fromUtc, toUtc)`**: le connessioni di **quel** controllore aperte in qualche momento
  dell'intervallo, nella stessa forma della domanda di T12 (`AtcActivity`): le presenze, e da quando l'archivio è completo per le
  postazioni della divisione e per il resto del mondo.
- **Una domanda per i due usi**: un anno per l'esperienza (`Of(callsign)` è una postazione), l'ora di un turno per la presenza, con
  `Covers` che separa «non c'era» da «non si sa». **Chi ha coperto** la postazione è la domanda di T12, ora con i VID (§2.1).
- **Il tipo di una postazione non lo dice questa interfaccia**: il nucleo dà i nominativi; il tipo di un nominativo lo dice la
  directory delle postazioni («per nominativo, con tipo e FIR», E10c), da cui vengono anche le postazioni dell'evento, così le due
  metà del roster leggono un vocabolario solo. La colonna `position` della vista resta fuori.
- **Un controllore alla volta**: vIPI indicizza `(UserId, StartUtc)` (letto nel suo modello il 30 settembre 2026), quindi un anno di un
  controllore è un tratto breve di quell'indice, non dell'archivio; il proponente chiede una volta per candidato, una volta per evento.
- **Una query sola per le due domande**: `VipiAtcActivitySource` legge la vista con la query di T12 — lo stesso limite di due giorni
  sull'inizio (`LongestConnection`), la stessa copertura in cache per un'ora — e il filtro su `vid` quando si chiede di un controllore.

### 2.3 «Non disponibile», come oggi

- **`atcData.source: none`** (un fork, e IT finché la vista non si accende): `UnavailableAtcActivitySource` risponde `null` alle due
  domande.
- **Un archivio scritto prima di E10b** — i doppi dei test fra questi — risponde `null` alla domanda nuova da sé: la domanda ha un
  **corpo predefinito nell'interfaccia**, come `IIvaoApiClient.GetAtcPositionsAsync` (A2 di M3). Senza, il doppio di
  `AtcContactsTests` non compilerebbe più.
- **Un archivio che non si legge** (la vista non c'è, il server non risponde, la stringa manca: §4): `null`, con un avviso nel log.
- Con `none` la presenza ATC si legge dal tracker, per VID e con `connectionType=ATC` (design §9.3): la ricerca è di E10a, la scelta
  fra le due fonti del modulo (E13a). Qui la risposta resta «non disponibile», come chiede `10`.

## 3. I test

`AtcActivitySourceTests` (integrazione, nuovo; VID 761030–761039, nessuna riga dell'hub scritta):

- **l'archivio sta in un database suo**, come `itivao_atc` in produzione: la tabella di vIPI per quanto la vista la legge, con i suoi
  indici, e la vista scritta parola per parola come la migrazione di vIPI; all'utente dell'hub **solo** il `SELECT` sulla vista (il
  `GRANT` che vIPI chiede a chi amministra il server). Una query che leggesse il database dell'hub, o altro che la vista, non
  troverebbe niente — cosa che la vista finta di `PirepTests`, nel database dell'hub, non poteva provare;
- **l'ora di un turno**: le due connessioni del controllore sulla torre (una aperta prima del turno, una ancora aperta), niente
  dell'avvicinamento di dopo; e chi ha tenuto la torre in mezzo, dai VID della domanda di T12;
- **un anno di un controllore**: le sue connessioni e di nessun altro, postazione per postazione, quella di prima dell'anno fuori; la
  copertura per metà (la divisione tutto l'anno, il mondo dal 28 agosto: se prima il controllore ha aperto un settore francese, non si
  sa); un VID mai visto ha una lista vuota, che è una risposta e non «non disponibile»;
- `none` e un archivio scritto prima di E10b: «non disponibile»; la vista sparita: «non disponibile» alle due domande; la stringa di
  connessione mancante (§4);
- **controprove**: senza il filtro su `vid`, o senza il VID nelle presenze, le due prove del VID cadono.
- L'host legge le fixture di IVAO (`useIvaoFixtures`): un host che parte con i dati di riferimento vuoti li sincronizza, e senza
  fixture chiede un token a IVAO (rifiutato, ma è una chiamata; nel primo giro della classe erano cinque per host).

## 4. Trovato e corretto: senza la stringa di connessione l'hub non partiva

- `VipiAtcActivitySource` diceva «Whatever goes wrong — no connection string, a server that does not answer, a view not yet created —
  the answer is null», e il suo `catch` aspettava già l'`InvalidOperationException` della stringa mancante. Ma il contesto della vista
  gli arrivava **dal costruttore**: si costruiva insieme alla sorgente, fuori dal `try`. Con `atcData: vipi` e
  `ConnectionStrings:AtcData` non ancora nei segreti cadeva chiunque costruisse la sorgente — e all'avvio la costruisce il seeder dei
  contenuti (`ContentSeeder` → `ContentPublishService` → `DataBlockProviders` → `ReviewQueueProvider` dei tour → `PirepReview` →
  `PirepDisputes` → `FlightOpsReferences` → `PirepSubmission` → `AtcProposer`): **l'hub non partiva**.
- **La correzione**: il contesto si chiede **dentro** il `try` (`IServiceProvider`, come `PersonalDataErasure` chiede i contesti dei
  moduli). L'hub parte, e le due domande rispondono «non disponibile» con l'avviso nel log.
- Accendere l'archivio sono quattro passi (nota `2026-09-23-gli-atc-contattati` §6: la release di vIPI con la vista, il `GRANT`, il
  segreto, `atcData`): fatti in un altro ordine, ora non spengono il sito.
- **Provato**: `AnArchiveNamedBeforeItsConnectionStringIsNotAvailable`; con la sorgente di prima cade all'avvio dell'host, «The
  connection string 'ConnectionStrings:AtcData' is not configured».
- Non è una decisione nuova: è il comportamento che la classe e `AddAtcActivity` descrivevano già. Scartato: rifiutare l'avvio, come
  per il client OAuth — l'archivio è facoltativo, e l'hub funziona senza.

## 5. Alternative scartate

| Alternativa | Perché no |
|---|---|
| Il VID come sesto parametro del costruttore di `AtcPresence` | la forma che costruiscono i test dei tour cambierebbe: una proprietà sotto il costruttore non tocca nessuno |
| `int Vid`, 0 dove non si sa | 0 è l'installazione stessa nelle colonne di persona (`UpdatedBy = 0` del seeder); `null` non è il VID di nessuno |
| La domanda nuova senza corpo nell'interfaccia | il doppio di `AtcContactsTests` (del maintainer) non compilerebbe più |
| Un'interfaccia nuova per la storia | è la domanda di T12 su una persona sola, con la stessa risposta e lo stesso «non disponibile»; design e piano la vogliono in `IAtcActivitySource` |
| La storia già contata nel nucleo (sessioni o minuti per postazione, o per tipo) | «più spesso» e «quel tipo» sono regole del roster, del modulo; il tipo lo dice la directory di E10c. Il nucleo dà le connessioni |
| Più VID in una domanda | un proponente per evento, una domanda per candidato, ognuna un tratto breve dell'indice di vIPI: si aggiunge se una fase la misura necessaria |
| Il ripiego sul tracker dentro `IAtcActivitySource` quando `none` | `10` chiede «non disponibile, come oggi»; il tracker per VID e per tipo di connessione è di E10a, e la scelta fra le due fonti del modulo (E13a) |
| La vista finta nel database dell'hub, come `PirepTests` | una sorgente che leggesse la connessione dell'hub invece di `AtcData` passerebbe inosservata |
| Anche il `SessionId` (l'id della sessione di IVAO, che vIPI copia dal whazzup) e la colonna `position` nella presenza | nessuna fase li chiede ancora; si aggiungono come `Vid`, una proprietà `init`, se E14a vorrà scrivere quale sessione ha trovato |

## 6. Che cosa si tocca

Tutto del nucleo, ed è il perché di questa nota (`core-guard`):

- **`src/IvaoHub.Core/Atc/`**: `IAtcActivitySource.cs` (il VID, la domanda nuova con il suo corpo predefinito, `Unavailable…` che la
  risponde) e `VipiAtcActivitySource.cs` (una query per le due domande; il contesto chiesto dentro il `try`, §4). Nient'altro del
  nucleo: niente `Core/Ivao/` — l'archivio non è il client di IVAO —, niente `AddAtcActivity`, niente migrazioni.
- **Test**: `tests/IvaoHub.IntegrationTests/AtcActivitySourceTests.cs` (nuovo). Nessun test del maintainer cambiato: `AtcContactsTests`
  e `PirepTests` compilano e passano come sono.
- **Nessun documento pubblico**: `docs/FORKING.md` e `config/division.example.json` descrivono l'archivio per i tour, e restano veri;
  la metà degli eventi la scrive E15b («`docs/FORKING.md` per l'ATC»), quando il modulo lo usa.

## Da portare nel piano

- **§9.7, «Dati di vIPI»**: l'archivio del nucleo risponde a due domande — le posizioni online in un intervallo, **ognuna con il VID di
  chi la teneva**, e **le connessioni di un controllore** in un intervallo (E10b) —; senza archivio, o con un archivio che non si legge,
  tutte e due «non disponibile». Con `atcData: vipi` e la stringa di connessione non ancora nei segreti l'hub parte e risponde «non
  disponibile» (§4; prima non partiva).
- **§9.7, tabella delle collaborazioni, riga ATC↔Events**: da `IAtcActivitySource` vengono l'esperienza di un controllore per il roster
  **e la presenza nei turni** (con chi ha coperto la postazione); senza la sorgente, la presenza la legge il modulo dal tracker (E10a).
- **§13, riga M4**: E10b (nucleo: le sessioni condivise per VID), con questa nota.
