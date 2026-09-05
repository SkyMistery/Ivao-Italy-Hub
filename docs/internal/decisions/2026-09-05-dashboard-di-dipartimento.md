# La dashboard di dipartimento

**Data:** 5 settembre 2026 — chiesta da Carmine aprendo M1
**Stato:** **decisa nel principio, da confermare nella forma** (§«La domanda che resta»)
**Regola applicata:** `CLAUDE.md` §5, caso **(c)**: funzione nuova, non prevista dai documenti. Ci si
ferma, si scrive, si decide, poi si codifica.

## Cosa serve

Ogni dipartimento nasce con una **propria dashboard di default**, che poi il dipartimento può
modificare.

## Che cosa esiste oggi, verificato

- **La dashboard personale `/me`** compone i widget che `/api/me` dichiara (`WidgetRegistry`,
  `welcome` è l'unico del nucleo). È di una **persona**, non di un dipartimento.
- **`WidgetDescriptor` porta già un `Department?` opzionale** — il contratto lo prevede, nessuno lo
  usa.
- **`/staff/{dept}` non esiste come schermata.** Le rotte di dipartimento sono
  `staff.$dept.content.*` e `staff.$dept.links.*`; non c'è una pagina di ingresso, la sidebar porta
  direttamente alle liste.
- Il piano §8.2 conosce `/staff/{dept}/**` come «spazio del dipartimento», senza dire che cosa si
  veda arrivandoci. **La dashboard di dipartimento non è in nessun documento**: né nel piano, né nel
  design di M0, né in quello di M1.

## Il bivio vero: widget o blocchi

| | Widget | Blocchi (raccomandata) |
|---|---|---|
| Che cos'è | un insieme di tile registrate, disposte per dipartimento | una riga di `cms_contents` con `Visibility.Department`, nata da un template |
| «Di default» | una disposizione seminata | il **seed**, la stessa macchina delle pagine di sistema (design M1 §8.2) |
| «Editabile» | serve un editor di disposizione: **un secondo editor** accanto a quello dei blocchi | l'editor che esiste, senza una riga nuova |
| Cosa mostra | tile registrate dai moduli | i blocchi Data che G4 porta (`calendar`, `newsList`, `documentList`, `stats`) più il testo che il dipartimento vuole |
| Costo | un meccanismo nuovo | una riga di seed per dipartimento e una rotta |
| Contro | `CLAUDE.md` §2: due modi di comporre una schermata editoriale | il piano §9.7 chiama «dashboard» la composizione di widget, e qui la parola prende un secondo significato |

**Raccomandazione: blocchi.** La dashboard di un dipartimento è un contenuto che il dipartimento
scrive, e questo progetto ha già un meccanismo per i contenuti che qualcuno scrive. I widget restano
quello che sono — le tile della dashboard **personale**, dove la composizione è per persona e non
editoriale — e i moduli continueranno a registrarli per `/me`.

## Come sarebbe fatta, in concreto

- **Una riga per dipartimento** in `cms_contents`, `kind = Dashboard` (valore nuovo in fondo
  all'enum, additivo), `slug` = il codice del dipartimento minuscolo, `owner_department` = suo,
  `visibility = Department`. L'unicità è già `(kind, slug, is_template)`, quindi nove righe
  convivono senza toccare l'indice.
- **Il seed** applica una volta per dipartimento, con la stessa chiave in `hub_division_settings` che
  `ContentTemplateSeeder` usa già (`page.dashboard:<dept>`): un dipartimento nuovo nell'enum riceve
  la sua al primo avvio successivo, e quella che lo staff ha già modificato non viene toccata.
- **Nasce da un template di sistema** (`dashboard`, seminato in WD). È il primo cliente vero della
  decisione sui template (`2026-09-05-template-di-sistema-e-dipartimenti.md`): senza lettura
  condivisa, otto dipartimenti su nove non potrebbero nemmeno leggere il proprio template di
  partenza, e G11 non potrebbe dire loro che è cambiato.
- **`Url`** di una riga `Dashboard` è `/staff/{dept}` e non un indirizzo pubblico; la rotta pubblica
  `/{slug}` continua a servire solo `kind = Page`, quindi non c'è collisione.
- **Pubblicare** conserva il suo significato: la bozza è quella che il coordinatore sta scrivendo, la
  versione pubblicata è quella che il dipartimento vede. Non diventa pubblica: la visibilità è
  `Department` e il query filter fa il resto.
- **Permessi invariati**: leggerla è `Content.View` sul proprio dipartimento, modificarla
  `Content.Edit`. Nessun permesso nuovo, nessun handler nuovo.

## Dove va nel lavoro

**G8**, che è già la fase del seed delle pagine di sistema e del menu: aggiungere nove righe seminate
e una rotta è un delta piccolo sopra una macchina che quella fase costruisce comunque. Prima di G3 e
G4 non avrebbe blocchi da mostrare.

Se invece si sceglie la strada dei widget, **non è una fase di M1**: è un meccanismo nuovo, va
progettato, e la sua casa naturale è M2, quando i moduli cominciano a registrare tile.

## La domanda che resta

1. **Blocchi o widget?** (raccomandato: blocchi)
2. La dashboard la vede **solo il proprio dipartimento** (raccomandato) o qualunque staff?
3. Entra in **M1/G8** (raccomandato, se blocchi) o slitta a M2?
