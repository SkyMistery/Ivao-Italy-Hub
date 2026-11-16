# Il file ancora da scegliere non trattiene il resto del blocco

**Data:** 15 novembre 2026, issue #377. Ramo `fix/image-blocks-377`, in coda alla #528 (#488, parte A).
**Stato:** Decisa da Carmine il 15 novembre 2026, in chat alla sessione master, scritta sulla issue: «A: file da scegliere»
(https://github.com/SkyMistery/Ivao-Italy-Hub/issues/377#issuecomment-6895537436).
**Classificazione** (`CLAUDE.md` §5): (b). Si estende il form «vivo» del nucleo (`SchemaForm` con `onChange`, G15), che
applicava solo i valori che passano lo schema.

## Che cosa serve

Il pannello di un blocco è un form vivo: applica quello che si scrive dopo una pausa, ma solo se tutto lo schema passa
(`useLiveValues`, `safeParse`). Il `mediaId` di un'Immagine è obbligatorio. Così, finché il file non è scelto, nulla di quello che
si scrive nel blocco viene applicato. Lasciando il blocco il form si smonta, e il testo alternativo scritto per primo si perde
senza un messaggio. Lo stesso vale per ogni campo di un blocco con un file obbligatorio ancora vuoto: le voci di una Galleria e
i loghi.

## Le alternative (chieste a Carmine)

| | Che cosa | Pro | Contro |
|---|---|---|---|
| **A** | Il form vivo applica anche quando **l'unica** cosa che lo schema rifiuta è un file ancora da scegliere | È lo stato che la 3.15 ha già reso noto: la bozza lo tiene, la pubblicazione lo ferma e nomina il blocco. Il test che c'è («un campo obbligatorio svuotato a metà non è un valore») resta com'è | Tocca il nucleo del form |
| B | Applica con qualunque campo obbligatorio vuoto | Più largo | Tocca anche i form vivi della pagina (titolo, indirizzo), e una bozza potrebbe arrivare al server con quello che il server rifiuta |
| C | Non applica, ma il pannello lo dice | Niente di nuovo nel form | Il lavoro si perde lo stesso, solo annunciato |

Scelta: **A**.

## Com'è fatto

- **`filesToChoose(fields, issues, values)`** (`shared/forms/schema.ts`) risponde con i percorsi dei file mancanti quando
  **tutti** i rifiuti dello schema sono campi media che non hanno niente (`invalid_type` e il valore assente), anche nelle voci
  di una lista. Se c'è qualcos'altro risponde `null`.
- **`applicable`** (`SchemaForm.tsx`) chiede ancora lo schema con un numero al posto di ogni file mancante, e poi lo toglie.
  Così ogni altra regola dello schema (un `refine`, un valore predefinito, una trasformazione) dice ancora la sua, e quello che
  esce è il dato dello schema e non i valori grezzi del form. Il file mancante non c'è: niente chiave e niente segnaposto.
- **Non cambia niente** per un form che passa, né per un form che rifiuta qualcos'altro: un testo troppo lungo, un titolo
  svuotato. Il campo dice l'errore e non si applica nulla, come prima.
- Il form che si invia con un pulsante (`onSubmit`) non cambia: la regola vale solo per il form vivo.

## Da portare nel piano

- **§16** (meccanismi generici), al form generato vivo (G15): applica quello che lo schema accetta, e anche quando l'unica cosa
  che manca è un file ancora da scegliere. Quel file resta fuori dal valore applicato, e la pubblicazione lo nomina
  (`errors.content.mediaToChoose`, 3.15).
