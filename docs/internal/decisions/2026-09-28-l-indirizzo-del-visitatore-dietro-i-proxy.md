# L'indirizzo del visitatore dietro più proxy

**Data:** 28 settembre 2026
**Stato:** **Proposta.** Le domande sono al §6, poste a Carmine sulla PR; il codice arriva solo dopo la sua risposta e
dopo la misura del §5.
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

- **Adesso quasi niente.** Il 28 set, 06:50 UTC, `https://test.it.ivao.aero/health` risponde `500` (l'applicazione non
  parte). Si vede solo che davanti c'è Cloudflare (`Server: cloudflare`, `CF-RAY … MXP`); dei salti dietro, niente.
- **Quando parte**, tre prove sull'installazione di prova, senza codice:
  1. **L'indirizzo**: come super amministratore cambiare qualcosa di innocuo e leggere il registro
     (`docs/DEPLOYING.md`, «After every deploy: the checks»). Il tuo indirizzo ✅; `127.0.0.1` vuol dire che `X-Forwarded-For` non arriva;
     un indirizzo di Cloudflare vuol dire la seconda riga del §2.
  2. **Chi falsifica non passa**: undici richieste in un minuto a `/auth/login`, ciascuna con un `X-Forwarded-For` diverso
     inventato. L'undicesima deve rispondere `429`: il limite non si aggira cambiando l'header.
  3. **Lo schema**: `curl -sI https://test.it.ivao.aero/` porta `Strict-Transport-Security` con `max-age=2592000` (i trenta
     giorni dell'hub, non un valore di Cloudflare) e nessun rinvio; `Set-Cookie`, dove c'è, ha `secure`.
- **Che cosa resta cieco anche allora**: la catena esatta degli header (quanti salti, chi aggiunge). Il middleware di ASP.NET
  Core non la scrive nel log, nemmeno a `Debug` (scrive solo il proxy sconosciuto a cui si ferma, e su una catena pulita non
  si ferma su nessuno). Leggerla vuol dire una riga di codice che scrive gli header grezzi a `Debug`: la si aggiunge solo se la
  prova 1 sorprende.

## 6. Le domande a Carmine

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
