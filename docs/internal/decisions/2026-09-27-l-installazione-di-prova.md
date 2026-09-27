# L'installazione di prova

**Data:** 27 settembre 2026
**Stato:** **decisa da Carmine in chat, 27 settembre 2026** (le tre risposte del §2); il modo del §3 è della sessione di
lavoro, e lo legge la revisione.
**Regola applicata:** `CLAUDE.md` §5, caso **(c)**: un meccanismo nuovo del nucleo, le impostazioni di un'installazione.

## 1. Il problema

- La prima installazione è **di prova**, su `https://test.it.ivao.aero`, con la stessa divisione e quindi lo stesso
  `division.json` della produzione.
- `division.json → domain` è l'unico dominio: ci si costruiscono i link delle mail (contatti, revisione di pagine e
  documenti, ban, PIREP, leg, richieste di training), la sitemap e robots.txt. Con il file di produzione la prova
  manderebbe link a `it.ivao.aero`; una copia del file cambiata a mano è un file che prima o poi arriva sul server
  sbagliato.
- Niente dice che un'installazione è privata: robots e sitemap invitano i motori, e chiunque abbia un account IVAO entra e
  lascia la sua riga in `hub_users`.

## 2. Che cosa ha deciso Carmine (in chat, 27 settembre 2026)

1. **Il dominio è dell'installazione**, non solo del file della divisione.
2. **Un'installazione di prova resta privata**: non indicizzata, e aperta **solo allo staff**, per ora.
3. **Ha un suo client OAuth IVAO**, che registra Carmine: niente codice.

E il database: **suo**, sul server MariaDB 11.4.10 condiviso (per esempio `itivao_hub_test`), separato da `itivao_atc` di
vIPI e dalla futura produzione; pool ≤ 15.

## 3. Come

- **`Installation:Domain`**, in `secrets/*.json` o in `Installation__Domain`, accanto ad `AllowedHosts`. Si risolve **in
  un punto**: `HubConfiguration.Division` lo mette sopra la chiave `domain` del file quando si costruiscono le
  `DivisionOptions`, e `DivisionOptions.Domain` resta **l'unico dominio da leggere**: nessun chiamante cambia, nemmeno nei
  moduli. Il validatore rifiuta un dominio che non è un nome host (schema, porta, percorso), da qualunque delle due parti
  venga.
- **`Installation:Preview=true`**: robots.txt `Disallow: /` senza sitemap, `/sitemap.xml` 404, `X-Robots-Tag: noindex,
  nofollow` su ogni risposta (nel middleware degli header di sicurezza).
- **Solo staff**: alla fine del giro con IVAO, **prima di scrivere qualsiasi cosa**, chi non è staff né super
  amministratore va a `/login-error?code=staffOnly`, tradotta («di te non è stato salvato niente»). Nessuna riga in
  `hub_users`, nelle posizioni, nei token. Il corpo di `OnTicketReceived` diventa il servizio `IvaoSignIn`, perché questo
  si possa provare senza un identity provider.
  - **Staff** è la regola di `is_staff`: una posizione di questa divisione che `StaffRoleMap` riconosce. L'`isStaff` di
    IVAO non conta.
  - **Il super amministratore di bootstrap passa per la colonna, non per il file**: il primo avvio scrive la sua riga
    segnaposto con `is_superadmin` (`SuperadminService.BootstrapAsync`), quindi al primo accesso lo è già. Il login non
    legge `division.json`, e resta vero che dopo il bootstrap il file non conta (piano §4.1).
- `diagnostics/startup.txt` scrive il dominio e se l'installazione è privata: è il primo controllo dopo l'upload.

## 4. Limiti, scritti e non risolti

- **Le pagine pubbliche restano leggibili** senza accedere, da chi conosce l'indirizzo: privata vuol dire non indicizzata
  e accesso solo allo staff. Se servisse chiuderla anche ai lettori, la protezione con password di Plesk sulla cartella
  lo fa senza codice.
- **Chi è entrato prima** che un'installazione diventasse privata resta dentro finché il cookie vale, e i suoi token
  personali valgono. La prova nasce vuota e privata, quindi non succede; se un giorno servisse, si svuota `hub-keys/`
  (tutti fuori).
- **Lo staff di sole posizioni FIR** passa solo quando ci sono i dati di riferimento: il primo avvio li scarica, e se IVAO
  in quel momento non risponde è rifiutato fino al sync successivo.

## 5. Alternative scartate

| Alternativa | Perché no |
|---|---|
| Un `division.json` diverso per la prova | il file è della divisione: due copie divergono, e quella di prova prima o poi viaggia in produzione |
| Un servizio del dominio letto da ogni chiamante | undici letture in nove file, dentro i moduli (anche il training di `dalberone`), per lo stesso risultato |
| Rifiutare dopo aver scritto l'utente, e poi cancellarlo | scrive dati personali di chi non doveva entrare, e l'audit ne tiene copia |
| Chiudere nel codice tutto il sito ai non staff, anche ai lettori | non chiesto; Plesk lo fa senza codice |

## 6. Che cosa si tocca

- Nucleo: `Services/InstallationOptions.cs` (nuovo), `Auth/IvaoSignIn.cs` (nuovo), `Auth/IvaoAuthenticationExtensions.cs`,
  `Auth/UserSyncService.cs` (`IsStaffOrSuperadminAsync`, sola lettura), `Content/SeoEndpoints.cs`,
  `Division/DivisionOptionsValidator.cs`, `Services/StartupDiagnostics.cs`.
- Web: `Program.cs`, `HubConfiguration.cs`, `SecurityHeaders.cs`, `HubPipeline.cs`; la SPA: il motivo `staffOnly` della
  pagina del login fallito, in `locales/{en,it}/common.json`.
- Test: `InstallationTests` (integrazione), `DivisionOptionsValidationTests` (unità). README, «What production needs».

## Da portare nel piano

- **§11.3 punto 2** e **punto 8**: le impostazioni di un'installazione (`Installation:Domain`, `Installation:Preview`)
  accanto ad `AllowedHosts`; lo staging diventa l'installazione di prova su `test.it.ivao.aero`, con il suo client OAuth e
  il suo database.
- **§15 punto 3** (dominio di staging): chiuso per la prova, `test.it.ivao.aero`; il nome di produzione resta da decidere.
- **§15 punto 2c** (hosting): il database della prova è suo sul server condiviso, separato da vIPI e dalla produzione.
- **§4.1** (forkabilità): che cosa è della divisione (`division.json`) e che cosa dell'installazione (`secrets/`, variabili).
