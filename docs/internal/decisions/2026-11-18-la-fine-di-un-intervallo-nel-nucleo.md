# La fine di un intervallo nel nucleo

**Data:** 18–19 novembre 2026 (ramo `fix/interval-end-in-core`, PR #542).
**Stato:** decisa da Carmine, due volte.
- **14 novembre 2026**, in chat al master: le copie della fine di un intervallo vanno «in una PR del nucleo dopo i lotti», accanto a
  `useMoment`. Questa decisione non ha un commento su GitHub da linkare: è nella chat del master, che l'ha riportata nel compito di questa
  sessione.
- **19 novembre 2026**, sulla domanda del §3: «Spaziato ovunque» ([la risposta di Carmine sulla
  #542](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/542#issuecomment-7011403735)).

Sono della sessione di lavoro solo le scelte del §3 che non nominano il trattino.
**Classificazione** (`CLAUDE.md` §5): **(b)**, un meccanismo che c'è (`useMoment`/`formatMoment`, §16 punto 6) esteso con un pezzo
che quattro moduli scrivevano per conto loro.

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

**Fuori da questa domanda**, perché non sono righe di `intervalText`: gli intervalli fatti **di due ore nude**, senza data davanti — la
testa di un evento (`21/11/2026 · 17:00–22:00`, `headSpan`/`compactSpan`, nota `2026-10-11-la-testa-della-pagina-di-un-evento`) e le
ore degli slot privati (`18:00–19:00`) — tengono il trattino chiuso, che fra due parole senza spazi è la forma giusta. `headMoment`
confronta **lo stesso istante in due fusi**, non due istanti, e resta com'è.

**Che cosa vede l'utente:** negli eventi e nei tour niente di diverso; nel Training il trattino spaziato sugli intervalli di un giorno.

## 4. Alternative scartate

- **Lasciare `sessionEnd` ed `endOfHours` come involucri del nucleo**, per non toccare i loro test: due nomi per una cosa sola, quello
  che la decisione toglie.
- **Un `intervalText` con il trattino come parametro**: un'opzione che esiste solo per tenere in vita la differenza, che Carmine ha tolto.

## Da portare nel piano

- §16 punto 6 (un solo modo di scrivere un istante): accanto a `formatMoment`/`useMoment`, `intervalEnd` e `intervalText` (con
  `onSameDay`) sono l'unico posto che scrive un intervallo e la sua fine; nessun modulo ne tiene una copia. Il trattino fra inizio e fine
  è spaziato ovunque (Carmine, 19 nov 2026, link sopra).
- Changelog: le quattro copie diventano una (`sessionEnd`, `endOfHours` e le due `spanText` tolte); nel Training gli intervalli di un
  giorno prendono il trattino spaziato.
