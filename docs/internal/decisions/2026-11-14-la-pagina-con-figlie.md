# La pagina con figlie: che cosa succede quando si cancella

- **Data**: 14 novembre 2026
- **Stato**: Proposta — la domanda è di Carmine
- **Chi**: una sessione di lavoro di Carmine (issue #321, punto 3; lotto 3 della revisione del codice,
  `2026-10-20-la-revisione-del-codice.md`)
- **Classificazione** (`CLAUDE.md` §5): **(b)**, un uso del CRUD generico del nucleo. Con la raccomandazione serve una piccola
  estensione del meccanismo (`CrudSaving` che dice «è una cancellazione»), quindi codice del nucleo, scritto solo dopo la risposta.

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

## Che cosa si tocca, con la raccomandazione

- `CrudSaving` riceve `IsDelete` (oggi dice solo `IsNew`): `BeforeAuthorize` gira già sulla riga salvata prima di una cancellazione, ma
  non sa che lo è. È l'unica estensione del meccanismo; nessun meccanismo nuovo.
- Il CRUD dei contenuti, in `ContentEndpoints`, chiede in quel punto se esiste una pagina con `ParentId` uguale (fuori dal filtro della
  query: una figlia di un altro dipartimento conta lo stesso) e rifiuta.
- Una chiave nuova in `locales/*/errors.json`; un test d'integrazione (madre con figlia: 400; figlia cancellata, poi la madre: 204).
- Il form della pagina non cambia: il rifiuto arriva come gli altri, sopra il form.

**Le pagine già orfane**, se ce ne sono sulla prova o in produzione, restano come sono: le sposta chi ha `Content.Approve`, da
«Posizione» nel form, oppure si cerca il genitore che manca con una query
(`SELECT id, slug, parent_path FROM cms_contents c WHERE parent_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM cms_contents p WHERE p.id = c.parent_id)`).
La raccomandazione impedisce che ne nascano altre; non le ripara.

## La domanda per Carmine

⚖️ Quando si cancella una pagina che ha pagine sotto di sé: **(1) si rifiuta**, finché chi cancella non sposta o cancella le figlie
(raccomandata); (2) le figlie vanno in cima; (3) si cancellano anche loro?

## Risposta di Carmine

_(da scrivere con il link al suo commento)_

## Da portare nel piano

- §9.3 (il modello dei contenuti): una pagina con figlie non si cancella; le figlie si spostano o si cancellano prima.
- §16 (i meccanismi generici), il CRUD generico: `CrudSaving.IsDelete`, letto da `BeforeAuthorize`.
