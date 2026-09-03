# HANDOFF — stato di M0

> Documento **interno** (italiano). Si aggiorna alla fine di ogni fase (piano di implementazione §A.6).
> Fonte di verità: `00-piano-di-progettazione.md`; perimetro e firme: `01-design-m0.md`; ordine: `02-piano-implementazione-m0.md`.

**Ultimo aggiornamento:** 3 settembre 2026 — fine **F5** (`MapCrud` e `links` lato server).
**Repository:** https://github.com/SkyMistery/Ivao-Italy-Hub (pubblico).
**Piano:** v0.24. **Design:** v1.2 (due precisazioni da portarci, §5). **Test:** 244 verdi
(194 unit + 50 integrazione).

| Fase | Stato |
|---|---|
| F0 bootstrap | mergiata (PR #1) |
| F1 configurazione, avvio, DB | mergiata (PR #2) |
| F2 auth BFF, ruoli, permessi, `/api/me` | mergiata (PR #3 e #4) |
| F3 `IvaoApiClient` e dati `ref_` | mergiata (PR #5) |
| F4 spina dorsale del dominio | mergiata (PR #6) |
| F5 `MapCrud` e `links` (server) | **PR #8 aperta, CI verde**, branch `m0/f5-mapcrud-links` |
| **F6 spina dorsale frontend** | **prossima**, dopo il merge di F5 |

### Le prime tre cose da fare in una sessione nuova

1. **Leggere e mergiare la PR #8** (<https://github.com/SkyMistery/Ivao-Italy-Hub/pull/8>), con
   squash come le precedenti. La CI è verde (`build-test`, 2m17s): 244 test, il diff di
   `schema.d.ts` non si muove, e il pacchetto pubblicato porta `locales/`, gli `.example.json`,
   `LICENSE` e `NOTICE`.
2. **Confermare o correggere le due note di §5** marcate «da confermare». Non bloccano F6: sono due
   frasi del design da riformulare, e finché non sono confermate il design resta a v1.2 così com'è.
   Chi le conferma corregge `01-design-m0.md` §7.4, §9 punto 12 e §3.1 — le riformulazioni esatte
   sono già scritte in fondo a ciascuna nota.
3. **Aprire F6**: `git checkout main && git pull` (dal checkout principale, non da un worktree),
   poi `git checkout -b m0/f6-frontend-backbone` e il prompt di §C con `<N>` → `6`.

**Perimetro di F6** (§D del piano): i tre layout, le tre ricette del router, `DataList` e
`SchemaForm`, `LocaleFields`, `useProblemDetails`, il back-office di `links` **senza una riga di JSX
di tabella o di form**, `/staff/admin/ui-kit`, `docs/UI-GUIDELINES.md`.

Quattro cose che F6 eredita da F5 e deve usare, non riscrivere:

1. **I tipi dell'API non si scrivono a mano.** `web/src/shared/api/schema.d.ts` è generato
   (`pnpm gen:api`) dal documento OpenAPI che scrive `dotnet build`, ed è committato; la CI lo
   rigenera e fallisce sul diff. `bootstrap.ts` ora è solo un elenco di alias di quel file.
2. **I parametri di lista sono un tipo, non una convenzione.** `CrudListRequest` (`page`,
   `pageSize`, `sort`, `dir`, `q`) sta nell'OpenAPI, quindi i search params tipizzati della
   ricetta 2 devono coincidere con quelli. `filter[nome]=valore` non è nel documento perché i suoi
   nomi sono le proprietà dell'entità: l'elenco ammesso è `CrudOptions.Filterable`, e un nome fuori
   elenco prende 400.
3. **Un campo tradotto si riconosce dal contratto.** Nello schema, `LocalizedString` porta
   `x-localized: true`: è quello che `SchemaForm` deve leggere per disegnare `LocaleFields`, invece
   di indovinare dal nome del campo.
4. **Gli errori arrivano come chiavi i18n.** `errors[campo] = ["errors.localized.missing"]`, e
   quando mancano delle lingue l'estensione `localized` dice **quali**: `useProblemDetails` deve
   risolvere la chiave e usare quell'estensione, non inventarsi un messaggio.

Serve solo Docker attivo: le credenziali IVAO ci sono e funzionano, ma da F4 in poi non le usa
nessuno.

---

## 1. Come si avvia (locale)

```bash
cp config/ivao-oauth.example.json config/ivao-oauth.json   # e compilarlo; mai committato
docker compose up -d                                        # MariaDB 11.4.10 + Mailpit
dotnet run --project src/IvaoHub.Web                        # API su :5000, migra il DB da sola
cd web && pnpm install && pnpm dev                          # SPA su :5173 (proxy /api, /auth, /health)
```

Il login vero si prova da <http://localhost:5173>: «Accedi con IVAO» → consenso → ritorno su `/me`.
Perché funzioni, `LoginUrl` e `RedirectUri` in `config/ivao-oauth.json` devono coincidere **carattere per
carattere** con quelli registrati su IVAO per quel client (in locale: `http://localhost:5173/auth/login` e
`http://localhost:5173/auth/callback`).

Controlli:

```bash
dotnet build IvaoHub.sln                                    # scrive artifacts/openapi/IvaoHub.Web.json
cd web && pnpm gen:api && git diff --exit-code               # rigenera schema.d.ts: deve non muoversi
dotnet test --solution IvaoHub.sln --configuration Release  # richiede Docker (Testcontainers)
cd web && pnpm lint && pnpm format:check && pnpm typecheck && pnpm test && pnpm i18n:check && pnpm build
dotnet publish src/IvaoHub.Web -c Release -r linux-x64 --self-contained -o artifacts/publish
```

⚠️ **`dotnet build` esegue il nostro `Program` per un istante.** È così che
`Microsoft.Extensions.ApiDescription.Server` legge gli endpoint (misurato: se non arriva a
`app.Run()`, il documento esce con `"paths": { }`). Non tocca il database e non chiede il client
OAuth, perché `HubConfiguration.IsOpenApiDocumentGeneration` gli toglie da davanti la validazione di
Production, `ValidateOnStart` dell'OAuth e `InitializeAsync`, e apre una porta effimera sul loopback
invece della 5000. Nota: `docs/internal/decisions/2026-09-03-openapi-a-build-time.md`.

⚠️ **Se `dotnet test` dice «Zero tests ran» con exit code 5, non crederci.** È successo il 3 set 2026 su
Windows in **Debug**: il comando tornava in 110 ms senza eseguire niente, mentre il binario lanciato a
mano (`tests/IvaoHub.UnitTests/bin/Debug/net10.0/IvaoHub.UnitTests.exe`) eseguiva e passava tutti i
test. In **Release**, che è come gira la CI, funziona. Causa non trovata (sospetto il canale fra
`dotnet test` e l'host di Microsoft.Testing.Platform); non è un problema del repository. Regola
pratica: si verifica come verifica la CI, in Release, e in caso di dubbio si lancia il binario.

Nuova migrazione (**solo additiva**, mai modificare una già mergiata):

```bash
dotnet tool restore
dotnet dotnet-ef migrations add <Nome> --project src/IvaoHub.Core --startup-project src/IvaoHub.Core
```

## 2. Cosa c'è dopo F5

**Configurazione e avvio (F1)**: `config/division.json` versionato + esempi; opzioni validate prima di toccare
il DB; `Localized<T>` con converter EF e convenzione `_i18n`; `HubDbContext` su Pomelo pinnato a MariaDB
11.4.10; 16 tabelle dalla migrazione `Initial`; `HubPaths`, Serilog con correlation id, Data Protection su
`hub-keys/`, `diagnostics/startup.txt`; `/health` con ping DB e `/api/version`.

**Identità (F2)**:

- `StaffRoleMap.Parse(position, divisionCode, firIds)` copre **tutta** la tabella del piano §4.1, con
  l'ordine dei pattern che conta (`T01`–`T99` prima di `TA1`–`TA9`, poi `TAC`, poi `TC`). Una posizione non
  riconosciuta non si perde: resta in `hub_user_staff_positions`.
- `CorePermissions` (13 permessi, `global` dichiarato), `RolePermissionMatrix` (una tabella per livello) e
  `EffectivePermissionsCalculator` (derivati ∪ grant − deny, scadenza, sospensione, `Edit` implica `View`,
  mai globali via grant). Un permesso valido ovunque si salva **una volta sola** con dipartimento nullo:
  altrimenti Director e web team porterebbero ~90 claim nel cookie.
- BFF OIDC ereditato da vIPI: `code` + PKCE, nonce validato, `RequireState=false` con validator dedicato,
  `SaveTokens=false` con i token IVAO salvati **cifrati** in `hub_user_tokens`, `OnRemoteFailure` che manda
  a `/login-error` e **non** rimbalza al login.
- Cookie applicativo `hub.auth` (12 h scorrevoli, `HttpOnly`, `SameSite=Lax`) con claim compatti;
  `OnValidatePrincipal` confronta lo `stamp` con `hub_users.security_stamp` (cache 60 s, invalidata a mano
  da chi scrive) e rigetta il cookie all'istante quando cambia.
- `SuperadminService`: bootstrap da `division.json` **solo** se nel DB non c'è nessun superadmin, hash
  dell'insieme in `hub_division_settings` con riga di audit quando cambia, impossibile togliere l'ultimo.
- `GET /api/me` completo (utente, permessi effettivi, divisione, navigazione, versione); `modules` e
  `registries` restano vuoti fino a F8.
- Guardia CSRF: ogni `POST/PUT/PATCH/DELETE` sotto `/api` e su `/auth/logout` senza
  `X-Requested-With: hub` prende 403. Rate limiting 10/min per IP su `/auth/*`.
- Frontend: `shared/api/client.ts` (openapi-fetch, header CSRF, middleware 401), `features/me/queries.ts`,
  `AppShell` con login/logout, route `/me` e `/login-error` tradotte.
- **166 test verdi** (139 unit + 27 di integrazione su container `mariadb:11.4.10`).

**Dati di riferimento (F3)**:

- `IvaoApiClient` tipizzato, unico punto che parla con IVAO, con retry e circuit breaker dallo
  `StandardResilienceHandler`. Non lancia mai su chiamata fallita: uno snapshot vecchio di un giorno
  batte un sito fermo.
- `IvaoApiTokenProvider`: token `client_credentials` in cache fino a 60 s prima della scadenza; un
  token che vale meno del margine si usa e non si conserva. Gli scope dell'applicazione (`ApiScopes`)
  sono separati da quelli del membro: `client_credentials` non chiede `openid profile email`.
- `RefDataSyncJob`: upsert (mai duplicati) in `ref_ivao_centers` e `ref_ivao_airports` con il
  `raw_json` intero, riga in `hub_jobs_log`, cron 03:15 nel fuso della divisione, ed esecuzione
  all'avvio se le tabelle sono vuote. Se IVAO non risponde, la tabella resta com'era.
- `FixtureIvaoApiClient` con `Ivao:UseFixtures=true`, **rifiutato fuori da Development** sia alla
  registrazione sia nel costruttore. Le fixture stanno in `tests/fixtures/ivao/`.
- `IFirDirectory` con cache: `UserSyncService` non legge più la tabella a mano, e una posizione
  `LIRR-CH` diventa `FirChief` appena lo snapshot esiste.
- **182 test verdi** (152 unit + 30 di integrazione).

**Spina dorsale (F4)**:

- `Localized<T>` lato API: `LocalizedJsonConverterFactory` registrato una volta nelle `JsonOptions`
  globali (oggetto `{ "en": …, "it": … }`, un campo assente torna **vuoto e mai null**), e
  `LocalizedRules.Required(DivisionOptions)` per FluentValidation, che porta le lingue mancanti
  nello stato del fallimento invece di dire «non valido».
- Interfacce trasversali in `Division/DomainContracts.cs` (`IOwnedByDepartment`, `IVisible`,
  `IPublishable`, `IAuditable`, `IHasFir`) più gli attributi `[PermissionArea]` e `[Audited]`.
  Marcate: `Link` (tutte tranne publishable, più `IProjectable`), `ContentEntry` (tutte),
  `UserGrant` (solo `IAuditable`), `HubUser` (solo `[Audited]`). `ContentVersion` non ha
  dipartimento: eredita quello del contenuto.
- `HubSaveChangesInterceptor`, **l'unico**, registrato da `AddHubDbContext` e da
  `AddModuleDbContext<T>`: timestamp e audit di `IAuditable` (`created_*` scritti una volta sola e
  mai riscritti), **guardia di scrittura per dipartimento** (`{Area}.Edit`, area dall'attributo o
  dal nome del `DbSet`; spostare una riga fra dipartimenti richiede il permesso su entrambi) che
  lancia `ForbiddenDomainException`, righe di `hub_audit_log` per `[Audited]`, e le proiezioni.
- **Due tempi**: in `SavingChanges` si stampigliano le colonne, si applica la guardia e si raccoglie
  cosa scrivere; in `SavedChanges`, quando le righe nuove hanno finalmente un id, si scrivono audit
  e proiezioni con un secondo `SaveChanges` e un flag di rientranza per contesto. Se il chiamante
  non aveva una transazione, l'interceptor ne apre una propria e la chiude lui; se ce l'aveva, resta
  sua — in entrambi i casi la proiezione è **dentro** la transazione della scrittura.
- Global query filter su ogni entità che è insieme `IVisible` e `IOwnedByDepartment`, costruito per
  riflessione sul modello: `SeesEveryDepartment || Public || (membro && Members) || (staff && Staff)
  || (Department && dipartimenti dell'utente)`, più `status == Published` per le `IPublishable`. Le
  quattro proprietà stanno su `HubDbContext` e leggono `ICurrentUser` **quando la query parte**, non
  quando il contesto viene costruito.
- `BlockDocumentWalker` (puro `JsonNode`): `EnumerateBlocks`, `EnumerateSections`, `ExtractText` per
  lingua e `ValidateEnvelope` (versione, 1 MB, id unici, profondità ≤ 3, tipi noti, chiavi che solo
  un template può avere). Non conosce nessun blocco.
- `IProjectable` + `ProjectionWriter`: upsert per `(source_module, source_id)`, **una riga per
  lingua** in `cms_search_index`, una in `cms_calendar_entries`, e segnali award che non
  sovrascrivono mai uno già gestito. Una bozza proietta `null` per convenzione dell'interceptor,
  non per scelta dell'entità.
- `PermissionRequirement`, `HubPolicyProvider` (ogni nome del catalogo diventa una policy; un nome
  con il punto che non c'è nel catalogo è un'eccezione, non un divieto silenzioso) e
  `DepartmentAuthorizationHandler`, **l'unico handler**, registrati dentro `AddIvaoAuthentication`.
- **214 test verdi** (174 unit + 40 di integrazione). I sette test della spina dorsale di design §8
  stanno in `DomainBackboneTests`; girano sul `DbContext` e su `IAuthorizationService` veri, con un
  `TestCurrentUser` al posto del cookie perché F4 non ha ancora endpoint.

**`MapCrud` e `links` (F5)**:

- **`MapCrud<TEntity, TListDto, TDetailDto, TWriteDto>`** in `src/IvaoHub.Core/Data/Crud/`, unico
  motore CRUD del server. Genera `GET`/`GET {id}`/`POST`/`PUT {id}`/`DELETE {id}` con paginazione
  (`pageSize` **tagliato a 100**: una lista non è un modo di scaricare la tabella), `sort`/`dir` e
  `filter[...]` su **allow-list** (un nome fuori elenco è 400, non un filtro ignorato in silenzio),
  `q` sulle colonne dichiarate, validazione FluentValidation, 409 sulla concorrenza.
- **Due modalità, un ramo.** Dipartimentale quando l'entità è `IOwnedByDepartment`: lista filtrata
  sui `Departments` dell'utente (nessun filtro per chi ha `HasAllDepartments`; 403 per chi tiene il
  permesso ma non ha dipartimenti), e `AuthorizeAsync(entity)` su ogni riga. Globale altrimenti
  (`UserGrant`, `AuditLogEntry` in F8): solo la policy, nessun filtro, nessuna risorsa.
- **`AuthorizeAsync` è chiamato due volte su un `PUT`**: sulla riga com'è salvata (nessuno modifica
  ciò che non è suo) e sulla riga come diventerebbe (nessuno regala una riga a un altro
  dipartimento). Test: `MovingARowToAnotherDepartmentNeedsThePermissionOnBothSides`.
- **`ExtraWritePolicy`** è l'unico gancio di estensione, pronto per `Content.ManageTemplates` in F7.
- **`LocalizedQuery`**, il solo posto che legge una lingua da una colonna JSON in SQL: una
  `HasDbFunction` che diventa `JSON_UNQUOTE(JSON_EXTRACT(col, '$."it"'))`. Due dettagli che sono
  costati tempo e che non vanno rimossi: il parametro `field` ha bisogno di
  `HasParameter("field").HasStoreType("json")`, altrimenti la validazione del modello rifiuta
  `Localized<string>` come tipo non mappabile; e le due `SqlFunctionExpression` hanno bisogno di un
  **type mapping esplicito**, altrimenti «Expression … does not have a type mapping assigned».
- **La concorrenza non ha bisogno di un'interfaccia nuova.** Il motore trova la colonna di
  concorrenza dai metadati EF e, dopo `Apply`, ne copia il valore corrente in `OriginalValue`: una
  `rowVersion` vecchia finisce nel `WHERE`, non aggiorna nessuna riga e diventa 409. Un payload che
  non porta versione (`0001-01-01`) significa «la riga com'è adesso».
- **`ValidationProblem` con chiavi i18n**: `errors[campo] = ["errors.localized.missing"]`, più
  l'estensione `localized` che dice **quali lingue** mancano (dallo stato del fallimento di
  `LocalizedRules`). Il `title` invece è una frase, risolta dal `LocaleCatalog` nella lingua
  dell'utente: un chiamante che non è la nostra SPA riceve comunque qualcosa di leggibile.
- **`LocaleCatalog`** (`Core/Localization/`): legge `locales/{lang}/*.json` e li appiattisce in una
  mappa per lingua. I namespace sono un dettaglio di caricamento del client, non parte della chiave,
  quindi `nav.home` e `errors.localized.missing` si scrivono uguali sul server; due namespace che
  dichiarano la stessa chiave sono un'eccezione, non un ordine di lettura da indovinare.
- **`DomainExceptionHandler`** (`Core/Services/`) + `AddProblemDetails`: `ForbiddenDomainException`
  → 403 (e un warning nei log, perché se morde la rete dell'interceptor significa che una policy è
  stata dimenticata), `DbUpdateConcurrencyException` → 409.
- **Le policy dei permessi autenticano sul cookie**, non sullo schema di challenge di default: uno
  `RequireAuthorization("Links.View")` su `/api` deve rispondere **401**, non un 302 verso IVAO.
  Era un bug latente da F2 che nessuno poteva vedere finché non esisteva un endpoint protetto.
- **`/api/links`** in `Core/Content/LinksEndpoints.cs`: ~40 righe di configurazione e nient'altro.
  DTO più mapper Mapperly (`LinkDtos.cs`) e `LinkWriteDtoValidator` (titolo in tutte le lingue, URL
  assoluta http/https, `Sort ≥ 0`, lunghezze delle colonne).
- **OpenAPI a build-time** in `artifacts/openapi/IvaoHub.Web.json`, con il transformer che marca
  ogni `Localized<T>` come `x-localized: true` **e ne scrive la forma** (`additionalProperties`):
  un tipo con un converter proprio è opaco alla generazione dello schema e senza questo arriverebbe
  in TypeScript come `unknown`. `/api/me` e `/api/version` sono passati a `TypedResults` perché il
  loro payload finisse nel documento.
- **`pnpm gen:api`** → `web/src/shared/api/schema.d.ts`, committato, con uno step di CI che lo
  rigenera e fallisce sul diff. `client.ts` è ora `createClient<paths>` e `bootstrap.ts` è solo un
  elenco di alias: il tipo `ApiPaths` scritto a mano non esiste più.
- **Il pacchetto pubblicato** porta `locales/`, i `config/*.example.json`, `LICENSE` e `NOTICE`
  (target `PublishHubFiles`), verificato da uno step di CI.
- **244 test verdi** (194 unit + 50 di integrazione). I dieci di `MapCrudLinksEndToEndTests` girano
  sul cookie vero e sulle policy vere, con cinque identità: superadmin, coordinatore ED, advisor
  FOD, membro, anonimo.

**Provato contro l'API vera il 3 set 2026**: `client_credentials` **senza nessuno scope** basta per
`/v2/centers` e `/v2/airports/all`. Per la divisione IT tornano 7 centri (LIBB, LIMM, LIPP, LIRO,
LIRR, LIVK, LIZZ) e 221 aeroporti, tutti con le piste. Le fixture restano per la CI e per chi forka
senza credenziali.

## 3. Regole già attive (non aggirarle nelle fasi successive)

- ESLint blocca `fetch` fuori da `shared/api`, `<svg>` fuori da `shared/icons` e `blocks`, import dal nucleo
  verso `modules/` e import tra due moduli.
- Un campo tradotto è **solo** una colonna JSON `Localized<T>`: nessuna tabella `*_translations`.
- Gli enum si salvano come stringa; la conversione è registrata una volta sola.
- La concorrenza ottimistica passa da `HasRowVersion(...)`.
- Le migrazioni sono **solo additive**; `Initial` non si tocca più.
- L'identità si legge **solo** da `ICurrentUser`. Nessun endpoint guarda i claim a mano.
- Con IVAO parla **solo** `IvaoApiClient`: retry, circuit breaker e cache del token esistono una volta.
- Una configurazione che decide quale servizio usare si legge **quando il servizio viene costruito**,
  non quando viene registrato: un test host e un deploy aggiungono sorgenti dopo la registrazione.
  (Ci siamo cascati due volte: connection string in F1, fixture in F3.)
- Un `ClaimsPrincipal` dell'hub si costruisce **solo** con `HubClaims.BuildIdentity`: il login vero e il
  login finto dei test producono lo stesso cookie, quindi i test provano la cosa vera.
- Audit, timestamp e proiezioni **non si scrivono a mano**: li fa l'interceptor. Un servizio che
  aggiunge una riga in `hub_audit_log` o in `cms_search_index` sta duplicando un meccanismo.
- La visibilità **non si filtra in un endpoint**: c'è il global query filter. Il back office legge
  con `IgnoreQueryFilters`, e solo da `src/IvaoHub.Core/Data/Crud/` (test di architettura).
- Un handler di autorizzazione è **uno solo**; una policy è un permesso del catalogo. Chi ha bisogno
  di un permesso nuovo lo aggiunge al catalogo e alla matrice, non scrive un handler.
- Un contesto EF si registra **solo** con `AddHubDbContext`/`AddModuleDbContext<T>`: sono i due punti
  che agganciano l'interceptor.
- Un CRUD si espone **solo** con `MapCrud`. Un endpoint scritto a mano che pagina, filtra o
  autorizza una riga sta riscrivendo il motore; se il motore non copre il caso, si estende il
  motore (regola (b) di CLAUDE.md §5).
- La paginazione, l'allow-list di `sort` e `filter`, e il `ValidationProblem` vivono **una volta**,
  in `Core/Data/Crud/`. Nessun endpoint reinventa l'envelope della lista: è `PagedResult<T>`.
- I tipi TypeScript dell'API si **generano** (`pnpm gen:api`); nessuno li scrive a mano. Un endpoint
  nuovo che risponde `IResult` invece di `TypedResults` non finisce nel documento e quindi non
  esiste per il client: si tipizza la risposta.
- Il server non manda prose nella parte macchina di una risposta: `errors[campo]` sono chiavi i18n.
  Le frasi le risolve il `LocaleCatalog`, dagli stessi `locales/` della SPA.
- Un permesso del catalogo diventa una policy che autentica sul **cookie applicativo**: `/api`
  risponde 401 a chi non è autenticato, mai un redirect.

## 4. Scelte fatte finora che vale la pena conoscere

| Scelta | Perché |
|---|---|
| `global.json` contiene `"test": { "runner": "Microsoft.Testing.Platform" }` | xUnit v3 gira su MTP e l'SDK 10 rifiuta VSTest. Il comando è `dotnet test --solution IvaoHub.sln`. |
| `IvaoHub.Core` ha `FrameworkReference Microsoft.AspNetCore.App` | Il design mette nel nucleo `Auth/` (OIDC BFF) e `MapCrud`. |
| `config/ivao-oauth.json` è `optional: true`, la garanzia la dà il validatore | Il piano §6.1 vuole che le `Ivao__*` bastino da sole; l'app non parte lo stesso se manca tutto. |
| `HubPaths` risale fino a `config/division.json` | In produzione `config/` sta accanto all'app, in sviluppo alla radice del repo. |
| FULLTEXT dal modello (`.IsFullText()`), `row_version` `timestamp(6)` gestito dal server | I meccanismi esistono già nel provider; MariaDB non ha `rowversion`. |
| `redirect_uri` forzato da configurazione in `OnRedirectToIdentityProvider` | Dietro il proxy Vite e Cloudflare l'header `Host` non è affidabile, e IVAO confronta la stringa esatta. |
| I token IVAO letti da `TokenEndpointResponse` e non da `SaveTokens` | Con `SaveTokens=true` finirebbero nel cookie, cioè in ogni richiesta. Così restano solo cifrati a DB. |
| `RequireState = false` nel validator OIDC | ASP.NET Core non popola mai `ValidationContext.State`: con `true` il login si rompe con IDX21329 contro qualunque IdP. Lo `state` lo verifica l'handler col cookie di correlazione. |
| Un permesso valido su tutti i dipartimenti si salva con dipartimento `null` | Il cookie viaggia a ogni richiesta: il prodotto cartesiano permessi × dipartimenti lo farebbe esplodere. Un deny su un dipartimento espande comunque l'entrata, quindi morde lo stesso. |
| `UserGrant` non ha `granted_at`/`granted_by` separati | Sono `created_at`/`created_by` di `IAuditable`. |
| `IProjectable.Project()` prende un `ProjectionContext` (lingue, lingua di default, walker) | Un'entità EF non si fa iniettare niente, ma per proiettarsi un contenuto ha bisogno delle lingue della divisione e del walker. Le alternative erano cablare le lingue (un hub forkabile non può) o mettere un ramo per `Content` nel `ProjectionWriter` (che smetterebbe di essere generico). Nota: `docs/internal/decisions/2026-09-03-projection-context.md`, **confermata**; design §3.6 corretta. |
| Le righe di `hub_audit_log` si scrivono nel **secondo tempo**, non in `SavingChanges` | Prima del salvataggio una riga nuova non ha id: l'audit di una creazione punterebbe a `0`. Il prima/dopo si cattura comunque prima (il change tracker lo sa solo allora), si scrive dopo. |
| `HubUser` è `[Audited]`, quindi **ogni login lascia una riga di audit** | È il prezzo per avere l'audit dei superadmin senza che un servizio se lo scriva da sé (debito di F2 chiuso). La riga di un update contiene **solo le colonne cambiate**, quindi un login pesa poco. Se in M1 dà fastidio, si restringe lì. |
| `ICurrentUser` ha **due** metodi: `Has(permission, department)` e `HasAny(permission)` | Un solo metodo con il dipartimento opzionale lasciava indovinare cosa volesse dire `null` (Carmine l'ha letto come «solo se globale», che è una lettura legittima del nome). «Un dipartimento qualsiasi» serve davvero, perché è il caso di **ogni lista**: in F5 `MapCrud` controlla la policy quando una riga singola non c'è ancora e filtra per dipartimento subito dopo. Nota: `docs/internal/decisions/2026-09-03-has-and-has-any.md`, design §3.3 e §3.7 corrette. |
| `HubDbContext` legge `ICurrentUser` **quando parte la query**, non nel costruttore | Un contesto può nascere prima che il cookie sia validato (la cache dello `stamp` ne costruisce uno): leggere subito congelerebbe una risposta anonima. Per lo stesso motivo `HttpContextCurrentUser` rilegge i claim quando cambia il `ClaimsPrincipal` della richiesta. |
| Il flag di rientranza dell'interceptor è per contesto, non un campo di `HubDbContext` | Lo stesso interceptor scoped serve il contesto del nucleo e quello di ogni modulo: lo stato di un salvataggio non deve essere visibile all'altro. |
| `Department`, `Visibility`, `PublishStatus`, `StaffLevel` definiti in F1 | Le colonne della migrazione hanno bisogno del vocabolario; le **interfacce** restano a F4. |
| `.editorconfig` esenta `**/Migrations/*.cs`; `.gitignore` ancora le cartelle di runtime alla radice | File generati; e su Windows git confronta i pattern senza distinguere maiuscole. |
| Le cartelle di runtime hanno nomi inglesi (`secrets/`, `diagnostics/`, `startup.txt`) | Deciso il 2 set 2026: valgono le nostre regole, non quelle di vIPI (piano v0.20). |
| `CrudOptions` ha un `ContextType` (default `HubDbContext`) | Il design dà `Source` come `Func<DbContext, IQueryable<T>>` ma la firma di `MapCrud` ha quattro parametri di tipo e nessuno per il contesto: qualcuno deve dire *quale* contesto risolvere dal container. Un modulo scriverà `o.ContextType = typeof(AtcDbContext)`; il nucleo non scrive niente. Alternativa scartata: un quinto parametro di tipo su ogni chiamata, per un valore che è quasi sempre lo stesso. |
| `CrudOptions.SearchFields` è una collezione con `Add` sovraccaricato invece di `IList<Expression<Func<T,string?>>>` | Una colonna tradotta non è una `string?`, quindi la firma del design non la accetta. Con due `Add` la sintassi d'uso resta identica (`o.SearchFields.Add(x => x.Title)`) e il tipo decide da sé se serve il JSON path: l'estrazione resta in un helper solo, `LocalizedQuery`. |
| I parametri di lista sono un record `CrudListRequest` con `[FromQuery(Name=…)]`, non letti dalla query string | Letti a mano non finivano nell'OpenAPI, quindi il client generato non li conosceva. Gli attributi servono per il **case**: senza, l'`[AsParameters]` li pubblica `Page`/`PageSize` e il resto dell'API è camelCase. |
| `JsonNumberHandling.Strict` nelle `JsonOptions` | I default web accettano anche `"5"` per `5`, e lo schema generato lo dichiara: ogni intero diventava «integer oppure string» e ogni campo TypeScript `number` oppure `string`. La nostra SPA non ha ragione di mandare un numero come stringa, e il contratto lo dice. |
| Il documento OpenAPI si genera solo quando **non** c'è un `RuntimeIdentifier` | Lo strumento deve *eseguire* l'assembly compilato, e un `publish -r linux-x64 --self-contained` da Windows non può. Il documento è un artefatto di build, non di pubblicazione: `dotnet build` lo scrive, `dotnet publish` non ne ha bisogno. |
| Le policy dei permessi dichiarano `AddAuthenticationSchemes(CookieScheme)` | `DefaultChallengeScheme` è IVAO, che è giusto per `/auth/login` e sbagliato per `/api`: senza questa riga una chiamata non autenticata prendeva 302 verso il consenso invece di 401. |
| La lingua di un membro: `languageId` di IVAO se la divisione la parla, **altrimenti inglese** | Deciso da Carmine il 3 set 2026. L'inglese non e' il ripiego «della divisione» ma quello di IVAO e del progetto: una divisione italiana serve inglese a un tedesco, non italiano. La regola sta in un posto solo (`LocalePreference`), la usano il login e il selettore di lingua di F6. Si applica **solo alla creazione della riga**: la scelta esplicita dell'utente non si sovrascrive mai. Se una divisione non elenca l'inglese fra le sue lingue, si cade sul suo default, perche' deve poter rendere qualcosa. |
| `ivao_is_staff` e `ivao_is_supervisor` sono registrati ma non decidono niente | Il nostro `is_staff` significa «ha una posizione di QUESTA divisione», ed e' quello su cui poggiano permessi e grant. Quello di IVAO include HQ e altre divisioni: tenerli separati evita di allargare il perimetro per sbaglio. Servono alla staff directory di M1. |
| I codici dei dipartimenti sono quelli di IVAO: `HQ`, `SOD`, `FOD`, `AOD`, `TD`, `MD`, `ED`, `PRD`, `WD` | Confermati da Carmine il 3 set 2026 (piano v0.21). Non e' un suffisso meccanico: ATC operations e' `AOD` ma training e' `TD`. I **suffissi delle posizioni** non cambiano, cambia il dipartimento su cui mappano. La colonna e' passata a `varchar(4)` con la migrazione additiva `WidenDepartmentCodes`, che converte anche le righe gia' scritte; `Initial` non si tocca. |

## 5. Decisioni scritte (`docs/internal/decisions/`)

| File | Cosa dice |
|---|---|
| `2026-09-03-projection-context.md` | `IProjectable.Project()` riceve un `ProjectionContext` (lingue, lingua di default, walker): un'entità EF non si fa iniettare niente. **Confermata**, design §3.6 corretta. |
| `2026-09-03-has-and-has-any.md` | `ICurrentUser` fa due domande separate invece di una con il dipartimento opzionale. **Decisa da Carmine**, design §3.3 e §3.7 corrette. |
| `2026-09-03-licenza.md` | Apache-2.0, copyright «2026 Carmine Granato», con `NOTICE` fin da subito e senza header per file. **Decisa da Carmine**, piano §15.5 punto 5 chiuso. |
| `2026-09-03-openapi-a-build-time.md` | Il pacchetto Microsoft **esegue** il nostro `Program` fino a `app.Run()` per leggere gli endpoint: la frase del design «senza avviare l'app» è falsa, quella che conta («senza DB e senza client OAuth») la garantisce `HubConfiguration.IsOpenApiDocumentGeneration`. **Da confermare**, design §7.4 e §9 punto 12 da riformulare. |
| `2026-09-03-localized-nullable-nelle-api.md` | Una **lingua** che manca resta vuota; un **campo** dichiarato `Localized<T>?` e non valorizzato esce `null`, come dice lo schema generato. Era un 500 sul primo `GET` di un link senza descrizione. **Da confermare**, design §3.1 da precisare. |

Ogni decisione presa in corso d'opera finisce qui, con anche le alternative scartate e il perché:
serve a non ridiscutere fra sei mesi una cosa già discussa.

## 6. Letture del design da confermare (F2)

Il design non copre questi casi; ho scelto sempre l'opzione **più restrittiva**, così una correzione può solo
allargare i permessi, mai stringerli a sorpresa.

1. **Le posizioni FIR non danno nessun permesso del nucleo.** `RolePermissionMatrix` è indicizzata su
   `(Department, StaffLevel)` e una posizione FIR non ha dipartimento (design §3.8). Rendono l'utente staff e
   riempiono `ICurrentUser.Firs`, ma in M0 non aprono niente. Da decidere in M1 con `firStaffScope`.
2. **`HqStaff` si riconosce solo dal prefisso `HQ-`.** Il piano §4.1 dice «posizioni senza prefisso
   divisionale né FIR», che alla lettera renderebbe `FR-DIR` uno staffista HQ — mentre il test negativo
   preteso dal piano dice il contrario. Ho tenuto il prefisso esplicito.
3. **`Permissions.Manage` e `Admin.Access` vanno solo a Director e Web.** Design §3.7 li dichiara globali;
   il piano §6.3 diceva «coordinatori per il proprio dipartimento». Con F8 (schermata grant in modalità
   globale) la lettura del design è quella coerente.
4. ~~I nomi dei campi rating e Discord nel payload IVAO non sono verificati~~ **chiuso il 3 set 2026**:
   il payload reale e' stato misurato e i suoi campi sono documentati in `IvaoUserProfileReaderTests`.
   `IVAO non manda nessun campo Discord`, nemmeno con lo scope `discord` concesso: il tool Discord della
   divisione (`ivao-italy/discord`) infatti fa un OAuth verso Discord in proprio e si salva l'id da se'.
   La colonna `discord_id` resta vuota finche' l'hub non fara' lo stesso.

## 7. Debiti e cose da fare presto

> I punti barrati restano scritti apposta: dicono che una cosa è stata chiusa e in che modo, così
> non si riapre da sola alla fase dopo.


- ~~Il login vero non è ancora stato eseguito~~ **fatto il 3 set 2026**: giro completo fino a `/me`, riga in
  `hub_users`, posizioni lette, token IVAO cifrati a DB. Ha fatto emergere un bug reale (i cookie del giro
  uscivano `SameSite=None` senza `Secure` su http, quindi il browser li scartava): corretto, con test.
- Lo scope `discord` resta richiesto anche se il payload non restituisce niente (deciso da Carmine il
  3 set 2026): serve gia' pronto per quando l'hub collegera' Discord.
- ~~Le posizioni FIR non si riconoscono finché `ref_ivao_centers` è vuota~~ **chiuso in F3**: il job
  riempie la tabella all'avvio quando è vuota, e `IFirDirectory` la legge per tutti.
- ~~Il pacchetto pubblicato non ha `locales/` alla radice né i `config/*.example.json`, e manca
  `LocaleCatalog`~~ **chiuso in F5**: il target `PublishHubFiles` mette nel pacchetto `locales/`, i
  `config/*.example.json`, `LICENSE` e `NOTICE`, con uno step di CI che lo verifica; `LocaleCatalog`
  legge `locales/{lang}/*.json` per il server.
- ~~L'audit dei superadmin lo scrive il servizio a mano~~ **chiuso in F4**: `HubUser` è `[Audited]` e
  `SuperadminService.WriteAuditAsync` non esiste più. Resta a mano la sola riga
  `superadmin.set_changed`, che non è la scrittura di una riga ma un confronto fra due insiemi
  (design §4.5).
- `DivisionOptionsValidator` accetta le chiavi modulo note ma nessuno gliene passa: si accende in **F8**.
- ~~`shared/api/bootstrap.ts` e il tipo `ApiPaths` in `client.ts` sono scritti a mano~~ **chiuso in
  F5**: `schema.d.ts` è generato dall'OpenAPI e committato, `client.ts` è `createClient<paths>` e
  `bootstrap.ts` è un elenco di alias del contratto.
- La documentazione è stata riallineata il 3 set 2026: `README.md` e `docs/FORKING.md` dicevano
  ancora «phase F1» e «phase F0»; il design `01` descriveva l'interceptor e il query filter in una
  forma che F4 ha poi cambiato; i codici di dipartimento del changelog 0.21 erano rimasti in una
  decina di esempi. Vale la pena rifare lo stesso giro alla fine di ogni fase: costa dieci minuti e
  l'alternativa è un documento che mente.
- ~~Il test di architettura «nessun modulo referenzia un altro modulo»~~ **fatto in F4**
  (`ArchitectureTests`, che legge i `.csproj` e non le assembly: un riferimento che il compilatore
  elide perché nessuno lo usa ancora è comunque una dipendenza della build). `docs/UI-GUIDELINES.md`
  resta **F6**.
- Il catalogo dei permessi che `HubPolicyProvider` interroga è `CorePermissions` e basta: i permessi
  dei moduli si aggiungono in **F8**, quando `IModule.Permissions` esiste.
- `BlockDocumentWalker.ValidateEnvelope` accetta l'elenco dei tipi di blocco noti come parametro
  opzionale: il registry vero (server ⇄ manifest) è **F7/F8**. Finché è `null`, il tipo non si
  controlla.
- Un contesto di modulo non scrive proiezioni né audit se non ha quelle tabelle nel proprio modello:
  l'interceptor se ne accorge e non fa niente. Quando un modulo proietterà davvero (M1+), va deciso
  se condividere quelle entità o passare dal contesto del nucleo.
- Il chunk JS supera i 500 kB: lo split per route arriva con i layout di F6.
- ~~Licenza ancora «TBD»~~ **decisa il 3 set 2026**: Apache-2.0, copyright «2026 Carmine Granato».
  Testo canonico completo in `LICENSE`, più un `NOTICE` alla radice. Gli header di licenza nei
  singoli file **non** ci sono e non servono: Apache-2.0 li raccomanda, non li impone, e metterli in
  ogni `.cs` e `.tsx` sarebbe rumore in ogni diff futuro.
- ~~`LICENSE` e `NOTICE` non finiscono nel pacchetto pubblicato~~ **chiuso in F5**, nello stesso
  target `PublishHubFiles`.
- `Ivao:ApiScopes` e' vuoto: **misurato**, i due endpoint di riferimento non chiedono scope. Se in
  M2+ servira' `tracker` (chi e' online), si aggiunge li' senza toccare codice.
- Le fixture IVAO coprono 3 centri e 3 aeroporti: bastano a provare upsert e riconoscimento FIR, non
  sono un campione realistico dell'Italia (che ne ha 7 e 221).
- **`MapCrud` non ha ancora nessuna entità in modalità globale.** Il ramo esiste ed è scritto, ma il
  primo uso vero (`UserGrant` e `hub_audit_log` in sola lettura) è **F8**: finché non c'è, quel ramo
  è coperto solo dal codice e non da un test end-to-end.
- **`ExtraWritePolicy` non è ancora usato da nessuno.** Nasce per `Content.ManageTemplates` in
  **F7**; oggi è provato solo dal fatto che compila e che il ramo non morde quando è nullo.
- **La ricerca `q` ignora l'accento e la maiuscola per collation, non per scelta.** `LIKE` su
  `utf8mb4_unicode_ci` è già case e accent insensitive, che è quello che vogliamo; ma la ricerca
  della lista **non** passa dal FULLTEXT di `cms_search_index`. Quella è `/api/search` in **F8**, ed
  è un altro meccanismo: qui si cerca dentro la tabella del back-office, lì nell'indice pubblico.
- **`pageSize` è tagliato a 100 e `DefaultPageSize` è 25**, cablati nel motore. Se una schermata di
  F6 ne vorrà altri, diventano configurazione di `CrudOptions`, non un numero in più nel motore.
- **`CrudScope` risolve il contesto per `Type` dal container.** Funziona perché ogni contesto è
  registrato da `AddHubDbContext`/`AddModuleDbContext<T>`; un contesto registrato in un altro modo
  darebbe un `InvalidOperationException` a runtime e non a build. Il test di architettura che
  potrebbe pinnarlo (nessun `AddDbContext` fuori da quei due punti) non c'è: vale la pena scriverlo
  in **F8**, con il primo contesto di modulo davvero registrato.
- **Il `filter[...]` fa un solo confronto, l'uguaglianza.** Basta a F6 (dipartimento, visibilità,
  categoria, attivo). Intervalli e `in` non ci sono, e se servissero andrebbero nel motore.
- **Nessun test verifica il documento OpenAPI in sé** (che `/api/links` ci sia, che
  `LocalizedString` porti `x-localized`). Lo step di CI `pnpm gen:api && git diff --exit-code` lo
  copre di sponda: se il documento cambia forma, `schema.d.ts` si muove e la build cade.

---

## 8. Igiene del repository

- I branch `m0/f4-domain-backbone` e `m0/f5-mapcrud-links` sono **ancora sul remoto**: il repository
  non cancella i branch al merge. Si possono togliere quando fa comodo
  (`git push origin --delete <branch>`); niente ci dipende.
- La strategia di merge è **squash**: su `main` c'è un commit per fase (F4 è `586a432`), non la
  catena dei commit di lavoro. Chi cerca il dettaglio lo trova nella PR. F5 arriva sul branch con
  due commit (`5e9c197` il codice, `6e68fd8` il numero della PR nell'handoff) che lo squash unisce.
- Se si lavora in un worktree sotto `.claude/worktrees/`, `git checkout main` lì dentro fallisce
  perché `main` è già in uso dal checkout principale: è normale, la sessione nuova parte dal
  checkout principale. F5 è stata scritta in un worktree, e il branch è già sul remoto: dopo il
  merge il worktree non serve più a niente.
- `artifacts/` è gitignorata, quindi `artifacts/openapi/IvaoHub.Web.json` **non** è nel repository:
  lo riscrive `dotnet build`. Quello che è committato è il file che ne deriva,
  `web/src/shared/api/schema.d.ts`, marcato `linguist-generated` in `.gitattributes` e ignorato da
  Prettier e da ESLint come `routeTree.gen.ts`.
