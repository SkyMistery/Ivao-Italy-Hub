# HANDOFF — stato di M0

> Documento **interno** (italiano). Si aggiorna alla fine di ogni fase (piano di implementazione §A.6).
> Fonte di verità: `00-piano-di-progettazione.md`; perimetro e firme: `01-design-m0.md`; ordine: `02-piano-implementazione-m0.md`.

**Ultimo aggiornamento:** 3 settembre 2026 — fine **F4** (spina dorsale del dominio).
**Repository:** https://github.com/SkyMistery/Ivao-Italy-Hub (pubblico).
**Piano:** v0.24. **Design:** v1.2. **Test:** 214 verdi (174 unit + 40 integrazione).

| Fase | Stato |
|---|---|
| F0 bootstrap | mergiata (PR #1) |
| F1 configurazione, avvio, DB | mergiata (PR #2) |
| F2 auth BFF, ruoli, permessi, `/api/me` | mergiata (PR #3 e #4) |
| F3 `IvaoApiClient` e dati `ref_` | mergiata (PR #5) |
| F4 spina dorsale del dominio | mergiata (PR #6) |
| **F5 `MapCrud` e `links` (server)** | **prossima** |

Niente PR aperte: `main` contiene tutto fino a F4 compresa. Una sessione nuova parte da
`git checkout main && git pull` e apre subito il branch della fase.

**Come si apre F5**: prompt di §C del piano di implementazione con `<N>` → `5`. Perimetro in §D:
`MapCrud<TEntity, …>` nelle due modalità (dipartimentale e globale), il CRUD di `links` senza codice
a mano, `ExtraWritePolicy`, `ValidationProblem` con chiavi i18n, 409 su `row_version`, `LocaleCatalog`
(arrivato da F4, punto 5), OpenAPI a build-time e `schema.d.ts` generato in CI.

Due cose che F5 eredita e deve usare, non riscrivere: la spina dorsale scrive audit e proiezioni da
sola (a `MapCrud` non tocca nessuna delle due), e **`IgnoreQueryFilters` può comparire solo sotto
`src/IvaoHub.Core/Data/Crud/`** — c'è un test di architettura che lo verifica su tutto `src/`.

Serve solo Docker attivo: le credenziali IVAO ci sono e funzionano, ma F4 e F5 non le usano.

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
dotnet build IvaoHub.sln
dotnet test --solution IvaoHub.sln                          # richiede Docker (Testcontainers)
cd web && pnpm lint && pnpm format:check && pnpm typecheck && pnpm test && pnpm i18n:check && pnpm build
dotnet publish src/IvaoHub.Web -c Release -r linux-x64 --self-contained -o artifacts/publish
```

Nuova migrazione (**solo additiva**, mai modificare una già mergiata):

```bash
dotnet tool restore
dotnet dotnet-ef migrations add <Nome> --project src/IvaoHub.Core --startup-project src/IvaoHub.Core
```

## 2. Cosa c'è dopo F4

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
| La lingua di un membro: `languageId` di IVAO se la divisione la parla, **altrimenti inglese** | Deciso da Carmine il 3 set 2026. L'inglese non e' il ripiego «della divisione» ma quello di IVAO e del progetto: una divisione italiana serve inglese a un tedesco, non italiano. La regola sta in un posto solo (`LocalePreference`), la usano il login e il selettore di lingua di F6. Si applica **solo alla creazione della riga**: la scelta esplicita dell'utente non si sovrascrive mai. Se una divisione non elenca l'inglese fra le sue lingue, si cade sul suo default, perche' deve poter rendere qualcosa. |
| `ivao_is_staff` e `ivao_is_supervisor` sono registrati ma non decidono niente | Il nostro `is_staff` significa «ha una posizione di QUESTA divisione», ed e' quello su cui poggiano permessi e grant. Quello di IVAO include HQ e altre divisioni: tenerli separati evita di allargare il perimetro per sbaglio. Servono alla staff directory di M1. |
| I codici dei dipartimenti sono quelli di IVAO: `HQ`, `SOD`, `FOD`, `AOD`, `TD`, `MD`, `ED`, `PRD`, `WD` | Confermati da Carmine il 3 set 2026 (piano v0.21). Non e' un suffisso meccanico: ATC operations e' `AOD` ma training e' `TD`. I **suffissi delle posizioni** non cambiano, cambia il dipartimento su cui mappano. La colonna e' passata a `varchar(4)` con la migrazione additiva `WidenDepartmentCodes`, che converte anche le righe gia' scritte; `Initial` non si tocca. |

## 5. Letture del design da confermare (F2)

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

## 6. Debiti e cose da fare presto

- ~~Il login vero non è ancora stato eseguito~~ **fatto il 3 set 2026**: giro completo fino a `/me`, riga in
  `hub_users`, posizioni lette, token IVAO cifrati a DB. Ha fatto emergere un bug reale (i cookie del giro
  uscivano `SameSite=None` senza `Secure` su http, quindi il browser li scartava): corretto, con test.
- Lo scope `discord` resta richiesto anche se il payload non restituisce niente (deciso da Carmine il
  3 set 2026): serve gia' pronto per quando l'hub collegera' Discord.
- Le posizioni FIR non si riconoscono finché `ref_ivao_centers` è vuota: è **F3**. `UserSyncService` legge già
  la tabella, quindi si accendono da sole appena il job la riempie.
- **Il pacchetto pubblicato non ha `locales/` alla radice né i `config/*.example.json`**, e manca
  `LocaleCatalog`. Precisazione utile: le lingue **dentro `wwwroot/locales/` ci sono già** (le emette
  il plugin `divisionLocales` di `vite.config.ts`, ed è da lì che la SPA le carica); quello che manca
  è la copia alla radice, dove guarda `HubPaths.Locales`, cioè quella che serve al **backend**.
  **Spostato a F5 e scritto lì**: punto 5 di `02-piano-implementazione-m0.md` §D/F5. Il primo che ne
  ha davvero bisogno è il `ValidationProblem` di `MapCrud`.
- ~~L'audit dei superadmin lo scrive il servizio a mano~~ **chiuso in F4**: `HubUser` è `[Audited]` e
  `SuperadminService.WriteAuditAsync` non esiste più. Resta a mano la sola riga
  `superadmin.set_changed`, che non è la scrittura di una riga ma un confronto fra due insiemi
  (design §4.5).
- `DivisionOptionsValidator` accetta le chiavi modulo note ma nessuno gliene passa: si accende in **F8**.
- `shared/api/bootstrap.ts` e il tipo `ApiPaths` in `client.ts` sono scritti a mano: **F5** li sostituisce con
  `schema.d.ts` generato dall'OpenAPI.
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
- `LICENSE` e `NOTICE` **non finiscono nel pacchetto pubblicato**, mentre Apache-2.0 §4(d) vuole che
  il `NOTICE` viaggi con ogni ridistribuzione. Sta in **F5**, nello stesso target MSBuild che porta
  `locales/` e i `config/*.example.json`.
- `Ivao:ApiScopes` e' vuoto: **misurato**, i due endpoint di riferimento non chiedono scope. Se in
  M2+ servira' `tracker` (chi e' online), si aggiunge li' senza toccare codice.
- Le fixture IVAO coprono 3 centri e 3 aeroporti: bastano a provare upsert e riconoscimento FIR, non
  sono un campione realistico dell'Italia (che ne ha 7 e 221).
