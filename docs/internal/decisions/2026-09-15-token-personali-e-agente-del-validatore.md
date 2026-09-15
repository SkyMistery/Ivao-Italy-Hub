# I token personali e il contratto dell'agente del validatore

**Data:** 15 settembre 2026 — fase T0 di M2 (verifiche fatte la sera del 15)
**Stato:** il **che cosa** è deciso da Carmine nel design dei tour (`05-design-m2.md` §6.6): i controlli sulla geometria della rotta girano sul
PC del validatore, con Navigraph, e l'app **manda subito gli esiti** all'hub. **Chi adatta l'app**: Claude, in una fase dopo T19, nel
repository dell'app (Carmine, 15 settembre). La **forma** qui sotto è di Claude, da confermare nella revisione della PR di T0. ⚠️ La licenza di
Navigraph è verificata solo in parte (§4).
**Regola applicata:** `CLAUDE.md` §5, caso **(c)**: un secondo modo di autenticarsi e un contratto con un prodotto esterno.
**Estensione del nucleo** n.14 di `05-design-m2.md` §11. Fasi **T19** (hub) e **T21** (l'app).

## 1. Che cosa serve

- L'app sul PC del validatore (oggi `AutomaticValidatorTour`, Python) legge un PIREP dall'hub, fa `semicircularLevels` e `atcCoverage`
  con i dati di navigazione del validatore, e scrive gli esiti, che nell'hub **suggeriscono** errori come quelli del server.
- L'app **vede solo quello che vede il validatore** e scrive solo dove lui può decidere: niente PIREP suoi, niente tour su cui non è abilitato.
- Letto il 15 settembre: l'app oggi legge i dati di navigazione **dal database locale** (Navigraph FMS Data Manager, o il database
  Navigraph di Little Navmap); il provider dell'API REST di Navigraph è uno stub non implementato.

## 2. Perché nessun meccanismo esistente basta

L'hub ha un solo modo di sapere chi chiama: il **cookie** `hub.auth` dopo il login IVAO (`Core/Auth/IvaoAuthenticationExtensions.cs`).
Un programma sul PC non fa un login OIDC nel browser a ogni avvio, e dare all'app il cookie del browser vorrebbe dire darle **tutto** il
back office. Nessun token dell'hub esiste oggi.

## 3. La decisione

### 3.1 Il token personale (nucleo)

- **`hub_personal_tokens`**: `id`, `vid`, `name` (lo sceglie l'utente: «PC di casa»), `audience` (a che cosa serve: per ora solo
  `flightops.agent`), `token_hash` (SHA-256; il token in chiaro si mostra **una volta**, alla creazione), `prefix` (i primi caratteri,
  per riconoscerlo nella lista), `created_at`, `expires_at`, `last_used_at`, `revoked_at`. `IAuditable`, `[Audited]`.
- **Si crea e si revoca dalla pagina dell'utente** (`/me/tokens`, lista e form generati), solo se ha un permesso che l'`audience`
  richiede (`Tours.Validate` per `flightops.agent`). Scadenza a scelta **fino a 90 giorni**.
- **Uno schema di autenticazione a sé** (`Bearer`, token con il prefisso `hubpat_`), accettato **solo** dai gruppi di endpoint che
  dichiarano quella `audience`. Sul resto dell'API un token non vale niente: un token rubato non apre il back office.
- **Il token non porta permessi**: a ogni richiesta l'identità si costruisce **come per il cookie** (`HubClaims.BuildIdentity`), dalle
  posizioni e dai grant **di adesso**. Un grant tolto o sospeso smette di valere per il token alla richiesta dopo.
- ⚠️ **Le posizioni si aggiornano al login**: un validatore che lascia lo staff e non rientra più terrebbe le posizioni vecchie. Quindi un
  token **vale solo se il suo utente ha fatto login negli ultimi 30 giorni**; oltre, l'app riceve 401 con un messaggio che dice di
  entrare nell'hub. Proposta di Claude, da confermare.
- **Audit**: ogni scrittura fatta con un token è auditata come le altre, con l'id del token nel contesto; le letture aggiornano
  `last_used_at` e una riga di log, non una riga di audit per ogni PIREP letto.

### 3.2 Il contratto dell'agente (modulo)

- **Versione nell'intestazione, non nell'indirizzo**: il piano esclude `/api/v1` perché la SPA e il backend viaggiano insieme, e resta
  vero. L'app no: manda `Hub-Agent-Contract: 1`, l'hub risponde con la versione che parla. `GET /api/flightops/agent/contract` dice versione
  corrente, versioni accettate e controlli che l'hub conosce. Dentro una versione i cambi sono **solo additivi**; un cambio che rompe è la
  versione 2, accettata accanto alla 1 per almeno una release.
- **Lettura**: `GET /api/flightops/agent/pireps?queue=…` (i PIREP in coda sui tour che il validatore può decidere, non i suoi) e
  `GET /api/flightops/agent/pireps/{id}` (tutte le revisioni del piano, le tracce, i parametri congelati delle regole, la fetta di archivio
  ATC dell'intervallo del volo, gli aeroporti e le piste `ref_`).
- **Scrittura**: `POST /api/flightops/agent/pireps/{id}/checks` con, per ogni controllo, `checkKey`, `outcome` (`Passed`, `Failed`,
  `Unavailable`), `evidence` (testo, fino a 2000 caratteri) e la versione dell'app. Finiscono in `fo_check_results` con `ran_by = agent`;
  **rimandare lo stesso controllo sostituisce** l'esito precedente dell'agente per quel PIREP (niente doppioni). Autorizzato dall'**unico
  handler**: `Tours.Validate` con lo scope del tour, e mai sul proprio PIREP (nota `2026-09-15-permessi-su-una-riga-e-chi-ha-interesse`).
- **Il contratto non conosce i controlli**: una `checkKey` che il catalogo degli errori non collega a niente si salva e non suggerisce
  niente. Un controllo nuovo dell'app è una chiave nuova e un errore collegato nel catalogo; l'hub non cambia.
- **Documentato per chi scrive un agente**: `docs/agent-contract.md` (inglese), con esempi di richiesta e risposta, generato in parte
  dall'OpenAPI che l'hub produce già alla build.

## 4. La licenza di Navigraph — verificata a metà

- **I termini per gli abbonati** (`navigraph.com/legal/terms-of-service`) non si leggono con una lettura automatica (la pagina è
  JavaScript). Dal portale degli sviluppatori: la licenza è **personale e non trasferibile**.
- **I termini per gli sviluppatori** (16 dic 2021) valgono per chi usa l'**API** di Navigraph con un client id: ammettono uso personale e
  comunitario, ma vietano app «il cui scopo principale è consegnare dati», la costruzione di un database proprio e le rappresentazioni
  grafiche dei dati. **L'app di oggi legge un database locale e non usa l'API**: forse quei termini non la riguardano, ma quelli degli
  abbonati sì, e non sono stati letti.
- **Nessuna pagina dice** se un esito derivato mandato a un server è una «redistribuzione». I nomi dei fix e delle aerovie sono assegnati
  da ICAO e pubblicati negli AIP: non sono di Navigraph.
- **La forma che tiene basso il rischio**, e che il contratto chiede all'app:
  - **può** contenere: esito, chiave del controllo, i punti **che il pilota ha scritto** nel piano, la rotta magnetica arrotondata per
    tratto, il livello e il verdetto pari/dispari, il codice del paese o del FIR, il ciclo AIRAC;
  - **non può** contenere: coordinate, distanze abbastanza precise da ricavare posizioni, punti che il pilota non ha scritto (quelli
    espansi da un'aerovia), la declinazione magnetica, geometrie, mappe.
- ⚠️ **Da fare prima di distribuire l'app ad altri validatori** (T21): una mail a `dev@navigraph.com` con questa forma esatta, e la
  risposta scritta conservata in `docs/internal/decisions/`. È un messaggio verso l'esterno: lo manda Carmine, o Claude su sua conferma.

## 5. Alternative scartate

| Alternativa | Perché no |
|---|---|
| L'app solo in locale, senza scrivere niente nell'hub | scartata da Carmine il 15 settembre (design §6.6): si perdono suggerimento, registro e misura dell'errore |
| Il cookie del browser copiato nell'app | tutto il back office nelle mani di un programma; scade ogni 12 ore |
| Un login OAuth dell'app con IVAO, e l'hub che si fida del token IVAO | l'hub non riceve token IVAO per le sue API; e i permessi sono dell'hub, non di IVAO |
| Un token con i permessi fotografati alla creazione | un validatore tolto resterebbe validatore per 90 giorni |
| Versione nell'indirizzo (`/api/flightops/agent/v1/…`) | contraddice la regola «niente `/api/v1`» per un solo cliente; l'intestazione basta |
| I controlli sulla rotta sul server con il «trucco del `DCT`» | sbaglia proprio dove serve (design §6.6); il server non ha i dati di navigazione |

## 6. Che cosa si tocca

- **Codice (T19)**: `PersonalToken` + migrazione `AddPersonalTokens`; lo schema di autenticazione e la policy per `audience`;
  `/me/tokens`; il gruppo `/api/flightops/agent`; `fo_check_results.ran_by` e la versione dell'agente; `docs/agent-contract.md`.
- **Test**: un token vale solo sulla sua `audience` e non su `/api/staff`; revocato, scaduto o con l'ultimo login oltre 30 giorni dà 401;
  un grant tolto vale alla richiesta dopo; l'agente non scrive sul proprio PIREP né su un tour non abilitato; rimandare un esito lo
  sostituisce; una versione del contratto sconosciuta dà una risposta che dice quali sono accettate.
- **Fuori da questo repository (T21)**: l'app legge dall'hub e scrive gli esiti; la mail a Navigraph prima di distribuirla.
- **Piano** 0.79: §6 (un secondo schema, solo per `audience`), §9.7 (il contratto con un prodotto esterno), §16.10 (l'eccezione
  dell'intestazione). **Design** `05-design-m2.md` §6.6, §15.2 n.16.
