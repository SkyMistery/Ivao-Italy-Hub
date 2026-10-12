# Il pulsante di chiusura delle finestre (E10l)

**Data:** 11 ottobre 2026 — fase E10l di M4, PR del nucleo #250
**Stato:** **decisa**. Carmine sull'issue #224 ([la sua risposta][a224], data in chat alla sessione master l'11 ottobre e pubblicata
su sua istruzione, autore `SkyMistery`), al [rilievo][r224] dello stesso giorno: **sì, comporlo nel nucleo**, come la #238 fa per la
paginazione — i dialog dell'hub con un pulsante di chiusura loro, con un'etichetta tradotta —, in una PR piccola del nucleo di
dalberone dopo la #238, con la sua nota. **Non** una richiesta ad Atmosphere. La forma (§2) — una voce nuova dell'elenco chiuso e non
un cambio dentro una che c'è — è quella che la risposta descrive («i dialog dell'hub con un pulsante loro»); la nota la scrive perché
il revisore la controlli.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**, come E10k: Atmosphere fa quasi quello che serve, e il nucleo lo ricompone dai
suoi pezzi; ma qui il pezzo ricomposto è un componente a sé, quindi una voce dell'elenco chiuso (`docs/UI-GUIDELINES.md` §3, piano
§8.3), che è una decisione: presa da Carmine. PR del nucleo a sé, prima del codice dei moduli che lo usa (§0 regola 6).

[r224]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/224#issuecomment-6107362979
[a224]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/224#issuecomment-6113461064

## 1. Che cosa serve

Visto da dalberone sul banco di prova l'11 ottobre (sulla build di E7 con E6a ed E6b): la «X» in alto a destra del dialog di uno slot
ha per il lettore di schermo la parola inglese **«Close»**, anche con l'interfaccia in italiano.

**Misurato nel pacchetto di Atmosphere 3.1.0** (`dist/atmosphere-react.js` e `react-components.d.ts`), con una correzione al
rilievo: la «X» non la disegna `DialogContent`, che è solo il portale, lo sfondo e il contenuto di Radix. La disegna
**`DialogTopRightClose`**, un `Close` di Radix con l'icona `x` di lucide e `<span class="sr-only">Close</span>` scritto nella libreria
(i figli dati dal chiamante sono sovrascritti). La montano:

- la molecola **`Dialog`** (`trigger`, `title`, `description`, `children`, `footer`, `topRightCloseButton` che vale `true`);
- **`SheetContent`** (e quindi la molecola `Sheet`), con la stessa «X» e lo stesso «Close», e un «Close» visibile sul bottone del
  piede (`footerCloseButton`).

Chi le usa, in `main` a `bea8c0e`: **solo `EventSlots.tsx`** degli eventi (il dialog di uno slot). Sui branch aperti anche
**`PrivateSlots.tsx`** di E7 (#246, e E8b #249 sopra), il dialog dell'ora privata. **I due dialog del nucleo non hanno la «X»**:
`ConfirmDialog` è un `AlertDialog`, chiuso dai suoi due bottoni; la palette di ricerca (`SearchPalette`) è un `DialogContent` composto
a mano, chiuso da Esc o da un clic fuori. Nessun `Sheet` in tutto il codice; i tour nessun dialog.

## 2. La forma nel codice

1. **`Dialog` del nucleo** (`web/src/shared/ui/Dialog.tsx`), la **venticinquesima voce** di `web/src/shared/ui/catalog.ts`. **Voce
   nuova e non un cambio dentro una che c'è**: l'unica voce che disegna un dialog è `ConfirmDialog`, che fa un'altra cosa (chiede, con
   due bottoni, senza «X»); metterci dentro un dialog che mostra e si chiude sarebbe un secondo componente sotto il nome del primo. È la
   seconda voce che sostituisce un pezzo di Atmosphere invece di aggiungerne uno che manca, per la ragione della prima (`StaffSidebar`:
   una parola inglese scritta nella libreria).
2. **Le stesse props della molecola di Atmosphere**: `trigger` (una stringa diventa un `Button` `outline`, altrimenti è il trigger
   stesso, con `asChild`, come fa Atmosphere), `title`, `description`, `children`, `footer`, e le props del root di Radix (`open`,
   `onOpenChange`, `defaultOpen`, `modal`). Una sola differenza di nome: **`closeButton`** al posto di `topRightCloseButton`, sempre
   `true` se non detto. Lo stesso nome del componente, `Dialog`: il passaggio degli eventi cambia l'import e basta.
3. **Gli stessi pezzi, le stesse classi, lo stesso ordine** (header, contenuto, «X», piede: il primo elemento che prende il fuoco non
   cambia): `DialogRoot`, `DialogTrigger`, `DialogContent` con `sm:max-w-md`, `DialogHeader`, `DialogTitle`, `DialogDescription`,
   `DialogFooter`. **La «X» è nostra**: `DialogClose` con le classi di `DialogTopRightClose`, l'icona `X` di `lucide-react` (la stessa
   che Atmosphere usa) `aria-hidden`, e `aria-label={t('common.close')}`.
4. **Nessuna dipendenza nuova**: Radix non è una dipendenza diretta di `web/`, ma Atmosphere esporta il `Close` di Radix come
   **`DialogClose`** (e il `Root` come `DialogRoot`, il `Trigger` come `DialogTrigger`). `package.json` e il lockfile non cambiano.
5. **Una chiave nuova del nucleo**, `common.close`: «Close» e «Chiudi». Nessun doppione nei file dei moduli (cercato prima: nessun
   «Chiudi»; l'unico «Close» è `closeAt` dei tour, l'ora in cui una cosa chiude, un altro senso). Per la galleria `uiKit.sample.dialogTrigger` e `uiKit.sample.dialogTitle`.
6. **La galleria** (`/staff/admin/ui-kit`) ha la sua sezione `Dialog` (`DialogSample`), come ogni voce: `uiKit.test.ts` cade senza.
7. **Una guardia** in `web/src/shared/ui/Dialog.test.tsx`: nessun file di `web/src` importa da Atmosphere `Dialog`,
   `DialogTopRightClose`, `Sheet` o `SheetContent` (anche `type …` o `… as …`), **tranne** quelli della lista `NOT_MOVED_YET`:
   `modules/events/screens/EventSlots.tsx` e `modules/events/screens/PrivateSlots.tsx`. Il secondo **non è ancora in `main`**: è di E7
   (#246), aperta accanto a questa; è già nella lista perché nessuna delle due PR faccia cadere l'altra, chiunque entri prima. Per lo
   stesso motivo il test chiede che nessun file stia **fuori** dalla lista, e non che la lista sia uguale: un file spostato che resta
   nella lista non fa cadere niente, e la fase degli eventi la svuota (§4).

**I test** (vitest, 5 in `Dialog.test.tsx`): la «X» si chiama «Chiudi» in italiano e nel dialog non resta nessun «close»; in inglese
si chiama «Close», la parola del file, e chiude il dialog; titolo, descrizione e contenuto come in Atmosphere; senza `closeButton`
nessun bottone; la guardia. **Con il codice di oggi** — il `Dialog` di Atmosphere al posto del nostro, i test tenuti — cadono il primo
(«Unable to find … name "Chiudi"») e la guardia (il file che importa il `Dialog` di Atmosphere); gli altri tre passano, perché sono
ciò che non cambia.

## 3. Le alternative

1. **`aria-label` dato a `DialogTopRightClose`** (le props passano al bottone, e il nome accessibile lo prende `aria-label`). Scartata:
   il `<span>Close</span>` resta nel DOM, il testo del bottone resta inglese, e la molecola `Dialog` non lo passa comunque — andrebbe
   ricomposta lo stesso.
2. **Chiederlo ad Atmosphere** (una prop per l'etichetta). Esclusa da Carmine.
3. **Una patch del pacchetto** (`pnpm patch`). Scartata: una copia modificata di una libreria da tenere a ogni aggiornamento, per una
   parola che il nucleo può dire da sé.
4. **Raccomandata e fatta**: il `Dialog` del nucleo (§2).

## 4. Fuori da questa PR

- **I dialog degli eventi** (`EventSlots.tsx`, e `PrivateSlots.tsx` quando E7 entra): sono di dalberone e passano al `Dialog` del
  nucleo in una **fase piccola degli eventi dopo questa** (regola 6), che cambia l'import, e `topRightCloseButton` in `closeButton` se
  qualcuno lo passa (oggi nessuno), e toglie i due file da `NOT_MOVED_YET`.
- **I tour**: nessun dialog oggi; se ne avranno, sono del maintainer, e la guardia lo dice.
- ⚠️ **Lo stesso difetto nei toast, non deciso**: la molecola `Toast` di Atmosphere, che `useNotice()` usa per `Notice` in forma di
  toast, monta `ToastClose` senza props, e il suo `aria-label` predefinito è «Close»: anche la «X» di un toast è inglese sotto
  un'interfaccia italiana. Non è un dialog e la risposta di Carmine non lo copre: segnalato a dalberone, una domanda per Carmine.

## Da portare nel piano

- **§8.3, l'elenco chiuso** (il paragrafo ⚠️ dopo i componenti custom): la **venticinquesima** voce, `Dialog` — il dialog che mostra e
  si chiude con la sua «X» tradotta, ricomposto dai pezzi di Atmosphere perché il suo `Dialog` scrive «Close» nella libreria; la
  seconda voce, dopo `StaffSidebar`, che sostituisce un pezzo di Atmosphere invece di aggiungerne uno che manca.
- **§8.3, i componenti da Atmosphere**: «Dialog/Sheet (booking, form rapidi)» si legge ora con i pezzi di Atmosphere e la molecola
  del nucleo — il `Dialog` e il `Sheet` di Atmosphere non si montano, perché la loro «X» dice «Close»; si monta il `Dialog` del nucleo
  (e `ConfirmDialog` per una domanda).
