# Il limite dei verbi dei membri

**Data:** 19 ottobre 2026 — fase di test dopo M4 (nota `2026-10-18-la-fase-di-test.md`), PR del nucleo `fix/member-rate-limit`
**Stato:** **Proposta** — la domanda del §5 è per Carmine.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: il limite delle richieste esiste già nel nucleo (`AddRateLimiter` in
`Program.cs`, la policy `auth` di `/auth` e di `/api/jobs/run`); lo si estende con una seconda policy che un modulo applica per nome,
invece di lasciare a ogni modulo un contatore suo.

## 1. Che cosa serve

Un irrobustimento: oggi solo il login e l'indirizzo del task pianificato hanno un limite. I verbi con cui un membro scrive le sue cose
(le richieste e le scelte del training, le iscrizioni, i posti, le prenotazioni, le disponibilità, i cambi turno e i report degli eventi,
i report dei tour) non ne hanno, e ogni scrittura fa lavoro sul server. Un membro che compila i moduli dell'hub non arriva mai a decine di
scritture al minuto; un limite largo per ogni membro non tocca nessun uso vero, come quello di `/auth` non tocca nessun login vero.

## 2. Perché nessun meccanismo esistente basta

- La policy `auth` conta **per indirizzo**, dieci al minuto: giusta per chi non si è ancora presentato, sbagliata per i membri, che
  dietro lo stesso indirizzo (una rete, un proxy) sono tanti, e che scrivono più di dieci volte in una serata d'evento.
- Un contatore nel modulo sarebbe una copia per modulo (`CLAUDE.md` §2: niente scritto due volte), e un modulo non può registrare una
  policy: il limitatore è del host.

## 3. La forma

- **`src/IvaoHub.Core/Services/MemberWriteLimit.cs`**: `HubRateLimits.MemberWrites` (`"member-writes"`), la costante che un modulo
  scrive (`mine.RequireRateLimiting(HubRateLimits.MemberWrites)`) — nel nucleo e non in `Web`, perché un modulo vede solo il nucleo.
  - **Per membro**: la partizione è il VID del claim; chi non è entrato è contato per indirizzo.
  - **Solo le scritture**: GET, HEAD e OPTIONS passano senza contare, così un gruppo intero prende la policy con una riga e le letture
    della pagina restano libere.
  - **Finestra fissa**, nessuna coda: oltre il limite, 429 subito.
  - **I numeri in configurazione**: `RateLimits:MemberWrites:PermitLimit` e `RateLimits:MemberWrites:WindowSeconds`, **30 al minuto**
    se mancano; un valore sotto 1 ferma l'avvio con la chiave da correggere (`MemberWriteLimitOptionsValidator`). In
    `docs/DEPLOYING.md`.
- **La risposta di ogni limite**, anche quella di `/auth`: `HubRateLimits.RejectAsync`, 429 in `ProblemDetails` attraverso l'unico
  `IProblemDetailsService`, con il titolo nella lingua di chi chiede (`errors.tooManyRequests.title` in `locales/{en,it}/errors.json`,
  accanto ai titoli di 403, 404 e 409, non in `common.json`) e `Retry-After`. Nella SPA, `useProblemDetails` mappa 429 su quella chiave
  come fa con 403, 404 e 409.
- **`UseRateLimiter` scende dopo `UseAuthentication`** (prima di `UseAuthorization`): senza l'utente il limitatore non vede il VID. Per
  `/auth` e `/api/jobs/run` non cambia niente: contano per indirizzo e passano prima di ogni lavoro del gestore.
- **Dove si applica**, una riga per gruppo: `/api/training/mine` (Training), `/api/events/mine/{availability,shifts,bookings,
  registrations,seats,reports}` (Events), `/api/flightops/tours/{id}/reports` e `/api/flightops/reports` (i report dei tour, FlightOps).
  `/api/events/mine/record` è solo lettura e non la prende.
- **Le prove** (`MemberWriteLimitTests`): con il limite a tre, la quarta scrittura di un membro ha 429 con il titolo del nucleo e
  `Retry-After`, una lettura dello stesso gruppo passa, un altro membro dallo stesso indirizzo scrive; e ogni verbo di scrittura sotto
  `/mine` porta la policy, così un gruppo nuovo senza la riga si vede nei test. Mutazione: tolta la riga dal gruppo del training, il
  primo test fallisce.

## 4. Alternative

1. **Un limite globale su tutte le scritture di `/api`** — scartato: colpirebbe anche lo staff che importa o corregge in serie, e il
   limite giusto per un back office non è quello di un membro.
2. **La policy di `/auth` sui gruppi dei membri** — scartato per il §2: per indirizzo e troppo stretta.
3. **Un limite scritto dentro ogni verbo** — scartato: una copia per modulo.

**Raccomandazione: la forma del §3.**

## 5. La domanda per Carmine

Le righe nei moduli Training ed Events toccano codice di `dalberone` (`CLAUDE.md` §0). **Vanno in questa PR, o in una PR di `dalberone`
dopo questa?** Raccomandazione: **in questa PR** — è il nucleo che applica il suo meccanismo, una riga per gruppo più lo `using`, senza
logica del modulo; e il test sui verbi `/mine` resta verde solo con tutte le righe al loro posto. E **30 scritture al minuto per membro**
come valore di partenza: va bene?

## Da portare nel piano

- §16 (meccanismi generici): una riga per «il limite dei verbi dei membri» — `HubRateLimits.MemberWrites` nel nucleo, per VID, solo le
  scritture, numeri in `RateLimits:MemberWrites`, 429 in `ProblemDetails` con `errors.tooManyRequests.title`; un modulo la applica ai
  suoi gruppi di membri con una riga, mai un contatore suo.
- §9.7 (contratti trasversali): ogni gruppo di indirizzi propri del membro (`/mine`) porta la policy; il test lo controlla.
- §11.3 (configurazione dell'installazione): le due chiavi `RateLimits:MemberWrites:*`, facoltative.
- `CLAUDE.md` §2, la tabella: una riga «Un limite alle scritture di un membro → `HubRateLimits.MemberWrites`».
