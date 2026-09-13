# Staccarsi da vIPI: l'hub non monta, non consuma e non rimanda a vIPI se non con un link

**Data:** 13 settembre 2026 — portata da Carmine dopo essersi confrontato con lo staff di IVAO
(«dobbiamo, per ora, scollegarci dal sito delle vIPI»)
**Stato:** **decisa** (Carmine, 13 settembre 2026), sulle due domande di §3
**Regola applicata:** `CLAUDE.md` §5, caso **(c)**: toglie un milestone, cambia il catalogo dei moduli
obbligatori e smonta metà di una fase costruita (G14).
**Sostituisce:** `2026-09-07-vipi-dentro-l-hub.md`, che passa a **sospesa**; la regola «confine netto
con vIPI» di piano §9.4.

## 1. Che cosa cambia

- **vIPI esce dalla roadmap.** M5 («vIPI dentro l'hub») non ha più una data né un posto nell'ordine
  dei milestone: rientra solo se Carmine lo ripropone, come il test system in §9.6. La nota del 7
  settembre resta scritta — il lavoro di lettura dei due repository vale ancora — ma è sospesa.
- **Le prime versioni del sito hanno un link ad `atc.it.ivao.aero`**, e basta. Il link è una **voce
  di menu**, cioè una riga della tabella del menu (caso **(a)**): nessun codice, nessuna rotta
  riservata, nessun proxy su `/services/vsop`.
- **Nessuna API di vIPI è consumata dall'hub**: né le statistiche ATC in dashboard, né le sezioni
  piloti delle vSOP in `/pilots`, né le posizioni note dalle SOP per Events. Quando i moduli che le
  avrebbero usate avranno il loro design, ci penseranno lì, partendo da `ref_`.
- **I documenti dell'hub sono di qualsiasi natura.** Cade la regola pratica «se ha una FIR o un
  aeroporto come soggetto ed è operativo, è vIPI; altrimenti è un documento dell'hub»: un documento
  non sa di ATC, di aeroporti o di posizioni. Un dipartimento che vuole pubblicare una procedura la
  pubblica come qualsiasi altro documento; se la divisione preferisce tenerla in vIPI, è una scelta
  di contenuto e non un confine del codice.

## 2. Perché nessun meccanismo esistente copre il vecchio stato

Non è una questione di meccanismi mancanti ma di perimetro: il piano aveva costruito tre dipendenze
verso un prodotto esterno (montaggio, API, confine documentale) e un modulo obbligatorio che esisteva
per ospitarle. Staccarsi vuol dire togliere, e togliere tocca il catalogo (§9.2), il modello dei
documenti (§9.4), i contratti tra moduli (§9.7) e la roadmap (§13).

## 3. Le due decisioni

1. **Il modulo `atc` esce dai moduli obbligatori e rinasce opzionale.** Esisteva per le card e i deep
   link verso vIPI, per le statistiche via API vIPI e per tenere le rotte del montaggio fuori dal
   fallback della SPA; senza vIPI sarebbe una scatola vuota, e `/atc` è già una pagina di sistema
   come le altre. I moduli obbligatori diventano **tre**: `events`, `flightops`, `training`. Un
   modulo `atc` nascerà **opzionale** (`modules.atc` in `division.json`, come `specialops`) il
   giorno in cui l'ATC avrà logica che nessun altro dipartimento ha — e avrà allora il suo design.
   Il progetto `IvaoHub.Modules.Atc` e `web/src/modules/atc/` si tolgono nella fase che applica
   questa nota; la pagina `/atc` resta, come riga di `cms_contents`.
2. **La G14 tiene ciò che è generico e perde ciò che è ATC.**

   | Pezzo della G14 | Destino | Perché |
   |---|---|---|
   | `RetiredAt` + `SupersededById` (archiviato / sostituito dal successore) | **resta** | vale per un regolamento, una policy, una guida |
   | `EffectiveOn` («in vigore dal») e `ReviewOn` con l'avviso al dipartimento | **resta** | idem |
   | piè di pagina con versione, data, chi ha pubblicato, stampa (`ShowFooter`) | **resta** | idem |
   | blocchi `frequencyTable` e `coordination` | **restano** | un blocco non è un tipo di documento; chi non li usa non li vede |
   | `DocumentType { Sop, Loa }` | **via** | nomina l'ATC nel nucleo; il «tipo» di un documento è la sua **categoria**, che è un dato per dipartimento |
   | `PrimaryPosition`, `SecondaryPosition`, `Icao`, `Fir` e il loro validatore contro `ref_` | **via** | sono i campi di un documento di controllori |
   | `Airac` sulla pubblicazione e sul piè di pagina | **via** | idem |

   **Come si toglie**: la pila di PR #59–#64 si mergia com'è, verde, e la rimozione è una fase a sé
   dopo il merge. Le migrazioni sono solo additive (piano §11.3): la release che smette di usare le
   colonne le lascia nel database, la successiva le toglie (expand/contract). Riaprire il fondo della
   pila costerebbe sei rebase per un risultato identico.

## 4. Che cosa si tocca

- **Piano**: §1.1 punto 1, §2.2 (riga di vIPI), il diagramma di §3.1, §4.1 (`modules`), §5.1, §7
  (lo schema del modulo `atc`), §8.2 (sitemap: `/atc` e `/services/vsop`), §9.0, §9.1, §9.2 riga 4,
  §9.3, §9.4, §9.6, §9.7, §12 punto 6, §13 (M5), §14, §15 punti 1 e 2.
- **`CLAUDE.md`** §8 (la catena dei milestone).
- **Codice**, nella fase che applica la nota: rimozione di `IvaoHub.Modules.Atc` e di
  `web/src/modules/atc/`; rimozione dei campi ATC della G14 dal DTO, dal form, dal validatore e dal
  piè di pagina; migrazione di contract nella release successiva.
