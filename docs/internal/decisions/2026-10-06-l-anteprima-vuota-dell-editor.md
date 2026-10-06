# L'anteprima vuota dell'editor: l'invito «aggiungi una sezione» esce dal contenitore

**Data:** 6 ottobre 2026 — correzione del nucleo, sessione di lavoro di Carmine, ramo `fix/editor-preview-empty`
**Stato:** **Correzione**, non una decisione di prodotto: niente cambia per chi guarda. Scritta perché la causa è una trappola che
si ripresenterebbe, e perché il meccanismo dentro il browser **non è stato capito fino in fondo** (§5).
**Regola applicata:** `CLAUDE.md` §5, caso **(a)**: un elemento spostato di un livello nel renderer, nessun meccanismo nuovo.

## 1. Che cosa è successo

- Il 6 ottobre 2026 `web/e2e/full/dashboard.spec.ts` è caduto due volte in CI (`build-test`, PR #221 run 37442901106 e PR #222
  head `3441502`), su due PR che non toccano né il cruscotto né quel test. In entrambe: un minuto, poi
  `apiRequestContext.delete: Test timeout of 60000ms exceeded`.
- **Quell'errore non era il difetto.** Il log del server delle due run dice che il test non è mai arrivato né al salvataggio (nessun
  `PUT /api/content/22`) né alla rilettura; lo snapshot della pagina allegato al report mostra la regione «Preview» **vuota**. Il
  test si era fermato sul primo clic nell'anteprima, che aspetta finché ha tempo; scaduto il minuto, il `delete` nel `finally` è
  stato rifiutato perché il test era finito, e il suo errore ha **sostituito** quello del passo fermo. La riga è rimasta nel banco.
- Non era la maniglia, né il salvataggio, né il banco freddo: l'anteprima non disegnava niente.

## 2. La causa, misurata

Riprodotto in locale sul banco (`pnpm e2e:full`, database a parte) rallentando il processore del browser dal protocollo di debug
(`Emulation.setCPUThrottlingRate`); a velocità piena non si è mai visto (20 esecuzioni su 20 verdi).

| Che cosa | Anteprima vuota |
|---|---|
| Chromium 151 (quello di Playwright 1.62.1), processore ×4, editor di un cruscotto e di una pagina | 8 su 8, 6 su 6, 4 su 4 (una serie: 5 su 6) |
| lo stesso, ×20 | 1 su 4 |
| **Edge 154 stabile**, istanza isolata, ×4 e ×8 | 5 su 5 e 5 su 5 (×2: 0 su 5) |
| ×4, con i file dei caratteri bloccati, o ritardati di tre secondi | 0 su 6 e 0 su 6 |
| ×4, l'anteprima senza `container-type` | 0 su 5 |
| ×4, senza l'invito «Add a section» (tolto dal DOM, o il suo involucro `display: none`) | 0 su 8 e 0 su 6 |
| una pagina **pubblica** a ×2, ×4, ×6/×8: una sezione, due sezioni, con un blocco a schede | 0 su 18 ogni volta |
| un carattere che finisce di caricarsi **dopo**, su un'anteprima già disegnata | resta disegnata, 3 su 3 |

Che cosa si vede quando è vuota: le sezioni **sono nel documento**, con `display` giusto, ma senza scatola — `0×0`,
`checkVisibility()` falso, `height` calcolata `auto` — e il contenitore `@container` del renderer è alto 0. Non si riprende da sola
(né aspettando, né ridimensionando la finestra); torna appena si tocca il `display` di un figlio.

Quindi: **i caratteri (Poppins, Nunito Sans) finiscono di caricarsi un momento dopo il primo disegno dell'editor**, e in quella
finestra Chromium lascia senza albero d'impaginazione i discendenti del contenitore di container query dell'anteprima, **quando
dentro quel contenitore, dopo le sezioni, c'è l'invito ad aggiungerne una**. Su una macchina veloce i caratteri arrivano prima
della pagina e la finestra non si apre; su una più lenta — il runner della CI, il portatile di qualcuno dello staff — sì. È un
difetto del prodotto, non del test: **chi apre l'editor su una macchina lenta può trovare l'anteprima vuota**.

Provato e **senza effetto** (sempre vuota): `main` non più contenitore, il `fieldset` `display: contents`, il riquadro senza
`overflow: hidden`, nessuna transizione, niente dnd-kit nell'anteprima (né sezioni ordinabili, né slot, né blocchi trascinabili),
il bottone dell'invito `display: none` o in un carattere di sistema, l'involucro senza padding, `display: contents` o `absolute`.

## 3. La correzione

1. **`AddSectionInvitation` sta dopo il `div` `@container`, non dentro** (`web/src/blocks/ContentRenderer.tsx`). L'invito non
   misura niente (`px-4 py-4`, nessuna variante `@view-…`), il contenitore è una colonna senza `gap`: a schermo è identico. Per un
   visitatore non cambia nemmeno il DOM: l'invito non c'è.
   Misurato dopo: Chromium ×2, ×3, ×4, ×6, ×8, ×12 → 0 vuote su 38; editor di una pagina con una colonna vuota, con un blocco a
   schede, semplice, a ×4 e ×8 → 0 su 36; Edge 154 a ×4 e ×8 → 0 su 12.
2. **Due guardie.** `web/src/blocks/picking.test.tsx`: l'invito non è dentro il contenitore (deterministica, in jsdom).
   `web/e2e/full/preview.spec.ts`: l'editor aperto da un browser nuovo a ×2, ×4, ×8 disegna l'anteprima. Rimettendo l'invito dentro
   cadono tutte e due (la seconda 5 su 5, a ×4).
3. **Il test del cruscotto** (`dashboard.spec.ts`): la pulizia passa da un `finally` a un `afterEach`, che ha il suo tempo, gira
   anche dopo un timeout e **aggiunge** il suo errore invece di sostituirlo; e prima del clic si aspetta di *vedere* la tessera,
   così un'anteprima vuota lo dice in cinque secondi e con il suo nome.
4. Versione **0.6.5** (PATCH: correzione, nessuna migrazione).

## 4. Che cosa non si è fatto

- **Aspettare i caratteri prima di disegnare** (`document.fonts.load` in `main.tsx`): toglierebbe la finestra per ogni pagina, ma
  cambia l'avvio di tutto il sito per un difetto che si è visto solo nell'editor, e un carattere di un altro sottoinsieme
  (`latin-ext`) caricato più tardi la riaprirebbe comunque.
- **Alzare il timeout o ritentare il test**: l'anteprima non si riprende da sola.
- **Toccare gli altri `finally` della suite** (`e2e/full/*.spec.ts` ne ha molti con la stessa forma): mascherano allo stesso modo
  un timeout del corpo. È un lavoro a parte, da decidere; qui è cambiato solo il file che è caduto.

## 5. Che cosa non è stato verificato

- **Il perché dentro Chromium.** La correzione è scelta per misura, non per comprensione: non so perché proprio quell'elemento, in
  quella posizione, apra il difetto, e non ho cercato né aperto una segnalazione a Chromium. Un altro elemento messo domani nello
  stesso punto potrebbe riaprirlo: la guardia in `preview.spec.ts` c'è per quello, ma **è una guardia e non una prova** — su un
  runner il cui passo manca la finestra a tutte e tre le velocità passerebbe anche con il difetto tornato.
- **Chrome stabile** non è installato su questa macchina: provati Chromium 151 ed Edge 154. Firefox e Safari non provati.
- **Le pagine pubbliche** sono state misurate sane in tre forme, non in tutte quelle possibili.

## Da portare nel piano

- Niente che cambi una sezione del piano. Una riga di changelog per la **0.6.5**: «l'anteprima dell'editor restava vuota su una
  macchina lenta (l'invito ad aggiungere una sezione esce dal contenitore di container query); il test del cruscotto pulisce in un
  `afterEach`».
- Per `HANDOFF.md`, fra le trappole: ⚠️ **niente che non sia una sezione dentro il `@container` di `ContentRenderer`**; e ⚠️ **un
  `finally` che cancella maschera il timeout del corpo** — la pulizia di uno spec va in un `afterEach`.
