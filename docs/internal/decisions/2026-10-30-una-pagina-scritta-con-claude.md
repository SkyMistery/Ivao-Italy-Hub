# Una pagina scritta con Claude: le istruzioni da scaricare e il file da importare nella bozza

- **Data**: 30 ottobre 2026; risposte di Carmine il 31 ottobre 2026
- **Stato**: **Decisa da Carmine in chat al master**, 31 ottobre 2026: le risposte sono nel §8, e il §3–§4 descrive già la soluzione
  decisa
- **Chi**: una sessione di lavoro di Carmine, sull'idea che Carmine ha dato al master il 29–30 ottobre 2026; si colloca **in testa alla
  fase 3** (l'editor) di `2026-10-22-il-piano-dopo-la-prova-dello-staff.md`, §4.A e §5
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
- **La versione della bozza esiste già.** `cms_contents.row_version` (`ContentEntry.RowVersion`) è il token di concorrenza: un
  salvataggio con una versione vecchia risponde 409 (`MapCrudExtensions.CarryConcurrencyToken`). L'editor tiene la versione **su cui
  è basata la bozza** che ha davanti (`useDraftBase` in `ContentEditor.tsx`, issue #330, regola 5 di
  `2026-09-11-l-editor-che-risponde.md`), e l'autosalvataggio la fa avanzare a ogni salvataggio suo.
- ⚠️ **Gli id dei blocchi contano.** Il confronto che legge chi approva (`ContentReviewService`) abbina le sezioni per `key` e i
  blocchi **per id**, «che un blocco tiene per tutta la vita in una pagina»; da lì nasce anche il riassunto della versione
  (`changeSummary.ts`). Un import che riscrivesse tutti gli id farebbe vedere all'approvatore «tolto tutto, aggiunto tutto».
- **La pubblicazione resta com'è**: `ContentPublishService.ProblemsAsync` chiede ogni lingua della divisione in ogni valore tradotto,
  e le foto servibili ai lettori della riga; l'approvazione (`Content.Approve`, `ApprovedBy` nella versione) non cambia.
- **L'audit nomina già lo staffer**: l'import è un salvataggio suo, e `hub_audit_log` ha `Vid` (e `TokenId` per un token). Non ha un
  campo per dire «da un file»: `Action` lo scrive l'interceptor (`created`, `updated`, …). Lo si dice nella **riga «che cosa è
  cambiato»** della versione (`changelog`, precompilata da `changeSummary.ts`), senza toccare l'audit (§8, risposta 6).

## 3. Che cosa si fa

1. **«Scarica per Claude»** nell'editor di una riga di `cms_contents` che esiste già (dopo il primo «salva bozza»: prima non c'è una
   riga né una versione), per chi ha `Content.Edit` su di lei. Scarica **un file solo**, Markdown, composto al momento, mai scritto a
   mano, con dentro:
   - **la parte fissa** — che cosa è il file da restituire, le regole della busta, «niente colore libero», le quattro famiglie
     d'accento, gli identificativi del §4 e che cosa non si fa — in **un file Markdown del nucleo**, come `EmbedGuidelines.md`;
   - **la parte generata**: per ogni blocco che questa pagina ammette, il nome tradotto, la frase «a che cosa serve»
     (`blocks.<tipo>.about` nei file di lingua, §8 risposta 1), il JSON Schema e l'esempio; la busta come JSON Schema;
   - **lo stile della divisione**: la pagina dello staff nel CMS che lo descrive (tono, che cosa va in una news), scelta nelle
     impostazioni, aggiunta in coda; senza la pagina scelta, la parte non c'è;
   - **il contesto della pagina**: tipo (pagina, news, documento), le lingue della divisione (`division.json → locales`), la
     visibilità della riga (e, quando la #414 è unita, il pubblico dei blocchi: un blocco più stretto della pagina non è proposto),
     le sezioni del modello con `key`, `required`, `locked`, `allowedBlocks`, gli indirizzi pubblicati che lo staffer può vedere;
   - **la bozza com'è adesso** (titolo, riassunto, corpo, con i loro id), anche quando è vuota: Claude **integra** la parte nuova in
     quella e toglie quello che si può togliere (Carmine, §8 risposta 3);
   - **l'intestazione da copiare** nel file di risposta (§4);
   - l'interattivo è proposto solo a chi ha `Content.EmbedCode`, e per il suo codice le istruzioni rimandano a
     `/embed/guidelines` invece di ripeterlo.
2. **Il file che Claude restituisce**: un JSON solo, l'intestazione del §4 più `title`, `summary` e `body` (`schemaVersion` 1). Non
   porta indirizzo, visibilità, dipartimento, collezioni: sono della riga, e restano dello staffer.
3. **«Importa da Claude»** nell'editor, solo nella **bozza**, e **sostituisce sempre** la bozza (per le pagine nuove e per quelle con
   contenuto: §8 risposta 3). Il file si legge nel browser (come il campo Code), poi, in quest'ordine: l'intestazione (§4), la busta
   (`bodySchema`), ogni blocco con il suo schema, le regole del modello, gli id. Gli errori vanno accanto al blocco con i percorsi che
   `bodyPath.ts` già traduce («Hero › Testo › testo»). Poi il salvataggio normale, con la versione della bozza e i controlli del
   server. L'import entra nella storia dell'editor (`useBodyHistory`): **Annulla** lo toglie. Alla pubblicazione la riga «che cosa è
   cambiato» parte da «Importata da Claude» più il riassunto automatico.
4. **Gli id**: Claude **tiene l'id** di ogni sezione e blocco che lascia o modifica, e la `key` delle sezioni del modello; a un blocco
   o una sezione **nuova** non dà un id. L'import assegna gli id nuovi con `newId` e rifiuta un id che la bozza scaricata non aveva o
   un id ripetuto. Così l'approvatore vede «modificato», «aggiunto», «tolto» come per un lavoro a mano.
5. **I limiti**:
   - **le foto**: Claude non può caricare niente. Lascia vuoto ogni campo `media` (salvo quelli della bozza, che tiene) e scrive `alt`
     e didascalia; l'import accetta un blocco il cui solo errore è su un campo `media` e dice «3 foto da scegliere», accanto a
     ciascun blocco (§8 risposta 2);
   - **i link alle pagine del sito**: un percorso interno che non è fra quelli delle istruzioni l'import lo segnala, come farà il
     controllo dei link della fase 3;
   - **i blocchi di dati** con un valore che Claude non può sapere (la categoria di un `linkList`) restano da completare;
   - **l'interattivo** resta l'unico blocco con codice, e il server rifiuta il codice nuovo a chi non ha `Content.EmbedCode`.
6. **Un guadagno di lato**: Claude scrive tutte le lingue della divisione, quindi una pagina nata così è già pubblicabile. Copre la
   prima stesura, non le correzioni fatte a mano dopo: il «Traduci» della fase 7 (Microsoft Translator) resta utile, ma meno urgente.
   E l'hub non manda niente fuori: il testo lo dà a Claude lo staffer, con il suo account; nessuna chiave, nessuna riga di privacy.

## 4. Gli identificativi: «ho seguito queste istruzioni» e «ho lavorato su questa bozza»

Carmine (§8, risposta 3): il file deve portare degli identificativi che assicurino che la pagina è stata fatta **seguendo quelle
istruzioni** e, per una pagina che esiste già, **sulla pagina già esistente**. «È importante questa cosa». Le istruzioni li scrivono,
Claude li copia, l'import li controlla.

**Dove stanno**: in testa al file, prima del contenuto.

```json
{
  "format": "hub-content",
  "version": 1,
  "instructions": { "content": 412, "kind": "page", "rules": "sha256:9f2c…" },
  "draft": { "rowVersion": "2026-10-31T09:12:44.0000000", "body": "sha256:41ab…" },
  "title": { "it": "…", "en": "…" },
  "summary": { "it": "…", "en": "…" },
  "body": { "schemaVersion": 1, "sections": [ … ] }
}
```

- **`instructions`** dice **di quali istruzioni** è figlio il file: la riga (`content`, il suo id) e il tipo, e **`rules`**, l'impronta
  SHA-256 (`crypto.subtle.digest`) di un JSON canonico delle **regole** che le istruzioni hanno dato: versione del formato, tipo,
  visibilità, lingue, per ogni blocco ammesso tipo e `version` della registrazione e il suo JSON Schema, le sezioni del modello con
  le loro regole, la busta. Solo le regole, non la prosa: la stessa pagina scaricata da chi legge l'editor in inglese o in italiano ha
  la stessa impronta, e una frase cambiata nello stile della divisione non invalida un file.
- **`draft`** dice **su quale bozza** Claude ha lavorato: la `rowVersion` della riga al momento dello scaricamento (quella di
  `useDraftBase`, che l'editor già usa contro i salvataggi persi) e l'impronta del corpo che le istruzioni hanno mostrato. C'è sempre,
  anche per una pagina nuova, dove è la versione di una bozza vuota: «ho guardato la pagina e ho lavorato su quella» vale anche lì.
- **Gli id dei blocchi tenuti** (§3, punto 4) sono la seconda prova di «ho lavorato su quella»: un file che riparte da zero su una
  pagina con contenuto si vede, perché non ha nessuno degli id della bozza.

**Che cosa controlla l'import, e che cosa dice** (le chiavi stanno nei file di lingua del nucleo):

| Il file | L'import | Il messaggio |
|---|---|---|
| Senza intestazione, o con un `format`/`version` che non conosce | **rifiuta** | `content.import.notFromInstructions` — «Questo file non viene dalle istruzioni di questa pagina: scaricale e dalle a Claude» |
| `instructions.content` o `kind` di un'altra riga | **rifiuta** | `content.import.otherContent` — «Questo file è per un'altra pagina (…)» |
| `rules` diversa dalle regole di adesso (ricalcolate al momento dell'import) | **rifiuta** | `content.import.rulesChanged` — «Le regole di questa pagina sono cambiate dopo che hai scaricato le istruzioni (un blocco, il modello, le lingue, la visibilità): scaricale di nuovo» |
| `draft.rowVersion` diversa dalla base della bozza nell'editor, o modifiche non ancora salvate | **rifiuta** | `content.import.draftChanged` — «La bozza è cambiata dopo che hai scaricato le istruzioni: scaricale di nuovo e dalle a Claude con il file che ti ha dato, così integra anche le modifiche» |
| `draft.body` diversa dall'impronta del corpo di quella versione | **rifiuta** | `content.import.draftNotRead` — «Claude non ha lavorato sulla bozza che gli hai dato» |
| Un id che la bozza scaricata non aveva, o ripetuto | **rifiuta**, accanto al blocco | `content.import.idUnknown` |
| Tutto in regola, ma nessun id della bozza su una bozza che ne aveva | **chiede** | `content.import.startedOver` — «Claude ha rifatto la pagina da zero: tutto il contenuto di adesso sarà sostituito. Continuo?» |

**Perché rifiutare e non avvisare.** L'import **sostituisce** la bozza: un avviso che si clicca senza leggere è il lavoro di qualcuno
perso senza che nessuno se ne accorga, e Annulla aiuta solo chi se ne accorge subito. Un rifiuto costa a chi lo riceve un giro in più
con Claude, e il messaggio dice quale. L'unico caso che chiede invece di rifiutare è l'ultimo: tutto è in regola e la scelta di rifare
la pagina può essere voluta.

⚠️ **Che cosa gli identificativi non sono.** Non sono una firma: chiunque può copiarli in un file. Assicurano che il file risponde
**a queste istruzioni, di questa pagina, su questa bozza**, cioè gli errori veri (il file di un'altra pagina, istruzioni vecchie, una
bozza cambiata nel frattempo); non provano che Claude abbia rispettato le regole. Quello lo provano i controlli dell'import e del
server, che valgono per ogni file. Una firma del server (un HMAC con una chiave nei segreti) è stata considerata e scartata: chiede un
endpoint e una chiave in più e proverebbe solo che un valore è stato copiato, la stessa cosa che l'impronta già dice.

## 5. Più avanti: Claude scrive la bozza con un token personale

Lo staffer crea un token con un'audience per i contenuti, e Claude scrive la bozza con l'API dell'hub. Più comodo, ma: il nucleo oggi
non dichiara audience (solo `IModule.TokenAudiences` di FlightOps ed Events), le scritture dei contenuti vanno solo col cookie, e
soprattutto **il server non può controllare le `props`**: un corpo scritto via API arriverebbe nella bozza con il solo controllo della
busta, senza il `safeParse`, le regole del modello e gli identificativi, che stanno nel browser. Deciso: prima il file; il token, se
lo staff lo chiede, con una nota sua (§8 risposta 4).

## 6. Alternative

| | Che cosa | Pro | Contro |
|---|---|---|---|
| **A** | **Istruzioni generate nel browser + import di un file nella bozza** (decisa) | Niente di nuovo sul server; gli schemi restano solo in zod; tutto passa dall'editor e dalla bozza | Un controllo nuovo nel browser (`safeParse` per blocco, regole del modello, identificativi) |
| B | Istruzioni scritte a mano in un documento | Subito | Si guastano al primo blocco nuovo: è la copia che `CLAUDE.md` §2 vieta |
| C | Token personale e API (§5) | Nessun file da passare | Le `props` arrivano senza il controllo zod; audience e scritture col token da aggiungere al nucleo |
| D | Il «Traduci» e un assistente dentro l'hub, a consumo | Tutto in un posto | Chiave, costo, testo che esce dal server: le stesse domande della traduzione, più grandi |

## 7. Le domande per Carmine ⚖️ (come erano state poste)

1. **Dove sta «quale blocco per che cosa».** (a) Una frase per blocco nei file di lingua (`blocks.<tipo>.about`), generata e
   controllata da un test — raccomandata —; più, se vuoi, (b) una pagina dello staff nel CMS con lo stile della divisione; (c) tutto in
   un file del nucleo.
2. **Le foto**: (a) campi `media` vuoti, li sceglie lo staffer — raccomandata —; (b) l'elenco della libreria nelle istruzioni.
3. **L'import sostituisce o aggiunge?** (a) Su una bozza vuota sostituisce, su una con contenuto chiede, sempre con Annulla —
   raccomandata —; (b) sempre sostituisce; (c) sempre aggiunge.
4. **File ora, token dopo?** (a) Il file ora, il token con una nota sua — raccomandata —; (b) tutti e due subito.
5. **Dove va nelle fasi.** (a) In testa alla fase 3 — raccomandata dal master —; (b) in fondo alla fase 3; (c) nella fase 7.
6. **«Importata da un file»** solo nella riga della versione — raccomandato —, oppure anche nell'audit.

## 8. Le risposte di Carmine (31 ottobre 2026, in chat al master)

1. **(a) e (b)**: la frase per blocco nei file di lingua, generata e controllata da un test, **più** la pagina dello staff nel CMS con
   lo stile della divisione, aggiunta in coda alle istruzioni.
2. **(a)**: i campi `media` restano vuoti, le foto le sceglie lo staffer.
3. **Diversa dalle tre**, con queste parole: «Se la pagina ha già del contenuto deve essere passato a Claude rendendolo scaricabile
   insieme alle istruzioni e poi claude deve integrarci la parte nuova (o togliere eventuali parti che si possono togliere), quindi si
   deve sostituire. (anche per le pagine nuove) Deve essere tutto fatto in modo che claude metta degli identificativi che assicurino
   che la pagina è stata creata seguendo quelle istruzioni e, nel caso di pagina già esistente deve mettere dei codici che dicano "ho
   guardato la pagina già esistente e ho lavorato su quella" è importante questa cosa». Quindi: la bozza va nelle istruzioni, l'import
   sostituisce sempre, e gli identificativi del §4.
4. **(a)**: il file ora; il token personale dopo, con una nota sua.
5. **(a)**: in testa alla fase 3.
6. **Solo nella riga della versione**: niente modifiche all'audit.

La risposta è stata data in chat al master e non in un commento: la nota è di una sessione di Carmine, non del collaboratore. Il
disegno degli identificativi (§4: che cosa sono, dove stanno, rifiutare invece di avvisare) è la proposta di questa sessione sulla
risposta 3; il master lo porta a Carmine con la PR.

## Da portare nel piano

- **§9.3** (modello dei contenuti): un corpo può arrivare da un file nella bozza, che sostituisce; il formato `hub-content` versione 1
  con l'intestazione `instructions` (riga, tipo, impronta delle regole) e `draft` (versione e impronta della bozza letta); gli id dei
  blocchi tenuti; la busta, gli schemi di ogni blocco, le regole del modello e gli identificativi controllati nel browser, la busta sul
  server come ogni salvataggio.
- **§16** (meccanismi generici): le istruzioni per un assistente sono **composte** dal registro dei blocchi (JSON Schema da zod,
  `example`, `blocks.<tipo>.about`), da un file fisso del nucleo e dalla pagina dello stile della divisione, come le linee guida
  dell'interattivo; mai scritte a mano. Ogni blocco ha la sua frase `about`, e un test lo controlla.
- **Le fasi** della nota del 22 ottobre (§5): questa funzione in testa alla fase 3; alla fase 7 la traduzione resta, per le
  correzioni; il token personale per i contenuti, se chiesto, con una nota sua.
