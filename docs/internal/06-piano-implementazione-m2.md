# IVAO Division Hub — Piano di implementazione di M2

> Documento **interno** (italiano). Fonte di verità: `00-piano-di-progettazione.md` (§13, M2).
> Si scrive in due parti. La **parte A** sono i prerequisiti che il piano mette «prima di tutto»:
> i moduli fuori dai dipartimenti (`decisions/2026-09-13-moduli-non-subordinati-ai-dipartimenti.md`
> §4). La **parte B** sono le dashboard, decise nella nota `2026-09-13-le-dashboard-a-tutto-schermo`.
> La **parte C** — il modulo Events — si scrive dopo `05-design-m2.md`, e non c'è ancora.

## A. I prerequisiti: i moduli fuori dai dipartimenti

Tre fasi, una PR ciascuna, da `main`, nell'ordine. Nessuna tocca i contenuti editoriali: una pagina,
una news, un documento restano di un dipartimento solo (nota §3.3).

| Fase | Titolo | Dipende da | In una riga |
|---|---|---|---|
| H1 | I grant a una posizione — **fatta il 13 set 2026** | — | il soggetto di un grant è un VID **oppure** un dipartimento con uno o più livelli; seed una volta da `division.json` |
| H2 | Le righe di più dipartimenti — **fatta il 13 set 2026** | H1 | `IOwnedByDepartment` a insieme nel filtro, nel handler, nell'interceptor e nelle liste; `modules.<key>.baseDepartment`; un modulo di prova con una tabella nei test |
| H3 | Le sezioni dei moduli nella barra dello staff — **fatta il 13 set 2026** | H2 | Contenuti · un gruppo per modulo · Dipartimenti · Amministrazione |

### H1 — I grant a una posizione

Nota §3.2. Branch `m2/h1-position-grants`.

**Deciso con Carmine il 13 settembre 2026**: la posizione si indica con **dipartimento + uno o più
livelli** (coordinator, assistant, advisor, membro), non con il codice della posizione. Chi arriva a
quel ruolo prende il permesso da solo e chi lo lascia lo perde; regge quando IVAO rinumera le
posizioni.

1. **Il soggetto.** Sulla stessa tabella `hub_user_grants`: `vid` diventa facoltativo, e si
   aggiungono `position_department` e `position_levels_json` (l'elenco dei livelli). Un grant ha
   **esattamente uno** dei due soggetti. Stessi `value`, `department` (lo scope: `null` = ogni
   dipartimento), `effect`, `expires_at`, `reason`, stesso audit. Migrazione additiva.
2. **I permessi effettivi.** `EffectivePermissionsCalculator` riceve i grant del VID e quelli di
   posizione; un grant di posizione vale per chi ha **almeno una posizione** con quel dipartimento e
   uno di quei livelli. Le regole restano una: niente permessi globali da un grant (quindi mai
   `Permissions.Manage` né il superadmin), deny che vince, scadenza. **La sospensione** resta dei
   grant a un VID (chi perde ogni posizione): un grant di posizione non ha nessuno da sospendere, smette
   di valere perché nessuno ha più la posizione.
3. **Le sessioni.** Scrivere un grant di posizione rinfresca la sessione di **chi ha quella
   posizione** adesso, nella stessa transazione; e un grant modificato rinfresca anche chi aveva il
   soggetto di prima. `IAffectsUserSession` si allarga alla posizione invece di aggiungere un secondo
   meccanismo.
4. **Il seed.** `division.json → positionGrants: [{ department, levels, permission, scope, deny }]`,
   letto **una volta** (una riga in `hub_division_settings` ricorda che è stato applicato), poi la verità
   è la tabella e si cambia dalla schermata. Validato all'avvio come il resto di `division.json`.
5. **La schermata.** `/staff/admin/permissions`: il form chiede prima «a chi» (un VID o una posizione)
   e poi disegna i campi di quel soggetto; la lista mostra il soggetto in una colonna sola.
6. **I test.** Unit: un grant di posizione vale per chi ha il livello, non per gli altri livelli né per
   un altro dipartimento; non conferisce un permesso globale; un deny di posizione morde. Integrazione:
   un grant di posizione arriva alla richiesta dopo, per chi ha la posizione, e la sua rimozione a quella
   dopo ancora; il seed si applica una volta sola.

**Criteri**: «AOD, coordinator e assistant → `Links.Edit` su ED» dato dalla schermata vale per un
coordinator AOD senza rifare il login e non per un membro AOD; tolto, smette di valere; con
`positionGrants` nel file, il primo avvio crea le righe e il secondo non le ricrea anche se nel frattempo
le si è cancellate.

**Fatta il 13 settembre 2026** (branch `m2/h1-position-grants`). Com'è andata:

- **Il soggetto** sta sulla riga come da punto 1: `vid` facoltativo, `position_department`,
  `position_levels_json` (letto e scritto dall'entità come elenco, come le raccolte di G20).
  `UserGrant.IsHeldThrough(positions)` è l'unica risposta a «questo grant di posizione vale per chi ha
  queste posizioni?», e la usano il calcolo dei permessi e la ricerca di chi può approvare una pagina
  (G19), che ora trova anche chi ha `Content.Approve` per posizione. Migrazione `AddPositionGrants`
  (additiva: `vid` diventa facoltativo, due colonne e un indice).
- **Il validatore**: esattamente un soggetto (`errors.grant.subject` sul campo `vid`), almeno un livello
  per una posizione (`errors.grant.levelsRequired`); il controllo «il VID è staff» vale solo per un grant
  a un membro.
- **Le sessioni**: `IAffectsUserSession` ha un secondo membro con un default, `AffectedPosition`;
  l'interceptor raccoglie le posizioni e, dopo la scrittura e nella stessa transazione, le traduce nei
  VID che le hanno. In più, **una riga modificata rinfresca anche il soggetto di prima** (i valori
  originali), cosa che per i grant a un VID prima non succedeva: spostare un grant da un membro a un
  altro lasciava al primo il cookie vecchio fino al login successivo.
- **Il seed**: `PositionGrantSeeder`, chiamato all'avvio dopo i superadmin; la riga
  `positionGrants.seeded` in `hub_division_settings` ricorda che è stato applicato. Salta con un avviso
  un permesso sconosciuto o globale. `division.example.json` e `FORKING.md` lo spiegano;
  `division.json` di IT non ne ha ancora, perché non c'è ancora un permesso di modulo da dare.
- ⚠️ **Scostamento sul punto 5**: il form **non** chiede prima «a chi» per poi disegnare i campi di quel
  soggetto. Il generatore di form non ha campi condizionali, e aggiungerli sarebbe un'estensione per un
  form solo: i due soggetti stanno uno sotto l'altro, con un suggerimento per ciascuno, e il server
  risponde sul campo quando se ne compila nessuno o tutti e due. La lista mostra VID, dipartimento e
  livelli in tre colonne.
- **I test**: tre unit (vale per i livelli giusti e non per gli altri né per un altro dipartimento; mai
  un permesso globale; un divieto di posizione morde), tre d'integrazione (il grant di posizione arriva
  alla richiesta dopo per chi ha la posizione e la rimozione a quella dopo ancora; un soggetto solo; il
  seed applicato una volta e non ricreato dopo una cancellazione), due Vitest sul payload.
- **Verificato in locale**: unit .NET (312), Vitest (389), smoke (77), lint, typecheck, formato, i18n.
  **Non in locale**: integrazione (Docker spento), che esegue la CI.

### H2 — Le righe di più dipartimenti

Nota §3.3. Branch `m2/h2-owned-by-several`, **impilato su H1** (usa i grant di posizione nei test).

**Fatta il 13 settembre 2026.** Le decisioni tecniche, prese con il codice davanti:

- **L'insieme è una maschera di bit** in una colonna della riga (`owner_department_mask`), non una
  tabella di collegamento né un elenco JSON: «almeno uno in comune» diventa `(riga & lettore) != 0`,
  che il filtro globale scrive su una colonna e un parametro, mentre una tabella vorrebbe un join che un
  query filter non sa scrivere. ⚠️ **Il bit di ogni dipartimento è scritto a mano** in
  `DepartmentMask` e non dipende dall'ordine dell'enum, perché è un valore salvato: un dipartimento
  nuovo va in fondo, e un test fissa i bit.
- **Una interfaccia sola**, come chiedeva la nota: `IOwnedByDepartment` ha due membri con un default,
  `OwnerDepartmentMask` (per una riga editoriale è il bit del suo dipartimento) e `OwnerDepartments`.
  Una riga di modulo in cura a più dipartimenti **dichiara** `OwnerDepartmentMask` come proprietà
  scrivibile, che diventa la sua colonna; `OwnerDepartment` resta, ed è il dipartimento di base.
- **L'unico handler** chiede «tenuto su uno dei dipartimenti della riga», senza rami. **Il filtro
  globale, la narrowing delle liste e `DataBlockScope`** costruiscono l'espressione giusta per il tipo:
  `Contains` sul dipartimento per una riga editoriale, la maschera per una riga di modulo.
- **L'interceptor** chiede il permesso su almeno uno dei dipartimenti della riga — «chi crea ci mette
  un dipartimento su cui ha il permesso» — e, se l'insieme cambia, anche su uno di quelli di prima.
- **Il dipartimento di base** (`ModuleBaseDepartment`) si rimette sulla riga in due punti: nel motore
  CRUD subito dopo l'applicazione del payload, così il permesso si controlla sulla riga com'è
  davvero, e nell'interceptor, per ogni altra strada. Trovato leggendo il motore prima della CI: il
  controllo dopo l'applicazione avrebbe rifiutato all'ED un evento «SOD» dal payload.
- ⚠️ **I contesti dei moduli non avevano il filtro globale**: stava solo in `HubDbContext`. Adesso un
  modulo deriva da **`ModuleDbContext`**, che applica lo stesso filtro sulle stesse proprietà
  (`IVisibilityScope`) e non lascia dimenticarlo (`OnModelCreating` è sigillato, il modulo scrive
  `ConfigureModel`).
- **`division.json → modules`** cambia forma: da `{ "specialops": true }` a
  `{ "specialops": { "enabled": true }, "events": { "baseDepartment": "ED" } }`. IT ha già eventi ED,
  tour FOD e training TD; `division.example.json` e `FORKING.md` lo spiegano (e `FORKING.md` perde
  l'esempio con `Department`, rimasto da prima di G16).
- **Il modulo di prova ha una tabella**: `SampleDbContext` con `smp_items`, migrazione generata da
  `dotnet ef` nel progetto dei test (che per questo referenzia `Microsoft.EntityFrameworkCore.Design`),
  gli endpoint CRUD e una lettura attraverso il filtro. Il test host dice `modules.sample.baseDepartment:
  ED`.
- **I test**: quattro unit su `DepartmentMask` (un bit per dipartimento, i bit fissati, andata e
  ritorno, riga editoriale e riga di modulo) e tre d'integrazione sulla spina dorsale a due dipartimenti
  (la base aggiunta e i due dipartimenti che gestiscono, il terzo fuori da lista e scrittura; chi crea
  mette un dipartimento suo; il filtro di un contesto di modulo legge l'insieme), con i permessi del
  modulo dati a posizioni come farà una divisione.
- **Verificato in locale**: unit .NET (316), build, `has-pending-model-changes` sui due contesti.
  **Non in locale**: integrazione (Docker spento), che esegue la CI.

### H3 — Le sezioni dei moduli nella barra dello staff

Nota §3.1. Branch `m2/h3-module-sections`, impilato su H2.

**Fatta il 13 settembre 2026.**

- **Il bootstrap dice di che modulo è ogni voce dello staff**: `NavItem.Module`, riempito da `/api/me`
  componendo le voci modulo per modulo invece che dalla lista già appiattita del registry. Le voci del
  nucleo e del menu editoriale non ne hanno.
- **La barra**: Contenuti · **una sezione per modulo** · Dipartimenti · Amministrazione, come dice la
  nota. Il titolo di una sezione è la chiave `nav.section` nel namespace del modulo
  (`events:nav.section`), che il modulo porta nei suoi file di lingua; una voce senza modulo finisce
  sotto «Moduli», come prima. Lo stesso elenco serve la barra e la palette ⌘K.
- **Il modulo di prova** ha una voce dello staff dietro `Sample.View`; un test d'integrazione prova che
  il bootstrap la dà con `module: "sample"`, e un test Vitest l'ordine delle sezioni e le voci di ciascuna.
- ⚠️ **Da ricordare in M2**: un modulo che apre la sua sezione deve avere `nav.section` nel suo
  namespace, altrimenti la barra mostra la chiave.
- **Verificato in locale**: Vitest (390), smoke (77), lint, typecheck, formato, build .NET. **Non in
  locale**: integrazione (Docker spento), che esegue la CI.

Con H3 **la parte A è chiusa**. Il passo dopo, per il piano (§13), è la **nota sulle due dashboard
personali** (`/me` e `/staff`), poi `05-design-m2.md` e la parte B di questo documento.

## B. Le dashboard a tutto schermo

Nota `decisions/2026-09-13-le-dashboard-a-tutto-schermo.md`. Tre fasi, una PR ciascuna, da scrivere in
dettaglio all'inizio di ognuna con il codice davanti.

| Fase | Titolo | Dipende da | In una riga |
|---|---|---|---|
| D1 | La griglia a tessere | parte A | layout `grid` e `span` nell'envelope (walker, validazione, TypeScript); resa a tessera alta uguale per riga; resa a tutto schermo e barra compatta per `ContentKind.Dashboard`; le dashboard dei dipartimenti convertite |
| D2 | L'editor della griglia | D1 | spostare le tessere, ridimensionarle con la maniglia e con il selettore; il giro e2e che lo prova |
| D3 | `/me` e `/staff` | D1 | righe `me` e `staff` seminate; `/staff` smette di reindirizzare; i blocchi del nucleo (ciò che aspetta me, le mie bozze, calendario dei miei dipartimenti, i miei dipartimenti, il saluto); via il registro dei widget |

## C. Il modulo Events

Si scrive dopo `05-design-m2.md`.

