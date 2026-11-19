# L'indirizzo che l'hub non incorpora: come lo ferma la pubblicazione

- **Data**: 18 novembre 2026
- **Stato**: **decisa da Carmine in chat al master il 18 novembre 2026**: «A, un JSON solo», scritta sulla issue
  (https://github.com/SkyMistery/Ivao-Italy-Hub/issues/524#issuecomment-6981533425)
- **Chi**: la sessione di lavoro della issue #524 (ramo `fix/embed-host-at-publish`)
- **Classificazione** (`CLAUDE.md` §5): **(c)**. Il server dovrebbe leggere un campo delle `props` e sapere quali indirizzi
  il browser incornicia: oggi lo sa solo `web/src/blocks/allowlist.ts`. La nota `2026-11-07-la-foto-rimasta-da-scegliere`
  lo dice in chiaro: «un'altra regola di un blocco che il server volesse controllare è una domanda nuova, e non si appoggia
  a questa». Questa è la domanda nuova.
- **Viene da**: la risposta di Carmine sulla #376 (https://github.com/SkyMistery/Ivao-Italy-Hub/issues/376#issuecomment-6895535657),
  «Chiudi, issue Video/Embed».

## 1. Che cosa serve

Un blocco **Video** o **Embed** con un indirizzo che l'hub non sa incorniciare oggi si pubblica, e sulla pagina pubblica il
visitatore legge l'avviso dell'editor al posto del blocco (`blocks.video.notAllowed`, `blocks.embed.notAllowed`). Non è un
buco di sicurezza (non si carica niente), è una pagina rotta che nessuno ha fermato. La issue chiede che la pubblicazione lo
fermi, come ferma una foto ancora da scegliere (3.15, `errors.content.mediaToChoose`).

Misurato sul codice di oggi, l'avviso esce in **tre casi**, non in uno:

| Caso | Esempio | Oggi |
|---|---|---|
| host fuori elenco | `https://evil.example/video.mp4` | si pubblica, avviso al pubblico |
| host in elenco, indirizzo che non riconosce | `https://www.youtube.com/` (nessun video), `http://youtube.com/watch?v=…` (non https) | si pubblica, avviso al pubblico |
| Video senza indirizzo | `url` assente (lo schema lo permette: `.optional()`) | si pubblica, avviso al pubblico |

Un controllo che guarda solo l'host prende il primo caso e lascia passare gli altri due.

## 2. Perché nessun meccanismo che c'è basta

- **`RequiredMedia`** (3.15) dice dove deve esserci un **file**: un numero sotto `mediaId`. Qui serve un giudizio su una
  **stringa**, e il giudizio è la logica dell'elenco: host esatto o sottodominio, solo `https`, e per ogni sito come si legge
  l'identificativo (YouTube ha tre forme, Twitch canale o video). La nota del 7 novembre ha dichiarato l'eccezione «solo questo
  dato e solo per questo scopo».
- **I link** (`href`, `errors.content.linkNowhere`) il server li legge per convenzione di nome, ma `url` è un nome troppo
  comune per farne una convenzione, e un link non ha un elenco di host.
- **`config/security.json` → `frame-src`** elenca gli host **di destinazione** (`youtube-nocookie.com`, `player.vimeo.com`…),
  non quelli che scrive l'editor (`youtube.com`, `youtu.be`, `vimeo.com`, `twitch.tv`), e non dice come si riconosce un video.

## 3. Le alternative

| | Che cosa | Pro | Contro |
|---|---|---|---|
| **A** | **L'elenco diventa un dato, una fonte sola.** Un file JSON nel nucleo (per esempio `src/IvaoHub.Core/Content/embed-hosts.json`, risorsa incorporata, e importato da `allowlist.ts`): per ogni sito gli host e le forme riconosciute (un'espressione regolare con il gruppo `id`, l'indirizzo da costruire con `{id}`). Il browser costruisce l'`<iframe>` da lì come oggi; il server risponde solo «lo incornicerebbe?». La metà C# di `video` ed `embed` dichiara il campo, `FramedAddress: "url"`, accanto a `RequiredMedia`; un test di `web/` lo confronta con un `.meta({ framed: true })` dello schema | Niente scritto due volte: un sito nuovo è una riga del JSON, e vale subito per il blocco e per la pubblicazione. Prende tutti e tre i casi. Lo stesso test può controllare che ogni indirizzo costruito sia in `frame-src` | Riscrive `allowlist.ts` (nucleo) con le espressioni come dato: vanno ancorate e tenute strette come oggi (`[\w-]{1,64}`), e i test di `allowlist.test.ts` restano a guardia. Il `parent` di Twitch resta al browser, come segnaposto |
| B | **L'elenco copiato in C#** (`Core/Content/FramedAddresses.cs`, la stessa logica di `hostOf` + riconoscimento), tenuto insieme da una tabella di casi in JSON letta dai test di tutte e due le parti; il campo dichiarato come in A | È il disegno della 3.15; non tocca `allowlist.ts` | La stessa logica scritta due volte (~40 righe), che è la prima regola di `CLAUDE.md` §2; un sito nuovo si aggiunge in due posti, e il test prende solo i casi della tabella |
| C | **Solo l'host in C#**: un elenco di nomi, senza il riconoscimento | Pochissimo codice | Lascia passare il secondo e il terzo caso del §1: la pagina rotta si pubblica ancora |
| D | **Solo nel browser**: l'editor mette il blocco in «Che cosa manca per poterla pubblicare» e spegne «Pubblica» | Niente server | «Una regola solo nel browser non è una regola» (XVIII, nota `2026-10-28-il-pubblico-di-un-blocco`): chi approva e ogni altra strada non la vedono; la nota del 7 novembre l'ha già messa da parte (alternativa C) |

## 4. La raccomandazione: A

- **Una fonte sola**, il JSON degli host nel nucleo, letta dal browser per costruire l'`<iframe>` e dal server per giudicare.
- **`IBlockDescriptor.FramedAddress`** (predefinito nessuno), dichiarato per `video` ed `embed`: il solo campo delle `props` che
  il server guarda per questo scopo. Tenuto a zod da un test come `blockRequiredMedia.test.ts`.
- **`ContentPublishService`** rifiuta con una chiave nuova, `errors.content.addressNotFramed`, sul percorso del campo
  (`body.sections[i].blocks[j].props.url`): l'elenco «Che cosa manca per poterla pubblicare» la mette già accanto al blocco,
  come per la foto. Si fermano gli stessi tre gesti della 3.15 (Pubblica, Pronta per l'approvazione, l'approvazione), non il
  salvataggio della bozza.
- **Il Video senza indirizzo** si ferma anche lui (terzo caso del §1): sul sito mostra lo stesso avviso.
- ⚠️ Come per la 3.15: una pagina già online con un indirizzo così resta com'è, e si ferma quando qualcuno la ripubblica.
  Da scrivere nelle note di rilascio.

Se Carmine sceglie B, il resto (campo dichiarato, chiave, gesti fermati) non cambia: cambia solo dove sta l'elenco.

## 5. La domanda a Carmine

1. Un indirizzo che l'hub non incornicia (host fuori elenco, video non riconosciuto, Video senza indirizzo) ferma la
   pubblicazione sul server: **A** (l'elenco diventa un JSON del nucleo, una fonte sola), **B** (l'elenco copiato in C#,
   tenuto da una tabella di casi), **C** (solo gli host) o **D** (solo nel browser)? Raccomandazione: **A**.
2. È un'eccezione nuova, dichiarata, alla regola «il back end non replica gli schemi» di `CLAUDE.md` §2, accanto a
   `RequiredMedia`: **solo il campo dell'indirizzo incorniciato, solo per questo scopo**?

## La risposta di Carmine

Alle due domande del §5, in chat al master il 18 novembre 2026, scritta dal master sulla issue
(https://github.com/SkyMistery/Ivao-Italy-Hub/issues/524#issuecomment-6981533425): **«A, un JSON solo»**. Vale anche come sì alla
domanda 2: `IBlockDescriptor.FramedAddress` è la **seconda eccezione dichiarata** alla regola «il back end non replica gli
schemi», accanto a `RequiredMedia`, solo per l'indirizzo incorniciato e solo per questo scopo. Si fermano la pubblicazione,
l'invio all'approvazione e l'approvazione, non il salvataggio della bozza; e si fermano tutti e tre i casi del §1.

## Com'è fatto

- **La fonte unica** è `web/src/blocks/embedHosts.json`: per ogni sito gli host, le forme d'indirizzo che riconosce
  (`on`, `path` con segmenti letterali o `{nome}`, `query`) e il player che ne esce (`frame`, con `{parent}` riempito dal
  browser). L'espressione dell'identificativo sta nel file anche lei, scritta con una classe esplicita (`[A-Za-z0-9_-]`):
  il `\w` di .NET prende le lettere accentate, quello del browser no.
- ⚠️ **Dove sta il file**: tra i blocchi del nucleo nel browser, e non in `src/IvaoHub.Core/`, come diceva l'esempio del §3.
  Il server di sviluppo di Vite serve solo i file sotto `web/`: un import da fuori risponde 403 in sviluppo, e aprire
  `server.fs.allow` alla radice del repository esporrebbe anche `secrets/` e `config/ivao-oauth.json`. Il nucleo C# lo
  compila dentro di sé come risorsa (`IvaoHub.Core.csproj`, `LogicalName` `IvaoHub.Core.Content.embedHosts.json`), come
  i file del blocco interattivo: non si consegna un pacchetto senza.
- **Il browser**: `allowlist.ts` costruisce `EMBED_HOSTS` dal JSON; `hostOf` ed `embedSource` restano quelli di prima, e
  `allowlist.test.ts` passa senza una riga cambiata.
- **Il server**: `Core/Content/FramedAddresses.cs` risponde solo «lo incornicerebbe?», con le stesse regole. Il campo è
  `FramedAddress: "url"` su `video` ed `embed`; `BlockDocumentWalker.BlocksWithAddressesNotFramed` guarda solo quel campo;
  `ContentPublishService` rifiuta con `errors.content.addressNotFramed` sul percorso del campo, accanto a
  `mediaToChoose`.
- **Che cosa tiene insieme le due metà**:
  - `web/src/blocks/embedHosts.cases.json`, una tabella di indirizzi con il player atteso, letta da `embedHosts.test.ts`
    (il browser) e da `FramedAddressesTests` (il server): un caso aggiunto lì lo chiedono tutti e due;
  - `app/blockFramedAddress.test.ts` confronta `FramedAddress` con `.meta({ framed: true })` degli schemi, e cade per un
    blocco nuovo che ne dimentica una metà;
  - `embedHosts.test.ts` controlla che ogni player che il JSON sa costruire stia in `frame-src` di `config/security.json`,
    non solo quelli di un esempio.
- ⚠️ **Due differenze tra `URL` del browser e `Uri` di .NET**, trovate dalla tabella: una barra rovesciata prima della
  query per il browser è una barra (il server la tratta così); un escape nel percorso (`/%64Qw…`) il browser lo tiene, e
  .NET lo decodifica. Il server rifiuta un indirizzo con un `%` prima della query: un identificativo non lo contiene mai, e
  l'unico indirizzo che così si ferma e il browser incornicerebbe ha un escape in una parte che nessuno legge.

## Da portare nel piano

- **§9.3** (modello dei contenuti) e **§16** (meccanismi generici): un Video o un Embed con un indirizzo che l'hub non
  incornicia ferma la pubblicazione e l'invio all'approvazione; il blocco dichiara il campo in `IBlockDescriptor.FramedAddress`.
- **`CLAUDE.md` §2**, alla regola «the back end never replicates the schemas in C#»: la seconda eccezione dichiarata,
  `FramedAddress`, accanto a `RequiredMedia`, e la lista degli host in `web/src/blocks/embedHosts.json`, letta dalle due
  parti. La modifica la fa il master dopo il merge. (`docs/UI-GUIDELINES.md` lo dice già, in questa PR.)
- **Le note di rilascio**: una pagina già online con un indirizzo così resta online com'è; si ferma alla ripubblicazione.
