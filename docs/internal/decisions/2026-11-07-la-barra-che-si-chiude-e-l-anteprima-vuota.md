# La barra che si chiude e l'anteprima vuota: le sezioni nascono dopo

**Data:** 7 novembre 2026 — correzione dentro la PR 3.2 (#471), sessione di lavoro di Carmine, ramo `feat/editor-panels`
**Stato:** **Correzione**, non una decisione di prodotto: niente cambia per chi guarda. Scritta perché è la stessa trappola di
`2026-10-06-l-anteprima-vuota-dell-editor.md`, aperta da un'altra parte.
**Regola applicata:** `CLAUDE.md` §5, caso **(a)**: l'ordine di due cose nell'editor, nessun meccanismo nuovo.

## 1. Che cosa è successo

La prima versione della 3.2 chiudeva la barra dello staff in un `useLayoutEffect` di `BodyEditor`, cioè con la pagina già nel
documento. In CI (run 42300556822) `e2e/full/preview.spec.ts` è caduto a ×8: la regione «Preview» con il solo invito ad aggiungere
una sezione, e la sezione senza scatola. `main` e le altre PR della stessa finestra erano verdi.

## 2. La misura

Una spec temporanea su una porta privata: API simulata, un browser nuovo a ogni giro, `Emulation.setCPUThrottlingRate`, una sezione
con un titolo. «Vuota» vuol dire titolo non visibile in 15 secondi. Sulla macchina di Carmine non si apre fino a ×12: la finestra si
apre a ritmi più lenti che sul runner, quindi i numeri dicono chi la apre e non quanto spesso capita in CI.

| Che cosa | ×8 | ×16 | ×20 |
|---|---|---|---|
| `main` a `20a6aef8` | — | 0 su 8 | 0 su 8 |
| 3.2 prima della correzione (`213e2377`), due serie | 0 su 6 | 3 su 6, 4 su 8 | 4 su 6, 0 su 8 |
| 3.2 senza chiudere la barra | — | 0 su 8 | 0 su 8 |
| 3.2 con le colonne fisse di prima, la barra sì | — | 4 su 8 | 3 su 8 |
| **3.2 corretta** | 0 su 12 | 0 su 12 | 0 su 12 |

Quindi: la griglia nuova non c'entra, la barra sì. Le sezioni misurano un contenitore (`@container`); se quel contenitore si stringe
sotto di loro mentre la pagina sta nascendo, su una macchina lenta possono restare senza scatola per sempre. Il perché dentro Chromium
resta non capito, come il 6 ottobre: la correzione è scelta per misura.

## 3. La correzione

`useSidebarShutWhileWriting` dice quando la barra è chiusa, e `BodyEditor` disegna la colonna della pagina solo da quel momento. Le
due cose succedono prima del primo disegno sullo schermo, quindi non si vede nessuna attesa. La guardia che regge su ogni macchina è
in `BodyEditor.test.tsx`: a nessun commit dell'editor una sezione è nel documento con la barra aperta. Togliendo la correzione cade.

## 4. Che cosa non è stato verificato

- **`e2e:full` in locale**: Docker Desktop va in crash all'avvio su questa macchina (socket `dockerInference` rimasto appeso). La
  prova sul banco vero è quella della CI.
- **La barra aperta o chiusa a mano mentre i caratteri stanno ancora arrivando**: il contenitore cambia larghezza anche così, e questa
  correzione non lo copre. Serve un gesto dentro una finestra di qualche secondo su una macchina molto lenta; non è stato misurato.

## Da portare nel piano

- Niente che cambi una sezione del piano.
- Per `HANDOFF.md`, fra le trappole, accanto a quella del 6 ottobre: ⚠️ **il contenitore delle sezioni non cambia larghezza mentre la
  pagina nasce**: ciò che lo cambia all'apertura dell'editor avviene prima che le sezioni siano nel documento.
