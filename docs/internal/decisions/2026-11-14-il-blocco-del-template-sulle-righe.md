# Il blocco del template arriva alle righe della sezione

- **Data**: 14 novembre 2026
- **Stato**: **decisa da Carmine** il 14 novembre 2026, come raccomandato: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/513#issuecomment-6883306581. Correzione di un difetto (issue #460); la domanda che la issue lasciava aperta è sotto, «La domanda per Carmine»
- **Chi**: la sessione di lavoro `fix/editor-460-461`
- **Classificazione** (`CLAUDE.md` §5): **(b)**. Il meccanismo c'è (`templateRules.ts`: le regole del template lette per `key`); lo si estende alle righe, invece di una toppa in ogni lettore

## Il difetto

L'editor leggeva la regola del template con la `key` della sezione di cui si chiedeva. Una riga senza `key`, o con una `key` che il template lascia libera, dentro una sezione `locked` non aveva regola: la sua barra offriva «Aggiungi riga», le frecce, «Duplica» ed «Elimina», e lo stesso il suo riquadro nella struttura. L'import di un file di Claude accettava un blocco aggiunto in quella riga.

## La lettura

La issue chiedeva se il blocco del template arrivi alle righe. Per una riga **senza `key`** il piano e il design rispondono già: una sezione `locked` è «struttura fissa: si compila, non si ristruttura» (piano §9.3), e nell'editor «mostra i campi, non la struttura. Niente aggiungi blocco, niente sposta, niente elimina» (design M1 §1.4). Le righe di una sezione sono la sua struttura. Quindi una riga senza `key` dentro una sezione `locked` è `locked` anche lei, a ogni profondità. Arriva in basso solo il blocco: `required` e `allowedBlocks` restano quelli della riga, e su una riga bloccata non si elimina e non si aggiunge comunque niente.

## La domanda per Carmine

Il piano e il design non dicono che cosa succede a una riga **a cui il template stesso dà una `key` e che lascia libera** dentro una sezione `locked` (la cornice fissa, la riga dentro libera). La PR la blocca come le altre.

- **Raccomandata: bloccata anche lei.** Una sola regola, la stessa per la struttura, la barra sulla pagina e l'import: «dentro una sezione `locked` non si ristruttura niente». Il prezzo: chi scrive un template non può più dire «cornice fissa, riga libera dentro», e le impostazioni di quella riga sulla pagina diventano di sola lettura.
- **L'alternativa: libera.** La riga con una `key` sua e senza `locked` resta libera; il blocco arriva solo alle righe senza `key`. Si cambia `ruleInside`. Il prezzo: in una sezione «fissa» una riga si può spostare, duplicare ed eliminare, e la forma della sezione cambia lo stesso.

**La risposta di Carmine** (14 novembre 2026, https://github.com/SkyMistery/Ivao-Italy-Hub/pull/513#issuecomment-6883306581): **bloccata anche lei**, come fa la PR. Il blocco di una sezione `locked` arriva alle sue righe, anche a quella a cui il template dà una `key` e che lascia libera. Chi scrive un template e vuole una riga libera la mette fuori dalla sezione bloccata.

## Che cosa cambia

- `templateRules.ts`: `ruleInside(intorno, propria)` e `ruleOfSection(regole, corpo, id)`, che percorre la sezione dall'alto e porta giù il blocco.
- I tre lettori leggono quella: la barra sulla pagina e il pannello (`BodyEditor`), la struttura (`SectionTree`), l'import del file di Claude (`claudeFile.ts`). Le istruzioni per Claude lo dicono in una riga. Nell'import una riga nuova in una sezione bloccata è segnalata una volta sola, dalla sezione.
- Il trascinamento sulla pagina (rilievo 1 della revisione della #513): la maniglia sulla barra di una sezione scelta compare solo dove la barra offre le frecce, come già per un blocco, e `onDragEnd` rifiuta una sezione bloccata. Prima una riga di una sezione bloccata, e la sezione stessa (già su `main`), si riordinavano trascinandole, mentre la struttura non offriva la maniglia.
- Il server non cambia: non conosce i vincoli di una pagina (li toglie alla copia, design M0 §5.6) e non li controllava neanche prima.

## Da portare nel piano

- **§9.3** (il template), a `locked`: il blocco vale anche per le righe della sezione, a ogni profondità, con o senza `key`, anche una riga a cui il template dà una `key` e che lascia libera (una riga libera va fuori dalla sezione bloccata); `required` e `allowedBlocks` restano della riga. Sulla pagina una sezione bloccata non si trascina, come nella struttura.
