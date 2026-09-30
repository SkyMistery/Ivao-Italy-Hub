# La pagina aperta durante una consegna si ricarica da sola

**Data:** 30 settembre 2026
**Stato:** correzione di un difetto, **nessuna domanda aperta**: non è una «Proposta». Le scelte (ricaricare alla
navigazione e non con un avviso, confrontare il server con se stesso e non con un timbro nel bundle) le ha prese la
sessione di lavoro e sono scritte qui sotto e nella PR. Versione **0.5.1**, PATCH: solo una correzione, nessuna
migrazione, nessuna pagina.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: si estende il bootstrap unico (`/api/me`, letto una volta dalla
radice del router) con un controllo del timbro che già porta; nessun meccanismo nuovo, nessun endpoint, nessun componente.
Cambio del nucleo del front end (`web/src/app/`, `web/src/routes/__root.tsx`, `web/src/main.tsx`), nella sua PR.

## 1. Che cosa si è visto

Installazione di prova, 30 set 2026, dopo la consegna della 0.5.0. Una scheda del browser aperta prima del caricamento ha
continuato a girare con la SPA vecchia. Il menu arriva fresco dal bootstrap del server, quindi mostrava le voci nuove del
Training; il bundle vecchio non aveva né le loro rotte né le loro parole: le voci si leggevano `nav.trainees`, `nav.exams`
(le chiavi nude) e le pagine rispondevano «This page does not exist». Una ricarica forzata ha rimesso tutto a posto. Il
server era giusto (`/api/version` 0.5.0, `index.html` col bundle nuovo, Cloudflare `DYNAMIC`). Succede dopo ogni consegna a
chiunque abbia una scheda aperta.

## 2. Perché

La radice del router legge il bootstrap con `ensureQueryData` e lo passa alle rotte come contesto. La cache di TanStack
Query lo tiene finché qualcuno lo osserva (la home, i blocchi personali, le pagine pubbliche dei moduli), e lo richiede al
server quando torna utile a un osservatore (una finestra che riprende il fuoco, uno schermo che si monta) o quando la
cache lo lascia andare. A quel punto la navigazione successiva legge il bootstrap **del server nuovo** e lo disegna con
**il bundle vecchio**: niente se ne accorgeva.

## 3. La correzione

- **Il timbro del server che ha servito la pagina** è il primo bootstrap che la pagina ha visto (`version` + `commit`, gli
  stessi del piè di pagina). Un bootstrap successivo con un altro timbro vuol dire che il codice sullo schermo non è più
  quello che gira: **alla navigazione successiva** la radice (`beforeLoad`) carica di nuovo la pagina all'indirizzo verso
  cui stava andando, invece di disegnarla (`web/src/app/newBuild.ts`, `createBuildWatch`).
- **Il bootstrap vecchio di più di un minuto si richiede dietro le quinte a ogni navigazione** (`revalidateIfStale` nella
  radice, con lo `staleTime` di 60 s che c'era già): consegnato subito quello in cache, la navigazione dopo legge quello
  nuovo. Senza, una pagina osservata da uno schermo teneva la prima risposta per tutto il tempo in cui restava aperta. Il
  costo è una richiesta leggera di `/api/me`, al più una al minuto, solo quando si naviga.
- **Contro i cicli**: prima di ricaricare la pagina lascia in `sessionStorage` un segno per quel timbro
  (`hub:reloaded-for:<timbro>`) e non ricarica mai due volte per lo stesso. Durante un caricamento possono rispondere a
  turno il processo vecchio e quello nuovo: una pagina che ricaricasse a ogni cambio rimbalzerebbe tra i due; così una
  scheda ricarica al più una volta per ogni release che incontra. Senza `sessionStorage` (finestra privata, dati del sito
  bloccati) non ricarica affatto: una pagina vecchia è un fastidio, una pagina che non smette di caricarsi è un disservizio.
- **Un pezzo del bundle che la consegna ha tolto dal server**: Vite lo segnala con `vite:preloadError` per ogni import
  dinamico, e la pagina si ricarica una volta per quel pezzo (`reloadWhenAChunkIsGone`, con lo stesso segno). L'errore non
  si ferma lì: per il pezzo di uno schermo anche il router ricarica da sé (`lazyRouteComponent` di TanStack Router, una volta
  per messaggio d'errore), e due `reload` sono una ricarica sola.

## 4. Le alternative scartate

- **Un avviso «è disponibile una versione nuova — ricarica»** (`Notice`). Chiede un gesto a chi non sa perché dovrebbe
  farlo, e intanto il menu resta sbagliato. La ricarica alla navigazione non perde niente: navigare lascia già la pagina, e
  un form che protegge le sue modifiche (`useBlocker`, l'editor dei contenuti e dei tour) viene interpellato prima che il
  router arrivi alla radice. Nessuna ricarica avviene mentre qualcuno sta scrivendo senza muoversi. Niente stringhe nuove.
- **Un timbro del client scritto nel bundle da Vite**, confrontato con quello del server. Prende anche il caso in cui la
  pagina era già vecchia alla prima domanda (un `index.html` da una cache), ma sarebbe diverso dal server in sviluppo e in
  ogni suite che finge `/api/me`: una ricarica a ogni clic, o un'eccezione per ogni ambiente. Ed è il server che sa che
  cosa gira (nota `2026-09-27-la-versione-del-sito`). Il caso che resta scoperto è piccolo: il fallback serve
  `index.html` senza `Last-Modified` né `ETag`, cioè senza nulla con cui un browser lo terrebbe in cache.
- **Ricaricare subito**, appena arriva il bootstrap nuovo, senza aspettare la navigazione: perderebbe un form a metà.
- **Chiedere il bootstrap a ogni navigazione e aspettarlo** (`fetchQuery`): un giro di rete in più prima di ogni pagina,
  per una cosa che succede una volta per consegna.

Test: `web/src/app/newBuild.test.ts` (confronto, un solo ricaricamento per timbro, la memoria che rifiuta, il pezzo
mancante; fallivano prima della correzione perché il modulo non esisteva) e lo smoke `web/e2e/new-build.spec.ts` (lo
stesso timbro non ricarica; un timbro nuovo ricarica all'indirizzo giusto e il piè di pagina mostra il nuovo; due release
a turno ricaricano una volta sola; tutti e tre falliscono sulla radice di `main`).

## Da portare nel piano

- **§16.7 (il bootstrap unico)**: la radice del router ricorda il timbro del primo bootstrap della pagina e, quando un
  bootstrap successivo ne porta un altro, ricarica la pagina alla navigazione seguente, una volta per timbro
  (`sessionStorage`); il bootstrap più vecchio di un minuto si richiede dietro le quinte a ogni navigazione (dal 30 set
  2026, 0.5.1).
- **§11.3 (il deploy)**: dopo una consegna le schede aperte si ricaricano da sole alla prima navigazione che vede il server
  nuovo; un pezzo del bundle tolto dal server ricarica la pagina una volta. Non serve chiedere a nessuno di ricaricare.
