# Il lotto 3 dei contenuti: le scelte che la issue non scrive

- **Data**: 14 novembre 2026
- **Stato**: il §1 è una scelta di una sessione di lavoro, dentro la direzione della issue #321 (lotto 3 della revisione del codice,
  `2026-10-20-la-revisione-del-codice.md`): il revisore la legge come tale. Il §2 l'ha decisa Carmine in chat al master il
  14 novembre 2026 («Filtrare»), perché cambia quello che chi scrive vede ([il commento del master che la riporta](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/493#issuecomment-6874727366)). La pagina con figlie è in
  `2026-11-14-la-pagina-con-figlie.md`.
- **Classificazione** (`CLAUDE.md` §5): **(b)**, difetti del nucleo corretti con i meccanismi che ci sono.

## 1. Quali indirizzi si possono scrivere in un contenuto (punto 5)

La issue dice: rifiutare `javascript:` e `//`, come fanno già il link della libreria e la voce di menu. Quei due però hanno regole
diverse fra loro e più strette di quanto un blocco possa avere (il link vuole `http(s)`, la voce di menu un percorso o `http(s)` e poi
controlla che esista). La regola dei contenuti è **una sola**, `SiteDestinations.IsFollowable`, accanto a `OnThisSite` che già dice
che cos'è un indirizzo del sito, e la leggono:

- **ogni `href` di un corpo e ogni link di un pezzo di Markdown**, nella validazione della busta (`BlockDocumentWalker.ValidateEnvelope`):
  il walker trova già gli indirizzi senza conoscere i blocchi, quindi il controllo sta **in un posto solo** e vale anche per i blocchi di
  domani, per la descrizione di un evento e per il briefing di un tour, che passano dalla stessa validazione;
- **la pubblicazione**, che fino a ieri saltava ciò che era fuori dal sito: ora lo rifiuta, per una pagina salvata prima della regola;
- **l'indirizzo di una voce del calendario** (`CalendarWriteDtoValidator`), disegnato come link.

Che cosa passa: niente (un blocco a metà); un percorso che comincia con **una sola** `/`; un'àncora `#…`; un indirizzo intero
`http://`, `https://`, `mailto:`, `tel:`. Le ultime tre sono quelle che le istruzioni per Claude già chiamano «indirizzi interi»
(`WHOLE_ADDRESS` in `claudeFile.ts`); `mailto:` e `tel:` servono a un pulsante «Scrivici». Tutto il resto si rifiuta al salvataggio,
con `errors.url.unsafe` sotto il percorso del campo, così l'editor mette il messaggio accanto al blocco:

- `javascript:`, `data:`, `vbscript:` e ogni schema che non è nell'elenco: è un elenco di quelli ammessi, non di quelli vietati;
- `//altrove`, `/\altrove` e un percorso con dentro un carattere di controllo (`/\t/altrove`): il browser legge `\` come `/` e toglie
  tabulazioni e a capo, quindi diventano indirizzi di un altro sito che sembrano percorsi;
- un indirizzo relativo, `chi-siamo/staff` o `www.example.org`: non è pericoloso, ma porta in un posto che dipende dalla pagina in cui
  sta, e non dove chi l'ha scritto credeva. La voce di menu lo rifiuta per la stessa ragione.

⚠️ **Un test che c'era cambia**: `ContentPublishChecksTests.WhatTheApplicationAnswersForAndWhatLeavesTheSiteAreNotAsked` aveva
`//example.org/nobody-knows` fra gli indirizzi «che escono dal sito e non si chiedono». È proprio il caso che la issue chiede di
rifiutare: esce da quell'elenco e va nel test nuovo dei rifiuti, e al suo posto entra `tel:`.

Sul client non si aggiunge niente agli schemi zod: la regola sta sul server, il rifiuto torna come `ProblemDetails` sul percorso del
campo e l'editor lo mostra accanto al blocco (`CLAUDE.md` §2, «Rules are not written twice»). `web/src/blocks/schemas.ts` non si tocca.

**Il Markdown, dopo la revisione** ([il rilievo 1 del master](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/493#issuecomment-6874881009)). Il walker trova i link del Markdown con un'espressione, e
CommonMark ne ammette due forme che non leggeva: il testo con parentesi dentro (`[vedi [qui]](//altrove)`) e il link per riferimento
(`[testo][r]` con `[r]: //altrove` su una riga sua). E `react-markdown`, che li disegna, lascia passare `//altrove`. Quindi:

- sul server il walker legge tutte e due le forme (parentesi annidate di un livello; la riga di definizione intera, titolo compreso,
  così che una riga di prosa come `[Nota]: questo è il testo` resti prosa, come per CommonMark), e le rifiuta al salvataggio e alla
  pubblicazione come le altre;
- nel browser `MarkdownContent` ha un `urlTransform` che applica la stessa regola a ogni `href` disegnato (`isFollowable`, in
  `web/src/shared/ui/followable.ts`): è la rete per una forma che un'espressione non vede, non un secondo controllo da far valere. Gli
  indirizzi delle immagini restano al trasformatore della libreria, com'erano.

La regola sta così in due lingue, C# e TypeScript, come gli sfondi di una sezione. Le due metà **si tengono d'accordo con un file solo
di casi**, `tests/fixtures/content-addresses.json`, che leggono il test del server (`BlockDocumentWalkerTests`) e quello del browser
(`followable.test.ts`): un caso nuovo si aggiunge lì, e tutte e due lo devono rispettare. Gli `href` dei blocchi non passano dal
`urlTransform`: li disegna `blocks.tsx`, che è della 3.12 in questo momento, e il server li legge sempre (una proprietà `href` a
qualunque profondità, senza espressioni), quindi non hanno il buco del Markdown.

## 2. Le pagine sotto cui mettere una pagina (punto 6)

`GET /api/content/pages` lo chiamano solo i selettori del genitore: il form della pagina, la scelta del modello e il pannello di chi
approva. Leggeva con `CrudSource.BackOffice` **tutte** le pagine di ogni dipartimento, bozze comprese, mentre il commento diceva che
passavano solo indirizzo e titolo «che il sito già mostra a chiunque».

Le due strade erano filtrare o correggere il commento. **Carmine: «Filtrare»** (in chat al master, 14 novembre 2026). Ora l'elenco è:

- le pagine che il sito mostra a chi chiede: `database.Contents`, con il filtro di visibilità (`VisibilityQueryFilter`), quindi le pagine
  con una versione pubblicata e la visibilità di chi chiede, come gli indirizzi del sito (`2026-11-04-gli-indirizzi-del-sito-che-si-possono-leggere`);
- più ogni altra pagina, bozze comprese, che **l'handler unico** lascia modificare a chi chiede (`IAuthorizationService` con
  `Content.Edit` sulla riga, come `ContentEndpoints` fa già per `Content.EmbedCode` e `Content.Publish`): le bozze del suo dipartimento,
  perché una sezione si costruisce sotto la sua prima pagina prima che esca. Il super amministratore le ha tutte.

Che cosa cambia per chi scrive: nel selettore non vede più le bozze degli altri dipartimenti. Il server continua ad accettare come
genitore qualunque pagina (`ContentAddresses`): l'elenco è un'offerta, non la regola, e la regola non cambia. Il test che c'era
(`PageAddressTests.TheTopOfTheSiteIsNotACoordinatorsToWriteIn`) offriva al coordinatore degli eventi una **bozza** del web team: ora la
pubblica prima, e controlla anche che una bozza del web team non sia offerta e che la bozza del suo dipartimento lo sia.

## Da portare nel piano

- §9.7 (i contratti trasversali) o §16 (i meccanismi generici), dove si parla degli indirizzi dei blocchi: un indirizzo scritto in un
  contenuto è vuoto, un percorso del sito, un'àncora o un indirizzo `http(s)`, `mailto:`, `tel:`; il resto si rifiuta al salvataggio
  e alla pubblicazione (`SiteDestinations.IsFollowable`). Anche l'indirizzo di una voce del calendario.
- `docs/UI-GUIDELINES.md`, «How a block names an address»: l'ultima frase («An address of somewhere else is not checked») va letta
  come «where it leads is not checked; what it looks like is». Corretta in questa PR, con un paragrafo sulla forma.
- §9.3 (il modello dei contenuti), dove si parla della posizione di una pagina: il selettore del genitore offre le pagine online che chi
  scrive può leggere e le pagine, bozze comprese, che può modificare (decisione di Carmine del 14 novembre 2026).
