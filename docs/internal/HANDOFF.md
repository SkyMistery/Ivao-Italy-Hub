# HANDOFF — stato di M0

> Documento **interno** (italiano). Si aggiorna alla fine di ogni fase (piano di implementazione §A.6).
> Fonte di verità: `00-piano-di-progettazione.md`; perimetro e firme: `01-design-m0.md`; ordine: `02-piano-implementazione-m0.md`.

**Ultimo aggiornamento:** 2 settembre 2026 — fine **F1** (configurazione, avvio, DB del nucleo).
**Repository:** https://github.com/SkyMistery/Ivao-Italy-Hub (pubblico).
**Branch corrente:** `m0/f1-config-db`. **Prossima fase:** F2 — auth BFF, utenti, ruoli, superadmin, `/api/me`.

Fasi chiuse: **F0** (bootstrap, PR #1, mergiata in `main`), **F1**.

---

## 1. Come si avvia (locale)

```bash
cp config/ivao-oauth.example.json config/ivao-oauth.json   # e compilarlo; mai committato
docker compose up -d                                        # MariaDB 11.4.10 + Mailpit
dotnet run --project src/IvaoHub.Web                        # API su :5000, migra il DB da sola
cd web && pnpm install && pnpm dev                          # SPA su :5173 (proxy /api, /auth, /health)
```

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

## 2. Cosa c'è dopo F1

- **Configurazione**: `config/division.json` (IT, versionato: è comportamento, non un segreto) +
  `division.example.json` commentato + `ivao-oauth.example.json`. `config/ivao-oauth.json` è gitignored.
  Precedenza: `appsettings` < `secrets/*.json` < `config/ivao-oauth.json` < variabili d'ambiente.
- **Opzioni validate all'avvio**: `DivisionOptions` (codice 2–3 maiuscole, `defaultLocale` dentro `locales`,
  `name` per ogni lingua, timezone reale) e `IvaoOAuthOptions` (campi obbligatori, `RedirectUri` che finisce
  con `/auth/callback`, `LoginUrl` con `/auth/login`, stesso host). Senza configurazione valida **l'app non
  parte** e l'eccezione elenca i campi sbagliati; il secret non compare mai.
- **Localizzazione**: `Localized<T>` (record immutabile, chiavi ordinate, `Resolve`, `HasAll`,
  `MissingLocales`) + converter/comparer EF + `LocalizedColumnConvention` (`Title` → `title_i18n`).
  Registrati **una volta** in `HubDbContext.ConfigureConventions`.
- **`HubDbContext`**: Pomelo su `MariaDbServerVersion(11.4.10)` (mai `AutoDetect`), snake_case,
  `utf8mb4` / `utf8mb4_unicode_ci`, tabelle con prefisso esplicito (`hub_`, `ref_`, `cms_`).
  `AddHubDbContext` e `AddModuleDbContext<T>` (già pronto, storia migrazioni `__EFMigrationsHistory_<modulo>`).
- **16 tabelle** create dalla migrazione `Initial`, con `row_version timestamp(6)` gestito dal server e
  `security_stamp` su `hub_users`. `cms_search_index` ha una riga per lingua più l'indice FULLTEXT `(title, text)`.
- **Avvio**: `HubPaths` trova `config/`, `locales/`, `secrets/`, `hub-keys/`, `logs/`, `diagnostics/`;
  Serilog (console + `logs/hub-.log` giornaliero) con `X-Correlation-Id` per richiesta; Data Protection
  persistente su `hub-keys/`; `ForwardedHeaders` e `AllowedHosts` obbligatori solo in Production.
  Prima del traffico: validazione opzioni → `Migrate()` → `diagnostics/startup.txt`.
- **Endpoint**: `/health` con ping DB reale, `/api/version` (`version`, `commit`, `builtAt`, `dotnet`),
  `Cache-Control: no-store` su `/api/*` e `/health`.
- **Test**: 33 verdi. `DivisionOptionsValidationTests`, `IvaoOAuthOptionsValidationTests` (unit);
  `MigrationsApplyOnRealMariaDbTests` (catena da zero, idempotenza, utf8mb4, FULLTEXT e unicità per lingua) e
  `HealthAndVersionEndpointsTests` (health, version, diagnostica scritta, esclusioni del fallback SPA) su un
  container `mariadb:11.4.10` condiviso.

## 3. Regole già attive (non aggirarle nelle fasi successive)

- ESLint blocca `fetch` fuori da `shared/api`, `<svg>` fuori da `shared/icons` e `blocks`, import dal nucleo
  verso `modules/` e import tra due moduli (zone derivate dalle cartelle presenti).
- Un campo tradotto è **solo** una colonna JSON `Localized<T>`: nessuna tabella `*_translations`.
- Gli enum si salvano come stringa, mai come numero, e la conversione è registrata una volta sola.
- La concorrenza ottimistica passa da `HasRowVersion(...)`, un solo helper.
- Le migrazioni sono **solo additive**; `Initial` non si tocca più.

## 4. Scelte fatte finora che vale la pena conoscere

| Scelta | Perché |
|---|---|
| `global.json` contiene anche `"test": { "runner": "Microsoft.Testing.Platform" }` | xUnit v3 gira su MTP e l'SDK 10 rifiuta il percorso VSTest. Quindi niente `Microsoft.NET.Test.Sdk` e il comando è `dotnet test --solution IvaoHub.sln`. |
| `IvaoHub.Core` ha `FrameworkReference Microsoft.AspNetCore.App` | Il design mette dentro il nucleo `Auth/` (OIDC BFF) e `MapCrud` (endpoint): senza il framework servirebbero una decina di package ASP.NET sciolti. |
| `config/ivao-oauth.json` caricato con `optional: true` invece che `false` | Il piano §6.1 vuole che le variabili `Ivao__*` bastino da sole su Plesk. La garanzia «l'app non parte se manca» la dà il **validatore**, che fallisce elencando i campi: effetto identico, e la CI non ha bisogno del file. |
| `HubPaths` risolve la radice risalendo fino a `config/division.json` | In produzione `config/` sta accanto all'app (= content root); in sviluppo sta alla radice del repo mentre il content root è `src/IvaoHub.Web`. `IVAOHUB_ROOT` forza il valore. |
| FULLTEXT creato dal modello (`.IsFullText()` di Pomelo) e non con `migrationBuilder.Sql` | Il meccanismo esiste già nel provider: scrivere SQL a mano sarebbe una copia locale di qualcosa che il modello sa fare. Il DDL generato è verificato da un test. |
| `row_version` è `timestamp(6) DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6)` | MariaDB non ha `rowversion`: è il server a incrementarlo. Un solo helper, `HasRowVersion`. |
| `UserGrant` non ha `granted_at` e `granted_by` separati | Sono `created_at` e `created_by` di `IAuditable`: due colonne che dicono la stessa cosa sono esattamente ciò che il piano vieta. |
| `Department`, `Visibility`, `PublishStatus`, `StaffLevel` definiti già in F1 | Le colonne della migrazione hanno bisogno del vocabolario. Le **interfacce** trasversali restano a F4, come dice il piano. |
| `.editorconfig` esenta `**/Migrations/*.cs` dalle regole di stile | Sono file generati da EF: si rivedono, non si formattano. Vale anche per i contesti dei moduli. |
| `.gitignore`: le cartelle di runtime sono ancorate alla radice (`/data/`, `/logs/`, …) | Su Windows git confronta i pattern in modo case-insensitive: un `data/` non ancorato stava ignorando `src/IvaoHub.Core/Data/`, migrazioni comprese. |
| `@vitejs/plugin-react` 5.2.0, ESLint 10, `.sln` classico, `GenerateDocumentationFile=true` | Vedi F0: vincoli di compatibilità con Vite 7 e con `TreatWarningsAsErrors`. |

## 5. Debiti e cose da fare presto

- **`locales/` e `config/*.example.json` non finiscono nel pacchetto pubblicato**: oggi le lingue arrivano al
  browser da `wwwroot/locales` (emesse dal build della SPA), ma il backend le leggerà da `<root>/locales` con
  `LocaleCatalog` in **F4**. La copia nel publish si aggiunge quando serve, non prima.
- `config/ivao-oauth.json` locale contiene **segnaposto**: le credenziali di test vere servono in **F2**.
- `IvaoHub.Core/Auth/` per ora ha solo le opzioni OAuth: `StaffRoleMap`, `ICurrentUser`, `UserSyncService`
  e il BFF sono F2.
- `DivisionOptionsValidator` accetta un elenco di chiavi modulo note ma **nessuno gliene passa**: il warning
  sulle chiavi ignote si accende in F8 con il `ModuleRegistry`.
- Il test di architettura «nessun modulo referenzia un altro modulo, `Core` non referenzia `Web`» è **F4**.
- `docs/UI-GUIDELINES.md` è **F6**; `docs/FORKING.md` va completato in **F8/F9**.
- Il chunk JS supera i 500 kB: lo split per route arriva con i layout di F6.
- Licenza ancora «TBD» (piano §15.5).
- ~~Nomi italiani delle cartelle di runtime~~ **deciso con Carmine il 2 set 2026**: valgono le nostre regole,
  non quelle di vIPI (che resta un riferimento su *come* funziona il deploy, non sui nomi). `segreti/` è
  diventata `secrets/`, `diagnostica/` è diventata `diagnostics/` e `avvio.txt` è diventato `startup.txt`;
  piano 00 portato a v0.20 con la riga di changelog, design `01` §2.3–2.4, piano `02` F1 e `CLAUDE.md` §6
  allineati. Nessuna path del prodotto contiene più una parola italiana.
