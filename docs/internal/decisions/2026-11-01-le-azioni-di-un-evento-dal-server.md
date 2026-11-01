# Le azioni di un evento dette dal server (#333, punto 1)

**Data:** 1° novembre 2026, revisione del codice, lotto 10 (web), issue #333.
**Stato:** **Proposta**. La domanda a Carmine è sull'[issue #427](https://github.com/SkyMistery/Ivao-Italy-Hub/issues/427), aperta
dalla sessione coordinatrice con questa raccomandazione (A); la risposta, con il suo link, si scrive qui prima del codice. **Regola
applicata:** `CLAUDE.md` §5, caso **(b)** con un'estensione del nucleo: una PR del nucleo a parte, prima di quella del modulo
(`CLAUDE.md` §0, regola 6). Il punto 1 della #333 è passato alla #427 (e il punto 3, un difetto del nucleo, alla #428): la PR del lotto
10 corregge gli altri quattro e chiude la #333.

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

- **A (raccomandata): il server dice le azioni, con due pezzi del nucleo.**
  1. `CrudOptions.ToDetailPage` (o `ToDetailAsync`): `Func<TEntity, IServiceProvider, CancellationToken, Task<TDetailDto>>`, accanto a
     `ToListPage`, letta da `MapCrud` al posto di `ToDetail` quando c'è. Nessuna regola nuova: il modulo chiede l'unico handler sulla riga.
  2. Una rotta del manifest `unscoped: true`, che la guardia (`web/src/app/router.ts`) legge come «tenuto senza ambito di riga»: un permesso
     del bootstrap con `resourceScope === null`. È la stessa domanda di `HasUnscoped`, che il bootstrap sa già rispondere.

  Poi la PR degli eventi: `EventDetailDto.Actions` (modifica, elimina, pubblica/annulla/duplica, e per ogni scheda leggere e scrivere),
  `EventAtcPositionDto.Editable` anche sul dettaglio, «Nuova postazione» dall'`AtcEventDto` (dopo la #311, che tocca lo stesso endpoint), e
  `unscoped: true` sulle quattro rotte dei registri. Le pagine chiedono quelli, mai `holdsPermission`.
- **B: la SPA legge ambito e FIR dal bootstrap** (`holdsPermissionOn(bootstrap, permesso, riga)`, dopo #319 punto 7). Meno codice sul server,
  ma le regole dell'handler scritte due volte. Sconsigliata.
- **C: un endpoint del modulo** `GET /api/events/events/{id}/actions`, senza toccare il nucleo. Una richiesta in più per pagina, e il
  dettaglio di una postazione resterebbe senza `Editable`: servirebbe un secondo endpoint. Sconsigliata.

## 4. La domanda a Carmine

> Punto 1 della #333 (i pulsanti degli eventi decisi dal bootstrap, che ignora l'ambito della riga e il FIR): va bene **A** — due pezzi
> del nucleo in una PR loro (`CrudOptions.ToDetailPage` con i servizi, e la rotta del manifest `unscoped`), poi una PR degli eventi in cui
> il server dice le azioni sull'evento e sulla postazione — invece di **B** (la SPA rifà ambito e FIR dal bootstrap) o **C** (un endpoint
> delle azioni nel modulo)?

**Risposta:** in attesa, sulla [#427](https://github.com/SkyMistery/Ivao-Italy-Hub/issues/427).

## Da portare nel piano

- §16 (meccanismi generici): `MapCrud` con un dettaglio che riceve i servizi, se A.
- §9.7 e design M0 §7.3: la guardia di una rotta di modulo «senza ambito», se A.
- Design M4 §7.2: le azioni della pagina di un evento e dell'ATC dette dal server.
