# I tipi che il form di un evento offre (E4)

**Data:** 6 ottobre 2026 — fase E4 di M4 (il pubblico e le rotte), PR #223
**Stato:** **Proposta**. La domanda va a Carmine con un commento sulla PR — la terza, accanto alle due di
`2026-10-06-il-pubblico-degli-eventi` e `2026-10-06-chi-e-online-sugli-scali-di-un-evento` —, e la risposta, con il suo link, si
scrive qui sotto. **Il codice non cambia finché non risponde.**
**Regola applicata:** `CLAUDE.md` §5. Le note già unite leggono la riga del design nell'altro modo — il form sceglie fra **tutti** i
tipi del calendario —, quindi restringere la scelta è una domanda al maintainer, non un caso (a); e nessun dato, così com'è, dice quali
tipi sono degli eventi (§2). Design `09-design-m4.md` §1.2, §1.12, §12; note `2026-09-29-i-tipi-di-evento`,
`2026-09-30-i-tipi-degli-eventi-e-l-ed-sul-banco`, `2026-10-01-la-lettura-dei-preset-dei-tipi`.

## 1. Che cosa si è visto

- Sul banco di prova (l'hub pubblicato di `ff8ff9f`, 6 ottobre 2026) dalberone ha aperto il form di un evento: **«Tipo di evento»
  offre ogni tipo del calendario** — Evento, RFE, RFO, MSE, Online Day, e anche Training, Esame, Tour, Riunione, Scadenza. Un evento di
  tipo «Esame» o «Scadenza» non ha senso: la nota `i-tipi-di-evento` §2.1 dice che «training ed esami non sono eventi: sono di M3».
- **È quello che dicono le note unite.** Il form «sceglie `kind` fra i tipi del bootstrap» (nota di E1, §1; nota `i-tipi-di-evento`
  §4, E3a), e l'aiuto delle impostazioni dice che un tipo senza riga di preset si sceglie lo stesso: «Nessuna riga: un evento nuovo nasce
  con tutto spento». Il server accetta ogni tipo attivo del calendario: `errors.calendar.kindUnknown` solo per uno che non c'è, e solo su
  un evento nuovo o quando il tipo cambia (`Staff/EventSaving.cs`).

## 2. Che cosa c'è già, e perché da solo non basta

- **I tipi del calendario** (`cms_calendar_kinds`: chiave, etichetta, colore, ordine) non dicono di chi è un tipo. Il colore li
  raggruppa — i cinque degli eventi sono blu (nota di E1, §2.1) —, ma è una scelta grafica che la divisione cambia dal back office:
  leggerlo come «è un evento» sarebbe un significato nascosto in un colore.
- **`kindPresets`** (le impostazioni del modulo, E2) ha una riga per tipo con i suoi interruttori, sotto il titolo «Che cosa accende ogni
  tipo di evento». Ma oggi una riga **non** dice «questo tipo è di un evento»: un tipo senza riga si sceglie lo stesso, e il design
  (§1.12) dà a IT le righe di RFE, RFO, MSE e Online Day, non quella dell'evento libero, che non accende niente.
- Il form legge già i preset (`GET /api/events/kind-presets`, accettata da Carmine sulla #214) per preimpostare gli interruttori: la
  strada A qui sotto non chiede una lettura nuova.

## 3. Le strade

| | Strada | Che cosa cambia | Pro | Contro |
|---|---|---|---|---|
| **A** | **Le righe di `kindPresets` sono i tipi degli eventi** — **raccomandata** | il form offre i tipi che hanno una riga; **nessuna riga** (un fork appena nato, un'installazione che non le ha scritte): ogni tipo del calendario, come oggi. Il server rifiuta un tipo fuori dall'elenco su un evento nuovo o quando il tipo cambia, e tiene com'è quello di un evento già scritto, come fa con un tipo tolto dal calendario. IT scrive una riga anche per l'evento libero, con tutto spento: un dato | nessuna impostazione nuova, niente nucleo; la schermata dice già «ogni tipo di evento»; un fork nuovo non resta con la scelta vuota | una riga prende un secondo significato; ⚠️ la prima riga scritta restringe la scelta a quel tipo finché non si scrivono le altre: l'aiuto delle impostazioni lo dice |
| B | Un'impostazione nuova, `eventKinds` (i tipi che un evento sceglie; vuota: tutti) | una lista accanto a `kindPresets` | un significato esplicito | due liste di tipi nelle stesse impostazioni, da tenere d'accordo |
| C | Un segno sui tipi del calendario (di quale modulo è un tipo), nel nucleo | una colonna di `cms_calendar_kinds` e il suo campo nel back office | un posto solo, anche per gli altri moduli (Training potrebbe offrire solo i suoi) | una fase del nucleo con la sua nota, per un form |
| D | Lasciare com'è | niente | quello che dicono le note unite | lo staff vede Esame e Scadenza fra i tipi di un evento |

⚠️ Con A, **sul banco e sulla prova la scelta resta com'è** finché le impostazioni degli eventi non hanno righe: le scrive la divisione
(o una spec), non il codice — nessun tipo nel codice, test «XX».

## 4. La domanda a Carmine

> Il form di un evento offre ogni tipo del calendario, anche Training, Esame, Tour, Riunione e Scadenza, come dicono le note di E1 ed
> E3a. Proposta (A): i tipi di un evento sono quelli che hanno una riga in «Che cosa accende ogni tipo di evento» (`kindPresets`); senza
> nessuna riga, ogni tipo del calendario, come oggi; il server rifiuta un tipo fuori dall'elenco su un evento nuovo o quando il tipo
> cambia, e tiene quello di un evento già scritto; la divisione aggiunge una riga per l'evento libero, con tutto spento. Alternative:
> un'impostazione nuova (B), un segno sui tipi del calendario nel nucleo (C), lasciare com'è (D). Confermi A?

## 5. Che cosa si tocca, se è A

Solo il modulo, su questo branch: il controllo del tipo al salvataggio dell'evento (`Staff/EventSaving.cs`, con una chiave sua), la scelta
del tipo nel form (`schemas.ts`, `kindChoices` con i preset; `screens/events.tsx`, che i preset li legge già), l'aiuto di `kindPresets`
nelle due lingue, i test (integrazione, vitest, la spec del form). Nessun file del nucleo, nessuna migrazione, nessuna impostazione nuova.

## Da portare nel piano

Dopo la risposta: il design M4 §1.2 e §1.12 (che cosa dice una riga di `kindPresets`), letti con la nota `2026-09-29-i-tipi-di-evento`
§2.
