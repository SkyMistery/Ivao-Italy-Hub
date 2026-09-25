# Più permessi alternativi in scrittura, e uno anche alla creazione (A3)

**Data:** 25 settembre 2026 — fase A3 di M3, PR del nucleo
**Stato:** **scelta tecnica**, per dare forma nel codice alla decisione n.2 di Carmine (design `07-design-m3.md` §3.4, §8 n.7,
§12 n.2, deciso sulla PR #121 nel [commento delle risposte][r1]; nota `2026-09-25-chi-conduce-e-chi-scrive-un-training` §2 punto 2),
che rimanda a questa la forma nel codice. La domanda che il piano lasciava alla nota — «una proprietà dell'attributo o un attributo a
parte» (`08-piano-implementazione-m3.md`, A3 punto 2) — non apre un bivio (§3.2). **Una domanda proposta**, fuori dalla forma decisa
e posta in anticipo su A10: **chi elimina un esame** (§3.5), a Carmine con un commento su #131; solo una delle tre risposte
toccherebbe il codice di A3.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: si estendono la rete dell'interceptor (`HubSaveChangesInterceptor`, il guardiano
`EnsureWriteIsAllowed`) e il suo attributo `[AlsoWrittenWith]` (M2, T13), che coprono già un permesso alternativo in modifica; il
modulo non scrive una tabella per ruolo né un controllo suo. È una PR del nucleo, prima del codice del modulo che la usa (A7, A10),
con i test della spina dorsale che Carmine ha chiesto (`CLAUDE.md` §0 regola 6).

[r1]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/121#issuecomment-5832705237

## 1. Che cosa c'era, letto nel codice

- **`AlsoWrittenWithAttribute`** (`Core/Division/DomainContracts.cs`, T13): `AttributeUsage(AttributeTargets.Class)` senza
  `AllowMultiple`, quindi **un** permesso per entità.
- **Il guardiano**, in quest'ordine: un anonimo (l'installazione stessa), il superadmin e una riga senza dipartimento passano; una
  riga `ISubmittedByMembers` la crea chiunque; l'interessato modifica la riga che ha mandato lui (T11); il partecipante risponde al
  suo filo (T14); **la prima alternativa, e solo in modifica**, con tre condizioni — tenuta su un dipartimento della riga **con lo
  scope della riga** (`IHasResourceScope`), **mai dall'interessato** (`IHasStakeholder`), **i dipartimenti uguali prima e dopo** —;
  poi `{Area}.Edit` su almeno un dipartimento della riga, e anche su quelli di prima se la riga si sposta. **L'eliminazione chiede
  sempre `Edit`.**
- **Chi la usa oggi**: `ContactMessage` (`Contacts.View`, T14: chi legge la coda risponde, e la risposta sposta lo stato) e il PIREP
  dei tour (`Tours.Validate`, T13: il validatore abilitato su un tour decide i suoi PIREP). Tutti e due sono `ISubmittedByMembers`: li
  crea chiunque, e l'alternativa serve solo in modifica.

## 2. Che cosa serve al training

Il design (§3.4) lo dice due volte:

- **il training** (A7) lo scrivono tre permessi senza `Training.Edit`: `Approve` (i TA), `Assign` (TC e TAC; i capi FIR in A11),
  `Conduct` (il trainer, con lo scope del suo training, §3.3). Tre alternative sulla **stessa** entità;
- **un esame** (A10) lo **crea** chi l'ha assegnato su IVAO, con `Training.ManageExams` (TC, TAC, TA e trainer; nota di A0 §2 punto
  4). Un'alternativa che vale **anche alla creazione**.

## 3. Che cosa si fa

### 3.1 L'attributo si ripete

`[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]`. Il guardiano prova **ogni** alternativa dell'entità e **ne basta
una**; ognuna con le stesse tre condizioni di oggi (§1). Il training di A7 dichiarerà:

```csharp
[AlsoWrittenWith(TrainingPermissions.Approve)]
[AlsoWrittenWith(TrainingPermissions.Assign)]
[AlsoWrittenWith(TrainingPermissions.Conduct)]
```

### 3.2 Anche alla creazione: una proprietà dell'attributo

`AlsoOnCreation`, falsa se non si scrive:

```csharp
[AlsoWrittenWith(TrainingPermissions.ManageExams, AlsoOnCreation = true)]
```

Alla creazione il guardiano prova **solo le alternative segnate**, e ognuna:

- **senza scope**: conta chi tiene il permesso sul dipartimento, non su una riga. La riga nuova non ha ancora uno scope suo — o non ha
  ancora l'identificatore (`training:exam:0`, che il database dà dopo), o risponde con lo scope di ciò che le sta sopra (un PIREP con
  quello del suo tour). Nel primo caso un grant con scope nominerebbe una riga che non esiste; nel secondo un permesso dato su una
  riga ne farebbe nascere altre sotto di lei, che è più largo di quanto deciso. Se un giorno servisse, è un'altra nota;
- **su almeno uno dei dipartimenti della riga, come `Edit`**: il dipartimento di base del modulo è già nella riga quando il guardiano
  la guarda (`ModuleBaseDepartment.Keep` gira prima);
- **mai per una riga che riguarda chi la scrive** (`IHasStakeholder`), come in modifica.

**Perché una proprietà e non un attributo a parte**:

1. **il permesso è lo stesso**: un `[AlsoCreatedWith(X)]` dovrebbe ripetere `[AlsoWrittenWith(X)]` accanto (lo stesso permesso
   scritto due volte), oppure valere anche in modifica da solo (due attributi per la stessa cosa, e il guardiano che li legge tutti e
   due);
2. **la segnatura è di un'alternativa, non dell'entità**: il piano chiede che una riga nuova la crei «chi ha l'alternativa segnata
   anche alla creazione, non chi ha l'altra». Un'interfaccia dell'entità, sul modello di `ISubmittedByMembers`, direbbe «tutte le mie
   alternative creano», e sul training `Approve`, `Assign` e `Conduct` non devono creare niente;
3. **chi lo usa oggi non cambia una riga**: il valore predefinito è quello di oggi.

### 3.3 Che cosa non cambia

- **L'eliminazione resta di `Edit`**, per ogni alternativa, anche per quella che crea: la decisione dice «anche alla creazione», e
  l'eccezione di `ISubmittedByMembers` dice lo stesso della riga che un membro manda («deleting is still the department's»). ⚠️ **Da
  sapere per A10**: se un esame lo eliminano solo TC e TAC, A10 lo dice con `CrudOptions.DeletePolicy = Training.Edit` (T6a), e il
  guardiano è già d'accordo; se deve poterlo eliminare anche chi l'ha inserito, è un'altra estensione del nucleo (un'alternativa anche
  all'eliminazione). La domanda è in §3.5, posta a Carmine in anticipo su A10.
- **`ContactMessage` e il PIREP**: un'alternativa ciascuno, non segnata; il guardiano fa per loro esattamente ciò che faceva. I test
  che passano dal guardiano con quelle alternative — `ContactThreadTests` (chi legge soltanto risponde), `PirepTests.Review` (il
  validatore di un tour), `PersonalTokenTests` (l'agente di un validatore su una riga di prova) — restano verdi senza essere toccati.
- **Il superadmin e l'anonimo** passano come prima; **l'errore** è lo stesso, `ForbiddenDomainException` con `{Area}.Edit`, il
  permesso che basta sempre.

### 3.4 Il modulo di prova e i test della spina dorsale

- **`SampleRecord`** (`smp_records`), nel modulo di prova: in cura di più dipartimenti, con scope (`sample:record:{id}`) e interessato,
  scritto da `Sample.Decide` (in modifica, come un trainer conduce il suo training) e da **`Sample.Record`**, nuovo nel catalogo del
  modulo di prova (anche alla creazione, come chi esamina inserisce un esame). La migrazione `AddSampleRecords` è del **contesto del
  modulo di prova**, che sta nei test: nessun contesto dell'hub né di un modulo migra. Lo snapshot prende anche le tabelle che
  `ModuleDbContext` mappa fuori dalle migrazioni da T14 e T19a (i fili dei contatti, l'audit): lo scarto innocuo già visto in T4b.
- **`AlternativeWritePermissionTests`**, sul guardiano **senza un endpoint davanti**, come `DomainBackboneTests`, sulla MariaDB vera:
  ogni alternativa scrive la riga del suo scope e non un'altra; l'interessato non la scrive con nessuna, e non ne crea una su di sé;
  una riga nuova la crea chi ha l'alternativa segnata, non chi ha l'altra, non chi la tiene su una riga sola, e non su un dipartimento
  dove non la tiene; nessuna alternativa sposta o elimina una riga; `Edit` continua a bastare.
- **Chi scrive, nei test**: l'identità che un login mette nel cookie (`HubClaims.BuildIdentity`, come fa `TestSignIn`), letta dal vero
  `HttpContextCurrentUser` della richiesta. **Non `TestCurrentUser`**: tiene solo i permessi del nucleo, e il suo `Has` non passa lo
  scope della riga a `PermissionSet` (un permesso con scope non raggiunge mai una riga), che è proprio ciò che questi test provano. È
  un file del maintainer: non si tocca, e lo si dice al revisore (§5).

### 3.5 Una domanda per Carmine: chi elimina un esame (Proposta)

Posta a Carmine con un commento su #131 il 25 settembre, in anticipo su A10, come ha chiesto `dalberone`: se la risposta tocca il
nucleo, la porta questa PR e non una seconda.

Con A3 un esame lo **crea** e lo **cambia** chi ha `Training.ManageExams` (TC, TAC, TA e trainer; nota di A0 §2 punto 4); **eliminarlo
resta di `Edit`** (TC e TAC). Un esame annullato su IVAO resterebbe quindi nel calendario pubblico finché TC o TAC non lo tolgono; uno
rimandato lo sposta chi l'ha inserito.

1. **Lo eliminano TC e TAC**: in A10 `CrudOptions.DeletePolicy = Training.Edit`, e il guardiano è già d'accordo. Nessun cambio del
   nucleo; chi esamina chiede a TC o TAC di togliere un esame annullato.
2. **Eliminare un esame vuol dire annullarlo**: in A10 `CrudOptions.Delete` scrive `cancelled_at` invece di togliere la riga, come la
   libreria dei media ridefinisce già l'eliminazione. È una modifica della riga, che `ManageExams` fa con la sua alternativa; la voce del
   calendario sparisce (un esame annullato non proietta niente) e la riga resta, nel registro, con chi l'ha annullato. Nessun cambio del
   nucleo; la riga va via solo con la cancellazione dei dati di una persona.
3. **Elimina anche chi ha `ManageExams`**: un'estensione del nucleo — un'alternativa che vale anche all'eliminazione (per esempio
   `AlsoOnDeletion = true`) — aggiunta a questa PR prima del merge, con il suo test della spina dorsale. «Eliminare è di `Edit`»
   avrebbe un'eccezione.

**Raccomandazione: la 2.** Chi esamina toglie dal calendario un esame annullato il giorno stesso, il guardiano non si allarga, e
«eliminare è di `Edit`» resta vero ovunque. Se Carmine preferisce il minimo, la 1.

Il resto di A3 non ne dipende: solo la risposta 3 cambia il suo codice, e aspetta la risposta. La risposta entra qui con il link al
commento di Carmine; se questa PR viene unita prima, la domanda torna in apertura di A10.

## 4. Alternative scartate

| Alternativa | Perché no |
|---|---|
| Un attributo a parte, `[AlsoCreatedWith]` | lo stesso permesso scritto due volte, o due attributi per una cosa (§3.2) |
| Un'interfaccia dell'entità, «le mie alternative creano» | vale per tutte le alternative, e sul training nessuna deve creare (§3.2) |
| Alla creazione con lo scope della riga | la riga nuova non ne ha uno suo; un permesso dato su una riga ne farebbe nascere altre (§3.2) |
| Un'alternativa che elimina, già ora | non è nella decisione; è la risposta 3 della domanda di §3.5, non raccomandata |
| Nel modulo, una tabella per ruolo o un controllo suo prima del salvataggio | `CLAUDE.md` §5 caso (b): il meccanismo c'è e si estende (design §3.4) |
| `Training.Edit` ai TA e ai trainer | «tutto su ogni training», molto più largo del bisogno (nota di A0, §3) |
| I test con `TestCurrentUser`, corretto perché passi lo scope | un doppio del maintainer, toccato per far passare test nuovi (`CLAUDE.md` §0 regola 3) |

## 5. Trovato, per il revisore

Non cambiato: è il comportamento di oggi, e A3 lo ripete per ogni alternativa, come deciso.

1. **L'interessato si guarda solo dopo la scrittura.** Una riga che riguarda X, riscritta da X con un'alternativa perché riguardi Y,
   passa il guardiano (l'eccezione di `ISubmittedByMembers` guarda prima e dopo). Ci arriva solo un endpoint che non chiede l'handler
   **e** lascia cambiare l'interessato: nessuno lo fa oggi, e il training non cambia mai il suo trainee. Se Carmine lo vuole, è una
   condizione in più e un test.
2. **Lo scope si guarda solo dopo la scrittura**, allo stesso modo: una scrittura che cambia lo scope della riga si controlla sul nuovo.
   Oggi nessuna riga lo cambia (il PIREP scrive `ScopeTourId` una volta, il training e le righe di prova hanno quello del loro id).
3. **`TestCurrentUser.Has` non passa lo scope** a `PermissionSet.Has` (§3.4): con i permessi senza scope che il doppio sa dare è
   indifferente, con uno scope risponderebbe sempre no.

## 6. Che cosa si tocca

- **Il nucleo**: `src/IvaoHub.Core/Division/DomainContracts.cs` (l'attributo) e `src/IvaoHub.Core/Data/HubSaveChangesInterceptor.cs` (il
  guardiano).
- **Il modulo di prova**, file che ci sono (nucleo per `core-guard`, perché condivisi): `tests/IvaoHub.IntegrationTests/SampleItems.cs`
  (il contesto: l'insieme e la tabella), `SampleModule.cs` (il permesso nel catalogo), `Migrations/SampleDbContextModelSnapshot.cs`.
- **File nuovi**: `SampleRecords.cs`, la migrazione `AddSampleRecords`, `AlternativeWritePermissionTests.cs`. **Nessun test che c'era è
  cambiato.**

## Da portare nel piano

- **§16 punto 2** (l'unico handler e la rete dell'interceptor) e **`CLAUDE.md` §2**: la rete dell'interceptor accetta **più**
  permessi alternativi dichiarati dall'entità (`[AlsoWrittenWith]`, ripetibile), ognuno con lo scope della riga, mai per
  l'interessato, senza spostare la riga; uno segnato **`AlsoOnCreation`** vale anche alla creazione, senza scope e su almeno un
  dipartimento della riga; l'eliminazione resta di `Edit` (salvo la risposta 3 di §3.5). È la prima delle due estensioni dell'handler
  annunciate per M3 (la seconda, i capi FIR, in A11).
