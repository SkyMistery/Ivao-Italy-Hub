# Il selettore delle foto offre solo immagini

**Data:** 15 novembre 2026, issue #488, seconda parte (A) della risposta di Carmine. Ramo `fix/image-blocks-488-picker`, in coda
alla prima parte (B, `2026-11-15-un-documento-al-posto-di-una-foto.md`).
**Stato:** Decisa da Carmine il 15 novembre 2026, in chat alla sessione master, scritta sulla issue: «A+B, B per prima»
(https://github.com/SkyMistery/Ivao-Italy-Hub/issues/488#issuecomment-6895536882). Qui sotto ci sono le scelte di come farlo,
prese da questa sessione e da controllare.
**Classificazione** (`CLAUDE.md` §5): (b). Si estende il modo in cui la libreria si restringe per un campo, che c'è dalla #359
(`narrowLibrary`, `mediaCategory`), e la lista generata del server (`CrudOptions.CustomFilters`, come `archived`).

## Che cosa serve

Un campo che disegna una foto non deve offrire un documento: la parte B lo ferma alla pubblicazione, questa parte non lo offre
nemmeno. Tutti i campi `.meta({ media: true })` disegnano una foto (blocchi, sfondo di sezione, copertina, immagine social,
immagine di un award, banner di un evento, copertina e banner di un tour) **tranne uno**, il file di un documento
(`fileMediaId`), che si scarica.

## Com'è fatto

1. **Il server**: un filtro nuovo della libreria, `filter[picture]=true`, accanto ad `archived`. Tiene i file il cui tipo
   comincia con `image/` (`LIKE 'image/%'`). Usa il tipo e non la categoria, perché la categoria si cambia a mano. Un altro
   valore è un 400, come per ogni filtro sconosciuto.
2. **Lo schema**: **la foto è la regola, il documento l'eccezione.** Un campo media offre solo immagini, e
   `.meta({ media: true, mediaAnyFile: true })` offre tutto: oggi solo `fileMediaId`. Così un campo foto nuovo nasce giusto
   senza che nessuno ci pensi. Al contrario, la volta che qualcuno se ne dimentica tornerebbe il difetto.
3. **La libreria si restringe da sé**, come per la categoria: la query porta `meta.pictures` e `picturesOnly(query)` la usa
   (`shared/ui/narrowLibrary.ts`). Una libreria che non sa restringersi, quella d'esempio della galleria dei componenti, si
   offre intera. La pubblicazione ferma comunque il documento. `mediaPickerQuery` di una libreria di sole immagini:
   - tiene il filtro anche quando il campo chiede una categoria (i loghi sono immagini della categoria `Logo`) e quando la
     persona cerca o sceglie una categoria;
   - non offre la categoria «Document» nel suo menu;
   - offre solo immagini al caricamento (`accept="image/*"`, `meta.accept`).

## Che cosa non fa

- **Un file trascinato sulla griglia** non viene controllato nel browser: `accept` vale solo per la finestra di scelta. Se è un
  PDF il server lo prende, viene scelto, e la parte B ferma la pubblicazione con il campo nominato. Un rifiuto nel browser
  sarebbe una regola scritta due volte.
- **I file già scelti** non cambiano: li ferma la parte B, alla prossima pubblicazione.

## Da portare nel piano

- **§16** (meccanismi generici), al campo media del form generato: un campo media offre solo immagini, e
  `mediaAnyFile: true` (il file di un documento) offre tutti i file. La libreria si restringe con `filter[picture]=true`, per
  tipo e non per categoria.
- **§9.3** (modello dei contenuti), accanto alla parte B: una foto si sceglie fra le immagini, e un documento si sceglie solo
  dove si scarica.
