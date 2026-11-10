# L'allineamento nella busta: che cosa il design non diceva

**Data:** 10 novembre 2026, PR 3.9 della fase 3 (`12-design-editor-fase-3.md` §5.6, riga 3.9 del §7).
**Stato:** scelte della sessione di lavoro, dentro quello che il §5.6 ha già deciso; **nessuna è di Carmine**, e il revisore le
controlla una per una. Nessuna cambia che cosa fa la funzione: dicono quando, dove e come.
**Classificazione** (`CLAUDE.md` §5): (c), decisa dal design (approvato il 31 ottobre 2026, §9). Il §5.6 dice «nota sua: no»; questa
nota c'è per i sei punti che il §5.6 non scrive e che il codice ha dovuto scegliere.

## 1. Il pannello sposta l'`align` dei pulsanti alla prima scrittura, non all'apertura

Il §5.6 dice «il pannello lo sposta aprendo il blocco». Spostarlo vuol dire scrivere il corpo: aprire un blocco diventerebbe una
modifica, con un passo di Annulla e un autosalvataggio, su una pagina che nessuno ha toccato. Il principio 4 del §2 («un corpo salvato
non si riscrive») e il §5.3 («nessuna pagina cambia da sola») dicono il contrario.

Quindi: aprendo il blocco il pannello **mostra** l'allineamento vecchio come quello della busta (le tre icone, quella giusta premuta), e
**la prima cosa che il pannello scrive** su quel blocco — un'etichetta, un indirizzo, l'allineamento stesso — porta con sé lo
spostamento: `align` sulla busta, `props.align` tolto (`placedAt` in `blocks/envelope.ts`). Fino a quel momento il renderer legge il
vecchio quando la busta non ha niente (`alignOf`). La sola scrittura che non si fonde con le battute dopo è la prima.

## 2. Come lo disegna l'involucro

Il §5.6 dice «il testo e i pezzi stretti». Un involucro solo, in `ContentRenderer` (`BlockView`), e solo per un blocco `alignable` che
non sta a sinistra: a sinistra non c'è nessun involucro, e una pagina che non sceglie niente si disegna com'era.

- **Il testo**: `text-align`.
- **Un pezzo più stretto della colonna** (un'immagine di un terzo o di metà): i margini automatici del figlio diretto.
- **Una riga disposta con `flex`**, che né il testo né i margini muovono: la riga lo dice con `data-align-row`, e l'involucro la
  giustifica. Oggi sono due: i pulsanti di un gruppo, e la riga dell'autore di una testimonianza (foto, nome, ruolo), che altrimenti
  restava a sinistra sotto una citazione al centro.
- **Gli elenchi**: il pallino va accanto alle parole (`list-inside`). Fuori, restava a sinistra mentre le parole andavano al centro o a
  destra: visto nel browser con un testo a destra.
- **Il callout**: si allinea il suo testo; l'icona resta nell'angolo, dove la mette Atmosphere.

La regola sta nella registrazione (`BlockRegistration.alignable`, `shared/modules.ts`): un blocco allineabile **non** dichiara un
`align` fra le sue proprietà, e `blocks/align.test.tsx` lo controlla su ogni blocco registrato, moduli compresi. È anche quello che
rende sicuro leggere `props.align` di un blocco allineabile come quello vecchio.

## 3. Il server rifiuta tutto ciò che non è una delle tre parole

`CheckLayout` e `CheckBackground` rifiutano solo una stringa sconosciuta, e lasciano passare un numero. Per `align` il controllo
rifiuta qualsiasi valore che non sia `null` o una delle tre parole (un numero, un oggetto, `"Center"`), come già fa `CheckSpan` per un
numero sbagliato: il browser legge la busta con lo stesso insieme chiuso (`z.enum`), e un corpo che non sa leggere lo disegna come una
pagina vuota. Il server non sa quali blocchi sono allineabili (è la registrazione del browser) e accetta `align` su ogni blocco; il
renderer lo ignora dove non vuol dire niente.

## 4. L'import da Claude e le istruzioni

Il §5.6 non nomina l'import, ma il principio 5 del §2 sì: ogni proprietà nuova arriva a Claude. `align` arriva da solo nel JSON Schema
della busta (`envelopeSchema`), e quattro cose sono scritte a mano:

- **le istruzioni** dicono, sotto ogni blocco allineabile, che si può allineare; la parte fissa dice che `align` è `center` o `right`,
  che per la sinistra si lascia fuori, e che sugli altri blocchi non si scrive;
- **`align` è del file**, come `column`, e non resta quello della bozza come `renderMode`, `span` e `frozen`: è dove sta il blocco, non
  una scelta dell'editor che Claude non vede. Ma solo su un blocco allineabile: su un altro l'import tiene quello della bozza (nessuno);
- **l'allineamento vecchio non si perde**: su un gruppo di pulsanti vecchio che il file cambia, lo schema di oggi toglie `props.align`,
  e l'import lo porta sulla busta — anche quando il file lo ha lasciato cadere, come il JSON Schema del blocco ormai chiede — salvo che il
  file ne scelga uno sulla busta. Un gruppo di pulsanti che il file non tocca resta com'era scritto;
- **l'impronta `rules`** conta anche `alignable` di ogni blocco, oltre alla busta e allo schema di `buttonGroup` che cambiano.
  ⚠️ **Da dire nelle note di rilascio**: un file per Claude scaricato prima di questa versione è rifiutato con
  `content.import.rulesChanged`.

## 5. Una sezione bloccata si può ancora allineare

Una sezione bloccata da un template lascia cambiare le proprietà dei suoi blocchi e nient'altro (`templateRules.ts`, `shapeOf` di
`claudeFile.ts`, che confronta disposizione, sfondo, blocchi e colonne). L'allineamento **non** entra in quel confronto: fino a ieri
l'allineamento dei pulsanti era una proprietà, e spostarlo nella busta non deve togliere a chi scrive la pagina una cosa che poteva fare.
Il pannello lo offre anche in una sezione bloccata.

## 6. Le parole dei pulsanti

`blocks.buttonGroup.fields.align` e `blocks.buttonGroup.options.align` escono dai file di lingua, perché il form non disegna più quel
campo; le parole nuove sono `content.editor.align` (il pannello, accanto a «Sezione») ed `errors.body.alignUnknown`. Le icone sono
`TextAlignStart`, `TextAlignCenter` e `TextAlignEnd` di `lucide-react`, e il gruppo è quello dei pulsanti dell'anteprima
(`PreviewFrame`, `aria-pressed`), non un componente nuovo.

## Da portare nel piano

- **§9.3** (il modello dei contenuti): la busta di un blocco ha `align` (`left | center | right`, assente = sinistra) accanto a
  `column` e `span`, controllato dal server come `span` (`errors.body.alignUnknown`); vale per un blocco che si dichiara `alignable`, e
  l'`align` di `buttonGroup` è passato lì. (È già nel «Da portare nel piano» del design; qui le precisazioni dei punti 1 e 3.)
- **§16** (meccanismi generici): `BlockRegistration.alignable`; l'involucro del renderer disegna l'allineamento, e una riga `flex` di
  un blocco lo segue con `data-align-row`; un blocco allineabile non ha un `align` suo. Un corpo vecchio si legge com'è e si sposta
  alla prima scrittura del pannello, mai all'apertura.
- **La nota `2026-10-30-una-pagina-scritta-con-claude`** e il §3 del design: `align` è una scelta del file, come `column`, solo sui
  blocchi allineabili; l'impronta `rules` conta `alignable`.
