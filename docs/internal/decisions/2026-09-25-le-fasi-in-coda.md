# Le fasi in coda, e il revisore che commenta da solo

**Data:** 25 settembre 2026, dopo la revisione del design di M3 (PR #121)
**Stato:** **decisa** (Carmine, 25 settembre 2026, in chat: «tutte e due» alle due proposte del revisore)
**Regola applicata:** `CLAUDE.md` §5, caso **(c)**: nessun meccanismo del prodotto cambia, ma cambia il modo di lavorare
sul repository fissato dalla nota `2026-09-24-un-secondo-sviluppatore` (§4) e da `CLAUDE.md` §0, regola 4.

## 1. Che cosa non andava

La PR #121 (il design di M3) ha chiesto tre giri in un pomeriggio. Due cose li hanno allungati:

- **Carmine faceva da postino.** `CLAUDE.local.md` chiede il suo sì per ogni commento del revisore sulla PR del
  collaboratore, perché esce a suo nome: ogni rilievo passava da lui due volte, prima da approvare, poi da inoltrare.
- **Il collaboratore aspetta il merge per cominciare la fase dopo.** `CLAUDE.md` §0 regola 4 vuole un branch da un
  `main` aggiornato, e `CONTRIBUTING.md` sconsigliava le PR impilate. Con un revisore che legge quando Carmine glielo
  chiede e un merge solo a mano, il collaboratore resta fermo a ogni fase.

I giri di decisione del design, invece, non si ripetono: le decisioni di M3 sono prese (§12 del design) e vanno nelle
note della fase A0; una PR di codice normalmente non ne ha.

## 2. Le due risposte di Carmine

1. **Il revisore commenta da solo** i rilievi sulle PR del collaboratore, controlla le correzioni e chiama Carmine
   quando la PR è pronta. Restano di Carmine le **decisioni** (una domanda ⚖️, una nota «Proposta») e **il merge**.
2. **Le fasi vanno in coda**: il collaboratore comincia la fase dopo senza aspettare il merge; il revisore le legge anche
   in blocco; Carmine le unisce in ordine.

## 3. Che cosa si fa

**Il revisore** (`CLAUDE.md` §0, riga «Reviewer»; `CLAUDE.local.md`): pubblica i rilievi sulla PR senza chiedere ogni
volta, e li presenta come rilievi del revisore, non come parola di Carmine. Non risponde mai a una domanda che spetta a
Carmine, nemmeno ripetendo una raccomandazione: un commento che decide esce solo con il sì di Carmine in chat, come prima.
Non approva e non unisce.

**La coda** (`CLAUDE.md` §0 regola 4, `CONTRIBUTING.md` «Phases in a queue»):

- il branch della fase dopo parte dal branch della fase prima;
- **la PR punta sempre a `main`**, mai a un altro branch. Così `build-test` e `core-guard` girano (e la guardia legge
  sempre la base giusta), e non c'è niente da ridirigere prima del merge: la trappola della base cancellata che chiude la
  PR figlia per sempre non si presenta. Finché la fase sotto non è unita, il diff della PR mostra anche i suoi commit;
- **bozza**, con `(after #N)` nel titolo e `Queued after #N.` in testa al corpo: GitHub non unisce una bozza, quindi
  l'ordine non si può sbagliare. Nella sezione «For the reviewer» c'è l'intervallo che è della fase
  (`git diff m3/<prima>...m3/<dopo>`), e il revisore legge quello;
- una correzione chiesta su una fase sotto si fa sul suo branch e si porta **con un merge** nei branch sopra, in ordine;
  mai un rebase;
- unita la #N: il collaboratore unisce `main` nel branch dopo, ricompila e rilancia i test, toglie `(after #N)` e segna
  la PR pronta. Il revisore controlla che il diff sia ora solo della fase;
- una fase che dipende da una risposta di Carmine (una nota di caso (c), un cambio del nucleo ancora in revisione) non si
  mette in coda sopra la domanda: le parti che ne dipendono aspettano.

**Il costo, accettato**: se una fase del nucleo sotto (A1–A3 di M3) va corretta, la correzione risale in tutte le fasi
sopra, e qualcosa si riscrive. Il revisore può chiedere di fermare la coda quando la vede allungarsi su una base incerta.

**Scartate:** più fasi in una PR sola (il nucleo si legge separato dal modulo, `CLAUDE.md` §0 regola 6, e una PR enorme si
rivede male); PR figlie con base il branch della fase sotto (la CI e la guardia vanno pensate per un'altra base, e il
merge va ridiretto a mano, o la PR finisce nel branch sbagliato); un permesso di merge al revisore (la restrizione
assoluta del 24 settembre resta).

## 4. Che cosa si tocca

- `CLAUDE.md` §0: la riga «Reviewer» e la regola 4.
- `CONTRIBUTING.md`: il passo 1 di «Working a phase» e la sezione «Phases in a queue», al posto del divieto di impilare.
- `CLAUDE.local.md` (privato, fuori dal repository): il sì di Carmine non serve più per i rilievi; serve per le decisioni.
- Piano: versione 1.10 e riga di changelog.

## Da portare nel piano

Portato nella stessa PR, perché la scrive la sessione di Carmine: versione 1.10, changelog.
