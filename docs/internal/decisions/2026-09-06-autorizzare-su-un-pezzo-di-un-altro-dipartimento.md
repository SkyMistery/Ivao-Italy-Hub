# Autorizzare un VID su **un pezzo** di un altro dipartimento

**Data:** 6 settembre 2026 — nata rispondendo alle tre domande della dashboard di dipartimento
**Stato:** **decisa** (Carmine, 6 settembre 2026). La correzione della «portata» entra in **G8**; la
regola sulla granularità vincola i design dei moduli e non apre nessuna fase.
**Regola applicata:** `CLAUDE.md` §5. La prima metà è un difetto da correggere; la seconda si è
rivelata un caso **(b)** — il meccanismo esiste già — dopo essere partita come una (c) sbagliata.

## Cosa serve, con le parole di Carmine

- «i CH possano gestire i training (assegnarli o cambiare trainer) **ma non altro** nel TD»
- «il FOD possa inserire le rotte di un evento **ma non**, ad esempio, toccare le postazioni da aprire»

Cioè: autorizzare qualcuno su **una capacità precisa** dentro un dipartimento che non è il suo.

## La prima proposta era sbagliata, e vale la pena dire perché

La prima stesura di questa nota proponeva `GrantKind.Level`: dare a un VID un **livello**
(«assistente sull'AOD») che si espande nei permessi di quel livello. È **il contrario** di quello
che serve. Un livello è un pacchetto — «tutto quello che può fare un assistente» — mentre i due
esempi qui sopra dicono «questa cosa e nient'altro». Un pacchetto non si può stringere.

`GrantKind.Level` è quindi **scartato**. L'enum resta con un valore solo; se un giorno servirà
davvero dare in blocco «tutto ciò che fa un assistente», sarà una riga in quel momento, con il caso
davanti. Oggi sarebbe codice speculativo che spinge nella direzione opposta a quella richiesta.

## Come si articola davvero, con quello che esiste

Un grant è già **un permesso più un dipartimento**, e il piano §6.3 lo dice con questo stesso caso
d'uso: «uno staffista che aiuta un altro dipartimento (`IT-AOA1` → `Events.Manage`)». Quindi «un CH
assegna i trainer nel TD e nient'altro» **si esprime oggi**, a due condizioni.

### 1. La granularità sta nel catalogo del modulo, non nel grant

Un permesso si chiama `<Area>.<Azione>`, e chi decide quante azioni esistono è il **modulo**, che
dichiara le proprie in `IModule.Permissions`. `Training.AssignTrainer` accanto a `Training.Edit` non
è un meccanismo nuovo: è una riga nel catalogo di quel modulo.

⚠️ **La regola, che vincola ogni design di modulo da qui in avanti:** una capacità che ha senso
delegare a un altro dipartimento **ha un nome suo** nel catalogo. Il momento per accorgersene è il
design del modulo, non il giorno in cui qualcuno chiede il grant e si scopre che l'unico nome
disponibile è `Training.Edit`, cioè «tutto il TD».

### 2. E una capacità delegabile è **una riga sua**, con la sua area

Questa non è una preferenza di stile, è la spina dorsale che lo impone.
`HubSaveChangesInterceptor.EnsureWriteIsAllowed` chiede `<Area>.Edit` **per tipo di entità** —
l'area è il nome del `DbSet` o l'attributo `[PermissionArea]`. Non esistono permessi per **campo**,
e non vanno inventati: il motore CRUD applica un DTO intero e ha un solo gancio per riga
(`ExtraWritePolicy`), non uno per colonna.

Quindi «il FOD scrive le rotte ma non le postazioni» significa che rotte e postazioni sono **due
entità**, non due campi della stessa riga:

| | dove sta la riga | chi la scrive |
|---|---|---|
| **a. la riga è dell'evento** (`OwnerDepartment = ED`), area `EventRoutes` | nello spazio dell'ED | chi tiene `EventRoutes.Edit` su ED: lo staff ED, e chi ha ricevuto **quel** grant |
| **b. la riga è del FOD** (`OwnerDepartment = FOD`) | nello spazio del FOD | lo staff FOD **senza nessun grant**, e le rotte compaiono nelle liste del FOD |

Sono tutte e due esprimibili con quello che c'è oggi, e la scelta è del design di **M2**: (a) tiene
l'evento tutto in casa ED e paga un grant per persona; (b) dice che «le rotte le fa il FOD» come
fatto strutturale e non paga niente. Questa nota non la decide — dice che è una scelta, e che va
fatta guardandola.

### 3. Per i CH c'è una terza via che non costa nessun grant

Un CH è una posizione **di FIR**, e l'hub le conosce: `IHasFir` sull'entità più
`division.json → firStaffScope`, e l'unico authorization handler già restringe una riga con una FIR
a chi quella FIR ce l'ha. Oggi le posizioni FIR non conferiscono nessun permesso — è la lettura più
restrittiva, e quella che si può solo allargare (design M1 §14, debito n.6 di HANDOFF §10).

Se l'assegnazione di un training è una riga che porta una FIR, «i CH gestiscono i training della
propria FIR» è una **regola**, non nove grant scritti a mano che qualcuno dovrà ricordarsi di
revocare. È la domanda che il design di **M4 (Training)** deve porsi esplicitamente.

## Quello che manca davvero, ed è piccolo: la portata

⚠️ Verificato leggendo il codice: `HubClaims.BuildIdentity` scrive i claim `dept` **solo dalle
posizioni staff**, e su quei claim si reggono il global query filter (`HubDbContext.VisibleDepartments`)
e il filtro di dipartimento di ogni lista (`MapCrudExtensions.TryNarrowToDepartments`).

Quindi oggi un grant su un altro dipartimento fa **aprire la riga se se ne conosce l'id** — il
back-office legge con `IgnoreQueryFilters` e l'handler dice sì — ma fa uscire **la lista vuota**, e
tiene nascoste le righe `Visibility.Department` di quel dipartimento. Il test dei grant di F8
(`AGrantReachesTheNextRequestAndItsRemovalTheOneAfter`) prova il dettaglio e mai la lista: è per
questo che non se n'era accorto nessuno.

**Correzione, in G8**: `BuildIdentity` scrive un claim `dept` anche per ogni dipartimento nominato da
un grant attivo. Due righe, più i test sulla **lista** che oggi mancano.

⚠️ **Cosa allarga, detto ad alta voce.** Un claim `dept` non è un permesso: è «questa persona fa
parte di quel dipartimento, ai fini di ciò che può *vedere*». Chi riceve un grant qualunque sull'AOD
comincia quindi a vedere tutte le righe `Visibility.Department` dell'AOD, comprese quelle di aree su
cui non ha ricevuto niente. È la lettura giusta di «autorizzato ad accedere a quel dipartimento» ed è
ciò che rende vera la visibilità decisa per la dashboard — ma chi concede un grant deve saperlo, e la
schermata dei grant lo dirà.

## Le decisioni

1. **La correzione della portata entra in G8**, con i test sulla lista. *(Carmine, 6 set 2026)*
2. **`GrantKind.Level` è scartato.** La risposta ai due casi è l'opposto di un pacchetto: nomi più
   fini nel catalogo del modulo, e una capacità delegabile che è una risorsa sua. *(6 set 2026)*
3. **Nessuna fase nuova.** Non c'è un meccanismo da costruire: c'è una regola che vincola i design di
   M2 e M4, dove stanno i casi veri. *(scelta lasciata a me da Carmine; questa è la valutazione)*
