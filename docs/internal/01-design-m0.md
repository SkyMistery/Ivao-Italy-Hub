# IVAO Division Hub — Design di M0 (fondamenta + spina dorsale generica)

> Documento **interno** (italiano). Fonte di verità: `00-piano-di-progettazione.md` v0.18. Questo documento scrive le
> **firme** dei meccanismi decisi in §16 e il perimetro esatto di M0; il piano di lavoro passo-passo è in
> `02-piano-implementazione-m0.md`. Se questo documento e il piano non coincidono, vince il piano e questo va corretto.

**Versione:** 1.4 — 3 settembre 2026 (confermate le due note di F5: §7.4 e §9 punto 12 — l'OpenAPI a build-time **esegue** l'entry point, e ciò che conta è che lo faccia senza database e senza client OAuth; §3.1 — una lingua assente è vuota, un campo `Localized<T>?` non valorizzato è `null`)
**Versione 1.3** — 3 settembre 2026 (revisione senior di fine F4: §3.3 `HasAllDepartments` è un claim, §3.4 il secondo tempo dell'interceptor gestisce il proprio fallimento, §3.6 le proiezioni sotto il query filter e lette in blocco, §2.3 proxy fidati obbligatori in produzione più HSTS e redirezione HTTPS)
**Versione 1.2** — 3 settembre 2026 (allineato a ciò che F4 ha davvero costruito: §3.3, §3.4, §3.5, §3.6, §3.7, §5.3, più i codici di dipartimento di piano 0.21 rimasti negli esempi)
**Stato:** in implementazione (F0–F4 fatte)

---

## 0. Perimetro di M0

### 0.1 Definizione di «fatto» (piano §16.15)

M0 è finito quando, **in locale** (docker-compose + `dotnet run` + `pnpm dev`) e con la CI verde:

1. si fa login con le credenziali IVAO di test di Carmine, l'utente viene creato in `hub_users`, il superadmin viene bootstrappato da `division.json`, `/api/me` restituisce menu, permessi effettivi, moduli e registry;
2. l'entità-cavia **`links`** funziona end-to-end: campi `Localized<T>`, `owner_department`, `visibility`, audit compilato dall'interceptor, CRUD esposto da `MapCrud` senza codice a mano, lista+form nel back-office generati dalla configurazione, autorizzazione decisa dall'**unico** handler di dipartimento, riga proiettata in `search_index` **nella stessa transazione**;
3. un contenuto `cms_contents` (`kind = page`) viene creato **da un template seedato**, modificato nell'editor a lista, **pubblicato** (riga in `cms_content_versions`, blocco Data `linkList` catturato in `frozen_json` se `renderMode = frozen`) e reso sul sito pubblico leggendo **solo** la versione pubblicata;
4. i **test della spina dorsale** (§8) passano e il test di forkabilità «divisione XX» passa;
5. il modulo `atc` esiste come `IModule` minimo e dimostra che il nucleo compone contributi registrati (rotte escluse dal fallback SPA, voce di menu, catalogo permessi vuoto).

### 0.2 Fuori perimetro (deciso)

- **Deploy su staging Plesk**: escluso da M0 (deciso 2 set 2026); il pacchetto self-contained e il foglio `LEGGIMI` si preparano in M1, quando Ivao.It avrà risposto alle domande A9 (§15.2c). In M0 la CI produce comunque l'artefatto `publish/`.
- News, documenti, calendario con UI, media library, contatti, staff directory, live status, mail: **M1**. In M0 esistono solo le **tabelle** che la spina dorsale richiede per essere provata (`cms_contents`, `cms_content_versions`, `cms_search_index`, `cms_calendar_entries`, `cms_award_signals`, `cms_links`) e nessuna schermata oltre a quelle elencate in §7.
- Set completo di blocchi ed editor rifinito (dnd-kit, anteprima multi-device, «allinea al template»): M1. In M0 il set è **minimo** (§5.4) e l'editor è la versione «a lista» essenziale.
- Notifiche, Quartz oltre al job di sync `ref_`, impersonazione, SignalR, iCal, RSS, ricerca UI (⌘K): dopo M0. L'endpoint `GET /api/search?q=` esiste solo per dimostrare la proiezione.
- Grant per VID: **tabella + calcolo** in M0 (la spina dorsale deve unirli ai derivati); la **schermata** `/staff/admin/permissions` è generata dal motore lista+form (costa poco, si fa) ma senza sospensione automatica al sync del roster (M1).

### 0.3 Versioni fissate (verificate il 2 set 2026)

| Componente | Versione | Nota |
|---|---|---|
| .NET SDK / runtime | 10.0.x | `global.json` pinna la minor; pacchetto self-contained linux-x64 |
| EF Core | 9.0.x | Pomelo 10 **non esiste** (verificato su NuGet: ultima 9.0.0). EF Core 9 gira su runtime .NET 10 |
| Pomelo.EntityFrameworkCore.MySql | 9.0.0 | `ServerVersion` esplicita `MariaDbServerVersion(new Version(11, 4, 10))`, mai `AutoDetect` |
| MySqlConnector | ≥ 2.4 | |
| Node / pnpm | 22 LTS / 10 | `packageManager` in `package.json` |
| React / Vite / TypeScript | 19 / 7 / 5.x strict | |
| Tailwind | 4 | `@import '@ivao/atmosphere-react/theme.css'` |
| `@ivao/atmosphere-react` / `-brand` | 3.1.0 / 3.0.x | dipende già da **`lucide-react`** → set di icone confermato (§7.1) |
| `@tanstack/react-router` + `@tanstack/router-plugin` | 1.170.x | **deciso** (2 set 2026): TanStack Router, non React Router |
| `@tanstack/react-query` | 5.x | |
| `react-hook-form` + `zod` + `@hookform/resolvers` | ultime | zod **4** |
| `i18next` + `react-i18next` | ultime | |
| `openapi-typescript` + `openapi-fetch` | ultime | client generato in CI |
| Serilog, Quartz.NET, FluentValidation, Mapperly, Polly (`Microsoft.Extensions.Http.Resilience`) | ultime | |
| xUnit + Testcontainers.MariaDb | ultime | immagine `mariadb:11.4.10` |
| Vitest + Testing Library; Playwright (solo smoke) | ultime | |

Tutte le versioni si pinnano nei file di lock; le dipendenze si aggiornano in PR dedicate.

---

## 1. Struttura del repository (concreta)

```
ivao-division-hub/
├── global.json                         # SDK 10.0.x
├── Directory.Build.props               # nullable, warnings as errors, LangVersion, InvariantGlobalization=false
├── Directory.Packages.props            # central package management
├── IvaoHub.sln
├── .editorconfig, .gitattributes, .gitignore, LICENSE (Apache-2.0), README.md (EN), docs/FORKING.md (EN), docs/UI-GUIDELINES.md (EN)
├── .github/
│   ├── workflows/build-test.yml        # restore/build/test .NET + Testcontainers, pnpm lint/typecheck/test/build, openapi check, publish artifact
│   ├── workflows/release.yml           # su tag: zip publish/ + note
│   └── PULL_REQUEST_TEMPLATE.md        # checklist §16.E in inglese
├── config/
│   ├── division.json                   # IT (comportamento, non contenuti)
│   ├── division.example.json           # copia commentata per chi forka
│   ├── ivao-oauth.example.json
│   └── ivao-oauth.json                 # gitignored
├── locales/
│   ├── it/{common,auth,staff,content,errors,mail}.json
│   └── en/{...stessi file...}          # un solo set, letto da SPA e backend
├── seed/
│   └── content-templates/*.json        # template di sistema (§5.6)
├── docker-compose.yml                  # mariadb:11.4.10 + mailpit
├── src/
│   ├── IvaoHub.Core/
│   │   ├── Division/                   # DivisionOptions, StaffRoleMap, Department, permessi
│   │   ├── Localization/               # Localized<T>, converter, validator, LocaleCatalog (legge locales/)
│   │   ├── Data/                       # HubDbContext, interceptor, query filter, migrazioni nucleo, MapCrud
│   │   ├── Auth/                       # OIDC BFF, HubUser/ICurrentUser, policy provider, DepartmentAuthorizationHandler, grants
│   │   ├── Ivao/                       # IvaoApiClient, RefDataSyncJob, entità ref_
│   │   ├── Content/                    # cms_: Content, ContentVersion, Link, SearchIndexEntry, CalendarEntry, AwardSignal, BlockDocument envelope, data-block providers, publish service
│   │   ├── Modules/                    # IModule, ModuleRegistry, contributi (nav, permessi, widget, blocchi, fallback SPA)
│   │   └── Services/                   # Quartz host, clock, audit, version
│   ├── IvaoHub.Web/                    # Program.cs, endpoint mapping, /api/me, /api/version, /health, SPA fallback, wwwroot
│   └── IvaoHub.Modules.Atc/            # IModule minimo (§6.4)
├── tests/
│   ├── IvaoHub.UnitTests/
│   └── IvaoHub.IntegrationTests/       # Testcontainers MariaDB 11.4.10, WebApplicationFactory
├── web/
│   ├── package.json, vite.config.ts, tsconfig.json, tailwind (via @tailwindcss/vite)
│   └── src/
│       ├── routes/                     # TanStack Router file-based (§7.3)
│       ├── app/                        # providers (Query, Router, i18n, Bootstrap), layouts
│       ├── shared/api/                 # client openapi-fetch + hooks Query generati/standard
│       ├── shared/ui/                  # componenti custom dell'elenco chiuso (§7.2)
│       ├── shared/icons/               # solo icone assenti da lucide
│       ├── shared/forms/               # generatore form da zod, LocaleFields, problem-details
│       ├── shared/list/                # motore lista (DataTable + search params)
│       ├── shared/i18n/
│       ├── blocks/                     # schema zod BlockDocument, registry, componenti blocco (del nucleo), editor a lista, renderer
│       ├── features/{auth,me,staff,admin,content,links}/   # nucleo
│       └── modules/<key>/              # UN SOLO punto per il frontend di ogni modulo (§6.5): index.ts esporta blocchi, widget, route, ns i18n
└── docs/internal/                      # IT: piano, questo design, decisions/
```

Regole: `web/dist` → copiato in `src/IvaoHub.Web/wwwroot` dal target MSBuild `PublishSpa` (solo in `dotnet publish`) e dal task CI; in dev Vite proxya `/api`, `/auth`, `/health` verso Kestrel `http://localhost:5000`.

---

## 2. Configurazione e avvio

### 2.1 `division.json` → `DivisionOptions`

Schema come piano §4.1. Caricato con `AddJsonFile("config/division.json", optional: false, reloadOnChange: false)` e validato con `ValidateDataAnnotations().ValidateOnStart()` + un `IValidateOptions<DivisionOptions>`: `code` 2–3 lettere maiuscole, `locales` non vuoto e contenente `defaultLocale`, `timezone` valido (`TimeZoneInfo.FindSystemTimeZoneById`), `firStaffScope ∈ {all, own}`, `modules` solo chiavi note al `ModuleRegistry` (warning per chiavi ignote).

`DivisionOptions.Locales` alimenta il validatore di `Localized<T>` (§3.1) e la lingua di fallback.

### 2.2 `config/ivao-oauth.json` → `IvaoOAuthOptions`

`AddJsonFile("config/ivao-oauth.json", optional: **true**, reloadOnChange: true)`; le variabili d'ambiente `Ivao__*` vincono e da sole bastano (piano §6.1), quindi la garanzia «l'app non parte se manca» la dà il **validatore**, non il caricatore di file. Campi: `Authority`, `ClientId`, `ClientSecret`, `LoginUrl`, `RedirectUri`, `PostLogoutRedirectUri`, `Scopes` (del membro) e `ApiScopes` (dell'applicazione, `client_credentials`). Validazione all'avvio (fail-fast, messaggio chiaro senza il secret): tutti i campi presenti, `RedirectUri` termina con `/auth/callback`, `LoginUrl` termina con `/auth/login`, stesso host per entrambi. Il secret non compare mai in log, `/api/me`, OpenAPI o pagine di errore.

### 2.3 `secrets/*.json` e ambiente

`Program.cs` aggiunge ogni `*.json` in `secrets/` (se la cartella esiste) **dopo** `appsettings.{Env}.json`, così vince. Chiavi attese: `ConnectionStrings:Default`, `Smtp:*` (M1), `DataProtection:KeysPath` (default `hub-keys/`). `AllowedHosts` obbligatorio in Production (host filtering: una richiesta con `Host` falsificato non va servita affatto; il `redirect_uri` OIDC non c'entra, quello viene preso alla lettera da `ivao-oauth.json`).

In Production si aggiungono **HSTS** (trenta giorni, senza `includeSubDomains` né preload: l'hub è un host sotto un dominio condiviso, e una policy HSTS è reversibile solo quanto il suo `max-age`) e la **redirezione a https**, disattivabile con `Https:Redirect=false` per chi la fa già fare al proxy. Entrambe vanno **dopo** `UseForwardedHeaders`: prima, lo schema è quello del salto dal proxy e la redirezione diventa un ciclo.

`ForwardedHeaders:TrustedNetworks` è l'elenco **in CIDR** dei proxy di cui si crede `X-Forwarded-For`/`X-Forwarded-Proto`, e in Production è **obbligatorio**: senza, l'applicazione non parte. Svuotare `KnownNetworks` e `KnownProxies` senza rimpiazzarle — che è ciò che si faceva — non vuol dire «fidati di Cloudflare» ma «fidati di chiunque», e su quell'indirizzo poggiano il rate limiter di `/auth/*` (aggirabile cambiando un header a ogni richiesta) e la colonna `ip` di `hub_audit_log`. Con la lista vuota fuori da Production il middleware **non entra nella pipeline**, quindi in sviluppo l'indirizzo è quello vero della connessione. Nota: `docs/internal/decisions/2026-09-03-proxy-fidati.md`.

### 2.4 Avvio

Ordine in `Program.cs`: opzioni → Serilog (file rolling `logs/hub-.log` + console) → Data Protection su `hub-keys/` → DbContext (pool ≤ 15, `MaximumPoolSize=15` nella connection string di default) → auth (§4) → `ModuleRegistry` (scopre gli `IModule` referenziati da `IvaoHub.Web`, filtra con `division.modules`) → Quartz → endpoint. Poi, **prima di accettare traffico**: `Database.Migrate()` per il contesto del nucleo e per ogni modulo abilitato; bootstrap superadmin (§4.5); seed template (§5.6, per chiave di seed); scrittura di `diagnostics/startup.txt` (versione, migrazioni applicate, moduli attivi, superadmin count — mai segreti). Se la migrazione fallisce l'app **non parte** e il file lo dice.

`/health` (liveness: DB ping) e `/api/version` (`{ version, commit, builtAt, dotnet }` da `AssemblyMetadata`) sono anonimi e `Cache-Control: no-store`.

---

## 3. Spina dorsale del dominio (`IvaoHub.Core`)

### 3.1 `Localized<T>`

```csharp
// Localization/Localized.cs
public sealed record Localized<T> : IReadOnlyDictionary<string, T>
{
    private readonly ImmutableSortedDictionary<string, T> _values;   // chiave = codice lingua ("it", "en")
    public Localized(IEnumerable<KeyValuePair<string, T>> values);
    public static Localized<T> Empty { get; }
    public T? Get(string locale);                                     // null se assente
    public T? Resolve(string locale, string fallback);                // locale → fallback → prima disponibile
    public Localized<T> With(string locale, T value);
    public bool HasAll(IEnumerable<string> locales);                  // usato dal validatore
    // IReadOnlyDictionary members…
}
public static class LocalizedExtensions { public static Localized<string> L(this string it, string en) … }   // solo nei seed/test
```

- **EF**: `LocalizedConverter<T>` (JSON ↔ `Localized<T>`, `System.Text.Json`, `WriteIndented=false`, chiavi ordinate) + `LocalizedComparer<T>`; registrati una volta in `HubDbContext.ConfigureConventions` per `Localized<string>` e `Localized<JsonNode>`; colonna MariaDB `json` (Pomelo `HasColumnType("json")`). Nomi: si usa `EFCore.NamingConventions` (`UseSnakeCaseNamingConvention`) per tutto, più **una** convenzione del modello (`LocalizedColumnConvention`, in `ConfigureConventions`) che appende `_i18n` alle colonne di tipo `Localized<T>`: `Title` → `title_i18n`. Un solo posto decide i nomi.
- **API**: serializzato come oggetto `{ "it": "...", "en": "..." }`; il `JsonConverter` è registrato nelle `JsonOptions` globali. Una **lingua** assente torna vuota e mai null; un **campo dichiarato** `Localized<T>?` e non valorizzato torna `null`, che è quello che lo schema OpenAPI dichiara — sono due cose diverse, e confonderle costava un 500 sul primo `GET` di un link senza descrizione. Nota: `docs/internal/decisions/2026-09-03-localized-nullable-nelle-api.md`. OpenAPI: schema `LocalizedString` = `additionalProperties: string` con `x-localized: true` (estensione usata dal generatore di form per scegliere `LocaleFields`).
- **Validazione**: `LocalizedRules.Required(DivisionOptions)` per FluentValidation → errore `errors.localized.missing` con `locales` mancanti; regola «tutte le lingue prima di pubblicare» vive in `ContentPublishService` (§5.5), non nei DTO (una bozza può essere incompleta).
- **Lettura pubblica**: la SPA riceve sempre l'oggetto intero e risolve con `useLocalized()` (locale corrente → `defaultLocale`); niente endpoint «per lingua».

### 3.2 Interfacce trasversali ed enum

```csharp
public enum Department { HQ, SOD, FOD, AOD, TD, MD, ED, PRD, WD }     // codici IVAO; stesso vocabolario di StaffRoleMap; serializzato come stringa
public enum Visibility { Public, Members, Staff, Department }
public enum PublishStatus { Draft, Published }

public interface IOwnedByDepartment { Department OwnerDepartment { get; } }
public interface IVisible          { Visibility Visibility { get; } }
public interface IPublishable      { PublishStatus Status { get; } DateTime? PublishedAt { get; } }
public interface IAuditable
{
    DateTime CreatedAt { get; set; } int CreatedBy { get; set; }      // VID
    DateTime UpdatedAt { get; set; } int UpdatedBy { get; set; }
}
public interface IHasFir { string? Fir { get; } }                       // opzionale, per firStaffScope = own (nessuna entità di M0 la usa)
```

Colonne: `owner_department varchar(4)`, `visibility varchar(16)`, `status varchar(16)`, `created_at/updated_at datetime(6)`, `created_by/updated_by int`. Indice `(owner_department, status)` dove esistono entrambi.

### 3.3 `ICurrentUser` e `HubPrincipal`

```csharp
public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    int Vid { get; }                                   // 0 se anonimo
    bool IsSuperadmin { get; }
    bool IsStaff { get; }
    string Locale { get; }
    IReadOnlySet<Department> Departments { get; }       // dai claim (posizioni divisionali)
    bool HasAllDepartments { get; }                     // Director (DIR/ADIR), Web (WM/AWM) o superadmin: vede/scrive ogni dipartimento
    IReadOnlySet<string> Firs { get; }                  // dalle posizioni FIR
    IReadOnlyList<EffectivePermission> Permissions { get; }
    bool Has(string permission, Department department);           // su quella riga; superadmin → true
    bool HasAny(string permission);                               // «può farlo, in generale»: un dipartimento qualsiasi, tutti, o globale
}
public readonly record struct EffectivePermission(string Name, Department? Department, string Source); // Source: "role:ED/coordinator" | "grant:123" | "superadmin"
```

`HasAllDepartments` è un **fatto del ruolo** e viaggia in un claim suo (`alldept`), scritto da `HubClaims.BuildIdentity` da `RolePermissionMatrix.ReachesEveryDepartment` sulle posizioni. **Non si deduce dalla forma della lista dei permessi**: l'entrata «permesso non-globale con dipartimento `null`» appartiene anche a una posizione di IVAO HQ (che deve solo leggere) e viene consumata dall'espansione di un deny (§6.3), quindi come indizio sbaglia in tutt'e due le direzioni. Nota: `docs/internal/decisions/2026-09-03-reaches-every-department.md`; test `ReachesEveryDepartmentTests`.

`PermissionSet.Has/HasAny` contengono la regola vera e propria, così che i doppioni di test rispondano con **lo stesso codice** e non con una copia.

Implementazione `HttpContextCurrentUser` che legge i claim del cookie (§4.3); i permessi effettivi sono nel ticket (ricalcolati al login e quando un grant cambia → `SecurityStamp` in `hub_users` confrontato a ogni richiesta dal `CookieAuthenticationEvents.OnValidatePrincipal`, con cache 60 s per VID in `IMemoryCache`; chi scrive grant o superadmin **invalida la voce di cache** del VID toccato tramite `ISecurityStampCache.Invalidate(vid)`, così l'effetto è immediato).

### 3.4 `HubSaveChangesInterceptor` — l'unico interceptor

`SavingChangesAsync`:

1. **Audit/timestamp** per ogni `IAuditable` aggiunto o modificato (`UtcNow` da `IClock`, VID da `ICurrentUser`, 0 per i job).
2. **Guardia di scrittura**: per ogni entità `IOwnedByDepartment` aggiunta/modificata/eliminata, se `ICurrentUser.IsAuthenticated` e non `IsSuperadmin`, verifica `Has(requiredPermission, entity.OwnerDepartment)` dove il permesso richiesto è `<Area>.Edit` (l'area è dichiarata sull'entità con `[PermissionArea("Content")]`, default = nome del DbSet). Se fallisce → `ForbiddenDomainException` (mappata a 403). Questa è la **rete di sicurezza** che i test della spina dorsale colpiscono: nessun endpoint può scrivere in un dipartimento altrui nemmeno dimenticando la policy.
3. **Righe di `hub_audit_log`** per ogni entità marcata `[Audited]` (before/after JSON delle proprietà scalari, `is_superadmin`). Il prima/dopo si cattura qui, perché solo ora il change tracker lo sa, ma la riga si **scrive nel secondo tempo** insieme alle proiezioni: prima del salvataggio una riga nuova non ha `id` e l'audit di una creazione punterebbe a `0`. Per un update si registrano le sole proprietà cambiate; la colonna di concorrenza è esclusa.
4. **Proiezioni** (§3.6) — in due tempi, perché prima del salvataggio le entità nuove non hanno `Id`: in `SavingChangesAsync` l'interceptor apre una transazione se non ce n'è una (e la segna come «propria») e raccoglie le entità `IProjectable` toccate con il loro stato; in `SavedChangesAsync`, con un flag di rientranza **per contesto** tenuto dall'interceptor (e non un campo di `HubDbContext`: lo stesso interceptor scoped serve il contesto del nucleo e quello di ogni modulo) che disattiva i punti 1–4 durante il secondo giro, calcola gli snapshot, fa upsert/delete tramite `ProjectionWriter` e un secondo `SaveChanges`, poi fa commit della transazione propria; in `SaveChangesFailedAsync` fa rollback. **Anche il secondo tempo può fallire per conto suo** (un `Project()` che inciampa sui propri dati, una riga di proiezione che viola un vincolo): in quel caso l'interceptor fa rollback della transazione propria e rilascia lo stato, altrimenti la scrittura resterebbe né committata né annullata e la voce resterebbe nella tabella dei pending. Test `InterceptorFailureTests`. Se la transazione era del chiamante, il commit resta al chiamante (le proiezioni sono comunque dentro). I test `ProjectionUpsertedInSameTransaction` coprono entrambi i casi.

Registrato una volta in `AddHubDbContext`; ogni `DbContext` di modulo lo riceve dallo stesso metodo (`AddModuleDbContext<T>`), quindi non può essere «dimenticato».

### 3.5 Global query filter di visibilità

In `OnModelCreating`, per ogni entità che implementa **sia** `IVisible` **sia** `IOwnedByDepartment` (via reflection sul modello; `IVisible` da sola non basta perché `Department` richiede il proprietario), il filtro è costruito come **espressione su scalari** che il contesto legge da `ICurrentUser` a ogni istanza (EF Core 9 traduce solo campi/proprietà del contesto, non chiamate a servizi): `SeesEveryDepartment` (superadmin o `HasAllDepartments`), `SeesStaffRows`, `SeesMemberRows`, `VisibleDepartments` (`List<Department>`) →
`e => SeesEveryDepartment || e.Visibility == Public || (SeesMemberRows && e.Visibility == Members) || (SeesStaffRows && e.Visibility == Staff) || (e.Visibility == Department && VisibleDepartments.Contains(e.OwnerDepartment))`.
Sono **proprietà pubbliche** che leggono `ICurrentUser` quando la query parte, non campi valorizzati nel costruttore: un contesto può nascere prima che il cookie sia stato validato, e congelare lì la risposta darebbe a quella richiesta la visibilità di un anonimo. Le entità `IPublishable` aggiungono `&& e.Status == Published` nello stesso filtro (EF Core 9 ha un solo filtro per entità; i filtri nominati sono EF 10). Il **back-office** usa sempre `.IgnoreQueryFilters()` + filtro di dipartimento + policy, e lo fa **solo** dentro `MapCrud` (§3.9); le letture pubbliche non toccano mai `IgnoreQueryFilters`. Test `VisibilityFilterPerRole` e un test che nessun file fuori da `Core/Data/Crud/` contenga `IgnoreQueryFilters`.

### 3.6 `IProjectable` e le tre proiezioni

```csharp
public interface IProjectable
{
    string SourceModule { get; }                          // "core" | "events" | …
    string SourceId { get; }                              // stabile per l'entità, es. "link:42"
    ProjectionSnapshot? Project(ProjectionContext ctx);   // null = rimuovi ogni proiezione
}
// Ciò che un'entità può sapere della divisione mentre si proietta: un'entità EF non si fa iniettare
// niente, ma le lingue e il walker le servono per forza (decisione del 3 set 2026).
public sealed record ProjectionContext(IReadOnlyList<string> Locales, string DefaultLocale, BlockDocumentWalker Blocks);
public sealed record ProjectionSnapshot(
    SearchProjection? Search,
    CalendarProjection? Calendar,
    IReadOnlyList<AwardSignalProjection> AwardSignals);
public sealed record SearchProjection(string Kind, string Url, Department OwnerDepartment, Visibility Visibility,
                                      Localized<string> Title, Localized<string> Text);
public sealed record CalendarProjection(string Kind, DateTime StartsAtUtc, DateTime? EndsAtUtc, bool AllDay,
                                        Department OwnerDepartment, Visibility Visibility, string Url,
                                        Localized<string> Title, Localized<string>? Description);
public sealed record AwardSignalProjection(int Vid, string Reason);
```

Tabelle nel nucleo: `cms_search_index` con **una riga per lingua** — chiave univoca `(source_module, source_id, locale)`, colonne `kind`, `url`, `owner_department`, `visibility`, `title varchar(512)`, `text mediumtext` con indice FULLTEXT `(title, text)` — così il FULLTEXT esiste per qualunque insieme di lingue di `division.locales` senza colonne cablate per lingua (una migrazione non può conoscere le lingue di chi forka) e senza tabelle `*_translations`: è una proiezione, riscritta integralmente a ogni upsert; `cms_calendar_entries`, `cms_award_signals` (`status = pending` alla creazione, mai sovrascritto se già `handled`). `cms_search_index` e `cms_calendar_entries` implementano `IVisible`+`IOwnedByDepartment`, quindi il **global query filter di §3.5 si applica anche a loro**: un endpoint di ricerca o di calendario non può restituire una riga che il lettore non deve vedere. `cms_award_signals` no, di proposito: non ha dipartimento, è una risorsa globale nel senso di §3.9. `ProjectionWriter` è il **secondo e ultimo** posto autorizzato a `IgnoreQueryFilters` (il primo è `Data/Crud/`): deve ritrovare la riga da riscrivere chiunque stia scrivendo, o ne inserirebbe una seconda contro la chiave unica.

La lettura è **una sola per salvataggio**, non una per riga: `ProjectionWriter.Load`/`LoadAsync` caricano tutto ciò che le sorgenti toccate hanno proiettato finora (tre query per modulo), `Apply` non fa I/O. È dentro la transazione della scrittura, quindi ogni round trip è un lock tenuto aperto. La stessa separazione dà al percorso sincrono un'implementazione sincrona vera invece di bloccare su una asincrona.

L'upsert è per chiave `(source_module, source_id)`; `Project() == null` o entità eliminata → delete. Un'entità `IPublishable` in `Draft` proietta `null` **per convenzione applicata dall'interceptor**, non da ogni entità.

### 3.7 Permessi: grammatica, catalogo, policy, un solo handler

- **Nome**: `<Area>.<Azione>`; catalogo statico del nucleo in `CorePermissions`: `Content.View`, `Content.Edit`, `Content.Publish`, `Content.ManageTemplates`, `Links.View`, `Links.Edit`, `Calendar.View`, `Calendar.Edit`, `Permissions.Manage`, `Modules.Manage`, `Audit.View`, `Awards.Assign`, `Admin.Access`. Regola del catalogo: ogni area dipartimentale dichiara **sempre** `View` ed `Edit`, e `Edit` implica `View` nel calcolo dei permessi effettivi (una riga in `EffectivePermissionsCalculator`, non nel handler). I moduli aggiungono i propri via `IModule.Permissions`.
- **Scoping**: un permesso è **dipartimentale** (scope implicito dalla risorsa) salvo che sia dichiarato `global` nel catalogo (`Permissions.Manage`, `Modules.Manage`, `Admin.Access`, `Awards.Assign` con dipartimenti da `division_settings`).
- **Derivazione**: `RolePermissionMatrix` (codice, una tabella): per ogni `(Department, StaffLevel)` → elenco di permessi sul **proprio** dipartimento; `Director` (HQ coordinator/assistant) e `Web` (WM/AWM) → tutti i permessi dipartimentali su **tutti** i dipartimenti + i globali; advisor → `View/Edit` ma non `Publish/ManageTemplates`; `Trainer` → nessun permesso di nucleo; `HqStaff` → `Content.View` su tutto. La matrice è un file solo, testato riga per riga.
- **Grant** (`hub_user_grants`): `kind ∈ {permission}`, `value` = nome permesso, `department` nullable (null = tutti), `effect ∈ {grant, deny}`, `expires_at`, `suspended_at`, `granted_by`, `reason`; audit standard (`IAuditable`), **non** `IOwnedByDepartment` (è gestita in modalità globale da `MapCrud`, §3.9). Effettivi = derivati ∪ grant − deny. Vincoli server: grant solo a VID con `is_staff = true`; mai `Permissions.Manage` né globali per grant.
- **Policy provider**: `HubPolicyProvider : IAuthorizationPolicyProvider` — qualsiasi nome `X.Y` presente nel catalogo diventa una policy con `PermissionRequirement("X.Y")`; nomi ignoti → eccezione all'avvio (test che enumera gli attributi `[Authorize(Policy)]` e i `RequireAuthorization("...")`).
- **L'unico handler**: `DepartmentAuthorizationHandler : AuthorizationHandler<PermissionRequirement>`. Senza risorsa → `ICurrentUser.HasAny(name)` (basta un dipartimento qualsiasi, o globale: negare qui chiuderebbe a ogni coordinatore la lista del proprio dipartimento, che il `MapCrud` filtra riga per riga subito dopo). Con risorsa `IOwnedByDepartment` → `Has(name, resource.OwnerDepartment)`; con `IHasFir` e `firStaffScope = own` → la FIR della risorsa deve essere tra `ICurrentUser.Firs` salvo Director/coordinatori. Endpoint minimal API usano `authorizationService.AuthorizeAsync(user, entity, "Content.Edit")` **solo dentro `MapCrud`** e nei servizi del nucleo; un modulo non chiama mai `AuthorizeAsync` con logica propria.

### 3.8 `StaffRoleMap`

```csharp
public enum StaffLevel { Coordinator, Assistant, Advisor, Member }     // Member = trainer T01–T99
public sealed record StaffPosition(string Raw, Department? Department, StaffLevel Level, string? Fir, StaffRole Role);
public enum StaffRole { Director, SpecialOps, FlightOps, AtcOps, Training, Trainer, Membership, Events, PublicRelations, Web, FirChief, FirAssistantChief, FirAdvisor, HqStaff }
public static class StaffRoleMap
{
    public static StaffPosition? Parse(string position, string divisionCode, IReadOnlySet<string> firIds);
}
```

Ordine dei pattern esattamente come piano §4.1 (`T\d\d` prima di `TA\d`, `TA\d` prima di `TC`/`TAC`); test parametrico con l'elenco completo per `IT`, `XX`, `XXX` e FIR `LIRR`. Le posizioni FIR si riconoscono solo se il prefisso è in `ref_ivao_centers` (§4.6): finché la tabella è vuota (primo avvio senza rete) si loggano come «non riconosciute», non si perdono (restano in `hub_user_staff_positions.raw`).

### 3.9 `MapCrud<TEntity, TDto>` — l'unico motore CRUD server

```csharp
public static RouteGroupBuilder MapCrud<TEntity, TListDto, TDetailDto, TWriteDto>(
    this IEndpointRouteBuilder app, string pattern, Action<CrudOptions<TEntity, TListDto, TDetailDto, TWriteDto>> configure)
    where TEntity : class;      // se TEntity : IOwnedByDepartment → modalità dipartimentale; altrimenti → modalità globale (vedi sotto)

public sealed class CrudOptions<…>
{
    public string PermissionArea { get; set; }                 // "Links" → ReadPolicy = Links.View, WritePolicy = Links.Edit (default)
    public string? ReadPolicy { get; set; }                    // override espliciti (es. grants: Permissions.Manage per entrambi)
    public string? WritePolicy { get; set; }
    public bool ReadOnly { get; set; }                          // solo GET (audit log)
    public Expression<Func<TEntity, object>> DefaultOrder { get; set; }
    public IList<Expression<Func<TEntity, string?>>> SearchFields { get; }   // colonne per ?q= (su Localized usa JSON_EXTRACT per lingua corrente)
    public IList<string> Filterable { get; }                    // nomi proprietà ammessi in ?filter[prop]=
    public IList<string> Sortable { get; }
    public bool AllowDelete { get; set; } = true;
    public Func<TEntity, TListDto> ToList; Func<TEntity, TDetailDto> ToDetail; Action<TWriteDto, TEntity> Apply;  // Mapperly
    public IValidator<TWriteDto>? Validator { get; set; }
    public Func<DbContext, IQueryable<TEntity>>? Source { get; set; }         // default: Set<TEntity>().IgnoreQueryFilters()
}
```

Genera: `GET pattern?page&pageSize&sort&dir&q&filter[...]` → `PagedResult<TListDto>`; `GET pattern/{id}`; `POST pattern`; `PUT pattern/{id}`; `DELETE pattern/{id}` (gli ultimi tre assenti se `ReadOnly`). **Modalità dipartimentale** (`TEntity : IOwnedByDepartment`): policy `ReadPolicy`/`WritePolicy`, filtro di dipartimento sulla lista (`ICurrentUser.Departments`, o nessun filtro se `HasAllDepartments`; utenti senza dipartimenti né `HasAllDepartments` → 403), `AuthorizeAsync(entity)` sulla singola risorsa. **Modalità globale** (entità senza dipartimento: `UserGrant`, `AuditLogEntry`): solo la policy globale (`Permissions.Manage`, `Audit.View`), nessun filtro di dipartimento, nessuna `AuthorizeAsync` con risorsa. Le due modalità sono lo stesso codice con un ramo, non due helper. Poi: validazione FluentValidation → `ValidationProblem` (400, `errors` per campo con chiavi i18n `errors.*`), 409 su `DbUpdateConcurrencyException` (colonna `row_version timestamp(6)`), audit e proiezioni dall'interceptor. I DTO portano `[OpenApiListConfig]`-like metadata? **No**: la configurazione di lista/form del frontend è in TypeScript (§7.5); il server espone solo lo schema OpenAPI. `MapCrud` è l'unico posto in cui compaiono `AuthorizeAsync` e la paginazione.

### 3.10 `/api/me` — bootstrap

```ts
type Bootstrap = {
  user: null | { vid: number; firstName: string; lastName: string; locale: string; isStaff: boolean; isSuperadmin: boolean;
                 departments: Department[]; firs: string[]; positions: string[] };
  permissions: { name: string; department: Department | null }[];   // effettivi, già uniti
  division: { code: string; name: LocalizedString; locales: string[]; defaultLocale: string; timezone: string; firStaffScope: 'all'|'own' };
  modules: { key: string; department: Department | null; enabled: true; maintenance: boolean }[];
  navigation: { public: NavItem[]; staff: NavItem[] };               // composti da nucleo + IModule
  registries: { blocks: BlockDescriptor[]; widgets: WidgetDescriptor[] };
  version: string;
};
```

Anonimo → `user: null`, permessi vuoti. `Cache-Control: no-store`. Chiavi di menu come chiavi i18n (`nav.pilots`), mai testo.

### 3.11 Tabelle del nucleo create in M0

`hub_users`, `hub_user_staff_positions`, `hub_user_grants`, `hub_user_tokens`, `hub_division_settings`, `hub_audit_log`, `hub_jobs_log`, `ref_ivao_centers`, `ref_ivao_airports`, `cms_contents`, `cms_content_versions`, `cms_links`, `cms_search_index`, `cms_calendar_entries`, `cms_award_signals`. Colonne come piano §7 più `row_version` su tutte le entità scrivibili e `security_stamp` in `hub_users`. Migrazione iniziale unica `Initial` del contesto `HubDbContext` (history `__EFMigrationsHistory`); i moduli: `__EFMigrationsHistory_<modulo>` via `MigrationsHistoryTable`.

---

## 4. Autenticazione (BFF OIDC)

1. `GET /auth/login?returnUrl=` → `Challenge` OIDC (`ResponseType=code`, `UsePkce=true`, `SaveTokens=false`, `GetClaimsFromUserInfoEndpoint=true`, scope da `ivao-oauth.json`). `returnUrl` accettato solo se relativo.
2. `OnTokenValidated`/`OnUserInformationReceived`: mappa i claim reali (`id`→VID, `firstName`, `lastName`, `publicNickname`, `divisionId`, `countryId`, `rating` ATC/pilota, `discordId`, `userStaffPositions[].id`), scarta `ivao.aero/permissions`; `UserSyncService.UpsertAsync` aggiorna `hub_users` + `hub_user_staff_positions` (snapshot, `synced_at`), calcola `is_staff`, bootstrap superadmin (§4.5), salva access/refresh token in `hub_user_tokens` cifrati con `IDataProtector("IvaoTokens")`; calcola permessi effettivi e costruisce il `ClaimsPrincipal` applicativo (schema cookie `Hub`, claim compatti: `vid`, `sa`, `staff`, `dept:*`, `fir:*`, `perm:<name>[:dept]`, `locale`, `stamp`).
3. Cookie: `HttpOnly; Secure; SameSite=Lax`, 12 h scorrevole, Data Protection su `hub-keys/`. `OnValidatePrincipal` confronta `stamp` con `hub_users.security_stamp` (cache 60 s) e rigetta se cambia (grant modificato, superadmin rimosso).
4. `POST /auth/logout` (richiede header `X-Requested-With: hub`) → cancella cookie e token.
5. `OnRemoteFailure` → redirect a `/login-error?code=` (route SPA tradotta; **non** sotto `/auth`, che è escluso dal fallback SPA). Le esclusioni del nucleo dal fallback sono esattamente `/api`, `/auth/login`, `/auth/callback`, `/auth/logout`, `/health`, `/openapi`, `/scalar`; i moduli aggiungono le proprie (§6.1).
6. CSRF: middleware che rifiuta ogni `POST/PUT/PATCH/DELETE` sotto `/api` e `/auth/logout` senza `X-Requested-With: hub`; il client generato lo aggiunge sempre. Rate limiting `/auth/*` (10/min per IP) e `/api/search`.
7. Token IVAO illeggibili (chiavi perse) → `IvaoUserTokenStore.GetAsync` restituisce `null` e logga warning: il chiamante tratta come «assente» (§16.14).

### 4.5 Bootstrap superadmin

All'avvio, se `SELECT COUNT(*) FROM hub_users WHERE is_superadmin = 1` è 0: per ogni VID in `division.superAdmins` fa upsert di una riga «placeholder» (`first_name = ''`, `is_superadmin = 1`, `created_by = 0`) che il primo login completa. Altrimenti il file è ignorato. Hash dell'insieme effettivo in `hub_division_settings['superadmins.hash']`; se differisce dall'ultimo noto → riga di audit `superadmin.set_changed` (la mail arriva in M1 col servizio notifiche). Impossibile rimuovere l'ultimo superadmin (vincolo nel servizio, testato).

### 4.6 `IvaoApiClient` e `ref_`

`IvaoApiClient` (typed `HttpClient` + `AddStandardResilienceHandler`): `client_credentials` con cache del token; metodi M0: `GetCentersAsync(countryId)`, `GetAirportsAsync(countryId, includeRunways: true)`, `GetMeAsync(accessToken)`. `RefDataSyncJob` (Quartz, cron giornaliero 03:15 tz divisione + esecuzione all'avvio se le tabelle sono vuote) fa upsert in `ref_ivao_centers`/`ref_ivao_airports` con `raw_json`, scrive `hub_jobs_log`. Se l'API fallisce: log + riga `failed`, mai eccezione all'avvio; se l'API risponde ma senza righe, lo snapshot resta com'era invece di svuotarsi. Gli scope del `client_credentials` stanno in `Ivao:ApiScopes`, separati da quelli del membro: **misurato il 3 set 2026**, `/v2/centers` e `/v2/airports/all` non ne richiedono nessuno (per IT: 7 centri, 221 aeroporti). Con credenziali che non coprono questi endpoint → fixture JSON in `tests/fixtures/ivao/` e `IvaoApiClient` sostituibile con `FixtureIvaoApiClient` via `Ivao:UseFixtures=true` (solo Development, rifiutato sia alla registrazione sia nel costruttore del client). Quale dei due client risponde si decide **quando il client viene costruito**, non quando viene registrato: un test host e un deploy aggiungono sorgenti di configurazione dopo la registrazione.

---

## 5. Contenuti a sezioni — ciò che M0 costruisce

### 5.1 Entità

`Content` (`cms_contents`): `Id`, `Kind` (page|news|document), `Slug`, `OwnerDepartment`, `Visibility`, `Status`, `TemplateId?`, `IsTemplate`, `Title: Localized<string>`, `Summary: Localized<string>?`, `Seo: Localized<JsonNode>?`, `Body: JsonDocument` (opaco), `SchemaVersion int`, `PublishedVersionId?`, `PublishedAt?`, colonne nullable di news/document (esistono già, non usate in M0), `RowVersion`. Implementa `IOwnedByDepartment, IVisible, IPublishable, IAuditable, IProjectable` (`SourceId = "content:{id}"`, proietta `Search` con testo estratto dal walker §5.3; `Calendar` null). Unicità dello slug: MariaDB non ha indici filtrati, quindi indice univoco su `(kind, slug, is_template)` — un template e una pagina possono condividere lo slug, due pagine no.

`ContentVersion` (`cms_content_versions`): `Id`, `ContentId`, `Version`, `Title`, `Body` (con `frozen` popolato), `SchemaVersion`, `Changelog`, `PublishedAt`, `PublishedBy`.

`Link` (`cms_links`, entità-cavia): `Id`, `OwnerDepartment`, `Visibility`, `Title: Localized<string>`, `Url`, `Description: Localized<string>?`, `Category` (string libera), `Sort`, `IsActive`, audit, `RowVersion`. Implementa `IOwnedByDepartment, IVisible, IAuditable, IProjectable` (Search: kind `link`, url = `Url`). Permesso area `Links`.

### 5.2 Envelope di `body_json` (unico contratto che il backend conosce)

```json
{
  "schemaVersion": 1,
  "sections": [
    { "id": "s_hero", "key": "hero", "title": { "it": "…", "en": "…" }, "layout": "stacked",
      "background": "none", "padding": "md", "width": "default", "collapsed": false,
      "required": true, "locked": false, "allowedBlocks": null, "renderMode": null,
      "blocks": [
        { "id": "b_1", "type": "text", "version": 1, "props": { … }, "renderMode": null, "frozen": null }
      ],
      "sections": [ … profondità ≤ 3 … ] }
  ]
}
```

Il backend valida **solo** l'envelope (`schemaVersion` supportato, dimensione ≤ 1 MB, `id` univoci, profondità ≤ 3, `type` presente in `registries.blocks`, `required/locked/allowedBlocks` accettati solo su `is_template`), con un `JsonNode` walker. `props` è opaco. Sezione `layout ∈ {stacked, 1/2+1/2, 1/3+2/3, 2/3+1/3, 3x1/3}` (con `stacked` i blocchi vanno in colonna; con le colonne ogni blocco porta `column: 0..n`).

### 5.3 Walker generico

`BlockDocumentWalker` (Core), costruito con le lingue della divisione: `EnumerateBlocks(body)`, `EnumerateSections(body)`, `ExtractText(body, locale)` (concatena tutte le stringhe foglia dentro `props`, risolvendo gli oggetti `Localized` per lingua — un oggetto è «localized» se tutte le chiavi sono lingue della divisione), `ValidateEnvelope(body, knownBlockTypes?, isTemplate)`. I tipi noti arrivano come parametro perché il registry lo compongono i moduli (F7/F8): finché è `null` il tipo non si controlla, il resto dell'envelope sì. Serve a ricerca, publish e validazione; nessuna conoscenza di schema.

### 5.4 Registry dei blocchi e set minimo di M0

Frontend `web/src/blocks/registry.ts`: `registerBlock({ type, version, schema: zod, kind: 'content'|'data', alwaysLive?, component, editorLabelKey, icon })`. Backend `IBlockDescriptor` (`type, version, kind, alwaysLive, providerKey?`) registrato dal nucleo e dai moduli (`IModule.Blocks`) e pubblicato in `/api/me` — il **client** verifica all'avvio che ogni descrittore server abbia il componente registrato (warning per lo staff nella ui-kit).

Set M0 (tutti nel nucleo): `heading` (level, text L), `text` (markdown L, sanitizzato con `rehype-sanitize` in un solo componente `MarkdownContent`), `callout` (tone, title L, text L), `cta` (label L, href), `linkList` (**Data**: `category?`, `department?`, `limit`; provider server `LinkListProvider` → `{ items: [{title, url, description}] }`), `networkStats` **non** in M0 (M1, sempre-live). Basta a dimostrare live/frozen e sezioni libere/strutturate/derivate.

### 5.5 Pubblicazione

`POST /api/content/{id}/publish` (`Content.Publish` sul dipartimento): (1) `Title.HasAll(locales)` e walker «ogni `Localized` dentro `props` ha tutte le lingue» → altrimenti 400 con elenco dei percorsi mancanti; (2) per ogni blocco `kind = data` con `renderMode = frozen` chiama `IDataBlockProvider(type).ResolveAsync(props, currentUser)` e scrive il risultato in `frozen`; (3) crea `ContentVersion` (`Version = max+1`), aggiorna `PublishedVersionId`, `Status = Published`, `PublishedAt`; (4) l'interceptor proietta nella ricerca. `GET /api/content/public/{kind}/{slug}` restituisce **solo** la versione pubblicata (filtro visibilità dal query filter); i blocchi Data `live` vengono risolti dalla SPA con `GET /api/blocks/data/{type}?props=<base64 json>` (stesso provider, stesso `ICurrentUser`). Un `IDataBlockProvider` **non** è un mini-modulo: è un servizio del nucleo o di un modulo, registrato per `type`.

### 5.6 Template seedati

`seed/content-templates/*.json`: `section-page.json` (Landing/Section page: `hero` required+locked con `heading`+`text`; `body` libera; `links` derivata: `linkList` con `renderMode` fissato dal template), `about.json`, `policy.json` (tutto `locked`, sezioni `purpose`, `scope`, `rules`, `changelog`). Seed all'avvio guidato **solo** dalla chiave `template.system:<slug>` in `hub_division_settings`: ogni file di seed viene applicato una volta (così una release successiva può aggiungere template nuovi senza toccare quelli già modificati dallo staff). `OwnerDepartment = WD`, `Visibility = Staff`. «Nuovo da template» = `POST /api/content?templateId=` → copia profonda con nuovi `id` di sezione/blocco, `TemplateId` valorizzato, `Status = Draft`. Le pagine di sistema (home, `/start`, `/pilots`, `/about`) si seedano in **M1**; in M0 basta una pagina creata a mano dal template.

### 5.7 Permessi dei contenuti

`Content.View/Edit/Publish` per dipartimento dalla matrice; `Content.ManageTemplates` solo `Director`, `Web`, e coordinator/assistant del proprio dipartimento (riga esplicita nella matrice). Modificare una riga con `IsTemplate = true` richiede `ManageTemplates` (controllo in `MapCrud` via `CrudOptions.ExtraWritePolicy(entity => …)` — unico gancio di estensione previsto, così il caso rientra in (b) e non in (c)).

---

## 6. Moduli

### 6.1 Contratto `IModule`

```csharp
public interface IModule
{
    string Key { get; }                              // "atc", "events"… (chiave in division.modules per gli opzionali)
    Department? Department { get; }
    bool IsOptional { get; }                         // false per i quattro obbligatori
    IReadOnlyList<PermissionDescriptor> Permissions { get; }
    IReadOnlyList<NavItemDescriptor> PublicNavigation { get; }
    IReadOnlyList<NavItemDescriptor> StaffNavigation { get; }
    IReadOnlyList<BlockDescriptor> Blocks { get; }
    IReadOnlyList<WidgetDescriptor> Widgets { get; }
    IReadOnlyList<string> SpaFallbackExclusions { get; }   // prefissi che la SPA non deve intercettare
    void ConfigureServices(IServiceCollection services, IConfiguration configuration);   // DbContext via AddModuleDbContext<T>(), provider, job
    void MapEndpoints(IEndpointRouteBuilder endpoints);      // sotto /api/<Key>
    IEnumerable<Type> DbContextTypes { get; }                // per Migrate() all'avvio
}
```

`ModuleRegistry` (nucleo): riceve un **elenco esplicito** di moduli — `IvaoHub.Web/Modules.cs` contiene `public static readonly IModule[] All = [new AtcModule(), …]`, l'unico posto da toccare per aggiungere un modulo al backend — niente scansione «di tutto ciò che Web referenzia» (§6.5); esclude gli opzionali spenti in `division.modules`, espone `Enabled`, `IsInMaintenance(key)` (da `hub_division_settings['modules.<key>.maintenance']`, cache 5 s) e compone i contributi. Middleware `ModuleMaintenanceMiddleware`: `GET` sotto `/api/<key>` passano (sola lettura), altri verbi → 503 `ProblemDetails` `errors.maintenance`; i job del modulo controllano `IsInMaintenance` a inizio esecuzione.

### 6.2 Regole compile-time

`IvaoHub.Modules.*` referenziano solo `IvaoHub.Core`; `IvaoHub.Web` referenzia tutti; un test di architettura (`NetArchTest` o riflessione sugli `AssemblyReferences`) fallisce se un modulo referenzia un altro modulo o se `Core` referenzia `Web`/moduli.

### 6.3 Widget

`WidgetDescriptor(key, department?, titleKey, sizes)`; in M0 il nucleo registra `welcome` e la dashboard `/me` compone i widget presenti in `registries.widgets` (componenti in `web/src/features/me/widgets/`). Nessun widget di modulo in M0.

### 6.4 `IvaoHub.Modules.Atc` in M0

`Key = "atc"`, `Department = AOD`, `IsOptional = false`, `SpaFallbackExclusions = ["/services/vsop", "/vsop", "/_content", "/_framework"]` (aggiunte a quelle del nucleo, §4 punto 5), `PublicNavigation = [{ key: "nav.atc", path: "/atc" }]`, un endpoint `GET /api/atc/ping`. Nessuna tabella. Frontend: `web/src/modules/atc/` con manifest, una route `/atc` (pagina segnaposto tradotta) e namespace i18n `atc`. Serve a provare `IModule`, la composizione del menu e l'esclusione dal fallback.

### 6.5 Confine del modulo anche nel frontend (deciso il 2 set 2026)

Un modulo oggi si aggiunge **nel monorepo e si ricompila** (non è un plugin caricato a runtime: niente NuGet di `Core`, niente `AssemblyLoadContext`, niente bundle JS dinamici — costo alto, contro la regola del minimo codice). Per lasciare aperta quella porta a costo zero, il confine del modulo è netto anche lato SPA:

- tutto il codice React di un modulo vive in **`web/src/modules/<key>/`** e in nessun altro posto: `blocks/`, `widgets/`, `routes/` (file di route TanStack che il generatore include tramite `routeFilePrefix`/`routesDirectory` aggiuntiva, oppure route registrate a mano dal manifest), `queries.ts`, `schema.ts`, `locales/{it,en}/<key>.json` (namespace i18n del modulo, copiati in `locales/` dallo script di build);
- `web/src/modules/<key>/index.ts` esporta **un solo oggetto** `ModuleManifest = { key, blocks: BlockRegistration[], widgets: WidgetRegistration[], routes: RouteDefinition[], i18nNamespaces: string[] }`;
- `web/src/modules/index.ts` è l'**elenco esplicito** dei manifest (speculare a `IvaoHub.Web/Modules.cs`); `app/` legge quell'elenco e registra blocchi, widget e route. Nessun `import` da `modules/<a>/` a `modules/<b>/` né da `features/` a `modules/` (regola ESLint `import/no-restricted-paths`; l'inverso, `modules/` → `shared/`, `blocks/` core, è ammesso);
- ciò che un modulo offre e ciò che il server dichiara in `registries` devono coincidere: il test «registry ⇄ ui-kit» diventa «server ⇄ manifest ⇄ ui-kit».

Aggiungere un modulo = un progetto `IvaoHub.Modules.<Nome>` + una cartella `web/src/modules/<key>/` + una riga in ciascuno dei due elenchi. Quando (se) servirà il plugin vero, il perimetro da estrarre è già disegnato.

---

## 7. Frontend

### 7.1 Convenzioni UI (chiude §16.C)

- **Icone**: `lucide-react` (già dipendenza di Atmosphere 3.1.0). Regola: cercare in lucide; se assente, aggiungere in `web/src/shared/icons/<Name>.tsx` (SVG 24×24, `stroke-width` 2, `currentColor`) e esportare da `shared/icons/index.ts`; mai SVG inline in una schermata. ESLint: regola `no-restricted-syntax` che blocca `<svg` fuori da `shared/icons/` e `blocks/`.
- **Elenco chiuso dei componenti custom** (piano §8.3, versione M0): `Hero`, `SectionHeader`, `StatTile` (statico in M0), `PageShell` (titolo + azioni + breadcrumb), `EmptyState`, `LocaleSwitcher`, `LocaleFields`, `MarkdownContent`, `DataList` (motore lista), `SchemaForm` (generatore), `ProblemAlert`, `DepartmentBadge`, `VisibilityBadge`, `StatusBadge`, `ConfirmDialog`. Aggiungerne uno = decisione (c) con riga in `docs/UI-GUIDELINES.md`. `LiveStatusStrip`, `RatingBadge`, `AirportCard`, `EventTimeline`, `ContactForm` arrivano in M1+.
- **`/staff/admin/ui-kit`**: una route che monta ogni componente dell'elenco e ogni blocco del registry con props di esempio; test Vitest che verifica che ogni entry del registry compaia nella ui-kit.
- `docs/UI-GUIDELINES.md` (EN): le tre regole sopra + token Atmosphere ammessi (niente colori hex nel codice, solo classi `atmos-*`/`fuselage-*`/semantiche), dark mode obbligatoria per ogni componente, orari in UTC + tz divisione.

### 7.2 Layout e route

Tre layout: `_public` (Navbar Atmosphere + NavigationMenu dal bootstrap + footer legale HQ), `_member` (`/me/*`, richiede `user`), `_staff` (`/staff/*`, Sidebar con i dipartimenti visibili: i propri o tutti; `/staff/admin/*` richiede `Admin.Access`). Un `NotFound` e un `Forbidden` tradotti.

### 7.3 Le tre ricette TanStack Router (da copiare, mai reinventare)

```tsx
// (1) layout con guard — web/src/routes/_staff.tsx
export const Route = createFileRoute('/_staff')({
  beforeLoad: ({ context, location }) => {
    const me = context.bootstrap;                       // caricato una volta in root con ensureQueryData
    // /auth/login è un endpoint Kestrel, non una route SPA: redirect per href (navigazione piena), non per `to`
    if (!me.user) throw redirect({ href: `/auth/login?returnUrl=${encodeURIComponent(location.href)}` });
    if (!me.user.isStaff && !me.user.isSuperadmin) throw redirect({ to: '/forbidden' });
  },
  component: StaffLayout,
});

// (2) lista con search params tipizzati — web/src/routes/_staff/staff.$dept.links.tsx
const listSearch = z.object({ page: z.number().int().min(1).default(1), pageSize: z.number().int().max(100).default(25),
  sort: z.string().optional(), dir: z.enum(['asc','desc']).default('asc'), q: z.string().optional() });
export const Route = createFileRoute('/_staff/staff/$dept/links')({
  params: { parse: ({ dept }) => ({ dept: deptParam.parse(dept) }), stringify: ({ dept }) => ({ dept: deptParam.format(dept) }) },
  validateSearch: listSearch,
  loaderDeps: ({ search }) => search,
  loader: ({ context, deps, params }) => context.queryClient.ensureQueryData(linksListQuery(params.dept, deps)),
  component: LinksPage,
});
// deptParam (shared/api/department.ts) è l'UNICO punto che converte l'URL minuscolo ("ed") ↔ l'enum API ("ED"):
// lo usano le route, la Sidebar e i filter[ownerDepartment] di MapCrud.

// (3) dettaglio pubblico — web/src/routes/_public/$slug.tsx
export const Route = createFileRoute('/_public/$slug')({
  loader: ({ context, params }) => context.queryClient.ensureQueryData(publicContentQuery('page', params.slug)),
  notFoundComponent: NotFound, component: PublicContentPage,
});
```

Root context: `{ queryClient, bootstrap }`; `bootstrap` da `GET /api/me` con `staleTime: 60s`, invalidato dopo login/logout e dopo ogni mutazione su grant/moduli. Il router usa `basepath` `/` e il generatore (`@tanstack/router-plugin/vite`) scrive `routeTree.gen.ts` (in git, come raccomandato).

### 7.4 API client e Query

Il documento OpenAPI si genera **a build-time**, **senza database e senza client OAuth**: `Microsoft.Extensions.ApiDescription.Server` in `IvaoHub.Web.csproj` (`OpenApiDocumentsDirectory = artifacts/openapi/`) emette `IvaoHub.Web.json` a ogni `dotnet build`. Lo strumento **esegue l'entry point fino a `app.Run()`**, perché è lì che gli endpoint minimal API esistono (sono registrati dopo `builder.Build()`, e un tool che si fermasse alla costruzione dell'host li perderebbe tutti: misurato, con `app.Run()` saltata il documento usciva con `"paths": { }`); `HubConfiguration.IsOpenApiDocumentGeneration` gli toglie da davanti l'irrigidimento di Production, la validazione OAuth e `InitializeAsync`. Nota: `docs/internal/decisions/2026-09-03-openapi-a-build-time.md`.
`pnpm gen:api` → `openapi-typescript artifacts/openapi/IvaoHub.Web.json` → `shared/api/schema.d.ts` (in git; la CI rigenera e fallisce se il diff non è vuoto). Il transformer che marca `Localized<T>` con `x-localized` è registrato in `AddOpenApi` e quindi finisce anche nel documento a build-time. `shared/api/client.ts` = `createClient<paths>({ baseUrl: '/', headers: { 'X-Requested-With': 'hub' } })` + middleware che, su 401, invalida il bootstrap. Convenzione: ogni feature espone `queries.ts` con `queryOptions(...)` e `mutations.ts`; **nessun `fetch` diretto** (ESLint `no-restricted-globals: fetch` fuori da `shared/api/`).

### 7.5 Motore lista (`DataList`) e generatore form (`SchemaForm`)

- `DataList<TRow>({ columns: ColumnDef[], query: (search) => queryOptions, route, actions, toolbar })`: usa `DataTable` Atmosphere (TanStack Table) in modalità server-side; la paginazione/ordinamento/`q` sono **i search params della route** (ricetta 2). Colonne dichiarate in `features/<x>/list.ts` con helper `col.localized('title')`, `col.badge('visibility')`, `col.date('updatedAt')`, `col.department()`.
- `SchemaForm({ schema: zod, defaults, onSubmit, labels: nsKey })`: walk dello schema zod 4 → campi: `z.string()` → input/textarea (`.meta({ multiline: true })`), `z.number()`, `z.boolean()` → switch, `z.enum` → select, `localized(z.string())` (helper che produce un `z.record` con `.meta({ localized: true })`) → `LocaleFields` (tab per lingua + «copia dall'altra lingua»), `z.array` di oggetti → lista ripetibile, `z.object` annidato → fieldset. Etichette da i18n `<ns>.fields.<path>`. Lo stesso generatore serve al **form proprietà dei blocchi** e alle entità (`links`, `content` metadati, `grants`). Per le entità lo schema zod è scritto a mano in `features/<x>/schema.ts` **specchio del DTO** (test Vitest che confronta le chiavi con `schema.d.ts`).
- `useProblemDetails(form)`: mappa `errors[field]` del `ValidationProblem` su `form.setError` (chiavi i18n risolte); errori non di campo in `ProblemAlert`. Il client non ripete regole del server: gli schemi zod dei form-entità hanno solo tipi e `required` (che il server ripete comunque).

### 7.6 i18n

`i18next` con backend HTTP che carica `/locales/{lng}/{ns}.json` (i file di `locales/` sono serviti da Kestrel come static files **e** letti dal backend con `LocaleCatalog` per `ProblemDetails` e mail). Lingua: `user.locale` → cookie `hub.lang` → `navigator.language` → `defaultLocale`. Script `pnpm i18n:check`: ogni chiave usata (`t('...')` statiche) esiste in tutte le lingue della divisione; ogni file `en` e `it` hanno le stesse chiavi. Fallisce la CI.

### 7.7 Editor a lista (M0)

Route `/staff/$dept/content/$id`: pannello sinistro albero sezioni/blocchi (aggiungi sezione, aggiungi blocco tra gli `allowedBlocks`, sposta su/giù, duplica, elimina; sezioni `locked` mostrano solo «modifica props»), pannello destro `SchemaForm` del blocco/sezione selezionata, in alto metadati (`SchemaForm` dei metadati con `LocaleFields`), pulsanti Salva bozza / Pubblica / Anteprima (renderer con la bozza, blocchi Data `frozen` mostrati con badge «catturato alla pubblicazione» e dati live). Differenze rispetto al template (sezioni nuove/tolte): **M1**.

---

## 8. Test della spina dorsale (obbligatori, rompono la build)

Unit (`IvaoHub.UnitTests`): `StaffRoleMapTests` (tabella completa, ordine dei pattern, XX/XXX, FIR), `LocalizedTests` (converter round-trip, `Resolve`, `HasAll`), `RolePermissionMatrixTests` (ogni riga), `EffectivePermissionsTests` (derivati ∪ grant − deny, scadenza, sospensione, vincoli), `BlockDocumentWalkerTests` (envelope, profondità, testo per lingua, ordine), `DivisionOptionsValidationTests`, `IvaoOAuthOptionsValidationTests`, `ArchitectureTests` (riferimenti tra progetti), `PolicyNamesTests` (ogni policy usata esiste nel catalogo).

Integrazione (`IvaoHub.IntegrationTests`, Testcontainers `mariadb:11.4.10`, `WebApplicationFactory` con `ICurrentUser` finto e `FixtureIvaoApiClient`):
- `MigrationsApplyOnRealMariaDb` (catena completa da zero + idempotenza al secondo avvio);
- `InterceptorFillsAuditAndTimestamps`, `InterceptorBlocksCrossDepartmentWrite` (staff ED scrive un link FOD → 403 anche chiamando `SaveChanges` direttamente), `AuditLogWritten`;
- `ProjectionUpsertedInSameTransaction` (crea link → riga in `search_index`; update → aggiornata; delete → rimossa; rollback della transazione → nessuna riga), `DraftContentIsNotProjected`;
- `VisibilityFilterPerRole` (anonimo/membro/staff/dipartimento/superadmin);
- `MapCrudLinksEndToEnd` (list paginata/filtrata/ordinata/ricerca in lingua, get, create con `ValidationProblem` su lingua mancante, update con 409 su `row_version` stale, delete; 403 per dipartimento altrui; 401 anonimo);
- `AuthorizationHandlerIsTheOnlyOne` (riflessione: una sola implementazione di `IAuthorizationHandler` non-framework);
- `ContentPublishFreezesDataBlocks`, `PublicReadsOnlyPublishedVersion`, `NewFromTemplateDeepCopies`, `TemplateEditRequiresManageTemplates`;
- `SuperadminBootstrapOnlyWhenNone`, `CannotRemoveLastSuperadmin`, `SecurityStampInvalidatesCookie`;
- `ModuleRegistryComposesNavAndExclusions` (atc), `MaintenanceReturns503OnWrites`;
- **`ForkabilityXxDivision`**: avvio con `division.json` `{ code: "XX", locales: ["en"], superAdmins: [] }`, chiama `/api/me`, `/`, `/staff/admin/ui-kit` (HTML) e verifica che nessuna risposta contenga `IT-`, `LIRR`, `Italia`, `Italy`, `it.ivao.aero` e che i seed siano in `en`.

Frontend (Vitest): schemi zod dei blocchi (props di esempio validi), `SchemaForm` per ogni tipo di campo, `LocaleFields`, `useProblemDetails`, registry ⇄ ui-kit, `i18n:check`, schema-entità ⇄ `schema.d.ts`. Playwright (solo `pnpm e2e`, non bloccante in M0): login mock non disponibile → smoke anonimo su `/` e `/{slug}` pubblicato dal seed di test.

---

## 9. Decisioni prese in questo design (da riportare nel piano v0.18)

1. Router: **TanStack Router** (2 set 2026).
2. Staging Plesk **fuori** da M0.
3. Icone: `lucide-react` confermato (dipendenza di Atmosphere 3.1.0).
4. Blocchi Data risolti **lato server** da `IDataBlockProvider` registrati per `type` (nucleo e moduli): la cattura `frozen` avviene nel servizio di pubblicazione, il `live` via `/api/blocks/data/{type}`; il backend continua a ignorare `props` (li passa opachi al provider). Coerente con §16.5: l'envelope (id/type/version/props/renderMode/frozen) è l'unico contratto server.
5. Guardia di scrittura per dipartimento **nell'interceptor** oltre che nel handler: la spina dorsale non si bypassa nemmeno dimenticando la policy.
6. `security_stamp` in `hub_users` per invalidare il cookie quando cambiano grant/superadmin.
7. `cms_search_index` con una riga per lingua (`source_module, source_id, locale`) e FULLTEXT su `title`/`text`: FULLTEXT senza colonne cablate per lingua e senza tabelle `*_translations` (è una proiezione riscritta a ogni upsert, non un'entità).
11. Unicità slug dei contenuti su `(kind, slug, is_template)` (niente indici filtrati in MariaDB).
12. OpenAPI generato a build-time (`Microsoft.Extensions.ApiDescription.Server`), senza database e senza client OAuth. Lo strumento esegue l'entry point fino a `app.Run()` — è l'unico modo di vedere gli endpoint minimal API — e `IsOpenApiDocumentGeneration` disinnesca ciò che pretenderebbe un'installazione vera (§7.4).
13. `/login-error` come route SPA; sotto `/auth` solo `login`, `callback`, `logout` esclusi dal fallback.
14. `MapCrud` in due modalità (dipartimentale / globale) con `ReadPolicy`/`WritePolicy`/`ReadOnly` espliciti; `Edit` implica `View`.
15. I moduli non sono plugin a runtime (monorepo + ricompilazione), ma il confine è netto anche nel frontend: `web/src/modules/<key>/` con manifest unico, elenchi espliciti dei moduli sia in `IvaoHub.Web/Modules.cs` sia in `web/src/modules/index.ts`, regola ESLint sui percorsi (§6.5).
8. `MapCrud` ha un unico gancio `ExtraWritePolicy` (usato per `ManageTemplates`).
9. Pagine di sistema seedate in M1; in M0 solo i template di sistema.
10. EF Core 9 + Pomelo 9.0.0 confermati (nessuna Pomelo 10 al 2 set 2026).

## 10. Ancora aperto (non blocca M0)

- Dominio di staging e domande A9 (§15.2c, §15.3).
- Tutte le voci di §15 del piano.
