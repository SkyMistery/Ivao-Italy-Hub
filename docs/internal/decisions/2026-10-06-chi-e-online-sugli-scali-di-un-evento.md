# Chi è online sugli scali di un evento: una fase del nucleo, non E4

**Data:** 6 ottobre 2026 — fase E4 di M4 (il pubblico e le rotte), PR #223
**Stato:** **decisa** (Carmine, 6 ottobre 2026, in chat al master e pubblicata sulla #223 su sua istruzione: [la risposta][ok223], autore
`SkyMistery`): **(b), una fase del nucleo piccola, E4b**, in una PR sua con la sua nota, e la striscia la monta la prima fase del modulo
dopo di lei; **non è urgente, e può venire dopo E5**. Non è di E4: la sessione che coordina prepara la sessione di E4b a parte, da `main`.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)** — il meccanismo c'è (il blocco `networkStats` e la `LiveStatusStrip`), ma non copre il
caso: si estende, in una PR del nucleo a sé (§0 regola 6), mai dentro una fase del modulo. Design `09-design-m4.md` §7.1, §9.1;
`10-piano-implementazione-m4.md` E0 («Trovato», punto 17) ed E4 punto 2.

[ok223]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/223#issuecomment-6017107039

## 1. Che cosa serve

Il design M4 §7.1 vuole sulla pagina di un evento, **il giorno dell'evento**, «la `LiveStatusStrip` con chi è online sugli scali
dell'evento», e §9.1 dice come: `GetNetworkStatusAsync` (whazzup, 60 secondi di cache), letto alla richiesta, mai campionato. E0 l'aveva
segnato da misurare in E4; il piano di E4 dice: se il blocco `networkStats` non si restringe agli scali dell'evento senza toccare il nucleo,
la parte resta fuori e si scrive in «Com'è andata», o diventa una fase del nucleo con la sua nota. È questa.

## 2. Perché E4 non lo fa

**Misurato nel codice** (6 ottobre 2026, sul branch di E4: `main` a `78df526` più E3b; la #219, unita dopo su `main`, cambia solo le
parole della striscia):

- il blocco `networkStats` (`NetworkStatsProvider`, `Core/Content/CoreDataBlockProviders.cs`) conta **l'area della divisione** e
  nient'altro: il suo spazio aereo è quello di `IFirDirectory.GetAirspaceAsync`, e le sue proprietà sono un insieme chiuso — le quattro
  cifre e `showPositions` —, nessuna dice degli scali;
- `IvaoAirspace` (`Core/Ivao/IvaoNetworkStatus.cs`) sa fare uno spazio di soli aeroporti, ma la sua `CacheKey` è costruita sui **centri**
  e sui due conteggi (`"{centri}/{aeroporti}/{elenco dei centri}"`): due eventi con lo stesso numero di scali e nessun centro avrebbero la
  stessa chiave, e il secondo leggerebbe la risposta del primo per un minuto;
- il modulo **non può chiederlo da sé**: `IIvaoApiClient` nomina la rete, e `EventsArchitectureTests.TheModuleDoesNotNameTheNetwork` lo
  vieta (piano §4.2: il modulo passa dal nucleo);
- la `LiveStatusStrip` (`web/src/shared/ui/LiveStatusStrip.tsx`) fa una domanda fissa (`QUESTION`: le due cifre della divisione).

Ogni strada passa da file del nucleo (`Core/Content`, `Core/Ivao`, `web/src/shared/ui`): fuori da E4 per la regola 6.

## 3. Che cosa si toccherebbe

Una fase del nucleo piccola — **E4b**, se Carmine la vuole —, che non migra niente:

1. **`NetworkStatsProvider`** prende una proprietà in più, **gli scali** (`airports`, un elenco di ICAO), che una **schermata chiede** e un
   editor non salva — come `from` e `to` del blocco del calendario: fuori dallo schema zod del blocco, perché una pagina pubblicata con gli
   scali scritti dentro invecchierebbe con l'evento. Con gli scali, lo spazio è quello degli scali: controllori la cui stazione è uno di
   loro, piloti il cui piano di volo parte da uno di loro o ci arriva — le stesse due regole di `IvaoWhazzup.Read`, su un altro spazio.
2. **`IvaoAirspace`** con una `CacheKey` che nomina gli aeroporti quando non ci sono centri, così due insiemi diversi non si scambiano la
   risposta; ⚠️ e da misurare che un whazzup per insieme di scali al minuto regga (oggi uno solo per la divisione).
3. **`LiveStatusStrip`** con gli scali facoltativi, e la parola del titolo per quel caso.

Poi, **in una fase del modulo** (la prima dopo che E4b è unita, per esempio E6b), la pagina dell'evento monta la striscia con i suoi
scali **il giorno dell'evento**, e un evento di tutta la divisione mostra quella della divisione, che il sito ha già in cima.

## 4. Alternative

| Alternativa | Pro | Contro |
|---|---|---|
| **(a) Fuori da M4** | niente da scrivere | il design §7.1 lo chiede, e Carmine l'ha deciso |
| **(b) E4b come sopra** (raccomandata) | si estende un meccanismo che c'è, il modulo non nomina la rete | una fase del nucleo in più, una misura del costo del whazzup |
| **(c) Un'interfaccia nuova del nucleo** «chi è online su questi scali» per i moduli | il modulo chiede quello che gli serve | un secondo modo di leggere la stessa cosa del blocco: la regola «un pezzo scritto una volta» lo vieta |

**Raccomandazione: (b)**, quando Carmine vuole, in parallelo alle fasi del modulo (non migra `EventsDbContext`), e la pagina la usa nella
prima fase del modulo dopo il suo merge.

## 5. La domanda a Carmine

> «Chi è online sugli scali» sulla pagina di un evento (design M4 §7.1) non si fa senza toccare il nucleo, quindi E4 lo lascia fuori. Lo
> facciamo come fase del nucleo **E4b** — `networkStats` con gli scali chiesti dalla schermata, la chiave della cache che li nomina, la
> striscia con gli scali facoltativi — e la pagina lo monta nella prima fase del modulo dopo? Oppure resta fuori da M4?

## Da portare nel piano

Con la risposta (b): §9.1 (riga «Live status») e §9.3 (i blocchi Data: `networkStats` con gli scali che una schermata chiede), quando
E4b è unita con la sua nota; `10-piano-implementazione-m4.md` (una riga E4b, e la fase del modulo che monta la striscia); il design M4
§7.1 resta com'è.
