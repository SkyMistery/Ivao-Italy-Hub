# La pulizia dei sottotour: quando la conservazione tocca un sottotour

- **Data**: 14 novembre 2026
- **Stato**: **decisa da Carmine il 14 novembre 2026**, in chat al master: **A, «Col contenitore»** (la domanda: «quando la
  conservazione dei dati pulisce un sottotour?»). Riportata dal master in https://github.com/SkyMistery/Ivao-Italy-Hub/pull/496#issuecomment-6874810397
- **Chi**: la sessione di lavoro `fix/review-lot8-tours` (lotto 8 della revisione del codice, il server dei tour)
- **Classificazione** (`CLAUDE.md` §5): **(b)**, tutta nel modulo: cambia quando `TourRetentionJob` considera pronto un sottotour; il
  nucleo non si tocca
- **Completa** il §3 di `2026-09-25-la-conservazione-dei-tour.md` («ogni tour sulle sue date»), che non si riscrive

## 1. Che cosa non va

`TourRetentionJob` (`Tours/TourRetentionJob.cs`) guarda ogni tour sulle sue date: un sottotour scade 13 (o 25) mesi dopo la **sua**
chiusura, senza guardare il contenitore, e ripulendolo **cancella le sue iscrizioni** (`ArchiveAsync`, `database.Enrolments.RemoveRange`).
Il contenitore però conta i sottotour completati **proprio da quelle iscrizioni** (`Pireps/PilotProgress.cs`, `SubtoursDoneAsync`:
`fo_enrolments` con `completed_at` dei sottotour). La nota del 25 settembre lo permette in chiaro: «uno con date sue scade da solo».

Il caso: un contenitore che resta aperto più di 13 mesi dopo la chiusura di un suo sottotour (un contenitore su più anni, un sottotour
del primo anno). Il job pulisce il sottotour; i piloti che l'avevano completato lo perdono dal conteggio del contenitore, e chi ne
completa un altro dopo può non arrivare mai a `RequiredSubtours`: niente completamento, niente award. Nessuno se ne accorge, perché la
pagina del pilota mostra solo il conteggio più basso.

## 2. Le strade

| | Che cosa fa | Pro | Contro |
|---|---|---|---|
| **A** (raccomandata) | **Un sottotour si pulisce solo insieme al suo contenitore**: finché il contenitore non è pronto per la pulizia (o già pulito), il sottotour **aspetta**, come aspetta un tour con un PIREP aperto, e il job lo conta tra «in attesa». Quando il contenitore scade, nello stesso giro vanno tutti e due. | Poche righe in `WaitsAsync`; nessun dato nuovo; il conteggio del contenitore resta vero finché il contenitore vive; il registro non cambia. | Un sottotour breve dentro un contenitore lungo tiene briefing, regole, tracce ed esiti dei controlli fino alla scadenza del contenitore (al più 25 mesi dopo la chiusura del contenitore, non del sottotour). |
| B | Il sottotour si pulisce sulle sue date ma **le iscrizioni completate restano** finché il contenitore non è pulito. | La pulizia del sottotour resta puntuale. | Una pulizia a metà, con un'eccezione in `ArchiveAsync` e un secondo passaggio quando scade il contenitore; le iscrizioni sono dati di una persona e restano oltre la loro scadenza lo stesso. |
| C | Il contenitore conta i sottotour da qualcosa che la pulizia non tocca (i PIREP accettati, che restano nel registro, o una riga del contenitore scritta al completamento). | Pulizie indipendenti. | Cambia come si calcola il progresso di un contenitore (`PilotProgress`, `TourCompletion`): più codice e un rischio sul calcolo che oggi è giusto, per un caso raro. |
| D | Nulla nel codice: si scrive che un contenitore non deve restare aperto più di 13 mesi dopo un suo sottotour. | Zero codice. | È una regola che nessuno controlla; il danno (un award mai dato) si vede tardi. |

## 3. Raccomandazione

**A.** È la più piccola, tiene vero il conteggio senza toccare il calcolo del progresso, e usa lo stesso meccanismo che il job ha già
per i tour «in attesa». Il costo — i dati pesanti di un sottotour tenuti fino alla scadenza del contenitore — è lo stesso che la nota del
25 settembre accetta già per i sottotour con le date del contenitore.

## 3-bis. Com'è fatto (dopo la risposta)

- `TourRetentionJob.RunAsync`: un sottotour scaduto **aspetta** (contato fra «in attesa», come un tour con un PIREP aperto) finché il
  suo contenitore non ha `purged_at`. I tour scaduti si trattano **prima quelli di primo livello**, poi i sottotour: così un contenitore
  che scade nello stesso giro dei suoi sottotour li porta con sé in quel giro, non in quello del mese dopo.
- Il contenitore aspetta già per conto suo (PIREP aperti dei suoi, una segnalazione d'award in attesa): allora aspettano anche i suoi
  sottotour.
- **L'altra metà** (rilievo del master sulla #496): il contenitore **aspetta finché aspetta uno dei suoi sottotour** (un suo PIREP
  da decidere o contestato, un suo completamento con l'award in attesa; `WaitsAsync` guarda il tour e i suoi figli). Senza, un
  contenitore e un sottotour scaduti nello stesso giro, con un PIREP del sottotour ancora aperto: il contenitore se ne andava con le sue
  iscrizioni, e il PIREP accettato dopo non trovava più l'iscrizione del contenitore, quindi né il completamento né l'award. Non c'è
  ciclo: il sottotour aspetta il contenitore per le date, il contenitore aspetta il sottotour solo per i PIREP e gli award.
- Test: `PirepTests.ASubtourWaitsForItsContainerBeforeTheRetention` (contenitore aperto, un sottotour chiuso da 14 mesi: non si pulisce
  e il suo completamento conta ancora, il pilota completa il contenitore con l'altro; scaduto il contenitore, vanno tutti e tre in un
  giro). Sul codice di prima fallisce: il sottotour veniva pulito. `PirepTests.AContainerWaitsWhileOneOfItsSubtoursWaits` (contenitore e
  sottotour scaduti insieme, un PIREP del sottotour da decidere: nessuno dei tre si pulisce; deciso, il contenitore si completa e il giro
  dopo vanno tutti e tre). Sul codice di prima fallisce: il contenitore e l'altro sottotour venivano puliti.

## 4. La risposta di Carmine

**A, «Col contenitore»** (Carmine, in chat al master, 14 novembre 2026): un sottotour si pulisce solo insieme al suo contenitore, e
finché il contenitore non è scaduto aspetta. Il commento del master sulla PR che lo riporta: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/496#issuecomment-6874810397.

## Da portare nel piano

- `05-design-m2.md` §10 (il paragrafo «Fatto in T20a»): un sottotour si pulisce solo con il suo contenitore e intanto aspetta; il
  contenitore aspetta finché aspetta uno dei suoi sottotour; i tour di primo livello si trattano prima dei sottotour.
- Piano: dove descrive la conservazione dei tour di T20a (la conservazione mensile, `TourRetentionJob`), la stessa frase.
- `2026-09-25-la-conservazione-dei-tour.md` §3 resta com'è; il piano dice che questa nota lo completa.
