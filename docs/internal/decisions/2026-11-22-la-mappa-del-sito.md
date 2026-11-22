# La mappa del sito

- **Data**: 22 novembre 2026
- **Stato**: **decisa** per quello che è il §5.1 del design `13-design-struttura-del-sito.md`, approvato da Carmine in chat al master
  il 19 novembre 2026 («Sì, tutte e sette»; la risposta 5 è «pagine e schermate, ognuno quelle che può leggere»),
  [riportato sulla #540](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/540#issuecomment-7011361289). Le scelte del §4 sono
  della sessione e la PR le dice perché il revisore le veda; **la scelta 1 (`/contact`) è una «Proposta»**: è una domanda di
  prodotto, portata al master per Carmine, e la nota registrerà la risposta con il link al suo commento.
- **Chi**: una sessione di lavoro di Carmine (fase 4, PR 4.4, accanto alla 4.3)
- **Classificazione** (`CLAUDE.md` §5): **(c)** come dice il design (§0: «il blocco della mappa del sito» è uno dei due pezzi nuovi
  del nucleo), con il design come nota della fase; **(b)** per il resto: un blocco registrato in due metà, un provider, la lettura
  degli indirizzi che diventa una sola, un seme. Nessuna tabella, nessuna migrazione, nessun endpoint in più (passa da
  `/api/blocks/data/siteMap`).

## 1. Che cosa serviva

Il sito non aveva una pagina che dicesse che cosa c'è dentro. C'era solo `sitemap.xml`, per i motori di ricerca, con le pagine
pubbliche. La fase 4 vuole una mappa per le persone, generata dall'albero (4.2) e dalle schermate dichiarate (4.1), che mostri a
ognuno quello che può aprire.

## 2. Che cosa si fa

- **Il blocco di dati `siteMap`, del nucleo, in due metà.** In C# (`CoreBlocks.SiteMap`, `BlockRegistry.cs`) il tipo, `Data`,
  **`AlwaysLive`**, `Audience` `Public` (quella di default) e **`SharedFor` cinque minuti** (`SiteMapProvider.SharedFor`). In
  TypeScript (`web/src/blocks/`) lo schema `siteMapSchema` (un titolo tradotto facoltativo e `href`, la pagina da cui partire), il
  componente `SiteMapBlock`, l'icona `ListTree` di `lucide-react`, nel cassetto «Dati».
- **Il provider `SiteMapProvider`** (`CoreDataBlockProviders.cs`) risponde a chi legge: le pagine online che il filtro di
  visibilità gli lascia vedere, con il titolo della versione pubblicata, e le schermate di `SiteDestinations.Screens` (del nucleo e
  dei moduli abilitati) che quel lettore può aprire secondo la visibilità che ciascuna dichiara. Niente news né documenti singoli
  (risposta 5): ci sono le schermate `/news` e `/documents`. La risposta è piatta, in ordine di indirizzo (`path`, `title` per una
  pagina, `titleKey` per una schermata).
- **Il browser** (`web/src/blocks/siteMap.ts`, `siteMapTree`) mette ogni voce sotto la pagina in cui sta e ordina le sorelle per
  titolo nella lingua sullo schermo (quella dell'anteprima, nell'editor), con `Intl.Collator`: l'ordine dipende dalla lingua, che il
  server non conosce. Lo disegna come un elenco annidato, `ul` dentro `ul` con lo stile degli elenchi della prosa
  (`list-disc pl-6`, come `MarkdownContent`), dentro un `nav` che porta il nome del blocco. Nessun componente nuovo.
- **Una lettura sola** (design §5.1): `SiteContents.ReadAsync` (`Core/Content/SiteContents.cs`, accanto a `ContentAddresses`) è
  la query di quello che il sito mostra a chi chiede, e la usano la mappa e `GET /api/content/addresses`, che prima la scriveva
  per conto suo.
- **La visibilità di una schermata** (`VisibilityScopeRules.Reads`, in `Data/VisibilityQueryFilter.cs`, accanto al filtro): la
  regola del filtro per una cosa che non è una riga e non ha dipartimento, letta dalle stesse proprietà del contesto
  (`SeesMemberRows`, `SeesStaffRows`, `SeesEveryDepartment`). Sta nello stesso file del filtro perché chi cambia l'uno veda l'altro.
- **La pagina seminata** `seed/content-pages/site-map.json`: `/site-map`, del WD (il proprietario dei semi, `SiteOwnership`),
  `Public`, in cima, dal template `section-page`, con il blocco `siteMap` in una sezione sua; testi in `seed.pages.siteMap.*` di
  `locales/{it,en}/seed.json`. ⚠️ `site-map` non è una parola riservata (lo è `sitemap.xml`): l'indirizzo è libero, e le
  installazioni che ci sono già la ricevono al prossimo avvio se `/site-map` non è preso.
- **Le parole** in `locales/{it,en}/common.json`: `blocks.siteMap.*` (nome, descrizione, vuoto, campi, suggerimento).
- **I test.** `SiteMapBlockTests` (integrazione: un visitatore, un membro e il superadmin ricevono ognuno le sue pagine; una bozza
  non c'è; il titolo è quello online e non quello della bozza; la mappa di una sezione non ha schermate; l'intero sito ha le sei
  schermate del nucleo, `/contact` compresa; un indirizzo di fuori non è una radice; la risposta di un visitatore porta
  `s-maxage`, quella di un membro no; una cattura risponde niente). `siteMap.test.ts` (l'annidamento, la pagina iniziale, la madre
  che il lettore non vede, l'ordine per lingua). L'e2e smoke `site-map.spec.ts` (la pagina e la galleria `/staff/admin/ui-kit`). I
  conteggi scritti a mano crescono di uno: `uiKit.test.ts` (47), `blocks.test.tsx` (nove blocchi di dati del nucleo),
  `DataBlockEndToEndTests` (22), e `blockAddresses.test.ts` aggiunge `siteMap` ai blocchi che portano da qualche parte.

## 3. Che cosa cambia per chi usa l'hub

- Una pagina nuova, **«Mappa del sito»** a `/site-map`, con una voce nel piè di pagina; il WD la modifica come ogni altra.
- Nella tavolozza dell'editor, cassetto «Dati», il blocco **«Mappa del sito»**: tutto il sito, o una sezione (il campo «Comincia
  dalla pagina»).
- ⚠️ **Le istruzioni per Claude cambiano** (design §7): un blocco nuovo nella tavolozza cambia l'impronta `rules` dell'intestazione
  di «Scarica per Claude», e l'import rifiuta un file scaricato prima. **Chi ha un file scaricato prima di questo rilascio lo
  riscarica**: va scritto nelle note di rilascio della 0.27.0.
- **`GET /api/content/addresses`** dà il titolo della versione online, non quello della bozza: gli indirizzi che il form offre si
  chiamano come il sito li mostra (vedi §4, scelta 8).

## 4. Le scelte che il design non scriveva

1. **`/contact` nella mappa di un visitatore** — ⚖️ **Proposta**, domanda a Carmine. La 4.1 ha lasciato `/contact` dichiarata
   `Public` (nota `2026-11-19-le-schermate-del-sito-dichiarate-una-volta` §4, scelta 2, decisa da Carmine sulla
   [#546](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/546#issuecomment-7058790582)), anche se la rotta è sotto `_member` e
   chi non ha fatto l'accesso finisce al login. La mappa legge la dichiarazione e non ha eccezioni sue, quindi oggi un visitatore
   vede «Contatti» e cliccandolo gli si chiede l'accesso.
   - (a) **Lasciarla così** — **raccomandata**: la regola resta una, la dichiarazione; il visitatore vede dove si scrive alla
     divisione, e il login glielo spiega. Nessuna riga di codice.
   - (b) **Dichiarare `/contact` `Members`**: sparisce dalla mappa del visitatore, ma il controllo dei link alla pubblicazione
     (3.15) rifiuterebbe ogni pagina pubblica che porta a `/contact` — il seme `about` e il suo template hanno il pulsante
     «Scrivici». Una riga di `SiteDestinations.CoreScreens`, e una decisione sui link.
   - Mai un'eccezione nella mappa per `/contact`: sarebbe la seconda regola che la nota della 4.1 ha tolto.
2. **La radice si chiama `href`** ed è il campo indirizzo della fase 3: `blockAddresses.test.ts` vuole che un campo indirizzo si
   chiami `href`, e così la pubblicazione lo controlla come ogni altro (deve arrivare, per i lettori della pagina: una mappa
   pubblica che parte da una pagina dello staff non esce). Il provider legge la pagina a quell'indirizzo e quelle sotto di lei, senza
   schermate; vuoto o `/` è tutto il sito; un indirizzo di fuori dà una mappa vuota, mai il sito intero.
3. **La pagina iniziale è una riga sola.** La riga `home` si legge a `/home` e a `/`, e la schermata `/` è la stessa pagina (design
   §1, ⚠️): quando il lettore può aprire la riga `home`, la mappa la disegna a `/` con il suo titolo e lascia fuori la schermata;
   altrimenti c'è la schermata. Sta in testa, prima delle altre. La costante `HOME_SLUG` passa da `HomePage.tsx` a
   `web/src/blocks/siteMap.ts` (esportata da `blocks/index.ts`, che `HomePage` già legge): un blocco che importasse la schermata
   importerebbe sé stesso.
4. **Una pagina la cui madre il lettore non vede** (una pagina pubblica sotto una dei membri, letta da un visitatore) sta sotto la
   pagina più vicina sopra di lei che vede, o in cima: sulla mappa, non persa con la madre. L'albero della 4.2 la rientra sotto
   nessuna riga; qui un elenco annidato non lo può fare.
5. **Il seme ha anche le sezioni `body` e `links`** del template `section-page`: senza, `seeds.test.ts` dice che l'editor le
   offrirebbe come «aggiunte nel template». In `body` un paragrafo rimanda a `/news` e `/documents`; `links` è quella del template.
   **La voce nel piè di pagina** (`Footer`, `sort` 90) è mia: una mappa a cui nessun link porta non la trova nessuno. Il design non
   la chiedeva; il WD la sposta o la toglie dal menu come ogni altra.
6. **`SharedFor` cinque minuti**: la risposta di un visitatore è uguale per ogni visitatore, e una pagina pubblicata compare sulla
   sua mappa entro cinque minuti. Quella di chi ha fatto l'accesso non è mai condivisa (la regola di `SharedFor`).
7. **Una cattura risponde niente.** `AlwaysLive` fa sì che la pubblicazione non la chieda mai; se un giorno la chiedesse, una mappa
   vuota è meglio di quella di chi ha premuto «Pubblica» su una pagina che legge chiunque.
8. **Il titolo è quello della versione online, anche per `GET /api/content/addresses`.** Prima quella lettura dava il titolo della
   riga, cioè della bozza; la mappa non può (un visitatore legge quello che è stato pubblicato, nota
   `2026-10-21-il-pubblico-legge-la-versione`), e con una lettura sola vale per tutte e due, come per l'albero della 4.2 (revisione
   della #493).
9. **Senza pagine**, come la lettura degli indirizzi: è il sito che il lettore può già aprire pagina per pagina, e una divisione ha
   qualche centinaio di pagine.

## Da portare nel piano

- **§16** (meccanismi generici): il blocco di dati `siteMap`, sempre live, che risponde a chi legge (pagine online e schermate che
  può aprire), con una radice facoltativa scelta dal campo indirizzo; la lettura del sito in una classe sola, `SiteContents`, per
  gli indirizzi e per la mappa, con il titolo della versione online; la regola di visibilità per una schermata accanto al filtro
  (`VisibilityScopeRules.Reads`).
- **§9.3** (il modello dei contenuti), con il design della fase 4: la pagina `/site-map` è un seme del WD, pubblica, con una voce
  nel piè di pagina; la pagina iniziale è una riga sola sulla mappa.
- **Le note di rilascio della 0.27.0**: un blocco nuovo nella tavolozza, quindi i file «Scarica per Claude» presi prima vanno
  riscaricati.
- **La risposta di Carmine su `/contact`** (§4, scelta 1), quando arriva, con il link al suo commento.
