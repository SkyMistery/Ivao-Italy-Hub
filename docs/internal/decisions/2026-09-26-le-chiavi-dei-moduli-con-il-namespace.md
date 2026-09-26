# Le chiavi dei moduli con il namespace, anche sul server

**Data:** 26 settembre 2026, dopo la revisione della PR #133 (A4a)
**Stato:** **decisa** (Carmine, 26 settembre 2026, in chat al revisore: «procedi» sulla raccomandazione del revisore, che
era un seguito proposto nei [rilievi sulla #133][r]).
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: il meccanismo è il catalogo delle lingue del server come l'ha lasciato A4a
(nota `2026-09-26-le-parole-di-piu-moduli`); cambia come il codice di un modulo lo interroga, e la convenzione scritta in
`CONTRIBUTING.md`.

[r]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/133#issuecomment-5844271855

## 1. Il problema

- **A4a** (#133) tiene le parole di più moduli nel catalogo del server: ogni chiave di un modulo sta sotto il suo namespace
  (`flightops:threads.legLabel`), e **anche senza** finché nessun altro modulo la dichiara.
- **Il codice dei tour chiede le sue chiavi senza namespace**, come diceva `CONTRIBUTING.md` («in C# a module key is
  `threads.x`, never `flightops:threads.x`»): 12 letture in 7 file (`threads.*`, `awards.tourCompleted`, `errors.agent*`,
  `mail.flightops.*`).
- **La trappola**: appena un secondo modulo dichiara una di quelle chiavi, la lettura senza namespace non ha più risposta, e
  `LocaleCatalog.Resolve` restituisce **la chiave stessa**. Una mail dei tour direbbe `threads.legLabel`, e niente fallirebbe:
  né l'avvio, né un test che non guardi quel testo. Il training ne condivide già quattro con i tour (`settings.*`,
  `nav.settings`), per ora non lette dal C#.

## 2. La decisione

1. **In C#, la chiave di un modulo si chiede con il suo namespace**, come nel browser: `flightops:threads.legLabel`. Le 12
   letture dei tour passano a questa forma (lo stile c'era già: `TourShape` scrive `flightops:errors.legHubOnly`).
2. **Restano senza namespace** le chiavi del nucleo, e le mail dei tipi di notifica (`mail.{tipo}.subject|body`): le compone il
   nucleo, e il tipo porta già il nome del suo modulo (`flightops.pirepAccepted`), quindi non collidono per costruzione.
3. **Un test di architettura lo tiene**: `ArchitectureTests.AModuleKeyIsAskedWithItsNamespaceOnTheServer` legge i file di
   lingua dei moduli (quelli con `_source`) e ogni letterale del C# in `src/`. Fallisce su una chiave che solo un modulo
   dichiara, scritta senza namespace, e su una con namespace che il file di quel modulo non ha (un refuso a cui il catalogo
   risponderebbe con la chiave). Sul codice dei tour di prima fallisce; su questo passa.
4. **`CONTRIBUTING.md`**, «Traps already paid for»: la riga si capovolge.

La risposta senza namespace di A4a resta: non serve più ai tour, ma toglierla cambierebbe di nuovo il nucleo per niente.

## 3. Alternative scartate

| Alternativa | Perché no |
|---|---|
| Lasciare le letture senza namespace e scrivere solo la trappola | resta una rottura silenziosa, che arriva con il modulo di qualcun altro |
| Un helper nel modulo che aggiunge `flightops:` | un pezzo in più per una stringa; il letterale completo è quello che il test legge, e lo stile c'è già |
| Rifiutare all'avvio una chiave che due moduli dichiarano | è ciò che A4a ha tolto: `nav.section` la dichiarano per forza tutti i moduli |

## 4. Che cosa si tocca

- `src/IvaoHub.Modules.FlightOps/`: `Agent/AgentContract.cs`, `Agent/AgentEndpoints.cs`, `People/Bans.cs`,
  `Pireps/TourCompletion.cs`, `Review/PirepReview.cs`, `Threads/FlightOpsReferences.cs`, `Threads/PirepDisputes.cs`.
- `tests/IvaoHub.UnitTests/ArchitectureTests.cs` (il test nuovo), `CONTRIBUTING.md`, questa nota.
- **Vale anche per il training**: da A4 in poi, il C# del modulo chiede `training:…`.

## Da portare nel piano

- **§16 punto 8** (un solo set di file di lingua), insieme alla nota di A4a: il server chiede la chiave di un modulo con il suo
  namespace, come il browser; senza namespace solo le chiavi del nucleo e le mail dei tipi.
