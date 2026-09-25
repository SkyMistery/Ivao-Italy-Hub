# Che cosa resta fuori da M3: lo storico di PATS, le altre funzioni del TDCenter, il feed del calendario

**Data:** 25 settembre 2026 — fase A0 di M3
**Stato:** **decisa** (Carmine, 25 settembre 2026, sulla PR #121, [risposte alle domande di §12][r1], n.6, n.8 e n.14; la n.14
ripetuta nel [secondo giro][r2], con le correzioni al design che ne seguono).
**Regola applicata:** `CLAUDE.md` §5, caso **(c)**: che cosa entra nel modulo e che cosa no. Design `07-design-m3.md` §0.1, §0.2,
§0.6, §P, §7, §8 n.9, §11, §12 n.6, n.8 e n.14.

[r1]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/121#issuecomment-5832705237
[r2]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/121#issuecomment-5832839987

## 1. Che cosa serviva decidere

- **Lo storico di PATS.** Il dump del 12 settembre 2026 (fornito da `dalberone`, fuori dal repository perché contiene dati
  personali) ha tre database: `trainingNEW` (PATS vivo, 2020–2026), `exam` (gli esami, 2015–2026) e `training` (il PATS di prima,
  2014–2020, con uno schema diverso e 130 training dal testo rovinato già nell'originale). I codici numerici (`status`,
  `approved`, `type`, `outcome`…) hanno un significato che sta nel codice PHP di PATS, che non abbiamo (design §P, §7).
- **Le altre funzioni del TDCenter di HQ** (piano §2.3-ter): group training, GCA, flight briefing. `dalberone` non sa se servono
  (R.6-ter).
- **Il feed del calendario dei trainer.** Oggi i trainer usano quello di PATS per Google Calendar (R.7, d4). Il feed iCal del
  nucleo è in M6, e la sua forma è una domanda aperta del piano (§15.9). La prima stesura del design proponeva di anticiparlo in
  M3 come fase del nucleo (l'estensione n.9).

## 2. Le decisioni

1. **Lo storico di PATS** (n.6): **un archivio in sola lettura** dei training di `trainingNEW` e degli esami di `exam`, mostrato
   **così com'è** sul percorso del trainee, senza rimapparlo sulle nuove schede — **solo se** otteniamo il significato dei codici.
   **Niente da `training`** (2014–2020). Il dump non entra nel repository, né in una PR né in una fixture; un import si fa con un
   dump nuovo al momento del passaggio.
2. **Group training, GCA e flight briefing sono fuori da M3** (n.8), da riprendere se lo staff TD li chiede. I GCA di un membro
   arrivano già nel profilo IVAO letto al login (`gcas`), un campo che l'hub oggi non legge: una funzione sui GCA non chiederebbe
   fonti nuove.
3. **Il feed del calendario non è in M3** (n.14). Il feed iCal resta in **M6**, con la domanda aperta del piano §15.9;
   l'estensione n.9 e la sua fase sono uscite dal design. Fino ad allora i trainer usano il feed di PATS: **PATS resta acceso solo
   per quello**, quando tutto il resto è passato all'hub.

## 3. Alternative scartate

| Alternativa | Perché no |
|---|---|
| Nessun import dello storico, come per tour ed eventi | il TD torna sul percorso di un membro anni dopo (PATS tiene i training dal 2014); l'archivio lo conserva, se i codici si capiscono |
| Importare anche `training` (2014–2020) | schema diverso e testi rovinati già nell'originale |
| Anticipare in M3 il feed iCal come fase del nucleo (un feed personale, con un token di sola lettura nell'indirizzo) | Carmine lo lascia in M6 con la domanda aperta del piano §15.9 |
| Perdere il feed fino a M6 | i trainer lo usano (d4) |

## 4. Che cosa cambia

- **Il «fatto» di M3** (design §0.1): lo staff TD spegne PATS **tranne il feed del calendario dei trainer**.
- **Le fasi**: l'archivio, se si fa, è l'ultima parte di **A12**, e nessun'altra fase ne dipende. Prima serve il significato dei
  codici: chi lo ha — il codice PHP, chi mantiene PATS, la memoria dello staff — è la domanda del piano §15.7, che resta aperta.
- **Il dump del 12 settembre** è servito solo a misurare e a provare che si importa (design §P): un import vero parte da un dump
  nuovo, al momento del passaggio.

## Da portare nel piano

- **§9.2, riga Training**: group training fuori da M3 (n.8); lo storico di PATS come archivio in sola lettura di `trainingNEW` ed
  `exam`, solo se si ottiene il significato dei codici, niente da `training` (n.6). Il «⚠️ da decidere» sulla migrazione dello
  storico è deciso.
- **§13, riga M3**: il contenuto segue n.6 e n.8 (niente group training; lo storico come archivio, se possibile); l'uscita
  («Spegne `training.ivao.it`») diventa «spegne PATS tranne il feed del calendario dei trainer, che resta fino all'iCal del nucleo
  in M6» (n.14); le fasi sono A0–A12 di `08-piano-implementazione-m3.md`.
- **§15.7**: il dump di PATS c'è (12 settembre 2026, fuori dal repository); il significato dei codici resta da ottenere.
  **§15.9** resta aperta.
