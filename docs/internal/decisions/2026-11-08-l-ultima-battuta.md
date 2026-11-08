# L'ultima battuta prima di un tasto che salva

- **Data**: 8 novembre 2026
- **Stato**: correzione di un difetto, verificata con misure; nessuna scelta nuova chiesta a Carmine. Il sospetto è del worker della PR 3.7 (#476); Carmine ha chiesto che lo verificasse un altro worker
- **Chi**: la sessione di lavoro `fix/autosave-last-keystroke`
- **Classificazione** (`CLAUDE.md` §5): **(b)**. Il meccanismo c'è già (il form dal vivo di `SchemaForm` e l'autosave di `useAutosave`) e copre il caso **quasi** del tutto. Lo si estende in un punto solo, invece di una toppa per ogni tasto

## 1. Il difetto, misurato

I tasti che salvano la bozza prima di fare qualcosa («Scarica per Claude», «Pubblica», «Pronta per la revisione», «Apri l'anteprima» della #476) chiamano `useAutosave.flush`. `flush` salva lo snapshot che l'editor gli ha già passato. L'editor però lo costruisce da ciò che il form dal vivo **ha applicato**, e il form applica una pausa dopo l'ultima battuta (`LIVE_DELAY` = 150 ms in `SchemaForm.useLiveValues`). La stessa pausa vale per i campi di un blocco (`BlockProperties` usa lo stesso form). Se il tasto arriva dentro la pausa, `flush` trova lo snapshot uguale all'ultimo salvato, risponde `true` e non salva niente: il tasto lavora sulla bozza **senza l'ultima battuta**.

Misure con `pnpm e2e` (Chromium, bundle di produzione, API finta), sul campo «Indirizzo» del form della pagina; per «Apri l'anteprima», sul titolo. Si scrive con `fill` oppure con `keyboard.type`, poi si fa subito il clic. Lo scarto fra l'ultima battuta e il clic è misurato nella pagina (`input` → `pointerdown`):

| Tasto | Scarto battuta → pressione che salva | Battuta persa, prima | Dopo la correzione |
|---|---|---|---|
| Scarica per Claude | 35–57 ms | **40 su 40** | 0 su 20 |
| Apri l'anteprima (#476) | 31–86 ms | **18 su 20** (le 2 salvate: clic a 205 e 225 ms) | 0 su 20 |
| Pubblica (dialogo, poi Conferma) | 299–389 ms (la Conferma) | 0 su 40 | 0 su 20 |
| Pronta per la revisione (dialogo, poi Conferma) | 300–356 ms (la Conferma) | 0 su 40 | 0 su 20 |

Per trovare la soglia, sul codice vecchio, sul solo «Scarica per Claude» con una pausa voluta prima del clic, 10 prove per riga:

| Clic dopo la battuta | Battuta persa |
|---|---|
| 148–189 ms | 3 su 10 |
| 184–229 ms | 0 su 10 |
| 203–379 ms | 0 su 40 |

**La finestra è di 170–190 ms**: i 150 ms della pausa più un render. «Pubblica» e «Pronta» non ci cadono, perché chiedono una seconda pressione su un dialogo che deve prima comparire. «Scarica per Claude» e «Apri l'anteprima» sono una pressione sola.

Un clic che parte dal mouse, con la mano che lascia la tastiera, non ci arriva. Ci arriva una pressione col puntatore già sul tasto: un tocco sul trackpad, o il mouse nell'altra mano. Ci arriva sempre un test che fa `fill` e poi `click`. Il difetto è stretto ma vero. La correzione costa una riga nel meccanismo. Dovrebbe chiudere anche un'altra strada, che non ho provato: un blocco lasciato per un altro dentro la pausa, dove lo smontaggio del form dei suoi campi cancella il timer e con lui la battuta.

## 2. La correzione

**Quando il fuoco lascia un form dal vivo, ciò che la pausa sta trattenendo si applica subito** (`onBlur` del `<form>` di `SchemaForm`, che in React è `focusout` e risale dai campi). Il mouse sposta il fuoco alla pressione, prima del clic. Il Tab lo sposta prima dell'Invio. Così lo stato dello schermo ha l'ultima battuta prima che il tasto sia gestito. Fra due campi dello stesso form la battuta arriva solo un po' prima. Un valore che lo schema rifiuta resta non applicato, come dopo la pausa.

Le alternative scartate:

- **Una toppa per tasto** (aspettare la pausa, o leggere il form, in ogni `onClick`): sono quattro copie della stessa regola, più una per ogni tasto futuro (`CLAUDE.md` §2).
- **`flush` che legge il form direttamente**: l'autosave non sa che cos'è una bozza e riceve una stringa (`useAutosave`, ⚠️ in testa). Dovrebbe chiedere al form di applicare e poi aspettare un render prima di leggere lo snapshot e il `save` nuovi: diventerebbe una seconda copia dello stato dell'editor.
- **Togliere la pausa** del form dal vivo: ridisegnerebbe la pagina a ogni lettera, che è il motivo per cui la pausa esiste.

Che cosa resta fuori: un tasto che non sposta il fuoco. Nessuno di quelli che chiamano `flush` lo fa oggi. Lo stesso vale per la chiusura della scheda con una scorciatoia (Ctrl+W) dentro la pausa: `beforeunload` legge `dirty` prima che la battuta sia applicata, e la scheda si chiude senza chiedere.

## 3. Le prove

- `web/src/features/content/useAutosave.lastKeystroke.test.tsx`: l'editor in piccolo (form dal vivo, snapshot, tasto che chiama `flush`), col clic e con Tab + Invio. Rosso prima: `save` mai chiamato, `flush` risponde «salvato». Verde dopo.
- `web/e2e/claude-download.spec.ts`, «a press right after the last keystroke stores the draft with that keystroke first»: `fill` e poi subito il clic, nel browser. Rosso prima, 5 su 5: il file arriva come `welcome.claude.md`, cioè la bozza senza la battuta. Verde dopo, 10 su 10.

## Da portare nel piano

- **§16** (meccanismi generici), al form generato dal vivo (la settima estensione, G15): applica dopo una pausa, **e subito quando il fuoco lo lascia**, così un tasto premuto subito dopo l'ultima battuta salva la bozza con quella battuta.
- **Il changelog**: la versione 0.24.1, che corregge un difetto su 0.24.0, non ancora rilasciata.
