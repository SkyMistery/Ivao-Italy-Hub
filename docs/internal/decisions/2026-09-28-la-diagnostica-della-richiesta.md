# La diagnostica della richiesta

**Data:** 28 settembre 2026
**Stato:** **Decisa da Carmine**: la strada (i) del §8 della nota `2026-09-28-l-indirizzo-del-visitatore-dietro-i-proxy`, «sì» in
chat il 28 set 2026, riportato in <https://github.com/SkyMistery/Ivao-Italy-Hub/pull/165#issuecomment-5865694616>. È la PR 1
della coda decisa da Carmine lo stesso giorno (piano 1.21, «L'ordine del codice»): versione **0.2.2**.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: estende la diagnostica che c'è già (`diagnostics/startup.txt`, `/api/version`)
e il meccanismo dei proxy fidati (`2026-09-03-proxy-fidati.md`, design M0 §2.3). Cambio del nucleo, nella sua PR.

## 1. Che cosa serve

Sull'installazione di prova il registro scrive `127.0.0.1` per tutti, mentre lo schema `https` arriva (nota sui proxy, §5 e §8).
Le due ipotesi del §8 (l'`X-Forwarded-For` non arriva; oppure arriva e il middleware lo scarta perché il vicino è
`::ffff:127.0.0.1`) da fuori non si distinguono: serve che l'hub dica come vede la richiesta di chi guarda.

## 2. Che cosa si è fatto

`GET /api/admin/diagnostics/request`, in `src/IvaoHub.Web/Endpoints/RequestDiagnosticsEndpoints.cs`.

- **Un endpoint a mano, di sola lettura, del nucleo** (non un CRUD, nessuna riga dietro): per il conto per famiglia di §16.6 sta
  nella famiglia **amministrazione del sistema, riservata al super amministratore**, accanto a `/api/admin/superadmins`.

- **Solo il super amministratore**, con l'autorizzazione che c'è: `SignedIn` sull'endpoint e `ICurrentUser.IsSuperadmin` nel
  corpo, come l'elenco dei super amministratori (`GrantEndpoints.MapSuperadminEndpoints`). Nessun handler nuovo, nessun
  permesso nuovo: chi ha tutto il catalogo (il direttore) riceve `403`.
- **Solo la richiesta di chi guarda, e niente resta**: la risposta si costruisce dalla richiesta e muore con lei. `no-store` come
  tutto `/api`; nessuna riga di log oltre a quella normale della richiesta.
- **Che cosa mostra**:
  - `neighbour`: il vicino grezzo, con la **famiglia** (`IPv4`, `IPv6`, `IPv4-mapped IPv6`) e la porta;
  - `believed`: l'indirizzo creduto, quello del registro e del limite del login, con la famiglia;
  - `addressForwarded` / `schemeForwarded`: se il middleware ha sostituito l'indirizzo o lo schema (lo si vede dagli
    `X-Original-For` / `X-Original-Proto` che scrive, **solo se non c'erano già all'arrivo**: un chiamante può mandarne uno suo);
  - `schemeReceived` e `scheme`, prima e dopo; `isHttps`; `host`;
  - `headers`: `X-Forwarded-For`, `X-Forwarded-Proto`, `X-Forwarded-Host`, `X-Real-IP` e `Forwarded` **come sono arrivati**,
    anche se assenti (zero voci), con tutte le righe e il numero di voci separate da virgola;
  - `headerNames`: il **nome** di ogni header arrivato, mai il valore. Se l'indirizzo del visitatore arriva sotto un altro nome,
    lo si vede qui senza che il codice lo conosca;
  - `forwarding`: se il middleware è nella pipeline, quali header applica, `ForwardLimit`, le reti e gli indirizzi fidati, i nomi
    degli header da cui legge.
- **Gli header «come sono arrivati» si copiano prima del middleware.** Il middleware dei forwarded header sostituisce indirizzo e
  schema e **toglie da `X-Forwarded-For` le voci che consuma**: letti dopo, gli header non sono più quelli grezzi. Un piccolo
  middleware messo subito prima di `UseForwardedHeaders` copia, **solo per questo indirizzo**, vicino, schema, host, i nomi e i
  valori degli header elencati in una feature della richiesta. Ogni altra richiesta passa senza che si tocchi niente.
- **Nessun nome di un fornitore nel codice** (`CLAUDE.md` §3). Un header in più di cui mostrare il valore si nomina nella
  configurazione, `Diagnostics:RequestHeaders` (un elenco, nel file di `secrets/`). `Cookie`, `Authorization` e
  `Proxy-Authorization` non si mostrano mai, anche se nominati.

## 3. Perché un JSON e non una pagina

- La regola della versione (`Directory.Build.props`): una PATCH **non** porta pagine o sezioni nuove. La coda ha deciso 0.2.2, cioè
  una PATCH; una pagina ne farebbe una MINOR.
- Chi la legge è chi installa, e ne copia i valori in un rapporto **parola per parola**: nomi di header e indirizzi, che nessuna
  lingua traduce. Una pagina aggiungerebbe una rotta, una voce di menu e una dozzina di chiavi i18n per etichette che sono già
  nomi tecnici, e il testo andrebbe poi ricopiato a mano.
- Il browser apre il JSON con il cookie della sessione (una `GET` di primo livello porta il cookie `Lax`; la guardia delle
  richieste da altri siti riguarda solo le scritture). Se servirà una pagina, leggerà questo stesso endpoint.

## 4. Che cosa dicono già i test (misurato in locale, non sul server)

`tests/IvaoHub.IntegrationTests/RequestDiagnosticsTests.cs` mette sotto la richiesta un vicino scelto dal test:
- un vicino fidato con `X-Forwarded-For: 203.0.113.7, 198.51.100.9` e `X-Forwarded-Proto: https`: l'hub crede `198.51.100.9`
  (un salto, il limite 1 di oggi), l'header grezzo ha 2 voci, lo schema passa da `http` a `https`;
- un vicino non fidato: niente sostituito, e un `X-Original-For` inventato dal chiamante non passa per quello del middleware;
- **un vicino `::ffff:10.20.30.40` con `10.20.30.40/32` fra le reti fidate: il middleware di ASP.NET Core 10 lo riconosce e crede
  l'header.** La seconda ipotesi del §8 della nota sui proxy, nella forma «IPv4 dentro IPv6 scartato», con questa versione del
  middleware **non si verifica**. Resta da vedere sul server che cosa arriva (la prima ipotesi, o un'altra).

Una prova di mutazione: con la copia spostata dopo il middleware, due test su cinque diventano rossi.

## 5. Che cosa legge Carmine dopo la consegna

Sull'installazione di prova, dopo l'upload della 0.2.2, nello stesso browser: entrare come super amministratore e aprire
`https://test.it.ivao.aero/api/admin/diagnostics/request`. Si copia la risposta intera. Le righe che decidono:
- `headers` → `X-Forwarded-For`: `entries` 0 vuol dire ipotesi 1 (l'indirizzo non arriva con quel nome); 1 o più, e
  `addressForwarded` falso, vuol dire che arriva ma non è creduto;
- `neighbour.family` e `neighbour.address`;
- `headerNames`: se c'è un altro header che porta l'indirizzo del visitatore.

Se l'indirizzo arriva sotto un altro nome, si può vederne il valore aggiungendo quel nome a `Diagnostics:RequestHeaders` nel file
di `secrets/`, e riavviando.

## Da portare nel piano

- **§11.3 punto 9**, terzo trattino: la pagina diagnostica esiste (`/api/admin/diagnostics/request`, 0.2.2), JSON e non pagina
  (§3 qui sopra); il risultato locale sull'IPv4 dentro IPv6 (§4); che cosa si legge sulla prova (§5).
- **§11.3 punto 5** (o dove il piano elenca la diagnostica: `startup.txt`, `/api/version`): una terza voce, la diagnostica della
  richiesta, solo per il super amministratore.
- **Design M0 §2.3**, paragrafo di `ForwardedHeaders:TrustedNetworks`: la chiave `Diagnostics:RequestHeaders` e l'endpoint.
- **`docs/DEPLOYING.md`**: aggiornato in questa PR (tabella della configurazione, controlli dopo la consegna).
