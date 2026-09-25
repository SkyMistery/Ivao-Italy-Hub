# Rating e postazioni vengono dal nucleo: il modulo non scrive regole di IVAO

**Data:** 25 settembre 2026 — fase A0 di M3
**Stato:** **decisa** (Carmine, 25 settembre 2026, sulla PR #121). Le regole dei rating stanno nel vocabolario del nucleo:
correzione 3 della [prima revisione][r0], accolta nel [secondo giro][r2]. Le postazioni vengono da IVAO, con il legame
postazione→rating nel vocabolario e nelle impostazioni solo `hiddenPositions`: n.5, [precisata][r3] dopo la prima risposta in
[r1]. La **forma nel codice** del vocabolario e delle postazioni la scrivono le note nuove delle fasi del nucleo A1 e A2.
**Regola applicata:** `CLAUDE.md` §3 («does this name IVAO?») e §5, caso **(b)**: si estende il perimetro IVAO del nucleo (dati di
riferimento, tabelle `ref_ivao_*`), non si scrive nel modulo. Design `07-design-m3.md` §1.6, §1.7, §2.2, §2.4, §8 n.4 e n.5,
§12 n.5.

[r0]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/121#issuecomment-5832673576
[r1]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/121#issuecomment-5832705237
[r2]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/121#issuecomment-5832839987
[r3]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/121#issuecomment-5833005647

## 1. Che cosa serviva

Il training vive di regole dei rating:

- l'hub propone al trainee **solo il rating successivo** al suo, se ha un training pratico (R.2, d2): con le regole di IVAO di oggi
  ADC, APC e ACC per l'ATC, PP, SPP e CP per i piloti;
- un trainer conduce un training se il suo rating è **uguale o superiore** (R.1, d2), e SEC, SAI e CAI fanno tutti i training;
- l'ATC sceglie la **postazione** fra quelle su cui si fa training per quel rating (R.2), che PATS tiene in una tabella scritta a
  mano (`facilities`, 143 righe).

La prima stesura del design le scriveva come numeri del modulo («i rating allenati sono 5–7», «SEC, SAI e CAI passano sempre») e
metteva nelle impostazioni un `facilityRatings` con i tipi di postazione per rating. **Carmine l'ha corretta** ([prima
revisione][r0], punto 3): sono conoscenza di IVAO, e stanno nel vocabolario dei rating del nucleo o nelle impostazioni, non come
costanti del modulo.

## 2. Le decisioni

1. **Le regole dei rating stanno nel vocabolario dei rating del perimetro IVAO del nucleo** (estensione n.4, fase **A1**): per
   ATC e piloti il numero di IVAO, l'**ordine**, la sigla, il nome tradotto, **quali hanno un training pratico** e **quale tipo di
   postazione** serve a ciascuno. Il modulo gli fa tre domande — il rating successivo a questo, se ha un training; questo rating è
   almeno quello?; quali postazioni per questo rating — e **non scrive mai un numero di rating**. «SEC, SAI e CAI fanno tutti i
   training» non è una regola a parte: segue dall'ordine, perché stanno sopra ogni rating allenato. Con il vocabolario nasce il
   componente **`RatingBadge`** dell'elenco chiuso (piano §8.3), un badge di testo, senza immagini di IVAO.
2. **Le postazioni ATC vengono da IVAO** (n.5; estensione n.5, fase **A2**): una tabella `ref_ivao_atc_positions` riempita dalla
   sincronizzazione notturna dei dati di riferimento (`/v2/ATCPositions/all`, `/v2/subcenters/all`; i campi si misurano con il
   token vero), con una directory come `IAirportDirectory`. **Il legame postazione→rating sta nel vocabolario** (n.4); se le
   postazioni di IVAO portano già un rating minimo, il legame si misura da lì.
3. **Nelle impostazioni del modulo c'è solo `hiddenPositions`**: le postazioni che il TD esclude dal training, vuote di default.
   **Niente `facilityRatings`.** La prima risposta di Carmine lo nominava ([r1], n.5) perché era scritta sul testo di prima della
   correzione 3, e l'ha corretta lui stesso ([r3]).

## 3. Alternative scartate

| Alternativa | Perché no |
|---|---|
| I rating allenati e i loro numeri come costanti del modulo | regole di IVAO fuori dal perimetro IVAO (`CLAUDE.md` §3): un fork e un cambio di IVAO toccherebbero il modulo |
| `facilityRatings` nelle impostazioni (i tipi di postazione per rating) | la stessa regola di IVAO, scritta a mano da ogni divisione; tolta da Carmine ([r3]) |
| Un elenco delle postazioni scritto a mano dallo staff, come le 143 righe di PATS | un dato di IVAO copiato, da tenere allineato a mano |

## 4. Che cosa si tocca

- **A1** (nucleo): il vocabolario dei rating e `RatingBadge`, con la loro nota nuova. I test del vocabolario vero stanno nel
  nucleo; il modulo si prova su un vocabolario di prova (design §10).
- **A2** (nucleo): `ref_ivao_atc_positions`, la sincronizzazione e la directory, con la loro nota nuova; le forme delle risposte
  misurate con il token vero e salvate come fixture.
- **A4**: `hiddenPositions` nelle impostazioni. **A6**: il rating proposto e l'elenco delle postazioni. **A7**: il trainer adatto.
- **I controlli di architettura del modulo** (design §10: il modulo non nomina IVAO e non scrive numeri di rating) stanno in un file
  di test del modulo: `ArchitectureTests.cs` è un file del maintainer (`CLAUDE.md` §0 regola 2).

## Da portare nel piano

- **§9.1, riga «Dati di riferimento IVAO»**: le postazioni ATC di IVAO (A2) e il vocabolario dei rating (A1), quando le note delle
  loro fasi sono decise.
- **§4.2** (il perimetro IVAO): il vocabolario dei rating ne fa parte; un modulo chiede, non scrive regole di IVAO.
- **§8.3**: `RatingBadge`, già nell'elenco del piano, entra nel catalogo dei componenti con A1 (`docs/UI-GUIDELINES.md` lo
  rimandava ai moduli).
