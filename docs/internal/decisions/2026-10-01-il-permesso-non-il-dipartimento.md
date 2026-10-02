# Un grant a una posizione su un altro dipartimento dà il permesso, non il dipartimento (E2b)

**Data:** 1 ottobre 2026 — fase E2b di M4, PR del nucleo #212
**Stato:** **decisa** (Carmine, 30 settembre 2026): la (b) della nota `2026-09-30-i-grant-di-chi-collabora-sugli-eventi` (fase E2,
PR #209), data in chat al master e pubblicata sulla PR su sua istruzione ([la risposta][ok], autore `SkyMistery`). Questa nota scrive
**la forma nel codice** della decisione; non apre domande nuove.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: si estendono tre meccanismi che ci sono — il permesso effettivo e il suo claim
(`EffectivePermission`, `HubClaims`), i claim `dept` di `BuildIdentity` (la «portata» della nota
`2026-09-06-autorizzare-su-un-pezzo-di-un-altro-dipartimento`) e il filtro di dipartimento della lista generata
(`TryNarrowToDepartments`) —, sulla strada che A11a di M3 ha aperto per il team di un FIR (nota `2026-09-27-i-capi-fir-sul-loro-fir`
§3.5: «the team of a FIR is not part of the department, and sees the rows of its FIR through the permission itself»). `CLAUDE.md` §0
regola 6: una PR del nucleo a sé, **prima di E3a**.

[ok]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/209#issuecomment-5917066144

## 1. Che cosa ha deciso Carmine

La (b), con le sue parole sulla #209:

1. il permesso effettivo sa di venire da un grant a una posizione su un dipartimento che non è quello della posizione, come sa il suo
   FIR (`EffectivePermission.Fir`), e il claim lo porta;
2. `HubClaims.BuildIdentity` lascia quei dipartimenti fuori dai claim `dept`, come per un permesso tenuto su un FIR;
3. la lista generata aggiunge le righe dei dipartimenti su cui chi legge tiene **il permesso di lettura di quella lista** per quella
   via — la forma di `onTheirFir` in `TryNarrowToDepartments`, con un dipartimento al posto del FIR;
4. l'unico handler, il guardiano dell'interceptor e il filtro globale non cambiano; **un grant a una persona resta come deciso il 6
   settembre**; i test del maintainer restano come sono (`SeveralDepartmentsTests`, `SearchEndpointTests.SearchRespectsVisibility`).

**Una fase del nucleo a sé, E2b, con la sua nota, prima di E3a**; i nove grant di chi collabora si seminano dopo.

## 2. Che cosa c'è, letto nel codice

Il 1 ottobre 2026, su `main` a `db9268f`:

1. **Il calcolo** scrive la stessa sorgente `grant:{id}` per un grant a una persona e per uno a una posizione
   (`EffectivePermissionsCalculator.cs:169–186`), e il FIR solo per un grant al team di un FIR con `firStaffScope: own`.
2. **`BuildIdentity`** scrive un claim `dept` per il dipartimento di ogni posizione e di ogni permesso che viene da un grant, salvo
   quelli con un FIR; un grant senza dipartimento li scrive tutti (`HubClaims.cs:253–272`).
3. **Chi legge i claim `dept`** (`ICurrentUser.Departments`), e nessun altro: il filtro globale delle righe `Visibility.Department`
   (`HubDbContext.VisibleDepartments` e `ModuleDbContext`, quindi anche la ricerca e le pagine), la lista generata
   (`TryNarrowToDepartments`), `/api/me` (`user.departments`: i gruppi dei dipartimenti nella barra dello staff e le loro rotte
   `/staff/{dept}/…`, `reachableDepartments` in `web/src/shared/api/bootstrap.ts`), il blocco del calendario con `myDepartments`
   (`CoreDataBlockProviders.cs:309`).
4. **L'unico handler, il guardiano e `IPermissionHolders`** leggono il permesso — nome, dipartimento, scope, FIR — e mai i claim
   `dept`: un permesso tenuto sull'ED vale sulle righe dell'ED qualunque sia la sua via.
5. **Il team di un FIR con `firStaffScope: all`**, come dice la nota di E2 (§2): il suo permesso è tenuto sul dipartimento del grant
   senza FIR, e `BuildIdentity` ne scrive il claim `dept`: il team entra nel dipartimento, e `docs/FORKING.md` lo diceva («is let in to
   see that department's rows as any grantee is»). **Verificato**; con `own` non ci entra già (A11a).
6. **Oggi nessuno è toccato**: i grant del seme di IT sono del FOD sul FOD, del TD sul TD e del team di un FIR con `own`; nessun grant a
   una posizione su un altro dipartimento. I primi saranno i nove di chi collabora sugli eventi.

## 3. La forma nel codice

### 3.1 Il permesso sa di venire da fuori

- **`EffectivePermission.FromOutside`**, un booleano accanto a `Fir`, falso per ogni permesso di oggi: il permesso è tenuto sul suo
  dipartimento **da fuori**. «Da fuori» e non «da una posizione»: dice quello che conta a chi legge il permesso, e vale per le due vie
  che lo danno.
- **Quali grant** (`UserGrant.GivesThePermissionNotTheDepartment`): un grant a una posizione su un dipartimento che non è quello della
  posizione (`PositionDepartment` diverso da `Department`), **compreso un grant senza dipartimento**, che vale su tutti e quindi anche
  sugli altri; e un grant al team di un FIR, le cui posizioni non hanno dipartimento. Mai un grant a una persona (6 settembre), né uno a
  una posizione sul suo dipartimento: la posizione è già lì.
- **Il calcolo** lo scrive su ogni permesso che un grant così dà **senza FIR**. Con `own` il permesso del team porta il FIR, che lo dice
  già (A11a): il suo claim resta quello di oggi, per ogni capo FIR di IT.
- **Il `View` implicato** (da `Edit` e dagli altri permessi dell'area) è da fuori come il permesso che lo implica, come porta il FIR.
- **Lo stesso permesso raggiunto due volte** — da fuori, e da un grant alla persona o da un ruolo — resta **una** voce, come oggi, e
  vince quella che dà anche il dipartimento. Altrimenti un grant per nome perderebbe il dipartimento per il caso dell'ordine delle
  sorgenti (`grant:10` viene prima di `grant:9`); il ruolo vinceva già.
- **Un divieto** toglie come sempre: sul suo dipartimento, o espandendo il «tutti» nei dipartimenti rimasti, che restano da fuori.

### 3.2 Nel cookie

- **Il segno è il primo carattere del pezzo dello scope**: `Events.View:ED@!`; con uno scope `Events.View:ED@!events:event:5`; su tutti i
  dipartimenti `Events.View@!`. Lo scrive `FormatPermission` e lo legge `ReadPermission`; senza segno il claim di ogni permesso è quello
  di oggi.
- **Chi non lo conosce lo legge chiuso**, per la ragione del FIR (A11a §3.2): un pacchetto di prima, dopo un ritorno indietro, o
  `ParsePermission` — che tiene la sua firma, confrontata da `ResourceScopeAndStakeholderTests` — ci legge lo scope `!`, che nessuna
  riga dichiara, e il permesso non raggiunge nessuna riga; conta solo alla domanda senza riga, che apre un menu. Mai il dipartimento.
  Uno scope non comincia mai con `!`.

### 3.3 Nei claim `dept`

- **`BuildIdentity` lascia fuori il dipartimento di un permesso da fuori**, come quello di un permesso con un FIR: chi lo tiene non è
  nel dipartimento «per vedere». Di quel dipartimento, quindi: nessuna riga `Visibility.Department` (il filtro globale, la ricerca, le
  pagine), nessun gruppo nella barra dello staff né le sue schermate, nessuna riga nelle liste degli altri permessi, nessuna voce nel
  blocco del calendario `myDepartments`.
- **Un grant a una persona fa entrare come il 6 settembre**: la regola scritta sopra `BuildIdentity` resta, e il commento dice ora
  anche la seconda eccezione.

### 3.4 Nella lista generata

- **`TryNarrowToDepartments` tiene anche le righe dei dipartimenti su cui chi legge tiene da fuori il permesso di lettura della
  lista** (`CrudOptions.EffectiveReadPolicy`, come per il FIR), e solo con quel nome: ciò che lo implica è già dentro, perché il calcolo
  scrive il `View` dell'area accanto a ogni permesso dell'area che dà. Chi ha solo quei permessi non riceve il 403 «nessun
  dipartimento».
- **Tenuto da fuori su tutti i dipartimenti**, la lista di quel permesso tiene le righe di ogni dipartimento (`RolePermissionMatrix.AllDepartments`):
  quelle che i claim `dept` di tutti davano a un grant così prima di E2b, e non una riga di più — e nient'altro di nessun dipartimento,
  altrove. Dopo la revisione della #212: prima la lista rispondeva senza nessun filtro, e una riga in cura a nessun dipartimento sarebbe
  passata; per ogni riga vera la risposta è la stessa.
- **Tenuto su una riga sola** (`ResourceScope`) non allarga la lista: apre quella riga all'handler, e basta. Oggi nessun grant a una
  posizione ha uno scope (la schermata dei permessi non lo scrive, il seme non lo conosce): è il lato che chiude, se un giorno qualcuno
  lo scrivesse.
- **Con un FIR** resta di `onTheirFir`.

### 3.5 Che cosa non cambia

- **L'unico handler, il guardiano, il filtro globale, `IPermissionHolders`** (punto 4 di Carmine): `Has(permission, department, scope,
  fir)` risponde già per permesso e dipartimento, e un permesso da fuori è tenuto lì.
- **`/api/me`** non ha un campo nuovo: il browser mostra menu e pulsanti con i permessi e non guarda la loro via (`holdsPermission`),
  come per il FIR (A11a §3.2), e `staffDestinations.test.tsx`, che costruisce la risposta a mano con i campi tutti obbligatori, resta
  com'è. I gruppi dei dipartimenti vengono da `user.departments`, che il cambio stringe da solo.
- **`ICurrentUser`** non ha un membro nuovo (il segno viaggia in `Permissions`): `TestCurrentUser` resta com'è.
- **Nessun test che c'era cambia.** `FirTeamPermissionRulesTests` e `FirTeamPermissionTests` (A11a) restano verdi: con `all` il permesso
  del team è ora da fuori, e i due test lo chiedono all'handler e al guardiano, che non lo guardano, e al calcolo il suo FIR (`null`).
- **Nessuna migrazione**: `GivesThePermissionNotTheDepartment` si calcola, non è una colonna; né il database né lo snapshot cambiano.

### 3.6 Il team di un FIR con `all`

Verificato che entrava nel dipartimento (§2 punto 5). Con E2b il suo permesso è da fuori, perché le sue posizioni non hanno un
dipartimento: tiene il permesso sul dipartimento del grant, ne vede le righe nelle liste di quel permesso, e non entra nel dipartimento.
Lo provano i test di unità (il calcolo con `all` e con `own`); il resto — i claim `dept`, la lista — è la stessa strada di un grant a una
posizione, provata sul database. `docs/FORKING.md` e il commento di `firStaffScope` in `config/division.example.json` lo dicono ora, con
l'avviso per chi contava sul contrario (un grant per nome). IT è su `own`: per lei non cambia niente.

### 3.7 Che cosa vedranno AOD, FOD e MD sugli eventi, detto

Con i nove grant (AOD `Events.View` ed `EventAtc.*`, FOD `Events.View` ed `EventRoutes.*`, MD `Events.View` ed `EventReports.*`, tutti con
`scope: ED`, a tutti i livelli): la lista degli eventi e le liste della loro area con le righe dell'ED, cioè di ogni evento; ogni evento e
ogni riga della loro area con l'unico handler, riga per riga; la sezione «Eventi» del back office, che si apre con un permesso del modulo
tenuto da qualche parte. **Non** vedranno: le righe `Visibility.Department` dell'ED; il gruppo dell'ED nella barra dello staff, con la
sua dashboard, il calendario, le categorie e **i contatti**; le liste di pagine, news, documenti, link e media dell'ED; la coda dei
messaggi dei membri all'ED.

⚠️ **Nel browser, `writableDepartments`** (i dipartimenti raggiunti e con il permesso: il pulsante «nuovo» di una schermata che non è di
un dipartimento) **non offre il dipartimento di un permesso da fuori**, che non è fra quelli raggiunti. Una schermata del modulo che fa
creare una riga a chi collabora — una rotta del FOD sotto un evento — la crea sotto l'evento, con maschera e scope copiati dall'evento
(`BeforeAuthorize`), e chiede al server che cosa si può fare (le `actions` della pagina), non a `writableDepartments`.

### 3.8 Il grant di E10f senza dipartimento

**E10f** (in corso il 1 ottobre, branch `m4/e10f-grantable-award-assign`) rende `Awards.Assign` concedibile e lo dà all'MD con un grant
a una posizione **senza `scope`**. Su `main` un permesso da un grant senza dipartimento fa scrivere a `BuildIdentity` tutti i claim
`dept` (§2 punto 2): coordinator e assistant dell'MD entrerebbero in **ogni** dipartimento — la sessione di E10f l'ha misurato,
`user.departments` con tutti e nove. Con E2b quel grant è da fuori (§3.1: una posizione su tutti i dipartimenti) e l'MD resta nel solo
MD per ciò che vede. Detto alla sessione di E10f il 1 ottobre, che lo chiude anche da sé **con lo stesso campo e la stessa forma**
(`FromOutside` ultimo parametro, lo stesso ordine delle voci uguali, la stessa riga in `BuildIdentity`) per ogni permesso globale da
un grant, anche per nome, con il `View` che porta. Chi delle due arriva seconda a `main` tiene una dichiarazione sola e somma le due
condizioni del calcolo: `fir is null && (grant.GivesThePermissionNotTheDepartment || catalogue.IsGlobal(grant.Value))`.

**E10f è arrivata prima** (#213, unita il 1 ottobre alle 11:05 UTC), e **la riconciliazione è in questo branch**, con un merge di `main`
(la richiesta del revisore sulla #212): un `FromOutside` solo, con le due vie nel suo `<param>`; la condizione sommata qui sopra; un
`.ThenBy(FromOutside)` solo, con le due ragioni; la riga di `BuildIdentity` con i due commenti; le due aggiunte in `docs/FORKING.md` e in
`config/division.example.json`. E10f non scriveva il segno nel cookie, E2b sì: chi assegna gli award per grant ha ora **`Awards.Assign@!`
e `Awards.View@!`**. **Ogni riga di `Award` resta aperta**, per due strade: il catalogo è condiviso in lettura (`Award` è
`ISharedForReading`, e la sua lista dice `SharedForReading = award => true`), e l'unico handler risponde alla lettura di una riga
condivisa con `HasAny(Awards.View)`, che non guarda né il dipartimento né il segno; e `Awards.View@!` si rilegge senza dipartimento, cioè
tenuto su tutti da fuori, così la lista generata ci aggiunge ogni dipartimento (§3.4) e chi non sta in nessun dipartimento non riceve il
403 «nessun dipartimento». Lo prova `PermissionFromOutsideTests` (il quarto caso, VID 761094): una posizione di HQ, che non ha
dipartimento, con `Awards.Assign` per nome legge gli award di SOD e FOD nella lista e uno riga per riga; senza il ramo di tutti i
dipartimenti la lista risponde 403, quello che `main` dava a un lettore così prima di E2b (la nota di E10f lo diceva). Un lettore che
non conosce il segno legge lo scope `!`: per `Awards.Assign`, globale, conta solo `HasAny`, che dice sì; per `Awards.View` la riga resta
aperta dalla lettura condivisa.

### 3.9 I nove grant di chi collabora

**Non sono in questa PR.** Nominano permessi del modulo degli eventi, che esistono solo con E2 (#209), non ancora unita il 1 ottobre
2026. **Li porta E3a** — o la prima fase che trova unite E2 ed E2b —, in `config/division.json` e in `config/division.example.json`, con
un test che raggiungono la loro parte e non l'ED; e cambia `EventsArchitectureTests` di E2, che oggi li rifiuta nei due file finché
questa nota non ha risposta. **Misurato** (§6): con E2 e i nove grant sopra E2b l'integrazione intera è verde, e i tre test del
maintainer che la nota di E2 aveva visto rossi con loro.

## 4. Alternative scartate

| Alternativa | Perché no |
|---|---|
| Il segno nel pezzo del dipartimento (`Events.View:!ED`) | un lettore di prima di A11a legge un dipartimento che non sa leggere come «ogni dipartimento» |
| Un claim a parte per i dipartimenti tenuti da fuori | due claim per un permesso; e la lista vuole sapere **quale** permesso è tenuto da fuori, non solo dove |
| Il segno anche sui permessi del team di un FIR con `own` | il FIR lo dice già; cambierebbe il cookie di ogni capo FIR, senza niente da guadagnare |
| Nella lista, il dipartimento di **ogni** permesso di lettura tenuto, qualunque la via | allargherebbe anche i ruoli: il `Content.View` su tutti dello staff di HQ aprirebbe la lista di ogni dipartimento |
| Un membro nuovo di `ICurrentUser` (i dipartimenti tenuti da fuori) | `TestCurrentUser` del maintainer non compilerebbe più; `Permissions` porta già il segno |
| Un campo nuovo in `/api/me` | il browser non lo userebbe, e `staffDestinations.test.tsx` costruisce la risposta con i campi tutti obbligatori |
| Anche il filtro globale di `Visibility.Department` | deciso: non cambia (punto 4); nessuna riga degli eventi è visibile per dipartimento |
| Da fuori solo un grant su **un altro** dipartimento, non quello su tutti | un grant a una posizione senza dipartimento farebbe entrare i suoi titolari in ogni dipartimento: più di un grant su uno solo |

## 5. Che cosa si tocca

- **Il nucleo**: `Auth/Permissions/EffectivePermissionsCalculator.cs` (`EffectivePermission.FromOutside`, il calcolo, l'ordine delle voci
  uguali); `Auth/UserGrant.cs` (`GivesThePermissionNotTheDepartment`); `Auth/HubClaims.cs` (il segno nel claim, `ReadPermission`, i claim
  `dept`); `Auth/ICurrentUser.cs` (il commento di `Departments`); `Data/Crud/MapCrudExtensions.cs` (`TryNarrowToDepartments`).
- **Le parole**: l'aiuto del form della schermata dei permessi (`grants.formHint` in `locales/en/common.json` e `locales/it/common.json`)
  dice le due portate. La nota del 6 settembre lo prometteva («la schermata dei grant lo dirà») e non lo diceva ancora; è una frase, e si
  toglie da sola se il revisore la vuole altrove.
- **I documenti pubblici**: `docs/FORKING.md` (che cosa dà un grant a una posizione su un altro dipartimento; `firStaffScope: all`, con
  l'avviso) e i commenti di `config/division.example.json` (`positionGrants`, `firStaffScope`).
- **I test nuovi**: `tests/IvaoHub.UnitTests/PermissionFromOutsideRulesTests.cs` e
  `tests/IvaoHub.IntegrationTests/PermissionFromOutsideTests.cs`.
- Nessuna migrazione, nessun endpoint, nessun cambio al contratto OpenAPI, nessun file del maintainer, nessun test che c'era.

## 6. I test, e la misura

- **Di unità** (`PermissionFromOutsideRulesTests`, 8): quali grant danno il permesso da fuori — una posizione su un altro dipartimento e
  su tutti sì, sul suo no, una persona no, i ruoli no — e il `View` implicato; un divieto su un dipartimento accanto a un grant su tutti,
  che lascia da fuori ognuno degli altri otto; il team di un FIR con `all` da fuori e con `own` sul suo FIR;
  lo stesso permesso da una posizione e per nome resta della persona; il segno nel claim, che va e torna, e un lettore che non lo conosce
  e lo legge chiuso; nessun claim `dept` dal permesso da fuori, anche su tutti i dipartimenti, e il claim da un grant per nome; l'unico
  handler che lo tiene sulle righe del suo dipartimento.
- **Sul database** (`PermissionFromOutsideTests`, 4, VID 761091–761094; 761090 è un'identità dei test di unità): gli advisor dell'AOD con
  il permesso di lettura del modulo di prova sul SOD leggono le righe del SOD nella lista generata che legge con quel permesso, e una di
  esse con l'unico handler, e non una riga del FOD; **e nient'altro del SOD** — nessun `dept` in `/api/me`, quindi nessun gruppo del SOD
  nella barra; nessun link del SOD nella lista dei link, che leggono con un altro permesso sul loro dipartimento; nessuna riga che il filtro
  globale tiene al SOD, né del modulo né nella ricerca. Un grant **per nome** sullo stesso dipartimento fa ancora entrare: `dept` SOD, i
  link del SOD, le righe `Visibility.Department` del SOD nella pagina del modulo e nella ricerca. **Dopo la revisione della #212** (VID
  761093): gli advisor dell'AOD con `Links.View` su **tutti** i dipartimenti, da fuori, leggono nella lista dei link quelli di AOD, SOD e
  FOD, e restano nel solo AOD (`/api/me`, la ricerca); un divieto di `Links.View` sul FOD alla stessa posizione toglie il link del FOD e
  lascia gli altri. **Dopo la riconciliazione con E10f** (VID 761094): una posizione di HQ, in nessun dipartimento, con `Awards.Assign`
  per nome legge ogni award di ogni dipartimento (§3.8).
- **Provati al contrario**: con il codice di `main` il primo test d'integrazione cade sul primo `Assert` (`/api/me` dice `["AOD",
  "SOD"]`); con E2b senza il pezzo della lista cade sulla lista; senza l'ordine delle voci uguali cade il test di unità della persona. Il
  secondo test d'integrazione è verde anche su `main`: il 6 settembre non cambia. Il caso di tutti i dipartimenti: senza il suo ramo
  della lista cade sulla lista (il link del SOD manca); con il codice del nucleo di `main` la lista passa e `/api/me` dice tutti e nove i
  dipartimenti.
- **La misura della nota di E2, rifatta sopra E2b** (1 ottobre 2026, in un worktree di prova mai spinto e tolto dopo: E2b più
  `origin/m4/e2-events-skeleton` a `b3b4849` più i nove grant in `config/division.json`, cioè i 22 del design): **integrazione intera
  senza filtro 443/443** (8,1 minuti). I tre test del maintainer che la nota di E2 aveva visto rossi — `SeveralDepartmentsTests` due
  volte, `SearchEndpointTests.SearchRespectsVisibility` — sono verdi senza essere toccati, e `ErasureTests` pure (E2 ha le sue righe
  `evt_`). Unità 944, **un solo rosso, voluto**: `EventsArchitectureTests.TheDivisionFilesGiveTheEventsAndWhoeverCollaboratesWhatTheDesignSays`
  di E2, che rifiuta i nove grant nei file finché questa nota non ha risposta; lo cambia E3a (§3.9).

## Da portare nel piano

- **§6.3** (i grant): un grant a una posizione su un dipartimento che non è il suo — su un altro, o su tutti — e un grant al team di un FIR
  con `firStaffScope: all` danno **il permesso, non il dipartimento**: il permesso vale nell'unico handler e nel guardiano come ogni altro,
  la lista generata che legge con quel permesso tiene le righe di quel dipartimento, e chi lo tiene non entra nel dipartimento per ciò che
  vede (righe `Visibility.Department`, schermate del dipartimento, altre liste). La «portata» della nota del 6 settembre vale per un grant
  a una persona. Nel cookie il permesso tenuto da fuori ha un `!` in testa al pezzo dello scope, che un lettore che non lo conosce legge
  chiuso.
- **§4.1** (`firStaffScope`): con `all` il permesso del team vale sul dipartimento **senza farvi entrare** il team.
- **§16.2–§16.3** (l'unico handler, la lista generata): la lista generata tiene anche le righe dei dipartimenti su cui chi legge tiene da
  fuori il suo permesso di lettura, accanto a quelle del FIR (A11a).
- **`CLAUDE.md` §2**, la riga dei permessi su una riga sola e dei team di un FIR, se il master lo ritiene utile: un grant a una posizione
  su un altro dipartimento dà il permesso, non il dipartimento.
- **`10-piano-implementazione-m4.md`**: la fase E2b fra E2 ed E3a (scritta in questa PR).
