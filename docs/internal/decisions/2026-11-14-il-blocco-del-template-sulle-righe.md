# Il blocco del template arriva alle righe della sezione

- **Data**: 14 novembre 2026
- **Stato**: correzione di un difetto (issue #460); la scelta che la issue lasciava aperta è letta dal design M1 §1.4, non presa qui. Se Carmine la legge diversamente, si cambia una funzione sola (`ruleInside`)
- **Chi**: la sessione di lavoro `fix/editor-460-461`
- **Classificazione** (`CLAUDE.md` §5): **(b)**. Il meccanismo c'è (`templateRules.ts`: le regole del template lette per `key`); lo si estende alle righe, invece di una toppa in ogni lettore

## Il difetto

L'editor leggeva la regola del template con la `key` della sezione di cui si chiedeva. Una riga senza `key`, o con una `key` che il template lascia libera, dentro una sezione `locked` non aveva regola: la sua barra offriva «Aggiungi riga», le frecce, «Duplica» ed «Elimina», e lo stesso il suo riquadro nella struttura. L'import di un file di Claude accettava un blocco aggiunto in quella riga.

## La lettura

La issue chiedeva se il blocco del template arrivi alle righe. Il design lo dice già: una sezione `locked` è «struttura fissa: si compila, non si ristruttura» (piano §9.3), e nell'editor «mostra i campi, non la struttura. Niente aggiungi blocco, niente sposta, niente elimina» (design M1 §1.4). Le righe di una sezione sono la sua struttura. Quindi **una riga dentro una sezione `locked` è `locked` anche lei, con o senza `key`, a ogni profondità**. Arriva in basso solo il blocco: `required` e `allowedBlocks` restano quelli della riga, e su una riga bloccata non si elimina e non si aggiunge comunque niente.

## Che cosa cambia

- `templateRules.ts`: `ruleInside(intorno, propria)` e `ruleOfSection(regole, corpo, id)`, che percorre la sezione dall'alto e porta giù il blocco.
- I tre lettori leggono quella: la barra sulla pagina e il pannello (`BodyEditor`), la struttura (`SectionTree`), l'import del file di Claude (`claudeFile.ts`). Le istruzioni per Claude lo dicono in una riga.
- Il server non cambia: non conosce i vincoli di una pagina (li toglie alla copia, design M0 §5.6) e non li controllava neanche prima.

## Da portare nel piano

- **§9.3** (il template), a `locked`: il blocco vale anche per le righe della sezione, con o senza `key`, a ogni profondità; `required` e `allowedBlocks` restano della riga.
