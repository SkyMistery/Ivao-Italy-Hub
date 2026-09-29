# L'accesso dalla pagina d'errore torna alla home

**Data:** 30 settembre 2026
**Stato:** correzione di un difetto, **nessuna domanda aperta**: non è una «Proposta», e la scelta di dove correggere
(il server e non la SPA, §3) l'ha presa la sessione di lavoro e si legge nella PR. Versione **0.4.2**, PATCH: solo una
correzione, nessuna migrazione, nessuna pagina.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: si corregge `SafeReturnUrl`, il filtro unico dell'indirizzo di
ritorno del login; nessun meccanismo nuovo. Cambio del nucleo (`Core/Auth/`), nella sua PR.

## 1. Che cosa si è visto

Installazione di prova, 0.4.1, log del 29 set 2026 alle 22:55 UTC. Un VID non ammesso sull'installazione privata è stato
respinto due volte (`/auth/callback` → `/login-error`). Poi, nello stesso browser, Carmine ha premuto «Accedi» **sulla
pagina `/login-error`**: l'accesso a IVAO è riuscito («VID 704798 signed in as a super administrator») e il ritorno lo ha
riportato su **`/login-error`**, che mostra un errore a chi è appena entrato. Un secondo clic da lì andava alla home.

## 2. Perché

Il pulsante «Accedi» della barra (`web/src/app/layouts/Chrome.tsx`) costruisce `loginHref(window.location.pathname)`:
manda la pagina su cui si trova. `SafeReturnUrl` (`src/IvaoHub.Core/Auth/IvaoAuthenticationExtensions.cs`) accettava ogni
percorso locale, quindi anche la pagina d'errore. Lo stesso vale per `/forbidden`: ci arriva chi è entrato e non può vedere
una pagina; se esce e rientra da lì, torna a una pagina che spiega solo un rifiuto.

## 3. La correzione, e perché nel server

`SafeReturnUrl` riporta a `/` un ritorno verso una **pagina che spiega solo un rifiuto**: `LoginErrorPath` e il nuovo
`ForbiddenPath`. Il confronto è sul solo percorso (senza `?…` e `#…`, senza la barra finale, senza badare alle maiuscole);
`/login-errors` o `/forbidden-zone` restano come sono.

- **Nel server e non nella SPA**: `SafeReturnUrl` è già il punto unico che decide dove si torna, lo chiamano l'endpoint
  `/auth/login` e il secondo giro del nonce, e conosce già `LoginErrorPath`. Nella SPA andrebbe ripetuto in ogni chiamante di
  `loginHref` (la barra, `LoginErrorPage`, gli schermi dei moduli), cioè scritto più volte (`CLAUDE.md` §2), e un link a
  mano verso `/auth/login?returnUrl=/login-error` lo scavalcherebbe.
- **Scartato**: togliere il pulsante «Accedi» dalla barra su quelle due pagine. `LoginErrorPage` ha già il suo «Riprova»
  verso `/`, ma la barra è la stessa su ogni pagina, e una barra che cambia per pagina è un caso speciale in più.

Test: `AuthenticationTests.OnlyLocalReturnUrlsSurvive`, sette casi nuovi; i cinque sulle due pagine fallivano prima della
correzione.

## Da portare nel piano

- **§6.1, punto 1 del giro di login**: l'indirizzo di ritorno passa da `SafeReturnUrl`, che accetta solo percorsi di questo
  sito e **riporta alla home un ritorno verso `/login-error` o `/forbidden`** (dal 30 set 2026, 0.4.2).
