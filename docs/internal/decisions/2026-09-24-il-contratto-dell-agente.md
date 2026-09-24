# Il contratto dell'agente del validatore (T19b)

**Data:** 24 settembre 2026 — fase T19b di M2
**Stato:** **decisa** (Carmine, 24 settembre 2026: due risposte in apertura, §2; il resto è la nota del 15 settembre, le quattro risposte di
T19a nella nota `2026-09-24-i-token-personali` §2, o una scelta tecnica dichiarata qui sotto)
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: il meccanismo è deciso nella nota `2026-09-15-token-personali-e-agente-del-validatore`
(caso c, piano 0.79) e il nucleo l'ha scritto in T19a; qui il modulo lo usa. Nessun meccanismo nuovo del nucleo.

## 1. Che cosa serve

Piano di implementazione, parte C, T19 punti 2 e 3: `GET /api/flightops/agent/contract`, la coda e il dettaglio del PIREP, `POST …/checks`
con sostituzione e `ran_by = agent`, l'intestazione `Hub-Agent-Contract`, `docs/agent-contract.md`. «Fatta quando»: con un token creato da
`/me/tokens`, una `curl` in sviluppo legge un PIREP e scrive un esito che la pagina di validazione mostra.

## 2. Le risposte di Carmine

| Domanda | Risposta | Scartato |
|---|---|---|
| `fo_check_results` diventa `[Audited]`? | **No.** Ogni esito dell'agente porta **sulla sua riga** chi l'ha mandato: `by_vid`, `token_id`, `agent_version`. Rimandare lo stesso controllo sostituisce la riga, quindi la versione precedente sparisce; la decisione del validatore resta la cosa che conta. | `[Audited]`: ogni scrittura, del server e dell'agente, e ogni sostituzione in `hub_audit_log` — circa sedici righe per PIREP inviato più quelle dell'agente, con l'evidenza dentro, sul DB condiviso. |
| L'agente può mandare un controllo che gira già sul server? | **No, 400** (`flightops:errors.agentCheckKeyServer`): **un controllo, un esecutore**. L'agente scrive solo le chiavi che il server non esegue (oggi `semicircularLevels`, `atcCoverage`) e quelle nuove dell'app; così si misura quanto sbaglia ogni controllo. | Affiancato: due verdetti sullo stesso controllo che possono non coincidere, e un errore suggerito da uno dei due. |

## 3. Com'è fatto

- **`Agent/`** del modulo: `AgentContract` (l'audience `flightops.agent`, l'intestazione, versione corrente 1 e versioni accettate `[1]`, i
  limiti, il filtro della versione), `AgentDtos` (i DTO della versione 1, **suoi**: stati ed esiti come stringhe, nessun valore di default),
  `AgentDesk` (coda, lettura, scrittura) e `AgentEndpoints`.
- **L'audience** è dichiarata in `FlightOpsModule.TokenAudiences` con `Tours.Validate`; la sua parola è `flightops:tokenAudiences.agent`. Il
  form di `/me/tokens` ora mostra la parola del modulo (`audienceWordKey`: `flightops.agent` → `flightops:tokenAudiences.agent`); la lista
  mostra ancora l'identificatore, come un permesso.
- **`GET /api/flightops/agent/contract`** è **aperto a tutti**, senza token né intestazione: un'app lo chiede prima di essere configurata, e
  non dice niente che la pagina pubblica degli errori non dica già. Dice versioni, `agentChecks` (le chiavi del catalogo che il server non
  esegue) e `serverChecks`. *Scelta di Claude.*
- **Il resto del gruppo** chiede `PersonalTokenPolicy.For("flightops.agent")` e un `Hub-Agent-Contract` accettato (400 con `accepted`
  altrimenti), e risponde con la stessa intestazione.
- **La coda** (`GET …/pireps`, `?pending=true`): i PIREP `Queued` e `InReview` che **l'unico handler** lascia validare al membro — mai i suoi,
  mai un tour su cui non è abilitato —, i più vecchi prima, al massimo 200. `pending` = le regole chiedono un controllo dell'agente e nessun
  agente ha scritto dopo `queued_at`. *Scelte di Claude:* il limite 200 (una coda vera ne ha decine) e il filtro per riga in memoria.
- **Il dettaglio** (`GET …/pireps/{id}`): **403** se il membro non può decidere il PIREP. Qui l'agente legge **meno** della pagina: la pagina
  lascia leggere ogni PIREP a chi valida anche un solo tour (design §4.1), l'agente solo quelli che il suo membro decide (nota del 15
  settembre §3.2). Porta tutte le revisioni del piano, le tracce, i controlli dell'agente che le regole congelate nominano con i loro
  parametri, l'impostazione `northSouthLevelCountries`, gli aeroporti con le piste, l'archivio ATC da un'ora prima del primo decollo a
  un'ora dopo l'ultimo atterraggio (la finestra del meteo, `WeatherArchive.Window`) con i contatti e le esenzioni dichiarati, e gli esiti che
  gli agenti hanno già mandato.
- **La scrittura** (`POST …/pireps/{id}/checks`): 404, poi 403 (l'unico handler), poi **409** se il PIREP non aspetta (`agentNotWaiting`),
  poi 400 campo per campo (`agentVersion`, `results`, `results[i].checkKey|outcome|evidence`). Ogni esito sostituisce quello dell'agente per
  lo stesso controllo, **chiunque l'abbia mandato**; poi gli errori di **tutti** i controlli falliti — del server e degli agenti — si
  suggeriscono di nuovo con `FlightChecks.Suggest`, lo stesso del motore. Il PIREP non si tocca: la sua `row_version` non cambia, e un
  validatore che ha la pagina aperta non riceve un 409 perché l'agente ha scritto. Vede il suggerimento ricaricando.
- **Una chiave sconosciuta** si salva e non suggerisce niente (nota del 15 settembre §3.2). La forma è quella del catalogo: lettere e cifre,
  minuscola in testa, fino a 64. L'esito si legge **per nome** (`Enum.TryParse` avrebbe preso anche `"1"`).
- **La pagina di validazione** dice di chi è l'agente e quale versione (`ReviewCheckDto.By`, `AgentVersion`: «dall'agente di Bench
  Coordinator, versione e2e»).
- **`FlightChecks`** ha ora `WantedOfTheAgent(rules)` accanto a `Wanted(rules)` (lo stesso codice, predicato opposto) e `ServerKeys`.
- **Migrazione** `AddAgentResultColumns`: tre colonne nullable su `fo_check_results`. Additiva.
- **`docs/agent-contract.md`** (inglese): token, intestazioni, i quattro endpoint con esempi, le regole di un esito, la tabella di che cosa
  l'evidenza può e non può contenere (nota del 15 settembre §4), le risposte non 200 con le chiavi, tre `curl`.

## 4. Trovato per strada

- ⚠️ **Il PIREP non è `[Audited]`**: è `IAuditable` (chi e quando), e la sua storia sta in `fo_pirep_events`. La nota di T19a, HANDOFF e il
  changelog 1.04 dicevano «PIREP e tour compresi» fra le righe dei moduli senza audit fino a T19a: per il PIREP la frase era sbagliata (i tour,
  le leg, le regole, i ban sono `[Audited]`). Corretta in HANDOFF; la nota di T19a resta com'era, con il rimando qui.
- **Il login di prova non aggiorna `last_login_at`**: i test dei PIREP lo scrivono nel seme dell'utente, altrimenti il token risponde
  `signInAgain`. Il login del banco e2e passa da `UserSyncService` e lo aggiorna.

## 5. Test

Integrazione `PirepTests.Agent.cs` (le VID della classe; il validatore 780085 con un grant su un tour): **l'agente di prova** crea il suo
token da `/api/me/tokens`, trova il PIREP nella coda `pending`, legge piano e traccia, manda `semicircularLevels` fallito → la pagina mostra
l'esito dell'agente con VID e versione, l'errore suggerito e non segnato, la coda conta un controllo fallito; rimandato come passato
sostituisce la riga e toglie il suggerimento. **I rifiuti**: contratto aperto a tutti; 400 senza versione, con `2` e con `one`; il cookie
non apre gli endpoint dell'agente; 403 e fuori coda su un tour non abilitato; le sette chiavi di errore; una chiave sconosciuta salvata
senza suggerimento; 409 dopo la decisione; 403 e fuori coda sul proprio PIREP. **e2e** `full/tours-agent.spec.ts`: il token dalla pagina
`/me/tokens`, un contesto con il solo token (la `curl`), il risultato sulla pagina di validazione. Unit: `audienceWordKey`.

## 6. Che cosa resta

- **T21**: l'app Python legge e scrive con questo contratto; la mail a Navigraph prima di distribuirla (nota del 15 settembre §7).
- **T20**: `FORKING.md` rimanda a `docs/agent-contract.md`; gli endpoint dell'agente si contano fra quelli scritti a mano (piano §16.6).
