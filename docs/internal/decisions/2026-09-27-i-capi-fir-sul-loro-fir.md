# I capi FIR: un permesso di un modulo al team di un FIR, contato sul suo FIR (A11a)

**Data:** 27 settembre 2026 — fase A11a di M3, PR del nucleo #159
**Stato:** **decisa** (Carmine, 28 settembre 2026, [il suo commento sulla #159][a1]): **sì** alla forma di §3, alla condizione che la nota
correggesse prima del codice i punti 1 e 2 del revisore e rispondesse ai punti 3–5 ([i suoi rilievi][rv]); **la (a)** alla domanda 2. I
cinque punti sono entrati in §3.1, §3.2, §3.4, §3.5, §3.7 e §6 prima del codice (§5). #135 (A3b), che toccava lo stesso handler, lo stesso
guardiano e lo stesso contesto di prova, è unita dal 27 settembre, e `main` è entrato nel branch con un merge.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: si estendono tre meccanismi che ci sono — i grant a una posizione (M2, nota
`2026-09-13-moduli-non-subordinati-ai-dipartimenti` §3.2), la regola del FIR dell'unico handler (`IHasFir` e `firStaffScope`, M0) e il
filtro di dipartimento delle liste generate —, sulla strada che la nota `2026-09-06-autorizzare-su-un-pezzo-di-un-altro-dipartimento` §3
indicava per i CH: «i CH gestiscono i training della propria FIR» come «una regola, non nove grant scritti a mano che qualcuno dovrà
ricordarsi di revocare». **Che** i capi FIR ci siano l'ha deciso Carmine (design `07-design-m3.md` §8 n.2 e §12 n.3, [le risposte sulla
#121][r1]; nota `2026-09-25-chi-conduce-e-chi-scrive-un-training` §2 punto 3); la **forma** nel codice è di questa nota, e apre due
scelte che sono sue.

[r1]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/121#issuecomment-5832705237
[q1]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/159#issuecomment-5857885144
[rv]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/159#issuecomment-5859604416
[a1]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/159#issuecomment-5864855723

## 1. Che cosa serve

- **CH e ACH di un FIR vedono e assegnano i training del loro FIR** (design §3.2): `Training.View` e `Training.Assign`, solo sulle righe
  del loro FIR. Il FIR di un training è quello della sua postazione; un training pilota non ne ha (§1.1). La lista `/staff/training` e il
  blocco `training.approvalQueue` mostrano a un capo FIR solo il suo FIR (§4.2, §4.3).
- **Per posizione, non per persona**: chi diventa capo lo tiene, chi lascia la posizione lo perde, senza grant da scrivere e da togliere a
  ogni cambio.
- **Nell'unico handler e nel guardiano** (§8 n.2), così che l'endpoint e la rete dicano la stessa cosa; e — lo aggiunge questa nota, §2
  punto 5 — **nella lista generata**, o la «lista per FIR» di A11b sarebbe un filtro scritto a mano.
- **Per tutti gli altri non cambia niente**: TC, TAC, TA e trainer vedono e fanno quello che fanno oggi, anche sui training con un FIR.

## 2. Che cosa c'è, letto nel codice

1. **Una posizione FIR non porta permessi.** `StaffRoleMap` riconosce `{FIR}-CH`, `{FIR}-ACH` e `{FIR}-CHA1…9`, per i FIR della divisione,
   come posizioni **senza dipartimento**, con il FIR e il livello (`Coordinator`, `Assistant`, `Advisor`). Il calcolo dei permessi
   effettivi (`EffectivePermissionsCalculator.AddDerived`) non dà niente a una posizione senza dipartimento; il login ne mette i FIR nel
   cookie (il claim `fir`, `ICurrentUser.Firs`).
2. **Un grant a una posizione si scrive per dipartimento.** `UserGrant.PositionDepartment` con `PositionLevels`; `IsHeldThrough` cerca una
   posizione di quel dipartimento, quindi una posizione FIR non tiene mai un grant a una posizione. `positionGrants` scrive `department` e
   `levels`.
3. **La regola del FIR dell'handler è una sola, e ferma tutti.** Con `firStaffScope: own` una riga `IHasFir` con un FIR è rifiutata a
   chiunque non raggiunga ogni dipartimento e non abbia quel FIR fra i suoi: per **ogni** permesso e **ogni** persona. Il piano §4.1 dice
   un'altra cosa: `firStaffScope` è il perimetro **dei team FIR** («con "own" solo quelli della propria»), e «il Director e i coordinatori
   di dipartimento non sono mai limitati per FIR»; il commento di `division.example.json` lo ripete («own = each FIR team only sees its own
   FIR»). Con `own`, il codice di oggi fermerebbe TC, TAC, TA e trainer su **ogni training ATC**: il training ha un FIR, loro no. Non si
   vede perché IT ha `all`, il training è l'unica riga `IHasFir` e nessun test prova la regola.
4. **Il guardiano non guarda il FIR.** Con `own`, l'handler rifiuterebbe una riga che il guardiano lascia scrivere a chi tiene `Edit`.
5. **La lista generata si restringe per dipartimento, e ogni grant porta il suo dipartimento fra quelli «per vedere».** `MapCrud` chiede
   il permesso di lettura senza una riga (`HasAny`) e tiene le righe dei dipartimenti di `ICurrentUser.Departments`
   (`TryNarrowToDepartments`), che vengono dalle posizioni **e** da ogni grant (`HubClaims.BuildIdentity`). Un grant al capo di un FIR sul
   TD gli aprirebbe tutta la lista `/api/training/queue`, e ogni riga `Visibility.Department` del TD.
6. **Nel training** (sui branch della coda): la pagina e i verbi dello staff (`/api/training/trainings/{id}`) e il blocco
   `training.approvalQueue` chiedono l'unico handler sulla riga (`StaffTrainings.MayAsync`), e l'assegnazione passa il guardiano con
   `[AlsoWrittenWith(Training.Assign)]` (A7): quando l'handler e il guardiano conoscono il FIR, lo seguono da soli. La lista
   `/api/training/queue` è `MapCrud` in sola lettura con `Training.View`: segue il motore.

## 3. La forma nel codice (domanda 1)

### 3.1 Il soggetto: il team di un FIR

Un grant a una posizione ha oggi un dipartimento e i livelli. Guadagna **una seconda specie di posizione, il team di un FIR**, con i livelli:
CH è `Coordinator`, ACH `Assistant`, CHA1–9 `Advisor`. **Non nomina un FIR**: vale per il team di ogni FIR, e ognuno lo tiene sul suo
(§3.2). In `division.json`:

```json
{ "firTeam": true, "levels": ["Coordinator", "Assistant"], "permission": "Training.Assign", "scope": "TD" }
```

`scope` resta il dipartimento su cui il permesso vale. Nel database una colonna, `hub_user_grants.position_fir_team`, booleana e falsa per
ogni grant di oggi: una migrazione del nucleo, solo additiva. Un grant ha **un** soggetto — un VID, la posizione di un dipartimento, o il
team di un FIR —, e i livelli sono obbligatori per le due posizioni: lo dicono il validatore della schermata dei permessi e quello di
`division.json`. La schermata lo mostra e lo scrive come gli altri due (una casella nel form generato).

**Un grant al team di un FIR vale solo su un'area che ha un'entità `IHasFir`** (punto 3 del revisore): un permesso della cui area nessuna
riga dice il suo FIR non raggiungerebbe mai una riga, e alla domanda senza riga direbbe comunque sì (§3.2). Il validatore della schermata
lo rifiuta sul campo del permesso, e il seme lo salta con un avviso, come un permesso che non conosce: sbaglia la schermata, non una
risposta muta. Le aree con il FIR le impara il catalogo all'avvio, dagli stessi modelli dei contesti che A3b già legge
(`HubPipeline.InitializeAsync`, `PermissionCatalog.VerifyAlternatives`): l'area di un'entità è quella del guardiano (`[PermissionArea]` o
il nome del suo `DbSet`), e il training è `IHasFir` nell'area `Training`.

### 3.2 Dove vale: sul FIR della posizione

Con `firStaffScope: own` (la risposta (a) alla domanda 2), il calcolo dei permessi effettivi scrive su ogni permesso che un grant al team
gli dà **il FIR della posizione da cui gli arriva**: `EffectivePermission` guadagna `Fir`, accanto a `ResourceScope`. Chi è capo di due FIR
tiene due permessi. Con `all` il calcolo non lo scrive, e il permesso è del dipartimento come quello di ogni altro grant.

**Nel cookie il FIR sta nel pezzo dello scope, dopo un `#`**: `Training.Assign:TD@#LIRR` (e `Nome:DIP@scope#FIR` se un giorno un permesso
avesse tutti e due). Così **chi non conosce il FIR lo legge chiuso**, mai «su ogni dipartimento» (punto 1 del revisore): un lettore più
vecchio — un pacchetto senza A11a dopo un ritorno indietro, che controlla solo il security stamp, o un pezzo nuovo che chiama
`HubClaims.ParsePermission` — ci legge il dipartimento giusto e lo scope `#LIRR`, che nessuna riga dichiara, e il permesso non raggiunge
nessuna riga; conta solo alla domanda senza riga, che apre un menu e niente di più. Uno scope non contiene `#`; se un modulo ne scrivesse
uno, il suo permesso non raggiungerebbe niente, di nuovo chiuso. Il lettore del cookie di A11a separa lo scope (prima del `#`) dal FIR
(dopo). E **un dipartimento che il lettore non sa leggere non è più «ogni dipartimento»**: oggi `ParsePermission` restituisce `null`, cioè
tutti, per un dipartimento che non riconosce; con A11a il lettore del cookie scarta quel claim, e `ParsePermission`, che tiene la sua firma,
lo rifiuta con un errore invece di aprirlo.

Un permesso con un FIR è **tenuto sulle righe di quel FIR**: un'altra coordinata di «tenuto sulla riga», accanto al dipartimento e allo
scope (`PermissionSet.Has`, e `ICurrentUser.Has` con il FIR della riga accanto al suo scope).

- **Su una riga di un dipartimento raggiunge una riga `IHasFir` con lo stesso FIR**, e nessun'altra: non quella di un altro FIR, non una
  riga senza FIR (un training pilota), non una riga che il FIR non lo dice (un ban, una voce della scheda).
- **Dove l'handler chiede `HasAny` lo conta, come ogni permesso** (punto 3 del revisore): la domanda senza riga, che apre il menu e la
  lista e che il motore fa prima di avere una riga, e la lettura di una riga che la sua area condivide in lettura (`ISharedForReading`).
  È voluto, ed è stretto dalla regola di §3.1: un grant al team vale solo su un'area con il FIR, e oggi nessuna entità di un'area con il
  FIR è condivisa in lettura.
- **`/api/me` non cambia**: il browser usa i permessi per mostrare menu e pulsanti e non guarda nemmeno lo scope (`holdsPermission`), come
  per il validatore di un tour; sulla riga risponde il server (nel training, le `actions` delle pagine dello staff).
- **«`Edit` implica `View`» tiene il FIR.** Un **deny** al team toglie il permesso come ogni deny, sul suo dipartimento e non su un FIR
  solo: nessuno chiede di più.

### 3.3 Nell'unico handler

In `DepartmentAuthorizationHandler` il FIR della riga viaggia con la domanda, come lo scope: un permesso vale se è tenuto su uno dei
dipartimenti della riga, con lo scope della riga **e con il suo FIR**. Con la risposta (a) alla domanda 2, decisa, **la regola di oggi**
(§2 punto 3) **lascia il posto a questa**: chi tiene un permesso sul suo dipartimento — per posizione, o per un grant a un VID — non è mai
fermato dal FIR, e chi lo tiene dal team di un FIR lo tiene sul suo. Il resto come oggi: prima di tutto l'interessato, poi i partecipanti,
la domanda senza riga, le righe condivise in lettura; e la regola di A3b, il cui ripiego su `{Area}.Edit` passa dalla stessa funzione, e
quindi dallo stesso FIR.

### 3.4 Nel guardiano

Ogni domanda del guardiano porta il FIR della riga: quella su `Edit` (`RequireAny`) e quella su ogni alternativa, nei **tre rami** di
`IsWrittenWithAnAlternative` — la creazione, la modifica e l'eliminazione di A3b (punto 5 del revisore). **Una riga che cambia FIR** è come
una riga che cambia dipartimento: `Edit` sul FIR di prima e su quello di dopo, e **nessuna alternativa la sposta** — la condizione sta
accanto a quella sui dipartimenti di prima e di dopo (`OriginalDepartments`). Così un capo FIR assegna i training del suo FIR, e non porta
un training dal suo FIR a un altro, né da un altro al suo. Il training, del resto, non cambia il suo FIR dopo la richiesta.

### 3.5 Nelle liste generate

- **Un permesso con un FIR non porta il suo dipartimento fra quelli «per vedere»** (i claim `dept`, `ICurrentUser.Departments`): il capo di
  un FIR non è del TD, e non ne vede né le righe `Visibility.Department` né le liste.
- **La lista generata di un'entità `IHasFir`** tiene, oltre alle righe dei dipartimenti di chi legge, quelle **del dipartimento e del FIR
  dei suoi permessi con un FIR che sono il permesso di lettura della lista** (punto 2 del revisore), in SQL. `TryNarrowToDepartments`
  riceve quel permesso (`CrudOptions.EffectiveReadPolicy`), e conta solo i permessi con un FIR che hanno il suo nome; «`Edit` implica
  `View`» è già dentro, perché il calcolo scrive il `View` accanto all'`Edit`, con lo stesso FIR. Un capo che tiene dal team
  `Training.Assign` — che non implica `View` — e `Training.View` da un grant a un VID su un altro dipartimento non vede le righe del suo
  FIR. Tutto dal cookie, senza un claim nuovo. Chi ha solo quei permessi non riceve il 403 «nessun dipartimento» su quella lista; su
  un'entità che il FIR non lo dice non contano, e la lista gli resta chiusa come oggi. Il FIR dell'entità dev'essere una colonna, come
  quello del training.
- **Il filtro globale di `Visibility.Department` non cambia**: nessuna riga `IHasFir` è visibile per dipartimento (il training è
  `Members`). La prima che lo sarà lo estenderà allo stesso modo, con la sua nota.

### 3.6 Chi lascia la posizione, la sessione, il seme

- **Il grant vale finché c'è la posizione**: `IsHeldThrough` riconosce il team di un FIR — una posizione con un FIR e senza dipartimento, a
  uno dei livelli — e dà il FIR di ognuna. Il calcolo che non trova più la posizione (al login, o quando il cookie si ricostruisce) non
  mette il permesso nel cookie, come per ogni grant a una posizione.
- **Scrivere un grant al team** fa rientrare chi tiene ora una posizione FIR a quei livelli (`IAffectsUserSession`: l'interceptor cerca
  in `hub_user_staff_positions` le righe con un FIR), come un grant alla posizione di un dipartimento.
- **Il seme** si ricorda come gli altri (`positionGrants.seeded`): la sua impronta dice il team di un FIR al posto del dipartimento.

### 3.7 Che cosa non cambia

- **Il personale dei dipartimenti** non è mai fermato dal FIR; il Director, il web team e il superadmin come prima.
- **Un grant a un VID**, anche al capo di un FIR, vale sul suo dipartimento, senza FIR: è personale, ed è il modo di dare a un capo più del
  suo FIR.
- Lo scope, l'interessato, i partecipanti, le righe condivise in lettura, le alternative di A3 e di A3b; `ICurrentUser.Firs` e i claim `fir`.
- **IT resta `all` fino ad A11b**: finché nessuno scrive un grant al team di un FIR, nessuno tiene un permesso con un FIR, e niente cambia
  per nessuno. I test che ci sono restano verdi senza essere toccati.
- ⚠️ **Cambia il senso di un'impostazione che c'è** (punto 4 del revisore), com'è nel piano §4.1. **Una divisione già su `own`** vede il
  personale dei suoi dipartimenti allargato a ogni FIR: la regola di oggi lo fermava sulle righe con un FIR, e non lo fa più. **Con
  `all`** ogni CH e ACH tiene il permesso del team su tutto il dipartimento, e lo riceve anche «per vedere», righe `Visibility.Department`
  comprese, come ogni grant. Lo dicono `docs/FORKING.md` e il commento di `config/division.example.json`, nella PR del codice (§6).

### 3.8 Che cosa farà A11b

- In `config/division.json` due righe: `Training.View` e `Training.Assign` al team del FIR, con i livelli `Coordinator` e `Assistant` (CH e
  ACH, non i CHA: design §3.2) e `scope: TD`; e **`firStaffScope: own`** (Carmine, risposta 2).
- La pagina, l'assegnazione e `training.approvalQueue` seguono l'unico handler; `/staff/training` segue il motore. I training dei piloti
  restano di TC e TAC. Un capo FIR assegna un training già accettato: accettare è `Training.Approve`, che non ha.
- ⚠️ A11b guarda ogni lettore del modulo che chiede `Training.View` senza chiedere l'handler sulla riga né passare dal motore (il percorso
  del trainee e i suoi ban, per esempio): un capo FIR lo tiene «da qualche parte», e alla domanda senza riga l'handler dice sì.

### 3.9 Il modulo di prova e i test della spina dorsale

- **`SampleRecord` dice il suo FIR** (`IHasFir`: una colonna e una migrazione del solo contesto di prova, dopo `AddSampleAssignee` di A3b),
  ed è letto anche da una lista generata del modulo di prova.
- **I test della spina dorsale**, sulla MariaDB vera, con l'identità del cookie e l'handler chiesto sulla riga come in A3 e A3b (VID
  790080–790089, cercati liberi su ogni branch):
  1. un permesso dato al team vale sulle righe del FIR della posizione e su nessun'altra — né di un altro FIR, né senza FIR —, per CH e per
     ACH; un CHA, fuori dai livelli del grant, non tiene niente;
  2. il guardiano lascia scrivere al capo la riga del suo FIR e non quella di un altro, in modifica, alla creazione e all'eliminazione; chi
     tiene un permesso solo dal team non sposta una riga fra due FIR;
  3. l'handler e il guardiano rispondono uguale;
  4. **chi lascia la posizione lo perde**: il calcolo vero, rifatto senza la posizione FIR, non glielo dà più;
  5. con `all` il grant al team vale sul dipartimento; con `own` il personale dei dipartimenti non è fermato dal FIR;
  6. la lista generata mostra al capo le righe del suo FIR e basta; una lista di righe senza FIR gli resta chiusa; e **il caso negativo del
     punto 2**: con dal team un permesso che non è quello di lettura della lista, e la lettura da un grant su un altro dipartimento, le
     righe del suo FIR non ci sono;
  7. scrivere un grant al team fa rientrare chi tiene la posizione.
- **I test di unità**: `PermissionSet` con il FIR e senza, e `HasAny`; il claim che va e torna, un cookie vecchio, e **un claim con il FIR
  letto da chi non conosce il FIR** (`ParsePermission` e `PermissionSet` senza FIR) **che non raggiunge nessuna riga**; **«un dipartimento
  illeggibile non è ogni dipartimento»** (il punto 1); il calcolo con `own` e con `all`, con due FIR, con `Edit` che implica `View`, con un
  grant a un VID; l'impronta del seme e il seme saltato su un'area senza FIR; i due validatori, e il rifiuto di un grant al team su un'area
  senza un'entità `IHasFir` (il punto 3).

## 4. Alternative scartate

| Alternativa | Perché no |
|---|---|
| Un grant per FIR (`"fir": "LIRR"` in `positionGrants`) | il file della divisione nominerebbe i FIR, che vengono da IVAO (piano §4.1), e un FIR nuovo vorrebbe righe nuove |
| Il FIR come scope della riga (`fir:LIRR`) | una riga ha uno scope solo (il training ha già il suo, per il trainer), e il nucleo dovrebbe costruire e leggere uno scope, cosa che non fa mai |
| I permessi dei moduli derivati nel codice dalle posizioni FIR, come fa `RolePermissionMatrix` | quali permessi di un modulo tiene un capo FIR lo decide la divisione con `positionGrants`, non il codice (nota `moduli-non-subordinati` §3.2) |
| Il team FIR come dipartimento (`Department.FIR`) | `Department` è l'asse di proprietà delle righe (`DepartmentMask`) e l'elenco dei dipartimenti della rete: un FIR non ha righe in cura |
| Un grant per VID a ogni capo | lavoro a mano a ogni cambio di capo, e un grant a un VID vale su tutto il dipartimento (nota `autorizzare-su-un-pezzo…` §3) |
| Nel modulo, un filtro per il FIR di chi legge e un controllo prima del salvataggio | un secondo handler scritto a mano (`CLAUDE.md` §2), che il guardiano e la lista non conoscono |
| La regola di oggi così com'è, con IT a `own` | fermerebbe TC, TAC, TA e trainer su ogni training ATC, e i capi FIR resterebbero comunque senza permessi |
| Il dipartimento di un permesso con un FIR «per vedere», come quello di ogni grant | il capo vedrebbe la lista intera del TD e le sue righe `Visibility.Department` |
| Anche il filtro globale di `Visibility.Department`, già ora | nessuna riga `IHasFir` è visibile per dipartimento: codice senza un caso da provare |
| Il FIR di un permesso anche in `/api/me` | il browser non lo userebbe, e il campo nuovo, obbligatorio nel tipo generato, toccherebbe i test del nucleo che costruiscono la risposta |

## 5. Le domande per Carmine

Poste il 27 settembre 2026 con [un commento sulla #159][q1], la PR di questa fase.

1. **La forma è quella di §3?** Il team di un FIR come soggetto di un grant a una posizione (`firTeam` con i livelli); il permesso scritto
   nel cookie con il FIR della posizione, contato dall'unico handler, dal guardiano (anche prima e dopo la scrittura) e dalle liste
   generate; il suo dipartimento non «per vedere»; il filtro globale no. **Raccomandata: sì.** Da questa risposta dipende il codice di A11a.
2. **Chi decide che il permesso del team vale solo sul suo FIR?**
   - **(a) `firStaffScope`**, com'è nel piano §4.1: con `own` il permesso del team è legato al FIR della posizione, con `all` vale sul
     dipartimento. La regola di oggi (§2 punto 3) lascia il posto a questa, e il personale dei dipartimenti non è più fermato dal FIR.
     **Per IT, `firStaffScope` passa da `all` a `own`**, in A11b insieme ai `positionGrants` dei capi FIR: oggi non cambia nient'altro,
     perché nessun team FIR tiene un permesso e il training è l'unica riga con un FIR.
   - **(b) Sempre il suo FIR, qualunque `firStaffScope`** (la lettera del design §8 n.2): IT resta `all`. Ma `firStaffScope` non decide più
     niente per i team FIR, gli unici che terrebbero qualcosa per FIR, e la regola di oggi resta, con il suo difetto sotto `own` (§7): due
     regole sul FIR nell'handler.
   - **(c) Grant per grant** (`"firTeam": "own"` o `"all"`): la più larga, e oggi nessuno la chiede.

   **Raccomandata: la (a).** Una regola sola, quella che c'è, con il significato che il piano le dà. Se un giorno servirà un team FIR su
   ogni FIR per una cosa e sul suo per un'altra, sarà un'eccezione grant per grant, con la sua nota.

**Decise da Carmine il 28 settembre 2026** ([il suo commento sulla #159][a1]):

1. **Sì, la forma di §3**: il team di un FIR come soggetto di un grant a una posizione, il permesso portato con il FIR della posizione e
   contato dall'unico handler, dal guardiano e dalle liste generate; il suo dipartimento non «per vedere»; il filtro globale com'è. **Alla
   condizione** che la nota correggesse, prima del codice, i punti 1 e 2 del revisore — un claim che un lettore più vecchio legge chiuso,
   mai come «ogni dipartimento»; la lista ristretta solo dai permessi con un FIR che soddisfano il suo permesso di lettura — e rispondesse
   ai punti 3–5 come il revisore chiedeva.
2. **La (a), `firStaffScope`**, com'è nel piano §4.1: con `own` il permesso del team vale sul FIR della posizione, con `all` sul
   dipartimento, e il personale dei dipartimenti non è mai fermato dal FIR. **IT passa da `all` a `own` in A11b**, insieme ai
   `positionGrants` dei capi FIR.

**I rilievi del revisore** ([il suo commento sulla #159][rv]), e dove sono entrati:

1. il claim che un lettore senza il FIR legge «su ogni dipartimento» → **§3.2**: il FIR nel pezzo dello scope, dopo un `#`, che un lettore
   vecchio legge chiuso; un dipartimento illeggibile non è più «ogni dipartimento»; i due test di unità in §3.9;
2. la lista allargata da un permesso con un FIR qualunque → **§3.5**: solo quelli che sono il permesso di lettura della lista; il caso
   negativo nel test 6 di §3.9;
3. un permesso con un FIR conta dove l'handler chiede `HasAny` → **§3.2**, detto; e **§3.1**: un grant al team solo su un'area con
   un'entità `IHasFir`, rifiutato dalla schermata e saltato dal seme, come il revisore raccomandava;
4. il senso nuovo di `firStaffScope` per chi è già su `own` e per chi è su `all` → **§3.7** e **§6** (`FORKING.md`,
   `division.example.json`);
5. i documenti e il codice → `main` nel branch con un merge (28 settembre, lo stato di questa nota, `08` e `HANDOFF-M3.md` non dicono più
   che il codice aspetta #135); **§3.4**, i tre rami di `IsWrittenWithAnAlternative` e la condizione accanto a `OriginalDepartments`;
   **§3.9**, la migrazione di prova dopo `AddSampleAssignee`.

## 6. Che cosa si tocca

- **Il nucleo**: `Auth/UserGrant.cs` (il soggetto, `IsHeldThrough`) e la migrazione `AddGrantFirTeam` del contesto del nucleo;
  `Division/DivisionOptions.cs` e `DivisionOptionsValidator.cs` (il seme); `Division/DomainContracts.cs` (`StaffPositionSubject`);
  `Auth/PositionGrantSeeder.cs` (l'impronta, e il seme saltato su un'area senza FIR); `Auth/GrantDtos.cs` e `GrantWriteDtoValidator.cs`
  (il soggetto e l'area); `Auth/Permissions/PermissionCatalog.cs` (le aree con il FIR) e `Web/HubPipeline.cs` (le impara all'avvio, dai
  modelli che legge già); `Auth/Permissions/EffectivePermissionsCalculator.cs` (`EffectivePermission.Fir`, `PermissionSet`);
  `Auth/HubClaims.cs` (il FIR nel pezzo dello scope, il dipartimento illeggibile rifiutato, i dipartimenti «per vedere»);
  `Auth/ICurrentUser.cs` (il lettore del cookie); `Auth/UserSyncService.cs` (`firStaffScope` al calcolo);
  `Auth/Permissions/HubAuthorization.cs`; `Data/HubSaveChangesInterceptor.cs` (il guardiano e le sessioni);
  `Data/Crud/MapCrudExtensions.cs` (la lista); la schermata dei permessi (`web/src/features/admin/grants/`) e le sue parole
  (`locales/*/common.json`).
- **Senza toccare i file di prova del maintainer**: `ICurrentUser` guadagna la domanda con il FIR con una risposta predefinita
  (`PermissionSet`), così `TestCurrentUser` non cambia; `HubClaims.ParsePermission` tiene la sua firma, che
  `ResourceScopeAndStakeholderTests` confronta; il calcolo prende `firStaffScope` con `all` come valore predefinito, così i test che lo
  chiamano non cambiano.
- **I documenti pubblici**: `config/division.example.json` e `docs/FORKING.md` — come si scrive un grant al team di un FIR (solo su
  un'area che ha righe con il FIR), che cosa vuol dire `firStaffScope` (il perimetro dei team FIR, mai del personale dei dipartimenti), e
  che cosa cambia per chi è già su `own` o su `all` (§3.7).
- **Il modulo di prova**: `SampleRecords.cs`, `SampleModule.cs`, la migrazione e lo snapshot del suo contesto.
- **File nuovi**: i test della spina dorsale e di unità. **Nessun test che c'era cambia.** Niente del modulo del training: è di A11b.

## 7. Trovato, per il revisore

1. **Con `own`, la regola del FIR di oggi ferma anche il personale dei dipartimenti** (§2 punto 3), contro il piano §4.1 e il commento di
   `division.example.json`. Con la (a), decisa, lascia il posto a quella di §3.
2. **Il guardiano non guarda il FIR** (§2 punto 4): con `own`, l'endpoint che chiede l'handler e la rete direbbero due cose. Con A11a lo
   guardano tutti e due.

## Da portare nel piano

- **§4.1** (le posizioni FIR e `firStaffScope`) e **§6.3** (i grant): il soggetto di un grant è un VID, la posizione di un dipartimento **o
  il team di un FIR** (con i livelli: CH, ACH, CHA), e un grant al team vale solo su un'area che ha righe con il FIR (`IHasFir`);
  `firStaffScope` decide se un permesso del team vale solo sulle righe del FIR della posizione (`own`) o sul dipartimento (`all`), e **il
  personale dei dipartimenti non è mai limitato per FIR**, ora anche nel codice. IT: `own`, da A11b.
- **§6.3**, i permessi nel cookie: il FIR di un permesso sta nel pezzo dello scope, così un lettore che non lo conosce lo legge chiuso, e
  **un permesso che il lettore non sa leggere non vale da nessuna parte**, mai «su ogni dipartimento».
- **§16 punto 2** e **`CLAUDE.md` §2** (la riga «A permission on one row only…»): il FIR della riga (`IHasFir`) è una coordinata di «tenuto
  sulla riga», accanto al dipartimento e allo scope, nell'unico handler e nel guardiano; **§16 punto 6**: la lista generata di un'entità
  `IHasFir` tiene anche le righe del FIR dei permessi che sono il suo permesso di lettura.
- **§9.2, riga Training**: CH e ACH vedono e assegnano i training del loro FIR (A11b).
