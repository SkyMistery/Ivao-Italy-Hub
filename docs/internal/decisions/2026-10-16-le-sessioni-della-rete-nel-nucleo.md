# Le sessioni della rete, con un nome neutro, nel nucleo (E10t)

**Data:** 16 ottobre 2026 — fase E10t di M4, PR del nucleo (#277)
**Stato:** **scelta tecnica**, per dare a E13a la domanda che E10a ha già messo nel client (nota `2026-09-30-il-tracker-senza-vid`,
decisa da Carmine sulla #180 e sulla #210) senza che il modulo degli eventi nomini IVAO. La fase del nucleo l'ha scelta `dalberone`
(16 ottobre 2026), fra le strade di §4. **Nessuna domanda per Carmine**: la forma è quella di un pezzo che c'è già (`IAtcBookingSource`,
E15a), e nessun comportamento cambia.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: si estende la cartella del nucleo che parla con la rete (`Core/Ivao/`) con
un'interfaccia dal nome neutro sopra l'unico client, come E15a ha fatto per le prenotazioni ATC; `CLAUDE.md` §3: «a module never talks
to IVAO itself», e un modulo che lo nominerebbe passa dal nucleo. È una PR del nucleo, prima di E13a che la usa (`CLAUDE.md` §0
regola 6): nessun uso negli eventi qui.

## 1. Che cosa serve, e perché il modulo non ne fa a meno

- **E13a** (`events-after`, design M4 §5.1, §9.3; nota `dopo-l-evento-e-gli-award` §2.1) legge il tracker della rete: per ogni
  prenotazione le sessioni del pilota, per VID; **chi ha volato senza prenotare**, le sessioni negli scali dell'evento **senza VID**,
  partenze e arrivi, fino al tetto di 1000; le presenze ATC per VID quando la divisione non ha `atcData`, solo `ATC`.
- **La domanda c'è già**: E10a (#210) ha esteso `IIvaoApiClient.SearchSessionsAsync` con `IvaoSessionQuery` — VID facoltativo, scali,
  `ConnectionType`, `Limit` fino a `MaxLimit` — e scriveva (§3.6) che E13a l'avrebbe chiamata.
- **Ma il modulo non può nominarla**: `EventsArchitectureTests.TheModuleDoesNotNameTheNetwork` (E2) rifiuta ogni riga del codice degli
  eventi che contenga «ivao», tolti lo spazio dei nomi `IvaoHub.Core.Ivao`, `ivaohub` e `@ivao/atmosphere`. `IIvaoApiClient`,
  `IvaoSessionQuery`, `IvaoConnectionType` e `IvaoTrackerSessionDto` sono tutti nomi che il test trova. Il test dice la regola di
  `CLAUDE.md` §3: un nome che dice IVAO fuori dal suo perimetro va dentro, o passa dal nucleo.
- **Il precedente**: E15a (nota `2026-09-30-le-prenotazioni-atc-della-rete`) ha dato agli eventi le prenotazioni ATC con
  `IAtcBookingSource`, in `Core/Ivao/AtcBookingSource.cs`, sopra `GetDailyAtcBookingsAsync`; e gli eventi leggono già da
  `IvaoHub.Core.Ivao` pezzi dal nome neutro, come `RatingVocabulary` (E11a, E11b).

## 2. Che cosa c'è, letto nel codice (`main` a `4ad747ad`)

1. **Il client**: `SearchSessionsAsync(IvaoSessionQuery)` legge le pagine dalla più recente, una sessione una volta sola per `Id`, fino al
   `Limit`; `null` quando IVAO non si può chiedere, mai una lista vuota al suo posto. Il client delle fixture risponde con la stessa
   regola (`IvaoTrackerReader.Answers`).
2. **La domanda**: `IvaoSessionQuery(int? Vid, DateTime FromUtc, DateTime ToUtc, string? DepartureIcao, string? ArrivalIcao,
   IvaoConnectionType? ConnectionType)`, con `Limit` da 1 a `MaxLimit` (1000), `DefaultLimit` 200, e l'eccezione nell'`init`.
3. **La risposta**: `IvaoTrackerSessionDto`, con il JSON grezzo, gli scali della prima revisione, `Duration`, `EndedAt`, il tipo di
   connessione.
4. **I chiamanti**: solo i tour (`PirepSubmission`), per VID. Nessun modulo chiede senza VID.

## 3. La forma

In `src/IvaoHub.Core/Ivao/NetworkSessionSource.cs`, accanto a `AtcBookingSource.cs`:

```csharp
public interface INetworkSessionSource
{
    Task<IReadOnlyList<NetworkSession>?> SessionsAsync(NetworkSessionQuery query, CancellationToken cancellationToken = default);
}

public enum NetworkConnection { Pilot, Atc }

public sealed record NetworkSessionQuery(
    int? Vid, DateTime FromUtc, DateTime ToUtc,
    string? DepartureIcao = null, string? ArrivalIcao = null, NetworkConnection? Connection = null)
{
    public const int DefaultLimit = IvaoSessionQuery.DefaultLimit;
    public const int MaxLimit = IvaoSessionQuery.MaxLimit;
    public int Limit { get; init; } = DefaultLimit; // 1..MaxLimit, altrimenti ArgumentOutOfRangeException
}

public sealed record NetworkSession(long Id, int Vid, string Callsign, DateTime StartedAt, DateTime EndedAt);
```

1. **La domanda passa intera**: `NetworkSessionSource`, interna, la scrive in una `IvaoSessionQuery` — VID o nessuno, finestra, scali,
   `Pilot` → `IvaoConnectionType.Pilot`, `Atc` → `Atc`, `Limit` — e chiede l'unico client. Le regole del tracker (la finestra
   sull'inizio, uno scalo in qualunque revisione, due nella stessa) restano quelle di E10a; il commento dell'interfaccia le ripete per
   chi chiama.
2. **La risposta è il poco che serve**: il numero della sessione (per contarla una volta fra partenze e arrivi), il VID, il callsign,
   l'inizio e la fine dichiarata (`StartedAt + Duration`). Il resto — scali, revisioni, JSON grezzo — resta nel nucleo; se una fase
   dopo ne avrà bisogno, si aggiunge qui un campo, con la sua nota.
3. **`null` resta `null`**: «la rete non ha risposto» non è mai «nessuno ha volato».
4. **L'ordine è una promessa dell'interfaccia**: dalla più recente, una volta per `Id`, al più `Limit`. Il client lo fa già; la
   sorgente lo ridice, perché non dipenda dal client che sta dietro (lo stesso fa `AtcBookingSource`).
5. **Il tetto resta uno**: `NetworkSessionQuery.DefaultLimit` e `MaxLimit` sono le costanti di `IvaoSessionQuery`, non due numeri; il
   rifiuto avviene già nella domanda neutra, prima di arrivare al client.
6. **Nessun nome contiene «ivao»** fuori dallo spazio dei nomi `IvaoHub.Core.Ivao`: il modulo lo scrive con un `using` che il test degli
   eventi già lascia passare, come per `RatingVocabulary`.
7. **Registrata** con `AddScoped` in `IvaoServiceCollectionExtensions`, accanto a `IAtcBookingSource`.
8. **I tour non cambiano**: continuano a chiamare il client, per VID. FlightOps non è toccato.

## 4. Alternative scartate

| Alternativa | Perché no |
|---|---|
| **Allentare il test degli eventi** (lasciar passare `IIvaoApiClient` e la sua domanda) | il test dice la regola di `CLAUDE.md` §3, e un test che diventa rosso dice che la modifica è sbagliata, non il test (`CLAUDE.md` §0 regola 3); è anche un file del modulo scritto in E2, ma cambiarlo per far passare il modulo è proprio ciò che la regola vieta |
| Rinominare nel nucleo i tipi del client (`IvaoSessionQuery` → un nome neutro) | toccherebbe i tour (FlightOps, non nostro) e il perimetro IVAO di `Core/Ivao/`, dove i nomi IVAO sono giusti |
| Restituire `IvaoTrackerSessionDto` sotto un altro nome (un alias, un `using`) | il tipo resta IVAO, con il JSON grezzo e le revisioni: il modulo leggerebbe la forma della rete, e un `using` alias nel modulo conterrebbe comunque il nome |
| Un metodo per ogni domanda di E13a (le sessioni di un pilota, di uno scalo, di un controllore) | tre metodi per una ricerca sola; la domanda di E10a ha già tutte le parti, e passarla intera tiene una sola strada |
| Il tipo di connessione con tutti e quattro i valori (`Observer`, `FollowMe`) | nessun modulo li chiede; si aggiungono il giorno che servono |

## 5. Che cosa si tocca

Tutto del nucleo, ed è il perché di questa nota (`core-guard`):

- `src/IvaoHub.Core/Ivao/NetworkSessionSource.cs` (nuovo: l'interfaccia, la domanda, la sessione, la sorgente);
- `src/IvaoHub.Core/Ivao/IvaoServiceCollectionExtensions.cs` (la registrazione);
- `src/IvaoHub.Core/Ivao/IIvaoApiClient.cs` (solo il commento di `SearchSessionsAsync`: un modulo chiede la sorgente);
- `tests/IvaoHub.UnitTests/NetworkSessionSourceTests.cs` (nuovo, unità: la domanda intera in quattro forme, la sessione, l'ordine con un
  doppione e il limite, `null`, il tetto, e il client delle fixture con la sera di Fiumicino). **Nessun test esistente cambiato.**
- **Nessuna riga** di un modulo; niente nel browser, nessuna migrazione.

## Da portare nel piano

- **§16** (la riga «IVAO API», l'unico client) e **`CLAUDE.md` §2**, la stessa riga: un modulo che non può nominare la rete legge le
  sue sessioni con `INetworkSessionSource` e le sue prenotazioni ATC con `IAtcBookingSource`, interfacce del nucleo in `Core/Ivao/`
  sopra l'unico client; dal 16 ott 2026 (M4, E10t).
- **`09-design-m4.md` §5.1, §9.3** e la nota `2026-09-30-il-tracker-senza-vid` §3.6 «Per chi chiama (E13a)»: E13a chiama
  `INetworkSessionSource`, non `IIvaoApiClient`; le regole di §3.6 valgono uguali.
- **`10-piano-implementazione-m4.md`**: E10t, scritta in questa PR; E13a dipende anche da E10t.
