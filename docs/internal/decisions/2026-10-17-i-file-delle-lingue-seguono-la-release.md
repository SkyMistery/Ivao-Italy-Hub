# I file delle lingue seguono la release

**Data:** 17 ottobre 2026
**Stato:** correzione di un difetto, **nessuna domanda aperta**: non è una «Proposta». Le scelte (un'impronta del
contenuto nell'indirizzo, nessun header dal server) le ha prese la sessione di lavoro e sono scritte qui sotto e nella PR.
Versione **0.8.1**, PATCH: solo una correzione, nessuna migrazione, nessuna pagina.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: si estende il meccanismo che già c'è (un solo insieme di file
`locales/{lng}/*.json`, copiato da `vite.config.ts` nel pacchetto e chiesto da `app/i18n.ts`), senza un endpoint, un
header o un componente nuovo. Cambio del nucleo del front end (`web/src/app/i18n.ts`, `web/vite.config.ts`), nella sua PR.

## 1. Che cosa si è visto

Installazione di prova, 17 ottobre 2026, dopo la consegna della 0.8.0. La pagina nuova `/staff/admin/connections`
mostrava le chiavi nude (`connections.database.title`, `connections.outcomes.Ok`…). Il server era giusto: `curl` su
`/locales/it/common.json` dava il file nuovo, con «connections», il `last-modified` della consegna,
`cf-cache-status: DYNAMIC` e **nessun `Cache-Control`**. Il browser teneva il file vecchio. Una ricarica forzata lo
rimette a posto per una persona; dopo ogni consegna capita a tutti.

## 2. Perché

- I pezzi JS cambiano nome a ogni build (l'impronta di Vite); i file delle lingue **no**: la SPA li chiedeva sempre a
  `/locales/{{lng}}/{{ns}}.json`.
- Su questo hosting `/locales/` lo consegna **il server web da sé**, da `wwwroot/`, non l'hub (misurato il 28 settembre,
  nota `2026-09-28-gli-header-dei-file-statici`, §1 e §6): con `Last-Modified` ed `ETag`, senza `Cache-Control`. Senza
  `Cache-Control` il browser applica la cache **euristica** (di solito un decimo dell'età del file): un file vecchio di
  dieci giorni resta buono un giorno senza chiedere niente al server.
- La 0.5.1 ricarica una scheda aperta durante una consegna (nota `2026-09-30-la-pagina-dopo-una-consegna`), ma la
  ricarica chiede di nuovo lo stesso indirizzo, e il browser risponde dalla sua cache.

## 3. La correzione

- **Un'impronta dei file delle lingue nell'indirizzo**: `vite.config.ts` calcola, alla build, lo SHA-256 di tutti i file
  sotto `locales/` (percorso e contenuto, in ordine; i primi 12 caratteri, `web/localesStamp.ts`) e lo scrive nel
  bundle come `__HUB_LOCALES_STAMP__`; i18next chiede ogni file con `?v=<impronta>` (`queryStringParams` di
  `i18next-http-backend`, `LOCALES_STAMP` in `app/i18n.ts`).
- Una release che cambia una parola cambia l'indirizzo, quindi il browser non ha niente in cache per esso; una release che
  non tocca le lingue lascia l'impronta com'è e la cache vale ancora. Il server web, Vite preview e `UseStaticFiles`
  ignorano la query e consegnano il file: nessuna riga sul server.
- I file dei moduli ci sono già: `pnpm i18n:sync` li mette sotto `locales/` prima della build, e l'impronta li legge da lì.
- Il pezzo JS che porta l'impronta cambia quando cambia l'impronta, quindi la consegna parziale per hash lo porta con sé.
- **Il back end** legge `locales/` dal disco per le mail e i rifiuti (`LocaleCatalog`): non passa da un indirizzo, e non
  cambia niente.

Test: `web/src/app/localesStamp.test.ts` (stessi file stessa impronta; una parola, un file aggiunto, un file spostato la
cambiano), `web/src/app/i18n.test.ts` (ogni richiesta finisce con `?v=` e l'impronta dei file del repository; senza
`queryStringParams` fallisce, provato) e lo smoke `web/e2e/locales-stamp.spec.ts` (il bundle costruito chiede ogni file con
la stessa impronta e il server statico risponde 200).

## 4. Le alternative scartate

- **`Cache-Control: no-cache` su `/locales/` dal back end e da Vite preview** (`config/security.json`). Su questo
  hosting l'hub quei file non li vede mai: li consegna il server web, e un header dell'hub non arriva (è la stessa ragione
  per cui non portano la CSP, nota del 28 settembre §7). Varrebbe solo dove l'hub serve tutto (document root vuoto,
  contenitori), e lì l'indirizzo nuovo basta già. Una direttiva nel server web o una regola di Cloudflare sarebbe una
  seconda copia per ogni installazione, fuori dal repository.
- **La versione o il commit del server nell'indirizzo**, dal bootstrap `/api/me`. Le lingue partono prima del bootstrap:
  aspettarlo ritarderebbe la prima parola di ogni pagina di un giro di rete. E cambierebbe a ogni release, anche quando le
  parole sono le stesse.
- **La versione di `Directory.Build.props` scritta nel bundle.** Vite non la legge, e due build con lo stesso numero
  possono avere parole diverse (nota `2026-09-27-la-versione-del-sito`); l'impronta del contenuto è proprio la cosa che
  deve cambiare.
- **Un'impronta per file** (una mappa `lingua/namespace → impronta`). Riscaricherebbe solo i file cambiati, ma sono pochi
  kB, una volta per consegna: non vale il codice in più.

## 5. Che cosa resta fuori

- `index.html` sul server di prova lo consegna ancora il server web, con `Last-Modified` (la strada A della nota del 28
  settembre non è ancora nel codice): in teoria anche lui può restare nella cache euristica di un browser. Non lo si è visto
  succedere; se succede, la cura è la strada A, già decisa, non un'altra cosa.
- Una scheda già aperta con il bundle vecchio, prima della ricarica della 0.5.1, chiede ancora l'indirizzo vecchio: è il
  bundle vecchio, e la ricarica alla navigazione lo sostituisce.

## Da portare nel piano

- **§16 (meccanismi), il file delle lingue unico** (design M0 §7.6): la SPA chiede ogni `/locales/{lng}/{ns}.json` con
  `?v=` e l'impronta del contenuto di `locales/`, calcolata alla build (`web/localesStamp.ts`); una release che cambia
  una parola cambia l'indirizzo, chiunque consegni il file (dal 17 ottobre 2026, 0.8.1).
- **§11.3 (il deploy)**: dopo una consegna le parole nuove arrivano senza ricarica forzata; nessuna regola di cache da
  mettere nel server web o in Cloudflare per `/locales/`.
