# Il loader è la precarica, non la riga che la schermata legge

**Data:** 7 settembre 2026 — trovato in G12 ricopiando `/start` a mano, **corretto lo stesso giorno**
**Dove è implementata:** G12 (`04-piano-implementazione-m1.md`), undici rotte di dettaglio
**Ricetta corretta:** design M0 §7.3, ricetta 2c

## Cosa succedeva

Ogni schermata di dettaglio del back-office leggeva la propria riga da `Route.useLoaderData()`. Un
`loader` di TanStack Router gira **alla navigazione e mai più**: non è un osservatore della cache di
React Query, quindi il `setQueryData` che ogni mutazione fa nel suo `onSuccess` non lo raggiunge, e
in tutta la SPA non c'è una sola chiamata a `router.invalidate()`.

Conseguenza: dopo il **primo** salvataggio riuscito il form continua a portarsi dietro il
`rowVersion` di quando la pagina è stata aperta. Il secondo salvataggio manda una versione vecchia,
il server fa esattamente il suo dovere e risponde **409**, e il messaggio dice

> Somebody else changed this in the meantime

incolpando qualcuno che non esiste.

**In pratica: ogni form del back-office si poteva salvare una volta sola per caricamento di pagina.**
Undici rotte: contenuti, news, documenti, dashboard, link, media, menu, categorie, calendario,
messaggi di contatto, permessi. La ricetta era stata copiata fedelmente, che è precisamente la
famiglia di difetto di HANDOFF §12.

Sulle schermate che tornano alla lista dopo il salvataggio il difetto era latente; sull'**editor dei
contenuti**, che resta sulla pagina e si salva molte volte di seguito, era il difetto principale — e
si è manifestato la prima volta che qualcuno ha ricopiato una pagina vera.

## Perché nessuno se n'era accorto

Due cose lo nascondevano, e vale la pena scriverle tutte e due.

1. **L'e2e del giro completo ricarica la pagina** prima di rimettere mano alla riga, con un commento
   che spiega il reload come una questione di tempi della pubblicazione. Quel reload rifà girare il
   loader, quindi maschera esattamente questo.
2. ⚠️ **`Route.useLoaderData()` in quei file è tipizzato `never`.** `never` è assegnabile a
   qualunque cosa, quindi TypeScript non ha mai controllato niente su quelle righe: una schermata
   poteva ricevere qualunque tipo di riga e compilare. Verificato con una sonda
   (`const probe: never = Route.useLoaderData();` compila; `const probe: null = …` pure). È un
   secondo difetto sotto il primo, e la correzione lo chiude di sponda perché smette di dipendere da
   quella chiamata.

## La decisione

**Il `loader` resta, e resta `ensureQueryData`: è la precarica.** È quello che fa arrivare la
schermata con i dati già in mano, ed è il motivo per cui esiste. Quello che la schermata **legge** è
la query che il loader ha riempito:

```tsx
// una rotta il cui indirizzo può essere `new`
const row = useQuery({ ...contentQuery(Number(id)), enabled: id !== 'new' }).data ?? null;

// una rotta che indirizza sempre una riga esistente
const media = useQuery(mediaQuery(Number(id))).data;
// … dopo gli altri hook:
if (media === undefined) return null;
```

Così il `setQueryData` che le mutazioni già facevano arriva davvero allo schermo, il `rowVersion` è
sempre quello dell'ultima risposta del server, e il 409 torna a voler dire quello che dice.

**Costo misurato**: una `GET` in più all'apertura di ogni schermata di dettaglio, perché la query
monta con `staleTime` a zero sulla riga che il loader ha appena preso. Una richiesta, e in cambio la
schermata è fresca anche quando si torna indietro. Se dovesse dare fastidio si mette uno `staleTime`
sulle query di dettaglio, non si torna al loader.

## Come si verifica

- **Comportamentale, contro l'API vera**: due salvataggi di seguito dallo stesso caricamento di
  pagina. Con la correzione passano tutti e due (misurato: `updated_at` si muove due volte). Rimessa
  la vecchia riga, il secondo dà «Somebody else changed this in the meantime» — cioè il difetto
  riprodotto a comando.
- **Di regressione**: `web/src/routes/routes.test.ts` — nessun file di `routes/_staff/` legge
  `useLoaderData` nel **codice**. ⚠️ La prima stesura guardava il testo del file e falliva su tutti e
  undici, perché ognuno porta un commento che spiega perché il loader non si legge: il test toglie i
  commenti prima di guardare, e c'è un caso che lo dice.

⚠️ **Le rotte pubbliche continuano a usare il loader**, ed è giusto: `_public/$slug`, le news e i
documenti pubblici non salvano niente, quindi non hanno una versione da tenere fresca.
