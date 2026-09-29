# I tipi di evento: chiavi del calendario, e due interruttori

**Data:** 29 settembre 2026 — fase E0 di M4
**Stato:** **decisa** (Carmine, 29 settembre 2026, sulla PR #180, [conferma di §17.1 e §17.2][ok]; §17.1 n.2 e n.3, §17.2 n.1,
come raccomandato).
**Regola applicata:** `CLAUDE.md` §3 (forkabilità: nessun tipo di evento nel codice) e §5, caso **(b)**: il vocabolario dei tipi del
calendario c'è già (nota `2026-09-08-tipi-di-evento-di-divisione`, piano §9.5). Design `09-design-m4.md` §0.6, §1.2, §1.12, §8.1,
§9.2, §12, §17.1 n.2 e n.3, §17.2 n.1.

[ok]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/180#issuecomment-5880522987

## 1. Che cosa serviva decidere

- **Quali eventi fa l'ED** (design §R.3, c1 e c2): **RFE** con soli slot pubblici; **RFO** con slot pubblici e privati; **MSE**
  (Mega Slot Event) con soli slot privati; **eventi liberi**, senza slot; l'**Online Day**, una serata in cui ognuno apre una
  postazione della divisione e ogni pilota vola, purché ogni volo tocchi un aeroporto della divisione; gli **eventi di HQ**, solo
  quelli in collaborazione con la divisione.
- **Dove sta il tipo.** Lo schizzo `evt_` del piano (§7) aveva una colonna `tipo` con un elenco nel codice (RFE, online day,
  training, exam…). Un elenco nel codice è un tipo di IT: un fork non lo rinomina senza toccare il codice.
- **Gli eventi di HQ.** L'API pubblica di IVAO non ha eventi (design §9.1, letta il 28 e 29 settembre 2026).

## 2. Le decisioni

1. **Il tipo di un evento è una chiave del vocabolario dei tipi del calendario** (`cms_calendar_kinds`, §17.2 n.1): la divisione
   ha `rfe`, `rfo`, `mse`, `onlineDay` ed `event`; il seme dei tipi li riceve in **E1** (estensione n.1 del design), senza toccare
   una chiave che il back office ha già scritto a mano. La voce di calendario di un evento ha il tipo dell'evento, e Training può
   aggiungere `onlineDay` ai suoi `conflictKinds`. **Training ed esami non sono eventi**: sono di M3.
2. **Il comportamento sta in interruttori sulla riga**, non nel tipo: `public_slots`, `private_slots`, `has_roster`,
   `whole_division` (l'evento vale su tutti gli aeroporti e le postazioni della divisione, come l'Online Day), `in_person`. **Il
   tipo li preimposta** con l'impostazione `kindPresets` (design §1.12), vuota di predefinito: RFE pubblici, RFO pubblici e
   privati, MSE privati, evento libero e Online Day nessuno. Lo staff li cambia sull'evento; il codice non conosce nessun tipo.
3. **Gli eventi di HQ** sono solo quelli **in collaborazione** con la divisione, **scritti a mano** con la loro scheda (§17.1
   n.2): `organizer` (`Division`, `Network` per HQ, `OtherDivision`) e `external_url`, la pagina di chi organizza.
4. **L'Online Day** è un evento `whole_division`: «della divisione» è il paese della divisione e le sue postazioni, dal nucleo, mai
   un elenco del modulo (design §12).

## 3. Alternative scartate

| Alternativa | Perché no |
|---|---|
| Un `enum` dei tipi nel modulo, con il comportamento legato al tipo | è un vocabolario di IT nel codice; un fork non lo rinomina (piano §4, test «XX») |
| Un `booking_mode` sulla riga (lo schizzo del piano) | due interruttori indipendenti dicono di più: RFO e MSE non sono «modi», e un evento in presenza può avere anche slot |
| Leggere gli eventi di HQ da IVAO | l'API non ne ha |

## 4. Che cosa si tocca, e dove

- **E1** (nucleo): i quattro tipi nel seme del calendario (`seed/calendar-kinds/kinds.json`, dove oggi ci sono `event`, `training`,
  `exam`, `tour`, `meeting` e `deadline`), con le etichette nelle lingue del seme.
- **E2**: `kindPresets` nelle impostazioni, vuote di predefinito; i valori di IT li scrive la divisione.
- **E3a**: `kind` dalla lista dei tipi del bootstrap e gli interruttori preimpostati nel form; `organizer` ed `external_url`.
- **E3b**: la voce di calendario con il tipo dell'evento.

## Da portare nel piano

**Già nel piano 1.24**: §7 (lo schizzo `evt_` sostituito, il tipo dal vocabolario, niente training ed esami), §9.2 riga Events, §9.5
(il tipo come chiave del vocabolario, il seme in E1, «senza nota, come `exam`»). Resta:

- **§9.5**: il seme resta senza nota (caso (a), un seme), ma **E1 ha una nota breve** per l'altra cosa che porta, il personaggio
  dell'ED sul banco e2e (`10-piano-implementazione-m4.md`, E1): oggi il banco non ha nessuno staff degli eventi.
