# Un campo grigio (#367)

**Data:** 24 ottobre 2026 — fase 2 del piano dopo la prova dello staff (`2026-10-22-il-piano-dopo-la-prova-dello-staff.md`,
§4 G, punto h), PR del nucleo prima di quella dei tour
**Stato:** **Proposta**. Carmine ha chiesto i campi «grigi e non si scrivono» (#367); il generatore dei form non sa disegnarli, e
questa nota dice come gli si aggiunge. Chiesto al master il 23 ottobre se va in una PR del nucleo a parte: in attesa della risposta.
**Regola applicata:** `CLAUDE.md` §2 (il form generato dallo schema zod) e §5, caso **(b)**: il generatore copre il caso quasi tutto,
e il pezzo che manca si aggiunge al meccanismo, in una PR del nucleo prima del codice del modulo che la usa (`CLAUDE.md` §0 regola 6).

## 1. Che cosa serve, e perché il generatore non basta

- **Nel form di un tour alcuni campi valgono per un tipo solo**: l'ordine delle rotazioni (tour a hub), la distanza da completare (tour
  a distanza), i sottotour da completare (tour con sottotour). Oggi si scrivono su ogni tour, e il server li ignora sugli altri.
- **Carmine li vuole grigi, non nascosti** (#367): chi legge il form vede che cosa il tour potrebbe avere, e non scrive un valore che
  il server butterebbe.
- **`SchemaForm` sa solo due cose**: un campo disegnato e scrivibile, o un campo portato e non disegnato (`hidden`). Grigio e non
  scrivibile non c'è.

## 2. La forma nel codice

- **`FieldMeta.disabled`** (`web/src/shared/forms/schema.ts`): `.meta({ disabled: true })`. La lettura dello schema la porta come ogni
  altra annotazione.
- **`SchemaForm`** (`Field`): un campo con `disabled` si disegna dentro un `<fieldset disabled>` (opacità 60 %). Il `fieldset`
  spegne **ogni** controllo che contiene, di qualunque tipo — numero, select, interruttore, lista — senza toccare il disegno di
  ciascuno: una riga sola invece di un `disabled` portato in dieci posti.
- **Il valore resta del form e si manda com'è**, come un campo `hidden`: un controllo dentro un `fieldset` spento non è `disabled`
  lui stesso, e `react-hook-form` legge ancora il suo valore (provato dal test).
- **Statico**: chi costruisce lo schema decide. Nel form dei tour il tipo è già fissato quando i tre campi si vedono, perché stanno
  nella scheda «Regole» (#369) e il tipo si sceglie nelle «Impostazioni»; un campo che si spegne mentre si scrive un altro campo non
  serve a nessuno oggi, e non c'è.
- `docs/UI-GUIDELINES.md` aggiunge l'annotazione all'elenco di quello che lo schema può dire.

## 3. Le alternative

- **A (raccomandata): `disabled` nel generatore**, come sopra.
- **B: nascondere i campi degli altri tipi** (`hidden`), senza toccare il nucleo. Meno codice, ma non è quello che Carmine ha chiesto:
  il form cambierebbe forma da un tour all'altro.
- **C: un form scritto a mano per le regole del tour.** Contro `CLAUDE.md` §2: i form del back office sono generati.

## 4. I test

`web/src/shared/forms/extensions.test.tsx`: un campo `disabled` si disegna, è spento, non si scrive, e il valore che aveva arriva a
`onSubmit`. Nel modulo, `web/src/modules/flightops/schemas.test.ts` dice quali campi sono grigi per ogni tipo di tour (nella PR di #367).

## Da portare nel piano

- §16 (meccanismi generici), il generatore dei form: l'annotazione `disabled` accanto a `hidden` e `label`.
- Design M0 §7.5 (il form generato), se elenca le annotazioni.
