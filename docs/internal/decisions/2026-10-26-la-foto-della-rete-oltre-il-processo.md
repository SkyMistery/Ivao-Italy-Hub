# La fotografia della rete oltre il processo (#397)

**Data:** 26 ottobre 2026, fase di test.
**Stato:** **decisa da Carmine in chat al master, 26 ottobre 2026**: «2 + 3a + 1, come consigli tu». Le tre strade del §2, tutte.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: si estendono meccanismi del nucleo che ci sono già — la cache del client IVAO
(strada 1), il descrittore dei blocchi e la regola `no-store` dell'API (strada 2) —; la strada 3 è un valore della striscia.
**Da dove viene:** l'analisi della velocità del sito di prova (`docs/internal/test/velocita-del-sito.md`, §3.4 e §4, C2; PR #403).

## 1. Il problema, in tre numeri

- `networkStats` è il blocco più caro: 323 richieste in otto giorni, p50 429 ms, p95 1,2 s, metà del tempo di tutti i blocchi dati.
- La fotografia della rete (`/v2/tracker/whazzup`, alcuni MB) resta in memoria 60 s (`IvaoWhazzup.Freshness`), ma un processo vive
  30 s in mediana: **183 processi su 400** l'hanno scaricata di nuovo (p50 536 ms, ~9 MB di memoria ogni volta).
- La striscia (`LiveStatusStrip`) richiede ogni 60 s: un processo vive meno, quindi **una scheda pubblica aperta e visibile risveglia
  l'hub ogni minuto** (≈ 8 s di avvio a chi la provoca, ≈ 6 s di CPU al server). Una scheda nascosta non chiede già oggi: React Query
  non ripete la domanda quando la pagina non è visibile (`refetchIntervalInBackground` è spento di default; verificato in
  `query-core` 5.102).

**Fatto prima, senza decisione** (PR #404, «Refs #397»): un solo download alla volta dentro un processo. I blocchi di una pagina
fresca chiedevano insieme, ognuno con il suo client, e scaricavano due volte; ora il secondo aspetta il primo e legge quello che ha
tenuto.

## 2. Le tre strade, come erano state proposte

| | Che cosa | Che cosa guadagna | Che cosa costa |
|---|---|---|---|
| **1** | La fotografia **ridotta** (pochi KB, nessun nome) **fuori dal processo**, con la sua ora, in un file dell'installazione. Un processo nuovo, entro il minuto, la legge invece di scaricarla | ~0,5 s e ~9 MB sulla prima pagina pubblica di un processo nato meno di un minuto dopo un altro che l'aveva scaricata. **Nessun risveglio in meno** | Una forma scritta della fotografia e un lettore che non si fida del file; una cartella scrivibile in più. Non un record nel database: aprirebbe una connessione del pool condiviso (≤ 15) per risparmiare una chiamata a IVAO |
| **2** | La risposta **anonima** di `networkStats` con `Cache-Control: public, s-maxage=60` invece di `no-store`, e una regola di cache di Cloudflare su quel percorso | **Con più lettori**: tutte le schede aperte nello stesso minuto ricevono una risposta di Cloudflare e **l'hub non si sveglia**. **Con una scheda sola non guadagna niente**: la sua domanda arriva quando la copia di un minuto è appena scaduta | Un'eccezione nella regola del nucleo «ogni `/api` è `no-store`», con un test. Un fork senza Cloudflare non perde niente |
| **3a** | La striscia chiede ogni 60 s solo finché chi legge è attivo; dopo **10 minuti** senza un gesto sulla pagina smette, e riprende al primo gesto | **Toglie i risvegli della scheda dimenticata aperta**, il caso che la strada 2 non copre | I numeri restano vecchi finché il lettore non si muove; al primo gesto si aggiornano subito |
| 3b | Ogni **5 minuti** sempre | I risvegli della striscia si dividono per cinque | Numeri vecchi fino a 5 minuti anche per chi guarda. Non scelta |

## 3. Perché anche la 1

La proposta della sessione teneva la 1 in attesa della risposta di H1 e della prova del ping. Il master ha consigliato di farla subito,
e Carmine ha scelto così: su vIPI si è misurato (`vIPI Ivao Italy/docs/lavori-aperti.md`, §BG) che **un ping non tiene acceso il
processo, lo riavvia soltanto** — Passenger lo ferma circa 50 s dopo l'avvio, qualunque sia il traffico. Quindi anche con il ping ogni
processo nuovo scaricherebbe di nuovo la fotografia, e il file risparmia ~0,5 s a ognuno.

## 4. Come è fatta

- **Strada 1** — `IvaoNetworkPictureStore` (`src/IvaoHub.Core/Ivao/`), un singleton con la cartella `cache/` dell'installazione
  (`HubPaths.Cache`, accanto a `diagnostics/`, **mai sotto `wwwroot/`**):
  - il file `cache/network-picture.txt` ha solo quello che `IvaoWhazzup.Read` tiene già: callsign, stazione e frequenza dei
    controllori, i due estremi dei piani di volo, i due totali della rete, l'ora della rete e **l'ora in cui l'installazione l'ha
    presa**. Nessun nome, nessun VID: lo prova un test;
  - righe di testo separate da tabulazioni, lette senza reflection: **la lettura costa meno del download che risparmia**. Misurato
    in sviluppo con una sera più piena di ogni sera misurata (800 controllori, 8 000 voli): 117 KB, letti in **24 ms a freddo** e
    1,3 ms a caldo, contro i 536 ms del download. Un file oltre 4 MB non si legge;
  - si scrive con un nome proprio e si rinomina sul file, così chi legge non vede mai mezzo file;
  - vale per **quello che resta del suo minuto**: un processo nato 20 s dopo la tiene 40 s, non 60. Un file più vecchio, di un'ora
    «futura», di un'altra forma o a metà è «nessun file», e si scarica come prima. Anche un fallimento si tiene per il suo minuto,
    come in memoria;
  - nel client: dentro il cancello di #404, prima la memoria, poi il file, poi il download, che scrive il file.
- **Strada 2** — il descrittore di un blocco ha `SharedFor` (null di default); `networkStats` lo dichiara con il minuto della
  fotografia. L'endpoint `/api/blocks/data/{type}` scrive `public, max-age=0, s-maxage=60` sopra il `no-store` di
  `UseNoStoreForApi` **solo per chi non ha fatto l'accesso**: la risposta anonima è la stessa per tutti; quella di chi ha fatto
  l'accesso resta `no-store` e Cloudflare non la tiene. `max-age=0`: il browser non tiene niente, decide la sua cache delle domande.
  **La regola di cache di Cloudflare è di Carmine, nel pannello**: è scritta, generica e senza dominio, in `docs/DEPLOYING.md`
  («Cloudflare: the anonymous answer of who is on the network»).
- **Strada 3a** — `LiveStatusStrip`: dopo 10 minuti senza un gesto (`pointerdown`, `pointermove`, `keydown`, `scroll`, `wheel`,
  `touchstart`) `refetchInterval` diventa `false`; il primo gesto chiede subito e riprende il minuto.

## Da portare nel piano

- §16 (meccanismi generici), la regola della cache dell'API: `no-store` per tutto `/api`, **tranne** la risposta anonima di un blocco
  dati che dichiara `SharedFor` (oggi solo `networkStats`, un minuto), con il perché.
- §16 o §11.3 (cartelle dell'installazione): `cache/`, che un processo lascia al successivo; si può cancellare quando si vuole.
- Design M1 §6.2 (la striscia e il blocco `networkStats`): la fotografia ridotta anche su disco per il minuto; la striscia smette di
  chiedere dopo 10 minuti senza un gesto e riprende al primo.
- `docs/DEPLOYING.md` ha già la cartella, la riga di `deny` e la regola di Cloudflare.
