# La fotografia della rete oltre il processo (#397)

**Data:** 26 ottobre 2026, fase di test.
**Stato:** **Proposta**. La domanda va a Carmine attraverso il master; la risposta si scrive qui con il link.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)** per la strada 2 (cambia la regola del nucleo `UseNoStoreForApi`), caso **(a)** per
la strada 3 (un valore della striscia). La strada 1 estende la cache del client IVAO, che è nucleo.
**Da dove viene:** l'analisi della velocità del sito di prova (`docs/internal/test/velocita-del-sito.md`, §3.4 e §4, C2; PR #403).

## 1. Il problema, in tre numeri

- `networkStats` è il blocco più caro: 323 richieste in otto giorni, p50 429 ms, p95 1,2 s, metà del tempo di tutti i blocchi dati.
- La fotografia della rete (`/v2/tracker/whazzup`, alcuni MB) resta in memoria 60 s (`IvaoWhazzup.Freshness`), ma un processo vive
  30 s in mediana: **183 processi su 400** l'hanno scaricata di nuovo (p50 536 ms, ~9 MB di memoria ogni volta).
- La striscia (`LiveStatusStrip`) richiede ogni 60 s: un processo vive meno, quindi **una scheda pubblica aperta e visibile risveglia
  l'hub ogni minuto** (≈ 8 s di avvio a chi la provoca, ≈ 6 s di CPU al server). Una scheda nascosta non chiede già oggi: React Query
  non ripete la domanda quando la pagina non è visibile (`refetchIntervalInBackground` è spento di default; verificato in
  `query-core` 5.102).

**Già fatto, senza decisione** (PR «Refs #397»): un solo download alla volta dentro un processo. I blocchi di una pagina fresca
chiedevano insieme, ognuno con il suo client, e scaricavano due volte; ora il secondo aspetta il primo e legge quello che ha tenuto.

## 2. Le tre strade

| | Che cosa | Che cosa guadagna | Che cosa costa |
|---|---|---|---|
| **1** | La fotografia **ridotta** (pochi KB, nessun nome) **fuori dal processo**, con la sua ora: un file in una cartella dell'installazione (come `diagnostics/`), scritto con un nome temporaneo e rinominato. Un processo nuovo, entro il minuto, la legge invece di scaricarla | ~0,5 s e ~9 MB sulla prima pagina pubblica di un processo nato meno di un minuto dopo un altro che l'aveva scaricata. **Nessun risveglio in meno** | Una forma serializzata di `IvaoNetworkPicture` e un lettore che non si fida del file; una cartella scrivibile in più. Non un record nel database: aprirebbe una connessione del pool condiviso (≤ 15) per risparmiare una chiamata a IVAO |
| **2** | La risposta **anonima** di `networkStats` con `Cache-Control: public, s-maxage=60` invece di `no-store`, e una regola di cache di Cloudflare su `/api/blocks/data/networkStats` (Carmine, nel pannello). Solo quel blocco: è l'unico che non legge il database né chi chiede | **Con più lettori**: tutte le schede aperte nello stesso minuto ricevono una risposta di Cloudflare e **l'hub non si sveglia**; in produzione, con decine di schede, una richiesta al minuto invece di decine. **Con una scheda sola non guadagna niente**: la sua domanda arriva quando la copia di un minuto è appena scaduta | Un'eccezione nella regola del nucleo «ogni `/api` è `no-store`», scritta per un tipo di blocco (`AlwaysLive` e senza dati di nessuno), con un test. Una risposta che porta `Set-Cookie` (una sessione rinnovata) non va in cache: Cloudflare la passa da sé. Un fork senza Cloudflare non perde niente |
| **3** | La striscia chiede **meno**: ogni 60 s solo finché chi legge è attivo; dopo **10 minuti** senza un gesto sulla pagina (mouse, tasto, scroll) smette, e riprende al primo gesto. Oppure, più semplice, ogni **5 minuti** sempre | **Toglie i risvegli della scheda dimenticata aperta**, che è il caso di una scheda sola, quello che la strada 2 non copre. Con l'intervallo di 5 minuti i risvegli della striscia si dividono per cinque | I numeri restano vecchi finché il lettore non si muove (strada 3a) o fino a 5 minuti (3b). Una riga di commento della striscia («l'intervallo è il minuto della cache del server») cambia |

## 3. Come si intrecciano con il resto

- **Se H1 riesce** (Passenger tiene acceso un processo: `velocita-del-sito.md` §4), la cache in memoria di 60 s torna a funzionare, e la
  strada 1 non serve più; la 2 e la 3 restano utili, perché riducono il traffico verso l'hub comunque.
- **Il ping che tiene sveglio l'hub** (sessione parallela, Worker di Cloudflare) va nella stessa direzione di H1. L'analisi dice che su
  questo server il processo si ferma comunque entro il minuto (§4, «Da non fare»): se è così, la strada 1 resta l'unica che toglie il
  download dalla prima pagina di un processo nuovo.

## 4. La raccomandazione

**2 e 3a insieme, la 1 no per ora.**

- La 2 e la 3 sono le sole che **tolgono risvegli**, che costano 8 s, contro i 0,5 s che toglie la 1. La 2 copre i molti lettori, la 3a la
  scheda sola: insieme coprono i due casi.
- La 3a e non la 3b: chi guarda la pagina vede i numeri del minuto come oggi; si smette di chiedere solo per chi non c'è.
- La 1 aspetta la risposta di H1 e la prova del ping: se il processo resta acceso non serve, se non resta acceso si fa in una PR sua.

## 5. La risposta di Carmine

(da scrivere, con il link)

## Da portare nel piano

- §16 (meccanismi generici), la regola della cache dell'API: `no-store` per tutto `/api`, **tranne** la risposta anonima del blocco dati
  `networkStats` (se la strada 2 è scelta), con il perché.
- Design M1 §6.2 (la striscia): l'intervallo e quando smette di chiedere (se la strada 3 è scelta).
- `docs/DEPLOYING.md`: la regola di cache di Cloudflare per quel percorso (se la strada 2 è scelta).
