# Il grant su una riga non dà il dipartimento, e la lista mostra le righe del suo ambito

**Data:** 21 ottobre 2026 — fase di test (nota `2026-10-18-la-fase-di-test`), revisione del codice per lotti (nota
`2026-10-20-la-revisione-del-codice`, lotto 1)
**Stato:** **decisa da Carmine** — advisory privato, risposta del maintainer del 21 ottobre 2026 («Tutte come suggerisci tu», in chat
al master): la **(a)**. Un grant su una riga **non dà il dipartimento**; apre la sua riga all'unico handler e, in una lista generata,
**solo le righe del suo ambito**: il filtro della lista impara l'ambito invece di leggere il claim del dipartimento. La forma nel codice
(§3) è di questa nota.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: si estendono due meccanismi del nucleo che ci sono — i claim `dept` di
`HubClaims.BuildIdentity` e il filtro di dipartimento della lista generata (`MapCrudExtensions.TryNarrowToDepartments`) —, sulla strada
che hanno già il permesso tenuto su un FIR (A11a) e quello tenuto da fuori del dipartimento (E2b). `CLAUDE.md` §0 regola 6: una PR del
nucleo a sé.

## 1. Che cosa c'era, letto nel codice (`main` a `ff9bea77`)

1. **`BuildIdentity`** scrive un claim `dept` per ogni dipartimento che un grant nomina (nota
   `2026-09-06-autorizzare-su-un-pezzo-di-un-altro-dipartimento`), e ne lascia fuori il permesso tenuto su un FIR (A11a) e quello tenuto
   da fuori (E2b). **Non** ne lasciava fuori il permesso tenuto su una riga sola (`ResourceScope`, nota
   `2026-09-15-permessi-su-una-riga-e-chi-ha-interesse`): un grant `EventBookings.View` su un evento dava il claim `ED`.
2. **Quel claim lo leggono in tre**: il filtro della lista generata (`currentUser.Departments`), il filtro globale di visibilità
   (`HubDbContext`, `ModuleDbContext`: `VisibleDepartments`) e il bootstrap (`/api/me`, `user.departments`, da cui la SPA ricava i
   dipartimenti in cui lavorare). Con un grant su una riga, quindi, una lista generata rispondeva con le righe del dipartimento, mentre il
   dettaglio delle stesse righe, chiesto all'unico handler con l'ambito della riga, rispondeva 403; e il filtro di visibilità contava il
   dipartimento come per chi ci lavora.
3. **Il commento del codice diceva già il contrario** (`TryNarrowToDepartments`: «one held on a single row opens that row to the handler
   and no list»), e così l'intento della nota `2026-10-17-il-permesso-senza-ambito`. La nota `2026-10-09-il-grant-su-una-riga-scrive-la-sua-riga`
   §3.5 diceva invece «La lista di chi ha un grant su un evento solo resta quella di oggi», e la nota del 17 ottobre §4 contava «ogni lista
   che un grant su una riga apre oggi» fra i comportamenti da non cambiare. **Questa nota cambia quella frase del §3.5**: la lista di chi ha
   un grant su un evento solo ora mostra le righe di quell'evento e nessun'altra.
4. **Lo scope di una riga non è una colonna.** `IHasResourceScope.ResourceScope` è calcolato dalle colonne della riga
   (`Event.ScopeOf(EventId)`), e ogni modulo lo dichiara `Ignore` nel suo contesto: il database non lo conosce, e il nucleo non legge la
   forma degli scope (nota del 15 settembre §3.1). Il FIR, invece, è una colonna (`IHasFir`), e la lista lo confronta in SQL.
5. **La coda della validazione dei tour** (`/api/flightops/review/queue`, M2) è una lista generata dei PIREP con la policy
   `Tours.Validate`, e un validatore abilitato su un tour solo la leggeva **tutta** proprio grazie al claim del dipartimento. È voluto: il
   design M2 §4.1, deciso da Carmine, dice «chi ha `Tours.Validate` su almeno un tour vede **tutti** i PIREP in sola lettura; "Prendi" c'è
   solo sui tour per cui è abilitato», e lo prova `PirepTests.Review` («The other tour: read, not taken»). L'advisory supponeva che la coda
   restringesse da sé per ambito: non è così, e senza il claim un validatore su un tour avrebbe visto solo il suo tour.

## 2. Perché nessun meccanismo lo copre così com'è

- Togliere lo scope dal claim e basta chiuderebbe la lista: chi ha solo grant su righe non ha dipartimenti, e oggi riceve 403. La lista
  deve poter aprire **le righe dell'ambito**, che l'unico handler già apre una per una.
- Restringere in SQL sullo scope chiederebbe una colonna o un'espressione per ogni entità con uno scope (venti righe degli eventi, il
  PIREP, il modulo di prova), cioè la forma `events:event:{id}` scritta una seconda volta accanto a `ScopeOf`, nel codice dei moduli.
- La coda di M2 ha bisogno di un'eccezione dichiarata, non di un caso speciale nel nucleo: è la risorsa che la dice, come dice le righe
  condivise per la lettura (`SharedForReading`) e quelle che nessuno scrive (`ReadOnlyRows`).

## 3. La forma nel codice

### 3.1 `HubClaims.BuildIdentity`: un permesso su una riga non porta il dipartimento

Accanto a `Fir is null && !FromOutside`, un terzo `ResourceScope is null`: un grant tenuto su una riga non scrive il claim `dept`, come
non lo scrive un permesso su un FIR o da fuori. Lo stesso grant **su tutto il dipartimento** (senza scope) lo scrive come prima. Il
claim `perm` non cambia: porta lo scope dopo `@` come sempre.

Di conseguenza, **senza cambiare una riga** del filtro:

- il **filtro globale di visibilità** non conta più quel dipartimento per chi ha solo un grant su una riga: le righe
  `Visibility.Department` del dipartimento non le legge più; le altre visibilità restano come sono (pubbliche, dei membri, dello staff);
- il **bootstrap** non elenca più quel dipartimento in `user.departments`: la SPA non offre a chi collabora su un evento le pagine del
  dipartimento intero. I permessi in `/api/me` restano gli stessi, con il loro scope.

### 3.2 La lista generata: le righe dell'ambito

`TryNarrowToDepartments` decide come oggi chi può aprire la lista e con quali righe di dipartimento e di FIR; in più raccoglie i
permessi di lettura della lista **tenuti su una riga** (`ResourceScope` non nullo). Su un'entità `IHasResourceScope`:

1. **Chi ha solo grant su righe non riceve più 403**: la lista è aperta, e contiene le righe del suo ambito.
2. **Quali righe**: quelle per cui l'unico handler direbbe sì alla lettura — su uno dei dipartimenti della riga, con il suo scope e il suo
   FIR, `ICurrentUser.Has(lettura, dipartimento, riga.ResourceScope, riga.Fir)`. La domanda è la stessa dell'handler, chiesta con lo
   stesso `PermissionSet`, quindi lista e dettaglio rispondono uguale.
3. **Come, senza una colonna**: dopo i filtri e la ricerca della richiesta, il motore legge le righe candidate — quelle dei dipartimenti
   su cui i permessi su una riga sono tenuti, tolti quelli che chi legge ha già interi —, calcola in memoria lo scope di ciascuna con la
   sua proprietà, e restringe la lista, in SQL, a `readable OR chiave IN (…)`. Conteggio, ordine e pagine restano del database. Lo scope
   resta scritto **una volta**, nell'entità; il nucleo lo confronta, non lo legge.
   - ⚠️ **Il costo, detto**: per chi ha **solo** grant su righe la lista legge tutte le righe candidate prima di paginare. Le liste degli
     eventi sono chieste con il filtro dell'evento (`filter[eventId]`), che riduce le candidate a quelle dell'evento; senza filtro sono le
     righe del dipartimento. Per lo staff del dipartimento, per i team dei FIR e per chi legge da fuori non cambia niente: non ha permessi
     su righe da aggiungere, o li ha su un dipartimento che legge già intero. Se un giorno servisse di più, la strada è un'espressione dello
     scope dichiarata dall'entità e compilata per la proprietà (la disciplina di `SharedForReading`): non ora.
4. **Un'entità senza scope**: un permesso su una riga non vi apre righe. Chi ha solo quello riceve **403**, come oggi chi non ha nessun
   dipartimento. È il caso di un validatore su un tour davanti alla lista dei tour (`Tour` non ha scope): la pagina della coda lo sa già
   («A validator enabled on one tour by a grant may not hold `Tours.View`: then there is no filter», `review.tsx`).
5. **Le righe condivise per la lettura** (`SharedForReading`) non si aggiungono a chi ha solo grant su righe: oggi riceveva 403, e i
   modelli dei contenuti non hanno uno scope.

### 3.3 La coda di M2: `CrudOptions.ReadableAcrossScopes`

Un segno della risorsa, `false` per difetto: **chi tiene il permesso di lettura della lista su una riga legge le righe del dipartimento
di quel permesso**, non solo quelle del suo ambito — esattamente ciò che il claim gli dava prima, ma **solo in questa lista e con questo
permesso**, come il permesso da fuori di E2b. Lo dichiara la coda della validazione (`ReviewEndpoints`), e nessun'altra risorsa. Il
design M2 §4.1 resta com'è: il validatore su un tour legge tutta la coda, prende e decide solo i PIREP del suo tour (l'handler, riga per
riga); e il dipartimento non gli apre più niente fuori dalla coda.

### 3.4 Che cosa non cambia

- **L'unico handler**: nessuna riga. Il dettaglio di una riga risponde come oggi (sì con lo scope della riga, 403 sulle altre).
- **`HasAny`** e **`HasUnscoped`**: nessuna riga. `HasAny` apre ancora la lista a chi tiene il permesso su una riga (§3.2 punto 1 ne è la
  conseguenza voluta); `HasUnscoped` continua a chiuderla dove serve tutta la divisione (i registri di E13b).
- **Il guardiano dell'interceptor** (E10i): un grant su una riga scrive la sua riga e quelle sotto di lei, come prima.
- **Il permesso su un FIR** (A11a) e **da fuori** (E2b): le loro strade nella lista non cambiano.
- **Il cookie**: il formato del claim `perm` è lo stesso; un cookie scritto prima del rilascio porta ancora il claim `dept` fino al
  prossimo accesso o al prossimo cambio del suo `security_stamp` (la durata della sessione, nota a parte).
- Nessuna migrazione, nessuna forma di `/api/me` (cambia il contenuto di `user.departments` per chi ha solo grant su righe).

## 4. Alternative scartate

| Alternativa | Perché no |
|---|---|
| **(b) dell'advisory**: lasciare il comportamento com'era | Carmine ha scelto la (a) |
| Togliere il claim e lasciare la lista com'è | chi ha solo un grant su un evento riceverebbe 403 sulle liste del suo evento, e le pagine dell'evento non funzionerebbero |
| Lo scope in SQL, con un'espressione o una colonna dichiarata da ogni entità | la forma dello scope scritta due volte, in venti entità di due moduli; un'entità dimenticata darebbe 403 (§2) |
| Filtrare e paginare tutto in memoria | i filtri, la ricerca e l'ordine sono espressioni per il database (funzioni sulle colonne tradotte); il conteggio e le pagine resterebbero diversi da ogni altra lista |
| Restringere anche la coda di M2 all'ambito | contro il design M2 §4.1, deciso da Carmine, e contro il suo test |
| Il segno della coda nel catalogo dei permessi (`Tours.Validate` «legge tutta la lista») | è la lettura di una lista, non il permesso, che vuole il dipartimento intero (lo stesso argomento della nota del 17 ottobre §4) |

## 5. I test

- **Integrazione, nuova classe `ScopedGrantListTests`** (eventi veri, un VID senza posizione e senza indirizzo, grant scritti nella
  tabella come li scrive una schermata del modulo):
  1. con `EventBookings.View` su un evento, la lista delle prenotazioni contiene quelle dell'evento e nessuna dell'altro, con e senza
     `filter[eventId]`; il dettaglio risponde sì sulla sua e 403 sull'altra, come prima;
  2. con `Events.Edit` su un evento, la lista degli eventi contiene quell'evento e non l'altro; il dettaglio come prima;
  3. con un grant su un evento solo, il bootstrap non elenca il dipartimento.
- **La prova al contrario**: con il permesso su una riga rimesso nel claim del dipartimento (la riga di `BuildIdentity` di `main`), i test
  delle liste cadono.
- **Restano verdi senza toccarli**: la coda dei validatori su un tour (`PirepTests.Review`), `EventsScopedGrantTests`, `EventsBookingsTests`,
  i test dei team dei FIR e «da fuori», gli architetturali.

## 6. Che cosa si tocca

- `src/IvaoHub.Core/Auth/HubClaims.cs` (`BuildIdentity`, una condizione e il commento);
- `src/IvaoHub.Core/Auth/ICurrentUser.cs` (il riassunto di `Departments`);
- `src/IvaoHub.Core/Data/Crud/MapCrudExtensions.cs` (`ListAsync`, `TryNarrowToDepartments` e il restringimento sulle righe dell'ambito);
- `src/IvaoHub.Core/Data/Crud/CrudOptions.cs` (`ReadableAcrossScopes`);
- `src/IvaoHub.Modules.FlightOps/Review/ReviewEndpoints.cs` (una riga: la coda dichiara il segno);
- `tests/IvaoHub.IntegrationTests/ScopedGrantListTests.cs` (nuovo). **Nessun test esistente cambiato.**

## Da portare nel piano

- **§16** (punto 2, l'unico handler e la lista generata; e il punto sulla lista di `MapCrud`): **dal 21 ott 2026** un grant su una riga
  (`resource_scope`) **non dà il dipartimento** (`dept`), come un permesso su un FIR o da fuori: né al filtro globale di visibilità né al
  bootstrap. Nella lista generata apre **le righe del suo ambito** — quelle a cui l'unico handler direbbe sì —, lette in memoria fra le
  candidate e ristrette in SQL per chiave; su un'entità senza scope non apre righe (403 a chi ha solo quello). Una risorsa dichiara
  `CrudOptions.ReadableAcrossScopes` quando chi legge su una riga deve leggere il dipartimento intero: solo la coda della validazione dei
  tour (design M2 §4.1).
- **§6.3**, la voce «Un grant su una riga sola»: oltre all'handler e al guardiano, la lista generata mostra le righe del suo ambito.
- **`CLAUDE.md` §2**, la riga «A permission on one row only»: aggiungere che un grant su una riga non dà il dipartimento, e che la lista
  generata ne mostra le righe del suo ambito (`ReadableAcrossScopes` per la coda dei tour); citare questa nota.
- **La nota `2026-10-09-il-grant-su-una-riga-scrive-la-sua-riga` §3.5** («La lista … resta quella di oggi») non vale più: lo dice il
  piano, la nota resta com'è.
- **`09-design-m4.md` §6.2** (i grant su un evento): chi collabora su un evento lista le righe di quell'evento.
