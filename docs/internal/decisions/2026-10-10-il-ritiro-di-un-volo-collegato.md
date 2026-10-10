# Il ritiro di uno dei due voli collegati: il legame si scioglie, l'altro resta (E7)

**Data:** 10 ottobre 2026 — fase E7 di M4 (gli slot privati), all'apertura
**Stato:** **Proposta** — la domanda è a Carmine con [l'issue #245][q]. E7 scrive la raccomandazione (§3); una risposta diversa è una
correzione sul branch della fase, prima del merge.
**Regola applicata:** `CLAUDE.md` §5, casi **(a)** e **(b)**: nessun meccanismo nuovo — il ritiro del pilota che cancella la riga
(`[WithdrawnByStakeholder]`, E10h), «togli» dello staff (E6a), `paired_booking_id` (E6a, design §1.6) —; è un comportamento del modulo che
il design non dice, e il piano chiede di domandarlo all'apertura di E7 (`10-piano-implementazione-m4.md`, E7, punto 2).

[q]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/245

## 1. Che cosa il design non dice

- **La partenza collegata** (design M4 §3.4; nota `2026-09-29-gli-slot-e-le-prenotazioni` §2.4): un arrivo su uno slot privato può
  chiedere anche una partenza privata dallo stesso scalo; le due prenotazioni **nascono insieme**, collegate da `paired_booking_id`
  (§1.6), perché il Gate Manager dia loro lo stesso gate. La colonna sta sull'arrivo e nomina la partenza (E6a, `EventBooking`).
- **Il ritiro** (§3.6): il pilota ritira una prenotazione fino al suo off block, e la riga si cancella; lo staff ne toglie una con un
  motivo, in qualunque momento (E6a). Ogni prenotazione è una riga a sé, con il suo slot, la sua compatibilità e il suo off block.
- **Non è detto che cosa succede all'altra** quando una delle due se ne va. E le due chiudono in momenti diversi: l'off block
  dell'arrivo — l'orario in cui il pilota parte dall'altro aeroporto — viene prima di quello della partenza, che lascia lo scalo almeno
  `bookingGapMinutes` dopo l'on block dell'arrivo. Per un po' la partenza si ritira ancora e l'arrivo no.

## 2. Le strade

| Strada | Che cosa succede | Perché sì, perché no |
|---|---|---|
| **A. Il legame si scioglie, l'altra resta** (raccomandata) | si cancella solo la prenotazione ritirata o tolta; l'altra resta, una prenotazione privata qualunque, senza legame; nell'esportazione il suo `paired_slot_id` torna vuoto | ogni prenotazione è già valida da sola (il suo slot, la sua compatibilità, il suo off block); vale lo stesso in ogni momento, anche dopo l'off block dell'arrivo; il pilota non perde una prenotazione che non ha ritirato; lo staff che toglie un volo non toglie l'altro |
| B. Se ne va una, se ne vanno tutte e due | il ritiro e «togli» cancellano anche l'altra | dopo l'off block dell'arrivo il ritiro della partenza dovrebbe cancellare un arrivo già chiuso, oppure essere rifiutato; «togli» di un volo toglierebbe al pilota anche quello che lo staff non ha nominato |
| C. Una delle due non si ritira da sola | il pilota ritira la coppia intera, o niente | un verbo in più, e la stessa difficoltà di B dopo l'off block dell'arrivo |

Rifare il legame più tardi non c'è in nessuna strada: una coppia nasce insieme (§3.4). Chi la vuole di nuovo ritira quella rimasta e
prenota di nuovo arrivo e partenza.

## 3. Che cosa fa E7, con la strada A

- **Il ritiro del pilota** (`PilotBookings.WithdrawAsync`) e **«togli» dello staff** (`POST /api/events/bookings/{id}/remove`) cancellano
  la prenotazione chiesta, come oggi. Se è la partenza di una coppia, lo stesso salvataggio svuota `paired_booking_id` dell'arrivo; se è
  l'arrivo, la partenza non ha niente da cambiare (la colonna sta sull'arrivo).
- La mail di «togli» (`bookingRemoved`) parla del volo tolto e di nessun altro.
- L'esportazione, `/events/mine` e la scheda «Prenotazioni» leggono il legame da `paired_booking_id`: sciolto, non lo mostrano più.
- Nessuna migrazione, niente nel nucleo.

## 4. La domanda a Carmine

Posta in inglese con [l'issue #245][q]; in italiano dice:

> Un arrivo privato e la sua partenza collegata nascono insieme (design M4 §3.4). Quando una delle due se ne va — il pilota la ritira, o
> lo staff la toglie —, che cosa succede all'altra? Raccomandato: **il legame si scioglie e l'altra resta**, una prenotazione qualunque
> (strada A). Oppure se ne vanno tutte e due (B), o una delle due non si ritira da sola (C)?

## Da portare nel piano

- Design M4 §3.4 e §3.6: il ritiro, o «togli», di uno dei due voli collegati — la risposta di Carmine.
