# L'indirizzo del visitatore dietro più proxy

**Data:** 28 settembre 2026
**Stato:** **Decisa da Carmine sulla PR, 28 settembre 2026**: le due domande del §6 (5 e 6 del commento), tutte e due come
raccomandato — <https://github.com/SkyMistery/Ivao-Italy-Hub/pull/165#issuecomment-5865067623>. `ForwardLimit = null` si fa
dopo la prova 3 del §5, **qualunque cosa dica**.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: il meccanismo è quello deciso il 3 settembre
(`2026-09-03-proxy-fidati.md`, design M0 §2.3), e si **estende**. Cambio del nucleo, nella sua PR.
**Da dove viene:** piano §11.3 punto 9, terzo trattino; `docs/DEPLOYING.md` «Known limits».

## 1. Che cosa fa l'hub oggi

- `src/IvaoHub.Web/Program.cs:238-256`: se `ForwardedHeaders:TrustedNetworks` non è vuota (in produzione è obbligatoria,
  `src/IvaoHub.Web/HubConfiguration.cs:136-174`), il middleware legge `X-Forwarded-For` e `X-Forwarded-Proto` e crede solo
  a chi sta in quelle reti. **`ForwardLimit` non è impostato: vale 1**, il default di ASP.NET Core. Il middleware entra alla
  riga 266.
- Con il limite 1 il middleware guarda **solo la voce più a destra** di ciascun header: se il vicino (Passenger, cioè
  `127.0.0.1`) è fidato, l'indirizzo diventa l'ultima voce di `X-Forwarded-For` e lo schema l'ultima di
  `X-Forwarded-Proto`. Poi si ferma, anche se quella voce è a sua volta un proxy fidato.
- Su quell'indirizzo poggiano:
  - la colonna `ip` di `hub_audit_log` (`src/IvaoHub.Core/Data/HubSaveChangesInterceptor.cs:745`);
  - il limite di 10 accessi al minuto **per indirizzo** su `/auth/*` (`src/IvaoHub.Web/Program.cs:177-187`,
    `src/IvaoHub.Web/Endpoints/AuthEndpoints.cs:17`).
- Sullo schema poggiano HSTS e la redirezione a https (`Program.cs:279`, `:283`) e il cookie `Secure`.

## 2. Che cosa si rompe, e quando

La catena è visitatore → Cloudflare → nginx di Plesk → (forse Apache) → Passenger → hub. Cloudflare mette in
`X-Forwarded-For` l'indirizzo del visitatore (aggiunto in coda a quel che il visitatore ha mandato). **Che cosa fanno i salti
dopo non lo sappiamo**:

| Se dietro Cloudflare… | `X-Forwarded-For` che arriva all'hub | Con il limite 1 l'hub crede |
|---|---|---|
| nessuno aggiunge niente | `visitatore` | il visitatore ✅ |
| nginx aggiunge chi gli ha parlato (`$proxy_add_x_forwarded_for`, come fa di solito un proxy di Plesk; non verificato su questo server) | `visitatore, <Cloudflare>` | **un indirizzo di Cloudflare** ❌ |
| nginx aggiunge, e Apache con `mod_remoteip` toglie le voci dei proxy di cui si fida | dipende da quali reti conosce Apache | da misurare |

Nel secondo caso, che è il più probabile:

1. **Il registro scrive l'indirizzo del nodo di Cloudflare**, non quello di chi ha scritto: la colonna non serve più a niente.
2. **Il limite del login diventa condiviso**: tutti i visitatori che passano dallo stesso nodo (per l'Italia, quasi tutti da
   Milano) hanno *un* contatore di 10 accessi al minuto. La sera di un evento, gente vera riceve `429`.
3. **Lo schema**: se nginx aggiunge anche a `X-Forwarded-Proto` il suo, e fra Cloudflare e il server si parla in http,
   l'ultima voce è `http`: l'hub rimanda a https una richiesta già https, e il sito gira in tondo.

## 3. Le strade

| | Che cosa | Costo | Limite |
|---|---|---|---|
| **a. `ForwardLimit = null`** | il middleware risale da destra **finché chi parla sta in `TrustedNetworks`**, e si ferma al primo indirizzo che non ci sta | una riga in `Program.cs` e un test con tre salti | è sicuro quanto la lista, che già oggi contiene solo le porte d'ingresso. Un residuo, che c'è già oggi: tutto ciò che parte da un indirizzo di Cloudflare è creduto su quel che dichiara |
| **b. `ForwardLimit` configurabile** | un numero per installazione | una chiave in più, che chi installa deve contare | si rompe in silenzio quando l'hosting cambia un salto. Peggio di (a) |
| **c. Leggere `CF-Connecting-IP`** | l'header di Cloudflare | poco codice | il codice saprebbe di Cloudflare (forkabilità, `CLAUDE.md` §3); e dopo nginx l'hub non vede più se la richiesta è passata davvero da Cloudflare, quindi chi raggiunge il server direttamente lo falsifica. **Scartata** |
| **d. `real_ip` in nginx** | `set_real_ip_from` con le reti di Cloudflare e `real_ip_header CF-Connecting-IP` nelle direttive aggiuntive di Plesk | niente codice | vale finché qualcuno tiene quella configurazione sull'host; è una **seconda serratura**, non la prima |

## 4. La raccomandazione

**Prima la misura del §5, poi (a)**, se il registro mostra un indirizzo di Cloudflare. (a) non peggiora nessun caso rispetto
a oggi: dove la catena è di un salto si comporta come il limite 1, dove è di due o tre trova il visitatore, e lo schema viene
dalla stessa voce dell'indirizzo. Se la misura dà già l'indirizzo giusto, (a) si fa lo stesso, perché la prossima modifica
dell'hosting non ce lo dica il registro sbagliato: è una riga.

## 5. Che cosa si può misurare adesso, e che cosa no

L'installazione di prova risponde dal 28 set mattina (`0.2.1`, `fa089de`; il `500` delle 06:50 UTC era l'utente del
database d'esempio rimasto nel file dei segreti). Tre prove, senza codice:

1. **Lo schema — misurato, 28 set 07:00 UTC ✅.** Le risposte dell'hub (`/api/version`, `/auth/login`) portano
   `strict-transport-security: max-age=2592000`, i trenta giorni dell'hub, che l'hub manda solo a una richiesta che crede https;
   i cookie di `/auth/login` hanno `secure`; il `redirect_uri` verso IVAO è `https://`. `http://` riceve `301` da Cloudflare.
   Nessun giro in tondo: l'ultima voce di `X-Forwarded-Proto` che arriva all'hub è `https`.
2. **Chi falsifica non passa — misurato, 28 set 07:00 UTC ✅.** Dieci `GET /auth/login` in 3 secondi (una da sola, poi nove),
   ognuna con un `X-Forwarded-For` inventato diverso (`198.51.100.1`…`.12`): `302` fino alla decima, poi `429`. L'header del
   visitatore non viene creduto. ⚠️ La prova **non distingue** l'indirizzo del visitatore da quello di un nodo di
   Cloudflare: nei due casi il contatore è uno solo.
3. **L'indirizzo — serve Carmine, da fare.** Nello stesso browser: aprire `https://test.it.ivao.aero/cdn-cgi/trace` e
   annotare la riga `ip=` (l'indirizzo come lo vede Cloudflare); entrare come super amministratore; in `/staff/links` creare
   un link («ip test», `https://example.org`), salvarlo e cancellarlo; in `/staff/admin/audit` leggere la colonna `ip` delle
   due righe. **(a)** uguale a `ip=` ✅ la catena ha un salto solo e il limite 1 basta; **(b)** `127.0.0.1` o `::1`:
   `X-Forwarded-For` non arriva; **(c)** un altro indirizzo pubblico, nelle reti di Cloudflare: la seconda riga del §2.

Da fuori si vede anche che `/` risponde `200` **senza** `strict-transport-security` né `x-robots-tag`: sembra servito da nginx
dalla cartella `wwwroot/`, non dall'hub. Non riguarda questa nota (è il tema delle intestazioni dei file serviti da Plesk).

- **Che cosa resta cieco anche allora**: la catena esatta degli header (quanti salti, chi aggiunge). Il middleware di ASP.NET
  Core non la scrive nel log, nemmeno a `Debug` (scrive solo il proxy sconosciuto a cui si ferma, e su una catena pulita non
  si ferma su nessuno). Leggerla vuol dire una riga di codice che scrive gli header grezzi a `Debug`: la si aggiunge solo se la
  prova 1 sorprende.

## 6. Le domande a Carmine

**Risposta di Carmine, 28 set 2026: sì a tutte e due, come raccomandato**
(<https://github.com/SkyMistery/Ivao-Italy-Hub/pull/165#issuecomment-5865067623>, domande 5 e 6). `ForwardLimit = null` si
fa dopo la prova 3 del §5, qualunque cosa mostri; la domanda sull'origine la pone lui a chi amministra il server.

1. **(a), `ForwardLimit = null`, nel nucleo?** Raccomandato: **sì**, dopo la prova 1, anche se la prova dà l'indirizzo giusto.
2. **Chiedi a chi amministra il server** se l'origine accetta connessioni solo dalle reti di Cloudflare? Raccomandato: **sì**,
   nello stesso messaggio della nota `2026-09-28-i-job-quando-passenger-spegne-l-hub` (§6, domanda 4). Non cambia (a), ma dice
   quanto vale il residuo del §3.

## Da portare nel piano

- **§11.3 punto 9**, terzo trattino: il risultato della misura e la strada decisa.
- **Design M0 §2.3**: il paragrafo di `ForwardedHeaders:TrustedNetworks` dice che il middleware risale la catena finché chi
  parla è fidato.
- **`docs/DEPLOYING.md`** «Known limits» (la riga dei forwarded header) e «Not measured» (che cosa passa Passenger in
  `X-Forwarded-For`): quando la misura c'è.
