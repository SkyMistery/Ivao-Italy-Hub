# Il ping che tiene sveglio l'hub

**Data:** 26 ottobre 2026, fase di test.
**Stato:** **decisa**: Carmine l'ha chiesto in chat al master il 26 ottobre 2026, per l'installazione di prova
(`test.it.ivao.aero`), dopo l'analisi della velocità (`docs/internal/test/velocita-del-sito.md`, PR #403). Cambia, per la prova,
la risposta alla domanda 3 della nota `2026-09-28-i-job-quando-passenger-spegne-l-hub` (§6: «il ping? no»), e va contro il
«da non fare» del §4 dell'analisi: lo sceglie Carmine sapendo che costa (§4). Per la produzione vale la nota
`2026-10-18-la-produzione` (§2, punto 5): il Worker di `it.ivao.aero` si decide lì, al sito pubblico; questo si può riusare.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**. Il Worker sta fuori dal pacchetto (`tools/`); nel nucleo si estende la
diagnostica che c'è già (`starts.txt`, `requests-AAAAMMGG.txt`, il log delle richieste) perché un controllo di `/health` non
conti come una visita. Nessuna rotta nuova, nessun meccanismo nuovo.

## 1. Che cosa serve

Passenger ferma l'hub 30 s dopo l'avvio in mediana, quasi mai oltre 60 s, **anche con traffico** (analisi §2.1); chi arriva
mentre nessun processo è acceso aspetta l'avvio intero: **8,0 s in mediana**, 13,5 s al p90 (0.10.5). L'hosting non si cambia e
chiederlo è inutile (Carmine). vIPI, sullo stesso hosting, ha lo stesso problema e lo mitiga con un Worker di Cloudflare che lo
chiama ogni 10 s: la prima richiesta dopo un silenzio è passata da 18,2 s a 0,2 s (vIPI, `docs/lavori-aperti.md` §BG).

## 2. Che cosa si fa

- **Un Worker di Cloudflare** in `tools/cloudflare/keep-warm/` (`index.js`, `wrangler.jsonc`, `README.md`): un cron al minuto, e
  dentro ogni giro un ping subito e uno ogni `INTERVAL_SECONDS` (10) con `ctx.waitUntil`, come vIPI. L'indirizzo è
  `TARGET_URL`, dato al deploy (`--var TARGET_URL:https://<host>/health`): nessun dominio nel codice (`CLAUDE.md` §3), nessun
  segreto. `?t=<adesso>` e `cf: { cacheTtl: 0 }` perché Cloudflare non risponda dalla sua cache. Lo pubblica Carmine con il suo
  account; nessuna sessione si collega a Cloudflare.
- **L'indirizzo è `/health`**, e non una rotta che scaldi di più (§3).
- **Un controllo di `/health` non è una visita** (nucleo, `HubPipeline.IsHealthProbe`):
  - `starts.txt`: la riga `STOP` li conta a parte, `probes N`, scritto solo quando ce ne sono; `requests N` e `first answer at`
    restano dei visitatori. Così `first answer at` molto dopo `ready in` vuol dire «un visitatore ha trovato il processo acceso».
  - `requests-AAAAMMGG.txt`: nessuna riga e nessun numero; la richiesta fredda di un processo resta il primo visitatore.
  - il log: nessuna riga per un `/health` sano (livello `Verbose`); uno che risponde 503 resta una riga d'errore.
  - Né analytics né sessioni: l'hub non ha analytics, e `/health` non legge né scrive un cookie.
- **Il log di Quartz a `Warning`** (`appsettings.json`): ogni avvio scriveva ~30 righe di Quartz (i 22 job aggiunti, il pool, lo
  store); con un avvio al minuto sarebbero ~40 000 righe al giorno che nessuno legge. Gli avvisi e gli errori di Quartz restano.

## 3. Quale indirizzo: la misura

Il pacchetto pubblicato (Release, `win-x64` self-contained con ReadyToRun, come il rilascio salvo il sistema), ambiente `E2E`
del banco con `Jobs:CatchUp=true` (il recupero dei job come in produzione), MariaDB 11.4.10 in Docker, **una CPU**
(`start /affinity` e `DOTNET_PROCESSOR_COUNT=1`). Per ogni avvio: il processo parte, il ping lo sveglia (il client riprova finché
la porta apre, come fa Passenger), 10 s dopo arriva **la prima pagina vera**: la home anonima, cioè le cinque lingue e
`/api/me` insieme, poi `networkStats` e il contenuto `home`, poi `linkList` e `newsList`, come le chiede il browser. Si misura
il tempo di tutta la pagina. Turni alternati (ogni variante una volta per giro, sette giri), mediana.

| Ping, 10 s prima della pagina | pagina p50 | min – max | `/api/me` nella pagina | il ping stesso (avvio compreso) |
|---|---:|---:|---:|---:|
| nessuno: il visitatore sveglia l'hub | **2 763 ms** | 2 169 – 5 649 | 2 534 | — |
| `/health` | **364 ms** | 209 – 1 245 | 108 | 2 441 |
| `/api/version` | 317 ms | 225 – 7 669 | 99 | 2 543 |
| `/api/me` | 256 ms | 163 – 400 | 40 | 2 773 |
| `/health` poi `/api/me` | 259 ms | 186 – 992 | 48 | 2 516 |
| la pagina intera (il processo già caldo) | 44 ms | 38 – 122 | 22 | 2 992 |

- **Il grosso è l'avvio, e lo toglie qualunque ping**: la pagina passa da ~2,8 s (chi sveglia l'hub) a 0,25–0,36 s.
- **Che cosa chiede il ping cambia poco** sulla pagina dopo. `/api/me` toglie ~100 ms in più di `/health` (~0,3 s sul server, stimato), ma
  è una rotta di visitatori: per non contarla come visita l'hub dovrebbe riconoscere il Worker da un header, e ogni ping farebbe
  i sei comandi di `/api/me`. Il resto (~0,3 s qui) è la compilazione delle rotte della pagina, sparsa su tutte: la toglierebbe
  solo un ping della pagina intera, che vuol dire elencare nel Worker gli indirizzi della home di una divisione e scaricare la
  fotografia della rete (`networkStats`) a ogni processo. **Scelto `/health`**: sveglia l'hub come gli altri, è già fuori dalle
  visite per natura, e il Worker non sa niente dell'hub oltre un indirizzo.
- **`/` non serve**: sul server `index.html` lo dà il web server da `wwwroot/` (analisi §2.9), quindi un ping a `/` non arriva
  mai all'hub e non lo sveglia.
- **Il database di `/health`**: un `SELECT 1` su una connessione del pool (`DatabaseHealthCheck`). Sei al minuto sono sei
  comandi da meno di un millisecondo su una connessione che il processo tiene già: il pool di 15 non se ne accorge. In cambio il
  Worker sa se il database risponde (un 503 finisce nel suo log).

Sul server, ~2,7 volte più lento di questa macchina con una CPU (nota `2026-09-28-l-avvio-a-freddo`, §5), la pagina di un processo
già acceso costa stimata ~1 s (364 ms × 2,7), ~0,1–0,2 s a processo caldo, contro gli 8 s di oggi.

## 4. Che cosa costa

- **Gli avvii.** Il processo muore comunque entro il minuto; il ping riaccende il successivo entro 10 s. Con vite di 30–50 s sono
  **60–80 avvii l'ora, ~1 500–1 900 al giorno**, contro i 36–115 al giorno dei giorni di prova. Ogni avvio sul server è ~5,8 s
  (`ready in` p50), quasi tutto CPU: **circa 2–3 ore di CPU al giorno** sullo stesso server dei siti vicini. vIPI lo fa da
  settembre sullo stesso hosting senza che l'hosting abbia detto niente, ma se l'account ha un limite di CPU (analisi §3.1) il
  costo si vede negli avvii stessi: se `ready in` sale sopra la mediana di oggi, o le richieste iniziali rallentano, il Worker si
  spegne (dal pannello di Cloudflare, nessun rilascio).
- **Il recupero dei job a ogni avvio** (`JobCatchUp.FirstCheck`, issue #399): 5 s dopo l'avvio, a ogni avvio, con
  `flightops-flight-checks` sempre dovuto in un processo nuovo. Con il Worker quasi ogni avvio lo provoca un ping e non un
  visitatore, quindi il giro **cade meno di oggi** sulla prima pagina: #399 non diventa più urgente e **non deve venire prima**.
  Costa qualche query e qualche centinaio di ms di CPU ad avvio; resta da fare quando si fa, come dice la issue.
- **I job puntuali, in regalo**: con un processo vivo ogni minuto, i job della notte girano alla loro ora e le mail partono
  entro il minuto, senza aspettare una visita (nota `2026-10-09-i-job-che-recuperano`). Un giro lungo può ancora essere fermato
  con il suo processo, e lo rifà il successivo.
- **`starts.txt` tiene meno storia**: con due righe al minuto, i suoi 2 000 righe sono mezza giornata. La storia lunga resta nei
  file delle richieste (14 giorni, per giorno), che i ping non toccano.
- **Il log**: ~6 righe ad avvio (l'avvio di ASP.NET, il giro dei job) invece di ~36, cioè ~10 000 righe al giorno.

## 5. Da decidere dopo (caso (c), non in questa PR)

**Un processo che si scalda da solo** dopo l'avvio, come il recupero dei job: qualche secondo dopo «pronto», senza visitatori in
attesa, l'hub chiederebbe a sé stesso le rotte anonime della home (non `networkStats`). Toglierebbe la differenza fra `/health` e
la pagina calda (~320 ms qui, ~0,9 s stimati sul server) a ogni visitatore che trova il processo acceso. È un meccanismo nuovo:
una nota sua, se dopo una settimana di Worker `starts.txt` e i riassunti dicono che il resto vale la pena.

## 6. Che cosa non è verificato

- **Sul server**: quanti visitatori trovano ancora un avvio (il visitatore che arriva nei ≤ 10 s fra l'arresto e il ping, o
  mentre il processo nuovo parte), e quanto pesa un avvio al minuto sul server condiviso. Lo dicono `starts.txt` e il
  riassunto delle richieste dopo una settimana (`docs/DEPLOYING.md`, «Keeping the hub awake»).
- **Il Worker non è stato pubblicato**: provato con `wrangler deploy --dry-run` (la configurazione si legge) e in Node con un
  `fetch` finto (sei ping al minuto, il minimo di 5 s, un indirizzo non `https` rifiutato, un errore che non ferma il ping dopo).
- **La riga `STOP` con `probes`** è provata dai test unitari; su un processo vero no: il banco ferma il processo con
  `taskkill /F`, che non lascia la riga `STOP`.
- **La misura del §3** è su Windows con una CPU, non sul pacchetto linux-x64 in un contenitore come la nota dell'avvio a freddo;
  i rapporti fra le varianti contano più dei millisecondi.

## Da portare nel piano

- **§11.3** (il deploy): per l'installazione di prova un Worker di Cloudflare (`tools/cloudflare/keep-warm/`) chiama `/health`
  ogni 10 s; per la produzione si decide con il Worker dei lavori pianificati della nota `2026-10-18-la-produzione`.
- **§11.3** o dove il piano descrive `diagnostics/`: un controllo di `/health` non è una visita (`probes` nella riga `STOP` di
  `starts.txt`, nessuna riga in `requests-AAAAMMGG.txt` né nel log quando è sano); il log di Quartz a `Warning`.
- **§15**, se elenca la velocità della prova: il ping è la mitigazione scelta da Carmine finché l'hosting non tiene un processo
  acceso; i tagli del codice (#397–#401) restano utili per i risvegli che restano.
