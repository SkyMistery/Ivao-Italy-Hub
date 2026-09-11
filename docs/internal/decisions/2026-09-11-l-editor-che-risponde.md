# L'editor che risponde: applicazione immediata, autosalvataggio, trascinamento dalla barra, annulla e ripeti, anteprima mobile vera

**Data:** 11 settembre 2026 — chiesto da Carmine, con davanti il page builder di va.ivao.aero
(`editor.php?id=4`) e il nostro editor sulla dashboard di HQ
**Stato:** **decisa da Carmine l'11 settembre 2026**, sulle tre domande in fondo: **G15 viene
prima di G14**; l'audit dell'autosalvataggio è la **(B)**, senza corpo; la pausa è di **10 secondi**.
Piano 0.60, piano di implementazione M1 2.20 (fase G15). **Costruita per intero lo stesso giorno**,
le tre sessioni in tre commit sulla PR #58; quanto è costata e cosa ha insegnato sta nella sezione
G15 del piano di implementazione.
**Come è stato guardato:** i due editor nel browser di Carmine, uno accanto all'altro, in sola
lettura sul loro; il nostro letto nel codice (`ContentEditor.tsx`, `useBodyHistory.ts`,
`PreviewFrame.tsx`, `BlockProperties.tsx`, `BlockPalette.tsx`, `SchemaForm.tsx`,
`HubSaveChangesInterceptor.cs`) e **misurato** nel browser dove serviva.

## Che cosa costa a IVAO

Quasi niente, ed è stato chiesto prima di tutto: l'editor gira nel browser di chi scrive. Il server
vede un `GET` all'apertura e un `PUT` a ogni salvataggio. Delle cinque cose qui sotto, **una sola**
tocca il server — l'autosalvataggio — ed è trattata con questo davanti. Il conto è nella sezione 2.

## 1. Le proprietà si applicano mentre si scrive

**Oggi.** `BlockProperties` e `SectionProperties` sono un `SchemaForm` con un pulsante «Apply to
the block» / «Apply to the section»: scrivi, premi, guardi, correggi, premi. Le due pastiglie della
`SectionFrame` (sfondo e colonne) si applicano già al clic, e sono l'unica parte dell'editor che
risponde subito.

**Domani.** Il form applica **a ogni modifica valida**, senza pulsante. Il renderer ridisegna il
blocco, e siccome è lo stesso del sito pubblico quello che si vede è quello che si pubblica.

**Che cosa cambia — (b), estensione del generatore di form.**

- `SchemaForm` riceve `onChange?: (values) => void`, chiamato con un ritardo di ~150 ms dopo
  l'ultimo tasto (`form.watch()`, che `useProposedSlugs` usa già) e **solo se
  `schema.safeParse(values)` passa**: un titolo obbligatorio svuotato a metà frase non svuota il
  blocco, il campo mostra il suo errore e il blocco resta com'era fino al prossimo valore valido.
  È la settima estensione del generatore, dopo le sei di G2 e G11a; i form delle entità non la
  usano e non cambiano.
- `BlockProperties` e `SectionProperties` passano da `onSubmit` a `onChange` e perdono il pulsante.
  `writtenValues` resta dov'è: quello che si scrive è quello che si applica.
- Il form dei **metadati** della pagina (indirizzo, visibilità, titolo, SEO) resta a `onSubmit`:
  è la riga, non il corpo, e lo salva «Save draft».
- ⚠️ **Senza il punto 4 questo rompe l'annulla**: ogni tasto è un `change(body)` e venti tasti
  svuotano una cronologia di venti passi. I due si fanno insieme.

**Test.** Vitest su `SchemaForm` (`onChange` non chiamato su valori non validi; chiamato una volta
per pausa e non per tasto). I test che oggi premono «Apply» (`e2e/full/round.spec.ts`,
`template.spec.ts`, `bench.ts`, `useBodyHistory.test.tsx`) cambiano gesto: scrivono e guardano.

**Costo server:** zero.

## 2. Autosalvataggio della bozza

**Oggi.** «Save draft» esplicito. Nessun avviso uscendo con modifiche non salvate: `useBlocker` e
`beforeunload` **non esistono** in `web/src`. Chi chiude la scheda perde il lavoro.

**Domani.** La bozza si salva da sola **dopo 10 secondi senza tasti** e **all'uscita** dalla pagina;
un indicatore accanto ai pulsanti dice «Salvato alle 14:32» / «Salvataggio…» / «Modifiche non
salvate». «Save draft» resta, per chi vuole premerlo. «Publish» prima svuota il salvataggio in
sospeso e poi pubblica: un gesto solo.

**Le regole, che sono il modo di non pagare.**

1. **Pausa lunga, non ogni tasto**: 10 s. Un'ora di lavoro vero sono 20–40 salvataggi, non 300.
2. **Solo se è cambiato qualcosa** dall'ultimo salvataggio: confronto in memoria del corpo
   serializzato e dei metadati, gratis.
3. **Solo se i metadati sono validi lato client**: un indirizzo vuoto non parte per il server a
   farsi rifiutare ogni 10 secondi; si riprova alla prossima pausa. I problemi di pubblicazione
   restano una cosa a parte (`PublishProblems`).
4. **Mai prima del primo salvataggio esplicito.** Una riga nuova la crea solo «Save draft»: un
   autosalvataggio che crea righe farebbe nascere pagine da chi ha aperto «Nuova pagina» ed è
   andato via.
5. **Un 409 ferma l'autosalvataggio** e lo dice: qualcun altro ha salvato quella riga, e il modo
   giusto di continuare è ricaricare, non sovrascrivere ogni 10 secondi.
6. **All'uscita**: `useBlocker` del router per la navigazione interna (salva e lascia andare, o
   chiede se il salvataggio non può partire), `beforeunload` per la scheda chiusa, che il browser
   permette solo di **avvisare**.

**Una trappola trovata leggendo il codice.** Il form dei metadati è rimontato a ogni salvataggio
(`key={content?.rowVersion}`, `ContentEditor.tsx:260`): serve perché la versione della riga è un
campo nascosto del form e una versione vecchia risponde 409. Con l'autosalvataggio quel rimontaggio
arriverebbe **mentre qualcuno scrive nel titolo**, e gli porterebbe via il cursore. Quindi la
versione **esce dal form**: la tiene l'editor, `onSave` la mette nel DTO, il `key=` sparisce.
È più giusto anche a prescindere: la versione è un fatto della riga, non un campo che si compila.

**Il server, e la sola decisione che tocca dati.** Ogni `PUT` oggi scrive `cms_contents` e **una
riga di audit con i campi cambiati prima e dopo** (`HubSaveChangesInterceptor.cs:341`): il corpo
due volte, fino a 1 MB l'uno, di solito 5–30 KB. La ricerca non è toccata: il walker gira alla
pubblicazione. Con le regole 1–3 il conto è ~1–2 MB di audit per ora di lavoro su una pagina, da
~12 senza. Due strade:

- **(A) Il server non cambia.** Si accettano 1–2 MB per ora-pagina. Semplice, tutto auditato.
- **(B) L'autosalvataggio si audita senza il corpo.** Il client manda un'intestazione
  `X-Hub-Autosave: 1`; il motore CRUD la legge e mette un flag nell'ambito della richiesta; il
  `CollectAudit` dell'interceptor scrive una riga con azione `autosaved`, l'elenco dei campi
  cambiati e **senza `BeforeJson`/`AfterJson`**. «Save draft» premuto a mano e «Publish» restano
  auditati per intero; la storia delle versioni pubblicate la tiene già `cms_content_versions`.
  Costo: ~200 byte a salvataggio. È un'estensione dell'unico interceptor, non un secondo modo di
  auditare.

**Raccomandazione: (B)**, perché il motivo per cui l'autosalvataggio era in discussione è proprio
il DB condiviso con vIPI, e perché della bozza intermedia di dieci secondi fa nessuno chiederà mai
la storia. ⚠️ Da decidere: è l'unica delle cinque cose che cambia che cosa il DB ricorda.

**Test.** Vitest sull'orologio finto (nessun `PUT` prima dei 10 s, uno solo dopo, nessuno se non è
cambiato niente, nessuno su riga nuova, stop al 409). Integrazione .NET: un `PUT` con l'intestazione
lascia una riga `autosaved` senza corpo, senza intestazione una `updated` con il corpo. E2E: si
scrive, si aspetta l'indicatore, si ricarica, il testo c'è.

## 3. Trascinare un componente dalla barra dentro la pagina

**Oggi.** Clic sulla barra → il blocco va nella colonna scelta, in fondo. Il punto 3 della nota del
10 settembre, lasciato aperto «per ultimo».

**Domani.** Si prende «Picture» dalla barra e la si lascia **fra due blocchi** o in una colonna
vuota. Il clic resta, ed è l'unica strada da tastiera.

**Che cosa cambia — (b), dnd-kit è già in casa.**

- `BlockPalette`: ogni voce è `useDraggable` con `data: { type }`. Un `DndContext` in
  `ContentEditor` avvolge barra e pagina **quando la pagina è nel mezzo**; l'outline ha il suo e i
  due non convivono mai (`preview ? … : …`), quindi non c'è un contesto dentro l'altro.
- ⚠️ **Il renderer non importa dnd-kit.** Il pubblico deve restare inerte per costruzione
  (`blocks/picking.ts`): la `Picking` porta un componente `DropZone` che l'editor fornisce
  (`useDroppable` dentro) e che `Column` disegna fra un blocco e l'altro e in fondo, **solo mentre un
  trascinamento è in corso**. Con `picking === null` non esiste; in una sezione che `accepts` rifiuta
  non esiste. Un componente passato dal contesto, non un hook: gli hook non attraversano un contesto.
- `addBlock` riceve un indice (`at`) oltre alla colonna; oggi aggiunge sempre in fondo.
- Lo stesso `DropZone` è, domani, il posto dove atterra un **blocco esistente** trascinato sulla
  pagina — la cosa che loro hanno e che qui non si fa ancora. Non in questo giro.

**Test.** Vitest su `addBlock` con `at`; su `Column` con un `DropZone` finto (disegnato solo durante
il trascinamento, mai con `picking === null` — il test che oggi asserisce la pagina inerte resta).
E2E: il rilascio fra due blocchi si misura con `dragTo` di Playwright, che è dove si sbaglia.

**Costo server:** zero.

## 4. Annulla e ripeti, anche da tastiera

**Oggi.** `useBodyHistory`: solo annulla, venti passi, nessuna scorciatoia — scelta scritta nel file:
«⌘Z dentro un campo vuol dire "annulla quello che ho scritto", e un gestore di pagina glielo
toglierebbe».

**Domani.** Annulla **e ripeti**, pulsanti accanto, `Ctrl/⌘+Z`, `Ctrl/⌘+Shift+Z` e `Ctrl+Y`.

**Che cosa cambia — (b).**

- `useBodyHistory` prende una pila `future`: `change` la svuota, `undo` ci mette il presente,
  `redo` lo riprende.
- **La scorciatoia rispetta l'obiezione del file**: il gestore sta sul documento e **non fa niente**
  se `event.target` è un `input`, `textarea`, `select` o `contenteditable`. Dentro un campo ⌘Z resta
  del browser; fuori — sulla pagina, sull'outline, sui pulsanti — è dell'editor.
- **Coalescenza**, ed è ciò che rende possibile il punto 1: `change(next, { coalesce: key })`. Due
  modifiche consecutive con la stessa chiave (`props:<idBlocco>`) **sostituiscono** la cima della
  pila invece di aggiungere; una chiave diversa, o una mossa strutturale, chiude la corsa. Una frase
  scritta in un blocco è **un** passo da annullare, non venti.
- Il tetto passa da 20 a **50**: un corpo sono 5–30 KB, cinquanta sono 1,5 MB di memoria nel
  browser, e con la coalescenza cinquanta passi sono cinquanta cose fatte.

**Test.** `useBodyHistory.test.tsx` cresce: ripeti, ripeti svuotato da una modifica, coalescenza per
chiave, chiave diversa che chiude la corsa. Vitest sul gestore: ⌘Z in un `textarea` non chiama
`undo`, ⌘Z su un pulsante sì.

**Costo server:** zero.

## 5. L'anteprima mobile è finta — la nostra come la loro

**Misurato**, l'11 settembre, sulla dashboard di HQ con l'anteprima a «Phone»: la regione è larga
**390 px**, la sezione «Tools» a due colonne disegna **due colonne da 167 px**
(`grid-template-columns: 167px 167px`). Su un telefono vero ne disegnerebbe una. Il motivo è una
riga: `md:grid-cols-2` guarda la **finestra** (1912 px), non l'anteprima. `PreviewFrame` fa
esattamente ciò che promette — un `max-width` sul renderer vero — e il renderer decide con
breakpoint di finestra. Il test e2e di `template.spec.ts` misura che la regione si stringa, non che
le colonne si accorpino: per questo è passato.

Da loro è uguale: l'anteprima «mobile» stringe la tela e la riga a due colonne resta a due colonne.
**Qui possiamo fare meglio di tutti e due**, e senza iframe.

**Che cosa cambia — (b), è CSS.** Tailwind 4 ha le **container query** di serie.

- La radice di `ContentRenderer` diventa un `@container`.
- Le **23** varianti di finestra dentro `web/src/blocks/` (11 in `ContentRenderer.tsx`, 12 in
  `blocks.tsx`: `sm:`, `md:`) diventano varianti di contenitore (`@sm:`, `@md:`), con le stesse soglie:
  `--container-sm: 40rem`, `--container-md: 48rem` in `@theme`, uguali ai `--breakpoint-*`. Nessun
  altro pezzo dell'app usa varianti `@…`, quindi ridefinirle non tocca nessuno. ⚠️ Da verificare
  nella build che Tailwind 4.3 legga `--container-*` come qui scritto.
- Fuori dall'editor cambia **quasi niente**: sul sito pubblico il contenitore è largo quanto la
  finestra meno la barra di scorrimento (≈15 px), quindi una pagina fra 768 e 783 px passa a una
  colonna quindici pixel prima. Sulla **dashboard di dipartimento**, che sta accanto alla barra
  laterale, le colonne si accorpano quando *lo spazio* è stretto e non quando lo è la finestra —
  che è più giusto di oggi.
- `PreviewFrame` non cambia una riga, e il suo avviso «non è un emulatore» resta vero.
- ⚠️ Quello che non si può promettere: un componente Atmosphere **dentro** un blocco, se decide con
  media query sue, continua a guardare la finestra. Da guardare a occhio sul set dei blocchi.

**Test.** L'e2e che misura la regione misura anche `gridTemplateColumns` a «Phone» = **una**
colonna: è la riga di test che avrebbe trovato il difetto. Vitest sul renderer: la radice porta
`@container`, nessuna classe `sm:`/`md:` sotto `blocks/` (un grep nel test, così la prossima
variante di finestra non torna).

**Costo server:** zero.

## 6. Due cose piccole viste per strada, da fare nello stesso giro — (a)

- Il percorso dell'editor della dashboard mostra **`{{department}}`** letterale: `common.json:239`
  ha `"dashboard.title": "{{department}}"` e chi disegna il percorso non lo interpola.
- La barra dei componenti sembrava **disabilitata** (grigio chiaro) mentre diceva «Adds to:
  Welcome». ⚠️ Costruendolo si è visto che **era** disabilitata, e a ragione: «Welcome» è una sezione
  che il template blocca, quindi non ci si aggiunge niente. A mentire era il suggerimento sopra la
  barra, che prometteva un'aggiunta impossibile. Corretto lì: con una sezione bloccata la barra dice
  «“Welcome” è fissata dal template: non ci si può aggiungere niente».

## Che cosa **non** si prende, di nuovo

La tela con le anteprime approssimate, la barra di testo ricco dentro il campo, l'icona digitata
come classe Font Awesome, il colore libero: già scartati (10 e 11 settembre), e il confronto di oggi
ha confermato il perché — le loro anteprime non sono la pagina, la nostra sì.

## Ordine proposto

Una fase, **G15 «l'editor che risponde»**, in quest'ordine, perché ognuno si appoggia al precedente:

1. **Annulla/ripeti con coalescenza** (4) — la rete sotto tutto il resto.
2. **Applicazione immediata** (1) — sicura solo con la 1 fatta.
3. **Anteprima mobile vera** (5) — CSS, indipendente, e ora che le proprietà si applicano guardando
   l'anteprima deve dire la verità.
4. **Autosalvataggio** (2) — l'unica con una decisione dentro.
5. **Trascinamento dalla barra** (3) — la più delicata da far funzionare bene, ultima come deciso il 10.
6. Le due piccole (6).

Stima: **una sessione per i punti 1–3**, una per il 4, una per il 5 — con `pnpm e2e:full` alla fine
di ognuna, perché quattro delle cinque cose cambiano il gesto che i test fanno.

## Le tre domande per Carmine

1. **Quando.** G14 (il documento operativo) era la fase dopo il tag; questa è nata mentre si
   collaudava l'editor. Prima l'una o l'altra? La proposta è **questa prima**: è ciò che si sta
   guardando adesso, e il documento operativo nascerà in un editor migliore.
2. **L'audit dell'autosalvataggio**: (A) tutto auditato, ~1–2 MB per ora-pagina, o (B) `autosaved`
   senza corpo, ~200 byte. **Raccomandata la (B).**
3. **La pausa**: 10 secondi è la proposta. Cinque salva più spesso e costa il doppio; venti fa
   perdere venti secondi di lavoro a chi chiude la scheda.
