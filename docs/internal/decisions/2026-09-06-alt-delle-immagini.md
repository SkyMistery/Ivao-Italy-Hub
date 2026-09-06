# L'alt di un'immagine non si eredita dalla libreria

**Data:** 6 settembre 2026 — aperta scrivendo G3, decisa da Carmine prima di scrivere gli schemi
**Stato:** **decisa**. Design M1 §1.2 e `docs/UI-GUIDELINES.md` corretti nello stesso cambio.
**Regola applicata:** `CLAUDE.md` §5. Non è un caso (c): la decisione è **non** costruire il
meccanismo che l'eredità richiederebbe. Il task si chiude, e questa nota dice perché.

## Cosa dicevano i documenti

Design M1 §1.2, riga del blocco `image`: «`alt` vuoto **eredita** quello della media library: l'alt
si scrive una volta accanto al file, non a ogni uso». `docs/UI-GUIDELINES.md` diceva la stessa cosa.

L'intenzione è giusta e vale la pena tenerla in vista: un'immagine descritta tre volte in tre modi
diversi, e la quarta volta non descritta affatto, è esattamente ciò che succede quando l'alt si
riscrive a ogni uso.

## Perché non regge, verificato

Il renderer pubblico riceve **solo il corpo pubblicato**: `PublicContentDto` porta `Body`,
`Title`, `Summary`, `Seo` e niente altro. Di una media dentro `props` il browser conosce **l'id** e
nient'altro; l'indirizzo del file lo può costruire (`/media/{id}/{nome}`), l'alt no.

Le tre strade per l'eredità, e cosa costa ognuna:

1. **Il server risolve l'alt alla pubblicazione**, come cattura i blocchi Data. Per farlo dovrebbe
   sapere che dentro `props` esiste un campo `alt` legato a un campo `mediaId` — cioè leggere le
   `props`. È la cosa che il piano §16.5 vieta esplicitamente: il backend conosce l'envelope, mai le
   `props`. **Scartata: non è un compromesso, è la regola che regge tutto il modello dei contenuti.**
2. **Un endpoint pubblico dei metadati della media**, interrogato dal blocco. Funziona, ed è
   l'eredità vera e viva. Costa il **secondo endpoint scritto a mano di M1** (§E lo chiama «un evento
   da riportare nel rapporto di chiusura») e trasforma un blocco Content in un blocco che fa una
   richiesta — una per immagine, su una pagina che ne può avere venti.
3. **L'editor copia l'alt al momento della scelta.** Nessuna richiesta pubblica, ma l'eredità
   diventa una copia che non si aggiorna più, e il generatore di form dovrebbe imparare che
   `mediaId` e `alt` sono una coppia: un meccanismo nuovo dentro `SchemaForm`, caso (c), per ottenere
   qualcosa che non è nemmeno l'eredità promessa.

## Decisione

**`alt` è una props tradotta opzionale del blocco, e basta.** Vuota significa **immagine
decorativa**, e il blocco rende `alt=""`, che è ciò che fa saltare l'immagine a un lettore di
schermo. Non significa «leggi il nome del file»: un nome di file letto ad alta voce è peggio del
silenzio.

Il campo porta un `hints.alt` nei file di lingua che lo dice al redattore: *cosa dice l'immagine, per
chi non la vede; lascialo vuoto se è decorativa*.

## Cosa si è perso, e dove tornerebbe

L'alt scritto una volta accanto al file resta un'idea buona, e la libreria continua a portarne uno
(`cms_media.Alt`): serve alla libreria stessa e al selettore. Se un giorno lo si vuole anche sulle
pagine, la strada è la (2), e la sua casa naturale è la fase che porta il sito pubblico (G8) o il
rapporto di chiusura di M1 — non G3, che consegnerebbe sedici blocchi più un endpoint.

## Cosa cambia nei documenti

- design M1 §1.2, riga `image`: la nota sull'eredità diventa la regola dell'`alt` vuoto;
- `docs/UI-GUIDELINES.md`, «How a block names a file»: stessa correzione, in inglese;
- gli schemi: `alt: localized().optional()` con il commento che rimanda a questa nota.
