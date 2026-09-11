# Due immagini identiche nella libreria

**Data:** 12 settembre 2026 — Carmine, mentre sceglieva le comodità dell'editor (piano 0.63): «se
provo a caricare due immagini identiche che succede? Puoi verificare?».

**Stato:** verificato l'11 settembre, **deciso da Carmine il 12 settembre** («procediamo con le
immagini identiche»), costruito lo stesso giorno sul branch `m1/media-dedupe`.

## Che cosa succedeva

Niente. `MediaEndpoints.UploadAsync` scriveva i byte sotto un nome nuovo (`MediaStorage.SaveAsync`,
un Guid per file) e poi la riga: **due righe e due file**, senza confronto né sul nome né sul
contenuto. Un logo caricato tre volte era tre logo, tre `alt` da scrivere, tre voci nel selettore.

## Perché è (c) e non (b)

Nessun meccanismo esistente sa che cosa c'è *dentro* un file: la riga tiene nome, tipo, dimensioni
e misure, e nessuna delle quattro dice «è lo stesso file». Serve una cosa nuova, piccola: **l'impronta
del contenuto** sulla riga.

## Che cosa si fa

- **Una colonna** `cms_media.sha256` (char(64), null per le righe di prima), con un indice su
  `(owner_department, sha256)`. Migrazione additiva `AddMediaSha256`.
- **L'impronta si calcola mentre i byte vanno su disco**: `MediaStorage.SaveAsync` copia e fa
  l'hash nello stesso passaggio e risponde con nome e impronta insieme. Niente seconda lettura.
- **Se nel dipartimento c'è già una riga viva con la stessa impronta** — non cancellata, con il file
  ancora su disco — il file appena scritto viene tolto e **la risposta è quella riga**, `200 OK`
  invece di `201 Created`. La SPA lo dice: la libreria porta alla scheda del file che c'era già con
  l'avviso «era già in libreria»; il selettore dell'editor sceglie quello e non dice niente, perché
  è esattamente il risultato che chi caricava voleva.
- **Per dipartimento, mai attraverso**: un file di un altro dipartimento non è di chi carica, ha la
  sua visibilità e il suo `alt`; ognuno tiene la sua copia. È il prezzo, ed è quello giusto.
- **Nessuna condivisione dei byte fra due righe**: si sarebbe potuto scrivere una seconda riga che
  punta allo stesso `stored_name`, ma la cancellazione toglie il file «solo quando nulla di
  pubblicato lo nomina», e due righe su un file sono due modi di sbagliare quel conto.

## Che cosa non si fa

- **Le righe di prima non hanno impronta** e non la ricevono: un job che rilegge tutta la libreria
  per calcolarla è più codice di quanto il caso valga oggi. Un secondo caricamento di un file che
  c'era già prima del 12 settembre resta una seconda riga, come prima; dal terzo in poi no.
- Niente confronto «quasi uguale» (stessa immagine ricompressa): è un'altra domanda.

## Che cosa tocca

`MediaAsset`, `CmsSchemaConfiguration`, `MediaStorage`, `MediaEndpoints`, una migrazione, la
mutazione `useUploadMedia` e i suoi due chiamanti, due chiavi i18n, un test di integrazione
(stesso file due volte nello stesso dipartimento: una riga, un file; in un altro dipartimento: due).
Nessun componente nuovo, nessun endpoint nuovo.
