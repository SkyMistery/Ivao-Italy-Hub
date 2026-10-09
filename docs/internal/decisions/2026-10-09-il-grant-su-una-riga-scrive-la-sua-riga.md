# Il grant su una riga scrive la sua riga, e quelle sotto di lei (E10i)

**Data:** 9 ottobre 2026 — fase E10i di M4, PR del nucleo (#237)
**Stato:** **decisa** (Carmine, 9 ottobre 2026, in chat alla sessione master e pubblicata su sua istruzione [sulla #235][a235]): **sì** alla
fase del nucleo di M4 (risposta 1), e un grant su un evento solo **crea un figlio di quell'evento** — uno slot, una rotta —, **mai un
evento nuovo** (risposta 2). **La forma nel codice è di questa nota** (§3), come per ogni fase del nucleo (`10`, «Regole di tutte le
fasi»): segue le due risposte senza aprire domande nuove; due sue conseguenze sono dette al revisore (§6).
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: si estende la rete dell'interceptor (`HubSaveChangesInterceptor`, il guardiano
`EnsureWriteIsAllowed`), che conosce già lo scope della riga per i permessi alternativi (M2, T13; M3, A3) e non per `{Area}.Edit`; nessun
meccanismo nuovo, nessun segno nuovo. È una PR del nucleo, prima del codice del modulo che la usa (`CLAUDE.md` §0 regola 6). L'ha trovata
la sessione di E6a (la #233) misurando il ritiro di una prenotazione da parte dello staff, e dalberone l'ha segnalata con [l'issue
#235][i235] senza toccare il nucleo.

[i235]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/235
[a235]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/235#issuecomment-6070088402

## 1. Che cosa serve, e perché nessun meccanismo lo copre

- **Il design M4** (§6.2) e la nota `2026-09-29-chi-lavora-sugli-eventi` (punto 4): chi sta fuori dalle posizioni riceve un grant a un
  VID, «anche **su un evento solo**» (`resource_scope`, `events:event:{id}`). Le righe dello staff di un evento rispondono con lo scope
  del loro evento — `EventAirport`, `EventRoute`, `EventSlot`: `ResourceScope => Event.ScopeOf(EventId)` — proprio perché un permesso
  dato sull'evento valga sulle sue righe, senza un grant per riga (nota `2026-09-15-permessi-su-una-riga-e-chi-ha-interesse` §3.1).
- **L'handler lo fa, il guardiano no** ([issue #235][i235]): `DepartmentAuthorizationHandler` chiede il permesso con lo scope della riga;
  `EnsureWriteIsAllowed` chiedeva `{Area}.Edit` con `RequireAny(permission, departments, fir)`, cioè
  `Has(permission, department, null, fir)`, **senza scope**. Un permesso con uno scope non raggiunge mai «nessuno scope»
  (`PermissionSet.Reaches`), quindi il salvataggio cadeva con `ForbiddenDomainException`: un VID con `EventBookings.Edit` su un evento
  solo passava l'autorizzazione dell'endpoint e cadeva al salvataggio, e non toglieva una prenotazione né correggeva uno slot di
  quell'evento (la misura della sessione di E6a). Il master ha letto il guardiano su `main` e l'ha trovato così (risposta 1).
- **La creazione l'aveva chiusa la nota di A3** (`2026-09-25-i-permessi-alternativi-e-la-creazione` §3.2), per le alternative: «senza
  scope», perché la riga nuova o non ha ancora l'identificatore (`training:exam:0`, che il database dà dopo) o risponde con lo scope di
  ciò che le sta sopra; «nel primo caso un grant con scope nominerebbe una riga che non esiste; nel secondo un permesso dato su una riga ne
  farebbe nascere altre sotto di lei, che è più largo di quanto deciso. Se un giorno servisse, è un'altra nota». **È questa**: Carmine ha
  deciso che per un figlio serve — «chi collabora su un evento deve poterne caricare gli slot» (risposta 2) —, e mai per una riga nuova
  come quella del grant.

## 2. Che cosa c'è, letto nel codice (`main` a `0f72737`)

1. **Il guardiano** (`src/IvaoHub.Core/Data/HubSaveChangesInterceptor.cs`, `EnsureWriteIsAllowed`), in quest'ordine: l'anonimo, il
   superadmin e una riga senza dipartimento passano; una riga `ISubmittedByMembers` nuova; l'interessato che cambia la sua (T11); il ritiro
   (E10h); il partecipante (T14); le alternative (`IsWrittenWithAnAlternative`: in modifica con lo scope dell'istanza, alla creazione
   **senza** scope, all'eliminazione con lo scope dell'istanza); infine `RequireAny({Area}.Edit, dipartimenti, FIR)` **senza scope**, e se
   la riga cambia dipartimenti o FIR lo stesso sui dipartimenti e sul FIR di prima.
2. **L'unico handler** chiede sempre con lo scope della risorsa, anche per una riga nuova: il motore CRUD gliela passa prima di salvarla
   (`DeniesWrite`, dopo `Apply` e `BeforeAuthorize`). Uno slot nuovo arriva con lo scope del suo evento, un evento nuovo con
   `events:event:0`: il suo `Id` lo dà il database, e fino al salvataggio è `0` (EF tiene il valore provvisorio nel tracker, non
   sull'istanza).
3. **Chi risponde con uno scope**: `Event` (il suo, `events:event:{Id}`); `EventAirport`, `EventRoute`, `EventSlot` (quello del loro
   evento); il PIREP dei tour (quello del suo tour; è `ISubmittedByMembers`, quindi alla creazione passa prima di ogni domanda); nel modulo
   di prova `SampleItem` e `SampleRecord` (il loro). Il training non ne ha più (il trainer è sulle righe affidate, A3b).
4. **Chi scrive un grant con scope**: solo l'endpoint di un modulo, su una riga che ha trovato (`ModuleGrants.GiveAsync`, «aggiungi
   validatore» dei tour); la schermata dei permessi non scrive mai uno scope (nota del 15 settembre §3.1).

## 3. La forma nel codice

### 3.1 Una domanda sola: «tenuto sulla riga»

`Holds(permission, departments, scope, fir)`: tenuto su uno dei dipartimenti della riga, **con il suo scope** e il suo FIR — la domanda
dell'unico handler. La chiedono `RequireAny` per `{Area}.Edit` e ogni alternativa: il guardiano chiede **in un modo solo**. Prima `Edit`
passava `null` e le alternative lo scope della riga: due domande diverse sulla stessa riga.

### 3.2 Lo scope della riga, letto come il suo FIR

`RowScope(entry)`: lo scope della riga nuova o cambiata com'è scritta, della riga tolta **com'era** (`OriginalScope`, dai valori
originali del tracker, `entry.OriginalValues.ToObject()`), come `RowFir` legge il FIR e il ritiro l'interessato.

- **In modifica e all'eliminazione** `Edit` si chiede con lo scope della riga: un grant su un evento cambia e toglie l'evento e le sue
  righe — scali, rotte, slot, e le prenotazioni di E6a quando ci saranno —, e nessuna riga di un altro evento.
- **All'eliminazione, lo scope com'era**: chi cambia la colonna dello scope e poi toglie la riga nella stessa unità di lavoro è giudicato
  sulla riga che c'era. Le alternative prima leggevano l'istanza anche qui; ora tutte e due leggono `RowScope`.

### 3.3 Alla creazione: lo scope con cui la riga nuova risponde (la risposta 2)

**Una regola generica, senza un segno sull'entità**: alla creazione il guardiano chiede con lo scope con cui **la riga nuova** risponde,
come l'handler gliela chiede già dal motore CRUD. Due casi, senza che il nucleo legga lo scope:

- **una riga figlia** risponde con lo scope della riga sopra di lei, che esiste — uno slot con quello del suo evento —: un grant su
  quell'evento la crea. È la risposta 2;
- **una riga che risponde con il suo scope** — un evento, un item del modulo di prova — lo costruisce sulla sua chiave, che il database
  non ha ancora dato: al guardiano è `events:event:0`. Nessun grant lo nomina, quindi un grant su un evento **non crea un evento nuovo**:
  «un permesso dato su una riga non ne fa nascere altre». È il caso che la nota di A3 descriveva come «un grant con scope nominerebbe una
  riga che non esiste».

**Si regge su due condizioni**, scritte nel riassunto di `IHasResourceScope` (`src/IvaoHub.Core/Division/DomainContracts.cs`), dove le
legge chi scrive un modulo:

1. **un grant si scrive su una riga che esiste**: l'endpoint del modulo la trova prima, come «aggiungi validatore»; la schermata dei
   permessi non scrive scope;
2. **lo scope proprio di una riga si costruisce sulla sua chiave**, mai su qualcosa che chi la scrive sceglie, come il suo indirizzo: un
   grant scritto su quello creerebbe la riga.

Oggi le tengono tutte le entità con uno scope (§2 punto 3). Sono le stesse su cui l'handler si regge già alla creazione. **Misurata**, la
condizione 1, con una prova buttata (mai nel ramo): con un permesso scritto su `sample:item:0` un item nuovo si crea, perché al guardiano
la riga nuova risponde proprio con `sample:item:0`. È il limite della regola, detto: un grant scritto su una riga che non esiste ancora
creerebbe righe come lei; nessun modulo ne scrive uno, e l'handler, dal motore CRUD, risponde già allo stesso modo.

**Perché nessun segno sull'entità** (l'alternativa più vicina, §5): un segno «il mio scope è quello della riga sopra» sulle righe figlie
chiederebbe a ogni modulo di ricordarlo su ogni figlia — scali, rotte e slot oggi; postazioni, regole di award, domande dopo —; una figlia
dimenticata darebbe un 403 al collaboratore con l'handler che dice sì; e questa PR aspetterebbe una fase del modulo che metta il segno.
Con la regola, **guardiano e handler rispondono uguale sulla stessa riga anche alla creazione**, che è lo scopo per cui l'avvio rifiuta
le dichiarazioni contraddittorie (piano §16 punto 2, 0.4.3).

**Anche per le alternative segnate `AlsoOnCreation`**, la stessa domanda: con lo scope della riga nuova, dove prima era «senza scope».
Oggi non cambia niente: nessuna entità ha un'alternativa che crea e uno scope preso da sopra (`SampleRecord` risponde con il suo, il PIREP
non crea con un'alternativa, il training non ha scope). È la regola di A3 riletta con la risposta 2: un grant su una riga ne crea i figli,
qualunque permesso porti. Una conseguenza detta al revisore (§6).

### 3.4 Lo spostamento fra scope

`IsMoved(entry, owned)`: una modifica sposta la riga se cambiano i suoi dipartimenti, il suo FIR **o il suo scope** — i tre posti in cui un
permesso è tenuto su una riga. Allora `Edit` si chiede **anche** sui dipartimenti, sullo scope e sul FIR di prima: un grant su un evento
non prende uno slot di un altro evento né ne dà uno via. Senza, con `Edit` che ora conta lo scope, un grant sull'evento B sposterebbe uno
slot dall'evento A a B rispondendo solo per lo scope nuovo. Il motore CRUD fa già lo stesso (chiede la scrittura sulla riga prima e dopo il
payload), e il form non sposta uno slot (`SlotMapper.Apply` scrive l'evento solo su una riga nuova): il guardiano lo tiene per ogni altra
strada.

**Nessuna alternativa sposta la riga fra scope**, come fra dipartimenti e FIR: `IsMoved` è lo stesso per le due. Per lo scope chiude il
punto 2 trovato in A3 (nota `2026-09-25-i-permessi-alternativi-e-la-creazione` §5: «lo scope si guarda solo dopo la scrittura»), che la
nota di A3b lasciava al maintainer come rafforzamento: qui lo porta la stessa domanda. Nessuna riga di oggi cambia il suo scope con
un'alternativa (il PIREP scrive `ScopeTourId` una volta). Una conseguenza detta al revisore (§6).

### 3.5 Che cosa non cambia

- **L'unico handler, il motore CRUD, la lista generata**: nessuna riga. La lista di chi ha un grant su un evento solo resta quella di oggi.
- **Un permesso senza scope** raggiunge ogni scope (`PermissionSet.Reaches`): chi tiene `Edit` sul dipartimento — per posizione o per un
  grant — fa ciò che faceva, anche spostando una riga fra scope.
- Il superadmin e l'anonimo; le eccezioni dei membri (T11, E10h, T14); l'errore, `ForbiddenDomainException` con `{Area}.Edit`; il punto 1
  di A3 (l'interessato guardato solo dopo la scrittura).
- **Nessuna forma che un test del maintainer usa**: `RequireAny` è privato, e `ICurrentUser.Has(permission, department, scope, fir)` c'era
  già (`TestCurrentUser` lo eredita con i permessi senza scope, che raggiungono ogni scope); niente in `HubClaims.ParsePermission`, nel
  cookie, nei grant, in `/api/me`, nel browser. Nessuna migrazione del nucleo.

## 4. I test

- **Il modulo di prova** guadagna una riga figlia, `SamplePart` (`smp_parts`, `tests/IvaoHub.IntegrationTests/SampleParts.cs`), che
  risponde con lo scope del suo item (`SampleItem.ScopeOf`, nuovo, che anche `SampleItem.ResourceScope` usa, per non scrivere la forma due
  volte): uno slot di un evento, senza niente di uno slot. `[Audited]`, area `Sample`, e le due alternative di `SampleRecord`
  (`Sample.Decide` che cambia, `Sample.Record` che crea). La migrazione `AddSampleParts` è del **solo contesto di prova**, e lo snapshot
  cambia solo per `smp_parts`. `ErasureTests` non cambia: non legge il contesto di prova, e `smp_parts` non ha colonne di persona.
- **`ResourceScopeWriteTests`** (integrazione, nuova, 6), sul guardiano senza un endpoint davanti, come `AlternativeWritePermissionTests`,
  con l'identità che un login mette nel cookie, sulla MariaDB vera; un VID solo, **761097** (l'ultimo libero del modulo, nessuno
  seminato: i claim sono la persona):
  1. `Sample.Edit` su un item cambia e toglie quell'item, e non un altro; l'audit dice 761097;
  2. raggiunge le parti con il suo scope, e non quelle di un altro item;
  3. crea una parte del suo item, e non di un altro, e nessun item nuovo;
  4. una parte spostata da un item all'altro chiede il permesso sui due: tenuto su quello di partenza no, su quello d'arrivo no, sui due
     sì; e nessuna alternativa la sposta, nemmeno tenuta sui due;
  5. un'alternativa segnata per la creazione, tenuta su un item, crea una parte di quell'item e non di un altro; `Sample.Decide` la cambia
     e non ne crea;
  6. tenuto sul dipartimento senza scope fa ciò che faceva (crea, cambia, sposta fra item, toglie), e lo spostamento fra dipartimenti
     chiede ancora i due lati — anche tenuto sulla riga: sul solo SOD no, su SOD ed ED sì.
- **`EventsScopedGrantTests`** (integrazione, nuova, 2), **sugli endpoint veri degli eventi**, il caso dell'issue: 761097 seminato senza
  indirizzo né posizione, gli eventi e i loro scali scritti dall'installazione, i grant scritti nella tabella come li scriverà una
  schermata del modulo:
  1. con `EventBookings.Edit` su un evento: **carica** una tabella (`/slots/load`), crea uno slot, lo corregge, lo toglie; sull'altro
     evento 403 a tutte e quattro, e il suo slot resta com'era;
  2. con `Events.Edit` su un evento: lo cambia; l'altro 403; **un evento nuovo 403**.
- **La prova al contrario**: con `HubSaveChangesInterceptor.cs` e `DomainContracts.cs` di `main` (`0f72737`) rimessi, ricompilati, le 8
  cadono **tutte**, ognuna dove la fase cambia qualcosa: `Edit` con lo scope rifiutato
  (`VID 761097 does not hold Sample.Edit on any of ED`; sugli endpoint `EventBookings.Edit` al caricamento e `Events.Edit` alla modifica,
  403), e `Sample.Decide` tenuto sui due item che sposta la parte (`No exception was thrown`, il punto 2 di A3). Con i file della fase
  rimessi, toccati e ricompilati, 8 su 8.
- **Le prenotazioni** (la #233, non su `main`): la stessa regola, provata qui sulle righe di prova e sugli slot; sul branch di E6a la
  rivede la sua sessione dopo il merge, dove un grant su un evento solo toglierà le prenotazioni di quell'evento.

## 5. Alternative scartate

| Alternativa | Perché no |
|---|---|
| La creazione sempre senza scope, come proponeva l'issue | contro la risposta 2: chi collabora su un evento non ne caricherebbe gli slot |
| Un segno sulle righe figlie («il mio scope è quello della riga sopra»): un attributo, un'interfaccia, un membro di `IHasResourceScope` | ogni figlia di ogni modulo dovrebbe ricordarlo; una dimenticata dà 403 con l'handler che dice sì; e questa PR aspetterebbe una fase del modulo (§3.3) |
| Riconoscere lo scope proprio perturbando la chiave sul tracker (`CurrentValues.Clone()` con un'altra chiave, e lo scope confrontato) | chiuderebbe anche un grant scritto su una riga che non esiste, con un pezzo furbo sul tracker di EF, per un caso che nessun modulo produce (§3.3, condizione 1) e che l'handler chiede già così |
| Cercare la riga sopra nel database, da una chiave esterna o leggendo lo scope come `{modulo}:{tipo}:{id}` | il nucleo non legge gli scope (nota del 15 settembre §3.1), e sarebbe una query per ogni riga nuova |
| Le alternative alla creazione ancora senza scope | due domande diverse alla creazione; oggi non cambia niente (§3.3, §6) |
| Lo spostamento fra scope lasciato al motore CRUD | il guardiano è la rete sotto le policy: una scrittura fuori dal motore sposterebbe una riga con il solo scope nuovo |

## 6. Le domande a Carmine, e le risposte

**La domanda**, con [l'issue #235][i235] (8 ottobre 2026): il guardiano chieda con lo scope della riga in modifica e all'eliminazione, come
l'handler; la creazione resti senza scope, come per le alternative; e una domanda in più: **un grant su un evento crea un figlio di
quell'evento** (uno slot nuovo, una rotta)?

**Le risposte** (9 ottobre 2026, in chat alla sessione master e pubblicate su sua istruzione [sulla #235][a235]):

1. **Sì, la prende dalberone**, come fase del nucleo di M4: la sua PR, una nota nuova, i test nei test del guardiano, unita prima della fase
   del modulo che se ne serve. Il master ha letto il guardiano su `main` e l'ha trovato come l'issue lo descrive: `RequireAny` chiede
   `{Area}.Edit` senza lo scope della riga.
2. **Sì per un figlio, mai per un evento nuovo**: chi collabora su un evento deve poterne caricare gli slot; una riga nuova che non è figlia
   della riga con lo scope resta com'è — un permesso dato su una riga non ne fa nascere altre.

**Dette al revisore**, perché sono conseguenze della forma e non righe delle risposte: le alternative segnate `AlsoOnCreation` chiedono la
creazione con lo stesso scope (§3.3, oggi non cambia niente), e nessuna alternativa sposta una riga fra scope (§3.4, chiude per lo scope il
punto 2 di A3). Se Carmine preferisce lasciarle com'erano, ognuna è una riga del guardiano e un test.

## 7. Che cosa si tocca

- **Il nucleo**: `src/IvaoHub.Core/Data/HubSaveChangesInterceptor.cs` (`EnsureWriteIsAllowed`, `IsWrittenWithAnAlternative`, e `Holds`,
  `RowScope`, `OriginalScope`, `IsSameScope`, `IsMoved`, `RequireAny` con lo scope) e `src/IvaoHub.Core/Division/DomainContracts.cs` (i
  riassunti di `AlsoWrittenWithAttribute`, di `AlsoOnCreation` e di `IHasResourceScope`, con le due condizioni).
- **Il modulo di prova**, file che ci sono (nucleo per `core-guard`, perché condivisi): `tests/IvaoHub.IntegrationTests/SampleItems.cs`
  (`SampleItem.ScopeOf`, l'insieme e la tabella delle parti) e `Migrations/SampleDbContextModelSnapshot.cs`.
- **File nuovi**: `SampleParts.cs`, la migrazione `AddSampleParts`, `ResourceScopeWriteTests.cs`, `EventsScopedGrantTests.cs`. **Nessun
  test che c'era cambia.**

## Da portare nel piano

- **§16 punto 2** (la rete dell'interceptor): **dal 9 ott 2026** (M4, E10i, decisa da Carmine sulla #235) il guardiano chiede
  `{Area}.Edit` come l'unico handler, **con lo scope della riga** (`IHasResourceScope`): in modifica, all'eliminazione (la riga com'era) e
  alla creazione (lo scope con cui la riga nuova risponde). Un grant su una riga scrive quella riga e quelle che rispondono con il suo
  scope, e le crea — un grant su un evento ne crea gli slot e le rotte —, mai un'altra riga come lei, perché lo scope proprio di una riga
  nuova è costruito sulla chiave che il database non ha ancora dato. Si regge su due condizioni: **un grant si scrive su una riga che
  esiste**, e **lo scope proprio di una riga si costruisce sulla sua chiave**. Le alternative `AlsoOnCreation` chiedono la creazione allo
  stesso modo («senza scope» non vale più); una riga che cambia scope chiede `Edit` sui due scope, come fra dipartimenti e FIR, e nessuna
  alternativa la sposta.
- **§6.3**, la voce «Un grant su una riga sola»: «Tutto nell'unico handler» diventa «nell'unico handler e, dal 9 ott 2026 (E10i), nel
  guardiano: in modifica, all'eliminazione e alla creazione delle righe che rispondono con il suo scope».
- **`09-design-m4.md` §13** («Che cosa chiede al nucleo»): una riga in più, il grant su un evento solo nel guardiano (E10i, questa nota),
  che E0 non aveva visto; §6.2 non cambia: il grant «anche su un evento solo» ora scrive davvero l'evento e le sue righe.
- **`10-piano-implementazione-m4.md`**: E10i, scritta in questa PR.
