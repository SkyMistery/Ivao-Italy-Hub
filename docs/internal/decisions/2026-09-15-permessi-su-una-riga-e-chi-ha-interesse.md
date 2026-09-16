# Permessi su una riga sola e chi ha un interesse non decide

**Data:** 15 settembre 2026 — fase T0 di M2
**Stato:** il **che cosa** è deciso da Carmine nel design dei tour (`05-design-m2.md` §7.3, §4.1, risposte 15, 17 e 18); la
**forma nel codice** qui sotto è di Claude, da confermare nella revisione della PR di T0.
**Regola applicata:** `CLAUDE.md` §5, caso **(c)**: estende l'unico authorization handler, che è spina dorsale (piano §16.2).
**Estensione del nucleo** n.1 di `05-design-m2.md` §11. Fase **T3**.

## 1. Che cosa serve

1. **Un validatore abilitato su un tour e non su un altro.** «Aggiungi validatore» nella pagina delle statistiche dà
   `Tours.Validate` **su quel tour**. Il validatore vede tutti i PIREP in sola lettura, ma prende e decide solo quelli dei tour
   su cui è abilitato.
2. **Nessuno valida i propri PIREP, superadmin compreso** (risposta 15). Li vede, in sola lettura, con esito ed errori.

## 2. Perché nessun meccanismo esistente basta

- Un grant oggi ha un solo scope: il **dipartimento** (`hub_user_grants.department`, `null` = ogni dipartimento). Un tour è del
  FOD; un grant `Tours.Validate` su FOD abiliterebbe a **tutti** i tour.
- Il handler (`DepartmentAuthorizationHandler`, `Core/Auth/Permissions/HubAuthorization.cs`) confronta i permessi di chi scrive
  con i dipartimenti della riga. Non sa chi è **legato** alla riga, e il superadmin riceve dal calcolo ogni permesso.
- Scritti a mano nel modulo, i due controlli sarebbero proprio l'«authorization handler scritto a mano» che `CLAUDE.md` §2 vieta,
  e un endpoint che se ne dimentica non lo scoprirebbe nessun test della spina dorsale.

## 3. La decisione

### 3.1 Lo scope per risorsa

- **Una colonna in più** sulla riga che esiste: `hub_user_grants.resource_scope` (`varchar(128)`, `null` = nessuna restrizione),
  migrazione additiva. Forma `<modulo>:<tipo>:<id>`, per esempio `flightops:tour:42`. Il nucleo non la interpreta: la confronta.
- **Una risorsa dichiara il suo** con `IHasResourceScope { string ResourceScope { get; } }`. Un PIREP risponde con lo scope del
  suo tour, così il grant sul tour vale per tutti i PIREP del tour senza scriverne uno per PIREP.
- **Il calcolo**: `EffectivePermission` guadagna `ResourceScope`; il claim `perm` diventa `Name:DEPT` oppure `Name:DEPT@scope`.
  `PermissionSet.Has(name, dept)` resta com'è e **non** conta i permessi con scope; `Has(name, dept, scope)` li conta solo con lo
  stesso scope. `HasAny(name)` li conta tutti: è la domanda «ha questo permesso su almeno qualcosa?», che è quella della lettura
  in sola lettura della coda (design §4.1).
- **Il handler**: se la risorsa è `IHasResourceScope`, un permesso vale se c'è senza scope (coordinator, assistant, advisor,
  HQ, superadmin) **oppure** con lo scope della risorsa. Nessun ramo che nomina un modulo.
- **Chi scrive un grant con scope**: solo l'endpoint del modulo che lo possiede («aggiungi validatore», con
  `Tours.ManageValidators`), attraverso lo stesso `UserGrant` e quindi con audit, sospensione dello staff e rinfresco della sessione
  che esistono già (`IAffectsUserSession`). La schermata `/staff/admin/permissions` mostra lo scope in una colonna e **non** lo fa
  scrivere: il suo form non sa quali tour esistono, e un campo di testo libero sarebbe un errore di battitura che abilita il tour
  sbagliato.
- **Il peso nel cookie**: un claim per tour abilitato, una trentina di byte. Un validatore su venti tour aggiunge meno di un
  kilobyte. Se un giorno servisse di più, i permessi con scope si leggerebbero dal database alla richiesta: non ora.

### 3.2 Chi ha un interesse

- **Una risorsa dichiara il suo interessato** con `IHasStakeholder { int? StakeholderVid { get; } }`. Il PIREP risponde con il
  VID del pilota.
- **Quali permessi l'interessato non ha su quella riga** lo dice il **catalogo dei permessi**, non la risorsa:
  `PermissionDescriptor.DeniedToStakeholder`. Per M2 solo `Tours.Validate`. Una sola riga di catalogo invece di un elenco per ogni
  entità, e un permesso nuovo con lo stesso bisogno si segna lì.
- **Il handler** lo controlla **per primo**, prima di ogni altra regola e prima dei permessi da superadmin: se chi scrive è
  l'interessato e il permesso è segnato, la risposta è no. È l'unico punto dove un superadmin riceve un no, ed è voluto.
- **La lettura resta concessa**: `Tours.View` o la regola di lettura dei PIREP non sono segnati, quindi il validatore vede i suoi.

## 4. Alternative scartate

| Alternativa | Perché no |
|---|---|
| Una tabella del modulo `fo_tour_validators` e un controllo nel servizio dei PIREP | un secondo authorization handler; audit, sospensione e sessione da riscrivere |
| Un permesso per tour (`Tours.Validate.42`) | nomi che il catalogo non conosce, e il calcolo scarta i nomi sconosciuti (H1) |
| Usare il dipartimento come scope | un tour non è un dipartimento; spezzerebbe `DepartmentMask` |
| «Stakeholder» come `deny` automatico nei grant | un grant è di un VID o di una posizione, non di «il pilota di questa riga» |
| L'interessato dichiarato dalla risorsa con l'elenco dei suoi permessi negati | lo stesso elenco copiato su ogni entità che ne ha bisogno |

## 5. Che cosa si tocca

- **Codice (T3)**: `UserGrant` + migrazione `AddGrantResourceScope`; `EffectivePermissionsCalculator`, `EffectivePermission`,
  `PermissionSet`, `HubClaims` (formato del claim, lettura compatibile con i cookie vecchi); `DepartmentAuthorizationHandler`;
  `IHasResourceScope` e `IHasStakeholder` in `Division/DomainContracts.cs`; `PermissionDescriptor.DeniedToStakeholder`; la lista
  dei grant con la colonna. `/api/me` porta lo scope nei permessi effettivi, perché la SPA mostri «Prendi» solo dove vale.
- **Test della spina dorsale**: un grant con scope vale sulla risorsa con lo stesso scope e su nessun'altra; uno senza scope vale
  su tutte; `Has` senza scope non conta un permesso con scope; l'interessato riceve no anche da superadmin e sì in lettura; il
  grant con scope sospeso dalla sincronizzazione dello staff smette di valere; la sessione si rinfresca quando lo si scrive.
- **Piano** 0.79: §6.3, §16.2, §16.3. **Design** `05-design-m2.md` §7.3 rimanda qui. **`CLAUDE.md`** §2, una riga nella tabella.
