# Il nuovo hub di IVAO Italia — l'installazione di prova su `test.it.ivao.aero`

> **Per chi carica i file**: oggi Carmine, domani chiunque dello staff abbia l'accesso FTP. `test.it.ivao.aero` è un sito
> di staging che Ivao.It ha lasciato alla divisione, già preparato: sottodominio, document root, Passenger e database li
> ha impostati Ivao.It, e il pannello non ce l'abbiamo. Noi **carichiamo via FTP** (FileZilla) e controlliamo da fuori.
>
> È un'installazione **di prova e privata**: non la trovano i motori di ricerca, ed entrano **solo lo staff della
> divisione** e i super amministratori. Nessun dato vero di soci, nessuna mail in uscita.
>
> La guida generale, in inglese, è `docs/DEPLOYING.md` del repository; la preparazione dello zip è `docs/DELIVERING.md`.
> Questo foglio è la versione per questo server, com'è davvero.

## In breve

| Cosa | Valore |
|---|---|
| Indirizzo | `https://test.it.ivao.aero`, dietro Cloudflare. L'origine risponde anche senza Cloudflare, e resta così (decisione di Carmine, [commento](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/165#issuecomment-5869107095)) |
| Come si carica | FTP, con FileZilla. Alla radice dell'FTP ci sono due cartelle: `logs/` e `webapp/` |
| Cartella dell'applicazione | **`webapp/`**: tutto il pacchetto va qui, e qui stanno `secrets/`, `config/`, `hub-keys/`, `tmp/` |
| Document root | **`webapp/wwwroot/`**: il server web consegna da sé solo i file che trova lì |
| Avvio | Passenger, impostato da Ivao.It: `dotnet IvaoHub.Web.dll`, con `webapp/` come cartella di lavoro |
| Database | **`itivao_test`**, su `Server=localhost;Port=3306`, con **l'utente che ha dato Ivao.It** (ha un nome diverso dal database) |
| Client OAuth IVAO | **suo**, registrato da Carmine per `test.it.ivao.aero` |
| Log | `webapp/logs/hub-<data>.log`; a richiesta anche in `logs/` alla radice (§3, punto 4) |
| Pacchetto | lo zip della release, self-contained linux-x64: **non serve .NET 10 sul server** |

## Che cosa arriva a chi carica

1. **Lo zip della consegna**, preparato con `tools/prepare-delivery.ps1` (`docs/DELIVERING.md`) dalla release che GitHub
   produce dal tag, non dal PC di qualcuno. Dentro, tre rami che non si mescolano:
   - la cartella dei file da caricare, `full-<versione>/` (il pacchetto intero) oppure `only-<N>-files-<versione>/` (solo
     i file cambiati), con il suo `MANIFEST.txt`, l'elenco dei file e delle loro impronte;
   - `docs/`, con questo foglio e il modello dei segreti, `segreti.esempio.json`;
   - `restart.txt`, il file vuoto che riavvia l'applicazione.

   Accanto allo zip, il suo `.sha256`.
2. **`config/division.json`** della divisione italiana, dal repository allo stesso tag, **solo** alla prima
   installazione o quando cambia. Non è nello zip apposta: è dell'installazione, e un aggiornamento non deve sovrascriverlo.
3. **Mai nello zip, mai in una mail in chiaro**: `ClientId` e `ClientSecret` del client OAuth di prova (li ha Carmine) e le
   credenziali del database (le ha date Ivao.It). Chi carica le scrive direttamente nel file dei segreti sul server (§3).

## 1. Che cosa è già pronto sul server

L'ha fatto Ivao.It, e non si tocca da qui:

- il sottodominio `test.it.ivao.aero`, proxato da Cloudflare, con il **document root su `webapp/wwwroot/`**: così il server
  web può consegnare solo i file del sito, e il resto (segreti, chiavi, configurazione) non ha un indirizzo. Misurato il
  28 settembre 2026: `/secrets/x.json`, `/config/division.json`, `/appsettings.json`, `/IvaoHub.Web.dll`,
  `/diagnostics/startup.txt` rispondono **404 dall'hub**;
- Passenger sulla cartella `webapp/`, con `dotnet IvaoHub.Web.dll` (la porta gliela passa l'hosting: da noi non serve
  niente) e il riavvio con `tmp/restart.txt`;
- il database `itivao_test`, vuoto alla consegna, con il suo utente. **Le tabelle le crea l'hub** al primo avvio: non si
  importa niente. Il pool dell'hub è di **al massimo 15 connessioni** (è scritto nel file dei segreti): il tetto per utente
  del server è condiviso con vIPI.

Se un giorno l'installazione va rifatta su un altro server, quello che serve chiedere a chi amministra è in
`docs/DEPLOYING.md` («What the server needs», «The web server in front», «The database»), comprese le direttive nginx da
aggiungere come seconda serratura.

## 2. Caricare i file

Si carica **il contenuto della cartella dei file da caricare** dello zip (non `docs/`, non `restart.txt`) dentro
`webapp/`, con FileZilla:

1. **Trasferimento binario**, non «Auto» (in FileZilla: *Trasferimento → Tipo di trasferimento → Binario*): in ASCII i
   file dell'applicazione arrivano rovinati.
2. Si trascina il **contenuto intero** della cartella su `webapp/`, così le sottocartelle (`wwwroot/`, `locales/`, `seed/`,
   `config/`) restano al loro posto. Alla fine la scheda dei trasferimenti falliti dev'essere vuota. `MANIFEST.txt`
   dice quali file sono e con quale impronta.
3. **Aggiornando, mai sovrascrivere un file mentre l'applicazione gira**: un `.dll` sovrascritto sotto un processo vivo lo
   fa morire senza una riga nel log. Si carica il file con un altro nome e poi lo si **rinomina** al suo posto (la
   procedura di vIPI, `LEGGIMI-AGGIORNARE-VIA-FTP.md`, e `docs/DEPLOYING.md` «Updating»).
4. Solo alla **prima installazione**, a mano, **accanto** ai file del pacchetto:

   ```
   webapp/
     config/division.json                   ← quello consegnato con lo zip
     secrets/<nome-non-indovinabile>.json   ← §3
     tmp/restart.txt                        ← un file di testo vuoto: serve a riavviare, conta la data
   ```

   `hub-keys/`, `logs/`, `diagnostics/` e `media/` li crea l'applicazione.
5. Permessi `755` su `IvaoHub.Web`, se FileZilla li accetta. Con `dotnet IvaoHub.Web.dll` non servono (misurato), ma non
   costano niente e lasciano possibile l'altro comando di avvio (`./IvaoHub.Web`).
6. ⚠️ **Mai caricare `appsettings.Development.json`**: il pacchetto non lo contiene, e se sul server c'è va cancellato.
7. **Per ultimo**, `restart.txt` dello zip dentro `webapp/tmp/` (sovrascrive quello di prima: conta la data), e poi si
   apre il sito una volta (§4).

## 3. Il file dei segreti

Solo alla prima installazione, o quando cambia qualcosa dentro.

1. Copiate `segreti.esempio.json` dentro `webapp/secrets/` e **rinominatelo con un nome che nessuno può indovinare** (va
   bene `k7f3a91c4e8b2.json`; non vanno `segreti.json`, `password.json`, `config.json`). È la stessa regola di vIPI: la
   cartella non ha un indirizzo, il nome è la seconda serratura. ⚠️ Tutto minuscolo: `secrets`, non `Secrets`.
2. **Sostituite ogni `SCRIVI-QUI-…`, nessuno escluso**:
   - il **nome del database** (`itivao_test`), **l'utente** e **la password**: tutti e tre sono **quelli che ha dato
     Ivao.It**. L'utente e la password compaiono **due volte**, in `Default` e in `AtcData`;
   - `ClientId` e `ClientSecret` del client OAuth di prova, che ha Carmine.

   ⚠️ **Il primo avvio di questa installazione è fallito proprio qui**: nel file era rimasto l'utente del modello, e il
   database ha risposto «Access denied». Un nome del database o un utente lasciato come nel modello non è un nome
   plausibile da correggere dopo: è un avvio che non parte.
3. Il resto non si tocca: `Server=localhost;Port=3306`, `AtcData` sul database `itivao_atc` (la riga con cui l'hub legge
   **solo** la vista condivisa di vIPI; resta spenta finché `division.json` non dice `atcData: vipi`, e tenerla scritta non
   costa niente), gli indirizzi del login, `AllowedHosts`, le reti fidate (il server stesso e gli intervalli di Cloudflare)
   e le due righe dell'installazione di prova, `Domain` e `Preview`.
4. **Facoltativo: il log anche in `logs/` alla radice dell'FTP.** L'hub scrive sempre in `webapp/logs/`. Per averne una
   copia nella `logs/` accanto a `webapp/`, nel file dei segreti rinominate la chiave `$esempio-Serilog` in `Serilog`: il
   percorso `../logs/hub-.log` è relativo alla cartella di lavoro, `webapp/`. *Non misurato sul server.*
5. **Non c'è la sezione della posta, apposta**: senza, l'hub non manda nessuna mail e non perde niente (le mette in coda).
   Una prova non deve scrivere a soci veri.

## 4. L'avvio

Dopo `restart.txt`, aprite `https://test.it.ivao.aero/staff` (non `/`: la home la consegna il server web senza passare
dall'hub, vedi §5): è la richiesta che fa partire l'applicazione. Il primo avvio di tutti crea le tabelle e scarica da IVAO
FIR e aeroporti della divisione; quelli dopo applicano solo le migrazioni che mancano.

**L'avvio a freddo è normale**: Passenger spegne l'hub dopo **10–30 s** senza richieste, e la prima richiesta dopo il
silenzio aspetta **8–10 s** (misurato il 28 settembre 2026, nota `decisions/2026-09-28-l-avvio-a-freddo.md`, che dice
dove vanno quei secondi e che cosa li taglia). Non è un guasto; lo è se l'attesa finisce con l'errore di Passenger.
Dalla 0.2.4 l'avvio è più corto (stima ~5 s, nota `decisions/2026-09-28-un-avvio-piu-veloce.md`), e
`webapp/diagnostics/starts.txt` scrive una riga per ogni avvio e ogni arresto: quanto è durato l'avvio (`ready in`), dove
sono andati i secondi (`steps ms`), quando è uscita la prima risposta (`first answer at`, nella riga `STOP`). Un `!!` subito
dopo il caricamento è il processo vecchio sostituito; nei giorni dopo va letto.

## 5. I controlli

Non nel minuto del riavvio: lasciategli il tempo delle migrazioni. `/api/version` e le altre chiamate all'API si
controllano con `curl -s` (GET), non con `curl -I`: a una richiesta HEAD l'API risponde 404.

| Che cosa | Che cosa deve dire |
|---|---|
| `webapp/diagnostics/startup.txt`, scaricato via FTP | `started at` di **adesso** (in UTC); `version` e `commit` del tag consegnato; `environment Production`; `division IT`; **`domain test.it.ivao.aero`**; **`access private: staff only, not indexed`**; in `root` la cartella `webapp`; in `migrations` quelle applicate da questo avvio, oppure `none applied, already up to date`; in `modules` `flightops, training` |
| `webapp/diagnostics/startup-error.txt` | **non deve esserci**: un avvio riuscito lo cancella |
| `curl -s https://test.it.ivao.aero/api/version` | la **versione e il commit** del tag consegnato, e `.NET 10…`. Il numero da solo non basta: è il commit a dire quale codice gira |
| `curl -s https://test.it.ivao.aero/health` | `Healthy` |
| `curl -sI https://test.it.ivao.aero/staff` | `200`, con le righe `content-security-policy:`, `x-robots-tag: noindex, nofollow` e `x-correlation-id:`: è l'hub che risponde, con i suoi header |
| `curl -s https://test.it.ivao.aero/robots.txt` | `User-agent: *` e `Disallow: /`, e nient'altro |
| `curl -sI https://test.it.ivao.aero/sitemap.xml` | **404**: un'installazione privata non ha sitemap |
| `curl -sI` su `/secrets/<nome del file>.json`, `/config/division.json`, `/appsettings.json`, `/IvaoHub.Web.dll`, `/hub-keys/`, `/diagnostics/startup.txt` | 404, oppure la pagina del sito (`/hub-keys/` risponde così): **mai il file** |
| L'accesso con IVAO di **Carmine** | entra, e in alto compare il suo nome: è il super amministratore del primo avvio |
| L'accesso di un socio che **non** è staff | la pagina «L'accesso non è andato a buon fine» con la frase «Questa è una copia privata del sito, aperta solo allo staff…». **Di lui non resta niente** nel database. *Non ancora provato su questo server* |
| Prima di tutto, nello stesso browser, `https://test.it.ivao.aero/cdn-cgi/trace` | la riga `ip=`: è il tuo indirizzo come lo vede Cloudflare, e le due righe qui sotto lo confrontano con quello |
| Da super amministratore, `https://test.it.ivao.aero/api/admin/diagnostics/request` nel browser | un JSON con `scheme` `https` e, **dalla `0.2.3`**, `believed.address` uguale alla riga `ip=`. Mai `127.0.0.1`, mai un indirizzo di Cloudflare. Se non lo è, si copia la risposta intera: dice che cosa è arrivato e dove il middleware si è fermato (nota `decisions/2026-09-28-la-catena-dei-proxy.md`) |
| Dalla `0.2.3`, da super amministratore: in `/staff/links` si crea un link («ip test», `https://example.org`), si salva e si cancella; poi in `/staff/admin/audit` si legge la colonna `ip` delle due righe | il tuo indirizzo, lo stesso della riga `ip=`. È questa la prova che il limite degli accessi al login conta ogni visitatore per conto suo |

**I limiti noti, che oggi sono il risultato atteso** (piano §11.3; vanno risolti prima della produzione):

| Che cosa si vede | Perché | Che cosa lo corregge |
|---|---|---|
| `curl -sI https://test.it.ivao.aero/` risponde 200 **senza** `content-security-policy` né `x-robots-tag` (solo il `nosniff` dell'hosting) | la home, `index.html`, la consegna il server web da `wwwroot/` senza passare dall'hub. Da lì la visita gira senza CSP | nota `decisions/2026-09-28-gli-header-dei-file-statici.md`, strada A (in coda, e solo se l'avvio a freddo scende abbastanza) |
| La prima richiesta dopo un po' di silenzio aspetta **8–10 s** | Passenger spegne l'hub inattivo dopo 10–30 s, e non si può cambiare da qui | nota `decisions/2026-09-28-l-avvio-a-freddo.md` |
| I lavori pianificati (la coda delle mail, i dati di riferimento, l'uscita dei tour) girano solo mentre l'hub è acceso | lo stesso spegnimento | nota `decisions/2026-09-28-i-job-quando-passenger-spegne-l-hub.md` |

## Il `robots.txt` messo a mano

In `webapp/wwwroot/` c'è un `robots.txt` con `Disallow: /`, messo **a mano** il 27 settembre come tappa. Senza, l'hub
risponde da sé con lo stesso testo (misurato il 28 settembre), ma Cloudflare tiene in cache la versione di prima.

- **Non si cancella** aggiornando, e **non entra mai nel pacchetto**: direbbe `Disallow: /` anche alla produzione.
- Si toglie solo quando è sul server la release che fa servire la home all'hub **e** una misura mostra che `/robots.txt`
  arriva all'applicazione (nota `2026-09-28-gli-header-dei-file-statici`, decisione 2). Dopo, si svuota la cache di
  quell'indirizzo in Cloudflare (lo chiede Carmine a chi amministra).

## Se qualcosa non parte

**Si parte sempre da `webapp/diagnostics/startup-error.txt`**, scaricato via FTP: la riga `reason` (e `cause`, se c'è)
dice il motivo, le righe sopra quale versione, da quale cartella e con quale cartella di lavoro. Non contiene password né
segreti: al loro posto c'è `[redacted]`. Se ha una data **vecchia** è il residuo di un guasto già risolto: un avvio
riuscito lo cancella, quindi l'applicazione non è ripartita. Poi il log del giorno, `webapp/logs/hub-<data>.log`.

| Sintomo | Causa quasi certa |
|---|---|
| In `startup-error.txt` o nel log: «Access denied for user» | l'utente o la password del database nel file dei segreti non sono quelli di Ivao.It: **un `SCRIVI-QUI` o un nome del modello rimasto** (§3, punto 2). È il guasto del primo avvio |
| «Unknown database» | il nome del database nel file dei segreti non è `itivao_test` |
| «An error occurred using the connection to database», senza altro | password, utente o nome del database: rileggete le due righe di `ConnectionStrings` |
| «The division file is missing» | manca `webapp/config/division.json` |
| «'ClientId' is required» (o un altro campo di `Ivao`) | un segnaposto del §3 rimasto vuoto |
| Passenger mostra «Web application could not be started» e **né `startup.txt` né `startup-error.txt`** | l'applicazione non è arrivata a eseguire il suo codice (un `.dll` troncato, un file caricato in ASCII, il comando sbagliato): il motivo è solo nel log di Passenger, e serve Ivao.It |
| Ogni pagina risponde **400 Bad Request** | `AllowedHosts` non dice `test.it.ivao.aero` |
| Il login si ferma su una pagina d'errore di IVAO, o torna con «IVAO ha rifiutato l'accesso» o «Qualcosa è andato storto al ritorno da IVAO» | i tre indirizzi del login non combaciano con quelli registrati da Carmine, oppure ClientId o ClientSecret sono sbagliati |
| Ogni login torna con «il giro di andata e ritorno non è stato riconosciuto» | `hub-keys/` non è scrivibile, o è stata cancellata |
| Dopo un aggiornamento `startup.txt` ha ancora l'ora di prima | risponde il processo vecchio: `restart.txt` non è arrivato in `webapp/tmp/`, o il sito non è stato aperto dopo |
| `Exec format error` | trasferimento FTP in ASCII invece che binario (§2) |

## Le cose da non cancellare mai

| Cosa, in `webapp/` | Se sparisce |
|---|---|
| `hub-keys/` | tutti fuori, e i token IVAO salvati diventano illeggibili: si rientra, ma è evitabile |
| `secrets/` | l'applicazione non trova il database e non parte |
| `config/division.json` | l'applicazione non parte |
| `media/` | spariscono le immagini caricate dallo staff |
| `tmp/` | `restart.txt` non riavvia più niente |
| `wwwroot/robots.txt` | per un po' Cloudflare serve ancora quello vecchio, poi risponde l'hub: nessun danno, ma si toglie solo come dice il paragrafo sopra |

Dopo un aggiornamento: `restart.txt` in `webapp/tmp/` **e poi aprite il sito una volta**, e rileggete la prima riga di
`diagnostics/startup.txt`.

## Backup

Il database **non è tutta l'installazione**. Insieme: il database `itivao_test`, **`hub-keys/`**, **`media/`**, il file
dentro `secrets/` e `config/division.json`. Per una prova è poco, ma `hub-keys/` è la cosa che, persa, si nota. Se e come
il server fa il backup, e come si ripristina, va avuto per iscritto da Ivao.It prima della produzione (piano §15).

## Cosa è stato verificato, e cosa no

**Misurato su questo server** (28 settembre 2026, con la `0.2.1`):

- l'avvio da Passenger con `dotnet IvaoHub.Web.dll` e `webapp/` come cartella di lavoro, le migrazioni su `itivao_test`
  vuoto, il riavvio con `tmp/restart.txt` e una visita;
- `diagnostics/startup-error.txt` che ha detto la causa del primo guasto (l'utente del modello);
- `/api/version`, `/health`, `/sitemap.xml` 404, `startup.txt` con `access private: staff only, not indexed`;
- i file fuori da `wwwroot/` (segreti, configurazione, `.dll`, diagnostica) che rispondono 404 dall'hub;
- il login IVAO vero di Carmine;
- gli header dell'hub su tutto ciò che passa dall'hub, e intatti attraverso Passenger e Cloudflare; la home e i file
  statici senza (limite noto);
- lo schema `https` creduto dall'hub (HSTS, cookie `secure`), e l'indirizzo del visitatore no: `127.0.0.1` nel registro;
- con la pagina diagnostica della `0.2.2`, perché: `X-Forwarded-For` arriva, in due righe, con tre voci (il visitatore, un
  nodo di Cloudflare, `127.0.0.1` del server web davanti a Passenger), e l'hub, che risaliva di un solo passo, si fermava
  alla terza. La `0.2.3` risale finché chi scrive è fidato (nota `decisions/2026-09-28-la-catena-dei-proxy.md`);
- l'avvio a freddo di 8–10 s dopo 10–30 s di silenzio.

**Non ancora verificato**:

- la pagina di chi non è staff, sul server (in sviluppo sì, in italiano e in inglese);
- l'indirizzo del visitatore creduto dalla `0.2.3`, sul server: i test mandano la catena misurata qui, il server non l'ha
  ancora mostrato (le due prove della tabella dei controlli);
- il log anche in `logs/` alla radice (§3, punto 4);
- i lavori pianificati dopo uno spegnimento di Passenger.

**Limiti che restano per scelta**, nella nota `decisions/2026-09-27-l-installazione-di-prova.md`: le pagine pubbliche si
leggono senza entrare da chi conosce l'indirizzo; i file statici (`assets/`, `locales/`, `branding/`) non portano
`X-Robots-Tag` (nota `2026-09-28-gli-header-dei-file-statici`, §7): il `Disallow: /` li copre per i motori che lo leggono.
