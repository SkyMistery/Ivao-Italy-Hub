# Le note riservate e il trainee: l'endpoint dello staff le toglie a chi legge il proprio training

**Data:** 25 settembre 2026 — fase A0 di M3
**Stato:** **decisa** (Carmine, 25 settembre 2026, sulla PR #121, [risposte alle domande di §12][r1], n.13): l'endpoint dello
staff toglie le note riservate quando chi legge è il trainee della riga; e poiché è una regola su chi legge che cosa, scritta a
mano, ha **una nota sua e un test d'integrazione**. Questa è quella nota. La forma qui sotto (una funzione sola, dove, che cosa
toglie) è di Claude, da confermare nella revisione della PR di A0.
**Regola applicata:** `CLAUDE.md` §5, caso **(c)**: una regola nuova del modulo su che cosa si legge di una riga, scritta a mano
accanto all'unico handler e non dentro. È un'eccezione dichiarata, e per questo ha una nota sua e un test.
Design `07-design-m3.md` §1.1, §3.1, §4.1, §4.2, §10, §12 n.13.

[r1]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/121#issuecomment-5832705237

## 1. Il caso

- **Le note riservate** — la nota riservata di ogni voce della scheda, il commento riservato del report, gli appunti interni di
  una sessione rischedulata — le leggono lo staff e i trainer (R.1, d3), **mai il trainee** (R.5).
- **Il trainee legge il suo training dai suoi endpoint** (`/training/mine`, `/training/mine/{id}`), con un DTO che le note non le
  ha, come i PIREP di un pilota in M2.
- **Ma un trainer è anche un membro**: chiede un training per il rating successivo come chiunque. Il suo `Training.View` viene
  dalla posizione (T01–T99, TA1–9, TAC, TC), e il nucleo **non nega mai la lettura** all'interessato. Dall'endpoint dello staff —
  la pagina del training, il percorso del trainee — leggerebbe le note riservate sul **proprio** training.

## 2. Perché nessun meccanismo del nucleo basta

- **`DeniedToStakeholder`** (nota `2026-09-15-permessi-su-una-riga-e-chi-ha-interesse` §3.2) nega all'interessato i permessi che
  **scrivono**: nel catalogo del design sono segnati `Approve`, `Assign`, `Conduct`, `Edit` e `Ban`. Nessun permesso di lettura è
  segnato, di proposito; e segnare `Training.View` toglierebbe al trainer **tutta** la riga, anche la parte che può leggere, dalla
  lista dello staff e dal percorso.
- **`IHasParticipants`** fa il contrario di quello che serve: l'handler dà al partecipante il `View` dell'area sulla riga, quindi
  la risposta dello staff, note comprese, arriverebbe a **ogni** trainee. Per questo il training non lo usa (design §1.1).
- L'unico handler decide **se** una riga si legge, non **quali campi**: quale campo è riservato lo sa solo il modulo.

## 3. La decisione

**L'endpoint dello staff toglie le note riservate quando chi legge è il trainee della riga** (`trainee_vid` uguale al VID di chi
legge). È una regola sulla **forma della risposta**, non un'autorizzazione: la riga resta leggibile — il trainer vede il suo
training nella lista e nel percorso come ogni altro —, ma senza ciò che è riservato.

- **Che cosa si toglie**: la nota riservata di ogni voce della scheda (`trn_evaluations`), il commento riservato del report
  (`staff_comment`), gli appunti interni delle sessioni (`trn_sessions`), e ogni campo riservato che una fase aggiunga dopo.
  Restano voti, spunte, commenti per il trainee e commento generale: ciò che il trainee legge comunque dai suoi endpoint.
- **Dove**: **una funzione sola** del modulo, che costruisce la risposta dello staff di un training e la passa per la regola.
  Ogni endpoint dello staff che porta campi riservati la usa — la pagina `/staff/training/{id}` e il percorso
  `/staff/training/trainees/{vid}` —, e nessuno ne scrive una copia. Una fase che aggiunge un campo riservato lo aggiunge lì.
- **Per chiunque**: vale anche per TC, TAC, HQ, il web e il superadmin quando sono il trainee della riga, come il no
  dell'interessato nell'handler.
- **Le scritture non cambiano**: il trainee non approva, non assegna, non conduce e non banna il proprio training già per il
  catalogo (`DeniedToStakeholder`); questa nota riguarda solo ciò che legge.

## 4. Il test d'integrazione

Nella fase che fa nascere i campi riservati (**A9**: appunti della rischedula, scheda, report), allargato in **A10** al percorso
del trainee (design §10):

- un trainer che è anche trainee legge dall'endpoint dello staff il **proprio** training **senza** note riservate, appunti e
  commento riservato, e il training di un altro **con**;
- lo stesso sul percorso del trainee;
- il test elenca i campi tolti, così un campo riservato nuovo che la funzione dimentica si vede nella revisione.

## 5. Che cosa non copre, detto

- **L'audit del nucleo.** Il training è `[Audited]`, e le righe di `hub_audit_log` ricopiano i valori scritti, commento riservato
  compreso. Le legge chi ha `Audit.View` (un permesso globale) o il superadmin, dalla schermata dell'audit del nucleo: non è un
  endpoint del modulo, e la regola non ci arriva. È un caso stretto (un trainee che ha anche l'audit) e resta com'è; chiuderlo
  sarebbe una domanda del nucleo, non di M3.
- **Le mail** al trainee non portano campi riservati (design §5.2): non riguardano questa regola.

## 6. Alternativa scartata

| Alternativa | Perché no |
|---|---|
| Accettarlo: un trainer legge le note riservate sui propri training | le note riservate esistono per dire allo staff ciò che il trainee non legge; un trainer che è anche trainee le leggerebbe |

## 7. Che cosa si tocca

Solo il modulo: la funzione della risposta dello staff (A9, con il suo test), il percorso del trainee che la usa (A10). Nessun
file del nucleo.

## Da portare nel piano

- **§16 punto 2** (l'unico handler): un'eccezione dichiarata di M3 — una regola scritta a mano su **quali campi** si leggono di una
  riga (l'endpoint dello staff del training toglie le note riservate al trainee della riga), con questa nota e il suo test
  d'integrazione. Il rapporto di chiusura di M3 la elenca accanto agli endpoint scritti a mano (§16 punto 6).
