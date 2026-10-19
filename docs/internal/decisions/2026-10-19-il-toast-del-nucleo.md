# Il toast del nucleo (E10r)

**Data:** 19 ottobre 2026 — fase E10r di M4, PR del nucleo #308
**Stato:** **decisa**. Carmine sull'issue #266, in due risposte date in chat alla sessione master e pubblicate su sua istruzione (autore
`SkyMistery`): la [prima][a266] (16 ottobre), che **sostituisce** quella di poco prima — **non** chiedere ad Atmosphere (la (b)), **ma
(a): un toast del nucleo**, con il suo provider, la sua coda e il suo viewport su `@radix-ui/react-toast` come dipendenza diretta di
`web/`, disegnato con le classi di Atmosphere, con le tre parole tradotte in `locales/{lang}/common.json`, al posto del `ToastProvider` di
Atmosphere e dietro `useNotice`, una voce dell'elenco chiuso come `Dialog`, mostrata dalla galleria, con una guardia come quella di E10l,
e una nota che dica quali classi di Atmosphere copia; non (c) né (d); la [seconda][w266] (18 ottobre): **E10r la scrive dalberone**, una
fase del nucleo, la sua PR con la sua nota, da `main`. L'[ultima parola del revisore][m266] (19 ottobre): la #301 è unita, E10r è il passo
dopo.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**, come E10l: un pezzo che Atmosphere dà quasi, ripreso dal nucleo con le sue parole;
qui il pezzo è un componente a sé, quindi una voce dell'elenco chiuso (`docs/UI-GUIDELINES.md` §3, piano §8.3), decisa da Carmine. PR
del nucleo a sé (§0 regola 6); nessuna schermata cambia, quindi nessuna fase di modulo dopo.

[e266]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/266#issuecomment-6213206387
[a266]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/266#issuecomment-6216418088
[w266]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/266#issuecomment-6253185099
[m266]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/266#issuecomment-6256810723

## 1. Che cosa serve

Ogni toast dell'hub — tutti passano da `useNotice()` (`web/src/shared/ui/notices.ts`) — diceva al lettore di schermo tre parole inglesi
scritte in Atmosphere 3.1, anche sotto l'interfaccia italiana:

- **«Close»**, l'`aria-label` predefinito di `ToastClose`, che la molecola `Toast` monta senza props;
- **«Notification»**, il `label` predefinito del provider di Radix, che il `ToastProvider` di Atmosphere non passa: precede l'annuncio
  di ogni toast nella regione viva che Radix apre;
- **«Notifications (F8)»**, il `label` predefinito del viewport di Radix, il nome della `region` dove i toast compaiono.

Misurato su E10r il 16 ottobre ([il commento di dalberone][e266]): Atmosphere esporta solo `Toast`, `ToastProvider` e `useToast`; gli
atomi sono dichiarati nel `d.ts` ma non esportati; la coda prende solo props e le disegna con la sua molecola; Radix è impacchettato
dentro il `dist` di Atmosphere, quindi un pezzo nostro non può stare dentro il suo provider. Per questo la prima risposta (comporlo dagli
atomi, come `Dialog`) non si poteva fare, e Carmine ha scelto la (a).

## 2. La forma nel codice

1. **`Toaster`** (`web/src/shared/ui/Toaster.tsx`), la **ventiseiesima voce** di `web/src/shared/ui/catalog.ts`, la terza che sostituisce
   un pezzo di Atmosphere (dopo `StaffSidebar` e `Dialog`). È il provider, la coda e il viewport, e disegna i toast: un contesto con la
   funzione che accoda, il `Provider` di Radix con `label={t('toast.label')}` e `swipeDirection="right"`, i toast in coda, il `Viewport`
   con `label={t('toast.region')}`. Ogni toast è un `Root` di Radix con dentro titolo, descrizione (se c'è) e la «X», un `Close` con
   `aria-label={t('common.close')}` e l'icona `X` di `lucide-react` in uno `span` `aria-hidden`, come Atmosphere.
2. **Lo monta `HubProviders`** (`web/src/app/Providers.tsx`) al posto del `ToastProvider` di Atmosphere, nello stesso punto, dentro
   `I18nextProvider` (chiede le sue parole). Una sola volta: `Toaster.test.tsx`, `Notice.test.tsx` e gli altri test che montano
   `HubProviders` montano quello vero.
3. **`useNotice` non cambia forma**: le stesse props (`tone`, `title`, `description`, `duration`), gli stessi quattro toni. Dentro, al
   posto di `useToast()` di Atmosphere chiama `useToastQueue()` del `Toaster`, e passa `variant: 'destructive'` per un rifiuto e il bordo
   del tono (`TOAST_TONES`, invariata) per gli altri tre. **Nessuna schermata cambia.** `useToastQueue` non è esportato da
   `shared/ui/index.ts`: si arriva al toast solo da `useNotice`.
4. **Gli stessi comportamenti**: cinque secondi se non detto (`TOAST_DURATION`, quello della molecola di Atmosphere), lo swipe a destra
   (quello del suo provider), il tasto F8 (quello di Radix, non cambiato). La coda cresce soltanto, come quella di Atmosphere: un toast
   chiuso Radix lo smonta dopo la sua animazione, e nella coda resta un oggetto di poche stringhe fino a quando si lascia la pagina. La
   chiave di ogni toast è un numero che cresce, invece dell'indice di Atmosphere (lo stesso effetto, in una coda che non toglie niente).
5. **Le chiavi nuove del nucleo**, in `locales/{en,it}/common.json`: `toast.label` («Notification», «Notifica») e `toast.region`
   («Notifications (F8)», «Notifiche (F8)»); la «X» usa `common.close`, che c'è da E10l («Close», «Chiudi»). Per la galleria
   `uiKit.sample.toastError`, `toastWarning`, `toastSuccess`, `toastInfo`.
6. **La galleria** (`/staff/admin/ui-kit`) ha la sua sezione `Toaster` (`ToasterSample`): quattro bottoni, un toast per tono, passando da
   `useNotice` come una schermata. `uiKit.test.ts` cade senza. La sezione `Notice` tiene il suo bottone «Mostralo come toast».
7. **La guardia**, in `web/src/shared/ui/Toaster.test.tsx`, con la forma di quella di E10l: nessun file di `web/src` importa da Atmosphere
   `Toast`, `ToastProvider` o `useToast` (anche `type …` o `… as …`); e solo `shared/ui/Toaster.tsx` importa da `@radix-ui/react-toast`,
   perché un secondo toast fatto a mano direttamente su Radix direbbe di nuovo «Close». Nessun file era in attesa: l'unico uso era quello
   di `notices.ts` e `Providers.tsx`.
8. **La dipendenza nuova**: `@radix-ui/react-toast` **1.2.23**, esatta, in `dependencies` di `web/package.json`. È **la stessa versione**
   che Atmosphere 3.1.0 dichiara e impacchetta (il suo `package.json`), quindi lo stesso comportamento di prima — timer, swipe, annuncio,
   F8 — e nessun pacchetto scaricato di nuovo: il lockfile la aveva già, come dipendenza di Atmosphere, e cambia solo l'elenco del
   progetto. Pubblicata il 24 luglio 2026 (ben più di due settimane fa); chiede React `^16.8 || … || ^19.0`, e il progetto è su 19.2.8.
   La 1.2.24 (5 ottobre) e la 1.3.x (12 ottobre) non sono quelle che Atmosphere usa: portarle avrebbe voluto dire due toast diversi da
   confrontare, per niente.

### 2.1 Le classi copiate da Atmosphere

Da `@ivao/atmosphere-react` **3.1.0**, `dist/atmosphere-react.js`, regioni `src/components/atoms/toast/*` e
`src/components/molecules/toast/index.tsx`. Se Atmosphere cambia i suoi toast, queste sono le stringhe da rimettere al passo, tutte in
`Toaster.tsx`:

| Pezzo di Atmosphere | Costante in `Toaster.tsx` | Che cosa |
|---|---|---|
| `toastVariants` (la base) | `TOAST_ROOT` | `group`, le classi dello swipe (`radix-swipe-*`, `translate-x-radix-toast-swipe-*`), le animazioni d'entrata e d'uscita, `flex`, `rounded-md border p-4 pr-6 shadow-lg` |
| `toastVariants`, `variant: default` / `destructive` | `TOAST_VARIANTS` | `bg-background text-foreground border` / `group border-destructive bg-destructive text-destructive-foreground` |
| `ToastViewport` | `VIEWPORT` | `fixed top-0 z-[100] … sm:bottom-0 sm:right-0 sm:flex-col md:max-w-[420px]` |
| `ToastClose` | `CLOSE` | il posto in alto a destra, visibile al passaggio e al fuoco, i rossi del destructive |
| `ToastTitle` | in linea | `text-sm font-semibold [&+div]:text-xs` |
| `ToastDescription` | in linea | `text-sm opacity-90` |
| la molecola `Toast` | in linea | il `div` `grid gap-1` di titolo e descrizione; `duration` 5000 (`TOAST_DURATION`) |
| `ToastProvider` | — | `swipeDirection` `right` |

**`ToastAction` non è copiato**: `useNotice` non ha mai passato un'azione, e nessuna schermata la chiede. Se un giorno servisse, è una
voce di `useNotice` e una classe in più, da prendere dalla stessa regione.

Le classi sono stringhe uguali a quelle di Atmosphere, quindi le regole CSS sono quelle che `@ivao/atmosphere-react/styles.css` porta già
(il suo toast le usava); Tailwind le genera anche dalla sorgente nostra (`@source '../../src'`). Le classi si uniscono con uno spazio,
senza `tailwind-merge` (che Atmosphere usa dentro e non esporta): il bordo di un tono (`border-semantic-*-600`) non urta la base, che ha
solo `border` (lo spessore).

## 3. Le alternative

Quelle dell'issue, decise da Carmine:

1. **(b) Chiedere ad Atmosphere** di esportare gli atomi o di passare le tre parole. Scelta il 16 ottobre e poi sostituita dalla (a): non
   si chiede.
2. **(c) Una patch del pacchetto** (`pnpm patch`), già scartata per i dialog; **(d) mettere le parole nel DOM dopo il disegno**. Escluse.
3. **La prima risposta, comporlo dagli atomi** di Atmosphere tenendo il suo provider: impossibile con la 3.1 (§1).
4. **Fatta**: (a), il toast del nucleo (§2).

## 4. I test

`web/src/shared/ui/Toaster.test.tsx` (vitest, 6 casi, tutti montano `HubProviders`):

- in italiano la «X» ha `aria-label` «Chiudi», la regione si chiama «Notifiche (F8)», l'annuncio è «Notifica …», e nessuna delle tre è
  in inglese;
- in inglese sono «Close», «Notification» e «Notifications (F8)», le parole di Atmosphere, ora prese dal file della lingua;
- la «X» chiude il toast;
- un toast resta cinque secondi (c'è a 4999 ms, non c'è a 5000), con i timer finti;
- le classi di Atmosphere, lo swipe a destra (`data-swipe-direction`), il rosso pieno del rifiuto e il bordo dei tre toni, e il viewport;
- la guardia (§2.7).

`Notice.test.tsx` (del maintainer) passa senza cambiare: il toast arriva dai provider dell'applicazione.

## Da portare nel piano

- **§8.3, l'elenco chiuso** (il paragrafo ⚠️ dopo i componenti custom): la **ventiseiesima** voce, `Toaster` — il provider, la coda e il
  viewport dei toast, scritti di nuovo su Radix (`@radix-ui/react-toast` 1.2.23, dipendenza diretta di `web/`) con le classi di
  Atmosphere, perché il toast di Atmosphere dice «Close», «Notification» e «Notifications (F8)» scritti nella libreria e non esporta un
  modo per tradurli; la terza voce, dopo `StaffSidebar` e `Dialog`, che sostituisce un pezzo di Atmosphere. Una schermata non lo monta:
  dice un toast con `useNotice`.
- **§8.3, i componenti da Atmosphere** (la riga che elenca «… Tabs, Toast, …»): «Toast» si legge ora come il toast del nucleo — il
  `ToastProvider`, `Toast` e `useToast` di Atmosphere non si usano (la guardia di `Toaster.test.tsx`). Il piano non ha un elenco delle
  dipendenze del front end: che `@radix-ui/react-toast` è diretta, alla versione che Atmosphere impacchetta, e che quando Atmosphere sale
  di versione si rimettono al passo lei e le classi della §2.1, sta nella voce `Toaster` qui sopra.
