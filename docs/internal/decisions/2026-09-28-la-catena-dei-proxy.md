# La catena dei proxy: l'hub risale fino al visitatore

**Data:** 28 settembre 2026
**Stato:** **Decisa da Carmine**, domanda 5 della PR #165: `ForwardLimit = null`, «dopo la prova del registro, qualunque cosa
mostri» (<https://github.com/SkyMistery/Ivao-Italy-Hub/pull/165#issuecomment-5865067623>). La nota che la prepara è
`2026-09-28-l-indirizzo-del-visitatore-dietro-i-proxy.md` (§3 strada a, §6 domanda 1, §8). Carmine ha messo questa
correzione **prima** dell'avvio a freddo, in chat il 28 set 2026: è la `0.2.3`.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: il meccanismo dei proxy fidati (`2026-09-03-proxy-fidati.md`, design M0
§2.3) si estende, non se ne aggiunge un altro. Cambio del nucleo, nella sua PR.

## 1. La misura

Il 28 settembre, sull'installazione di prova con la `0.2.2`, Carmine ha aperto `GET /api/admin/diagnostics/request` da super
amministratore (nota `2026-09-28-la-diagnostica-della-richiesta.md`). Qui l'indirizzo vero del visitatore non si scrive.

- **Il vicino** è `127.0.0.1`, IPv4; l'hub credeva `127.0.0.1`; `addressForwarded` vero; `schemeReceived` `http`, `scheme`
  `https`. La seconda ipotesi della nota di partenza (un vicino IPv6 con dentro un IPv4, scartato) è esclusa.
- **`X-Forwarded-For` arriva in due righe di header**: `"<visitatore>, <nodo di Cloudflare>"` e `"127.0.0.1"`. Tre voci:
  il visitatore; un nodo dentro `172.64.0.0/13`, una delle reti che Cloudflare pubblica; `127.0.0.1`, aggiunto dal server
  web che sta davanti a Passenger.
- **`X-Forwarded-Proto`**: `"https", "https"`, due voci. Niente `X-Real-IP` né `Forwarded`. C'è l'header del visitatore di
  Cloudflare, che il codice non nomina mai (`CLAUDE.md` §3). Passenger aggiunge i suoi `!~Passenger-*`.
- **Le impostazioni**: `XForwardedFor, XForwardedProto`, `forwardLimit` 1, reti fidate = loopback e le reti di Cloudflare
  (dal file dei segreti).

Quindi la prima ipotesi della nota di partenza, «l'header non arriva», era sbagliata: l'header arriva. Con il limite 1 il
middleware consuma **una** voce da destra, la `127.0.0.1` del server web, e si ferma lì anche se quella voce è a sua volta
fidata. Lo schema invece arrivava giusto per caso: l'ultima voce di `X-Forwarded-Proto` è già `https`.

## 2. La correzione

`src/IvaoHub.Web/Program.cs`, dove si configurano le `ForwardedHeadersOptions`:

- **`ForwardLimit = null`**: il middleware risale da destra finché chi ha scritto la voce sta in
  `ForwardedHeaders:TrustedNetworks`, e si ferma al primo indirizzo che non ci sta. Sulla catena misurata: `127.0.0.1`
  (fidato) → il nodo di Cloudflare (fidato) → il visitatore (non fidato): l'hub crede il visitatore.
- **`RequireHeaderSymmetry = false`**, scritto per esteso anche se è il default: i due header portano un numero diverso di
  voci (tre indirizzi, due schemi). Con `true` il middleware scarterebbe tutto e l'hub tornerebbe a `127.0.0.1`: un test lo
  prova (§3).

**Chi chiama l'origine direttamente**, senza Cloudflare (nota di partenza, §7), con un `X-Forwarded-For` inventato: il server
web aggiunge l'indirizzo vero del chiamante e poi il suo `127.0.0.1`. Il middleware risale fino al chiamante, che non è
fidato, e si ferma lì: l'hub registra il chiamante e il limite del login conta lui, qualunque cosa abbia scritto a sinistra,
anche un indirizzo che sembra di Cloudflare. Il residuo resta quello di prima (§3 della nota di partenza): quello che parte
davvero da una rete di Cloudflare è creduto su quello che dichiara.

## 3. Le prove

`tests/IvaoHub.IntegrationTests/ForwardedChainTests.cs`, con indirizzi di documentazione (visitatore `198.51.100.7`, nodo
`172.68.10.20`, vicino `127.0.0.1`) e le reti fidate del file dei segreti di prova (loopback e `172.64.0.0/13`):

- **la catena vera**, nelle sue due righe: la pagina diagnostica mostra due righe e tre voci, due voci di schema, e crede il
  visitatore con `https`;
- **una catena falsificata** da un chiamante diretto (`203.0.113.66, 172.68.10.20, 192.0.2.50` e poi `127.0.0.1`): crede
  `192.0.2.50`;
- **il limite del login**: dieci `GET /auth/login` dello stesso visitatore passano, l'undicesimo è `429`, e un altro
  visitatore dietro la stessa catena passa;
- **il registro**: un link creato e cancellato come nella prova di Carmine, e le due righe di `hub_audit_log` hanno l'indirizzo
  del visitatore.

**Le mutazioni**: con `ForwardLimit = 1` falliscono tutti e quattro, e ogni volta l'hub crede `127.0.0.1`, lo stesso valore
del server; con `RequireHeaderSymmetry = true` falliscono tutti e quattro. Il test della pagina diagnostica della #168 ora
si aspetta `forwardLimit` `null`; il filtro che mette il vicino sotto la richiesta è passato in `TestPeer.cs`, condiviso.

**Da verificare dopo il caricamento** (lo fa Carmine; non si può da qui): la riga `ip=` di
`https://test.it.ivao.aero/cdn-cgi/trace`, poi `believed.address` della pagina diagnostica e la colonna `ip` di una riga
nuova del registro devono essere lo stesso indirizzo. Poi si possono ripetere la prova 2 della nota di partenza e quella
sull'origine del §7: adesso il `429` deve arrivare **per chiamante**, non per tutti.

## Da portare nel piano

- **§11.3 punto 9**, terzo trattino: la misura del §1 e la correzione del §2; il limite «ogni visitatore è `127.0.0.1`»
  sparisce con la `0.2.3`.
- **Design M0 §2.3**, il paragrafo di `ForwardedHeaders:TrustedNetworks`: il middleware risale la catena finché chi parla è
  fidato (`ForwardLimit = null`), e i due header possono avere un numero diverso di voci (`RequireHeaderSymmetry = false`).
- **`docs/DEPLOYING.md`**, fatto in questa PR: il limite noto dell'indirizzo è tolto; il controllo del registro dice che
  l'indirizzo dev'essere il proprio; la riga di `TrustedNetworks` dice come risale il middleware; «Not measured» ha
  l'indirizzo creduto dalla `0.2.3` sul server. Stesso lavoro in `docs/internal/deploy/LEGGIMI-INSTALLAZIONE-DI-PROVA.md`.
