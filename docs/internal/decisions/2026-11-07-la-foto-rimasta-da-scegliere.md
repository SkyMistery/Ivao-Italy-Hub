# La foto rimasta da scegliere: come il server sa che è obbligatoria

- **Data**: 7 novembre 2026
- **Stato**: **Proposta**, domanda per Carmine. Niente codice sui blocchi finché non ha risposto
- **Chi**: la sessione di lavoro della PR 3.15 (design `12-design-editor-fase-3.md` §5.9, riga 3.15 del §7)
- **Classificazione** (`CLAUDE.md` §5): **(c)** per i blocchi: serve un fatto nuovo del tipo di blocco. Lo sfondo di una sezione è **(b)**: sta nella busta, e il server la legge già
- **Completa** il punto 11 di `2026-11-04-le-scelte-dell-import-da-claude.md`, che non si riscrive

## 1. Che cosa serve

Carmine ha deciso che una foto rimasta da scegliere ferma la pubblicazione, per ogni pagina, importata o scritta a mano (5 novembre 2026, «Sì, la ferma»). Il server però non legge gli schemi (`CLAUDE.md` §2) e il design rimanda qui.

Una foto da scegliere, in un corpo salvato, è una chiave che **non c'è**: il form non scrive un campo vuoto, e l'import lo toglie. Il server che cammina il JSON vede `{ "width": "full" }` e non può sapere che manca qualcosa: un `hero` senza foto ha la stessa forma ed è giusto. La convenzione del nome (`mediaId`) dice dov'è un file **che c'è**, non dove ne manca uno.

## 2. I casi di oggi, misurati

Gli schemi di tutti i blocchi registrati (`web/src/blocks/schemas.ts`; i moduli non ne hanno):

| Dove | Che cosa | Obbligatoria? |
|---|---|---|
| `image` | `mediaId` | sì |
| `gallery` | `images[].mediaId` | sì, per ogni voce |
| `logoGrid` | `items[].mediaId` | sì, per ogni voce |
| `hero`, `cardGrid.cards[]`, `testimonial` | `mediaId` | no |
| una sezione con `background: "image"` | `mediaId` della sezione | sì, ed è nella busta |

Sono **tre blocchi su quarantasei** (33 del nucleo, 13 dei moduli). Una galleria senza voci non ha una foto da scegliere: non ha foto, e resta com'è.

## 3. Che cosa non serve chiedere: lo sfondo di una sezione

`background` e `mediaId` di una sezione sono nella busta, che il server già controlla (`CheckBackground`). Una sezione con lo sfondo a immagine e senza immagine si rifiuta alla pubblicazione senza nessun meccanismo nuovo, sul percorso `body.sections[0].mediaId`.

## 4. Le alternative per i blocchi

| | Che cosa | Pro | Contro |
|---|---|---|---|
| **A** | **Un fatto del tipo, nella metà C#**: `BlockDescriptor.RequiredMedia`, per esempio `["images[].mediaId"]`, accanto ad `Audience`. Il server guarda solo quei percorsi, e un test confronta il C# con quello che dice zod | Vale per ogni corpo, anche per quelli già salvati e per chi approva; è il pattern di `AlwaysLive` e `Audience`; tre righe | È un pezzettino di schema detto due volte (`.optional()` in zod, il percorso in C#): lo tiene insieme il test, non il compilatore |
| B | **Un valore nel corpo**: per un campo obbligatorio il browser scrive `"mediaId": null`, e il server legge «chiave presente e vuota» | Il server resta cieco, nessun dichiarato in C#; è una convenzione come `href` | Un corpo salvato non si riscrive (design §2.4): i blocchi già salvati sfuggono senza dire niente. Il form e l'import oggi tolgono quella chiave, e zod rifiuta `null`: tocca schemi, form e impronta delle istruzioni |
| C | **Solo nel browser**: l'editor spegne «Pubblica» leggendo zod | Niente server | «Una regola solo nel browser non è una regola» (XVIII, nota `2026-10-28-il-pubblico-di-un-blocco`); il pannello di chi approva e ogni altra strada non la vedono |
| D | **Un elenco generato da zod** alla build e letto dal server | Una fonte sola | Un generatore nuovo che deve montare il registro del browser, e un passo in più in CI, per tre blocchi |

## 5. La raccomandazione: A

- **`IBlockDescriptor.RequiredMedia`**, predefinito vuoto: i percorsi dentro `props` che devono avere un file, nella forma `mediaId` o `<lista>[].mediaId`. Dichiarati per `image`, `gallery`, `logoGrid`.
- **`BlockDocumentWalker`** ne ricava i percorsi vuoti, senza leggere altro delle `props`. Una lista che manca o è vuota non è un problema; una voce senza file sì.
- **`ContentPublishService`** rifiuta con una chiave nuova, `errors.content.mediaToChoose`, sul percorso del campo: l'elenco «che cosa manca per pubblicare» lo mette già accanto al blocco.
- **Il legame con zod**: un test di `web/` legge i `RequiredMedia` dal C# con un pattern stretto, come già fa `modules/manifest.test.ts` per i blocchi dei moduli, e li confronta con i campi `.meta({ media: true })` non facoltativi degli schemi. Un blocco che dimentica una delle due metà fa fallire la build. Niente in `/api/me`: il browser non ne ha bisogno, perché il problema gli arriva già dal server.
- ⚠️ **Una pagina già online che oggi ha un'immagine senza file** non cambia finché è online; si ferma quando qualcuno la ripubblica, e l'elenco dice quale blocco. Da scrivere nelle note di rilascio.

## La domanda per Carmine

Come sa il server che una foto è obbligatoria: **A** (raccomandata), B, C o D?

## Da portare nel piano

- **§9.3** (modello dei contenuti) e **§16** (meccanismi generici): una foto rimasta da scegliere ferma la pubblicazione; lo sfondo di una sezione dalla busta. Il meccanismo dei blocchi dopo la risposta di Carmine.
