# L'ultima battuta prima di un tasto che salva

- **Data**: 8 novembre 2026
- **Stato**: correzione di un difetto, verificata con misure; nessuna scelta nuova chiesta a Carmine. Il sospetto è del worker della PR 3.7 (#476); Carmine ha chiesto che lo verificasse un altro worker
- **Chi**: la sessione di lavoro `fix/autosave-last-keystroke`
- **Classificazione** (`CLAUDE.md` §5): **(b)**. Il meccanismo c'è già (il form dal vivo di `SchemaForm` e l'autosave di `useAutosave`) e copre il caso **quasi** del tutto. Lo si estende in un punto solo, invece di una toppa per ogni tasto

## 1. Il difetto, misurato

I tasti che salvano la bozza prima di fare qualcosa («Scarica per Claude», «Pubblica», «Pronta per la revisione», «Apri l'anteprima» della #476) chiamano `useAutosave.flush`. `flush` salva lo snapshot che l'editor gli ha già passato. L'editor però lo costruisce da ciò che il form dal vivo **ha applicato**, e il form applica una pausa dopo l'ultima battuta (`LIVE_DELAY` = 150 ms in `SchemaForm.useLiveValues`). La stessa pausa vale per i campi di un blocco (`BlockProperties` usa lo stesso form). Se il tasto arriva dentro la pausa, `flush` trova lo snapshot uguale all'ultimo salvato, risponde `true` e non salva niente: il tasto lavora sulla bozza **senza l'ultima battuta**.

Misure con `pnpm e2e` (Chromium, bundle di produzione, API finta), sul campo «Indirizzo» del form della pagina; per «Apri l'anteprima», sul titolo. Si scrive con `fill` oppure con `keyboard.type`, poi si fa subito il clic. Lo scarto fra l'ultima battuta e il clic è misurato nella pagina (`input` → `pointerdown`):

| Tasto | Scarto battuta → pressione che salva | Battuta persa, prima | Dopo, al blur (primo commit) | Dopo, al clic (questa versione) |
|---|---|---|---|---|
| Scarica per Claude | 32–73 ms | **40 su 40** | 0 su 20 | 0 su 40, e il file porta la battuta nel nome 40 su 40 |
| Scarica per Claude, da tastiera (fuoco sul tasto, poi Invio) | 78–97 ms | — | — | 0 su 20 |
| Apri l'anteprima (#476) | 31–86 ms | **18 su 20** (le 2 salvate: clic a 205 e 225 ms) | 0 su 20 | 0 su 20 |
| Pubblica (dialogo, poi Conferma) | 299–389 ms (la Conferma) | 0 su 40 | 0 su 20 | 0 su 20 |
| Pronta per la revisione (dialogo, poi Conferma) | 299–356 ms (la Conferma) | 0 su 40 | 0 su 20 | 0 su 20 |

Per «Scarica per Claude» la misura guarda la `PUT` che parte prima dello scaricamento **e** il nome del file (`welcome-<battuta>.claude.md`). Il nome è il dato dell'indirizzo come lo legge lo schermo al momento della pressione. Le misure di prima guardavano il testo del file: lì l'indirizzo non c'è, quindi quella colonna era falsa per tutte e due le versioni e non diceva niente.

Per trovare la soglia, sul codice vecchio, sul solo «Scarica per Claude» con una pausa voluta prima del clic, 10 prove per riga:

| Clic dopo la battuta | Battuta persa |
|---|---|
| 148–189 ms | 3 su 10 |
| 184–229 ms | 0 su 10 |
| 203–379 ms | 0 su 40 |

**La finestra è di 170–190 ms**: i 150 ms della pausa più un render. «Pubblica» e «Pronta» non ci cadono, perché chiedono una seconda pressione su un dialogo che deve prima comparire. «Scarica per Claude» e «Apri l'anteprima» sono una pressione sola.

Un clic che parte dal mouse, con la mano che lascia la tastiera, non ci arriva. Ci arriva una pressione col puntatore già sul tasto: un tocco sul trackpad, o il mouse nell'altra mano. Ci arriva sempre un test che fa `fill` e poi `click`. Il difetto è stretto ma vero. La correzione costa una riga nel meccanismo. Dovrebbe chiudere anche un'altra strada, che non ho provato: un blocco lasciato per un altro dentro la pausa, dove lo smontaggio del form dei suoi campi cancella il timer e con lui la battuta.

## 2. La correzione

**Finché una battuta aspetta la sua pausa, un clic in qualunque punto della pagina la applica subito.** `SchemaForm.useLiveValues` ascolta `click` in cattura sul documento, solo mentre c'è una battuta in attesa. Il clic ha già il suo bersaglio, e nessun gestore del clic ha ancora letto lo schermo. Anche Invio e Spazio su un tasto sono un clic. Un valore che lo schema rifiuta resta non applicato, come dopo la pausa.

**Il dubbio del master: `flush` legge lo snapshot nuovo?** `useAutosave` aggiorna `latest` in un `useEffect`, dopo il render. Il listener del clic chiama `onChange`, cioè un `setState` della schermata, e subito dopo arriva il gestore del tasto, che chiama `flush`. La risposta è sì, per due ragioni di React 19, e le misure sopra lo confermano (la battuta persa 0 volte su 120 pressioni):
- l'aggiornamento nasce dentro un evento `click`, quindi ha priorità discreta (corsia `SyncLane`). React lo disegna in un microtask, e dopo un render sincrono esegue subito anche gli effetti passivi. Dopo il listener del documento il browser svuota i microtask prima del listener seguente, che è quello di React sulla radice. Quindi il gestore del tasto trova già lo schermo e `latest` aggiornati;
- in ogni caso `flush` legge `latest.current.snapshot` dentro il compito di `exclusive`, cioè in un `then` della coda. Quel microtask parte dopo quello del render, accodato prima. Per questo anche il test in jsdom passa, dove fra i listener i microtask non vengono svuotati.

Una schermata che costruisse lo snapshot in un secondo passo (un `useEffect` che chiama un altro `setState`) uscirebbe da tutte e due le garanzie. Nessuna di quelle che chiamano `flush` lo fa oggi.

**La prima strada era il blur del form** (`onBlur` del `<form>`, primo commit di questa PR), e la CI l'ha bocciata. Il fuoco lascia il campo quando il mouse **scende**, non quando risale. Una schermata che risponde alla modifica con una riga sopra i suoi tasti li sposta fra la pressione e il rilascio, e il clic si perde. È successo in `EventEditor`: `EventForm` è dal vivo, e il suo `onChange` fa comparire la riga `saveFirst` nelle azioni dell'intestazione, sopra il form. In `e2e:full`, `events-registers.spec.ts:123` e `events-staff.spec.ts:238` facevano `fill` e poi il clic su «Save», e nessun `PUT` partiva. Su `main` i due test erano verdi (lettura del master, https://github.com/SkyMistery/Ivao-Italy-Hub/pull/477#issuecomment-6721297431). Col clic in cattura la riga compare dopo che il clic è arrivato al suo tasto. Il caso «la discesa del mouse non applica, il clic sì» è in `SchemaForm.live.test.tsx`: è rosso con la versione al blur e verde con questa.

Le alternative scartate:

- **Una toppa per tasto** (aspettare la pausa, o leggere il form, in ogni `onClick`): sono quattro copie della stessa regola, più una per ogni tasto futuro (`CLAUDE.md` §2).
- **`flush` che legge il form direttamente**: l'autosave non sa che cos'è una bozza e riceve una stringa (`useAutosave`, ⚠️ in testa). Dovrebbe chiedere al form di applicare e poi aspettare un render prima di leggere lo snapshot e il `save` nuovi: diventerebbe una seconda copia dello stato dell'editor.
- **Togliere la pausa** del form dal vivo: ridisegnerebbe la pagina a ogni lettera, che è il motivo per cui la pausa esiste.
- **Il blur**, o `pointerdown`: applicano quando il mouse scende, e spostano i tasti sotto il mouse (sopra).

Che cosa resta fuori: un comando che non passa da un clic. Un controllo che agisce alla discesa del mouse (i trigger di Radix: schede, menu) legge lo schermo prima del clic. Nessuno di quelli che chiamano `flush` lo fa oggi. Resta fuori anche la chiusura della scheda con una scorciatoia (Ctrl+W) dentro la pausa: `beforeunload` legge `dirty` prima che la battuta sia applicata, e la scheda si chiude senza chiedere.

## 3. Le prove

- `web/src/features/content/useAutosave.lastKeystroke.test.tsx`: l'editor in piccolo (form dal vivo, snapshot, tasto che chiama `flush`), col clic e con Tab + Invio. Rosso prima: `save` mai chiamato, `flush` risponde «salvato». Verde dopo.
- `web/e2e/claude-download.spec.ts`, «a press right after the last keystroke stores the draft with that keystroke first»: `fill` e poi subito il clic, nel browser. Rosso prima, 5 su 5: il file arriva come `welcome.claude.md`, cioè la bozza senza la battuta. Verde dopo, 10 su 10.
- `web/src/shared/forms/SchemaForm.live.test.tsx`, «a click inside the pause applies at once, and the mouse going down does not»: rosso con la versione al blur, verde con questa.
- `e2e:full`, `events-registers.spec.ts` ed `events-staff.spec.ts`, in locale su un banco rifatto da capo: **con la versione al blur** (`3f45ab05`) `:123` e `:238` sono rossi, 30 s senza nessuna `PUT`, come nella CI; **con questa** sono verdi tutti e cinque. La lettura del master è riprodotta.

## Da portare nel piano

- **§16** (meccanismi generici), al form generato dal vivo (la settima estensione, G15): applica dopo una pausa, **e subito a un clic in qualunque punto della pagina, prima che il clic sia gestito** (non al blur, che sposta i tasti sotto il mouse). Così un tasto premuto subito dopo l'ultima battuta salva la bozza con quella battuta.
- **Il changelog**: la versione 0.24.1, che corregge un difetto su 0.24.0, non ancora rilasciata.
