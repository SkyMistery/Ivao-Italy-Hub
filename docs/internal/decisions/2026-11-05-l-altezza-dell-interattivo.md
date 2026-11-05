# L'altezza dell'interattivo: da dove veniva la barra, e il frammento oltre i 2000 px

- **Data**: 5 novembre 2026
- **Stato**: **decisa** per il §3, da Carmine, in chat al master, 5 novembre 2026: «(c) resta la barra». Il resto sono scelte di una sessione di lavoro dentro il design, da leggere sulla PR
- **Classificazione** (`CLAUDE.md` §5): **(b)**, PR 3.8 di `12-design-editor-fase-3.md` §5.5: nessun meccanismo nuovo. Il design non prevedeva una nota; questa esiste perché ci si scosta da due sue frasi (§2) e perché c'era una domanda (§3)

## 1. Da dove veniva la barra

Il design elencava quattro sospetti. Riprodotti con il guscio vero e con le barre di scorrimento vere (Playwright le nasconde di suo), la causa che basta da sola non era fra questi: **la barra si tiene in piedi da sé**.

Un disegno più alto dell'altezza iniziale della cornice fa comparire la barra; la barra toglie 15 px di larghezza; un SVG scalato dal suo `viewBox` diventa più basso; la misura dice l'altezza più bassa; a quell'altezza, senza barra, il disegno a piena larghezza non ci starebbe, e la barra resta. **Lo faceva l'esempio delle linee guida, a ogni larghezza**, con `scrollHeight` uguale a `clientHeight`: guardando i numeri non c'era niente da scorrere. Non si vede dove la barra non toglie larghezza.

Il frammento che lo staff ha provato non è nel repository, quindi non è stato riprovato. Dei quattro sospetti del design:

- **un pixel di arrotondamento**: non riprodotto, né a densità 1 né a 1,25 e 1,5. La misura era già arrotondata per eccesso;
- **fuori dal flusso e margini negativi**: confermati, barra;
- **`100vh`**: `height: 100vh` è innocuo, `min-height: 100vh` con un margine accanto cresce a ogni giro fino a 2000 px, confermato. Lo dicono le linee guida; né il guscio né la pagina possono impedirlo;
- **oltre 2000 px**: confermato, §3.

Un caso non elencato: `html, body { height: 100% }`, comune nel codice scritto da un assistente. Il rettangolo dice l'altezza della cornice e quello che sta dentro trabocca.

## 2. I due scostamenti dal design

### La misura non è solo «il più grande dei due»

Il design dice: la misura è il più grande fra il rettangolo e `scrollHeight`. **Scritta così, la cornice non scenderebbe mai**: lo `scrollHeight` di `documentElement` non è mai minore dell'altezza della cornice. Un frammento più basso dei 320 px di partenza li terrebbe per sempre, e così un disegno quando un tablet si gira.

Quindi `scrollHeight` conta solo quando supera la cornice, e quello che ha detto si ricorda (a quell'altezza non avrà più niente da aggiungere). Si dimentica quando la radice cambia forma: la misura riparte dal flusso, la pagina risponde, e se qualcosa trabocca ancora lo dice di nuovo. Due passi, solo quando la larghezza o il flusso cambiano.

Scartato **`position: relative` su `body`**, che avrebbe reso `body.scrollHeight` una misura senza quel pavimento: un `<canvas style="position:absolute;inset:0">` su un `body` senza altezza oggi riempie la cornice e diventerebbe alto zero. Scartato anche **`height: auto !important` su `html` e `body`**, per la stessa ragione. I blocchi già salvati si disegnano come oggi (design §6).

Dove la barra non c'è, quello che non si misura non si raggiunge: per questo si misura anche quando il documento cambia (`MutationObserver`, al massimo una volta per fotogramma, senza gli attributi che un disegno cambia sessanta volte al secondo) e quando la pagina risponde (`resize`).

### «La cornice non scorre mai» non è più vero, e la barra non la decide il guscio

⚠️ Il design dice che la cornice non scorre mai, e che la correzione sta nel guscio. Dopo la risposta di Carmine (§3) **la cornice non ha la barra fino a 2000 px, e oltre sì**; e a deciderlo è **la pagina**, con l'attributo `scrolling` dell'`iframe` (`no` / `auto`, `InteractiveBlock` in `blocks/blocks.tsx`, e la stessa regola nell'anteprima locale). Il guscio non dice più niente sulla barra e il limite non è scritto dentro al guscio. Tre ragioni:

1. **Il limite è della pagina**, che è già lei a tenere i 2000 px.
2. **Il frame di una pagina pubblica è in cache per un anno**, `immutable` (`EmbedEndpoints`), nel browser di chi l'ha già letto e in ciò che sta davanti al server: il suo indirizzo ha il numero della versione, non quello del guscio. Una regola nel guscio non arriverebbe alle versioni già pubblicate; l'attributo sì.
3. **Un frammento non può cambiarla**, nemmeno scrivendo `html { overflow: scroll }`.

Provato che regge: cambiato dopo il caricamento, avanti e indietro, `scrolling` viene applicato da Chromium, Firefox e WebKit (Playwright, su Windows), con la rotella che scorre e non scorre di conseguenza.

**La regola, intorno al limite.** Oltre i 2000 px la barra toglie larghezza e il disegno si accorcia: 2005 px senza barra sono 1977 con la barra, e presa alla lettera la barra se ne andrebbe, poi tornerebbe, per sempre. Quindi una volta che c'è, la barra se ne va solo quando quello che il frammento dice ci starebbe alla larghezza che la barra toglieva (20 px, un po' più di quanti ne prende). Una prova per ogni altezza da 1975 a 2060 px controlla che la misura si fermi; con la regola ingenua, 2001, 2010 e 2030 oscillano. Un disegno appena sopra il limite può restare con una barra che non ha niente da far scorrere: la sua altezza senza barra è comunque oltre i 2000 px.

**Il guscio vecchio** (una versione già pubblicata, in cache), provato con le stesse prove: nessuna barra sui disegni, oltre i 2000 px la barra c'è, nessuna oscillazione. Le prove che falliscono sono quelle di ciò che il guscio vecchio non misura (fuori dal flusso, margine negativo, `body` alto come la cornice, pannello aperto da una scelta): **lì la parte che esce resta tagliata, senza barra, finché la pagina non si ripubblica**. Va detto nelle note di rilascio. Cambiare la cache, o mettere la versione del guscio nell'indirizzo, è un'altra cosa e non è qui.

### Limiti che restano

Quello che trabocca e poi sparisce lascia spazio vuoto finché la larghezza o il flusso non cambiano; un `body` che ha un'altezza in `%` o in `vh` non fa scendere la cornice; quello che è più largo della cornice viene tagliato senza che nessuno lo dica, e le istruzioni chiedono la prova a 360 e a 768 px per questo. Il ciclo della barra si è potuto provare solo su Chromium: Firefox e WebKit, qui, non tolgono larghezza. Il WebKit di Playwright su Windows non è Safari su un iPad.

### Piccole aggiunte per coerenza con le istruzioni

Il pulsante «Tablet, 768 px» nell'anteprima locale, che altrimenti non offriva la prova che le istruzioni chiedono, e `margin: 0` sulla `figure` dell'esempio, che su un telefono perdeva 80 px su 360.

## 3. La domanda per Carmine: un frammento più alto di 2000 px

Con «la cornice non scorre mai» la parte oltre i 2000 px sarebbe stata tagliata, e nessuno avrebbe potuto più raggiungerla. Le tre strade:

- (a) tagliata, con una riga solo per lo staff sotto il blocco;
- (b) tagliata e basta, il design alla lettera;
- **(c) oltre i 2000 px la barra resta.**

**Carmine ha scelto la (c)**, contro la raccomandazione della sessione, che era la (a): «(c) resta la barra» (in chat al master, 5 novembre 2026). Tutto il frammento resta raggiungibile, sul sito pubblico compreso. La riga per lo staff, che la PR aveva scritto per la (a), non serve più ed è stata tolta: la barra si vede da sé, e l'anteprima locale la mostra come il sito. Le linee guida dicono che cosa succede oltre i 2000 px, e che resta una cosa da correggere.

## Da portare nel piano

- **§9.3** (il blocco interattivo): nessuna barra fino a 2000 px, oltre i 2000 px la cornice si ferma e la barra resta (scelta (c) di Carmine, scostamento da `12-design-editor-fase-3.md` §5.5, che dice «la cornice non scorre mai»); la barra la decide la pagina con `scrolling`, non il guscio, e il perché è la cache del frame; la misura del guscio (§2).
- **Le note di rilascio** della versione che porta questa PR: le pagine pubbliche già pubblicate con un interattivo vanno ripubblicate se il frammento disegna fuori dal flusso.
