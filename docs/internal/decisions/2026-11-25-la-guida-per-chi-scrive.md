# La guida «Come si fa una pagina»

- **Data**: 25 novembre 2026
- **Stato**: **decisa** per quello che è il §5.2 del design `13-design-struttura-del-sito.md`, approvato da Carmine in chat al master
  il 19 novembre 2026 («Sì, tutte e sette»; la risposta 6 è «una pagina seminata del WD, visibile allo staff, nominata dall'editor
  con un'impostazione come la pagina dello stile»),
  [riportato sulla #540](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/540#issuecomment-7011361289). Le scelte del §4 sono
  della sessione, tecniche o al seguito del design: la PR le elenca perché il revisore le veda. Nessuna chiede una risposta di
  Carmine prima del merge.
- **Chi**: una sessione di lavoro di Carmine (fase 4, PR 4.5, l'ultima della fase, dopo la 4.3 e la 4.4)
- **Classificazione** (`CLAUDE.md` §5): **(b)**. Estende due meccanismi che ci sono: le impostazioni dei contenuti
  (`ContentSettingsEndpoints`, nota `2026-11-04-la-pagina-dello-stile`) con una seconda chiave della stessa forma, e il seme delle
  pagine (`ContentSeeder`) con un campo che sceglie la pagina seminata per un'impostazione. Nessuna tabella, nessuna migrazione,
  nessun endpoint in più (gli stessi due verbi crescono di un campo), nessun componente nuovo.

## 1. Che cosa serviva

Chi scrive una pagina non aveva un posto dove leggere come si fa: dove va, come nasce l'indirizzo, chi la approva, quale blocco per
che cosa. La fase 4 ha cambiato tre di queste cose (l'albero, l'indirizzo dal titolo, la mappa), e il design la chiude con una guida
che le racconta **come sono**, nominata dall'editor.

## 2. Che cosa si fa

- **L'impostazione `content.guidePage`** in `hub_division_settings`, con il valore `{ "contentId": <id> }` come `content.stylePage`.
  `ContentSettingsEndpoints` tiene le due impostazioni con una regola sola (`PageSetting`): una pagina — non un modello, non una
  news — che chi sceglie legge online, e non di un solo dipartimento, perché chi la apre può essere di un dipartimento qualunque.
  - `GET /api/content/settings` risponde anche `guidePageId` e `guidePagePath`, il percorso letto con la visibilità di chi chiede
    (nullo se non la può aprire);
  - `PUT` prende anche `guidePageId`, con i rifiuti `errors.content.settings.guidePageNotFound` e `…guidePageTooNarrow` sul campo
    `guidePageId`; lo scrive chi lo scriveva già (`Content.ManageTemplates` e tutti i dipartimenti).
- **Il seme** `seed/content-pages/how-to-make-a-page.json`: `/how-to-make-a-page`, del WD (`SiteOwnership`), **`Staff`**, in
  cima, dal template `section-page`, con la testata, sette coppie titolo e testo nella sezione `body` e la sezione `links` del
  template; i testi in `seed.pages.howTo.*` di `locales/{it,en}/seed.json`. Nessuna voce di menu: la guida si apre dall'editor.
- **Il seme sceglie la pagina** (`"setting": "content.guidePage"` nel file; `ContentSeeder.ChooseAsync`,
  `ContentSettingsEndpoints.ChooseIfNoneAsync`): quando il seme crea la pagina e nessuna riga di `content.guidePage` c'è già, la
  sceglie, con VID 0 come ogni riga che l'installazione scrive per sé.
- **Il link «Come si fa una pagina»** nelle azioni in testa al form di una riga (`ContentFormScreen`, un `Button` di Atmosphere
  con `asChild` intorno a un `<a>`, icona `BookOpen` di `lucide-react`), in una scheda nuova: lasciare l'editor per la guida
  lascerebbe anche la bozza. C'è solo quando il server dà il percorso, cioè quando chi scrive può aprire la guida.
- **Il pannello «Impostazioni»** ha un secondo campo, «Pagina della guida «Come si fa una pagina»», con le stesse scelte del
  primo (le pagine pubblicate che tutto lo staff legge).
- **Le parole** in `locales/{it,en}/common.json` (`content.settings.fields.guidePageId`, `content.settings.hints.guidePageId`,
  `content.guide.*`) e in `errors.json`.

### Che cosa dice la guida, e dove l'ho controllato

Il testo racconta il codice di oggi, letto nel codice e non solo nelle note (le etichette sono quelle dei file di lingua):

1. **Dove va una pagina**: l'albero di `/staff/content` e «Nuova pagina qui»; tre livelli (`ContentAddresses.MaxDepth`); in cima
   solo chi ha `Content.Approve` (`TopLevelNotAllowed`); le schermate segnate e spente (`parentChoices`); le parole riservate
   (`ReservedSegments`); una pagina con figlie non si cancella (`DeletionProblemAsync`); una pagina online la sposta chi la può
   approvare (`PositionNotTheirsAsync`), le figlie la seguono e il vecchio indirizzo porta al nuovo (`PreviousPathsJson`, il
   rimando del router).
2. **L'indirizzo** come l'ha fatto la 4.3: «Dove sarà», nasce dal titolo nella lingua principale, si fissa al primo salvataggio,
   «Usa …» per un indirizzo preso, il campo che si apre per un titolo che non dà un indirizzo, lo cambia solo chi approva, anche per
   news e documenti.
3. **Dalla bozza al sito**: «Salva bozza» e il salvataggio da solo dopo una pausa (`useAutosave`), «Apri l'anteprima», «Segna
   pronta» con nota e voce di menu, «In attesa di approvazione» e «Ritira dall'approvazione», gli stessi controlli della
   pubblicazione (`ContentReviewService.ReadyAsync`), «Approva» con la correzione dell'indirizzo e «Rimanda indietro», l'avviso a
   chi approva, la versione pubblicata che resta finché qualcuno non ripubblica, «Versioni»; news e documenti senza approvazione
   (`contentApproval: ["Page"]`), pubblicati da coordinatore e assistente (`Content.Publish`).
4. **Sezioni e righe**: otto sfondi, quattro spaziature, quattro larghezze, le cinque disposizioni (`envelope.ts`), la riga come
   sezione dentro la sezione che nasce senza sfondo e spaziatura (`body.ts`), nessun blocco che contiene blocchi, le sezioni fissate
   dal template.
5. **Quale blocco per che cosa**: un paragrafo per cassetto della tavolozza (Contenuto, Impaginazione, Interattivi, Struttura, Dati;
   `BLOCK_GROUPS`, `core.ts`), con i nomi dei blocchi; la galleria `/staff/admin/ui-kit`, detta «per chi ha l'accesso
   all'amministrazione» perché la rotta chiede `Admin.Access`.
6. **Le foto**: la libreria del dipartimento, il testo alternativo del file, un file caricato è `Staff` finché qualcuno non lo rende
   pubblico (`MediaEndpoints`), la foto lasciata da scegliere (`RequiredMedia`), un file mostrato non si cancella.
7. **Scrivere con Claude**: «Scarica per Claude» e «Importa da Claude», che cosa c'è nel file, che l'import mette nella bozza e
   «Annulla» lo toglie, e quando rifiuta (un'altra pagina, un'altra bozza, le regole cambiate: `content.import.*`).

La galleria e la mappa del sito della 4.4 sono nominate; la guida non elenca a mano gli indirizzi della divisione.

## 3. Che cosa cambia per chi usa l'hub

- Ogni installazione riceve al primo avvio dopo il rilascio la pagina **`/how-to-make-a-page`**, dello staff, scelta come guida se
  nessuno ne ha scelta un'altra; dopo è del WD, che la riscrive dall'editor come ogni altra pagina.
- Il form di una pagina, una news, un documento (e di un modello o di una dashboard, che stanno nello stesso schermo) ha in testa il
  link «Come si fa una pagina».
- Il pannello «Impostazioni» di `/staff/content` ha il campo della guida accanto a quello dello stile.

## 4. Le scelte che il design non scriveva

1. **Il `PUT` salva le impostazioni intere**: un campo nullo toglie la scelta, come un campo svuotato di un form. Il pannello manda
   sempre tutti e due. Un'alternativa era «un campo assente lascia com'è», che in C# vuol dire distinguere assente da nullo su ogni
   campo: più codice per un solo chiamante, il pannello. ⚠️ Chi chiama il `PUT` a mano con il solo `stylePageId` toglie la guida (lo
   fanno i test di `ContentSettingsTests` che c'erano, e il test nuovo del seme si rimette in pari da solo).
2. **Una riga cambia solo se il valore cambia**: il pannello salva tutte e due le impostazioni, e quella lasciata com'era non scrive
   chi l'ha cambiata né lascia una riga nell'audit.
3. **Il seme dice quale impostazione sceglie** (`"setting"` nel file), invece di un nome di pagina scritto nel seeder. Il seeder
   accetta solo le chiavi di `ContentSettingsEndpoints.PageKeys` (lo stile e la guida) e su un seme per dipartimento non sceglie
   niente; una chiave diversa la dice nel log e la salta.
4. **Il seme sceglie solo all'avvio che crea la pagina, e solo se non c'è una riga dell'impostazione.** Una riga c'è anche quando
   qualcuno ha scelto una pagina che poi è stata cancellata: resta sua, e la guida seminata dopo non la prende. Una pagina scritta a
   mano a `/how-to-make-a-page` prima del rilascio non è scelta per nessuno (la regola dei semi, `IsWrittenByHandAsync`).
5. **Il link sta in `ContentFormScreen` e non in `ContentEditor`.** Il design nomina tutti e due; il form «Nuova pagina» e l'editor
   sono lo stesso schermo (`id = new` o un numero), e le azioni in testa (`PageShell.actions`) li coprono insieme. Vale per ogni tipo
   che quello schermo apre: la guida parla anche dei blocchi e delle foto, che news, documenti e dashboard usano.
6. **La sezione del testo è larga «Stretta»** (`narrow`), per righe di prosa che si leggono; il template ha `default`, e
   `templateDiff` non confronta la larghezza (lo tiene `seeds.test.ts`).
7. **Le parole della guida dicono «la direzione e il team web»** per chi approva, come già dice l'errore
   `errors.content.address.topLevel`, e non le sigle delle posizioni.

## 5. I test

- **Nuovi**: `ContentSettingsTests.TheGuideIsChosenByTheSameRuleAndReadByWhoeverWrites` (un coordinatore di un altro dipartimento
  legge la guida scelta; una pagina di un dipartimento è rifiutata sul campo `guidePageId`; la guida non tocca lo stile) e
  `TheSeedMakesTheGuideForTheStaffAndChoosesItOnlyWhenNobodyChoseAnother` (cancellata la guida e dimenticate le chiavi, il seme la
  rifà del WD, `Staff`, pubblicata, e la sceglie; un membro non la legge; con un'altra pagina già scelta il seme rifà la guida e lascia
  la scelta; alla fine la rimette come una installazione nuova, per chi viene dopo nello stesso database). L'e2e
  `content-settings.spec.ts`: il campo della guida e quello che manda, il link nel form con `target="_blank"`, e nessun link senza
  percorso. L'e2e `site-guide.spec.ts`: la guida disegnata dal seme vero, risolto dai file di lingua come fa il seeder, in inglese e
  in italiano, con i sette titoli, gli elenchi e i link al back office, e le due schermate.
- **Cambiati** (uno per uno):
  - `web/src/features/content/contentSettings.test.ts`, «the form says "none" when no page is chosen…»: il DTO ha due campi in
    più e il form un campo in più; il test li aggiunge e prova la guida da sola. `NO_STYLE_PAGE` si chiama ora `NO_PAGE`, perché vale
    per tutte e due le scelte.
  - `web/e2e/content-settings.spec.ts`, «whoever cures the templates chooses the page of the style…»: il corpo mandato è ora
    `{ stylePageId: 9, guidePageId: null }` (§4, scelta 1) e non `{ stylePageId: 9 }`.

## 6. Un limite che resta

⚠️ **La guida racconta l'editor della 0.27.0** (design §5.2, ultima riga). È una pagina del database, non un file: una PR che dopo
cambia quello che racconta non la aggiorna, e le installazioni che ci sono non la ricevono di nuovo. La tiene viva il WD; le note di
rilascio dicono quando una fase cambia qualcosa che la guida racconta. (Già oggi `docs/UI-GUIDELINES.md` dice che le sezioni
scendono di tre livelli «per il server», mentre il server ne accetta quattro e l'editor si ferma a tre: la guida non ne parla.)

## Da portare nel piano

- **§16** (meccanismi generici), le impostazioni dei contenuti: una seconda chiave, `content.guidePage`, con la stessa regola della
  pagina dello stile (una pagina pubblicata che tutto lo staff legge, riletta con i diritti di chi chiede); il `PUT` salva le
  impostazioni intere (§4, scelta 1); un seme di pagina può scegliere la pagina che crea per una di queste impostazioni (`"setting"`),
  solo se nessuno ne ha scelta un'altra (§4, scelte 3 e 4).
- **§16.6**: nessun endpoint in più; i due verbi di `/api/content/settings` crescono di un campo.
- **§9.3** (il modello dei contenuti), con il design della fase 4: la guida «Come si fa una pagina» è un seme del WD, dello staff, in
  cima, scelta come guida; il form di ogni riga la nomina con un link quando chi scrive la può aprire.
- **Le note di rilascio della 0.27.0**, una riga: ogni installazione riceve al primo avvio la pagina dello staff
  `/how-to-make-a-page`, scelta come guida nelle impostazioni dei contenuti se nessuno ne ha scelta un'altra; il WD la riscrive come
  ogni altra pagina, e la tiene in pari quando l'editor cambia.
- **Il design 13 §7**: la fase 4 è chiusa con la 4.5.
