# Gli eventi sul lettore dei nomi e sul `Dialog` del nucleo (E4d)

**Data:** 14 ottobre 2026 — fase E4d di M4, PR #263
**Stato:** **decisa**, dalla regola che Carmine ha dato per la stessa situazione sulla #258 (A13f: [la sua risposta][a258], pubblicata
dal master su sua istruzione): un test del nucleo che un handoff chiede di toccare fa ancora cadere `core-guard`, quindi la PR porta una
nota nuova dal primo push, e il guard passa per la sua regola.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: gli eventi usano due meccanismi già decisi, il lettore dei nomi di
[`2026-10-11-un-solo-lettore-dei-nomi`](2026-10-11-un-solo-lettore-dei-nomi.md) (E10m, #252), che al suo §5 aveva scritto questo
passaggio, e il `Dialog` di [`2026-10-11-il-pulsante-di-chiusura-delle-finestre`](2026-10-11-il-pulsante-di-chiusura-delle-finestre.md)
(E10l, #250), che al suo §4 aveva scritto l'altro. Nessuna decisione nuova sui due meccanismi; la nota c'è perché la PR tocca due test
del nucleo.

## 1. Che cosa esce da `MemberNamesTests.cs`, e perché

`tests/IvaoHub.IntegrationTests/MemberNamesTests.cs` è un test del nucleo (E10m): esiste già e non ha «/Events» nel percorso. Ne esce
**solo la riga degli eventi**, come per la formazione in [`2026-10-13-la-formazione-sul-lettore-dei-nomi`](2026-10-13-la-formazione-sul-lettore-dei-nomi.md)
(A13f, #258):

- **dal test gemello**, che confrontava il lettore con le copie dei moduli: il servizio `EventsPeople` e il suo `Assert.Equal`. Dopo il
  passaggio `EventsPeople.NamesAsync` **è** `MemberNames.ReadAsync`, e il confronto proverebbe il lettore con sé stesso. Il test si chiama
  ora `TheReaderAnswersWhatTheToursAnswer`;
- **da `AVidBelowOneIsNeverNamedEvenWhenAUserRowHasIt`**: la riga, con il suo commento, che affermava che gli eventi nominavano ancora un
  utente con un VID sotto uno. Dopo il passaggio non lo nominano più, come vuole la decisione sulla #247 (una persona cancellata non ha
  nome). È uno stato che l'hub non scrive: nessuna pagina e nessuna mail cambia;
- il riassunto della classe dice che gli eventi sono passati sul lettore in E4d.

Resta **la riga dei tour** (una sessione di Carmine), e `AnEmptyNameIsKeptAndEachModuleStillReadsItItsOwnWay`, che legge ancora
`EventsPeople.Member`, che resta con `EventMemberDto`.

È il passaggio che §5 della nota di E10m prevedeva per «gli eventi (una piccola fase di dalberone)».

## 2. Che cosa esce da `Dialog.test.tsx`, e perché

`web/src/shared/ui/Dialog.test.tsx` è un test del nucleo (E10l). La sua guardia ammetteva due file che importavano ancora il `Dialog` di
Atmosphere, la lista `NOT_MOVED_YET`: `EventSlots.tsx` (il dialog di uno slot) e `PrivateSlots.tsx` (quello dell'ora privata, E7). Con
E4d tutti e due importano il `Dialog` del nucleo, e **la lista se ne va**: la guardia chiede ora che nessun file di `web/src` importi da
Atmosphere `Dialog`, `DialogTopRightClose`, `Sheet` o `SheetContent`, senza eccezioni. Il nome del test perde «except the ones waiting to
move». È il passaggio che §4 della nota di E10l prevedeva: la fase degli eventi «toglie i due file da `NOT_MOVED_YET`».

## 3. Che cosa resta fuori

- **La forma del passaggio**: `EventsPeople.NamesAsync` resta, come una riga, invece di andarsene, perché ha i suoi chiamanti nel modulo,
  come `TrainingPeople.NamesAsync` in A13f. La nota di E10m lasciava le due strade.
- **I tour** passano in una sessione di Carmine; con loro esce l'ultima riga di un modulo dal test gemello.

## Da portare nel piano

- `09-design-m4.md` §1.13 (il nome letto dai membri): **dal 14 ott 2026** (M4, E4d) gli eventi lo leggono con `MemberNames.ReadAsync`
  del nucleo; quello che la nota di E10m rimandava a «quando le fasi della formazione e degli eventi l'avranno adottato», ora per tutti e
  due.
- `00-piano-di-progettazione.md` §16, la riga del lettore dei nomi (nota di E10m): gli eventi sono passati; restano i tour.
- `00-piano-di-progettazione.md` §8.3, il `Dialog` del nucleo (nota di E10l): nessuna schermata monta più il `Dialog` di Atmosphere.
  Versione e changelog.
- `10-piano-implementazione-m4.md`: E4d, scritta in questa PR.

[a258]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/258#issuecomment-6146758656
