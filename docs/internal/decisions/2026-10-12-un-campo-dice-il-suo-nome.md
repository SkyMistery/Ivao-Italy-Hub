# Un campo dice il suo nome (E10o)

**Data:** 12 ottobre 2026 — fase E10o di M4, PR del nucleo (#254)
**Stato:** **decisa** (Carmine, 12 ottobre 2026, risposte date in chat al master e pubblicate da lui sulla #253 su sua istruzione:
[risposte][a253], punto 1): **A, prima una fase del nucleo**. Un campo può portare **il suo nome nello schema**, scritto da chi chiama già
nella lingua sullo schermo, come le etichette di `choices`; una select opzionale di un campo con un nome suo dice «nessuno» con **una
parola del nucleo**. Il form del membro di E16 (le domande di un evento in presenza) la usa, e la sua PR è in coda dopo questa. **Non B**:
i file di lingua non sono un posto dove tenere righe. La domanda l'ha posta la sessione di E16 ([#253][q253]).
**Regola applicata:** `CLAUDE.md` §2 (il form generato dallo schema zod, mai un form scritto a mano) e §5, caso **(b)**: il generatore
copre il caso quasi tutto, e il pezzo che manca si aggiunge al meccanismo, in una PR del nucleo prima del codice del modulo che la usa
(`CLAUDE.md` §0 regola 6).

## 1. Che cosa serve, e perché il generatore non basta

- **Il form del membro è il `SchemaForm` di sempre, con lo schema costruito dalle righe** (design M4 §4-bis, nota
  `2026-09-29-gli-eventi-in-presenza`): ogni domanda di `evt_questions` è un campo che il generatore già disegna — un interruttore per
  un sì o no, una select per una scelta, una casella per scelta per più scelte, un numero, un testo.
- **Il nome di ogni campo è il testo della domanda**, scritto dallo staff dell'evento in ogni lingua della divisione. `SchemaForm` lo
  cerca solo in i18n, `<labels>.fields.<path>`, e nessun file di lingua lo ha: è una riga. Le scelte di una select possono già portare
  un'etichetta scritta da chi chiama (`{ value, label }`, le categorie delle notizie, design M1 §3.4), il nome del campo no.
- **E la voce «nessuno» di una select opzionale** si cerca in `<labels>.options.<path>.none`: per un campo costruito da una riga quella
  chiave non c'è, e i18next restituirebbe la chiave stessa.

## 2. La forma nel codice

- **`FieldMeta.label`** (`web/src/shared/forms/schema.ts`): `.meta({ label })` su un campo, una stringa già nella lingua sullo
  schermo. La lettura dello schema la porta come ogni altra annotazione (`unwrap` la raccoglie dentro e fuori da `optional`), e il
  generatore **non la traduce**: la disegna, come le etichette di `choices`.
- **`SchemaForm`** (`Field`): il nome è `node.meta.label`, e senza è `t('<labels>.fields.<path>')` come sempre. Vale per ogni tipo di
  campo, perché ogni tipo riceve lo stesso `label`: la riga, l'interruttore, il gruppo di caselle (il suo `aria-label`), il
  fieldset di un oggetto o di una lista, le schede di un campo tradotto.
- **La voce «nessuno»**: per un campo con un nome suo è `t('form.none')`, una parola nuova del nucleo («No choice» / «Nessuna scelta»,
  neutra rispetto al genere del campo in italiano); per gli altri la chiave del campo come sempre. Calcolata una volta in `Field` e
  usata dai tre posti che la disegnano: la select di `choices`, la select di una `z.enum`, la griglia delle icone (`IconChoice` riceve
  la parola invece del prefisso, che usava solo per quella).
- **Il suggerimento sotto il campo resta in i18n** (`<labels>.hints.<path>`, letto con `i18n.exists`): il form di E16 non ne ha
  bisogno (lo dice la sessione che coordina), e un'annotazione senza chi la usa è codice in più. Il giorno in cui un form costruito da
  righe ne portasse uno, è una riga accanto a questa, con la stessa regola.
- **Le opzioni di una `z.enum` restano chiavi i18n** anche su un campo con un nome suo: una `z.enum` è scritta nel codice, e un insieme
  che viene da righe è `choices` con le sue etichette.
- **Niente altro**: nessun file del server, nessuna migrazione, nessun cambio a `DataList` (le intestazioni di colonna sono di uno
  schema scritto nel codice, con le sue chiavi). `docs/UI-GUIDELINES.md` aggiunge l'annotazione all'elenco di quello che lo schema può
  dire.

## 3. I test

`web/src/shared/forms/SchemaForm.label.test.tsx`, nuovo, 10 casi, con lo schema che E16 costruisce (`answers.q<id>`, i cinque tipi
che la sessione che coordina ha descritto):

- **un campo con un nome suo**: l'interruttore, la select, il gruppo di caselle (il suo nome accessibile), il numero e il testo si
  trovano per il nome della domanda; l'oggetto intorno tiene il suo nome i18n; nessuna chiave stampata;
- **la select opzionale** dice «No choice», e sceglierla svuota il campo (`''`, come ogni select di `choices`); in italiano dice
  «Nessuna scelta», letta da `locales/it`;
- **un campo senza nome suo** si chiama con la sua chiave, e la sua select opzionale dice «nessuno» con la sua chiave, non con la
  parola del nucleo;
- **la lettura dello schema** porta il nome, che l'annotazione stia dentro o fuori da `optional`.

**Provati sul codice di `main`** (`a75ed6c`, i quattro file rimessi, poi ripristinati): cadono i 7 casi del nome e della parola del
nucleo; passano i 3 che devono restare uguali (i due del campo senza nome suo, e la lettura dello schema, che portava già ogni
annotazione). I test del maintainer sul generatore (`SchemaForm.test.tsx`, `extensions.test.tsx`, `SchemaForm.enter.test.tsx`,
`SchemaForm.live.test.tsx`) non cambiano e passano: `label` è opzionale, e senza il comportamento è quello di prima.

## 4. Alternative scartate

| Alternativa | Perché no |
|---|---|
| B, le parole delle domande caricate in i18n a runtime (`i18n.addResourceBundle`) | Carmine: i file di lingua non sono un posto dove tenere righe; e il prossimo form costruito da righe copierebbe il trucco (`CLAUDE.md` §5) |
| Un form scritto a mano per le domande | piano §16 punto 6: un form si genera dallo schema |
| `title` o `description` di zod (`GlobalMeta`) invece di `label` | sono di zod e di JSON Schema, e uno schema esportato li leggerebbe come documentazione; `label` è la parola che le scelte usano già |
| Il nome tradotto (`{ it, en }`) nello schema, e il generatore che sceglie la lingua | il generatore non traduce mai un valore che riceve (come per `choices`): chi chiama sa già la lingua sullo schermo |
| La voce «nessuno» scritta da chi chiama (`.meta({ none })`) | una parola in più per ogni campo, la stessa ogni volta: la dice il nucleo una volta |
| Anche `hint` nello schema | nessuno lo usa oggi (§2) |

## Da portare nel piano

- `01-design-m0.md` §7.5, il punto di `SchemaForm`: dopo «Etichette da i18n `<ns>.fields.<path>`…», **dal 12 ott 2026** (M4, E10o) un
  campo può portare il suo nome nello schema, `.meta({ label })`, già nella lingua sullo schermo, per un form costruito da righe; la sua
  select opzionale dice «nessuno» con `form.none` del nucleo.
- `00-piano-di-progettazione.md` §16 punto 6 («Un solo motore di back-office», accanto al campo suggerito): la stessa riga, in breve.
  Versione e changelog.
- `09-design-m4.md` §4-bis: il form del membro nomina i campi con il testo delle domande attraverso questa annotazione (la forma è di
  E16).
- `10-piano-implementazione-m4.md`: E10o, scritta in questa PR.

[q253]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/253
[a253]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/253#issuecomment-6120877540
