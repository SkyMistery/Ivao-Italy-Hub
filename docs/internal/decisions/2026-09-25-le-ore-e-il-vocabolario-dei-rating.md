# Le ore di connessione e il vocabolario dei rating (A1)

**Data:** 25 settembre 2026 — fase A1 di M3, PR del nucleo
**Stato:** **scelta tecnica**, per dare forma nel codice a decisioni già prese da Carmine: le estensioni n.1, n.4 e n.8 del
design (`07-design-m3.md` §8, deciso sulla PR #121) e la nota `2026-09-25-rating-e-postazioni-dal-nucleo`, che rimanda a
questa la forma nel codice. Nessuna domanda nuova; le misure qui sotto non hanno aperto un bivio.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: si estendono meccanismi del nucleo che esistono già — il profilo IVAO
letto al login (come l'email il 6 settembre, nota `2026-09-06-indirizzo-di-un-destinatario`), il perimetro IVAO di
`Core/Ivao/`, l'elenco chiuso dei componenti, il banco e2e — e il modulo non ne scrive una copia sua. È una PR del nucleo,
prima del codice del modulo che la usa (`CLAUDE.md` §0 regola 6).

## 1. Che cosa si è misurato, il 25 settembre 2026, con il token vero

- **`/v2/users/me`**, con un login vero di `dalberone` e gli scope di `config/ivao-oauth.json`: gli stessi 26 campi misurati il
  3 settembre (il commento di `IvaoUserProfileReader` li elenca). **`hours` è un array** di tre righe
  `{ "type": "pilot" | "atc" | "staff", "hours": <intero> }`, in quest'ordine, e i valori sono **secondi**: lo dice lo schema
  pubblico di IVAO (`UserHoursDto`, «the user hours for this connection type in seconds») e lo dicono i numeri, 7 502 599 per
  l'ATC (2 084,05 ore), 6 273 832 per il pilota (1 742,73), 795 217 da staff (220,89), che `dalberone` ha ritrovato uguali sul
  suo profilo di IVAO. Con il codice di questa fase, un login vero sull'hub di sviluppo ha scritto in `hub_users` proprio
  2 084,05 e 1 742,73, con i rating 6 (APC) e 5 (PP). ⚠️ **Il test del lettore scrive un'altra
  forma**: `IvaoUserProfileReaderTests.RealShape` ha `"hours": { "atc": 100, "pilot": 200 }`, un oggetto. Allora il campo non si
  leggeva e quella forma non era stata misurata; il test resta verde, perché non la legge. È un test del maintainer e non si
  tocca (`CLAUDE.md` §0 regola 3): lo si dice al revisore.
- **`rating`**: `{ isPilot, isAtc, pilotRating, atcRating, networkRating }`, i primi due `{ id, name, shortName, description }`.
  Il lettore legge già `id`, e non cambia.
- **Il vocabolario dei rating, come l'API lo scrive**: nessun endpoint lo elenca, ma ogni cliente di `/v2/tracker/now/atc`,
  `/v2/tracker/now/pilots` e delle sessioni del tracker porta i rating di chi è connesso. Letti lì, **solo i rating e nessuna
  persona**, con le sessioni degli ultimi quattordici giorni per i tre che nessuno aveva in quel momento:

  | Numero | ATC | Pilota |
  |---|---|---|
  | 2 | AS1 — ATC Applicant | FS1 — Basic Flight Student |
  | 3 | AS2 — ATC Trainee | FS2 — Flight Student |
  | 4 | AS3 — Advanced ATC Trainee | FS3 — Advanced Flight Student |
  | 5 | ADC — Aerodrome Controller | PP — Private Pilot |
  | 6 | APC — Approach Controller | SPP — Senior Private Pilot |
  | 7 | ACC — Centre Controller | CP — Commercial Pilot |
  | 8 | SEC — Senior Controller | ATP — Airline Transport Pilot |
  | 9 | SAI — Senior ATC Instructor | SFI — Senior Flight Instructor |
  | 10 | CAI — Chief ATC Instructor | CFI — Chief Flight Instructor |

  Nessun `1` da nessuna parte: un membro nuovo parte da AS1 e FS1.
- **Le postazioni**: `/v2/ATCPositions/all` (11 863 postazioni del mondo, 21 MB) e `/v2/subcenters/all` (1 497 settori, 32 MB)
  **non hanno nessun campo di rating**, né minimo né altro. Le postazioni d'aeroporto sono di sei tipi (`DEL`, `GND`, `TWR`,
  `APP`, `DEP`, `ATIS`), i settori di due (`CTR`, `FSS`), e **i settori stanno solo nel secondo endpoint**. Quindi, come il
  design prevedeva per questo caso (§8 n.4), **il tipo di postazione di un rating lo scrive il vocabolario**.

## 2. Che cosa si fa

### 2.1 Le ore (n.1)

- `IvaoUserProfileReader` legge le righe `atc` e `pilot` di `hours` e le converte **in ore, con due decimali arrotondati verso lo
  zero** (un numero non è mai più di quanto IVAO ha contato); la riga `staff` no, perché non ha uno scopo (i dati IVAO minimi, piano
  §6.4, la riga GDPR; il design e la nota del 6 settembre la chiamano §11.4). Una forma diversa, una riga che manca, un numero
  negativo o scritto come testo valgono null, mai un login fallito, come il resto del lettore.
- **Due colonne su `hub_users`**, `hours_atc` e `hours_pilot`, `decimal(9,2)`, nullable; migrazione additiva `AddConnectionHours`
  del contesto del nucleo. **Ore e non secondi**: che IVAO conti in secondi lo sa il lettore e nessun altro, e il modulo ragiona
  in ore (`minimumHours`, `trainee_hours_at_request`).
- **`UserSyncService` le scrive a ogni login**, accanto ai rating. **Quando IVAO non le manda resta la fotografia di prima**,
  come per l'email: le ore crescono soltanto, quindi un numero vecchio può contarne meno e mai far passare una soglia che non si
  raggiunge. ⚠️ Come i rating, **si aggiornano solo al login**: il modulo legge l'ultima fotografia.
- `IvaoUserProfile` le porta come **proprietà `init`**, non come parametri del record: chi lo costruisce nei test che ci sono
  (`PirepTests.People`, `RefDataSyncTests`, `UserSyncTests`) non cambia.

### 2.2 Il vocabolario dei rating (n.4)

- **`Core/Ivao/RatingVocabulary.cs`**, nel perimetro IVAO: `RatingKind` (`Atc`, `Pilot`); `Rating` — tipo, numero di IVAO, sigla,
  se ha un training pratico, il tipo di postazione su cui si allena, la chiave del nome; e **`RatingVocabulary`**, che risponde
  alle domande del modulo: `Ladder` (l'ordine), `Find`, `NextTraining` («il successivo a questo, se ha un training pratico»),
  `IsAtLeast` («questo è almeno quello?»). La terza domanda del design, «le postazioni per questo rating», è
  `Find(...).PositionType`, che la directory delle postazioni di A2 incrocerà con i dati di IVAO.
- **`IvaoRatings.Vocabulary`**: i dati della tabella di §1, registrati come singleton in `AddIvaoIntegration`. **L'ordine è quello
  della lista**, non il numero, anche se oggi coincidono: l'ordine è un dato del vocabolario (design §1.7), e così lo dice.
  **Hanno un training pratico** ADC, APC, ACC e PP, SPP, CP (design R.2 e §1.7; i training di PATS sono 5–7). **Il tipo di
  postazione**, fatto confermato da `dalberone` il 25 settembre: ADC → `TWR`, APC → `APP`, ACC → `CTR`; un tipo per rating.
- **Una classe costruita da dati, non un'interfaccia**: i test del modulo costruiscono un vocabolario di prova con la stessa
  classe (design §10), e un'interfaccia con la sua sola implementazione IVAO sarebbe lo stesso comportamento scritto due volte.
  «SEC, SAI e CAI fanno tutti i training» non è scritto da nessuna parte: segue dall'ordine.
- **I nomi sono chiavi del nucleo**, `ratings.Atc.ADC`, `ratings.Pilot.PP`, in `locales/*/common.json` (la chiave è il valore
  dell'enum, come `visibility.Public`). **In italiano restano i nomi di IVAO, in inglese**, come i livelli dello staff: scelta di
  `dalberone` il 25 settembre.

### 2.3 `RatingBadge`

- **Il ventiquattresimo dell'elenco chiuso** (`web/src/shared/ui/catalog.ts`), in `shared/ui/badges.tsx` accanto a
  `DepartmentBadge`, con la sua sezione nella galleria `/staff/admin/ui-kit` e la sua riga in `docs/UI-GUIDELINES.md` §3, che fino a
  oggi lo rimandava ai moduli. L'aveva già deciso Carmine (nota di A0, §2 punto 1): **un badge di testo, nessuna immagine di IVAO**.
- **La sigla è il testo**, perché è quello che lo staff dice ad alta voce, come il codice di un dipartimento; il nome tradotto è
  il `title` e il testo per uno screen reader; una sigla che le lingue non conoscono mostra la sigla anche lì. Un colore per tipo,
  ATC e pilota.
- **Prende `kind` e `shortName`**, non un numero: la sigla la manda il server, dal vocabolario, e il client non ha una copia della
  conoscenza di IVAO.

### 2.4 Il banco e2e (n.8)

- **`E2ESignIn`**: ogni personaggio può avere rating ATC e pilota e ore, in una classe base comune (`E2EPersonOptions`, con VID e
  nome, che oggi erano scritti tre volte); e c'è **un quarto personaggio, il trainer** (`?as=trainer`), con una posizione di
  trainer del TD e una casella di Mailpit. La risposta di `/e2e/signin` dice anche rating e ore, così una corsa che fallisce dice
  chi era.
- **`web/scripts/e2e-server.mjs`**: il pilota del banco diventa anche **il trainee** — AS3 e FS3, quindi il prossimo è ADC o PP, e
  ore sopra ogni soglia plausibile —; **il trainer** ha SEC e ATP, sopra ogni rating allenato.
- **Perché un personaggio nuovo e non il coordinator del web**: il design §2.4 propone come trainer lo staff del training (HQ, TC,
  TAC, TA, trainer), e il WM non ne fa parte. Il coordinator e l'assistente dei tour non cambiano, né le spec che li usano.

### 2.5 Le misure diventano fixture

`tools/record-ivao-fixtures.mjs` ha due modalità nuove. **`--me <asVid>`** fa il login come l'hub (codice con PKCE, il client e
gli scope di `config/ivao-oauth.json`) ascoltando sull'indirizzo di ritorno registrato, e scrive solo i campi che l'hub legge, con
la persona tolta: VID, nomi e indirizzo diventano `<asVid>` e segnaposto, le posizioni di staff un elenco vuoto, le ore restano
nella forma vera con valori inventati; i rating restano come IVAO li manda. **`--positions <name> <ICAO...>`** scrive le
postazioni degli aeroporti e i settori dei FIR nominati, senza il contorno disegnato. Le fixture: `users-me-790001.json`,
`atc-positions-sample.json` (Fiumicino e Malpensa), `subcenters-sample.json` (Roma). I test del vocabolario le leggono: i rating
del profilo vero stanno nel vocabolario con la stessa sigla, e ogni tipo di postazione di un rating allenato è un tipo che IVAO usa.

## 3. Alternative scartate

| Alternativa | Perché no |
|---|---|
| I secondi di IVAO nelle colonne | ogni lettore dovrebbe sapere che IVAO conta in secondi; il modulo, le soglie e le pagine parlano di ore |
| Svuotare le ore quando IVAO non le manda, come i rating | le ore crescono soltanto: la fotografia di prima non fa mai passare una soglia, il vuoto perde un dato vero |
| Leggere anche le ore da staff | nessuno scopo: il minimo dei dati IVAO |
| Un'interfaccia `IRatingVocabulary` con l'implementazione IVAO | due tipi per un comportamento; il vocabolario di prova del modulo è la stessa classe con altri dati |
| L'ordine dei rating dal loro numero | oggi coincidono, ma l'ordine è un dato del vocabolario (design §1.7), non una proprietà dei numeri di IVAO |
| I nomi dal profilo IVAO (`name`) | arrivano solo per i due rating di chi entra, e non si traducono |
| Il vocabolario anche in TypeScript | la stessa conoscenza di IVAO scritta due volte; il badge riceve la sigla dal server |
| Il tipo di postazione nelle impostazioni del modulo | è il `facilityRatings` tolto da Carmine in A0 |
| Il trainer del banco nel coordinator del web o nell'assistente dei tour | il primo non è staff del training, il secondo serve alle spec dei tour con un'altra posizione |

## 4. Che cosa si tocca

Tutto del nucleo, ed è il perché di questa nota (`core-guard`):

- **Le ore**: `Core/Auth/IvaoUserProfileReader.cs`, `Core/Auth/UserSyncService.cs` (`IvaoUserProfile` e la scrittura),
  `Core/Auth/HubUser.cs`, `Core/Data/Configurations/HubSchemaConfiguration.cs`, la migrazione `AddConnectionHours` e lo snapshot
  del contesto del nucleo.
- **Il vocabolario**: `Core/Ivao/RatingVocabulary.cs` (nuovo), `Core/Ivao/IvaoServiceCollectionExtensions.cs` (la
  registrazione), `locales/en/common.json` e `locales/it/common.json` (i nomi).
- **Il badge**: `web/src/shared/ui/badges.tsx`, `index.ts`, `catalog.ts`, `web/src/features/admin/uiKitSections.tsx`,
  `docs/UI-GUIDELINES.md`.
- **Il banco**: `IvaoHub.Web/E2E/E2ESignIn.cs`, `web/scripts/e2e-server.mjs`, `web/e2e/full/README.md`.
- **Le misure**: `tools/record-ivao-fixtures.mjs`, `tests/fixtures/ivao/README.md` e le tre fixture nuove.
- **Test nuovi**, nessun test esistente cambiato: il vocabolario e le ore del lettore (unità), la scrittura delle ore al login
  (integrazione, VID `790001–790099`), il badge (`RatingBadge.test.tsx`), il banco (`web/e2e/full/training-bench.spec.ts`).

I contesti dei moduli non mappano `hub_users` (solo le proiezioni e l'audit), quindi le colonne nuove non cambiano il modello di
nessun modulo. Il conteggio della galleria che un test scrive a mano è quello dei **blocchi** (`uiKit.test.ts`, 38), non dei
componenti: il badge non lo tocca.

## Da portare nel piano

- **§7, tabella `users`**: `hours_atc`, `hours_pilot` — le ore di connessione come controllore e come pilota, dal profilo IVAO a
  ogni login, in ore; restano le ultime quando IVAO non le manda.
- **§10, riga «Profilo al login»**: il profilo porta anche le ore (`hours`, secondi, per tipo), e l'hub legge `atc` e `pilot`.
- **§9.1, riga «Dati di riferimento IVAO»**, e **§4.2** (il perimetro IVAO): il vocabolario dei rating di IVAO, in
  `Core/Ivao/RatingVocabulary.cs`; un modulo gli fa domande e non scrive numeri di rating (lo annunciava la nota di A0).
- **§8.3**: `RatingBadge` è nell'elenco chiuso, il ventiquattresimo.
- **§6.4, la riga GDPR** («dati IVAO minimi»), e **§14, rischio «GDPR»**: le ore sono un dato IVAO in più conservato con uno
  scopo, le soglie del training, come l'email dal 6 settembre per il servizio notifiche; la cancellazione dei dati di una persona le
  toglie con la riga di `hub_users`.
