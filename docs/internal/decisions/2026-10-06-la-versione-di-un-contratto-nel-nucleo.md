# La versione di un contratto nel nucleo (E10g)

**Data:** 6 ottobre 2026 — fase E10g di M4, PR del nucleo (#230)
**Stato:** **scelta tecnica**, per dare forma nel codice a una decisione già presa: che l'esportazione degli eventi porti la versione
del suo contratto l'ha deciso Carmine sulla #228 ([risposte][a228], punto 9), e che il controllo passi nel nucleo invece di essere
copiato l'ha scelto dalberone il 6 ottobre, nella sessione di E5, sulla classificazione che la sessione che coordina ha chiesto prima
di ogni codice. Nessuna domanda su una decisione nuova; **una richiesta a Carmine** (§5): la copia dei tour resta com'è in questa PR, e
la sostituisce una sua sessione, come `GreatCircle` dopo la #206; e **una domanda** (§5): se vuole una riga nella tabella di
`CLAUDE.md` §2.
**Regola applicata:** `CLAUDE.md` §2 («a piece used in two places is written once») e §5, caso **(b)**: un pezzo del modulo dei tour
passa nel nucleo perché un secondo modulo ne ha bisogno, e un modulo non ne referenzia un altro. È una PR del nucleo, prima di E5 che
la usa (`CLAUDE.md` §0 regola 6), come E10e per la distanza fra due aeroporti (nota `2026-09-30-la-distanza-fra-due-aeroporti-nel-nucleo`).

## 1. Che cosa serve, e perché il modulo non ne fa a meno

- **L'esportazione degli eventi porta la versione del suo contratto** (Carmine sulla #228, punto 9): un'intestazione sua, di quel
  contratto; senza, o con una versione che l'hub non parla, 400 con le versioni accettate; il contratto scritto in un documento
  pubblico, come quello dell'agente. Il Gate Manager è un programma del maintainer, e romperlo in silenzio è ciò che la versione
  impedisce.
- **Il controllo c'è solo nei tour** (`AgentContract.RequireVersionAsync`, `src/IvaoHub.Modules.FlightOps/Agent/AgentContract.cs`,
  righe 44–73, T19b di M2), e un modulo non ne referenzia un altro (`CLAUDE.md` §2;
  `ArchitectureTests.AModuleDependsOnTheCoreAndOnNoOtherModule`). Una copia negli eventi sarebbe lo stesso pezzo scritto due volte: il
  giorno in cui una delle due cambiasse, due programmi non riceverebbero più la stessa risposta alla stessa domanda.
- **Il resto è già del nucleo**: il token personale con la sua `audience` (`PersonalTokenPolicy.For`, T19a), le parole
  (`LocaleCatalog`), la lingua di chi chiede (`ICurrentUser.Locale`). Manca solo il filtro.

## 2. Che cosa c'è, letto nel codice (`main` a `e9702b2`)

1. **Il filtro dei tour**: `AgentContract.RequireVersionAsync` legge l'intestazione `Hub-Agent-Contract` senza gli spazi intorno
   (`Trim`), la legge come un numero di sole cifre (`int.TryParse` con `NumberStyles.None` e la cultura invariante) e la cerca fra le
   accettate (`Accepted`, `[1]`). Altrimenti risponde 400 con `Results.Problem`: il titolo `flightops:errors.agentContract` nella lingua
   di chi chiede (`LocaleCatalog` e `ICurrentUser` chiesti a `RequestServices`; senza catalogo, la chiave) e le estensioni
   `code: "agentContract"`, `current` e `accepted`. Se la versione è accettata scrive nella risposta la stessa intestazione con **la
   versione parlata**, e passa all'endpoint. Nello stesso file le costanti dell'agente (l'audience, i limiti dell'evidenza), che sono dei tour e
   restano lì.
2. **Chi lo usa**: `AgentEndpoints.MapAgentEndpoints` mette il filtro sul gruppo `/api/flightops/agent` dopo la policy del token (riga
   42); l'endpoint aperto `/contract` scrive da sé `Hub-Agent-Contract` con la versione corrente (riga 32), perché un agente lo chiede
   prima di parlarne una; `AgentDesk.Contract()` dice `current` e `accepted` nel corpo (righe 56–57). Nei test, `PirepTests.Agent`
   (integrazione, righe 146–160): 400 senza versione, con `2` e con `one`, e `accepted` uguale a `[1]`.
3. **Il documento pubblico** è `docs/agent-contract.md` (righe 34–59 e 252): l'intestazione, il 400 con `code`, `current` e
   `accepted`, la regola delle versioni — dentro una versione l'hub solo aggiunge, e una modifica che rompe un agente è la versione
   dopo, accettata accanto alla vecchia per almeno un rilascio.
4. **Nel nucleo** nessun filtro di endpoint, e niente che sappia di una versione: `Core/Auth/` ha la metà del token (`PersonalTokens`,
   `PersonalTokenPolicy`, lo schema di autenticazione), non quella del contratto.

## 3. La forma nel codice

- **`src/IvaoHub.Core/Auth/ContractVersion.cs`**, namespace `IvaoHub.Core.Auth`, accanto ai token personali: il contratto è quello che
  parla un programma con un token. Una classe sola, immutabile:

  ```csharp
  public sealed class ContractVersion
  {
      public ContractVersion(string header, int current, IReadOnlyList<int> accepted, string titleKey, string code);
      public string Header { get; }
      public int Current { get; }
      public IReadOnlyList<int> Accepted { get; }
      public string TitleKey { get; }   // la chiave del modulo, con il suo namespace: il nucleo non aggiunge parole
      public string Code { get; }       // il "code" del problema (quello dei tour è "agentContract")
      public ValueTask<object?> RequireAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next);
  }
  ```

- **`RequireAsync` è il codice dei tour**, riga per riga, con i valori del contratto al posto delle costanti dell'agente: lo stesso
  `Trim`, lo stesso `int.TryParse`, lo stesso problema con le estensioni nello stesso ordine, la stessa intestazione nella risposta con
  la versione parlata (non la corrente). Un modulo dichiara il suo contratto una volta (`static readonly`) e lo mette sui suoi endpoint
  con `AddEndpointFilter(contratto.RequireAsync)`, dopo la policy del token, come i tour.
- **Il costruttore rifiuta quello che non può essere un contratto**: un'intestazione che non è un token di HTTP (vuota, con uno spazio,
  con `:`, con un a capo, con una lettera accentata: RFC 9110 §5.6.2), nessuna versione accettata, una versione zero o negativa, **una
  versione ripetuta**, la corrente che non è fra le accettate, una chiave o un `code` vuoti. Il rifiuto arriva quando il modulo dichiara
  il contratto, cioè quando mappa i suoi endpoint all'avvio, non a un programma. Le versioni sono **copiate**: aggiungerne alla lista
  passata, dopo, non apre niente. *Scelte di Claude*: l'intestazione come token e la versione ripetuta sono due rifiuti in più della
  lista della fase; i valori dei tour e quelli di E5 li passano.
- **Le parole sono del modulo**: il nucleo non aggiunge chiavi. Il titolo è la chiave del modulo con il suo namespace (nota
  `2026-09-26-le-chiavi-dei-moduli-con-il-namespace`), e senza catalogo è la chiave stessa, come nei tour.
- **Niente `Announce`** (la versione corrente nell'intestazione di un endpoint aperto, la riga 32 dei tour): il passaggio dei tour non
  ne ha bisogno, perché `AgentContract` tiene `Header` e `Current` e la riga 32 resta com'è, ed E5 non ha un endpoint aperto. Il giorno
  in cui un secondo endpoint aperto la chiedesse, è una riga lì; il commento di `RequireAsync` dice che l'endpoint aperto la scrive da
  sé.
- **Niente altro**: nessuna migrazione, nessun endpoint, nessuna chiave del nucleo, nessun cambio nel browser, nessuna registrazione
  (è un oggetto che il modulo costruisce, come `GreatCircle` è una funzione statica).

## 4. I test

`tests/IvaoHub.UnitTests/ContractVersionTests.cs`, nuovo, 21 casi. Una richiesta passa nel filtro come la fa passare un endpoint
(`DefaultHttpContext`, `DefaultEndpointFilterInvocationContext`), e il rifiuto si scrive come lo scrive il server
(`IResult.ExecuteAsync` nel corpo della risposta): è quello che legge un programma.

- **i rifiuti**: nessuna intestazione, vuota, di soli spazi, `2`, `0`, `-1`, `+1`, `1.0`, `one`, `1 1`, `99999999999`, e
  l'intestazione mandata due volte (`1,1`) → 400 con `code`, `current` e `accepted`, l'endpoint non raggiunto, nessuna versione nella
  risposta;
- **le accettate**: `1`, e `1` fra due spazi → l'endpoint raggiunto, la sua risposta passata così com'è, l'intestazione `1`;
- **la versione parlata, non la corrente**: un contratto alla versione 2 che parla ancora la 1 risponde `1` a chi parla `1` e `2` a chi
  parla `2`, e un rifiuto dice `current: 2` e `accepted: [1, 2]`;
- **il titolo nella lingua di chi chiede**: con le parole del repository, la frase dei tour in inglese e in italiano; senza nessuno a
  cui chiedere la lingua, quella della divisione; senza catalogo, o con una chiave che nessun file dichiara, la chiave;
- **i rifiuti del costruttore** (§3), e le versioni copiate;
- **il test gemello**: il nucleo con i valori dei tour (`AgentContract.Header`, `Current`, `Accepted`, `VersionTitleKey`,
  `"agentContract"`) e `AgentContract.RequireVersionAsync` su venti richieste — quelle sopra, e `\t1`, `01`, `1e0`, `0x1`, la cifra
  indo-araba U+0661, l'intestazione due volte con `1,2` —, con e senza catalogo, in inglese, in italiano e senza utente: **120
  coppie, la stessa risposta byte per byte** (lo stesso stato, lo stesso corpo, la stessa intestazione, l'endpoint raggiunto o no). E
  dice quali richieste i tour accettano, e quindi il nucleo: `1`, `1` fra due spazi, `\t1` e `01`. È la prova che il passaggio dei
  tour al nucleo non cambia nessuna risposta che un agente legge, e se ne va con la copia (§5).

`PirepTests.Agent` non cambia.

## 5. La copia dei tour, e la richiesta a Carmine

- **Questa PR non la tocca** (`CLAUDE.md` §0 regola 2; `core-guard`): come `GreatCircle` dopo la #206 (nota
  `2026-09-30-i-tour-sulla-distanza-del-nucleo`), la sostituisce **una sessione di Carmine**. Fino ad allora l'hub ha due copie dello
  stesso filtro, e il test gemello le tiene uguali.
- **Che cosa fa quel passaggio**: in `Agent/AgentContract.cs` restano le costanti (`Header`, `Current`, `Accepted`, `VersionTitleKey`,
  che leggono `AgentEndpoints` alla riga 32, `AgentDesk` alle righe 56–57 e `PirepTests.Agent`), e il filtro diventa quello del nucleo
  con i valori dei tour, in un campo:

  ```csharp
  public static readonly ContractVersion Version = new(Header, Current, Accepted, VersionTitleKey, "agentContract");
  ```

  e poi **o** `RequireVersionAsync` che lo chiama (`AgentEndpoints` non cambia), **o** `RequireVersionAsync` tolto e la riga 42 di
  `AgentEndpoints` che diventa `.AddEndpointFilter(AgentContract.Version.RequireAsync)`, una riga. In `AgentContract.cs` i `using` di
  `System.Globalization` e `IvaoHub.Core.Localization` non servono più (l'analisi, `IDE0005`, li fa togliere). In `ContractVersionTests`
  se ne va il test gemello; restano gli altri, che leggono di `AgentContract` solo le costanti e la frase. **Nessuna risposta cambia**
  (il test gemello), nessuna migrazione, `docs/agent-contract.md` com'è.
- **La richiesta** va a Carmine in un commento sulla #230, con la raccomandazione: il passaggio fatto da una sua sessione dopo l'unione
  di questa PR, quando gli torna comodo. Nessun codice di questa PR né di E5 ne dipende.
- **La domanda**: se vuole **una riga nella tabella di `CLAUDE.md` §2**, accanto a «An external program of the user that calls the hub»
  (i token personali): la versione del contratto di un programma esterno è `ContractVersion` del nucleo, con un'intestazione di quel
  contratto e un documento pubblico, mai un controllo scritto dal modulo. Il file è suo, e la riga l'aggiunge il master.
  Raccomandazione: **sì**, come per `GreatCircle`, perché è la tabella dove la prossima sessione che scrive un contratto lo cerca.

## 6. Trovato scrivendo il codice

1. **Il test gemello non vede la versione della risposta.** Con la corrente al posto della parlata nell'intestazione i 120 confronti
   passano tutti, perché i tour accettano solo la 1 e la parlata è sempre la corrente (provato, poi rimesso). Cade solo il test del
   contratto alla versione 2: è il perché di quel test. Il giorno in cui un contratto accetterà due versioni, è lì che la risposta può
   sbagliare.
2. **Le altre prove al contrario**: con `NumberStyles.Integer` al posto di `None` cadono il gemello e il rifiuto di `+1`; senza `Trim`
   cadono il gemello e l'accettata `1` fra due spazi (provato, poi rimesso).
3. **`01` è la versione 1, e U+0661 non è una versione**: `NumberStyles.None` lascia passare gli zeri in testa, e `int.TryParse` con la
   cultura invariante legge solo le cifre ASCII. È il comportamento dei tour, il gemello lo fissa, e non c'è ragione di cambiarlo qui:
   cambierebbe una risposta dei tour.

## 7. Alternative scartate

| Alternativa | Perché no |
|---|---|
| Una copia negli eventi | lo stesso pezzo scritto due volte (§1); la nota della distanza l'aveva già scartata (§7 lì) |
| La versione nell'indirizzo (`/api/events/v1/…`) | niente `/api/v1` (piano §16 punto 10, `CLAUDE.md` §2): il contratto di un programma si versiona nell'intestazione, come quello dell'agente |
| Un'intestazione sola per tutti i contratti | due programmi, due storie: la versione 2 dell'esportazione non è la versione 2 dell'agente. Carmine: un'intestazione di quel contratto |
| Una registrazione dei contratti (un elenco su `IModule`, un filtro in `Core/Modules/`) | un modulo non dichiara i suoi contratti al nucleo, li usa sui suoi endpoint: un elenco che nessuno legge. `Core/Auth/` è dove sta l'altra metà, il token |
| Una policy di autorizzazione accanto a quella del token | una versione sbagliata non è un permesso negato: la risposta è 400 con le versioni, non 403; e una policy non scrive l'intestazione nella risposta |
| Un middleware sul percorso | conosce indirizzi, non endpoint; il filtro sta sul gruppo che lo chiede, dopo la policy del token, come nei tour |
| Un `Announce` per l'endpoint aperto | il passaggio dei tour non ne ha bisogno, ed E5 non ha un endpoint aperto (§3) |
| Passare i tour al nucleo in questa PR | `CLAUDE.md` §0 regola 2 e `core-guard`: è di una sessione di Carmine (§5) |
| Solo le prove sulla forma, senza il test gemello | provano il nucleo, non che il passaggio dei tour non cambi niente; il gemello confronta byte per byte (§6, punti 1 e 2) |

## Da portare nel piano

- `00-piano-di-progettazione.md` §6.3, il punto «Token personali»: **dal 6 ott 2026** (M4, E10g) il nucleo ha anche **la versione di
  un contratto** (`ContractVersion`, `Core/Auth/`): un modulo dichiara l'intestazione del suo contratto, la versione corrente, le
  accettate, la chiave del titolo e il `code`, e mette il filtro sugli endpoint del contratto; senza intestazione, o con una versione che
  l'hub non parla, 400 con `current` e `accepted`, e una risposta accettata ripete la versione parlata. Versione e changelog.
- `00-piano-di-progettazione.md` §9.1, la riga «Token personali (M2)»: la stessa cosa, in breve.
- `00-piano-di-progettazione.md` §9.7, «Prodotti esterni con un contratto»: la versione di un contratto è `ContractVersion` del nucleo
  (il secondo prodotto, il Gate Manager con l'esportazione degli eventi, lo porta la nota di E5).
- `00-piano-di-progettazione.md` §16 punto 10 («Niente `/api/v1`»): la versione nell'intestazione la controlla `ContractVersion` del
  nucleo, per ogni programma che non viaggia con il pacchetto (l'agente dei tour dopo il passaggio di §5, l'esportazione di E5).
- `05-design-m2.md` §6.6 (il paragrafo «Scritto in T19b») e §11, la riga 14; `06-piano-implementazione-m2.md`, T19b: il filtro della
  versione è del nucleo, quando la sessione di Carmine avrà tolto la copia (§5).
- `09-design-m4.md` §7.4 («Il Gate Manager»): l'esportazione porta la versione del suo contratto con `ContractVersion` (la forma è della
  nota di E5, punto 9).
- `10-piano-implementazione-m4.md`: E10g, scritta in questa PR.
- **Una riga nella tabella di `CLAUDE.md` §2**, se Carmine la vuole (§5): la aggiunge il master.

[a228]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/228#issuecomment-6022686808
