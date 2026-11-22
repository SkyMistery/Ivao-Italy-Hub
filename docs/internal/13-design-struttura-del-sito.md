# IVAO Division Hub — Design degli indirizzi e della struttura del sito (fase 4)

**Data:** 18 novembre 2026, fase di test, fase 4 della nota `2026-10-22-il-piano-dopo-la-prova-dello-staff` (§4.C e §5 riga 4).
**Stato:** **approvato.** Le sette domande del §8 **le ha decise Carmine in chat al master, 19 novembre 2026**, ognuna con l'opzione
raccomandata (§9, [il commento che le riporta](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/540#issuecomment-7011361289)).
Il codice comincia nell'ordine del §7, una sessione per pezzo; la 4.1 parte in coda dopo questo documento. **Al 22 novembre 2026**
(piano 1.57) sono unite la 4.1 (#546, nota `2026-11-19-le-schermate-del-sito-dichiarate-una-volta`) e la 4.2 (#550, nota
`2026-11-19-l-albero-del-sito`); restano la 4.3, la 4.4 e la 4.5.
**Classificazione** (`CLAUDE.md` §5): **(c)** per due pezzi nuovi del nucleo — le schermate dell'applicazione dichiarate una volta
(§3.1) e il blocco della mappa del sito (§5.1) —; **(b)** per il resto, che estende meccanismi che ci sono: gli indirizzi di
`ContentAddresses`, la lettura delle pagine `GET /api/content/pages`, il form generato, il seme dei contenuti, le impostazioni dei
contenuti. Come il design della fase 3, questo documento è la nota della fase: ogni pezzo dice quale meccanismo estende, quali file
del nucleo tocca e se chiede una nota sua.
**Che cosa tocca:** il nucleo del server (`Core/Content/`, `Core/Modules/IModule.cs`, il bootstrap `/api/me`), il nucleo del browser
(`web/src/features/content/`, `web/src/blocks/`), tre righe dei moduli (le loro schermate pubbliche), i semi
(`seed/content-pages/`) e i file di lingua. **Nessuna tabella e nessuna migrazione**; nessun endpoint scritto a mano in più (§6).

## 0. Perimetro

**Dentro**, i cinque punti del §4.C della nota del 22 ottobre:

1. **l'albero del sito**: chi crea una pagina sceglie la pagina madre, non l'indirizzo (§3.2);
2. **l'indirizzo che nasce dal titolo** sotto la madre, e che cambiano solo WD, HQ e il superadmin (§4.1);
3. **`Content.Approve` sulle pagine a WD e HQ**, con `division.json → positionGrants` e non nel codice (§4.2);
4. **la mappa del sito**, generata dall'albero (§5.1);
5. **le pagine scritte nel codice** nell'albero e negli elenchi, segnate e non modificabili (§3.1, §3.2);

e la **guida «Come si fa una pagina»** per lo staff (§5.2).

**Fuori**: il menu resta un albero suo (nota `2026-09-13-contenuti-centralizzati` §3.6–3.7: «i due alberi restano distinti»); le
fasce ai lati, scrivere nel blocco e la traduzione sono la fase 7; `sitemap.xml` per i motori di ricerca resta com'è (§6); l'ordine
a mano delle pagine sorelle non c'è (§3.2, ultima riga).

## 1. Che cosa c'è oggi

Letto nel codice di `main` (0.26.0, `1a174116`, piano 1.56). ⚠️ **Gran parte dell'albero esiste già**, dalla nota del 13 settembre
(§3.7) e dalle correzioni del lotto 3 (#493): la fase 4 lo rende visibile e chiude tre buchi, non lo costruisce.

- **Una pagina sta sotto un'altra.** `ContentEntry.ParentId` e `ParentPath` (`Core/Content/ContentEntry.cs`), con la colonna calcolata
  e indicizzata del percorso intero (`ContentAddresses.StoredPath`). `ContentAddresses` (`Core/Content/ContentAddresses.cs`) è l'unica
  classe degli indirizzi, usata dal salvataggio (`BeforeSave`), da «nuovo da template» e dal controllo che il form chiede mentre si
  scrive: **tre livelli al massimo** (`MaxDepth`); il primo segmento non può essere una parola dell'applicazione (`ReservedSegments`
  del nucleo più `IModule.ReservedSegments`: `events`, `tours`, `training`); **la cima del sito chiede `Content.Approve`**
  (`TopLevelNotAllowed`); una pagina che si sposta **porta con sé le figlie**, e ogni pagina online si tiene il vecchio indirizzo in
  `PreviousPathsJson`, che manda avanti chi ci arriva; **una pagina con figlie non si cancella** (`DeletionProblemAsync`, #493).
- **Il selettore del genitore** è un campo `choices` piatto del form generato (`features/content/schema.ts`, `parentId`), riempito da
  `GET /api/content/pages` (`ContentPageNodeDto(Id, Path, Title, Depth)`): le pagine online che chi scrive può leggere più quelle,
  bozze comprese, che può modificare (decisione «Filtrare» del 14 novembre). L'etichetta è l'indirizzo; **la profondità c'è nel DTO
  ma il campo non la disegna**. Chi non ha `Content.Approve` deve scegliere un genitore (il campo è obbligatorio).
- **L'indirizzo si vede** sotto i campi che lo compongono (`features/content/AddressPreview.tsx`): libero, preso con il primo libero,
  riservato, troppo profondo, in cima senza permesso. Chiede lo stesso controllo del salvataggio (`GET /api/content/address`).
- **Lo slug è un campo libero.** Il form lo **propone dal titolo** (`slug: z.string().meta({ slugFrom: 'title' })`, la regola in
  `shared/forms/slug.ts`) finché nessuno ci scrive; poi chiunque abbia `Content.Edit` su una bozza lo riscrive. Su una riga **online**
  slug, genitore e visibilità li cambia solo chi ha `Content.Publish` e, dove il tipo si approva, `Content.Approve`
  (`ContentEndpoints.PositionNotTheirsAsync`, nota `2026-10-21-i-campi-fuori-dalla-versione`). Vale uguale per news e documenti,
  anche se la nota del 13 settembre (§3.7, ultima riga) diceva che «news e documenti non scelgono»: quella riga non è mai arrivata nel
  codice.
- **Chi ha `Content.Approve`.** Nessun livello lo ha nella matrice (`Auth/Permissions/RolePermissionMatrix.cs`); lo raggiungono
  **DIR, ADIR, WM e AWM**, perché `ReachesEveryDepartment` dà a coordinatore e assistente di Direzione e Web ogni permesso su ogni
  dipartimento; il **superadmin**; e chi lo riceve **per grant**. Non l'hanno **WMA** (gli advisor del web: «un advisor è advisor del
  suo dipartimento») né le posizioni dello staff di IVAO HQ (`StaffRole.HqStaff`, che leggono tutto e non scrivono niente). Che si
  approvino le pagine è già configurazione: `division.json → contentApproval: ["Page"]`. `division.json → positionGrants` oggi non ha
  righe di `Content.*`.
- **Le schermate scritte nel codice** sono in **tre posti che si tengono d'accordo a mano**: `SiteDestinations.Screens` sul server e
  `SITE_SCREENS` nel browser (`features/content/siteAddresses.ts`), le stesse sei righe (`/`, `/calendar`, `/news`, `/documents`,
  `/search`, `/contact`, con un test d'integrazione che le tiene uguali); e le rotte pubbliche dei moduli (`/tours`, `/events`,
  `/training`, `/training/request`…), che il server conosce solo come primo segmento riservato e il menu non offre fra le schermate.
  `IModule.PublicNavigation` esiste, ma nessun modulo la usa.
- **Gli elenchi.** `/staff/content` è la lista generica, con `slug` fra le colonne e non il percorso (`features/content/list.ts`): una
  pagina sotto `/regolamenti` si chiama solo con il suo ultimo pezzo. Il lettore degli indirizzi del sito (`siteAddresses.ts`, design
  della fase 3 §4.5, nota `2026-11-04-gli-indirizzi-del-sito-che-si-possono-leggere`) unisce pagine, schermate e link della libreria
  per il menu, i blocchi e le istruzioni per Claude.
- **La mappa del sito** non c'è. C'è `sitemap.xml` (`SeoEndpoints.cs`), solo pagine pubbliche, per i motori di ricerca.
- **La guida** non c'è. Il meccanismo per «una pagina che l'editor nomina» c'è: **la pagina dello stile** (nota
  `2026-11-04-la-pagina-dello-stile`), scelta in `ContentSettingsEndpoints` e salvata in `hub_division_settings`.
- **I semi**: `seed/content-pages/*.json`, applicati **una volta** ciascuno da `ContentSeeder`, con il testo in chiavi `{ "$t": … }`
  risolte nelle lingue della divisione; uno nuovo in un rilascio successivo arriva anche sulle installazioni che ci sono già, se
  l'indirizzo è libero.
- ⚠️ **La pagina iniziale** è la riga `home` (`seed/content-pages/home.json`), servita a `/` dalla schermata `HomePage` e anche a
  `/home`. Nell'albero è una riga come le altre, e `/` è la stessa pagina vista da un'altra porta.

## 2. I principi

1. **Nessuna tabella nuova.** L'albero è `cms_contents` con `ParentId`, com'è; le schermate dell'applicazione non sono righe ma una
   dichiarazione del codice (§3.1); la mappa è un blocco di dati; la guida è una pagina.
2. **Un posto solo per ogni regola.** Gli indirizzi restano di `ContentAddresses`; chi cambia uno slug lo dice lo stesso controllo che
   già dice chi sposta una pagina online (`PositionNotTheirsAsync`); le schermate le dichiara il server una volta, e il browser le
   legge da `/api/me` invece di copiarle.
3. **Nessun componente nuovo nell'elenco chiuso.** L'albero è la lista generica (`DataTable` con una configurazione di colonne) letta
   in un altro ordine, con il rientro della profondità; il selettore del genitore resta il campo `choices` del form generato, con le
   etichette rientrate.
4. **La configurazione, non il codice**, per chi approva: una riga di `positionGrants` se serve qualcuno in più (§4.2), mai una
   regola nuova nella matrice o nell'handler.
5. **Il codice non sa che è italiano né che è IVAO**: gli slug dei semi sono in inglese (`site-map`, `how-to-make-a-page`), i testi
   in chiavi di lingua; la divisione «XX» deve passare.

## 3. La struttura

### 3.1 Le schermate dell'applicazione dichiarate una volta (o)

- **Che cosa**: un record del nucleo, `SiteScreen(Path, TitleKey, Visibility)`, e **una lista sola** sul server: quelle del nucleo
  (oggi le sei di `SiteDestinations.Screens`, che diventa questa lista) più quelle che ogni modulo abilitato dichiara con un membro
  nuovo di `IModule`, **`SiteScreens`** (default vuoto, come `ReservedSegments`). FlightOps dichiara `/tours`, Events `/events`,
  Training `/training` e `/training/request` (`Members`). Non sono schermate del sito le pagine personali (`/me`, `/events/mine`,
  `/training/mine`) né il back office (`/staff`).
- **Il browser le legge da `/api/me`** (il bootstrap porta già i blocchi registrati, il menu e i moduli abilitati: `CLAUDE.md` §2),
  e `SITE_SCREENS` sparisce: il menu, i blocchi, le istruzioni per Claude, l'albero e la mappa leggono la stessa lista. Il ⚠️ «si
  tengono d'accordo a mano» di `SiteDestinations` e `siteAddresses.ts` cade; al suo posto un test di architettura del browser
  controlla che ogni schermata dichiarata sia una rotta del router (come `-reserved.test.ts` fa per le parole riservate).
- **Il controllo dei link** (`SiteDestinations.ReachableAsync`, 3.15) e la voce di menu leggono la stessa lista: una schermata di un
  modulo spento non è più un indirizzo che esiste.
- **File del nucleo**: `Core/Modules/IModule.cs`, `ModuleRegistry.cs`, `Core/Content/SiteDestinations.cs`, `IvaoHub.Web/Endpoints/MeEndpoints.cs`,
  `web/src/features/content/siteAddresses.ts`, il client generato; una riga in ogni modulo (codice di FlightOps, Events, Training:
  la PR tocca i moduli per una riga ciascuno, ed è il caso (b) di un'interfaccia del nucleo che cresce). **Nota sua**: no, è questa.

### 3.2 L'albero del sito (1, o)

- **La vista «Albero»** di `/staff/content`, per le pagine: la stessa lista generica, ordinata per **percorso** (la colonna indicizzata
  `StoredPath`), con il titolo rientrato secondo la profondità e una colonna **«Indirizzo»** con il percorso intero al posto dello
  slug. ~~Con il filtro `kind = Page` è la vista **predefinita**~~ **Senza un dipartimento nell'indirizzo** è la vista **predefinita**
  (scelta mia: chi cerca una pagina la cerca dov'è; la riga corretta da Carmine il 21 nov 2026, nota `2026-11-19-l-albero-del-sito` §4
  scelta 1: l'albero non filtra per dipartimento, e la voce di un dipartimento tiene il suo elenco); un interruttore torna
  all'elenco piatto, paginato e filtrabile come oggi. Le news e i documenti non hanno albero: stanno sotto `/news` e `/documents`.
- **La lettura** è `GET /api/content/pages`, che già dà le pagine giuste a chi chiede (§1) e la profondità: cresce di quello che una
  riga dell'albero mostra (stato, dipartimento, visibilità). Senza pagine, come gli indirizzi del sito: una divisione ha qualche
  centinaio di pagine.
- **Le schermate del §3.1 compaiono nell'albero** al loro indirizzo, con il nome della loro chiave, segnate «Schermata
  dell'applicazione», senza azioni: non si aprono nell'editor, non si spostano, non si cancellano, l'indirizzo è fisso. Nella lista
  piatta (che il server pagina dalla tabella) non ci sono: una riga sopra la tabella lo dice e porta all'albero.
- **Il selettore del genitore** (form della pagina, «nuovo da template», pannello di chi approva) diventa l'albero: le stesse scelte,
  in ordine di percorso, con l'etichetta «titolo — /percorso» rientrata. Le schermate ci compaiono **spente**, con la ragione («una
  pagina dell'applicazione non ha pagine sotto di sé»): risposta 4 (a).
- **«Nuova pagina qui»**: ogni riga dell'albero che chi guarda può usare come madre ha l'azione che apre il form della pagina con il
  genitore già scelto (un parametro di ricerca della rotta, come il dipartimento oggi). È il «chi crea sceglie dove va» della nota:
  si parte dal posto, non dall'indirizzo.
- **L'ordine fra sorelle** è quello dell'indirizzo. Un ordine scelto a mano (trascinando) chiederebbe una colonna e una regola nuove:
  non ora; il menu ha già il suo ordine (`sort`), ed è quello che il visitatore vede.
- **File del nucleo**: `ContentEndpoints.cs` (il DTO della lettura), `features/content/list.ts`, `ContentListScreen.tsx`,
  `features/content/schema.ts`, `ContentFormScreen.tsx`, i file di lingua. **Nota sua**: no.

## 4. L'indirizzo e l'approvazione

### 4.1 L'indirizzo nasce dal titolo (1)

- **Chi scrive non vede il campo dello slug.** Vede l'indirizzo che nascerà (`AddressPreview`, com'è) sotto il titolo e la madre. Lo
  slug lo propone il form dal titolo nella **lingua principale della divisione** (`slugFrom`, com'è), e si salva con la riga alla
  creazione.
- **Dopo la creazione lo slug lo cambia solo chi ha `Content.Approve` sulla riga** — DIR, ADIR, WM, AWM, il superadmin, e i grant
  (§4.2) —, anche su una bozza mai pubblicata. Il server lo fa valere nel controllo che c'è: `PositionNotTheirsAsync` oggi guarda solo
  le righe online; per lo slug guarderà anche le bozze, con la stessa chiave `errors.content.position.needsApproval`. Il campo si
  disegna solo per chi può usarlo (il form lo sa dai permessi di `/api/me`, come sa già se una pagina può stare in cima:
  `mayBeAtTheTop`, `ContentListScreen.tsx`).
- **Il genitore** di una bozza continua a sceglierlo chi la scrive, dentro le regole di `ContentAddresses` (non in cima senza
  `Content.Approve`, tre livelli); di una pagina online, chi ha `Content.Publish` e `Content.Approve`, com'è. Spostare non cambia lo
  slug: l'ultimo pezzo resta, la madre cambia.
- **Chi approva corregge** l'indirizzo e pubblica, invece di rimandare (nota del 13 settembre, §3.7): lo fa già dal pannello della
  revisione; con questa regola è l'unico posto dove uno slug sbagliato si corregge.
- **Cambiare il titolo non sposta la pagina.** Un indirizzo che segue il titolo ad ogni modifica romperebbe i link già scritti in
  altre pagine prima ancora che la pagina esca: lo slug nasce una volta (domanda 1).
- **News e documenti**: la stessa regola, che è quella che la nota del 13 settembre aveva già deciso («non scelgono») — domanda 2.
- **File del nucleo**: `ContentEndpoints.cs` (`PositionNotTheirsAsync`), `features/content/schema.ts`, `ContentFormScreen.tsx`,
  `ContentEditor.tsx` (il pannello dei metadati), i test di `PageAddressTests`. **Nota sua**: no.
- ⚠️ **Un test che c'è cambierà**: quelli che scrivono uno slug su una bozza con un coordinatore (cercare in `PageAddressTests` e
  negli altri test d'integrazione dei contenuti, `ContentEndToEndTests` compreso) diventano rifiuti o passano a un utente con `Content.Approve`; la PR li elenca uno per uno, come chiede la
  regola 3 di `CLAUDE.md` §0.

### 4.2 Chi approva le pagine (2)

- **Oggi è già come chiede la nota**: DIR e ADIR (HQ della divisione) e WM e AWM (WD) approvano le pagine di ogni dipartimento,
  perché raggiungono ogni permesso (§1); il superadmin anche. La nota del 22 ottobre chiedeva di darlo «in `positionGrants`, non nel
  codice»: per loro non serve una riga, e scriverla sarebbe un doppione che non cambia niente.
- **Se si vuole qualcuno in più**, è una riga di `division.json`, senza codice: per gli advisor del web
  `{ "department": "WD", "levels": ["Advisor"], "permission": "Content.Approve" }`, **senza `scope`** (vuol dire ogni dipartimento:
  `PositionGrantSeed.Scope`). È un grant a una posizione su tutti i dipartimenti, quindi dà il permesso e non il dipartimento
  (`EffectivePermission.FromOutside`, piano 1.28): WMA approva le pagine di tutti senza diventare staff di tutti. Con quella riga WMA
  può anche mettere una pagina in cima e cambiarne lo slug (§4.1). Domanda 3.
- **Che cosa non si tocca**: `ReachesEveryDepartment` e `StaffRoleMap` restano come sono. Togliere il permesso a DIR/ADIR/WM/AWM per
  ridarlo da configurazione cambierebbe la matrice universale (`CLAUDE.md` §3) per un risultato identico.
- **File**: nessuno. Con la risposta 3 (a) di Carmine (§9) non si aggiunge nessuna riga, e la PR 4.6 non c'è. **Nota sua**: no.

## 5. La mappa del sito e la guida

### 5.1 La mappa del sito (3)

- **Un blocco di dati del nucleo, `siteMap`**, registrato nelle due metà (`CLAUDE.md` §2): lo schema e il componente in TypeScript,
  il tipo, il `kind` e il provider in C#. **`AlwaysLive`**, come `networkStats`: una mappa congelata alla pubblicazione invecchierebbe,
  e — peggio — fotograferebbe le pagine che vede **chi pubblica**, cioè anche quelle dello staff, su una pagina pubblica. Live, il
  provider risponde a **chi legge**: `database.Contents` con il filtro di visibilità del sito (solo pagine con una versione pubblicata
  che quel lettore può leggere), più le schermate del §3.1 che quel lettore può aprire. Un visitatore vede le pagine pubbliche, un
  membro anche quelle dei membri, lo staff anche le sue. `Audience` `Public`; `SharedFor` qualche minuto per chi non ha fatto
  l'accesso, come gli altri blocchi che lo dichiarano.
- **Che cosa disegna**: un elenco annidato (non un componente nuovo: `ul` dentro `ul`, lo stile della prosa), una voce per pagina con
  il titolo nella lingua sullo schermo, rientrata sotto la madre; le sorelle in ordine di titolo. `props`: un titolo facoltativo
  tradotto e, facoltativa, **la radice** (una pagina da cui partire, scelta con il campo indirizzo della fase 3), così una sezione del
  sito può avere la sua mappa. Niente news e documenti singoli (domanda 5): ci sono le schermate `/news` e `/documents`.
- **Una lettura sola**: la query sta in una classe del nucleo accanto a `ContentAddresses` e la usa anche `GET /api/content/addresses`,
  che oggi la scrive per conto suo; la mappa non aggiunge un endpoint (passa da `/api/blocks/data/siteMap`).
- **La pagina**: un seme nuovo, `seed/content-pages/site-map.json`, del **WD**, `Public`, in cima, con una sezione e il blocco
  `siteMap`; titolo in chiavi di lingua. La creano così anche le installazioni che ci sono già, se `/site-map` è libero; dopo è una
  pagina come le altre, che il WD modifica. ⚠️ `sitemap.xml` è riservato, `site-map` no: il test delle parole riservate lo
  ricontrolla.
- **File del nucleo**: `Core/Content/BlockRegistry.cs`, `CoreDataBlockProviders.cs`, `ContentEndpoints.cs` (la lettura in comune),
  `web/src/blocks/` (schema, componente, icona da `lucide-react`), `blockRequiredMedia` no (il blocco non ha foto), i file di lingua,
  il seme, la galleria `/staff/admin/ui-kit` (da sola). **Nota sua**: no.

### 5.2 La guida «Come si fa una pagina» (5)

- **Una pagina seminata**, `seed/content-pages/how-to-make-a-page.json`, del WD, visibilità **`Staff`**, in cima, dal template
  seminato `section-page` (il template «Guida» del piano §9.3 non è fra i semi). Il testo è nostro, in chiavi `seed.pages.howTo.*` di `locales/{it,en}/`, e dice: dove va una
  pagina e chi la mette in cima; l'indirizzo che nasce dal titolo e chi lo corregge; chi approva e che cosa succede dopo «Pronta»; che
  cosa va in una sezione e in una riga; quale blocco per che cosa (un paragrafo per gruppo della tavolozza, con il rimando alla
  galleria); le foto e la libreria; «Scarica per Claude» e «Importa». Dopo il primo avvio è del WD, che la riscrive dall'editor.
- **L'editor la nomina**: un'impostazione dei contenuti accanto alla pagina dello stile, **«La pagina della guida»**
  (`content.guidePage` in `hub_division_settings`, la stessa forma di `content.stylePage`, con lo stesso controllo che sia una pagina
  che lo staff può leggere). Quando è scelta, il form «Nuova pagina» e l'editor hanno il link «Come si fa una pagina». Il seme la
  sceglie quando la crea, se nessuno ne ha scelta un'altra.
- **File del nucleo**: `ContentSettingsEndpoints.cs`, `ContentSeeder.cs` (la scelta della pagina seminata), `contentSettings.ts`,
  `ContentSettingsPanel.tsx`, `ContentFormScreen.tsx`, `ContentEditor.tsx`, il seme, i file di lingua. **Nota sua**: no.
- ⚠️ La guida parla dell'editor com'è: una PR che lo cambia dopo non la aggiorna da sola (è una pagina del database, non un file).
  Il WD la tiene viva; le note di rilascio dicono quando una fase cambia qualcosa che la guida racconta.

## 6. Che cosa non cambia

- **Il modello**: `cms_contents`, `ParentId`, tre livelli, le parole riservate, il 301 dei vecchi indirizzi, una pagina con figlie che
  non si cancella. Nessuna tabella, **nessuna migrazione**.
- **Il menu** resta un albero suo, scritto da WD e HQ, con l'ordine suo e le voci esterne.
- **L'approvazione**: `contentApproval`, `ContentReviewService` e l'handler unico non cambiano; cambia al più una riga di
  configurazione (§4.2).
- **`sitemap.xml`**: solo pagine pubbliche per i motori di ricerca, com'è. Che prenda anche le schermate pubbliche dei moduli è una
  correzione piccola che il §3.1 rende possibile, non parte di questa fase.
- **Gli endpoint scritti a mano** (§16.6): nessuno in più. La lettura delle pagine e quella degli indirizzi ci sono già; la mappa usa
  `/api/blocks/data/{type}`; le schermate viaggiano in `/api/me`.
- **L'elenco chiuso dei componenti** resta com'è.

## 7. L'ordine delle PR

Una PR per riga, ~~ognuna con la sua versione (`Directory.Build.props`)~~ **tutte sulla stessa MINOR** finché non è consegnata, come
dice il piano 1.53: la 0.27.0, aperta una volta dal master (#551, piano 1.57), e nessuna di queste PR tocca `Directory.Build.props`;
tutte del nucleo, quindi ognuna PR sua, con il rimando a questo documento come nota.

| PR | Che cosa | File principali | Dopo |
|---|---|---|---|
| **4.1** | Le schermate dichiarate una volta (§3.1) | `IModule`, `ModuleRegistry`, `SiteDestinations`, `MeEndpoints`, `siteAddresses.ts`, una riga per modulo | — |
| **4.2** | L'albero: la vista, «nuova pagina qui», il selettore del genitore (§3.2) | `ContentEndpoints` (DTO), `list.ts`, `ContentListScreen`, `schema.ts`, `ContentFormScreen` | 4.1 |
| **4.3** | L'indirizzo dal titolo, lo slug solo con `Content.Approve` (§4.1) | `ContentEndpoints` (`PositionNotTheirsAsync`), `schema.ts`, `ContentFormScreen`, `ContentEditor` | — (in parallelo a 4.1; prima di 4.2 se tocca gli stessi file del form, altrimenti dopo) |
| **4.4** | La mappa del sito: il blocco e la pagina seminata (§5.1) | `BlockRegistry`, `CoreDataBlockProviders`, `web/src/blocks/`, `seed/content-pages/` | 4.1 |
| **4.5** | La guida: la pagina seminata e l'impostazione (§5.2) | `ContentSettingsEndpoints`, `ContentSeeder`, `ContentSettingsPanel`, il seme, `locales/` | 4.3 e 4.4 (racconta l'indirizzo e la mappa come sono) |
| ~~(4.6)~~ | ~~La riga di `positionGrants` per WMA~~: non c'è, con la risposta 3 (a) | — | — |

4.2 e 4.3 toccano tutte e due il form della pagina (`schema.ts`, `ContentFormScreen.tsx`): vanno in fila, non accanto. 4.4 tocca
file suoi e corre accanto a 4.2. Le istruzioni per Claude cambiano solo con la 4.4 (un blocco nuovo nella tavolozza): ⚠️ l'impronta
`rules` cambia e un file scaricato prima viene rifiutato, come per i blocchi della fase 3.

## 8. Le domande per Carmine ⚖️

1. **Quando lo slug smette di seguire il titolo** (§4.1). (a) **Nasce alla creazione** dal titolo nella lingua principale, e da lì lo
   cambia solo chi ha `Content.Approve`, anche in bozza — **raccomandata**: la regola sta nel controllo che c'è e non serve una seconda
   copia della regola dello slug in C#; (b) segue il titolo finché la pagina non esce la prima volta, e solo dopo si ferma: il server
   dovrebbe comporlo da sé, con una copia in C# di `slugify` tenuta d'accordo con quella del browser da un file di casi comune.
2. **News e documenti** (§4.1). (a) **La stessa regola** delle pagine: chi scrive non sceglie lo slug — **raccomandata**, era già la
   decisione del 13 settembre (§3.7) mai arrivata nel codice; (b) solo le pagine, e news e documenti restano come oggi.
3. **Chi approva oltre a DIR, ADIR, WM, AWM e il superadmin** (§4.2). (a) **Nessun altro**: oggi è già come chiede la nota, e non si
   aggiunge niente — **raccomandata**; (b) anche gli advisor del web (WMA), con una riga di `positionGrants` senza codice.
4. **Una pagina sotto una schermata dell'applicazione** (§3.2), per esempio `/tours/regolamento`. (a) **No**: una schermata non ha
   pagine sotto di sé, e nel selettore compare spenta — **raccomandata**: la rotta del modulo (`/tours/$slug`) prenderebbe quell'indirizzo
   prima della pagina; (b) sì, per le schermate che lo dichiarano, con una rotta di riserva del modulo verso la pagina: un pezzo in
   più in ogni modulo che lo vuole.
5. **Che cosa mostra la mappa del sito** (§5.1). (a) **Le pagine e le schermate**, ognuno quelle che può leggere — **raccomandata**;
   (b) anche ogni news e ogni documento pubblicati.
6. **La guida** (§5.2). (a) **Una pagina seminata** del WD, visibile allo staff, con il testo scritto da noi in italiano e in inglese,
   che il WD poi riscrive, nominata dall'editor con un'impostazione come la pagina dello stile — **raccomandata**; (b) nessun seme: il
   WD la scrive a mano sulla prova, e l'impostazione la sceglie.
7. **L'ordine del §7**: va bene, o qualcosa va prima?

## 9. Le risposte di Carmine (19 novembre 2026, in chat al master)

Con le sue parole: «Sì, tutte e sette» — ogni domanda con l'opzione raccomandata. Il master le ha riportate sulla PR:
[https://github.com/SkyMistery/Ivao-Italy-Hub/pull/540#issuecomment-7011361289](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/540#issuecomment-7011361289).

1. **Lo slug: (a)**, nasce alla creazione dal titolo nella lingua principale, e da lì lo cambia solo chi ha `Content.Approve`, anche in
   bozza (§4.1).
2. **News e documenti: (a)**, la stessa regola delle pagine (§4.1).
3. **Chi approva: (a)**, nessun altro oltre a DIR, ADIR, WM, AWM e il superadmin; nessuna riga in `positionGrants`, e **la 4.6 non
   c'è** (§4.2, §7).
4. **Una pagina sotto una schermata: (a)**, no; nel selettore la schermata è spenta (§3.2).
5. **La mappa del sito: (a)**, pagine e schermate, ognuno quelle che può leggere (§5.1).
6. **La guida: (a)**, una pagina seminata del WD, visibile allo staff, con il testo in italiano e in inglese scritto da noi, nominata
   dall'editor con un'impostazione come la pagina dello stile (§5.2).
7. **L'ordine del §7: approvato**: 4.1, 4.2, 4.3, 4.4, 4.5.

La risposta è stata data in chat al master e non in un commento: il documento è di una sessione di Carmine, non del collaboratore.

## Da portare nel piano

- **§13** e le fasi della nota del 22 ottobre (§5 riga 4): la fase 4 in cinque PR nell'ordine del §7, approvata da Carmine il
  19 novembre 2026 con le risposte raccomandate (§9).
- **§9.3** (il modello dei contenuti): l'albero del sito è `cms_contents` con `ParentId`, visto nella vista «Albero» di
  `/staff/content`; chi crea una pagina sceglie la madre, e lo slug nasce dal titolo e lo cambia solo chi ha `Content.Approve`
  (risposte 1 e 2); le schermate dell'applicazione compaiono nell'albero, segnate e ferme, e non hanno pagine sotto (risposta 4); chi
  approva le pagine (risposta 3); la pagina della mappa e la pagina della guida sono semi del WD.
- **§9.1** e **§9.7** (i contratti fra nucleo e moduli): `IModule.SiteScreens`, le schermate pubbliche di un modulo, dichiarate una
  volta e pubblicate da `/api/me`; `SITE_SCREENS` e la copia a mano di `SiteDestinations.Screens` non ci sono più.
- **§16** (meccanismi generici): il blocco di dati `siteMap`, sempre live, che risponde a chi legge; la lettura delle pagine del sito
  in una classe sola, per gli indirizzi e la mappa; l'impostazione «la pagina della guida» accanto a quella dello stile.
- **`decisions/2026-09-13-contenuti-centralizzati.md` §3.7**, letta nel piano: «news e documenti non scelgono» arriva nel codice con la
  4.3 (risposta 2).
