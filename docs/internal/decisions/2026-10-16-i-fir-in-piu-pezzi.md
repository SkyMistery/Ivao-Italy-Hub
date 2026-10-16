# I FIR disegnati in più pezzi

**Data:** 16 ottobre 2026 — correzione del nucleo (`src/IvaoHub.Core/Airspace/`, nato in T1), branch `fix/fir-sync-duplicates`
**Stato:** **Correzione** — nessuna scelta nuova: il dato si legge com'è, e il job non fallisce più per un identificativo ripetuto.
Versione 0.7.5 assegnata dal master (le PR dei tour hanno 0.7.1–0.7.4).
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: il lettore della fonte (`VatSpyFirBoundarySource`, nota
`2026-09-16-i-confini-dei-fir.md`) e il job (`FirSyncJob`) esistono; si correggono, nessun meccanismo nuovo.

## 1. Il guasto

Sull'installazione di prova (`test.it.ivao.aero`, 0.7.0), al primo avvio dopo la consegna, il recupero dei job (E10j) ha fatto girare
per la prima volta `fir-boundaries-sync`, ed è fallito:

```
The instance of entity type 'FirBoundary' cannot be tracked because another instance with the same key value for {'Id'} is already being tracked.
```

Il job aggiungeva una riga nuova quando l'identificativo non era fra le righe esistenti, ma non la metteva fra quelle: un identificativo
che la fonte nomina **due volte** produceva due righe con la stessa chiave, EF le rifiutava e la corsa finiva senza scrivere niente.
Su un database nuovo, quindi, **nessun contorno**: «in quale FIR è questo punto» non rispondeva mai. Prima nessuno lo aveva visto
perché il job gira la domenica alle 04:10, e un'installazione che dorme a quell'ora non lo faceva mai partire.

## 2. Che cosa c'è nel dataset (misurato il 16 ottobre 2026)

`Boundaries.geojson` di VATSpy: 1219 feature, 426 regioni intere (senza i settori con il trattino). Cinque identificativi compaiono due
volte, nessuno con maiuscole diverse:

| Id | Che cosa sono le due feature |
|---|---|
| `KZNY` | un pezzo continentale (`oceanic: 0`) e uno oceanico (`oceanic: 1`) |
| `SUEO` | come `KZNY` |
| `CZQM`, `CZQX` | due poligoni diversi ciascuno; le due coppie si somigliano (stesso riquadro complessivo), com'è il dataset |
| `NZOH` | lo stesso poligono due volte, identico carattere per carattere |

Quindi non sono doppioni da scartare: sono **pezzi della stessa regione**. Tenerne uno solo farebbe rispondere «nessun FIR» a un
punto dell'altro (l'Atlantico di `KZNY`, l'oceano di `SUEO`).

## 3. La correzione

- **Il lettore fonde i pezzi** (`VatSpyFirBoundarySource.Read`): le feature con lo stesso identificativo diventano una riga sola, con
  un `MultiPolygon` che contiene tutti i poligoni come sono arrivati; un poligono ripetuto identico si tiene una volta; il riquadro è
  quello che li contiene tutti; la regione è oceanica solo se **tutti** i pezzi lo sono (`KZNY` resta New York); la `region` è la prima
  che c'è. Una regione in un pezzo solo resta com'era, geometria compresa. `FirShape` legge già un `MultiPolygon` (un punto dentro
  un anello qualsiasi è dentro la regione), quindi il localizzatore non cambia.
- **La chiave come la confronta il database**: `ref_firs.id` è sotto `utf8mb4_unicode_ci`, che non distingue maiuscole né spazi in
  coda. Il lettore raggruppa per identificativo **tolto lo spazio e in maiuscolo**, lo stesso che scrive nella riga; il job confronta
  già senza maiuscole.
- **Il job non fallisce più per un nome ripetuto**: il contratto di `IFirBoundarySource` ora dice «una per regione, i pezzi li fonde la
  fonte», perché solo la fonte sa che cosa sono. Se una fonte lo rompesse, la riga aggiunta è già nel dizionario e il secondo nome la
  aggiorna (vince l'ultimo); il messaggio in `hub_jobs_log` lo dice («named more than once and kept as the last: …»). Meglio un pezzo
  perso e scritto nel registro che nessun contorno.

Provato sul file vero (una prova locale, non committata: il file ha licenza CC BY-SA e non entra nel repository): 426 righe, 426
identificativi distinti; `KZNY`, `SUEO`, `CZQM`, `CZQX` con due anelli, `NZOH` con uno; un punto nel pezzo oceanico di `KZNY` e di
`SUEO` e Fiumicino in `LIRR` rispondono giusto.

## 4. Le alternative scartate

- **Tenere l'ultima feature** (solo la correzione del job): niente più errore, ma un punto del pezzo perso non sarebbe in nessuna
  regione, in silenzio.
- **Fondere nel job**: il job non sa leggere una geometria e non deve; la forma la conosce il lettore.
- **Una riga per pezzo** (chiave composta): una migrazione per un problema che il `MultiPolygon` risolve già, e «quali FIR ha
  attraversato» dovrebbe togliere i doppioni.

## 5. Sull'installazione di prova

Il recupero dei job conta l'ultima **partenza**, riuscita o no: la corsa fallita del 16 ottobre conta come fatta, e il job torna dovuto
alla sua ora successiva, **domenica alle 04:10** del fuso della divisione, oppure alla prima visita dopo quell'ora. Con 0.7.5 consegnata
prima di allora i contorni si caricano a quel giro; con 0.7.0 ancora installata il job fallirebbe di nuovo nello stesso modo.

## Da portare nel piano

- §9.7 o la sezione dei confini dei FIR (nota `2026-09-16-i-confini-dei-fir.md`): la fonte consegna **una riga per regione**; una
  regione che il dataset disegna in più feature è un `MultiPolygon` con tutti i pezzi, oceanica solo se lo sono tutti.
- Versione e changelog: 0.7.5.
