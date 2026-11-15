# Il deadlock da solo nel nucleo, e un test contro le copie di `WriteConflicts`

- **Data**: 15 novembre 2026
- **Stato**: decisa da Carmine nella direzione della issue #519, in chat al master il 14 novembre 2026 («Sì a tutte e cinque, come
  raccomandi»); la forma (una terza domanda, il suo nome, il testo del test) è scelta di una sessione di lavoro del maintainer, e il
  revisore la legge come tale. Il comportamento visibile non cambia (§2).
- **Classificazione** (`CLAUDE.md` §5): **(b)**, il meccanismo del nucleo `WriteConflicts` (nota
  `2026-11-14-un-altra-scrittura-ha-preso-la-chiave`, piano §16 punto 18) esteso con la domanda che mancava, e le due copie dei moduli
  tolte. Una PR sola, con `Closes #519`: la regola delle issue (`decisions/2026-10-23-chi-puo-prende-la-issue.md`) ammette che una
  correzione tocchi il nucleo e i moduli insieme, e qui il nucleo aggiunge solo ciò che i due moduli chiamano.

## 1. Che cosa c'era

Dopo la #499 la domanda «un'altra scrittura ha vinto?» stava in `WriteConflicts` con due forme, `AnotherWriteWon` (chiave duplicata o
deadlock) e `DuplicateKey` (la chiave e basta). Restava fuori la terza, **il deadlock e basta**, scritta due volte:

- `Modules.Events/Data/DatabaseErrors.cs`, `Deadlocked`: chiesta dalle tre prenotazioni del pilota (`BookingEndpoints`: il volo, la
  rotazione, lo slot privato: «riprova») e dalla generazione degli slot (`EventSlotEndpoints`: 409);
- `Modules.FlightOps/Review/ReviewEndpoints.cs`, `IsDeadlock`: due presi in carico dello stesso PIREP, la seconda è 409. Nominava
  `MySqlConnector` dentro il modulo dei tour.

Tutte e due percorrevano la catena intera e leggevano il codice `LockDeadlock`: rispondevano **esattamente** come risponde ora il nucleo.

## 2. Che cosa c'è ora

- **`WriteConflicts.Deadlock`** — il deadlock **e basta**, sulla catena intera, sul codice; `null` per tutto il resto, chiave duplicata
  compresa. È la domanda di un posto dove una chiave duplicata non è attesa — nessun indice unico può rifiutare la scrittura, oppure la
  chiave ha già avuto la sua risposta più dentro (le prenotazioni la chiedono a `DuplicateKey` in `PilotBookings`) —: lì l'unico modo in
  cui un'altra scrittura vince è che le due si blocchino sulle stesse righe, e una chiave duplicata che arriva fin lì non è una notizia
  ma un guasto, che deve uscire come tale.
- I quattro chiamanti degli eventi e quello della revisione chiedono `WriteConflicts.Deadlock(exception) is not null`. `Deadlocked` e
  `IsDeadlock` spariscono. In `DatabaseErrors` restano le domande degli eventi sulle loro chiavi esterne (`LostTheSlot`,
  `SlotWasBooked`, `StillReferenced`), che non sono questa.
- Perché una terza funzione e non `AnotherWriteWon`: nei cinque posti una chiave duplicata non è attesa, e prenderla come «riprova» o
  come 409 nasconderebbe un difetto. Perché
  non un parametro: come nella nota del 14 novembre, il nome dice quale domanda si fa.

Nessuna delle due copie aveva una ragione per restare: nessuna guardava altro che il codice.

## 3. Il test contro le copie

`ArchitectureTests.NobodyButWriteConflictsAsksWhetherAnotherWriteWon` legge ogni `.cs` di `src/` (fuori dalle migrazioni e da `obj/`)
tranne `Core/Data/WriteConflicts.cs` e rifiuta, riga per riga con `file:riga`:

- i nomi `DuplicateKeyEntry` e `LockDeadlock` (i due codici della domanda, con o senza `MySqlErrorCode.`);
- un `Message` seguito sulla stessa riga dalla stringa `"Duplicate` (maiuscole o minuscole): la forma delle copie di Training e dei
  PIREP prima della #516 e della #517.

Non rifiuta gli altri codici di `MySqlErrorCode` (le chiavi esterne degli eventi, gli errori di connessione di `ConnectionCheckEndpoints`),
né la parola «Duplicate» in un commento o in un nome («Duplicate» di un evento, E8b). Non prende i codici scritti come numeri (1062,
1213): nessuno l'ha mai fatto qui, e un numero nudo nel codice lo ferma già la revisione.

**Provato con una copia rimessa apposta**: in fondo a `ReviewEndpoints.cs` una classe con `IsDeadlock` su
`MySqlErrorCode.LockDeadlock` e un `Message.Contains("Duplicate", …)`: il test cade con le due righe di quel file; tolta la classe,
torna verde.

## 4. Le prove

`WriteConflictsTests`: il deadlock due livelli sotto un'`InvalidOperationException` è anche `Deadlock`; una chiave duplicata (anche due
livelli sotto), una chiave esterna e `null` non lo sono. I test d'integrazione che c'erano sulle corse delle prenotazioni, della
generazione degli slot e della presa in carico dei PIREP restano com'erano.

## Da portare nel piano

- §16 punto 18: **tre domande** — `AnotherWriteWon`, `DuplicateKey` e **`Deadlock`** (il deadlock e basta, dove una chiave duplicata
  non è attesa: le prenotazioni e la generazione degli slot degli eventi, la presa in carico dei PIREP); il ⚠️ sulle
  due copie si chiude (#519); e il test d'architettura `NobodyButWriteConflictsAsksWhetherAnotherWriteWon` che lo tiene.
- §9.7, dove dice di due scritture della stessa chiave: dove una chiave duplicata non è attesa si chiede il deadlock da solo, e la chiave
  esce come il guasto che è.
- `CLAUDE.md` §2, la riga «A write that lost to another write of the same key»: aggiungere `Deadlock` (the deadlock alone) e il test
  (la scrive il maintainer).
