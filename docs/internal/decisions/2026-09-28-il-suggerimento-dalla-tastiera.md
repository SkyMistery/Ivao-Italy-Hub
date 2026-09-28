# Il campo suggerito si sceglie dalla tastiera

**Data:** 28 settembre 2026. PR del nucleo, di una sessione di lavoro del maintainer, dopo l'unione di #145 (A6c).
**Stato:** **Proposta**. La domanda è al §5, sulla pull request; il codice della PR è la risposta raccomandata e cambia se la
risposta è un'altra.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**. Il meccanismo c'è: il campo suggerito del generatore di form (`Suggest` in
`web/src/shared/forms/SchemaForm.tsx`, piano §16.6; chiuso con `suggestionsOnly`, nota `2026-09-08-dove-puo-portare-una-voce-di-menu`).
Il difetto è nel suo posto unico, e lì si corregge. Nessuna schermata lo aggira. PR del nucleo a sé (`CLAUDE.md` §0, regola 6), fuori
dalla coda di M3.

## 1. Che cosa succede su `main`

- **Dalla tastiera non si sceglie niente.** Si scrive una parte del valore, si preme Freccia giù, poi Invio: niente.
  - La casella (l'`Input` di Atmosphere, dentro il `PopoverAnchor` di Radix) sta **fuori** dal `CommandRoot` di `cmdk`, che è nel
    `PopoverContent`.
  - `cmdk` ascolta i tasti sul suo root, quindi non sente mai quelli scritti nella casella.
  - Le frecce spostano solo il cursore del testo. Invio non sceglie l'opzione illuminata, che pure si vede: `cmdk` accende da sé la
    prima.
  - Vale in ogni campo suggerito dell'hub:
    - l'indirizzo di una voce del menu;
    - le postazioni nascoste delle impostazioni del training;
    - la postazione di una richiesta di training;
    - gli aerei dei tour;
    - la collezione nelle proprietà di un blocco.
- **Invio è del browser, e invia il form.** Nessuno lo gestisce, quindi vale l'invio implicito dell'HTML. Un campo chiuso invia un
  testo che nessuno ha offerto, a metà della ricerca, e il server lo rifiuta. Nel form della richiesta di training (#144) la casella è
  l'unica riga di un form senza bottone, e Invio manda la richiesta senza la domanda sulla teoria. La correzione di #144 lo ferma nel
  modulo; il motivo per cui Invio arriva al form è questo.
- **Chi l'ha visto:** dalberone scrivendo A6c (#145; `08`, A6c) e il revisore su #144.

## 2. Che cosa serve

- **Scegliere dalla tastiera:** le frecce muovono l'opzione illuminata, e Invio la sceglie senza inviare il form.
- **ARIA giusta:** la casella è un `combobox` che dice se la lista è aperta, quale lista controlla e quale riga è illuminata. Il fuoco
  resta nella casella, quindi lo screen reader segue le frecce tramite `aria-activedescendant`.
- **Niente perso di A6c:**
  - la regola del campo chiuso;
  - la lista che non ruba il fuoco;
  - il clic nella casella che non la chiude;
  - Escape che chiude e lascia il testo;
  - la barra di scorrimento che si trascina.

## 3. La correzione (raccomandata)

**Il meccanismo è di `cmdk`:** la casella gli **passa i tasti** e `cmdk` muove e sceglie come fa per il suo input.

- Freccia giù, Freccia su e Invio diventano un `keydown` nuovo, spedito al root di `cmdk`, che lo gestisce subito. Lo scorrimento
  della riga illuminata e il salto dei gruppi restano di `cmdk`.
- Quale opzione è illuminata lo tiene `Suggest` (`value` e `onValueChange` sul `CommandRoot`), perché serve per decidere due cose: se
  qualcosa è illuminato, e che cosa vuol dire Invio.

| Tasto nella casella | Che cosa fa |
|---|---|
| **Freccia giù / su**, lista aperta | Muove l'opzione illuminata. Freccia su senza niente illuminato va all'ultima. Il cursore del testo non si muove. |
| **Freccia giù / su**, lista chiusa | Apre la lista sulla prima opzione. |
| **Invio**, lista aperta e un'opzione illuminata | La sceglie. `preventDefault`: il form **non** parte. |
| **Invio**, campo chiuso con un testo scritto che nessuno ha offerto | Non invia: riapre la lista, perché il testo è una ricerca non finita. |
| **Invio**, altrimenti (niente illuminato, e la casella tiene un valore) | **È del form, e lo invia**, come in ogni riga di un form. |
| **Escape** | Come oggi: chiude la lista, il testo resta finché non si esce dal campo. |
| **Tab** | Come oggi: non sceglie. Uscendo vale la regola del campo chiuso. |

**Che cosa è illuminato.** Non è un dettaglio: è ciò che Invio sceglie.
- **Niente, finché qualcuno non cerca o non si muove.** La lista si apre al fuoco. Se lì fosse già illuminata la prima opzione, l'Invio
  di chi attraversa il form solo per inviarlo diventerebbe una scelta mai fatta. Per dire «niente» a `cmdk` si usa un valore che
  nessuna opzione ha (`NOTHING_LIT`, un NUL): con la stringa vuota `cmdk` accende la prima da sé.
- **In un campo chiuso che si sta cercando, la prima opzione ancora mostrata**, se una freccia non ne ha illuminata un'altra. Lì il
  testo è una ricerca, e Invio la chiude: si scrive una parte del nominativo, Invio, e si prende quello che corrisponde.
- **In un campo aperto, solo una freccia o il puntatore.** Lì il testo scritto è il valore, e Invio lo tiene.

**Invio a lista chiusa invia il form, deliberatamente.** Una casella che inghiottisse Invio sarebbe l'unica riga del form in cui Invio
non fa niente. `form.submitHint` dice a chi usa uno screen reader che Invio invia. L'unica eccezione è la ricerca non finita di un
campo chiuso (la tabella sopra).
- **Per #144 vuol dire:** il primo Invio dopo aver scritto il nominativo lo sceglie, il secondo apre la domanda sulla teoria. La
  protezione nel modulo (inviare solo dopo la conferma) resta necessaria.

**Il punto 4 della nota di A6c** (una scelta diventa «quello che c'era») ora ha una prova che non dipende dai tempi. Si sceglie con
Invio, il fuoco non lascia mai la casella, nessun arrivo rilegge il valore, e l'uscita con un testo nuovo rimette la scelta. Senza il
punto 4 rimetterebbe il valore di partenza: tolta quella riga, la prova Vitest cade.

**Gli id per l'ARIA sono di `cmdk`,** che sovrascrive quelli dati alla sua lista e alle sue righe.
- `aria-controls` prende l'id della lista dal suo `ref`.
- `aria-activedescendant` lo cerca nella pagina (la riga con quel `data-value`) e lo scrive a mano sulla casella, in un
  `useLayoutEffect`. Nient'altro scrive quell'attributo.

**Provato:**

- **Vitest** (`extensions.test.tsx`, sette casi nuovi, tutti rossi sul `Suggest` di `main`):
  - la parte scritta più Invio sceglie e non invia;
  - le frecce e l'`aria-activedescendant`;
  - la freccia che apre, e Freccia su verso l'ultima;
  - Invio senza ricerca che invia;
  - la ricerca non finita in un form senza bottone, che non parte;
  - il punto 4;
  - il campo aperto.
- **Playwright** (smoke, `e2e/keyboard-suggestion.spec.ts`, cinque prove sulla voce di menu, tutte rosse su `main`):
  - Invio sceglie e il salvataggio parte solo al secondo Invio;
  - le frecce scorrono una lista lunga fino all'ultima riga, visibile;
  - Invio senza ricerca salva;
  - la ricerca non finita non salva;
  - la freccia riapre la lista chiusa da Escape.
- **Le cinque prove di `closed-suggestion.spec.ts` (A6c)** passano senza modifiche.

## 4. Alternative scartate

| Alternativa | Perché no |
|---|---|
| **Mettere la casella dentro il `CommandRoot`**: il root avvolge il popover, e i tasti ci arrivano da soli | Il `CommandRoot` di Atmosphere è la scatola della lista, con sfondo e `overflow-hidden`. Attorno alla casella ne taglierebbe l'anello del fuoco, e toglierebbe lo sfondo alla lista nel popover. Porterebbe anche nel form il `tabindex="-1"` del root e la sua `<label>` nascosta. Rimetterlo a posto vorrebbe dire sovrascrivere le classi di Atmosphere. |
| **Usare il `CommandInput` di `cmdk` come casella** | Vive dentro il root, quindi dentro il popover: la casella si aprirebbe e chiuderebbe con la lista. |
| **Una lista fatta a mano** (un `listbox` nostro, frecce e scorrimento scritti qui) | È la copia di ciò che `cmdk` fa già, scorrimento e gruppi compresi. `CLAUDE.md` §2: niente scritto due volte. |
| **Lasciare a `cmdk` la prima opzione sempre illuminata** (il suo comportamento di default) | L'Invio di chi attraversa un campo chiuso senza toccarlo sceglierebbe la prima opzione al posto del valore che c'è. In un campo aperto, Invio su un testo che somiglia a un'opzione prenderebbe l'opzione. |
| **Invio non invia mai da un campo suggerito** | La casella sarebbe l'unica riga del form dove Invio non fa niente, contro `form.submitHint`. E non basterebbe a #144, dove anche il bottone deve chiedere la teoria. |

## 5. La domanda per Carmine

**Il campo suggerito si sceglie dalla tastiera come al §3?** Cioè:
- la casella passa frecce e Invio a `cmdk`;
- Invio con un'opzione illuminata la sceglie e non invia il form;
- all'arrivo non è illuminato niente;
- in un campo chiuso che si cerca è illuminata la prima opzione;
- Invio a lista chiusa invia il form, tranne per una ricerca non finita di un campo chiuso.

**Raccomandata: sì.** Usa il meccanismo di `cmdk` invece di rifarlo, cambia solo `Suggest`, e toglie la causa del difetto di #144
senza togliere l'invio con Invio a nessun form.

## 6. Che cosa si tocca

- **Questa PR (nucleo):**
  - `web/src/shared/forms/SchemaForm.tsx` (`Suggest`);
  - `web/src/shared/forms/extensions.test.tsx` (i sette casi, e `actionsElsewhere` nel suo `render`);
  - la spec nuova `web/e2e/keyboard-suggestion.spec.ts`;
  - questa nota.
- **Nessuna schermata, nessuno schema, nessuna parola.** La casella diventa `role="combobox"`: le prove che la cercano per etichetta
  non cambiano. Nessuna prova esistente la cercava come `textbox`.
- **#144 (A6b, dalberone):** il caso della sua spec smoke `training-request.spec.ts` che fa `fill('XXAA_TWR')` e subito Invio per
  aprire la domanda avrà bisogno di **Invio per scegliere, poi Invio per chiedere**, quando le due PR saranno in `main`. La sua spec
  non si tocca qui: il caso preciso lo dice la PR dopo averlo eseguito, e lo chiede il revisore su #144.

## Da portare nel piano

- **§16.6** (il generatore di form), il campo suggerito, in una riga: si sceglie anche dalla tastiera. Le frecce muovono l'opzione
  illuminata e Invio la sceglie senza inviare il form. All'arrivo non è illuminato niente, e in un campo chiuso che si cerca la prima
  opzione mostrata. A lista chiusa Invio invia il form, tranne per una ricerca non finita di un campo chiuso. Una riga nel changelog.
- **`CONTRIBUTING.md`, «Traps already paid for»:**
  - una casella fuori dal root di `cmdk` non gli passa i tasti da sola;
  - `cmdk` accende la prima opzione ogni volta che il suo `value` è vuoto;
  - `cmdk` sovrascrive gli `id` dati alla sua lista e alle sue righe.
