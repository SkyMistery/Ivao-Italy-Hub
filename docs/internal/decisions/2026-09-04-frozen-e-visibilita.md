# La cattura `frozen` non può essere più visibile della pagina che la contiene

**Data:** 4 settembre 2026 — F7
**Stato:** **decisa** (Carmine, 4 set 2026: «sistema i debiti»). Implementata l'opzione 2.

## Il fatto

Design §5.5: alla pubblicazione, «per ogni blocco `kind = data` con `renderMode = frozen` chiama
`IDataBlockProvider(type).ResolveAsync(props, currentUser)` e scrive il risultato in `frozen`».

`LinkListProvider` legge `cms_links` **senza** `IgnoreQueryFilters`, cioè attraverso il global query
filter di §3.5, che è la cosa giusta: è un lettore come gli altri. Il filtro però risponde a
`ICurrentUser`, e in fase di pubblicazione `ICurrentUser` è **lo staff che sta pubblicando**.

Quindi: un coordinatore che pubblica una pagina `Public` con un `linkList` `frozen` cattura anche i
link `Staff` e `Members` che lui vede — e quella cattura finisce nella versione, che è ciò che legge
un anonimo. Il filtro di visibilità protegge la riga `cms_contents`, non il contenuto che qualcuno
ha copiato dentro la versione.

Non era un buco nel meccanismo: era il meccanismo applicato al momento sbagliato. Il percorso `live`
è corretto per costruzione (`GET /api/blocks/data/{type}` risolve con l'`ICurrentUser` del lettore).

## Cosa si è deciso

**L'opzione 2**: la pubblicazione dice al provider dove finirà la risposta, e il provider si ferma a
ciò che quella pagina può mostrare.

```csharp
public sealed record DataBlockContext(ContentAudience? Page)
{
    public static readonly DataBlockContext Reader;                       // qualcuno sta leggendo adesso
    public static DataBlockContext Publishing(Visibility v, Department d); // sta per essere congelata
}
Task<JsonNode> ResolveAsync(JsonNode? props, DataBlockContext context, CancellationToken ct);
```

`Page` è `null` sul percorso `live`: lì il lettore è il lettore, e il query filter ha già detto
l'ultima parola. È valorizzato solo alla pubblicazione.

Il tetto è una **tabella**, non un ordinamento, in `Core/Division/VisibilityCeiling.cs`:

| pagina | può contenere righe |
|---|---|
| `Public` | `Public` |
| `Members` | `Public`, `Members` |
| `Staff` | `Public`, `Members`, `Staff` |
| `Department` | le tre sopra, più righe `Department` **dello stesso dipartimento** |

Le quattro visibilità non sono davvero una scala — `Department` è più stretta di `Staff` ma indica
un dipartimento in particolare — e una tabella si legge e si prova riga per riga, come
`RolePermissionMatrix`. L'ultima riga è la ragione per cui `Allows` prende anche i due dipartimenti:
«visibile a un dipartimento» nomina persone diverse per ognuno.

## Perché non è una seconda copia del query filter

Rispondono a due domande diverse, e il filtro non può rispondere alla seconda:

- **query filter**: «questo lettore può vedere questa riga?» Resta l'unico a rispondere.
- **`VisibilityCeiling`**: «questa riga può essere *copiata dentro* una pagina che leggerà qualcun
  altro?» Esiste solo perché la pubblicazione copia, ed è la sola cosa che copia.

Un provider che non tiene righe (nessuno, oggi) può ignorare il contesto.

## Cosa si tocca

- `docs/internal/01-design-m0.md` §5.5: la firma di `ResolveAsync` e il tetto.
- `IDataBlockProvider`, `LinkListProvider`, `ContentPublishService`, `ContentEndpoints`.
- Test: `VisibilityCeilingTests` (la tabella riga per riga) e
  `PublishingDoesNotFreezeWhatThePageMayNotShow` (un superadmin che pubblica una pagina pubblica
  cattura il link `Public` e non quello `Staff`, mentre dal vivo li vede tutti e due).

## Nota di contorno, decisa nello stesso giro

`LinkListProvider` trattava un `department` che non riconosce come «nessun filtro». Adesso lo tratta
come «nessuna riga»: una proprietà con un refuso deve restringere a niente, mai allargare a tutto.
Test `ABlockAskingForADepartmentNobodyKnowsShowsNothing`. Lato editor il campo non è più testo
libero ma una select dei dipartimenti, quindi il refuso può arrivare solo da una chiamata API a mano.
