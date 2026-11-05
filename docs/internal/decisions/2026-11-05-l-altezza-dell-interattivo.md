# L'altezza dell'interattivo: da dove veniva la barra, e il frammento oltre i 2000 px

- **Data**: 5 novembre 2026
- **Stato**: **Proposta** per la domanda del §3, che è di Carmine. Il resto sono scelte di una sessione di lavoro dentro il design, da leggere sulla PR
- **Classificazione** (`CLAUDE.md` §5): **(b)**, PR 3.8 di `12-design-editor-fase-3.md` §5.5: nessun meccanismo nuovo. Il design non prevedeva una nota; questa esiste perché la misura se ne scosta (§2) e perché c'è una domanda (§3)

## 1. Da dove veniva la barra

Il design elencava quattro sospetti. Riprodotti con il guscio vero e con le barre di scorrimento vere (Playwright le nasconde di suo), la causa che basta da sola non era fra questi: **la barra si tiene in piedi da sé**.

Un disegno più alto dell'altezza iniziale della cornice fa comparire la barra; la barra toglie 15 px di larghezza; un SVG scalato dal suo `viewBox` diventa più basso; la misura dice l'altezza più bassa; a quell'altezza, senza barra, il disegno a piena larghezza non ci starebbe, e la barra resta. **Lo faceva l'esempio delle linee guida, a ogni larghezza**, con `scrollHeight` uguale a `clientHeight`: guardando i numeri non c'era niente da scorrere. Non si vede sui tablet e sui telefoni, dove la barra non toglie larghezza.

Il frammento che lo staff ha provato non è nel repository, quindi non è stato riprovato. Dei quattro sospetti del design:

- **un pixel di arrotondamento**: non riprodotto, né a densità 1 né a 1,25 e 1,5. La misura era già arrotondata per eccesso;
- **fuori dal flusso e margini negativi**: confermati, barra;
- **`100vh`**: `height: 100vh` è innocuo, `min-height: 100vh` con un margine accanto cresce a ogni giro fino a 2000 px, confermato. Lo dicono le linee guida; il guscio non può impedirlo;
- **oltre 2000 px**: confermato, §3.

Un caso non elencato: `html, body { height: 100% }`, comune nel codice scritto da un assistente. Il rettangolo dice l'altezza della cornice e quello che sta dentro trabocca.

## 2. La misura, e perché non è solo «il più grande dei due»

Il design dice: la misura è il più grande fra il rettangolo e `scrollHeight`. **Scritta così, la cornice non scenderebbe mai**: lo `scrollHeight` di `documentElement` non è mai minore dell'altezza della cornice. Un frammento più basso dei 320 px di partenza li terrebbe per sempre, e così un disegno quando un tablet si gira.

Quindi `scrollHeight` conta solo quando supera la cornice, e quello che ha detto si ricorda (a quell'altezza non avrà più niente da aggiungere). Si dimentica quando la radice cambia forma: la misura riparte dal flusso, la pagina risponde, e se qualcosa trabocca ancora lo dice di nuovo. Due passi, solo quando la larghezza o il flusso cambiano.

Scartato **`position: relative` su `body`**, che avrebbe reso `body.scrollHeight` una misura senza quel pavimento: un `<canvas style="position:absolute;inset:0">` su un `body` senza altezza oggi riempie la cornice e diventerebbe alto zero. Scartato anche **`height: auto !important` su `html` e `body`**, per la stessa ragione. I blocchi già salvati si disegnano come oggi (design §6).

In una cornice che non scorre, quello che non si misura non si raggiunge: per questo si misura anche quando il documento cambia (`MutationObserver`, al massimo una volta per fotogramma, senza gli attributi che un disegno cambia sessanta volte al secondo) e quando la pagina risponde (`resize`).

Limiti che restano, sapendolo: quello che trabocca e poi sparisce lascia spazio vuoto finché la larghezza o il flusso non cambiano; un `body` che ha un'altezza in `%` o in `vh` non fa scendere la cornice; quello che è più largo della cornice viene tagliato senza che nessuno lo dica, e le istruzioni chiedono la prova a 360 e a 768 px per questo.

⚠️ **Il guscio nuovo non arriva da solo alle versioni già pubblicate.** Il frame di una pagina pubblica è in cache per un anno, `immutable` (`EmbedEndpoints`), nel browser di chi l'ha già letto e in ciò che sta davanti al server: il suo indirizzo ha il numero della versione, non quello del guscio. Per questo la pagina dice anche `scrolling="no"` sull'`iframe`, un attributo vecchio ma che tutti rispettano: provato con il guscio vecchio, toglie la barra e il disegno torna alla sua altezza, perché senza barra la misura vecchia è giusta. Quello che il guscio vecchio non misura (fuori dal flusso) resta invece tagliato **finché la pagina non si ripubblica**. Va detto nelle note di rilascio. Cambiare la cache, o mettere la versione del guscio nell'indirizzo, è un'altra cosa e non è qui.

Piccole aggiunte per coerenza con le istruzioni: il pulsante «Tablet, 768 px» nell'anteprima locale, che altrimenti non offriva la prova che le istruzioni chiedono, e `margin: 0` sulla `figure` dell'esempio, che su un telefono perdeva 80 px su 360.

## 3. ⚖️ La domanda per Carmine: un frammento più alto di 2000 px

Finora la parte oltre i 2000 px si raggiungeva con la barra. Con «la cornice non scorre mai» **viene tagliata**, e nessuno può più arrivarci: un pulsante lì sotto non esiste.

- **(a) Tagliata, e lo staff lo sa — raccomandata, ed è quello che fa la PR.** Una riga sotto il blocco, solo per lo staff, come quella dei rifiuti del browser («questa animazione è alta 2600 px e un frame si ferma a 2000…»), e la stessa nell'anteprima locale. Stesso meccanismo, nessun componente, una chiave di lingua. Chi scrive il frammento lo scopre mentre compone, e un lettore non vede niente. Oltre a tutto, è la stessa regola della nota del 12 settembre: niente di vietato fallisce in silenzio.
- **(b) Tagliata e basta.** Il design alla lettera, che non cita la riga: si toglie un `useState` da `blocks.tsx`, una chiave di lingua e due righe dell'anteprima.
- **(c) Oltre i 2000 px la barra resta.** Riapre il difetto, solo per quei frammenti.

## Da portare nel piano

- **§9.3** (il blocco interattivo): la cornice non scorre mai; la misura del guscio (§2 di questa nota); oltre i 2000 px il frammento è tagliato e lo staff lo sa, se Carmine sceglie (a).
