# La larghezza dell'immagine e il tutto schermo: che cosa il design non diceva

**Data:** 10 novembre 2026, PR 3.10 della fase 3 (`12-design-editor-fase-3.md` §5.3, riga 3.10 del §7).
**Stato:** **il punto 1 l'ha deciso Carmine**, perché si scosta dalla lettera del §5.3: «Sì, come la 3.9», in chat al master
l'11 novembre 2026, riportato sulla PR (https://github.com/SkyMistery/Ivao-Italy-Hub/pull/485#issuecomment-6797313455). La
sessione di lavoro l'aveva proposto per analogia con la sua decisione sulla 3.9 («Sì, accetto»,
https://github.com/SkyMistery/Ivao-Italy-Hub/pull/479#issuecomment-6769720262). I punti 2–8 sono scelte della sessione, dentro
quello che il §5.3 ha già deciso, e il revisore le ha lette senza rilievi. Nessuno cambia che cosa fa la funzione: dicono dove sta,
come si scrive e come si legge.
**Classificazione** (`CLAUDE.md` §5): (c), decisa dal design (approvato il 31 ottobre 2026, §9). Il §5.3 dice «nota sua: no»; questa
nota c'è per gli otto punti che il §5.3 non scrive e che il codice ha dovuto scegliere.

## 1. Il vecchio `width` si sposta alla prima scrittura, non all'apertura

Il §5.3 dice «il pannello lo traduce aprendo il blocco». Tradurlo vuol dire scrivere il corpo: aprire un'immagine diventerebbe una
modifica, con un passo di Annulla e un autosalvataggio, su una pagina che nessuno ha toccato. È lo stesso caso dell'`align` dei
pulsanti, e Carmine sulla 3.9 ha scelto la prima scrittura (nota `2026-11-10-l-allineamento-nella-busta`, punto 1).

Quindi, per analogia: aprendo il blocco il pannello **mostra** la larghezza vecchia (`third` → 33, `half` → 50, `full` → 100) nel suo
cursore, e **la prima cosa che l'editor scrive** su quel blocco — una didascalia, il cursore, la maniglia — porta con sé lo
spostamento: `widthPercent` scritto, `width` tolto (`sizedAt` in `blocks/size.ts`). Fino a quel momento il renderer legge il vecchio
quando il nuovo manca (`sizeOf`). Nessuna pagina cambia da sola.

**La risposta di Carmine**, in chat al master l'11 novembre 2026: «Sì, come la 3.9». Il codice resta così, e il §5.3 del design
cambia come il §5.6 («Da portare nel piano»). Il link: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/485#issuecomment-6797313455.

## 2. La larghezza la disegna l'involucro del renderer, non il blocco

Come l'allineamento (§5.6): un involucro solo in `ContentRenderer` (`BlockView`), per un blocco la cui registrazione dichiara
`resizable` — `{ prop, min, max, step, old }` in `BlockRegistration` (`shared/modules.ts`). Prende `widthPercent` % della colonna con
una variabile CSS (`w-(--block-width)`), `min-w-[min(100%,10rem)]`, e `@max-sm:w-full`: sotto i 24 rem di contenitore (la colonna è il
contenitore, `Column`) prende tutta la colonna. Sta **dentro** l'allineamento: è il pezzo più stretto che l'allineamento spinge di lato.
`ImageBlock` non ha più larghezza sua, e disegna la foto larga quanto gli è dato.

Le quattro funzioni stanno in `blocks/size.ts` e imitano quelle dell'allineamento in `envelope.ts`: `sizeOf` (`alignOf`),
`carriesOldSize` (`carriesOldAlign`), `sizedAt` (`placedAt`), e `snapped` (il passo più vicino, entro i limiti). Il vecchio lo dichiara
la registrazione come dato (`old: { prop: 'width', values: { third: 33, half: 50, full: 100 } }`), non come funzione: il renderer, la
maniglia, il pannello e l'import leggono la stessa tabella.

## 3. Lo schema accetta ogni intero da 20 a 100; i passi di 5 sono del gesto

`widthPercent: z.number().int().min(20).max(100).default(100)`, senza `multipleOf(5)`. Il §5.3 dice «a passi di 5», e la maniglia e il
cursore vanno a passi di 5; ma un terzo di colonna è **33**, e la prima scrittura di una foto vecchia scrive 33. Con `multipleOf(5)` lo
schema rifiuterebbe il valore del blocco stesso: il form dal vivo non manderebbe più niente (il suo `safeParse` fallisce) e il pannello
non potrebbe scrivere nulla su quella foto. `default(100)`: un'immagine nuova prende tutta la colonna, come prima.

## 4. Il campo del pannello è un cursore fuori dal form generato

Il §5.3 dice «la tastiera ha il campo del pannello». Il campo è uno `Slider` di Atmosphere — montato dai suoi pezzi (`SliderRoot`,
`SliderTrack`, `SliderRange`, `SliderThumb`), perché quello composto non dà un nome al cursore —, con il numero scritto accanto, e sta
**fuori** dal form, accanto all'allineamento, come la larghezza di una tessera della dashboard.

Il perché: il form dal vivo di un blocco tiene i valori con cui è nato (`useNewerRow` lascia stare un form dal vivo). La maniglia scrive
la stessa proprietà mentre il pannello è aperto, e un campo del form l'avrebbe riscritta indietro con la battuta successiva nella
didascalia: un trascinamento perso senza un segno. Così il form **porta** la proprietà senza disegnarla — `withoutDrawing`, un aiuto
nuovo del form generato (`shared/forms/schema.ts`), che mette `hidden` solo nello schema dato al form e non in quello del blocco, da cui
sono fatte le istruzioni per Claude — e ogni sua scrittura prende la larghezza del blocco com'è adesso (`BlockProperties`, `withWidth`).
Il test `WidthField.test.tsx` lo controlla, e una mutazione (la larghezza non imposta) lo fa fallire.

`Slider` è il primo uso nell'hub di quel componente di Atmosphere; non è un componente dell'elenco chiuso.

## 5. La maniglia: del puntatore, un gesto un passo, sul bordo che si muove

- **Solo del puntatore**: `aria-hidden` e nessun fuoco, perché la strada della tastiera è il cursore del pannello, con gli stessi
  limiti e passi; due controlli dello stesso valore sarebbero letti due volte. È uno `span` con `data-chrome`, così il clic in cattura
  del blocco la lascia passare, come la barra del blocco scelto.
- **Il bordo**: quello destro; su un blocco allineato a destra quello sinistro, che è il bordo che si muove. Al centro si muovono tutti e
  due, e la larghezza è il doppio della distanza dal centro.
- **Mentre si trascina** dice la percentuale; al rilascio inghiotte il clic che il rilascio fa, come la maniglia delle tessere.
- **Non c'è sotto i 24 rem**, dove il blocco prende tutta la colonna e un trascinamento non cambierebbe niente che si veda.
- **Un gesto, un passo di Annulla**: l'editor fonde le scritture con la chiave `resize:<blocco>:<gesto>`, e il gesto è un numero
  contato per la pagina (`newGesture` in `blocks/picking.ts`), non dalla maniglia, che rinasce ogni volta che il blocco è scelto di
  nuovo. Il cursore fa lo stesso: un trascinamento del pollice, o un tasto, è un gesto (`onValueCommit` lo chiude). ⚠️ Le tessere della
  dashboard usano `span:<blocco>`, quindi due trascinamenti di fila della stessa tessera sono un passo solo: non si tocca qui.
- **Su un corpo bloccato** non c'è maniglia; in una sezione bloccata da un template sì, perché la larghezza è una proprietà del blocco,
  e le proprietà restano aperte.

## 6. Il tutto schermo: il `Dialog` del nucleo con una misura `screen`

Il `Dialog` è quello del nucleo, dell'elenco chiuso, mai quello di Atmosphere (che scrive «Close»). Prende una seconda misura, `screen`:
largo quanto la foto (`w-max`) fino alla finestra meno un margine, con il titolo detto a un lettore di schermo e non disegnato sopra la
foto. L'elenco resta di ventisei: il componente cambia dentro, come `MediaPicker` nella 3.4. La 3.11 lo riusa per la galleria, e il
pezzo dei blocchi (`FullScreenPicture` in `blocks/blocks.tsx`) è scritto una volta.

- **Il nome della finestra**: il testo alternativo; se la foto è decorativa, la didascalia; se non c'è nemmeno quella, «Immagine».
- **Il pulsante** intorno alla foto della pagina si chiama «Mostra a tutto schermo: <testo alternativo>», perché il suo nome
  sostituirebbe quello della foto; senza testo alternativo, «Mostra l'immagine a tutto schermo».
- **Una foto più piccola della finestra resta alla sua misura**: allargata si sgranerebbe. Una più grande si ferma alla finestra, con
  la sua forma.
- **Mentre si compone** il clic sceglie il blocco (la cattura di `BlockView`) e non apre niente; **nell'anteprima in una scheda** (§4.7)
  si apre, perché è un pulsante e non un link, come le schede e le fisarmoniche.
- ⚠️ **Due trappole pagate**, scritte nel codice: `w-fit` su un riquadro fisso al centro della finestra ha metà finestra per stare, e la
  foto usciva grande la metà; e il foglio di Atmosphere ha un suo `.p-3` nello stesso strato `utilities`, letto dopo il nostro, che si
  riprendeva il margine sopra (`pt-12`): da qui `p-[0.75rem]`.

## 7. L'import da Claude e le istruzioni

- **La larghezza vecchia non si perde**, come l'allineamento della 3.9: su una foto vecchia che il file cambia, lo schema di oggi
  toglie `width` e il default darebbe tutta la colonna; se il file non scrive `widthPercent`, l'import porta la larghezza vecchia del
  blocco del file, o altrimenti di quello della bozza. Come per l'allineamento, solo il modo vecchio di dirla passa: un `widthPercent`
  della bozza che il file lascia cadere non torna.
- **La parte fissa delle istruzioni** dice che cos'è `widthPercent`, che su un telefono non conta, il tutto schermo con il suo nome, e
  che una foto della bozza con `width` si lascia com'è (§7: «dopo 3.10 la parte fissa dice l'immagine»).
- ⚠️ **L'impronta `rules` cambia**, perché cambia lo schema dell'immagine. **Da dire nelle note di rilascio**: un file per Claude
  scaricato prima di questa versione è rifiutato con `content.import.rulesChanged`.

## 8. Le parole

`blocks.image.fields.width` e `blocks.image.options.width` escono dai file di lingua, perché il form non disegna più quel campo; entrano
`blocks.image.fields.widthPercent` (l'etichetta del cursore), `blocks.image.fields.fullscreen`, `blocks.image.enlarge`,
`blocks.image.enlargeNamed` e `content.editor.resize` (il numero e la frase sotto il cursore).

## Da portare nel piano

- **Il design dell'editor, `12-design-editor-fase-3.md` §5.3** (Carmine, 11 novembre 2026, punto 1): il `width` delle foto vecchie si
  sposta in `widthPercent` **alla prima scrittura dell'editor sul blocco**, non «aprendo il blocco»; il campo della tastiera è un cursore
  fuori dal form generato; lo schema accetta ogni intero fra 20 e 100, e i passi di 5 sono della maniglia e del cursore.
- **§9.3** (il modello dei contenuti): l'immagine ha `widthPercent` (20–100, di default 100) e `fullscreen` (di default sì); il suo
  `width` vecchio è letto finché l'editor non scrive il blocco.
- **§16** (meccanismi generici): `BlockRegistration.resizable`, l'involucro del renderer che disegna la larghezza (dentro
  l'allineamento) e la maniglia nel contesto di composizione, con `blocks/size.ts`; un gesto un passo di Annulla (`newGesture`); il form
  generato porta un campo senza disegnarlo (`withoutDrawing`) quando la schermata lo scrive con un controllo suo.
- **§8.3** (i componenti): il `Dialog` del nucleo ha la misura `screen`; l'elenco resta di ventisei.
- **La nota `2026-10-30-una-pagina-scritta-con-claude`** e il §3 del design: l'import porta la larghezza vecchia di una foto come
  porta l'allineamento vecchio dei pulsanti; l'impronta `rules` cambia con la 3.10.
