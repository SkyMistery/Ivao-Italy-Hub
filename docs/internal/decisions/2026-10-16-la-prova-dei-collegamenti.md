# La prova dei collegamenti ai database

**Data:** 16 ottobre 2026
**Stato:** **chiesta da Carmine** (16 ottobre 2026, in chat alla sessione master, che l'ha passata a una sessione di lavoro): un
pulsante del super amministratore che prova, a richiesta, i due collegamenti dell'hub. **Rifiutato da Carmine nella stessa richiesta**:
un pulsante che scarica i database (dati personali; il database di vIPI si legge solo dalle sue viste `v_share_`, nota
`2026-09-27-i-dati-condivisi-senza-isolamento`). La forma nel codice è di questa nota (§2–§4). Versione **0.8.0** (§5).
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: estende la diagnostica del super amministratore che c'è già
(`GET /api/admin/diagnostics/request`, nota `2026-09-28-la-diagnostica-della-richiesta`; `startup.txt`) e legge l'archivio con il
codice che c'è già (`VipiAtcActivitySource`, `VipiShareDbContext`). Nessun meccanismo nuovo, nessun permesso nuovo. Cambio del nucleo,
nella sua PR.

## 1. Che cosa serve

Sull'installazione di prova `test.it.ivao.aero` dal 16 ottobre l'archivio ATC di vIPI è acceso (`division.json → atcData: vipi`,
`ConnectionStrings:AtcData`). L'hub lo apre **solo** quando la validazione di un PIREP o una proposta del roster gli chiede qualcosa, e
ogni errore diventa «non disponibile» (`VipiAtcActivitySource`, per scelta: il rapporto parte lo stesso). Quindi oggi nessuno sa se il
collegamento funziona finché un PIREP non risponde «non disponibile», e anche allora non si sa perché: password, database, vista,
server. Lo stesso vale, in piccolo, per il database dell'hub: `startup.txt` dice che cosa è successo **all'avvio**, non adesso.

## 2. Che cosa si è fatto

`POST /api/admin/diagnostics/connections`, in `src/IvaoHub.Web/Endpoints/ConnectionCheckEndpoints.cs`, e la pagina
`/staff/admin/connections` («Collegamenti ai database»), con la sua voce nel menu dell'amministrazione.

- **Solo il super amministratore**, con la porta della diagnostica della richiesta: `SignedIn` sull'endpoint e
  `ICurrentUser.IsSuperadmin` nel corpo. Chi ha tutto il catalogo (il direttore) riceve `403`. La pagina ha la stessa guardia
  (`bootstrap.user.isSuperadmin`) e la voce del menu compare solo a lui: comodità, non sicurezza.
- **`POST` e non `GET`**: è una prova che qualcuno lancia, non una risorsa; niente la mette in cache, niente la precarica. Di sola
  lettura: **nessuna riga scritta**, nell'hub come nell'archivio.
- **Il database dell'hub** (`Default`): apre la connessione, `SELECT 1`, la versione del server; poi, per il contesto del nucleo e per
  quello di ogni modulo acceso, l'ultima migrazione applicata e quelle **in sospeso** (le stesse informazioni di `startup.txt`, lette
  adesso). Esito, tempo in millisecondi.
- **L'archivio** (`AtcData`): **solo** quando `atcData.source` è `vipi` (`AtcDataOptions.ReadsVipi`, la stessa domanda che
  `AddAtcActivity` fa per scegliere la fonte); altrimenti «non configurato» e **non si apre niente**. Se la divisione lo nomina e la
  stringa di connessione non è nei segreti, lo dice senza aprire niente. Altrimenti lo apre **attraverso la fonte del nucleo**:
  `VipiAtcActivitySource.LatestStartAsync`, un `MAX(start_utc)` sulla vista `v_share_atc_sessions` del contesto condiviso. Risponde
  **un'ora**, quella della connessione più recente, **mai una riga**: niente VID, niente nominativo. Mai una tabella di vIPI, solo la
  vista, in sola lettura (il contesto rifiuta di salvare).
- **Pazienza di dieci secondi per database**, apertura compresa: vince anche su un `Connect Timeout` più lungo della stringa.
- **Un errore è un tipo, non un messaggio**: il server manda una parola fra `AccessDenied`, `UnknownDatabase`, `MissingView`, `Timeout`,
  `Unreachable`, `NoConnectionString`, `Other` (dai codici di MariaDB lungo la catena delle eccezioni: 1044/1045/1142/1143, 1049, 1146,
  1042, il timeout); la pagina la dice con le chiavi `connections.errors.*` di `locales/{en,it}/common.json`. **Mai** la stringa di
  connessione, l'utente, l'host, il nome del database o il messaggio dell'eccezione nella risposta. Il log tiene l'eccezione, come per
  ogni altro errore del database (il messaggio di MariaDB nomina utente e host, mai la password).
- **Il guasto dell'archivio non tocca la risposta sul database dell'hub**, e viceversa: ognuno ha il suo esito.

## 3. Perché una pagina, questa volta

La diagnostica della richiesta è un JSON (nota del 28 set, §3): nomi di header e indirizzi che si copiano parola per parola. Qui chi
legge vuole una frase («accesso negato», «manca la vista») e un pulsante, e Carmine ha chiesto un pulsante. La pagina è piccola: un
pulsante, due riquadri, le parole in `common.json`. È una pagina nuova, quindi una **MINOR** (§5).

## 4. Che cosa non c'è, e non ci sarà

- **Nessuno scaricamento** di un database o di una sua parte (rifiutato da Carmine: dati di persone; vIPI si legge solo dalle viste).
- **Nessun conteggio** delle righe dell'archivio: su un anno di sessioni del mondo un `COUNT(*)` attraverso la vista legge tutto; il
  `MAX` sull'indice dell'inizio basta a dire che la vista risponde e che è viva.
- **Nessun permesso nuovo**: la porta è quella del super amministratore, come la diagnostica della richiesta e l'elenco dei super
  amministratori.

## 5. Versione

`0.8.0`: una pagina nuova è una MINOR per la regola di `Directory.Build.props`, nessuna migrazione. Viene dopo la `0.7.5` della
#279, già unita.

## 6. Prove

`tests/IvaoHub.IntegrationTests/ConnectionCheckTests.cs`, sulla MariaDB del banco, con un archivio finto **in un database suo** e la
vista del contratto sopra, come `AtcActivitySourceTests`:
- solo il super amministratore (`401` anonimo, `403` membro e direttore);
- database dell'hub raggiunto, con versione e migrazioni di ogni contesto aggiornate; archivio `none` → «non configurato»;
- archivio raggiunto → l'ora della connessione più recente, nessun nominativo né VID nella risposta;
- password sbagliata → `AccessDenied`, e nella risposta né la password né l'utente né l'host né il nome del database;
- database inesistente → `UnknownDatabase`; vista tolta → `MissingView`; archivio nominato senza stringa → `NoConnectionString`.

La pagina: `web/src/features/admin/connections/ConnectionCheckPanel.test.tsx` (niente si chiede prima del pulsante; un tipo d'errore
diventa la frase).

## Da portare nel piano

- **§11.3 punto 5** (la diagnostica): una **quarta voce**, la prova dei collegamenti, `POST /api/admin/diagnostics/connections` e la
  pagina `/staff/admin/connections`, solo per il super amministratore (0.8.0).
- **§16.6** (gli endpoint a mano per famiglia): un endpoint del nucleo in più, fra i verbi **fuori da ogni risorsa**, alla voce
  **amministrazione del sistema, riservata al super amministratore**, accanto a `/api/admin/diagnostics/request`.
- **Il paragrafo dell'archivio ATC** (nota `2026-09-27-i-dati-condivisi-senza-isolamento`, §9.7 dove il piano lo descrive): la fonte
  ha una seconda domanda, `LatestStartAsync`, che lascia passare l'errore perché serve a dirlo; le altre due restano «non disponibile».
- **`docs/DEPLOYING.md`**: aggiornato in questa PR (i controlli dopo la consegna).
