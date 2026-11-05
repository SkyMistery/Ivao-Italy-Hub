# Le fessure «+ sezione» e l'anteprima vuota: si disegnano a caratteri finiti

**Data:** 5 novembre 2026 — PR 3.1 della fase 3 dell'editor, sessione di lavoro di Carmine, ramo `feat/editor-sections`
**Stato:** **Correzione**, non una decisione di prodotto: il difetto l'ha aperto questa stessa PR, prima che entrasse in `main`.
Scritta perché è la stessa trappola di `2026-10-06-l-anteprima-vuota-dell-editor.md`, e il suo perché resta non capito (§4).
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

`SectionInsertions` (`web/src/blocks/ContentRenderer.tsx`) disegna le fessure solo dopo `document.fonts.ready`. Non si perde niente:
una fessura è invisibile finché non ci arriva il puntatore o la tastiera. Le **etichette** non aspettano, non aprono il difetto e
servono subito. Un componente a sé, che esiste solo mentre si compone: per un visitatore non c'è né stato né effetto in più.

Le guardie: `preview.spec.ts`, non toccata; `picking.test.tsx`, deterministica in jsdom, dice che le fessure aspettano i caratteri e
le etichette no.

## 4. Che cosa non è stato verificato

- **Il perché dentro Chromium**, come il 6 ottobre: la correzione è scelta per misura, non per comprensione.
- **Un carattere caricato molto dopo** (un altro sottoinsieme, un'altra grafia): il 6 ottobre un carattere arrivato su un'anteprima
  già disegnata la lasciava disegnata, 3 su 3; qui non è stato rimisurato.
- **Firefox, Safari, Chrome stabile**: provato solo il Chromium di Playwright.
- **`rows` e sezioni molte**: la spec ha una pagina di una sezione sola.

## Da portare nel piano

- Fra le trappole di `HANDOFF.md`, accanto a quella del 6 ottobre: ⚠️ **dentro il `@container` di `ContentRenderer` un'etichetta passa,
  un bottone della cromatura no: quello che si aggiunge alle sezioni dell'editor si prova con `preview.spec.ts --repeat-each`**;
  ⚠️ **il difetto non si riproduce con una copia ritoccata della spec**.
- Nessuna sezione del piano cambia.
