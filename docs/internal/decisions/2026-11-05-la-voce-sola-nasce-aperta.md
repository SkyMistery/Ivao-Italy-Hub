# La voce sola di un form nasce aperta

**Data:** 5 novembre 2026, fase 3 dell'editor, PR 3.3, branch `feat/form-repeated-items`.
**Stato:** **decisa** — **Carmine in chat al master, 5 novembre 2026**: «Sì, aperta». La risposta è stata data in chat al master e non in
un commento: il lavoro è di una sessione di Carmine, non del collaboratore.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: estende un meccanismo del nucleo che la PR sta già cambiando, il form generato
(`shared/forms/SchemaForm.tsx`). Il design (`12-design-editor-fase-3.md` §4.3) non prevedeva il caso.

## Che cosa serve

Il §4.3 chiude ogni voce di un elenco, tranne quella appena aggiunta e quella con un errore. Ma quattro form nascono con **una voce già lì**:
la proposta di date del training (`EMPTY_PROPOSAL`), il report del pilota e quello del controllore (`emptyPilotReport`, `emptyAtcReport`) e la
disponibilità (`emptyAvailability`). Con la regola scritta, un pilota che apre il report troverebbe una riga «Voce 1» da cliccare prima di
qualunque cosa da scrivere, e una voce sola, chiusa, non fa posto a niente.

Misurato: con la regola scritta `e2e/training-dates.spec.ts` ha due test rossi; a leggerle, la stessa causa tocca altre quattro spec.

## La decisione

**Un elenco che all'apertura del form ha una voce sola la mostra aperta.** Con due o più voci vale la regola del design.

- Vale per **ogni** elenco di una voce sola all'apertura del form, non solo per un form nuovo: una voce sola già salvata si apre anch'essa.
- Si guarda **solo all'apertura**. Chi ci arriva togliendo le altre voci non vede aprirsi niente, perché nessuno gliel'ha chiesto.
- Resta una voce come le altre: un clic la chiude, e aggiungerne un'altra la chiude.

## Alternative scartate

- **Chiusa, come dice il §4.3**: il clic in più di sopra, e sei spec da cambiare, due delle quali con commit di `dalberone`.
- **Aperte solo le voci vuote**: il report del pilota nasce con il giorno già scritto e la disponibilità con le ore dell'evento, quindi resterebbero
  chiusi proprio i form che servivano.

## Da portare nel piano

- **`12-design-editor-fase-3.md` §4.3**: un elenco che all'apertura del form ha una voce sola la mostra aperta (Carmine, 5 novembre 2026).
- **§16** (meccanismi generici): nella riga del form generato con le voci chiuse, la stessa eccezione.
