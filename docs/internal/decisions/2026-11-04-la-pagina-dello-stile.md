# La pagina dello stile della divisione, in coda alle istruzioni per Claude

- **Data**: 4 novembre 2026
- **Stato**: **decisa — Carmine in chat al master, 4 novembre 2026** («(a), PR a parte»). La risposta è in chat e non in un
  commento, perché la sessione è di Carmine: come il §9 del design `12-design-editor-fase-3.md`
- **Chi**: la sessione di lavoro della PR 3.0a («Scarica per Claude», #449); questa è la PR del nucleo che la segue, in coda
- **Classificazione** (`CLAUDE.md` §5): **(b)**, un meccanismo che c'è già (`hub_division_settings`, una riga chiave/valore che il
  nucleo usa per sé: `ContentSeeder`, `PositionGrantSeeder`, `SuperadminService`) esteso a un'impostazione dei contenuti; tocca il
  nucleo del server, quindi PR sua con questa nota

## 1. Che cosa serve

La nota `2026-10-30-una-pagina-scritta-con-claude` (§3 e risposta 1 del §8) chiede, in coda alle istruzioni scaricate, **la pagina
dello staff nel CMS che descrive lo stile della divisione** (il tono, che cosa va in una news), **scelta nelle impostazioni**; «senza
la pagina scelta, la parte non c'è». La 3.0a (#449) è uscita senza, con il punto dove si aggancia (`style` del compositore).

Il nucleo però non ha impostazioni del sito: ci sono solo quelle dei moduli (`/api/modules/{key}/settings`, una riga per modulo).
Il master ha posto la domanda a Carmine il 4 novembre 2026, e Carmine ha risposto «(a), PR a parte».

## 2. Che cosa si fa (la risposta (a))

- **La riga**: la chiave `content.stylePage` in `hub_division_settings`, con il valore `{ "contentId": <id> }`, oppure nessuna riga
  se non è scelta nessuna pagina. **Nessuna migrazione**: la tabella c'è e le chiavi sono libere. La riga è `[Audited]` come le altre:
  il cambio lascia una traccia con chi l'ha fatto.
- **Le due letture**, a mano, in `Core/Content/ContentSettingsEndpoints.cs`:
  - `GET /api/content/settings`, dietro `Content.Edit` (chi scarica le istruzioni). Risponde `stylePageId` e `stylePagePath`. Il
    percorso è letto **con il filtro di visibilità di chi chiede** e solo se la pagina è pubblicata: è `null` quando chi chiede non può
    leggerla, oppure quando la pagina non è più pubblicata.
  - `PUT /api/content/settings`, dietro `Content.ManageTemplates` **senza ambito su una riga** (`ICurrentUser.HasUnscoped`): chi cura i
    modelli della divisione cura anche lo stile, e un grant su una riga sola non decide un'impostazione di tutta la divisione
    (`decisions/2026-10-17-il-permesso-senza-ambito.md`). `null` toglie la scelta.
- **La pagina scelta deve poterla leggere chi scarica.** Chi scarica ha `Content.Edit`, quindi è dello staff, ma di un dipartimento
  qualunque. Per questo il PUT accetta solo una **pagina** (`kind = Page`, non un modello), **pubblicata**, con visibilità `Public`,
  `Members` o `Staff`. Rifiuta `Department`, che uno staffer di un altro dipartimento non legge. I rifiuti passano da `Refusals`, sul
  campo `stylePageId`: `errors.content.settings.stylePageNotFound` per una pagina che chi sceglie non trova pubblicata (la stessa
  lettura filtrata del sito: una bozza e una pagina che non può leggere sono la stessa risposta, senza dire che una riga esiste) e
  `errors.content.settings.stylePageTooNarrow` per una pagina di un dipartimento solo.
- **Allo scaricamento il browser rilegge la pagina con i diritti di chi scarica**: la lettura pubblica `/api/content/public/page`, la
  stessa che usa il sito, che conosce solo la versione pubblicata. Se la pagina è stata tolta, ristretta o non si legge, la parte non
  c'è, come chiede la nota. Il corpo diventa testo nel browser, nella lingua dello schermo: i titoli come titoli, il Markdown di
  `text`, poi ogni testo tradotto di un blocco come paragrafo. Il server non legge le `props` (`CLAUDE.md` §2).
- **Lo stile non entra nell'impronta delle regole**: è prosa (nota del 30 ottobre, §4). Cambiare la pagina dello stile non fa
  rifiutare un file già scaricato.
- **Il pannello «Impostazioni»** sta nella schermata dei contenuti (`/staff/content`), per chi ha `Content.ManageTemplates` senza
  ambito. È un `Dialog` dell'elenco chiuso con il form generato: un campo solo, la pagina da un elenco delle pagine pubblicate che
  tutto lo staff legge (il lettore degli indirizzi della 3.0a, `siteAddresses.ts`), oppure nessuna.

## 3. Che cosa tocca

- **Server, nucleo**: `Core/Content/ContentSettingsEndpoints.cs` (nuovo; due letture a mano in più, da contare nel §16.6 del piano),
  la registrazione accanto agli altri endpoint dei contenuti, le chiavi di errore in `locales/*/errors.json`.
- **Browser, nucleo**: `features/content/` (la lettura, la mutazione, il pannello, il testo dello stile nel compositore),
  `shared/api/bootstrap.ts` (`holdsPermissionUnscoped`), il client generato, i file di lingua.
- **Test**: d'integrazione dei due verbi (permessi, rifiuti, lettura con la visibilità di chi chiede), unitari del testo dello stile,
  e2e del pannello e della parte in coda al file.

## 4. Alternative considerate

| | Che cosa | Perché no |
|---|---|---|
| (b) | Lo slug della pagina in `division.json` | Più piccola, ma per cambiarla serve una consegna; Carmine ha scelto la (a) |
| — | Un'impostazione del modulo dei contenuti | I contenuti non sono un modulo: `/api/modules/{key}/settings` vuole un `IModule` |
| — | Il server compone il testo dello stile | Dovrebbe leggere le `props` dei blocchi, che il server non legge mai |

## Da portare nel piano

- **§16** (meccanismi generici): le impostazioni del nucleo dei contenuti stanno in `hub_division_settings`, una chiave per
  impostazione (`content.stylePage`), lette e scritte da `/api/content/settings`. Lo scrive chi ha `Content.ManageTemplates` senza
  ambito; la pagina dello stile è una pagina pubblicata che tutto lo staff legge, e si rilegge con i diritti di chi scarica.
- **§16.6**: due letture a mano in più nella famiglia dei contenuti (`GET` e `PUT /api/content/settings`).
- **§9.3** (modello dei contenuti), con la nota del 30 ottobre: le istruzioni per Claude finiscono con lo stile della divisione quando
  è scelta una pagina che chi scarica può leggere, e lo stile non entra nell'impronta delle regole.
