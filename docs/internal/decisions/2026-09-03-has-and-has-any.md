# `ICurrentUser` fa due domande, non una con un parametro opzionale

**Data:** 3 settembre 2026 — fase F4
**Stato:** **decisa da Carmine il 3 settembre 2026**. `01-design-m0.md` §3.3 e §3.7 corrette,
changelog del piano 0.23.

## Il design

§3.3 dà una firma sola:

```csharp
bool Has(string permission, Department? department = null);   // superadmin → true
```

e §3.7, parlando dell'unico handler, dice: «Senza risorsa → `ICurrentUser.Has(name)` (basta un
dipartimento qualsiasi, o globale)».

## Il problema

Le due righe insieme dicono che `Has(name)` — cioè `department = null` — significa «su un
dipartimento qualsiasi». Ma `null` si legge altrettanto bene come «su nessun dipartimento», cioè
«solo se il permesso è globale». Carmine ha sollevato esattamente questa lettura, e ha ragione: dal
nome del parametro non si capisce quale delle due valga.

L'implementazione di F2 aveva scelto la seconda senza dirlo (`Has("Links.Edit")` rispondeva sì solo
a chi teneva il permesso con dipartimento nullo), e nessuno se n'era accorto perché fino a F4 non lo
chiamava nessuno.

## Perché «un dipartimento qualsiasi» serve davvero

Il caso senza risorsa non è teorico: è quello di **ogni lista**. In F5 `MapCrud` in modalità
dipartimentale controlla la policy sulla lista — dove una riga singola non c'è ancora — e poi filtra
per i dipartimenti dell'utente. Se la policy chiedesse il permesso «globale», un coordinatore ED, che
ha `Links.Edit` **su ED**, si vedrebbe negare la lista dei link del proprio dipartimento.

Il dipartimento continua a mordere dove conta: sulla riga, dove l'handler chiama
`Has(permission, resource.OwnerDepartment)`, e nell'interceptor, che passa sempre un dipartimento.

## La decisione

Due metodi, niente da dedurre da un `null`:

```csharp
bool Has(string permission, Department department);   // «su questa riga?» — un permesso con
                                                      // dipartimento nullo vale ovunque
bool HasAny(string permission);                       // «può farlo, in generale?» — un dipartimento
                                                      // qualsiasi, tutti, o globale
```

L'handler usa `HasAny` quando non ha una risorsa e `Has` quando ce l'ha. Un permesso globale sta
sempre in memoria con dipartimento nullo, quindi per lui le due domande coincidono e `HasAny` è
quella che si legge meglio.

Pinnato da `CurrentUserPermissionTests`, che costruisce l'identità vera con `HubClaims.BuildIdentity`
e non un doppione.
