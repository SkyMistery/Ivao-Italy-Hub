# Le parole di più moduli: il catalogo delle lingue del server con due moduli (A4a)

**Data:** 26 settembre 2026 — fase A4a di M3, PR del nucleo, prima dello scheletro del modulo (A4)
**Stato:** **Proposta**. La domanda (§5) va a Carmine con un commento sulla PR; il codice di questa PR è la raccomandazione, e
cambia se la risposta è un'altra. Il codice di A4 aspetta la risposta e il merge (`CONTRIBUTING.md`, «Phases in a queue»).
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: il meccanismo c'è — **un solo set di file di lingua**, letto dalla SPA e dal
back end (piano §16 punto 8), con le parole di un modulo accanto al suo codice e copiate in `locales/` da `pnpm i18n:sync` — e non
copre un caso che nessuno aveva ancora: **due moduli**. Si estende il meccanismo, non lo si aggira nel modulo. È una PR del nucleo,
prima del codice del modulo che la usa (`CLAUDE.md` §0 regola 6).

## 1. Che cosa è successo

Lo scheletro del training (A4) era scritto e compilava; al primo test d'integrazione **l'hub non è partito**:

```text
InvalidOperationException: The translation key '_source' is declared twice for the same language;
the second one is in '…/locales/en/training.json'.
```

- **`LocaleCatalog`** (`Core/Localization/LocaleCatalog.cs`) legge tutti i file di una lingua e li **appiattisce in un solo
  dizionario**: i namespace del client sono «un dettaglio di caricamento», e una chiave dichiarata da due file **è rifiutata**
  all'avvio, perché la risposta dipenderebbe dall'ordine dei file. È una regola scritta di proposito, e con un modulo solo è giusta.
- **Con due moduli si rompe per forza**, per due chiavi che il modulo non sceglie:
  - **`_source`**: `web/scripts/sync-module-locales.mjs` la scrive in **ogni** copia di un file di modulo, perché nessuno modifichi
    la copia invece dell'originale. Due moduli, due `_source`.
  - **`nav.section`**: la barra dello staff intitola il gruppo di un modulo con `t('<modulo>:nav.section')`
    (`web/src/app/layouts/staffDestinations.ts`), quindi **ogni** modulo con una sezione nel back office la dichiara.
- Misurato sui file veri: fra `training.json` e `flightops.json` collidono `_source`, `nav.section` e quattro chiavi che il training
  avrebbe potuto chiamare diversamente (`nav.settings`, `settings.title`, `settings.description`, `settings.saved`). Nessuna collide
  con i file del nucleo (`common`, `errors`, `mail`, `seed`).
- **Non si vedeva prima** perché fino a oggi i moduli erano uno. Lo stesso vale per i due test di unità che caricano le lingue del
  repository (`NotificationTemplateTests`, `RatingVocabularyTests`): con il file del training cadono anche loro.

## 2. Che cosa serve

Che le parole di più moduli stiano insieme nel catalogo del server **senza** perdere ciò per cui la regola c'è: nessuna risposta che
dipenda dall'ordine dei file, e nessun modulo che ridefinisca una parola del nucleo. E senza toccare i tour: il loro codice legge
chiavi **senza namespace** (`mail.flightops.allTours`, `mail.{tipo}.subject` per le mail dei tipi di notifica, `CONTRIBUTING.md`:
«in C# a module key is `threads.x`, never `flightops:threads.x`»).

## 3. La proposta (raccomandata)

1. **`_source` non è una parola**: il catalogo la salta. È anche il modo di riconoscere il file di un modulo: la scrive solo
   `pnpm i18n:sync`, e i file del nucleo non ce l'hanno.
2. **Il file di un modulo si legge anche con il suo namespace**, come lo scrive il client: `training:nav.section`,
   `flightops:nav.section`. Il namespace è il nome del file. Una chiave di modulo si può sempre chiedere così.
3. **Senza namespace**, come oggi, una chiave che **un solo** modulo dichiara: i tour non cambiano una riga, e le mail dei tipi di
   notifica (`mail.<modulo>.<tipo>`) restano uniche per costruzione.
4. **Una chiave che due moduli dichiarano non si legge senza namespace**: non c'è una risposta giusta, e non se ne sceglie una per
   ordine; con il namespace si legge sempre. Oggi il server non chiede nessuna di queste chiavi (sono della barra e delle schermate).
5. **Resta rifiutata**, come oggi, una chiave dichiarata due volte **dai file del nucleo**, o da un modulo **e** dal nucleo: un
   modulo non ridefinisce una parola del nucleo.

Tocca un file del nucleo, `LocaleCatalog.cs`, e aggiunge i suoi test (`LocaleCatalogModuleTests`, file nuovo): due moduli con
`_source` e `nav.section` si caricano; la chiave di un modulo solo si legge con e senza namespace; quella di due moduli solo con;
una chiave del nucleo ridefinita da un modulo, e una doppia nei file del nucleo, fermano l'avvio come prima. I test che c'erano non si
toccano.

## 4. Alternative scartate

| Alternativa | Perché no |
|---|---|
| Il training chiama le sue chiavi in un altro modo | `_source` e `nav.section` non sono sue: le scrivono lo script di sincronizzazione e la convenzione della barra dello staff |
| Il catalogo tiene la prima delle due e prosegue | è la risposta che dipende dall'ordine dei file, ciò che la regola impedisce |
| Il server legge le chiavi dei moduli **solo** con il namespace | più pulito, ma cambia le chiavi che il codice dei tour e le mail dei tipi di notifica chiedono oggi: un cambio del modulo dei tour, che non è del collaboratore |
| Lo script non scrive più `_source` | toglie la protezione della copia, e `nav.section` resterebbe |
| Il server non legge i file dei moduli | le mail di un modulo stanno nel suo file (`IModule.NotificationTypes`, `mail.{tipo}`) |

## 5. La domanda per Carmine

**Il catalogo delle lingue del server tiene le parole di più moduli come al §3** — `_source` saltata, le chiavi di un modulo anche con
il namespace, senza namespace solo quelle di un modulo solo, e i doppioni che toccano il nucleo ancora rifiutati? **Raccomandata:
sì**, perché è la sola forma che non cambia né i tour né la regola dell'ordine. Le alternative sono al §4; la più vicina è leggere i
moduli solo con il namespace, che però chiede un giro sul codice dei tour.

## 6. Che cosa si tocca

- **A4a** (questa PR, nucleo): `src/IvaoHub.Core/Localization/LocaleCatalog.cs`; il test nuovo; questa nota; `08`, la fase A4a.
- **A4** (modulo, dopo il merge): il file di lingua del training, con `nav.section` come ogni modulo.

## Da portare nel piano

- **§16 punto 8** (un solo set di file di lingua): il back end legge i file dei moduli anche con il loro namespace, e una chiave che
  due moduli dichiarano si chiede solo con il namespace.
- **`CONTRIBUTING.md`**, «Traps already paid for» (la riga «Server-side i18n keys are flattened»): una chiave di un modulo si legge
  in C# senza namespace solo se nessun altro modulo la dichiara; `<modulo>:<chiave>` va sempre.
