# L'ATC di un solo evento: il grant su un evento legge quell'evento (#311)

**Data:** 1 novembre 2026 — fase di test, correzione della issue #311 (branch `fix/events-atc-view-one-event`)
**Stato:** **decisa** da Carmine il 19 ottobre 2026, in chat al master, che l'ha pubblicata sulla issue su sua istruzione
([risposta][a]): «per il grant su un solo evento si, come consigli» — la raccomandazione del master. Questa nota registra quella risposta
e **corregge la lettura 1** di `2026-10-12-le-postazioni-e-la-disponibilita` (§2), come la risposta chiede; la nota del 12 ottobre resta com'è.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: si usano i pezzi del nucleo che ci sono — l'unico handler chiesto sulla riga
dell'evento e `ICurrentUser.HasUnscoped` di E10u (`2026-10-17-il-permesso-senza-ambito`) — e niente del nucleo cambia.

[a]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/311#issuecomment-6269150807

## 1. Che cosa non andava

La lettura 1 della nota del 12 ottobre diceva le intestazioni degli eventi con un roster (`GET /api/events/atc/events` e `/{id}`) lette
**con `EventAtc.View` chiesto senza riga**: chi tiene l'ATC «da qualche parte» legge l'intestazione di ogni evento con un roster. Era
scritta prima di E10u, quando l'unico handler senza riga sapeva rispondere solo `HasAny`. Così un grant di `EventAtc.View` **su un solo
evento** (`resource_scope` `events:event:{id}`) apriva le intestazioni di **tutti** gli eventi con un roster non finito — id,
dipartimenti, slug, titolo, stato, date e date del roster, bozze e eventi di altri dipartimenti compresi; nessun dato dei membri. I
registri accanto (E13b) chiedevano già `HasUnscoped`.

## 2. La risposta, e la forma

**Un grant su un evento resta su quell'evento**, come per i registri: la lista e l'intestazione rispondono solo per gli eventi che chi
legge raggiunge sulla riga, e la voce di menu segue.

- **Chi tiene `EventAtc.View` senza un ambito** (`HasUnscoped`: lo staff del dipartimento, l'AOD, i team dei FIR, un grant a una
  posizione, l'HQ, il super amministratore) legge **come prima** tutti gli eventi con un roster non finito: lo staff di un FIR non legge
  l'evento, e un evento non dice un FIR, quindi chiedere loro la riga li chiuderebbe fuori.
- **Chi lo tiene solo su singoli eventi**: la lista tiene gli eventi che l'unico handler gli concede sulla riga (`EventAtc.View`
  sull'`Event`, con il suo ambito), trovati fra gli eventi con un roster non finito — pochi — e ristretti in SQL alle loro chiavi, così
  conteggio e pagine restano del database (la stessa via di `NarrowToScopesAsync` della lista generata, che qui non si può chiamare:
  la lista è scritta a mano). L'intestazione di un altro evento risponde **403**, come i candidati di una postazione; un evento che non c'è,
  o senza roster, **404** come prima.
- **La voce di menu** `events:nav.atc` resta senza `Unscoped`: la lista mostra a chi ha un grant su un evento proprio quell'evento, e la
  pagina dell'ATC di quell'evento legge le sue postazioni dalla lista generata, già ristretta all'ambito.

Una domanda sola in `AtcPositionEndpoints.ReachesAsync`, chiamata dalla lista e dall'intestazione: `HasUnscoped(EventAtc.View)`, o
l'unico handler sull'evento. Nessun controllo dei permessi scritto a mano.

## 3. Alternative scartate

- **Tutte e due le letture solo con `HasUnscoped`** (403 a chi ha un grant su un evento): chi tiene l'ATC di un solo evento non
  arriverebbe più nemmeno al suo evento, e la pagina dell'ATC di quell'evento parte dall'intestazione. La risposta dice «reaches that
  event only», non «reaches nothing».
- **L'unico handler sulla riga per tutti**: lo staff di un FIR tiene `EventAtc.View` sul suo FIR, e un `Event` non dice un FIR
  (`IHasFir`): l'handler lo rifiuterebbe su ogni evento, e la issue chiede che team dei FIR, staff e super amministratore tengano quello
  che hanno.
- **Leggere gli id degli eventi dagli ambiti dei grant**: è il controllo scritto a mano che l'unico handler esiste per evitare.

## 4. Che cosa si tocca

- `src/IvaoHub.Modules.Events/Atc/AtcPositionEndpoints.cs`: `EventsAsync`, `EventAsync` (con `Produces(403)`) e `ReachesAsync`.
- `web/src/shared/api/schema.d.ts`: rigenerato (`pnpm gen:api`), il 403 dell'intestazione.
- `tests/IvaoHub.IntegrationTests/EventsAtcOneEventTests.cs`: un grant su un evento (la lista tiene solo quell'evento, l'altro è
  rifiutato, il menu c'è) e lo stesso permesso senza ambito (tutti e due gli eventi). Il primo è rosso sul codice di `main`.

## Da portare nel piano

- **`09-design-m4.md` §4.1** («dove lavora chi tiene l'ATC senza gli eventi») e la **lettura 1** della nota
  `2026-10-12-le-postazioni-e-la-disponibilita`: le intestazioni chiedono `HasUnscoped(EventAtc.View)` per tutti gli eventi con un
  roster; a chi tiene `EventAtc.View` solo su singoli eventi, quelli che l'unico handler gli concede sulla riga (403 sugli altri). Dal 1
  nov 2026 (#311).
- **`09-design-m4.md` §6.2** (i grant su un evento): accanto ai registri, anche la sezione «ATC degli eventi» legge di un grant su un
  evento solo quell'evento.
