# Una select chiusa parla la lingua dello schermo

**Data:** 6 novembre 2026, branch `fix/select-placeholder-language`, trovato lavorando alla PR #467.
**Stato:** correzione di un difetto. Le parole e la regola per le select facoltative le ha proposte la sessione di lavoro seguendo i precedenti qui
sotto, e **Carmine le ha confermate**, in chat al master, il 6 novembre 2026: «Confermo 469»
([il commento sulla PR](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/469#issuecomment-6688464947)).
**Regola applicata:** `CLAUDE.md` §1 (nessuna stringa visibile fuori dalle chiavi i18n) e §5, caso **(b)**: estende il form generato
(`shared/forms/SchemaForm.tsx`) senza aggiungere un meccanismo.

## Il difetto, misurato

Con l'interfaccia in italiano, in un browser vero sul build di produzione, le select chiuse del form di un permesso
(`/staff/admin/permissions/new`: `positionDepartment`, `value`, `department`) e di una voce di menu (`parentId`) dicevano `Select an option`.
La stringa è il valore predefinito della prop `placeholder` di `Select` in `@ivao/atmosphere-react`; `SchemaForm` non la passava.

## Le altre select dell'hub

Letti tutti i `<Select` di `web/src`, fuori da `SchemaForm` nessuno può mostrarla:

- **Passano il loro segnaposto**: `ListFilter`, `ReviewPanel`, `TemplatePicker`.
- **Hanno sempre un valore**: vista del calendario, «crea in», modalità e colonna di `BlockProperties`, `DataList`, `LegGrid`, modo di `LegImport`.
- **`LegImport`, il foglio**: `file.sheet ?? ''`, ma la select si disegna solo con più di un foglio, e allora `readLegFile` ne sceglie sempre uno.
- **`LocaleSwitcher`**: senza valore solo se la divisione non ha lingue, e allora non ha neanche opzioni.

Quindi la correzione resta nel form generato: un componente nostro attorno a `Select`, per cambiare il predefinito ovunque, sarebbe una voce in più
nell'elenco chiuso per un caso che non c'è.

## La correzione

- **Una select obbligatoria chiede**: `form.choose`, «Scegli un’opzione» / «Choose an option». «Scegli» è già la parola dell'hub (`content.chooseTemplate`,
  `content.chooseParent`). L'inglese cambia di proposito rispetto ad Atmosphere: oltre a essere la stessa parola, è ciò che permette a un test di
  distinguere la chiave dal predefinito.
- **Una select facoltativa dice lo stato in cui è**: la parola del suo `none`, la stessa che l'elenco offre per tornare a niente («Tutti i dipartimenti»,
  «Nessuna posizione — un membro, per VID», «Scegli un gruppo»). È la regola già scritta in `ListFilter` («il segnaposto mentre non è scelto niente») e
  seguita da `TemplatePicker` e `ReviewPanel`.
- **Un facoltativo senza un `none` suo dice la parola del nucleo**, `form.none`, come già fa il campo col nome proprio (E10o). In `locales/en` e `locales/it`
  mancano `menu.options.parentId.none` e `events:settings.options.minimumAtcRating.none`: l'elenco offriva la chiave grezza, e col segnaposto
  sarebbe finita anche sulla select chiusa. Si legge con `i18n.exists`, come il suggerimento sotto un campo.

Il test `SchemaForm.placeholder.test.tsx` copre le tre specie di select in inglese e in italiano; senza la prop `placeholder` cadono tutti e sei i suoi casi.

## Alternative scartate

- **`form.choose` anche sulle facoltative**: è più piccola, ma dice «scegli» su un campo che può restare vuoto. E con la PR #466, che ridisegna il
  segnaposto dopo un ritorno a niente, chi sceglie «Tutti i dipartimenti» si ritroverebbe davanti «Scegli un’opzione». Era uno dei due punti posti a
  Carmine, che ha confermato il `none`.
- **Un componente nostro attorno a `Select`**: vedi sopra.
- **Patch di Atmosphere**: è una dipendenza, e accetta già il segnaposto dal chiamante.

## Da portare nel piano

- **§16** (meccanismi generici), nella riga del form generato: una select chiusa senza scelta dice `form.choose` se il campo è obbligatorio, il suo `none`
  se è facoltativo, e `form.none` se quel `none` non esiste. Mai il predefinito di Atmosphere.
