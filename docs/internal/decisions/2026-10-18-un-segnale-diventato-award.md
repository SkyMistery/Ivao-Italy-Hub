# Un segnale diventato award: una domanda del nucleo (E10x)

**Data:** 18 ottobre 2026 — fase E10x di M4, PR del nucleo (#299)
**Stato:** **decisa** da Carmine: il punto 3 della sua risposta sulla #293 (E14b, le regole di award), data in chat al master il 18
ottobre 2026 e pubblicata sulla PR su sua istruzione ([risposta][a293]) — «a small core method, "did this source row give an award?", by
`source_module` and the source ids, locking the signal rows when called inside a transaction, in its own core pull request with a new
note (case (b)), before E14b uses it». La forma qui sotto è quella che E14b ha proposto, controllata nel codice; quello che il codice ha
detto in più (§2.4: «diventato award» è esattamente `Handled`, anche dopo un'assegnazione cancellata) il revisore l'ha portato a
Carmine, che ha risposto **(A)**, va bene così ([risposta][a299]).
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: la coda degli award è del nucleo (le proiezioni, `IProjectable`, e
`cms_award_signals`), e la si estende con la sola domanda che un modulo le fa, invece di lasciare al modulo una query sua sulla tabella.
È una PR del nucleo, prima di E14b che la usa (`CLAUDE.md` §0 regola 6): nessun uso negli eventi qui.

[a293]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/293#issuecomment-6248618376
[r293]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/293#issuecomment-6248457744
[r299]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/299#issuecomment-6251435462
[a299]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/299#issuecomment-6251691217

## 1. Che cosa serve

- **E14b** (`m4/e14b-award-rules`, #293) scrive, accettando un PIREP, una riga per regola soddisfatta (`evt_report_awards`), che proietta
  un segnale nella coda; e da lì la decisione sul PIREP **non cambia più una volta che un award è stato assegnato** (il limite di Carmine
  sulla #287). Per saperlo legge `cms_award_signals` con due query sue (`ReportAwards.AssignedAsync`, una `SELECT … FOR UPDATE` scritta a
  mano per ogni sorgente, e `AssignedAmongAsync`, una lettura LINQ per la lista).
- **Il punto 1 della revisione** ([osservazioni][r293]): rifiutando il PIREP, E14b cancella le sue righe, il nucleo ritira solo i segnali
  `Pending`, e uno `Dismissed` resta orfano — riaperto e assegnato, darebbe un award da un PIREP rifiutato. Il rimedio suggerito è tenere
  le righe e proiettare uno snapshot vuoto finché il PIREP non è accettato; allora la domanda va fatta su **ogni** segnale che il PIREP ha
  mai prodotto, e Carmine l'ha voluta nel nucleo.
- **La domanda è generale**: ogni modulo che proietta segnali e vuole fermare qualcosa dopo un award se la porrebbe con la stessa query.
  I tour leggono la tabella anche loro (`TourRetentionJob`, «c'è ancora un segnale in attesa?»): è un'altra domanda, e FlightOps non si
  tocca (§5).

## 2. Che cosa c'è, letto nel codice (`main` a `01fdbed5`)

1. **`Pending`**: lo scrive il writer delle proiezioni (`ProjectionWriter.ApplyAwardSignals`) quando una riga indica un membro; un segnale
   in attesa segue la riga (l'award proposto) e sparisce quando la riga smette di indicarlo. Una volta guardato da qualcuno, non lo si
   riscrive più.
2. **`Handled`**: lo scrive **solo** l'assegnazione che risponde al segnale (`AwardEndpoints.BeforeAssigningAsync`, nello stesso
   salvataggio della riga di `hub_award_assignments` con `SignalId`), e solo da `Pending`. La coda non lo accetta a mano
   (`AwardSignalWriteDtoValidator`), e un segnale `Handled` non si sposta più (`BeforeMovingASignal`).
3. **`Dismissed`**: lo mette a mano chi assegna, e lo può rimettere `Pending`.
4. **Quindi «la riga ha dato un award» è esattamente `Status == Handled`**, senza leggere il registro: un `Handled` nasce solo da
   un'assegnazione, e il registro (`hub_award_assignments`) **non è mappato** nei contesti dei moduli (`ModuleDbContext.MapProjectionTables`
   mappa la coda, non il registro), né può esserlo per una join fra contesti. Un'assegnazione **cancellata** dal registro (il registro
   permette la cancellazione) lascia il segnale `Handled`, e nessun codice lo riporta indietro: la domanda continua a rispondere sì. È
   coerente con la coda, che è «a record of what was decided» (`ProjectionWriter`); se un giorno si volesse che togliere un'assegnazione
   riapra la decisione, sarebbe una modifica della coda (il segnale che torna `Pending`), non di questa lettura.
   **Decisa da Carmine, (A)** (18 ottobre 2026, in chat al master, «va bene la A», pubblicata sulla PR su sua istruzione:
   [risposta][a299]): un'assegnazione cancellata dal registro lascia il suo segnale `Handled`, e va bene così — un award dato e poi
   tolto è comunque una storia chiusa, e il PIREP da cui viene non si decide di nuovo. Il registro non cambia; se mai servirà, si
   corregge a mano.
5. **La corsa con un'assegnazione**: l'assegnazione legge il segnale senza lock, poi lo aggiorna; se nel frattempo la riga del segnale è
   sparita, l'`UPDATE` tocca 0 righe, `DbUpdateConcurrencyException`, 409 (`DomainExceptionHandler`). Basta che chi chiede tenga il lock
   sulle righe del segnale fino alla fine della sua transazione.
6. **L'indice**: `cms_award_signals` ha l'unico `(source_module, source_id, vid)`, e la lettura per sorgente lo usa.

## 3. La forma

In `src/IvaoHub.Core/Content/AwardSignalQueries.cs`, accanto a `AwardSignal.cs`:

```csharp
public static class AwardSignalQueries
{
    public static Task<IReadOnlySet<string>> AssignedAsync(
        DbContext context, string sourceModule, IReadOnlyCollection<string> sourceIds, CancellationToken cancellationToken = default);
}
```

1. **La risposta**: i source id fra quelli chiesti, del modulo chiesto, di cui un segnale è `Handled`. Un id senza segnale, uno `Pending`,
   uno `Dismissed`, o lo stesso id di un altro modulo non ci sono. Gli id doppi contano una volta; nessun id, nessuna query e un insieme
   vuoto.
2. **Il contesto è quello di chi chiama**: qualunque contesto che mappi la coda — quello del hub, o quello di un modulo — perché il lock
   viva nella **sua** transazione, sulla sua connessione. Un servizio con il contesto del hub avrebbe un'altra connessione, e il lock non
   proteggerebbe niente.
3. **Dentro una transazione** (`context.Database.CurrentTransaction` non nullo): una sola `SELECT source_id, status FROM cms_award_signals
   WHERE source_module = … AND source_id IN (…) FOR UPDATE`, con parametri. Si bloccano **tutte** le righe del segnale di quelle
   sorgenti, di qualunque stato, perché quella che un'assegnazione sta per toccare è una `Pending`. InnoDB prende i lock **mentre scorre
   l'indice** `(source_module, source_id, vid)`, nel suo ordine, prima di qualunque ordinamento: due chiamanti li prendono nello stesso
   ordine. (La prima versione aveva un `ORDER BY id` «perché due chiamanti non le prendano incrociate»: non decideva niente, e la revisione
   l'ha detto; tolto, §7.) Una lettura con lock legge l'**ultima versione committata** sotto qualunque isolamento, anche REPEATABLE READ,
   dove una lettura semplice dopo il lock potrebbe vedere la fotografia vecchia della transazione: per questo stato e id arrivano dalla
   stessa istruzione. **Ciò che blocca, invece, dipende dall'isolamento**: si chiama sotto READ COMMITTED (punto 6). Il lock resta fino alla fine della transazione di chi chiama: un'assegnazione nello stesso istante
   passa prima (e la si legge `Handled`) o dopo (e trova quello che chi chiama ha lasciato), mai in mezzo.
4. **Fuori da una transazione**: una lettura LINQ semplice, senza tracking, che non aspetta nessuno (una `FOR UPDATE` in autocommit
   aspetterebbe un lock per poi lasciarlo subito).
5. **Le colonne del tipo non mappato** seguono la convenzione del hub (`source_id`, `status`): la prima prova le aveva con un alias, e EF
   le cercava con il nome della convenzione.
6. **Si chiama sotto READ COMMITTED** (come le transazioni degli eventi), e il commento del metodo lo dice: lì non ci sono lock sui buchi,
   una sorgente senza segnale non blocca niente, ed è giusto, perché un segnale non ancora committato non si può assegnare. Sotto
   REPEATABLE READ (il predefinito di MariaDB, e `BeginTransactionAsync()` senza argomenti) l'`IN` sul prefisso dell'indice prende anche i
   lock sui buchi, e due chiamanti su sorgenti ancora senza segnale possono andare in deadlock (1213) quando poi scrivono tutti e due i
   loro segnali.
7. **Conta solo la transazione del contesto**: una transazione su un altro contesto, una `TransactionScope` ambientale, o una chiamata da un
   `BeforeSave` del CRUD generico (che gira prima che l'intercettore apra la sua transazione) ricadono nella lettura semplice, senza lock
   e senza una parola. Il commento del metodo lo dice.

## 4. Alternative scartate

| Alternativa | Perché no |
|---|---|
| Lasciare le due query nel modulo | è la risposta di Carmine; e un secondo modulo con la stessa domanda scriverebbe la terza copia |
| Un servizio registrato, con il contesto del hub | il lock deve stare nella transazione di chi chiama, sulla sua connessione (§3.2) |
| Un parametro `locking`, come in E14b | lo decide la transazione: un lock fuori transazione non serve, e una lettura senza lock dentro una transazione lascerebbe passare l'assegnazione in mezzo |
| Leggere anche il registro, «Handled e un'assegnazione che c'è ancora» | i moduli non lo mappano, e un'assegnazione cancellata non riapre il segnale (§2.4) |
| Un `bool` per una sorgente, o gli stati per id | la domanda è sì/no; un insieme serve a una sorgente come a una lista (le due chiamate di E14b) |
| Una query LINQ con `FOR UPDATE` aggiunto da un intercettore o da un tag | un meccanismo nuovo per una query |

## 5. Che cosa si tocca

Tutto del nucleo, ed è il perché di questa nota (`core-guard`):

- `src/IvaoHub.Core/Content/AwardSignalQueries.cs` (nuovo);
- `tests/IvaoHub.IntegrationTests/AwardSignalQueriesTests.cs` (nuovo, integrazione, cinque casi, con il modulo di test e le persone di
  `AwardsTests`): solo `Handled` e solo del modulo, dal contesto del hub e da quello del modulo, con e senza transazione; nessun id; dentro
  una transazione la chiamata aspetta un'assegnazione che tiene la riga e la legge; al contrario, un'assegnazione vera (HTTP) aspetta chi
  chiama, e dopo il ritiro del segnale risponde 409 senza award; fuori da una transazione nessuna attesa. **Nessun test esistente
  cambiato.**
- **Nessuna riga** di un modulo; niente nel browser, nessuna migrazione. `TourRetentionJob` (FlightOps) legge la tabella per un'altra
  domanda e resta com'è: adottare qui una sua domanda è del maintainer.

## 6. Per chi chiama (E14b)

- La decisione su un PIREP, nella sua transazione (READ COMMITTED, dopo il lock del PIREP):
  `await AwardSignalQueries.AssignedAsync(database, EventsModule.ModuleKey, sources, cancellationToken)`, con `sources` le
  `EventReportAward.SourceOf(id)` di **tutte** le righe del PIREP, che restano anche quando il PIREP è rifiutato (il punto 1 della
  revisione); non vuoto, la decisione si rifiuta.
- La lista dei PIREP, fuori transazione: la stessa chiamata con le sorgenti della pagina, poi da sorgente a PIREP.
- Le due query di `ReportAwards` (la `SqlQuery` scritta a mano e la LINQ) se ne vanno.

## 7. Dopo la revisione (#299)

Le [osservazioni del revisore][r299] (18 ottobre 2026): approvabile, tre commenti da correggere, nessuno bloccante, tutti fatti:

1. **L'isolamento**: il commento del metodo diceva «under any isolation level», vero per quello che risponde, non per quello che blocca;
   ora dice «call it under READ COMMITTED», con il perché (§3.6).
2. **L'ordine dei lock**: `ORDER BY id` non lo decideva — InnoDB blocca scorrendo l'indice `(source_module, source_id, vid)`, prima di
   ordinare. Corretti il commento e §3.3; e tolto l'`ORDER BY`, che ordinava solo un insieme.
3. **La lettura semplice in silenzio**: una riga nel commento del metodo per la `TransactionScope` ambientale e il `BeforeSave` del CRUD
   (§3.7); il caso dell'altro contesto era già nell'handoff.

E il punto portato a Carmine, l'assegnazione cancellata, è **deciso (A)** (§2.4, [risposta][a299]).

## Da portare nel piano

- **§9.1** (gli award) e **§16** (la riga delle proiezioni, «award signal»): se un segnale di una riga è diventato award lo dice il
  nucleo, `AwardSignalQueries.AssignedAsync` (`Core/Content/`), sul contesto di chi chiama, con il lock dentro una sua transazione READ
  COMMITTED; mai una query del modulo su `cms_award_signals`; «diventato award» è `Handled`, anche se l'assegnazione è stata poi
  cancellata (Carmine, (A), sulla #299); dal 18 ott 2026 (M4, E10x).
- **`CLAUDE.md` §2**, la riga «Calendar entry, search index row, award signal…» (file del maintainer, la proposta è sua): «whether a
  source row's signal became an award: the core's `AwardSignalQueries.AssignedAsync`, never a query of the module»
  (`decisions/2026-10-18-un-segnale-diventato-award.md`).
- **`09-design-m4.md` §5.4** e la nota di E14b `2026-10-17-le-regole-di-award`: la decisione su un PIREP chiede al nucleo.
- **`10-piano-implementazione-m4.md`**: E10x, scritta in questa PR; E14b dipende anche da E10x.
