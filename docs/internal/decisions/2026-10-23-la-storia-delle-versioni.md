# La storia delle versioni

**Data:** 23 ottobre 2026 — fase di test dopo M4 (nota `2026-10-18-la-fase-di-test.md`), fase 1 del piano dopo la prova dello staff;
issue #352, PR del nucleo `feat/content-version-history`.
**Stato:** **decisa**: Carmine l'ha approvata con il piano della nota `2026-10-22-il-piano-dopo-la-prova-dello-staff` (§2, **G2**;
§7.6, «l'ordine del §5: approvato»). Questa nota scrive come è fatta.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: si usano il confronto della revisione e la lettura dei nomi che ci sono; una
lettura scritta a mano in più, contata.

## 1. Che cosa serve

La riga «che cosa è cambiato» chiesta a chi pubblica si salva (`cms_content_versions.changelog`), ma nessuna schermata la mostrava e
il campo ripartiva vuoto a ogni pubblicazione. Servono:

1. **la storia delle versioni** nel pannello del contenuto: numero, data, chi l'ha pubblicata (e chi l'ha approvata, dove il tipo
   passa dall'approvazione), la riga;
2. **il campo del dialog precompilato** con un riassunto di che cosa è cambiato rispetto alla versione online — sezioni e blocchi
   aggiunti, tolti, modificati, e i campi intorno al corpo —, che chi pubblica può riscrivere.

## 2. Come è fatta

- **La storia**: `GET /api/content/{id}/versions` (`ContentVersionDto`), dietro `Content.View` sulla riga chiesto all'**unico
  handler**, come `/review` e `/appears-in`. Dalla più nuova, senza il corpo; i nomi da `MemberNames.ReadAsync`, e nella pagina
  `personName` (una persona cancellata è «Persona cancellata»). Nel pannello delle proprietà della riga, sotto l'indirizzo
  (`VersionHistory`, composto da Atmosphere: nessun componente nuovo dell'elenco chiuso). Una riga mai pubblicata e un template non
  la disegnano.
- **Il riassunto si calcola sul server, e il browser lo scrive in parole.** Il confronto c'era già: è quello che legge chi approva
  (`ContentReviewService.DescribeAsync`, sezioni per chiave, senza il `frozen` che la pubblicazione scrive in ogni blocco).
  `ContentReviewDto` porta ora anche i **blocchi** (per identificativo, ovunque stiano: aggiunti, tolti, modificati) e i **campi**
  della versione cambiati (titolo, riassunto, SEO, collezioni, copertina, file, piè di pagina — nota
  `2026-10-21-i-campi-fuori-dalla-versione`). Il browser (`changeSummary`) ne fa una frase nella lingua dello schermo, al più 512
  caratteri.
  **Perché sul server e non nel browser:** il browser non ha la versione online con i suoi campi (la lettura pubblica è un'altra
  cosa, e per una riga riservata non c'è), e il confronto avrebbe dovuto riscrivere in TypeScript quello che il server già fa — le
  chiavi delle sezioni, il `frozen` da togliere —: la regola scritta due volte (`CLAUDE.md` §2). Così si scrive meno, e chi approva e
  chi pubblica leggono lo stesso confronto.
- **Il dialog**: all'apertura salva la bozza (il confronto è sulla riga salvata), chiede `/review` e scrive la frase nel campo — solo
  se il campo è vuoto o ha ancora la frase scritta da sé l'ultima volta: quello che la persona ha scritto resta suo. Dopo una
  pubblicazione il campo torna vuoto, e la prossima ha la sua riga. Il dialog dell'approvazione fa lo stesso con il riepilogo che ha
  già. Il campo è un `Textarea` di tre righe: il riassunto non sta in una riga sola.

## 3. Che cosa non fa

- **Chi ha scritto una pagina approvata non è nella versione.** `ApprovedBy` e `PublishedBy` di una versione approvata sono la stessa
  persona (chi approva pubblica); chi l'aveva segnata pronta (`ReadyBy`) si azzera all'approvazione. La storia dice quindi
  «Approvata e pubblicata da …». Tenere l'autore vorrebbe una colonna nuova di `cms_content_versions` (migrazione additiva): non
  fatto, e se serve è una domanda per Carmine.
- Nessun confronto fra due versioni qualsiasi né un «torna a questa versione»: la storia si legge e basta.

## Da portare nel piano

- **§9.3** (modello dei contenuti) e **design M0 §5.5**: la riga di una pubblicazione si vede nella storia delle versioni
  dell'editor, e il dialog la propone dal confronto con la versione online; `ContentReviewDto` porta blocchi e campi cambiati.
- **§16.6**: una lettura scritta a mano in più nel CMS, contata (`/api/content/{id}/versions`).
- **Nota `2026-10-22-il-piano-dopo-la-prova-dello-staff`** §2, G2: fatta (issue #352); l'autore di una pagina approvata resta fuori
  dalla versione (§3 qui sopra).
