# La barra di formattazione: che cosa cambia rispetto al design

**Data:** 7 novembre 2026, PR 3.6 della fase 3 (`12-design-editor-fase-3.md` §4.6).
**Stato:** il punto 1 e il punto 3 sono scelte della sessione, che seguono una regola già decisa dal design. **Il punto 2 è
«Proposta»**: il tono scuro del verde acqua lo decide Carmine (la domanda è andata al master il 7 novembre).
**Classificazione** (`CLAUDE.md` §5): (b), si estende `MarkdownContent` come dice il §4.6. Il design diceva «nota sua: no»; questa
nota c'è perché tre cose scritte lì, messe alla prova, non reggono così come sono.

## 1. Non `remark-gfm` e `remark-directive` interi, ma il pezzo che serve di ciascuno

Il §4.6 dice `remark-gfm` con `allowedElements` e `remark-directive`. Il §6 dice che i corpi già salvati si disegnano come oggi. Le
due frasi non stanno insieme, misurato il 7 novembre sui due pacchetti:

| Testo salvato | Oggi | `remark-gfm` con `allowedElements` | `remark-directive` |
|---|---|---|---|
| una tabella Markdown | il testo con i `\|` | **sparisce** | come oggi |
| `nota[^1]` con la sua nota | il testo | **sparisce la nota** | come oggi |
| `- [ ] da fare` | il testo | sparisce `[ ]` | come oggi |
| `vedi www.ivao.aero` | il testo | diventa un link | come oggi |
| `alle 10:30` | il testo | come oggi | **`alle 10` e un riquadro vuoto** |
| `Frequenza:LIRF_APP` | il testo | come oggi | **`Frequenza` e un riquadro vuoto** |
| `:::note` e le righe sotto | il testo | come oggi | **riquadro senza la riga `:::note`** |

Quindi entrano i pezzi di cui quei due pacchetti sono fatti: `micromark-extension-gfm-strikethrough` e
`mdast-util-gfm-strikethrough` per il barrato, solo con due tildi (`singleTilde: false`); `micromark-extension-directive`, solo la
direttiva dentro la riga, e `mdast-util-directive`. Sono quattro dipendenze dove il design ne contava due, le stesse che i due
pacchetti porterebbero comunque, e il resto di GFM non entra nel bundle.

Una direttiva che non è un colore torna a essere quello che è stato scritto: senza etichetta è il testo sorgente (`10:30`); con
un'etichetta e un nome di sole lettere è un colore fuori dall'insieme, e resta la parola in testo semplice come dice il §4.6.

⚠️ Il testo alternativo di un'immagine dentro il Markdown è calcolato prima di quel passo, e `![foto 10:30](/x.png)` usciva «foto 10»:
se contiene due punti, lo stesso passo lo riprende dal sorgente.

## 2. Il verde acqua sugli sfondi scuri (Proposta) ⚖️

Il §4.6 chiede una coppia chiaro/scuro di token, ognuna a 4,5 : 1 su tutti gli sfondi, e dice che se un tono non ci arriva la PR si
ferma. Blu (`atmos-700`, `atmos-100`), azzurro (`ocean-600`, `ocean-200`) e rosso (`semantic-red-700`, `semantic-red-200`) ci
arrivano. Il verde acqua no: la famiglia `product-aurora` ha tre toni, `mid` va bene sugli sfondi chiari (5,09 : 1), e il più chiaro,
`light`, si ferma a 2,52 : 1 sullo sfondo `brand`.

**Raccomandazione:** `aurora-light` schiarito con il 40 % di bianco (`color-mix`, nessun esadecimale), circa 5,0 : 1 sullo sfondo
peggiore. È un derivato del token e non un token. Le alternative: `semantic-green-400` (6,0 : 1, ma è verde e cambia tinta fra i due
temi); il verde acqua solo sugli sfondi chiari, che contraddice la risposta 3 e che non si raccomanda.

## 3. Ci sono altri due campi

Il §4.6 dice «oggi sono quattro», e il callout è il quinto. `MarkdownContent` disegna anche la descrizione e gli esempi di un errore
del catalogo dei tour (`modules/flightops/blocks/errorCatalog.tsx`), e la regola del §4.6 è «sopra ogni campo che il renderer disegna
con `MarkdownContent`»: hanno la barra anche loro. Non essendo blocchi non toccano l'impronta `rules`.

## Da notare, non deciso

Sugli sfondi scuri blu e azzurro sono quasi uguali (`#a4b6f7` e `#aab6e1`), e sugli sfondi chiari vicini (`#0d2c99` e `#3c55ac`): la
famiglia `ocean` del marchio è un blu spento. Passano la misura e restano così.

## Da portare nel piano

- **§8.3**: `MarkdownContent` legge il barrato e le direttive di colore con i quattro pacchetti detti sopra, non con `remark-gfm` e
  `remark-directive`.
- **§16**: l'annotazione `markdown` del form generato vale per ogni campo disegnato da `MarkdownContent`, anche di un modulo.
- **`docs/UI-GUIDELINES.md`**: già cambiata in questa PR, come chiede il §4.6.
