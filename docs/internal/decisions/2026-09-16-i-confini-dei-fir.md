# I confini dei FIR non vengono da OpenAIP: vengono dal dataset di VATSpy

**Data:** 16 settembre 2026 — fase T1 di M2
**Stato:** **decisa** (Carmine, 16 settembre 2026, messo davanti alle misure). Sostituisce la scelta di
`2026-09-15-meteo-e-confini-dei-fir.md` §3.2, presa in T0 sulla documentazione e non sui dati.
**Regola applicata:** `CLAUDE.md` §5, caso **(c)**: una fonte esterna nuova, con la sua licenza e la sua attribuzione.

## 1. Perché si riapre una decisione di ieri

La nota di T0 sceglieva **OpenAIP** (tipi `10` = FIR e `11` = UIR) perché lo schema della sua API li dichiara e la licenza
sembrava compatibile. In T1, con la chiave vera di Carmine, **si è misurato che cosa c'è dentro**, e non regge:

| Misura (16 settembre 2026, chiave vera) | Risultato |
|---|---|
| Airspace di tipo 10 in **tutto il mondo** | **108** |
| Tipo 11 (UIR) | **0** |
| Italia | **3**, e sono i FIR veri: Brindisi, Milano, Roma |
| Regno Unito, Spagna | **0** |
| Stati Uniti | **1** (Boston) |
| Che cosa sono gli altri | anche cose che FIR non sono: «AALBORG LOCAL ATS», «AARHUS LOCAL ATS», tre «BEIRA» |

Un tour italiano funzionerebbe; un volo Londra–Madrid no, e un tour `Open` «giro del mondo» nemmeno. La domanda che il
nucleo deve saper rispondere è **«in quale FIR sta questo punto»** lungo rotte che vanno ovunque (design §3.3), quindi una
copertura a macchia di leopardo non è una copertura.

## 2. La decisione

- **La fonte è il dataset di VATSpy** (`vatsimnetwork/vatspy-data-project`, file `Boundaries.geojson`), misurato lo stesso
  giorno: **1121 confini**, mondiali (LIRR, EGTT, LECM, KZBW, KZLA, EDGG, LFFF, RJJJ, SBBS ci sono tutti), aggiornato il
  12 settembre 2026, 2,0 MB, `MultiPolygon` con `id`, `oceanic`, `region`, `division`.
- **Si scarica, non si committa**: un job del nucleo lo prende e riempie `ref_firs`, come il job che tiene lo snapshot di
  IVAO. Il file non entra nel repository, così l'hub non **ridistribuisce** un dato altrui.
- **Licenza CC BY-SA 4.0**, quindi: **attribuzione** («FIR boundaries: VATSpy Data Project, CC BY-SA 4.0») dove il dato
  derivato si vede — il form del PIREP accanto alla proposta degli ATC e la pagina di validazione — e nei crediti del sito;
  e nessuna ridistribuzione del file. Le risposte che l'hub dà («questo punto sta in LIRR») sono un uso del dato, non una
  sua copia.
- **Il dataset porta anche i settori** (`LIRR-NE`, `LIMM-ES5`…): si tengono **solo i confini interi**, cioè gli `id` senza
  trattino. I settori sono di VATSIM, cambiano spesso e non servono alla domanda che facciamo.
- **Resta opzionale**: senza il file (fork che non lo scarica, rete assente) `ref_firs` è vuota e la proposta degli ATC si
  ferma agli aeroporti — «non verificabile», mai un errore. È la stessa regola di ogni fonte esterna del nucleo.
- **OpenAIP esce di scena per i FIR**, e con esso la chiave: resta scritta qui come alternativa se un giorno servissero le
  altre classi di spazio aereo (CTR, TMA, zone P/R/D), dove OpenAIP è invece ricco (in Italia: 279 zone di tipo 1, 272 di
  tipo 3, 113 CTR). La chiave che Carmine ha creato non si butta: non serve **ora**.
- ⚠️ **Che cosa si perde, detto chiaro**: i confini di VATSIM sono quelli con cui *VATSIM* divide lo spazio aereo, non
  necessariamente i confini AIP del giorno. Per il nostro uso — incrociare i punti di una traccia con le posizioni ATC che
  erano online, per **proporre** al pilota chi ha contattato — è esattamente la granularità giusta, e nessuna proposta
  decide niente: decide il validatore.

## 3. Alternative scartate

| Alternativa | Perché no |
|---|---|
| Tenere OpenAIP e degradare dove manca | metà Europa e quasi tutti gli Stati Uniti senza FIR: la funzione esisterebbe solo per i tour italiani |
| Niente FIR sul server, tutto all'agente del validatore | il pilota non ha Navigraph, e la proposta degli ATC è per lui (design §6.6 lo dice esplicitamente) |
| I `centerId` degli aeroporti IVAO come proxy | dicono il FIR di un aeroporto, non quello di un punto in rotta |
| Disegnare i confini a mano | il dato del mondo da mantenere a ogni ciclo, cioè quello che Carmine ha escluso |
| Committare `Boundaries.geojson` nel repository | ridistribuzione di un dato CC BY-SA dentro un repository Apache-2.0: si evita scaricandolo |

## 4. Che cosa si tocca

- **Codice (T1)**: `ref_firs` (codice, nome, `oceanic`, regione, poligono, riquadro che lo contiene), il job settimanale che
  lo riempie dal file, `IFirLocator` («in quali FIR sta questo punto»). Nessun modulo nomina VATSpy: un test di
  architettura, come per il meteo.
- **Documenti**: `2026-09-15-meteo-e-confini-dei-fir.md` §3.2 e §3.3 rimandano qui; `FORKING.md` (il dato facoltativo e
  l'attribuzione); la pagina dei crediti del sito.
- **Piano 0.80**: §9.1 (la riga dei confini dei FIR), §9.7, changelog.
