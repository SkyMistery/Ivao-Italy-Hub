# Il ritiro di chi ha mandato la riga (E10h)

**Data:** 7 ottobre 2026 — fase E10h di M4, PR del nucleo (#232)
**Stato:** **Proposta** — la domanda a Carmine è un commento sulla #232 (§6). Il codice è nella PR, e la PR non si unisce prima della
sua risposta; E6a, che lo usa, unisce questo branch e va in coda dopo la #232.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: si estende la rete dell'interceptor (`HubSaveChangesInterceptor`, il guardiano
`EnsureWriteIsAllowed`), che già lascia al membro **creare** la riga che manda (`ISubmittedByMembers`, M1) e **cambiarla** finché è sua
(M2, T11); nessun meccanismo nuovo, nessuna scrittura «come il sistema». È una PR del nucleo, prima del codice del modulo che la usa
(`CLAUDE.md` §0 regola 6). L'ha trovata la sessione di E6a leggendo il guardiano prima di scrivere il ritiro, e dalberone il 7 ottobre
2026 ha scelto, fra le tre strade offerte — una fase del nucleo prima (raccomandata), la domanda a Carmine prima, E6a senza il ritiro —,
**la fase del nucleo**.

## 1. Che cosa serve, e perché nessun meccanismo lo copre

- **Il design M4**, deciso da Carmine sulla #180: la prenotazione `evt_bookings` è `ISubmittedByMembers` e `IHasStakeholder` (il
  pilota), area `EventBookings`, `[Audited]`, e «**Ritirare cancella la riga**; il registro di chi ha fatto che cosa è l'audit del
  nucleo» (§1.6); «il pilota ritira fino all'EOBT: la riga si cancella e lo slot torna libero» (§3.6); le righe dei membri «il membro le
  crea e le ritira, e non le decide» (§1.1). La nota `2026-09-29-gli-slot-e-le-prenotazioni` lo ripete (§2.6), e tiene l'unicità nel
  database con l'indice univoco su `slot_id` (§2.7).
- **Il guardiano non lo permette.** Una riga `ISubmittedByMembers` la crea chiunque (nota `2026-09-06-una-riga-scritta-da-fuori`: solo
  alla creazione; «il resto — leggere la coda, muovere lo stato, cancellare — continua a chiedere il permesso sul dipartimento»), e
  l'interessato la cambia finché resta sua (nota `2026-09-23-il-pirep` §5: «Cancellarla resta del dipartimento»). Un pilota senza
  `EventBookings.Edit` riceve `ForbiddenDomainException`, e l'endpoint un 403: la sessione di E6a l'ha misurato, il suo test del ritiro
  (`EventsBookingsTests.APilotWithdrawsUntilTheOffBlockAndTheSlotIsFreeAgain`) cadeva con 403 (`Expected: NoContent`,
  `Actual: Forbidden`) prima di unire questo branch.
- **Le strade che la nota del PIREP ha già chiuso** (§5): scrivere la cancellazione «come il sistema», senza utente, è l'aggiramento
  che la rete esiste per impedire; una riga che non fosse `IOwnedByDepartment` toglierebbe la rete e il modo in cui l'handler legge la
  riga; un grant ai piloti darebbe loro `EventBookings.Edit`, cioè il lavoro dello staff.
- **Chi altro lo troverà**: E11a (la disponibilità di un controllore, che «la ritira fino alla chiusura», design §4.2) ed E16
  (l'iscrizione a un evento in presenza: il membro «si ritira fino all'inizio dell'evento», §4-bis.2), se le loro fasi diranno che
  ritirarle vuol dire cancellarle: a loro basterà il segno.

## 2. Che cosa c'è, letto nel codice (`main` a `e9702b2`)

1. **Il guardiano** (`src/IvaoHub.Core/Data/HubSaveChangesInterceptor.cs`, `EnsureWriteIsAllowed`, righe 410–474), in quest'ordine:
   l'anonimo (l'installazione), il superadmin e una riga senza dipartimento passano; una riga `ISubmittedByMembers` nuova passa; una
   `ISubmittedByMembers` e `IHasStakeholder` cambiata passa se l'interessato è chi scrive prima (dai valori originali) e dopo, negli
   stessi dipartimenti — e il commento chiude «Deleting is still the department's»; il partecipante di una `IHasParticipants`; le
   alternative (`IsWrittenWithAnAlternative`: all'eliminazione solo un permesso `OnlyForAssignee` con `AlsoOnDeletion`, e mai per
   l'interessato); infine `{Area}.Edit` sui dipartimenti della riga. Il commento di `ISubmittedByMembers` (`DomainContracts.cs`) dice lo
   stesso.
2. **Chi usa la regola oggi**: il PIREP dei tour (si ritira cambiando lo stato, `PirepStatus.Withdrawn`: la riga resta, è un registro) e il
   training di M3 (il trainee lo manda e lo cambia; si annulla con uno stato, `Cancelled`). `ContactMessage` è `ISubmittedByMembers` con
   `IHasParticipants`. Nessuna di queste righe la cancella il membro.
3. **L'unico handler non conosce `ISubmittedByMembers`**: le eccezioni per chi manda una riga sono tutte e sole del guardiano. La DELETE
   del motore (`MapCrud`) chiede all'handler la policy di scrittura, quindi un membro non elimina mai da lì; i suoi verbi sono endpoint
   del suo flusso (piano §16 punto 6, «flusso di un membro»), come il ritiro e la correzione del PIREP.
4. **All'avvio** `HubPipeline.InitializeAsync` fa girare `PermissionCatalog.VerifyAlternatives` sul modello di ogni contesto: le sei
   cose che la nota `2026-09-30-il-controllo-all-avvio-rinforzato` rifiuta.

## 3. La forma nel codice

### 3.1 L'entità lo dice: `[WithdrawnByStakeholder]`

`WithdrawnByStakeholderAttribute` in `src/IvaoHub.Core/Division/DomainContracts.cs`, namespace `IvaoHub.Core.Division`, accanto a
`ISubmittedByMembers`; `AttributeUsage(AttributeTargets.Class)`, nessuna proprietà: «una riga che il suo membro si riprende
cancellandola — la prenotazione di un evento». La prenotazione di E6a lo dichiara:

```csharp
[Audited]
[PermissionArea(EventsPermissions.BookingsArea)]
[WithdrawnByStakeholder]
public sealed class EventBooking : IOwnedByDepartment, IVisible, ISubmittedByMembers, IHasStakeholder, IHasResourceScope
```

È **un'opzione dell'entità**, la più stretta che funziona: dove non c'è — il PIREP, il training — cancellare resta del dipartimento.
**Un attributo**, come gli altri segni che il guardiano legge sulla classe (`[Audited]`, `[AlsoWrittenWith]`, `[PermissionArea]`), letto
con `inherit: false` come quelli: va sull'entità stessa.

### 3.2 Il guardiano

In `EnsureWriteIsAllowed`, subito dopo l'eccezione di T11, la sua gemella per l'eliminazione: un'entrata `Deleted` di un'entità
`ISubmittedByMembers` e `IHasStakeholder` che porta il segno passa se **l'interessato com'era salvato** è chi scrive.

- **Com'era salvato, non com'è l'istanza in mano**: l'interessato si legge da `entry.OriginalValues.ToObject()`, come l'eccezione di T11
  legge il «prima». Chi carica la riga di un altro, ci scrive il suo VID e poi la toglie non passa (un test lo prova). Un interessato
  calcolato da una colonna — `StakeholderVid => BookerVid` della prenotazione — si legge così uguale.
- **Nessun controllo dei dipartimenti**: in modifica servono perché la riga non si sposti; un'eliminazione non sposta niente, e il membro
  non tiene permessi su nessun dipartimento.
- **Tutto il resto come prima**: una riga senza il segno la elimina solo chi ha `{Area}.Edit`; un altro membro non elimina mai quella di
  un altro; lo staff con `{Area}.Edit`; il superadmin e l'anonimo come sempre; l'errore è lo stesso, `ForbiddenDomainException` con
  `{Area}.Edit`.
- **L'audit** scrive la riga `deleted` con il VID del membro e la riga com'era: è «il registro di chi ha fatto che cosa» del design §1.6.
- **I commenti** di `ISubmittedByMembers` e dell'eccezione di T11 ora dicono «Deleting is still the department's, unless the entity says
  its member takes it back».

### 3.3 All'avvio, il settimo rifiuto

`HubSaveChangesInterceptor.VerifyWithdrawals(entities)`, statico, accanto a `PermissionAreaOf`; `HubPipeline.InitializeAsync` lo chiama
sul modello di ogni contesto, subito dopo `VerifyAlternatives`. Rifiuta il segno su un'entità che **non è insieme** `IOwnedByDepartment`,
`ISubmittedByMembers` e `IHasStakeholder`, con tutti gli errori di tutte le entità in un messaggio, come `VerifyAlternatives`:

- **senza `IHasStakeholder`** non c'è un membro che se la riprenda;
- **senza `ISubmittedByMembers`** la riga non l'ha mandata il membro: un ban o un no-show lo riguardano, e non sono suoi da togliere;
- **senza `IOwnedByDepartment`** il guardiano non guarda affatto la riga, e nessuno sarebbe fermato dall'eliminarla. È la condizione che
  la fase non elencava (*scelta di Claude*): come in A3b (`AlsoOnDeletion` su un permesso non segnato «non eliminerebbe niente») e nel
  controllo rinforzato (un'alternativa segnata su un'entità senza dipartimento), un segno che il guardiano non onorerebbe ferma l'avvio
  invece di essere ignorato in silenzio.

**Fuori da `VerifyAlternatives`**, perché quella verifica i permessi alternativi contro il catalogo e questo segno non è un permesso: il
catalogo non serve, e il nome direbbe un'altra cosa. Sta accanto a chi lo legge, il guardiano, come la regola del controllo rinforzato che
è del catalogo sta nel suo costruttore (nota `2026-09-30-il-controllo-all-avvio-rinforzato` §3, §4). **Non un test di architettura**:
`ArchitectureTests.cs` è del maintainer, e il controllo all'avvio ferma anche un fork che aggiunge un modulo senza i test (A3b, nota
`2026-09-26-le-righe-affidate-a-chi-scrive` §3.5-bis).

### 3.4 Che cosa non cambia

- **L'unico handler e il motore CRUD**: nessuna riga. Il segno non apre la DELETE di `MapCrud`, che chiede all'handler la policy di
  scrittura; il ritiro passa da un endpoint del flusso del membro, che legge la sua riga e la elimina, e il guardiano controlla di nuovo
  sotto (E6a: `DELETE /api/events/mine/bookings/{id}`).
- **Le righe che ci sono**: nessuna entità ha il segno. Il PIREP, il training, `ContactMessage`, `SampleItem` e `SampleRecord` fanno ciò
  che facevano, e i loro test restano verdi senza essere toccati.
- Nessuna migrazione del nucleo; nessun cambio del cookie, dei grant, di `/api/me`, del browser.
- I due punti trovati in A3 (l'interessato e lo scope guardati solo dopo la scrittura, nota
  `2026-09-25-i-permessi-alternativi-e-la-creazione` §5) restano com'erano: la regola nuova guarda la riga com'era.

## 4. I test

- **Il modulo di prova** guadagna due righe gemelle (`tests/IvaoHub.IntegrationTests/SampleSubmissions.cs`): `SampleSubmission`
  (`smp_submissions`), **con il segno**, come una prenotazione, e `SampleReport` (`smp_reports`), **senza**, come un PIREP. Tutte e due
  `IOwnedByDepartment` con la maschera, `ISubmittedByMembers`, `IHasStakeholder` (da una colonna `sender_vid`, come la prenotazione legge
  il pilota da `booker_vid`), `[Audited]`, area `Sample`. La migrazione `AddSampleSubmissions` è del **solo contesto di prova**; lo
  snapshot prende anche `notified_at` e il suo indice su `cms_award_signals` (E10d), una tabella che il contesto mappa fuori dalle
  migrazioni: lo scarto innocuo già visto in T4b e in A3. `SampleItem` e `SampleRecord` non cambiano. **`ErasureTests` non cambia**: il
  test delle colonne di persona legge i contesti di ogni modulo tranne quello di prova (piano §16 punto 16), e `sender_vid` non entra
  nella sua lista.
- **`WithdrawnByStakeholderTests`** (integrazione, nuova): sul guardiano senza un endpoint davanti, come
  `AlternativeWritePermissionTests`, con l'identità che un login mette nel cookie, sulla MariaDB vera; VID 761037, 761047 e 761048 (quelli
  che E6a ha lasciato a questa fase), nessuno seminato, quindi nessuno staff dell'ED o dell'MD con un indirizzo:
  - il membro manda la sua riga e se la riprende, e l'audit ha `created` e `deleted` con il suo VID;
  - un altro membro non la elimina, nemmeno con `Sample.View` sul dipartimento: `ForbiddenDomainException` con `Sample.Edit`;
  - un altro membro che scrive il suo VID nell'istanza prima di toglierla non passa: decide la riga com'era salvata;
  - lo staff la elimina con `Sample.Edit` sul dipartimento della riga, e non con `Sample.Edit` su un altro;
  - una riga senza il segno (`SampleReport`) il membro la manda e la cambia (T11), ma non la elimina; lo staff sì.
- **`VerifyWithdrawalsTests`** (unità, nuova): i tre rifiuti uno per uno; tutti gli errori di tutte le entità in un messaggio, una volta
  sola anche per un'entità elencata due volte; passa ciò che il guardiano può onorare e ciò che non ha il segno.
- **La prova al contrario**: con l'interceptor e `HubPipeline.cs` di `main` rimessi (il segno resta, perché il modulo di prova lo usa),
  della classe nuova cade **solo** il ritiro del membro, `ForbiddenDomainException: VID 761037 does not hold Sample.Edit on any of ED`
  (4 su 5); con i file della fase, 5 su 5. **E dal lato del modulo**, sul branch di E6a (la sua sessione, 7 ottobre 2026): il ritiro del
  pilota cadeva con 403 prima di unire questo branch, e dopo, con il segno su `EventBooking`, risponde 204 con l'audit `deleted` a nome
  del pilota; `WithdrawnByStakeholderTests` ed `EventsBookingsTests` lì 16 su 16.

## 5. Alternative scartate

| Alternativa | Perché no |
|---|---|
| Il ritiro come uno stato della riga, come il PIREP (`PirepStatus.Withdrawn`) | contraddice «ritirare cancella la riga» (design §1.6, §3.6, deciso da Carmine); e la riga terrebbe lo slot sotto l'indice univoco su `slot_id`, che andrebbe allargato a «univoco fra le non ritirate», con ogni lettura del modulo a ricordarsi il filtro |
| La cancellazione «come il sistema» (senza utente) nell'endpoint del ritiro | l'aggiramento che la nota del PIREP scarta (§5): nessuna eliminazione di quell'endpoint sarebbe più controllata, e l'audit non saprebbe chi |
| L'eliminazione aperta a ogni riga `ISubmittedByMembers` e `IHasStakeholder` | il guardiano lascerebbe a un pilota cancellare il suo PIREP, che è un registro, e a un trainee il suo training; il segno la lascia solo dove il design la vuole |
| Un permesso alternativo (`[AlsoWrittenWith(…, AlsoOnDeletion = true)]`) | `IsWrittenWithAnAlternative` non lascia mai passare l'interessato, ed elimina solo con un permesso `OnlyForAssignee`; e un membro non tiene permessi: servirebbe un grant a ogni pilota (la nota del PIREP lo scarta) |
| Un'interfaccia-segno (`IWithdrawnByStakeholder`), o una proprietà di `ISubmittedByMembers` | dice la stessa cosa di un attributo senza dati; la proprietà farebbe rispondere ogni riga che la implementa (PIREP, training, contatti) |
| Il controllo all'avvio dentro `VerifyAlternatives` | quella verifica i permessi alternativi contro il catalogo; il segno non è un permesso (§3.3) |
| Un test di architettura invece del controllo all'avvio | `ArchitectureTests.cs` è del maintainer, e un fork che aggiunge un modulo senza i test non sarebbe fermato |
| L'interessato letto dall'istanza in mano, non dai valori originali | chi carica la riga di un altro potrebbe scriverci il suo VID e poi toglierla |

## 6. La domanda per Carmine

Posta con un commento sulla #232, la PR di questa fase:

> Confermi: una riga che l'entità dichiara ritirabile (`[WithdrawnByStakeholder]`) la cancella il membro che ne è l'interessato, e solo
> lui; le altre come prima (un PIREP o un training restano del dipartimento da cancellare, lo staff cancella con `{Area}.Edit`, il
> superadmin come sempre)? E l'hub rifiuta all'avvio il segno su un'entità che non è `IOwnedByDepartment`, `ISubmittedByMembers` e
> `IHasStakeholder`?

**Raccomandata: sì.** È la riga del design §1.6 detta nel nucleo, per le sole entità che la vogliono. Con un no, E6a resta senza il ritiro
(il suo test cade con 403) finché non c'è un'altra forma, e questa PR si chiude senza essere unita.

## 7. Che cosa si tocca

- **Il nucleo**: `src/IvaoHub.Core/Division/DomainContracts.cs` (l'attributo, il commento di `ISubmittedByMembers`),
  `src/IvaoHub.Core/Data/HubSaveChangesInterceptor.cs` (il guardiano, `VerifyWithdrawals`), `src/IvaoHub.Web/HubPipeline.cs` (la
  chiamata all'avvio; e uno spazio dopo `=` alla riga 234, venuto con la #218, che `dotnet format` chiede sul file toccato: un commit a
  parte, `style`, senza effetti).
- **Il modulo di prova**, file che ci sono (nucleo per `core-guard`, perché condivisi): `tests/IvaoHub.IntegrationTests/SampleItems.cs`
  (i due insiemi e le loro tabelle) e `Migrations/SampleDbContextModelSnapshot.cs`.
- **File nuovi**: `SampleSubmissions.cs`, la migrazione `AddSampleSubmissions`, `WithdrawnByStakeholderTests.cs`,
  `tests/IvaoHub.UnitTests/VerifyWithdrawalsTests.cs`. **Nessun test che c'era cambia.**

## Da portare nel piano

- **§16 punto 2** (la rete dell'interceptor): **dal 7 ott 2026** (M4, E10h) una riga `ISubmittedByMembers` e `IHasStakeholder` la cui
  entità lo dice (`[WithdrawnByStakeholder]`) la **cancella** anche il membro che ne è l'interessato, com'era salvata, e nessun altro
  senza `{Area}.Edit`; senza il segno, cancellarla resta del dipartimento. **L'avvio rifiuta sette cose**: le sei della 0.4.3 e il segno
  su un'entità che non è `IOwnedByDepartment`, `ISubmittedByMembers` e `IHasStakeholder`.
- **§9.7**: niente; non descrive le eccezioni del guardiano, e «Privacy dei membri» non cambia (una prenotazione ritirata non c'è più, la
  sua storia è nell'audit).
- **`09-design-m4.md` §13** («Che cosa chiede al nucleo»): una riga in più, il ritiro di chi ha mandato la riga (E10h, questa nota), che
  E0 non aveva visto; §1.1, §1.6 e §3.6 non cambiano.
- **`10-piano-implementazione-m4.md`**: E10h, scritta in questa PR.
