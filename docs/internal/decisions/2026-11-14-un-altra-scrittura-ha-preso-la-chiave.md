# «Un'altra scrittura ha preso questa chiave?»: una risposta sola, due domande

- **Data**: 14 novembre 2026
- **Stato**: scelta di una sessione di lavoro dentro la direzione della issue #499, scritta dal master dopo la revisione della #494; il
  revisore la legge come tale. Nessuna domanda per Carmine: il comportamento visibile resta quello di prima, salvo i due difetti del
  §3, che la issue chiama tali.
- **Classificazione** (`CLAUDE.md` §5): **(b)**, un meccanismo del nucleo che c'è (`WriteConflicts`, nato con la #494) esteso, e le
  copie che lo rifacevano tolte. Una PR del nucleo, poi una per modulo (`CLAUDE.md` §0, regola 6).

## 1. Che cosa c'era

La domanda «la scrittura è fallita perché un'altra ha preso la stessa chiave?» (la chiave duplicata di MariaDB, a volte con il deadlock
in cui si incontrano due insert della stessa chiave quando la riga è stata appena cancellata) aveva risposta in sette posti, scritta in
quattro modi: la catena intera o un livello solo; il codice d'errore o la parola «Duplicate» del messaggio; con o senza il deadlock, e
quasi mai detto quale dei due. La tabella è nella issue.

## 2. Che cosa c'è ora

`IvaoHub.Core/Data/WriteConflicts.cs` ha **due domande**, e nient'altro le fa:

- **`AnotherWriteWon`** — chiave duplicata **o** deadlock. È la domanda di chi deve solo riprovare, o rispondere 409
  (`DomainExceptionHandler`, la #494).
- **`DuplicateKey`** — la chiave duplicata **e basta**. È la domanda di chi deve dire alla persona «qualcun altro tiene questa cosa»: un
  posto, un account, una rivendicazione. Un deadlock non è quella notizia, perché può incontrare qualunque altra scrittura delle stesse
  righe (la revisione della #233, punto 3, che negli Eventi distingueva già `Duplicated` da `TookTheKey`).

Tutte e due **percorrono la catena intera** (EF riporta un deadlock come `InvalidOperationException` con la `MySqlException` due livelli
sotto, `CONTRIBUTING.md`) e **leggono il codice d'errore**, mai le parole del messaggio. `KeyOf` resta com'era.

Perché due funzioni e non una con un parametro: chi chiama deve dire quale delle due domande fa, e il nome lo dice meglio di un `bool`;
e una funzione per ogni codice («`Is(exception, code)`») riaprirebbe la porta alle copie con codici scelti a mano.

## 3. Ogni copia, caso per caso

| Dove | Prima | Ora | Perché |
|---|---|---|---|
| `Core/Ivao/RunwayDirectory.cs` | catena intera, solo la chiave | `DuplicateKey` | Due richieste caricano le piste dello stesso aeroporto: la seconda lascia le sue. Prende solo `DbUpdateException` (le piste sono righe nuove, non cancellate un attimo prima), come prima. **Uguale.** |
| `Core/Services/InitialisationMarker.cs` | catena intera, chiave o deadlock | `AnotherWriteWon` | Due avvii scrivono il marcatore insieme: in tutti e due i casi si riprova. **Uguale.** |
| `Core/Auth/DiscordLinks.cs` | un livello solo, solo la chiave | `DuplicateKey` | Due membri per lo stesso account: la chiave dice «preso» (`Taken`). Un deadlock non lo dice, resta `Failed` (un giro da rifare), come prima. **Cambia**: una chiave duplicata più in basso di un livello ora è `Taken` e non `Failed` (difetto della issue). |
| `Modules.Events/Data/DatabaseErrors.cs` `TookTheKey` / `Duplicated` | catena intera, codice | `AnotherWriteWon` / `DuplicateKey` | Le due funzioni del modulo spariscono e i chiamanti chiedono al nucleo la stessa domanda. Le altre domande del modulo (`LostTheSlot`, `SlotWasBooked`, `StillReferenced`, `Deadlocked`) restano lì: sono domande degli Eventi su chiavi esterne, non questa. **Uguale.** PR a parte. |
| `Modules.Training/Requests/TrainingRequests.cs` | un livello, parola «Duplicate» | `DuplicateKey` | Due richieste sulla stessa scala: «hai già una richiesta aperta». Un deadlock non è quella notizia, quindi non lo prende: resta l'errore di prima. **Cambia**: si legge il codice, a qualunque livello (difetto della issue). PR a parte. |
| `Modules.FlightOps/Pireps/PirepSubmission.cs` | un livello, parola «Duplicate» | `DuplicateKey` | Due invii della stessa sessione: «sessione già rivendicata». Come Training. **Cambia** allo stesso modo. PR a parte, dopo la #495 che tocca lo stesso file. |

La parola «Duplicate» prendeva anche errori che non sono una chiave presa (per esempio «Duplicate column name», un errore di schema):
con il codice quei casi tornano a essere l'errore che sono.

## 4. Le prove

`tests/IvaoHub.UnitTests/WriteConflictsTests.cs`: la chiave sotto un salvataggio risponde a tutte e due le domande; la chiave due livelli
sotto si trova ancora (il caso che `DiscordLinks` perdeva); il deadlock due livelli sotto in un'`InvalidOperationException` è
`AnotherWriteWon` e non `DuplicateKey`; un messaggio con «Duplicate» e un altro codice non è nessuna delle due; `KeyOf` legge il nome
dell'indice e non il valore. La `MySqlException` del driver ha i costruttori interni: il test la costruisce con quello che prende un
codice e un messaggio, e se il driver lo togliesse il test lo dice per nome. I test che c'erano (le corse sulle piste, sul marcatore, su
Discord, sulle prenotazioni, sulle richieste e sui PIREP) restano com'erano e verdi.

## Da portare nel piano

- §16 (meccanismi generici): una riga per `WriteConflicts` — le due domande (`AnotherWriteWon`, `DuplicateKey`), la catena intera, il
  codice e non il messaggio; nessun modulo ne scrive una copia.
- §9.7 (contratti trasversali), dove dice del 409 per due scritture della stessa riga: la chiave duplicata e il deadlock sono la stessa
  notizia per il 409, non per la persona a cui si dice «preso».
- `CLAUDE.md` §2, tabella dei meccanismi: una riga «A write that lost to another write of the same key → `WriteConflicts`» (la scrive il
  maintainer).
