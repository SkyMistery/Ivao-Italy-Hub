# Chi è cancellato prima del controllo

**Data:** 1 novembre 2026, fase di test.
**Stato:** **Proposta** — la domanda a Carmine è al §3; la risposta, con il link al suo commento, va scritta qui prima del merge.
**Regola applicata:** `CLAUDE.md` §5, caso **(c)** per il §3 (una scelta che il codice non può fare da solo); il §2 è una correzione
(issue #322, voce 5 (b)), che non decide nulla di nuovo.

## 1. Il caso

Fra la fine di un evento e il suo controllo (`events-after`, sei ore dopo la fine; giorni se la rete non risponde) una persona può
chiedere la cancellazione dei suoi dati. Le sue prenotazioni e i suoi turni di un evento **finito** restano, con lo pseudonimo che il
nucleo scrive nella colonna (note `2026-09-29-i-dati-dei-membri-negli-eventi` §2.5, E8b; E11b). Il controllo poi:

- chiedeva alla rete le sessioni **dello pseudonimo**, non ne trovava, e scriveva «prenotato, non volato»;
- chiedeva all'archivio le connessioni dello pseudonimo, non ne trovava, proponeva l'assenza e, dove l'archivio copre il turno, scriveva
  come «chi ha coperto» chi l'archivio trova sulla posizione: **la persona stessa, col suo VID vero**, appena cancellato dall'hub.

## 2. Che cosa fa la correzione (#322, voce 5 (b))

Una riga di una persona cancellata **non si chiede** alla rete né all'archivio: la rete la conosce per un VID che l'hub non ha più.
Resta **non controllata** — la prenotazione senza `flown_checked_at`, il turno `Unknown` —, che è «non abbiamo potuto guardare», mai
«non ha volato» (la regola di E10t: le due risposte non si leggono uguali), e **non tiene aperto l'evento**: il controllo lo chiude
senza di lei. Le statistiche la contano fra le prenotate e mai fra le non volate; il turno né presente né assente. Nel registro dei
piloti non entra (una prenotazione non controllata non è un'assenza).

## 3. La domanda: quello che la rete sa e l'hub non sa più

La rete e l'archivio continuano a dare il **VID vero** di chi è stato cancellato, e l'hub non può riconoscerlo (la cancellazione non
lascia la corrispondenza, per definizione). Restano due effetti:

1. **«Volati senza prenotare»** (`flown_unbooked`): il pilota cancellato che ha volato è contato fra chi ha volato senza prenotare, perché
   la sua prenotazione ha lo pseudonimo. Il numero è più alto, al massimo di quanti prenotanti sono stati cancellati in quelle ore. Non è
   un dato di persona: è un numero.
2. **«Chi ha coperto»** su un **altro** turno dello stesso evento (`covered_by_vid`): se il controllore cancellato era sulla posizione di
   un collega assente, l'archivio lo dà come chi ha coperto, e il controllo scrive il suo VID vero sulla riga del collega, dopo la
   cancellazione. Questo è un dato di persona.

Le strade:

- **(A)** lasciare com'è: la finestra è di ore, il caso è raro, e per (2) lo staff legge un VID che non nomina più nessuno nell'hub.
- **(B) — raccomandata.** Per (2): in un evento con un turno di una persona cancellata non ancora controllato, il controllo **non scrive
  «chi ha coperto»** su nessun turno di quell'evento (le assenze restano proposte, senza chi ha coperto: lo staff decide come oggi). Per
  (1): il numero resta com'è (A), detto nel registro del job come già si dice quando il conteggio è un minimo. Poche righe nel job,
  nessuna migrazione, nessuna modifica del nucleo.
- **(C)** come (B) per (2), e per (1) togliere dal conteggio un pilota per ogni prenotazione cancellata non controllata: giusto se ha
  volato, uno in meno se non ha volato.

La correzione di #322 **non fa né (B) né (C)**: aspetta la risposta. Il codice oggi è (A).

## Da portare nel piano

- **§9.7, Events** (il controllo dopo l'evento) e design M4 **§5.1**: una riga di una persona cancellata prima del controllo non si chiede
  alla rete né all'archivio, resta non controllata e non tiene aperto l'evento (§2); e la risposta al §3.
