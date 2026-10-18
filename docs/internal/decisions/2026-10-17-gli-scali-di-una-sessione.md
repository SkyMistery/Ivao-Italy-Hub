# Gli scali di una sessione della rete (E10v)

**Data:** 17 ottobre 2026 — fase E10v di M4, PR del nucleo (#291)
**Stato:** **scelta tecnica**, per dare a E14b gli scali di una sessione senza che il modulo degli eventi nomini IVAO. La fase del nucleo
l'ha scelta `dalberone` (17 ottobre 2026), come per E10t; e, letto il codice, `dalberone` ha scelto anche **quale revisione** del piano
di volo dicono gli scali (§2.3): tutte, in una lista; i due campi dicono l'ultima, la revisione che la fase raccomandava (§3.2). **Nessuna domanda per Carmine**: si aggiunge un campo a una forma che c'è già,
come la nota di E10t prevedeva (§3.2: «se una fase dopo ne avrà bisogno, si aggiunge qui un campo, con la sua nota»), nessun
comportamento esistente cambia e non c'è una chiamata nuova alla rete.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: si estende `INetworkSessionSource` (E10t, nota
`2026-10-16-le-sessioni-della-rete-nel-nucleo`), il pezzo del nucleo da cui un modulo legge le sessioni della rete; `CLAUDE.md` §3: il
modulo non legge mai la riga del tracker da sé. È una PR del nucleo, prima di E14b che la usa (`CLAUDE.md` §0 regola 6): nessun uso negli
eventi qui.

## 1. Che cosa serve, e perché la domanda non basta

- **E14b** (design M4 §1.8, §1.9, §5.1; i PIREP `Auto`) scrive per chi ha la preferenza una voce di volo — callsign, partenza, arrivo,
  giorno — da ogni sua sessione, e le regole di award `Route`, `RouteTimes` e `MinLegDistance` leggono partenza e arrivo.
- **`NetworkSession` non li dice**: `NetworkSession(Id, Vid, Callsign, StartedAt, EndedAt)`, per scelta di E10t (§3.2 della sua nota).
- **Chiedere per scalo non basta**: le sessioni di un VID con partenza X dicono che è partito da X, mai dove è arrivato; e per un evento
  `whole_division` gli scali sono tutti quelli della divisione.
- **La riga c'è già**: il client legge `IvaoTrackerSessionDto`, con gli scali della **prima** revisione e il JSON grezzo della riga, che
  porta **ogni** revisione del piano in forma breve (`flightPlans`: `id`, `departureId`, `arrivalId`, `aircraftId`).

## 2. Che cosa c'è, letto nel codice (`main` a `8bf63ea3`)

1. **La riga**: `IvaoTrackerReader.ReadSession` prende partenza e arrivo dal **primo** elemento di `flightPlans`, in maiuscolo; il commento
   del DTO lo dice (⚠️ «the airports here are the first revision's»). Il client vero e quello delle fixture leggono la riga con lo stesso
   lettore, e `RawJson` è la riga intera.
2. **L'ordine delle revisioni** nella riga: misurato su ogni volo registrato con più di una revisione (`tracker-sessions-780001.json`,
   `tracker-sessions-780002.json`, otto sessioni) contro i loro `tracker-flightplans-<id>.json`: la riga elenca i piani dal primo
   all'ultimo, con gli `id` crescenti come i numeri di revisione.
3. **Il caso che decide**: nella sera di Fiumicino (`tracker-airport-LIRF.json`, E10a) la sessione `1000007` ha la prima revisione da LIPZ a
   LIRF e la seconda da LIRF a LICR. La prima direbbe LIPZ→LIRF; E13a, che chiede partenza e arrivo insieme (una stessa revisione, nota
   `2026-09-30-il-tracker-senza-vid` §3.6), trova la stessa sessione per una prenotazione LIRF→LICR. Una sola coppia, la prima, avrebbe
   fatto dire a E14b una rotta diversa da quella che E13a ha dato per volata; e un pilota che vola due tratte nella stessa connessione ne
   avrebbe mostrata una.
4. **Chi costruisce `NetworkSession`**: la sorgente; il doppio di `INetworkSessionSource` di `EventsAfterTests` (E13a) e i test di unità
   `EventsAfterRulesTests` (E13a) e `NetworkSessionSourceTests` (E10t), tutti con i cinque argomenti posizionali. Nessuno decostruisce il
   record.

## 3. La forma

In `src/IvaoHub.Core/Ivao/NetworkSessionSource.cs`:

```csharp
public sealed record NetworkSession(
    long Id, int Vid, string Callsign, DateTime StartedAt, DateTime EndedAt,
    string? DepartureIcao = null, string? ArrivalIcao = null)
{
    public IReadOnlyList<NetworkRoute> Routes { get; init; } = [];
    // l'uguaglianza confronta Routes per contenuto
}

public sealed record NetworkRoute(string DepartureIcao, string ArrivalIcao);
```

1. **`Routes`**: partenza e arrivo di **ogni revisione** del piano, dalla più vecchia, in maiuscolo; una revisione senza uno dei due scali
   resta fuori; vuota per un controllore o una sessione senza piano. Una per revisione, anche due uguali (la sessione `1000010` ne ha due
   LIRF→LFKJ): chi conta le rotte volate (`RouteTimes`) conta le sessioni, non le revisioni.
2. **`DepartureIcao` e `ArrivalIcao`**: quelli dell'**ultima** revisione, cioè l'ultima voce di `Routes`; `null` quando `Routes` è vuota.
   L'ultima è il piano con cui la sessione è finita: per la `1000007` LIRF→LICR, la rotta che E13a trova.
3. **Le due posizioni in fondo con un predefinito** tengono compilato ogni `new NetworkSession(…)` di oggi; `Routes` è una proprietà
   `init`, fuori dai posizionali, per la stessa ragione.
4. **L'uguaglianza resta di valore**: un record con una lista confronterebbe la lista per riferimento, e due sessioni con le stesse rotte
   sarebbero diverse; `Equals` e `GetHashCode` sono scritti nel record e confrontano `Routes` voce per voce. Un test lo prova.
5. **Il lettore resta uno**: le revisioni si leggono con `IvaoTrackerReader.ReadPlannedRoutes(IvaoTrackerSessionDto)`, accanto a
   `ReadSession`, dal `RawJson` che il client ha già, **nell'ordine del loro `id`** (§6), che cresce con la revisione (§2.2): il client vero e quello delle fixture rispondono uguale, e non c'è una chiamata nuova.
   Una riga che non è JSON, o senza `flightPlans`, non ha revisioni.
6. **Il DTO non cambia**: la sua forma, la sua uguaglianza e gli scali della prima revisione restano quelli che i tour leggono; il suo
   commento rimanda al lettore nuovo. FlightOps non è toccato.
7. **Nessun nome contiene «ivao»** nella forma che il modulo legge.

## 4. Alternative scartate

| Alternativa | Perché no |
|---|---|
| **Gli scali del DTO com'è** (la prima revisione) | la forma più piccola, ma dice LIPZ→LIRF per la sessione che E13a trova LIRF→LICR (§2.3): `Route` di E14b non tornerebbe con E13a |
| Solo l'ultima revisione, senza `Routes` | dice giusto la `1000007`, ma un pilota con due tratte in una connessione ne mostra una; `dalberone` ha scelto tutte le revisioni |
| Una lista nel DTO (`IvaoTrackerSessionDto`), letta da `ReadSession` | cambierebbe l'uguaglianza del record che i tour e i loro test confrontano; il lettore accanto a `ReadSession` legge la stessa riga senza toccarlo |
| Le revisioni intere (`GetFlightPlansAsync`) | una chiamata alla rete per ogni sessione, fino a mille in un giro; gli scali sono già nella riga |
| `Routes` con le coppie una volta sola | tre regole per una lista (quale ordine, quale ultima); una per revisione è ciò che la riga dice |

## 5. Che cosa si tocca

Tutto del nucleo, ed è il perché di questa nota (`core-guard`):

- `src/IvaoHub.Core/Ivao/NetworkSessionSource.cs` (i due campi, `Routes`, `NetworkRoute`, l'uguaglianza, la sorgente che li riempie);
- `src/IvaoHub.Core/Ivao/IvaoTracker.cs` (`IvaoTrackerReader.ReadPlannedRoutes`, e una frase nel commento del DTO);
- `tests/IvaoHub.UnitTests/NetworkSessionSourceTests.cs` (di E10t, di questo contributore): cinque casi nuovi (il quinto dopo la revisione, §6) — l'ultima revisione e
  ogni revisione nella sera di Fiumicino, le maiuscole e la revisione senza arrivo, l'ordine per `id`, un controllore e due sessioni senza piano,
  l'uguaglianza —; e il caso delle fixture di E10t confronta i cinque campi di prima da tutte e due le parti, perché la sorgente ora dice
  anche gli scali. **Nessun test del maintainer cambiato.**
- **Nessuna fixture**: la sera di Fiumicino porta già le revisioni. **Nessuna riga** di un modulo; niente nel browser, nessuna migrazione.

## 6. Dopo la revisione (#291)

Il revisore l'ha trovata [approvabile][r291], con due punti non bloccanti:

1. **«Dalla più vecchia» era l'ordine dell'array**, misurato sui voli registrati ma non imposto: se la rete un giorno elencasse i piani in
   un altro ordine, «l'ultima» non sarebbe più la più recente, e le fixture non se ne accorgerebbero. **Fatto**: `ReadPlannedRoutes`
   ordina le revisioni per `id` (un piano senza `id` resta al suo posto nella riga), e un caso nuovo, con la riga che le elenca dalla più
   recente, lo prova (cade togliendo l'ordine).
2. **Per E14b, non qui**: `Routes` sono i piani **depositati**, non le tratte volate; chi deposita LIRF→LICR, corregge in LIRF→LIMC e vola
   quella ha tutte e due in `Routes` e LIRF→LIMC nei due campi. La nota di E14b dirà, regola per regola (`Route`, `RouteTimes`,
   `MinLegDistance`, i PIREP `Auto`), quale lettura usa, perché un award non si dia mai per una rotta solo depositata. Scritto in
   `HANDOFF-M4.md`, «Che cosa ha lasciato E10v».

[r291]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/291#issuecomment-6246724281

## Da portare nel piano

- **`09-design-m4.md` §5.1**, il paragrafo «Deciso con E10t»: la sessione risponde anche gli scali — `DepartureIcao`/`ArrivalIcao`
  dell'ultima revisione del piano e `Routes`, ogni revisione dalla più vecchia —, `null` e vuota per un controllore o senza piano; dal 17
  ott 2026 (M4, E10v). E14b legge da lì la voce di volo di un PIREP `Auto` e le rotte delle regole `Route`, `RouteTimes`,
  `MinLegDistance`.
- **`10-piano-implementazione-m4.md`**: E10v, scritta in questa PR; E14b dipende anche da E10v.
