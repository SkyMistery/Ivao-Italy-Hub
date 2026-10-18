# La produzione

**Data:** 18 ottobre 2026 (decisioni del 17 e 18 ottobre)
**Stato:** **decisa da Carmine in chat al master**, 17 ottobre 2026 (§2); i fatti del §3 sono della sessione master di vIPI,
interrogata dal master su richiesta di Carmine lo stesso giorno. Nessun codice: è una nota del master, portata nel piano 1.37.
**Regola applicata:** chiude le voci aperte del piano §15 punti 2c (l'hosting dell'hub) e 3 (il nome della produzione).

## 1. Che cosa serviva

Il piano lasciava aperti il nome della produzione e il modo di ospitarla (§15 punti 2c e 3); E9 (fuori dal repository: il
Gate Manager che legge dall'hub, il primo evento vero, `ivao-booking` spento) ha bisogno di una produzione, e le domande a chi
gestisce il server richiedono tempo.

## 2. Che cosa ha deciso Carmine

1. **Il nome**: la produzione è **`it.ivao.aero`** («passerà da test.it.ivao.aero a it.ivao.aero»).
2. **Un'installazione nuova** (strada A, «Un installazione nuova su it.ivao.aero la A»): un **database suo**, un **client OAuth
   IVAO suo**, registrato da Carmine come per la prova (le credenziali nel file dei segreti sul server, mai in chat). La prova
   `test.it.ivao.aero` **resta**, con `itivao_test`, per provare ogni consegna prima della produzione: i suoi dati di prova non
   passano in produzione.
3. **Carica Carmine**, via FTP, come sulla prova («Io caricherò in produzione»).
4. **Hosting, database, parametri, backup e lavori pianificati: come vIPI** («chiedi a vIPI come funziona, noi faremo uguale, non
   possiamo fare di più»).
5. **I lavori pianificati**: un **Worker di Cloudflare** gemello di quello di vIPI, che chiama `POST /api/jobs/run` con
   `Jobs:Token` (`docs/DEPLOYING.md`), **configurato dalla squadra di vIPI quando il sito sarà pubblico, non ora** («il Worker lo
   configura vIPI, ma non ora, quando faremo il sito pubblico»).
6. **Il messaggio a Ivao.It** (sottodominio, database nuovo, document root, `max_user_connections`, backup) **si manda quando il
   sito è pronto**, non prima («Quando il sito è pronto per ora aspetta»).

## 3. Come fa vIPI (risposta di vIPI Master, 17 ottobre 2026)

- **Cartella e document root**: su `atc.it.ivao.aero` il document root **è** la cartella dell'app (a fine agosto si scaricavano
  `appsettings` e DLL; dal 25 agosto l'hosting risponde 404). Per l'hub si chiede la disposizione della prova: cartella dell'app
  `webapp/`, document root `webapp/wwwroot`.
- **Sottodominio e database li crea Ivao.It**: Carmine non ha il pannello Plesk, solo FTP.
- **Parametri MariaDB** (`max_allowed_packet`, `sql_mode`, `max_user_connections`): nessuno per iscritto; `max_user_connections`
  va chiesto a Ivao.It. All'hub `max_allowed_packet` non pesa: gli upload vanno su disco (piano §2.5).
- **Backup**: vIPI si salva scaricando dal suo sito una copia del database; **per l'hub Carmine l'ha escluso** (nota
  `2026-10-16-la-prova-dei-collegamenti`: dati personali). Resta **solo il backup dell'hosting**, che nessuno conosce: è la prima
  domanda del messaggio a Ivao.It, con la conservazione, prima del primo dato reale. Vale anche per `hub-keys/` e `media/`.
- **Lavori pianificati**: vIPI non ha cron; Passenger spegne il processo e `passenger_min_instances` è stato negato. vIPI usa un
  Worker di Cloudflare con un cron ogni minuto: da qui il punto 5.
- **Consegna**: ⚠️ **mai sovrascrivere via FTP una DLL dell'app accesa** (l'FTP tronca il file e il processo muore: vIPI è rimasto
  giù una notte così); vIPI carica con un nome finto e rinomina. ⚠️ **Non caricare intorno ai minuti :56–:57**: l'hosting taglia le
  connessioni a quell'ora, senza spiegazione. Il foglio di consegna della produzione li dirà.

## 4. Che cosa resta da fare (non ora)

- Al sito pronto: il messaggio a Ivao.It (punto 6) e il Worker (punto 5).
- Il foglio di consegna della produzione (`docs/internal/deploy/`), con le due trappole del §3.
- Prima del primo dato reale: la risposta di Ivao.It sul backup.

## Da portare nel piano

- **§15 punto 3**: il nome della produzione è deciso, `it.ivao.aero`, installazione nuova.
- **§15 punto 2c**: hosting come vIPI; carica Carmine; i lavori pianificati con un Worker di Cloudflare al sito pubblico; il
  messaggio a Ivao.It al sito pronto; il backup solo dall'hosting, da chiedere prima del primo dato reale.
- **§11.3** (il deploy): le due trappole della consegna in produzione (DLL a caldo, minuti :56–:57).
