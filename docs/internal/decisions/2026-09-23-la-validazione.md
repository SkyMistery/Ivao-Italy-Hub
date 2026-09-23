# La validazione dei PIREP (T13)

**Data:** 23 settembre 2026 — fase T13 di M2
**Stato:** **decisa** (Carmine, 23 settembre 2026, cinque risposte in apertura; il resto è design già scritto o scelta tecnica
dichiarata qui sotto)
**Regola applicata:** `CLAUDE.md` §5. Qui ci sono **cinque precisazioni del design** (caso c, decise da Carmine) e **quattro
estensioni di meccanismi del nucleo** (caso b): la rete dell'interceptor per chi valida, i tipi di notifica dei moduli, chi tiene un
permesso, l'ordine secondario della lista generica.

## 1. Che cosa serve

Design M2 §4 (le code, la presa in carico, la riapertura, il riepilogo, la pagina di validazione), §3.5 (le mail degli esiti senza
il nome del validatore), §8.5 (le due schermate). Il piano di implementazione (parte C, T13) lo elenca in sette punti.

## 2. Le cinque risposte di Carmine

1. **T13 si divide**, come T6, T7 e T11: **T13a — il server** (branch `m2/t13a-validation-server`): le tabelle, le code e la pagina
   di validazione come API, la presa, la decisione con gli errori e il suggerimento, le mail, la riapertura, il riepilogo, le tracce;
   tutto provato da test d'integrazione. **T13b — le pagine** (branch `m2/t13b-validation-pages`): `/staff/tours/review` e
   `/staff/tours/review/{id}` con la mappa e la traccia, il blocco `flightops.reviewQueue` (le due metà), lo smoke e il giro e2e —
   il «fatta quando» di T13 è suo.
2. **Le tracce si salvano all'invio.** Oggi il PIREP teneva i piani di volo ma **non** la traccia: la pagina di validazione la deve
   mostrare e i controlli di T17–T18 la leggeranno. Misurata sulle tre tracce registrate (voli di 2–3 ore, un punto ogni ~11 s):
   ~280 KB a volo come JSON di IVAO, ~22 KB lo stesso JSON compresso. Si salva **ciò che il client IVAO del nucleo legge di ogni
   punto** (nove campi: ora, posizione, quota, velocità, prua, a terra, stato, transponder) **compresso gzip, senza togliere altro**:
   **~10 KB a volo** misurati sulle stesse tre tracce. Se T18 vorrà un campo in più lo aggiungerà al client, e le tracce nuove lo
   porteranno. In una tabella a parte, `fo_pirep_tracks`, così nessuna lista la carica. Scartato: rileggerla da IVAO ogni volta —
   se IVAO non risponde o ha perso la sessione il validatore e i controlli non vedono niente.
3. **Le tracce si cancellano 90 giorni dopo la decisione** (Carmine: «non possiamo avere GB di dati di tracce inutili»). A regime
   **meno di 20 MB** con 7000 PIREP l'anno (~10 KB a volo per tre mesi di PIREP). Restano il PIREP, i piani, gli errori, e (da T17) gli esiti dei controlli con la loro evidenza;
   una riapertura dopo 90 giorni si fa senza traccia, e la pagina lo dice. Il numero è un'impostazione,
   `trackRetentionDays` (default 90, fra 7 e 730), non una costante. Scartati: con i dati pesanti del tour (13 mesi dalla chiusura),
   e alla fine della finestra di contestazione (una riapertura, che non ha limiti di tempo, non troverebbe quasi mai la traccia).
4. **Riaprire una decisione** (§4.2.1: il validatore che l'ha presa, FOC e FOAC) è un **permesso nuovo, `Tours.ReopenDecisions`**,
   negato all'interessato, dato da `positionGrants` a Coordinator e Assistant del FOD. Chi ha deciso riapre sempre la sua decisione
   con il solo `Tours.Validate` sul tour. Scartato: riusare `Tours.ManageValidators` (oggi ce l'hanno le stesse due posizioni, ma
   chi lo ricevesse per gestire i validatori riceverebbe anche la riapertura).
5. **Il conteggio dell'anno** di un errore (§4.3, il suggerimento su un `Warning` oltre `yearly_max`): gli errori **confermati** sui
   PIREP **accettati e rifiutati** del pilota, di **tutti i tour** (il catalogo degli errori è della divisione), per **anno UTC del
   decollo**, più quelli che il validatore sta segnando. Un «da modificare» non conta: la sua decisione non è definitiva, e quando
   torna si decide di nuovo. Lo stesso conteggio «da sempre» è la seconda colonna della tabella.

## 3. Le estensioni del nucleo (caso b)

### 3.1 La rete dell'interceptor per chi valida

Annunciata dalla nota di T11a (§5): la rete di sicurezza chiede `Tours.Edit` su ogni modifica di una riga di un dipartimento, e un
validatore abilitato su un tour ha **solo** `Tours.Validate` con lo scope di quel tour. Prendere o decidere un PIREP sarebbe stato
rifiutato.

**Deciso**: un tipo di riga può dichiarare **con quale altro permesso si scrive**, con l'attributo `[AlsoWrittenWith("Tours.Validate")]`
accanto a `[PermissionArea]`. La rete accetta la scrittura se l'utente ha **quel** permesso su uno dei dipartimenti della riga
**con lo scope della riga** (`IHasResourceScope`), e **mai** se è l'interessato della riga: lui ha già la sua strada, più stretta
(`ISubmittedByMembers`), e il permesso alternativo non la allarga — più severo dell'handler, che nega all'interessato solo i
permessi `DeniedToStakeholder`, e la rete non deve leggere il catalogo per saperlo. Spostare la riga fra dipartimenti chiede ancora
`Edit` da tutte e due le parti. Il PIREP lo dichiara; nessun'altra riga oggi. I test d'integrazione della validazione lo provano:
il validatore dei test ha `Tours.Validate` su un tour e nient'altro, e ogni sua scrittura passa di qui.

- **Scartato**: scrivere la decisione «come il sistema». È l'aggiramento che la rete esiste per impedire (nota di T11a §5).
- **Scartato**: dare `Tours.Edit` con scope ai validatori. Darebbe loro anche le leg, le regole e il tour.
- **Scartato**: far leggere alla rete lo scope anche per `Edit`. Non serve a nessuno oggi, e cambia la risposta per ogni riga.

### 3.2 I tipi di notifica dei moduli

`NotificationTypes` era un elenco chiuso del nucleo («il secondo tipo è una riga qui»): un modulo non poteva dichiarare
`flightops.pirepAccepted` senza che il nucleo nominasse un modulo. **Deciso**, come le preferenze di T4b: `IModule.NotificationTypes`,
nomi `{modulo}.{nome}`, composti all'avvio in un **`NotificationTypeCatalog`** che rifiuta un doppione o un nome fuori dal suo
modulo. I tipi del nucleo restano costanti in `NotificationTypes`. Il servizio, lo schermo delle preferenze e il suo endpoint
chiedono al catalogo invece che all'elenco statico. **Le parole** stanno nel file di lingua del modulo: il modello della mail
sotto `mail.{tipo}` (il nucleo legge ogni file di `locales/{lang}/` come una mappa unica), l'etichetta del profilo sotto
`notifications.{nome}` nel namespace del modulo, che lo schermo del profilo sceglie dal prefisso del tipo quando è un modulo.

### 3.3 Chi tiene un permesso

Il riepilogo giornaliero va «a chi ha `Tours.Validate`», ciascuno con i **suoi** tour. Chi lo tiene lo dicono le posizioni, i grant
a una persona, i grant a una posizione, lo scope e il superadmin: esattamente ciò che `EffectivePermissionsCalculator` già compone
al login. **Deciso**: un'interfaccia del nucleo, **`IPermissionHolders`** (`Core/Auth/Permissions`), che per un permesso restituisce chi lo
tiene con tutto ciò che tiene (`PermissionHolder.Has(permesso, dipartimento, scope)`, la stessa domanda dell'handler), **calcolato
con lo stesso calcolatore del login** sui candidati — chi ha una posizione, chi un grant nomina, i superadmin. La implementa
`UserSyncService.HoldersOfAsync`, accanto a `LoadAsync`: leggere le posizioni è della metà IVAO di `Core/Auth/`, e un modulo non la
nomina. Scartato: rifare a mano in un modulo la somma di posizioni e grant (lo fa già, per il suo solo caso, l'elenco di chi approva
una pagina in `ContentReviewService`; lì resta com'è, perché il suo test lo tiene e cambiarlo non è di questa fase).
⚠️ **Il superadmin e HQ ricevono il riepilogo** (tengono ogni permesso): lo spengono dal profilo.

### 3.4 L'ordine secondario della lista generica

La coda «per tour» è ordinata per tour e, **dentro il tour, per data**. La lista generica ordinava per la sola colonna chiesta, e le
righe uguali uscivano nell'ordine che il database preferiva. **Deciso**: ordinando per una colonna, le righe uguali tengono
l'**ordine di default** della risorsa (`ThenBy(DefaultOrder)`), per ogni lista. Nessuna pagina mostrava righe uguali in un ordine su
cui contasse, quindi niente cambia per chi c'era.

## 4. Le scelte tecniche (dichiarate, non domandate)

- **Tabelle** (migrazione additiva `AddValidation`): su `fo_pireps` `note_to_pilot`, `staff_note`, `threshold_overridden`,
  `override_reason`; `fo_pirep_errors` (`pirep_id`, `error_id`, `category` congelata, `suggested_by_check`, `confirmed`), come nel
  design; `fo_pirep_tracks` (`pirep_flight_id`, `points_gzip`, `point_count`, `stored_at`). La traccia in un blob di ~22 KB **non
  è un upload** (la regola «mai in `longblob`» di §11.3 è per i file caricati): nasce e muore con il PIREP, nella stessa
  transazione, e il suo backup è quello del database.
- **Le code** sono la lista generica (`MapCrud`, sola lettura) su `GET /api/flightops/review/queue`: `filter[tourId]`,
  `filter[status]`, `filter[open]` (`true`, il default: in coda e in validazione; `false`: decisi), `sort=queuedAt` (il default) o
  `sort=tourId`. Chi ha `Tours.Validate` **da qualche parte** vede **tutti** i PIREP non ritirati (§4.1); ogni riga dice se chi
  guarda la può prendere (`canTake`) e se è sua (`isOwn`), con l'unico handler sulla riga. L'ordine scelto è la preferenza
  `flightops.reviewQueueOrder` (`date` | `tour`), dichiarata dal modulo: la pagina la legge e la manda come `sort` (T13b).
  ⚠️ La lettura di una riga della lista (`…/queue/{id}`, che il motore mappa sempre) chiede `Tours.Validate` **su quella riga**, quindi
  è più stretta della lista; nessuno la usa: la pagina di validazione è `GET /api/flightops/review/{id}`.
- **La coda si ordina su `queued_at`**, una colonna nuova: quando il PIREP è entrato in coda l'ultima volta (invio o reinvio).
  La migrazione la riempie per i PIREP che ci sono già.
- **La presa** (`POST …/review/{id}/take`): da `Queued`, o da `InReview` con il lease scaduto, o per rinnovare il proprio; vince la
  prima (`row_version`). **Lasciare** (`…/release`) lo rimette in coda: non è nel design, ma senza un validatore che apre per errore
  il PIREP sbagliato lo tiene per mezz'ora.
- **La decisione** (`…/decide`): solo chi lo ha in mano. `ToModify` chiede la nota al pilota (deve sapere cosa correggere);
  `Rejected` chiede almeno un errore (la mail dice le regole violate); gli errori si scelgono fra quelli **delle regole congelate**.
  `threshold_overridden` lo calcola il server confrontando esito e suggerimento, e allora `override_reason` è obbligatorio; un «da
  modificare» non si confronta. Gli errori della decisione **sostituiscono** quelli di prima (§4.2.1: la nuova decisione sostituisce
  la vecchia nei contatori).
- **Il suggerimento** è una funzione pura (`ReviewSuggestion`): un `Dangerous` segnato → rifiuto; un `Warning` che con il
  conteggio dell'anno supera `yearly_max` → rifiuto; altrimenti accettazione, con i motivi. **Mentre il validatore spunta gli errori**
  la pagina lo chiede al server, `GET …/review/{id}/suggestion?errorIds=…`: la regola non si riscrive in TypeScript.
- **La pagina** è `GET /api/flightops/review/{id}`; le tracce sono una richiesta a parte, `…/tracks`, perché pesano e servono solo
  alla mappa. Meteo (T16) e controlli (T17) hanno il loro posto nella risposta e dicono «non disponibile».
- **La riapertura** (`…/reopen`, motivazione obbligatoria): da `Accepted`, `Rejected` o `ToModify`; torna `InReview` **in mano a chi
  riapre**, con il lease. La mail al pilota parte alla nuova decisione.
- **Le mail** (`flightops.pirepAccepted`, `pirepToModify`, `pirepRejected`): tour e leg nella lingua del pilota, esito, nota,
  regole violate (codice e titolo), link alla pagina del tour. **Mai il nome del validatore**: i dati dell'intento non lo portano.
  Si accodano **dopo** il salvataggio della decisione, con il servizio del nucleo e il suo contesto: se l'accodamento fallisse,
  la decisione resta e manca la mail — lo stesso patto di ogni altra notifica dell'hub.
- **Il riepilogo** (`flightops.reviewDigest`, job giornaliero alle 07:00 UTC): a ogni validatore la coda dei tour che **può**
  validare (i propri PIREP esclusi) — quanti per tour e da quando aspetta il più vecchio; non parte a chi non ha niente.
- **Il job delle tracce** (ogni giorno alle 03:40 UTC): cancella le tracce dei PIREP decisi (accettati o rifiutati, non contestati)
  da più di `trackRetentionDays` contando da `decided_at`, e dei ritirati contando dal ritiro. Attraverso l'interceptor come ogni
  scrittura (un test di architettura vieta le istruzioni in blocco), leggendo solo le chiavi.
- **La mail** porta la rotta, la data, la nota e le regole: nessun numero di leg, che in un `Open` non c'è. Le parole «nessuna nota» e
  «nessuna» sono chiavi del modulo (`mail.flightops.noNote`, `noRules`).
- **Il pilota** legge ora nel suo PIREP la nota e le regole violate, mai chi ha deciso. Il profilo del pilota nella pagina di
  validazione conta le leg del tour (inviate, accettate, rifiutate) e i ban; le **contestazioni** arrivano con T14 e fino ad allora
  sono zero.
- **I PIREP inviati prima di T13** non hanno traccia: la pagina dice «non disponibile». In produzione non ce ne sono.

## 5. Che cosa si è toccato

- Nucleo: `AlsoWrittenWithAttribute` e la rete in `HubSaveChangesInterceptor`; `IModule.NotificationTypes`,
  `NotificationTypeCatalog` e chi li legge (servizio, preferenze, schermo del profilo); `IPermissionHolders` e
  `UserSyncService.HoldersOfAsync`; `ThenBy(DefaultOrder)` in `MapCrudExtensions`.
- Modulo: `Tours.ReopenDecisions` in `TourPermissions` e in `positionGrants` (`division.json`, `division.example.json`,
  non in `division.xx.json`, che non ne ha); `Review/` (code, pagina, presa, decisione, riapertura, suggerimento, mail,
  riepilogo); le tracce in `Pireps/` (`TrackCodec`, `TrackRetentionJob`); `trackRetentionDays` nelle impostazioni; la preferenza
  `flightops.reviewQueueOrder` dichiarata; migrazione `AddValidation`.
- Piano 0.94; design M2 §1.8, §1.11, §4.2.1, §4.3, §7.1, §7.2, §10; piano di implementazione, parte C, T13.
