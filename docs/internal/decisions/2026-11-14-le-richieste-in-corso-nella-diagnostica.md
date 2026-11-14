# Il tipo del blocco, le richieste in corso e quelle tagliate da un arresto, nella diagnostica

**Data:** 14 novembre 2026
**Stato:** **Decisa** da Carmine il 14 novembre 2026, «Sì 402», sulle tre scelte del §2 così come sono: la risposta è
registrata sulla issue, <https://github.com/SkyMistery/Ivao-Italy-Hub/issues/402#issuecomment-6875865214>. Che cosa serve
l'aveva già chiesto lui nella issue #402, aperta dopo l'analisi della velocità del sito di prova
(`docs/internal/test/velocita-del-sito.md` §5).
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: si estende la diagnostica che c'è (nota
`2026-10-18-i-tempi-delle-richieste`, che dopo il merge non si tocca). Nessun meccanismo nuovo, nessun modulo vede niente.
Cambia però il **contratto del file** che Carmine scarica e legge (una colonna in più, il contenuto di `route` per una
rotta, una parte in più nella riga `STOP`), quindi una nota e non solo «Closes #402».

## 1. Che cosa serve

Dalla issue #402, tre cose che i file della 0.9.2 non dicono e che l'analisi ha dovuto stimare o prendere dal log:

1. **quale blocco** era un `GET /api/blocks/data/{type}` (la rotta più chiesta, 1 253 richieste su 5 732, e quella che sveglia
   l'hub più spesso);
2. **quante richieste erano in corso**, perché `jit_ms` e `alloc_kb` sono di tutto il processo: con le 5–10 richieste in
   parallelo di una pagina appena aperta ogni riga conta anche la compilazione delle altre;
3. **se un arresto ha tagliato una richiesta** (44 arresti su 398 a meno di un secondo dall'ultima risposta).

## 2. Come, e le scelte

1. **`route`**: per lo schema `/api/blocks/data/{type}` la colonna scrive il tipo al posto di `{type}`
   (`/api/blocks/data/networkStats`) **solo se è un tipo registrato** nel `BlockRegistry`, l'insieme chiuso dichiarato dal
   codice; altrimenti resta `{type}`. Mai il testo grezzo della richiesta, mai `props` né la query. Il riassunto, che
   raggruppa per rotta, dà da sé una riga per tipo di blocco.
2. **Una colonna nuova, in fondo: `others_n`**, le *altre* richieste che sono state in corso in un momento qualunque della
   vita di questa: quelle già in corso quando è cominciata più quelle cominciate mentre girava. Con `0`, `jit_ms` e `alloc_kb`
   sono della richiesta e di nessun'altra.
   - *Scelta*: la issue dice «in corso quando è cominciata (o finita)». Un solo istante non basta: una richiesta che comincia
     sola e ne vede partire altre cinque ha `0` all'inizio e `jit_ms` di sei. Il conteggio delle sovrapposizioni costa lo
     stesso (un contatore in corso, che c'è, e il numero della richiesta, che c'è già: `numero alla fine − numero proprio`).
   - *Scelta*: **in fondo**, dopo `alloc_kb`, così chi legge per posizione le prime 18 colonne non cambia niente.
   - Le righe vecchie (18 colonne) si rileggono ancora: il riassunto del giorno del cambio le mescola con le nuove, e una riga
     vecchia nelle «dieci più lente» scrive `-` per `others_n`, come `ttfb_ms` senza primo byte.
   - Non conta i controlli di `/health`, che non hanno riga (nota `2026-10-26-il-ping-che-tiene-sveglio-l-hub`): una
     richiesta sovrapposta a un ping ha `0` anche se il ping ha compilato qualcosa (un `SELECT 1`, trascurabile).
3. **La riga `STOP` di `starts.txt`**: `in flight N`, le richieste ancora in corso quando l'arresto è cominciato.
   - *Scelta*: scritta **solo quando N > 0**, come `probes`: la riga di un arresto pulito resta com'era.
   - Le richieste in corso possono ancora finire durante l'arresto ordinato: la loro riga, con lo stesso pid e un orario dopo
     lo `STOP`, è nel file delle richieste. `in flight N` senza N righe dopo lo `STOP` vuol dire richieste tagliate (o righe
     perse con un `SIGKILL`, nota `2026-10-18` §7).

Niente di questo nomina una persona: un tipo di blocco è un nome del codice, due numeri sono conteggi. Il test che controlla
che il file non contenga niente di personale resta com'è, e uno nuovo manda un tipo non registrato (a forma di VID) per vedere
che non arriva nel file.

## 3. Alternative scartate

- **Il tipo grezzo dalla richiesta**: è testo del visitatore, può essere qualunque cosa. No.
- **Una colonna a parte per il tipo del blocco**: una colonna vuota su 4 righe su 5, e il riassunto non lo raggrupperebbe.
- **Il massimo delle richieste contemporanee** durante la richiesta: dice meno (non conta quelle in fila) e costa di più.
- **Le richieste in corso scritte nella riga `SIGNAL`**: il segnale arriva prima dello `STOP`, ma è la riga dell'arresto
  ordinato quella che si legge; con lo `STOP` mancante resta la riga `!!` del processo dopo.

## 4. Che cosa tocca

`src/IvaoHub.Core/Services/RequestTimingsLine.cs` (colonna, lettura delle righe vecchie, la rotta del blocco),
`RequestTimingsLog.cs` (l'intestazione del file), `StartsLog.cs` (la riga `STOP` e la sua intestazione),
`src/IvaoHub.Web/RequestTimingsMiddleware.cs`, `StartsWatch.cs`, `docs/DEPLOYING.md` (due frasi), i test unitari e
d'integrazione della diagnostica. Nessuna migrazione, nessuna pagina, nessun modulo.

## Da portare nel piano

- **§11.3 punto 2** (diagnostica): nel file delle richieste la colonna `others_n` (in fondo) e la rotta dei blocchi dati con
  il tipo registrato; nella riga `STOP` di `starts.txt` `in flight N`.
- `docs/internal/test/velocita-del-sito.md` §5: la voce «nei file dell'hub (#402)» è fatta.
