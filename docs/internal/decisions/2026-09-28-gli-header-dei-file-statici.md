# Gli header dei file che il server web consegna da sé

**Data:** 28 settembre 2026
**Stato:** **decisa da Carmine sulla pull request, 28 settembre 2026**, tutte e tre le risposte come raccomandato
([commento](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/164#issuecomment-5865067394)): §5. Il codice della strada A
viene in una pull request del nucleo dopo questa, dopo la prova del §6.1 sul server **e dopo la nota sull'avvio a
freddo** (§3, strada A, il suo costo misurato).
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: il meccanismo esiste (gli header di `config/security.json`,
`SecurityHeaders.cs`) e non copre un modo di ospitare l'hub che `docs/DEPLOYING.md` stesso raccomanda. Si estende il
meccanismo, non lo si aggira; l'estensione è del nucleo, quindi una pull request sua (`CLAUDE.md` §0, regola 6).

## 1. Che cosa si è misurato

Su `https://test.it.ivao.aero` (Plesk, Passenger su Apache, nginx e Cloudflare davanti; cartella dell'applicazione
`webapp/`, document root `webapp/wwwroot/`, come chiede `DEPLOYING.md` «The web server in front»), con `curl -I` da
fuori, **28 settembre 2026 alle 06:47 UTC**, con l'applicazione che **non parte** (Passenger: «Web application could not
be started»):

| Indirizzo | Risposta | Chi risponde |
|---|---|---|
| `/`, `/index.html` | 200, `text/html`, `last-modified` del caricamento | il server web, da `wwwroot/` |
| `/assets/index-….js`, `/assets/index-….css`, `/locales/it/common.json`, `/branding/favicon.svg` | 200, con `etag` | il server web |
| `/robots.txt` | 200, `Disallow: /` | il server web: è il file messo **a mano** il 27 sera (§4) |
| `/health`, `/api/version`, `/sitemap.xml`, `/staff`, un indirizzo inventato, `/assets/non-esiste.js`, `/branding/` | 500, la pagina di errore di Passenger | Passenger |
| `/secrets/x.json`, `/config/division.json` | 500 di Passenger: non esistono in `wwwroot/`, quindi il file non è raggiungibile | Passenger |

Su **tutte** le risposte, anche sulla pagina di errore di Passenger, c'è `X-Content-Type-Options: nosniff`: lo aggiunge
l'hosting (nginx di Plesk o Cloudflare), non l'hub. **Nessuna** delle risposte del server web porta
`Content-Security-Policy`, `X-Frame-Options`, `Referrer-Policy`, `Cross-Origin-Opener-Policy` né `X-Robots-Tag`.

Che cosa se ne ricava:

- **La CSP vale per documento.** Chi arriva su `/` riceve `index.html` dal server web, senza policy; da lì la SPA
  naviga senza ricaricare, quindi **tutta la visita** gira senza CSP e senza `frame-ancestors 'none'` né
  `X-Frame-Options: DENY`: la home si può incorniciare in un'altra pagina. Chi arriva su un indirizzo profondo
  (`/staff/…`) passa invece da Passenger e, quando l'applicazione girerà, avrà la policy. Lo stesso sito si comporta in
  due modi a seconda della porta da cui si entra, e la suite smoke, che gira sotto la policy vera (`CLAUDE.md` §2), non
  vede la differenza, perché lì la pagina la serve Vite.
- **Un'installazione privata** promette `X-Robots-Tag: noindex, nofollow` su ogni risposta
  (`2026-09-27-l-installazione-di-prova`): non arriva sulla home, che è proprio la pagina che un motore trova.
- **Per i file che non sono documenti** (JS, CSS, JSON delle lingue, immagini) CSP, `X-Frame-Options`, COOP e
  `Referrer-Policy` non servono: il browser applica quelle del documento che li chiede. `nosniff`, l'unico che conta per
  loro, l'hosting lo manda già.
- `/branding/`, una cartella di `wwwroot/` **senza** `index.html`, va a Passenger e non al server web. È l'indizio che
  una cartella senza indice arriva all'applicazione (§3, strada A); per `/` stesso va provato (§6.1).

## 2. Perché il meccanismo di oggi non basta

`UseSecurityHeaders` mette gli header su tutto ciò che **attraversa l'applicazione**, file statici compresi — ed era
vero quando `DEPLOYING.md` fu scritto con il pacchetto provato nei contenitori, dove ASP.NET serviva tutto. Con il document
root su `wwwroot/`, la scelta più sicura per i segreti, il server web consegna da sé ogni file che trova lì, e i file
più importanti per gli header, `index.html`, non passano mai dall'hub.

## 3. Le strade

**A. `index.html` fuori dal document root, lo serve solo l'applicazione.** *(raccomandata)*
La pubblicazione copia `web/dist/` in `wwwroot/` **tranne** `index.html`, che va in una cartella del pacchetto fuori da
`wwwroot/` (per esempio `spa/index.html`); il fallback della SPA (`HubPipeline.MapSpaFallback`) lo legge da lì, e
`UseDefaultFiles` non serve più. Il server web non trova più un indice in `wwwroot/`, quindi `/` e `/index.html` vanno a
Passenger come oggi `/branding/`; `assets/`, `locales/` e `branding/` restano statici, veloci e in cache.
- Gli header restano **in un posto solo**, il file e il middleware che già li provano (`SecurityHeadersTests`): niente
  da copiare, niente che si allontana da `security.json`, e l'interruttore `enabled` continua a spegnere tutto senza
  ricompilare. Vale per qualsiasi hosting con il document root su `wwwroot/`, quindi anche per chi fa un fork.
- Costa: `index.html` (meno di 1 kB) passa da Passenger a ogni primo caricamento. Quando Passenger ha spento
  l'applicazione inattiva, la home aspetta l'avvio; oggi lo aspetta comunque la prima chiamata a `/api/me`, ma con la SPA
  già caricata invece che con la pagina bianca del browser. Se l'applicazione non parte, `/` mostra l'errore di Passenger
  invece di una SPA che non carica: più onesto.
- ⚠️ **Quanto costa davvero, misurato dopo la decisione** (sessione della #165, 28 settembre 2026, 7 prove su 7 dopo
  almeno 60 s di silenzio): su `test.it.ivao.aero` Passenger spegne l'hub dopo **10–30 s** di inattività, e un avvio a
  freddo costa **8–10 s**. Il tempo di inattività non lo decide la divisione. Con A, quindi, chi apre la home di un sito
  poco visitato vede **una pagina bianca per 8–10 s**. **Carmine** sulla pull request, 28 settembre 2026
  ([commento](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/164#issuecomment-5865536250)): A **resta decisa**, ma **il
  codice di A aspetta la nota sull'avvio a freddo** (un'altra sessione di lavoro misura dove vanno i secondi e che cosa
  li taglia): la pull request del nucleo parte dopo che quella nota è decisa.
- Tocca il nucleo: `IvaoHub.Web.csproj` (la copia della build), `HubPipeline.cs`, `Program.cs`, `HubPaths` (dove sta
  l'indice), un test che il pacchetto pubblicato non abbia `wwwroot/index.html`, e in `DEPLOYING.md` e nel foglio
  italiano il perché, `spa` nella riga dei `deny` (serve solo se il document root è la cartella dell'applicazione) e il
  controllo `curl -sI /` con la CSP. Da verificare nella fase: come la suite e2e completa monta la SPA.
- Resta fuori: i file statici di un'installazione privata senza `X-Robots-Tag` (domanda 3; accettato, §7).

**B. Document root su una cartella vuota: tutto passa dall'applicazione.** Nessuna riga di codice, un'impostazione del
pannello; l'hub serve già `wwwroot/` da sé e nient'altro (misurato il 27 settembre). Ma ogni JS, CSS e JSON passa da
nginx → Apache → Passenger → Kestrel; a freddo, dopo lo spegnimento per inattività, anche gli asset aspettano l'avvio
(Cloudflare poi li tiene in cache, e sono i nomi con l'impronta). Va bene come **ripiego** su un'installazione; come regola
per tutti sprecherebbe il server web.

**C. Direttive nel server web** (nginx o Apache aggiuntive di Plesk) che aggiungono la CSP di `security.json` e, per una
prova, `X-Robots-Tag`. Scartata come strada principale:
- un `add_header` di nginx a livello di server vale anche per le risposte dell'applicazione: due `Content-Security-Policy`
  si applicano **entrambe**, e sulla cornice del blocco interattivo (`/embed/…`, che si dà `frame-ancestors 'self'`,
  `EmbedEndpoints.cs`) l'intersezione con `'none'` **rompe il blocco**. Limitarla alle `location` statiche vuol dire
  scrivere dentro la configurazione che Plesk genera, e in nginx un `add_header` in una `location` cancella quelli
  ereditati: da fuori non si può verificare. Con Apache `Header setifempty` rispetterebbe la policy dell'app, ma solo se
  i file statici li serve Apache e non nginx;
- è **una seconda copia** della policy, incollata a mano in ogni installazione: si allontana da `security.json` alla
  prima modifica, `enabled` non la spegne più, la prova della divisione fittizia «XX» e la CI non la vedono. Si può
  attenuare (`tools/prepare-delivery.ps1` scrive le righe da `security.json` al tag, e il controllo dopo il deploy
  confronta la CSP di `/` con quella di `/staff`), ma resta un passo manuale per ogni rilascio.
- Resta utile per una cosa sola, **costante**: `X-Robots-Tag` sui file statici di un'installazione privata (domanda 3).

**D. La CSP in un `<meta http-equiv>` di `index.html`**, generata da `security.json` alla build. Scartata: in un `meta`
il browser **ignora `frame-ancestors`** (e `report-to`, `sandbox`), e `X-Frame-Options` non esiste in `meta`, quindi la
home resta incorniciabile; `enabled` non si spegne più senza ricompilare; e `X-Robots-Tag` non si può mettere nel
pacchetto, perché privata è l'installazione e non la build: un `noindex` nel pacchetto toglierebbe dai motori la
produzione.

**E. Regole di Cloudflare** (*Transform Rules* sugli header di risposta). Stessi limiti di C per la CSP, e in più fuori
dal repository, nell'account di chi amministra `it.ivao.aero`. Per `X-Robots-Tag` su `test.it.ivao.aero` invece è la più
semplice: una regola sul nome host copre tutto, statici compresi, senza toccare il server (domanda 3).

## 4. `robots.txt`, a parte

Prima del file a mano, `/robots.txt` rispondeva un **404 di Apache** (misura di Carmine del 27 settembre), mentre gli
altri indirizzi assenti arrivavano a Passenger. Oggi non si può rimisurare senza togliere il file. Se l'hosting tiene per
sé `/robots.txt`, nessuna delle strade lo porta all'applicazione, e in **produzione** l'hub risponderebbe 404: i motori
lo leggono come «tutto permesso» (va bene) ma non trovano la riga `Sitemap:` (si rimedia dichiarando la sitemap nella
Search Console, o con una regola del pannello). Il file a mano in `webapp/wwwroot/` **non deve mai entrare nel pacchetto**:
direbbe `Disallow: /` anche alla produzione.

## 5. Le domande, e che cosa ha deciso Carmine

Carmine ha risposto sulla pull request il 28 settembre 2026, **tutte e tre come raccomandato**
([commento](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/164#issuecomment-5865067394)).

1. **Quale strada?** Raccomandazione: **A**, in una pull request del nucleo dopo questa, con la versione `0.2.2` (nessuna
   migrazione). B come ripiego solo se serve la CSP sulla prova prima di A.
   **Deciso: A**, `0.2.2`, nessuna migrazione — **dopo** la prova del §6.1 sul server di prova. Poi, saputo il costo
   dell'avvio a freddo (§3): il codice aspetta anche la nota sull'avvio a freddo
   ([commento](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/164#issuecomment-5865536250)).
2. **Sulla prova, intanto?** Raccomandazione: **niente di più** del `robots.txt` a mano. È privata all'accesso e senza dati
   veri: il buco della CSP lì conta poco per qualche giorno. Il file a mano si toglie solo quando la release con A è sul
   server **e** una misura mostra che `/robots.txt` arriva all'applicazione.
   **Deciso così.**
3. **I file statici di un'installazione privata, senza `X-Robots-Tag`?** Sono JS, CSS, JSON e immagini, non pagine; un
   motore obbediente li salta per il `Disallow: /`. Raccomandazione: **accettarlo e scriverlo** tra i limiti; se un giorno
   va chiuso, **una regola di Cloudflare** sul nome host (E), chiesta a chi amministra, non una direttiva nel pacchetto.
   **Deciso così**: il limite è scritto qui sotto (§7), e la pull request di A lo porta in `DEPLOYING.md` e nel foglio
   italiano.

## 6. Con l'applicazione accesa

L'applicazione gira su `test.it.ivao.aero` dalla mattina del 28 settembre (`0.2.1`, commit `fa089de`). **Rimisurato da
fuori alle 07:05 UTC circa**:

| Indirizzo | Risposta | Chi risponde |
|---|---|---|
| `/`, `/index.html` | 200, **nessun** header dell'hub: solo il `nosniff` dell'hosting | il server web, come prima |
| `/staff` (GET e HEAD) | 200, **lo stesso `index.html`** di `/` (stessa impronta SHA-256), con CSP identica a `security.json`, `X-Frame-Options: DENY`, `Referrer-Policy`, COOP, HSTS (`max-age=2592000`), `X-Robots-Tag: noindex, nofollow`, `X-Correlation-Id` | l'hub |
| `/health` | 200 `Healthy`, gli stessi header, `Cache-Control: no-store, no-cache` | l'hub |
| `/api/version` (GET) | 200, `0.2.1`, `fa089de…`, `.NET 10.0.12`, `Cache-Control: no-store` | l'hub |
| `/sitemap.xml` | 404 con gli header: un'installazione privata non ha sitemap | l'hub |
| `/secrets/x.json`, `/config/division.json`, `/appsettings.json`, `/IvaoHub.Web.dll`, `/diagnostics/startup.txt` | 404 dell'applicazione, con gli header: prima era il 500 di Passenger | l'hub |
| `/hub-keys/`, `/branding/` | 200, la pagina del sito (il fallback della SPA), con gli header | l'hub |
| `/assets/index-….js` | 200, solo `nosniff`, `cf-cache-status: HIT` | il server web, poi Cloudflare |
| `/robots.txt` | 200, il file a mano, `cf-cache-status: HIT` (Cloudflare lo tiene in cache) | il server web, poi Cloudflare |

Che cosa conferma:

- **Il buco è quello del §1, e solo quello**: la stessa pagina arriva con la policy da `/staff` e senza da `/`. Gli header
  dell'hub attraversano Passenger e Cloudflare intatti, e `nosniff` arriva **una volta sola**.
- La **cornice del blocco interattivo** qui non si vede: `/embed/guidelines` risponde 401 a chi non è entrato, con la
  policy della pagina; la sua policy propria si misura da dentro, con una pagina pubblicata che ha il blocco.
- ⚠️ `curl -I` (HEAD) su un indirizzo `GET` dell'API risponde **404**: gli endpoint `MapGet` non accettano HEAD, e il
  fallback rifiuta `/api`. È un limite di misura, non un guasto: `/api/version` si controlla con `curl -s` (GET), come
  dice già `DEPLOYING.md`. Per lo stesso motivo HEAD su `/embed/…` risponde con la pagina del sito.

Resta da misurare **con l'FTP**, quindi da Carmine o da chi amministra (§6.1 e §6.2).

### 6.1. La prova di A: `/` senza `index.html` arriva all'hub?

È la condizione di Carmine prima del codice. Dura un minuto; in quel minuto la home risponde 404 (gli indirizzi profondi
funzionano).

1. Con FileZilla, in `webapp/wwwroot/`, **rinominare** (non cancellare) `index.html` in `index.html.off`.
2. Dal terminale:

   ```bash
   curl -sI https://test.it.ivao.aero/
   ```

3. **Riuscita** se risponde `HTTP/1.1 404 Not Found` **con** le righe `content-security-policy:`, `x-robots-tag:` e
   `x-correlation-id:`: è l'hub che risponde (404 perché oggi il fallback cerca l'indice proprio lì, e non lo trova).
   **Fallita** se risponde 403, un elenco dei file della cartella, o un 404 **senza** `x-correlation-id`: allora il server
   web tiene per sé `/` e la strada A va ripensata prima di scrivere codice.
4. Subito dopo, in FileZilla, **rinominare** `index.html.off` di nuovo in `index.html`, e controllare che la home torni:

   ```bash
   curl -sI https://test.it.ivao.aero/
   ```

   deve dire di nuovo `HTTP/1.1 200 OK`.

### 6.2. `/robots.txt` arriva all'hub senza il file a mano?

Facoltativa ora, necessaria prima di togliere il file per sempre (decisione 2). Stessa durata.

1. In `webapp/wwwroot/`, rinominare `robots.txt` in `robots.txt.off`.
2. Dal terminale, con un parametro inventato perché Cloudflare ha la versione vecchia in cache:

   ```bash
   curl -sD - "https://test.it.ivao.aero/robots.txt?prova=1"
   ```

3. **L'hub risponde** se il testo è `User-agent: *` e `Disallow: /` **e** tra le righe c'è `x-correlation-id:`. Se invece
   torna il 404 di Apache del 27 settembre, l'hosting tiene per sé `/robots.txt` (§4) e il file a mano resta.
4. Rinominare `robots.txt.off` di nuovo in `robots.txt`.

## 7. Limiti, accettati

- **I file statici di un'installazione privata** (`assets/`, `locales/`, `branding/`) non portano `X-Robots-Tag`, né
  con A né oggi: non sono pagine, e il `Disallow: /` di `robots.txt` li copre per i motori che lo leggono. Se un giorno
  va chiuso, una regola di Cloudflare sul nome host, chiesta a chi amministra; mai una direttiva nel pacchetto (decisione
  3).
- **I file statici non portano gli header dell'hub** nemmeno con A: per un file che non è un documento contano solo quelli
  del documento che lo chiede, e `nosniff` lo aggiunge l'hosting (misurato).

## Da portare nel piano

- **§11.3** (produzione): il document root su `wwwroot/` non basta per gli header, e `index.html` sta fuori da `wwwroot/`
  perché li serva l'applicazione (strada A, decisa); il controllo `curl -sI /` con la CSP tra quelli dopo ogni deploy.
  Tra i limiti dell'installazione privata: i file statici senza `X-Robots-Tag` (§7), e la regola di Cloudflare se va chiuso.
- **§16** (meccanismi) e la nota `2026-09-12-gli-header-di-sicurezza`: gli header valgono su ciò che attraversa l'hub;
  i documenti ci passano sempre, i file statici no, ed è voluto.
- **§15 punto 2c** (hosting): `/robots.txt` su Plesk, se la misura del §6.2 conferma che l'hosting lo tiene per sé.
