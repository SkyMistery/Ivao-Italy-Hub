# La formazione sul lettore dei nomi (A13f)

**Data:** 13 ottobre 2026 — fase A13f di M3, PR #258
**Stato:** **decisa** (Carmine, 13 ottobre 2026, data in chat alla sessione master e pubblicata dal master sulla #258 su sua
istruzione: [risposta][a258]): il codice va bene com'è; la PR **non si unisce con `core-guard` rosso**, e la riga della formazione esce da
`MemberNamesTests.cs` con questa nota, perché il guard passi per la sua regola.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: la formazione usa il meccanismo deciso in
[`2026-10-11-un-solo-lettore-dei-nomi`](2026-10-11-un-solo-lettore-dei-nomi.md) (E10m, #252), che al suo §5 aveva già scritto questo
passaggio. Nessuna decisione nuova sul lettore; la nota c'è perché la PR tocca un test del nucleo.

## 1. Che cosa esce da `MemberNamesTests.cs`, e perché

`tests/IvaoHub.IntegrationTests/MemberNamesTests.cs` è un test del nucleo (E10m): un file di test che esiste già e non ha «training» nel
percorso, quindi per `core-guard` è nucleo. Ne esce **solo la riga della formazione**:

- **dal test gemello**, che confrontava il lettore con le tre copie dei moduli: il servizio `TrainingPeople` e il suo `Assert.Equal`. Dopo
  il passaggio `TrainingPeople.NamesAsync` **è** `MemberNames.ReadAsync`, e il confronto proverebbe il lettore con sé stesso. Il test si
  chiama ora `TheReaderAnswersWhatTheToursAndTheEventsAnswer`;
- **da `AVidBelowOneIsNeverNamedEvenWhenAUserRowHasIt`**: la riga che affermava che la formazione nominava ancora un utente con un VID
  sotto uno. Dopo il passaggio non lo nomina più, come vuole la decisione sulla #247 (una persona cancellata non ha nome), e la riga
  cercherebbe una chiave che non c'è. È uno stato che l'hub non scrive (la cancellazione toglie l'utente, e uno pseudonimo non è di
  nessuno): nessuna pagina e nessuna mail cambia;
- il riassunto della classe dice che la formazione è passata sul lettore, e il commento rimasto nel test del VID sotto uno parla solo
  della copia degli eventi.

Restano **le righe degli eventi e dei tour** (la loro fase di dalberone; una sessione di Carmine) e
`AnEmptyNameIsKeptAndEachModuleStillReadsItItsOwnWay`, che legge ancora `TrainingPeople.Label` e `TrainingPeople.Member`, che restano.

Questo è esattamente il passaggio che §5 della nota di E10m prevedeva per «la formazione (una piccola fase di dalberone)»: «In
`MemberNamesTests` esce la riga della formazione dal test gemello e dal test del VID sotto uno».

## 2. Che cosa resta fuori

- **La forma del passaggio**: `TrainingPeople.NamesAsync` resta, come una riga, invece di andarsene, perché ha dieci chiamanti nel
  modulo. La nota di E10m lasciava le due strade; è una scelta tecnica dentro il modulo.
- **La fase degli eventi** toglierà la sua riga dallo stesso test con una nota come questa, e andrà in conflitto con questa PR: le righe
  sono vicine, e git dà conflitto anche quando due rami tolgono due righe vicine diverse. Si risolve togliendo tutte e due.

## Da portare nel piano

- `07-design-m3.md` §6 (il nome letto dai membri): **dal 13 ott 2026** (M3, A13f) la formazione lo legge con `MemberNames.ReadAsync`
  del nucleo; quello che la nota di E10m rimandava a «quando le fasi della formazione e degli eventi l'avranno adottato», per la
  formazione.
- `00-piano-di-progettazione.md` §16, la riga del lettore dei nomi (nota di E10m): la formazione è passata; restano gli eventi e i
  tour. Versione e changelog.
- `08-piano-implementazione-m3.md`: A13f, scritta in questa PR.

[a258]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/258#issuecomment-6146758656
