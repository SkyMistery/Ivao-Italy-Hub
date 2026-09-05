# HANDOFF — stato di M0

> Documento **interno** (italiano). Si aggiorna alla fine di ogni fase (piano di implementazione §A.6).
> Fonte di verità: `00-piano-di-progettazione.md`; perimetro e firme: `01-design-m0.md`; ordine: `02-piano-implementazione-m0.md`.

**Ultimo aggiornamento:** 5 settembre 2026 — **M0 è chiusa, M1 è aperta e la sua prima fase è
fatta**: design (`03-design-m1.md`), piano (`04-piano-implementazione-m1.md`) e **G0**, il giro
contro l'API vera in un browser, che chiude il debito n.1 di §10 (il racconto è in **§14**). Il
prossimo lavoro è **G1**, la media library. F9 aveva verificato invece di costruire (la checklist §16.E letta su tutto il codice, la demo a
mano, i passi reali di un fork, il tag `v0.1.0-m0`), e le fondamenta con la spina dorsale generica
sono dimostrate end-to-end su `links` e su una pagina nata da un template, che è esattamente ciò che
§16.15 del piano chiedeva. Dopo il tag sono arrivate tre PR e **nessuna di esse ha aperto perimetro
nuovo**: #29 ha rimesso il tag al posto giusto e scritto cosa aveva insegnato il giro visivo, #30 ha
chiuso le due cose che quel giro aveva visto e lasciato aperte (§13), #31 ha aggiunto una regola al
piano (§3, ultima voce), #32 ha scritto come si apre M1. **Non resta niente di M0 da finire.**
**Repository:** https://github.com/SkyMistery/Ivao-Italy-Hub (pubblico). Con il merge di #35, `main`
è **sette PR avanti** al tag `v0.1.0-m0`.
**Piano:** v0.37. **Design M0:** v2.1. **Piano di implementazione M0:** v1.6.
**Design M1:** v1.1 (`03-design-m1.md`). **Piano di implementazione M1:** v1.1
(`04-piano-implementazione-m1.md`, fasi G0–G12): **G0 è chiusa** (§14), la prossima è **G1**.
**Test:** 355 .NET verdi (253 unit + 102 integrazione) + **79 Vitest** + **10 smoke Playwright** +
**3 del giro pieno** (`pnpm e2e:full`, G0 di M1).
Nessuno skippato, **rieseguiti tutti e tre il 5 set 2026** contro la MariaDB vera prima di scrivere
questa riga: i numeri qui sopra sono misurati oggi, non ricopiati.

⚠️ **Tre difetti sono stati trovati aprendo l'applicazione a mano, dopo il tag** — e sono la stessa
cosa vista **tre** volte: **i test provano i pezzi, e niente provava la composizione.** Prima la
composizione dei provider (nessuna schermata si disegnava, §11), poi quella delle route (nessun form
del back-office era raggiungibile, §12), poi quella del layout (tutto funzionava, dentro una colonna
da 255 px, §13). Tutti corretti, tutti con una rete che fallisce se qualcuno li rifà — e la terza
misura la **geometria**, perché le prime due asserivano sul testo e il testo era giusto.

| Fase | Stato |
|---|---|
| F0 bootstrap | mergiata (PR #1) |
| F1 configurazione, avvio, DB | mergiata (PR #2) |
| F2 auth BFF, ruoli, permessi, `/api/me` | mergiata (PR #3 e #4) |
| F3 `IvaoApiClient` e dati `ref_` | mergiata (PR #5) |
| F4 spina dorsale del dominio | mergiata (PR #6) |
| F4bis revisione senior (correzioni, nessun perimetro nuovo) | mergiata (PR #9), vedi §8 |
| F5 `MapCrud` e `links` (server) | mergiata (PR #8) |
| F6 spina dorsale frontend | mergiata (PR #13) |
| F7 contenuti: entità, envelope, publish, blocchi, editor, template | mergiata (PR #17) |
| F8 moduli, admin, manutenzione, ricerca, forkabilità | mergiata (PR #20) |
| **F9 chiusura di M0** | **fusa** (PR #22, `0c387d8`), tag `v0.1.0-m0` e release pubblicata |

### Il tag

**`v0.1.0-m0` punta a `fc0edb2`**, il merge commit del terzo hotfix (PR #28, due genitori,
verificato). Ci è arrivato al **quinto** tentativo, e i quattro precedenti sono la storia di §9, §11,
§12 e §13 — **nessuno è stato lo stesso errore due volte**: il primo tag era finito sulla punta di
F8 perché il rapporto di chiusura conteneva un blocco eseguibile che saltava il merge; il secondo
puntava a F9 fusa, un'applicazione che non si apriva in un browser; il terzo a una che si apriva e
in cui nessun form del back-office era raggiungibile; il quarto a una in cui il back-office era
raggiungibile e disegnato in una colonna da 255 pixel.

La release è pubblicata e **verificata sull'artefatto, non sul commit**: lo zip
(`ivao-division-hub-v0.1.0-m0.zip`) è stato scaricato, scompattato, il suo `wwwroot/` servito, e
**tutti e nove** gli smoke Playwright eseguiti **contro quello** — cioè contro il file che qualcuno
scaricherebbe, non contro una build locale dello stesso commit. Nove su nove, geometria del layout compresa.

⚠️ **Il server con cui lo si serve deve fare il fallback SPA.** Al primo tentativo i quattro smoke
del back-office sono usciti rossi contro un pacchetto perfettamente sano: `python -m http.server` è
statico e basta, quindi un indirizzo profondo come `/staff/ed/links` risponde **404** invece di
servire `index.html`, che è quello che in produzione fa `MapFallbackToFile`. Era un difetto del
banco di prova, non della build — e per due minuti è sembrato il terzo bug della giornata. Lo script
che serve con il fallback sta nello scratchpad della sessione; ricrearlo è una decina di righe, e
il controllo che dice subito da che parte sta il problema è
`curl -o /dev/null -w '%{http_code}' <host>/staff/ed/links`: 404 è il server, non il pacchetto. Porta `wwwroot/`, `locales/{en,it}/`, `seed/content-templates/`, i due
`config/*.example.json`, `LICENSE` e `NOTICE`.

⚠️ **Un grep su un bundle minificato non è una verifica.** Il primo tentativo di controllare che la
correzione fosse dentro il pacchetto è stato cercare `TooltipProvider` negli asset: inutile, perché
i nomi sono manglati e il risultato non distingue «il provider è nel bundle» da «il provider è
montato». La verifica è **comportamentale** o non è.

`release.yml` **dipende da `build-test`**, che dallo stesso giorno include gli smoke in un browser:
i 353 test .NET, i 76 Vitest e i 9 Playwright girano prima che lo zip esista. È la
proprietà per cui quella dipendenza esiste, ed è servita due volte in un giorno.

### Il giro visivo: tre punti su quattro fatti, il quarto no

Fatto e verde: le due liste affiancate **sembrano la stessa schermata** (è il punto di avere un
motore solo, e regge); la **ui-kit nei due temi** monta tutto senza zone chiare rimaste; il **cambio
lingua** funziona ed è ora uno smoke.

⚠️ **Non fatto: anteprima dell'editor contro pagina pubblica.** Richiede un contenuto pubblicato
dietro una **sessione IVAO vera**, che né gli smoke né io possiamo produrre — gli smoke stubbano
`/api/me`. È l'ultimo punto della «definizione di fatto» di M0 (§0.1 punto 3) che nessuno ha ancora
guardato con gli occhi, anche se `ContentEndToEndTests` lo asserisce lato API. La cosa da guardare è
che le due rese siano **indistinguibili**: è lo stesso `ContentRenderer`, e se divergono non lo
stanno usando entrambe. Attenzione a due trappole: il badge dei blocchi Data lo vede **solo lo
staff**, quindi in finestra anonima non c'è ed è corretto; e se si modifica la bozza dopo aver
pubblicato, le due **devono** divergere.

### Che cosa ha trovato davvero il giro visivo, e perché conta per M1

Tre difetti in un giorno, tutti trovati **guardando l'applicazione**, nessuno da un test. E i tre
sono una scala, che vale la pena leggere in ordine perché descrive un punto cieco che si restringe:

| | Che cosa non funzionava | Perché i test non lo vedevano |
|---|---|---|
| §11 | Nessuna schermata si disegnava | Nessun test montava l'albero dei provider |
| §12 | Nessun form era raggiungibile | Nessun test montava la composizione delle route |
| §13 | Tutto funzionava, in una colonna da 255 px | Ogni test chiedeva «c'è?», nessuno «dov'è?» |

Ogni rete nuova ha chiuso il buco che la precedente lasciava aperto, e l'ultima ha dovuto misurare
**pixel** perché il testo era corretto. Se M1 aggiunge schermate, la domanda da farsi non è «ho
scritto i test?» ma **«che cosa, di questa schermata, un test non può vedere?»**.

Le due cose viste e non corrette in §13 — l'intestazione di colonna che riusava la chiave
dell'etichetta del form, e i campi tradotti più stretti degli altri — **sono state chiuse il 5 set
2026** (PR #30), entrambe estendendo un meccanismo invece di aggirarlo: il racconto e le due trappole
che nascondevano stanno in fondo a §13. **M1 non le eredita.**

### Come si apre M1

⚠️ **`gh pr list` prima di cominciare.** Il 3 set 2026 due sessioni hanno lavorato in parallelo in
worktree diversi senza vedersi: una ha aperto la PR di F5, l'altra ha rivisto F4 e ha mergiato per
prima, e F5 si è ritrovata dodici commit indietro con tre conflitti. Nessun lavoro è andato perso,
ma è stato un caso. Costa due secondi.

**M1 ha il suo design**, scritto il 5 set 2026: `03-design-m1.md` v1.0. Copre il perimetro, il set
dei blocchi (22 nuovi, il catalogo di §9.3 meno `Columns`, meno le tre già coperte da M0, meno quelle
di proprietà di un modulo), le **convenzioni dei blocchi** che chiudono piano §16.C, news e documenti,
il calendario con UI, media, contatti e notifiche, staff directory e live status, la ricerca, il menu
editoriale e le pagine di sistema, le rifiniture dell'editor, e l'ordine di lavoro proposto in tredici
fasi G0–G12.

Quattro decisioni prese aprendo M1, il 5 set 2026: il set dei blocchi è **tutto** quello che il nucleo
possiede; lo **staging Plesk esce da M1 ed entra in M2** (le risposte A9 non ci sono, e §15.2c ora
blocca M2); la **migrazione dei contenuti dal Blazor è manuale**, nessun import; il **debito n.1** —
il giro e2e contro l'API vera — è la **prima** fase di M1, non l'ultima.

**Il piano di implementazione c'è**, `04-piano-implementazione-m1.md` v1.1: una fase per sessione, i
prompt di apertura in §C, i rischi in §E, come `02-` per M0. §12 del design era l'ordine; il piano è
quello che dice cosa consegna ogni fase e con quali test si chiude. **G0 è chiusa** (§14): la
prossima sessione apre **G1**, la media library, con il prompt di §C.

Scrivendolo sono state prese **tre decisioni** che il design lasciava a chi implementa, tutte del 5 set
2026: le dimensioni di un'immagine le legge un **parser di header** per PNG/JPEG/WebP in un helper del
nucleo (niente `ImageSharp`, niente `SkiaSharp` con i suoi asset nativi in un self-contained);
l'upload multipart convive con `MapCrud` **estendendo `CrudOptions`** perché una risorsa possa non
mappare la create, non spostando l'upload su un secondo indirizzo; e le preferenze di notifica sono la
tabella `hub_notification_preferences`, non una colonna di `hub_users`. La terza ha corretto una
contraddizione dentro il design — §5.2 nominava una tabella che §10.2 non contava — che è passato a
**v1.1** e ora dice **sei** tabelle nuove.

**Il punto di partenza è pulito, e vale la pena saperlo prima di cercare code da finire.** Nessuna PR
aperta, working tree pulito, i 442 test verdi rieseguiti oggi, nessun difetto noto in sospeso, e le
due decisioni che il giro visivo aveva lasciato aperte sono state prese (§13). Quello che M1 eredita
sono **debiti scelti**, elencati e ordinati in §10, non lavoro non finito.

Serve solo Docker attivo: le credenziali IVAO ci sono e funzionano, ma da F4 in poi non le usa
nessuno se non chi vuole rifare il login vero.

---

## 1. Come si avvia (locale)

```bash
cp config/ivao-oauth.example.json config/ivao-oauth.json   # e compilarlo; mai committato
docker compose up -d                                        # MariaDB 11.4.10 + Mailpit
dotnet run --project src/IvaoHub.Web                        # API su :5000, migra il DB da sola
cd web && pnpm install && pnpm dev                          # SPA su :5173 (proxy /api, /auth, /health)
```

**Dove stanno le credenziali** (mai *quali*, e mai in chat né in un commit):

| Cosa | Dove | Chi la conosce |
|---|---|---|
| Client OAuth IVAO della divisione | `config/ivao-oauth.json`, gitignored; in alternativa le variabili `Ivao__*` | Carmine. In sviluppo si usa il client di test, registrato su `http://localhost:5173`. |
| Connection string, SMTP, `AllowedHosts`, reti dei proxy fidati | `secrets/<nome-non-indovinabile>.json`, gitignored, o variabili d'ambiente | L'installazione. In sviluppo basta `appsettings.Development.json`. |
| Chiavi Data Protection | `hub-keys/`, gitignored e **persistente** | Nessuno: si perdono e si perdono i token IVAO salvati, che il codice tratta come assenti forzando il re-login. |
| Credenziali del container MariaDB di sviluppo | `docker-compose.yml`, in chiaro e va bene così | Chiunque: è un database usa-e-getta in locale. |

Nessuno di questi file è nel repository, e nessuno finisce nel pacchetto pubblicato: stanno
**accanto** all'applicazione sul server, così un deploy non li sovrascrive mai.

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

⚠️ **`seed/content-templates/*.json` fa parte dell'installazione**, come `locales/`. `HubPaths.Seed`
lo trova, `PublishHubFiles` lo mette nel pacchetto, e `ContentTemplateSeeder` lo applica una volta
sola per file, ricordandoselo con la chiave `template.system:<slug>` in `hub_division_settings`. Un
pacchetto senza quella cartella parte lo stesso, con un warning e zero template: un sito senza
template è un sito, un sito che non si avvia no.

Nuova migrazione (**solo additiva**, mai modificare una già mergiata):

```bash
dotnet tool restore
dotnet dotnet-ef migrations add <Nome> --project src/IvaoHub.Core --startup-project src/IvaoHub.Core
```

## 2. Cosa c'è dopo F8

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

**Spina dorsale frontend (F6)**:

- **Tre layout dietro le loro guardie**, ricetta 1 del design: `_public` (aperto), `_member`
  (`/me`, redirect al login per `href` perché `/auth/login` è un endpoint Kestrel), `_staff`
  (`/staff/*`, redirect al login se anonimo e a `/forbidden` se non staff — due risposte diverse
  apposta, mandare la seconda al login è un ciclo). Il bootstrap si carica **una volta** nel root
  con `ensureQueryData` e sta nel context: nessuna guardia fa una fetch propria.
- **`DataList`**: `DataTable` di Atmosphere in modalità server-side, guidato dai search params
  tipizzati della route. La paginazione la disegniamo noi perché quella di Atmosphere scrive
  «Rows per page» in inglese; il menu di visibilità colonne è spento per lo stesso motivo. Le
  colonne sono **descrizioni** (`col.localized`, `col.badge`, `col.date`, `col.department`,
  `col.boolean`, `col.number`, `col.text`), così `features/<x>/list.ts` resta TypeScript senza JSX.
- **`SchemaForm`**: cammina uno schema zod 4 e disegna il form. `localized()` → `LocaleFields` (una
  tab per lingua, badge «vuoto», bottone «copia da»), `.meta({ multiline })` → textarea,
  `.meta({ hidden })` → campo che viaggia e non si vede (`rowVersion`), `z.enum` → select con le
  etichette da `<ns>.options.<path>.<valore>`. Un tipo non coperto **lancia**. Dentro una lista
  ripetibile l'etichetta usa il path dello schema e l'indice resta solo nel nome del campo.
- **`useProblemDetails`**: `errors[campo]` → il campo, con la chiave i18n risolta; se l'estensione
  `localized` dice quali lingue mancano, la frase le **nomina** (`Intl.DisplayNames`, così una
  lingua nuova non ha bisogno di una chiave sua). Quello che non riguarda un campo (409, 403) va in
  `ProblemAlert`.
- **Back-office `links`**: `/staff/$dept/links` e `/staff/$dept/links/$id`, **zero JSX di tabella o
  di form**. Il perimetro di F6 è dimostrato lì e da nessuna altra parte.
- **`/staff/admin/ui-kit`** dietro `Admin.Access`: monta ogni componente dell'elenco chiuso e ogni
  blocco del registry. `UI_KIT_SECTIONS` è una lista di dati e non markup, apposta: il test la legge
  senza montare router, query client e i18n.
- **`shared/modules.ts`** (`ModuleManifest`, `BlockRegistration`, `WidgetRegistration`,
  `RouteDefinition`) e **`app/registry.ts`**, che compone il registry del nucleo con quelli dei
  moduli. Riempiti in F8: `atc` è il primo.
- **`PUT /api/me/locale`** (`Core/Auth/LocaleEndpoints.cs`) e **`hasAllDepartments`** nel bootstrap.
- **35 test Vitest** (generatore per ogni tipo di campo, `LocaleFields`, `ProblemDetails`,
  registry ⇄ ui-kit, schema-entità ⇄ contratto, `deptParam`, search params) e **286 .NET**.

**Contenuti (F7)** — la fase che dimostra §9.3 del piano per intero:

- **`BlockDocumentWalker.ValidateEnvelope` completo.** Oltre a versione, dimensione, `id` univoci,
  profondità e tipo noto, ora controlla `layout` (insieme chiuso), `renderMode ∈ {live, frozen}` e
  `column` **dentro le colonne che il layout della sua sezione ha**. Nuovo `MissingLocales(body)`:
  ogni valore tradotto dentro le `props` che non è scritto in tutte le lingue, col suo percorso —
  è la metà di §5.5 che la pubblicazione chiede, l'altra è `Title.HasAll`.
- **`BlockRegistry` + `IBlockDescriptor`** (`Core/Content/BlockRegistry.cs`): composto dal container
  da ogni `IBlockDescriptor` registrato, quindi un modulo aggiunge un blocco senza che il nucleo ne
  sappia il nome. `CoreBlocks.All` sono i cinque di §5.4. Pubblicati in `/api/me` come
  `BootstrapBlock` (era `string[]`).
- **`IDataBlockProvider` + `DataBlockProviders` + `LinkListProvider`**, e
  `GET /api/blocks/data/{type}?props=<base64url>`. Il provider legge **senza** `IgnoreQueryFilters`:
  è un lettore come gli altri, e la visibilità la decide il global query filter. Le `props`
  viaggiano base64**url** perché un `+` in una query string è uno spazio; il server accetta
  entrambi gli alfabeti.
- **`/api/content` è `MapCrud`**, come `links`, con tre cose in più registrate sullo stesso gruppo:
  `POST /from-template/{templateId}`, `POST /{id}/publish`, `GET /public/{kind}/{slug}`. Il motore
  non ha imparato niente sui contenuti; le tre cose che una pagina sa fare e un link no stanno
  fuori dal motore, nello stesso file.
- **`ExtraWritePolicy` ha il suo primo uso vero**: `IsTemplate → Content.ManageTemplates`. Il test
  `TemplateEditRequiresManageTemplates` lo prova con un advisor WD, che tiene `Content.Edit` su WD
  e non `ManageTemplates`: è l'unica identità che distingue il gancio dalla policy di scrittura.
- **`ContentPublishService`**: (1) tutte le lingue, sul titolo e dentro le `props`, altrimenti 400
  con un percorso per problema e l'estensione `localized`; (2) ogni blocco `Data` con
  `renderMode = frozen` viene risolto **adesso** e la risposta finisce in `frozen`, mentre ogni
  altro blocco vede il proprio `frozen` azzerato — senza quello, rimettere un blocco `live` non
  cambierebbe niente; (3) `ContentVersion` con `Version = max+1`; (4) `Status = Published`, che è
  ciò che fa proiettare l'interceptor e passare il query filter. Tutto dentro **una** transazione
  esplicita, che l'interceptor riusa invece di aprirne una sua.
- **La bozza non viene riscritta.** La cattura vive nella versione. Ripubblicare cattura di nuovo,
  ed è tutto ciò che «ripubblica per aggiornare» significa.
- **`ContentTemplateSeeder`** e `seed/content-templates/{section-page,about,policy}.json`. I file
  portano `{ "$t": "seed.templates…" }` al posto del testo, risolto al seed nelle lingue della
  divisione: una divisione che parla solo inglese non riceve una parola di italiano. Due test unit
  li validano con lo stesso walker dell'API e rifiutano un oggetto tradotto scritto a mano dentro
  un seed.
- **Frontend `web/src/blocks/`**: `envelope.ts` (zod dell'envelope, `readBody` che non lancia mai),
  `schemas.ts`, `blocks.tsx`, `core.ts`, `registry.ts`, `ContentRenderer.tsx`, `data.ts`. Il
  renderer disegna sezioni e colonne, mostra la cattura quando c'è e chiede al provider quando non
  c'è, e avvisa **solo lo staff** di un blocco che non sa disegnare.
- **Editor a lista** (`features/content/`): albero sezioni/blocchi a sinistra, `SchemaForm` di ciò
  che è selezionato a destra, metadati in alto, anteprima con lo **stesso** renderer del pubblico.
  Le regole del template (`locked`, `required`, `allowedBlocks`) l'editor le legge **dal template**,
  per `key` di sezione: la copia non le porta con sé, e non potrebbe.
- **`/_public/$slug`**: la ricetta 3 del design, che legge solo la versione pubblicata.
- **Una cattura non può essere più visibile della pagina che la contiene.** Un provider riceve un
  `DataBlockContext`: `null` sul percorso `live` — lì il lettore è il lettore e il query filter ha
  già risposto — e la visibilità più il dipartimento del contenuto quando la risposta sta per
  essere congelata. Il tetto è una tabella in `Core/Division/VisibilityCeiling.cs`, non un
  ordinamento. **Non è una seconda copia del query filter**: quello risponde «questo lettore può
  vedere questa riga», questo risponde «questa riga può essere copiata dentro una pagina che
  leggerà qualcun altro», ed esiste solo perché la pubblicazione copia.
- **Il generatore di form ha imparato tre cose** (tutte regola (b), tutte chieste da un blocco):
  legge il `.default()` di un campo; disegna una select per un numero `.meta({ choices })`; dà a
  una `z.enum` opzionale la voce «nessuno». Senza l'ultima, una select non ha modo di tornare
  indietro e la prima scelta sarebbe definitiva.
- **322 test .NET** (237 unit + 85 integrazione) e **69 Vitest**. I nove di
  `ContentEndToEndTests` sono l'accettazione di M0 eseguita: da template, pubblicata, letta da un
  anonimo, un link in più che **non** la cambia, e il ritorno a `live` che la fa cambiare.

**Moduli, amministrazione, ricerca (F8)** — la fase in cui il nucleo smette di essere l'unica cosa:

- **`IModule` e `ModuleBase`** (`Core/Modules/`): chiave, dipartimento, opzionalità, permessi, voci di
  menu pubbliche e di staff, blocchi, widget, esclusioni dal fallback della SPA, `ConfigureServices`,
  `MapEndpoints`, `DbContextTypes`. `ModuleBase` rende vuoto tutto tranne `Key`, quindi `AtcModule`
  è lungo trenta righe e non centotrenta.
- **`ModuleRegistry`** riceve l'elenco esplicito di `IvaoHub.Web/Modules.cs`, esclude gli opzionali
  che `division.modules` nomina con `false` (**il silenzio vale sì**: una release che aggiunge un
  modulo non deve aspettare che ogni divisione modifichi la configurazione), e compone
  `PublicNavigation`, `StaffNavigation`, `SpaFallbackExclusions`. `ForApiPath` dice a quale modulo
  appartiene una richiesta leggendo il percorso, ed è scritta a mano invece che con una regex perché
  gira su ogni richiesta.
- **Le esclusioni cablate di F0 non ci sono più**: `HubPipeline` tiene le sue sei (`/api`, i tre
  `/auth/*`, `/health`, `/openapi`, `/scalar`) e il resto — `/services/vsop`, `/vsop`, `/_content`,
  `/_framework` — arriva da `AtcModule`, che è il modulo che sa perché esistono.
- **`ModuleMaintenanceMiddleware`** sta **prima del routing** e dopo l'autenticazione: mentre un
  modulo è chiuso, ogni verbo che non sia `GET`/`HEAD`/`OPTIONS`/`TRACE` sotto `/api/<key>` prende
  503 con il titolo risolto da `errors.maintenance.title` e l'estensione `module` — anche su un
  indirizzo che quel modulo non ha, perché «chiuso» vale per il prefisso e non per l'elenco delle
  rotte. Le letture passano: un dipartimento che riorganizza i propri dati non vuole che le sue
  pagine diventino bianche, vuole che nessuno tocchi niente.
- **`PermissionCatalog`** (`Auth/Permissions/`): il catalogo diventa **composto**, nucleo ∪ moduli
  abilitati, e lo interrogano `HubPolicyProvider`, `EffectivePermissionsCalculator` e
  `GrantWriteDtoValidator`. `CorePermissions` resta i nomi e la lista del nucleo; `PermissionCatalog.Core`
  è il catalogo di un hub senza moduli, ed è quello che usano i test unitari.
- **`MapCrud` in modalità globale ha tre usi veri**: `/api/admin/grants` (lettura e scrittura dietro
  `Permissions.Manage`), `/api/admin/audit` (`ReadOnly = true`, dietro `Audit.View`), e fuori dal
  motore `/api/admin/superadmins`, che è visibile **solo a un superadmin** — il catalogo non ha
  niente sopra `Permissions.Manage`, di proposito, perché un permesso capace di distribuire lo
  scavalco renderebbe lo scavalco ordinario.
- **`GrantWriteDtoValidator`** impone le tre regole che sono il perimetro del modello dei permessi:
  solo un nome del catalogo, mai un permesso globale, solo a chi questa divisione conta come staff.
  Ognuna risponde con la propria chiave i18n sul campo giusto.
- **`IAffectsUserSession`** (nota `2026-09-04-grant-e-sessione.md`, decisa da Carmine): l'entità
  dichiara di quale VID decide la sessione, e l'interceptor rigenera lo `security_stamp` **dentro la
  transazione** e svuota la cache **dopo il commit**. `UserGrant` è la prima e in M0 l'unica.
- **`DomainRefusalException`** (`Core/Services/`): «non si può, ed ecco la chiave i18n del perché».
  `DomainExceptionHandler` la trasforma nello stesso 400 con una chiave per campo che produce un
  validatore, quindi «l'ultimo superadmin non si toglie» arriva al form per la strada che il form
  già conosce. `SuperadminService` non lancia più frasi inglesi.
- **`GET /api/search`** (`Core/Content/SearchEndpoints.cs`) + **`FullTextSearch`** (`Core/Data/`):
  `EF.Functions.Match` in modalità natural language su `title`/`text` di `cms_search_index`, filtrato
  per lingua, **senza** `IgnoreQueryFilters` — le righe dell'indice dichiarano proprietario e
  visibilità, quindi il query filter globale le restringe come qualsiasi altra cosa. Anonimo.
  M0 si ferma all'endpoint: la schermata è M1.
- **`DivisionSetting` è `[Audited]`**: accendere la manutenzione lascia una riga, scritta
  dall'interceptor e non dal servizio. Effetto collaterale voluto: anche i template seedati e il
  cambio dell'hash dei superadmin lasciano una riga, con VID 0, che è la risposta giusta per una cosa
  che un'installazione fa a se stessa.
- **`/api/me` porta tre cose in più**: `modules` (tutti quelli della build, con `enabled` e
  `maintenance`), `registries.widgets` e `registries.permissions` — il **catalogo**, non i permessi
  di chi chiede. Non esiste nessun `GET /api/admin/modules`: la stessa domanda con due risposte
  sarebbe la seconda cosa da tenere allineata.
- **`WidgetRegistry`** è composto dal container come `BlockRegistry`; il nucleo registra `welcome`,
  `/me` è diventata la dashboard che compone quello che il server dichiara, e `WelcomeWidget` è
  quello che prima era il corpo di `MePage`.
- **Frontend del modulo**: `web/src/modules/atc/` con manifest, pagina, e `locales/{en,it}/atc.json`;
  `pnpm i18n:sync` li copia in `locales/`, i copiati sono **committati** e la CI fallisce sul diff —
  stessa ricetta di `pnpm gen:api`, e per due ragioni concrete: un `dotnet run` senza pnpm deve
  comunque trovare ogni file di lingua, e un `dotnet publish` non deve dipendere da quale dei due
  target MSBuild ha girato per primo.
- **Le rotte dei moduli si registrano dal manifest**, in `app/router.ts`: il generatore di TanStack
  scansiona una cartella sola e il codice di un modulo sta altrove, quindi le rotte entrano
  nell'albero generato per l'altra via che il design §6.5 prevede. Sono montate sotto `_public`, così
  una pagina di modulo ha lo stesso header e lo stesso footer di tutte le altre; non sono in
  `FileRouteTypes`, quindi `<Link to="/atc">` non compilerebbe — ed è esattamente il caso per cui
  `RouterAnchor` esiste già.
- **Tre schermate di amministrazione**: `/staff/admin/permissions` (+ `$id` e il pannello superadmin),
  `/staff/admin/modules`, `/staff/admin/audit`. La prima è la ricetta 2 senza dipartimento
  nell'indirizzo, e sembra identica a quelle dipartimentali: è il punto di avere un motore solo.
- **La ui-kit ha il terzo lato**: una sezione in cima confronta quello che il server dichiara con
  quello che questa build ha registrato (`registryDiff.ts`, funzione pura), e lo dice a parole. Il
  test `web/src/modules/manifest.test.ts` legge i sorgenti C# dei moduli e i manifest e pretende che
  dichiarino gli stessi blocchi, gli stessi widget e le stesse chiavi.
- **`ForkabilityXxDivision`**: `IVAOHUB_ROOT` su una radice temporanea con `config/division.xx.json`,
  `locales/en/` e `seed/`, un database creato apposta (come root, perché l'utente applicativo di un
  container MariaDB non può crearne uno), e la catena di migrazioni che gira da zero. Nessuna
  risposta contiene `IT-`, `LIRR`, `Italia`, `Italy` o `it.ivao.aero`, e i template seedati hanno la
  sola chiave `en`.
- **353 test .NET** (253 unit + 100 integrazione) e **74 Vitest**.

**Chiusura (F9)** — la fase che verifica invece di costruire, e che quindi vale soprattutto per
quello che ha *trovato*:

- **La revisione §16.E su tutto il codice** sta in `decisions/2026-09-04-m0-review.md`: le undici
  domande del template di PR lette una per una contro 119 file `.cs`, 110 `.ts`/`.tsx` e 40 file di
  test. Le eccezioni sono **tre schermate** che non passano dal motore lista+form, e sono tutte lo
  stesso caso: dietro non c'è una risorsa paginata (l'elenco dei moduli è quello del bootstrap,
  quello dei superadmin è un `IReadOnlyList<int>`, e il dettaglio dell'audit non esiste). Tutto il
  resto — nessun `*_translations`, un handler solo, nessun `fetch` a mano, nessun componente fuori
  dall'elenco, nessuna FK fra contesti, nessun `ExecuteDelete`, `IgnoreQueryFilters` nei due soli
  posti previsti, migrazioni additive, zero `TODO` — è verificato riga per riga e non a memoria.
- **Tre stringhe visibili all'utente erano nel codice**, e sono l'unica modifica al codice di
  produzione che F9 contiene. La più istruttiva è `aria-label="breadcrumb"` in `PageShell`: era lì
  da F6 ed è sopravvissuta a tre giri di revisione **perché non si vede** — la legge solo uno screen
  reader, e lo faceva in inglese a un lettore italiano su ogni pagina di `/staff`. Le altre due sono
  un `placeholder` di esempio e il dominio di questa divisione dentro il messaggio con cui l'app si
  rifiuta di partire senza `AllowedHosts`. Quest'ultima `ForkabilityXxDivisionTests` non poteva
  prenderla: quel test controlla le **risposte HTTP**, e un messaggio di avvio non è una risposta.
  È un limite del test che vale la pena conoscere.
- **`tools/demo-m0.md`** (inglese): la demo end-to-end che §16.15 chiede, in sette parti, dalla
  cartella vuota alla pagina pubblicata, con la checklist «definizione di fatto» del design §0.1
  spuntata e il nome del test che asserisce la stessa cosa sotto ogni parte.
- **`docs/FORKING.md`** ha i passi reali di un fork, in ordine, e la frase che li riassume: nessuno
  di quei passi è la modifica di un file sorgente.
- **Playwright non è entrato in M0**, deciso da Carmine all'apertura della fase: non è fra i cinque
  task di F9, il design §8 lo dichiara non bloccante, e la demo che il piano chiede è quella da
  eseguire a mano. È la prima voce del backlog di M1 (§10).

## 3. Regole già attive (non aggirarle nelle fasi successive)

- ESLint blocca `fetch` fuori da `shared/api`, `<svg>` fuori da `shared/icons` e `blocks`, import dal nucleo
  verso `modules/` e import tra due moduli.
- Un campo tradotto è **solo** una colonna JSON `Localized<T>`: nessuna tabella `*_translations`.
- Gli enum si salvano come stringa; la conversione è registrata una volta sola.
- La concorrenza ottimistica passa da `HasRowVersion(...)`.
- Le migrazioni sono **solo additive**; `Initial` non si tocca più.
- L'identità si legge **solo** da `ICurrentUser`. Nessun endpoint guarda i claim a mano.
- Con IVAO parla **solo** `IvaoApiClient`: retry, circuit breaker e cache del token esistono una volta.
- **«Questo nomina IVAO?»** — regola nuova del 5 set 2026 (piano §4.2, PR #31), accanto a quella che
  il progetto applica dal primo giorno, «questo nomina l'Italia?». Il codice specifico di IVAO sta in
  `Core/Ivao/`, nella metà IVAO di `Core/Auth/` (`Ivao*.cs`, `UserSyncService`, `StaffRoleMap`), nelle
  tabelle `ref_ivao_*` e nell'enum `Department` — **una ventina di file su 119** — e da nessun'altra
  parte. Fuori dal perimetro lo nominano per forza, e va bene, solo `HubDbContext` (i `DbSet` dei dati
  `ref_`) e la composition root di `IvaoHub.Web`. Verificato, non assunto: `IIvaoApiClient` risulta
  usato solo dentro `Core/Ivao/`, e **13 tabelle su 15** non sanno cosa sia IVAO.
  È una domanda da farsi mentre si scrive, **non un'astrazione da costruire**: un `IIdentityProvider`
  o un `Department` configurabile sarebbero codice speculativo che peggiora questo prodotto, e per
  CLAUDE.md §5 vorrebbero comunque una nota di decisione prima. **Conta adesso** perché M1 e i moduli
  di dipartimento aggiungeranno molte volte il volume attuale sopra un nucleo che oggi è pulito.
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
- **Niente `ExecuteDelete`/`ExecuteUpdate`**: vanno dritti al server e non passano dall'interceptor,
  quindi sono un buco nell'audit, nella guardia e nelle proiezioni. Un test di architettura li vieta
  su tutto `src/`.
- **`HasAllDepartments` non si deduce dai permessi**: è il claim `alldept`, scritto da
  `HubClaims.BuildIdentity`. Chi ha bisogno di sapere «raggiunge ogni dipartimento?» lo chiede a
  `ICurrentUser`, non alla forma della lista.
- **La regola «tiene questo permesso?» sta in `PermissionSet`**, e la chiamano sia
  `HttpContextCurrentUser` sia i doppioni dei test. Una copia in un test è un posto dove il codice
  provato e quello vero divergono in silenzio.
- **Una schermata di back-office non contiene una tabella né un form.** Una lista è un elenco di
  `ColumnSpec` più `DataList`; un form è uno schema zod più `SchemaForm`. Se il generatore non copre
  un caso, si estende il generatore (`shared/forms/schema.ts`): lancia apposta invece di saltare il
  campo, così la scorciatoia non è nemmeno silenziosa.
- **Nessuna stringa utente nel codice, e adesso è verificato anche sul codice.** `pnpm i18n:check`
  controlla che le lingue siano allineate **e** che ogni chiave scritta come stringa letterale in
  `src/` esista in tutte. Una chiave costruita a runtime non è raggiungibile: chi ne aggiunge una
  aggiunge anche il test che rende il testo.
- **Solo `shared/api/department.ts` converte un dipartimento fra URL ed enum.** Lo usano le route,
  la sidebar e il `filter[ownerDepartment]`. Un secondo posto è un posto dove `ed` e `ED` divergono.
- **`filter[nome]=valore` si scrive in un posto solo lato client**, `listQuerySerializer`: non è nel
  contratto (i suoi nomi sono le proprietà dell'entità), quindi il client generato non lo tipizza.
- **`web/src/modules/index.ts` lo legge solo `app/registry.ts`.** ESLint vieta a `blocks/`,
  `features/`, `routes/` e `shared/` di toccare `src/modules`, e vieta ad `app/` di entrare dentro
  una cartella di modulo: la lista dei manifest sì, le sue viscere no.
- **Il backend non legge mai una `props`.** Valida l'envelope, ne estrae il testo per la ricerca
  con il walker, e passa le proprietà opache al provider. Lo schema di un blocco esiste **solo** in
  TypeScript, ed è lo stesso che `SchemaForm` disegna: una copia in C# sarebbe la seconda
  descrizione di un blocco, cioè quella che va fuori sincrono.
- **Il query filter risponde a una domanda sola: «questo lettore può vedere questa riga».** Chi
  *copia* una riga dentro qualcosa che leggerà qualcun altro — oggi solo la pubblicazione, con un
  blocco `frozen` — ha una seconda domanda, e la risposta è `VisibilityCeiling`. Non allargare il
  query filter per coprirla, e non scrivere un terzo posto che ragiona sulle visibilità.
- **`IgnoreQueryFilters` si chiede a `CrudSource.BackOffice<T>`.** Il test di architettura non è
  cambiato: quella chiamata esiste solo in `Core/Data/Crud/` e in `ProjectionWriter`. Chi ha
  bisogno di leggere una bozza — la pubblicazione è la prima — chiede lì.
- **Un seed scrive come l'installazione, cioè da anonimo.** La guardia di scrittura
  dell'interceptor lascia stare chi non è autenticato proprio perché è l'applicazione stessa. Vale
  anche per i test: il doppione di `ICurrentUser` di `HubWebApplicationFactory` è anonimo finché
  `ApplicationStarted` non è passato, altrimenti l'avvio girerebbe come il coordinatore che il test
  aveva in mente e il seeder si prenderebbe un 403.
- **Un blocco è tre file, non uno.** Schema in `blocks/schemas.ts`, componente in `blocks.tsx`,
  registrazione in `core.ts`. Non è pedanteria: un modulo che esporta componenti e costanti insieme
  perde il fast refresh, ed è una cosa che si paga ogni giorno.
- **Le chiavi i18n costruite a runtime hanno il loro test.** `pnpm i18n:check` non le vede;
  `blocks/registry.test.ts` legge i file di lingua e controlla `blocks.<tipo>.label`, i campi e le
  opzioni di ogni blocco del registry. Chi aggiunge un blocco non aggiunge un test: quello c'è già.
- **Un modulo si aggiunge in due elenchi espliciti e in nessun altro posto**: `IvaoHub.Web/Modules.cs`
  e `web/src/modules/index.ts`. Niente scansione delle assembly, niente `import` dinamico: «quali
  moduli ha questa build?» è una domanda a cui si risponde aprendo un file.
- **Il catalogo dei permessi si chiede a `PermissionCatalog`, mai a `CorePermissions`.** La seconda è
  il contributo del nucleo; il primo è quello che l'installazione ha davvero, moduli inclusi.
- **Un contesto EF si registra solo con `AddHubDbContext`/`AddModuleDbContext<T>`**, ed è ora un
  test di architettura (`AddDbContext<` vietato altrove sotto `src/`). Un contesto registrato a mano
  scriverebbe senza audit, senza guardia e senza proiezioni, e `MapCrud` — che risolve il contesto
  per tipo dal container — lo servirebbe volentieri.
- **Una riga che decide la sessione di qualcuno lo dichiara** (`IAffectsUserSession`), e lo stamp lo
  rigenera l'interceptor. Un servizio che scrivesse `user.SecurityStamp = …` per conto proprio
  starebbe duplicando un meccanismo, esattamente come chi scrive una riga di audit a mano.
- **Un endpoint di modulo vive sotto `/api/<key>` e da nessun'altra parte.** È ciò che rende
  possibile la manutenzione: il middleware riconosce il modulo dal percorso.
- **Le parole di un modulo stanno in `web/src/modules/<key>/locales/`**, e `pnpm i18n:sync` le copia
  in `locales/`. Le copie sono committate e portano `_source`: chi le modifica sta modificando la
  copia sbagliata, e la CI glielo dice con un diff.
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
| `CrudOptions` ha un `ContextType` (default `HubDbContext`) | Il design dà `Source` come `Func<DbContext, IQueryable<T>>` ma la firma di `MapCrud` ha quattro parametri di tipo e nessuno per il contesto: qualcuno deve dire *quale* contesto risolvere dal container. Un modulo scriverà `o.ContextType = typeof(AtcDbContext)`; il nucleo non scrive niente. Alternativa scartata: un quinto parametro di tipo su ogni chiamata, per un valore che è quasi sempre lo stesso. |
| `CrudOptions.SearchFields` è una collezione con `Add` sovraccaricato invece di `IList<Expression<Func<T,string?>>>` | Una colonna tradotta non è una `string?`, quindi la firma del design non la accetta. Con due `Add` la sintassi d'uso resta identica (`o.SearchFields.Add(x => x.Title)`) e il tipo decide da sé se serve il JSON path: l'estrazione resta in un helper solo, `LocalizedQuery`. |
| I parametri di lista sono un record `CrudListRequest` con `[FromQuery(Name=…)]`, non letti dalla query string | Letti a mano non finivano nell'OpenAPI, quindi il client generato non li conosceva. Gli attributi servono per il **case**: senza, l'`[AsParameters]` li pubblica `Page`/`PageSize` e il resto dell'API è camelCase. |
| `JsonNumberHandling.Strict` nelle `JsonOptions` | I default web accettano anche `"5"` per `5`, e lo schema generato lo dichiara: ogni intero diventava «integer oppure string» e ogni campo TypeScript `number` oppure `string`. La nostra SPA non ha ragione di mandare un numero come stringa, e il contratto lo dice. |
| Il documento OpenAPI si genera solo quando **non** c'è un `RuntimeIdentifier` | Lo strumento deve *eseguire* l'assembly compilato, e un `publish -r linux-x64 --self-contained` da Windows non può. Il documento è un artefatto di build, non di pubblicazione: `dotnet build` lo scrive, `dotnet publish` non ne ha bisogno. |
| Le policy dei permessi dichiarano `AddAuthenticationSchemes(CookieScheme)` | `DefaultChallengeScheme` è IVAO, che è giusto per `/auth/login` e sbagliato per `/api`: senza questa riga una chiamata non autenticata prendeva 302 verso il consenso invece di 401. |
| La lingua di un membro: `languageId` di IVAO se la divisione la parla, **altrimenti inglese** | Deciso da Carmine il 3 set 2026. L'inglese non e' il ripiego «della divisione» ma quello di IVAO e del progetto: una divisione italiana serve inglese a un tedesco, non italiano. La regola sta in un posto solo (`LocalePreference`), la usano il login e il selettore di lingua di F6. Si applica **solo alla creazione della riga**: la scelta esplicita dell'utente non si sovrascrive mai. Se una divisione non elenca l'inglese fra le sue lingue, si cade sul suo default, perche' deve poter rendere qualcosa. |
| `ivao_is_staff` e `ivao_is_supervisor` sono registrati ma non decidono niente | Il nostro `is_staff` significa «ha una posizione di QUESTA divisione», ed e' quello su cui poggiano permessi e grant. Quello di IVAO include HQ e altre divisioni: tenerli separati evita di allargare il perimetro per sbaglio. Servono alla staff directory di M1. |
| I codici dei dipartimenti sono quelli di IVAO: `HQ`, `SOD`, `FOD`, `AOD`, `TD`, `MD`, `ED`, `PRD`, `WD` | Confermati da Carmine il 3 set 2026 (piano v0.21). Non e' un suffisso meccanico: ATC operations e' `AOD` ma training e' `TD`. I **suffissi delle posizioni** non cambiano, cambia il dipartimento su cui mappano. La colonna e' passata a `varchar(4)` con la migrazione additiva `WidenDepartmentCodes`, che converte anche le righe gia' scritte; `Initial` non si tocca. |
| Il tipo di una colonna e quello di un `ColumnDef` si prendono da `DataTableProps<T>['columns'][number]` | Carmine ha chiesto di **non** aggiungere `@tanstack/react-table` fra le dipendenze dirette. È già lì come transitiva di Atmosphere, ma importarla sarebbe una dipendenza fantasma; passare dai props che Atmosphere esporta davvero è onesto e non aggiunge niente al `package.json`. |
| `DataList` prende `search` e `onSearchChange`, non l'oggetto `route` | Il design abbozzava `route`. Un componente generico che entrasse nel router dovrebbe riallargare i search params a `unknown`, cioè buttare via la tipizzazione della ricetta 2. Due righe di collegamento nel file di route, dove i tipi ci sono. Design §7.5 corretta. |
| `RouterAnchor` è l'unico posto che allarga un `href` a un `to` di TanStack | Sidebar e NavigationMenu di Atmosphere passano una stringa; il `Link` è tipizzato sull'albero delle route generato, che è ciò che rende un refuso un errore di build. Un cast, in un adattatore solo, invece di uno per ogni voce di menu costruita dai dati. |
| I link legali del footer stanno in `locales/{lng}/common.json` come array di `{label, href}` | Sono contenuto, non codice: un fork li cambia dove cambia ogni altra frase. `i18n:check` tratta un array come una chiave sola, quindi le due lingue restano allineate senza doverne contare gli elementi. |
| Il cookie `hub.lang` lo scrive il client, `hub_users.locale` lo scrive il server | Sono due cose diverse: la preferenza del browser (l'unica che ha un anonimo) e quella del membro (che lo segue su un altro browser). Nessuno dei due scrive quella dell'altro. |
| Il catalogo dei permessi è un **singleton composto** e non una lista statica | Che cosa contenga dipende da quali moduli sono compilati dentro, e non è noto a compile time. `PermissionCatalog.Core` resta per chi non ha un container a cui chiedere. |
| L'interceptor prende `IMemoryCache` e non `ISecurityStampCache` | Quella legge attraverso `HubDbContext`, che è costruito con l'interceptor dentro: chiederla lì sarebbe chiedere al container di costruire un contesto per costruire un contesto. La chiave sta comunque in un posto solo, `SecurityStampCache.Forget`. |
| `AddHubModules` prende `DivisionOptions` già legato, non `IOptions` | Quali moduli sono accesi va saputo **mentre** il container si costruisce — un servizio non si registra dopo — quindi è un valore e non una pipeline di opzioni. È l'unica eccezione alla regola «la configurazione si legge quando il servizio viene costruito», e `ModuleRegistryComposesNavAndExclusions` verifica che le due letture coincidano sull'applicazione vera. |
| Non esiste `GET /api/admin/modules` | `/api/me` porta già quello stato, perché il client ne ha bisogno per disegnarsi. Una seconda risposta alla stessa domanda è una seconda cosa da tenere allineata. |
| `POST /api/atc/ping` esiste solo come bersaglio del test di manutenzione | Il middleware sta prima del routing, quindi un `POST` su un indirizzo che il modulo non ha risponde 503 quando è chiuso e 404/405 quando è aperto. È esattamente la proprietà che serviva provare, senza inventare una scrittura che il modulo non ha. |
| La schermata dei moduli non usa `DataList` | `DataList` è il motore di una lista **paginata e ordinata lato server** su una risorsa. Qui non c'è una risorsa: la lista è quella del bootstrap, e ogni riga è tre fatti e un bottone. Usarlo avrebbe voluto dire inventare un endpoint per farlo funzionare. |
| Le rotte dei moduli entrano nell'albero a runtime, con `addChildren` | Il generatore di TanStack scansiona una cartella sola, e il codice di un modulo sta altrove per decisione (design §6.5). `addChildren` sostituisce i figli **e restituisce lo stesso oggetto** che l'albero generato già tiene, quindi appendere a quelli che ci sono è come una rotta di modulo si aggiunge senza ricostruire l'albero. |
| `_source` dentro i file di lingua copiati | Un file generato che non dice di esserlo è un file che qualcuno modifica sul posto. `i18n:check` lo tratta come una chiave qualsiasi, quindi le due lingue restano allineate. |
| `setup.ts` dei test stubba `ResizeObserver` e le pointer capture | jsdom li dichiara e non li implementa, e ogni componente Atmosphere costruito su un popper Radix si misura al mount. Uno stub basta: un test asserisce su ruoli e testo, mai su una dimensione. |

## 5. Decisioni scritte (`docs/internal/decisions/`)

| File | Cosa dice |
|---|---|
| `2026-09-03-projection-context.md` | `IProjectable.Project()` riceve un `ProjectionContext` (lingue, lingua di default, walker): un'entità EF non si fa iniettare niente. **Confermata**, design §3.6 corretta. |
| `2026-09-03-has-and-has-any.md` | `ICurrentUser` fa due domande separate invece di una con il dipartimento opzionale. **Decisa da Carmine**, design §3.3 e §3.7 corrette. |
| `2026-09-03-licenza.md` | Apache-2.0, copyright «2026 Carmine Granato», con `NOTICE` fin da subito e senza header per file. **Decisa da Carmine**, piano §15.5 punto 5 chiuso. |
| `2026-09-03-openapi-a-build-time.md` | Il pacchetto Microsoft **esegue** il nostro `Program` fino a `app.Run()` per leggere gli endpoint: la frase del design «senza avviare l'app» è falsa, quella che conta («senza DB e senza client OAuth») la garantisce `HubConfiguration.IsOpenApiDocumentGeneration`. **Confermata** il 3 set 2026; design §7.4 e §9 punto 12 riformulate. |
| `2026-09-03-localized-nullable-nelle-api.md` | Una **lingua** che manca resta vuota; un **campo** dichiarato `Localized<T>?` e non valorizzato esce `null`, come dice lo schema generato. Era un 500 sul primo `GET` di un link senza descrizione. **Confermata** il 3 set 2026; design §3.1 precisata. |
| `2026-09-03-reaches-every-department.md` | `HasAllDepartments` è un claim derivato dalle posizioni, non un indizio letto dalla lista dei permessi. Design §3.3 precisata. |
| `2026-09-03-proxy-fidati.md` | Le reti dei proxy di cui si crede `X-Forwarded-For` si dichiarano, e in produzione sono obbligatorie. Design §2.3 precisata. |
| `2026-09-03-snapshot-ref-potatura.md` | Lo snapshot `ref_` cancella ciò che IVAO non elenca più, solo su risposta non vuota. |
| `2026-09-03-markdown-content.md` | `MarkdownContent` usa `react-markdown`: albero React, mai `innerHTML`, HTML grezzo non abilitato. **Decisa da Carmine**, design §7.1. |
| `2026-09-04-nuovo-da-template.md` | «Nuovo da template» è una rotta sua e non una query su `POST /api/content`, che è già la creazione generata da `MapCrud`. **Decisa da Carmine** il 4 set 2026; design §5.6 corretto. |
| `2026-09-04-grant-e-sessione.md` | Un grant invalida la sessione del suo titolare attraverso l'**entità** (`IAffectsUserSession`, applicata dall'interceptor) e non attraverso un secondo gancio di `MapCrud`: vale per chiunque scriva la riga. Dice anche che cosa significa davvero «subito» — il cookie vecchio prende 401, non viene riscritto. **Decisa da Carmine** il 4 set 2026; design §3.4 e §3.7 aggiornate. |
| `2026-09-04-frozen-e-visibilita.md` | Una cattura `frozen` non può essere più visibile della pagina che la contiene: la pubblicazione passa al provider un `DataBlockContext`, e `VisibilityCeiling` dice cosa ci sta dentro. **Decisa da Carmine** il 4 set 2026; design §5.5 corretta. |
| `2026-09-04-rotte-di-dettaglio.md` | Una lista e il suo dettaglio sono **tre** route: layout con la guardia e l'`Outlet`, `index` con i search params, dettaglio fratello. Scritte in due, il dettaglio non si disegnava mai e nessun form del back-office era raggiungibile. **Decisa da Carmine** il 4 set 2026; design §7.3 corretta. |
| `2026-09-04-smoke-in-un-browser.md` | Lo smoke in un browser diventa **bloccante** in CI e non aspetta M1: un `TooltipProvider` mancante ha ucciso ogni schermata dietro un layout con 353 test .NET e 74 Vitest verdi, perché provavano i pezzi e nessuno provava la composizione. Contiene anche il perché l'albero dei provider è diventato un componente. **Decisa da Carmine** il 4 set 2026; design §8 corretta. |
| `2026-09-04-m0-review.md` | La revisione §16.E su tutto il codice di M0: le undici domande verificate riga per riga, le tre eccezioni (schermate senza una risorsa paginata dietro), le tre stringhe visibili trovate e corrette, e un rilievo rimandato a M1 (`LocalizedExtensions`, helper di test che vive in `src/`). Scritta in F9. |

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
- ~~`DivisionOptionsValidator` accetta le chiavi modulo note ma nessuno gliene passa~~ **chiuso in
  F8**: `Program` gli passa le chiavi di `Modules.All` (tutte, non solo quelle accese: nominare un
  modulo per spegnerlo è il senso della chiave). `config/division.json` di questa divisione nomina
  `specialops`, che questa build non ha: è un **warning a ogni avvio**, ed è il comportamento
  voluto — una divisione può tenersi la chiave di un modulo che non ha ancora mergiato.
- ~~`shared/api/bootstrap.ts` e il tipo `ApiPaths` in `client.ts` sono scritti a mano~~ **chiuso in
  F5**: `schema.d.ts` è generato dall'OpenAPI e committato, `client.ts` è `createClient<paths>` e
  `bootstrap.ts` è un elenco di alias del contratto.
- **Il giro di riallineamento della documentazione, fatto a fine F9** — la sesta volta, e ha trovato
  qualcosa anche stavolta: `README.md` e `docs/FORKING.md` fermi a «phase F8 of nine», il design che
  chiamava tre test della spina dorsale con nomi che F2 aveva poi scritto in forma più esplicita, e
  `docs/FORKING.md` che prometteva «i passi di un fork vero alla fine di M0» senza averli. Tutte e
  tre corrette. La lezione è sempre la stessa e ormai ha sei conferme: **costa dieci minuti e non è
  mai stato inutile.**
- La documentazione è stata riallineata il 3 set 2026: `README.md` e `docs/FORKING.md` dicevano
  ancora «phase F1» e «phase F0»; il design `01` descriveva l'interceptor e il query filter in una
  forma che F4 ha poi cambiato; i codici di dipartimento del changelog 0.21 erano rimasti in una
  decina di esempi. Vale la pena rifare lo stesso giro alla fine di ogni fase: costa dieci minuti e
  l'alternativa è un documento che mente. **Rifatto a fine F6**, e ha trovato di nuovo qualcosa:
  `README.md` e `docs/FORKING.md` fermi a «phase F5», §9 che descriveva worktree e rami locali
  cancellati un'ora prima, e la ricetta 2 del design che dichiarava i search params dentro la route
  quando il codice li tiene in un posto solo. Nessuna di queste è grave da sola; tutte insieme sono
  il motivo per cui una sessione nuova legge una cosa e ne trova un'altra.
- ~~Il test di architettura «nessun modulo referenzia un altro modulo»~~ **fatto in F4**
  (`ArchitectureTests`, che legge i `.csproj` e non le assembly: un riferimento che il compilatore
  elide perché nessuno lo usa ancora è comunque una dipendenza della build).
  ~~`docs/UI-GUIDELINES.md` resta F6~~ **scritta in F6**: le quattro regole, ciascuna con la cosa
  che la fa fallire (ESLint, `i18n:check`, il test della ui-kit) invece di un revisore che se la
  ricorda.
- ~~Il catalogo dei permessi che `HubPolicyProvider` interroga è `CorePermissions` e basta~~
  **chiuso in F8**: `PermissionCatalog` è composto da nucleo ∪ moduli abilitati, e lo interrogano il
  policy provider, il calcolatore dei permessi effettivi e il validatore di un grant.
- **`AtcModule` non dichiara nessun permesso**, quindi il percorso «un permesso di modulo diventa una
  policy» è provato con un **modulo finto** in `ModuleCompositionTests` e non da un modulo vero. È la
  scelta onesta: il design §6.4 dice che `atc` in M0 è una voce di menu e un `ping`, e inventargli un
  permesso avrebbe voluto dire inventare anche una riga nella matrice dei ruoli per non lasciarlo a
  disposizione del solo superadmin. Il primo permesso di modulo vero è M2.
- ~~`BlockDocumentWalker.ValidateEnvelope` accetta l'elenco dei tipi di blocco noti come parametro
  opzionale~~ **chiuso in F7** per il lato server: `BlockRegistry.Types` è quello che il validatore
  riceve, e un tipo sconosciuto è 400 sul percorso del blocco. Il terzo lato — che il **manifest di
  un modulo** dichiari lo stesso insieme del server — è **chiuso in F8**:
  `web/src/modules/manifest.test.ts` legge i `*Module.cs` dei progetti di modulo e i manifest, e
  pretende che dichiarino gli stessi blocchi, gli stessi widget e le stesse chiavi. Il C# lo legge
  con due regex volutamente strette (`ModuleKey = "…"`, `new BlockDescriptor("…"`) e **fallisce se una
  regex smette di corrispondere** invece di concludere che un modulo non dichiara niente: è la
  convenzione che un modulo accetta, cioè scrivere la propria chiave e i propri blocchi come
  letterali.
- Un contesto di modulo non scrive proiezioni né audit se non ha quelle tabelle nel proprio modello:
  l'interceptor se ne accorge e non fa niente. Quando un modulo proietterà davvero (M1+), va deciso
  se condividere quelle entità o passare dal contesto del nucleo.
- ~~Il chunk JS supera i 500 kB~~ **chiuso in F6**: il router splitta per route
  (`autoCodeSplitting`) e `manualChunks` separa React (192 kB), Atmosphere (422 kB) e il renderer
  Markdown (118 kB), che è caricato solo dalle pagine che mostrano prosa. Nessun avviso di Rollup.
  Atmosphere resta il pezzo più grosso e non c'è molto da fare: è una dipendenza sola.
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
- ~~**`MapCrud` non ha ancora nessuna entità in modalità globale**~~ **chiuso in F8**: `UserGrant` e
  `AuditLogEntry` (quest'ultima con `ReadOnly = true`) sono i due usi veri, e
  `ModuleAndAdminEndToEndTests` li esercita sul cookie vero e sulle policy vere.
- ~~**`ExtraWritePolicy` non è ancora usato da nessuno**~~ **chiuso in F7**:
  `IsTemplate → Content.ManageTemplates`, provato end-to-end con un advisor WD, che è l'unica
  identità che distingue il gancio dalla policy di scrittura.
- **La ricerca `q` ignora l'accento e la maiuscola per collation, non per scelta.** `LIKE` su
  `utf8mb4_unicode_ci` è già case e accent insensitive, che è quello che vogliamo; ma la ricerca
  della lista **non** passa dal FULLTEXT di `cms_search_index`. Quella è `/api/search`, **fatta in
  F8**, ed è un altro meccanismo: qui si cerca dentro la tabella del back-office, lì nell'indice
  pubblico. Due cose che `/api/search` non fa e che M1 dovrà decidere: **non ordina per rilevanza**
  in modo esplicito (in natural language mode MariaDB restituisce già le righe in quell'ordine per un
  `MATCH` nel `WHERE`, ma non è una garanzia che si possa paginare sopra) e non evidenzia niente. E
  InnoDB ignora le parole più corte di `innodb_ft_min_token_size` (tre, di default): una ricerca di
  due lettere non torna niente, e non è un bug nostro.
- **`pageSize` è tagliato a 100 e `DefaultPageSize` è 25**, cablati nel motore. Se una schermata di
  F6 ne vorrà altri, diventano configurazione di `CrudOptions`, non un numero in più nel motore.
- ~~**`CrudScope` risolve il contesto per `Type` dal container** e nessun test pinna che i contesti
  passino dai due metodi giusti~~ **chiuso in F8**:
  `AContextIsOnlyEverRegisteredByTheTwoMethodsThatAttachTheInterceptor` vieta `AddDbContext<` fuori da
  `HubDbContextServiceCollectionExtensions.cs`. Nota che `atc` **non ha un contesto**: il ramo
  `AddModuleDbContext<T>` + `MigrateAsync` all'avvio esiste, è scritto e non ha ancora un modulo che
  lo eserciti. Il primo è M2.
- **Il `filter[...]` fa un solo confronto, l'uguaglianza.** Basta a F6 (dipartimento, visibilità,
  categoria, attivo). Intervalli e `in` non ci sono, e se servissero andrebbero nel motore.
- **Nessun test end-to-end del browser**, e in M0 non ce ne sarà uno. Playwright è previsto dal
  design (§8, «solo `pnpm e2e`, non bloccante in M0»); **F9 non l'ha aggiunto**, deciso da Carmine:
  non è fra i cinque task della fase e la demo end-to-end che il piano chiede è `tools/demo-m0.md`,
  eseguita a mano. Quindi «staff ED apre `/staff/ed/links`, crea un link, vede l'errore sul campo
  giusto» resta verificato leggendo il codice e non eseguendolo. **Prima voce del backlog di M1**,
  §10.
- **Il form dei link non permette di spostare un link fra dipartimenti.** `ownerDepartment` è
  `hidden` e viene dal path della route. Non era nell'accettazione di F6; il giorno che servisse,
  è una select ristretta a `reachableDepartments`, non un campo libero — il server rifiuta comunque
  chi non ha il permesso su **entrambi** i dipartimenti, perché `MapCrud` controlla la riga com'è e
  come diventerebbe.
- ~~**`col.department()` non è usato da nessuna lista**~~ **chiuso in F8**: la lista dei grant lo usa,
  ed è esattamente il caso per cui esisteva — una lista in modalità globale, dove ogni riga può
  appartenere a un dipartimento diverso o a nessuno.
- **La lingua del server e quella dello schermo possono divergere per un istante.**
  `PUT /api/me/locale` riemette il cookie, quindi la richiesta successiva è già nella lingua nuova;
  ma una risposta **già in volo** quando l'utente cambia lingua arriva nella precedente. Non vale la
  pena inseguirla: riguarda solo il titolo di un `ProblemDetails`, e le chiavi dei campi le risolve
  comunque il client.
- **`LocaleSwitcher` scrive `hub.lang` con `document.cookie`.** È l'unico punto del client che
  scrive un cookie a mano. Se ne servisse un secondo, va estratto un helper prima, non copiato.
- ~~**La cattura `frozen` vede quello che vede chi pubblica**~~ **chiuso il 4 set 2026** con
  l'opzione raccomandata: `DataBlockContext` + `VisibilityCeiling`, e i due test
  `VisibilityCeilingTests` e `PublishingDoesNotFreezeWhatThePageMayNotShow`. Nota aggiornata a
  «decisa».
- **«Nuovo da template» è `POST /api/content/from-template/{templateId}`** e non la query string
  che il design scriveva: `POST /api/content` è già la creazione generata da `MapCrud`, e le
  minimal API non instradano per query string. Nota
  `docs/internal/decisions/2026-09-04-nuovo-da-template.md`, design §5.6 corretto.
- ~~**Il `department` di un `linkList` è una stringa libera**~~ **chiuso il 4 set 2026**: il
  generatore disegna una `z.enum` opzionale con la voce «nessuno» (sentinella `NO_CHOICE`, perché
  la stringa vuota è riservata dal Select), e `LinkListProvider` tratta un nome che non riconosce
  come **nessuna riga** invece che come nessun filtro — un refuso deve restringere, mai allargare.
- **`seo` non ha un campo nell'editor, e resta così di proposito.** È un `Localized<JsonNode>` e il
  design non dice **cosa ci sta dentro**: inventarne la forma adesso sarebbe decidere per M1, che è
  la milestone del sito pubblico e l'unica che ha una ragione per averne una. Viene rimandato
  indietro esattamente come è arrivato, quindi non si perde. Quando M1 gliela darà, il generatore
  avrà bisogno di un tipo nuovo — «oggetto tradotto» — che è un'estensione, non un form a mano.
- ~~**Il `level` di un `heading` è un numero libero**~~ **chiuso il 4 set 2026**: resta un numero,
  perché ogni stringa dentro `props` finisce nell'indice di ricerca come testo della pagina e `"2"`
  non è testo — ma `.meta({ choices })` fa disegnare al generatore una select, quindi un livello
  che non esiste non si può nemmeno digitare.
- **Un cambio di template non si propaga, e l'editor non lo mostra ancora.** Non è un debito di F7:
  il design §7.7 lo mette in **M1** a parole («Differenze rispetto al template: M1»). Oggi le regole
  del template si leggono per `key`, e una sezione che il template ha aggiunto dopo semplicemente
  non c'è sulla pagina — che è il comportamento voluto, perché un template non deve mai riscrivere
  una pagina da solo (CLAUDE.md §2).
- ~~**L'anteprima non dice se un blocco è una cattura**~~ **chiuso il 4 set 2026**, e la vecchia
  riga di questo elenco descriveva male il fatto: una bozza non porta **nessuna** cattura — la
  pubblicazione la scrive nella versione, non all'indietro nella bozza — quindi l'anteprima mostrava
  già dati live, solo senza dirlo. Ora il badge distingue «catturato alla pubblicazione» (una
  versione) da «ora dal vivo, catturato quando pubblichi» (una bozza), e lo vede solo lo staff.
- **Nessun test end-to-end del browser, ancora.** «Il coordinatore apre l'editor, aggiunge un
  blocco, pubblica» è coperto dai test di integrazione lato API e da 74 Vitest sui pezzi, ma non è
  stato eseguito in un browser. Non è un debito di F7 e non lo è di F9: il design §8 mette Playwright
  fra le cose «solo `pnpm e2e`, non bloccante in M0». Passa a M1 (§10), dove è il punto 1.
- **Nessun test verifica il documento OpenAPI in sé** (che `/api/links` ci sia, che
  `LocalizedString` porti `x-localized`). Lo step di CI `pnpm gen:api && git diff --exit-code` lo
  copre di sponda: se il documento cambia forma, `schema.d.ts` si muove e la build cade.
- **«Il grant morde subito» significa che il cookie vecchio prende 401**, non che la sessione continua
  con i permessi nuovi. `OnValidatePrincipal` **rigetta** il cookie quando lo stamp cambia (design
  §3.3, deciso in F2), quindi la sequenza vera è: grant → 401 → login rifatto (silenzioso con IVAO
  per chi ha già dato il consenso) → permessi nuovi. È la proprietà di sicurezza giusta e la
  chiudono i test; ricostruire il principal invece di rigettarlo cambierebbe una decisione di F2 e
  non è stato fatto in F8. Se in M1 dà fastidio, si riapre lì.
- **`expiresAt` di un grant è una casella di testo.** Il generatore di form non ha un tipo «data», e
  inventarlo in F8 avrebbe voluto dire anche la conversione fra ISO e `datetime-local`. L'etichetta
  dice il formato (`YYYY-MM-DD`, vuoto = mai) e il server rifiuta una data già passata con
  `errors.grant.alreadyExpired`. Il giorno che serve davvero, è un'estensione del generatore, non un
  form scritto a mano.
- **`/staff/admin/audit` non ha una schermata di dettaglio.** `MapCrud` mappa comunque
  `GET /api/admin/audit/{id}`, che risponde con `beforeJson`/`afterJson`; la lista non li mostra
  perché sono JSON di forma diversa per ogni entità e disegnarli bene è un componente, cioè una
  decisione (c).
- **Non esiste ancora un namespace `mail`.** Il task 8 di F8 chiedeva che `pnpm i18n:check`
  includesse `errors` e `mail`: lo script legge **tutti** i namespace che trova, quindi `errors` è
  già coperto e `mail` lo sarà da sé il giorno che il servizio notifiche di M1 lo crea. Non c'è
  niente da aggiungere allo script.
- **`ForkabilityXxDivisionTests` scrive una variabile d'ambiente del processo** (`IVAOHUB_ROOT`) e la
  rimette a posto in `DisposeAsync`. È sicuro perché i test di integrazione stanno tutti in una
  collection sola e girano quindi uno alla volta, e perché l'unica classe fuori da quella collection
  (`TrustedProxiesTests`) non costruisce un host. Se un giorno se ne aggiunge una che lo fa, questa
  è la premessa che salta.
- **Tutti i test di integrazione scrivono nello stesso database**, e l'ordine dei metodi dentro una
  classe non è garantito. Un test che **conta** righe deve quindi cercare una parola sua: la prima
  versione di `SearchEndpointTests` ne condivideva una fra due test, e in locale l'ordine lo
  nascondeva mentre in CI no. Costa una costante, e il tipo di rosso che produce («aspettavo 1, ne ho
  2») fa perdere venti minuti a cercarlo nel codice di produzione.
- **Il test manifest legge il C# con due regex.** È una convenzione dichiarata in testa al file — un
  modulo scrive la propria chiave e i propri blocchi come letterali — e il test **fallisce** se la
  chiave non si trova, invece di concludere che il modulo non dichiara niente. Un modulo che
  costruisse il nome di un blocco a runtime lo romperebbe, ed è il momento giusto per accorgersene.
- **La cattura della manutenzione è di cinque secondi** (design §6.1). Chi la accende non aspetta:
  `SetMaintenanceAsync` riscrive la cache subito, perché cinque secondi della risposta precedente
  sembrano un bottone rotto. Ma un'installazione **con più processi** avrebbe cinque secondi di
  disallineamento fra loro; oggi il processo è uno solo (Passenger), e quando non lo sarà più la
  risposta è un invalidamento condiviso, non una cache più corta.

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

**La catena di release è stata provata davvero**, il 3 set 2026, con un tag usa-e-getta
(`v0.0.1-ci-test`) poi cancellato insieme alla sua release. Il `workflow_call` funziona: il job
`verify / build-test` ha eseguito i 280 test e solo dopo è partito `release`, quindi **nessun tag
può più pubblicare senza passare dai test**. Controllato anche l'archivio: si scompatta come
applicazione e porta `LICENSE`, `NOTICE`, `config/*.example.json` e `locales/` alla radice — cioè
tutto ciò che F5 punto 5 doveva aggiungere, incluso il `NOTICE` che Apache-2.0 §4(d) pretende
viaggi con ogni ridistribuzione.

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

- **Il giro di pulizia di F8, fatto subito dopo il merge** (4 set 2026), come questa sezione dice di
  fare e non «ogni tanto»: `m0/f8-moduli` tolto dal remoto insieme al merge, e in locale tolti
  quello e **sei rami vuoti** lasciati da sessioni precedenti (`claude/vai-con-f8-*`,
  `claude/procediamo-con-f8-*`, `claude/branch-change-issue-*`). Erano tutti fermi su `2bc3b87`,
  cioè esattamente `main`: zero commit propri, quindi `git branch -d` — quello che rifiuta se c'è
  qualcosa dentro — li ha accettati tutti. Adesso in locale c'è solo `main`, e nessuna worktree
  oltre al checkout principale.
- **Sul remoto c'è solo `main`.** GitHub non cancella un branch da sé quando fonde la PR, quindi lo
  si toglie a mano con `git push origin --delete <branch>` — meglio subito dopo il merge che «ogni
  tanto»: `m0/f6-frontend-backbone` e `docs/f6-merged` sono stati tolti così. Prima di cancellarne
  uno vale la pena guardare **la PR**, non `git branch --merged`: una PR chiusa con squash lascia
  una punta che non è antenata di `main`, e quel comando la dichiara «non fusa» pur essendoci
  dentro tutto.
- ⚠️ **F7 è stata fusa in squash per sbaglio, e poi rifatta** (4 set 2026). La riga qui sotto c'era
  già e non è bastata: `gh pr merge <n> --squash` è uscito dalle dita di una sessione che aveva
  letto §9 il giorno prima. **Il comando giusto è `gh pr merge <n> --merge`**, e il modo di
  accorgersene in due secondi è `git rev-list --parents -n 1 origin/main`: un merge commit ha
  **due** genitori, uno squash ne ha uno.

  La correzione è stata possibile perché il ramo della PR era ancora sul remoto: merge commit
  ricostruito a mano (`git checkout --detach <main precedente>`, `git merge --no-ff <punta del
  ramo>`), verificato che l'albero fosse **identico** allo squash con `git diff --quiet <squash>
  HEAD` — cambia la forma della storia, mai il contenuto — e poi
  `git push --force-with-lease=main:<squash> origin HEAD:main`. Il `--force-with-lease` con lo SHA
  esplicito è il punto: se `main` si fosse mosso nel frattempo, il push si sarebbe rifiutato invece
  di cancellare il lavoro di qualcun altro. La PR resta segnata «merged» e punta a uno SHA che non
  è più su `main`; è cosmetico, i suoi commit sono tutti dentro il merge commit nuovo.

  Da farsi **subito**, non «ogni tanto»: dopo un force push su `main` la CI va rilanciata, perché
  quella verde era passata sul commit che non c'è più.

- ⚠️ **Il tag di M0 è finito una volta sul commit sbagliato, e la release è stata fermata in
  volo** (4 set 2026). Il rapporto di chiusura di F9 finiva con un blocco `bash` che cominciava con
  `git checkout main && git pull && git tag -a v0.1.0-m0 … && git push origin v0.1.0-m0`, e diceva
  «prima però fondi la PR» **nella prosa sotto**. Il blocco è stato eseguito: ha lasciato il ramo
  della fase, ha taggato la punta di **F8**, ha spinto il tag e ha fatto partire `release.yml` sul
  commit sbagliato. Nessun lavoro perso e **nessuna release pubblicata**: il run è stato annullato
  con circa novanta secondi di margine, e `gh release list` era ancora vuoto.

  La causa non è la distrazione di nessuno: **nell'app desktop ogni blocco marcato `bash` ha un
  bottone «Run»**, quindi un blocco è un'offerta di eseguire, non un esempio. Una precondizione
  scritta accanto non la fa rispettare a nessuno. La regola che ne esce, e vale per ogni consegna
  futura: *ciò che sta in un blocco eseguibile deve essere corretto anche se è l'unica cosa che
  gira*. Se un comando dipende da un passo fatto a mano — un merge, una revisione — o quel passo
  entra nello stesso blocco, oppure il comando si scrive in linea fra backtick, così va copiato
  apposta.

  Il sintomo, per riconoscerlo la prossima volta, è ingannevole: **i file «cambiano da soli» sul
  disco**. Non è un bug dell'editor né una sessione parallela, è `git checkout` che ha portato via
  il ramo della fase. Il modo di accorgersene in due secondi è `git status` seguito da
  `gh pr view <n> --json state`.

  La correzione: annullare il run (`gh run cancel <id>`) **prima** che pubblichi, fondere la PR,
  poi spostare il tag — `git push origin :refs/tags/v0.1.0-m0`, `git tag -d`, ricrearlo sul merge
  commit, rispingerlo. Cancellare un tag già spinto è sicuro **solo** finché non c'è una release
  attaccata: quello è il motivo per cui il run va fermato per primo.
- La strategia di merge era **squash** fino a F4 e dalla PR #8 in poi è il **merge commit**. Il
  criterio non è cambiato, è cambiato cosa lo soddisfa: quello che si vuole e' che `main` si legga
  a granularità di fase, e `git log --first-parent` lo fa — una voce per fase, F5 è `03d7f96` —
  mentre sotto resta il dettaglio, che con lo squash andava perso. Ha contato in due casi concreti:
  la revisione di §8 arrivava con undici commit costruiti apposta perché un `git bisect` potesse
  fermarsi su ciascuno, e il branch di F5 conteneva un merge di `main`, che schiacciato avrebbe
  prodotto un commit unico contenente anche modifiche già presenti su `main`.
- Se si lavora in un worktree sotto `.claude/worktrees/`, `git checkout main` lì dentro fallisce
  perché `main` è già in uso dal checkout principale: è normale, la sessione nuova parte dal
  checkout principale. Un worktree la cui fase è stata fusa non serve più a niente e si toglie con
  `git worktree remove` — **dal checkout principale**, perché un worktree non può togliere se
  stesso. Fatto a fine F6: c'erano tre worktree di fasi già chiuse oltre a quella in corso, e
  tredici rami locali di cui undici già dentro `main`; adesso restano `main` e la sessione viva.
  Il giro è `git worktree list`, poi `git worktree remove <path>` su quelle pulite, poi
  `git branch -D` sui rami che restano liberi. **Si guarda la PR, non `git branch --merged`**,
  perché le fasi fino a F4 furono fuse con squash e le loro punte non sono antenate di `main`:
  `m0/f4-domain-backbone` e `docs/handoff-f5` sembrano «non fuse» e non lo sono.

- ⚠️ **`git worktree remove` su Windows fallisce con «Filename too long» finché c'è
  `web/node_modules`.** Non è un permesso e non è un handle aperto: è `MAX_PATH`. Le cartelle di
  pnpm — `web/node_modules/.pnpm/@pacchetto+nome@versione_hash/node_modules/…` — sfondano i 260
  caratteri; misurato a fine F6 su una worktree sola: **360 percorsi oltre il limite, il più lungo
  295 caratteri**. Il guaio è che il comando fallisce **dopo** aver già cancellato quasi tutto,
  `.git` compreso, quindi git fa il prune della worktree e sul disco resta un mezzo scheletro che
  non è più una worktree e non è nemmeno una cartella vuota. Confonde parecchio.

  Due modi per non prenderla in faccia. Il primo, già fatto qui: `git config core.longpaths true`
  sul repository, che è **locale a `.git/config`** e quindi ogni clone nuovo va rifatto — è il
  motivo per cui sta scritto qui e non solo nella configurazione. Il secondo, se ci si è già dentro:
  svuotare `web/node_modules` **prima**, e l'unico strumento che sui percorsi lunghi funziona
  davvero è `robocopy`, perché `rd /s /q`, `rm -rf` e `Remove-Item -Recurse` si fermano tutti a 260:

  ```
  mkdir %TEMP%\vuota
  robocopy %TEMP%\vuota "<path della worktree>\web\node_modules" /MIR /XJ /R:0 /W:0
  git worktree remove --force "<path della worktree>"
  ```

  `/XJ` esclude le junction, così robocopy toglie il link e non insegue il contenuto dello store
  condiviso di pnpm. Verificato con un canarino su `web/node_modules` del checkout principale prima
  e dopo: intatto.

  Alla fine può comunque restare **la sola cartella radice, vuota, «busy»**, se una sessione ci ha
  ancora dentro la propria directory di lavoro. Quella è innocua — git non la considera più una
  worktree — e sparisce da sé quando quella sessione si chiude.
- `artifacts/` è gitignorata, quindi `artifacts/openapi/IvaoHub.Web.json` **non** è nel repository:
  lo riscrive `dotnet build`. Quello che è committato è il file che ne deriva,
  `web/src/shared/api/schema.d.ts`, marcato `linguist-generated` in `.gitattributes` e ignorato da
  Prettier e da ESLint come `routeTree.gen.ts`.

---

## 10. Cosa manca per M1

M0 ha costruito i meccanismi; M1 è la milestone in cui si vede che erano quelli giusti, perché il
sito pubblico dovrebbe essere **configurazione molto più che codice**. Se non lo è, il posto dove
scoprirlo è qui.

**Fatto il 5 set 2026**: M1 ha ricevuto il suo documento di design, `03-design-m1.md` v1.0 (piano
§13). Il perimetro qui sotto è stato il suo indice di partenza; da adesso in poi, **su M1 vince il
design**, e questa sezione resta come il racconto di che cosa M0 ha lasciato aperto e perché.

### Il perimetro che il piano assegna a M1

| Cosa | Perché dovrebbe costare poco | Dove sta scritto |
|---|---|---|
| **Sito pubblico**: navigazione, home, pagine di sistema seedate | Il renderer, l'editor, i template e la pubblicazione esistono; mancano le pagine e il menu | piano §9.1, §9.3 |
| **News e documenti** | Sono `kind` di `cms_contents`, cioè **due righe di configurazione** e non due tabelle. Se richiedono più di questo, la §9.3 non ha retto | piano §9.3, design §5 |
| **Calendario con UI** | `cms_calendar_entries` esiste già ed è riempito dalle proiezioni; `CalendarEntry` è già `IAuditable`, `IOwnedByDepartment`, `IVisible` e `[PermissionArea("Calendar")]` | HANDOFF §8.6 |
| **Schermata di ricerca (⌘K)** | `GET /api/search` esiste e legge il FULLTEXT dietro il query filter. Restano due domande che M0 ha lasciato aperte: l'ordinamento per rilevanza sopra la paginazione, e l'evidenziazione | §7, `SearchEndpoints.cs` |
| **Media library, contatti, staff directory, live status** | Il roster è «chi ha fatto login almeno una volta» (piano §16.D punto 13). `ivao_is_staff` e `ivao_is_supervisor` sono già registrati apposta e non decidono niente | piano §9.4, §9.5 |
| **Servizio notifiche** e il namespace `mail` nei file di lingua | Il namespace nascerà da sé: `pnpm i18n:check` legge tutti quelli che trova | §7 |
| **Deploy su staging Plesk** e il foglio `LEGGIMI` | Escluso da M0 per decisione (2 set 2026), in attesa delle risposte A9 | piano §13, §15.2c |
| **Set di blocchi completo ed editor rifinito** (dnd-kit, anteprima multi-device, «allinea al template») | Il registry dei blocchi e il generatore di form sono estendibili; le convenzioni dei blocchi si decidono con il set davanti | design §0.2, piano §16.C |

### Il primo lavoro di M1 è il set dei blocchi, e il catalogo esiste già

Domanda arrivata il 5 set 2026, e vale la pena che la risposta non si ricostruisca da capo: **i
blocchi di un vero page builder — tabelle, card con link, gallery, accordion, tabs, hero, stats —
non mancano, sono rimandati**, e il piano li ha già catalogati.

- **Il catalogo sta in `00-piano §9.3`** (l'analisi di `va.ivao.aero`, il backend del template HQ):
  24 blocchi in cinque gruppi — Content (Text, Hero, Image, Video, Embed), Layout (Card Grid, Icon
  Grid, Columns, Gallery, Logo Grid, Tabs), Data (Stats, Network Stats, Virtual Airlines, Calendar,
  Table, Progress/Timeline), Interactive (Accordion/FAQ, Testimonial, CTA, Alert/Notice, Button
  Group), Structure (Spacer, Divider). M0 ne ha **cinque**, e il design §5.4 dice perché: bastano a
  dimostrare live/frozen e le tre forme di sezione.
- ⚠️ **`Columns` non deve diventare un blocco.** In §9.3 il livello *Row* del Page Builder HQ è già
  diventato una **proprietà della sezione** (`layout`: `stacked`, `1/2+1/2`, `1/3+2/3`, `3×1/3`…), e
  l'envelope lo valida: F7 controlla che il `column` di un blocco stia dentro le colonne che il
  layout della sua sezione ha. Aggiungerlo come blocco sarebbe un secondo modo di fare la stessa
  cosa (CLAUDE.md §2). È l'errore più facile da fare copiando la palette di HQ voce per voce.
- **L'elenco chiuso della ui-kit e il registry dei blocchi sono due cose diverse.** I quindici
  componenti sono pezzi React riusati fra schermate; i blocchi sono un registry a parte, e un blocco
  `Table` non aggiunge di per sé un componente all'elenco. La ui-kit **monta tutto ciò che il
  registry dichiara**, quindi un blocco nuovo compare lì da solo: nessuno deve ricordarsi di
  aggiungerlo.
- **Le icone sono già decise e non vanno ridiscusse**: `lucide-react` (piano §16.C, design §7.1,
  `UI-GUIDELINES.md` §2), e **ogni blocco ne dichiara una** — il tipo lo impone
  (`shared/modules.ts:59`). `web/src/shared/icons/` non esiste ancora perché in nove fasi nessuna
  icona è mai mancata dal set; nascerà la prima volta che serve.

Aggiungere un blocco non è spuntare una lista: è uno schema zod, un componente, una registrazione,
le chiavi i18n di etichetta e campi, e per quelli **Data** un provider lato server. Piano §16.C dice
che le convenzioni dei blocchi si decidono **con il set davanti**: è esattamente il lavoro del
documento di design di M1.

### Debiti che M1 eredita, in ordine di quanto costano se ignorati

**Dove sono finiti** (design M1, 5 set 2026): il n.1 è la **prima fase** di M1 (§11.1); il n.2 (le
differenze rispetto al template) è §9.1; il n.3 (`seo`) è §9.2, che ne decide la forma; il n.4
(`expiresAt`) si chiude di rimbalzo con le estensioni del generatore di form (§1.6); il n.10 (le tre
domande della ricerca) è §7. Il n.6 (`firStaffScope`) resta aperto e passa a M2. Gli altri restano
com'erano, ed è una scelta scritta.

1. ~~**Playwright, `pnpm e2e`.**~~ **Chiuso il 4 set 2026, a forza** (§11): esiste `pnpm e2e`, e al
   5 set 2026 sono **dieci** test su Chromium contro il bundle di produzione, **bloccanti in CI** —
   quattro in `smoke.spec.ts` e sei in `back-office.spec.ts`, cresciuti a ogni difetto trovato
   guardando (§11, §12, §13), e gli ultimi due misurano **geometria** perché il testo era già
   corretto. Quello che **resta
   scoperto** è la metà che il design §8 immaginava e che questa suite non fa: il giro con l'**API
   vera** e una `/{slug}` pubblicata da un seed. Oggi `/api/me` arriva da `e2e/fixtures.ts` e ogni
   altra chiamata `/api` fallisce apposta. Quindi «lo staff apre l'editor, aggiunge un blocco,
   pubblica» **non è ancora stato eseguito in un browser**, ed è questo il debito che M1 eredita:
   un servizio MariaDB in CI, l'API avviata e attesa su `/health`, e la SPA servita davanti.
2. **Un cambio di template non si propaga, e l'editor non lo mostra.** Il design §7.7 lo mette
   esplicitamente in M1 («Differenze rispetto al template»). La regola resta quella di M0 — un
   template non riscrive mai una pagina da solo — ma l'editor deve **dire** che una sezione nuova
   esiste.
3. **`seo` non ha un campo nell'editor.** È un `Localized<JsonNode>` di cui il design non dice la
   forma, e M1 è l'unica milestone che ha una ragione per deciderla. Servirà un tipo nuovo al
   generatore di form — «oggetto tradotto» — che è un'estensione, non un form scritto a mano.
4. **`expiresAt` di un grant è una casella di testo.** Il generatore non ha un tipo «data»; il
   giorno che serve davvero, si estende `shared/forms/schema.ts` (che lancia apposta invece di
   saltare un campo che non sa disegnare).
5. **Il primo permesso di modulo vero, e il primo `DbContext` di modulo.** `AtcModule` non dichiara
   permessi e non ha un contesto: entrambi i rami sono scritti, testati con un modulo finto, e non
   ancora esercitati da un modulo reale. Il primo vero è M2 (Events), ma se un modulo di M1 li tocca
   è lì che si scopre se `AddModuleDbContext<T>` regge.
6. **Le posizioni FIR non danno nessun permesso** (§6 punto 1). È la lettura più restrittiva del
   design, scelta in F2 apposta perché una correzione possa solo allargare. `firStaffScope` esiste
   in `division.json` e in M1 va deciso cosa significa davvero.
7. **`LocalizedExtensions` vive in `src/` e non ha chiamanti di produzione** (nota di revisione di
   F9). `.L(italiano, inglese)` cabla `"it"` e `"en"` ed è usato solo dai test; `ToLocalized<T>` non
   è usato da nessuno. Va spostato nei progetti di test al primo giro che li tocca comunque.
8. **`HubUser` è `[Audited]`, quindi ogni login lascia una riga di audit.** In M0 è il prezzo
   corretto (l'audit dei superadmin senza che un servizio se lo scriva da sé) e la riga pesa poco
   perché contiene solo le colonne cambiate. Se in M1 la tabella dà fastidio, si restringe lì.
9. **La cache della manutenzione è di cinque secondi e il processo è uno solo.** Con Passenger
   oggi va bene; il giorno che i processi sono due, la risposta è un invalidamento condiviso, non
   una cache più corta.
10. **La ricerca non ordina per rilevanza in modo esplicito e non evidenzia niente**, e InnoDB
    ignora le parole più corte di tre lettere. Nessuna delle tre è un bug: sono decisioni che la
    schermata di ricerca di M1 deve prendere.

### Quello che M1 non deve rimettere in discussione

Le regole di §3 e le scelte di §4 di questo documento sono la spina dorsale, non un'opinione di M0:
un campo tradotto è una colonna JSON, un CRUD è `MapCrud`, una schermata di back-office è una
configurazione, l'autorizzazione è un handler solo, l'audit e le proiezioni le scrive l'interceptor,
e l'identità si legge da `ICurrentUser`. Se M1 trova un caso che un meccanismo non copre, la regola
(b) di CLAUDE.md §5 dice di **estendere il meccanismo**; la (c) dice di fermarsi e scrivere una
nota. Nessuna delle due dice di aggirarlo.

---

## 11. L'applicazione non si apriva, e il tag di M0 lo diceva (4 set 2026)

Va letto per intero da chi apre M1, perché è il difetto più istruttivo che questo repository abbia
prodotto finora — non per cosa era, che è banale, ma per **quanto è stato invisibile**.

### Che cosa succedeva

Subito dopo aver spinto `v0.1.0-m0`, la prima apertura di <http://localhost:5173> ha dato una
pagina rossa: `` `Tooltip` must be used within `TooltipProvider` ``.

`DarkModeToggle` di Atmosphere **si avvolge da sé in un `Tooltip` di Radix** (letto nel bundle,
`dist/atmosphere-react.js:11801`); un `Tooltip` senza il suo provider **lancia** invece di
degradare; `main.tsx` non montava `TooltipProvider`. E `DarkModeToggle` sta in `Chrome.tsx`, che è
il frame di **tutti e tre i layout** — quindi non era rotta la home: era rotta **ogni schermata
dietro un layout**. Il tag che dichiarava M0 finita puntava a un'applicazione che non si apriva.

### Perché nessuno dei 427 test l'ha visto

Questa è la parte che conta. I test erano verdi **legittimamente**: il difetto non era in un
componente, era **nell'albero**.

- `harness.tsx` monta `I18nextProvider` e `QueryClientProvider` attorno a **un componente per
  volta**, ed è giusto così: dare a un pezzo il minimo che gli serve è il suo lavoro.
- **Nessun test montava `Chrome`**, e nessuno montava affatto i provider dell'applicazione. La
  prova: la prima volta che abbiamo montato `ThemeProvider` in un test, ha chiesto un
  `window.matchMedia` che jsdom non ha — un buco che nessuno aveva mai dovuto stubare in nove fasi.
- Quindi la composizione non era provata da niente, **mentre l'intero progetto è costruito
  sull'idea che le schermate siano composizione**. Il punto cieco stava esattamente dove il sistema
  fa la sua scommessa più grossa.

### Come è stato chiuso, e la parte da non disfare

Tre reti, in ordine di quanto sono difficili da aggirare:

1. **`web/src/app/Providers.tsx`** — l'albero dei provider è ora `HubProviders`, un componente, e
   lo montano **sia `main.tsx` sia il test**. Un provider si aggiunge lì e in nessun altro posto.

   ⚠️ **La prima versione del test elencava i provider per conto suo, ed era inutile**: sarebbe
   rimasta verde con l'applicazione rotta. È esattamente la copia che diverge in silenzio contro
   cui §3 mette in guardia, e ci siamo cascati dentro il commit che serviva a non cascarci. Se
   qualcuno «semplifica» `Chrome.test.tsx` inlineando i provider, la rete torna a essere finta.
2. **`web/src/app/layouts/Chrome.test.tsx`** — monta `Shell` dentro `HubProviders` e dentro un
   router in memoria (i link dell'header sono `Link` di TanStack). Il router va **atteso**:
   `findBy*`, non `getBy*`, perché risolve la prima rotta dopo il primo paint.
3. **`web/e2e/` + `pnpm e2e`, bloccante in CI** — tre smoke su Chromium contro il **bundle di
   produzione** (`vite preview`), non contro il dev server. Nota di decisione dedicata.

**Entrambe le reti sono state verificate togliendo il provider**: il Vitest fallisce, e due dei tre
smoke falliscono. Un test di regressione che passa in entrambi i casi non è un test, ed è un
controllo che costa trenta secondi e va rifatto ogni volta che se ne scrive uno.

### Due cose trovate di rimbalzo

- **`DarkModeToggle` riceveva `aria-label` ma non `title`**, e il tooltip visibile restava
  l'inglese di Atmosphere. È la **terza** stringa non tradotta in due giorni che sopravvive perché
  «non la si guarda mai» — dopo l'`aria-label="breadcrumb"` e il messaggio di avvio della revisione
  §16.E. La famiglia è una sola: *stringhe che appaiono solo al passaggio del mouse, a uno screen
  reader, o in un fallimento*. Se M1 vuole una rete per questa famiglia, il posto è lo smoke.
- **I tipi di Atmosphere pretendono `children` su `DarkModeToggle` e il runtime li scarta**
  (`children` è assegnato **dopo** lo spread delle props). Il nostro `<Moon>` era markup morto da
  F6. Ora si passa `{null}`, che è l'unico modo onesto di soddisfare un tipo sbagliato.

### La regola che ne esce

Un test che monta un pezzo prova il pezzo. **Se il prodotto è fatto di composizione, qualcosa deve
montare la composizione**, e in questo repository sono `Chrome.test.tsx` e `pnpm e2e`. Non vanno
indeboliti per far passare una fase.

---

## 12. Nessun form del back-office era raggiungibile (4 set 2026)

Il secondo difetto trovato aprendo l'applicazione a mano, poche ore dopo il primo, e **della stessa
famiglia**: §11 era la composizione dei provider, questo è la composizione delle route.

### Il sintomo, e perché era difficile da leggere

«Vado su links, clicco su nuovo link, non funziona nulla.» L'indirizzo cambiava davvero
(`/staff/ed/links/new`), il server non riceveva nessuna chiamata, la console non mostrava nessuna
eccezione. Tutto sembrava a posto tranne il fatto che non succedeva niente.

`staff.$dept.links.$id.tsx` era un route **figlio** di `staff.$dept.links.tsx`, e in TanStack un
figlio si disegna dentro l'`<Outlet />` del padre. Il componente della lista non ne rendeva nessuno.
Quindi la lista restava sullo schermo e il form non compariva mai — per **tutte e tre** le coppie:
`links`, `content`, `admin/permissions`. Non si poteva creare un link, aprire l'editor di una
pagina, né toccare un grant.

**Il dettaglio che l'ha confermato senza ipotesi** era nell'URL stesso:
`/staff/ed/links/new?page=1&pageSize=25&dir=asc`. Quei parametri sono la paginazione della lista, e
il form se li portava dietro perché ne era figlio ed ereditava il suo `validateSearch`.

### Come si è arrivati alla diagnosi, che è la parte riutilizzabile

Tre ipotesi, tutte plausibili, **tutte sbagliate**, e tutte scartate con un test usa-e-getta invece
che a occhio:

1. «La `stringify` del padre butta via `id` quando si costruisce il link» → `buildLocation` sul
   router vero restituisce `/staff/ed/links/new`. Falso.
2. «`Button asChild` di Atmosphere ingoia il click» → il DOM è un `<a href="/target">` corretto.
   Falso. (Sospetto ragionevole, dopo aver scoperto la mattina che `DarkModeToggle` scarta i
   `children`.)
3. «La `parse` del padre perde `id` al match» → il figlio riceve `{dept: "ED", id: "new"}`. Falso.

Ognuna sarebbe stata una diagnosi convincente da raccontare. **Nessuna era vera**, e la cosa che ha
sbloccato è stata chiedere a Carmine le due informazioni che solo il suo browser aveva: se l'URL
cambiava, e che cosa diceva la console. La risposta ha eliminato metà dello spazio in un colpo. La
lezione: quando tre ipotesi cadono, il problema non è nei pezzi che si stanno guardando — e
l'osservazione di chi ha lo schermo davanti vale più di una quarta ipotesi.

### Che cosa c'è adesso

- La ricetta è **tre route** invece di due (design §7.3, corretto; nota di decisione dedicata):
  layout con il parse del dipartimento, la guardia e l'`Outlet`; `index` con i search params e il
  loader; dettaglio fratello. Guardia scritta una volta per entrambe, e i search params della lista
  non seguono più il form.
- `web/e2e/back-office.spec.ts`: quattro smoke su una **sessione staff finta** — un coordinatore con
  `hasAllDepartments: false` e un solo dipartimento, perché un superadmin non eserciterebbe la
  guardia. Le asserzioni guardano **due metà insieme**: l'indirizzo è cambiato *e* la cosa promessa è
  sullo schermo. Una metà sola è ciò che ha lasciato passare il difetto.
- Verificato togliendo l'`Outlet`: **tre dei quattro falliscono**.

### Un rumore in console che non è un difetto nostro

Nella stessa sessione è comparso `Unknown event handler property `onValueChange`. It will be
ignored.` su ogni pagina. **Non è un nostro bug e il selettore di lingua funziona**: il `Select` di
Atmosphere spande le proprie rest props **due volte** — una su `Select.Root` di Radix, che è quella
che gestisce il cambio, e una sul `div` del viewport, dove React la ignora e si lamenta
(`dist/atmosphere-react.js:15655`). Verificato con uno smoke che cambia lingua e pretende che il
nome della divisione passi da «IVAO Example» a «IVAO Esempio».

Sta scritto qui, e in un commento accanto al test, perché è il tipo di avviso che qualcuno «sistema»
togliendo `onValueChange` — cioè rompendo il selettore per far tacere un rumore di terze parti.

### La regola che ne esce, e che vale più della correzione

**Una ricetta che si copia è un moltiplicatore.** §7.3 documentava la ricetta 2 nella forma
sbagliata ed è stata copiata tre volte, fedelmente, da chi faceva esattamente ciò che il progetto
chiede. Quando una ricetta è giusta fa risparmiare tre volte; quando è sbagliata replica il difetto
tre volte e nessuno lo rimette in discussione, perché copiarla *è* la procedura. Se M1 aggiunge una
ricetta a §7.3, il momento di provarla in un browser è **prima** che diventi il quarto esemplare.

---

## 13. Il back-office era disegnato in una colonna da 255 pixel (4 set 2026)

Terzo difetto della giornata, trovato guardando le schermate invece di leggerle. **Della stessa
famiglia degli altri due** — un contratto di Atmosphere assunto e mai verificato in un browser — ma
con una differenza che conta: gli altri due impedivano a qualcosa di funzionare, questo lasciava
funzionare tutto **nel posto sbagliato**.

### Che cosa succedeva

Ogni schermata di `/staff` era disegnata in una colonna larga **255 pixel**, in alto a sinistra, con
il resto della finestra vuoto; la tabella era tagliata e il bottone «Close sidebar» compariva **due
volte**.

`StaffLayout` componeva così:

```tsx
<SidebarProvider><SidebarContainer>   {/* ← noi */}
  <Sidebar items={…} />               {/* ← che porta con sé un ALTRO provider e un ALTRO container */}
  <main className="w-full flex-1">…</main>
</SidebarContainer></SidebarProvider>
```

Due cose che non sapevamo, e che stanno nel bundle:

1. **`Sidebar` è già completo**: si avvolge da sé in `SidebarProvider` e `SidebarContainer`.
2. **`SidebarContainer` non è un guscio a due colonne: è l'`<aside>`**, con classe `w-72`.

Quindi la sidebar vera **e** il `<main>` finivano dentro un aside da 288 px, impilati. Misurato:
`main` a `x=16, width=255, y=542` in un viewport da 1280. La forma giusta è che la riga la facciamo
noi e la sidebar no:

```tsx
<div className="flex flex-1 items-stretch">
  <Sidebar … />
  <main className="min-w-0 flex-1 px-4 py-8"><Outlet /></main>
</div>
```

Dopo: `main` a `x=288, width=992`.

### Perché nemmeno gli smoke l'hanno visto

Perché **asserivano sul testo, e il testo era giusto**. Tutte le parole erano presenti, nell'ordine
previsto, cliccabili: gli otto smoke passavano su un back-office inutilizzabile. È il limite di un
test che chiede «c'è?» e non «dov'è?».

La rete nuova (`back-office.spec.ts`) misura quindi la **geometria**: `main.x > 200`,
`main.width > 600`, e un solo «Close sidebar». Verificata rimettendo il layout vecchio: fallisce con
`Received: 16`.

### Tre falsi allarmi, evitati controllando

Vale la pena scriverli, perché in un giro visivo la tentazione di riportare tutto ciò che sembra
storto è forte, e tre delle cinque cose che sembravano difetti non lo erano:

- **`grants.options.effect.Allow` a schermo come chiave grezza.** `GrantEffect` è `Grant | Deny`:
  `Allow` l'aveva inventato la mia fixture. Mostrare la chiave per un valore che non esiste è il
  comportamento giusto.
- **La data ripetuta due volte in ogni cella.** Voluto: `DateCell` mostra UTC **e** il fuso della
  divisione, e la fixture aveva `timezone: "UTC"`, quindi le due righe coincidevano.
- **Gli smoke del back-office rossi contro il pacchetto pubblicato** (§«Il tag»): mancava il
  fallback SPA nel server di prova.

Regola pratica per il prossimo giro visivo: **prima di chiamare difetto qualcosa, controllare se è
la fixture.** Costa un grep e ha salvato tre segnalazioni sbagliate su cinque.

### Le due cose viste qui, **corrette il 5 set 2026**

Erano rimaste aperte perché richiedevano una scelta. Entrambe chiuse estendendo un meccanismo, mai
aggirandolo (CLAUDE.md §5, regola (b)).

1. **Un'etichetta faceva due lavori.** `<ns>.fields.<campo>` è insieme l'etichetta del form e
   l'intestazione di colonna di `DataList`, e `grants.fields.expiresAt` portava il formato dentro
   l'etichetta — «Expires (YYYY-MM-DD, empty for never)» — quindi la tabella dei grant aveva
   un'intestazione alta cinque righe che tagliava le colonne a destra.

   **Scelta: separare il suggerimento dall'etichetta, non dare alle colonne una chiave propria.**
   `SchemaForm` disegna ora una frase sotto un campo quando `<ns>.hints.<campo>` esiste nei file di
   lingua — niente flag nello schema, perché un suggerimento è **parole** e le parole stanno in
   `locales/`. L'etichetta torna «Expires» / «Scadenza», e l'intestazione di colonna diventa corta
   **di conseguenza**, senza una seconda chiave da tenere allineata. La strada scartata
   (`<ns>.columns.<campo>` con fallback) avrebbe lasciato in piedi la causa e aggiunto due chiavi per
   lo stesso campo, cioè un posto dove far divergere lista e form.

   ⚠️ La trappola dell'implementazione: i18next restituisce **la chiave** quando manca, quindi senza
   `i18n.exists()` mezza form avrebbe mostrato `test.hints.reason`. C'è un test che lo fissa, ed è
   verificato togliendo la guardia.
2. **Un campo tradotto era largo 400 px** accanto a input larghi 960: **`Tabs` di Atmosphere si
   pinna a `w-[400px]`** (`dist/atmosphere-react.js:18568`), e `LocaleFields` è costruito su quello.
   Risolto con un `className="w-full"`, che si fonde invece di litigare perché quella libreria passa
   la classe da `cn`. **Quarto contratto di Atmosphere in due giorni** che andava misurato e non
   assunto, dopo `DarkModeToggle`, `Select` e `SidebarContainer`.

Entrambe hanno una rete, ed entrambe le reti sono state verificate rompendo la correzione: il test
del suggerimento fallisce senza la guardia, e quello della larghezza esce `Received: 400`. Il secondo
è di nuovo **geometria in un browser**, perché jsdom non fa layout — è la stessa lezione di §13, e
ormai è una categoria: *ciò che si vede e basta si prova solo guardando*.

---

## 14. G0 di M1: il giro contro l'API vera esiste (5 set 2026)

**Il debito n.1 di §10 è chiuso.** «Uno staff apre l'editor, aggiunge un blocco, pubblica» è stato
eseguito in un browser, contro MariaDB vera e l'applicazione pubblicata, e ora è tre test bloccanti
in CI.

### Come si esegue

Serve Docker attivo (`docker compose up -d mariadb`), poi da `web/`:

```bash
pnpm e2e:full
```

Pubblica l'applicazione in `artifacts/e2e-bench/`, la avvia su <http://127.0.0.1:5080>, aspetta
`/health` e gira. Il primo giro pubblica (un paio di minuti); mentre si lavora sulle spec,
`E2E_SKIP_PUBLISH=1 pnpm e2e:full` riusa l'ultima pubblicazione. Il resto è in
`web/e2e/full/README.md`.

### Che cos'è il banco, e i due lucchetti

Il banco è **l'applicazione pubblicata**: una sola origine per API e SPA, con il fallback del
server. Non un server statico davanti — è esattamente il banco che in M0 produsse quattro test rossi
contro un pacchetto sano («Il tag»), e uno dei tre test nuovi controlla proprio quel 200 per dire
subito da che parte sta il problema.

`POST /e2e/signin` firma un cookie applicativo vero per uno staff inventato. Esiste **solo** se
l'ambiente è `E2E` **e** `E2E:Enabled` è vero; il flag altrove **ferma l'applicazione**
(`HubConfiguration.RequireE2EEnvironment`, test in `E2EBenchTests`). Nota di decisione:
`decisions/2026-09-05-ambiente-e2e.md`. Di rimbalzo `FixtureIvaoApiClient` accetta anche `E2E`: il
banco gira senza credenziali IVAO e il sync `ref_` è atteso all'avvio.

⚠️ **Entrambe le asserzioni che contano sono state verificate rompendole** (§A.10 del piano M1). E la
prima versione del test «una bozza non è visibile» **è passata con la bozza pubblicata apposta**:
asseriva l'assenza di un'intestazione che su una pagina pubblica non c'è in nessun caso. Ora asserisce
il «questa pagina non esiste» e l'assenza del testo del template, e fallisce come deve. È la stessa
lezione di §11: una rete che non si prova rompendola non è una rete.

### Due cose viste facendo il giro, nessuna corretta qui

1. ⚠️ **I template di sistema li vedeva solo il dipartimento Web.** `ContentTemplateSeeder` li semina
   con `OwnerDepartment = WD` e `Content.View` è di dipartimento: per un coordinatore ED,
   `filter[isTemplate]=true` rispondeva **zero righe** e «Nuovo da template» non compariva affatto.
   Verificato nel browser con una sessione `IT-EC` vera. **Deciso lo stesso giorno da Carmine**: il
   template resta di un dipartimento — ognuno si fa i suoi — ma **lo legge tutto lo staff**, e chi
   vuole divergere ne prende una copia che diventa sua. Nota:
   `decisions/2026-09-05-template-di-sistema-e-dipartimenti.md`; si implementa nel **primo task di
   G5**, e senza di essa §9.1 del design (le differenze rispetto al template) non avrebbe il dato da
   mostrare a nessuno fuori da WD. Nel frattempo il banco firma come coordinatore **Web** (`IT-WM`),
   che è chi costruisce il sito — ma quel ruolo raggiunge ogni dipartimento, quindi **il giro non
   esercita la guardia di dipartimento**: quella resta di `back-office.spec.ts`.
2. ⚠️ **Pubblicare non dice niente a schermo.** Si clicca «Pubblica», la chiamata parte, la riga
   cambia versione e sullo schermo non cambia nulla di visibile. Non è un difetto di correttezza — la
   cache viene aggiornata e il form si rimonta sulla versione nuova — ma è la cosa che, guardando, si
   nota per prima. Da raccogliere in G11 (rifiniture dell'editor) o nel giro visivo di G12.
   Di rimbalzo: modificare **nello stesso millisecondo** in cui la pubblicazione risponde salva contro
   la versione precedente e prende 409, giustamente. Una persona non digita così in fretta; il test sì,
   e infatti ricarica la pagina come farebbe chi torna a cambiare qualcosa.

### Una cosa che non c'era in nessun documento: la dashboard di dipartimento

Chiesta da Carmine il 5 set 2026 aprendo M1. I documenti conoscevano solo la dashboard **personale**
`/me`, che compone i widget del registry; `/staff/{dept}` non esiste nemmeno come schermata, e il
piano §8.2 chiamava `/staff/{dept}/**` «spazio del dipartimento» senza dire che cosa si vedesse
arrivandoci. È il caso **(c)** di `CLAUDE.md` §5, quindi è stata scritta prima di essere codificata:
`decisions/2026-09-05-dashboard-di-dipartimento.md` misura il bivio (una riga di `cms_contents` per
dipartimento contro una disposizione di widget, che vorrebbe un secondo editor) e raccomanda la
prima. Entra in **G8**; la forma va confermata prima di aprire la fase. Piano v0.38, design M1 v1.2.

### Che cosa resta di G0

Niente. La fase è chiusa quando la PR è verde: tre test nuovi in CI con il servizio MariaDB,
`E2EBenchTests` (2), e le suite di M0 tutte ancora verdi.

