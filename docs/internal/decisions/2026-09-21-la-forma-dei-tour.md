# La forma dei tour: hub e rotazioni, sottotour, callsign; T7 in tre

**Data:** 21 settembre 2026 — fase T7b di M2
**Stato:** **decisa** (Carmine, 21 settembre 2026, cinque domande in apertura di T7b; il resto sono estensioni di meccanismi
esistenti e scelte di forma, dette qui perché toccano il nucleo o correggono il design).
**Regola applicata:** `CLAUDE.md` §5: **(c)** per la divisione della fase, le date e l'indirizzo dei sottotour, la loro proiezione e
la forma dei vincoli sul callsign; **(b)** per `CrudOptions.BeforeAuthorize` (si estende il motore, non lo si aggira).

## 1. Che cosa serviva

Aprendo T7b, cinque bivi:

1. **La fase era ancora grande**: hub e rotazioni, sottotour, vincoli sul callsign e il tour `Open` — sei obiettivi, undici filtri,
   sei regole di sequenza, ognuno con i suoi parametri e il suo form.
2. **Date e indirizzo di un sottotour** (lasciata aperta da T7a, nota `2026-09-18-le-leg-dei-tour` §2): il design §2.7 fissava
   l'eredità di aerei, callsign e regole, non quella del periodo né dello `slug`.
3. **Ricerca e calendario di un sottotour**: con uno slug proprio, un sottotour pronto dentro un `Container` in bozza o nascosto
   finirebbe nella ricerca pubblica.
4. **Che cosa vincola un callsign**: il design §1.6 aveva `Prefix`, `Exact` e `Pattern`, liberi per `Allow` e `Deny`.
5. **Come si combinano i livelli**: §1.6 diceva «il consentito di una leg è l'unione di leg, tour e tour padre», §2.7 che il
   sottotour eredita i vincoli «se non ne ha di suoi». Con l'unione, un tour che consente ITY e una sua leg che consente AZA
   accettano su quella leg tutti e due.

## 2. Le decisioni

1. **T7b si divide ancora** (Carmine). **T7b** sono hub e rotazioni, sottotour e `Container`, vincoli sul callsign, e i loro controlli
   di «pronto»; chiude il «fatta quando» di T7 (un tour `Hub` con due hub, rotazioni e un collegamento, pronto). **T7c**, in una chat
   nuova, è il tour `Open`: `open_goal` con i parametri, `fo_tour_constraints` (filtri e regole di sequenza), la scheda e i controlli.
   *Scartato*: tutto in T7b (una PR più grande di T7a).
2. **Un sottotour ha date proprie; se non le ha, prende quelle del padre** (Carmine). Ogni data da sola: un sottotour può avere il
   suo rilascio e la chiusura del padre. Le date proprie stanno **dentro il periodo del padre** (controllo di «pronto»).
3. **Un sottotour ha uno `slug` proprio** (Carmine): `/tours/{slug}` come ogni tour, unico fra i tour.
4. **Ricerca e calendario li porta solo il `Container`** (Carmine): un sottotour proietta solo i suoi file (banner, foto, immagini
   del briefing); la pagina del padre elenca i sottotour (T10). Nessuna fuga da un padre in bozza o nascosto, e un calendario senza
   una voce per ogni sottotour. *Scartato*: ogni sottotour con le sue voci quando il padre è pubblico (il salvataggio del padre
   avrebbe dovuto riproiettare i figli e il job del rilascio guardare anche il padre). Il prezzo: il rilascio proprio di un
   sottotour non compare nel calendario.
5. **Un vincolo sul callsign è sulla compagnia** (Carmine): il callsign reale di una leg è **solo un suggerimento**, il pilota vola
   quello che vuole dentro le regole. Una regola `ITY` vuol dire «si vola solo con ITY», e quello che segue le tre lettere è a
   scelta del pilota. `match` ha due valori: **`Airline`** (tre lettere, per `Allow` e `Deny`) ed **`Exact`** (un callsign intero,
   **solo per `Deny`**: il Vintage Jet di Toursystem vieta quattro callsign Itavia di voli con incidenti mortali). *Scartati*:
   `Prefix` e `Pattern` del design (più larghi di quello che serve); solo codici compagnia (il caso Itavia non si esprimerebbe).
6. **Fra i livelli vince l'`Allow` più specifico, i `Deny` si sommano** (Carmine). Gli `Allow` valgono dal livello più vicino che ne
   ha — leg, poi tour, poi padre del sottotour —; i `Deny` di tutti i livelli valgono sempre e vincono. Nessun `Allow` a nessun
   livello: ogni compagnia. Concilia §1.6 e §2.7. *Scartato*: l'unione di §1.6.

## 3. Che cosa si estende, e perché

- **`CrudOptions.BeforeAuthorize`** (nucleo, motore CRUD): che cosa una riga prende da altre righe **prima** che il suo permesso
  venga chiesto. Serve alla decisione di T7a sulle righe figlie: un hub, una rotazione, un vincolo — e un sottotour — hanno
  dipartimento e maschera **del tour**, ma il motore chiedeva il permesso sulla riga appena applicata, che ha solo il dipartimento
  di base del modulo. Chi cura un tour con un secondo dipartimento nella maschera sarebbe stato rifiutato creando un hub, e
  `BeforeSave` arriva dopo il permesso. Il gancio gira dopo `Apply` e prima del secondo controllo del permesso, nella creazione e
  nella modifica; può rifiutare come `BeforeSave` (il tour che non esiste). Il motore continua a non sapere che cosa sia un tour.
  *Scartato*: verbi scritti a mano per hub, rotazioni e vincoli (il design §8.3 li vuole liste e form generati, e l'eccezione
  dichiarata di §16.6 è la sola `LegGrid`); mandare la maschera dal client (una riga deciderebbe da sé chi la modifica).

## 4. Le scelte piccole, dette qui

- **Le date del sottotour** sono nelle colonne di sempre (`release_at`, `close_at`), con due colonne nuove,
  **`release_from_parent`** e **`close_from_parent`**: una data presa dal padre è **copiata** a ogni scrittura e **segue** il padre
  quando cambia, come la maschera. Così `TourState` resta una funzione del tour e basta, e `NeedsOwnDailyLimit` resta SQL.
  Il form mostra vuota una data presa dal padre.
- **Un sottotour**: il padre è un `Container` che non è un template; il sottotour non è un `Container` (un livello solo), non è un
  template, non ha award; il padre si sceglie alla creazione e non cambia. Un `Container` con sottotour non cambia tipo. Una data
  propria di un sottotour **pronto** fuori dal nuovo periodo del padre rifiuta il salvataggio del padre. Nella lista di
  `/staff/tours` i sottotour non compaiono (filtro `parent`, di default `none`): stanno nella scheda del padre.
- **Hub e rotazioni** sono due risorse del motore (`/api/flightops/hubs`, `/api/flightops/rotations`) filtrate per tour, solo su un
  tour `Hub` che non è un template. Un hub è un aeroporto noto, una volta per tour; una rotazione ha `size` 2, 4 o 6. Un hub con
  rotazioni e una rotazione con leg **non si eliminano** (si toglie prima quello che ci sta sotto; una rotazione con report si ritira
  intera, T7a).
- **Una leg dice la sua rotazione** nella griglia (`rotationId`, colonna solo sui tour `Hub`) e se è un **collegamento** fra hub
  (`kind`); `seq_in_rotation` **lo tiene il server** dall'ordine delle leg, come il numero. Una leg di rotazione o di collegamento su
  un tour che non è `Hub` è rifiutata alla scrittura.
- **I controlli di «pronto» del tour `Hub`** (in `TourShape`, funzione pura): almeno un hub; ogni hub con almeno una rotazione; ogni
  rotazione **con esattamente `size` leg** non ritirate, la prima **in partenza dall'hub** e l'ultima **in arrivo all'hub** (il
  design non chiede la continuità fra le leg di mezzo, e il controllo non la chiede); una rotazione tutta ritirata non conta più;
  ogni leg normale in una rotazione; un collegamento fra due hub diversi del tour. Un problema di una rotazione è
  `rotations.{ICAO dell'hub}.{posizione}` («Rotazione 2 di LIRF»).
- **Il `Container`**: almeno due sottotour, `required_subtours` obbligatorio e non oltre il loro numero. Contano i sottotour che
  esistono, pronti o no.
- **Ogni scrittura di una riga figlia di un tour pronto** — hub, rotazione, leg — passa i controlli di «pronto» con le righe come la
  scrittura le lascia, come le leg in T7a.
- **Chi decide un callsign** è una funzione pura, `CallsignRules.Allows`, provata qui con i suoi casi; la usa il form del PIREP in
  T11. Un vincolo di una leg si sceglie nella stessa lista, con la leg; un template copia i vincoli **del tour** (non ha leg).

## 5. Correzioni al design

- **§1.2**: `release_from_parent`, `close_from_parent`; lo slug e le date di un sottotour.
- **§1.6**: `match` è `Airline` o `Exact` (solo `Deny`); l'`Allow` più specifico vince, i `Deny` si sommano.
- **§2.7**: le date, lo slug, la proiezione del sottotour.
- **§14 / `06` parte C**: T7 è T7a, T7b, T7c.

## 6. Che cosa si tocca

Piano 0.86 (§16.6 `BeforeAuthorize`); design M2 §1.2, §1.3, §1.6, §2.7, §14; `06` parte C (T7b, T7c). Codice: `Core/Data/Crud`
(`BeforeAuthorize`); nel modulo `Shape/` (hub, rotazioni, vincoli sul callsign, `CallsignRules`), `TourShape` esteso, i sottotour in
`Tour`, `TourRules` e nei DTO, la migrazione `AddTourShape`; nel frontend le schede «Hub e rotazioni», «Sottotour», «Callsign» e le
colonne della rotazione in `LegGrid`.
