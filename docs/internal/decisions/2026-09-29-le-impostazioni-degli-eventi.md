# Le impostazioni degli eventi e i loro predefiniti

**Data:** 29 settembre 2026 — fase E0 di M4
**Stato:** **decisa** (Carmine, 29 settembre 2026, sulla PR #180: [conferma di §17.1 e §17.2][ok] e [conferma di §17.3][ok3];
§17.2 n.6 per i predefiniti, §17.3 n.1 e n.2 per le impostazioni che aggiungono).
**Regola applicata:** `CLAUDE.md` §3 (i predefiniti non conoscono la divisione: test «XX») e §5, caso **(a)**: configurazione, nelle
impostazioni dei moduli del nucleo (`ModuleSettingsDescriptor`, piano 0.83, nota `2026-09-16-impostazioni-dei-moduli`), con la schermata generata. Design `09-design-m4.md`
§1.12, §17.2 n.6, §17.3 n.1 e n.2.

[ok]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/180#issuecomment-5880522987
[ok3]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/180#issuecomment-5880732900

## 1. Che cosa serviva decidere

I requisiti dell'ED dicono «x minuti», «x+y giorni», «x turni di pausa dopo y» (c1, c2, c3): numeri che l'ED configura. Il design ha
proposto i predefiniti; Carmine ne ha deciso tre e ha confermato gli altri.

## 2. La decisione

**`EventsSettings`**, dietro **`Events.ManageSettings`** (solo EC ed EAC, nota `2026-09-29-chi-lavora-sugli-eventi`), schermata
generata. **I predefiniti non conoscono la divisione**: i valori di IT (AS3, i tipi) li scrive la divisione, dalla schermata.

| Impostazione | Predefinito | Da dove |
|---|---|---|
| `kindPresets` — per tipo di evento, gli interruttori `public_slots`, `private_slots`, `has_roster`, `whole_division`, `in_person` | vuoto; IT: `rfe`, `rfo`, `mse`, `onlineDay` | c1, c2; §17.2 n.1 |
| `bookingGapMinutes` — distanza minima fra due prenotazioni dello stesso pilota | **10** | §17.2 n.6, **deciso da Carmine** |
| `shiftMinutes` — durata del turno se l'evento non la dice | 60 | c1 |
| `minimumAtcRating` — il rating minimo per candidarsi, dal vocabolario | nessuno; IT: AS3 | c1 |
| `maxConsecutiveShifts`, `breakShifts` | 2, 1 | §17.2 n.6, come raccomandato |
| `applicationsCloseDays`, `rosterPublishDays` | **3**, **2** | §17.2 n.6, **deciso da Carmine**: lo staff ha un giorno per correggere |
| `experienceMonths` — quanto indietro guardare le postazioni aperte | 12 | c1 |
| `noShowWeight` — peso della penalità | 1 | c3 |
| `attendanceMinimumMinutes` — minuti in un turno per dire «c'era» | 30 | §17.2 n.6, come raccomandato |
| `reportDays` — giorni dopo l'evento per mandare un PIREP di supporto | 14 | §17.2 n.6, come raccomandato |
| `pilotRetentionMonths` — prenotazioni e PIREP dei piloti dopo l'evento | 24 | c3 |
| `inPersonRetentionMonths` — iscrizioni e attività dopo l'evento | 3 | §17.2 n.6 (la forma di M4c) |
| `reminderLeadHours` — anticipo del promemoria al pilota | 24 | §17.3 n.2 |
| `unflownThreshold` — prenotazioni non volate da cui valgono i limiti | 3 | §17.3 n.1 |
| `restrictedWindowHours`, `restrictedMaxPerWindow` | 2 ore, 1 | §17.3 n.1 |
| `restrictedMaxPerEvent` | 2 | §17.3 n.1 |

- **L'evento può cambiare per sé** la durata del turno (`shift_minutes`) e i tre limiti di chi non vola (§17.3 n.1); il resto vale
  per tutta la divisione.
- **Ogni impostazione nasce nella fase che la usa**, con il suo predefinito e la sua regola: le impostazioni si leggono campo per
  campo sopra i predefiniti (`ModuleSettingsStore.ReadAsync`), quindi un campo nuovo prende il suo predefinito anche su un database
  già avviato. Lo scheletro (**E2**) ha quelle di M4a.

## 3. Alternative scartate

| Alternativa | Perché no |
|---|---|
| I predefiniti di IT nel codice (AS3, `rfe`…) | il fork «XX» partirebbe con un rating e dei tipi di IT |
| 30 minuti fra due prenotazioni | Carmine: 10 |
| Candidature chiuse 2 giorni prima e roster lo stesso giorno | lo staff non avrebbe un giorno per correggere |

## 4. Che cosa si tocca, e dove

**E2** `EventsSettings` con `kindPresets`, `bookingGapMinutes`, `pilotRetentionMonths`, `reminderLeadHours`; **E11a** quelle
dell'ATC (`shiftMinutes`, `minimumAtcRating`, `maxConsecutiveShifts`, `breakShifts`, `applicationsCloseDays`,
`rosterPublishDays`, `experienceMonths`); **E13a** `attendanceMinimumMinutes`; **E13b** `noShowWeight` e i quattro dei limiti;
**E14a** `reportDays`; **E16** `inPersonRetentionMonths`.

## Da portare nel piano

**Nient'altro**: le impostazioni di un modulo stanno nel suo design e nel suo piano, non nel piano generale (così per tour e
training).
