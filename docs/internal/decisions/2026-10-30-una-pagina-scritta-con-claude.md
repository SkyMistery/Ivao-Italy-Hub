# Una pagina scritta con Claude: le istruzioni da scaricare e il file da importare nella bozza

- **Data**: 30 ottobre 2026
- **Stato**: **Proposta** — le domande del §6 sono per Carmine
- **Chi**: una sessione di lavoro di Carmine, sull'idea che Carmine ha dato al master il 29–30 ottobre 2026; si colloca nella fase 3
  (l'editor) di `2026-10-22-il-piano-dopo-la-prova-dello-staff.md`, §4.A e §5
- **Classificazione** (`CLAUDE.md` §5): **(c)**, una funzione nuova dell'editor dei contenuti (nucleo): prima la decisione, poi il codice

## 1. Che cosa serve

La prova dello staff ha detto che comporre una pagina, una news o un documento a mano è troppo per chi non conosce l'editor
(nota del 22 ottobre, §1). Lo staff della divisione però usa molto Claude Code. L'idea di Carmine: chi deve scrivere una pagina
**scarica dall'editor le istruzioni**, le dà a Claude insieme a quello che vuole dire, Claude **restituisce un file**, lo staffer lo
**importa nella bozza**, e da lì è esattamente come una pagina fatta a mano: si corregge, si approva, si pubblica.

## 2. Su che cosa poggia (verificato nel codice)

- **Il precedente esiste**: l'interattivo. `EmbedEndpoints.GuidelinesPattern` (`/embed/guidelines`, dietro `Content.EmbedCode`)
  serve `EmbedGuidelines.md` **composto** con `EmbedShell.html`, così il documento e la cosa che descrive non possono divergere; e il
  campo **Code** prende già un file «dal tuo computer», letto nel browser, senza caricare niente (`EmbedGuidelines.md`, «The file»).
  Qui si fa lo stesso per un corpo intero.
- ⚠️ **Le istruzioni si compongono nel browser, non sul server.** Gli schemi dei blocchi esistono solo in zod (`web/src/blocks/
  schemas.ts`, i manifest dei moduli; `CLAUDE.md` §2): il server conosce tipo, natura e busta, mai le `props`. Quindi, a differenza di
  `/embed/guidelines`, la parte generata la scrive l'editor: zod 4.6.6 ha `z.toJSONSchema`, e i `meta` già presenti (`localized`,
  `media`, `choices`, `multiline`) passano nel JSON Schema e dicono a Claude che cosa è un testo tradotto o una foto della libreria.
  Ogni `BlockRegistration` ha anche un `example` che soddisfa il suo schema (`features/admin/uiKit.test.ts`): è l'esempio da dare a
  Claude, già verificato. La busta (sezioni, `layout`, gli otto sfondi, le larghezze, niente blocchi dentro blocchi) è `bodySchema`
  in `web/src/blocks/envelope.ts`, anch'essa zod.
- **I controlli del salvataggio sono due metà.** Il server: `ContentWriteDtoValidator` → `BlockDocumentWalker.ValidateEnvelope`
  (oggetto, 1 MiB, `schemaVersion` = 1, profondità, tipi registrati, id unici, layout, sfondi, `renderMode`, `source` solo
  sull'interattivo, chiavi dei modelli rifiutate su una pagina), con l'errore sotto il percorso del blocco (`body.sections[0].
  blocks[2]`); e `CodeNotTheirsAsync` rifiuta il codice nuovo di un interattivo a chi non ha `Content.EmbedCode`. Il browser: le
  `props` sono controllate **campo per campo dal form generato** mentre si scrive. ⚠️ **Oggi nessuno passa un corpo intero per gli
  schemi zod al salvataggio**: a mano non serve, perché il form non lascia scrivere una `props` sbagliata. Un file importato non ha
  attraversato il form, quindi l'import deve fare un `safeParse` di ogni blocco con il suo schema: è un controllo nuovo, del
  browser, non «lo stesso del salvataggio a mano».
- ⚠️ **Le regole del modello stanno solo nel browser.** `required`, `locked`, `allowedBlocks` sono rifiutati su una pagina e tolti
  da `TemplateCopy`; l'editor li rilegge dal modello per `key` (`features/content/templateRules.ts`). Un corpo importato che toglie
  una sezione obbligatoria o mette un blocco non ammesso il server lo salva: l'import deve applicare le stesse regole.
- **La pubblicazione resta com'è**: `ContentPublishService.ProblemsAsync` chiede ogni lingua della divisione in ogni valore tradotto,
  e le foto servibili ai lettori della riga; l'approvazione (`Content.Approve`, `ApprovedBy` nella versione) non cambia.
- **L'audit nomina già lo staffer**: l'import è un salvataggio suo, e `hub_audit_log` ha `Vid` (e `TokenId` per un token). Non ha un
  campo per dire «da un file»: `Action` lo scrive l'interceptor (`created`, `updated`, …). Dirlo nell'audit sarebbe una modifica del
  nucleo; dirlo nella **riga «che cosa è cambiato»** della versione (`changelog`, precompilata da `changeSummary.ts` dalla fase 1) no.

## 3. Che cosa si fa (la proposta)

1. **«Scarica le istruzioni per Claude»** nell'editor di una riga di `cms_contents` (chi ha `Content.Edit` su di lei), un file
   Markdown composto al momento, mai scritto a mano:
   - **la parte fissa** — che cosa è il file da restituire, le regole della busta, «niente colore libero», le quattro famiglie
     d'accento, che cosa non si fa — in **un file Markdown del nucleo**, come `EmbedGuidelines.md`, letto dall'editor;
   - **la parte generata**: per ogni blocco che questa pagina ammette, il nome tradotto, una frase «a che cosa serve» (§6, domanda 1),
     il JSON Schema e l'esempio; la busta come JSON Schema;
   - **il contesto della pagina**: tipo (pagina, news, documento), le lingue della divisione (`division.json → locales`), la
     visibilità della riga (e, quando la #414 è unita, il pubblico dei blocchi: un blocco più stretto della pagina non è proposto),
     le sezioni del modello con `key`, `required`, `locked`, `allowedBlocks`;
   - l'interattivo è proposto solo a chi ha `Content.EmbedCode`, e per il suo codice le istruzioni rimandano a
     `/embed/guidelines` invece di ripeterlo.
2. **Il file che Claude restituisce**: un JSON solo, `{ "format": "hub-content", "version": 1, "kind": …, "title": {…},
   "summary": {…}, "body": { "schemaVersion": 1, "sections": [ … ] } }`. Non porta indirizzo, visibilità, dipartimento, collezioni:
   sono della riga, e restano dello staffer.
3. **«Importa da un file»** nell'editor, solo nella **bozza**: il file si legge nel browser (come il campo Code), poi la busta
   (`bodySchema`), ogni blocco con il suo schema, le regole del modello; gli id si riscrivono con `newId` (un file non sa quali
   esistono già). Gli errori vanno accanto al blocco con i percorsi che `bodyPath.ts` già traduce («Hero › Testo › testo»). Poi il
   salvataggio normale, con i controlli del server. L'import entra nella storia dell'editor (`useBodyHistory`): **Annulla** lo toglie.
   Alla pubblicazione la riga «che cosa è cambiato» parte da «Importata da un file» più il riassunto automatico.
4. **I limiti**:
   - **le foto**: Claude non può caricare niente. Raccomandato: lascia vuoto ogni campo `media` e scrive `alt` e didascalia; l'import
     accetta un blocco il cui solo errore è su un campo `media` e dice «3 foto da scegliere», accanto a ciascun blocco;
   - **i link alle pagine del sito**: le istruzioni portano gli indirizzi pubblicati che lo staffer può vedere (titolo e percorso);
     un percorso interno sconosciuto l'import lo segnala, come farà il controllo dei link della fase 3;
   - **i blocchi di dati** con un valore che Claude non può sapere (la categoria di un `linkList`) restano da completare;
   - **l'interattivo** resta l'unico blocco con codice, e il server rifiuta il codice nuovo a chi non ha `Content.EmbedCode`.
5. **Un guadagno di lato**: Claude scrive tutte le lingue della divisione, quindi una pagina nata così è già pubblicabile. Copre la
   prima stesura, non le correzioni fatte a mano dopo: il «Traduci» della fase 7 (Microsoft Translator) resta utile, ma meno urgente.
   E l'hub non manda niente fuori: il testo lo dà a Claude lo staffer, con il suo account; nessuna chiave, nessuna riga di privacy.

## 4. Più avanti: Claude scrive la bozza con un token personale

Lo staffer crea un token con un'audience per i contenuti, e Claude scrive la bozza con l'API dell'hub. Più comodo, ma: il nucleo oggi
non dichiara audience (solo `IModule.TokenAudiences` di FlightOps ed Events), le scritture dei contenuti vanno solo col cookie, e
soprattutto **il server non può controllare le `props`**: un corpo scritto via API arriverebbe nella bozza con il solo controllo della
busta, senza il `safeParse` e le regole del modello, che stanno nel browser. Prima il file; il token, se serve, con una nota sua.

## 5. Alternative

| | Che cosa | Pro | Contro |
|---|---|---|---|
| **A** | **Istruzioni generate nel browser + import di un file nella bozza** | Niente di nuovo sul server; gli schemi restano solo in zod; tutto passa dall'editor e dalla bozza | Un controllo nuovo nel browser (`safeParse` per blocco, regole del modello) |
| B | Istruzioni scritte a mano in un documento | Subito | Si guastano al primo blocco nuovo: è la copia che `CLAUDE.md` §2 vieta |
| C | Token personale e API (§4) | Nessun file da passare | Le `props` arrivano senza il controllo zod; audience e scritture col token da aggiungere al nucleo |
| D | Il «Traduci» e un assistente dentro l'hub, a consumo | Tutto in un posto | Chiave, costo, testo che esce dal server: le stesse domande della traduzione, più grandi |

**Raccomandazione: A** ora; C solo se lo staff lo chiede dopo averla provata.

## 6. Le domande per Carmine ⚖️

1. **Dove sta «quale blocco per che cosa».** (a) **Una frase per blocco nei file di lingua** (`blocks.<tipo>.about`, come già
   `linkList.about` ed `embed.about`), generata nelle istruzioni e controllata da un test come la galleria — **raccomandata** per la
   parte che nomina i blocchi, perché segue il registro da sola; più, se vuoi, (b) **una pagina dello staff nel CMS** con lo stile
   della divisione (tono, che cosa va in una news), scelta nelle impostazioni e aggiunta in coda alle istruzioni; (c) tutto in un file
   del nucleo, che però lo staff non può cambiare senza una PR.
2. **Le foto**: (a) campi `media` vuoti, li sceglie lo staffer — **raccomandata**; (b) l'elenco della libreria nelle istruzioni, perché
   Claude scelga per nome (istruzioni più lunghe, e Claude sceglie una foto senza vederla).
3. **L'import sostituisce o aggiunge?** (a) Su una bozza vuota sostituisce; su una bozza con contenuto chiede «sostituisci» o
   «aggiungi in fondo», sempre con Annulla — **raccomandata**; (b) sempre sostituisce; (c) sempre aggiunge.
4. **File ora, token dopo?** (a) Sì: il file ora, il token con una nota sua se lo staff lo chiede — **raccomandata**; (b) tutti e due
   subito.
5. **Dove va nelle fasi.** (a) **In testa alla fase 3**, prima delle sezioni visibili e della barra di formattazione: toglie la parte
   più dura della prova (comporre) e non dipende dagli altri pezzi dell'editor — **raccomandata dal master**; (b) in fondo alla fase 3,
   dopo il selettore delle pagine (così i link si scelgono già bene); (c) nella fase 7, con la traduzione.
6. **«Importata da un file»** solo nella riga della versione, senza toccare l'audit (che già nomina chi ha salvato) — **raccomandato**;
   oppure anche nell'audit, con una modifica del nucleo.

## Da portare nel piano

- **§9.3** (modello dei contenuti): un corpo può arrivare da un file nella bozza, con la busta, gli schemi di ogni blocco e le regole
  del modello controllati nel browser, e la busta sul server come ogni salvataggio; il formato `hub-content` versione 1.
- **§16** (meccanismi generici): le istruzioni per un assistente sono **composte** dal registro dei blocchi (JSON Schema da zod,
  `example`, la frase «a che cosa serve») e da un file fisso del nucleo, come le linee guida dell'interattivo; mai scritte a mano.
- **Le fasi** della nota del 22 ottobre (§5): la posizione decisa alla domanda 5; e alla fase 7 la traduzione resta, per le correzioni.
