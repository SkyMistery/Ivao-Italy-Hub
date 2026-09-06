# Il grant di un livello, e il dipartimento che non si raggiunge

**Data:** 6 settembre 2026 — trovata rispondendo alle tre domande della dashboard di dipartimento
**Stato:** **aperta.** La prima metà è decisa nel principio da Carmine (si corregge in G8); la
seconda è progettata qui e vuole una risposta prima di essere scritta.
**Regola applicata:** `CLAUDE.md` §5, caso **(c)** per la seconda metà: meccanismo nuovo, si scrive,
si decide, poi si codifica.

## Cosa serve

Rispondendo a «chi vede la dashboard di un dipartimento», Carmine ha detto: **il proprio, più i
dipartimenti a cui il proprio VID è stato autorizzato ad accedere**. E ha aggiunto la parte che
questo documento esiste per progettare: chi riceve quell'accesso deve riceverlo **a un certo
livello**, e quanto vale un livello **può cambiare da dipartimento a dipartimento**.

## Che cosa esiste oggi, verificato

- Un grant è `UserGrant { Vid, Kind, Value, Department?, Effect, ExpiresAt, SuspendedAt }`, dove
  `Value` è **un nome di permesso** (`Links.Edit`) e `GrantKind` ha **un solo valore**, `Permission`.
- `EffectivePermissionsCalculator` fa «derivati dalle posizioni ∪ grant − deny»: il permesso arriva
  nel cookie con il suo dipartimento, e l'unico authorization handler lo onora riga per riga.
- ⚠️ **Ma i dipartimenti che uno «raggiunge» non li decidono i grant.** `HubClaims.BuildIdentity`
  scrive i claim `dept` **solo dalle posizioni staff**, e su quei claim si reggono due cose:
  il **global query filter** (`HubDbContext.VisibleDepartments`) e il **filtro di dipartimento di
  ogni lista** (`MapCrudExtensions.TryNarrowToDepartments`).

Il risultato di oggi, misurato leggendo il codice: un grant `Content.View` su AOD a un membro
dell'ED gli fa **aprire la riga se ne conosce l'id** — il back-office legge con
`IgnoreQueryFilters` e l'handler dice sì — ma gli fa uscire **la lista vuota**, e gli tiene nascosta
qualunque riga a visibilità `Department` di quel dipartimento.

⚠️ Non se n'è accorto nessuno perché il test di F8 (`AGrantReachesTheNextRequestAndItsRemovalTheOneAfter`)
prova il **dettaglio** e mai la lista. È il caso di HANDOFF §10: un test che guarda metà della cosa.

## Prima metà: raggiungere il dipartimento (decisa, va in G8)

`BuildIdentity` scrive un claim `dept` anche per ogni dipartimento nominato da un grant attivo, non
solo per le posizioni. Il resto non si tocca: il filtro, la lista e l'handler continuano a leggere
ciò che leggono già.

⚠️ **Va detto ad alta voce cosa allarga.** Un claim `dept` non è un permesso: è «questa persona fa
parte di quel dipartimento, ai fini di cosa può *vedere*». Quindi chi riceve un grant qualunque su
AOD comincia a vedere **tutte** le righe `Visibility.Department` dell'AOD, comprese quelle di aree
su cui non ha ricevuto niente. È la lettura giusta di «autorizzato ad accedere a quel dipartimento»,
ed è esattamente ciò che rende vera la risposta sulla dashboard — ma è un allargamento, e chi dà un
grant deve saperlo. Lo scriverà la schermata dei grant.

Costo: due righe in `BuildIdentity`, e **i test che oggi mancano**: la lista di un dipartimento
altrui dopo un grant, e una riga `Department` di quel dipartimento.

## Seconda metà: il grant di un livello (progettata, da decidere)

Oggi autorizzare qualcuno su un altro dipartimento significa **elencare permessi uno per uno**. Con
sei aree nel nucleo e altre nei moduli è già scomodo, e soprattutto è una lista che nessuno rilegge:
il giorno che un'area nuova nasce, chi era «assistente di fatto» sull'AOD non la riceve.

**Forma proposta: `GrantKind.Level`.** L'enum ha un solo valore da M0 e questo è il secondo.

- `Value` è uno `StaffLevel` (`Coordinator`, `Assistant`, `Advisor`), `Department` è obbligatorio.
- Al login si espande con **`RolePermissionMatrix.OnOwnDepartment(level)`**, cioè con la stessa
  tabella che decide quanto vale una posizione. Un grant e una posizione non possono quindi
  divergere: c'è un posto solo che sa cosa vuol dire «assistente».
- Un grant di permesso singolo resta possibile e resta la cosa fine: `Kind = Permission` non va
  in pensione.

**«Quanto vale un livello cambia da dipartimento a dipartimento»** — ed è qui che serve il
meccanismo nuovo. Oggi `RolePermissionMatrix` conosce **solo i permessi del nucleo**: un assistente
del Training ha bisogno anche dei permessi del modulo `training`, che il nucleo non nomina e non
deve nominare (`CLAUDE.md` §2, il nucleo non referenzia un modulo). Quindi:

> **La matrice diventa componibile come il catalogo dei permessi.** `IModule` guadagna «quanto
> valgono i miei permessi a ogni livello, sul mio dipartimento», nella stessa forma con cui già
> contribuisce `Permissions`, `Blocks` e `Widgets`. Il nucleo compone, come fa per
> `PermissionCatalog`.

Così i livelli restano **quattro e universali** — sono la nomenclatura di IVAO, e `StaffRoleMap` è
universale apposta (piano §4.1: è il cardine della forkabilità) — mentre **ciò che valgono** varia
per dipartimento, perché a dirlo è il modulo di quel dipartimento. È la stessa mossa del catalogo
dei permessi, un anno di distanza.

Scartato: un vocabolario di livelli **per dipartimento** (`AOD: [radar, tower]`). Vorrebbe una
tabella di configurazione, romperebbe l'universalità di `StaffRoleMap`, e la prima divisione che
forka si troverebbe a inventarsi i livelli prima di poter dare un permesso.

## Cosa entra in G8 e cosa no (raccomandazione)

- **In G8**: la prima metà. È due righe più i test che mancano, ed è ciò che rende vera la risposta
  sulla dashboard — senza, un coordinatore autorizzato su un altro dipartimento non vedrebbe la
  dashboard di quel dipartimento.
- **Non in G8**: `GrantKind.Level` e la matrice componibile. Tocca `IModule` (cioè ogni modulo
  presente e futuro), l'espansione dei permessi, la schermata dei grant e la migrazione della
  colonna `kind`. G8 è già la fase che il piano dice può prendere due sessioni.
  ⚠️ E c'è un motivo di merito, non solo di taglia: **oggi esiste un solo modulo** (`atc`, a bassa
  complessità e senza permessi propri). Costruire adesso la matrice componibile significa
  progettarla su zero clienti veri; M2 (Events) è il primo che ne ha uno. Una fase sua, subito dopo
  M1 o all'inizio di M2, la costruisce **con il caso davanti** — che è come sono nati i blocchi Data
  e il generatore di form.

## Le domande

1. La prima metà in G8: confermata? (Carmine ha già detto sì; è scritta qui per completezza.)
2. `GrantKind.Level` con la matrice componibile dai moduli: **è la forma giusta**?
3. Si costruisce in una fase sua dopo M1 (raccomandato) o dentro G8?
