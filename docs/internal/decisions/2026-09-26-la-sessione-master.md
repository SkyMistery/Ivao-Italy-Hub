# La sessione master

**Data:** 26 settembre 2026, a M3 avviata (A0–A4 unite, A3b aperta)
**Stato:** **decisa** (Carmine, 26 settembre 2026, in chat: quattro risposte, tutte le raccomandazioni)
**Regola applicata:** `CLAUDE.md` §5, caso **(c)**: nessun meccanismo del prodotto cambia, cambia il modo di lavorare sul
repository fissato da `2026-09-24-un-secondo-sviluppatore` e da `2026-09-25-le-fasi-in-coda`.

## 1. Che cosa non andava

Carmine: «siamo in due a lavorare, ma la cosa si sta facendo un po' confusionaria». Misurato il 26 settembre:

- **Dal 20 settembre, 44 PR unite e 15 merge di `main` dentro un branch** per rimetterlo in pari. I file che quei merge
  toccano sono quasi solo documenti di bilancio: il piano (11 volte), `HANDOFF.md` (10), `HANDOFF-M3.md` (8),
  `08-piano-implementazione-m3.md` (7). Ogni PR di Carmine alzava la versione del piano (da 1.10 a 1.15 in tre giorni), e due
  PR parallele collidevano lì anche quando il codice non si toccava.
- **Nessuna sessione possedeva l'ordine.** Lo stato stava in una memoria locale del revisore e nella testa di Carmine; ogni
  merge era un click suo, con la casella del bypass, e ogni fase in coda aspettava che qualcuno si ricordasse di lei.
- **La cartella principale era di tutti.** Era ferma su un branch già unito, 127 commit dietro `main`, con tre worktree
  vecchi; due sessioni nella stessa cartella si erano già spostate il branch sotto i piedi (3 e 25 settembre).

## 2. Le quattro risposte di Carmine

1. **Il via è di Carmine, l'esecuzione è del master.** Carmine scrive in chat «unisci #N, #M», con i numeri; il master unisce
   in ordine e fa ciò che serve in mezzo. Scartate: il master prepara e Carmine preme (resta il postino), e il master che unisce
   da solo ciò che passa la revisione (toglie il controllo umano sul nucleo scritto da un altro agente).
2. **Le sessioni di lavoro di Carmine non scrivono più la versione del piano, il suo changelog e `HANDOFF.md`**: li scrive solo
   il master, dopo i merge, come già faceva il revisore per il collaboratore.
3. **Sul branch del collaboratore il master non spinge niente**: se va rimesso in pari con `main`, glielo chiede sulla PR. La
   sua sessione può avere lavoro non ancora spinto.
4. **Pulizia subito**: cartella principale su `main`, via i worktree e i branch già uniti (fatto il 26 settembre).

## 3. Che cosa si fa

**Quattro ruoli** (`CLAUDE.md` §0):

- **Maintainer**, Carmine: decide che cosa entra in `main` e quando, e prende le decisioni.
- **Master**: **una** sessione Claude di Carmine, nella **cartella principale** del repository, che resta su `main`. È il
  revisore di prima e in più l'integratore. Fa il **giro** quando Carmine lo chiede o una sessione di lavoro lo avvisa: PR
  aperte, CI, conflitti, ordine. Rivede le PR del collaboratore con la procedura di sempre e controlla quelle delle sessioni di
  lavoro. Unisce solo sul via di Carmine, **una PR alla volta, per numero**. Dopo i merge porta le note nel piano e scrive
  `HANDOFF.md`, in una PR sua per lotto. Tiene puliti branch e worktree. Non scrive codice di funzionalità.
- **Sessioni di lavoro** di Carmine: una per filone, **ognuna nel suo worktree**, mai nella cartella principale. Branch, PR su
  `main`, si fermano a CI verde e avvisano il master (l'app desktop lascia che una sessione scriva all'altra). Scrivono la nota
  con «Da portare nel piano», non la versione né il changelog del piano, né `HANDOFF.md`.
- **Collaboratore**: come prima. Solo chi preme il bottone cambia: il master, sul via di Carmine, invece di Carmine.

**Il giro** restituisce tre elenchi: pronte da unire, nell'ordine in cui vanno unite; in attesa di correzioni, e di chi; in
attesa di una decisione di Carmine.

**Il blocco tecnico cambia forma.** Una regola `deny` di Claude Code non si scavalca con un hook, quindi `gh pr merge` esce
dalle regole `deny` in entrambi i posti:

- nelle impostazioni utente di Carmine, private, lo sostituisce un **hook** (`PreToolUse`, Bash e PowerShell): nella cartella
  principale **chiede conferma** a ogni merge, anche in modalità auto; in ogni altra cartella, worktree compresi, **rifiuta**.
  Vale per ogni progetto di Carmine, come valeva il `deny`;
- in `.claude/settings.json` committato passa da `deny` ad `ask`: una sessione del collaboratore chiede al suo umano invece di
  rifiutare da sola, e GitHub rifiuta comunque. Il lucchetto resta il ruleset di `main` («Restrict updates», bypass solo
  admin), verificato da `dalberone` il 25 settembre.

Restano `deny` ovunque il push su `main`, `--force`, i tag, `gh api` in `PUT` e l'auto-merge dell'app: il master unisce solo
con `gh pr merge`, una PR alla volta.

**La restrizione del 24 settembre** («su `main` unisce solo Carmine») resta nella sostanza e cambia nella forma: **solo Carmine
decide** che cosa entra in `main`, con un via per numero scritto in chat; nessuna sessione unisce di sua iniziativa, e il prompt
di conferma è la prova tecnica di quel via.

**Scartate:**

- la merge queue di GitHub, che un repository personale non ha;
- l'auto-merge di GitHub, che unisce quando i controlli sono verdi e non conosce l'ordine delle fasi;
- un controllo periodico automatico del master (`/loop`): costa e non serve, il master lavora quando lo si chiama o lo si
  avvisa.

## 4. Che cosa si tocca

- `CLAUDE.md`: §0 (i ruoli, la regola 1), la riga sulle decisioni in testa, §5, §9.
- `CONTRIBUTING.md`: il passo 6 di «Working a phase» e «Phases in a queue».
- `.github/PULL_REQUEST_TEMPLATE.md`: l'ultima voce della checklist.
- `.claude/settings.json`: `gh pr merge` da `deny` ad `ask`.
- Fuori dal repository: `CLAUDE.local.md`, e in `~/.claude/` di Carmine il `deny` sostituito dall'hook.
- Piano: versione 1.16, changelog, §13 e §16.E; `HANDOFF.md`, il riquadro in testa.

## Da portare nel piano

Portato nella stessa PR, perché la scrive il master.
