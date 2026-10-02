# La lettura dei preset dei tipi a chi scrive gli eventi (E3a)

**Data:** 1 ottobre 2026 — fase E3a di M4 (l'evento nello staff), PR #214
**Stato:** **decisa** (Carmine, 1 ottobre 2026, in chat al master e pubblicata sulla PR su sua istruzione: [la risposta][ok], autore
`SkyMistery`), alla domanda del revisore ([i rilievi sulla #214][r], punto 1): **`GET /api/events/kind-presets` resta com'è**, uno
scostamento accettato dal design §7.2.
**Regola applicata:** `CLAUDE.md` §5 (uno scostamento dal design lo decide il maintainer, e la decisione sta in una nota) e §2 (sul
server `MapCrud`, mai un CRUD scritto a mano); piano §16.6 (gli endpoint a mano contati per famiglia, ognuno con la sua decisione).
Design `09-design-m4.md` §1.12, §6.2, §7.2; nota `2026-09-29-i-tipi-di-evento` §2.2.

[ok]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/214#issuecomment-5934118725
[r]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/214#issuecomment-5929130403

## 1. Che cosa serviva decidere

- **Il form dell'evento preimposta gli interruttori quando cambia il tipo** (`10-piano-implementazione-m4.md`, E3a punto 2; design §1.12):
  il browser deve sapere che cosa preimposta ogni tipo, cioè `kindPresets` delle impostazioni del modulo, prima che l'evento sia salvato.
- **Le impostazioni di un modulo le legge solo chi le gestisce**: `ModuleSettingsEndpoints` (`/api/modules/{key}/settings`) apre a
  `Events.ManageSettings`, che tengono il coordinatore e l'assistente dell'ED; gli advisor dell'ED (EA1–9) scrivono eventi con
  `Events.Edit` e non l'hanno (design §6.2).
- **Il design §7.2 dice che gli endpoint scritti a mano sono verbi**; una lettura non lo è. E3a l'aveva scritta fra gli scostamenti
  («Com'è andata», scostamento 1) senza una nota e senza una risposta: il revisore l'ha chiesta al maintainer.

## 2. La decisione

**`GET /api/events/kind-presets` resta com'è**: una lettura scritta a mano accanto al CRUD dell'evento (`Staff/EventEndpoints.cs`),
chiesta con `Events.Edit` tenuto da qualche parte, che risponde i preset dalle impostazioni del modulo (`ModuleSettingsStore`) e non
cambia niente. **Nessun permesso di lettura si aggiunge a `ModuleSettingsDescriptor`** (nucleo). È uno scostamento **accettato** dal
design §7.2 («gli endpoint a mano sono verbi»). Gli endpoint a mano di E3a sono due: un verbo (annulla) e questa lettura.

## 3. Alternative scartate

| Alternativa | Perché no |
|---|---|
| Un permesso di lettura su `ModuleSettingsDescriptor`, accanto a quello di gestione | l'altra strada che il revisore ha messo davanti a Carmine: una PR del nucleo con la sua nota per un caso solo, e una lettura di tutte le impostazioni a chi ne deve vedere una |
| Il server applica il preset quando l'evento nasce | lo staff non vedrebbe gli interruttori del tipo mentre lo sceglie, e il server cambierebbe in silenzio quello che il form mostra |
| `Events.ManageSettings` agli advisor dell'ED | gestirebbero le impostazioni, che il design dà al coordinatore e all'assistente (§6.2) |

## Da portare nel piano

- **§16.6** (gli endpoint a mano contati per famiglia, ognuno con la sua decisione): fra quelli di M4, **una lettura accanto al CRUD
  dell'evento**, `GET /api/events/kind-presets` (`Events.Edit`), ammessa perché l'endpoint delle impostazioni del nucleo apre solo a chi
  le gestisce e chi scrive eventi deve vedere che cosa preimposta ogni tipo; con il verbo «annulla», i due endpoint a mano di E3a.
- **Design `09-design-m4.md` §7.2** (lo porta il master, come ogni documento del maintainer): accanto a «gli endpoint a mano sono verbi»,
  la lettura dei preset dei tipi, con il link a questa nota.
