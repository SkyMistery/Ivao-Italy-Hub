# La pagina con figlie: che cosa succede quando si cancella

- **Data**: 14 novembre 2026
- **Stato**: Decisa da Carmine in chat al master, 14 novembre 2026: «Si rifiuta» (l'alternativa 1). Il link al commento che la
  riporta sulla PR è sotto, in «Risposta di Carmine».
- **Chi**: una sessione di lavoro di Carmine (issue #321, punto 3; lotto 3 della revisione del codice,
  `2026-10-20-la-revisione-del-codice.md`)
- **Classificazione** (`CLAUDE.md` §5): **(b)**, estensione di un meccanismo del nucleo, il CRUD generico (`MapCrud`): `CrudSaving`
  dice anche «è una cancellazione». Nessun meccanismo nuovo.

## Che cosa succede oggi

Una pagina si mette sotto un'altra con `ParentId` (nota `2026-09-13-contenuti-centralizzati.md`, 3.7: l'indirizzo si compone).
`ParentId` non ha una chiave esterna (`CmsSchemaConfiguration`), e il CRUD dei contenuti ha `AllowDelete` com'è di default: chi può
modificare la pagina madre la cancella, e nessuno guarda le figlie.

Le figlie restano online al loro indirizzo (`/training/guida` resta `/training/guida`, perché il percorso del genitore è salvato sulla
riga). Ma ogni loro salvataggio, anche quello automatico dell'editor, passa da `ContentAddresses.PrepareAsync`, che cerca il genitore,
non lo trova e risponde `errors.content.address.invalidParent`. La pagina non si salva più finché qualcuno non la sposta; e spostarla
in cima chiede `Content.Approve`, che chi la scrive di solito non ha. Chi l'ha scritta vede un rifiuto che non capisce, su una pagina
che non ha toccato.

## Le alternative

1. **Rifiutare la cancellazione di una pagina che ha figlie** (raccomandata). La risposta è un 400 con una chiave sola
   (`errors.content.address.hasChildren`, «Questa pagina ha delle pagine sotto di sé: spostale o cancellale prima»), sul campo della
   riga, come gli altri rifiuti del CRUD. Non cambia niente da solo: chi cancella decide che cosa fare delle figlie, che possono essere
   di un altro dipartimento. Si torna indietro senza perdere niente. Il prezzo: per togliere una sezione intera si cancella dal basso.
2. **Spostare le figlie in cima.** La cancellazione riesce e le figlie cambiano indirizzo (quello vecchio resta come indirizzo lasciato
   indietro, e arriva ancora). Ma mette pagine in cima al sito senza `Content.Approve`, che è proprio il permesso che la cima chiede
   (nota 2026-09-13, 3.7), e sposta pagine di altri dipartimenti senza che lo sappiano. Un indirizzo in cima può anche essere già
   preso: servirebbe una regola in più per il conflitto.
3. **Cascata: cancellare anche le figlie, fino in fondo.** Una cancellazione sola toglie un ramo intero, anche le pagine di altri
   dipartimenti e quelle che chi cancella non può modificare. Non si torna indietro. Sconsigliata.
4. **Accettare il genitore che manca** in `ContentAddresses`, tenendo il percorso salvato. Il salvataggio torna a funzionare, ma il
   sito ha un ramo senza radice: la pagina madre manca da `/training` e le figlie restano sotto un indirizzo che non risponde.
   Nasconde il buco invece di chiuderlo.

## Perché nessun meccanismo esistente bastava

Il CRUD generico ha quattro ganci per riga: `BeforeAuthorize`, `BeforeSave`, `AfterSave`, `Delete`. Una cancellazione ne fa girare uno
solo, `BeforeAuthorize`, sulla riga salvata; ma `CrudSaving`, quello che il gancio riceve, diceva solo `IsNew`, e un aggiornamento e una
cancellazione gli arrivavano uguali. `BeforeSave` su una cancellazione non gira, e farcelo girare avrebbe fatto girare anche quello che
i contenuti ci fanno (l'indirizzo, lo spostamento delle figlie) su una riga che sparisce. `Delete` sostituisce la rimozione e non può
rifiutare. `DeletePolicy` chiede un permesso, non un fatto di altre righe. Un controllo nell'endpoint, fuori dal CRUD, sarebbe stato il
«controllo scritto a mano» che `CLAUDE.md` §2 vieta.

## Che cosa si tocca

- `CrudSaving` riceve `IsDelete` (in fondo, con `false` di default: chi lo costruisce oggi non cambia), passato a `BeforeAuthorize`
  dalla cancellazione. È l'unica estensione del meccanismo; i ganci che già c'erano (Eventi) non lo leggono e fanno quello che facevano.
- `ContentAddresses.DeletionProblemAsync`: se la riga è una pagina (non un modello) e una riga ha `ParentId` uguale — letta fuori dal
  filtro della query, perché una figlia di un altro dipartimento o in bozza conta lo stesso — rifiuta con
  `errors.content.address.hasChildren` sul campo `id`. Sta con gli indirizzi, che sono la cosa di cui parla.
- Il CRUD dei contenuti, in `ContentEndpoints`, la chiede da `BeforeAuthorize` quando `IsDelete`.
- La chiave nuova in `locales/*/errors.json`; il test d'integrazione `PageAddressTests.APageWithPagesUnderItIsNotDeletedUntilTheyAreGone`
  (madre con figlia: 400; figlia cancellata, poi la madre: 204).
- Il form della pagina non cambia: il rifiuto arriva nel riquadro dei rifiuti del pulsante «Elimina» (`useRefusalNotice`, #332).
- ⚠️ `BeforeAuthorize` gira **prima** del permesso: chi non può cancellare la pagina e prova lo stesso si sente dire che ha delle figlie
  invece di un 403. Non rivela niente che l'albero delle pagine non dica già a chi scrive.

**Le pagine già orfane**, se ce ne sono sulla prova o in produzione, restano come sono: le sposta chi ha `Content.Approve`, da
«Posizione» nel form, oppure si cerca il genitore che manca con una query
(`SELECT id, slug, parent_path FROM cms_contents c WHERE parent_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM cms_contents p WHERE p.id = c.parent_id)`).
La raccomandazione impedisce che ne nascano altre; non le ripara.

## La domanda per Carmine

⚖️ Quando si cancella una pagina che ha pagine sotto di sé: **(1) si rifiuta**, finché chi cancella non sposta o cancella le figlie
(raccomandata); (2) le figlie vanno in cima; (3) si cancellano anche loro?

## Risposta di Carmine

«Si rifiuta» — l'alternativa 1, data in chat al master il 14 novembre 2026 e riportata dal master sulla PR: _(link al commento da
aggiungere quando il master lo pubblica)_.

## Da portare nel piano

- §9.3 (il modello dei contenuti): una pagina con figlie non si cancella; le figlie si spostano o si cancellano prima.
- §16 (i meccanismi generici), il CRUD generico: `CrudSaving.IsDelete`, letto da `BeforeAuthorize`.
