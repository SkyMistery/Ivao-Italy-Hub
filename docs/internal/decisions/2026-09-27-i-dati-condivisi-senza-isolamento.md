# I dati condivisi con vIPI quando il server non isola i database

**Data:** 27 settembre 2026, mentre si prepara l'installazione di prova su `test.it.ivao.aero`
**Stato:** **decisa** (Carmine, 27 settembre 2026, in chat)
**Regola applicata:** `CLAUDE.md` §5, caso **(c)**: cambia un'ipotesi della nota `2026-09-14-dati-condivisi-con-vipi`
(§3.1, §3.3, §4), che dava per possibile l'isolamento fra i due database sul server.

## 1. Che cosa è cambiato

La nota del 14 settembre aveva lasciato due domande per chi amministra il server (§4): se si può avere un utente MariaDB
dedicato con `SELECT` sulle sole viste `v_share_`, e se l'utente di vIPI può creare viste. Il 27 settembre la risposta di chi
amministra, riportata da Carmine: **sul server ogni utente dei database della sottoscrizione può accedere a tutti i database**,
e non si può legare un utente a un database solo. Quindi:

- l'isolamento **fra le due app** non lo fa il database: l'utente dell'hub può leggere e scrivere il database di vIPI, e
  viceversa;
- un permesso per vista, o un utente di sola lettura, non si può avere: la seconda domanda della nota del 14 settembre non
  serve più.

## 2. Le decisioni di Carmine

1. **Le viste restano, ognuno nel suo database**: vIPI pubblica le sue `v_share_` in `itivao_atc` (c'è già
   `v_share_atc_sessions`, dalla migrazione di vIPI 1.43.0); l'hub pubblicherà le sue nel suo database quando vIPI ne avrà
   bisogno (lo staff, nota del 14 settembre §3.5). **Non servono né database `share` a parte né utenti in più**: a chi
   amministra non si chiede niente oltre al database dell'hub e al suo utente.
2. **La vista è il contratto** anche senza permessi: il padrone cambia le sue tabelle come vuole, la vista cambia solo in modo
   additivo (una colonna si aggiunge, mai si rinomina o si toglie), e sceglie le colonne che escono.
3. **L'altra app legge con il suo stesso utente**, attraverso una **stringa di connessione a parte** (nell'hub
   `ConnectionStrings:AtcData`, `Database=itivao_atc`): si vede nei segreti che l'hub legge vIPI, e chi forka la lascia vuota.
4. **Le regole le fa rispettare il codice, non il database** («i siti si proteggono a vicenda»):
   - un'app legge il database dell'altra **solo attraverso le viste `v_share_`, mai tabelle, e solo in lettura**; nell'hub il
     contesto condiviso rifiuta di salvare e mappa solo viste `v_share_`, e **un test di architettura lo tiene**;
   - nessuna query nomina il database dell'altro fuori da quel contesto;
   - **il file dei segreti è la chiave di tutti e due i database**: `secrets/` fuori dal document root (`wwwroot/`), nome non
     indovinabile, direttive nginx che lo negano, per l'hub e per vIPI;
   - i dati più delicati restano **cifrati con chiavi fuori dal database** (nell'hub i token IVAO con `hub-keys/`): chi legge
     il database con l'utente dell'altra app vede testo cifrato.
5. vIPI rispetta le stesse regole dalla sua parte: è lavoro nel suo repository, e glielo si scrive.

## 3. Scartate

- **Due database `share` con permessi di lettura su tutto** (proposta del 27 set, prima della risposta di chi amministra):
  richiedevano utenti legati a un database, che il server non ha.
- **Leggere direttamente le tabelle dell'altra app**, ora che si può: lega ogni cambio di schema dell'una all'altra, e toglie
  il punto in cui si decide che cosa esce.
- **Un'API con chiave segreta** come strada principale: più codice da tutte e due le parti, e la lettura dipende dall'altra
  app accesa (Passenger spegne le app inattive). Resta il ripiego se un giorno le due app non stessero più sullo stesso server.

## 4. Che cosa si tocca

- Piano §2.5 (la riga dei due database), §9.7 («Dati di vIPI»), §15 punto 2.
- `docs/internal/deploy/segreti.esempio.json` e il foglio dell'installazione di prova: la riga `AtcData`, spenta finché
  `division.json → atcData.source` non dice `vipi`.
- Una PR del nucleo: il test di architettura sul contesto condiviso (sola lettura, solo viste `v_share_`).
- vIPI: le stesse regole, nel suo repository.

## Da portare nel piano

Portato nella stessa PR (piano 1.18), perché la scrive il master: §2.5, §9.7, §15 punto 2.
