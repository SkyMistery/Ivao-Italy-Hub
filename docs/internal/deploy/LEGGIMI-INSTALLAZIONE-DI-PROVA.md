# Il nuovo hub di IVAO Italia — prima installazione, di prova, su `test.it.ivao.aero`

> **Per chi amministra `it.ivao.aero` (Ivao.It).** È la prima volta che il nuovo hub va su un server, ed è
> un'installazione **di prova e privata**: non la trovano i motori di ricerca, ed entrano **solo lo staff della
> divisione** e i super amministratori. Nessun dato vero di soci da migrare, nessuna mail in uscita.
>
> Il modello è quello di vIPI su `atc.it.ivao.aero` (stesso server, stesso Passenger, stesso FTP), con tre differenze
> che contano: **il document root è `wwwroot/`**, la password del database **la scrivete voi** in un file che non
> passa da noi, e il **timbro** da controllare è la versione **con** il commit. La guida generale, in inglese, è `docs/DEPLOYING.md` del
> repository; questo foglio ne è la versione per il nostro server.

## In breve

| Cosa | Valore |
|---|---|
| Indirizzo | `https://test.it.ivao.aero`, dietro Cloudflare come gli altri |
| Cartella | una cartella sua nella sottoscrizione `it.ivao.aero`; proposta: `/var/www/vhosts/it.ivao.aero/test_hub/` (la sceglie chi amministra) |
| Document root | **`…/test_hub/wwwroot`** — non la cartella dell'applicazione |
| Avvio | Passenger sulla cartella `…/test_hub`, come vIPI: `dotnet IvaoHub.Web.dll` (vedi §2) |
| Database | **suo**: `itivao_hub_test`, con un utente suo; separato da `itivao_atc` di vIPI e dal database della futura produzione |
| Client OAuth IVAO | **suo**, registrato da Carmine per `test.it.ivao.aero` |
| Pacchetto | lo zip della release, self-contained linux-x64 (circa 600 file, 140 MB scompattato): **non serve installare .NET** |

## Che cosa vi consegniamo

1. **Lo zip della consegna**, preparato con `tools/prepare-delivery.ps1` (`docs/DELIVERING.md`) dalla release
   `ivao-division-hub-v<versione>.zip` che GitHub produce dal tag, non dal PC di qualcuno. Dentro, tre rami che non si
   mescolano: la cartella dei file da caricare con il suo `MANIFEST.txt` (per la prima installazione è il pacchetto
   intero), `docs/` con questo foglio e il modello dei segreti, e `restart.txt`. Accanto allo zip, il suo `.sha256`.
2. **`config/division.json`** della divisione italiana, dal repository allo stesso tag. Non è nello zip apposta: è
   dell'installazione, e un aggiornamento non deve sovrascriverlo.
3. **`segreti.esempio.json`**, qui accanto: il modello del file dei segreti (§5).
4. **Questo foglio.**
5. **A parte, da Carmine, per un canale privato** (mai nello zip, mai in una mail in chiaro): `ClientId` e
   `ClientSecret` del client OAuth della prova.

La **password del database** non ve la mandiamo noi: la create voi nel pannello e la scrivete voi nel file (§5).
Così non viaggia da nessuna parte.

## 1. Il database

1. Dal pannello: database **`itivao_hub_test`**, **vuoto**, e un utente suo (per esempio `itivao_hub_test`), con
   **tutti i privilegi su quel database e su nessun altro**.
   ✅ Provato il 27 settembre 2026 su MariaDB 11.4.10 con un utente che aveva `GRANT ALL` solo sul suo database:
   bastano per tutte le migrazioni, compresa la prima istruzione, `ALTER DATABASE … CHARACTER SET utf8mb4`.
2. Il pool dell'applicazione è **al massimo 15 connessioni** (è scritto nel file dei segreti): il tetto per utente
   del server è condiviso, e vIPI ne usa già la sua parte.
3. **Non si importa niente**: le tabelle le crea l'applicazione al primo avvio.

## 2. Il sottodominio, Passenger e Cloudflare

1. **Sottodominio `test.it.ivao.aero`** nella sottoscrizione, con il **document root su `…/test_hub/wwwroot`**.
   È l'impostazione più importante di tutto il foglio: così il server web può consegnare solo i file del sito, e il
   resto (segreti, chiavi, configurazione) non ha un indirizzo. ⚠️ Se il pannello non lo permette, ditecelo prima di
   caricare: le direttive del §3 diventano l'unica difesa, invece della seconda.
2. **Passenger** sulla cartella `…/test_hub`. Il comando di avvio, provato in due modi il 27 settembre 2026:

   | Comando | Quando funziona |
   |---|---|
   | `dotnet IvaoHub.Web.dll` | se sul server c'è un `dotnet` **di qualunque versione** — c'è, perché vIPI parte così. Provato con il solo .NET 8: il `dotnet` installato vede che il pacchetto porta il suo runtime (.NET 10) e passa la mano a quello. **Non serve il bit di esecuzione** |
   | `./IvaoHub.Web` | sempre, anche senza nessun .NET sul server. Ma **serve il bit di esecuzione** su `IvaoHub.Web` (permessi `755`), che l'FTP non trasporta |

   **Consigliato: `dotnet IvaoHub.Web.dll`**, come vIPI. La porta la sceglie Passenger: il comando deve passarla
   all'applicazione **come fate già per vIPI** (con Passenger di solito è `--urls http://127.0.0.1:$PORT` in fondo al
   comando). ⚠️ Questo da qui non l'abbiamo potuto provare.
3. `ASPNETCORE_ENVIRONMENT=Production` come per vIPI: si può anche non mettere, è il valore di partenza.
4. **Cloudflare**: il record DNS `test.it.ivao.aero` **proxato** (nuvola arancione), come `atc`, e *Always Use
   HTTPS* acceso: dietro Cloudflare l'applicazione non rimanda da sé `http` su `https`.

## 3. Le direttive nginx aggiuntive

Nelle **direttive nginx aggiuntive** del sottodominio, come per vIPI:

```nginx
location ~ ^/(secrets|hub-keys|config|diagnostics|logs|seed|tmp)(/|$) { deny all; }
location ~ ^/[^/]+\.(json|dll|pdb|so)$ { deny all; }
location ~ ^/IvaoHub\.[^/]+\.xml$ { deny all; }
location ~ ^/(IvaoHub\.Web|createdump)$ { deny all; }
```

⚠️ **Non negate `/media/` né `/tiles/`**, e non negate tutti i `.json` o tutti gli `.xml`: `/media/…` e `/tiles/…`
sono indirizzi dell'applicazione (le immagini delle pagine, la mappa dei tour), le traduzioni del sito sono
`/locales/…/*.json` e la sitemap è `/sitemap.xml`. Le quattro righe qui sopra sono scritte per non toccarli.

Non serve una direttiva per `Cache-Control: no-store` su `/api/*`: lo manda già l'applicazione (provato).

## 4. Caricare i file

Si carica **il contenuto della cartella dei file da caricare** dello zip della consegna (non `docs/`, non
`restart.txt`), nella cartella vuota `test_hub/`; `MANIFEST.txt` dice quali file sono e con quale impronta.

1. **Se il file manager del pannello sa estrarre uno zip**, potete caricare lo zip, scompattarlo in una cartella a
   parte e spostare in `test_hub/` il contenuto di quella cartella: un trasferimento solo, niente modalità da sbagliare.
2. **Altrimenti FileZilla**, con le regole di vIPI (`LEGGIMI-FTP.md` di vIPI, §2): **trasferimento binario**, non
   «Auto»; si trascina il **contenuto intero** della cartella, così le sottocartelle restano. Alla fine la scheda dei
   trasferimenti falliti dev'essere vuota.
3. Poi, a mano, **accanto** ai file del pacchetto:

   ```
   test_hub/
     config/division.json          ← quello che vi consegniamo (§ «Che cosa vi consegniamo», punto 2)
     secrets/<nome-non-indovinabile>.json   ← §5
     tmp/restart.txt               ← un file di testo vuoto: serve a riavviare, conta la data
   ```

   `hub-keys/`, `logs/`, `diagnostics/` e `media/` li crea l'applicazione. Non importa da quale cartella Passenger la
   avvii: le sue cartelle le trova anche partendo da dove sta `IvaoHub.Web.dll` (dalla `0.2.1`).
4. Permessi `755` su `IvaoHub.Web`. Con `dotnet IvaoHub.Web.dll` non servono (provato), ma non costano niente e
   lasciano possibile l'altro comando: li chiede ogni foglio di consegna.

## 5. Il file dei segreti

1. Copiate `segreti.esempio.json` dentro `test_hub/secrets/` e **rinominatelo con un nome che nessuno può
   indovinare** (va bene `k7f3a91c4e8b2.json`; non vanno `segreti.json`, `password.json`, `config.json`). È la
   stessa regola di vIPI (`LEGGIMI-SEGRETI.md`): la cartella è negata dal server, il nome è la seconda serratura.
   ⚠️ Tutto minuscolo: `secrets`, non `Secrets`. Permessi `700` sulla cartella e `600` sul file, se l'FTP li accetta.
2. **Dentro, scrivete voi la password** del database al posto di `SCRIVI-QUI-LA-PASSWORD-DEL-DATABASE`, e ClientId e
   ClientSecret che vi dà Carmine al posto degli altri due segnaposto. La password compare **due volte**: in `Default`
   (il database dell'hub) e in `AtcData`, la riga con cui l'hub legge **solo** la vista condivisa di vIPI,
   `itivao_atc.v_share_atc_sessions`, con lo stesso utente (il server non lega un utente a un database solo). `AtcData`
   resta spenta finché `division.json` non dice `atcData: vipi`: tenerla scritta non costa niente.
3. **Il resto non si tocca**: il nome del database e dell'utente (se ne avete scelti altri, correggeteli), gli
   indirizzi del login, `AllowedHosts`, le reti fidate (il server stesso e gli intervalli di Cloudflare) e le due
   righe dell'installazione di prova, `Domain` e `Preview`.
4. **Non c'è la sezione della posta, apposta**: senza, l'hub non manda nessuna mail e non perde niente (le mette in
   coda). Una prova non deve scrivere a soci veri.

## 6. Il primo avvio

Aprite `https://test.it.ivao.aero`: è la prima richiesta a far partire l'applicazione. Il primo avvio crea le tabelle
(in prova su una MariaDB 11.4.10 vuota: una ventina di secondi in tutto) e scarica da IVAO FIR e aeroporti della
divisione, quindi deve poter uscire verso `api.ivao.aero`, come vIPI.

## 7. I controlli

Non nel minuto del riavvio: lasciategli il tempo delle migrazioni.

| Che cosa | Che cosa deve dire |
|---|---|
| `diagnostics/startup.txt`, scaricato via FTP | `started at` di **adesso**; `environment Production`; `division IT`; **`domain test.it.ivao.aero`**; **`access private: staff only, not indexed`**; in `root` la cartella dell'applicazione; in `migrations` l'elenco applicato; in `modules` `flightops, training` |
| `diagnostics/startup-error.txt` | **non deve esserci**: un avvio riuscito lo cancella |
| `https://test.it.ivao.aero/api/version` | la **versione e il commit** del tag che vi abbiamo detto (per esempio `0.2.0` e il commit di `v0.2.0`), e `.NET 10…`. Il numero da solo non basta: è il commit a dire quale codice gira |
| `https://test.it.ivao.aero/health` | `Healthy` |
| `https://test.it.ivao.aero/robots.txt` | `User-agent: *` e `Disallow: /`, e nient'altro |
| `https://test.it.ivao.aero/sitemap.xml` | **404**: un'installazione privata non ha sitemap |
| `curl -I https://test.it.ivao.aero/` | la riga `X-Robots-Tag: noindex, nofollow` |
| L'accesso con IVAO di **Carmine** | entra, e in alto compare il suo nome: è il super amministratore del primo avvio |
| L'accesso di un socio che **non** è staff | la pagina «L'accesso non è andato a buon fine» con la frase «Questa è una copia privata del sito, aperta solo allo staff…». **Di lui non resta niente** nel database |
| `curl -I` su `/secrets/<nome del vostro file>.json`, `/config/division.json`, `/appsettings.json`, `/IvaoHub.Web.dll`, `/hub-keys/`, `/diagnostics/startup.txt` | 403 o 404, oppure la pagina del sito: **mai il file** |
| Carmine cambia una cosa innocua e apre il registro delle modifiche | l'indirizzo registrato è **il suo**, non `127.0.0.1` né uno di Cloudflare: è la prova che le reti fidate del §5 sono giuste. Se non lo è, ditecelo: è una riga da cambiare |

## Se qualcosa non parte

| Sintomo | Causa quasi certa |
|---|---|
| Passenger mostra «Web application could not be started» | scaricate **`diagnostics/startup-error.txt`** e mandatecelo: la riga `reason` (e `cause`, se c'è) dice il motivo, le righe sopra quale versione e da quale cartella. Non contiene password né segreti: al loro posto c'è `[redacted]`. Se ha una data **vecchia** è il residuo di un guasto già risolto: un avvio riuscito lo cancella, quindi l'applicazione non è ripartita |
| «Web application could not be started» e **né `startup.txt` né `startup-error.txt`** | l'applicazione non è arrivata a eseguire il suo codice (manca ICU, un `.dll` troncato, il comando sbagliato): il motivo è solo nel log di Passenger, serve chi ha il pannello |
| In `logs/hub-<data>.log`: «An error occurred using the connection to database» | password, utente o nome del database nel file dei segreti |
| «The division file is missing» | manca `config/division.json` |
| «'ClientId' is required» (o un altro campo di `Ivao`) | un segnaposto del §5 rimasto vuoto |
| Ogni pagina risponde **400 Bad Request** | `AllowedHosts` non dice `test.it.ivao.aero` |
| Il login si ferma su una pagina d'errore di IVAO, o torna con «IVAO ha rifiutato l'accesso» o «Qualcosa è andato storto al ritorno da IVAO» | i tre indirizzi del login non combaciano con quelli registrati da Carmine, oppure ClientId o ClientSecret sono sbagliati |
| Ogni login torna con «il giro di andata e ritorno non è stato riconosciuto» | `hub-keys/` non è scrivibile, o è stata cancellata |
| `Permission denied` | avvio con `./IvaoHub.Web` senza il `755` (§4) |
| `Exec format error` | trasferimento FTP in ASCII invece che binario (§4) |
| «Couldn't find a valid ICU package» | manca `libicu` sul server: improbabile, vIPI ne ha bisogno anche lui |

## Le cose da non cancellare mai

Per gli aggiornamenti vale la procedura di vIPI, **`LEGGIMI-AGGIORNARE-VIA-FTP.md`**: mai sovrascrivere un file mentre
l'applicazione gira, si carica col nome finto e si rinomina. Da quella cartella non si cancellano mai:

| Cosa | Se sparisce |
|---|---|
| `hub-keys/` | tutti fuori, e i token IVAO salvati diventano illeggibili: si rientra, ma è evitabile |
| `secrets/` | l'applicazione non trova il database e non parte |
| `config/division.json` | l'applicazione non parte |
| `media/` | spariscono le immagini caricate dallo staff |
| `tmp/` | `restart.txt` non riavvia più niente |

Dopo un aggiornamento: nuovo `restart.txt` in `tmp/` **e poi aprite il sito una volta**, e rileggete la prima riga
di `diagnostics/startup.txt`.

## Backup

Il database **non è tutta l'installazione**. Insieme: il database `itivao_hub_test`, **`hub-keys/`**, **`media/`**,
il file dentro `secrets/` e `config/division.json`. Per una prova è poco, ma `hub-keys/` è la cosa che, persa, si nota.

## Cosa è stato verificato, e cosa no

**Verificato il 27 settembre 2026**, sul pacchetto linux-x64 dello stesso codice, in contenitori Linux con
MariaDB 11.4.10 e un utente con `GRANT ALL` solo sul suo database:

- l'avvio con `dotnet IvaoHub.Web.dll` avendo solo .NET 8 installato, con tutti i file a `644` come dopo un FTP; e
  con `./IvaoHub.Web` senza nessun .NET (con `644` risponde `Permission denied`, con `755` parte);
- le migrazioni da un database vuoto, all'avvio; `diagnostics/startup.txt` con dominio e accesso privato;
- (dalla `0.2.1`) l'avvio da un'altra cartella di lavoro, con la radice trovata dalla cartella dell'applicazione;
  `diagnostics/startup-error.txt` per il file della divisione mancante, un campo OAuth mancante e il database
  irraggiungibile, senza segreti, e cancellato dall'avvio riuscito dopo;
- `/api/version`, `/health`, robots.txt con `Disallow: /`, sitemap 404, `X-Robots-Tag` e HSTS sulle risposte,
  `400` a un `Host` diverso da `test.it.ivao.aero`, e nessun file fuori da `wwwroot/` servito dall'applicazione;
- in sviluppo, la frase della pagina di chi non è staff, in italiano e in inglese.

**Non verificato**, perché si può solo sul server:

- Passenger: come gli arriva la porta, il riavvio con `restart.txt`, che cosa passa in `X-Forwarded-For`;
- il login IVAO vero su `test.it.ivao.aero`;
- se lo zip, scompattato dal pannello, conserva il bit di esecuzione (con `dotnet IvaoHub.Web.dll` non serve).

**Limiti che restano, scritti nella nota `decisions/2026-09-27-l-installazione-di-prova.md`:** le pagine pubbliche si
leggono senza entrare da chi conosce l'indirizzo (se serve chiudere anche quelle, la protezione con password del
pannello lo fa senza codice); Passenger spegne l'applicazione quando nessuno la usa, e i lavori pianificati (la coda
delle mail, i dati di riferimento) girano solo mentre è accesa.
