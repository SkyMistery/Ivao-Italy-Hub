# L'orologio del client NOAA: la finestra della storia si misura con `IClock`

**Data:** 9 ottobre 2026 — correzione del nucleo, sessione di lavoro di Carmine, ramo `fix/noaa-history-clock`
**Stato:** **Correzione**, non una decisione di prodotto: in produzione non cambia niente (`IClock` è `SystemClock`). Scritta perché
cambia il costruttore di una classe del nucleo.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: il meccanismo c'è già (`IClock`, che `WeatherSource` usa accanto), e il client
NOAA era l'unico punto del dominio che leggeva l'ora da `DateTime.UtcNow`.

## 1. Che cosa è successo

- Dal 9 ottobre 2026 `build-test` cade su ogni PR (#233, #237, #238, #239, #240; per esempio la run 37930875044) e cadrebbe su
  `main` alla prossima esecuzione, per un solo test d'unità:
  `WeatherTests.AForecastIsAskedForWithADateAndWithoutHours`, `Assert.NotNull() Failure` alla riga 164.
- Il test chiede a `NoaaWeatherClient.GetHistoryAsync` la finestra del **9 settembre 2026, 06–08Z**, una data fissa. Il client
  risponde `null` senza chiedere quando `DateTime.UtcNow - fromUtc > IWeatherSource.HistoryWindow` (trenta giorni): il 9 ottobre
  alle 06Z la data fissa è uscita dalla finestra. **Un test con una data che scade**, non una regressione di una PR aperta.

## 2. La correzione

1. `NoaaWeatherClient` riceve `IClock` nel costruttore e confronta con `clock.UtcNow`
   (`src/IvaoHub.Core/Weather/NoaaWeatherClient.cs`). Nessun'altra riga del client cambia.
   - La registrazione non si tocca: è un client tipizzato (`AddHttpClient<NoaaWeatherClient>` in
     `WeatherServiceCollectionExtensions`), e `IClock` è già un singleton (`Program.cs`); `WeatherTests.TheChainIsWiredAndResolves`
     lo risolve dal contenitore. L'unico altro `new NoaaWeatherClient(...)` del repository è l'helper `Noaa(...)` dei test.
2. `WeatherTests`: l'helper `Noaa(...)` dà al client lo `StubClock(Now)` che il file aveva già (16 settembre 2026, 08Z). Le date
   fisse e le stringhe attese (`date=20260909_0900`) restano come sono. I due test vicini che usavano `DateTime.UtcNow`
   (`AWindowOlderThanTheyKeepIsUnavailableWithoutAsking`, `AHistoryNobodyAnswersIsUnavailableAndNotEmpty`) ora partono da `Now`:
   nel file non c'è più l'orologio vero.
3. Versione **0.6.6** (PATCH: correzione, nessuna migrazione).

## 3. Che cosa non si è fatto

- **Rendere relativa a oggi la finestra del test** lasciando il client com'è: avrebbe chiuso il test e lasciato nel nucleo l'unica
  lettura di `DateTime.UtcNow` del dominio, cioè una regola (i trenta giorni) che nessun test può fissare a un giorno.
- **Una guardia d'architettura** («niente `DateTime.UtcNow` nel nucleo fuori da `SystemClock`»): oggi restano solo i registri
  d'avvio (`StartsLog`, `StartupTimings`, e `StartsWatch`/`StartupFailureWatch` in `IvaoHub.Web`), che scrivono l'ora vera di
  proposito. `ArchitectureTests.cs` è del maintainer: è una proposta, non parte di questa correzione.

## 4. Le altre date fisse nei test (cercate, non toccate)

Cercato in `tests/IvaoHub.UnitTests` e `tests/IvaoHub.IntegrationTests` ogni data scritta a mano (`new DateTime(202x…`, `"202x-…"`)
e ogni uso dell'orologio vero. **Nessun altro test ha lo stesso difetto**:

- i test d'unità con date fisse passano il «adesso» come argomento o con un orologio finto (`TourStateTests`,
  `TrainingStaffRulesTests`, `TrainingHistoryRulesTests`, `AtcPositionMinimumTests`, `EventsSlotsTests`, `LegImportTests`, …), e
  dopo questa correzione nel dominio non c'è più codice che legga `DateTime.UtcNow`;
- i test d'integrazione girano con l'orologio vero e costruiscono le date da `DateTime.UtcNow`. Le date fisse sono due file:
  `DocumentLifeTests` (`2026-10-01`, `2027-10-01`, `2026-11-01` sono solo salvate e rilette; la scaduta è `2026-01-01`, la non
  scaduta `2099-01-01`) e `ContactThreadTests` (un evento del 1º giugno 2026, già passato quando il test è nato).

Una trappola **diversa**, vista passando e non toccata: `PirepTests.Weather.cs` riga 34 compone un istante leggendo
`DateTime.UtcNow` quattro volte (anno, mese, giorno, ora): a cavallo di un'ora o della mezzanotte può comporre un istante sbagliato.
Non è mai caduto, per quanto ne so.

## 5. Che cosa non è stato verificato

- Vedi il corpo della PR per i comandi e i risultati. La chiamata vera a NOAA non è stata provata: il cambiamento non tocca né
  l'URL né la lettura della risposta.

## Da portare nel piano

- Niente che cambi una sezione del piano. Una riga di changelog per la **0.6.6**: «il client NOAA misura la finestra della storia
  con `IClock` (il test con la data del 9 settembre scadeva il 9 ottobre)».
- Per `HANDOFF.md`, fra le trappole: ⚠️ **l'ora si legge da `IClock`, mai da `DateTime.UtcNow`**, e un test d'unità che scrive una
  data fissa dà al codice un orologio fisso.
