# IVAO Division Hub — Piano di progettazione

**Progetto:** nuovo sito/hub della divisione italiana IVAO (sostituisce `it.ivao.aero`), progettato per essere forkabile da altre divisioni.
**Versione documento:** 0.24 — 3 settembre 2026 (spina dorsale di M0 in piedi; licenza decisa)
**Autore:** Carmine (IT-DIV), con supporto Claude
**Stato:** architettura, catalogo moduli (§9), contratti (§9.7), **meccanismi generici** (§16) e **modello unico dei contenuti** (§9.3) decisi; restano aperte solo le voci di §15 (per lo più informazioni da recuperare). **Design di M0 scritto** (`01-design-m0.md`, firme e perimetro) con piano di implementazione a fasi F0–F9 (`02-piano-implementazione-m0.md`). Implementazione in corso: F0–F4 chiuse, prossima F5. Le sezioni marcate ⚠️ richiedono ancora una decisione

**Changelog 0.24** (3 set 2026, sera): **licenza decisa — Apache-2.0**, copyright «2026 Carmine Granato» (§15.5 punto 5, che restava aperto fra MIT e Apache-2.0; il criterio «coerente con gli SDK `ivao-italy`» non decideva da solo, perché quell'organizzazione è mista). Il file `LICENSE` porta ora il testo canonico completo al posto del `TBD`, che alla lettera non concedeva niente a nessuno e rendeva non forkabile un repository che si presenta come forkabile. Nessun header di licenza nei singoli file: Apache-2.0 li raccomanda ma non li impone, e sarebbero rumore in ogni diff. Il file **`NOTICE` invece c'è fin da subito** (deciso da Carmine subito dopo): oggi porta solo l'attribuzione di questo progetto, ma è il posto dove va ogni attribuzione di terzi che il codice dovesse incorporare, ed è l'unica cosa che la licenza chiede a un fork di portarsi dietro alla lettera — averlo dal primo giorno significa che chi forka lo trova già, invece di doverselo inventare. Metterlo anche nel pacchetto pubblicato è compito di F5 (§D/F5 punto 5). `README.md`, `docs/FORKING.md`, design `01` §10 e HANDOFF aggiornati; nota in `docs/internal/decisions/2026-09-03-licenza.md`.

**Changelog 0.23** (3 set 2026, sera): chiusa la fase **F4**, la spina dorsale del dominio (PR #6). Due correzioni al design `01`, decise da Carmine e documentate in `docs/internal/decisions/`. (1) **`IProjectable.Project()` riceve un `ProjectionContext`** (lingue della divisione, lingua di default, `BlockDocumentWalker`): un'entità EF non si fa iniettare niente, ma un contenuto per proiettarsi ha bisogno delle lingue — cablarle sarebbe esattamente ciò che un hub forkabile non può fare, e farlo al posto suo nel `ProjectionWriter` toglierebbe a quello la sua unica ragione di esistere, cioè essere generico. Design §3.6 aggiornata. (2) **`ICurrentUser` fa due domande separate**, `Has(permission, department)` («su questa riga?») e `HasAny(permission)` («in generale?»), al posto di un solo metodo con il dipartimento opzionale: il comportamento è quello che §3.7 già pretendeva — senza risorsa basta un dipartimento qualsiasi, altrimenti in F5 la lista del back-office si chiuderebbe in faccia a ogni coordinatore — ma smette di dipendere da cosa significhi un `null`. Design §3.3 e §3.7 aggiornate. Inoltre **`LocaleCatalog` passa da F4 a F5**: il perimetro di F4 non lo elencava e il primo che ne ha davvero bisogno è il `ValidationProblem` di `MapCrud`; con lui si sistema anche il pacchetto pubblicato, che porta le lingue in `wwwroot/locales/` (per la SPA) ma non alla radice, dove le cerca il backend, e non porta affatto i `config/*.example.json` (piano `02` §D/F5).

Allineata inoltre tutta la documentazione a ciò che il codice fa davvero: design `01` a v1.2 (§3.4 — le righe di audit si scrivono nel secondo tempo come le proiezioni, e il flag di rientranza è per contesto, non un campo di `HubDbContext`; §3.5 — i nomi veri delle proprietà del filtro e il fatto che leggono `ICurrentUser` a query lanciata; §5.3 — la firma vera del walker), i **codici di dipartimento del changelog 0.21 che erano rimasti negli esempi** (§9.2 del piano, §3.3/§5.6/§6.4/§7.3/§8 del design, §D/F5-F6-F8 del piano `02`), e la documentazione pubblica: `README.md` e `docs/FORKING.md` dicevano ancora «phase F1» e «phase F0».

**Changelog 0.22** (3 set 2026): il file `config/ivao-oauth.json` guadagna la chiave **`ApiScopes`**, separata da `Scopes`: gli scope che l'applicazione chiede per se' con `client_credentials` non sono quelli che si chiedono al membro (`client_credentials` non ha `openid`, `profile` o `email` da chiedere). **Misurato il 3 set 2026 contro l'API vera**: per `/v2/centers` e `/v2/airports/all` basta un token `client_credentials` **senza alcuno scope**, quindi `ApiScopes` resta vuoto finche' non servira' `tracker` o simili; le credenziali della divisione IT coprono entrambi gli endpoint (7 centri e 221 aeroporti). Le fixture di `Ivao:UseFixtures=true` restano per la CI e per chi forka senza credenziali. Aggiornate §6.1 e il design `01` §2.2 e §4.6.

**Changelog 0.21** (3 set 2026): i codici dei dipartimenti diventano quelli che usa **IVAO**, confermati da Carmine: `HQ`, **`SOD`**, **`FOD`**, **`AOD`**, **`TD`**, **`MD`**, **`ED`**, **`PRD`**, **`WD`** (prima erano `HQ`, `SO`, `FO`, `AO`, `TR`, `MB`, `EV`, `PR`, `WM`). Non è un suffisso meccanico: ATC operations è `AOD` ma training è `TD`, e l'headquarters resta `HQ`. I **suffissi delle posizioni staff** non cambiano (`AOC`, `AOAC`, `AOA1`, `TC`, `TAC`, `TA1`, `T01`…): cambia solo il dipartimento su cui mappano. La colonna `owner_department` passa da `varchar(2)` a `varchar(4)` con una migrazione **additiva** (`WidenDepartmentCodes`) che converte anche le righe già scritte; `Initial` non è stata toccata. Aggiornate §7 e il design `01` §3.2.

**Changelog 0.20** (2 set 2026, notte): le cartelle di runtime prendono un nome **inglese**, coerente con la regola §4.2 «tutto ciò che non è documentazione interna è in inglese»: la cartella dei segreti si chiama **`secrets/`**, quella della diagnostica **`diagnostics/`** e il file di avvio **`startup.txt`** (prima erano `segreti/`, `diagnostica/` e `avvio.txt`). I nomi italiani venivano da vIPI, che resta un riferimento su *come* funziona il deploy su Plesk, non un vincolo sui nomi. Aggiornate §2.5, §11.3, §14 e il design `01` §2.3-2.4; chi forka non trova più una parola italiana dentro una path.

**Changelog 0.19** (2 set 2026, notte): chiarito che un **modulo non è un plugin caricato a runtime**: si aggiunge nel monorepo e si ricompila (niente NuGet di `Core`, niente `AssemblyLoadContext`, niente bundle JS dinamici — scartati per costo, §16.9 e design `01` §6.5). Per lasciare aperta la porta a costo zero, il confine del modulo vale anche nella SPA: tutto il frontend di un modulo sta in `web/src/modules/<key>/` con un manifest unico (blocchi, widget, route, namespace i18n); elenchi **espliciti** dei moduli in `IvaoHub.Web/Modules.cs` e in `web/src/modules/index.ts` (niente scansione degli assembly); regola ESLint che vieta import tra moduli e da `features/` verso `modules/`. §5.1 aggiornata. Aggiungere un modulo = un progetto + una cartella + due righe.

**Changelog 0.18** (2 set 2026, sera): scritti `docs/internal/01-design-m0.md` (firme di `Localized<T>`, interfacce trasversali, interceptor unico con guardia di scrittura per dipartimento, `IProjectable`, grammatica e matrice dei permessi, `MapCrud`, `/api/me`, `IModule`, envelope di `body_json`, set minimo di blocchi, publish con cattura `frozen`, template seedati, convenzioni UI, ricette di routing, test della spina dorsale) e `02-piano-implementazione-m0.md` (fasi F0–F9 con criteri di accettazione e prompt di apertura per Claude Code). Decisioni: **TanStack Router** al posto di React Router (§3.3, §5.3); **deploy su staging Plesk fuori da M0**, spostato a M1 (§13); icone **`lucide-react` confermate** (dipendenza di Atmosphere 3.1.0, §16.C); blocchi Data risolti lato server da `IDataBlockProvider` registrati per tipo (cattura `frozen` nel servizio di pubblicazione, `live` via `/api/blocks/data/{type}`; il backend continua a ignorare `props`); `security_stamp` in `hub_users` per invalidare il cookie al cambio di grant/superadmin; `cms_search_index` con una riga per lingua (`source_module, source_id, locale`) e FULLTEXT su titolo/testo (è una proiezione riscritta a ogni upsert, non una tabella di traduzioni; nessuna colonna cablata per lingua, per la forkabilità); OpenAPI generato a build-time; unicità slug su `(kind, slug, is_template)`; `MapCrud` in modalità dipartimentale/globale; pagine di sistema seedate in M1, in M0 solo i template. Versioni rivalidate: Pomelo resta 9.0.0 (nessuna 10.x), TanStack Router 1.170.

**Changelog 0.17** (2 set 2026): analisi pre-M0 con il criterio «quanto meno codice possibile, ogni pezzo scritto una volta». Blocchi Data con `renderMode` live/frozen catturato alla pubblicazione (§9.3). Nuova **§16 Meccanismi generici** (15 punti decisi): traduzioni in colonna JSON `Localized<T>` al posto delle tabelle `*_translations`; colonne trasversali come interfacce + un interceptor EF + un solo authorization handler per dipartimento; grammatica dei permessi; proiezioni (calendario, ricerca, award) via `IProjectable` nella stessa transazione, senza bus di eventi né MediatR; un solo motore lista+form nel back-office; un solo endpoint di bootstrap; un solo set di file di lingua; un solo progetto `IvaoHub.Core` (niente `Infrastructure`/`Content` separati); niente `/api/v1`; niente prefisso lingua né prerender per ora; roster staff = chi ha fatto login almeno una volta. **§9.3 riscritta**: pagine, news e documenti diventano **un solo contenuto a sezioni** (`cms_contents`, §7) con **template** che sono contenuti anch'essi, sul modello `Document`/`SectionCatalog` di vIPI; regole di propagazione dei template e chi può crearli. §5.1, §5.2, §5.3, §7, §9.1, §9.4, §9.5, §9.7, §13 e §15 allineate. §16.C: convenzioni UI (icone, componenti ammessi, pagina ui-kit) da fissare nel design di M0. §16.E: processo per i cambi in corso d'opera (classifica a/b/c, checklist PR, test della spina dorsale) e `CLAUDE.md` (privato, gitignored, in italiano) come raccolta delle regole operative.

**Changelog 0.16** (1 set 2026, notte): documentazione degli aeroporti/avvicinamenti militari — decisa l'opzione "fonte unica con viste per pubblico": si scrive solo nelle vSOP di vIPI, che **già oggi** permettono di marcare ogni sezione come *per ATC*, *per piloti* o *per tutti*; manca solo l'endpoint API che espone le sezioni piloti (lavoro nel backlog di vIPI) e la resa nell'hub, che le mostra dentro `/pilots` e nella pagina SO come già fa con le statistiche ATC. Aggiornate §9.4 e la tabella collaborazioni in §9.7.

**Changelog 0.15** (1 set 2026, notte): contratto `IModule` **confermato** dopo verifica sui casi reali di collaborazione tra moduli (§9.7): Events↔Training via calendario unico; ATC↔Events via `ref_` + API vIPI; FlightOps↔Events risolto spostando gli **award nel nucleo** (catalogo + assegnazioni, sempre manuali: il sistema *segnala* a chi assegna, mai assegnazione automatica; `Awards.Assign` configurabile per divisione); SpecialOps↔ATC: la documentazione degli aeroporti/avvicinamenti militari — info ATC **e** piloti — resta in vSOP/vIPI curata dal SOD, l'hub la linka. §15.11 chiusa.

**Changelog 0.14** (1 set 2026, sera): nuova **§9.7 Contratti trasversali**: comportamento in `maintenance` (contenuti in sola lettura, azioni 503, job in pausa), widget di dashboard registrati dai moduli, notifiche e preferenze nel nucleo, privacy (nessun profilo membro pubblico nell'hub: si linka il profilo IVAO ufficiale; GDPR allineato alle norme IVAO, niente export dati utente), ricerca globale con indice centrale nel nucleo alimentato dai moduli; bozza del contratto `IModule` ⚠️ in discussione.

**Changelog 0.13** (1 set 2026, sera): Special Operations entra nel catalogo come **primo modulo opzionale** (`modules.specialops`, acceso per IT), contenuto segnaposto da definire col dipartimento SO — il vSOP militare resta in vIPI; blocco `testimonial` aggiunto al set (§9.3), contenuti di proprietà PR; registro **Virtual Airlines** dentro il modulo `flightops`, mostrato in `/pilots`; chiarito che SES (Slot Events) del template HQ per noi è il `booking_mode` dentro Events.

**Changelog 0.12** (1 set 2026): catalogo moduli deciso. Principio "dipartimento = proprietà, modulo = logica" (§9.0); nucleo editoriale department-aware con pagine a blocchi, documenti per dipartimento e calendario unico (§9.1, §9.3–9.5); quattro moduli di dipartimento obbligatori: Events, Flight Ops (tour), Training, ATC (vIPI) (§9.2); Onboarding non è più un modulo ma una pagina; test system sospeso; ordine di uscita Events → Tours → Training (§13); Events senza migrazione dello storico di `ivao-booking` (§12). Ricerca sul backend del template HQ va.ivao.aero (§2.3-ter).

---

## 1. Obiettivi e vincoli

### 1.1 Obiettivi

1. Un unico punto d'ingresso per la community italiana IVAO, con login IVAO, che inglobi i servizi oggi sparsi su siti secondari (training, booking eventi, onboarding, tour; le info operative ATC restano in vIPI, che verrà montato nell'hub) invece di linkarli.
2. Aderenza al design system ufficiale IVAO **Atmosphere**, così che il sito sia riconoscibile come "IVAO 2.0" e non come un sito divisionale fatto in casa.
3. **Forkabilità**: un'altra divisione deve poter clonare il repository, cambiare un file di configurazione e i file di lingua, e avere il proprio hub funzionante. Nessun "IT" hardcodato nel codice.
4. Manutenibilità da parte di **una sola persona**: pochi pezzi mobili, stack che Carmine già padroneggia (C#/.NET), deploy ripetibile.
5. Migrazione dei dati dai servizi esistenti dove ha senso, senza big-bang: i vecchi servizi restano vivi finché il modulo corrispondente non è pronto.

### 1.2 Vincoli non negoziabili

| Vincolo | Implicazione |
|---|---|
| Hosting **Plesk Linux** (Passenger, solo FTP, Cloudflare davanti — vedi §2.5) | Un processo ASP.NET Core self-contained per (sotto)dominio, dietro nginx di Plesk. Niente Docker in produzione, niente shell, niente servizi aggiuntivi (Redis, RabbitMQ). Migrazioni all'avvio, segreti in cartella dedicata. |
| **MariaDB 11.4.10** | Provider EF Core: Pomelo 9.x (supporta esplicitamente MariaDB 11.4). Charset `utf8mb4`, InnoDB, tutte le date in UTC. |
| **Autenticazione IVAO OAuth2/OIDC** | Nessun account locale: l'identità è sempre quella IVAO. Credenziali (client_id/secret) da richiedere a web@ivao.aero con la lista dei redirect URL. |
| **Atmosphere** | È un pacchetto React (`@ivao/atmosphere-react` v3, Tailwind v4, Node ≥ 20, React 18/19). Usarlo appieno vincola il frontend a React. |
| Bilingue IT/EN | i18n dal giorno zero, sia nella UI sia nei contenuti editoriali. |

---

## 2. Cosa ho trovato (ricerca)

### 2.1 Il sito attuale `it.ivao.aero`

Tecnologia: **Blazor Web** (.NET, `blazor.web.js`) + Bootstrap 5.3, con login proprio su `/Account/Login`. Struttura: Home, Chi siamo, Piloti, ATC, Eventi, Special Ops, Calendario attività. Quasi tutta la documentazione è dietro login ("facendo il login puoi avere accesso a tutta la documentazione"). In homepage: widget "ATC schedulati oggi" (da ATC Scheduling HQ), prossime attività, partner random, link ai social.

Il sito attuale è quindi essenzialmente un **portale di contenuti + calendario**; i servizi veri sono altrove.

### 2.2 I servizi satellite oggi

| Servizio | URL | Cosa fa | Tecnologia nota | Proprietà |
|---|---|---|---|---|
| Training (PATS) | `training.ivao.it` | Richiesta training pratici e esami dopo il teorico, accordo date con trainer, mock exam PP/ADC | Web app con login IVAO | Divisione IT |
| QuickOverview | `quickoverview.ivao.it` | Info operative aeroporti/FIR italiane (LIBB, LIMM, LIPP, LIRR), vPIV/FLIP, vAOIS | v2.6.0, pubblico | Divisione IT — **destinato a sparire: vPIV e il resto confluiscono in vIPI** |
| Onboarding wizard | `welcome.it.ivao.aero` | Guida passo-passo per i nuovi membri | JS statico (repo `ivao-italy/onboarding`) | Divisione IT — diventa una **pagina a blocchi** `/start` (§9.3), non un modulo |
| Booking RFE | repo `ivao-italy/ivao-booking` | Prenotazione slot per Real Flight Event | PHP + MySQL, OAuth IVAO | Divisione IT (fork) |
| Tour system | `tours.th.ivao.aero?div=IT` | Tour divisionali, report leg | PHP, gestito da TH | HQ/altra divisione → **modulo `flightops` dell'hub** (§9.2), assorbe il progetto `Ivao Italy Toursystem` e `AutomaticValidatorTour` |
| Test system | — | Esami a crocette anti-AI | progetto Carmine (C#) | Divisione IT — **sospeso**: rientra solo se Carmine lo ripropone |
| **ATC Services (vIPI)** | `atc.it.ivao.aero` | vSOP (documentazione operativa ACC/APP/aeroporti/vLOA per FIR LIBB, LIMM, LIPP, LIRR, guida, vista Live con AoR top-down), vSOP militari, biblioteca allegati (incl. tipo **PIV**), statistiche ATC personali, Aurora Profile Swapper + bridge desktop Aurora, spazi aerei 3D, editor staff con release AIRAC e traduzione IT/EN | **Blazor Server** net8 (librerie multi-target net8/net10), Clean Architecture (`Vipi.Domain/Application/Infrastructure/Ui/Hosting/Host`), EF Core + Pomelo 8 su **MariaDB 11.4.10**, ~5 000 test, OIDC IVAO standalone, Apache-2.0. **È un modulo montabile in-process in un host ASP.NET Core** (`AddVipiModule`/`MapVipiModule`, prefisso fisso `/services/vsop`) | **progetto Carmine, in produzione dal 16 ago 2026 (v1.3.0)**, in `D:\Programmazione\IVAO_Test\vIPI Ivao Italy` |
| ATC Scheduling | `atc.ivao.aero` | Prenotazione posizioni ATC | IVAO 2.0 | **HQ — solo integrazione via API** |
| Calendario training/esami | `it.ivao.aero/events/calendar` | Eventi ATC Training/Exam con trainer/esaminatore | nel sito Blazor | Divisione IT |
| Discord | `discord.ivao.it` | Community | Bot C# (repo `ivao-italy/discord`) | Divisione IT |
| Forum, WebEye, Wiki | HQ | — | — | **HQ — solo link** |

La org GitHub `ivao-italy` è quasi interamente **C#**: `Ivao.It.IvaoApiSdk` (SDK per API IVAO), `Ivao.It.WhazzupData.SDK`, bot Discord, AuroraHelper. Questo è capitale riutilizzabile.

### 2.3 Atmosphere (design system IVAO)

Repository monorepo pnpm con due pacchetti pubblicati su npm:

- **`@ivao/atmosphere-brand` 3.0.0** — sorgente di verità *framework-neutral*: token DTCG (`tokens.json`), CSS custom properties (`--ivao-color-atmos-700`, `--ivao-font-sans`…), adapter tema Tailwind v4 (`theme.css` → utilities `bg-atmos-700`, `text-fuselage-800`, `font-head`).
- **`@ivao/atmosphere-react` 3.1.0** — libreria componenti React basata su shadcn/ui + Radix: accordion, alert, badge, button, calendar, card, carousel, checkbox, command palette, **data-table** (TanStack), date-picker, dialog, dropdown, **navbar**, **navigation-menu**, **sidebar**, pagination, popover, progress, select, sheet, skeleton, slider, switch, table, tabs, toast, tooltip, typography, **dark-mode-toggle**, ivao-logo. Documentazione Storybook su `ivaoaero.github.io/atmosphere/main`.

Palette: `atmos` (blu brand, default 700 `#0d2c99`), `ocean` (blu secondario, default 600), `fuselage` (grigi/neutri 50–950), semantici red/green/yellow/blue, colori prodotto (Aurora verde-teal, Altitude blu notte, Artifice arancio, Creators viola). Font: **Poppins** per i titoli, **Nunito Sans** per il testo, **IBM Plex Mono** per il mono. Dark mode via classe `.dark`. Container max 87.5rem.

Requisiti: Node 20+, React 18.2+, Tailwind CSS v4, browser moderni (Safari 16.4+, Chrome 111+, Firefox 128+).

**Conseguenza:** con React si usa tutto; con Blazor/Razor si userebbero solo i token e si ricostruirebbero ~45 componenti a mano. È la ragione principale della scelta di stack.

### 2.3-bis Il "sito di default" IVAO per le divisioni (`va.ivao.aero`)

HQ propone alle divisioni un sito template (esempio vivo: IVAO Vatican). È una **one-page** con pagebuilder e temi: navbar (logo IVAO, nome divisione, selettore lingua, menu), hero a gradiente navy→blu con eyebrow verde maiuscolo, titolo, due CTA e **tile numeriche** (membri attivi, prossimi eventi); sezioni About, Events, News, Virtual Airlines, Contact (form solo dopo login IVAO); footer con "Staff Login" e i link legali HQ (Terms, Privacy, IP Policy). Il tema `ivao-classic.css` usa **la palette Atmosphere** (`--ivao-primary #0D2C99` = atmos-700, `#091D66` ≈ atmos-800, `#3C55AC` = ocean-600, verde `#2EC662` e rosso `#E93434` semantici), Poppins per la UI e Nunito Sans per i titoli, radius 12px, ombre blu morbide, container 1140px. Niente dark mode.

**Cosa ne prendiamo** (§8): il ritmo della home pubblica (hero + numeri + eventi + news + contatti), le tile numeriche vive, l'eyebrow di sezione, il form di contatto dietro login, il footer legale HQ, il selettore lingua in navbar. **Cosa no**: la struttura one-page ad ancore (l'hub ha molte sezioni vere), il pagebuilder (abbiamo il CMS), l'assenza di dark mode. Conferma utile: un hub in Atmosphere è visivamente coerente con ciò che HQ già propone, e una divisione che passa dal template all'hub non cambia look.

### 2.3-ter Il backend del template HQ (`va.ivao.aero/backend`, v3.7.6) — visto il 1° set 2026

Osservato con il login staff di Carmine (Module Manager, User Management, Audit Trail e Site Settings non accessibili: `no_permission`). Serve come riferimento di *cosa* offrono le divisioni che usano il template, non di *come* lo costruiamo.

- **Catalogo**: Events (scope Divisional/HQ/RFO/RFE; tipo Online/Live/Training/Exam; booking slot), SES – Slot Events, Live Network, Calendar, Tourcenter (tour, award, validazione PIREP, submission award, import, statistiche), TDCenter (richieste, sessioni, group training, disponibilità trainer, calendario training, flight briefing, esiti/storico, staff TD, GCA holders, import, settings), Virtual Airlines, LoA/SOP (documenti con tipo/categoria/versione/stato), Page Builder, News & Posts, Contact Messages, Media Library, Discord Bot, Mail, IVAO API; amministrazione: User Management, Module Manager, Audit Trail, Site Settings. UI in 5 lingue.
- **Page Builder**: pagina = albero **Section** (sfondo, padding S–XL, larghezza narrow/default/wide/full) → **Row** (preset di colonne su griglia 12, gap, allineamento verticale) → **Block**. 24 blocchi in 5 gruppi: Content (Text, Hero, Image, Video, Embed), Layout (Card Grid, Icon Grid, Columns, Gallery, Logo Grid, Tabs), Data (Stats, Network Stats, Virtual Airlines, Calendar, Table, Progress/Timeline), Interactive (Accordion/FAQ, Testimonial, Call to Action, Alert/Notice, Button Group), Structure (Spacer, Divider). Ogni blocco ha un pannello proprietà a form (Card Grid: colonne + per card icona FA, titolo, descrizione, link). I blocchi *Data* leggono dai moduli: è ciò che rende le pagine vive. Pagine su `/page/{slug}`, bozza/pubblicata, template di partenza (Landing, About Us, Services, Events, Contact), anteprima desktop/tablet/mobile.
- **Cosa ne prendiamo**: il modello dati a blocchi e l'idea dei blocchi Data (§9.3); l'organizzazione di TDCenter e Tourcenter come checklist funzionale per Training e Flight Ops. **Cosa no**: il canvas drag & drop (il pezzo più costoso, e mantenuto da HQ), SES come modulo separato (per noi è il `booking_mode` di Events) e LoA/SOP (è vSOP, sta in vIPI). Virtual Airlines, Testimonial e Special Operations rientrano in forma diversa: registro VA in `flightops`, blocco `testimonial` di PR, modulo `specialops` opzionale (§9.6).

### 2.4 Autenticazione IVAO

- Provider **OpenID Connect standard**: discovery su `https://api.ivao.aero/.well-known/openid-configuration`; token endpoint `https://api.ivao.aero/v2/oauth/token`.
- Flussi: **authorization code** (utente, con client secret lato server, PKCE opzionale) e **client credentials** (server-to-server, per chiamare le API IVAO senza utente). Authorization code valido 5 minuti, access token 1 ora, refresh token disponibile.
- Scope documentati: `openid profile email discord location birthday configuration tracker flight_plans:read/write bookings:read/write friends:read/write training supervisor` (alcuni non ancora implementati).
- Userinfo: `GET /v2/users/me` con `Authorization: Bearer`.
- Claim utili per i ruoli: `ivao.aero/staff_positions` e `ivao.aero/permissions` (mappati nel sample ASP.NET Core `Ivao.AspNetCore.Authentication.OpenIdConnect`, che usa `Microsoft.AspNetCore.Authentication.OpenIdConnect` con `GetClaimsFromUserInfoEndpoint = true`, validazione nonce disabilitata, `SaveTokens = true`).
- Credenziali: per lo **sviluppo** si usano le credenziali di test di Carmine (legate al suo account IVAO, riciclabili tra progetti; quelle pubbliche nel README funzionano solo su `/v2/users/me`). Per la **produzione** le credenziali divisionali verranno inserite dalla divisione nel file JSON dedicato al momento del caricamento del sito. La registrazione dell'app lato IVAO richiede due URL: l'**URL di richiesta del login** (la pagina del nostro sito da cui parte il login, es. `https://<dominio>/auth/login`) e l'**URL di redirect** (la callback, es. `https://<dominio>/auth/callback`); entrambi devono coincidere esattamente con quelli configurati nel JSON.
- Nota del README: "IVAO 2.0 websites (Webeye, FPL, Tracker)" sono SPA React con `oidc-client-ts` + PKCE senza backend.

### 2.5 Plesk e MariaDB — com'è davvero il server (dai deploy di vIPI)

Il server di produzione è lo stesso su cui gira oggi `atc.it.ivao.aero`, quindi i suoi `deploy/atc-ivao/LEGGIMI-*.md` sono la fonte più affidabile che abbiamo. Fatti verificati sul campo tra il 16 e il 31 agosto 2026:

| Fatto | Conseguenza per l'hub |
|---|---|
| Sottoscrizione Plesk `it.ivao.aero`; l'app ATC vive in `/var/www/vhosts/it.ivao.aero/public_atc/` | L'hub sarà un'altra cartella della stessa sottoscrizione (`httpdocs/` o `public_hub/`). Stesso utente di sistema (`itivao`). |
| Le app .NET sono avviate da **Phusion Passenger** (start command `dotnet …/X.dll`), **non** dal .NET Toolkit; riavvio toccando `tmp/restart.txt` | Pacchetto **self-contained linux-x64** (il runtime viaggia nel pacchetto: non dipendiamo dalla versione .NET installata → .NET 10 è possibile). |
| Accesso solo **FTP**, confinato alla cartella dell'app; niente shell; i pacchetti li carica **il committente** (staff Ivao.It), non Carmine | Deploy = zip + foglio istruzioni; niente `dotnet ef database update` a mano; le migrazioni girano **all'avvio** dell'app (`Database.Migrate()`), quindi vanno progettate **additive e sicure**. |
| La cartella dell'app **è stata il document root**: `appsettings.Production.json` fu scaricabile (24–25 ago); ora davanti c'è **Cloudflare** e le direttive nginx negano i file sensibili | I segreti stanno in `secrets/<nome-non-indovinabile>.json` (l'app carica ogni `*.json` di quella cartella, che vince su appsettings); deny nginx su `appsettings*.json`, `*.dll`, `*.pdb`, `diagnostics/`, `secrets/`, `keys/`. Forwarded headers da Cloudflare. |
| Data Protection: le chiavi devono stare in una cartella **scrivibile e persistente dentro l'app** (`vipi-keys/`), da non cancellare a ogni upload | Stessa soluzione: `hub-keys/` + avviso in grassetto nel foglio di aggiornamento. Perderla slogga tutti. |
| MariaDB **11.4.10** condivisa: `max_user_connections` ~25–50, pool limitato a 20, `max_allowed_packet` non confermato, **backup non confermato** (A9), utente creato dal pannello con privilegi non verificati | Pool ≤ 15 per l'hub (condivide il tetto con vIPI!), upload file su disco e non in `longblob`, migrazioni che non richiedono `DROP`, e la domanda backup va chiusa **prima** del primo dato reale. |
| WebSocket passano dal proxy (Blazor Server funziona in produzione) | Se un giorno servisse SignalR nell'hub, è fattibile. |
| Un pacchetto consegnato in una "finestra cieca" (nessuno che possa ripristinare) è un rischio reale | Finestre di consegna concordate; ogni pacchetto porta un **timbro di versione** visibile (`/api/version`) e una sonda di verifica post-deploy. |

Note residue:
- **Next.js non è supportato ufficialmente** da Plesk; Node.js serve solo in CI per la build della SPA.
- **Pomelo**: la 9.0.0 (ago 2025) supporta MariaDB 11.4 e EF Core 9; gira su runtime .NET 10. ⚠️ Non esiste ancora Pomelo per EF Core 10. vIPI oggi usa Pomelo 8 su **net8, la cui fine supporto è il 10 novembre 2026**: il problema è comune ai due progetti e va risolto una volta sola (vedi §9 riga 7b e §15).

---

## 3. Decisione di stack

### 3.1 Scelta: **ASP.NET Core 10 (API + host) + React SPA con Atmosphere**, deploy come *un solo processo*

```
┌──────────────────────────── Plesk (dominio hub) ────────────────────────────┐
│  Cloudflare → nginx Plesk → Passenger (avvia `dotnet IvaoHub.Web.dll`)      │
│      │                                                                      │
│      ▼                                                                      │
│  Kestrel — IvaoHub.Web (.NET 10, self-contained)                            │
│   ├── /api/**            → controller/minimal API (JSON)                    │
│   ├── /auth/**           → login/callback/logout OIDC (BFF)                 │
│   ├── /health, /api/version                                                 │
│   ├── /services/vsop/**  → (M4) modulo vIPI Blazor Server montato in-process│
│   └── /**                → wwwroot (React SPA buildata, fallback index.html)│
│                                                                             │
│  Hosted services (job schedulati: sync whazzup, mail, cleanup)              │
│      │                                                                      │
│      ▼                                                                      │
│  MariaDB 11.4 (stesso server Plesk)          api.ivao.aero (HTTPS, OAuth)   │
└─────────────────────────────────────────────────────────────────────────────┘
```

**Perché così:**

- *Un processo, un dominio*: niente CORS, niente secondo sito Node da tenere acceso, un solo punto di deploy. Per un manutentore singolo è la differenza tra "funziona" e "mi si è spento il frontend".
- *Atmosphere nativo*: la SPA React usa `@ivao/atmosphere-react` così com'è; il frontend è visivamente indistinguibile dai siti HQ.
- *Continuità con l'ecosistema IT-DIV*: SDK C# già esistenti (`Ivao.It.IvaoApiSdk`, Whazzup SDK), stesso stack del tour system e del test system → potranno diventare moduli dell'hub o condividere librerie.
- *Plesk-friendly*: pacchetto self-contained avviato da Passenger, esattamente come vIPI oggi; Node serve solo in CI per la build.
- *Sicurezza dei token*: con il pattern **BFF** (Backend-for-Frontend) access/refresh token IVAO restano sul server; il browser ha solo un cookie di sessione `HttpOnly` + `SameSite`. Il client secret non tocca mai il browser.

### 3.2 Alternative scartate

| Alternativa | Perché no |
|---|---|
| Blazor (come oggi) + token Atmosphere | Si perdono tutti i componenti Atmosphere; ricostruirli in Razor è lavoro enorme e diverge dal look HQ. Blazor Server inoltre soffre dietro proxy (SignalR/WebSocket su Plesk). |
| Next.js full-stack | Miglior DX React, ma Plesk non lo supporta ufficialmente; server custom + Node a runtime = fragilità in più. Stack lontano dal C# di Carmine e dagli SDK IT-DIV. |
| Laravel + Inertia/React | Ottimo su Plesk, ma stack nuovo per il manutentore; si perde la condivisione con tour/test system. |
| SPA React "pura" con `oidc-client-ts` (come i siti HQ) | Richiede comunque un backend per DB e job; e il PKCE public client espone gli access token IVAO nel browser. Meglio BFF. |

### 3.3 Versioni di riferimento (da rivalidare al kickoff)

| Componente | Versione | Note |
|---|---|---|
| .NET SDK/runtime | 10.x LTS | self-contained linux-x64, avviato da Passenger |
| EF Core + Pomelo MySql | 9.0.x + 9.0.x | ⚠️ EF Core 9 finché Pomelo 10 non esce |
| MySqlConnector | ≥ 2.4 | dipendenza Pomelo |
| Node (solo build/CI) | 22 LTS | Atmosphere richiede ≥ 20 |
| pnpm | 10/11 | come il monorepo Atmosphere |
| React | 19 | supportato da Atmosphere 3.x |
| Vite | 7 | |
| TypeScript | 5.x | |
| Tailwind CSS | v4 | obbligatorio per Atmosphere 3 |
| `@ivao/atmosphere-react` / `-brand` | 3.1.0 / 3.0.x | dipende da `lucide-react` (set di icone, §16.C) |
| TanStack Query + **TanStack Router** | 5.x / 1.170.x | data fetching e routing type-safe — router **deciso** il 2 set 2026 (search params tipizzati con zod per il motore lista, integrazione nativa con Query); ricette in `01-design-m0.md` §7.3 |
| react-i18next + i18next | ultime | i18n frontend |
| Serilog | ultima | logging strutturato su file (Plesk) |
| Quartz.NET | ultima | job schedulati in-process |
| MailKit | ultima | SMTP (Plesk mail o esterno) |
| FluentValidation, Mapperly | ultime | validazione DTO, mapping source-generated |
| xUnit + Testcontainers (MariaDB) | ultime | test d'integrazione in locale/CI |

---

## 4. Forkabilità: il sito come "prodotto divisionale"

Sì, si può fare — ma va deciso ora, perché costa poco all'inizio e tantissimo dopo. Principio: **il codice non sa di essere italiano**. Tutto ciò che è specifico della divisione vive in tre posti soltanto.

### 4.1 I tre punti di personalizzazione

1. **`division.json`** (o tabella `division_settings` con seed) — **solo ciò di cui il codice ha bisogno per comportarsi**, il minimo indispensabile:
   ```json
   {
     "code": "IT",
     "countryId": "IT",
     "name": { "it": "IVAO Italia", "en": "IVAO Italy" },
     "domain": "it.ivao.aero",
     "defaultLocale": "it",
     "locales": ["it", "en"],
     "timezone": "Europe/Rome",
     "icaoPrefixes": ["LI"],
     "modules": { "specialops": true },
     "superAdmins": [704798],
     "firStaffScope": "all"
   }
   ```
   - `modules`: **solo i moduli opzionali** aggiunti in futuro (§9.6). I quattro moduli di dipartimento — `events`, `flightops`, `training`, `atc` — e il nucleo editoriale sono **sempre presenti** (decisione del 1° set 2026: obbligatori per IT e per chi forka; si spengono solo a caldo con `maintenance`, §4.2). Primo modulo opzionale: `specialops` (§9.2, riga 5), acceso per IT.
   - `superAdmins`: elenco di VID che **bypassano ogni policy** (vedi §6.3). Per IT è Carmine (704798); una divisione che forka mette i propri. ⚠️ È solo il **bootstrap**: viene letto una sola volta, quando la tabella `hub_users` non contiene ancora nessun superadmin; da lì in poi la verità sta nel DB e il file è ignorato (§6.3 spiega perché). Il test di forkabilità gira con la lista vuota.
   - `firStaffScope`: `"all"` (default, come vIPI oggi: CH/ACH/CHAx editano i documenti di tutte le FIR) oppure `"own"` (ogni team FIR accede solo ai contenuti della propria FIR). È una scelta della divisione, non del codice.
   Cosa **non** c'è, e perché:
   - **FIR/centri**: si leggono da IVAO, `GET https://api.ivao.aero/v2/centers?countryId={countryId}` (token `client_credentials`), con cache giornaliera e snapshot in tabella `ivao_centers` così l'hub funziona anche se l'API è giù.
   - **Aeroporti**: idem, `GET https://api.ivao.aero/v2/airports/all?countryId={countryId}&includeRunways=true`, snapshot in `ivao_airports` (con piste). `icaoPrefixes` resta comunque in configurazione come rete di sicurezza per filtri e validazioni che non passano dallo snapshot.
   - **Posizioni staff**: la nomenclatura IVAO è **uguale per tutte le divisioni** (fonte: [IVAO Staff Positions and their Roles](https://wiki.ivao.aero/en/home/ivao/role-descriptions)), con `CODE` di **due o tre caratteri** per la divisione e il **codice ICAO della FIR** per i team FIR. Quindi la mappa sta **nel codice** (`StaffRoleMap`, un solo posto), senza configurazione divisionale. Il filtro applica **due prefissi**: `^{division.code}-` per le posizioni divisionali e `^{fir}-` per ogni FIR presente in `ivao_centers` (es. `LIRR-CH` per la divisione IT). Un override in `division_settings` esiste solo per i casi anomali.

   **`StaffRoleMap` — posizioni divisionali (`{CODE}-…`)**

   | Dipartimento | Suffissi | Ruolo interno | Livello |
   |---|---|---|---|
   | Division HQ | `DIR`, `ADIR` | `Director` | coordinator |
   | Special Operations | `SOC`, `SOAC`, `SOA[1-9]` | `SpecialOps` | coordinator / assistant / advisor |
   | Flight Operations | `FOC`, `FOAC`, `FOA[1-9]` | `FlightOps` | idem |
   | ATC Operations | `AOC`, `AOAC`, `AOA[1-9]` | `AtcOps` | idem |
   | Training | `TC`, `TAC`, `TA[1-9]` | `Training` | idem |
   | Training — trainer | `T(0[1-9]\|[1-9][0-9])` (T01–T99) | `Trainer` | member of department |
   | Membership | `MC`, `MAC`, `MA[1-9]` | `Membership` | coordinator / assistant / advisor |
   | Events | `EC`, `EAC`, `EA[1-9]` | `Events` | idem |
   | Public Relations | `PRC`, `PRAC`, `PRA[1-9]` | `PublicRelations` | idem |
   | Web Development | `WM`, `AWM`, `WMA[1-9]` | `Web` | idem |

   **Posizioni FIR (`{FIR}-…`, es. `LIRR-CH`)**: `CH` → `FirChief`, `ACH` → `FirAssistantChief`, `CHA[1-9]` → `FirAdvisor`, con la FIR come attributo (`fir = LIRR`). Il perimetro lo decide `firStaffScope` in `division.json`: con `"all"` (default, come vIPI oggi) i team FIR editano i contenuti di tutte le FIR; con `"own"` solo quelli della propria. Le policy ricevono la FIR della risorsa e quella dell'utente e applicano l'opzione; il Director e i coordinatori di dipartimento non sono mai limitati per FIR.

   Ogni posizione si risolve in una tripla `(Department, Level, Fir?)`; le policy dell'hub ragionano su quelle (es. `Events.Manage` = `Director` ∪ `Events` con livello coordinator/assistant; `Training.Assign` = `Director` ∪ `Training` coordinator/assistant; `Trainer` vede solo le proprie sessioni). Il pattern `T\d\d` va provato **prima** di `TA\d`, e `TA\d` prima di `TC`/`TAC`: il matching è ordinato, dal più specifico al più generico, e coperto da test con l'elenco completo qui sopra. Le posizioni HQ (senza prefisso divisionale né FIR) danno `HqStaff`, sola lettura.

   **Grant manuali per VID**: la derivazione dai claim è la base, non il tetto. Un coordinatore (o il Director) può concedere a un VID specifico un ruolo o un permesso aggiuntivo — l'esempio tipico: `IT-AOA1` che dà una mano all'Events department riceve `Events.Manage` senza cambiare posizione su IVAO. Ogni grant ha chi lo ha concesso, quando, una scadenza opzionale e finisce nell'audit; i permessi effettivi di un utente sono **unione** di derivati + grant, e l'area `/staff/permissions` li mostra separati (così si vede cosa è "di ruolo" e cosa è concesso). Anche una **revoca** puntuale è possibile (un permesso derivato negato a un VID), per i casi rari.
   - **Link (Discord, social, ANSP nazionale…)**: sono contenuto editoriale, non comportamento → vivono nel CMS (`cms_links`, gestiti dallo staff web dall'area riservata), come qualsiasi altro testo o riferimento.
2. **File di lingua** — `locales/it/*.json`, `locales/en/*.json`, **un solo set** letto sia dalla SPA sia dal backend (mail, messaggi di errore): niente `.resx`, un formato e una cartella per chi traduce (§16.8). Una nuova divisione aggiunge `locales/fr/` e imposta `defaultLocale`.
3. **Contenuti editoriali nel DB** — pagine, news, documenti, FAQ, staff directory, link, partner: mai nel codice. Un seed iniziale crea la struttura vuota + pagine "Lorem" tradotte.

### 4.2 Regole di progetto per restare forkabili

- **Lingua del progetto: inglese.** Tutto il codice (identificatori, nomi di file, tabelle e colonne, chiavi i18n, commenti), i messaggi di commit, i nomi di branch, le issue/PR e tutta la documentazione destinata al pubblico (`README.md`, `FORKING.md`, `docs/api`, ADR, changelog, `.example` di configurazione, fogli di deploy) sono in **inglese**: chi forka non deve capire l'italiano. Unica eccezione voluta: la documentazione **interna di progetto** — questo piano, i documenti di design dei moduli, l'`HANDOFF` — resta in italiano perché la legge Carmine ogni giorno, e vive separata in `docs/internal/` (esclusa dai link del README e con una nota in testa che dice che è interna). Le stringhe italiane esistono in un solo posto: `locales/it/`.
- Nessuna stringa visibile all'utente nel codice: sempre chiave i18n.
- Nessun codice ICAO, nome FIR, posizione staff o URL italiano nel codice: FIR e centri dall'API IVAO, il resto da `division.json`/DB.
- I **moduli sono feature flag su due livelli**, perché su Plesk "riavviare" significa FTP + `tmp/restart.txt` e non deve essere l'unico modo per spegnere qualcosa:
  - `modules.<nome> = false` in `division.json` (letto **all'avvio**): il modulo non viene registrato nel processo — niente rotte, menu, job, né nuove migrazioni. È la scelta strutturale di *quali moduli usa questa divisione*, cambia raramente e richiede un riavvio. ⚠️ Le migrazioni già applicate **non vengono mai annullate** e i dati restano: disattivare nasconde, non cancella; riattivare riporta tutto com'era. All'avvio l'app avvisa nel log se un modulo disattivato ha ancora tabelle popolate.
  - `maintenance` **a caldo** da `/staff/modules` (Director e superadmin, con audit): il modulo resta caricato ma risponde 503 con una pagina cortese e tradotta, sparisce dal menu e i suoi job vanno in pausa. Nessun riavvio, effetto immediato, reversibile con un clic. È il livello per "il booking ha un problema, spegnilo finché non lo sistemo" e per le finestre di manutenzione annunciate.
  - Un modulo disattivato dal file non può essere acceso dall'interfaccia (l'interfaccia non lo vede proprio): il file è il tetto, l'interfaccia lavora sotto.
- Il ruolo dell'utente deriva dai claim IVAO `userStaffPositions` filtrati per `^{division.code}-` e per `^{fir}-` (FIR da `ivao_centers`), con il suffisso mappato dalla `StaffRoleMap` universale. Così una divisione XX o XXX funziona senza toccare codice né configurazione.
- Brand: Atmosphere è uguale per tutti (è il punto). Personalizzabile solo: logo secondario divisionale, immagini hero, colore d'accento opzionale entro la palette Atmosphere.
- Licenza open source **Apache-2.0** (§15.5, decisa il 3 set 2026), `README` di fork in inglese, template `.env.example`, `docs/FORKING.md`.
- Un **test automatico** che avvia l'app con `division.json` di una divisione fittizia ("XX") in lingua `en` e verifica che non compaiano stringhe italiane né riferimenti IT: è la rete di sicurezza contro le regressioni di forkabilità.

### 4.3 Cosa resta per forza della divisione che forka

Credenziali OAuth (ogni divisione registra la propria app con i propri login/redirect URL e le inserisce in `config/ivao-oauth.json`), server Plesk/DB, SMTP, contenuti, traduzioni. Il repository fornisce tutto il resto.

---

## 5. Architettura applicativa

### 5.1 Struttura del repository (monorepo)

```
ivao-division-hub/
├── src/
│   ├── IvaoHub.Web/              # host ASP.NET Core: Program.cs, auth, static SPA, DI dei moduli
│   ├── IvaoHub.Core/             # NUCLEO, un solo progetto (§16.9): dominio (utenti, divisione, ruoli, i18n),
│   │   ├── Data/                 #   EF Core (Pomelo), interceptor, migrazioni del nucleo
│   │   ├── Ivao/                 #   client API IVAO, snapshot ref_
│   │   ├── Content/              #   contenuti a sezioni (pagine/news/documenti), calendario unico, media,
│   │   │                         #   contatti, staff directory, award — tutto con owner_department
│   │   └── Services/             #   mail/notifiche, Quartz, live status, ricerca
│   ├── IvaoHub.Modules.Events/   # Events dept: eventi, slot RFE/RFO, booking
│   ├── IvaoHub.Modules.FlightOps/# Flight Ops dept: tour, leg, award, validatore automatico (ex Ivao Italy Toursystem)
│   ├── IvaoHub.Modules.Training/ # Training dept: richieste, trainer, disponibilità, sessioni, esiti, mock exam
│   ├── IvaoHub.Modules.Atc/      # ATC Ops dept: sezione /atc, card e deep link a vIPI; in M5 il montaggio in-process
│   └── IvaoHub.Modules.SpecialOps/ # SOD dept (OPZIONALE, modules.specialops): contenuto da definire col dipartimento
├── web/                          # React SPA (Vite + TS + Atmosphere)
│   ├── src/app/                  # router, layout, providers
│   ├── src/blocks/               # componenti React dei blocchi pagina (§9.3): registry nome → componente
│   ├── src/features/<area>/      # nucleo (auth, me, staff, admin, content, links)
│   ├── src/modules/<key>/        # TUTTO il frontend di un modulo: manifest (blocchi, widget, route, i18n) — design 01 §6.5
│   ├── src/shared/               # api client generato, hooks, i18n, componenti comuni
│   └── locales/{it,en}/
├── tests/
│   ├── IvaoHub.UnitTests/
│   └── IvaoHub.IntegrationTests/ # Testcontainers MariaDB 11.4
├── config/
│   ├── division.json             # identità divisione (IT di default)
│   └── division.example.json
├── deploy/                       # script Plesk, appsettings.Production.template.json
├── docs/                         # EN, pubblica: FORKING.md, ADR, API.md, deploy
│   └── internal/                 # IT, interna: questo piano, design dei moduli, HANDOFF
├── docker-compose.yml            # MariaDB 11.4 + mailpit per lo sviluppo locale
└── .github/workflows/            # build, test, release artifact
```

**Modular monolith**: un solo deploy, ma ogni modulo ha il proprio `DbContext` (o schema-prefix `trn_`, `evt_`…), le proprie rotte `/api/<modulo>/…`, le proprie migrazioni e il proprio `IModule` che si auto-registra se abilitato in `division.json` e che espone un interruttore `maintenance` a caldo (vedi §4.2). I moduli comunicano tramite interfacce di `Core`; ciò che proiettano nel nucleo (calendario, ricerca, segnalazioni award) passa da `IProjectable` e dall'interceptor EF (§16.4), non da un bus di eventi (niente MediatR, che dal 2025 è a licenza commerciale). Mai join cross-modulo, mai FK tra contesti (§16.12).

### 5.2 Backend — layer e convenzioni

- **API**: minimal API o controller con `[ApiController]`, DTO espliciti, `ProblemDetails` per gli errori, **nessun versionamento** (`/api/...`: frontend e backend viaggiano nello stesso pacchetto, §16.10). OpenAPI generato (`Microsoft.AspNetCore.OpenApi` + Scalar UI in dev).
- **Client TypeScript generato** dall'OpenAPI in CI (`openapi-typescript` + `openapi-fetch`): il frontend non scrive mai fetch a mano e rompe la build se il contratto cambia.
- **Persistenza**: EF Core code-first, migrazioni per modulo, `DateTime` sempre UTC (`datetime(6)`), chiavi `int`/`bigint` autoincrement per le tabelle interne e **VID IVAO come identificatore naturale dell'utente**.
- **Job**: Quartz.NET in-process (Plesk = un processo): sync periodico dati IVAO (whazzup, ATC online, booking), invio mail in coda, pulizia sessioni. Tabella `jobs_log`.
- **Cache**: `IMemoryCache`/`HybridCache` per le risposte API IVAO (whazzup 15–60 s, dati statici ore). Niente Redis.
- **Configurazione**: `appsettings.json` + variabili d'ambiente Plesk per i segreti (`IVAO__ClientSecret`, `ConnectionStrings__Default`, `Smtp__Password`). Mai segreti nel repo.
- **Logging**: Serilog → file rolling in `logs/` + console; livello configurabile; correlation id per richiesta.

### 5.3 Frontend — struttura e convenzioni

- Vite + React 19 + TypeScript strict, Tailwind v4 con `@import '@ivao/atmosphere-react/theme.css'`.
- Routing: **TanStack Router** (file-based, type-safe; deciso il 2 set 2026, tre ricette in `01-design-m0.md` §7.3). Layout a due livelli: **pubblico** (navbar Atmosphere + footer) e **area riservata** (navbar + `Sidebar` Atmosphere con i moduli abilitati).
- Stato server: TanStack Query; stato UI locale: React state/`zustand` se serve.
- Form: `react-hook-form` + `zod`; **un solo generatore di form dallo schema zod** per blocchi ed entità (§16.6). Regola: *valida il server, il client mostra* i `ProblemDetails` campo per campo — nessuna regola scritta due volte.
- i18n: `react-i18next`, namespace per modulo, lingua da profilo utente → cookie → `Accept-Language` → `defaultLocale`. Date/numeri con `Intl` e timezone della divisione, ma **orari operativi sempre anche in UTC** (standard IVAO).
- Accessibilità: Atmosphere è Radix-based (già accessibile); si mantiene focus visibile, contrasto, `aria-*` sulle tabelle custom.
- Build: `pnpm build` produce `web/dist` che la pipeline copia in `IvaoHub.Web/wwwroot`. In sviluppo Vite gira su `:5173` con proxy verso Kestrel `:5000`.

---

## 6. Autenticazione, sessione e autorizzazione

### 6.1 Flusso di login (BFF, authorization code)

1. La SPA fa `GET /auth/login?returnUrl=/dashboard` → il backend genera `state` (+ `nonce`, + PKCE anche se non obbligatorio) e redirige a `authorization_endpoint` con scope `openid profile email discord` (+ `tracker`, `bookings:read` se il modulo lo richiede).
2. IVAO riporta l'utente su `/auth/callback?code&state` → il backend scambia il code (`grant_type=authorization_code`, client secret) e ottiene access/refresh token.
3. Il backend chiama `/v2/users/me`, **crea/aggiorna l'utente locale** (VID, nome, rating, divisione, staff positions, permessi, discord id, lingua) e calcola i ruoli interni.
4. Emette un **cookie di sessione** `HttpOnly; Secure; SameSite=Lax` (ASP.NET Core cookie auth, ticket criptato con Data Protection su file system Plesk). I token IVAO restano nel ticket lato server o in tabella `user_tokens` cifrata.
5. Un middleware rinnova l'access token con il refresh token quando serve chiamare le API IVAO per conto dell'utente.
6. `POST /auth/logout` cancella cookie e token; opzionale `end_session_endpoint` se esposto dal provider.

Punto di partenza del codice: **`Vipi.Host/Auth/VipiStandaloneAuthExtensions.cs`**, già collaudato in produzione contro l'IdP IVAO, non il sample GitHub. Da lì si ereditano le scelte che costano settimane a riscoprire: `ResponseType=code` + `UsePkce=true`, i **claim reali** dell'userinfo IVAO (`id` = VID, `centerId`, `firstName`, `lastName`, `publicNickname`, `userStaffPositions` come array di oggetti → mappato ai soli codici, `ivao.aero/permissions` scartato), `OnRemoteFailure` gestito con pagina dedicata, `AllowedHosts` bloccato sul dominio (altrimenti il `redirect_uri` viene costruito dall'header Host), `SaveTokens=false` per non gonfiare il cookie. L'hub si discosta su un solo punto: poiché alcuni moduli chiamano le API IVAO per conto dell'utente, access/refresh token vengono salvati **in tabella cifrata** (`user_tokens`, Data Protection), mai nel cookie. Schema cookie applicativo, **niente tabelle Identity**. Lo stesso codice, estratto in una libreria `IvaoHub.Auth`, può servire app satellite future (il test system, se tornerà; il tour system invece è un modulo dell'hub e non ne ha bisogno).

**File credenziali dedicato — `config/ivao-oauth.json`** (fuori dal repository, in `.gitignore`; nel repo c'è solo `ivao-oauth.example.json`):

```json
{
  "Ivao": {
    "Authority": "https://api.ivao.aero",
    "ClientId": "<client id>",
    "ClientSecret": "<client secret>",
    "LoginUrl": "https://it.ivao.aero/auth/login",
    "RedirectUri": "https://it.ivao.aero/auth/callback",
    "PostLogoutRedirectUri": "https://it.ivao.aero/",
    "Scopes": ["openid", "profile", "email", "discord"],
    "ApiScopes": []
  }
}
```

Regole: il file è caricato all'avvio con `AddJsonFile("config/ivao-oauth.json", optional: false, reloadOnChange: true)`; l'app **rifiuta di partire** se manca un campo o se `RedirectUri` non termina con `/auth/callback`; `LoginUrl` e `RedirectUri` devono coincidere carattere per carattere con quelli registrati su IVAO (schema, host, porta, path, niente slash finale in più). In sviluppo il file contiene le credenziali di test di Carmine con `http://localhost:5173/auth/login` e `http://localhost:5173/auth/callback` (o le URL autorizzate per quelle credenziali); in produzione lo compila la divisione al caricamento del sito. Le variabili d'ambiente Plesk (`Ivao__ClientSecret`) restano supportate come alternativa e, se presenti, hanno la precedenza sul JSON. Il segreto non viene mai loggato né esposto da `/api`.

### 6.2 Server-to-server

Un `IvaoApiClient` con `client_credentials` (scope in `ApiScopes`, separati da quelli del membro; **misurato il 3 set 2026**: centri e aeroporti non ne richiedono nessuno) e token cache per: whazzup/tracker (chi è online in FIR italiane), ATC bookings, dati aeroporti. Un solo client tipizzato, retry con Polly, rate limit rispettoso.

### 6.3 Autorizzazione

- **Ruoli derivati, mai assegnati a mano** come default: `Member` (tutti), `Staff` (almeno una posizione `{code}-*` o `{fir}-*`), ruoli funzionali dalla `StaffRoleMap` universale nel codice (tabella completa in §4.1: dipartimento + livello + FIR opzionale), `HqStaff` (posizioni non divisionali, sola lettura).
- **Superadmin** (per IT il VID 704798): bypassa **ogni** policy e vede ogni area, modulo e ambiente, per poter verificare l'intero sistema senza dover simulare posizioni. È l'unico ruolo non derivato né concesso.

  *Modello di minaccia, detto onestamente*: chi ha l'FTP sulla cartella dell'app controlla già tutto (segreti, chiavi Data Protection con cui si forgia un cookie per qualsiasi VID, i binari stessi). Nessuna configurazione può difendere da quel livello di accesso; lo staff che carica i pacchetti è nella cerchia di fiducia per costruzione. L'obiettivo è quindi che cambiare il superadmin **via file non serva a nulla**, e che qualsiasi cambio sia **visibile e attribuibile**:
  - la verità è la colonna `hub_users.is_superadmin` nel **DB**; `division.json → superAdmins` è letto **solo al bootstrap**, cioè ogni volta che nel DB **non c'è nessun superadmin attivo** (primo avvio, oppure dopo che l'ultimo è stato rimosso) e poi ignorato — modificare il file su Plesk mentre esiste un superadmin non ha effetto. È anche la via di recupero della divisione se resta senza superadmin;
  - il superadmin è il ruolo del **manutentore del sistema**, non un ruolo divisionale: è **indipendente dalle posizioni staff IVAO** e non decade se la persona le perde. Nessun automatismo, nessuna notifica, nessun badge legati allo stato staff del superadmin (deciso da Carmine): si aggiunge e si rimuove solo come descritto sopra;
  - i cambi avvengono solo da `/staff/permissions`, da parte di un superadmin esistente, mai per grant; impossibile rimuovere l'ultimo superadmin;
  - ogni **cambio**, ogni **login** in veste di superadmin e ogni **avvio** in cui l'insieme effettivo dei superadmin differisce dall'ultimo noto (hash salvato in `division_settings`) genera una **email a tutti i superadmin** e una riga di audit con flag `superadmin`; l'interfaccia mostra un badge permanente quando si opera in quella veste;
  - un cambio "fuori banda" resta possibile solo scrivendo nel DB o sostituendo il binario: azioni più grosse, più rumorose e più facili da attribuire di una riga di JSON — e la notifica all'avvio le fa emergere comunque;
  - in staging/sviluppo il superadmin può **impersonare** un altro VID in sola lettura (`/staff/impersonate`), spento in produzione salvo esplicita abilitazione.
  Se in futuro si volesse alzare ancora l'asticella: firma dell'elenco superadmin con una chiave privata di Carmine e verifica con la chiave pubblica compilata nel binario — costringe a ricompilare per manomettere. Non è previsto in M0.
- **Grant manuali per VID** (tabella `user_grants`): ruoli o singoli permessi concessi o revocati a un VID specifico da chi ha `Permissions.Manage` (Director, coordinatori per il proprio dipartimento), con `granted_by`, `granted_at`, `expires_at`, motivo, audit. Casi d'uso: uno staffista che aiuta un altro dipartimento (`IT-AOA1` → `Events.Manage`), un permesso temporaneo per un evento. Permessi effettivi = derivati dai claim ∪ grant − revoche; ricalcolati a ogni login e cacheati nella sessione.
  **Salvagente**: un grant si può concedere **solo a chi ha già almeno una posizione staff** derivata dai claim IVAO (divisionale o FIR). L'interfaccia non propone nemmeno gli altri VID, e il server lo verifica comunque. Se l'utente perde tutte le posizioni staff (rilevato al login o dal sync giornaliero del roster), i suoi grant vengono **sospesi** automaticamente (non cancellati: tornano attivi se rientra nello staff) e i superadmin ricevono una notifica. Un grant non può mai conferire `Permissions.Manage` né lo stato di superadmin. Così nessuno può "aprire" il sistema a un VID qualsiasi: il perimetro dello staff lo decide sempre IVAO.
- Policy ASP.NET Core (`[Authorize(Policy = "Training.Manage")]`) + le stesse policy esposte alla SPA in `/api/me` per nascondere menu e pulsanti (la sicurezza vera è sempre lato server).
- Tabella `audit_log` per ogni azione di staff (chi, cosa, quando, prima/dopo).

### 6.4 Sicurezza trasversale

CSRF: cookie `SameSite=Lax` + header custom `X-Requested-With` richiesto sulle mutazioni + antiforgery token per i form. CSP restrittiva (self + `static.ivao.aero` per il logo). Rate limiting su `/auth/*` e sulle API pubbliche. HSTS. Segreti solo via env. GDPR: pagina privacy, export/cancellazione dati utente su richiesta, retention log 90 giorni, dati IVAO minimi (niente email se non serve al modulo).

---

## 7. Modello dati (nucleo)

**Regola sulle chiavi**: ogni tabella ha una PK esplicita (InnoDB altrimenti usa un row-id nascosto, la replica per riga degrada e EF Core mappa le entità senza chiave solo in sola lettura). Chiave **naturale** dove esiste ed è stabile, **composta** per le associazioni, **surrogata** `id BIGINT AUTO_INCREMENT` per tutto ciò che è storico o a righe multiple.

Schema `hub_` — identità e permessi, condiviso da tutti i moduli:

| Tabella | PK | Altri campi | Note |
|---|---|---|---|
| `users` | `vid` | `first_name`, `last_name`, `division_code`, `country`, `rating_atc`, `rating_pilot`, `discord_id`, `locale`, `is_staff`, `is_superadmin`, `last_login_at`, `created_at` | fonte: `/v2/users/me` ad ogni login; `is_superadmin` solo da bootstrap o da un altro superadmin |
| `user_staff_positions` | `(vid, position)` | `department`, `level`, `fir`, `synced_at` | snapshot dei claim + colonne derivate dalla `StaffRoleMap` |
| `user_grants` | `id` | `vid FK`, `kind` (role/permission), `value`, `effect` (grant/deny), `granted_by`, `granted_at`, `expires_at`, `suspended_at`, `reason` | permessi per VID oltre a quelli derivati; indice `(vid, effect)` |
| `user_tokens` | `vid` | `access_token_enc`, `refresh_token_enc`, `expires_at`, `scopes` | uno-a-uno con `users`, cifrati con Data Protection |
| `division_settings` | `key` | `value_json`, `updated_by`, `updated_at` | override runtime di `division.json` |
| `audit_log` | `id` | `vid`, `action`, `entity`, `entity_id`, `before_json`, `after_json`, `ip`, `is_superadmin`, `at` | indice `(entity, entity_id)`, `(vid, at)` |
| `jobs_log` | `id` | `job`, `started_at`, `finished_at`, `status`, `message` | indice `(job, started_at)` |

Schema `ref_` — **dati di riferimento IVAO**, nel nucleo perché servono a più moduli (Events per gli aeroporti degli slot, il nucleo stesso per riconoscere le posizioni staff FIR, il live status per le FIR online) e il nucleo non può dipendere da un modulo opzionale. Sono in sola lettura per l'app e alimentati dai job giornalieri; vIPI, quando montato, continua a usare il proprio `IAirportDirectory` sul proprio DB:

| Tabella | PK | Altri campi | Note |
|---|---|---|---|
| `ivao_centers` | `id` (es. `LIRR`) | `name`, `country_id`, `raw_json`, `synced_at` | snapshot di `/v2/centers?countryId=…`: le FIR non si configurano |
| `ivao_airports` | `icao` | `name`, `country_id`, `center_id FK`, `runways_json`, `raw_json`, `synced_at` | snapshot di `/v2/airports/all?countryId=…&includeRunways=true`; indice `(country_id)`, `(center_id)` |

**Convenzione `owner_department`** (§9.0): ogni riga editoriale o operativa — pagina, news, documento, voce di calendario, messaggio di contatto, evento, tour, sessione — porta `owner_department` (enum: `HQ`, `SOD`, `FOD`, `AOD`, `TD`, `MD`, `ED`, `PRD`, `WD` — i codici che usa IVAO, non un suffisso meccanico; stesso vocabolario di `StaffRoleMap`). Le policy confrontano quel valore con i dipartimenti delle posizioni staff dell'utente; `Director` e `Web` (`WD`) passano sempre. Indice su `(owner_department, status)` ovunque.

**Convenzione traduzioni** (§16.1): nessuna tabella `*_translations`. Ogni campo tradotto è una colonna JSON `{ "it": …, "en": … }` mappata su `Localized<T>`; un solo converter EF, un solo componente di editing, un solo validatore «tutte le lingue di `division.locales` prima di pubblicare». Nelle tabelle qui sotto i campi `*_i18n` sono di questo tipo.

Schema `cms_` (**nucleo** editoriale, cartella `Content` di `IvaoHub.Core`), tutte con `id` surrogata salvo dove indicato:

| Tabella | PK | Campi principali | Note |
|---|---|---|---|
| `contents` | `id` | `kind` (page/news/document), `slug` (univoco per kind), `owner_department`, `visibility` (public/members/staff/department), `status` (draft/published), `template_id FK → contents` (nullable), `is_template`, `title_i18n`, `summary_i18n`, `seo_i18n`, `body_json` (albero sezioni/blocchi, §9.3, con `schema_version`), `published_version_id`, `published_at`, `updated_by`; per `news`: `category`, `cover_media_id`, `pinned`; per `document`: `category`, `sort`, `file_media_id` (nullable: documento-file) | **Un solo contenuto** per pagine, news e documenti (§9.3); `/start`, `/pilots`, `/about`, la home sono righe `kind = page`. Indici `(kind, status)`, `(owner_department, status)`, `(template_id)` |
| `content_versions` | `id` | `content_id FK`, `version`, `title_i18n`, `body_json`, `changelog`, `published_at`, `published_by` | fotografia **congelata** di ciò che il pubblico vede; il pubblico legge sempre la versione pubblicata, mai la bozza (§9.3) |
| `calendar_entries` | `id` | `owner_department`, `kind` (event/rfe/training/exam/tour/meeting/deadline/other), `starts_at_utc`, `ends_at_utc`, `all_day`, `visibility`, `source_module`, `source_id`, `url`, `title_i18n`, `description_i18n`, `created_by` | §9.5: **un calendario per tutto**; le voci dei moduli sono proiezioni `IProjectable` (`source_module` + `source_id` univoci, §16.4), quelle interne (riunioni, scadenze) create a mano dallo staff |
| `search_index` | `id` | `source_module`, `source_id`, `kind`, `url`, `owner_department`, `visibility`, `title_i18n`, `text_i18n` (FULLTEXT) | §9.7: proiezione `IProjectable`, stesso meccanismo del calendario |
| `media` | `id` | `owner_department`, `kind` (image/file), `path`, `mime`, `size`, `width`, `height`, `alt_i18n`, `uploaded_by` | storage su disco sotto `data/media/`, limite di upload esplicito (proxy Plesk) |
| `contact_messages` | `id` | `to_department`, `from_vid`, `subject`, `body`, `status`, `handled_by`, `handled_at` | il form pubblico è per autenticati; ogni dipartimento vede i propri |
| `staff_directory` | `position` | `vid`, `sort`, `description_i18n` | dal roster (chi ha fatto login, §16.13) + ordinamento editoriale |
| `links`, `partners`, `faq` | `id` | `owner_department`, campi `*_i18n` | `faq` è anche un blocco Accordion; `links` è l'entità-cavia di M0 (§16.15) |
| `awards`, `award_assignments`, `award_signals` | `id` | catalogo (`name_i18n`, `image_media_id`, `owner_department`, `criteria_i18n`); assegnazioni (`award_id`, `vid`, `reason`, `assigned_by`, `assigned_at`); segnalazioni (`source_module`, `source_id`, `vid`, `reason`, `status`) | §9.1/§9.7: le segnalazioni sono una proiezione `IProjectable` come calendario e ricerca |

Schema `evt_` (modulo Events), `id` surrogata: `events` (tipo: RFE/online day/training/exam/…, `starts_at_utc`, `ends_at_utc`, `airport_icao FK → ref_.ivao_airports`, banner, visibility, `booking_mode`, `title_i18n`, `description_json` a sezioni §9.3), `event_slots` (callsign, dep/arr ICAO, times, aircraft, `booked_by_vid`, stato; indice univoco `(event_id, callsign)`), `event_participants` (PK `(event_id, vid)`), `event_atc_positions` (PK `(event_id, position)`).

Gli schemi degli altri moduli (`fo_` Flight Ops/tour + registro VA, `trn_` Training, `so_` Special Ops) si definiscono nel documento di design di ciascun modulo (vedi §9.2); il modulo `atc` non ha tabelle proprie finché vIPI non è montato (usa il suo DB).

Convenzioni MariaDB: `utf8mb4_unicode_ci`, InnoDB, `datetime(6)` UTC, soft delete solo dove serve storicità, indici su ogni FK e sui campi di ricerca, `JSON` nativo MariaDB per i campi flessibili (`value_json`, `before_json`).

---

## 8. Design e architettura dell'informazione

### 8.1 Principi

- **Atmosphere così com'è**: stessa navbar (logo IVAO + divisore + titolo "Italy"), stessi radius, stesse card. La personalità divisionale sta nei contenuti e nelle foto, non nei colori.
- **Due mondi, una navigazione**: area pubblica editoriale (chi siamo, come iniziare, eventi, news) e area riservata operativa (dashboard personale, moduli). Il login non è un muro: le pagine pubbliche sono davvero pubbliche (oggi non lo sono), l'accesso sblocca i servizi.
- **Dashboard personale come home post-login**: "cosa posso fare oggi" — prossimi eventi a cui sono iscritto, richieste training in corso, mie prenotazioni, ATC online in Italia adesso, avvisi staff.
- **Dark mode** di serie (Atmosphere la fornisce), preferenza salvata nel profilo.
- **Mobile-first per la consultazione**, desktop per la gestione (data-table, back-office).

### 8.2 Sitemap proposta

```
/                          Home (ispirata al template HQ, §2.3-bis): hero con eyebrow + titolo + CTA "inizia qui" (pilota/ATC) + tile numeriche vive (membri attivi, ATC online, piloti in area, prossimi eventi), poi prossimi eventi, news, "come iniziare", contatti (form dietro login)
/start                     Onboarding: pagina a blocchi (Timeline + Card + CTA) che sostituisce welcome.it.ivao.aero — non è un modulo
/pilots                    Sezione piloti (Flight Ops): guide, documenti del dipartimento, link software, card verso i tour, registro Virtual Airlines (Logo Grid)
/atc                       Sezione ATC: carriera, rating, posizioni, sector file + card verso vIPI (vSOP, statistiche, Profile Swapper)
/services/vsop/**          vIPI (vSOP, vPIV, spazi aerei, statistiche) — oggi su atc.it.ivao.aero, in M4 montato qui
/events                    Calendario + lista eventi; /events/{slug} dettaglio + booking slot
/training                  Modulo Training: richieste training/esami, disponibilità trainer, sessioni, esiti, mock exam
/tours, /tours/{slug}      Modulo Flight Ops: tour, leg, classifica, award; /tours/{slug}/report per il PIREP
/calendar                  Calendario unico (eventi, RFE, training, esami, tour; voci interne solo per staff)
/docs, /docs/{dept}        Documenti per dipartimento (visibilità per ruolo)
/news, /news/{slug}
/about                     Divisione, staff directory (da claim IVAO), partner, contatti
/me                        Dashboard personale; /me/profile, /me/bookings, /me/training, /me/tours
/staff                     Back-office: entri e vedi SOLO il tuo dipartimento (§9.0); DIR/ADIR/WM vedono tutti
/staff/{dept}/**           Spazio del dipartimento: le sue pagine, news, documenti, voci di calendario, contatti + le schermate del suo modulo (es. /staff/ev/events, /staff/tr/requests, /staff/fo/tours)
/staff/admin/**            Solo Director/WM/superadmin: utenti e grant, moduli/maintenance, impostazioni divisione, audit
/{locale}/...              prefisso lingua opzionale per SEO delle pagine pubbliche
```

### 8.3 Componenti chiave (tutti da Atmosphere)

Navbar + NavigationMenu (pubblico), Sidebar (riservato/staff), Card (eventi, moduli), DataTable (slot, richieste, utenti), Calendar/DatePicker (eventi, disponibilità trainer), Dialog/Sheet (booking, form rapidi), Badge (rating, stato), Tabs, Toast, Command palette (`⌘K` per staff: cerca utente/evento/pagina), DarkModeToggle, Skeleton per il loading.

Componenti custom (pochi, costruiti con i token): `Hero` (gradiente atmos-800→atmos-600, eyebrow verde, CTA), `StatTile` (numero grande + etichetta, dati vivi), `SectionHeader` (eyebrow + titolo, come nel template HQ), `LiveStatusStrip` (ATC/piloti online), `RatingBadge`, `AirportCard`, `EventTimeline`, `LocaleSwitcher`, `MarkdownContent`, `ContactForm` (visibile solo autenticati). Footer con link legali HQ (Terms of Use, Privacy Policy, IP Policy) e "Staff area".

---

## 9. Catalogo moduli — deciso il 1° settembre 2026

### 9.0 Il principio: il dipartimento è l'asse di proprietà, il modulo è il confine del codice

L'idea di partenza di Carmine era "un modulo per dipartimento, e dentro ciò che serve al dipartimento". Presa alla lettera duplicherebbe news, documenti e calendario in ogni dipartimento (tre sistemi news, sette calendari); presa nel modo giusto è la spina dorsale di tutto l'hub. La forma decisa:

- **Ogni contenuto appartiene a un dipartimento.** Pagine, news, documenti, voci di calendario, messaggi di contatto, eventi, tour, sessioni di training: tutti hanno `owner_department` obbligatorio (§7). Quel campo decide tre cose, senza regole aggiuntive: *chi può modificarlo* (lo staff del dipartimento, più Director e Web; gli altri via grant per VID, §6.3), *dove compare nel back-office* e *come si filtra sul sito pubblico* (`/training` mostra automaticamente news, documenti ed eventi del Training).
- **Il back-office è organizzato per dipartimento.** Uno staff Events entra in `/staff` e trova "Events Department": i suoi eventi, le sue news, i suoi documenti, le sue voci di calendario, i contatti ricevuti. **Non vede** gli altri dipartimenti (deciso: nessuna lettura trasversale; chi aiuta un altro dipartimento riceve un grant). Director, Assistant Director e Web vedono tutto; il superadmin anche.
- **I servizi comuni si scrivono una volta sola** e stanno nel *nucleo editoriale* (cartella `Content` di `IvaoHub.Core`): non sono un modulo, non si spengono, portano l'etichetta del dipartimento su ogni riga.
- **Un modulo di codice esiste solo dove un dipartimento ha logica che nessun altro ha**: Events (slot e prenotazioni), Flight Operations (tour, leg, award, validatore), Training (richieste, trainer, sessioni, esiti), ATC Operations (sezione `/atc` e vIPI). Membership, PR e Web usano i servizi comuni e hanno il loro spazio nel back-office, ma nessun modulo di codice finché non serve qualcosa di specifico (allora nasce un modulo **opzionale**, §9.6).
- **La navigazione pubblica non segue l'organigramma**: un nuovo membro cerca "Piloti / ATC / Eventi / Training", non "Flight Operations Department". La sitemap (§8.2) resta per pubblico; il dipartimento è visibile solo come etichetta e filtro.

### 9.1 Nucleo (sempre presente, `IvaoHub.Core`)

| Servizio | Cosa fa | Dipendenze | Note |
|---|---|---|---|
| Utenti, permessi, audit | login OIDC BFF, `StaffRoleMap`, grant per VID, superadmin, audit | IVAO OAuth | §6 |
| Dati di riferimento IVAO | FIR/centri, aeroporti (snapshot giornalieri) | API IVAO `client_credentials` | §7 schema `ref_` |
| **Contenuti a sezioni** | pagine, news e documenti: un solo modello `cms_contents` a sezioni e blocchi, con template, versioni, per dipartimento | media | §9.3; include `/start` (onboarding), `/pilots`, `/about`, la home |
| **News** | articoli con categoria, copertina, pin, RSS; corpo a blocchi | media | ogni dipartimento pubblica le proprie |
| **Documenti** | documenti per dipartimento con categoria, versioni, visibilità per ruolo | media | §9.4; confine netto con vIPI |
| **Calendario unico** | tutte le voci: eventi, RFE, training, esami, tour, riunioni staff, meeting di divisione, scadenze | proiezioni `IProjectable` dai moduli (§16.4) | §9.5 |
| Media library | upload immagini/file con alt tradotto, per dipartimento | disco Plesk | limite upload esplicito |
| Contatti | form (solo autenticati) indirizzato a un dipartimento; coda nel back-office | mail | sostituisce il form del sito Blazor |
| **Award** | catalogo award (nome, immagine, dipartimento, criterio) + assegnazioni per VID con motivazione e audit; permesso `Awards.Assign` per dipartimento/grant (in IT: MD e HQ — varia per divisione, quindi è configurazione, non codice) | segnalazioni dai moduli, mail | **mai assegnazione automatica**: i moduli segnalano a chi assegna cosa c'è da assegnare (coda "da verificare"), l'assegnazione è sempre umana (§9.7) |
| Mail | SMTP, template tradotti, coda con retry (Quartz) | SMTP Plesk | infrastruttura, non un modulo |
| Live status | ATC/piloti online in area, FIR online, tile della home | Whazzup SDK | polling, niente SignalR in prima fase |
| Discord | link "collega Discord" + Discord ID in profilo; il bot resta separato | scope `discord` | API interna hub→bot in M6 |
| Staff directory, partner, link, FAQ | dai claim + ordinamento editoriale; contenuti trasversali | — | FAQ riusata dal blocco Accordion |

### 9.2 Moduli di dipartimento (obbligatori, tutti nel monorepo)

Decisione: i moduli 1–4 sono **obbligatori** per IT e per chi forka (non hanno flag in `division.json`; hanno solo `maintenance` a caldo). Tutto ciò che si aggiunge dopo è opzionale — il primo è `specialops` (riga 5).

| # | Modulo | Dipartimento | Contenuto | Complessità | Dipendenze | Note e decisioni |
|---|---|---|---|---|---|---|
| 1 | **`events`** | Events (ED) | eventi (divisionali, HQ, RFE, RFO; online/live), slot con prenotazione, partecipanti, posizioni ATC, pubblicazione nel calendario unico | Media-alta | `ref_` aeroporti, API IVAO bookings ATC, mail | Sostituisce `ivao-booking` PHP e il calendario del Blazor. **Nessuna migrazione dello storico**: il vecchio booking resta consultabile in sola lettura per un periodo, poi redirect (§12). |
| 2 | **`flightops`** | Flight Operations (FOD) | tour dell'anno (creabili in anticipo), leg, PIREP con validazione (manuale + **validatore automatico** già scritto in `AutomaticValidatorTour`), classifiche, **segnalazioni di completamento per gli award** (il registro award vive nel nucleo, §9.1/§9.7: FlightOps segnala, chi ha `Awards.Assign` assegna), **registro Virtual Airlines** (nome, logo, link, descrizione — mostrato in `/pilots`), voci nel calendario unico | Alta | API IVAO (voli/tracker), mail, `ref_` aeroporti | Assorbe il progetto `Ivao Italy Toursystem`: il suo design confluisce nel documento di design di questo modulo e il repo separato si chiude. Sistema **divisionale** (i tour HQ restano su tours.th.ivao.aero). |
| 3 | **`training`** | Training (TD) | richieste training/esame, matching trainer, disponibilità, sessioni, esiti e storico, mock exam PP/ADC, group training, voci nel calendario unico | Alta | API IVAO (scope `training` non disponibile → input manuale/CSV, `ITheoryExamSource`), mail | Sostituisce PATS (`training.ivao.it`). Migrazione dello storico ⚠️ da decidere quando si sa chi mantiene PATS e se il DB è accessibile (§15). Checklist funzionale: il TDCenter del template HQ (§2.3-ter). |
| 4 | **`atc`** | ATC Operations (AOD) | sezione `/atc` (carriera, rating, posizioni, sector file), card e deep link verso vIPI, statistiche ATC in dashboard via API vIPI; in M5 il **montaggio in-process** di vIPI sotto `/services/vsop` | Bassa ora, media al montaggio | vIPI (Blazor, net8 → net10) | Due tempi come da §15.2: oggi app separata con SSO, domani un solo processo. Il modulo esiste da subito così le rotte riservate a vIPI sono escluse dal fallback SPA fin da M0. |
| 5 | **`specialops`** ⚠️ | Special Operations (SOD) | **Segnaposto, da definire col dipartimento SO** (candidati: presentazione del gruppo, arruolamento, attività/missioni con iscrizioni e voci nel calendario). Il vSOP militare e la documentazione operativa SO restano in vIPI | Da stimare | da definire | **OPZIONALE** (`modules.specialops`, acceso per IT): non tutte le divisioni hanno un gruppo SO attivo. Design rimandato (§15.10); nel frattempo SO ha comunque il suo spazio di dipartimento nel back-office (news, documenti, pagine, calendario). |

Ogni modulo: proprio progetto `IvaoHub.Modules.<Nome>`, proprio schema (`evt_`, `fo_`, `trn_`), proprie rotte `/api/<modulo>`, proprie migrazioni, proprio `IModule`; comunica col nucleo tramite interfacce ed eventi di dominio (es. `EventPublished` → il nucleo crea la voce di calendario), mai con join cross-schema.

### 9.3 Contenuti a sezioni — il modello unico (deciso il 2 settembre 2026)

Deciso da Carmine: **tutti i contenuti creati dal sito sono documenti modulari**, composti da sezioni predisposte per obiettivi specifici, e chi crea qualcosa crea *un documento o un template di documento* sul quale se ne costruiscono altri. Il riferimento è il modello a cui vIPI è arrivata dopo il refactor 08 (`Document → DocumentVersion → DocumentSection → ContentBlock`, `SectionCatalog`, profili per tipo, pubblico congelato alla release, editor che mostra cosa non va). Nell'hub lo si prende **allargando** di un livello il modello a blocchi già deciso, non aggiungendo un secondo sistema. Chiude la decisione «markdown vs WYSIWYG» e sostituisce le tre entità separate pagine/news/documenti.

- **Un solo contenuto.** Pagine, news e documenti sono righe della stessa tabella `cms_contents` (§7) con `kind`; condividono CRUD, editor, renderer, versioni, proiezione nella ricerca. Nel back-office restano separati come filtro su `kind`, non come codice. Le poche colonne specifiche (categoria e file per i documenti, copertina e pin per le news) sono nullable sulla stessa riga.
- **Struttura**: `Content → Section[] → Block[]`, con **sotto-sezioni fino a profondità 3** (vIPI l'ha trovata sufficiente), serializzata in `body_json` con `schema_version`. Una *Section* ha `key` (stabile, es. `purpose`, `syllabus`, `hero`), `title_i18n`, `layout` (`stacked` oppure colonne `1/2+1/2`, `1/3+2/3`, `3×1/3`… — il livello *Row* del Page Builder HQ diventa una proprietà della sezione), sfondo/padding/larghezza, `collapsed`, e i blocchi. Un *Block* ha `type` + `props` validati da uno schema `zod` con versione; i campi testuali dei blocchi sono `Localized` come tutto il resto (una sola traduzione da gestire, non un albero per lingua).
- **Le sezioni «predisposte per un obiettivo» non sono un nuovo tipo di oggetto.** In vIPI la distinzione Derived/Editorial/Host è costata registry e ponti; nell'hub il registry dei blocchi basta. Tre forme coprono tutto: sezione **libera** (blocchi testo, tabella, callout, immagine, video, embed, galleria, accordion, CTA…); sezione **strutturata** (un solo blocco con schema — «scheda evento», «syllabus», «scheda Virtual Airline», «verbale» — il cui form è generato dallo stesso motore di §16.6, bloccato dal template); sezione **derivata** (un blocco *Data* vivo: `calendar`, `newsList`, `documentList`, `eventList`, `staffList`, `networkStats`, `stats`, `timeline`). I moduli **registrano blocchi**, come già deciso in §9.7, non tipi di sezione.
- **Il template è un contenuto anch'esso**: una riga di `cms_contents` con `is_template = true`, stesso dipartimento, stesso editor. In più, per ogni sezione, porta tre attributi che il contenuto normale ignora: `required`, `locked` (struttura fissa: si compila, non si ristruttura) e `allowedBlocks`. «Nuovo da template» è una copia profonda che conserva `template_id`. Niente tabella dei template, niente editor dei template. I template di sistema (Landing, Section page, About, Contact, Onboarding, Verbale, Guida, Policy) e quelli portati dai moduli sono **seed JSON**, non codice, e chi forka li modifica dall'interfaccia.
- **Cambio di template dopo la creazione dei figli** (deciso): nessuna propagazione automatica del contenuto. Nell'**editor** il contenuto figlio mostra la sezione nuova da compilare ed evidenzia quella che il template ha tolto, con un'azione «allinea»; il **pubblico** continua a vedere la versione pubblicata, congelata in `cms_content_versions`, finché qualcuno non ripubblica. È lo stesso patto di vIPI: documento pubblico congelato, editor che mostra cosa non va.
- **Chi crea o modifica template** (deciso): solo Director, Assistant Director, WM, AWM e, per il proprio dipartimento, coordinator e assistant coordinator — permesso `Content.ManageTemplates` nella grammatica di §16.3. Advisor e membri usano i template, non li cambiano.
- **Pubblicazione e versioni**: ogni «Pubblica» crea una riga in `cms_content_versions` (fotografia di titolo e `body_json`); il sito pubblico legge **solo** la versione pubblicata.
- **Blocchi Data: live o frozen, a scelta** (deciso da Carmine, come le sezioni derivate di vIPI). Ogni blocco *Data* porta `renderMode: live | frozen`. *Live*: la versione pubblicata interroga i dati al momento della lettura (un elenco «prossimi eventi» in una pagina di sezione). *Frozen*: alla pubblicazione il renderer cattura il risultato del blocco e lo salva nella versione (`frozen_json` accanto alle `props`), così un verbale o una policy fotografano i dati di quel giorno anche se la fonte cambia. Il template può fissare il `renderMode` di una sezione `locked`; alcuni blocchi sono **sempre live** per natura (`networkStats`: uno stato della rete congelato è un dato scaduto spacciato per attuale, la stessa regola del METAR in vIPI) e non espongono il toggle. Non si prende il ciclo AIRAC di vIPI: qui la «release» è la singola pubblicazione.
- **Rendering**: ogni `type` ha un componente React in `web/src/blocks/` costruito con Atmosphere; registry `type → componente`; blocchi sconosciuti rendono un avviso solo per lo staff. Lo schema dei blocchi vive **solo** in TypeScript/zod (§16.5): il backend tratta `body_json` come opaco (controlla `schema_version` e dimensione), estrae il testo per la ricerca con un walker generico delle stringhe e non replica lo schema in C#. Sanitizzazione di markdown ed `embed` (allowlist di host) in un solo componente. Per il SEO delle pagine pubbliche non si fa prerender (§16.11).
- **Editor** (`/staff/{dept}/contents`): albero di sezioni e blocchi «a lista» con aggiungi / sposta su-giù / duplica / elimina, form proprietà generato dallo schema zod, anteprima nella stessa pagina, bozza/pubblicato, lingue affiancate nello stesso form («copia dall'altra lingua»). Niente canvas, niente drag libero (al più `dnd-kit` sulla lista, in un secondo momento). Le sezioni `locked` mostrano solo i campi da compilare.
- **Le pagine «di sistema»** (home, `/start`, `/pilots`, `/about`) sono righe `kind = page` seedate al primo avvio dai template con contenuto Lorem tradotto: chi forka le riempie dall'editor, mai dal codice.
- **Confine con vIPI invariato**: SOP, LoA, vPIV e spazi aerei restano in vIPI (§9.4); questo modello serve a tutto il resto.

### 9.4 Documenti per dipartimento

- Ogni documento è una riga di `cms_contents` con `kind = document` (§9.3) e ha `owner_department` obbligatorio, `category` (vocabolario per dipartimento: es. Training → syllabus, guide, materiale esami; Flight Ops → guide piloti, briefing; Membership → regolamenti, policy; HQ → verbali, policy divisionali), `visibility` (public / members / staff / department), **versioni** con changelog e data (`cms_content_versions`), corpo come file (PDF, `file_media_id`) **o** come contenuto a sezioni (§9.3), tipicamente da un template del dipartimento, per i documenti che conviene leggere nel browser.
- **Confine netto con vIPI** (deciso): SOP, LoA, vPIV, spazi aerei, tutto ciò che è documentazione operativa per FIR/aeroporto vive **solo** in vIPI — anche quando il lettore è un pilota. Per gli **aeroporti e avvicinamenti militari** curati dal SOD vale il modello **fonte unica, viste per pubblico**: le vSOP contengono sia le info ATC sia la documentazione piloti e già oggi ogni sezione è marcata *per ATC / per piloti / per tutti*; il SOD continua a scrivere in un solo posto. vIPI esporrà un endpoint API con le sole sezioni piloti (lavoro nel suo backlog, sensato insieme al passaggio net10) e l'hub le **renderizza dentro `/pilots`** e nella pagina SO, come già consuma l'API delle statistiche ATC; finché l'API non c'è, deep link puntuali ai documenti. L'hub non ne tiene copia né indice: la sezione `/atc` e la pagina della FIR mostrano card e deep link verso vIPI. Regola pratica per lo staff: "se ha una FIR o un aeroporto come soggetto ed è operativo, è vIPI; altrimenti è un documento dell'hub".
- Sul sito pubblico: `/docs` (tutti i pubblici, filtrabili), `/docs/{dept}` e il blocco `documentList` nelle pagine di sezione. Ricerca full-text sul titolo/sommario (MariaDB FULLTEXT), non sul PDF in prima fase.

### 9.5 Calendario unico

Deciso da Carmine: **un calendario per tutto** — eventi, RFE/RFO, training ed esami, tour, ma anche attività interne di divisione (riunioni staff, meeting di divisione, scadenze).

- Tabella `cms_calendar_entries` nel nucleo (§7). I moduli **non** scrivono direttamente: le loro entità (evento, sessione di training, leg di tour) implementano `IProjectable` e l'interceptor EF del nucleo crea/aggiorna/rimuove la voce con `source_module` + `source_id` **nella stessa transazione** (§16.4). Le voci interne (`meeting`, `deadline`, `other`) le crea lo staff a mano nel proprio spazio di dipartimento.
- `visibility` per voce: `public` (sito), `members` (dietro login), `staff` (tutto lo staff), `department` (solo il dipartimento proprietario). Una riunione dello staff Events è `department`; il meeting di divisione è `staff`; un RFE è `public`.
- Viste: `/calendar` pubblico (mese/settimana/agenda, filtri per `kind`), blocco `calendar` nelle pagine, dashboard `/me` (le mie voci: eventi a cui sono iscritto, sessioni, scadenze del mio dipartimento), feed **iCal** per utente con token (così finisce nel calendario personale) e per dipartimento.
- Orari sempre salvati in UTC, mostrati in UTC + fuso della divisione (standard IVAO).

### 9.6 Cosa NON entra (e cosa è opzionale)

| Cosa | Decisione |
|---|---|
| Onboarding come modulo | No: è la pagina `/start` a blocchi (Timeline + Card + CTA). Se un giorno servirà lo stato per utente (checklist "fatto/da fare" in `/me`), diventerà un piccolo modulo opzionale. |
| Test system (esami anti-AI) | **Sospeso.** Non è nel catalogo e non ha una data: rientra solo se Carmine lo ripropone, e in quel caso come app separata, estraendo allora l'auth dell'hub in una libreria condivisa (non prima: §16.9). |
| QuickOverview / Operations | Non esiste: confluito in vIPI (redirect 301 quando si spegne). |
| SES – Slot Events come modulo separato | Non replicato: gli eventi "a slot" (RFE/RFO) sono il `booking_mode` del modulo Events, non un modulo a parte. |
| ~~Virtual Airlines~~ | Ripescato (1° set, sera): registro VA dentro `flightops`, mostrato in `/pilots`. |
| ~~Testimonial~~ | Ripescato (1° set, sera): blocco `testimonial` nel set §9.3, contenuti di proprietà PR. |
| ~~Special Operations~~ | Ripescato (1° set, sera): modulo `specialops` opzionale, segnaposto (§9.2 riga 5). |
| ATC Scheduling, Forum, WebEye, Wiki, FPL | Solo link/embed: sono HQ. |
| Membership, PR, Web come moduli di codice | No: hanno il loro spazio nel back-office con i servizi comuni; un modulo opzionale nascerà solo per logica specifica (es. gestione soci, registro VA). |

Regola per il futuro: **tutto ciò che si aggiunge dopo questo catalogo è opzionale** (`modules.<nome>` in `division.json`, §4.2) — i quattro moduli di dipartimento obbligatori e il nucleo no. `specialops` è il primo modulo opzionale e fa da modello per i prossimi.

### 9.7 Contratti trasversali nucleo↔moduli (decisi il 1° set 2026, sera)

Regole che valgono per **ogni** modulo, presente e futuro — si scrivono una volta nel nucleo e si dettagliano nel documento di design di M0:

- **Maintenance**: con il modulo in manutenzione, i contenuti già pubblicati restano **visibili in sola lettura** (voci di calendario incluse); le *azioni* (prenotare, iscriversi, inviare un PIREP) rispondono 503 con pagina cortese e tradotta; i job del modulo vanno in pausa. Implementato nel nucleo, uguale per tutti.
- **Widget di dashboard**: ogni modulo **registra** i propri widget ("le mie prenotazioni", "le mie richieste training", "i miei tour in corso"); `/me` — e in prospettiva le pagine — li compongono liberamente. Stesso principio del registry dei blocchi: più il sito è flessibile, più è general purpose. I blocchi *Data* che dipendono da un modulo (`eventList`…) sono anch'essi registrati dal modulo, non cablati nel nucleo.
- **Notifiche**: servizio unico nel **nucleo** (mail ora, Discord in M6): i moduli pubblicano *intenti* di notifica, mai SMTP diretto — un cambiamento al servizio si fa in un punto solo. Preferenze per tipo di notifica in `/me/profile`.
- **Privacy dei membri**: l'hub **non ha un profilo utente pubblico**. L'unico profilo pubblico è quello ufficiale IVAO (`https://www.ivao.aero/Member.aspx?Id={VID}`): ovunque compaia un membro (classifiche tour, staff directory, partecipanti) si mostra il minimo necessario e si linka lì. Nessuna funzione di export dei dati utente (IVAO non la prevede); per il GDPR ci si allinea alle norme e alla privacy policy IVAO, e ogni modulo documenta nel proprio design cosa conserva di personale e per quanto (così una richiesta di cancellazione ha un percorso noto).
- **Ricerca globale**: indice centrale `search_index` nel **nucleo** (titolo, testo, tipo, url, dipartimento, visibilità), alimentato dai moduli via `IProjectable` con `source_module`+`source_id` — lo stesso pattern del calendario (§16.4). Matching, ranking e UI (⌘K e ricerca pubblica) vivono solo nel nucleo: un fix alla ricerca **non tocca i moduli**; un modulo si limita a dire "indicizza questo".
- **Proiezioni transazionali**: tutto ciò che i moduli proiettano nel nucleo (calendario, indice di ricerca, segnalazioni award) passa da un'unica interfaccia `IProjectable` — l'entità restituisce uno snapshot (titolo per lingua, url, dipartimento, visibilità, intervallo di tempo opzionale, testo per la ricerca, eventuale segnalazione award) e l'interceptor EF del nucleo fa l'upsert con chiave `source_module`+`source_id` **nella stessa transazione** del salvataggio. Niente bus di eventi, niente job di riconciliazione: non c'è nulla che possa restare a metà (§16.4). Gli eventi asincroni restano solo per le notifiche.

**Contratto `IModule`** (confermato il 1° set 2026 dopo la verifica sui casi reali qui sotto; firma esatta nel design di M0): un modulo dichiara identità e dipartimento; rotte `/api/<modulo>` e voci di navigazione (pubblica e staff); le proprie migrazioni; il proprio catalogo permessi (`Events.Manage`…); widget di dashboard e blocchi pagina che fornisce; i propri job Quartz; le entità `IProjectable` (calendario, indice di ricerca, segnalazioni award) e gli intenti di notifica che pubblica. Regole dure: un modulo non referenzia **mai** un altro modulo (solo `Core`); il nucleo non referenzia i moduli (riceve solo contributi registrati); la comunicazione tra moduli passa dal nucleo.

**Collaborazioni tra moduli — i casi reali, e come rientrano nel contratto**:

| Caso | Soluzione |
|---|---|
| Events↔Training: training ed esami a calendario | Calendario unico: le sessioni di Training sono `IProjectable`, il nucleo crea le voci. Nessun contatto diretto. |
| ATC↔Events: quali settori/posizioni servono all'evento | Dato di riferimento: `ref_` (aeroporti, FIR) nel nucleo + **API pubblica di vIPI** per le posizioni note dalle SOP. vIPI resta dietro la sua API anche dopo il montaggio in-process. |
| FlightOps↔Events: award "ATC/pilot event support", award legati a eventi | **Award nel nucleo** (§9.1). Non esistono meccanismi automatici di assegnazione: i moduli *segnalano* (FlightOps: "VID X ha completato il tour Y"; Events: eventi e partecipanti nel periodo, via calendario unico) e chi ha `Awards.Assign` (in IT: MD e HQ; configurabile per divisione) verifica e assegna a mano, con audit. |
| SpecialOps↔ATC: documenti degli aeroporti/avvicinamenti militari | **Fonte unica in vIPI, viste per pubblico** (§9.4): il SOD scrive solo nelle vSOP, che già marcano ogni sezione per ATC/piloti/tutti; vIPI espone le sezioni piloti via API e l'hub le renderizza in `/pilots` e nella pagina SO (deep link finché l'API non esiste). Nessun meccanismo di co-proprietà documenti nell'hub. |
| Discord bot (M6) | Parla solo col servizio notifiche/API del nucleo, mai coi moduli. |
| Membership: vista d'insieme del membro nel back-office | Composizione dei widget registrati dai moduli, come `/me`. |
| Training↔ATC: posizioni per gli esami | `ref_` nel nucleo, come per Events. |

---

## 10. Integrazioni con le API IVAO

| Uso | Endpoint (v2) | Auth | Frequenza/cache |
|---|---|---|---|
| Profilo al login | `/users/me` | token utente | ad ogni login |
| Chi è online (ATC/piloti in area) | `/tracker/now/atc/summary`, `/tracker/now/pilots/summary` (o Whazzup v2 JSON) | client_credentials `tracker` | job ogni 30–60 s, cache |
| Prenotazioni ATC dell'evento | `/atc/bookings` (verificare path) | client_credentials | job ogni 5 min |
| Aeroporti della divisione (con piste) | `/airports/all?countryId={countryId}&includeRunways=true` | client_credentials | giornaliero, snapshot in `ivao_airports` |
| Posizioni ATC | `/atc/positions` | client_credentials | giornaliero |
| FIR/centri della divisione | `/centers?countryId={countryId}` (es. `?page=1&region=Europe&countryId=IT`) | client_credentials | giornaliero, snapshot in `ivao_centers` |
| Sessione live utente | `/users/me/sessions/now` | token utente, scope `tracker` | on demand |

Tutto passa da `IvaoApiClient` (riuso/aggiornamento di `Ivao.It.IvaoApiSdk`), con log delle chiamate e circuit breaker: se `api.ivao.aero` è giù, l'hub degrada (widget "dati non disponibili"), non cade.

---

## 11. Ambiente di sviluppo, CI e deploy

### 11.1 Locale

- `docker-compose up`: MariaDB 11.4 (stessa minor della produzione) + Mailpit (SMTP finto con UI).
- `dotnet run` su `IvaoHub.Web` (`https://localhost:5001`) + `pnpm dev` su `web/` con proxy.
- OAuth in locale con le **credenziali di test di Carmine** in `config/ivao-oauth.json` (gitignored), con login/redirect URL su `localhost` come autorizzati per quelle credenziali. Se qualche endpoint API non è raggiungibile con le credenziali di test, mock dell'`IvaoApiClient` con fixture JSON.
- `dotnet ef migrations add` per modulo; seed di sviluppo con divisione IT + utenti finti con vari ruoli.

### 11.2 CI (GitHub Actions)

`build-test.yml`: restore → build .NET → test unit → test integrazione con Testcontainers MariaDB → `pnpm install/lint/typecheck/build` → genera client OpenAPI e verifica che sia allineato → artefatto `publish/` (`dotnet publish -c Release` con `wwwroot` popolato).
`release.yml` (su tag): crea la release GitHub con lo zip pronto per Plesk + note. Le divisioni che forkano ereditano la pipeline.

### 11.3 Deploy su Plesk — il modello di vIPI, riusato

La procedura ricalca quella già rodata per `atc.it.ivao.aero` (`deploy/atc-ivao/LEGGIMI-*.md`), perché il server e le persone sono gli stessi.

1. **Pacchetto**: `dotnet publish -c Release -r linux-x64 --self-contained` con `wwwroot` già popolato dalla SPA; asset minificati e precompressi `.br/.gz`; timbro di versione (`AssemblyMetadata` + commit) esposto su `/api/version`. Zip + foglio `LEGGIMI-PACCHETTO-x.y.z.md` con l'elenco dei file e i controlli post-deploy.
2. **Cartella dell'app** nella sottoscrizione `it.ivao.aero`, avviata da **Passenger** (`dotnet IvaoHub.Web.dll`), `ASPNETCORE_ENVIRONMENT=Production`. Struttura: `wwwroot/`, `config/division.json`, `config/ivao-oauth.json` (compilato dalla divisione), `secrets/<nome-non-indovinabile>.json` (connection string, SMTP, secret — l'app carica ogni `*.json` di `secrets/`), `hub-keys/` (Data Protection, **persistente, mai cancellare**), `uploads/` (documenti), `logs/`, `diagnostics/`.
3. **Direttive nginx aggiuntive** in Plesk: `deny all` su `secrets/`, `hub-keys/`, `diagnostics/`, `logs/`, `appsettings*.json`, `*.dll`, `*.pdb`, `*.json` alla radice; `Cache-Control: no-store` su `/api/*` (Cloudflare davanti). Verifica dall'esterno con `curl -I` dopo ogni cambio di hosting.
4. **Database**: DB + utente dedicati dal pannello (`GRANT ALL` sul solo schema, verificare che la prima migrazione con `ALTER DATABASE CHARACTER SET utf8mb4` passi); pool `MaximumPoolSize≤15` perché il tetto per utente è condiviso; `max_allowed_packet` confermato ≥ 4 MB o upload solo su disco.
5. **Migrazioni**: `Database.Migrate()` all'avvio (senza shell non c'è alternativa), con tre regole ferree: solo migrazioni **additive** (mai `DROP`/rename distruttivi nello stesso pacchetto che smette di usare la colonna → pattern *expand/contract* in due release), test CI che applica l'intera catena su una **MariaDB 11.4.10 vera**, e un `diagnostics/startup.txt` che dice quale migrazione ha applicato. Niente consegne con migrazioni nelle finestre in cui nessuno può ripristinare.
6. **Aggiornamento**: upload via FTP in **binario**, rimettere il bit di esecuzione all'eseguibile, non toccare `hub-keys/`, `secrets/`, `uploads/`; poi `tmp/restart.txt`. Sonda post-deploy (`/api/version`, `/health`, login, una pagina per modulo) eseguita **non** nel minuto del riavvio.
7. **Backup**: conferma scritta da Ivao.It su frequenza, retention, inclusione di `hub-keys/` e `uploads/` (non stanno nel DB) e un ripristino provato. Finché non c'è, si pianifica come se non ci fosse.
8. **Staging**: sottodominio dedicato nella stessa sottoscrizione, stesso pacchetto, credenziali OAuth di test con i propri login/redirect URL.

---

## 12. Migrazione e convivenza

Strategia **strangler**: l'hub nasce accanto ai siti esistenti, li sostituisce un modulo alla volta.

1. Hub online su un dominio temporaneo (es. `beta.it.ivao.aero`) con Content + auth + live status. Contenuti migrati dal Blazor (export manuale/script delle pagine).
2. Switch del dominio principale quando Content è completo; il vecchio sito resta raggiungibile in sola lettura per un periodo.
3. Events/Booking: **nessun import dello storico** (deciso): il modulo parte vuoto; `ivao-booking` resta acceso in sola lettura per un periodo concordato con lo staff Events, poi redirect 301 verso `/events`. Se in seguito servisse lo storico, lo script di import è un'aggiunta, non un prerequisito.
4. Flight Ops/Tour: i tour nascono nel nuovo modulo (i tour dell'anno successivo si creano direttamente nel tool); lo storico di `tours.th.ivao.aero` ⚠️ da decidere (import dei leg validati per le classifiche, o solo link al vecchio sistema).
5. Training: import di trainer, richieste e storico esiti da PATS **se** il DB è accessibile (⚠️ §15); periodo di doppia lettura, poi spegnimento.
6. Onboarding: i contenuti del wizard vengono riscritti come pagina `/start` a blocchi; redirect del sottodominio `welcome.it.ivao.aero`. QuickOverview: nessuna migrazione nell'hub (confluisce in vIPI), solo redirect 301 verso le pagine vIPI corrispondenti quando si spegne.
Ogni migrazione ha: script idempotente in `tools/migrate-<sorgente>/`, report di riconciliazione (conteggi prima/dopo), piano di rollback (il vecchio sistema resta acceso fino al go).

---

## 13. Roadmap proposta

| Fase | Contenuto | Uscita |
|---|---|---|
| **M0 — Fondamenta** | Repo, soluzione .NET, SPA Vite+Atmosphere, docker-compose, CI, `division.json`, i18n IT/EN, login OIDC BFF con credenziali di test, `users` + ruoli, layout pubblico/riservato, dashboard vuota; **la spina dorsale generica di §16** (`Localized<T>`, interfacce trasversali + interceptor + authorization handler, grammatica permessi, `IProjectable`, motore lista+form, endpoint di bootstrap) **dimostrata end-to-end** su `links` e su un primo `cms_contents` creato da template (§16.15) | Skeleton navigabile, login funzionante, meccanismi generici provati. Design: `01-design-m0.md`; fasi: `02-piano-implementazione-m0.md`. Il **deploy su staging Plesk** è spostato a M1 (deciso 2 set 2026: attende le risposte A9) |
| **M1 — Sito pubblico** | Primo pacchetto self-contained e deploy su staging Plesk (foglio `LEGGIMI`); nucleo editoriale: pagine a blocchi (editor a lista, set iniziale di blocchi), news, documenti per dipartimento, calendario unico (con sole voci interne per ora), media, contatti, staff directory, live status; pagina `/start`; back-office per dipartimento; modulo `atc` come sezione `/atc` con deep link a vIPI; SEO/i18n URL; migrazione contenuti dal Blazor | Sostituisce `it.ivao.aero` |
| **M2 — Eventi** | Modulo Events: eventi, slot RFE/RFO, booking, partecipanti, notifiche mail, voci nel calendario unico, back-office Events. Nessun import | Spegne `ivao-booking` |
| **M3 — Tour** | Modulo Flight Ops: tour, leg, PIREP, validatore automatico, classifiche, award con mail, voci nel calendario; design ereditato da `Ivao Italy Toursystem` | I tour IT lasciano `tours.th.ivao.aero` |
| **M4 — Training** | Modulo Training: richieste, trainer, disponibilità, sessioni, esiti, mock exam, group training, import storico se possibile | Spegne `training.ivao.it` |
| **M5 — vIPI dentro l'hub** | Allineamento TFM (vIPI su net10 + provider MariaDB), montaggio in-process sotto `/services/vsop`, `atc.it.ivao.aero` → redirect, spegnimento di `quickoverview.ivao.it` (già confluito in vIPI) | Un solo sito ATC+hub |
| **M6 — Ecosistema** | API interne per il bot Discord, iCal, prerender SEO, primi moduli opzionali se richiesti, `FORKING.md` rifinito, prima divisione pilota che forka | Prodotto divisionale |

M5 può scorrere prima o dopo M3/M4 a seconda di quando si scioglie il nodo Pomelo/net10 (§15 punto 2); nulla in M1–M4 dipende da esso. L'ordine Events → Tour → Training è deciso (1° set 2026): il tour ha già design e validatore, Training è il modulo più complesso.

Ogni modulo dopo M0 riceve il proprio breve documento di design (modello dati, schermate, permessi, migrazione) prima del codice, come per M0 stesso.

---

## 14. Rischi e mitigazioni

| Rischio | Mitigazione |
|---|---|
| Login URL / redirect URL registrati su IVAO diversi da quelli configurati | Validazione all'avvio del JSON; checklist di go-live che confronta i due URL con quelli registrati; staging con URL propri registrati a parte. |
| Scope `training` non implementato lato IVAO → esiti teorici non leggibili via API | Inserimento manuale/CSV dallo staff training; astrazione `ITheoryExamSource` per sostituirla quando l'API arriva. |
| Pomelo senza release per EF Core 10; vIPI in produzione su net8 che esce dal supporto il 10 nov 2026 | Hub: EF Core 9 + Pomelo 9 su runtime .NET 10. vIPI: verificare se il ramo net10 può usare EF Core 9 + Pomelo 9 (sblocca sia l'EOL sia il montaggio in-process); altrimenti attendere Pomelo 10. Decisione unica per i due progetti. |
| Migrazioni che girano da sole all'avvio senza possibilità di ripristino | Regola *additive-only / expand-contract*, test su MariaDB 11.4.10 vera in CI, finestre di consegna concordate, backup confermato prima del primo dato reale. |
| Segreti/chiavi esposti o persi via FTP (è già successo a vIPI il 24–25 ago) | Cartella `secrets/` con file dal nome non indovinabile, deny nginx verificato con `curl -I`, `hub-keys/` nel foglio "non cancellare", rotazione credenziali a ogni sospetto. |
| Tetto connessioni MariaDB condiviso con vIPI | Pool ≤ 15, `ConnectionIdleTimeout` basso, query brevi, cache in memoria per le letture calde. |
| Plesk: limiti del proxy (timeout, dimensione upload) | Niente SignalR nella prima fase (polling per live status); upload documenti con limite esplicito; test su staging Plesk fin da M0. |
| Cambi delle API/OAuth IVAO (il README avvisa di messaggi d'errore in cambiamento) | Client isolato, contratti in un solo posto, test con fixture; iscrizione ai canali dev IVAO. |
| Un solo manutentore | Documentazione nel repo, ADR per ogni decisione, CI che blocca regressioni, dipendenze minime, niente magia. |
| Forkabilità che si erode | Test "divisione XX" in CI; review checklist nel template PR. |
| GDPR (dati personali di membri, email, discord id) | Minimizzazione scope, retention, export/cancellazione, privacy policy divisionale. |

---

## 15. Decisioni aperte ⚠️

1. ~~Quali moduli inglobare e in che ordine~~ **Deciso il 1° set 2026** (§9, §13): nucleo editoriale + `events`, `flightops`, `training`, `atc`; ordine Events → Tour → Training; vIPI montato appena il TFM lo consente; test system sospeso.
2. **vIPI nell'hub — quando e come**: il montaggio in-process è la destinazione (§9 riga 7b), il nodo è il TFM. Da verificare in vIPI: può il ramo `net10.0` di `Vipi.Infrastructure` usare EF Core 9 + Pomelo 9 invece di EF Core 10 (le 65+ migrazioni sono generate con EF 10 ma applicate anche da EF 8 — con EF 9 dovrebbero passare)? Se sì, si sblocca insieme l'EOL di net8 e il montaggio. Decidere anche il dominio finale della parte ATC (`it.ivao.aero/services/vsop` con redirect da `atc.it.ivao.aero`, o viceversa proxy).
2b. ~~Tour system e test system~~ **Deciso**: il tour system è il modulo `flightops` nel monorepo dell'hub (repo separato chiuso, design confluisce). Il test system è sospeso; se tornerà, sarà app separata (auth estratta in libreria solo allora).
2d. **Storico tour**: importare i leg validati da `tours.th.ivao.aero` per le classifiche, o partire da zero come per gli eventi?
2c. **Hosting dell'hub**: chiedere a Ivao.It (stesse domande A9 di vIPI, già scritte): dove sta la cartella dell'hub nella sottoscrizione, se il document root può essere diverso dalla cartella dell'app, privilegi dell'utente DB, `max_allowed_packet`, `sql_mode`, backup con retention e ripristino provato, se esiste un sottodominio di staging.
3. **Dominio di staging** e nomi finali (`beta.it.ivao.aero`?), perché login URL e redirect URL vanno registrati su IVAO per ogni ambiente.
4. ~~Editor contenuti~~ **Deciso**: pagine a blocchi con editor a lista (§9.3); il blocco `text` usa markdown con anteprima. Prerender SEO: **no per ora** (§16.11).
5. ~~Licenza del repository pubblico~~ **Decisa il 3 set 2026**: **Apache-2.0**, copyright «2026 Carmine Granato». Nota in `docs/internal/decisions/2026-09-03-licenza.md`.
6. ~~Prefisso lingua negli URL~~ **Deciso: no per ora** (§16.11); lingua da profilo → cookie → `Accept-Language`.
7. **Accesso ai DB esistenti** (PATS, sito Blazor) per stimare le migrazioni — `ivao-booking` non serve più (nessun import). Chi mantiene PATS oggi?
7b. ~~Roster completo dello staff~~ **Deciso il 2 set 2026**: non esiste un endpoint IVAO per il roster; il roster dell'hub è **chi ha fatto login almeno una volta** (§16.13).
8. **Vocabolario delle categorie documenti per dipartimento** (§9.4): da definire con ogni coordinatore prima di M1.
9. **Feed iCal e notifiche del calendario unico**: per utente con token, per dipartimento, o entrambi; e se le voci `department` devono generare mail/Discord.
10. **Contenuto del modulo `specialops`** (§9.2 riga 5): da definire con il dipartimento SO (presentazione? arruolamento con workflow? missioni/attività?). Finché non si decide, il modulo resta un segnaposto senza tabelle.
11. ~~Contratto `IModule`~~ **Confermato** (1° set 2026, §9.7); firma esatta, snapshot di `IProjectable` e registry (widget/blocchi) **scritti** in `01-design-m0.md` §3.6 e §6.

---

## 16. Meccanismi generici — decisi il 2 settembre 2026

Criterio di Carmine: **quanto meno codice possibile; un pezzo usato in due punti si scrive una volta, mai due**. Il catalogo di §9 dice *cosa* costruire; questa sezione dice *con quali pezzi generici*, perché se non nascono in M0 verranno riscritti in M1 per pagine, news, documenti, calendario, link, partner, FAQ e poi in ogni modulo. Ogni punto è deciso; le firme sono in `01-design-m0.md`.

**A. La spina dorsale che M0 deve contenere**

1. **Traduzioni**: nessuna tabella `*_translations`. Ogni campo tradotto è una colonna JSON `{ "it": …, "en": … }` mappata su un value object `Localized<T>`; un converter EF, un componente React `LocaleFields`, un validatore «tutte le lingue di `division.locales` prima di pubblicare». Si perde il FULLTEXT sul titolo (la ricerca passa da `search_index`, che ha le sue colonne per lingua).
2. **Colonne trasversali come interfacce**: `IOwnedByDepartment`, `IVisible`, `IPublishable`, `IAuditable`. Un `SaveChangesInterceptor` compila audit e timestamp; un global query filter applica `visibility` all'utente corrente; **un solo** authorization handler confronta posizioni ∪ grant dell'utente con l'`owner_department` della risorsa. Nessun modulo riscrive «può modificare questa riga?».
3. **Grammatica dei permessi**: `<Area>.<Azione>` (`Content.Edit`, `Content.ManageTemplates`, `Events.Manage`, `Training.Assign`…) con lo scope di dipartimento **implicito** dalla risorsa. I moduli aggiungono nomi al catalogo, non handler. Fanno eccezione i permessi senza risorsa dipartimentale (`Permissions.Manage`, `Awards.Assign` configurato per divisione).
4. **Proiezioni via `IProjectable`**: calendario, indice di ricerca e segnalazioni award sono proiezioni dello stesso interceptor, upsert con `source_module`+`source_id` **nella stessa transazione** del salvataggio. Niente MediatR (licenza commerciale dal 2025), niente bus, niente job di riconciliazione. Eventi asincroni solo per le notifiche.
5. **Un solo documento a sezioni** (§9.3): editor, renderer e registry dei blocchi unici per pagine, news, documenti e per i corpi testuali dei moduli. Schema **solo** in TypeScript/zod; il backend tratta il JSON come opaco (`schema_version` + dimensione), estrae il testo per la ricerca con un walker generico delle stringhe; sanitizzazione markdown/`embed` (allowlist host) in un solo componente.
6. **Un solo motore di back-office**: lista generica su `DataTable` Atmosphere guidata da una configurazione di colonne + form generato dallo schema zod (lo stesso dei blocchi) anche per le entità; lato server un helper `MapCrud<TEntity, TDto>` che porta già la policy di dipartimento. Regola: **valida il server, il client mostra** i `ProblemDetails` campo per campo.
7. **Un solo endpoint di bootstrap** (`/api/me`): menu pubblico e staff, moduli abilitati / in maintenance, permessi effettivi, widget e blocchi registrati. La SPA non ha nulla di cablato.
8. **Un solo set di file di lingua** `locales/{lang}/*.json`, letto sia dalla SPA sia dal backend (mail, errori). Niente `.resx`.

**B. Cose tagliate o accorpate**

9. **Un solo progetto per il nucleo**: `IvaoHub.Core` (dominio + EF + client IVAO + `Content` come cartella) + `IvaoHub.Web` + un progetto per modulo. Niente `IvaoHub.Infrastructure` (interfacce e implementazioni in coppia sono codice doppio per costruzione: Clean Architecture ha senso alla scala di vIPI, non qui), niente `IvaoHub.Auth` finché il test system resta sospeso. Il confine compile-time che conta è tra i moduli e il nucleo.
10. **Niente `/api/v1`**: frontend e backend viaggiano nello stesso pacchetto.
11. **Niente prefisso lingua negli URL e niente prerender SEO**, per ora: le pagine pubbliche sono poche e Google renderizza le SPA (§15.4, §15.6 chiuse).
12. **DbContext per modulo** con tabella `__EFMigrationsHistory_<modulo>` separata (su MariaDB gli «schemi» sono solo prefissi) e **nessuna FK tra contesti**: solo colonne `vid`/`airport_icao` non vincolate.

**C. Convenzioni UI — da trattare nel design di M0, prima della prima schermata** (concordato il 2 set 2026)

Il problema noto (un pezzo nuovo che arriva con un design diverso dal resto della pagina) si risolve prima di tutto **per costruzione**: ogni schermata di back-office passa dal motore lista+form (punto 6) e ogni contenuto dal renderer dei blocchi (punto 5), quindi un design divergente non ha dove entrare. Le convenzioni coprono il residuo. Nel design di M0 si fissano: (a) il **set di icone** unico — **`lucide-react`, confermato** il 2 set 2026: è già una dipendenza di `@ivao/atmosphere-react` 3.1.0 — con la regola «se manca un'icona si cerca prima nel set; se proprio non c'è si aggiunge in `web/src/shared/icons/` nello stesso stile, mai inline nella schermata»; (b) l'**elenco chiuso dei componenti custom** oltre Atmosphere (§8.3): un pezzo nuovo si compone da quelli, non si scrive da zero, e aggiungerne uno è una decisione esplicita; (c) una pagina **`/staff/admin/ui-kit`** che mostra tutti i componenti e i blocchi in uso: riferimento vivo e test visivo quando si aggiunge qualcosa. Le regole finiscono in `docs/UI-GUIDELINES.md` (inglese, valgono anche per chi forka). Le convenzioni **dei blocchi** (spaziature tra sezioni, varianti di sfondo, resa di una sezione `locked` nell'editor) si discutono in **M1**, con il set di blocchi davanti.

**D. Buchi chiusi**

13. **Roster dello staff** (deciso da Carmine): non esiste un endpoint IVAO per l'elenco delle posizioni di una divisione; il roster dell'hub è **chi ha fatto login almeno una volta**. Staff directory, sospensione dei grant e scelta dei VID a cui proporre un grant si basano su quello.
14. **Perdita di `hub-keys/`**: oltre al logout di tutti, i token in `user_tokens` diventano illeggibili; il codice li tratta come assenti e forza il re-login, mai un'eccezione.
15. **Definizione di «fatto» per M0**: la spina dorsale esiste ed è dimostrata end-to-end su un'entità banale — `links`: localizzata, con dipartimento, visibilità, audit, CRUD via motore lista+form, proiettata nella ricerca — e su un primo contenuto di `cms_contents` creato da un template. Se passa, news e documenti in M1 sono configurazione più che codice.

**E. Come si cambia il sistema mentre si scrive codice** (concordato il 2 set 2026)

Durante il codice emergerà spesso che «serve altro». Il modello regge i cambi in corsa solo se, **prima di scrivere una riga**, la richiesta viene classificata:

- **(a) È un dato o una configurazione** — una sezione o un blocco in un template, un seed, una colonna di lista, una chiave i18n: si fa dentro il task, senza cerimonie.
- **(b) Rientra in un meccanismo generico esistente** — `IProjectable`, `MapCrud`, registry di blocchi/widget, `Localized<T>`, authorization handler: si usa quello. Se il meccanismo non copre il caso al 100 %, **si estende il meccanismo**, mai lo si aggira con un caso speciale.
- **(c) Serve un meccanismo nuovo o una funzione nuova di modulo**: ci si ferma. Nota di design breve (mezza pagina in `docs/internal/decisions/` o un paragrafo nel design del modulo: cosa serve, perché nessun meccanismo esistente basta, cosa si tocca), decisione insieme, poi aggiornamento del piano. Il task originale si chiude senza quella parte o resta aperto: **non si chiude «a qualunque costo»**.

Reti di sicurezza: la **checklist del template PR** («ho aggiunto una tabella `*_translations`, un handler di autorizzazione, un fetch a mano, una lista o un form non generati, un componente UI fuori dall'elenco? Se sì, perché?») e i **test della spina dorsale** di M0, che rompono la build se si bypassa l'interceptor o l'authorization handler. Le regole operative complete, nella forma che Claude Code legge a ogni sessione, stanno in **`CLAUDE.md`** alla radice della cartella di lavoro: è un **file privato di Carmine**, in italiano, escluso dal repository via `.gitignore` (insieme a `CLAUDE.local.md` e `.claude/`) — chi forka non lo riceve; le regole che devono valere anche per i fork vivono nella documentazione pubblica in inglese (`FORKING.md`, `UI-GUIDELINES.md`, template PR).

---

## 17. Fonti consultate

- Sito attuale: https://it.ivao.aero/ (home, /about, /pilots, /atc, /events, /special-ops, /events/calendar)
- Atmosphere: https://github.com/ivaoaero/atmosphere (README, `brand/README.md`, `brand/src/tokens.json`, `components/react/package.json`, `components/react/UPGRADE.md`, `src/styles/theme.css`, componenti) — docs https://ivaoaero.github.io/atmosphere/main
- OAuth-samples: https://github.com/ivaoaero/OAuth-samples (README, `php-pure`, `nodejs-pure`, `aspnetcore7/Ivao.OpenIdConnect`, `reactjs-with-lib`, `laravel-pure`)
- Scope OAuth IVAO: https://wiki.ivao.aero/en/home/devops/api/oauth-scopes
- Org GitHub IVAO Italy: https://github.com/ivao-italy (repos `ivao-booking`, `onboarding`, `Ivao.It.IvaoApiSdk`, `Ivao.It.WhazzupData.SDK`, `discord`)
- Servizi satellite: https://training.ivao.it/ , https://quickoverview.ivao.it/ , https://atc.it.ivao.aero/ (vIPI ATC Services)
- Template HQ per i siti divisionali: https://va.ivao.aero/ (IVAO Vatican; `assets/css/themes/ivao-classic.css`, `frontend.css`) e il suo backend https://va.ivao.aero/backend/ v3.7.6 (dashboard, Page Builder ed editor, LoA/SOP, TDCenter, Tourcenter, Events — visto con login staff il 1° set 2026)
- Repository vIPI (`D:\Programmazione\IVAO_Test\vIPI Ivao Italy\vIPI Ivao Italy`): `README.md`, `HANDOFF.md`, `docs/guide/integration.md`, `docs/lavori-aperti.md` §A (cutover MariaDB, A9 domande hosting), `deploy/atc-ivao/LEGGIMI-DEPLOY.md`, `LEGGIMI-SEGRETI.md`, `appsettings.Production.json`, `src/Vipi.Host/Auth/VipiStandaloneAuthExtensions.cs`
- Plesk: https://support.plesk.com/hc/en-us/articles/12377600431511-ASP-NET-Core-support-in-Plesk , https://support.plesk.com/hc/en-us/articles/12376965359511-Does-Plesk-support-Next-JS , https://support.plesk.com/hc/en-us/articles/12377519856023-Which-NET-versions-are-supported-by-Plesk
- Pomelo EF Core MySql: https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/releases , https://www.nuget.org/packages/Pomelo.EntityFrameworkCore.MySql
