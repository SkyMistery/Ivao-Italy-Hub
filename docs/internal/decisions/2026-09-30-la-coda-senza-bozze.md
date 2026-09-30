# La coda senza bozze, e il giro del master che parte da solo

**Data:** 30 settembre 2026, dopo la chiusura di M3 e con M4 a `dalberone`
**Stato:** **decisa** (Carmine, 30 settembre 2026, in chat: «tutte e due» alle due proposte del master; la risposta è in chat,
quindi non c'è un link a un commento, come per `2026-09-25-le-fasi-in-coda`)
**Regola applicata:** `CLAUDE.md` §5, caso **(c)**: nessun meccanismo del prodotto cambia, cambia il modo di lavorare fissato dalla
nota `2026-09-25-le-fasi-in-coda` e da `CLAUDE.md` §0, regola 4.

## 1. Che cosa non andava

Con le fasi in coda (nota del 25 settembre) ogni fase di M3 ha fatto tre giri anche quando nel merito era già approvata: la fase
sotto veniva unita, il collaboratore fondeva `main` nel suo branch, toglieva «(after #N)», aspettava la CI e **segnava la PR pronta**,
e solo allora il master la ricontrollava e diceva a Carmine «si può unire». Nella maggior parte dei casi il merge di `main` era una
formalità (il branch conteneva già la fase sotto, e `main` non toccava gli stessi file): il master lo verificava comunque con
`git merge-tree`. E il master partiva solo quando Carmine gli scriveva.

## 2. Le due risposte di Carmine

1. **Il giro del master parte da solo**: la sessione master controlla le PR a intervalli, legge le nuove, pubblica i rilievi, chiede
   il passo della coda, e **avvisa Carmine** (una notifica) solo quando c'è una sua **decisione** o una PR **pronta da unire**. Il
   merge resta di Carmine, **una PR alla volta, per numero** (`CLAUDE.md` globale §1): non cambia.
2. **La coda senza bozze**: il collaboratore apre una fase in coda **già pronta**, non in bozza, con `(after #N)` nel titolo e
   `Queued after #N.` in testa al corpo. Quando #N è unita, **il master** controlla che la PR mostri solo la sua fase contro `main`,
   che non abbia conflitti e che la CI sia verde, e la propone a Carmine; al collaboratore chiede di fondere `main` **solo** se c'è un
   conflitto, o se `main` ha cambiato qualcosa che la fase usa.

## 3. Che cosa si fa

- `CONTRIBUTING.md`, «Phases in a queue»: le due regole su bozza e merge di `main` cambiano come al §2.2; l'ordine lo tiene il master,
  che non propone mai una PR la cui `#N` non è unita.
- `CLAUDE.md` §0 regola 4: «as a draft, until the one below is merged» diventa «marked `(after #N)`».
- Il giro del master (`CLAUDE.local.md`, privato di Carmine) non è nel repository: lo fa la sessione master con un ciclo a intervalli.

**Il rischio accettato**: una PR unita senza `main` fuso dentro ha la CI eseguita su un `main` un po' più vecchio. Il master lo copre
con `git merge-tree` (nessun conflitto) e guardando che i file toccati da `main` nel frattempo non siano quelli della fase; se lo sono,
chiede il merge di `main` prima di proporla.

## Da portare nel piano

- Nessuna sezione del piano: il modo di lavorare sta in `CLAUDE.md` §0 e in `CONTRIBUTING.md`, cambiati in questa stessa PR. Una riga
  nel changelog della prossima versione.
