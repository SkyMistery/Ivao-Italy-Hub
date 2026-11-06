# Le fessure «+ sezione» e l'anteprima vuota: si disegnano a caratteri finiti

**Data:** 5 novembre 2026 — PR 3.1 della fase 3 dell'editor, sessione di lavoro di Carmine, ramo `feat/editor-sections`
**Stato:** **Correzione**, non una decisione di prodotto: il difetto l'ha aperto questa stessa PR, prima che entrasse in `main`.
Scritta perché è la stessa trappola di `2026-10-06-l-anteprima-vuota-dell-editor.md`, e il suo perché resta non capito (§4). Dentro c'è
una **decisione di Carmine**, con le sue parole: §3 ter.
**Regola applicata:** `CLAUDE.md` §5, caso **(a)**: nessun meccanismo nuovo, un componente della PR che aspetta i caratteri.

## 1. Che cosa è successo

`web/e2e/full/preview.spec.ts`, la guardia scritta il 6 ottobre, è caduta sul ramo: regione «Preview» con il solo invito «Add a
section», le sezioni nel documento e senza scatola, per sempre. È il difetto di allora, riaperto dalle fessure «+ sezione» e
«+ riga» (design della fase 3, §4.1), che stanno **dentro** le sezioni e quindi dentro il `@container` del renderer.

## 2. Le misure

La spec vera, intatta, su Chromium di Playwright, build separate della sola SPA sullo stesso server del banco. A-B-B-A per non
far pesare il carico della macchina, che è cambiato durante le prove.

| Build | `preview.spec.ts` |
|---|---|
| `origin/main` (`8755eb9f`) | 10 verdi su 10 |
| il ramo, prima della correzione | 10 rosse su 10 (a ×4 o ×8) |
| senza etichette, con le fessure | 4 rosse su 4 |
| con le etichette, senza le fessure | 4 verdi su 4 |
| senza etichette né fessure | 4 verdi su 4 |
| etichette e fessure disegnate dopo il primo disegno (`useEffect`) | 4 rosse su 4 |
| etichette e fessure disegnate dopo `document.fonts.ready` | 4 verdi su 4 |
| **la correzione** (§3: le sole fessure dopo `document.fonts.ready`) | **12 verdi su 12**; `origin/main` nello stesso giro, 12 su 12 |

Dentro la fessura **nessun pezzo è responsabile da solo**: restano rosse, 3 su 3 ciascuna, togliendo la sola fessura «prima», la
sola fessura «in fondo», le transizioni, l'opacità, la linea, il flex, tutte le classi del bottone, l'ombra.

⚠️ **Il difetto dipende da come lo si misura.** Una copia della spec con `addInitScript`, `waitFor` al posto di `expect` o un
cookie è scesa a 0–2 vuote su 8 con il difetto presente: per giudicare una correzione vale solo la spec vera, ripetuta
(`--repeat-each`), confrontata con `main`.

## 3. La correzione

`useInsert` (`web/src/blocks/ContentRenderer.tsx`) disegna le fessure solo dopo `document.fonts.ready`. Non si perde niente: una
fessura è invisibile finché non ci arriva il puntatore o la tastiera. Le **etichette** non aspettano, non aprono il difetto e servono
subito. Componenti a sé, che esistono solo mentre si compone: per un visitatore non c'è né stato né effetto in più.

Le guardie: `preview.spec.ts`, non toccata; `picking.test.tsx`, deterministica in jsdom, dice che le fessure aspettano i caratteri e
le etichette no.

## 3 bis. Le fessure stanno sotto quello che la sezione contiene

Dalla revisione della #457, riprodotto nel browser: una fessura sopra il contenuto prendeva il clic ai blocchi. In una riga appena
nata, senza padding, un titolo da 48 px restava al blocco per 28, e **un clic al centro del titolo inseriva una riga** invece di
sceglierlo. Due scelte, che non cambiano né il disegno né la geometria della pagina:

- **Gli strati.** Mentre si compone, il contenuto di una sezione sta su `z-[1]` e la striscia su `z-0`: la striscia risponde solo dove
  c'è aria, e un blocco si sceglie fino al bordo. La tastiera la alza, così il bottone con il focus si vede. Dove si toccano due sezioni
  **senza padding** l'aria non c'è e la striscia non risponde al puntatore: §3 ter. La fessura di una riga sta al centro dello spazio
  sopra la riga, che è suo e non copre niente.
- **«+ riga» in fondo solo per le sezioni della pagina.** Il design chiede «+ riga» fra le righe; quella in fondo a una sezione è
  un'aggiunta di questa PR, perché la prima riga di una sezione deve poter nascere. Su una riga non c'è aria per starci, e la sua
  striscia stava su quella dell'ultima figlia. Una riga dentro una riga nasce dalla barra della riga, come prima, o dalla struttura.

Il cursore: un «+» premuto da tastiera porta al nome della nuova sezione, come il doppio clic; dal puntatore resta dov'è.

Verificato con una mutazione: rimessa la striscia sopra il contenuto, il test nuovo di `web/e2e/editor.spec.ts` cade.

## 3 ter. Come si inserisce fra due sezioni che si toccano: la decisione di Carmine

**La domanda**, portata dal master: dove due sezioni senza padding si toccano non c'è aria per la striscia, e allora come si inserisce
fra le due? **La risposta di Carmine, 5 novembre 2026, in chat al master: «Dalla barra della sezione».**

Che cosa ne viene, come l'ha scritto il master:

- Nella barra della sezione scelta (`PickedBar`) ci sono **«+ sezione sopra» e «+ sezione sotto»**, per **ogni** sezione della pagina e
  non solo per quelle senza padding: il modo di inserire è uno solo, e funziona anche col dito. Su una **riga**, lo stesso gesto con
  **«+ riga sopra» e «+ riga sotto»**, dove il genitore prende righe.
- Le regole sono quelle che `actionsFor` già legge per la barra e la struttura: su un corpo bloccato niente; accanto a una sezione che
  un template blocca si inserisce, perché il blocco è su quello che contiene; una riga chiede quello che si chiede al genitore.
- Le strisce nello spazio vuoto **restano** come scorciatoia col mouse.
- Da tastiera il cursore va al nome della sezione nuova, come dalle strisce.

Scelte di chi scrive, non di Carmine, perché la barra restasse leggibile (guardata a schermo su una sezione stretta e a 360 px):

- Dove nome e comandi non stanno su una riga, il nome ne prende una sua sopra i comandi; prima si riduceva a una scheggia.
- Le due icone sono `+↑` e `+↓`, composte con quelle del set: nessuna icona sola di `lucide-react` dice «sopra» e «sotto» (le
  `between-horizontal-*` si distinguono per un triangolino a sinistra o a destra).

## 4. Che cosa non è stato verificato

- **Il perché dentro Chromium**, come il 6 ottobre: la correzione è scelta per misura, non per comprensione.
- **Un carattere caricato molto dopo** (un altro sottoinsieme, un'altra grafia): il 6 ottobre un carattere arrivato su un'anteprima
  già disegnata la lasciava disegnata, 3 su 3; qui non è stato rimisurato.
- **Firefox, Safari, Chrome stabile**: provato solo il Chromium di Playwright.
- **`rows` e sezioni molte**: la spec ha una pagina di una sezione sola.
- **La barra col dito, davvero**: guardata a 360 px e cliccata col puntatore, non provata su un telefono.
- **L'editor in una finestra da 360 px con il menu dello staff aperto**: la pagina resta larga 72 px, anche su `main` (`8755eb9f`). La
  barra è stata guardata con il menu chiuso a mano. Chiudere il menu è il §4.2 del design, che è un'altra PR.

## Da portare nel piano

- Fra le trappole di `HANDOFF.md`, accanto a quella del 6 ottobre: ⚠️ **dentro il `@container` di `ContentRenderer` un'etichetta passa,
  un bottone della cromatura no: quello che si aggiunge alle sezioni dell'editor si prova con `preview.spec.ts --repeat-each`**;
  ⚠️ **il difetto non si riproduce con una copia ritoccata della spec**.
- Sempre fra le trappole: ⚠️ **la cromatura dell'editor sta sotto il contenuto delle sezioni, non sopra**: quello che sta sopra ruba il
  clic ai blocchi, e nessun test in jsdom lo vede.
- **Design della fase 3, §4.1** (`12-design-editor-fase-3.md`), la decisione di Carmine del 5 novembre 2026, «Dalla barra della sezione»:
  il modo di inserire una sezione o una riga è la barra di quella scelta, sopra e sotto, per ogni sezione e ogni riga; le strisce sono
  la scorciatoia col mouse, e non rispondono dove non c'è aria. «+ riga» in fondo solo sulle sezioni della pagina.
- Nessuna sezione del piano cambia.
