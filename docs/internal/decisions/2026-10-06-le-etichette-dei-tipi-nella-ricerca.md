# Le etichette dei tipi nella ricerca: la parola di un modulo sta nel modulo

**Data:** 6 ottobre 2026 — correzione del nucleo, sessione di lavoro di Carmine, ramo `fix/search-kind-labels`
**Stato:** **Decisa** da Carmine il 6 ottobre 2026, **come raccomandato**: «Nel modulo», risposta data in chat alla sessione di lavoro
(§5). La domanda non è passata da un commento su una PR perché la sessione è del maintainer: non c'è un link, c'è questa riga.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: il meccanismo c'è — le parole di un modulo stanno nel suo file di lingua, sotto il
suo namespace, e il nucleo le chiede con `<modulo>:<chiave>` (piano §16 punto 8, nota `2026-09-26-le-parole-di-piu-moduli`; la barra
dello staff intitola così la sezione di un modulo, `t('<modulo>:nav.section')`) — e non copre un caso: il cartellino del tipo nei
risultati della ricerca.

## 1. Che cosa è successo

- Nei risultati della ricerca (`web/src/features/search/SearchResults.tsx`) il cartellino del tipo è
  `t('search.kinds.<kind>', { defaultValue: kind })`, e `search.kinds` in `locales/*/common.json` ha solo i tipi del nucleo: `page`,
  `news`, `document`, `dashboard`, `link`.
- I tour proiettano nella ricerca con il tipo `tour` (`Tour.cs`): il loro cartellino mostra la chiave
  nuda, «tour», in ogni lingua. Gli eventi, con la #221 (E3b), proiettano con `events`: stesso difetto.
- Lo ha trovato il revisore sulla #221 ([rilievo 3][r221]); Carmine ha chiesto al master di correggerlo nel nucleo.

[r221]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/221#issuecomment-6012258987

## 2. Che cosa serve

Che il cartellino di una riga proiettata da un modulo abbia una parola in ogni lingua, **senza** che il nucleo nomini il modulo
(`CLAUDE.md` §2: «The core never references a module») e senza che un modulo nuovo chieda un tocco ai file di lingua del nucleo.

## 3. La proposta (raccomandata)

1. **La riga dice già chi l'ha proiettata**: `SearchHitDto.sourceModule` è già nella risposta di `/api/search` (`core` per le righe
   editoriali, la chiave del modulo per le altre). **Nessun cambio dell'API né del back end.**
2. **Il cartellino chiede la parola a chi ha proiettato la riga**: per `core`, `search.kinds.<kind>` come oggi; per un modulo,
   `<modulo>:search.kinds.<kind>`, nel file di lingua del modulo. È la convenzione di `<modulo>:nav.section`, con un'altra chiave. I
   namespace dei moduli sono già caricati tutti all'avvio (`createI18n(registry.i18nNamespaces)`), anche nelle pagine pubbliche.
3. **Ogni modulo scrive la sua**: i tour `search.kinds.tour` in `web/src/modules/flightops/locales/{it,en}/flightops.json`, in questa
   PR. Gli eventi `search.kinds.events` in `web/src/modules/events/locales/{it,en}/events.json`: anche questa in questa PR (§5, in fondo).
4. **Un test che non lascia dimenticare il prossimo**: `web/src/modules/manifest.test.ts` legge già i sorgenti C# dei moduli; cerca
   ogni `new SearchProjection(` di un modulo, ne legge il tipo (un letterale, o una costante del modulo seguita fino al letterale) e
   chiede che `search.kinds.<tipo>` esista nel file di lingua del modulo, in ogni lingua della divisione. Un tipo che il test non sa
   leggere lo fa cadere, con la frase che dice come scriverlo.
5. Il `defaultValue` resta: una riga di un modulo che questa build non conosce mostra la chiave e non niente.

## 4. Alternative scartate

| Alternativa | Perché no |
|---|---|
| `tour` ed `events` in `search.kinds` di `common.json` | due righe, ma il file del nucleo nomina i moduli, ogni modulo nuovo tocca il nucleo (PR a parte, con nota), e un fork che toglie un modulo si tiene le sue parole |
| L'etichetta nella proiezione (`SearchProjection.Label`, `Localized<string>`), salvata nell'indice | una colonna nuova in `cms_search_index` e una migrazione per una parola che è dell'interfaccia; cambiarla chiederebbe di riproiettare ogni riga |
| I tipi della ricerca in `/api/me`, come i tipi del calendario | quelli sono righe che lo staff modifica (`cms_calendar_kinds`); questi sono parole fisse del codice, e il loro posto sono i file di lingua |

## 5. La domanda per Carmine

**La parola del cartellino di una riga di un modulo sta nel file di lingua del modulo, chiesta come `<modulo>:search.kinds.<tipo>`
(§3), o nel `common.json` del nucleo?** **Raccomandata: nel modulo.** Costa quanto l'altra (una riga cambiata in `SearchResults.tsx`,
nessun cambio del back end), il nucleo non nomina nessuno, e il test del §3 punto 4 vale per ogni modulo che verrà. Con l'altra strada
questa PR aggiunge due righe a `common.json` e gli eventi non devono fare niente.

**Risposta di Carmine, 6 ottobre 2026** (in chat): **nel modulo**, come al §3.

**La riga degli eventi è in questa PR** (6 ottobre 2026). La #221 è entrata in `main` prima di questa, senza la parola: il test del §3
punto 4, messo in pari con `main`, è caduto sugli eventi come doveva (`Event.SearchKind`, cioè `events`). Carmine, in chat, ha detto
alla sessione di aggiungere lei `search.kinds.events` («Event», «Evento») nei file di lingua del modulo degli eventi: è la sola riga
di questa PR in un modulo del collaboratore, e il master lo avvisa sulla #223, che tocca gli stessi file.

## 6. Che cosa si tocca

- `web/src/features/search/SearchResults.tsx` (nucleo) e il suo test;
- `web/src/modules/events/locales/{it,en}/events.json` (modulo del collaboratore: una chiave, su indicazione di Carmine);
- `web/src/modules/flightops/locales/{it,en}/flightops.json` e le copie in `locales/` (`pnpm i18n:sync`);
- `web/src/modules/manifest.test.ts` (nucleo): il test del §3 punto 4;
- `Directory.Build.props`: 0.6.4.

## Da portare nel piano

- **§16 punto 8** (un solo set di file di lingua): il cartellino del tipo nei risultati della ricerca è `search.kinds.<tipo>` per le
  righe del nucleo e `<modulo>:search.kinds.<tipo>` per quelle di un modulo, con `sourceModule` della riga a dire quale.
- **`CONTRIBUTING.md`**, dove si elenca che cosa porta un modulo: un modulo che proietta nella ricerca dichiara
  `search.kinds.<tipo>` nel suo file di lingua; `manifest.test.ts` cade se manca.
