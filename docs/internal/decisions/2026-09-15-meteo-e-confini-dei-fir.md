# Due fonti esterne nuove: il meteo e i confini dei FIR

**Data:** 15 settembre 2026 — fase T0 di M2 (verifiche fatte la sera del 15)
**Stato:** il **che cosa** è deciso da Carmine nel design dei tour (`05-design-m2.md` §1.13, §3.3, §15.2 n.12 e n.14); le **verifiche**
e la **forma** sono di Claude, da confermare nella revisione della PR di T0. ⚠️ Una verifica resta a metà (§3.3, la pagina legale di OpenAIP).
**Regola applicata:** `CLAUDE.md` §5, caso **(c)**: due fonti esterne nuove, che il nucleo nomina (piano §4.2: «questo nomina qualcuno fuori?»).
**Estensioni del nucleo** n.12 e n.13 di `05-design-m2.md` §11. Fasi **T1** (FIR), **T2** (meteo), **T16** (il meteo salvato).

## 1. Che cosa serve

- **Il meteo**: METAR e TAF di partenza, arrivo e deviazione nell'intervallo del volo, per verificare una deviazione per meteo e le
  VMC dei tour VFR (controllo `vmc`). Il meteo di un giorno passato non si recupera sempre dopo, quindi si salva.
- **I confini dei FIR**: per proporre al pilota gli ATC contattati (i FIR attraversati dai punti delle tracce) e per il paese di un
  punto. L'API IVAO dà l'elenco dei centri, non i poligoni; vIPI ha le forme dei settori italiani, non del mondo.

## 2. Il meteo — che cosa dice NOAA, provato il 15 settembre

Da `aviationweather.gov/data/api/` e dal suo `openapi.yaml`, più sette chiamate di sola lettura:

- **Limiti**: 100 richieste al minuto, oltre si viene bloccati; chiedono un `User-Agent` proprio; per scaricare molto raccomandano i
  **file di cache** (`/data/cache/metars.cache.csv.gz`, e il gemello dei TAF) invece di interrogazioni grandi o frequenti.
- **Storia dei METAR**: `date` (fine della finestra) più `hours` (quante ore indietro) chiedono una finestra passata. Trovati METAR a
  **18 giorni** indietro e **nessuno a 30**; a 76 giorni risponde 400. Una chiamata ha restituito esattamente **500 righe**: sembra un
  tetto per richiesta, quindi una storia lunga si chiede a finestre.
- ⚠️ **Il TAF passato c'è**, e **il design va corretto**: diceva «il TAF di un volo passato non c'è». Con `date` NOAA ha restituito un
  TAF di **7 giorni** prima; a 45 giorni risponde 400. Fin dove arrivi esattamente non è misurato.
- **IVAO e VATSIM** danno solo il METAR **attuale**: servono da ripiego nel job, non per la storia.

## 3. La decisione

### 3.1 Il meteo

- **`IWeatherSource` nel nucleo** (`Core/Weather/`), con due domande: «i bollettini attuali di questi aeroporti» e «i METAR e i TAF di
  questo aeroporto fra queste due ore». NOAA e VATSIM li nomina solo quella cartella; la parte IVAO passa dall'unico `IIvaoApiClient`
  (`/v2/airports/{icao}/metar`, **minuscolo**). Un test di architettura: nessun modulo nomina NOAA né VATSIM.
- **Il job ogni 30 minuti** (T16) legge i **due file di cache** di NOAA (METAR e TAF di tutto il mondo, due richieste) e ne tiene gli
  aeroporti delle leg dei tour aperti o in chiusura. Due richieste ogni mezz'ora qualunque sia il numero di aeroporti, com'è raccomandato.
  Se NOAA non risponde: METAR da IVAO, poi da VATSIM, per quegli aeroporti; il TAF salta il giro.
- **All'invio del PIREP** gli aeroporti toccati dal volo che non erano in elenco si chiedono all'API con `date` e `hours` sulla finestra
  del volo, **METAR e TAF** (correzione di §2). La finestra di un PIREP (`report_window_days`, 7 di default) sta ben dentro i 18 giorni
  misurati. Oltre, il validatore vede «non disponibile».
- **Un `User-Agent`** con il nome del prodotto e l'indirizzo del sito da `division.json`, e un tetto di richieste nel client (Polly, come
  il client IVAO).
- Il salvataggio (`fo_weather_reports`) e la cancellazione restano del modulo, come nel design.

### 3.2 I confini dei FIR

- **Da OpenAIP**, API `api.core.openaip.net`, chiave nell'intestazione `x-openaip-api-key`, nei segreti. Tipo **10** = FIR, **11** = UIR,
  filtrabili per paese (ISO a due lettere).
- **`ref_firs`** nel nucleo (tabelle `ref_`, come gli aeroporti): `code`, `name`, `country`, `type` (FIR o UIR), il poligono in GeoJSON e
  il riquadro che lo contiene (quattro colonne, per scartare in fretta). Un job **settimanale** (i confini cambiano di rado), che non
  cancella niente se la risposta è vuota (come `RefDataSyncJob`).
- **Il punto nel poligono** si calcola in memoria nel nucleo (qualche centinaio di poligoni, in cache come `FirDirectory`): `IFirLocator`
  «in quali FIR sta questo punto». Il modulo non nomina OpenAIP (test di architettura).
- **La copertura non è garantita**: il gestore di OpenAIP scrive che, secondo il paese, i FIR possono mancare o essere divisi in settori.
  Dove manca, la proposta degli ATC contattati si ferma agli aeroporti e il resto è «non verificabile»: mai un errore.
- **Senza chiave** (un fork che non l'ha chiesta) il job non parte e la proposta usa solo gli aeroporti.

### 3.3 La licenza di OpenAIP — verificata a metà

- Lo **schema dell'API** dichiara **CC BY-NC 4.0** e chiede «un link di attribuzione a OpenAIP (https://www.openaip.net) come fonte dei
  dati» nell'applicazione. Una fonte più vecchia parla di CC BY-NC-SA.
- ⚠️ **La pagina legale** (`openaip.net/legal`) e quella dei termini della chiave rispondono **403** a una lettura automatica: non le ha
  lette nessuno. **Da leggere a mano da Carmine** quando crea l'account e la chiave (che Claude non può creare).
- **Che cosa vuol dire per l'hub**, se la licenza è quella: IVAO e l'hub non sono commerciali, quindi l'uso è ammesso; la copia nel
  database e le risposte derivate («in che FIR sta questo punto») sono ammesse con l'attribuzione; i poligoni **non si ripubblicano**
  (l'hub non li mostra, li usa). **L'attribuzione** sta dove il dato derivato si vede: nel form del PIREP accanto alla proposta degli ATC
  e nella pagina di validazione. **Chi forka** per un uso commerciale non può usare questi dati: `FORKING.md` lo dice.

## 4. Alternative scartate

| Alternativa | Perché no |
|---|---|
| Interrogare NOAA per ogni aeroporto a ogni giro | centinaia di richieste ogni 30 minuti contro un limite di 100 al minuto, quando NOAA raccomanda i file di cache |
| Salvare il meteo di tutti gli aeroporti del mondo | spazio e cancellazioni per dati che nessun PIREP guarda |
| I confini dai file per paese di OpenAIP (bucket pubblico) | nessun file del mondo: circa duecento richieste a settimana contro un limite per secondo, invece di poche pagine dell'API |
| Le forme dei settori di vIPI | solo l'Italia, e un modulo che dipenderebbe da un prodotto che una divisione che forka non ha |
| I confini dei FIR scritti a mano dallo staff | un dato del mondo da tenere aggiornato, cioè quello che Carmine ha escluso («non voglio altra roba da aggiornare») |

## 5. Che cosa si tocca

- **Codice**: `Core/Weather/` con `IWeatherSource` e i tre client (T2); `ref_firs`, il job e `IFirLocator` (T1); la chiave nei segreti e la
  sua voce in `secrets.example`; il test di architettura.
- **Documenti**: `FORKING.md` (la chiave OpenAIP facoltativa, la licenza non commerciale, l'attribuzione); la pagina dei crediti del sito.
- **Da fare fuori dal codice**: Carmine crea l'account OpenAIP e la chiave, e legge termini e licenza (§3.3).
- **Piano** 0.79: §9.1 (due righe), §9.7 (fonti esterne), §10. **Design** `05-design-m2.md` §1.13 (il TAF passato c'è).
