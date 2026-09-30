# La distanza fra due aeroporti nel nucleo (E10e)

**Data:** 30 settembre 2026 — fase E10e di M4, PR del nucleo
**Stato:** **scelta tecnica**, per dare forma nel codice a una decisione già presa: che il calcolo sul cerchio massimo passi nel
nucleo lo dice il piano di M4 (`10-piano-implementazione-m4.md`, E0 «Trovato» n.10, ed E10e), perché la distanza nelle voci del PIREP
di supporto e la regola di award `MinLegDistance` la chiedono al nucleo (design `09-design-m4.md` §1.8, §1.9, §1.13; nota
`2026-09-29-dopo-l-evento-e-gli-award` §2.4 e §4, decisa sulla #180). Nessuna domanda su una decisione nuova; **uno scostamento dalla
lettera del piano** (§3: il calcolo sta in `Core/Airspace/`, non accanto a `AirportDirectory.cs` in `Core/Ivao/`) e **una richiesta a
Carmine** (§5): la copia dei tour resta com'è in questa PR, e la sostituisce una sua sessione, come `memberName` dopo A12a di M3.
**Regola applicata:** `CLAUDE.md` §2 («a piece used in two places is written once») e §5, caso **(b)**: un pezzo del modulo dei tour
passa nel nucleo perché un secondo modulo ne ha bisogno, e un modulo non ne referenzia un altro. È una PR del nucleo, prima di E14a ed
E14b che la usano (`CLAUDE.md` §0 regola 6).

## 1. Che cosa serve, e perché il modulo non ne fa a meno

- **Gli eventi misurano una tratta.** La verifica di un PIREP di supporto scrive, per ogni volo dichiarato, la distanza in miglia fra
  i suoi due aeroporti (design §1.8, §5.3; E14a), e la regola di award `MinLegDistance` premia «una tratta più lunga di x (dagli
  aeroporti del nucleo)» (design §1.9; E14b). È la distanza dei tour: fra i due aeroporti, non le miglia volate.
- **Il calcolo c'è solo nei tour** (`GreatCircle` in `src/IvaoHub.Modules.FlightOps/Legs/GreatCircle.cs`, T7a di M2, preso da
  Toursystem con i suoi test), e un modulo non ne referenzia un altro (`CLAUDE.md` §2;
  `ArchitectureTests.AModuleDependsOnTheCoreAndOnNoOtherModule`). Una copia negli eventi sarebbe lo stesso pezzo scritto due volte,
  e due numeri per la stessa tratta il giorno in cui una delle due cambiasse.
- **Le coordinate sono già del nucleo**: `IAirportDirectory.FindAsync` dà gli aeroporti con latitudine e longitudine (T7a di M2).
  Manca solo l'aritmetica.

## 2. Che cosa c'è, letto nel codice (`main` a `c107c98`)

1. **La copia dei tour**: `GeoPoint` (latitudine e longitudine in gradi), `GreatCircle.DistanceNm` (l'haversine sul raggio medio,
   3440,0647948164 miglia, cioè 6371 km / 1,852, con l'arcoseno della radice di `Math.Min(1, h)`) e `GreatCircle.DistanceNmRounded`
   (al decimo, con `Math.Round(decimal, 1)`: una metà va al decimo pari). Nello stesso file `EstimatedTime`, che è dei tour (design
   M2 §1.5, con le loro impostazioni) e resta lì.
2. **Chi la usa**: `LegBook.ApplyAsync` (congela sulla leg le coordinate dei due aeroporti e la misura, `fo_legs.distance_nm`; riga
   101), `PirepSubmission.OpenFlightAsync` (la distanza del volo di un tour `Open`; riga 912), `TrackChecks` (distanze e velocità dei
   controlli sulle tracce: punti di una traccia, non aeroporti; righe 96–101, 370, 465–481 e 547), `Leg.Departure` e `Leg.Arrival`.
   Nei test: `LegTests` (unità, due fatti sulla distanza) e `PirepTests.Checks` (integrazione, per costruire una traccia; riga 208).
3. ⚠️ **Tre file importano sia `IvaoHub.Core.Ivao` sia `IvaoHub.Modules.FlightOps.Legs`** e usano `GeoPoint` o `GreatCircle`:
   `Checks/TrackChecks.cs` (righe 3–4) e `Pireps/PirepSubmission.cs` (righe 7 e 13) dei tour, e
   `tests/IvaoHub.IntegrationTests/PirepTests.Checks.cs` (righe 3 e 7), un test del maintainer. **Con gli stessi nomi nel namespace
   `IvaoHub.Core.Ivao` la build dei tour cade** — provato: `error CS0104: 'GeoPoint' è un riferimento ambiguo tra
   'IvaoHub.Modules.FlightOps.Legs.GeoPoint' e 'IvaoHub.Core.Ivao.GeoPoint'` su `TrackChecks.cs` —, e sono file che questa PR non
   tocca (`CLAUDE.md` §0 regola 2). Nessun file che usa quei nomi importa `IvaoHub.Core.Airspace` (grep del 30 settembre).
4. **Nel browser** `web/src/shared/ui/greatCircle.ts` disegna la linea di un cerchio massimo sulla mappa e non misura niente; il suo
   commento dice che le distanze sono del server («`GreatCircle` in the module»).

## 3. La forma nel codice

- **`src/IvaoHub.Core/Airspace/GreatCircle.cs`**, namespace `IvaoHub.Core.Airspace`: `GeoPoint`, `GreatCircle.DistanceNm` e
  `GreatCircle.DistanceNmRounded`, **con gli stessi nomi, le stesse firme e lo stesso codice** della copia dei tour — la costante, la
  formula, il `Min`, l'arrotondamento. Così il passaggio dei tour al nucleo è un `using` e la copia tolta (§5), e nessun numero cambia
  (il test gemello, §4). Un modulo chiede a `IAirportDirectory.FindAsync` dove sono i due aeroporti e misura con `GreatCircle`.
- **Perché in `Airspace/` e non accanto a `AirportDirectory.cs`** in `Core/Ivao/`, come dice la lettera di E10e («accanto alle
  coordinate di `IAirportDirectory`»):
  1. la domanda di `CLAUDE.md` §3, «does this name IVAO?»: no. È aritmetica su una sfera, non un dato della rete; e la geometria del
     nucleo che non è di IVAO sta già in `Airspace/` — i contorni dei FIR, che vivono lì «and not under `Ivao/`» perché «it is not
     IVAO data» (`FirBoundary.cs`);
  2. in `IvaoHub.Core.Ivao`, con gli stessi nomi, la build dei tour cade (§2, punto 3); con nomi diversi il passaggio dei tour
     cambierebbe ogni riga che li nomina (§7).

  Resta «accanto alle coordinate» nel senso che conta: le due metà della distanza fra due aeroporti, dove sono e quanto distano, sono
  tutte e due del nucleo. È uno **scostamento** dalla lettera del piano, scritto anche in «Com'è andata» di E10e.
- **Niente altro**: nessuna registrazione (è una funzione statica, come nei tour), nessuna migrazione, nessun endpoint, nessun cambio
  nel browser, nessuna parola.

## 4. I test

`tests/IvaoHub.UnitTests/GreatCircleTests.cs`, nuovo:

- **le domande di `LegTests` al nucleo, con le stesse risposte**: Fiumicino–Linate 253,9 miglia, Orio al Serio–Courchevel 130,1,
  Fiumicino–Heathrow 779,6, Fiumicino–JFK 3707,2, da polo a polo 10807,3, sessanta miglia per grado all'equatore; nessuna direzione,
  zero senza NaN, Fiumicino–Urbe fra 12 e 20, il decimo con la scala 1. Sono gli aeroporti veri dei test dei tour, con le loro
  coordinate (i primi tre sono anche quelle degli aeroporti `XFA` dei loro test d'integrazione);
- **i bordi**: due antipodi sono mezza circonferenza (a 0°, 8°, 12° e 34° di latitudine, su due meridiani); un grado attraverso
  l'antimeridiano è un grado;
- **il test gemello**: il nucleo e la copia dei tour sulle stesse domande — gli aeroporti dei test, i bordi e una griglia del globo
  ogni 15° di latitudine e 30° di longitudine, 189 punti, ognuno con ognuno (35.721 coppie) — danno **lo stesso double, bit per bit, e
  lo stesso decimo**. Legge la copia dei tour con due alias (`ToursCircle`, `ToursPoint`): è la prova che il passaggio dei tour al
  nucleo non sposta nessuna distanza di una leg né di un PIREP, e se ne va con la copia (§5).

`LegTests` e `PirepTests.Checks` non cambiano.

## 5. La copia dei tour, e la richiesta a Carmine

- **Questa PR non la tocca** (`CLAUDE.md` §0 regola 2; `core-guard`): come `memberName` dopo A12a (nota
  `2026-09-29-la-persona-cancellata-nel-nucleo` §3, punto 5), la sostituisce **una sessione di Carmine**. Fino ad allora l'hub ha due
  copie con gli stessi numeri, e il test gemello le tiene uguali.
- **Che cosa fa quel passaggio**: in `src/IvaoHub.Modules.FlightOps/Legs/GreatCircle.cs` toglie `GeoPoint` e `GreatCircle` (resta
  `EstimatedTime`, dei tour); aggiunge `using IvaoHub.Core.Airspace;` in `Legs/Leg.cs`, `Legs/LegBook.cs`, `Checks/TrackChecks.cs`,
  `Pireps/PirepSubmission.cs`, `tests/IvaoHub.UnitTests/LegTests.cs` e `tests/IvaoHub.IntegrationTests/PirepTests.Checks.cs`; toglie
  da `GreatCircleTests` il test gemello e i due alias, che non hanno più niente da confrontare (i due fatti sulla distanza di
  `LegTests` ripetono allora quelli di `GreatCircleTests`: se tenerli è suo); il commento di `web/src/shared/ui/greatCircle.ts` dice
  «in the core». **Nessuna migrazione**: `fo_legs.distance_nm` ha già i numeri del nucleo (il test gemello).
- **La richiesta** è andata a Carmine in [un commento sulla #206][r206], con la raccomandazione: il passaggio fatto da una sua
  sessione, prima di E14a se gli torna comodo. Nessun codice di questa PR ne dipende: il passaggio dei tour si fa quando lui vuole, e
  fino ad allora la «Fatta quando» di E10e («i tour e il nucleo hanno un calcolo solo») è vera a metà: un calcolo solo per i numeri,
  due copie nel codice.

[r206]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/206#issuecomment-5916051005

## 6. Trovato scrivendo il codice

1. ⚠️ **Il namespace**: §2, punto 3.
2. **Agli antipodi `h` passa 1 di un'unità nell'ultima cifra**, ma l'arcoseno non vede mai più di 1: `Math.Sqrt` riporta 1 più
   un'unità esattamente a 1. Misurato con uno script su .NET 10 (Windows): su due milioni di coppie di antipodi a caso `h` passa 1 in
   77.455 (a 8°, 12° e 34° fra i gradi interi), e **anche senza `Math.Min(1, h)` nessun NaN**, né lì né su una griglia di centesimi di
   grado. Il `Min` resta com'è nei tour: è lo stesso codice, e una guardia per una libreria matematica che arrotondi altrimenti. Il
   test degli antipodi afferma mezza circonferenza, qualunque cosa ce la porti.
3. **Un test al decimo non vede un raggio cambiato**: con 3440,065 al posto di 3440,0647948164 le prove sui valori passano tutte, e
   cade solo il test gemello (provato). È il perché del test gemello: senza, il passaggio dei tour potrebbe spostare dei numeri senza
   che nessun test lo dica.

## 7. Alternative scartate

| Alternativa | Perché no |
|---|---|
| Il calcolo in `Core/Ivao/`, accanto a `AirportDirectory.cs` | non nomina IVAO (`CLAUDE.md` §3); e con gli stessi nomi la build dei tour cade (§2, punto 3) |
| Nomi diversi (`GeoPosition`, `GreatCircleDistance`) in `Core/Ivao/` | due nomi per la stessa cosa, e il passaggio dei tour non è più un `using`: cambia ogni riga che li nomina |
| Una cartella nuova (`Core/Geography/`) | una cartella per un file, accanto a quella che tiene già la geometria del nucleo che non è di IVAO |
| Un metodo `IAirportDirectory.DistanceNmAsync(from, to)` | una lettura del database per coppia, dove chi misura legge già gli aeroporti con un `FindAsync` solo; e dei tre usi dei tour uno solo misura due aeroporti al momento (le leg congelano le coordinate, le tracce misurano punti che non sono aeroporti) |
| Un `Location` su `AirportDto`, il punto pronto per il calcolo | cambia un DTO che l'API serve (`/api/reference/airports`) e che il tipo generato del browser legge; quello che risparmia è un pattern di una riga (`is { Latitude: { } …, Longitude: { } … }`), non un meccanismo |
| Una copia negli eventi | lo stesso pezzo scritto due volte (§1) |
| Passare i tour al nucleo in questa PR | `CLAUDE.md` §0 regola 2 e `core-guard`: è di una sessione di Carmine (§5) |
| Solo le prove sui valori, senza il test gemello | sono al decimo, e non vedono un raggio cambiato (§6, punto 3) |

## Da portare nel piano

- `00-piano-di-progettazione.md` §9.1, la riga «Confini dei FIR (M2)»: **dal 30 set 2026** (M4, E10e) il nucleo ha anche **la
  distanza sul cerchio massimo** fra due punti (`GreatCircle` e `GeoPoint`, `Core/Airspace/`), che un modulo usa con le coordinate di
  `IAirportDirectory`: quella dei tour, con i loro numeri. Versione e changelog.
- `09-design-m4.md` §1.13 («Postazioni, aeroporti, aerei, distanze») e §1.9 (`MinLegDistance`): le distanze con `GreatCircle` del
  nucleo.
- `10-piano-implementazione-m4.md`, E0 «Trovato» n.10 ed E10e: il calcolo è in `Core/Airspace/`, non accanto a `AirportDirectory.cs`
  (§3); i tour lo useranno dopo il passaggio di una sessione di Carmine (§5).
- `05-design-m2.md` §0.5 (la riga «Distanza GCD») e `06-piano-implementazione-m2.md` (T7a): la GCD dei tour è quella del nucleo,
  quando la sessione di Carmine avrà tolto la copia.
- Se Carmine lo vuole, una riga nella tabella di `CLAUDE.md` §2: la distanza fra due aeroporti è `GreatCircle` del nucleo con le
  coordinate di `IAirportDirectory`, mai una copia.
