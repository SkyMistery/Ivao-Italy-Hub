# HANDOFF — stato di M0

> Documento **interno** (italiano). Si aggiorna alla fine di ogni fase (piano di implementazione §A.6).
> Fonte di verità: `00-piano-di-progettazione.md`; perimetro e firme: `01-design-m0.md`; ordine: `02-piano-implementazione-m0.md`.

**Ultimo aggiornamento:** 3 settembre 2026 — fine **F3** (`IvaoApiClient` e dati `ref_`).
**Repository:** https://github.com/SkyMistery/Ivao-Italy-Hub (pubblico).
**Piano:** v0.22. **Test:** 182 verdi (152 unit + 30 integrazione).

| Fase | Stato |
|---|---|
| F0 bootstrap | mergiata (PR #1) |
| F1 configurazione, avvio, DB | mergiata (PR #2) |
| F2 auth BFF, ruoli, permessi, `/api/me` | mergiata (PR #3 e #4) |
| **F3 `IvaoApiClient` e dati `ref_`** | **PR #5 aperta, CI verde, da mergiare** |
| F4 spina dorsale del dominio | **prossima** |

> ⚠️ **Prima cosa da fare in una sessione nuova**: mergiare la
> [PR #5](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/5) (squash, come le altre), poi
> `git checkout main && git pull`. Il branch `m0/f3-ivao-ref` è ancora quello di lavoro.

**Come si apre F4**: prompt di §C del piano di implementazione con `<N>` → `4`. Perimetro in §D:
`Localized<T>` lato API, interfacce trasversali (`IOwnedByDepartment`, `IVisible`, `IPublishable`,
`IAuditable`), l'**unico** `HubSaveChangesInterceptor` (audit, guardia di scrittura per dipartimento,
`hub_audit_log`, proiezioni in due tempi), il global query filter di visibilità,
`BlockDocumentWalker`, `IProjectable` + `ProjectionWriter`, `HubPolicyProvider` +
`DepartmentAuthorizationHandler` (l'unico), e i test di integrazione della spina dorsale.
F4 non fa endpoint né frontend.

Serve solo Docker attivo: le credenziali IVAO ci sono e funzionano, ma F4 non le usa.

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

## 2. Cosa c'è dopo F3

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
- **`locales/` e `config/*.example.json` non finiscono nel pacchetto pubblicato**: servirà in **F4** con
  `LocaleCatalog`.
- L'audit dei superadmin lo scrive il servizio a mano: in **F4** lo sostituisce l'interceptor con `[Audited]`
  e la scrittura diretta si toglie.
- `DivisionOptionsValidator` accetta le chiavi modulo note ma nessuno gliene passa: si accende in **F8**.
- `shared/api/bootstrap.ts` e il tipo `ApiPaths` in `client.ts` sono scritti a mano: **F5** li sostituisce con
  `schema.d.ts` generato dall'OpenAPI.
- Il test di architettura «nessun modulo referenzia un altro modulo» è **F4**; `docs/UI-GUIDELINES.md` è **F6**.
- Il chunk JS supera i 500 kB: lo split per route arriva con i layout di F6.
- Licenza ancora «TBD» (piano §15.5).
- `Ivao:ApiScopes` e' vuoto: **misurato**, i due endpoint di riferimento non chiedono scope. Se in
  M2+ servira' `tracker` (chi e' online), si aggiunge li' senza toccare codice.
- Le fixture IVAO coprono 3 centri e 3 aeroporti: bastano a provare upsert e riconoscimento FIR, non
  sono un campione realistico dell'Italia (che ne ha 7 e 221).
