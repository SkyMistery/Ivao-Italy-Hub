# Un campo disabilitato nel form (#367)

**Data:** 24 ottobre 2026: fase 2 del piano dopo la prova dello staff. È la PR del nucleo, che precede quella dei tour.
**Stato:** **decisa**. Attua il punto h dell'elenco di Carmine (nota `2026-10-22-il-piano-dopo-la-prova-dello-staff`, §4.G): «i campi
di un altro tipo di tour sono grigi e non si scrivono». Lo fa con il meccanismo generico del form e non aggiunge nessuna regola nuova.
La forma è stata confermata dal master il 23 ottobre: una PR del nucleo a parte, prima di quella dei tour (#367 con #369).
**Regola applicata:** `CLAUDE.md` §2 (i form del back office sono generati dallo schema zod) e §5, caso **(b)**. Il generatore copre
quasi tutto il caso: il pezzo che manca si aggiunge al meccanismo, in una PR del nucleo che precede il codice del modulo che lo usa
(`CLAUDE.md` §0, regola 6).

## 1. Che cosa serve, e perché il generatore non basta

- **Nel form di un tour tre campi valgono per un tipo solo**:
  - l'ordine delle rotazioni (tour a hub);
  - la distanza da completare (tour a distanza);
  - i sottotour da completare (tour con sottotour).

  Oggi si possono scrivere su ogni tour. Il server li svuota sugli altri tipi (`TourDtos.cs`, `ApplyTo`: `RequiredSubtours`,
  `HubRotationOrder`, `RequiredNm` diventano `null` se il tipo non è il loro), quindi il valore scritto si perde in silenzio.
- **Carmine li vuole grigi, non nascosti.** Chi legge il form vede che cosa il tour potrebbe avere, e non scrive un valore che il
  server butterebbe. Nasconderli sarebbe un ripiego: il form cambierebbe forma da un tour all'altro.
- **`SchemaForm` oggi conosce solo due stati**:
  - un campo disegnato e scrivibile;
  - un campo portato ma non disegnato (`hidden`).

  Il campo grigio e non scrivibile non c'è.

## 2. La forma nel codice

- **`FieldMeta.disabled`** (`web/src/shared/forms/schema.ts`): si scrive `.meta({ disabled: true })`. La lettura dello schema la porta
  come ogni altra annotazione.
- **`SchemaForm`** (`Field`): un campo con `disabled` si disegna dentro un `<fieldset disabled>`, con opacità 60 %. Il `fieldset`
  spegne **ogni** controllo che contiene, di qualunque tipo (numero, select, interruttore, lista), senza toccare il modo in cui ciascuno
  è disegnato. È una riga sola, invece di un `disabled` da portare in dieci posti.
- **Il valore resta nel form e si invia così com'è**, come per un campo `hidden`. Un controllo dentro un `fieldset` spento non ha
  `disabled` su di sé, quindi `react-hook-form` ne legge ancora il valore: lo prova il test. Si invia perché il form dei tour manda
  sempre il tour intero, e il server, non il browser, decide che cosa vale per quale tipo.
- **L'annotazione è statica: decide chi costruisce lo schema.** Il master ha chiesto: se il tipo cambia nello stesso form, lo stato
  grigio deve seguire il valore sullo schermo. Nei tour il caso non si presenta:
  - i tre campi stanno nella scheda «Regole» (#369);
  - il tipo si sceglie nelle «Impostazioni», che sono un altro form;
  - il form delle regole si ricarica a ogni salvataggio del tour, perché la sua chiave è la versione della riga;
  - un tour nuovo si crea dalle sole impostazioni, e i tre campi non sono disegnati finché il tour non esiste.

  Il giorno in cui un form avesse un campo che si spegne mentre si scrive un altro campo dello stesso form, servirebbe una condizione
  sul valore (per esempio `disabledUnless: { field, values }`). Oggi nessun form ne ha bisogno, e questa nota non la aggiunge.
- **Nessuna regola solo nel browser**: le tre righe del server restano dove sono, e sono loro a fare la regola.
- **La galleria** (`/staff/admin/ui-kit`, `uiKitSamples.tsx`) disegna un campo «Bloccato» nel form di esempio.
- `docs/UI-GUIDELINES.md` aggiunge l'annotazione all'elenco di quello che lo schema può dire.

## 3. Le alternative

- **A (scelta): `disabled` nel generatore**, come descritto sopra.
- **B: nascondere i campi degli altri tipi con `hidden`**, senza toccare il nucleo. Non è quello che Carmine ha chiesto.
- **C: un form scritto a mano per le regole del tour.** È contro `CLAUDE.md` §2: i form del back office sono generati.

## 4. I test

- **Generatore**, `web/src/shared/forms/extensions.test.tsx`: un campo `disabled` viene disegnato, è spento, non si scrive, e il valore
  che aveva arriva a `onSubmit`.
- **Modulo**, nella PR di #367: `web/src/modules/flightops/schemas.test.ts` dice quali campi sono grigi per ogni tipo di tour.

## Da portare nel piano

- Design M0 §7.5 (il form generato): l'annotazione `disabled` accanto a `hidden` e `label`. Il campo è disegnato, grigio e non
  scrivibile; il valore resta nel form e si invia.
