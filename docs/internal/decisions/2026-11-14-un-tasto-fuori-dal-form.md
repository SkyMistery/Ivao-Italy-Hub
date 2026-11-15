# Un tasto fuori dal form applica l'ultima battuta, come un clic

- **Data**: 14 novembre 2026
- **Stato**: correzione di un difetto (issue #461); nessuna scelta nuova chiesta a Carmine. Estende la regola di `2026-11-08-l-ultima-battuta.md`
- **Chi**: la sessione di lavoro `fix/editor-460-461`
- **Classificazione** (`CLAUDE.md` §5): **(b)**. Il meccanismo è il form dal vivo di `SchemaForm` (`useLiveValues`); si aggiunge un secondo evento allo stesso ascolto

## Che cosa restava della issue

La issue (5 novembre) è di prima della correzione dell'8 novembre: un clic dentro la pausa del form ora applica la battuta prima di essere gestito. Il caso dell'e2e della #457 (`fill`, poi subito un clic) è quel caso: l'e2e ora non aspetta più che il nome compaia, e così lo prova nel browser.

Restava **un cambio di selezione senza clic**. Esc, ⌘D e ⌘Z agiscono su ciò che è scelto, fuori da un campo (`useSelectionShortcuts`, `useHistoryShortcuts`). Chi esce dal campo con Tab e preme Esc entro la pausa lascia la selezione; il pannello se ne va con lei, il form si smonta, il suo timer viene cancellato e la battuta è persa. Provato in un test dell'editor: rosso tre volte su tre prima, verde dopo.

## La correzione

**Finché una battuta aspetta la sua pausa, un tasto premuto fuori dai campi del form la applica subito**, in cattura sul documento come il clic. Un tasto in un campo del form è altra scrittura e la pausa continua. «Campo» è la stessa cosa per cui l'editor lascia stare i suoi tasti (`isEditableTarget`: input, textarea, select, contenteditable): un tasto lì non porta mai via il form.

Nel browser React disegna la battuta applicata fra l'ascolto in cattura e quello dei comandi, che è in bolla: la lista degli ascoltatori della bolla si prende dopo la cattura, quindi il comando gira con lo schermo nuovo (⌘D duplica il blocco con la battuta). Un test lo fissa con `flushSync` (`useBodyHistory.test.tsx`).

Scartata: **applicare allo smontaggio del form**. Lo schermo che lo smonta ha già risposto al tasto con il corpo che aveva; applicare dopo scriverebbe sopra quello che il tasto ha fatto (il duplicato di ⌘D, l'annullamento di ⌘Z), e sopra la tabella di un blocco ridisegnato (`BlockProperties`, `formTable.drawing`).

Che cosa resta fuori: un cambio di selezione fatto dal codice senza clic né tasto. Oggi non ce n'è.

## Da portare nel piano

- **§16** (meccanismi generici), al form generato dal vivo: applica dopo una pausa, subito a un clic in qualunque punto della pagina **e a un tasto premuto fuori dai suoi campi**, prima che il clic o il tasto siano gestiti.
