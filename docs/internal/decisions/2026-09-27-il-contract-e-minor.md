# La metà «contract» di un expand/contract è MINOR, con la copia fresca

**Data:** 27 settembre 2026, sera
**Stato:** **decisa** (Carmine, 27 settembre 2026, in chat: «sono d'accordo con la tua proposta»)
**Regola applicata:** `CLAUDE.md` §5, caso **(a)**: chiude il punto lasciato aperto dalla nota
`2026-09-27-la-versione-del-sito` sulla regola dei numeri.

## La domanda

La regola dei tre numeri (`Directory.Build.props`, la stessa di vIPI) dice:
- **PATCH**: solo correzioni, nessuna migrazione;
- **MINOR**: funzionalità nuove e/o migrazioni **additive**;
- **MAJOR**: il pacchetto non si consegna con il solo FTP.

La seconda metà di un expand/contract (`CLAUDE.md` §6) toglie una colonna, una tabella o un indice che il codice non usa più
da un rilascio. La sua migrazione non è additiva, ma si consegna con il solo FTP. A lettera non è nessuna delle tre.

## La decisione

- **È MINOR.** Il maggiore risponde solo a «basta l'FTP, o bisogna coordinarsi con chi amministra il database?», e qui basta
  l'FTP. Chiamarlo MAJOR farebbe partire un coordinamento che non serve, e toglierebbe al maggiore il suo significato.
- **Si consegna a occhi aperti.** Il foglio della consegna lo dice in cima e in rosso: quale migrazione, che cosa toglie, e
  **una copia fresca del database prima del caricamento**. Senza la copia non si consegna. La migrazione gira da sola
  all'avvio, su DDL non transazionale, quindi per tornare indietro si ripristina la copia e si carica il pacchetto di prima.
- Chi prepara la consegna guarda le migrazioni nuove prima del tag (`docs/DELIVERING.md`, «Before you start»).

**Scartate:**
- **MAJOR per ogni contract**: sposterebbe il senso del maggiore da «serve il database» a «è rischioso»;
- **un suffisso o una quarta cifra**: la forma dei tre numeri è tenuta da un test.

## Che cosa si tocca

- **`Directory.Build.props`:** la regola, accanto al numero.
- **`docs/DELIVERING.md`:** «Before you start» e §5.
- **Piano:** §11.3 punto 1.

## Da portare nel piano

Portato nella stessa PR (piano 1.18), perché la scrive il master.
