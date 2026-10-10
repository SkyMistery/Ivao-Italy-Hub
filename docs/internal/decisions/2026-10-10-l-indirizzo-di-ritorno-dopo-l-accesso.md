# L'indirizzo di ritorno dopo l'accesso: lo riduce `loginHref`, una volta

**Data:** 10 ottobre 2026 — correzione del nucleo, sessione di lavoro di Carmine, ramo `fix/sign-in-return-address`
**Stato:** **Correzione**, chiesta da Carmine in chat il 10 ottobre 2026 («sì» alla PATCH proposta). Scritta perché cambia il
comportamento di una funzione del nucleo (`web/src/shared/api/client.ts`).
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: il meccanismo c'è già (`loginHref`, il solo punto che scrive l'indirizzo
dell'accesso), e non copriva il caso al 100 %. Si estende quello, non i chiamanti.

## 1. Che cosa è successo

- Il compito era nato da un rilievo sulla #240: «chi accede da una pagina pubblica dei tour o del training torna sulla home»,
  perché le tre righe passano `loginHref(location.href)` e `SafeReturnUrl` (`Core/Auth/IvaoAuthenticationExtensions.cs`) risponde
  `/` a un indirizzo che non comincia con `/`.
- **Su `main` quel difetto non c'era.** In `modules/flightops/screens/public.tsx` e `modules/training/screens/public.tsx`
  `location` è `useLocation()` di TanStack Router, che oscura quello della finestra: il suo `href` è percorso + query + hash,
  relativo. Misurato in un browser sul bundle di produzione: da `/training/sessions/41?probe=1#frag` il link della pagina portava
  `returnUrl=%2Ftraining%2Fsessions%2F41%3Fprobe%3D1%23frag`.
- Sulla #240 invece c'era davvero: `SlotBooking.tsx` non aveva `useLocation`, e lì `location` era la finestra. Corretto dal
  collaboratore in quella PR.
- **Il difetto vero era il contrario, sulla barra.** `app/layouts/Chrome.tsx` passava `window.location.pathname`: chi accedeva
  dalla barra su `/tours/<slug>/report?leg=2` tornava senza `?leg=2` (stessa sonda: `returnUrl=%2Ftraining%2Fsessions%2F41`).

Quindi due cose sotto lo stesso nome (`location.href` del router, relativo; `location.href` della finestra, assoluto), un server
che al secondo risponde `/` **senza dire niente**, e un errore che nessuna schermata mostra: si è già presentato una volta.

## 2. La correzione

1. `loginHref(returnUrl)` riduce l'indirizzo a un percorso di questo sito prima di scriverlo (`returnPath`, privata, nello stesso
   file): `new URL(indirizzo, origine)`; se l'origine è la nostra, percorso + query + hash; altrimenti `/`.
   - Un indirizzo assoluto del sito (`window.location.href`) ora torna dov'era invece che sulla home.
   - Un indirizzo di un altro sito (`https://…`, `//…`, `/\…`, `javascript:`) diventa `/` già nel browser, come farebbe il server.
   - **`SafeReturnUrl` non è toccato** e resta il controllo che conta: quello del browser è una cortesia, non una difesa.
2. La barra (`Chrome.tsx`) passa l'`href` del router (`useLocation`), come le pagine dei moduli: percorso, query e hash.
3. Le due guardie (`routes/_member.tsx`, `routes/_staff.tsx`) scrivevano a mano
   `` `/auth/login?returnUrl=${encodeURIComponent(location.href)}` ``: ora chiamano `loginHref`. Stesso risultato, un solo punto
   che scrive quell'indirizzo.
4. Versione **0.6.7** (PATCH: correzione, nessuna migrazione, nessuna pagina nuova).

**L'hash.** Si tiene. Le pagine dei moduli lo mandavano già (è nell'`href` del router), il server lo rimanda nel `Location`, e il
browser lo segue. Toglierlo sarebbe stato cambiare un comportamento che c'era.

## 3. Che cosa non si è fatto

- **Togliere l'argomento a `loginHref`** (leggere da sé l'indirizzo della finestra): le guardie girano in `beforeLoad`, prima che
  la finestra cambi indirizzo, e devono passare la destinazione; `LoginErrorPage` passa `/` di proposito.
- **Toccare il modulo eventi.** `modules/events/screens/SlotBooking.tsx` compone a mano
  `window.location.pathname + window.location.search`: funziona (perde solo l'hash). Può passare `location.href` e basta, quando il
  collaboratore ci rimette mano.
- **Toccare i test del training.** `e2e/training-public.spec.ts` asseriva già il valore (`returnUrl=%2Ftraining`); non è cambiato.

## 4. I test

- `web/src/shared/api/client.test.ts` (nuovo): il valore intero con query e hash; l'indirizzo assoluto ridotto; cinque indirizzi
  non nostri che diventano `/`.
- `web/src/app/layouts/Chrome.test.tsx`: il link della barra su `/tours/xx-test/report?leg=2#notes`, per valore.
- `web/e2e/tours-map.spec.ts`: da `/tours/<slug>?from=calendar#legs` il link della pagina e quello della barra, per valore. I tour
  non avevano nessun test su quel link.

## 5. Una trappola trovata passando

⚠️ `playwright.config.ts` riusa in locale un server già acceso sulla **4173** (`reuseExistingServer` fuori dalla CI). Se su quella
porta ascolta un altro programma — sul PC di Carmine un altro progetto — `pnpm e2e` prova quel sito e cade con errori che non
c'entrano. Non toccato qui: la configurazione è del maintainer.

## Da portare nel piano

- Niente che cambi una sezione del piano. Una riga di changelog per la **0.6.7**: «l'accesso dalla barra riporta anche la query e
  l'hash; `loginHref` riduce da sé l'indirizzo di ritorno a un percorso del sito».
- Per `HANDOFF.md`, fra le trappole: ⚠️ **`location.href` sono due cose**: quello di `useLocation()` è relativo, quello della
  finestra è assoluto. L'indirizzo dell'accesso si scrive solo con `loginHref`. E la trappola della porta 4173 (§5).
