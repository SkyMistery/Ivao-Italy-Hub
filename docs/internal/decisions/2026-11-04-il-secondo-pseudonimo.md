# Il secondo pseudonimo di una cancellazione rifatta (#326, elemento 4)

**Data:** 4 novembre 2026, fase di test (nota `2026-10-18-la-fase-di-test`), branch `fix/review-lot5b-audit`.
**Stato:** **decisa** — **Carmine in chat al master, 4 novembre 2026** («Sì, come consigli»), pubblicato su sua istruzione sull'issue
([la risposta][a326]).
**Regola applicata:** `CLAUDE.md` §5: un'eccezione scritta a una risposta già data, nessun comportamento cambia.

[a326]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/326#issuecomment-6630877426

## Che cosa succede

Una cancellazione dei dati di una persona va per passi, ciascuno nella sua transazione (`PersonalDataErasure`): il nucleo, poi un modulo
alla volta. Se si ferma a metà — un modulo ha già scritto lo pseudonimo P1, un passo dopo fallisce — e il superadmin la rilancia, il
secondo giro prende un numero nuovo, P2, per quello che è rimasto. La persona finisce con due pseudonimi, e i conteggi aggregati (i piloti
distinti di un tour, le decisioni di un validatore) crescono di uno.

È contro la risposta 1 della nota `2026-09-25-la-cancellazione-dei-dati-di-una-persona` («un numero negativo per persona … i conteggi
aggregati restano uguali»).

## La decisione

**Si accetta il +1.** I conteggi restano uguali, **salvo dopo una cancellazione fallita e rilanciata**: è un'eccezione scritta alla
risposta 1.

L'alternativa scartata: ricordare fra i due giri quale pseudonimo spetta a quale VID (un segno tenuto finché il giro non finisce, poi
cancellato). È proprio il legame che la cancellazione toglie, e resterebbe nel database finché qualcuno non rilanciasse la cancellazione —
magari mai. Un conteggio più alto di uno, in un caso raro e visibile nel registro, costa meno di un VID ricollegabile al suo pseudonimo.

Nel codice non cambia niente: una riga accanto a `NextPseudonymAsync` (`Core/Privacy/PersonalDataErasure.cs`) dice che il numero è nuovo a
ogni giro e perché.

## Da portare nel piano

- `00-piano-di-progettazione.md` §9.7 («Privacy dei membri»), dove dice che lo pseudonimo lascia i conteggi uguali: «salvo dopo una
  cancellazione fallita a metà e rilanciata, che dà alla persona un secondo pseudonimo e ai conteggi uno in più (nota
  `2026-11-04-il-secondo-pseudonimo`)»; versione e changelog.
