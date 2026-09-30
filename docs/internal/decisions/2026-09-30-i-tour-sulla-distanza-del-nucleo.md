# I tour sulla distanza del nucleo

**Data:** 30 settembre 2026 — dopo la fase E10e di M4 (#206), PR di una sessione di Carmine
**Stato:** **esecuzione di una decisione già presa**: Carmine ha deciso sulla #206 ([risposte][a206]) che una sua sessione porti i tour
sul `GreatCircle` del nucleo dopo l'unione di E10e. La ricetta è quella della nota
`2026-09-30-la-distanza-fra-due-aeroporti-nel-nucleo` §5, seguita alla lettera; **una sola scelta** resta a questa PR (§2, punto 3), e
la raccomandazione era già scritta lì.
**Regola applicata:** `CLAUDE.md` §2 («a piece used in two places is written once»): dal merge di E10e l'hub aveva due copie dello
stesso calcolo, tenute uguali da un test gemello. Ora ne ha una.

## 1. Che cosa cambia

- **La copia dei tour se ne va.** Da `src/IvaoHub.Modules.FlightOps/Legs/GreatCircle.cs` escono `GeoPoint` e `GreatCircle`; resta
  `EstimatedTime`, che è dei tour (design M2 §1.5), e il file prende il suo nome: `Legs/EstimatedTime.cs`.
- **I tour misurano con il nucleo**: `using IvaoHub.Core.Airspace;` in `Legs/Leg.cs`, `Legs/LegBook.cs`, `Checks/TrackChecks.cs`,
  `Pireps/PirepSubmission.cs` e `tests/IvaoHub.IntegrationTests/PirepTests.Checks.cs` (la lista della nota di E10e, confermata da un
  grep). In `TrackChecks.cs` e `PirepTests.Checks.cs` il `using IvaoHub.Modules.FlightOps.Legs;` non serviva più ad altro, e l'analisi
  (`IDE0005`, un errore nella build) lo toglie.
- **Nel browser** il commento di `web/src/shared/ui/greatCircle.ts` dice che le distanze sono del server, «`GreatCircle` in the core».
- **Nessun numero cambia, nessuna migrazione**: il codice del nucleo è lo stesso della copia, e il test gemello di E10e lo ha provato
  bit per bit su 35.721 coppie; `fo_legs.distance_nm` ha già i numeri del nucleo.

## 2. I test

1. **Il test gemello** (`GreatCircleTests.TheCoreAndTheToursGiveTheSameAnswers`) e i due alias `ToursCircle` e `ToursPoint` se ne
   vanno: non c'è più niente da confrontare (nota di E10e §5).
2. `LegTests` non misura più niente e non nomina più `GeoPoint`: non gli serve il `using` del nucleo che la nota di E10e prevedeva.
3. **I due fatti sulla distanza di `LegTests`** (`TheGreatCircleIsRightOnThreeOrdersOfMagnitude`,
   `TheGreatCircleHasNoDirectionNoNaNAndATenthOfAMile`) e le sei coordinate che usavano **se ne vanno**. La nota di E10e lasciava la
   scelta a Carmine, con la raccomandazione di toglierli, e questa PR la applica: fanno le stesse domande, con le stesse risposte, di
   `GreatCircleTests.TheDistanceBetweenTwoKnownAirportsIsRightOnThreeOrdersOfMagnitude` e `TheDistanceHasNoDirectionNoNaNAndATenthOfAMile`,
   che nel nucleo le hanno già tutte (Fiumicino–Urbe compreso). Tenerli sarebbe lo stesso test scritto due volte sullo stesso codice.
   Se Carmine li vuole, tornano con un `using`.

Nessun altro test cambia.

## Da portare nel piano

- `05-design-m2.md` §0.5, la riga «Distanza GCD»: la distanza dei tour è `GreatCircle` del nucleo (`Core/Airspace/`), dal 30 set 2026;
  la copia del modulo non c'è più.
- `06-piano-implementazione-m2.md`, T7a: il calcolo scritto in T7a vive nel nucleo da E10e di M4, e i tour lo usano da questa PR;
  i suoi test sono in `GreatCircleTests`.
- `10-piano-implementazione-m4.md`, E10e: la «Fatta quando» («i tour e il nucleo hanno un calcolo solo») è vera anche nel codice.
- **La riga nella tabella di `CLAUDE.md` §2** decisa da Carmine sulla #206 ([risposte][a206]), che aggiunge il master: la distanza fra
  due aeroporti è `GreatCircle` del nucleo con le coordinate di `IAirportDirectory`, mai una copia.

[a206]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/206#issuecomment-5916695685
