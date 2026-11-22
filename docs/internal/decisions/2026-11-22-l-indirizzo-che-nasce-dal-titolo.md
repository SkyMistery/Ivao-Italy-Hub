# L'indirizzo che nasce dal titolo

- **Data**: 22 novembre 2026
- **Stato**: **decisa** per il §2, che è il §4.1 del design `13-design-struttura-del-sito.md`, approvato da Carmine in chat al
  master il 19 novembre 2026 («Sì, tutte e sette»; le risposte 1 e 2 sono quelle di questa PR),
  [riportato sulla #540](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/540#issuecomment-7011361289).
  **Le scelte del §4 le ha prese la sessione**, non Carmine: il design non le scriveva. La 4 è una domanda per lui ⚖️.
- **Chi**: una sessione di lavoro di Carmine (fase 4, PR 4.3)
- **Classificazione** (`CLAUDE.md` §5): **(b)**. Estende due meccanismi che ci sono: il controllo di chi sposta una riga
  (`ContentEndpoints.PositionNotTheirsAsync`, nota `2026-10-21-i-campi-fuori-dalla-versione`) e il form generato (`slugFrom`
  con `hidden`, che FlightOps usa già). Nessuna tabella, nessuna migrazione, nessun endpoint in più, nessun componente nuovo.

## 1. Che cosa serviva

Lo slug era un campo libero del form: il form lo proponeva dal titolo, ma chiunque avesse `Content.Edit` lo riscriveva su una
bozza, e su una riga online lo cambiava chi poteva pubblicarla. La nota del 13 settembre (§3.7) voleva che chi scrive scegliesse
il posto e non l'indirizzo, e che «news e documenti non scelgono»; la riga non era mai arrivata nel codice.

## 2. Che cosa si fa

- **Il server** (`PositionNotTheirsAsync`, con un pezzo suo, `SlugNotTheirsAsync`): su una pagina, una news o un documento che
  esiste già, uno slug diverso da quello salvato chiede **`Content.Approve` sulla riga**, all'handler unico; **anche su una bozza
  mai pubblicata** e anche dove il tipo non si approva (news e documenti). Il rifiuto è la chiave di sempre,
  `errors.content.position.needsApproval`, sul campo `slug`. La creazione non cambia: lo slug arriva con la riga.
- **Il browser**: lo schema dei metadati prende `maySetSlug` (`features/content/schema.ts`) e, quando è falso, il campo `slug`
  è `hidden`: **non si disegna ma c'è**, e `slugFrom: 'title'` continua a proporlo dal titolo nella lingua principale della
  divisione finché la riga è nuova (la proposta legge i valori del form, non il campo a schermo). `ContentFormScreen` lo calcola:
  vero per chi ha `Content.Approve` sul dipartimento della riga, come `mayBeAtTheTop`; `ContentEditor` lo passa allo schema.
- **Cambiare il titolo non sposta la riga**: una riga che esiste ha già il suo slug, e la proposta si ferma al primo disegno
  (`useProposedSlugs`, com'era).
- **Chi approva corregge** dal form (il campo per lui c'è) o dal pannello della revisione, che non cambia.
- **Le parole**: `errors.content.position.needsApproval` dice ora anche dell'indirizzo («l'indirizzo sempre, anche in bozza»),
  perché lo stesso rifiuto arriva su una bozza e su una news; due frasi nuove dell'anteprima (§4, scelta 3).

## 3. Che cosa cambia per chi usa l'hub

- Un coordinatore, un assistente o un advisor che crea una pagina, una news o un documento non vede più il campo «Indirizzo»:
  vede sotto il titolo e la madre l'indirizzo che nascerà, che segue il titolo finché non salva la prima volta.
- Dopo il primo salvataggio l'indirizzo resta quello. Lo cambiano DIR, ADIR, WM, AWM e il superadmin (e chi ha il permesso per
  grant), che vedono il campo come prima.

## 4. Le scelte che il design non scriveva (della sessione)

1. **I modelli e le dashboard tengono il loro slug com'è**, disegnato e libero per chi li scrive. Un modello non ha un indirizzo
   del sito (l'anteprima non c'è) e lo scrive già solo chi ha `Content.ManageTemplates`; lo slug di una dashboard è il codice del
   dipartimento. Il design parla di «pagine, news e documenti».
2. **Su una riga online il controllo di chi può pubblicare viene prima.** A chi non può pubblicare, uno slug cambiato su una riga
   online risponde `needsPublish` come prima (lo tiene `PublishedFieldsTests.WhereARowOnlineStandsIsChangedByWhoeverMayPublishIt`);
   a chi pubblica ma non approva (una news del suo dipartimento) risponde `needsApproval`.
3. **Un indirizzo preso, per chi non scrive lo slug.** Prima l'anteprima diceva «`x-2` è libero» e chi scriveva lo digitava nel
   campo, che ora non vede. Su una riga **nuova** l'anteprima dice «cambia il titolo, oppure prendi quello libero» e ha il
   pulsante **«Usa `x-2`»**, che rimette il form con quello slug (come un'importazione gli dà i suoi campi: `takeFields`). Su una
   riga **che esiste** (una bozza spostata sotto una madre dove quello slug c'è già) dice che l'indirizzo lo cambia chi approva:
   un'altra madre, o chiederlo a lui. Il pulsante è un `Button` di Atmosphere dentro `AddressPreview`, nessun componente nuovo.
4. ⚖️ **«Nuovo da template» chiede ancora lo slug a mano** (`TemplatePicker`), a chiunque crei, perché lì un titolo da cui
   proporlo non c'è: la pagina nasce con il titolo del modello. La regola del server la rispetta (è la creazione), ma è un posto
   dove chi non approva sceglie lo slug. **Domanda per Carmine**: (a) **lasciarlo così** in questa PR — raccomandata: la pagina
   nasce con il titolo del modello, quindi uno slug dal titolo darebbe a tutte le pagine dello stesso modello lo stesso indirizzo
   (`section-page`, `section-page-2`…), e il campo è l'unico modo di darle un nome alla nascita; (b) togliere il campo e chiedere
   invece il **titolo** della pagina nuova, da cui nasce lo slug: una PR piccola dopo questa, che tocca `TemplatePicker` e
   `ContentFromTemplateRequest`.

## 5. I test

- **Nuovi**, `PublishedFieldsTests`: `TheSlugIsBornWithTheRowAndChangedOnlyByWhoeverMayApprove` (pagina, news e documento in
  bozza: il coordinatore scrive il resto e la mette fra i membri, lo slug no; il direttore lo corregge) e
  `ANewsItemOnlineIsMovedByWhoeverPublishesItButRenamedOnlyByWhoeverApproves` (scelta 2). L'e2e `address.spec.ts` «a coordinator
  whose title makes a taken address…» (scelta 3).
- **Cambiati** (regola 3 di `CLAUDE.md` §0, uno per uno; nessun test d'integrazione esistente cambiava lo slug di una bozza come
  utente senza `Content.Approve`):
  - `web/e2e/address.spec.ts`, «the editor of a page says where it will be…»: il coordinatore della fixture scriveva lo slug; ora
    scrive il titolo, e il test controlla che il campo non ci sia e che l'anteprima chieda lo slug nato dal titolo.
  - `web/e2e/back-office.spec.ts`, «new news reaches the editor…»: controllava che il campo dello slug ci fosse, per un
    coordinatore; ora che non c'è.
  - `web/e2e/claude-download.spec.ts`, «a press right after the last keystroke…»: scrive nello slug perché è un campo di una
    riga; l'utente del test riceve `Content.Approve` sul dipartimento, e quello che il test misura non cambia.

## Da portare nel piano

- **§9.3** (il modello dei contenuti), con il design della fase 4: lo slug di una pagina, una news o un documento nasce alla
  creazione dal titolo nella lingua principale, e dopo lo cambia solo chi ha `Content.Approve` sulla riga, anche in bozza; chi non
  approva non vede il campo. Cambiare il titolo non sposta la riga. I modelli e le dashboard tengono il loro slug (§4, scelta 1).
- **§9.3**, la riga di `decisions/2026-10-21-i-campi-fuori-dalla-versione.md` letta nel piano: su una riga online lo slug chiede
  anche `Content.Approve` dove il tipo non si approva (news, documenti), dopo il controllo di `Content.Publish` (§4, scelta 2).
- **`decisions/2026-09-13-contenuti-centralizzati.md` §3.7**, letta nel piano: «news e documenti non scelgono» è nel codice.
- **§16.6** (il form generato): un campo `slugFrom` nascosto propone lo stesso; l'anteprima dell'indirizzo offre lo slug libero a
  chi non scrive lo slug (§4, scelta 3).
- La risposta di Carmine alla domanda del §4, scelta 4, quando c'è.
