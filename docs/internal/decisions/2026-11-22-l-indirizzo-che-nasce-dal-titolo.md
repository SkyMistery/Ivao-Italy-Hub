# L'indirizzo che nasce dal titolo

- **Data**: 22 novembre 2026
- **Stato**: **decisa** per il §2, che è il §4.1 del design `13-design-struttura-del-sito.md`, approvato da Carmine in chat al
  master il 19 novembre 2026 («Sì, tutte e sette»; le risposte 1 e 2 sono quelle di questa PR),
  [riportato sulla #540](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/540#issuecomment-7011361289).
  **Decise dal maintainer, dopo**: la scelta 3 e la 4 del §4 e la proposta vuota (§4, punto 5), che il design non scriveva,
  decise da Carmine in chat al master il 23 novembre 2026 («Sì, tutte e tre», ognuna come raccomandata),
  [riportato sulla #554](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/554#issuecomment-7094966395).
  **Le scelte 1 e 2 del §4 le ha prese la sessione**: la 1 segue il design, la 2 è l'ordine di due rifiuti (revisione della #554).
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
  ⚠️ Il tipo e «modello sì o no» si giudicano **sulla riga com'era e com'è dopo il salvataggio**: `kind` e `isTemplate` viaggiano
  nel corpo del PUT, e giudicati solo sulla riga nuova una bozza diventava modello (o dashboard) con lo slug nuovo e poi di nuovo
  pagina con lo slug fermo (rilievo 1 della revisione della #554; `HasAnAddress`).
- **La proposta si taglia a 160 caratteri**, la lunghezza della colonna (`ContentWriteDtoValidator.MaxSlugLength`): un titolo
  lunghissimo darebbe uno slug che il server rifiuta su un campo che chi scrive non vede. È un'annotazione nuova del form generato,
  `slugMaxLength`, accanto a `slugPrefix`; `slugify` taglia e non lascia un trattino in fondo (rilievo 2).
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
- «Nuovo da template» chiede il titolo della pagina nuova invece dello slug; chi approva vede anche lo slug, proposto dal titolo.
- Chi scrive un titolo che non dà un indirizzo (un altro alfabeto, sole emoji) vede comparire il campo e scrive l'indirizzo da sé.

## 4. Le scelte che il design non scriveva

La 1 e la 2 sono della sessione; la 3, la 4 e la 5 le ha decise Carmine
([commento sulla #554](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/554#issuecomment-7094966395), «Sì, tutte e tre»).

1. **I modelli e le dashboard tengono il loro slug com'è**, disegnato e libero per chi li scrive. Un modello non ha un indirizzo
   del sito (l'anteprima non c'è) e lo scrive già solo chi ha `Content.ManageTemplates`; lo slug di una dashboard è il codice del
   dipartimento. Il design parla di «pagine, news e documenti».
2. **Su una riga online il controllo di chi può pubblicare viene prima.** A chi non può pubblicare, uno slug cambiato su una riga
   online risponde `needsPublish` come prima (lo tiene `PublishedFieldsTests.WhereARowOnlineStandsIsChangedByWhoeverMayPublishIt`);
   a chi pubblica ma non approva (una news del suo dipartimento) risponde `needsApproval`.
3. **Un indirizzo preso, per chi non scrive lo slug** — **decisa da Carmine**: le parole restano come le ha la PR. Prima l'anteprima diceva «`x-2` è libero» e chi scriveva lo digitava nel
   campo, che ora non vede. Su una riga **nuova** l'anteprima dice «cambia il titolo, oppure prendi quello libero» e ha il
   pulsante **«Usa `x-2`»**, che rimette il form con quello slug (come un'importazione gli dà i suoi campi: `takeFields`). Su una
   riga **che esiste** (una bozza spostata sotto una madre dove quello slug c'è già) dice che l'indirizzo lo cambia chi approva:
   un'altra madre, o chiederlo a lui. Il pulsante è un `Button` di Atmosphere dentro `AddressPreview`, nessun componente nuovo.
   ⚠️ **Dopo «Usa `x-2`» lo slug non segue più il titolo** (rilievo 3 della revisione della #554): il form ripartito conta `x-2`
   come scritto a mano (`useProposedSlugs`), e cambiare ancora il titolo prima del primo salvataggio lascia `x-2`. Non si rompe
   niente, perché l'anteprima dice l'indirizzo vero; ma è un'eccezione a «segue il titolo finché non salva», e resta con la
   scelta approvata.
4. **«Nuovo da template» segue il design 13 §4.1** — **decisa da Carmine**. Il selettore (`TemplatePicker`) chiede il
   **titolo** della riga nuova, nella lingua principale della divisione, e lo slug nasce da quello (`slugify`, tagliato a 160),
   come nel form. Il campo dello slug lo vede chi ha `Content.Approve`, che lo scrive come altrove (proposto dal titolo finché non
   ci scrive), e chi ha un titolo che non dà uno slug (punto 5). `ContentFromTemplateRequest` prende un `Title` facoltativo
   (`Localized<string>`): la riga nasce con quel titolo e non con quello del modello, e senza titolo tiene quello del modello
   (chi approva può lasciarlo vuoto e scrivere solo lo slug, com'era: lo fa il giro completo, `round.spec.ts`). Le altre lingue
   del titolo si scrivono nell'editor.
   Il server **non controlla che lo slug venga dal titolo**, né qui né nel form: alla creazione lo slug arriva con la riga, e
   controllarlo vorrebbe una copia di `slugify` in C#, che la risposta 1 del design ha escluso. La regola sta nel browser, come la
   proposta; dopo la creazione la tiene il server (§2).
   (Nella prima versione di questa nota era una domanda: la raccomandazione era lasciare il campo, Carmine ha scelto il design.)
5. **Un titolo che non dà uno slug** — **decisa da Carmine**. `slugify` tiene solo `[a-z0-9]` dopo NFD, quindi un titolo in un
   altro alfabeto (cirillico, greco, CJK, arabo: ogni fork che non scrive in lettere latine), di sole emoji o punteggiatura, o di
   lettere come ł, ø e ß, propone uno slug vuoto: il server risponderebbe `errors.required` su un campo che chi scrive non vede.
   Allora **il campo dello slug si mostra a chi scrive**, che scrive l'indirizzo da sé, dentro le regole di `ContentAddresses`.
   Vale sulla riga nuova, nel form (`ContentEditor`) e in «nuovo da template»; la domanda è una sola, `cannotProposeASlug`
   (`shared/forms/slug.ts`, accanto a `slugify` e a `readSource`, spostata lì da `SchemaForm`). Una volta aperto il campo resta
   aperto: se il titolo diventa latino dopo, lo slug scritto a mano non si nasconde. Un titolo vuoto non lo apre: lì la proposta è
   vuota perché non c'è ancora niente da cui proporla.

## 5. I test

- **Nuovi**, `PublishedFieldsTests`: `TheSlugIsBornWithTheRowAndChangedOnlyByWhoeverMayApprove` (pagina, news e documento in
  bozza: il coordinatore scrive il resto e la mette fra i membri, lo slug no; il direttore lo corregge) e
  `ANewsItemOnlineIsMovedByWhoeverPublishesItButRenamedOnlyByWhoeverApproves` (scelta 2), e
  `TheSlugIsNotRenamedOnTheWayThroughATemplateOrADashboard` (rilievo 1: i due PUT, modello e dashboard, con lo slug nuovo).
  `extensions.test.tsx` «a proposal is cut where the column ends…» e «a hidden address still proposes itself…», e
  `features/content/schema.test.ts` (rilievo 2: il taglio, e i 160 dei tre tipi). L'e2e `address.spec.ts` «a coordinator
  whose title makes a taken address…» (scelta 3).
- **Nuovi, per le decisioni di Carmine**: `PublishedFieldsTests.APageFromATemplateIsBornWithTheTitleItsSlugCameFrom` (scelta 4:
  la riga nasce con il titolo mandato e lo slug mandato; senza titolo tiene quello del modello); `shared/forms/slug.test.ts`
  (`cannotProposeASlug` su cirillico, greco, CJK, emoji, punteggiatura, ł ø ß, titolo vuoto, e la lingua principale); gli e2e
  di `address.spec.ts` «a title that gives no slug opens the field of the slug…» (scelta 5: con la controprova, senza
  l'apertura il test è rosso), «new from a template: a coordinator writes the title…», «…a title that gives no slug opens the
  field there too» e «…whoever approves keeps the field…» (scelta 4).
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
  Un titolo che non dà uno slug apre il campo a chi scrive (§4, scelta 5). «Nuovo da template» chiede il titolo della riga nuova,
  da cui nasce lo slug, e il campo lo vede chi approva (§4, scelta 4). Le tre decisioni di Carmine del 23 novembre 2026.
- **§9.3**, la riga di `decisions/2026-10-21-i-campi-fuori-dalla-versione.md` letta nel piano: su una riga online lo slug chiede
  anche `Content.Approve` dove il tipo non si approva (news, documenti), dopo il controllo di `Content.Publish` (§4, scelta 2).
- **`decisions/2026-09-13-contenuti-centralizzati.md` §3.7**, letta nel piano: «news e documenti non scelgono» è nel codice.
- **§16.6** (il form generato): un campo `slugFrom` nascosto propone lo stesso; `slugMaxLength` taglia la proposta alla lunghezza
  della colonna; l'anteprima dell'indirizzo offre lo slug libero a chi non scrive lo slug (§4, scelta 3); `cannotProposeASlug`
  dice quando un titolo scritto non propone niente (§4, scelta 5).
- **§9.3**, «nuovo da template» (design M0 §5.6): la richiesta porta il titolo della riga nuova, facoltativo (§4, scelta 4).
