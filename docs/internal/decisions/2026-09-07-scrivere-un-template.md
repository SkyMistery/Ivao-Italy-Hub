# Scrivere un template da una schermata

**Data:** 7 settembre 2026 — debito lasciato da G11, sollevato e **deciso da Carmine lo stesso giorno**
**Dove è implementata:** G11a, fra G11 e G12 (`04-piano-implementazione-m1.md`)

## Cosa succede

G11 ha costruito le differenze rispetto al template (design §9.1) e tutto quel meccanismo poggia su
quattro campi di una sezione:

- **`key`** — l'unica cosa che una copia conserva, e quindi l'unico modo di riconoscere «questa
  sezione della pagina è quella sezione del template». Una sezione senza `key` non impone niente e
  non è confrontabile con niente;
- **`required`** — la pagina non può cancellarla;
- **`locked`** — la pagina può modificare le proprietà dei suoi blocchi e nient'altro;
- **`allowedBlocks`** — quali tipi di blocco possono starci.

**Nessuno dei quattro si scrive da una schermata.** `sectionSettingsSchema` ha `title`, `layout`,
`background`, `mediaId`, `padding`, `width` e basta. Oggi un template nasce da un seed
(`seed/content-templates/*.json`) oppure da una `PUT` scritta a mano — che è esattamente come l'e2e
di G11 prepara la scena, perché non c'era altro modo.

⚠️ La conseguenza è che design §9.4 promette una cosa che il prodotto non mantiene: «ogni
dipartimento si fa i suoi template, e li modifica con il `Content.ManageTemplates` che ogni
coordinatore ha già sul proprio». Un coordinatore quel permesso ce l'ha, e non ha dove usarlo.

## Perché nessun meccanismo esistente lo copre già

Tre gattelli, e i primi due sono **già aperti**:

1. **Il server accetta i quattro campi.** `BlockDocumentWalker.CheckTemplateOnlyKeys` rifiuta
   `required`, `locked` e `allowedBlocks` **solo su una riga che non è un template**; su un template
   passano, ed è così che i seed funzionano. `key` è accettata ovunque, perché una copia la porta.
   Quindi: nessuna migrazione, nessuna colonna, nessun endpoint, nessun permesso nuovo.
2. **Il permesso c'è**: `Content.ManageTemplates`, e G11 lo nomina già nell'editor per dire chi può
   cambiare un vincolo.
3. **Il generatore di form non disegna `allowedBlocks`.** È l'unico vero buco. `readFields` legge un
   `array` come una lista ripetibile i cui elementi sono **oggetti** (`readFields(def.element)`
   pretende uno shape e altrimenti lancia), quindi `z.array(z.string())` non si disegna. Un insieme
   chiuso da cui se ne scelgono diversi non è una cosa che il generatore sappia fare.

Da qui i due bivi.

## Bivio 1 — come si scelgono i tipi di blocco permessi

- **(A) Estendere il generatore**: `.meta({ multi: true })` su `z.array(z.string().meta({ choices }))`,
  disegnato come un gruppo di caselle o un select multiplo. È la regola di CLAUDE.md §2 applicata
  alla lettera — se il meccanismo non copre il caso al 100 % si estende il meccanismo — e vale per
  chiunque avrà mai un insieme chiuso da cui si sceglie più di una cosa.
  ⚠️ Costo: diventa la **sesta** estensione del generatore, e design §12 ne prevedeva cinque. G12
  riporta i numeri misurati contro quella previsione, quindi il numero cambia. Non è un fallimento:
  è l'informazione per cui l'esercizio esiste.
- **(B) Usare la lista ripetibile che c'è**: `z.array(z.object({ type: … .meta({ choices }) }))`, con
  la conversione `string[] ↔ {type}[]` in `SectionProperties`, che già traduce fra la busta e il
  form. Zero estensioni. ⚠️ Costo: per sceglierne cinque su ventisette servono cinque «aggiungi» e
  cinque select, e la forma sullo schermo non è la forma del dato.

## Bivio 2 — si può cambiare la `key` di una sezione che esiste già

⚠️ Cambiare la `key` di una sezione di un template su cui esistono già delle pagine **rompe il
legame in silenzio**: la sezione della pagina diventa «tolta dal template» e quella del template
«nuova», e nessuno ha fatto niente di sbagliato.

- **(A) Si scrive una volta sola**: modificabile finché è vuota, in sola lettura dopo. È il minimo
  onesto, e non toglie niente — il server accetta ancora qualunque cosa da una `PUT`, quindi chi sa
  quello che fa può ancora farlo.
- **(B) Sempre modificabile, con una riga che avverte.**
- **(C) Non si scrive affatto**, e allora il debito resta aperto: senza `key` un template nuovo non
  impone niente.

## La decisione

**Bivio 1 — si estende il generatore.** `.meta({ multi: true, choices })` su un `z.array(z.string())`,
disegnato come una casella per valore. È la regola di CLAUDE.md §2 applicata: se il meccanismo non
copre il caso al 100 % si estende il meccanismo, e una lista ripetibile di select per scegliere
cinque tipi su ventisette sarebbe stata la forma del generatore imposta al problema invece del
contrario. Serve a chiunque avrà mai un insieme chiuso da cui si sceglie più di una cosa.

⚠️ **Diventa la sesta estensione, e design §12 ne prevedeva cinque.** Il numero da riportare alla
chiusura di M1 è **6**, e la ragione dello scarto è di una riga: la previsione guardava i blocchi, i
blocchi non l'hanno mai chiesta, scrivere un template sì. §1.6 e §12 sono stati corretti con il
numero vero accanto alla previsione, invece che riscritti — è il modo in cui quella lista serve a
qualcosa.

**Bivio 2 — la `key` si scrive una volta sola.** Il form la offre finché è vuota e la mostra dopo, con
una riga che dice perché. Il server continua ad accettare qualunque cosa da una `PUT`: chi sa quello
che fa non è bloccato, ed è il form che non fa inciampare — non una regola nuova del dominio, che
avrebbe voluto una validazione e una migrazione per una cosa che non è mai stata vietata.

**Quello che non cambia**: nessuna tabella, nessuna colonna, nessuna migrazione, nessun permesso,
nessun endpoint. `BlockDocumentWalker` accettava già i quattro campi su un template e ne rifiuta tre
su una pagina; `Content.ManageTemplates` esisteva da M0. È tutto client.

## Come si verifica

- `extensions.test.tsx` — due casi nuovi sul sesto tipo di campo: quello che è già scelto è mostrato
  scelto, un clic ne aggiunge un secondo, e quello che esce è **nell'ordine dell'insieme** e non in
  quello in cui sono state spuntate le caselle (due array con gli stessi valori in ordine diverso
  sarebbero una modifica che nessuno ha fatto).
- `e2e/full/template.spec.ts` — il giro intero, che prima di G11a non si poteva fare affatto: si
  scrive una sezione di template **dall'editor** (chiave e blocchi permessi), si salva, si riapre e
  la chiave è una riga invece che un campo; poi nasce una pagina da quel template e la sua palette
  offre **il solo blocco permesso**. Verificato rompendo il prodotto due volte — la chiave sempre
  modificabile, e `allowedBlocks` scritto sempre nullo.

## Cosa si tocca

- `web/src/features/content/schema.ts` — `sectionSettingsSchema` diventa una funzione di
  `isTemplate`, come `contentMetadataSchema` è già una funzione di `kind`.
- `web/src/features/content/BlockProperties.tsx` — `SectionProperties` riceve `isTemplate` e mappa i
  quattro campi in entrata e in uscita. ⚠️ Su una pagina non deve **mai** emettere `allowedBlocks`,
  nemmeno vuoto: un `JsonArray` su una riga che non è un template è un 400.
- `web/src/features/content/ContentEditor.tsx` — passa `isTemplate`.
- Con il bivio 1 (A): `web/src/shared/forms/schema.ts` e `SchemaForm.tsx`, più `FieldMeta.multi`.
- `locales/{en,it}/common.json` — le etichette dei quattro campi.
- Design M1 §9.1 e §9.4, piano di implementazione M1, `HANDOFF.md`.

Nessun file C#, nessuna migrazione, nessun permesso: è client, come G11.
