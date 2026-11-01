# IVAO Division Hub — Design dell'editor (fase 3)

**Data:** 31 ottobre 2026, fase di test, fase 3 della nota `2026-10-22-il-piano-dopo-la-prova-dello-staff` (§4.A, §4.B, §5 riga 3
e le risposte di Carmine del §7).
**Stato:** **approvato.** Le sette domande del §8 **le ha decise Carmine in chat al master, 31 ottobre 2026**, ognuna con l'opzione
raccomandata (§9). Il codice comincia dopo il merge di #420 e di questo documento, nell'ordine del §7, una sessione per pezzo.
**Classificazione** (`CLAUDE.md` §5): **(c)**, funzioni nuove dell'editor e dei blocchi del nucleo. Questo documento è la nota della
fase: ogni pezzo dice quale meccanismo estende, quali file del nucleo tocca e se chiede una nota sua.
**Che cosa tocca:** il nucleo del browser (`web/src/features/content/`, `web/src/blocks/`, `web/src/shared/forms/`, tre componenti
dell'elenco chiuso — `MediaPicker`, `MarkdownContent`, `StaffSidebar` — che cambiano senza che l'elenco cresca), tre punti del nucleo
del server (la busta dei blocchi, la pubblicazione, le postazioni ATC) e `docs/UI-GUIDELINES.md` per le due regole che Carmine ha
cambiato il 22 ottobre. Una migrazione, additiva (§6.8).

## 0. Perimetro

**Dentro**, nell'ordine in cui li dice la nota del 22 ottobre:

- **in testa, «importa da Claude»** (Carmine, 31 ottobre 2026, nota `2026-10-30-una-pagina-scritta-con-claude`, risposta 5): è decisa
  per intero in quella nota e qui non si ripete; il §3 dice solo come si lega al resto della fase;
- **l'editor** (§4.A): sezioni visibili e «+ sezione» (2, 8); la barra dello staff che si chiude e i pannelli allargabili (3, 5); le
  voci ripetute chiuse (IX); media e link più veloci (XXII, XXIII, II); la barra di formattazione con i colori del testo (7);
  l'anteprima in una scheda (6);
- **i blocchi** (§4.B): tabella (X), galleria (XII), immagine (V, risposta (b) di Carmine), domande e risposte (XV), altezza
  dell'interattivo (VIII), allineamento (XIV), etichette dei blocchi di dati (XVI), tabella delle frequenze (XX), pulsanti con la
  scelta della pagina e il controllo della visibilità (II).

**Fuori**: «scrivere nel blocco» (3), la traduzione (4) e le fasce ai lati (10) restano nella fase 7, ognuna con la sua nota; l'albero
del sito e l'indirizzo dal titolo sono la fase 4 (che userà il lettore degli indirizzi del §4.5). Già fatte nella fase 2, e qui non
riaperte: callout, video, loghi, calendario a scelta, coordinamento e «pulsante nel testo» fuori dalla tavolozza (`retired`), la riga
«a che cosa serve» di linkList ed embed (`BlockRegistration.about`), il pubblico di un blocco (#414); l'accento delle icone visibile
(XI) è la #363.

## 1. Che cosa c'è oggi

Letto nel codice di `main` (0.15.0, `b35a1fa3`).

- **L'editor** è `BodyEditor` (`features/content/BodyEditor.tsx`), lo stesso per una pagina e per il briefing di un tour; intorno,
  `ContentEditor` aggiunge metadati, salvataggio, pubblicazione e revisione. Tre colonne **fisse** da `xl` in su,
  `13rem / minmax(0,1fr) / 19rem` (`BodyEditor.tsx:753`): tavolozza, pagina, proprietà (la terza appiccicata in alto).
- **Le sezioni sulla pagina** (`blocks/ContentRenderer.tsx`, `SectionView`): nel modo di composizione hanno solo un contorno al
  passaggio del mouse; il nome compare nella barra (`PickedBar`) **solo quando la sezione è scelta**. «+ sezione» c'è soltanto in fondo
  alla pagina (`AddSectionInvitation`, `ContentRenderer.tsx:211`), e `addSection` aggiunge sempre in coda (`features/content/body.ts:124`).
  Il `title` di una sezione è un testo tradotto che il visitatore non vede mai: è già il nome della sezione nell'editor.
  ⚠️ **Le sottosezioni esistono**, a differenza di quanto dice la nota del 22 ottobre (§4.A): una sezione contiene **righe** (sezioni
  figlie, `section.sections`, «Aggiungi una riga», fino alla profondità che il server accetta, `MAX_ROW_DEPTH`). Quello che non esiste
  sono i blocchi dentro i blocchi.
- **La barra laterale dello staff** (`shared/ui/StaffSidebar.tsx`) monta da sé il `SidebarProvider` di Atmosphere (riga 64): lo stato
  aperto/chiuso non si raggiunge da fuori, e nessuna schermata può chiuderla.
- **Le voci ripetute** (`RepeatableList`, `shared/forms/SchemaForm.tsx:1243`): ogni voce è disegnata aperta, con tutti i campi, frecce e
  «Togli». Una data facoltativa (`Instant`) non ha un modo di essere tolta.
- **Il selettore dei media** (`shared/ui/MediaPicker.tsx`): i 24 file più recenti (`PICKER_PAGE_SIZE`, `features/media/queries.ts`),
  **nessuna ricerca**, nessuna pagina successiva. Il caricamento c'è già (`onUpload`, 11 settembre 2026), sotto la griglia, e prende la
  categoria del campo (#359). Il server sa già cercare per nome del file e testo alternativo e filtrare per categoria
  (`Core/Content/MediaEndpoints.cs:58–62`): la ricerca manca solo nel browser.
- **Gli indirizzi nei blocchi** (`blocks/schemas.ts`: `linkFields` di hero e buttonGroup, `cardGrid`, `logoGrid`) sono testo libero.
  **Il menu ha già la lista degli indirizzi del sito**: pagine pubblicate e in bozza, le schermate dell'applicazione (`SITE_SCREENS`), i
  link della libreria, con il campo `Suggest` del form generato (`routes/_staff/staff.$dept.menu.$id.tsx:96`, `SchemaForm.tsx:525`);
  e il server sa dire se un indirizzo porta da qualche parte (`MenuItemWriteDtoValidator.LeadsSomewhereThisSiteOwnsAsync` e
  `Screens`). Nessuno dei due è usato dai blocchi.
- **Il testo** è Markdown in un `Textarea` (`LocaleFields.tsx:53`), disegnato da `MarkdownContent` (`shared/ui/MarkdownContent.tsx`)
  con `react-markdown` 10.1 **senza plugin** e senza HTML. ⚠️ Quindi il **barrato non c'è** (`~~` è GFM, e GFM non è acceso) e nemmeno
  le tabelle Markdown; sottolineato, colore e allineamento non hanno una sintassi Markdown. Il server toglie il Markdown dal testo
  della ricerca con un filtro piccolo e voluto (`BlockDocumentWalker.Prose`).
- **L'anteprima** (`PreviewFrame`) sta dentro l'editor, a tre larghezze, con il confronto con la versione pubblicata. Non c'è un
  indirizzo che mostri la bozza fuori dall'editor.
- **I blocchi della fase**:
  - `image`: larghezza `third | half | full` (`schemas.ts:95`), nessuno schermo intero;
  - `gallery`: solo griglia, immagini **senza testo alternativo né didascalia**, e il clic apre il file in una scheda — perché quando
    è nata «un dialog sarebbe un componente nuovo» (`schemas.ts:287`); oggi `Dialog` è nell'elenco chiuso;
  - `table`: colonne e righe di celle tradotte, scritte campo per campo nel form generato (`schemas.ts:189`);
  - `accordion`: domanda e risposta in Markdown, `allowMultiple`;
  - `interactive`: ⚠️ **l'altezza viaggia già** — il guscio misura `document.documentElement` con un `ResizeObserver` e la manda alla
    pagina (`Core/Content/EmbedShell.html:221–235`), che la accetta solo da quella cornice e la tiene fra 80 e 2000 px
    (`blocks/blocks.tsx`, `InteractiveBlock`). La barra di scorrimento che lo staff ha visto (VIII) è quindi un difetto, non un pezzo
    mancante;
  - `stats` e `networkStats`: l'etichetta di ogni numero è fissa, dalla chiave della metrica;
  - `frequencyTable`: ogni riga scritta a mano (nominativo, frequenza, tipo, CPDLC, rating come testo libero). Le postazioni della
    divisione ci sono già (`ref_ivao_atc_positions`, `IAtcPositionDirectory.OfDivisionAsync`, `Core/Ivao/AtcPositionDirectory.cs`), ma
    **la frequenza è solo nel JSON grezzo** della riga e `AtcPositionDto` non la porta;
  - l'allineamento: qualche blocco ha il suo nelle `props` (hero `left | center`, buttonGroup `left | center | right`), nessuno comune.
    La busta del blocco ha già due campi di presentazione, `column` e `span`, che il server controlla da un insieme chiuso
    (`BlockDocumentWalker.cs:574`).

## 2. I principi

1. **Un editor, un renderer, un form.** Ogni pezzo allarga uno dei tre; nessuno ne scrive un secondo (`CLAUDE.md` §2). Quello che si
   vede solo mentre si compone vive nel contesto di composizione (`blocks/picking.ts`), che il sito pubblico non monta: la pagina di
   un visitatore resta inerte per costruzione.
2. **Nessun componente nuovo nell'elenco chiuso.** Atmosphere ha già `Collapsible`, `Accordion`, `Carousel`, `ToggleGroup`, `Slider`,
   `Popover`, `Command`; l'elenco ha `Dialog`, `MediaPicker`, `SchemaForm`, `Notice`. I pezzi nuovi sono pezzi interni dell'editor o dei
   blocchi (come `SectionFrame` e `DropZone` oggi), non componenti condivisi.
3. **Il server non legge le `props`.** Quando il server deve controllare qualcosa dentro un blocco, lo fa con una **convenzione del
   nome** della chiave, come già fa per `mediaId` (`docs/UI-GUIDELINES.md`, «How a block names a file»): qui `href` (§5.9).
4. **Un corpo salvato non si riscrive.** Un blocco che cambia forma continua a leggere quella vecchia, e il pannello la traduce
   quando lo apre (immagine, pulsanti).
5. **Ogni proprietà nuova arriva a Claude da sola**: le istruzioni dell'import sono composte dagli schemi zod. ⚠️ Per questo ogni PR che
   cambia uno schema cambia anche l'impronta `rules` della nota del 30 ottobre (§4): un file scaricato prima di quella PR viene rifiutato
   con `content.import.rulesChanged`. È voluto; va detto nelle note di rilascio.

## 3. In testa: importa da Claude

Tutto è nella nota `2026-10-30-una-pagina-scritta-con-claude` (decisa, #420). Qui solo i legami con il resto della fase:

- **Due PR**: (3.0a) «Scarica per Claude» — le istruzioni composte, la frase `blocks.<tipo>.about` per **ogni** blocco con il test che
  la chiede (oggi l'hanno solo i blocchi del §3 della nota del 22 ottobre), la pagina dello stile nelle impostazioni; (3.0b) «Importa da
  Claude» — la lettura, i controlli del browser, gli identificativi.
- **Il lettore degli indirizzi del sito** (§4.5) nasce nella 3.0a, perché le istruzioni elencano «gli indirizzi pubblicati che lo
  staffer può vedere»: è la lista del menu tolta dalla schermata del menu e messa in un posto solo, che poi usano i blocchi e la fase 4.
- **Il controllo dei link dell'import** (nota, §3.5) è nel browser e usa quella lista; il controllo del server (§5.9) arriva dopo e vale
  per ogni salvataggio, a mano o importato.
- **I colori del testo e la sintassi del §4.6** entrano nella parte fissa delle istruzioni quando la loro PR è unita, non prima: le
  istruzioni non promettono a Claude una sintassi che il renderer non conosce.

## 4. L'editor

### 4.1 Le sezioni si vedono, e si creano dove servono (2, 8)

- **Che cosa.** Nel modo di composizione, e solo lì, ogni sezione ha sempre un contorno sottile tratteggiato (pieno quando è scelta) e
  un'etichetta in alto a sinistra: il **numero** fra le sorelle (`2`, e `2.1` per la prima riga della seconda sezione), il **nome** e lo
  **sfondo** — «2 · Regolamenti · sfondo azzurro». Le parole dello sfondo sono quelle che la striscia degli sfondi già usa
  (`content.section.options.background.*`). Fra due sezioni, al passaggio del mouse o con la tastiera, un «**+ sezione**» che la
  inserisce **lì**, alla stessa profondità; dentro una sezione, «+ riga» fa lo stesso per le righe. Il nome è il `title` di oggi, che
  nel pannello diventa «Nome della sezione (lo vede solo chi scrive)».
- **Le sottosezioni** (8): sono le righe, e l'etichetta lo dice («Riga 2.1»). Nel pannello di una sezione una frase sola: «Per mettere
  cose una accanto all'altra usa le colonne; per una parte dentro questa sezione, una riga; per raggruppare, le Schede o le Domande».
  Nessun livello nuovo nel modello.
- **Meccanismo**: il contesto di composizione. `Picking.onAddSection` prende dove (`parentId`, posizione); `addSection` di `body.ts` un
  indice. Le etichette le disegna `SectionView` quando `usePicking()` non è `null`.
- **File del nucleo**: `blocks/ContentRenderer.tsx`, `blocks/picking.ts`, `features/content/BodyEditor.tsx`, `features/content/body.ts`,
  i file di lingua. Il server non cambia.
- **Nota sua**: no. **Test**: `blocks.test.tsx` già controlla che la pagina di un visitatore sia inerte — ci si aggiunge che non ha
  etichette; un e2e che inserisce una sezione fra la prima e la seconda.

### 4.2 Più spazio: la barra dello staff si chiude, i pannelli si allargano (3, 5)

- **La barra dello staff.** Il `SidebarProvider` sale da `StaffSidebar` a `StaffLayout`, così lo stato aperto/chiuso — che resta quello
  di Atmosphere, come dice `docs/UI-GUIDELINES.md` §3 — è raggiungibile con `useSidebar()`. L'editor (`BodyEditor`, quindi anche il
  briefing di un tour) la **chiude quando si apre** e la **rimette com'era** quando si esce. Chi la riapre a mano mentre scrive la tiene
  aperta. Fuori dal layout dello staff non fa niente.
- **I pannelli.** Due bordi sottili fra le tre colonne, che si trascinano. Sono separatori veri (`role="separator"`, con il valore
  per un lettore di schermo), si spostano anche con le frecce, e il doppio clic li rimette com'erano. Limiti: tavolozza fra 10 e 20 rem,
  proprietà fra 16 e 36 rem, la pagina mai sotto i 24 rem. Le larghezze sono due variabili CSS della griglia; solo da `xl` in su, perché
  sotto le colonne sono già una sotto l'altra. Le larghezze si **ricordano nel browser** (`localStorage`, dentro un `try/catch`): una
  comodità di chi guarda, non un dato da tenere sul server. È il primo uso di `localStorage` nell'hub: lo dice il codice.
- **La regola delle tre colonne** (`docs/UI-GUIDELINES.md`, «The editor of a page») non cambia: le colonne laterali non si muovono
  quando si cambia modo di guardare la pagina; le muove solo chi trascina.
- **File del nucleo**: `shared/ui/StaffSidebar.tsx` e `app/layouts/StaffLayout.tsx` (cambia dove sta il provider, non che cosa fa: la
  geometria la misura già `e2e/back-office.spec.ts`), `features/content/BodyEditor.tsx` e un pezzo interno nuovo accanto,
  `features/content/PanelSplitter.tsx`. **Nota sua**: no.

### 4.3 Le voci ripetute si chiudono (IX)

- **Che cosa.** In ogni elenco di voci del form generato — passi, pulsanti, righe, domande, immagini, e quindi in ogni modulo — una
  voce è una riga chiusa con il numero e il suo **titolo**, le frecce e «Togli»; un clic la apre e chiude quella aperta prima. Il titolo
  è il valore, nella lingua sullo schermo, del primo campo di testo della voce («Voce 3» se è vuoto); un blocco che vuole un altro campo
  lo dice con `.meta({ entryTitle: true })`. Una voce **nuova** nasce aperta; una voce con un **errore** si apre da sola, perché un
  errore dentro una voce chiusa è un errore che nessuno vede (i percorsi di `useProblemDetails` dicono già quale voce).
- **La data che si toglie**: un campo data facoltativo (`Instant`) ha «Togli la data» accanto.
- **Meccanismo**: il form generato (`SchemaForm`), con `Collapsible` di Atmosphere. **File**: `shared/forms/SchemaForm.tsx`,
  `shared/forms/schema.ts` (l'annotazione), i file di lingua. **Nota sua**: no.
- ⚠️ **Tocca ogni form di ogni modulo.** Gli e2e che aggiungono una voce e la scrivono continuano a funzionare (la voce nuova è aperta);
  quelli che **modificano una voce che c'era** devono aprirla prima. La PR gira tutta la batteria e conta quali spec cambiano: quelle di
  Eventi e Training sono di `dalberone`, e la PR glielo dice prima, sulla PR, invece di cambiarle da sola.

### 4.4 Media più veloci (XXII, XXIII)

- **Cerca.** In cima a `MediaPicker` una casella di ricerca (`q`, sul nome del file e sul testo alternativo, la ricerca che il server
  ha già) e la scelta della categoria fra quelle chiuse della #359, salvo quando il campo ne chiede una sola (i loghi); sotto la
  griglia, «Mostra altri» (la pagina successiva della stessa lettura).
- **Carica qui.** Il caricamento c'è dal 11 settembre, ma sta sotto 24 miniature e lo staff non l'ha visto: sale in cima, accanto alla
  ricerca, e accetta un file **trascinato** sulla griglia. Il file caricato è già scelto (com'è oggi) e la miniatura dice «testo
  alternativo da scrivere» con il link alla sua scheda nella libreria. Il testo alternativo del blocco resta del blocco
  (`docs/UI-GUIDELINES.md`, decisione del 6 settembre 2026).
- **File del nucleo**: `shared/ui/MediaPicker.tsx` (componente dell'elenco chiuso: cambia, non si aggiunge), `features/media/queries.ts`
  (`mediaPickerQuery` prende la ricerca e la pagina). Il server non cambia. **Nota sua**: no.

### 4.5 Scegliere una pagina invece di scriverne l'indirizzo (II, XXIII)

- **Un lettore solo degli indirizzi del sito**: la lista che oggi costruisce la schermata del menu (pagine raggruppate per dipartimento,
  bozze segnate, schermate dell'applicazione, link della libreria) esce dalla rotta e va in `features/content/siteAddresses.ts`; il
  menu la usa da lì. Nasce nella PR 3.0a (§3).
- **Nei blocchi**: un campo indirizzo dei blocchi si annota `.meta({ address: true })` (`linkFields`, `cardGrid`, `logoGrid`, `cta`
  ritirato compreso); `BlockProperties` lo trasforma in `suggestions`, come già fa per le collezioni (`suggestCollections`). Il campo
  resta **aperto**: si sceglie una pagina dall'elenco, oppure si scrive un indirizzo di fuori (§9, risposta 5).
- **File del nucleo**: `shared/forms/schema.ts` (l'annotazione), `features/content/BlockProperties.tsx`, `blocks/schemas.ts`, la rotta del
  menu. **Nota sua**: no. Il controllo che l'indirizzo porti a una pagina che esiste è il §5.9.

### 4.6 La barra di formattazione e i colori del testo (7)

- **Dove.** Sopra ogni campo che il renderer disegna con `MarkdownContent`. Non si deduce da `multiline` (anche un testo semplice va a
  capo): i campi si annotano `.meta({ markdown: true })` e la barra compare lì, in `LocaleFields`. Oggi sono quattro: il testo di
  `text`, il corpo di una scheda (`tabs`), la risposta di `accordion`, il messaggio di `welcome` (`blocks/blocks.tsx`,
  `blocks/personal.tsx`). ⚠️ Il testo di `callout`, `hero`, `timeline`, `cardGrid` e `iconGrid` è disegnato **come testo semplice**:
  un asterisco lì resta un asterisco, e la barra non ci va. Il callout è quello dove lo staff la cercherà: passa a `MarkdownContent`
  nella stessa PR (un callout è prosa); gli altri restano testo, perché sono righe brevi dentro un disegno.
- **Che cosa fa.** Grassetto, corsivo, barrato; elenco puntato e numerato, rientra ed esci (gli elenchi annidati); link, con la scelta
  della pagina del §4.5; il colore. ⌘B e ⌘I. La barra **scrive Markdown** intorno alla selezione del campo: chi scrive vede ancora il
  testo con i segni, e la pagina accanto lo disegna. Scrivere nella pagina senza segni è il pezzo 3, nella fase 7.
- **Il barrato** chiede GFM: `remark-gfm` entra in `MarkdownContent`, con `allowedElements` che tiene fuori le tabelle Markdown (una
  tabella è il blocco tabella).
- **Il colore di una parola** (risposta 1 di Carmine): un **insieme chiuso**, scritto con la sintassi delle direttive
  (`remark-directive`), dove il nome è il colore: `:ocean[parola]`. Un nome fuori dall'insieme si disegna come testo semplice. Ogni
  colore è una coppia chiaro/scuro di token del marchio, e dentro uno sfondo scuro prende il valore scuro per costruzione (la classe
  `dark` degli sfondi scuri).
  - **Quali** (§9, risposta 3): `brand` (blu), `ocean` (azzurro), `aurora` (verde acqua) e `danger` (rosso, per un avviso), ognuno
    con il tono che arriva a 4,5 : 1 su tutti gli otto sfondi nei due temi. L'**arancione resta fuori**: `docs/UI-GUIDELINES.md`
    misura `artifice-low` a 3,7 : 1 sullo sfondo `accent` e `artifice-dark` a 3,2 : 1 sul bianco. ⚠️ Se la misura dice che un tono
    di questi quattro non ci arriva su uno sfondo, la PR si ferma e lo dice: non si toglie lo sfondo e non si abbassa la soglia.
  - **La misura** la fa `e2e/contrast.spec.ts`, che già misura i grigi e i titoli: una frase per colore su ogni sfondo, nei due temi.
- **Sottolineato e allineamento** non sono Markdown (§9, risposta 4): **niente sottolineato** — sul sito una parola sottolineata si
  legge come un link — e l'allineamento è quello **del blocco intero** (§5.6); la barra non ha né l'uno né l'altro.
- **La ricerca**: `BlockDocumentWalker.Prose` toglie anche `~~` e le direttive di colore tenendone il testo (`:ocean[parola]` →
  «parola»), con il suo test.
- **Le regole che cambiano** (`docs/UI-GUIDELINES.md`, nella stessa PR): «no free colour» resta, e si aggiunge che **il testo** può
  prendere uno dei colori dell'insieme dalla barra, e nient'altro; l'accento di un blocco resta «graphics only, never a word», perché
  l'accento e il colore del testo sono due insiemi diversi, misurati per due soglie diverse (3 : 1 e 4,5 : 1).
- **File del nucleo**: `shared/ui/MarkdownContent.tsx` (elenco chiuso: cambia), `shared/forms/LocaleFields.tsx`, `shared/forms/schema.ts`,
  `blocks/schemas.ts`, `src/styles/index.css` (le classi dei colori, nessun valore esadecimale), `Core/Content/BlockDocumentWalker.cs`,
  `e2e/contrast.spec.ts`, `web/package.json` (due dipendenze). **Nota sua**: no; la regola cambiata la registra questo documento.

### 4.7 L'anteprima in una scheda nuova (6)

- **Che cosa.** «Apri l'anteprima» nella barra dell'editor apre in una scheda nuova la **bozza salvata** disegnata con la cornice del sito
  pubblico (intestazione e piè di pagina), alla larghezza vera della finestra, nella lingua scelta. In cima, una striscia: «Anteprima
  della bozza — i link sono disattivati» e «Aggiorna». Prima di aprirla l'editor salva quello che c'è sullo schermo
  (`autosave.flush`), così la scheda mostra quello che si sta guardando; tornando sulla scheda, la lettura si rifà da sola (TanStack
  Query rilegge quando la finestra torna in primo piano), e «Aggiorna» lo fa a richiesta.
- **I link disattivati**: lo stesso modo del contesto di composizione (il clic preso nella fase di cattura e fermato), senza la
  scelta dei blocchi. Gli interattivi si vedono dalla cornice della bozza (`frameAddress(…, 'draft', …)`), i blocchi di dati sono vivi.
- **Meccanismo**: il renderer unico. Una rotta nuova dietro la guardia dello staff ma con la cornice del sito (le tre ricette del
  design M0 §7.3: layout con guardia, dettaglio con loader), che legge la bozza con la lettura del back office che l'editor usa già
  (`contentQuery`): chi non può modificare la riga non la vede, come oggi.
- ⚠️ **Non è un emulatore** (`docs/UI-GUIDELINES.md`, «The multi-device preview is a max-width»): è la stessa pagina a un'altra
  larghezza; le tre larghezze restano dentro l'editor.
- **File del nucleo**: una rotta in `web/src/routes/`, `features/content/ContentEditor.tsx` (il tasto), `blocks/picking.ts` (il modo
  «solo lettura» del contesto). Il server non cambia. **Nota sua**: no.

## 5. I blocchi

### 5.1 Tabella (X)

- **Incolla**: «Incolla da Excel» legge dagli appunti un testo con le tabulazioni (Excel, Fogli, Word) e lo trasforma in colonne e righe;
  la prima riga è l'intestazione se lo si spunta. **Importa un file**: CSV, xlsx, ods, con SheetJS, che l'hub ha già. ⚠️ Il lettore che
  c'è (`readLegFile`) sta nel modulo dei tour, e il nucleo non importa dai moduli: la parte generica («un foglio → righe di celle») sale
  in `web/src/shared/files/sheet.ts` e il modulo la usa da lì. Il codice dei tour è di Carmine: stesso lavoro, commit suo.
  **Crea N × M**: due numeri e un tasto.
- **Scrivere nella cella** (§9, risposta 1): «Modifica nella griglia» apre una **griglia** in un `Dialog` largo, una casella per cella
  nella lingua sullo schermo (con le schede delle lingue), che scrive le stesse `props` con lo stesso `onApplyProps`. È un'eccezione
  **dichiarata** del form generato, solo per la tabella, come il campo del codice dell'interattivo (`carriesSource`): la registrazione
  lo dice, e il form generato resta sotto per le colonne e la didascalia.
- **Le righe**: `rows: 'lines' | 'striped' | 'plain'`, di default quello che si vede oggi.
- Il testo delle celle resta tradotto (`localized`), quindi un incolla scrive la lingua sullo schermo e le altre restano da scrivere
  (o da far scrivere a Claude, §3).
- **File del nucleo**: `blocks/schemas.ts` (proprietà facoltative in più), `blocks/blocks.tsx` (`TableBlock`),
  `features/content/BlockProperties.tsx`, `shared/files/sheet.ts`, e nel modulo dei tour `legFile.ts`. **Nota sua**: no.

### 5.2 Galleria (XII)

- **Tre modi**: `layout: 'grid' | 'carousel' | 'strip'` — la griglia di oggi; il **carosello** di Atmosphere (`Carousel`, con i tasti
  avanti e indietro); la **striscia** che scorre di lato (solo CSS, `scroll-snap`). Le colonne valgono solo per la griglia.
- **Ogni immagine** ha `alt` e `caption`, tradotti e facoltativi. Il testo alternativo mancava: è un difetto di accessibilità che si
  chiude qui.
- **Il clic** apre l'immagine a tutto schermo nel `Dialog` dell'elenco chiuso, con avanti e indietro fra le immagini della galleria; lo
  stesso pezzo dell'immagine (§5.3), scritto una volta nei blocchi. Il commento di `gallerySchema` che lo escludeva si corregge.
- **In stampa** il carosello e la striscia si disegnano come griglia, come le schede si aprono (`PrintContext`).
- **File del nucleo**: `blocks/schemas.ts`, `blocks/blocks.tsx`. **Nota sua**: no.

### 5.3 Immagine: la larghezza trascinando, e a tutto schermo (V, risposta (b))

- **La larghezza** diventa una percentuale della **colonna** in cui sta il blocco: `widthPercent`, da 20 a 100, a passi di 5. I limiti
  che la risposta di Carmine chiedeva:
  - **non più larga della sezione**: 100 % è la colonna, e la colonna sta dentro la larghezza della sezione;
  - **non più stretta di un minimo leggibile**: 20 %, e mai sotto i 10 rem (`min-width: min(100%, 10rem)`);
  - **in percentuale, perché segua lo schermo**: e quando la colonna è stretta (una query del contenitore, sotto i 24 rem: un
    telefono, una colonna di un terzo) l'immagine prende tutta la larghezza, qualunque percentuale abbia.
- **I corpi salvati**: `width` (`third | half | full`) resta letto quando `widthPercent` manca — 33, 50, 100 — dal renderer, e il pannello
  lo traduce aprendo il blocco. Nessuna pagina cambia da sola.
- **Trascinando**: sull'immagine scelta, una maniglia sul bordo destro. Mentre si trascina dice la percentuale; un gesto è un passo di
  Annulla (come la larghezza di una tessera della dashboard). La tastiera ha il campo del pannello. **Il meccanismo** è generico e
  imita quello delle tessere (`Picking.onSpan`, `ContentRenderer.tsx:379`): una registrazione dichiara
  `resizable: { prop: 'widthPercent', min: 20, max: 100, step: 5 }`, il renderer disegna la maniglia sull'involucro del blocco scelto, e
  l'editor scrive la proprietà. Un visitatore non ha maniglie (il contesto è `null`). Oggi lo dichiara solo l'immagine.
- **Dove sta un'immagine più stretta della colonna**: l'allineamento comune (§5.6).
- **A tutto schermo**: `fullscreen` (sì di default); il clic apre il `Dialog` con l'immagine alla misura della finestra, il testo
  alternativo come nome della finestra, Esc per chiudere.
- **La regola che cambia** (`docs/UI-GUIDELINES.md`, «The width belongs to the section»): resta della sezione; dentro la sua colonna,
  un'immagine ha una larghezza in percentuale entro questi limiti.
- **File del nucleo**: `blocks/schemas.ts`, `blocks/blocks.tsx`, `blocks/ContentRenderer.tsx`, `blocks/picking.ts`,
  `shared/modules.ts` (`BlockRegistration.resizable`), `features/content/BodyEditor.tsx`. **Nota sua**: no.

### 5.4 Domande e risposte (XV)

È il blocco `accordion` che cresce, non un blocco nuovo; nella tavolozza si chiama «Domande e risposte».

- `title` (facoltativo, sopra le domande), `numbered` (le domande numerate), `initially: 'closed' | 'first' | 'all'` (come si apre la
  pagina; `all` vale anche «più aperte insieme»).
- **«Più risposte per domanda»** (§9, risposta 2): la risposta resta **un testo** Markdown, che può avere più paragrafi e un elenco,
  scritto con la barra del §4.6. Nessun elenco di risposte.
- In stampa resta tutto aperto, come oggi.
- **File del nucleo**: `blocks/schemas.ts`, `blocks/blocks.tsx`, i file di lingua. **Nota sua**: no.

### 5.5 L'altezza dell'interattivo (VIII)

- **Un difetto, non un pezzo nuovo**: l'altezza viaggia già (§1). La PR comincia **riproducendo** la barra sulla pagina che lo staff ha
  provato, con il suo frammento, nell'anteprima locale (`/embed/preview`), e controlla nell'ordine: la barra di scorrimento della cornice
  stessa (il guscio non dice `overflow: hidden` e un pixel di arrotondamento basta), il contenuto posizionato fuori dal flusso o con
  margini negativi (il rettangolo di `documentElement` non lo conta, `scrollHeight` sì), un frammento che usa `100vh` (dentro una
  cornice cresce con la cornice, fino ai 2000 px), un frammento più alto di 2000 px.
- **La correzione** nel guscio: la misura è il più grande fra il rettangolo e `scrollHeight`, e la cornice non scorre mai.
- **Le istruzioni** (`EmbedGuidelines.md`): un paragrafo «Tablet e telefono» — niente altezze in `vh`, niente larghezze in pixel,
  `viewBox` e `flex-wrap`, una prova a 768 px oltre ai 360 che l'anteprima locale già offre. L'import da Claude rimanda a queste
  istruzioni, quindi le legge anche lui.
- ⚠️ Guscio e istruzioni sono risorse incorporate: dopo averli cambiati si ricompila **e si riavvia** l'API (`CLAUDE.md` §2).
- **File del nucleo**: `Core/Content/EmbedShell.html`, `Core/Content/EmbedGuidelines.md`, forse `blocks/blocks.tsx`. **Nota sua**: no.

### 5.6 L'allineamento (XIV)

- **Nella busta del blocco**, accanto a `column` e `span`: `align: 'left' | 'center' | 'right'`, assente = sinistra. Lo disegna
  l'involucro del renderer (il testo e i pezzi stretti — un'immagine, i pulsanti, un video più stretto); lo sceglie il pannello comune
  del blocco con tre icone, accanto a «Sposta nella sezione»; il server lo controlla da un insieme chiuso come `span`
  (`BlockDocumentWalker`, `errors.body.alignUnknown`).
- **Solo dove vuol dire qualcosa**: una registrazione lo dichiara (`alignable: true`) — titolo, testo, callout, immagine, video,
  pulsanti, testimonianza. Una tabella o una griglia di card prendono già tutta la colonna.
- **Niente scritto due volte**: l'`align` di `buttonGroup` è la stessa cosa e passa nella busta — il renderer legge ancora quello delle
  `props` quando la busta non lo ha, il pannello lo sposta aprendo il blocco. Quello di `hero` resta suo: allinea il contenuto dentro il
  suo sfondo, non il blocco nella colonna.
- **È una modifica del nucleo del server** (la busta): una PR sua, prima dell'immagine (§7), con il test d'integrazione del valore
  rifiutato.
- **File del nucleo**: `blocks/envelope.ts`, `blocks/ContentRenderer.tsx`, `shared/modules.ts`, `features/content/BlockProperties.tsx`,
  `Core/Content/BlockDocumentWalker.cs`, i file di lingua (anche `errors.body.alignUnknown`). **Nota sua**: no, la decide questo
  documento.

### 5.7 Le etichette dei blocchi di dati (XVI)

- Ogni voce di `stats` (`metrics[]`) e di `networkStats` (`figures[]`) ha `label`, tradotta e facoltativa: vuota, l'etichetta di oggi.
- Un blocco di dati di un modulo che mostra numeri fa lo stesso nel suo schema: i tour sono di Carmine e seguono nella stessa PR se ne
  hanno; gli eventi sono di `dalberone`, a cui va una issue.
- **File del nucleo**: `blocks/schemas.ts`, `blocks/blocks.tsx`. Il server non cambia (non legge le `props`, e il fornitore dei dati
  risponde i numeri, non le parole). **Nota sua**: no.

### 5.8 La tabella delle frequenze dalle postazioni dell'hub (XX)

- **La lettura**: `AtcPositionDto` porta la **frequenza**. Una colonna additiva `frequency` di `ref_ivao_atc_positions`, riempita dal
  sincronismo che c'è già (`RefDataSyncJob`, `IvaoAtcPositionReader`): finché il sincronismo non è passato è vuota, e il blocco lo dice.
- **L'elenco con la ricerca**: una lettura del nucleo, `GET /api/atc/positions?q=`, per chi scrive contenuti, in `Core/Ivao/` (le
  postazioni sono dati IVAO: stanno nel suo perimetro, `CLAUDE.md` §3). Nel blocco il nominativo si annota `.meta({ atcPosition: true })`
  e `BlockProperties` lo trasforma in suggerimenti, come le collezioni. Il nome della postazione, il tipo e la frequenza si **copiano**
  nella riga quando la si sceglie. Una postazione che IVAO non elenca si scrive ancora a mano.
- **Copia, non lettura viva** (§9, risposta 6): i valori stanno nella riga del blocco, come oggi. L'editor segnala una riga diversa
  da quello che IVAO dice adesso (la stessa lettura) e offre «**Riallinea**», che la ricopia; un SOP si approva com'è scritto e cambia
  solo quando qualcuno lo ripubblica. Nessun blocco di dati nuovo.
- **File del nucleo**: `Core/Ivao/IvaoAtcPosition.cs`, `Core/Ivao/AtcPositionDirectory.cs`, la configurazione e la migrazione di
  `HubDbContext`, un file di endpoint in `Core/Ivao/`, `blocks/schemas.ts`, `features/content/BlockProperties.tsx`. ⚠️ È un endpoint
  scritto a mano in più: il conteggio per famiglia del §16.6 del piano lo registra. **Nota sua**: no.

### 5.9 Pulsanti: la pagina scelta, e il controllo della visibilità (II)

- **La scelta**: §4.5.
- **Il controllo**, sul server, **alla pubblicazione** (non a ogni salvataggio: una bozza può puntare a una pagina che uscirà dopo):
  1. **La convenzione**: un indirizzo del sito dentro un blocco si chiama `href`, a qualunque profondità, come un file si chiama
     `mediaId`. Il server non legge gli schemi; legge le chiavi `href` del JSON e i link del Markdown (`[testo](/indirizzo)`), che
     `BlockDocumentWalker` già sa trovare (`LinkPattern`).
  2. **Porta da qualche parte**: un indirizzo che comincia con `/` deve essere una schermata dell'applicazione o un contenuto
     pubblicato. La risposta la dà **un servizio solo** del nucleo, tolto da `MenuItemWriteDtoValidator` (`Screens`,
     `LeadsSomewhereThisSiteOwnsAsync`) e usato dal menu e dalla pubblicazione. Altrimenti: `errors.content.linkNowhere`, con il percorso
     del blocco.
  3. **Non porta a una pagina meno visibile**: la visibilità della pagina di destinazione non è più stretta di quella della riga che
     pubblica (una pagina per tutti non porta a una pagina dello staff): `errors.content.linkLessVisible`. È la stessa regola e lo stesso
     ordine delle visibilità del pubblico di un blocco (#414, `ContentPublishService`, `errors.content.position.blockAudience`).
- **Prima del clic**: i due problemi compaiono anche nell'elenco «che cosa manca per pubblicare» dell'editor (`PublishProblems`, la
  domanda che l'editor fa già al server prima della pubblicazione), accanto al blocco.
- **Gli indirizzi di fuori** non si controllano: non si sa che cosa c'è dall'altra parte.
- **È una modifica del nucleo del server**: una PR sua, dopo §4.5, con il test d'integrazione dei due rifiuti.
- **File del nucleo**: `Core/Content/ContentPublishService.cs`, `Core/Content/BlockDocumentWalker.cs`,
  `Core/Content/MenuItemWriteDtoValidator.cs` e il servizio nuovo accanto, i file di lingua del nucleo. **Nota sua**: no.

## 6. Che cosa non cambia

- **Il modello**: `Content → Section → Block`, niente blocchi dentro i blocchi, gli otto sfondi, le quattro larghezze della sezione.
- **Il server non legge le `props`**: gli unici due punti nuovi del server sono un campo della busta (`align`, §5.6) e una chiave per
  convenzione (`href`, §5.9), come `mediaId`.
- **L'elenco chiuso** resta di ventisei componenti: `MediaPicker`, `MarkdownContent` e `StaffSidebar` cambiano dentro.
- **La pubblicazione e l'approvazione**: si aggiungono due problemi (§5.9), nient'altro.
- **I blocchi già salvati** si disegnano come oggi (immagine, pulsanti); nessuna migrazione dei corpi.
- **Una migrazione sola**, additiva: la colonna `frequency` delle postazioni (§5.8).

## 7. L'ordine delle PR

Una PR per riga, ognuna con la sua versione come le altre (`Directory.Build.props`); l'ordine tiene insieme chi tocca gli stessi file.

| PR | Che cosa | File principali | Dopo |
|---|---|---|---|
| **3.0a** | Scarica per Claude, `about` di ogni blocco, il lettore degli indirizzi (§3, §4.5) | `features/content/`, i file di lingua, la rotta del menu | — |
| **3.0b** | Importa da Claude (§3) | `features/content/` | 3.0a |
| **3.1** | Sezioni visibili, «+ sezione», righe (§4.1) | `ContentRenderer`, `picking`, `BodyEditor`, `body` | 3.0b |
| **3.2** | Barra dello staff e pannelli (§4.2) | `StaffSidebar`, `StaffLayout`, `BodyEditor` | 3.1 |
| **3.3** | Voci ripetute chiuse, la data che si toglie (§4.3) | `SchemaForm` | — (in parallelo a 3.1) |
| **3.4** | Media: cerca, categoria, carica in cima (§4.4) | `MediaPicker`, `features/media/` | — (in parallelo) |
| **3.5** | La pagina scelta nei blocchi (§4.5) | `schema.ts`, `BlockProperties`, `schemas.ts` | 3.0a, 3.3 |
| **3.6** | Barra di formattazione e colori del testo (§4.6) | `MarkdownContent`, `LocaleFields`, `BlockDocumentWalker`, guida | 3.5 |
| **3.7** | Anteprima in una scheda (§4.7) | una rotta, `ContentEditor` | 3.2 |
| **3.8** | Altezza dell'interattivo (§5.5) | `EmbedShell.html`, `EmbedGuidelines.md` | — (in parallelo, piccola) |
| **3.9** | Allineamento nella busta (§5.6), nucleo del server | `envelope`, `BlockDocumentWalker`, `BlockProperties` | 3.5 |
| **3.10** | Immagine: percentuale, maniglia, tutto schermo (§5.3) | `schemas`, `blocks`, `ContentRenderer`, `picking` | 3.9 |
| **3.11** | Galleria (§5.2) | `schemas`, `blocks` | 3.10 (il `Dialog` comune) |
| **3.12** | Tabella (§5.1) | `schemas`, `blocks`, `BlockProperties`, `shared/files/`, `legFile` | 3.11 |
| **3.13** | Domande e risposte (§5.4), etichette dei dati (§5.7) | `schemas`, `blocks` | 3.12 |
| **3.14** | Frequenze dalle postazioni (§5.8), nucleo del server | `Core/Ivao/`, migrazione, `schemas` | 3.13 |
| **3.15** | Il controllo dei link alla pubblicazione (§5.9), nucleo del server | `ContentPublishService`, il servizio degli indirizzi | 3.5 |

Le PR dei blocchi (3.10–3.14) toccano tutte `blocks/schemas.ts` e `blocks/blocks.tsx`: vanno in fila. Quelle segnate «in parallelo»
toccano file loro e possono correre accanto, in sessioni diverse. Dopo 3.6, 3.10 e 3.12 la parte fissa delle istruzioni per Claude
(3.0a) dice le cose nuove (colori, immagine, tabella) nella stessa PR che le introduce.

## 8. Le domande per Carmine ⚖️

1. **La tabella: scrivere nella cella** (X). (a) Una **griglia** («Modifica nella griglia»), in un `Dialog` largo, una cella per casella
   nella lingua sullo schermo, che scrive le stesse `props` con lo stesso `onApplyProps`: un'eccezione dichiarata del form generato,
   solo per la tabella, come il campo del codice dell'interattivo (`carriesSource`) — **raccomandata**, perché nel pannello largo 19 rem
   una tabella di cinque colonne non si scrive; (b) resta il form generato, con incolla, importa e «crea N × M» soltanto.
2. **Domande e risposte: «più risposte per domanda»** (XV). (a) La risposta resta **un testo** Markdown, che può avere più paragrafi e
   un elenco — **raccomandata**, e la barra del §4.6 lo rende facile; (b) un elenco di risposte per ogni domanda, ognuna con il suo
   titolo.
3. **I colori del testo** (7). (a) **Blu, azzurro, verde acqua e rosso**, quelli che la misura di `e2e/contrast.spec.ts` dice leggibili
   su tutti gli sfondi nei due temi; l'**arancione resta fuori**, perché nessun suo tono arriva a 4,5 : 1 dove serve — **raccomandata**;
   (b) anche l'arancione, solo in grassetto e solo sugli sfondi chiari.
4. **Sottolineato e allineamento di un paragrafo** (7). (a) **Niente sottolineato** — sul sito una parola sottolineata si legge come un
   link (`MarkdownContent` sottolinea i link) — e l'allineamento è **del blocco intero** (§5.6) — **raccomandata**; (b) tutti e due nella
   barra, con la stessa sintassi del colore.
5. **Gli indirizzi di fuori nei pulsanti** (II). (a) Restano **liberi**: si sceglie una pagina dall'elenco o si scrive un indirizzo di
   fuori — **raccomandata**, un pulsante verso un modulo di iscrizione esterno è normale; (b) come il menu, solo pagine, schermate e link
   della libreria (`cms_links`), così ogni indirizzo che esce dal sito sta in una tabella.
6. **Le frequenze** (XX). (a) **Copiate** quando si sceglie la postazione, con l'editor che segnala una riga diversa da quella che IVAO
   dice adesso e «Riallinea» — **raccomandata**: un SOP si approva com'è scritto e cambia solo quando qualcuno lo ripubblica; (b) un
   **blocco di dati** nuovo che legge le frequenze vive dalle postazioni, con `frequencyTable` tolto dalla tavolozza come il
   coordinamento.
7. **L'ordine del §7**: va bene, o qualcosa va prima?

## 9. Le risposte di Carmine (31 ottobre 2026, in chat al master)

Con le sue parole: «1) ok 2) ok 3) Ok 4) Ok 5) Ok 6) Ok 7) va bene» — ogni domanda con l'opzione raccomandata.

1. **La tabella: (a)**, la griglia in un `Dialog` largo, eccezione dichiarata del form generato solo per la tabella (§5.1).
2. **Domande e risposte: (a)**, una risposta in un testo Markdown, senza un elenco di risposte (§5.4).
3. **I colori del testo: (a)**, blu, azzurro, verde acqua e rosso; l'arancione fuori (§4.6).
4. **Sottolineato e allineamento: (a)**, niente sottolineato, e l'allineamento del blocco intero (§4.6, §5.6).
5. **Gli indirizzi di fuori nei pulsanti: (a)**, restano liberi, con la scelta delle pagine come suggerimento (§4.5).
6. **Le frequenze: (a)**, copiate quando si sceglie la postazione, con la segnalazione e «Riallinea» (§5.8).
7. **L'ordine del §7: approvato.** Il codice comincia dopo il merge di #420 e di questo documento, una sessione per pezzo.

La risposta è stata data in chat al master e non in un commento: il documento è di una sessione di Carmine, non del collaboratore.

## Da portare nel piano

- **§13** e le fasi della nota del 22 ottobre (§5): la fase 3 in diciassette PR nell'ordine del §7, «importa da Claude» in testa,
  approvata da Carmine il 31 ottobre 2026 (§9); «scrivere nel blocco», la traduzione e le fasce restano nella fase 7.
- **§9.3** (il modello dei contenuti): le righe di una sezione sono le «sottosezioni» (correzione della nota del 22 ottobre, §4.A); il
  nome di una sezione è solo dell'editor; la busta di un blocco ha `align` accanto a `column` e `span`; un indirizzo del sito dentro un
  blocco si chiama `href`.
- **§16** (meccanismi generici): un lettore solo degli indirizzi del sito (menu, blocchi, import, fase 4) e un servizio solo del server
  che dice se un indirizzo porta da qualche parte; il controllo dei link alla pubblicazione con la regola della visibilità del pubblico
  di un blocco; il form generato con le voci chiuse, la data che si toglie, le annotazioni `entryTitle`, `address`, `markdown`,
  `atcPosition`; un blocco ridimensionabile (`resizable`), come le tessere della dashboard; la griglia della tabella come seconda
  eccezione dichiarata del form generato, accanto al codice dell'interattivo (risposta 1); un endpoint in più nel conteggio del
  §16.6.
- **§8.3** (i componenti): l'elenco chiuso resta di ventisei; cambiano `MediaPicker` (ricerca, pagine, caricamento in cima),
  `MarkdownContent` (GFM per il barrato, le direttive dei colori) e `StaffSidebar` (il provider nel layout).
- **`docs/UI-GUIDELINES.md`** (lo cambia la PR 3.6 per il testo e la 3.10 per l'immagine, come conseguenza delle risposte): la regola
  «no free colour» resta, e il **testo** prende un colore solo dall'insieme chiuso di quattro — blu, azzurro, verde acqua, rosso —
  misurati a 4,5 : 1 su ogni sfondo (risposta 1 della nota del 22 ottobre, risposta 3 di questo documento), l'arancione fuori;
  l'accento dei blocchi resta solo sui disegni; niente sottolineato (risposta 4); l'immagine a
  larghezza in percentuale dentro la sua colonna, fra 20 e 100, piena su una colonna stretta (risposta 4 della nota del 22 ottobre); l'allineamento comune dei
  blocchi; nell'editor le sezioni etichettate, «+ sezione» fra due sezioni e i pannelli che si allargano.
- **§10** (integrazioni con le API IVAO): la frequenza delle postazioni in `ref_ivao_atc_positions`.
