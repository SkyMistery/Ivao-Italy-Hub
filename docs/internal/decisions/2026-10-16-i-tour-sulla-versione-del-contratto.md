# I tour sulla versione del contratto del nucleo

**Data:** 16 ottobre 2026 — una sessione di lavoro di Carmine sul modulo dei tour (Flight Ops)
**Stato:** **decisa**: è il passaggio che la nota di E10g ([`2026-10-06-la-versione-di-un-contratto-nel-nucleo`](2026-10-06-la-versione-di-un-contratto-nel-nucleo.md),
§5) affidava a «una sessione di Carmine, dopo l'unione», con le risposte di Carmine del 7 ottobre sulla #230 citate lì. Nessuna
decisione nuova sul meccanismo; la nota c'è perché la PR tocca un test del nucleo (`ContractVersionTests.cs`).
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: il contratto dell'agente del validatore usa `ContractVersion` del nucleo.

## 1. Che cosa cambia nei tour

- `AgentContract.RequireVersionAsync` se ne va. Al suo posto **un campo**, come la nota di E10g lo scriveva:

  ```csharp
  public static readonly ContractVersion Version = new(Header, Current, Accepted, VersionTitleKey, "agentContract");
  ```

  dichiarato dopo `Accepted`, che legge. Le costanti (`Header`, `Current`, `Accepted`, `VersionTitleKey`) restano: le leggono
  `AgentEndpoints` (l'endpoint aperto `/contract`, che scrive da sé la versione corrente), `AgentDesk` e `PirepTests.Agent`.
- `AgentEndpoints` mette sul gruppo `/api/flightops/agent` `.AddEndpointFilter(AgentContract.Version.RequireAsync)`, dopo la policy del
  token, com'era. Delle due strade della nota (il metodo che chiama il nucleo, o il metodo tolto) è la seconda: una riga, nessun
  metodo in più.
- I `using` di `System.Globalization`, `IvaoHub.Core.Localization` e `Microsoft.AspNetCore.Http` in `AgentContract.cs` non servono più.
- **Nessuna risposta cambia**: il test gemello di E10g aveva provato, su 120 coppie, che il nucleo con i valori dei tour risponde
  byte per byte come il filtro dei tour. `docs/agent-contract.md` resta com'è.

## 2. Che cosa cambia in `ContractVersionTests.cs`

- **Il test gemello** (`TheCoreAnswersEveryRequestAsTheToursFilterDoes`) confrontava il nucleo con la copia dei tour: la copia non c'è
  più, e il confronto proverebbe il nucleo con sé stesso. **Al suo posto** `TheToursAgentIsAnsweredAsItsOwnFilterAnsweredIt`: le stesse
  venti richieste, con e senza catalogo, in inglese, in italiano e senza utente, attraverso il contratto **come i tour lo dichiarano
  ora** (`AgentContract.Version`), e quello che il gemello diceva dei tour resta scritto — sono accettate `1`, `1` fra due spazi, `\t1` e
  `01`, e nessun'altra; un rifiuto è 400 e scritto in più di una lingua; una risposta accettata dice `1`. Se un giorno qualcuno
  dichiarasse il contratto dei tour con altri valori, è questo test che cade.
- Il campo `ToursInTheCore`, che costruiva il contratto con i valori dei tour «come il loro passaggio lo dichiarerà», è ora
  `AgentContract.Version`: gli altri test che lo leggono (il titolo nella lingua di chi chiede, la chiave senza catalogo) leggono il
  contratto vero dei tour.

## Da portare nel piano

- `05-design-m2.md` §6.6 (l'agente del validatore): **dal 16 ott 2026** la versione del contratto la controlla `ContractVersion` del
  nucleo (`AgentContract.Version`); i tour non hanno più un filtro loro.
- `00-piano-di-progettazione.md` §16, la riga di `ContractVersion` (nota di E10g): i tour sono passati, e il test gemello se n'è andato.
  Versione e changelog.
- `HANDOFF-M4.md`, la trappola «La copia dei tour c'è ancora» di E10g: chiusa.
