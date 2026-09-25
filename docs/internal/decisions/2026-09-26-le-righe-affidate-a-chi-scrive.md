# Le righe affidate a chi scrive: un permesso che vale solo per chi ha la riga (A3b)

**Data:** 26 settembre 2026 — fase A3b di M3, PR del nucleo, in coda dopo la #131 (A3)
**Stato:** **Proposta.** Due domande a Carmine (§5), con un commento sulla PR. La prima decide la forma nel codice, e **il codice
aspetta la risposta**; la seconda riguarda A7 e non cambia il codice di questa fase.
**Regola applicata:** `CLAUDE.md` §5, caso **(c)**: un meccanismo nuovo nell'unico handler e nel guardiano dell'interceptor, che sono
spina dorsale (piano §16.2). Lo chiede la risposta 4 di Carmine sulla #131 ([il suo commento][c4]; nota
`2026-09-25-i-permessi-alternativi-e-la-creazione` §3.5); `08-piano-implementazione-m3.md`, A3b.

[c4]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/131#issuecomment-5840224757

## 1. Che cosa serve

- **La regola del TD, scelta da Carmine** (la 4): un esame lo **cambiano e lo tolgono** dall'hub **HQ, TC, TAC e il TA a cui è
  assegnato**, nessun altro. Toglierlo è solo una rimozione dal calendario: l'annullamento vero si fa su ivao.aero.
- **I fatti** (`dalberone`, 26 settembre 2026; `08`, A3b punto 3): un esame si assegna **solo a un esaminatore**, e gli esaminatori
  sono **HQ, TC, TAC e i TA (TA1–9)**, mai i trainer. HQ, TC e TAC hanno già `Training.Edit` su ogni esame: la regola nuova serve ai
  **TA**, ognuno sugli esami **suoi**. Chi tiene `Training.ManageExams` lo propone la nota di A4
  (`2026-09-26-gli-esami-li-inserisce-chi-esamina`, «Proposta», sul branch di A4): TC, TAC e TA1–9, non più i trainer.
- **Alla creazione** vale la n.10: un esame lo inserisce **chi ce l'ha assegnato**.

## 2. Perché nessun meccanismo esistente basta

- `Training.ManageExams` tenuto sul dipartimento raggiunge **ogni** riga del TD: l'handler e il guardiano confrontano i dipartimenti e
  lo scope, non a chi è affidata la riga.
- **Il grant con scope** (nota `2026-09-15-permessi-su-una-riga-e-chi-ha-interesse`) nomina una riga che esiste già. Un grant per
  esame, scritto all'inserimento, farebbe rientrare l'esaminatore a ogni esame (il security stamp); e alla creazione A3 chiede il
  permesso senza scope, quindi un grant su una riga non ne crea.
- **Eliminare** resta di `Edit` per ogni alternativa (A3, nota §3.3).
- **Un controllo nel modulo** («chi scrive è l'esaminatore?» in un `BeforeSave`) sarebbe il «may this user edit this row?» scritto a
  mano che `CLAUDE.md` §2 vieta, e il guardiano lascerebbe comunque scrivere ogni esame a chi tiene il permesso.
- In A0 questa strada — «l'assegnatario della riga», un pezzo nuovo nell'unico handler — era stata **scartata** per il trainer,
  perché il grant con scope copriva già quel caso (nota `2026-09-25-chi-conduce-e-chi-scrive-un-training` §3). Per gli esami il grant
  non basta, e Carmine ha scelto la regola.

## 3. La proposta (domanda 1)

### 3.1 La riga dice a chi è affidata

`IHasAssignee { int? AssigneeVid { get; } }` in `Core/Division/DomainContracts.cs`, accanto a `IHasStakeholder` e
`IHasParticipants`: il VID della persona a cui la riga è affidata, o nessuno. Un esame risponde con il suo esaminatore
(`int? IHasAssignee.AssigneeVid => ExaminerVid;`), e la colonna resta quella del modulo, con un nome che finisce in `Vid`
(`PersonColumns`). **Una persona sola**: nessun uso di oggi ne chiede di più.

### 3.2 Il permesso si segna nel catalogo

`PermissionDescriptor.OnlyForAssignee`, gemello di `DeniedToStakeholder`. Un permesso segnato **raggiunge una riga di un dipartimento
solo se la riga è affidata a chi lo chiede**. Su ogni altra riga — affidata a un altro, a nessuno, o di un'entità che non dice a chi è
affidata — **conta come `{Area}.Edit`**: la raggiunge chi tiene anche `Edit` sulla riga (HQ, TC, TAC, il superadmin), e nessun altro.
È lo stesso ripiego del guardiano, dove `Edit` scrive comunque ogni riga dell'area.

- **Nel catalogo, e non sull'entità**: è un fatto del permesso, come «l'interessato non lo usa», e l'unico handler legge già il
  catalogo. **L'interfaccia da sola non basta**: su una riga affidata a qualcuno altri permessi valgono per tutti (su un training
  `Approve` e `Assign` non dipendono dal trainer, domanda 2).
- **Mai su un permesso che legge**: la lista si restringe in SQL per dipartimento e mostrerebbe comunque le righe degli altri. Il
  catalogo lo rifiuta quando si compone, come un nome dichiarato due volte.

### 3.3 Nell'unico handler

In `DepartmentAuthorizationHandler`, dopo i dipartimenti, lo scope e il FIR: se il permesso è segnato e la riga non è affidata a chi
chiede, la risposta è quella che la stessa funzione dà per `{Area}.Edit` sulla stessa riga, con tutte le sue regole (anche «negato
all'interessato»). **Il motore CRUD non cambia**: chiede il permesso di scrittura sulla riga prima e dopo il payload, e prima di
eliminare, quindi l'endpoint risponde come il guardiano.

### 3.4 Nel guardiano

In `IsWrittenWithAnAlternative`, per un'alternativa il cui permesso è segnato:

- **in modifica**, la riga è affidata a chi scrive **prima e dopo** la scrittura, come nell'eccezione di `ISubmittedByMembers`. Chi ha
  la riga non la passa a un altro e non prende quella di un altro: lo fa chi ha `Edit`;
- **alla creazione** (`AlsoOnCreation`, come in A3: senza scope, su almeno un dipartimento della riga), la riga nuova è affidata a chi
  la crea. Un TA inserisce i suoi esami; HQ, TC e TAC quelli di chiunque;
- **all'eliminazione**, con **`AlsoOnDeletion`**, proprietà nuova di `[AlsoWrittenWith]` e gemella di `AlsoOnCreation`: la toglie la
  persona a cui era affidata. **Conta solo per un permesso segnato**; su ogni altro non conta, e l'eliminazione chiede `Edit` come in
  A3. Così la risposta 3 della #131, che Carmine non ha scelto, resta fuori. La segnatura è dell'alternativa e non del permesso,
  perché eliminare non segue sempre dall'avere la riga: un trainer non deve togliere il suo training.

Tutto il resto come in A3: mai all'interessato, mai per spostare la riga fra dipartimenti, e l'errore è lo stesso
(`ForbiddenDomainException` con `{Area}.Edit`). Il guardiano riceve il catalogo dal contenitore, come l'handler.

### 3.5 Che cosa non cambia

- Le alternative che ci sono (`ContactMessage`, il PIREP, `SampleItem`, `Sample.Decide` e `Sample.Record` su `SampleRecord`) non hanno
  permessi segnati né `AlsoOnDeletion`: il guardiano fa per loro ciò che faceva, e i loro test restano verdi senza essere toccati.
- **I due punti trovati in A3** (l'interessato e lo scope guardati solo dopo la scrittura) restano come sono: sono un compito di
  rafforzamento del maintainer. La regola nuova non li ha, perché guarda chi ha la riga prima e dopo.
- Nessuna migrazione del nucleo, e nessun cambio del cookie, dei grant, di `positionGrants` o di `/api/me`.

### 3.6 Gli esami in A10

```csharp
[AlsoWrittenWith(TrainingPermissions.ManageExams, AlsoOnCreation = true, AlsoOnDeletion = true)]
public sealed class Exam : IOwnedByDepartment, IAuditable, IHasAssignee /* … */
{
    public int ExaminerVid { get; set; }

    int? IHasAssignee.AssigneeVid => ExaminerVid;
}
```

Con `new(ManageExams, IsGlobal: false, OnlyForAssignee: true)` nel catalogo del modulo, e `MapCrud` con
`WritePolicy = Training.ManageExams`, senza `DeletePolicy`. ⚠️ **Per A10**: la lista la leggono tutti con `Training.View`, quindi un TA
vede anche gli esami degli altri, e un'azione su una riga non sua ha un 403: la schermata deve saperlo (per esempio da un campo della
riga). Come HQ, TC e TAC scelgono l'esaminatore di un esame che inseriscono per un altro lo decide A10. Se i trainer tenessero ancora
`ManageExams` (la nota di A4), ognuno raggiungerebbe solo gli esami affidati a lui, ma potrebbe inserirne uno per sé: per questo A4
glielo toglie.

### 3.7 Il modulo di prova e i test della spina dorsale

- **`SampleRecord`** guadagna `AssigneeVid` (la migrazione `AddSampleAssignee`, del solo contesto di prova) e una terza alternativa,
  **`Sample.Manage`**, segnata `OnlyForAssignee` nel catalogo del modulo di prova, con `AlsoOnCreation` e `AlsoOnDeletion`: come un TA
  con i suoi esami.
- **I test**, sulla MariaDB vera e con l'identità del cookie come in A3 (VID da 790040): una riga affidata a X la cambia e la toglie X
  con `Sample.Manage`, e non Y che lo tiene allo stesso modo; X crea una riga affidata a sé e non una affidata a Y; X non passa la sua
  riga a Y e non prende quella di Y; `Edit` basta ancora, anche su una riga affidata a un altro; l'handler e il guardiano rispondono
  uguale in ogni caso (l'handler chiesto sulla riga, come fa il motore); l'interessato resta escluso come in A3; le alternative di A3
  ancora non eliminano. E i test di unità dell'handler e del catalogo: il superadmin, chi tiene un permesso su ogni dipartimento, una
  riga affidata a nessuno, un permesso che legge rifiutato.

## 4. Alternative scartate

| Alternativa | Perché no |
|---|---|
| **La forma 2**: la riga risponde con uno scope derivato dal VID (`training:examiner:{vid}`), e ogni TA tiene il permesso con lo scope del suo VID | `positionGrants` scrive solo il dipartimento: servirebbe uno scope con `{vid}` che il nucleo sostituisce, mentre oggi uno scope non lo interpreta; A3 crea senza scope, quindi il TA non inserirebbe niente senza cambiare A3; il guardiano guarda lo scope solo dopo la scrittura, e senza l'endpoint davanti un TA prenderebbe l'esame di un altro scrivendoci il suo VID (il punto 2 di A3 diventerebbe raggiungibile); una riga ha un solo scope |
| Un grant con scope per esame | un rientro a ogni esame inserito, e alla creazione non basta (§2) |
| Il segno sull'alternativa dell'entità (`[AlsoWrittenWith(..., OnlyForAssignee = true)]`) | l'handler dovrebbe leggere gli attributi dell'entità, che oggi non conosce, e lo stesso permesso sarebbe ristretto su un'entità e libero su un'altra |
| Il segno sul grant («tenuto solo sulle righe affidate»: una colonna di `hub_user_grants`, un campo di `positionGrants`, il claim) | il più preciso (chi ha `Edit` non avrebbe bisogno del ripiego), ma una migrazione, il formato del cookie, il seme e la schermata dei permessi, per un uso solo |
| Eliminare implicito per chi ha la riga | con la domanda 2 un trainer toglierebbe il suo training |
| `AlsoOnDeletion` per ogni alternativa | è la risposta 3 della #131, non scelta; nessuna entità la chiede |
| Un controllo nel modulo | un secondo handler scritto a mano, che il guardiano non conosce (§2) |

## 5. Le domande per Carmine

1. **La forma della regola è quella di §3?** Il VID sulla riga (`IHasAssignee`); il permesso segnato nel catalogo (`OnlyForAssignee`);
   chi non ha la riga ripiega su `{Area}.Edit`, nell'handler come nel guardiano; `AlsoOnDeletion` sull'alternativa, che conta solo per
   un permesso segnato. **Raccomandata: sì.** Da questa risposta dipende il codice di questa PR.
2. **In A7, anche il trainer della n.1 con la stessa regola?** Il training direbbe il suo trainer (`IHasAssignee`), `Training.Conduct`
   sarebbe segnato, e lo terrebbero per posizione i TA1–9 e i T01–T99, oltre a TC e TAC (R.7: si assegna chiunque sia staff del
   training); ognuno raggiungerebbe solo i training affidati a lui.
   - **Si risparmiano**: il grant scritto a ogni assegnazione e tolto a ogni riassegnazione, il **login in più** del trainer ogni
     volta, il job notturno che toglie i grant dei training chiusi, e i grant con scope nel cookie. Riassegnare è cambiare
     `trainer_vid`.
   - **Si perdono**: il grant con scope come traccia nella schermata dei permessi (resta il registro dell'assegnazione), e la
     possibilità di dare `Conduct` a mano su un training a una seconda persona, che oggi nessuno chiede.
   - In A0 questa strada era stata scartata solo perché aggiungeva un pezzo al nucleo, che con A3b c'è comunque.

   **Raccomandata: sì**, con la nota di A7. Il codice di A3b non cambia in nessun caso; con un no, A7 resta com'è in `08`.

## 6. Che cosa si tocca

- **Il nucleo**: `src/IvaoHub.Core/Division/DomainContracts.cs` (`IHasAssignee`, `AlsoOnDeletion`),
  `src/IvaoHub.Core/Auth/Permissions/CorePermissions.cs` (`OnlyForAssignee`), `PermissionCatalog.cs` (la lettura, e il rifiuto di un
  permesso che legge), `HubAuthorization.cs` (l'handler), `src/IvaoHub.Core/Data/HubSaveChangesInterceptor.cs` (il guardiano).
- **Il modulo di prova**: `SampleRecords.cs`, `SampleModule.cs`, la migrazione `AddSampleAssignee` e lo snapshot.
- **File nuovi**: i test della spina dorsale e di unità. **Nessun test che c'era cambia.**

## Da portare nel piano

- **§16 punto 2** e **`CLAUDE.md` §2** (la riga «A permission on one row only»): un permesso segnato `OnlyForAssignee` raggiunge solo
  le righe affidate a chi lo chiede (`IHasAssignee`), nell'unico handler e nel guardiano, e sulle altre conta come `{Area}.Edit`;
  `AlsoOnDeletion` lo lascia anche eliminare, solo a chi ha la riga.
- **§9.2, riga Training**: un esame lo cambiano e lo tolgono HQ, TC, TAC e il TA a cui è assegnato (la nota di A3 lo annuncia), con la
  regola di questa nota. Con un sì alla domanda 2, anche il trainer conduce solo i training affidati a lui, senza grant.
