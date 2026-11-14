# Il lotto 3 dei contenuti: le scelte che la issue non scrive

- **Data**: 14 novembre 2026
- **Stato**: scelte di una sessione di lavoro, dentro la direzione della issue #321 (lotto 3 della revisione del codice,
  `2026-10-20-la-revisione-del-codice.md`). Nessuna chiede la risposta di Carmine: il revisore le legge come scelte di una sessione.
  La domanda vera del lotto, la pagina con figlie, è in `2026-11-14-la-pagina-con-figlie.md`.
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

## Da portare nel piano

- §9.7 (i contratti trasversali) o §16 (i meccanismi generici), dove si parla degli indirizzi dei blocchi: un indirizzo scritto in un
  contenuto è vuoto, un percorso del sito, un'àncora o un indirizzo `http(s)`, `mailto:`, `tel:`; il resto si rifiuta al salvataggio
  e alla pubblicazione (`SiteDestinations.IsFollowable`). Anche l'indirizzo di una voce del calendario.
- `docs/UI-GUIDELINES.md`, «How a block names an address»: l'ultima frase («An address of somewhere else is not checked») va letta
  come «where it leads is not checked; what it looks like is». Corretta in questa PR, con un paragrafo sulla forma.
