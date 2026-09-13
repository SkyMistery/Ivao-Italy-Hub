# I moduli non appartengono ai dipartimenti

**Data:** 13 settembre 2026 — portata da Carmine dopo il confronto con lo staff di IVAO («non
subordinare i moduli ai dipartimenti: una sezione per i training, una per i tour, una per gli
eventi, a parte dai dipartimenti»)
**Stato:** **decisa** (Carmine, 13 settembre 2026), sulle quattro domande di §3
**Regola applicata:** `CLAUDE.md` §5, caso **(c)**: cambia il principio di piano §9.0, il contratto
`IModule`, la forma dei grant e l'interfaccia di proprietà su cui poggia l'unico authorization
handler.
**Tempismo:** nessun modulo di dipartimento è costruito (Events apre M2), quindi il cambio è quasi
tutto sulla carta. Nel codice: `IModule.Department` (già nullable), i grant, `IOwnedByDepartment`.

## 1. Che cosa serve

- **Eventi, tour e training sono sezioni a sé**, non pezzi di un dipartimento: nel pubblico
  (`/events`, `/tours`, `/training`, già così) e nel back-office (`/staff/events`, `/staff/tours`,
  `/staff/training`, allo stesso livello di `/staff/content`).
- **Si stabilisce chi, dai singoli dipartimenti, può fare che cosa** in ogni modulo.
- **Le dashboard dei dipartimenti mostrano ciò che di quei moduli interessa loro.**

## 2. Perché il piano di oggi non lo copre

Piano §9.0 lega ogni modulo a un dipartimento («Events (ED)», «Training (TD)»), mette le sue
schermate sotto `/staff/{dept}/`, e fa decidere a `owner_department` — uno solo — chi modifica una
riga. Un evento del SOD fatto con l'ED non ha un posto; un permesso «AOD gestisce le posizioni ATC
degli eventi» non ha dove essere scritto se non come grant a ogni VID, uno per uno.

## 3. Le decisioni

### 3.1 Il modulo è un'area di funzioni, il dipartimento resta l'asse di proprietà delle righe

- **`IModule.Department` è `null` per ogni modulo**, e la proprietà si toglie dal contratto nella
  fase che applica la nota. Un modulo dichiara identità, rotte, permessi, blocchi e job, non un
  dipartimento.
- **Back-office**: `/staff/events`, `/staff/tours`, `/staff/training`. La barra laterale dello staff
  diventa **Contenuti** (pagine, news, documenti, template, link, media) · **Eventi · Tour · Training**
  (ognuno visibile a chi ha almeno un permesso del modulo) · **Dipartimenti** (dashboard, voci di
  calendario interne, contatti).

### 3.2 Chi può fare che cosa: i grant si danno anche a una posizione (domanda a → 3)

- **I grant di oggi hanno per soggetto un VID; da qui il soggetto è un VID oppure una posizione**
  (dipartimento + livello: «AOD, coordinator e assistant → `Events.ManageAtcPositions`»). Stessa
  tabella `hub_user_grants`, stessa schermata `/staff/admin/permissions`, stesso audit, stessa
  sospensione, stessa regola: un grant non conferisce mai `Permissions.Manage` né il superadmin.
- **I valori iniziali di una divisione vengono da `division.json`** (letti al primo avvio, come i
  superadmin); poi si modificano dall'interfaccia. Scartati il codice — cambia da divisione a
  divisione — e il solo `division.json` — ogni cambio vorrebbe dire un file via FTP.
- **I permessi del nucleo restano dove sono** (`RolePermissionMatrix`); i grant per posizione
  servono ai permessi dei moduli, e a `Awards.Assign`, che il piano già dice «configurazione, non
  codice».
- I nomi delle azioni li decide il design di ciascun modulo; per orientarsi: `Events.Manage`,
  `Events.ManageAtcPositions`, `Tours.Manage`, `Tours.ValidateReports`, `Training.ManageRequests`,
  `Training.Conduct`, `Training.ViewResults`.

### 3.3 Una riga di modulo appartiene a uno o più dipartimenti (domande b e c → sì)

- **«A cura di» è obbligatorio e può essere più di un dipartimento**: l'ED coordina gli eventi, ma
  un evento può essere organizzato **in collaborazione** con un altro dipartimento (Carmine).
- **E decide i permessi**, non è solo un'etichetta: **il SOD non tocca gli eventi degli altri**.
  Un permesso di modulo tenuto su un dipartimento vale sulle righe che hanno quel dipartimento fra
  i propri: `Events.Manage` sul SOD gestisce gli eventi a cui il SOD partecipa; l'ED, che è in
  tutti gli eventi come dipartimento di base (sotto), li gestisce tutti. È
  la grammatica di piano §16.3 — lo scope è implicito dalla risorsa — con la risorsa che porta un
  insieme invece di un valore.
- **Ogni modulo ha un dipartimento di base, sempre presente** (Carmine, lo stesso giorno): gli
  eventi sono **sempre a cura dell'ED**, i tour **sempre del FOD**, i training **sempre del TD**; gli altri dipartimenti si
  **aggiungono** in collaborazione e il dipartimento di base non si toglie. Un evento creato dal SOD
  è quindi «ED + SOD» da solo, e l'ED lo gestisce con il permesso sul **proprio** dipartimento,
  senza bisogno di un permesso su tutti.
- **Il dipartimento di base è configurazione, non codice**: `division.json → modules.events.baseDepartment: "ED"`,
  `modules.tours.baseDepartment: "FOD"`, `modules.training.baseDepartment: "TD"`. Il modulo non lo nomina (resta vero che `IModule` non
  dichiara un dipartimento), e una divisione che forka con un'altra organizzazione cambia una riga.
- **Chi crea una riga deve metterci almeno un dipartimento su cui ha il permesso** (oltre a quello di
  base, che c'è sempre): il SOD crea eventi «ED + SOD»; chi ha il permesso solo sull'ED crea eventi
  dell'ED.
- **Come nel codice — da confermare nel design di M2**: si **estende l'unica interfaccia** di
  proprietà invece di aggiungerne una seconda. `IOwnedByDepartment` espone l'insieme dei
  dipartimenti; una riga editoriale ne ha uno solo e continua a comportarsi com'è. Il global query
  filter e l'unico authorization handler guardano «almeno uno in comune». Un'interfaccia parallela
  vorrebbe dire due rami nel handler, cioè il caso che `CLAUDE.md` §2 vieta. I test della spina
  dorsale (interceptor, handler) vanno estesi con il caso a due dipartimenti.
- **Nei contenuti editoriali niente cambia**: una pagina, una news, un documento hanno ancora un
  dipartimento solo. La collaborazione non è stata chiesta lì.

### 3.4 Le «widget» dei dipartimenti sono blocchi Data dei moduli

- La dashboard di un dipartimento è già una pagina a blocchi (5 settembre): i moduli **registrano
  blocchi Data** («Training: richieste in attesa», «Eventi della settimana senza ATC», «PIREP da
  validare») e ogni dipartimento mette nella sua quelli che gli servono, con l'editor che c'è.
  Nessun secondo meccanismo di widget.
- Questi blocchi sono **sempre `live`** e rispondono **con ciò che chi guarda può vedere**: il
  provider interroga con l'utente corrente e il filtro globale; chi non ha il permesso vede il
  blocco vuoto. Possono prendere come proprietà «a cura di» (§3.3), così la dashboard del SOD mostra
  gli eventi del SOD.

### 3.5 Il calendario (domanda d)

- Le voci proiettate dai moduli prendono i dipartimenti da «a cura di». La visibilità la decide il
  modulo: **gli eventi e le sessioni di training sono pubblici** (Carmine). Per le sessioni vale la
  regola di privacy di piano §9.7: si mostra il minimo e si linka il profilo IVAO; che cosa esattamente
  compare di un allievo lo decide il design di M4.

### 3.6 Il modulo finto dei test

- `AtcModule`, che la nota `staccarsi-da-vipi` toglie, è oggi **l'unico modulo**: su di lui i test di
  M0 dimostrano che il nucleo compone menu, rotte, esclusioni dal fallback e maintenance di un modulo
  senza conoscerne il nome. Si sostituisce con un **modulo finto che esiste solo nel progetto dei
  test**, non con un modulo vuoto nel prodotto. Che non dichiari un dipartimento è già la forma di
  questa nota.

## 4. Che cosa si tocca

- **Piano**: §6.3 (i grant per posizione), §8.2 (sitemap staff), §9.0 (il principio), §9.2 (la
  colonna «Dipartimento» diventa «a cura di, di solito»), §9.5 (visibilità delle voci dei moduli),
  §9.7 (contratto `IModule`, widget come blocchi Data), §16.2–16.3.
- **Codice, in due tempi** (scritto il 13 settembre con le fasi G16–G20 di
  `04-piano-implementazione-m1.md`):
  - **in G16**, subito dopo il merge della pila: `IModule` senza `Department` e il modulo finto nei
    test al posto di `AtcModule` — tolgono codice e non ne aggiungono;
  - **all'apertura di M2**, prima del modulo Events: il soggetto «posizione» nei grant con la
    schermata e il seed da `division.json`, `IOwnedByDepartment` a insieme con filtro, handler e test
    della spina dorsale, `modules.<key>.baseDepartment`, la sezione «Eventi» nella barra dello staff.
    Farli adesso vorrebbe dire costruire per un consumatore che non esiste: un grant di posizione su
    un permesso di modulo senza moduli, un insieme di dipartimenti senza righe che ne abbiano due.
- **`CLAUDE.md`** §2 (la riga «proprietà di dipartimento») e §8.
