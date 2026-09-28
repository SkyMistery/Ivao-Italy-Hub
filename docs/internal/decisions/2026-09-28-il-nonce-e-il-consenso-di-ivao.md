# Il nonce e il consenso di IVAO: il primo accesso riparte una volta

**Data:** 28 settembre 2026
**Stato:** correzione urgente chiesta da Carmine in chat il 28 set 2026 (sessione di lavoro `fix/first-sign-in-nonce`),
versione `0.2.5`. Il cambio di comportamento è piccolo e dentro un meccanismo che esiste già: va letto da Carmine nella PR.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: la gestione dei fallimenti del giro IVAO (`OnRemoteFailure`,
piano §6.1) si estende, non se ne aggiunge un'altra. Cambio del nucleo, nella sua PR.

## 1. Il sintomo

Sull'installazione di prova (`0.2.1` e `0.2.4`), due volte il 28 settembre, alle 10:49 e alle 15:19 UTC, il **primo** accesso
è finito su `/login-error?code=nonce` (`IDX21323: … OpenIdConnectProtocolValidationContext.Nonce was null,
…ValidatedIdToken.Payload.Nonce was not null`), e il secondo, pochi secondi dopo, è riuscito. Login e callback erano serviti
dallo stesso processo, a 2–3 s di distanza; la chiave di Data Protection è una sola da tutto il giorno.

## 2. La misura

- **Il registro** (`hub-20260928.log`): in tutta la giornata un solo `GET /auth/login` per tentativo, anche nei due falliti.
  La SPA lo conferma: il pulsante è un `<a href>` semplice (`Chrome.tsx`), le guardie usano `redirect({ href })`, il router
  non precarica. Non è il doppio avvio di vIPI (§A78).
- **La correlazione passa**: lo `state` torna ed è di questo browser. Manca solo il nonce: l'id_token ne porta uno, ma non
  quello del cookie di questo giro.
- **Il tempo passato da IVAO**: nei giri riusciti il callback arriva 0,2–0,7 s dopo il `302` verso IVAO; nei due falliti
  **2,3 s e 2,6 s**. IVAO ha mostrato una pagina. E nessuno dei due era un accesso di Carmine (704798), che al client «Test»
  ha dato il consenso da tempo e non ha mai fallito: alle 10:49 era il VID 374133, alle 15:19 un membro dello staff che il
  browser mostrava in `it-IT` prima di entrare.
- **La pagina di consenso di IVAO** (`https://sso.ivao.aero/authorize`, Next.js, pubblica): quando la mostra, i
  `pageProps` portano `state`, `redirect_uri`, `code_challenge`, `code_challenge_method` e non il `nonce`, anche se il
  `nonce` è nella query; e il form che la pagina invia (`_next/static/chunks/pages/authorize-*.js`, letto il 28 set 2026)
  ha i campi nascosti `clientId`, `responseType`, `scope`, `state`, `redirectUrl`, `codeChallenge`,
  `codeChallengeMethod`. **Il nonce non c'è.** Quando invece il consenso è già dato, IVAO risponde subito dal server e il
  nonce torna giusto.

Quindi: il primo accesso di un membro a un client passa dal consenso, e il consenso perde il nonce. Il secondo tentativo
trova il consenso già dato e riesce. L'avvio a freddo era una coincidenza: sul server di prova Passenger spegne l'hub dopo
circa 10 s senza richieste, e quasi ogni primo clic dopo una pausa trova l'hub da avviare.

## 3. La correzione

`IvaoAuthenticationExtensions.OnRemoteFailure`: un fallimento classificato `nonce`, con lo `state` letto, **fa ripartire il
giro una volta** (`ChallengeAsync` con lo stesso indirizzo di ritorno). Il segno del secondo giro (`ivao.second-round`) sta
nelle `Items` delle proprietà, cioè dentro lo `state` cifrato: non si può falsificare né togliere, e un secondo fallimento
sul nonce finisce sulla pagina d'errore come prima. Niente ciclo.

Il nonce **resta validato**. Le alternative scartate:

- **spegnere il nonce** (`RequireNonce = false`, o la via di fuga `RelaxProtocolValidation` di vIPI): con `code` + PKCE
  lo scambio è già legato a questo browser, ma si toglierebbe un controllo per tutti per colpa di una pagina che si vede una
  volta per membro;
- **lasciare la pagina d'errore** con «Riprova»: è quello che il membro ha fatto a mano; il primo contatto con il sito
  sarebbe sempre un errore.

Un fallimento di correlazione non riparte: senza `state` leggibile non c'è indirizzo di ritorno né segno del secondo giro.

La riga di avviso nel registro dice ora anche **quanti cookie di nonce sono tornati** (zero = il browser non l'ha mandato;
uno o più = l'id_token porta un nonce che non è nostro) e se era il secondo giro; il secondo giro scrive una riga
«starts a second round».

Da segnalare a IVAO (lo fa Carmine, se vuole): il form di consenso di `sso.ivao.aero/authorize` dovrebbe portare il `nonce`.

## 4. Le prove

`tests/IvaoHub.IntegrationTests/IvaoRoundTripTests.cs`, con IVAO fatto dal test (chiave di firma, token endpoint,
user info):

- un ritorno con un nonce estraneo manda il browser a un secondo giro con `state` e `nonce` nuovi, e il secondo giro
  entra (`302` all'indirizzo di ritorno, cookie `hub.auth`);
- un secondo giro che fallisce di nuovo sul nonce finisce su `/login-error?code=nonce`;
- il nonce giusto entra al primo ritorno, con una sola chiamata al token endpoint;
- un errore di correlazione va a `/login-error?code=correlation`, senza secondo giro.

Senza la correzione i primi due sono rossi. ⚠️ Trappola trovata scrivendoli: fissare `OpenIdConnectOptions.Configuration`
non basta per validare un id_token in un test, perché il framework ha già costruito un gestore di configurazione per la
discovery vera e il gestore dei token chiede a quello le chiavi (cioè a IVAO, in rete). Il test lo sostituisce con uno
statico.

**Da verificare sul server** (lo fa Carmine; da qui non si può): un membro che non è mai entrato nel client «Test», o
Carmine dopo aver revocato il consenso al client dal suo profilo IVAO, entra al primo clic; nel registro, prima del
`302` finale, una riga «classified as nonce … Second round: False» e una «starts a second round».

## Da portare nel piano

- **§6.1**, punto 2 e paragrafo di `OnRemoteFailure`: il consenso di IVAO perde il nonce; un fallimento sul nonce riparte
  una volta, con il segno nello `state`; il resto va alla pagina d'errore.
