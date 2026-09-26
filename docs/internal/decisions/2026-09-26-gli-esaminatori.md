# Gli esaminatori: HQ, TC, TAC e i TA, mai i trainer

**Data:** 26 settembre 2026, dopo il merge di A3 (#131)
**Stato:** **decisa** (Carmine, 26 settembre 2026, in chat al revisore; [commento sulla #131][c]). Corregge la decisione n.10 del
design (`07-design-m3.md` §12, [risposte sulla #121][r1]) e il punto 4 della nota `2026-09-25-chi-conduce-e-chi-scrive-un-training`,
che è unita e non si modifica: vale questa.
**Regola applicata:** `CLAUDE.md` §5, caso **(a)**: cambia chi riceve un permesso del modulo (`division.json → positionGrants`), non
un meccanismo.

[c]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/131#issuecomment-5844102750
[r1]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/121#issuecomment-5832705237

## 1. Che cosa è cambiato

- **Il design** (R.7, d4) diceva che un esame lo inserisce «chi ha l'esame assegnato», e §2.8 e §3.4 ne ricavavano «anche un TA o un
  trainer». Per questo la decisione n.10 dava `Training.ManageExams` a **tutto** lo staff del training, trainer compresi.
- **Il TD ha precisato** (`dalberone`, 26 settembre 2026, in `08` A3b punto 3): per regolamento un esame si assegna **solo a un
  esaminatore**, e gli esaminatori sono **HQ, TC, TAC e i TA (TA1–9)**. **Mai i trainer (T01–T99).**
- **Carmine l'ha confermato** il 26 settembre: «gli esami si assegnano solo a HQ, TC, TAC e TA, mai ai trainer».

## 2. La decisione

- **`Training.ManageExams` va a TC, TAC e TA1–9**, per posizione (`positionGrants` del TD, scope TD). **I trainer non lo hanno.** HQ
  (DIR, ADIR) e il web lo hanno dal nucleo, come ogni permesso del modulo.
- **Il resto non cambia**:
  - la creazione passa il guardiano con `AlsoOnCreation` (A3);
  - cambiare e togliere un esame resta a HQ, TC, TAC e al TA a cui è assegnato (risposta 4 sulla #131, fase A3b).

  Con questa correzione, «il TA a cui è assegnato» è l'unico caso che la regola di A3b deve coprire.

## 3. Che cosa si tocca

- **A4**: i `positionGrants` del TD in `config/division.json` e `division.example.json`. I trainer ricevono `Training.View` e basta
  (la conduzione arriva con il grant con scope, n.1).
- **A10**: i test e il «fatta quando» degli esami parlano di un TA, non di un trainer.
- **I documenti di M3** (`07-design-m3.md` §2.8, §3.2, §3.4, §10, §12 n.10; `08-piano-implementazione-m3.md` A3b, A4, A10): allineati
  nella stessa PR di questa nota, dalla sessione del maintainer, con un rimando qui.

## Da portare nel piano

- **§9.2, riga Training**: un esame lo inseriscono HQ, TC, TAC e i TA, mai i trainer; lo cambiano e lo tolgono HQ, TC, TAC e il TA a
  cui è assegnato. Portato nella stessa PR, perché la scrive la sessione di Carmine.
