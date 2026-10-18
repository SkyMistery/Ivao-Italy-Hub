# La sicurezza dell'hub: la lista di controllo

> Per la fase di test dopo M4 (nota `decisions/2026-10-18-la-fase-di-test.md`, §4). È OWASP (Top 10 e ASVS, livello 2) ridotto a ciò
> che l'hub ha davvero: ogni voce dice **che cosa provare** e **dove guardare**. Chi attacca segna accanto a ogni voce *fatto / trovato
> #N / non applicabile*, e la lista compilata va nella sua PR o nel suo advisory.

## 0. Le regole del gioco

- **Si attacca un'istanza locale**: il pacchetto della release (`ivao-division-hub-v<versione>.zip`, o `dotnet publish` dello stesso
  tag) con MariaDB 11.4.10 in Docker, ambiente **`Production`**, il proprio `config/ivao-oauth.json` di prova. **Mai** l'ambiente
  `E2E` (apre `/e2e/signin` e `/e2e/jobs` per costruzione: si controlla solo che **non** esistano in `Production`).
- **Sulla prova online** (`test.it.ivao.aero`) solo letture non distruttive: header, CSP, cookie, che cosa risponde una pagina
  riservata senza accesso, `robots.txt`. Niente scansioni automatiche, niente tentativi in massa.
- **Una debolezza sfruttabile non va in una issue pubblica** (il repository è pubblico): advisory privato di GitHub o, in attesa, in chat
  a Carmine. Un indurimento senza attacco è una issue con l'etichetta `security`.
- Servono almeno **tre identità**: un superadmin, uno staff di un dipartimento (o un grant su un'area), un membro semplice; più il
  visitatore senza accesso.

## 1. Accesso e sessione

- [ ] **Login IVAO (OIDC)**: `state`, `nonce` e correlazione rifiutano una risposta rigiocata o di un altro browser
  (`Core/Auth/IvaoAuthenticationExtensions.cs`); il «secondo giro» del nonce (0.2.5) non apre un accesso senza consenso.
- [ ] **Ritorno dopo l'accesso**: il parametro di ritorno non porta fuori dal sito (open redirect con `//evil`, `\\evil`, `https:evil`,
  URL codificati due volte) (`loginHref`, 0.6.7).
- [ ] **Il cookie**: `HttpOnly`, `Secure`, `SameSite` adatto; dopo il logout il cookie vecchio non apre più nulla; la sessione scade.
- [ ] **CSRF**: un POST/PUT/DELETE dell'API da un'altra origine con il cookie del membro viene rifiutato (verificare che cosa protegge
  davvero: `SameSite`, header richiesti, antiforgery).
- [ ] **Superadmin**: `division.json → superAdmins` è solo l'avvio; la verità è `hub_users.is_superadmin` (`CLAUDE.md` §3). Un VID
  tolto dal file resta superadmin? Un VID aggiunto lo diventa senza riavvio?
- [ ] **Installazione privata**: con `Installation:Preview` un membro non staff non entra e non lascia la sua riga in `hub_users`.

## 2. Permessi (il punto più delicato)

L'hub ha **un solo** gestore d'autorizzazione (`Core/Auth/Permissions/HubAuthorization.cs`), il filtro globale e la guardia
dell'interceptor. Ogni prova: **da un membro senza il permesso**, poi **con il permesso su un altro dipartimento / su una riga sola**.

- [ ] **Leggere una riga di un altro dipartimento** cambiando l'id nell'URL dell'API (IDOR) — liste e dettagli di ogni modulo.
- [ ] **Scrivere** (PUT/DELETE) una riga che si può solo leggere, o di un altro dipartimento.
- [ ] **Grant su una riga** (`resource_scope`): apre solo quella riga, mai la lista intera né i registri (E10u, `HasUnscoped`).
- [ ] **«Chi ha interesse non decide»** (`DeniedToStakeholder`): nessuno, superadmin compreso, decide la propria riga — un proprio
  turno ATC, un proprio PIREP, una propria richiesta di training, un proprio leg dei tour.
- [ ] **Le righe affidate** (`OnlyForAssignee`): chi non è l'assegnatario non scrive.
- [ ] **I capi FIR** (`firStaffScope: own`): vedono e scrivono solo le righe del loro FIR.
- [ ] **Il menu non è la sicurezza**: ogni pagina staff aperta scrivendo l'URL risponde 403 dall'API, non solo un menu nascosto.
- [ ] **Bozze**: una bozza di pagina, news, documento o evento non è mai pubblica (`/api/content/...`, il sito pubblico legge solo
  `cms_content_versions`).
- [ ] **Campi che il client non deve poter scrivere**: mandare nel corpo `ownerDepartment`, `isSuperadmin`, `decidedBy`, `state`, `vid`
  di un altro (mass assignment) — il server li ignora o rifiuta.

## 3. Dati personali e privacy

- [ ] **Le API pubbliche** (senza accesso) non danno VID, nomi o dati di membri oltre quello che la pagina pubblica mostra (eventi,
  tour, calendario, ricerca, «In rete adesso»).
- [ ] **Un membro non legge i dati di un altro**: prenotazioni, PIREP, disponibilità ATC, iscrizioni in presenza, richieste di training,
  registri (`/events/mine` e simili) — solo i propri.
- [ ] **La cancellazione** (`IPersonalDataEraser`, `PersonColumns`, `[ErasedWith]`): dopo la cancellazione il nome non compare più da
  nessuna parte, audit compreso; il pseudonimo non porta indietro alla persona.
- [ ] **Gli errori** (`ProblemDetails`) non portano stack, SQL, stringhe di connessione o percorsi del server.
- [ ] **La diagnostica** (`diagnostics/`, 0.9.2): i file non nominano nessuno (nessun VID, IP, query string) e **non sono scaricabili**
  dal sito (document root su `wwwroot/`).
- [ ] **La prova dei collegamenti** (`/api/admin/diagnostics/connections`) risponde un tipo d'errore, mai la stringa di connessione,
  l'utente, l'host o il messaggio; solo superadmin.

## 4. Token personali e chiamate da programmi

- [ ] **Token personali** (`Core/Auth/`, note `2026-09-24-i-token-personali`): un token di un `audience` non apre un altro audience; i
  permessi si ricalcolano a ogni richiesta (un permesso tolto chiude subito); un token revocato non apre più nulla; il token non compare
  in nessun log né nella diagnostica.
- [ ] **L'esportazione delle prenotazioni** (`docs/events-bookings-export.md`): solo con il token giusto; nessun dato in più di quello
  scritto nel contratto; la versione del contratto (`ContractVersion`) rifiuta un client vecchio come dice.
- [ ] **`/api/jobs/run`** (`Jobs:Token`): senza token l'indirizzo non esiste; con un token sbagliato 401/404 senza indizi; il token
  nell'URL (fallback) non finisce nei log.
- [ ] **Il banco**: `/e2e/signin`, `/e2e/jobs/{name}`, `/e2e/sessions/{vid}` **non esistono** in `Production` (404), nemmeno con
  `E2E:Enabled` nei segreti (l'avvio deve fermarsi).

## 5. Contenuti, blocchi e file

- [ ] **XSS nei contenuti**: testo, markdown, titoli, nomi di file, campi tradotti, note dello staff, motivazioni — `<script>`,
  `javascript:` nei link, `onerror=` negli attributi, SVG con script — non eseguono nulla, né nell'editor né sul sito pubblico.
- [ ] **CSP** (`config/security.json`): `script-src 'self'` senza eccezioni; una pagina con un contenuto ostile non carica script
  esterni; gli header ci sono anche sulle pagine servite dal server web (`index.html`, nota `2026-09-28-gli-header-dei-file-statici`).
- [ ] **Il blocco interattivo** (`/embed/{content}/{version}/{block}`, nota `2026-09-12-il-blocco-interattivo`): gira con la **sua**
  CSP, in un frame isolato; dal frame non si leggono cookie né si chiama l'API con le credenziali del visitatore; la bozza di un blocco
  non si apre da `/embed` senza i permessi.
- [ ] **I blocchi `video` ed `embed`**: solo gli host dell'allowlist (`docs/UI-GUIDELINES.md`); un host fuori lista è rifiutato al
  salvataggio, non solo al disegno.
- [ ] **Upload** (`Core/Content/MediaEndpoints.cs`, `MediaOptions.AllowedContentTypes`): un file con estensione falsa (un `.php`, un
  `.html` o un SVG travestiti da immagine), un file troppo grande, un nome con `../` — rifiutati; un file caricato non si esegue e non
  si apre come pagina HTML del sito (Content-Type e `Content-Disposition`); i file di una bozza non sono pubblici.
- [ ] **Indirizzi dei contenuti**: uno slug non può occupare un indirizzo riservato (`api`, `auth`, `media`, `embed`, `openapi`,
  `scalar`…, `ContentAddresses.cs`).

## 6. Input, server e configurazione

- [ ] **Injection**: le poche query scritte a mano (`SqlQuery`, `FOR UPDATE`, ricerca) usano parametri — provare apici e `%` nei
  campi di ricerca e nei filtri delle liste.
- [ ] **Paginazione e limiti**: `pageSize` enormi, liste senza fine, corpi JSON enormi, array con migliaia di voci (PIREP: 1–20 voci;
  slot da tabella) — rifiutati senza far cadere il processo.
- [ ] **Header del proxy** (`ForwardedHeaders`, nota sulla catena dei proxy, 0.2.3): un `X-Forwarded-For` scritto dal client non
  cambia l'IP creduto né aggira un controllo.
- [ ] **OpenAPI / Scalar**: in `Production` sono esposti? Se sì, dicono solo ciò che è già pubblico?
- [ ] **Header di sicurezza** su `test.it.ivao.aero` (solo lettura): HSTS, `X-Content-Type-Options`, `Referrer-Policy`,
  `Permissions-Policy`, `frame-ancestors`.
- [ ] **File che non devono uscire**: `appsettings*.json`, `secrets/`, `hub-keys/`, `diagnostics/`, `*.dll`, `*.pdb` — non scaricabili
  dalla prova (document root `wwwroot/`).
- [ ] **Dipendenze**: `dotnet list package --vulnerable --include-transitive` e `pnpm audit` puliti, o con un motivo scritto.

## 7. Integrazioni esterne

- [ ] **IVAO API** (`IIvaoApiClient`): una risposta strana o enorme di IVAO non fa cadere l'hub né scrive dati sbagliati; i token
  dell'app non finiscono nei log.
- [ ] **L'archivio di vIPI** (`v_share_atc_sessions`): solo lettura, mai una scrittura (il contesto condiviso rifiuta di salvare); una
  vista assente o un DEFINER rotto danno «non disponibile», non un errore all'utente.
- [ ] **Le mail** (servizio di notifica del nucleo): un campo scritto dall'utente (nome, nota, motivazione) non inietta header né HTML
  attivo nella mail.

## Dove guardare per prime

Per l'esperienza della revisione: **i permessi** (§2) e **i dati di un membro visti da un altro** (§3) sono dove un difetto è più
probabile e fa più danno; poi **upload e blocco interattivo** (§5). I verbi scritti a mano dei moduli — quelli fuori da `MapCrud`, che i
rapporti di chiusura contano per famiglia — sono i primi da leggere.
