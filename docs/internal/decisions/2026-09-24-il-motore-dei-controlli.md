# Il motore dei controlli (T17)

**Data:** 24 settembre 2026 — fase T17 di M2
**Stato:** **decisa** (Carmine, 24 settembre 2026: tre risposte in apertura e una a metà fase, §2; il resto è design già scritto o scelta
tecnica dichiarata qui sotto)
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: il motore è nel design (`05-design-m2.md` §6.1–§6.4) e i controlli nuovi nella nota
`2026-09-24-i-controlli-dai-pirep-veri`; qui la forma, le quattro risposte e una correzione al lettore del tracker del nucleo (§4), che è
un errore di lettura e non un'estensione.

## 1. Che cosa serve

Piano di implementazione, parte C, T17: `IFlightCheck`, il job all'invio e al reinvio, `fo_check_results`, gli errori suggeriti con la
conferma del validatore registrata; i controlli sul piano (`callsign`, `aircraft`, `alternate`, `equipment`, `repeatedRoute`,
`flightRules`, `planAtTakeoff`, `flightPlanForm`); la sezione dei controlli nella pagina di validazione e nella coda; `semicircularLevels` e
`atcCoverage` dell'agente `Unavailable` senza agente; prima di tutto, le fixture dei PIREP veri.

## 2. Le risposte di Carmine

| Domanda | Risposta |
|---|---|
| Oltre a W sopra FL285, altre lettere con una condizione? | **Anche J1 sopra FL285** (il mandato CPDLC europeo ha la stessa soglia). E due cose in più: **le lettere della casella 10b (transponder) per regola di volo**, come la 10a; **R senza `PBN/`** nella casella 18 fa fallire `flightPlanForm`, come Z senza COM/NAV/DAT. |
| Come si prendono VID, data e callsign dei PIREP della nota §5? | **Da Chrome**, nella sessione da amministratore di Carmine sul sistema dei tour di oggi, in sola lettura (`fetch()` delle pagine dei PIREP, nessun form toccato), come il 24 settembre. |
| 881923 (N260MA, VFR, accettato) deposita `F085` come livello: la nota lo dava «tutto passa». | **Fallisce**: in un piano V il livello è `VFR`. La nota §5 si corregge (§6). Scartati: una riga solo nell'evidenza, e non guardare il livello. |

## 3. Com'è fatto

- **Le fixture** (`tests/fixtures/ivao/`): i **15** PIREP della nota §5 (la nota diceva 16: gli identificativi distinti sono 15), ognuno la
  sessione del tracker che era in volo al decollo dichiarato fra i due aeroporti della leg, registrati con
  `tools/record-ivao-fixtures.mjs --list <file> 780002` — un modo nuovo dello strumento, con l'elenco (che contiene i VID veri) **fuori dal
  repository**. Tutti sotto il VID 780002; `tracker-reports-780002.json` dice quale sessione ha volato ogni PIREP, senza VID. 3,5 MB.
- **`IFlightCheck`** (`Checks/` del modulo): `Key` e **`Evaluate(context, parameters)`, una funzione pura e sincrona** — il design diceva
  `EvaluateAsync`: il contesto si raccoglie una volta sola (`FlightChecks.ContextAsync`), e un controllo che non legge il database né la rete
  si prova sul corpus senza server. I parametri e i loro valori di partenza restano **solo** in `CheckCatalog` (T9).
- **Il contesto**: il PIREP, la leg congelata, il tipo del tour, i voli con **tutte le revisioni** e quella al decollo come l'ha scelta
  l'invio, le tracce quando ci sono, le regole del callsign per livello, gli aerei ammessi con i gruppi espansi, le rotte già volate dal
  pilota sul tour, le impostazioni. Meteo, piste e archivio ATC arrivano con T18.
- **Quali controlli girano**: quelli che una regola congelata nomina, con i suoi parametri composti; più quelli che solo un errore nomina,
  con i valori di partenza. I due dell'agente mai sul server.
- **Quando**: **subito dopo l'invio e il reinvio** (dopo il meteo, che T18 leggerà), senza mai rifiutare l'invio; e **`FlightCheckJob` ogni
  dieci minuti** riprende i PIREP in coda o in revisione con `checks_ran_at` vuoto o più vecchio di `queued_at` (un invio il cui giro non è
  stato salvato, un PIREP inviato prima di T17), 50 alla volta. Una riga nel log dei job solo quando ha lavorato o è fallito. Un PIREP
  cambiato nel frattempo (concorrenza) si riprova al giro dopo.
- **`fo_check_results`**: `pirep_id` (FK in cascata), `check_key`, `outcome` (`Passed`, `Failed`, `Unavailable`), `evidence_json`, `ran_by`
  (`Server`, `Agent`), `ran_at`; unico su PIREP, controllo e chi l'ha eseguito. Il server sostituisce i suoi a ogni giro. `fo_pireps` ha
  `checks_ran_at`. Una migrazione, additiva.
- **L'evidenza** è un elenco di righe: dal server **una chiave i18n con i suoi valori** (`flightops:evidence.equipmentMissing` con
  `letters`, `rules`, `filed`), che la pagina scrive nella lingua di chi legge; dall'agente (T19) **testo**. Su un PIREP con deviazione ogni
  riga dice di quale volo.
- **Un controllo che lancia un'eccezione** è `Unavailable` con la riga «non è riuscito a girare», mai `Failed`.
- **I suggerimenti**: gli errori congelati con la `check_key` di un controllo fallito (del server o dell'agente) diventano righe di
  `fo_pirep_errors` con `suggested_by_check` e **non confermate**. Lo snapshot degli errori porta ora `CheckKey`. **La decisione tiene** le
  righe suggerite accanto a quelle spuntate, con `confirmed` vero solo dove il validatore ha spuntato: così si sa quali controlli sbagliano
  (§6.3). Contano solo le confermate (conteggi dell'anno, regole violate nella mail).
- **La pagina di validazione**: `ReviewDto.checks` (per ogni esito: chiave, esito, righe, chi, quando, errori che suggerisce) e
  `checksRanAt` al posto di `checksAvailable`. Un controllo dell'agente che le regole nominano e nessun agente ha mandato compare
  `Unavailable`. La tabella degli errori diceva già «suggerito da un controllo» (T13b).
- **La coda**: `failedChecks` e `checkSuggestion` (l'esito a cui portano gli errori suggeriti, con i conteggi dell'anno del pilota; nullo
  finché i controlli non sono girati); la lista li scrive in una parola: «nessuno fallito», «falliti, accettare», «falliti, rifiutare».

### 3.1 I controlli sul piano

| Chiave | Che cosa guarda |
|---|---|
| `callsign`, `aircraft` | Come all'invio (che li rifiuta già): passano, a meno che le regole del tour siano cambiate. |
| `flightRules` | Le regole di volo del piano al decollo fra quelle del parametro `rules` (I, V, Y, Z; parte con tutte e quattro). |
| `planAtTakeoff` | Una revisione depositata **prima** del decollo; le revisioni dopo si elencano e non contano (GR9). |
| `alternate` | Manca, o è la destinazione: fallisce. `ZZZZ` senza `ALTN/`: fallisce. Uguale alla partenza: passa e lo dice. |
| `equipment` | Per regola di volo: `lettersI|V|Y|Z` (10a) e `transponderI|V|Y|Z` (10b); `highLevelLetters` (parte con **W e J1**) si chiedono solo se il piano chiede un livello sopra `highLevelFl` (parte da **285**), nella casella 15 **o nella rotta** (`/N0459F380`). Una lettera che dice di più vale per quella che contiene: `S` della 10a è anche V, O, L; nella 10b un Mode S con identità e quota (E, H, L) è un `S`, e con S o P è un `C` (ICAO Doc 4444, appendice 2). |
| `flightPlanForm` | Riga per riga: `REG/` quando il callsign ha la forma di una compagnia (tre lettere e un numero; una marca come `ICELLO` o `N260MA` no; un caso dubbio lo dice senza fallire), `RMK` senza barra, Z senza COM/NAV/DAT, **R senza PBN/**, nel piano V **`DCT`** e **un livello che non sia `VFR`**, e fuori dai piani V una SID o una STAR scritta nella rotta (prima o ultima parola della forma «punto, cifra, lettera», `OBUTI2W`) dove l'aeroporto non comincia con un prefisso dell'impostazione **`routeProcedurePrefixes`** (parte da `ED`, `LO`; la modificano coordinator e assistant). |
| `repeatedRoute` | Solo sui tour `Distance` e `Open`: A→B già volata dal pilota sul tour (non ritirata né respinta). |

La forma del parametro di `equipment` cambia: `letters` non c'è più. Nessun dato da migrare (nessuna regola in produzione); una regola di
sviluppo con `letters` non chiede più niente finché qualcuno la risalva.

## 4. Una correzione nel nucleo: quando è stata depositata una revisione

Sui PIREP veri, **tutte le revisioni di un piano hanno lo stesso `createdAt`** finché la rotta non cambia; quello che cambia per revisione è
**`updatedAt`**. SCI044 (880760) è decollato alle 08:22 con la revisione 2; le revisioni 3 e 4 hanno `updatedAt` 08:39 e 08:44 e lo stesso
`createdAt` della prima. Il lettore del tracker (`IvaoTrackerReader`, T2) prendeva `createdAt` come «depositata alle», quindi il piano al
decollo (T11a) era sempre l'ultima revisione. Ora `FiledAt` è `updatedAt`, con `createdAt` di ripiego: sulle fixture di giugno il piano al
decollo non cambia, su SCI044 diventa la revisione 2. I PIREP già inviati in sviluppo tengono il numero scelto allora.

## 5. Il corpus

Con le regole dei tour come le ha date Carmine per il Turboprop (I e Y: S, D, G, R, W, Y; V nessuna) applicate a tutti i tour IFR, V per i
VFR, e per il PIREP di London City gli aerei ammessi senza il B737:

| PIREP | Controlli sul piano che non passano | Come la nota §5 |
|---|---|---|
| 879788, 879691, 879558 (B350) | `equipment`: mancano **D e Y** (W non richiesto: FL250, FL160, FL240) | sì (la nota diceva «manca Y») |
| 882171 (C152) | `flightPlanForm`: DCT nella rotta VFR | sì |
| 880760 (C208) | nessuno; `planAtTakeoff` elenca le revisioni 3 e 4 depositate dopo il decollo | sì |
| 881923 (F26T) | `flightPlanForm`: livello `F085` in un piano VFR | **no**: deciso da Carmine (§2), la nota si corregge |
| 877464 (B737 a London City) | `aircraft` | sì |
| 877596, 877187, 877196, 880159, 879610, 881263, 881169, 876413 | nessuno (quello che la nota aspetta per questi è di T18) | sì |

**880159, capito**: la sessione vera di RYR73F è del 15 settembre, 15:45–17:37; gli orari dichiarati dal pilota (14:08–15:04) coincidono
con la fine di un'altra sessione **del 13 settembre** (LICC–LATI, finita alle 15:05). Il vecchio sistema ha preso quella: di qui «disconnesso
2921 minuti» (dal 13 alle 15:05 al 15 alle 15:45) e i due aeroporti sbagliati. Da noi il pilota sceglie la sessione, e T18 lo proverà.

## 6. Correzioni ad altri documenti

- Nota `2026-09-24-i-controlli-dai-pirep-veri` §5: 881923 non «tutto passa» ma `flightPlanForm` non passa; §7: le fixture ci sono.
- Design §6.1, §6.2 e §6.4: la forma di `Evaluate`, il job, `equipment`, `flightPlanForm`.
- `tests/fixtures/ivao/README.md`: il corpus e `updatedAt`.

## 7. Che cosa non si è verificato

- Le **lettere equivalenti** (S della 10a, S e C della 10b) sono una lettura del Doc 4444, non una risposta di Carmine: da confermare.
- La **forma del callsign** (compagnia, marca, dubbio) e la **forma di una procedura** nella rotta sono euristiche provate sul corpus e su
  pochi casi scritti: un nome di punto con una cifra e una lettera in fondo alla rotta sarebbe letto come STAR.
- Le regole vere dei tour del corpus (quali lettere chiede ciascuno) non sono state lette: il corpus usa quelle del Turboprop per tutti gli
  IFR. Il «fatta quando» vale con quelle.
- Il job gira in sviluppo solo nei test d'integrazione; sul banco lo si vede con un PIREP inviato prima di T17.
