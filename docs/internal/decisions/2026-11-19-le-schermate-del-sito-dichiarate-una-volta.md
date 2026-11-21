# Le schermate del sito dichiarate una volta

- **Data**: 19 novembre 2026
- **Stato**: **decisa** — è il §3.1 del design `13-design-struttura-del-sito.md`, approvato da Carmine in chat al master il
  19 novembre 2026 («Sì, tutte e sette»), [riportato sulla #540](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/540#issuecomment-7011361289).
  Questa nota dice com'è fatta la PR 4.1 e le due scelte che il design non scriveva (§4).
- **Chi**: una sessione di lavoro di Carmine (fase 4, PR 4.1)
- **Classificazione** (`CLAUDE.md` §5): **(c)**, un membro nuovo del contratto fra nucleo e moduli (`IModule.SiteScreens`), e
  **(b)** per il resto: il bootstrap `/api/me`, `SiteDestinations` e il lettore degli indirizzi del browser crescono, nessun
  meccanismo nuovo. Tocca il nucleo: PR sua, prima delle altre della fase.

## 1. Che cosa serviva

Le pagine del sito scritte nel codice stavano in due liste uguali, `SiteDestinations.Screens` sul server e `SITE_SCREENS` nel
browser, tenute d'accordo a mano; e le pagine pubbliche dei moduli (`/tours`, `/events`, `/training`) non erano in nessuna delle
due: il server le conosceva solo come primo segmento riservato, e il menu rifiutava una voce verso `/tours` (nessuna pagina si chiama
così). La fase 4 le vuole nell'albero del sito (4.2) e nella mappa (4.4), segnate e non modificabili: prima serve una lista sola.

## 2. Che cosa si fa

- **`SiteScreen(Path, TitleKey, Visibility)`** (`Core/Modules/IModule.cs`) e **`IModule.SiteScreens`**, vuota di default in
  `ModuleBase`, come `ReservedSegments`. Ogni modulo dichiara la sua pagina d'ingresso con la chiave del titolo che la pagina usa già:
  `/tours` `flightops:public.title`, `/events` `events:public.title`, `/training` `training:public.title`. Una riga per modulo.
- **`ModuleRegistry.SiteScreens`** compone quelle dei moduli **abilitati**: un modulo spento si porta via le sue schermate.
- **`SiteDestinations.CoreScreens`** sono le sei del nucleo, con le chiavi che il menu ha sempre usato (`menu.screens./calendar`);
  **`SiteDestinations.Screens`** (e `SiteDestinations.Of(registry)` per il bootstrap) è la lista intera, prima il nucleo poi i
  moduli, una per indirizzo. La leggono la voce di menu (`MenuMayLeadToAsync`) e il controllo dei link alla pubblicazione
  (`ReachableAsync`), che prende la visibilità della schermata dalla dichiarazione.
- **`/api/me`** porta la lista in `registries.screens` (`BootstrapScreen`: indirizzo, chiave, visibilità), accanto ai blocchi e al
  catalogo dei permessi.
- **Il browser** non ha più `SITE_SCREENS`: `siteAddresses`, `knownAddresses`, `useAddressesToLinkTo`, `useSiteAddresses`, lo
  scarico per Claude e l'import leggono le schermate dal bootstrap e le nominano con `t(titleKey)`.
- **I test**: `ModuleCompositionTests` (le schermate dei moduli abilitati dopo quelle del nucleo, uno spento non ne porta; ogni
  schermata di un modulo sta sotto un segmento che il modulo riserva); `SiteMenuAndDashboardTests` (il bootstrap le porta, e una voce
  di menu verso `/tours` ora è accettata); nel browser `web/src/routes/-screens.test.ts`, che legge i sorgenti del server come
  `-reserved.test.ts` e controlla che ogni schermata del nucleo sia una rotta dell'albero del router e ogni schermata di un modulo
  una sua rotta pubblica, che la schermata sia scritta corta (`new("/x", …)`) o per esteso (`new SiteScreen("/x", …)`)
  (provato con due mutazioni: `/tourz`, e `new SiteScreen("/nowhere", …)` negli eventi, lo fanno fallire). Le prove con un bootstrap finto hanno
  `screens: []`, quelle e2e le sei del nucleo.

## 3. Che cosa cambia per chi usa l'hub

- Il menu e i campi indirizzo dei blocchi offrono anche **Tours, Eventi e Training** fra le «schermate del sito», e il menu le accetta.
- Nient'altro: il controllo dei link alla pubblicazione trattava già `/tours` come pubblico (primo segmento riservato), e lo stesso
  fa ora dalla dichiarazione.

## 4. Le due scelte che il design non scriveva

1. **`/training/request` non è dichiarata**, a differenza di quanto elenca il §3.1 del design (con visibilità `Members`). Dichiararla
   `Members` cambierebbe la pubblicazione: il controllo dei link (3.15) rifiuta un collegamento da una pagina pubblica a una
   destinazione più stretta, e oggi un pulsante «Richiedi un training» su una pagina pubblica passa (la rotta è sotto un segmento
   riservato, quindi pubblica). È un cambio di regola, non di struttura: se serve, ha una domanda sua. La pagina d'ingresso
   `/training` porta comunque alla richiesta.
2. **`/contact` resta `Public`**, com'era nelle due liste, anche se la rotta del browser è sotto `_member` (chi non ha fatto
   l'accesso viene mandato al login). Stessa ragione: la 4.1 non cambia che cosa si può collegare. ⚠️ Da guardare con la mappa del
   sito (4.4), che mostra a ognuno le schermate che può aprire.

Due cose del codice che sono volute, per chi aggiunge un quarto modulo:

- **`SiteDestinations.Of` tiene una schermata per indirizzo** (`DistinctBy(Path)`), la prima: prima il nucleo, poi i moduli
  nell'ordine del registro. Un modulo che dichiara un indirizzo che c'è già non cambia il titolo né la visibilità di quello, e
  l'avvio non si ferma; è la stessa regola di `ReservedSegments`, che si compone con `Distinct`.
- **`ModuleCompositionTests.EveryScreenOfAModuleIsUnderASegmentItReserves` elenca i tre moduli a mano**: il progetto dei test
  unitari non vede la lista di `IvaoHub.Web/Modules.cs` (non referenzia il Web, e la lista è `internal`). Un modulo nuovo con delle
  schermate si aggiunge a quella riga, accanto al `ProjectReference` che aggiunge comunque; la prova del browser
  (`-screens.test.ts`) invece legge da sola ogni `IvaoHub.Modules.*`.

## Da portare nel piano

- **§9.1** e **§9.7** (il contratto fra nucleo e moduli): `IModule.SiteScreens`, le pagine pubbliche di un modulo che il sito mostra
  fra le sue, dichiarate una volta con indirizzo, chiave del titolo e visibilità; quelle dei moduli spenti non ci sono.
- **§16** (meccanismi generici), il lettore degli indirizzi del sito: le schermate vengono dal server (`SiteDestinations.Screens`)
  attraverso `/api/me → registries.screens`; `SITE_SCREENS` e l'accordo a mano non ci sono più.
- **§9.3**, con il design della fase 4: il menu può portare alle pagine dei moduli.
