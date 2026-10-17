# Il permesso tenuto senza un ambito, nel nucleo (E10u)

**Data:** 17 ottobre 2026 — fase E10u di M4, PR del nucleo (#288)
**Stato:** **decisa** da Carmine: alla revisione della #283 (E13b), punto 1, ha scelto la **(b)** — «per chi vede i due registri la b»
(17 ottobre 2026, in chat al master, che l'ha pubblicato sulla PR su sua istruzione: [risposta][a283]): i registri dei controllori e dei
piloti rispondono solo a chi tiene il permesso **senza un ambito su una riga** (lo staff del dipartimento, i grant alle posizioni, i team
dei FIR, l'HQ e il super amministratore); un grant su un evento o su una riga non li apre, **né le loro voci di menu**. La forma qui sotto
è una scelta tecnica della fase, sopra quella risposta. La fase del nucleo l'ha scelta `dalberone` (17 ottobre 2026), perché la regola 6
di `CLAUDE.md` §0 tiene un pezzo del nucleo fuori dalla PR del modulo.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: si estende un meccanismo che c'è — l'unico insieme delle regole dei permessi
(`PermissionSet`, con `Has`/`HasAny`) e il menu che il bootstrap costruisce (`NavItemDescriptor`, `MeEndpoints.Visible`) — invece di
scrivere nel modulo un controllo a mano. È una PR del nucleo, prima di E13b che la usa: **nessun uso negli eventi qui**.

[a283]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/283#issuecomment-6230728736

## 1. Che cosa serve, e perché il modulo non ne fa a meno

- **E13b** (#283) ha due letture di tutta la divisione: `/api/events/atc/register` dietro `EventAtc.View`, `/api/events/bookings/register`
  dietro `EventBookings.View`. Senza una riga in mano, l'unico handler risponde «lo tiene da qualche parte?» (`HasAny`,
  `HubAuthorization.cs`, il ramo senza `IOwnedByDepartment`), e un grant su **un evento** (design M4 §6.2) basta: chi l'ha legge ogni
  pilota della divisione, con i nomi, i non volati e «limitato fino a». Carmine ha risposto (b).
- **La voce di menu** ha lo stesso difetto: `MeEndpoints.Visible` mostra una voce a chi tiene il suo permesso **dovunque**
  (`user.HasAny`), e `NavItemDescriptor` ha solo `Permission`. Una voce che porta a un 403 «insegna a ignorare il menu» (il commento di
  `NavItemDescriptor`).
- **Il nucleo non ha la domanda**: `PermissionSet` ha `Has` (con una riga) e `HasAny` (senza), e l'ambito lo legge solo il motore delle
  liste, da sé, in `TryNarrowToDepartments`. Scriverla nel modulo vorrebbe dire leggere a mano `ICurrentUser.Permissions` e
  `ResourceScope`: una seconda copia della regola, che `PermissionSet` esiste per non avere (design M0 §3.3).

## 2. Che cosa c'è, letto nel codice (`main` a `9a78b2c3`)

1. **`EffectivePermission(Name, Department, Source, ResourceScope, Fir, FromOutside)`**: `ResourceScope` non nullo è un permesso **su
   quella riga sola** (`flightops:tour:42`, `events:event:5`; nota `2026-09-15-permessi-su-una-riga-e-chi-ha-interesse`); `Fir` è il FIR
   di un team quando la divisione dice `firStaffScope: own` (nota `2026-09-27-i-capi-fir-sul-loro-fir`); `FromOutside` è un permesso
   tenuto da fuori del suo dipartimento — un grant a una posizione di un altro dipartimento o di tutti, al team di un FIR con
   `firStaffScope: all`, un permesso globale concedibile e la `View` che porta (note `2026-10-01-il-permesso-non-il-dipartimento`,
   `2026-10-01-chi-assegna-gli-award-con-un-grant`).
2. **Il calcolo** scrive `ResourceScope` dal grant (`hub_user_grants.resource_scope`), uguale per un grant a una persona, a una
   posizione o al team di un FIR; la `View` che un `Edit` implica lo eredita; un permesso globale concedibile non lo ha mai (un grant
   così con un ambito non è onorato).
3. **Il cookie** porta l'ambito dopo `@`, il FIR dopo `#` e il segno di «da fuori» come `!` in testa (`HubClaims`); `ReadPermission` li
   rilegge separati; `ParsePermission`, il lettore di prima, legge `!` e `#XXAA` come pezzi dell'ambito.
4. **I doppioni di test**: `TestCurrentUser` (integrazione, del maintainer) implementa `ICurrentUser` membro per membro; il secondo `Has`,
   quello con il FIR, è già un **membro predefinito** dell'interfaccia, ed è così che un doppione non lo deve scrivere.
5. **`NavItemDescriptor(string Key, string Path, string? Permission = null)`**: costruito con tre argomenti in ogni modulo e in
   `SampleModule`; nessuno lo decostruisce.

## 3. La forma

```csharp
// PermissionSet (Core/Auth/Permissions/EffectivePermissionsCalculator.cs), accanto a HasAny
public static bool HasUnscoped(IEnumerable<EffectivePermission> permissions, bool isSuperadmin, string permission) =>
    isSuperadmin || permissions.Any(held => held.Name == permission && held.ResourceScope is null);

// ICurrentUser, accanto a HasAny: un membro predefinito, che HttpContextCurrentUser scrive uguale
bool HasUnscoped(string permission) => PermissionSet.HasUnscoped(Permissions, IsSuperadmin, permission);

// Core/Modules/IModule.cs
public sealed record NavItemDescriptor(string Key, string Path, string? Permission = null, bool Unscoped = false);

// MeEndpoints.Visible
entry.Permission is null || (entry.Unscoped ? user.HasUnscoped(entry.Permission) : user.HasAny(entry.Permission))
```

1. **«Senza ambito» vuol dire `ResourceScope` nullo, e solo quello.** Per ogni titolare (le letture che la fase doveva dire):
   - **lo staff di un dipartimento** (i permessi del ruolo sul suo dipartimento, e quelli di un modulo dati alle sue posizioni da
     `positionGrants`): **sì**;
   - **un grant a una persona su un dipartimento, o su tutti**, senza riga: **sì** — è tenuto su tutto il dipartimento, come lo staff;
   - **un grant a una posizione**, sul suo dipartimento: **sì**; **su un altro dipartimento o su tutti** (`FromOutside`): **sì**, perché
     Carmine conta «i grant alle posizioni» senza distinguere, e il segno dice quali righe di dipartimento leggono le liste, non che il
     permesso sia su una riga;
   - **un team di FIR**, sul suo FIR (`Fir`) o sul dipartimento (`firStaffScope: all`, `FromOutside`): **sì**, come dice Carmine
     (e la lettura 4 di E13b);
   - **l'HQ** — il direttore e il web team, che tengono tutto senza dipartimento — e **un super amministratore**: **sì**;
   - **un permesso globale** e la `View` che un globale concedibile porta: **sì**, non hanno mai un ambito;
   - **un grant su una riga o su un evento**, a una persona, a una posizione o al team di un FIR: **no**; e la `View` che il suo `Edit`
     implica sulla stessa riga: **no**. Basta però un'altra presa senza ambito dello stesso permesso perché sia sì.
2. **Il FIR e il segno «da fuori» non si guardano**, apposta: il significato che hanno (quali righe di un FIR, quali di un dipartimento
   leggono le liste) resta del motore delle liste e dell'handler. `HasUnscoped` dice solo se il permesso è tenuto **su tutta l'area**
   invece che su righe; che cosa la lettura poi mostri a un team di FIR, o a una posizione da fuori, è del modulo che la scrive (E13b, le
   sue letture 4 e 5).
3. **Un lettore che non conosce i segni legge chiuso**: `ParsePermission` legge `!` e `#XXAA` come un ambito, e con un ambito non è
   «senza». Chiuso, mai «tutta la divisione» (la regola di A11a).
4. **Un membro predefinito di `ICurrentUser`**, come il `Has` con il FIR: `TestCurrentUser` del maintainer non cambia e risponde con lo
   stesso codice; `HttpContextCurrentUser` lo scrive uguale, per chi la chiama dal tipo concreto.
5. **`Unscoped = false` è il predefinito**, e lascia ogni voce di oggi com'era: tenuto dovunque basta. La voce lo dice da sé, accanto al
   permesso; il bootstrap non cambia forma (`NavItem` non porta né il permesso né il segno), quindi niente cambia in `/api/me` né in
   `schema.d.ts`.
6. **L'unico handler non cambia.** La domanda senza riga resta `HasAny`: apre le liste del proprio dipartimento e la coda di un
   validatore su un tour (nota del 15 settembre). Chi legge tutta la divisione lo chiede da sé con `HasUnscoped` e risponde 403 (E13b).

## 4. Alternative scartate

| Alternativa | Perché no |
|---|---|
| **Il controllo nel modulo** (leggere `Permissions` e `ResourceScope` negli endpoint dei registri) | una copia della regola fuori da `PermissionSet`, e la voce di menu non la potrebbe chiedere: il menu lo costruisce il nucleo; e la regola 6 non lo lascia nella PR di E13b |
| **Cambiare `HasAny`**, o il ramo senza riga dell'handler, perché non conti gli ambiti | chiuderebbe la coda dei validatori su un tour (M2), il menu dei capi FIR (A11a) e ogni lista che un grant su una riga apre oggi; cambia un comportamento deciso, non lo estende |
| **Una policy nuova** (`PermissionRequirement` con un segno «senza ambito») | un secondo tipo di requisito per lo stesso handler, e i nomi delle policy sono i permessi del catalogo; per due endpoint basta la domanda |
| **Un segno nel catalogo** (`PermissionDescriptor` «solo senza ambito») | sarebbe del permesso intero, ma `EventBookings.View` su un evento deve continuare ad aprire quell'evento: è la lettura, non il permesso, che vuole tutta la divisione |
| **Contare anche il FIR o «da fuori» come un ambito** | contro la risposta di Carmine, che apre i registri ai team dei FIR e ai grant alle posizioni |
| **Il segno della voce come un enum** (`Visibility { Anywhere, Unscoped }`) | un valore in più che nessuno chiede; un `bool` con il predefinito che chiude meno è la forma più piccola |

## 5. Che cosa si tocca

Tutto del nucleo, ed è il perché di questa nota (`core-guard`):

- `src/IvaoHub.Core/Auth/Permissions/EffectivePermissionsCalculator.cs` (`PermissionSet.HasUnscoped`);
- `src/IvaoHub.Core/Auth/ICurrentUser.cs` (il membro predefinito e quello di `HttpContextCurrentUser`);
- `src/IvaoHub.Core/Modules/IModule.cs` (`NavItemDescriptor.Unscoped`);
- `src/IvaoHub.Web/Endpoints/MeEndpoints.cs` (`Visible`);
- `tests/IvaoHub.UnitTests/UnscopedPermissionRulesTests.cs` (nuovo, unità: ogni titolare del §3.1, il cookie, il membro predefinito, il
  lettore vecchio) e `tests/IvaoHub.IntegrationTests/UnscopedNavigationTests.cs` (nuovo, integrazione: un modulo di prova della classe,
  con una voce `Unscoped` e la sua gemella, nascosta a un grant su una riga e mostrata a chi tiene il permesso sul dipartimento).
  **Nessun test esistente cambiato**, `SampleModule` compreso.
- **Nessuna riga** di un modulo; niente nel browser, nessuna migrazione, `/api/me` uguale.

## 6. Per chi chiama (E13b)

- I due endpoint dei registri, dopo la policy che hanno: `if (!currentUser.HasUnscoped(EventsPermissions.AtcView)) return
  Results.StatusCode(StatusCodes.Status403Forbidden);` — e `EventsPermissions.BookingsView` per i piloti (i nomi come li scrive E13b).
- Le due voci: `new NavItemDescriptor("events:nav.…", "/staff/events/…", EventsPermissions.…, Unscoped: true)`.
- Un test di E13b con un grant su un evento rifiutato su tutti e due i registri, come Carmine chiede.

## 7. Dopo la revisione (#288)

Le [osservazioni del revisore](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/288#issuecomment-6234538666) (17 ottobre 2026):
approvabile, niente da correggere. I due punti piccoli sono fatti nei riassunti di `HasUnscoped`: l'ambito è `ResourceScope`, un FIR non
lo è; il dipartimento non si guarda, e che cosa il titolare legge poi lo restringe chi chiama (E13b, come dice il §3.2).

## Da portare nel piano

- **§16.2** (le colonne trasversali e l'unico handler) e il design M0 **§3.3, §3.7** (`Has`, `HasAny`): accanto a «può farlo, in
  generale» la domanda «lo tiene senza un ambito su una riga», `ICurrentUser.HasUnscoped` / `PermissionSet.HasUnscoped`: un
  dipartimento, tutti, un FIR, da fuori, un globale, il super amministratore; mai un grant su righe. Dal 17 ott 2026 (M4, E10u).
- **§16** (il menu nel bootstrap, `/api/me`) e `CLAUDE.md` §2 (la riga «A permission on one row only»): una voce di modulo
  `NavItemDescriptor(…, Unscoped: true)` si mostra solo a chi tiene il permesso senza ambito; il predefinito è quello di sempre.
- **`09-design-m4.md` §6.2** (i grant su un evento) e le letture 4 e 5 di E13b: i registri e le loro voci chiedono `HasUnscoped`.
- **`10-piano-implementazione-m4.md`**: E10u, scritta in questa PR; E13b dipende anche da E10u.
