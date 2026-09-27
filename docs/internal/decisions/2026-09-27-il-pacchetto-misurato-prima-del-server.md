# Il pacchetto misurato prima del server

**Data:** 27 settembre 2026, con la guida al deploy e il foglio della prima installazione di prova
**Stato:** **misure**, non decisioni: le ha fatte la sessione di lavoro sul pacchetto linux-x64 del codice della PR
dell'installazione di prova, in contenitori Linux, con MariaDB 11.4.10. Correggono o precisano righe del piano; l'unica
scelta presa qui è dove sta il foglio in italiano (§3), e resta della revisione.
**Regola applicata:** `CLAUDE.md` §5, caso **(a)**: documentazione, nessun codice.

## 1. Che cosa si è misurato

| Misura | Esito |
|---|---|
| `dotnet IvaoHub.Web.dll` con **solo .NET 8** installato | **parte**: il `dotnet` installato legge il `runtimeconfig`, vede un'app self-contained («Executing as a self-contained app as per config file») e carica `libhostpolicy` 10.0.12 **dal pacchetto** |
| lo stesso, con **tutti i file a `644`** (come dopo un FTP) | **parte**: con `dotnet …dll` il bit di esecuzione non serve |
| `./IvaoHub.Web` senza nessun .NET sul server | parte con `755`; con `644` «Permission denied» |
| il pacchetto su Linux **senza ICU** | si ferma subito: «Couldn't find a valid ICU package» |
| database vuoto, utente con **`GRANT ALL` sul solo suo schema** | tutte le migrazioni passano, `ALTER DATABASE … utf8mb4` compreso |
| `Production`, `Installation:Preview` | `startup.txt` con dominio e accesso; robots `Disallow: /`; sitemap 404; `X-Robots-Tag`, HSTS e `Cache-Control: no-store` su `/api`; 400 a un `Host` non elencato; nessun file fuori da `wwwroot/` servito dall'applicazione |
| `http` senza `X-Forwarded-Proto` | **200, nessun rinvio**: dietro il proxy l'applicazione non ha una porta https a cui mandare |
| un campo OAuth mancante | il motivo va **solo su stdout**: niente in `logs/`, niente `startup.txt` |
| il database irraggiungibile | una riga in `logs/hub-<data>.log`, senza il motivo del server |

## 2. Che cosa ne segue

- **Il comando di avvio del piano (`dotnet IvaoHub.Web.dll`) è giusto** su un server che ha già un `dotnet` qualunque,
  come quello di vIPI, e con quel comando l'FTP che perde il bit di esecuzione non fa danno. `./IvaoHub.Web` serve solo
  su un server senza .NET, e allora vuole il `755`.
- **Il document root va su `wwwroot/`**, e le direttive nginx sono la seconda serratura. Non si possono negare `/media/`
  e `/tiles/` (sono indirizzi dell'applicazione), né tutti i `*.json` (le traduzioni in `wwwroot/locales/`) o tutti gli
  `*.xml` (`/sitemap.xml`).
- **Il `Cache-Control: no-store` su `/api/*` lo manda già l'applicazione**: la direttiva nginx del piano non serve.
- **Il rinvio a https lo fa Cloudflare** («Always Use HTTPS»), non l'applicazione.
- **Quattro cose da guardare prima della produzione**, scritte in `docs/DEPLOYING.md` («Known limits»):
  1. un avvio che fallisce per la configurazione lo dice **solo su stdout**, che con il solo FTP non si legge: vIPI
     l'ha risolto con `diagnostica/avvio-errore.txt`;
  2. Passenger spegne l'applicazione inattiva, e i job pianificati (coda delle mail, dati di riferimento, rilascio dei
     tour) girano solo mentre è accesa; per vIPI `passenger_min_instances` non si può avere (risposta di Ivao.It, 16
     settembre 2026);
  3. i forwarded header si leggono **un salto solo** (`ForwardLimit` di ASP.NET Core, 1): se tra Cloudflare e
     l'applicazione ci sono due salti, l'indirizzo creduto può essere quello di Cloudflare. Il controllo è l'indirizzo
     nel registro delle modifiche, sulla prova;
  4. `<Version>` in `Directory.Build.props` è `0.1.0` e il tag non lo cambia: `/api/version` direbbe 0.1.0 per ogni
     release. Il timbro affidabile, per ora, è il commit.

## 3. Dove sta il foglio in italiano

In `docs/internal/deploy/` (`LEGGIMI-INSTALLAZIONE-DI-PROVA.md` e `segreti.esempio.json`): l'italiano sta solo sotto
`docs/internal/` (`CLAUDE.md` §1), e il foglio è di questa divisione, non di chi forka. La guida generale, in inglese e
senza l'Italia, è `docs/DEPLOYING.md`.

## Da portare nel piano

- **§11.3 punto 2**: il comando `dotnet IvaoHub.Web.dll` resta, con il perché misurato (qualunque `dotnet` installato
  passa al runtime del pacchetto; niente bit di esecuzione); `./IvaoHub.Web` con `755` solo senza .NET. Serve ICU sul
  server. La cartella: `config/` contiene anche `security.json` del pacchetto; `media/` (non `uploads/`).
- **§11.3 punto 3**: document root su `wwwroot/`; l'elenco delle direttive di `docs/DEPLOYING.md`; niente direttiva per
  `no-store` (la manda l'applicazione).
- **§11.3 punto 6**: il bit di esecuzione solo per `./IvaoHub.Web`.
- **§2.5**, riga di Passenger: il comando di vIPI vale anche per l'hub, per la ragione misurata qui.
- **§14** o **§15**: i quattro punti del §2, da chiudere prima della produzione.
