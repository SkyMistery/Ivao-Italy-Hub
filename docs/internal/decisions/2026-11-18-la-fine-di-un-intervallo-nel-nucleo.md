# La fine di un intervallo nel nucleo

**Data:** 18–19 novembre 2026 (ramo `fix/interval-end-in-core`).
**Stato:** decisa da Carmine il 14 novembre 2026, in chat al master: le copie della fine di un intervallo vanno «in una PR del nucleo
dopo i lotti», accanto a `useMoment`. La decisione non ha un commento su GitHub da linkare: è nella chat del master, che l'ha
riportata nel compito di questa sessione. Qui sono della sessione di lavoro solo le scelte del §3.
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

**Una differenza, fuori dalla fine:** il trattino di un intervallo di un giorno. Gli eventi scrivono `21/11/2026 17:00 – 22:00` (spaziato),
il training `03/10/2026 16:00–18:00` (chiuso); su due giorni scrivono entrambi lo spaziato. Si vede a schermo, e ognuno dei due ha un
test che lo fissa.

## 3. Che cosa entra nel nucleo, e che cosa no

- **`intervalEnd(startsAt, endsAt, moment, timeZone?)`** in `web/src/shared/i18n/useMoment.ts`, con **`onSameDay`**, il confronto dei
  giorni su cui poggia (serve anche al trattino del training e a `oneDay` della testa di un evento), e il tipo **`MomentFormat`** del
  formatter passato da fuori, come le copie lo passavano. Test in `web/src/shared/i18n/intervalEnd.test.ts`.
- **`sessionEnd` e `endOfHours` se ne vanno**: erano `intervalEnd` con un altro nome. Chi le chiamava (`report.tsx`, `ActivityTurns.tsx`,
  `PrivateSlots.tsx`) chiama il nucleo; i loro casi (#333, #334) stanno nel test del nucleo con gli stessi ingressi e le stesse attese,
  e `hours.test.ts` con loro.
- **Le due `spanText` restano nei moduli**, perché scrivono un intervallo intero e non solo la fine, e prendono la fine da `intervalEnd`.
  **Il trattino non si unifica qui**: cambierebbe quello che legge uno dei due moduli e il test di un modulo che non è di chi scrive.
  Raccomandazione per Carmine, se vuole un solo trattino: lo spaziato (` – `), perché la parte prima contiene spazi
  («21/11/2026 17:00»); allora anche `spanText` passa nel nucleo e il test del training cambia il suo valore con il suo sì.
- `headMoment` degli eventi confronta **lo stesso istante in due fusi**, non due istanti: non è questa domanda, resta com'è.

**Che cosa vede l'utente:** niente di diverso. Ogni test dei moduli che resta (`cards.test.ts`, `dates.test.ts`, `reporting.test.ts`) è
invariato e verde; una mutazione di `intervalEnd` che dimentica il fuso fa cadere il test del nucleo e quelli dei due moduli.

## 4. Alternative scartate

- **Lasciare `sessionEnd` ed `endOfHours` come involucri del nucleo**, per non toccare i loro test: due nomi per una cosa sola, quello
  che la decisione toglie.
- **Un `intervalText` del nucleo con il trattino come parametro**: un'opzione che esiste solo per tenere in vita la differenza.

## Da portare nel piano

- §16 punto 6 (un solo modo di scrivere un istante): accanto a `formatMoment`/`useMoment`, `intervalEnd` (e `onSameDay`) è l'unico
  posto che scrive la fine di un intervallo dopo il suo inizio; nessun modulo ne tiene una copia.
- Changelog: le quattro copie diventano una (`sessionEnd`, `endOfHours` tolte; le due `spanText` sopra `intervalEnd`); la domanda del
  trattino resta a Carmine (§3).
