# I token personali, e come si divide T19 (T19a)

**Data:** 24 settembre 2026 — fase T19a di M2
**Stato:** **decisa** (Carmine, 24 settembre 2026: quattro risposte in apertura, §2; il resto è la nota del 15 settembre, già decisa, o una
scelta tecnica dichiarata qui sotto)
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: il meccanismo è deciso nella nota `2026-09-15-token-personali-e-agente-del-validatore`
(caso c, piano 0.79); qui si scrive, e si estendono quattro meccanismi del nucleo (§4). Un difetto trovato per strada (§5).

## 1. Che cosa serve

Piano di implementazione, parte C, T19: nel nucleo `hub_personal_tokens`, lo schema `Bearer` per `audience`, l'identità costruita come per
il cookie, l'ultimo login entro 30 giorni, `/me/tokens`, l'audit delle scritture; nel modulo `/api/flightops/agent` con `Hub-Agent-Contract`;
`docs/agent-contract.md`. «Fatta quando»: con un token creato da `/me/tokens`, una `curl` legge un PIREP e scrive un esito che la pagina di
validazione mostra.

## 2. Le risposte di Carmine

| Domanda | Risposta | Scartato |
|---|---|---|
| T19 si divide? | **Sì, come T14 e T15**: **T19a** il nucleo (token, schema, policy, `/me/tokens`), **T19b** il modulo (contratto dell'agente, documento, la `curl` del «fatta quando»). | Una PR sola, nucleo e modulo insieme. |
| Il dettaglio del PIREP per l'agente | **DTO suoi, versionati** (`AgentPirepDto` e simili, congelati nella versione 1), riempiti dagli stessi servizi della pagina di validazione. Vale per T19b. | Riusare `ReviewDto`: ogni cambio della pagina diventerebbe un cambio del contratto con l'app. |
| L'agente scrive su un PIREP già deciso? | **Solo in coda o in revisione** (`Queued`, `InReview`); su uno deciso 409. Una riapertura lo rimette in coda e l'agente può riscrivere. Vale per T19b. | Sempre: cambierebbe i suggerimenti di un PIREP deciso. |
| Una richiesta senza `Hub-Agent-Contract` | **400 con le versioni accettate**, su tutto tranne `GET /agent/contract`. Vale per T19b. | Assente vuol dire la versione corrente: un'app vecchia passerebbe in silenzio alla versione 2. |

## 3. Com'è fatto (T19a)

- **`hub_personal_tokens`** (`Core/Auth/PersonalTokens.cs`), `[Audited]` e `IAuditable`: `vid`, `name`, `audience`, `token_hash` (SHA-256 in
  esadecimale, unico), `prefix` (i primi undici caratteri, `hubpat_` e quattro), `expires_at`, `last_used_at`, `revoked_at`. Il token è
  `hubpat_` + 32 byte casuali in base64url: con 256 bit casuali un sale o un hash lento non proteggerebbero niente. Si mostra **una volta**,
  nella risposta della creazione.
- **Le regole stanno in un posto**, `PersonalTokens`: il nome (obbligatorio, 64 caratteri), l'audience fra quelle concesse, i giorni **da 1 a
  90**, **dieci token vivi** al massimo per persona (scelta di Claude: abbastanza per qualche computer, non una perdita che nessuno segue);
  e il riconoscimento: sconosciuto, revocato, scaduto, o con l'**ultimo login oltre 30 giorni** → rifiutato.
- **Lo schema `HubToken`** (`PersonalTokenAuthentication.cs`) non è mai lo schema di default. Legge `Authorization: Bearer`, ricostruisce
  l'identità **a ogni richiesta** con `UserSyncService.LoadAsync` e `HubClaims.BuildIdentity` — le posizioni e i grant di adesso — e ci
  aggiunge due claim, `aud` e `tok` (l'id del token). Un rifiuto è **401** con `WWW-Authenticate: Bearer error="invalid_token"` e un
  `ProblemDetails` con `code` (`unknown`, `revoked`, `expired`, `signInAgain`) e la frase nella lingua della divisione.
- **La policy di un'audience** è `PersonalTokenPolicy.For("flightops.agent")`, risolta dall'unico policy provider: **solo** lo schema del
  token, il claim `aud` uguale, e il permesso dell'audience tenuto da qualche parte (`PermissionRequirement` senza risorsa, l'unico handler).
  Che cosa si può fare su una riga resta la domanda all'unico handler con la riga in mano. Tutte le altre policy chiedono il cookie: con
  il solo token `/api/me` vede un visitatore e il back office risponde 401. Nemmeno un token crea un token.
- **`/me/tokens`**: la lista è la vista personale del motore CRUD (`Participating`, i token attivi per default, `filter[active]=false` gli
  altri); due verbi, `POST /api/me/tokens` (restituisce la riga e il token) e `POST /api/me/tokens/{id}/revoke` (204, o 404 per un token di
  un altro). La pagina è la lista generata, il form generato (l'audience è una scelta fra quelle che il bootstrap offre) e un `Notice` con il
  token da copiare. Il link sta sotto la dashboard `/me` **solo** per chi ha almeno un'audience.
- **La guardia contro le richieste da altri siti** lascia passare una richiesta con `Authorization: Bearer`: il browser non la aggiunge da sé
  e un'altra pagina non può metterla senza una preflight che questo host non concede. Con il cookie resta `X-Requested-With: hub`.
- **I log**: ogni uso di un token è una riga di log (id del token, VID, metodo, percorso), non una riga di audit.

## 4. I meccanismi estesi

1. **`IModule.TokenAudiences`** (`TokenAudienceDescriptor(Key, RequiredPermission)`, catalogo `TokenAudienceCatalog`): un'audience si chiama
   come il modulo (`flightops.agent`), come i tipi di notifica. Il nucleo non ne dichiara e **non nomina il modulo dei tour**; le parole
   stanno nel file di lingua del modulo, sotto `tokenAudiences.{nome}` (in T19a la pagina mostra la chiave, che è un identificatore).
2. **`/api/me` → `user.tokenAudiences`**: le audience per cui chi legge può creare un token. Serve alla pagina e al link.
3. **`[NotAudited]`** su una proprietà: un salvataggio che cambia **solo** colonne così non è un cambio della riga — niente audit, `updated_at`
   fermo. Il primo è `last_used_at`, scritto al massimo una volta al minuto.
4. **`hub_audit_log.token_id`**: una scrittura fatta con un token dice quale. Revocare quello giusto lo richiede.

## 5. Trovato: l'audit dei moduli non c'era

L'interceptor scrive una riga di audit solo in un contesto che mappa `hub_audit_log`, e **nessun contesto di modulo la mappava**: ogni riga
`[Audited]` di un modulo — un PIREP, un tour, una regola — è stata scritta **senza audit** da quando esiste, mentre il commento di
`ModuleDbContext` diceva il contrario. Nessun test lo guardava. Trovato perché la nota del 15 settembre chiede che le scritture dell'agente
siano auditate, e l'agente scrive nel contesto dei tour. **Corretto** come le tabelle delle proiezioni in T4: `ModuleDbContext` mappa
`hub_audit_log` con la configurazione del nucleo, fuori dalle migrazioni del modulo (la migrazione `MapAuditLog` dei tour è vuota: porta nello
snapshot le tabelle escluse, compresi i contatti di T14, come HANDOFF aveva previsto). Il test lo tiene: `SampleItem` è ora `[Audited]`.
⚠️ Le righe dei moduli scritte finora restano senza audit: non si ricostruiscono.

## 6. Test

Integrazione `PersonalTokenTests` (VID 780091–780094, audience `sample.agent` del modulo di prova): il token si mostra una volta e solo per
un'audience concessa; apre la sua audience e nient'altro (`/api/me`, `/api/sample/items`, `/api/admin/grants`, `/api/me/tokens`; e il cookie
non apre gli endpoint del token); i quattro rifiuti con il loro `code`; un grant tolto vale alla richiesta dopo (403); la riga è la risposta
dell'unico handler (tour non abilitato e riga del proprio VID: 403) e la scrittura è auditata **nel contesto del modulo** con `token_id`;
l'uso aggiorna `last_used_at` senza una riga di audit. Unit: la policy di un'audience è del solo schema del token; un'audience sconosciuta o
chiamata male è un errore all'avvio.

## 7. Che cosa resta a T19b

`GET /api/flightops/agent/contract`, la coda e il dettaglio del PIREP con i DTO dell'agente, `POST …/checks` con sostituzione e
`ran_by = agent` e la versione dell'app (colonna nuova), `Hub-Agent-Contract`, l'audience `flightops.agent` (con `Tours.Validate`) e la sua
parola, `docs/agent-contract.md`, il giro e2e (token creato dalla pagina, `curl`, esito sulla pagina di validazione). Da decidere lì: se
`fo_check_results` diventa `[Audited]` (oggi non lo è: un esito del server si riscrive a ogni giro del job).
