# Il marcatore d'inizializzazione

**Data:** 28 settembre 2026
**Stato:** **codice di una decisione già presa da Carmine**, con una domanda nuova (§6).
- Il marcatore: nota `2026-09-28-l-avvio-a-freddo`, domanda 2, decisa da Carmine con i numeri del server: **sì, ora**
  (<https://github.com/SkyMistery/Ivao-Italy-Hub/pull/166#issuecomment-5873177258>). Su `0.2.4`, 8 avvii a freddo: pronto in
  4,2 s (mediana), prima risposta a 5,4 s; due volte su otto Passenger ha spento l'hub 20–30 ms dopo la prima risposta di
  un avvio lento, e la pagina ha pagato un secondo avvio (12 s + 7 s).

Versione **0.2.5**, PATCH: nessuna migrazione (il marcatore è una riga di una tabella che c'è), nessuna pagina.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: si estendono `InitializeAsync`, `hub_division_settings` (che tiene già
le chiavi «applicato una volta» dei seeder e l'hash dei superadmin) e `diagnostics/starts.txt`. Nessun meccanismo nuovo.

## 1. Che cosa fa

Un avvio che trova il database inizializzato **da un avvio uguale** non rifà i passi che quell'avvio ha già fatto:

| Passo | Su un risveglio uguale |
|---|---|
| validazione della configurazione, modelli EF con `VerifyAlternatives` | gira |
| **lettura del marcatore** (nuovo) | gira |
| migrazioni del nucleo, migrazioni dei moduli | **saltato** |
| `PositionGrantSeeder` | **saltato** |
| `ContentSeeder` (modelli, pagine, cruscotti, tipi di calendario) | **saltato** |
| `SuperadminService.BootstrapAsync` | **gira** (§3, scelta di questa sessione, §6) |
| controllo dei dati di riferimento vuoti, `startup.txt` | gira |

Il primo avvio dopo un caricamento (build nuova) fa tutto come prima e poi scrive il marcatore.

## 2. Dove sta, e quando si scrive

- **Nel database**: la riga `startup.initialised` di `hub_division_settings`, un JSON con la chiave (§3), lo stamp della build
  (`0.2.5+abc1234`) e l'ora. Nessuna tabella nuova, nessuna migrazione. Pesate le alternative:
  - **un file in `diagnostics/`**: più economico da leggere (niente query), ma un database ripristinato da una copia vecchia,
    o un'installazione puntata su un altro database, troverebbe un file che dice «già fatto» per uno schema che non ha visto.
    Nel database il marcatore viaggia **con** i dati che descrive: una copia vecchia porta il suo marcatore vecchio, o nessuno.
  - **una riga di `hub_jobs_log`** (la proposta della nota dell'avvio a freddo, §4.3): è un registro, cresce e si pulisce, e
    il marcatore va letto per chiave, non cercato per ultimo esito. `hub_division_settings` è già il posto delle cose «che
    devono sopravvivere a un riavvio» (seeder, hash dei superadmin): una riga in più, stessa forma.
  - **una tabella nuova**: una migrazione (MINOR) per una riga sola. No.
- **Si scrive per ultimo**, dopo che migrazioni, permessi e contenuti sono riusciti. Un avvio che si ferma a metà lascia il
  marcatore che ha trovato, che ha un'altra chiave: l'avvio dopo rifà tutto. Ogni passo è già «applicato una volta sola»,
  quindi rifarlo costa solo tempo. Provato con un test (un passo che lancia: il marcatore resta quello di prima).
- **Due processi insieme** (Passenger a volte ne tiene due, #165): se nessuno dei due trova un marcatore valido, tutti e due
  inizializzano, come ogni avvio faceva prima, e tutti e due scrivono. L'inserimento doppio della riga dà un errore di chiave
  duplicata al secondo, che rilegge e aggiorna. Provato con un test che fa aspettare ognuno dei due l'altro dentro
  l'inizializzazione; togliendo il secondo tentativo il test fallisce (prova di mutazione).
- **Una lettura che fallisce vuol dire un'inizializzazione completa**, mai un avvio fallito. Al primo avvio di
  un'installazione la tabella non c'è: EF scrive una volta nel log un `ERR` «Table … doesn't exist», e subito dopo l'hub
  scrive che il marcatore non si legge e che l'inizializzazione è completa.

## 3. La chiave: gli ingressi di ogni passo saltato

Il rischio vero (nota dell'avvio a freddo, §4.3) è un cambiamento che il passo saltato avrebbe dovuto vedere e che la chiave
non vede. Quindi gli ingressi, passo per passo:

| Passo | Che cosa legge | Nella chiave come |
|---|---|---|
| Migrazioni del nucleo e dei moduli | le migrazioni compilate negli assembly; quali moduli sono accesi; la storia delle migrazioni nel database | **build**: l'MVID di ogni assembly dell'hub (nucleo, web, ogni modulo). La build è deterministica (`Deterministic`), quindi l'MVID cambia **esattamente** quando cambia il codice, migrazioni comprese, qualunque cosa dica il numero di versione, anche in sviluppo con modifiche non committate. **configurazione**: i moduli accesi. La storia è nel database, **con** il marcatore |
| `PositionGrantSeeder` | `division.json → positionGrants`; il catalogo dei permessi (codice + moduli accesi); la riga `positionGrants.seeded` | **configurazione** (le opzioni effettive) e **build**. La riga la scrive solo il seeder |
| `ContentSeeder` | i file di `seed/`; le lingue della divisione; i file di `locales/` e l'enum `Department`; le righe `template.system:*`, `page.*`, `calendar.kind:*` e le righe già scritte a mano | **seme**: ogni file di `seed/`, per percorso e contenuto. **configurazione**: le lingue. **build**: l'enum. `locales/` conta solo quando un seme viene applicato, cioè quando `seed/` è cambiato; a seme uguale il seeder non applica niente |

- **La configurazione è quella effettiva**, non il file: le `DivisionOptions` come le lega l'hub (il JSON di tutte le proprietà),
  perché le impostazioni dell'installazione (`secrets/`, variabili d'ambiente, l'host di prova) si sommano a `division.json`.
  Più i moduli accesi e il nome dell'ambiente. Un campo che nessun passo saltato legge (`domain`, `logoUrl`…) cambia la chiave
  lo stesso: costa un avvio completo in più, mai uno saltato di troppo.
- **Che cosa la chiave non vede**, e non può vedere senza leggere il database, cioè senza rifare il passo: un cambiamento fatto
  **a mano nel database** mentre codice, configurazione e seme restano gli stessi.
  - una riga `template.system:*` cancellata per far riseminare un modello: oggi riseminato all'avvio dopo, con il marcatore
    al primo avvio completo;
  - in sviluppo, `dotnet ef database update <migrazione vecchia>`: le migrazioni tolte non tornano finché il codice non cambia.

  Il rimedio è lo stesso: **cancellare anche la riga `startup.initialised`**, e l'avvio dopo fa tutto. Scritto in
  `CONTRIBUTING.md` («Traps already paid for») e in `docs/DEPLOYING.md`.
- **`SuperadminService` resta a ogni avvio**, ed è la ragione di questa sezione: legge `division.json` quando non c'è più
  nessun superadmin, e scrive una riga d'audit quando l'insieme dei superadmin è cambiato **fuori dall'hub**. Dall'hub
  l'insieme non si svuota mai (l'ultimo non si toglie, un superadmin non si cancella) e ogni cambiamento si registra da sé;
  resta il database modificato a mano, e il server **non isola i database** (`CLAUDE.md` §6: chiunque ci arriva). Saltarlo
  vorrebbe dire scoprire un superadmin aggiunto a mano solo al prossimo caricamento, non al prossimo avvio. Quanto costa
  tenerlo: §5; la domanda a Carmine: §6.

## 4. Che cosa scrive `starts.txt`

Subito dopo `ready in`, nella riga `START`:

```
… START   pid 8        0.2.5+abc1234  ready in 1.20 s  initialisation skipped (marker of 0.2.5+abc1234, 2026-09-28 16:40:02Z): migrations, module migrations, position grants, content  previous: …  steps ms: …, models 655, marker 286, superadmins 68, reference data 7, …
… START   pid 9        0.2.6+def5678  ready in 1.73 s  initialisation full: another build (the marker is of 0.2.5+abc1234)  previous: …  steps ms: …, marker 275, migrations 26, …, content 227, marker written 83, superadmins 20, …
```

- `initialisation full:` dice perché: `no marker`, `another build (the marker is of …)`, `the configuration changed`,
  `seed/ changed`, anche più d'uno.
- I passi saltati non compaiono fra gli `steps ms`; compaiono `marker` (la lettura) e, su un avvio completo, `marker written`.
- `StartupTimings` è ora anche nel contenitore dei servizi: i test d'integrazione leggono da lì che cosa ha fatto un host.

## 5. Le misure

Come la nota dell'avvio più veloce (§3 di quella): `dotnet publish -c Release -r linux-x64 --self-contained` nel contenitore
`mcr.microsoft.com/dotnet/sdk:10.0`; avvio con `dotnet IvaoHub.Web.dll` da un host `aspnet:8.0`, cartella dell'app come cartella
di lavoro, `Production`, MariaDB 11.4.10 già migrato e seminato; un contenitore nuovo per avvio, **una CPU** (`--cpus=1`),
**varianti alternate**, mediana. «main» è `4d424f9` (0.2.4); «marcatore» è questo ramo con il marcatore valido (18 avvii);
«primo avvio» è questo ramo con la riga del marcatore cancellata prima di ogni avvio, cioè il primo avvio dopo un caricamento
(9 avvii).

| | pronto (`ready in`) | `/api/me` risposto, da fuori | rispetto a main |
|---|---:|---:|---:|
| main (0.2.4) | 1,64 s | **2,17 s** | — |
| **marcatore, risveglio uguale** | 1,36 s | **1,91 s** | **−0,26 s (−12%)** |
| primo avvio dopo un caricamento | 1,73 s | 2,28 s | +0,11 s |

I passi, da `starts.txt` (ms, mediane):

| Passo | main | risveglio uguale | primo avvio |
|---|---:|---:|---:|
| modelli EF | 651 | 655 | 661 |
| **marcatore** (lettura) | — | **286** | 275 |
| migrazioni del nucleo | **193** | — | 26 |
| migrazioni dei moduli | 27 | — | 40 |
| superadmin | 121 | **68** | 20 |
| permessi delle posizioni | 26 | — | 29 |
| contenuti | **242** | — | 227 |
| marcatore scritto | — | — | 83 |

- **Il guadagno è metà di quello che dicevano i passi.** Sul server i passi saltati sommavano ~1,0 s (più i superadmin), e la
  decisione stimava ~1,3 s. Ma una buona parte di ogni passo era **la prima query EF del processo**: la costruzione della
  pipeline delle query, che non sparisce, passa al primo che interroga il database. Su `main` la paga
  `GetPendingMigrationsAsync` (193 ms), con il marcatore la paga **la lettura del marcatore** (286 ms per una query di una
  riga). Quello che si toglie davvero è il lavoro dei passi: qui ~0,3 s su 1,64.
- **Leggerlo senza EF non cambia il conto**: la query dei superadmin, e comunque quella della prima richiesta (`/api/me`), la
  pagherebbe al suo posto. Non l'ho fatto: una stringa SQL a mano in più per spostare il costo, non per toglierlo.
- **Sul server, stimato** con il rapporto dei numeri di Carmine (pronto 4,2 s contro 1,64 qui, ×2,6; prima risposta 5,4 s
  contro 2,1): un risveglio uguale **pronto in ~3,5 s, prima risposta a ~4,7 s**, cioè **circa −0,7 s**, non −1,3 s.
  ⚠️ È una stima; lo dirà `starts.txt` del server (§7 e la PR).
- **Il primo avvio dopo un caricamento costa ~0,1 s in più** (la lettura e la scrittura del marcatore): una volta per consegna.
- **La consegna**: fra i due pacchetti cambiano **12 file su 600**, tutti dell'hub (le 4 DLL, `.pdb`, `.xml`, `deps.json`,
  `staticwebassets.endpoints.json`), 15 MB. Il runtime e le librerie di terzi no.

## 6. La domanda a Carmine

**I superadmin a ogni avvio, o saltati anche loro?** La decisione della #166 li elencava fra i passi da saltare. Il codice di
questa PR li tiene **a ogni avvio**, per la ragione del §3: è il controllo che vede un superadmin aggiunto o tolto **a mano nel
database**, e sul server il database lo raggiunge chiunque ne abbia uno. Saltati, un cambiamento così si vedrebbe al prossimo
caricamento, che può essere settimane dopo.
- Il costo, misurato: **68 ms** a un avvio saltato (una CPU), sul server ~0,15 s stimati; parte andrebbe comunque alla prima
  richiesta.
- **Raccomandato: tenerli** (come nel codice). Se la risposta è «saltali», è spostare quattro righe dentro la funzione dei passi
  saltati, e la voce nell'elenco di `starts.txt`.

## 7. Che cosa non è verificato

- **I numeri del server**: il §5 è misurato in locale; sul server li dirà `starts.txt` (le righe `initialisation skipped`).
- **Il primo avvio di un'installazione nuova sul server**: la riga `ERR` di EF per la tabella che non c'è l'ho vista nei test
  (database nuovo) e sul banco e2e, non sul server.
- **Passenger con due processi insieme**: provato con due inizializzazioni concorrenti nello stesso processo di test, su due
  connessioni; due processi veri sul server no.
- `tools/prepare-delivery.ps1`: non lanciato (il cancello lo rifiuta da una chat); il confronto dei file del §5 è per hash.

## Da portare nel piano

- **§11.3 punto 5** (Migrazioni): il marcatore d'inizializzazione, com'è fatto (§1–§3): una riga di `hub_division_settings`,
  la chiave build/configurazione/seme, scritto per ultimo; su un risveglio uguale saltano le migrazioni (nucleo e moduli), i
  permessi delle posizioni e i contenuti; i superadmin girano sempre; il rimedio a un cambiamento a mano (cancellare la riga).
  Toglie la riga «il marcatore resta aperto» della nota dell'avvio a freddo.
- **§11.3 punto 2**: `starts.txt` dice se l'inizializzazione è stata completa (e perché) o saltata (e quali passi).
- **§2.5**, la riga di Passenger: la misura locale con il marcatore (§5: −0,26 s su 2,17 in locale, ~−0,7 s stimati sul
  server, metà della stima della decisione, e perché); quella del server quando c'è.
- **La domanda del §6** (i superadmin a ogni avvio), con la risposta di Carmine.
- `docs/DEPLOYING.md` e `CONTRIBUTING.md` sono aggiornati in questa PR.
