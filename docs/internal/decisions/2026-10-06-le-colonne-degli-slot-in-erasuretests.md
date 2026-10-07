# Le colonne degli slot in `ErasureTests` (E5)

**Data:** 6 ottobre 2026 — fase E5 di M4 (gli slot pubblici e l'esportazione)
**Stato:** **nessuna decisione nuova**: questa nota dice dove si applica una decisione già presa — le colonne di persona in
`ErasureTests` (la risposta 2 di Carmine sulla #187, nota `2026-09-29-la-persona-cancellata-nel-nucleo`, come per lo scheletro in
`2026-09-30-le-colonne-degli-eventi-in-erasuretests` e per le rotte in `2026-10-06-i-conteggi-e-le-colonne-di-e4`) —, e la regola di
tutte le fasi di `10-piano-implementazione-m4.md` («Un test condiviso si tocca solo nei due casi in cui il test lo chiede»).
**Regola applicata:** `CLAUDE.md` §0 regola 3 (nessun test che non si è scritto si cambia per farlo passare) e regola 6 (una PR che
tocca un file del nucleo porta una nota nuova, e `core-guard` la chiede).

## 1. Che cosa

La fase E5 aggiunge **una tabella**, `evt_slots` (design M4 §1.5), nella migrazione additiva `AddEventSlots` del contesto degli eventi.
Lo slot è una riga dello staff dell'evento, `IAuditable` come gli scali e le rotte, quindi porta le due colonne dell'audit del nucleo,
`created_by` e `updated_by`. `ErasureTests` (`TheColumnsThatNameAPersonAreTheOnesTheErasureKnows`) legge da solo i contesti di ogni
modulo e le confronta con la lista `PersonColumnsOfTheHub`: senza le due righe va rosso.

Le due righe `evt_slots.created_by` ed `evt_slots.updated_by`, in ordine, fra `evt_routes.updated_by` e
`fo_aircraft_groups.created_by`. Nient'altro: nessuna asserzione, nessun altro test.

## 2. Perché non è far passare un test

È il caso per cui `ErasureTests` esiste: far pensare alla cancellazione di ogni colonna nuova di una persona. Alle due colonne la
cancellazione del nucleo scrive lo pseudonimo, perché i nomi seguono la convenzione (`…By`), senza niente da scrivere nel modulo: uno
slot è una riga dello staff, non è **su** nessuno. Chi prenota uno slot non sta sulla sua riga: la prenotazione è una riga sua
(`evt_bookings`, E6a, con `booker_vid`), e la sua riga di `ErasureTests` la scrive E6a, con il resto della cancellazione in E8b.

## 3. Perché una nota

`core-guard` classifica il file come nucleo — è un test che c'era già sotto `tests/` e non porta il nome degli eventi —, e una PR che
tocca il nucleo porta una nota nuova in `decisions/`. È questa, breve come quelle che la precedono.

## Da portare nel piano

Niente: le regole sono già in `10-piano-implementazione-m4.md` («Regole di tutte le fasi») e nel piano. Dopo E5 la lista di
`ErasureTests` ha nove righe `evt_`.
