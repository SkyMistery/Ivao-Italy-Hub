# Il membro collega il suo account Discord

**Data:** 5 novembre 2026, fase di test (nota `2026-10-18-la-fase-di-test`), branch `feat/discord-link`.
**Stato:** **decisa** — **Carmine in chat alla sessione di lavoro, 5 novembre 2026**: su tutte e quattro le domande di §4 ha scelto la
raccomandazione (OAuth, solo il membro, pulsante sul profilo, solo `identify`).
**Regola applicata:** `CLAUDE.md` §5, caso **(c)**: una funzione nuova del nucleo, con un secondo accesso OAuth che l'hub non ha.

## 1. Che cosa serve

Carmine: «L'utente deve poter collegare il proprio account discord a quello ivao e il nome utente deve uscire sul profilo. Poi lo
assegnamo a dalbero che fa il collegamento con il discord ivao italia».

Questa nota è il primo punto. Il secondo, il collegamento con il server Discord della divisione, è di `dalberone` e viene dopo (§6).

## 2. Che cosa c'è oggi

- **Il piano lo prevede già**: §9.1, riga Discord, «link "collega Discord" + Discord ID in profilo; il bot resta separato», con l'API
  verso il bot in M6. Questa nota anticipa la prima metà e corregge la colonna «scope `discord`» (qui sotto).
- **La colonna `hub_users.discord_id` c'è ed è sempre vuota.** IVAO non manda nessun campo Discord, nemmeno con lo scope `discord`
  concesso (misurato il 3 settembre 2026, `IvaoUserProfileReader`, `HANDOFF.md`). Nessuna schermata e nessun DTO la portano fuori.
- ⚠️ **Oggi ogni accesso la azzera**: `UserSyncService.cs:112` scrive `user.DiscordId = profile.DiscordId`, e il lettore passa sempre
  `null`. Così com'è, un collegamento durerebbe fino al login dopo.
- **Fuori dall'audit**: `[NotAudited]` dal 4 novembre (nota `2026-11-04-i-dati-della-persona-fuori-dall-audit`). Le copie vecchie
  dell'audit non hanno niente da svuotare per questa colonna: è sempre stata vuota.
- **La cancellazione** toglie già la riga di `hub_users` (`PersonalDataErasure.EraseCoreAsync`), e con lei il collegamento.
- **Il profilo** è `/me` (`web/src/features/me/MePage.tsx`): la dashboard del membro, e sotto i suoi contatti, i token, le notifiche.
- **`StaffDirectoryExposesNoContactData`** vieta già la parola `discord` nella directory dello staff, e §9.7 dice che l'hub non ha un
  profilo pubblico del membro.
- **Il bot della divisione** (`ivao-italy/discord`, pubblico) fa già lo stesso giro per conto suo: un OAuth verso Discord, e nel suo
  database `Vid`, `DiscordUserId` e `GdprAcceptDate`. Chi si è collegato lì oggi non risulta collegato nell'hub.

## 3. Le alternative

| | Come | Pro | Contro |
|---|---|---|---|
| **A. OAuth di Discord** (consigliata) | Il membro preme «Collega», autorizza su Discord, l'hub legge `id` e `username` con il solo scope `identify` | Dimostra che l'account è suo; dà l'`id`, che non cambia e che il punto 2 usa | Serve un'applicazione Discord per ogni installazione, con le sue credenziali |
| B. Nome scritto a mano | Un campo di testo in `/me` | Nessuna credenziale, poche righe | Chiunque può scrivere il nome di un altro; il nome non è univoco né fisso, e **senza `id` il punto 2 non si può fare**: si rifarebbe tutto |
| C. Leggerlo da IVAO | Lo scope `discord` che l'hub chiede già | Nessun passaggio per il membro | Non esiste: misurato, IVAO non lo manda |
| D. Leggerlo dal database del bot | Una vista `v_share_` | Chi è collegato lì lo resta | Dipende da un programma che il punto 2 può cambiare, e una divisione che forka non ce l'ha. Semmai è un'importazione del punto 2 |

## 4. Le domande a Carmine, tutte con la raccomandazione scelta

1. **Come si collega**: OAuth (A) o nome a mano (B)? **Raccomandazione: A.** B lascerebbe il punto 2 senza fondamenta.
2. **Chi vede il nome**: **raccomandazione: solo il membro**, in `/me`. Tutti è escluso da §9.7. Lo staff oggi non ha una schermata del
   membro nel nucleo: vuol dire una schermata e un permesso nuovi, da aggiungere quando c'è un bisogno concreto.
3. **Come si scollega**: **raccomandazione: un pulsante in `/me`, subito e senza conferme di nessuno**, che svuota le colonne. L'hub non
   tiene token, quindi non resta niente. L'autorizzazione resta elencata tra le app del membro su Discord, da dove la toglie lui, ed è
   innocua: nessuno ha più il token.
4. **Il punto 2 dovrà far entrare le persone nel server da solo?** Per farlo serve lo scope `guilds.join`, da chiedere **al momento del
   collegamento**: chi si è già collegato dovrebbe rifarlo. **Raccomandazione: no, solo `identify`**: l'invito al server esiste già
   (`discord.ivao.it`), e al bot per dare ruoli e nomi basta l'`id`.

## 5. Le scelte tecniche, se la risposta è A

Prese dalla sessione perché non cambiano che cosa vede nessuno; il revisore le può contestare.

- **Nel nucleo, non un modulo**: `hub_users` e `/me` sono del nucleo, §9.1 ce la mette già, e un modulo con il suo contesto per tre
  colonne sarebbe più codice per lo stesso risultato. Niente tabella generica degli «account esterni»: il fornitore è uno.
- **Colonne su `hub_users`**, tutte nullable e `[NotAudited]` come `discord_id`: `discord_username` (32 caratteri, il limite di Discord)
  e `discord_linked_at` (quando il membro ha dato il suo sì). Migrazione additiva, una sola.
- **Un account Discord, un solo VID**: indice univoco su `discord_id`, che oggi è ovunque `NULL`. Un account già collegato a un altro VID
  si rifiuta con un messaggio. Così il bot non trova mai due persone per lo stesso account.
- **Il nome è una fotografia del collegamento**: si aggiorna con «Aggiorna il nome utente», che rifà il giro. Nessun token conservato,
  nessun job.
- **Via il `UserSyncService.cs:112`** e il parametro `DiscordId` di `IvaoUserProfile`: IVAO non è la fonte di questo dato. Lo scope
  `discord` di IVAO resta com'è (Carmine, 3 settembre): toglierlo è un'altra domanda, e cambia il consenso di tutti.
- **`AddOAuth` di ASP.NET Core**, che porta `state`, cookie di correlazione e PKCE. ⚠️ Lo schema è **sempre registrato**, e senza
  configurazione non risponde a niente (`DiscordLinkHandler`): un gestore remoto senza `ClientId` fallisce a ogni richiesta, e quali schemi
  esistono si decide prima che le impostazioni siano definitive, come il commento di `Program.cs` dice per la divisione. Il token si usa
  una volta per leggere `/users/@me` e si butta. Il VID è quello del cookie dell'hub, mai un dato che arriva dal giro.
- **Il giro parte da un link (`GET /auth/discord`)**, non da un form: `form-action 'self'` in `config/security.json` blocca un form che
  finisce in un redirect verso Discord. `/auth` è già in `web/backendPaths.ts`. Lo stato lo tiene il cookie: niente memoria del processo
  da perdere quando Passenger lo ferma.
- **`GET` e `DELETE /api/me/discord`**, fuori da `/api/me` come le notifiche: serve solo a questa schermata. Il `DELETE` ha
  `HubRateLimits.MemberWrites`. Senza configurazione la risposta dice che non è disponibile, e la sezione non si disegna, tranne per chi
  era già collegato: quello può ancora scollegarsi. Il `GET` non porta l'`id`: al membro non serve.
- **Le credenziali**: sezione `Discord` (`ClientId`, `ClientSecret`, `RedirectUri`) in `secrets/*.json`, mai nel repository; esempio e
  istruzioni in `docs/FORKING.md` e `docs/DEPLOYING.md`. Sezione assente: tutto spento, e l'hub parte. Sezione a metà: l'hub non parte,
  come per `Ivao`.
- **Le prove**: Discord finto nei test (`Backchannel`), prova a schermo con un'applicazione vera creata da chi ne ha il diritto.

## 6. Che cosa lascia al punto 2

- `hub_users.discord_id`: l'`id` di Discord, fisso e univoco, `NULL` se non collegato. **Non** dimostra che la persona sia sul server.
- `discord_username`, che può essere vecchio: il bot lo sa aggiornato meglio dell'hub. `discord_linked_at`: la data del sì del membro.
- ⚠️ **Collegare e scollegare non lasciano tracce da seguire**: le colonne sono `[NotAudited]`, quindi niente riga d'audit e `updated_at`
  fermo. Chi deve accorgersi di uno scollegamento confronta l'elenco intero, o il punto 2 aggiunge il suo meccanismo.
- **Come il bot legge i collegamenti non è deciso qui**: una vista `v_share_`, un token con la sua `audience` o altro è la prima decisione
  del punto 2, con la sua nota. Oggi nessun endpoint espone i collegamenti di altri.
- **I collegamenti già nel database del bot** non sono importati, e i consensi di `GdprAcceptDate` restano lì: se servono, è il punto 2.
- **Scope in più** (`guilds.join`, §4 domanda 4): vanno chiesti nel giro del collegamento e usati lì, perché il token non resta.

## 7. Che cosa non fa

- Non tocca il server Discord: niente ruoli, nomi, messaggi, bot.
- Non mostra il nome ad altri, e non aggiunge notifiche via Discord (§9.7, M6).
- **La pagina della privacy** è un contenuto del database: una riga su che cosa si conserva e perché la scrive chi la gestisce, prima di
  aprire la funzione ai membri. Il testo accanto al pulsante dice già che cosa conserva l'hub, e che la divisione può usare l'`id` sul suo
  server: così il sì di oggi vale anche per il punto 2.

## Da portare nel piano

- `00-piano-di-progettazione.md` §9.1 riga Discord: il collegamento è fatto, con OAuth e `identify`; lo scope `discord` di IVAO non porta
  niente. §6.1 passo 3 (il «discord id» di `/v2/users/me`). §7 (`users`, le colonne nuove). §9.7 «Privacy dei membri»: chi vede il nome
  e come si scollega. §13 riga M6: resta l'API verso il bot. Versione e changelog.
- `docs/internal/HANDOFF.md`: la riga «la colonna `discord_id` resta vuota».
