# L'albero del sito

- **Data**: 19 novembre 2026
- **Stato**: **decisa**, in due tempi.
  - **Dal design**: il §2 è il §3.2 del design `13-design-struttura-del-sito.md`, approvato da Carmine in chat al master il
    19 novembre 2026 («Sì, tutte e sette»; la risposta 4 è quella che spegne le schermate nel selettore),
    [riportato sulla #540](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/540#issuecomment-7011361289). La scelta 3 del §4
    segue il design così com'è scritto.
  - **Dal maintainer, dopo**: le scelte 1 e 2 del §4, che il design non scriveva (rilievo 3 della revisione della #550), decise da
    Carmine in chat al master il 21 novembre 2026 («Sì, tutte e due»),
    [riportato sulla #550](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/550#issuecomment-7059596766). La scelta 1 sostituisce
    la riga del design 13 §3.2 «Con il filtro `kind = Page` è la vista predefinita».
  - Le cose del §5 non chiedono una decisione: dicono com'è fatta la PR dove si discosta dal design o lascia un caso al lettore.
- **Chi**: una sessione di lavoro di Carmine (fase 4, PR 4.2, in coda dopo la 4.1, #546)
- **Classificazione** (`CLAUDE.md` §5): **(b)**. Estende tre meccanismi del nucleo: la lista generica (`DataList` senza pagine,
  la colonna tradotta rientrata), il form generato (una scelta spenta) e la lettura delle pagine `GET /api/content/pages`. Nessuna
  tabella, nessuna migrazione, nessun endpoint in più.

## 1. Che cosa serviva

Chi cerca una pagina in `/staff/content` la trovava in una lista piatta e paginata, con l'ultimo pezzo dell'indirizzo (`team`) e non
il suo posto (`/about/team`); le pagine scritte nel codice non c'erano; e il selettore del genitore era un elenco di indirizzi
scritto tre volte, nel form, in «nuovo da template» e nel pannello di chi approva.

## 2. Che cosa si fa

- **La vista «Albero del sito»** di `/staff/content` (`ContentListScreen`): le pagine e le schermate (dalla 4.1) in ordine di
  indirizzo, un livello alla volta, con il titolo rientrato, l'indirizzo intero, il tipo («Pagina» o «Pagina dell'applicazione»), il
  dipartimento, lo stato e la visibilità. È **la lista generica** con due estensioni: `DataList` con `paged={false}` (nessun pager e
  nessuna casella di ricerca: il browser ha tutto) e `col.localized(…, { indentBy })` (un passo a destra per livello). Un
  selettore «Vista» (non «Mostra», che è già il filtro del tipo) passa all'elenco, che resta com'era; il parametro è `view` nell'indirizzo.
- **Le azioni di una riga**: «Modifica» per una pagina che chi guarda può modificare; **«Nuova pagina qui»** per una pagina sotto cui
  può andare una pagina (non al terzo livello), che apre il form di una pagina nuova con quella madre già scelta
  (`/staff/content/new?kind=Page&department=…&parent=<id>`; il form la prende come valore iniziale, il server controlla il posto come
  ogni altro). Una schermata non ha azioni. Il form aperto dall'albero torna all'albero, con la briciola e alla fine
  (`view=tree` nell'indirizzo del form, `contentReturnSearch`); aperto dall'elenco torna all'elenco del suo dipartimento, come
  prima (rilievo 1 della revisione della #550).
- **La lettura** `GET /api/content/pages` dà di più: dipartimento, visibilità, stato (solo per una pagina che chi chiede può
  modificare: lo stato della bozza di un altro dipartimento è affare suo) e `mayEdit`, chiesto all'handler unico.
- **Il selettore del genitore** è uno, `parentChoices` (`features/content/pageTree.ts`), per i tre posti che lo disegnano: in ordine
  di albero, rientrato di un passo per livello (`ChoiceOption.level`, disegnato come margine interno della voce da
  `choiceItem`, mai come spazi nell'etichetta: si leggerebbero ad alta voce, starebbero davanti a quello che la ricerca per
  lettere della select confronta e si vedrebbero nella select chiusa — rilievo 6), con le etichette di sempre
  (`/about/team — Team`), e con le schermate **spente**, con la ragione (`ChoiceOption.disabled`, risposta 4 di Carmine). La pagina
  iniziale `/` non c'è: è la cima del sito.
- **Le parole** nuove in `locales/{it,en}/common.json` (`content.view.*`, `content.tree.*`, `content.options.source.*`,
  `content.fields.path`, `content.fields.source`).
- **I test**: `pageTree.test.ts` (l'ordine per livelli, le righe, le scelte, una pagina che non va sotto sé stessa);
  `SchemaForm.levels.test.tsx` (la scelta di un livello sotto: spinta dal margine, con il nome e la select chiusa senza spazi);
  `contentSearch.test.ts` (dove torna il form); `PageAddressTests.TheTopOfTheSiteIsNotACoordinatorsToWriteIn` (i campi nuovi: lo
  stato e «modifica» solo per la pagina propria) e `APageOnlineForAnotherDepartmentOnlyIsNotInTheirTree` (una pagina pubblicata
  con visibilità Department di un altro dipartimento non arriva al coordinatore: senza il filtro di visibilità diventa rosso,
  rilievo 2); l'e2e `site-tree.spec.ts` (l'ordine, la schermata segnata e senza link, «Nuova pagina qui» con la madre scelta, e
  la briciola che riporta all'albero).

## 3. Che cosa cambia per chi usa l'hub

- `/staff/content` per le pagine apre **l'albero**; le news, i documenti e i modelli restano liste.
- Chi crea una pagina può partire dal posto («Nuova pagina qui») invece che dal pulsante in cima.
- Nel selettore del genitore le pagine sono rientrate e le pagine dell'applicazione compaiono spente.

## 4. Le tre scelte che il design non scriveva

Le scelte 1 e 2 le ha decise il maintainer
([commento sulla #550](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/550#issuecomment-7059596766), «Sì, tutte e due»); la 3
segue il design così com'è scritto.

1. **L'albero è la vista predefinita solo senza un dipartimento nell'indirizzo.** La voce di un dipartimento nella barra laterale
   apre `/staff/content?department=…`: l'albero non filtra per dipartimento (una pagina tolta lascerebbe le figlie senza madre),
   quindi quella voce tiene il suo elenco, e l'albero è a un clic. **Sostituisce** la riga del design 13 §3.2 «Con il filtro
   `kind = Page` è la vista predefinita».
2. **Nell'albero non ci sono i filtri di dipartimento e di stato**, né la ricerca: è il sito intero, e chi lo legge ha tutte le righe.
3. **L'ordine fra sorelle è quello dell'indirizzo, un livello alla volta** (`byAddress`), non il confronto delle stringhe intere,
   che metterebbe `/a-b` fra `/a` e `/a/b`.

## 5. Dove la PR si discosta dal design, e i casi lasciati al lettore

1. **Il selettore del genitore dice `/percorso — titolo`**, come ha sempre detto, dove il design scrive «titolo — /percorso»:
   l'indirizzo davanti tiene in colonna i livelli e lascia alla ricerca per lettere della select il primo carattere, `/`.
2. **La colonna dello stato ha un valore solo per chi può modificare la riga**: per una pagina che si legge soltanto online lo stato
   della sua bozza è affare del suo dipartimento (il campo `status` della lettura è nullo), quindi la cella resta vuota.
3. **Una pagina la cui madre chi guarda non vede** (per esempio la bozza sotto una pagina tolta dal sito) compare rientrata sotto
   nessuna riga. Non si rompe niente e non c'è un giro: la colonna dell'indirizzo dice dove sta (rilievo 4).
4. **Con `?department=X&view=tree`** la briciola nomina il dipartimento mentre l'albero mostra il sito intero, e il dipartimento
   si toglie solo dall'elenco, dove c'è il suo filtro (rilievo 5). È l'indirizzo che dà la voce di un dipartimento seguita dal
   clic su «Albero del sito», e la scelta 1 del §4 lo lascia così.

## Da portare nel piano

- **§9.3** (il modello dei contenuti), con il design della fase 4: `/staff/content` mostra le pagine come albero del sito, con le
  pagine dell'applicazione segnate e senza azioni; «Nuova pagina qui» apre una pagina con la madre scelta; il selettore del genitore
  è uno, con le schermate spente. L'albero è la vista predefinita solo senza un dipartimento nell'indirizzo, e non ha filtri né
  ricerca (§4, scelte 1 e 2 del maintainer).
- **Il design 13 §3.2**: la riga «Con il filtro `kind = Page` è la vista predefinita» diventa «senza un dipartimento
  nell'indirizzo» (§4, scelta 1).
- **§16.6** (la lista e il form generati): `DataList` con `paged={false}` per una lettura che dà tutte le righe; la colonna tradotta
  con `indentBy`; una scelta del form generato può essere spenta (`ChoiceOption.disabled`), con la ragione nell'etichetta, e
  rientrata (`ChoiceOption.level`, un margine e mai spazi nell'etichetta), disegnata da ogni select con `choiceItem`.
