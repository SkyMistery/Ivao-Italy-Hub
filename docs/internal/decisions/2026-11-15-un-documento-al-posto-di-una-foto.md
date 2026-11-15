# Un documento al posto di una foto

**Data:** 15 novembre 2026, issue #488 (il terzo punto di #378, lasciato fuori apposta dalla 3.11: nota
`2026-11-12-la-galleria.md` §8). Ramo `fix/image-blocks-488`.
**Stato:** Decisa da Carmine il 15 novembre 2026, in chat alla sessione master, scritta sulla issue: «A+B, B per prima»
(https://github.com/SkyMistery/Ivao-Italy-Hub/issues/488#issuecomment-6895536882).
**Classificazione** (`CLAUDE.md` §5): (b). Si estendono due meccanismi che ci sono già, i controlli della pubblicazione
(`ContentPublishService`) e il selettore della libreria (`MediaPicker` con `narrowLibrary`, #359). Nessun meccanismo nuovo.

## Che cosa serve

La libreria prende immagini (png, jpeg, webp, gif) e PDF (`MediaOptions.AllowedContentTypes`). Il selettore di ogni campo
`.meta({ media: true })` offre tutti i file. Così un PDF scelto per un blocco Immagine, una voce di Galleria o lo sfondo di una
sezione esce come `<img>` rotto, e la pubblicazione non dice niente.

## I fatti, misurati

- Nel corpo di una pagina **ogni** `mediaId` è una foto: lo sfondo di una sezione e i sei campi di blocco (`hero`, `image`,
  `cardGrid.cards[]`, `gallery.images[]`, `logoGrid.items[]`, `testimonial`). I blocchi dei moduli non nominano file.
- Fuori dal corpo, due colonne del contenuto nominano un file: la copertina di una notizia (`coverMediaId`, una foto) e il file
  di un documento (`fileMediaId`, da scaricare, quindi un file qualunque). È l'**unico** campo che offre un documento.
- La **categoria** di un file si cambia a mano (`MediaWriteDto.Category`), quindi «categoria diversa da Document» non dice che
  è un'immagine. Lo dice il **tipo** (`ContentType`), che il server ricava dai byte e non da quello che dichiara il browser.

## Le alternative (dalla issue)

| | Che cosa | Pro | Contro |
|---|---|---|---|
| A | Il selettore di un campo foto offre solo immagini | Non si offre la scelta sbagliata | Non ferma i PDF già scelti, né un corpo importato |
| **B** | La pubblicazione rifiuta un file del corpo, o la copertina, che non è `image/*`. Il file di un documento resta libero | È la regola: niente immagine rotta online. Vale per i corpi già salvati, per l'import e per chi approva | Si vede solo quando si chiede di pubblicare |
| **A+B** | Tutte e due | | Due PR |

## La decisione: A+B, prima B

Due PR in fila sulla stessa issue. Questa nota è della prima (B). La seconda (A) ha la sua nota per il dato del campo e il
filtro della libreria.

**B, com'è fatta:**

- `ContentPublishService.FilesThatAreNotPicturesAsync`, uno dei controlli di `ProblemsAsync`. Accanto ci sono la foto da scegliere
  (3.15) e i link che non arrivano (3.13). Vale quindi dappertutto: «Pubblica», «Pronta per l'approvazione», l'approvazione, e
  l'elenco «Che cosa manca per poterla pubblicare».
- Guarda i file che il corpo nomina (`BlockDocumentWalker.MediaReferences`, la convenzione `mediaId` che c'è già) e la copertina.
  Rifiuta quelli il cui `ContentType` non comincia con `image/`, con la chiave nuova `errors.content.mediaNotPicture`, sul
  percorso del campo.
- **Il server non legge gli schemi.** La regola è «un file nominato nel corpo si disegna come foto». È vera oggi per tutti i
  blocchi, e non chiede niente di `props` oltre a quello che `MediaReferences` legge già.
- Un file che non c'è più non si guarda: lo dice già `mediaMissing`, e un problema per campo basta.
- **Non si ferma**: il salvataggio della bozza, e il cambio di visibilità senza ripubblicare (`NewReadersProblemsAsync` non
  chiede questo controllo, come per la 3.15).
- ⚠️ **Una pagina già online con un PDF al posto di una foto** resta com'è. Si ferma la sua versione successiva, e l'elenco dice
  quale campo. Da scrivere nelle note di rilascio.
- ⚠️ **Un blocco futuro che offre un documento da scaricare dentro il corpo** rompe la convenzione. Dovrà dichiararlo, e il
  modo (un nome di campo diverso da `mediaId`, o un fatto del tipo di blocco) sarà una domanda nuova.
- **Lasciata fuori**: l'immagine per i social (`seo.ogImageMediaId`). Non è nella domanda della issue e non si disegna sulla
  pagina.

## Da portare nel piano

- **§9.3** (modello dei contenuti), dove dice che cosa ferma la pubblicazione: un file che il corpo nomina, e la copertina di una
  notizia, devono essere immagini (`errors.content.mediaNotPicture`). Il file di un documento è libero.
- **§16** (meccanismi generici), alla convenzione `mediaId` del corpo: ogni `mediaId` di un corpo è una foto, e un blocco che
  vorrà nominare un documento da scaricare deve chiederlo.
- **Le note di rilascio**: una pagina già online con un documento al posto di una foto resta online com'è. Il suo testo nuovo non
  esce finché il file non è cambiato.
