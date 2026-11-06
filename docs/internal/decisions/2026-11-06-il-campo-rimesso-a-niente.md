# Un campo facoltativo rimesso a «niente» disegna niente

**Data:** 6 novembre 2026, branch `fix/form-optional-reset`, dopo la PR 3.3 dell'editor.
**Stato:** correzione di un difetto, nessuna scelta da fare per Carmine: il comportamento giusto è quello che il form già salvava. Lo
scrive la sessione di lavoro, incaricata dal master.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: estende un meccanismo del nucleo, il form generato (`shared/forms/SchemaForm.tsx`), senza
aggiungerne uno. Nessuna nuova voce nell'elenco chiuso dei componenti: è un pezzo interno del file.

## Il difetto, misurato

Rimesso a «niente», un campo restava disegnato col valore vecchio mentre il form teneva e salvava niente. La PR 3.3 l'aveva trovato e corretto
per la data. Misurati gli altri, con un test rosso prima della correzione (18 su 20 in `SchemaForm.optional.test.tsx`):

- **Enum facoltativo**: `Select` mostra ancora la scelta.
- **Icona facoltativa**: la griglia segna ancora l'icona e non «nessuna».
- **Media facoltativo**: la miniatura resta segnata e «Non scegliere nessuno» resta lì. È il caso peggiore: premere un'immagine scelta la toglie,
  quindi premendola di nuovo la persona non poteva più sceglierla, e il form salvava niente.
- **Scelta da un elenco di parole** (`choices` su una stringa): stesso sintomo, causa diversa, qui sotto.

## Le due cause

1. **Il controller** (quella della 3.3): il valore di un `Controller` è osservato con il valore con cui è nato come predefinito, quindi
   `undefined` torna a essere il valore di partenza. E dopo un `reset` il controller che si registra riscrive quel valore sopra `undefined`.
2. **Il `Select`, ed è nuova**: Radix, senza `value`, passa a non controllato e ridisegna l'ultima scelta che aveva tenuto mentre era tale. Basta
   nascere vuoto, scegliere e tornare a «niente»; oppure togliere due volte. Con la sola causa 1 corretta l'enum restava sbagliato.

## La correzione

- **Un pezzo solo**, `HeldField`: le due metà della 3.3 (`useWatch` per ciò che si disegna, il controller che rinasce su `rowsTaken`). Lo usano
  enum, icona, media e `Instant`, che così non le ha più per conto suo. Per un campo che torna a `undefined` si usa questo, mai un `Controller` nudo.
- **Il `Select` riceve sempre un valore**: `''` quando non c'è niente, cioè lo stesso segnaposto di un form nato vuoto. Non `NO_CHOICE`: mostrerebbe la
  parola «nessuno» dove oggi un form nuovo non ne mostra, ed è un altro cambiamento visibile, che nessuno ha chiesto.

## Alternative scartate

- **La stringa vuota al posto di `undefined`**: `z.string().optional()` l'accetta, quindi il form manderebbe `""` dove non deve esserci niente
  (già scartata dalla 3.3), e per il media non esiste.
- **`useWatch` dentro `Field`**: `Field` esce prima per i campi nascosti e passa da ogni tipo di campo, e un osservatore per ogni campo del form non
  serve a chi non torna mai a niente.

## Fuori da questa correzione, lasciato com'è

- **`ListFilter`** (`shared/list/ListFilter.tsx`) ha la causa 2, misurato: dopo una scelta e il ritorno a «tutti», `held=NOTHING drawn=Alpha`. Il filtro è
  a posto ma ne mostra un altro. Non è il form generato: segnalato al master. Anche `LocaleSwitcher` passa un `value` facoltativo, ma non ha un ritorno
  a «niente».
- **Il numero con `choices`**: facoltativo o no, il suo `Select` non offre la via di ritorno. Non è questo difetto, ma il gesto che manca.

## Da portare nel piano

- **§16** (meccanismi generici), nella riga del form generato: un campo che può tornare a `undefined` passa da `HeldField`, e un `Select` riceve sempre un
  `value` (`''` per niente).
- **`12-design-editor-fase-3.md` §4.3**: la correzione della data vale per enum, icona e media.
