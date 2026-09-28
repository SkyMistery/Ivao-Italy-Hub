# Gli header dei file che il server web consegna da sé

**Data:** 28 settembre 2026
**Stato:** **Proposta** — le tre domande del §5 vanno a Carmine sulla pull request; il codice viene dopo la sua risposta.
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
  una cartella senza indice arriva all'applicazione (§3, strada A); per `/` stesso va provato (§6).

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
  l'applicazione inattiva, la home aspetta l'avvio; oggi lo aspetta comunque la prima chiamata a `/api/me`, con una pagina
  bianca invece che ferma. Se l'applicazione non parte, `/` mostra l'errore di Passenger invece di una SPA che non carica:
  più onesto.
- Tocca il nucleo: `IvaoHub.Web.csproj` (la copia della build), `HubPipeline.cs`, `Program.cs`, `HubPaths` (dove sta
  l'indice), un test che il pacchetto pubblicato non abbia `wwwroot/index.html`, e in `DEPLOYING.md` e nel foglio
  italiano il perché, `spa` nella riga dei `deny` (serve solo se il document root è la cartella dell'applicazione) e il
  controllo `curl -sI /` con la CSP. Da verificare nella fase: come la suite e2e completa monta la SPA.
- Resta fuori: i file statici di un'installazione privata senza `X-Robots-Tag` (domanda 3).

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

## 5. Le domande per Carmine

1. **Quale strada?** Raccomandazione: **A**, in una pull request del nucleo dopo questa, con la versione `0.2.2` (nessuna
   migrazione). B come ripiego solo se serve la CSP sulla prova prima di A.
2. **Sulla prova, intanto?** Raccomandazione: **niente di più** del `robots.txt` a mano. È privata all'accesso e senza dati
   veri, e l'applicazione non parte ancora: il buco della CSP lì conta poco per qualche giorno. Il file a mano si toglie
   solo quando la release con A è sul server **e** una misura mostra che `/robots.txt` arriva all'applicazione.
3. **I file statici di un'installazione privata, senza `X-Robots-Tag`?** Sono JS, CSS, JSON e immagini, non pagine; un
   motore obbediente li salta per il `Disallow: /`. Raccomandazione: **accettarlo e scriverlo** tra i limiti della nota
   dell'installazione di prova; se Carmine vuole chiuderlo, **una regola di Cloudflare** sul nome host (E), chiesta a chi
   amministra, non una direttiva nel pacchetto.

## 6. Che cosa aspetta che l'applicazione parta

Da rimisurare, e da scrivere nella nota della fase A:

- `/health` e `/api/version`: la versione e il commit, e `Cache-Control: no-store`;
- la CSP e `X-Robots-Tag` su un indirizzo profondo (`curl -sI /staff`): che l'applicazione li mandi davvero dietro
  Passenger, e che `nosniff` non arrivi doppio in modo rotto (l'hosting lo aggiunge già);
- **la prova di A prima del codice**: rinominare per un minuto `wwwroot/index.html` sul server, `curl -sI /`, e
  aspettarsi dall'applicazione un 404 (il fallback non trova l'indice) **con** la CSP e `X-Correlation-Id`: vuol dire
  che `/` senza indice arriva all'hub. Poi si rimette il file. Serve chi ha l'FTP;
- `/robots.txt` senza il file a mano (rinominarlo per un minuto): se risponde l'hub o ancora Apache (§4);
- `/secrets/…` e `/config/division.json`: 404 dell'applicazione, non più il 500 di Passenger.

## Da portare nel piano

- **§11.3** (produzione): il document root su `wwwroot/` non basta per gli header, e `index.html` sta fuori da `wwwroot/`
  perché li serva l'applicazione (se passa A); il controllo `curl -sI /` con la CSP tra quelli dopo ogni deploy.
- **§16** (meccanismi) e la nota `2026-09-12-gli-header-di-sicurezza`: gli header valgono su ciò che attraversa l'hub;
  i documenti ci passano sempre, i file statici no, ed è voluto.
- **§15 punto 2c** (hosting): `/robots.txt` su Plesk, se la misura del §6 conferma che l'hosting lo tiene per sé.
