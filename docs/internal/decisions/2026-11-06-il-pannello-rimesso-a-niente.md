# Un genitore rimesso a «niente» nel dialogo di approvazione disegna niente

**Data:** 6 novembre 2026, branch `fix/review-panel-back-to-nothing`, dopo le PR #466 e #468.
**Stato:** correzione di un difetto, nessuna scelta da fare per Carmine: il comportamento giusto è che il dialogo mostri ciò che manda. Lo scrive la
sessione di lavoro, incaricata dal master.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: corregge un pezzo del nucleo, `features/content/ReviewPanel.tsx`, senza aggiungere niente. È la
«causa 2» di `2026-11-06-il-campo-rimesso-a-niente.md`, segnalata e non misurata in `2026-11-06-il-filtro-rimesso-a-tutti.md`.

## Il difetto, misurato

I due `Select` del dialogo di approvazione, la pagina genitore (`review-parent`) e il genitore nel menu (`review-menu-parent`), senza `value` passano a
non controllati e ridisegnano l'ultima scelta. Il test nuovo (`ReviewPanel.test.tsx`) è rosso su 5 di 9 prima della correzione:

- **Scelto un genitore e tornati a «niente»**: il dialogo disegna ancora il genitore, per tutti e due i `Select`.
- **La pagina nata con un genitore** il primo giro lo fa bene, perché è nata controllata; toglierlo, ridarlo e toglierlo di nuovo ha lo stesso sintomo.
- **La stessa scelta non si può rifare**, come sul filtro: il dialogo mostra il genitore e il pannello tiene niente.

**Qui il difetto non è solo di disegno, e questo è ciò che il filtro e il form non avevano.** Il dialogo pubblica: chi approva legge «sotto `/pilots`»,
preme «Approva e pubblica» e la richiesta manda `parentId: null` (misurato: `Expected 2, Received null`; per il menu `Expected 7, Received null`). La pagina
va online in cima al sito, con un indirizzo diverso da quello mostrato, e la voce di menu in cima al menu, senza che niente lo dica.

## La correzione

I due `Select` ricevono sempre un valore: `value={parent}` e `value={menuParent}`, con `''` per niente. `''` fa disegnare il segnaposto, che qui dice già
la cosa giusta («In cima al sito», «In cima al menu»), lo stesso di un dialogo appena aperto. Non `NO_CHOICE`, per il motivo delle due note: è il valore della
voce nell'elenco, non il «niente» del controllo. Lo stato del pannello e ciò che manda non cambiano.

## Visto e lasciato fuori

- **`features/content/TemplatePicker.tsx:100`** (`newParent`, la pagina genitore di una pagina nuova) ha la stessa forma e la stessa voce `NO_CHOICE`: dal
  codice è la stessa causa, non è stato misurato né corretto qui. Segnalato al master.
- **`TemplatePicker.tsx:88`** (`templateId`) e **`shared/ui/LocaleSwitcher.tsx:51`** passano un `value` facoltativo, ma non offrono un ritorno a «niente»;
  di `LocaleSwitcher` lo dice già la nota sul filtro. Letti, non misurati.

## Da portare nel piano

- **§16** (meccanismi generici), accanto alla regola portata dalle note `2026-11-06-il-campo-rimesso-a-niente` e `2026-11-06-il-filtro-rimesso-a-tutti`:
  «un `Select` riceve sempre un `value` (`''` per niente)» vale anche per un `Select` scritto a mano in una schermata, non solo per il form generato e per il
  filtro di un elenco.
