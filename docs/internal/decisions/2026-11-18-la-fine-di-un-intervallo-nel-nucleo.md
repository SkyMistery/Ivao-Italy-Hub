# La fine di un intervallo nel nucleo

**Data:** 18–21 novembre 2026 (ramo `fix/interval-end-in-core`, PR #542).
**Stato:** decisa da Carmine, due volte.
- **14 novembre 2026**, in chat al master: le copie della fine di un intervallo vanno «in una PR del nucleo dopo i lotti», accanto a
  `useMoment`. Questa decisione non ha un commento su GitHub da linkare: è nella chat del master, che l'ha riportata nel compito di questa
  sessione.
- **19 novembre 2026**, sulla domanda del §3: «Spaziato ovunque» ([la risposta di Carmine sulla
  #542](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/542#issuecomment-7011403735)).

Sono della sessione di lavoro solo le scelte del §3 che non nominano il trattino.
**Classificazione** (`CLAUDE.md` §5): **(b)**, un meccanismo che c'è (`useMoment`/`formatMoment`, §16 punto 6) esteso con un pezzo
che tre moduli — i tour, gli eventi, il training — scrivevano per conto loro, in quattro copie.

## 1. Che cosa serve

Una riga che dice un intervallo — un volo sulla rete, le ore di un turno, un evento, una data di un training — scrive l'inizio per intero
e la fine **dopo l'inizio**: l'ora sola se finisce lo stesso giorno, la data con l'ora se finisce un altro giorno, così un'ora che finisce
alle 02:45 del giorno dopo non si legge mai come «02:45» e basta (#333 punto 6, #334 punto 4). Il giorno si legge nel fuso chiesto, UTC
quando non ce n'è: un intervallo di un solo giorno in UTC può durarne due dove vive la divisione, e viceversa.

## 2. Le quattro copie, confrontate

| Copia | Che cosa scrive | Fuso |
|---|---|---|
| `sessionEnd`, `flightops/screens/reporting.ts` | la fine sola | UTC |
| `endOfHours`, `events/screens/hours.ts` | la fine sola | UTC |
| `spanText`, `events/screens/cards.ts` | `inizio – fine` | quello chiesto, UTC se nessuno |
| `spanText`, `training/screens/dates.ts` | `inizio–fine` lo stesso giorno, `inizio – fine` su due; senza fine, l'inizio | quello chiesto, UTC se nessuno |

**La fine è la stessa in tutte e quattro**: stesso confronto dei giorni (`time: false` nel fuso), ora sola (`date: false`) se uguali,
istante intero altrimenti. Le prime due sono identiche riga per riga; le due `spanText` la scrivono con il fuso.

**Una differenza, fuori dalla fine:** il trattino di un intervallo di un giorno. Gli eventi scrivevano `21/11/2026 17:00 – 22:00`
(spaziato), il training `03/10/2026 16:00–18:00` (chiuso); su due giorni scrivevano entrambi lo spaziato.

## 3. Che cosa entra nel nucleo

In `web/src/shared/i18n/useMoment.ts`, con i test in `web/src/shared/i18n/interval.test.ts`:

- **`intervalEnd(startsAt, endsAt, moment, timeZone?)`**, la fine;
- **`intervalText(startsAt, endsAt | null, moment, timeZone?)`**, la riga intera: l'inizio, **il trattino spaziato**, la fine di
  `intervalEnd`; senza fine, l'inizio;
- **`onSameDay`**, il confronto dei giorni su cui poggiano (serve anche a `oneDay` della testa di un evento);
- il tipo **`MomentFormat`** del formatter passato da fuori, come le copie lo passavano.

**Le copie se ne vanno tutte e quattro.** `sessionEnd` ed `endOfHours` erano `intervalEnd` con un altro nome: chi le chiamava
(`report.tsx`, `ActivityTurns.tsx`, `PrivateSlots.tsx`) chiama il nucleo, e i loro casi (#333, #334) stanno nel test del nucleo con gli
stessi ingressi e le stesse attese, `hours.test.ts` con loro. Le due `spanText` diventano `intervalText`: chi le chiamava
(`EventCards.tsx`, `compactSpan` di `cards.ts`, `parts.tsx`, `staff.tsx`, `traineeTraining.tsx`) chiama il nucleo, e i loro test restano
dove sono (`cards.test.ts`, `dates.test.ts`) chiamando `intervalText`.

**Il trattino.** La sessione aveva lasciato i due trattini com'erano e chiesto a Carmine. La risposta è «Spaziato ovunque»: un
intervallo di un giorno si legge `17:00 – 22:00` in ogni modulo. **Nel Training cambia quello che si vede**: `03/10/2026 16:00–18:00`
diventa `03/10/2026 16:00 – 18:00`, nelle righe delle date (`parts.tsx`, `staff.tsx`, `traineeTraining.tsx`). Per questo cambiano tre
attese di `dates.test.ts`, per decisione di Carmine e non per far passare un test. Il motivo dello spaziato: la parte prima del trattino
ha spazi suoi («03/10/2026 16:00»), e un trattino chiuso la legherebbe solo all'ora.

**Le righe che scrivevano un intervallo a mano**, senza una funzione che lo chiamasse così, passano anch'esse dal nucleo
(la revisione del master sulla #542):

- la sessione scelta nel report di un tour (`report.tsx`, `flightops:report.sessionTime`): scriveva l'inizio e `intervalEnd` a mano,
  cioè `intervalText`; la stringa diventa `{{when}} UTC`, e a schermo non cambia niente;
- le finestre date da un controllore (`Availability.tsx`) e le posizioni fra cui scegliere in un turno (`roster.tsx`): scrivevano i due
  estremi per intero, `21/11/2026 17:00 – 21/11/2026 19:00`; adesso `21/11/2026 17:00 – 19:00`, la data della fine solo se è un
  altro giorno.

**Restano fuori, e lo dicono qui**, due righe che scrivono un intervallo in un'altra forma, voluta:

- la riga di un turno in un report di supporto (`reportWords.ts`, `events:reports.shift`, `{{from}} → {{to}}`): la freccia, con i due
  estremi interi, perché la lista dello staff attacca a quella riga il suo controllo con un trattino
  (`… — Controllato: 120 minuti sulla postazione`), e un secondo trattino nella stessa riga la renderebbe ambigua;
- le ore degli slot privati (`PrivateSlots.tsx`, `events:private.range`): quando gli slot stanno in un giorno solo la data è detta una
  volta sopra le tabelle e l'inizio è un'ora nuda, che `intervalText` (l'inizio sempre intero) non scrive; la fine passa già da
  `intervalEnd`.

**La mail del Training** scrive la stessa data con `TrainingMail.Span` (`src/IvaoHub.Modules.Training/TrainingMail.cs`), sul server, che
non può chiamare il nucleo della SPA: prende anch'essa il trattino spaziato (`03/10/2026 16:00 – 18:00`), con un test suo in
`TrainingDatesRulesTests`, che prima non ne aveva. Così la pagina e la mail della stessa data dicono la stessa cosa.

**Fuori da questa domanda**, perché non sono righe di `intervalText`: gli intervalli fatti **di due ore nude**, senza data davanti — la
testa di un evento (`21/11/2026 · 17:00–22:00`, `headSpan`/`compactSpan`, nota `2026-10-11-la-testa-della-pagina-di-un-evento`) e le
ore degli slot privati (`18:00–19:00`) — tengono il trattino chiuso, che fra due parole senza spazi è la forma giusta. `headMoment`
confronta **lo stesso istante in due fusi**, non due istanti, e resta com'è.

**Dopo questa nota**, la nota `2026-11-19-il-trattino-delle-ore-senza-data` (#547, in coda dopo questa PR) rovescia il paragrafo qui
sopra: Carmine ha deciso «Sì, PR a parte», e anche le due ore nude prendono il trattino spaziato. Vale quella nota.

**Che cosa vede l'utente:** nei tour niente di diverso; negli eventi le finestre di un controllore e le posizioni di un turno scrivono la
fine di un giorno come ora sola; nel Training, sulle pagine e nella mail, il trattino spaziato sugli intervalli di un giorno.

## 4. Alternative scartate

- **Lasciare `sessionEnd` ed `endOfHours` come involucri del nucleo**, per non toccare i loro test: due nomi per una cosa sola, quello
  che la decisione toglie.
- **Un `intervalText` con il trattino come parametro**: un'opzione che esiste solo per tenere in vita la differenza, che Carmine ha tolto.

## Da portare nel piano

- §16 punto 6 (un solo modo di scrivere un istante): accanto a `formatMoment`/`useMoment`, `intervalEnd` e `intervalText` (con
  `onSameDay`) sono l'unico posto della SPA che scrive un intervallo con l'inizio intero e la sua fine; nessun modulo ne tiene una
  copia. Due righe scrivono un intervallo in un'altra forma, e restano loro: il turno di un report di supporto (`reportWords.ts`, la
  freccia) e le ore degli slot privati di un giorno solo (`PrivateSlots.tsx`, l'inizio senza data). Sul server la mail del Training
  (`TrainingMail.Span`) scrive le date allo stesso modo. Il trattino fra inizio e fine è spaziato ovunque (Carmine, 19 nov 2026, link
  sopra).
- Changelog: le quattro copie diventano una (`sessionEnd`, `endOfHours` e le due `spanText` tolte); il report di un tour, le
  finestre e le posizioni dei turni degli eventi passano da `intervalText`; nel Training gli intervalli di un giorno prendono il
  trattino spaziato, sulle pagine e nella mail.
