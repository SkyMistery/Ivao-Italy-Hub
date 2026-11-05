# Le azioni di un evento dette dal server (#333, punto 1)

**Data:** 1° novembre 2026, revisione del codice, lotto 10 (web), issue #333; il nucleo nell'issue #427.
**Stato:** **decisa** da Carmine: la **(A)**, come raccomandata — «Tutte come consigli» (1° novembre 2026, in chat al master, che l'ha
pubblicata sull'issue su sua istruzione: [risposta][a427]): prima una PR del nucleo a parte (`CrudOptions.ToDetailPage` con i servizi, e la
rotta del manifest `unscoped`), con questa nota; poi una PR degli eventi in cui il server dice le azioni sull'evento e sulla postazione. Non
la (B), né la (C). La nota è nata sul branch della #426 (il lotto 10), con lo stato «Proposta»; il master ha chiesto alla #426 di toglierla,
e viaggia con il lavoro che la applica: la PR del nucleo la porta, la PR degli eventi chiude la #427. La forma del §5 è una scelta tecnica
della PR del nucleo, sopra quella risposta. **Regola applicata:** `CLAUDE.md` §5, caso **(b)** con un'estensione del nucleo: una PR del
nucleo a parte, prima di quella del modulo (`CLAUDE.md` §0, regola 6). Il punto 1 della #333 è passato alla #427 (e il punto 3, un difetto del
nucleo, alla #428): la PR del lotto 10 corregge gli altri quattro e chiude la #333.

[a427]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/427#issuecomment-6555197465

## 1. Che cosa serve

La pagina di un evento nel back office e l'ATC di un evento decidono i pulsanti dal bootstrap, con `holdsPermission(bootstrap, permesso,
dipartimento)` (`web/src/shared/api/bootstrap.ts`). Quella funzione guarda solo il nome e il dipartimento:

- chi ha `Events.Edit` **sull'evento 42** (`resource_scope`) vede «Modifica», «Pubblica», «Annulla» e «Duplica» su **ogni** evento del
  dipartimento (`screens/events.tsx`, le schede Rotte, Prenotazioni, ATC, Report allo stesso modo); ogni clic è un 403;
- un membro di una squadra di FIR vede «Salva» ed «Elimina» sulle postazioni degli **altri** FIR (`screens/atc.tsx`): il bootstrap non porta
  il FIR (#319, punto 7);
- le rotte dei registri (`modules/events/index.ts`) si aprono a chi ha il permesso su una riga sola, e poi la lista risponde 403: il server
  chiede `HasUnscoped`, la guardia della SPA `holdsPermissionAnywhere`.

## 2. Perché nessun meccanismo basta oggi

- **Il dettaglio generato non sa chi legge.** `CrudOptions.ToDetail` è `Func<TEntity, TDetailDto>`: niente servizi, niente utente. Per
  questo `EventDetailDto` non può dire che cosa il lettore fa sulla riga, e il dettaglio di una postazione dice sempre `Editable = false`
  (la lista lo dice giusto, perché `ToListPage` ha i servizi). Il Training e i trasferimenti lo dicono (`actions.canConduct`, `decidable`)
  perché i loro endpoint sono scritti a mano.
- **Il bootstrap non basta per una riga.** Porta `resourceScope`, ma non il FIR né il «da fuori» (#319, punto 7). E anche con quelli, la
  SPA dovrebbe rifare le regole dell'unico handler (ambito, FIR, chi ha interesse, da fuori): una seconda copia, contro `CLAUDE.md` §2.
- **Una rotta del manifest ha solo `permission`.** Non c'è un modo di dire «solo senza ambito», come `NavItemDescriptor(…, Unscoped: true)`
  fa per la voce del menu.

## 3. Le alternative

- **A (raccomandata, decisa): il server dice le azioni, con due pezzi del nucleo.**
  1. `CrudOptions.ToDetailPage`: `Func<TEntity, IServiceProvider, CancellationToken, Task<TDetailDto>>`, accanto a `ToListPage`, letta da
     `MapCrud` al posto di `ToDetail` quando c'è. Nessuna regola nuova: il modulo chiede l'unico handler sulla riga.
  2. Una rotta del manifest `unscoped: true`, che la guardia (`web/src/app/router.ts`) legge come «tenuto senza ambito di riga»: un permesso
     del bootstrap con `resourceScope === null`. È la stessa domanda di `HasUnscoped`, che il bootstrap sa già rispondere.

  Poi la PR degli eventi: `EventDetailDto.Actions` (modifica, elimina, pubblica/annulla/duplica, e per ogni scheda leggere e scrivere),
  `EventAtcPositionDto.Editable` anche sul dettaglio, «Nuova postazione» dall'`AtcEventDto` (dopo la #311, che tocca lo stesso endpoint), e
  `unscoped: true` sulle quattro rotte dei registri. Le pagine chiedono quelli, mai `holdsPermission`.
- **B: la SPA legge ambito e FIR dal bootstrap** (`holdsPermissionOn(bootstrap, permesso, riga)`, dopo #319 punto 7). Meno codice sul server,
  ma le regole dell'handler scritte due volte. Sconsigliata, non scelta.
- **C: un endpoint del modulo** `GET /api/events/events/{id}/actions`, senza toccare il nucleo. Una richiesta in più per pagina, e il
  dettaglio di una postazione resterebbe senza `Editable`: servirebbe un secondo endpoint. Sconsigliata, non scelta.

## 4. La domanda a Carmine

> Punto 1 della #333 (i pulsanti degli eventi decisi dal bootstrap, che ignora l'ambito della riga e il FIR): va bene **A** — due pezzi
> del nucleo in una PR loro (`CrudOptions.ToDetailPage` con i servizi, e la rotta del manifest `unscoped`), poi una PR degli eventi in cui
> il server dice le azioni sull'evento e sulla postazione — invece di **B** (la SPA rifà ambito e FIR dal bootstrap) o **C** (un endpoint
> delle azioni nel modulo)?

**Risposta:** **(A)**, «Tutte come consigli», data da Carmine in chat al master il 1° novembre 2026 e pubblicata su sua istruzione
sull'[issue #427][a427] (autore `SkyMistery`).

## 5. La forma nel nucleo (scelta tecnica della PR del nucleo)

- **`ToDetailPage` è lo specchio di `ToListPage`.** Sta accanto a `ToDetail` in `CrudOptions` (`src/IvaoHub.Core/Data/Crud/CrudOptions.cs`);
  `MapCrudExtensions` la chiama, in un solo punto (`DetailAsync`), per la lettura, la creazione e la modifica: le tre risposte che danno il
  dettaglio. La riceve **dopo** che il permesso sulla riga è stato chiesto e, nelle scritture, dopo il salvataggio: la riga che il modulo
  descrive è quella che il database ha. `ToDetail` resta obbligatoria, come `ToList` lo è accanto a `ToListPage`: il controllo all'avvio
  non cambia, e una risorsa che non la usa non cambia in niente.
- **Il motore non aggiunge regole.** Che cosa il lettore fa sulla riga lo chiede il modulo all'unico handler
  (`IAuthorizationService.AuthorizeAsync(lettore, riga, permesso)`), come già fa `StaffAvailability` nella lista delle postazioni: ambito,
  FIR, chi ha interesse e «da fuori» restano dove sono. Il test (`DetailPageTests`, sul modulo di prova, `SampleModule.ActionsPattern`) lo
  prova sul `Sample.Decide`: sì sulla riga del grant, no sulla vicina, no sulla riga di chi ha interesse anche se il grant la nomina, e la
  stessa risposta da `POST` e `PUT`.
- **`unscoped` è un campo opzionale di `RouteDefinition`** (`web/src/shared/modules.ts`), letto solo con `permission` e solo sotto `staff`.
  Assente vale «no»: ogni rotta che c'è chiede quello che chiedeva (`holdsPermissionAnywhere`). La decisione della guardia sta in una
  funzione sola, `opensStaffRoute` (`web/src/app/staffRoute.ts`), che `router.ts` chiama nel `beforeLoad`; la domanda «senza ambito» è
  `holdsPermissionUnscoped` accanto a `holdsPermissionAnywhere` (`web/src/shared/api/bootstrap.ts`), lo specchio di
  `PermissionSet.HasUnscoped`: il bootstrap del super amministratore porta ogni permesso senza ambito, quindi lo tiene. Una rotta
  `unscoped` senza `permission` non dice niente e si apre allo staff, come oggi.
- **Nessun uso negli eventi qui.** `EventDetailDto.Actions`, `Editable` sul dettaglio della postazione e `unscoped: true` sulle rotte dei
  registri sono la PR degli eventi che segue, in coda dopo questa.

## Da portare nel piano

- §16.6 (il motore CRUD): `CrudOptions.ToDetailPage`, il dettaglio con i servizi della richiesta accanto a `ToListPage`, per la lettura, la
  creazione e la modifica; nessuna regola nuova.
- §9.7 e design M0 §7.3 (la guardia di una rotta di modulo): `RouteDefinition.unscoped`, «tenuto senza ambito di riga», la stessa domanda di
  `HasUnscoped` e di `NavItemDescriptor(…, Unscoped: true)`; `CLAUDE.md` §2, la riga «a permission on one row only», nomina la rotta accanto
  alla voce del menu.
- Design M4 §7.2: le azioni della pagina di un evento e dell'ATC dette dal server (con la PR degli eventi).
