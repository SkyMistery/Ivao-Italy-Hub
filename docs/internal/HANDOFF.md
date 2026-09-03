# HANDOFF — stato di M0

> Documento **interno** (italiano). Si aggiorna alla fine di ogni fase (piano di implementazione §A.6).
> Fonte di verità: `00-piano-di-progettazione.md`; perimetro e firme: `01-design-m0.md`; ordine: `02-piano-implementazione-m0.md`.

**Ultimo aggiornamento:** 3 settembre 2026 — fine **F4**, più la **revisione senior** che l'ha seguita.
**Repository:** https://github.com/SkyMistery/Ivao-Italy-Hub (pubblico).
**Piano:** v0.25. **Design:** v1.3. **Test:** 249 verdi (190 unit + 59 integrazione).

| Fase | Stato |
|---|---|
| F0 bootstrap | mergiata (PR #1) |
| F1 configurazione, avvio, DB | mergiata (PR #2) |
| F2 auth BFF, ruoli, permessi, `/api/me` | mergiata (PR #3 e #4) |
| F3 `IvaoApiClient` e dati `ref_` | mergiata (PR #5) |
| F4 spina dorsale del dominio | mergiata (PR #6) |
| F4bis revisione senior (correzioni, nessun perimetro nuovo) | vedi §8 |
| **F5 `MapCrud` e `links` (server)** | **prossima** |

Niente PR aperte: `main` contiene tutto fino a F4 compresa. Una sessione nuova parte da
`git checkout main && git pull` e apre subito il branch della fase.

**Come si apre F5**: prompt di §C del piano di implementazione con `<N>` → `5`. Perimetro in §D:
`MapCrud<TEntity, …>` nelle due modalità (dipartimentale e globale), il CRUD di `links` senza codice
a mano, `ExtraWritePolicy`, `ValidationProblem` con chiavi i18n, 409 su `row_version`, `LocaleCatalog`
(arrivato da F4, punto 5), OpenAPI a build-time e `schema.d.ts` generato in CI.

**Prima di aprire F5, leggere §8.** La revisione ha cambiato due comportamenti su cui F5 poggia
direttamente: `HasAllDepartments` (da cui dipende il filtro di dipartimento di `MapCrud`, design
§3.9) e la gestione dell'errore nel secondo tempo dell'interceptor.

Tre cose che F5 eredita dalla spina dorsale e deve usare, non riscrivere:

1. **Audit e proiezioni li scrive l'interceptor.** A `MapCrud` non tocca nessuna delle due: se si
   trova a scrivere una riga di `hub_audit_log` o di `cms_search_index`, sta duplicando.
2. **`IgnoreQueryFilters` può comparire solo sotto `src/IvaoHub.Core/Data/Crud/`** (e in
   `ProjectionWriter.cs`, che deve ritrovare la riga da riscrivere chiunque stia scrivendo). Il
   filtro di visibilità nasconde le bozze e i dipartimenti altrui anche allo staff, quindi la lista
   del back-office **deve** ignorarlo e rifiltrare per dipartimento — ma solo lì dentro. Un test di
   architettura verifica l'allow-list su tutto `src/`.
3. **La guardia dell'interceptor è una rete, non il controllo.** Quando morde lancia
   `ForbiddenDomainException`, cioè un'eccezione a metà transazione, non un 403 pulito: `MapCrud`
   deve comunque chiamare `AuthorizeAsync` sulla risorsa **prima**, e mappare l'eccezione a 403 solo
   come ultima difesa (perimetro F5 punto 3).

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
dotnet test --solution IvaoHub.sln --configuration Release  # richiede Docker (Testcontainers)
cd web && pnpm lint && pnpm format:check && pnpm typecheck && pnpm test && pnpm i18n:check && pnpm build
dotnet publish src/IvaoHub.Web -c Release -r linux-x64 --self-contained -o artifacts/publish
```

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
- `RefDataSyncJob`: upsert (mai duplicati) — e, dalla revisione di §8, cancellazione di ciò che una
  risposta **non vuota** non elenca più — in `ref_ivao_centers` e `ref_ivao_airports` con il
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
- **Niente `ExecuteDelete`/`ExecuteUpdate`**: vanno dritti al server e non passano dall'interceptor,
  quindi sono un buco nell'audit, nella guardia e nelle proiezioni. Un test di architettura li vieta
  su tutto `src/`.
- **`HasAllDepartments` non si deduce dai permessi**: è il claim `alldept`, scritto da
  `HubClaims.BuildIdentity`. Chi ha bisogno di sapere «raggiunge ogni dipartimento?» lo chiede a
  `ICurrentUser`, non alla forma della lista.
- **La regola «tiene questo permesso?» sta in `PermissionSet`**, e la chiamano sia
  `HttpContextCurrentUser` sia i doppioni dei test. Una copia in un test è un posto dove il codice
  provato e quello vero divergono in silenzio.
- **Le proiezioni si leggono una volta per salvataggio, non una per riga.** `ProjectionWriter`
  separa `Load`/`LoadAsync` da `Apply` apposta: sono dentro la transazione della scrittura, e ogni
  round trip in più è un lock tenuto aperto più a lungo. `ProjectionBatchingTests` lo fissa
  contando gli statement veri.

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

## 5. Decisioni scritte (`docs/internal/decisions/`)

| File | Cosa dice |
|---|---|
| `2026-09-03-projection-context.md` | `IProjectable.Project()` riceve un `ProjectionContext` (lingue, lingua di default, walker): un'entità EF non si fa iniettare niente. **Confermata**, design §3.6 corretta. |
| `2026-09-03-has-and-has-any.md` | `ICurrentUser` fa due domande separate invece di una con il dipartimento opzionale. **Decisa da Carmine**, design §3.3 e §3.7 corrette. |
| `2026-09-03-licenza.md` | Apache-2.0, copyright «2026 Carmine Granato», con `NOTICE` fin da subito e senza header per file. **Decisa da Carmine**, piano §15.5 punto 5 chiuso. |
| `2026-09-03-reaches-every-department.md` | `HasAllDepartments` è un claim derivato dalle posizioni, non un indizio letto dalla lista dei permessi. Design §3.3 precisata. |
| `2026-09-03-proxy-fidati.md` | Le reti dei proxy di cui si crede `X-Forwarded-For` si dichiarano, e in produzione sono obbligatorie. Design §2.3 precisata. |
| `2026-09-03-snapshot-ref-potatura.md` | Lo snapshot `ref_` cancella ciò che IVAO non elenca più, solo su risposta non vuota. |

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

---

## 8. Revisione senior di fine F4 (3 set 2026)

Rilettura di tutto il repository come se fosse di altri, prima di aprire F5. Undici delle
segnalazioni iniziali si sono rivelate cose già previste dal piano (le esclusioni `/vsop` di F0 che
F8 sostituisce, `GetMeAsync` chiesto da F3, `react-hook-form`/`lucide-react` installati da F0 per
F6, `SearchIndexEntry` che diventa `IVisible` in F8 §6, `AddProblemDetails` in F5 §3,
`ValidateEnvelope` cablato in F7, il pacchetto completato in F5 §5): **non erano difetti, era la
revisione che non aveva ancora letto il piano di implementazione**. Quello che resta è qui.

### 8.1 Bug corretti

| Cosa | Dove era | Perché contava |
|---|---|---|
| **La lingua del membro non veniva mai decisa dalla regola** | `UserSyncService`: la riga nuova nasceva con `Locale = DefaultLocale`, quindi il `??=` sotto era irraggiungibile | Ogni nuovo iscritto di ogni divisione prendeva la lingua della divisione. La regola documentata (§4, «`languageId` di IVAO se la divisione la parla, altrimenti inglese») funzionava per i soli superadmin bootstrappati, che nascono senza lingua. Test: `UserSyncTests`. |
| **`HasAllDepartments` dedotto invece che dichiarato** | `HttpContextCurrentUser` | Sbagliava in due direzioni opposte: dava «vede tutto» a una posizione IVAO HQ (scavalcando l'intero filtro di visibilità) e lo **toglieva** a un Director colpito da un deny, lasciandolo con il solo `HQ`. In F5 sarebbe stato un **403** su ogni lista (design §3.9). Nota di decisione dedicata. |
| **Il secondo tempo dell'interceptor non gestiva il proprio fallimento** | `HubSaveChangesInterceptor.SavedChanges(Async)` | Un'eccezione nella proiezione lasciava la transazione **aperta** e la voce in `_pending`: scrittura né committata né annullata, connessione avvelenata per il prossimo. Raggiungibile oggi con un `body_json` non valido, perché la validazione dell'envelope è F7. Test: `InterceptorFailureTests`. |
| **Il ramo di errore del sync scriveva i dati parziali** | `RefDataSyncJob` | Il `SaveChanges` che salvava la riga «failed» portava con sé tutto ciò che il run aveva già tracciato: riga «failed» sopra uno snapshot mezzo scritto. Ora il change tracker si svuota prima. Aggiunto anche lo stato `partial`. Test: `RefDataSyncTests`. |
| **Permessi duplicati nel cookie** | `EffectivePermissionsCalculator` | `EffectivePermission` include `Source` nell'uguaglianza (è la firma del design §3.3, non si tocca), quindi lo stesso permesso da ruolo **e** da grant faceva due entrate, cioè due claim identici in un cookie che viaggia a ogni richiesta. Deduplicato su `(nome, dipartimento)`, vince il ruolo. |

### 8.2 Sicurezza

- **`X-Forwarded-For` creduto da chiunque.** `KnownIPNetworks`/`KnownProxies` erano svuotate senza
  rimpiazzo: il rate limiter di `/auth/*` si aggirava cambiando un header, e l'indirizzo dell'audit
  lo sceglieva chi scriveva. Ora `ForwardedHeaders:TrustedNetworks` è obbligatorio in produzione, e
  fuori produzione, se vuoto, il middleware non entra affatto. Nota di decisione dedicata.
- **Il cookie `hub.auth` non dichiarava `SecurePolicy`.** Erano scritti a mano `HttpOnly` e
  `SameSite` «perché un default cambia con la versione del framework», e mancava proprio il terzo,
  sull'unica credenziale che il sito emette. Ora segue lo schema del callback, come i cookie del
  giro OIDC.
- **`OnValidatePrincipal` rigettava senza fare sign-out** nel ramo del cookie malformato: il cookie
  inutilizzabile restava nel browser e ripercorreva quel ramo a ogni richiesta. Il ramo sotto lo
  faceva già.
- **`hub_audit_log.ip`** era a specifica (piano §7) e non veniva mai popolata. Ora sì — il che ha
  senso solo dopo il punto sui proxy, non prima.
- **HSTS e redirezione HTTPS** in produzione, subito dopo i forwarded headers. Vedi §8.6.

### 8.3 Coerenza con le regole già scritte

- **Terza violazione di «la configurazione si legge quando il servizio viene costruito»** (§3):
  `AddIvaoIntegration` prendeva `IConfiguration`, `IHostEnvironment` e `DivisionOptions` alla
  registrazione. Ora non prende **niente**: il fuso del cron passa da `QuartzOptions` via options
  pipeline, e la scelta fixture/reale resta nella factory scoped, con la guardia dove conta già
  (il costruttore di `FixtureIvaoApiClient`).
- **`ExecuteDeleteAsync` scavalcava l'interceptor** in `IvaoUserTokenStore`. Sostituito con una
  cancellazione tracciata, e aggiunto il test di architettura che lo vieta ovunque sotto `src/`.
- **`TestCurrentUser` riscriveva la regola di autorizzazione** che avrebbe dovuto esercitare. Ora
  entrambe le implementazioni chiamano `PermissionSet.Has/HasAny`: stesso codice, non una copia.
- **Il test «un solo handler» non vedeva `IvaoHub.Web`** (il progetto unit non lo referenzia).
  Rinominato per dire cosa copre davvero, e affiancato da un test sui **sorgenti** di tutto `src/`,
  che prende anche un handler dichiarato e mai registrato.

### 8.4 Documentazione corretta

- `VisibilityQueryFilter` diceva «legge l'utente quando il contesto viene costruito»: il contrario
  del codice, del commento di `HubDbContext` dieci righe più in là e di questo file.
- `HubConfiguration.RequireAllowedHosts` motivava con «il redirect URI OIDC è costruito dall'header
  Host»: il contrario del design §4, dove il `redirect_uri` è preso alla lettera da configurazione.
- La home diceva ancora «Solo bootstrap del repository» (testo di F0), in entrambe le lingue.
- `README.md` e `docs/FORKING.md` non dicevano che **in produzione servono `AllowedHosts` e i proxy
  fidati**, senza i quali l'applicazione non parte. Ora c'è una tabella con l'esempio.
- `docs/FORKING.md` non avvertiva che `superAdmins` in `division.json` contiene i VID **di questa**
  divisione: chi forka e avvia senza toccarlo si ritrova un superadmin altrui.
- Il commento di `i18n.ts` descriveva al presente la lingua dell'account, che è F6.
- `holdsPermission` lato SPA aveva la firma con dipartimento opzionale che la decisione
  `2026-09-03-has-and-has-any.md` aveva scartato lato server. Ora sono due funzioni.

### 8.5 Minori

`ResolveName` rimosso e `IcaoPrefixes` reso reale (vedi §8.6);
`fir` passato a `varchar(8)` come `ref_ivao_centers.id` (migrazione additiva
`WidenStaffPositionFir`); potatura dello snapshot `ref_`; `Localized.Equals` che confronta le chiavi
con il comparer che il dizionario usa davvero (`OrdinalIgnoreCase`); `SecurityStampCache` che non
memorizza più «questo VID non esiste»; `IClock` al posto di `DateTime.UtcNow` in `OnTokenValidated`
e `StartupDiagnostics`; `openid` preteso fra gli `Scopes`; `PostLogoutRedirectUri` non più
obbligatorio (nessuno lo legge, e IVAO non ha un end session da cui tornare); `UnknownModuleKeys`
non è più stato mutabile su un singleton ma una riga di log; i due significati di `HQ` documentati
dove si incontrano; `mailpit` pinnato; `release.yml` che ora **dipende da `build-test`** invece di
pubblicare a scatola chiusa; lo zip che si scompatta nell'applicazione e non in `artifacts/publish/`.

### 8.6 Quello che era stato lasciato aperto, e come è stato chiuso

La prima passata aveva lasciato sei punti «di proposito». Sono stati chiusi tutti.

- **L'N+1 di `ProjectionWriter`** — era il punto rinviato a F5. Chiuso adesso, perché rinviarlo
  significava consegnare a F5 un meccanismo che `MapCrud` avrebbe subito usato in massa. Lettura e
  scrittura sono ora separate: `Load`/`LoadAsync` leggono **una volta per l'intero salvataggio**
  (tre query, non tre per riga), `Apply` non fa I/O. Un salvataggio di dodici righe che prima
  costava trentasei query ne costa tre. Test: `ProjectionBatchingTests`, che conta gli statement
  veri con un `DbCommandInterceptor` — la proprietà è invisibile finché qualcuno non rimette il
  ciclo dentro, quindi va fissata.
- **Il `GetAwaiter().GetResult()` del percorso sincrono** — sparito con la stessa separazione: la
  duplicazione fra sincrono e asincrono è ora di tre query, non di tutto il ragionamento.
- **HSTS e redirezione HTTPS** — ci sono, in produzione, **dopo** i forwarded headers (prima, lo
  schema è quello del salto dal proxy e la redirezione è un ciclo infinito). HSTS a trenta giorni,
  senza `includeSubDomains` e senza preload: l'hub è un host sotto un dominio condiviso con il resto
  della divisione, e una policy HSTS è reversibile solo quanto il suo `max-age` più lungo. La
  redirezione si spegne con `Https:Redirect=false` per chi la fa già fare al proxy.
- **`CalendarEntry`** — è `IAuditable`, `IOwnedByDepartment`, `IVisible` e `[PermissionArea("Calendar")]`.
  Le voci che lo staff scriverà a mano in M1 hanno quindi guardia di scrittura (`Calendar.Edit` sul
  proprio dipartimento) e le quattro colonne di audit dall'interceptor; quelle proiettate le
  stampiglia `ProjectionWriter`, perché una proiezione è il risultato di una scrittura, non una
  scrittura. La domanda «basterà in M1?» ha adesso una risposta invece di un rinvio.
- **Le proiezioni fuori dal query filter** — `SearchIndexEntry` e `CalendarEntry` dichiarano owner e
  visibilità, quindi il filtro globale si applica anche a loro: le due tabelle su cui si costruiscono
  ricerca e calendario non sono più le uniche senza rete. `AwardSignal` resta fuori **per decisione**,
  non per dimenticanza: non ha un dipartimento contro cui confrontare nulla, è una risorsa globale
  nel senso del design §3.9, come `UserGrant` e `AuditLogEntry`, letta dietro `Awards.Assign`.
  `ProjectionWriter` legge le due tabelle con `IgnoreQueryFilters` — è il secondo e ultimo posto in
  cui compare, l'allow-list del test di architettura ora ne elenca due — perché deve trovare la riga
  da riscrivere chiunque sia loggato, altrimenti ne inserirebbe una seconda contro una chiave unica.
  Test: `ProjectionVisibilityTests`, che copre entrambe le metà. **F8 §6 trova questa parte già
  fatta**, e gli resta l'endpoint `/api/search`.
- **`IcaoPrefixes` e `ResolveName`** — `ResolveName` è stato **rimosso**: era una seconda copia della
  regola di fallback fra lingue che `Localized<T>.Resolve` già implementa, senza chiamanti, cioè
  esattamente la copia destinata a divergere. `IcaoPrefixes` invece è diventato reale: validato
  all'avvio (1–4 lettere maiuscole, così un refuso non resta muto) e usato dal sync come rete di
  sicurezza — se **nessun** aeroporto tornato da IVAO comincia con uno dei prefissi, la riga di log
  dice di controllare `countryId`, che è la causa quasi certa.

Una nota di metodo: `AddHubDbContext`/`AddModuleDbContext` ora agganciano anche gli `IInterceptor`
registrati nel container. EF Core non li raccoglie da sé quando gli interceptor vengono aggiunti a
mano, e serviva un modo di attaccare una diagnostica senza aprire una seconda strada per costruire
un contesto.

---

## 9. Igiene del repository

- Il branch `m0/f4-domain-backbone` è **ancora sul remoto**: il repository non cancella i branch al
  merge. Si può togliere quando fa comodo (`git push origin --delete m0/f4-domain-backbone`); niente
  ci dipende.
- La strategia di merge è **squash**: su `main` c'è un commit per fase (F4 è `586a432`), non la
  catena dei commit di lavoro. Chi cerca il dettaglio lo trova nella PR.
- Se si lavora in un worktree sotto `.claude/worktrees/`, `git checkout main` lì dentro fallisce
  perché `main` è già in uso dal checkout principale: è normale, la sessione nuova parte dal
  checkout principale.
