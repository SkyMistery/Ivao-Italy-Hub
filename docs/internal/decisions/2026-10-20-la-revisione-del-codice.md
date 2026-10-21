# La revisione di tutto il codice, a codice finito

**Data:** 20 ottobre 2026
**Stato:** **decisa da Carmine in chat al master**, 20 ottobre 2026: «Sì: la pausa è tolta col nuovo account. Scrivi il piano come
nota e parti dal lotto 1, permessi», e poi «Possiamo anche fare più lotti in parallelo». È una nota del master: dice come si lavora, non
tocca il codice. Il piano dei lotti, con l'avanzamento, è nel file accanto alla lista di sicurezza,
`docs/internal/test/revisione-del-codice.md`.
**Regola applicata:** la fase di test (nota `2026-10-18-la-fase-di-test`, §3: «il master [fa] la revisione del codice per la sicurezza
e la privacy»), allargata ai difetti.

## 1. Che cosa serve

Con M4 chiusa nel codice (tranne il Gate Manager) l'hub è scritto: circa **87 000 righe di C#** (nucleo 37 000, Eventi 22 000, Tour
18 000, Training 7 000, host 3 000) e **84 000 di TypeScript**, test esclusi. Ogni milestone ha avuto la sua revisione di chiusura
(`decisions/2026-09-04-m0-review.md` … `2026-10-17-m4-review.md`), ma quelle guardavano soprattutto **le regole** (`CLAUDE.md` §2–§5):
un meccanismo copiato, un handler in più, una stringa italiana. Nessuna ha letto il codice **cercando i difetti**: una transazione che
lascia mezza riga, un orario nel fuso sbagliato, due richieste insieme, una lista senza tetto, un permesso che apre una riga di troppo.

## 2. Che cosa si fa

- **Dieci lotti per rischio**, non per cartella (il file del piano li elenca con i file di ciascuno).
- **La lettura di un lotto** la fa un agente, con due liste: la lista di sicurezza (`test/sicurezza-lista-di-controllo.md`) e la lista
  dei difetti del file del piano (§2 di quel file). **Più lotti in parallelo** (Carmine, 20 ottobre): le letture sono indipendenti.
- **Ogni rilievo lo verifica il master nel codice** prima che esca, come in ogni revisione da settembre; dove serve, una prova mirata
  (una mutazione, un test che lo prende). Un rilievo non verificato non esce.
- **Dove va un rilievo**: una issue con il modulo e la gravità (modello `bug.yml`, nota della fase di test §5); una debolezza
  **sfruttabile** in un advisory privato, mai in una issue pubblica prima della correzione (stessa nota, §4).
- **Chi corregge**: il proprietario, come nella fase di test (§3): `dalberone` Eventi e Training, le sessioni di lavoro di Carmine il
  nucleo, il sito e i Tour. Il master non corregge: smista, e rivede le correzioni.
- **Che cosa non si fa**: nessuna esecuzione contro la prova online, nessun giro e2e dentro la lettura; i rilievi che vogliono una
  prova in esecuzione si segnano «da provare» e si provano su un'istanza locale.

## 3. L'ordine

Prima i lotti a rischio alto: **1** (permessi), **2** (accesso e token), **3** (contenuti, blocchi, file), **6** (Eventi). Eventi ha già
avuto una lettura di sicurezza il 19 ottobre (issue #311–#314, advisory GHSA-c964 e GHSA-h2hw): il lotto 6 cerca i difetti e non ripete
quella.

## Da portare nel piano

- **§13**: nella fase di test, la revisione dei difetti di tutto il codice per lotti, con il file del piano.
