# HANDOFF — stato di M0

> Documento **interno** (italiano). Si aggiorna alla fine di ogni fase (piano di implementazione §A.6).
> Fonte di verità: `00-piano-di-progettazione.md`; perimetro e firme: `01-design-m0.md`; ordine: `02-piano-implementazione-m0.md`.

**Ultimo aggiornamento:** 9 settembre 2026 — **M0 è chiusa, M1 è costruita e in collaudo**: design
(`03-design-m1.md`), piano (`04-piano-implementazione-m1.md`), **G0** il giro contro l'API vera in un
browser (**§14**), **G1** la media library (**§15**), **G2** le cinque estensioni del generatore di
form (**§16**), **G3** i sedici blocchi Content, Layout, Interactive e Structure (**§17**), che ha
chiuso **§16.C del piano**, **G4** i sei blocchi Data con i loro provider (**§18**), che porta il
registry a **27**, **G5** news, documenti e categorie come due `kind` di una tabella sola (**§19**),
**G6** il calendario con la sua UI (**§20**), **G7** i contatti con il servizio notifiche
(**§21**), che aggiunge alla spina dorsale il terzo della famiglia, `ISubmittedByMembers`, e l'unico
indirizzo che l'hub conserva, e **G8** il sito pubblico (**§22**): il menu è una tabella, le cinque
pagine di sistema sono seminate, ogni dipartimento nasce con la propria dashboard a blocchi, e un
grant fa finalmente raggiungere il dipartimento su cui è dato, e **G9** il live status (**§23**) —
la fase più corta di M1, perché ha aperto e ha trovato la staff directory già in piedi, costruita da
G4 e da G8 senza che nessuna delle due la chiamasse così, e **G10** la ricerca (**§24**), che chiude
il **debito n.10** di §10: le tre domande lasciate aperte da M0 — rilevanza, evidenziazione, parole
corte — hanno una risposta scritta e provata, e **G11** le rifiniture dell'editor (**§25**), che
chiude il **debito n.2**: l'editor dice quando il template si è mosso, applica una differenza alla
volta, e si trascina con dnd-kit senza perdere le frecce, che sono l'unica strada da tastiera, e
**G11a** (**§26**), la mezza fase che ha chiuso il debito di G11: i quattro campi di un template si
scrivono da una schermata, e il generatore di form ha imparato il sesto tipo di campo — uno più di
quanti design §12 ne prevedesse, ed è un numero che G12 riporta invece di nasconderlo. Il prossimo
e **G12** (**§27**), la fase che verifica invece di costruire: due pagine ricopiate a mano
dall'editor, il giro visivo, la demo, la revisione §16.E e il conto contro la previsione. Ha trovato
più difetti di qualunque altra fase, fra cui **ogni form del back-office si poteva salvare una volta
sola per caricamento di pagina**. ⚠️ Poi Carmine ha **eseguito la demo** e ha trovato altri quattro
difetti e dodici richieste (**§28**): **tutti e quattro i difetti sono corretti e tutte e dodici le
richieste fatte**; una seconda esecuzione ne ha aggiunte quattro, fatte anche quelle — l'ultima è che
l'indirizzo di una voce di menu è un **insieme chiuso**, perché ogni indirizzo che esce dal sito deve
vivere in una tabella sola. **Il tag `v0.2.0-m1` aspetta** che Carmine riesegua la scheda da capo e
mergi la PR #57 (§29). M0 resta chiusa e non
c'è niente di suo da finire: F9 aveva verificato invece di costruire (la checklist §16.E letta su
tutto il codice, la demo a mano, i passi reali di un fork, il tag `v0.1.0-m0`), e le fondamenta con
la spina dorsale generica sono dimostrate end-to-end su `links` e su una pagina nata da un template,
che è esattamente ciò che §16.15 del piano chiedeva.

**Repository:** https://github.com/SkyMistery/Ivao-Italy-Hub (pubblico). `main` è avanti al tag
`v0.1.0-m0` di tutto M1: quanto esattamente lo dice
`git log v0.1.0-m0..main --merges --oneline`, che è sempre giusto — un numero scritto qui sarebbe
sbagliato dal merge dopo, ed è già successo due volte.
**Design M0:** v2.1. **Piano di implementazione M0:** v1.6.
**Piano:** v0.50. **Design M1:** v1.15 (`03-design-m1.md`). **Piano di implementazione M1:** v2.11
(`04-piano-implementazione-m1.md`, fasi G0–G13): **da G0 a G12 sono chiuse** (§14–§27); **G13 è
aperta** (§28) ma non ha più lavoro suo — quattro difetti e sedici richieste, tutti chiusi — e il tag
viene dopo che Carmine ha rieseguito la scheda.
**Test:** 470 .NET verdi (306 unit + 164 integrazione) + **274 Vitest** + **47 smoke Playwright** +
**12 del giro pieno** (`pnpm e2e:full`).
Nessuno skippato, **rieseguiti tutti e quattro l'8 set 2026** contro la MariaDB vera prima di
scrivere questa riga: i numeri qui sopra sono misurati oggi, non ricopiati.

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

> Scritta il **5 settembre 2026**, quando M1 si apriva, e lasciata com'era: la regola in cima vale
> ogni volta, il resto è il quadro di quel giorno. **Dove si è arrivati oggi lo dicono l'intestazione
> di questo documento e §19**, non questa sezione — che al 6 set 2026 dice ancora «la prossima
> sessione apre G1», e da G1 a G4 sono chiuse.

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
- **Nessun form si scrive a mano, e il generatore lo rende impossibile piuttosto che sconsigliato**:
  su un tipo che non sa disegnare **lancia**, e da G2 lancia anche su un campo media senza libreria e
  su un istante senza fuso. Quella proprietà non si indebolisce per far passare una fase.
- **Un identificativo di media non si digita mai**: `.meta({ media: true })` apre `MediaPicker`.
  Un'icona non si digita: `.meta({ icon: true })` offre l'allowlist di `shared/icons`. Una data e un
  istante sono input nativi il cui valore resta **ISO in UTC**, e un istante mostra sotto l'ora del
  fuso della divisione.
- **Le chiavi i18n dei figli di un gruppo si scrivono piatte**: `"seo"` accanto a `"seo.title"`.
  i18next risolve una chiave puntata in entrambi i modi (misurato), e un `seo` annidato sarebbe un
  oggetto dove il nome del gruppo deve essere una parola.
- **Un file caricato non tiene il nome che aveva.** Il nome su disco lo genera `MediaStorage` e non
  deriva mai da quello dell'upload: due dipartimenti che caricano `logo.png` non si sovrascrivono, e
  un nome che qualcuno ha digitato non diventa un percorso. La stessa classe è l'unica che apre un
  file della libreria, e apre solo ciò che ha la forma che ha generato.
- **Che tipo sia un file lo dicono i suoi byte, mai l'intestazione che li accompagnava.**
  `MediaFormats.Detect` decide sia se un caricamento entra sia con che cosa viene servito.
- **`mediaId` è il nome con cui un blocco nomina un file**, a qualunque profondità, ed è una
  convenzione dichiarata in `docs/UI-GUIDELINES.md` perché il server non può leggere lo schema di un
  blocco. `JsonQuery` è l'unico posto che chiede a un `body_json` se nomina un id; un blocco che
  inventasse un altro nome si vedrebbe cancellare il file sotto i piedi — ed è per questo che lo
  sfondo di una sezione porta `mediaId` e non `backgroundMediaId` (§17).
  Un blocco che mostra molti file tiene una **lista di oggetti** con dentro `mediaId`
  (`images[] { mediaId }`), perché il generatore disegna liste di oggetti; `mediaIds[]`, un array
  nudo, resta capito dalla stessa query e non lo scrive più nessuno.
- **Una risorsa dice come si chiama nel contratto**, `CrudOptions.Name`, quando non basta l'area
  dei permessi. Due risorse nella stessa area — `/api/content` e `/api/categories` — rispondevano
  entrambe a `ContentList`, e il generatore del client teneva l'ultima letta: una risorsa che
  sparisce dal client senza che niente lo dica.
- **Le righe che nessuno scrive si dichiarano sulla risorsa**, `CrudOptions.ReadOnlyRows`. Non è un
  permesso e non c'è permesso che le sblocchi — **nemmeno un superadmin** — perché sono righe che
  qualcos'altro scrive: una modifica a mano tornerebbe indietro al primo salvataggio di ciò che
  rispecchiano. Oggi lo dichiara solo `CalendarEntry`, per le voci proiettate da un modulo, e
  l'entità è il posto che decide quali sono, così il badge che la lista disegna e il 403 che il
  motore dà non possono dire cose diverse.
- **Le righe che ogni dipartimento può *leggere* si dichiarano una volta sola.** L'entità porta
  un'espressione (`ContentEntry.SharedForReading`); il motore CRUD la mette in `OR` con il filtro di
  dipartimento della lista, e l'unico authorization handler chiede alla riga la **stessa espressione
  compilata** (`ISharedForReading`). Vale **solo** per il permesso di lettura, cioè il `.View`
  dell'area secondo `PermissionCatalog.ViewOf`: condividere una riga in lettura non è mai una
  licenza per modificarla. Oggi lo dichiara solo `ContentEntry`, per i template.
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
| `2026-09-05-ambiente-e2e.md` | L'ambiente `E2E` e `POST /e2e/signin`: perché il banco ha un bypass di autenticazione, e com'è recintato — vive in un ambiente solo, e il flag fuori da lì **ferma l'applicazione**. Scritta in G0 di M1. |
| `2026-09-05-template-di-sistema-e-dipartimenti.md` | Un template appartiene a un dipartimento, ma **lo legge tutto lo staff**: senza, per otto dipartimenti su nove «Nuovo da template» non esiste, e una pagina nata da un template che il suo editore non può leggere perde i vincoli nell'editor. **Decisa da Carmine** il 5 set 2026; **costruita in G5** (§19), con `ContentEntry.SharedForReading` letta in SQL dal motore CRUD e in memoria dall'unico handler. |
| `2026-09-05-dashboard-di-dipartimento.md` | Ogni dipartimento nasce con la propria dashboard. Misura il bivio — una riga di `cms_contents` contro una disposizione di widget — e **raccomanda** la prima. ⚠️ **Ancora da confermare**, prima di G8: è l'unica nota aperta. |
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
- ~~**Il `filter[...]` fa un solo confronto, l'uguaglianza.**~~ **allargato in G1**: l'uguaglianza su
  una colonna resta `CrudOptions.Filterable`, e un filtro che è una domanda e non un confronto —
  «quali pagine usano questo file?» — è `CrudOptions.CustomFilters`, che riceve la query e il valore
  grezzo e la restringe. Intervalli e `in` non ci sono ancora; il giorno che servissero, il posto
  ormai esiste ed è quello.
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
- ~~**`seo` non ha un campo nell'editor.**~~ **chiuso in G2**: è un `localizedObject` con la forma che
  il design M1 §9.2 decide — `{ title, description, ogImageMediaId }` per lingua — e viaggia nei
  valori del form invece di essere trasportato intatto dalla schermata. `ogImageMediaId` è il primo
  campo media vero dell'hub. La riga qui sotto resta perché dice **perché** era stato rimandato, ed è
  la ragione per cui la forma l'ha decisa M1 e non F7.
- **(storico) `seo` non aveva un campo nell'editor, di proposito.** È un `Localized<JsonNode>` e il
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
- ~~**`expiresAt` di un grant è una casella di testo.**~~ **chiuso in G2**: `.meta({ date: true })` è
  un input nativo, e il valore che arriva al server è un istante ISO in UTC invece di un testo che
  .NET interpretava senza fuso. L'etichetta non porta più il formato — quello è mestiere dell'input —
  e il suggerimento dice la sola cosa che l'input non dice, cioè che vuoto significa «mai».
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

> ✅ **Fatto il 6 settembre 2026, in G3 e G4** (§17, §18): i sedici Content/Layout/Interactive/
> Structure e i sei Data esistono, il registry ne conta **27**, e le convenzioni sono scritte. Del
> catalogo restano fuori solo i due Data di un modulo — `eventList` (M2) e `virtualAirlines` (M3).
> Quanto segue è la risposta com'era stata scritta il 5 settembre; si legge ancora perché il
> ragionamento — che cosa è un blocco, che cosa non deve diventarlo — non è cambiato.

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
- **L'elenco chiuso della ui-kit e il registry dei blocchi sono due cose diverse.** I sedici
  componenti (quindici più `MediaPicker`, nato in G1) sono pezzi React riusati fra schermate; i
  blocchi sono un registry a parte, e un blocco `Table` non aggiunge di per sé un componente
  all'elenco — G3 ne ha aggiunti sedici senza toccare quella lista. La ui-kit **monta tutto ciò che
  il registry dichiara**, quindi un blocco nuovo compare lì da solo: nessuno deve ricordarsi di
  aggiungerlo.
- **Le icone sono già decise e non vanno ridiscusse**: `lucide-react` (piano §16.C, design §7.1,
  `UI-GUIDELINES.md` §2), e **ogni blocco ne dichiara una** — il tipo lo impone
  (`shared/modules.ts:59`). `web/src/shared/icons/` **esiste da G2** e tiene l'allowlist da cui un
  redattore sceglie; nessuna icona è ancora mancata dal set, quindi non ce n'è una disegnata a mano.

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

**Dove sono adesso** (7 set 2026, dopo G0-G11): **n.1 chiuso** in G0 (§14), **n.3 e n.4 chiusi**
in G2 (§16), **n.10 chiuso** in G10 (§24), **n.2 chiuso** in G11 (§25). Restano aperti il
n.5, il n.7, il n.8 e il n.9; il n.6 è di M2. G5 ne ha aggiunti due suoi, piccoli e scritti in §19: la slug di un documento che coincide con
un codice di dipartimento, e il vocabolario che viaggia con ogni lista. Le voci qui sotto portano il segno di chi le ha chiuse: **questa lista e §7 devono dire la stessa
cosa**, ed è la ragione per cui si rileggono insieme a fine fase.

1. ~~**Playwright, `pnpm e2e`.**~~ **Chiuso il 4 set 2026, a forza** (§11): esiste `pnpm e2e`, e al
   5 set 2026 sono **dieci** test su Chromium contro il bundle di produzione, **bloccanti in CI** —
   quattro in `smoke.spec.ts` e sei in `back-office.spec.ts`, cresciuti a ogni difetto trovato
   guardando (§11, §12, §13), e gli ultimi due misurano **geometria** perché il testo era già
   corretto. ~~Quello che **resta
   scoperto** è la metà che il design §8 immaginava~~ — **chiuso il 5 set 2026 da G0** (§14): il giro
   con l'**API vera**, contro MariaDB vera e l'applicazione pubblicata, sono tre test bloccanti in
   CI (`pnpm e2e:full`). Gli smoke restano quelli che non hanno un'API apposta, e al 6 set 2026 sono
   **tredici**.
2. ~~**Un cambio di template non si propaga, e l'editor non lo mostra.**~~ **Chiuso in G11** (§25):
   la regola non si è mossa — un template non riscrive mai una pagina da solo — e adesso l'editor
   **dice** che cosa è cambiato, una differenza alla volta. Il terzo dei tre stati che design §9.1
   chiedeva è stato **corretto** invece che improvvisato: «i vincoli sono cambiati» non è
   calcolabile, perché quelli di prima non sono scritti da nessuna parte; «la pagina non soddisfa
   più il vincolo di adesso» sì, ed è quello che l'editor riporta.
3. ~~**`seo` non ha un campo nell'editor.**~~ **Chiuso in G2** (§16): è un `localizedObject` con la
   forma che il design M1 §9.2 decide — `{ title, description, ogImageMediaId }` per lingua — e
   viaggia nei valori del form. Il tipo nuovo del generatore era davvero un'estensione, come questa
   riga prevedeva, e `ogImageMediaId` è il primo campo media vero dell'hub.
4. ~~**`expiresAt` di un grant è una casella di testo.**~~ **Chiuso in G2** (§16): `.meta({ date:
   true })` è un input nativo, e quello che arriva al server è un istante ISO in UTC invece di un
   testo senza fuso.
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
10. ~~**La ricerca non ordina per rilevanza in modo esplicito e non evidenzia niente**, e InnoDB
    ignora le parole più corte di tre lettere.~~ **Chiuso in G10** (§24): si ordina per punteggio e
    poi per recenza — che ha voluto una colonna nuova sull'indice — l'evidenziazione la fa il client
    su uno `snippet` di testo, e le parole troppo corte le **dice**, perché è l'unica delle tre che
    il codice non può risolvere.

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

Niente. La fase è chiusa: PR #35 fusa con la CI verde — tre test nuovi in CI con il servizio
MariaDB, `E2EBenchTests` (2), e le suite di M0 tutte ancora verdi.

---

## 15. G1 di M1: la media library (5 set 2026)

Un'immagine caricata una volta si riusa ovunque. È la fase che viene prima dei blocchi perché otto
dei ventidue la nominano, e il design la descrive per intero in §2 di `03-design-m1.md`.

### Che cosa c'è adesso

- **`cms_media`** (`MediaAsset`): `IOwnedByDepartment, IVisible, IAuditable`, e **non**
  `IProjectable` — un file non si cerca da sé, si cerca la pagina che lo usa. Migrazione
  `AddMediaLibrary`, puramente additiva: una tabella e tre indici, nessuna colonna esistente toccata.
- **I file stanno su disco**, sotto `HubPaths.Media` (o `Media:Directory`), in cartelle
  `anno/mese`. Il nome su disco è **opaco** (`2026/09/<32 esadecimali><estensione>`) e non deriva mai
  da quello caricato: due dipartimenti che caricano `logo.png` ottengono due file, e un nome che
  qualcuno ha digitato non diventa mai un percorso.
- **`POST /api/media`** è l'**unico endpoint scritto a mano** che M1 aveva previsto. Valida
  dimensione e tipo contro `MediaOptions`, scrive i byte e **poi** la riga.
  ⚠️ Il tipo lo decidono i **byte**, mai l'intestazione del multipart: un caricamento che dichiara
  `image/png` e contiene HTML è il trucco più vecchio che ci sia, e `MediaFormats.Detect` lo rifiuta.
- **`GET /media/{id}/{nome}`** serve il file da Kestrel dietro il **query filter**: una media
  `Staff` risponde **404** a un anonimo, non 403 — un 403 confermerebbe che a quell'id c'è qualcosa.
  Cache lunga e `immutable` solo per una media pubblica, `private` per tutte le altre. `/media` è in
  `SpaFallbackExclusions`, o la SPA se lo mangerebbe.
- **Larghezza e altezza** le legge `ImageHeader`, un parser di intestazioni per PNG, JPEG e WebP in
  una sessantina di righe, senza dipendenze (deciso il 5 set 2026, `04-` G1). ⚠️ **Quei tre formati
  sono il perimetro**: un formato che chiede di più è una (c) con la nota, non un allargamento
  silenzioso di quel file.
- **Cancellare prende il file, non la riga.** La riga sopravvive perché una pagina già pubblicata
  ne nomina l'id e un browser ne ha in cache l'indirizzo; i byte se ne vanno **solo** se nessuna
  versione pubblicata li mostra ancora. Chi cancella vede prima **dove è usata**.
- **`MediaPicker`** entra nell'elenco chiuso (sedicesimo componente), con la sua sezione nella
  ui-kit. Sceglie e basta: caricare è della schermata della libreria, e un selettore che caricasse
  sarebbe un secondo modo di far entrare un file nell'hub.
- Permessi **`Media.View`** e **`Media.Edit`**, nel catalogo e nella matrice, con la riga di test.
  Nessun handler nuovo.

### Tre estensioni generiche a `MapCrud`, e perché non erano aggiramenti

Sono tutte regola (b) di `CLAUDE.md` §5 — si estende il meccanismo — e vivono in `Core/Data/Crud/`
senza nominare la media da nessuna parte. Vanno conosciute perché le userà chi viene dopo.

| Estensione | Che cosa dice | Perché |
|---|---|---|
| `CrudOptions.MapCreate` | «questa risorsa non ha una create JSON» | Una riga `cms_media` senza file non deve poter esistere. L'alternativa scartata era spostare l'upload su un secondo indirizzo, cioè due modi di creare una media (previsto in `04-` G1) |
| `CrudOptions.Delete` | «che cosa significa cancellare, qui» | Il motore chiama questo invece di `Remove` e salva lo stesso: audit, guardia e proiezioni restano quelle di una scrittura qualsiasi |
| `CrudOptions.CustomFilters` | un `filter[nome]` che non è un'uguaglianza su una colonna | «Quali pagine usano questo file?» si legge dentro un `body_json`, non in una colonna. Chiude di sponda il debito di §7 sul `filter` che fa un solo confronto |

⚠️ `CustomFilters` e `Filterable` vivono nello stesso spazio di nomi e il motore **rifiuta di
partire** se una risorsa dichiara lo stesso nome in tutti e due: un filtro che è insieme colonna e
funzione è un filtro il cui comportamento dipende dall'ordine in cui questo file li guarda.

### La domanda che il server non può fare al blocco, e come si risponde senza aggirarla

Il backend non legge mai una `props` (§3), quindi non sa che cosa sia un `hero` né una `gallery`. Ma
per non cancellare il file sotto una pagina già pubblicata deve poter chiedere: **questo documento
JSON nomina questo id?**

La risposta è `Core/Data/JsonQuery.cs`, accanto a `FullTextSearch`: una funzione mappata sul modello
(`JSON_CONTAINS(JSON_EXTRACT(documento, percorso), candidato)`, come fa già `LocalizedQuery`),
nessuna tabella e nessuna migrazione. Il percorso è ricorsivo (`$**.mediaId`, `$**.mediaIds[*]`),
quindi trova un id a qualsiasi profondità senza sapere com'è fatta una sezione.

⚠️ **`mediaId` e `mediaIds` sono una convenzione, non un tipo.** È scritta in `docs/UI-GUIDELINES.md`
perché è l'unica cosa su cui i due lati devono mettersi d'accordo per nome: un blocco che inventasse
un terzo nome si vedrebbe cancellare il file sotto i piedi. **Verificata su MariaDB vera**, non
assunta: `MediaUsageQueryFindsPagesByMediaId` trova sia la props singola sia quella dentro la lista.

### I test, e il fatto che sono stati rotti

Sei di integrazione (`MediaEndToEndTests`, MariaDB vera più una cartella su disco tutta loro), cinque
unitari su `ImageHeader` e `MediaFormats`, sette Vitest (`MediaPicker`, più `describeProblem`), due
smoke nuovi.

⚠️ **Sono passati tutti al primo giro, il che non vuol dire niente** (§A.10 del piano M1). Quindi
sono stati verificati **rompendo** ciò che proteggono, tutti insieme: servire senza il query filter,
cancellare il file sempre, dimenticare che una gallery tiene una lista, derivare il nome su disco da
quello caricato, credere al `Content-Type` dichiarato. **Sei rotture, sei rossi, ognuno sul test
giusto.** Lo stesso per i due smoke (il file servito come 404: il preview resta nella pagina, con il
suo testo alternativo, e solo `naturalWidth` lo smaschera) e per gli unitari.

⚠️ **E una trappola dell'ambiente, che è costata mezz'ora**: dopo aver rimesso a posto i file rotti i
test sono rimasti rossi, perché `cp` più `mv` avevano restituito ai sorgenti un mtime **più vecchio**
di `bin/`, e MSBuild li ha considerati aggiornati. Non era un difetto: era una build vecchia. Se un
test resta rosso dopo un ripristino, `touch` sui file e ricompilare **prima** di cercare la causa nel
codice.

### Che cosa la fase non ha fatto, ed è giusto così

- **Nessun blocco usa ancora una media**: è G3, e il campo `.meta({ media: true })` del generatore è
  G2. `MediaPicker` esiste, è nella ui-kit e ha i suoi test, ma nessuna schermata di prodotto lo
  monta ancora — sarà G2 a farlo.
- **Nessuna nota di decisione**: la fase non ha incontrato un caso (c). Le due cose che potevano
  diventarlo — il parser delle dimensioni e la convivenza fra l'upload e `MapCrud` — erano già decise
  nel piano prima di aprire la sessione, ed è esattamente il motivo per cui quella riga esisteva.
- **Il recupero dei file orfani non esiste.** Un file resta finché una versione pubblicata lo mostra;
  quando quella versione viene sostituita, il file resta comunque. Non è un difetto della fase: è che
  «passare a ripulire» è un job, e un job che cancella file va deciso prima di essere scritto.

### Debiti nuovi che G1 lascia

1. **Nessuno ripulisce i file che nessuna versione nomina più.** Vedi sopra: la riga è marcata, il
   file resta. Finché la libreria è piccola non si nota; il giorno che si nota, è un job con una
   decisione dietro, non un `Delete` in più dentro l'endpoint.
2. **Il limite di dimensione è controllato dopo che il corpo è arrivato.** `file.Length` esiste
   perché Kestrel ha già bufferizzato il multipart: il rifiuto è corretto, ma i byte hanno viaggiato.
   Un limite vero si mette sul corpo della richiesta, ed è configurazione del server (Passenger e
   Cloudflare hanno la propria): da guardare quando M2 farà il pacchetto e lo staging.
3. **`ContentEntry.CoverMediaId` e `FileMediaId` non hanno ancora un campo nell'editor**: sono
   colonne che esistono da M0 e che G5 (news e documenti) userà. `JsonQuery.UsingMedia` le conta già,
   quindi il giorno che una news avrà una copertina l'uso sarà trovato senza toccare quella query.

---

## 16. G2 di M1: il generatore di form sa disegnare tutto (5 set 2026)

Le cinque estensioni che il design §1.6 chiedeva. Non sono cinque comodità: sono le cinque cose che i
ventidue blocchi di G3 chiedono, e senza le quali qualcuno avrebbe scritto un form a mano — che è
esattamente ciò che tutto questo meccanismo esiste per rendere impossibile.

### Che cosa il generatore sa fare adesso

| Annotazione | Che cosa disegna | Che cosa promette |
|---|---|---|
| `.meta({ media: true })` su un numero | `MediaPicker`, la libreria di G1 | Un id non si digita mai: un campo numerico libero produce pagine che puntano a file cancellati |
| `.meta({ icon: true })` su una stringa | Una griglia di icone dell'allowlist | Insieme chiuso, e ogni voce mostra la propria figura |
| `.meta({ date: true })` / `datetime` | Input nativo | Il valore nel form è **sempre ISO in UTC**, qualunque sia il fuso del browser |
| `localizedObject({ … })` | Schede per lingua, e dentro il generatore stesso | Un coordinatore riempie campi, non scrive JSON |
| liste | Su e giù accanto ad aggiungi e rimuovi | Sono i pulsanti che una tastiera raggiunge; il drag-and-drop di G11 **non** li sostituisce |

Due estensioni non restano senza cliente, ed è voluto: **`expiresAt` di un grant** smette di essere
una casella di testo (debito n.4 chiuso) e **`seo`** diventa un campo vero, `{ title, description,
ogImageMediaId }` per lingua come decide il design §9.2 (debito n.3 chiuso). `ogImageMediaId` è il
primo campo media vero dell'hub: G1 ha costruito il selettore, G2 gli dà qualcuno che lo monta.

`SchemaForm` guadagna due props, entrambe opzionali: `mediaLibrary` (la libreria da cui scegliere) e
`division` (lingua di default e fuso). Non sono in `locales` perché **solo due tipi di campo su
undici** ne hanno bisogno; un form che non ha né media né istanti non se le vede chiedere. E se un
campo le pretende senza averle, il generatore **lancia** e dice quale manca — la stessa disciplina
che ha su un tipo che non sa disegnare.

### Tre cose decise scrivendo, e perché

1. ⚠️ **L'allowlist delle icone sta in `web/src/shared/icons/`, non in `blocks/icons.ts`** come
   diceva il piano. Motivo tecnico e non estetico: `blocks/` importa già il generatore (per
   `localized()`), quindi il generatore che importa `blocks/` chiuderebbe un ciclo fra i due. La
   cartella è quella che il design §1.4 aveva già messo in conto per M1, e nasce una volta. **G3 la
   legge da lì.**
2. ⚠️ **Le icone sono una griglia di radio, non un select** come diceva il piano. `SelectItemProps`
   di Atmosphere ha `label?: string`: un select può elencare i **nomi** e nient'altro, e un nome
   senza la sua figura è precisamente la scelta che nessuno può fare. L'insieme resta chiuso — che è
   ciò di cui parla la regola — e un `radiogroup` è una cosa che la tastiera già sa percorrere.
   Quinto contratto di Atmosphere misurato invece che assunto, dopo `DarkModeToggle`, `Select`,
   `SidebarContainer` e `Tabs`.
3. **Le chiavi dei figli di un gruppo si scrivono piatte**: `"seo"` accanto a `"seo.title"`, non
   `seo` annidato. Avevo introdotto una famiglia `groups` per evitare la collisione fra il nome del
   gruppo e i suoi figli, poi **l'ho misurata** con i18next invece di dedurla: una chiave puntata si
   risolve in entrambi i modi, quindi la famiglia nuova non serviva e la convenzione che i test di
   `SchemaForm` usano da M0 bastava. Un concetto in meno, trovato smontando il proprio.

### Un guscio condiviso invece di un secondo

`LocaleFields` e l'oggetto tradotto avevano bisogno della stessa cornice — schede per lingua, badge
«vuoto» su quelle ancora da riempire — e la cornice è stata estratta in `shared/forms/LocaleTabs.tsx`
invece di essere scritta due volte. Quello che resta a `LocaleFields` è ciò che appartiene a una
stringa: il pulsante «copia dall'italiano». **I tre test di `LocaleFields` non sono stati toccati**, e
sono la prova che il rifattore non ha cambiato quello che quel campo fa.

### I test, e due lezioni che sono costate tempo

Undici Vitest in `shared/forms/extensions.test.tsx` — uno per estensione, i due rifiuti (media senza
libreria, istante senza fuso) e quello che pretende che il generatore **continui a lanciare** su un
tipo che non sa disegnare — più uno smoke sulla galleria che li misura tutti e cinque insieme.

⚠️ **Verificati rompendo, in due giri**: il primo ha rotto le cinque conversioni e ha fatto cadere
otto test su undici; i tre rimasti verdi proteggevano cose che non avevo rotto, quindi il **secondo**
giro ha rotto anche quelle (i pulsanti agli estremi, il rifiuto dell'istante senza fuso, il lancio su
un tipo ignoto) e le ha fatte cadere. Un giro solo avrebbe lasciato credere che tre test fossero
inutili.

Due cose che sono costate tempo davvero, e che vale la pena non ripetere:

1. ⚠️ **`git checkout -- <file>` su lavoro non committato lo cancella.** Rimettendo a posto i file
   rotti del primo giro ho usato `git checkout --` invece delle copie che avevo fatto: `HEAD` era
   `main`, e mezz'ora di `SchemaForm.tsx` e `schema.ts` è sparita. Ricostruita dagli script della
   sessione. **Regola**: prima di rompere qualcosa apposta, si committa; e si ripristina dalle copie,
   mai da git.
2. ⚠️ **`"2:00"` sta dentro `"12:00"`.** Lo smoke che doveva provare che l'orario locale non è UTC
   passava con l'orario UTC, perché l'asserzione era un `/2:00.*Europe\/Rome/`. Ora è un `toHaveText`
   esatto e cade come deve. È la stessa famiglia dei tre difetti di §11–§13: un'asserzione che chiede
   «c'è?» invece di «è quello?».

E una terza, più piccola: la fixture degli smoke aveva `timezone: 'UTC'`, che rende le due righe di
ogni orario identiche — HANDOFF §13 lo aveva già segnalato come falso allarme. Ora è `Europe/Rome`,
quindi uno schermo che mostrasse UTC due volte non passa più.

### Che cosa la fase non ha fatto

- **Nessun blocco usa ancora le estensioni**: è G3. Quello che c'è è il primo cliente di ciascuna
  dove esisteva già una schermata — `expiresAt` e `seo` — e la galleria, che le monta tutte e cinque.
- **Niente `dnd-kit`**: è G11, e il su/giù non gli lascerà il posto.
- **Nessuna nota di decisione**: le tre scelte qui sopra sono estensioni o correzioni di dettaglio
  dentro il perimetro della fase, non meccanismi nuovi. Le prime due sono deviazioni dalla lettera
  del piano con il motivo scritto, e vanno lette prima di G3.

---

## 17. G3 di M1: i sedici blocchi (6 set 2026)

Il grosso del volume di M1, e **zero meccanismo nuovo** — che era l'obiettivo dichiarato della fase e
adesso è un fatto misurato: zero endpoint scritti a mano, zero componenti custom nuovi, zero
meccanismi nuovi. Il registry conta **21 blocchi**, e con il set davanti si è chiuso **§16.C** del
piano, che aspettava dal 2 settembre.

### Che cosa c'è adesso

I sedici: `hero`, `image`, `video`, `embed`, `timeline`, `table` — `cardGrid`, `iconGrid`, `gallery`,
`logoGrid`, `tabs`, `accordion` — `testimonial`, `buttonGroup`, `spacer`, `divider`.

Ognuno è costato **cinque cose e non una di più** (design §1.3): uno schema in `blocks/schemas.ts`,
un componente in `blocks/blocks.tsx`, una riga in `blocks/core.ts`, le chiavi i18n in tutte e due le
lingue, e — per i Data, qui nessuno — un provider. **Nessuno ha aggiunto una sezione alla ui-kit**:
la galleria monta ciò che il registry dichiara, ed è la proprietà per cui esiste.

Accanto a loro:

- **`web/src/blocks/allowlist.ts`**, l'unico punto in cui `video` ed `embed` decidono cosa è lecito
  incorniciare. Non filtra un indirizzo: legge l'indirizzo della **pagina** (quello nella barra del
  browser) e **ricostruisce** quello del player dall'identificatore che ha riconosciuto. Niente di
  ciò che è stato scritto finisce dentro il `src`, e il test lo prova con `evil-youtube.com`.
- **Lo sfondo `image` della sezione**, con il suo `mediaId`, e `BlockDocumentWalker` che ha imparato
  gli sfondi: erano l'unico insieme chiuso dell'envelope che il server non controllava.
- **`docs/UI-GUIDELINES.md`, «The conventions every block follows»**: la spaziatura e lo sfondo sono
  della sezione e mai del blocco, quattro sfondi, quattro larghezze, la resa di una sezione `locked`,
  il blocco sconosciuto visibile solo allo staff, l'icona dichiarata dal tipo, nessuna stringa che
  non sia prosa dentro `props`, nessun blocco che contiene blocchi.

### Le due lacune del generatore che i blocchi hanno trovato per primi

Nessuna delle due è una comodità, ed entrambe sono state chiuse **estendendo** il generatore.

1. **Una voce nuova di lista nasceva come `{}`.** Undici blocchi su sedici hanno una lista di
   oggetti; `append({})` produce campi che React non controlla, e un campo tradotto così dimentica
   ciò che viene scritto dentro. Ora `blankEntry` costruisce la voce dai campi che lo schema
   dichiara. Verificato rompendolo: il test fallisce senza la correzione.
2. **⚠️ Una props tradotta opzionale, lasciata vuota, avrebbe impedito di pubblicare la pagina.**
   Viaggiava come `{ en: "", it: "" }`, e `ContentPublishService` — che il corpo lo legge senza sapere
   che cosa sia un blocco — la leggeva come una traduzione fatta a metà. Un `caption` che nessuno ha
   scritto avrebbe rifiutato la pagina, con un errore che punta a un campo che il redattore non ha
   mai toccato. Ora `writtenValues` salva solo ciò che è stato scritto.
   **La regola sul server non è stata toccata**, ed è la parte da non disfare: un campo
   **obbligatorio** vuoto viene ancora rifiutato, e una lingua scritta e l'altra no è ancora un buco.
   Si è tolta la causa, non il controllo.

### Quattro deviazioni dalla lettera del design, tutte scritte

1. **`table` e `gallery` non hanno liste nude**: `rows[] { cells[] { text L } }` e
   `images[] { mediaId }`. Il generatore disegna liste di **oggetti**, e una lista di valori nudi
   sarebbe stata una sesta estensione per una forma che nessun altro chiede. Effetto collaterale
   buono: la chiave resta `mediaId`, che è quella che `JsonQuery.UsingMedia` cerca a ogni profondità.
2. **L'`alt` non eredita dalla libreria**: vuoto = decorativa. Nota
   `decisions/2026-09-06-alt-delle-immagini.md`, decisa da Carmine prima di scrivere gli schemi.
3. **Larghezze quattro** (`narrow` resta: toglierlo non sarebbe additivo su corpi già pubblicati),
   **sfondi quattro**. La chiave della sezione si chiama `mediaId` e non `backgroundMediaId`, di
   nuovo per il punto 1.
4. **`aspect` di `video` vale `16x9 | 4x3 | 1x1`.** I due punti sono il separatore di namespace di
   i18next: `options.aspect.16:9` non si risolve, e il campo avrebbe mostrato la chiave al posto
   dell'etichetta.

### Tre cose viste misurando, che valgono più del codice che le ha prodotte

- **Il wrapper `overflow-x-auto` del blocco `table` era una copia.** L'e2e che misura la pagina
  passava **identico** togliendolo, perché la tabella di Atmosphere si avvolge già in
  `relative w-full overflow-auto`. Tolto (`CLAUDE.md` §2). Il test resta, perché la proprietà — la
  pagina non scorre di lato — va difesa comunque; ma nella spec c'è scritto che misura la pagina e
  non il nostro codice, il che è la differenza fra una rete e un test che si crede una rete.
- **`has()` dentro `blocks/registry.test.ts` era più severo di i18next.** Camminava sui punti, e le
  chiavi dei figli si scrivono piatte (`"cards.title"` accanto a `"cards"`); i sedici blocchi lo
  hanno fatto fallire su dieci gruppi. i18next risolve entrambe le forme (`deepFind` ricompone i
  segmenti), ed è stato **letto nel sorgente** prima di allargare il test. Un controllo più severo
  del runtime fallisce su chiavi che funzionano.
- **`aspect: '16:9'` non è stato scoperto da un test**, ma dalla domanda «questa stringa finisce in
  una chiave i18n?». Vale la pena farsela ogni volta che un `z.enum` nasce.

### I test

176 Vitest (erano 145), 259 unit C#, 109 di integrazione, 16 smoke Playwright. **Rieseguiti tutti il
6 set 2026** contro la MariaDB vera prima di scrivere questa riga. Le due reti nuove che contano:

- `web/e2e/blocks.spec.ts` **misura** che a 1280 px le tre card stanno sulla stessa riga in tre punti
  diversi, e che a 375 px stanno una sotto l'altra. Verificata rompendola (una colonna sola: fallisce).
- `EnvelopeValidationRejectsABackgroundTheServerDoesNotKnow` posta uno sfondo che il server non
  conosce, ed è ciò che tiene allineate a mano `BACKGROUNDS` e `BlockDocumentWalker.Backgrounds`.
  Verificata rompendola (tolto `CheckBackground`: fallisce).

### Che cosa la fase non ha fatto, ed è giusto così

- **Nessun blocco Data e nessun provider**: è G4, e con esso il registry passa a 27.
- **Nessuna pagina pubblica costruita con i blocchi nuovi**: è G8. I sedici esistono e si vedono
  nella ui-kit; comporre `/`, `/start`, `/pilots` con loro è un'altra fase.
- **Nessun lightbox vero**: `gallery.lightbox` apre il file in una scheda. Un dialogo nostro sarebbe
  un componente custom nuovo, e quell'elenco è chiuso (design M1 §12).

### Debiti nuovi che G3 lascia

1. **L'esempio di `image`, `gallery` e `logoGrid` nella ui-kit punta ai file 1, 2 e 3**, che su
   un'installazione nuova non esistono: la galleria mostra un'immagine rotta finché la libreria è
   vuota. Non c'è un file finto nel repository e inventare un indirizzo sarebbe peggio; si guarderà
   in G12, quando la divisione avrà caricato qualcosa.
2. **L'esempio di `embed` punta a Vimeo**, quindi aprire `/staff/admin/ui-kit` carica un riquadro da
   fuori. Per `video` è stato evitato passando a un file della libreria; per `embed` non c'è modo,
   perché è esattamente ciò che il blocco fa.
3. **`writtenValues` è applicato dove le props si scrivono** (l'editor che applica, il blocco appena
   aggiunto). Un terzo punto che scrivesse props senza passare di lì rimetterebbe il problema: se
   G11 aggiunge il drag-and-drop o un «duplica» che ricostruisce le props, va passato di lì.

---

## 18. G4 di M1: i sei blocchi Data e i loro provider (6 set 2026)

Il registry conta **27 blocchi**, che è il set intero che il design M1 §1 prevedeva per il nucleo
(`eventList` arriva con Events in M2, `virtualAirlines` con Flight Ops in M3). **Zero endpoint
scritti a mano, zero componenti custom nuovi**; l'unico meccanismo nuovo è quello che il piano
assegnava esplicitamente a questa fase, la lettura dello stato della rete.

### Che cosa c'è adesso

I sei: `stats`, `networkStats` (**`alwaysLive`**), `calendar`, `newsList`, `documentList`,
`staffList`. Ognuno è costato le cinque cose di design §1.3, e per un Data la quinta sono due:
un `IDataBlockProvider` registrato per il `type` e un `IBlockDescriptor` in `CoreBlocks.All`.

- **`stats`** risponde su un **insieme chiuso** di cinque metriche del nucleo — `knownMembers`,
  `staffMembers`, `publishedNews`, `publishedDocuments`, `upcomingEntries`. Non è nato un registro
  delle metriche: un modulo che vuole la propria cifra registra il proprio blocco (design §1.2,
  correzione 2). Una metrica che nessuno dichiara viene **lasciata fuori** dalla risposta, non
  rifiutata: un corpo scritto da una release più nuova non deve diventare una pagina di errore su
  una più vecchia.
- **`networkStats`** è l'unico `alwaysLive` del set, e la regola sta **nel tipo**: l'editor non
  mostra il toggle e `ContentPublishService` non congela, senza un `if` da nessuna parte.
- **`calendar`, `newsList`, `documentList`** leggono tabelle che esistono da M0 e **non aspettano**
  G5 né G6, che era il punto di metterli qui.
- **`staffList`** raggruppa per dipartimento e poi per FIR, ordina per livello della posizione e
  mostra nome, posizione e un link al profilo ufficiale. Niente altro: **non esiste un profilo
  membro pubblico** e non nascerà (piano §9.7). Il blocco dice in una riga che compare solo chi ha
  fatto almeno un accesso, invece di fingere completezza.

Accanto a loro:

- **`Core/Ivao/IvaoWhazzup.cs`**, la lettura dello stato della rete: endpoint pubblico (nessun
  token, così un'installazione senza credenziali applicative disegna comunque il blocco), cache di
  **60 s** — anche di un fallimento, o una rete giù diventa una chiamata per lettore — e **non lancia
  mai**. Sta in `Core/Ivao/`, il solo posto dove il nome IVAO può comparire (piano §4.2).
- **`IvaoAirspace`**, che è la risposta alla domanda «in area»: un controllore è dei nostri quando la
  **stazione** del suo nominativo (quello che sta prima del primo `_`) è un centro o un aeroporto
  dello snapshot; un pilota quando il piano di volo parte o arriva da un aeroporto dello snapshot.
  È una **regola sopra i dati di riferimento**, non una lista nel codice: una divisione che forka ha
  la propria risposta senza configurare niente. `IFirDirectory` ha imparato a restituire l'aerospazio
  intero con la stessa cache che aveva già per i FIR.
- **`BlockProps`**, i lettori di props che `LinkListProvider` si teneva privati, adesso condivisi da
  tutti e sette (`CLAUDE.md` §2).
- **`DataBlockScope.WithinPage`**, il soffitto di visibilità alla pubblicazione, **un metodo invece
  di uno per tabella**: il predicato è costruito con `Expression.Property` come fa già
  `VisibilityQueryFilter`, perché una lambda su una proprietà d'interfaccia è una lambda che il
  provider del database deve indovinare.

### Tre deviazioni dalla lettera del design, tutte scritte

1. **Le liste di valori nudi non esistono, nemmeno qui.** `metrics[]`, `figures[]` e `kinds[]` sono
   liste di **oggetti** (`{ metric }`, `{ figure }`, `{ kind }`), esattamente come `table.rows` e
   `gallery.images` in G3: il generatore disegna liste di oggetti, e inventare un tipo di campo per
   tre schemi sarebbe stata la sesta estensione per una forma che nessun altro chiede.
2. **`networkStats.showFirs` si chiama `showPositions`**, e la risposta porta `positions` e non
   `firs`. Motivo: la stazione di un nominativo è tanto spesso un **aeroporto** quanto un FIR
   (`LIRF_TWR`), e chiamarla FIR sarebbe stato un nome che mente. Le posizioni sono un elenco piatto
   ordinato per nominativo, non un raggruppamento.
3. **`calendar` non ha la props `view`.** Il componente di G4 è **solo la vista agenda** — lo dice il
   piano — e un select con una sola voce è un comando che non fa niente. Quando G6 fa nascere
   `CalendarView`, `view` si aggiunge con `.default('agenda')`: è additivo, e i corpi già pubblicati
   non se ne accorgono.

⚠️ Le metriche e le figure sono scritte come **parole composte** (`publishedNews`, non `news`) per
la regola di design §1.5: ogni stringa dentro `props` finisce nel testo della pagina per la ricerca,
e una metrica chiamata «news» sarebbe una pagina che risponde a chi cerca news.

### Due cose decise scrivendo, che vale la pena sapere

- **L'URL del profilo IVAO sta in un file di lingua**, `blocks.staffList.profileUrl`, non nel codice.
  È il precedente del footer (`locales/*/common.json` nomina già `ivao.aero` per termini, privacy e
  regole), ed è l'unico modo per cui `Core/Content` e `web/src/blocks/` non nominano IVAO: la domanda
  «questo nomina IVAO?» ha una risposta anche quando il dato *è* un indirizzo di IVAO.
- **`departments.<CODE>` è nato in `common.json`**: il blocco `staffList` è la prima schermata
  pubblica che deve dire «Eventi» invece di «ED». Sono nove chiavi in due lingue, non codice.

### I test

199 Vitest (erano 176), 259 unit C#, **115 di integrazione** (erano 109), 17 smoke Playwright, 3 del
giro pieno. Rieseguiti tutti il 6 set 2026 contro la MariaDB vera. I criteri di accettazione della
fase stanno in `tests/IvaoHub.IntegrationTests/DataBlockEndToEndTests.cs` e sono **tutti e sei
verificati rompendoli** (§A.10 del piano di implementazione):

| Test | Rotto così | Esito |
|---|---|---|
| `EveryDataBlockTypeHasAProvider` | tolta una registrazione dal container | rosso |
| `NetworkStatsIsNeverFrozenOnPublish` | tolto `AlwaysLive: true` dal descrittore | rosso |
| `PublishFreezesNewsListButNotNetworkStats` | idem | rosso |
| `DataBlockRespectsVisibility` | `IgnoreQueryFilters()` sul provider dei contenuti | rosso |
| `StatsMetricsAreAClosedSet` | tolto il filtro sull'insieme chiuso | rosso |
| `NetworkStatsCountsOnlyWhatTheSnapshotCallsOurs` | `Covers` che risponde sempre sì | rosso |

Più, lato browser: `web/e2e/blocks.spec.ts` **misura** che tre numeri di `stats` stanno sulla stessa
riga in tre punti diversi a 1280 px e vanno a capo a 375 px (verificato togliendo la terza colonna:
fallisce), e `blocks.test.tsx` monta ognuno dei sette blocchi Data con il proprio `exampleData` e
pretende che disegni qualcosa — un componente che legge la risposta sotto la chiave sbagliata cade
nello stato «in arrivo» e passerebbe uno smoke.

### Che cosa la fase non ha fatto, ed è giusto così

- **Nessuna schermata di news, documenti o calendario**: sono G5 e G6. I blocchi leggono le tabelle,
  che è tutto quello che serviva per non aspettarle.
- **Nessun `CalendarView`**: nasce in G6, quando due schermate lo montano, che è il criterio
  dell'elenco chiuso. Il componente agenda di G4 sparisce lì.
- **Nessuna striscia `LiveStatusStrip`**: è G9, che aggiunge la striscia e non il dato.
- **`stats` non ha una metrica di eventi o di tour**: le porteranno i moduli, con i loro blocchi.

### Debiti nuovi che G4 lascia

1. **Le figure di `networkStats` in `/staff/admin/ui-kit` sono un `exampleData`**, quindi la galleria
   non prova mai la chiamata vera. Il giro contro la rete vera lo fa solo il test di integrazione con
   le fixture; la prima volta che qualcuno guarderà il blocco contro l'API vera è G9.
2. **Il fuso della divisione nel blocco `calendar` arriva da `/api/me`** con `useQuery(bootstrapQuery)`.
   È una lettura dalla cache (la shell l'ha già caricata), ma è la prima volta che un blocco legge il
   bootstrap: se in G6 `CalendarView` fa lo stesso, la lettura va fatta una volta sola e passata.
3. **`staffList` ordina i cognomi con `CurrentCultureIgnoreCase`**, cioè con la cultura del processo.
   Su una divisione con un alfabeto diverso l'ordine sarà quello del server e non quello del lettore;
   nessuno lo noterà finché non succede, ed è scritto qui perché quando succederà si sappia dov'è.

---
## 19. G5 di M1: due `kind`, non due tabelle (6 settembre 2026)

La fase che §9.3 del piano aveva messo lì apposta per essere smentita, e non lo è stata. **News e
documenti non hanno una tabella, un editor, un renderer, una pubblicazione né una proiezione di
loro**: sono due valori di `ContentEntry.Kind`, e quello che è servito per farli esistere è
configurazione più cinque colonne che c'erano già.

### Il conto, prima di tutto

| | |
|---|---|
| Entità nuove con un corpo a blocchi | **zero** (`NoSecondContentEntity` lo tiene fermo) |
| Editor nuovi | **zero** — `ContentEditor` prende `kind` e le categorie, lo schema fa il resto |
| Renderer nuovi | **zero** — il pubblico legge con lo stesso `ContentRenderer` |
| Colonne nuove su `cms_contents` | **zero** — le cinque di news e documenti ci sono da M0 |
| Tabelle nuove | **una**, `cms_categories`, già contata fra le sei di design §10.2 |
| Endpoint scritti a mano | **zero** (M1 resta a uno, l'upload) |
| Componenti custom | **zero** (M1 resta a uno, `MediaPicker`) |
| Meccanismi nuovi | **zero**; due estensioni generiche, sotto |

### Che cosa c'è adesso

- **`/staff/{dept}/news`, `/staff/{dept}/documents` e `/staff/{dept}/content`** sono **una schermata
  montata tre volte**: `ContentListScreen` più `ContentFormScreen`, guidate da
  `features/content/kinds.ts`, che è un oggetto di configurazione per `kind` — un `kind` fisso, un
  elenco di colonne, e il namespace da cui la schermata prende le proprie parole. ⚠️ Le **etichette
  dei campi** restano tutte in `content`: «Titolo» e «Indirizzo» vogliono dire la stessa cosa
  qualunque riga si stia modificando, e tre copie sarebbero tre posti da tenere allineati.
- **Il `kind` non è più un campo del form.** Lo fissa la lista da cui si è entrati, esattamente come
  il dipartimento. Un select avrebbe voluto dire una pagina che diventa documento con i campi di una
  pagina ancora a schermo.
- **`cms_categories`**: chiave stabile, etichetta tradotta, ordine, `IsActive`, per dipartimento e
  per `kind`. `MapCrud` come tutto il resto, back-office in `/staff/{dept}/categories`, **seed
  vuoto**. ⚠️ **Nessuna FK** verso `cms_contents`: una categoria cancellata lascia la riga con la sua
  chiave, ed è la regola dei moduli applicata a un vocabolario che può cambiare sotto righe già
  pubblicate.
- **Il pubblico**: `/news`, `/news/{slug}`, `/documents`, `/documents/{slug}`. ⚠️ **`/documents/{dept}`
  non esiste**: un segmento non può essere un dipartimento e una slug insieme, e i documenti di un
  dipartimento sono `?department=ED`, lo stesso filtro che `/news` ha già. La prima versione decideva
  sbirciando se il segmento nominasse un dipartimento, il che prenotava nove slug e nascondeva
  qualunque documento chiamato `ed`; scartata anche la via di mezzo `/documents/dept/{codice}`, che
  toglieva otto slug su nove ma lasciava a una delle due schermate un secondo modo di dire ciò che
  l'altra dice con un search param (deciso da Carmine, design changelog 1.6).
  ⚠️ **Le liste pubbliche sono i blocchi Data di G4**, montati con `BlockView`: `newsList` e
  `documentList` sanno già chiedere, sanno già che cosa un dipartimento può mostrare e sanno già
  disegnare una card. Un secondo lettore delle stesse righe sarebbe stato un secondo posto dove le
  due versioni divergono — e avrebbe voluto un endpoint suo, che M1 ha un budget di uno e l'ha già
  speso sull'upload.
- **Le categorie arrivano al pubblico dentro la risposta del provider**, `categories: [{key, label}]`.
  Chi può leggere la lista può leggere i nomi dei suoi scaffali; così il filtro di `/news` e il
  raggruppamento di `documentList` mostrano la parola invece della chiave, e una categoria cancellata
  si vede come la chiave nuda — che è alla lettera ciò che il design chiedeva.

### La lettura condivisa dei template, e la forma che ha preso

Il primo task, e l'unico che tocca la spina dorsale. Un template appartiene a un dipartimento e lo
**legge** tutto lo staff (design M1 §9.4).

**Una sola fonte**, `ContentEntry.SharedForReading`, che è una `Expression`. Il motore CRUD la mette
in `OR` con il filtro di dipartimento della lista; l'unico authorization handler chiede alla riga
(`ISharedForReading`), e la riga risponde con **quella stessa espressione compilata**. Non due
scritture della stessa regola: una, letta in due lingue.

La scrittura non si è mossa di un millimetro: la regola dell'handler vale **solo quando il permesso
è quello di lettura**, cioè quando è il `.View` della sua area — e a dirlo è
`PermissionCatalog.ViewOf`, che è già il posto che sa che cosa vuol dire «il View di un'area» (ci
sta anche «Edit implica View»).

### Due estensioni generiche, e perché non erano aggiramenti

1. **`CrudOptions.Name`.** Due risorse nella stessa area di permessi collidevano sul nome
   dell'operazione: `/api/categories` e `/api/content` rispondevano entrambe a `ContentList`, e il
   generatore del client teneva l'ultima letta — una risorsa che sparisce dal client **in silenzio**,
   non un errore. Il nome nel contratto e l'area dei permessi sono due cose diverse che finora
   coincidevano; adesso una risorsa può dire come si chiama.
2. **`.meta({ choices })` con etichette a runtime.** `{ value, label }` accanto ai valori nudi che
   già accettava, più la voce «nessuna scelta» che un `z.enum` opzionale aveva e un `text` con
   `choices` no. La categoria di una news è una **chiave stabile** mostrata con la **parola** che un
   coordinatore ha scritto in un'altra tabella: né un `z.enum` (l'insieme non è noto a compile time)
   né una chiave i18n (l'etichetta è un dato) potevano portarla. Non è un sesto tipo di campo, ed è
   scritta in `docs/UI-GUIDELINES.md` per chi forka.

E **due** righe in più al vocabolario delle colonne: **`col.media`**, una miniatura invece del numero
con cui una copertina è salvata, e **`col.file`**, un link per un allegato di cui la riga non conosce
il tipo — è così che la lista dei documenti mostra il file senza dover sapere se dietro c'è un PDF o
un'immagine, e una riga senza file resta vuota, che è uno stato vero. Ognuna è un `case` in
`DataList` e una riga in `columns.ts`, che è ciò che quel file dice di fare quando serve una cella
nuova.

### I test, e le due volte che non erano test

Sette di accettazione (`NewsDocumentsAndCategoriesTests`), quattro sull'handler
(`SharedForReadingTests`), uno di architettura (`NoSecondContentEntity`), nove in un browser — cinque
su `/news` e `/documents` (`public-lists.spec.ts`), di cui due sono misure, e quattro sul
back-office. Al 6 set 2026 la suite è **264 unit .NET, 122 di integrazione, 199 Vitest, 26 smoke
Playwright e 3 del giro contro l'API vera**. Tutti i test nuovi sono stati verificati rompendo la
correzione — e **due volte la verifica ha trovato un test che non lo era**:

1. **Un end-to-end respinto due volte non prova chi lo ha respinto.**
   `TemplatesAreWritableOnlyByTheirDepartment` restava verde con la regola «solo in lettura»
   cancellata, perché la scrittura di un template altrui è rifiutata **sia** dall'handler **sia**
   dall'interceptor. La rete vera è un test di unità sull'handler da solo; l'end-to-end resta perché
   la proprietà vale la pena di essere provata sul giro intero.
2. **Un test di accoppiamento con righe scelte a mano prova le righe, non l'accoppiamento.** La
   prima versione di `TheSqlAndTheInMemoryHalvesAgree` aveva quattro righe, tutte alla visibilità di
   default, e restava verde mentre una seconda metà scritta a mano dissentiva su **ogni template
   vero** — che è `Visibility.Staff`. Adesso è il prodotto cartesiano delle proprietà che l'una o
   l'altra metà potrebbe guardare.

### Due cose viste facendo la fase

- ⚠️ **Una `Label` puntava a nulla.** Il filtro pubblico aveva `htmlFor` senza un `id` sul `Select`.
  Il `Select` di Atmosphere **inoltra `id` al trigger** — misurato nel bundle, non assunto: è il
  **quinto** contratto di quella libreria che andava guardato, dopo `DarkModeToggle`, `Select` (le
  rest props sdoppiate), `SidebarContainer` e `Tabs`. Senza, il controllo non ha nome per chi legge
  con uno screen reader, e il test non riusciva a trovarlo: è così che si è visto.
- ⚠️ **I VID dei test di integrazione sono un intervallo per classe, e la collisione è muta.**
  Questa classe aveva preso 630001-630003, che `SearchEndpointTests` già usa, e dare una posizione
  del dipartimento Web a 630003 ha trasformato il *coordinatore Flight Ops* di quella suite in uno
  che raggiunge ogni dipartimento: una riga in più in un conteggio, tre classi più in là, e niente a
  che vedere con il codice sotto test. Il database è **uno solo per l'intera collection**: prima di
  scegliere dei VID si guarda `grep -n "const int.*Vid" tests/IvaoHub.IntegrationTests/*.cs`. Questa
  classe sta ora su 650xxx.

### Che cosa la fase non ha fatto, ed è giusto così

- **La copia di un template in un altro dipartimento**: il design la esclude esplicitamente finché
  qualcuno non vuole davvero divergere (§9.4). È la stessa copia profonda che esiste già.
- **Le voci di menu verso `/news` e `/documents`**: il menu è editoriale e nasce in G8. Le rotte
  esistono e si aprono per indirizzo.
- **La paginazione delle liste pubbliche**: un blocco Data risponde con al massimo cinquanta righe
  (`DataBlockScope.MaxItems`), che è il tetto che vale anche dentro una pagina. Quando la divisione
  avrà più di cinquanta news pubblicate lo si guarderà con il dato davanti, non prima.

### Debiti nuovi che G5 lascia

1. **Il vocabolario viaggia con ogni lista.** `newsList` e `documentList` fanno una query in più per
   le categorie del proprio `kind`, anche quando il blocco sta dentro una pagina e nessuno userà le
   etichette. Sono poche righe indicizzate; se un giorno pesasse, la risposta è una props e non una
   seconda strada.

⚠️ Il debito che stava qui — «un documento con slug uguale a un codice di dipartimento non è
raggiungibile» — **non esiste più**: Carmine ha deciso lo stesso giorno di togliere
`/documents/{dept}` come indirizzo, e adesso quel segmento è una slug e nient'altro. C'è un test in
un browser che apre `/documents/ed` e pretende di vedere un documento.

---

## 20. G6 di M1: il calendario guadagna la sua UI (6 settembre 2026)

Il modello c'era tutto da M0 e non è stato toccato: `cms_calendar_entries`, `CalendarEntry` con
`IOwnedByDepartment`, `IVisible`, `IAuditable` e `[PermissionArea("Calendar")]`, i permessi già in
catalogo. Mancava la UI, e mancava una cosa sola di sostanza — che **una voce proiettata da un modulo
non si modifica**.

### Il conto

| | |
|---|---|
| Tabelle nuove | **zero**; nessuna migrazione |
| Permessi nuovi | **zero**; `Calendar.View` e `Calendar.Edit` erano già in catalogo da M0 |
| Endpoint scritti a mano | **zero** (M1 resta a uno, l'upload) |
| Componenti custom | **uno**, `CalendarView` — il secondo dei quattro previsti, dopo `MediaPicker` |
| Meccanismi nuovi | **zero**; due estensioni generiche, sotto |

### Che cosa c'è adesso

- **`/staff/{dept}/calendar`**: `MapCrud` come ogni altra risorsa, con la ricetta a tre route.
- ⚠️ **Una voce con `SourceModule != "core"` è una proiezione e nessuno la scrive.** Non è un
  permesso e non c'è permesso che la sblocchi: la riga rispecchia qualcosa che appartiene a un
  modulo, e una modifica tornerebbe indietro al primo salvataggio di quella cosa. Il motore la
  rifiuta **anche a un superadmin**, ed è proprio la differenza fra questa regola e una policy.
- **`/calendar` pubblico**: mese, settimana e agenda, filtri per vista, dipartimento e tipo, tutto
  nell'indirizzo così che quello che uno sta guardando si possa mandare a qualcun altro.
- **`CalendarView`** è il componente custom, e si guadagna l'elenco chiuso con il criterio scritto
  lì: **due schermate lo montano**, `/calendar` e il blocco `calendar` dentro una pagina. Il
  componente provvisorio che G4 aveva scritto apposta per sparire è sparito.
- **Il blocco `calendar` guadagna `view`**, additivo e con `.default('agenda')`, esattamente come il
  design §1.2 aveva previsto: in G4 la vista era una sola e un select con una voce è un comando che
  non fa niente.
- Ogni ora è **in UTC e nel fuso della divisione**, mai una al posto dell'altra, e il fuso arriva da
  `/api/me` e mai da una costante (piano §9.5).

### Due estensioni generiche, e perché non erano aggiramenti

1. **`CrudOptions.ReadOnlyRows`** — un predicato che dice quali righe di una risorsa nessuno scrive.
   ⚠️ Il piano diceva di impedire la scrittura con `ExtraWritePolicy`, e **non si può**: quello
   restituisce il *nome di un permesso*, e non esiste un permesso che voglia dire «nessuno», perché
   un superadmin li ha tutti. Il punto che il piano stava facendo — «non un handler nuovo» — è
   rispettato in pieno: la regola sta dentro il motore, dove sta già quella del dipartimento.
   È il gemello di `SharedForReading` di G5: una dice quali righe tutti **leggono**, l'altra quali
   righe nessuno **scrive**.
2. **Una finestra esplicita per il provider del calendario**, `from` e `to` accanto a `range`.
   Una griglia che mostra settembre mostra settembre, non «i prossimi trentun giorni». ⚠️ Le due
   props **non stanno nello schema zod del blocco**, ed è deliberato: lo schema è ciò che un
   redattore *salva*, e un corpo che inchiodasse una pagina a un mese sarebbe scaduto il giorno dopo
   la pubblicazione. Quello è ciò che si **chiede**, e a chiederlo è una schermata.

### Una cosa che il modello ha detto solo quando gliel'hanno chiesta

⚠️ **`(source_module, source_id)` è unico**, e nessuno aveva mai creato una voce scritta dallo staff:
la prima passa, la seconda va a sbattere sull'indice perché sono entrambe `("core", "")`. La risposta
è un identificativo opaco generato alla creazione — `staff:{guid}` — come il nome su disco di un
file della libreria: nessuno lo legge tranne l'indice. Nessuna migrazione, nessun indice toccato.
C'è un test che crea **due** voci nello stesso dipartimento, e senza la correzione la seconda dà 500.

### Che cosa la fase non ha fatto, ed è giusto così

- **`RowVersion` sul calendario**: la tabella non ha un token di concorrenza e il modello di M0 non
  si tocca in questa fase. Due membri che modificano la stessa voce insieme finiscono con il secondo
  salvataggio che vince. È scritto nello schema del form e nel DTO invece di essere lasciato
  scoprire; se un giorno serve, è una colonna additiva e una decisione.
- **Le notifiche del calendario e il feed iCal**: restano dove il piano li ha messi, M6 (§15.9).
- **Le voci `department` sul pubblico**: non compaiono, e a tenerle fuori è il query filter, non una
  riga in questa fase.

### I test

Quattro di accettazione (`CalendarEndToEndTests`), sette Vitest su `CalendarView` e sulle due
funzioni che decidono i giorni e la finestra, tre in un browser con una misura sulla griglia, due
sulla schermata dello staff. Al 6 set 2026 la suite è **264 unit .NET, 126 di integrazione, 206
Vitest, 31 smoke Playwright e 3 del giro contro l'API vera**.

Tutti verificati rompendo la correzione, e **una rottura ha trovato un difetto vero**: la finestra
che la schermata chiedeva era calcolata dal giorno di ancoraggio invece che dai quadrati che la
griglia disegna, quindi una griglia del mese aperta il 28 chiedeva l'ultima settimana e disegnava
vuote le prime tre. Adesso **una sola funzione** decide i quadrati e la finestra (`calendarDays` e
`calendarWindow`), e il test le confronta su tre giorni diversi del mese.

⚠️ La fixture del fuso è **Asia/Tokyo** nei Vitest e **Europe/Rome** negli e2e, mai UTC: con
`timezone: "UTC"` le due righe di ogni data coincidono e una schermata che mostra UTC due volte
passa inosservata. È il terzo dei tre falsi allarmi di §13, e questa è la rete che lo prenderebbe.

---

## 21. G7 di M1: i contatti, e l'unica cosa che manda mail (6 settembre 2026)

La fase ha costruito quello che il design §5 chiedeva, e ha trovato **due cose che nessun documento
poteva sapere**. Tutte e due sono note di decisione chiuse da Carmine prima di scrivere codice, e
tutte e due riguardano la spina dorsale: `decisions/2026-09-06-indirizzo-di-un-destinatario.md` e
`decisions/2026-09-06-una-riga-scritta-da-fuori.md`.

### Il conto

| | |
|---|---|
| Tabelle nuove | **tre**: `cms_contact_messages`, `hub_notifications`, `hub_notification_preferences`; una migrazione, additiva, più la colonna `hub_users.email` |
| Permessi nuovi | **due**: `Contacts.View`, `Contacts.Edit`; nessun handler |
| Endpoint scritti a mano | **tre** — M1 passa da uno a quattro, ed è il primo scostamento dalla previsione di design §12 (sotto il perché) |
| Componenti custom | **uno**, `ContactForm` — il terzo dei quattro previsti, dopo `MediaPicker` e `CalendarView` |
| Meccanismi nuovi | **uno**, `ISubmittedByMembers`, deciso con una nota |
| Dipendenze nuove | **una**, MailKit 4.17.0 — già nel piano §3 dal primo giorno, mai pinnata prima |

⚠️ **I tre endpoint a mano, uno per uno**, perché §E dice che il secondo è «un evento da riportare»:

1. **`POST /api/contacts`** — la stessa forma che G1 ha scelto per l'upload (`MapCreate = false` più
   un `POST` sul gruppo, mai un secondo indirizzo per creare la stessa cosa). Nessuna delle tre cose
   che fa è una create del motore CRUD: la policy è `SignedIn` e non il permesso di scrittura
   dell'area, il mittente è la sessione, e dopo il salvataggio parte un intento.
2. **`GET /api/me/notifications`** e 3. **`PUT /api/me/notifications`** — sono la famiglia di
   `/api/me/locale`, non risorse del back-office: un'impostazione di chi la chiede, senza
   dipartimento a cui applicarla e senza lista da paginare. Il motore in modalità globale avrebbe
   una policy sola, cioè chiunque sui dati di chiunque.

Chi tirerà le somme in G12 confronti questa riga con la previsione (6 tabelle / 3 aree di permessi /
5 estensioni al generatore / 4 componenti custom / **1** endpoint a mano): tabelle e componenti sono
in linea, gli endpoint a mano no, e il motivo è che M1 non aveva previsto le **impostazioni di un
membro** come categoria — ne esisteva già una, la lingua, scritta in M0.

### Che cosa c'è adesso

- **`cms_contact_messages`**, `OwnerDepartment` = il dipartimento destinatario, così la coda, il
  filtro di dipartimento e il controllo riga per riga escono tutti da ciò che esiste.
  ⚠️ **Niente `FromVid`, niente `HandledBy`**: il mittente è `CreatedBy` e chi ha mosso lo stato è
  `UpdatedBy`, che li scrive l'interceptor. Il design li elencava; sono l'audit scritto a mano.
- ⚠️ **`ISubmittedByMembers`**, il terzo della famiglia dopo `ISharedForReading` (allarga la lettura)
  e `ReadOnlyRows` (restringe la scrittura): allarga **la sola creazione**, e solo per i tipi che la
  dichiarano. Serviva perché `EnsureWriteIsAllowed` chiede `<Area>.Edit` sul dipartimento della riga
  a chiunque sia autenticato — cioè rifiuta esattamente il mittente di un messaggio, che per
  definizione non fa parte del dipartimento a cui scrive. Togliere quelle tre righe fa fallire
  **nove** test su undici: è stato misurato.
- ⚠️ **Il payload del back-office porta solo lo stato.** «Dettaglio in sola lettura» non è una
  schermata gentile, è un tipo con un campo: non esiste permesso — nemmeno un superadmin — che possa
  riscrivere il messaggio di qualcun altro, perché non c'è niente da applicare. Il test manda
  `subject` e `body` nel JSON e verifica che la riga non cambi.
- **La coda**: `INotificationService.QueueAsync(NotificationIntent)`, una riga di `hub_notifications`
  per destinatario (un retry è per indirizzo), e un job Quartz al minuto che la svuota con tre
  tentativi e poi si arrende. Non è un bus di eventi: nessuno si iscrive, e l'unica cosa che legge
  quella tabella è il job.
- ⚠️ **Un destinatario è una persona o una casella.** `Member(vid)` risolve indirizzo, lingua e
  preferenza quando l'intento entra in coda; `Mailbox(address)` è un indirizzo che non appartiene a
  nessuno — `division.json` → `departmentMailboxes`, facoltativa — e legge nella lingua della
  divisione, perché una casella non ne ha una.
- ⚠️ **`hub_users.email` esiste, e prima non esisteva apposta.** `IvaoUserProfileReader` scartava
  `email` con un commento che lo diceva; lo scope era già chiesto al login. Adesso si legge, si tiene
  per la coda **e per nient'altro**, e a dirlo è un test di architettura, `NoDtoCarriesAnEmailAddress`,
  che guarda ogni DTO e ogni `Bootstrap*`. La staff directory di G9 nasce già dentro quella rete.
- **I template sono file di lingua**: `locales/{it,en}/mail.json`, namespace `mail`, risolto da
  `LocaleCatalog` nella lingua **del destinatario** — quella salvata sulla riga della coda, decisa
  quando l'intento è entrato, non quando la mail parte.
- **SMTP sta in un file solo**, `MailSender.cs`, e `NoSmtpOutsideTheNotificationService` lo fissa
  cercando `MailKit`, `MimeKit` e `SmtpClient` su tutto `src/`.
- **Senza server di posta non si rompe niente**: la coda si riempie, il job scrive `skipped` in
  `hub_jobs_log`, e le righe partono al primo giro dopo che qualcuno configura un server.
- **Le schermate**: `/contact` sotto `_member` (il form è per chi ha fatto l'accesso: niente captcha,
  niente mittente da verificare), `/staff/{dept}/contacts` con la lista generata e il dettaglio, una
  voce di sidebar, e **un interruttore** su `/me` — uno, perché i tipi di notifica sono uno.

### Due nomi che il codice ha corretto ai documenti

1. **`Contacts.Manage` → `Contacts.Edit`.** Con `.Manage` la riga non sarebbe scrivibile da nessuno:
   la guardia dell'interceptor chiede `<Area>.Edit`, `MapCrud` deriva lo stesso nome, e
   `CorePermissions` dichiara la regola per esteso. Design M1 §10.1 è corretto in v1.8.
2. **`OutgoingMail.Text` e non `Body`.** `NoSecondContentEntity` cerca entità con un `Body` di tipo
   stringa, perché è così che si vedrebbe una seconda entità con un documento a blocchi. La prosa di
   una mail non è un documento: il record si chiama `Text`, e la famiglia `Contact*` è nominata nel
   test come l'unica eccezione, così resta una decisione e non una coincidenza di ortografia.

### I test, e due rotture che hanno insegnato qualcosa

Undici di accettazione (`ContactsAndNotificationsTests`), due unit sui template (`mail.json` esiste
in ogni lingua; un segnaposto che i dati non hanno resta **visibile**), uno di architettura per
l'SMTP, uno per gli indirizzi nei DTO, e `ForkabilityXxDivision` esteso alle mail. Al 6 set 2026 la
suite è **269 unit .NET, 138 di integrazione, 206 Vitest**.

Ogni correzione è stata rotta apposta per guardare il test fallire, e due rotture hanno insegnato
qualcosa:

- ⚠️ **Una rottura che non compila non è una rottura.** `if (false)` ha dato `CS0162` (qui gli avvisi
  sono errori), la build è fallita e i test sono girati sui binari di prima — **tutti verdi**. Se
  quella riga fosse stata l'unica prova, il meccanismo sarebbe stato dichiarato coperto senza
  esserlo. Rompere significa cambiare il *comportamento*, non spegnere il codice.
- ⚠️ **La prima versione di `TheMailsOfAForkNameNobodyElsesDivision` non provava niente in più.**
  Renderizzava i template con dati inventati, cioè ricontrollava il file che il test accanto già
  legge. Adesso un membro di XX **scrive davvero** a un dipartimento di XX e la coda viene svuotata:
  mettere `it.ivao.aero` a mano nell'URL della mail fa fallire quel test **e nessun altro**.

### La prova a mano che il piano chiedeva

Fatta, e non è un dettaglio: è l'unico pezzo che i test non toccano, perché `SmtpMailSender` parla
davvero con un server. Il banco e2e pubblicato, avviato con `Smtp__Host=localhost Smtp__Port=1025`,
un messaggio dal form, e **la mail è arrivata in Mailpit in inglese** mentre la divisione di quel
banco ha l'italiano come default — cioè nella lingua del destinatario, che è esattamente il criterio.
L'unico ritocco necessario è stato dare un indirizzo all'utente del banco: il login e2e non ne
inventa uno, apposta.

---

## 22. G8 di M1: il sito pubblico esiste, e non lo disegna il codice (7 settembre 2026)

La fase che risponde alla domanda di M1. Il menu è una tabella, le cinque pagine di sistema sono
righe seminate da template, ogni dipartimento apre il proprio spazio su una dashboard a blocchi, e
un grant fa finalmente raggiungere il dipartimento su cui è dato.

### Il conto

| | |
|---|---|
| Tabelle nuove | **una**: `cms_menu_items`; una migrazione, additiva, più un valore in fondo a `ContentKind` |
| Permessi nuovi | **due**: `Menu.View`, `Menu.Edit`; nessun handler |
| Endpoint scritti a mano | **due**: `sitemap.xml` e `robots.txt` — M1 passa da quattro a sei |
| Componenti custom | **zero** dell'elenco chiuso. `PageMetadata` è un componente e non disegna niente: rende `<title>` e i `<meta>`, che React 19 solleva nel `head` |
| Meccanismi nuovi | **zero**. Un seeder che ne assorbe un altro, un contratto che guadagna due campi, una costante |
| Dipendenze nuove | **nessuna** |

⚠️ **I due endpoint a mano, e perché sono due e non zero**: `sitemap.xml` e `robots.txt` non sono
risorse del back office e non c'è motore che le possa produrre — sono due file che un crawler
chiede, uno dei quali è una query e l'altro cinque righe di testo. Chi tira le somme in G12 li conti
come tali: la previsione di design §12 diceva **uno** in tutta M1, e siamo a sei (tre di G7, due di
G8, uno di M0).

### Che cosa c'è adesso

- **`cms_menu_items`**, esposta da `MapCrud` e da nient'altro. ⚠️ **Il payload non porta il
  dipartimento**: ogni riga appartiene a chi possiede il sito, dichiarato una volta sull'entità, e
  questa è tutta l'autorizzazione della risorsa — il filtro di dipartimento della lista e l'unico
  authorization handler rispondono senza che ci sia una riga scritta per loro. Un coordinatore di un
  altro dipartimento tiene `Menu.Edit` sul proprio, dove non esiste nessuna riga di menu.
- ⚠️ **Chi possiede il sito è una costante sola**, `SiteOwnership.Department`: menu, template di
  sistema e pagine seminate. La SPA **non la ripete** — arriva in `/api/me` come
  `division.siteDepartment` — perché un `staff.wd.menu.tsx` sarebbe un codice di dipartimento
  scritto dentro un client che non ha diritto di conoscerne uno (CLAUDE.md §2 e §3).
- **`/api/me` compone editoriale ∪ moduli**, ordinato. `NavItem` porta `Key` **oppure** `Label`, mai
  una stringa che a volte è una chiave, più i propri figli; la navigazione guadagna lo scope
  `footer`. ⚠️ **La voce fissa `nav.home` non c'è più**: la home è una riga seminata, o il menu non
  sarebbe dati. Una voce editoriale che nomina l'indirizzo di un modulo **vince**, così `/atc` non
  compare due volte il giorno che qualcuno lo mette in menu a mano.
- **`ContentSeeder` ha assorbito `ContentTemplateSeeder`**: la stessa chiave in
  `hub_division_settings`, la stessa risoluzione dei `$t`, lo stesso envelope opaco, applicati a due
  cartelle. Un seed di pagina **nasce da un template** (è ciò che permette a G11 di dire che il
  template è cambiato), può portare un corpo proprio, e quando non lo porta è una **copia** di
  quello del template — riidentificata dalla stessa `TemplateCopy` che usa «nuovo da template».
- **Le pagine seminate sono pubblicate dal seeder.** Non è un dettaglio: il query filter ferma una
  bozza per il visitatore *e* per il dipartimento, quindi una dashboard non pubblicata non la
  vedrebbe nemmeno chi la possiede.
- **Nove dashboard**, `kind = Dashboard`, slug il codice del dipartimento, `Visibility.Department`,
  nate dal template `dashboard`. ⚠️ Il template **non** porta blocchi filtrati per dipartimento: è
  uno e le righe sono nove. La base e i tool li dà il template, il filtro lo mette il dipartimento
  nell'editor.
- ⚠️ **Un grant adesso porta il dipartimento**, non solo il permesso: `HubClaims.BuildIdentity`
  scrive un claim `dept` per ogni dipartimento nominato da un permesso la cui sorgente è un grant —
  e **non** per «qualunque permesso con un dipartimento», perché l'espansione di un deny fabbrica
  dipartimenti espliciti a partire da un permesso globale. Allarga la **visibilità**: chi riceve un
  grant qualunque su un dipartimento ne vede tutte le righe `Department`, dashboard compresa.
- **Le schermate**: `/staff/{dept}/menu` (il trio di route, guardato al dipartimento che possiede il
  sito), `/staff/{dept}` che disegna la dashboard pubblicata con il renderer di sempre,
  `/staff/{dept}/dashboard/{id}` per modificarla — una rotta sua e non `/content/{id}`, perché
  quella schermata porta `kind = Page` nel payload e salvare lì trasformerebbe la dashboard in una
  pagina. `/staff` apre sulla dashboard e non più sulla prima lista della sidebar.
- **Il pubblico**: la home disegna la riga `home` pubblicata, `/start`, `/pilots` e `/about` cadono
  già dalla rotta `$slug` che esiste, e `/atc` è la pagina di sistema più le card che il modulo
  registra sotto. `sitemap.xml` e `robots.txt` li serve il server, esclusi dal fallback della SPA;
  `<title>`, la description e gli `og:` li rende la pagina che li conosce.

### Tre difetti trovati facendo, e nessuno era nel codice di questa fase

1. ⚠️ **Nessun form del back office poteva creare una riga contro l'API vera.** Mandavano
   `rowVersion: ""`, che non è una data: il server rifiutava il payload prima di qualunque
   validatore, con un 400 che nessuno aveva mai visto. Vale per link, categorie, pagine e menu, e
   viene da M0. È sopravvissuto perché il giro di G0 crea **da template**, che è un altro endpoint,
   e gli smoke del back office **stubbano l'API**: G8 è la prima fase che ha spedito un form vuoto
   al banco. Un solo valore adesso, `shared/api/rowVersion.ts`. È la lezione di §11 in una forma
   nuova: quello che nessuno monta, nessuno prova.
2. **Un intervallo di VID già occupato.** 660001-660004 appartiene a `CalendarEndToEndTests`, il cui
   660002 è un **superadmin**: «un coordinatore di un altro dipartimento» era qualcuno che può
   tutto, e due rifiuti smettevano di essere rifiuti. In isolamento passava tutto; solo la suite
   intera lo mostrava.
3. **Un test che sporcava la tabella.** Quello che prova che una voce editoriale nasconde quella di
   un modulo lasciava la riga in tabella, e da lì in poi `/atc` non aveva più la voce del modulo per
   nessuna classe successiva. Una riga che un test scrive per cambiare una risposta è una riga che
   quel test si riprende.

### I test, e le rotture che li hanno verificati

Dodici di accettazione (`SiteMenuAndDashboardTests`), `ForkabilityXxDivision` esteso alle pagine
seminate e al menu, quattro nel banco e2e (`e2e/full/menu.spec.ts`) e tre smoke nuovi, uno dei quali
è una **misura**. Al 7 set 2026 la suite è **294 unit .NET, 150 di integrazione, 207 Vitest, 33
smoke, 7 sul banco**.

Ogni correzione è stata rotta apposta per guardare il test fallire: i dipartimenti dei grant tolti da
`BuildIdentity`, il proprietario di una riga di menu spostato, il seed applicato due volte, e la
composizione del menu in `/api/me` — quest'ultima con il banco ripubblicato, che è l'unico modo di
provare «senza ricompilare» dicendolo davvero.

⚠️ **E una misura che non misurava.** La prima versione dell'asserzione sulla colonna di lettura
della home diceva «larga fra 500 e 1100 pixel», ed è **passata con il layout rotto apposta**: una
sezione `default` è larga 992 dentro una cornice da 1152, una `full` è larga 1088, e la soglia le
accettava entrambe. Adesso confronta la colonna con la cornice che la contiene — 100 pixel di
margine per lato — ed è stata scritta **misurando i due stati**, non indovinandoli. È la stessa
lezione di §13 con il difetto spostato di un passo: non basta misurare, bisogna misurare qualcosa
che distingua.

### Che cosa la fase non ha fatto, ed è giusto così

- **Il Lorem non è contenuto vero.** Ricopiare `/about` e `/start` dal sito Blazor è G12, ed è lì
  apposta: è il collaudo dell'editor, non lavoro di riempimento.
- **Nessun prerender.** `<title>` e `og:` li rende il browser, quindi un crawler che non esegue
  JavaScript legge quelli di `index.html`. È la decisione del piano §16.11, e la metà che un
  crawler vede sempre è `sitemap.xml`, che la serve il server.
- **Le differenze rispetto al template** restano G11: il seed scrive `TemplateId`, che è il dato di
  cui quella fase ha bisogno, e nient'altro.

### Debiti nuovi che G8 lascia

- **Due `<h1>` su una pagina pubblica.** Il titolo della riga è reso `sr-only` per le pagine il cui
  corpo non ha un'intestazione, e una pagina che ce l'ha ne ha due. Non è rotto — è ridondante per
  chi legge con uno screen reader — e la correzione (il renderer che si accorge di un `heading` di
  livello 1 nel corpo) tocca un meccanismo di M0: si guarda nel giro visivo di G12.
- **Le opzioni tradotte di `visibility` sono scritte quattro volte** (`content`, `links`, `menu`, e
  la chiave `visibility` di primo livello). È la convenzione che le tre schermate precedenti hanno
  già seguito, e cambiarla è un lavoro suo: si dichiara qui perché la quarta copia l'ha aggiunta
  questa fase.
- **Il `path` di una voce di menu non è validato contro le rotte che esistono.** Un refuso porta a
  una pagina non trovata, che è esattamente ciò che succede scrivendo un indirizzo sbagliato in una
  pagina; farlo verificare vorrebbe dire insegnare al server l'albero delle rotte della SPA.

---

## 23. G9 di M1: la striscia, e una directory che c'era già (7 settembre 2026)

La fase più corta di M1, e non perché il perimetro fosse piccolo: **tre task su quattro erano già
fatti da altre fasi**, e accorgersene è stata metà del lavoro.

### Il conto

| | |
|---|---|
| Tabelle nuove | **zero** |
| Permessi nuovi | **zero** |
| Endpoint scritti a mano | **zero** — la striscia interroga il blocco `networkStats` di G4 |
| Componenti custom | **uno**, `LiveStatusStrip` — il terzo dei quattro previsti |
| Meccanismi nuovi | **zero**. Uno slot `banner` in `Shell`, che è un prop |
| Dipendenze nuove | **nessuna** |

### Che cosa c'era già, e chi l'aveva costruito

Vale la pena scriverlo perché è il caso migliore che questo progetto abbia prodotto finora: **una
fase che apre e trova il suo lavoro quasi fatto da meccanismi generici messi lì prima.**

- Il provider `staffList` è di **G4**: raggruppa per dipartimento e poi per FIR, ordina per anzianità
  e poi per cognome, espone `vid`, nome, posizione e livello — e nient'altro.
- La sezione di `/about` che lo monta l'ha seminata **G8**, insieme alla pagina.
- La riga onesta su chi non compare c'era già nel blocco, in `blocks.staffList.rosterNote`.

Quindi G9 ha aggiunto **una** cosa e ha scritto i **test** delle tre promesse. È esattamente ciò che
§A.5 del piano intende dicendo che i criteri di accettazione sono test: valgono anche — soprattutto —
quando il codice li precede, perché è allora che nessuno li ha ancora verificati.

### La striscia

- **Nessun endpoint suo**: `/api/blocks/data/networkStats` è anonimo, sempre vivo ed è già la
  risposta a quella domanda. Un indirizzo in più sarebbe stato un secondo modo di chiedere una cosa.
- **Polling al minuto**, lo stesso minuto che il server tiene in cache: un lettore che arriva sul
  minuto non costa niente. Non un socket — il proxy davanti non è il posto per una connessione per
  lettore (piano §16, §14).
- ⚠️ **Quando la rete non risponde non disegna niente.** `updatedAt` nullo vuol dire «non ho potuto
  chiedere», che non è «non c'è nessuno»: quattro zeri in cima a ogni pagina pubblica sarebbero il
  sito che risponde a una domanda che non ha fatto.
- ⚠️ **Sta nello slot `banner` di `Shell`**, fra header e contenuto. Dentro la colonna di lettura è
  larga **1120 px in una finestra da 1280** — misurato — e non è una banda. Lo slot è un prop, non un
  meccanismo: la cornice decide *dove*, il layout decide *che cosa*, e il back office non ne chiede.

### Due cose trovate facendo

1. ⚠️ **La garanzia sul roster è più forte di come il design la descriveva.** «Chi non ha mai fatto
   login non compare» non è il provider che lo esclude: `hub_user_staff_positions.vid` è una **chiave
   esterna** verso `hub_users`, quindi la posizione di chi non ha mai aperto l'hub non è scrivibile.
   La directory non può mostrarlo perché non può esistere. Il test l'ha scoperto **provando a
   costruire il caso contrario** e prendendosi un `DbUpdateException`, e adesso asserisce il rifiuto:
   è una regola dello schema, non il risultato di una query.
2. **Una rottura che non compila non è una rottura**, di nuovo (§21). Il primo tentativo di rompere
   il provider ha aggiunto un `return` anticipato: `CS0162`, codice irraggiungibile, che qui è un
   errore. Rompere vuol dire cambiare il **comportamento** — la seconda versione ha filtrato via le
   posizioni riconosciute, ed è quella che ha fatto diventare rosso il test del banco.

### I test

Sei unit (`LiveStatusTests`: quattro modi di essere giù, la cache del fallimento, la lettura senza
token), tre di integrazione (`StaffDirectoryTests`), due Vitest sulla striscia, uno rinominato al
criterio che già soddisfaceva (`StaffDirectorySaysWhoIsMissing`), tre smoke di cui **uno è una
misura**, e uno sul banco che legge `/about` da visitatore anonimo — dove le tre metà costruite da
tre fasi diverse si incontrano per la prima volta.

Al 7 set 2026 la suite è **300 unit .NET, 153 di integrazione, 209 Vitest, 36 smoke, 8 sul banco**.

Ogni correzione è stata rotta apposta: il client che rilancia invece di degradare, l'ordinamento per
cognome invece che per anzianità, la striscia che disegna anche senza risposta, la striscia rimessa
dentro la colonna di lettura, e il provider che non torna nessun gruppo.

### Debiti nuovi che G9 lascia

- **Nessuno.** L'unico appunto è che il banco e2e porta ancora una voce di menu lasciata da un giro
  fallito di G8: è dato del banco, che per progetto non si ripulisce, e si è fatto notare solo
  perché un selettore troppo largo l'ha pescata.

---

## 24. G10 di M1: la ricerca, e il debito n.10 chiuso (7 settembre 2026)

`GET /api/search` esisteva da F8. Quello che mancava erano **le tre risposte** che M0 aveva lasciato
aperte, e una schermata da cui farle.

### Il conto

| | |
|---|---|
| Tabelle nuove | **zero**; una colonna su `cms_search_index` (`updated_at`), migrazione additiva con riempimento |
| Permessi nuovi | **zero**: la ricerca è pubblica e il query filter fa il resto |
| Endpoint scritti a mano | **zero**: `/api/search` esiste da F8 e ha cambiato forma, non numero |
| Componenti custom | **zero**. La palette è `Command` di Atmosphere, assemblata dai suoi pezzi |
| Meccanismi nuovi | **zero**. Una funzione in `shared/search/`, un envelope che ne contiene un altro |
| Dipendenze nuove | **nessuna** |

### Le tre risposte

1. **Rilevanza.** Le righe tornano più pertinenti per prime e, a parità, la più recente per prima.
   ⚠️ Il punteggio **non** è una colonna selezionata: EF Core non sa filtrare su un membro di una
   proiezione — `Select(new { entry, score })` seguito da `Where(x => x.Score > 0)` risponde «the
   LINQ expression could not be translated», misurato contro MariaDB vera. Quello che si scrive una
   volta è quindi **l'espressione**, usata come ordinamento e, maggiore di zero, come filtro. In SQL
   il risultato è lo stesso; il sorgente ha una `MATCH` sola, che era la ragione della regola.
2. **Evidenziazione.** Lo `snippet` esce dal server come **testo**, tagliato intorno alla prima
   occorrenza sulle sole righe della pagina; a marcare è il browser, l'unico che sa in che lingua sta
   guardando. `<mark>` e non un colore: è un significato, e uno screen reader deve sentirlo.
   La piegatura che decide che due parole sono la stessa parola (maiuscole, accenti) è scritta una
   volta e la usano l'evidenziazione **e** la palette.
3. **Parole corte.** InnoDB non indicizza sotto le tre lettere e su una MariaDB condivisa quella
   variabile non è nostra. Quindi la risposta lo **dice**, con una chiave i18n dentro un envelope che
   **contiene** `PagedResult` invece di reinventarlo. ⚠️ Una parola lunga accanto a due corte non è
   quel caso — MariaDB ignora le corte e risponde sul resto — e c'è un test che lo fissa.

### Che cosa c'è adesso

- **`cms_search_index.updated_at`**, scritto dal projection writer a ogni scrittura. ⚠️ È la data
  della **proiezione** e non della sorgente, deliberatamente: una proiezione porta ciò che ogni riga
  proiettabile può promettere, e «quando sei stata pubblicata» non lo è — un link non ha una data di
  pubblicazione, una pagina ne ha una, una voce di calendario due. Insegnarlo a `SearchProjection`
  avrebbe fatto rispondere ogni modulo a una domanda che quasi nessuno può rispondere, per un
  criterio di parità.
- **`/search?q=`**, pubblica, con lo stato nell'indirizzo: un risultato che vale la pena mostrare a
  qualcuno è un link che vale la pena mandargli.
- **La palette ⌘K**, ovunque nel back office. ⚠️ È `Command` di Atmosphere **assemblata dai suoi
  pezzi**: `CommandDialog` inoltra le proprie props al *dialogo* e non al comando, quindi
  `shouldFilter` non lo raggiunge, e ne avvolge già uno. Con il filtro acceso `cmdk` filtra una
  seconda volta la risposta del server e butta via ogni risultato trovato nel **corpo** di una pagina
  invece che nel titolo — cioè esattamente quelli per cui esiste lo snippet.
- **`staffDestinations`**: le schermate che la sidebar disegna e quelle che la palette offre sono
  **una lista sola**. Due liste è come una schermata finisce raggiungibile da una e non dall'altra.
- **Il modo di arrivarci**: un bottone nella cornice, accanto alla lingua e al tema. Non nel menu,
  perché il menu è ciò che scrive lo staff e una casella di ricerca non deve dipendere dal fatto che
  qualcuno si ricordi di aggiungerla.
- **`/search` è fra i `Disallow` di `robots.txt`**: è pubblica e risponde, ma ogni query è un altro
  indirizzo che risponde, quindi un crawler lasciato lì cammina su un insieme senza fine.

### Due contratti di libreria misurati, e un test che non provava niente

- **EF Core** sul filtro di una proiezione (sopra). Provato eseguendo, non deducendo.
- **`cmdk`** sul filtro dei propri item, letto nel bundle di Atmosphere. C'è un test del banco smoke
  che monta un risultato la cui parola sta **solo** nello snippet: rimettendo `shouldFilter` a `true`
  quel test diventa rosso.
- ⚠️ **E il primo test dello snippet non provava niente**: il testo della fixture era **più corto di
  uno snippet**, quindi tornava intero comunque e il test passava anche ignorando del tutto la query
  — verificato rompendo la funzione e vedendolo restare verde. Adesso il testo è lungo, l'estratto
  comincia con un'ellissi, e la rottura lo fa fallire. È la stessa lezione della misura di G9: non
  basta scrivere l'asserzione, deve distinguere i due stati.

### I test

Tre di accettazione (`SearchOrdersByRelevanceThenRecency`, `SearchReturnsSnippetPerLocale`,
`SearchTellsWhenEveryTermIsTooShort`) accanto ai tre di F8 che restano verdi, nove Vitest
dell'evidenziazione (accenti, maiuscole, termini che si sovrappongono, testo ricostruito identico),
due sulla palette, e sei smoke fra schermata pubblica e palette.

Al 7 set 2026 la suite è **300 unit .NET, 156 di integrazione, 220 Vitest, 42 smoke, 8 sul banco**.

### Debiti nuovi che G10 lascia

- **La paginazione della ricerca non ha una seconda pagina da cliccare.** L'API pagina e la rotta
  porta `page` nell'indirizzo; la schermata mostra i primi venti e non offre il ventunesimo. Con il
  contenuto che una divisione ha oggi non si vede; è una riga di `Pagination` quando si vedrà.
- **Le righe già indicizzate hanno tutte la stessa data** fino al primo salvataggio successivo: il
  riempimento della migrazione le timbra tutte al momento del deploy, che è vero («per questo indice
  sono vecchie uguali») ma rende il criterio di parità inutile fra loro finché non si toccano.

---

## 25. G11 di M1: l'editor dice quando il template si è mosso (7 settembre 2026)

Le rifiniture, dopo che l'editor è stato usato davvero. La regola non si è mossa di un millimetro —
**un template non riscrive mai una pagina da sé** — e quello che mancava era che l'editor lo
**dicesse**: una sezione aggiunta a un template era invisibile a chiunque lavorasse sulle pagine
nate da quello.

### Il conto

| | |
|---|---|
| Tabelle nuove | **zero**; nessuna migrazione, nessuna colonna |
| Permessi nuovi | **zero**: `Content.ManageTemplates` esiste da M0, qui si legge e si nomina |
| Endpoint scritti a mano | **zero**: la diff è tutta nel client, sui due corpi che già scarica |
| Componenti custom | **zero fuori dall'editor**. `TemplateDifferences`, `LockedByTemplate` e `PreviewFrame` vivono in `features/content/` e non sono pezzi condivisi |
| Meccanismi nuovi | **zero**. `templateDiff` sta accanto a `templateRules`, e `reorderSections`/`reorderBlocks` sono due funzioni in `body.ts` come le dieci che c'erano |
| Dipendenze nuove | **una**, `@dnd-kit/core` + `sortable` + `utilities`, chiesta per nome dal design §9.3 |

### Il terzo stato non si poteva calcolare, e il design è stato corretto

Design §9.1 chiedeva tre stati, e il terzo era «sezione **cambiata** nei vincoli». Non è
calcolabile: confrontare i vincoli di prima con quelli di adesso vuol dire sapere quali fossero, e
**non lo sa nessuno** — le restrizioni non viaggiano nella copia, apposta, perché una pagina che le
portasse potrebbe togliersele (`TemplateCopy.Reidentify`).

Quello che si può chiedere onestamente è **se la pagina soddisfa ancora il vincolo com'è oggi**, e le
due risposte concrete sono un blocco di un tipo che `allowedBlocks` non permette più, e una sezione
`locked` la cui copia non è più quella del template — che può succedere solo se il template si è
mosso, visto che una sezione bloccata nell'editor non si ristruttura.

⚠️ E per quel terzo stato **non c'è nessun «allinea»**: applicarlo vorrebbe dire cancellare blocchi
che qualcuno ha scritto, che è esattamente il pulsante premuto per sbaglio contro cui §9.1 mette in
guardia. L'editor dice che cosa non torna e lascia decidere. Nemmeno un pulsante disabilitato, che è
la stessa trappola con la faccia gentile — e c'è un test che conta i pulsanti.

### Che cosa c'è adesso

- **`features/content/templateDiff.ts`**: una funzione pura, `templateDiff(page, template)`, che
  torna `added` / `removed` / `changed`, e `applyDifference`, che ne applica **una**. Il tipo
  `AlignableDifference` esclude `changed` a livello di tipi: «non si allinea» non è una convenzione,
  è un errore di compilazione.
- Una sezione annidata il cui padre manca anche lui si riporta **una volta sola**, sul padre.
- **Nessun template non è un template vuoto**: senza template non ci sono differenze. La prima
  versione invece proponeva di cancellare ogni sezione della pagina, e il test l'ha preso al primo
  giro — succede a chi ha un template che non è ancora arrivato, o che non può aprire (§9.4).
- **La riga della sezione `locked`** dice quale template la fissa e chi può cambiarlo: il permesso
  per nome e il dipartimento su cui è tenuto, oppure «puoi farlo tu» a chi ce l'ha. Il commento in
  `BlockProperties` che prometteva «la riga qui sopra» adesso non mente più.
- **dnd-kit sta sopra le frecce, non al loro posto.** Si trascina per una maniglia — non per la riga,
  che è fatta di pulsanti — e una sezione `locked` non ha né maniglia né frecce.
- **`PreviewFrame`**: tre larghezze, `max-width` sul renderer del sito. Non è un emulatore.

### I test

- `templateDiff.test.ts`, dodici casi, di cui il primo è
  `TemplateDiffDetectsAddedRemovedAndChanged` e quattro sono su «una differenza alla volta».
- `SectionTree.test.tsx`, quattro casi, il cuore dei quali arriva ai pulsanti **tabulando** e preme
  Invio. Deliberatamente non `focus()`: la domanda è se una persona che si muove con la tastiera ci
  **arriva**. Verificato rompendolo due volte — frecce trasformate in `span role="button"` (i due
  test da tastiera rossi, quelli che contano verdi: la prova che è l'asserzione giusta a lavorare) e
  frecce tolte del tutto (tre rossi).
- `TemplatePanel.test.tsx`, cinque casi, fra cui quello che conta i pulsanti; verificato mettendo un
  terzo pulsante disabilitato, che lo fa fallire.
- `e2e/full/template.spec.ts`: si crea un template, ne nasce una pagina, si pubblica, un visitatore
  la legge; poi il template guadagna una sezione, l'editor lo dice, e la pagina pubblica non è
  cambiata — **né allora, né dopo che la differenza è stata accettata nella bozza e salvata**. Più la
  misura delle tre larghezze. Verificati rompendo il prodotto: `templateDiff` che torna sempre vuoto,
  e le tre larghezze rese uguali.

### Due cose imparate contro il banco vero

- **`X-Requested-With: hub` non è un dettaglio del client.** Una chiamata di preparazione scritta a
  mano nelle spec è **403** senza quell'intestazione, ed è il prodotto che funziona: un form di un
  altro sito può mandare il cookie, non può mettere un'intestazione.
- **`POST /api/content/{id}/publish` senza corpo è 404**, non 400: l'endpoint dichiara un corpo, e
  senza non ci si arriva nemmeno. Un 404 nudo che sembra «riga che non esiste» e invece è
  «richiesta che non è arrivata».

### Debiti nuovi che G11 lascia

- ⚠️ **Un template non si scrive da nessuna schermata.** `key`, `required`, `locked` e
  `allowedBlocks` sono i quattro campi su cui poggia tutto §9.1, e il form delle sezioni non ne ha
  nessuno: oggi un template nasce da un seed o da una `PUT`. Non era nel perimetro di G11 e non ci è
  entrato. Va deciso **prima** di dire a un coordinatore che «ogni dipartimento si fa i suoi
  template» (§9.4). E `key` va restato **template-only**, per la stessa ragione degli altri tre.
- **La diff confronta il corpo in bozza**, non l'ultima versione pubblicata. È giusto — si lavora
  sulla bozza — ma vuol dire che due persone sulla stessa pagina vedono differenze diverse finché non
  salvano. Con un editor per riga alla volta non si vede; se M2 aggiunge la modifica concorrente,
  torna.

---

## 26. G11a di M1: scrivere un template da una schermata (7 settembre 2026)

Mezza fase, aperta il giorno stesso in cui G11 l'ha lasciata come debito. §9.1 poggia su quattro
campi di una sezione — `key`, `required`, `locked`, `allowedBlocks` — e **nessuno dei quattro si
scriveva da nessuna parte**: un template nasceva da un seed o da una `PUT` a mano. Design §9.4
prometteva a ogni coordinatore i template del proprio dipartimento, e il permesso ce l'avevano tutti
senza avere dove usarlo.

Nota di decisione: `decisions/2026-09-07-scrivere-un-template.md`, con i due bivi e le risposte.

### Il conto

| | |
|---|---|
| Tabelle nuove | **zero**; nessuna migrazione, nessuna colonna |
| Permessi nuovi | **zero**: `Content.ManageTemplates` esiste da M0 |
| Endpoint scritti a mano | **zero**: il server accettava già i quattro campi su un template |
| Componenti custom | **zero** |
| Meccanismi nuovi | **zero**, ma **una estensione al generatore**: `multi`, la sesta |
| Dipendenze nuove | **nessuna** |

### ⚠️ Il numero che G12 deve riportare non è più cinque

Design §12 prevedeva **cinque** estensioni al generatore di form. Sono **sei**: la sesta è `multi` —
un array di stringhe dentro un insieme chiuso, disegnato come una casella per valore — e l'ha chiesta
questa mezza fase.

La previsione guardava i **blocchi**, e i blocchi non l'hanno mai chiesta: scrivere un template sì.
§1.6 e §12 del design portano adesso il numero vero accanto alla previsione, invece di essere stati
riscritti: una previsione corretta a posteriori non è una previsione. **G12 riporta sei, e la riga
del perché.**

L'alternativa c'era e si è scartata: `z.array(z.object({ type }))`, cioè la lista ripetibile che
esiste già, con la conversione ai bordi. Zero estensioni, e per scegliere cinque tipi su ventisette
cinque «aggiungi» e cinque select. Era la forma del generatore imposta al problema; CLAUDE.md §2 dice
di estendere il meccanismo, non di aggirarlo.

### Che cosa c'è adesso

- **`sectionSettingsSchema` è una funzione**, come `contentMetadataSchema` è una funzione di `kind`:
  prende `{ blocks, unnamed }` su un template e `null` su una pagina. ⚠️ Su una pagina quei campi
  non si disegnano **e non si scrivono**: `CheckTemplateOnlyKeys` ne rifiuta tre a priori, quindi un
  form che li mandasse sarebbe un 400 a ogni salvataggio.
- ⚠️ **La `key` si scrive una volta sola**: il form la offre finché è vuota e la mostra dopo.
  Cambiarla su un template che ha già delle pagine rompe la corrispondenza **in silenzio** — la
  sezione di ogni pagina diventa «tolta dal template» e questa «nuova», e nessuno ha sbagliato
  niente. Il server accetta ancora qualunque cosa da una `PUT`: è il form che non fa inciampare, non
  una regola nuova del dominio.
- **`allowedBlocks` vuoto è l'assenza della chiave**, non una lista vuota: vuoto vuol dire «qualunque
  blocco», una lista vuota vorrebbe dire «nessuno», e sarebbe una sezione in cui non si può mettere
  niente.
- **Le caselle escono nell'ordine dell'insieme**, non in quello in cui sono state spuntate: quello
  che si salva è un insieme, e due array con gli stessi valori in ordine diverso sarebbero una
  modifica che nessuno ha fatto.

### I test

- `extensions.test.tsx` — due casi sul sesto tipo di campo, compreso l'ordine.
- `e2e/full/template.spec.ts` — **il giro che prima di oggi non si poteva fare affatto**: si scrive
  una sezione di template dall'editor (chiave e blocchi permessi), si salva, si riapre e la chiave è
  una riga invece che un campo; poi nasce una pagina da quel template e la sua palette offre **il
  solo blocco permesso** e nessuno degli altri ventisei. Verificato rompendo il prodotto due volte —
  la chiave sempre modificabile, e `allowedBlocks` scritto sempre nullo.

### Debiti nuovi che G11a lascia

- **Un template non si crea da nessun pulsante**: si scrive la *struttura* di un template che
  esiste, ma la riga nasce ancora da un seed o da un `POST` con `isTemplate: true` — `isTemplate` è
  `hidden` nel form dei metadati apposta, perché una pagina che si promuove a template è un permesso
  e non una casella. Serve un «nuovo template» accanto a «nuovo da template», ed è una schermata di
  lista, non un meccanismo. Piccolo, e va guardato in G12 mentre si ricopiano `/about` e `/start`.
- **Un template non dice quante pagine sono nate da lui.** Adesso che si modificano i vincoli, la
  domanda «chi tocco se cambio questo?» è ragionevole e non ha risposta sullo schermo. Il dato c'è
  (`TemplateId`), la schermata no.

---

## 27. G12 di M1: usare quello che si è costruito (7 settembre 2026)

La fase che verifica invece di costruire. Ha prodotto **più difetti di qualunque altra**, e nessuno
di essi era trovabile prima: metà esistono solo quando il contenuto è vero.

### Il conto

| | |
|---|---|
| Tabelle nuove | **zero** |
| Permessi nuovi | **zero** |
| Endpoint scritti a mano | **zero** |
| Componenti custom | **zero** |
| Meccanismi nuovi | **zero**; una correzione a una ricetta esistente (design M0 §7.3) |
| Dipendenze nuove | **nessuna** |

### Che cosa ha trovato la ricopiatura a mano

`/about` e `/start` sono state ricopiate dal sito attuale **dall'editor**, in italiano e in inglese, e
pubblicate. Il piano prevedeva «due o tre attriti»; sono stati sette, e due erano difetti veri.

⚠️ **Il grosso: ogni form del back-office si poteva salvare una volta sola per caricamento di
pagina.** Le schermate leggevano la riga dal `loader` della rotta, che gira alla navigazione e mai
più: dopo il primo salvataggio il form teneva il `rowVersion` di quando la pagina si era aperta, e il
secondo era **409 «somebody else changed this in the meantime»**, con nessun altro in giro. Undici
rotte. Sotto c'era un secondo difetto: `Route.useLoaderData()` è tipizzato **`never`** in quei file, e
`never` è assegnabile a tutto, quindi TypeScript non stava controllando niente lì. Nota
`decisions/2026-09-07-il-loader-non-e-la-riga.md`; il guardiano è `web/src/routes/routes.test.ts`.

⚠️ **Il secondo: le pagine seminate non soddisfacevano i propri template seminati**, da sempre, in
quattro modi. Il pannello di G11 lo ha detto la prima volta che qualcuno ha aperto `/about` per
davvero. Il peggiore era la home, che dava una `key` a una sezione tutta sua: l'azione offerta per
«non è più nel template» è **rimuovi**. Corretti, e adesso `seeds.test.ts` confronta i due alberi con
lo stesso `templateDiff` dell'editor.

Gli altri cinque attriti, che sono misure e non difetti:

- **ricopiare è tradurre**: due lingue obbligatorie per pubblicare, fonte solo italiana;
- **un wizard non è una pagina**: il bivio Pilota/ATC di `/start` è diventato due sezioni, e il
  template ne dava tre dove ne servivano cinque;
- **il blocco «Link list» è un blocco Data**: i cinque link di `/start` sono righe del modulo Link, e
  quella tabella era **vuota** — `/start`, `/home`, `/pilots` e `/atc` finivano tutte con un titolo e
  il nulla sotto;
- **l'editor non conferma mai un salvataggio**: nessun avviso, nessuna ora. Una sezione intera è
  andata persa così, e me ne sono accorto interrogando il database;
- **nessun annulla sull'ultima mossa strutturale**: le frecce stanno a due pixel dal nome della
  sezione, e una sezione spostata per sbaglio non si recupera.

### Che cosa ha trovato il giro visivo

Misurato invece che guardato (`decisions/2026-09-07-giro-visivo-m1.md`), perché una cattura dello
schermo aveva già dato un falso allarme — HANDOFF §13, la regola tiene.

- ⚠️ **`--muted-foreground` non passa AA nel tema scuro**: è lo stesso colore nei due temi, fa
  5,89 : 1 su bianco e **3,14 : 1** sul fondo scuro, sotto i 18,66 px. Token di Atmosphere.
- ⚠️ **Le props che non sono prosa finiscono nell'indice di ricerca**: `left muted` del blocco `hero`
  si legge negli snippet. Rompe una regola già scritta in `CLAUDE.md` §4. Il rimedio generico è
  indicizzare **solo le mappe tradotte**: la prosa è sempre `Localized`, le enumerazioni e gli URL
  sono stringhe nude, e il server distingue le due cose senza conoscere gli schemi.
- La sintassi Markdown si legge negli snippet, stesso punto di intervento.
- **Due `h1` su ogni pagina pubblica**, e **il blocco Titolo nasce a livello 1**.
- A posto: nessuno scorrimento orizzontale a 419 né a 1280 px, larghezze delle sezioni esatte, zero
  fallimenti di contrasto su `/start`.

⚠️ **Il back-office a 375 px non è stato guardato**: il pannello del browser non scende sotto 419 e
il browser con la sessione dello staff non si ridimensiona. È l'unico pezzo di giro visivo che manca.

### I documenti che G12 lascia

- `tools/demo-m1.md` — nove parti, gli otto punti della definizione di fatto spuntati uno a uno.
  **L'accettazione è che Carmine lo esegua da zero.**
- `decisions/2026-09-07-m1-review.md` — il conto contro la previsione: **6 / 3 / 6 / 4 / 7** contro
  6 / 3 / 5 / 4 / 1.
- `decisions/2026-09-07-m1-checklist.md` — la revisione §16.E su tutto il codice di M1.
- `decisions/2026-09-07-giro-visivo-m1.md`, `2026-09-07-il-loader-non-e-la-riga.md`.
- Piano 00 a **v0.45**, con la metrica «endpoint scritti a mano» corretta; `docs/UI-GUIDELINES.md`
  guadagna le quattro regole dell'editor che M1 ha deciso usandolo.

### Debiti nuovi che G12 lascia

Tutti e sei quelli elencati sopra e non ancora corretti — il grigio nel tema scuro, le props
nell'indice, il Markdown negli snippet, i due `h1`, il livello 1 di default, la conferma del
salvataggio e l'annulla. Nessuno impedisce il tag: sono rifiniture, tutte scritte, nessuna scoperta
per caso.

---

## 28. G13: che cosa è uscito eseguendo la demo (7 settembre 2026)

M1 era chiusa nei documenti e mancava solo il tag. Poi Carmine ha eseguito `tools/demo-m1.md` fino al
punto 7 e ha trovato **quattro difetti e dodici richieste**. È il motivo per cui quel documento
esiste, e la prova che l'accettazione non può darla chi ha scritto il codice.

⚠️ **Il tag `v0.2.0-m1` aspetta la fine dei difetti.** Deciso da Carmine.

La fonte di questa sezione è `decisions/2026-09-07-dopo-la-demo.md`, che ha l'elenco completo: qui c'è
solo quello che serve per riprendere in mano il lavoro.

### Corretti, con un test ciascuno verificato rompendolo

**Ogni data dell'hub era mostrata due ore indietro** (`6f8217e`). L'API mandava
`2026-09-07T14:22:35.99759`, senza `Z`: un browser legge una stringa così come ora **locale**. I
valori nel database erano giusti; mancava che il modello dicesse che sono UTC.
`UtcDateTimeConverter`, un posto solo. Il test guarda **il testo sul filo** e su una **lettura** —
la risposta alla creazione serializza l'entità ancora nel change tracker e passa anche senza la
correzione.

**Cancellare lasciava la pagina aperta** (`fc33848`), ed ⚠️ **era una regressione della correzione
del loader della stessa mattina**. La mutazione aspettava `invalidateQueries`, che aspetta il refetch
di ogni query attiva sotto quella chiave — compresa quella della schermata che sta cancellando, la
cui riga è appena sparita. Il refetch va in 404, riprova, `onSuccess` non si risolve e i callback di
`mutate` non partono. Prima che le schermate leggessero la query invece del loader, quella query non
aveva osservatori: **una correzione ne ha scoperta un'altra**, e vale la pena aspettarselo di nuovo.

### Chiusi il 7 settembre, misurando prima di correggere

**Il logout non aggiornava la pagina** (`686ee82`), e l'ipotesi era giusta: **il bootstrap non è
solo una query**. La radice lo carica una volta con `ensureQueryData` e lo passa come **contesto del
router**, ed è quella copia che leggono l'header, la sidebar e ogni guardia — invalidare una query
non rifà un `beforeLoad`. Un posto solo lo dice adesso, `sessionChanged`: **rimuove** la risposta in
cache (invalidarla non basta, `ensureQueryData` restituisce ciò che trova) e chiama
`router.invalidate()`. Lo usa anche la risposta al 401. ⚠️ Uscire porta prima **a casa**: una
schermata del back-office sta dietro una guardia che manda a `/auth/login`, e ridisegnare dove si è
avrebbe risposto a un clic su «esci» con il login di IVAO, che ha ancora la sua sessione.

**Un documento pubblicato con un'immagine non la mostrava** (`0e28db1`): guardato dal filo, è **la
visibilità della riga media**. Un file nasce `Staff` — diventa pubblico perché qualcuno lo dice — e
la pagina usciva lo stesso, così il lettore riceveva 404, anche questo per disegno. Quello che
mancava è che la pagina non aveva titolo per uscire portandola. La pubblicazione ora **rifiuta**
sotto `VisibilityCeiling`, lo stesso soffitto di un blocco Data `frozen`, per i **tre** modi di
nominare un file: il corpo, la copertina di una news e il file di un documento — che è la forma in
cui Carmine l'ha incontrato. Rifiuta e non ripara: pubblicare non deve rendere pubblico un file di
nascosto.

⚠️ Quel test ha fatto uscire un accoppiamento fra classi di test che era lì da prima: tutta
l'assemblea di integrazione scrive nello **stesso** database, `ContentEndToEndTests` nominava la
media `7` in un corpo, e il file caricato in più ha spostato gli identificatori finché «questa media
non è usata da nessuna parte» ha trovato quella pagina. Un numero che nessun upload raggiunge, e la
ragione scritta lì.

### Le decisioni prese in quella conversazione

1. **Il tag dopo i difetti.**
2. **Niente icone per i dipartimenti: la sigla è il segno.** Oggi sono nove scudi identici. Ragione
   di Carmine: un fork non-IVAO riscrive comunque l'enum `Department`, quindi l'icona non è il pezzo
   che gli costa — e la sigla è già l'identificatore che lo staff usa.
3. **L'avviso a quattro stati** (errore, avviso, successo, informazione) è un componente
   **condiviso**. ⚠️ Quinto della lista chiusa di piano §8.3: va scritto, non aggiunto di straforo.
4. **I tipi di evento del calendario sono di divisione**, decisi centralmente e uguali per tutti.
   ⚠️ Non sono le categorie, che sono per dipartimento: serve un vocabolario di divisione con un
   permesso di scope diverso da `Calendar.Edit`.

### Due lezioni di metodo, pagate care

- ⚠️ **`grep "error CS"` non dice se una build è riuscita.** Con l'API in esecuzione MSBuild fallisce
  con `MSB3027`/`MSB3021` — DLL bloccate — senza **nessun** errore `CS`. Due volte ho letto «0
  errori» ed eseguito un binario vecchio, e una verifica «rotta apposta» è passata a vuoto. Si guarda
  `Error(s)` nel riepilogo, e **si ferma l'API prima di compilare**.
- **Tre ipotesi plausibili di fila possono essere tutte sbagliate.** Sul difetto della cancellazione:
  componente smontato, promessa rifiutata, retry lento — tutte no. Le sonde dentro la mutazione hanno
  risolto in un giro.

---

## 29. Da dove riparte la prossima sessione (7 settembre 2026)

### Si continua G13 dalle richieste: **i quattro difetti sono chiusi**

Ramo **`m1/g13-fixes`**, sedici commit, spinto, con la **PR #57 aperta e la CI verde**. Il piano di implementazione è a
v2.8 e la fase è scritta lì; l'elenco completo è in `decisions/2026-09-07-dopo-la-demo.md`. Verde in
locale: **462 .NET** (300 unit + 162 integrazione), **270 Vitest**, **43 smoke Playwright** e **12
del giro pieno** (`pnpm e2e:full`, rieseguito l'8 set 2026 contro l'API vera, e nei suoi log si vede
l'editor che chiede `publish-problems`), più lint, typecheck, format e i18n. Non resta niente di
non eseguito.

⚠️ **Il numero da riportare alla chiusura sono otto estensioni del generatore di form**, non sei:
`slugFrom` è la settima e `suggestions` l'ottava, e §12 del design ne prevedeva cinque. Le ultime
due le ha chieste l'uso, non i blocchi — che è la ragione dello scarto, e vale la pena scriverla
così nel rapporto.

**Il prossimo passo**, nell'ordine del piano di implementazione §G13:

1. ~~Lo **`slug` proposto dal titolo**~~ — fatto (`38c6e1c`).
2. ~~La **conferma dell'editor** (6) e l'**avviso a quattro stati** (9)~~ — fatti. `Notice` è il
   **quinto** componente dell'elenco chiuso, e la riga in piano §8.3 c'è insieme a
   `docs/UI-GUIDELINES.md` §3, `catalog.ts` e la sezione della ui-kit. La conferma è un **toast**,
   scelta di Carmine, e `ProblemAlert` resta dov'è — unirli tocca ogni schermata del back-office.
3. ~~**«Cosa manca per pubblicare»**~~ — fatto: un `GET` che fa la prova a vuoto, e una lista sola
   in tono `warning` che si svuota da sola. ⚠️ **Quarto verbo a mano** appeso a `MapCrud`, deciso
   con Carmine perché l'alternativa era riscrivere le regole nel client.
4. ~~Il calendario (10), la striscia (12) e la ricerca visibile (13)~~ — fatti.
5. ~~La **11**, i tipi di evento di divisione~~ — decisa da Carmine sulla prima opzione della nota e
   **fatta** (`8b6458c`): `cms_calendar_kinds`, modalità globale del motore CRUD, `Calendar.View`
   per leggere e il nuovo **globale** `Calendar.ManageKinds` per scrivere, schermata sotto
   `/staff/admin/calendar-kinds`, cinque parole seminate con etichette i18n.
   ⚠️ Da qui in avanti **il `kind` di una voce non è testo libero**: il validatore chiede al
   vocabolario, ed è l'unico validatore dell'hub che interroga il database. Una voce che un modulo
   proietta non passa da quel DTO e resta libera, ed è voluto. Il vocabolario viaggia in `/api/me`,
   perché la chip di un calendario pubblico deve dire la parola e il colore.
6. ~~La **14**, il giro sull'editor~~ — **fatta** (`9bf76ea`), e sono le quattro cose che la
   ricopiatura a mano e il giro visivo avevano già scritto: l'**annulla** sull'ultima mossa
   strutturale, il blocco Titolo che **nasce a livello 2**, **un solo `h1`** per pagina pubblica, e
   il pannello delle proprietà che **resta fermo** mentre l'albero scorre.

**G13 è completa**: quattro difetti e dodici richieste su dodici. Restano due cose, e sono di
Carmine: **rifare `tools/demo-m1.md` dal punto 1** — i punti 8 e 9 non erano mai stati eseguiti
prima del 7 settembre — e il **tag `v0.2.0-m1`** dopo il merge della PR #57, verificato
**sull'artefatto** e non sul commit.

⚠️ **`hidden sm:block` non funziona in questa applicazione**, ed è costato due elementi invisibili
nella stessa ora: il foglio di stile di Atmosphere è importato **dopo** le utility di Tailwind e
ridichiara `.hidden`, quindi la classe semplice batte quella dentro la media query e l'elemento non
torna più. Si scrive `max-sm:hidden`. È in `docs/UI-GUIDELINES.md` §3 per chi forka.

**Poi, e solo poi:**

5. **Carmine rifà `tools/demo-m1.md` dal punto 1** e arriva in fondo. ⚠️ **I punti 8 e 9 non sono
   ancora stati eseguiti**: sono i test, la forkabilità e il pacchetto, cioè l'ultimo pezzo di
   accettazione.
6. **Il tag `v0.2.0-m1`.** ⚠️ Si spinge **dopo** il merge, e si verifica **sull'artefatto**, non sul
   commit: in M0 ci vollero cinque tentativi, il server di prova deve fare il fallback SPA, e un grep
   su un bundle minificato non è una verifica — la verifica è comportamentale o non è.

⚠️ **Come si lavora su questa macchina**, imparato oggi: l'API di sviluppo (`dotnet run`) tiene
bloccate le DLL, quindi **si ferma prima di compilare** o la build fallisce con errori `MSB` che un
`grep "error CS"` non vede. E il banco di sviluppo gira su `ivaohub`, il database vero dello
sviluppo: le due pagine ricopiate a mano stanno lì e non nei seed.

- **G12 — migrazione a mano, giro visivo, chiusura di M1.** Ricopiare `/about` e `/start` a mano
  dall'editor — che è il vero collaudo di tutto quello che M1 ha costruito, fatto da chi lo userà —
  il giro visivo, il rapporto di chiusura con i numeri **contro la previsione di design §12** (6
  tabelle / 3 aree di permessi / 5 estensioni del generatore / 4 componenti custom / 1 endpoint
  scritto a mano) e il tag `v0.2.0-m1`.
- Il primo posto dove guardare per il conto è la riga «Il conto» di ogni sezione da §14 a §25: sono
  già le stesse cinque voci, fase per fase.

⚠️ **Il debito n.10 è chiuso** (§24) e **il n.2 pure** (§25): l'editor mostra le differenze dal
template. Di §10 restano aperti il n.5, il n.7, il n.8 e il n.9; il n.6 è di M2. Il debito che G11
aveva lasciato — **nessuna schermata scrive un template** — **è chiuso da G11a** (§26); quello che
resta di quella famiglia è più piccolo e sta in §26: un template si *modifica* ma non si *crea* da
un pulsante, e non dice quante pagine sono nate da lui.

Undici cose che G5, G6, G7, G8, G9 e G10 lasciano pronte e che **non vanno rifatte**:

- **`FullTextSearch` è l'unico posto che scrive una `MATCH`**, e la scrive una volta: chi ordina o
  filtra per rilevanza passa di lì.
- **`shared/search/highlight.ts` decide che due parole sono la stessa parola** — maiuscole, accenti —
  e lo decide per l'evidenziazione e per la palette insieme.
- **`staffDestinations` è l'elenco delle schermate del back office**, letto dalla sidebar e dalla
  palette. Una schermata nuova si aggiunge lì e compare in tutte e due.

- **Una striscia, una cornice, uno slot.** Quello che appartiene alla finestra e non alla colonna di
  lettura va nel `banner` di `Shell`; chi ne aggiunge un secondo lo passa dal layout che lo vuole.
- **Un componente che mostra dati del server prende un `status` di esempio** per la ui-kit, come un
  blocco prende `exampleData`. La galleria non chiama l'API.

- **La navigazione arriva da `/api/me` e non si scrive nel client**, `NavItem` compreso: una voce ha
  una chiave **oppure** un'etichetta tradotta, mai una stringa che a volte è una chiave. Chi aggiunge
  una voce di modulo aggiunge un `NavItemDescriptor`; chi ne aggiunge una editoriale scrive una riga.
- **Chi possiede il sito è `SiteOwnership`**, e il client lo legge da `division.siteDepartment`.
  Nessun file, nessuna route e nessuna schermata nomina un dipartimento.
- **Un seed di pagina è dati**: `seed/content-pages/*.json`, una chiave in `hub_division_settings`,
  applicato una volta. Una pagina nuova in una release successiva è un file, non del codice.
- **`shared/api/rowVersion.ts`** è quello che un form manda quando la riga non esiste ancora. Un
  `empty*` nuovo lo usa; una stringa vuota è un 400.
- **`ContentListScreen` e `ContentFormScreen`** sono la lista e il form di un `kind` qualunque, e
  `features/content/kinds.ts` è ciò che li distingue — quattro `kind` adesso.
- **`CrudOptions.SharedForReading`** (righe che tutti leggono), **`CrudOptions.ReadOnlyRows`** (righe
  che nessuno scrive) e **`ISubmittedByMembers`** (righe che chiunque può creare) sono i tre modi
  generici di dire una cosa sola: il motore non sa che cosa sia un template, una proiezione o un
  messaggio.
- **`CrudOptions.Name`** serve a chiunque metta una seconda risorsa nella stessa area di permessi.
- **`CalendarView`** disegna agenda, settimana e mese e non decide niente altro: quali voci e per
  quale finestra è affare di chi lo monta.
- **Il servizio notifiche esiste ed è finito.** Un modulo che deve avvisare qualcuno pubblica un
  intento e non sa niente di SMTP, code o tentativi; una notifica nuova è **una riga** in
  `NotificationTypes` e una coppia di chiavi in `mail.json`, in ogni lingua.
- **Le impostazioni di un membro sono la famiglia `/api/me/…`**, non il motore CRUD: `locale` da M0,
  `notifications` da G7. La terza si scrive come le prime due.

### Deciso e già collocato, da non ridiscutere

- **I template sono di dipartimento e li legge tutto lo staff** (piano §9.3, design M1 §9.4):
  **costruito in G5** (§19), con una sola espressione sull'entità letta in SQL e in memoria.
- **`mediaId` è il nome con cui un blocco nomina un file**, a qualunque profondità (§15, §17).
- **Le icone sono una griglia e non un select** (§16), perché il `Select` di Atmosphere prende una
  stringa per opzione. È stato misurato.
- **La regola «in area» dello stato della rete** è quella di §18: stazione del nominativo contro
  centri e aeroporti dello snapshot, partenza o arrivo contro gli aeroporti. Non è configurazione e
  non deve diventarlo.
- **Le liste di valori nudi non si aggiungono al generatore di form**: una lista di oggetti con una
  chiave dentro costa una chiave nel JSON e niente nell'editor, ed è il precedente di G3 e G4.
- **L'indirizzo di un membro è di sola andata** (§21): si legge dal profilo IVAO, vive in
  `hub_users.email` per la coda delle notifiche, e nessun DTO lo espone. La staff directory di G9
  non lo tocca — `NoDtoCarriesAnEmailAddress` fallirebbe.

### Deciso il 6 settembre 2026, e costruito in G8

La **dashboard di dipartimento** non è più aperta (`decisions/2026-09-05-dashboard-di-dipartimento.md`,
sezione «La decisione»). Le tre risposte:

1. **Blocchi.** Una riga di `cms_contents` per dipartimento, `kind = Dashboard`, `slug` = il codice
   del dipartimento, nata da un template di sistema e modificata nell'editor che esiste già. Il
   motivo, con le parole di Carmine: *ogni dipartimento ha le sue esigenze; la base e i tool glieli
   dà lui, la gestione è loro*. Si rinuncia alle tile che **compiono azioni** — i blocchi mostrano —
   e la strada per i moduli di M2/M3 è registrare un **blocco Data**, non una tile.
2. **La vede il proprio dipartimento, più i dipartimenti a cui il VID è autorizzato.**
3. **Dentro G8**, come task 6.

### La portata di un grant: chiusa in G8

✅ **Fatto** (§22): `HubClaims.BuildIdentity` scrive un claim `dept` anche per ogni dipartimento
nominato da un permesso la cui sorgente è un grant attivo, e i test sulla **lista** che mancavano ci
sono. Un grant dà adesso il permesso, la lista e le righe `Visibility.Department` di quel
dipartimento — che è ciò che rende vera la visibilità decisa per la dashboard.

⚠️ **Va detto a chi concede un grant, e la schermata dei grant non lo dice ancora**: un claim `dept`
non è un permesso, è «questa persona fa parte di quel dipartimento ai fini di ciò che *vede*». Chi
riceve un grant qualunque sull'AOD comincia a vedere tutte le righe `Department` dell'AOD, comprese
quelle di aree su cui non ha ricevuto niente. Una riga di spiegazione su `/staff/admin/permissions`
è il debito che resta, ed è di chi tocca quella schermata.

### Aperto, e non è di M1

- **Autorizzare qualcuno su un pezzo di un altro dipartimento** —
  «i CH gestiscono i training ma nient'altro nel TD», «il FOD inserisce le rotte di un evento ma non
  le postazioni». ⚠️ La prima stesura della nota proponeva un grant che porta un **livello**: è
  **scartata**, perché serviva l'opposto di un pacchetto. Il meccanismo esiste già — un grant è un
  permesso più un dipartimento — e quello che serve sono **due regole di design**, che vincolano M2
  e M4 e non M1:
  1. **la granularità sta nel catalogo del modulo**: una capacità delegabile ha un nome suo
     (`Training.AssignTrainer` accanto a `Training.Edit`), e ci si accorge al design del modulo, non
     il giorno in cui qualcuno chiede il grant;
  2. **una capacità delegabile è una riga sua, con la sua area**, perché
     `EnsureWriteIsAllowed` chiede `<Area>.Edit` **per tipo di entità**: permessi per *campo* non
     esistono e non si inventano. Rotte e postazioni di un evento sono due entità, non due colonne —
     e se quelle righe siano dell'evento (con un grant) o del FOD (senza) lo decide il design di M2.
  3. E per i CH c'è una terza via che non costa grant: `IHasFir` più `firStaffScope`, cioè «i
     training della propria FIR» come regola invece di nove grant da revocare a mano. Domanda di M4.

### Il banco e2e, in due righe

`docker compose up -d mariadb`, poi da `web/`: `pnpm e2e:full`. Pubblica in `artifacts/e2e-bench/`,
avvia su <http://127.0.0.1:5080>, aspetta `/health`, gira. `E2E_SKIP_PUBLISH=1` riusa l'ultima
pubblicazione mentre si lavora sulle spec. Scrive in un database suo, **`ivaohub_e2e`**, mai in
quello di sviluppo, e non lo ripulisce: ogni giro crea la propria pagina. Il resto in
`web/e2e/full/README.md`.

⚠️ **`dotnet test` su questa macchina dice «Zero tests ran»** e chiude con exit 5, per entrambi i
progetti e anche su `main`: è il ponte fra il runner e xUnit v3, non i test. Si eseguono lanciando
direttamente `tests/IvaoHub.UnitTests/bin/Debug/net10.0/IvaoHub.UnitTests.exe` e l'omologo di
integrazione (`-class <nome completo>` per filtrare). In CI `dotnet test --solution` funziona.

### Igiene

Niente da ripulire: i branch delle fasi vengono cancellati alla fusione, e `git branch -a` mostra
soltanto `main`.

⚠️ **Gli intervalli di VID dei test di integrazione sono occupati, e la suite condivide un database
solo**: due classi sullo stesso VID sono **una riga**, e la posizione che una classe gli dà lo segue
nell'altra. Al 7 set 2026: 610xxx `MapCrudLinks`, 620xxx `Content` e `Media`, 630xxx `Search`,
640xxx `DataBlock`, 650xxx `NewsDocumentsAndCategories`, 660xxx `Calendar`, 670xxx `Contacts`,
680001 `ForkabilityXx`, 690xxx `SiteMenuAndDashboard`. Chi apre una classe nuova parte da 700001.
G8 ci è cascata: 660002 è un superadmin di `CalendarEndToEndTests`, e due rifiuti smettevano di
essere rifiuti — in isolamento tutto passava, solo la suite intera lo mostrava (§22).

⚠️ **E una riga che un test scrive per cambiare una risposta è una riga che quel test si riprende.**
Sempre in G8: una voce di menu lasciata in tabella nascondeva la voce del modulo `atc` a ogni classe
che guardasse `/api/me` dopo. Un `finally` che la toglie costa tre righe.

⚠️ **`git checkout -- <file>` su lavoro non committato lo cancella**, e in G2 è costato mezz'ora
(§16). Prima di rompere qualcosa apposta per provare un test si committa, e si ripristina dalle
proprie copie — in G4 è stato fatto così sei volte di seguito, e ripristinare è costato un comando.

⚠️ **Docker Desktop di questa macchina è caduto due volte durante G1**, e non per colpa del
progetto: al riavvio il backend non riesce a rimuovere due socket rimasti da un crash precedente
(`%LOCALAPPDATA%\Docker\run\dockerInference` e `%LOCALAPPDATA%\docker-secrets-engine\engine.sock`) e
si ferma con un dialogo. Il rimedio, autorizzato da Carmine e applicato due volte: chiudere Docker
Desktop, **rinominare le due cartelle** (i singoli file non si lasciano cancellare), riavviare. Mai
«Reset to factory defaults», che butterebbe immagini e volumi. Senza engine non gira nessun test di
integrazione, quindi vale la pena saperlo prima di perderci tempo.
