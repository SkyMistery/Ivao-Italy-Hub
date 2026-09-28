# La versione del sito: tre numeri e il commit, come vIPI

**Data:** 27 settembre 2026 — PR del nucleo, dal lato del maintainer (sessione di lavoro), prima del primo tag di prova
`v0.2.0` per `test.it.ivao.aero`.
**Stato:** **Decisa da Carmine in chat, 27 settembre 2026**: l'hub usa la numerazione di vIPI, e il sito mostra «versione ·
commit» come vIPI.
**Regola applicata:** `CLAUDE.md` §2 (tutto ciò che la SPA deve sapere all'avvio viene da `/api/me`) e §5, caso **(b)**: il
timbro della build c'è già (`BuildInfo`, `/api/version`, piano §11.3 punto 1) e il bootstrap porta già `version`; manca il
commit. Si **estende il bootstrap** con un campo, non si aggiunge una seconda richiesta né un valore cotto dentro Vite.

## 1. La regola (da `Directory.Build.props` di vIPI, senza la sua storia)

| | | quando |
|---|---|---|
| PATCH | `x.y.Z` | solo correzioni: nessuna migrazione, nessuna pagina o sezione nuova |
| MINOR | `x.Y.0` | funzionalità nuove, e/o migrazioni **additive** |
| MAJOR | `X.0.0` | il pacchetto **non** si consegna col solo FTP: serve sostituire il database, o il codice nuovo non sa leggere i dati che ci sono in produzione |

- Il maggiore non misura l'importanza: risponde a «basta l'FTP, o serve anche il database?».
- **Il numero sta in `Directory.Build.props`**, non sulla riga di comando: si alza in un commit, e chi lo rivede vede la
  promessa accanto a ciò che la giustifica. La regola è scritta lì, in inglese, sopra `<Version>`.
- **Forma**: tre numeri separati da punti, niente `v`, niente suffissi. La tiene un test (`BuildInfoTests`), che la legge
  dall'assembly compilato e non dal file.
- **Il timbro è «numero · commit»**: `0.2.0 · 51f946b`. Il numero è il nome che diamo noi, il commit è l'unica cosa che dice
  quale codice gira; due build con lo stesso numero possono essere due codici.

## 2. Che cosa si tocca

- **`Directory.Build.props`**: la regola, `<Version>0.2.0</Version>` e `IncludeSourceRevisionInInformationalVersion` scritto
  esplicito (è il default, ma `BuildInfo` ne dipende). `ContinuousIntegrationBuild` resta com'era.
- **Il commit viene dall'SDK**: Source Link lo scrive dopo il «+» della versione informativa leggendo il `.git` del checkout
  (misurato: `0.1.0+3acd2953…` in locale, in un worktree). `BuildInfoTests.TheBuildCarriesTheCommitItWasBuiltFrom` gira anche
  in CI, ed è la prova che un checkout di GitHub Actions lo timbra.
- **`BuildInfo.ShortCommit`**: sette caratteri, `null` se la build non ha un commit (un pacchetto costruito fuori da un
  checkout). `/api/version` e `diagnostics/startup.txt` tengono il commit intero.
- **Il bootstrap** (`BootstrapResponse`): accanto a `Version` c'è `Commit`, il commit corto o `null`. Un test d'integrazione
  controlla che coincidano con `/api/version`.
- **La SPA**: il piè di pagina, che è di tutti e tre i layout (pubblico, membro, staff), dice
  `© 2026 <divisione>. Tutti i diritti riservati. · 0.2.0 · 51f946b` sulla riga in basso, dove c'era già `v0.1.0`. La chiave
  `footer.rights` perde la `v` e il segnaposto diventa `{{build}}`, in tutte e due le lingue; i numeri non si traducono.
  Senza commit si vede il solo numero.
- **`release.yml`**: un job `tag`, prima della suite, rifiuta un tag che non sia esattamente `v` + la `Version` del file,
  con un messaggio che dice che cosa fare. Lo zip continua a chiamarsi col tag (`ivao-division-hub-v0.2.0.zip`).
- **`web/package.json`** perde il suo `"version": "0.1.0"`: il pacchetto è privato, nessuno lo leggeva, e un secondo numero
  sarebbe rimasto indietro al primo aumento.

## 3. Perché 0.2.0

MINOR: da 0.1.0 (M0) sono entrate solo funzionalità nuove e migrazioni additive — M1, M2 e le prime fasi di M3 — e il pacchetto
si consegna ancora col solo FTP. Non è MAJOR perché non c'è un database in produzione da sostituire.

## 4. Alternative scartate

| Alternativa | Perché no |
|---|---|
| La SPA chiede `/api/version` per conto suo | una seconda richiesta a ogni avvio, contro `CLAUDE.md` §2: ciò che serve per disegnarsi viene dal bootstrap |
| Il numero e il commit cotti nella SPA da Vite | la SPA e il server sono un pacchetto solo, ma chi sa quale codice gira è il server; e il numero sarebbe scritto in due posti |
| `release.yml` prende il numero dal tag (`-p:Version=…`) | è la «lettera sulla riga di comando» che vIPI ha abbandonato: un numero che non vive in nessun file e che nessuno vede cambiare in un diff |
| Il timbro come elemento a parte nel piè di pagina | Carmine ha voluto sulla riga in basso due frasi sole, chi è e quale versione (10 set 2026): il numero c'era già, gli si aggiunge il commit |

## 5. Da sapere

- **I tag con un suffisso non pubblicano più**: `v0.1.0-m0` (M0) oggi sarebbe rifiutato. Un traguardo si dice nelle note della
  release, non nel tag.
- **Resta aperto**, non deciso: la metà «contract» di un expand/contract (`CLAUDE.md` §6), che toglie una colonna non più usata,
  non è additiva ma si consegna col solo FTP. **Raccomandazione**: MINOR, perché la domanda del maggiore è «basta l'FTP?» e qui
  basta.
- `README.md` e `docs/FORKING.md` dicono ancora «l'unico tag è `v0.1.0-m0`»: da aggiornare quando esce `v0.2.0`.

## Da portare nel piano

- **§11.3 punto 1** (timbro di versione): la regola dei tre numeri e dove sta (`Directory.Build.props`); il commit dall'SDK
  (Source Link, versione informativa), intero su `/api/version` e in `startup.txt`, corto nel piè di pagina di ogni layout come
  «0.2.0 · 51f946b»; il foglio `LEGGIMI-PACCHETTO-x.y.z.md` prende quel numero.
- **§11.2** (`release.yml`): il tag dev'essere `v` + `Version`, altrimenti la release si ferma prima della suite.
- **§16 punto 7** (bootstrap): porta anche la versione e il commit della build.
- Design M0 §2.4 (`/api/version`, riga 158 di `01-design-m0.md`), se il master lo ritiene: il commit intero lì, quello corto nel
  bootstrap.
- Changelog: la versione del sito, decisa da Carmine il 27 set 2026; il primo tag di prova è `v0.2.0`.
