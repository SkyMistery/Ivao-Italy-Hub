# Le colonne delle prenotazioni in `ErasureTests` (E6a)

**Data:** 7 ottobre 2026 — fase E6a di M4 (prenotare: il server)
**Stato:** **nessuna decisione nuova**: questa nota dice dove si applica una decisione già presa — le colonne di persona in
`ErasureTests` (la risposta 2 di Carmine sulla #187, nota `2026-09-29-la-persona-cancellata-nel-nucleo`, come per lo scheletro in
`2026-09-30-le-colonne-degli-eventi-in-erasuretests`, per le rotte in `2026-10-06-i-conteggi-e-le-colonne-di-e4` e per gli slot in
`2026-10-06-le-colonne-degli-slot-in-erasuretests`) —, e la regola di tutte le fasi di `10-piano-implementazione-m4.md` («Un test
condiviso si tocca solo nei due casi in cui il test lo chiede»).
**Regola applicata:** `CLAUDE.md` §0 regola 3 (nessun test che non si è scritto si cambia per farlo passare) e regola 6 (una PR che
tocca un file del nucleo porta una nota nuova, e `core-guard` la chiede).

## 1. Che cosa

La fase E6a aggiunge **una tabella**, `evt_bookings` (design M4 §1.6), nella migrazione additiva `AddEventBookings` del contesto degli
eventi. La prenotazione è una riga di un membro, non dello staff: non è `IAuditable` (la sua storia è l'audit del nucleo, `[Audited]`), e
ha **due** colonne che nominano una persona, con i nomi della convenzione: `booker_vid`, il pilota, e `unflown_excused_by`, chi dello staff
toglierà una prenotazione non volata dal registro del pilota (E13b). `ErasureTests` (`TheColumnsThatNameAPersonAreTheOnesTheErasureKnows`)
legge da solo i contesti di ogni modulo e le confronta con la lista `PersonColumnsOfTheHub`: senza le due righe va rosso.

Le due righe `evt_bookings.booker_vid` ed `evt_bookings.unflown_excused_by`, in ordine, prima di `evt_event_airports.created_by`.
Nient'altro: nessuna asserzione, nessun altro test.

## 2. Perché non è far passare un test

È il caso per cui `ErasureTests` esiste: far pensare alla cancellazione di ogni colonna nuova di una persona. Alle due colonne la
cancellazione del nucleo scrive lo pseudonimo, perché i nomi seguono la convenzione (`…Vid`, `…By`). Ma una prenotazione è **su** una
persona, il pilota: il nucleo non la cancella, la lascia con lo pseudonimo. Che cosa farne — cancellare le prenotazioni degli eventi non
conclusi, perché lo slot torni libero, e tenere con lo pseudonimo quelle degli eventi conclusi, senza i dati del volo privato — è di
`EventsPersonalData` (design M4 §11.1, nota `2026-09-29-i-dati-dei-membri-negli-eventi` §2.5), nella fase **E8b**. Fino ad allora una
persona cancellata resta su uno slot con lo pseudonimo: scritto nell'HANDOFF.

## 3. Perché una nota

`core-guard` classifica il file come nucleo — è un test che c'era già sotto `tests/` e non porta il nome degli eventi —, e una PR che
tocca il nucleo porta una nota nuova in `decisions/`. È questa, breve come quelle che la precedono.

## Da portare nel piano

Niente: le regole sono già in `10-piano-implementazione-m4.md` («Regole di tutte le fasi») e nel piano. Dopo E6a la lista di
`ErasureTests` ha undici righe `evt_`.
