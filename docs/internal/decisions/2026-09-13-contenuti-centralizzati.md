# I contenuti centralizzati: una schermata per tutto lo staff, le pagine approvate, i documenti nelle pagine

**Data:** 13 settembre 2026 — portata da Carmine dopo essersi confrontato con lo staff di IVAO
(«dobbiamo centralizzare la creazione di documenti, news e pagine»)
**Stato:** **decisa** (Carmine, 13 settembre 2026) sulle domande di §3; un punto da confermare in §5
**Regola applicata:** `CLAUDE.md` §5. La schermata unica e la lettura condivisa di media e link sono
**(b)**, estensioni del motore di lista e di `ISharedForReading`; l'**approvazione delle pagine** è
**(c)**, un meccanismo nuovo.

## 1. Che cosa serve, detto da Carmine

1. Uno staffista va nel menu del suo dipartimento, clicca «Pagine» e arriva a **`/staff/content`**,
   una schermata **comune a tutto lo staff**: ci vede solo pagine, news e documenti dei dipartimenti
   di cui fa parte, e solo lì crea e modifica.
2. **Una pagina** la crea un membro del dipartimento e la segna **pronta**; un membro del WD o
   dell'HQ ne **approva** la pubblicazione, e la pagina va online.
3. **News e documenti** hanno il loro indirizzo ma **stanno dentro le pagine**: chi li crea deve
   poterli far comparire nelle pagine che hanno una sezione documenti.
4. **Link e media library**: una schermata sola, ognuno vede i propri dipartimenti, WD e HQ vedono
   tutto.
5. **I template** funzionano come news e documenti, nella stessa schermata.

## 2. Perché i meccanismi di oggi non bastano

- **Il `{dept}` nell'URL.** Lista e form stanno sotto `/staff/{dept}/content|news|documents|
  templates|links|media`: il filtro globale e l'unico authorization handler sanno già quali righe
  una persona raggiunge, ma la schermata ne mostra un dipartimento alla volta. Si **estende** il
  motore (caso b), non si scrive una lista nuova.
- **La pubblicazione è di chi scrive.** `PublishStatus` ha `Draft` e `Published`, e `Content.Publish`
  ce l'hanno coordinator e assistant del dipartimento proprietario. Uno stato intermedio, una
  versione candidata e un permesso che non segue il dipartimento **non esistono**: è il caso (c).
- **Media e link** si leggono solo dal dipartimento proprietario; il picker di una pagina Training
  non vede il logo caricato dal Web. `ISharedForReading` esiste già — lo usano i template — ma
  nessun'altra entità lo dichiara.

## 3. Le decisioni

### 3.1 Una schermata per tipo di oggetto, non per dipartimento

- **`/staff/content`** elenca pagine, news, documenti e template, con `kind` e `department` nei
  search params (la ricetta `validateSearch` di design M0 §7.3). Mostra l'unione dei dipartimenti che
  la persona raggiunge; nessun filtro scelto vuol dire tutti.
- **Le voci del menu del dipartimento restano** e portano alla stessa schermata già filtrata:
  «Pagine» di Training → `/staff/content?kind=page&department=training`.
- **Alla creazione** il dipartimento è una select con i soli dipartimenti in cui si ha il permesso di
  scrittura; se è uno solo è già scelto e non si mostra.
- **Stessa estensione, stesso giorno, per `/staff/links` e `/staff/media`.** Le rotte
  `/staff/{dept}/content|news|documents|templates|links|media` diventano redirect alla schermata
  filtrata e poi spariscono.
- **Chi vede tutto**: Director e Web (coordinator e assistant: `ReachesEveryDepartment`), il
  superadmin, e **chi riceve un grant** sul permesso con dipartimento `null` («ogni dipartimento»),
  che `hub_user_grants` sa già esprimere. ⚠️ Da verificare nella fase: che un grant `null` allarghi
  davvero la **lista** e non solo l'autorizzazione della singola riga (la nota del 3 settembre
  `reaches-every-department` fa poggiare il filtro della lista su un fatto del ruolo).

### 3.2 Le pagine si approvano, news documenti e template no

- **Un terzo stato, `Ready`**, additivo in `PublishStatus`.
- **Segnare «pronta» fotografa una versione candidata** in `cms_content_versions`; chi approva
  pubblica **esattamente quella**, così non si approva una pagina e ne va online un'altra. Se l'autore
  modifica dopo averla segnata pronta, torna in bozza e la candidata decade.
- **Chi approva può rimandare indietro** con una nota. Tutti e due i passaggi — «c'è una pagina da
  approvare», «la tua pagina è stata rimandata / pubblicata» — sono intenti del servizio notifiche del
  nucleo.
- **Ogni ripubblicazione di una pagina già online ripassa dall'approvazione**; nel frattempo il
  pubblico vede la versione precedente, come già fanno le versioni.
- **Il permesso è `Content.Approve`** (grammatica §16.3). Lo tengono Director e Web ovunque — cioè
  WD e HQ — e chi lo riceve **per grant**, su un dipartimento o su tutti. **Il coordinator del
  dipartimento non ce l'ha**: segna pronte le sue pagine come chiunque altro.
- **Chi ha `Content.Approve` sul dipartimento della pagina pubblica direttamente**: le pagine create
  da WD e HQ non passano dall'approvazione.
- **News, documenti e template si pubblicano dal dipartimento**, con `Content.Publish` come oggi.
- **Quali `kind` richiedono approvazione è configurazione**: `division.json → contentApproval:
  ["page"]` per IT. Una divisione che non la vuole lascia l'elenco vuoto e il flusso è quello di oggi.
  Il servizio di pubblicazione chiede `Content.Approve` invece di `Content.Publish` quando il `kind`
  è nell'elenco: una riga nel servizio, **non** un authorization handler nuovo.

### 3.3 News e documenti restano indici generali, e le pagine li «tirano»

- **`/news` e `/documents` restano** come indici pubblici generali, e ogni news e documento tiene il
  suo indirizzo (`/news/{slug}`, `/documents/{slug}`).
- **La pagina tira, il documento non si infila.** Una sezione documenti di una pagina è un blocco
  `documentList` (o `newsList`) che dice quali contenuti elenca: dipartimento e **raccolta**. Chi crea
  il documento sceglie in quali raccolte sta, e con questo decide in quali pagine compare. Un
  documento può stare in **più raccolte**, quindi in più pagine.
- **La raccolta** allarga la categoria di oggi: un vocabolario per dipartimento (dato, non codice) e
  un elenco sul documento invece di un valore solo, così una pagina può avere due sezioni documenti
  dello stesso dipartimento e un documento può comparire in tutte e due. Si scarta una tabella di
  collocazioni documento↔(pagina, sezione): per sapere quali pagine hanno una sezione documenti il
  server dovrebbe leggere le `props` dei blocchi, che per §16.5 non legge mai.
- **Nell'editor del documento** si mostra, in sola lettura, «compare in: Training › Guide», calcolato
  dalle raccolte scelte.
- **Un documento aggiunto a una pagina già approvata non ripassa dall'approvazione**: si approva la
  pagina, non ciò che la pagina elenca.

### 3.4 Media e link si usano da tutti, si gestiscono dal proprietario

- `MediaAsset` e `Link` dichiarano **`ISharedForReading`**, come i template: **tutto lo staff li legge
  e li sceglie** nei picker; **modificarli e cancellarli** resta del dipartimento proprietario, di WD
  e HQ e dei grant. Nella schermata `/staff/media` si vedono quindi tutti, con il filtro
  «dipartimento» e le azioni di modifica solo sulle righe proprie.
- Solo i media **pubblici** (quelli che una pagina pubblicata può mostrare) entrano nel picker di un
  altro dipartimento; un media con visibilità `department` resta del suo dipartimento.

### 3.5 I template nella stessa schermata

- Sono `kind` della schermata unica (filtro «Template»), si creano e modificano con
  `Content.ManageTemplates` — **coordinator e assistant coordinator** del proprio dipartimento, **WD e
  HQ** ovunque, e i **grant** — e **non** passano dall'approvazione. La lettura resta comune a tutto
  lo staff, come deciso il 5 settembre. Nessun cambio alla matrice: gli advisor già non hanno
  `Content.ManageTemplates`.

## 4. Il dubbio di Carmine: un documento AOD in una pagina TD

Con §3.3 **non serve essere WD o HQ**, e la risposta segue il proprietario di ciascun pezzo:

- **Chi scrive la pagina TD** (lo staff Training) può mettere nella pagina una sezione che elenca una
  **raccolta dell'AOD**: il blocco `documentList` accetta già un dipartimento diverso da quello della
  pagina, e leggere documenti pubblicati non chiede permessi. La pagina poi passa comunque
  dall'approvazione di WD o HQ.
- **Lo staff AOD da solo non può** infilare un suo documento in una pagina TD: non scrive quella
  pagina. Può metterlo in una sua raccolta; se la pagina TD elenca quella raccolta, il documento ci
  compare.
- **WD e HQ** possono fare tutte e due le cose.

⚠️ **Da confermare con Carmine** che sia il comportamento voluto.

## 5. Che cosa si tocca

- **Piano**: §6.3 (il permesso nuovo), §7 (`PublishStatus`, raccolte), §8.2 (sitemap staff), §9.1
  (documenti, media, link), §9.3 (editor, pubblicazione), §9.4 (documenti), §16.3 (catalogo dei
  permessi del nucleo).
- **Codice**, in fasi da scrivere in `04-piano-implementazione-m1.md` (o in un piano di M1-bis):
  1. schermata unica per content, template, link e media, con i redirect dalle vecchie rotte;
  2. `ISharedForReading` su media e link, e il picker;
  3. `Ready`, la versione candidata, `Content.Approve`, `contentApproval` in `division.json`, le
     notifiche e la coda «da approvare» per WD e HQ;
  4. le raccolte: vocabolario, colonna JSON sul contenuto, `documentList`/`newsList` che le leggono,
     «compare in» nell'editor.
- **`CLAUDE.md`** §2 (la tabella dei meccanismi: pubblicazione e approvazione).
