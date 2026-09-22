# IVAO Division Hub — Piano di implementazione di M2

> Documento **interno** (italiano). Fonte di verità: `00-piano-di-progettazione.md` (§13, M2).
> Si scrive in due parti. La **parte A** sono i prerequisiti che il piano mette «prima di tutto»:
> i moduli fuori dai dipartimenti (`decisions/2026-09-13-moduli-non-subordinati-ai-dipartimenti.md`
> §4). La **parte B** sono le dashboard, decise nella nota `2026-09-13-le-dashboard-a-tutto-schermo`.
> La **parte C** — il modulo dei tour (`flightops`), che dal 13 settembre 2026 è il primo modulo
> (`decisions/2026-09-13-ordine-dei-moduli.md`) — è **scritta** (fase T0, 16 settembre 2026, piano 0.79): le fasi T1–T21 dal
> design `05-design-m2.md` §14, chiuso il 15 settembre 2026.

## A. I prerequisiti: i moduli fuori dai dipartimenti

Tre fasi, una PR ciascuna, da `main`, nell'ordine. Nessuna tocca i contenuti editoriali: una pagina,
una news, un documento restano di un dipartimento solo (nota §3.3).

| Fase | Titolo | Dipende da | In una riga |
|---|---|---|---|
| H1 | I grant a una posizione — **fatta il 13 set 2026** | — | il soggetto di un grant è un VID **oppure** un dipartimento con uno o più livelli; seed una volta da `division.json` |
| H2 | Le righe di più dipartimenti — **fatta il 13 set 2026** | H1 | `IOwnedByDepartment` a insieme nel filtro, nel handler, nell'interceptor e nelle liste; `modules.<key>.baseDepartment`; un modulo di prova con una tabella nei test |
| H3 | Le sezioni dei moduli nella barra dello staff — **fatta il 13 set 2026** | H2 | Contenuti · un gruppo per modulo · Dipartimenti · Amministrazione |

### H1 — I grant a una posizione

Nota §3.2. Branch `m2/h1-position-grants`.

**Deciso con Carmine il 13 settembre 2026**: la posizione si indica con **dipartimento + uno o più
livelli** (coordinator, assistant, advisor, membro), non con il codice della posizione. Chi arriva a
quel ruolo prende il permesso da solo e chi lo lascia lo perde; regge quando IVAO rinumera le
posizioni.

1. **Il soggetto.** Sulla stessa tabella `hub_user_grants`: `vid` diventa facoltativo, e si
   aggiungono `position_department` e `position_levels_json` (l'elenco dei livelli). Un grant ha
   **esattamente uno** dei due soggetti. Stessi `value`, `department` (lo scope: `null` = ogni
   dipartimento), `effect`, `expires_at`, `reason`, stesso audit. Migrazione additiva.
2. **I permessi effettivi.** `EffectivePermissionsCalculator` riceve i grant del VID e quelli di
   posizione; un grant di posizione vale per chi ha **almeno una posizione** con quel dipartimento e
   uno di quei livelli. Le regole restano una: niente permessi globali da un grant (quindi mai
   `Permissions.Manage` né il superadmin), deny che vince, scadenza. **La sospensione** resta dei
   grant a un VID (chi perde ogni posizione): un grant di posizione non ha nessuno da sospendere, smette
   di valere perché nessuno ha più la posizione.
3. **Le sessioni.** Scrivere un grant di posizione rinfresca la sessione di **chi ha quella
   posizione** adesso, nella stessa transazione; e un grant modificato rinfresca anche chi aveva il
   soggetto di prima. `IAffectsUserSession` si allarga alla posizione invece di aggiungere un secondo
   meccanismo.
4. **Il seed.** `division.json → positionGrants: [{ department, levels, permission, scope, deny }]`,
   letto **una volta** (una riga in `hub_division_settings` ricorda che è stato applicato), poi la verità
   è la tabella e si cambia dalla schermata. Validato all'avvio come il resto di `division.json`.
5. **La schermata.** `/staff/admin/permissions`: il form chiede prima «a chi» (un VID o una posizione)
   e poi disegna i campi di quel soggetto; la lista mostra il soggetto in una colonna sola.
6. **I test.** Unit: un grant di posizione vale per chi ha il livello, non per gli altri livelli né per
   un altro dipartimento; non conferisce un permesso globale; un deny di posizione morde. Integrazione:
   un grant di posizione arriva alla richiesta dopo, per chi ha la posizione, e la sua rimozione a quella
   dopo ancora; il seed si applica una volta sola.

**Criteri**: «AOD, coordinator e assistant → `Links.Edit` su ED» dato dalla schermata vale per un
coordinator AOD senza rifare il login e non per un membro AOD; tolto, smette di valere; con
`positionGrants` nel file, il primo avvio crea le righe e il secondo non le ricrea anche se nel frattempo
le si è cancellate.

**Fatta il 13 settembre 2026** (branch `m2/h1-position-grants`). Com'è andata:

- **Il soggetto** sta sulla riga come da punto 1: `vid` facoltativo, `position_department`,
  `position_levels_json` (letto e scritto dall'entità come elenco, come le raccolte di G20).
  `UserGrant.IsHeldThrough(positions)` è l'unica risposta a «questo grant di posizione vale per chi ha
  queste posizioni?», e la usano il calcolo dei permessi e la ricerca di chi può approvare una pagina
  (G19), che ora trova anche chi ha `Content.Approve` per posizione. Migrazione `AddPositionGrants`
  (additiva: `vid` diventa facoltativo, due colonne e un indice).
- **Il validatore**: esattamente un soggetto (`errors.grant.subject` sul campo `vid`), almeno un livello
  per una posizione (`errors.grant.levelsRequired`); il controllo «il VID è staff» vale solo per un grant
  a un membro.
- **Le sessioni**: `IAffectsUserSession` ha un secondo membro con un default, `AffectedPosition`;
  l'interceptor raccoglie le posizioni e, dopo la scrittura e nella stessa transazione, le traduce nei
  VID che le hanno. In più, **una riga modificata rinfresca anche il soggetto di prima** (i valori
  originali), cosa che per i grant a un VID prima non succedeva: spostare un grant da un membro a un
  altro lasciava al primo il cookie vecchio fino al login successivo.
- **Il seed**: `PositionGrantSeeder`, chiamato all'avvio dopo i superadmin; la riga
  `positionGrants.seeded` in `hub_division_settings` ricorda che è stato applicato. Salta con un avviso
  un permesso sconosciuto o globale. `division.example.json` e `FORKING.md` lo spiegano;
  `division.json` di IT non ne ha ancora, perché non c'è ancora un permesso di modulo da dare.
- ⚠️ **Scostamento sul punto 5**: il form **non** chiede prima «a chi» per poi disegnare i campi di quel
  soggetto. Il generatore di form non ha campi condizionali, e aggiungerli sarebbe un'estensione per un
  form solo: i due soggetti stanno uno sotto l'altro, con un suggerimento per ciascuno, e il server
  risponde sul campo quando se ne compila nessuno o tutti e due. La lista mostra VID, dipartimento e
  livelli in tre colonne.
- **I test**: tre unit (vale per i livelli giusti e non per gli altri né per un altro dipartimento; mai
  un permesso globale; un divieto di posizione morde), tre d'integrazione (il grant di posizione arriva
  alla richiesta dopo per chi ha la posizione e la rimozione a quella dopo ancora; un soggetto solo; il
  seed applicato una volta e non ricreato dopo una cancellazione), due Vitest sul payload.
- **Verificato in locale**: unit .NET (312), Vitest (389), smoke (77), lint, typecheck, formato, i18n.
  **Non in locale**: integrazione (Docker spento), che esegue la CI.

### H2 — Le righe di più dipartimenti

Nota §3.3. Branch `m2/h2-owned-by-several`, **impilato su H1** (usa i grant di posizione nei test).

**Fatta il 13 settembre 2026.** Le decisioni tecniche, prese con il codice davanti:

- **L'insieme è una maschera di bit** in una colonna della riga (`owner_department_mask`), non una
  tabella di collegamento né un elenco JSON: «almeno uno in comune» diventa `(riga & lettore) != 0`,
  che il filtro globale scrive su una colonna e un parametro, mentre una tabella vorrebbe un join che un
  query filter non sa scrivere. ⚠️ **Il bit di ogni dipartimento è scritto a mano** in
  `DepartmentMask` e non dipende dall'ordine dell'enum, perché è un valore salvato: un dipartimento
  nuovo va in fondo, e un test fissa i bit.
- **Una interfaccia sola**, come chiedeva la nota: `IOwnedByDepartment` ha due membri con un default,
  `OwnerDepartmentMask` (per una riga editoriale è il bit del suo dipartimento) e `OwnerDepartments`.
  Una riga di modulo in cura a più dipartimenti **dichiara** `OwnerDepartmentMask` come proprietà
  scrivibile, che diventa la sua colonna; `OwnerDepartment` resta, ed è il dipartimento di base.
- **L'unico handler** chiede «tenuto su uno dei dipartimenti della riga», senza rami. **Il filtro
  globale, la narrowing delle liste e `DataBlockScope`** costruiscono l'espressione giusta per il tipo:
  `Contains` sul dipartimento per una riga editoriale, la maschera per una riga di modulo.
- **L'interceptor** chiede il permesso su almeno uno dei dipartimenti della riga — «chi crea ci mette
  un dipartimento su cui ha il permesso» — e, se l'insieme cambia, anche su uno di quelli di prima.
- **Il dipartimento di base** (`ModuleBaseDepartment`) si rimette sulla riga in due punti: nel motore
  CRUD subito dopo l'applicazione del payload, così il permesso si controlla sulla riga com'è
  davvero, e nell'interceptor, per ogni altra strada. Trovato leggendo il motore prima della CI: il
  controllo dopo l'applicazione avrebbe rifiutato all'ED un evento «SOD» dal payload.
- ⚠️ **I contesti dei moduli non avevano il filtro globale**: stava solo in `HubDbContext`. Adesso un
  modulo deriva da **`ModuleDbContext`**, che applica lo stesso filtro sulle stesse proprietà
  (`IVisibilityScope`) e non lascia dimenticarlo (`OnModelCreating` è sigillato, il modulo scrive
  `ConfigureModel`).
- **`division.json → modules`** cambia forma: da `{ "specialops": true }` a
  `{ "specialops": { "enabled": true }, "events": { "baseDepartment": "ED" } }`. IT ha già eventi ED,
  tour FOD e training TD; `division.example.json` e `FORKING.md` lo spiegano (e `FORKING.md` perde
  l'esempio con `Department`, rimasto da prima di G16).
- **Il modulo di prova ha una tabella**: `SampleDbContext` con `smp_items`, migrazione generata da
  `dotnet ef` nel progetto dei test (che per questo referenzia `Microsoft.EntityFrameworkCore.Design`),
  gli endpoint CRUD e una lettura attraverso il filtro. Il test host dice `modules.sample.baseDepartment:
  ED`.
- **I test**: quattro unit su `DepartmentMask` (un bit per dipartimento, i bit fissati, andata e
  ritorno, riga editoriale e riga di modulo) e tre d'integrazione sulla spina dorsale a due dipartimenti
  (la base aggiunta e i due dipartimenti che gestiscono, il terzo fuori da lista e scrittura; chi crea
  mette un dipartimento suo; il filtro di un contesto di modulo legge l'insieme), con i permessi del
  modulo dati a posizioni come farà una divisione.
- **Verificato in locale**: unit .NET (316), build, `has-pending-model-changes` sui due contesti.
  **Non in locale**: integrazione (Docker spento), che esegue la CI.

### H3 — Le sezioni dei moduli nella barra dello staff

Nota §3.1. Branch `m2/h3-module-sections`, impilato su H2.

**Fatta il 13 settembre 2026.**

- **Il bootstrap dice di che modulo è ogni voce dello staff**: `NavItem.Module`, riempito da `/api/me`
  componendo le voci modulo per modulo invece che dalla lista già appiattita del registry. Le voci del
  nucleo e del menu editoriale non ne hanno.
- **La barra**: Contenuti · **una sezione per modulo** · Dipartimenti · Amministrazione, come dice la
  nota. Il titolo di una sezione è la chiave `nav.section` nel namespace del modulo
  (`events:nav.section`), che il modulo porta nei suoi file di lingua; una voce senza modulo finisce
  sotto «Moduli», come prima. Lo stesso elenco serve la barra e la palette ⌘K.
- **Il modulo di prova** ha una voce dello staff dietro `Sample.View`; un test d'integrazione prova che
  il bootstrap la dà con `module: "sample"`, e un test Vitest l'ordine delle sezioni e le voci di ciascuna.
- ⚠️ **Da ricordare in M2**: un modulo che apre la sua sezione deve avere `nav.section` nel suo
  namespace, altrimenti la barra mostra la chiave.
- **Verificato in locale**: Vitest (390), smoke (77), lint, typecheck, formato, build .NET. **Non in
  locale**: integrazione (Docker spento), che esegue la CI.

Con H3 **la parte A è chiusa**. Il passo dopo, per il piano (§13), è la **nota sulle due dashboard
personali** (`/me` e `/staff`), poi `05-design-m2.md` e la parte B di questo documento.

## B. Le dashboard a tutto schermo

Nota `decisions/2026-09-13-le-dashboard-a-tutto-schermo.md`. Tre fasi, una PR ciascuna, da scrivere in
dettaglio all'inizio di ognuna con il codice davanti.

| Fase | Titolo | Dipende da | In una riga |
|---|---|---|---|
| D1 | La griglia a tessere — **fatta il 13 set 2026** | parte A | layout `grid` e `span` nell'envelope (walker, validazione, TypeScript); resa a tessera alta uguale per riga; resa a tutto schermo e barra compatta per `ContentKind.Dashboard`; le dashboard dei dipartimenti convertite |
| D2 | L'editor della griglia — **fatta il 13 set 2026** | D1 | spostare le tessere, ridimensionarle con la maniglia e con il selettore; il giro e2e che lo prova |
| D3 | `/me` e `/staff` — **fatta il 13 set 2026** | D1 | righe `me` e `staff` seminate; `/staff` smette di reindirizzare; i blocchi del nucleo (ciò che aspetta me, le mie bozze, calendario dei miei dipartimenti, i miei dipartimenti, il saluto); via il registro dei widget |

### D1 — La griglia a tessere

Branch `m2/d1-tile-grid`, impilato sulla nota (#74). **Fatta il 13 settembre 2026.**

- ⚠️ **Scostamento dalla nota §3.3: niente layout `grid`.** Una dashboard non ha bisogno di un layout
  nuovo: **ogni sezione di una riga `Dashboard` si disegna a tessere**, e la larghezza di un blocco è
  `span` nell'envelope (sei valori, validati dal walker come `column`, `errors.body.spanUnknown`). Un
  blocco senza `span` prende la **quota della sua colonna** (`spanOf`: ½ in una sezione a due colonne,
  ⅓ e ⅔ nelle altre), quindi le dashboard dei dipartimenti già seminate diventano tessere **senza
  convertire niente** — la nota prevedeva una conversione con il seeder, che non serve più.
- **La resa**: `ContentRenderer` con `dashboard` mette ogni sezione a tutta larghezza e ne disegna i
  blocchi in una griglia di 12 colonne (sotto `@view-md` una tessera per riga); ogni tessera ha bordo,
  e il contenuto oltre il 40% dell'altezza della finestra scorre dentro; le tessere di una stessa riga
  si allungano alla stessa altezza (è il comportamento della griglia). L'ordine è quello delle colonne,
  e dentro una colonna quello di scrittura.
- **Tutto schermo nel back office c'era già**: la cornice di `/staff` è una riga sola (`PageShell`
  compatto, 11 settembre) e il `<main>` è largo quanto lo spazio accanto alla barra; a stringere erano
  le larghezze delle sezioni, che una dashboard ora ignora. `/staff/{dept}` passa `dashboard`. `/me`,
  che sta nella cornice pubblica larga al massimo 1152 px, è di D3.
- **Non in D1**: l'editor di una dashboard mostra ancora la dashboard come una pagina (D2).
- **I test**: otto casi del walker su `span`; due Vitest sulla resa a tessere (larghezza dichiarata o
  della colonna, ordine, contenuto che scorre, niente larghezza di sezione; e una pagina che resta una
  pagina). **Verificato in locale**: unit .NET (324), Vitest (392), smoke (77), lint, typecheck, formato,
  i18n. Non guardata a occhio: nel browser integrato non si entra nel back office senza il login IVAO.

### D2 — L'editor della griglia

Branch `m2/d2-grid-editor`, da `main` dopo il merge della pila #71–#75. **Fatta il 13 settembre 2026.**

- **L'anteprima di una riga `Dashboard` è la griglia**: `PreviewFrame` passa `dashboard` al renderer, e
  si compone sulle tessere come sulle pagine — si clicca per scegliere, la barra e la maniglia di
  trascinamento sono quelle dei blocchi.
- **Spostare una tessera**: gli slot compaiono prima di ogni tessera e dopo l'ultima, ciascuno su una
  riga intera della griglia, per un blocco trascinato e per un componente dalla palette. La prima volta
  che si aggiunge o si sposta una tessera la sezione viene riscritta **come le sue tessere**
  (`asTiles`: ogni blocco con la larghezza con cui è mostrato, tutti nella stessa colonna, nell'ordine
  della griglia), così «prima della terza tessera» vuol dire la terza tessera.
- **La maniglia**: sul bordo destro della tessera scelta; trascinata misura in colonne la distanza dal
  bordo sinistro della tessera e scatta sulla più vicina delle sei larghezze; con il fuoco, le frecce
  destra e sinistra la allargano e la stringono. Eventi del puntatore, non dnd-kit, che il renderer non
  importa. Un gesto è un passo solo della cronologia (`coalesce`).
- **Il selettore**: «Larghezza della tessera» nel pannello del blocco, al posto della colonna.
- ⚠️ **Trovato dal giro completo**: il click con cui finisce il trascinamento cadeva sulla sezione e la
  sceglieva, togliendo la maniglia e il selettore; la maniglia ora ingoia quel click e solo quello.
- **I test**: Vitest su `asTiles`/`setSpan` e sulla maniglia da tastiera e gli slot; **un test del giro
  completo** (`e2e/full/dashboard.spec.ts`) che trascina la maniglia fino a un quarto, allarga dal
  selettore a metà e trova la larghezza salvata — come chiedeva la nota.
- **Verificato in locale**: Vitest (394), smoke (77), giro completo (19, MariaDB vera), lint,
  typecheck, formato, i18n.

### D3 — `/me` e `/staff`

Branch `m2/d3-personal-dashboards`, da `main` dopo il merge di D2 (#76). **Fatta il 13 settembre 2026.**

- **Via il registro dei widget**, nei due lati: `IModule.Widgets`, `WidgetDescriptor`, `WidgetRegistry`,
  `registries.widgets` di `/api/me`, i widget dei manifest, `features/me/widgets/`, la voce nella galleria
  e le chiavi `widgets.*`. Un modulo registra blocchi e basta.
- **Tre blocchi del nucleo** (`blocks/personal.tsx`, `CoreBlocks`): `welcome` (il saluto, con un messaggio
  che scrive il web team), `myDepartments` (i link alle dashboard dei miei dipartimenti) — tutti e due
  Content, perché il browser sa già chi guarda — e `myWork` (Data, `AlwaysLive`, provider
  `MyWorkProvider`) con `what` = `approvals` | `contacts` | `reviews` | `drafts`: un blocco con una scelta
  e non quattro blocchi, perché è una domanda sola con quattro risposte. Legge oltre il filtro (una bozza
  non passa il filtro) e restringe con gli stessi permessi delle schermate dietro i link
  (`Content.Approve`, `Contacts.View`, `Content.Edit`, autore). In palette stanno sotto Dati, sottogruppo
  nuovo **«Per chi guarda»** (`personal`).
- **Il calendario**: `myDepartments: true` mostra i dipartimenti di chi guarda. ⚠️ **Scostamento dalla nota
  §3.5.4**: un booleano accanto a `department` e non un valore di `department`, perché `department` è
  l'enum dei nove e un valore in più sarebbe stato un caso speciale nel selettore. E se il blocco viene
  **catturato** alla pubblicazione, «chi guarda» non c'è ancora: si usa il dipartimento della pagina, non
  quelli di chi ha premuto «pubblica».
- **Le righe**: `seed/content-pages/me.json` (Members, il saluto) e `staff.json` (Staff: quattro `myWork`
  da ¼, calendario e dipartimenti da ½), del dipartimento del sito. ⚠️ **Scostamento**: nascono da un
  **template nuovo**, `personal-dashboard` (una sezione libera `tiles`), e non da `dashboard`, che ha
  la sezione `welcome` obbligatoria e bloccata con il titolo del dipartimento: `seeds.test.ts` lo diceva.
- **Le schermate**: `DashboardScreen` (`features/content/`) è la schermata di `/staff/{dept}`, `/staff` e
  `/me`, cambia solo lo slug. «Modifica» porta alla riga che si sta leggendo, trovata dall'`id` della
  versione pubblicata. ⚠️ **Trovato qui**: prima la rotta del dipartimento prendeva la prima dashboard del
  dipartimento dalla lista, e da D3 il WD ne ha tre (`wd`, `me`, `staff`). L'editor torna dove la
  dashboard si legge (`dashboardAddress`). `/staff` smette di reindirizzare. `/me` chiede la larghezza
  intera con `staticData: { wide: true }`, che il layout dei membri legge e passa a `Shell`; le
  preferenze delle notifiche restano sotto. La barra di `/me` è il `PageShell` pubblico, con titolo e
  «Modifica» su una riga: il compatto è tarato sui margini del back office.
- **I test**: `MyWorkTests` (integrazione: pagina da approvare solo a chi approva, contatto al suo
  dipartimento e non a un altro, documento da rivedere a chi lo modifica, bozza al suo autore e non al
  direttore, niente a un visitatore), i conteggi dei blocchi Data (8) e della galleria (33), le dashboard
  del fork XX (nove + due), `seeds.test.ts` con il template nuovo, smoke `e2e/dashboards.spec.ts`
  (`/staff` con `myWork` e i dipartimenti, «Modifica» solo al WD, `/me` più largo della colonna).
- ⚠️ **Trovato dai test d'integrazione**: i VID 690001–690003 erano già di `SiteMenuAndDashboardTests`
  (690003 è il loro director), e le posizioni si **aggiungono** a quelle che un VID ha già nel DB
  condiviso: `MyWorkTests` usa 770001–770003.
- **Verificato in locale**: unit .NET (332), integrazione (203, MariaDB vera), Vitest (405), smoke (80),
  giro completo (19), lint, typecheck, formato, i18n. **Non guardata a occhio** nel browser: il back
  office chiede il login IVAO.

## C. Il modulo dei tour (`flightops`)

Scritta nella fase **T0** (15–16 settembre 2026, piano 0.79) da `05-design-m2.md` §14, chiuso il 15 settembre dopo quattro giri di
revisione con Carmine (PR #80). **Il design è la fonte**: qui c'è l'ordine, il perimetro di ogni fase, i test e quando è fatta; il
perché di ogni scelta sta nel design e nelle sei note di T0. Carmine ha portato il riscontro dello staff di IVAO il 13 settembre 2026
(nota `2026-09-13-ordine-dei-moduli`): **Tours, poi Training, poi Eventi**; coordinator e assistant del FOD hanno tutte le funzioni.

**Regole di tutte le fasi**, per non ripeterle venti volte:

- Una fase per sessione, un branch `m2/t<N>-<slug>` da `main`, una PR con la checklist compilata onestamente. Due fasi che toccano
  tutte e due una migrazione dello stesso contesto **non** partono in parallelo (memoria `parallel-worktree-sessions`).
- **Migrazioni solo additive**, una per fase e per contesto. L'`Initial` del modulo nasce in T5 e da lì non si tocca.
- **Test d'integrazione** sulla MariaDB condivisa con **VID `780001–780099`** e **slug `fo-test-…`** (design §13): un tour di prova si
  crea e si toglie nel test, niente dati lasciati (memorie `integration-tests-share-one-database`, `bench-database-accumulates`).
- **Nessuna chiamata a IVAO, NOAA, VATSIM, OpenAIP nei test**: fixture registrate. Le forme delle risposte si misurano **con il token
  vero** in sviluppo, nella fase che usa l'endpoint, e diventano la fixture.
- **Divisione XX**: ogni seed e ogni stringa del modulo passa il test del fork fittizio (niente ICAO italiani, niente italiano fuori da
  `locales/it/`).
- **Ogni scostamento dal design** si scrive nella fase, sotto «Com'è andata», come nelle parti A e B; se è una decisione, anche nel piano.
- Prima di pushare una fase con UI: `pnpm e2e:full` (memoria `running-the-hub-locally`).

**Il corpus dei voli di test** (design §13): Carmine lo manda il 16–17 settembre, con l'esito atteso per ogni controllo scritto secondo lo
**standard** che il sistema vuole fissare. Le sessioni del tracker diventano fixture in T2; gli esiti attesi diventano i test di T17–T18 e la
taratura del tempo stimato (`durationFactor`, `durationFixedMinutes`) e di `thresholdToleranceMeters`.

| Fase | Titolo | Dipende da | In una riga |
|---|---|---|---|
| T0 | Note, piano 0.79, questa parte — **fatta il 16 set 2026** | design chiuso | sei note di decisione, il piano 0.79, le fasi qui sotto |
| T1 | Nucleo: i dati di riferimento del mondo | — | aeroporti del mondo con IATA e coordinate, piste con le testate, tipi di aereo, confini dei FIR |
| T2 | Nucleo: il tracker e il meteo — **fatta il 16 set 2026** | — | sessioni, piani e tracce nel client IVAO con fixture di voli veri; `IWeatherSource` (NOAA → IVAO → VATSIM) |
| T3 | Nucleo: scope per risorsa e interessato — **fatta il 16 set 2026** | — | l'unico handler estende i grant a una riga e nega all'interessato |
| T4a | Nucleo: proiezioni dei moduli e file con scadenza — **fatta il 16 set 2026** | T3 | le righe di modulo proiettano davvero; più voci di calendario; `cms_media_uses` e il job |
| T4b | Nucleo: award e preferenze — **fatta il 16 set 2026** | T4a | award; preferenze dell'utente |
| T5 | Modulo: lo scheletro — **fatta il 16 set 2026** | T4a | progetto, contesto, permessi, `positionGrants`, impostazioni, profili e gruppi di aerei |
| T6a | I tour — **fatta il 16 set 2026** | T5 | modello, stato dalle date, nascondere ed eliminare, «pronto», template, proiezioni, il job del rilascio |
| T6b | Il briefing — **fatta il 18 set 2026** | T6a | l'editor del corpo estratto dall'editor dei contenuti, montato come scheda del tour |
| T7a | Le leg — **fatta il 18 set 2026** | T1, T6a | `fo_legs`, GCD e tempo stimato, eliminare e rinumerare, ritirare e ripristinare, `LegGrid`, aerei consentiti nel form, `Distance`, «pronto» dei tipi con leg |
| T7b | La forma del tour — **fatta il 21 set 2026** | T7a | hub e rotazioni, sottotour e `Container`, vincoli sul callsign, «pronto» di quei tipi, `CrudOptions.BeforeAuthorize` |
| T7c | Il tour `Open` | T7b | `open_goal` con i parametri, `fo_tour_constraints` (filtri e regole di sequenza), la scheda, «pronto» di `Open` |
| T8 | L'import delle leg — **fatta il 22 set 2026** | T7a | XLSX e CSV letti nel browser, differenze dal server, «fondi» e «sostituisci» |
| T9 | Regole ed errori | T6 | regole con parametri, errori, regole effettive, `errorCatalog`, copia delle regole |
| T10 | Il pubblico e la mappa | T7b, T7c, T9 | `/tours`, `/tours/{slug}`, `RouteMap`, `tourCards` |
| T11 | Il PIREP | T2, T3, T9, T10 | `TourRules`, ricerca nel tracker, form, controlli che bloccano, deviazioni, iscrizione, snapshot |
| T12 | Gli ATC contattati | T1, T11 | proposta dal server, esenzioni, `IAtcActivitySource` |
| T13 | La validazione | T11 | code, presa in carico, pagina, suggerimento, decisione, mail, riapertura, riepilogo, `reviewQueue` |
| T14 | Contestazioni, chiarimenti, segnalazioni | T4a, T13 | i contatti con le risposte; la contestazione che sblocca; `openIssues` |
| T15 | Completamento, validatori, piloti, ban | T4b, T13 | segnalazione dell'award, statistiche e «aggiungi validatore», pagina del pilota, ban, `myTours` |
| T16 | Il meteo salvato | T2, T13 | job ogni 30 minuti, scarico all'invio, cancellazione, meteo nella pagina di validazione |
| T17 | Il motore dei controlli e i controlli sul piano | T9, T13 | `IFlightCheck`, job, `fo_check_results`, suggerimenti; `callsign`, `aircraft`, `alternate`, `equipment`, `repeatedRoute` |
| T18 | I controlli sulle tracce | T1, T16, T17 | disconnessioni, parcheggio, 250 kt, sim rate, atterraggio, decollo dalla testata, `vmc`; tarature |
| T19 | Token personali e contratto dell'agente | T3, T17 | `hub_personal_tokens`, lo schema `Bearer` per `audience`, `/api/flightops/agent` |
| T20 | Conservazione, rifiniture, giro completo | tutte | job mensile, cancellazione dei dati di un pilota, smoke, giro e2e, documenti |
| T21 | L'app del validatore parla con l'hub | T19 | nel repository `AutomaticValidatorTour`, fuori da questo; la mail a Navigraph prima di distribuirla |

**Parallelismo possibile** (se servisse): T1, T2 e T3 non si toccano; T8 e T9 nemmeno; T12, T14, T15 e T16 dopo T13 toccano pezzi
diversi ma tutti `FlightOpsDbContext`, quindi **in fila** per le migrazioni. **T4b e T5** migrano contesti diversi e possono andare in
parallelo; l'unico punto in comune è `IModule`, se T4b gli fa dichiarare le chiavi delle preferenze.

### T0 — Note, piano 0.79, parte C

Branch `m2/t0-decision-notes`, impilato su `m2/tours-design` (#80). **Fatta il 16 settembre 2026** (PR #81, entrata in `main` con la #83).

- **Sei note** in `decisions/`: `2026-09-15-permessi-su-una-riga-e-chi-ha-interesse`, `…-contatti-con-risposte`, `…-la-mappa`,
  `…-meteo-e-confini-dei-fir`, `…-token-personali-e-agente-del-validatore`, `…-file-con-scadenza`.
- **Misure e verifiche**: la mappa di base misurata sul file di Protomaps (179 MB fino allo zoom 7); MapLibre 6 senza build CSP e con
  `blob:` in `img-src`; NOAA con la storia dei METAR a 18 giorni e **anche dei TAF**; OpenAIP con licenza CC BY-NC 4.0 dallo schema
  dell'API (pagina legale non letta, 403); Navigraph con i termini degli abbonati non letti (pagina JavaScript).
- **Decise con Carmine il 15 settembre**, sulle tre proposte ancora aperte: l'app Python la adatta Claude dopo T19 (nasce **T21**); la
  mappa fino allo zoom 7; le immagini le collega al tour chi modifica il tour.
- ⚠️ **Trovato leggendo il codice**: `IProjectable` non proietta le righe di un modulo (il contesto del modulo non ha le tabelle
  delle proiezioni, e l'interceptor salta in silenzio). Si corregge in **T4**, prima di tutto il resto.
- **Scostamenti dall'elenco del design §14**: `myTours` passa da T10 a **T15**, perché senza PIREP non ha niente da mostrare; la tabella
  `fo_bans` nasce in **T11** (il PIREP deve già rifiutare un pilota bannato) e la sua schermata resta in T15; nasce **T21**.

### T1 — Nucleo: i dati di riferimento del mondo

Design §1.5, §1.12, §3.3; note `meteo-e-confini-dei-fir` §3.2. Branch `m2/t1-world-reference-data`.

1. **Aeroporti del mondo**: `RefDataSyncJob` legge `/v2/airports/all` invece degli aeroporti del paese; `ref_ivao_airports` guadagna
   `iata`, `latitude`, `longitude` (e l'elevazione, che serve al filtro `ArrivalElevationMin`, se l'API la dà). Convertitori tolleranti
   sui numeri facoltativi. ⚠️ **`FirDirectory` e `networkStats`** oggi deducono gli aeroporti della divisione dalla tabella intera:
   filtrano per paese. Da verificare con i test esistenti di M1 che non cambi niente per IT.
2. **Piste**: `ref_ivao_runways` (`airport_icao`, `runway`, latitudine e longitudine della testata, `bearing`, `length`, `width`) da
   `/v2/airports/{icao}/runways`, **non per tutto il mondo**: `IRunwayDirectory.EnsureAsync(icaos)` le scarica per gli aeroporti che le
   chiedono (le leg in T7, i PIREP in T11) e il job le rinfresca per gli aeroporti già presenti.
3. **Tipi di aereo**: `ref_ivao_aircraft` (codice ICAO, costruttore, modello, categoria di scia, varianti) da `/v2/aircrafts/all` e
   `/{aircraftId}/variants`; `ref_ivao_aircraft_equipments` e `ref_ivao_transponder_types` da `/equipments` e `/transponderTypes`. Il
   selettore di un tipo ICAO per il form generato (un campo che cerca nella tabella), riusabile da tour, leg, profili, gruppi.
4. **Confini dei FIR**: `ref_firs` da OpenAIP (tipi 10 e 11, paginati, chiave nei segreti), job settimanale che non cancella su
   risposta vuota; `IFirLocator` («in quali FIR sta questo punto», poligoni in cache). Senza chiave il job non parte.
5. **Misurare** con il token vero le forme di `/v2/airports/all`, `/runways`, `/v2/aircrafts/*` e salvarle come fixture.

**Test**: unit sui convertitori con le fixture vere; `IFirLocator` su punti dentro, fuori e sul bordo, con un poligono che attraversa
l'antimeridiano; integrazione del job con il client fittizio (aggiunge, aggiorna, non pota su risposta vuota); architettura: solo il nucleo
nomina la fonte dei FIR; i test di M1 su `FirDirectory` e `networkStats` restano verdi con aeroporti di altri paesi nella tabella.
**Fatta quando**: in sviluppo, con il token vero, le tabelle si riempiono (numeri scritti nella PR), LIRF ha IATA, coordinate e le sue
piste dopo un `EnsureAsync`, e un punto su Roma risponde LIRR.

**Fatta il 16 settembre 2026** (branch `m2/t1-world-reference-data`, PR #84). Com'è andata:

- **Gli aeroporti sono quelli del mondo**: `/v2/airports/all` senza paese — **44 689 aeroporti di 235 paesi, 13,9 MB, tre secondi**,
  misurati con il token vero — e la riga guadagna `iata` (7732 ne hanno), `latitude`, `longitude` ed `elevation`. Il job li scrive con il
  rilevamento delle modifiche **spento dentro il ciclo** (con 45 000 righe diventa quadratico) e riacceso subito dopo: `SaveChanges` passa
  dall'interceptor come sempre.
- ⚠️ **Il filtro per paese, che il design chiedeva di verificare**: `FirDirectory.GetAirspaceAsync` prendeva **tutta** la tabella, quindi
  con il mondo dentro ogni volo del pianeta sarebbe risultato «della divisione». Adesso filtra su `division.countryId`, e un test
  d'integrazione lo fissa (LIRF sì, LFPG e KJFK no).
- **Le piste non si scaricano per il mondo**: sarebbero 45 000 chiamate per un dato che quasi nessuno legge. `IRunwayDirectory.EnsureAsync`
  le prende per gli aeroporti che servono (le leg in T7, i PIREP in T11) e non le richiede due volte. La riga porta **la coordinata della
  testata**, che è tutta la ragione della tabella (`takeoffFromThreshold`, T18).
- **I tipi di aereo** (2626) con costruttore, modello, categoria di scia e numero di motori, più i due vocabolari del piano di volo
  (36 equipaggiamenti, 17 transponder). Non si potano mai: un PIREP di tre anni fa può nominare un tipo che IVAO ha tolto, e il registro
  disciplinare deve restare leggibile (design §10.1).
- ⚠️ **Le «varianti» non sono quello che il design credeva** (§1.5): `/v2/aircrafts/{icao}/variants` dà le varianti **dello stesso tipo**
  (`A320w`, `A320CFM`, `A320IAE`), non i tipi imparentati — **`A20N` non è una variante di `A320`** — e un piano di volo non porta mai
  una variante, solo il codice ICAO. Quindi la bandiera «anche le varianti» del tour **non ha l'effetto che si voleva**: quello che serve
  («ammetti anche i neo») lo dice un **gruppo di aerei**, che il design ha già. Da correggere in §1.5 quando si scrive T6/T7; qui le
  varianti non si sincronizzano. In più, `/v2/aircrafts/{icao}` risponde **500** (rotto lato IVAO il 16 settembre).
- **I confini dei FIR cambiano fonte**: OpenAIP ne ha 108 nel mondo, e la nota `2026-09-16-i-confini-dei-fir.md` racconta la misura e la
  decisione di Carmine. `ref_firs` si riempie dal dataset di **VATSpy** (1121 confini, solo quelli interi e non i settori), con un job
  settimanale che non svuota la tabella se la risposta è vuota, e `IFirLocator` risponde «in quali FIR sta questo punto» con il riquadro
  come prefiltro e il ray casting sui poligoni. **Attribuzione** obbligatoria (CC BY-SA 4.0), pronta come costante: va mostrata in T12,
  dove il dato derivato si vede.
- **I test**: 16 unit nuovi (otto sui confini e sul punto dentro il poligono, uno di architettura sul perimetro della fonte, più quelli del
  meteo rimasti verdi) e quattro d'integrazione (il mondo nella tabella, lo spazio aereo che resta della divisione, le piste prese una volta
  sola, i tipi e i vocabolari). Unit **357**, verdi.
- ⚠️ **Trovato scrivendo i test**: su una macchina italiana l'interpolazione di stringa scrive `36,5`, che in JSON non è un numero. I
  numeri dei test di geometria passano dalla cultura invariante.
- **Non verificato in locale**: integrazione e giro e2e (Docker spento), che esegue la CI; e **quanto ci mette davvero** il primo
  riempimento di 44 689 righe su MariaDB vera — da guardare nel log della CI e sullo staging.

### T2 — Nucleo: il tracker e il meteo

Design §3.2, §3.4, §6.2, §1.13; nota `meteo-e-confini-dei-fir` §3.1. Branch `m2/t2-tracker-and-weather`.

1. **Tracker in `IIvaoApiClient`**: `SearchSessionsAsync(userId, departure?, arrival?, from, to)` su `/v2/tracker/sessions`,
   `GetFlightPlansAsync(sessionId)` (**tutte** le revisioni) e `GetTracksAsync(sessionId)`. Tipi del nucleo, non del modulo. Il client
   fittizio legge le fixture.
2. **Le fixture del corpus**: le sessioni dei voli di test di Carmine (e i 39 log di Toursystem dove servono), registrate con uno script in
   `tools/` che le scarica con il token vero e le salva anonimizzate (VID sostituiti con `780…`). Ogni fixture porta un file con l'esito
   atteso per controllo, come l'ha scritto Carmine.
3. **`IWeatherSource`** in `Core/Weather/`: NOAA (file di cache per «attuali», API con `date` e `hours` per la storia, METAR e TAF), IVAO
   (`/v2/airports/{icao}/metar`, minuscolo, dal client unico), VATSIM; catena di ripiego solo per il METAR attuale; `User-Agent`; Polly.
4. **Misurare**: il campionamento delle tracce IVAO a terra (serve a T18), e fin dove arriva la storia dei TAF su NOAA.

**Test**: unit sulla lettura delle fixture (revisioni del piano in ordine, revisione al decollo individuata, tracce con disconnessioni);
la catena del meteo con fonti finte (NOAA giù → IVAO → VATSIM; TAF senza ripiego); architettura: nessun modulo nomina NOAA o VATSIM.
**Fatta quando**: le fixture del corpus sono nel repository e i test le leggono; in sviluppo, la storia di METAR e TAF di un aeroporto
per un volo di una settimana fa arriva da NOAA.

**Fatta il 16 settembre 2026** (branch `m2/t2-tracker-and-weather`, PR #82). Com'è andata:

- **Il tracker sta nell'unico client**: `SearchSessionsAsync` (paginata, cinquanta per pagina, con i filtri `departureId` e `arrivalId`
  che l'API ha davvero), `GetFlightPlansAsync` (**tutte** le revisioni, dalla prima) e `GetTracksAsync`. **`null` vuol dire «non abbiamo
  potuto guardare»**, e non è la stessa risposta di una lista vuota: a un pilota sicuro di aver volato non si dice «non c'è» quando
  l'API non ha risposto. `IvaoTrackerReader` legge i payload una volta sola, per il client vero e per quello a fixture.
- **Le fixture sono voli veri**: tre tratte (LIRQ–LXGB–LPMA–LPBJ) registrate con `tools/record-ivao-fixtures.mjs` e **anonimizzate** a
  VID 780001, senza l'oggetto `user` che IVAO annida. Un parser provato su JSON inventato prova solo che l'invenzione è stata letta.
- **Il meteo è `Core/Weather/`**: NOAA, poi il METAR di IVAO, poi VATSIM; il TAF senza ripiego; la storia è NOAA o niente. Un test di
  architettura tiene il perimetro: **nessun file fuori da `Core/Weather` nomina un fornitore di meteo** (e il commento di `Program.cs`
  è stato riscritto proprio per questo).
- **Misurato contro i servizi veri il 16 settembre**, e quattro cose correggono il design:
  1. **Il TAF passato c'è** (§1.13 diceva di no): si chiede con `date` da solo, perché `hours` su un TAF è un 400.
  2. **NOAA tiene trenta giorni** di storia, e lo dice lui stesso quando gliene chiedi di più.
  3. **Non esiste il file di cache dei TAF** (404): solo i METAR (240 KB gzip, tutto il mondo). I TAF si chiedono a blocchi di quaranta.
  4. **`/v2/airports/{icao}/metar` risponde anche in maiuscolo**: la regola «minuscolo, il maiuscolo dà 404» non vale più.
- **Altre due misure, che servono dopo**: IVAO tiene i **punti delle tracce circa novanta giorni** (a 90 sì, a 91 no), e il
  **campionamento è di circa 15 secondi** su un volo lungo (minimo 4, massimo 20; 5 su uno corto).
  ⚠️ **Per T18**: con un punto ogni quindici secondi la tolleranza di 150 m di `takeoffFromThreshold` **non è misurabile a quella
  precisione** dalla sola traccia. Il controllo parte dall'ultimo punto **fermo** prima della corsa, e il numero si tara sul corpus.
- **Il corpus dei voli di Carmine non c'era ancora** (arriva il 16–17): le fixture di oggi sono il ponte, e i suoi voli si aggiungono con
  lo stesso script quando arrivano. Gli esiti attesi restano il materiale di T17–T18.
- **Verificato in locale**: unit .NET **347** (16 nuovi), build, formato. **Non in locale**: integrazione e giro e2e (Docker spento),
  che esegue la CI.
- ⚠️ **Com'è andato il merge**: la PR #82 è entrata in `main` **prima** di T0, perché la PR di T0 (#81) era impilata sul branch del
  design e il suo merge è finito **dentro quel branch** invece che in `main`. Il contenuto di T0 è tornato in `main` con la PR di
  recupero del 16 settembre. La regola della memoria `stacked-pr-base-deletion` vale anche prima della cancellazione: **una PR impilata
  va ritargettata sulla base nuova prima di mergiarla**, altrimenti mergia nella vecchia.

### T3 — Nucleo: scope per risorsa e interessato

Nota `2026-09-15-permessi-su-una-riga-e-chi-ha-interesse`. Branch `m2/t3-resource-scope`.

1. `hub_user_grants.resource_scope`, migrazione `AddGrantResourceScope`.
2. `EffectivePermission.ResourceScope`, il claim `Name:DEPT@scope` (i cookie vecchi si leggono ancora), `PermissionSet.Has` con e senza scope.
3. `IHasResourceScope`, `IHasStakeholder`, `PermissionDescriptor.DeniedToStakeholder`; il controllo dell'interessato **per primo** nel handler.
4. La colonna dello scope nella lista dei grant (in sola lettura); lo scope nei permessi effettivi di `/api/me`.
5. Il modulo di prova (`SampleModule`) guadagna una risorsa con scope e interessato, così la spina dorsale si prova senza aspettare i tour.

**Test**: quelli della nota §5, fra i test della spina dorsale; più: un superadmin interessato riceve no e la lista in sola lettura sì.
**Fatta quando**: tutti i test della spina dorsale passano, e un grant con scope dato a mano nel DB di sviluppo si vede in `/api/me`.

**Fatta il 16 settembre 2026** (branch `m2/t3-resource-scope`, PR #85). Com'è andata:

- **Tutto come nella nota**, senza scostamenti di forma: `hub_user_grants.resource_scope` (migrazione `AddGrantResourceScope`, una
  colonna in `Up`), `EffectivePermission.ResourceScope`, il claim `Nome:DIP@scope` con il `@` **dopo** il dipartimento così un cookie
  scritto prima si legge uguale, `IHasResourceScope` e `IHasStakeholder` in `DomainContracts.cs`,
  `PermissionDescriptor.DeniedToStakeholder` e `PermissionCatalog.IsDeniedToStakeholder`.
- **La regola sta in `PermissionSet`, una volta**: un permesso senza scope raggiunge ogni riga come sempre; uno con scope raggiunge solo
  la riga con lo stesso scope, e **non** risponde a `Has` chiesto senza riga. `HasAny` li conta tutti: è ciò che apre la coda in sola
  lettura a un validatore abilitato su un tour solo (design §4.1).
- **Il handler controlla l'interessato per primo**, prima di ogni permesso e quindi anche del superadmin: è l'unico no che un superadmin
  riceve, ed è voluto. La lettura non è toccata, perché nessun permesso di lettura è segnato.
- **Due grant sullo stesso permesso e dipartimento ma su due righe sono due permessi**: il calcolo li distingueva per (nome,
  dipartimento) e ne avrebbe tenuto uno solo. Ora la chiave comprende lo scope, e un test lo fissa.
- **La schermata dei permessi mostra lo scope e non lo scrive** (colonna «Solo su»): lo scrive il modulo che conosce le righe. `/api/me`
  porta lo scope nei permessi effettivi, e i tipi generati della SPA si sono allungati di conseguenza (dieci oggetti di test di
  `staffDestinations.test.tsx` hanno ricevuto `resourceScope: null`).
- **Il modulo di prova** ha `Sample.Decide` (negato all'interessato), la riga `smp_items` con `stakeholder_vid` (migrazione
  `AddSampleStakeholder` nel progetto dei test) e un endpoint `POST /api/sample/items/{id}/decide` che chiede il permesso all'unico
  handler **con la riga in mano**: la forma che avrà «Prendi» su un PIREP.
- **I test**: otto unit (`ResourceScopeAndStakeholderTests`: scope sì e no, `Has` senza riga, il cookie con e senza scope, due grant su due
  righe, il handler con lo scope, nessuno decide sé stesso, nemmeno il superadmin, una riga senza interessato) e due d'integrazione
  (`ResourceScopeTests`: il grant su una riga raggiunge quella e non la vicina e arriva in `/api/me`; il superadmin non decide la riga che
  lo riguarda e la legge). VID `780021–780023`, titoli `fo-test-scope-…`.
- **Non fatto, e detto**: la sospensione di un grant con scope quando il validatore lascia lo staff non ha un test suo. Non c'è codice
  nuovo da provare — la sospensione riguarda ogni grant a un VID, con o senza scope — ma la nota la elencava fra i test: resta per T15,
  quando «togli validatore» e la sospensione avranno la loro schermata.
- **Verificato in locale**: unit .NET **365**, Vitest **405**, lint, typecheck, formato, i18n, build. **Non in locale**: integrazione e
  giro e2e (Docker spento), che esegue la CI.

### T4 — Nucleo: proiezioni dei moduli, award, file con scadenza, preferenze

Note `2026-09-15-file-con-scadenza` e `2026-09-15-contatti-con-risposte` §3.3; design §3.11, §4.1, §9. Branch `m2/t4-core-for-modules`.
Se in apertura risulta troppo per una PR, si divide in **T4a** (punti 1–3) e **T4b** (punti 4–5), scritto qui.

1. **Le righe di un modulo proiettano davvero**: `ModuleDbContext` mappa `cms_search_index`, `cms_calendar_entries`,
   `cms_award_signals` (e da qui `cms_media_uses`) **escluse dalle migrazioni del modulo**; l'interceptor le scrive nella transazione del
   modulo. Prima il test che fallisce sul modulo di prova, poi la correzione.
2. **Più voci di calendario per riga**: `ProjectionSnapshot.Calendar` diventa un elenco; il writer le riscrive per intero per
   `source_module` + `source_id` (una chiave in più, il numero della voce). Le righe esistenti del nucleo passano un elenco di uno.
3. **Usi dei file con scadenza**: `MediaUseProjection`, `cms_media_uses`, `UsesOfMediaAsync` sulle due tabelle, il servizio di
   eliminazione estratto da `MediaEndpoints.DeleteAsync`, `MediaExpiryJob`, l'avviso nella lista dei media.
4. **Award**: `hub_awards` (nome e descrizione `Localized`, immagine dalla media library, dipartimento, criterio) e
   `hub_award_assignments` (VID, award, motivazione, segnalazione d'origine), lista e form generati, la coda delle segnalazioni che
   esiste già (`cms_award_signals`) con «assegna» e «scarta». `Awards.Assign` come nel piano §9.1. L'immagine usata da un award è un uso
   senza scadenza.
5. **Preferenze dell'utente**: `hub_user_preferences` (`vid`, `key`, `value_json`), `GET`/`PUT /api/me/preferences/{key}` con le chiavi
   dichiarate dai moduli (una chiave sconosciuta è 400). Come le preferenze delle notifiche, che restano dove sono.

**Test**: una riga del modulo di prova compare in ricerca e in calendario con due voci, e sparisce con un `null`; il rollback non lascia
proiezioni; i test della nota dei file; un award assegnato da una segnalazione la segna gestita; una preferenza si legge da un altro
cookie dello stesso utente e non da un altro utente.
**Fatta quando**: i test sopra e quelli della spina dorsale passano; in sviluppo il job dei media gira e scrive la sua riga di log.

**Divisa il 16 settembre 2026** in apertura (Carmine): **T4a** = punti 1–3, branch `m2/t4a-module-projections`; **T4b** = punti 4–5,
in una chat nuova, branch `m2/t4b-awards-and-preferences`. I punti 1–3 si tengono fra loro (gli usi dei file dipendono dalla
correzione delle proiezioni), award e preferenze no.

**T4a fatta il 16 settembre 2026** (branch `m2/t4a-module-projections`). Com'è andata:

- **Prima il test che fallisce**, come chiedeva la fase: `ModuleProjectionTests` su una riga nuova del modulo di prova (`SampleEvent`,
  tabella `smp_events`, migrazione `AddSampleEvents` nel progetto dei test) è stato visto **rosso in locale** con la ricerca vuota, e
  verde dopo la correzione.
- **La correzione**: `ModuleDbContext` mappa le quattro tabelle delle proiezioni con le configurazioni del nucleo ed
  `SetIsTableExcludedFromMigrations`, e le convenzioni del nucleo passano da `HubDbContext.ApplyConventions`, sigillate come
  `OnModelCreating` (un modulo aggiunge le sue con `ConfigureModuleConventions`). La migrazione del modulo di prova contiene **solo**
  `smp_events`: le tabelle del nucleo restano nella storia del nucleo.
- ⚠️ **Un contesto che non mappa le tabelle ora è un errore**: l'interceptor lancia invece di saltare. Il salto silenzioso era proprio
  il difetto, e tenerlo avrebbe nascosto il prossimo.
- **Più voci di calendario**: `ProjectionSnapshot.Calendar` è un elenco, la posizione diventa `cms_calendar_entries.sequence` e l'indice
  unico è `source_module, source_id, sequence`; una riga con meno voci perde quelle in fondo. Le voci dello staff restano la numero zero.
- **Gli usi dei file**: `MediaUseProjection`, `cms_media_uses` (una migrazione sola del nucleo per la fase,
  `AddMediaUsesAndCalendarSequence`, e non `AddMediaUses` come diceva la nota), riscritti per intero; lo stesso file nominato due volte
  dalla stessa riga è un uso, con la scadenza più lunga.
- ⚠️ **Trovato scrivendo**: la regola «una bozza non proietta» stava nell'interceptor e annullava **tutto** lo snapshot, quindi un tour in
  bozza avrebbe perso il banner, contro la nota §3. Ora una riga non pubblicata tiene **solo** gli usi dei file
  (`ProjectionSnapshot.Unpublished`).
- **Una domanda sola**: `ContentReferenceIndex.UsesOfMediaAsync` restituisce `MediaUsage` (pagine e usi dei moduli), con la regola in un
  posto (`IsInUse`, `DeletesOn`); `MediaDeletion` è l'eliminazione estratta da `MediaEndpoints`, usata dalla libreria e dal job. Il rifiuto
  per un uso di modulo ha la sua chiave, `errors.media.inUseByModule`, perché quello della pagina offre l'archivio e qui l'archivio non serve.
- **`MediaExpiryJob`** alle 04:00 della divisione, mezz'ora dopo il promemoria dei documenti: un file con tutti gli usi finiti va via con
  la stessa eliminazione a mano (riga d'audit di nessuno, file tolto dal disco se nessuna versione lo mostra), e i suoi usi con lui. Una
  pagina pubblicata vince anche qui: il file resta e i suoi usi pure, così si riguarda quando la pagina lo lascia.
- **L'avviso nella libreria** è una colonna, «Sarà eliminato il», vuota per quasi tutti i file. ⚠️ **Estensione del motore della lista**
  (piano 0.81, §16.6): `CrudOptions.ToListPage` mappa una pagina in una volta, perché la data è un fatto degli usi e non del file; due
  query per pagina. Nessuna schermata a mano.
- **I test**: tre d'integrazione sulle proiezioni del modulo (ricerca e due voci di calendario che si riscrivono e spariscono con un
  `null`; il rollback della transazione del modulo che non lascia niente; la bozza che tiene solo gli usi, la proroga e il cambio di banner),
  due sul job e sull'eliminazione (un uso vivo tiene il file, tutti finiti lo tolgono come a mano, un file mai dichiarato non si tocca;
  l'eliminazione a mano rifiutata, la data nella lista che segue la proroga e sparisce quando la riga lascia il file), cinque unit su
  `MediaUsage`. VID `780031–780032`, titoli `fo-test-proj-…` e `fo-test-media-…`.
- **Verificato in locale** (Docker acceso): integrazione **213** tutte verdi, unit .NET **370**, Vitest **405**, typecheck, lint, formato,
  i18n. **Non verificato**: il job lanciato dal suo orario nel DB di sviluppo — la riga di log la scrive il test d'integrazione, non
  un'esecuzione alle 04:00.

**T4b fatta il 16 settembre 2026** (branch `m2/t4b-awards-and-preferences`). Com'è andata:

- **Quattro domande a Carmine in apertura**, perché il piano non le decideva: nota `decisions/2026-09-16-award-e-preferenze.md`,
  piano **0.82**. Il catalogo è **del dipartimento** (`Awards.View`/`Awards.Edit`, nuovi, a coordinator, assistant e advisor come i
  link) e letto da tutti; **assegna chi ha `Awards.Assign`**, globale. La segnalazione **propone l'award** (`award_id`). **Nessuna
  mail.** Il membro **non vede mai** i suoi award nell'hub: li vede sul profilo IVAO.
- **Tre risorse del motore CRUD, nessun endpoint scritto a mano**: `/api/awards` (dipartimentale, `ISharedForReading` su tutte le
  righe), `/api/award-assignments` e `/api/award-signals` (globali dietro `Awards.Assign`, la forma dei grant). Assegnare da una riga
  della coda è **creare un'assegnazione con `signalId`**: `BeforeSave` controlla che la riga sia in attesa e dello stesso VID e la
  segna gestita nello stesso salvataggio; l'indice unico su `signal_id` chiude la corsa. Scartare è un `PUT` dello stato. I nomi degli
  award nella coda e nel registro vengono da `ToListPage` (T4a), una query per pagina.
- **Un award ricevuto non si elimina**, si ritira: `CrudOptions.Delete` lancia `DomainRefusalException` (`errors.awards.assigned`), e la
  FK `hub_award_assignments.award_id` è `Restrict`. L'immagine è `MediaUseProjection(id, null)` con sorgente `core`: un uso senza fine.
- **Le preferenze**: `hub_user_preferences`, `GET`/`PUT /api/me/preferences/{key}`, le chiavi da **`IModule.Preferences`**
  (`PreferenceDescriptor`, `OneOf` per un insieme chiuso) composte in `PreferenceCatalog`, che rifiuta all'avvio una chiave fuori dal
  nome del modulo o dichiarata due volte. Nessuna riga = il membro non ha scelto (200 con `value: null`, non 404). Il modulo di prova
  dichiara `sample.order`. ⚠️ **Per T5, che va in parallelo**: `IModule` ha un membro in più, con il default vuoto in `ModuleBase`.
- **Una migrazione del nucleo**, `AddAwardsAndUserPreferences`, solo additiva. Lo snapshot del modulo di prova si allinea alla colonna
  nuova di `cms_award_signals` **senza migrazione** (quella generata era vuota, perché la tabella è esclusa): ogni contesto di modulo
  generato prima di questa fase — `FlightOpsDbContext` di T5 — avrà lo stesso scarto innocuo alla sua prossima migrazione.
- **Schermate**: `/staff/awards` (catalogo, come i link), `/staff/awards/queue` (la coda: assegna, scarta, rimetti in coda),
  `/staff/awards/assignments` (il registro, con la revoca), un gruppo «Award» nella barra dello staff. Nessun componente nuovo.
- ⚠️ **Trovato dal giro e2e, non dai test unitari**: il form dell'award andava in «Something went wrong» perché `SchemaForm` vuole
  `mediaLibrary` e `division` per un campo media, e i test di schema non montano il form. Il nuovo `e2e/full/awards.spec.ts` (scrive,
  assegna a mano, trova nel registro, revoca, elimina) l'ha preso; la coda la provano i test d'integrazione, perché solo una riga di
  modulo la riempie.
- **Scelte piccole, dette qui**: revocare un'assegnazione **non** rimette la riga in coda; uno stesso VID può ricevere due volte lo stesso
  award (nessun indice unico: un tour di un anno e quello dell'anno dopo possono dare lo stesso award); il selettore degli award offre i
  primi cento attivi.
- **I test**: integrazione `AwardsTests` (tre: assegnare dalla coda la gestisce e non due volte, una riga gestita non si scarta e l'award
  non si elimina; scartare e rimettere in coda, il VID sbagliato, l'award ritirato; il catalogo letto da un altro dipartimento e non
  scritto, coda e registro chiusi a chi non assegna, l'uso dell'immagine che nasce e sparisce) e `UserPreferenceTests` (due: un altro
  cookie dello stesso membro sì, un altro membro no; chiave sconosciuta, valore rifiutato, anonimo 401); unit `PreferenceCatalogTests`
  (otto) e la riga del coordinator in `RolePermissionMatrixTests`; Vitest `features/awards/schema.test.ts` (tre); e2e `awards.spec.ts`.
  VID `780041–780049`, nomi `fo-test-award-…`.
- **Verificato in locale** (Docker acceso): integrazione **218** tutte verdi, unit .NET **378**, Vitest **408**, typecheck, lint,
  formato, i18n, build Release, giro e2e completo **20** e smoke **80**. **Non verificato**: le schermate a mano nel browser di sviluppo
  (il login passa da IVAO con le credenziali di Carmine); le ha guidate il giro e2e con il login del banco.

### T5 — Modulo: lo scheletro

Design §0.4, §1.5, §1.11, §7. Branch `m2/t5-flightops-skeleton`.

1. `IvaoHub.Modules.FlightOps` (referenzia solo `Core`), `FlightOpsDbContext : ModuleDbContext`, `__EFMigrationsHistory_flightops`,
   migrazione `Initial` con `fo_aircraft_profiles` e `fo_aircraft_groups`. Registrato in `IvaoHub.Web/Modules.cs`.
2. `web/src/modules/flightops/` con il manifest, `nav.section`, i18n `flightops` in `it` e `en`; `web/src/modules/index.ts`.
3. **I permessi** del design §7.1 nel catalogo del modulo; **`positionGrants`** del design §7.2 in `config/division.json` di IT e in
   `division.example.json`; `modules.flightops.baseDepartment: FOD`.
4. **Le impostazioni** (design §1.11) nella riga `flightops` di `hub_division_settings`, con validazione e un form generato dietro
   `Tours.ManageSettings`; il rifiuto di `dailyLegLimit = null` arriva in T6 (serve la tabella dei tour).
5. **Profili degli aerei** (`Tours.ManageAircraft`) e **gruppi di aerei** (nome `Localized`, tipi ICAO): liste e form generati; il
   selettore dei tipi di T1.
6. La sezione del modulo nella barra dello staff con le prime voci; la galleria se nasce un campo nuovo del form.

**Test**: il seed dei grant di posizione crea le righe del FOD una volta; un advisor FOD modifica un profilo e non le impostazioni; il
fork XX parte con il modulo e `northSouthLevelCountries` vuoto; architettura (il modulo referenzia solo `Core`, niente IVAO fuori dal client).
**Fatta quando**: un coordinator FOD, entrato con il login di sviluppo, vede la sezione Tours, cambia un'impostazione e crea un profilo.

**Fatta il 16 settembre 2026** (branch `m2/t5-flightops-skeleton`, subito dopo il merge di T4b). Com'è andata:

- **Tre cose che il piano dava per esistenti e non c'erano**, nota `decisions/2026-09-16-impostazioni-dei-moduli.md`, piano **0.83**.
  (1) Il seed di `positionGrants` era «una volta per installazione»: i grant del FOD non sarebbero mai arrivati a un DB già avviato.
  **Deciso con Carmine**: ogni grant del file si ricorda con la sua impronta. (2) Non c'era un modo per le impostazioni di un modulo.
  **Deciso con Carmine**: `IModule.Settings` nel nucleo, riga `modules.flightops.settings` (non `flightops` come diceva il design),
  `/api/modules/{key}/settings`. (3) Le rotte di un modulo nella SPA erano solo pubbliche: `RouteDefinition.area: 'staff'`, con
  `permission` e `validateSearch`.
- **Il selettore dei tipi di T1 non esisteva**: `IAircraftTypeDirectory` e `GET /api/reference/aircraft-types?q=` nel nucleo; il campo è
  una proposta chiusa che chiede al server mentre si scrive. I tipi di un gruppo sono nel form una lista di `{ icao }`, perché il
  generatore ripete oggetti. Nessun campo nuovo del form, quindi la galleria non cambia.
- **Il modulo**: `IvaoHub.Modules.FlightOps` referenzia solo `Core`; `FlightOpsDbContext`, migrazione `Initial` con le sole
  `fo_aircraft_profiles` (un profilo per tipo, indice unico) e `fo_aircraft_groups` (tipi in JSON, maiuscoli, ognuno una volta);
  undici permessi `Tours.*`, `Tours.Validate` negato all'interessato; righe `IOwnedByDepartment` con l'insieme dei dipartimenti e il
  FOD come base; letti con `Tours.View`, scritti con `Tours.ManageAircraft`. ⚠️ La guardia dell'interceptor chiede anche
  `Tours.Edit` sul dipartimento (l'area delle righe è `Tours`): chi ha `ManageAircraft` senza `Edit` verrebbe rifiutato. In §7.2 non
  succede — chi ha l'uno ha l'altro — ma un grant a mano solo di `ManageAircraft` non basterebbe.
- **Le impostazioni** sono quelle del design §1.11 con i valori proposti; `northSouthLevelCountries` parte vuoto (in IT lo scrive il
  FOD); `weatherRetentionDays` non c'è ancora, perché segue i tour aperti e lo decide T16. Il rifiuto di `dailyLegLimit = null` con
  tour senza limite resta per T6.
- **`positionGrants` del FOD** (design §7.2) in `config/division.json` e in `division.example.json`, con `scope: FOD`.
- **La SPA**: `web/src/modules/flightops/` con il manifest (cinque rotte di staff), le schermate generate di profili, gruppi e
  impostazioni, i18n `flightops` in `it` e `en`; la sezione «Tour» nella barra dello staff arriva da `StaffNavigation` del modulo.
- **I test**: integrazione `FlightOpsSkeletonTests` (tre: i grant del FOD arrivano una volta e raggiungono la sessione; un advisor FOD
  scrive un profilo e non le impostazioni, il coordinator le cambia e le rilegge, un tipo sconosciuto o doppio e un valore fuori
  limite sono rifiutati sul campo, un coordinator di un altro dipartimento no; un gruppo normalizza i tipi e il campo dei tipi
  risponde), il test del seeder esteso (un grant aggiunto dopo arriva una volta) e il fork XX (il modulo c'è, i paesi nord–sud sono
  vuoti); unit `FlightOpsSkeletonTests` (tre: impronta, valori di partenza, regole); e2e `tours-skeleton.spec.ts` (impostazioni
  salvate e rilette, schermate che si aprono). L'architettura (il modulo referenzia solo `Core`) la prende il test che c'era, che
  ora ha un progetto vero da leggere. VID `780051–780053`, nomi `fo-test-group-…`.
- ⚠️ **Il test del seeder** cancellava la riga `positionGrants.seeded` per simulare un primo avvio: con i grant del FOD nel file, gli
  host avviati dopo li avrebbero applicati una seconda volta. Ora la rimette com'era.
- **Verificato in locale** (Docker acceso): unit .NET **381**, Vitest **410**, typecheck, lint, formato, i18n, build Release, giro e2e
  completo **21** e smoke **80**, integrazione **221**. ⚠️ Un test di M0 (`ModuleRegistryComposesNavAndExclusions`) presupponeva che il
  modulo di prova fosse l'unico del build: ora lo cerca per chiave. **Non verificato**: il «fatta quando» con il login di
  sviluppo vero (passa da IVAO con le credenziali di Carmine): creare un profilo lo provano i test d'integrazione, perché il banco
  e2e non ha i tipi di aereo finché il job notturno non gira.

### T6 — I tour

Design §1.1, §1.2, §1.10, §2 (tipi), §3.7, §8.3, §9, §1.14. Branch `m2/t6-tours`.

1. `fo_tours` com'è nel design §1.2, `IOwnedByDepartment` con la maschera, `IAuditable`, `[Audited]`, `IVisible`.
2. **Lo stato dalle date** in un solo posto (bozza, in arrivo, aperto, in chiusura, chiuso), usato da lista, pagina, form e salvataggio.
3. **Nascondere ed eliminare** (§1.2.2): elimina solo senza PIREP (`Tours.Delete`; la domanda «ha PIREP?» risponde no finché T11 non
   esiste, e il test la prova con una riga scritta a mano), nasconde con `Tours.Edit`; **la nuova chiusura** almeno `2 × report_window_days`
   da oggi.
4. **«Segna pronto»**: i controlli di pubblicazione del design §1.2.1 che non dipendono dalle leg (lingue, date, limite giornaliero,
   profilo dell'aereo di riferimento); quelli sulle leg li aggiunge T7. Il **tipo bloccato** quando il tour è pubblico.
5. **Il rifiuto di `dailyLegLimit = null`** nelle impostazioni con l'elenco dei tour senza limite (§3.7).
6. **Template** (§1.10, `Tours.ManageTemplates`): «Salva come template» e «Nuovo da template» copiano le impostazioni; le regole si
   aggiungono alla copia in T9.
7. **L'editor** `/staff/tours` e `/staff/tours/{id}`: lista (stato calcolato, tipo, date, nascosto) e le schede impostazioni e briefing
   (l'editor dei blocchi); banner e foto dal selettore della media library.
8. **Proiezioni**: ricerca per un tour visibile, **due voci di calendario** (rilascio e chiusura), niente per un tour nascosto; **usi dei
   file** con scadenza `close_at + 1 mese` anche da nascosto e in bozza.

**Test**: unit sullo stato alle soglie delle date; integrazione: un tour con un PIREP scritto a mano non si elimina e si nasconde; la
chiusura sotto `2 × X` rifiutata; il tipo non cambia da pubblico; «pronto» con i problemi elencati campo per campo; il template non copia
le date; la proroga sposta la scadenza del banner; un tour nascosto sparisce da ricerca e calendario.
**Fatta quando**: dal back office si crea un tour da template, lo si segna pronto con una data di rilascio passata e compare in ricerca e
calendario.

**Divisa il 16 settembre 2026** in apertura (Carmine, nota `decisions/2026-09-16-i-tour-nel-back-office.md`, piano **0.84**): **T6a** è
tutto quello sopra tranne la scheda del briefing, branch `m2/t6a-tours`; **T6b** è la scheda del briefing, in una chat nuova. Nella stessa
apertura altre due risposte: un **job del modulo riproietta i tour al rilascio**, e gli **aerei consentiti sono tipi più gruppi**, senza la
spunta «anche le varianti».

**T6a fatta il 16 settembre 2026** (branch `m2/t6a-tours`). Com'è andata:

- **Tre domande a Carmine in apertura**, tutte decise come proposto (sopra). ⚠️ La prima tocca una regola del piano: §16.4 diceva «niente
  job di riconciliazione», e `TourReleaseJob` è la prima eccezione, scritta lì. Non pubblica e non scrive il tour: chiede
  all'interceptor di proiettarlo di nuovo.
- **Nel nucleo, quattro estensioni** (caso b): `ProjectionRefresh` (riproiettare senza scrivere, attraverso l'interceptor: nessuna riga
  d'audit, nessuna `row_version` nuova sotto un editor aperto); l'**orologio** in `ProjectionContext` (la proiezione di un tour dipende
  dall'ora); `CrudOptions.DeletePolicy` (`Tours.Delete` chiesto **in più** di `Tours.Edit`, perché l'advisor modifica e non elimina);
  e le **funzioni SQL** in `ModuleDbContext`. ⚠️ **Trovato dal test d'integrazione, c'era da T5**: la ricerca su un campo tradotto di
  una lista di modulo rispondeva 500, perché `LocalizedQuery` era registrata solo nel contesto del nucleo; i gruppi di aerei avevano
  quella ricerca e nessun test la usava.
- **Il modello**: `fo_tours` con **tutte** le colonne del design §1.2 (migrazione `AddTours`, solo additiva); quelle della forma del tour
  (`parent_tour_id`, `required_*`, `open_goal*`, `allowed_aircraft_json`) nascono ora e le scrive T7, ma il template le copia già.
  **«Pronto» è `PublishStatus.Published`**: `Ready` nel nucleo vuol dire «in approvazione», e la regola della bozza (tiene solo i file)
  è già dell'interceptor. `visibility` è calcolata (pronto, non nascosto, non template) e grossolana: la lettura pubblica di T10 chiede
  anche `TourState.IsPublic`.
- **Lo stato in un posto**: `TourState.Of`, `IsPublic` e l'espressione `NeedsOwnDailyLimit` (il validatore delle impostazioni e il
  filtro `needsOwnDailyLimit` della lista dei tour sono la stessa regola). Le regole che guardano altre righe sono `TourSaving` (indirizzo
  libero, tipo noto, award esistente, tipo bloccato da pubblico, chiusura a due finestre, template che non cambia natura) e quelle di
  «pronto» sono `TourReadiness`; le usano il motore CRUD, i due verbi dei template e l'azione. **Un tour pronto resta pronto solo se
  potrebbe esserlo**: ogni salvataggio ripassa i controlli.
- **Quattro verbi scritti a mano**, contati: `POST /status` (pronto, bozza, nascondi, mostra — una macchina a stati), `GET /ready-problems`,
  `POST /from-template/{id}`, `POST /{id}/save-as-template`. Torna in bozza **solo prima del rilascio**; la chiusura a due finestre vale
  per un tour **pronto**.
- **«Ha PIREP?»** è `ITourReports`, che risponde no finché T11 non lo sostituisce; il test lo prova **sostituendo la risposta**, non con una
  riga scritta a mano, perché `fo_pireps` non esiste ancora.
- **Le schermate**: `/staff/tours` e `/staff/tours/templates` (liste generate, stato calcolato), `/staff/tours/{id}` (form generato,
  problemi di «pronto» prima di premere, barra delle azioni), `/staff/tours/from-template` e `/staff/tours/{id}/save-as-template` (form
  generati). Banner e foto dal selettore della libreria, **senza caricamento** (li carica il PRD, design §1.14). Nelle impostazioni, il
  rifiuto di spegnere il limite mostra sotto il form i tour che lo impediscono. Nessun componente nuovo. ⚠️ Il modulo importa due
  query del nucleo da `features/` (`activeAwardsQuery`, `mediaPickerQuery`) invece di copiarle: la regola ESLint vieta
  `features/ → modules/`, non il contrario.
- **I test**: unit `TourStateTests` (cinque: lo stato a ogni soglia, pubblico da rilascio o anteprima e mai da nascosto, la colonna
  grossolana e le proiezioni che seguono l'orologio, nascosto e template che proiettano solo le foto, la copia che non porta date,
  indirizzo e award e rinomina i blocchi del briefing); integrazione `TourTests` (cinque: da template a pronto trovato in ricerca e
  calendario e sparito da nascosto, con i problemi campo per campo; con report si nasconde e non si elimina, l'advisor non elimina; il tipo
  bloccato e la chiusura a due finestre con la scadenza della foto che si sposta; il limite di divisione che resta acceso; il job che rende
  pubblico un tour rilasciato senza cambiargli `row_version`); Vitest `schemas.test.ts` (tre); e2e `tours.spec.ts` (il «fatta quando»,
  dal template alla ricerca anonima e al calendario, poi elimina tour e template). VID `780061–780062`, slug `fo-test-tour-…`.
- ⚠️ **Trovato dal giro e2e, non dai test unitari** (come in T4b): il generatore dei form leggeva un campo numerico con
  `valueAsNumber`, che trasforma una casella vuota in `NaN`, e nessuno schema accetta `NaN`. Un numero **facoltativo** non si poteva
  lasciare vuoto: il form di un tour non si salvava, e — c'era da prima — nemmeno «spegnere il limite giornaliero» nelle impostazioni di
  T5 né un grant a una posizione senza VID. Corretto in `SchemaForm` (vuoto = nessun numero), con un test Vitest visto rosso prima.
  ⚠️ Il controllo sul calendario dello spec legge il **blocco calendario pubblico**: la lista di staff del calendario è per dipartimento,
  e quello del banco non è il FOD.
- **Verificato in locale** (Docker acceso): unit .NET **386**, Vitest **414**, typecheck, lint, formato, i18n, build Release, giro e2e
  completo **22** e smoke **80**, integrazione **226** tutte verdi. **Non verificato**: il «fatta quando» con il login di sviluppo vero (passa da
  IVAO con le credenziali di Carmine) — l'ha guidato il giro e2e con il login del banco; il job del rilascio lanciato dal suo orario —
  lo lancia il test d'integrazione.

### T6b — Il briefing

Design §1.2 (`briefing_json`), §8.3 (la scheda «briefing»), §1.14 (le immagini del briefing). Branch `m2/t6b-briefing`, **dopo** il merge di
T6a. Nata dalla divisione di T6 (nota `2026-09-16-i-tour-nel-back-office`).

1. **L'editor del corpo estratto** da `features/content/ContentEditor.tsx`: la parte che modifica un `BlockDocument` (tavolozza, albero
   delle sezioni, proprietà del blocco, trascinamento, annulla e ripeti, anteprima con il renderer vero) diventa un componente che riceve
   il corpo e lo restituisce, **senza** metadati, indirizzo, template o revisione. L'editor dei contenuti lo usa com'è oggi: nessun
   cambiamento di comportamento, e i suoi test e il giro e2e dei contenuti lo provano.
2. **La scheda «briefing»** in `/staff/tours/{id}`: lo stesso componente, salvato con il `PUT` del tour (il server accetta già
   `briefing`, valida l'envelope, estrae il testo per la ricerca e dichiara le immagini come usi con la scadenza del tour).
3. **I problemi di «pronto»** con i percorsi dentro il briefing descritti come l'editor dei contenuti li descrive (`publishProblems.tsx`),
   non come «Briefing: …».
4. **Le schede** del tour: impostazioni e briefing ora; T7 e T9 aggiungono le loro.

**Test**: Vitest sul componente estratto (un corpo entra, un corpo modificato esce); e2e: un blocco di testo nel briefing, «pronto» che
chiede la seconda lingua del blocco, il testo trovato in ricerca. **Fatta quando**: un tour ha un briefing con un'immagine della libreria, è
pronto, e l'editor dei contenuti fa quello che faceva.

**T6b fatta il 18 settembre 2026** (branch `m2/t6b-briefing`). Com'è andata:

- **Nessuna decisione nuova, nessun cambiamento del server**: il `PUT` del tour accettava già il briefing (T6a). Niente versione nuova del
  piano; le tre scelte piccole qui sotto sono dette qui e le ha confermate Carmine nella PR.
- **L'editor estratto** è `features/content/BodyEditor.tsx`: riceve `initial` e restituisce ogni cambiamento con `onChange`, annulla e
  ripeti compresi. Tiene per sé tavolozza, pagina o struttura, pannello delle proprietà, trascinamento, storia, scorciatoie, lingua
  dell'anteprima e il contesto del blocco interattivo. Il resto arriva da fuori come **prese** e **risposte**: `toolbar(tools)` (dove vanno
  i suoi tre pulsanti in mezzo a quelli del padrone), `header` (problemi, stato della bozza, revisione), `pageProperties` (quello che il
  pannello mostra quando non è scelto niente: per un contenuto il form dei metadati, sempre montato come prima), `template` (corpo, titolo,
  dipartimento e «può modificarlo» già risolti), `frameUrl`, `published`/`comparing`, `holds`, `locked`. `ContentEditor` è sceso da 1098 a
  437 righe (l'editor estratto ne ha 796, commenti compresi) e fa quello che faceva: la query del template, il salvataggio automatico, il blocco all'uscita, la pubblicazione e la
  revisione restano lì. Lo provano Vitest (tutti i test dei contenuti, invariati), la smoke (80) e il giro completo (i giri dei contenuti).
- **Il percorso dentro un corpo** («Sezione › Testo › markdown») è uscito da `publishProblems.tsx` in `features/content/bodyPath.ts`
  (`useBodyPathDescription`), perché lo leggono in due: i problemi di pubblicazione di un contenuto e quelli di «pronto» del tour, che
  per un percorso `briefing.…` scrivono «Briefing › Sezione › Testo › markdown» invece di «Briefing: …».
- **La scheda «briefing»**: `/staff/tours/{id}?tab=briefing`, con le schede di Atmosphere controllate dall'indirizzo (un link o un
  ricaricamento riaprono quella giusta) e montata solo quella aperta. Un tour nuovo non ha schede: il briefing si scrive dopo il primo
  salvataggio, come un contenuto non si salva da solo prima del primo «salva bozza». Il salvataggio è `useSaveTour` con
  `{ values, briefing }`: le impostazioni del tour come le ha la cache (la `row_version` più recente) più il corpo. Le immagini vengono dalla
  libreria del dipartimento del tour, **senza caricamento**, come banner e foto (design §1.14).
- **Tre scelte piccole, confermate da Carmine il 18 settembre 2026** (prima del merge, e fatte nella stessa PR): (1) il briefing si
  salva **con un pulsante**, non da solo. Il `PUT` porta tutto il tour, e un salvataggio automatico sotto le dita di qualcuno lo sarebbe
  anche delle impostazioni; con modifiche non salvate, cambiare scheda o pagina chiede conferma con le parole dell'editor dei contenuti.
  (2) **Il blocco interattivo non si offre nel briefing** (`holds` risponde no): il suo frame lo costruisce `/embed/{contenuto}/…`, solo
  per una riga di `cms_contents`; se un giorno servisse, è un meccanismo nuovo con la sua nota. (3) **Un blocco Data nel briefing è
  sempre vivo**: la cattura di `frozen` la fa il servizio di pubblicazione dei contenuti, che un tour non attraversa. L'editor **non
  offre** l'interruttore vivo/congelato dove niente cattura: `BodyEditor` ha la proprietà `captures` (vera per difetto, falsa nel
  briefing), che `BlockProperties` legge accanto a `alwaysLive`. Caso b, niente versione nuova del piano.
- **I test**: Vitest `BodyEditor.test.tsx` (quattro: un corpo entra ed esce con un blocco aggiunto senza toccare quello dato; annulla e
  ripeti che lo restituiscono; la tavolozza che offre il blocco interattivo solo a chi ha il permesso, e la scelta vivo/congelato offerta solo con `captures`, provate nei due sensi); e2e
  `full/tours-briefing.spec.ts` (il «fatta quando»: un tour, un testo solo in inglese e un'immagine caricata nella libreria del suo
  dipartimento, salvati; «pronto» che chiede l'italiano e dice «Briefing › … › Text»; l'italiano scritto, il briefing salvato con
  l'immagine, il tour pronto e il testo del briefing trovato in ricerca anonima; poi tour e immagine tolti). Il banco ha due aiuti nuovi,
  `uploadMedia` e `deleteMedia`.
- **Verificato in locale** (Docker acceso): Vitest **420** (418, più i due di `captures` aggiunti dopo la conferma), typecheck, lint, formato, i18n, smoke **80**, giro e2e completo **23**. Guardata la
  scheda a 1500 px: tre colonne come l'editor dei contenuti, e il cambio di scheda con il briefing non salvato fermato. **Non eseguiti**:
  unit .NET e integrazione, perché il server non è cambiato (li esegue la CI).

### T7 — Le leg e la forma del tour

Design §1.3, §1.4, §1.5 (tempo stimato), §1.6, §2, §2.6.1 (la definizione, non la verifica), §2.7, §8.4. Branch `m2/t7-legs`.

1. `fo_legs` con le coordinate **congelate** da `ref_` e la GCD calcolata dal server (`GreatCircle` di Toursystem, con i suoi test);
   `IRunwayDirectory.EnsureAsync` sugli aeroporti delle leg.
2. **Il tempo stimato** calcolato a ogni lettura (formula del design §1.5), per leg e totale, solo se c'è l'aereo di riferimento.
3. **Togliere una leg** (§1.4.1): elimina e rinumera senza PIREP, ritira con motivo con PIREP, ripristina finché il tour non è chiuso;
   una rotazione si ritira intera. `change_reason` obbligatorio quando si modifica una leg con PIREP.
4. **Hub e rotazioni** (`fo_hubs`, `fo_rotations`, leg `HubConnection`), **sottotour** (`parent_tour_id`, `required_subtours`, eredità di
   aerei, callsign e regole), **vincoli sul callsign** (`fo_callsign_rules`), **aerei consentiti** con varianti e gruppi.
5. **Tour `Distance` e `Open`**: `required_nm`; `open_goal` con i parametri e `fo_tour_constraints` (filtri e regole di sequenza del
   design §2.6.1), ciascuno con il suo schema di parametri e la sua chiave i18n. La **verifica** su un volo è di T11.
6. **I controlli di «pronto» che dipendono dalla forma**: almeno una leg, aeroporti noti, date delle leg nel periodo, rotazioni di `size`
   leg che partono e tornano all'hub, anello per `SequentialChosenStart`, somma delle GCD per `Distance`, `Container` con almeno due sottotour.
7. **L'editor delle leg a tabella** — **l'eccezione dichiarata** al motore lista e form (design §8.4, piano 0.79): un componente
   **`LegGrid`** nell'elenco chiuso, righe con `row_version`, errori sulla cella dai `ProblemDetails`, azioni «duplica», «segue», «chiudi
   tour», «elimina o ritira» (il server dice quale prima di confermare), «ripristina». Le schede hub e rotazioni, sottotour, callsign e
   vincoli sono liste e form generati.

**Test**: unit sulla GCD, sul tempo stimato con i numeri del design, sui controlli di forma per ogni tipo, sul consentito del callsign
(unione e `Deny` che vince); integrazione: rinumerare senza toccare un PIREP scritto a mano; ritirare e ripristinare; ritirare una
rotazione intera; una leg modificata con PIREP senza motivo rifiutata. Smoke: l'editor delle leg aggiunge, duplica e ritira.
**Fatta quando**: si compone da zero un tour `Hub` con due hub, rotazioni e un collegamento, e lo si segna pronto.

**Divisa il 18 settembre 2026** in apertura (Carmine, nota `decisions/2026-09-18-le-leg-dei-tour.md`, piano **0.85**): **T7a** sono i punti
1, 2, 3 e 7, gli aerei consentiti del punto 4, `required_nm` del punto 5 e i controlli del punto 6 per `Sequential`, `Free`,
`SequentialChosenStart` e `Distance`; branch `m2/t7a-legs`. **T7b** è il resto — hub e rotazioni, sottotour e `Container`, vincoli sul
callsign (`fo_callsign_rules`, con la copia del template), il tour `Open` (`open_goal`, `fo_tour_constraints`) e i loro controlli di
«pronto», più le schede generate — in una chat nuova, branch `m2/t7b-shape`; chiude il «fatta quando» di T7. **In apertura di T7b** si
porta a Carmine la domanda sui sottotour: date e indirizzo propri (dentro il periodo del padre) o del padre. Nella stessa apertura di
T7a: **le righe figlie di un tour copiano la sua maschera dei dipartimenti** a ogni scrittura, e la seguono quando cambia.

**T7a fatta il 18 settembre 2026** (branch `m2/t7a-legs`). Com'è andata:

- **Due domande a Carmine in apertura**, decise come proposto (sopra).
- **Il modello**: `fo_legs` com'è nel design §1.4 (migrazione `AddLegs`, solo additiva, con la chiave verso `fo_tours` in cascata: un
  tour eliminato si porta via le leg); `kind`, `rotation_id` e `seq_in_rotation` nascono ora e li scrive T7b. Le coordinate sono
  **congelate** alla scrittura da `IAirportDirectory` (nel nucleo, come `IAircraftTypeDirectory`) e la distanza è `GreatCircle` di
  Toursystem, arrotondata al decimo come la colonna. ⚠️ **L'indice `(tour_id, number)` non è unico**: MariaDB controlla un indice unico
  riga per riga e spostare i numeri collide a metà; il numero lo tiene unico il server, che rinumera tutto il tour.
- **Sei verbi scritti a mano** (l'eccezione di §16.6 vale anche lato server, contati nella nota): la griglia, aggiungi (`?after=`),
  modifica, «che cosa farebbe togliere», togli, ripristina. Ogni scrittura risponde con la griglia intera. La risorsa è il tour:
  `Tours.View` e `Tours.Edit` sul tour, dall'unico handler. Una scrittura di una leg di un tour **pronto** passa i controlli di «pronto»
  con le leg come le lascia (`TourReadiness` prende le leg in ingresso). Le piste degli aeroporti si chiedono dopo ogni scrittura, e un
  errore di rete non rifiuta mai una leg.
- **I report** non esistono ancora: `ITourReports.LegsWithReportsAsync` risponde «nessuna» finché T11 non la sostituisce, e i test
  la provano sostituendo la risposta. La regola della **rotazione ritirata intera** è già nel server e il test la prova con
  `rotation_id` scritto a mano.
- **Il tempo stimato** a ogni lettura, per leg e in totale, solo con l'aereo di riferimento e il suo profilo. ⚠️ **Trovato scrivendo il
  test con i numeri del design**: a 2000 NM la tabella del design §1.5 diceva 4 h 40 per «5 % + 20 min», che è la sola parte
  proporzionale; la formula dà 5 h 00. Corretto nel design.
- **`TourShape`**: i controlli di forma in una funzione pura (almeno una leg non ritirata per i tipi con leg, nessuna per `Open` e
  `Container`, aeroporti noti, rilascio della leg dentro il periodo, anello per `SequentialChosenStart`, GCD sufficienti per `Distance`),
  usata da «pronto», dai problemi prima di premere e da ogni scrittura di un tour pronto. Un problema di una leg è `legs.{numero}`, e la
  schermata lo dice «Leg 3».
- **Gli aerei consentiti** (tipi e gruppi) nel form del tour, con `AllowedAircraftCheck` condiviso da tour e leg. Nel form i tipi portano
  il nome del campo del payload (`allowedAircraft`), così un rifiuto del server cade su un campo; i gruppi stanno accanto
  (`allowedGroups`). `required_nm` nel form, letto solo da un tour `Distance`.
- **`LegGrid`** (`screens/LegGrid.tsx`): ogni riga si modifica dov'è e si salva da sola con la sua versione, il rifiuto sotto la cella;
  «aggiungi dopo: duplica, segue, chiudi il tour» compilano una riga nuova che il server numera al salvataggio; «togli» chiede prima al
  server che cosa succederà (`ConfirmDialog` esteso con `onOpenChange` e `confirmDisabled`) e un ritiro vuole il motivo; una leg ritirata
  resta nella tabella con il motivo e si ripristina; una leg con report chiede perché cambia. La cella di un aeroporto propone gli
  aeroporti con un `datalist` del browser. La riga ha un nome accessibile («Leg 3»). ⚠️ **Guardata a 1500 px**: la prima stesura
  scorreva in orizzontale e nascondeva numero e partenza; ora le celle hanno larghezze fisse, lo stato sta sotto il numero e la colonna
  del tempo stimato c'è solo quando c'è una stima. **Non fatto**: i gruppi di una leg (il server li accetta, la griglia li conserva ma
  non li offre); `LegGrid` nella galleria dei componenti, che non importa da `modules/` — lo dirà T20.
- **I test**: unit `LegTests` (otto, dieci casi: la GCD con i test di Toursystem, il tempo stimato con i numeri del design corretti, i
  controlli di forma di ogni tipo con leg, la rinumerazione); integrazione `LegTests` (due: misurate, numerate, «duplica dopo»,
  eliminata e rinumerata senza toccare la leg con report, modificata solo con un motivo, ritirata e ripristinata, una rotazione
  ritirata intera; un tour a partenza libera che chiede l'anello, le stime di un aereo a 450 kt, un tour pronto che rifiuta di rompersi,
  il rilascio fuori periodo sotto `legs.3`, il 409 di una versione vecchia, un tour `Open` senza leg) e gli aeroporti di prova in
  `FoTestAirports` (codici `XFA1–3`, nessuna regione ICAO); Vitest `LegGrid.test.tsx` (quattro: segue, duplica, ritira con motivo dopo la
  risposta del server, motivo di modifica e rifiuto sotto la cella) e `schemas.test.ts` (uno in più); e2e `full/tours-legs.spec.ts` (il
  «fatta quando» di T7a: un tour a partenza libera composto da zero nella tabella — aggiungi, segue, chiudi il tour, duplica e togli il
  duplicato — rifiutato finché non è un anello e poi pronto). VID `780071–780072`, slug `fo-test-legs-…`. ⚠️ **I test di T6a e T6b segnavano
  pronto tour senza leg**: ora aggiungono una leg (integrazione con le fixture di IVAO, e2e con l'aiuto `addLeg` del banco).
- ⚠️ **Trovato dal giro e2e, non dai test**: il database del banco era dell'11 settembre, prima di T1, e aveva gli aeroporti **senza
  coordinate** (la sincronizzazione all'avvio gira solo su un database senza centri). Rifatto il database del banco (memoria
  `bench-database-accumulates`); in CI il banco è sempre nuovo. Un'installazione già avviata prima di T1 prende le coordinate alla prima
  sincronizzazione notturna.
- **Verificato in locale** (Docker acceso): unit .NET **396**, integrazione **228**, Vitest **425**, typecheck, lint, formato, i18n, smoke
  **80**, giro e2e completo **24**. Guardata la tabella a 1500 px sul banco. **Non verificato**: il «fatta quando» con il login di
  sviluppo vero (l'ha guidato il giro e2e con il login del banco); il recupero delle piste da IVAO vera (nei test e sul banco rispondono
  le fixture, che non hanno piste per quegli aeroporti).

**Divisa ancora il 21 settembre 2026** in apertura di T7b (Carmine, nota `decisions/2026-09-21-la-forma-dei-tour.md`, piano **0.86**):
**T7b** sono hub e rotazioni, sottotour e `Container`, vincoli sul callsign e i loro controlli di «pronto», e chiude il «fatta quando» di
T7; **T7c** è il tour `Open` (sotto).

**T7b fatta il 21 settembre 2026** (branch `m2/t7b-shape`). Com'è andata:

- **Cinque domande a Carmine in apertura** (nota §2): la fase in due; un sottotour ha **date proprie, e quelle che non ha sono del
  padre**, ognuna da sola; **uno slug proprio**; **ricerca e calendario li porta solo il `Container`**; un vincolo sul callsign è **sulla
  compagnia** (tre lettere, il resto lo sceglie il pilota: il callsign reale è un suggerimento) o, solo per vietare, su un callsign
  intero; fra i livelli **vince l'`Allow` più vicino e i `Deny` si sommano**. Corretti nel design §1.2, §1.3, §1.6, §2.7.
- **Il modello** (migrazione `AddTourShape`, solo additiva): `fo_hubs`, `fo_rotations`, `fo_callsign_rules`, due colonne del tour
  (`release_from_parent`, `close_from_parent`) e tre chiavi dentro il contesto — la leg verso la rotazione (`SET NULL`), la rotazione
  verso l'hub e il tour (cascata), il sottotour verso il padre (`RESTRICT`: un `Container` si elimina dopo i suoi sottotour, e il
  server lo dice prima). ⚠️ La chiave della leg verso la rotazione ha rotto il test di T7a che scriveva un `rotation_id` inventato:
  ora crea una rotazione vera. Le righe figlie implementano **`ITourChild`**, e `TourSaving` le fa seguire tutte — leg, hub, rotazioni,
  vincoli, e i sottotour con le loro — quando la cura del tour cambia.
- **`CrudOptions.BeforeAuthorize`** (nucleo, caso b): il motore chiedeva il permesso di una riga nuova sulla sola base del modulo, e
  `BeforeSave` arriva dopo. Il gancio gira dopo `Apply` e prima del controllo, in creazione, modifica ed eliminazione; lo usano hub,
  rotazioni, vincoli (la cura del tour) e il tour stesso (un sottotour prende cura e date del padre). Il test d'integrazione lo prova con
  un membro dell'ED a cui è data la cura di un tour: crea un hub, e non più quando la cura torna al solo FOD.
- **Hub, rotazioni, vincoli**: tre risorse del motore (`/api/flightops/hubs`, `/rotations`, `/callsign-rules`) filtrate per
  `tourId`, con liste e form generati nelle schede «Hub e rotazioni» e «Callsign» e un form per pagina
  (`/staff/tours/{id}/hubs|rotations|callsigns/{…}`). Un vincolo su un template chiede `Tours.ManageTemplates` (`ExtraWritePolicy` su un
  campo non mappato, `OnTemplate`, scritto da `BeforeAuthorize`). Ogni scrittura di un hub o di una rotazione di un tour **pronto** passa
  i controlli di «pronto» con le righe come la scrittura le lascia (`TourChildren.StillReadyAsync`, `TourParts` nel controllo).
- **`TourShape`** prende tutte le parti (`TourParts`: leg, hub, rotazioni, sottotour, padre), sempre funzione pura. Un problema di un hub
  è `hubs.{ICAO}`, di una rotazione `rotations.{ICAO}.{posizione}`, e la schermata li dice «Hub LIRF», «Rotazione 2 di LIRF».
- **La leg** dice la sua rotazione o il collegamento (`kind`, `rotationId` in `LegWriteDto`, in coda con un default così i test di T7a
  non cambiano); `seq_in_rotation` lo scrive il server (`LegBook.SequenceRotations`). In `LegGrid` una colonna sola, «Rotazione», solo sui
  tour `Hub`: «nessuna», «collegamento», o «LIRF 1 (2/2)». ⚠️ **Guardata a 1500 px**: la prima stesura spingeva fuori dallo schermo la
  colonna delle azioni, senza che il contenitore a scorrimento lo segnalasse; etichette corte e 16 px presi dalla cella dei tipi, solo
  sui tour `Hub`.
- **I sottotour**: la scheda del `Container` elenca i suoi (`filter[parent]`, e la lista di `/staff/tours` ha `parent=none` di default);
  «Nuovo sottotour» apre l'editor del tour con `?parent=`, che non offre `Container`, non offre l'award e lascia vuote le date del padre.
  ⚠️ **Trovato dal test d'integrazione**: «un `Container` con sottotour non cambia tipo» leggeva `Tours` con il filtro di visibilità, che
  nasconde i sottotour in bozza; ora `CrudSource.BackOffice`, anche nell'eliminazione.
- **I test**: unit `TourShapeTests` (nove, venti casi: hub, rotazioni, ritirate, collegamenti, tipi senza hub, `Container`, sottotour, il
  posto nella rotazione, dodici casi di `CallsignRules.Judge`); integrazione `TourShapeTests` (cinque: il tour `Hub` composto e pronto, hub
  e collegamenti solo sui tour `Hub`, la cura presa prima del permesso e seguita, il `Container` con le date che i sottotour prendono e
  seguono, i vincoli e il template che li copia); Vitest `schemas.test.ts` (i tre form nuovi rispecchiano i loro payload); e2e
  `full/tours-hub.spec.ts` (**il «fatta quando» di T7**: due hub, una rotazione ciascuno nei form generati, le leg e il collegamento
  nella tabella, rifiutato finché una rotazione è corta, poi pronto). VID `780073–780075`, slug `fo-test-shape-…`.
- **Verificato in locale** (Docker acceso): unit .NET **416**, integrazione **233**, Vitest **426**, typecheck, lint, formato, i18n, smoke
  **80**, giro e2e completo **25**. Guardate a 1500 px le schede «Leg», «Hub e rotazioni», «Callsign» e il form di una rotazione.
  **Non verificato**: il «fatta quando» con il login di sviluppo vero (l'ha guidato il giro e2e); la scheda «Sottotour» a occhio (la
  provano l'integrazione e il typecheck, non uno spec e2e).

### T7c — Il tour `Open`

Design §2.6, §2.6.1, §14; nota `decisions/2026-09-21-la-forma-dei-tour.md` §2.1. Branch `m2/t7c-open`, dopo T7b; **non** in parallelo con
T8 (tutte e due migrano `FlightOpsDbContext`).

1. **L'obiettivo** (`fo_tours.open_goal`, `open_goal_json`): i sei obiettivi di §2.6.1, ciascuno con il suo schema di parametri, la sua
   verifica dei parametri sul server e la sua chiave i18n. Si scrive dal form del tour (o da una scheda sua, da decidere in apertura).
2. **`fo_tour_constraints`**: i filtri per volo e le regole di sequenza di §2.6.1 (`NoRepeatedRoute` è sempre accesa e non è una riga),
   una riga figlia del tour come hub e vincoli (`ITourChild`, `BeforeAuthorize`), con lista e form generati; il tipo si sceglie prima e
   poi il form dei suoi parametri. Li copia un template.
3. **I controlli di «pronto» di `Open`**: un obiettivo con i suoi parametri; filtri che non si contraddicono dove si vede (`Eastbound` con
   `Westbound`); nessuna leg (già in `TourShape`).
4. La **verifica** su un volo resta di T11.

**Test**: unit sui parametri di ogni obiettivo e filtro; integrazione: un tour `Open` composto e pronto, un parametro sbagliato rifiutato
sul campo. **Fatta quando**: si compone da zero un tour `Open` con un obiettivo, due filtri e una regola di sequenza, e lo si segna
pronto. **In apertura** si porta a Carmine: dove si scrive l'obiettivo (form del tour o scheda) e se un filtro vale anche su un tour con
leg.

**T7c fatta il 22 settembre 2026** (branch `m2/t7c-open`, nota `decisions/2026-09-22-il-tour-open.md`, piano **0.87**). Com'è andata:

- **Cinque domande a Carmine in apertura**, le due di qui e tre nate leggendo il codice: l'obiettivo ha **una scheda sua**
  («Obiettivo e vincoli», solo sui tour `Open`) e si salva con il tour; filtri e regole **solo sui tour `Open`**; **una riga per tipo**,
  tranne `MinFlightsAt` (una per aeroporto), e `TouchesAirport` prende un elenco; un `Open` con vincoli **non cambia tipo** (l'obiettivo
  si svuota fuori da `Open`); gli elenchi **si scrivono nel tour**, e un template li porta. Corretto il design §2.6.1 e §8.3.
- **`AircraftTypes` cade** (caso b): tipi e gruppi ammessi sono già gli aerei consentiti del tour (T7a), anche su un `Open`. Resta
  `AircraftCategory` (la categoria di scia, `L`/`M`/`H`/`J`).
- **Il modello** (migrazione `AddTourConstraints`, solo additiva): `fo_tour_constraints` (`kind`, `parameters_json`, la cura del tour,
  cascata col tour), riga figlia come hub e callsign (`ITourChild`, `BeforeAuthorize`, `TourSaving` la fa seguire, la copia la porta,
  `Tours.ManageTemplates` sul template). L'obiettivo sta nelle colonne che T6a aveva già (`open_goal`, `open_goal_json`), scritte da
  `TourWriteDto.OpenGoal`/`OpenGoalParameters`: assenti, restano come sono (come il briefing); lette e verificate dal salvataggio solo
  quando cambiano.
- **Un catalogo solo** dei parametri, in C# (`Shape/OpenCatalog.cs`): per ogni obiettivo e vincolo i campi, il tipo, obbligatorio e
  limiti; `Read` normalizza (maiuscole, ognuno una volta, solo i campi del tipo) e rifiuta sul campo; le regole fra due campi
  (`CollectRegions` paesi **o** FIR, «quanti» non oltre l'elenco, `DistanceBetween` con almeno un estremo e il minimo non sopra il
  massimo) stanno lì. Se aeroporti, paesi e FIR esistono lo chiede **`OpenParameterCheck`** al nucleo, con due domande nuove
  (caso b): **`IAirportDirectory.KnownCountriesAsync`** (un paese è il `countryId` di un aeroporto noto) e **`IFirLocator.KnownAsync`**
  (i confini di VATSpy del mondo). Gli errori stanno sotto `openGoalParameters.{campo}` e `parameters.{campo}`, i nomi dei form.
- **«Pronto» di un `Open`** (`TourShape.OpenProblems`): un obiettivo, i suoi parametri ancora buoni (controllo puro, senza i
  dizionari), niente `Eastbound` con `Westbound`; su un altro tipo un vincolo è un problema (non può capitare: il server rifiuta).
- **Il frontend**: il pezzo «tipo, poi i suoi parametri» sono **due form generati** — il tipo è un `SchemaForm` di un campo che si
  applica mentre lo si sceglie (`onChange`, l'estensione di G15), sotto il form dei parametri di quel tipo (`KindPicker` in
  `screens/shape.tsx`, usato dall'obiettivo e dal vincolo). Nessuna select scritta a mano, nessuna estensione del generatore: gli
  oggetti annidati e le liste c'erano già, e le etichette annidate si scrivono piatte (`"parameters.countries": …`, come `seo.title`).
  Gli schemi dei parametri sono una tabella (`GOAL_PARAMETERS`, `CONSTRAINT_PARAMETERS` in `schemas.ts`); ogni tipo ha una frase che
  dice che cosa chiede al pilota (`open.goals`, `constraints.explain`). La lista dei vincoli mostra i valori con le loro unità
  (`≥ 200 NM`, `LIRF`, `× 3`), composti dal server senza parole.
- **I test**: unit `OpenTourTests` (i parametri di ogni obiettivo e vincolo tenuti e rifiutati, la lista, «pronto» di `Open`; un
  test di T7a ora non si aspetta un `Open` senza problemi); integrazione `OpenTourTests` (due: il «fatta quando» via API con i rifiuti
  sul campo, una riga per tipo e per aeroporto, `Eastbound`+`Westbound` rifiutati su un tour pronto; vincoli solo su `Open`, il cambio
  di tipo rifiutato, il template che porta obiettivo e vincoli, l'obiettivo che si svuota); Vitest `schemas.test.ts` (ogni form si
  disegna, il form del vincolo rispecchia il payload, i parametri andata e ritorno); e2e `full/tours-open.spec.ts` (**il «fatta
  quando»**: un `CollectList` scritto nella scheda, due filtri e `Chained` nei form generati, un `DistanceBetween` rovesciato
  rifiutato sul campo, pronto). VID `780076–780077`, slug `fo-test-open-…`.
- **Trovato dal test della divisione XX, non dagli altri**: il file inglese diceva "for example LIRR" in un messaggio d'errore; un
  fork l'avrebbe ereditato. Gli esempi dell'inglese sono ora di nessuno (EDGG, FR/DE).
- **Verificato in locale** (Docker acceso): unit .NET **468**, integrazione **235**, Vitest **429**, typecheck, lint,
  formato, i18n, smoke **80**, giro e2e completo **26**. Guardate a 1500 px la scheda «Obiettivo e vincoli», il form di un vincolo nuovo
  e quello di una categoria di scia. ⚠️ **A 400 px ogni pagina dello staff è schiacciata**, perché la barra laterale non si chiude:
  succede anche su «Gruppi di aerei», quindi non è di T7c; segnalato a parte. **Non verificato**: il «fatta quando» con il login di
  sviluppo vero (l'ha guidato il giro e2e); un FIR nei parametri contro i confini veri (sul banco e nei test i confini di VATSpy non
  ci sono: il controllo del formato è provato, quello dell'esistenza no).

### T8 — L'import delle leg

Design §8.4, ADR-051 di Toursystem. Branch `m2/t8-leg-import`, dopo T7a (usa `LegBook` e la griglia).

1. Il file XLSX o CSV si legge **nel browser** (una libreria da scegliere in apertura: licenza compatibile con Apache-2.0 e nessun
   codice valutato a runtime, per la CSP; scritta nella PR).
2. Il server riceve le righe e risponde con **le differenze** senza scrivere (aggiunte, cambiate, assenti con e senza PIREP).
3. «Fondi» non tocca le assenti; «sostituisci» elimina le assenti senza PIREP e ritira quelle con PIREP, con un motivo. Una transazione.
4. Il **modello di file** con le colonne, scaricabile dall'editor.

**Test**: unit sul confronto (stessa leg riconosciuta da partenza, arrivo e numero); integrazione: sostituire ritira una leg con PIREP e
ne elimina una senza; un file con un aeroporto sconosciuto rifiutato sulla riga. Smoke: un import di un CSV con l'anteprima.
**Fatta quando**: le leg di un tour vero del 2026 (file del FOD) entrano con l'anteprima giusta.

**T8 fatta il 22 settembre 2026** (branch `m2/t8-leg-import`, nota `decisions/2026-09-22-l-import-delle-leg.md`, piano **0.88**).
Com'è andata:

- **Quattro domande a Carmine in apertura**, tutte con la risposta raccomandata: **SheetJS 0.20.3** (Apache-2.0) e non read-excel-file
  (crea worker da `blob:`, che la CSP blocca) né un lettore nostro; **la stessa leg è la stessa coppia** partenza→arrivo, abbinata
  nell'ordine se ripetuta, e l'ordine delle righe è l'ordine del tour (il «numero» di qui sopra non regge: lo tiene il server e cambia a
  ogni inserimento); **niente import sui tour `Hub`**; **una ritirata che il file nomina torna nel tour**. Corretto il design §1.4.1 e
  §8.4.
- **Il secondo giro, sul file vero** (la cartella dei tour 2027 che Carmine ha mostrato la sera stessa, letta con il lettore vero in
  ognuno dei 14 fogli): l'intestazione è **nella seconda riga** in 11 fogli (sopra i totali), e accanto a «Departure ICAO» c'è
  «Departure» con la città — ora si cerca l'intestazione nelle prime righe e **vince la colonna ICAO**; «Destination ICAO» vale come
  arrivo. **Più callsign in una cella** (`ITY1357/1365/1359/1363`, fino a quindici su Linate–Fiumicino, anche con `&`): Carmine li
  vuole tutti, suggeriti — la leg ha **due liste** (`callsigns_json`, `flight_numbers_json`, migrazione `AddLegSuggestions`, additiva
  con il travaso dei valori di prima), al massimo 24. **Le righe che non sono leg si rifiutano** (Carmine): sui 14 fogli, 7 entrano
  come sono (AEZ, Bizjet, IFR, ITY, RYR BRI, Skills, Turboprop), gli altri hanno sotto le leg note, totali, la tabella di riferimento
  del Long-haul, la descrizione del Vintage, le attività di Heli — da togliere. Attenzione alle righe **valide ma di bozza**, che
  entrano come leg e si vedono solo nell'anteprima: le due in fondo al Mistral, le alternative per Buenos Aires del Long-haul. **La
  cartella ha 17 fogli**: si apre sul primo con le leg e si sceglie.
- **Il «fatta quando», sul banco**: il foglio ITY esportato com'è (32 leg, la riga dei totali sopra l'intestazione) è entrato dalle
  schermate vere contro il server vero, anteprima «32 aggiunte» con la riga del file di ognuna, e la leg 21 con i suoi quindici
  callsign. ⚠️ Con **coordinate approssimate** messe a mano nel banco per 15 aeroporti (il database di sviluppo ha lo snapshot vecchio,
  senza coordinate), tolte subito dopo: le distanze di quella prova non contano, l'anteprima sì. Guardato a 1500 px: la cella dei
  callsign era stretta, ora è più larga e mostra la lista intera al passaggio del mouse.
- ⚠️ **SheetJS non sta su npm**: la 0.18.5 del registro ha due CVE in lettura, la versione corretta si installa dal tarball del CDN di
  SheetJS, fissato con l'hash nel lockfile. Dependabot non la vede: si aggiorna a mano. Chunk suo, caricato solo all'import (500 KB,
  163 KB compressi); nessun `eval`, nessun worker in lettura.
- **Il backend**: `Legs/LegImport.cs` è il confronto, puro (`LegImportPlan.Make`, `Apply`, `Fingerprint`); due verbi nell'eccezione
  dell'editor delle leg, `…/legs/import/preview` e `…/legs/import`. Ogni riga passa per `LegWriteDtoValidator` e `LegBook.ApplyAsync`
  (le regole di una leg scritta a mano), i rifiuti stanno sotto `rows[n].campo`. L'impronta delle leg (id e versione) torna con
  l'import: leg cambiate nel frattempo, 409. Il motivo solo quando si ritira, si ripristina o si cambia una leg con PIREP. Nessuna
  migrazione.
- **Il frontend**: `screens/legFile.ts` (il file in righe, colonne per nome, date di Excel e ISO in UTC, il modello scaricabile) e
  `screens/LegImport.tsx`, un pannello dentro `LegGrid` (parte dell'eccezione, nessun componente nuovo); l'import è un caso in più di
  `useLegChange`, quindi la risposta sostituisce la griglia come ogni scrittura.
- **I test**: unit `LegImportTests` (dieci: stesso file due volte, riga inserita, coppia ripetuta, «fondi» e «sostituisci», ritirate,
  gruppi che restano, impronta); Vitest `legFile.test.ts` (cinque: colonne, date, CSV col `;` e un XLSX vero, file illeggibile);
  integrazione `LegTests.AnImportIsPreviewedWithoutWritingAndAppliedByTheRulesOfReports`; e2e `full/tours-import.spec.ts` (due CSV:
  un aeroporto sconosciuto detto sulla riga, due leg aggiunte, poi «sostituisci» che ne elimina una).
- **Trovato dal test d'integrazione**: il primo giro aspettava «uguale» su una leg che il file dava con un tipo d'aereo in più; il
  confronto aveva ragione (una colonna vuota è «nessun tipo»), il test no.
- **Verificato in locale** (Docker acceso, dopo il secondo giro): unit .NET **478**, integrazione **236**, Vitest **437**, typecheck, lint, formato, i18n,
  smoke **80**, giro e2e completo **27**. ⚠️ Il primo giro completo ha avuto **un fallimento instabile** in uno spec di T6 che cerca un
  tour appena creato nella ricerca (`expect(hits…).toContain('/tours/…')`); da soli quei due spec passano sempre, e il giro rifatto è
  verde: non è di T8, ma se torna va guardato. **Non verificato**: un'anteprima del server sui tour con aeroporti
  fuori dal database locale (Long-haul, Bizjet, RYR…): il lettore li legge giusti, ma l'esistenza degli aeroporti del mondo si prova
  solo dove c'è lo snapshot con le coordinate (staging).

### T9 — Regole ed errori

Design §1.7, §5, §6.2 (lo schema dei parametri). Branch `m2/t9-rules-and-errors`.

1. `fo_rules` (generali e del tour, `amends_rule_id`, `check_key`, `parameters_json`), `fo_errors` (categoria, `yearly_max`,
   `is_public`), `fo_rule_errors`; `Tours.ManageRules`.
2. **Il form della regola disegna i parametri** dallo schema del controllo collegato. ⚠️ Estensione n.9 del design: verificare in
   apertura se il generatore di form sa disegnare uno schema scelto a runtime e una selezione multipla; se no, **si estende il generatore**
   (caso b), non si scrive un form a mano. I controlli di T17 non esistono ancora: il catalogo delle chiavi e dei loro schemi nasce qui,
   vuoto di logica.
3. **Le regole effettive** (§5.2): un servizio solo, con i sottotour.
4. **«Copia le regole da un altro tour»**, e i template che copiano le regole (completa T6).
5. Le colonne «senza errori» e «senza regole» (aggregati nella lista, estensione n.9).
6. Il blocco Data **`flightops.errorCatalog`** (sempre `live`), registrato in due metà (TypeScript e `CoreBlocks`-equivalente del modulo).

**Test**: unit sulle regole effettive (emenda con i parametri, ritirata esclusa, padre prima del sottotour); integrazione: la copia porta
parametri e collegamenti; il blocco mostra solo gli errori pubblici; `ArchitectureTests` sulle due metà del blocco.
**Fatta quando**: il FOD scrive una regola generale con i parametri delle disconnessioni, un tour la emenda, e la pagina di prova col blocco
mostra il catalogo pubblico.

### T10 — Il pubblico e la mappa

Design §8.1, §8.2, §8.6; nota `2026-09-15-la-mappa`. Branch `m2/t10-public-tours`.

1. **`/tours`**: riquadri dei tour aperti, in chiusura, e in arrivo con anteprima; anonimo senza avanzamento.
2. **`/tours/{slug}`**: briefing, date, aerei, regole effettive con i parametri, leg con distanza, tempo stimato e totale, callsign
   reale, pulsante SimBrief; per `Distance` e `Open` i vincoli. Le leg ritirate non compaiono.
3. **`RouteMap`**: MapLibre 6, `pmtiles`, `/tiles` nel backend e in `web/backendPaths.ts`, `blob:` in `img-src`, attribuzione, fondo neutro
   senza file, avviso senza WebGL2. Nell'elenco chiuso e nella galleria. Colori per stato già previsti (gli stati del pilota arrivano in T11).
4. Lo script in `tools/` che estrae la mappa di base e la voce in `FORKING.md`.
5. Il blocco Data **`flightops.tourCards`**.
6. **Le due verifiche della nota** (§5): `Range` e `ETag` sul pacchetto pubblicato, e se `blob:` serve.

**Test**: Vitest sull'interpolazione del cerchio massimo (anche attraverso l'antimeridiano); smoke `/tours` e `/tours/{slug}` con la mappa
**sotto la CSP vera** e nessun errore in console; un tour nascosto dà 404; `devProxy.test.ts` con `/tiles`.
**Fatta quando**: la pagina di un tour di prova mostra la mappa con la base del mondo servita dall'hub, in sviluppo e nella preview.

### T11 — Il PIREP

Design §1.8, §1.9, §2, §3.1, §3.2, §3.4, §3.6, §5.4. Branch `m2/t11-pirep`.

1. **`TourRules`**: per ogni tipo le tre domande (volabili, prossima, finito), il rifiuto e la tolleranza (§2.5), le leg ritirate e non
   ancora rilasciate; per `Open` obiettivo, filtri e regole di sequenza di §2.6.1; `NoRepeatedRoute` con A→B diversa da B→A.
2. `fo_pireps`, `fo_pirep_flights` (sessione **unica**), `fo_pirep_events`, `fo_enrolments` (il primo PIREP iscrive), `fo_bans` (solo la
   tabella e la lettura).
3. **Il form**: finestra dedicata; la ricerca nel tracker (T2) che esclude le sessioni rivendicate e quelle dopo `close_at`; la scelta del
   volo; tutte le revisioni del piano e quella al decollo; SID, STAR e IAP secondo `I`, `V`, `Y`, `Z`; la deviazione con il volo di
   riposizionamento e il motivo.
4. **I controlli che bloccano** (§3.2 punto 5) con `ProblemDetails` campo per campo: limiti giornalieri per giorno UTC del decollo, callsign,
   aereo con varianti e gruppi, rating pilota, ban, rilascio, «da modificare» in sospeso, filtri `Open`.
5. **Lo snapshot** delle regole effettive con parametri ed errori al primo invio, e i dati della leg com'erano; il reinvio non lo rifà.
6. **Gli stati del pilota**: ritirare da `Queued`; correggere tutto e reinviare da `ToModify`; il **ritiro automatico** dopo
   `report_window_days` in `ToModify` (job giornaliero del modulo).
7. I colori degli stati sulla mappa di `/tours/{slug}` per il pilota; «Invia il report».

**Test**: unit su `TourRules` per ogni tipo con tabelle di casi (compresi ritiro, tolleranza, contestazione che sblocca — la bandiera
esiste già sulla riga), sui limiti giornalieri con l'esempio dei 12 voli del design, su `Open`; integrazione: sessione rivendicata una
volta; limite che blocca; ban che blocca; snapshot non rifatto al reinvio; ritiro automatico. Smoke: il form con il tracker finto.
**Fatta quando**: con il login di sviluppo e un volo del corpus, un pilota invia un PIREP su un tour di prova e lo vede in coda.

### T12 — Gli ATC contattati

Design §3.3, §6.5; nota `2026-09-14-dati-condivisi-con-vipi`. Branch `m2/t12-atc-contacts`.

1. **`IAtcActivitySource`** nel nucleo: «quali posizioni erano online in questo intervallo, e dove». Implementazione **vIPI** (contesto EF
   di sola lettura sulla vista `v_share_atc_sessions`, connessione nei segreti, accesa da `division.json → atcData`) e **nessuna**
   (`Unavailable`). Nessun modulo nomina vIPI.
2. **La proposta**: posizioni online negli aeroporti di partenza, arrivo e deviazione e nei FIR attraversati dai punti delle tracce a
   campione (`IFirLocator`); il pilota toglie e aggiunge; resta scritto chi è proposto e chi aggiunto. Attribuzione OpenAIP accanto.
3. **Le esenzioni**: tipo, quali controlli ammorbidisce, stato («online», «non verificabile»).

⚠️ **Prerequisiti fuori da questo repository**: la vista in vIPI (una migrazione nel suo repository) e, per la produzione, l'utente MariaDB
dedicato (nota vIPI §4). In sviluppo si prova con una vista finta nel database di sviluppo.

**La vista, scritta il 16 settembre 2026 leggendo `AtcSession` di vIPI** (tabella `AtcSessions` in `itivao_atc`). L'hub fa una domanda
sola — «quali posizioni erano online in questo intervallo» — quindi la vista porta dieci colonne e **non** traffico, piste, `ShiftKey`,
movimenti o riepiloghi:

```sql
CREATE OR REPLACE SQL SECURITY DEFINER VIEW v_share_atc_sessions AS
SELECT SessionId         AS session_id,
       UserId            AS vid,
       Callsign          AS callsign,
       Position          AS position,
       Frequency         AS frequency,
       StartUtc          AS start_utc,
       EndUtc            AS end_utc,
       DurationSeconds   AS duration_seconds,
       Rating            AS rating,
       IsOutsideDivision AS is_outside_division
FROM AtcSessions;
```

con `GRANT SELECT ON itivao_atc.v_share_atc_sessions` all'utente di sola lettura dell'hub. `end_utc` nullo vuol dire sessione ancora in
corso. Servono anche le righe **fuori divisione** (i tour si volano nel mondo): vIPI le archivia dal 28 agosto 2026, quindi per i voli
precedenti la copertura fuori Italia è `Unavailable`, mai «fallita».
**Test**: unit sulla proposta con tracce del corpus e un archivio finto; integrazione: senza vIPI il form funziona e dice «non
disponibile»; architettura: il modulo non nomina vIPI né OpenAIP.
**Fatta quando**: un PIREP del corpus riceve una proposta plausibile da una vista finta, e senza vista il form funziona uguale.

### T13 — La validazione

Design §4, §3.5, §8.5. Branch `m2/t13-validation`.

1. **Le code** `/staff/tours/review`: unica e per tour, ordine per data o per tour salvato come **preferenza dell'utente** (T4); chi ha
   `Tours.Validate` su almeno un tour vede tutto in sola lettura; «Prendi» solo dove vale lo scope e mai sui propri (T3).
2. **La presa in carico** con il lease e `row_version` (vince la prima).
3. **La pagina di validazione** `/staff/tours/review/{id}`: leg e volo con la mappa e la traccia, tutte le revisioni del piano,
   procedure, ATC ed esenzioni, deviazione; profilo del pilota nel tour; tabella degli errori delle regole congelate con i conteggi
   nell'anno del volo e da sempre; il **suggerimento**; la decisione con note e `threshold_overridden`. Il meteo arriva in T16, i
   controlli in T17: le due sezioni ci sono già e dicono «non disponibile».
4. **Gli esiti**: intenti `flightops.pirepAccepted`, `pirepToModify`, `pirepRejected` con le regole violate, **senza il nome del validatore**.
5. **Riaprire una decisione** (§4.2.1): il validatore che l'ha presa, FOC e FOAC, con motivazione; nuova mail.
6. **Il riepilogo giornaliero** `flightops.reviewDigest` a chi ha `Tours.Validate`, con la preferenza per spegnerlo, non se la coda è vuota.
7. Il blocco Data **`flightops.reviewQueue`**.

**Test**: integrazione: il ciclo con tutti gli stati; nessuno valida i propri (superadmin compreso); abilitato su un tour e non su un
altro; due prese insieme; il suggerimento con `Dangerous` e con `Warning` oltre `yearly_max`; la mail senza il nome; la riapertura;
il riepilogo non parte a coda vuota. Smoke: coda unica e per tour, pagina di validazione.
**Fatta quando**: un PIREP del corpus si prende, si decide con un errore e il pilota riceve la mail in Mailpit.

### T14 — Contestazioni, chiarimenti, segnalazioni

Nota `2026-09-15-contatti-con-risposte`; design §3.8, §3.10, §3.11. Branch `m2/t14-threads`.

1. **Il nucleo**: `kind`, `participants_json`, `cms_contact_replies`, `cms_contact_references`; `IHasParticipants` nel handler e nel
   filtro; `ThreadOpeningProjection` («una volta sola»); il registro dei risolutori dei riferimenti; gli intenti `contacts.threadOpened` e
   `contacts.threadReplied`; `/me/contacts`; `MessageThread` nell'elenco chiuso; le risposte nel back office dei contatti.
2. **La contestazione**: su un `Rejected` entro `disputeWindowDays`; `dispute_status`, `dispute_text`, il filo aperto dalla proiezione con
   il validatore fra i partecipanti; **la leg non blocca più** (`TourRules`); respinta, torna a bloccare con la tolleranza da quel momento;
   accolta, un validatore riapre il PIREP in coda. I contatori delle contestazioni nella pagina di validazione (non al pilota).
3. **Il chiarimento**: su un PIREP deciso, una leg o una regola, più riferimenti in un messaggio, dal nucleo.
4. **Segnalare un problema su una leg**: `fo_leg_issues`, intento `flightops.legIssueReported` alla casella del FOD.
5. Il blocco Data **`flightops.openIssues`**.

**Test**: quelli della nota §5; più: la contestazione fuori finestra rifiutata; aperta sblocca, respinta riblocca; il chiarimento non
cambia lo stato né conta come contestazione. Giro completo: contestato e riaperto.
**Fatta quando**: un pilota contesta, il validatore risponde dal back office, il pilota riceve la mail e risponde da `/me/contacts`.

### T15 — Completamento, validatori, piloti, ban

Design §3.9, §3.11, §7.2, §8.2, §8.7. Branch `m2/t15-completion-and-people`.

1. **Il completamento**: `completed_at` sull'iscrizione quando un PIREP accettato completa il tour (anche per `Distance`, `Open`,
   `Container` con `required_subtours`); **`AwardSignalProjection`** nella stessa transazione; mai tolto.
2. **Statistiche dei validatori** `/staff/tours/validators`: per anno e per tour; **«aggiungi validatore»** e «togli» (grant con scope, T3,
   `Tours.ManageValidators`).
3. **Pagina del pilota** `/staff/tours/pilots/{vid}`: errori confermati per categoria (anno e da sempre), leg volate con esito e validatore,
   contestazioni, chiarimenti, ban, tour con avanzamento.
4. **Ban** `/staff/tours/bans` (lista e form generati, `Tours.Ban`), intento `flightops.banned` con motivo e durata.
5. Il blocco Data **`flightops.myTours`** (spostato da T10): tour iniziati, avanzamento, prossima leg, PIREP da correggere, chiarimenti con
   risposta, il riepilogo del pilota.

**Test**: integrazione: completamento con segnalazione per ogni tipo; il completamento resta dopo una leg aggiunta; un validatore aggiunto
prende subito sul suo tour; un validatore che non è più staff perde il grant alla sincronizzazione; il ban blocca il PIREP e non la
validazione di quelli inviati.
**Fatta quando**: un tour di prova completato compare nella coda degli award, e chi ha `Awards.Assign` lo assegna.

### T16 — Il meteo salvato

Design §1.13; nota `meteo-e-confini-dei-fir` §3.1. Branch `m2/t16-weather`.

1. `fo_weather_reports` senza doppioni; il **job ogni 30 minuti** sugli aeroporti delle leg dei tour aperti o in chiusura (file di cache
   NOAA, ripiego del METAR); **all'invio del PIREP** gli aeroporti toccati che mancano, METAR **e TAF** della finestra del volo.
2. **La cancellazione**: più vecchio di `weatherRetentionDays` **e** tutti i PIREP con un volo in quel giorno su quell'aeroporto decisi; job giornaliero.
3. **La pagina di validazione**: METAR e TAF di partenza, arrivo e deviazione nell'intervallo, con la fonte; in evidenza con una deviazione `Weather`.

**Test**: integrazione: il job salva senza doppioni; un bollettino non si cancella finché un PIREP di quel giorno è in coda; lo scarico
all'invio con una fonte finta.
**Fatta quando**: in sviluppo il job gira due volte senza doppioni, e un PIREP del corpus mostra i METAR del suo volo.

### T17 — Il motore dei controlli e i controlli sul piano

Design §6.1–§6.4. Branch `m2/t17-check-engine`.

1. `IFlightCheck` (chiave, schema dei parametri con i default, `EvaluateAsync` → `Passed`, `Failed` con evidenza, `Unavailable`);
   `FlightCheckContext` con PIREP, snapshot, revisioni del piano, tracce, `ref_`, meteo, archivio ATC.
2. **Il job** all'invio e al reinvio; `fo_check_results` (`ran_by = server`); gli errori con quella `check_key` diventano **suggeriti**;
   si registra se il validatore conferma.
3. **I controlli sul piano**: `callsign`, `aircraft`, `alternate` con la regola di `ZZZZ` e `ALTN/`, `equipment` (vocabolario da T1),
   `repeatedRoute`.
4. Gli schemi dei parametri collegati al catalogo di T9; la sezione dei controlli nella pagina di validazione con l'evidenza.
5. `semicircularLevels` e `atcCoverage` nel catalogo come controlli **dell'agente**: senza agente risultano `Unavailable`.

**Test**: ogni controllo sul **corpus** con gli esiti attesi di Carmine; un controllo che lancia un'eccezione diventa `Unavailable`, mai
`Failed`; il suggerimento compare nella pagina e nella colonna della coda.
**Fatta quando**: tutti i voli del corpus danno sui controlli del piano l'esito atteso (o la differenza è scritta e decisa con Carmine).

### T18 — I controlli sulle tracce

Design §6.4. Branch `m2/t18-track-checks`.

1. `disconnections`, `parking`, `speed250` (con le esenzioni), `simRate`, `landingAtArrival`, `takeoffFromThreshold` (piste di T1, prua più
   vicina, «decollo da un'intersezione» invece di un fallimento), `vmc` sul METAR più vicino (solo le parti VFR).
2. **Le tarature sul corpus**: `thresholdToleranceMeters` (150 m di partenza) secondo il campionamento misurato in T2; `durationFactor` e
   `durationFixedMinutes` confrontando la stima con la durata delle sessioni. I numeri scelti, e la tabella degli errori che hanno, si
   scrivono nella PR e **si decidono con Carmine** prima di cambiare i default.

**Test**: ogni controllo sul corpus; `vmc` `Unavailable` senza METAR; `speed250` ammorbidito da un'esenzione `FreeSpeed`.
**Fatta quando**: il corpus dà gli esiti attesi, e i numeri delle tarature sono decisi.

### T19 — Token personali e contratto dell'agente

Nota `2026-09-15-token-personali-e-agente-del-validatore`. Branch `m2/t19-agent-contract`.

1. **Nucleo**: `hub_personal_tokens`, lo schema `Bearer` accettato solo per `audience`, l'identità costruita come per il cookie, la regola
   dell'ultimo login entro 30 giorni, `/me/tokens` (il token in chiaro una volta), audit delle scritture.
2. **Modulo**: `GET /api/flightops/agent/contract`, la coda e il dettaglio del PIREP, `POST …/checks` con sostituzione e `ran_by = agent`;
   l'intestazione `Hub-Agent-Contract`.
3. **`docs/agent-contract.md`** in inglese, con esempi e la forma ammessa dell'evidenza (nota §4).

**Test**: quelli della nota §6; più: un agente di prova in C# nei test d'integrazione legge un PIREP del corpus e scrive un esito che
compare come suggerimento.
**Fatta quando**: con un token creato da `/me/tokens`, una `curl` in sviluppo legge un PIREP e scrive un esito che la pagina di
validazione mostra.

### T20 — Conservazione, rifiniture, giro completo

Design §9, §10, §13. Branch `m2/t20-retention-and-round`.

1. **La conservazione** (§10, strada B): job mensile del modulo; dopo 13 o 25 mesi dalla chiusura via tracce, revisioni dei piani, esiti dei
   controlli, snapshot, note, ATC ed esenzioni, briefing, regole del tour, hub, rotazioni, vincoli, iscrizioni; **tour e leg restano**
   archiviati; il registro disciplinare mai. Una riga nel log dei job.
2. **La richiesta di cancellazione dei dati di un pilota** (§10.0): PIREP e dati personali del modulo via, registro disciplinare
   anonimizzato. ⚠️ Il design la segna come **direzione**: in apertura si guarda come il nucleo tratta la stessa richiesta per gli altri
   dati (oggi non è descritto) e, se serve un meccanismo del nucleo, **ci si ferma** con una nota (caso c).
3. **Rifiniture**: la galleria (`RouteMap`, `LegGrid`, `MessageThread`), `docs/UI-GUIDELINES.md` §3, `FORKING.md` (modulo, mappa, OpenAIP,
   agente), la checklist del fork XX, il riepilogo dei PIREP nella ricerca e nel calendario rivisto.
4. **Il giro completo** (e2e con MariaDB vera): tour da template, leg importate, PIREP con tracker finto, controlli, validato, contestato,
   riaperto, completato, award assegnato.
5. **La chiusura di M2 (a)**: rapporto come `decisions/2026-09-07-m1-review.md`, con gli endpoint scritti a mano accanto al motore contati
   (piano §16.6).

**Test**: integrazione: la conservazione non tocca il registro disciplinare e i PIREP di un tour ancora nella finestra; l'anonimizzazione
lascia i conteggi aggregati uguali. Giro completo verde.
**Fatta quando**: il giro completo passa in locale e in CI, e il rapporto di chiusura è scritto.

### T21 — L'app del validatore parla con l'hub

Fuori da questo repository: `D:\Programmazione\IVAO_Test\AutomaticValidatorTour`. Deciso da Carmine il 15 settembre: la adatta Claude,
dopo T19. Si scrive in dettaglio all'apertura, con il codice dell'app davanti.

1. L'app si configura con l'indirizzo dell'hub e un token personale; legge la coda e il PIREP; esegue `semicircularLevels` (rotta
   ricostruita con i fix locali, FRA sui tratti `DCT`, paese dal FIR, `northSouthLevelCountries`) e `atcCoverage`; scrive gli esiti.
2. Mostra in locale tutto quello che mostra oggi.
3. ⚠️ **Prima di distribuirla ad altri validatori**: la mail a `dev@navigraph.com` con la forma delle evidenze e la risposta conservata
   (nota T0 §4). È un messaggio verso l'esterno: lo manda Carmine, o Claude su sua conferma.

**Fatta quando**: su un PIREP del corpus l'app scrive i due esiti e la pagina di validazione li mostra come suggerimenti.

