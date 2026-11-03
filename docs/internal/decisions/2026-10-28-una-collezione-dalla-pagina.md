# Una raccolta dalla pagina della news o del documento (#364)

**Data:** 28 ottobre 2026, fase di test, fase 2 della nota `2026-10-22-il-piano-dopo-la-prova-dello-staff` (voce e).
**Stato:** **decisa.** Che cosa: nella nota del 22 ottobre (§3, voce e, «Sì alla proposta di Carmine: si scelgono dalla pagina del
documento o della news, da un elenco, con "nuova collezione"»). Come: **Carmine in chat alla sessione di lavoro, 28 ottobre 2026**,
tra le due strade del §2: «Nel generatore».
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: si estende un meccanismo del nucleo che c'è già, il generatore dei form (il campo
`multi`, la sesta estensione, G11a).

## 1. Il problema

Il campo «Raccolte» c'era già sulla pagina di una news e di un documento (G20): le caselle delle raccolte del dipartimento. Ma una
raccolta nuova si faceva solo da `/staff/<dip>/categories`, una schermata che lo staff della prova non sapeva che esistesse: chi
scriveva un documento non trovava dove metterlo, e con nessuna raccolta il campo era un titolo senza caselle.

## 2. Le due strade

| | Che cosa | Costo |
|---|---|---|
| **A** | **Nel generatore**: un campo `multi` può dire `creatable`; sotto le caselle il generatore disegna quello che lo schermo gli dà (`createChoice`) e spunta il valore che gli torna | Un'estensione piccola del nucleo; riusabile (categorie dei link, tipi del calendario) |
| B | Un pulsante accanto al modulo, fuori dal generatore: la raccolta nasce e compare tra le caselle, ma va spuntata a mano | Nessun cambio del generatore; un clic in più, e un pezzo di modulo scritto a mano |

**Scelta A** (Carmine).

## 3. Che cosa cambia

- **Il generatore** (`shared/forms`): l'annotazione `creatable` su un campo `multi`, e la prop `createChoice(field, choose)` di
  `SchemaForm`. `choose` aggiunge il valore a quello spuntato **in quel momento** (letto allora, non al disegno), anche se la scelta
  non è ancora tra le caselle: l'elenco arriva un attimo dopo. Senza `createChoice` il campo è quello di sempre.
- ⚠️ **Un form in un dialog di un form**: il dialog è un portale, fuori dal form nel DOM ma dentro nell'albero di React, e React porta
  il `submit` lungo il suo albero. Invio nel dialog salvava anche la pagina dietro. `SchemaForm` ora ferma la propagazione del suo
  `submit` (`stopPropagation`), con un test che fallisce senza.
- **La pagina** (`features/content`, `features/categories/NewCollection.tsx`): «Nuova raccolta…» apre il `Dialog` del nucleo con il
  form generato della raccolta ridotto a nome e chiave (`newCollectionSchema`: il tipo è quello della pagina, ordine e «in uso»
  restano quelli di una raccolta nuova), la crea con la stessa chiamata della schermata delle raccolte, nel dipartimento della pagina,
  e la spunta. Il campo ha un aiuto che dice quali blocchi elencano una raccolta.
- Il server non cambia.

## 4. Che cosa resta fuori

- Le raccolte offerte sono quelle del dipartimento della pagina, come prima: una raccolta di un altro dipartimento si legge
  (`SharedForReading`) ma non si offre qui, perché la chiave non è unica fra dipartimenti.

## Da portare nel piano

- **§16** (il generatore dei form): l'ottava estensione, `creatable` e `createChoice`; e la regola del `submit` che non esce da un form
  dentro un dialog.
- **§9.3** (le raccolte, G20): una raccolta si crea anche dalla pagina della news o del documento, e la pagina ci viene messa.
