# Le categorie dei media, e il video senza file (#359)

**Data:** 28 ottobre 2026, fase di test, fase 2 della nota `2026-10-22-il-piano-dopo-la-prova-dello-staff` (voci d, VI, XIII).
**Stato:** **decisa.** Le categorie fisse e i selettori sono decisi nella nota del 22 ottobre (§3 voce VI, §4.B «Loghi», §4.F).
Il punto nuovo, il video, **l'ha deciso Carmine in chat alla sessione di lavoro, 28 ottobre 2026**: «Togli il campo».
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: si estendono meccanismi del nucleo che ci sono già — la colonna `category` della
libreria, il filtro della lista generata, l'annotazione `media` del generatore dei form.

## 1. Il problema

- La categoria di un media era **testo libero**: lo staff scriveva «logo», «Loghi», «logos» per la stessa cosa, e nessun selettore
  poteva offrire «i loghi».
- Il campo «Oppure un file della libreria» del blocco **Video** mostrava tutti i media. Ma la libreria **non accetta nessun video**
  (`MediaOptions.AllowedContentTypes`: PNG, JPEG, WebP, GIF, PDF; massimo 8 MB), come ha notato `gabbosonc31` sulla #359: un selettore
  «solo video» sarebbe stato sempre vuoto.

## 2. Le due strade per il video

| | Che cosa | Costo |
|---|---|---|
| **A** | **Togliere il campo**: il blocco Video ha solo l'indirizzo (YouTube, Vimeo, Twitch, l'elenco degli host del nucleo); niente categoria «Video» | Poche righe |
| B | Accettare MP4 e WebM: riconoscimento dai primi byte, un limite a parte per i video, servizio a intervalli (`Range`) | Spazio e banda su un hosting condiviso con Cloudflare davanti (100 MB per richiesta sul piano gratuito); più codice |

**Scelta A** (Carmine). Un video sta sul suo host e il blocco lo incornicia.

## 3. Che cosa cambia

- **`MediaCategory`** (`Core/Content/MediaCategory.cs`): `Image`, `Icon`, `Logo`, `Banner`, `Document`, un insieme chiuso, salvato
  come testo nella stessa colonna `cms_media.category` (varchar 64, nessuna colonna nuova). Il validatore rifiuta un valore fuori
  dall'insieme (`errors.media.categoryInvalid`; un testo che l'enum non legge si ferma già alla lettura del corpo).
- **Al caricamento** la categoria la dicono i byte (`MediaCategories.For`: un'immagine è `Image`, un PDF è `Document`), a meno che il
  selettore da cui si carica ne dica una: un file caricato dal selettore dei loghi è un `Logo`.
- **La migrazione `MapMediaCategories`** riscrive solo i valori, nessuno schema cambia (quindi additiva: il pacchetto di prima legge i
  valori nuovi come il testo che sono). Un file che non è un'immagine diventa `Document`, qualunque nome avesse; tra le immagini, un
  testo che contiene «logo»/«loghi» diventa `Logo`, «icon» `Icon`, «banner» `Banner`, il resto (anche vuoto) `Image`. La colonna è
  `utf8mb4_unicode_ci`, quindi maiuscole e accenti non contano. Le parole di prima non si conservano.
- **Il generatore dei form**: un campo `.meta({ media: true, mediaCategory: '<categoria>' })` offre solo i file di quella categoria. La
  query della libreria sa restringersi (`narrowLibrary`, e `mediaPickerQuery` porta nel suo `meta` il modo, come già il link alla
  pagina della libreria). Lo usa il blocco **Loghi** (`logoGrid`) con `Logo`.
- **Il blocco Video** perde `mediaId`: solo l'indirizzo. Un corpo salvato prima con un `mediaId` (poteva essere solo un'immagine) non
  ne mostra niente.
- **La libreria** (`/staff/media`): la categoria si sceglie da un elenco, e nella lista è un'etichetta.

## 4. Che cosa resta fuori

- I selettori degli altri blocchi (eroe, immagine, galleria, schede, testimonianza) mostrano ancora tutti i media, PDF compresi: la
  #359 chiede solo Video e Loghi. Restringerli alle immagini è una riga per blocco, quando si vorrà.
- Una categoria non è legata al tipo del file dopo il caricamento: chi scrive i metadati può dire `Logo` di un PDF. La migrazione e il
  caricamento non lo fanno.

## Da portare nel piano

- **§9.3** (la libreria dei media): la categoria è un insieme chiuso (`MediaCategory`), data dai byte al caricamento o dal selettore;
  niente video nella libreria, il blocco Video ha solo l'indirizzo.
- **§16** (il generatore dei form): l'annotazione `mediaCategory` accanto a `media`, e `narrowLibrary`.
- La nota del 22 ottobre, §4.F: «Video» non è tra le categorie (Carmine, 28 ottobre 2026).
