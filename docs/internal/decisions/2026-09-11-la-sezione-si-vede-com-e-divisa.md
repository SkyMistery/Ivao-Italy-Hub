# La sezione si vede com'è divisa, e può stare su un fondo scuro

**Data:** 11 settembre 2026 — Carmine, con davanti il page builder di va.ivao.aero
(`backend/pagebuilder/editor.php?id=4`): «quando si aggiunge una sezione si vede chiaramente come è
divisa (vedi i vari drop here nelle aree vuote?), si può selezionare un colore di sfondo (e io direi
di permettere anche un'immagine)… Possiamo fare un meccanismo più simile a quello?»

**Stato:** **decisa da Carmine lo stesso giorno, fatta.** Piano **0.58**, che riapre e cambia una
convenzione di §16.C chiusa il 6 settembre.

**Come è stato guardato:** nel browser di Carmine, già autenticato, **in sola lettura** — nessun
salvataggio, nessun trascinamento, nessuna sezione aggiunta (il loro editor salva da solo). Letto
nel DOM, non dedotto da uno screenshot.

## Che cosa fanno loro

- **Ogni colonna ha sempre un bordo tratteggiato** (`pb-col-zone`, altezza minima 50 px), e una
  colonna vuota mostra un riquadro tratteggiato «Drop here» (`pb-drop-hint`). Per questo si vede
  com'è divisa una sezione prima che ci sia dentro qualcosa.
- Sulla barra della sezione: **sei colori predefiniti** (bianco `#fff`, bianco sporco `#f8f9fc`,
  azzurrino `#EEF1FB`, blu `#0D2C99`, navy `#081A5C`, scuro `#111827`), **più un `<input type=color>`
  libero**, poi spaziatura e larghezza.
- **Nessuno sfondo a immagine.** Noi ce l'abbiamo da G3.

## Che cosa facevamo noi, misurato

- La pagina che si compone è la pagina vera, resa cliccabile (strada A del 9 settembre). Ma le
  colonne **non si vedevano**, e una sezione o una colonna vuota **non disegnava niente**: aggiunta
  una sezione, non si capiva com'era fatta.
- ⚠️ **Un componente finiva sempre nella prima colonna** (`addBlock` scriveva `column: 0`). In una
  sezione a due colonne, per metterlo nella seconda lo si doveva spostare dopo. È esattamente
  l'attrito che il loro «Drop here» colonna per colonna toglie.

## Le due decisioni

1. **Colonne visibili + clic** (non il trascinamento). Mentre si compone, ogni colonna è
   tratteggiata e una colonna vuota dice «+ Aggiungi qui»; sceglierla manda lì il prossimo
   componente della barra di sinistra, e la barra lo dice («Aggiunge a: split · Colonna 2»). Con un
   blocco selezionato, la destinazione è la sua colonna. Una sezione che un template blocca non
   invita niente: un invito che la barra poi rifiuta è un pulsante che mente.
   Il visitatore **non vede niente**: è il contesto di picking del 9 settembre, che il sito pubblico
   non monta. Il trascinamento dalla barra alla colonna resta il **punto 3 aperto del 10 settembre**.
2. **La loro tavolozza, senza il colore libero.** Si aggiungono tre fondi scuri — `brand`, `deep`,
   `dark` — presi dai token di Atmosphere: `atmos-700` è **esattamente** il loro #0D2C99, `atmos-800` il
   loro navy, `fuselage-900` il loro scuro. Gli sfondi passano da quattro a **sette**.

## Perché i fondi scuri sono sicuri, e il colore libero no

⚠️ **Un fondo scuro è un pezzo di pagina nel tema scuro.** Atmosphere definisce i colori del tema
scuro sulla classe `.dark`, e la variante `dark:` di Tailwind vale per `.dark *`: una sezione che
porta `dark` insieme al suo colore rende chiaro tutto quello che i blocchi ci scrivono sopra, **per
costruzione**, qualunque colore del tema chiedano. Un colore scelto a mano non può promettere niente
del genere — testo bianco su un colore chiaro, o una fascia chiarissima quando il sito è in tema
scuro — ed è la ragione per cui il 6 settembre gli sfondi erano stati resi un insieme chiuso. Quella
ragione resta intera; il colore libero è l'unico pezzo loro non preso.

**Misurato, non supposto** (`e2e/contrast.spec.ts`, con la funzione condivisa di `e2e/contrast.ts`):

| Testo | Blu del marchio | Blu profondo | Scuro |
|---|---|---|---|
| Titolo | 4,88 : 1 | 6,52 : 1 | 7,39 : 1 |
| Testo secondario (grigio del tema scuro) | **3,50 : 1** ✗ | 4,67 : 1 | 5,29 : 1 |
| Testo secondario con `.on-brand-ground` | **4,88 : 1** | — | — |

Il grigio secondario del tema scuro (`fuselage-400`) è pensato per il fondo quasi nero, e sul blu del
marchio non basta. Un gradino più chiaro (`fuselage-300`) **solo su quel fondo**, accanto all'unica
altra correzione di colore del progetto (`styles/index.css`). Il test è stato verificato **fallire**
senza (3,5 : 1) e passare con.

## Che cosa si è toccato

- `blocks/picking.ts`: il contesto sa anche la **colonna** di destinazione, e se una sezione accetta
  componenti.
- `blocks/ContentRenderer.tsx`: il componente `Column`, che per un visitatore è una colonna qualsiasi;
  i tre fondi scuri.
- `features/content/ContentEditor.tsx` e `body.ts`: `addBlock` prende la colonna.
- `SectionFrame.tsx`: tre pastiglie in più nel selettore. `BlockDocumentWalker.Backgrounds` sul
  server, con il test d'integrazione che li chiede uno per uno.
- Piano 0.58 (§16.C e changelog), `docs/UI-GUIDELINES.md`, `CLAUDE.md`.
