# Regole ed errori: l'emendamento eredita, la copia aggiunge, i valori di partenza

**Data:** 22 settembre 2026 — fase T9 di M2
**Stato:** **decisa** (Carmine, 22 settembre 2026, tre domande in apertura; il resto sono scelte di forma, scritte qui perché si vedono
nelle schermate del FOD o in quello che T10, T11 e T17 leggeranno).
**Regola applicata:** `CLAUDE.md` §5 **(b)** per tutto: le regole e gli errori sono due risorse del motore CRUD con liste e form
generati; i parametri di una regola sono lo stesso lettore di `OpenCatalog` (T7c) su un catalogo nuovo di campi; il form della regola è
lo stesso «tipo, poi i suoi parametri» di `KindPicker`; il blocco è un blocco Data del modulo registrato in due metà. **Nessun
meccanismo nuovo** e nessuna estensione del generatore di form (vedi §3).

## 1. Che cosa serviva

Il design §1.7, §5 e §6.2 fissa il modello (`fo_rules`, `fo_errors`, `fo_rule_errors`), le regole effettive (§5.2) e il blocco degli
errori pubblici (§5.3). Aprendo T9 restavano aperte tre cose che cambiano il risultato:

1. **Un emendamento e i parametri che non cambia.** «Una regola del tour che la emenda ne cambia uno o più» (§1.7): gli altri restano
   legati alla generale o si fermano al momento dell'emenda?
2. **«Copia le regole da un altro tour» su un tour che ha già regole sue.**
3. **I valori di partenza dei controlli** che il design non dà in numeri (raggio dell'atterraggio, parcheggio, 250 kt, sim rate).

## 2. Le decisioni

1. **L'emendamento eredita** (Carmine): salva **solo** i parametri che cambia; gli altri li legge dalla regola generale **a ogni
   lettura**, quindi un cambio della generale arriva ai tour che non l'hanno toccato. Il PIREP congela comunque tutto al primo invio
   (§5.4). *Scartata*: la copia di tutti i parametri all'emenda (un cambio della divisione non arriverebbe più a quel tour, e nessuno
   se ne accorgerebbe).
2. **La copia aggiunge** (Carmine): le regole **proprie** in vigore dell'altro tour (non quelle del padre, non le generali), con
   parametri ed errori collegati. Quello che il tour ha già **resta**: un emendamento della stessa regola generale, una regola con lo
   stesso codice; la copia li salta e l'esito dice quali. *Scartata*: sostituire (dopo una conferma) — un clic sbagliato cancellerebbe
   il lavoro del tour.
3. **I valori di partenza** (Carmine): atterraggio entro **5 NM** dall'arrivo · parcheggio **2 min** prima e **2** dopo · 250 kt sotto
   FL100 con **10 kt** di tolleranza · sim rate **10 %** · disconnessioni **15/25 min** e VMC **5000 m / 1500 ft** come nel design.
   Sono valori da tarare sui voli veri in T17: stanno in `CheckCatalog` e una regola nuova li prende per quello che non scrive.

### 2.1 Le scelte di forma

4. **La tolleranza del decollo dalla testata resta un'impostazione** (`thresholdToleranceMeters`, design §1.11 e risposta 15: «una
   per il sistema, la cambiano FOC e FOAC»): il controllo `takeoffFromThreshold` non ha parametri di regola. La tabella di §6.4 la
   metteva fra i parametri: corretta.
5. **Si emenda solo una regola generale**, una volta per tour. Un sottotour ha prima le regole del contenitore e poi le sue; se emenda
   la stessa generale che il contenitore ha emendato, **vince il sottotour**, con i suoi numeri sopra quelli del contenitore.
6. **Il controllo di un emendamento è quello della sua regola**, qualunque cosa mandi il form: un emendamento cambia i numeri, mai che
   cosa misurano.
7. **Gli errori di un emendamento** sono quelli della regola generale **più** i suoi: la colonna «errori» e le regole effettive li
   contano insieme.
8. **Una regola generale ritirata si porta via i suoi emendamenti** dalle regole effettive; un emendamento ritirato lascia di nuovo in
   vigore la generale. Una regola generale che qualche tour emenda **non si elimina** (`ruleHasAmendments`): si tolgono prima gli
   emendamenti, o la si ritira.
9. **Il codice** («GR4», «IR3») è unico fra le generali in vigore e fra quelle in vigore di un tour; un emendamento può riprendere il
   codice della generale, perché sta nello spazio del tour.
10. **Titolo e testo in tutte le lingue della divisione** a ogni salvataggio, come il nome di un gruppo di aerei: il pilota li legge. Un
    errore vuole nome e descrizione in tutte le lingue; gli esempi sono facoltativi. Il massimo annuale c'è **solo e sempre** su un
    warning.
11. **Il blocco `flightops.errorCatalog` non ha proprietà**: mostra tutto il catalogo pubblico, per peso (info, warning, dangerous),
    con le regole **generali** in vigore collegate — mai quelle di un tour, che può essere una bozza. ⚠️ Il form delle proprietà di un
    blocco prende le etichette da `blocks.<tipo>` nei file del nucleo, dove un modulo non scrive: **il primo blocco di un modulo con
    proprietà** (T10, `tourCards`) dovrà estendere `BlockRegistration` con il prefisso delle sue etichette. Qui non serviva.
12. **Le regole effettive** sono un servizio (`EffectiveRules`) e un verbo di sola lettura, `GET /api/flightops/tours/{id}/effective-rules`,
    che la scheda «Regole» mostra e che T10 renderà pubblico. **La copia** è il secondo verbo scritto a mano
    (`POST …/tours/{id}/copy-rules`, `Tours.ManageRules`, e `Tours.ManageTemplates` su un template). La copia dei template usa la stessa
    funzione (`TourCopy.Rules`).
13. **I collegamenti regola–errore non sono auditati a parte**: sono scritti dal salvataggio della regola, e l'audit della regola non
    li mostra. Se servirà sapere chi ha tolto un errore da una regola, si aggiunge allora.

## 3. L'estensione n.9 del design, verificata

Il design §11 chiedeva di verificare se il generatore sa **disegnare uno schema scelto a runtime** e **una selezione multipla**. Li sa
fare tutti e due: lo schema a runtime è il `KindPicker` di T7c (due `SchemaForm`, il secondo con la chiave del tipo scelto), la
selezione multipla è `meta({ multi: true, choices })` (già usata per le categorie di scia). Gli **aggregati nella lista** («senza
errori», «senza regole») sono `CrudOptions.ToListPage` (T4a). **Nessuna estensione del generatore.**

## 4. Che cosa si tocca

- Modulo: `Rules/` (entità, `CheckCatalog`, `EffectiveRules`, DTO e validatori, endpoint, `ErrorCatalogProvider`), migrazione
  `AddRules`, `TourCopy.Rules` e le regole che seguono la cura del tour (`TourSaving`), `OpenCatalog.Read` sui campi reso pubblico e
  `ParameterField.Default`.
- Frontend: `screens/rules.tsx` (regole generali, errori, la scheda «Regole» del tour), `blocks/` del modulo, `KindPicker`,
  `TabList` e `ShapeFormPage` esportati, `useRowId` spostato in `hooks.ts`.
- Test: unit `RuleTests`, integrazione `RuleTests`, e2e `full/tours-rules.spec.ts`; i conteggi dei blocchi (galleria 34, blocchi
  Data 9).
