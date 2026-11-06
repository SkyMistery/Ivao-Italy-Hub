# Un filtro rimesso a «tutti» disegna «tutti»

**Data:** 6 novembre 2026, branch `fix/list-filter-back-to-all`, dopo la PR #466.
**Stato:** correzione di un difetto, nessuna scelta da fare per Carmine: il comportamento giusto è quello che il filtro già applicava all'elenco. Lo
scrive la sessione di lavoro, incaricata dal master.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: corregge un pezzo del nucleo, `shared/list/ListFilter.tsx`, senza aggiungere niente. È la
«causa 2» di `2026-11-06-il-campo-rimesso-a-niente.md`, che lì era stata lasciata fuori perché il filtro non è il form generato.

## Il difetto, misurato

Scelto un valore e tornati a «tutti», l'elenco è di nuovo intero, ma il `Select` del filtro disegna ancora la scelta vecchia. Radix, senza `value`,
passa a non controllato e ridisegna l'ultima che teneva. Il test nuovo (`ListFilter.test.tsx`) è rosso su 3 di 7 prima della correzione:

- **Il ritorno a «tutti»**: tiene niente e disegna `Alpha`.
- **Il filtro azzerato da fuori** (l'indirizzo che cambia, per esempio): stesso sintomo.
- **Una conseguenza che la #466 non aveva misurato: la stessa scelta non si può rifare.** Per Radix il valore è ancora `alpha`, quindi riscegliere
  `Alpha` non è un cambiamento e `onValueChange` non parte: la persona preme e l'elenco resta intero. Solo un'altra scelta in mezzo sbloccava il filtro.

## La correzione

`ListFilter` passa sempre un valore a `Select`: `value ?? ''`. `''` è ciò che fa disegnare il segnaposto, lo stesso di un filtro nuovo, con `none` come
testo. Niente cambia di aspetto per chi non ha ancora filtrato. Non `NO_CHOICE`, per lo stesso motivo della nota sul form: è il valore della voce «tutti»
nell'elenco, non il «niente» del controllo, e `data-placeholder` non ci sarebbe più.

La firma di `ListFilter` non cambia: i chiamanti continuano a passare `undefined`, e nessuno di loro tocca il proprio codice.

## `LocaleSwitcher`: nessuna modifica

Passa un `value` facoltativo, ma non è lo stesso difetto. `current` è `undefined` solo se la divisione non ha alcuna lingua (`locales` vuoto), e allora
non c'è niente da scegliere né da disegnare. Con almeno una lingua `current` ricade su `locales[0]`, quindi è sempre una stringa, e non esiste la voce per
tornare a «niente». Letto, non misurato a schermo: non c'è un caso da provocare.

## Visto e lasciato fuori

`features/content/ReviewPanel.tsx` (righe 104 e 285, il genitore nel menu e la pagina genitore) scrive `{...(x === '' ? {} : { value: x })}` e offre la voce
`NO_CHOICE`: dal codice è la stessa causa, non è stato misurato né corretto qui. Non è questo pezzo: segnalato al master.

## Da portare nel piano

- **§16** (meccanismi generici), accanto alla regola portata dalla nota `2026-11-06-il-campo-rimesso-a-niente`: «un `Select` riceve sempre un `value` (`''` per
  niente)» vale anche per il filtro di un elenco, non solo per il form generato.
