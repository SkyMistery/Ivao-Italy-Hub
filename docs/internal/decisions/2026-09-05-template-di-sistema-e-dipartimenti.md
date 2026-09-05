# I template sono strumenti di dipartimento, e ogni staff può leggerli

**Data:** 5 settembre 2026 — trovata in G0 di M1, **decisa da Carmine lo stesso giorno**
**Dove va implementata:** G5 (`04-piano-implementazione-m1.md`)

## Cosa succedeva

`ContentTemplateSeeder` semina i tre template di sistema (`section-page`, `about`, `policy`) con
`OwnerDepartment = WD`. Per un coordinatore ED (posizione `IT-EC`, verificato nel browser durante
G0) `GET /api/content?filter[isTemplate]=true` risponde **zero righe**, e `TemplatePicker` non rende
niente quando l'elenco è vuoto: «Nuovo da template» semplicemente non esiste per lui.

Tre cancelli, e il primo è già aperto:

1. **il filtro di visibilità non c'entra**: i template sono `Visibility.Staff` e il global query
   filter lascia passare le righe `Staff` a qualunque staff, di qualunque dipartimento;
2. **il restringimento della lista** di `MapCrud` (`TryNarrowToDepartments`) la limita ai dipartimenti
   dell'utente: è questo che risponde zero;
3. **l'handler unico** chiede `Content.View` *sul dipartimento della riga*: 403 sul dettaglio, e
   soprattutto nel `from-template`, che quella verifica la fa esplicitamente.

⚠️ Conseguenza non ovvia dello stato attuale, letta nel codice: se una pagina nasce da un template
che il suo editore non può leggere, `contentQuery(templateId)` risponde 403, `templateRules` cade su
`NO_RULES` e **le sezioni `locked` smettono di apparire bloccate**. Le restrizioni non viaggiano
nella copia e il server non le rivalida: sono consigli dell'editor. G11 («differenze rispetto al
template») non avrebbe il dato da mostrare.

## La decisione

**Un template è uno strumento di dipartimento**: appartiene a chi lo possiede, e ogni dipartimento
si fa i suoi. Ma **ogni staff può leggerli tutti**.

- **Lettura condivisa.** Qualunque membro dello staff legge qualunque template — l'elenco e il corpo.
  Non è una concessione grande: i template sono `Visibility.Staff` per costruzione, non sono mai
  pubblicabili (`PublishAsync` li rifiuta), e non contengono dati, solo struttura e testo di esempio.
- **Scrittura invariata.** Modificare un template resta `Content.ManageTemplates` **sul dipartimento
  che lo possiede** (`ExtraWritePolicy` + handler dipartimentale). Ogni coordinatore ha già quel
  permesso sul proprio dipartimento: può creare, modificare e cancellare i propri template, e non
  toccare quelli altrui.
- **Usare quello di un altro dipartimento è permesso**, e crea una pagina **nel proprio**: è
  esattamente ciò che `CreateFromTemplateAsync` già chiede — `Content.View` sul template,
  `Content.Edit` sul dipartimento di destinazione — e che il suo commento dichiara a parole
  («*a coordinator may use a template without being allowed to change one*»). Era una porta
  costruita con cura in un muro senza corridoio; questa decisione apre il corridoio.
- **Copiare un template nel proprio dipartimento** è il modo di divergere: la copia nasce di
  proprietà di chi la chiede, e da quel momento è sua. **Non si costruisce ora**: la copia è la
  stessa copia profonda di `CreateFromTemplateAsync` con `IsTemplate = true` e
  `Content.ManageTemplates` sulla destinazione, quindi è un secondo uso di codice che esiste, e si
  scrive quando qualcuno vuole davvero divergere — non prima.

## Come si implementa, e perché tocca la spina dorsale

Due punti, ed entrambi vanno estesi in modo **generico**: nessuno dei due deve sapere che cosa sia
un template (`CLAUDE.md` §2, piano §16.6).

1. **Il restringimento della lista**: `CrudOptions` guadagna un predicato di righe leggibili da tutti
   — `options.SharedForReading = content => content.IsTemplate` — che il motore mette in `OR` con il
   filtro di dipartimento. Il motore continua a non sapere che cosa sia un template: sa che questa
   risorsa dichiara alcune righe condivise.
2. **L'handler unico**: una riga che si dichiara condivisa passa la verifica quando il permesso è di
   **lettura**. Non un secondo handler — la regola sta dentro l'unico che esiste, come già ci sta
   quella delle FIR.

⚠️ I due punti devono dire la stessa cosa: uno è un'espressione SQL, l'altro un controllo in memoria.
La forma raccomandata è **una sola fonte** sull'entità (un'espressione statica che l'istanza compila),
con il test che li confronta. È lo stesso rischio della coppia `blockEnvelopeSchema` /
`BlockDocumentWalker`, che questo repository tiene onesta con un test di integrazione.

Costo stimato: mezza giornata con i test della spina dorsale. Nessuna migrazione, nessun permesso
nuovo, nessuna schermata nuova.

## Cosa si è scartato

- **Copia per dipartimento al seed**: dà l'effetto della biblioteca condivisa e il debito delle
  fotocopie. Il seed si applica una volta per chiave, quindi una release che corregge
  `section-page` non raggiungerebbe mai le copie già seminate: fra sei mesi ci sono nove varianti
  dello stesso template e nessuno sa quale sia quella buona.
- **Lasciare tutto com'è**: coerente (ogni dipartimento si fa i suoi da zero, e già oggi può — crea
  una pagina, spunta «Template», salva), ma i tre template seminati resterebbero un attrezzo del solo
  dipartimento Web, e G11 mostrerebbe le differenze solo a chi possiede il template.
- **Distinguere «template di sistema» da «template di dipartimento»**: richiederebbe una colonna
  nuova e un concetto in più. La privacy di un template non è un bisogno che il prodotto ha espresso.
