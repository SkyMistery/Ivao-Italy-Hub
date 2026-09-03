# «Raggiunge ogni dipartimento» è un fatto del ruolo, non una forma della lista dei permessi

**Data:** 3 settembre 2026 — revisione senior di fine F4, prima di aprire F5
**Stato:** decisa e implementata

## Il problema

Il design §3.3 definisce `HasAllDepartments` **per ruolo**:

> `bool HasAllDepartments { get; }  // Director (DIR/ADIR), Web (WM/AWM) o superadmin: vede/scrive ogni dipartimento`

L'implementazione di F2 lo **deduceva** invece dalla forma della lista dei permessi effettivi:
«esiste almeno un permesso non-globale con dipartimento `null`, quindi raggiunge tutto».

La deduzione sbagliava in **tutte e due** le direzioni.

**Troppo largo.** `RolePermissionMatrix.ReadsEveryDepartment` dà a una posizione di IVAO HQ
(`HQ-…`) il permesso `Content.View` con dipartimento `null`, perché legge il contenuto di ogni
dipartimento. Quel `null` faceva scattare la deduzione, e con essa `SeesEveryDepartment` nel global
query filter — che è il ramo `all`, quello che **cortocircuita l'intera clausola di visibilità**.
Uno staffista HQ, che deve solo leggere, si ritrovava a vedere anche le righe `Visibility.Staff` e
`Visibility.Department` di ogni dipartimento.

**Troppo stretto.** `EffectivePermissionsCalculator.Deny` espande un permesso con dipartimento
`null` nell'elenco esplicito dei dipartimenti superstiti — è l'unico modo perché un deny su un
dipartimento morda quando il permesso è tenuto ovunque. Ma l'espansione **consuma** proprio le
entrate `null` da cui la deduzione leggeva la risposta. Un Director con un deny su abbastanza
permessi di un dipartimento perdeva `HasAllDepartments`, e con esso la lettura di tutti i
dipartimenti tranne quelli del suo claim `dept` — che per `IT-DIR` è **solo `HQ`**. Un deny su un
dipartimento ne chiudeva sette.

E non sarebbe rimasto un problema di sola lettura: il design §3.9 fa poggiare su quel flag anche il
filtro di dipartimento di `MapCrud`, che arriva in F5 —

> filtro di dipartimento sulla lista (`ICurrentUser.Departments`, o nessun filtro se
> `HasAllDepartments`; utenti senza dipartimenti né `HasAllDepartments` → **403**)

— quindi lo stesso Director avrebbe preso **403** sulla lista di ogni dipartimento, non una lista
più corta.

## La decisione

`HasAllDepartments` diventa un **claim dedicato**, `alldept`, scritto da `HubClaims.BuildIdentity`
a partire dalle posizioni:

```csharp
if (isSuperadmin || materialised.Any(RolePermissionMatrix.ReachesEveryDepartment))
{
    identity.AddClaim(new Claim(AllDepartments, "1"));
}
```

Calcolato dentro `BuildIdentity` e non dal chiamante, perché quella è già «l'unico posto in cui si
compone un cookie dell'hub»: il login vero e il login finto dei test non possono discordare.
`HttpContextCurrentUser` lo rilegge e basta, senza più guardare la lista.

L'espansione del deny resta com'è: serve, ed è tornata innocua adesso che non è più la risposta a
una domanda diversa da quella che pone.

## Conseguenze volute

- **Una posizione di IVAO HQ non raggiunge più ogni dipartimento.** Continua a essere staff e a
  tenere `Content.View` ovunque, quindi passa la policy dappertutto; ma il filtro di visibilità
  torna a valere per lei, e le righe che un dipartimento tiene per sé restano del dipartimento.
  È la lettura **più restrittiva** delle due, coerente con la convenzione di HANDOFF §6 («ho scelto
  sempre l'opzione più restrittiva, così una correzione può solo allargare i permessi»). Se in M1 si
  decide che HQ deve vedere anche quelle, si allarga di proposito.
- **Un deny non tocca più la portata del ruolo.** Toglie il permesso su cui è scritto, sul
  dipartimento su cui è scritto, e nient'altro.

## Alternative scartate

- **Un terzo flag per «legge ogni dipartimento»**, distinto da «li raggiunge». Aggiungeva un
  concetto al design per servire un solo ruolo che in M0 non ha ancora schermate. Se HQ avrà davvero
  bisogno di leggere le righe dipartimentali, quello sarà il momento di introdurlo, con un caso
  d'uso in mano.
- **Marcare `Content.View` come globale nel catalogo.** Avrebbe tolto HQ dalla deduzione senza
  toccarla, ma `Content.View` è dipartimentale per tutti gli altri: renderlo globale avrebbe rotto
  la regola del catalogo («ogni area dipartimentale dichiara sempre `View` ed `Edit`») per
  aggiustare un effetto collaterale altrove.
- **Non espandere il deny e trattarlo come un filtro a valle.** Avrebbe salvato la deduzione, ma il
  calcolo dei permessi effettivi sarebbe smesso di essere una lista chiusa: ogni consumatore
  avrebbe dovuto ricordarsi di applicare anche i deny.

## Test

`ReachesEveryDepartmentTests` fissa le sei risposte (director, coordinatore, superadmin, HQ, deny
singolo, deny su tutti i permessi dipartimentali) leggendo il cookie vero costruito da
`BuildIdentity`, non un oggetto finto.
