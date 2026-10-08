# Il ritiro di chi ha mandato la riga (E10h)

**Data:** 7 ottobre 2026 — fase E10h di M4, PR del nucleo (#232)
**Stato:** **decisa** (Carmine, 7 ottobre 2026, in chat alla sessione master e pubblicata su sua istruzione sulla #232: [le risposte 1 e
2][a1], dopo [i rilievi del revisore][rv], e [la risposta 3 corretta][a3]): **sì** alla forma (risposta 1, §6.2); **il limite della riga
mai caricata si accetta e si scrive, come per T11** (risposta 2, §3.2); sul rilievo 6 **una frase nel riassunto del segno, non un ottavo
rifiuto all'avvio**, e la nota di ogni fase che mette il segno dice perché la sua riga non porta decisioni (risposta 3, §6.3, che
sostituisce [quella delle 15:41][a3old]). La domanda era [un commento sulla #232][q1]; E6a, che usa il segno, unisce questo branch e va in
coda dopo la #232.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: si estende la rete dell'interceptor (`HubSaveChangesInterceptor`, il guardiano
`EnsureWriteIsAllowed`), che già lascia al membro **creare** la riga che manda (`ISubmittedByMembers`, M1) e **cambiarla** finché è sua
(M2, T11); nessun meccanismo nuovo, nessuna scrittura «come il sistema». È una PR del nucleo, prima del codice del modulo che la usa
(`CLAUDE.md` §0 regola 6). L'ha trovata la sessione di E6a leggendo il guardiano prima di scrivere il ritiro, e dalberone il 7 ottobre
2026 ha scelto, fra le tre strade offerte — una fase del nucleo prima (raccomandata), la domanda a Carmine prima, E6a senza il ritiro —,
**la fase del nucleo**.

[q1]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/232#issuecomment-6038934438
[rv]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/232#issuecomment-6039667157
[a1]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/232#issuecomment-6039778269
[p6]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/232#issuecomment-6040156289
[a3]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/232#issuecomment-6041679155
[a3old]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/232#issuecomment-6041353964

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
con `inherit: false` come quelli: va sull'entità stessa. Il suo riassunto dice anche dove non va (risposta 3, §6.3): **mai su una riga su
cui lo staff decide qualcosa del membro**, perché cancellarla cancellerebbe la decisione; e la nota della fase che lo mette dice perché la
sua riga non porta decisioni.

### 3.2 Il guardiano

In `EnsureWriteIsAllowed`, subito dopo l'eccezione di T11, la sua gemella per l'eliminazione: un'entrata `Deleted` di un'entità
`ISubmittedByMembers` e `IHasStakeholder` che porta il segno passa se **l'interessato com'era caricato** è chi scrive.

- **Com'era caricato, non com'è l'istanza in mano**: l'interessato si legge da `entry.OriginalValues.ToObject()`, come l'eccezione di T11
  legge il «prima». Chi legge la riga di un altro, ci scrive il suo VID e poi la toglie non passa (un test lo prova). Un interessato
  calcolato da una colonna — `StakeholderVid => BookerVid` della prenotazione — si legge così uguale.
- ⚠️ **L'endpoint deve caricare la riga; uno stub passa** (il rilievo 2 del revisore; **accettato da Carmine come limite scritto, come per
  T11**, risposta 2). I valori originali sono ciò che il tracker ha visto quando ha cominciato a seguire la riga, non una lettura del
  database: per `Remove(new X { Id = id, SenderVid = me })`, una riga mai letta, l'interessato «originale» è quello che ha scritto chi
  chiama, il guardiano gli crede, e il `DELETE` toglie la riga di un altro membro. L'eccezione di T11 si fida del tracker allo stesso modo;
  questa in più dà una cancellazione a chi non tiene permessi. Oggi nessun codice cancella con uno stub, ed E6a legge la prenotazione per
  id e per pilota prima di toglierla (`PilotBookings.WithdrawAsync`). Rileggere l'interessato dal database (`GetDatabaseValues`, una query
  in più per ogni ritiro) è l'alternativa che Carmine non ha scelto. La frase sta nel riassunto dell'attributo, nel commento del guardiano,
  qui e nella trappola di `HANDOFF-M4.md`, e un test fissa il caso com'è (§4).
- **Nessun controllo dei dipartimenti**: in modifica servono perché la riga non si sposti; un'eliminazione non sposta niente, e il membro
  non tiene permessi su nessun dipartimento.
- **Tutto il resto come prima**: una riga senza il segno la elimina solo chi ha `{Area}.Edit`, o un'alternativa segnata `AlsoOnDeletion`
  sulla riga affidata a lui (A3b); un altro membro non elimina mai quella di un altro; lo staff con `{Area}.Edit`; il superadmin e
  l'anonimo come sempre; l'errore è lo stesso, `ForbiddenDomainException` con `{Area}.Edit`.
- **Il guardiano guarda chi la riga riguarda, non chi l'ha mandata, e non sa che ore sono** (il rilievo 5 del revisore): una riga segnata
  che lo staff avesse scritto su un membro la cancella il membro, in qualunque momento. È la gemella di T11 e va letta così; «fino
  all'off block» lo dice l'endpoint di E6a, non il guardiano.
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
  - un altro membro che legge la riga, ci scrive il suo VID e poi la toglie non passa: decide la riga com'era caricata
    (`TheRowAsItWasLoadedSaysWhoseItIs`);
  - **ma uno stub mai letto passa** (`AStubNeverLoadedIsBelievedAsItsCallerWroteIt`, dopo la revisione): un altro membro toglie uno stub
    della riga del membro con il suo VID, la riga se ne va, e l'audit dice almeno chi l'ha tolta. Fissa il limite che Carmine ha
    accettato (risposta 2): se un giorno il guardiano rileggerà il database, il test cambierà con lui;
  - lo staff la elimina con `Sample.Edit` sul dipartimento della riga, e non con `Sample.Edit` su un altro;
  - una riga senza il segno (`SampleReport`) il membro la manda e la cambia (T11), ma non la elimina; lo staff sì.
- **`VerifyWithdrawalsTests`** (unità, nuova): i tre rifiuti uno per uno; tutti gli errori di tutte le entità in un messaggio, una volta
  sola anche per un'entità elencata due volte; passa ciò che il guardiano può onorare e ciò che non ha il segno.
- **La prova al contrario** (al primo giro, con i cinque test di allora): con l'interceptor e `HubPipeline.cs` di `main` rimessi (il segno
  resta, perché il modulo di prova lo usa), della classe nuova cade **solo** il ritiro del membro,
  `ForbiddenDomainException: VID 761037 does not hold Sample.Edit on any of ED` (4 su 5); con i file della fase, 5 su 5. **E dal lato del modulo**, sul branch di E6a (la sua sessione, 7 ottobre 2026): il ritiro del
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
| L'interessato riletto dal database (`GetDatabaseValues`), così che nemmeno uno stub passi | una query in più per ogni ritiro; Carmine ha scelto il limite scritto, come per T11 (risposta 2, §3.2) |

## 6. Le domande per Carmine, e le risposte

### 6.1 La domanda della nota

Posta il 7 ottobre 2026 con [un commento sulla #232][q1], la PR di questa fase (lì in inglese, come ogni testo di una PR):

> Confermi: una riga che l'entità dichiara ritirabile (`[WithdrawnByStakeholder]`) la cancella il membro che ne è l'interessato, e solo
> lui; le altre come prima (un PIREP o un training restano del dipartimento da cancellare, lo staff cancella con `{Area}.Edit`, il
> superadmin come sempre)? E l'hub rifiuta all'avvio il segno su un'entità che non è `IOwnedByDepartment`, `ISubmittedByMembers` e
> `IHasStakeholder`?

**Raccomandata: sì.** È la riga del design §1.6 detta nel nucleo, per le sole entità che la vogliono. Con un no, E6a resta senza il ritiro
(il suo test cade con 403) finché non c'è un'altra forma, e questa PR si chiude senza essere unita.

### 6.2 Le risposte di Carmine (7 ottobre 2026)

Date in chat alla sessione master e pubblicate su sua istruzione [sulla #232][a1], dopo [i rilievi del revisore][rv]:

1. **Sì alla domanda di §6.1**: una riga che l'entità dichiara ritirabile (`[WithdrawnByStakeholder]`) la cancella il membro che ne è
   l'interessato, e solo lui; ogni altra come prima (un PIREP o un training restano del dipartimento da cancellare, lo staff cancella con
   `{Area}.Edit`, il superadmin come sempre). L'hub rifiuta all'avvio il segno su un'entità che non è insieme `IOwnedByDepartment`,
   `ISubmittedByMembers` e `IHasStakeholder`: è ciò che fa `VerifyWithdrawals` (§3.3), e il suo riassunto ora lo dice con queste parole.
2. **La riga mai caricata** (il rilievo 2 del revisore, e la sua domanda 4): **il limite si accetta e si scrive, come per T11**; il
   guardiano continua a leggere i valori originali del tracker e non rilegge il database. Quindi la frase — *l'endpoint deve caricare la
   riga; uno stub passa* — nel riassunto dell'attributo, nella nota (§3.2) e nella trappola dell'handoff, e un test che fissa il caso com'è
   (§4). Fatto dopo la revisione, con il rilievo 3 (la parola su `AlsoOnDeletion` nel riassunto dell'attributo).

### 6.3 Il segno su un'entità con un permesso `DeniedToStakeholder` (rilievo 6): una frase, deciso

Il revisore: il controllo all'avvio non rifiuta il segno su un'entità che ha anche un permesso `DeniedToStakeholder`; lì il membro potrebbe
cancellare una decisione presa su di lui, e oggi lo protegge solo l'opzione. **Un ottavo rifiuto, o una frase nel riassunto
dell'attributo?**

**Decisa da Carmine** ([risposta 3, corretta][a3], 7 ottobre 2026, in chat alla sessione master e pubblicata su sua istruzione): **una
frase nel riassunto del segno, non un ottavo rifiuto all'avvio**, come raccomandato qui sotto. Il riassunto di `[WithdrawnByStakeholder]`
dice: *mai su una riga su cui lo staff decide qualcosa del membro — cancellarla cancellerebbe la decisione; il guardiano non sa né lo stato
né l'ora, e fino a quando un membro si ritira lo dice l'endpoint del modulo*; e **la nota di ogni fase che mette il segno su un'entità dice
perché la sua riga non porta decisioni**, così la revisione lo controlla. Nessun codice oltre la frase. Una prima risposta, delle 15:41
([qui][a3old]), chiedeva l'ottavo rifiuto, con un test: era stata data prima che Carmine vedesse la raccomandazione, che la sessione
master non gli aveva mostrato (l'errore, dice la risposta corretta, è del master), ed è sostituita da questa.

**La raccomandazione: la frase** (scritta a Carmine anche [in un commento sulla #232][p6]).

- **Il guardiano lascia già all'interessato cambiare ogni colonna della riga che ha mandato**: l'eccezione di T11 vale su ogni entità
  `ISubmittedByMembers` e `IHasStakeholder`, senza segno, e non guarda quali colonne cambiano. Uno stato deciso dallo staff, oggi, lo
  protegge l'endpoint del modulo, non il guardiano. Un ottavo rifiuto sarebbe più severo per la cancellazione, che è un'opzione, che per
  la modifica, che vale per tutte.
- **Il catalogo conosce i permessi per area, non per entità**, e «l'area ha un permesso `DeniedToStakeholder`» non dice che la decisione
  sta su quella riga. `EventAtc.Edit` è negato all'interessato per i turni (il no-show, la cessione), e nella stessa area sta la
  disponibilità di un controllore, che E11a potrebbe voler ritirare cancellandola e che non porta nessuna decisione: un rifiuto per area
  la fermerebbe. Uno solo per le alternative dell'entità (`[AlsoWrittenWith]` negato all'interessato) lascerebbe passare il PIREP di
  supporto, che l'MD decide con l'`Edit` dell'area.
- **La frase** direbbe: *mai su una riga su cui lo staff decide qualcosa del membro — un PIREP di supporto, la cessione di un turno —:
  cancellarla cancellerebbe la decisione; il guardiano non sa né lo stato né l'ora, e fino a quando si ritira lo dice l'endpoint del
  modulo*. E la nota della fase che mette il segno dice perché la sua riga non porta decisioni, così il revisore lo controlla.

L'alternativa, non scelta: **l'ottavo rifiuto** nella forma più vicina al bisogno, quella per area — il segno su un'entità la cui area ha un
`Edit` `DeniedToStakeholder` ferma l'avvio. La prenotazione (`EventBookings`) e l'iscrizione in presenza sarebbero passate; la disponibilità
di E11a si sarebbe ritirata con uno stato, come il PIREP.

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

- **§16 punto 2** (la rete dell'interceptor): **dal 7 ott 2026** (M4, E10h, decisa da Carmine sulla #232) una riga `ISubmittedByMembers`
  e `IHasStakeholder` la cui entità lo dice (`[WithdrawnByStakeholder]`) la **cancella** anche il membro che ne è l'interessato, com'era
  caricata, e nessun altro senza `{Area}.Edit` (o un'alternativa `AlsoOnDeletion`); senza il segno, cancellarla resta del dipartimento.
  Come per la modifica di T11 il guardiano legge i valori originali del tracker: **l'endpoint deve caricare la riga; uno stub passa** (un
  limite accettato e scritto, risposta 2). **L'avvio rifiuta sette cose**: le sei della 0.4.3 e il segno su un'entità che non è insieme
  `IOwnedByDepartment`, `ISubmittedByMembers` e `IHasStakeholder`.
- **E nello stesso punto** (risposta 3, §6.3): il segno **mai su una riga su cui lo staff decide qualcosa del membro** — cancellarla
  cancellerebbe la decisione; il guardiano non sa né lo stato né l'ora, e fino a quando un membro si ritira lo dice l'endpoint del modulo —,
  e **la nota di ogni fase che mette il segno dice perché la sua riga non porta decisioni**, così la revisione lo controlla. Una frase nel
  riassunto del segno, non un rifiuto all'avvio: il catalogo conosce i permessi per area, non per entità.
- **§9.7**: niente; non descrive le eccezioni del guardiano, e «Privacy dei membri» non cambia (una prenotazione ritirata non c'è più, la
  sua storia è nell'audit).
- **`09-design-m4.md` §13** («Che cosa chiede al nucleo»): una riga in più, il ritiro di chi ha mandato la riga (E10h, questa nota), che
  E0 non aveva visto; §1.1, §1.6 e §3.6 non cambiano.
- **`10-piano-implementazione-m4.md`**: E10h, scritta in questa PR.
