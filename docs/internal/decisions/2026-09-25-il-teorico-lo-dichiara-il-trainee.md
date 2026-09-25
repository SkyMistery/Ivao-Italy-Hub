# Il teorico lo dichiara il trainee, e il sito dell'esame è un'impostazione

**Data:** 25 settembre 2026 — fase A0 di M3
**Stato:** **decisa** (Carmine, 25 settembre 2026, sulla PR #121: la n.15 nel [secondo giro][r2], come raccomandato; la n.12 nelle
[risposte alle domande di §12][r1]). **È uno scostamento dal piano** (§9.2, §14), che il revisore registra dopo il merge. Dove vive
`ITheoryExamSource` (§2, ultimo punto) è una scelta tecnica di Claude, da confermare nella revisione della PR di A0.
**Regola applicata:** `CLAUDE.md` §5, caso **(c)**: una funzione del modulo che il piano immaginava diversa; e `CLAUDE.md` §3
(«does this name IVAO?») per il sito dell'esame. Design `07-design-m3.md` §0.2, §0.6, §1.6, §2.2, §2.3, §12 n.12 e n.15.

[r1]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/121#issuecomment-5832705237
[r2]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/121#issuecomment-5832839987

## 1. Che cosa serviva

- **Prima di un training il trainee deve aver superato l'esame teorico** di quel rating, su IVAO (R.2, d1). L'hub non lo può
  verificare: lo scope `training` dell'API non c'è (piano §14), e la documentazione pubblica dell'API, vista il 25 settembre 2026,
  non ha endpoint di training né di esami (design §0.2).
- **Il piano** (§9.2, riga Training; §14, il rischio dello scope `training`) prevedeva che gli esiti li inserisse **lo staff**, a
  mano o da CSV, dietro un'astrazione `ITheoryExamSource` da sostituire quando l'API arriva.
- **Il TD chiede un'altra cosa** (d1), che è come fa PATS oggi: alla richiesta il trainee risponde alla domanda «hai superato
  l'esame teorico per *rating*?»; con «no» la richiesta è rifiutata in automatico e resta registrata; chi approva ha il promemoria
  di controllare su IVAO.
- La domanda e il promemoria portano il **link al sito dell'esame**. Scritto nel codice, il modulo nominerebbe IVAO (`CLAUDE.md`
  §3), e un fork mostrerebbe il sito di un altro.

## 2. Le decisioni

1. **Il teorico lo dichiara il trainee** (n.15), dietro **`ITheoryExamSource`**, come il piano chiede. L'implementazione di oggi è
   «chiedilo al trainee»: la domanda compare al clic su «Richiedi training»; **no** → la richiesta si registra `Rejected` con
   `TheoryNotPassed`, il messaggio a schermo lo dice e **nessuna mail** parte (d4); **sì** → `Requested`, `theory_confirmed_at`,
   mail `requestReceived`. **Chi approva lo controlla su IVAO**: la pagina dello staff mostra il promemoria in evidenza. Quando
   IVAO esporrà gli esiti, un'altra implementazione della stessa interfaccia li leggerà e la domanda sparirà, senza toccare il
   resto del flusso.
2. **Il sito dell'esame è un'impostazione del modulo** (n.12): `theoryExamUrl`, nel testo della domanda e nel promemoria. Il
   predefinito è vuoto, come ogni predefinito che non conosce la divisione (test «XX»); chi forka mette il suo.
3. **Dove vive `ITheoryExamSource`** (scelta tecnica): **nel modulo**. Il design non la mette fra le estensioni del nucleo (§8), e
   l'unica implementazione di oggi — la dichiarazione — è logica del modulo. Il giorno in cui IVAO espone gli esiti, leggerli è
   del perimetro IVAO del nucleo (un modulo non parla con IVAO, `CLAUDE.md` §3): quella fase dirà con una nota sua come il modulo
   li riceve.

## 3. Il costo, accettato

Un trainee può rispondere «sì» senza aver fatto l'esame. Se ne accorge chi approva, che lo controlla su IVAO prima di accettare:
la verifica resta umana dove c'è già, e lo staff non deve inserire esiti che vede su IVAO uno per uno.

## 4. Alternative scartate

| Alternativa | Perché no |
|---|---|
| La strada del piano: lo staff inserisce l'esito, a mano o da CSV, prima che il trainee possa chiedere | sposta il lavoro sullo staff, che gli esiti non li ha (li vede su IVAO, uno per uno), e blocca la richiesta finché qualcuno non l'ha fatto |
| Il link al sito dell'esame scritto nel modulo | il modulo nominerebbe IVAO, e un fork erediterebbe il sito di un'altra divisione |

## 5. Che cosa si tocca

Solo il modulo: `theoryExamUrl` nelle impostazioni (**A4**); `ITheoryExamSource` con la dichiarazione, la domanda con il link e il
rifiuto automatico registrato (**A6**); il promemoria nella pagina dello staff (**A7**).

## Da portare nel piano

- **§9.2, riga Training** (le dipendenze: «input manuale/CSV, `ITheoryExamSource`»): il teorico lo **dichiara il trainee** alla
  richiesta, dietro `ITheoryExamSource`, e chi approva lo controlla su IVAO; niente esiti inseriti dallo staff.
- **§14, il rischio dello scope `training`**: la mitigazione diventa la dichiarazione del trainee con il controllo di chi approva,
  dietro `ITheoryExamSource`, da sostituire quando l'API esporrà gli esiti.
