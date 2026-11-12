# La galleria: che cosa il design non diceva

**Data:** 12 novembre 2026, PR 3.11 della fase 3 (#487; `12-design-editor-fase-3.md` §5.2, riga 3.11 del §7).
**Stato:** scelte della sessione di lavoro, dentro quello che il §5.2 ha già deciso (approvato da Carmine il 31 ottobre 2026, §9).
Nessuna cambia che cosa fa la funzione: dicono dove sta, come si scrive e come si legge. Nessuna si scosta dalla lettera del
§5.2, quindi nessuna chiede la risposta di Carmine; il revisore le legge come scelte di una sessione.
**Classificazione** (`CLAUDE.md` §5): (c), decisa dal design. Il §5.2 dice «nota sua: no»; questa nota c'è per i punti che il §5.2 non
scrive e che il codice ha dovuto scegliere, come la nota della 3.10.

## 1. `lightbox` resta il nome, e cambia che cosa apre

Il §5.2 dice «il clic apre l'immagine a tutto schermo». Fino alla 3.11 la casella `lightbox` apriva **il file** in una scheda nuova
(un link); da qui apre il tutto schermo della 3.10, con le frecce. La proprietà **non si rinomina** in `fullscreen` come quella
dell'immagine: un nome nuovo andrebbe spostato alla prima scrittura dell'editor (la regola della 3.9 e della 3.10), per una parola e
nient'altro. Con il nome che resta **nessun corpo salvato si sposta**: una galleria scritta prima ha `lightbox: true` e si apre a tutto
schermo, come il design vuole; con `lightbox: false` resta una galleria che non si apre. Cambia solo l'etichetta («Si apre a tutto
schermo con un clic», come quella dell'immagine).

Una galleria scritta prima non ha `layout` e si disegna com'era: la griglia, con le sue colonne. Le sue foto non hanno parole e restano
decorative, come erano. Nient'altro da spostare.

## 2. Il tutto schermo: un pezzo solo, con l'elenco delle foto

`FullScreenPicture` (in `blocks/blocks.tsx`, scritto nella 3.10 per questo) prende **l'elenco delle foto e quella cliccata**;
l'immagine gli dà un elenco di una. Con più di una:

- **Ogni foto della galleria è un pulsante suo**, con la sua finestra: chiudendo, il fuoco torna sulla foto che l'ha aperta, come deve
  fare una finestra di dialogo (APG). Una finestra sola con una scelta dall'esterno avrebbe perso il fuoco alla chiusura (il `Dialog`
  di Radix lo rimette sul suo pulsante, e non ne avrebbe avuto uno).
- **Le frecce stanno nella barra sopra la foto**, dal lato opposto alla «X», e non sulla foto: la foto ha tutto lo spazio che ha da
  sola, nessuna freccia le sta sopra. La finestra è larga almeno quanto la barra (15 rem), anche per una foto piccola.
- **Fanno il giro**: dopo l'ultima viene la prima, e prima della prima l'ultima, come in ogni visore di foto. **I tasti freccia**
  funzionano ovunque sia il fuoco nella finestra (anche sulla «X»).
- **Il nome della finestra** segue la foto mostrata (testo alternativo, se no la didascalia, se no «Galleria»), e il numero «2 di 4»
  si legge quando cambia (`aria-live`), perché un lettore di schermo non rilegge il nome di una finestra già aperta.
- Riaperta, si apre sempre sulla foto cliccata, non dove la si era lasciata.

## 3. Il carosello: i pezzi di Atmosphere, non il suo `Carousel` composto

Il `Carousel` composto di Atmosphere ha due difetti per un blocco: le frecce dicono **«Previous slide» / «Next slide» in inglese**,
scritti nella libreria (lo stesso caso della «Close» del suo `Dialog`, #224), e stanno **fuori** dal carosello (`-left-12`), oltre il
bordo della colonna e, su un telefono, dello schermo. Quindi la galleria monta i suoi pezzi (`CarouselRoot`, `CarouselContent`,
`CarouselItem`, `CarouselPrevious`, `CarouselNext`), come la 3.10 ha fatto con lo `Slider`:

- le frecce hanno il nome nella lingua sullo schermo (`aria-label`, che vince sul testo inglese nascosto dentro), stanno **dentro** il
  blocco, ai lati della foto, e hanno un disco del fondo della pagina (`bg-background/90`): quelle di Atmosphere sono trasparenti, e
  sopra una foto si leggevano quanto la foto lasciava;
- ogni foto è una «slide» con il nome di dove sta («1 di 3»), il carosello si chiama «Galleria»;
- ⚠️ **i tasti freccia li gestisce la galleria, non Atmosphere**: il suo gestore sta sul carosello in fase di cattura, e per React la
  finestra del tutto schermo aperta da una slide sta dentro il carosello anche se il browser la disegna altrove (un portale). Lasciati
  ad Atmosphere, i tasti della finestra giravano anche il carosello dietro. Il gestore della galleria prende solo i tasti nati dentro
  il carosello nel DOM; il test e2e lo controlla, e senza quel gestore fallisce;
- il carosello **non fa il giro** (il default di Atmosphere: le frecce si spengono ai capi), il tutto schermo sì: il carosello dice
  dove si è nella pagina, la finestra è un visore.

## 4. Le cornici e le didascalie

- Ogni modo ritaglia le foto in una cornice della stessa forma (`object-cover`), così le righe e le slide stanno allineate qualunque
  foto sia stata caricata: **quadrata** nella griglia (com'era), **16:9** nel carosello, **4:3** nella striscia. La foto intera è quella
  del tutto schermo.
- **La striscia**: ogni foto è larga `min(80%, 18rem)`, così la successiva mostra il bordo e si capisce che si scorre; `scroll-snap`
  la ferma su una foto. La striscia è una regione con un nome e prende il fuoco, così la tastiera la scorre anche quando le foto non si
  aprono (con `lightbox: false` dentro non c'è niente di focalizzabile).
- **La didascalia** di ogni foto sta sotto la foto, in ogni modo (`figcaption`), e sotto la foto nel tutto schermo.
- **Una galleria senza nessun file** non disegna niente, in nessun modo (prima: un elenco vuoto).

## 5. Le colonne restano nel pannello, con una riga che dice «solo per la griglia»

Il §5.2 dice «le colonne valgono solo per la griglia». Il renderer le legge solo nella griglia. Il form generato **non sa nascondere un
campo secondo un altro**, e insegnarglielo sarebbe un'estensione del nucleo che il design non chiede: il campo resta, con il
suggerimento `blocks.gallery.hints.columns` («Solo per la griglia.»). Se un giorno un secondo blocco avrà lo stesso bisogno, quella
sarà la sua nota.

## 6. Mentre si compone e sulla carta

- **Mentre si compone** il clic su una foto o su una freccia del carosello **sceglie il blocco** e non apre né gira niente, come ogni
  controllo dentro un blocco (la cattura di `BlockView`). L'anteprima in una scheda (§4.7) e il sito girano il carosello e aprono le foto.
- **In stampa** il carosello e la striscia sono la griglia, con le colonne del blocco (`PrintContext`, che oggi accendono i documenti e
  le notizie): ogni foto è stampata.

## 7. L'import da Claude e le istruzioni

- **La parte fissa delle istruzioni** dice i tre modi, che le colonne contano solo per la griglia, il testo alternativo e la didascalia
  di ogni foto, il tutto schermo con `lightbox`, e che una galleria della bozza senza `layout` è una griglia da lasciare così. Un test
  controlla che la parte fissa nomini ogni valore di `GALLERY_LAYOUTS`. L'esempio della galleria (quello che mostra la pagina dei
  componenti e che le istruzioni copiano) porta un testo alternativo e una didascalia.
- Niente da portare per l'import: le proprietà nuove sono facoltative, e un corpo vecchio è già valido.
- ⚠️ **L'impronta `rules` cambia**, perché cambia lo schema della galleria. **Da dire nelle note di rilascio**: un file per Claude
  scaricato prima di questa versione è rifiutato con `content.import.rulesChanged`.

## 8. La issue #378

Copre i primi due punti: **il testo alternativo** di ogni foto (mancava, e la galleria era decorativa per costruzione) e **i link senza
nome** (non ci sono più link: ogni foto che si apre è un pulsante «Mostra a tutto schermo: <testo alternativo>», o «Mostra l'immagine a
tutto schermo» se è decorativa). **Resta il terzo**: un PDF scelto come foto si disegna come un'immagine rotta. Non è della galleria:
il selettore offre i documenti a **ogni** campo di foto (immagine, galleria, copertina, schede…), e la risposta è un meccanismo del
selettore o della pubblicazione, fuori dal §5.2.

## 9. Le parole e i test

Entrano `blocks.gallery.fields.images.alt`, `.images.caption`, `.layout`, `blocks.gallery.hints.images.alt`, `.hints.columns`,
`blocks.gallery.options.layout.*`, `blocks.gallery.previous`, `.next` e `.position`; cambiano `blocks.gallery.about` e
`blocks.gallery.fields.lightbox`. Nel setup dei test di vitest uno stub di `IntersectionObserver`, accanto a quello di
`ResizeObserver`: il carosello di Atmosphere (Embla) lo chiede al montaggio e jsdom non l'ha.

## Da portare nel piano

- **Il design dell'editor, `12-design-editor-fase-3.md` §5.2**: `lightbox` resta il nome e apre il tutto schermo (nessun corpo si
  sposta); il carosello è fatto dei pezzi di Atmosphere, con le frecce dentro il blocco e i tasti della galleria; le colonne restano nel
  pannello con un suggerimento.
- **§9.3** (il modello dei contenuti): la galleria ha `layout` (`grid`, `carousel`, `strip`; di default `grid`) e, per ogni foto, `alt`
  e `caption` tradotti e facoltativi; `lightbox` apre il tutto schermo.
- **§16** (meccanismi generici): il tutto schermo dei blocchi (`FullScreenPicture`) prende un elenco di foto e quella da cui parte, con
  le frecce e i tasti quando sono più d'una; l'immagine gli dà un elenco di una.
- **§8.3** (i componenti): il carosello di Atmosphere è usato nei suoi pezzi, non composto, perché il composto scrive le frecce in
  inglese e fuori dal blocco; l'elenco resta di ventisei.
- **La nota `2026-10-30-una-pagina-scritta-con-claude`** e il §3 del design: l'impronta `rules` cambia con la 3.11.
- **La issue #378**: il punto del PDF scelto come foto resta aperto, per ogni campo di foto.
