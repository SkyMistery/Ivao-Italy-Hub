# Le parole dei controlli che il browser fa con lo schema

**Data:** 14 novembre 2026, issue #386 (giro dei tester). Ramo `fix/forms-words-2`, in coda alla #501 (#384).
**Stato:** Proposta. Domanda a Carmine posta dal master il 14 novembre 2026; risposta: _da scrivere, con il link al commento_.
**Classificazione** (`CLAUDE.md` §5): (c). Un meccanismo nuovo del nucleo: le parole di un controllo che oggi nessuno traduce.

## Che cosa serve

Un modulo generato (`SchemaForm`) controlla nel browser lo schema zod prima di mandare la richiesta
(`zodResolver(schema)`, `web/src/shared/forms/SchemaForm.tsx`), e sotto il campo disegna il messaggio che zod gli dà, così
com'è (`FieldError`). Nessuno schema di `web/src` passa un messaggio a zod, non c'è una mappa d'errori, e zod parla inglese:
sotto il SID del report del pilota si legge «Too big: expected string to have <=16 characters», a qualunque lingua dello schermo.
Lo stesso per ogni `max`, `min`, campo richiesto o formato che il browser controlla, in ogni modulo e nel nucleo (≈ 220
controlli tra `modules/`, `features/` e `shared/`).

Il server, per le stesse regole, risponde con chiavi i18n (`errors.required`, `errors.text.tooLong`, `errors.number.min`,
`errors.number.range`, …) che `useProblemDetails` trasforma in frasi in un posto solo. Il browser deve dire le stesse parole, senza
copiare le regole.

## Perché nessun meccanismo esistente basta

`useProblemDetails` traduce solo le risposte del server. `CLAUDE.md` §2 «Validation» dice che le regole non si scrivono due volte:
le regole ci sono già, una volta, nello schema; mancano le **parole** del browser, e oggi non c'è un posto che le dica.

## Le strade

**(A) — raccomandata. Una mappa d'errori del nucleo, nel generatore.** Un solo pezzo in `web/src/shared/forms/` traduce i
problemi di zod nelle chiavi che il server già usa, risolte con la stessa `t` della lingua sullo schermo:

| Problema di zod | Chiave |
|---|---|
| `too_big` su un testo | `errors.text.tooLong` (del server) |
| `too_small` su un testo con minimo 1, `invalid_type` su un valore assente | `errors.required` (del server) |
| `too_small` su un numero | `errors.number.min` (del server) |
| `too_big` su un numero | `errors.number.range` (del server) |
| tutto il resto (formato, elenco troppo corto o lungo, scelta non valida) | **una chiave nuova**, `errors.invalid` («Questo valore non è valido.» / «This value is not valid.») |

La mappa si dà **solo al generatore** (`zodResolver(schema, { error })`, l'opzione di zod 4 per una validazione), non a tutto
zod con `z.config`: i messaggi si leggono solo sotto un campo di un modulo generato, e le altre letture di zod (`validateSearch`
delle rotte, i payload) non ne hanno bisogno. Uno schema che un giorno scrivesse un messaggio suo lo terrebbe: in zod 4 il messaggio
dello schema vince su quello della validazione.

Tocca: un file nuovo in `shared/forms/` con il suo test, una riga in `SchemaForm.tsx`, `errors.invalid` in `locales/en` e
`locales/it`. Nessuno schema, nessuna regola copiata, ogni modulo coperto in un colpo.

**(B) — alternativa. Lasciare al server le regole che il server controlla.** Togliere dagli schemi del browser `max`, `min` e
simili, e lasciare che la risposta del server (già tradotta) arrivi sul campo. È la strada già presa per un caso: la #491 (lotto 10,
punto 2, `49f0bdda`) ha tolto i `min`/`max` dagli schemi **live** dei tour (`disputeSchema`, `legIssueReportSchema`,
`diversionSchema`, `atcDeclarationSchema`), ma per un'altra ragione: un modulo live applica solo quello che lo schema accetta, e lo
schermo restava fermo all'ultimo testo valido. Come regola generale costa: ≈ 220 controlli da togliere in tutti i moduli, nessun
avviso prima del salvataggio, e nei moduli live nessun avviso finché non si conferma. E resterebbero comunque i controlli che solo
il browser può fare (un campo richiesto di un modulo che non va al server, un formato): parlerebbero ancora inglese.

**Raccomandazione: (A).** Una regola, un posto, le parole del server; (B) resta giusta dove la #491 l'ha usata, per i moduli
live.

## Da portare nel piano

- §16 (meccanismi generici), la riga «Validazione»: il browser dice le parole del server con la mappa d'errori del generatore
  (`shared/forms/`), mai messaggi scritti negli schemi, mai una seconda copia delle regole.
- Design M0 §7.5 (il generatore dei moduli): i messaggi del controllo nel browser passano dalla stessa mappa.
- `CLAUDE.md` §2, tabella, riga «Validation»: aggiungere la mappa d'errori del generatore accanto a `ProblemDetails`.
