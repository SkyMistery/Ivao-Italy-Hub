# Il meteo salvato (T16)

**Data:** 24 settembre 2026 — fase T16 di M2
**Stato:** **decisa** (Carmine, 24 settembre 2026, due risposte in apertura; il resto è design già scritto o scelta tecnica dichiarata qui
sotto)
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: il **che cosa** è nel design (`05-design-m2.md` §1.13) e nella nota
`2026-09-15-meteo-e-confini-dei-fir` §3.1; la fonte (`IWeatherSource`) è del nucleo da T2. Qui la forma di `weatherRetentionDays`, che il
design lasciava a T16, e come il modulo tiene e mostra i bollettini. **Nessuna estensione del nucleo.**

## 1. Che cosa serve

Piano di implementazione, parte C, T16: `fo_weather_reports` senza doppioni; il job ogni 30 minuti sugli aeroporti delle leg dei tour aperti
o in chiusura; all'invio del PIREP gli aeroporti toccati che mancano, METAR e TAF della finestra del volo; la cancellazione; METAR e TAF
nella pagina di validazione, in evidenza con una deviazione `Weather`.

## 2. Le due risposte di Carmine

Carmine ha chiesto prima di partire se il meteo si scarica solo per gli aeroporti dei tour (sì, §1.13) e se i bollettini non legati ai
PIREP si cancellano passato il tempo massimo di riporto. Il design diceva una cosa più stretta, e le due differenze gli sono state chieste:

| Domanda | Risposta |
|---|---|
| Il meteo di un volo il cui PIREP è deciso: si tiene o si cancella? | **Si cancella**, come nel design: passato il tempo, un bollettino resta solo finché un PIREP di quel giorno su quell'aeroporto aspetta una decisione. Scartato: tenere i bollettini della finestra del volo finché esiste il PIREP (chi riapre una decisione vecchia avrebbe il meteo, ma è un'eccezione alla regola di Carmine per un caso raro). |
| Quanto vale `weatherRetentionDays`? | **La finestra di riporto più lunga** (`report_window_days`) fra i tour aperti o in chiusura; il default della divisione (`defaultReportWindowDays`) quando non ce n'è nessuno. **Non è un'impostazione**: segue i tour, come il design diceva. Scartata: una chiave fissa in `division.json`. |

## 3. Com'è fatto

- **`WeatherArchive`** (`Weather/` del modulo) è l'unico posto che sa quali aeroporti si guardano, come si salva, che cosa manca a un volo,
  quanto resta un bollettino e che cosa vede il validatore. Il job e l'invio lo chiamano soltanto. Il modulo non nomina nessuna fonte:
  chiede a `IWeatherSource`.
- **`fo_weather_reports`** (`WeatherBulletin`): `icao`, `kind` (`Metar`/`Taf`, per nome), `issued_at`, `raw`, `source`, `fetched_at`.
  **Indice unico** su aeroporto, momento e tipo: il salvataggio legge prima quelli che ci sono e, se un invio e il job salvano lo stesso
  bollettino nello stesso istante, l'indice decide e chi perde salva il resto uno per uno. Il momento si tiene al secondo, come lo danno
  le fonti.
- **Il job ogni 30 minuti** (`WeatherJob`, ai minuti 5 e 35): gli aeroporti delle leg non ritirate dei tour pronti, usciti e non oltre
  `close_at + report_window_days` (sottotour compresi); un tour `Open` non ha leg e non ne aggiunge. Come chiedere lo decide il nucleo: sopra
  i 60 aeroporti il file di cache dei METAR di tutto il mondo, altrimenti a gruppi di 40; **i TAF sempre a gruppi**, perché il file di cache
  dei TAF non esiste (misurato in T2: la nota del 15 settembre va corretta, §4).
- **All'invio e al reinvio**, dopo il salvataggio del PIREP (come le piste di T1): ogni aeroporto del report — la leg, la deviazione, i voli
  — che **non ha un METAR salvato nella finestra del volo** si chiede alla storia della fonte, METAR e TAF. È più largo di «gli aeroporti
  che non erano in elenco»: copre anche un giro del job andato a vuoto. Il tempo massimo è di 20 secondi e un errore non rifiuta mai
  l'invio: il validatore vedrà «non disponibile».
- **La finestra del volo**: da un'ora prima del primo decollo a un'ora dopo l'ultimo atterraggio; un volo senza atterraggio si guarda per 12
  ore. Il **TAF in vigore** è l'ultimo emesso prima della finestra (fino a 30 ore prima) più quelli emessi durante.
- **La cancellazione** (`WeatherRetentionJob`, ogni giorno alle 03:50 UTC): un bollettino se ne va quando è più vecchio di
  `weatherRetentionDays` **più un giorno** (il METAR del mattino appartiene a un volo della sera riportato l'ultimo giorno) **e** nessun PIREP
  `Queued`, `InReview`, `ToModify`, o `Rejected` con una contestazione aperta, ha volato quel giorno su quell'aeroporto; il giorno prima del
  volo resta con il volo, per il TAF. Un PIREP deciso (anche ritirato) non tiene niente. Righe cancellate con `SaveChanges`, a gruppi.
- **La pagina di validazione**: `ReviewDto.weather` sostituisce `weatherAvailable` — per ogni aeroporto del report il ruolo (partenza,
  arrivo, deviazione, toccato dal volo), la finestra, i METAR e i TAF, ognuno con la fonte. Senza bollettini la pagina dice «non
  disponibile», mai «bel tempo». Con una deviazione `Weather` l'arrivo previsto va in cima con un avviso (§3.4 del design).
- **Nei test** nessuno chiede le fonti vere: la factory registra un `WeatherDouble` vuoto, e i test del meteo gliene danno uno con i loro
  bollettini. Il test di architettura «nessuna fonte del meteo fuori dalla cartella» ora guarda la cartella **del nucleo**, non una qualsiasi
  cartella `Weather` (quella del modulo sarebbe stata un buco).

## 4. Correzioni ad altri documenti

- Nota `2026-09-15-meteo-e-confini-dei-fir` §3.1: «legge i **due file di cache** di NOAA» → il file dei TAF non esiste; i TAF si chiedono a
  gruppi. Corretto lì con un rimando qui.
- Design §1.13 e la tabella delle impostazioni (`weatherRetentionDays`): la forma decisa qui.

## 5. Che cosa non si è verificato

- La storia dei TAF di NOAA chiesta con la sola `date` restituisce il TAF in vigore **alla fine** della finestra: per un volo lungo il TAF in
  vigore al decollo potrebbe mancare, se il job non l'aveva già salvato. Non misurato; il job lo salva comunque per gli aeroporti delle leg.
- In sviluppo il job gira con NOAA vero: il «fatta quando» (due giri senza doppioni, un PIREP del corpus con i METAR del suo volo) si
  guarda sul banco, non nei test.
