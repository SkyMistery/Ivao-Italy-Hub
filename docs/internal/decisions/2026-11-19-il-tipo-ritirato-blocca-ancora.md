# Il tipo ritirato blocca ancora, e la legenda lo chiama per nome

**Data:** 19 novembre 2026 — fase di test, fase 5 (c), revisione della PR #538 (nota 5 del revisore).
**Stato:** **decisa** da Carmine il 19 novembre 2026, in chat al master, con le parole «Sì, finché si toglie», riportate sulla PR:
https://github.com/SkyMistery/Ivao-Italy-Hub/pull/538#issuecomment-7012296208.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: completa il meccanismo della nota `2026-11-18-il-calendario-libero-e-occupato`
(già unita, e per questo non toccata) in un punto che quella nota non diceva.

## La domanda

Un tipo del calendario si ritira (`IsActive = false`) senza cancellarlo: nessuno ci scrive più voci nuove, ma quelle già scritte
restano dove sono. Se quel tipo **blocca** (`Blocks`), le sue voci continuano a togliere ore a chi sceglie una data? E il bootstrap
porta solo i tipi in uso, quindi la legenda del selettore mostrava la chiave grezza del tipo ritirato.

## La risposta di Carmine

**(a), «finché si toglie»**: un tipo ritirato che blocca **continua a bloccare finché non si toglie la spunta** «Blocca le ore» nella
schermata dei tipi. Ritirare un tipo ferma le voci nuove, non cambia che cosa fanno quelle che ci sono. L'alternativa scartata era
(b): un tipo ritirato smette di bloccare da solo.

## Che cosa cambia nel codice (#538)

- **La risposta di `/api/calendar/busy`** porta per ogni intervallo `kindLabel` e `kindColour`, letti dalla tabella dei tipi, quelli
  ritirati compresi (`CalendarBusyDto`). Sono null solo per una parola che un modulo ha proiettato e che nessuna tabella conosce.
- **Il selettore** (`vocabularyOf` in `web/src/shared/forms/busy.ts`) aggiunge ai tipi del bootstrap quelli che la risposta nomina e
  il bootstrap non porta. Così la legenda, i pallini e le righe sotto le ore non mostrano mai la chiave grezza di un tipo della
  tabella.
- **I test**: `CalendarBusyEndpointTests.ARetiredKindKeepsBlockingAndTheAnswerNamesIt` controlla che blocchi, nome e colore nella
  risposta, che il tipo sia assente dal bootstrap e che il controllo sul server rifiuti. Accanto, `busy.test.ts` (`vocabularyOf`) e
  un test del form sulla legenda.

## Da portare nel piano

- **§9.5** (calendario): un tipo ritirato che blocca continua a bloccare finché non si toglie la spunta; la risposta del libero e
  occupato porta nome e colore di ogni tipo, ritirati compresi. Accanto a quanto porta la nota `2026-11-18-il-calendario-libero-e-occupato`.
