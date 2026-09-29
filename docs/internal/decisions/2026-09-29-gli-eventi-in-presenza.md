# Gli eventi in presenza: le domande, l'iscrizione, le attività a posti

**Data:** 29 settembre 2026 — fase E0 di M4
**Stato:** **decisa** (Carmine, 29 settembre 2026, sulla PR #180, [conferma di §17.1 e §17.2][ok]; la richiesta nuova di §17.2 n.6,
con le scelte di forma del design).
**Regola applicata:** `CLAUDE.md` §5, caso **(c)**: una funzione nuova **del modulo**, decisa nel design; nessun meccanismo del nucleo
la copre, e non ne serve uno nuovo — lo staff scrive le domande come righe, e il form del membro è il `SchemaForm` di sempre con lo
schema costruito dalle righe. Design `09-design-m4.md` §0.1 (M4c), §4-bis, §11, §17.2 n.6.

[ok]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/180#issuecomment-5880522987

## 1. Che cosa serviva decidere

Rispondendo alle domande della seconda stesura (c4), Carmine ha aggiunto una richiesta: **non tutti gli eventi hanno un roster**, e
un evento **in presenza** può chiedere a chi viene **che materiale porta** (PC, monitor…), **se partecipa alle cene** organizzate, e
fargli **prenotare le attività parallele** a posti limitati, come un turno a un simulatore di volo.

## 2. Le decisioni

1. **È il terzo blocco, M4c**, e **non dipende da M4b**: può venire prima, se il primo evento in presenza arriva prima. Un evento è
   in presenza con l'interruttore `in_person` e un luogo (`venue`, tradotto); gli interruttori sono indipendenti, quindi un evento
   in presenza può avere anche slot o roster.
2. **Le domande** (`evt_questions`, area `Events`): etichetta e scelte tradotte, obbligatoria o no, ordine, e un **tipo da un elenco
   chiuso** — `YesNo`, `Choice`, `MultiChoice`, `Quantity`, `Text`. «Che materiale porti» è una `MultiChoice` o una `Quantity` per
   voce; «vieni alla cena del sabato» è un `YesNo`.
3. **L'iscrizione** (`evt_registrations`, area `EventBookings`): una per membro per evento, con le risposte validate **dal server**
   secondo il tipo; `ISubmittedByMembers`, stakeholder il membro. Il membro si iscrive, cambia le risposte o si ritira **fino
   all'inizio dell'evento**; tutto in `/events/mine`; mail `registrationReceived` con il riepilogo.
4. **Le attività parallele** (`evt_activities`, area `EventBookings`): nome tradotto, luogo, finestra, **durata del turno** e **posti
   per turno** (due simulatori = 2 posti ogni 30 minuti). **Il posto** (`evt_activity_bookings`) lo prende solo chi è iscritto; i
   posti si contano con un blocco sulla riga dell'attività (`SELECT … FOR UPDATE`), perché un turno ha più posti e un indice univoco
   non basta; un membro non prende due turni sovrapposti.
5. **Lo staff** (`Events.Edit` per domande e attività, `EventBookings.Edit` per le iscrizioni) vede le iscrizioni e **le somme**
   (quanti a cena, quanti monitor), e toglie un'iscrizione.
6. **I dati si tengono poco**: le risposte parlano di una persona e di un posto fisico, quindi iscrizioni e posti si cancellano
   **`inPersonRetentionMonths` (3) dopo l'evento**; le somme restano nelle statistiche. Li vedono solo lo staff con
   `EventBookings.View` e il membro.

## 3. Alternative scartate

| Alternativa | Perché no |
|---|---|
| Un form libero per evento, scritto a mano | lista e form generati; un form a mano non è accettato (`CLAUDE.md` §2) |
| Domande con un tipo libero o un'espressione | un elenco chiuso si valida sul server tipo per tipo |
| Un indice univoco per il posto di un'attività | un turno ha più posti: serve il conteggio sotto un blocco |
| Tenere le risposte come le prenotazioni dei piloti (24 mesi) | dicono chi viene di persona e dove; bastano le somme |
| Un meccanismo del nucleo per i «moduli con domande» | c'è un modulo solo che lo chiede; il form generato basta |

## 4. Che cosa si tocca, e dove

**E16** le domande, l'iscrizione, il form costruito dalle domande, le somme, `/events/mine`, la mail; **E17** le attività, i turni e
i posti con il blocco, la conservazione breve, «Duplica» per domande e attività, il giro completo di M4c.

## Da portare nel piano

**Già nel piano 1.24**: §7 (le tabelle `evt_` di M4c), §9.2 riga Events (M4c, gli eventi in presenza con l'iscrizione a domande e le
attività parallele a posti), §13 riga M4. Resta:

- **§9.7, «Privacy dei membri»**: le iscrizioni a un evento in presenza e i posti delle attività si cancellano 3 mesi dopo l'evento
  (un'impostazione); restano le somme.
