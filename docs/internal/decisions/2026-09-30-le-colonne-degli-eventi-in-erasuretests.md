# Le colonne degli eventi in `ErasureTests`

**Data:** 30 settembre 2026 — fase E2 di M4 (lo scheletro), PR #209
**Stato:** **nessuna decisione nuova**: questa nota dice dove si applica una decisione già presa — la risposta 2 di Carmine sulla #187
(nota `2026-09-29-la-persona-cancellata-nel-nucleo`, la (c): il test legge i contesti di ogni modulo, e la fase che crea una colonna di
persona scrive la sua riga) e la regola di tutte le fasi di `10-piano-implementazione-m4.md` («Un test condiviso si tocca solo nei due
casi in cui il test lo chiede»).
**Regola applicata:** `CLAUDE.md` §0 regola 3 (nessun test che non si è scritto si cambia per farlo passare) e regola 6 (una PR che
tocca un file del nucleo porta una nota nuova, e `core-guard` la chiede), come `2026-09-27-i-conteggi-dei-blocchi-del-training`.

## 1. Che cosa

La fase E2 crea `evt_events` intera e `evt_event_airports` (migrazione `Initial` di `EventsDbContext`). Le colonne che la regola di
`PersonColumns.IsVid` riconosce come il VID di una persona sono cinque:

- `evt_event_airports.created_by`, `evt_event_airports.updated_by` (l'audit del nucleo: la riga è `IAuditable`);
- `evt_events.cancelled_by` (chi annulla un evento, design M4 §2.3), `evt_events.created_by`, `evt_events.updated_by`.

`ErasureTests.TheColumnsThatNameAPersonAreTheOnesTheErasureKnows` legge da solo il contesto degli eventi, e senza le cinque righe va
rosso: **misurato** il 30 settembre, sulla suite intera, «Collections differ at index 22 — expected `fo_aircraft_groups.created_by`,
actual `evt_event_airports.created_by`» (`ErasureTests.cs:230`). Le cinque righe entrano nella lista `PersonColumnsOfTheHub`, in ordine,
fra `cms_menu_items.updated_by` e `fo_aircraft_groups.created_by`. Nient'altro del test cambia.

## 2. Perché non è far passare un test

Il test esiste per far pensare alla cancellazione di ogni colonna nuova di una persona, ed è quello che dice: a tutte e cinque la
cancellazione del nucleo scrive lo pseudonimo, perché i nomi seguono la convenzione (`…By`), senza niente da scrivere nel modulo. Che
cosa gli eventi cancellano delle **righe dei membri** (le prenotazioni, le disponibilità, i PIREP) è di `EventsPersonalData`, in E8b e
nelle fasi dopo, come dice il design §11.1: nessuna di quelle righe esiste ancora.

## 3. Perché una nota

`core-guard` classifica il file come nucleo — è un test condiviso che c'era già sotto `tests/` e non porta il nome degli eventi —, e
una PR che tocca il nucleo porta una nota nuova in `decisions/`. È questa, breve come quella dei conteggi dei blocchi.

## Da portare nel piano

Niente: la regola è già in `10-piano-implementazione-m4.md` («Regole di tutte le fasi») e nel piano 1.25 (E8a tolta). Dopo E2 la lista
ha le cinque righe `evt_`; ogni fase degli eventi che aggiunge una colonna `…Vid` o `…By` scrive la sua.
